# Contributing

Status: active
Last updated: 2026-10-29
Summary: how to propose a change to the rebuild, the gate it has to pass, and the licence of contributions.

Thank you for your interest. Please read this before opening an issue or a pull request.

## How changes are made

Open an issue describing the problem or the change, or a pull request against `main`. Keep a pull request to one
subject, say what it changes in behaviour, and name the evidence: the Present where the rebuild first differs from the
reference, the capture or test that shows it, or the file and line of Stuart Gillam's GPL Onslaught project
([stuart73/Onslaught](https://github.com/stuart73/Onslaught), pinned in `references/Onslaught`) or of
[onslaught-decomp](https://github.com/dlprentice/onslaught-decomp) the change follows. A plausible name or a guess
about what the original did is not evidence.

To play the game rather than work on the rebuild, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce), and
report problems with it there.

## The gate

- **Simulation changes** (`rebuild/OnslaughtRebuild.Parity`) pass the affected Parity tests, then the whole suite
  (`dotnet test rebuild/OnslaughtRebuild.Parity.Tests`), with the reference variables set as
  [rebuild/README.md](rebuild/README.md) describes. A missing reference, an empty test selection or a new skip is not a
  pass. The parity core stays deterministic given its inputs ([rebuild/DETERMINISM.md](rebuild/DETERMINISM.md)): no
  wall time, locale, environment, threads, presentation, network or GPU.
- **Host changes** (`rebuild/OnslaughtRebuild.Host`) build (`dotnet build rebuild/OnslaughtRebuild.Host`) and keep the
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
