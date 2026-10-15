#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Characterise the SPATIAL STRUCTURE of the terrain difference, not its magnitude.

`terrain_gain_dispersion_probe.py` reports that retail's per-pixel terrain gain
carries roughly three times the reconstruction's relative dispersion
(`sd/mean` 0.375/0.317/0.310 against 0.100/0.077/0.061) and correctly refuses to
read that as evidence about the flat ambient-light factor. It does not say *where
in space* the extra variation lives, and a single standard deviation cannot: it is
identical for white noise, for one clustered blob, and for a handful of outliers.

This script answers the spatial question directly, with four independent measures
over the same terrain pixels:

1.  **Radial and anisotropic power spectra** of retail's frame, the
    reconstruction's frame and the macro probe, averaged over every window that
    is entirely terrain. If retail carried a high-frequency term the
    reconstruction lacks, retail's power would exceed the reconstruction's in the
    high-frequency bands. This is the measurement that names a missing term - or
    rules one out.

2.  **Robust dispersion.** The interquartile range divided by 1.349 estimates the
    same quantity as `sd` for a normal distribution but ignores the tails, so
    comparing the two says how much of a reported `sd` is bulk and how much is a
    small number of extreme pixels.

3.  **Variance localisation.** What fraction of the gain variance is carried by
    what fraction of the pixels, and where those pixels sit relative to the
    terrain silhouette and to the HUD/cockpit regions - a measure of whether the
    residual is a shading term (diffuse) or a content mismatch (clustered).

4.  **Sub-pixel registration.** The correlation of the two high-pass images over a
    grid of fractional shifts. A residual sub-pixel offset decorrelates the two
    frames at pixel scale and inflates every ratio statistic, so it must be
    measured before any per-pixel dispersion is attributed to shading.

    A half-pixel bilinear shift has frequency response |cos(pi f)|, which is ZERO
    at Nyquist: resampling either frame destroys exactly the high-frequency
    content the spectrum measure is about. The spectra in (1) are therefore taken
    on integer-shifted frames only, and the registration search in (4) reports the
    control that separates a genuine offset from the blur its own resampling
    introduces - shifting the *reference* by the same amount must move the
    correlation the other way.

Read-only. Fits nothing.

Usage:
    python ./tools/terrain_spatial_structure_probe.py \
        --retail  <retail frame.png> \
        --rebuild <reconstruction frame.png> \
        --macro-probe <ONSLAUGHT_TERRAIN_PROBE=macro frame.png> \
        --mask-probe  <ONSLAUGHT_TERRAIN_PROBE=mask frame.png> \
        [--regions rebuild/tools/gameplay-regions-level100.json] \
        [--shift dx,dy] [--fog-color r,g,b] [--window 48]
