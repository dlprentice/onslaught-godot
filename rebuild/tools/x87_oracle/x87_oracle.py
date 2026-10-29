#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Oracle tables for the parity core's x87 numerics: VC6-compiled results at 24-bit precision control.

    python rebuild/tools/x87_oracle/x87_oracle.py build             compile harness.c and trunc.c (VC6 SP5, /O2 /QIfist)
    python rebuild/tools/x87_oracle/x87_oracle.py generate [--count N] [--ops a,b]   inputs + oracle outputs

The pinned VC6 SP5 toolchain comes from a private copy of the VC6 SP5 toolchain in the canonical checkout; Wine runs
in this tool's own prefix (X87_ORACLE_WINEPREFIX, made once as a copy of the prefix X87_ORACLE_BASE_PREFIX names, which
has the toolchain's Wine set-up) with no DISPLAY. Everything
it writes (the harness executable, inputs, outputs, manifest.json) goes to X87_ORACLE_OUT, by default the canonical
checkout's ignored local-data/parity-oracle/x87/ (set it to an internal-disk folder while archive I/O is limited).
The tables are results of our own compiled program, not bytes of the game. The parity tests read the same folder
(X87_ORACLE_OUT, else the canonical default) and skip when it is absent.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import random
import shutil
import struct
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
CANONICAL = ROOT.parents[1] if ROOT.parent.name == ".worktrees" else ROOT
VC6 = CANONICAL / "local-lab/third-party/msvc6-sp5-toolchain"
BASE_PREFIX_ENV = os.environ.get("X87_ORACLE_BASE_PREFIX", "")
PREFIX_ENV = os.environ.get("X87_ORACLE_WINEPREFIX", "")
BASE_PREFIX = Path(BASE_PREFIX_ENV)
PREFIX = Path(PREFIX_ENV)
OUT = Path(os.environ.get("X87_ORACLE_OUT", str(CANONICAL / "local-data/parity-oracle/x87")))
OPS = ("add_d", "sub_d", "mul_d", "div_d", "sqrt_d", "sin_d", "cos_d", "atan2_d",
       "add_f", "sub_f", "mul_f", "div_f", "sqrt_f", "sin_f", "cos_f", "atan2_f", "d2f", "fist", "trunc")


def winpath(p: Path) -> str:
    return "Z:" + str(p.resolve()).replace("/", "\\")


def prefix() -> Path:
    if not PREFIX_ENV or not BASE_PREFIX_ENV:
        raise SystemExit("set X87_ORACLE_WINEPREFIX (this tool's Wine prefix) and X87_ORACLE_BASE_PREFIX (the prefix to copy)")
    if not PREFIX.exists():
        staging = PREFIX.with_name(PREFIX.name + ".partial")
        shutil.rmtree(staging, ignore_errors=True)
        shutil.copytree(BASE_PREFIX, staging, symlinks=True,
                        ignore=shutil.ignore_patterns(".bea-decomp.lock", "ONSLAUGHT2"))
        staging.rename(PREFIX)
    return PREFIX


def env() -> dict[str, str]:
    e = dict(os.environ, WINEPREFIX=str(prefix()), WINEDEBUG="-all", WINEDLLOVERRIDES="mscoree,mshtml=",
             WINEPATH=f"{winpath(VC6 / 'VC98/Bin')};{winpath(VC6 / 'Common/MSDev98/Bin')}",
             INCLUDE=winpath(VC6 / "VC98/Include"), LIB=winpath(VC6 / "VC98/Lib"))
    e.pop("DISPLAY", None)
    e.pop("WAYLAND_DISPLAY", None)
    return e


