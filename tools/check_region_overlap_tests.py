"""Self-tests for check_region_overlap.py and for union regions in compare_capture.

The defect this gate closes is that `rebuild/tools/gameplay-regions-level100.json`
declared overlapping scoring boxes, so a change to one subsystem moved another
subsystem's score, and nothing could say so. Shipping a checker that cannot fail
would be the same defect wearing the fix's clothes, so most of what is below is
FALSIFICATION: take the real declaration, break it one way at a time, and assert
the gate rejects and exits non-zero.

If `test_undeclared_overlap_is_caught` ever passes trivially, this gate is
decoration.
"""
from __future__ import annotations

import copy
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

from compare_capture import gap_pct, region_area, region_stats  # noqa: E402

TOOL = Path(__file__).resolve().parent / "check_region_overlap.py"
REAL = (Path(__file__).resolve().parent.parent
        / "rebuild" / "tools" / "gameplay-regions-level100.json")


def run(*paths: Path) -> tuple[int, str]:
    proc = subprocess.run(
        [sys.executable, str(TOOL), *[str(p) for p in paths]],
        capture_output=True, text=True)
    return proc.returncode, proc.stdout + proc.stderr


def mutate(root: Path, edit) -> Path:
    """Copy the real region file, apply `edit` to the parsed body, write it back."""
    body = json.loads(REAL.read_text(encoding="utf-8"))
    edit(body)
    path = root / "regions.json"
    path.write_text(json.dumps(body, indent=2), encoding="utf-8")
    return path


# --- the real file -----------------------------------------------------------

def test_the_shipped_region_file_passes(root: Path) -> None:
    code, out = run(REAL)
    assert code == 0, out
    assert "PASS (ENFORCED)" in out, out


def test_the_shipped_file_declares_all_three_overlaps(root: Path) -> None:
    """The sweep found three overlapping pairs; only one was documented before."""
    body = json.loads(REAL.read_text(encoding="utf-8"))
    declared = body["_attributionClean"]["acknowledgedOverlaps"]
    assert len(declared) == 3, declared
    pairs = {(min(e["a"], e["b"]), max(e["a"], e["b"])): e["pixels"] for e in declared}
    assert pairs[("terrain mid-band", "threat circle")] == 17100, pairs
    assert pairs[("cockpit frame (right)", "horizon ridge")] == 1800, pairs
    assert pairs[("horizon ridge", "threat circle")] == 1600, pairs


# --- falsification: the gate must reject each way of breaking it -------------

def test_undeclared_overlap_is_caught(root: Path) -> None:
    """Move a box so it touches a neighbour it did not touch before.

    This is the case that had never been caught: `horizon ridge` acquired an
    overlap with two other boxes and no document noticed for two days.
    """
    def edit(body):
        body["sky"] = [60, 20, 580, 200]  # now runs into 'horizon ridge'
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "UNDECLARED OVERLAP" in out, out


def test_wrong_acknowledged_pixel_count_is_caught(root: Path) -> None:
    def edit(body):
        body["_attributionClean"]["acknowledgedOverlaps"][0]["pixels"] = 17099
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "17099 px; measured 17100 px" in out, out


def test_stale_acknowledged_pair_is_caught(root: Path) -> None:
    """A declared overlap that is no longer real hides the next real one."""
    def edit(body):
        body["_attributionClean"]["acknowledgedOverlaps"].append(
            {"a": "sky", "b": "message panel", "pixels": 10})
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "but" in out and "they do not" in out, out


def test_excision_claim_is_verified_not_trusted(root: Path) -> None:
    """`terrain mid-band (clean)` must BE parent-minus-threat, not merely say so."""
    def edit(body):
        # Widen one sub-rectangle back into the threat circle by one column.
        body["terrain mid-band (clean)"][0] = [120, 230, 231, 340]
        body["_attributionClean"]["regions"]["terrain mid-band (clean)"]["pixels"] = 27010
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "is not 'terrain mid-band' minus" in out, out


def test_declared_pixel_count_must_match(root: Path) -> None:
    def edit(body):
        body["_attributionClean"]["regions"]["terrain mid-band (clean)"]["pixels"] = 26901
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "declares 26901 px; it is 26900 px" in out, out


