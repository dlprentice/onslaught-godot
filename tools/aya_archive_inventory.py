#!/usr/bin/env python3
"""
Inventory Battle Engine Aquila packed resource archives (*.aya).

This tool targets the higher-level chunker archives in `data/resources`,
not the lower-level raw mesh payloads under `data/resources/meshes`.

PC AYA resource archives are chunked-zlib files:

    [u32 compressed_part_size][zlib blob][u32 size][zlib blob]...

After inflation, the payload is a tagged chunk stream:

    [4-byte ASCII tag][u32 chunk_size][chunk_data]...

Console resource files may already be the raw tagged stream. Select that
explicitly with ``--envelope raw-tag-stream`` or use the fail-closed ``auto``
mode for a mixed local shelf.

Examples:
    python tools/aya_archive_inventory.py game/data/resources/852_res_PC.aya
    python tools/aya_archive_inventory.py game/data/resources --glob "*_res_PC.aya"
    python tools/aya_archive_inventory.py game/data/resources/852_res_PC.aya --expect-numeric-schedule
    python tools/aya_archive_inventory.py game/data/resources/852_res_PC.aya --show-chunks 6
    python tools/aya_archive_inventory.py game/data/resources/852_res_PC.aya --dump-tag TEXT --dump-tag MESH --dump-dir .artifacts/aya-dump
    python tools/aya_archive_inventory.py game/data/resources/852_res_PC.aya --dump-index 255 --carve-from-tag CMSH --dump-dir .artifacts/aya-dump
"""

from __future__ import annotations

import argparse
import contextlib
import hashlib
import json
import os
import stat
import struct
import sys
import zlib
from collections import Counter
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable

from safe_generated_output import SecuredOutputRoot


MAX_COMPRESSED_BYTES = 64 * 1024 * 1024
MAX_INFLATED_BYTES = 128 * 1024 * 1024
MAX_MEMBERS = 4096
MAX_MEMBER_COMPRESSED_BYTES = 32 * 1024 * 1024
MAX_TOP_LEVEL_CHUNKS = 100_000
MAX_CHUNK_BYTES = 64 * 1024 * 1024
MAX_EMBEDDED_BODIES = 4096
MAX_BODY_BYTES = 32 * 1024 * 1024
MAX_AGGREGATE_BODY_BYTES = 128 * 1024 * 1024
OBSERVATION_SCHEMA = "onslaught.aya-archive-observation.v1"
OBSERVATION_PROFILE = "bounded-observation-v1"

REJECTION_CATEGORIES = frozenset(
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
    }
)
ARCHIVE_ERROR_CATEGORIES = REJECTION_CATEGORIES | frozenset({"raw_tag_stream"})


class ArchiveObservationError(ValueError):
    """Path-free terminal rejection from the bounded observation profile."""

    def __init__(self, category: str) -> None:
        if category not in ARCHIVE_ERROR_CATEGORIES:
            raise ValueError("unknown archive observation rejection category")
        self.category = category
        super().__init__(category)


COMMON_TAG_SCAN = (
    "AYAD",
    "CMSH",
    "CMST",
    "CMSP",
    "CTEX",
    "DXTX",
    "GDAT",
    "IBUF",
    "MESP",
    "MMPT",
    "MSHT",
    "MESH",
    "PMSH",
    "PMS2",
    "TEXB",
    "TFRM",
    "TXTR",
    "VBUF",
)

# Exact top-level tags observed across the complete installed 301-file PC
# resource-archive shelf. Raw-stream admission is deliberately limited to this
# vocabulary until a reviewed console specimen proves an addition.
TOP_LEVEL_ARCHIVE_TAGS = frozenset(
    {
        "AYAD",
        "DMKR",
        "ERES",
        "GDIE",
        "IMPS",
        "LNDS",
        "LVLR",
        "MESH",
        "PLAT",
        "PMIB",
        "SSHD",
        "SURF",
        "TARG",
        "TEXT",
        "VSDS",
        "WRES",
    }
)

EMBEDDED_TAG_PRIORITY = (
    "CMSH",
    "DXTX",
    "GDAT",
    "PMS2",
)

@dataclass(frozen=True)
class ChunkEntry:
    index: int
    tag: str
    size: int
    offset: int


@dataclass(frozen=True)
class ArchiveSummary:
    path: str
    envelope_kind: str
    compressed_size: int
    compressed_sha256: str
    raw_size: int
    raw_sha256: str
    chunk_count: int
    unique_tag_count: int
    tag_counts: dict[str, int]
    first_tags: list[str]


@dataclass(frozen=True)
class AssetResolver:
    resource_root: str
    texture_index: dict[str, list[str]]
    mesh_index: dict[str, list[str]]


@dataclass(frozen=True)
class ReleasedResourceRoutePlan:
    """Released filename plus platform-specific loader side routes."""

    archive_path: str
    loader_materializes: bool
    xbox_cache_source: str | None
    xbox_open_path: str | None
    ps2_level_apf: str | None
    ps2_master_mpf: str | None


@dataclass(frozen=True)
class Ps2TexturePageResolution:
    """One bounded PS2 PAGE-word resolution result."""

    source_kind: str
    offset: int
    length: int
    data: bytes


@dataclass(frozen=True)
class Ps2ApfMergeInput:
    """One explicitly ordered APF and its sibling AYA."""

    role: str
    apf_bytes: bytes | bytearray | memoryview
    aya_bytes: bytes | bytearray | memoryview


@dataclass(frozen=True)
class Ps2ApfMergeResult:
    """Headerless master bytes, copied patched AYAs, and a compact receipt."""

    master_page_bytes: bytes
    patched_aya_bytes: tuple[bytes, ...]
    block_count: int
    texture_name_count: int
    texture_key_count: int
    master_sha256: str


def resolve_released_resource_route(
    platform: str,
    resource_id: int,
    *,
    playable_demo: bool = False,
    pause_for_showing_controls: bool = False,
    language_index: int = 0,
    xbox_base_path: str = "d:",
) -> ReleasedResourceRoutePlan:
    """Reproduce the released PC/Xbox/PS2 resource routing decision.

    The returned path is the filename-constructor result even when the PC/PS2
    loader returns before invoking that constructor for resource ``-3``.
    Xbox cache/open paths are present only for the normal ``-1/-2/-3`` helper
    route; numeric, Goodie, and localized-loading names remain relative.
    """

    platform_key = platform.casefold()
    target_names = {"pc": "PC", "xbox": "XBOX", "ps2": "PS2"}
    try:
        target_name = target_names[platform_key]
    except KeyError as exc:
        raise ValueError("platform must be PC, Xbox, or PS2") from exc

    localized_loading = (
        resource_id == -3 and playable_demo and pause_for_showing_controls
    )
    if resource_id == -1:
        relative_path = f"data\\Resources\\base_res_{target_name}.aya"
    elif resource_id == -2:
        relative_path = f"data\\Resources\\Frontend_res_{target_name}.aya"
    elif resource_id == -3:
        suffix = f"_{language_index}" if localized_loading else ""
        relative_path = f"data\\Resources\\Loading_res_{target_name}{suffix}.aya"
    elif resource_id >= 0:
        relative_path = f"data\\Resources\\{resource_id:03d}_res_{target_name}.aya"
    else:
        goodie_number = -resource_id - 1000
        relative_path = (
            f"data\\Resources\\goodie_{goodie_number:02d}_res_{target_name}.aya"
        )

    loader_materializes = not (
        resource_id == -3 and platform_key in {"pc", "ps2"}
    )
    archive_path = relative_path
    xbox_cache_source: str | None = None
    xbox_open_path: str | None = None
    normal_xbox_special = (
        platform_key == "xbox"
        and resource_id in {-1, -2, -3}
        and not localized_loading
    )
    if normal_xbox_special:
        if not xbox_base_path:
            raise ValueError("xbox_base_path must not be empty for a normal special route")
        rooted = f"{xbox_base_path}\\{relative_path}"
        xbox_open_path = f"Z{rooted[1:]}"
        xbox_cache_source = f"D{xbox_open_path[1:]}"
        archive_path = xbox_open_path

    ps2_level_apf = None
    if platform_key == "ps2" and resource_id >= 0:
        ps2_level_apf = f"{relative_path[:-4]}.apf"

    ps2_master_mpf = (
        r"data\resources\pagefile.mpf"
        if platform_key == "ps2" and loader_materializes
        else None
    )
    return ReleasedResourceRoutePlan(
        archive_path=archive_path,
        loader_materializes=loader_materializes,
        xbox_cache_source=xbox_cache_source,
        xbox_open_path=xbox_open_path,
        ps2_level_apf=ps2_level_apf,
        ps2_master_mpf=ps2_master_mpf,
    )


