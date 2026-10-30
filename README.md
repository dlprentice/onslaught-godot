# onslaught-godot

Status: active proof of concept. Goal: startup to exit 1:1 with retail, level 100 to completion. Measured: a whole-game
win route from boot through the outro, debriefing and quit equals the native port on every probe field every Present;
the saved career and options file match the port's byte for byte. Against the original game itself, one separately
recorded level-100 win (2026-10-28) equals one of the original's two debriefing outcomes (its process fl2a; 2 of the 8
original processes that reach the debriefing take that path) on every probe at all 12,312 Presents (on 2,477 of them,
frames with no player, the original's probes record POS and LIFE as null, which the comparison counts as all-zero), with
the saved careers and the options file byte for byte. That is a private harness run with the pristine game under the replay shim in Wine, not
a suite test; it admits a sound-pool seed and a goodie-load completion observed in port processes. The suite's own retail
references for the main level-100 run end at Present 10000. Two other recorded level-100 loss routes, one including Retry,
match selected probes and modelled battle-engine fields against two original processes per route, within each test's stated
Present bounds. These comparisons do not establish audio, picture or whole-campaign parity.
Last updated: 2026-10-29 (the tracked game-generated save fixture named; earlier: the two bounded loss-route comparisons with original processes stated; the null POS and LIFE probes of the recorded win stated; the recorded win equals the original game's fl2a process after the lead-time sum-order correction; what is compared with retail and what with the native port, each route to its test's floor; the host's drawing comparison is in progress)
Summary: a C# reimplementation of *Battle Engine Aquila* (Lost Toys, 2003) on Godot 4.8 .NET. Its simulation is compared
Present by Present with recorded runs of the native port and selected original-game recordings, each within its stated
bounds, and on one recorded win with the original's own process to the end (PARITY M5).

onslaught-godot rebuilds *Battle Engine Aquila* in C# on Godot 4.8 .NET. It has two parts:

- **The parity core** (`rebuild/OnslaughtRebuild.Parity`): the game's simulation, deterministic given its inputs (a
  replay tape, the retail data files and a virtual clock), compared at every Present with recorded runs of
  [Onslaught CE](https://github.com/dlprentice/onslaught-ce) (the native port) and selected recorded runs of the original
  game; [PARITY.md](rebuild/PARITY.md) states each comparison's fields and Present bounds.
- **The host** (`rebuild/OnslaughtRebuild.Host`): a Godot project, built in code, that runs the parity core and draws
  the game; its drawing is being compared with the native port's frames part by part (milestone M7, in progress).

It is not a playable release yet: [rebuild/PARITY.md](rebuild/PARITY.md) records what is measured and what is open.
To play the game, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce).

## You need your own copy of the game

The repository contains no game executable or game assets. It carries one [game-generated career fixture](tests_shared/fixtures/README.md)
for regression tests. The parity core and the host read the game's data from your own installation of *Battle Engine
Aquila* for PC; the comparison references (recorded runs) are kept outside the repository and named by environment variables.

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

You need the .NET 10 SDK (`global.json` pins 10.0.402) and Godot 4.8 beta1 .NET (the host pins `Godot.NET.Sdk/4.8.0-beta.1`). Clone with the
submodule (`git clone --recurse-submodules`), then:

```sh
dotnet test rebuild/OnslaughtRebuild.Parity.Tests       # the parity suite; set the reference variables first
dotnet build rebuild/OnslaughtRebuild.Host              # the host
npm run run:host                                        # build and run the host: the whole game, with your own game data
npm run test:tools && npm run test:docs && npm run test:safety
```

[rebuild/README.md](rebuild/README.md#running-the-host) says what the host needs (the engine, `ONSLAUGHT_GAME_DIR`)
and lists the reference variables, the host's run modes and the tools.

## Licence and credits

The code is free software under the GNU General Public License, version 3 only ([LICENSE](LICENSE),
[REUSE.toml](REUSE.toml)); [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md) records which units adapt Stuart Gillam's
GPL source release ([stuart73/Onslaught](https://github.com/stuart73/Onslaught)). The original game is by Lost Toys.
*Battle Engine Aquila* is a trademark of its owners; this is an unofficial project, not affiliated with or endorsed by
the game's developer, publisher or rights holders.

## Contributing and security

[CONTRIBUTING.md](CONTRIBUTING.md) gives the gate a change has to pass and the licence of contributions;
[SECURITY.md](SECURITY.md) says how to report a security problem privately.

## Related projects

- [Onslaught CE](https://github.com/dlprentice/onslaught-ce): the native port, to play the game.
- [onslaught-decomp](https://github.com/dlprentice/onslaught-decomp): the byte-matching decompilation the parity core is
  ported from.
- [onslaught-preservation](https://github.com/dlprentice/onslaught-preservation): the game's documentation (formats,
  mechanics, levels).
- [Onslaught-Career-Editor](https://github.com/dlprentice/Onslaught-Career-Editor): the companion app for saves,
  settings and lore (to be renamed onslaught-companion).
- [stuart73/Onslaught](https://github.com/stuart73/Onslaught): Stuart Gillam's GPL source release of the game.

## Long-term direction

The shared [project goals](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/GOAL.md) and [program](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/PROGRAM.md) cover later HD assets, level and mission creation, modding and multiplayer. These are longer-term aims across the project; they do not establish that those features work in this rebuild.

For this rebuild's measured status and open gaps, see [Parity](rebuild/PARITY.md). The [audio notes](rebuild/OnslaughtRebuild.Host/AUDIO.md) describe the sound evidence and its limits, and [Determinism](rebuild/DETERMINISM.md) describes the simulation contract.
