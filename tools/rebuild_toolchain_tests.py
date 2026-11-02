#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Authored archives test public toolchain provenance, bounds and refusal behavior."""
from __future__ import annotations

import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import stat
import tarfile
import tempfile
import unittest
from unittest import mock
import zipfile

import rebuild_toolchain as tool

REAL_DOWNLOAD = tool.download


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def make_zip(path: Path, files: dict[str, bytes], *, executable: set[str] = frozenset()) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        for name, data in files.items():
            entry = zipfile.ZipInfo(name)
            entry.external_attr = (stat.S_IFREG | (0o755 if name in executable else 0o644)) << 16
            archive.writestr(entry, data)


def make_tar(path: Path, files: dict[str, bytes]) -> None:
    with tarfile.open(path, "w:gz") as archive:
        root = tarfile.TarInfo(".")
        root.type, root.mode = tarfile.DIRTYPE, 0o755
        archive.addfile(root)
        for name, data in files.items():
            entry = tarfile.TarInfo(name)
            entry.size, entry.mode = len(data), 0o755 if name == "dotnet" else 0o644
            archive.addfile(entry, io.BytesIO(data))


class PublicToolchainTests(unittest.TestCase):
    def setUp(self) -> None:
        owner = tool.ROOT / "local-data/toolchain-tests"
        owner.mkdir(parents=True, exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=owner)
        self.addCleanup(temporary.cleanup)
        self.checkout = Path(temporary.name)
        self.root = self.checkout / "local-data/toolchains/rebuild"
        self.inputs = self.checkout / "inputs"
        self.inputs.mkdir()
        self.pins = {"schemaVersion": 1, "engineVersion": "test-engine", "templateVersion": "test-template",
                     "engine": {"file": "engine.zip", "executable": "engine", "sdkPackage": "GodotSharp/sdk.nupkg",
                                "executableSha256": sha(b"engine"), "sdkPackageSha256": sha(b"sdk")},
                     "templates": {"file": "templates.zip", "fileSha256": {"version.txt": sha(b"test-template\n"),
                                   "linux_release": sha(b"linux"), "windows_release": sha(b"windows")}},
                     "dotnet": {"sdkVersion": "1.0.0", "runtimeVersion": "1.0.1"}}
        make_zip(self.inputs / "engine.zip", {"engine/engine": b"engine", "engine/GodotSharp/sdk.nupkg": b"sdk"},
                 executable={"engine/engine"})
        make_zip(self.inputs / "templates.zip", {"templates/version.txt": b"test-template\n",
                 "templates/linux_release": b"linux", "templates/windows_release": b"windows"})
        make_tar(self.inputs / "dotnet-sdk-1.0.0-linux-x64.tar.gz", {"dotnet": b"dotnet",
                 "sdk/1.0.0/dotnet.dll": b"sdk", "shared/Microsoft.NETCore.App/1.0.1/Microsoft.NETCore.App.deps.json": b"{}"})
        self.manifest = {"schemaVersion": 1, "platform": "linux-x86_64"}
        for name, filename, wrapper, kind in (("engine", "engine.zip", "engine", "zip"),
                  ("templates", "templates.zip", "templates", "zip"),
                  ("dotnet", "dotnet-sdk-1.0.0-linux-x64.tar.gz", "", "tar.gz")):
            path = self.inputs / filename
            self.manifest[name] = {"url": "https://github.com/official/" + filename,
                "sizeBytes": path.stat().st_size, "sha256": tool.sha_file(path), "license": "MIT",
                "licenseUrl": "https://example.org/license", "sourceSubdirectory": wrapper,
                "archiveType": kind, "maxFiles": 100, "maxExpandedBytes": 10000}
            if name != "dotnet":
                self.pins[name]["archiveSha256"] = tool.sha_file(path)
        self.pins["engine"]["payloadSha256"] = tool.archive_payload(self.inputs / "engine.zip", self.manifest["engine"])
        self.pin_file, self.manifest_file = self.checkout / "pins.json", self.checkout / "manifest.json"
        self.pin_file.write_text(json.dumps(self.pins))
        self.manifest_file.write_text(json.dumps(self.manifest))
        self.stack = contextlib.ExitStack()
        self.addCleanup(self.stack.close)
        self.stack.enter_context(mock.patch.object(tool, "ROOT", self.checkout))
        self.stack.enter_context(mock.patch.object(tool, "PINS", self.pin_file))
        self.stack.enter_context(mock.patch.object(tool, "MANIFEST", self.manifest_file))
        self.stack.enter_context(mock.patch.object(tool, "canonical_root", return_value=self.checkout))
        self.stack.enter_context(mock.patch.object(tool.subprocess, "run", return_value=mock.Mock(returncode=0)))
        self.stack.enter_context(mock.patch("builtins.print"))
        self.downloads = []
        def authored_download(path, spec):
            self.downloads.append(path.name)
            shutil.copyfile(self.inputs / path.name, path)
            tool.verify_archive(path, spec)
        self.stack.enter_context(mock.patch.object(tool, "download", side_effect=authored_download))

    def test_setup_and_verify_use_public_sources_without_executing_tools(self) -> None:
        engine, templates, dotnet = tool.setup(self.root)
        self.assertEqual(self.downloads, ["engine.zip", "dotnet-sdk-1.0.0-linux-x64.tar.gz"])
        self.assertIsNone(templates)
        self.assertTrue(engine.stat().st_mode & 0o111)
        self.assertEqual(dotnet.read_bytes(), b"dotnet")
        with mock.patch.object(tool.urllib.request, "build_opener", side_effect=AssertionError("network during verify")):
            self.assertEqual(tool.verified_install(self.root, self.pins, []), (engine, None, dotnet))

    def test_repeat_setup_verifies_existing_files_without_downloading(self) -> None:
        tool.setup(self.root)
        before = (self.root / "engine" / tool.MARKER).read_bytes()
        tool.setup(self.root)
        self.assertEqual(len(self.downloads), 2)
        self.assertEqual((self.root / "engine" / tool.MARKER).read_bytes(), before)

    def test_templates_are_explicit_and_keep_pinned_files(self) -> None:
        tool.setup(self.root)
        with self.assertRaises((tool.ToolchainError, FileNotFoundError)):
            tool.verified_install(self.root, self.pins, ["linux"])
        _, templates, _ = tool.setup(self.root, with_templates=True)
        self.assertEqual(templates, self.root / "templates")
        self.assertEqual(self.downloads[-1], "templates.zip")

    def test_differing_existing_install_is_preserved(self) -> None:
        tool.setup(self.root)
        selected = self.root / "engine/engine"
        selected.write_bytes(b"unique edit")
        with self.assertRaisesRegex(tool.ToolchainError, "differs"):
            tool.setup(self.root)
        self.assertEqual(selected.read_bytes(), b"unique edit")
        self.assertEqual(len(self.downloads), 2)

    def test_changed_payload_and_receipt_cannot_replace_archive_evidence(self) -> None:
        tool.setup(self.root)
        selected = self.root / "dotnet/dotnet"
        selected.write_bytes(b"different sdk")
        receipt_path = self.root / "dotnet" / tool.MARKER
        receipt = json.loads(receipt_path.read_text())
        receipt["payloadSha256"] = tool.installed_payload(self.root / "dotnet", self.manifest["dotnet"])
        receipt_path.write_text(json.dumps(receipt))
        with self.assertRaisesRegex(tool.ToolchainError, "receipt differs"):
            tool.verified_install(self.root, self.pins, [])
        self.assertEqual(selected.read_bytes(), b"different sdk")

    def test_extra_file_and_changed_executable_mode_are_not_accepted(self) -> None:
        tool.setup(self.root)
        extra = self.root / "dotnet/unexpected"
        extra.write_text("extra")
        with self.assertRaisesRegex(tool.ToolchainError, "differs"):
            tool.verified_install(self.root, self.pins, [])
        extra.unlink()
        (self.root / "engine/engine").chmod(0o644)
        with self.assertRaisesRegex(tool.ToolchainError, "differs"):
            tool.verified_install(self.root, self.pins, [])

    def test_changed_archive_is_refused_without_replacement(self) -> None:
        tool.setup(self.root)
        selected = self.root / "downloads/engine.zip"
        selected.write_bytes(b"X" * selected.stat().st_size)
        with self.assertRaisesRegex(tool.ToolchainError, "SHA-256"):
            tool.setup(self.root)
        self.assertEqual(selected.read_bytes()[:1], b"X")

    def test_outside_or_linked_install_roots_write_nothing(self) -> None:
        outside = self.checkout / "outside"
        with self.assertRaises(tool.ToolchainError):
            tool.setup(outside)
        self.assertFalse(outside.exists())
        (self.checkout / "local-data").mkdir()
        target = self.checkout / "linked-target"
        target.mkdir()
        (self.checkout / "local-data/link").symlink_to(target, target_is_directory=True)
        with self.assertRaises(tool.ToolchainError):
            tool.setup(self.checkout / "local-data/link/toolchain")
        self.assertEqual(list(target.iterdir()), [])

    def test_install_directory_symlink_is_preserved_and_refused(self) -> None:
        self.root.mkdir(parents=True)
        target = self.checkout / "target"
        target.mkdir()
        (self.root / "engine").symlink_to(target, target_is_directory=True)
        with self.assertRaises((tool.ToolchainError, FileNotFoundError)):
            tool.setup(self.root)
        self.assertTrue((self.root / "engine").is_symlink())
        self.assertEqual(list(target.iterdir()), [])

    def test_non_linux_host_and_unknown_platform_fail_before_download(self) -> None:
        with mock.patch.object(tool.sys, "platform", "win32"):
            with self.assertRaises(tool.ToolchainError):
                tool.setup(self.root)
        with self.assertRaises(tool.ToolchainError):
            tool.verified_install(self.root, self.pins, ["other"])
        self.assertEqual(self.downloads, [])

    def test_unsafe_zip_names_and_duplicate_members_are_refused(self) -> None:
        path = self.inputs / "hostile.zip"
        spec = {**self.manifest["engine"], "sourceSubdirectory": ""}
        for name in ("../escape", "/absolute", "bad\\name", "C:/drive", "a/../escape", tool.MARKER):
            with self.subTest(name=name):
                make_zip(path, {name: b"bad"})
                with self.assertRaises(tool.ToolchainError):
                    tool.archive_payload(path, spec)
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("same", b"first")
            with self.assertWarns(UserWarning):
                archive.writestr("same", b"second")
        with self.assertRaisesRegex(tool.ToolchainError, "duplicate"):
            tool.archive_payload(path, spec)
        self.assertFalse((self.checkout / "escape").exists())

    def test_archive_links_and_special_files_are_refused(self) -> None:
        path = self.inputs / "link.zip"
        entry = zipfile.ZipInfo("link")
        entry.external_attr = (stat.S_IFLNK | 0o777) << 16
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(entry, "../elsewhere")
        with self.assertRaises(tool.ToolchainError):
            tool.archive_payload(path, {**self.manifest["engine"], "sourceSubdirectory": ""})
        path = self.inputs / "link.tar.gz"
        for kind in (tarfile.SYMTYPE, tarfile.LNKTYPE, tarfile.FIFOTYPE, tarfile.CHRTYPE):
            with self.subTest(kind=kind), tarfile.open(path, "w:gz") as archive:
                entry = tarfile.TarInfo("bad")
                entry.type, entry.linkname = kind, "elsewhere"
                archive.addfile(entry)
            with self.assertRaises(tool.ToolchainError):
                tool.archive_payload(path, self.manifest["dotnet"])

    def test_extraction_budgets_and_nonempty_destination_are_refused(self) -> None:
        path = self.inputs / "engine.zip"
        for limits in ({"maxFiles": 1}, {"maxExpandedBytes": 1}):
            with self.subTest(limits=limits), self.assertRaises(tool.ToolchainError):
                tool.archive_payload(path, {**self.manifest["engine"], **limits})
        destination = self.checkout / "extract"
        destination.mkdir()
        unique = destination / "unique"
        unique.write_text("keep")
        with self.assertRaises(tool.ToolchainError):
            tool.archive_payload(path, self.manifest["engine"], destination)
        self.assertEqual(unique.read_text(), "keep")

    def test_download_size_and_hash_fail_before_publication(self) -> None:
        owner = self.checkout / "downloads"
        owner.mkdir()
        target = owner / "download.zip"
        spec = {**self.manifest["engine"], "sizeBytes": 3, "sha256": sha(b"yes")}
        for content in (b"too much", b"bad", b"x"):
            response = io.BytesIO(content)
            response.headers = {}
            opener = mock.Mock()
            opener.open.return_value = response
            with self.subTest(content=content), mock.patch.object(tool.urllib.request, "build_opener", return_value=opener):
                with self.assertRaises(tool.ToolchainError):
                    REAL_DOWNLOAD(target, spec)
                self.assertFalse(target.exists())
                self.assertEqual(list(owner.iterdir()), [])

    def test_existing_download_and_directory_are_never_replaced(self) -> None:
        target = self.checkout / "existing"
        target.write_bytes(b"unique")
        with mock.patch.object(tool.urllib.request, "build_opener", side_effect=AssertionError("network")):
            with self.assertRaises(tool.ToolchainError):
                REAL_DOWNLOAD(target, self.manifest["engine"])
        self.assertEqual(target.read_bytes(), b"unique")
        source, destination = self.checkout / "source", self.checkout / "destination"
        source.mkdir()
        destination.mkdir()
        with self.assertRaises(FileExistsError):
            tool.rename_new(source, destination)
        self.assertTrue(source.is_dir())
        self.assertTrue(destination.is_dir())

    def test_invalid_sdk_is_refused_before_engine_download(self) -> None:
        self.root.mkdir(parents=True)
        (self.root / "dotnet").mkdir()
        unique = self.root / "dotnet/unique"
        unique.write_text("keep")
        with self.assertRaises((tool.ToolchainError, FileNotFoundError)):
            tool.setup(self.root)
        self.assertEqual(unique.read_text(), "keep")
        self.assertEqual(self.downloads, [])
        self.assertFalse((self.root / "engine").exists())

    def test_fifo_lock_and_linked_archive_are_refused_without_waiting(self) -> None:
        self.root.mkdir(parents=True)
        os.mkfifo(self.root / ".setup.lock")
        with self.assertRaises(OSError):
            tool.setup(self.root)
        linked = self.checkout / "archive-link"
        linked.symlink_to(self.inputs / "engine.zip")
        with self.assertRaises(tool.ToolchainError):
            tool.verify_archive(linked, self.manifest["engine"])
        hardlink = self.checkout / "archive-hardlink"
        os.link(self.inputs / "engine.zip", hardlink)
        with self.assertRaises(tool.ToolchainError):
            tool.verify_archive(hardlink, self.manifest["engine"])


if __name__ == "__main__":
    unittest.main()
