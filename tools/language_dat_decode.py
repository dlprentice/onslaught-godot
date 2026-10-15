#!/usr/bin/env python3
"""
Decode BEA language tables: game/data/language/*.dat

These are loaded by CText__Init (0x004f21f0) from data/LANGUAGE/<lang>.DAT.

Retail files in this repo are v3:
- u32 magic      = 0xFFFFFFBB
- u32 ver_flags  = 3 (high bit currently unused in shipped .dat)
- u32 count      = 2571
- entries[count] = 0x0C bytes each:
    u32 text_id
    u32 text_off_words   (UTF-16 code units from text pool start)
    u32 audio_off_bytes  (byte offset into audio pool, or 0xFFFFFFFF)
- u32 uVar7 (language-specific offset used by the loader)
- UTF-16LE string pool (NUL-terminated strings)
- Audio C-string pool header+data (used when audio_off_bytes != 0xFFFFFFFF)

This tool can optionally correlate IDs to token names using:
  game/data/MissionScripts/text/text.stf
"""

from __future__ import annotations

import argparse
import json
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


MAGIC_V3 = 0xFFFFFFBB
ENTRY_SIZE = 0x0C


def _u32(data: bytes, off: int) -> int:
    return struct.unpack_from("<I", data, off)[0]


def _i32(data: bytes, off: int) -> int:
    return struct.unpack_from("<i", data, off)[0]


def _read_utf16z(data: bytes, off_bytes: int, max_chars: int = 8192) -> str:
    out: list[str] = []
    for i in range(max_chars):
        if off_bytes + i * 2 + 1 >= len(data):
            break
        ch = struct.unpack_from("<H", data, off_bytes + i * 2)[0]
        if ch == 0:
            break
        out.append(chr(ch))
    return "".join(out)


def _read_asciiz(data: bytes, off_bytes: int, max_len: int = 512) -> str:
    out: list[str] = []
    for i in range(max_len):
        if off_bytes + i >= len(data):
            break
        b = data[off_bytes + i]
        if b == 0:
            break
        # Keep output ASCII-safe for terminal/TSV.
        out.append(chr(b) if 0x20 <= b < 0x7F else "?")
    return "".join(out)


@dataclass(frozen=True)
class LangDatEntry:
    text_id: int
    text_off_words: int
    audio_off_bytes: int  # 0xFFFFFFFF means none


@dataclass(frozen=True)
class LangDat:
    path: Path
    ver: int
    wide_flag: bool
    count: int
    entries: list[LangDatEntry]
    text_pool_off: int
    uvar7: int
    audio_pool_off: int
    audio_pool_size: int

    # NOTE: Keep this struct purely as offsets/metadata; callers should cache the file bytes.


def parse_lang_dat(path: Path) -> LangDat:
    data = path.read_bytes()
    if len(data) < 0x0C:
        raise ValueError(f"{path}: too small to be a language .dat")

    magic = _u32(data, 0x00)
    if magic != MAGIC_V3:
        raise ValueError(f"{path}: unsupported magic 0x{magic:08X} (expected 0x{MAGIC_V3:08X})")

    ver_flags = _u32(data, 0x04)
    wide_flag = bool(ver_flags & 0x80000000)
    ver = ver_flags & 0x7FFFFFFF
    if ver not in (2, 3):
        raise ValueError(f"{path}: unsupported v{ver} (expected v2/v3)")

    count = _u32(data, 0x08)
    entries: list[LangDatEntry] = []
    entries_off = 0x0C
    need = entries_off + count * ENTRY_SIZE + 4
    if len(data) < need:
        raise ValueError(f"{path}: truncated (need >= 0x{need:X}, have 0x{len(data):X})")

    for i in range(count):
        off = entries_off + i * ENTRY_SIZE
        text_id = _u32(data, off + 0x00)
        text_off_words = _u32(data, off + 0x04)
        audio_off_bytes = _u32(data, off + 0x08)
        entries.append(LangDatEntry(text_id=text_id, text_off_words=text_off_words, audio_off_bytes=audio_off_bytes))

    uvar7_off = entries_off + count * ENTRY_SIZE
    uvar7 = _u32(data, uvar7_off)
    text_pool_off = uvar7_off + 4

    # Mirrors CText__Init(v2/v3) loader logic:
    #   iVar3 = uVar7 + count*12
    #   audio_size = *(u32*)(base + iVar3 + 0x10)
    #   audio_pool = base + iVar3 + 0x14
    iVar3 = uvar7 + count * ENTRY_SIZE
    if iVar3 + 0x14 > len(data):
        raise ValueError(f"{path}: uVar7/iVar3 points past EOF (uVar7=0x{uvar7:X}, iVar3=0x{iVar3:X})")
    audio_pool_size = _u32(data, iVar3 + 0x10)
    audio_pool_off = iVar3 + 0x14

    if audio_pool_off + audio_pool_size > len(data):
        raise ValueError(
            f"{path}: audio pool exceeds EOF (audio_pool_off=0x{audio_pool_off:X}, size=0x{audio_pool_size:X})"
        )

    return LangDat(
        path=path,
        ver=ver,
        wide_flag=wide_flag,
        count=count,
        entries=entries,
        text_pool_off=text_pool_off,
        uvar7=uvar7,
        audio_pool_off=audio_pool_off,
        audio_pool_size=audio_pool_size,
    )


_STF_DEFINE_RE = re.compile(r"^#define\s+(\S+)\s+(\d+)\s*$")


