#!/usr/bin/env python3
"""Fail-closed, path-free structural analyzer for held AYA CMSH inputs.

This tool reports source-informed identity evidence.  It deliberately does not
compose parent/runtime transforms, decode textures, or assign a gameplay role.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
import math
import os
from pathlib import Path
import re
import stat
import struct
import sys
import zlib


MAX_SOURCE = 64 * 1024 * 1024
MAX_INFLATED = 128 * 1024 * 1024
MAX_MEMBERS = 4096
MAX_TEXTURES = 256
MAX_PARTS = 256
MAX_GROUPS = 1024
MAX_VERTICES = 100_000
MAX_INDICES = 600_000
MAX_OPAQUE = 16 * 1024 * 1024
SCHEMA = "onslaught.aya-battleengine-identity-analysis.v1"


class AnalysisError(ValueError):
    pass


@dataclass(frozen=True)
class Chunk:
    tag: bytes
    payload: bytes
    offset: int


class Reader:
    def __init__(self, data: bytes, *, origin: int = 0):
        self.data = data
        self.origin = origin
        self.pos = 0

    def chunk(self, role: str) -> Chunk:
        if len(self.data) - self.pos < 8:
            raise AnalysisError(f"malformed framing: truncated {role} header")
        offset = self.origin + self.pos
        tag = self.data[self.pos : self.pos + 4]
        length = struct.unpack_from("<I", self.data, self.pos + 4)[0]
        self.pos += 8
        if length > len(self.data) - self.pos:
            raise AnalysisError(f"malformed framing: {role} length")
        payload = self.data[self.pos : self.pos + length]
        self.pos += length
        return Chunk(tag, payload, offset)

    def expected(self, tag: bytes, role: str, *, length: int | None = None) -> Chunk:
        result = self.chunk(role)
        if result.tag != tag:
            raise AnalysisError(f"malformed framing: expected {tag.decode('ascii')} for {role}")
        if length is not None and len(result.payload) != length:
            raise AnalysisError(f"malformed framing: {role} length")
        return result

    def end(self, role: str) -> None:
        if self.pos != len(self.data):
            raise AnalysisError(f"malformed framing: residue in {role}")


def _u32(data: bytes, offset: int, role: str) -> int:
    if offset < 0 or offset + 4 > len(data):
        raise AnalysisError(f"malformed framing: {role}")
    return struct.unpack_from("<I", data, offset)[0]


def _fixed_name(data: bytes, role: str, *, allow_relative: bool) -> str:
    raw = data.split(b"\0", 1)[0]
    try:
        value = raw.decode("ascii")
    except UnicodeDecodeError as error:
        raise AnalysisError(f"private path or invalid {role}") from error
    if not value or any(ord(char) < 32 or ord(char) == 127 for char in value):
        raise AnalysisError(f"private path or invalid {role}")
    if allow_relative:
        _validate_relative_resource(value, role)
    elif "/" in value or "\\" in value or ":" in value or value in {".", ".."}:
        raise AnalysisError(f"private path in {role}")
    return value


def _validate_relative_resource(value: str, role: str) -> None:
    normalized = value.replace("\\", "/")
    components = normalized.split("/")
    if normalized.startswith("/") or ":" in normalized or any(part in {"", ".", ".."} for part in components):
        raise AnalysisError(f"private path in {role}")


def _validate_identity(value: str, role: str) -> str:
    if not re.fullmatch(r"[A-Za-z0-9_.-]+", value) or value in {".", ".."}:
        raise AnalysisError(f"private path in {role}")
    return value


def _inflate_aya(source: bytes) -> bytes:
    if len(source) > MAX_SOURCE:
        raise AnalysisError("AYA framing: source limit")
    output = bytearray()
    pos = 0
    member_count = 0
    while pos < len(source):
        if member_count >= MAX_MEMBERS:
            raise AnalysisError("AYA framing: member count limit")
        if len(source) - pos < 4:
            raise AnalysisError("AYA framing: truncated member header")
        length = struct.unpack_from("<I", source, pos)[0]
        pos += 4
        if length == 0 or length > len(source) - pos:
            raise AnalysisError("AYA framing: member length")
        member = source[pos : pos + length]
        pos += length
        decoder = zlib.decompressobj()
        try:
            remaining = MAX_INFLATED - len(output)
            inflated = decoder.decompress(member, remaining + 1)
        except zlib.error as error:
            raise AnalysisError("AYA framing: zlib member") from error
        if len(inflated) > remaining or decoder.unconsumed_tail:
            raise AnalysisError("AYA framing: inflate limit")
        if not decoder.eof or decoder.unused_data:
            raise AnalysisError("AYA framing: incomplete zlib member")
        inflated += decoder.flush()
        if len(inflated) > remaining:
            raise AnalysisError("AYA framing: inflate limit")
        output.extend(inflated)
        member_count += 1
    if not output:
        raise AnalysisError("AYA framing: empty archive")
    return bytes(output)


def _finite_values(data: bytes, offset: int, count: int, role: str) -> tuple[float, ...]:
    if offset + count * 4 > len(data):
        raise AnalysisError(f"malformed framing: {role}")
    values = struct.unpack_from(f"<{count}f", data, offset)
    if not all(math.isfinite(value) for value in values):
        raise AnalysisError(f"non-finite transform or numeric data: {role}")
    return values


def _parse_pmvb(payload: bytes, *, texture_count: int, reference_source: bool) -> dict[str, object]:
    reader = Reader(payload)
    cmvb = reader.expected(b"CMVB", "CMVB", length=296)
    group_count = cmvb.payload[264]
    stride, fvf, topology = struct.unpack_from("<III", cmvb.payload, 276)
    opaque = bytearray(cmvb.payload)
    opaque[264] = 0
    opaque[276:288] = bytes(12)
    cmvb_opaque_nonzero = sum(value != 0 for value in opaque)
    if group_count > 12:
        raise AnalysisError("unsupported structure: material group count")
    if reference_source and group_count:
        raise AnalysisError("ambiguous reference source")
    if group_count == 0:
        if reference_source and (stride, fvf, topology) not in {(36, 0x152, 4), (0, 0, 0)}:
            raise AnalysisError("unsupported structure: reference source metadata")
        reader.end("empty PMVB")
        return {"groupCount": 0, "stride": stride, "fvf": fvf, "topology": topology, "cmvbOpaqueNonZero": cmvb_opaque_nonzero, "groups": []}
    if (stride, fvf, topology) != (36, 0x152, 4):
        raise AnalysisError("unsupported structure: populated stride/FVF/topology")

    groups: list[dict[str, object]] = []
    owned_vbytes = owned_vcount = None
    for group_index in range(group_count):
        mmpt = reader.expected(b"MMPT", f"MMPT {group_index}", length=24)
        vbytes, ibytes, icount, vcount, primitive_count, active = struct.unpack("<6I", mmpt.payload)
        if active != 1 or icount < 3 or ibytes != icount * 2 or primitive_count != icount - 2:
            raise AnalysisError("malformed framing: MMPT declarations")
        if vcount > MAX_VERTICES or icount > MAX_INDICES:
            raise AnalysisError("unsupported structure: geometry limit")
        if group_index == 0:
            if vcount == 0 or vbytes != vcount * 36:
                raise AnalysisError("malformed framing: owned VBUF declaration")
            owned_vbytes, owned_vcount = vbytes, vcount
        elif (vbytes, vcount) != (owned_vbytes, owned_vcount):
            raise AnalysisError("malformed framing: reused VBUF declaration")
        ibuf = reader.expected(b"IBUF", f"IBUF {group_index}", length=ibytes)
        vbuf = reader.expected(b"VBUF", f"VBUF {group_index}")
        texr = reader.expected(b"TEXR", f"TEXR {group_index}", length=24)
        if group_index == 0:
            if len(vbuf.payload) != vbytes:
                raise AnalysisError("malformed framing: owned VBUF length")
            for vertex in range(vcount):
                _finite_values(vbuf.payload, vertex * 36, 6, "vertex position/normal")
                _finite_values(vbuf.payload, vertex * 36 + 28, 2, "vertex UV")
        elif vbuf.payload:
            raise AnalysisError("malformed framing: reused VBUF payload")
        indices = struct.unpack(f"<{icount}H", ibuf.payload)
        if any(index >= (owned_vcount or 0) for index in indices):
            raise AnalysisError("malformed framing: index out of bounds")
        slots = list(struct.unpack("<6I", texr.payload))
        groups.append({
            "group": group_index,
            "vertexCount": vcount,
            "indexCount": icount,
            "textureReferencesRawU32": slots,
            "texturePositionSemantics": "unresolved",
        })
    reader.end("PMVB")
    return {
        "groupCount": group_count,
        "stride": stride,
        "fvf": fvf,
        "topology": topology,
        "cmvbOpaqueNonZero": cmvb_opaque_nonzero,
        "groups": groups,
    }


_TAG_ORDER = {
    b"CHLD": 0, b"PRNT": 1, b"NMIC": 2, b"BBOX": 3, b"VHFM": 4,
    b"HORI": 5, b"HPOS": 6, b"HFOV": 7, b"PBKT": 8, b"CPOS": 9,
    b"CORI": 10, b"REFR": 11, b"PMVB": 12,
}

_PART_ORDERS = {
    tuple(tag.encode("ascii") for tag in value.split())
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
        "PRNT BBOX VHFM HORI HPOS CPOS CORI REFR PMVB",
        "CHLD PRNT BBOX VHFM HORI HPOS CPOS CORI REFR PMVB",
    )
}

_POST_BODY_SIBLING_ORDERS = {
    (b"BBOX",),
    (b"BBOX", b"CEMT"),
    (b"CAMD", b"BBOX"),
    (b"CAMD", b"BBOX", b"CEMT"),
    (b"BBOX", b"PMS2"),
    (b"CAMD", b"BBOX", b"CEMT", b"PMS2"),
}


def _parse_part(chunk_value: Chunk, ordinal: int, part_count: int, texture_count: int) -> dict[str, object]:
    reader = Reader(chunk_value.payload, origin=chunk_value.offset + 8)
    cmsp = reader.expected(b"CMSP", "CMSP", length=316)
    current = _finite_values(cmsp.payload, 0, 12, "current orientation")
    base = _finite_values(cmsp.payload, 0x30, 12, "base orientation")
    offset_position = _finite_values(cmsp.payload, 0x60, 4, "offset position")
    base_position = _finite_values(cmsp.payload, 0x70, 4, "base position")
    number, part_type, child_count = struct.unpack_from("<III", cmsp.payload, 0x88)
    dverts, pverts, triangles, aframes, vframes, hframes, bones = struct.unpack_from("<7I", cmsp.payload, 0xA8)
    if number != ordinal or not 1 <= part_type <= 6 or dverts or pverts or triangles:
        raise AnalysisError("unsupported structure: CMSP identity/counts")
    if child_count > MAX_PARTS or not 1 <= vframes <= 512 or not 1 <= hframes <= 256 or aframes > 2:
        raise AnalysisError("unsupported structure: CMSP frame/count limits")
    if bones:
        raise AnalysisError("unsupported structure: bones")
    part_name = _fixed_name(cmsp.payload[0xDC:0xFC], "part name", allow_relative=False)
    known = bytearray(cmsp.payload)
    for start, size in ((0, 0x80), (0x88, 12), (0xA8, 28), (0xDC, 32)):
        known[start : start + size] = bytes(size)
    cmsp_opaque_nonzero = sum(value != 0 for value in known)

    children: list[int] = []
    parent: int | None = None
    reference: int | None = None
    pmvb: dict[str, object] | None = None
    seen: set[bytes] = set()
    tags: list[bytes] = []
    prior_rank = -1
    while reader.pos < len(reader.data):
        record = reader.chunk("MESP record")
        if record.tag not in _TAG_ORDER or record.tag in seen or _TAG_ORDER[record.tag] <= prior_rank:
            raise AnalysisError("malformed framing: MESP tag order")
        seen.add(record.tag)
        tags.append(record.tag)
        prior_rank = _TAG_ORDER[record.tag]
        if record.tag == b"CHLD":
            if child_count == 0 or len(record.payload) != child_count * 4:
                raise AnalysisError("malformed framing: CHLD")
            children = list(struct.unpack(f"<{child_count}I", record.payload))
        elif record.tag == b"PRNT":
            if len(record.payload) != 4:
                raise AnalysisError("malformed framing: PRNT")
            parent = struct.unpack("<I", record.payload)[0]
        elif record.tag == b"NMIC":
            if len(record.payload) != 4:
                raise AnalysisError("malformed framing: NMIC")
            _u32(record.payload, 0, "NMIC")
        elif record.tag == b"BBOX":
            nested = Reader(record.payload)
            nested.expected(b"BBOX", "inner BBOX", length=40)
            nested.end("outer BBOX")
        elif record.tag == b"VHFM" and len(record.payload) != vframes:
            raise AnalysisError("malformed framing: VHFM")
        elif record.tag == b"HORI" and len(record.payload) != hframes * 48:
            raise AnalysisError("malformed framing: HORI")
        elif record.tag == b"HPOS" and len(record.payload) != hframes * 16:
            raise AnalysisError("malformed framing: HPOS")
        elif record.tag == b"HFOV" and len(record.payload) != hframes * 4:
            raise AnalysisError("malformed framing: HFOV")
        elif record.tag in {b"PBKT", b"CPOS", b"CORI"} and len(record.payload) > MAX_OPAQUE:
            raise AnalysisError("unsupported structure: opaque record limit")
        elif record.tag == b"REFR":
            if len(record.payload) != 4:
                raise AnalysisError("malformed framing: REFR")
            reference = struct.unpack("<I", record.payload)[0]
        elif record.tag == b"PMVB":
            pmvb = _parse_pmvb(record.payload, texture_count=texture_count, reference_source=reference is not None)
    if tuple(tags) not in _PART_ORDERS:
        raise AnalysisError("malformed framing: complete MESP record order")
    if pmvb is None or (child_count > 0) != (b"CHLD" in seen):
        raise AnalysisError("malformed framing: required part records")
    for index in children + ([parent] if parent is not None else []) + ([reference] if reference is not None else []):
        if index is not None and index >= part_count:
            role = "reference target" if index == reference else "hierarchy index"
            raise AnalysisError(f"{role} out of range")
    return {
        "part": ordinal,
        "name": part_name,
        "partType": part_type,
        "parent": parent,
        "children": children,
        "reference": reference,
        "bones": bones,
        "aFrames": aframes,
        "vFrames": vframes,
        "hFrames": hframes,
        "cmspOpaqueNonZero": cmsp_opaque_nonzero,
        "baseTransform": {"orientation": list(base), "position": list(base_position)},
        "storedCurrentTransform": {"orientation": list(current), "offsetPosition": list(offset_position)},
        "pmvb": pmvb,
    }


def _validate_graph(parts: list[dict[str, object]]) -> tuple[int, list[list[int]]]:
    edges: list[list[int]] = []
    parents_from_children: dict[int, int] = {}
    for part in parts:
        parent_index = int(part["part"])
        for child in part["children"]:
            child = int(child)
            if child in parents_from_children:
                raise AnalysisError("hierarchy ambiguous parent")
            parents_from_children[child] = parent_index
            edges.append([parent_index, child])
    state = [0] * len(parts)

    def visit(index: int) -> None:
        if state[index] == 1:
            raise AnalysisError("hierarchy cycle")
        if state[index] == 2:
            return
        state[index] = 1
        for child in parts[index]["children"]:
            visit(int(child))
        state[index] = 2

    for index in range(len(parts)):
        visit(index)
    roots = [int(part["part"]) for part in parts if part["parent"] is None]
    if len(roots) != 1:
        raise AnalysisError("hierarchy root count")
    for part in parts:
        index = int(part["part"])
        parent = part["parent"]
        if index == roots[0]:
            if index in parents_from_children:
                raise AnalysisError("hierarchy root reciprocity")
        elif parent is None or parents_from_children.get(index) != parent:
            raise AnalysisError("hierarchy reciprocity")
    return roots[0], sorted(edges)


def _resolve_references(parts: list[dict[str, object]]) -> tuple[list[dict[str, int]], list[int]]:
    instances: list[dict[str, int]] = []
    owners: list[int] = []
    for part in parts:
        if int(part["pmvb"]["groupCount"]) > 0:
            owners.append(int(part["part"]))
    for part in parts:
        reference = part["reference"]
        if reference is None:
            continue
        index = int(part["part"])
        target = int(reference)
        if target >= index:
            raise AnalysisError("reference target must be an earlier direct owner")
        target_part = parts[target]
        if target_part["reference"] is not None or int(target_part["pmvb"]["groupCount"]) == 0:
            raise AnalysisError("reference target is missing direct geometry")
        if int(part["pmvb"]["groupCount"]) != 0:
            raise AnalysisError("ambiguous reference source")
        instances.append({"instancePart": index, "ownerPart": target})
    return instances, owners


def analyze_aya_bytes(source: bytes, *, loose_ordinal: str, archive_member_identity: str) -> dict[str, object]:
    if not re.fullmatch(r"\d{4}", loose_ordinal):
        raise AnalysisError("private path or invalid loose ordinal")
    _validate_identity(archive_member_identity, "archive member identity")
    data = _inflate_aya(source)
    if len(data) < 380 or data[:4] != b"CMSH" or _u32(data, 4, "CMSH length") != 372:
        raise AnalysisError("malformed framing: CMSH header")
    texture_count = _u32(data, 12, "texture count")
    part_count = _u32(data, 0x164, "part count")
    if texture_count > MAX_TEXTURES or not 1 <= part_count <= MAX_PARTS:
        raise AnalysisError("unsupported structure: CMSH counts")
    mesh_name = _fixed_name(data[44:344], "internal mesh name", allow_relative=False)
    reader = Reader(data[380:], origin=380)
    reader.expected(b"CMST", "CMST", length=texture_count * 36)
    texture_names: list[str] = []
    for index in range(texture_count):
        msht = reader.expected(b"MSHT", f"MSHT {index}", length=156)
        nested = Reader(msht.payload)
        texb = nested.expected(b"TEXB", f"TEXB {index}", length=148)
        nested.end(f"MSHT {index}")
        texture_names.append(_fixed_name(texb.payload[20:148], "texture name", allow_relative=True))
    chunks = [reader.expected(b"MESP", f"MESP {index}") for index in range(part_count)]
    post_body_siblings: list[bytes] = []
    while reader.pos < len(reader.data):
        sibling = reader.chunk("post-body sibling")
        if len(sibling.payload) > MAX_OPAQUE:
            raise AnalysisError("unsupported structure: post-body sibling limit")
        post_body_siblings.append(sibling.tag)
    if post_body_siblings and tuple(post_body_siblings) not in _POST_BODY_SIBLING_ORDERS:
        raise AnalysisError("malformed framing: post-body sibling order")
    parts = [_parse_part(value, index, part_count, texture_count) for index, value in enumerate(chunks)]
    total_groups = 0
    total_vertices = 0
    total_indices = 0
    for part in parts:
        groups = part["pmvb"]["groups"]
        total_groups += int(part["pmvb"]["groupCount"])
        if groups:
            total_vertices += int(groups[0]["vertexCount"])
        total_indices += sum(int(group["indexCount"]) for group in groups)
        if total_groups > MAX_GROUPS:
            raise AnalysisError("unsupported structure: aggregate group limit")
        if total_vertices > MAX_VERTICES:
            raise AnalysisError("unsupported structure: aggregate vertex limit")
        if total_indices > MAX_INDICES:
            raise AnalysisError("unsupported structure: aggregate index limit")
    root, edges = _validate_graph(parts)
    instances, owners = _resolve_references(parts)
    bone_count = sum(int(part["bones"]) for part in parts)
    hframes = [int(part["hFrames"]) for part in parts]
    vframes = [int(part["vFrames"]) for part in parts]
    aframes = [int(part["aFrames"]) for part in parts]
    articulated = any(value > 1 for value in hframes) or any(value > 1 for value in vframes) or any(value > 0 for value in aframes)
    family = "skeletal" if bone_count else ("non-skeletal-part-hierarchy-articulated" if articulated else "static")
    report_parts = []
    for part in parts:
        report_parts.append({
            "part": part["part"], "name": part["name"], "partType": part["partType"],
            "parent": part["parent"], "children": part["children"], "reference": part["reference"],
            "bones": part["bones"], "aFrames": part["aFrames"], "vFrames": part["vFrames"], "hFrames": part["hFrames"],
            "cmspOpaqueNonZero": part["cmspOpaqueNonZero"], "baseTransform": part["baseTransform"],
            "storedCurrentTransform": part["storedCurrentTransform"], "pmvb": part["pmvb"],
        })
    return {
        "schema": SCHEMA,
        "sourceIdentity": {"looseCorpusOrdinal": loose_ordinal, "archiveMemberIdentity": archive_member_identity},
        "mesh": {
            "internalName": mesh_name,
            "partCount": part_count,
            "boneCount": bone_count,
            "declaredTextureSlots": texture_count,
            "observedTextureNames": texture_names,
            "geometryTotals": {
                "groupCount": total_groups,
                "ownedVertexCount": total_vertices,
                "indexCount": total_indices,
            },
        },
        "hierarchy": {"rootPart": root, "reciprocal": True, "edges": edges},
        "references": {"geometryOwnerParts": owners, "instances": instances},
        "frames": {
            "family": family,
            "aFrameRange": [min(aframes), max(aframes)],
            "vFrameRange": [min(vframes), max(vframes)],
            "hFrameRange": [min(hframes), max(hframes)],
            "runtimeArticulationProven": False,
        },
        "transforms": {
            "classes": ["local-base", "reference-instance", "parent-composed-unproven", "runtime-articulated-unproven"],
            "parentCompositionApplied": False,
            "runtimeTransformApplied": False,
        },
        "postBodySiblingTags": [tag.decode("ascii") for tag in post_body_siblings],
        "parts": report_parts,
        "nonClaims": [
            "no faction or player identity claim",
            "no parent transform composition claim",
            "no runtime articulation claim",
            "no texture decode or material fidelity claim",
            "no TEXR positional meaning or sentinel claim",
            "no inference from geometry-only OBJ equality",
        ],
    }


def render_report(report: dict[str, object]) -> bytes:
    return (json.dumps(report, indent=2, sort_keys=True, ensure_ascii=True, allow_nan=False) + "\n").encode("utf-8")


def compare_reports(left: dict[str, object], right: dict[str, object]) -> dict[str, object]:
    fields: list[str] = []
    differences: dict[str, object] = {}
    for field in ("mesh", "hierarchy", "references", "frames", "transforms", "postBodySiblingTags", "parts"):
        if left[field] != right[field]:
            fields.append(field)
            differences[field] = {"left": left[field], "right": right[field]}
    return {
        "schema": "onslaught.aya-battleengine-identity-comparison.v1",
        "looseCorpusOrdinals": [left["sourceIdentity"]["looseCorpusOrdinal"], right["sourceIdentity"]["looseCorpusOrdinal"]],
        "archiveMemberIdentities": [left["sourceIdentity"]["archiveMemberIdentity"], right["sourceIdentity"]["archiveMemberIdentity"]],
        "differentFields": fields,
        "differences": differences,
        "identityConclusion": "unproven",
    }


def resolve_private_selection(rows: list[dict[str, str]], selection: str) -> tuple[str, str]:
    matches = [row for row in rows if row.get("selection") == selection]
    if len(matches) != 1:
        raise AnalysisError("ambiguous mapping")
    row = matches[0]
    ordinal = row.get("looseOrdinal", "")
    member = row.get("memberIdentity", "")
    if not re.fullmatch(r"\d{4}", ordinal) or not member:
        raise AnalysisError("ambiguous mapping")
    _validate_identity(member, "archive member identity")
    return ordinal, member


def _path_has_reparse(path: Path) -> bool:
    absolute = Path(os.path.abspath(path))
    current = Path(absolute.anchor)
    for component in absolute.parts[1:]:
        current /= component
        if not os.path.lexists(current):
            continue
        metadata = os.lstat(current)
        attributes = getattr(metadata, "st_file_attributes", 0)
        if stat.S_ISLNK(metadata.st_mode) or attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0):
            return True
    return False


def _identity(metadata: os.stat_result) -> tuple[int, int, int, int, int]:
    return (
        metadata.st_dev, metadata.st_ino, metadata.st_size,
        getattr(metadata, "st_mtime_ns", int(metadata.st_mtime * 1_000_000_000)),
        metadata.st_nlink,
    )


def analyze_aya_file(path: Path, *, loose_ordinal: str, archive_member_identity: str) -> dict[str, object]:
    path = Path(os.path.abspath(path))
    if _path_has_reparse(path):
        raise AnalysisError("reparse traversal rejected")
    before_path = os.lstat(path)
    if not stat.S_ISREG(before_path.st_mode):
        raise AnalysisError("held input is not regular")
    with path.open("rb") as stream:
        before = os.fstat(stream.fileno())
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            raise AnalysisError("held input must be regular single-link")
        if _identity(before) != _identity(before_path):
            raise AnalysisError("changed held input before read")
        source = stream.read(MAX_SOURCE + 1)
        middle = os.fstat(stream.fileno())
        stream.seek(0)
        source_recheck = stream.read(MAX_SOURCE + 1)
        after = os.fstat(stream.fileno())
        after_path = os.lstat(path)
    if len(source) > MAX_SOURCE:
        raise AnalysisError("AYA framing: source limit")
    if _identity(before) != _identity(middle) or _identity(middle) != _identity(after) or _identity(after) != _identity(after_path):
        raise AnalysisError("changed held input")
    if hashlib.sha256(source).digest() != hashlib.sha256(source_recheck).digest():
        raise AnalysisError("changed held input bytes")
    return analyze_aya_bytes(source, loose_ordinal=loose_ordinal, archive_member_identity=archive_member_identity)


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--loose-ordinal", required=True)
    parser.add_argument("--member-identity", required=True)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(sys.argv[1:] if argv is None else argv)
    try:
        report = analyze_aya_file(args.input, loose_ordinal=args.loose_ordinal, archive_member_identity=args.member_identity)
    except AnalysisError as error:
        print(f"analysis rejected: {error}", file=sys.stderr)
        return 2
    except OSError:
        print("analysis rejected: held input unavailable or changed", file=sys.stderr)
        return 2
    sys.stdout.buffer.write(render_report(report))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
