# SPDX-License-Identifier: GPL-3.0-or-later
"""Measure cockpit placement error between a retail frame and a candidate.

The cockpit interior is the dark part of the frame; the world is not. Reducing
both frames to a "cockpit-dark" mask and asking how well the masks agree turns
"this region's mean colour is wrong" into "this many pixels are cockpit in one
image and world in the other", which is the distinction a region mean cannot
make. A whole-mask translation search is available for the same reason: a rigid
screen offset and a shading error look identical in a region mean and completely
different here.

Analysis only; writes nothing.

Usage:
  python ./tools/cockpit_edge_profile.py <retail.png> <candidate.png> [more.png ...]
"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def dark_mask(path: Path, threshold: int) -> tuple[list[bool], int, int]:
    with Image.open(path) as handle:
        image = handle.convert("RGB")
    width, height = image.size
    limit = threshold * 3
    raw = image.tobytes()
    mask = [raw[i] + raw[i + 1] + raw[i + 2] <= limit for i in range(0, len(raw), 3)]
    return mask, width, height


def agreement(
    reference: list[bool],
    candidate: list[bool],
    width: int,
    height: int,
    dx: int,
    dy: int,
) -> tuple[int, int, int]:
    """Return (both, reference_only, candidate_only) for candidate shifted by (dx, dy)."""
    both = ref_only = cand_only = 0
    for y in range(height):
        sy = y + dy
        if sy < 0 or sy >= height:
            continue
        row = y * width
        srow = sy * width
        for x in range(width):
            sx = x + dx
            if sx < 0 or sx >= width:
                continue
            r = reference[row + x]
            c = candidate[srow + sx]
            if r and c:
                both += 1
            elif r:
                ref_only += 1
            elif c:
                cand_only += 1
    return both, ref_only, cand_only


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("reference", type=Path)
    ap.add_argument("candidates", type=Path, nargs="+")
    ap.add_argument("--threshold", type=int, default=80,
                    help="mean channel value at or below which a pixel counts as cockpit-dark")
    ap.add_argument("--search", type=int, default=0,
                    help="half-width of the (dx, dy) translation search, in pixels")
    ap.add_argument("--search-step", type=int, default=4)
    args = ap.parse_args()

    reference, width, height = dark_mask(args.reference, args.threshold)
    print(f"reference {args.reference.name}: {sum(reference)} dark px of {width * height}")

    for path in args.candidates:
        candidate, candidate_width, candidate_height = dark_mask(path, args.threshold)
        if (candidate_width, candidate_height) != (width, height):
            raise SystemExit(
                f"{path} is {candidate_width}x{candidate_height}, "
                f"reference is {width}x{height}")
        both, ref_only, cand_only = agreement(reference, candidate, width, height, 0, 0)
        union = both + ref_only + cand_only
        if not union:
            print(f"{path.name}: both masks are empty")
            continue
        print(
            f"{path.name}: dark {sum(candidate)}  agree {both}  "
            f"retail-only {ref_only}  build-only {cand_only}  IoU {both / union:.4f}")
        if not args.search:
            continue
        best = None
        for dy in range(-args.search, args.search + 1, args.search_step):
            for dx in range(-args.search, args.search + 1, args.search_step):
                b, r, c = agreement(reference, candidate, width, height, dx, dy)
                u = b + r + c
                if u and (best is None or b / u > best[0]):
                    best = (b / u, dx, dy)
        if best:
            print(f"    best shift dx={best[1]} dy={best[2]} -> IoU {best[0]:.4f}")


if __name__ == "__main__":
    main()
