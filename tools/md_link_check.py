#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""
Repo-wide OFFLINE markdown link check.

Goal: catch broken *local* links (relative paths) early.

Notes:
- External links (http/https/mailto/etc.) are ignored.
- This is best-effort and intentionally conservative; it does not try to
  validate anchors (#...) inside documents.
"""

from __future__ import annotations

import argparse
import datetime as _dt
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, List, Tuple

REPO_ROOT = Path(__file__).resolve().parents[1]

LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")
PUBLIC_CORE_MARKDOWN = (
    "README.MD",
    "GOAL.md",
    "PROGRAM.md",
    "PROJECT-INDEX.md",
    "CURRENT_CAPABILITIES.md",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "VALIDATION.md",
    "lore/_index.md",
    "reverse-engineering/RE-INDEX.md",
    "rebuild/README.md",
    "rebuild/PROVENANCE.md",
)


def utc_now_iso() -> str:
    return _dt.datetime.now(tz=_dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def is_markdown_name(name: str) -> bool:
    """Markdown by extension, in any case: README.MD was once skipped because of its case."""
    return name.lower().endswith(".md")


def iter_filesystem_markdown_files() -> List[Path]:
    files: List[Path] = []
    for p in REPO_ROOT.rglob("*"):
        if not p.is_file() or not is_markdown_name(p.name) or not include_markdown_path(p):
            continue
        files.append(p)
    files.sort(key=lambda x: x.as_posix())
    return files


def include_markdown_path(path: Path) -> bool:
    rel = path.relative_to(REPO_ROOT).as_posix()
    if rel.startswith(".git/"):
        return False
    if rel.startswith(".venv/"):
        return False
    if rel.startswith(".pytest_cache/"):
        return False
    if rel.startswith("node_modules/"):
        return False
    if rel.startswith("release/artifacts/"):
        return False
    if rel.startswith(".artifacts/"):
        return False
    if rel.startswith("wave_"):
        return False
    if "/bin/" in rel or "/obj/" in rel:
        return False
    return True


def iter_markdown_files(scope: str = "all") -> List[Path]:
    if scope == "public-core":
        return sorted(
            [REPO_ROOT / rel for rel in PUBLIC_CORE_MARKDOWN if (REPO_ROOT / rel).is_file()],
            key=lambda x: x.as_posix(),
        )

    try:
        result = subprocess.run(
            ["git", "ls-files", "--", ":(icase)*.md"],
            cwd=REPO_ROOT,
            check=True,
            capture_output=True,
            text=True,
        )
        files = [REPO_ROOT / line for line in result.stdout.splitlines() if line.strip()]
        files = [p for p in files if p.is_file() and is_markdown_name(p.name) and include_markdown_path(p)]
        files.sort(key=lambda x: x.as_posix())
        if files:
            return files
    except (subprocess.CalledProcessError, FileNotFoundError):
        pass

    return iter_filesystem_markdown_files()


def strip_fenced_code(text: str) -> str:
    return re.sub(r"```.*?```", "", text, flags=re.S)


def inline_code_ranges(text: str) -> List[Tuple[int, int]]:
    """Ranges whose apparent ``[label](target)`` syntax is literal code."""
    return [(match.start(), match.end()) for match in re.finditer(r"`[^`\n]*`", text)]


def inside_any_range(position: int, ranges: List[Tuple[int, int]]) -> bool:
    return any(start <= position < end for start, end in ranges)


def is_external_target(t: str) -> bool:
    t_l = t.lower()
    if t_l.startswith("#"):
        return True
    if t_l.startswith("http://") or t_l.startswith("https://"):
        return True
    if t_l.startswith("mailto:") or t_l.startswith("tel:"):
        return True
    if "://" in t_l:
        return True
    return False


def normalize_target(t: str) -> str:
    t = t.strip()
    # strip optional surrounding <>
    if t.startswith("<") and t.endswith(">"):
        t = t[1:-1].strip()
    # drop anchor
    if "#" in t:
        t = t.split("#", 1)[0]
    # drop query
    if "?" in t:
        t = t.split("?", 1)[0]
    return t.strip()


def resolve_local_target(md_path: Path, target: str) -> Path:
    # Repo-root absolute links like "/reverse-engineering/..." are treated as repo-root relative.
    if target.startswith("/"):
        return (REPO_ROOT / target.lstrip("/")).resolve()
    return (md_path.parent / target).resolve()


def self_test() -> int:
    cases = [("README.MD", True), ("notes.md", True), ("Guide.Md", True), ("page.mdx", False), ("md", False)]
    failures = [name for name, expected in cases if is_markdown_name(name) != expected]
    tracked = [p.relative_to(REPO_ROOT).as_posix() for p in iter_markdown_files("all")]
    if (REPO_ROOT / "README.MD").is_file() and "README.MD" not in tracked:
        failures.append("README.MD is not scanned")
    print("md_link_check self-test: " + ("PASS" if not failures else f"FAIL {failures}"))
    return 0 if not failures else 1


def main() -> int:
    if "--self-test" in sys.argv[1:]:
        return self_test()
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--out-dir",
        default=".artifacts/md-link-check",
        help="Output dir (workspace-relative)",
    )
    ap.add_argument("--date", default=_dt.date.today().isoformat(), help="Date stamp (YYYY-MM-DD)")
    ap.add_argument(
        "--scope",
        choices=("all", "public-core"),
        default="all",
        help="Markdown file scope to scan.",
    )
    ap.add_argument(
        "--check-only",
        action="store_true",
        help="Validate without writing markdown/json report artifacts.",
    )
    args = ap.parse_args()

    md_files = iter_markdown_files(args.scope)
    broken: List[Dict[str, Any]] = []
    total_links = 0

    for p in md_files:
        rel = p.relative_to(REPO_ROOT).as_posix()
        try:
            text = p.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            text = p.read_text(encoding="latin-1")

        scan = strip_fenced_code(text)
        code_ranges = inline_code_ranges(scan)
        for m in LINK_RE.finditer(scan):
            # Keep links whose *label* uses backticks, such as
            # [`texture.cpp`](path), but ignore link-like text wholly inside a
            # code span, such as `vtable[0](1)`.
            if inside_any_range(m.start(), code_ranges):
                continue
            raw_target = m.group(1).strip()
            if is_external_target(raw_target):
                continue
            target = normalize_target(raw_target)
            if not target:
                continue
            total_links += 1
            resolved = resolve_local_target(p, target)
            if resolved.exists():
                continue
            broken.append(
                {
                    "path": rel,
                    "target": target,
                    "resolved": os.fspath(resolved),
                }
            )

    payload: Dict[str, Any] = {
        "generated_utc": utc_now_iso(),
        "scope": args.scope,
        "files_scanned": len(md_files),
        "total_local_links_checked": total_links,
        "broken_links": broken,
        "broken_count": len(broken),
    }

    out_json: Path | None = None
    if not args.check_only:
        out_dir = (REPO_ROOT / args.out_dir).resolve()
        out_dir.mkdir(parents=True, exist_ok=True)
        out_json = out_dir / f"md-link-check-{args.date}.json"
        out_md = out_dir / f"md-link-check-{args.date}.md"
        out_json.write_text(json.dumps(payload, indent=2, sort_keys=True), encoding="utf-8")

        lines: List[str] = []
        lines.append(f"# Markdown Link Check ({args.date})")
        lines.append("")
        lines.append(f"- Files scanned: `{len(md_files)}`")
        lines.append(f"- Local links checked: `{total_links}`")
        lines.append(f"- Broken links: `{len(broken)}`")
        # State the bound. A report that prints "0 broken" without naming the set
        # it scanned reads as a repo-wide result; it is not one. Gitignored trees
        # (notably local-lab/, ~470 markdown files) are structurally invisible
        # here, so this report must never be cited as evidence that their links
        # resolve. Added 2026-07-28.
        if args.scope == "public-core":
            scope_note = (
                "an explicit list of public-core documents only. Every other "
                "markdown file in the repository is **not** scanned"
            )
        else:
            scope_note = (
                "**git-tracked markdown only** (`git ls-files '*.md'`). Gitignored "
                "trees are structurally invisible to this check — including "
                "`local-lab/` — and `include_markdown_path()` additionally drops "
                "`.git/`, `.venv/`, `.pytest_cache/`, `node_modules/`, "
                "`release/artifacts/`, `.artifacts/`, `wave_*`, and any `/bin/` or "
                "`/obj/` path. A `0` above is a result about that set, not about "
                "the repository"
            )
        lines.append(f"- Scope: `{args.scope}` — {scope_note}.")
        lines.append("")
        if broken:
            lines.append("## Broken Links (Top 200)")
            lines.append("")
            for item in broken[:200]:
                lines.append(f"- `{item['path']}` -> `{item['target']}` (resolved `{item['resolved']}`)")
            if len(broken) > 200:
                lines.append(f"- ... (see `{out_json.relative_to(REPO_ROOT).as_posix()}`)")
        else:
            lines.append("No broken local links detected.")

        out_md.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(out_md.relative_to(REPO_ROOT).as_posix())
        print(out_json.relative_to(REPO_ROOT).as_posix())

    print(f"Markdown files scanned: {len(md_files)}")
    print(f"Local links checked: {total_links}")
    if broken:
        for item in broken[:25]:
            print(f"- {item['path']} -> {item['target']}")
        if len(broken) > 25:
            print(f"- ... ({len(broken) - 25} more)")
        print(f"Markdown link check: FAIL ({len(broken)} broken local links)")
        return 1
    print("Markdown link check: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
