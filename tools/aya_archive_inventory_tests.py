#!/usr/bin/env python3
"""Generated-fixture tests for the bounded AYA observation producer."""

from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
import stat
import struct
import tempfile
import unittest
import zlib
from pathlib import Path
from unittest import mock
from zipfile import ZipFile

import aya_archive_inventory as inventory
import aya_cross_platform_compare as cross_compare


def _chunk(tag: bytes, payload: bytes = b"") -> bytes:
    if len(tag) != 4:
        raise ValueError("fixture tags are exactly four bytes")
    return tag + struct.pack("<I", len(payload)) + payload


def _compressed_member(raw: bytes) -> bytes:
    compressed = zlib.compress(raw)
    return struct.pack("<I", len(compressed)) + compressed


def _archive(*members: bytes) -> bytes:
    return b"".join(_compressed_member(member) for member in members)


def _ps2_apf(
    blocks: tuple[tuple[str, int, bytes, int, int], ...],
) -> tuple[bytes, bytes]:
    apf = bytearray(_chunk(b"PAGE"))
    aya_size = max(fxup for _, _, _, fxup, _ in blocks) + 4
    aya = bytearray(aya_size)
    for name, mip, tex8, fxup, existing_word in blocks:
        encoded_name = name.encode("ascii")
        if len(encoded_name) >= 128:
            raise ValueError("synthetic name is too long")
        ident = bytearray(144)
        ident[: len(encoded_name)] = encoded_name
        struct.pack_into("<I", ident, 128, mip)
        inner = (
            _chunk(b"IDNT", bytes(ident))
            + _chunk(b"TEX8", tex8)
            + _chunk(b"FXUP", struct.pack("<I", fxup))
        )
        apf.extend(_chunk(b"TBLK", inner))
        aya[fxup - 8 : fxup] = b"PAGE" + struct.pack("<I", 4)
        struct.pack_into("<I", aya, fxup, existing_word)
    return bytes(apf), bytes(aya)


def _mesh_payload(*bodies: bytes) -> bytes:
    payload = bytearray(b"prefix")
    for index, body in enumerate(bodies):
        payload.extend(b"CMSH")
        payload.extend(body)
        if index + 1 < len(bodies):
            payload.extend(b"PMSH")
            payload.extend(struct.pack("<I", 4))
            payload.extend(b"PMS2")
    return bytes(payload)


def _cmsh_body(
    *,
    binding_records: tuple[bytes, ...],
    texb_payloads: tuple[bytes, ...],
    mesh_name: str = "fixture",
    num_parts: int = 1,
) -> bytes:
    if len(binding_records) != len(texb_payloads):
        raise ValueError("each CMST binding record needs one TEXB payload")
    if any(len(record) != 36 for record in binding_records):
        raise ValueError("CMST binding records are exactly 36 bytes")

    root = bytearray(372)
    struct.pack_into("<I", root, 0, 0x0000DEAD)
    struct.pack_into("<I", root, 4, len(binding_records))
    encoded_name = mesh_name.encode("ascii")
    root[36 : 36 + len(encoded_name)] = encoded_name
    struct.pack_into("<I", root, 348, num_parts)

    body = bytearray(_chunk(b"CMSH", bytes(root)))
    body.extend(_chunk(b"CMST", b"".join(binding_records)))
    for texb_payload in texb_payloads:
        body.extend(_chunk(b"MSHT", _chunk(b"TEXB", texb_payload)))
    if num_parts:
        body.extend(_chunk(b"MESP"))
    return bytes(body)


def _text_resource_payload(name: str, trailer: bytes = b"") -> bytes:
    body = b"CTEX" + (b"\0" * 12) + name.encode("ascii") + b"\0" + trailer
    return b"DXTX" + struct.pack("<I", len(body)) + body


def _pc_mesh_resource_payload(name: str, trailer: bytes = b"") -> bytes:
    body = name.encode("ascii") + b"\0" + trailer
    inner = b"PMS2" + struct.pack("<I", len(body)) + body
    return b"PMSH" + struct.pack("<I", len(inner)) + inner


def _xbox_mesh_resource_payload(name: str, trailer: bytes = b"") -> bytes:
    body = name.encode("ascii") + b"\0" + trailer
    return b"PMSH" + struct.pack("<I", len(body)) + body


def _cross_archive(
    raw: bytes, envelope: str
) -> tuple[dict[str, object], bytes, list[dict[str, object]]]:
    source = _archive(raw) if envelope == "pc-chunked-zlib" else raw
    return cross_compare.inspect_archive(
        source=source,
        envelope=envelope,
        stored_length=len(source),
        stored_sha256=hashlib.sha256(source).hexdigest(),
    )


