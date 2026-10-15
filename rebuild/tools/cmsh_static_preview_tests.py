# SPDX-License-Identifier: GPL-3.0-or-later

from __future__ import annotations

import collections
import itertools
import json
import math
import os
from dataclasses import replace
from pathlib import Path
import struct
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock
import zlib

import cmsh_static_preview as preview


def _chunk(tag: bytes, payload: bytes) -> bytes:
    return tag + struct.pack("<I", len(payload)) + payload


def _cmsp(
    *,
    part: int,
    children: int,
    base_position: tuple[float, float, float],
    rotated: bool,
    hierarchy_frames: int = 1,
    bones: int = 0,
) -> bytes:
    payload = bytearray(316)
    identity = (1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0)
    orientation = (
        (0.0, 1.0, 0.0, 0.0, -1.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0)
        if rotated
        else identity
    )
    struct.pack_into("<12f", payload, 0x00, *identity)
    struct.pack_into("<12f", payload, 0x30, *orientation)
    struct.pack_into("<4f", payload, 0x60, 0.0, 0.0, 0.0, 1.0)
    struct.pack_into("<4f", payload, 0x70, *base_position, 1.0)
    struct.pack_into("<III", payload, 0x88, part, 1, children)
    struct.pack_into("<IIIIII", payload, 0xA8, 0, 0, 0, 0, 1, hierarchy_frames)
    struct.pack_into("<I", payload, 0xC0, bones)
    return _chunk(b"CMSP", bytes(payload))


def _bbox() -> bytes:
    return _chunk(b"BBOX", _chunk(b"BBOX", bytes(range(40))))


def _cmvb(group_count: int) -> bytes:
    payload = bytearray(296)
    payload[264] = group_count
    struct.pack_into("<III", payload, 276, 36, 0x152, 4)
    return _chunk(b"CMVB", bytes(payload))


def _vertices() -> bytes:
    rows = [
        ((1.0, 2.0, 3.0), (0.0, 1.0, 0.0), (0.0, 0.0)),
        ((4.0, 5.0, 6.0), (1.0, 0.0, 0.0), (0.25, 0.5)),
        ((7.0, 8.0, 9.0), (0.0, 0.0, 1.0), (1.0, 0.5)),
        ((2.0, 2.0, 3.0), (0.0, -1.0, 0.0), (0.75, 1.0)),
    ]
    return b"".join(struct.pack("<6fI2f", *position, *normal, 0xFFFFFFFF, *uv) for position, normal, uv in rows)


def _group(
    indices: tuple[int, ...],
    *,
    owns_vertices: bool,
    texr: tuple[int, int, int, int, int, int] = (0, 0, 0, 0, 0, 0),
) -> bytes:
    vertex_payload = _vertices() if owns_vertices else b""
    declared_vertex_bytes = 4 * 36
    index_payload = struct.pack(f"<{len(indices)}H", *indices)
    mmpt = struct.pack("<6I", declared_vertex_bytes, len(index_payload), len(indices), 4, len(indices) - 2, 1)
    return (
        _chunk(b"MMPT", mmpt)
        + _chunk(b"IBUF", index_payload)
        + _chunk(b"VBUF", vertex_payload)
        + _chunk(b"TEXR", struct.pack("<6I", *texr))
    )


def _pmvb(
    *,
    populated: bool,
    texrs: tuple[tuple[int, int, int, int, int, int], ...] | None = None,
) -> bytes:
    if not populated:
        return _chunk(b"PMVB", _cmvb(0))
    selected = texrs or ((0, 0, 0, 0, 0, 0), (0, 0, 0, 0, 0, 0))
    return _chunk(
        b"PMVB",
        _cmvb(2)
        + _group((0, 1, 2, 2, 3), owns_vertices=True, texr=selected[0])
        + _group((0, 2, 3), owns_vertices=False, texr=selected[1]),
    )


def _empty_reference_pmvb(
    *,
    stride: int,
    fvf: int,
    topology: int,
    group_count: int = 0,
    residue: bytes = b"",
) -> bytes:
    payload = bytearray(296)
    payload[264] = group_count
    struct.pack_into("<III", payload, 276, stride, fvf, topology)
    return _chunk(b"PMVB", _chunk(b"CMVB", bytes(payload)) + residue)


def _reference_group(indices: tuple[int, ...], *, owns_vertices: bool) -> bytes:
    rows = (
        ((1.0, 2.0, 3.0), (0.0, 1.0, 0.0), (0.0, 0.0)),
        ((4.0, 5.0, 6.0), (1.0, 0.0, 0.0), (0.25, 0.5)),
        ((7.0, 8.0, 9.0), (0.0, 0.0, 1.0), (1.0, 1.0)),
    )
    vertex_payload = (
        b"".join(struct.pack("<6fI2f", *position, *normal, 0xFFFFFFFF, *uv) for position, normal, uv in rows)
        if owns_vertices
        else b""
    )
    index_payload = struct.pack(f"<{len(indices)}H", *indices)
    mmpt = struct.pack("<6I", 3 * 36, len(index_payload), len(indices), 3, len(indices) - 2, 1)
    return (
        _chunk(b"MMPT", mmpt)
        + _chunk(b"IBUF", index_payload)
        + _chunk(b"VBUF", vertex_payload)
        + _chunk(b"TEXR", bytes(24))
    )


def _reference_geometry() -> bytes:
    return _chunk(
        b"PMVB",
        _cmvb(2)
        + _reference_group((0, 1, 2), owns_vertices=True)
        + _reference_group((0, 2, 1), owns_vertices=False),
    )


def _multipart_part(
    part: int,
    *,
    parent: int | None,
    children: tuple[int, ...] = (),
    base_position: tuple[float, float, float] = (0.0, 0.0, 0.0),
    rotated: bool = False,
    geometry: bytes | None = None,
    reference_payload: bytes | None = None,
    before_reference: bytes = b"",
    after_reference: bytes = b"",
) -> bytes:
    records = b""
    if children:
        records += _chunk(b"CHLD", struct.pack(f"<{len(children)}I", *children))
    if parent is not None:
        records += _chunk(b"PRNT", struct.pack("<I", parent))
    records += _bbox() + _chunk(b"VHFM", b"\x01") + _chunk(b"HORI", bytes(48)) + _chunk(b"HPOS", bytes(16))
    records += _chunk(b"CPOS", b"") + _chunk(b"CORI", b"") + before_reference
    if reference_payload is not None:
        records += _chunk(b"REFR", reference_payload)
    records += after_reference + (geometry if geometry is not None else _pmvb(populated=False))
    return _chunk(
        b"MESP",
        _cmsp(part=part, children=len(children), base_position=base_position, rotated=rotated) + records,
    )


def reference_fixture_parts() -> list[bytes]:
    return [
        _multipart_part(0, parent=None, children=(1, 2, 3), base_position=(100.0, 200.0, 300.0), rotated=True),
        _multipart_part(1, parent=0, base_position=(10.0, 20.0, 30.0), rotated=True, geometry=_reference_geometry()),
        _multipart_part(2, parent=0, base_position=(40.0, 50.0, 60.0), reference_payload=struct.pack("<I", 1)),
        _multipart_part(3, parent=0, children=(4,), base_position=(-10.0, -20.0, -30.0), rotated=True, reference_payload=struct.pack("<I", 1)),
        _multipart_part(4, parent=3),
    ]


def build_reference_fixture_stream(parts: list[bytes] | None = None) -> bytes:
    selected = reference_fixture_parts() if parts is None else parts
    header = bytearray(380)
    header[0:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 0x164, len(selected))
    return bytes(header) + _chunk(b"CMST", b"") + b"".join(selected)


# --- skinned (stride-48) fixtures -------------------------------------------
# A skinned part streams 48-byte vertices and leaves the FVF word zero. The four
# rows below carry the same positions/normals/UVs as `_vertices`, so the emitted
# OBJ geometry is directly comparable with the rigid path.
_SKINNED_ROWS = (
    ((1.0, 2.0, 3.0), (0.0, 1.0, 0.0), (0.0, 0.0)),
    ((4.0, 5.0, 6.0), (1.0, 0.0, 0.0), (0.25, 0.5)),
    ((7.0, 8.0, 9.0), (0.0, 0.0, 1.0), (1.0, 0.5)),
    ((2.0, 2.0, 3.0), (0.0, -1.0, 0.0), (0.75, 1.0)),
)
_SKINNED_SLOTS = ((0, 0, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1))


def _skinned_cmvb(group_count: int) -> bytes:
    payload = bytearray(296)
    payload[264] = group_count
    struct.pack_into("<III", payload, 276, 48, 0, 4)
    return _chunk(b"CMVB", bytes(payload))


def _skinned_vertices(slots: tuple[tuple[int, int, int], ...] | None = None) -> bytes:
    selected = slots or _SKINNED_SLOTS
    return b"".join(
        struct.pack(
            "<3f3f3fI2f",
            *position,
            # each slot is the BONE index scaled by the matrix-palette stride
            *(float(index * 3) for index in slot),
            *normal,
            0xFFFFFFFF,
            *uv,
        )
        for (position, normal, uv), slot in zip(_SKINNED_ROWS, selected, strict=True)
    )


def _skinned_geometry(
    slots: tuple[tuple[int, int, int], ...] | None = None,
    *,
    vertices: bytes | None = None,
) -> bytes:
    indices = (0, 1, 2, 3)
    index_payload = struct.pack(f"<{len(indices)}H", *indices)
    vertex_payload = _skinned_vertices(slots) if vertices is None else vertices
    group = (
        _chunk(b"MMPT", struct.pack("<6I", 4 * 48, len(index_payload), len(indices), 4, len(indices) - 2, 1))
        + _chunk(b"IBUF", index_payload)
        + _chunk(b"VBUF", vertex_payload)
        + _chunk(b"TEXR", bytes(24))
    )
    return _chunk(b"PMVB", _skinned_cmvb(1) + group)


def _skinned_part(
    part: int,
    *,
    parent: int,
    bone_parts: tuple[int, ...] = (0, 2),
    declared_bones: int | None = None,
    bone_record: bytes | None = None,
    geometry: bytes | None = None,
) -> bytes:
    """A part in the released `PRNT BBOX VHFM HORI HPOS BONE PBKT CPOS PMVB` order."""
    records = (
        _chunk(b"PRNT", struct.pack("<I", parent))
        + _bbox()
        + _chunk(b"VHFM", b"\x01")
        + _chunk(b"HORI", bytes(48))
        + _chunk(b"HPOS", bytes(16))
    )
    if bone_record is not None:
        records += bone_record
    else:
        records += _chunk(b"BONE", struct.pack(f"<{len(bone_parts)}I", *bone_parts))
    records += _chunk(b"PBKT", b"opaque") + _chunk(b"CPOS", b"")
    records += geometry if geometry is not None else _skinned_geometry()
    return _chunk(
        b"MESP",
        _cmsp(
            part=part,
            children=0,
            base_position=(0.0, 0.0, 0.0),
            rotated=False,
            bones=len(bone_parts) if declared_bones is None else declared_bones,
        )
        + records,
    )


def skinned_fixture_parts(**kwargs: object) -> list[bytes]:
    return [
        _multipart_part(0, parent=None, children=(1, 2)),
        _skinned_part(1, parent=0, **kwargs),  # type: ignore[arg-type]
        _multipart_part(2, parent=0),
    ]


def build_skinned_fixture_stream(parts: list[bytes] | None = None) -> bytes:
    return build_reference_fixture_stream(skinned_fixture_parts() if parts is None else parts)


def _part(
    *,
    parent: bool,
    texrs: tuple[tuple[int, int, int, int, int, int], ...] | None = None,
    hierarchy_positions: tuple[tuple[float, float, float], ...] | None = None,
) -> bytes:
    selected_positions = hierarchy_positions or ((0.0, 0.0, 0.0),)
    hierarchy_frames = len(selected_positions)
    hierarchy_orientations = b"".join(
        struct.pack("<12f", 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0)
        for _ in selected_positions
    )
    hierarchy_position_bytes = b"".join(
        struct.pack("<4f", *position, 1.0)
        for position in selected_positions
    )
    if parent:
        records = (
            _chunk(b"CHLD", struct.pack("<I", 1))
            + _chunk(b"PRNT", struct.pack("<I", 0))
            + _bbox()
            + _chunk(b"VHFM", b"\x01")
            + _chunk(b"HORI", hierarchy_orientations)
            + _chunk(b"HPOS", hierarchy_position_bytes)
            + _chunk(b"PBKT", b"opaque")
            + _chunk(b"CPOS", b"\x02\x03")
            + _chunk(b"CORI", b"\x04")
            + _pmvb(populated=False)
        )
        return _chunk(
            b"MESP",
            _cmsp(
                part=0,
                children=1,
                base_position=(0.0, 0.0, 0.0),
                rotated=False,
                hierarchy_frames=hierarchy_frames,
            )
            + records,
        )
    records = (
        _chunk(b"PRNT", struct.pack("<I", 0))
        + _bbox()
        + _chunk(b"VHFM", b"\x01")
        + _chunk(b"HORI", hierarchy_orientations)
        + _chunk(b"HPOS", hierarchy_position_bytes)
        + _chunk(b"CPOS", b"")
        + _chunk(b"CORI", b"")
        + _pmvb(populated=True, texrs=texrs)
    )
    return _chunk(
        b"MESP",
        _cmsp(
            part=1,
            children=0,
            base_position=(10.0, 20.0, 30.0),
            rotated=True,
            hierarchy_frames=hierarchy_frames,
        )
        + records,
    )


def _texture(name: str, *, metadata: bytes = bytes(20)) -> bytes:
    encoded = name.encode("utf-8")
    if len(encoded) >= 128:
        raise ValueError("generated texture name must fit the fixed field")
    return _chunk(b"MSHT", _chunk(b"TEXB", metadata + encoded + b"\0" + bytes(127 - len(encoded))))


def build_fixture_stream() -> bytes:
    header = bytearray(380)
    header[0:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 0x0C, 1)
    header[0x2C:0x34] = b"fixture\0"
    struct.pack_into("<I", header, 0x164, 2)
    texture = _chunk(b"CMST", bytes(36)) + _chunk(b"MSHT", _chunk(b"TEXB", bytes(148)))
    return bytes(header) + texture + _part(parent=True) + _part(parent=False) + _chunk(b"BBOX", b"post")


