#!/usr/bin/env python3
"""Deterministically compare PC and Xbox Battle Engine Aquila AYA resources.

The PC side is decoded only as ``pc-chunked-zlib`` and the Xbox side only as a
``raw-tag-stream``.  Envelope and top-level chunk validation remain owned by
``aya_archive_inventory``; this module adds cross-corpus pairing, validated
TEXT/MESH logical keys, exact chunk geometry, and aggregate equality counts.

Generated reports are local evidence.  They contain hashes and logical names,
not retail payload bytes, and must be written to an explicit ignored output
root.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import struct
from collections import Counter, defaultdict
from pathlib import Path
from typing import Iterable
from zipfile import ZipFile, ZipInfo

import aya_archive_inventory as aya
from safe_generated_output import SecuredOutputRoot


SCHEMA = "bea.pc-xbox-aya-logical-census.v3"
GEOMETRY_SCHEMA = "bea.pc-xbox-aya-chunk-geometry.v2"
MAX_NAME_BYTES = 300
ASSET_TAGS = frozenset({"TEXT", "MESH"})


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_sha256(lines: Iterable[str]) -> str:
    digest = hashlib.sha256()
    for line in lines:
        digest.update(line.encode("utf-8"))
        digest.update(b"\n")
    return digest.hexdigest()


def u32(data: bytes, offset: int) -> int:
    if offset < 0 or offset + 4 > len(data):
        raise ValueError("u32 outside payload")
    return struct.unpack_from("<I", data, offset)[0]


def resource_id(name: str, suffix: str) -> str:
    folded = Path(name).name.casefold()
    if not folded.endswith(suffix):
        raise ValueError("unexpected resource suffix")
    return folded[: -len(suffix)]


def read_c_string(data: bytes, offset: int) -> str:
    if offset < 0 or offset >= len(data):
        raise ValueError("logical name offset outside payload")
    end = data.find(b"\0", offset, min(len(data), offset + MAX_NAME_BYTES + 1))
    if end < 0:
        raise ValueError("logical name is not bounded by NUL")
    try:
        value = data[offset:end].decode("ascii")
    except UnicodeDecodeError as error:
        raise ValueError("logical name is not ASCII") from error
    if any(ord(character) < 0x20 for character in value):
        raise ValueError("logical name contains a control character")
    return value


def normalize_asset_name(value: str) -> str:
    return value.replace("/", "\\").casefold()


def logical_key(tag: str, payload: bytes) -> tuple[str, str, str]:
    """Return the normalized key, display value, and extraction contract."""

    if tag == "TEXT":
        if len(payload) < 25 or payload[:4] != b"DXTX" or payload[8:12] != b"CTEX":
            raise ValueError("TEXT does not carry the validated DXTX/CTEX prefix")
        if u32(payload, 4) != len(payload) - 8:
            raise ValueError("TEXT DXTX wrapper does not cover its payload")
        value = read_c_string(payload, 24)
        return normalize_asset_name(value), value, "DXTX/CTEX:name@0x18"

    if tag == "MESH":
        if len(payload) < 9 or payload[:4] != b"PMSH":
            raise ValueError("MESH does not carry the validated PMSH prefix")
        if u32(payload, 4) != len(payload) - 8:
            raise ValueError("MESH PMSH wrapper does not cover its payload")
        if len(payload) >= 17 and payload[8:12] == b"PMS2":
            if u32(payload, 12) != len(payload) - 16:
                raise ValueError("MESH PMS2 wrapper does not cover its payload")
            offset = 16
            method = "PMSH/PMS2:name@0x10"
        else:
            offset = 8
            method = "PMSH:name@0x08"
        value = read_c_string(payload, offset)
        normalized = normalize_asset_name(value) if value else "<empty-name>"
        return normalized, value, method

    return tag, tag, "top-level-tag+occurrence"


def tag_runs(chunks: list[dict[str, object]]) -> list[dict[str, object]]:
    runs: list[dict[str, object]] = []
    for chunk in chunks:
        tag = str(chunk["tag"])
        if runs and runs[-1]["tag"] == tag:
            runs[-1]["count"] = int(runs[-1]["count"]) + 1
        else:
            runs.append({"tag": tag, "count": 1})
    return runs


def compact_runs(runs: list[dict[str, object]]) -> str:
    return ",".join(f"{row['tag']}x{row['count']}" for row in runs)


def compact_counter(counter: Counter[str]) -> str:
    return ",".join(f"{key}:{counter[key]}" for key in sorted(counter))


def common_tag_edges(
    pc_chunks: list[dict[str, object]], xbox_chunks: list[dict[str, object]]
) -> tuple[int, int]:
    pc_tags = [str(row["tag"]) for row in pc_chunks]
    xbox_tags = [str(row["tag"]) for row in xbox_chunks]
    prefix = 0
    while (
        prefix < len(pc_tags)
        and prefix < len(xbox_tags)
        and pc_tags[prefix] == xbox_tags[prefix]
    ):
        prefix += 1
    suffix = 0
    while (
        suffix < len(pc_tags) - prefix
        and suffix < len(xbox_tags) - prefix
        and pc_tags[-1 - suffix] == xbox_tags[-1 - suffix]
    ):
        suffix += 1
    return prefix, suffix


def inspect_archive(
    *,
    source: bytes,
    envelope: str,
    stored_length: int,
    stored_sha256: str,
) -> tuple[dict[str, object], bytes, list[dict[str, object]]]:
    """Decode one explicitly typed archive and account for every raw byte."""

    raw, parsed, member_count, detected = aya.decode_archive_envelope(source, envelope)
    if detected != envelope:
        raise ValueError("explicit envelope changed during decode")
    if any(chunk.tag not in aya.TOP_LEVEL_ARCHIVE_TAGS for chunk in parsed):
        raise ValueError("archive contains a non-admitted top-level tag")

    chunks: list[dict[str, object]] = []
    occurrences: Counter[tuple[str, str]] = Counter()
    expected_offset = 0
    for chunk in parsed:
        if chunk.offset != expected_offset:
            raise ValueError("top-level chunk geometry has a gap or overlap")
        payload_start = chunk.offset + 8
        payload_end = payload_start + chunk.size
        payload = raw[payload_start:payload_end]
        key, display, method = logical_key(chunk.tag, payload)
        occurrence = occurrences[(chunk.tag, key)]
        occurrences[(chunk.tag, key)] += 1
        chunks.append(
            {
                "index": chunk.index,
                "tag": chunk.tag,
                "offset": chunk.offset,
                "payloadOffset": payload_start,
                "declaredSize": chunk.size,
                "endOffset": payload_end,
                "payloadSha256": sha256_bytes(payload),
                "logicalKey": key,
                "logicalDisplay": display,
                "logicalKeyMethod": method,
                "logicalOccurrence": occurrence,
            }
        )
        expected_offset = payload_end
    if expected_offset != len(raw):
        raise ValueError("top-level chunks do not account for every raw byte")

    geometry_sha256 = canonical_sha256(
        "\t".join(
            (
                str(row["index"]),
                str(row["tag"]),
                str(row["offset"]),
                str(row["declaredSize"]),
                str(row["endOffset"]),
                str(row["payloadSha256"]),
                str(row["logicalKey"]),
                str(row["logicalOccurrence"]),
                str(row["logicalKeyMethod"]),
            )
        )
        for row in chunks
    )
    counts = Counter(str(row["tag"]) for row in chunks)
    archive = {
        "envelope": detected,
        "memberCount": member_count,
        "storedLength": stored_length,
        "storedSha256": stored_sha256,
        "rawLength": len(raw),
        "rawSha256": sha256_bytes(raw),
        "chunkCount": len(chunks),
        "tagCounts": dict(sorted(counts.items())),
        "tagRuns": tag_runs(chunks),
        "geometrySha256": geometry_sha256,
    }
    return archive, raw, chunks


def chunk_groups(
    chunks: list[dict[str, object]],
) -> dict[tuple[str, str], list[dict[str, object]]]:
    groups: dict[tuple[str, str], list[dict[str, object]]] = defaultdict(list)
    for chunk in chunks:
        groups[(str(chunk["tag"]), str(chunk["logicalKey"]))].append(chunk)
    return groups


def count_map(chunks: list[dict[str, object]]) -> Counter[tuple[str, str]]:
    return Counter((str(row["tag"]), str(row["logicalKey"])) for row in chunks)


def logical_deltas(
    left: Counter[tuple[str, str]], right: Counter[tuple[str, str]]
) -> list[dict[str, object]]:
    delta = left - right
    return [
        {"tag": tag, "logicalKey": key, "count": delta[(tag, key)]}
        for tag, key in sorted(delta)
    ]


def geometry_join_row(
    resource: str,
    platform: str,
    archive: dict[str, object],
    row: dict[str, object],
    counterpart: dict[str, object] | None,
    confidence: str,
    same_size: bool,
    same_payload: bool,
) -> dict[str, object]:
    return {
        "schemaVersion": GEOMETRY_SCHEMA,
        "resourceId": resource,
        "platform": platform,
        "archiveRawSha256": archive["rawSha256"],
        "chunkIndex": row["index"],
        "tag": row["tag"],
        "offset": row["offset"],
        "payloadOffset": row["payloadOffset"],
        "declaredSize": row["declaredSize"],
        "endOffset": row["endOffset"],
        "payloadSha256": row["payloadSha256"],
        "logicalKey": row["logicalKey"],
        "logicalDisplay": row["logicalDisplay"],
        "logicalKeyMethod": row["logicalKeyMethod"],
        "logicalOccurrence": row["logicalOccurrence"],
        "joinConfidence": confidence,
        "counterpartIndex": "" if counterpart is None else counterpart["index"],
        "counterpartDeclaredSize": (
            "" if counterpart is None else counterpart["declaredSize"]
        ),
        "counterpartPayloadSha256": (
            "" if counterpart is None else counterpart["payloadSha256"]
        ),
        "sameDeclaredSize": int(same_size),
        "samePayloadSha256": int(same_payload),
    }


def compare_pair(
    resource: str,
    pc_archive: dict[str, object],
    pc_chunks: list[dict[str, object]],
    xbox_archive: dict[str, object],
    xbox_chunks: list[dict[str, object]],
) -> tuple[dict[str, object], list[dict[str, object]], dict[str, Counter[str]]]:
    """Compare one normalized resource ID without assuming ordinal equality."""

    pc_groups = chunk_groups(pc_chunks)
    xbox_groups = chunk_groups(xbox_chunks)
    pc_counts = count_map(pc_chunks)
    xbox_counts = count_map(xbox_chunks)
    geometry_rows: list[dict[str, object]] = []
    tag_aggregate: dict[str, Counter[str]] = defaultdict(Counter)

    for chunk in pc_chunks:
        tag = str(chunk["tag"])
        tag_aggregate[tag]["pcChunks"] += 1
        tag_aggregate[tag]["pcPayloadBytes"] += int(chunk["declaredSize"])
    for chunk in xbox_chunks:
        tag = str(chunk["tag"])
        tag_aggregate[tag]["xboxChunks"] += 1
        tag_aggregate[tag]["xboxPayloadBytes"] += int(chunk["declaredSize"])

    for tag, key in sorted(set(pc_groups) | set(xbox_groups)):
        pc_rows = pc_groups.get((tag, key), [])
        xbox_rows = xbox_groups.get((tag, key), [])
        shared = min(len(pc_rows), len(xbox_rows))
        if max(len(pc_rows), len(xbox_rows)) == 1:
            confidence = "unique-logical-key"
        elif len(pc_rows) == len(xbox_rows):
            confidence = "ordered-duplicate-equal-count"
        else:
            confidence = "ordered-duplicate-unequal-count"

        tag_aggregate[tag]["logicalJoins"] += shared
        tag_aggregate[tag]["pcOnlyLogicalChunks"] += len(pc_rows) - shared
        tag_aggregate[tag]["xboxOnlyLogicalChunks"] += len(xbox_rows) - shared
        if confidence != "unique-logical-key":
            tag_aggregate[tag]["duplicateKeyJoins"] += shared

        for ordinal in range(shared):
            pc_row = pc_rows[ordinal]
            xbox_row = xbox_rows[ordinal]
            same_size = int(pc_row["declaredSize"]) == int(xbox_row["declaredSize"])
            same_payload = pc_row["payloadSha256"] == xbox_row["payloadSha256"]
            tag_aggregate[tag]["sameDeclaredSize"] += int(same_size)
            tag_aggregate[tag]["samePayloadSha256"] += int(same_payload)
            geometry_rows.append(
                geometry_join_row(
                    resource,
                    "PC",
                    pc_archive,
                    pc_row,
                    xbox_row,
                    confidence,
                    same_size,
                    same_payload,
                )
            )
            geometry_rows.append(
                geometry_join_row(
                    resource,
                    "XBOX",
                    xbox_archive,
                    xbox_row,
                    pc_row,
                    confidence,
                    same_size,
                    same_payload,
                )
            )

        for row in pc_rows[shared:]:
            geometry_rows.append(
                geometry_join_row(
                    resource,
                    "PC",
                    pc_archive,
                    row,
                    None,
                    "pc-only-logical-key",
                    False,
                    False,
                )
            )
        for row in xbox_rows[shared:]:
            geometry_rows.append(
                geometry_join_row(
                    resource,
                    "XBOX",
                    xbox_archive,
                    row,
                    None,
                    "xbox-only-logical-key",
                    False,
                    False,
                )
            )

    pc_tags = [str(row["tag"]) for row in pc_chunks]
    xbox_tags = [str(row["tag"]) for row in xbox_chunks]
    pc_tag_counts = Counter(pc_tags)
    xbox_tag_counts = Counter(xbox_tags)
    prefix, suffix = common_tag_edges(pc_chunks, xbox_chunks)
    pc_asset_counts = Counter(
        {key: count for key, count in pc_counts.items() if key[0] in ASSET_TAGS}
    )
    xbox_asset_counts = Counter(
        {key: count for key, count in xbox_counts.items() if key[0] in ASSET_TAGS}
    )
    pc_key_sequence = [
        (str(row["tag"]), str(row["logicalKey"])) for row in pc_chunks
    ]
    xbox_key_sequence = [
        (str(row["tag"]), str(row["logicalKey"])) for row in xbox_chunks
    ]
    pair = {
        "resourceId": resource,
        "pc": pc_archive,
        "xbox": xbox_archive,
        "comparison": {
            "tagSequenceEqual": pc_tags == xbox_tags,
            "tagRunTopologyEqual": [row["tag"] for row in pc_archive["tagRuns"]]
            == [row["tag"] for row in xbox_archive["tagRuns"]],
            "logicalKeySequenceEqual": pc_key_sequence == xbox_key_sequence,
            "logicalChunkMultisetEqual": pc_counts == xbox_counts,
            "topLevelTextMeshMultisetEqual": pc_asset_counts == xbox_asset_counts,
            "commonTagPrefixCount": prefix,
            "commonTagSuffixCount": suffix,
            "pcMinusXboxTagCounts": dict(
                sorted((pc_tag_counts - xbox_tag_counts).items())
            ),
            "xboxMinusPcTagCounts": dict(
                sorted((xbox_tag_counts - pc_tag_counts).items())
            ),
            "pcOnlyLogical": logical_deltas(pc_counts, xbox_counts),
            "xboxOnlyLogical": logical_deltas(xbox_counts, pc_counts),
        },
    }
    return pair, geometry_rows, tag_aggregate


def read_zip_member(archive: ZipFile, info: ZipInfo) -> bytes:
    if info.file_size > aya.MAX_INFLATED_BYTES:
        raise ValueError("Xbox raw AYA exceeds the bounded raw-stream limit")
    with archive.open(info) as stream:
        source = stream.read(aya.MAX_INFLATED_BYTES + 1)
    if len(source) != info.file_size:
        raise ValueError("bounded Xbox member read disagrees with central directory")
    return source


def build(
    pc_root: Path, xbox_zip: Path
) -> tuple[dict[str, object], list[dict[str, object]]]:
    """Build a deterministic comparison from one PC root and one Xbox ZIP."""

    pc_root = pc_root.resolve(strict=True)
    xbox_zip = xbox_zip.resolve(strict=True)
    pc: dict[str, Path] = {}
    for path in pc_root.glob("*_res_PC.aya"):
        key = resource_id(path.name, "_res_pc.aya")
        if key in pc:
            raise ValueError("duplicate PC resource id")
        pc[key] = path

    pairs: list[dict[str, object]] = []
    geometry: list[dict[str, object]] = []
    aggregate: dict[str, Counter[str]] = defaultdict(Counter)
    pc_manifest_rows: list[str] = []
    xbox_manifest_rows: list[str] = []
    xbox_only_records: list[dict[str, object]] = []

    with ZipFile(xbox_zip) as archive:
        xbox: dict[str, ZipInfo] = {}
        for info in archive.infolist():
            if not info.filename.casefold().endswith("_res_xbox.aya"):
                continue
            key = resource_id(info.filename, "_res_xbox.aya")
            if key in xbox:
                raise ValueError("duplicate Xbox resource id")
            xbox[key] = info

        # The v3 output has an explicit Xbox-only record surface but no
        # symmetric PC-only record surface. Refuse that unsupported shape
        # instead of publishing an incomplete PC manifest or byte total.
        if set(pc) - set(xbox):
            raise ValueError("PC-only resources are not supported by census v3")

        shared = sorted(set(pc) & set(xbox))
        for key in shared:
            pc_source = aya.read_held_archive(pc[key])
            pc_archive, _pc_raw, pc_chunks = inspect_archive(
                source=pc_source,
                envelope="pc-chunked-zlib",
                stored_length=len(pc_source),
                stored_sha256=sha256_bytes(pc_source),
            )
            xbox_source = read_zip_member(archive, xbox[key])
            xbox_archive, _xbox_raw, xbox_chunks = inspect_archive(
                source=xbox_source,
                envelope="raw-tag-stream",
                stored_length=len(xbox_source),
                stored_sha256=sha256_bytes(xbox_source),
            )
            xbox_archive["zipCrc32"] = f"{xbox[key].CRC:08x}"
            xbox_archive["zipCompressionMethod"] = xbox[key].compress_type
            xbox_archive["zipCompressedLength"] = xbox[key].compress_size
            pair, rows, tag_rows = compare_pair(
                key, pc_archive, pc_chunks, xbox_archive, xbox_chunks
            )
            pairs.append(pair)
            geometry.extend(rows)
            for tag, counts in tag_rows.items():
                aggregate[tag].update(counts)
            pc_manifest_rows.append(
                "\t".join(
                    (
                        key,
                        pc[key].name.casefold(),
                        str(pc_archive["storedLength"]),
                        str(pc_archive["storedSha256"]),
                        str(pc_archive["rawLength"]),
                        str(pc_archive["rawSha256"]),
                        str(pc_archive["memberCount"]),
                        str(pc_archive["chunkCount"]),
                        str(pc_archive["geometrySha256"]),
                    )
                )
            )
            # The Xbox manifest intentionally binds central-directory compressed
            # length and CRC plus raw identity/geometry.  The whole-ZIP hash and
            # method histogram below bind the surrounding container separately.
            xbox_manifest_rows.append(
                "\t".join(
                    (
                        key,
                        Path(xbox[key].filename).name.casefold(),
                        str(xbox_archive["zipCompressedLength"]),
                        str(xbox_archive["rawLength"]),
                        str(xbox_archive["rawSha256"]),
                        str(xbox_archive["zipCrc32"]),
                        str(xbox_archive["chunkCount"]),
                        str(xbox_archive["geometrySha256"]),
                    )
                )
            )

        for key in sorted(set(xbox) - set(pc)):
            source = read_zip_member(archive, xbox[key])
            record, _raw, chunks = inspect_archive(
                source=source,
                envelope="raw-tag-stream",
                stored_length=len(source),
                stored_sha256=sha256_bytes(source),
            )
            record["resourceId"] = key
            record["zipCrc32"] = f"{xbox[key].CRC:08x}"
            record["zipCompressionMethod"] = xbox[key].compress_type
            record["zipCompressedLength"] = xbox[key].compress_size
            xbox_only_records.append(record)
            xbox_manifest_rows.append(
                "\t".join(
                    (
                        key,
                        Path(xbox[key].filename).name.casefold(),
                        str(record["zipCompressedLength"]),
                        str(record["rawLength"]),
                        str(record["rawSha256"]),
                        str(record["zipCrc32"]),
                        str(record["chunkCount"]),
                        str(record["geometrySha256"]),
                    )
                )
            )
            for chunk in chunks:
                geometry.append(
                    geometry_join_row(
                        key,
                        "XBOX",
                        record,
                        chunk,
                        None,
                        "xbox-only-resource",
                        False,
                        False,
                    )
                )

        zip_method_counts = dict(
            sorted(Counter(str(info.compress_type) for info in xbox.values()).items())
        )
        zip_compressed_resource_bytes = sum(info.compress_size for info in xbox.values())

    divergent = [pair for pair in pairs if not pair["comparison"]["tagSequenceEqual"]]
    tag_equal_text_mesh_different = [
        str(pair["resourceId"])
        for pair in pairs
        if pair["comparison"]["tagSequenceEqual"]
        and not pair["comparison"]["topLevelTextMeshMultisetEqual"]
    ]
    tag_aggregates: list[dict[str, object]] = []
    for tag in sorted(aggregate):
        counts = aggregate[tag]
        tag_aggregates.append(
            {
                "tag": tag,
                **{
                    key: counts[key]
                    for key in (
                        "pcChunks",
                        "xboxChunks",
                        "logicalJoins",
                        "pcOnlyLogicalChunks",
                        "xboxOnlyLogicalChunks",
                        "duplicateKeyJoins",
                        "sameDeclaredSize",
                        "samePayloadSha256",
                        "pcPayloadBytes",
                        "xboxPayloadBytes",
                    )
                },
                "xboxMinusPcPayloadBytes": counts["xboxPayloadBytes"]
                - counts["pcPayloadBytes"],
            }
        )

    pc_total_raw = sum(int(pair["pc"]["rawLength"]) for pair in pairs)
    xbox_paired_total_raw = sum(int(pair["xbox"]["rawLength"]) for pair in pairs)
    result = {
        "schemaVersion": SCHEMA,
        "sources": {
            "pc": {
                "archiveCount": len(pc),
                "envelope": "pc-chunked-zlib",
                "canonicalManifestSha256": canonical_sha256(sorted(pc_manifest_rows)),
                "totalRawBytes": pc_total_raw,
                "totalStoredBytes": sum(
                    int(pair["pc"]["storedLength"]) for pair in pairs
                ),
                "zlibMemberCount": sum(
                    int(pair["pc"]["memberCount"]) for pair in pairs
                ),
                "zlibMemberCountHistogram": dict(
                    sorted(
                        Counter(
                            str(pair["pc"]["memberCount"]) for pair in pairs
                        ).items()
                    )
                ),
            },
            "xbox": {
                "archiveCount": len(xbox_manifest_rows),
                "envelope": "raw-tag-stream",
                "zipLength": xbox_zip.stat().st_size,
                "zipSha256": sha256_file(xbox_zip),
                "canonicalMemberManifestSha256": canonical_sha256(
                    sorted(xbox_manifest_rows)
                ),
                "pairedTotalRawBytes": xbox_paired_total_raw,
                "allResourceTotalRawBytes": xbox_paired_total_raw
                + sum(int(row["rawLength"]) for row in xbox_only_records),
                "totalZipCompressedResourceBytes": zip_compressed_resource_bytes,
                "zipCompressionMethodCounts": zip_method_counts,
            },
        },
        "summary": {
            "pairedCount": len(pairs),
            "pcOnly": sorted(set(pc) - {str(pair["resourceId"]) for pair in pairs}),
            "xboxOnly": [str(row["resourceId"]) for row in xbox_only_records],
            "tagSequenceEqualCount": len(pairs) - len(divergent),
            "tagSequenceDivergentCount": len(divergent),
            "tagRunTopologyEqualCount": sum(
                int(pair["comparison"]["tagRunTopologyEqual"]) for pair in pairs
            ),
            "logicalKeySequenceEqualCount": sum(
                int(pair["comparison"]["logicalKeySequenceEqual"]) for pair in pairs
            ),
            "topLevelTextMeshMultisetEqualCount": sum(
                int(pair["comparison"]["topLevelTextMeshMultisetEqual"])
                for pair in pairs
            ),
            "tagSequenceEqualButTextMeshDivergentCount": len(
                tag_equal_text_mesh_different
            ),
            "tagSequenceEqualButTextMeshDivergentIds": (
                tag_equal_text_mesh_different
            ),
            "pcChunkCount": sum(int(pair["pc"]["chunkCount"]) for pair in pairs),
            "xboxPairedChunkCount": sum(
                int(pair["xbox"]["chunkCount"]) for pair in pairs
            ),
            "xboxAllResourceChunkCount": sum(
                int(pair["xbox"]["chunkCount"]) for pair in pairs
            )
            + sum(int(row["chunkCount"]) for row in xbox_only_records),
            "geometryRowCount": len(geometry),
        },
        "tagAggregates": tag_aggregates,
        "divergences": [
            {
                "resourceId": pair["resourceId"],
                "pcChunkCount": pair["pc"]["chunkCount"],
                "xboxChunkCount": pair["xbox"]["chunkCount"],
                "pcTagRuns": pair["pc"]["tagRuns"],
                "xboxTagRuns": pair["xbox"]["tagRuns"],
                **pair["comparison"],
            }
            for pair in divergent
        ],
        "xboxOnlyRecords": xbox_only_records,
        "pairs": pairs,
    }
    return result, sorted(
        geometry,
        key=lambda row: (
            str(row["resourceId"]),
            0 if row["platform"] == "PC" else 1,
            int(row["chunkIndex"]),
        ),
    )


def validate_expectations(
    result: dict[str, object],
    *,
    paired_count: int | None = None,
    require_no_pc_only: bool = False,
    xbox_only: list[str] | None = None,
    divergent_count: int | None = None,
    divergence_tags: frozenset[str] | None = None,
) -> None:
    """Fail closed when a caller's preregistered census facts have changed."""

    summary = result["summary"]
    if paired_count is not None and summary["pairedCount"] != paired_count:
        raise ValueError("paired resource census changed")
    if require_no_pc_only and summary["pcOnly"]:
        raise ValueError("unexpected PC-only resource")
    if xbox_only is not None and summary["xboxOnly"] != sorted(xbox_only):
        raise ValueError("Xbox-only resource census changed")
    if (
        divergent_count is not None
        and summary["tagSequenceDivergentCount"] != divergent_count
    ):
        raise ValueError("tag-sequence divergence census changed")
    if divergence_tags is not None:
        for pair in result["divergences"]:
            changed = set(pair["pcMinusXboxTagCounts"]) | set(
                pair["xboxMinusPcTagCounts"]
            )
            if not changed or not changed <= divergence_tags:
                raise ValueError("tag divergence escaped the expected tag families")


