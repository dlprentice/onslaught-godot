# SPDX-License-Identifier: GPL-3.0-or-later
"""Read-only whole-image scan for direct CALL/JMP rel32 references to a VA.

Every `E8`/`E9` byte in the image whose rel32 resolves to the target is
reported; a plausibility filter is not applied, so verify each hit by
disassembling it.

Usage:
  python ./tools/call_xref_scan.py <image> <target_va> [<target_va> ...]
"""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from pe_read_va import PeImage  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("image", type=Path)
    ap.add_argument("targets", nargs="+", type=lambda s: int(s, 0))
    args = ap.parse_args()

    img = PeImage(args.image)
    text = [s for s in img.sections if s[0] == ".text"][0]
    _name, vaddr, _vsize, rawptr, rawsize = text
    base_va = img.image_base + vaddr

    for target in args.targets:
        print(f"\n=== direct rel32 references to 0x{target:08x} ===")
        found = 0
        for off in range(rawsize - 5):
            op = img.data[rawptr + off]
            if op not in (0xE8, 0xE9):
                continue
            rel = struct.unpack_from("<i", img.data, rawptr + off + 1)[0]
            site = base_va + off
            if site + 5 + rel == target:
                kind = "CALL" if op == 0xE8 else "JMP "
                print(f"  {kind} at 0x{site:08x}")
                found += 1
        print(f"  total: {found}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
