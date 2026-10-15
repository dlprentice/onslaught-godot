#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Decode the player's Bink films (data/video/**.vid) into frame archives the Godot host can draw frame by frame.

    python rebuild/tools/materialize_films.py OUT [--game-dir DIR] [--only NAME ...]

The output is retail-derived and stays out of Git (write it under an ignored local-data/ or scratch directory).
Per film, OUT/<relative path without .vid>.bfa holds every frame as retail's binkw32 copies it for the game's default
USE_32_BIT_FMV (BGRA, 4 bytes per pixel, alpha 0 where the film has no alpha plane), each frame deflated on its own,
so any frame can be read directly. OUT/<...>.ogg holds the film's sound, when it has any. OUT/films.json lists every
film: archive and sound paths, width, height, frames and rate, all from the Bink header the core's FilmPlayer reads.

Pixels: FFmpeg decodes Bink to YUV 4:2:0 (its Bink decoder reproduces the planes); the conversion to BGRA is retail
binkw32's own, as measured for the native port (adapted from bea-decomp port/bink/bink.cpp, Tables and
BinkCopyToBuffer): nearest-neighbour chroma, one table per term, each a 16.16 product truncated toward zero, summed
and clamped, luma below 16 taken as 0. There the port matches retail's frames on 48,294 of 48,627 (port/bink/PARITY.md).

Archive format (little-endian): b"BFA1", then u32 width, height, frames, rate numerator, rate denominator,
bytes per pixel (4); then frames x (u64 offset, u32 length) from the start of the file; then the zlib streams.
"""
from __future__ import annotations

import argparse
import json
import shutil
import struct
import subprocess
import sys
import zlib
from pathlib import Path

import numpy as np

DEFAULT_GAME = Path.home() / ".local/share/Steam/steamapps/common/Battle Engine Aquila"


def bink_header(path: Path) -> dict:
    """The Bink header fields the game and the core read: frames, width, height, rate."""
    h = path.read_bytes()[:44]
    if h[:3] != b"BIK":
        raise SystemExit(f"{path}: not a Bink file")
    frames, = struct.unpack_from("<I", h, 8)
    width, height, rate_num, rate_den = struct.unpack_from("<IIII", h, 20)
    return {"frames": frames, "width": width, "height": height, "rate_num": rate_num, "rate_den": rate_den}


def trunc16(products: np.ndarray) -> np.ndarray:
    """A 16.16 product truncated toward zero, as retail's tables (arithmetic shift of the magnitude)."""
    return np.where(products >= 0, products >> 16, -((-products) >> 16))


I = np.arange(256, dtype=np.int64)
TABLE_Y = np.where(I < 16, 0, ((I - 16) * 76309) >> 16)
TABLE_RV = trunc16(104597 * (I - 128))
TABLE_GU = trunc16(-25675 * (I - 128))
TABLE_GV = trunc16(-53279 * (I - 128))
TABLE_BU = trunc16(132219 * (I - 128))


def to_bgra(y: np.ndarray, u: np.ndarray, v: np.ndarray, a: np.ndarray | None) -> bytes:
    """Retail binkw32's BGRA copy of one frame's planes (u and v at half resolution, nearest neighbour)."""
    h, w = y.shape
    uu = np.repeat(np.repeat(u, 2, axis=0), 2, axis=1)[:h, :w]
    vv = np.repeat(np.repeat(v, 2, axis=0), 2, axis=1)[:h, :w]
    ty = TABLE_Y[y]
    r = np.clip(ty + TABLE_RV[vv], 0, 255)
    g = np.clip(ty + TABLE_GU[uu] + TABLE_GV[vv], 0, 255)
    b = np.clip(ty + TABLE_BU[uu], 0, 255)
    out = np.empty((h, w, 4), dtype=np.uint8)
    out[..., 0], out[..., 1], out[..., 2] = b, g, r
    out[..., 3] = a if a is not None else 0
    return out.tobytes()


def has_alpha(path: Path) -> bool:
    probe = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=pix_fmt",
                            "-of", "csv=p=0", str(path)], capture_output=True, text=True, check=True)
    return probe.stdout.strip().startswith("yuva")


def materialize(src: Path, archive: Path, sound: Path) -> dict:
    head = bink_header(src)
    w, h = head["width"], head["height"]
    alpha = has_alpha(src)
    fmt = "yuva420p" if alpha else "yuv420p"
    cw, ch = (w + 1) // 2, (h + 1) // 2
    frame_size = w * h + 2 * cw * ch + (w * h if alpha else 0)
    proc = subprocess.Popen(["ffmpeg", "-v", "error", "-i", str(src), "-map", "0:v:0", "-f", "rawvideo", "-pix_fmt", fmt,
                             "-"], stdout=subprocess.PIPE)
    archive.parent.mkdir(parents=True, exist_ok=True)
    blobs: list[bytes] = []
    assert proc.stdout is not None
    while True:
        raw = proc.stdout.read(frame_size)
        if len(raw) < frame_size:
            break
        p = np.frombuffer(raw, dtype=np.uint8)
        y = p[:w * h].reshape(h, w)
        u = p[w * h:w * h + cw * ch].reshape(ch, cw)
        v = p[w * h + cw * ch:w * h + 2 * cw * ch].reshape(ch, cw)
        a = p[w * h + 2 * cw * ch:].reshape(h, w) if alpha else None
        blobs.append(zlib.compress(to_bgra(y, u, v, a), 6))
    if proc.wait() != 0:
        raise SystemExit(f"{src}: ffmpeg failed")
    if len(blobs) != head["frames"]:
        raise SystemExit(f"{src}: decoded {len(blobs)} frames, header says {head['frames']}")
    table_size = 4 + 6 * 4 + len(blobs) * 12
    with archive.open("wb") as f:
        f.write(b"BFA1" + struct.pack("<6I", w, h, len(blobs), head["rate_num"], head["rate_den"], 4))
        offset = table_size
        for blob in blobs:
            f.write(struct.pack("<QI", offset, len(blob)))
            offset += len(blob)
        for blob in blobs:
            f.write(blob)
    audio = subprocess.run(["ffprobe", "-v", "error", "-select_streams", "a", "-show_entries", "stream=index",
                            "-of", "csv=p=0", str(src)], capture_output=True, text=True, check=True).stdout.strip()
    if audio:
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(src), "-map", "0:a:0", "-c:a", "libvorbis", "-q:a", "6",
                        str(sound)], check=True)
    return {**head, "archive": str(archive), "sound": str(sound) if audio else None, "alpha": alpha}


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("out", type=Path)
    ap.add_argument("--game-dir", type=Path, default=DEFAULT_GAME)
    ap.add_argument("--only", nargs="*", help="film names (relative paths without .vid), e.g. LTLogo cutscenes/01")
    a = ap.parse_args(argv)
    for tool in ("ffmpeg", "ffprobe"):
        if not shutil.which(tool):
            raise SystemExit(f"{tool} not found")
    video = a.game_dir / "data" / "video"
    films = sorted(video.rglob("*.vid"))
    manifest = {}
    for src in films:
        name = src.relative_to(video).with_suffix("").as_posix()
        if a.only and name not in a.only:
            continue
        entry = materialize(src, a.out / f"{name}.bfa", a.out / f"{name}.ogg")
        manifest[name] = entry
        print(f"{name}: {entry['width']}x{entry['height']}, {entry['frames']} frames at "
              f"{entry['rate_num']}/{entry['rate_den']}{', sound' if entry['sound'] else ''}", flush=True)
    a.out.mkdir(parents=True, exist_ok=True)
    (a.out / "films.json").write_text(json.dumps(manifest, indent=1) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