class AyaArchiveInventoryObservationTests(unittest.TestCase):
    maxDiff = None

    def _api(self, name: str):
        value = getattr(inventory, name, None)
        self.assertIsNotNone(value, f"missing Sequence 1A API: {name}")
        return value

    def _error_type(self):
        return self._api("ArchiveObservationError")

    def _assert_category(self, category: str, callback) -> None:
        with self.assertRaises(self._error_type()) as caught:
            callback()
        self.assertEqual(category, caught.exception.category)

    def _observe(self, paths: list[Path]) -> dict[str, object]:
        return self._api("observe_archives")(paths)

    def _render(self, report: dict[str, object]) -> bytes:
        return self._api("render_observation_records")(report)

    def _body_candidates(self, chunk: dict[str, object]) -> list[dict[str, object]]:
        self.assertIn("bodyCandidateObservations", chunk)
        self.assertNotIn("bodyObservations", chunk)
        return chunk["bodyCandidateObservations"]

    def test_contract_surface_and_closed_rejection_categories(self) -> None:
        for name in (
            "read_held_archive",
            "inflate_aya_bytes",
            "merge_ps2_apfs",
            "observe_archives",
            "render_observation_records",
            "resolve_released_resource_route",
            "resolve_ps2_texture_page",
            "validate_numeric_resource_schedule",
        ):
            self.assertTrue(callable(self._api(name)))
        categories = self._api("REJECTION_CATEGORIES")
        self.assertIsInstance(categories, frozenset)
        self.assertEqual(
            {
                "aggregate_body_limit",
                "body_count_limit",
                "body_framing",
                "body_length_limit",
                "changed_held_input",
                "chunk_count_limit",
                "chunk_length_limit",
                "chunk_overrun",
                "compressed_limit",
                "empty_archive",
                "hardlink_input",
                "incomplete_zlib_member",
                "inflate_limit",
                "internal_error",
                "invalid_member_length",
                "member_count_limit",
                "member_length_limit",
                "member_overrun",
                "not_regular_input",
                "reparse_input",
                "trailing_zlib_data",
                "truncated_chunk_header",
                "truncated_member_header",
                "unavailable_input",
                "zlib_member",
            },
            categories,
        )
        self.assertEqual(
            categories | {"raw_tag_stream"}, self._api("ARCHIVE_ERROR_CATEGORIES")
        )

    def test_released_resource_route_plan_preserves_platform_divergences(self) -> None:
        plan = self._api("ReleasedResourceRoutePlan")
        resolve = self._api("resolve_released_resource_route")
        cases = (
            (
                ("PC", -3),
                {},
                plan(r"data\Resources\Loading_res_PC.aya", False, None, None, None, None),
            ),
            (
                ("PS2", -1),
                {},
                plan(
                    r"data\Resources\base_res_PS2.aya",
                    True,
                    None,
                    None,
                    None,
                    r"data\resources\pagefile.mpf",
                ),
            ),
            (
                ("PS2", 7),
                {},
                plan(
                    r"data\Resources\007_res_PS2.aya",
                    True,
                    None,
                    None,
                    r"data\Resources\007_res_PS2.apf",
                    r"data\resources\pagefile.mpf",
                ),
            ),
            (
                ("PS2", -3),
                {"playable_demo": True, "pause_for_showing_controls": True},
                plan(r"data\Resources\Loading_res_PS2_0.aya", False, None, None, None, None),
            ),
            (
                ("Xbox", -1),
                {},
                plan(
                    r"Z:\data\Resources\base_res_XBOX.aya",
                    True,
                    r"D:\data\Resources\base_res_XBOX.aya",
                    r"Z:\data\Resources\base_res_XBOX.aya",
                    None,
                    None,
                ),
            ),
            (
                ("Xbox", -3),
                {},
                plan(
                    r"Z:\data\Resources\Loading_res_XBOX.aya",
                    True,
                    r"D:\data\Resources\Loading_res_XBOX.aya",
                    r"Z:\data\Resources\Loading_res_XBOX.aya",
                    None,
                    None,
                ),
            ),
            (
                ("Xbox", -3),
                {
                    "playable_demo": True,
                    "pause_for_showing_controls": True,
                    "language_index": 4,
                },
                plan(
                    r"data\Resources\Loading_res_XBOX_4.aya",
                    True,
                    None,
                    None,
                    None,
                    None,
                ),
            ),
            (
                ("Xbox", 1234),
                {},
                plan(r"data\Resources\1234_res_XBOX.aya", True, None, None, None, None),
            ),
            (
                ("PC", -1232),
                {},
                plan(r"data\Resources\goodie_232_res_PC.aya", True, None, None, None, None),
            ),
            (
                ("PC", -4),
                {},
                plan(r"data\Resources\goodie_-996_res_PC.aya", True, None, None, None, None),
            ),
        )
        for arguments, keywords, expected in cases:
            with self.subTest(arguments=arguments, keywords=keywords):
                self.assertEqual(expected, resolve(*arguments, **keywords))

        with self.assertRaisesRegex(ValueError, "PC, Xbox, or PS2"):
            resolve("Dreamcast", 1)

    def test_ps2_texture_page_resolver_preserves_selector_rounding_and_bounds(
        self,
    ) -> None:
        resolution = self._api("Ps2TexturePageResolution")
        resolve = self._api("resolve_ps2_texture_page")
        master = bytes(range(96))
        per_resource = bytes(reversed(range(96)))

        self.assertEqual(
            resolution("master_mpf", 16, 16, master[16:32]),
            resolve(
                0x11,
                4,
                4,
                master_page_bytes=master,
                per_resource_page_bytes=per_resource,
            ),
        )
        self.assertEqual(
            resolution("per_resource_apf", 2, 32, per_resource[2:34]),
            resolve(
                0x02,
                5,
                5,
                master_page_bytes=master,
                per_resource_page_bytes=per_resource,
            ),
        )

        failures = (
            (
                (-1, 4, 4),
                {"master_page_bytes": master},
                "nonnegative signed 32-bit",
            ),
            (
                (1, 0, 4),
                {"master_page_bytes": master},
                "positive signed 32-bit",
            ),
            (
                (1, 0x7FFFFFFF, 2),
                {"master_page_bytes": master},
                "safe signed 32-bit domain",
            ),
            (
                (0, 4, 4),
                {"master_page_bytes": master},
                "per_resource_apf bytes are required",
            ),
            (
                (0x59, 4, 4),
                {"master_page_bytes": master},
                r"interval \[88, 104\) exceeds the 96-byte source",
            ),
        )
        for arguments, keywords, message in failures:
            with self.subTest(arguments=arguments, message=message):
                with self.assertRaisesRegex(ValueError, message):
                    resolve(*arguments, **keywords)

    def test_ps2_apf_merger_preserves_explicit_order_and_patches_copies(
        self,
    ) -> None:
        merge_input = self._api("Ps2ApfMergeInput")
        merge = self._api("merge_ps2_apfs")
        first_apf, first_aya = _ps2_apf(
            (("Textures\\First.tga", 0, b"A" * 16, 24, 1),)
        )
        second_apf, second_aya = _ps2_apf(
            (("Textures\\Second.tga", 0, b"B" * 32, 40, 17),)
        )

        result = merge(
            (
                merge_input("base", first_apf, first_aya),
                merge_input("201", second_apf, second_aya),
            )
        )

        self.assertEqual(b"A" * 16 + b"B" * 32, result.master_page_bytes)
        self.assertEqual((first_aya, second_aya), result.patched_aya_bytes)
        self.assertEqual(2, result.block_count)
        self.assertEqual(2, result.texture_name_count)
        self.assertEqual(2, result.texture_key_count)
        self.assertEqual(
            hashlib.sha256(result.master_page_bytes).hexdigest(),
            result.master_sha256,
        )

    def test_ps2_apf_merger_can_patch_unmodified_aya_without_mutating_input(
        self,
    ) -> None:
        merge_input = self._api("Ps2ApfMergeInput")
        merge = self._api("merge_ps2_apfs")
        apf, released_aya = _ps2_apf(
            (("Texture.tga", 0, b"X" * 16, 24, 1),)
        )
        unpatched_aya = bytearray(released_aya)
        struct.pack_into("<I", unpatched_aya, 24, 0)
        original = bytes(unpatched_aya)

        result = merge(
            (merge_input("base", apf, unpatched_aya),),
            require_matching_page_words=False,
        )

        self.assertEqual(original, bytes(unpatched_aya))
        self.assertEqual(
            1,
            struct.unpack_from("<I", result.patched_aya_bytes[0], 24)[0],
        )

    def test_ps2_apf_merger_does_not_content_deduplicate_distinct_names(
        self,
    ) -> None:
        merge_input = self._api("Ps2ApfMergeInput")
        merge = self._api("merge_ps2_apfs")
        apf, aya = _ps2_apf(
            (
                ("A8_warspite-PAGE1.tga", 0, b"W" * 16, 24, 1),
                ("warspite-PAGE1.tga", 0, b"W" * 16, 48, 17),
            )
        )

        result = merge((merge_input("level", apf, aya),))

        self.assertEqual(b"W" * 32, result.master_page_bytes)
        self.assertEqual(2, result.block_count)
        self.assertEqual(2, result.texture_key_count)

    def test_ps2_apf_merger_rejects_ambiguous_or_malformed_inputs(self) -> None:
        merge_input = self._api("Ps2ApfMergeInput")
        merge = self._api("merge_ps2_apfs")
        valid_apf, valid_aya = _ps2_apf(
            (("Texture.tga", 0, b"X" * 16, 24, 1),)
        )
        duplicate_apf, duplicate_aya = _ps2_apf(
            (("TEXTURE.TGA", 0, b"X" * 16, 24, 17),)
        )
        no_sentinel = valid_apf[8:]
        wrong_word = bytearray(valid_aya)
        struct.pack_into("<I", wrong_word, 24, 33)
        wrong_page_owner = bytearray(valid_aya)
        wrong_page_owner[16:20] = b"NOPE"
        bad_length_apf, bad_length_aya = _ps2_apf(
            (("Odd.tga", 0, b"Y" * 15, 24, 1),)
        )

        failures = (
            (
                (merge_input("base", no_sentinel, valid_aya),),
                "begin with PAGE",
            ),
            (
                (
                    merge_input("base", valid_apf, valid_aya),
                    merge_input("level", duplicate_apf, duplicate_aya),
                ),
                "duplicate case-folded texture name and mip",
            ),
            (
                (merge_input("base", valid_apf, wrong_word),),
                "does not match merge order",
            ),
            (
                (merge_input("base", valid_apf, wrong_page_owner),),
                "does not name a PAGE",
            ),
            (
                (merge_input("base", bad_length_apf, bad_length_aya),),
                "positive multiple of 16",
            ),
        )
        for inputs, message in failures:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    merge(inputs)

    def test_numeric_resource_schedule_accepts_writer_order_and_rejects_first_drift(
        self,
    ) -> None:
        prefix = (
            _chunk(b"LVLR", b"\0" * 4)
            + _chunk(b"TARG", b"\0" * 4)
            + _chunk(b"AYAD", b"\0" * 24)
        )
        tail = b"".join(
            _chunk(tag)
            for tag in (b"IMPS", b"LNDS", b"SURF", b"ERES", b"SSHD", b"WRES")
        )
        validate = self._api("validate_numeric_resource_schedule")
        parse = self._api("parse_top_level_chunks_bounded")

        validate(parse(prefix + _chunk(b"TEXT") * 2 + _chunk(b"MESH") + tail))

        cases = (
            (
                prefix + _chunk(b"MESH") + _chunk(b"TEXT") + tail,
                r"expected IMPS, got TEXT",
            ),
            (
                prefix + _chunk(b"TEXT") + _chunk(b"MESH")
                + _chunk(b"LNDS") + _chunk(b"IMPS")
                + b"".join(
                    _chunk(tag) for tag in (b"SURF", b"ERES", b"SSHD", b"WRES")
                ),
                r"expected IMPS, got LNDS",
            ),
        )
        for raw, message in cases:
            with self.subTest(message=message):
                with self.assertRaisesRegex(ValueError, message):
                    validate(parse(raw))

    def test_observation_v1_rejects_raw_stream_only_rejection_category(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "empty.aya"
            source.write_bytes(b"")
            report = self._observe([source])
        report["archiveRecords"][0]["rejectionCategory"] = "raw_tag_stream"
        report["sourceUniverseId"] = inventory._source_universe_id(report["archiveRecords"])
        with self.assertRaises(ValueError):
            self._render(report)
        with self.assertRaises(ValueError):
            self._error_type()("not-a-closed-category")

    def test_deterministic_path_free_records_keep_duplicate_sources_distinct(self) -> None:
        fixture = _archive(_chunk(b"AYAD", b"same"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = root / "private-alpha.aya"
            second = root / "private-beta.aya"
            first.write_bytes(fixture)
            second.write_bytes(fixture)

            forward = self._render(self._observe([second, first]))
            reverse = self._render(self._observe([first, second]))
            self.assertEqual(forward, reverse)
            self.assertTrue(forward.endswith(b"\n"))
            decoded = json.loads(forward)
            self.assertEqual("onslaught.aya-archive-observation.v1", decoded["schemaVersion"])
            self.assertEqual(
                {
                    "name": "aya_archive_inventory",
                    "profileVersion": "bounded-observation-v1",
                    "producerVersion": 1,
                },
                decoded["producer"],
            )
            records = decoded["archiveRecords"]
            self.assertEqual(["archive-0001", "archive-0002"], [item["archiveOrdinal"] for item in records])
            self.assertEqual(records[0]["sourceIdentity"], records[1]["sourceIdentity"])
            self.assertNotEqual(records[0]["archiveOrdinal"], records[1]["archiveOrdinal"])
            rendered = forward.decode("utf-8")
            self.assertNotIn(str(root), rendered)
            self.assertNotIn("private-alpha", rendered)
            self.assertNotIn("private-beta", rendered)

    def test_unequal_content_order_is_independent_of_private_names_and_locations(self) -> None:
        first_fixture = _archive(_chunk(b"AYAD", b"first-public-content"))
        second_fixture = _archive(_chunk(b"TARG", b"second-public-content"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first_location = root / "first-private-location"
            second_location = root / "second-private-location"
            first_location.mkdir()
            second_location.mkdir()

            first_alpha = first_location / "alpha-private-name.aya"
            first_zulu = first_location / "zulu-private-name.aya"
            second_alpha = second_location / "alpha-renamed.aya"
            second_zulu = second_location / "zulu-renamed.aya"
            first_alpha.write_bytes(first_fixture)
            first_zulu.write_bytes(second_fixture)
            second_alpha.write_bytes(second_fixture)
            second_zulu.write_bytes(first_fixture)

            original = self._render(self._observe([first_zulu, first_alpha]))
            relocated = self._render(self._observe([second_zulu, second_alpha]))

        self.assertEqual(original, relocated)
        original_report = json.loads(original)
        relocated_report = json.loads(relocated)
        self.assertEqual(original_report["sourceUniverseId"], relocated_report["sourceUniverseId"])
        self.assertEqual(
            ["archive-0001", "archive-0002"],
            [record["archiveOrdinal"] for record in original_report["archiveRecords"]],
        )

    def test_multipart_cardinality_and_unknown_tag_bytes_are_preserved(self) -> None:
        raw_one = _chunk(b"AYAD", b"one")
        raw_two = _chunk(b"\x00A\xffZ", b"two")
        fixture = _archive(raw_one, raw_two)
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "input.AYA"
            source.write_bytes(fixture)
            record = self._observe([source])["archiveRecords"][0]
        self.assertEqual("observed", record["observationStatus"])
        self.assertIsNone(record["rejectionCategory"])
        self.assertEqual(".aya", record["extension"])
        self.assertEqual(2, record["memberCount"])
        chunks = record["chunkObservations"]
        self.assertEqual(["chunk-0001", "chunk-0002"], [item["chunkOrdinal"] for item in chunks])
        self.assertEqual("AYAD", chunks[0]["tagAscii"])
        self.assertEqual("41594144", chunks[0]["tagHex"])
        self.assertEqual(".A.Z", chunks[1]["tagAscii"])
        self.assertEqual("0041ff5a", chunks[1]["tagHex"])

    def test_mesh_body_candidates_are_explicitly_candidate_only_and_one_to_one(self) -> None:
        fixture = _archive(_chunk(b"MESH", _mesh_payload(b"first", b"second")))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "mesh.aya"
            source.write_bytes(fixture)
            record = self._observe([source])["archiveRecords"][0]
        candidates = self._body_candidates(record["chunkObservations"][0])
        self.assertEqual(
            ["body-candidate-0001", "body-candidate-0002"],
            [item["candidateOrdinal"] for item in candidates],
        )
        self.assertTrue(all(item["tagAscii"] == "CMSH" for item in candidates))
        self.assertTrue(all(item["tagHex"] == "434d5348" for item in candidates))
        self.assertTrue(all(item["evidenceKind"] == "candidate-only" for item in candidates))
        self.assertTrue(
            all(
                item["boundaryRule"] == "cmsh-to-next-pmsh-pms2-or-mesh-end"
                for item in candidates
            )
        )
        self.assertTrue(
            all(
                set(item)
                == {
                    "boundaryRule",
                    "candidateOrdinal",
                    "evidenceKind",
                    "length",
                    "sha256",
                    "tagAscii",
                    "tagHex",
                }
                for item in candidates
            )
        )

    def test_repeated_body_marker_without_wrapper_is_rejected_as_ambiguous(self) -> None:
        self._assert_category(
            "body_framing",
            lambda: self._api("observe_embedded_bodies")(b"CMSHoneCMSHtwo"),
        )

    def test_rejected_record_is_terminal_closed_and_redacts_private_input(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "do-not-leak-this-name.aya"
            source.write_bytes(struct.pack("<I", 12) + b"short")
            rendered = self._render(self._observe([source]))
        record = json.loads(rendered)["archiveRecords"][0]
        self.assertEqual("rejected", record["observationStatus"])
        self.assertEqual("member_overrun", record["rejectionCategory"])
        self.assertEqual([], record["chunkObservations"])
        self.assertNotIn("do-not-leak", rendered.decode("utf-8"))
        self.assertNotIn(str(root), rendered.decode("utf-8"))

    def test_read_held_archive_accepts_regular_single_link_and_rejects_hardlink(self) -> None:
        fixture = _archive(_chunk(b"AYAD"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source.aya"
            alias = root / "alias.aya"
            source.write_bytes(fixture)
            self.assertEqual(fixture, self._api("read_held_archive")(source))
            os.link(source, alias)
            self._assert_category("hardlink_input", lambda: self._api("read_held_archive")(alias))

    def test_read_held_archive_rejects_reparse_and_same_handle_change(self) -> None:
        fixture = _archive(_chunk(b"AYAD"))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            with mock.patch.object(inventory, "_path_has_reparse", return_value=True):
                self._assert_category("reparse_input", lambda: self._api("read_held_archive")(source))

            real_fstat = os.fstat
            calls = 0

            def changed_fstat(descriptor: int):
                nonlocal calls
                calls += 1
                metadata = real_fstat(descriptor)
                if calls == 2:
                    values = list(metadata)
                    values[6] += 1
                    return os.stat_result(values)
                return metadata

            with mock.patch.object(inventory.os, "fstat", side_effect=changed_fstat):
                self._assert_category("changed_held_input", lambda: self._api("read_held_archive")(source))

    def test_read_held_archive_rechecks_reparse_components_after_read(self) -> None:
        fixture = _archive(_chunk(b"AYAD"))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            with mock.patch.object(inventory, "_path_has_reparse", side_effect=[False, True]):
                self._assert_category("reparse_input", lambda: self._api("read_held_archive")(source))

    def test_read_held_archive_rejects_post_close_path_identity_change(self) -> None:
        fixture = _archive(_chunk(b"AYAD"))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            real_lstat = os.lstat
            source_calls = 0

            def changed_lstat(path):
                nonlocal source_calls
                metadata = real_lstat(path)
                if os.path.normcase(os.fspath(path)) == os.path.normcase(os.fspath(source)):
                    source_calls += 1
                    if source_calls >= 3:
                        values = list(metadata)
                        values[6] += 1
                        return os.stat_result(values)
                return metadata

            with mock.patch.object(inventory.os, "lstat", side_effect=changed_lstat):
                self._assert_category("changed_held_input", lambda: self._api("read_held_archive")(source))

    def test_compressed_bytes_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        fixture = _archive(_chunk(b"AYAD", b"payload"))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            with mock.patch.object(inventory, "MAX_COMPRESSED_BYTES", len(fixture), create=True):
                self.assertEqual(fixture, self._api("read_held_archive")(source))
            with mock.patch.object(inventory, "MAX_COMPRESSED_BYTES", len(fixture) - 1, create=True):
                self._assert_category("compressed_limit", lambda: self._api("read_held_archive")(source))

    def test_member_count_and_member_length_caps_are_independent(self) -> None:
        raw = _chunk(b"AYAD")
        one = _compressed_member(raw)
        two = one + one
        three = two + one
        compressed_length = struct.unpack_from("<I", one, 0)[0]
        with mock.patch.object(inventory, "MAX_MEMBERS", 2, create=True):
            self.assertEqual(raw + raw, self._api("inflate_aya_bytes")(two))
            self._assert_category("member_count_limit", lambda: self._api("inflate_aya_bytes")(three))
        with mock.patch.object(inventory, "MAX_MEMBER_COMPRESSED_BYTES", compressed_length, create=True):
            self.assertEqual(raw, self._api("inflate_aya_bytes")(one))
        with mock.patch.object(inventory, "MAX_MEMBER_COMPRESSED_BYTES", compressed_length - 1, create=True):
            self._assert_category("member_length_limit", lambda: self._api("inflate_aya_bytes")(one))

    def test_inflated_bytes_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        raw = _chunk(b"AYAD", b"bounded")
        fixture = _archive(raw)
        with mock.patch.object(inventory, "MAX_INFLATED_BYTES", len(raw), create=True):
            self.assertEqual(raw, self._api("inflate_aya_bytes")(fixture))
        with mock.patch.object(inventory, "MAX_INFLATED_BYTES", len(raw) - 1, create=True):
            self._assert_category("inflate_limit", lambda: self._api("inflate_aya_bytes")(fixture))

    def test_member_framing_and_decompressor_states_fail_closed(self) -> None:
        raw = _chunk(b"AYAD", b"payload")
        compressed = zlib.compress(raw)
        cases = {
            "empty_archive": b"",
            "truncated_member_header": b"\x01\x02\x03",
            "invalid_member_length": struct.pack("<I", 0),
            "member_overrun": struct.pack("<I", len(compressed) + 1) + compressed,
            "zlib_member": struct.pack("<I", 4) + b"nope",
            "incomplete_zlib_member": struct.pack("<I", len(compressed) - 2) + compressed[:-2],
            "trailing_zlib_data": struct.pack("<I", len(compressed) + 1) + compressed + b"x",
        }
        for category, fixture in cases.items():
            with self.subTest(category=category):
                self._assert_category(category, lambda fixture=fixture: self._api("inflate_aya_bytes")(fixture))

    def test_top_level_chunk_count_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        one = _chunk(b"AYAD")
        two = one + _chunk(b"TARG")
        with mock.patch.object(inventory, "MAX_TOP_LEVEL_CHUNKS", 1, create=True):
            self.assertEqual(1, len(self._api("parse_top_level_chunks_bounded")(one)))
            self._assert_category(
                "chunk_count_limit", lambda: self._api("parse_top_level_chunks_bounded")(two)
            )

    def test_raw_console_envelope_is_explicit_bounded_and_auto_detectable(self) -> None:
        raw = _chunk(b"LVLR", b"\x67\x00\x00\x00") + _chunk(b"TARG", b"target")
        decoded, chunks, members, envelope = self._api("decode_archive_envelope")(
            raw, "raw-tag-stream"
        )
        self.assertEqual(raw, decoded)
        self.assertEqual(["LVLR", "TARG"], [chunk.tag for chunk in chunks])
        self.assertEqual(0, members)
        self.assertEqual("raw-tag-stream", envelope)
        self.assertEqual(
            (raw, chunks, 0, "raw-tag-stream"),
            self._api("decode_archive_envelope")(raw, "auto"),
        )

    def test_raw_console_envelope_rejects_shifted_random_and_unknown_tags(self) -> None:
        valid = _chunk(b"LVLR", b"\x67\x00\x00\x00") + _chunk(b"TARG", b"target")
        cases = (
            valid[1:],
            bytes(range(64)),
            _chunk(b"NOPE") + _chunk(b"TARG"),
            _chunk(b"LVLR"),
        )
        for fixture in cases:
            with self.subTest(fixture=fixture[:8].hex()):
                self._assert_category(
                    "raw_tag_stream",
                    lambda fixture=fixture: self._api("decode_archive_envelope")(
                        fixture, "raw-tag-stream"
                    ),
                )

    def test_per_chunk_length_cap_and_malformed_chunk_states(self) -> None:
        payload = b"12345"
        raw = _chunk(b"AYAD", payload)
        with mock.patch.object(inventory, "MAX_CHUNK_BYTES", len(payload), create=True):
            self.assertEqual(1, len(self._api("parse_top_level_chunks_bounded")(raw)))
        with mock.patch.object(inventory, "MAX_CHUNK_BYTES", len(payload) - 1, create=True):
            self._assert_category(
                "chunk_length_limit", lambda: self._api("parse_top_level_chunks_bounded")(raw)
            )
        self._assert_category(
            "truncated_chunk_header", lambda: self._api("parse_top_level_chunks_bounded")(b"AYAD\x00")
        )
        self._assert_category(
            "chunk_overrun",
            lambda: self._api("parse_top_level_chunks_bounded")(b"AYAD" + struct.pack("<I", 10) + b"x"),
        )

    def test_body_count_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        one = _mesh_payload(b"one")
        two = _mesh_payload(b"one", b"two")
        with mock.patch.object(inventory, "MAX_EMBEDDED_BODIES", 1, create=True):
            self.assertEqual(1, len(self._api("observe_embedded_bodies")(one)))
            self._assert_category("body_count_limit", lambda: self._api("observe_embedded_bodies")(two))

    def test_importer_mesh_probe_keeps_pc_and_ps2_material_profiles_distinct(self) -> None:
        lane_words = (
            (0x3F800000, 0x3F000000),
            (0x00000000, 0x3E800000),
            (0x00000000, 0x3E000000),
            (0x3F800000, 0x3F000000),
            (0x3F800000, 0x40000000),
        )
        ps2_record = bytearray(36)
        struct.pack_into("<I", ps2_record, 8, 2)
        struct.pack_into("<I", ps2_record, 32, 0x3F800000)
        ps2_texb = b"".join(
            struct.pack("<I", word) for lane in lane_words for word in lane
        ) + struct.pack("<I", 7)
        ps2_body = _cmsh_body(
            binding_records=(bytes(ps2_record),),
            texb_payloads=(ps2_texb,),
            mesh_name="ps2-fixture",
        )

        ps2_probe = inventory._probe_importer_mesh_body(ps2_body, profile="ps2")
        self.assertTrue(ps2_probe["plausible"])
        self.assertEqual("ps2", ps2_probe["profile"])
        self.assertEqual([], ps2_probe["first_texture_names"])
        self.assertEqual(
            {
                "sample_count": 2,
                "lane_words_u32": [list(lane) for lane in lane_words],
                "texture_key": 7,
                "cached_max_lane0_bits": 0x3F800000,
            },
            ps2_probe["first_texture_bindings"][0],
        )
        self.assertEqual(len(ps2_body) - 8, ps2_probe["first_part_offset"])
        self.assertFalse(inventory._probe_importer_mesh_body(ps2_body)["plausible"])

        pc_record = bytes(36)
        pc_name = b"meshtex\\panel.tga"
        pc_texb = bytes(20) + pc_name + bytes(128 - len(pc_name))
        pc_body = _cmsh_body(
            binding_records=(pc_record,),
            texb_payloads=(pc_texb,),
            mesh_name="pc-fixture",
        )
        pc_probe = inventory._probe_importer_mesh_body(pc_body)
        self.assertTrue(pc_probe["plausible"])
        self.assertEqual("pc", pc_probe["profile"])
        self.assertEqual(["meshtex\\panel.tga"], pc_probe["first_texture_names"])
        self.assertEqual([], pc_probe["first_texture_bindings"])
        self.assertFalse(inventory._probe_importer_mesh_body(pc_body, profile="ps2")["plausible"])

        malformed = bytearray(ps2_body)
        struct.pack_into("<I", malformed, 388 + 8, 3)
        malformed_probe = inventory._probe_importer_mesh_body(bytes(malformed), profile="ps2")
        self.assertFalse(malformed_probe["plausible"])
        self.assertIn("20*3+4", malformed_probe["error"])

    def test_body_length_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        payload = _mesh_payload(b"12345")
        body_length = len(b"CMSH12345")
        with mock.patch.object(inventory, "MAX_BODY_BYTES", body_length, create=True):
            self.assertEqual(body_length, self._api("observe_embedded_bodies")(payload)[0]["length"])
        with mock.patch.object(inventory, "MAX_BODY_BYTES", body_length - 1, create=True):
            self._assert_category("body_length_limit", lambda: self._api("observe_embedded_bodies")(payload))

    def test_aggregate_body_cap_accepts_cap_and_rejects_cap_plus_one(self) -> None:
        payload = _mesh_payload(b"one", b"two")
        observed = self._api("observe_embedded_bodies")(payload)
        aggregate = sum(item["length"] for item in observed)
        with mock.patch.object(inventory, "MAX_AGGREGATE_BODY_BYTES", aggregate, create=True):
            self.assertEqual(2, len(self._api("observe_embedded_bodies")(payload)))
        with mock.patch.object(inventory, "MAX_AGGREGATE_BODY_BYTES", aggregate - 1, create=True):
            self._assert_category(
                "aggregate_body_limit", lambda: self._api("observe_embedded_bodies")(payload)
            )

    def test_internal_os_error_is_path_free_and_terminal(self) -> None:
        private = r"C:\private\retail\secret.aya"
        with mock.patch.object(inventory, "read_held_archive", side_effect=OSError(private)):
            report = self._observe([Path(private)])
        rendered = self._render(report).decode("utf-8")
        self.assertIn('"rejectionCategory":"internal_error"', rendered)
        self.assertNotIn("private", rendered.lower())
        self.assertNotIn("retail", rendered.lower())
        self.assertNotIn("secret", rendered.lower())

    def test_renderer_rejects_extra_fields_duplicate_ordinals_and_changed_universe(self) -> None:
        fixture = _archive(_chunk(b"AYAD"))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            report = self._observe([source])

        injected = json.loads(json.dumps(report))
        injected["archiveRecords"][0]["machinePath"] = r"C:\private\source.aya"
        with self.assertRaises(ValueError):
            self._render(injected)

        duplicated = json.loads(json.dumps(report))
        duplicated["archiveRecords"].append(dict(duplicated["archiveRecords"][0]))
        with self.assertRaises(ValueError):
            self._render(duplicated)

        changed_universe = json.loads(json.dumps(report))
        changed_universe["sourceUniverseId"] = "0" * 64
        with self.assertRaises(ValueError):
            self._render(changed_universe)

    def test_renderer_rejects_reordered_unequal_records_with_recomputed_identity(self) -> None:
        first_fixture = _archive(_chunk(b"AYAD", b"first"))
        second_fixture = _archive(_chunk(b"TARG", b"second"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = root / "first.aya"
            second = root / "second.aya"
            first.write_bytes(first_fixture)
            second.write_bytes(second_fixture)
            canonical = self._observe([first, second])

        canonical_bytes = self._render(canonical)
        reordered = json.loads(json.dumps(canonical))
        reordered["archiveRecords"].reverse()
        for ordinal, record in enumerate(reordered["archiveRecords"], 1):
            record["archiveOrdinal"] = f"archive-{ordinal:04d}"
        reordered["sourceUniverseId"] = inventory._source_universe_id(
            reordered["archiveRecords"]
        )

        with self.assertRaises(ValueError):
            self._render(reordered)
        self.assertEqual(canonical_bytes, self._render(canonical))

    def test_renderer_rejects_type_cap_arithmetic_tag_and_body_scope_mismatches(self) -> None:
        fixture = _archive(_chunk(b"MESH", _mesh_payload(b"body")))
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source.aya"
            source.write_bytes(fixture)
            report = self._observe([source])

        def changed(callback):
            candidate = json.loads(json.dumps(report))
            callback(candidate["archiveRecords"][0])
            candidate["sourceUniverseId"] = inventory._source_universe_id(candidate["archiveRecords"])
            return candidate

        cases = {
            "boolean source length": lambda record: record["sourceIdentity"].__setitem__("length", True),
            "source cap": lambda record: record["sourceIdentity"].__setitem__(
                "length", inventory.MAX_COMPRESSED_BYTES + 1
            ),
            "boolean inflated length": lambda record: record.__setitem__("inflatedLength", True),
            "inflated cap": lambda record: record.__setitem__(
                "inflatedLength", inventory.MAX_INFLATED_BYTES + 1
            ),
            "boolean member count": lambda record: record.__setitem__("memberCount", True),
            "inflated arithmetic": lambda record: record.__setitem__("inflatedLength", 0),
            "boolean chunk length": lambda record: record["chunkObservations"][0].__setitem__(
                "declaredLength", True
            ),
            "tag mismatch": lambda record: record["chunkObservations"][0].__setitem__(
                "tagAscii", "NOPE"
            ),
            "body candidate outside MESH": lambda record: (
                record["chunkObservations"][0].__setitem__("tagAscii", "AYAD"),
                record["chunkObservations"][0].__setitem__("tagHex", "41594144"),
            ),
            "body candidate shorter than CMSH marker": lambda record: self._body_candidates(
                record["chunkObservations"][0]
            )[0].__setitem__("length", 3),
            "body candidate aggregate exceeds MESH payload": lambda record: self._body_candidates(
                record["chunkObservations"][0]
            )[0].__setitem__(
                "length", record["chunkObservations"][0]["declaredLength"] + 1
            ),
            "body candidate evidence kind": lambda record: self._body_candidates(
                record["chunkObservations"][0]
            )[0].__setitem__("evidenceKind", "parsed"),
            "body candidate boundary rule": lambda record: self._body_candidates(
                record["chunkObservations"][0]
            )[0].__setitem__("boundaryRule", "unknown"),
        }
        for role, mutate in cases.items():
            with self.subTest(role=role), self.assertRaises(ValueError):
                self._render(changed(mutate))

        with mock.patch.object(inventory, "MAX_TOP_LEVEL_CHUNKS", 0), self.assertRaises(ValueError):
            self._render(report)
        with mock.patch.object(inventory, "MAX_EMBEDDED_BODIES", 0), self.assertRaises(ValueError):
            self._render(report)
        body_length = self._body_candidates(
            report["archiveRecords"][0]["chunkObservations"][0]
        )[0]["length"]
        with mock.patch.object(inventory, "MAX_AGGREGATE_BODY_BYTES", body_length - 1), self.assertRaises(ValueError):
            self._render(report)

    def test_unavailable_custom_suffix_is_reduced_to_closed_extension_token(self) -> None:
        private = Path(r"C:\private\name.private-client")
        with mock.patch.object(inventory, "read_held_archive", side_effect=OSError(str(private))):
            rendered = self._render(self._observe([private]))
        record = json.loads(rendered)["archiveRecords"][0]
        self.assertEqual("other", record["extension"])
        self.assertNotIn("private-client", rendered.decode("utf-8"))

    def test_legacy_summary_inflate_and_human_output_remain_compatible(self) -> None:
        raw = _chunk(b"AYAD", b"payload") + _chunk(b"TARG")
        fixture = _archive(raw)
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "legacy.aya"
            source.write_bytes(fixture)
            self.assertEqual(raw, inventory.inflate_aya(source))
            summary, inflated, chunks = inventory.summarize_archive(source)
            stream = io.StringIO()
            with contextlib.redirect_stdout(stream):
                inventory.print_summary(summary)
        self.assertEqual(raw, inflated)
        self.assertEqual(2, len(chunks))
        self.assertEqual(str(source), summary.path)
        self.assertEqual(len(fixture), summary.compressed_size)
        self.assertEqual("pc-chunked-zlib", summary.envelope_kind)
        self.assertEqual(hashlib.sha256(fixture).hexdigest(), summary.compressed_sha256)
        self.assertEqual({"AYAD": 1, "TARG": 1}, summary.tag_counts)
        self.assertIn(str(source), stream.getvalue())
        self.assertIn("AYAD:1", stream.getvalue())

    def test_cli_opt_in_writes_path_free_record_without_changing_normal_output(self) -> None:
        raw = _chunk(b"AYAD", b"payload")
        fixture = _archive(raw)
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "private-source.aya"
            output = root / "records" / "observations.json"
            source.write_bytes(fixture)
            normal_stdout = io.StringIO()
            with contextlib.redirect_stdout(normal_stdout):
                result = inventory.main([str(source), "--observation-records-out", str(output)])
            rendered = output.read_text(encoding="utf-8")
        self.assertEqual(0, result)
        self.assertIn(str(source), normal_stdout.getvalue())
        self.assertNotIn("private-source", rendered)
        self.assertNotIn(str(root), rendered)
        self.assertEqual("observed", json.loads(rendered)["archiveRecords"][0]["observationStatus"])

    def test_cli_raw_stream_mode_reports_detected_envelope(self) -> None:
        raw = _chunk(b"LVLR", b"\x67\x00\x00\x00") + _chunk(b"TARG", b"target")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "612_res_XBOX.aya"
            output = root / "output" / "inventory.json"
            source.write_bytes(raw)
            with contextlib.redirect_stdout(io.StringIO()):
                result = inventory.main(
                    [str(source), "--envelope", "raw-tag-stream", "--json-out", str(output)]
                )
            record = json.loads(output.read_text(encoding="utf-8"))[0]
        self.assertEqual(0, result)
        self.assertEqual("raw-tag-stream", record["envelope_kind"])
        self.assertEqual(hashlib.sha256(raw).hexdigest(), record["raw_sha256"])

    def test_legacy_dump_and_json_outputs_remain_available_without_opt_in(self) -> None:
        fixture = _archive(_chunk(b"TEXT", b"legacy-payload"))
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "legacy.aya"
            dump_root = root / "dump"
            manifest_path = root / "manifest" / "inventory.json"
            source.write_bytes(fixture)
            with contextlib.redirect_stdout(io.StringIO()):
                result = inventory.main(
                    [
                        str(source),
                        "--json-out",
                        str(manifest_path),
                        "--dump-dir",
                        str(dump_root),
                        "--dump-tag",
                        "TEXT",
                    ]
                )
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            dumped_payloads = list(dump_root.rglob("*_payload.bin"))
            dumped_metadata = list(dump_root.rglob("*_payload.json"))
        self.assertEqual(0, result)
        self.assertEqual(str(source), manifest[0]["path"])
        self.assertEqual({"TEXT": 1}, manifest[0]["tag_counts"])
        self.assertEqual(1, len(dumped_payloads))
        self.assertEqual(1, len(dumped_metadata))


class AyaCrossPlatformComparatorTests(unittest.TestCase):
    maxDiff = None

    def test_v3_schema_and_unique_logical_joins_are_stable(self) -> None:
        pc_raw = b"".join(
            (
                _chunk(b"LVLR", struct.pack("<I", 103)),
                _chunk(b"TARG", struct.pack("<I", 1)),
                _chunk(b"AYAD", struct.pack("<6I", 1, 2, 3, 4, 5, 6)),
                _chunk(b"TEXT", _text_resource_payload("MeshTex/Panel.tga", b"pc")),
                _chunk(b"MESH", _pc_mesh_resource_payload("craft.msh", b"pc")),
            )
        )
        xbox_raw = b"".join(
            (
                _chunk(b"LVLR", struct.pack("<I", 103)),
                _chunk(b"TARG", struct.pack("<I", 2)),
                _chunk(b"AYAD", struct.pack("<6I", 1, 2, 7, 4, 5, 6)),
                _chunk(b"TEXT", _text_resource_payload("meshtex\\panel.tga", b"xbox")),
                _chunk(b"MESH", _xbox_mesh_resource_payload("craft.msh", b"xbox")),
            )
        )

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            pc_root = root / "pc"
            pc_root.mkdir()
            (pc_root / "unit_res_PC.aya").write_bytes(_archive(pc_raw))
            xbox_zip = root / "xbox.zip"
            with ZipFile(xbox_zip, "w") as archive:
                archive.writestr("data/resources/unit_res_XBOX.aya", xbox_raw)
            result, geometry = cross_compare.build(pc_root, xbox_zip)

        self.assertEqual("bea.pc-xbox-aya-logical-census.v3", cross_compare.SCHEMA)
        self.assertEqual(
            "bea.pc-xbox-aya-chunk-geometry.v2", cross_compare.GEOMETRY_SCHEMA
        )
        self.assertEqual(cross_compare.SCHEMA, result["schemaVersion"])
        self.assertEqual(
            {
                "schemaVersion",
                "sources",
                "summary",
                "tagAggregates",
                "divergences",
                "xboxOnlyRecords",
                "pairs",
            },
            set(result),
        )
        self.assertEqual(
            {
                "pairedCount",
                "pcOnly",
                "xboxOnly",
                "tagSequenceEqualCount",
                "tagSequenceDivergentCount",
                "tagRunTopologyEqualCount",
                "logicalKeySequenceEqualCount",
                "topLevelTextMeshMultisetEqualCount",
                "tagSequenceEqualButTextMeshDivergentCount",
                "tagSequenceEqualButTextMeshDivergentIds",
                "pcChunkCount",
                "xboxPairedChunkCount",
                "xboxAllResourceChunkCount",
                "geometryRowCount",
            },
            set(result["summary"]),
        )
        self.assertEqual(
            {
                "archiveCount",
                "envelope",
                "canonicalManifestSha256",
                "totalRawBytes",
                "totalStoredBytes",
                "zlibMemberCount",
                "zlibMemberCountHistogram",
            },
            set(result["sources"]["pc"]),
        )
        self.assertEqual(
            {
                "archiveCount",
                "envelope",
                "zipLength",
                "zipSha256",
                "canonicalMemberManifestSha256",
                "pairedTotalRawBytes",
                "allResourceTotalRawBytes",
                "totalZipCompressedResourceBytes",
                "zipCompressionMethodCounts",
            },
            set(result["sources"]["xbox"]),
        )
        pair = result["pairs"][0]
        self.assertEqual(
            {
                "tagSequenceEqual",
                "tagRunTopologyEqual",
                "logicalKeySequenceEqual",
                "logicalChunkMultisetEqual",
                "topLevelTextMeshMultisetEqual",
                "commonTagPrefixCount",
                "commonTagSuffixCount",
                "pcMinusXboxTagCounts",
                "xboxMinusPcTagCounts",
                "pcOnlyLogical",
                "xboxOnlyLogical",
            },
            set(pair["comparison"]),
        )
        self.assertNotIn("logicalAssetMultisetEqual", json.dumps(result))
        self.assertTrue(pair["comparison"]["tagSequenceEqual"])
        self.assertTrue(pair["comparison"]["logicalKeySequenceEqual"])
        self.assertTrue(pair["comparison"]["topLevelTextMeshMultisetEqual"])
        self.assertEqual([], result["divergences"])
        self.assertEqual(10, len(geometry))
        self.assertEqual(
            {
                "schemaVersion",
                "resourceId",
                "platform",
                "archiveRawSha256",
                "chunkIndex",
                "tag",
                "offset",
                "payloadOffset",
                "declaredSize",
                "endOffset",
                "payloadSha256",
                "logicalKey",
                "logicalDisplay",
                "logicalKeyMethod",
                "logicalOccurrence",
                "joinConfidence",
                "counterpartIndex",
                "counterpartDeclaredSize",
                "counterpartPayloadSha256",
                "sameDeclaredSize",
                "samePayloadSha256",
            },
            set(geometry[0]),
        )
        text_pc = next(
            row
            for row in geometry
            if row["platform"] == "PC" and row["tag"] == "TEXT"
        )
        self.assertEqual("meshtex\\panel.tga", text_pc["logicalKey"])
        self.assertEqual("unique-logical-key", text_pc["joinConfidence"])
        cross_compare.validate_expectations(
            result,
            paired_count=1,
            require_no_pc_only=True,
            xbox_only=[],
            divergent_count=0,
        )

    def test_duplicate_text_and_mesh_keys_are_ordered_and_counted(self) -> None:
        pc_raw = b"".join(
            (
                _chunk(b"LVLR", b"v"),
                _chunk(b"TEXT", _text_resource_payload("same.tga", b"pc-0")),
                _chunk(b"TEXT", _text_resource_payload("same.tga", b"pc-1")),
                _chunk(b"MESH", _pc_mesh_resource_payload("same.msh", b"pc-0")),
                _chunk(b"MESH", _pc_mesh_resource_payload("same.msh", b"pc-1")),
            )
        )
        xbox_raw = b"".join(
            (
                _chunk(b"LVLR", b"v"),
                _chunk(b"TEXT", _text_resource_payload("same.tga", b"xb-0")),
                _chunk(b"TEXT", _text_resource_payload("same.tga", b"xb-1")),
                _chunk(b"MESH", _xbox_mesh_resource_payload("same.msh", b"xb-0")),
                _chunk(b"MESH", _xbox_mesh_resource_payload("same.msh", b"xb-1")),
            )
        )
        pc_archive, _pc_raw, pc_chunks = _cross_archive(
            pc_raw, "pc-chunked-zlib"
        )
        xbox_archive, _xbox_raw, xbox_chunks = _cross_archive(
            xbox_raw, "raw-tag-stream"
        )
        pair, geometry, aggregate = cross_compare.compare_pair(
            "duplicate", pc_archive, pc_chunks, xbox_archive, xbox_chunks
        )

        self.assertTrue(pair["comparison"]["logicalChunkMultisetEqual"])
        self.assertEqual(2, aggregate["TEXT"]["logicalJoins"])
        self.assertEqual(2, aggregate["MESH"]["logicalJoins"])
        self.assertEqual(2, aggregate["TEXT"]["duplicateKeyJoins"])
        self.assertEqual(2, aggregate["MESH"]["duplicateKeyJoins"])
        for tag in ("TEXT", "MESH"):
            rows = [
                row
                for row in geometry
                if row["platform"] == "PC" and row["tag"] == tag
            ]
            self.assertEqual([0, 1], [row["logicalOccurrence"] for row in rows])
            self.assertTrue(
                all(
                    row["joinConfidence"] == "ordered-duplicate-equal-count"
                    for row in rows
                )
            )
            self.assertEqual(
                [row["chunkIndex"] for row in rows],
                [row["counterpartIndex"] for row in rows],
            )

    def test_empty_mesh_name_is_explicit_and_resource_scoped(self) -> None:
        pc_raw = _chunk(b"LVLR", b"v") + _chunk(
            b"MESH", _pc_mesh_resource_payload("")
        )
        xbox_raw = _chunk(b"LVLR", b"v") + _chunk(
            b"MESH", _xbox_mesh_resource_payload("")
        )
        pc_archive, _pc_raw, pc_chunks = _cross_archive(
            pc_raw, "pc-chunked-zlib"
        )
        xbox_archive, _xbox_raw, xbox_chunks = _cross_archive(
            xbox_raw, "raw-tag-stream"
        )
        _pair, geometry, aggregate = cross_compare.compare_pair(
            "empty-name", pc_archive, pc_chunks, xbox_archive, xbox_chunks
        )

        rows = [row for row in geometry if row["tag"] == "MESH"]
        self.assertEqual(2, len(rows))
        self.assertTrue(all(row["logicalKey"] == "<empty-name>" for row in rows))
        self.assertTrue(all(row["logicalDisplay"] == "" for row in rows))
        self.assertTrue(
            all(row["joinConfidence"] == "unique-logical-key" for row in rows)
        )
        self.assertEqual(1, aggregate["MESH"]["logicalJoins"])

    def test_trailing_text_is_a_tag_run_topology_exception(self) -> None:
        pc_raw = (
            _chunk(b"LVLR", b"v") + _chunk(b"TARG", b"p") + _chunk(b"AYAD", b"a")
        )
        xbox_raw = pc_raw + _chunk(
            b"TEXT", _text_resource_payload("loadingscreen.tga")
        )
        pc_archive, _pc_raw, pc_chunks = _cross_archive(
            pc_raw, "pc-chunked-zlib"
        )
        xbox_archive, _xbox_raw, xbox_chunks = _cross_archive(
            xbox_raw, "raw-tag-stream"
        )
        pair, _geometry, _aggregate = cross_compare.compare_pair(
            "loading", pc_archive, pc_chunks, xbox_archive, xbox_chunks
        )
        comparison = pair["comparison"]

        self.assertFalse(comparison["tagSequenceEqual"])
        self.assertFalse(comparison["tagRunTopologyEqual"])
        self.assertEqual({"TEXT": 1}, comparison["xboxMinusPcTagCounts"])
        self.assertEqual(
            [{"tag": "TEXT", "logicalKey": "loadingscreen.tga", "count": 1}],
            comparison["xboxOnlyLogical"],
        )

    def test_unknown_and_malformed_cross_platform_inputs_fail_closed(self) -> None:
        unknown = _chunk(b"LVLR", b"v") + _chunk(b"NOPE", b"x")
        with self.assertRaises(inventory.ArchiveObservationError) as caught:
            _cross_archive(unknown, "raw-tag-stream")
        self.assertEqual("raw_tag_stream", caught.exception.category)

        overrun = _chunk(b"LVLR", b"v") + b"TARG" + struct.pack("<I", 12) + b"x"
        with self.assertRaises(inventory.ArchiveObservationError) as caught:
            _cross_archive(overrun, "raw-tag-stream")
        self.assertEqual("raw_tag_stream", caught.exception.category)

        malformed_text = _chunk(b"LVLR", b"v") + _chunk(
            b"TEXT", b"DXTX" + struct.pack("<I", 99) + b"CTEX" + (b"\0" * 16)
        )
        with self.assertRaisesRegex(ValueError, "DXTX wrapper"):
            _cross_archive(malformed_text, "raw-tag-stream")

        bad_mesh_body = b"PMS2" + struct.pack("<I", 99) + b"name\0"
        malformed_mesh = _chunk(b"LVLR", b"v") + _chunk(
            b"MESH", b"PMSH" + struct.pack("<I", len(bad_mesh_body)) + bad_mesh_body
        )
        with self.assertRaisesRegex(ValueError, "PMS2 wrapper"):
            _cross_archive(malformed_mesh, "raw-tag-stream")


if __name__ == "__main__":
    unittest.main(verbosity=2)
