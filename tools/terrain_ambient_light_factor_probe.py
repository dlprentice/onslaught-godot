# SPDX-License-Identifier: GPL-3.0-or-later
"""Predict retail's flat terrain factor from the D3D9 fixed-function ambient term.

`CDXLandscape::Render` draws the terrain with:

  * `D3DRS_AMBIENT = 0`                                  (`0x005454db`)
  * `SetMaterial(0x0083d28c)` — the terrain-only record   (`0x005454f4`, vtable +0xc4)
    whose Diffuse is black, Ambient is 0.8 grey and Emissive is zero
    (written by the sole initialiser at `0x004eb9a0`)
  * every enabled cached light re-uploaded with `enabled = 1`, which is the
    argument that copies the light colour into `D3DLIGHT9.Ambient`
    (`CDXEngine::ApplyCachedLight` @ `0x00551200`, stores at `0x005512be`)
  * stage 0/1 `D3DTSS_COLOROP = D3DTOP_MODULATE2X` while `USE_MODULATE_2X` is set
    (`0x00513af0`), with `COLORARG1 = TEXTURE`, `COLORARG2 = DIFFUSE`
  * `D3DRS_LIGHTING` left enabled, because `RenderTerrain` only clears it when
    `LANDSCAPE_LIGHTING == 0` and that CVar defaults to 1

The terrain vertex stride is 0x14 (position + one UV pair) and carries no normal,
so the N.L diffuse term is zero and the only surviving fixed-function term is

    vertex_diffuse = Emissive + Ambient_material * (D3DRS_AMBIENT + sum light.Ambient)
                   = 0.8 * sum light.Ambient

which is constant over the whole terrain. MODULATE2X then doubles it, giving a
flat multiplicative colour applied outside the macro cache and outside the
shipped texture stages:

    factor = 2 * 0.8 * sum(light colour) / 256

`CEngine::SetupLights` @ `0x0044a2d0` enables exactly lights 0 and 1
(`0x009c68a0`/`0x009c68a1` := 1, `0x009c68a2..a7` := 0) and fills them from HFLD
`CHFD+0x107C` and `CHFD+0x1080`, each byte scaled by `_DAT_005db060`
= `0x3b800000` = 1/256.

Usage:
    python ./tools/terrain_ambient_light_factor_probe.py [heightfield.hfld.bin]
"""

from __future__ import annotations

import struct
import sys
from pathlib import Path

DEFAULT_HEIGHT_FIELD = (
    Path(__file__).resolve().parents[1]
    / "rebuild"
    / "OnslaughtRebuild.Core"
    / "Assets"
    / "Level100"
    / "level100-heightfield.hfld.bin"
)

CHFD_OFFSET = 0x10
LIGHT0_COLOUR_FIELD = 0x107C  # sun, becomes cached light 0
LIGHT1_COLOUR_FIELD = 0x1080  # second light, becomes cached light 1
AMBIENT_COLOUR_FIELD = 0x108C  # D3DRS_AMBIENT register - explicitly zeroed for terrain

BYTE_SCALE = 1.0 / 256.0  # _DAT_005db060 = 0x3b800000
MATERIAL_AMBIENT = 0.8  # 0x3f4ccccd at 0x0083d28c + 0x10
MODULATE2X = 2.0

# reverse-engineering/binary-analysis/terrain-implied-macro-inversion-2026-07-26.md
MEASURED_FACTOR = (1.457, 1.389, 1.147)


def channels(colour: int) -> tuple[int, int, int]:
    return ((colour >> 16) & 0xFF, (colour >> 8) & 0xFF, colour & 0xFF)


def main() -> int:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_HEIGHT_FIELD
    data = path.read_bytes()

    def field(offset: int) -> int:
        return struct.unpack_from("<I", data, CHFD_OFFSET + offset)[0]

    light0 = channels(field(LIGHT0_COLOUR_FIELD))
    light1 = channels(field(LIGHT1_COLOUR_FIELD))
    register_ambient = channels(field(AMBIENT_COLOUR_FIELD))

    total = tuple(light0[i] + light1[i] for i in range(3))
    sum_light_ambient = tuple(v * BYTE_SCALE for v in total)
    predicted = tuple(MODULATE2X * MATERIAL_AMBIENT * v for v in sum_light_ambient)

    print(f"source                    {path}")
    print(f"light 0  CHFD+0x107C      {light0}")
    print(f"light 1  CHFD+0x1080      {light1}")
    print(f"D3DRS_AMBIENT CHFD+0x108C {register_ambient}   (zeroed at 0x005454db)")
    print()
    print(f"sum of enabled light colours          {total}")
    print("sum light.Ambient  = sum/256          ("
          + ", ".join(f"{v:.4f}" for v in sum_light_ambient) + ")")
    print("x material ambient 0.8 x MODULATE2X   ("
          + ", ".join(f"{v:.4f}" for v in predicted) + ")")
    print("measured implied/ours factor          ("
          + ", ".join(f"{v:.4f}" for v in MEASURED_FACTOR) + ")")
    print()
    print(f"{'':34s}{'R':>9s}{'G':>9s}{'B':>9s}")
    err = tuple(100.0 * (predicted[i] / MEASURED_FACTOR[i] - 1.0) for i in range(3))
    print("magnitude error vs measured       "
          + "".join(f"{v:>+8.1f}%" for v in err))
    pc = tuple(v / predicted[2] for v in predicted)
    mc = tuple(v / MEASURED_FACTOR[2] for v in MEASURED_FACTOR)
    print("predicted, normalised to blue     " + "".join(f"{v:>9.4f}" for v in pc))
    print("measured,  normalised to blue     " + "".join(f"{v:>9.4f}" for v in mc))
    print("chromaticity error                "
          + "".join(f"{100.0 * (pc[i] / mc[i] - 1.0):>+8.1f}%" for i in range(3)))
    print()
    print(
        "The quantity is the sum of the two LIGHT colours (CHFD+0x107C and +0x1080),\n"
        "not sun + HFLD ambient: the ambient REGISTER is set to zero for this draw and\n"
        "restored afterwards, while both light colours are promoted into D3DLIGHT9.Ambient\n"
        "for the terrain draw only."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
