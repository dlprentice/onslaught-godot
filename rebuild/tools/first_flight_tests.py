#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Linux launcher regressions using only short-lived fake tools."""

from __future__ import annotations

import json
import io
import os
import subprocess
import sys
import tempfile
import time
import unittest
from contextlib import redirect_stderr
from pathlib import Path
from unittest import mock

import first_flight as launcher

# A completed smoke that meets the whole report contract; the mixer-derived
# fields take one legal value each.
COMPLETED_SMOKE = dict(launcher.SMOKE_VALUES) | dict(launcher.SMOKE_NEAR) | {
    "level100PlayingMessageId": launcher.SMOKE_DELIVERED_MESSAGE_IDS[1],
    "level100VoiceStartedMessageIds": list(launcher.SMOKE_DELIVERED_MESSAGE_IDS[:2]),
    "level100MessagePlaying": False,
    "level100MessagePlaybackAvailable": False,
    "tutorialVoicePlaying": True,
    "windowFocusedAtGameplay": False,
}


@unittest.skipUnless(sys.platform.startswith("linux"), "Linux process-group launcher")
class LauncherTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="first-flight-tests-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.canonical = self.root / "canonical"
        self.checkout = self.root / "worktree"
        (self.canonical / "local-lab").mkdir(parents=True)
        (self.checkout / launcher.PROJECT).mkdir(parents=True)
        (self.checkout / launcher.PROJECT / "OnslaughtRebuild.Godot.csproj").touch()
        (self.checkout / "rebuild/tools").mkdir()
        self.game = self.root / "Retail Installation"
        for name in ("BEA.exe", launcher.materializer.LEVEL_ARCHIVE,
                     launcher.materializer.BASE_ARCHIVE, launcher.materializer.SOUND_BANK):
            path = self.game / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.touch()
        self.calls = self.root / "calls.jsonl"
        fake = (
            f"#!{sys.executable}\n"
            "import json, os, pathlib, sys\n"
            "if '--version' in sys.argv:\n"
            "    print(os.environ.get('FAKE_ENGINE_VERSION', " + repr(launcher.ENGINE_VERSION) + "))\n"
            "    raise SystemExit(0)\n"
            "with open(os.environ['FAKE_CALLS'], 'a') as output:\n"
            "    output.write(json.dumps({'tool': pathlib.Path(sys.argv[0]).name, "
            "'args': sys.argv[1:], 'tmp': os.environ['TMPDIR'], "
            "'data': os.environ['XDG_DATA_HOME'], 'cache': os.environ['XDG_CACHE_HOME'], "
            "'config': os.environ['XDG_CONFIG_HOME'], "
            "'unrelated': os.environ['FAKE_UNRELATED']}) + '\\n')\n"
            "if pathlib.Path(sys.argv[0]).name == 'godot48-mono':\n"
            "    if '--log-file' in sys.argv and 'FAKE_NO_LOG' not in os.environ:\n"
            "        pathlib.Path(sys.argv[sys.argv.index('--log-file') + 1]).write_text("
            "os.environ.get('FAKE_LOG', 'Godot Engine v4.8\\n'))\n"
            "    report = os.environ.get('FAKE_SMOKE_REPORT')\n"
            "    for arg in sys.argv:\n"
            "        if arg.startswith('--report=') and report is not None:\n"
            "            pathlib.Path(arg.split('=', 1)[1]).write_text(report)\n"
            "    raise SystemExit(int(os.environ.get('FAKE_ENGINE_EXIT', '0')))\n"
        )
        self.engine = self.root / "engine/godot48-mono"
        self.engine.parent.mkdir()
        self.engine.write_text(fake, encoding="utf-8")
        self.engine.chmod(0o700)
        packages = self.engine.parent / "GodotSharp/Tools/nupkgs"
        packages.mkdir(parents=True)
        (packages / "Godot.NET.Sdk.4.8.0-dev.6.nupkg").touch()
        dotnet = self.engine.parent / "dotnet"
        dotnet.write_text(fake, encoding="utf-8")
        dotnet.chmod(0o700)
        (self.checkout / "rebuild/tools/materialize_retail_assets.py").write_text(fake, encoding="utf-8")
        # godot-offscreen stands in for the hidden-output GPU runner: it records
        # its call and writes the rig's manifest into --capture-dir.
        offscreen = (
            f"#!{sys.executable}\n"
            "import json, os, pathlib, sys\n"
            "with open(os.environ['FAKE_CALLS'], 'a') as output:\n"
            "    output.write(json.dumps({'tool': 'godot-offscreen', 'args': sys.argv[1:]}) + '\\n')\n"
            "values = dict(arg[2:].split('=', 1) for arg in sys.argv if arg.startswith('--capture-'))\n"
            "width, height = (int(part) for part in values['capture-size'].split('x'))\n"
            "shot = {'width': width, 'height': height, 'screenMatched': True, 'saveError': None}\n"
            "manifest = {'plan': values['capture-plan'], 'plannedShots': 2, 'shots': [shot, dict(shot)],\n"
            "            'engineVersion': '4.8.dev6.mono.official'}\n"
            "manifest.update(json.loads(os.environ.get('FAKE_MANIFEST_CHANGES', '{}')))\n"
            "if 'FAKE_NO_MANIFEST' not in os.environ:\n"
            "    pathlib.Path(values['capture-dir'], 'capture-manifest.json').write_text(json.dumps(manifest))\n"
            "raise SystemExit(int(os.environ.get('FAKE_OFFSCREEN_EXIT', '0')))\n"
        )
        self.offscreen = self.engine.parent / "godot-offscreen"
        self.offscreen.write_text(offscreen, encoding="utf-8")
        self.offscreen.chmod(0o700)
        # The retail scorers report the verdict their environment names.
        for relative, variable in (("tools/score_frontend_capture.py", "FAKE_PARITY_VERDICT"),
                                   ("rebuild/tools/compare_options_capture.py", "FAKE_OPTIONS_VERDICT")):
            scorer = self.checkout / relative
            scorer.parent.mkdir(parents=True, exist_ok=True)
            scorer.write_text(
                "import json, os, pathlib, sys\n"
                f"verdict = os.environ.get({variable!r}, 'PASS')\n"
                "if verdict != 'NONE':\n"
                "    out = pathlib.Path(sys.argv[sys.argv.index('--json-out') + 1])\n"
                "    out.write_text(json.dumps({'verdict': verdict}))\n",
                encoding="utf-8")
        patches = [
            mock.patch.object(launcher.materializer, "ROOT", self.checkout),
            mock.patch.object(launcher.materializer, "_canonical_repository_root", return_value=self.canonical),
            mock.patch.object(launcher.materializer, "_outputs_ready", return_value=False),
            mock.patch.object(launcher.materializer, "_startup_media_ready", return_value=True),
            mock.patch.dict(os.environ, {"FAKE_CALLS": str(self.calls),
                                        "FAKE_UNRELATED": "preserved",
                                        "ONSLAUGHT_STARTUP_MEDIA": "",
                                        "PATH": str(self.engine.parent) + os.pathsep + os.environ["PATH"]}),
        ]
        for patch in patches:
            patch.start()
            self.addCleanup(patch.stop)

    def invoke(self, mode: str, *args: str) -> int:
        return launcher.main([mode, "--engine", str(self.engine), "--game-root", str(self.game), *args])

    def capture(self, *args: str) -> int:
        return self.invoke("capture", "--no-build", "--offscreen", str(self.offscreen), *args)

    def offscreen_calls(self) -> list[list[str]]:
        return [call["args"] for call in self.read_calls() if call["tool"] == "godot-offscreen"]

    def read_calls(self) -> list[dict]:
        return [json.loads(line) for line in self.calls.read_text(encoding="utf-8").splitlines()]

    def test_run_uses_selected_game_canonical_media_local_packages_and_user_arguments(self) -> None:
        self.assertEqual(0, self.invoke("run", "--", "--skipfmv"))
        calls = self.read_calls()
        self.assertEqual(["materialize_retail_assets.py",
                          "dotnet", "dotnet", "godot48-mono"], [call["tool"] for call in calls])
        assets, restore, build, engine = (call["args"] for call in calls)
        self.assertEqual(["--reuse-canonical-assets"], assets)
        canonical_media = self.canonical / "local-lab/startup-media"
        self.assertEqual(str(self.engine.parent / "GodotSharp/Tools/nupkgs"), restore[restore.index("--source") + 1])
        self.assertIn("--locked-mode", restore)
        self.assertIn("--no-restore", build)
        self.assertEqual([f"--startup-media={canonical_media}", "--skipfmv"], engine[engine.index("--") + 1:])
        for call in calls:
            self.assertTrue(Path(call["tmp"]).is_relative_to(self.checkout / "local-data"))
            self.assertFalse(Path(call["tmp"]).exists())
            self.assertTrue(Path(call["data"]).is_relative_to(self.checkout / "local-data"))
            self.assertTrue(Path(call["cache"]).is_relative_to(self.checkout / "local-data"))
            self.assertTrue(Path(call["config"]).is_relative_to(self.checkout / "local-data"))
            self.assertEqual("preserved", call["unrelated"])
        self.assertFalse((self.canonical / "local-data").exists())

    def test_verified_asset_cache_skips_regeneration_and_no_build_skips_dotnet(self) -> None:
        with mock.patch.object(launcher.materializer, "_outputs_ready", return_value=True):
            self.assertEqual(0, self.invoke("run", "--no-build"))
        calls = self.read_calls()
        self.assertEqual(["godot48-mono"], [call["tool"] for call in calls])

    def test_missing_worktree_media_fails_without_writing_shared_inputs(self) -> None:
        with mock.patch.object(launcher.materializer, "_outputs_ready", return_value=True), \
             mock.patch.object(launcher.materializer, "_startup_media_ready", return_value=False):
            self.assertEqual(2, self.invoke("run", "--no-build"))
        self.assertFalse(self.calls.exists())
        self.assertFalse((self.canonical / "local-data").exists())

    def test_canonical_preparation_keeps_the_explicit_staging_owner(self) -> None:
        with mock.patch.object(launcher.materializer, "_canonical_repository_root", return_value=self.checkout):
            (self.checkout / "local-lab").mkdir()
            self.assertEqual(0, self.invoke("run", "--no-build"))
        assets, media, _ = (call["args"] for call in self.read_calls())
        self.assertIn("--host-default-work-root", assets)
        self.assertEqual(str(self.game), assets[assets.index("--game-root") + 1])
        self.assertIn("--startup-media", media)
        self.assertEqual(str(self.checkout / "local-lab/startup-media"),
                         media[media.index("--startup-media-root") + 1])

    def test_output_owner_is_checkout_local_and_each_run_has_an_isolated_profile(self) -> None:
        output = self.checkout / "local-data/engine-check/run"
        self.assertEqual(0, self.invoke("run", "--no-build", "--no-prepare", "--output-root", str(output)))
        self.assertEqual(0, self.invoke("run", "--no-build", "--no-prepare"))
        calls = self.read_calls()
        for key in ("data", "cache", "config"):
            self.assertNotEqual(calls[0][key], calls[1][key])
        self.assertEqual(2, self.invoke("run", "--no-build", "--no-prepare", "--output-root", str(output)))
        shared = self.canonical / "local-data/first-flight/run"
        self.assertEqual(2, self.invoke("run", "--no-build", "--no-prepare", "--output-root", str(shared)))
        self.assertFalse(shared.exists())

    def test_default_engine_selects_the_pinned_48_alias(self) -> None:
        self.assertEqual(0, launcher.main(["run", "--no-build", "--no-prepare"]))
        self.assertEqual("godot48-mono", self.read_calls()[0]["tool"])

    def test_build_has_no_startup_media_or_runtime(self) -> None:
        self.assertEqual(0, self.invoke("build"))
        self.assertEqual(["materialize_retail_assets.py", "dotnet", "dotnet"],
                         [call["tool"] for call in self.read_calls()])

    def test_no_prepare_launch_skips_validation_and_materializer_processes(self) -> None:
        with mock.patch.object(launcher.materializer, "_outputs_ready", side_effect=AssertionError("must not validate")), \
             mock.patch.object(launcher.materializer, "_resolve_game_root", side_effect=AssertionError("must not discover retail inputs")):
            self.assertEqual(0, self.invoke("run", "--no-build", "--no-prepare"))
        calls = self.read_calls()
        self.assertEqual(["godot48-mono"], [call["tool"] for call in calls])
        self.assertIn(f"--startup-media={self.canonical / 'local-lab/startup-media'}", calls[0]["args"])

    def test_no_prepare_build_reuses_existing_assets(self) -> None:
        with mock.patch.object(launcher.materializer, "_outputs_ready", side_effect=AssertionError("must not validate")):
            self.assertEqual(0, self.invoke("build", "--no-prepare"))
        self.assertEqual(["dotnet", "dotnet"], [call["tool"] for call in self.read_calls()])

    def test_wrong_engine_version_stops_before_materialization(self) -> None:
        with mock.patch.dict(os.environ, {"FAKE_ENGINE_VERSION": "4.7.1.stable.mono.official.old"}):
            self.assertEqual(2, self.invoke("run"))
        self.assertFalse(self.calls.exists())

    def test_native_failure_exit_is_preserved(self) -> None:
        with mock.patch.dict(os.environ, {"FAKE_ENGINE_EXIT": "17"}):
            self.assertEqual(17, self.invoke("run", "--no-build"))

    def test_smoke_and_capture_receive_fresh_outputs_and_bounded_timeout(self) -> None:
        real_run = launcher.run_process
        with mock.patch.object(launcher, "run_process", wraps=real_run) as run, \
             mock.patch.dict(os.environ, {"FAKE_SMOKE_REPORT": json.dumps(COMPLETED_SMOKE)}):
            self.assertEqual(0, self.invoke("smoke", "--no-build"))
            self.assertEqual(75, run.call_args.kwargs["timeout"])
            self.assertEqual(0, self.capture("--plan", "mainmenu"))
            # godot-offscreen bounds the capture itself, after the GPU lock.
            self.assertIsNone(run.call_args.kwargs["timeout"])
        engine_calls = [call["args"] for call in self.read_calls() if call["tool"] == "godot48-mono"]
        report = next(arg.removeprefix("--report=") for arg in engine_calls[0] if arg.startswith("--report="))
        capture_call = self.offscreen_calls()[0]
        capture = next(arg.removeprefix("--capture-dir=") for arg in capture_call if arg.startswith("--capture-dir="))
        self.assertNotEqual(Path(report).parent, Path(capture))
        self.assertTrue(Path(capture).is_relative_to(self.checkout / "local-data"))
        self.assertIn("--capture-plan=mainmenu", capture_call)
        # No capture ever starts the engine on the desktop.
        self.assertEqual(1, len(engine_calls))

    def test_capture_draws_offscreen_with_the_launchers_plan_and_size(self) -> None:
        self.assertEqual(0, self.capture("--plan", "options", "--size", "1280x720",
                                         "--engine-arg=--verbose", "--", "--skipfmv"))
        call = self.offscreen_calls()[0]
        project = str(self.checkout / launcher.PROJECT)
        self.assertEqual(["--path", project], call[:2])
        self.assertEqual("120", call[call.index("--timeout") + 1])
        godot = call[call.index("--") + 1:]
        user = godot[godot.index("--") + 1:]
        self.assertEqual(["--windowed", "--log-file"], godot[:2])
        self.assertEqual("1280x720", godot[godot.index("--resolution") + 1])
        self.assertIn("--verbose", godot[:godot.index("--")])
        self.assertNotIn("--audio-driver", godot)
        output = Path(godot[godot.index("--log-file") + 1]).parent
        self.assertEqual([f"--capture-dir={output}", "--capture-plan=options", "--capture-size=1280x720",
                          "--skipfmv"], user[1:])
        status = json.loads((output / "capture-status.json").read_text(encoding="utf-8"))
        self.assertEqual(("PASS", "PASS", "PASS", 2), (status["status"], status["parityVerdict"],
                                                        status["optionsInkVerdict"], status["shots"]))

    def test_capture_owns_the_rig_arguments_and_capture_options(self) -> None:
        for flag in ("--capture-plan=mainmenu", "--capture-size=640x480", "--capture-offsets-ms=1",
                     "--capture-dir=/elsewhere"):
            with self.subTest(flag=flag):
                self.assertEqual(2, self.capture("--", flag))
        for mode, option in (("smoke", "--plan"), ("run", "--purpose"), ("run", "--offscreen")):
            with self.subTest(mode=mode, option=option), redirect_stderr(io.StringIO()), \
                 self.assertRaises(SystemExit):
                self.invoke(mode, option, "mainmenu" if option == "--plan" else "probe")
        for args in (("--size", "640"), ("--size", "0x480"),
                     ("--plan", "mainmenu", "--retail-offset-manifest", "manifest.json")):
            with self.subTest(args=args), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                self.capture(*args)
        self.assertEqual(2, self.capture("--offscreen", str(self.root / "absent-runner")))
        self.assertEqual([], self.offscreen_calls())

    def test_gameplay_capture_samples_retail_offsets_silently(self) -> None:
        manifest = self.root / "retail-manifest.json"
        manifest.write_text(json.dumps({"frames": [
            {"path": "opening-pan-run1/a.png", "levelOffsetMs": 1006},
            {"path": "hud-timeline-run1/b.png", "levelOffsetMs": 250},
            {"path": "opening-pan-run1/c.png", "levelOffsetMs": 250},
            {"path": "opening-pan-run2/d.png", "levelOffsetMs": 499},
        ]}), encoding="utf-8")
        self.assertEqual(0, self.capture("--plan", "gameplay", "--retail-offset-manifest", str(manifest)))
        call = self.offscreen_calls()[0]
        self.assertEqual("600", call[call.index("--timeout") + 1])
        self.assertEqual("Dummy", call[call.index("--audio-driver") + 1])
        self.assertIn("--capture-offsets-ms=250,1006", call)
        self.assertEqual(2, self.capture("--plan", "gameplay", "--retail-offset-manifest", str(manifest),
                                         "--retail-offset-runs", "absent-run"))

    def test_capture_status_needs_a_healthy_run_and_a_retail_verdict(self) -> None:
        shot = {"width": 640, "height": 480, "screenMatched": True, "saveError": None}
        for environment, code, status in (
            ({}, 0, "PASS"),
            ({"FAKE_PARITY_VERDICT": "FAIL"}, 1, "FAIL"),
            ({"FAKE_PARITY_VERDICT": "UNSCORED"}, 0, "UNSCORED"),
            ({"FAKE_PARITY_VERDICT": "ERROR"}, 2, "SUSPECT"),
            ({"FAKE_PARITY_VERDICT": "MAYBE"}, 2, "SUSPECT"),
            ({"FAKE_PARITY_VERDICT": "NONE"}, 2, "SUSPECT"),
            ({"FAKE_MANIFEST_CHANGES": json.dumps({"plannedShots": 3})}, 2, "SUSPECT"),
            ({"FAKE_MANIFEST_CHANGES": json.dumps({"shots": [shot, shot | {"screenMatched": False}]})},
             2, "SUSPECT"),
            ({"FAKE_MANIFEST_CHANGES": json.dumps({"shots": [shot, shot | {"saveError": "ERR_CANT_OPEN"}]})},
             2, "SUSPECT"),
            ({"FAKE_MANIFEST_CHANGES": json.dumps({"shots": [shot, shot | {"width": 1280}]})}, 2, "SUSPECT"),
        ):
            with self.subTest(environment=environment), mock.patch.dict(os.environ, environment):
                self.assertEqual(code, self.capture("--plan", "mainmenu"))
                output = Path(next(arg.removeprefix("--capture-dir=") for arg in self.offscreen_calls()[-1]
                                   if arg.startswith("--capture-dir=")))
                recorded = json.loads((output / "capture-status.json").read_text(encoding="utf-8"))
                self.assertEqual(status, recorded["status"])
        with mock.patch.dict(os.environ, {"FAKE_OPTIONS_VERDICT": "ERROR"}):
            self.assertEqual(2, self.capture("--plan", "options"))
        with mock.patch.dict(os.environ, {"FAKE_NO_MANIFEST": "1"}):
            self.assertEqual(2, self.capture("--plan", "mainmenu"))
        with mock.patch.dict(os.environ, {"FAKE_OFFSCREEN_EXIT": "124"}):
            self.assertEqual(124, self.capture("--plan", "mainmenu"))

    def test_production_needs_committed_godot_source(self) -> None:
        def stamped(*args: str) -> dict:
            self.capture("--plan", "mainmenu", *args)
            output = Path(next(arg.removeprefix("--capture-dir=") for arg in self.offscreen_calls()[-1]
                               if arg.startswith("--capture-dir=")))
            return json.loads((output / "capture-manifest.json").read_text(encoding="utf-8"))

        # No work tree: the source is unknown, which is never clean.
        manifest = stamped("--purpose", "production")
        self.assertEqual(("probe", "production", "unknown", True, None),
                         (manifest["capturePurpose"], manifest["requestedPurpose"],
                          manifest["godotSourceCleanliness"], manifest["godotSourceDirty"],
                          manifest["sourceCommit"]))
        self.assertIn("unknown", manifest["purposeDowngradeReason"])

        def git(*args: str) -> str:
            return subprocess.run(["git", "-C", str(self.checkout), *args], check=True,
                                  capture_output=True, text=True).stdout.strip()
        git("init", "-q")
        git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "add", "-A")
        git("-c", "user.name=t", "-c", "user.email=t@example.invalid", "commit", "-q", "-m", "fixture")
        head = git("rev-parse", "HEAD")
        (self.checkout / ".gitignore").write_text("local-data/\n", encoding="utf-8")
        manifest = stamped("--purpose", "production")
        self.assertEqual(("production", "clean", False, head, None),
                         (manifest["capturePurpose"], manifest["godotSourceCleanliness"],
                          manifest["godotSourceDirty"], manifest["sourceCommit"],
                          manifest["purposeDowngradeReason"]))
        # Probe stays probe; production over modified Godot source is a probe.
        self.assertEqual("probe", stamped()["capturePurpose"])
        (self.checkout / launcher.PROJECT / "Edited.cs").write_text("// uncommitted\n", encoding="utf-8")
        manifest = stamped("--purpose", "production")
        self.assertEqual(("probe", "dirty", True), (manifest["capturePurpose"],
                                                   manifest["godotSourceCleanliness"],
                                                   manifest["godotSourceDirty"]))
        self.assertIn("1 uncommitted change", manifest["purposeDowngradeReason"])

    def test_zero_exit_without_smoke_completion_is_failure(self) -> None:
        delivered = list(launcher.SMOKE_DELIVERED_MESSAGE_IDS)
        invalid = [None, "{", "[]", "{}"]
        invalid.extend(json.dumps(COMPLETED_SMOKE | {key: value}) for key, value in (
            ("schemaVersion", "old"), ("exitReason", "window-closed"),
            ("finalFrontendScreen", "Gameplay"), ("retrySessionFresh", False),
            ("worldReleasedAtMainMenu", 1), ("tick", "2148"), ("targetVisualCount", 9),
            ("stateHash", "0" * 64), ("retailLevel100PineCount", 0),
            ("level100DeliveredMessageIds", delivered[::-1]),
            ("gameplayCursorPolicy", ["Visible", "Captured", "Visible", "Visible"]),
            ("retailAquilaStandingClearance", 0.0594),
            ("level100PlayingMessageId", 7),
            ("level100VoiceStartedMessageIds", []),
            ("level100VoiceStartedMessageIds", [delivered[1]]),
            ("level100VoiceStartedMessageIds", delivered + [7]),
            ("level100MessagePlaying", True),
            ("mode", "/home/someone/profile"),
        ))
        invalid.append(json.dumps(COMPLETED_SMOKE | {
            "tutorialVoicePlaying": True, "level100PlayingMessageId": None}))
        for payload in invalid:
            with self.subTest(payload=payload), mock.patch.dict(os.environ):
                os.environ.pop("FAKE_SMOKE_REPORT", None)
                if payload is not None:
                    os.environ["FAKE_SMOKE_REPORT"] = payload
                self.assertEqual(2, self.invoke("smoke", "--no-build", "--no-prepare"))

    def test_smoke_report_bounds_host_timing_rather_than_pinning_it(self) -> None:
        for fields in (
            {"level100PlayingMessageId": None, "tutorialVoicePlaying": False},
            {"level100VoiceStartedMessageIds": list(launcher.SMOKE_DELIVERED_MESSAGE_IDS)},
            {"level100MessagePlaying": True, "level100MessagePlaybackAvailable": True},
            {"windowFocusedAtGameplay": True},
        ):
            with self.subTest(fields=fields), \
                 mock.patch.dict(os.environ, {"FAKE_SMOKE_REPORT": json.dumps(COMPLETED_SMOKE | fields)}):
                self.assertEqual(0, self.invoke("smoke", "--no-build", "--no-prepare"))

    def test_smoke_needs_a_clean_godot_log(self) -> None:
        report = {"FAKE_SMOKE_REPORT": json.dumps(COMPLETED_SMOKE)}
        for environment in ({"FAKE_NO_LOG": "1"}, {"FAKE_LOG": "SCRIPT ERROR: boom\n"},
                            {"FAKE_LOG": "ERROR: Condition failed\n"}):
            with self.subTest(environment=environment), mock.patch.dict(os.environ, report | environment):
                self.assertEqual(2, self.invoke("smoke", "--no-build", "--no-prepare"))

    def test_native_smoke_failure_is_not_replaced_by_missing_report_error(self) -> None:
        with mock.patch.dict(os.environ, {"FAKE_ENGINE_EXIT": "17"}):
            self.assertEqual(17, self.invoke("smoke", "--no-build", "--no-prepare"))

    def test_captured_version_timeout_retains_diagnostics(self) -> None:
        error = subprocess.TimeoutExpired([str(self.engine), "--version"], 30,
                                          output=b"version stalled\n", stderr=b"detail\n")
        output = io.StringIO()
        with mock.patch.object(launcher, "run_process", side_effect=error), redirect_stderr(output):
            self.assertEqual(124, self.invoke("smoke", "--no-build", "--no-prepare"))
        self.assertIn("version stalled\n", output.getvalue())
        self.assertIn("detail\n", output.getvalue())

    def _descendant_command(self, *, exit_early: bool = False) -> tuple[list[str], Path]:
        pid_path = self.root / "descendant.pid"
        child = (
            "import os,pathlib,signal,time; "
            "signal.signal(signal.SIGTERM, signal.SIG_IGN); "
            f"pathlib.Path({str(pid_path)!r}).write_text(str(os.getpid())); "
            "time.sleep(30)"
        )
        parent = (
            "import pathlib,subprocess,sys,time; "
            f"subprocess.Popen([sys.executable, '-c', {child!r}]); "
            f"p=pathlib.Path({str(pid_path)!r})\n"
            "while not p.exists(): time.sleep(0.01)\n"
            + ("raise SystemExit(19)\n" if exit_early else "time.sleep(30)\n")
        )
        return [sys.executable, "-c", parent], pid_path

    def assert_not_running(self, pid_path: Path) -> None:
        pid = int(pid_path.read_text(encoding="utf-8"))
        stat = Path(f"/proc/{pid}/stat")
        deadline = time.monotonic() + 1
        while time.monotonic() < deadline:
            try:
                state = stat.read_text().split(") ", 1)[1].split()[0]
            except FileNotFoundError:
                return
            # SIGKILL delivery is asynchronous; a dead orphan can await init's reap.
            if state == "Z":
                return
            time.sleep(0.01)
        self.fail(f"owned descendant {pid} still runs after process-group cleanup")

    def test_timeout_stops_term_resistant_descendant(self) -> None:
        command, pid = self._descendant_command()
        with self.assertRaises(subprocess.TimeoutExpired):
            launcher.run_process(command, cwd=self.root, env=dict(os.environ), timeout=1)
        self.assert_not_running(pid)

    def test_failure_stops_descendant_after_its_parent_has_exited(self) -> None:
        command, pid = self._descendant_command(exit_early=True)
        with self.assertRaises(subprocess.CalledProcessError) as failure:
            launcher.run_process(command, cwd=self.root, env=dict(os.environ), timeout=3)
        self.assertEqual(19, failure.exception.returncode)
        self.assert_not_running(pid)

    def test_interruption_stops_owned_process_group(self) -> None:
        command, pid = self._descendant_command()
        original_communicate = subprocess.Popen.communicate

        def interrupt_after_start(process, *args, **kwargs):
            try:
                original_communicate(process, timeout=0.5)
            except subprocess.TimeoutExpired:
                raise KeyboardInterrupt

        with mock.patch.object(subprocess.Popen, "communicate", interrupt_after_start):
            with self.assertRaises(KeyboardInterrupt):
                launcher.run_process(command, cwd=self.root, env=dict(os.environ), timeout=3)
        self.assert_not_running(pid)


if __name__ == "__main__":
    unittest.main()
