#!/usr/bin/env python3
"""Run every host-supported tools/ test suite and report one summary.

Every supported suite runs, even after a failure. Windows-dependent suites
are explicitly reported as skipped on other hosts, never counted as passes.
Output is streamed as it happens - a summary
is a navigation aid, not a replacement for the failing suite's own report - and
the exit code is non-zero when any suite failed, exactly as the chain was.

Order is the chain's original order and is not a dependency: the suites are
independent, and running them in one process would be wrong, because several
of them compile executables or spawn engines.
"""

from __future__ import annotations

import argparse
import contextlib
import io
import os
import pathlib
import subprocess
import sys
import time
import unittest
from unittest import mock


ROOT = pathlib.Path(__file__).resolve().parents[1]

# One entry per command the gate used to chain.  Paths are repository-relative
# and are resolved against ROOT so the gate behaves the same from any cwd.
SUITES: tuple[tuple[str, ...], ...] = (
    ("tools/check_installed_game_claims.py", "--self-test"),
    ("tools/check_installed_game_claims.py", "--check"),
    ("tools/check_registered_screenshots.py", "--self-test"),
    ("tools/check_registered_screenshots.py", "--check"),
    ("tools/aya_archive_inventory_tests.py",),
    ("tools/cmsh_animation_usage_census_tests.py",),
    ("tools/cmsh_grammar_walk_tests.py",),
    ("tools/aya_texture_fidelity_census_tests.py",),
    ("tools/export_game_assets_tests.py",),
    ("rebuild/tools/materialize_retail_assets_tests.py",),
    ("rebuild/tools/cmsh_static_preview_tests.py",),
    ("tools/score_frontend_capture_tests.py",),
    ("tools/check_region_overlap_tests.py",),
    ("tools/check_region_overlap.py",),
    # Name-align plates (Gen34–38): keep regression guards in the sweep (Opus).
    ("tools/worldheaders_decode.py", "--self-test"),
)

# Suites that need a Windows host, by path, with the reason. None remain since the
# Windows runtime-probe lab moved to the private RE archive; the mechanism stays so a
# future one is reported as skipped rather than counted as a pass.
WINDOWS_ONLY: dict[str, str] = {}


def format_summary(results: list[dict[str, object]]) -> str:
    """One line per suite, failures repeated at the end so none can be missed."""

    width = max((len(str(row["name"])) for row in results), default=0)
    lines = ["", "tools test summary", "=" * (width + 26)]
    for row in results:
        lines.append(
            "{status:4}  {name:<{width}}  exit={exit:<4} {seconds:>7.1f}s".format(
                status="SKIP" if row.get("skipReason") else "PASS" if row["exitCode"] == 0 else "FAIL",
                name=str(row["name"]),
                width=width,
                exit=str(row["exitCode"]),
                seconds=float(str(row["seconds"])),
            )
        )
        if row.get("skipReason"):
            lines.append(f"      {row['skipReason']}")
    failed = [row for row in results if row["exitCode"] not in (None, 0)]
    skipped = [row for row in results if row.get("skipReason")]
    passed = sum(row["exitCode"] == 0 for row in results)
    lines.append(
        f"{passed} passed, {len(failed)} failed, "
        f"{len(results) - len(skipped)} run"
        + (f", {len(skipped)} skipped (Windows required)" if skipped else "")
    )
    if failed:
        lines.append("failed suites:")
        lines.extend(f"  {row['name']} (exit {row['exitCode']})" for row in failed)
    return "\n".join(lines) + "\n"


def run_suite(command: tuple[str, ...]) -> dict[str, object]:
    """Run one suite to completion, streaming its own output as it goes."""

    arguments = [sys.executable, str(ROOT / command[0]), *command[1:]]
    started = time.monotonic()
    completed = subprocess.run(arguments, cwd=ROOT, check=False)
    return {
        "name": " ".join(command),
        "exitCode": completed.returncode,
        "seconds": time.monotonic() - started,
    }


