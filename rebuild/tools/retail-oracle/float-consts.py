#!/usr/bin/env python3
"""Per retail function: each x87 instruction reading a constant in memory (width, address, value). Reads the oracle
image (retail-derived, scratch only). Function ends: the first ret followed by int3/nop padding."""
import re, struct, subprocess, sys
IMG = open('$SCRATCH', 'rb').read()
EXE = '~/.local/share/Steam/steamapps/common/Battle Engine Aquila/BEA.exe'
def val(width, a):
    o = a - 0x400000
    if width == 'DWORD': return struct.unpack_from('<f', IMG, o)[0]
    if width == 'QWORD': return struct.unpack_from('<d', IMG, o)[0]
    return None
for addr in sys.argv[1:]:
    a = int(addr, 16)
    out = subprocess.run(['objdump', '-d', '-M', 'intel', f'--start-address={a:#x}', f'--stop-address={a + 0x1800:#x}', EXE],
                         capture_output=True, text=True).stdout.splitlines()
    print(f'== {a:08x}')
    prev_ret = False
    for line in out:
        m = re.match(r'\s*([0-9a-f]+):\s+(?:[0-9a-f]{2} )+\s*(.*)', line)
        if not m: continue
        ip, ins = int(m.group(1), 16), m.group(2).strip()
        if prev_ret and (ins.startswith('int3') or ins.startswith('nop') or ins.startswith('lea    esi,[esi')): break
        prev_ret = ins.startswith('ret')
        c = re.search(r'(DWORD|QWORD|TBYTE) PTR ds:0x([0-9a-f]+)', ins)
        if ins.startswith('f') and c:
            print(f'  {ip:08x} {ins:44s} {c.group(1)} {val(c.group(1), int(c.group(2), 16))!r}')
