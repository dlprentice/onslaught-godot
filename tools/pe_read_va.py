# SPDX-License-Identifier: GPL-3.0-or-later
"""Read-only PE virtual-address reader.

Maps a virtual address to a file offset through the section table and prints the
bytes there, optionally decoded as little-endian floats/dwords. Opens the image
read-only and writes nothing.

Usage:
  python ./tools/pe_read_va.py <image> <va> [--count N] [--as float|u32|hex]
"""

from __future__ import annotations

import argparse
import struct
from pathlib import Path


class PeImage:
    def __init__(self, path: Path) -> None:
        self.data = path.read_bytes()
        if self.data[:2] != b"MZ":
            raise ValueError("not a PE image")
        pe = struct.unpack_from("<I", self.data, 0x3C)[0]
        if self.data[pe : pe + 4] != b"PE\0\0":
            raise ValueError("bad PE signature")
        n_sections = struct.unpack_from("<H", self.data, pe + 6)[0]
        opt_size = struct.unpack_from("<H", self.data, pe + 20)[0]
        self.image_base = struct.unpack_from("<I", self.data, pe + 24 + 28)[0]
        sec = pe + 24 + opt_size
        self.sections = []
        for i in range(n_sections):
            off = sec + i * 40
            name = self.data[off : off + 8].rstrip(b"\0").decode("ascii", "replace")
            vsize, vaddr, rawsize, rawptr = struct.unpack_from("<IIII", self.data, off + 8)
            self.sections.append((name, vaddr, vsize, rawptr, rawsize))

    def to_offset(self, va: int) -> int:
        rva = va - self.image_base
        for name, vaddr, vsize, rawptr, rawsize in self.sections:
            if vaddr <= rva < vaddr + max(vsize, rawsize):
                if rva - vaddr >= rawsize:
                    raise ValueError(f"VA 0x{va:08x} is in uninitialised part of {name}")
                return rawptr + (rva - vaddr)
        raise ValueError(f"VA 0x{va:08x} not in any section")

    def read(self, va: int, count: int) -> bytes:
        off = self.to_offset(va)
        return self.data[off : off + count]


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("image", type=Path)
    ap.add_argument("va", type=lambda s: int(s, 0))
    ap.add_argument("--count", type=int, default=4)
    ap.add_argument("--as", dest="fmt", choices=["float", "u32", "hex"], default="float")
    args = ap.parse_args()

    img = PeImage(args.image)
    raw = img.read(args.va, args.count)
    print(f"VA 0x{args.va:08x} -> file offset 0x{img.to_offset(args.va):08x}")
    print("hex:", raw.hex(" "))
    if args.fmt == "float":
        for i in range(0, len(raw) - 3, 4):
            print(f"  +0x{i:02x}  {struct.unpack_from('<f', raw, i)[0]!r}")
    elif args.fmt == "u32":
        for i in range(0, len(raw) - 3, 4):
            print(f"  +0x{i:02x}  0x{struct.unpack_from('<I', raw, i)[0]:08x}")


if __name__ == "__main__":
    main()