def build_hierarchy_frame_fixture_stream() -> bytes:
    header = bytearray(380)
    header[0:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 0x0C, 1)
    struct.pack_into("<I", header, 0x164, 2)
    texture = _chunk(b"CMST", bytes(36)) + _chunk(b"MSHT", _chunk(b"TEXB", bytes(148)))
    return (
        bytes(header)
        + texture
        + _part(parent=True)
        + _part(parent=False, hierarchy_positions=((1.0, 2.0, 3.0), (40.0, 50.0, 60.0)))
        + _chunk(b"BBOX", b"post")
    )


def build_material_fixture_stream(
    *,
    texture_names: tuple[str, ...] = (
        "meshtex\\alpha.tga",
        "meshtex\\beta.tga",
        "meshtex\\alpha.tga",
        "meshtex\\delta.tga",
    ),
    texrs: tuple[tuple[int, int, int, int, int, int], ...] = (
        (0, 1, 2, 3, 0, 1),
        (3, 2, 1, 0, 3, 2),
    ),
) -> bytes:
    header = bytearray(380)
    header[0:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 0x0C, len(texture_names))
    struct.pack_into("<I", header, 0x164, 2)
    raw_cmst = b"".join(bytes([(index + 0x41) & 0xFF]) * 36 for index in range(len(texture_names)))
    texture_table = _chunk(b"CMST", raw_cmst) + b"".join(
        _texture(name, metadata=bytes([(index + 1) & 0xFF]) * 20) for index, name in enumerate(texture_names)
    )
    return bytes(header) + texture_table + _part(parent=True) + _part(parent=False, texrs=texrs)


def build_fixture_aya() -> bytes:
    stream = build_fixture_stream()
    split = len(stream) // 2
    members = [zlib.compress(stream[:split]), zlib.compress(stream[split:])]
    return b"".join(struct.pack("<I", len(member)) + member for member in members)


# The subset of `_PART_ORDERS` that `build_order_stream` can synthesise as a
# valid single-part stream. `REFR` orders need a second part to point at and
# `BONE` orders need a populated bone array, so those rows are exercised against
# the retail corpus instead (see `NestedAndEmbeddedCorpusCensus`). The full
# allow-list is pinned literally by `REVIEWED_PART_ORDERS` below.
ACCEPTED_PART_ORDERS = tuple(
    tuple(value.split())
    for value in (
        "PRNT BBOX VHFM HORI HPOS CPOS CORI PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS PBKT CPOS PMVB",
        "PRNT BBOX VHFM HORI HPOS PBKT CPOS PMVB",
        "PRNT BBOX VHFM HORI HPOS CPOS PMVB",
        "PRNT BBOX VHFM HORI HPOS PBKT CPOS CORI PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS PBKT CPOS CORI PMVB",
        "CHLD BBOX VHFM HORI HPOS PBKT CPOS CORI PMVB",
        "BBOX VHFM HORI HPOS PBKT CPOS CORI PMVB",
        "CHLD BBOX VHFM HORI HPOS CPOS CORI PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS PBKT PMVB",
        "PRNT BBOX VHFM HORI HPOS PBKT PMVB",
        "PRNT BBOX VHFM HORI HPOS HFOV CPOS CORI PMVB",
        "PRNT BBOX VHFM HORI HPOS PMVB",
        "PRNT NMIC BBOX VHFM HORI HPOS PBKT CPOS PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS CPOS CORI PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS CPOS PMVB",
        "BBOX VHFM HORI HPOS CPOS CORI PMVB",
    )
)
# Every row of `_PART_ORDERS`, restated by hand. Pinned equal to the module by
# `test_the_module_allowlists_are_exactly_the_reviewed_set`, so widening the
# allow-list is always a deliberate edit in two places and never silent.
REVIEWED_PART_ORDERS = ACCEPTED_PART_ORDERS + tuple(
    tuple(value.split())
    for value in (
        "CHLD PRNT BBOX VHFM HORI HPOS CPOS REFR PMVB",
        "PRNT BBOX VHFM HORI HPOS CPOS CORI REFR PMVB",
        "PRNT BBOX VHFM HORI HPOS CPOS REFR PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS CPOS CORI REFR PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS BONE PBKT CPOS CORI PMVB",
        "PRNT BBOX VHFM HORI HPOS BONE PBKT CPOS PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS BONE PBKT CPOS PMVB",
    )
)
ACCEPTED_SIBLING_ORDERS = (
    (),
    ("BBOX",),
    ("BBOX", "CEMT"),
    ("CAMD", "BBOX"),
    ("CAMD", "BBOX", "CEMT"),
    ("BBOX", "PMS2"),
    ("CAMD", "BBOX", "CEMT", "PMS2"),
    ("BBOX", "PMSH"),
    ("CAMD", "BBOX", "CEMT", "PMSH"),
)


def _record(tag: str) -> bytes:
    return {
        "CHLD": _chunk(b"CHLD", struct.pack("<I", 0)),
        "PRNT": _chunk(b"PRNT", struct.pack("<I", 0)),
        "NMIC": _chunk(b"NMIC", struct.pack("<I", 0)),
        "BBOX": _bbox(),
        "VHFM": _chunk(b"VHFM", b"\x01"),
        "HORI": _chunk(b"HORI", bytes(48)),
        "HPOS": _chunk(b"HPOS", bytes(16)),
        "HFOV": _chunk(b"HFOV", bytes(4)),
        "PBKT": _chunk(b"PBKT", b"opaque"),
        "CPOS": _chunk(b"CPOS", b"position"),
        "CORI": _chunk(b"CORI", b"orientation"),
        "PMVB": _pmvb(populated=True),
        "ZZZZ": _chunk(b"ZZZZ", b""),
        "REFR": _chunk(b"REFR", b""),
    }[tag]


def build_order_stream(order: tuple[str, ...], siblings: tuple[str, ...] = (), overrides: dict[str, bytes] | None = None) -> bytes:
    header = bytearray(380)
    header[0:4] = b"CMSH"
    struct.pack_into("<I", header, 4, 372)
    struct.pack_into("<I", header, 0x0C, 0)
    struct.pack_into("<I", header, 0x164, 1)
    children = 1 if "CHLD" in order else 0
    part = _chunk(
        b"MESP",
        _cmsp(part=0, children=children, base_position=(0.0, 0.0, 0.0), rotated=False)
        + b"".join((overrides or {}).get(tag, _record(tag)) for tag in order),
    )
    return bytes(header) + _chunk(b"CMST", b"") + part + b"".join(_chunk(tag.encode("ascii"), b"x") for tag in siblings)


class MeshEmitterBindingTests(unittest.TestCase):
    @staticmethod
    def _payload(*bindings: tuple[str, int, int | None]) -> bytes:
        records = bytearray(struct.pack("<I", 336))
        for name, selector, part in bindings:
            record = bytearray(336)
            encoded = name.encode("ascii")
            record[76:76 + len(encoded)] = encoded
            struct.pack_into("<I", record, 0x40, 0 if part is None else 0x12345678)
            struct.pack_into("<i", record, 332, selector)
            records.extend(record)
            if part is not None:
                records.extend(struct.pack("<I", part))
        return bytes(records)

    def test_framed_emitters_ignore_tag_in_header_and_preserve_duplicate_names(self):
        source = bytearray(build_order_stream(ACCEPTED_PART_ORDERS[0]))
        # A mesh name is not a chunk boundary. The previous materializer's
        # bytes.find(CEMT) selected this decoy instead of the real sibling.
        source[0x2C:0x34] = b"CEMTfake"
        source.extend(_bbox())
        bindings = (("SpawnerA", 1, 0), ("Component", -1, None), ("Component", 2, 0))
        source.extend(_chunk(b"CEMT", self._payload(*bindings)))
        parsed = preview.parse_cmsh_stream(bytes(source))
        self.assertEqual(tuple(preview.MeshEmitterBinding(*row) for row in bindings),
                         parsed.emitter_bindings())
        self.assertEqual(bytes(source), preview.emit_cmsh_stream(parsed)[0])

    def test_no_emitter_chunk_has_no_bindings(self):
        parsed = preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0]))
        self.assertEqual((), parsed.emitter_bindings())

    def test_malformed_or_out_of_range_emitter_records_are_rejected(self):
        parsed = preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0]))
        for payload in (b"", struct.pack("<I", 335), self._payload(("Component", 1, 0))[:-1],
                        self._payload(("Component", 1, None))[:-1],
                        self._payload(("Component", 1, 1))):
            with self.subTest(payload=payload[:8]):
                changed = replace(parsed, siblings=(preview.MeshSibling(b"CEMT", payload),))
                with self.assertRaises(preview.CmshProfileError):
                    changed.emitter_bindings()


EXPECTED_OBJ = b"""v 12.0 19.0 -33.0
v 15.0 16.0 -36.0
v 18.0 13.0 -39.0
v 12.0 18.0 -33.0
f 1 3 2
f 1 4 3
"""
EXPECTED_REFERENCE_OBJ = b"""v 12.0 19.0 -33.0
v 15.0 16.0 -36.0
v 18.0 13.0 -39.0
v 41.0 52.0 -63.0
v 44.0 55.0 -66.0
v 47.0 58.0 -69.0
v -8.0 -21.0 27.0
v -5.0 -24.0 24.0
v -2.0 -27.0 21.0
f 1 3 2
f 1 2 3
f 4 6 5
f 4 5 6
f 7 9 8
f 7 8 9
"""
EXPECTED_ATTRIBUTE_OBJ = b"""v 12.0 19.0 -33.0
v 15.0 16.0 -36.0
v 18.0 13.0 -39.0
v 12.0 18.0 -33.0
vt 0 0
vt 0.25 0.5
vt 1.0 0.5
vt 0.75 1.0
vn 1.0 0 0
vn 0 -1.0 0
vn 0 0 -1.0
vn -1.0 0 0
f 1/1/1 3/3/3 2/2/2
f 1/1/1 4/4/4 3/3/3
"""


def _validate_obj_semantics(value: bytes) -> None:
    text = value.decode("utf-8")
    assert text.endswith("\n") and "\r" not in text
    lines = text.splitlines()
    vertices = [line for line in lines if line.startswith("v ")]
    faces = [line for line in lines if line.startswith("f ")]
    assert len(vertices) == 4 and len(faces) == 2
    assert all(len(line.split()) == 4 for line in lines)
    assert all(1 <= int(token) <= len(vertices) for line in faces for token in line.split()[1:])