def resolve_ps2_texture_page(
    page_word: int,
    width: int,
    height: int,
    *,
    master_page_bytes: bytes | bytearray | memoryview | None,
    per_resource_page_bytes: bytes | bytearray | memoryview | None = None,
) -> Ps2TexturePageResolution:
    """Resolve one released PS2 texture-mip PAGE reference.

    Bit zero selects the master MPF when set and the current per-resource APF
    when clear. The remaining bits are the byte offset; the released read size
    is ``16 * ceil(width * height / 16)``. This local tool boundary rejects
    signed-negative words, unsafe arithmetic, a missing selected source, and
    out-of-bounds reads rather than reproducing the console's soft failures.
    Palette bytes remain owned by the AYA and are not inputs here.
    """

    for name, value in (
        ("page_word", page_word),
        ("width", width),
        ("height", height),
    ):
        if not isinstance(value, int) or isinstance(value, bool):
            raise TypeError(f"{name} must be an integer")

    if not 0 <= page_word <= 0x7FFFFFFF:
        raise ValueError("page_word must be a nonnegative signed 32-bit value")
    if not 0 < width <= 0x7FFFFFFF or not 0 < height <= 0x7FFFFFFF:
        raise ValueError("width and height must be positive signed 32-bit values")

    texel_count = width * height
    if texel_count > 0x7FFFFFFF:
        raise ValueError("width * height exceeds the safe signed 32-bit domain")
    length = ((texel_count + 15) // 16) * 16
    if length > 0x7FFFFFFF:
        raise ValueError("resolved page length exceeds the safe signed 32-bit domain")

    use_master = (page_word & 1) != 0
    source_kind = "master_mpf" if use_master else "per_resource_apf"
    source = master_page_bytes if use_master else per_resource_page_bytes
    if source is None:
        raise ValueError(f"{source_kind} bytes are required by this PAGE word")
    if not isinstance(source, (bytes, bytearray, memoryview)):
        raise TypeError(f"{source_kind} bytes must be bytes-like")

    offset = page_word & ~1
    source_length = len(source)
    if offset > source_length or length > source_length - offset:
        raise ValueError(
            f"{source_kind} interval [{offset}, {offset + length}) exceeds "
            f"the {source_length}-byte source"
        )
    return Ps2TexturePageResolution(
        source_kind=source_kind,
        offset=offset,
        length=length,
        data=bytes(source[offset : offset + length]),
    )


def merge_ps2_apfs(
    ordered_inputs: Iterable[Ps2ApfMergeInput],
    *,
    require_matching_page_words: bool = True,
) -> Ps2ApfMergeResult:
    """Merge the exact measured PS2 APF grammar without mutating inputs.

    Input order is authoritative. Each APF must be an empty top-level PAGE
    followed by TBLK records containing exactly IDNT(144), TEX8, and FXUP(4).
    The demo profile requires every case-folded (texture name, mip) key to be
    unique; an unresolved duplicate therefore fails closed. TEX8 payloads are
    concatenated without padding and copied AYAs receive ``cursor | 1`` at the
    PAGE payload named by FXUP. By default an already-patched AYA must match the
    computed word before the copy is patched.
    """

    if not isinstance(require_matching_page_words, bool):
        raise TypeError("require_matching_page_words must be a boolean")
    inputs = tuple(ordered_inputs)
    if not inputs:
        raise ValueError("at least one ordered APF/AYA input is required")

    master = bytearray()
    patched_ayas: list[bytes] = []
    names: set[str] = set()
    keys: set[tuple[str, int]] = set()
    block_count = 0

    for input_index, item in enumerate(inputs):
        if not isinstance(item, Ps2ApfMergeInput):
            raise TypeError("ordered_inputs must contain Ps2ApfMergeInput values")
        if not isinstance(item.role, str) or not item.role:
            raise ValueError(f"input {input_index} must have a nonempty role")
        if not isinstance(item.apf_bytes, (bytes, bytearray, memoryview)):
            raise TypeError(f"{item.role}: APF bytes must be bytes-like")
        if not isinstance(item.aya_bytes, (bytes, bytearray, memoryview)):
            raise TypeError(f"{item.role}: AYA bytes must be bytes-like")

        apf = bytes(item.apf_bytes)
        aya = bytearray(item.aya_bytes)
        outer = parse_chunk_stream(apf)
        if not outer or outer[0].tag != "PAGE" or outer[0].size != 0:
            raise ValueError(f"{item.role}: APF must begin with PAGE(0)")
        if len(outer) == 1:
            raise ValueError(f"{item.role}: APF contains no TBLK records")

        seen_fixups: set[int] = set()
        for outer_chunk in outer[1:]:
            if outer_chunk.tag != "TBLK":
                raise ValueError(
                    f"{item.role}: unexpected APF top-level tag {outer_chunk.tag}"
                )
            block = _payload_bytes(apf, outer_chunk)
            inner = parse_chunk_stream(block)
            if [chunk.tag for chunk in inner] != ["IDNT", "TEX8", "FXUP"]:
                raise ValueError(
                    f"{item.role}: TBLK must contain exactly IDNT, TEX8, FXUP"
                )
            if inner[0].size != 144 or inner[2].size != 4:
                raise ValueError(f"{item.role}: IDNT/FXUP size mismatch")

            ident = _payload_bytes(block, inner[0])
            name_end = ident[:128].find(b"\0")
            if name_end <= 0:
                raise ValueError(
                    f"{item.role}: IDNT name must be nonempty and NUL-terminated"
                )
            try:
                texture_name = ident[:name_end].decode("ascii").casefold()
            except UnicodeDecodeError as exc:
                raise ValueError(f"{item.role}: IDNT name is not ASCII") from exc
            mip_index = _u32(ident, 128)
            key = (texture_name, mip_index)
            if key in keys:
                raise ValueError(
                    f"{item.role}: duplicate case-folded texture name and mip"
                )

            tex8 = _payload_bytes(block, inner[1])
            if not tex8 or len(tex8) % 16 != 0:
                raise ValueError(
                    f"{item.role}: TEX8 length must be a positive multiple of 16"
                )
            cursor = len(master)
            if cursor > 0x7FFFFFFE or len(tex8) > 0x7FFFFFFF - cursor:
                raise ValueError("merged pagefile exceeds the signed 32-bit domain")

            fxup = _u32(_payload_bytes(block, inner[2]), 0)
            if fxup in seen_fixups:
                raise ValueError(f"{item.role}: duplicate AYA FXUP offset")
            seen_fixups.add(fxup)
            if fxup < 8 or fxup + 4 > len(aya):
                raise ValueError(f"{item.role}: FXUP is outside the sibling AYA")
            if aya[fxup - 8 : fxup - 4] != b"PAGE" or _u32(aya, fxup - 4) != 4:
                raise ValueError(f"{item.role}: FXUP does not name a PAGE(4) payload")

            expected_word = cursor | 1
            if require_matching_page_words and _u32(aya, fxup) != expected_word:
                raise ValueError(
                    f"{item.role}: existing AYA PAGE word does not match merge order"
                )
            struct.pack_into("<I", aya, fxup, expected_word)
            master.extend(tex8)
            names.add(texture_name)
            keys.add(key)
            block_count += 1

        patched_ayas.append(bytes(aya))

    master_bytes = bytes(master)
    return Ps2ApfMergeResult(
        master_page_bytes=master_bytes,
        patched_aya_bytes=tuple(patched_ayas),
        block_count=block_count,
        texture_name_count=len(names),
        texture_key_count=len(keys),
        master_sha256=_sha256_hex(master_bytes),
    )


def _u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0]


def _decode_tag(tag_bytes: bytes) -> str:
    return "".join(chr(b) if 0x20 <= b < 0x7F else "." for b in tag_bytes)


def _ascii_preview(data: bytes, limit: int) -> str:
    return "".join(chr(b) if 0x20 <= b < 0x7F else "." for b in data[:limit])


def _hex_preview(data: bytes, limit: int) -> str:
    return data[:limit].hex()


def _sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _safe_name(value: str) -> str:
    return "".join(ch if ch.isalnum() or ch in ("-", "_", ".") else "_" for ch in value)


def _normalize_asset_ref(value: str) -> str:
    return value.lstrip("?").replace("/", "\\").strip().lower()


def _texture_refs_from_loose_name(file_name: str) -> set[str]:
    lower = file_name.lower()
    base: str | None = None
    if ".tga" in lower:
        base = lower[: lower.index(".tga") + 4]
    elif ".t" in lower:
        base = f"{lower[: lower.index('.t') + 2]}ga"
    if base is None:
        return set()

    candidates = {base}
    if base.startswith("mustbe_"):
        candidates.add(base[len("mustbe_") :])

    refs: set[str] = set()
    for candidate in candidates:
        refs.add(candidate.replace("%", "\\"))
    return refs


def build_asset_resolver(resource_root: Path) -> AssetResolver:
    texture_index: dict[str, list[str]] = {}
    mesh_index: dict[str, list[str]] = {}

    for texture_dir_name in ("dxtntextures", "textures"):
        texture_root = resource_root / texture_dir_name
        if not texture_root.exists():
            continue
        for path in sorted(texture_root.glob("*.aya")):
            refs = _texture_refs_from_loose_name(path.name)
            for ref in refs:
                texture_index.setdefault(ref, []).append(str(path))

    mesh_root = resource_root / "meshes"
    if mesh_root.exists():
        for path in sorted(mesh_root.glob("*.aya")):
            name = path.name.lower()
            if not name.endswith(".aya"):
                continue
            stem = name[:-4]
            aliases = {stem}
            if stem.startswith("m_"):
                aliases.add(stem[2:])
            for alias in aliases:
                mesh_index.setdefault(alias, []).append(str(path))

    return AssetResolver(
        resource_root=str(resource_root),
        texture_index=texture_index,
        mesh_index=mesh_index,
    )


def _all_offsets(data: bytes, marker: bytes) -> list[int]:
    out: list[int] = []
    start = 0

    while True:
        pos = data.find(marker, start)
        if pos < 0:
            return out
        out.append(pos)
        start = pos + 1