def write_outputs(
    output_root: Path,
    prefix: str,
    result: dict[str, object],
    geometry: list[dict[str, object]],
    protected_sources: tuple[Path, ...],
) -> list[Path]:
    if not geometry:
        raise ValueError("comparison produced no geometry rows")
    summary_path = output_root / f"{prefix}-census.json"
    geometry_path = output_root / f"{prefix}-geometry.tsv"
    divergence_path = output_root / f"{prefix}-divergences.tsv"
    with SecuredOutputRoot(output_root, protected_sources=protected_sources) as secured:
        secured.atomic_write_text(
            summary_path, json.dumps(result, indent=2, sort_keys=True) + "\n"
        )
        geometry_fields = list(geometry[0])
        with secured.atomic_text_writer(geometry_path) as stream:
            writer = csv.DictWriter(
                stream, fieldnames=geometry_fields, delimiter="\t", lineterminator="\n"
            )
            writer.writeheader()
            writer.writerows(geometry)

        divergence_fields = (
            "resourceId",
            "pcChunkCount",
            "xboxChunkCount",
            "commonTagPrefixCount",
            "commonTagSuffixCount",
            "pcTagRuns",
            "xboxTagRuns",
            "pcMinusXboxTagCounts",
            "xboxMinusPcTagCounts",
            "pcOnlyLogical",
            "xboxOnlyLogical",
        )
        with secured.atomic_text_writer(divergence_path) as stream:
            writer = csv.DictWriter(
                stream,
                fieldnames=divergence_fields,
                delimiter="\t",
                lineterminator="\n",
            )
            writer.writeheader()
            for row in result["divergences"]:
                writer.writerow(
                    {
                        "resourceId": row["resourceId"],
                        "pcChunkCount": row["pcChunkCount"],
                        "xboxChunkCount": row["xboxChunkCount"],
                        "commonTagPrefixCount": row["commonTagPrefixCount"],
                        "commonTagSuffixCount": row["commonTagSuffixCount"],
                        "pcTagRuns": compact_runs(row["pcTagRuns"]),
                        "xboxTagRuns": compact_runs(row["xboxTagRuns"]),
                        "pcMinusXboxTagCounts": compact_counter(
                            Counter(row["pcMinusXboxTagCounts"])
                        ),
                        "xboxMinusPcTagCounts": compact_counter(
                            Counter(row["xboxMinusPcTagCounts"])
                        ),
                        "pcOnlyLogical": json.dumps(
                            row["pcOnlyLogical"],
                            sort_keys=True,
                            separators=(",", ":"),
                        ),
                        "xboxOnlyLogical": json.dumps(
                            row["xboxOnlyLogical"],
                            sort_keys=True,
                            separators=(",", ":"),
                        ),
                    }
                )
    return [summary_path, geometry_path, divergence_path]


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pc-root", required=True, type=Path)
    parser.add_argument("--xbox-zip", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--prefix", required=True)
    parser.add_argument("--expect-paired-count", type=int)
    parser.add_argument("--expect-no-pc-only", action="store_true")
    parser.add_argument("--expect-xbox-only", action="append")
    parser.add_argument("--expect-divergent-count", type=int)
    parser.add_argument(
        "--expect-divergence-tag",
        action="append",
        choices=sorted(aya.TOP_LEVEL_ARCHIVE_TAGS),
    )
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    if not args.prefix or any(
        character not in "abcdefghijklmnopqrstuvwxyz0123456789-"
        for character in args.prefix
    ):
        raise ValueError(
            "prefix must contain only lowercase ASCII letters, digits, and hyphens"
        )
    pc_root = args.pc_root.resolve(strict=True)
    xbox_zip = args.xbox_zip.resolve(strict=True)
    output_root = args.output_root.resolve()
    result, geometry = build(pc_root, xbox_zip)
    validate_expectations(
        result,
        paired_count=args.expect_paired_count,
        require_no_pc_only=args.expect_no_pc_only,
        xbox_only=(
            None if args.expect_xbox_only is None else sorted(args.expect_xbox_only)
        ),
        divergent_count=args.expect_divergent_count,
        divergence_tags=(
            None
            if args.expect_divergence_tag is None
            else frozenset(args.expect_divergence_tag)
        ),
    )
    paths = write_outputs(
        output_root,
        args.prefix,
        result,
        geometry,
        (pc_root, xbox_zip),
    )
    for path in paths:
        print(f"READY {path.name} {path.stat().st_size} {sha256_file(path)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
