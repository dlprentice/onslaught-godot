# SPDX-License-Identifier: GPL-3.0-or-later
"""Compare a retail frame against a reconstruction frame over the cockpit's
*intersection mask* — the pixels where the same cockpit surface point is the
visible one in both images — instead of over a rectangular region box.

Why this exists
---------------
`local-lab/COCKPIT-DISPLACEMENT-EVIDENCE-2026-07-26.md` showed that a region
mean over the cockpit boxes averages two different objects together, and
`local-lab/COCKPIT-NORMAL-SIGN-2026-07-26.md` showed that the `B/R = 0.631`
inference drawn from such a mean is outside the shipped lighting law's feasible
range.  This probe removes the contamination geometrically rather than by a
luminance heuristic.

Two z-buffered rasters of the shipped `m_cockpit2.msh.aya` are built at 640x480:

  RETAIL  cockpit-local -> world through the runtime-measured cockpit root
          rotation, then world -> view through the captured `D3DTS_VIEW`, then
          the captured projection shadow (`diag(1, 4/3)`, hfov 90 at 4:3).
  BUILD   cockpit-local -> Godot camera space through
          `RetailAquilaWalkerAsset.MapVector` = `(x, -z, -y)` with
          `Root.Position = Vector3.Zero`, then the same projection
          (`Camera3D.Fov = 73.739795`, `tan(vhalf) = 0.75`).

Both record per pixel which (part, triangle) is in front plus the barycentric
weights.  A sample is admitted only when the *same surface point* is visible in
both rasters, so the texel and the per-vertex `COLOR1` are identical on the two
sides of the ratio and cancel exactly.

Nothing is launched; only the two PNGs and the shipped mesh are read.

Usage
-----
    python ./tools/cockpit_intersection_mask_probe.py RETAIL.png BUILD.png
    python ./tools/cockpit_intersection_mask_probe.py RETAIL.png BUILD.png --scale 0.5
"""
from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "rebuild" / "tools"))

from cmsh_static_preview import inflate_aya, parse_cmsh_stream  # noqa: E402

MESH = ROOT / "rebuild/OnslaughtRebuild.Godot/Assets/Aquila/Source/m_cockpit2.msh.aya"
W, H = 640, 480

# Captured at the Level 100 cockpit draw under CDB on the verified safe copy;
# see local-lab/COCKPIT-WORLD-MATRIX-RUNTIME-2026-07-26.md.
BATCH0 = ((0.88662648, 0.46089223, -0.03836670),
          (-0.46159714, 0.88701338, -0.01164191),
          (0.02866611, 0.02803198, 0.99919587))
BATCH0_T = (288.67752000, 243.25581000, -12.27214300)
VIEW = ((0.87282735, -0.00000015, -0.48802879),
        (0.48802879, 0.00000027, 0.87282735),
        (0.00000000, -0.99999994, 0.00000031))
VIEW_T = (-370.68732000, -12.11152000, -71.42742000)

# Shipped level100-heightfield.hfld.bin lighting constants.
SUN = (0.0340740, 0.9086333, -0.4162026)
AMB = (13 / 255, 15 / 255, 43 / 255)
SUNC = (189 / 256, 177 / 256, 121 / 256)
ANTIC = (35 / 256, 35 / 256, 56 / 256)

DRAW_ORDER = [1, 11, 12, 13, 14, 15, 18]   # the seven drawing CMSP parts


def _norm(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v) if n else v


def _dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def _matmul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3))
                 for i in range(3))


def _mat_vec(m, v):
    return tuple(sum(m[i][j] * v[j] for j in range(3)) for i in range(3))


def _rowvec_mat(v, m):
    return tuple(sum(v[i] * m[i][j] for i in range(3)) for j in range(3))


def _transpose(m):
    return tuple(tuple(m[j][i] for j in range(3)) for i in range(3))


def load_geometry():
    mesh = parse_cmsh_stream(inflate_aya(MESH.read_bytes()), hierarchy_frame=25)
    parts = mesh.parts
    locals_ = []
    for p in parts:
        t = p.track
        h = t.hierarchy[t.frame_map[min(25, len(t.frame_map) - 1)]]
        locals_.append((h.rows, h.position))
    glob: list = [None] * len(parts)
    for i, p in enumerate(parts):
        R, T = locals_[i]
        if p.parent is None:
            glob[i] = (R, T)
        else:
            PR, PT = glob[p.parent]
            glob[i] = (_matmul(PR, R),
                       tuple(a + b for a, b in zip(_mat_vec(PR, T), PT)))
    return parts, glob