def _find_pmsh_wrapper_offsets(payload: bytes) -> list[int]:
    offsets: list[int] = []
    for pos in _all_offsets(payload, b"PMSH"):
        if payload[pos + 8 : pos + 12] == b"PMS2":
            offsets.append(pos)
    return offsets


def _payload_bytes(raw: bytes, chunk: ChunkEntry, *, base_offset: int = 0) -> bytes:
    payload_off = (chunk.offset - base_offset) + 8
    return raw[payload_off : payload_off + chunk.size]


def _inner_tag(payload: bytes) -> str | None:
    if len(payload) < 4 or not all(0x20 <= b < 0x7F for b in payload[:4]):
        return None
    return _decode_tag(payload[:4])


def _extract_c_strings(payload: bytes, *, min_len: int = 4, limit: int = 4) -> list[str]:
    out: list[str] = []
    current = bytearray()

    for b in payload:
        if 0x20 <= b < 0x7F:
            current.append(b)
            continue

        if b == 0 and len(current) >= min_len:
            out.append(current.decode("ascii", errors="replace"))
            if len(out) >= limit:
                return out

        current.clear()

    if len(current) >= min_len and len(out) < limit:
        out.append(current.decode("ascii", errors="replace"))

    return out


def _scan_name_hints(payload: bytes, *, limit: int = 6) -> list[str]:
    hints: list[str] = []
    current = bytearray()

    for b in payload:
        if 0x20 <= b < 0x7F:
            current.append(b)
            continue

        if b == 0 and len(current) >= 4:
            candidate = current.decode("ascii", errors="replace")
            if "\\" in candidate or "/" in candidate or "." in candidate:
                hints.append(candidate)
                if len(hints) >= limit:
                    return hints

        current.clear()

    if len(current) >= 4 and len(hints) < limit:
        candidate = current.decode("ascii", errors="replace")
        if "\\" in candidate or "/" in candidate or "." in candidate:
            hints.append(candidate)

    return hints[:limit]


def _find_tag_offsets(payload: bytes, tags: Iterable[str] = COMMON_TAG_SCAN) -> dict[str, int]:
    offsets: dict[str, int] = {}
    for tag in tags:
        pos = payload.find(tag.encode("ascii"))
        if pos >= 0:
            offsets[tag] = pos
    return offsets


def _scan_asset_refs(payload: bytes) -> tuple[list[str], list[str]]:
    hints = [_normalize_asset_ref(h) for h in _scan_name_hints(payload, limit=64)]
    texture_refs = sorted({h for h in hints if h.endswith(".tga")})
    mesh_refs = sorted({h for h in hints if h.endswith(".msh")})
    return texture_refs, mesh_refs


def _resolve_asset_refs(
    resolver: AssetResolver | None,
    *,
    texture_refs: list[str],
    mesh_refs: list[str],
) -> dict[str, object] | None:
    if resolver is None:
        return None

    texture_matches = [{"ref": ref, "matches": resolver.texture_index.get(ref, [])} for ref in texture_refs]
    mesh_matches = [{"ref": ref, "matches": resolver.mesh_index.get(ref, [])} for ref in mesh_refs]
    return {
        "texture_refs_resolved": sum(1 for item in texture_matches if item["matches"]),
        "mesh_refs_resolved": sum(1 for item in mesh_matches if item["matches"]),
        "texture_matches": texture_matches,
        "mesh_matches": mesh_matches,
    }


def _classify_gdie(texture_refs: list[str], mesh_refs: list[str]) -> str:
    if texture_refs and mesh_refs:
        return "texture_mesh"
    if texture_refs:
        return "texture_only"
    if mesh_refs:
        return "mesh_only"
    return "metadata_only"


def _read_fixed_c_string(data: bytes, off: int, length: int) -> str:
    return data[off : off + length].split(b"\0", 1)[0].decode("ascii", errors="replace")


def _probe_importer_mesh_body(body: bytes, *, profile: str = "pc") -> dict[str, object]:
    probe: dict[str, object] = {
        "plausible": False,
        "profile": profile,
        "cmst_offset": body.find(b"CMST"),
        "mesp_offset": body.find(b"MESP"),
    }

    try:
        if profile not in {"pc", "ps2"}:
            raise ValueError(f"unsupported mesh profile: {profile!r}")
        if len(body) < 380:
            raise ValueError(f"body too short: {len(body)} bytes")
        if body[:4] != b"CMSH":
            raise ValueError(f"body does not start with CMSH (got {body[:4]!r})")

        num_textures = _u32(body, 12)
        mesh_name = _read_fixed_c_string(body, 44, 300)
        num_parts = _u32(body, 356)
        cmst_tag = body[380:384]
        if cmst_tag != b"CMST":
            raise ValueError(f"expected CMST at 0x17C, got {cmst_tag!r}")

        if len(body) < 388:
            raise ValueError("truncated CMST header")
        cmst_size = _u32(body, 384)
        expected_cmst_size = num_textures * 36
        if profile == "ps2" and cmst_size != expected_cmst_size:
            raise ValueError(
                f"CMST payload is {cmst_size} bytes; expected {expected_cmst_size} "
                f"for {num_textures} PS2 binding records"
            )
        cmst_end = 388 + expected_cmst_size
        if cmst_end > len(body):
            raise ValueError(
                f"CMST binding records end at 0x{cmst_end:X}, beyond body length 0x{len(body):X}"
            )

        index = cmst_end
        texture_names: list[str] = []
        texture_bindings: list[dict[str, object]] = []
        for texture_index in range(num_textures):
            if len(body) - index < 8 or body[index : index + 4] != b"MSHT":
                raise ValueError(f"texture {texture_index}: expected MSHT at 0x{index:X}")
            msht_size = _u32(body, index + 4)
            msht_payload = index + 8
            msht_end = msht_payload + msht_size
            if msht_end > len(body):
                raise ValueError(
                    f"texture {texture_index}: MSHT ends at 0x{msht_end:X}, "
                    f"beyond body length 0x{len(body):X}"
                )
            if len(body) - msht_payload < 8 or body[msht_payload : msht_payload + 4] != b"TEXB":
                raise ValueError(f"texture {texture_index}: expected TEXB at 0x{msht_payload:X}")
            texb_size = _u32(body, msht_payload + 4)
            texb_payload = msht_payload + 8
            texb_end = texb_payload + texb_size

            if profile == "pc":
                texture_name_offset = texb_payload + 20
                if texture_name_offset + 128 > len(body):
                    raise ValueError(f"texture {texture_index}: truncated PC TEXB name")
                texture_names.append(_read_fixed_c_string(body, texture_name_offset, 128))
                index = texture_name_offset + 128
                continue

            if msht_size != texb_size + 8 or texb_end != msht_end:
                raise ValueError(
                    f"texture {texture_index}: PS2 MSHT payload must contain exactly one TEXB"
                )
            binding_offset = 388 + texture_index * 36
            sample_count = _u32(body, binding_offset + 8)
            expected_texb_size = 20 * sample_count + 4
            if texb_size != expected_texb_size:
                raise ValueError(
                    f"texture {texture_index}: TEXB payload is {texb_size} bytes; "
                    f"expected 20*{sample_count}+4 = {expected_texb_size}"
                )

            lane_words: list[list[int]] = []
            lane_offset = texb_payload
            for _ in range(5):
                lane_words.append(
                    [_u32(body, lane_offset + sample * 4) for sample in range(sample_count)]
                )
                lane_offset += sample_count * 4
            texture_bindings.append(
                {
                    "sample_count": sample_count,
                    "lane_words_u32": lane_words,
                    "texture_key": _u32(body, lane_offset),
                    "cached_max_lane0_bits": _u32(body, binding_offset + 32),
                }
            )
            index = msht_end

        first_part_tag = body[index : index + 4]
        if num_parts > 0 and first_part_tag != b"MESP":
            raise ValueError(f"expected MESP at 0x{index:X}, got {first_part_tag!r}")

        probe.update(
            {
                "plausible": True,
                "num_textures": num_textures,
                "mesh_name": mesh_name,
                "num_parts": num_parts,
                "first_texture_names": texture_names[:4],
                "first_texture_bindings": texture_bindings[:4],
                "first_part_offset": index,
            }
        )
    except Exception as exc:
        probe["error"] = str(exc)

    return probe


def _extract_embedded_mesh_bodies(
    payload: bytes,
    *,
    preview_bytes: int,
    mesh_profile: str = "pc",
) -> list[tuple[bytes, dict[str, object]]]:
    wrapper_offsets = _find_pmsh_wrapper_offsets(payload)
    cmsh_offsets = _all_offsets(payload, b"CMSH")
    bodies: list[tuple[bytes, dict[str, object]]] = []

    if not cmsh_offsets:
        return bodies

    for body_index, cmsh_offset in enumerate(cmsh_offsets):
        next_wrapper = next((offset for offset in wrapper_offsets if offset > cmsh_offset), len(payload))
        wrapper_start = max((offset for offset in wrapper_offsets if offset < cmsh_offset), default=None)
        body = payload[cmsh_offset:next_wrapper]
        record: dict[str, object] = {
            "body_index": body_index,
            "cmsh_offset": cmsh_offset,
            "end_offset": next_wrapper,
            "length": len(body),
            "sha256": _sha256_hex(body),
            "preview_hex": _hex_preview(body, preview_bytes),
            "preview_ascii": _ascii_preview(body, preview_bytes),
            "wrapper_start_offset": wrapper_start,
            "body_boundary_rule": "CMSH_to_next_sibling_PMSH_or_EOF",
            "importer_probe": _probe_importer_mesh_body(body, profile=mesh_profile),
        }
        bodies.append((body, record))

    return bodies


