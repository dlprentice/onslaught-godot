"""Self-tests for score_frontend_capture.py.

The point of task #113 was that the frontend gate could not fail for the reason
it claimed to guard. Replacing it with a gate that also cannot fail would be the
same defect in a new coat, so the central test here is the FALSIFICATION one:
inject a deliberate regression and assert the gate reports FAIL and exits
non-zero. If `test_regression_is_caught` ever passes trivially, the gate is
worthless again.

These build their own images and plan in a temp directory, so they run in a
fresh clone with no retail material present - which is also what lets the
"no reference set" case be tested rather than assumed.
"""
from __future__ import annotations

import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

TOOL = Path(__file__).resolve().parent / "score_frontend_capture.py"

W, H = 40, 30
FILL = (23, 23, 48)
CHROME = (200, 180, 90)
CHROME_BOX = (4, 4, 20, 16)
# A patch that differs between the two retail runs: retail cannot reproduce it
# itself, so a difference there must NOT be charged to the reconstruction.
ANIMATED_BOX = (24, 18, 36, 28)

OFFSETS = [0, 500, 1000]


def write_frame(path: Path, chrome: tuple[int, int, int], animated: int) -> None:
    img = Image.new("RGB", (W, H), FILL)
    for x in range(*CHROME_BOX[0::2]):
        for y in range(*CHROME_BOX[1::2]):
            img.putpixel((x, y), chrome)
    for x in range(*ANIMATED_BOX[0::2]):
        for y in range(*ANIMATED_BOX[1::2]):
            img.putpixel((x, y), (animated, animated, animated))
    img.save(path)


REF_ANIMATED = 10
FLOOR_ANIMATED = 250


def build_fixture(
    root: Path,
    *,
    chrome: tuple[int, int, int],
    capture_animated: int = FLOOR_ANIMATED,
    floor_animated: int = FLOOR_ANIMATED,
) -> tuple[Path, Path]:
    """Lay out a retail reference set, a capture, and a plan.

    `chrome` is the colour the CAPTURE draws its chrome in; retail always draws
    CHROME, so a different value is a regression in a floor-stable region.

    `capture_animated` defaults to differing from run1 and MATCHING run2, so the
    animated patch is a real difference that the floor should excuse. It used to
    be hard-coded equal to run1's value, which made the difference zero and meant
    NO test exercised the floor at all while a comment claimed one did.

    `floor_animated` exists so a test can take the floor away - set it equal to
    run1 and the same candidate difference stops being excused. That pair is what
    actually demonstrates the floor, rather than one run that could pass for
    either reason."""
    ref_root = root / "reference"
    (ref_root / "run1").mkdir(parents=True)
    (ref_root / "run2").mkdir(parents=True)
    capture = root / "capture"
    capture.mkdir()

    for offset in OFFSETS:
        write_frame(ref_root / "run1" / f"mm-t{offset:06d}ms.png", CHROME, REF_ANIMATED)
        write_frame(ref_root / "run2" / f"mm-t{offset:06d}ms.png", CHROME, floor_animated)
        write_frame(capture / f"mainmenu-t{offset:06d}ms.png", chrome, capture_animated)

    (capture / "capture-manifest.json").write_text(json.dumps({
        "plan": "mainmenu", "capturePurpose": "production", "sourceCommit": "0" * 40,
    }), encoding="utf-8")

    regions = root / "regions.json"
    regions.write_text(json.dumps({"chrome": list(CHROME_BOX)}), encoding="utf-8")

    plan = root / "plan.json"
    plan.write_text(json.dumps({
        "referenceRoot": str(ref_root),
        "_measurementProvenance": {"marginPp": 2.0},
        "pages": [{
            "id": "TEST_PAGE",
            "capturePlan": "mainmenu",
            "shotPrefix": "mainmenu",
            "reference": {"run": "run1", "prefix": "mm"},
            "noiseFloor": {"run": "run2", "prefix": "mm"},
            "regions": str(regions),
            "samples": OFFSETS,
            # Ceilings are derived as measured + marginPp, so this is a 2.0
            # ceiling on both regions.
            "measured": {"FULL FRAME": 0.0, "chrome": 0.0},
        }],
        "unscored": [],
    }), encoding="utf-8")
    return plan, capture


def run(plan: Path, capture: Path, out: Path) -> tuple[int, dict]:
    proc = subprocess.run(
        [sys.executable, str(TOOL), "--capture-dir", str(capture),
         "--plan", str(plan), "--json-out", str(out)],
        capture_output=True, text=True, check=False)
    report = json.loads(out.read_text(encoding="utf-8")) if out.is_file() else {}
    return proc.returncode, report


def test_clean_capture_passes(root: Path) -> None:
    """Baseline: a capture that differs from retail ONLY where retail's own two
    runs also differ. The gap must be zero everywhere."""
    plan, capture = build_fixture(root, chrome=CHROME)
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "PASS", report["verdict"]
    assert code == 0, code
    worst = report["pages"][0]["worstGapPct"]
    assert worst["FULL FRAME"] == 0.0, worst


