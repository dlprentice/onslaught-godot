#!/usr/bin/env python3
"""Print mean RGB of named boxes for one or two frames, plus the per-channel ratio.

Read-only. Boxes are given as name=x0,y0,x1,y1 so a probe is reproducible from
the command line and nothing is hard-coded to one investigation.
"""

from __future__ import annotations

import argparse
import sys

import numpy as np
from PIL import Image


def load(path: str) -> np.ndarray:
    with Image.open(path) as image:
        return np.asarray(image.convert("RGB"), dtype=np.float64)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--reference", required=True)
    parser.add_argument("--candidate")
    parser.add_argument("--box", action="append", default=[],
                        help="name=x0,y0,x1,y1")
    args = parser.parse_args(argv)

    reference = load(args.reference)
    candidate = load(args.candidate) if args.candidate else None

    for spec in args.box:
        name, _, coords = spec.partition("=")
        x0, y0, x1, y1 = (int(v) for v in coords.split(","))
        ref = reference[y0:y1, x0:x1].reshape(-1, 3)
        ref_mean = ref.mean(axis=0)
        line = (f"{name:22s} retail {ref_mean[0]:6.1f} {ref_mean[1]:6.1f} "
                f"{ref_mean[2]:6.1f}")
        if candidate is not None:
            cand = candidate[y0:y1, x0:x1].reshape(-1, 3)
            cand_mean = cand.mean(axis=0)
            ratio = cand_mean / np.maximum(ref_mean, 1e-9)
            line += (f" | rebuild {cand_mean[0]:6.1f} {cand_mean[1]:6.1f} "
                     f"{cand_mean[2]:6.1f} | ratio {ratio[0]:.3f} "
                     f"{ratio[1]:.3f} {ratio[2]:.3f}")
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
