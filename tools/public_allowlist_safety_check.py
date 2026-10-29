#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Hard-payload safety check for the public-primary repo.

The public repository is now the working repository. This check rejects exact
local retail-materialization owners, copied game/runtime payload roots, build
outputs, and obvious secret files. File extensions alone do not establish
provenance, so ordinary project-authored or separately provided asset formats
remain reviewable. This is intentionally not a portable-app ZIP manifest; the
WinUI package has its own stricter payload boundary.

It also guards the one thing file types cannot show: original-game bytes and private machine paths quoted in
tracked text. Three rules fail any text file (comments, docs and code alike): a run of four or more
space-separated hex byte tokens, a contiguous hex string of sixteen or more digits that is not a SHA-1 or SHA-256
identity, and the private path markers ``local-lab/``, ``/srv/archive``, ``~/Projects/`` and ``/home/<user>``.
Reviewed exceptions are listed in RETAIL_TEXT_ALLOW with a reason each.
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

# --- original-game bytes and private paths in tracked text -----------------------------------------------------------
#
# (a) Four or more space-separated two-digit hex tokens (an optional 0x on each): a byte dump or an instruction
#     encoding. Three tokens or fewer are not flagged: they cannot be told from short prose such as "be it" or "10 20".
RETAIL_BYTE_RUN_RE = re.compile(
    r"(?<![0-9A-Za-z_])(?:0x)?[0-9A-Fa-f]{2}(?:[ \t]+(?:0x)?[0-9A-Fa-f]{2}){3,}(?![0-9A-Za-z_])"
)
# (b) A contiguous run of sixteen or more hex digits holding at least one a-f letter (so long decimal numbers pass).
#     Exactly 40 (SHA-1) and exactly 64 (SHA-256) digits are identities and pass. A 0x literal up to 64 bits (16
#     digits) is a number, not a dump; wider than that it is not a number any code uses and is flagged. A run that
#     follows a "." is the fraction of a decimal such as 9.2233720368547758e18 and is skipped.
LONG_HEX_RE = re.compile(r"(?<![0-9A-Za-z_.])(0[xX])?([0-9A-Fa-f]{16,})(?![0-9A-Za-z_])")
LONG_HEX_ALLOWED_LENGTHS = (40, 64)
# (c) Private machine paths. The first is this project's ignored lab folder; the others are the maintainers' layout.
PRIVATE_PATH_RE = re.compile(
    r"(?<![A-Za-z0-9_-])local-lab/[^\s\"'`<>()\[\]|,;]*|/srv/archive[^\s\"'`<>()\[\]|,;]*"
    r"|~/Projects/[^\s\"'`<>()\[\]|,;]*|/home/[A-Za-z0-9_.][A-Za-z0-9_.-]*[^\s\"'`<>()\[\]|,;]*"
)

# Reviewed exceptions: the exact matched token in the exact file, with the reason. A token that is not listed for that
# file fails, and a listed token fails in every other file. The hex alphabets are the only global entries.
RETAIL_TEXT_ALLOW_ANY_FILE: dict[str, str] = {
    "0123456789abcdef": "the lower-case hex digit alphabet, a constant string in hex-validation code",
    "0123456789ABCDEF": "the upper-case hex digit alphabet",
    "0123456789abcdefABCDEF": "the mixed-case hex digit alphabet",
}
RETAIL_TEXT_ALLOW: dict[str, dict[str, str]] = {
    # --- code that locates the ignored lab folder by relative path (changing the string changes behaviour) ------
    "rebuild/OnslaughtRebuild.Godot/FrontendCaptureRig.cs": {
        "local-lab/retail-reference-pristine/level100-gameplay/manifest.json":
            "the capture rig looks for the retail reference manifest in the ignored lab folder",
    },
    "rebuild/tools/frontend-parity-plan.json": {
        "local-lab/retail-reference-pristine/nofmv-frontend-2026-07-26":
            "referenceRoot: the scorer resolves it against the repository root",
    },
    "rebuild/tools/materialize_retail_assets.py": {
        "local-lab/rebuild-godot": "default work root under the ignored lab folder (code and its help text)",
        "local-lab/startup-media": "default startup-media folder under the ignored lab folder (code and its help text)",
    },
    "rebuild/tools/x87_oracle/x87_oracle.py": {
        "local-lab/third-party/msvc6-sp5-toolchain": "default location of the pinned toolchain in the ignored lab folder",
    },
    "rebuild/tools/first_flight_tests.py": {
        "local-lab/startup-media": "the test checks the tool's default startup-media path",
        "/home/someone/profile": "a synthetic profile path in a unit test; no real account",
    },
    "tools/frontend_arc_bracket_fit.py": {
        "local-lab/retail-reference-pristine/select-level/04-select-level-640x480.png":
            "default reference image for the fit, read from the ignored lab folder",
        "local-lab/retail-reference-pristine/choose-game-name/choose-game-name-640x480.png":
            "default reference image for the fit, read from the ignored lab folder",
    },
    "tools/terrain_macro_inversion_probe.py": {
        "local-lab/godot-captures/inv-probe": "an example value in an argparse help string",
    },
    # --- tools whose job is to treat the ignored lab folder specially --------------------------------------------
    "tools/check_installed_game_claims.py": {"local-lab/": "SKIP_PREFIXES: the ignored folder is not scanned"},
    "tools/md_link_check.py": {"local-lab/": "the scope note it prints says the ignored folder is invisible to the check"},
    "tools/doc_header_check.py": {
        "local-lab/": "the rule that excludes the ignored folder, and the self-test case that checks it",
        "local-lab/NOTES.md": "self-test fixture: a path inside the excluded folder",
        "local-lab/safe-copy-bea-pristine/": "self-test fixture text for the header check",
        "local-lab/safe-copy-bea-pristine/BEA.exe.original.backup": "self-test fixture text for the header check",
        "local-lab/ghidra-fullpass-2026-07-23/exports/W001/": "self-test fixture text for the header check",
    },
    # --- the project's own synthetic test vectors: made up inside the tests, not retail data -------------------------
    "rebuild/OnslaughtRebuild.Parity.Tests/HarnessTests.cs": {
        "0000a04100000040": "synthetic probe row (a LIFE pair of floats as hex) made up in the test; not retail data",
        "45332211ddccbbaa": "synthetic RNG probe row made up in the test; not retail data",
        "46332211ddccbbaa": "the same synthetic row with one digit changed: the mismatch the test expects to be reported",
        "0000803f0000004000004040": "synthetic probe row (a POS triple of floats as hex) made up in the test; not retail data",
    },
}

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


