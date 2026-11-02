#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Build the live Host or run a bounded authored-test selection with public tools.

Setup is explicit; build/test never install a toolchain or use the retail lab.
The full reference-bearing Parity suite remains a separate maintainer gate.
"""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path, PurePosixPath
import shlex
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile

from godot_host import print_process_output, run_process


ROOT = Path(__file__).resolve().parents[1]
HOST = Path("rebuild/OnslaughtRebuild.Host/OnslaughtRebuild.Host.csproj")
TESTS = Path("rebuild/OnslaughtRebuild.Parity.Tests/OnslaughtRebuild.Parity.Tests.csproj")
DEFAULT_FILTER = "|".join(
    f"FullyQualifiedName~OnslaughtRebuild.Parity.Tests.{name}"
    for name in ("CrtRandTests", "LiveAudioOutputStartupTests", "Flow.SaveGameTests")
)
REFERENCE_VARIABLES = (
    "ONSLAUGHT_GAME_DIR", "ONSLAUGHT_PORT_REFS", "ONSLAUGHT_PORT_CRT", "ONSLAUGHT_PORT_LOGS",
    "ONSLAUGHT_RETAIL_ORACLE", "ONSLAUGHT_RETAIL_GOLDEN", "ONSLAUGHT_RETAIL_MCLK2",
    "ONSLAUGHT_GOLDEN_VDS", "ONSLAUGHT_BEOBJ_VDS", "ONSLAUGHT_PORT_OCCUPANCY",
    "ONSLAUGHT_PARITY_DATA", "X87_ORACLE_OUT",
)
BUILD_FLAGS = ("-m:1", "-nodeReuse:false", "-p:UseSharedCompilation=false", "-p:BuildInParallel=false")


def child_environment(root: Path, run: Path, dotnet: Path, sdk_directory: Path,
                      inherited: dict[str, str] | None = None) -> dict[str, str]:
    """Keep host/network essentials; use checkout-local .NET/NuGet and XDG state."""
    inherited = os.environ if inherited is None else inherited
    allowed = ("PATH", "HOME", "LANG", "LC_ALL", "LC_CTYPE", "TERM", "SSL_CERT_FILE", "SSL_CERT_DIR",
               "http_proxy", "https_proxy", "no_proxy", "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY")
    env = {key: inherited[key] for key in allowed if key in inherited}
    cache = root / "local-data/public-build/cache"
    paths = {
        "DOTNET_CLI_HOME": cache / "dotnet-home",
        "NUGET_PACKAGES": cache / "packages",
        "NUGET_HTTP_CACHE_PATH": cache / "http",
        "NUGET_PLUGINS_CACHE_PATH": cache / "plugins",
        "TMPDIR": run / "tmp", "TMP": run / "tmp", "TEMP": run / "tmp",
        "XDG_DATA_HOME": run / "user-data", "XDG_CONFIG_HOME": run / "config",
        "XDG_CACHE_HOME": cache / "xdg",
    }
    for key, path in paths.items():
        path.mkdir(parents=True, exist_ok=True)
        env[key] = str(path)
    env.update(
        PATH=str(dotnet.parent) + os.pathsep + env.get("PATH", os.defpath),
        DOTNET_ROOT=str(dotnet.parent), DOTNET_ROOT_X64=str(dotnet.parent), DOTNET_HOST_PATH=str(dotnet),
        DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", DOTNET_MULTILEVEL_LOOKUP="0",
        DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true", DOTNET_PROCESSOR_COUNT="4",
        MSBUILDDISABLENODEREUSE="1", MSBUILDDISABLENUGETSDKRESOLVER="1",
        MSBuildSDKsPath=str(sdk_directory),
    )
    # Nonempty absent overrides prevent reference discovery from walking out of a
    # linked worktree into a maintainer's canonical local-data or Steam install.
    for variable in REFERENCE_VARIABLES:
        env[variable] = str(run / "unavailable-reference-inputs")
    return env


def _ordinary_path(path: Path) -> None:
    if any(ancestor.is_symlink() for ancestor in (path, *path.parents)):
        raise ValueError(f"linked SDK cache path refused: {path}")


def _link_directory(link: Path, target: Path) -> None:
    _ordinary_path(link.parent)
    if link.is_symlink() and link.resolve() == target.resolve():
        return
    if link.exists() or link.is_symlink():
        raise ValueError(f"SDK directory already has a different owner: {link}")
    link.symlink_to(target, target_is_directory=True)


def sdk_directory(root: Path, dotnet: Path, engine: Path, sdk_version: str, godot_version: str) -> Path:
    """Expose the pinned bundled Godot SDK without NuGet's ambient SDK resolver."""
    standard = dotnet.parent / "sdk" / sdk_version / "Sdks"
    package = engine.parent / "GodotSharp/Tools/nupkgs" / f"Godot.NET.Sdk.{godot_version}.nupkg"
    if not standard.is_dir() or not package.is_file():
        raise ValueError("the installed .NET/Godot bundle is missing its pinned SDK files; rerun setup")
    cache = root / "local-data/public-build/cache"
    extracted = cache / "godot-sdk" / godot_version
    _ordinary_path(extracted)
    extracted.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(package) as archive:
        if sum(entry.file_size for entry in archive.infolist()) > 16 * 1024 * 1024:
            raise ValueError("unexpectedly large bundled Godot SDK package")
        for entry in archive.infolist():
            relative = PurePosixPath(entry.filename)
            if relative.is_absolute() or ".." in relative.parts or "\\" in entry.filename:
                raise ValueError("invalid bundled Godot SDK package path")
            destination = extracted.joinpath(*relative.parts)
            _ordinary_path(destination)
            if entry.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
                continue
            content = archive.read(entry)
            if destination.exists():
                if destination.is_symlink() or destination.read_bytes() != content:
                    raise ValueError(f"extracted Godot SDK changed: {destination}")
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(content)
    if not (extracted / "Sdk/Sdk.props").is_file():
        raise ValueError("the bundled Godot SDK has no Sdk/Sdk.props")
    combined = cache / "sdks" / f"{sdk_version}-{godot_version}"
    _ordinary_path(combined)
    combined.mkdir(parents=True, exist_ok=True)
    for item in standard.iterdir():
        if item.is_dir():
            _link_directory(combined / item.name, item)
    _link_directory(combined / "Godot.NET.Sdk", extracted)
    return combined


