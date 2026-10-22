#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Enforce the tracked documentation header standard (``DOCUMENTATION.md``).

Why this exists
---------------
Every convention in this repository that had no checker has rotted. The header
patterns this tool enforces were not invented for it: they were measured out of
the corpus on 2026-07-28 and written up in ``DOCUMENTATION.md``. This tool is
the half that makes them survive.

The single highest-value rule here is the **specimen rule**. There are two
retail binaries on this machine and they are not interchangeable -- the
installed Steam ``BEA.exe`` is deliberately patched. A document that quotes a
byte or an address without naming which binary it read is a category-1 defect
(a claim that may simply be false), not a formatting nit. This checker turns
that rule from prose into a gate, and additionally rejects a Specimen field
that points at the live install.

Scope
-----
Tracked files only, sourced from ``git ls-files``. ``local-lab/`` is untracked
by design and is therefore structurally out of scope; working notes there stay
loose on purpose and are additionally excluded by path so the rule is legible
rather than accidental.

The ratchet
-----------
``tools/doc_header_backlog.txt`` lists the documents that predate the standard.
They are reported but do not fail. Anything not in that list is enforced, so a
newly added document is gated from the moment it exists. The list can only
shrink: a stale entry (a path that no longer exists, or one that now conforms)
is itself a failure, so the backlog cannot quietly become permanent.

Exit codes
----------
0  every enforced document conforms
1  at least one enforced document violates the standard
2  the check could not run -- no git, no backlog file, bad arguments.
   It abstains rather than passing. "I could not look" is not "I found no
   problem"; see AGENTS.md, "A critic that cannot return UNSCORED is not a
   gate."