def parse_text_stf(path: Path) -> dict[int, str]:
    id_to_name: dict[int, str] = {}
    for line in path.read_text(errors="replace").splitlines():
        m = _STF_DEFINE_RE.match(line.strip())
        if not m:
            continue
        name = m.group(1)
        value = int(m.group(2), 10)
        # Prefer the first name if duplicates exist.
        id_to_name.setdefault(value, name)
    return id_to_name


def _parse_id_arg(s: str) -> int:
    s = s.strip()
    base = 16 if s.lower().startswith("0x") else 10
    return int(s, base)


def _iter_entries_filtered(
    lang: LangDat,
    ids: set[int] | None,
    only_audio: bool,
) -> Iterable[LangDatEntry]:
    for e in lang.entries:
        if ids is not None and e.text_id not in ids:
            continue
        if only_audio and e.audio_off_bytes == 0xFFFFFFFF:
            continue
        yield e


def entry_record(
    lang: LangDat,
    data: bytes,
    entry: LangDatEntry,
    *,
    id_to_name: dict[int, str] | None = None,
) -> dict[str, object]:
    text = _read_utf16z(data, lang.text_pool_off + entry.text_off_words * 2)
    audio = None
    if entry.audio_off_bytes != 0xFFFFFFFF:
        audio = _read_asciiz(data, lang.audio_pool_off + entry.audio_off_bytes)
    return {
        "id": entry.text_id,
        "hex": f"0x{entry.text_id:08X}",
        "name": (id_to_name or {}).get(entry.text_id),
        "audio": audio,
        "text": text,
        "text_off_words": entry.text_off_words,
        "audio_off_bytes": entry.audio_off_bytes,
    }


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("path", type=Path, help="Path to language .dat (e.g. game/data/language/english.dat)")
    ap.add_argument(
        "--stf",
        type=Path,
        default=Path("game/data/MissionScripts/text/text.stf"),
        help="Optional text.stf to map numeric IDs to token names (default: game/data/MissionScripts/text/text.stf)",
    )
    ap.add_argument("--no-stf", action="store_true", help="Do not load text.stf name mapping")
    ap.add_argument("--id", dest="ids", action="append", default=[], help="Filter by numeric text_id (decimal or 0xHEX)")
    ap.add_argument("--name", dest="names", action="append", default=[], help="Filter by token name from text.stf")
    ap.add_argument("--only-audio", action="store_true", help="Only show entries that have an audio name")
    ap.add_argument("--dump-tsv", action="store_true", help="Dump as TSV: id, hex, name, audio, text")
    ap.add_argument("--dump-json", action="store_true", help="Dump filtered entries as JSON")
    args = ap.parse_args(argv)

    lang = parse_lang_dat(args.path)
    data = args.path.read_bytes()
    id_to_name: dict[int, str] = {}
    name_to_id: dict[str, int] = {}
    if not args.no_stf and args.stf.exists():
        id_to_name = parse_text_stf(args.stf)
        name_to_id = {v: k for k, v in id_to_name.items()}

    ids: set[int] | None = None
    if args.ids:
        ids = {_parse_id_arg(x) for x in args.ids}

    if args.names:
        if not name_to_id:
            raise SystemExit("--name requires --stf (and the file must exist)")
        name_ids = set()
        for n in args.names:
            if n not in name_to_id:
                raise SystemExit(f"Unknown token name in text.stf: {n}")
            name_ids.add(name_to_id[n])
        ids = name_ids if ids is None else (ids & name_ids)

    if not args.dump_tsv and not args.dump_json:
        print(f"path: {lang.path}")
        print(f"size: 0x{lang.path.stat().st_size:X} ({lang.path.stat().st_size} bytes)")
        print(f"magic: 0x{MAGIC_V3:08X} ver={lang.ver} wide_flag={lang.wide_flag}")
        print(f"count: {lang.count}")
        print(f"entries: off=0x0C size=0x{lang.count * ENTRY_SIZE:X}")
        print(f"uVar7: 0x{lang.uvar7:X} (at 0x{lang.text_pool_off - 4:X})")
        print(f"text pool: off=0x{lang.text_pool_off:X} (UTF-16LE, word offsets)")
        print(f"audio pool: off=0x{lang.audio_pool_off:X} size=0x{lang.audio_pool_size:X}")
        if id_to_name:
            print(f"text.stf: {args.stf} (names={len(id_to_name)})")
        print("")

    if args.dump_tsv:
        print("id\thex\tname\taudio\ttext")

    json_rows: list[dict[str, object]] = []

    for e in _iter_entries_filtered(lang, ids=ids, only_audio=args.only_audio):
        row = entry_record(lang, data, e, id_to_name=id_to_name)
        text = row["text"]
        audio = row["audio"]
        name = row["name"]

        if args.dump_tsv:
            print(
                "{id}\t0x{id:08X}\t{name}\t{audio}\t{text}".format(
                    id=e.text_id,
                    name=name or "",
                    audio=audio or "",
                    text=text.replace("\t", "\\t").replace("\n", "\\n"),
                )
            )
        elif args.dump_json:
            json_rows.append(row)
        else:
            name_s = f" {name}" if name else ""
            audio_s = f" audio={audio}" if audio else ""
            print(f"0x{e.text_id:08X}{name_s}:{audio_s} {text}")

    if args.dump_json:
        payload = {
            "path": str(lang.path),
            "size": lang.path.stat().st_size,
            "ver": lang.ver,
            "wide_flag": lang.wide_flag,
            "count": lang.count,
            "uvar7": lang.uvar7,
            "text_pool_off": lang.text_pool_off,
            "audio_pool_off": lang.audio_pool_off,
            "audio_pool_size": lang.audio_pool_size,
            "rows": json_rows,
        }
        print(json.dumps(payload, indent=2, ensure_ascii=False))

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