class CmshStaticPreviewTests(unittest.TestCase):
    def test_generated_archive_emits_exact_geometry_only_obj(self) -> None:
        result = preview.convert_aya_bytes(build_fixture_aya())
        self.assertEqual(EXPECTED_OBJ, result)
        _validate_obj_semantics(result)

    def test_opt_in_hierarchy_frame_selects_and_clamps_authored_part_pose(self) -> None:
        stream = build_hierarchy_frame_fixture_stream()

        self.assertEqual((10.0, 20.0, 30.0), preview.parse_cmsh_stream(stream).parts[1].transform.position)
        self.assertEqual((1.0, 2.0, 3.0), preview.parse_cmsh_stream(stream, hierarchy_frame=0).parts[1].transform.position)
        self.assertEqual((40.0, 50.0, 60.0), preview.parse_cmsh_stream(stream, hierarchy_frame=1).parts[1].transform.position)
        self.assertEqual((40.0, 50.0, 60.0), preview.parse_cmsh_stream(stream, hierarchy_frame=25).parts[1].transform.position)
        with self.assertRaisesRegex(preview.CmshProfileError, "hierarchy frame"):
            preview.parse_cmsh_stream(stream, hierarchy_frame=-1)

    def test_reference_fixture_emits_owner_and_instances_in_part_sequence_order(self) -> None:
        result = preview.emit_obj(preview.parse_cmsh_stream(build_reference_fixture_stream()))
        self.assertEqual(EXPECTED_REFERENCE_OBJ, result)
        self.assertEqual(9, sum(line.startswith(b"v ") for line in result.splitlines()))
        self.assertEqual(6, sum(line.startswith(b"f ") for line in result.splitlines()))

    def test_opt_in_obj_emits_retained_uvs_normals_and_multi_group_face_indices(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())

        first = preview.emit_obj(mesh, include_vertex_attributes=True)

        self.assertEqual(EXPECTED_ATTRIBUTE_OBJ, first)
        self.assertEqual(first, preview.emit_obj(mesh, include_vertex_attributes=True))
        self.assertEqual(EXPECTED_ATTRIBUTE_OBJ, preview.convert_aya_bytes(build_fixture_aya(), include_vertex_attributes=True))

    def test_opt_in_reference_obj_keeps_direct_owner_instances_and_part_order(self) -> None:
        mesh = preview.parse_cmsh_stream(build_reference_fixture_stream())

        result = preview.emit_obj(mesh, include_vertex_attributes=True)
        lines = result.splitlines()

        self.assertEqual(9, sum(line.startswith(b"v ") for line in lines))
        self.assertEqual(9, sum(line.startswith(b"vt ") for line in lines))
        self.assertEqual(9, sum(line.startswith(b"vn ") for line in lines))
        self.assertEqual(
            [
                b"vn 1.0 0 0",
                b"vn 0 -1.0 0",
                b"vn 0 0 -1.0",
                b"vn 0 1.0 0",
                b"vn 1.0 0 0",
                b"vn 0 0 -1.0",
                b"vn 1.0 0 0",
                b"vn 0 -1.0 0",
                b"vn 0 0 -1.0",
            ],
            [line for line in lines if line.startswith(b"vn ")],
        )
        self.assertEqual(
            [
                b"f 1/1/1 3/3/3 2/2/2",
                b"f 1/1/1 2/2/2 3/3/3",
                b"f 4/4/4 6/6/6 5/5/5",
                b"f 4/4/4 5/5/5 6/6/6",
                b"f 7/7/7 9/9/9 8/8/8",
                b"f 7/7/7 8/8/8 9/9/9",
            ],
            [line for line in lines if line.startswith(b"f ")],
        )
        self.assertEqual(result, preview.emit_obj(mesh, include_vertex_attributes=True))

    def test_opt_in_obj_omits_missing_optional_attributes_without_changing_geometry(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())
        parts = tuple(
            replace(part, vertices=tuple(replace(vertex, normal=None, uv=None) for vertex in part.vertices))
            for part in mesh.parts
        )
        without_attributes = replace(mesh, parts=parts)

        self.assertEqual(EXPECTED_OBJ, preview.emit_obj(without_attributes, include_vertex_attributes=True))

    def test_opt_in_obj_rejects_malformed_or_nonfinite_optional_attributes(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())
        populated = mesh.parts[1]
        cases = (
            ("normal_count", replace(populated.vertices[0], normal=(0.0, 1.0))),
            ("uv_count", replace(populated.vertices[0], uv=(0.0,))),
            ("normal_nonfinite", replace(populated.vertices[0], normal=(0.0, float("nan"), 0.0))),
            ("uv_nonfinite", replace(populated.vertices[0], uv=(0.0, float("inf")))),
        )
        for case, vertex in cases:
            malformed_part = replace(populated, vertices=(vertex, *populated.vertices[1:]))
            malformed = replace(mesh, parts=(mesh.parts[0], malformed_part))
            with self.subTest(case=case):
                with self.assertRaises(preview.CmshProfileError):
                    preview.emit_obj(malformed, include_vertex_attributes=True)

        malformed_group = replace(populated.groups[0], indices=(0, 1, len(populated.vertices)))
        malformed_part = replace(populated, groups=(malformed_group, *populated.groups[1:]))
        with self.assertRaisesRegex(preview.CmshProfileError, "OBJ rejection"):
            preview.emit_obj(replace(mesh, parts=(mesh.parts[0], malformed_part)), include_vertex_attributes=True)

    def test_opt_in_normals_use_inverse_transpose_for_scale_and_shear(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())
        populated = mesh.parts[1]
        scale = preview._Transform(
            rows=((2.0, 0.0, 0.0), (0.0, 4.0, 0.0), (0.0, 0.0, 8.0)),
            position=populated.transform.position,
        )
        scaled_vertex = replace(populated.vertices[0], normal=(1.0, 1.0, 0.0))
        scaled_part = replace(populated, transform=scale, vertices=(scaled_vertex, *populated.vertices[1:]))
        scaled = preview.emit_obj(replace(mesh, parts=(mesh.parts[0], scaled_part)), include_vertex_attributes=True)
        expected_scale = (0.5 / math.sqrt(0.3125), 0.25 / math.sqrt(0.3125), 0.0)
        first_scaled = tuple(float(value) for value in next(
            line for line in scaled.decode("ascii").splitlines() if line.startswith("vn ")
        ).split()[1:])
        self.assertEqual(expected_scale, first_scaled)

        shear = preview._Transform(
            rows=((1.0, 1.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)),
            position=populated.transform.position,
        )
        sheared_vertex = replace(populated.vertices[0], normal=(0.0, 1.0, 0.0))
        sheared_part = replace(populated, transform=shear, vertices=(sheared_vertex, *populated.vertices[1:]))
        sheared = preview.emit_obj(replace(mesh, parts=(mesh.parts[0], sheared_part)), include_vertex_attributes=True)
        first_sheared = tuple(float(value) for value in next(
            line for line in sheared.decode("ascii").splitlines() if line.startswith("vn ")
        ).split()[1:])
        self.assertEqual((0.0, 1.0, 0.0), first_sheared)

    def test_opt_in_normals_reject_degenerate_and_ill_conditioned_transforms(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())
        populated = mesh.parts[1]
        for role, rows in (
            ("degenerate", ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 0.0))),
            ("ill-conditioned", ((1e5, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1e-5))),
        ):
            with self.subTest(role=role):
                transformed = replace(
                    populated,
                    transform=preview._Transform(rows=rows, position=populated.transform.position),
                )
                with self.assertRaisesRegex(preview.CmshProfileError, "normal transform"):
                    preview.emit_obj(replace(mesh, parts=(mesh.parts[0], transformed)), include_vertex_attributes=True)

    def test_cli_opt_in_flags_are_explicit_and_forwarded(self) -> None:
        with mock.patch.object(preview, "publish_anonymous_previews", return_value=(2, 0)) as publish:
            status = preview._main(
                [
                    "--checkout",
                    "checkout",
                    "--input",
                    "input",
                    "--output",
                    "output",
                    "--vertex-attributes",
                    "--material-layer-groups",
                    "--hierarchy-frame",
                    "25",
                ]
            )

        self.assertEqual(0, status)
        publish.assert_called_once_with(
            Path("checkout"),
            Path("input"),
            Path("output"),
            include_vertex_attributes=True,
            include_material_layer_groups=True,
            hierarchy_frame=25,
        )

    def test_opt_in_material_layer_groups_preserve_all_slots_and_fail_closed(self) -> None:
        mesh = preview.parse_cmsh_stream(build_material_fixture_stream())

        result = preview.emit_obj(
            mesh,
            include_vertex_attributes=True,
            include_material_layer_groups=True,
        )

        self.assertEqual(
            [
                b"usemtl layers-00000000-00000001-00000002-00000003-00000000-00000001",
                b"usemtl layers-00000003-00000002-00000001-00000000-00000003-00000002",
            ],
            [line for line in result.splitlines() if line.startswith(b"usemtl ")],
        )
        malformed = replace(
            mesh,
            parts=(
                mesh.parts[0],
                replace(
                    mesh.parts[1],
                    groups=(
                        replace(
                            mesh.parts[1].groups[0],
                            raw_texr_u32=(0, 4, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF),
                        ),
                        mesh.parts[1].groups[1],
                    ),
                ),
            ),
        )
        with self.assertRaisesRegex(preview.CmshProfileError, "unresolved material texture"):
            preview.emit_obj(malformed, include_material_layer_groups=True)

    def test_emit_mtl_names_match_obj_usemtl_and_map_kd_is_slot0_name(self) -> None:
        """MTL is the missing bind half of `--material-layer-groups`.

        `map_Kd` is the mesh TEXB name for TEXR slot 0, not a guessed PNG path.
        """
        mesh = preview.parse_cmsh_stream(build_material_fixture_stream())
        obj = preview.emit_obj(mesh, include_material_layer_groups=True)
        mtl = preview.emit_mtl(mesh)
        usemtl = [line[7:] for line in obj.splitlines() if line.startswith(b"usemtl ")]
        newmtl = [line[7:] for line in mtl.splitlines() if line.startswith(b"newmtl ")]
        self.assertEqual(usemtl, newmtl)
        self.assertEqual(
            [b"meshtex\\alpha.tga", b"meshtex\\delta.tga"],
            [line[7:] for line in mtl.splitlines() if line.startswith(b"map_Kd ")],
        )
        self.assertNotIn(str(Path.cwd()).encode("utf-8"), mtl)
        malformed = replace(
            mesh,
            parts=(
                mesh.parts[0],
                replace(
                    mesh.parts[1],
                    groups=(
                        replace(
                            mesh.parts[1].groups[0],
                            raw_texr_u32=(0, 4, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF),
                        ),
                        mesh.parts[1].groups[1],
                    ),
                ),
            ),
        )
        with self.assertRaisesRegex(preview.CmshProfileError, "unresolved material texture"):
            preview.emit_mtl(malformed)

    def test_material_report_retains_vertex_attributes_duplicate_names_and_six_unknown_positions(self) -> None:
        mesh = preview.parse_cmsh_stream(build_material_fixture_stream())

        self.assertEqual((0.0, 1.0, 0.0), mesh.parts[1].vertices[0].normal)
        self.assertEqual((0.0, 0.0), mesh.parts[1].vertices[0].uv)
        self.assertEqual(0xFFFFFFFF, mesh.parts[1].vertices[0].raw_color_u32)
        self.assertEqual((0, 1, 2, 3, 0, 1), mesh.parts[1].groups[0].raw_texr_u32)
        self.assertEqual((3, 2, 1, 0, 3, 2), mesh.parts[1].groups[1].raw_texr_u32)
        self.assertEqual(
            ("meshtex\\alpha.tga", "meshtex\\beta.tga", "meshtex\\alpha.tga", "meshtex\\delta.tga"),
            tuple(texture.name for texture in mesh.textures),
        )
        self.assertEqual(bytes([0x41]) * 36, mesh.textures[0].raw_cmst_entry)
        self.assertEqual(bytes([0x43]) * 36, mesh.textures[2].raw_cmst_entry)
        self.assertEqual(bytes([1]) * 20, mesh.textures[0].raw_texb_metadata)
        self.assertEqual(bytes([3]) * 20, mesh.textures[2].raw_texb_metadata)
        expected_name = b"meshtex\\alpha.tga\0" + bytes(128 - len(b"meshtex\\alpha.tga") - 1)
        self.assertEqual(expected_name, mesh.textures[0].raw_name_field)
        self.assertEqual(expected_name, mesh.textures[2].raw_name_field)

        first = preview.emit_material_report(mesh)
        second = preview.emit_material_report(mesh)
        self.assertEqual(first, second)
        self.assertTrue(first.endswith(b"\n"))
        report = json.loads(first)
        self.assertEqual("onslaught-cmsh-material-report.v1", report["schemaVersion"])
        self.assertEqual([0xFFFFFFFF], report["acceptedSentinelU32"])
        self.assertEqual(
            [
                "base",
                "dot3Lighting",
                "environmentReflection",
                "disabledProjective",
                "alphaOverlay",
                "disabled",
            ],
            report["retailPositionSemantics"],
        )
        self.assertEqual(["meshtex\\alpha.tga", "meshtex\\beta.tga", "meshtex\\alpha.tga", "meshtex\\delta.tga"], report["textures"])
        populated = report["parts"][1]
        self.assertEqual(1, populated["partIndex"])
        self.assertEqual(1, populated["geometrySourcePart"])
        self.assertEqual([0, 1, 2, 3, 0, 1], populated["groups"][0]["rawTexrU32"])
        self.assertEqual([3, 2, 1, 0, 3, 2], populated["groups"][1]["rawTexrU32"])
        self.assertEqual(
            report["retailPositionSemantics"],
            [row["retailSemantic"] for row in populated["groups"][0]["positions"]],
        )
        self.assertEqual(["resolved"] * 6, [row["status"] for row in populated["groups"][0]["positions"]])
        self.assertNotIn(b"materialRole", first)
        strings: list[str] = []

        def collect_strings(value: object) -> None:
            if isinstance(value, str):
                strings.append(value)
            elif isinstance(value, list):
                for item in value:
                    collect_strings(item)
            elif isinstance(value, dict):
                for key, item in value.items():
                    collect_strings(key)
                    collect_strings(item)

        collect_strings(report)
        self.assertTrue(all(str(Path.cwd()) not in value for value in strings))

    def test_material_report_uses_portable_display_name_and_preserves_non_utf8_raw_name_bytes(self) -> None:
        stream = bytearray(build_material_fixture_stream())
        name_offset = stream.index(b"meshtex\\alpha.tga")
        stream[name_offset] = 0x80

        mesh = preview.parse_cmsh_stream(bytes(stream))

        self.assertEqual(b"\x80eshtex\\alpha.tga", mesh.textures[0].raw_name_field.split(b"\0", 1)[0])
        report = json.loads(preview.emit_material_report(mesh))
        self.assertEqual("\ufffdeshtex\\alpha.tga", report["textures"][0])
        self.assertTrue(all(not 0xD800 <= ord(character) <= 0xDFFF for character in report["textures"][0]))

    def test_material_report_distinguishes_unsigned_max_sentinel_from_unresolved_values(self) -> None:
        self.assertEqual(frozenset({0xFFFFFFFF}), preview.TEXR_SENTINEL_U32)
        mesh = preview.parse_cmsh_stream(
            build_material_fixture_stream(texrs=((3, 4, 0xFFFFFFFF, 2, 5, 1), (3, 2, 1, 0, 3, 2)))
        )

        report = json.loads(preview.emit_material_report(mesh))
        rows = report["parts"][1]["groups"][0]["positions"]
        self.assertEqual([3, 4, 0xFFFFFFFF, 2, 5, 1], [row["rawU32"] for row in rows])
        self.assertEqual(
            ["resolved", "unresolved", "sentinel", "resolved", "unresolved", "resolved"],
            [row["status"] for row in rows],
        )
        self.assertEqual([3, 2, 1], [rows[index]["textureIndex"] for index in (0, 3, 5)])
        self.assertTrue(all("textureName" not in rows[index] and "textureIndex" not in rows[index] for index in (1, 2, 4)))
        self.assertEqual(
            [
                "base",
                "dot3Lighting",
                "environmentReflection",
                "disabledProjective",
                "alphaOverlay",
                "disabled",
            ],
            [row["retailSemantic"] for row in rows],
        )

    def test_material_report_retains_complete_declared_texture_table_through_limit(self) -> None:
        names = tuple(f"meshtex\\generated-{index:03d}.tga" for index in range(preview.MAX_TEXTURES))
        mesh = preview.parse_cmsh_stream(build_material_fixture_stream(texture_names=names))

        self.assertEqual(names, tuple(texture.name for texture in mesh.textures))
        self.assertEqual(list(names), json.loads(preview.emit_material_report(mesh))["textures"])

    def test_material_report_reference_instances_preserve_geometry_owner_and_raw_texr(self) -> None:
        mesh = preview.parse_cmsh_stream(build_reference_fixture_stream())

        first = preview.emit_material_report(mesh)
        self.assertEqual(first, preview.emit_material_report(mesh))
        report = json.loads(first)
        self.assertEqual([0, 1, 1, 1, 4], [part["geometrySourcePart"] for part in report["parts"]])
        for part_index in (1, 2, 3):
            self.assertEqual([0, 0, 0, 0, 0, 0], report["parts"][part_index]["groups"][0]["rawTexrU32"])
            self.assertEqual(
                ["unresolved"] * 6,
                [row["status"] for row in report["parts"][part_index]["groups"][0]["positions"]],
            )

    def test_texr_truncation_and_nonfinite_retained_attributes_fail_closed(self) -> None:
        stream = build_material_fixture_stream()
        texr = stream.find(b"TEXR")
        truncated = bytearray(stream)
        struct.pack_into("<I", truncated, texr + 4, 23)
        del truncated[texr + 8 + 23]
        with self.assertRaisesRegex(preview.CmshProfileError, "truncation: .*MESP 1 payload"):
            preview.parse_cmsh_stream(bytes(truncated))

        for role, vertex_offset in (("position", 0), ("normal", 12)):
            malformed = bytearray(stream)
            vbuf = malformed.find(b"VBUF")
            struct.pack_into("<f", malformed, vbuf + 8 + vertex_offset, float("nan"))
            with self.subTest(role=role):
                with self.assertRaisesRegex(preview.CmshProfileError, "non-finite numeric value"):
                    preview.parse_cmsh_stream(bytes(malformed))

    def test_non_finite_uv_is_retained_not_rejected_and_never_substituted(self) -> None:
        """A non-finite texture coordinate degrades that vertex, not the mesh.

        `M_Prison.msh` ships five of them and is loaded by name from four retail
        level archives, so rejecting the mesh would diverge from the released
        loader. The coordinate is dropped from OBJ output rather than replaced.
        """
        stream = build_material_fixture_stream()
        baseline = preview.parse_cmsh_stream(stream)
        self.assertEqual(0, baseline.non_finite_uv_count)

        for pattern in (float("nan"), float("inf"), float("-inf")):
            malformed = bytearray(stream)
            vbuf = malformed.find(b"VBUF")
            struct.pack_into("<f", malformed, vbuf + 8 + 28, pattern)
            raw_words = struct.unpack_from("<2I", malformed, vbuf + 8 + 28)
            with self.subTest(pattern=pattern):
                mesh = preview.parse_cmsh_stream(bytes(malformed))
                self.assertEqual(1, mesh.non_finite_uv_count)
                degraded = [
                    vertex
                    for part in mesh.parts
                    for vertex in part.vertices
                    if vertex.uv is None
                ]
                self.assertEqual(1, len(degraded))
                # Nothing is invented: the raw dwords are carried through.
                self.assertEqual(raw_words, degraded[0].raw_uv_u32)
                # Every other vertex keeps a real coordinate.
                self.assertTrue(
                    all(
                        vertex.uv is not None
                        for part in mesh.parts
                        for vertex in part.vertices
                        if vertex is not degraded[0]
                    )
                )
                obj = preview.emit_obj(mesh, include_vertex_attributes=True)
                text = obj.decode("utf-8")
                # No substitute coordinate was emitted for the degraded vertex,
                # and no face mixes reference forms.
                self.assertNotIn("nan", text.lower())
                self.assertNotIn("inf", text.lower())
                for line in text.splitlines():
                    if not line.startswith("f "):
                        continue
                    forms = {reference.count("/") for reference in line[2:].split()}
                    self.assertEqual(1, len(forms), line)
                    slashes = {"//" in reference for reference in line[2:].split()}
                    self.assertEqual(1, len(slashes), line)

    def test_reference_zero_sentinel_source_metadata_preserves_reference_output(self) -> None:
        parts = reference_fixture_parts()
        sentinel = _empty_reference_pmvb(stride=0, fvf=0, topology=0)
        parts[2] = _multipart_part(
            2,
            parent=0,
            base_position=(40.0, 50.0, 60.0),
            geometry=sentinel,
            reference_payload=struct.pack("<I", 1),
        )
        parts[3] = _multipart_part(
            3,
            parent=0,
            children=(4,),
            base_position=(-10.0, -20.0, -30.0),
            rotated=True,
            geometry=sentinel,
            reference_payload=struct.pack("<I", 1),
        )

        result = preview.emit_obj(preview.parse_cmsh_stream(build_reference_fixture_stream(parts)))

        self.assertEqual(EXPECTED_REFERENCE_OBJ, result)

    def test_reference_zero_group_source_ignores_unused_metadata_but_rejects_payload(self) -> None:
        malformed_cmvb = _chunk(b"PMVB", _chunk(b"CMVB", bytes(295)))
        populated_sentinel = _empty_reference_pmvb(stride=0, fvf=0, topology=0, group_count=1)
        cases = [
            ("populated", populated_sentinel, "unsupported bones/reference graph"),
            ("residue", _empty_reference_pmvb(stride=0, fvf=0, topology=0, residue=b"x"), "unsupported bones/reference graph"),
            (
                "child",
                _empty_reference_pmvb(stride=0, fvf=0, topology=0, residue=_chunk(b"MMPT", b"")),
                "unsupported bones/reference graph",
            ),
            ("malformed_cmvb_length", malformed_cmvb, "unsupported bones/reference graph"),
        ]
        for case, geometry, category in cases:
            parts = reference_fixture_parts()
            parts[2] = _multipart_part(
                2,
                parent=0,
                geometry=geometry,
                reference_payload=struct.pack("<I", 1),
            )
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, category):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        for stride, fvf, topology in ((48, 0, 0), (0, 0x152, 4), (1, 2, 3)):
            parts = reference_fixture_parts()
            parts[2] = _multipart_part(
                2,
                parent=0,
                base_position=(40.0, 50.0, 60.0),
                geometry=_empty_reference_pmvb(stride=stride, fvf=fvf, topology=topology),
                reference_payload=struct.pack("<I", 1),
            )
            with self.subTest(stride=stride, fvf=fvf, topology=topology):
                self.assertEqual(
                    EXPECTED_REFERENCE_OBJ,
                    preview.emit_obj(preview.parse_cmsh_stream(build_reference_fixture_stream(parts))),
                )

        unsupported_target = bytearray(_reference_geometry())
        cmvb = unsupported_target.find(b"CMVB")
        struct.pack_into("<I", unsupported_target, cmvb + 8 + 276, 48)
        parts = reference_fixture_parts()
        parts[1] = _multipart_part(1, parent=0, geometry=bytes(unsupported_target))
        parts[2] = _multipart_part(
            2,
            parent=0,
            geometry=_empty_reference_pmvb(stride=0, fvf=0, topology=0),
            reference_payload=struct.pack("<I", 1),
        )
        with self.assertRaisesRegex(preview.CmshProfileError, "unsupported profile"):
            preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

    def test_reference_length_target_and_geometry_source_fail_closed(self) -> None:
        for length in (0, 3, 8):
            parts = reference_fixture_parts()
            parts[2] = _multipart_part(2, parent=0, reference_payload=bytes(length))
            with self.subTest(case="length", length=length):
                with self.assertRaisesRegex(preview.CmshProfileError, "invalid declared length/count"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        target_cases = {
            "self": 2,
            "forward_last_index": 4,
            "out_of_range": 5,
        }
        for case, target in target_cases.items():
            parts = reference_fixture_parts()
            parts[2] = _multipart_part(2, parent=0, reference_payload=struct.pack("<I", target))
            pattern = "index out of bounds" if case == "out_of_range" else "unsupported bones/reference graph"
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, pattern):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        # A reference whose target is itself empty resolves to no geometry
        # instead of rejecting the mesh. Exactly two shipped references do this,
        # both in `m_Building 5 Top.msh`; the other 386 target real geometry.
        parts = reference_fixture_parts()
        parts[2] = _multipart_part(2, parent=0, reference_payload=struct.pack("<I", 0))
        with self.subTest(case="empty_target"):
            mesh = preview.parse_cmsh_stream(build_reference_fixture_stream(parts))
            self.assertEqual((), mesh.parts[0].vertices)
            self.assertEqual((), mesh.parts[2].vertices)
            self.assertEqual((), mesh.parts[2].groups)
            self.assertEqual(0, mesh.parts[2].reference)

        for case, geometry in (
            ("populated", _reference_geometry()),
            ("residual", _chunk(b"PMVB", _cmvb(0) + b"x")),
        ):
            parts = reference_fixture_parts()
            parts[2] = _multipart_part(2, parent=0, geometry=geometry, reference_payload=struct.pack("<I", 1))
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, "unsupported bones/reference graph"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

    def test_reference_chains_cycles_order_and_hierarchy_fail_closed(self) -> None:
        chain = [
            _multipart_part(0, parent=None, children=(1, 2), geometry=_reference_geometry()),
            _multipart_part(1, parent=0, reference_payload=struct.pack("<I", 0)),
            _multipart_part(2, parent=0, reference_payload=struct.pack("<I", 1)),
        ]
        cycle = [
            _multipart_part(0, parent=None, children=(1, 2)),
            _multipart_part(1, parent=0, reference_payload=struct.pack("<I", 2)),
            _multipart_part(2, parent=0, reference_payload=struct.pack("<I", 1)),
        ]
        for case, parts in (("chain", chain), ("cycle", cycle)):
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, "unsupported bones/reference graph"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        duplicate = reference_fixture_parts()
        duplicate[2] = _multipart_part(
            2,
            parent=0,
            reference_payload=struct.pack("<I", 1),
            before_reference=_chunk(b"REFR", struct.pack("<I", 1)),
        )
        wrong_order = reference_fixture_parts()
        wrong_order[2] = _multipart_part(
            2,
            parent=0,
            reference_payload=struct.pack("<I", 1),
            after_reference=_chunk(b"CPOS", b""),
        )
        for case, parts in (("duplicate", duplicate), ("wrong_order", wrong_order)):
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, "unexpected tag/order"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        missing_reciprocal = reference_fixture_parts()
        missing_reciprocal[0] = _multipart_part(0, parent=None, children=(1, 3), base_position=(100.0, 200.0, 300.0), rotated=True)
        duplicate_parent = reference_fixture_parts()
        duplicate_parent[3] = _multipart_part(3, parent=0, children=(2, 4), reference_payload=struct.pack("<I", 1))
        parent_cycle = reference_fixture_parts()
        parent_cycle[0] = _multipart_part(0, parent=3, children=(1, 2, 3))
        parent_cycle[3] = _multipart_part(3, parent=0, children=(0, 4), reference_payload=struct.pack("<I", 1))
        for case, parts in (
            ("missing_reciprocal", missing_reciprocal),
            ("duplicate_parent", duplicate_parent),
            ("parent_cycle", parent_cycle),
        ):
            with self.subTest(case=case):
                with self.assertRaisesRegex(preview.CmshProfileError, "unsupported bones/reference graph"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

    def test_reference_expansion_caps_count_every_instance(self) -> None:
        stream = build_reference_fixture_stream()
        for cap_name, at_cap, above_cap in (
            ("MAX_VERTICES", 9, 8),
            ("MAX_GROUPS", 6, 5),
            ("MAX_INDICES", 18, 17),
            ("MAX_TRIANGLES", 6, 5),
        ):
            with self.subTest(cap=cap_name, boundary="cap"):
                with mock.patch.object(preview, cap_name, at_cap):
                    self.assertEqual(EXPECTED_REFERENCE_OBJ, preview.emit_obj(preview.parse_cmsh_stream(stream)))
            with self.subTest(cap=cap_name, boundary="cap_plus_one"):
                with mock.patch.object(preview, cap_name, above_cap):
                    with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                        preview.emit_obj(preview.parse_cmsh_stream(stream))

        with mock.patch.object(preview, "MAX_OBJ", len(EXPECTED_REFERENCE_OBJ)):
            self.assertEqual(EXPECTED_REFERENCE_OBJ, preview.emit_obj(preview.parse_cmsh_stream(stream)))
        with mock.patch.object(preview, "MAX_OBJ", len(EXPECTED_REFERENCE_OBJ) - 1):
            with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                preview.emit_obj(preview.parse_cmsh_stream(stream))

    def test_reference_profile_rejects_bones_and_populated_stride48(self) -> None:
        bone_parts = reference_fixture_parts()
        bone_parts[2] = _multipart_part(
            2,
            parent=0,
            reference_payload=struct.pack("<I", 1),
            after_reference=_chunk(b"BONE", b""),
        )
        # A BONE record on a part whose CMSP declares numBones == 0 is malformed:
        # the two must agree, and no shipped mesh disagrees.
        with self.assertRaisesRegex(preview.CmshProfileError, "invalid declared length/count"):
            preview.parse_cmsh_stream(build_reference_fixture_stream(bone_parts))

        stride48 = bytearray(_reference_geometry())
        cmvb = stride48.find(b"CMVB")
        struct.pack_into("<I", stride48, cmvb + 8 + 276, 48)
        stride_parts = reference_fixture_parts()
        stride_parts[1] = _multipart_part(1, parent=0, geometry=bytes(stride48))
        with self.assertRaisesRegex(preview.CmshProfileError, "unsupported profile"):
            preview.parse_cmsh_stream(build_reference_fixture_stream(stride_parts))

        empty_stride48 = bytearray(_pmvb(populated=False))
        empty_cmvb = empty_stride48.find(b"CMVB")
        struct.pack_into("<I", empty_stride48, empty_cmvb + 8 + 276, 48)
        source_stride_parts = reference_fixture_parts()
        source_stride_parts[2] = _multipart_part(
            2,
            parent=0,
            base_position=(40.0, 50.0, 60.0),
            geometry=bytes(empty_stride48),
            reference_payload=struct.pack("<I", 1),
        )
        self.assertEqual(
            EXPECTED_REFERENCE_OBJ,
            preview.emit_obj(preview.parse_cmsh_stream(build_reference_fixture_stream(source_stride_parts))),
        )

    def test_skinned_part_profile_and_bone_slot_contract(self) -> None:
        """The released skinning records decode to a bind-pose part.

        `BONE` is `numBones` part indices; the stride-48 vertex is
        position / three bone slots / normal / diffuse / UV, and each slot holds
        its BONE index multiplied by three.
        """
        mesh = preview.parse_cmsh_stream(build_skinned_fixture_stream())
        skinned = mesh.parts[1]
        self.assertEqual((0, 2), skinned.bones)
        self.assertEqual((), mesh.parts[0].bones)
        self.assertEqual(4, len(skinned.vertices))
        self.assertEqual(
            [row[0] for row in _SKINNED_ROWS], [vertex.position for vertex in skinned.vertices]
        )
        self.assertEqual(
            [row[1] for row in _SKINNED_ROWS], [vertex.normal for vertex in skinned.vertices]
        )
        self.assertEqual(
            [row[2] for row in _SKINNED_ROWS], [vertex.uv for vertex in skinned.vertices]
        )
        self.assertEqual(
            list(_SKINNED_SLOTS), [vertex.bone_slots for vertex in skinned.vertices]
        )
        self.assertEqual([0xFFFFFFFF] * 4, [vertex.raw_color_u32 for vertex in skinned.vertices])
        # A rigid part in the same mesh keeps no bone slots at all.
        self.assertTrue(all(vertex.bone_slots is None for vertex in mesh.parts[0].vertices))
        # The bind pose is emitted exactly like any rigid part: no bone
        # transform is applied, because the stored positions are part-local.
        obj = preview.emit_obj(mesh, include_vertex_attributes=True).decode("utf-8")
        self.assertIn("v 1.0 2.0 -3.0", obj)
        self.assertIn("vt 0.25 0.5", obj)
        self.assertEqual(2, sum(1 for line in obj.splitlines() if line.startswith("f ")))

    def test_skinned_profile_rejects_malformed_bone_records_and_slots(self) -> None:
        good_slot = float(1 * 3)
        cases: list[tuple[str, list[bytes], str]] = [
            (
                "bone_length_mismatch",
                skinned_fixture_parts(bone_record=_chunk(b"BONE", struct.pack("<3I", 0, 2, 0))),
                "invalid declared length/count",
            ),
            (
                "bone_target_out_of_range",
                skinned_fixture_parts(bone_parts=(0, 9)),
                "index out of bounds",
            ),
            (
                "declared_bones_exceed_part_count",
                skinned_fixture_parts(declared_bones=99),
                "limit exceeded",
            ),
        ]
        for name, parts, pattern in cases:
            with self.subTest(case=name):
                with self.assertRaisesRegex(preview.CmshProfileError, pattern):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        # numBones and the BONE record must agree in both directions.
        zero_bones = skinned_fixture_parts(declared_bones=0)
        with self.subTest(case="bone_record_without_declared_bones"):
            with self.assertRaisesRegex(preview.CmshProfileError, "invalid declared length/count"):
                preview.parse_cmsh_stream(build_reference_fixture_stream(zero_bones))

        # Omitting BONE leaves the accepted PRNT BBOX VHFM HORI HPOS PBKT CPOS
        # PMVB order, so only the presence check can catch it.
        missing_record = skinned_fixture_parts(bone_record=b"")
        with self.subTest(case="declared_bones_without_bone_record"):
            with self.assertRaisesRegex(preview.CmshProfileError, "BONE presence"):
                preview.parse_cmsh_stream(build_reference_fixture_stream(missing_record))

        # Per-vertex slot words must be exact non-negative multiples of three
        # whose quotient indexes the BONE array.
        slot_cases = (
            ("slot_not_a_multiple_of_three", 4.0, "invalid declared length/count"),
            ("slot_negative", -3.0, "invalid declared length/count"),
            ("slot_fractional", 1.5, "invalid declared length/count"),
            ("slot_beyond_bone_count", 6.0, "index out of bounds"),
            ("slot_non_finite", float("nan"), "non-finite numeric value"),
        )
        for name, value, pattern in slot_cases:
            vertices = bytearray(_skinned_vertices())
            struct.pack_into("<f", vertices, 12, value)
            parts = skinned_fixture_parts(geometry=_skinned_geometry(vertices=bytes(vertices)))
            with self.subTest(case=name):
                with self.assertRaisesRegex(preview.CmshProfileError, pattern):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        # The unchanged control must still parse, so the mutations above are
        # what the rejections are reacting to.
        control = bytearray(_skinned_vertices())
        struct.pack_into("<f", control, 12, good_slot)
        preview.parse_cmsh_stream(
            build_reference_fixture_stream(
                skinned_fixture_parts(geometry=_skinned_geometry(vertices=bytes(control)))
            )
        )

    def test_skinned_part_requires_the_wide_stride_and_zero_fvf(self) -> None:
        for name, stride, fvf in (
            ("rigid_stride", 36, 0),
            ("rigid_fvf", 48, 0x152),
            ("wrong_stride", 44, 0),
        ):
            geometry = bytearray(_skinned_geometry())
            cmvb = geometry.find(b"CMVB")
            struct.pack_into("<II", geometry, cmvb + 8 + 276, stride, fvf)
            parts = skinned_fixture_parts(geometry=bytes(geometry))
            with self.subTest(case=name):
                with self.assertRaisesRegex(preview.CmshProfileError, "unsupported profile"):
                    preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

        # A rigid part may not present the skinned stride either.
        rigid = bytearray(_reference_geometry())
        cmvb = rigid.find(b"CMVB")
        struct.pack_into("<II", rigid, cmvb + 8 + 276, 48, 0)
        parts = reference_fixture_parts()
        parts[1] = _multipart_part(1, parent=0, geometry=bytes(rigid))
        with self.assertRaisesRegex(preview.CmshProfileError, "unsupported profile"):
            preview.parse_cmsh_stream(build_reference_fixture_stream(parts))

    def test_parser_rejects_every_truncation_without_partial_output(self) -> None:
        stream = build_fixture_stream()
        valid_body_boundary = stream.rfind(b"BBOX")
        for length in range(len(stream)):
            if length == valid_body_boundary:
                continue
            with self.subTest(length=length):
                with self.assertRaises(preview.CmshProfileError):
                    preview.parse_cmsh_stream(stream[:length])

    def test_archive_rejects_trailing_or_incomplete_members(self) -> None:
        valid = build_fixture_aya()
        for malformed in (valid + b"x", valid[:-1], struct.pack("<I", 999) + b"short"):
            with self.assertRaises(preview.CmshProfileError):
                preview.inflate_aya(malformed)

    def test_rejects_unsupported_topology_and_secondary_vertex_payload(self) -> None:
        stream = bytearray(build_fixture_stream())
        cmvb = stream.rfind(b"CMVB")
        struct.pack_into("<I", stream, cmvb + 8 + 284, 5)
        with self.assertRaisesRegex(preview.CmshProfileError, "unsupported topology"):
            preview.parse_cmsh_stream(bytes(stream))

        stream = bytearray(build_fixture_stream())
        second_vbuf = stream.rfind(b"VBUF")
        struct.pack_into("<I", stream, second_vbuf + 4, 1)
        stream.insert(second_vbuf + 8, 0)
        mesp = stream.rfind(b"MESP", 0, second_vbuf)
        struct.pack_into("<I", stream, mesp + 4, struct.unpack_from("<I", stream, mesp + 4)[0] + 1)
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(bytes(stream))

    def test_error_text_is_path_free_and_categorized(self) -> None:
        with self.assertRaises(preview.CmshProfileError) as caught:
            preview.parse_cmsh_stream(b"NOPE")
        message = str(caught.exception)
        self.assertIn("truncation", message)
        self.assertNotIn("\\", message)
        self.assertNotIn(":/", message)

    def test_the_module_allowlists_are_exactly_the_reviewed_set(self) -> None:
        """Both allow-lists must equal the hand-reviewed restatement above.

        Fail-closed order acceptance is only as good as the review of what was
        let in, so widening either list has to be a deliberate edit in two
        places. Before 2026-07-31 the mirror had drifted six rows behind the
        module, which is what let the deletion sweep below compare mutations
        against an out-of-date set.
        """
        self.assertEqual({tuple(order) for order in REVIEWED_PART_ORDERS}, set(preview._PART_ORDERS))
        self.assertEqual(24, len(preview._PART_ORDERS))
        mirror = {
            tuple(tag.encode("ascii") for tag in siblings)
            for siblings in ACCEPTED_SIBLING_ORDERS
            if siblings
        }
        self.assertEqual(mirror, set(preview._SIBLING_ORDERS))
        self.assertEqual(8, len(preview._SIBLING_ORDERS))

    def test_all_exact_part_and_sibling_orders_are_accepted(self) -> None:
        for order in ACCEPTED_PART_ORDERS:
            with self.subTest(order=order):
                _validate_obj_semantics(preview.emit_obj(preview.parse_cmsh_stream(build_order_stream(order))))
        for siblings in ACCEPTED_SIBLING_ORDERS:
            with self.subTest(siblings=siblings):
                preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], siblings))

    def test_part_grammar_rejects_deletion_duplication_swap_insertion_and_reference(self) -> None:
        for order in ACCEPTED_PART_ORDERS:
            mutations = []
            mutations.extend(order[:index] + order[index + 1 :] for index in range(len(order)))
            mutations.extend(order[:index] + (order[index],) + order[index:] for index in range(len(order)))
            mutations.extend(order[:index] + (order[index + 1], order[index]) + order[index + 2 :] for index in range(len(order) - 1))
            mutations.extend(order[:index] + ("ZZZZ",) + order[index:] for index in range(len(order) + 1))
            for mutation in mutations:
                # A mutation that lands on another accepted order is not a
                # violation. Checked against the module's own allow-list, not
                # the mirror above, so a stale mirror can never turn a genuine
                # acceptance into a spurious failure.
                if mutation in preview._PART_ORDERS:
                    continue
                with self.subTest(order=order, mutation=mutation):
                    with self.assertRaises(preview.CmshProfileError):
                        preview.parse_cmsh_stream(build_order_stream(mutation))
        with self.assertRaisesRegex(preview.CmshProfileError, "invalid declared length/count"):
            preview.parse_cmsh_stream(build_order_stream(("REFR",) + ACCEPTED_PART_ORDERS[0]))

    def test_numeric_index_and_primitive_mutations_fail_closed(self) -> None:
        mutations: list[tuple[str, int, bytes]] = []
        original = build_order_stream(ACCEPTED_PART_ORDERS[0])
        vbuf = original.find(b"VBUF")
        ibuf = original.find(b"IBUF")
        mmpt = original.find(b"MMPT")
        mutations.append(("non-finite", vbuf + 8, struct.pack("<f", float("nan"))))
        mutations.append(("index", ibuf + 8, struct.pack("<H", 99)))
        mutations.append(("primitive", mmpt + 8 + 16, struct.pack("<I", 99)))
        for name, offset, replacement in mutations:
            stream = bytearray(original)
            stream[offset : offset + len(replacement)] = replacement
            with self.subTest(name=name):
                with self.assertRaises(preview.CmshProfileError):
                    preview.parse_cmsh_stream(bytes(stream))

    def test_guarded_local_publication_uses_only_anonymous_obj_names(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        input_parent = checkout / "local-lab" / "rebuild-godot" / "input"
        output_parent = checkout / "local-lab" / "rebuild-godot" / "generated"
        input_parent.mkdir(parents=True, exist_ok=True)
        output_parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            input_root = Path(input_temp)
            output_root = Path(output_temp) / "new" / "session"
            (input_root / "private-source-name.aya").write_bytes(build_fixture_aya())

            matches, failures = preview.publish_anonymous_previews(checkout, input_root, output_root)

            self.assertEqual((1, 0), (matches, failures))
            self.assertEqual(["candidate-0001.obj", "summary.json"], sorted(path.name for path in output_root.iterdir()))
            self.assertEqual(EXPECTED_OBJ, (output_root / "candidate-0001.obj").read_bytes())
            self.assertNotIn("private-source-name", " ".join(path.name for path in output_root.iterdir()))
            self.assertEqual(
                {
                    "schemaVersion": "onslaught-cmsh-static-preview-summary.v0",
                    "matched": 1,
                    "rejected": 0,
                    "categories": {},
                },
                json.loads((output_root / "summary.json").read_text(encoding="utf-8")),
            )
            with self.assertRaises(preview.CmshProfileError):
                preview.publish_anonymous_previews(checkout, input_root, checkout / "outside")
            with self.assertRaisesRegex(preview.CmshProfileError, "output session must be empty"):
                preview.publish_anonymous_previews(checkout, input_root, output_root)

    def test_local_publication_records_only_aggregate_path_free_rejections(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        input_parent = checkout / "local-lab" / "rebuild-godot" / "input"
        output_parent = checkout / "local-lab" / "rebuild-godot" / "generated"
        input_parent.mkdir(parents=True, exist_ok=True)
        output_parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            input_root = Path(input_temp)
            output_root = Path(output_temp)
            (input_root / "sensitive-name.aya").write_bytes(b"bad")

            self.assertEqual((0, 1), preview.publish_anonymous_previews(checkout, input_root, output_root))
            summary = (output_root / "summary.json").read_text(encoding="utf-8")
            self.assertNotIn("sensitive-name", summary)
            self.assertEqual({"truncation": 1}, json.loads(summary)["categories"])

    def test_local_preflight_is_path_free_and_checkout_cannot_be_relabelled(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        private_missing = checkout / "local-lab" / "rebuild-godot" / "input" / "private-missing"
        output = checkout / "local-lab" / "rebuild-godot" / "generated" / "unused"
        with self.assertRaises(preview.CmshProfileError) as caught:
            preview.publish_anonymous_previews(checkout, private_missing, output)
        self.assertNotIn("private-missing", str(caught.exception))
        with tempfile.TemporaryDirectory() as external:
            fake = Path(external)
            (fake / "game").mkdir()
            with self.assertRaises(preview.CmshProfileError):
                preview.publish_anonymous_previews(fake, fake / "game", fake / "local-lab" / "rebuild-godot" / "generated")

    def test_vec4_w_components_are_opaque_not_finite_numeric_fields(self) -> None:
        original = build_order_stream(ACCEPTED_PART_ORDERS[0])
        cmsp = original.find(b"CMSP")
        for payload_offset in (0x0C, 0x1C, 0x2C, 0x3C, 0x4C, 0x5C, 0x6C, 0x7C):
            stream = bytearray(original)
            struct.pack_into("<f", stream, cmsp + 8 + payload_offset, float("nan"))
            with self.subTest(payload_offset=payload_offset):
                preview.parse_cmsh_stream(bytes(stream))

    def test_profile_rejects_missing_malformed_families(self) -> None:
        original = build_order_stream(ACCEPTED_PART_ORDERS[0])
        cmvb = original.find(b"CMVB")
        cmsp = original.find(b"CMSP")
        vbuf = original.find(b"VBUF")
        cases = (
            ("stride", cmvb + 8 + 276, struct.pack("<I", 48), "unsupported profile"),
            ("FVF", cmvb + 8 + 280, struct.pack("<I", 0), "unsupported profile"),
            # Declaring bones on a part that still streams the rigid stride-36
            # FVF-0x152 vertex is rejected: numBones selects the vertex layout,
            # so the two can never disagree. BONE-record presence itself is
            # covered by test_skinned_part_profile_and_bone_slot_contract.
            ("bones", cmsp + 8 + 0xC0, struct.pack("<I", 1), "unsupported profile"),
            ("position infinity", vbuf + 8 + 0, struct.pack("<f", float("inf")), "non-finite numeric value"),
        )
        for name, offset, value, category in cases:
            stream = bytearray(original)
            stream[offset : offset + len(value)] = value
            with self.subTest(name=name):
                with self.assertRaisesRegex(preview.CmshProfileError, category):
                    preview.parse_cmsh_stream(bytes(stream))

        short_pm_vb = _chunk(b"PMVB", _cmvb(1) + _group((0, 1), owns_vertices=True))
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": short_pm_vb}))
        trailing_empty = _chunk(b"PMVB", _cmvb(0) + b"x")
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": trailing_empty}))
        for siblings in (("CEMT",), ("BBOX", "BBOX"), ("CAMD", "CEMT", "BBOX")):
            with self.subTest(siblings=siblings):
                with self.assertRaises(preview.CmshProfileError):
                    preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], siblings))

    def test_opaque_cap_and_empty_or_out_of_bounds_obj_policy(self) -> None:
        at_cap = _chunk(b"PBKT", bytes(16 * 1024 * 1024))
        preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[2], overrides={"PBKT": at_cap}))
        above_cap = _chunk(b"PBKT", bytes(16 * 1024 * 1024 + 1))
        with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[2], overrides={"PBKT": above_cap}))

        empty_pm_vb = _chunk(b"PMVB", _cmvb(0))
        empty_mesh = preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": empty_pm_vb}))
        with self.assertRaisesRegex(preview.CmshProfileError, "OBJ rejection"):
            preview.emit_obj(empty_mesh)

        stream = bytearray(build_order_stream(ACCEPTED_PART_ORDERS[0]))
        cmsp = stream.find(b"CMSP")
        struct.pack_into("<f", stream, cmsp + 8 + 0x70, 1_000_001.0)
        with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
            preview.emit_obj(preview.parse_cmsh_stream(bytes(stream)))

    def test_header_count_caps_fail_before_dependent_records(self) -> None:
        for offset in (0x0C, 0x164):
            stream = bytearray(build_order_stream(ACCEPTED_PART_ORDERS[0]))
            struct.pack_into("<I", stream, offset, 257)
            with self.subTest(offset=offset):
                with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                    preview.parse_cmsh_stream(bytes(stream))

    def test_single_owned_group_and_remaining_container_failures(self) -> None:
        single = _chunk(b"PMVB", _cmvb(1) + _group((0, 1, 2), owns_vertices=True))
        obj = preview.emit_obj(preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": single})))
        self.assertEqual(1, sum(line.startswith(b"f ") for line in obj.splitlines()))

        populated = _pmvb(populated=True)
        residual = _chunk(b"PMVB", populated[8:] + b"x")
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": residual}))

        reuse = bytearray(populated)
        second_mmpt = reuse.find(b"MMPT", reuse.find(b"MMPT") + 1)
        struct.pack_into("<I", reuse, second_mmpt + 8 + 12, 3)
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"PMVB": bytes(reuse)}))

        bad_frame = _chunk(b"VHFM", b"")
        with self.assertRaises(preview.CmshProfileError):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], overrides={"VHFM": bad_frame}))
        bad_child = _chunk(b"CHLD", struct.pack("<I", 1))
        with self.assertRaisesRegex(preview.CmshProfileError, "index out of bounds"):
            preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[1], overrides={"CHLD": bad_child}))

    def test_every_short_sibling_transition_is_exactly_allowlisted(self) -> None:
        allowed = set(ACCEPTED_SIBLING_ORDERS)
        # `PMSH` joined the sweep on 2026-07-31: it is the level archives'
        # spelling of the trailing submesh slot, so every PMSH permutation has
        # to be shown to fail closed except the two that were admitted.
        tags = ("CAMD", "BBOX", "CEMT", "PMS2", "PMSH")
        for length in range(1, 5):
            for siblings in itertools.product(tags, repeat=length):
                if siblings in allowed:
                    preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], siblings))
                else:
                    with self.assertRaises(preview.CmshProfileError):
                        preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0], siblings))

    def test_archive_and_body_caps_reject_before_profile_allocation(self) -> None:
        with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
            preview.inflate_aya(bytes(64 * 1024 * 1024 + 1))

        huge = zlib.compress(bytes(128 * 1024 * 1024 + 1), level=1)
        with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
            preview.inflate_aya(struct.pack("<I", len(huge)) + huge)

        header = bytearray(380)
        header[0:4] = b"CMSH"
        struct.pack_into("<I", header, 4, 372)
        struct.pack_into("<I", header, 0x164, 1)
        oversized_part = b"MESP" + struct.pack("<I", 32 * 1024 * 1024) + bytes(32 * 1024 * 1024)
        with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
            preview.parse_cmsh_stream(bytes(header) + _chunk(b"CMST", b"") + oversized_part)

    def test_candidate_count_and_aggregate_source_caps_are_preflight_only(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        input_parent = checkout / "local-lab" / "rebuild-godot" / "input"
        output_parent = checkout / "local-lab" / "rebuild-godot" / "generated"
        input_parent.mkdir(parents=True, exist_ok=True)
        output_parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            for index in range(257):
                (Path(input_temp) / f"{index:04d}.aya").write_bytes(b"")
            with self.assertRaisesRegex(preview.CmshProfileError, "candidate count"):
                preview.publish_anonymous_previews(checkout, Path(input_temp), Path(output_temp))
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            for index in range(5):
                with (Path(input_temp) / f"{index:04d}.aya").open("wb") as stream:
                    stream.truncate(55 * 1024 * 1024)
            with self.assertRaisesRegex(preview.CmshProfileError, "aggregate candidate"):
                preview.publish_anonymous_previews(checkout, Path(input_temp), Path(output_temp))

    def test_late_candidate_change_rolls_back_prior_atomic_obj(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        input_parent = checkout / "local-lab" / "rebuild-godot" / "input"
        output_parent = checkout / "local-lab" / "rebuild-godot" / "generated"
        input_parent.mkdir(parents=True, exist_ok=True)
        output_parent.mkdir(parents=True, exist_ok=True)
        fixture = build_fixture_aya()
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            input_root, output_root = Path(input_temp), Path(output_temp)
            (input_root / "a.aya").write_bytes(fixture)
            (input_root / "b.aya").write_bytes(fixture)
            original_read = Path.read_bytes

            def changed_second(path: Path) -> bytes:
                return b"" if path.name == "b.aya" else original_read(path)

            with mock.patch.object(Path, "read_bytes", changed_second):
                with self.assertRaisesRegex(preview.CmshProfileError, "candidate changed"):
                    preview.publish_anonymous_previews(checkout, input_root, output_root)
            self.assertEqual([], list(output_root.iterdir()))

    def test_held_metadata_change_rejects_and_rolls_back_prior_obj(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        input_parent = checkout / "local-lab" / "rebuild-godot" / "input"
        output_parent = checkout / "local-lab" / "rebuild-godot" / "generated"
        input_parent.mkdir(parents=True, exist_ok=True)
        output_parent.mkdir(parents=True, exist_ok=True)
        fixture = build_fixture_aya()
        with tempfile.TemporaryDirectory(dir=input_parent) as input_temp, tempfile.TemporaryDirectory(dir=output_parent) as output_temp:
            input_root, output_root = Path(input_temp), Path(output_temp)
            (input_root / "a.aya").write_bytes(fixture)
            target = input_root / "b.aya"
            target.write_bytes(fixture)
            original_lstat = preview.os.lstat
            target_calls = 0

            def changed_link_count(path: object, *args: object, **kwargs: object) -> object:
                nonlocal target_calls
                result = original_lstat(path, *args, **kwargs)
                if Path(path) == target:
                    target_calls += 1
                    if target_calls >= 4:
                        return SimpleNamespace(
                            st_mode=result.st_mode,
                            st_nlink=2,
                            st_size=result.st_size,
                            st_file_attributes=getattr(result, "st_file_attributes", 0),
                        )
                return result

            with mock.patch.object(preview.os, "lstat", changed_link_count):
                with self.assertRaisesRegex(preview.CmshProfileError, "held candidate"):
                    preview.publish_anonymous_previews(checkout, input_root, output_root)
            self.assertEqual([], list(output_root.iterdir()))

    def test_aggregate_and_emitter_caps_have_exact_boundaries(self) -> None:
        pmvb_payload = memoryview((_chunk(b"PMVB", _cmvb(1) + _group((0, 1, 2), owns_vertices=True)))[8:])
        for budget in (
            preview._Budget(groups=1024),
            preview._Budget(vertices=99_999),
            preview._Budget(indices=599_999),
        ):
            with self.subTest(budget=budget):
                with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                    preview._parse_pm_vb(pmvb_payload, 0, budget)

        mesh = preview.parse_cmsh_stream(build_order_stream(ACCEPTED_PART_ORDERS[0]))
        identity_obj = preview.emit_obj(mesh)
        with mock.patch.object(preview, "MAX_TRIANGLES", 2):
            self.assertEqual(identity_obj, preview.emit_obj(mesh))
        with mock.patch.object(preview, "MAX_TRIANGLES", 1):
            with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                preview.emit_obj(mesh)
        with mock.patch.object(preview, "MAX_OBJ", len(identity_obj)):
            self.assertEqual(identity_obj, preview.emit_obj(mesh))
        with mock.patch.object(preview, "MAX_OBJ", len(identity_obj) - 1):
            with self.assertRaisesRegex(preview.CmshProfileError, "limit exceeded"):
                preview.emit_obj(mesh)

    def test_broken_reparse_dirent_is_not_treated_as_a_missing_output_component(self) -> None:
        checkout = Path(__file__).resolve().parents[2]
        root = checkout / "local-lab" / "rebuild-godot" / "generated"
        path = root / "broken" / "session"

        def lexical_exists(candidate: object) -> bool:
            return Path(candidate).name == "broken"

        def reparse(candidate: Path) -> bool:
            return candidate.name == "broken"

        with mock.patch.object(preview.os.path, "lexists", lexical_exists), mock.patch.object(preview, "_has_reparse_point", reparse):
            with self.assertRaisesRegex(preview.CmshProfileError, "reparse component"):
                preview._validate_no_reparse_descendant(path, root, checkout, must_exist=False)


def _mutate_part(mesh: preview.ParsedMesh, index: int, **changes: object) -> preview.ParsedMesh:
    """Replace one field of one part in both the file view and the geometry view."""
    parts = list(mesh.file_parts())
    parts[index] = replace(parts[index], **changes)
    return replace(mesh, source_parts=tuple(parts), parts=tuple(parts))


class CmshReEmitterTests(unittest.TestCase):
    """`emit_cmsh_stream` is the inverse of `parse_cmsh_stream`, byte for byte."""

    def _round_trip(self, source: bytes) -> preview.ByteAccounting:
        identical, account = preview.round_trip_cmsh_stream(source)
        self.assertTrue(identical)
        # Every byte of the stream lands in exactly one accounting column.
        self.assertEqual(len(source), account.total)
        return account

    def test_the_fixture_stream_re_emits_byte_identically(self) -> None:
        self._round_trip(build_fixture_stream())

    def test_the_reference_fixture_stream_re_emits_byte_identically(self) -> None:
        """`REFR` expansion rewrites the geometry view, never the file view."""
        source = build_reference_fixture_stream()
        mesh = preview.parse_cmsh_stream(source)
        referring = [part for part in mesh.parts if part.reference is not None]
        self.assertTrue(referring)
        self.assertTrue(any(part.vertices for part in referring))
        self.assertFalse(any(part.vertices for part in mesh.file_parts() if part.reference is not None))
        self._round_trip(source)

    def test_the_skinned_fixture_stream_re_emits_byte_identically(self) -> None:
        """The bone slots the parser divided by three are multiplied back."""
        source = build_skinned_fixture_stream()
        mesh = preview.parse_cmsh_stream(source)
        self.assertTrue(any(part.bones for part in mesh.file_parts()))
        self._round_trip(source)

    def test_the_hierarchy_frame_and_material_fixtures_re_emit_byte_identically(self) -> None:
        self._round_trip(build_hierarchy_frame_fixture_stream())
        self._round_trip(build_material_fixture_stream())

    def test_every_synthesisable_part_and_sibling_order_re_emits_byte_identically(self) -> None:
        for order in ACCEPTED_PART_ORDERS:
            for siblings in ACCEPTED_SIBLING_ORDERS:
                with self.subTest(order=order, siblings=siblings):
                    self._round_trip(build_order_stream(order, siblings))

    def test_re_emission_reads_the_model_and_not_a_replayed_copy(self) -> None:
        """The check is proven able to fail: perturb the model, the bytes move.

        Each field below is rebuilt from typed state with no raw fallback, so a
        change to it must change the emitted stream. If any of these ever came
        back identical the round-trip would be replaying bytes rather than
        proving the model, and the corpus result would mean nothing.
        """
        source = build_fixture_stream()
        mesh = preview.parse_cmsh_stream(source)
        geometry = next(index for index, part in enumerate(mesh.file_parts()) if part.vertices)
        part = mesh.file_parts()[geometry]
        first = part.vertices[0]
        group = part.groups[0]
        perturbations = {
            "vertex position": {"vertices": (replace(first, position=(first.position[0] + 1.0, *first.position[1:])), *part.vertices[1:])},
            "vertex normal": {"vertices": (replace(first, normal=(0.0, 0.0, -1.0)), *part.vertices[1:])},
            "vertex colour": {"vertices": (replace(first, raw_color_u32=0xFF00FF00), *part.vertices[1:])},
            "vertex UV": {"vertices": (replace(first, uv=(0.125, 0.375)), *part.vertices[1:])},
            "strip indices": {"groups": (replace(group, indices=group.indices[:-1]), *part.groups[1:])},
            "TEXR slots": {"groups": (replace(group, raw_texr_u32=(9, 9, 9, 9, 9, 9)), *part.groups[1:])},
            "bounding box": {"bounding_box": replace(part.bounding_box, radius=part.bounding_box.radius + 1.0)},
            "bounding box pad word": {"bounding_box": replace(part.bounding_box, pad_words=(0xDEADBEEF, 0))},
            "hierarchy frame map": {"track": replace(part.track, frame_map=(0,) * len(part.track.frame_map))},
            "hierarchy pose": {
                "track": replace(
                    part.track,
                    hierarchy=(replace(part.track.hierarchy[0], position=(1.5, 2.5, 3.5)), *part.track.hierarchy[1:]),
                )
            },
            "hierarchy pose pad word": {
                "track": replace(
                    part.track,
                    hierarchy=(replace(part.track.hierarchy[0], position_pad_word=0x0BADF00D), *part.track.hierarchy[1:]),
                )
            },
            "carried CPOS payload": {"track": replace(part.track, cached_position_bytes=b"\x09\x09")},
        }
        for label, changes in perturbations.items():
            with self.subTest(field=label):
                emitted, _ = preview.emit_cmsh_stream(_mutate_part(mesh, geometry, **changes))
                self.assertNotEqual(source, emitted, f"{label} left the stream unchanged")
        # The carried blocks go back out unaltered, so a change to one still has
        # to move the stream - on the part that actually carries the record.
        carrier = next(index for index, item in enumerate(mesh.file_parts()) if item.raw_pbkt)
        emitted, _ = preview.emit_cmsh_stream(_mutate_part(mesh, carrier, raw_pbkt=b"OPAQUE"))
        self.assertNotEqual(source, emitted)

    def test_a_derived_field_that_contradicts_its_carried_block_is_a_hard_failure(self) -> None:
        """A model that disagrees with the bytes is a defect, never a rounding.

        Every field written into a carried block is proved against it first, so
        a contradiction stops the emission rather than quietly preferring one
        side. Without this, carrying `CMSP` whole would hide the whole identity
        and count model behind a memcpy.
        """
        source = build_fixture_stream()
        mesh = preview.parse_cmsh_stream(source)
        geometry = next(index for index, part in enumerate(mesh.file_parts()) if part.vertices)
        contradictions = (
            ("part type", 0, {"part_type": 6}),
            ("animation frame count", 0, {"anim_frames": 2}),
            (
                "CMSP base position",
                0,
                {
                    "cmsp_transforms": (
                        mesh.file_parts()[0].cmsp_transforms[0],
                        replace(mesh.file_parts()[0].cmsp_transforms[1], position=(9.0, 9.0, 9.0)),
                    )
                },
            ),
            ("retained CMSP payload", 0, {"raw_cmsp": bytes(316)}),
            ("CMVB group count", geometry, {"raw_cmvb": bytes(296)}),
        )
        for label, index, changes in contradictions:
            with self.subTest(field=label):
                with self.assertRaisesRegex(preview.CmshProfileError, "round-trip mismatch"):
                    preview.emit_cmsh_stream(_mutate_part(mesh, index, **changes))

    def test_a_truncated_retained_block_is_refused_rather_than_padded(self) -> None:
        mesh = preview.parse_cmsh_stream(build_fixture_stream())
        for label, changes in (
            ("CMSP", {"raw_cmsp": b""}),
            ("CMVB", {"raw_cmvb": b"\x00" * 295}),
            ("HFOV", {"record_tags": ("HFOV",), "raw_hfov": None}),
        ):
            with self.subTest(block=label):
                with self.assertRaisesRegex(preview.CmshProfileError, "round-trip mismatch"):
                    preview.emit_cmsh_stream(_mutate_part(mesh, 0, **changes))
        with self.assertRaisesRegex(preview.CmshProfileError, "round-trip mismatch"):
            preview.emit_cmsh_stream(replace(mesh, raw_header=b""))

    def test_a_conditionally_derived_name_buffer_degrades_to_carried_and_says_so(self) -> None:
        """A name that no longer re-encodes is carried, and the count shows it.

        This is the honest failure mode of every field that is derived only
        where the bytes prove it: the stream still comes back identical, but the
        derived column drops by exactly the buffer that stopped being claimed.
        Byte identity alone would not have shown it; the accounting does.
        """
        source = build_material_fixture_stream()
        mesh = preview.parse_cmsh_stream(source)
        _, before = preview.emit_cmsh_stream(mesh)
        renamed = replace(
            mesh,
            textures=(replace(mesh.textures[0], name="not the stored name"), *mesh.textures[1:]),
        )
        emitted, after = preview.emit_cmsh_stream(renamed)
        self.assertEqual(source, emitted)
        self.assertEqual(
            len(mesh.textures[0].raw_name_field),
            before.regions[preview.REGION_TEXB_NAME][0] - after.regions[preview.REGION_TEXB_NAME][0],
        )

    def test_the_wholly_carried_regions_are_declared(self) -> None:
        """The disclosure list cannot shrink without a deliberate edit here."""
        self.assertEqual(
            {
                preview.REGION_CMST,
                preview.REGION_TEXB_METADATA,
                preview.REGION_PBKT,
                preview.REGION_CPOS,
                preview.REGION_CORI,
                preview.REGION_SIBLING,
            },
            set(preview.WHOLLY_CARRIED_REGIONS),
        )
        account = self._round_trip(build_fixture_stream())
        for region in preview.WHOLLY_CARRIED_REGIONS:
            if region in account.regions:
                self.assertEqual(0, account.regions[region][0], region)

    def test_the_accounting_never_folds_carried_bytes_into_derived(self) -> None:
        account = self._round_trip(build_fixture_stream())
        self.assertEqual(account.derived + account.carried, account.total)
        self.assertGreater(account.carried, 0)
        self.assertGreater(account.derived, 0)
        merged = preview.ByteAccounting()
        merged.merge(account)
        merged.merge(account)
        self.assertEqual(2 * account.derived, merged.derived)
        self.assertEqual(2 * account.carried, merged.carried)


def _shipped_mesh_directory() -> Path:
    """Where the released meshes are materialised on this machine.

    `local-lab/` is gitignored and exists only in the checkout it was populated
    in, so a linked worktree has to be told where it is. The census skips
    entirely when neither location is present, which is the case in a clone.
    """
    override = os.environ.get("ONSLAUGHT_MESH_CORPUS")
    if override:
        return Path(override)
    return (
        Path(__file__).resolve().parents[2]
        / "local-lab"
        / "safe-copy-bea-pristine"
        / "data"
        / "resources"
        / "meshes"
    )


SHIPPED_MESHES = _shipped_mesh_directory()


@unittest.skipUnless(
    SHIPPED_MESHES.is_dir(),
    "the retail mesh corpus is materialised locally and is not part of a clone",
)
class ShippedMeshCorpusCensus(unittest.TestCase):
    """Census of the released mesh corpus. Nothing retail-derived is written."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.meshes = {}
        for path in sorted(SHIPPED_MESHES.glob("*.msh.aya")):
            cls.meshes[path.stem] = preview.parse_cmsh_stream(
                preview.inflate_aya(path.read_bytes())
            )

    def test_every_shipped_mesh_parses(self) -> None:
        self.assertEqual(213, len(self.meshes))

    def test_bone_records_appear_in_exactly_the_seven_skinned_meshes(self) -> None:
        skinned = {
            name: tuple(len(part.bones) for part in mesh.parts if part.bones)
            for name, mesh in self.meshes.items()
            if any(part.bones for part in mesh.parts)
        }
        self.assertEqual(
            {
                "m_Sentinel Arm Big.msh": (14,),
                "m_Sentinel Arm Small.msh": (14,),
                "m_f_dtroop.msh": (18,),
                "m_ftrooper.msh": (18,),
                "m_mcommando.msh": (18,),
                "m_mfiredude.msh": (19,),
                "m_mgrunt.msh": (19,),
            },
            skinned,
        )
        # Exactly one part per skinned mesh carries bones, and it is part 1 in
        # every case; every bone names a part of the same mesh.
        for name, mesh in self.meshes.items():
            carriers = [index for index, part in enumerate(mesh.parts) if part.bones]
            if not carriers:
                continue
            self.assertEqual([1], carriers, name)
            for bone in mesh.parts[1].bones:
                self.assertLess(bone, len(mesh.parts), name)
            # Every skinned vertex carries three in-range slots.
            for vertex in mesh.parts[1].vertices:
                self.assertIsNotNone(vertex.bone_slots, name)
                self.assertEqual(3, len(vertex.bone_slots), name)
                for slot in vertex.bone_slots:
                    self.assertLess(slot, len(mesh.parts[1].bones), name)

    def test_non_finite_uvs_are_confined_to_the_five_in_m_m_prison(self) -> None:
        degraded = {
            name: mesh.non_finite_uv_count
            for name, mesh in self.meshes.items()
            if mesh.non_finite_uv_count
        }
        self.assertEqual({"m_M_Prison.msh": 5}, degraded)
        # Their raw dwords are retained, and each is two 16-bit halves of a fill
        # pattern rather than an authored coordinate.
        halves = set()
        for part in self.meshes["m_M_Prison.msh"].parts:
            for vertex in part.vertices:
                if vertex.uv is None:
                    self.assertIsNotNone(vertex.raw_uv_u32)
                    for word in vertex.raw_uv_u32:
                        halves.add(word & 0xFFFF)
                        halves.add(word >> 16)
        self.assertTrue(
            halves <= {0xFFFF, 0x7FFF, 0x2AAA, 0x3333, 0x9999, 0xCCCC, 0xD555}, sorted(halves)
        )

    def test_only_m_building_5_top_references_an_empty_part(self) -> None:
        empty = {}
        for name, mesh in self.meshes.items():
            for index, part in enumerate(mesh.parts):
                if part.reference is not None and not part.vertices:
                    empty.setdefault(name, []).append(index)
        self.assertEqual({"m_Building 5 Top.msh": [2, 3]}, empty)

    def test_the_header_names_the_mesh_after_its_own_file(self) -> None:
        """`CMSH+0x2C` is the mesh's own name - found by writing the re-emitter.

        The parser read two words of the 372-byte header and skimmed the rest,
        so this field was invisible until an encoder had to reproduce it. It
        matches the source file's stem without the `m_` prefix on 213/213 loose
        shipped meshes, case-insensitively; 20 of them disagree on case alone,
        the same authoring hazard as `meshtex\\FB_biodome.tga`.
        """
        exact = insensitive = 0
        for name, mesh in self.meshes.items():
            stem = name[2:]
            exact += mesh.name == stem
            insensitive += mesh.name.casefold() == stem.casefold()
        self.assertEqual(213, insensitive)
        self.assertEqual(193, exact)

    def test_the_nine_formerly_unsupported_meshes_emit_obj(self) -> None:
        formerly_unsupported = (
            "m_Building 5 Top.msh",
            "m_M_Prison.msh",
            "m_Sentinel Arm Big.msh",
            "m_Sentinel Arm Small.msh",
            "m_f_dtroop.msh",
            "m_ftrooper.msh",
            "m_mcommando.msh",
            "m_mfiredude.msh",
            "m_mgrunt.msh",
        )
        for name in formerly_unsupported:
            with self.subTest(mesh=name):
                obj = preview.emit_obj(
                    self.meshes[name],
                    include_vertex_attributes=True,
                    include_material_layer_groups=True,
                    include_vertex_colors=True,
                )
                text = obj.decode("utf-8")
                self.assertTrue(any(line.startswith("v ") for line in text.splitlines()))
                self.assertTrue(any(line.startswith("f ") for line in text.splitlines()))
                # No invented coordinate ever reaches the output.
                self.assertNotIn("nan", text.lower())
                self.assertNotIn("inf", text.lower())
                # Every face element uses one reference form throughout.
                for line in text.splitlines():
                    if line.startswith("f "):
                        references = line[2:].split()
                        self.assertEqual(1, len({"//" in item for item in references}), line)
                        self.assertEqual(1, len({item.count("/") for item in references}), line)


_CMSH_HEADER = 380
_PMS2_HEADER = 309
_CONTAINER_TAGS = (b"MESH", b"PMSH", b"IMPS", b"SURF", b"LNDS", b"OBJS", b"BLDS")


def _walk_chunks(buf: memoryview):
    """Sequential tag/length walk. Stops at the first record that overruns."""
    position = 0
    while position + 8 <= len(buf):
        tag = bytes(buf[position : position + 4])
        length = struct.unpack_from("<I", buf, position + 4)[0]
        if position + 8 + length > len(buf):
            return
        yield tag, buf[position + 8 : position + 8 + length], position
        position += 8 + length


def _is_chunk_stream(payload: memoryview) -> bool:
    position = 0
    seen = 0
    if len(payload) < 8:
        return False
    while position < len(payload):
        if position + 8 > len(payload):
            return False
        if not all(0x20 <= byte < 0x7F for byte in payload[position : position + 4]):
            return False
        length = struct.unpack_from("<I", payload, position + 4)[0]
        if position + 8 + length > len(payload):
            return False
        position += 8 + length
        seen += 1
    return seen > 0


def _is_cmsh(buf, offset: int = 0) -> bool:
    return (
        len(buf) - offset >= _CMSH_HEADER
        and bytes(buf[offset : offset + 4]) == b"CMSH"
        and struct.unpack_from("<I", buf, offset + 4)[0] == 372
    )


def _scan_cmsh(data: bytes) -> list[int]:
    """Byte-signature sweep, used only as a completeness oracle for the walk."""
    hits = []
    index = data.find(b"CMSH")
    while index != -1:
        if _is_cmsh(data, index):
            hits.append(index)
        index = data.find(b"CMSH", index + 1)
    return hits


def _post_body_siblings(data: bytes):
    """Structurally walk a CMSH stream to its post-body sibling list.

    Deliberately independent of `parse_cmsh_stream`, which refuses the stream
    outright on an unknown order and so cannot be used to census one.
    """
    buf = memoryview(data)
    texture_count = struct.unpack_from("<I", buf, 0x0C)[0]
    part_count = struct.unpack_from("<I", buf, 0x164)[0]

    def read(position):
        if position + 8 > len(buf):
            return None
        tag = bytes(buf[position : position + 4])
        length = struct.unpack_from("<I", buf, position + 4)[0]
        if position + 8 + length > len(buf):
            return None
        return tag, buf[position + 8 : position + 8 + length], position + 8 + length

    position = _CMSH_HEADER
    for expected, repeat in ((b"CMST", 1), (b"MSHT", texture_count), (b"MESP", part_count)):
        for _ in range(repeat):
            record = read(position)
            if record is None or record[0] != expected:
                return None
            position = record[2]
    siblings = []
    while position < len(buf):
        record = read(position)
        if record is None:
            return None
        siblings.append((record[0], record[1]))
        position = record[2]
    return siblings


def _nested_streams(data: bytes, locus: str, out: list, shapes: collections.Counter) -> None:
    """Every further CMSH stream reachable through a post-body sibling."""
    siblings = _post_body_siblings(data)
    if siblings is None:
        return
    for tag, payload in siblings:
        if tag == b"PMSH":
            children = list(_walk_chunks(payload)) if _is_chunk_stream(payload) else []
            shapes["PMSH shape " + "+".join(bytes(t).decode("ascii", "replace") for t, _, _ in children)] += 1
            for child_tag, child_payload, offset in children:
                if child_tag != b"PMS2":
                    continue
                if len(child_payload) > _PMS2_HEADER and _is_cmsh(child_payload, _PMS2_HEADER):
                    shapes["PMSH/PMS2 body CMSH at +309"] += 1
                    nested = bytes(child_payload[_PMS2_HEADER:])
                    where = f"{locus}/sibPMSH/PMS2@{offset}+309"
                    out.append((where, nested))
                    _nested_streams(nested, where, out, shapes)
                else:
                    shapes["PMSH/PMS2 body not CMSH at +309"] += 1
        elif tag == b"PMS2":
            if len(payload) > _PMS2_HEADER and _is_cmsh(payload, _PMS2_HEADER):
                shapes["PMS2 sibling body CMSH at +309"] += 1
                nested = bytes(payload[_PMS2_HEADER:])
            elif _is_cmsh(payload, 0):
                shapes["PMS2 sibling body bare CMSH at +0"] += 1
                nested = bytes(payload)
            else:
                shapes["PMS2 sibling body carries no CMSH"] += 1
                continue
            where = f"{locus}/sibPMS2"
            out.append((where, nested))
            _nested_streams(nested, where, out, shapes)


SHIPPED_RESOURCES = SHIPPED_MESHES.parent


@unittest.skipUnless(
    SHIPPED_MESHES.is_dir() and (SHIPPED_RESOURCES / "500_res_PC.aya").is_file(),
    "the retail corpora are materialised locally and are not part of a clone",
)
class NestedAndEmbeddedCorpusCensus(unittest.TestCase):
    """Every CMSH stream in both retail corpora, not just the top-level meshes.

    Pins the measured conversion rate so a widening or narrowing of the order
    allow-lists cannot move it silently. Read-only; nothing is written.
    """

    @classmethod
    def setUpClass(cls) -> None:
        cls.streams: dict[str, list[tuple[str, str, bytes]]] = {
            "loose": [],
            "loose-nested": [],
            "embedded": [],
        }
        cls.shapes: collections.Counter = collections.Counter()
        cls.locate: collections.Counter = collections.Counter()

        for path in sorted(SHIPPED_MESHES.glob("*.msh.aya")):
            data = preview.inflate_aya(path.read_bytes())
            cls.locate["loose signature hits"] += len(_scan_cmsh(data))
            cls.streams["loose"].append((path.name, "file", data))
            nested: list = []
            _nested_streams(data, "file", nested, cls.shapes)
            for where, stream in nested:
                cls.streams["loose-nested"].append((path.name, where, stream))

        archives = sorted(
            candidate
            for candidate in SHIPPED_RESOURCES.glob("*_res_PC.aya")
            if candidate.stem.split("_")[0].isdigit()
        )
        cls.locate["level archives"] = len(archives)
        for path in archives:
            buf = memoryview(preview.inflate_aya(path.read_bytes()))
            cls.locate["embedded signature hits"] += len(_scan_cmsh(bytes(buf)))
            found: list = []
            containers: list = []

            def descend(payload, where, depth=0):
                if depth > 10 or not _is_chunk_stream(payload):
                    return
                for tag, child, offset in _walk_chunks(payload):
                    if tag == b"PMS2":
                        containers.append((child, f"{where}/PMS2@{offset}"))
                    elif tag in _CONTAINER_TAGS:
                        descend(child, f"{where}/{tag.decode('ascii', 'replace')}", depth + 1)

            for tag, payload, offset in _walk_chunks(buf):
                if tag == b"PMS2":
                    containers.append((payload, f"PMS2@{offset}"))
                else:
                    descend(payload, tag.decode("ascii", "replace"))
            cls.locate["embedded PMS2 chunks"] += len(containers)
            for payload, where in containers:
                if len(payload) <= _PMS2_HEADER:
                    continue
                cls.locate["embedded PMS2 with a body"] += 1
                if not _is_cmsh(payload, _PMS2_HEADER):
                    cls.locate["embedded PMS2 body not CMSH"] += 1
                    continue
                cls.locate["embedded CMSH at PMS2 +309"] += 1
                stream = bytes(payload[_PMS2_HEADER:])
                nested_where = f"{where}+309"
                found.append((nested_where, stream))
                before = len(found)
                _nested_streams(stream, nested_where, found, cls.shapes)
                cls.locate["embedded CMSH nested deeper"] += len(found) - before
            for where, stream in found:
                cls.streams["embedded"].append((path.name, where, stream))

    def test_the_structural_walk_finds_every_cmsh_signature_in_both_corpora(self) -> None:
        """No stream is reached by byte-scanning alone, so none is a coincidence."""
        self.assertEqual(213, len(self.streams["loose"]))
        self.assertEqual(15, len(self.streams["loose-nested"]))
        self.assertEqual(139, len(self.streams["embedded"]))
        self.assertEqual(228, self.locate["loose signature hits"])
        self.assertEqual(
            self.locate["loose signature hits"],
            len(self.streams["loose"]) + len(self.streams["loose-nested"]),
        )
        self.assertEqual(139, self.locate["embedded signature hits"])
        self.assertEqual(self.locate["embedded signature hits"], len(self.streams["embedded"]))
        self.assertEqual(66, self.locate["level archives"])
        self.assertEqual(3485, self.locate["embedded PMS2 chunks"])
        self.assertEqual(225, self.locate["embedded PMS2 with a body"])
        self.assertEqual(53, self.locate["embedded CMSH at PMS2 +309"])
        self.assertEqual(86, self.locate["embedded CMSH nested deeper"])

    def test_pmsh_is_the_archives_spelling_of_the_pms2_submesh_slot(self) -> None:
        """The evidence that admitted `BBOX PMSH` and `CAMD BBOX CEMT PMSH`.

        Every post-body `PMSH` sibling in the corpus holds exactly one `PMS2`,
        and every one of those carries a complete CMSH stream at `+309`. If a
        single counterexample ever ships, this fails rather than quietly
        accepting a tag that means something else.
        """
        self.assertEqual(86, self.shapes["PMSH shape PMS2"])
        self.assertEqual(
            86, sum(count for shape, count in self.shapes.items() if shape.startswith("PMSH shape "))
        )
        self.assertEqual(86, self.shapes["PMSH/PMS2 body CMSH at +309"])
        self.assertEqual(0, self.shapes["PMSH/PMS2 body not CMSH at +309"])
        # The loose lane omits the wrapper and puts a bare, header-less CMSH
        # directly in the `PMS2` sibling payload - which is why those `+309`
        # bytes read as zeros and the nested loose streams went uncounted.
        self.assertEqual(15, self.shapes["PMS2 sibling body bare CMSH at +0"])
        self.assertEqual(0, self.shapes["PMS2 sibling body CMSH at +309"])
        self.assertEqual(0, self.shapes["PMS2 sibling body carries no CMSH"])

    def test_the_measured_conversion_rate_is_pinned(self) -> None:
        """367 streams parse; 366 emit an OBJ.

        Measured 2026-07-31 against the shipped allow-lists. Baseline before the
        four legitimate orders were admitted was loose 213/213, loose-nested
        11/15, embedded 35/139 - the embedded lane was 25.2%. A regression in
        the loose lane is the hard failure to watch: those 213 are the meshes
        this tool has always converted.
        """
        parsed: collections.Counter = collections.Counter()
        emitted: collections.Counter = collections.Counter()
        rejections: dict[str, str] = {}
        for corpus, entries in self.streams.items():
            for source, where, data in entries:
                try:
                    mesh = preview.parse_cmsh_stream(data)
                except preview.CmshProfileError as error:
                    rejections[f"{corpus} parse {source}::{where}"] = str(error)
                    continue
                parsed[corpus] += 1
                try:
                    preview.emit_obj(
                        mesh,
                        include_vertex_attributes=True,
                        include_material_layer_groups=True,
                        include_vertex_colors=True,
                    )
                except preview.CmshProfileError as error:
                    rejections[f"{corpus} obj {source}::{where}"] = str(error)
                    continue
                emitted[corpus] += 1

        self.assertEqual(213, parsed["loose"], "the loose corpus must never regress")
        self.assertEqual(213, emitted["loose"], "the loose corpus must never regress")
        self.assertEqual(15, parsed["loose-nested"])
        self.assertEqual(14, emitted["loose-nested"])
        self.assertEqual(139, parsed["embedded"])
        self.assertEqual(139, emitted["embedded"])
        self.assertEqual(367, sum(parsed.values()))
        self.assertEqual(366, sum(emitted.values()))

        # The single stream that parses but emits nothing is a geometry-less
        # placeholder node, which `emit_obj` refuses by contract. That is a
        # property of the shipped asset, not a gap in the decode - so it is
        # named here rather than counted as a rejection class.
        self.assertEqual(1, len(rejections), rejections)
        [(where, message)] = rejections.items()
        self.assertIn("m_Boss_gill-m-Node.msh.aya", where)
        self.assertIn("empty geometry", message)

    def test_every_shipped_cmsh_stream_re_emits_byte_identically(self) -> None:
        """367/367 streams round-trip, and the honest split is pinned with them.

        Measured 2026-07-31 over both retail corpora. Byte identity alone would
        be a misleading headline, so the derived/carried split is pinned beside
        it: 32.05% of the 100,813,615 stream bytes are rebuilt from typed model
        state and proved against the source, and 67.95% are copied through
        because nothing in the model describes them. A change that moves bytes
        from the derived column to the carried one keeps the streams identical
        and fails here instead.
        """
        identical: collections.Counter = collections.Counter()
        total = preview.ByteAccounting()
        for corpus, entries in self.streams.items():
            for source, where, data in entries:
                mesh = preview.parse_cmsh_stream(data)
                emitted, account = preview.emit_cmsh_stream(mesh)
                total.merge(account)
                self.assertEqual(data, emitted, f"{corpus} {source}::{where}")
                self.assertEqual(len(data), account.total, f"{corpus} {source}::{where}")
                identical[corpus] += 1

        self.assertEqual(213, identical["loose"], "the loose corpus must never regress")
        self.assertEqual(15, identical["loose-nested"])
        self.assertEqual(139, identical["embedded"])
        self.assertEqual(367, sum(identical.values()))

        self.assertEqual(100_813_615, total.total)
        self.assertEqual(32_313_656, total.derived)
        self.assertEqual(68_499_959, total.carried)
        # The six regions no byte is ever derived from, with their measured
        # weight. `post-body sibling` holds the nested CMSH streams, which are
        # round-tripped as streams in their own right elsewhere in this count;
        # `PBKT` at 15 MB is the largest block nothing in the tree interprets.
        self.assertEqual(
            {
                preview.REGION_SIBLING: 23_446_373,
                preview.REGION_CORI: 20_585_088,
                preview.REGION_PBKT: 15_003_828,
                preview.REGION_CPOS: 6_907_456,
                preview.REGION_CMST: 63_324,
                preview.REGION_TEXB_METADATA: 35_180,
            },
            {region: total.regions[region][1] for region in preview.WHOLLY_CARRIED_REGIONS},
        )
        for region in preview.WHOLLY_CARRIED_REGIONS:
            self.assertEqual(0, total.regions[region][0], region)
        # The geometry, the transform tracks and every count and length are
        # rebuilt outright, with no retained payload to fall back on.
        for region in (
            preview.REGION_VBUF,
            preview.REGION_IBUF,
            preview.REGION_MMPT,
            preview.REGION_TEXR,
            preview.REGION_HORI,
            preview.REGION_HPOS,
            preview.REGION_VHFM,
            preview.REGION_BBOX,
            preview.REGION_LINKS,
            preview.REGION_FRAMING,
            preview.REGION_TEXB_NAME,
        ):
            self.assertEqual(0, total.regions[region][1], region)

    def test_the_corpus_round_trip_is_able_to_fail(self) -> None:
        """One perturbed byte must go red, or the count above proves nothing."""
        _, where, data = self.streams["loose"][0]
        emitted, _ = preview.emit_cmsh_stream(preview.parse_cmsh_stream(data))
        self.assertEqual(data, emitted, where)
        for offset in (0, len(data) // 2, len(data) - 1):
            perturbed = bytearray(emitted)
            perturbed[offset] ^= 0x01
            self.assertNotEqual(data, bytes(perturbed))

    def test_no_part_or_sibling_order_in_either_corpus_is_rejected(self) -> None:
        """Order acceptance, isolated from every other profile check.

        The rate test above could be held up by a bounds or index check even if
        an order were rejected, so the order gap is pinned to zero on its own.
        """
        for corpus, entries in self.streams.items():
            for source, where, data in entries:
                try:
                    preview.parse_cmsh_stream(data)
                except preview.CmshProfileError as error:
                    self.fail(f"{corpus} {source}::{where}: {error}")


if __name__ == "__main__":
    unittest.main()