def wine(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(["wine", *args], env=env(), cwd=OUT, capture_output=True, text=True, timeout=1800)


def build() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    cl = str(VC6 / "VC98/Bin/CL.EXE")
    steps = [
        [cl, "/nologo", "/O2", "/QIfist", "/MT", "/c", winpath(HERE / "harness.c"), f"/Fo{winpath(OUT)}\\"],
        [cl, "/nologo", "/O2", "/MT", "/c", winpath(HERE / "trunc.c"), f"/Fo{winpath(OUT)}\\"],
        [str(VC6 / "VC98/Bin/LINK.EXE"), "/nologo", "/subsystem:console", f"/out:{winpath(OUT / 'harness.exe')}",
         winpath(OUT / "harness.obj"), winpath(OUT / "trunc.obj")],
    ]
    for step in steps:
        result = wine(step)
        print(result.stdout + result.stderr, end="")
        if result.returncode != 0:
            return result.returncode
    print(f"built {OUT / 'harness.exe'} sha256 {hashlib.sha256((OUT / 'harness.exe').read_bytes()).hexdigest()}")
    return 0


def f32(x: float) -> float:
    return struct.unpack("<f", struct.pack("<f", x))[0]


def next_f32(x: float) -> float:
    bits = struct.unpack("<I", struct.pack("<f", x))[0]
    return struct.unpack("<f", struct.pack("<I", bits + 1))[0]


def rand_float(r: random.Random) -> float:
    """A float32-representable value over a wide magnitude range, either sign."""
    return f32(math.ldexp(r.uniform(1.0, 2.0), r.randint(-40, 40)) * r.choice((1, -1)))


def rand_double(r: random.Random) -> float:
    """A full 53-bit double (like a double constant or a double local), wide magnitudes."""
    return math.ldexp(r.uniform(1.0, 2.0), r.randint(-40, 40)) * r.choice((1, -1))


def midpoint(r: random.Random) -> float:
    """An exact midpoint between two adjacent float32 values (a 25-bit number)."""
    f = abs(rand_float(r))
    return (f + next_f32(f)) / 2 * r.choice((1, -1))


SPECIALS = [0.0, -0.0, math.inf, -math.inf, math.nan, 1.0, -1.0, 0.5, 2.0 ** -126, 2.0 ** -149, 3.4028234663852886e38,
            1e-45, 1e30, -1e30, 1e300, 1e-300]


def pairs(op: str, count: int, seed: int) -> list[tuple[float, float]]:
    r = random.Random(f"{op}:{seed}")
    out: list[tuple[float, float]] = [(a, b) for a in SPECIALS for b in SPECIALS]
    floats_only = op.endswith("_f") and op not in ("sqrt_f", "sin_f", "cos_f", "atan2_f")
    while len(out) < count:
        kind = r.random()
        if op in ("add_d", "sub_d", "add_f", "sub_f"):
            if not floats_only and kind < 0.25:
                m = midpoint(r)
                out.append((m, math.ldexp(m, -r.randint(26, 80)) * r.choice((1, -1, 0))))
            elif not floats_only and kind < 0.5:
                out.append((rand_double(r), rand_double(r)))
            elif kind < 0.75:
                a = rand_float(r)
                out.append((a, f32(math.ldexp(a, -r.randint(0, 30)) * r.uniform(-2, 2))))
            else:
                out.append((rand_float(r), rand_float(r)))
        elif op in ("mul_d", "div_d", "mul_f", "div_f"):
            if not floats_only and kind < 0.25:
                eps = r.choice((2.0 ** -52, -(2.0 ** -53), 2.0 ** -40, -(2.0 ** -45)))
                out.append((midpoint(r), 1.0 + eps) if op == "mul_d" else (midpoint(r) * (1.0 + eps), 1.0 + eps))
            elif not floats_only and kind < 0.5:
                out.append((rand_double(r), rand_double(r)))
            else:
                out.append((rand_float(r), rand_float(r)))
        elif op in ("sqrt_d", "sqrt_f"):
            v = rand_double(r) if kind < 0.5 else rand_float(r)
            out.append((abs(v) if kind < 0.95 else -abs(v), 0.0))
        elif op in ("sin_d", "sin_f", "cos_d", "cos_f"):
            if kind < 0.5:
                v = r.uniform(-2 * math.pi, 2 * math.pi)
            elif kind < 0.8:
                v = f32(r.uniform(-2 * math.pi, 2 * math.pi))
            elif kind < 0.95:
                v = r.uniform(-1e4, 1e4)
            else:
                v = math.ldexp(r.uniform(1, 2), r.randint(-60, 0)) * r.choice((1, -1))
            out.append((v, 0.0))
        elif op in ("atan2_d", "atan2_f"):
            if kind < 0.1:
                out.append((r.choice((0.0, -0.0, 1.0, -1.0)) * r.uniform(0, 10), r.choice((0.0, -0.0, 1.0, -1.0)) * r.uniform(0, 10)))
            elif kind < 0.5:
                out.append((f32(r.uniform(-1000, 1000)), f32(r.uniform(-1000, 1000))))
            else:
                out.append((rand_double(r), rand_double(r)))
        elif op == "d2f":
            out.append((midpoint(r) if kind < 0.3 else rand_double(r) if kind < 0.9 else math.ldexp(r.uniform(1, 2), r.randint(-160, 140)), 0.0))
        else:  # fist, trunc
            if kind < 0.3:
                v = r.randint(-100000, 100000) + 0.5
            elif kind < 0.6:
                v = r.uniform(-1e6, 1e6)
            elif kind < 0.8:
                v = f32(r.uniform(-1e4, 1e4))
            else:
                v = r.uniform(-3e9, 3e9)
            out.append((v, 0.0))
    return out[:count]


def generate(count: int, ops: list[str], seed: int) -> int:
    exe = OUT / "harness.exe"
    if not exe.exists():
        print("build the harness first", file=sys.stderr)
        return 1
    manifest_path = OUT / "manifest.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else {}
    manifest["harnessSha256"] = hashlib.sha256(exe.read_bytes()).hexdigest()
    manifest["toolchain"] = "VC6 SP5 CL 12.00.8804 (/O2 /QIfist /MT; trunc.c without /QIfist), _PC_24, round to nearest"
    manifest.setdefault("ops", {})
    for op in ops:
        data = b"".join(struct.pack("<dd", a, b) for a, b in pairs(op, count, seed))
        (OUT / f"{op}.in.bin").write_bytes(data)
        result = wine([str(exe), op, winpath(OUT / f"{op}.in.bin"), winpath(OUT / f"{op}.out.bin")])
        print(result.stdout.strip() or result.stderr.strip())
        if result.returncode != 0:
            return result.returncode
        manifest["ops"][op] = {
            "count": count, "seed": seed,
            "inSha256": hashlib.sha256(data).hexdigest(),
            "outSha256": hashlib.sha256((OUT / f"{op}.out.bin").read_bytes()).hexdigest(),
        }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("command", choices=("build", "generate"))
    parser.add_argument("--count", type=int, default=1_000_000)
    parser.add_argument("--ops", default=",".join(OPS))
    parser.add_argument("--seed", type=int, default=20261015)
    args = parser.parse_args(argv)
    if args.command == "build":
        return build()
    ops = [op for op in args.ops.split(",") if op]
    unknown = [op for op in ops if op not in OPS]
    if unknown:
        parser.error(f"unknown ops: {unknown}")
    return generate(args.count, ops, args.seed)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