def run_all(suites: tuple[tuple[str, ...], ...] = SUITES, *, host_os: str | None = None) -> int:
    host_os = os.name if host_os is None else host_os
    results: list[dict[str, object]] = []
    for index, command in enumerate(suites, start=1):
        print(
            f"\n[{index}/{len(suites)}] {' '.join(command)}",
            flush=True,
        )
        if host_os != "nt" and command[0] in WINDOWS_ONLY:
            reason = "Windows host required: " + WINDOWS_ONLY[command[0]]
            print("SKIP " + reason, flush=True)
            results.append({"name": " ".join(command), "exitCode": None,
                            "seconds": 0.0, "skipReason": reason})
        else:
            results.append(run_suite(command))
    print(format_summary(results), flush=True)
    return 1 if any(row["exitCode"] not in (None, 0) for row in results) else 0


class RunToolTestsSelfTest(unittest.TestCase):
    """The property that matters: a failure never stops a later suite running."""

    def test_every_suite_runs_even_when_an_early_one_fails(self) -> None:
        marker = "tools/run_tool_tests.py"
        suites = (
            (marker, "--emit-exit-code", "0"),
            (marker, "--emit-exit-code", "7"),
            (marker, "--emit-exit-code", "0"),
        )
        results = [run_suite(command) for command in suites]

        self.assertEqual([0, 7, 0], [row["exitCode"] for row in results])
        summary = format_summary(results)
        self.assertIn("2 passed, 1 failed, 3 run", summary)
        self.assertIn("failed suites:", summary)
        self.assertIn("(exit 7)", summary)

    def test_the_run_fails_when_any_suite_fails(self) -> None:
        marker = "tools/run_tool_tests.py"
        self.assertEqual(
            0,
            run_all(
                (
                    (marker, "--emit-exit-code", "0"),
                    (marker, "--emit-exit-code", "0"),
                )
            ),
        )
        self.assertEqual(
            1,
            run_all(
                (
                    (marker, "--emit-exit-code", "0"),
                    (marker, "--emit-exit-code", "3"),
                )
            ),
        )

    def test_the_suite_list_matches_the_files_on_disk(self) -> None:
        # A gate that silently skips a suite it cannot find is the failure this
        # runner exists to prevent.
        for command in SUITES:
            with self.subTest(suite=command[0]):
                self.assertTrue((ROOT / command[0]).is_file(), command[0])
        self.assertTrue(set(WINDOWS_ONLY) <= {command[0] for command in SUITES})

    @mock.patch.dict(WINDOWS_ONLY, {f"tools/windows_only_{n}.py": "synthetic" for n in range(4)})
    def test_windows_suites_are_reported_without_launching_on_linux(self) -> None:
        windows = tuple((path,) for path in WINDOWS_ONLY)
        portable = ("tools/run_tool_tests.py", "--emit-exit-code", "0")

        def result(command):
            return {"name": " ".join(command), "exitCode": 0, "seconds": 0.0}

        for host, expected in [("posix", [portable]), ("nt", [*windows, portable])]:
            captured = io.StringIO()
            with mock.patch(__name__ + ".run_suite", side_effect=result) as run, contextlib.redirect_stdout(captured):
                self.assertEqual(0, run_all((*windows, portable), host_os=host))
            self.assertEqual(expected, [call.args[0] for call in run.call_args_list])
            if host == "posix":
                self.assertIn("1 passed, 0 failed, 1 run, 4 skipped (Windows required)", captured.getvalue())
            else:
                self.assertIn("5 passed, 0 failed, 5 run", captured.getvalue())


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument(
        "--emit-exit-code",
        type=int,
        default=None,
        help=argparse.SUPPRESS,  # self-test fixture: exit with the given code
    )
    arguments = parser.parse_args(argv)
    if arguments.emit_exit_code is not None:
        return arguments.emit_exit_code
    if arguments.self_test:
        loader = unittest.TestLoader()
        suite = loader.loadTestsFromTestCase(RunToolTestsSelfTest)
        result = unittest.TextTestRunner(verbosity=2).run(suite)
        return 0 if result.wasSuccessful() else 1
    return run_all()


if __name__ == "__main__":
    raise SystemExit(main())
