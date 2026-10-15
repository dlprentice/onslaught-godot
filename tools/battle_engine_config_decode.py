#!/usr/bin/env python3
"""Read and re-encode BEA ``battle engine configurations.dat`` in memory.

This tool is deliberately read-only: it has no output-file or install-mutation
path. Parsed bytes stay in explicit spans so re-encoding an admitted baseline
preserves every bit, including fields whose meaning remains ``UNKNOWN``.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import struct
import sys
from dataclasses import dataclass, replace
from pathlib import Path


EXPECTED_RECORD_COUNT = 6
EXPECTED_RECORD_VERSION = 12
STORE_COUNT = 6
MAX_CSTRING_BYTES = 256  # retail uses a 256-byte stack buffer, including NUL
BASELINE_SIZE = 1_514
BASELINE_SHA256 = (
    "58722b12a04cae97ad2163acb2cc2c1699f95a0688318bd8a86696714d94454a"
)
BASELINE_NAMES = (
    "Racer",
    "Standard",
    "Sniper",
    "Aquila Prototype",
    "Laser",
    "Blaster",
)
BASELINE_RECORD_OFFSETS = (0x004, 0x0D4, 0x1EE, 0x2D2, 0x3DE, 0x4DD)
BASELINE_RECORD_END_OFFSETS = (0x0D4, 0x1EE, 0x2D2, 0x3DE, 0x4DD, 0x5EA)

SUPPORTED = "SUPPORTED"
FRAME = "FRAME"
UNKNOWN = "UNKNOWN"

LEADING_FIELDS = (
    "mLife",
    "mEnergy",
    "mGroundEnergyIncrease",
    "mMaxAirEnergyCost",
    "mMinTransformEnergy",
    "mMaxAirVelocity",
    "mGroundVelocity",
    "mAirTurnRate",
    "mGroundTurnRate",
)
TRAILING_FLOAT_FIELDS = (
    "mMinAirVelocity",
    "mMaxWalkVelocity",
    "mWalkFriction",
    "mMinAirEnergyCost",
    "mRollEnergyCost",
    "mLoopEnergyCost",
)
FIXED_U32_FIELDS = frozenset(
    (*LEADING_FIELDS, *TRAILING_FLOAT_FIELDS, "mShieldEfficiency", "mStealth")
).union(
    f"mStore{kind}[{store_index}]"
    for store_index in range(STORE_COUNT)
    for kind in ("Heat", "Value")
)

BASELINE_FILE_COUNT_EVIDENCE = (
    "references/Onslaught/BattleEngineDataManager.h:301-317;"
    "reverse-engineering/asset-formats/config-dat.md:57-70"
)
UNPINNED_EVIDENCE = "UNPINNED_INPUT_NO_RETAIL_EVIDENCE"
UNPINNED_FALSIFIER = (
    "Hash-pin and independently adjudicate this input before treating its "
    "offsets as shipped retail evidence."
)
UNKNOWN_FALSIFIER = (
    "A promoted retail-static destination/consumer join for this exact span; "
    "source order alone is insufficient."
)
RECORD_EVIDENCE = (
    "reverse-engineering/asset-formats/config-dat.md:57-70;"
    "reverse-engineering/installed-corpus-census.md:619-638;"
    "rebuild/OnslaughtRebuild.Core/SimulationConstants.cs:135-221,444-622;"
    "rebuild/PROVENANCE.md:514-524;"
    "rebuild/OnslaughtRebuild.Core/RetailBattleEngineConfigurations.cs:85-91;"
    "rebuild/OnslaughtRebuild.Core/RetailWeaponStores.cs:11-42;"
    "references/Onslaught/BattleEngineDataManager.cpp:148-446"
)


class BattleEngineConfigError(ValueError):
    """The input cannot be represented by the admitted version-12 framing."""


@dataclass(frozen=True)
class ByteSpan:
    offset: int
    record_index: int | None
    field: str
    classification: str
    raw: bytes

    @property
    def width(self) -> int:
        return len(self.raw)

    @property
    def end_offset(self) -> int:
        return self.offset + self.width

    @property
    def u32_bits(self) -> int:
        if self.width != 4:
            raise BattleEngineConfigError(
                f"{self.field} at 0x{self.offset:X} is {self.width} bytes, not 4"
            )
        return struct.unpack("<I", self.raw)[0]


@dataclass(frozen=True)
class BattleEngineConfigRecord:
    index: int
    offset: int
    end_offset: int
    spans: tuple[ByteSpan, ...]

    @property
    def version(self) -> int:
        return struct.unpack("<i", self.field("version").raw)[0]

    @property
    def configuration_name(self) -> str:
        raw = self.field("mConfigurationName").raw
        return raw[:-1].decode("ascii")

    def field(self, name: str) -> ByteSpan:
        matches = [span for span in self.spans if span.field == name]
        if len(matches) != 1:
            raise BattleEngineConfigError(
                f"record {self.index}: expected one {name!r} span, found {len(matches)}"
            )
        return matches[0]


@dataclass(frozen=True)
class BattleEngineConfigFile:
    header: ByteSpan
    records: tuple[BattleEngineConfigRecord, ...]

    @property
    def spans(self) -> tuple[ByteSpan, ...]:
        return (self.header,) + tuple(
            span for record in self.records for span in record.spans
        )

    @property
    def covered_bytes(self) -> int:
        return sum(span.width for span in self.spans)

    def assert_complete_coverage(self, expected_size: int) -> None:
        next_offset = 0
        for span in self.spans:
            if span.offset != next_offset:
                relationship = "overlap" if span.offset < next_offset else "gap"
                raise BattleEngineConfigError(
                    f"coverage {relationship}: expected 0x{next_offset:X}, "
                    f"found {span.field} at 0x{span.offset:X}"
                )
            if span.width <= 0:
                raise BattleEngineConfigError(
                    f"zero-width span {span.field} at 0x{span.offset:X}"
                )
            next_offset = span.end_offset
        if next_offset != expected_size:
            raise BattleEngineConfigError(
                f"coverage ends at 0x{next_offset:X}, expected 0x{expected_size:X}"
            )
        if self.covered_bytes != expected_size:
            raise BattleEngineConfigError(
                f"covered {self.covered_bytes} bytes, expected {expected_size}"
            )


class _Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    @property
    def remaining(self) -> int:
        return len(self.data) - self.offset

    def span(
        self,
        width: int,
        record_index: int | None,
        field: str,
        classification: str,
    ) -> ByteSpan:
        if width <= 0:
            raise BattleEngineConfigError(
                f"{field} at 0x{self.offset:X}: invalid width {width}"
            )
        end = self.offset + width
        if end > len(self.data):
            raise BattleEngineConfigError(
                f"{field} at 0x{self.offset:X}: truncated; need {width} bytes, "
                f"have {self.remaining}"
            )
        result = ByteSpan(
            offset=self.offset,
            record_index=record_index,
            field=field,
            classification=classification,
            raw=self.data[self.offset:end],
        )
        self.offset = end
        return result

    def cstring(
        self,
        record_index: int,
        field: str,
        classification: str,
    ) -> ByteSpan:
        search_end = min(len(self.data), self.offset + MAX_CSTRING_BYTES)
        terminator = self.data.find(b"\0", self.offset, search_end)
        if terminator < 0:
            raise BattleEngineConfigError(
                f"record {record_index} {field} at 0x{self.offset:X}: "
                f"missing NUL within {MAX_CSTRING_BYTES} bytes"
            )
        return self.span(
            terminator - self.offset + 1,
            record_index,
            field,
            classification,
        )


def _signed_i32(span: ByteSpan) -> int:
    return struct.unpack("<i", span.raw)[0]


def _counted_unknown_cstrings(
    reader: _Reader,
    record_index: int,
    block_index: int,
) -> list[ByteSpan]:
    count_span = reader.span(
        4,
        record_index,
        f"unknown_counted_block_{block_index}.count",
        FRAME,
    )
    count = _signed_i32(count_span)
    if count < 0:
        raise BattleEngineConfigError(
            f"record {record_index} block {block_index}: negative count {count}"
        )
    if count > reader.remaining:
        raise BattleEngineConfigError(
            f"record {record_index} block {block_index}: count {count} cannot fit "
            f"in {reader.remaining} remaining bytes"
        )
    spans = [count_span]
    for entry_index in range(count):
        spans.append(
            reader.cstring(
                record_index,
                f"UNKNOWN_COUNTED_BLOCK_{block_index}_ENTRY_{entry_index}",
                UNKNOWN,
            )
        )
    return spans


def parse_config(
    data: bytes,
    *,
    expected_sha256: str | None = None,
) -> BattleEngineConfigFile:
    """Parse one exact-shape six-record, version-12 configuration stream."""

    if expected_sha256 is not None:
        actual_sha256 = hashlib.sha256(data).hexdigest()
        if actual_sha256 != expected_sha256:
            raise BattleEngineConfigError(
                f"unexpected SHA-256 {actual_sha256}; expected {expected_sha256}"
            )

    reader = _Reader(data)
    header = reader.span(4, None, "record_count", FRAME)
    record_count = _signed_i32(header)
    if record_count != EXPECTED_RECORD_COUNT:
        raise BattleEngineConfigError(
            f"record_count {record_count}; expected {EXPECTED_RECORD_COUNT}"
        )

    records: list[BattleEngineConfigRecord] = []
    for record_index in range(record_count):
        record_offset = reader.offset
        spans: list[ByteSpan] = []
        version = reader.span(4, record_index, "version", FRAME)
        spans.append(version)
        version_value = _signed_i32(version)
        if version_value != EXPECTED_RECORD_VERSION:
            raise BattleEngineConfigError(
                f"record {record_index} at 0x{record_offset:X}: version "
                f"{version_value}; expected {EXPECTED_RECORD_VERSION}"
            )

        for field in LEADING_FIELDS:
            spans.append(reader.span(4, record_index, field, SUPPORTED))

        name = reader.cstring(
            record_index, "mConfigurationName", SUPPORTED
        )
        try:
            name.raw[:-1].decode("ascii")
        except UnicodeDecodeError as exc:
            raise BattleEngineConfigError(
                f"record {record_index} configuration name is not ASCII"
            ) from exc
        spans.append(name)

        spans.append(reader.span(4, record_index, "mShieldEfficiency", SUPPORTED))
        spans.append(reader.span(4, record_index, "mStealth", SUPPORTED))
        spans.append(
            reader.cstring(record_index, "UNKNOWN_CSTRING_0", UNKNOWN)
        )
        spans.extend(_counted_unknown_cstrings(reader, record_index, 0))
        spans.extend(_counted_unknown_cstrings(reader, record_index, 1))

        for store_index in range(STORE_COUNT):
            spans.append(
                reader.span(
                    4,
                    record_index,
                    f"mStoreHeat[{store_index}]",
                    SUPPORTED,
                )
            )
            spans.append(
                reader.span(
                    4,
                    record_index,
                    f"mStoreValue[{store_index}]",
                    SUPPORTED,
                )
            )

        for field in TRAILING_FLOAT_FIELDS:
            spans.append(reader.span(4, record_index, field, SUPPORTED))

        for unknown_index in range(1, 4):
            spans.append(
                reader.cstring(
                    record_index,
                    f"UNKNOWN_CSTRING_{unknown_index}",
                    UNKNOWN,
                )
            )
        spans.append(reader.span(4, record_index, "UNKNOWN_I32_0", UNKNOWN))

        records.append(
            BattleEngineConfigRecord(
                index=record_index,
                offset=record_offset,
                end_offset=reader.offset,
                spans=tuple(spans),
            )
        )

    if reader.offset != len(data):
        raise BattleEngineConfigError(
            f"{len(data) - reader.offset} trailing bytes at 0x{reader.offset:X}"
        )

    parsed = BattleEngineConfigFile(header=header, records=tuple(records))
    parsed.assert_complete_coverage(len(data))
    return parsed


def encode_config(parsed: BattleEngineConfigFile) -> bytes:
    """Re-encode every stored span without normalising any value."""

    output = b"".join(span.raw for span in parsed.spans)
    parsed.assert_complete_coverage(len(output))
    return output


def parse_exact_baseline(data: bytes) -> BattleEngineConfigFile:
    """Parse only the hash-pinned shipped baseline and verify its public bounds."""

    if len(data) != BASELINE_SIZE:
        raise BattleEngineConfigError(
            f"unexpected baseline size {len(data)}; expected {BASELINE_SIZE}"
        )
    parsed = parse_config(data, expected_sha256=BASELINE_SHA256)
    names = tuple(record.configuration_name for record in parsed.records)
    offsets = tuple(record.offset for record in parsed.records)
    end_offsets = tuple(record.end_offset for record in parsed.records)
    if names != BASELINE_NAMES:
        raise BattleEngineConfigError(
            f"unexpected baseline profile order {names!r}; expected {BASELINE_NAMES!r}"
        )
    if offsets != BASELINE_RECORD_OFFSETS or end_offsets != BASELINE_RECORD_END_OFFSETS:
        raise BattleEngineConfigError(
            "unexpected baseline record boundaries: "
            f"starts={offsets!r}, ends={end_offsets!r}"
        )
    return parsed


def mutate_supported_u32(
    parsed: BattleEngineConfigFile,
    *,
    record_index: int,
    field: str,
    bits: int,
) -> BattleEngineConfigFile:
    """Return an in-memory copy with one admitted four-byte field changed.

    This is an encoder-locality probe, not a file-writing or patch-authorisation
    surface. Unknown spans and variable-width fields cannot be changed here.
    """

    # Re-derive all classifications and field identities from bytes so a caller
    # cannot relabel an UNKNOWN or variable-width span as an admitted u32.
    parsed = parse_config(encode_config(parsed))
    if not 0 <= record_index < len(parsed.records):
        raise BattleEngineConfigError(f"record index {record_index} is out of range")
    if not 0 <= bits <= 0xFFFFFFFF:
        raise BattleEngineConfigError(f"bits {bits!r} do not fit u32")

    record = parsed.records[record_index]
    target = record.field(field)
    if (
        target.classification != SUPPORTED
        or target.width != 4
        or target.field not in FIXED_U32_FIELDS
    ):
        raise BattleEngineConfigError(
            f"{field} is not an admitted fixed-width four-byte field"
        )

    replacement = replace(target, raw=struct.pack("<I", bits))
    spans = tuple(replacement if span is target else span for span in record.spans)
    records = tuple(
        replace(record, spans=spans) if current.index == record_index else current
        for current in parsed.records
    )
    return replace(parsed, records=records)


def _span_encoding(span: ByteSpan) -> str:
    if span.field == "mConfigurationName":
        return "cstring_ascii_nul"
    if "CSTRING" in span.field or "ENTRY" in span.field:
        return "cstring_raw_nul"
    if span.field.startswith("mStoreHeat"):
        return "i32le_raw_word"
    if (
        span.field in LEADING_FIELDS
        or span.field in TRAILING_FLOAT_FIELDS
        or span.field in {"mShieldEfficiency", "mStealth"}
        or span.field.startswith("mStoreValue")
    ):
        return "f32le_raw_bits"
    return "i32le_raw_bits"


def render_layout_tsv(
    parsed: BattleEngineConfigFile,
    *,
    exact_baseline: bool = False,
) -> str:
    """Render compact complete record coverage without unknown payload bytes."""

    if exact_baseline:
        # Do not trust provenance metadata supplied by a caller. Reparse the
        # encoded bytes through the fixed size/hash/name/boundary gate and render
        # only that canonical exact-baseline projection.
        parsed = parse_exact_baseline(encode_config(parsed))
    file_evidence = (
        BASELINE_FILE_COUNT_EVIDENCE if exact_baseline else UNPINNED_EVIDENCE
    )
    record_evidence = RECORD_EVIDENCE if exact_baseline else UNPINNED_EVIDENCE
    unknown_falsifier = UNKNOWN_FALSIFIER if exact_baseline else UNPINNED_FALSIFIER
    output = io.StringIO(newline="")
    fieldnames = (
        "ordinal",
        "record_index",
        "record_name",
        "offset",
        "width",
        "classification",
        "field",
        "encoding",
        "evidence",
        "cheapest_falsifier",
        "frame_spans",
        "supported_spans",
        "unknown_spans",
    )
    writer = csv.DictWriter(
        output,
        fieldnames=fieldnames,
        delimiter="\t",
        lineterminator="\n",
    )
    writer.writeheader()

    def span_list(spans: tuple[ByteSpan, ...], classification: str) -> str:
        return ";".join(
            f"{span.field}@0x{span.offset:04X}+{span.width}"
            for span in spans
            if span.classification == classification
        )

    writer.writerow(
        {
            "ordinal": 0,
            "record_index": "",
            "record_name": "FILE",
            "offset": f"0x{parsed.header.offset:04X}",
            "width": parsed.header.width,
            "classification": FRAME,
            "field": parsed.header.field,
            "encoding": _span_encoding(parsed.header),
            "evidence": file_evidence,
            "cheapest_falsifier": "",
            "frame_spans": (
                f"{parsed.header.field}@0x{parsed.header.offset:04X}"
                f"+{parsed.header.width}"
            ),
            "supported_spans": "",
            "unknown_spans": "NONE",
        }
    )
    for ordinal, record in enumerate(parsed.records, start=1):
        writer.writerow(
            {
                "ordinal": ordinal,
                "record_index": record.index,
                "record_name": record.configuration_name,
                "offset": f"0x{record.offset:04X}",
                "width": record.end_offset - record.offset,
                "classification": "MIXED_RECORD",
                "field": "version_12_record",
                "encoding": "ordered_raw_spans",
                "evidence": record_evidence,
                "cheapest_falsifier": unknown_falsifier,
                "frame_spans": span_list(record.spans, FRAME),
                "supported_spans": span_list(record.spans, SUPPORTED),
                "unknown_spans": span_list(record.spans, UNKNOWN),
            }
        )
    return output.getvalue()


def render_receipt_json(parsed: BattleEngineConfigFile, source: bytes) -> str:
    """Render a deterministic, path-free proof receipt for one in-memory input."""

    encoded = encode_config(parsed)
    if encoded != source:
        raise BattleEngineConfigError(
            "cannot render receipt: parser/encoder identity check failed"
        )
    unknown_bytes = sum(
        span.width for span in parsed.spans if span.classification == UNKNOWN
    )
    unclassified_bytes = sum(
        span.width
        for span in parsed.spans
        if span.classification not in {FRAME, SUPPORTED, UNKNOWN}
    )
    receipt = {
        "coverage": {
            "coveredBytes": parsed.covered_bytes,
            "gapBytes": 0,
            "overlapBytes": 0,
            "spanCount": len(parsed.spans),
            "unclassifiedBytes": unclassified_bytes,
            "unknownBytes": unknown_bytes,
        },
        "recordCount": len(parsed.records),
        "records": [
            {
                "configurationName": record.configuration_name,
                "endOffset": f"0x{record.end_offset:04X}",
                "index": record.index,
                "offset": f"0x{record.offset:04X}",
                "version": record.version,
            }
            for record in parsed.records
        ],
        "roundTripIdentity": True,
        "roundTripSha256": hashlib.sha256(encoded).hexdigest(),
        "schema": "bea-battle-engine-config-receipt-v1",
        "sha256": hashlib.sha256(source).hexdigest(),
        "size": len(source),
    }
    return json.dumps(receipt, indent=2, sort_keys=True) + "\n"


def _canonicalize_checked_layout(data: bytes) -> bytes:
    """Normalize Git checkout newlines without hiding other byte drift.

    The renderer's contract is UTF-8 with LF terminators. A checked-out TSV may
    be CRLF on Windows, so that one transport-only rewrite is admitted. Bare CR
    remains an error instead of becoming a second ambiguous text convention.
    """

    canonical = data.replace(b"\r\n", b"\n")
    if b"\r" in canonical:
        raise BattleEngineConfigError(
            "checked layout contains a bare CR; expected canonical LF or CRLF"
        )
    return canonical


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Read and byte-exactly re-encode battle engine configurations.dat "
            "in memory; never writes the input or an installation."
        )
    )
    parser.add_argument("path", type=Path, help="input file (opened read-only)")
    parser.add_argument(
        "--exact",
        action="store_true",
        help="require the shipped 1,514-byte SHA-256 baseline and profile order",
    )
    output = parser.add_mutually_exclusive_group()
    output.add_argument(
        "--receipt",
        action="store_true",
        help="emit the deterministic path-free JSON receipt (default)",
    )
    output.add_argument(
        "--layout-tsv",
        action="store_true",
        help="emit the public-safe offset/width TSV without unknown payload bytes",
    )
    parser.add_argument(
        "--check-layout",
        type=Path,
        help="fail unless generated TSV bytes equal this tracked contract",
    )
    arguments = parser.parse_args(argv)

    try:
        data = arguments.path.read_bytes()
        parsed = parse_exact_baseline(data) if arguments.exact else parse_config(data)
        encoded = encode_config(parsed)
        if encoded != data:
            raise BattleEngineConfigError("parser/encoder identity check failed")

        layout = render_layout_tsv(parsed, exact_baseline=arguments.exact)
        if arguments.check_layout is not None:
            expected_layout = _canonicalize_checked_layout(
                arguments.check_layout.read_bytes()
            )
            actual_layout = layout.encode("utf-8")
            if actual_layout != expected_layout:
                raise BattleEngineConfigError(
                    "generated layout TSV differs from the checked contract"
                )

        if arguments.layout_tsv:
            payload = layout
        else:
            payload = render_receipt_json(parsed, data)
        # Bypass platform text-mode newline translation: CLI stdout is the same
        # canonical UTF-8/LF byte form hashed by the in-process renderers.
        sys.stdout.buffer.write(payload.encode("utf-8"))
        return 0
    except (BattleEngineConfigError, OSError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
