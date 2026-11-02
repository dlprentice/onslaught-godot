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
Last updated: 2026-11-02 (public contributor setup and reference validation; 2026-10-29: the tracked game-generated save fixture named; earlier: the two bounded loss-route comparisons with original processes stated; the null POS and LIFE probes of the recorded win stated; the recorded win equals the original game's fl2a process after the lead-time sum-order correction; what is compared with retail and what with the native port, each route to its test's floor; the host's drawing comparison is in progress)
Summary: a C# reimplementation of *Battle Engine Aquila* (Lost Toys, 2003) on Godot 4.8 .NET. Its simulation is compared
Present by Present with recorded runs of the native port and selected original-game recordings, each within its stated
bounds, and on one recorded win with the original's own process to the end (PARITY M5).

onslaught-godot rebuilds *Battle Engine Aquila* in C# on Godot 4.8 .NET. It has two parts:

- **The parity core** (`rebuild/OnslaughtRebuild.Parity`): the game's simulation, designed to be deterministic given its inputs (a
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
| `references/Onslaught` | Stuart Gillam's GPL Onslaught project, pinned as a submodule |
| `tools/` | measurement and documentation tools the rebuild uses |
| `tests_shared/fixtures/` | the one tracked career save, a test baseline |

## Building and testing

The public setup route targets Linux x86_64. Start with Python 3.11 or newer with `venv` and pip; the Python tools suite needs
Pillow. The npm shortcuts also need Node 26 and npm 11.19 or later in the 11.x series (`package.json`). The setup
command installs the pinned .NET 10.0.402 SDK and Godot 4.8 beta1 .NET into this checkout's ignored `local-data/`.
Your distribution must provide the native dependencies described in Microsoft's
[Linux installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux#manual-installation).

The repository and its pinned submodule use public HTTPS URLs. A contributor can clone them and work on authored
tests, tools, documentation and source without access to the maintainer's computer, private repositories or lab:

```sh
git clone --recurse-submodules https://github.com/dlprentice/onslaught-godot.git
cd onslaught-godot
python3 -m venv local-data/venv
. local-data/venv/bin/activate
python -m pip install Pillow
python tools/public_build.py setup
python tools/public_build.py build
python tools/public_build.py test
npm run test:tools && npm run test:docs && npm run test:safety
```

Setup downloads only the official public archives pinned by URL, licence and hash in
[`tools/rebuild_toolchain.json`](tools/rebuild_toolchain.json). Build and test restore packages from the installed
Godot bundle and `https://api.nuget.org/v3/index.json`. Their .NET, NuGet, XDG and temporary state stays inside this
checkout; they do not use the maintainer's profiles, private scripts or local reference paths. Setup is explicit:
build and test fail if the pinned tools are unavailable instead of installing them automatically.

MSBuild's directory resolver prints `MSB4241` with an empty SDK version label on this build route. The installer
checks the exact pinned engine archive and SDK-package hashes before the runner uses that directory.

The default authored selection covers `CrtRandTests`, `LiveAudioOutputStartupTests` and `Flow.SaveGameTests` with no
game data or recorded references. It is a starter check; choose the affected authored tests for your change:

```sh
python tools/public_build.py test --filter FullyQualifiedName~OnslaughtRebuild.Parity.Tests.CrtRandTests
```

The runner rejects empty selections, skipped selected tests and failures, and prints its local log/report folder.
`npm run build`, `npm run test:authored` and `npm run setup:toolchain` are shortcuts for these Python commands.
The tools suite also has retail-corpus checks that report skips when those corpora are absent. Running
`npm test` executes the full Parity test project; comparisons whose game or reference inputs are missing report
skips. Report those separately from passing authored tests. Before a simulation or Host change merges, a maintainer
must run the required reference-bearing gate on the proposed commit, as [CONTRIBUTING.md](CONTRIBUTING.md#the-gate)
describes. Missing references, an empty selection and new skips do not satisfy that gate.

Running the Host requires your own game installation. Optional film preparation additionally needs NumPy in the
Python environment (`python -m pip install numpy`) and FFmpeg with `ffprobe` on `PATH`.
[rebuild/README.md](rebuild/README.md#running-the-host) gives the game and film paths, preparation commands and run
modes; its [working lane](rebuild/README.md#working-lane) lists the maintainer's reference inputs.

## Licence and credits

The code is free software under the GNU General Public License, version 3 only ([LICENSE](LICENSE),
[REUSE.toml](REUSE.toml)); [rebuild/PROVENANCE.md](rebuild/PROVENANCE.md) records which units adapt Stuart Gillam's
GPL Onslaught project ([stuart73/Onslaught](https://github.com/stuart73/Onslaught)). The original game is by Lost Toys.
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
- [Stuart Gillam’s GPL Onslaught project](https://github.com/stuart73/Onslaught).

## Long-term direction

The shared [project goals](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/GOAL.md) and [program](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/PROGRAM.md) cover later HD assets, level and mission creation, modding and multiplayer. These are longer-term aims across the project; they do not establish that those features work in this rebuild.

For this rebuild's measured status and open gaps, see [Parity](rebuild/PARITY.md). The [audio notes](rebuild/OnslaughtRebuild.Host/AUDIO.md) describe the sound evidence and its limits, and [Determinism](rebuild/DETERMINISM.md) describes the simulation contract.
