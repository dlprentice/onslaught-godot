# onslaught-godot

Status: active proof of concept. Goal: startup to exit 1:1 with retail, level 100 playable to completion. Measured status, Present
bounds, the comparisons with the native port and with the original game, and the open gaps are in [rebuild/PARITY.md](rebuild/PARITY.md),
which records its required reference gate as incomplete. These comparisons do not establish audio, picture or whole-campaign parity.
Last updated: 2026-11-05 (the status points to the parity record; the Companion's move into Onslaught CE is named; wording corrections)
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
for regression tests; the fixture carries no project licence (`REUSE.toml`: `LicenseRef-retail-save-fixture`) and is not to be reused
outside this repository's tests. The parity core and the host read the game's data from your own installation of *Battle Engine
Aquila* for PC; the comparison references (recorded runs) are kept outside the repository and named by environment variables.

## Layout

The rebuild keeps its `rebuild/` folder from the repository it was split from: its tools, tests and documents name
their paths from that root (`rebuild/...`), and several find the repository root by it, so moving it would change
dozens of paths for no gain.

| Path | What |
| --- | --- |
| `rebuild/OnslaughtRebuild.Parity`, `.Parity.Tests` | the parity core and its suite |
| `rebuild/OnslaughtRebuild.Host` | the Godot host |
| `rebuild/OnslaughtRebuild.Core`, `.Client`, `.Godot`, `.Headless` | the earlier fixed-point rebuild, kept as reference only (Core and Client have test projects; Godot has in-scene checks; Headless has none) |
| `rebuild/tools/` | the rebuild's tools: the retail oracle, the asset materializer, captures |
| `rebuild/README.md`, `PARITY.md`, `PROVENANCE.md`, `DETERMINISM.md` | how to build and test, what is measured, where code comes from, the determinism contract |
| `references/Onslaught` | Stuart Gillam's GPL Onslaught project, pinned as a submodule |
| `tools/` | measurement and documentation tools the rebuild uses |
| `tests_shared/fixtures/` | the one tracked career save, a test baseline |

## Building and testing

**On Windows (x64), use WSL2 for these contributor checks.** Follow Microsoft's
[WSL installation guidance](https://learn.microsoft.com/en-us/windows/wsl/install). Enabling WSL, changing Windows
features or installing a distribution needs administrator rights and a restart; if the PC is not yours, get its
administrator's approval first. In a Windows terminal, run
`wsl.exe --list --verbose` and confirm that your Linux distribution shows `VERSION` 2, then open that distribution's
Linux terminal. Keep the checkout under its Linux home directory, such as `~/src/onslaught-godot`, following
[Microsoft's file-storage guidance](https://learn.microsoft.com/en-us/windows/wsl/filesystems#file-storage-and-performance-across-file-systems).
Run the setup, build and authored-test commands below inside that Linux terminal.

The public setup route targets Linux x86_64. Start with Python 3.11 or newer with `venv` and pip; the Python tools suite needs
Pillow. The npm shortcuts also need Node 26 and npm 11.19 or later in the 11.x series (`package.json`). The setup
command installs the pinned .NET 10.0.402 SDK and Godot 4.8 beta1 .NET into this checkout's ignored `local-data/`.
Your distribution must provide the native dependencies described in Microsoft's
[Linux installation guidance](https://learn.microsoft.com/en-us/dotnet/core/install/linux#manual-installation).

The repository and its pinned submodule use public HTTPS URLs. A contributor can clone them and work on authored
tests, tools, documentation and source without access to the maintainer's computer, private repositories or lab:

```sh
mkdir -p ~/src
cd ~/src
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

These produce Linux build and authored-test results, including when run under WSL2. Native Windows builds and
applications require their own native Windows acceptance; a WSL2 result does not establish that acceptance.

Setup downloads only the official public archives pinned by URL, licence and hash in
[`tools/rebuild_toolchain.json`](tools/rebuild_toolchain.json). Build and test restore packages from the installed
Godot bundle and `https://api.nuget.org/v3/index.json`. Their child-process home, .NET, NuGet, XDG and temporary state stays inside this
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
The tools suite also has retail-corpus checks that report skips when those corpora are absent.

`npm test` is the separate full Parity project, run through the terminal's `dotnet` command. Setup leaves the
terminal's environment unchanged. When you have configured the [reference inputs](rebuild/README.md#working-lane),
the public local SDK can supply that command from the repository root:

```sh
export DOTNET_ROOT="$PWD/local-data/toolchains/rebuild/dotnet"
export DOTNET_ROOT_X64="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version                         # the pinned 10.0.402 SDK
npm test
```

This full suite uses your configured reference, MSBuild and NuGet environment; it does not inherit the public
runner's isolated settings. Comparisons whose game or reference inputs are missing report skips. Report those
separately from passing authored tests. Before a simulation or Host change merges, a maintainer
must run the required reference-bearing gate on the proposed commit, as [CONTRIBUTING.md](CONTRIBUTING.md#the-gate)
describes. Missing references, an empty selection and new skips do not satisfy that gate.

Running the Host requires your own game installation. Optional film preparation additionally needs NumPy in the
Python environment (`python -m pip install numpy`) and FFmpeg with `ffprobe` on `PATH`.
[rebuild/README.md](rebuild/README.md#running-the-host) gives the local SDK/runtime environment, game and film paths,
preparation commands and run modes. Its [working lane](rebuild/README.md#working-lane) lists the maintainer's
reference inputs.

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
- [Onslaught-Career-Editor](https://github.com/dlprentice/Onslaught-Career-Editor): the Toolkit and Companion app
  (careers, saves and backups, settings, lore, content exploration and the catalogue, the editors, installation
  management); every Companion capability is moving into native Onslaught CE, and the separate Companion stays supported
  until each replacement is verified (see its PROGRAM.md).
- [Stuart Gillam’s GPL Onslaught project](https://github.com/stuart73/Onslaught).

## Long-term direction

The shared [project goals](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/GOAL.md) and [program](https://github.com/dlprentice/Onslaught-Career-Editor/blob/main/PROGRAM.md) cover later HD assets, level and mission creation, modding and multiplayer. In Onslaught CE, usable 3D map and mission authoring, mods and multiplayer are required work with their own status (the same goals and program); in this rebuild they follow a faithful baseline, and nothing here establishes that those features work in this rebuild.

For this rebuild's measured status and open gaps, see [Parity](rebuild/PARITY.md). The [audio notes](rebuild/OnslaughtRebuild.Host/AUDIO.md) describe the sound evidence and its limits, and [Determinism](rebuild/DETERMINISM.md) describes the simulation contract.
