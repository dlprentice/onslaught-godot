# SPDX-License-Identifier: GPL-3.0-or-later
"""Carry shipped static-world OBJ bytes through BOTH lighting chains and compare.

Chain A (retail): the mesh normal in BEA model space, yawed about the BEA Z
(down) axis by the authored yaw, dotted with the runtime-observed toward-sun
vector.

Chain B (reconstruction): the same OBJ normal carried through exactly the node
chain `Level100StaticWorldAsset.Load` builds -- an object root with
`Rotation = (0, yaw, 0)` and a child `MeshInstance3D` with
`RotationDegrees = (-90, 0, 0)` -- dotted with
`Level100HeightFieldAsset.SunlightDirection` the way the shader does.

Nothing here argues; every number is computed from the shipped file bytes and
the runtime-observed light state.
"""
from __future__ import annotations

import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "rebuild/OnslaughtRebuild.Godot/Assets/Level100/StaticWorld"
MANIFEST = ASSETS / "level100-static-world.json"

# local-lab/TERRAIN-LIGHT-STATE-RUNTIME-2026-07-26.md section 3: light 0's
# direction-of-travel, read out of the running safe copy at 0x009c65c0+0x14.
LIGHT0_DIRECTION = (-0.03407396, -0.90863329, +0.41620260)
LIGHT1_DIRECTION = (+0.03407396, +0.90863329, -0.41620260)
SUN_RGB = (189 / 256, 177 / 256, 121 / 256)
ANTI_RGB = (35 / 256, 35 / 256, 56 / 256)
AMBIENT_RGB = (0x0D / 255, 0x0F / 255, 0x2B / 255)  # D3DRS_AMBIENT 0x000d0f2b


def norm(v):
    n = math.sqrt(sum(c * c for c in v))
    return tuple(c / n for c in v) if n else v


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0])


def mat_vec(m, v):
    return tuple(sum(m[i][j] * v[j] for j in range(3)) for i in range(3))


def matmul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3))
                 for i in range(3))


def det3(m):
    return (m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1])
            - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0])
            + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]))


def rot_x(theta):
    """Godot Basis(Vector3.Right, theta), column-vector convention."""
    c, s = math.cos(theta), math.sin(theta)
    return ((1.0, 0.0, 0.0), (0.0, c, -s), (0.0, s, c))


def rot_y(theta):
    c, s = math.cos(theta), math.sin(theta)
    return ((c, 0.0, s), (0.0, 1.0, 0.0), (-s, 0.0, c))


def rot_z(theta):
    c, s = math.cos(theta), math.sin(theta)
    return ((c, -s, 0.0), (s, c, 0.0), (0.0, 0.0, 1.0))


# `emit_obj` (rebuild/tools/cmsh_static_preview.py:775 for positions, :738 for
# normals) writes the BEA model-space triple with its third component negated.
F = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, -1.0))
# RetailAquilaWalkerAsset.MapVector / Level100HeightFieldAsset's sun map.
MAPVECTOR = ((1.0, 0.0, 0.0), (0.0, 0.0, -1.0), (0.0, -1.0, 0.0))


def parse_obj(path: Path):
    verts, normals, faces = [], [], []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line:
            continue
        f = line.split()
        if f[0] == "v":
            verts.append((float(f[1]), float(f[2]), float(f[3])))
        elif f[0] == "vn":
            normals.append((float(f[1]), float(f[2]), float(f[3])))
        elif f[0] == "f":
            faces.append(tuple(int(p.split("/")[0]) - 1 for p in f[1:4]))
    return verts, normals, faces


def light(term_sun: float, term_anti: float, gain: float):
    return tuple(gain * (AMBIENT_RGB[k] + term_sun * SUN_RGB[k] + term_anti * ANTI_RGB[k])
                 for k in range(3))