def _stat_identity(metadata: os.stat_result) -> tuple[int, int, int, int, int]:
    return (
        metadata.st_dev,
        metadata.st_ino,
        metadata.st_size,
        metadata.st_mtime_ns,
        metadata.st_nlink,
    )


def _metadata_is_reparse(metadata: os.stat_result) -> bool:
    attributes = getattr(metadata, "st_file_attributes", 0) or 0
    return stat.S_ISLNK(metadata.st_mode) or bool(
        attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
    )


def _path_has_reparse(path: Path) -> bool:
    path = Path(os.path.abspath(path))
    current = Path(path.anchor)
    for component in path.parts[1:]:
        current /= component
        metadata = os.lstat(current)
        if _metadata_is_reparse(metadata):
            return True
    return False


def read_held_archive(path: Path) -> bytes:
    """Read one regular single-link archive once and revalidate its identity."""

    path = Path(os.path.abspath(path))
    try:
        if _path_has_reparse(path):
            raise ArchiveObservationError("reparse_input")
        before_path = os.lstat(path)
        if not stat.S_ISREG(before_path.st_mode):
            raise ArchiveObservationError("not_regular_input")
        if before_path.st_nlink != 1:
            raise ArchiveObservationError("hardlink_input")

        with path.open("rb") as stream:
            before_handle = os.fstat(stream.fileno())
            if not stat.S_ISREG(before_handle.st_mode):
                raise ArchiveObservationError("not_regular_input")
            if before_handle.st_nlink != 1:
                raise ArchiveObservationError("hardlink_input")
            if _stat_identity(before_path) != _stat_identity(before_handle):
                raise ArchiveObservationError("changed_held_input")
            source = stream.read(MAX_COMPRESSED_BYTES + 1)
            after_handle = os.fstat(stream.fileno())

        if _path_has_reparse(path):
            raise ArchiveObservationError("reparse_input")
        after_path = os.lstat(path)
        if len(source) > MAX_COMPRESSED_BYTES:
            raise ArchiveObservationError("compressed_limit")
        if (
            _stat_identity(before_handle) != _stat_identity(after_handle)
            or _stat_identity(after_handle) != _stat_identity(after_path)
        ):
            raise ArchiveObservationError("changed_held_input")
        return source
    except ArchiveObservationError:
        raise
    except OSError as error:
        raise ArchiveObservationError("unavailable_input") from error


def _inflate_aya_bytes_with_count(source: bytes) -> tuple[bytes, int]:
    if len(source) > MAX_COMPRESSED_BYTES:
        raise ArchiveObservationError("compressed_limit")
    if not source:
        raise ArchiveObservationError("empty_archive")

    out = bytearray()
    index = 0
    member_count = 0

    while index < len(source):
        if member_count >= MAX_MEMBERS:
            raise ArchiveObservationError("member_count_limit")
        if index + 4 > len(source):
            raise ArchiveObservationError("truncated_member_header")

        size = _u32(source, index)
        index += 4
        if size == 0:
            raise ArchiveObservationError("invalid_member_length")
        if size > MAX_MEMBER_COMPRESSED_BYTES:
            raise ArchiveObservationError("member_length_limit")
        end = index + size

        if end > len(source):
            raise ArchiveObservationError("member_overrun")

        decoder = zlib.decompressobj()
        remaining = MAX_INFLATED_BYTES - len(out)
        try:
            inflated = decoder.decompress(source[index:end], remaining + 1)
        except zlib.error as error:
            raise ArchiveObservationError("zlib_member") from error
        if len(inflated) > remaining or decoder.unconsumed_tail:
            raise ArchiveObservationError("inflate_limit")
        if not decoder.eof:
            raise ArchiveObservationError("incomplete_zlib_member")
        if decoder.unused_data:
            raise ArchiveObservationError("trailing_zlib_data")

        out.extend(inflated)
        index = end
        member_count += 1

    return bytes(out), member_count


def inflate_aya_bytes(source: bytes) -> bytes:
    return _inflate_aya_bytes_with_count(source)[0]


def inflate_aya(path: Path) -> bytes:
    """Compatibility wrapper for the legacy path-oriented caller."""

    try:
        return inflate_aya_bytes(read_held_archive(path))
    except ArchiveObservationError as error:
        raise ValueError(f"{path}: {error.category}") from error


def _decode_raw_tag_stream(source: bytes) -> tuple[bytes, list[ChunkEntry]]:
    if not source:
        raise ArchiveObservationError("empty_archive")
    if len(source) > MAX_INFLATED_BYTES:
        raise ArchiveObservationError("inflate_limit")
    try:
        chunks = parse_top_level_chunks_bounded(source)
    except ArchiveObservationError as error:
        raise ArchiveObservationError("raw_tag_stream") from error
    if len(chunks) < 2 or any(chunk.tag not in TOP_LEVEL_ARCHIVE_TAGS for chunk in chunks):
        raise ArchiveObservationError("raw_tag_stream")
    return source, chunks


def decode_archive_envelope(
    source: bytes,
    envelope_kind: str,
) -> tuple[bytes, list[ChunkEntry], int, str]:
    if envelope_kind == "pc-chunked-zlib":
        raw, member_count = _inflate_aya_bytes_with_count(source)
        return raw, parse_top_level_chunks_bounded(raw), member_count, envelope_kind
    if envelope_kind == "raw-tag-stream":
        raw, chunks = _decode_raw_tag_stream(source)
        return raw, chunks, 0, envelope_kind
    if envelope_kind == "auto":
        try:
            raw, member_count = _inflate_aya_bytes_with_count(source)
            return raw, parse_top_level_chunks_bounded(raw), member_count, "pc-chunked-zlib"
        except ArchiveObservationError:
            raw, chunks = _decode_raw_tag_stream(source)
            return raw, chunks, 0, "raw-tag-stream"
    raise ValueError("unsupported AYA envelope kind")


def parse_top_level_chunks_bounded(raw: bytes) -> list[ChunkEntry]:
    chunks: list[ChunkEntry] = []
    index = 0
    chunk_index = 0

    while index < len(raw):
        if chunk_index >= MAX_TOP_LEVEL_CHUNKS:
            raise ArchiveObservationError("chunk_count_limit")
        if index + 8 > len(raw):
            raise ArchiveObservationError("truncated_chunk_header")
        size = _u32(raw, index + 4)
        if size > MAX_CHUNK_BYTES:
            raise ArchiveObservationError("chunk_length_limit")
        end = index + 8 + size
        if end > len(raw):
            raise ArchiveObservationError("chunk_overrun")
        chunks.append(
            ChunkEntry(
                index=chunk_index,
                tag=_decode_tag(raw[index : index + 4]),
                size=size,
                offset=index,
            )
        )
        chunk_index += 1
        index = end

    return chunks


def validate_numeric_resource_schedule(chunks: Iterable[ChunkEntry]) -> None:
    """Validate the canonical PC-host numeric resource-writer topology.

    The same tag runs are measured in all 66 numeric archives from the named
    retail PC and USA Xbox shelves. This validates only outer framing/order;
    it does not identify payload schemas or prove decoder order dependence.
    """
    entries = list(chunks)
    prefix = (
        ("LVLR", 0, 4),
        ("TARG", 12, 4),
        ("AYAD", 24, 24),
    )

    def mismatch(index: int, expected: str) -> ValueError:
        if index < len(entries):
            actual = entries[index]
            return ValueError(
                "does not match the canonical numeric writer profile at "
                f"inflated offset 0x{actual.offset:X}: expected {expected}, "
                f"got {actual.tag} (size {actual.size})"
            )
        eof_offset = (
            entries[-1].offset + 8 + entries[-1].size if entries else 0
        )
        return ValueError(
            "does not match the canonical numeric writer profile at "
            f"inflated offset 0x{eof_offset:X}: expected {expected}, got EOF"
        )

    for index, (tag, offset, size) in enumerate(prefix):
        if index >= len(entries):
            raise mismatch(index, f"{tag} at 0x{offset:X} with size {size}")
        actual = entries[index]
        if actual.tag != tag or actual.offset != offset or actual.size != size:
            raise mismatch(index, f"{tag} at 0x{offset:X} with size {size}")

    index = len(prefix)
    while index < len(entries) and entries[index].tag == "TEXT":
        index += 1
    while index < len(entries) and entries[index].tag == "MESH":
        index += 1

    for tag in ("IMPS", "LNDS", "SURF", "ERES", "SSHD", "WRES"):
        if index >= len(entries) or entries[index].tag != tag:
            raise mismatch(index, tag)
        index += 1

    if index != len(entries):
        raise mismatch(index, "end of stream")


def _next_pmsh_wrapper(payload: bytes, start: int) -> int:
    cursor = start
    while True:
        offset = payload.find(b"PMSH", cursor)
        if offset < 0:
            return len(payload)
        if payload[offset + 8 : offset + 12] == b"PMS2":
            return offset
        cursor = offset + 1


