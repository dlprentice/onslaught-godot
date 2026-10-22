#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Cases for the poly bucket searches: per dumped bucket (in order), lines through and around its grid and spheres in it."""
import os, random, struct, sys
d = sys.argv[1]; rnd = random.Random(int(sys.argv[2])); per = int(sys.argv[3])
def h(x): return '%08x' % struct.unpack('<I', struct.pack('<f', x))[0]
names = sorted(f for f in os.listdir(d) if f.startswith('bucket') and f.endswith('.bin'))
lines = [f'pbload {os.path.join(d, n)}' for n in names]
for i, n in enumerate(names):
    rec = open(os.path.join(d, n), 'rb').read()[4:4 + 0xb8]
    ox, oy, oz = struct.unpack_from('<3f', rec, 0x30)
    w, hgt = struct.unpack_from('<2i', rec, 0x68)
    cxy, cz = struct.unpack_from('<2f', rec, 0x70)
    sx, sy, sz = w * cxy, hgt * cxy, 32 * cz
    def pt(m=0.2):
        p = [ox + rnd.uniform(-m, 1 + m) * sx, oy + rnd.uniform(-m, 1 + m) * sy, oz + rnd.uniform(-m, 1 + m) * sz]
        if rnd.random() < 0.3:  # on a cell border (x, y or both)
            if rnd.random() < 0.7: p[0] = ox + rnd.randint(-1, w + 1) * cxy
            if rnd.random() < 0.7: p[1] = oy + rnd.randint(-1, hgt + 1) * cxy
        return p
    for _ in range(per):
        a, b = pt(), pt()
        r0 = rnd.random()
        if r0 < 0.1: b[0] = a[0]          # vertical in x
        elif r0 < 0.2: b[1] = a[1]
        elif r0 < 0.25: b = list(a)      # degenerate
        elif r0 < 0.35: dd = b[0] - a[0]; b[1] = a[1] + dd * rnd.choice([1, -1])  # exact diagonal
        stop = 1 if rnd.random() < 0.2 else 0
        lines.append(f'pbline {i:x} ' + ' '.join(h(x) for x in a + b) + f' {stop:x}')
        c = pt(0.05)
        r = cxy * rnd.uniform(0.2, 2.5)
        lines.append(f'pbsphere {i:x} ' + ' '.join(h(x) for x in c + [r]))
print('\n'.join(lines))
