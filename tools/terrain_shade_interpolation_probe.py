# SPDX-License-Identifier: GPL-3.0-or-later
"""Measure retail's 8.8 fixed-point shade interpolation in the landscape blit.

Re-implements `CLandscapeTexture__BlitTileRegionWithLightingMask` @ 0x0047eff0
exactly as disassembled (see
`reverse-engineering/binary-analysis/terrain-shade-bilinear-decode-2026-07-26.md`)
over the locally materialized Level 100 terrain hierarchy, and answers three
measurable questions:

  1. At level 0 (one destination texel per shade unit), is the interpolated
     index bit-identical to the far corner `shade[(y+1)*512 + (x+1)]`?
  2. Does the unclamped retail index ever leave the gradient's 0..63 range at
     any level, i.e. is the reconstruction's `Math.Clamp` a divergence?
  3. What per-texel standard deviation does the interpolation actually produce
     in the composited macro at each level?

Read-only. Renders into memory and writes nothing.
"""

from __future__ import annotations

import argparse
import struct
import sys
from pathlib import Path

import numpy as np

MAP_SIZE = 512
TILES = 64
TILE_WIDTH = 8
MATERIALS = 6
WEIGHTS_PER_LAYER = 9 * 9
PINE_LEVEL_OFFSETS = (0, 1, 5, 21, 85, 341, 1365)


def _sar(value: int, count: int) -> int:
    """Arithmetic right shift with x86 SAR semantics (floor division)."""
    return value >> count


