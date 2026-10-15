#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Build and launch First Flight with the installed, pinned Linux Godot Mono."""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

import materialize_retail_assets as materializer


sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "tools"))
from godot_host import DEFAULT_ENGINE, ENGINE_VERSION, build_project, engine_path as _engine_path, output_directory, print_process_output, run_process
PROJECT = Path("rebuild/OnslaughtRebuild.Godot")
PREPARATION_TIMEOUT = 1200


def _prepare(
    game_root: Path,
    media_root: Path | None,
    *,
    cwd: Path,
    env: dict[str, str],
) -> None:
    script = str(materializer.ROOT / "rebuild/tools/materialize_retail_assets.py")
    canonical = materializer._canonical_repository_root()
    if materializer.ROOT.resolve() != canonical.resolve():
        # A child checkout reads the current verified payloads. It does not
        # become a second staging owner or regenerate another lane's media.
        if not materializer._outputs_ready():
            run_process([sys.executable, script, "--reuse-canonical-assets"],
                        cwd=cwd, env=env, timeout=PREPARATION_TIMEOUT)
        if media_root is not None and not materializer._startup_media_ready(game_root, media_root):
            raise RuntimeError(
                "canonical startup media is absent or out of date; prepare it in an authorized "
                "canonical-checkout task before running from this worktree"
            )
        return
    if not materializer._outputs_ready():
        run_process(
            [sys.executable, script, "--host-default-work-root", "--game-root", str(game_root)],
            cwd=cwd, env=env, timeout=PREPARATION_TIMEOUT,
        )
    if media_root is not None:
        # Startup media has its own cache check and cannot take a work-root flag.
        run_process(
            [sys.executable, script, "--startup-media", "--game-root", str(game_root),
             "--startup-media-root", str(media_root)],
            cwd=cwd, env=env, timeout=PREPARATION_TIMEOUT,
        )


CAPTURE_PLANS = ("startup", "gameplay", "mainmenu", "options")
DEFAULT_OFFSCREEN = "~/.local/bin/godot-offscreen"
# The two retail runs whose realised level offsets the gameplay plan samples.
RETAIL_OFFSET_RUNS = "opening-pan-run1,hud-timeline-run1"


@dataclass(frozen=True)
class CaptureRequest:
    """What a capture run is for: the rig's plan and size, the operator's
    declared purpose, retail's offsets for the gameplay plan, and the hidden
    GPU runner that draws it."""

    plan: str
    size: str
    purpose: str
    offsets_ms: tuple[int, ...]
    offscreen: Path
    timeout: float


def _runtime_command(
    engine: Path,
    project: Path,
    media_root: Path,
    output: Path,
    mode: str,
    engine_args: list[str],
    user_args: list[str],
    capture: CaptureRequest | None = None,
) -> list[str]:
    reserved = ["--startup-media", "--report", "--capture-dir"]
    if mode == "capture":
        reserved += ["--capture-plan", "--capture-size", "--capture-offsets-ms"]
    if any(arg == flag or arg.startswith(flag + "=") for arg in user_args for flag in reserved):
        raise RuntimeError(", ".join(reserved) + " are owned by the launcher")
    godot = ["--windowed", "--log-file", str(output / "first-flight.log")]
    if mode in ("smoke", "capture"):
        godot.extend(("--fixed-fps", "60"))
    if capture is not None:
        # The rig sets the viewport to the capture size itself and disables
        # content scaling, so frames compose 1:1; project.godot's window
        # overrides would win over --resolution alone.
        godot.extend(("--resolution", capture.size))
        if capture.plan == "gameplay":
            # Audio is not in the viewport texture, so silencing the mixer
            # cannot change a captured pixel.
            godot.extend(("--audio-driver", "Dummy"))
    godot.extend(engine_args)
    godot.extend(("--", f"--startup-media={media_root}"))
    if mode == "smoke":
        godot.extend(("--smoke", f"--report={output / 'first-flight-smoke.json'}"))
    elif capture is not None:
        godot.extend((f"--capture-dir={output}", f"--capture-plan={capture.plan}",
                      f"--capture-size={capture.size}"))
        if capture.offsets_ms:
            godot.append("--capture-offsets-ms=" + ",".join(map(str, capture.offsets_ms)))
    godot.extend(user_args)
    if capture is not None:
        # A capture draws on godot-offscreen's hidden output behind the
        # machine-wide GPU lock, never on the desktop. The wrapper supplies
        # --path and the display driver, and bounds the run itself.
        return [str(capture.offscreen), "--path", str(project), "--qa", str(output / "offscreen"),
                "--timeout", str(int(capture.timeout)), "--", *godot]
    return [str(engine), "--path", str(project), *godot]


