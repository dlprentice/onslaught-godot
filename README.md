# onslaught-godot

Status: active, a proof of concept: startup to exit 1:1 with retail, with level 100 playable to completion
Last updated: 2026-10-22 (the repository split from Onslaught-Career-Editor)
Summary: a C# reimplementation of *Battle Engine Aquila* (Lost Toys, 2003) on Godot 4.8 .NET, measured against the
original game Present by Present.

onslaught-godot rebuilds *Battle Engine Aquila* in C# on Godot 4.8 .NET. It has two parts:

- **The parity core** (`rebuild/OnslaughtRebuild.Parity`): the game's simulation, deterministic given its inputs (a
  replay tape, the retail data files and a virtual clock), compared with recorded runs of the original game and of
  [Onslaught CE](https://github.com/dlprentice/onslaught-ce) (the native port) at every Present.
- **The host** (`rebuild/OnslaughtRebuild.Host`): a Godot project, built in code, that runs the parity core and draws it
  as the original renderer does.

It is not a playable release yet: [rebuild/PARITY.md](rebuild/PARITY.md) records what is measured and what is open.
To play the game, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce).

## You need your own copy of the game

The repository contains none of the game's files. The parity core and the host read the game's data from your own
installation of *Battle Engine Aquila* for PC; the comparison references (recorded runs) are kept outside the
repository and named by environment variables.

## Layout

The rebuild keeps its `rebuild/` folder from the repository it was split from: its tools, tests and documents name
their paths from that root (`rebuild/...`), and several find the repository root by it, so moving it would change
dozens of paths for no gain.

| Path | What |
| --- | --- |
| `rebuild/OnslaughtRebuild.Parity`, `.Parity.Tests` | the parity core and its suite |
| `rebuild/OnslaughtRebuild.Host` | the Godot host |
| `rebuild/OnslaughtRebuild.Core`, `.Client`, `.Godot`, `.Headless` (and their tests) | the earlier fixed-point rebuild, kept as reference only |
| `rebuild/tools/` | the rebuild's tools: the retail oracle, the asset materializer, captures |
| `rebuild/README.md`, `PARITY.md`, `PROVENANCE.md`, `DETERMINISM.md` | how to build and test, what is measured, where code comes from, the determinism contract |
| `references/Onslaught` | Stuart Gillam's GPL source release of the game, pinned as a submodule |
| `tools/` | measurement and documentation tools the rebuild uses |
| `tests_shared/fixtures/` | the one tracked career save, a test baseline |

## Building and testing

You need the .NET 8 SDK and Godot 4.8 .NET (the host's SDK version is pinned in its project file). Clone with the
submodule (`git clone --recurse-submodules`), then:

```sh
dotnet test rebuild/OnslaughtRebuild.Parity.Tests       # the parity suite; set the reference variables first
dotnet build rebuild/OnslaughtRebuild.Host              # the host
npm run test:tools && npm run test:docs && npm run test:safety
```

[rebuild/README.md](rebuild/README.md) lists the reference variables, the host's run modes and the tools.

## Licence and credits

The code is free software under the GNU General Public License, version 3 only ([LICENSE](LICENSE),
[REUSE.toml](REUSE.toml)); [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md) records which units adapt Stuart Gillam's
GPL source release ([stuart73/Onslaught](https://github.com/stuart73/Onslaught)). The original game is by Lost Toys.
*Battle Engine Aquila* is a trademark of its owners; this is an unofficial project, not affiliated with or endorsed by
the game's developer, publisher or rights holders.

Related: [Onslaught CE](https://github.com/dlprentice/onslaught-ce) (the native port, to play the game) and
[Onslaught-Career-Editor](https://github.com/dlprentice/Onslaught-Career-Editor) (the companion app and the game's
documentation).
