#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Fail if anything still promises the installed game is never changed.

The app can patch an installed game after an explicit choice and a verified
backup. Scan active product claims for contradictory blanket prohibitions.

What is banned is the ABSOLUTE form - a standing promise about the app or the installed
game. Describing what a particular flow does is fine and mostly still true: creating a
safe copy really does change nothing outside the copy, and the live trainer really does
never open an installed game. The difference is whether the sentence is about an action
or about the world.

Run:  python ./tools/check_installed_game_claims.py            list every hit
      python ./tools/check_installed_game_claims.py --check    non-zero if any is unexplained
      python ./tools/check_installed_game_claims.py --self-test
"""

from __future__ import annotations

import argparse
import io
import pathlib
import re
import subprocess
import sys


# Directories with their own evidence rules, or not shipped as product claims.
SKIP_PREFIXES = (
    "rebuild/",
    "reverse-engineering/",
    "lore/",
    "lore-book/",
    "lore-pack/",
    "local-lab/",
    ".artifacts/",
)

# .txt is here because the retired WinUI ZIP's user README was one, and it carried a
# stale prohibition for months BECAUSE this list did not include its extension. The
# guard was fine; its reach was not.
CHECKED_SUFFIXES = (".cs", ".xaml", ".md", ".MD", ".json", ".py", ".txt")

# Absolute promises. Each is a sentence about the world rather than about an action.
BANNED = [
    (r"installed game is never (?:changed|modified|touched)", "standing promise about the installed game"),
    (r"Steam install is never changed", "standing promise about the installed game"),
    (r"installed Steam executable is never changed", "standing promise about the installed game"),
    (r"only ever reads it", "standing promise that the app only reads"),
    (r"Never mutate an installed game", "prohibition the app no longer honours"),
    (r"installed game (?:remains|stays) read-only", "standing promise about the installed game"),
    (r"installed game(?: folder)? and (?:the )?original .*?(?:are|remain) read-only", "standing promise about installed and original files"),
    (r"No installed-game mutation", "app-wide claim in something that describes one profile"),
    (r"it does not edit that folder", "standing promise about the configured folder"),
    (r"(?:Never|Do not|Don't) patch an installed", "prohibition the app no longer honours"),
]

# Where the banned text is legitimate, and why. A file is only exempt for the reason given.
ALLOWED = {
    "OnslaughtCareerEditor.AppCore.Tests/GameProfilePreflightServiceTests.cs":
        "superseded note quotes the receipt limit it replaced",
    "tools/check_installed_game_claims.py": "this file lists the banned phrases",
}


def repo_root() -> pathlib.Path:
    here = pathlib.Path(__file__).resolve()
    for candidate in [here.parent.parent, *here.parents]:
        if (candidate / "package.json").is_file():
            return candidate
    raise SystemExit("Could not find the repository root.")


def tracked_files(root: pathlib.Path) -> list[str]:
    out = subprocess.run(
        ["git", "ls-files"], cwd=root, capture_output=True, text=True, check=False
    ).stdout.split()
    return [
        f for f in out
        if f.endswith(CHECKED_SUFFIXES) and not f.startswith(SKIP_PREFIXES)
    ]


def scan(root: pathlib.Path) -> list[tuple[str, int, str, str]]:
    hits: list[tuple[str, int, str, str]] = []
    patterns = [(re.compile(p, re.I), why) for p, why in BANNED]
    for name in tracked_files(root):
        path = root / name
        try:
            text = io.open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        for number, line in enumerate(text.splitlines(), 1):
            for pattern, why in patterns:
                if pattern.search(line):
                    hits.append((name, number, line.strip()[:160], why))
                    break
    return hits


def self_test() -> int:
    """The classifier's rules, on synthetic input."""
    patterns = [(re.compile(p, re.I), why) for p, why in BANNED]

    def flagged(line: str) -> bool:
        return any(p.search(line) for p, _ in patterns)

    cases = [
        ("Your installed game is never changed.", True),
        ("the app only ever reads it and works in a separate folder", True),
        ("- Never mutate an installed game or synthesize a save.", True),
        ("No installed-game mutation.", True),
        ("it does not edit that folder", True),
        ("- Never patch an installed `BEA.exe`.", True),
        ("Do not patch an installed Steam/Program Files BEA.exe in place.", True),
        ("The installed game and original `BEA.exe` are read-only.", True),
        ("The installed game folder and original `BEA.exe` remain read-only.", True),
        # These describe one action and stay true.
        ("Creating a safe copy changes nothing outside the safe copy.", False),
        ("Validation used disposable copies and preserved pristine inputs.", False),
        ("Your installed game is never opened.", False),
        ("The save you started from was not touched.", False),
        ("Your original executable is copied and checked before anything is written.", False),
    ]

    failures = 0
    for line, expected in cases:
        actual = flagged(line)
        ok = actual == expected
        failures += 0 if ok else 1
        verdict = "banned" if actual else "allowed"
        print(f"  {'PASS' if ok else 'FAIL'}  [{verdict}] {line[:64]}")

    print("SELF-TEST PASS" if failures == 0 else f"SELF-TEST FAIL ({failures})")
    return 0 if failures == 0 else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Fail on any unexplained claim.")
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    root = repo_root()
    hits = scan(root)
    unexplained = [h for h in hits if h[0] not in ALLOWED]

    if args.check:
        if unexplained:
            print("FAIL: the installed game is still promised to be untouchable:")
            for name, number, line, why in unexplained:
                print(f"  {name}:{number}  ({why})")
                print(f"      {line}")
            print()
            print("The app patches an installed game on request. A sentence that promises")
            print("otherwise is now false. Describe what a flow does, or add the file to")
            print("ALLOWED with the reason the wording is legitimate there.")
            return 1

        print(f"PASS: no unexplained claim. {len(hits)} allowed occurrence(s) in {len(ALLOWED)} known files.")
        return 0

    for name, number, line, why in hits:
        marker = "ALLOWED" if name in ALLOWED else "BANNED "
        print(f"{marker}  {name}:{number}  ({why})")
        print(f"          {line}")
    print(f"\n{len(hits)} occurrence(s); {len(unexplained)} unexplained.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