def test_floor_excuses_only_what_retail_cannot_reproduce(root: Path) -> None:
    """THE FLOOR TEST, and it is a PAIR because one run alone proves nothing.

    Both halves present the identical candidate difference on the animated
    patch. The only thing that changes is whether retail's second run also
    differs there. If the verdict flips, the floor is what flipped it."""
    # Half 1: run2 disagrees with run1 on the patch -> excused.
    plan, capture = build_fixture(root, chrome=CHROME)
    _, excused = run(plan, capture, root / "excused.json")
    assert excused["verdict"] == "PASS", excused["verdict"]
    assert excused["pages"][0]["worstGapPct"]["FULL FRAME"] == 0.0

    # Half 2: same candidate, but retail's two runs now AGREE on the patch, so
    # the difference is ours and must be charged.
    root2 = root / "no-floor"
    root2.mkdir()
    plan2, capture2 = build_fixture(
        root2, chrome=CHROME, floor_animated=REF_ANIMATED)
    code, charged = run(plan2, capture2, root / "charged.json")
    assert charged["verdict"] == "FAIL", charged["verdict"]
    assert code == 1, code
    assert charged["pages"][0]["worstGapPct"]["FULL FRAME"] > 0.0, charged


def test_regression_is_caught(root: Path) -> None:
    """THE test. A deliberate one-element regression must FAIL the gate."""
    plan, capture = build_fixture(root, chrome=(90, 200, 180))
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "FAIL", report["verdict"]
    assert code == 1, code
    breaches = {b["region"] for b in report["pages"][0]["breaches"]}
    assert breaches == {"FULL FRAME", "chrome"}, breaches
    assert report["pages"][0]["worstGapPct"]["chrome"] == 100.0


def test_missing_reference_set_is_unscored_not_pass(root: Path) -> None:
    """A fresh clone has no retail material. That must read as UNSCORED - the
    whole defect being fixed here is 'no evidence' rendering as 'no problem'."""
    plan, capture = build_fixture(root, chrome=CHROME)
    body = json.loads(plan.read_text(encoding="utf-8"))
    body["referenceRoot"] = str(root / "does-not-exist")
    plan.write_text(json.dumps(body), encoding="utf-8")
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "UNSCORED", report["verdict"]
    assert code == 0, code
    assert report["pages"] == []


def test_stale_measurements_are_unscored(root: Path) -> None:
    """A known-inapplicable measurement set must abstain rather than PASS/FAIL."""
    plan, capture = build_fixture(root, chrome=CHROME)
    body = json.loads(plan.read_text(encoding="utf-8"))
    body["_measurementProvenance"]["status"] = "STALE"
    body["_measurementProvenance"]["staleReason"] = "fixture changed"
    plan.write_text(json.dumps(body), encoding="utf-8")
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "UNSCORED", report["verdict"]
    assert report["measurementStatus"] == "STALE", report
    assert report["reason"] == "fixture changed", report
    assert report["pages"] == [], report
    assert code == 0, code


def test_capture_plan_with_no_page_is_unscored(root: Path) -> None:
    plan, capture = build_fixture(root, chrome=CHROME)
    manifest = capture / "capture-manifest.json"
    body = json.loads(manifest.read_text(encoding="utf-8"))
    body["plan"] = "startup"
    manifest.write_text(json.dumps(body), encoding="utf-8")
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "UNSCORED", report["verdict"]
    assert code == 0, code


def test_pairing_outside_tolerance_is_an_error(root: Path) -> None:
    """A frame far from its retail counterpart must be an ERROR, never a
    quietly-used approximation."""
    plan, capture = build_fixture(root, chrome=CHROME)
    for offset in OFFSETS:
        (capture / f"mainmenu-t{offset:06d}ms.png").rename(
            capture / f"mainmenu-t{offset + 900:06d}ms.png")
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "ERROR", report["verdict"]
    assert code == 1, code
    assert any("tolerance" in e for e in report["pages"][0]["errors"]), report


def test_size_mismatch_is_refused(root: Path) -> None:
    plan, capture = build_fixture(root, chrome=CHROME)
    for offset in OFFSETS:
        path = capture / f"mainmenu-t{offset:06d}ms.png"
        Image.open(path).resize((W * 2, H * 2)).save(path)
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "ERROR", report["verdict"]
    assert code == 1, code
    assert any("REFUSED" in e for e in report["pages"][0]["errors"]), report


def test_ceiling_naming_an_unknown_region_is_an_error(root: Path) -> None:
    """A ceiling on a region that does not exist would silently gate nothing."""
    plan, capture = build_fixture(root, chrome=CHROME)
    body = json.loads(plan.read_text(encoding="utf-8"))
    body["pages"][0]["measured"]["chrom"] = 0.0
    plan.write_text(json.dumps(body), encoding="utf-8")
    code, report = run(plan, capture, root / "out.json")
    assert report["verdict"] == "ERROR", report["verdict"]
    assert code == 1, code


def main() -> int:
    tests = [v for k, v in sorted(globals().items()) if k.startswith("test_")]
    failed = 0
    for test in tests:
        root = Path(tempfile.mkdtemp(prefix="score-frontend-"))
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
