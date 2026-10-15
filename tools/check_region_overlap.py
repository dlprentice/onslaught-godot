"""Geometry gate for parity region files: report every overlap, and enforce the
attribution-clean declaration.

Why this exists
---------------
A parity region file says "score this subsystem over these pixels". If two
regions share pixels, a change to ONE subsystem moves the OTHER's score, and a
critic reading the second number credits a builder for work it did not do.

That is not hypothetical here and it is not small. In
`rebuild/tools/gameplay-regions-level100.json`, commit `dba2948e` changed only
the compass gauge blend, yet moved the "terrain" box 22.35 -> 20.75. With the
overlapping "threat circle" box excised, real terrain moved 12.36 -> 12.27:
**94 % of that terrain "win" was the compass overlay.** The two boxes share
17,100 of terrain's 44,000 px.

Sweeping the same file mechanically on 2026-07-28 found two MORE overlapping
pairs that no document mentioned - `horizon ridge` with `cockpit frame (right)`
(1,800 px, 20 % of the cockpit box) and `horizon ridge` with `threat circle`
(1,600 px). One overlap had been found by hand, over weeks; the other two fell
out of a script in a second. That asymmetry is the whole argument for this file
existing.

What it enforces, and what it deliberately does not
---------------------------------------------------
`--report` (and the default listing) prints the pairwise intersection table for
ANY region file. Overlap is not automatically a defect: several frontend region
files use deliberately nested boxes (`progress-bar` contains
`progress-bar-middle`), and reshaping them would silently invalidate every
regression ceiling ever derived from them.

Enforcement therefore applies only to a file that DECLARES an `_attributionClean`
block, opting in. For such a file:

  1. `acknowledgedOverlaps` must match the file's real overlaps EXACTLY - same
     pairs, same pixel counts. A box moved so that it starts overlapping a
     neighbour fails here, which is the case that had never been caught.
  2. Every region named in `regions` must be pairwise disjoint from every other
     region in that set. This is the property that makes the set safe to
     attribute from.
  3. `parent` minus the union of `excises` must be EXACTLY the declared region -
     verified as a set of pixels, not asserted in a comment. `self` means the
     region is its own parent and excises nothing.
  4. The declared `pixels` count must equal the computed area.
  5. The rectangles making up one region must be pairwise disjoint among
     themselves, or every shared pixel is weighed twice by
     `compare_capture.region_stats`.

What it CANNOT certify, stated because the distinction has been muddled here:
disjointness is not subsystem purity. It proves no pixel is scored twice. It
proves nothing about whether a region contains one object or five - both cockpit
boxes are 19-27 % non-cockpit BY OBJECT and pass every check in this file.

Exit code is non-zero on any violation, so a caller that ignores the text still
cannot ignore the verdict.
"""
from __future__ import annotations

import argparse
import itertools
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

# Files this checks when invoked with no paths. The gameplay file is the one that
# opts into enforcement; the frontend ones are reported so a new overlap is at
# least visible in the output of a routine run.
DEFAULT_TARGETS = [
    REPO_ROOT / "rebuild" / "tools" / "gameplay-regions-level100.json",
    *sorted((REPO_ROOT / "rebuild" / "tools").glob("frontend-regions-*.json")),
]


def rects(box) -> list[tuple[int, int, int, int]]:
    """One `[x0,y0,x1,y1]` rectangle, or a list of them whose union is the region.

    Kept in step with `compare_capture.rects`. Duplicated rather than imported so
    this gate does not fail to run in an environment where Pillow is missing -
    a geometry check needs no imaging library, and a gate that cannot run is a
    gate that reports nothing.
    """
    if box and isinstance(box[0], (list, tuple)):
        return [tuple(part) for part in box]
    return [tuple(box)]


def cells(box) -> set[tuple[int, int]]:
    """The region as an explicit pixel set.

    Deliberately literal. These frames are 640x480 and the largest region is
    57,200 px, so exact set arithmetic costs milliseconds and removes every
    chance of an interval-algebra bug in the one tool whose entire job is to be
    right about geometry.
    """
    out: set[tuple[int, int]] = set()
    for x0, y0, x1, y1 in rects(box):
        for y in range(y0, y1):
            for x in range(x0, x1):
                out.add((x, y))
    return out


def double_counted(box) -> int:
    """Pixels a region covers more than once across its own rectangles."""
    parts = rects(box)
    total = sum(max(0, x1 - x0) * max(0, y1 - y0) for x0, y0, x1, y1 in parts)
    return total - len(cells(box))


def scoring_regions(body: dict) -> dict:
    return {name: box for name, box in body.items() if not name.startswith("_")}


