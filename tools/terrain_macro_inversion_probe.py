#!/usr/bin/env python3
"""Invert retail's rendered terrain to its IMPLIED macro cache, and compare it
texel by texel against the reconstruction's macro cache.

Why this exists. The reconstruction's macro cache has never been checked against
retail's. Its only retail-side validation was shown to be circular in c745a9a2:
`level100-root-terrain.rgb565.bin` is this project's own Python transcription of
the blit, gated against a hash of its own output, and the C# compositor test then
asserts C# equals that Python. So the measured "retail / macro = 1.400, 1.295,
1.075" is a ratio against an unvalidated denominator.

Method. Retail's terrain pixel is

    retail_pixel = fog( min(min(macro * detail1, 1) * cloud * 2, 1) * detail2 * 2 )

Every term except `macro` is measured directly from the reconstruction, at the
same deterministic camera offset, by capturing the terrain shader with its
fragment tail replaced (ONSLAUGHT_TERRAIN_PROBE):

  mask    terrain coverage (R==255, B==0) with per-pixel fog visibility in G
  chain   0.25 * detail1 * cloud * 2 * detail2 * 2, with both min() removed
  macro   our macro cache as sampled on screen
  uv      root-map texel coordinate at 2-unit resolution, plus the macro level
  uvfine  the same coordinate at 1/128-unit resolution inside a 2-unit period

Then per paired pixel

    implied_retail_macro = unfog(retail_pixel) / chain
    our_macro            = macro probe

and the world position from uv+uvfine attributes each pixel to a root-map texel,
hence to its authored material set, mixer weights, shade index and tile.

This applies no gain, offset or tint anywhere. It only divides by terms it has
measured, and bins the result.
"""

from __future__ import annotations

import argparse
import struct
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from PIL import Image

REPO = Path(__file__).resolve().parents[1]
HIERARCHY = REPO / (
    "rebuild/OnslaughtRebuild.Godot/Assets/Level100/Source/level100-terrain-hierarchy.bin"
)
ROOT_MAP = REPO / (
    "rebuild/OnslaughtRebuild.Godot/Assets/Level100/Source/level100-root-terrain.rgb565.bin"
)

# HFLD chunk values for Level 100, already pinned by the materializer.
SUN_RGB24 = 0xBDB179
AMBIENT_RGB24 = 0x0D0F2B

MAP_SIZE = 512
TILES = 64


# --------------------------------------------------------------------------
# LTH1 terrain hierarchy - the same bytes Level100TerrainCompositor consumes.
# --------------------------------------------------------------------------


class Hierarchy:
    def __init__(self, source: bytes) -> None:
        pos = 0

        def u32() -> int:
            nonlocal pos
            value = struct.unpack_from("<I", source, pos)[0]
            pos += 4
            return value

        def take(count: int) -> bytes:
            nonlocal pos
            chunk = source[pos : pos + count]
            pos += count
            return chunk

        if take(4) != b"LTH1" or u32() != 1:
            raise RuntimeError("not an LTH1 terrain hierarchy")
        level_count = u32()
        self.maps: list[tuple[int, np.ndarray, np.ndarray]] = []
        for _ in range(level_count):
            width = u32()
            length = u32()
            indices = np.frombuffer(take(length), dtype=np.uint8)
            palette_length = u32()
            palette = np.frombuffer(take(palette_length * 4), dtype="<u4")
            self.maps.append((width, indices, palette))

        cell_count = u32()
        self.material_ids: list[bytes] = []
        self.weights: list[np.ndarray] = []
        for _ in range(cell_count):
            layers = source[pos]
            pos += 1
            self.material_ids.append(take(layers))
            self.weights.append(
                np.frombuffer(take(layers * 81), dtype=np.int8).reshape(layers, 9, 9)
            )

        shade_length = u32()
        self.shade = np.frombuffer(take(shade_length), dtype=np.uint8).reshape(
            MAP_SIZE, MAP_SIZE
        )

        shadow_count = u32()
        self.shadow: dict[int, np.ndarray] = {}
        for _ in range(shadow_count):
            tile_index = struct.unpack_from("<H", source, pos)[0]
            pos += 2
            bits = np.frombuffer(take(512), dtype=np.uint8)
            self.shadow[tile_index] = np.unpackbits(bits, bitorder="little").reshape(
                64, 64
            )

        pine_alpha_length = u32()
        self.pine_alpha = np.frombuffer(take(pine_alpha_length), dtype=np.uint8)
        pine_count = u32()
        self.pines = [
            struct.unpack_from("<hhB", source, pos + (index * 5))
            for index in range(pine_count)
        ]
        pos += pine_count * 5
        if pos != len(source):
            raise RuntimeError("trailing hierarchy data")


