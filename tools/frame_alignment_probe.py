#!/usr/bin/env python3
"""Report whether two renders of the same scene are looking at the same world.

Colour comparison between a retail frame and a reconstruction frame is only
meaningful when the world camera agrees: retail's own Level 100 terrain spans a
2.7x range of regional means inside a single frame, so a fixed screen box is a
different surface in each render when the pose differs.

This measures normalized cross-correlation of luminance per horizontal band,
both unshifted and over a search of 2-D shifts. A best NCC well below 1.0, or
best shifts that disagree between bands, means no translation aligns the frames
and the difference is a camera pose error, not a shading error.
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
from PIL import Image


def load(path: str) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("L"), dtype=np.float64)


def ncc(a: np.ndarray, b: np.ndarray) -> float:
    am = a - a.mean()
    bm = b - b.mean()
    denominator = np.sqrt((am * am).sum() * (bm * bm).sum())
    return float((am * bm).sum() / denominator) if denominator > 0 else 0.0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", required=True)
    parser.add_argument("--candidate", required=True)
    parser.add_argument("--search", type=int, default=25)
    parser.add_argument("--x0", type=int, default=140)
    parser.add_argument("--x1", type=int, default=520)
    parser.add_argument("--band", action="append", default=[],
                        help="name=y0,y1 (repeatable)")
    args = parser.parse_args(argv)

    reference = load(args.reference)
    candidate = load(args.candidate)
    if reference.shape != candidate.shape:
        print(f"shape mismatch {reference.shape} vs {candidate.shape}")
        return 2

    bands = args.band or ["far=170,240", "mid=240,310", "near=310,395"]
    search = args.search
    for spec in bands:
        name, _, coords = spec.partition("=")
        y0, y1 = (int(v) for v in coords.split(","))
        reference_band = reference[y0:y1, args.x0:args.x1]
        best = (-1.0, 0, 0)
        for dy in range(-search, search + 1):
            for dx in range(-search, search + 1):
                window = candidate[y0 + dy:y1 + dy, args.x0 + dx:args.x1 + dx]
                if window.shape != reference_band.shape:
                    continue
                value = ncc(reference_band, window)
                if value > best[0]:
                    best = (value, dx, dy)
        unshifted = ncc(reference_band, candidate[y0:y1, args.x0:args.x1])
        print(f"{name:6s} y[{y0}:{y1}]  ncc@(0,0)={unshifted:.3f}  "
              f"best={best[0]:.3f} at dx={best[1]:+d} dy={best[2]:+d}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
