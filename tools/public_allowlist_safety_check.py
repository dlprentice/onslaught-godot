#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Hard-payload safety check for the public-primary repo.

The public repository is now the working repository. This check rejects exact
local retail-materialization owners, copied game/runtime payload roots, build
outputs, and obvious secret files. File extensions alone do not establish
provenance, so ordinary project-authored or separately provided asset formats
remain reviewable. This is intentionally not a portable-app ZIP manifest; the
WinUI package has its own stricter payload boundary.
"""

from __future__ import annotations

import argparse
import hashlib
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

sys.dont_write_bytecode = True


ROOT = Path(__file__).resolve().parents[1]
SELF_REL = "tools/public_allowlist_safety_check.py"

DENY_ROOTS = (
    ".vs/",
    "GameProfiles/",
    "Ghidra/",
    "PatchBench/",
    "reverse-engineering/ghidra/",
    "game/",
    "ghidra-local/",
    "local-game/",
    "local-ghidra/",
    "local-lab/",
    "local-media/",
    "local-rom-input/",
    "local-proofs/",
    "local-saves/",
    "mcps/",
    "media/",
    "save-attempts/",
)

DENY_CONTAINS = (
    "/bin/",
    "/obj/",
    "/TestResults/",
    "/__pycache__/",
    ".rep/",
    "/.rep/",
    "/secrets/",
    "/credentials/",
    "/.codex/auth/",
    "/.codex/cache/",
    "/.codex/logs/",
    "/.codex/sessions/",
    "/.codex/tmp/",
)

DENY_EXACT = {
}

ALLOW_EXACT_SHA256 = {
    "references/AYAResourceExtractor/BoxWithTextures.fbx": "37526ffde1d48016fa8a2a05c5dfeb3cd0a30a8ab402ccce60a7f44addf8eed2",
    "tests_shared/fixtures/gold_career_save.bin": "0c17e47db9d666e9b26ef88d43d0a25e7cbfbf4f88c8005cc748965050e506fb",
}

ALLOW_EXACT = set(ALLOW_EXACT_SHA256)

RETAIL_MATERIALIZED_ROOTS = (
    "rebuild/OnslaughtRebuild.Core/Assets/Level100/",
    "rebuild/OnslaughtRebuild.Godot/Assets/Aquila/",
    "rebuild/OnslaughtRebuild.Godot/Assets/Hud/",
    "rebuild/OnslaughtRebuild.Godot/Assets/Level100/",
)
RETAIL_MATERIALIZED_METADATA = {
    "rebuild/OnslaughtRebuild.Godot/Assets/Aquila/README.md",
    "rebuild/OnslaughtRebuild.Godot/Assets/Hud/README.md",
    "rebuild/OnslaughtRebuild.Godot/Assets/Level100/README.md",
}

ALLOW_CDB_SCRIPT_PREFIXES: tuple[str, ...] = ()

DENY_OPERATIONAL_SUFFIXES = (
    ".7z",
    ".appx",
    ".appxbundle",
    ".bak",
    ".bea",
    ".bes",
    ".bik",
    ".cab",
    ".cue",
    ".crt",
    ".db",
    ".dll",
    ".dmp",
    ".etl",
    ".exe",
    ".gbf",
    ".gdt",
    ".gpr",
    ".gz",
    ".gzf",
    ".img",
    ".iso",
    ".key",
    ".log",
    ".mso",
    ".msi",
    ".msix",
    ".msixbundle",
    ".pem",
    ".pfx",
    ".pdb",
    ".pyo",
    ".pyc",
    ".raw",
    ".rar",
    ".sav",
    ".sqlite",
    ".tar",
    ".trx",
    ".vid",
    ".zip",
)

MAX_UNREVIEWED_FILE_BYTES = 5 * 1024 * 1024
MAGIC_SCAN_BYTES = 4096

TEXT_SUFFIXES = {
    ".cmd",
    ".cs",
    ".css",
    ".html",
    ".java",
    ".json",
    ".jsonl",
    ".jsonc",
    ".md",
    ".ps1",
    ".py",
    ".sh",
    ".ts",
    ".tsx",
    ".tsv",
    ".txt",
    ".xml",
    ".xaml",
    ".yml",
    ".yaml",
}

TEXT_DENY_PATTERNS = (
    ("deny-private-key-block", re.compile(r"-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----")),
    ("deny-openai-key", re.compile(r"\bsk-[A-Za-z0-9_-]{20,}\b")),
    ("deny-github-token", re.compile(r"\b(?:ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9_]{20,}\b")),
    ("deny-github-fine-grained-token", re.compile(r"\bgithub_pat_[A-Za-z0-9_]{30,}\b")),
    ("deny-aws-access-key", re.compile(r"\bA(?:KIA|SIA)[A-Z0-9]{16}\b")),
    ("deny-stripe-key", re.compile(r"\b(?:sk|rk)_(?:live|test)_[A-Za-z0-9]{20,}\b")),
    ("deny-cloudflare-token", re.compile(r"\b(?:CF_API_TOKEN|CLOUDFLARE_API_TOKEN)\s*[:=]\s*[A-Za-z0-9_-]{20,}\b")),
    ("deny-huggingface-token", re.compile(r"\bhf_[A-Za-z0-9]{20,}\b")),
    ("deny-sentry-dsn", re.compile(r"https://[A-Fa-f0-9]{16,}@[A-Za-z0-9.-]+/\d+")),
    ("deny-npm-token", re.compile(r"\bnpm_[A-Za-z0-9]{20,}\b")),
    ("deny-supabase-jwt", re.compile(r"\beyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b")),
    ("deny-discord-token", re.compile(r"\b(?:mfa\.)?[A-Za-z0-9_-]{24}\.[A-Za-z0-9_-]{6}\.[A-Za-z0-9_-]{27,}\b")),
)

TEXT_ALLOW_EXACT = {
    "tools/public_allowlist_safety_check.py",
}

PAYLOAD_TEXT_ALLOW_EXACT: set[str] = set()

# Exact reviewed text, not an extension/root exemption: a path listed here may carry
# long Base64 only while its whole content still has the listed sha256; secret
# checks still run. The Ghidra cohort manifests that needed it moved to the private
# RE archive on 2026-10-15, so the set is empty.
REVIEWED_ENCODED_COMMENTS_SHA256: dict[str, str] = {}

CDB_PROMPT_RE = re.compile(r"(?m)^\s*\d+:\d+>\s+")
REGISTER_DUMP_RE = re.compile(
    r"(?im)\b(?:eax|ebx|ecx|edx|esi|edi|eip|esp|rax|rbx|rcx|rdx|rsi|rdi|rip|rsp)=[0-9a-f`]{4,}\b"
)
STACK_TRACE_RE = re.compile(r"(?im)^\s*(?:ChildEBP|Child-SP|RetAddr)\s+")
DATA_IMAGE_RE = re.compile(r"data:image/(?:png|jpeg|jpg|gif|webp|bmp);base64,", re.IGNORECASE)
EMBEDDED_PNG_RE = re.compile(r"(?:\\x89PNG|iVBORw0KGgo)", re.IGNORECASE)
EMBEDDED_JPEG_RE = re.compile(r"(?:\\xff\\xd8\\xff|/9j/4AAQSkZJRgABAQ)", re.IGNORECASE)
BASE64_TOKEN_RE = re.compile(r"(?<![A-Za-z0-9+/=])[A-Za-z0-9+/=]{512,}(?![A-Za-z0-9+/=])")

MAGIC_DENY_SIGNATURES = (
    ("deny-magic-executable", b"MZ"),
    ("deny-magic-zip-archive", b"PK\x03\x04"),
    ("deny-magic-7z-archive", b"7z\xbc\xaf\x27\x1c"),
    ("deny-magic-rar-archive", b"Rar!\x1a\x07"),
    ("deny-magic-msi-ole-package", b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1"),
    ("deny-magic-cab-archive", b"MSCF"),
    ("deny-magic-pdb-symbols", b"Microsoft C/C++"),
    ("deny-magic-bink-video", b"BIK"),
    ("deny-magic-sqlite-db", b"SQLite format 3\x00"),
)

@dataclass(frozen=True)
class Finding:
    path: str
    label: str
    detail: str


def normalize(path: str) -> str:
    return path.replace("\\", "/")


def public_candidate_files(root: Path, *, include_submodules: bool = False) -> list[str]:
    try:
        result = subprocess.run(
            ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
    except FileNotFoundError as exc:
        raise RuntimeError("git executable was not found for public candidate enumeration") from exc
    except subprocess.CalledProcessError as exc:
        stderr = exc.stderr.decode("utf-8", errors="replace") if isinstance(exc.stderr, bytes) else str(exc.stderr)
        raise RuntimeError(f"git ls-files failed for public candidate enumeration: {stderr.strip()}") from exc
    paths = [
        path
        for item in result.stdout.decode("utf-8", errors="replace").split("\0")
        if (path := normalize(item)) and (root / path).exists()
    ]
    if include_submodules:
        paths.extend(submodule_candidate_files(root))
    return sorted(set(paths))


def payload_root_files(root: Path) -> list[str]:
    if not root.is_dir():
        raise RuntimeError(f"payload root is not a directory: {root}")

    paths: list[str] = []
    for path in root.rglob("*"):
        if not path.is_file():
            continue
        try:
            relative = path.relative_to(root).as_posix()
        except ValueError as exc:
            raise RuntimeError(f"payload root enumeration escaped root: {path}") from exc
        if relative.startswith(".git/"):
            continue
        paths.append(normalize(relative))
    return sorted(set(paths))


def submodule_paths(root: Path) -> list[str]:
    gitmodules = root / ".gitmodules"
    if not gitmodules.is_file():
        return []
    try:
        result = subprocess.run(
            ["git", "config", "--file", str(gitmodules), "--get-regexp", r"^submodule\..*\.path$"],
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
    except FileNotFoundError as exc:
        raise RuntimeError("git executable was not found for .gitmodules parsing") from exc
    except subprocess.CalledProcessError as exc:
        raise RuntimeError(f".gitmodules could not be parsed: {exc.stderr.strip()}") from exc
    paths: list[str] = []
    for line in result.stdout.splitlines():
        parts = line.split(maxsplit=1)
        if len(parts) == 2:
            paths.append(normalize(parts[1].strip()))
    return sorted(paths)


def submodule_candidate_files(root: Path) -> list[str]:
    paths: list[str] = []
    for submodule_path in submodule_paths(root):
        full_path = root / submodule_path
        if not full_path.is_dir():
            continue
        try:
            result = subprocess.run(
                ["git", "ls-files", "-z"],
                cwd=full_path,
                check=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
        except FileNotFoundError as exc:
            raise RuntimeError(f"git executable was not found while scanning submodule {submodule_path}") from exc
        except subprocess.CalledProcessError as exc:
            stderr = exc.stderr.decode("utf-8", errors="replace") if isinstance(exc.stderr, bytes) else str(exc.stderr)
            raise RuntimeError(f"submodule {submodule_path} could not be scanned: {stderr.strip()}") from exc
        for item in result.stdout.decode("utf-8", errors="replace").split("\0"):
            if not item:
                continue
            paths.append(f"{submodule_path}/{normalize(item)}")
    return sorted(paths)


def submodule_scan_findings(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    try:
        paths = submodule_paths(root)
    except RuntimeError as exc:
        return [Finding(".gitmodules", "deny-unreadable-submodule-map", str(exc))]
    for submodule_path in paths:
        full_path = root / submodule_path
        if not full_path.is_dir():
            findings.append(Finding(submodule_path, "deny-missing-submodule-scan", "declared submodule directory is absent"))
            continue
        try:
            subprocess.run(
                ["git", "ls-files", "-z"],
                cwd=full_path,
                check=True,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
            )
        except (FileNotFoundError, subprocess.CalledProcessError) as exc:
            findings.append(Finding(submodule_path, "deny-unreadable-submodule-scan", str(exc)))
    return findings


def is_text_file(path: str) -> bool:
    return Path(path).suffix.lower() in TEXT_SUFFIXES


def is_text_candidate(path: str) -> bool:
    """Compatibility helper used by release accounting scripts."""
    return is_text_file(path)


def path_findings(path: str) -> list[Finding]:
    if path in ALLOW_EXACT:
        return []
    findings: list[Finding] = []
    lower = path.lower()
    name = Path(path).name.lower()
    if lower.startswith(".codex/"):
        findings.append(Finding(path, "deny-codex-runtime-subtree", path))
    if path in DENY_EXACT:
        findings.append(Finding(path, "deny-exact", path))
    if name == ".env" or name.startswith(".env."):
        findings.append(Finding(path, "deny-env-file", name))
    if lower.startswith(tuple(prefix.lower() for prefix in DENY_ROOTS)):
        findings.append(Finding(path, "deny-root", path.split("/", 1)[0]))
    if (
        lower.startswith(tuple(prefix.lower() for prefix in RETAIL_MATERIALIZED_ROOTS))
        and lower not in {item.lower() for item in RETAIL_MATERIALIZED_METADATA}
    ):
        findings.append(Finding(path, "deny-materialized-retail-output", path))
    if any(token.lower() in lower for token in DENY_CONTAINS):
        findings.append(Finding(path, "deny-generated-or-private-path", path))
    if lower.endswith(".cdb.txt") and not any(lower.startswith(prefix.lower()) for prefix in ALLOW_CDB_SCRIPT_PREFIXES):
        findings.append(Finding(path, "deny-raw-cdb-text-transcript", ".cdb.txt"))
    if lower.endswith(".txt") and "cdb" in name and "log" in name:
        findings.append(Finding(path, "deny-raw-cdb-text-transcript", name))
    if lower.endswith(DENY_OPERATIONAL_SUFFIXES):
        findings.append(Finding(path, "deny-operational-payload-suffix", Path(path).suffix.lower()))
    return findings


def size_findings(root: Path, path: str) -> list[Finding]:
    full_path = root / path
    if full_path.is_dir():
        return []
    try:
        size = full_path.stat().st_size
    except OSError as exc:
        return [Finding(path, "stat-error", str(exc))]
    if path in ALLOW_EXACT_SHA256:
        return []
    if size > MAX_UNREVIEWED_FILE_BYTES:
        return [Finding(path, "deny-large-unreviewed-file", str(size))]
    return []


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def exact_allow_hash_findings(root: Path, path: str) -> list[Finding]:
    expected = ALLOW_EXACT_SHA256.get(path)
    if expected is None:
        return []
    full_path = root / path
    try:
        actual = sha256_file(full_path)
    except OSError as exc:
        return [Finding(path, "exact-allow-hash-read-error", str(exc))]
    if actual.lower() != expected.lower():
        return [Finding(path, "exact-allow-hash-mismatch", actual)]
    return []


def magic_findings(root: Path, path: str) -> list[Finding]:
    if path in ALLOW_EXACT_SHA256 or is_text_file(path):
        return []
    full_path = root / path
    if full_path.is_dir():
        return []
    try:
        prefix = full_path.read_bytes()[:MAGIC_SCAN_BYTES]
    except OSError as exc:
        return [Finding(path, "read-error", str(exc))]
    findings: list[Finding] = []
    for label, signature in MAGIC_DENY_SIGNATURES:
        offset = prefix.find(signature)
        if offset >= 0:
            findings.append(Finding(path, label, f"offset={offset} signature={signature.hex()}"))
            break
    return findings


def text_binary_findings(root: Path, path: str) -> list[Finding]:
    if path == SELF_REL or path in TEXT_ALLOW_EXACT or not is_text_file(path):
        return []
    full_path = root / path
    try:
        prefix = full_path.read_bytes()[:MAGIC_SCAN_BYTES]
    except OSError as exc:
        return [Finding(path, "read-error", str(exc))]
    if b"\x00" in prefix:
        return [Finding(path, "deny-nul-byte-in-text-file", "NUL byte")]
    control_count = sum(1 for byte in prefix if byte < 32 and byte not in {9, 10, 13})
    if prefix and control_count / len(prefix) > 0.05:
        return [Finding(path, "deny-control-byte-heavy-text-file", f"{control_count}/{len(prefix)}")]
    return []


def content_signature_findings(path: str, text: str) -> list[Finding]:
    findings: list[Finding] = []
    if path in PAYLOAD_TEXT_ALLOW_EXACT:
        return findings
    cdb_prompt_count = len(CDB_PROMPT_RE.findall(text))
    register_count = len(REGISTER_DUMP_RE.findall(text))
    stack_trace_count = len(STACK_TRACE_RE.findall(text))
    if cdb_prompt_count >= 3 or (cdb_prompt_count >= 1 and (register_count >= 4 or stack_trace_count >= 1)):
        findings.append(
            Finding(
                path,
                "deny-raw-debugger-transcript-content",
                f"cdbPrompts={cdb_prompt_count} registerRows={register_count} stackRows={stack_trace_count}",
            )
        )
    if DATA_IMAGE_RE.search(text):
        findings.append(Finding(path, "deny-data-image-url", "data:image/*;base64"))
    if EMBEDDED_PNG_RE.search(text):
        findings.append(Finding(path, "deny-embedded-png-header", "png header/base64 marker"))
    if EMBEDDED_JPEG_RE.search(text):
        findings.append(Finding(path, "deny-embedded-jpeg-header", "jpeg header/base64 marker"))
    if (REVIEWED_ENCODED_COMMENTS_SHA256.get(path) ==
            hashlib.sha256(text.encode("utf-8")).hexdigest()):
        return findings
    for match in BASE64_TOKEN_RE.finditer(text):
        token = match.group(0)
        if "0x" in token.lower():
            continue
        if "+" not in token and "/" not in token and "=" not in token:
            continue
        snippet = token[:117] + "..." if len(token) > 120 else token
        findings.append(Finding(path, "deny-large-base64-blob", snippet))
        break
    return findings


def text_findings(root: Path, path: str) -> list[Finding]:
    if path == SELF_REL or path in TEXT_ALLOW_EXACT:
        return []
    if not is_text_file(path):
        return []
    full_path = root / path
    try:
        text = full_path.read_bytes().decode("utf-8", errors="replace")
    except OSError as exc:
        return [Finding(path, "read-error", str(exc))]

    findings: list[Finding] = []
    for label, pattern in TEXT_DENY_PATTERNS:
        match = pattern.search(text)
        if match:
            snippet = match.group(0).replace("\n", "\\n")
            if len(snippet) > 120:
                snippet = snippet[:117] + "..."
            findings.append(Finding(path, label, snippet))
    findings.extend(content_signature_findings(path, text))
    return findings


def find_text_payload_errors(path: str, text: str, require_private_text_guard: bool = False) -> list[str]:
    """Compatibility helper used by release accounting scripts."""
    if path == SELF_REL or path in TEXT_ALLOW_EXACT:
        return []
    if not is_text_candidate(path):
        return []

    errors: list[str] = []
    for label, pattern in TEXT_DENY_PATTERNS:
        match = pattern.search(text)
        if match:
            snippet = match.group(0).replace("\n", "\\n")
            if len(snippet) > 120:
                snippet = snippet[:117] + "..."
            errors.append(f"{label} in {path}: {snippet}")
    for finding in content_signature_findings(path, text):
        errors.append(f"{finding.label} in {path}: {finding.detail}")
    return errors


def check_repo(root: Path, *, include_submodules: bool = False) -> list[Finding]:
    findings: list[Finding] = []
    try:
        paths = public_candidate_files(root, include_submodules=include_submodules)
    except RuntimeError as exc:
        return [Finding(".", "deny-public-candidate-enumeration-failed", str(exc))]
    if not paths:
        return [Finding(".", "deny-empty-public-candidate-set", "git candidate enumeration returned zero files")]
    if include_submodules:
        findings.extend(submodule_scan_findings(root))
    for path in paths:
        findings.extend(path_findings(path))
        findings.extend(exact_allow_hash_findings(root, path))
        findings.extend(size_findings(root, path))
        findings.extend(magic_findings(root, path))
        findings.extend(text_binary_findings(root, path))
        findings.extend(text_findings(root, path))
    return findings


def check_payload_root(root: Path) -> list[Finding]:
    findings: list[Finding] = []
    try:
        paths = payload_root_files(root)
    except RuntimeError as exc:
        return [Finding(".", "deny-public-candidate-enumeration-failed", str(exc))]
    if not paths:
        return [Finding(".", "deny-empty-public-candidate-set", "payload-root enumeration returned zero files")]
    for path in paths:
        findings.extend(path_findings(path))
        findings.extend(exact_allow_hash_findings(root, path))
        findings.extend(size_findings(root, path))
        findings.extend(magic_findings(root, path))
        findings.extend(text_binary_findings(root, path))
        findings.extend(text_findings(root, path))
    return findings


def run_self_test() -> int:
    # A reviewed encoding allows only the exact path/content. Secret scanning
    # still runs on that same file; no general TSV or Base64 exemption exists.
    with tempfile.TemporaryDirectory() as tmp:
        import base64
        root = Path(tmp)
        path = "reviewed-comments.tsv"
        value = base64.b64encode(("Reviewed text only. " * 64).encode()).decode() + "\n"
        REVIEWED_ENCODED_COMMENTS_SHA256[path] = hashlib.sha256(value.encode()).hexdigest()
        try:
            (root / path).write_text(value, encoding="utf-8")
            if text_findings(root, path):
                print("Public payload safety self-test: FAIL - exact reviewed comments rejected")
                return 1
            for candidate_path, candidate_text in (
                (path, value + "changed\n"),
                (path, value.replace("\n", "\r\n")),
                ("unreviewed.tsv", value),
            ):
                (root / candidate_path).write_bytes(candidate_text.encode())
                if not any(f.label == "deny-large-base64-blob" for f in text_findings(root, candidate_path)):
                    print("Public payload safety self-test: FAIL - encoded comment guard widened")
                    return 1
            secret_text = value + "ghp" + "_" + "testfixturevalue000000000000\n"
            REVIEWED_ENCODED_COMMENTS_SHA256[path] = hashlib.sha256(secret_text.encode()).hexdigest()
            (root / path).write_text(secret_text, encoding="utf-8")
            if not any(f.label == "deny-github-token" for f in text_findings(root, path)):
                print("Public payload safety self-test: FAIL - reviewed comments bypassed secret check")
                return 1
        finally:
            del REVIEWED_ENCODED_COMMENTS_SHA256[path]
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        openai_fixture = "sk" + "-" + "not-a-real-fixture-value"
        generic_secret_fixture = "sk" + "-" + "thisfixturehasenoughcharacters000000"
        github_fixture = "ghp" + "_" + "thisfixturehasenoughcharacters000000"
        stripe_fixture = "sk" + "_" + "live" + "_" + "thisfixturehasenoughcharacters000000"
        hf_fixture = "hf" + "_" + "thisfixturehasenoughcharacters000000"
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        (root / "game").mkdir()
        (root / "game" / "BEA.exe").write_bytes(b"not ok")
        (root / "media").mkdir()
        (root / "media" / "music.ogg").write_bytes(b"not ok")
        (root / "save-attempts").mkdir()
        (root / "save-attempts" / "slot.bes").write_bytes(b"not ok")
        (root / "local-rom-input").mkdir()
        (root / "local-rom-input" / "payload.txt").write_text("local-only payload root\n", encoding="utf-8")
        (root / "project-assets").mkdir()
        (root / "project-assets" / "frame.webp").write_bytes(b"RIFF\x00\x00\x00\x00WEBP")
        (root / "project-assets" / "theme.ogg").write_bytes(b"OggS\x00project audio")
        (root / "project-assets" / "effect.wav").write_bytes(b"RIFF\x00\x00\x00\x00WAVE")
        (root / "project-assets" / "model.obj").write_text("project-authored model\n", encoding="utf-8")
        (root / "project-assets" / "mesh.aya").write_bytes(b"project-authored mesh")
        (root / "project-assets" / "data.bin").write_bytes(b"project-authored data")
        (root / "installer.msix").write_bytes(b"not ok")
        (root / "symbols.pdb").write_bytes(b"Microsoft C/C++ MSF 7.00\r\n\x1aDS\0\0\0")
        (root / "package.msi").write_bytes(b"\xd0\xcf\x11\xe0\xa1\xb1\x1a\xe1not ok")
        (root / "cabinet.cab").write_bytes(b"MSCFnot ok")
        (root / "archive.7z").write_bytes(b"not ok")
        (root / "slot.sav").write_bytes(b"not ok")
        materialized_payloads = (
            "rebuild/OnslaughtRebuild.Core/Assets/Level100/retail-chunk.bin",
            "rebuild/OnslaughtRebuild.Godot/Assets/Aquila/retail-model.obj",
            "rebuild/OnslaughtRebuild.Godot/Assets/Hud/retail-hud.aya",
            "rebuild/OnslaughtRebuild.Godot/Assets/Level100/retail-terrain.bin",
        )
        for materialized_payload in materialized_payloads:
            target = root / materialized_payload
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"local retail materialization")
        (root / "rebuild" / "OnslaughtRebuild.Godot" / "Assets" / "Level100" / "README.md").write_text(
            "# Materialization owner\n",
            encoding="utf-8",
        )
        (root / "manual.pdf").write_bytes(b"%PDF-project documentation")
        (root / "screenshot.png").write_bytes(b"\x89PNG\r\n\x1a\nproject screenshot")
        (root / "disguised-note.md").write_bytes(b"\x89PNG\r\n\x1a\nnot ok")
        (root / "padded-disguised-note.md").write_bytes((b"x" * 40) + b"\x89PNG\r\n\x1a\nnot ok")
        (root / "nul-note.md").write_bytes(b"text before\x00payload")
        (root / "manual.html").write_text("<html>not ok</html>\n", encoding="utf-8")
        (root / "manual.xml").write_text("<xml>not ok</xml>\n", encoding="utf-8")
        (root / "local.rep").mkdir()
        (root / "local.rep" / "project.db").write_bytes(b"not ok")
        canonical_ghidra = root / "reverse-engineering" / "ghidra" / "BEA.rep" / "idata"
        canonical_ghidra.mkdir(parents=True)
        (canonical_ghidra / "database.gbf").write_bytes(b"Ghidra database fixture")
        (root / "reverse-engineering" / "ghidra" / "BEA.exe").write_bytes(b"not reviewed")
        (root / ".codex" / "custom").mkdir(parents=True)
        (root / ".codex" / "custom" / "instructions.md").write_text("not public\n", encoding="utf-8")
        (root / ".codex" / "sessions").mkdir(parents=True)
        (root / ".codex" / "sessions" / "session.jsonl").write_text("not ok\n", encoding="utf-8")
        (root / "archive" / "electron-workbench" / "packages" / "ui").mkdir(parents=True)
        (root / "archive" / "electron-workbench" / "packages" / "ui" / "index.html").write_text("<div>allowed app shell</div>\n", encoding="utf-8")
        (root / "references" / "AYAResourceExtractor").mkdir(parents=True)
        (root / "references" / "AYAResourceExtractor" / "BoxWithTextures.fbx").write_bytes(b"allowed non-BEA extractor fixture")
        (root / "big.md").write_bytes(b"x" * (MAX_UNREVIEWED_FILE_BYTES + 1))
        (root / "tests_shared" / "fixtures").mkdir(parents=True)
        (root / "tests_shared" / "fixtures" / "gold_career_save.bin").write_bytes(b"allowed regression fixture")
        (root / ".env").write_text(f"OPENAI_API_KEY={openai_fixture}\n", encoding="utf-8")
        (root / "docs.md").write_text("Raw RE notes may mention local paths and field names.\n", encoding="utf-8")
        (root / "token-note.md").write_text(
            f"Accidental token example {generic_secret_fixture}\n",
            encoding="utf-8",
        )
        (root / "inline-image.md").write_text(
            "Accidental inline image data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAAB\n",
            encoding="utf-8",
        )
        (root / "blob-note.md").write_text(
            "Accidental blob " + ("A" * 260) + "/" + ("B" * 260) + "\n",
            encoding="utf-8",
        )
        (root / "raw-debugger-summary.md").write_text(
            "0:000> r\n"
            "eax=00000001 ebx=00000002 ecx=00000003 edx=00000004 esi=00000005 edi=00000006 eip=00401000 esp=0019ff00\n",
            encoding="utf-8",
        )
        (root / "raw-debugger-events.jsonl").write_text(
            '{"line":"0:000> r eax=00000001 ebx=00000002 ecx=00000003 edx=00000004 eip=00401000"}\n',
            encoding="utf-8",
        )
        (root / "subagents" / "runtime").mkdir(parents=True)
        (root / "subagents" / "runtime" / "raw-session.cdb.txt").write_text(
            "0:000> g\nraw debugger transcript fixture\n",
            encoding="utf-8",
        )
        (root / "release" / "readiness").mkdir(parents=True)
        (root / "release" / "readiness" / "session-cdb-log.txt").write_text(
            "raw cdb log fixture\n",
            encoding="utf-8",
        )
        (root / "tools" / "runtime-probes").mkdir(parents=True)
        (root / "tools" / "runtime-probes" / "allowed-observer.cdb.txt").write_text(
            ".echo command script fixture\nvertarget\ng\n",
            encoding="utf-8",
        )
        (root / "github-token-note.md").write_text(
            f"Accidental token example {github_fixture}\n",
            encoding="utf-8",
        )
        (root / "stripe-token-note.md").write_text(
            f"Accidental token example {stripe_fixture}\n",
            encoding="utf-8",
        )
        (root / "hf-token-note.md").write_text(
            f"Accidental token example {hf_fixture}\n",
            encoding="utf-8",
        )
        subprocess.run(["git", "add", "."], cwd=root, check=True)
        findings = check_repo(root)
        labels = {finding.label for finding in findings}
        required = {
            "deny-root",
            "deny-codex-runtime-subtree",
            "deny-generated-or-private-path",
            "deny-operational-payload-suffix",
            "deny-materialized-retail-output",
            "deny-env-file",
            "deny-large-unreviewed-file",
            "deny-magic-pdb-symbols",
            "deny-magic-msi-ole-package",
            "deny-magic-cab-archive",
            "deny-nul-byte-in-text-file",
            "deny-data-image-url",
            "deny-embedded-png-header",
            "deny-raw-debugger-transcript-content",
            "exact-allow-hash-mismatch",
            "deny-openai-key",
            "deny-github-token",
            "deny-stripe-key",
            "deny-huggingface-token",
            "deny-raw-cdb-text-transcript",
        }
        missing = sorted(required - labels)
        if missing:
            print("Public payload safety self-test: FAIL")
            print(f"- missing expected labels: {', '.join(missing)}")
            print(f"- findings: {findings!r}")
            return 1
        if any(finding.path == "tests_shared/fixtures/gold_career_save.bin" for finding in findings):
            if not any(
                finding.path == "tests_shared/fixtures/gold_career_save.bin" and finding.label == "exact-allow-hash-mismatch"
                for finding in findings
            ):
                print("Public payload safety self-test: FAIL")
                print("- gold fixture exception was rejected for a reason other than hash mismatch")
                print(f"- findings: {findings!r}")
                return 1
        if any(finding.path == "references/AYAResourceExtractor/BoxWithTextures.fbx" for finding in findings):
            if not any(
                finding.path == "references/AYAResourceExtractor/BoxWithTextures.fbx"
                and finding.label == "exact-allow-hash-mismatch"
                for finding in findings
            ):
                print("Public payload safety self-test: FAIL")
                print("- AYAResourceExtractor fixture fbx was rejected for a reason other than hash mismatch")
                print(f"- findings: {findings!r}")
                return 1
        for denied_payload in materialized_payloads:
            if not any(finding.path == denied_payload for finding in findings):
                print("Public payload safety self-test: FAIL")
                print(f"- local retail-materialization output was not rejected: {denied_payload}")
                print(f"- findings: {findings!r}")
                return 1
        allowed_project_files = (
            "project-assets/frame.webp",
            "project-assets/theme.ogg",
            "project-assets/effect.wav",
            "project-assets/model.obj",
            "project-assets/mesh.aya",
            "project-assets/data.bin",
            "manual.pdf",
            "screenshot.png",
            "manual.html",
            "manual.xml",
            "rebuild/OnslaughtRebuild.Godot/Assets/Level100/README.md",
        )
        unexpected_project_findings = [
            finding for finding in findings if finding.path in allowed_project_files
        ]
        if unexpected_project_findings:
            print("Public payload safety self-test: FAIL")
            print("- legitimate authored/document asset formats were rejected")
            print(f"- findings: {unexpected_project_findings!r}")
            return 1
        if not any(finding.path == "tools/runtime-probes/allowed-observer.cdb.txt" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- tracked CDB command scripts were not rejected")
            print(f"- findings: {findings!r}")
            return 1
        if not any(finding.path.startswith("reverse-engineering/ghidra/BEA.rep/") for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- a Ghidra project database was not rejected")
            print(f"- findings: {findings!r}")
            return 1
        if not any(finding.path == "reverse-engineering/ghidra/BEA.exe" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- a standalone executable in the old Ghidra folder was not rejected")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        (root / "Game").mkdir()
        (root / "Game" / "payload.txt").write_text("case variant root\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=root, check=True)
        findings = check_repo(root)
        if not any(finding.label == "deny-root" and finding.path == "Game/payload.txt" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- case-variant game root was not rejected")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        non_repo = Path(tmp)
        findings = check_repo(non_repo)
        if not any(finding.label == "deny-public-candidate-enumeration-failed" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- non-git root did not fail closed")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        payload_root = Path(tmp)
        (payload_root / "README.MD").write_text("# OK\n", encoding="utf-8")
        (payload_root / "game").mkdir()
        (payload_root / "game" / "BEA.exe").write_bytes(b"not ok")
        findings = check_payload_root(payload_root)
        if not any(finding.label == "deny-root" and finding.path == "game/BEA.exe" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- payload-root mode did not reject game root")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        (root / ".gitmodules").write_text("[submodule \"broken\"\n\tpath = broken\n", encoding="utf-8")
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=root, check=True)
        findings = check_repo(root, include_submodules=True)
        if not any(finding.label == "deny-public-candidate-enumeration-failed" for finding in findings):
            print("Public payload safety self-test: FAIL")
            print("- malformed .gitmodules did not fail closed")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        (root / ".gitmodules").write_text(
            '[submodule "missing"]\n\tpath = references/missing\n\turl = ../missing.git\n',
            encoding="utf-8",
        )
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=root, check=True)
        findings = check_repo(root, include_submodules=True)
        if not any(
            finding.label == "deny-missing-submodule-scan"
            and finding.path == "references/missing"
            for finding in findings
        ):
            print("Public payload safety self-test: FAIL")
            print("- missing declared submodule did not fail closed")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        submodule = root / "references" / "payload-fixture"
        submodule.mkdir(parents=True)
        subprocess.run(["git", "init", "-q"], cwd=submodule, check=True)
        (submodule / "game").mkdir()
        (submodule / "game" / "BEA.exe").write_bytes(b"not ok")
        subprocess.run(["git", "add", "."], cwd=submodule, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Payload Fixture",
                "-c",
                "user.email=payload-fixture@example.invalid",
                "commit",
                "-q",
                "-m",
                "payload fixture",
            ],
            cwd=submodule,
            check=True,
        )
        (root / ".gitmodules").write_text(
            '[submodule "payload-fixture"]\n'
            "\tpath = references/payload-fixture\n"
            "\turl = ../payload-fixture.git\n",
            encoding="utf-8",
        )
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        subprocess.run(
            ["git", "-c", "advice.addEmbeddedRepo=false", "add", "."],
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        findings = check_repo(root, include_submodules=True)
        if not any(
            finding.path == "references/payload-fixture/game/BEA.exe"
            for finding in findings
        ):
            print("Public payload safety self-test: FAIL")
            print("- initialized submodule hard payload was not rejected")
            print(f"- findings: {findings!r}")
            return 1
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        (root / "game").mkdir()
        (root / "game" / "BEA.exe").write_bytes(b"root payload")
        submodule = root / "references" / "clean-fixture"
        submodule.mkdir(parents=True)
        subprocess.run(["git", "init", "-q"], cwd=submodule, check=True)
        (submodule / "README.MD").write_text("# Clean fixture\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=submodule, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Clean Fixture",
                "-c",
                "user.email=clean-fixture@example.invalid",
                "commit",
                "-q",
                "-m",
                "clean fixture",
            ],
            cwd=submodule,
            check=True,
        )
        (root / ".gitmodules").write_text(
            '[submodule "clean-fixture"]\n'
            "\tpath = references/clean-fixture\n"
            "\turl = ../clean-fixture.git\n",
            encoding="utf-8",
        )
        (root / "README.MD").write_text("# Root fixture\n", encoding="utf-8")
        subprocess.run(
            ["git", "-c", "advice.addEmbeddedRepo=false", "add", "."],
            cwd=root,
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        root_findings = set(check_repo(root))
        combined_findings = set(check_repo(root, include_submodules=True))
        if not root_findings or not root_findings.issubset(combined_findings):
            print("Public payload safety self-test: FAIL")
            print("- root findings changed when submodule scanning was enabled")
            print(f"- root findings: {sorted(root_findings, key=lambda item: item.path)!r}")
            print(f"- combined findings: {sorted(combined_findings, key=lambda item: item.path)!r}")
            return 1
    print("Public payload safety self-test: PASS")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--self-test", action="store_true", help="Run built-in fixture tests.")
    parser.add_argument("--include-submodules", action="store_true", help="Also scan initialized submodule tracked files with parent hard-payload rules.")
    parser.add_argument("--require-private-text-guard", action="store_true", help="Accepted for compatibility; denylist text guards are built in.")
    parser.add_argument("--repo-root", type=Path, default=ROOT)
    parser.add_argument("--payload-root", type=Path, help="Scan an already materialized non-git payload/export tree by walking files under this root.")
    args = parser.parse_args()

    if args.self_test:
        return run_self_test()

    root = (args.payload_root or args.repo_root).resolve()
    findings = check_payload_root(root) if args.payload_root else check_repo(root, include_submodules=args.include_submodules)
    if findings:
        print("Public payload safety check: FAIL")
        for finding in findings[:200]:
            print(f"- {finding.path}: {finding.label}: {finding.detail}")
        if len(findings) > 200:
            print(f"- ... ({len(findings) - 200} more)")
        return 1

    print("Public payload safety check: PASS")
    count = len(payload_root_files(root)) if args.payload_root else len(public_candidate_files(root, include_submodules=args.include_submodules))
    print(f"Public candidate files checked: {count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
