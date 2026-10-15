#!/usr/bin/env python3
"""Focused public-safe tests for battle_engine_config_decode.py."""

from __future__ import annotations

import csv
import hashlib
import io
import importlib.util
import json
import os
import pathlib
import struct
import subprocess
import sys
import tempfile
import unittest
from dataclasses import replace


TOOL = pathlib.Path(__file__).with_name("battle_engine_config_decode.py")
LAYOUT = (
    pathlib.Path(__file__).resolve().parents[1]
    / "reverse-engineering"
    / "asset-formats"
    / "battle-engine-config-layout.tsv"
)
SPEC = importlib.util.spec_from_file_location("battle_engine_config_decode", TOOL)
assert SPEC is not None and SPEC.loader is not None
decode = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = decode
SPEC.loader.exec_module(decode)


def _cstring(value: bytes) -> bytes:
    return value + b"\0"


def _record(index: int, configuration_name: bytes | None = None) -> bytes:
    if configuration_name is None:
        configuration_name = f"Synthetic {index}".encode("ascii")
    leading_bits = tuple(0x3F000000 + index * 0x100 + field for field in range(9))
    store_bits = tuple(
        value
        for slot in range(6)
        for value in (slot + index * 16, 0x40000000 + index * 0x100 + slot)
    )
    trailing_bits = tuple(0x41000000 + index * 0x100 + field for field in range(6))
    return b"".join(
        (
            struct.pack("<i9I", 12, *leading_bits),
            _cstring(configuration_name),
            struct.pack("<II", 0x42000000 + index, 0x43000000 + index),
            _cstring(f"opaque-a-{index}".encode("ascii")),
            struct.pack("<i", 2),
            _cstring(f"opaque-b-{index}-0".encode("ascii")),
            _cstring(f"opaque-b-{index}-1".encode("ascii")),
            struct.pack("<i", 1),
            _cstring(f"opaque-c-{index}".encode("ascii")),
            struct.pack("<12I", *store_bits),
            struct.pack("<6I", *trailing_bits),
            _cstring(f"opaque-d-{index}".encode("ascii")),
            _cstring(f"opaque-e-{index}".encode("ascii")),
            _cstring(f"opaque-f-{index}".encode("ascii")),
            struct.pack("<I", 0x12340000 + index),
        )
    )


def synthetic_fixture() -> bytes:
    return struct.pack("<i", 6) + b"".join(_record(index) for index in range(6))


_retail_path_text = os.environ.get("BEA_BATTLE_ENGINE_CONFIG_DAT")
RETAIL_PATH = pathlib.Path(_retail_path_text) if _retail_path_text else None


class RoundTripTests(unittest.TestCase):
    def test_synthetic_version_12_file_round_trips_with_complete_coverage(self) -> None:
        data = synthetic_fixture()

        parsed = decode.parse_config(data)

        self.assertEqual(data, decode.encode_config(parsed))
        self.assertEqual(data, decode.encode_config(decode.parse_config(data)))
        self.assertEqual(6, len(parsed.records))
        self.assertEqual([12] * 6, [record.version for record in parsed.records])
        self.assertEqual(len(data), parsed.covered_bytes)
        parsed.assert_complete_coverage(len(data))