"""

from __future__ import annotations

import argparse
import datetime as _dt
import os
import re
import subprocess
import sys
import tempfile
from dataclasses import dataclass, field
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[1]
BACKLOG_PATH = REPO_ROOT / "tools" / "doc_header_backlog.txt"
STANDARD_PATH = "DOCUMENTATION.md"

# The date DOCUMENTATION.md took effect. Used only by the wave-shard ratchet,
# which gates its two optional fields on the shard's own `Reviewed at:` stamp
# rather than on a backlog entry. See check_wave_shard.
STANDARD_EFFECTIVE = _dt.date(2026, 7, 28)

# --------------------------------------------------------------------------
# scope
# --------------------------------------------------------------------------

# Out of scope entirely. Each entry states why, because an unexplained
# exclusion is how a gate silently stops covering the thing it was built for.
EXCLUDED_PREFIXES: tuple[tuple[str, str], ...] = (
    ("local-lab/", "untracked working notes; deliberately looser"),
    (".github/", "issue/PR templates, not documents"),
    ("lore/", "in-universe narrative; makes no claim about the shipped game"),
    ("lore-book/", "in-universe narrative"),
    ("references/", "pinned third-party source submodule"),
    ("node_modules/", "dependency tree"),
    (".artifacts/", "generated check output"),
    ("release/artifacts/", "generated release output"),
)

EXCLUDED_PATHS: tuple[tuple[str, str], ...] = (
    ("CLAUDE.md", "router only: points at AGENTS.md and makes no claim of its own"),
)

# --------------------------------------------------------------------------
# classes
# --------------------------------------------------------------------------

CLASS_GOVERNANCE = "GOVERNANCE"
CLASS_INDEX = "INDEX"
CLASS_FINDING = "FINDING"
CLASS_FUNCTION_NOTE = "FUNCTION-NOTE"
CLASS_WAVE_SHARD = "WAVE-SHARD"

GOVERNANCE_PATHS = frozenset(
    {
        "AGENTS.md",
        "CONTRIBUTING.md",
        "CURRENT_CAPABILITIES.md",
        "DOCUMENTATION.md",
        "GOAL.md",
        "LOCAL_LAB_OVERLAY.md",
        "PROGRAM.md",
        "README.MD",
        "SECURITY.md",
        "VALIDATION.md",
        "rebuild/PROVENANCE.md",
    }
)

INDEX_BASENAMES = frozenset({"_index.md", "readme.md", "readme.md".upper()})

FUNCTIONS_ROOT = "reverse-engineering/binary-analysis/functions/"
FULLPASS_ROOT = "reverse-engineering/binary-analysis/ghidra-fullpass-findings/"


def classify(rel: str) -> str:
    if rel in GOVERNANCE_PATHS:
        return CLASS_GOVERNANCE
    if rel.startswith(FULLPASS_ROOT):
        tail = rel[len(FULLPASS_ROOT) :]
        parts = tail.split("/")
        if len(parts) == 3 and parts[1] in ("primary", "adversarial"):
            return CLASS_WAVE_SHARD
        return CLASS_INDEX if Path(rel).name.lower() in INDEX_BASENAMES else CLASS_FINDING
    if rel.startswith(FUNCTIONS_ROOT):
        return CLASS_INDEX if Path(rel).name == "_index.md" else CLASS_FUNCTION_NOTE
    if Path(rel).name.lower() in INDEX_BASENAMES:
        return CLASS_INDEX
    if re.search(r"(^|/)[A-Z0-9_.-]*INDEX[A-Z0-9_.-]*\.md$", rel):
        return CLASS_INDEX
    return CLASS_FINDING


# --------------------------------------------------------------------------
# header parsing
# --------------------------------------------------------------------------

HEADER_BLOCK_MAX_LINES = 60

# Accepts the four blockquote/bullet/bold decorations already in use:
#   Status: ...
#   > Status: ...
#   - Status: ...
#   > **Verdict:** ...
FIELD_RE = re.compile(
    r"^\s{0,3}(?:>\s?)*\s*(?:[-*+]\s+)?\*{0,2}"
    r"(?P<name>[A-Za-z][A-Za-z ]{0,24}?)"
    r"\*{0,2}\s*:\s?(?P<value>.*)$"
)

# Not \b-delimited: in an ISO 8601 stamp the date is followed by `T`, which is
# a word character, so \b would refuse to match `2026-07-23T19:35:02Z` and the
# wave-shard date ratchet would silently never fire.
ISO_DATE_RE = re.compile(r"(?<!\d)(\d{4})-(\d{2})-(\d{2})(?!\d)")
ISO_STAMP_RE = re.compile(r"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")

# A retail virtual address. The image is based at 0x00400000 and is 2,506,752
# bytes, so anything in this window is a claim about shipped bytes.
RETAIL_VA_RE = re.compile(r"\b0x00[0-9A-Fa-f]{6}\b")
RETAIL_VA_LO, RETAIL_VA_HI = 0x00400000, 0x006FFFFF

HASH_RE = re.compile(r"\b[0-9A-Fa-f]{8,64}\b")
EVIDENCE_GRADES = ("MEASURED", "SOURCE", "INFERRED", "UNKNOWN")
PLACEHOLDERS = frozenset({"tbd", "todo", "tba", "n/a", "na", "-", "?", "xxx", "fixme"})


@dataclass
class Doc:
    rel: str
    text: str
    fields: dict[str, str] = field(default_factory=dict)
    field_lines: dict[str, int] = field(default_factory=dict)
    first_heading: str | None = None


def header_block(text: str) -> tuple[list[str], int]:
    """Lines before the first level-2+ heading, capped. Returns (lines, count)."""
    lines = text.splitlines()
    out: list[str] = []
    for line in lines[:HEADER_BLOCK_MAX_LINES]:
        if re.match(r"^\s{0,3}#{2,6}\s", line):
            break
        out.append(line)
    return out, len(out)


def parse(rel: str, text: str) -> Doc:
    doc = Doc(rel=rel, text=text)
    lines, _ = header_block(text)

    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith("# "):
            if doc.first_heading is None:
                doc.first_heading = stripped[2:].strip()
            continue
        match = FIELD_RE.match(line)
        if not match:
            continue
        name = " ".join(match.group("name").split()).lower()
        if name in doc.fields:
            continue
        value = [match.group("value").strip()]
        # A field's value continues onto following lines until a blank line,
        # a heading, or the start of another field. Both the Verdict
        # blockquote and the Specimen declaration wrap in practice, and the
        # hash regularly lands on the continuation line.
        for follow in lines[index + 1 :]:
            body = re.sub(r"^\s{0,3}(?:>\s?)*", "", follow).strip()
            if not body:
                break
            if body.startswith("#"):
                break
            if FIELD_RE.match(follow) and not follow.strip().startswith("`"):
                break
            value.append(body)
        doc.fields[name] = " ".join(v for v in value if v).strip()
        doc.field_lines[name] = index + 1

    if doc.first_heading is None:
        for line in lines:
            if line.strip().startswith("# "):
                doc.first_heading = line.strip()[2:].strip()
                break
    return doc


def strip_fences(text: str) -> str:
    return re.sub(r"```.*?```", "", text, flags=re.S)


def makes_byte_claim(text: str) -> bool:
    for match in RETAIL_VA_RE.finditer(strip_fences(text)):
        value = int(match.group(0), 16)
        if RETAIL_VA_LO <= value <= RETAIL_VA_HI:
            return True
    return False


def is_placeholder(value: str) -> bool:
    bare = value.strip().strip("*`_ ").lower()
    return not bare or bare in PLACEHOLDERS


# --------------------------------------------------------------------------
# rules
# --------------------------------------------------------------------------


@dataclass(frozen=True)
class Violation:
    rel: str
    rule: str
    detail: str


def _one_of(doc: Doc, names: tuple[str, ...]) -> tuple[str, str] | None:
    for name in names:
        if name in doc.fields:
            return name, doc.fields[name]
    return None


def check_prose(doc: Doc, klass: str) -> list[Violation]:
    """Rules for hand-written documents: GOVERNANCE, INDEX, FINDING."""
    bad: list[Violation] = []
    add = lambda rule, detail: bad.append(Violation(doc.rel, rule, detail))

    if not doc.first_heading:
        add("H1", "no level-1 heading in the header block")

    status = _one_of(doc, ("status",))
    if status is None:
        add("Status", "missing `Status:` -- a reader cannot tell if this is live")
    elif is_placeholder(status[1]):
        add("Status", f"`Status:` is a placeholder ({status[1]!r})")

    dated = _one_of(doc, ("date", "last updated"))
    if dated is None:
        add(
            "Date",
            "missing `Date:` (dated write-up) or `Last updated:` (living document)"
            " -- nothing says how old the claim is",
        )
    else:
        name, value = dated
        match = ISO_DATE_RE.search(value)
        if not match:
            add(name.title(), f"`{name}:` has no ISO YYYY-MM-DD date ({value[:60]!r})")
        else:
            try:
                _dt.date(int(match.group(1)), int(match.group(2)), int(match.group(3)))
            except ValueError:
                add(name.title(), f"`{name}:` date is not a real calendar date ({match.group(0)})")

    headline = _one_of(doc, ("verdict", "summary"))
    if headline is None:
        add(
            "Verdict",
            "missing `Verdict:` (what this settles) or `Summary:` (what this is for)"
            " -- a reader cannot stop after ten lines",
        )
    elif is_placeholder(headline[1]):
        add("Verdict", f"`{headline[0]}:` is a placeholder ({headline[1]!r})")

    if klass == CLASS_FINDING:
        bad.extend(check_evidence(doc))
        bad.extend(check_specimen(doc))
    return bad


def check_evidence(doc: Doc) -> list[Violation]:
    if "evidence" not in doc.fields:
        return [
            Violation(
                doc.rel,
                "Evidence",
                "missing `Evidence:` -- the MEASURED / SOURCE / INFERRED / UNKNOWN "
                "partition is what separates a fact from a guess",
            )
        ]
    value = doc.fields["evidence"]
    bare = value.lstrip("*_ `")
    grade = next((g for g in EVIDENCE_GRADES if bare.upper().startswith(g)), None)
    if grade is None:
        return [
            Violation(
                doc.rel,
                "Evidence",
                "`Evidence:` must begin with one of "
                f"{' | '.join(EVIDENCE_GRADES)}; got {value[:60]!r}",
            )
        ]
    remainder = bare[len(grade) :].strip(" *_`-\u2014\u2013:.,;")
    if len(remainder) < 12:
        return [
            Violation(
                doc.rel,
                "Evidence",
                f"`Evidence: {grade}` names no evidence. State the capture, byte "
                "comparison, test, or source file+line -- or, for UNKNOWN, what "
                "would settle it",
            )
        ]
    return []


def check_specimen(doc: Doc) -> list[Violation]:
    if not makes_byte_claim(doc.text):
        return []
    if "specimen" not in doc.fields:
        return [
            Violation(
                doc.rel,
                "Specimen",
                "quotes a retail address in 0x00400000-0x006FFFFF but declares no "
                "`Specimen:`. The installed BEA.exe is deliberately patched; a "
                "byte claim that does not name its binary may simply be false",
            )
        ]
    value = doc.fields["specimen"]
    bad: list[Violation] = []
    if ".exe" not in value.lower() and ".dat" not in value.lower():
        bad.append(
            Violation(doc.rel, "Specimen", f"`Specimen:` names no file ({value[:70]!r})")
        )
    if not any(len(m.group(0)) >= 8 for m in HASH_RE.finditer(value)):
        bad.append(
            Violation(
                doc.rel,
                "Specimen",
                "`Specimen:` carries no hash prefix of 8+ hex characters",
            )
        )
    # The exemption is per PATH, not per field: a Specimen line may legitimately
    # name the pristine backup *and* mention the install, and an earlier version
    # of this rule let that combination through. Each steamapps-bearing token is
    # judged on its own.
    for token in re.split(r"[\s`'\"(),;]+", value):
        low = token.lower()
        if "steamapps" not in low:
            continue
        if ".original.backup" in low or "safe-copy" in low:
            continue
        bad.append(
            Violation(
                doc.rel,
                "Specimen",
                f"`Specimen:` cites the live Steam install ({token[:70]}). That "
                "binary is deliberately patched; read byte evidence from the "
                "pristine specimen instead",
            )
        )
        break
    return bad


def check_wave_shard(doc: Doc) -> list[Violation]:
    """Fullpass wave shards.

    Three fields are gated on every shard because all 514 already carry them.
    ``Export root:`` and ``Primary:`` are gated only on shards reviewed on or
    after the standard's effective date. Measured 2026-07-28: 107 of 514 shards
    carry no ``Export root:`` and 181 of 257 adversarial shards carry no
    ``Primary:``. Those come from the 2026-07-23 expedition, which closed on
    2026-07-25 and will not be re-reviewed, so gating them would freeze a
    288-entry backlog that can never shrink -- and a backlog that cannot move
    stops being read. The shard's own ``Reviewed at:`` stamp is the ratchet, so
    the next wave is gated without a single backlog line.
    """
    bad: list[Violation] = []
    for name in ("phase", "function count", "reviewed at"):
        if name not in doc.fields:
            bad.append(Violation(doc.rel, name.title(), f"missing `{name.title()}:`"))
    reviewed = doc.fields.get("reviewed at", "")
    if reviewed and not ISO_STAMP_RE.search(reviewed):
        bad.append(
            Violation(
                doc.rel,
                "Reviewed At",
                "`Reviewed at:` is not an ISO 8601 timestamp",
            )
        )

    match = ISO_DATE_RE.search(reviewed)
    reviewed_on: _dt.date | None = None
    if match:
        try:
            reviewed_on = _dt.date(
                int(match.group(1)), int(match.group(2)), int(match.group(3))
            )
        except ValueError:
            reviewed_on = None
    if reviewed_on is None or reviewed_on < STANDARD_EFFECTIVE:
        return bad

    if "export root" not in doc.fields:
        bad.append(Violation(doc.rel, "Export Root", "missing `Export root:`"))
    if "/adversarial/" in doc.rel and "primary" not in doc.fields:
        bad.append(
            Violation(doc.rel, "Primary", "adversarial shard does not name its `Primary:`")
        )
    return bad


def check_function_note(doc: Doc) -> list[Violation]:
    """Per-function RE notes.

    Deliberately NOT subject to the Specimen rule. These notes index a Ghidra
    database rather than establish a byte finding; every one of them quotes an
    address, so the rule would fire on all of them while adding no truth -- the
    binary is already named in their own trailer. Since 2026-10-15 the
    byte-matching decompilation, not a Ghidra name table, is the naming authority.
    A gate that punishes correct documents gets switched off.
    """
    bad: list[Violation] = []
    if not doc.first_heading:
        bad.append(Violation(doc.rel, "H1", "no level-1 heading in the header block"))
    if "source file" not in doc.fields:
        bad.append(
            Violation(doc.rel, "Source File", "missing `Source File:` provenance trailer")
        )
    elif "binary:" not in doc.fields["source file"].lower():
        bad.append(
            Violation(
                doc.rel,
                "Source File",
                "`Source File:` trailer does not name its `Binary:`",
            )
        )
    dated = _one_of(doc, ("last updated", "date"))
    if dated is None:
        bad.append(Violation(doc.rel, "Last Updated", "missing `Last updated:`"))
    elif not ISO_DATE_RE.search(dated[1]):
        bad.append(
            Violation(doc.rel, "Last Updated", f"`{dated[0]}:` has no ISO date")
        )
    return bad


CHECKS = {
    CLASS_GOVERNANCE: lambda d: check_prose(d, CLASS_GOVERNANCE),
    CLASS_INDEX: lambda d: check_prose(d, CLASS_INDEX),
    CLASS_FINDING: lambda d: check_prose(d, CLASS_FINDING),
    CLASS_WAVE_SHARD: check_wave_shard,
    CLASS_FUNCTION_NOTE: check_function_note,
}


# --------------------------------------------------------------------------
# corpus
# --------------------------------------------------------------------------


def excluded(rel: str) -> str | None:
    for prefix, why in EXCLUDED_PREFIXES:
        if rel.startswith(prefix):
            return why
    for path, why in EXCLUDED_PATHS:
        if rel == path:
            return why
    if "/bin/" in rel or "/obj/" in rel:
        return "build output"
    return None


def tracked_markdown(root: Path) -> list[str] | None:
    """Tracked markdown, plus untracked-but-not-ignored markdown.

    The second half matters: ``git ls-files`` alone lists the index, so a
    document that has been written but not yet ``git add``-ed would be invisible
    to the gate at exactly the moment a review is most useful. Adding
    ``--others --exclude-standard`` gates it the moment it exists, and still
    excludes ``local-lab/`` structurally -- that tree is gitignored, so
    ``--exclude-standard`` drops it without the checker needing to know why.
    """
    rels: set[str] = set()
    for args in (
        ["git", "ls-files", "*.md", "*.MD"],
        ["git", "ls-files", "--others", "--exclude-standard", "*.md", "*.MD"],
    ):
        try:
            result = subprocess.run(
                args, cwd=root, check=True, capture_output=True, text=True
            )
        except (subprocess.CalledProcessError, FileNotFoundError, OSError):
            return None
        rels.update(line.strip() for line in result.stdout.splitlines() if line.strip())
    return sorted(r for r in rels if excluded(r) is None and (root / r).is_file())


def load_backlog(path: Path) -> set[str] | None:
    if not path.is_file():
        return None
    out: set[str] = set()
    for line in path.read_text(encoding="utf-8").splitlines():
        body = line.split("#", 1)[0].strip()
        if body:
            out.add(body)
    return out


def write_backlog(path: Path, rels: list[str], counted: dict[str, int]) -> None:
    head = [
        "# Documentation-header backlog -- see DOCUMENTATION.md.",
        "#",
        "# Documents listed here predate the header standard. They are reported by",
        "# tools/doc_header_check.py but do not fail it. Anything NOT listed here is",
        "# enforced, so a newly added document is gated from the moment it exists.",
        "#",
        "# This list may only shrink. A stale entry -- a path that no longer exists,",
        "# or one that now conforms -- is itself a failure, so the backlog cannot",
        "# quietly become permanent. Delete a line when you bring its document up to",
        "# standard; never add one to silence a new failure.",
        "#",
        f"# Generated 2026-07-28. Entries: {len(rels)}.",
    ]
    for klass in sorted(counted):
        head.append(f"#   {klass}: {counted[klass]}")
    path.write_text("\n".join(head) + "\n" + "\n".join(rels) + "\n", encoding="utf-8")


# --------------------------------------------------------------------------
# run
# --------------------------------------------------------------------------


def run(
    root: Path,
    backlog_path: Path,
    show_backlog: bool,
    strict: bool,
) -> int:
    rels = tracked_markdown(root)
    if rels is None:
        print(
            "UNAVAILABLE: could not list tracked markdown (is this a git checkout?).\n"
            "             Abstaining rather than passing.",
            file=sys.stderr,
        )
        return 2
    if not rels:
        print("UNAVAILABLE: no tracked markdown in scope.", file=sys.stderr)
        return 2

    backlog = load_backlog(backlog_path)
    if backlog is None:
        print(
            f"UNAVAILABLE: backlog file not found: {backlog_path}\n"
            "             Regenerate it with --write-backlog, or pass --backlog.",
            file=sys.stderr,
        )
        return 2

    by_class: dict[str, int] = {}
    enforced_by_class: dict[str, int] = {}
    violations: list[Violation] = []
    deferred: list[str] = []
    now_conforming: list[str] = []

    for rel in rels:
        klass = classify(rel)
        by_class[klass] = by_class.get(klass, 0) + 1
        text = (root / rel).read_text(encoding="utf-8", errors="replace")
        doc = parse(rel, text)
        bad = CHECKS[klass](doc)
        if rel in backlog:
            if bad:
                deferred.append(rel)
            else:
                now_conforming.append(rel)
            continue
        enforced_by_class[klass] = enforced_by_class.get(klass, 0) + 1
        violations.extend(bad)

    stale = sorted(b for b in backlog if b not in set(rels))

    print(f"tracked markdown in scope : {len(rels)}")
    for klass in sorted(by_class):
        print(
            f"  {klass:<14} {by_class[klass]:>5}   enforced "
            f"{enforced_by_class.get(klass, 0):>5}"
        )
    print(f"backlog (pre-standard)    : {len(deferred)} deferred, {len(backlog)} listed")
    print(f"violations                : {len(violations)}")

    if violations:
        print("")
        print("VIOLATIONS")
        current = None
        for v in sorted(violations, key=lambda x: (x.rel, x.rule)):
            if v.rel != current:
                current = v.rel
                print(f"  {v.rel}  [{classify(v.rel)}]")
            print(f"      {v.rule}: {v.detail}")

    if now_conforming:
        print("")
        print(
            f"BACKLOG ENTRIES THAT NOW CONFORM ({len(now_conforming)}) -- "
            "delete these lines from the backlog file:"
        )
        for rel in now_conforming[:50]:
            print(f"  {rel}")
        if len(now_conforming) > 50:
            print(f"  ... ({len(now_conforming) - 50} more)")

    if stale:
        print("")
        print(f"STALE BACKLOG ENTRIES ({len(stale)}) -- path no longer tracked:")
        for rel in stale[:50]:
            print(f"  {rel}")
        if len(stale) > 50:
            print(f"  ... ({len(stale) - 50} more)")

    if show_backlog and deferred:
        print("")
        print(f"DEFERRED ({len(deferred)}):")
        for rel in deferred:
            print(f"  {rel}")

    failed = bool(violations) or bool(stale) or bool(now_conforming)
    if strict and deferred:
        failed = True

    if failed:
        print("")
        reasons = []
        if violations:
            reasons.append(f"{len(violations)} violation(s)")
        if stale:
            reasons.append(f"{len(stale)} stale backlog entry(ies)")
        if now_conforming:
            reasons.append(f"{len(now_conforming)} backlog entry(ies) that now conform")
        if strict and deferred:
            reasons.append(f"{len(deferred)} deferred document(s) under --strict")
        print(f"Documentation header check: FAIL ({'; '.join(reasons)})", file=sys.stderr)
        return 1

    print("")
    print("Documentation header check: PASS")
    return 0


# --------------------------------------------------------------------------
# self-test
# --------------------------------------------------------------------------

_GOOD_FINDING = """# The pine LOD distance is retail's authored 30.0, not 70.0