def retail_text_findings(path: str, text: str) -> list[Finding]:
    """The three retail-text rules. Details give line numbers and counts, never the bytes themselves."""
    allowed_here = RETAIL_TEXT_ALLOW.get(path, {})

    def allowed(token: str) -> bool:
        return token in allowed_here or token in RETAIL_TEXT_ALLOW_ANY_FILE

    def line_of(match: re.Match) -> int:
        return text.count("\n", 0, match.start()) + 1

    findings: list[Finding] = []
    for match in RETAIL_BYTE_RUN_RE.finditer(text):
        run = match.group(0)
        if allowed(run):
            continue
        tokens = len(run.split())
        findings.append(Finding(path, "deny-retail-byte-run", f"line {line_of(match)}: {tokens} space-separated hex byte tokens"))
    for match in LONG_HEX_RE.finditer(text):
        prefix, digits = match.group(1), match.group(2)
        if allowed(digits) or allowed(match.group(0)):
            continue
        if not re.search(r"[A-Fa-f]", digits):
            continue  # a long decimal number
        if len(digits) in LONG_HEX_ALLOWED_LENGTHS:
            continue  # a SHA-1 or SHA-256 identity
        if prefix and len(digits) == 16:
            continue  # a 64-bit numeric literal such as an IEEE-754 double pattern
        findings.append(Finding(path, "deny-long-hex-string", f"line {line_of(match)}: {len(digits)} contiguous hex digits"))
    for match in PRIVATE_PATH_RE.finditer(text):
        token = match.group(0).rstrip(".:")
        if allowed(token):
            continue
        findings.append(Finding(path, "deny-private-path-marker", f"line {line_of(match)}: {token}"))
    return findings


def content_signature_findings(path: str, text: str, *, retail_rules: bool = True) -> list[Finding]:
    findings: list[Finding] = []
    if path in PAYLOAD_TEXT_ALLOW_EXACT:
        return findings
    if retail_rules:
        findings.extend(retail_text_findings(path, text))
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


def text_findings(root: Path, path: str, *, retail_rules: bool = True) -> list[Finding]:
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
    findings.extend(content_signature_findings(path, text, retail_rules=retail_rules))
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
    submodule_prefixes: tuple[str, ...] = ()
    if include_submodules:
        findings.extend(submodule_scan_findings(root))
        try:
            submodule_prefixes = tuple(f"{item}/" for item in submodule_paths(root))
        except RuntimeError:
            submodule_prefixes = ()
    for path in paths:
        findings.extend(path_findings(path))
        findings.extend(exact_allow_hash_findings(root, path))
        findings.extend(size_findings(root, path))
        findings.extend(magic_findings(root, path))
        findings.extend(text_binary_findings(root, path))
        # A submodule's files are another project's published text; the retail-text rules cover this repository's own.
        findings.extend(text_findings(root, path, retail_rules=not path.startswith(submodule_prefixes)))
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