class MutationTests(unittest.TestCase):
    def test_supported_fixed_field_mutation_changes_only_its_four_bytes(self) -> None:
        data = synthetic_fixture()
        parsed = decode.parse_config(data)
        original = parsed.records[3].field("mGroundTurnRate")
        # Invert every byte so the locality assertion sees all four positions,
        # not merely the subset whose replacement byte differs.
        replacement_bits = original.u32_bits ^ 0xFFFFFFFF

        mutated = decode.mutate_supported_u32(
            parsed,
            record_index=3,
            field="mGroundTurnRate",
            bits=replacement_bits,
        )
        encoded = decode.encode_config(mutated)

        changed = [
            index
            for index, pair in enumerate(zip(data, encoded))
            if pair[0] != pair[1]
        ]
        self.assertEqual(
            list(range(original.offset, original.end_offset)),
            changed,
        )
        reparsed = decode.parse_config(encoded)
        self.assertEqual(
            replacement_bits,
            reparsed.records[3].field("mGroundTurnRate").u32_bits,
        )
        self.assertEqual(encoded, decode.encode_config(reparsed))

    def test_four_byte_configuration_name_is_not_treated_as_a_u32_field(self) -> None:
        data = struct.pack("<i", 6) + b"".join(
            _record(index, f"N{index}X".encode("ascii")) for index in range(6)
        )
        parsed = decode.parse_config(data)
        self.assertEqual(4, parsed.records[0].field("mConfigurationName").width)

        with self.assertRaisesRegex(
            decode.BattleEngineConfigError,
            "not an admitted fixed-width four-byte field",
        ):
            decode.mutate_supported_u32(
                parsed,
                record_index=0,
                field="mConfigurationName",
                bits=0x41414100,
            )

    def test_mutation_rederives_span_identity_instead_of_trusting_metadata(self) -> None:
        data = synthetic_fixture()
        parsed = decode.parse_config(data)
        record = parsed.records[0]
        actual = record.field("mGroundTurnRate")
        forged_spans = tuple(
            replace(span, field="mConfigurationName")
            if span is actual
            else replace(span, field="mGroundTurnRate", classification=decode.SUPPORTED)
            if span.field == "UNKNOWN_I32_0"
            else span
            for span in record.spans
        )
        forged = replace(
            parsed,
            records=(replace(record, spans=forged_spans), *parsed.records[1:]),
        )

        mutated = decode.mutate_supported_u32(
            forged,
            record_index=0,
            field="mGroundTurnRate",
            bits=actual.u32_bits ^ 0xFFFFFFFF,
        )
        encoded = decode.encode_config(mutated)
        changed = [
            index
            for index, pair in enumerate(zip(data, encoded))
            if pair[0] != pair[1]
        ]

        self.assertEqual(list(range(actual.offset, actual.end_offset)), changed)


class ExactIdentityTests(unittest.TestCase):
    def test_expected_hash_mode_rejects_an_unexpected_input(self) -> None:
        data = synthetic_fixture()
        actual = hashlib.sha256(data).hexdigest()
        unexpected = "0" * 64 if actual != "0" * 64 else "1" * 64

        with self.assertRaisesRegex(decode.BattleEngineConfigError, "SHA-256"):
            decode.parse_config(data, expected_sha256=unexpected)

    def test_exact_layout_mode_revalidates_instead_of_trusting_the_flag(self) -> None:
        parsed = decode.parse_config(synthetic_fixture())

        with self.assertRaisesRegex(decode.BattleEngineConfigError, "baseline size"):
            decode.render_layout_tsv(parsed, exact_baseline=True)


class AdverseControlTests(unittest.TestCase):
    def test_wrong_version_truncation_trailing_and_invalid_framing_are_rejected(self) -> None:
        data = synthetic_fixture()
        parsed = decode.parse_config(data)
        first = parsed.records[0]

        wrong_version = bytearray(data)
        wrong_version[first.offset : first.offset + 4] = struct.pack("<i", 11)
        negative_count = struct.pack("<i", -1) + data[4:]
        impossible_block_count = bytearray(data)
        block_count = first.field("unknown_counted_block_0.count")
        impossible_block_count[block_count.offset : block_count.end_offset] = struct.pack(
            "<i", len(data) + 1
        )
        missing_name_terminator = bytearray(data)
        name = first.field("mConfigurationName")
        missing_name_terminator[name.offset : name.offset + 256] = b"X" * 256

        cases = {
            "wrong version": bytes(wrong_version),
            "truncation": data[:-1],
            "trailing slack": data + b"\0",
            "negative record count": negative_count,
            "impossible counted block": bytes(impossible_block_count),
            "invalid string framing": bytes(missing_name_terminator),
        }
        for name, malformed in cases.items():
            with self.subTest(name=name):
                with self.assertRaises(decode.BattleEngineConfigError):
                    decode.parse_config(malformed)