def build_projections(parts, glob):
    """Return (project_retail, project_build, A, root_camera_distance, angle_deg)."""
    Mroot = _matmul(glob[1][0], BATCH0)
    rootT = tuple(BATCH0_T[k] - _rowvec_mat(glob[1][1], Mroot)[k] for k in range(3))
    camw = tuple(-_rowvec_mat(VIEW_T, _transpose(VIEW))[k] for k in range(3))
    RC = _matmul(Mroot, VIEW)
    off = _rowvec_mat(tuple(rootT[k] - camw[k] for k in range(3)), VIEW)
    PERM = ((1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, -1.0, 0.0))
    tr = sum(_matmul(RC, _transpose(PERM))[i][i] for i in range(3))
    angle = math.degrees(math.acos(max(-1.0, min(1.0, (tr - 1) / 2))))

    def project_retail(p):
        v = _rowvec_mat(p, RC)
        x, y, z = v[0] + off[0], v[1] + off[1], v[2] + off[2]
        if z <= 0.001:
            return None
        return ((x / z + 1.0) * 0.5 * W, (1.0 - (4.0 / 3.0) * y / z) * 0.5 * H, z)

    def project_build(p):
        x, y, z = p[0], -p[2], p[1]
        if z <= 0.001:
            return None
        return ((x / z + 1.0) * 0.5 * W, (1.0 - (4.0 / 3.0) * y / z) * 0.5 * H, z)

    return (project_retail, project_build, _transpose(Mroot),
            math.dist(rootT, camw), angle)


def collect_triangles(parts, glob, A):
    """Cockpit-local triangles plus per-vertex (a_sun, a_anti) in retail world space."""
    L0 = _norm(SUN)
    tris = []
    light = {}
    for pi in DRAW_ORDER:
        p = parts[pi]
        R, T = glob[pi]
        pos = [tuple(_mat_vec(R, v.position)[t] + T[t] for t in range(3))
               for v in p.vertices]
        lit = []
        for v in p.vertices:
            d = _dot(_norm(_mat_vec(A, _mat_vec(R, _norm(v.normal)))), L0)
            lit.append((max(0.0, d), max(0.0, -d)))
        light[pi] = (lit, pos)
        tid = 0
        for g in p.groups:
            idx = g.indices
            for k in range(len(idx) - 2):
                i0, i1, i2 = idx[k], idx[k + 1], idx[k + 2]
                if len({i0, i1, i2}) != 3:
                    tid += 1
                    continue
                if k & 1:
                    i0, i1 = i1, i0
                tris.append(((pi, tid, i0, i1, i2), (pos[i0], pos[i1], pos[i2])))
                tid += 1
    return tris, light


