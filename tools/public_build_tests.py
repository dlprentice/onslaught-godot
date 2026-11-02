#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Public runner regressions with authored reports and fake tool bundles only."""
from __future__ import annotations

import tempfile
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
import zipfile

import public_build as runner


class PublicEnvironmentTests(unittest.TestCase):
    def test_machine_feeds_profiles_and_retail_inputs_do_not_reach_authored_checks(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            inherited = {
                "PATH": "/usr/bin", "HOME": "/unchanged-user-home", "LANG": "C.UTF-8",
                "DOTNET_ROOT": "/unrelated-dotnet", "MSBuildSDKsPath": "/unrelated-sdk",
                "NuGetPackageSourceCredentials_Private": "not-for-the-child",
                "NUGET_PLUGIN_PATHS": "/unrelated-plugin", "HOST_LIVE": "flow", "DISPLAY": ":42",
                "ONSLAUGHT_GAME_DIR": "/unrelated-install", "ONSLAUGHT_PORT_REFS": "/unrelated-recordings",
            }
            env = runner.child_environment(root, root / "run", root / "dotnet/dotnet", root / "sdks", inherited)
            self.assertEqual(inherited["HOME"], env["HOME"])
            self.assertNotIn("NuGetPackageSourceCredentials_Private", env)
            self.assertNotIn("NUGET_PLUGIN_PATHS", env)
            self.assertNotIn("HOST_LIVE", env)
            self.assertNotIn("DISPLAY", env)
            self.assertEqual(str(root / "dotnet"), env["DOTNET_ROOT"])
            self.assertEqual("1", env["MSBUILDDISABLENUGETSDKRESOLVER"])
            for name in runner.REFERENCE_VARIABLES:
                self.assertEqual(str(root / "run/unavailable-reference-inputs"), env[name])
                self.assertFalse(Path(env[name]).exists())
            for name in ("DOTNET_CLI_HOME", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "TMPDIR"):
                self.assertTrue(Path(env[name]).is_relative_to(root))

    def test_restore_config_has_only_public_and_supplied_bundle_sources(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "NuGet.Config"
            runner.write_nuget_config(path, root / "Godot & tools/godot")
            config = ET.parse(path)
            sources = config.findall("./packageSources/add")
            self.assertIsNotNone(config.find("./packageSources/clear"))
            self.assertEqual([
                str(root / "Godot & tools/GodotSharp/Tools/nupkgs"),
                "https://api.nuget.org/v3/index.json",
            ], [source.get("value") for source in sources])
            self.assertIsNotNone(config.find("./fallbackPackageFolders/clear"))


class SdkBundleTests(unittest.TestCase):
    def make_bundle(self, root: Path, entry: str = "Sdk/Sdk.props"):
        dotnet = root / "dotnet/dotnet"
        (dotnet.parent / "sdk/10.0.402/Sdks/Microsoft.NET.Sdk/Sdk").mkdir(parents=True)
        engine = root / "godot/godot"
        packages = engine.parent / "GodotSharp/Tools/nupkgs"
        packages.mkdir(parents=True)
        with zipfile.ZipFile(packages / "Godot.NET.Sdk.4.8.0-beta.1.nupkg", "w") as archive:
            archive.writestr(entry, "<Project />")
        return dotnet, engine

    def test_pinned_bundle_sdk_is_used_without_changing_the_dotnet_install(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            dotnet, engine = self.make_bundle(root)
            combined = runner.sdk_directory(root, dotnet, engine, "10.0.402", "4.8.0-beta.1")
            self.assertEqual(dotnet.parent / "sdk/10.0.402/Sdks/Microsoft.NET.Sdk",
                             (combined / "Microsoft.NET.Sdk").resolve())
            self.assertEqual("<Project />", (combined / "Godot.NET.Sdk/Sdk/Sdk.props").read_text())
            self.assertFalse((dotnet.parent / "sdk/10.0.402/Sdks/Godot.NET.Sdk").exists())
            self.assertEqual(combined, runner.sdk_directory(root, dotnet, engine, "10.0.402", "4.8.0-beta.1"))
            (combined / "Godot.NET.Sdk/Sdk/Sdk.props").write_text("changed")
            with self.assertRaisesRegex(ValueError, "changed"):
                runner.sdk_directory(root, dotnet, engine, "10.0.402", "4.8.0-beta.1")

    def test_bundle_cannot_write_outside_its_extraction_root(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            dotnet, engine = self.make_bundle(root, "../escape")
            with self.assertRaisesRegex(ValueError, "package path"):
                runner.sdk_directory(root, dotnet, engine, "10.0.402", "4.8.0-beta.1")

    def test_linked_sdk_cache_is_refused_without_writing_its_target(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            dotnet, engine = self.make_bundle(root)
            cache = root / "local-data/public-build/cache/godot-sdk"
            cache.mkdir(parents=True)
            target = root / "retained"
            target.mkdir()
            (cache / "4.8.0-beta.1").symlink_to(target, target_is_directory=True)
            with self.assertRaisesRegex(ValueError, "linked SDK cache"):
                runner.sdk_directory(root, dotnet, engine, "10.0.402", "4.8.0-beta.1")
            self.assertEqual([], list(target.iterdir()))


class AuthoredResultsTests(unittest.TestCase):
    def report(self, root, *, total=1, executed=1, passed=1, outcomes=("Passed",)):
        document = ET.Element("TestRun", xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
        summary = ET.SubElement(document, "ResultSummary")
        ET.SubElement(summary, "Counters", total=str(total), executed=str(executed), passed=str(passed))
        results = ET.SubElement(document, "Results")
        for outcome in outcomes:
            ET.SubElement(results, "UnitTestResult", outcome=outcome)
        path = root / "authored.trx"
        ET.ElementTree(document).write(path)
        return path

    def test_a_nonempty_passing_selection_is_accepted(self):
        with tempfile.TemporaryDirectory() as temporary:
            self.assertEqual(1, runner.check_authored_results(self.report(Path(temporary))))

    def test_empty_skipped_failed_and_incomplete_reports_fail(self):
        for values in (
            {"total": 0, "executed": 0, "passed": 0, "outcomes": ()},
            {"executed": 0, "passed": 0, "outcomes": ("NotExecuted",)},
            {"passed": 0, "outcomes": ("Failed",)},
            {"outcomes": ()},
        ):
            with self.subTest(values=values), tempfile.TemporaryDirectory() as temporary:
                with self.assertRaises(ValueError):
                    runner.check_authored_results(self.report(Path(temporary), **values))


if __name__ == "__main__":
    unittest.main()
