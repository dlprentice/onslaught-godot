"""Score a frontend capture against the retail reference set, and FAIL on regression.

Why this exists
---------------
The capture gate (`Capture-Frontend.ps1` then, `rebuild/tools/first_flight.py
capture` since 2026-09-26) reported PASS iff the capture run itself was
healthy: every shot landed on the expected screen, every file saved, every frame
was the requested size, and the plan ran to the end. Every one of those is a
property of OUR OWN run. The gate proved the capture executed. **It never
compared a single pixel against retail**, so it could not detect divergence from
the target - which is the only thing a "parity gate" appears to guarantee.

That is the "gate that cannot see the thing it guards" class, and it is worse
than an individual pixel defect: while it stands, every frontend change is
unguarded against regression, and "13 startup shots byte-identical" means only
that we have not changed, not that we are right.

This tool supplies the missing half. `first_flight.py capture` calls it, and
its verdict is folded into the capture's status.

What is scored, and what deliberately is not
--------------------------------------------
Only pages whose retail reference can actually pin them. Measured 2026-07-26
(local-lab/STARTUP-NOFMV-BASELINE-2026-07-26.md section 2.2), retail's own two
runs disagree with each other by:

    FEP_MAIN          0.11 - 1.46 %   phase-reproducible from the click anchor
    FEP_DEVSELECT     5.8  - 44.2 %   drifts
    FEP_LEVEL_SELECT  9.6  - 62.5 %   drifts

A page carrying 5.8-62 % of intrinsic disagreement with its own twin cannot
score a reconstruction improvement below that, so gating on it would be theatre.
Those pages are declared UNSCORED in the plan, with a reason and a task id, and
they are reported as UNSCORED - never silently skipped and never counted as a
pass. A run in which nothing at all was scorable returns UNSCORED, not PASS,
because "no evidence" must never render as "no problem". That rule is the whole
point of the tool.

The statistic
-------------
The gated number is `gapPct` from `compare_capture.py`: the share of pixels where
the candidate differs materially from the retail reference AND retail's own
second run does not. It is per-pixel and floor-aware, so animated content retail
cannot itself reproduce is not charged to us, while a layer we simply failed to
draw still is. `materialPct` and `meanD` against the reference are reported
beside it for continuity with the hand measurements, but they are not the gate.

Thresholds are REGRESSION CEILINGS, not parity claims
-----------------------------------------------------
Each gated region carries a `measured` value - what the build scored when it was
last measured - and the ceiling is DERIVED from it here as
`min(measured + marginPp, 100)`. The ceiling says "do not get worse than this".
It does not say the page is correct: FEP_MAIN's title logo and top-right emblem
are both ~20-29 % wrong and their ceilings encode exactly that. Reading a ceiling
as a parity statement is the mistake this docstring exists to prevent, and this
docstring is the canonical statement of it - other files should point here rather
than restate it.

The ceiling is derived rather than stored because it was briefly both: the plan
carried 30 `regressionCeiling` values, every one exactly `measured + 2.0`. Two
copies of one fact drift - re-measure after a fix, forget to regenerate, and the
gate silently guards the old build. `measured` is the fact; the margin is policy.

Honesty rules baked in
----------------------
* Frames of different sizes are refused, exactly as `compare_capture.py` refuses
  them. No resampling, ever - rescaling hides the layout error this hunts.
* Pairing is nearest-offset within a stated tolerance, and the realised pairing
  error is REPORTED for every pair. A pair outside tolerance is an error, not a
  quietly-used approximation.
* A missing reconstruction shot or a missing retail frame is an error. Nothing
  is interpolated or substituted.
* Exit code is non-zero when any gated region exceeds its ceiling, so a caller
  that ignores the JSON still cannot ignore the verdict.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from compare_capture import gap_pct, load_rgb, region_stats  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_PLAN = REPO_ROOT / "rebuild" / "tools" / "frontend-parity-plan.json"

FULL_FRAME = "FULL FRAME"


def offsets_in(directory: Path, prefix: str) -> dict[int, Path]:
    """Map millisecond offset -> file for `<prefix>-t<offset>ms.png` frames.

    Both retail's burst captures and the rig's `mainmenu` sweep use this
    convention with the same origin (the moment the page is first drawn), which
    is what makes pairing arithmetic rather than judgement.
    """
    found: dict[int, Path] = {}
    for path in sorted(directory.glob(f"{prefix}-t*ms.png")):
        stem = path.stem
        digits = stem[len(prefix) + 2 : -2]
        if digits.isdigit():
            found[int(digits)] = path
    return found


def nearest(offsets: dict[int, Path], want: int) -> tuple[int, Path]:
    key = min(offsets, key=lambda o: abs(o - want))
    return key, offsets[key]


def score_page(
    page: dict,
    capture_dir: Path,
    reference_root: Path,
    tolerance_ms: int,
    plan_margin: float,
) -> dict:
    """Score one page. Returns a result dict; never raises for data problems,
    because a page that cannot be scored must be REPORTED as unscorable rather
    than aborting the run and leaving the other pages unmeasured."""
    result: dict = {
        "id": page["id"],
        "verdict": "PASS",
        "errors": [],
        "samples": [],
    }

    regions_path = REPO_ROOT / page["regions"]
    if not regions_path.is_file():
        result["verdict"] = "ERROR"
        result["errors"].append(f"missing region file {regions_path}")
        return result
    regions: dict[str, list[int]] = {
        name: box
        for name, box in json.loads(regions_path.read_text(encoding="utf-8")).items()
        if not name.startswith("_")
    }

    ref_dir = reference_root / page["reference"]["run"]
    floor_dir = reference_root / page["noiseFloor"]["run"]
    ref_offsets = offsets_in(ref_dir, page["reference"]["prefix"])
    floor_offsets = offsets_in(floor_dir, page["noiseFloor"]["prefix"])
    cap_offsets = offsets_in(capture_dir, page["shotPrefix"])

    for store, label, where in (
        (ref_offsets, "reference", ref_dir),
        (floor_offsets, "noiseFloor", floor_dir),
        (cap_offsets, "capture", capture_dir),
    ):
        if not store:
            result["verdict"] = "ERROR"
            result["errors"].append(f"no {label} frames under {where}")
    if result["verdict"] == "ERROR":
        return result

    # Ceilings are DERIVED from `measured`, not stored beside it.
    #
    # They used to be a second block of 30 numbers in the plan, every one of them
    # exactly min(measured + marginPp, 100). Two copies of the same fact drift:
    # re-measure after a fix, forget to regenerate, and the gate silently guards
    # the old build. `measured` is the fact; the ceiling is a policy applied to
    # it, so the policy is applied here.
    #
    # `regressionCeilingOverride` stays available for a region that genuinely
    # needs a tighter or looser bound than the blanket margin. There are none
    # today, and one should carry a comment saying why.
    margin = float(plan_margin)
    ceilings: dict[str, float] = {
        name: min(round(value + margin, 2), 100.0)
        for name, value in page["measured"].items()
    }
    ceilings.update(page.get("regressionCeilingOverride", {}))
    result["marginPp"] = margin
    result["regressionCeiling"] = ceilings

    unknown = sorted(set(ceilings) - set(regions) - {FULL_FRAME})
    if unknown:
        result["verdict"] = "ERROR"
        result["errors"].append(
            f"regressionCeiling names no region in {regions_path.name}: {unknown}")
        return result

    for want_ms in page["samples"]:
        ref_ms, ref_path = nearest(ref_offsets, want_ms)
        floor_ms, floor_path = nearest(floor_offsets, want_ms)
        cap_ms, cap_path = nearest(cap_offsets, want_ms)

        sample: dict = {
            "requestedOffsetMs": want_ms,
            "referenceOffsetMs": ref_ms,
            "noiseFloorOffsetMs": floor_ms,
            "captureOffsetMs": cap_ms,
            "pairingErrorMs": cap_ms - ref_ms,
            "noiseFloorPairingErrorMs": floor_ms - ref_ms,
            "reference": str(ref_path),
            "noiseFloorFrame": str(floor_path),
            "candidate": str(cap_path),
            "regions": {},
        }

        worst = max(abs(cap_ms - ref_ms), abs(floor_ms - ref_ms))
        if worst > tolerance_ms:
            sample["error"] = (
                f"pairing error {worst} ms exceeds tolerance {tolerance_ms} ms")
            result["errors"].append(f"{want_ms} ms: {sample['error']}")
            result["verdict"] = "ERROR"
            result["samples"].append(sample)
            continue

        ref_img = load_rgb(ref_path)
        cap_img = load_rgb(cap_path)
        floor_img = load_rgb(floor_path)
        sizes = {ref_img.size, cap_img.size, floor_img.size}
        if len(sizes) != 1:
            sample["error"] = (
                f"REFUSED size mismatch reference={ref_img.size} "
                f"candidate={cap_img.size} noiseFloor={floor_img.size}")
            result["errors"].append(f"{want_ms} ms: {sample['error']}")
            result["verdict"] = "ERROR"
            result["samples"].append(sample)
            continue

        # Boxes are passed through unconverted: compare_capture accepts either a
        # single rectangle or a list of them (a disjoint union), and wrapping in
        # tuple() here would have to know which it got.
        boxes = {FULL_FRAME: [0, 0, ref_img.size[0], ref_img.size[1]], **regions}
        for name, box in boxes.items():
            stats = region_stats(ref_img, cap_img, box)
            stats["gapPct"] = gap_pct(ref_img, cap_img, floor_img, box)
            stats["floorPct"] = region_stats(ref_img, floor_img, box)["materialPct"]
            sample["regions"][name] = stats

        result["samples"].append(sample)

    if result["verdict"] == "ERROR":
        return result

    # A ceiling is applied to the WORST sample, not the mean. A regression that
    # only shows at one video phase is still a regression, and averaging it
    # against four good phases is how it would get through.
    #
    # The worst gap is computed for EVERY region, whether gated or not, so that
    # --measure can re-derive the ceilings after a fix lands without needing a
    # second tool, and so an ungated region's number is still on the record.
    worst_by_region: dict[str, float] = {}
    for name in [FULL_FRAME, *regions]:
        observed = [s["regions"][name]["gapPct"] for s in result["samples"]
                    if name in s.get("regions", {})]
        if observed:
            worst_by_region[name] = max(observed)
    result["worstGapPct"] = worst_by_region

    breaches = []
    for name, ceiling in ceilings.items():
        if name not in worst_by_region:
            continue
        entry = {"region": name,
                 "worstGapPct": worst_by_region[name],
                 "regressionCeiling": ceiling}
        if worst_by_region[name] > ceiling:
            breaches.append(entry)
        result.setdefault("gated", []).append(entry)

    ungated = sorted(set(worst_by_region) - set(ceilings))
    if ungated:
        result["ungatedRegions"] = ungated

    if breaches:
        result["verdict"] = "FAIL"
        result["breaches"] = breaches
    return result


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--capture-dir", required=True, type=Path,
                    help="a directory written by first_flight.py capture")
    ap.add_argument("--plan", type=Path, default=DEFAULT_PLAN)
    ap.add_argument("--json-out", type=Path)
    ap.add_argument("--tolerance-ms", type=int, default=25,
                    help="retail's stated matched-offset validity (default 25)")
    ap.add_argument("--measure", action="store_true",
                    help="print a ready-to-paste `measured`/`regressionCeiling` "
                         "block for each page instead of only gating. Use this "
                         "to re-derive ceilings after a fix lands - a ceiling "
                         "must always be set from a measurement of the build it "
                         "will guard, never guessed and never nudged to make a "
                         "failing run pass.")
    args = ap.parse_args(argv)

    plan = json.loads(args.plan.read_text(encoding="utf-8"))
    manifest_path = args.capture_dir / "capture-manifest.json"
    if not manifest_path.is_file():
        print(f"no capture-manifest.json under {args.capture_dir}", file=sys.stderr)
        return 2
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    capture_plan = manifest.get("plan")

    measurement = plan.get("_measurementProvenance", {})
    if measurement.get("status", "CURRENT") == "STALE" and not args.measure:
        report = {
            "verdict": "UNSCORED",
            "reason": measurement.get(
                "staleReason", "frontend parity measurements are stale"),
            "captureDir": str(args.capture_dir),
            "capturePlan": capture_plan,
            "measurementStatus": "STALE",
            "pages": [],
        }
        print(f"UNSCORED: {report['reason']}")
        if args.json_out:
            args.json_out.parent.mkdir(parents=True, exist_ok=True)
            args.json_out.write_text(json.dumps(report, indent=2), encoding="utf-8")
        return 0

    reference_root = REPO_ROOT / plan["referenceRoot"]
    if not reference_root.is_dir():
        # The reference set is retail-derived and lives under the gitignored
        # local-lab, so a fresh clone legitimately has none of it. That is an
        # UNSCORED run, not a pass and not a crash.
        report = {
            "verdict": "UNSCORED",
            "reason": f"retail reference set not present at {reference_root}",
            "captureDir": str(args.capture_dir),
            "capturePlan": capture_plan,
            "pages": [],
        }
        print(f"UNSCORED: {report['reason']}")
        if args.json_out:
            args.json_out.parent.mkdir(parents=True, exist_ok=True)
            args.json_out.write_text(json.dumps(report, indent=2), encoding="utf-8")
        return 0

    margin_pp = float(measurement.get("marginPp", 2.0))
    applicable = [p for p in plan["pages"] if p["capturePlan"] == capture_plan]
    unscored = [u for u in plan.get("unscored", []) if u.get("capturePlan") == capture_plan]

    report = {
        "verdict": "UNSCORED",
        "captureDir": str(args.capture_dir),
        "capturePlan": capture_plan,
        "capturePurpose": manifest.get("capturePurpose"),
        "sourceCommit": manifest.get("sourceCommit"),
        "plan": str(args.plan),
        "referenceRoot": str(reference_root),
        "toleranceMs": args.tolerance_ms,
        "pages": [],
        "unscored": unscored,
    }

    print(f"capture   : {args.capture_dir}")
    print(f"  plan={capture_plan} purpose={manifest.get('capturePurpose')} "
          f"commit={str(manifest.get('sourceCommit'))[:8]}")
    print(f"reference : {reference_root}")
    print()

    for page in applicable:
        result = score_page(
            page, args.capture_dir, reference_root, args.tolerance_ms, margin_pp)
        report["pages"].append(result)

        print(f"{result['id']}: {result['verdict']}")
        for sample in result["samples"]:
            if "error" in sample:
                print(f"  t{sample['requestedOffsetMs']:06d}  {sample['error']}")
                continue
            full = sample["regions"][FULL_FRAME]
            print(f"  t{sample['requestedOffsetMs']:06d}"
                  f"  ref t{sample['referenceOffsetMs']:06d}"
                  f"  errMs {sample['pairingErrorMs']:+5d}"
                  f"  material {full['materialPct']:6.2f}%"
                  f"  floor {full['floorPct']:5.2f}%"
                  f"  GAP {full['gapPct']:6.2f}%"
                  f"  meanD {full['meanAbsDelta']:5.2f}")
        for entry in result.get("gated", []):
            mark = "BREACH" if entry in result.get("breaches", []) else "ok"
            print(f"    {entry['region'].ljust(20)} worst gap {entry['worstGapPct']:6.2f}%"
                  f"  ceiling {entry['regressionCeiling']:6.2f}%  {mark}")
        for err in result["errors"]:
            print(f"    ERROR {err}")

        if args.measure and result.get("worstGapPct"):
            # Only `measured` is printed. The ceiling is derived from it at
            # load time, so pasting a second block back into the plan would
            # recreate the drift this removed.
            print(f'    "measured": {json.dumps(result["worstGapPct"])},')
        print()

    for entry in unscored:
        print(f"{entry['id']}: UNSCORED - {entry['reason']}")
    if unscored:
        print()

    verdicts = [p["verdict"] for p in report["pages"]]
    if "ERROR" in verdicts:
        report["verdict"] = "ERROR"
    elif "FAIL" in verdicts:
        report["verdict"] = "FAIL"
    elif verdicts:
        report["verdict"] = "PASS"
    else:
        report["verdict"] = "UNSCORED"
        report["reason"] = f"no plan page targets capture plan '{capture_plan}'"

    print(f"VERDICT: {report['verdict']}  "
          f"({len(report['pages'])} page(s) scored, {len(unscored)} declared unscored)")

    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(json.dumps(report, indent=2), encoding="utf-8")

    return 0 if report["verdict"] in ("PASS", "UNSCORED") else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