class DeterministicOutputTests(unittest.TestCase):
    def test_tracked_layout_pins_canonical_lf_bytes_across_checkout_styles(self) -> None:
        checked_out = LAYOUT.read_bytes()
        canonical = checked_out.replace(b"\r\n", b"\n")

        self.assertNotIn(b"\r", canonical)
        self.assertIn(checked_out, (canonical, canonical.replace(b"\n", b"\r\n")))
        self.assertEqual(
            "e2e1e86ca34ac1918c27da4bee6caaf764cbdeb48794d265e606b12c0ad5a3ad",
            hashlib.sha256(canonical).hexdigest(),
        )
        rows = list(
            csv.DictReader(io.StringIO(canonical.decode("utf-8")), delimiter="\t")
        )
        self.assertEqual(
            ["FILE", "Racer", "Standard", "Sniper", "Aquila Prototype", "Laser", "Blaster"],
            [row["record_name"] for row in rows],
        )
        self.assertEqual(1_514, sum(int(row["width"]) for row in rows))
        unknown_bytes = 0
        for row in rows[1:]:
            self.assertEqual(
                "A promoted retail-static destination/consumer join for this exact span; "
                "source order alone is insufficient.",
                row["cheapest_falsifier"],
            )
            for entry in row["unknown_spans"].split(";"):
                field, encoded_range = entry.rsplit("@", 1)
                _offset_text, width_text = encoded_range.split("+", 1)
                self.assertTrue(field.startswith("UNKNOWN"), field)
                unknown_bytes += int(width_text)
        self.assertEqual(689, unknown_bytes)

    def test_layout_and_receipt_are_deterministic_and_omit_unknown_payloads(self) -> None:
        data = synthetic_fixture()
        parsed = decode.parse_config(data)

        first_layout = decode.render_layout_tsv(parsed)
        second_layout = decode.render_layout_tsv(decode.parse_config(data))
        first_receipt = decode.render_receipt_json(parsed, data)
        second_receipt = decode.render_receipt_json(decode.parse_config(data), data)

        self.assertEqual(first_layout, second_layout)
        self.assertEqual(first_receipt, second_receipt)
        self.assertIn("UNKNOWN_CSTRING_0", first_layout)
        self.assertNotIn("opaque-a-0", first_layout)
        self.assertIn("UNPINNED_INPUT_NO_RETAIL_EVIDENCE", first_layout)
        self.assertNotIn("SimulationConstants.cs", first_layout)

        rows = list(csv.DictReader(io.StringIO(first_layout), delimiter="\t"))
        self.assertEqual(len(data), sum(int(row["width"]) for row in rows))
        self.assertEqual(list(range(len(rows))), [int(row["ordinal"]) for row in rows])
        for row in rows[1:]:
            listed: list[tuple[int, int]] = []
            for column in ("frame_spans", "supported_spans", "unknown_spans"):
                for entry in row[column].split(";"):
                    _field, encoded_range = entry.rsplit("@", 1)
                    offset_text, width_text = encoded_range.split("+", 1)
                    listed.append((int(offset_text, 16), int(width_text)))
            listed.sort()
            next_offset = int(row["offset"], 16)
            for offset, width in listed:
                self.assertEqual(next_offset, offset)
                next_offset += width
            self.assertEqual(
                int(row["offset"], 16) + int(row["width"]),
                next_offset,
            )

        receipt = json.loads(first_receipt)
        self.assertEqual(len(data), receipt["size"])
        self.assertEqual(hashlib.sha256(data).hexdigest(), receipt["sha256"])
        self.assertNotIn("path", receipt)
        self.assertEqual(len(data), receipt["coverage"]["coveredBytes"])
        self.assertEqual(0, receipt["coverage"]["gapBytes"])
        self.assertEqual(0, receipt["coverage"]["overlapBytes"])
        self.assertEqual(0, receipt["coverage"]["unclassifiedBytes"])
        with self.assertRaisesRegex(decode.BattleEngineConfigError, "identity"):
            decode.render_receipt_json(parsed, data + b"\0")