def write_nuget_config(path: Path, engine: Path) -> None:
    """Only the supplied Godot bundle and public NuGet feed serve packages."""
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="GodotBundled", value=str(engine.parent / "GodotSharp/Tools/nupkgs"))
    ET.SubElement(sources, "add", key="nuget.org", value="https://api.nuget.org/v3/index.json", protocolVersion="3")
    fallback = ET.SubElement(config, "fallbackPackageFolders")
    ET.SubElement(fallback, "clear")
    ET.indent(config)
    ET.ElementTree(config).write(path, encoding="utf-8", xml_declaration=True)


def check_authored_results(path: Path) -> int:
    """A successful process with no selected/passed tests is not a passing check."""
    document = ET.parse(path)
    counters = document.find(".//{*}Counters")
    results = document.findall(".//{*}UnitTestResult")
    if counters is None:
        raise ValueError("authored test report has no counters")
    total = int(counters.get("total", "0"))
    nonpassing = ("failed", "error", "timeout", "aborted", "inconclusive", "notRunnable", "notExecuted", "disconnected")
    if (total == 0 or int(counters.get("executed", "0")) != total
            or int(counters.get("passed", "0")) != total or len(results) != total
            or any(int(counters.get(name, "0")) for name in nonpassing)
            or any(result.get("outcome") != "Passed" for result in results)):
        raise ValueError("authored selection must execute and pass every selected test; empty selections and skips fail")
    return total


