#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""The rebuild lane's whole gate in one command.

Runs, in order: the Godot build; the Core suite (whose cold-start test also
writes its won tape); the Core ferry-landing sweep, the expensive oracle the
default Core filter leaves out; the Client suite; the pause and AYA checks in headless
Godot; the headless smoke, recording its tape; and the C# headless replayer
twice over the smoke tape and the won tape. Every log goes to a fresh
directory under this checkout's local-data/test-runs, and the summary prints
the suite counts and the tapes' hashes, so pins are read from one place.

Usage: python rebuild/tools/rebuild_gate.py [--only STEP[,STEP...]]
Steps: build, core, ferry, client, pause, aya, smoke, replay.
"""

from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import os
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STEPS = ("build", "core", "ferry", "client", "pause", "aya", "smoke", "replay")
GODOT_CHECK = ("python", "rebuild/tools/first_flight.py", "run", "--no-build", "--no-prepare",
               "--timeout", "600", "--engine-arg=--headless", "--engine-arg=--audio-driver",
               "--engine-arg=Dummy")
SUMMARY = re.compile(r"(Passed|Failed)!\s+-\s+Failed:\s+(\d+), Passed:\s+(\d+), Skipped:\s+(\d+), Total:\s+(\d+)")


def dotnet_counts(log: str) -> dict[str, int] | None:
    """The last `dotnet test` summary line's counts."""
    matches = SUMMARY.findall(log)
    if not matches:
        return None
    _, failed, passed, skipped, total = matches[-1]
    return {"failed": int(failed), "passed": int(passed), "skipped": int(skipped), "total": int(total)}


def replay_hashes(output: str) -> dict[str, object]:
    """The headless replayer's JSON report: its trace and final-state hashes."""
    start = output.find("{")
    report = json.loads(output[start:output.rindex("}") + 1])
    return {key: report.get(key) for key in (
        "ticks", "repeats", "traceHash", "finalStateHash", "traceHashVerified", "finalStateHashVerified", "hull")}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--only", help="comma-separated steps to run")
    args = parser.parse_args(argv)
    steps = STEPS if not args.only else tuple(step for step in STEPS if step in args.only.split(","))
    run = ROOT / "local-data/test-runs" / datetime.datetime.now().strftime("rebuild-gate-%Y%m%d-%H%M%S")
    run.mkdir(parents=True)
    env = dict(os.environ, BEA_LOCAL_LAB=os.environ.get(
        "BEA_LOCAL_LAB", str(ROOT.parents[1] / "local-lab") if ROOT.parent.name == ".worktrees" else str(ROOT / "local-lab")))
    won_tape, smoke_tape = run / "won-tape.json", run / "smoke-tape.json"
    env["ONSLAUGHT_WON_TAPE_PATH"] = str(won_tape)
    summary: dict[str, object] = {"runDirectory": str(run)}

    def step(name: str, command: list[str] | tuple[str, ...]) -> str:
        result = subprocess.run(command, cwd=ROOT, env=env, capture_output=True, text=True)
        log = result.stdout + result.stderr
        (run / f"{name}.log").write_text(log, encoding="utf-8")
        print(f"{name}: exit {result.returncode}", flush=True)
        if result.returncode != 0:
            summary[name] = {"exit": result.returncode, **(dotnet_counts(log) or {})}
            raise RuntimeError(f"{name} failed; see {run / (name + '.log')}")
        return log

    try:
        if "build" in steps:
            step("build", ["npm", "run", "build:rebuild-godot"])
        if "core" in steps:
            summary["core"] = dotnet_counts(step("core", ["npm", "run", "test:rebuild-core"]))
        if "ferry" in steps:
            summary["ferry"] = dotnet_counts(step("ferry", ["npm", "run", "test:rebuild-ferry-sweep"]))
        if "client" in steps:
            summary["client"] = dotnet_counts(step("client", ["npm", "run", "test:rebuild-client"]))
        if "pause" in steps:
            step("pause", [*GODOT_CHECK, "--engine-arg=res://Scenes/Pause/Tests/PauseSceneChecks.tscn"])
        if "aya" in steps:
            step("aya", [*GODOT_CHECK, "--engine-arg=res://Scenes/Shared/Tests/AyaTextureChecks.tscn"])
        if "smoke" in steps:
            step("smoke", ["python", "rebuild/tools/first_flight.py", "smoke", "--no-build", "--no-prepare",
                           "--timeout", "600", "--engine-arg=--headless", "--engine-arg=--audio-driver",
                           "--engine-arg=Dummy", "--", f"--record-tape={smoke_tape}"])
            summary["smokeTapeSha256"] = sha256(smoke_tape)
        if "replay" in steps:
            for name, tape in (("smoke-replay", smoke_tape), ("won-replay", won_tape)):
                if tape.exists():
                    summary[name] = replay_hashes(step(name, [
                        "npm", "run", "run:rebuild-headless", "--", "--tape", str(tape), "--repeat", "2"]))
        return_code = 0
    except RuntimeError as error:
        print(error, file=sys.stderr)
        return_code = 1
    (run / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return return_code


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
