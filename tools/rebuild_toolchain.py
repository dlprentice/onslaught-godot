#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Install/verify the pinned public Linux x86_64 Rebuild toolchain locally.

Only setup downloads official public sources. verified_install() never downloads
or executes a payload. Generated files stay below ignored local-data/.
"""
from __future__ import annotations

import argparse
import base64
import ctypes
import fcntl
import gzip
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import platform
import stat
import subprocess
import sys
import tarfile
import tempfile
import time
from typing import Any, BinaryIO
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = Path(__file__).with_suffix(".json")
PINS = MANIFEST
MARKER = ".rebuild-toolchain.json"
MAX_MEMBER = 512 * 1024 * 1024
CHUNK = 1024 * 1024


class ToolchainError(RuntimeError):
    """A missing, unsafe or differing input; no replacement is attempted."""


def read_json(path: Path) -> dict[str, Any]:
    if path.is_symlink() or not path.is_file() or path.stat().st_size > 1024 * 1024:
        raise ToolchainError(f"missing or unsafe JSON input: {path}")
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ToolchainError(f"expected a JSON object: {path}")
    return value


def canonical_root() -> Path:
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    common = subprocess.check_output(
        ["git", "-C", str(ROOT), "rev-parse", "--path-format=absolute", "--git-common-dir"],
        env=environment, text=True).strip()
    return Path(common).parent.resolve(strict=True)


def default_root() -> Path:
    return ROOT / "local-data/toolchains/rebuild"


def check_host() -> None:
    if sys.platform != "linux" or platform.machine().lower() not in ("x86_64", "amd64"):
        raise ToolchainError("the standalone toolchain supports Linux x86_64 only")


def ordinary_path(path: Path) -> None:
    for ancestor in reversed((path, *path.parents)):
        try:
            mode = ancestor.lstat().st_mode
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(mode):
            raise ToolchainError(f"linked toolchain path refused: {ancestor}")
        if ancestor != path and not stat.S_ISDIR(mode):
            raise ToolchainError(f"non-directory toolchain ancestor: {ancestor}")


def checked_root(root: Path, *, create: bool) -> Path:
    root = Path(os.path.abspath(root.expanduser()))
    owners = [ROOT / "local-data", canonical_root() / "local-data"]
    owner = next((owner for owner in owners if root != owner and root.is_relative_to(owner)), None)
    if owner is None:
        raise ToolchainError("toolchain root must be below this checkout's ignored local-data/")
    ordinary_path(root)
    environment = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
    ignored = subprocess.run(["git", "-C", str(owner.parent), "check-ignore", "-q", str(root / "probe")],
                             env=environment, check=False)
    if ignored.returncode != 0:
        raise ToolchainError("toolchain root is not ignored by Git")
    if create:
        root.mkdir(parents=True, exist_ok=True, mode=0o700)
    if not root.is_dir():
        raise ToolchainError(f"toolchain is absent; run tools/rebuild_toolchain.py setup: {root}")
    return root


def sha_file(path: Path, algorithm: str = "sha256") -> str:
    descriptor = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK)
    with os.fdopen(descriptor, "rb") as stream:
        before = os.fstat(stream.fileno())
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            raise ToolchainError(f"not an ordinary single-link file: {path}")
        result = hashlib.file_digest(stream, algorithm).hexdigest()
        after = os.fstat(stream.fileno())
        if (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (
                after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns, after.st_ctime_ns):
            raise ToolchainError(f"file changed while verifying: {path}")
        return result


def specs(pins: dict[str, Any]) -> dict[str, dict[str, Any]]:
    manifest = read_json(MANIFEST)
    if manifest.get("schemaVersion") != 1 or pins.get("schemaVersion") != 1 or manifest.get("platform") != "linux-x86_64":
        raise ToolchainError("unsupported public toolchain schema")
    result = {}
    for name in ("engine", "dotnet", "templates"):
        entry = dict(manifest[name])
        parsed = urllib.parse.urlsplit(entry["url"])
        if (parsed.scheme != "https" or parsed.hostname not in ("github.com", "builds.dotnet.microsoft.com") or
                parsed.username or parsed.password or parsed.fragment):
            raise ToolchainError(f"invalid official download URL: {name}")
        if (not entry.get("license") or not entry.get("licenseUrl") or
                not 0 < entry.get("sizeBytes", 0) <= 2 * 1024 ** 3 or
                not 0 < entry.get("maxFiles", 0) <= 32768 or
                not 0 < entry.get("maxExpandedBytes", 0) <= 4 * 1024 ** 3):
            raise ToolchainError(f"invalid archive bounds or licence: {name}")
        if name != "dotnet":
            pin = pins[name]
            if PurePosixPath(parsed.path).name != pin["file"]:
                raise ToolchainError(f"archive filename differs from Rebuild pin: {name}")
            entry.update(file=pin["file"], sha256=pin["archiveSha256"])
        else:
            entry["file"] = f"dotnet-sdk-{pins['dotnet']['sdkVersion']}-linux-x64.tar.gz"
            if PurePosixPath(parsed.path).name != entry["file"]:
                raise ToolchainError("public SDK URL differs from Rebuild SDK pin")
        result[name] = entry
    return result


def verify_archive(path: Path, spec: dict[str, Any]) -> None:
    ordinary_path(path)
    if path.lstat().st_size != spec["sizeBytes"]:
        raise ToolchainError(f"archive size differs from public pin: {path.name}")
    if sha_file(path) != spec["sha256"]:
        raise ToolchainError(f"archive SHA-256 differs from public pin: {path.name}")
    if "sha512Base64" in spec:
        expected = base64.b64decode(spec["sha512Base64"], validate=True).hex()
        if len(expected) != 128 or sha_file(path, "sha512") != expected:
            raise ToolchainError(f"archive SHA-512 differs from official pin: {path.name}")


class HttpsRedirects(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, request, fp, code, message, headers, newurl):
        if urllib.parse.urlsplit(newurl).scheme != "https":
            raise ToolchainError("refusing a non-HTTPS download redirect")
        return super().redirect_request(request, fp, code, message, headers, newurl)


def download(path: Path, spec: dict[str, Any]) -> None:
    if path.exists() or path.is_symlink():
        verify_archive(path, spec)
        return
    opener = urllib.request.build_opener(HttpsRedirects())
    request = urllib.request.Request(spec["url"], headers={"User-Agent": "Onslaught-Rebuild-toolchain/1"})
    with tempfile.NamedTemporaryFile(prefix="download-", dir=path.parent, delete=False) as stream:
        temporary = Path(stream.name)
        try:
            with opener.open(request, timeout=60) as response:
                deadline = time.monotonic() + 1800
                length = response.headers.get("Content-Length")
                if length is not None and int(length) != spec["sizeBytes"]:
                    raise ToolchainError("download length differs from public pin")
                total = 0
                while block := response.read(min(CHUNK, spec["sizeBytes"] + 1 - total)):
                    if time.monotonic() > deadline:
                        raise ToolchainError("download exceeded its 30-minute deadline")
                    total += len(block)
                    if total > spec["sizeBytes"]:
                        raise ToolchainError("download exceeds pinned size")
                    stream.write(block)
            stream.flush()
            os.fsync(stream.fileno())
            verify_archive(temporary, spec)
            os.link(temporary, path, follow_symlinks=False)
        finally:
            temporary.unlink()


def member_name(name: str, prefix: str) -> str | None:
    while name.startswith("./"):
        name = name[2:]
    name = name.rstrip("/")
    if name in ("", "."):
        return None
    parts = name.split("/")
    if (name.startswith("/") or "\\" in name or ":" in name or "\0" in name or
            len(name) > 512 or len(parts) > 32 or any(part in ("", ".", "..") for part in parts)):
        raise ToolchainError(f"unsafe archive member: {name!r}")
    if prefix:
        if parts[0] != prefix:
            raise ToolchainError(f"archive member outside expected wrapper: {name!r}")
        parts = parts[1:]
    return "/".join(parts) or None


def payload_hash(records: list[tuple[str, int, bool, str]]) -> str:
    digest = hashlib.sha256()
    for name, size, executable, checksum in sorted(records):
        digest.update(f"{name}\0{size}\0{int(executable)}\0{checksum}\n".encode("utf-8"))
    if not records:
        raise ToolchainError("archive payload is empty")
    return digest.hexdigest()


class BoundedReader:
    """Bound tar metadata as well as file contents before tarfile allocates a read."""
    def __init__(self, source: BinaryIO, limit: int):
        self.source, self.remaining = source, limit

    def read(self, size: int) -> bytes:
        if size < 0 or size > self.remaining:
            raise ToolchainError("expanded tar stream exceeds bound")
        result = self.source.read(size)
        self.remaining -= len(result)
        return result


def archive_payload(archive: Path, spec: dict[str, Any], destination: Path | None = None) -> str:
    """Bound every member; optionally extract files into a newly owned empty directory."""
    if destination is not None:
        ordinary_path(destination)
        if not destination.is_dir() or any(destination.iterdir()):
            raise ToolchainError("extraction requires an ordinary empty directory")
    records: list[tuple[str, int, bool, str]] = []
    seen: dict[str, bool] = {}
    total = 0

    def consume(name: str, size: int, mode: int, directory: bool, stream: BinaryIO | None) -> None:
        nonlocal total
        relative = member_name(name, spec["sourceSubdirectory"])
        if relative is None:
            if not directory:
                raise ToolchainError("archive wrapper is not a directory")
            return
        if relative in seen:
            raise ToolchainError(f"duplicate archive member: {relative}")
        if relative == MARKER:
            raise ToolchainError("archive contains the reserved installation receipt name")
        if len(seen) >= spec["maxFiles"]:
            raise ToolchainError("archive member count exceeds bound")
        for parent in PurePosixPath(relative).parents:
            if str(parent) in seen and not seen[str(parent)]:
                raise ToolchainError("archive member descends through a file")
        if not directory and any(path.startswith(relative + "/") for path in seen):
            raise ToolchainError("archive file replaces a directory")
        seen[relative] = directory
        if directory:
            if size != 0:
                raise ToolchainError("archive directory declares file contents")
            if destination is not None:
                (destination / relative).mkdir(parents=True, exist_ok=True, mode=0o700)
            return
        if not 0 <= size <= MAX_MEMBER or total + size > spec["maxExpandedBytes"]:
            raise ToolchainError("expanded archive exceeds bound")
        total += size
        digest = hashlib.sha256()
        target = None
        if destination is not None:
            path = destination / relative
            ordinary_path(path.parent)
            path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
            target = path.open("xb")
        try:
            count = 0
            while block := stream.read(min(CHUNK, size + 1 - count)):
                count += len(block)
                if count > size:
                    raise ToolchainError("archive member exceeds declared size")
                digest.update(block)
                if target is not None:
                    target.write(block)
            if count != size:
                raise ToolchainError("truncated archive member")
        finally:
            if target is not None:
                target.close()
        executable = bool(mode & 0o111)
        if destination is not None:
            path.chmod(0o755 if executable else 0o644)
        records.append((relative, size, executable, digest.hexdigest()))

    if spec["archiveType"] == "zip":
        with zipfile.ZipFile(archive) as container:
            if len(container.infolist()) > spec["maxFiles"] + 1:
                raise ToolchainError("archive member count exceeds bound")
            for item in container.infolist():
                mode = item.external_attr >> 16
                kind = stat.S_IFMT(mode)
                if item.flag_bits & 1 or kind not in (0, stat.S_IFREG, stat.S_IFDIR) or mode & 0o7000:
                    raise ToolchainError("archive links, special or encrypted files are refused")
                with container.open(item) as stream:
                    consume(item.filename, item.file_size, mode, item.is_dir(), stream)
    elif spec["archiveType"] == "tar.gz":
        with gzip.open(archive, "rb") as expanded, tarfile.open(fileobj=BoundedReader(
                expanded, spec["maxExpandedBytes"] + spec["maxFiles"] * 10240), mode="r|") as container:
            for item in container:
                if not (item.isfile() or item.isdir()) or item.mode & 0o7000:
                    raise ToolchainError("archive links and special files are refused")
                stream = container.extractfile(item) if item.isfile() else None
                consume(item.name, item.size, item.mode, item.isdir(), stream)
                if stream is not None:
                    stream.close()
    else:
        raise ToolchainError("unsupported archive type")
    return payload_hash(records)


def installed_payload(directory: Path, spec: dict[str, Any]) -> str:
    ordinary_path(directory)
    if not directory.is_dir():
        raise ToolchainError(f"missing installed payload: {directory}")
    records = []
    total = 0
    visited = 0
    for current, directories, files in os.walk(directory, followlinks=False):
        visited += len(directories) + len(files)
        if visited > spec["maxFiles"] + 1:
            raise ToolchainError("installed member count exceeds public archive bound")
        for name in directories:
            child = Path(current) / name
            if child.is_symlink():
                raise ToolchainError(f"linked installed directory refused: {child}")
        for name in files:
            child = Path(current) / name
            relative = child.relative_to(directory).as_posix()
            if relative == MARKER:
                continue
            mode = child.lstat()
            total += mode.st_size
            if len(records) >= spec["maxFiles"] or total > spec["maxExpandedBytes"] or mode.st_size > MAX_MEMBER:
                raise ToolchainError("installed payload exceeds public archive bounds")
            records.append((relative, mode.st_size, bool(mode.st_mode & 0o111), sha_file(child)))
    return payload_hash(records)


def rename_new(source: Path, destination: Path) -> None:
    library = ctypes.CDLL(None, use_errno=True)
    rename = library.renameat2
    rename.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int, ctypes.c_char_p, ctypes.c_uint]
    rename.restype = ctypes.c_int
    if rename(-100, os.fsencode(source), -100, os.fsencode(destination), 1) != 0:
        error = ctypes.get_errno()
        raise OSError(error, os.strerror(error), str(destination))


def verify_component(root: Path, name: str, spec: dict[str, Any], pins: dict[str, Any]) -> Path:
    ordinary_path(root / name)
    archive = root / "downloads" / spec["file"]
    verify_archive(archive, spec)
    directory = root / name
    receipt = read_json(directory / MARKER)
    expected = pins["engine"]["payloadSha256"] if name == "engine" else spec.get("payloadSha256")
    if expected is None:
        expected = archive_payload(archive, spec)
    if receipt != {"schemaVersion": 1, "archiveSha256": spec["sha256"], "payloadSha256": expected}:
        raise ToolchainError(f"installed {name} receipt differs from verified public archive")
    actual = installed_payload(directory, spec)
    if actual != expected:
        raise ToolchainError(f"installed {name} differs from verified public archive; refusing replacement")
    if name == "engine":
        pin = pins["engine"]
        if (actual != pin["payloadSha256"] or sha_file(directory / pin["executable"]) != pin["executableSha256"] or
                sha_file(directory / pin["sdkPackage"]) != pin["sdkPackageSha256"]):
            raise ToolchainError("installed engine differs from Rebuild payload/executable/SDK pins")
    elif name == "templates":
        for filename, expected_hash in pins["templates"]["fileSha256"].items():
            if sha_file(directory / filename) != expected_hash:
                raise ToolchainError(f"template differs from Rebuild pin: {filename}")
        if (directory / "version.txt").read_text(encoding="utf-8").strip() != pins["templateVersion"]:
            raise ToolchainError("template version differs from Rebuild pin")
    else:
        for relative in ("dotnet", f"sdk/{pins['dotnet']['sdkVersion']}/dotnet.dll",
                         f"shared/Microsoft.NETCore.App/{pins['dotnet']['runtimeVersion']}/Microsoft.NETCore.App.deps.json"):
            if not (directory / relative).is_file():
                raise ToolchainError(f"pinned SDK/runtime file is absent: {relative}")
    return directory


def verified_install(root: Path, pins: dict[str, Any], platforms: list[str]) -> tuple[Path, Path | None, Path]:
    """Return engine, optional templates and dotnet after read-only source verification."""
    check_host()
    if any(value not in ("linux", "windows") for value in platforms):
        raise ToolchainError("unknown export platform")
    root = checked_root(root, create=False)
    entries = specs(pins)
    engine = verify_component(root, "engine", entries["engine"], pins)
    dotnet = verify_component(root, "dotnet", entries["dotnet"], pins)
    templates = verify_component(root, "templates", entries["templates"], pins) if platforms else None
    return engine / pins["engine"]["executable"], templates, dotnet / "dotnet"


def setup(root: Path, *, with_templates: bool = False) -> tuple[Path, Path | None, Path]:
    check_host()
    root = checked_root(root, create=True)
    pins = read_json(PINS)
    entries = specs(pins)
    lock = os.open(root / ".setup.lock", os.O_WRONLY | os.O_CREAT | os.O_NOFOLLOW | os.O_NONBLOCK, 0o600)
    with os.fdopen(lock, "w") as held:
        identity = os.fstat(held.fileno())
        if not stat.S_ISREG(identity.st_mode) or identity.st_nlink != 1:
            raise ToolchainError("setup lock is not an ordinary single-link file")
        fcntl.flock(held.fileno(), fcntl.LOCK_EX)
        selected = ("engine", "dotnet", *(("templates",) if with_templates else ()))
        for name in selected:
            if (root / name).exists() or (root / name).is_symlink():
                verify_component(root, name, entries[name], pins)
        for name in selected:
            spec = entries[name]
            directory = root / name
            if directory.exists() or directory.is_symlink():
                continue
            downloads = root / "downloads"
            ordinary_path(downloads)
            downloads.mkdir(exist_ok=True, mode=0o700)
            archive = downloads / spec["file"]
            print(f"Preparing {name}: {spec['url']} ({spec['sizeBytes']} bytes; {spec['license']})", flush=True)
            download(archive, spec)
            with tempfile.TemporaryDirectory(prefix=f"stage-{name}-", dir=root) as temporary:
                payload = Path(temporary) / "payload"
                payload.mkdir(mode=0o700)
                expected = archive_payload(archive, spec, payload)
                if name == "engine" and expected != pins["engine"]["payloadSha256"]:
                    raise ToolchainError("official engine extraction differs from Rebuild payload pin")
                if "payloadSha256" in spec and expected != spec["payloadSha256"]:
                    raise ToolchainError(f"official {name} extraction differs from public payload pin")
                receipt = {"schemaVersion": 1, "archiveSha256": sha_file(archive), "payloadSha256": expected}
                (payload / MARKER).write_text(json.dumps(receipt, sort_keys=True) + "\n", encoding="utf-8")
                ordinary_path(root)
                rename_new(payload, directory)
            verify_component(root, name, spec, pins)
    return verified_install(root, pins, ["linux", "windows"] if with_templates else [])


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("setup", "verify"))
    parser.add_argument("--root", type=Path, help="ignored local-data root; default: local-data/toolchains/rebuild")
    parser.add_argument("--with-templates", action="store_true", help="include optional Linux/Windows export templates")
    arguments = parser.parse_args(argv)
    try:
        root = arguments.root or default_root()
        result = (setup(root, with_templates=arguments.with_templates) if arguments.mode == "setup" else
                  verified_install(root, read_json(PINS), ["linux", "windows"] if arguments.with_templates else []))
        print(json.dumps(dict(zip(("engine", "templates", "dotnet"), (str(path) if path else None for path in result))), indent=2))
        return 0
    except (ToolchainError, OSError, ValueError, KeyError, tarfile.TarError, zipfile.BadZipFile) as error:
        print(f"Rebuild toolchain: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
