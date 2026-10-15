"""Pixel comparison between a retail reference frame and a reconstruction frame.

Parity claims in this project have historically cited code paths. This tool exists
so they can cite pixels instead.

Design notes:

* It REFUSES to compare frames of different sizes rather than resampling. Rescaling
  is precisely what hides sub-pixel and layout error, which is the class of defect
  this is meant to surface.
* It reports per-region deltas, not a single global number. A global mean is
  dominated by whatever occupies the most area (usually the background) and will
  happily read "97% similar" while the menu is in the wrong place.
* It separates *structural* disagreement (a pixel differs at all) from *magnitude*
  (by how much), because a small uniform tint error and a misplaced element produce
  very different profiles and need different fixes.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image


def load_rgb(path: Path) -> Image.Image:
    if not path.is_file():
        raise SystemExit(f"missing image: {path}")
    return Image.open(path).convert("RGB")


def rects(box) -> list[tuple[int, int, int, int]]:
    """Normalise a region to a list of rectangles.

    A region is either one `[x0,y0,x1,y1]` rectangle or a LIST of them, whose
    union is the region. The union form exists because the only attribution-clean
    shapes this project has been able to establish are not rectangles: excising
    one measurement box from another leaves an L or a U, and until this existed
    `"terrain mid-band (clean)"` could only be described in prose. A region that
    no tool can compute is not a measurement, and the caveat block of
    `rebuild/tools/gameplay-regions-level100.json` carried exactly that for two
    days while the contaminated box it warns against went on being quoted.

    Rectangles in a union MUST be pairwise disjoint or their shared pixels are
    counted twice. `tools/check_region_overlap.py` enforces that mechanically;
    this function does not, because it is on the per-frame path.
    """
    if box and isinstance(box[0], (list, tuple)):
        return [tuple(part) for part in box]
    return [tuple(box)]


def region_pixels(image: Image.Image, box) -> list:
    parts = rects(box)
    if len(parts) == 1:
        return list(image.crop(parts[0]).get_flattened_data())
    out: list = []
    for part in parts:
        out.extend(image.crop(part).get_flattened_data())
    return out


def region_area(box) -> int:
    return sum(max(0, x1 - x0) * max(0, y1 - y0) for x0, y0, x1, y1 in rects(box))


def region_stats(ref: Image.Image, cmp_: Image.Image, box) -> dict:
    a = region_pixels(ref, box)
    b = region_pixels(cmp_, box)
    n = len(a)
    if n == 0:
        return {"pixels": 0}

    diffs = [abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2]) for p, q in zip(a, b)]
    changed = sum(1 for d in diffs if d > 0)
    # Threshold is on the SUMMED L1 delta across the three channels, so the
    # per-channel cliff sits at 8: 3*8 = 24 is not > 24, and 3*9 = 27 is.
    #
    # This comment used to read "below this is dither/AA noise". That was FALSE
    # and load-bearing, because it licensed reading a 0.00% as "no error".
    # Measured 2026-07-26 on retail t025065 against itself:
    #     uniform +8/channel  -> material 0.00%   (meanD 7.71)
    #     uniform +9/channel  -> material 89.89%  (meanD 8.66)
    #     multiplicative 1.03 -> material 0.00%   (meanD 3.18)
    # A uniform 3% shading error is not dither and it is not AA. material% is
    # BLIND to it while meanD rises monotonically throughout. So:
    #
    #   * material% is an ORDERING statistic, sound at a fixed threshold - rank
    #     order was invariant across thresholds 4..96 on every real capture pair
    #     tested. Use it to compare candidates.
    #   * material% is NOT a shading metric. For a suspected gain or offset
    #     error, read meanD, which does not have this blind spot.
    material = sum(1 for d in diffs if d > 24)

    def mean(xs):
        return sum(xs) / len(xs)

    return {
        "pixels": n,
        "changedPct": round(100.0 * changed / n, 2),
        "materialPct": round(100.0 * material / n, 2),
        "meanAbsDelta": round(mean(diffs) / 3.0, 2),
        "maxAbsDelta": max(diffs) // 3,
        "refMeanRGB": [round(mean([p[i] for p in a]), 1) for i in range(3)],
        "cmpMeanRGB": [round(mean([p[i] for p in b]), 1) for i in range(3)],
    }


def gap_pct(
    ref: Image.Image,
    cmp_: Image.Image,
    floor: Image.Image,
    box,
) -> float:
    """Percentage of pixels where the candidate differs materially from the
    reference AND retail's own second run does not.

    `floor` is a second retail capture of the same frame. A pixel that moves
    between two retail runs is one this frame cannot pin down -- an animated
    background, a rotating model, a talking portrait -- so a difference there
    is not evidence against the reconstruction. A pixel that is stable across
    retail runs but differs in ours is.

    This is deliberately asymmetric: it never credits the reconstruction for a
    layer it failed to draw, because an omitted layer produces a difference at
    pixels that may well be floor-stable, and those still count.
    """
    a = region_pixels(ref, box)
    b = region_pixels(cmp_, box)
    f = region_pixels(floor, box)
    n = len(a)
    if n == 0:
        return 0.0

    def material(p, q) -> bool:
        return abs(p[0] - q[0]) + abs(p[1] - q[1]) + abs(p[2] - q[2]) > 24

    gap = sum(
        1
        for pa, pb, pf in zip(a, b, f)
        if material(pa, pb) and not material(pa, pf)
    )
    return round(100.0 * gap / n, 2)


def capture_source_commit(path: Path):
    """The build a captured frame came from, if it can be established.

    Both capture rigs write a `capture-manifest.json` beside their frames
    carrying `sourceCommit`. This tool takes bare image paths, so that stamp is
    the only way it can tell whether two frames describe the same build.
    Returns (commit, manifest_path); (None, None) when there is no manifest -
    a retail reference has none, and that is correct rather than a problem.
    """
    manifest = path.parent / "capture-manifest.json"
    if not manifest.is_file():
        return None, None
    try:
        body = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None, None
    return body.get("sourceCommit"), manifest


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--reference", required=True, type=Path)
    ap.add_argument("--candidate", required=True, type=Path)
    ap.add_argument("--regions", type=Path,
                    help="JSON: {name: [x0,y0,x1,y1], ...}. A value may also be "
                         "a LIST of rectangles, whose disjoint union is the "
                         "region - that is how a box with another box excised "
                         "from it is expressed. See tools/check_region_overlap.py.")
    ap.add_argument(
        "--noise-floor",
        type=Path,
        help="A SECOND reference frame of the same screen captured at a different "
             "moment. Animated elements differ between any two retail frames, so "
             "raw percentages overstate the real defect. Supplying this reports the "
             "gap ABOVE that floor, which is the part actually attributable to us.")
    ap.add_argument("--diff-image", type=Path, help="write an amplified difference image")
    ap.add_argument("--json-out", type=Path)
    args = ap.parse_args(argv)

    ref = load_rgb(args.reference)
    cmp_ = load_rgb(args.candidate)

    if ref.size != cmp_.size:
        print(
            f"REFUSED: size mismatch reference={ref.size} candidate={cmp_.size}.\n"
            "These frames are not comparable. Rescaling would conceal exactly the\n"
            "layout error this tool exists to find. Re-capture the reconstruction at\n"
            f"{ref.size[0]}x{ref.size[1]} (first_flight.py capture --size "
            f"{ref.size[0]}x{ref.size[1]}).",
            file=sys.stderr,
        )
        return 2

    floor = load_rgb(args.noise_floor) if args.noise_floor else None
    if floor is not None and floor.size != ref.size:
        print("REFUSED: --noise-floor frame is a different size to the reference.", file=sys.stderr)
        return 2

    # A NOISE FLOOR FROM A DIFFERENT BUILD IS NOT A NOISE FLOOR.
    #
    # The floor measures OUR OWN run-to-run variance, so that differences
    # retail cannot reproduce are not charged to us. A floor captured from a
    # DIFFERENT build measures the gap between two builds instead, and gapPct
    # then silently CREDITS the candidate for every change made in between.
    #
    # This already happened: the walker-diffuse floor pair sat 44 commits and
    # ~18 hours behind the captures it was applied to, and nothing noticed,
    # because this tool takes bare image paths and had no way to tell.
    #
    # Both rigs stamp sourceCommit into a sibling capture-manifest.json, so the
    # check is mechanical. Enforced only when BOTH sides carry one: a retail
    # reference has no manifest and must not be refused for lacking one.
    candidate_commit, candidate_manifest = capture_source_commit(args.candidate)
    floor_commit, floor_manifest = (
        capture_source_commit(args.noise_floor) if floor is not None
        else (None, None))
    if candidate_commit and floor_commit and candidate_commit != floor_commit:
        print(
            "REFUSED: the noise floor came from a different build." + chr(10) +
            f"  candidate  {candidate_commit}  ({candidate_manifest})" + chr(10) +
            f"  noiseFloor {floor_commit}  ({floor_manifest})" + chr(10) +
            "A floor from another build measures the difference between two "
            "builds, not this build's run-to-run variance, and subtracting it "
            "credits the candidate for every change made in between. "
            "Re-capture the floor against the build being measured.",
            file=sys.stderr,
        )
        return 2

    regions = {"FULL FRAME": [0, 0, ref.size[0], ref.size[1]]}
    if args.regions:
        # Keys beginning with "_" are metadata, not boxes. Region files carry
        # per-box caveats there -- several boxes are measurably contaminated by
        # a second object and must not be read as shading metrics -- and those
        # notes belong next to the numbers rather than in a document nobody
        # opens while measuring.
        regions.update({
            name: box
            for name, box in json.loads(args.regions.read_text(encoding="utf-8")).items()
            if not name.startswith("_")
        })

    report = {
        "reference": str(args.reference),
        "candidate": str(args.candidate),
        "size": list(ref.size),
        "regions": {},
    }

    width = max(len(k) for k in regions)
    if floor is None:
        print(f"{'region'.ljust(width)}  changed%  material%  meanD  maxD  refRGB -> cmpRGB")
        print("-" * (width + 56))
    else:
        print(f"{'region'.ljust(width)}  material%    floor%      GAP  meanD")
        print("-" * (width + 40))

    for name, box in regions.items():
        stats = region_stats(ref, cmp_, box)
        if floor is not None:
            fstats = region_stats(ref, floor, box)
            stats["floorPct"] = fstats["materialPct"]
            # gapPct is PER-PIXEL, and it must stay that way.
            #
            # It used to be a scalar subtraction of two independently computed
            # region percentages, which is unsound: the candidate can differ
            # from the reference on entirely different pixels than the floor
            # does and still score a zero gap. Worse, it actively REWARDED
            # omitting an animated layer -- a region where retail's own two runs
            # disagree (video background, rotating model, talking portrait)
            # carries a high floor, so a reconstruction that simply does not
            # draw that layer subtracts its way to zero and reads as perfect.
            #
            # The correct question is "where does the candidate differ from the
            # reference BEYOND what retail's own run-to-run variation explains",
            # which is a property of each pixel, not of two summary statistics.
            stats["gapPct"] = gap_pct(ref, cmp_, floor, box)
        report["regions"][name] = stats
        if not stats["pixels"]:
            continue
        if floor is None:
            print(
                f"{name.ljust(width)}  {stats['changedPct']:7.2f}  {stats['materialPct']:8.2f}"
                f"  {stats['meanAbsDelta']:5.1f}  {stats['maxAbsDelta']:4d}"
                f"  {stats['refMeanRGB']} -> {stats['cmpMeanRGB']}"
            )
        else:
            print(
                f"{name.ljust(width)}  {stats['materialPct']:8.2f}  {stats['floorPct']:8.2f}"
                f"  {stats['gapPct']:8.2f}  {stats['meanAbsDelta']:5.1f}"
            )

    if args.diff_image:
        # 4x amplification: real layout error saturates, dither noise stays dim.
        diff = Image.new("RGB", ref.size)
        rp = list(ref.get_flattened_data())
        cp = list(cmp_.get_flattened_data())
        diff.putdata([
            (min(255, abs(a[0] - b[0]) * 4),
             min(255, abs(a[1] - b[1]) * 4),
             min(255, abs(a[2] - b[2]) * 4))
            for a, b in zip(rp, cp)
        ])
        args.diff_image.parent.mkdir(parents=True, exist_ok=True)
        diff.save(args.diff_image)
        print(f"\ndiff image: {args.diff_image}")

    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(json.dumps(report, indent=2), encoding="utf-8")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
