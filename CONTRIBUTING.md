# Contributing

Status: active
Last updated: 2026-11-02
Summary: how to propose a change to the rebuild, the gate it has to pass, and the licence of contributions.

Thank you for your interest. Please read this before opening an issue or a pull request.

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
Work on this repository's Parity core and Host; the Toolkit repository's `rebuild/` remains a frozen reference copy.

Keep a pull request to one subject, say what it changes in behaviour, and name the evidence: the Present where the
rebuild first differs from the reference, the capture or test that shows it, or the file and line of Stuart Gillam's GPL Onslaught project
([stuart73/Onslaught](https://github.com/stuart73/Onslaught), pinned in `references/Onslaught`) or of
[onslaught-decomp](https://github.com/dlprentice/onslaught-decomp) the change follows. A plausible name or a guess
about what the original did is not evidence.

To play the game rather than work on the rebuild, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce), and
report problems with it there.

## The gate

Contributors run the affected authored tests and the applicable build, documentation, tools and safety checks below.
If you lack game or recorded-reference inputs, list the checks and test names that could not run in the pull request.
A maintainer must run the required reference-bearing checks on the proposed commit and record the results before
merge. Reference validation remains pending until then; passing authored tests does not replace it.

- **Simulation changes** (`rebuild/OnslaughtRebuild.Parity`) must pass the affected Parity tests, then the whole suite
  (`dotnet test rebuild/OnslaughtRebuild.Parity.Tests`), with the reference variables set as
  [rebuild/README.md](rebuild/README.md) describes. A missing reference, an empty test selection or a new skip is not a
  pass. The parity core stays deterministic given its inputs ([rebuild/DETERMINISM.md](rebuild/DETERMINISM.md)): no
  wall time, locale, environment, threads, presentation, network or GPU.
- **Host changes** (`rebuild/OnslaughtRebuild.Host`) must build (`dotnet build rebuild/OnslaughtRebuild.Host`) and keep the
  Parity suite passing.
- **Documentation and tools** pass `npm run test:docs`, and `npm run test:tools` for tool changes.
- **New files** pass `npm run test:safety`, which checks that no game material is tracked.
- C# only, built in code: no `.gd` files and no editor-authored scenes or resources. Units that adapt Stuart Gillam's
  source are recorded in [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md).

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
