#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Places the host's capture beside a reference capture of the same Present (retail's or the port's), with their
absolute difference below, and prints the mean absolute difference per channel. The images stay where they are given
(local scratch); nothing here reads or writes the repository."""

import argparse
import sys

from PIL import Image, ImageChops, ImageDraw


def load(path: str) -> Image.Image:
    return Image.open(path).convert("RGB")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ours")
    parser.add_argument("reference")
    parser.add_argument("output")
    parser.add_argument("--label", default="reference")
    args = parser.parse_args()

    ours = load(args.ours)
    ref = load(args.reference)
    if ref.size != ours.size:
        ref = ref.resize(ours.size, Image.NEAREST)
    diff = ImageChops.difference(ours, ref)
    w, h = ours.size
    sheet = Image.new("RGB", (w * 2, h * 2), "black")
    sheet.paste(ours, (0, 0))
    sheet.paste(ref, (w, 0))
    sheet.paste(diff, (0, h))
    draw = ImageDraw.Draw(sheet)
    draw.text((4, 4), "host", fill="yellow")
    draw.text((w + 4, 4), args.label, fill="yellow")
    draw.text((4, h + 4), "|difference|", fill="yellow")
    sheet.save(args.output)

    pixels = w * h
    means = [sum(i * n for i, n in enumerate(diff.getchannel(c).histogram())) / pixels for c in range(3)]
    print(f"mean |difference| R {means[0]:.2f} G {means[1]:.2f} B {means[2]:.2f} (0..255)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