def lighting_gradient(sun: int, ambient: int) -> list[tuple[int, int, int]]:
    """Level100TerrainCompositor.BuildLightingGradient, value for value."""
    red_base = (((ambient >> 16) & 0xFF) << 8) // (((sun >> 16) & 0xFE) + 1)
    green_base = (ambient & 0xFF00) // (((sun >> 8) & 0xFE) + 1)
    blue_base = ((ambient & 0xFF) << 8) // ((sun & 0xFE) + 1)
    red, green, blue = red_base << 8, green_base << 8, blue_base << 8
    out = []
    for _ in range(64):
        out.append(
            (
                min(((red >> 8) << 16) * 2, 0x00F80000) & 0x00F80000,
                min(((green >> 8) << 11) * 2, 0x0007E000) & 0x0007E000,
                min(((blue >> 3) & ~31) * 2, 0x00001F00) & 0x00001F00,
            )
        )
        red += (255 - red_base) * 4
        green += (255 - green_base) * 4
        blue += (255 - blue_base) * 4
    return out


def _blend(color: int, candidate: int) -> int:
    difference = (candidate - color) & 0xFFFFFFFF
    if difference >= 0x80000000:
        return color
    if difference > 0x1FFFFFFF:
        return candidate
    weight = difference >> 26
    return (
        (
            ((color & 0x00F8F8FF) * (7 - weight) + (candidate & 0x00F8F8FF) * weight)
            >> 3
        )
        + (candidate & 0xFF000000)
    ) & 0xFFFFFFFF


def root_metadata(hierarchy: Hierarchy) -> dict[str, np.ndarray]:
    """Per root-map texel: unlit material blend colour, shade index, material key."""
    width, indices, palette = hierarchy.maps[0]
    if width != 16:
        raise RuntimeError(f"level 0 MAPT width {width}, expected 16")
    unlit = np.zeros((MAP_SIZE, MAP_SIZE, 3), dtype=np.int32)
    shade_used = np.zeros((MAP_SIZE, MAP_SIZE), dtype=np.int32)
    material_key = np.zeros((MAP_SIZE, MAP_SIZE), dtype=np.int64)
    layer_count = np.zeros((MAP_SIZE, MAP_SIZE), dtype=np.int32)
    shaded = np.zeros((MAP_SIZE, MAP_SIZE), dtype=bool)

    for y in range(MAP_SIZE):
        tile_y = y >> 3
        local_y = y & 7
        for x in range(MAP_SIZE):
            tile_x = x >> 3
            local_x = x & 7
            cell = (tile_y * TILES) + tile_x
            source_x = local_x + (8 if cell & 1 else 0)
            source_y = local_y + (8 if cell & 0x40 else 0)
            texel = (source_y * 16) + source_x
            color = int(palette[int(indices[texel])])
            ids = hierarchy.material_ids[cell]
            weights = hierarchy.weights[cell]
            for layer, material in enumerate(ids):
                index = int(indices[(material * 256) + texel])
                candidate = int(palette[(material * 256) + index])
                weight = int(weights[layer, local_y, local_x])
                candidate = (candidate + ((weight << 24) & 0xFFFFFFFF)) & 0xFFFFFFFF
                color = _blend(color, candidate)
            unlit[y, x] = (color & 0xFF, (color >> 8) & 0xFF, (color >> 16) & 0xFF)

            index = int(hierarchy.shade[min(y + 1, 511), min(x + 1, 511)])
            shadow = hierarchy.shadow.get(cell)
            if shadow is not None and shadow[local_y * 8, local_x * 8]:
                index >>= 1
                shaded[y, x] = True
            shade_used[y, x] = index
            key = 0
            for material in sorted(ids):
                key = (key * 8) + material + 1
            material_key[y, x] = key
            layer_count[y, x] = len(ids)

    return {
        "unlit": unlit,
        "shade": shade_used,
        "material_key": material_key,
        "layers": layer_count,
        "static_shadow": shaded,
    }