def retail_text_self_test() -> list[str]:
    """Fixtures and mutants for the three retail-text rules; returns the failures (empty when all pass)."""
    failures: list[str] = []

    def labels(path: str, text: str) -> set[str]:
        return {finding.label for finding in retail_text_findings(path, text)}

    def must_fail(rule: str, name: str, text: str, path: str = "doc.md") -> None:
        if rule not in labels(path, text):
            failures.append(f"{rule} did not fire: {name}")

    def must_pass(name: str, text: str, path: str = "doc.md") -> None:
        found = labels(path, text)
        if found:
            failures.append(f"false positive ({', '.join(sorted(found))}): {name}")

    run, long_hex, path_rule = "deny-retail-byte-run", "deny-long-hex-string", "deny-private-path-marker"

    # (a) four or more space-separated hex byte tokens (synthetic bytes, not game bytes)
    must_fail(run, "four tokens", "Body ab cd ef 01.\n")
    must_fail(run, "upper case", "Body AB CD EF 01.\n")
    must_fail(run, "tab separated", "ab\tcd\tef\t01\n")
    must_fail(run, "several spaces", "ab  cd   ef 01\n")
    must_fail(run, "0x on each token", "0xab 0xcd 0xef 0x01\n")
    must_fail(run, "inside an XML doc tag", "/// <c>ab cd ef 01 23</c>\n", "Doc.cs")
    must_fail(run, "an ellipsis in the middle", "<c>ab cd ef 01 23 45 ... 67 89</c>\n")
    must_fail(run, "twenty tokens", "bytes " + " ".join(["ab"] * 20) + "\n")
    must_fail(run, "in a Python comment", "x = 1  # ab cd ef 01\n", "tool.py")
    must_fail(run, "in a JSON string", '{"note": "ab cd ef 01"}\n', "plan.json")
    must_fail(run, "start of the file", "ab cd ef 01")
    must_pass("three tokens", "Body ab cd ef.\n")
    must_pass("comma separated array initialiser", "var a = new byte[] { 0xab, 0xcd, 0xef, 0x01 };\n", "Code.cs")
    must_pass("eight digits without separators", "abcdef01\n")
    must_pass("a run broken by a word", "ab cd of ef 01 23\n")
    must_pass("three-digit tokens", "abc def 012 345\n")
    must_pass("prose", "to be or not to be a fine ad\n")

    # (b) contiguous hex of 16 or more digits other than a SHA-1 or SHA-256 identity
    digits = "a1b2c3d4e5f60718293a4b5c6d7e8f90" * 4  # synthetic: 128 digits, no game bytes
    for length in (16, 17, 24, 33, 34, 39, 41, 63, 65, 100):
        must_fail(long_hex, f"{length} digits", f"body {digits[:length]} on specimen\n")
    must_fail(long_hex, "upper case", "body " + digits[:20].upper() + "\n")
    must_fail(long_hex, "SHA-256 glued to its digits", "SHA-256" + digits[:64] + ".\n")
    must_fail(long_hex, "0x wider than 64 bits", "0x" + digits[:18] + "\n")
    must_fail(long_hex, "in a code string", 'const string s = "' + digits[:24] + '";\n', "Code.cs")
    must_pass("15 digits", f"body {digits[:15]}\n")
    must_pass("a SHA-1 identity", f"sha1 {digits[:40]}\n")
    must_pass("a SHA-256 identity", f"sha256 {digits[:64]}\n")
    must_pass("a SHA-256 identity after a space", f"SHA-256 {digits[:64]}\n")
    must_pass("a long decimal number", "count 12345678901234567890\n")
    must_pass("a decimal fraction", "x = 0.19999998807907104;\n")
    must_pass("a decimal in exponent form", "x = 9.2233720368547758e18;\n")
    must_pass("a 64-bit 0x literal", "const ulong a = 0x4075E00000000000UL; // bits 0x4075E00000000000\n", "Code.cs")
    must_fail(long_hex, "the same 16 digits without 0x", "bits 4075E00000000000\n", "Code.cs")
    must_pass("0x with 40 digits", "0x" + digits[:40] + "\n")
    must_pass("part of an identifier", "foo_" + digits[:20] + "\n")
    must_pass("the hex digit alphabet", 'c in "0123456789abcdef"\n', "tool.py")

    # (c) private path markers
    must_fail(path_rule, "the lab folder", "see local-lab/notes.md\n")
    must_fail(path_rule, "the lab folder in backticks", "`local-lab/`\n")
    must_fail(path_rule, "an archive mount", "copy to /srv/archive-a/data\n")
    must_fail(path_rule, "the bare archive mount", "mounted at /srv/archive\n")
    must_fail(path_rule, "a home-relative project path", "cd ~/Projects/game-dev/x\n")
    must_fail(path_rule, "a home folder", "see /home/alice/work\n")
    must_fail(path_rule, "after a drive path", "C:/x/local-lab/y\n")
    must_fail(path_rule, "in a C# string", 'var p = "local-lab/startup-media";\n', "Code.cs")
    must_pass("the word without a slash", "the local-lab folder\n")
    must_pass("a longer name", "local-labs/x and locallab/x\n")
    must_pass("a different prefix", "my-local-lab/x\n")
    must_pass("projects without the slash", "~/Projects\n")
    must_pass("another mount", "/srv/other/x\n")
    must_pass("a bare home", "/home/\n")

    # reviewed exceptions: exact token, exact file
    saved = dict(RETAIL_TEXT_ALLOW)
    try:
        RETAIL_TEXT_ALLOW["listed.md"] = {"local-lab/x": "test", "abcdef0123456789": "test"}
        must_pass("an allowed token in its file", "see local-lab/x and abcdef0123456789\n", "listed.md")
        must_fail(path_rule, "the same token in another file", "see local-lab/x\n", "other.md")
        must_fail(long_hex, "the same digits in another file", "abcdef0123456789\n", "other.md")
        must_fail(path_rule, "another token in the listed file", "see local-lab/y\n", "listed.md")
        must_fail(path_rule, "an allowed token with a suffix in the listed file", "see local-lab/xy\n", "listed.md")
        must_fail(long_hex, "allowed digits with one more digit in the listed file", "abcdef01234567890\n", "listed.md")
        must_fail(long_hex, "other digits in the listed file", "abcdef0123456780\n", "listed.md")
        must_fail(run, "a byte run in the listed file", "ab cd ef 01\n", "listed.md")
    finally:
        RETAIL_TEXT_ALLOW.clear()
        RETAIL_TEXT_ALLOW.update(saved)
    for path, tokens in RETAIL_TEXT_ALLOW.items():
        for token, reason in tokens.items():
            if not reason.strip():
                failures.append(f"allowlist entry without a reason: {path}: {token}")

    # the rules reach every text file type through check_repo, but not a submodule's own files
    with tempfile.TemporaryDirectory() as tmp:
        root = Path(tmp)
        subprocess.run(["git", "init", "-q"], cwd=root, check=True)
        (root / "README.MD").write_text("# OK\n", encoding="utf-8")
        dump = "ab cd ef 01 23"
        (root / "doc.md").write_text(f"bytes {dump}\n", encoding="utf-8")
        (root / "Code.cs").write_text(f"// {dump}\nclass C {{}}\n", encoding="utf-8")
        (root / "tool.py").write_text(f"# {dump}\n", encoding="utf-8")
        (root / "data.json").write_text('{"a": "' + dump + '"}\n', encoding="utf-8")
        (root / "long.md").write_text("body " + digits[:34] + "\n", encoding="utf-8")
        (root / "paths.md").write_text("see local-lab/x and /home/alice/y\n", encoding="utf-8")
        submodule = root / "references" / "upstream"
        submodule.mkdir(parents=True)
        subprocess.run(["git", "init", "-q"], cwd=submodule, check=True)
        (submodule / "notes.md").write_text(f"bytes {dump} local-lab/x {digits[:34]}\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=submodule, check=True)
        subprocess.run(
            ["git", "-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-q", "-m", "fixture"],
            cwd=submodule, check=True,
        )
        (root / ".gitmodules").write_text(
            '[submodule "upstream"]\n\tpath = references/upstream\n\turl = ../upstream.git\n', encoding="utf-8"
        )
        subprocess.run(
            ["git", "-c", "advice.addEmbeddedRepo=false", "add", "."],
            cwd=root, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        )
        found = check_repo(root, include_submodules=True)
        for name, label in (
            ("doc.md", run), ("Code.cs", run), ("tool.py", run), ("data.json", run),
            ("long.md", long_hex), ("paths.md", path_rule),
        ):
            if not any(f.path == name and f.label == label for f in found):
                failures.append(f"check_repo missed {label} in {name}")
        leaked = [f for f in found if f.path.startswith("references/upstream/")]
        if leaked:
            failures.append(f"retail-text rules were applied to a submodule's files: {leaked!r}")
        # without --include-submodules the nested files are not scanned at all
        plain = check_repo(root)
        if any(f.path.startswith("references/upstream/") for f in plain):
            failures.append("submodule files were scanned without --include-submodules")
    return failures


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
    retail_failures = retail_text_self_test()
    if retail_failures:
        print("Public payload safety self-test: FAIL")
        for item in retail_failures:
            print(f"- {item}")
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