def test_clean_set_must_be_mutually_disjoint(root: Path) -> None:
    """The whole point of the set. Break it and the gate must say so."""
    def edit(body):
        # Give 'threat circle (clean)' a rectangle inside 'terrain mid-band (clean)'.
        body["threat circle (clean)"].append([120, 230, 230, 340])
        clean = body["_attributionClean"]["regions"]["threat circle (clean)"]
        clean["pixels"] = 17400 + 12100
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "ATTRIBUTION-CLEAN SET IS NOT DISJOINT" in out, out


def test_self_overlapping_union_is_double_counting(root: Path) -> None:
    """Two rectangles of one region sharing pixels would weigh them twice."""
    def edit(body):
        body["terrain mid-band (clean)"].append([120, 230, 230, 340])
        body["_attributionClean"]["regions"]["terrain mid-band (clean)"]["pixels"] = 26900
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "more than once across its own" in out, out


def test_clean_region_must_exist_as_a_box(root: Path) -> None:
    """The exact defect being closed: a clean region described but not computable."""
    def edit(body):
        del body["terrain mid-band (clean)"]
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "no tool can compute it" in out, out


def test_parent_self_may_not_declare_excisions(root: Path) -> None:
    def edit(body):
        body["_attributionClean"]["regions"]["sky"]["excises"] = ["message panel"]
    code, out = run(mutate(root, edit))
    assert code == 1, out
    assert "parent 'self' but declares excisions" in out, out


# --- report-only files -------------------------------------------------------

def test_file_without_declaration_is_report_only(root: Path) -> None:
    """Frontend region files nest boxes deliberately; reshaping them would
    invalidate every ceiling derived from them. They are reported, not failed."""
    path = root / "nested.json"
    path.write_text(json.dumps({
        "outer": [0, 0, 100, 100],
        "inner": [10, 10, 20, 20],
    }), encoding="utf-8")
    code, out = run(path)
    assert code == 0, out
    assert "report-only" in out, out
    assert "100 px" in out, out  # inner is 10x10 and wholly inside outer


def test_the_gated_frontend_region_file_is_reported(root: Path) -> None:
    """The main-menu file IS gated, and its boxes overlap: title-logo shares
    26,100 px with bg-emblem-topright. That is not fixed here - it is surfaced,
    because the ceilings derived from those boxes are not independent."""
    menu = REAL.parent / "frontend-regions-main-menu.json"
    code, out = run(menu)
    assert code == 0, out
    assert "26100 px" in out, out


# --- union regions in compare_capture ---------------------------------------

def test_union_region_area_and_stats(root: Path) -> None:
    """A list-of-rectangles region must be measured over the union, and a plain
    rectangle must keep behaving exactly as before."""
    single = [0, 0, 10, 10]
    union = [[0, 0, 10, 10], [20, 0, 30, 10]]
    assert region_area(single) == 100
    assert region_area(union) == 200

    ref = Image.new("RGB", (40, 10), (0, 0, 0))
    cand = Image.new("RGB", (40, 10), (0, 0, 0))
    floor = Image.new("RGB", (40, 10), (0, 0, 0))
    # Make the SECOND rectangle of the union entirely wrong, the first correct.
    for x in range(20, 30):
        for y in range(10):
            cand.putpixel((x, y), (255, 255, 255))

    assert region_stats(ref, cand, single)["materialPct"] == 0.0
    stats = region_stats(ref, cand, union)
    assert stats["pixels"] == 200, stats
    assert stats["materialPct"] == 50.0, stats
    assert gap_pct(ref, cand, floor, union) == 50.0
    assert gap_pct(ref, cand, floor, single) == 0.0


def test_shipped_clean_region_areas(root: Path) -> None:
    body = json.loads(REAL.read_text(encoding="utf-8"))
    assert region_area(body["terrain mid-band (clean)"]) == 26900
    assert region_area(body["terrain mid-band"]) == 44000
    assert region_area(body["horizon ridge (clean)"]) == 15800
    assert region_area(body["threat circle (clean)"]) == 17400
    assert region_area(body["cockpit frame (right, clean)"]) == 7200


def main() -> int:
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    failed = 0
    for test in tests:
        root = Path(tempfile.mkdtemp(prefix="region-overlap-"))
        try:
            test(root)
            print(f"PASS {test.__name__}")
        except AssertionError as exc:
            failed += 1
            print(f"FAIL {test.__name__}: {exc}")
        finally:
            shutil.rmtree(root, ignore_errors=True)
    print(f"\n{len(tests) - failed}/{len(tests)} passed")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
