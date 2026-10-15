from __future__ import annotations

import contextlib
import hashlib
import io
import json
import math
import os
from pathlib import Path
import struct
import tempfile
import unittest
from unittest import mock
import zlib

import aya_battleengine_identity_analyzer as analyzer


def chunk(tag: bytes, payload: bytes) -> bytes:
    return tag + struct.pack("<I", len(payload)) + payload


def matrix(*, nonfinite: bool = False) -> bytes:
    value = math.nan if nonfinite else 1.0
    return struct.pack(
        "<12f",
        value, 0.0, 0.0, 0.0,
        0.0, 1.0, 0.0, 0.0,
        0.0, 0.0, 1.0, 0.0,
    )


def cmsp(
    part: int,
    children: int,
    *,
    frames: int = 3,
    nonfinite: bool = False,
    opaque: int = 0,
    base_x_delta: float = 0.0,
) -> bytes:
    payload = bytearray(316)
    payload[0:48] = matrix(nonfinite=nonfinite)
    payload[0x30:0x60] = matrix()
    struct.pack_into("<4f", payload, 0x60, 0.0, 0.0, 0.0, 1.0)
    struct.pack_into("<4f", payload, 0x70, float(part) + base_x_delta, 0.0, 0.0, 1.0)
    struct.pack_into("<III", payload, 0x88, part, 1, children)
    struct.pack_into("<7I", payload, 0xA8, 0, 0, 0, 1, frames, frames, 0)
    payload[0xD0] = opaque
    name = f"part_{part}".encode("ascii")
    payload[0xDC : 0xDC + len(name)] = name
    return chunk(b"CMSP", bytes(payload))


def pmvb(*, populated: bool, texr: tuple[int, ...] = (0, 0, 0, 0, 0, 0)) -> bytes:
    header = bytearray(296)
    header[264] = 1 if populated else 0
    struct.pack_into("<III", header, 276, 36, 0x152, 4)
    body = chunk(b"CMVB", bytes(header))
    if populated:
        vertices = b"".join(
            struct.pack("<6fI2f", *xyz, 0.0, 1.0, 0.0, 0xFFFFFFFF, uv[0], uv[1])
            for xyz, uv in (((0.0, 0.0, 0.0), (0.0, 0.0)), ((1.0, 0.0, 0.0), (1.0, 0.0)), ((0.0, 1.0, 0.0), (0.0, 1.0)))
        )
        indices = struct.pack("<3H", 0, 1, 2)
        body += chunk(b"MMPT", struct.pack("<6I", len(vertices), len(indices), 3, 3, 1, 1))
        body += chunk(b"IBUF", indices) + chunk(b"VBUF", vertices) + chunk(b"TEXR", struct.pack("<6I", *texr))
    return chunk(b"PMVB", body)


def part(
    ordinal: int,
    *,
    parent: int | None,
    children: tuple[int, ...] = (),
    reference: int | None = None,
    populated: bool = False,
    nonfinite: bool = False,
    opaque: int = 0,
    base_x_delta: float = 0.0,
    omit_cpos: bool = False,
) -> bytes:
    records = b""
    if children:
        records += chunk(b"CHLD", struct.pack(f"<{len(children)}I", *children))
    if parent is not None:
        records += chunk(b"PRNT", struct.pack("<I", parent))
    records += chunk(b"BBOX", chunk(b"BBOX", bytes(40)))
    records += chunk(b"VHFM", bytes(3)) + chunk(b"HORI", bytes(3 * 48)) + chunk(b"HPOS", bytes(3 * 16))
    if not omit_cpos:
        records += chunk(b"CPOS", bytes(3 * 16))
    records += chunk(b"CORI", bytes(3 * 48))
    if reference is not None:
        records += chunk(b"REFR", struct.pack("<I", reference))
    records += pmvb(populated=populated, texr=(0, 1, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF))
    return chunk(
        b"MESP",
        cmsp(
            ordinal,
            len(children),
            nonfinite=nonfinite,
            opaque=opaque,
            base_x_delta=base_x_delta,
        )
        + records,
    )