> Verdict: **the shipped image's own static initialiser writes `30.0`.**

Status: active -- supersedes the 70.0 reading
Date: 2026-07-28
Evidence: MEASURED -- three arms at `0x004DD6B0` write 10.0/30.0/70.0; the
image's initialisers are uniquely the middle arm.
Specimen: `BEA.exe.original.backup`, SHA-256 `74154bfa`, 2,506,752 bytes
(`local-lab/safe-copy-bea-pristine/`, read-only).

## Body

The dispatch at `0x004DD6B0` has one caller.
"""

_GOOD_INDEX = """# Some index

Status: active index
Last updated: 2026-07-28
Summary: front door for the thing.

## Entries
"""

_GOOD_SHARD = """# W001 adversarial B01 (vs primary A01)

- Phase: adversarial
- Function count: 25
- Reviewed at: 2026-07-23T19:35:02Z
- Primary: `reverse-engineering/.../W001/primary/A01.md`
- Export root: `local-lab/ghidra-fullpass-2026-07-23/exports/W001/`

## Summary
"""

_GOOD_FUNCTION = """# AsmInstruction.cpp Functions

> Source File: MissionScript/AsmInstruction.cpp | Binary: BEA.exe
> Debug Path: 0x0064c5c4
> Last updated: 2026-06-08

