#!/usr/bin/env python3
"""Measure retail's rendered-vs-source gain on a NON-TERRAIN surface.

Retail's terrain renders at a measured per-channel gain of about
(1.400, 1.295, 1.075) over the reconstruction's macro cache. One surviving
hypothesis was that the gain is frame-global - device gamma, back-buffer
format, RGB565 quantisation, or the presentation path. A frame-global transform
must also apply to the sky, which is drawn through the same device, the same
back buffer and the same presentation path, in the same frame.

This probe decodes the five shipped Kempy cube-25 DXT1 faces to RGB888 and asks
what gain best explains retail's on-screen sky pixels as samples of those
texels. It needs no UV mapping and no camera model: it scans a candidate gain g,
divides the rendered pixels by g, and measures the mean Euclidean distance to
the nearest shipped texel colour. A pass-through path minimises at g = 1.

Run the same scan on a reconstruction capture as a control - Godot's sky shader
is `ALBEDO = texture(sky_texture, UV).rgb`, unshaded, with no gain constant, so
its argmin is the calibration for "pass-through".

Read-only. Consumes materialized retail assets and promoted retail PNG frames.
"""

from __future__ import annotations

import argparse
import struct
import sys
from collections import Counter
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools"))

SKY_DIR = REPO / "rebuild/OnslaughtRebuild.Godot/Assets/Level100/Sky"
FACES = ["cent", "up", "right", "down", "left"]

# Measured retail terrain transfer, commits 7b53a174 / 93b514c0.
TERRAIN_GAIN = (1.400, 1.295, 1.075)

# Clean sky window: above the horizon ridge, right of the cockpit frame, clear
# of the HUD. x0,y0,x1,y1 half-open, 640x480 frames.
DEFAULT_WINDOW = "340,30,620,100"


def inflate_face(name: str) -> bytes:
    from aya_archive_inventory import inflate_aya_bytes

    dds = inflate_aya_bytes((SKY_DIR / f"cube25-{name}.texture.aya").read_bytes())
    if dds[:4] != b"DDS " or dds[84:88] != b"DXT1":
        raise RuntimeError(f"cube25-{name} is not an AYA-wrapped DXT1 DDS")
    height = struct.unpack_from("<I", dds, 12)[0]
    width = struct.unpack_from("<I", dds, 16)[0]
    if (width, height) != (512, 512):
        raise RuntimeError(f"cube25-{name} is {width}x{height}, expected 512x512")
    return dds[128 : 128 + (512 * 512 // 2)]


def _rgb565_to_888(v: np.ndarray) -> np.ndarray:
    r = (v >> 11) & 0x1F
    g = (v >> 5) & 0x3F
    b = v & 0x1F
    return np.stack(
        [(r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2)], axis=-1
    ).astype(np.int32)


def decode_dxt1(blocks: bytes) -> np.ndarray:
    """Decode a 512x512 DXT1 payload to a (512,512,3) uint8 array."""
    raw = np.frombuffer(blocks, dtype="<u2").reshape(-1, 4)
    idx = np.frombuffer(blocks, dtype="<u4").reshape(-1, 2)[:, 1]
    e0 = _rgb565_to_888(raw[:, 0])
    e1 = _rgb565_to_888(raw[:, 1])
    opaque = (raw[:, 0] > raw[:, 1])[:, None]
    c2 = np.where(opaque, (2 * e0 + e1) // 3, (e0 + e1) // 2)
    c3 = np.where(opaque, (e0 + 2 * e1) // 3, np.zeros_like(e0))
    palette = np.stack([e0, e1, c2, c3], axis=1)
    sel = np.stack([(idx >> (2 * i)) & 3 for i in range(16)], axis=1)
    texels = np.take_along_axis(palette, sel[:, :, None].repeat(3, axis=2), axis=1)
    bw = 512 // 4
    out = texels.reshape(-1, bw, 4, 4, 3).transpose(0, 2, 1, 3, 4)
    return out.reshape(512, 512, 3).astype(np.uint8)


def source_palette() -> np.ndarray:
    faces = [decode_dxt1(inflate_face(name)).reshape(-1, 3) for name in FACES]
    return np.unique(np.concatenate(faces), axis=0).astype(np.float32)


def mean_nearest_distance(pixels: np.ndarray, gain, palette: np.ndarray) -> float:
    q = pixels / np.asarray(gain, dtype=np.float32)
    out = np.empty(len(q))
    for i in range(0, len(q), 2000):
        chunk = q[i : i + 2000]
        out[i : i + 2000] = np.sqrt(
            ((chunk[:, None, :] - palette[None, :, :]) ** 2).sum(-1)
        ).min(1)
    return float(out.mean())


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--frame", required=True, type=Path)
    ap.add_argument("--window", default=DEFAULT_WINDOW, help="x0,y0,x1,y1")
    ap.add_argument("--samples", type=int, default=3000)
    ap.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()

    palette = source_palette()
    exact = {tuple(int(v) for v in row) for row in palette}
    print(f"shipped cube-25 palette: {len(palette)} distinct RGB888 texel colours")

    x0, y0, x1, y1 = (int(v) for v in args.window.split(","))
    frame = np.asarray(Image.open(args.frame).convert("RGB")).astype(np.float32)
    window = frame[y0:y1, x0:x1].reshape(-1, 3)
    print(f"{args.frame.name} window {x0},{y0}-{x1},{y1}: {len(window)} px "
          f"mean {np.round(window.mean(0), 2).tolist()}")

    counts = Counter(map(tuple, window.astype(int)))
    total = sum(counts.values())
    hit = sum(v for k, v in counts.items() if k in exact)
    ung = sum(
        v
        for k, v in counts.items()
        if tuple(int(round(c / g)) for c, g in zip(k, TERRAIN_GAIN)) in exact
    )
    print(f"  exact membership in the shipped palette   : {100*hit/total:5.2f}%")
    print(f"  after dividing by the terrain gain {TERRAIN_GAIN}: {100*ung/total:5.2f}%")
    print("  most common window colours (colour, px, in-palette):")
    for k, v in counts.most_common(8):
        print(f"    {k!s:22s} {v:5d}  {k in exact}")

    rng = np.random.default_rng(args.seed)
    sub = window[rng.choice(len(window), min(args.samples, len(window)), replace=False)]

    print("\n  uniform gain scan (mean distance to nearest shipped texel):")
    uniform = [
        (round(mean_nearest_distance(sub, (g, g, g), palette), 3), round(float(g), 3))
        for g in np.arange(0.90, 1.14, 0.02)
    ]
    for dist, g in uniform:
        print(f"    g={g:.2f}  {dist:6.3f}")
    print(f"  argmin uniform gain: {min(uniform)[1]}  (distance {min(uniform)[0]})")

    print("  per-channel argmin (other two channels held at 1.0):")
    for ch, label in enumerate("RGB"):
        scan = [
            (
                round(
                    mean_nearest_distance(
                        sub, [1.0 if k != ch else g for k in range(3)], palette
                    ),
                    3,
                ),
                round(float(g), 3),
            )
            for g in np.arange(0.90, 1.46, 0.02)
        ]
        best = min(scan)
        print(f"    {label}: g={best[1]}  distance {best[0]}")

    print(
        f"  terrain gain {TERRAIN_GAIN}: "
        f"{mean_nearest_distance(sub, TERRAIN_GAIN, palette):.3f}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
