# SPDX-License-Identifier: GPL-3.0-or-later
"""Fit the retail FE_select_level_bracket01 arc alpha mask to a retail frontend capture.

Pure image analysis. Reads a 640x480 retail reference PNG and the AYA-wrapped
DXT2 bracket texture, then sweeps uniform scale and centre position to find the
placement whose alpha mask best covers the arc pixels actually drawn on the page.

The AYA container framing and the DXT2 (premultiplied DXT3) alpha layout follow
rebuild/OnslaughtRebuild.Godot/CuratedAyaTextureLoader.cs: a stream of
<uint32 little-endian compressed length><zlib record> producing a DDS whose
128-byte header is followed by 16-byte blocks of 8 alpha bytes (4 bits/texel,
row-major within the 4x4 block) then 8 colour bytes.

Placement convention matches DrawSurfaceCentered in RetailFrontendFlow.cs: the
512x512 surface is drawn centred on (cx, cy) at uniform `scale`, so a screen
pixel (px, py) samples texel (u, v) = ((px - cx) / scale + 256,
(py - cy) / scale + 256).

Nothing here launches, mutates, or reads the installed game.
"""

from __future__ import annotations

import argparse
import json
import struct
import zlib
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image

SURFACE = 512
SURFACE_CENTRE = SURFACE / 2.0
DESIGN_W = 640
DESIGN_H = 480


# --------------------------------------------------------------------------
# Texture decode
# --------------------------------------------------------------------------
def inflate_aya(raw: bytes) -> bytes:
    out = bytearray()
    pos = 0
    while pos < len(raw):
        (declared,) = struct.unpack_from("<I", raw, pos)
        pos += 4
        if declared == 0 or declared > len(raw) - pos:
            raise ValueError("invalid AYA record framing")
        out += zlib.decompress(raw[pos : pos + declared])
        pos += declared
    return bytes(out)


def dxt3_alpha(dds: bytes) -> np.ndarray:
    if dds[:4] != b"DDS ":
        raise ValueError("not a DDS payload")
    height = struct.unpack_from("<I", dds, 12)[0]
    width = struct.unpack_from("<I", dds, 16)[0]
    fourcc = dds[84:88]
    if fourcc not in (b"DXT2", b"DXT3"):
        raise ValueError(f"unsupported fourcc {fourcc!r}; only explicit-alpha DXT2/3 handled")
    body = np.frombuffer(dds, dtype=np.uint8, offset=128)
    blocks_x = width // 4
    blocks_y = height // 4
    body = body[: blocks_x * blocks_y * 16].reshape(blocks_y, blocks_x, 16)
    alpha_bytes = body[:, :, :8]  # (by, bx, 8)
    lo = alpha_bytes & 0x0F
    hi = alpha_bytes >> 4
    # byte k of a block holds texels (row k//2, cols 0..3) for k even/odd pairs
    inter = np.empty((blocks_y, blocks_x, 16), dtype=np.uint8)
    inter[:, :, 0::2] = lo
    inter[:, :, 1::2] = hi
    inter = inter.reshape(blocks_y, blocks_x, 4, 4)  # (by, bx, row, col)
    alpha = inter.transpose(0, 2, 1, 3).reshape(height, width)
    return (alpha.astype(np.uint16) * 17).astype(np.uint8)


def load_bracket_alpha(path: Path) -> np.ndarray:
    return dxt3_alpha(inflate_aya(path.read_bytes()))


# --------------------------------------------------------------------------
# Page masks
# --------------------------------------------------------------------------
@dataclass(frozen=True)
class PageSpec:
    name: str
    image: str
    # rectangles (x0, y0, x1, y1) inclusive that must NOT contribute samples
    exclude: tuple[tuple[int, int, int, int], ...]


# Exclusions are drawn from the proven page inventories in
# local-lab/STARTUP-FLOW-FINDINGS-2026-07-25.md and the region JSON under
# rebuild/tools/. They remove page furniture that is bright and neutral enough to
# be confused with arc metal, never the arc itself.
COMMON_EXCLUDE = (
    (0, 0, 200, 205),  # Forseti emblem, its writing, and the left header end-cap
    (180, 55, 610, 108),  # header bar and title text
    (560, 0, 610, 145),  # right header end-cap bracket (FET3_HEADER_BRACKET1)
    (0, 420, 60, 479),  # left chevron
    (588, 420, 639, 479),  # right chevron
)

