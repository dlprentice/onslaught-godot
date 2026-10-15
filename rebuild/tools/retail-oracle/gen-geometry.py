#!/usr/bin/env python3
"""Cases for the retail oracle's Geometry functions (seeded): mostly constructed hits and near misses, some random."""
import random, struct, sys

rnd = random.Random(int(sys.argv[1]) if len(sys.argv) > 1 else 20261015)
N = int(sys.argv[2]) if len(sys.argv) > 2 else 2000


def f32(x):
    return struct.unpack('<f', struct.pack('<f', x))[0]


def h(x):
    return '%08x' % struct.unpack('<I', struct.pack('<f', x))[0]


def v(scale=10.0):
    return [f32(rnd.uniform(-scale, scale)) for _ in range(3)]


def tri(scale=10.0):
    a = v(scale)
    b = [a[i] + f32(rnd.uniform(-scale, scale)) for i in range(3)]
    c = [a[i] + f32(rnd.uniform(-scale, scale)) for i in range(3)]
    return a, b, c


def inside(a, b, c):
    u, w = rnd.random(), rnd.random()
    if u + w > 1:
        u, w = 1 - u, 1 - w
    return [a[i] + u * (b[i] - a[i]) + w * (c[i] - a[i]) for i in range(3)]


def report():
    a8 = rnd.choice([0, 0, 1])
    return ['%08x' % a8, h(f32(rnd.uniform(0, 1.5)) if a8 else 0.0), '%08x' % rnd.choice([0, 1]), '%08x' % rnd.choice([0, 1])]


out = []
for _ in range(N):
    a, b, c = tri()
    p = inside(a, b, c)
    d = v(5.0)
    s = [f32(p[i] + d[i]) for i in range(3)]
    e = [f32(p[i] - d[i] * rnd.uniform(0.2, 3)) for i in range(3)]
    if rnd.random() < 0.3:
        s, e = e, s
    out.append('TestSegmentAgainstTriangle ' + ' '.join(map(h, a + b + c + s + e)) + ' ' + ' '.join(report()))

    a, b, c = tri()
    p = inside(a, b, c)
    d = v(3.0)
    radius = f32(rnd.uniform(0.1, 3))
    pos = [f32(p[i] + d[i]) for i in range(3)]
    move = [f32(-d[i] * rnd.uniform(0.1, 2.5)) for i in range(3)]
    out.append('TestSweptSphereAgainstTriangle ' + ' '.join(map(h, a + b + c + pos + move + [radius])) + ' ' + ' '.join(report()))

    a, b, c = tri()
    pt = inside(a, b, c) if rnd.random() < 0.6 else v()
    nrm = v(1.0)
    out.append('PointInsideTrianglePrism ' + ' '.join(map(h, pt + a + b + c + nrm)))
    out.append('SelectClosestPointOnTriangleEdges ' + ' '.join(map(h, a + b + c + v())))
    out.append('RaySphereEntryDistance ' + ' '.join(map(h, v() + v() + [f32(rnd.uniform(0.1, 8))])))
    out.append('DistanceOutsideBox ' + ' '.join(map(h, v() + v(4.0))))
    lo = v(5.0)
    hi = [f32(lo[i] + rnd.uniform(0.5, 10)) for i in range(3)]
    out.append('ClipSegmentAgainstAABB3D ' + ' '.join(map(h, v(12.0) + v(12.0) + [lo[0], lo[1], hi[0], hi[1], lo[2], hi[2]])))

print('\n'.join(out))
