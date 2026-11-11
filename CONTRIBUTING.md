# Contributing

Status: active
Last updated: 2026-11-11 (where things live and where to start added; the earlier lane's clean-clone limit stated)
Summary: where each kind of work belongs, how to propose a change to the rebuild, the gate it has to pass, and the licence of contributions.

Thank you for your interest. Please read this before opening an issue or a pull request.

## Where to contribute

This repository takes work on the Godot rebuild: the parity core, the Host, their tests and tools. Everyone contributes
by fork and pull request; no invitation, collaborator access or private repository is needed, and the maintainer
reviews and merges pull requests here (no response time is promised). The short map:

- Playing Onslaught CE, its bugs, Linux and Windows issues, the launcher and updates (Internet multiplayer is planned,
  not in this version yet):
  [onslaught-ce](https://github.com/dlprentice/onslaught-ce) (generated; open issues and pull requests there as usual).
- The byte-matching decompilation (functions, tools, matching notes):
  [onslaught-decomp](https://github.com/dlprentice/onslaught-decomp) (generated; matching work needs your own copy of the
  game plus the compiler and SDK inputs its guide names).
- How the game works (file formats, saves, mechanics, levels, engine):
  [onslaught-preservation](https://github.com/dlprentice/onslaught-preservation).
- The Companion (saves, backups, settings, lore, music) and the program's goals and plan:
  [Onslaught-Career-Editor](https://github.com/dlprentice/Onslaught-Career-Editor). Its `rebuild/` tree is a frozen copy
  of this repository's `rebuild/`: rebuild work comes here.
- The [Onslaught](https://github.com/dlprentice/Onslaught) and
  [AYAResourceExtractor](https://github.com/dlprentice/AYAResourceExtractor) forks are pinned references, not
  contribution targets.

The full map, with how each repository takes pull requests, is
[Where to contribute](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/CONTRIBUTING.md#where-to-contribute).
Questions and ideas: https://github.com/dlprentice/onslaught-ce/discussions.
Security problems go through [SECURITY.md](SECURITY.md); [REUSE.toml](REUSE.toml) says which licence covers which files.

## Where things live

- [README.md](README.md): what the project is, the layout, and how to build and test (the public route).
- [rebuild/README.md](rebuild/README.md): the glossary, the reference environment and running the Host.
- [rebuild/PARITY.md](rebuild/PARITY.md): what is measured and what is open; [rebuild/PARITY-LOG.md](rebuild/PARITY-LOG.md) holds
  the dated measurements.
- [rebuild/DETERMINISM.md](rebuild/DETERMINISM.md) and [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md): the simulation contract,
  and where each unit of code comes from.
- [rebuild/EARLIER-REBUILD.md](rebuild/EARLIER-REBUILD.md): the earlier fixed-point rebuild (`OnslaughtRebuild.Core`, `Client`,
  `Godot`, `Headless`). It is reference only and cannot be built from a clean clone, so a change to the Parity core or the Host
  never needs it. Do not build the two `.slnx` files in `rebuild/`: both include it.

## Where to start

A first pull request that needs no game and no recordings can fix a stale or unclear sentence in the documents above (run the
Markdown checks below), add an authored test for a Parity unit, or improve a tool in `tools/` with its `*_tests.py`. For a
change to behaviour, open an issue first so the evidence it needs is agreed before you write code.

## Your first hour

There is no hosted CI, so the checks below are yours to run and report. What you need depends on what you change:

| You are changing | You need | Run before the pull request |
| --- | --- | --- |
| Text or links (Markdown) | Git and Python 3.11 or newer; no game, Godot or .NET | `git diff --check`, `python3 tools/md_link_check.py --check-only`, `python3 tools/doc_header_check.py` |
| Python tools (`tools/`, `rebuild/tools/`) | The above plus Pillow (`python -m pip install Pillow`) | `npm run test:tools`, or the affected `*_tests.py` directly |
| Parity core or Host (C#) | Linux x86_64 or WSL2, then `python tools/public_build.py setup`; no game for the authored tests | `python tools/public_build.py build` and `python tools/public_build.py test --filter ...` for the affected tests |
| Anything that runs the game or compares with recordings | Your own copy of *Battle Engine Aquila*; the recorded references stay with the maintainers | List the checks you could not run in the pull request |
| Any new file | Nothing more | `python3 tools/public_allowlist_safety_check.py --include-submodules` |

To choose Parity tests for a C# change, look in `rebuild/OnslaughtRebuild.Parity.Tests/` for the test classes of the area you
change and pass their names to `--filter`. The default selection runs three classes only. A test that needs game data or a
recorded reference reports a skip, which is not a pass; check the run's report for skips, and list them in the pull request.
Which classes run fully without references is not recorded in one place yet.

The `npm run` forms of the documentation and safety checks (`npm run test:docs`, `npm run test:safety`) need Node 26
and are equivalent; the [README](README.md#building-and-testing) has the full setup.

A new or edited Markdown file starts, after its title, with `Status:`, `Last updated:` (or `Date:`) and `Summary:` lines;
a finding that quotes a retail address also carries `Evidence:` and a `Specimen:` line naming the file and the start of
its SHA-256. `tools/doc_header_check.py` enforces this. Commit messages describe the change itself; leave out session or
chat links, private paths and anything copied from the game.

### Terms you will meet

Present, tape, probe, golden, parity core, Host, the Toolkit and the other terms the documents use are defined once, in the
[glossary](rebuild/README.md#terms-used-in-these-documents).

## How changes are made

Open an issue describing the problem or the change, or fork this public repository and open a pull request against
`dlprentice/onslaught-godot`'s `main`. Clone your fork with its public submodule and make a branch:

```sh
git clone --recurse-submodules https://github.com/<your-account>/onslaught-godot.git
cd onslaught-godot
git switch -c my-change
```

Follow the [public source setup](README.md#building-and-testing), commit only the files for your change, push the
branch to your fork, and open the pull request. People and automated agents use the same workflow; upstream write
access, private repositories, the maintainer's computer and its local lab are not needed to prepare a contribution.
Windows contributors use the WSL2 route in the [README](README.md#building-and-testing); WSL2 checks do not establish
native Windows acceptance. Work on this repository's Parity core and Host; the Toolkit repository's `rebuild/` remains a
frozen reference copy.

Keep a pull request to one subject, say what it changes in behaviour, and name the evidence: the Present where the
rebuild first differs from the reference, the capture or test that shows it, or the file and line of Stuart Gillam's GPL Onslaught project
([stuart73/Onslaught](https://github.com/stuart73/Onslaught), pinned in `references/Onslaught`) or of
[onslaught-decomp](https://github.com/dlprentice/onslaught-decomp) the change follows. A plausible name or a guess
about what the original did is not evidence.

To play the game rather than work on the rebuild, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce), and
report problems with it there.

## The gate

Contributors run the affected authored tests and the applicable build, documentation, tools and safety checks below.
The public [setup and runner](README.md#building-and-testing) build the live Host and run an authored selection
without private inputs. Use `python tools/public_build.py test --filter ...` for the affected authored tests; its
default selection is a starter check, not the full suite.
If you lack game or recorded-reference inputs, list the checks and test names that could not run in the pull request.
A maintainer must run the required reference-bearing checks on the proposed commit and record the results before
merge. Reference validation remains pending until then; passing authored tests does not replace it.

- **Simulation changes** (`rebuild/OnslaughtRebuild.Parity`) must pass the affected Parity tests, then the whole suite
  (`dotnet test rebuild/OnslaughtRebuild.Parity.Tests`), with the reference variables set as
  [rebuild/README.md](rebuild/README.md) describes. A missing reference, an empty test selection or a new skip is not a
  pass. The parity core stays deterministic given its inputs ([rebuild/DETERMINISM.md](rebuild/DETERMINISM.md)): no
  wall time, locale, environment, threads, presentation, network or GPU.
- **Host changes** (`rebuild/OnslaughtRebuild.Host`) must build (`npm run build`) and keep the
  Parity suite passing.
- **Documentation and tools** pass `npm run test:docs`, and `npm run test:tools` for tool changes.
- **New files** pass `npm run test:safety`, which checks that no game material is tracked.
- C# only, built in code: no `.gd` files and no editor-authored scenes or resources. Units that adapt Stuart Gillam's
  source are recorded in [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md).

### What you can and cannot reproduce

- **Anyone**: the authored Parity tests, the Host build, and the documentation, tools and safety checks. They need no game
  and no recordings.
- **With your own copy of the game**: running the Host, preparing its assets and films, and any check that reads the
  game's data files.
- **Maintainers only**: the reference-bearing checks. The recorded runs they compare with are kept outside this
  repository and are not available to contributors; some claims in [PARITY.md](rebuild/PARITY.md) and
  [PROVENANCE.md](rebuild/PROVENANCE.md) rest on private evidence, and each says so where it does. A maintainer runs
  those checks on the proposed commit before a simulation or Host change merges.

When you report a difference from the original game without recordings, give the commit, command, level and what you saw;
the Present where it first differs is welcome if you can measure it, and not required.

## Licence of contributions

Inbound = outbound: by contributing you agree that your contribution is licensed under the GNU General Public License,
version 3 only, like the rest of this repository ([LICENSE](LICENSE), [REUSE.toml](REUSE.toml)), and that you have
the right to license it so.

## Never include game files

Never attach or commit any of the game's files or anything derived from them: executables, data, text dumps, saves
(beyond the one tracked test fixture), captures, byte dumps, decompiler output or instruction listings. Everyone uses
their own copy of the game; this repository contains none of it and will not accept it.

## Evidence and references

[Parity](rebuild/PARITY.md) records measured behavior and open differences; [Audio](rebuild/OnslaughtRebuild.Host/AUDIO.md) records the sound-specific evidence and limits. [Determinism](rebuild/DETERMINISM.md) is the simulation contract.

When reporting checks, distinguish authored tests from comparisons that require local reference data. Say which reference-bearing checks ran and which were skipped; a passing authored test does not establish a result for an unavailable reference. See the [rebuild guide](rebuild/README.md) for setup.
