# SPDX-License-Identifier: GPL-3.0-or-later
"""Print the light state retail's cockpit draw sees, from the shipped bytes.

Read-only. Reproduces sections 3 and 6 of
`reverse-engineering/binary-analysis/cockpit-lighting-law-2026-07-26.md`.

`CEngine::SetupLights` @ `0x0044a2d0` (sole caller `0x0053e5b8`, once per frame)
reads four HFLD fields and builds the entire lighting environment from them:

  * `CHFD+0x107C` -> cached light 0 diffuse, each byte x `1/256`
    (`_DAT_005db060` = `0x3b800000`)
  * `CHFD+0x1080` -> cached light 1 diffuse, same scale
  * `CHFD+0x108C` -> the `D3DRS_AMBIENT` register shadow `0x009c68a8`
  * `CHFD+0x10A4..0x10AC` -> the sun position; the routine negates each
    component (`FCHS` at `0x0044a2f2`) and normalizes, giving
    `light0.Direction = -normalize(p)` and `light1.Direction = +normalize(p)`

At the cockpit draw the ambient channel of both lights is zero
(`ApplyCachedLight(i, 0)` at `0x0054554c`), `SetMaterial(0x0083d248)` selects a
record whose only non-zero reflectance is Diffuse `(1,1,1,1)`, and both
`D3DRS_DIFFUSEMATERIALSOURCE` and `D3DRS_AMBIENTMATERIALSOURCE` are
`D3DMCS_COLOR1` (`0x004eb32f`, `0x004eb353`). The surviving term is therefore

    C = COLOR1 x ( ambient + sum_i max(0, N.L_i) x light_i.Diffuse )

The alpha histogram at the end closes the alpha-blend mechanism: the cockpit
draw runs with `SRCALPHA`/`INVSRCALPHA` (`0x0044a650`), but the texture is
uniformly opaque, so the blend is an identity.

Usage:
    python ./tools/cockpit_light_state_probe.py [heightfield.hfld.bin] [cockpit.texture.aya]
"""

from __future__ import annotations

import collections
import math
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

DEFAULT_HEIGHT_FIELD = (
    ROOT / "rebuild" / "OnslaughtRebuild.Core" / "Assets" / "Level100"
    / "level100-heightfield.hfld.bin"
)
DEFAULT_TEXTURE = (
    ROOT / "rebuild" / "OnslaughtRebuild.Godot" / "Assets" / "Aquila" / "Textures"
    / "cockpit.texture.aya"
)

CHFD_OFFSET = 0x10
LIGHT0_COLOUR = 0x107C
LIGHT1_COLOUR = 0x1080
AMBIENT_COLOUR = 0x108C
SUN_POSITION = 0x10A4

BYTE_SCALE = 1.0 / 256.0  # _DAT_005db060 = 0x3b800000, at 0x0044a431
AMBIENT_SCALE = 1.0 / 255.0  # D3DRS_AMBIENT is a D3DCOLOR register


def channels(colour: int) -> tuple[int, int, int]:
    return ((colour >> 16) & 0xFF, (colour >> 8) & 0xFF, colour & 0xFF)


def report_lights(path: Path) -> None:
    data = path.read_bytes()

    def dword(offset: int) -> int:
        return struct.unpack_from("<I", data, CHFD_OFFSET + offset)[0]

    def vec3(offset: int) -> tuple[float, float, float]:
        return struct.unpack_from("<3f", data, CHFD_OFFSET + offset)

    light0 = channels(dword(LIGHT0_COLOUR))
    light1 = channels(dword(LIGHT1_COLOUR))
    ambient = channels(dword(AMBIENT_COLOUR))
    sun = vec3(SUN_POSITION)
    length = math.sqrt(sum(component * component for component in sun))

    print(f"height field: {path}")
    print(f"  CHFD+0x{LIGHT0_COLOUR:04X} light 0 colour   {light0}")
    print(f"  CHFD+0x{LIGHT1_COLOUR:04X} light 1 colour   {light1}")
    print(f"  CHFD+0x{AMBIENT_COLOUR:04X} D3DRS_AMBIENT    {ambient}")
    print(
        f"  CHFD+0x{SUN_POSITION:04X} sun position     "
        f"({sun[0]:.7f}, {sun[1]:.7f}, {sun[2]:.7f})  |p| = {length:.7f}"
    )
    print()
    print("  as the fixed-function pipeline sees them")
    print(
        "    light0.Diffuse  "
        + str(tuple(round(c * BYTE_SCALE, 6) for c in light0))
        + "   Direction = -normalize(p)"
    )
    print(
        "    light1.Diffuse  "
        + str(tuple(round(c * BYTE_SCALE, 6) for c in light1))
        + "   Direction = +normalize(p)"
    )
    print(
        "    D3DRS_AMBIENT   "
        + str(tuple(round(c * AMBIENT_SCALE, 6) for c in ambient))
    )
    print("    light[i].Ambient = 0 outside CDXLandscape::Render (0x0054554c)")
    print("    material[0] reflectance: Diffuse (1,1,1,1), everything else zero")


def report_texture_alpha(path: Path) -> None:
    raw = path.read_bytes()
    surface = zlib.decompress(raw[4:])
    if surface[:4] != b"DDS ":
        raise SystemExit(f"{path}: inflated payload is not a DDS surface")
    _size, _flags, height, width = struct.unpack_from("<4I", surface, 4)
    fourcc = surface[84:88].decode("ascii", "replace")
    if fourcc not in ("DXT2", "DXT3"):
        raise SystemExit(f"{path}: expected explicit-alpha DXT2/DXT3, found {fourcc}")

    histogram: collections.Counter[int] = collections.Counter()
    base = 128
    for block in range((width // 4) * (height // 4)):
        alpha = int.from_bytes(surface[base + block * 16 : base + block * 16 + 8], "little")
        for texel in range(16):
            histogram[(alpha >> (4 * texel)) & 0xF] += 1

    total = sum(histogram.values())
    print()
    print(f"texture: {path}")
    print(f"  {width}x{height} {fourcc}, top mip explicit 4-bit alpha over {total} texels")
    for value in sorted(histogram):
        share = 100.0 * histogram[value] / total
        print(f"    alpha4 = {value:2d}   {histogram[value]:8d}   {share:6.2f}%")
    if list(histogram) == [15]:
        print("  uniformly opaque: SRCALPHA/INVSRCALPHA at 0x0044a650 is an identity blend")


def main() -> int:
    height_field = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_HEIGHT_FIELD
    texture = Path(sys.argv[2]) if len(sys.argv) > 2 else DEFAULT_TEXTURE
    report_lights(height_field)
    if texture.exists():
        report_texture_alpha(texture)
    else:
        print()
        print(f"texture: {texture} not materialized; alpha histogram skipped")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