def command(arguments: list[str], *, root: Path, env: dict[str, str], run: Path, name: str,
            timeout: int = 1200) -> subprocess.CompletedProcess[str]:
    print("+ " + shlex.join(arguments), flush=True)
    try:
        completed = run_process(arguments, cwd=root, env=env, timeout=timeout, capture=True)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        output = error.output or ""
        if isinstance(output, bytes):
            output = output.decode("utf-8", errors="replace")
        (run / f"{name}.log").write_text(output, encoding="utf-8")
        raise
    (run / f"{name}.log").write_text(completed.stdout or "", encoding="utf-8")
    print(completed.stdout or "", end="", flush=True)
    return completed


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("setup", "build", "test"))
    parser.add_argument("--filter", default=DEFAULT_FILTER, help="authored tests to run (test mode only)")
    args = parser.parse_args(argv)
    if not args.filter.strip():
        parser.error("--filter must select authored tests")
    # The self-contained installer is copied into this repository; it never
    # imports a sibling project or the maintainer's machine toolchain.
    import rebuild_toolchain as toolchain
    pins = json.loads((ROOT / "tools/rebuild_toolchain.json").read_text(encoding="utf-8"))
    if args.mode == "setup":
        return toolchain.main(["setup", "--root", str(ROOT / "local-data/toolchains/rebuild")])
    engine, _, dotnet = toolchain.verified_install(ROOT / "local-data/toolchains/rebuild", pins, [])
    sdk_version = json.loads((ROOT / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]
    godot_version = ET.parse(ROOT / HOST).getroot().get("Sdk", "").removeprefix("Godot.NET.Sdk/")
    if sdk_version != pins["dotnet"]["sdkVersion"] or f"Godot.NET.Sdk.{godot_version}.nupkg" != Path(pins["engine"]["sdkPackage"]).name:
        raise ValueError("public toolchain pins differ from global.json or the Host project")
    sdks = sdk_directory(ROOT, dotnet, engine, sdk_version, godot_version)
    runs = ROOT / "local-data/public-build/runs"
    runs.mkdir(parents=True, exist_ok=True)
    run = Path(tempfile.mkdtemp(prefix=f"{args.mode}-", dir=runs))
    print(f"Public {args.mode} logs: {run}", flush=True)
    env = child_environment(ROOT, run, dotnet, sdks)
    config = run / "NuGet.Config"
    write_nuget_config(config, engine)
    version = command([str(dotnet), "--version"], root=ROOT, env=env, run=run, name="sdk-version").stdout.strip()
    if version != sdk_version:
        raise ValueError(f"expected .NET SDK {sdk_version}, got {version}")
    project = ROOT / (HOST if args.mode == "build" else TESTS)
    command([str(dotnet), "restore", str(project), "--configfile", str(config), "--disable-parallel",
             "--nologo", "-p:NuGetAudit=false", *BUILD_FLAGS], root=ROOT, env=env, run=run, name="restore")
    if args.mode == "build":
        command([str(dotnet), "build", str(project), "--no-restore", "--nologo", "--disable-build-servers",
                 *BUILD_FLAGS], root=ROOT, env=env, run=run, name="build")
        print("PUBLIC_BUILD_PASS: live Host and its Parity dependency compiled")
    else:
        print(f"Authored selection: {args.filter}", flush=True)
        command([str(dotnet), "test", str(project), "--no-restore", "--nologo", "--disable-build-servers",
                 "--filter", args.filter, "--logger", "trx;LogFileName=authored.trx",
                 "--results-directory", str(run), *BUILD_FLAGS], root=ROOT, env=env, run=run, name="test")
        count = check_authored_results(run / "authored.trx")
        print(f"AUTHORED_TEST_PASS: {count} selected tests passed; reference comparisons were not run")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, RuntimeError, ET.ParseError, zipfile.BadZipFile) as error:
        print(f"public build: {error}", file=sys.stderr)
        raise SystemExit(1)
    except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print_process_output(error)
        raise SystemExit(1)
