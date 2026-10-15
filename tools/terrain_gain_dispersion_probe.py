#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Report the DISPERSION of the per-pixel terrain gain, not just its mean.

`terrain_transfer_probe.py` reports the mean and median of

    gain = unfog(rendered pixel) / macro probe

per channel. That number can be matched by any constant multiplier. This script
adds the standard deviation and quantiles of the same per-pixel quantity, which
a constant multiplier scales but whose *relative* spread it cannot change:
multiplying every pixel by k multiplies both the mean and the sd by k, leaving
sd/mean fixed. So if retail's relative spread is wider than the
reconstruction's, no flat factor - including the byte-derived ambient-light term
- will close that part of the gap, and this script exists to keep that visible
while the mean is being matched.

Read-only. Fits nothing; it only divides by measured terms and describes the
distribution.

Usage:
    python ./tools/terrain_gain_dispersion_probe.py \
        --retail  <retail frame.png> \
        --rebuild <reconstruction frame.png> \
        --macro-probe <ONSLAUGHT_TERRAIN_PROBE=macro frame.png> \
        --mask-probe  <ONSLAUGHT_TERRAIN_PROBE=mask frame.png> \
        [--shift dx,dy] [--fog-color r,g,b]
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
from PIL import Image


def load(path: str) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.float64)


def describe(name: str, values: np.ndarray) -> None:
    q = np.percentile(values, [5, 25, 50, 75, 95], axis=0)
    mean = values.mean(axis=0)
    sd = values.std(axis=0)
    print(f"  {name:<24} mean {np.round(mean, 3)}  sd {np.round(sd, 3)}  "
          f"sd/mean {np.round(sd / np.maximum(mean, 1e-9), 3)}")
    print(f"  {'':<24} p05 {np.round(q[0], 3)}  p25 {np.round(q[1], 3)}  "
          f"p50 {np.round(q[2], 3)}  p75 {np.round(q[3], 3)}  p95 {np.round(q[4], 3)}")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--retail", required=True)
    ap.add_argument("--rebuild", required=True)
    ap.add_argument("--macro-probe", required=True)
    ap.add_argument("--mask-probe", required=True)
    ap.add_argument("--fog-color", default="216,216,252")
    ap.add_argument("--shift", default="-1,0")
    ap.add_argument("--exclude", action="append", default=[])
    args = ap.parse_args(argv)

    dx, dy = (int(v) for v in args.shift.split(","))

    def shift(frame: np.ndarray) -> np.ndarray:
        return np.roll(frame, (-dy, -dx), axis=(0, 1)) if (dx or dy) else frame

    retail = load(args.retail)
    rebuild = shift(load(args.rebuild))
    macro = shift(load(args.macro_probe))
    mask = shift(load(args.mask_probe))
    fog = np.array([float(v) for v in args.fog_color.split(",")])

    keep = (mask[:, :, 0] >= 250.0) & (mask[:, :, 2] <= 5.0)
    for spec in args.exclude:
        x0, y0, x1, y1 = (int(v) for v in spec.split(","))
        keep[y0:y1, x0:x1] = False

    visibility = (mask[:, :, 1] / 255.0)[keep][:, None]
    macro_v = macro[keep]
    retail_v = retail[keep]
    rebuild_v = rebuild[keep]

    usable = (
        (macro_v.min(axis=1) > 8.0)
        & (retail_v.max(axis=1) < 250.0)
        & (rebuild_v.max(axis=1) < 250.0)
        & (visibility[:, 0] > 0.5)
    )
    print(f"terrain pixels {int(keep.sum())}   usable {int(usable.sum())} "
          f"(macro>8, both renders<250, vis>0.5)")
    if usable.sum() < 100:
        return 1

    def unfog(pixel: np.ndarray) -> np.ndarray:
        return (pixel - fog * (1.0 - visibility)) / np.maximum(visibility, 1e-6)

    m = macro_v[usable]
    retail_gain = unfog(retail_v)[usable] / m
    rebuild_gain = unfog(rebuild_v)[usable] / m

    print()
    print("PER-PIXEL GAIN = unfog(pixel) / macro probe")
    describe("retail", retail_gain)
    describe("reconstruction", rebuild_gain)
    print()
    print("A flat multiplier moves 'mean' and 'sd' together and leaves 'sd/mean'")
    print("untouched, so any remaining sd/mean disagreement is a separate defect")
    print("from the magnitude and cannot be read as evidence about the factor.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
