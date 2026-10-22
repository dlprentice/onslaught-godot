#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Flatten a PE file's sections into a memory image (SizeOfImage bytes, sections at their RVAs).
Usage: mkimage.py BEA.exe OUT. The output is retail-derived: keep it in scratch, never commit it."""
import sys, hashlib, pefile
src, out = sys.argv[1], sys.argv[2]
data = open(src, 'rb').read()
print('sha256', hashlib.sha256(data).hexdigest())
pe = pefile.PE(data=data)
base = pe.OPTIONAL_HEADER.ImageBase
img = bytearray(pe.OPTIONAL_HEADER.SizeOfImage)
img[:pe.OPTIONAL_HEADER.SizeOfHeaders] = data[:pe.OPTIONAL_HEADER.SizeOfHeaders]
for s in pe.sections:
    raw = data[s.PointerToRawData:s.PointerToRawData + min(s.SizeOfRawData, s.Misc_VirtualSize or s.SizeOfRawData)]
    img[s.VirtualAddress:s.VirtualAddress + len(raw)] = raw
    print(s.Name.rstrip(b'\0').decode(), hex(base + s.VirtualAddress), hex(s.Misc_VirtualSize))
open(out, 'wb').write(img)
print('base', hex(base), 'size', hex(len(img)))
