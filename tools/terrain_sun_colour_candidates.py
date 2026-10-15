#!/usr/bin/env python3
"""Re-derive, from shipped Level 100 HFLD bytes only, every per-channel factor
the retail sun/ambient colours can produce, and compare each one's chromaticity
against the measured terrain factor.

This exists to settle whether the terrain gain's chromaticity identifies a
mechanism. It does not: several unrelated quantities built from the same two
colours agree with the measurement to within a few percent, and none of them
supplies a magnitude. Run it to reproduce the numbers quoted in
`reverse-engineering/binary-analysis/terrain-sun-colour-route-2026-07-26.md`.

Usage:
    python ./tools/terrain_sun_colour_candidates.py [heightfield.hfld.bin]
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

# CHFD payload start inside the HFLD chunk: 8-byte HFLD frame, then the 8-byte
# CHFD frame. Matches rebuild/tools/materialize_retail_assets.py.
CHFD_OFFSET = 0x10
SUN_COLOUR_FIELD = 0x107C
AMBIENT_COLOUR_FIELD = 0x108C

# reverse-engineering/binary-analysis/terrain-implied-macro-inversion-2026-07-26.md
# mean of implied_retail_macro / reconstruction_macro over 28,196 paired pixels.
MEASURED_FACTOR = (1.457, 1.389, 1.147)


def channels(colour: int) -> tuple[int, int, int]:
    return ((colour >> 16) & 0xFF, (colour >> 8) & 0xFF, colour & 0xFF)


def gradient_base(sun: tuple[int, int, int], ambient: tuple[int, int, int]) -> tuple[int, int, int]:
    """`CHeightField__InitColorGradient` / `DXPalletizer__Palletize` share this
    expression: (ambient << 8) / ((sun & 0xFE) + 1), integer division."""
    return tuple((ambient[i] << 8) // ((sun[i] & 0xFE) + 1) for i in range(3))


def normalise_to_blue(values: tuple[float, float, float]) -> tuple[float, float, float]:
    return tuple(value / values[2] for value in values)


def main() -> int:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT_HEIGHT_FIELD
    data = path.read_bytes()
    sun_raw = struct.unpack_from("<I", data, CHFD_OFFSET + SUN_COLOUR_FIELD)[0]
    ambient_raw = struct.unpack_from("<I", data, CHFD_OFFSET + AMBIENT_COLOUR_FIELD)[0]
    sun = channels(sun_raw)
    ambient = channels(ambient_raw)
    base = gradient_base(sun, ambient)

    print(f"source           {path}")
    print(f"sun     +0x107C  0x{sun_raw:08X}  {sun}")
    print(f"ambient +0x108C  0x{ambient_raw:08X}  {ambient}")
    print(f"gradient base    {base}   (shared by InitColorGradient and Palletize)")
    print()

    candidates: list[tuple[str, tuple[float, float, float]]] = [
        ("measured terrain factor", MEASURED_FACTOR),
        ("sun + ambient", tuple(float(sun[i] + ambient[i]) for i in range(3))),
        ("sun alone", tuple(float(v) for v in sun)),
        # DXPalletizer__Palletize expand_half_palette writes
        #   dest = src * ((base + 255) // 2) // 255
        # so recovering src from dest costs the reciprocal of that factor.
        (
            "1 / Palletize half-palette factor",
            tuple(255.0 / ((base[i] + 255) // 2) for i in range(3)),
        ),
    ]

    measured_chroma = normalise_to_blue(MEASURED_FACTOR)
    print(f"{'candidate':38s} {'raw':>22s} {'normalised to blue':>24s}   error vs measured")
    for label, values in candidates:
        chroma = normalise_to_blue(values)
        raw = "(" + ", ".join(f"{v:.3f}" for v in values) + ")"
        norm = "(" + ", ".join(f"{v:.3f}" for v in chroma) + ")"
        if label == "measured terrain factor":
            err = ""
        else:
            err = "  " + ", ".join(
                f"{100.0 * (chroma[i] / measured_chroma[i] - 1.0):+.1f}%" for i in range(2)
            )
        print(f"{label:38s} {raw:>22s} {norm:>24s}{err}")

    print()
    print(
        "Chromaticity does not discriminate: every candidate above is built from the same\n"
        "two colours, so agreeing to a few percent is expected and identifies nothing.\n"
        "None supplies a magnitude, and the Palletize branch is unreachable on PC."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