def check_file(path: Path) -> dict:
    result: dict = {
        "file": str(path),
        "verdict": "PASS",
        "errors": [],
        "overlaps": [],
        "enforced": False,
    }
    if not path.is_file():
        result["verdict"] = "ERROR"
        result["errors"].append(f"missing region file {path}")
        return result

    body = json.loads(path.read_text(encoding="utf-8"))
    regions = scoring_regions(body)
    if not regions:
        result["verdict"] = "ERROR"
        result["errors"].append("file declares no scoring regions")
        return result

    pixels = {name: cells(box) for name, box in regions.items()}
    for name, box in regions.items():
        dup = double_counted(box)
        if dup:
            result["verdict"] = "FAIL"
            result["errors"].append(
                f"'{name}' covers {dup} px more than once across its own "
                f"rectangles; every shared pixel would be weighed twice")
        result.setdefault("areas", {})[name] = len(pixels[name])

    clean_block = body.get("_attributionClean") or {}
    clean = clean_block.get("regions") or {}

    # The clean regions are SUBSETS of their parents by construction, so a
    # parent/child pair is not an overlap finding. Every other pair is.
    parent_of = {name: spec.get("parent") for name, spec in clean.items()}
    for a, b in itertools.combinations(sorted(regions), 2):
        shared = pixels[a] & pixels[b]
        if not shared:
            continue
        if parent_of.get(a) == b or parent_of.get(b) == a:
            continue
        result["overlaps"].append({
            "a": a,
            "b": b,
            "pixels": len(shared),
            "pctOfA": round(100.0 * len(shared) / len(pixels[a]), 2),
            "pctOfB": round(100.0 * len(shared) / len(pixels[b]), 2),
        })

    if not clean_block:
        # No opt-in: report and stop. See the module docstring for why an
        # undeclared overlap is not automatically a defect.
        return result

    result["enforced"] = True

    def fail(message: str) -> None:
        result["verdict"] = "FAIL"
        result["errors"].append(message)

    # 1. The acknowledged-overlap table must be the truth, exactly.
    declared = {
        (min(entry["a"], entry["b"]), max(entry["a"], entry["b"])): entry["pixels"]
        for entry in clean_block.get("acknowledgedOverlaps", [])
    }
    measured = {
        (min(o["a"], o["b"]), max(o["a"], o["b"])): o["pixels"]
        for o in result["overlaps"]
    }
    for pair in sorted(set(measured) - set(declared)):
        fail(f"UNDECLARED OVERLAP {pair[0]!r} x {pair[1]!r} = {measured[pair]} px. "
             "A region pair that shares pixels moves each other's score; declare "
             "it in _attributionClean.acknowledgedOverlaps or excise it.")
    for pair in sorted(set(declared) - set(measured)):
        fail(f"acknowledgedOverlaps claims {pair[0]!r} x {pair[1]!r} overlap, but "
             "they do not. A stale entry hides the next real one.")
    for pair in sorted(set(declared) & set(measured)):
        if declared[pair] != measured[pair]:
            fail(f"acknowledgedOverlaps says {pair[0]!r} x {pair[1]!r} = "
                 f"{declared[pair]} px; measured {measured[pair]} px.")

    # 2-5. The attribution-clean set.
    for name, spec in sorted(clean.items()):
        if name not in regions:
            fail(f"_attributionClean names {name!r}, which is not a region in "
                 "this file, so no tool can compute it.")
            continue
        parent = spec.get("parent")
        excises = spec.get("excises", [])
        if parent != "self" and parent not in regions:
            fail(f"{name!r} declares parent {parent!r}, which is not a region here.")
            continue
        want_pixels = spec.get("pixels")
        if want_pixels is not None and want_pixels != len(pixels[name]):
            fail(f"{name!r} declares {want_pixels} px; it is {len(pixels[name])} px.")

        if parent == "self":
            # A 'self' region carries no excision to verify. Its whole claim is
            # disjointness from the rest of the clean set, checked below.
            if excises:
                fail(f"{name!r} has parent 'self' but declares excisions {excises}.")
        else:
            missing = [e for e in excises if e not in regions]
            if missing:
                fail(f"{name!r} excises {missing}, which are not regions here.")
                continue
            expected = set(pixels[parent])
            for e in excises:
                expected -= pixels[e]
            if expected != pixels[name]:
                fail(f"{name!r} is not {parent!r} minus {excises}: "
                     f"{len(pixels[name] - expected)} px are in the region but not "
                     f"in that difference, {len(expected - pixels[name])} px the "
                     "other way. The declaration and the box disagree.")

    clean_names = sorted(n for n in clean if n in regions)
    for a, b in itertools.combinations(clean_names, 2):
        shared = pixels[a] & pixels[b]
        if shared:
            fail(f"ATTRIBUTION-CLEAN SET IS NOT DISJOINT: {a!r} and {b!r} share "
                 f"{len(shared)} px. A change inside one moves the other.")

    return result


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", type=Path,
                    help="region JSON files (default: the repo's own)")
    ap.add_argument("--json-out", type=Path)
    args = ap.parse_args(argv)

    targets = args.files or DEFAULT_TARGETS
    reports = [check_file(Path(t)) for t in targets]

    for report in reports:
        mode = "ENFORCED" if report["enforced"] else "report-only"
        print(f"{Path(report['file']).name}: {report['verdict']} ({mode})")
        for overlap in report["overlaps"]:
            print(f"    overlap  {overlap['a']:<32s} x {overlap['b']:<32s}"
                  f" {overlap['pixels']:7d} px"
                  f"  {overlap['pctOfA']:6.2f}% / {overlap['pctOfB']:6.2f}%")
        if not report["overlaps"]:
            print("    no overlapping pairs")
        for err in report["errors"]:
            print(f"    ERROR {err}")
        print()

    verdict = "PASS"
    if any(r["verdict"] == "ERROR" for r in reports):
        verdict = "ERROR"
    elif any(r["verdict"] == "FAIL" for r in reports):
        verdict = "FAIL"
    enforced = sum(1 for r in reports if r["enforced"])
    print(f"VERDICT: {verdict}  ({len(reports)} file(s) checked, "
          f"{enforced} enforced)")

    if args.json_out:
        args.json_out.parent.mkdir(parents=True, exist_ok=True)
        args.json_out.write_text(
            json.dumps({"verdict": verdict, "files": reports}, indent=2),
            encoding="utf-8")

    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