def observe_embedded_bodies(payload: bytes) -> list[dict[str, object]]:
    if len(payload) > MAX_CHUNK_BYTES:
        raise ArchiveObservationError("chunk_length_limit")

    offsets: list[int] = []
    cursor = 0
    while True:
        offset = payload.find(b"CMSH", cursor)
        if offset < 0:
            break
        if len(offsets) >= MAX_EMBEDDED_BODIES:
            raise ArchiveObservationError("body_count_limit")
        offsets.append(offset)
        cursor = offset + 1

    observations: list[dict[str, object]] = []
    aggregate = 0
    for ordinal, offset in enumerate(offsets, 1):
        end = _next_pmsh_wrapper(payload, offset + 4)
        if ordinal < len(offsets) and offsets[ordinal] < end:
            raise ArchiveObservationError("body_framing")
        body = payload[offset:end]
        if len(body) > MAX_BODY_BYTES:
            raise ArchiveObservationError("body_length_limit")
        aggregate += len(body)
        if aggregate > MAX_AGGREGATE_BODY_BYTES:
            raise ArchiveObservationError("aggregate_body_limit")
        observations.append(
            {
                "boundaryRule": "cmsh-to-next-pmsh-pms2-or-mesh-end",
                "candidateOrdinal": f"body-candidate-{ordinal:04d}",
                "evidenceKind": "candidate-only",
                "length": len(body),
                "sha256": _sha256_hex(body),
                "tagAscii": "CMSH",
                "tagHex": "434d5348",
            }
        )
    return observations


def parse_chunk_stream(raw: bytes, *, base_offset: int = 0) -> list[ChunkEntry]:
    chunks: list[ChunkEntry] = []
    index = 0
    chunk_index = 0

    while index < len(raw):
        if index + 8 > len(raw):
            raise ValueError(f"truncated chunk header at raw offset 0x{base_offset + index:X}")

        tag = _decode_tag(raw[index : index + 4])
        size = _u32(raw, index + 4)
        payload_off = index + 8
        end = payload_off + size

        if end > len(raw):
            raise ValueError(
                f"chunk {chunk_index} ({tag}) overruns payload "
                f"(payload_off 0x{base_offset + payload_off:X}, size 0x{size:X}, raw_len 0x{len(raw):X})"
            )

        chunks.append(ChunkEntry(index=chunk_index, tag=tag, size=size, offset=base_offset + index))
        chunk_index += 1
        index = end

    return chunks


def parse_top_level_chunks(raw: bytes) -> list[ChunkEntry]:
    return parse_chunk_stream(raw)


def _nested_chunk_tree(payload: bytes, *, base_offset: int = 0, depth: int = 4, max_children: int = 24) -> list[dict[str, object]]:
    if depth <= 0:
        return []

    try:
        chunks = parse_chunk_stream(payload, base_offset=base_offset)
    except Exception:
        return []

    out: list[dict[str, object]] = []
    for chunk in chunks[:max_children]:
        child_payload = _payload_bytes(payload, chunk, base_offset=base_offset)
        item: dict[str, object] = {
            "index": chunk.index,
            "tag": chunk.tag,
            "size": chunk.size,
            "offset": chunk.offset,
            "payload_offset": chunk.offset + 8,
            "preview_ascii": _ascii_preview(child_payload, 24),
            "name_hints": _scan_name_hints(child_payload, limit=4),
        }
        if depth > 1:
            children = _nested_chunk_tree(
                child_payload,
                base_offset=chunk.offset + 8,
                depth=depth - 1,
                max_children=max_children,
            )
            if children:
                item["children"] = children
        out.append(item)

    return out


def _embedded_chunk_tree(payload: bytes, *, base_offset: int = 0, depth: int = 4, max_children: int = 24) -> dict[str, object] | None:
    tag_offsets = _find_tag_offsets(payload)
    ranked_offsets = sorted(
        ((tag, pos) for tag, pos in tag_offsets.items() if pos > 0),
        key=lambda item: (
            EMBEDDED_TAG_PRIORITY.index(item[0]) if item[0] in EMBEDDED_TAG_PRIORITY else len(EMBEDDED_TAG_PRIORITY),
            item[1],
            item[0],
        ),
    )

    for tag, pos in ranked_offsets:
        candidate = payload[pos:]
        try:
            chunks = parse_chunk_stream(candidate, base_offset=base_offset + pos)
        except Exception:
            continue

        if not chunks:
            continue

        return {
            "start_tag": tag,
            "stream_offset": base_offset + pos,
            "chunks": _nested_chunk_tree(
                candidate,
                base_offset=base_offset + pos,
                depth=depth,
                max_children=max_children,
            ),
        }

    return None


def _chunk_record(
    raw: bytes,
    chunk: ChunkEntry,
    *,
    preview_bytes: int,
    include_nested: bool = False,
    resolver: AssetResolver | None = None,
) -> dict[str, object]:
    payload = _payload_bytes(raw, chunk)
    texture_refs, mesh_refs = _scan_asset_refs(payload)
    record: dict[str, object] = {
        "index": chunk.index,
        "tag": chunk.tag,
        "size": chunk.size,
        "offset": chunk.offset,
        "payload_offset": chunk.offset + 8,
        "end_offset": chunk.offset + 8 + chunk.size,
        "sha256": _sha256_hex(payload),
        "inner_tag": _inner_tag(payload),
        "preview_hex": _hex_preview(payload, preview_bytes),
        "preview_ascii": _ascii_preview(payload, preview_bytes),
        "c_strings": _extract_c_strings(payload),
        "name_hints": _scan_name_hints(payload),
        "tag_offsets": _find_tag_offsets(payload),
        "texture_refs": texture_refs,
        "mesh_refs": mesh_refs,
    }
    resolution = _resolve_asset_refs(resolver, texture_refs=texture_refs, mesh_refs=mesh_refs)
    if resolution is not None and (texture_refs or mesh_refs):
        record["resolution"] = resolution
    if chunk.tag == "GDIE":
        record["gdie_family"] = _classify_gdie(texture_refs, mesh_refs)
    if include_nested:
        nested = _nested_chunk_tree(payload, base_offset=chunk.offset + 8)
        if nested:
            record["nested_chunks"] = nested
        embedded = _embedded_chunk_tree(payload, base_offset=chunk.offset + 8)
        if embedded:
            record["embedded_chunks"] = embedded
    return record


def _summarize_archive_bytes(
    path: Path,
    compressed: bytes,
    *,
    envelope_kind: str = "pc-chunked-zlib",
) -> tuple[ArchiveSummary, bytes, list[ChunkEntry], int]:
    raw, chunks, member_count, detected_envelope = decode_archive_envelope(
        compressed, envelope_kind
    )
    counts = Counter(chunk.tag for chunk in chunks)

    summary = ArchiveSummary(
        path=str(path),
        envelope_kind=detected_envelope,
        compressed_size=len(compressed),
        compressed_sha256=_sha256_hex(compressed),
        raw_size=len(raw),
        raw_sha256=_sha256_hex(raw),
        chunk_count=len(chunks),
        unique_tag_count=len(counts),
        tag_counts=dict(sorted(counts.items())),
        first_tags=[chunk.tag for chunk in chunks[:12]],
    )
    return summary, raw, chunks, member_count


def summarize_archive(
    path: Path,
    envelope_kind: str = "pc-chunked-zlib",
) -> tuple[ArchiveSummary, bytes, list[ChunkEntry]]:
    compressed = read_held_archive(path)
    summary, raw, chunks, _ = _summarize_archive_bytes(
        path, compressed, envelope_kind=envelope_kind
    )
    return summary, raw, chunks


def _normalized_extension(path: Path) -> str:
    return ".aya" if path.suffix.casefold() == ".aya" else "other"


def _chunk_observations(raw: bytes, chunks: list[ChunkEntry]) -> list[dict[str, object]]:
    observations: list[dict[str, object]] = []
    for ordinal, chunk in enumerate(chunks, 1):
        tag_bytes = raw[chunk.offset : chunk.offset + 4]
        payload = _payload_bytes(raw, chunk)
        observations.append(
            {
                "bodyCandidateObservations": (
                    observe_embedded_bodies(payload) if tag_bytes == b"MESH" else []
                ),
                "chunkOrdinal": f"chunk-{ordinal:04d}",
                "declaredLength": chunk.size,
                "payloadSha256": _sha256_hex(payload),
                "tagAscii": _decode_tag(tag_bytes),
                "tagHex": tag_bytes.hex(),
            }
        )
    return observations


def _observed_archive_record(
    path: Path,
    ordinal: int,
    compressed: bytes,
    raw: bytes,
    chunks: list[ChunkEntry],
    member_count: int,
) -> dict[str, object]:
    return {
        "archiveOrdinal": f"archive-{ordinal:04d}",
        "chunkObservations": _chunk_observations(raw, chunks),
        "extension": _normalized_extension(path),
        "inflatedLength": len(raw),
        "memberCount": member_count,
        "observationStatus": "observed",
        "rejectionCategory": None,
        "sourceIdentity": {
            "length": len(compressed),
            "sha256": _sha256_hex(compressed),
        },
    }


def _rejected_archive_record(
    path: Path,
    ordinal: int,
    category: str,
    compressed: bytes | None,
) -> dict[str, object]:
    if category not in REJECTION_CATEGORIES:
        raise ValueError("unknown archive observation rejection category")
    return {
        "archiveOrdinal": f"archive-{ordinal:04d}",
        "chunkObservations": [],
        "extension": _normalized_extension(path),
        "inflatedLength": None,
        "memberCount": None,
        "observationStatus": "rejected",
        "rejectionCategory": category,
        "sourceIdentity": (
            {"length": len(compressed), "sha256": _sha256_hex(compressed)}
            if compressed is not None
            else None
        ),
    }