def stream(
    *,
    names: tuple[str, str] = (r"meshtex\hull.tga", r"meshtex\glass.tga"),
    reference: int = 1,
    reference_populated: bool = False,
    cycle: bool = False,
    nonfinite: bool = False,
    opaque: int = 0,
    base_x_delta: float = 0.0,
    post_body: tuple[bytes, ...] = (),
    omit_first_cpos: bool = False,
    direct_third_owner: bool = False,
) -> bytes:
    header = bytearray(380)
    header[:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 12, len(names))
    mesh_name = b"fixture_mech.msh"
    header[44 : 44 + len(mesh_name)] = mesh_name
    struct.pack_into("<I", header, 0x164, 3)
    textures = chunk(b"CMST", bytes(36 * len(names)))
    for name in names:
        raw = name.encode("ascii")
        textures += chunk(b"MSHT", chunk(b"TEXB", bytes(20) + raw + bytes(128 - len(raw))))
    if cycle:
        parts = (
            part(0, parent=2, children=(1,)),
            part(1, parent=0, children=(2,), populated=True, nonfinite=nonfinite, opaque=opaque),
            part(2, parent=1, children=(0,), reference=reference, populated=reference_populated),
        )
    else:
        parts = (
            part(0, parent=None, children=(1, 2), omit_cpos=omit_first_cpos),
            part(
                1,
                parent=0,
                populated=True,
                nonfinite=nonfinite,
                opaque=opaque,
                base_x_delta=base_x_delta,
            ),
            part(2, parent=0, reference=None if direct_third_owner else reference, populated=direct_third_owner or reference_populated),
        )
    siblings = b"".join(chunk(tag, b"public-safe-opaque") for tag in post_body)
    return bytes(header) + textures + b"".join(parts) + siblings


def aya(payload: bytes) -> bytes:
    compressed = zlib.compress(payload)
    return struct.pack("<I", len(compressed)) + compressed


def aya_members(payload: bytes, split: int) -> bytes:
    return aya(payload[:split]) + aya(payload[split:])