PAGES = {
    "select-level": PageSpec(
        name="select-level",
        image="local-lab/retail-reference-pristine/select-level/04-select-level-640x480.png",
        exclude=COMMON_EXCLUDE
        + (
            (120, 125, 480, 190),  # "Episode 1" / "1.00 - Training Level" caption block
            (120, 305, 180, 345),  # lit level node (gold/blue disc) on the map
        ),
    ),
    "dev-select": PageSpec(
        name="dev-select",
        image="local-lab/retail-reference-pristine/choose-game-name/choose-game-name-640x480.png",
        exclude=COMMON_EXCLUDE
        + (
            (126, 128, 533, 404),  # translucent career list panel incl. border + scrollbar
            (126, 406, 533, 453),  # name entry field incl. border and cyan highlight
            (121, 0, 125, 479),  # faint vertical guide line at x=123
            (0, 178, 639, 182),  # faint horizontal guide line at y=180
        ),
    ),
}


def metal_mask(rgb: np.ndarray) -> np.ndarray:
    """Bright, near-neutral pixels: the arc's lit metal.

    The page background is (23,23,48) and the map/guide furniture is dim blue, so
    requiring min channel >= 60 and a channel spread <= 45 keeps the arc and
    rejects background, guides, dim blue map lines and the blue emblem.
    """
    r = rgb[:, :, 0].astype(int)
    g = rgb[:, :, 1].astype(int)
    b = rgb[:, :, 2].astype(int)
    lo = np.minimum(np.minimum(r, g), b)
    hi = np.maximum(np.maximum(r, g), b)
    return (lo >= 60) & ((hi - lo) <= 45)


def thick_only(mask: np.ndarray, min_run: int) -> np.ndarray:
    """Drop pixels whose horizontal run of metal is thinner than `min_run`.

    The arc bracket is a wide band (>= 25px across every scanline it occupies).
    The level-map episode curves, the x=123/y=180 guide lines and stray text
    strokes are 1-3px. This is a geometric filter, independent of any fitted
    parameter, so it cannot bias the fit toward a particular scale.
    """
    if min_run <= 1:
        return mask
    out = np.zeros_like(mask)
    for y in range(mask.shape[0]):
        row = mask[y]
        x = 0
        while x < len(row):
            if not row[x]:
                x += 1
                continue
            start = x
            while x < len(row) and row[x]:
                x += 1
            if x - start >= min_run:
                out[y, start:x] = True
    return out


def build_sample(page: PageSpec, count: int, seed: int, min_run: int = 6
                 ) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    rgb = np.array(Image.open(page.image).convert("RGB"))
    mask = metal_mask(rgb)
    for x0, y0, x1, y1 in page.exclude:
        mask[y0 : y1 + 1, x0 : x1 + 1] = False
    mask = thick_only(mask, min_run)
    ys, xs = np.nonzero(mask)
    rng = np.random.default_rng(seed)
    if len(ys) > count:
        pick = rng.choice(len(ys), size=count, replace=False)
        ys, xs = ys[pick], xs[pick]
    return xs.astype(float), ys.astype(float), mask


# --------------------------------------------------------------------------
# Fit
# --------------------------------------------------------------------------
def coverage(alpha: np.ndarray, xs: np.ndarray, ys: np.ndarray, cx: float, cy: float,
             scale: float, thresh: int) -> float:
    u = np.rint((xs - cx) / scale + SURFACE_CENTRE).astype(int)
    v = np.rint((ys - cy) / scale + SURFACE_CENTRE).astype(int)
    inside = (u >= 0) & (u < SURFACE) & (v >= 0) & (v < SURFACE)
    hit = np.zeros(len(xs), dtype=bool)
    hit[inside] = alpha[v[inside], u[inside]] > thresh
    return float(hit.mean())