def _source_universe_id(records: list[dict[str, object]]) -> str:
    universe_members = [
        {
            "archiveOrdinal": record["archiveOrdinal"],
            "extension": record["extension"],
            "observationStatus": record["observationStatus"],
            "rejectionCategory": record["rejectionCategory"],
            "sourceIdentity": record["sourceIdentity"],
        }
        for record in records
    ]
    universe_bytes = json.dumps(
        universe_members,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("ascii")
    return hashlib.sha256(universe_bytes).hexdigest()


def _canonical_archive_record_identity(record: dict[str, object]) -> bytes:
    public_identity = {key: value for key, value in record.items() if key != "archiveOrdinal"}
    return json.dumps(
        public_identity,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("ascii")


def _canonicalize_archive_records(
    records: list[dict[str, object]],
) -> list[dict[str, object]]:
    ordered = sorted(
        (dict(record) for record in records),
        key=_canonical_archive_record_identity,
    )
    for ordinal, record in enumerate(ordered, 1):
        record["archiveOrdinal"] = f"archive-{ordinal:04d}"
    return ordered


def _observation_report(records: list[dict[str, object]]) -> dict[str, object]:
    ordered = _canonicalize_archive_records(records)
    return {
        "archiveRecords": ordered,
        "producer": {
            "name": "aya_archive_inventory",
            "producerVersion": 1,
            "profileVersion": OBSERVATION_PROFILE,
        },
        "schemaVersion": OBSERVATION_SCHEMA,
        "sourceUniverseId": _source_universe_id(ordered),
    }


def observe_archives(paths: Iterable[Path]) -> dict[str, object]:
    records: list[dict[str, object]] = []
    for path in (Path(path) for path in paths):
        compressed: bytes | None = None
        try:
            compressed = read_held_archive(path)
            _, raw, chunks, member_count = _summarize_archive_bytes(path, compressed)
            record = _observed_archive_record(path, 0, compressed, raw, chunks, member_count)
        except ArchiveObservationError as error:
            record = _rejected_archive_record(path, 0, error.category, compressed)
        except Exception:
            record = _rejected_archive_record(path, 0, "internal_error", compressed)
        records.append(record)
    return _observation_report(records)


def _valid_digest(value: object) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(
        character in "0123456789abcdef" for character in value
    )


def _plain_int(value: object) -> bool:
    return isinstance(value, int) and not isinstance(value, bool)


def _validate_source_identity(value: object) -> None:
    if value is None:
        return
    if not isinstance(value, dict) or set(value) != {"length", "sha256"}:
        raise ValueError("invalid observation source identity")
    if (
        not _plain_int(value["length"])
        or not 0 <= value["length"] <= MAX_COMPRESSED_BYTES
        or not _valid_digest(value["sha256"])
    ):
        raise ValueError("invalid observation source identity")


def _validate_body_candidate_observations(value: object) -> int:
    if not isinstance(value, list):
        raise ValueError("invalid body candidate observations")
    if len(value) > MAX_EMBEDDED_BODIES:
        raise ValueError("body candidate observation count exceeds profile")
    aggregate = 0
    for ordinal, body in enumerate(value, 1):
        if not isinstance(body, dict) or set(body) != {
            "boundaryRule",
            "candidateOrdinal",
            "evidenceKind",
            "length",
            "sha256",
            "tagAscii",
            "tagHex",
        }:
            raise ValueError("invalid body candidate observation")
        if body["candidateOrdinal"] != f"body-candidate-{ordinal:04d}":
            raise ValueError("invalid body candidate ordinal")
        if body["evidenceKind"] != "candidate-only":
            raise ValueError("invalid body candidate evidence kind")
        if body["boundaryRule"] != "cmsh-to-next-pmsh-pms2-or-mesh-end":
            raise ValueError("invalid body candidate boundary rule")
        if body["tagAscii"] != "CMSH" or body["tagHex"] != "434d5348":
            raise ValueError("invalid body candidate tag")
        if not _plain_int(body["length"]) or not 4 <= body["length"] <= MAX_BODY_BYTES:
            raise ValueError("invalid body candidate length")
        aggregate += body["length"]
        if aggregate > MAX_AGGREGATE_BODY_BYTES:
            raise ValueError("body candidate aggregate exceeds profile")
        if not _valid_digest(body["sha256"]):
            raise ValueError("invalid body candidate digest")
    return aggregate


def _validate_chunk_observations(value: object) -> int:
    if not isinstance(value, list):
        raise ValueError("invalid chunk observations")
    if len(value) > MAX_TOP_LEVEL_CHUNKS:
        raise ValueError("chunk observation count exceeds profile")
    inflated_length = 0
    for ordinal, chunk in enumerate(value, 1):
        if not isinstance(chunk, dict) or set(chunk) != {
            "bodyCandidateObservations",
            "chunkOrdinal",
            "declaredLength",
            "payloadSha256",
            "tagAscii",
            "tagHex",
        }:
            raise ValueError("invalid chunk observation")
        if chunk["chunkOrdinal"] != f"chunk-{ordinal:04d}":
            raise ValueError("invalid chunk ordinal")
        if not _plain_int(chunk["declaredLength"]) or not 0 <= chunk["declaredLength"] <= MAX_CHUNK_BYTES:
            raise ValueError("invalid chunk length")
        if not _valid_digest(chunk["payloadSha256"]):
            raise ValueError("invalid chunk digest")
        if not isinstance(chunk["tagAscii"], str) or len(chunk["tagAscii"]) != 4:
            raise ValueError("invalid chunk tag")
        if not isinstance(chunk["tagHex"], str) or len(chunk["tagHex"]) != 8 or any(
            character not in "0123456789abcdef" for character in chunk["tagHex"]
        ):
            raise ValueError("invalid chunk tag")
        tag_bytes = bytes.fromhex(chunk["tagHex"])
        if _decode_tag(tag_bytes) != chunk["tagAscii"]:
            raise ValueError("chunk tag encodings disagree")
        body_aggregate = _validate_body_candidate_observations(
            chunk["bodyCandidateObservations"]
        )
        if chunk["bodyCandidateObservations"] and tag_bytes != b"MESH":
            raise ValueError("body candidate observations outside MESH")
        if body_aggregate > chunk["declaredLength"]:
            raise ValueError("body candidate observations exceed MESH payload")
        inflated_length += 8 + chunk["declaredLength"]
        if inflated_length > MAX_INFLATED_BYTES:
            raise ValueError("chunk observations exceed inflate profile")
    return inflated_length


def render_observation_records(report: dict[str, object]) -> bytes:
    if set(report) != {"archiveRecords", "producer", "schemaVersion", "sourceUniverseId"}:
        raise ValueError("invalid observation envelope")
    if report.get("schemaVersion") != OBSERVATION_SCHEMA:
        raise ValueError("unexpected observation schema")
    if report.get("producer") != {
        "name": "aya_archive_inventory",
        "producerVersion": 1,
        "profileVersion": OBSERVATION_PROFILE,
    }:
        raise ValueError("unexpected observation producer")
    records = report.get("archiveRecords")
    if not isinstance(records, list):
        raise ValueError("invalid observation records")
    for ordinal, record in enumerate(records, 1):
        if not isinstance(record, dict) or set(record) != {
            "archiveOrdinal",
            "chunkObservations",
            "extension",
            "inflatedLength",
            "memberCount",
            "observationStatus",
            "rejectionCategory",
            "sourceIdentity",
        }:
            raise ValueError("invalid observation record")
        if record["archiveOrdinal"] != f"archive-{ordinal:04d}":
            raise ValueError("invalid archive ordinal")
        extension = record["extension"]
        if extension not in {".aya", "other"}:
            raise ValueError("invalid observation extension")
        _validate_source_identity(record["sourceIdentity"])
        status = record.get("observationStatus")
        category = record.get("rejectionCategory")
        if status == "observed":
            if category is not None or record["sourceIdentity"] is None:
                raise ValueError("invalid observed record")
            if (
                not _plain_int(record["inflatedLength"])
                or not 0 <= record["inflatedLength"] <= MAX_INFLATED_BYTES
            ):
                raise ValueError("invalid observed record")
            if not _plain_int(record["memberCount"]) or not 1 <= record["memberCount"] <= MAX_MEMBERS:
                raise ValueError("invalid observed record")
            if record["sourceIdentity"]["length"] == 0:
                raise ValueError("invalid observed record")
            if _validate_chunk_observations(record["chunkObservations"]) != record["inflatedLength"]:
                raise ValueError("inflated length does not match chunks")
        elif status == "rejected":
            if category not in REJECTION_CATEGORIES:
                raise ValueError("rejected record has unknown category")
            if record["inflatedLength"] is not None or record["memberCount"] is not None or record["chunkObservations"]:
                raise ValueError("invalid rejected record")
        else:
            raise ValueError("unknown observation status")
    if records != _canonicalize_archive_records(records):
        raise ValueError("observation records are not canonical")
    if report.get("sourceUniverseId") != _source_universe_id(records):
        raise ValueError("observation universe mismatch")
    return (
        json.dumps(report, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n"
    ).encode("ascii")


def iter_archive_paths(inputs: Iterable[Path], glob_pattern: str) -> list[Path]:
    out: list[Path] = []
    for item in inputs:
        if item.is_file():
            out.append(item)
            continue
        if item.is_dir():
            out.extend(sorted(p for p in item.glob(glob_pattern) if p.is_file()))
            continue
        raise FileNotFoundError(str(item))
    return sorted(dict.fromkeys(out))


def print_summary(summary: ArchiveSummary) -> None:
    counts = ", ".join(f"{tag}:{count}" for tag, count in sorted(summary.tag_counts.items()))
    first_tags = ", ".join(summary.first_tags)
    print(f"{summary.path}")
    print(f"  envelope_kind : {summary.envelope_kind}")
    print(f"  compressed_size: {summary.compressed_size}")
    print(f"  raw_size       : {summary.raw_size}")
    print(f"  chunk_count    : {summary.chunk_count}")
    print(f"  unique_tags    : {summary.unique_tag_count}")
    print(f"  first_tags     : {first_tags}")
    print(f"  tag_counts     : {counts}")


def print_chunk_table(raw: bytes, chunks: list[ChunkEntry], limit: int, *, preview_bytes: int) -> None:
    print("  chunks:")
    for chunk in chunks[:limit]:
        record = _chunk_record(raw, chunk, preview_bytes=preview_bytes)
        inner = f" inner={record['inner_tag']}" if record["inner_tag"] else ""
        preview = record["preview_ascii"]
        print(
            f"    [{chunk.index:03d}] tag={chunk.tag} size=0x{chunk.size:X} ({chunk.size}) "
            f"raw_off=0x{chunk.offset:X}{inner} preview={preview}"
        )


def _select_chunks(
    chunks: list[ChunkEntry],
    *,
    dump_tags: set[str],
    dump_indices: set[int],
    dump_limit_per_tag: int,
) -> list[ChunkEntry]:
    selected: list[ChunkEntry] = []
    selected_indices: set[int] = set()
    per_tag_counts: Counter[str] = Counter()

    for chunk in chunks:
        by_index = chunk.index in dump_indices
        by_tag = chunk.tag in dump_tags and (dump_limit_per_tag <= 0 or per_tag_counts[chunk.tag] < dump_limit_per_tag)

        if not by_index and not by_tag:
            continue

        if chunk.index in selected_indices:
            continue

        selected.append(chunk)
        selected_indices.add(chunk.index)

        if chunk.tag in dump_tags:
            per_tag_counts[chunk.tag] += 1

    return selected


def dump_selected_chunks(
    archive_path: Path,
    raw: bytes,
    chunks: list[ChunkEntry],
    *,
    output: SecuredOutputRoot,
    dump_dir: Path,
    dump_tags: set[str],
    dump_indices: set[int],
    dump_limit_per_tag: int,
    dump_with_header: bool,
    carve_tags: set[str],
    extract_embedded_mesh_bodies: bool,
    embedded_mesh_profile: str,
    resolver: AssetResolver | None,
    preview_bytes: int,
) -> list[dict[str, object]]:
    selected = _select_chunks(
        chunks,
        dump_tags=dump_tags,
        dump_indices=dump_indices,
        dump_limit_per_tag=dump_limit_per_tag,
    )

    dumped: list[dict[str, object]] = []
    archive_dir = dump_dir / _safe_name(archive_path.stem)
    output.ensure_directory(archive_dir)

    for chunk in selected:
        payload = _payload_bytes(raw, chunk)
        if dump_with_header:
            blob = raw[chunk.offset : chunk.offset + 8 + chunk.size]
            suffix = "with_header"
        else:
            blob = payload
            suffix = "payload"

        file_stem = f"{chunk.index:03d}_{_safe_name(chunk.tag)}_{suffix}"
        blob_path = archive_dir / f"{file_stem}.bin"
        meta_path = archive_dir / f"{file_stem}.json"
        output.atomic_write_bytes(blob_path, blob)

        record = _chunk_record(
            raw,
            chunk,
            preview_bytes=preview_bytes,
            include_nested=True,
            resolver=resolver,
        )
        record["dump_path"] = str(blob_path)
        record["metadata_path"] = str(meta_path)
        record["bytes_written"] = len(blob)
        record["includes_header"] = dump_with_header

        carved_streams: list[dict[str, object]] = []
        for carve_tag in sorted(carve_tags):
            marker = carve_tag.encode("ascii")
            carve_off = payload.find(marker)
            if carve_off < 0:
                continue

            carve_blob = payload[carve_off:]
            carve_stem = f"{file_stem}_carve_{_safe_name(carve_tag)}"
            carve_path = archive_dir / f"{carve_stem}.bin"
            output.atomic_write_bytes(carve_path, carve_blob)
            carved_streams.append(
                {
                    "tag": carve_tag,
                    "offset": carve_off,
                    "length": len(carve_blob),
                    "sha256": _sha256_hex(carve_blob),
                    "dump_path": str(carve_path),
                    "preview_hex": _hex_preview(carve_blob, preview_bytes),
                    "preview_ascii": _ascii_preview(carve_blob, preview_bytes),
                }
            )

        if carved_streams:
            record["carved_streams"] = carved_streams

        if extract_embedded_mesh_bodies and chunk.tag == "MESH":
            embedded_bodies = _extract_embedded_mesh_bodies(
                payload,
                preview_bytes=preview_bytes,
                mesh_profile=embedded_mesh_profile,
            )
            if embedded_bodies:
                body_records: list[dict[str, object]] = []
                for body, body_record in embedded_bodies:
                    body_stem = f"{file_stem}_embedded_body{body_record['body_index']:02d}_CMSH"
                    body_path = archive_dir / f"{body_stem}.bin"
                    output.atomic_write_bytes(body_path, body)
                    body_record["dump_path"] = str(body_path)
                    body_records.append(body_record)

                record["embedded_mesh_bodies"] = body_records

        output.atomic_write_json(meta_path, record)
        dumped.append(record)

    return dumped


def parse_args(argv: list[str]) -> argparse.Namespace:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "inputs",
        nargs="*",
        type=Path,
        default=[Path("game/data/resources")],
        help="AYA files and/or directories to scan (default: game/data/resources)",
    )
    ap.add_argument(
        "--glob",
        default="*_res_PC.aya",
        help="Glob when an input is a directory (default: *_res_PC.aya)",
    )
    ap.add_argument(
        "--envelope",
        choices=("pc-chunked-zlib", "raw-tag-stream", "auto"),
        default="pc-chunked-zlib",
        help="Input envelope (default: pc-chunked-zlib; auto admits only a strict known raw tag stream fallback)",
    )
    ap.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Max number of archives to process after collection (0 = no limit)",
    )
    ap.add_argument(
        "--show-chunks",
        type=int,
        default=0,
        help="Show the first N top-level chunks for each archive",
    )
    ap.add_argument(
        "--expect-numeric-schedule",
        action="store_true",
        help="Fail unless every input matches the measured canonical numeric writer profile",
    )
    ap.add_argument(
        "--preview-bytes",
        type=int,
        default=24,
        help="Byte count for ASCII/hex previews in detailed chunk output and metadata (default: 24)",
    )
    ap.add_argument(
        "--resource-root",
        type=Path,
        default=Path("game/data/resources"),
        help="Resource root used for loose mesh/texture resolution (default: game/data/resources)",
    )
    ap.add_argument(
        "--resolve-assets",
        action="store_true",
        help="Resolve packed texture/mesh references against loose payload files under --resource-root",
    )
    ap.add_argument(
        "--json-include-chunks",
        action="store_true",
        help="Include full top-level chunk metadata in JSON output",
    )
    ap.add_argument(
        "--dump-dir",
        type=Path,
        help="Optional root directory where selected chunk payloads will be dumped",
    )
    ap.add_argument(
        "--dump-tag",
        action="append",
        default=[],
        help="Top-level tag to dump (repeatable, e.g. --dump-tag TEXT --dump-tag MESH)",
    )
    ap.add_argument(
        "--dump-index",
        type=int,
        action="append",
        default=[],
        help="Specific top-level chunk index to dump (repeatable)",
    )
    ap.add_argument(
        "--dump-limit-per-tag",
        type=int,
        default=0,
        help="Max chunks to dump per requested tag per archive (0 = no limit)",
    )
    ap.add_argument(
        "--dump-with-header",
        action="store_true",
        help="Dump selected chunks with the outer 8-byte tag/size header instead of payload-only bytes",
    )
    ap.add_argument(
        "--carve-from-tag",
        action="append",
        default=[],
        help="Within each dumped payload, also dump bytes from the first occurrence of this inner tag to EOF (repeatable, e.g. --carve-from-tag CMSH)",
    )
    ap.add_argument(
        "--extract-embedded-mesh-bodies",
        action="store_true",
        help="For selected top-level MESH payloads, emit bounded embedded CMSH bodies using the current composite-wrapper rule (CMSH -> next sibling PMSH or EOF)",
    )
    ap.add_argument(
        "--embedded-mesh-profile",
        choices=("pc", "ps2"),
        default="pc",
        help="Interpret embedded CMSH material bindings as PC fixed-name TEXB (default) or PS2 five-lane/numeric-key TEXB",
    )
    ap.add_argument(
        "--json-out",
        type=Path,
        help="Optional JSON output path for machine-readable summaries",
    )
    ap.add_argument(
        "--asset-manifest-out",
        type=Path,
        help="Optional JSON output path for an aggregate packed-ref resolution manifest and GDIE family table",
    )
    ap.add_argument(
        "--observation-records-out",
        type=Path,
        help="Optional path-free bounded archive observation record output",
    )
    return ap.parse_args(argv)


def _append_ref_counts(counter: Counter[str], refs: list[str]) -> None:
    for ref in refs:
        counter[ref] += 1


def build_asset_manifest(
    archives: list[tuple[Path, bytes, list[ChunkEntry]]],
    *,
    resolver: AssetResolver | None,
) -> dict[str, object]:
    text_texture_refs: Counter[str] = Counter()
    reference_mesh_refs: Counter[str] = Counter()
    gdie_texture_refs: Counter[str] = Counter()
    gdie_mesh_refs: Counter[str] = Counter()
    gdie_families: list[dict[str, object]] = []

    for archive_path, raw, chunks in archives:
        for chunk in chunks:
            payload = _payload_bytes(raw, chunk)
            texture_refs, mesh_refs = _scan_asset_refs(payload)
            if chunk.tag == "TEXT":
                _append_ref_counts(text_texture_refs, texture_refs)
            elif chunk.tag == "MESH" and payload.find(b"CMSH") < 0:
                _append_ref_counts(reference_mesh_refs, mesh_refs)
            elif chunk.tag == "GDIE":
                _append_ref_counts(gdie_texture_refs, texture_refs)
                _append_ref_counts(gdie_mesh_refs, mesh_refs)
                family_record = {
                    "archive": str(archive_path),
                    "chunk_index": chunk.index,
                    "family": _classify_gdie(texture_refs, mesh_refs),
                    "texture_refs": texture_refs,
                    "mesh_refs": mesh_refs,
                }
                resolution = _resolve_asset_refs(resolver, texture_refs=texture_refs, mesh_refs=mesh_refs)
                if resolution is not None and (texture_refs or mesh_refs):
                    family_record["resolution"] = resolution
                gdie_families.append(family_record)

    def _emit(counter: Counter[str], index: dict[str, list[str]] | None) -> list[dict[str, object]]:
        out: list[dict[str, object]] = []
        for ref, count in sorted(counter.items()):
            matches = index.get(ref, []) if index is not None else []
            out.append(
                {
                    "ref": ref,
                    "count": count,
                    "match_count": len(matches),
                    "matches": matches,
                }
            )
        return out

    texture_index = resolver.texture_index if resolver is not None else None
    mesh_index = resolver.mesh_index if resolver is not None else None

    text_records = _emit(text_texture_refs, texture_index)
    ref_mesh_records = _emit(reference_mesh_refs, mesh_index)
    gdie_texture_records = _emit(gdie_texture_refs, texture_index)
    gdie_mesh_records = _emit(gdie_mesh_refs, mesh_index)

    return {
        "resource_root": resolver.resource_root if resolver is not None else None,
        "summary": {
            "text_texture_refs": len(text_records),
            "text_texture_refs_resolved": sum(1 for item in text_records if item["match_count"] > 0),
            "reference_mesh_refs": len(ref_mesh_records),
            "reference_mesh_refs_resolved": sum(1 for item in ref_mesh_records if item["match_count"] > 0),
            "gdie_texture_refs": len(gdie_texture_records),
            "gdie_texture_refs_resolved": sum(1 for item in gdie_texture_records if item["match_count"] > 0),
            "gdie_mesh_refs": len(gdie_mesh_records),
            "gdie_mesh_refs_resolved": sum(1 for item in gdie_mesh_records if item["match_count"] > 0),
            "gdie_family_counts": dict(sorted(Counter(item["family"] for item in gdie_families).items())),
        },
        "text_texture_refs": text_records,
        "reference_mesh_refs": ref_mesh_records,
        "gdie_texture_refs": gdie_texture_records,
        "gdie_mesh_refs": gdie_mesh_records,
        "gdie_families": gdie_families,
    }


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    dump_tags = {tag.upper() for tag in args.dump_tag}
    dump_indices = set(args.dump_index)
    carve_tags = {tag.upper() for tag in args.carve_from_tag}
    resolver = build_asset_resolver(args.resource_root) if args.resolve_assets else None

    try:
        paths = iter_archive_paths(args.inputs, args.glob)
    except Exception as exc:
        print(f"[ERR] {exc}", file=sys.stderr)
        return 1

    if args.dump_dir is not None and not dump_tags and not dump_indices:
        print("[ERR] --dump-dir requires at least one --dump-tag or --dump-index selector", file=sys.stderr)
        return 1

    if args.observation_records_out is not None and args.envelope != "pc-chunked-zlib":
        print(
            "[ERR] path-free observation schema v1 is limited to pc-chunked-zlib inputs",
            file=sys.stderr,
        )
        return 1

    if not paths:
        print("[ERR] no .aya archives matched the requested inputs", file=sys.stderr)
        return 1

    if args.limit > 0:
        paths = paths[: args.limit]

    paths = [Path(os.path.abspath(path)) for path in paths]
    protected_sources = tuple(paths)
    output_stack = contextlib.ExitStack()
    dump_output = None
    json_output = None
    asset_manifest_output = None
    observation_output = None
    if args.dump_dir is not None:
        args.dump_dir = args.dump_dir.resolve()
        dump_output = output_stack.enter_context(
            SecuredOutputRoot(args.dump_dir, protected_sources=protected_sources)
        )
    if args.json_out is not None:
        args.json_out = args.json_out.resolve()
        json_output = output_stack.enter_context(
            SecuredOutputRoot(args.json_out.parent, protected_sources=protected_sources)
        )
    if args.asset_manifest_out is not None:
        args.asset_manifest_out = args.asset_manifest_out.resolve()
        asset_manifest_output = output_stack.enter_context(
            SecuredOutputRoot(args.asset_manifest_out.parent, protected_sources=protected_sources)
        )
    if args.observation_records_out is not None:
        args.observation_records_out = args.observation_records_out.resolve()
        observation_output = output_stack.enter_context(
            SecuredOutputRoot(args.observation_records_out.parent, protected_sources=protected_sources)
        )

    manifest: list[dict[str, object]] = []
    archive_cache: list[tuple[Path, bytes, list[ChunkEntry]]] = []
    observation_records: list[dict[str, object]] = []
    had_error = False

    for ordinal, path in enumerate(paths, 1):
        compressed: bytes | None = None
        try:
            compressed = read_held_archive(path)
            summary, raw, chunks, member_count = _summarize_archive_bytes(
                path, compressed, envelope_kind=args.envelope
            )
            if args.expect_numeric_schedule:
                validate_numeric_resource_schedule(chunks)
            print_summary(summary)
            if args.expect_numeric_schedule:
                print("  numeric_schedule : PASS")
            if args.show_chunks > 0:
                print_chunk_table(raw, chunks, args.show_chunks, preview_bytes=args.preview_bytes)

            item = asdict(summary)
            if args.json_include_chunks:
                item["chunks"] = [
                    _chunk_record(raw, chunk, preview_bytes=args.preview_bytes, resolver=resolver)
                    for chunk in chunks
                ]
            elif args.show_chunks > 0:
                item["chunks"] = [
                    _chunk_record(raw, chunk, preview_bytes=args.preview_bytes, resolver=resolver)
                    for chunk in chunks[: args.show_chunks]
                ]

            if args.dump_dir is not None:
                assert dump_output is not None
                dumped = dump_selected_chunks(
                    path,
                    raw,
                    chunks,
                    output=dump_output,
                    dump_dir=args.dump_dir,
                    dump_tags=dump_tags,
                    dump_indices=dump_indices,
                    dump_limit_per_tag=args.dump_limit_per_tag,
                    dump_with_header=args.dump_with_header,
                    carve_tags=carve_tags,
                    extract_embedded_mesh_bodies=args.extract_embedded_mesh_bodies,
                    embedded_mesh_profile=args.embedded_mesh_profile,
                    resolver=resolver,
                    preview_bytes=args.preview_bytes,
                )
                if dumped:
                    print(f"  dumped_chunks : {len(dumped)} -> {args.dump_dir / _safe_name(path.stem)}")
                    item["dumped_chunks"] = dumped

            print("")
            manifest.append(item)
            archive_cache.append((path, raw, chunks))
            if args.observation_records_out is not None:
                observation_records.append(
                    _observed_archive_record(path, 0, compressed, raw, chunks, member_count)
                )
        except ArchiveObservationError as error:
            had_error = True
            if args.observation_records_out is not None:
                observation_records.append(
                    _rejected_archive_record(path, 0, error.category, compressed)
                )
            print(f"{path}")
            print(f"  [ERR] {error.category}")
            print("")
        except Exception as exc:
            had_error = True
            if args.observation_records_out is not None:
                observation_records.append(
                    _rejected_archive_record(path, 0, "internal_error", compressed)
                )
            print(f"{path}")
            print(f"  [ERR] {exc}")
            print("")

    if args.json_out is not None:
        assert json_output is not None
        json_output.atomic_write_json(args.json_out, manifest)
        print(f"[OK] wrote JSON manifest: {args.json_out}")

    if args.asset_manifest_out is not None:
        assert asset_manifest_output is not None
        asset_manifest = build_asset_manifest(archive_cache, resolver=resolver)
        asset_manifest_output.atomic_write_json(args.asset_manifest_out, asset_manifest)
        print(f"[OK] wrote asset manifest: {args.asset_manifest_out}")

    if args.observation_records_out is not None:
        assert observation_output is not None
        observation_output.atomic_write_bytes(
            args.observation_records_out,
            render_observation_records(_observation_report(observation_records)),
        )
        print(f"[OK] wrote observation records: {args.observation_records_out}")

    output_stack.close()
    return 1 if had_error else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
