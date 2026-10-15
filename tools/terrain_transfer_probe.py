#!/usr/bin/env python3
"""Measure the terrain colour transfer function of two renders against a common macro input.

Method. The reconstruction's terrain shader is temporarily reduced to two probe
forms and captured at the same deterministic camera offsets as the ordinary
build:

  macro probe  ALBEDO = retail_output(macro_color)
  mask  probe  ALBEDO = retail_output(vec3(1.0, fog_visibility, 0.0))

The mask probe marks every screen pixel the terrain surface actually covers
(R == 255 and B == 0 is not produced anywhere else in the frame) and carries the
per-pixel fog visibility in green. With the world camera aligned, the same
screen pixel is the same terrain surface in the retail frame, so each pixel
yields a triple

    (macro value, retail pixel, reconstruction pixel)

and the ratio retail/macro is retail's measured stage-1..3 chain gain, with fog
removed analytically using the visibility the mask probe reports.

Read-only. Takes no thresholds and fits nothing; it only bins measurements.
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
from PIL import Image


def load(path: str) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.float64)


def unfog(pixel: np.ndarray, visibility: np.ndarray, fog: np.ndarray) -> np.ndarray:
    """Invert color = mix(fog, chain, vis) -> chain = (color - fog*(1-vis)) / vis."""
    return (pixel - fog * (1.0 - visibility)) / np.maximum(visibility, 1e-6)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--retail", required=True)
    parser.add_argument("--rebuild", required=True)
    parser.add_argument("--macro-probe", required=True)
    parser.add_argument("--mask-probe", required=True)
    parser.add_argument("--fog-color", default="216,216,252",
                        help="HFLD fog colour, 0-255 (default retail #D8D8FC)")
    parser.add_argument("--region", default="0,0,640,480",
                        help="x0,y0,x1,y1 screen box to restrict the pairing to")
    parser.add_argument("--exclude", action="append", default=[],
                        help="x0,y0,x1,y1 box to drop (HUD overlays); repeatable")
    parser.add_argument("--bins", type=int, default=8)
    parser.add_argument("--shift", default="0,0",
                        help="dx,dy from frame_alignment_probe; applied to the "
                             "reconstruction and both probes so they line up "
                             "with the retail frame")
    args = parser.parse_args(argv)

    retail = load(args.retail)
    rebuild = load(args.rebuild)
    macro = load(args.macro_probe)
    mask_probe = load(args.mask_probe)
    dx, dy = (int(v) for v in args.shift.split(","))
    if dx or dy:
        rebuild = np.roll(rebuild, (-dy, -dx), axis=(0, 1))
        macro = np.roll(macro, (-dy, -dx), axis=(0, 1))
        mask_probe = np.roll(mask_probe, (-dy, -dx), axis=(0, 1))
    fog = np.array([float(v) for v in args.fog_color.split(",")])

    height, width, _ = retail.shape
    keep = np.zeros((height, width), dtype=bool)
    x0, y0, x1, y1 = (int(v) for v in args.region.split(","))
    keep[y0:y1, x0:x1] = True
    for spec in args.exclude:
        ex0, ey0, ex1, ey1 = (int(v) for v in spec.split(","))
        keep[ey0:ey1, ex0:ex1] = False

    terrain = (mask_probe[:, :, 0] >= 250.0) & (mask_probe[:, :, 2] <= 5.0)
    keep &= terrain

    visibility = (mask_probe[:, :, 1] / 255.0)[keep][:, None]
    macro_v = macro[keep]
    retail_v = retail[keep]
    rebuild_v = rebuild[keep]

    print(f"terrain pixels paired: {int(keep.sum())} "
          f"({100.0 * keep.sum() / (height * width):.1f}% of frame)")
    if keep.sum() == 0:
        return 1

    retail_chain = unfog(retail_v, visibility, fog)
    rebuild_chain = unfog(rebuild_v, visibility, fog)

    # Only pixels where the macro input is meaningful and unsaturated can carry
    # a ratio; and only pixels where fog has not swamped the surface.
    usable = (macro_v.min(axis=1) > 8.0) & (retail_v.max(axis=1) < 250.0) \
        & (visibility[:, 0] > 0.5)
    print(f"usable for ratio (macro>8, retail<250, vis>0.5): {int(usable.sum())}")

    m = macro_v[usable]
    r = retail_chain[usable]
    b = rebuild_chain[usable]
    v = visibility[usable]

    print()
    print("MEANS over usable terrain pixels (R G B)")
    print(f"  macro probe        {m.mean(axis=0).round(1)}")
    print(f"  retail  (unfogged) {r.mean(axis=0).round(1)}")
    print(f"  rebuild (unfogged) {b.mean(axis=0).round(1)}")
    print(f"  retail  (as shown) {retail_v[usable].mean(axis=0).round(1)}")
    print(f"  rebuild (as shown) {rebuild_v[usable].mean(axis=0).round(1)}")
    print(f"  mean fog visibility {v.mean():.3f}")

    print()
    print("CHAIN GAIN = unfogged pixel / macro, per channel")
    print(f"  retail   {(r / m).mean(axis=0).round(3)}   "
          f"median {np.median(r / m, axis=0).round(3)}")
    print(f"  rebuild  {(b / m).mean(axis=0).round(3)}   "
          f"median {np.median(b / m, axis=0).round(3)}")

    print()
    print("TRANSFER FUNCTION: retail unfogged pixel binned by macro luminance")
    lum = m @ np.array([0.299, 0.587, 0.114])
    edges = np.quantile(lum, np.linspace(0.0, 1.0, args.bins + 1))
    print(f"{'macro bin':>13} {'n':>7} | {'macro RGB':>19} | "
          f"{'retail RGB':>19} | {'rebuild RGB':>19} | {'r/m':>17} | {'b/m':>17}")
    for i in range(args.bins):
        lo, hi = edges[i], edges[i + 1]
        sel = (lum >= lo) & (lum <= hi if i == args.bins - 1 else lum < hi)
        if sel.sum() < 50:
            continue
        mm = m[sel].mean(axis=0)
        rr = r[sel].mean(axis=0)
        bb = b[sel].mean(axis=0)
        print(f"{lo:6.1f}-{hi:6.1f} {int(sel.sum()):7d} | "
              f"{mm[0]:6.1f}{mm[1]:6.1f}{mm[2]:6.1f}  | "
              f"{rr[0]:6.1f}{rr[1]:6.1f}{rr[2]:6.1f}  | "
              f"{bb[0]:6.1f}{bb[1]:6.1f}{bb[2]:6.1f}  | "
              f"{rr[0]/mm[0]:5.2f}{rr[1]/mm[1]:6.2f}{rr[2]/mm[2]:6.2f} | "
              f"{bb[0]/mm[0]:5.2f}{bb[1]/mm[1]:6.2f}{bb[2]/mm[2]:6.2f}")

    print()
    print("SAME, binned by fog visibility (distance proxy)")
    vedges = np.quantile(v[:, 0], np.linspace(0.0, 1.0, args.bins + 1))
    for i in range(args.bins):
        lo, hi = vedges[i], vedges[i + 1]
        sel = (v[:, 0] >= lo) & (v[:, 0] <= hi if i == args.bins - 1 else v[:, 0] < hi)
        if sel.sum() < 50:
            continue
        mm = m[sel].mean(axis=0)
        rr = r[sel].mean(axis=0)
        bb = b[sel].mean(axis=0)
        print(f"vis {lo:5.3f}-{hi:5.3f} {int(sel.sum()):7d} | "
              f"macro {mm[0]:6.1f}{mm[1]:6.1f}{mm[2]:6.1f} | "
              f"r/m {rr[0]/mm[0]:5.2f}{rr[1]/mm[1]:6.2f}{rr[2]/mm[2]:6.2f} | "
              f"b/m {bb[0]/mm[0]:5.2f}{bb[1]/mm[1]:6.2f}{bb[2]/mm[2]:6.2f}")

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