class CommandLineTests(unittest.TestCase):
    def test_receipt_mode_emits_the_canonical_lf_form_without_transport_rewrite(self) -> None:
        data = synthetic_fixture()
        canonical = decode.render_receipt_json(
            decode.parse_config(data),
            data,
        ).encode("utf-8")
        with tempfile.TemporaryDirectory() as temporary:
            path = pathlib.Path(temporary) / "synthetic.dat"
            path.write_bytes(data)

            completed = subprocess.run(
                [sys.executable, str(TOOL), str(path), "--receipt"],
                capture_output=True,
                check=False,
            )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual(canonical, completed.stdout)
        self.assertNotIn(b"\r\n", completed.stdout)
        receipt = json.loads(completed.stdout)
        self.assertNotIn("path", receipt)
        self.assertNotIn(b"opaque-a-0", completed.stdout)
        self.assertEqual("bea-battle-engine-config-receipt-v1", receipt["schema"])

    def test_layout_check_accepts_lf_or_crlf_but_rejects_content_drift(self) -> None:
        data = synthetic_fixture()
        layout = decode.render_layout_tsv(decode.parse_config(data)).encode("utf-8")
        crlf_layout = layout.replace(b"\n", b"\r\n")
        with tempfile.TemporaryDirectory() as temporary:
            root = pathlib.Path(temporary)
            input_path = root / "synthetic.dat"
            layout_path = root / "layout.tsv"
            input_path.write_bytes(data)
            layout_path.write_bytes(layout)

            passing_lf = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    str(input_path),
                    "--check-layout",
                    str(layout_path),
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            layout_path.write_bytes(crlf_layout)
            passing_crlf = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    str(input_path),
                    "--check-layout",
                    str(layout_path),
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            layout_path.write_bytes(crlf_layout + b"# drift\r\n")
            failing_drift = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    str(input_path),
                    "--check-layout",
                    str(layout_path),
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            layout_path.write_bytes(layout.replace(b"\n", b"\r", 1))
            failing_bare_cr = subprocess.run(
                [
                    sys.executable,
                    str(TOOL),
                    str(input_path),
                    "--check-layout",
                    str(layout_path),
                ],
                capture_output=True,
                text=True,
                check=False,
            )

        self.assertEqual(0, passing_lf.returncode, passing_lf.stdout + passing_lf.stderr)
        self.assertEqual(
            0,
            passing_crlf.returncode,
            passing_crlf.stdout + passing_crlf.stderr,
        )
        self.assertEqual(
            1,
            failing_drift.returncode,
            failing_drift.stdout + failing_drift.stderr,
        )
        self.assertIn("differs", failing_drift.stderr)
        self.assertEqual(
            1,
            failing_bare_cr.returncode,
            failing_bare_cr.stdout + failing_bare_cr.stderr,
        )
        self.assertIn("bare CR", failing_bare_cr.stderr)