def precision(alpha: np.ndarray, page_mask: np.ndarray, excl: np.ndarray,
              cx: float, cy: float, scale: float, thresh: int) -> tuple[float, int]:
    """Fraction of on-screen, non-excluded mask footprint pixels that are page metal."""
    yy, xx = np.mgrid[0:DESIGN_H, 0:DESIGN_W]
    u = np.rint((xx - cx) / scale + SURFACE_CENTRE).astype(int)
    v = np.rint((yy - cy) / scale + SURFACE_CENTRE).astype(int)
    inside = (u >= 0) & (u < SURFACE) & (v >= 0) & (v < SURFACE)
    foot = np.zeros((DESIGN_H, DESIGN_W), dtype=bool)
    foot[inside] = alpha[v[inside], u[inside]] > thresh
    foot &= ~excl
    n = int(foot.sum())
    if n == 0:
        return 0.0, 0
    return float((foot & page_mask).sum() / n), n


def sweep(alpha, xs, ys, scales, cxs, cys, thresh):
    best = None
    surface = {}
    for s in scales:
        row_best = None
        for cx in cxs:
            for cy in cys:
                c = coverage(alpha, xs, ys, cx, cy, s, thresh)
                if row_best is None or c > row_best[0]:
                    row_best = (c, cx, cy)
        surface[round(s, 4)] = row_best
        if best is None or row_best[0] > best[0]:
            best = (row_best[0], s, row_best[1], row_best[2])
    return best, surface


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--page", choices=sorted(PAGES), required=True)
    ap.add_argument("--texture", default="rebuild/OnslaughtRebuild.Godot/Assets/Frontend/level-bracket-01.texture.aya")
    ap.add_argument("--samples", type=int, default=4000)
    ap.add_argument("--seed", type=int, default=20260726)
    ap.add_argument("--alpha-threshold", type=int, default=128)
    ap.add_argument("--scale-min", type=float, default=1.10)
    ap.add_argument("--scale-max", type=float, default=1.75)
    ap.add_argument("--scale-step", type=float, default=0.01)
    ap.add_argument("--centre-radius", type=int, default=10)
    ap.add_argument("--centre-x", type=int, default=328)
    ap.add_argument("--centre-y", type=int, default=343)
    ap.add_argument("--min-run", type=int, default=6)
    ap.add_argument("--json", default=None)
    args = ap.parse_args()

    page = PAGES[args.page]
    alpha = load_bracket_alpha(Path(args.texture))
    xs, ys, mask = build_sample(page, args.samples, args.seed, args.min_run)

    excl = np.zeros((DESIGN_H, DESIGN_W), dtype=bool)
    for x0, y0, x1, y1 in page.exclude:
        excl[y0 : y1 + 1, x0 : x1 + 1] = True

    scales = np.arange(args.scale_min, args.scale_max + 1e-9, args.scale_step)
    r = args.centre_radius
    cxs = range(args.centre_x - r, args.centre_x + r + 1)
    cys = range(args.centre_y - r, args.centre_y + r + 1)

    best, surface = sweep(alpha, xs, ys, scales, cxs, cys, args.alpha_threshold)
    cov, s, cx, cy = best
    prec, foot = precision(alpha, mask, excl, cx, cy, s, args.alpha_threshold)

    print(f"page            : {page.name}")
    print(f"samples         : {len(xs)} (of {int(mask.sum())} eligible metal pixels)")
    print(f"peak coverage   : {cov*100:.1f}% at scale {s:.2f}, centre ({cx},{cy})")
    print(f"peak precision  : {prec*100:.1f}% over {foot} footprint px")
    print("scale response (best centre per scale):")
    for k in sorted(surface):
        c, bx, by = surface[k]
        bar = "#" * int(round(c * 60))
        print(f"  {k:5.2f}  {c*100:5.1f}%  ({bx},{by})  {bar}")

    if args.json:
        Path(args.json).write_text(
            json.dumps(
                {
                    "page": page.name,
                    "samples": len(xs),
                    "eligible": int(mask.sum()),
                    "peak": {"coverage": cov, "scale": s, "cx": cx, "cy": cy, "precision": prec},
                    "surface": {str(k): {"coverage": v[0], "cx": v[1], "cy": v[2]} for k, v in surface.items()},
                },
                indent=2,
            ),
            encoding="utf-8",
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