"""

from __future__ import annotations

import argparse
import json
import sys

import numpy as np
from PIL import Image

_RADIAL_BANDS = ((0.0, 0.05), (0.05, 0.1), (0.1, 0.2), (0.2, 0.3), (0.3, 0.4), (0.4, 0.51))


def load(path: str) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.float64)


def roll(frame: np.ndarray, dy: int, dx: int) -> np.ndarray:
    return np.roll(frame, (dy, dx), axis=(0, 1)) if (dy or dx) else frame


def fractional_shift(frame: np.ndarray, fy: float, fx: float) -> np.ndarray:
    """Bilinear resample. Destroys Nyquist at fy == fx == 0.5; see the docstring."""
    iy, ix = int(np.floor(fy)), int(np.floor(fx))
    ty, tx = fy - iy, fx - ix
    def s(dy: int, dx: int) -> np.ndarray:
        return np.roll(frame, (iy + dy, ix + dx), axis=(0, 1))
    return (s(0, 0) * (1 - ty) * (1 - tx) + s(1, 0) * ty * (1 - tx)
            + s(0, 1) * (1 - ty) * tx + s(1, 1) * ty * tx)


def high_pass(frame: np.ndarray) -> np.ndarray:
    box = sum(np.roll(frame, (dy, dx), axis=(0, 1))
              for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9.0
    return frame - box


def band_power(tile: np.ndarray) -> np.ndarray:
    """Power of (tile / tile mean - 1) in six radial bands plus two directional ones."""
    size = tile.shape[0]
    centred = tile / tile.mean() - 1.0
    hann = np.hanning(size)
    windowed = centred * hann[:, None] * hann[None, :]
    power = np.abs(np.fft.fftshift(np.fft.fft2(windowed))) ** 2
    freq = np.fft.fftshift(np.fft.fftfreq(size))
    fy, fx = np.meshgrid(freq, freq, indexing="ij")
    radius = np.hypot(fy, fx)
    bands = [power[(radius >= lo) & (radius < hi)].mean() for lo, hi in _RADIAL_BANDS]
    bands.append(power[(np.abs(fy) > 0.30) & (np.abs(fx) < 0.10)].mean())
    bands.append(power[(np.abs(fx) > 0.30) & (np.abs(fy) < 0.10)].mean())
    return np.array(bands)


def full_terrain_windows(keep: np.ndarray, size: int, step: int) -> list[tuple[int, int]]:
    height, width = keep.shape
    return [(x, y)
            for y in range(0, height - size, step)
            for x in range(0, width - size, step)
            if keep[y:y + size, x:x + size].all()]


def edge_distance(keep: np.ndarray, limit: int) -> np.ndarray:
    """Chebyshev-ish erosion depth of the terrain mask, capped at `limit`."""
    distance = np.full(keep.shape, limit + 1, dtype=np.int32)
    current = keep.copy()
    for depth in range(1, limit + 1):
        eroded = (current
                  & np.roll(current, 1, 0) & np.roll(current, -1, 0)
                  & np.roll(current, 1, 1) & np.roll(current, -1, 1))
        distance[current & ~eroded] = np.minimum(distance[current & ~eroded], depth)
        current = eroded
    return distance


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--retail", required=True)
    ap.add_argument("--rebuild", required=True)
    ap.add_argument("--macro-probe", required=True)
    ap.add_argument("--mask-probe", required=True)
    ap.add_argument("--regions", default=None)
    ap.add_argument("--fog-color", default="216,216,252")
    ap.add_argument("--shift", default="-1,0", help="integer dx,dy applied to the rebuild frames")
    ap.add_argument("--window", type=int, default=48)
    args = ap.parse_args(argv)

    dx, dy = (int(v) for v in args.shift.split(","))
    retail = load(args.retail)
    rebuild = roll(load(args.rebuild), -dy, -dx)
    macro = roll(load(args.macro_probe), -dy, -dx)
    mask = roll(load(args.mask_probe), -dy, -dx)
    fog = np.array([float(v) for v in args.fog_color.split(",")])

    keep = (mask[:, :, 0] >= 254.9) & (mask[:, :, 2] <= 0.1)
    visibility = (mask[:, :, 1] / 255.0)[:, :, None]

    def unfog(pixel: np.ndarray) -> np.ndarray:
        return (pixel - fog * (1.0 - visibility)) / np.maximum(visibility, 1e-6)

    usable = (keep
              & (macro.min(2) > 8.0)
              & (retail.max(2) < 250.0)
              & (rebuild.max(2) < 250.0)
              & (visibility[:, :, 0] > 0.5))

    print(f"terrain pixels {int(keep.sum())}   usable {int(usable.sum())}")
    if usable.sum() < 1000:
        return 1

    # ---------------------------------------------------------------- spectra
    size = args.window
    windows = full_terrain_windows(keep, size, step=16)
    print()
    print(f"1. POWER SPECTRUM of (pixel / window mean), {len(windows)} fully-terrain "
          f"{size}x{size} windows, integer shift only")
    labels = ["r<.05", ".05-.1", ".1-.2", ".2-.3", ".3-.4", ".4-.5", "|fy|>.3", "|fx|>.3"]
    if windows:
        totals = {}
        for name, frame in (("retail", retail), ("rebuild", rebuild), ("macro", macro)):
            grey = frame.mean(2)
            totals[name] = sum(band_power(grey[y:y + size, x:x + size])
                               for x, y in windows) / len(windows)
        print("   " + "".join(f"{label:>11s}" for label in ["band"] + labels))
        for name in ("retail", "rebuild", "macro"):
            print(f"   {name:>11s}" + "".join(f"{v:11.4f}" for v in totals[name]))
        ratio = totals["retail"] / np.maximum(totals["rebuild"], 1e-12)
        print(f"   {'retail/ours':>11s}" + "".join(f"{v:11.2f}" for v in ratio))

    # ------------------------------------------------------ robust dispersion
    gains = {
        "retail": (unfog(retail) / np.maximum(macro, 1e-6))[usable],
        "rebuild": (unfog(rebuild) / np.maximum(macro, 1e-6))[usable],
    }
    print()
    print("2. DISPERSION of the per-pixel gain, plain and robust")
    for name, gain in gains.items():
        q = np.percentile(gain, [1, 25, 50, 75, 99], axis=0)
        robust = (q[3] - q[1]) / 1.349
        print(f"   {name:<8} mean {gain.mean(0).round(3)} sd {gain.std(0).round(3)} "
              f"sd/mean {(gain.std(0) / gain.mean(0)).round(3)}")
        print(f"   {'':<8} median {q[2].round(3)} robustSD {robust.round(3)} "
              f"robust/median {(robust / q[2]).round(3)}  p01 {q[0].round(2)} p99 {q[4].round(2)}")

    # ---------------------------------------------------- variance localisation
    channel = np.where(usable, (unfog(retail) / np.maximum(macro, 1e-6))[:, :, 0], np.nan)
    median = np.nanmedian(channel)
    robust = (np.nanpercentile(channel, 75) - np.nanpercentile(channel, 25)) / 1.349
    outlier = usable & (np.abs(channel - median) > 4 * robust)
    flat = channel[usable]
    share = (((flat - flat.mean()) ** 2)[np.abs(flat - median) > 4 * robust].sum()
             / ((flat - flat.mean()) ** 2).sum())
    print()
    print("3. VARIANCE LOCALISATION (retail, red channel)")
    print(f"   |z|>4 outliers: {int(outlier.sum())} of {int(usable.sum())} "
          f"({100 * outlier.sum() / usable.sum():.2f}% of pixels) carrying "
          f"{100 * share:.1f}% of the variance")
    distance = edge_distance(usable, limit=12)
    for lo, hi in ((1, 1), (2, 2), (3, 4), (5, 8), (9, 12), (13, 13)):
        band = usable & (distance >= lo) & (distance <= min(hi, 13))
        if band.sum() < 100:
            continue
        tag = f"{lo}-{hi}" if hi < 13 else ">12"
        print(f"   distance to terrain silhouette {tag:>5}: n={int(band.sum()):6d} "
              f"outliers {100 * outlier[band].sum() / band.sum():5.1f}%")
    if args.regions:
        with open(args.regions, encoding="utf-8") as handle:
            regions = json.load(handle)
        boxes = np.zeros_like(usable)
        for name, (x0, y0, x1, y1) in regions.items():
            if name in ("sky", "terrain mid-band", "horizon ridge"):
                continue
            boxes[y0:y1, x0:x1] = True
        print(f"   inside HUD/cockpit boxes: {100 * (usable & boxes).sum() / usable.sum():.1f}% "
              f"of terrain pixels but {100 * (outlier & boxes).sum() / max(outlier.sum(), 1):.1f}% "
              f"of the outliers")

    # ---------------------------------------------------------- registration
    print()
    print("4. SUB-PIXEL REGISTRATION (high-pass correlation; rebuild frame moved)")
    grey_retail, grey_rebuild = retail.mean(2), rebuild.mean(2)
    scope = keep.copy()
    for a in range(-3, 4):
        for b in range(-3, 4):
            scope &= np.roll(keep, (a, b), axis=(0, 1))
    reference = high_pass(grey_retail)[scope]
    grid = np.arange(-1.0, 1.26, 0.25)
    best = (-9.0, 0.0, 0.0)
    print("        " + "".join(f"{v:+7.2f}" for v in grid))
    for fy in grid:
        row = []
        for fx in grid:
            corr = np.corrcoef(reference, high_pass(fractional_shift(grey_rebuild, fy, fx))[scope])[0, 1]
            row.append(corr)
            if corr > best[0]:
                best = (corr, fy, fx)
        print(f"  dy{fy:+.2f}" + "".join(f"{v:+7.3f}" for v in row))
    print(f"   best correlation {best[0]:.4f} at rebuild shift dy={best[1]:+.2f} dx={best[2]:+.2f}")
    _, fy, fx = best
    control = np.corrcoef(high_pass(fractional_shift(grey_retail, fy, fx))[scope],
                          high_pass(grey_rebuild)[scope])[0, 1]
    print(f"   CONTROL - same shift applied to the RETAIL frame instead: {control:+.4f}")
    print("   A genuine offset makes this control fall; a blur artefact would raise it too.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
