#!/usr/bin/env python3
"""Synthetic process controls for the host-flow runner; never starts Godot."""

from pathlib import Path
import importlib.util
import os
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location(
    "host_flow_check", Path(__file__).with_name("host_flow_check.py")
)
assert SPEC is not None and SPEC.loader is not None
host_flow_check = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(host_flow_check)


class DirectRecursionGuardTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp(prefix="synthetic-host-flow-"))
        self.host = self.root / "host"
        self.host.mkdir()
        self.bin = self.host / "bin"
        self.bin.mkdir()
        self.wrapper = self.bin / "godot-headless"
        self.wrapper.write_text("#!/bin/sh\nexit 7\n")
        self.wrapper.chmod(0o700)
        self.engine = self.bin / "synthetic-engine"
        self.engine.write_text("#!/bin/sh\nexit 9\n")
        self.engine.chmod(0o700)
        self.log = self.root / "host.log"

    def run_host(self, env, reject=False):
        before = env.copy()
        with patch.object(host_flow_check, "HOST", self.host), \
             patch.object(host_flow_check.subprocess, "run") as spawn:
            spawn.return_value.returncode = 23
            if reject:
                with self.assertRaises(SystemExit):
                    host_flow_check.run_host(env, self.log)
                spawn.assert_not_called()
                self.assertFalse(self.log.exists())
            else:
                self.assertEqual(host_flow_check.run_host(env, self.log), 23)
                spawn.assert_called_once()
                self.assertEqual(spawn.call_args.args[0][0], "godot-headless")
                self.assertEqual(spawn.call_args.kwargs["cwd"], self.host)
                self.assertEqual(spawn.call_args.kwargs["env"], before)
        self.assertEqual(env, before)

    def test_explicit_name_and_absolute_wrapper_are_rejected(self):
        for engine in ("godot-headless", str(self.wrapper)):
            with self.subTest(engine=engine):
                self.run_host({"PATH": str(self.bin), "GODOT": engine}, reject=True)

    def test_symbolic_and_hard_link_aliases_are_rejected(self):
        symbolic, hard = self.bin / "symbolic", self.bin / "hard"
        symbolic.symlink_to(self.wrapper)
        os.link(self.wrapper, hard)
        for engine in (str(symbolic), str(hard)):
            with self.subTest(engine=engine):
                self.run_host({"PATH": str(self.bin), "GODOT": engine}, reject=True)

    def test_relative_path_entries_are_resolved_in_child_working_directory(self):
        self.run_host({"PATH": "bin", "GODOT": "godot-headless"}, reject=True)
        self.run_host({"PATH": "bin", "GODOT": "./bin/godot-headless"}, reject=True)

    def test_empty_path_searches_child_working_directory(self):
        os.link(self.wrapper, self.host / "godot-headless")
        self.run_host({"PATH": "", "GODOT": "./godot-headless"}, reject=True)

    def test_unset_empty_and_distinct_engine_preserve_existing_launch(self):
        for env in ({}, {"PATH": str(self.bin)},
                    {"PATH": str(self.bin), "GODOT": ""},
                    {"PATH": str(self.bin), "GODOT": str(self.engine)}):
            with self.subTest(godot=env.get("GODOT")):
                self.run_host(env)

    def test_distinct_file_with_wrapper_basename_still_launches(self):
        other = self.host / "other"
        other.mkdir()
        engine = other / "godot-headless"
        engine.write_text("#!/bin/sh\nexit 11\n")
        engine.chmod(0o700)
        self.run_host({"PATH": str(self.bin), "GODOT": str(engine)})

    def test_unresolved_wrapper_or_engine_preserve_existing_launch(self):
        for env in ({"PATH": str(self.bin), "GODOT": "not-present"},
                    {"PATH": str(self.root / "absent"), "GODOT": str(self.wrapper)}):
            with self.subTest(env=env):
                self.run_host(env)


if __name__ == "__main__":
    unittest.main()