class Hierarchy:
    def __init__(self, blob: bytes) -> None:
        view = memoryview(blob)
        position = 0

        def take(count: int) -> bytes:
            nonlocal position
            chunk = bytes(view[position:position + count])
            if len(chunk) != count:
                raise ValueError("terrain hierarchy is truncated")
            position += count
            return chunk

        def u32() -> int:
            return struct.unpack("<I", take(4))[0]

        if take(4) != b"LTH1" or u32() != 1:
            raise ValueError("not an LTH1 terrain hierarchy")
        map_count = u32()
        self.maps: list[tuple[int, bytes, tuple[int, ...]]] = []
        for _ in range(map_count):
            width = u32()
            data = take(u32())
            palette_count = u32()
            palette = struct.unpack(f"<{palette_count}I", take(palette_count * 4))
            self.maps.append((width, data, palette))

        cell_count = u32()
        self.cells: list[tuple[bytes, bytes]] = []
        for _ in range(cell_count):
            layers = take(1)[0]
            materials = take(layers)
            weights = take(layers * WEIGHTS_PER_LAYER)
            self.cells.append((materials, weights))

        self.shade = take(u32())
        shadow_count = u32()
        self.shadows: list[bytes | None] = [None] * cell_count
        for _ in range(shadow_count):
            tile_index = struct.unpack("<H", take(2))[0]
            self.shadows[tile_index] = take(64 * 64 // 8)

        self.pine_alpha = take(u32())
        pine_count = u32()
        self.pines = [struct.unpack("<hhB", take(5)) for _ in range(pine_count)]
        if position != len(blob):
            raise ValueError("terrain hierarchy has trailing data")


def lighting_gradient(sun: int, ambient: int) -> list[tuple[int, int, int]]:
    red_base = (((ambient >> 16) & 0xFF) << 8) // (((sun >> 16) & 0xFE) + 1)
    green_base = (ambient & 0xFF00) // (((sun >> 8) & 0xFE) + 1)
    blue_base = ((ambient & 0xFF) << 8) // ((sun & 0xFE) + 1)
    red, green, blue = red_base << 8, green_base << 8, blue_base << 8
    gradient = []
    for _ in range(64):
        gradient.append((
            min(((red >> 8) << 16) * 2, 0x00F80000) & 0x00F80000,
            min(((green >> 8) << 11) * 2, 0x0007E000) & 0x0007E000,
            min(((blue >> 3) & ~0x1F) * 2, 0x00001F00) & 0x00001F00,
        ))
        red += (255 - red_base) * 4
        green += (255 - green_base) * 4
        blue += (255 - blue_base) * 4
    return gradient


def _signed32(value: int) -> int:
    value &= 0xFFFFFFFF
    return value - 0x100000000 if value & 0x80000000 else value


def blend_material(color: int, candidate: int) -> int:
    difference = _signed32(candidate - color)
    if difference > 0x1FFFFFFF:
        return candidate
    if difference < 0:
        return color
    blend = difference >> 26
    return ((((color & 0x00F8F8FF) * (7 - blend)) +
             ((candidate & 0x00F8F8FF) * blend)) >> 3) + (candidate & 0xFF000000)


def render_tile(
    hierarchy: Hierarchy,
    gradient: list[tuple[int, int, int]],
    level: int,
    tile_x: int,
    tile_y: int,
    clamp: bool,
    index_range: list[int],
) -> tuple[np.ndarray, np.ndarray]:
    """Return (rgb565 block, raw shade index block) for one tile at `level`."""
    scale = 1 << level
    block_size = TILE_WIDTH * scale
    block = np.zeros((block_size, block_size), dtype=np.uint16)
    indices = np.zeros((block_size, block_size), dtype=np.int32)
    width, map_data, palette = hierarchy.maps[level]
    materials, weights = hierarchy.cells[(tile_y * TILES) + tile_x]
    shadow = hierarchy.shadows[(tile_y * TILES) + tile_x]
    shade = hierarchy.shade

    for control_y in range(TILE_WIDTH):
        shade_y = (tile_y * TILE_WIDTH) + control_y
        for control_x in range(TILE_WIDTH):
            shade_x = (tile_x * TILE_WIDTH) + control_x
            right = min(shade_x + 1, MAP_SIZE - 1)
            down = min(shade_y + 1, MAP_SIZE - 1)
            s00 = shade[(shade_y * MAP_SIZE) + shade_x]
            s01 = shade[(shade_y * MAP_SIZE) + right]
            s10 = shade[(down * MAP_SIZE) + shade_x]
            s11 = shade[(down * MAP_SIZE) + right]
            step_x = _sar((s01 << 8) - (s00 << 8), level)
            step_y = _sar((s10 << 8) - (s00 << 8), level)
            step_xy = _sar(((s11 << 8) - (s10 << 8)) - ((s01 << 8) - (s00 << 8)), level * 2)

            for sub_y in range(scale):
                pixel_y = (control_y * scale) + sub_y
                row = (s00 << 8) + ((sub_y + 1) * step_y)
                step = step_x + ((sub_y + 1) * step_xy)
                for sub_x in range(scale):
                    pixel_x = (control_x * scale) + sub_x
                    source_x = ((tile_x & 1) * block_size) + pixel_x
                    source_y = ((tile_y & 1) * block_size) + pixel_y
                    texel = (source_y * width) + source_x
                    color = palette[map_data[texel]]
                    for layer, material in enumerate(materials):
                        offset = (layer * WEIGHTS_PER_LAYER) + (control_y * 9) + control_x
                        w00 = struct.unpack("b", weights[offset:offset + 1])[0]
                        w01 = struct.unpack("b", weights[offset + 1:offset + 2])[0]
                        w10 = struct.unpack("b", weights[offset + 9:offset + 10])[0]
                        w11 = struct.unpack("b", weights[offset + 10:offset + 11])[0]
                        wx = _sar((w01 - w00) << 24, level)
                        wy = _sar((w10 - w00) << 24, level)
                        wxy = _sar(((w11 - w10) - (w01 - w00)) << 24, level * 2)
                        weight = (w00 << 24) + (sub_y * wy) + (sub_x * (wx + (sub_y * wxy)))
                        candidate = (
                            palette[(material * 256) +
                                    map_data[(material * width * width) + texel]] +
                            (weight & 0xFF000000)
                        ) & 0xFFFFFFFF
                        color = blend_material(color, candidate) & 0xFFFFFFFF

                    raw = _sar(row + ((sub_x + 1) * step), 8)
                    index_range[0] = min(index_range[0], raw)
                    index_range[1] = max(index_range[1], raw)
                    indices[pixel_y, pixel_x] = raw
                    value = min(max(raw, 0), 63) if clamp else raw
                    if shadow is not None:
                        shadow_x = (pixel_x * 8) // scale
                        shadow_y = (pixel_y * 8) // scale
                        bit = (shadow_y * 64) + shadow_x
                        if shadow[bit >> 3] & (1 << (bit & 7)):
                            value >>= 1
                    light_r, light_g, light_b = gradient[value]
                    red = color & 0xFF
                    green = (color >> 8) & 0xFF
                    blue = (color >> 16) & 0xFF
                    block[pixel_y, pixel_x] = (
                        ((green * light_g & 0x07E00000) +
                         (blue * light_b & 0x001F0000) +
                         (red * light_r & 0xF8000000)) >> 16
                    ) & 0xFFFF
    return block, indices


def unpack565(block: np.ndarray) -> np.ndarray:
    value = block.astype(np.int32)
    return np.stack([
        ((value >> 11) & 0x1F) / 31.0,
        ((value >> 5) & 0x3F) / 63.0,
        (value & 0x1F) / 31.0,
    ], axis=-1)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--hierarchy",
        default="rebuild/OnslaughtRebuild.Godot/Assets/Level100/Source/"
                "level100-terrain-hierarchy.bin")
    parser.add_argument(
        "--root",
        default="rebuild/OnslaughtRebuild.Godot/Assets/Level100/Source/"
                "level100-root-terrain.rgb565.bin")
    parser.add_argument("--sun", default="0xBDB179")
    parser.add_argument("--ambient", default="0x0D0F2B")
    parser.add_argument("--levels", default="0,1,2,3,4")
    parser.add_argument(
        "--tile-sample", type=int, default=48,
        help="island tiles to composite per level for the spread statistic; "
             "levels 3-4 render 64x64 and 128x128 texels per tile")
    args = parser.parse_args(argv)

    hierarchy = Hierarchy(Path(args.hierarchy).read_bytes())
    gradient = lighting_gradient(int(args.sun, 0), int(args.ambient, 0))
    shade = np.frombuffer(hierarchy.shade, dtype=np.uint8).reshape(MAP_SIZE, MAP_SIZE)

    # Island tiles only: the sea floor is a uniform shade-0 plateau and would
    # dominate any whole-map statistic.
    island_tiles = [
        (tile_x, tile_y)
        for tile_y in range(TILES)
        for tile_x in range(TILES)
        if shade[tile_y * 8:(tile_y + 1) * 8 + 1, tile_x * 8:(tile_x + 1) * 8 + 1].any()
    ]
    print(f"island tiles with any non-zero shade corner: {len(island_tiles)} of {TILES * TILES}")

    # Question 1 -- level-0 degeneracy, measured over every texel of the map.
    corner_mismatch = 0
    corner_checked = 0
    index_range = [1 << 30, -(1 << 30)]
    for tile_x, tile_y in island_tiles:
        _, indices = render_tile(hierarchy, gradient, 0, tile_x, tile_y, True, index_range)
        for control_y in range(TILE_WIDTH):
            for control_x in range(TILE_WIDTH):
                y = (tile_y * 8) + control_y
                x = (tile_x * 8) + control_x
                expected = shade[min(y + 1, 511), min(x + 1, 511)]
                corner_checked += 1
                if indices[control_y, control_x] != expected:
                    corner_mismatch += 1
    print(f"level 0: interpolated index vs shade[(y+1)*512+(x+1)] -> "
          f"{corner_mismatch} mismatches in {corner_checked} texels")

    # Questions 2 and 3 -- range and per-texel spread at each level.
    print(f"{'level':>5} {'rawMin':>7} {'rawMax':>7} "
          f"{'meanR':>7} {'meanG':>7} {'meanB':>7} {'sdR':>6} {'sdG':>6} {'sdB':>6}")
    stride = max(1, len(island_tiles) // args.tile_sample)
    sampled = island_tiles[::stride]
    print(f"spread statistic over {len(sampled)} island tiles (stride {stride})")
    for level in [int(part) for part in args.levels.split(",")]:
        index_range = [1 << 30, -(1 << 30)]
        samples: list[np.ndarray] = []
        for tile_x, tile_y in sampled:
            block, _ = render_tile(
                hierarchy, gradient, level, tile_x, tile_y, True, index_range)
            samples.append(unpack565(block).reshape(-1, 3))
        pixels = np.concatenate(samples, axis=0)
        mean = pixels.mean(axis=0)
        sd = pixels.std(axis=0)
        print(f"{level:>5} {index_range[0]:>7} {index_range[1]:>7} "
              f"{mean[0]:>7.4f} {mean[1]:>7.4f} {mean[2]:>7.4f} "
              f"{sd[0]:>6.4f} {sd[1]:>6.4f} {sd[2]:>6.4f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