## Overview
"""


def _mutate(text: str, old: str, new: str) -> str:
    assert old in text, old
    return text.replace(old, new, 1)


def _self_test() -> int:
    finding_no_specimen = _mutate(
        _GOOD_FINDING,
        "Specimen: `BEA.exe.original.backup`, SHA-256 `74154bfa`, 2,506,752 bytes\n"
        "(`local-lab/safe-copy-bea-pristine/`, read-only).\n",
        "",
    )
    finding_live_install = _mutate(
        _GOOD_FINDING,
        "Specimen: `BEA.exe.original.backup`, SHA-256 `74154bfa`, 2,506,752 bytes\n"
        "(`local-lab/safe-copy-bea-pristine/`, read-only).",
        "Specimen: `BEA.exe`, SHA-256 `e1436ef7`\n"
        "(`C:/Steam/steamapps/common/Battle Engine Aquila/BEA.exe`).",
    )
    # Regression: an earlier rule exempted the whole field if it mentioned the
    # pristine backup anywhere, so naming both let the live install through.
    finding_both_binaries = _mutate(
        _GOOD_FINDING,
        "(`local-lab/safe-copy-bea-pristine/`, read-only).",
        "(`local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`), compared "
        "against `C:/Steam/steamapps/common/Battle Engine Aquila/BEA.exe`.",
    )

    cases: list[tuple[str, str, str, list[str]]] = [
        # (label, rel, text, expected rule names -- empty means it must pass)
        ("good finding passes", "reverse-engineering/x-2026-07-28.md", _GOOD_FINDING, []),
        ("good index passes", "reverse-engineering/_index.md", _GOOD_INDEX, []),
        (
            "good shard passes",
            FULLPASS_ROOT + "W001/adversarial/B01.md",
            _GOOD_SHARD,
            [],
        ),
        (
            "good function note passes",
            FUNCTIONS_ROOT + "AsmInstruction.cpp.md",
            _GOOD_FUNCTION,
            [],
        ),
        (
            "missing Status fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Status: active -- supersedes the 70.0 reading\n", ""),
            ["Status"],
        ),
        (
            "placeholder Status fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Status: active -- supersedes the 70.0 reading", "Status: TBD"),
            ["Status"],
        ),
        (
            "missing Date fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Date: 2026-07-28\n", ""),
            ["Date"],
        ),
        (
            "non-ISO Date fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Date: 2026-07-28", "Date: July 2026"),
            ["Date"],
        ),
        (
            "impossible Date fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Date: 2026-07-28", "Date: 2026-02-31"),
            ["Date"],
        ),
        (
            "missing Verdict fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(
                _GOOD_FINDING,
                "> Verdict: **the shipped image's own static initialiser writes `30.0`.**\n",
                "",
            ),
            ["Verdict"],
        ),
        (
            "missing Evidence fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(
                _GOOD_FINDING,
                "Evidence: MEASURED -- three arms at `0x004DD6B0` write 10.0/30.0/70.0; the\n"
                "image's initialisers are uniquely the middle arm.\n",
                "",
            ),
            ["Evidence"],
        ),
        (
            "ungraded Evidence fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, "Evidence: MEASURED -- three", "Evidence: reading the code, three"),
            ["Evidence"],
        ),
        (
            "bare Evidence grade with no evidence fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(
                _GOOD_FINDING,
                "Evidence: MEASURED -- three arms at `0x004DD6B0` write 10.0/30.0/70.0; the\n"
                "image's initialisers are uniquely the middle arm.",
                "Evidence: MEASURED",
            ),
            ["Evidence"],
        ),
        (
            "UNKNOWN is an accepted grade",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(
                _GOOD_FINDING,
                "Evidence: MEASURED -- three arms at `0x004DD6B0` write 10.0/30.0/70.0; the\n"
                "image's initialisers are uniquely the middle arm.",
                "Evidence: UNKNOWN -- a CDB stop at `0x004DD6B0` would settle it.",
            ),
            [],
        ),
        (
            "byte claim without Specimen fails",
            "reverse-engineering/x-2026-07-28.md",
            finding_no_specimen,
            ["Specimen"],
        ),
        (
            "Specimen citing the live Steam install fails",
            "reverse-engineering/x-2026-07-28.md",
            finding_live_install,
            ["Specimen"],
        ),
        (
            "naming the pristine backup does not license citing the live install",
            "reverse-engineering/x-2026-07-28.md",
            finding_both_binaries,
            ["Specimen"],
        ),
        (
            "Specimen without a hash fails",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(_GOOD_FINDING, ", SHA-256 `74154bfa`", ""),
            ["Specimen"],
        ),
        (
            "no byte claim means no Specimen needed",
            "reverse-engineering/x-2026-07-28.md",
            _mutate(
                finding_no_specimen, "The dispatch at `0x004DD6B0` has one caller.", "Prose only."
            ).replace("`0x004DD6B0`", "the dispatch"),
            [],
        ),
        (
            "index needs no Evidence",
            "reverse-engineering/_index.md",
            _GOOD_INDEX,
            [],
        ),
        (
            "shard missing Reviewed at fails",
            FULLPASS_ROOT + "W001/adversarial/B01.md",
            _mutate(_GOOD_SHARD, "- Reviewed at: 2026-07-23T19:35:02Z\n", ""),
            ["Reviewed At"],
        ),
        (
            "pre-standard shard without Primary is not gated",
            FULLPASS_ROOT + "W001/adversarial/B01.md",
            _mutate(_GOOD_SHARD, "- Primary: `reverse-engineering/.../W001/primary/A01.md`\n", ""),
            [],
        ),
        (
            "post-standard shard without Primary fails",
            FULLPASS_ROOT + "W019/adversarial/B01.md",
            _mutate(
                _mutate(_GOOD_SHARD, "- Primary: `reverse-engineering/.../W001/primary/A01.md`\n", ""),
                "2026-07-23T19:35:02Z",
                "2026-08-01T09:00:00Z",
            ),
            ["Primary"],
        ),
        (
            "post-standard shard without Export root fails",
            FULLPASS_ROOT + "W019/primary/A01.md",
            _mutate(
                _mutate(_GOOD_SHARD, "- Export root: `local-lab/ghidra-fullpass-2026-07-23/exports/W001/`\n", ""),
                "2026-07-23T19:35:02Z",
                "2026-08-01T09:00:00Z",
            ),
            ["Export Root"],
        ),
        (
            "post-standard shard with the full trailer passes",
            FULLPASS_ROOT + "W019/adversarial/B01.md",
            _mutate(_GOOD_SHARD, "2026-07-23T19:35:02Z", "2026-08-01T09:00:00Z"),
            [],
        ),
        (
            "function note missing trailer fails",
            FUNCTIONS_ROOT + "AsmInstruction.cpp.md",
            _mutate(_GOOD_FUNCTION, "> Source File: MissionScript/AsmInstruction.cpp | Binary: BEA.exe\n", ""),
            ["Source File"],
        ),
        (
            "function note trailer without a Binary fails",
            FUNCTIONS_ROOT + "AsmInstruction.cpp.md",
            _mutate(_GOOD_FUNCTION, " | Binary: BEA.exe", ""),
            ["Source File"],
        ),
        (
            "function note is exempt from the Specimen rule",
            FUNCTIONS_ROOT + "AsmInstruction.cpp.md",
            _GOOD_FUNCTION,
            [],
        ),
        (
            "governance needs no Evidence",
            "GOAL.md",
            "# The goal\n\n> Status: standing objective\n> Date: 2026-07-27\n> Summary: what is wanted.\n\n## Body\n",
            [],
        ),
    ]

    failures = 0
    for label, rel, text, expected in cases:
        doc = parse(rel, text)
        klass = classify(rel)
        got = sorted({v.rule for v in CHECKS[klass](doc)})
        want = sorted(set(expected))
        ok = got == want
        if not ok:
            failures += 1
        print(f"  [{'ok ' if ok else 'FAIL'}] {label}: expected {want}, got {got}")

    # classification
    class_cases = [
        ("GOAL.md", CLASS_GOVERNANCE),
        ("PROGRAM.md", CLASS_GOVERNANCE),
        ("rebuild/PROVENANCE.md", CLASS_GOVERNANCE),
        ("reverse-engineering/RE-INDEX.md", CLASS_INDEX),
        ("reverse-engineering/save-file/_index.md", CLASS_INDEX),
        ("tools/README.md", CLASS_INDEX),
        (FUNCTIONS_ROOT + "DXTrees.cpp.md", CLASS_FUNCTION_NOTE),
        (FUNCTIONS_ROOT + "_index.md", CLASS_INDEX),
        (FULLPASS_ROOT + "W003/primary/A02.md", CLASS_WAVE_SHARD),
        (FULLPASS_ROOT + "W003/wave-closeout.md", CLASS_FINDING),
        ("reverse-engineering/binary-analysis/terrain-third-light-2026-07-26.md", CLASS_FINDING),
    ]
    for rel, want_class in class_cases:
        got_class = classify(rel)
        ok = got_class == want_class
        if not ok:
            failures += 1
        print(f"  [{'ok ' if ok else 'FAIL'}] class {rel} -> {got_class} (want {want_class})")

    # scope
    for rel, should_exclude in (
        ("local-lab/NOTES.md", True),
        ("lore/faction.md", True),
        (".github/ISSUE_TEMPLATE/bug_report.md", True),
        ("CLAUDE.md", True),
        ("reverse-engineering/RE-INDEX.md", False),
    ):
        ok = (excluded(rel) is not None) == should_exclude
        if not ok:
            failures += 1
        print(f"  [{'ok ' if ok else 'FAIL'}] scope {rel} excluded={excluded(rel) is not None}")

    # end-to-end: missing backlog file abstains with exit 2
    with tempfile.TemporaryDirectory() as tmp:
        base = Path(tmp)
        code = run(base, base / "no-such-backlog.txt", False, False)
        ok = code == 2
        if not ok:
            failures += 1
        print(f"  [{'ok ' if ok else 'FAIL'}] non-repo abstains: expected exit 2, got {code}")

    if failures:
        print(f"\nSELF-TEST FAILED: {failures} case(s)", file=sys.stderr)
        return 1
    print(f"\nSELF-TEST PASS ({len(cases) + len(class_cases) + 6} cases)")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--root", type=Path, default=REPO_ROOT)
    parser.add_argument("--backlog", type=Path, default=BACKLOG_PATH)
    parser.add_argument(
        "--show-backlog", action="store_true", help="list the deferred documents"
    )
    parser.add_argument(
        "--strict",
        action="store_true",
        help="also fail on deferred pre-standard documents (the end state)",
    )
    parser.add_argument(
        "--write-backlog",
        action="store_true",
        help="regenerate the backlog from the current corpus (deliberate, rare)",
    )
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args(argv)

    if args.self_test:
        return _self_test()

    if args.write_backlog:
        rels = tracked_markdown(args.root)
        if rels is None:
            print("UNAVAILABLE: could not list tracked markdown.", file=sys.stderr)
            return 2
        failing: list[str] = []
        counted: dict[str, int] = {}
        for rel in rels:
            klass = classify(rel)
            doc = parse(rel, (args.root / rel).read_text(encoding="utf-8", errors="replace"))
            if CHECKS[klass](doc):
                failing.append(rel)
                counted[klass] = counted.get(klass, 0) + 1
        write_backlog(args.backlog, failing, counted)
        print(f"wrote {args.backlog} with {len(failing)} entries")
        for klass in sorted(counted):
            print(f"  {klass}: {counted[klass]}")
        return 0

    return run(args.root, args.backlog, args.show_backlog, args.strict)


if __name__ == "__main__":
    raise SystemExit(main())