def analyse(name: str, path: Path, yaw: float, gain: float = 2.0):
    verts, normals, faces = parse_obj(path)
    sun_bea = tuple(-c for c in LIGHT0_DIRECTION)          # toward the sun, BEA
    anti_bea = tuple(-c for c in LIGHT1_DIRECTION)
    # Reconstruction: SunlightDirection = MapVector(direction of travel).
    sunlight_direction_godot = mat_vec(MAPVECTOR, LIGHT0_DIRECTION)

    Rz = rot_z(yaw)                                        # retail object yaw
    chainB = matmul(rot_y(yaw), rot_x(math.radians(-90.0)))  # Godot node chain

    worst_nl = 0.0
    worst_rgb = 0.0
    up = down = 0
    for n_obj in normals:
        n_bea = norm(mat_vec(F, n_obj))
        n_world_bea = mat_vec(Rz, n_bea)
        a_sun = max(0.0, dot(n_world_bea, sun_bea))
        a_anti = max(0.0, dot(n_world_bea, anti_bea))
        La = light(a_sun, a_anti, gain)

        n_god = norm(mat_vec(chainB, n_obj))
        b_sun = max(0.0, dot(n_god, tuple(-c for c in sunlight_direction_godot)))
        b_anti = max(0.0, dot(n_god, sunlight_direction_godot))
        Lb = light(b_sun, b_anti, gain)

        worst_nl = max(worst_nl, abs(dot(n_world_bea, sun_bea) - dot(n_god, tuple(-c for c in sunlight_direction_godot))))
        worst_rgb = max(worst_rgb, max(abs(La[k] - Lb[k]) for k in range(3)))
        # "up" in BEA is -Z (Z is down).
        if n_bea[2] < -0.5:
            up += 1
        elif n_bea[2] > 0.5:
            down += 1

    # winding vs stored normal, in the shipped OBJ frame
    agree = disagree = degen = 0
    for i0, i1, i2 in faces:
        a, b, c = verts[i0], verts[i1], verts[i2]
        g = cross(tuple(b[k] - a[k] for k in range(3)),
                  tuple(c[k] - a[k] for k in range(3)))
        if math.sqrt(sum(x * x for x in g)) < 1e-12:
            degen += 1
            continue
        navg = tuple(sum(normals[i][k] for i in (i0, i1, i2)) / 3 for k in range(3))
        if dot(g, navg) >= 0:
            agree += 1
        else:
            disagree += 1

    print(f"{name:26s} n={len(normals):5d} faces={len(faces):5d} yaw={yaw:+.5f} "
          f"maxdN.L={worst_nl:.3e} maxdRGB={worst_rgb:.3e} "
          f"BEAup={up:4d} BEAdown={down:4d} wind+={agree:5d} wind-={disagree:5d} degen={degen}")
    return worst_nl, worst_rgb


def main():
    print("=== chain algebra ===")
    chain = matmul(rot_x(math.radians(-90.0)), F)
    print("Rx(-90) * F =")
    for r in chain:
        print("   " + "  ".join(f"{v:+.17g}" for v in r))
    print(f"   det = {det3(chain):+.17g}")
    print("MapVector    =")
    for r in MAPVECTOR:
        print("   " + "  ".join(f"{v:+.17g}" for v in r))
    print("max |Rx(-90)*F - MapVector| = "
          f"{max(abs(chain[i][j] - MAPVECTOR[i][j]) for i in range(3) for j in range(3)):.3e}")

    # conjugation identity: M * Rz(phi) * M  ==  Ry(phi)?
    worst = 0.0
    for deg in range(0, 360, 7):
        phi = math.radians(deg)
        conj = matmul(MAPVECTOR, matmul(rot_z(phi), MAPVECTOR))
        ry = rot_y(phi)
        worst = max(worst, max(abs(conj[i][j] - ry[i][j]) for i in range(3) for j in range(3)))
    print(f"max |M Rz(phi) M - Ry(phi)| over 52 angles = {worst:.3e}")

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    print("\n=== per authored world object: retail chain vs Godot chain ===")
    worst_nl = worst_rgb = 0.0
    for obj in sorted(manifest["objects"], key=lambda o: o["ordinal"]):
        mesh = obj["mesh"]
        path = ASSETS / "Meshes" / (mesh.replace("_", "-") + ".obj")
        if not path.exists():
            cands = list((ASSETS / "Meshes").glob("*.obj"))
            match = [c for c in cands if c.stem.replace("-", "") == mesh.replace("_", "").lower()]
            if not match:
                print(f"  MISSING obj for mesh '{mesh}'")
                continue
            path = match[0]
        a, b = analyse(f"{obj['ordinal']:02d} {mesh}", path, float(obj["yaw"]))
        worst_nl = max(worst_nl, a)
        worst_rgb = max(worst_rgb, b)

    print("\n=== pine close meshes (no yaw; Basis(Right, -pi/2)) ===")
    for variant in range(4):
        a, b = analyse(f"pinesnow{variant}", ASSETS / "Meshes" / f"pinesnow{variant}.obj", 0.0)
        worst_nl = max(worst_nl, a)
        worst_rgb = max(worst_rgb, b)

    print(f"\nWORST over every shipped static-world normal: "
          f"|dN.L| = {worst_nl:.3e}, |dRGB| = {worst_rgb:.3e}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
