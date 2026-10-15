"""Compare a `-Plan options` capture against the retail FEP_OPTIONS frames.

Why this is not `tools/compare_capture.py`
------------------------------------------
It was tried first, and it cannot see this page. The Options pages sit on the
FEBack128 underlay, a ~19-second video loop, and the retail frames this tool
compares against were captured with `-skipfmv`, which SUPPRESSES that underlay
entirely. That shows up as a near-uniform background lift - measured, every
region of every page reads about +8 R/G and +18 B - which swamps every region
statistic. `compare_capture.py` reported 65-95% "material" on regions that are
in fact pixel-exact.

CORRECTED 2026-07-31 (task #150). This paragraph used to attribute that lift to
a PHASE mismatch between two running videos. It is not a phase difference; it is
the whole video, present on our side and absent on retail's. Measured on
options-root, full-frame mean RGB: retail `-skipfmv` (30.6, 31.8, 54.3), retail
WITH the video (38.2, 41.0, 68.7), ours (36.8, 39.5, 68.4). The retail reference
is the outlier, and our page sits within ~1.5 units of retail's video-on truth.
The conclusion below is unaffected - an ink mask is opaque glyph core either way
- but the reason mattered enough to get wrong twice, so it is stated correctly
here. Video-on references now exist at
local-lab/retail-reference-pristine/nofmv-options-2026-07-31/{run1,run2}.

What discriminates instead is an INK MASK: the pixels bright enough to be glyph
core rather than background or arc. Text either lands on retail's pixels or it
does not, and the underlay phase cannot move it.

This tool is deliberately capable of failing, and it did
--------------------------------------------------------
Run against the first build of the page it scored:

    bindings grid glyphs   44.19% IoU   (half-pixel text origin: every glyph
                                         resampled into a 5px-wide run where
                                         retail draws 4 full texels)

and after that was fixed but before the column offsets were,

    label / value columns  ~52-61% IoU  (one pixel out on all three columns)

Both were found by this number moving, not by looking at the page. Do not read a
passing number as parity: what it proves is that the ink is in retail's place,
not that the page is right.

What is deliberately excluded, with reasons
-------------------------------------------
* The top-left Forseti emblem. Unidentified art that NO page in this lane draws.
  Charging it to the options page would report the same constant on every page
  forever and hide real movement underneath it.
* The retail mouse cursor. Present in every retail frame at a different place
  on each. The capture rig pins our custom cursor at (0,0), outside every scored
  band, so live operator input cannot contaminate this comparison.
Both exclusions are boxes, stated below, not adaptive masks - an adaptive mask
would be able to excuse a real defect.

There is only one retail run of the `-skipfmv` frames THIS tool reads, so it has
no noise floor of its own. Two video-on retail runs of the same pages do now
exist (nofmv-options-2026-07-31/run1 and run2, task #150), but they are bursts
and our options capture is one settled shot per page, so there is still nothing
for `tools/score_frontend_capture.py` to pair; it reports the options plan as
UNSCORED, which is correct and must not be read as a pass. See the FEP_OPTIONS
entry in rebuild/tools/frontend-parity-plan.json for what would unblock it.

Usage:
    python ./rebuild/tools/compare_options_capture.py <capture-dir> [--min-iou N]

`--min-iou` is an explicitly supplied regression threshold, not a retail-parity
bar. No default floor is inferred from the single retained retail run.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
RETAIL = REPO_ROOT / "local-lab" / "retail-captures-options-pause-2026-07-27"

# page id -> (retail file, capture file, row-band y range, retail cursor box)
PAGES = {
    "root": ("fep-options-root-640x480.png", "fep-options-root.png", (240, 325), (215, 400, 250, 435)),
    "controller": ("fep-options-controller-640x480.png", "fep-options-controller.png", (105, 460), (325, 250, 365, 290)),
    "sound": ("fep-options-sound-640x480.png", "fep-options-sound.png", (195, 372), (315, 275, 355, 315)),
    "video": ("fep-options-video-640x480.png", "fep-options-video.png", (145, 422), (320, 285, 360, 325)),
}

BAND_X0, BAND_X1 = 40, 600

# Glyph core against the page fill and the arcs. The lit metal arc reads about
# (107,117,131), channel sum 355; the dimmest thing that must count as ink is the
# bindings grid's green (126,253,94), sum 473. 450 sits between them, so the arcs
# cannot drift in and out of the mask as the underlay phase moves.
INK_SUM = 450
INK_MAX = 150


def ink_mask(image: np.ndarray, band: tuple[int, int]) -> np.ndarray:
    y0, y1 = band
    window = image[y0:y1, BAND_X0:BAND_X1]
    return (window.sum(axis=2) > INK_SUM) & (window.max(axis=2) > INK_MAX)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_dir", type=Path)
    parser.add_argument(
        "--min-iou",
        type=float,
        default=0.0,
        help="Fail if any page's ink IoU falls below this percentage.",
    )
    parser.add_argument("--json-out", type=Path)
    args = parser.parse_args()

    report: dict = {
        "verdict": "UNSCORED",
        "scope": "options-ink-placement-only",
        "captureDir": str(args.capture_dir),
        "referenceRoot": str(RETAIL),
        "minimumIoUPct": args.min_iou,
        "pages": [],
        "errors": [],
    }

    def finish(verdict: str, exit_code: int, message: str | None = None) -> int:
        report["verdict"] = verdict
        if message:
            if verdict == "ERROR":
                report["errors"].append(message)
            else:
                report["reason"] = message
            print(f"{verdict}: {message}")
        if args.json_out:
            args.json_out.parent.mkdir(parents=True, exist_ok=True)
            args.json_out.write_text(json.dumps(report, indent=2), encoding="utf-8")
        return exit_code

    if not RETAIL.is_dir():
        return finish("UNSCORED", 0, f"no retail reference set at {RETAIL}")

    print(f"{'page':12s} {'inkIoU':>8s} {'retailInk':>10s} {'ourInk':>8s} "
          f"{'retailOnly':>11s} {'oursOnly':>9s}")
    worst = 100.0
    for page, (retail_name, our_name, band, cursor) in PAGES.items():
        retail_path = RETAIL / retail_name
        our_path = args.capture_dir / our_name
        if not retail_path.is_file():
            return finish("ERROR", 2, f"retail reference missing {retail_path}")
        if not our_path.is_file():
            return finish("ERROR", 2, f"capture missing {our_path}")

        try:
            retail = np.asarray(Image.open(retail_path).convert("RGB")).astype(int)
            ours = np.asarray(Image.open(our_path).convert("RGB")).astype(int)
        except OSError as exception:
            return finish("ERROR", 2, f"{page} image decode failed: {exception}")
        if retail.shape != ours.shape:
            return finish(
                "ERROR",
                2,
                f"{page} size mismatch {retail.shape} vs {ours.shape}; "
                "frames are never resampled",
            )

        a = ink_mask(retail, band)
        b = ink_mask(ours, band)

        y0, y1 = band
        rows = np.arange(y0, y1)[:, None]
        cols = np.arange(BAND_X0, BAND_X1)[None, :]
        cx0, cy0, cx1, cy1 = cursor
        keep = ~((rows >= cy0) & (rows < cy1) & (cols >= cx0) & (cols < cx1))
        # The emblem box, in the same coordinates.
        keep &= ~((rows >= 18) & (rows < 115) & (cols >= 40) & (cols < 175))
        a &= keep
        b &= keep

        if int(a.sum()) == 0:
            return finish(
                "ERROR",
                2,
                f"{page} retail reference ink mask is empty; comparison is unsound",
            )

        union = int((a | b).sum())
        iou = 100.0 * int((a & b).sum()) / union
        worst = min(worst, iou)
        page_result = {
            "id": page,
            "inkIoUPct": iou,
            "retailInk": int(a.sum()),
            "ourInk": int(b.sum()),
            "retailOnly": int((a & ~b).sum()),
            "oursOnly": int((b & ~a).sum()),
            "reference": str(retail_path),
            "capture": str(our_path),
        }
        report["pages"].append(page_result)
        print(f"{page:12s} {iou:7.2f}% {int(a.sum()):10d} {int(b.sum()):8d} "
              f"{int((a & ~b).sum()):11d} {int((b & ~a).sum()):9d}")

    if args.min_iou > 0.0:
        verdict = "PASS" if worst >= args.min_iou else "FAIL"
        report["worstInkIoUPct"] = worst
        print(
            f"\nworst page {worst:.2f}% against regression minimum "
            f"{args.min_iou:.2f}% -> {verdict}"
        )
        return finish(verdict, 0 if verdict == "PASS" else 1)

    report["worstInkIoUPct"] = worst
    print(f"\nworst page {worst:.2f}% (no regression minimum requested)")
    return finish(
        "UNSCORED",
        0,
        "measurement completed without a regression minimum",
    )


if __name__ == "__main__":
    sys.exit(main())
