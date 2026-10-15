#!/usr/bin/env python3
"""Cases for the fonts' GetTextExtent and WordWrap (seeded): the dumped fonts and language-file texts (DIR, from
RetailOracleTests.DumpFrontEndFontsForTheOracle); each text measured in each font, and wrapped at assorted widths."""
import os, random, struct, sys
d = sys.argv[1]; rnd = random.Random(int(sys.argv[2]))
def h(x): return '%08x' % struct.unpack('<I', struct.pack('<f', x))[0]
texts = sorted(set(l.strip() for l in open(os.path.join(d, 'texts.txt')) if l.strip()))
out = [f'fontload {os.path.join(d, "font0.bin")}', f'fontload {os.path.join(d, "font1.bin")}']
for t in texts:
    for f in (0, 1):
        out.append(f'extent {f} {t}')
        for _ in range(2):
            w = rnd.choice([rnd.uniform(60, 600), float(rnd.choice([184, 284, 384, 484, 584]))])
            out.append(f'wrap {f} {h(w)} {t}')
print('\n'.join(out))