@unittest.skipUnless(
    RETAIL_PATH is not None and RETAIL_PATH.is_file(),
    "BEA_BATTLE_ENGINE_CONFIG_DAT does not name the local hash-pinned retail file",
)
class HashPinnedRetailTests(unittest.TestCase):
    def test_exact_baseline_has_six_source_order_records_and_cited_field_bits(self) -> None:
        assert RETAIL_PATH is not None
        data = RETAIL_PATH.read_bytes()

        parsed = decode.parse_exact_baseline(data)

        self.assertEqual(1_514, len(data))
        self.assertEqual(
            "58722b12a04cae97ad2163acb2cc2c1699f95a0688318bd8a86696714d94454a",
            hashlib.sha256(data).hexdigest(),
        )
        self.assertEqual(
            ["Racer", "Standard", "Sniper", "Aquila Prototype", "Laser", "Blaster"],
            [record.configuration_name for record in parsed.records],
        )
        self.assertEqual(
            [0x004, 0x0D4, 0x1EE, 0x2D2, 0x3DE, 0x4DD],
            [record.offset for record in parsed.records],
        )
        self.assertEqual([12] * 6, [record.version for record in parsed.records])
        self.assertEqual(data, decode.encode_config(parsed))
        self.assertEqual(data, decode.encode_config(decode.parse_exact_baseline(data)))
        parsed.assert_complete_coverage(1_514)
        canonical_layout = decode.render_layout_tsv(
            parsed,
            exact_baseline=True,
        ).encode("utf-8")
        canonical_receipt = decode.render_receipt_json(parsed, data).encode("utf-8")
        checked_out_layout = LAYOUT.read_bytes().replace(b"\r\n", b"\n")
        self.assertNotIn(b"\r", canonical_layout)
        self.assertNotIn(b"\r", canonical_receipt)
        self.assertEqual(
            "e2e1e86ca34ac1918c27da4bee6caaf764cbdeb48794d265e606b12c0ad5a3ad",
            hashlib.sha256(canonical_layout).hexdigest(),
        )
        self.assertEqual(
            "cfb753a6853fa9383efdf607f3f0bc029f78a555bf4125a33ffe4521584cdb33",
            hashlib.sha256(canonical_receipt).hexdigest(),
        )
        self.assertEqual(
            canonical_layout,
            checked_out_layout,
        )

        aquila = parsed.records[3]
        expected_bits = {
            "mLife": 0x41A00000,
            "mEnergy": 0x41000000,
            "mGroundEnergyIncrease": 0x3D4CCCCD,
            "mMaxAirEnergyCost": 0x3C449BA6,
            "mMinTransformEnergy": 0x3F800000,
            "mMaxAirVelocity": 0x3F666666,
            "mGroundVelocity": 0x40A00000,
            "mAirTurnRate": 0x40000000,
            "mGroundTurnRate": 0x3F800000,
            "mShieldEfficiency": 0x42C40000,
            "mStealth": 0x00000000,
            "mMinAirVelocity": 0x3E99999A,
            "mMaxWalkVelocity": 0x3E19999A,
            "mWalkFriction": 0x3F333333,
            "mMinAirEnergyCost": 0x3BA3D70A,
            "mRollEnergyCost": 0x3F800000,
            "mLoopEnergyCost": 0x3F800000,
        }
        self.assertEqual(
            expected_bits,
            {field: aquila.field(field).u32_bits for field in expected_bits},
        )
        self.assertEqual(0x2D6, aquila.field("mLife").offset)
        self.assertEqual(0x2F2, aquila.field("mAirTurnRate").offset)
        self.assertEqual(0x2F6, aquila.field("mGroundTurnRate").offset)

        expected_stores = (
            (0, 0x44FA0000),
            (0, 0x42C80000),
            (1, 0x43160000),
            (0, 0x43480000),
            (1, 0x42C80000),
            (1, 0x42C80000),
        )
        self.assertEqual(
            expected_stores,
            tuple(
                (
                    aquila.field(f"mStoreHeat[{slot}]").u32_bits,
                    aquila.field(f"mStoreValue[{slot}]").u32_bits,
                )
                for slot in range(6)
            ),
        )

    def test_exact_supported_mutation_is_local_and_no_longer_passes_hash_gate(self) -> None:
        assert RETAIL_PATH is not None
        data = RETAIL_PATH.read_bytes()
        parsed = decode.parse_exact_baseline(data)
        target = parsed.records[3].field("mGroundTurnRate")

        mutated = decode.mutate_supported_u32(
            parsed,
            record_index=3,
            field="mGroundTurnRate",
            bits=0x40000000,
        )
        encoded = decode.encode_config(mutated)
        changed = [
            index
            for index, pair in enumerate(zip(data, encoded))
            if pair[0] != pair[1]
        ]

        self.assertEqual([target.offset + 2, target.offset + 3], changed)
        self.assertEqual(encoded, decode.encode_config(decode.parse_config(encoded)))
        with self.assertRaisesRegex(decode.BattleEngineConfigError, "SHA-256"):
            decode.parse_exact_baseline(encoded)


if __name__ == "__main__":
    unittest.main(verbosity=2)