def _retail_offsets(manifest: Path, runs: str) -> tuple[int, ...]:
    """Retail's realised level offsets for the named runs, so gameplay shots
    pair with retail frames exactly instead of on the nominal grid (retail's
    burst scheduler drifted up to ~80 ms over its 1 Hz window)."""
    wanted = {run.strip() for run in runs.split(",") if run.strip()}
    frames = json.loads(manifest.read_text(encoding="utf-8")).get("frames", [])
    offsets = sorted({int(frame["levelOffsetMs"]) for frame in frames
                      if str(frame.get("path", "")).split("/")[0] in wanted})
    if not offsets:
        raise RuntimeError(f"no frames from runs {runs!r} in {manifest}")
    return tuple(offsets)


def _godot_source_state(root: Path) -> tuple[str | None, str, list[str]]:
    """HEAD, and whether the Godot project the frames came from is committed
    source: clean, dirty or unknown. Unknown (no work tree, no git) is never
    clean; a capture from an unidentifiable tree once stamped itself clean."""
    def git(*arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(["git", "-C", str(root), *arguments],
                              capture_output=True, text=True, timeout=60)
    try:
        inside = git("rev-parse", "--is-inside-work-tree")
        if inside.returncode != 0 or inside.stdout.strip() != "true":
            return None, "unknown", []
        head = git("rev-parse", "HEAD")
        commit = head.stdout.strip() or None if head.returncode == 0 else None
        status = git("status", "--porcelain", "--", PROJECT.as_posix())
        if status.returncode != 0:
            return commit, "unknown", []
        dirty = [line for line in status.stdout.splitlines() if line]
        return commit, "dirty" if dirty else "clean", dirty
    except (OSError, subprocess.SubprocessError):
        return None, "unknown", []


CAPTURE_VERDICTS = ("PASS", "FAIL", "ERROR", "UNSCORED")


def _scorer_verdict(command: list[str], report: Path) -> str:
    report.unlink(missing_ok=True)
    subprocess.run(command, cwd=materializer.ROOT, timeout=1200, check=False)
    try:
        verdict = json.loads(report.read_text(encoding="utf-8")).get("verdict")
    except (OSError, ValueError, AttributeError):
        return "ERROR"
    return verdict if verdict in CAPTURE_VERDICTS else "ERROR"


def _finish_capture(output: Path, capture: CaptureRequest) -> dict[str, object]:
    """Stamp the capture's provenance, check the run, and score it against
    retail. Writes capture-status.json and returns the same summary."""
    manifest_path = output / "capture-manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError("the capture produced no manifest; the rig did not reach its final shot")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not isinstance(manifest, dict) or not isinstance(manifest.get("shots"), list):
        raise RuntimeError("the capture manifest is malformed")
    # The purpose is the operator's declaration. Gates that score "the newest
    # production capture" must never pick up an experiment, so production is
    # kept only when the Godot source is provably the committed build.
    commit, cleanliness, dirty = _godot_source_state(materializer.ROOT)
    purpose, reason = capture.purpose, None
    if capture.purpose == "production" and cleanliness != "clean":
        purpose = "probe"
        reason = (f"{PROJECT.as_posix()} has {len(dirty)} uncommitted change(s); a capture of "
                  "modified source is not the shipping build" if cleanliness == "dirty" else
                  f"git could not tell whether {PROJECT.as_posix()} is clean, so the source "
                  "of these frames is unknown")
        print(f"Purpose downgraded to probe: {reason}", file=sys.stderr, flush=True)
    manifest.update(capturePurpose=purpose, requestedPurpose=capture.purpose,
                    purposeDowngradeReason=reason, sourceCommit=commit,
                    godotSourceCleanliness=cleanliness,
                    # "Not provably clean", for readers of the older field.
                    godotSourceDirty=cleanliness != "clean")
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    # A shot on the wrong screen, unsaved, resampled or missing is not
    # evidence; a short manifest means the rig could not reach the end of its
    # plan, which is reportable but never a pass.
    shots = [shot for shot in manifest["shots"] if isinstance(shot, dict)]
    width, height = (int(part) for part in capture.size.split("x"))
    mismatched = sum(1 for shot in shots if shot.get("screenMatched") is not True)
    failed_saves = sum(1 for shot in shots if shot.get("saveError") is not None)
    wrong_size = sum(1 for shot in shots if (shot.get("width"), shot.get("height")) != (width, height))
    planned = manifest.get("plannedShots", len(shots))
    missing = max(0, planned - len(shots)) if type(planned) is int else len(shots) + 1

    # The retail comparison. UNSCORED (no retail reference present, or no plan
    # page for this capture) is its own verdict and never counts as a pass.
    parity = _scorer_verdict(
        [sys.executable, str(materializer.ROOT / "tools/score_frontend_capture.py"),
         "--capture-dir", str(output), "--json-out", str(output / "frontend-parity.json")],
        output / "frontend-parity.json")
    options_ink = None
    if capture.plan == "options":
        # A single-run diagnostic of the Options rows, never a parity claim.
        options_ink = _scorer_verdict(
            [sys.executable, str(materializer.ROOT / "rebuild/tools/compare_options_capture.py"),
             str(output), "--json-out", str(output / "options-ink-regression.json")],
            output / "options-ink-regression.json")
    healthy = mismatched == failed_saves == wrong_size == missing == 0
    status = ("SUSPECT" if not healthy or options_ink == "ERROR" else
              parity if parity in ("PASS", "FAIL", "UNSCORED") else "SUSPECT")
    summary: dict[str, object] = {
        "status": status, "parityVerdict": parity, "optionsInkVerdict": options_ink,
        "purpose": purpose, "godotSourceCleanliness": cleanliness, "sourceCommit": commit,
        "plan": manifest.get("plan"), "size": capture.size, "shots": len(shots),
        "screenMismatches": mismatched, "failedSaves": failed_saves, "wrongSizeShots": wrong_size,
        "missingShots": missing, "boundary": manifest.get("boundary"),
        "engineVersion": manifest.get("engineVersion"), "outputDirectory": str(output),
    }
    (output / "capture-status.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2), flush=True)
    return summary


# The completed smoke's report contract. Window closure can exit zero before
# CompleteSmoke writes its report, so the lifecycle is required, and every
# other field the scenario determines is pinned. This is not a pixel or audio
# parity gate. Values are compared with their JSON type, so 1 is not true.
SMOKE_DELIVERED_MESSAGE_IDS = (
    292562, 293386, 296682, -1575499396, -257967449, 82987417, 4422830,
    175347826, 4458134, 4493438, 295858, 1339691000, 669198996)
SMOKE_VALUES: dict[str, object] = {
    "schemaVersion": "onslaught-first-flight-smoke.v17",
    "engineVersion": "4.8-dev6 (official)",
    "exitReason": "smoke-complete",
    "tick": 2148,
    # The scenario's final state, shared with InteractiveSessionTests and the
    # C# headless replay of the recorded tape.
    "stateHash": "79fc338c15183a50455f1e2ffdce549183e74cdf53b8a4a5f8f6bc7edc3e4503",
    "targetsDestroyed": 0,
    "mode": "Walker",
    "level100OpeningTicksRemaining": 0,
    # The mission clock also counts the level's 60 pre-run frames.
    "level100MissionTick": 2208,
    "level100MissionOutcome": "Running",
    "level100TerminalState": "None",
    # The released message-box gate holds the fourteenth message
    # (TUTORIAL_PULSE_CANNON_2) past this scenario's 2,148 ticks;
    # FirstFlightSmokeScenario.DurationTicks samples where the schedule is quiet.
    "level100DeliveredMessageIds": list(SMOKE_DELIVERED_MESSAGE_IDS),
    "level100DeliveredMessageCount": 13,
    # Where the host forwards Core's message events to audio: the same
    # sequence, and Core's speaker order.
    "level100AudioQueuedMessageIds": list(SMOKE_DELIVERED_MESSAGE_IDS),
    "level100AudioQueuedSpeakerIds": [1508464] * 4 + [10565784] + [1508464] * 8,
    "level100VoicePlaybackConsistent": True,
    "level100DeliveredHelpCount": 1,
    "level100PlayerControlEnabled": True,
    "level100FlightEnabled": False,
    "level100PulseCannonEnabled": True,
    "level100VulcanCannonEnabled": False,
    "level100FiringRangeTargetsActive": True,
    "level100CurrentWeaponHighlighted": False,
    "totalSteps": 2148,
    "toggleEdgesConsumed": 0,
    "resetEdgesConsumed": 0,
    "resetGeneration": 0,
    "fireHeldTicksSampled": 4,
    "firePulseEdgesConsumed": 4,
    "movementPulseEdgesConsumed": 0,
    "cappedFrameCount": 0,
    "droppedElapsedTicks": 0,
    "playerVisualPresent": True,
    "retailAquilaMeshesPresent": True,
    "retailAquilaSurfaceCount": 112,
    "retailAquilaPartCount": 63,
    "retailAquilaAnimatedPartCount": 20,
    "retailCockpitSurfaceCount": 10,
    "retailLevel100StaticObjectCount": 33,
    # 111 object surfaces and the 13 separately animated subparts.
    "retailLevel100StaticObjectSurfaceCount": 124,
    "retailLevel100PineCount": 1481,
    "retailLevel100WaterPresent": True,
    "retailLevel100WaterGridVertexCount": 625,
    "retailLevel100WaterGridTriangleCount": 1152,
    "retailLevel100ShorelineTriangleCount": 2056,
    # Three Target Tanks (one group each), the Warehouse (three), three Target
    # Trucks (one each), the U-17 (two) and the ambient Air Trainer (one).
    "retailLevel100TargetSurfaceCount": 12,
    "level100ObjectiveMarkerCount": 4,
    # Not asset facts: the height field picks each tile's LOD from the smoothed
    # camera, so these follow the player's pose on the reported frame.
    "retailLevel100TerrainVertexCount": 34499,
    "retailLevel100TerrainTriangleCount": 33476,
    "retailLevel100SkySurfaceCount": 5,
    # Nine target visuals less the ambient Air Trainer, which retreats and
    # leaves (tick 867 with idle input, PlaneRetreatTests); the U-17 leaves
    # only after this run (tick 2,979, DropshipFlightTests).
    "targetVisualCount": 8,
    "openingPanActive": False,
    "hudVisible": True,
    "hudReady": True,
    "focusLossHandlerInputCleared": True,
    "focusLossHandlerNeutralRearmed": True,
    "coldClickToStart": True,
    "coldMainMenu": True,
    "coldDevSelect": True,
    "coldLevelSelect": True,
    # Traversal only; their drawing is measured by pixel capture.
    "coldMissionBriefing": True,
    "coldSelectConfiguration": True,
    "coldLoading": True,
    "coldGameplay": True,
    "cursorPolicyCustomAtFrontend": True,
    "cursorPolicyHiddenAtLoading": True,
    # The product's gameplay cursor policy over the four (focused, paused)
    # inputs. windowFocusedAtGameplay is desktop state and is not pinned.
    "gameplayCursorPolicy": ["Captured", "Visible", "Visible", "Visible"],
    "cursorPolicyAppliedAtGameplay": True,
    "focusLossCursorPolicyVisible": True,
    "focusGainCursorPolicyCaptured": True,
    "retryRequested": True,
    "retryGameplayActivated": True,
    "retrySessionFresh": True,
    "returnToMainMenuRequested": True,
    "returnedToMainMenu": True,
    "worldReleasedAtMainMenu": True,
    "mainMenuCursorPolicyCustom": True,
    "finalFrontendScreen": "MainMenu",
}
SMOKE_NEAR = {
    "retailAquilaStandingClearance": 0.059322417,
    "level100PlayerStartRelativeHeight": 0.21149921,
}
SMOKE_NEAR_TOLERANCE = 0.00001
SMOKE_LOG_ERROR = re.compile(
    r"(?im)(^|\s)(SCRIPT ERROR|ERROR:|FATAL|CRASH|Unhandled exception|System\.[A-Za-z]+Exception)")
SMOKE_PRIVATE_PATH = re.compile(r"(?i)[a-z]:[\\/]|users[\\/]|/home/")


def _same(expected: object, actual: object) -> bool:
    if isinstance(expected, list):
        return (isinstance(actual, list) and len(actual) == len(expected)
                and all(_same(e, a) for e, a in zip(expected, actual)))
    return type(actual) is type(expected) and actual == expected


def _validate_smoke_completion(output: Path) -> None:
    report_path, log_path = output / "first-flight-smoke.json", output / "first-flight.log"
    for path in (report_path, log_path):
        if not path.is_file():
            raise RuntimeError(f"smoke artifact is missing: {path.name}")
    raw = report_path.read_text(encoding="utf-8")
    if SMOKE_PRIVATE_PATH.search(raw) or str(Path.home()) in raw:
        raise RuntimeError("smoke report contains an absolute or user-specific path")
    report = json.loads(raw)
    if not isinstance(report, dict):
        raise RuntimeError("smoke report must be an object")

    def fail(name: str, detail: str) -> None:
        raise RuntimeError(f"smoke completion failed: {name}: {detail}")

    for name, expected in SMOKE_VALUES.items():
        if not _same(expected, report.get(name)):
            fail(name, f"expected {expected!r}, observed {report.get(name)!r}")
    for name, expected in SMOKE_NEAR.items():
        actual = report.get(name)
        if type(actual) not in (int, float) or abs(actual - expected) > SMOKE_NEAR_TOLERANCE:
            fail(name, f"expected {expected} ± {SMOKE_NEAR_TOLERANCE}, observed {actual!r}")
    delivered = list(SMOKE_DELIVERED_MESSAGE_IDS)
    # Which message the mixer is playing is host timing, not a Core fact:
    # a delivered message, or none in the handoff gap between two.
    playing = report.get("level100PlayingMessageId")
    if playing is not None and not (type(playing) is int and playing in delivered):
        fail("level100PlayingMessageId", f"{playing!r} is not a delivered message")
    # How far the mixer got is host timing; what it started is an exact
    # ordered prefix of the delivered sequence, at least one message long.
    started = report.get("level100VoiceStartedMessageIds")
    if (not isinstance(started, list) or not 1 <= len(started) <= len(delivered)
            or not _same(delivered[:len(started)], started)):
        fail("level100VoiceStartedMessageIds", f"{started!r} is not a prefix of the delivered messages")
    # Mixer-derived flags may all be false at the sampled tick; these hold at
    # every instant.
    for antecedent, consequent, holds in (
        ("level100MessagePlaying", "level100MessagePlaybackAvailable",
         report.get("level100MessagePlaybackAvailable") is True),
        ("tutorialVoicePlaying", "level100PlayingMessageId", playing is not None),
        ("level100MessagePlaybackAvailable", "level100PlayingMessageId", playing is not None),
    ):
        if report.get(antecedent) is True and not holds:
            fail(antecedent, f"true without {consequent}")
    if SMOKE_LOG_ERROR.search(log_path.read_text(encoding="utf-8", errors="replace")):
        raise RuntimeError("smoke completion failed: the Godot log contains an error")


def main(argv: list[str] | None = None) -> int:
    arguments = list(sys.argv[1:] if argv is None else argv)
    split = arguments.index("--") if "--" in arguments else len(arguments)
    user_args = arguments[split + 1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "run", "smoke", "capture"), nargs="?", default="run")
    parser.add_argument("--engine", default=DEFAULT_ENGINE, help="installed pinned Godot .NET executable")
    parser.add_argument("--game-root", type=Path, help="retail installation; otherwise discover Linux Steam")
    parser.add_argument("--no-build", action="store_true", help="run the existing managed build")
    parser.add_argument("--no-prepare", action="store_true",
                        help="reuse prepared assets and startup media without validation or materialization")
    parser.add_argument("--output-root", type=Path, help="fresh directory below this checkout's local-data")
    parser.add_argument("--timeout", type=float, help="runtime limit in seconds (smoke: 75; capture: 120, gameplay 600)")
    parser.add_argument("--engine-arg", action="append", default=[], help="Godot option; use --engine-arg=VALUE")
    parser.add_argument("--plan", choices=CAPTURE_PLANS, help="capture: the rig's plan (default startup)")
    parser.add_argument("--size", help="capture: frame size WIDTHxHEIGHT (default 640x480, retail's frontend)")
    parser.add_argument("--purpose", choices=("probe", "production"),
                        help="capture: production only when the Godot source is committed (default probe)")
    parser.add_argument("--retail-offset-manifest", type=Path,
                        help="capture, gameplay plan: sample at retail's realised offsets from this manifest")
    parser.add_argument("--retail-offset-runs", help=f"capture: runs to take offsets from ({RETAIL_OFFSET_RUNS})")
    parser.add_argument("--offscreen", help=f"capture: the hidden-output GPU runner ({DEFAULT_OFFSCREEN})")
    args = parser.parse_args(arguments[:split])
    if not sys.platform.startswith("linux"):
        parser.error("this launcher requires Linux")
    if args.timeout is not None and (args.timeout <= 0 or not args.timeout < float("inf")):
        parser.error("--timeout must be a finite positive number")
    if args.mode == "build" and (args.no_build or user_args or args.engine_arg):
        parser.error("build does not accept --no-build or runtime arguments")
    capture_options = (args.plan, args.size, args.purpose, args.retail_offset_manifest,
                       args.retail_offset_runs, args.offscreen)
    if args.mode != "capture" and any(option is not None for option in capture_options):
        parser.error("--plan, --size, --purpose, --retail-offset-* and --offscreen are capture options")
    if args.size is not None and not re.fullmatch(r"[1-9][0-9]{1,4}x[1-9][0-9]{1,4}", args.size):
        parser.error("--size must be WIDTHxHEIGHT, for example 640x480")
    if args.retail_offset_manifest is not None and args.plan != "gameplay":
        parser.error("--retail-offset-manifest applies only to --plan gameplay")

    interrupted = signal.SIGINT

    def interrupt(signum: int, _frame: object) -> None:
        nonlocal interrupted
        interrupted = signum
        raise KeyboardInterrupt

    prior_term = signal.signal(signal.SIGTERM, interrupt)
    try:
        project = materializer.ROOT / PROJECT
        engine = _engine_path(args.engine)
        game_root = None
        if not args.no_prepare:
            game_root = materializer._resolve_game_root(
                args.game_root.expanduser().absolute() if args.game_root else None
            )
        media_root = None
        if args.mode != "build":
            media_root = materializer._resolve_work_root(
                materializer._default_startup_media_root(), game_root=game_root
            )
        scratch_owner = output_directory(materializer.ROOT, args.output_root, "first-flight", args.mode)
        for child in ("user-data", "cache", "config"):
            (scratch_owner / child).mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="scratch-", dir=scratch_owner) as scratch:
            env = dict(os.environ, TMPDIR=scratch, TMP=scratch, TEMP=scratch,
                       XDG_DATA_HOME=str(scratch_owner / "user-data"),
                       XDG_CACHE_HOME=str(scratch_owner / "cache"),
                       XDG_CONFIG_HOME=str(scratch_owner / "config"),
                       DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
            version = run_process([str(engine), "--version"], cwd=project, env=env,
                                  timeout=30, capture=True).stdout.strip()
            if version != ENGINE_VERSION:
                raise RuntimeError(f"expected Godot {ENGINE_VERSION}; found {version!r}")
            if not args.no_prepare:
                assert game_root is not None
                _prepare(game_root, media_root, cwd=materializer.ROOT, env=env)
            if not args.no_build:
                build_project(project, engine, env)
            if args.mode == "build":
                return 0
            assert media_root is not None
            output = scratch_owner
            capture = None
            if args.mode == "capture":
                plan = args.plan or "startup"
                offscreen = shutil.which(os.path.expanduser(args.offscreen or DEFAULT_OFFSCREEN))
                if offscreen is None:
                    raise RuntimeError("godot-offscreen was not found; captures never open a desktop window")
                offsets = () if args.retail_offset_manifest is None else _retail_offsets(
                    args.retail_offset_manifest, args.retail_offset_runs or RETAIL_OFFSET_RUNS)
                # The gameplay plan holds Level 100 for 42 s of engine time
                # after the frontend traversal.
                capture = CaptureRequest(plan, args.size or "640x480", args.purpose or "probe", offsets,
                                         Path(offscreen),
                                         args.timeout or (600 if plan == "gameplay" else 120))
            command = _runtime_command(engine, project, media_root, output, args.mode,
                                       args.engine_arg, user_args, capture)
            print(f"First Flight {args.mode} output: {output}", flush=True)
            timeout = args.timeout
            if timeout is None:
                timeout = {"smoke": 75}.get(args.mode)
            # godot-offscreen bounds a capture itself, after waiting for the GPU lock.
            run_process(command, cwd=project, env=env, timeout=None if capture else timeout)
            if args.mode == "smoke":
                _validate_smoke_completion(output)
            if capture is not None:
                status = _finish_capture(output, capture)["status"]
                # UNSCORED is reported as such; only the capture's own health
                # and a retail regression fail the command.
                return {"PASS": 0, "UNSCORED": 0, "FAIL": 1}.get(str(status), 2)
            return 0
    except subprocess.TimeoutExpired as error:
        print_process_output(error)
        print(f"First Flight timed out after {error.timeout}s: {error.cmd[0]}", file=sys.stderr)
        return 124
    except subprocess.CalledProcessError as error:
        print_process_output(error)
        print(f"First Flight process exited {error.returncode}: {error.cmd[0]}", file=sys.stderr)
        return error.returncode if error.returncode > 0 else 128 - error.returncode
    except KeyboardInterrupt:
        print("First Flight interrupted; owned processes stopped.", file=sys.stderr)
        return 128 + interrupted
    except (OSError, RuntimeError, ValueError) as error:
        print(f"First Flight failed: {error}", file=sys.stderr)
        return 2
    finally:
        signal.signal(signal.SIGTERM, prior_term)


if __name__ == "__main__":
    raise SystemExit(main())