class IdentityAnalyzerTests(unittest.TestCase):
    def test_generated_articulated_reference_fixture_reports_path_free_graph(self) -> None:
        report = analyzer.analyze_aya_bytes(aya(stream()), loose_ordinal="0102", archive_member_identity="member-0102")
        self.assertEqual("onslaught.aya-battleengine-identity-analysis.v1", report["schema"])
        self.assertEqual({"looseCorpusOrdinal": "0102", "archiveMemberIdentity": "member-0102"}, report["sourceIdentity"])
        self.assertEqual(3, report["mesh"]["partCount"])
        self.assertEqual(0, report["mesh"]["boneCount"])
        self.assertEqual(2, report["mesh"]["declaredTextureSlots"])
        self.assertEqual([r"meshtex\hull.tga", r"meshtex\glass.tga"], report["mesh"]["observedTextureNames"])
        self.assertEqual([[0, 1], [0, 2]], report["hierarchy"]["edges"])
        self.assertEqual([{"instancePart": 2, "ownerPart": 1}], report["references"]["instances"])
        owner_group = report["parts"][1]["pmvb"]["groups"][0]
        self.assertEqual([0, 1, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF], owner_group["textureReferencesRawU32"])
        self.assertEqual("unresolved", owner_group["texturePositionSemantics"])
        self.assertNotIn("sentinel", json.dumps(owner_group).lower())
        self.assertIn("no TEXR positional meaning or sentinel claim", report["nonClaims"])
        self.assertEqual("non-skeletal-part-hierarchy-articulated", report["frames"]["family"])
        self.assertFalse(report["transforms"]["parentCompositionApplied"])
        self.assertEqual(
            ["local-base", "reference-instance", "parent-composed-unproven", "runtime-articulated-unproven"],
            report["transforms"]["classes"],
        )
        self.assertNotIn(str(Path.cwd()), json.dumps(report))

    def test_json_is_canonical_and_deterministic(self) -> None:
        report = analyzer.analyze_aya_bytes(aya(stream()), loose_ordinal="0102", archive_member_identity="member-0102")
        first = analyzer.render_report(report)
        second = analyzer.render_report(report)
        self.assertEqual(first, second)
        self.assertTrue(first.endswith(b"\n"))
        self.assertEqual(hashlib.sha256(first).hexdigest(), hashlib.sha256(second).hexdigest())
        self.assertEqual(report, json.loads(first))

    def test_comparison_reports_real_metadata_differences_not_obj_equivalence(self) -> None:
        left = analyzer.analyze_aya_bytes(aya(stream()), loose_ordinal="0102", archive_member_identity="member-a")
        right = analyzer.analyze_aya_bytes(
            aya(stream(names=(r"meshtex\hull_m.tga", r"meshtex\glass.tga"), opaque=1, base_x_delta=0.25)),
            loose_ordinal="0103",
            archive_member_identity="member-b",
        )
        comparison = analyzer.compare_reports(left, right)
        self.assertEqual(["0102", "0103"], comparison["looseCorpusOrdinals"])
        self.assertEqual(["mesh", "parts"], comparison["differentFields"])
        self.assertEqual(
            [r"meshtex\hull.tga", r"meshtex\glass.tga"],
            comparison["differences"]["mesh"]["left"]["observedTextureNames"],
        )
        self.assertEqual(0, comparison["differences"]["parts"]["left"][1]["cmspOpaqueNonZero"])
        self.assertEqual(1, comparison["differences"]["parts"]["right"][1]["cmspOpaqueNonZero"])
        self.assertEqual(1.0, comparison["differences"]["parts"]["left"][1]["baseTransform"]["position"][0])
        self.assertEqual(1.25, comparison["differences"]["parts"]["right"][1]["baseTransform"]["position"][0])
        self.assertNotIn("OBJ", json.dumps(comparison))

        sibling_left = analyzer.analyze_aya_bytes(
            aya(stream()), loose_ordinal="0102", archive_member_identity="member-a"
        )
        sibling_right = analyzer.analyze_aya_bytes(
            aya(stream(post_body=(b"BBOX",))), loose_ordinal="0103", archive_member_identity="member-b"
        )
        sibling_comparison = analyzer.compare_reports(sibling_left, sibling_right)
        self.assertEqual(["postBodySiblingTags"], sibling_comparison["differentFields"])

    def test_bounded_post_body_siblings_are_reported_and_other_orders_fail_closed(self) -> None:
        accepted_orders = (
            (b"BBOX",),
            (b"BBOX", b"CEMT"),
            (b"CAMD", b"BBOX"),
            (b"CAMD", b"BBOX", b"CEMT"),
            (b"BBOX", b"PMS2"),
            (b"CAMD", b"BBOX", b"CEMT", b"PMS2"),
        )
        for order in accepted_orders:
            with self.subTest(order=order):
                report = analyzer.analyze_aya_bytes(
                    aya(stream(post_body=order)),
                    loose_ordinal="0102",
                    archive_member_identity="member-0102",
                )
                self.assertEqual([tag.decode("ascii") for tag in order], report["postBodySiblingTags"])

        for order in ((b"CEMT",), (b"BBOX", b"BBOX"), (b"CAMD", b"CEMT", b"BBOX")):
            with self.subTest(rejected=order), self.assertRaisesRegex(analyzer.AnalysisError, "post-body sibling"):
                analyzer.analyze_aya_bytes(
                    aya(stream(post_body=order)),
                    loose_ordinal="0102",
                    archive_member_identity="member-0102",
                )

    def test_selection_mapping_is_singular_and_never_collapses_pair(self) -> None:
        resolved = analyzer.resolve_private_selection(
            [{"selection": "choice-a", "looseOrdinal": "0102", "memberIdentity": "member-0102"}], "choice-a"
        )
        self.assertEqual(("0102", "member-0102"), resolved)
        with self.assertRaisesRegex(analyzer.AnalysisError, "ambiguous mapping"):
            analyzer.resolve_private_selection(
                [
                    {"selection": "choice-a", "looseOrdinal": "0102", "memberIdentity": "member-0102"},
                    {"selection": "choice-a", "looseOrdinal": "0103", "memberIdentity": "member-0103"},
                ],
                "choice-a",
            )

    def test_cli_os_error_diagnostic_is_path_free(self) -> None:
        private_path = r"C:\private\selected-resource.aya"
        stderr = io.StringIO()
        with mock.patch.object(analyzer, "analyze_aya_file", side_effect=OSError(f"denied: {private_path}")):
            with contextlib.redirect_stderr(stderr):
                result = analyzer.main(
                    ["--input", private_path, "--loose-ordinal", "0102", "--member-identity", "member-0102"]
                )
        self.assertEqual(2, result)
        self.assertEqual("analysis rejected: held input unavailable or changed\n", stderr.getvalue())
        self.assertNotIn(private_path, stderr.getvalue())

    def test_hierarchy_cycle_missing_reference_and_ambiguous_source_fail_closed(self) -> None:
        cases = (
            (stream(cycle=True), "cycle"),
            (stream(reference=9), "reference target"),
            (stream(reference_populated=True), "ambiguous reference source"),
        )
        for payload, message in cases:
            with self.subTest(message=message), self.assertRaisesRegex(analyzer.AnalysisError, message):
                analyzer.analyze_aya_bytes(aya(payload), loose_ordinal="0102", archive_member_identity="member-0102")

    def test_missing_required_part_record_fails_exact_profile_order(self) -> None:
        with self.assertRaisesRegex(analyzer.AnalysisError, "complete MESP record order"):
            analyzer.analyze_aya_bytes(
                aya(stream(omit_first_cpos=True)),
                loose_ordinal="0102",
                archive_member_identity="member-0102",
            )

    def test_aggregate_geometry_and_archive_member_budgets_fail_closed(self) -> None:
        limits = (("MAX_GROUPS", 1, "group"), ("MAX_VERTICES", 3, "vertex"), ("MAX_INDICES", 3, "index"))
        for attribute, limit, message in limits:
            with self.subTest(attribute=attribute), mock.patch.object(analyzer, attribute, limit):
                with self.assertRaisesRegex(analyzer.AnalysisError, message):
                    analyzer.analyze_aya_bytes(
                        aya(stream(direct_third_owner=True)),
                        loose_ordinal="0102",
                        archive_member_identity="member-0102",
                    )

        payload = stream()
        with mock.patch.object(analyzer, "MAX_MEMBERS", 1, create=True), self.assertRaisesRegex(
            analyzer.AnalysisError, "member count"
        ):
            analyzer.analyze_aya_bytes(
                aya_members(payload, len(payload) // 2),
                loose_ordinal="0102",
                archive_member_identity="member-0102",
            )

    def test_nonfinite_transform_and_private_path_tokens_fail_closed(self) -> None:
        with self.assertRaisesRegex(analyzer.AnalysisError, "non-finite transform"):
            analyzer.analyze_aya_bytes(aya(stream(nonfinite=True)), loose_ordinal="0102", archive_member_identity="member-0102")
        with self.assertRaisesRegex(analyzer.AnalysisError, "private path"):
            analyzer.analyze_aya_bytes(aya(stream()), loose_ordinal="0102", archive_member_identity=r"C:\private\member")
        with self.assertRaisesRegex(analyzer.AnalysisError, "private path"):
            analyzer.analyze_aya_bytes(
                aya(stream(names=(r"..\private.tga", r"meshtex\glass.tga"))),
                loose_ordinal="0102",
                archive_member_identity="member-0102",
            )

    def test_bone_declared_structure_is_rejected_without_skeletal_inference(self) -> None:
        payload = bytearray(stream())
        first_cmsp = payload.find(b"CMSP")
        struct.pack_into("<I", payload, first_cmsp + 8 + 0xC0, 1)
        with self.assertRaisesRegex(analyzer.AnalysisError, "bones"):
            analyzer.analyze_aya_bytes(aya(bytes(payload)), loose_ordinal="0102", archive_member_identity="member-0102")

    def test_malformed_framing_and_texture_slot_fail_closed(self) -> None:
        malformed = aya(stream()) + b"x"
        with self.assertRaisesRegex(analyzer.AnalysisError, "AYA framing"):
            analyzer.analyze_aya_bytes(malformed, loose_ordinal="0102", archive_member_identity="member-0102")
        bad = bytearray(stream())
        texr = bad.find(b"TEXR")
        struct.pack_into("<I", bad, texr + 4, 20)
        with self.assertRaisesRegex(analyzer.AnalysisError, "TEXR"):
            analyzer.analyze_aya_bytes(aya(bytes(bad)), loose_ordinal="0102", archive_member_identity="member-0102")

    def test_held_regular_single_link_file_is_accepted_without_path_disclosure(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.aya"
            source.write_bytes(aya(stream()))
            report = analyzer.analyze_aya_file(source, loose_ordinal="0102", archive_member_identity="member-0102")
            self.assertNotIn(directory, json.dumps(report))

    def test_hardlink_reparse_and_changed_held_input_fail_closed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.aya"
            source.write_bytes(aya(stream()))
            linked = Path(directory) / "linked.aya"
            os.link(source, linked)
            with self.assertRaisesRegex(analyzer.AnalysisError, "single-link"):
                analyzer.analyze_aya_file(source, loose_ordinal="0102", archive_member_identity="member-0102")

        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.aya"
            source.write_bytes(aya(stream()))
            real_fstat = os.fstat
            calls = 0

            def changed(fd: int):
                nonlocal calls
                calls += 1
                result = real_fstat(fd)
                if calls < 2:
                    return result
                values = list(result)
                values[8] = result.st_mtime + 1
                return os.stat_result(values)

            with mock.patch.object(analyzer.os, "fstat", side_effect=changed):
                with self.assertRaisesRegex(analyzer.AnalysisError, "changed held input"):
                    analyzer.analyze_aya_file(source, loose_ordinal="0102", archive_member_identity="member-0102")

        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.aya"
            source.write_bytes(aya(stream()))
            with mock.patch.object(analyzer, "_path_has_reparse", return_value=True):
                with self.assertRaisesRegex(analyzer.AnalysisError, "reparse"):
                    analyzer.analyze_aya_file(source, loose_ordinal="0102", archive_member_identity="member-0102")

        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "input.aya"
            original = aya(stream())
            source.write_bytes(original)
            real_stream = source.open("rb")

            class ChangedSecondRead:
                def __init__(self) -> None:
                    self.read_count = 0

                def __enter__(self):
                    return self

                def __exit__(self, *args):
                    real_stream.close()

                def fileno(self) -> int:
                    return real_stream.fileno()

                def seek(self, offset: int) -> int:
                    return real_stream.seek(offset)

                def read(self, size: int = -1) -> bytes:
                    self.read_count += 1
                    data = real_stream.read(size)
                    if self.read_count == 2 and data:
                        return bytes([data[0] ^ 1]) + data[1:]
                    return data

            with mock.patch.object(Path, "open", return_value=ChangedSecondRead()):
                with self.assertRaisesRegex(analyzer.AnalysisError, "changed held input bytes"):
                    analyzer.analyze_aya_file(
                        source, loose_ordinal="0102", archive_member_identity="member-0102"
                    )


if __name__ == "__main__":
    unittest.main()
