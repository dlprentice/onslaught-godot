#!/usr/bin/env python3
"""Cases for CMeshCollisionVolume::CollideWithLine and CollideWithSphere on level 100's meshes (seeded). DIR holds the
bucket dumps and meshes.txt (RetailOracleTests' dump); each part gets a transform (identity or a rotation, and an
offset), and the line or sphere is aimed at a point inside a collision part's grid, or placed as BuildStatic does."""
import math, os, random, struct, sys
d = sys.argv[1]; rnd = random.Random(int(sys.argv[2])); N = int(sys.argv[3])
def f32(x): return struct.unpack('<f', struct.pack('<f', x))[0]
def h(x): return '%08x' % struct.unpack('<I', struct.pack('<f', f32(x)))[0]
def fl(w): return struct.unpack('<f', struct.pack('<I', int(w, 16)))[0]
names = sorted(f for f in os.listdir(d) if f.startswith('bucket') and f.endswith('.bin'))
grids = []
for n in names:
    rec = open(os.path.join(d, n), 'rb').read()[4:4 + 0xb8]
    o = struct.unpack_from('<3f', rec, 0x30); w, hh = struct.unpack_from('<2i', rec, 0x68); cxy, cz = struct.unpack_from('<2f', rec, 0x70)
    grids.append((o, (w * cxy, hh * cxy, 32 * cz)))
meshes = []
for line in open(os.path.join(d, 'meshes.txt')):
    w = line.split()[1:]; np_ = int(w[0], 16); parts = []
    for i in range(np_):
        q = w[1 + 11 * i: 12 + 11 * i]
        t = int(q[0], 16); b = int(q[3], 16); b = b if b < 0x80000000 else -1
        parts.append((t, b))
    meshes.append(parts)
usable = [i for i, m in enumerate(meshes) if any(t == 1 and b >= 0 for t, b in m) and len(m) <= 40]
def rot():
    if rnd.random() < 0.3: return [1, 0, 0, 0, 1, 0, 0, 0, 1]
    a, b, c = (rnd.uniform(-math.pi, math.pi) for _ in range(3))
    ca, sa, cb, sb, cc, sc = math.cos(a), math.sin(a), math.cos(b), math.sin(b), math.cos(c), math.sin(c)
    return [cb * cc, -cb * sc, sb, sa * sb * cc + ca * sc, -sa * sb * sc + ca * cc, -sa * cb,
            -ca * sb * cc + sa * sc, ca * sb * sc + sa * cc, ca * cb]
out = [f'pbload {os.path.join(d, n)}' for n in names] + [f'meshes {os.path.join(d, "meshes.txt")}']
def report(buildstatic=False):
    if buildstatic: return ['00000000', h(0.0), '00000000', '00000001']
    a8 = rnd.choice([0, 0, 1])
    return ['%08x' % a8, h(rnd.uniform(0, 1.5) if a8 else 0.0), '%08x' % rnd.choice([0, 1, 2, 2]), '%08x' % rnd.choice([0, 0, 0, 1])]
for _ in range(N):
    for kind in ('cline', 'csphere'):
        m = rnd.choice(usable); parts = meshes[m]
        ts = []
        shared = rnd.random() < 0.5
        base = ([f32(x) for x in rot()], [f32(rnd.uniform(-5, 5)) for _ in range(3)])
        for _p in parts:
            ts.append(base if shared else ([f32(x) for x in rot()], [f32(rnd.uniform(-5, 5)) for _ in range(3)]))
        cand = [i for i, (t, b) in enumerate(parts) if t == 1 and b >= 0]
        p = rnd.choice(cand); (o, s) = grids[parts[p][1]]
        q = [o[i] + rnd.uniform(0.05, 0.95) * s[i] for i in range(3)]
        R, T = ts[p]
        world = [R[3 * r] * q[0] + R[3 * r + 1] * q[1] + R[3 * r + 2] * q[2] + T[r] for r in range(3)]
        words = [f'{m:x}'] + [h(x) for (R_, T_) in ts for x in R_ + T_]
        size = max(s[0], s[1], 1.0)
        if kind == 'cline':
            if rnd.random() < 0.4:
                pos = [world[0], world[1], world[2] + rnd.uniform(-1, 3)]
                out.append('cline ' + ' '.join(words + [h(x) for x in pos + [0, 0, 0, 0, 0, -100, 0, 0, 0]] + report(True)))
            else:
                dirv = [rnd.uniform(-1, 1) for _ in range(3)]; L = rnd.uniform(0.5, 2) * size
                st = [world[i] - dirv[i] * L for i in range(3)]; en = [world[i] + dirv[i] * L * rnd.uniform(0.2, 1.5) for i in range(3)]
                mp = [rnd.uniform(-1, 1) for _ in range(3)]
                out.append('cline ' + ' '.join(words + [h(x) for x in mp + [0, 0, 0] + [st[i] - mp[i] for i in range(3)] + [en[i] - mp[i] for i in range(3)]] + report()))
        else:
            r = rnd.uniform(0.05, 0.4) * size
            if rnd.random() < 0.3:
                out.append('csphere ' + ' '.join(words + [h(x) for x in [0, 0, 0] + world + [1.0]] + ['00000000'] + report(True)))
            else:
                mv = [rnd.uniform(-1, 1) * size * 0.5 for _ in range(3)]
                if rnd.random() < 0.15: mv = [0.0, 0.0, 0.0]
                pos = [world[i] + mv[i] * rnd.uniform(0, 1) for i in range(3)]
                mode = 1 if rnd.random() < 0.2 else 0
                out.append('csphere ' + ' '.join(words + [h(x) for x in mv + pos + [r]] + ['%08x' % mode] + report()))
print('\n'.join(out))