def raster(tris, project):
    zbuf = [1e30] * (W * H)
    kbuf: list = [None] * (W * H)
    bbuf: list = [None] * (W * H)
    for key, (a, b, c) in tris:
        pr = (project(a), project(b), project(c))
        if any(q is None for q in pr):
            continue
        (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = pr
        area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
        if abs(area) < 1e-9:
            continue
        minx, maxx = max(0, int(min(x0, x1, x2))), min(W - 1, int(max(x0, x1, x2)) + 1)
        miny, maxy = max(0, int(min(y0, y1, y2))), min(H - 1, int(max(y0, y1, y2)) + 1)
        for py in range(miny, maxy + 1):
            fy = py + 0.5
            row = py * W
            for px in range(minx, maxx + 1):
                fx = px + 0.5
                w0 = ((x1 - fx) * (y2 - fy) - (x2 - fx) * (y1 - fy)) / area
                if w0 < 0:
                    continue
                w1 = ((x2 - fx) * (y0 - fy) - (x0 - fx) * (y2 - fy)) / area
                if w1 < 0:
                    continue
                w2 = 1.0 - w0 - w1
                if w2 < 0:
                    continue
                z = w0 * z0 + w1 * z1 + w2 * z2
                o = row + px
                if z < zbuf[o]:
                    zbuf[o], kbuf[o], bbuf[o] = z, key, (w0, w1, w2)
    return kbuf, bbuf


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("retail", type=Path)
    ap.add_argument("build", type=Path)
    ap.add_argument("--scale", type=float, default=1.0,
                    help="multiply the build pixel by this before differencing "
                         "(0.5 removes the shader's MODULATE2X factor)")
    args = ap.parse_args(argv)

    from PIL import Image
    retail_img = Image.open(args.retail).convert("RGB")
    build_img = Image.open(args.build).convert("RGB")
    if retail_img.size != (W, H) or build_img.size != (W, H):
        raise SystemExit(f"both images must be {W}x{H}")
    rpx, bpx = retail_img.load(), build_img.load()

    parts, glob = load_geometry()
    proj_r, proj_b, A, rootdist, angle = build_projections(parts, glob)
    print(f"cockpit root to camera: {rootdist:.6f} units")
    print(f"retail composition vs the build permutation: {angle:.4f} deg")

    tris, light = collect_triangles(parts, glob, A)
    kr, br = raster(tris, proj_r)
    kb, _ = raster(tris, proj_b)
    print(f"triangles={len(tris)}  retail coverage={sum(k is not None for k in kr)} px"
          f"  build coverage={sum(k is not None for k in kb)} px")

    samples = []
    for o in range(W * H):
        key = kr[o]
        if key is None:
            continue
        pi, _tid, i0, i1, i2 = key
        w0, w1, w2 = br[o]
        lit, pos = light[pi]
        p3 = tuple(w0 * pos[i0][t] + w1 * pos[i1][t] + w2 * pos[i2][t] for t in range(3))
        q = proj_b(p3)
        if q is None:
            continue
        bx, by = int(q[0]), int(q[1])
        if not (0 <= bx < W and 0 <= by < H) or kb[by * W + bx] != key:
            continue
        samples.append((
            rpx[o % W, o // W], bpx[bx, by], pi,
            w0 * lit[i0][0] + w1 * lit[i1][0] + w2 * lit[i2][0],
            w0 * lit[i0][1] + w1 * lit[i1][1] + w2 * lit[i2][1]))

    n = len(samples)
    if n == 0:
        raise SystemExit("no intersection samples")
    k = args.scale
    mr = [sum(s[0][c] for s in samples) / n for c in range(3)]
    mb = [sum(s[1][c] * k for s in samples) / n for c in range(3)]
    a_sun = sum(s[3] for s in samples) / n
    a_anti = sum(s[4] for s in samples) / n
    Lp = [AMB[c] + a_sun * SUNC[c] + a_anti * ANTIC[c] for c in range(3)]
    ratio = [mr[c] / mb[c] for c in range(3)]
    Li = [Lp[c] * ratio[c] * k for c in range(3)]
    diffs = [sum(abs(s[0][c] - k * s[1][c]) for c in range(3)) for s in samples]

    print(f"\n=== intersection mask: {n} px (same surface point visible in both) ===")
    print(f"  retail mean RGB   = ({mr[0]:6.2f},{mr[1]:6.2f},{mr[2]:6.2f})  B/R={mr[2]/mr[0]:.3f}")
    print(f"  build  mean RGB   = ({mb[0]:6.2f},{mb[1]:6.2f},{mb[2]:6.2f})  B/R={mb[2]/mb[0]:.3f}"
          f"   (build x {k})")
    print(f"  a_sun={a_sun:.4f}  a_anti={a_anti:.4f}")
    print(f"  predicted L       = ({Lp[0]:.4f},{Lp[1]:.4f},{Lp[2]:.4f})  B/R={Lp[2]/Lp[0]:.3f}")
    print(f"  retail/build      = ({ratio[0]:.4f},{ratio[1]:.4f},{ratio[2]:.4f})")
    print(f"  IMPLIED retail L  = ({Li[0]:.4f},{Li[1]:.4f},{Li[2]:.4f})  B/R={Li[2]/Li[0]:.3f}")
    print(f"  feasible B/R range of the shipped law = [0.813, 3.308]")
    print(f"  meanD={sum(diffs)/n/3:.2f}  material(sum|d|>24)={sum(1 for d in diffs if d>24)/n*100:.2f}%")

    print("\n  per part:")
    for pi in DRAW_ORDER:
        sel = [s for s in samples if s[2] == pi]
        if not sel:
            continue
        m = len(sel)
        pr = [sum(s[0][c] for s in sel) / m for c in range(3)]
        pb = [sum(s[1][c] * k for s in sel) / m for c in range(3)]
        print(f"    {parts[pi].name:14s} n={m:6d} "
              f"retail=({pr[0]:6.2f},{pr[1]:6.2f},{pr[2]:6.2f}) "
              f"build=({pb[0]:6.2f},{pb[1]:6.2f},{pb[2]:6.2f}) "
              f"ratio=({pr[0]/max(pb[0],1e-6):.3f},{pr[1]/max(pb[1],1e-6):.3f},"
              f"{pr[2]/max(pb[2],1e-6):.3f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