def rgb565_to_888(value: np.ndarray) -> np.ndarray:
    r = (value >> 11) & 0x1F
    g = (value >> 5) & 0x3F
    b = value & 0x1F
    return np.stack(
        [(r << 3) | (r >> 2), (g << 2) | (g >> 4), (b << 3) | (b >> 2)], axis=-1
    ).astype(np.float64)


# --------------------------------------------------------------------------
# Capture pairing
# --------------------------------------------------------------------------


def load(path: Path) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.float64)


def shift(frame: np.ndarray, dx: int, dy: int) -> np.ndarray:
    return np.roll(frame, (-dy, -dx), axis=(0, 1)) if (dx or dy) else frame


def describe(name: str, values: np.ndarray) -> str:
    q = np.percentile(values, [5, 25, 50, 75, 95], axis=0)
    return (
        f"  {name:<22} mean {np.round(values.mean(0), 3)}  sd {np.round(values.std(0), 3)}\n"
        f"  {'':<22} p05 {np.round(q[0], 3)}  p25 {np.round(q[1], 3)}  "
        f"p50 {np.round(q[2], 3)}  p75 {np.round(q[3], 3)}  p95 {np.round(q[4], 3)}"
    )


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--retail", required=True, type=Path)
    ap.add_argument("--probe-dir-prefix", required=True,
                    help="e.g. local-lab/godot-captures/inv-probe")
    ap.add_argument("--frame", default="level100-t025065ms.png")
    ap.add_argument("--shift", default="-1,0")
    ap.add_argument("--fog-color", default="216,216,252")
    ap.add_argument("--exclude", action="append", default=[])
    args = ap.parse_args(argv)

    dx, dy = (int(v) for v in args.shift.split(","))
    base = Path(args.probe_dir_prefix)
    probes = {}
    for mode in ("mask", "chain", "macro", "uv", "uvfine"):
        probes[mode] = shift(load(Path(f"{base}-{mode}") / args.frame), dx, dy)
    retail = load(args.retail)
    fog = np.array([float(v) for v in args.fog_color.split(",")])

    mask = probes["mask"]
    keep = (mask[:, :, 0] >= 250.0) & (mask[:, :, 2] <= 5.0)
    for spec in args.exclude:
        x0, y0, x1, y1 = (int(v) for v in spec.split(","))
        keep[y0:y1, x0:x1] = False

    visibility = (mask[:, :, 1] / 255.0)[keep][:, None]
    chain = probes["chain"][keep] / 255.0 * 4.0
    our_macro = probes["macro"][keep]
    retail_v = retail[keep]
    ys, xs = np.nonzero(keep)

    retail_chain = (retail_v - fog * (1.0 - visibility)) / np.maximum(visibility, 1e-6)

    # World identity. uv carries fract(world/512) at 8 bits (2.008 units per
    # code); uvfine carries fract(world/2) at 8 bits (1/128 unit per code).
    coarse = probes["uv"][keep][:, :2] / 255.0 * 512.0
    fine = probes["uvfine"][keep][:, :2] / 255.0 * 2.0
    steps = (coarse - fine) / 2.0
    ambiguous = np.abs(steps - np.round(steps)).max(axis=1) > 0.32
    world = (np.round(steps) * 2.0) + fine
    level = np.round(probes["uv"][keep][:, 2] / 255.0 * 8.0).astype(int)

    texel_x = np.clip(np.floor(world[:, 0]).astype(int), 0, 511)
    texel_y = np.clip(np.floor(world[:, 1]).astype(int), 0, 511)

    # Screen-space texel footprint, from the coarse uv gradient. A pixel whose
    # macro footprint spans many texels cannot be attributed to one texel and
    # cannot carry an unaliased ratio either.
    uvmap = np.full((*keep.shape, 2), np.nan)
    uvmap[keep] = world
    du = np.abs(np.gradient(uvmap[:, :, 0], axis=1)) + np.abs(
        np.gradient(uvmap[:, :, 0], axis=0)
    )
    dv = np.abs(np.gradient(uvmap[:, :, 1], axis=1)) + np.abs(
        np.gradient(uvmap[:, :, 1], axis=0)
    )
    footprint = np.maximum(du, dv)[keep]

    usable = (
        (our_macro.min(axis=1) > 8.0)
        & (retail_v.max(axis=1) < 250.0)
        & (visibility[:, 0] > 0.5)
        & (chain.min(axis=1) > 0.05)
        & ~ambiguous
    )
    # Both engines clamp at min(...,1). Drop any pixel where our own unclamped
    # chain would have saturated, since there the division is not an inverse.
    unclamped = (our_macro / 255.0 * chain).max(axis=1) < 0.97
    usable &= unclamped

    print(f"terrain pixels: {int(keep.sum())}   usable: {int(usable.sum())}")
    print(f"  dropped ambiguous world decode: {int(ambiguous.sum())}")
    print(f"  macro level histogram: {Counter(level[usable].tolist())}")

    sel = usable
    implied = retail_chain[sel] / chain[sel]
    ours = our_macro[sel]
    tx, ty = texel_x[sel], texel_y[sel]
    fp = footprint[sel]

    print()
    print("MACRO, 0-255, over usable paired terrain pixels")
    print(describe("our macro (probe)", ours))
    print(describe("retail IMPLIED macro", implied))
    print(describe("implied / ours", implied / ours))
    print(describe("implied - ours", implied - ours))

    print()
    print("Restricted to pixels whose macro footprint is under 1 texel "
          f"({int((fp < 1.0).sum())} px)")
    tight = fp < 1.0
    if tight.sum() > 200:
        print(describe("implied / ours", (implied / ours)[tight]))
        print(describe("implied", implied[tight]))
        print(describe("ours", ours[tight]))

    hierarchy = Hierarchy(HIERARCHY.read_bytes())
    meta = root_metadata(hierarchy)
    root = np.frombuffer(ROOT_MAP.read_bytes(), dtype="<u2").reshape(MAP_SIZE, MAP_SIZE)
    root888 = rgb565_to_888(root)

    unlit = meta["unlit"][ty, tx].astype(np.float64)
    shade = meta["shade"][ty, tx]
    mkey = meta["material_key"][ty, tx]
    cached = root888[ty, tx]

    print()
    print("SELF-CHECK: the macro probe against the pinned root map at the same texel")
    near = fp < 0.5
    if near.sum() > 100:
        print(f"  {int(near.sum())} px with footprint < 0.5 texel")
        print(describe("probe - root map", (ours - cached)[near]))

    print()
    print("IMPLIED MACRO vs the UNLIT material blend (the palette ceiling)")
    print(describe("unlit blend colour", unlit))
    print(describe("implied / unlit", implied / np.maximum(unlit, 1.0)))
    over = (implied > 252.0).mean(axis=0)
    print(f"  fraction of implied macro above 252 (palette max): {np.round(over, 4)}")
    print(f"  implied max per channel: {np.round(implied.max(axis=0), 1)}")

    # The compositor's own per-texel ceiling. The blit's last step is
    #   R5 = (red   * lightR & 0xF8000000) >> 16
    #   G6 = (green * lightG & 0x07E00000) >> 16
    #   B5 = (blue  * lightB & 0x001F0000) >> 16
    # and the gradient's three min() saturations cap lightR/G/B at
    # 0xF80000 / 0x7E000 / 0x1F00. Substituting those caps, a texel whose
    # material blend is `unlit` can never exceed unlit * 30/31, 62/63, 30/31
    # once expanded back to 8 bits - i.e. the saturated-gradient value. No
    # choice of shade index, mixer weight, palette entry, material id or
    # material ordering can put a macro texel above this line.
    ceiling = unlit * np.array([247.0 / 255.0, 251.0 / 255.0, 247.0 / 255.0])
    print()
    print("THE COMPOSITOR CEILING: the value each texel would take at a fully")
    print("saturated lighting gradient, which is the most its material data allows")
    print(describe("ceiling", ceiling))
    print(describe("ours / ceiling", ours / np.maximum(ceiling, 1.0)))
    print(describe("implied / ceiling", implied / np.maximum(ceiling, 1.0)))
    above = (implied > ceiling).mean(axis=0)
    print(f"  fraction of pixels where implied macro EXCEEDS the ceiling: "
          f"{np.round(above, 4)}")
    safe = retail_v[sel].max(axis=1) < 200.0
    if safe.sum() > 500:
        print(f"  same, restricted to {int(safe.sum())} px whose retail pixel is "
              f"under 200 so no retail-side min() can have fired: "
              f"{np.round((implied > ceiling)[safe].mean(axis=0), 4)}")
        print(describe("  implied/ceiling (unclamped)", (implied / np.maximum(ceiling, 1.0))[safe]))
        print(describe("  implied/ours    (unclamped)", (implied / ours)[safe]))

    print()
    print("PER-PIXEL SELF-CHECK: our macro against unlit x gradient(shade), which")
    print("verifies the world decode, the material blend and the shade attribution")
    gradient = lighting_gradient(SUN_RGB24, AMBIENT_RGB24)
    lut = np.array(
        [
            [
                ((255 * g[0]) & 0xF8000000) >> 16,
                ((255 * g[1]) & 0x07E00000) >> 16,
                ((255 * g[2]) & 0x001F0000) >> 16,
            ]
            for g in gradient
        ],
        dtype=np.float64,
    )
    # Expand the packed 5/6/5 result of a full-scale channel back to 8 bits.
    lut = np.stack(
        [
            ((lut[:, 0].astype(int) >> 11) << 3) | ((lut[:, 0].astype(int) >> 11) >> 2),
            ((lut[:, 1].astype(int) >> 5) << 2) | ((lut[:, 1].astype(int) >> 5) >> 4),
            ((lut[:, 2].astype(int)) << 3) | ((lut[:, 2].astype(int)) >> 2),
        ],
        axis=-1,
    ).astype(np.float64)
    predicted = unlit * (lut[shade] / 255.0)
    tight2 = fp < 0.5
    if tight2.sum() > 500:
        print(f"  {int(tight2.sum())} px, footprint < 0.5 texel")
        print(describe("ours / predicted", (ours / np.maximum(predicted, 1.0))[tight2]))

    print()
    print("EFFECTIVE LIGHTING FACTOR per shade index: macro / unlit blend")
    print(f"{'shade':>6} {'n':>7} | {'ours/unlit':>18} | {'implied/unlit':>18} | "
          f"{'retail/ours':>18}")
    for lo in range(0, 40, 2):
        s = (shade >= lo) & (shade < lo + 2) & tight2
        if s.sum() < 60:
            continue
        o = (ours / np.maximum(unlit, 1.0))[s].mean(0)
        i_ = (implied / np.maximum(unlit, 1.0))[s].mean(0)
        r = (implied / ours)[s].mean(0)
        print(f"{lo:3d}-{lo+1:<2d} {int(s.sum()):7d} | "
              f"{o[0]:6.3f}{o[1]:6.3f}{o[2]:6.3f} | "
              f"{i_[0]:6.3f}{i_[1]:6.3f}{i_[2]:6.3f} | "
              f"{r[0]:6.3f}{r[1]:6.3f}{r[2]:6.3f}")

    print()
    print("BY AUTHORED SHADE INDEX (after the static-shadow >>1)")
    print(f"{'shade':>6} {'n':>7} | {'ours RGB':>18} | {'implied RGB':>18} | "
          f"{'implied/ours':>18} | {'unlit RGB':>18}")
    for lo in range(0, 64, 4):
        s = (shade >= lo) & (shade < lo + 4)
        if s.sum() < 100:
            continue
        o, i_, u = ours[s].mean(0), implied[s].mean(0), unlit[s].mean(0)
        print(f"{lo:3d}-{lo+3:<2d} {int(s.sum()):7d} | "
              f"{o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f} | "
              f"{i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f} | "
              f"{i_[0]/o[0]:6.2f}{i_[1]/o[1]:6.2f}{i_[2]/o[2]:6.2f} | "
              f"{u[0]:6.1f}{u[1]:6.1f}{u[2]:6.1f}")

    print()
    print("BY MATERIAL SET (sorted material ids of the tile)")
    print(f"{'materials':>14} {'n':>7} | {'ours RGB':>18} | {'implied RGB':>18} | "
          f"{'implied/ours':>18} | {'shade':>5}")
    for key, count in Counter(mkey.tolist()).most_common(12):
        s = mkey == key
        if s.sum() < 100:
            continue
        ids = []
        k = int(key)
        while k:
            ids.append((k % 8) - 1)
            k //= 8
        o, i_ = ours[s].mean(0), implied[s].mean(0)
        print(f"{str(sorted(ids)):>14} {int(s.sum()):7d} | "
              f"{o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f} | "
              f"{i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f} | "
              f"{i_[0]/o[0]:6.2f}{i_[1]/o[1]:6.2f}{i_[2]/o[2]:6.2f} | "
              f"{shade[s].mean():5.1f}")

    print()
    print("BY MACRO LEVEL and by screen-space texel footprint")
    for name, groups in (
        ("level", [(v, level[sel] == v) for v in sorted(set(level[sel].tolist()))]),
        ("footprint", [
            (f"{lo}-{lo+1}", (fp >= lo) & (fp < lo + 1)) for lo in (0, 1, 2, 4, 8)
        ]),
    ):
        for label, s in groups:
            if s.sum() < 200:
                continue
            o, i_ = ours[s].mean(0), implied[s].mean(0)
            print(f"  {name} {str(label):>5} {int(s.sum()):6d} | ours "
                  f"{o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f} | implied "
                  f"{i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f} | ratio "
                  f"{i_[0]/o[0]:5.2f}{i_[1]/o[1]:5.2f}{i_[2]/o[2]:5.2f}")

    print()
    print("BY OUR MACRO VALUE (is the difference a gain, or value dependent?)")
    lum = ours @ np.array([0.299, 0.587, 0.114])
    edges = np.quantile(lum, np.linspace(0.0, 1.0, 9))
    for i in range(8):
        s = (lum >= edges[i]) & (lum < edges[i + 1] if i < 7 else lum <= edges[i + 1])
        if s.sum() < 200:
            continue
        o, i_ = ours[s].mean(0), implied[s].mean(0)
        print(f"  lum {edges[i]:6.1f}-{edges[i+1]:6.1f} {int(s.sum()):6d} | ours "
              f"{o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f} | implied "
              f"{i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f} | ratio "
              f"{i_[0]/o[0]:5.2f}{i_[1]/o[1]:5.2f}{i_[2]/o[2]:5.2f}")

    print()
    print("AT OUR COMPOSITOR'S SATURATION: pixels whose attributed shade is >= 26,")
    print("where the gradient is within 8% of its cap and the macro texel is")
    print("therefore within 8% of everything its material palettes allow")
    for cut in (24, 26, 28):
        s = shade >= cut
        if s.sum() < 100:
            continue
        o, i_, c = ours[s].mean(0), implied[s].mean(0), ceiling[s].mean(0)
        print(f"  shade >= {cut}  n={int(s.sum()):6d} | ours {o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f}"
              f" | ceiling {c[0]:6.1f}{c[1]:6.1f}{c[2]:6.1f}"
              f" | implied {i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f}"
              f" | ours/ceil {o[0]/c[0]:5.2f}{o[1]/c[1]:5.2f}{o[2]/c[2]:5.2f}"
              f" | implied/ceil {i_[0]/c[0]:5.2f}{i_[1]/c[1]:5.2f}{i_[2]/c[2]:5.2f}")

    print()
    print("BY SCREEN BAND (distance proxy: rows of the 640x480 frame)")
    py = ys[sel]
    for lo in range(0, 480, 60):
        s = (py >= lo) & (py < lo + 60)
        if s.sum() < 100:
            continue
        o, i_ = ours[s].mean(0), implied[s].mean(0)
        print(f"  y {lo:3d}-{lo+59:3d} {int(s.sum()):6d} | ours "
              f"{o[0]:6.1f}{o[1]:6.1f}{o[2]:6.1f} | implied "
              f"{i_[0]:6.1f}{i_[1]:6.1f}{i_[2]:6.1f} | ratio "
              f"{i_[0]/o[0]:5.2f}{i_[1]/o[1]:5.2f}{i_[2]/o[2]:5.2f}")

    print()
    print("SPATIAL MAP of implied/ours (green channel), by 64x64-unit world block")
    grid = defaultdict(list)
    ratio_g = implied[:, 1] / np.maximum(ours[:, 1], 1.0)
    for bx, by, value in zip(tx // 64, ty // 64, ratio_g):
        grid[(int(by), int(bx))].append(value)
    print("      " + "".join(f"{bx*64:>7d}" for bx in range(8)))
    for by in range(8):
        row = f"{by*64:>5d} "
        for bx in range(8):
            cell = grid.get((by, bx))
            row += "      ." if not cell or len(cell) < 30 else f"{np.mean(cell):7.2f}"
        print(row)
    print("SPATIAL MAP counts")
    for by in range(8):
        row = f"{by*64:>5d} "
        for bx in range(8):
            cell = grid.get((by, bx))
            row += f"{len(cell) if cell else 0:7d}"
        print(row)

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
