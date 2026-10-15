#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Engine discovery and owned-process handling with fake tools; no window or retail data is used."""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import godot_host as host


@unittest.skipUnless(sys.platform.startswith("linux"), "Linux engine discovery")
class EnginePathTests(unittest.TestCase):
    def test_sdk_resolution_rejects_an_engine_without_the_pinned_package(self) -> None:
        with tempfile.TemporaryDirectory(prefix="godot-host-tests-") as temporary:
            engine = Path(temporary) / "engine/godot48-mono"
            packages = engine.parent / "GodotSharp/Tools/nupkgs"
            packages.mkdir(parents=True)
            (packages / "Godot.NET.Sdk.4.7.2.nupkg").touch()
            engine.write_text("#!/bin/sh\n", encoding="utf-8")
            engine.chmod(0o700)
            with self.assertRaisesRegex(RuntimeError, "4.8.0-dev.6 bundled SDK package is missing"):
                host.engine_path(str(engine))
            (packages / "Godot.NET.Sdk.4.8.0-dev.6.nupkg").touch()
            self.assertEqual(engine, host.engine_path(str(engine)))


@unittest.skipUnless(sys.platform.startswith("linux"), "Linux process-group launcher")
class OwnedProcessTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory(prefix="godot-process-tests-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.processes = []
        real_popen = subprocess.Popen
        self.popen_type = real_popen

        def start(*args, **kwargs):
            process = real_popen(*args, **kwargs)
            self.processes.append(process)
            return process

        patch = mock.patch.object(host.subprocess, "Popen", side_effect=start)
        patch.start()
        self.addCleanup(patch.stop)
        self.addCleanup(self.close_pipes)

    def close_pipes(self) -> None:
        for process in self.processes:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()

    def run_script(self, script: str, timeout: float = 0.25):
        return host.run_process(["/bin/sh", "-c", script], cwd=self.root,
                                env=dict(os.environ), timeout=timeout, capture=True)

    def test_timeout_retains_output_written_during_shutdown(self) -> None:
        script = (
            "trap 'printf shutdown-out; printf shutdown-err >&2; exit 0' TERM; "
            "printf started-out; printf started-err >&2; "
            "while :; do sleep 1; done"
        )
        with self.assertRaises(subprocess.TimeoutExpired) as failure:
            self.run_script(script)
        error = failure.exception
        self.assertEqual(0.25, error.timeout)
        self.assertIn(b"started-out", error.output)
        self.assertIn(b"shutdown-out", error.output)
        self.assertIn(b"started-err", error.stderr)
        self.assertIn(b"shutdown-err", error.stderr)
        self.assertIsNotNone(self.processes[0].returncode)

    def test_timeout_closes_both_capture_pipes(self) -> None:
        with self.assertRaises(subprocess.TimeoutExpired):
            self.run_script("while :; do sleep 1; done")
        process = self.processes[0]
        self.assertTrue(process.stdout.closed)
        self.assertTrue(process.stderr.closed)
        self.assertIsNotNone(process.returncode)

    def test_nonzero_exit_keeps_both_streams_and_status(self) -> None:
        with self.assertRaises(subprocess.CalledProcessError) as failure:
            self.run_script("printf normal-out; printf normal-err >&2; exit 17", timeout=3)
        self.assertEqual(17, failure.exception.returncode)
        self.assertEqual("normal-out", failure.exception.stdout)
        self.assertEqual("normal-err", failure.exception.stderr)
        self.assertTrue(self.processes[0].stdout.closed)
        self.assertTrue(self.processes[0].stderr.closed)



    def test_interruption_closes_capture_pipes_after_stopping_the_group(self) -> None:
        with mock.patch.object(self.popen_type, "communicate", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt):
                self.run_script("sleep 30")
        process = self.processes[0]
        self.assertIsNotNone(process.returncode)
        self.assertTrue(process.stdout.closed)
        self.assertTrue(process.stderr.closed)

    def test_output_drain_is_bounded_and_preserves_the_original_timeout(self) -> None:
        original = self.popen_type.communicate
        calls = []

        def simulate_held_pipe(process, *args, **kwargs):
            calls.append(kwargs.get("timeout"))
            if len(calls) == 2:
                raise subprocess.TimeoutExpired(process.args, kwargs["timeout"],
                                                 output=b"retained-tail", stderr=b"retained-error")
            return original(process, *args, **kwargs)

        with mock.patch.object(self.popen_type, "communicate", simulate_held_pipe):
            with self.assertRaises(subprocess.TimeoutExpired) as failure:
                self.run_script("printf started; sleep 30")
        self.assertEqual([0.25, 1.0], calls)
        self.assertEqual(0.25, failure.exception.timeout)
        self.assertEqual(b"retained-tail", failure.exception.output)
        self.assertEqual(b"retained-error", failure.exception.stderr)
        self.assertIn("drain exceeded", " ".join(failure.exception.__notes__))
        self.assertTrue(self.processes[0].stdout.closed)
        self.assertTrue(self.processes[0].stderr.closed)

    def test_invalid_final_text_does_not_replace_the_timeout_exception(self) -> None:
        with self.assertRaises(subprocess.TimeoutExpired) as failure:
            self.run_script(r"printf '\377'; sleep 30")
        self.assertEqual(b"\xff", failure.exception.output)
        self.assertEqual(0.25, failure.exception.timeout)
        self.assertTrue(self.processes[0].stdout.closed)
        self.assertTrue(self.processes[0].stderr.closed)

if __name__ == "__main__":
    unittest.main()
