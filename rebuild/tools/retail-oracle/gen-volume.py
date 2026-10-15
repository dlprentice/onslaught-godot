#!/usr/bin/env python3
"""Cases for CMeshCollisionVolume's tests (seeded): ResolveContact, the swept sphere against a part's box and against
its poly bucket (bucket dumps in DIR, loaded first, as gen-buckets.py)."""
import math, os, random, struct, sys
d = sys.argv[1]; rnd = random.Random(int(sys.argv[2])); N = int(sys.argv[3])
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def h(x): return '%08x' % struct.unpack('<I', struct.pack('<f', f32(x)))[0]
def v(s=10.0): return [f32(rnd.uniform(-s, s)) for _ in range(3)]
def unit():
    while True:
        a = v(1.0); m = math.sqrt(sum(x * x for x in a))
        if m > 0.1: return [f32(x / m) for x in a]
def report():
    a8 = rnd.choice([0, 0, 1])
    return ['%08x' % a8, h(rnd.uniform(0, 1.5) if a8 else 0.0), '%08x' % rnd.choice([0, 1, 2]), '%08x' % rnd.choice([0, 0, 1])]
names = sorted(f for f in os.listdir(d) if f.startswith('bucket') and f.endswith('.bin'))
out = [f'pbload {os.path.join(d, n)}' for n in names]
grids = []
for n in names:
    rec = open(os.path.join(d, n), 'rb').read()[4:4 + 0xb8]
    o = struct.unpack_from('<3f', rec, 0x30); w, hh = struct.unpack_from('<2i', rec, 0x68); cxy, cz = struct.unpack_from('<2f', rec, 0x70)
    grids.append((o, (w * cxy, hh * cxy, 32 * cz)))
for _ in range(N):
    # ResolveContact
    n = rnd.choice([0, 0, 1, 1, 2, 3])
    pos = v(); travel = v(3.0)
    cpos = [f32(pos[i] + rnd.uniform(-2, 2)) for i in range(3)]
    tn = unit()
    if rnd.random() < 0.4: tn[2] = f32(rnd.uniform(-0.95, 0.0))
    normals = []
    for k in range(6):
        if k > 0 and rnd.random() < 0.15: normals.append(list(normals[rnd.randrange(k)]))
        elif rnd.random() < 0.5:  # facing the travel, so the slides are blocked and the plane loop decides
            m = math.sqrt(sum(x * x for x in travel)) or 1.0
            a = [-x / m + rnd.uniform(-0.6, 0.6) for x in travel]; am = math.sqrt(sum(x * x for x in a)) or 1.0
            normals.append([f32(x / am) for x in a])
        else: normals.append(unit())
    if rnd.random() < 0.05: travel = [0.0, 0.0, 0.0]
    stuck = 1 if rnd.random() < 0.3 else 0
    tt = 1 if rnd.random() < 0.1 else 2
    dist = rnd.choice([rnd.uniform(-0.5, 2), 0.01, rnd.uniform(0, 0.02)])
    out.append('resolve %08x %08x ' % (stuck, n) + ' '.join(map(h, [dist] + cpos)) + ' %08x ' % tt
               + ' '.join(map(h, tn + sum(normals, []) + pos + travel)))
    # the box
    origin = v(3.0); axes = [f32(rnd.uniform(0.2, 5)) for _ in range(3)]
    p = [origin[i] + rnd.uniform(-1.5, 1.5) * (axes[i] + 2) for i in range(3)]
    m = v(3.0); r = rnd.uniform(0.1, 2)
    out.append('sbox ' + ' '.join(map(h, p + m + [r] + origin + axes)) + ' ' + ' '.join(report()))
    # the part's triangles
    b = rnd.randrange(len(names)); (o, s) = grids[b]
    p = [o[i] + rnd.uniform(-0.1, 1.1) * s[i] for i in range(3)]
    m = [rnd.uniform(-0.3, 0.3) * max(s[0], s[1]) for _ in range(3)]
    if rnd.random() < 0.3: m = [0.0, 0.0, 0.0]
    r = rnd.uniform(0.1, 0.3) * max(s[0], s[1])
    out.append(f'spart {b:x} ' + ' '.join(map(h, p + m + [r])) + ' ' + ' '.join(report()))
print('\n'.join(out))
