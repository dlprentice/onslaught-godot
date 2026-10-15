#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Shared installed-Godot engine discovery and owned-process support.

The companion launches through companion_godot.py and the rebuild through
rebuild/tools/first_flight.py. This module contains no rebuild, retail-data or
simulation dependencies.
"""
from __future__ import annotations

import os
import shutil
import signal
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ENGINE_VERSION = "4.8.dev6.mono.official.8898c2b3d"
ENGINE_SDK_VERSION = "4.8.0-dev.6"
DEFAULT_ENGINE = "godot48-mono"
ROOT = Path(__file__).resolve().parents[1]


def _stop_owned_group(process: subprocess.Popen[str]) -> None:
    """Stop descendants too, including children left after the leader exits."""
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        process.wait()
        return
    deadline = time.monotonic() + 0.5
    while time.monotonic() < deadline:
        process.poll()
        try:
            os.killpg(process.pid, 0)
        except ProcessLookupError:
            break
        time.sleep(0.01)
    else:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
    process.wait()


def run_process(
    command: list[str],
    *,
    cwd: Path,
    env: dict[str, str],
    timeout: float | None,
    capture: bool = False,
) -> subprocess.CompletedProcess[str]:
    """Run one owned Linux process group; preserve output and nonzero exits."""
    process = subprocess.Popen(
        command,
        cwd=cwd,
        env=env,
        start_new_session=True,
        text=True,
        stdout=subprocess.PIPE if capture else None,
        stderr=subprocess.PIPE if capture else None,
    )
    timed_out: subprocess.TimeoutExpired | None = None
    try:
        stdout, stderr = process.communicate(timeout=timeout)
        result = subprocess.CompletedProcess(command, process.returncode, stdout, stderr)
        result.check_returncode()
        return result
    except subprocess.TimeoutExpired as error:
        timed_out = error
        raise
    finally:
        try:
            # Also clean up an engine/build tool which exits but leaves workers alive.
            _stop_owned_group(process)
            if timed_out is not None and capture:
                try:
                    stdout, stderr = process.communicate(timeout=1.0)
                except subprocess.TimeoutExpired as drain_error:
                    # An escaped descendant may still hold a pipe. Keep the
                    # latest partial output without turning this into a hang.
                    if drain_error.output is not None:
                        timed_out.output = drain_error.output
                    if drain_error.stderr is not None:
                        timed_out.stderr = drain_error.stderr
                    timed_out.add_note("Owned process group stopped; output drain exceeded one second.")
                except UnicodeError:
                    # Preserve the original byte diagnostics and timeout even
                    # when a tool's final output is not valid text.
                    timed_out.add_note("Owned process group stopped; final output could not be decoded.")
                else:
                    # communicate() normalizes text; TimeoutExpired diagnostics
                    # remain bytes, as in subprocess's public exception contract.
                    encoding = process.encoding or "utf-8"
                    timed_out.output = stdout.encode(encoding) if stdout is not None else None
                    timed_out.stderr = stderr.encode(encoding) if stderr is not None else None
        finally:
            for stream in (process.stdout, process.stderr):
                if stream is not None:
                    stream.close()


def print_process_output(error: subprocess.CalledProcessError | subprocess.TimeoutExpired) -> None:
    """Print captured diagnostics without byte-literal reprs or lost partial lines."""
    for output in (error.output, error.stderr):
        if output:
            text = output.decode("utf-8", errors="replace") if isinstance(output, bytes) else output
            print(text, file=sys.stderr, end="" if text.endswith("\n") else "\n")
    for note in getattr(error, "__notes__", ()):
        print(note, file=sys.stderr)


def engine_path(requested: str, *, sdk_version: str = ENGINE_SDK_VERSION) -> Path:
    located = shutil.which(requested)
    if located is None:
        raise RuntimeError(f"Godot Mono executable not found: {requested}")
    engine = Path(located).resolve(strict=True)
    packages = engine.parent / "GodotSharp/Tools/nupkgs"
    if not (packages / f"Godot.NET.Sdk.{sdk_version}.nupkg").is_file():
        raise RuntimeError(f"Godot {sdk_version} bundled SDK package is missing: {packages}")
    return engine


def output_directory(checkout: Path, requested: Path | None, owner: str, mode: str) -> Path:
    """Allocate a fresh invocation below this checkout, never a shared profile."""
    local_data = (checkout / "local-data").resolve()
    if requested is None:
        parent = local_data / owner
        parent.mkdir(parents=True, exist_ok=True)
        return Path(tempfile.mkdtemp(prefix=f"{mode}-", dir=parent))
    output = requested.expanduser().absolute()
    if output.resolve() == local_data or not output.resolve().is_relative_to(local_data):
        raise RuntimeError("--output-root must be below this checkout's local-data")
    output.mkdir(parents=True, exist_ok=False)
    return output


def build_project(project: Path, engine: Path, env: dict[str, str]) -> None:
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        raise RuntimeError("dotnet is not installed or not on PATH")
    projects = list(project.glob("*.csproj"))
    if len(projects) != 1:
        raise RuntimeError(f"Expected one Godot C# project in {project}")
    packages = str(engine.parent / "GodotSharp/Tools/nupkgs")
    run_process([dotnet, "restore", str(projects[0]), "--source", packages,
                 "--locked-mode", "--nologo", "-p:NuGetAudit=false"],
                cwd=project, env=env, timeout=1200)
    run_process([dotnet, "build", str(projects[0]), "--no-restore", "--nologo"],
                cwd=project, env=env, timeout=1200)
