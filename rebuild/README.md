# Onslaught Rebuild

Status: active proof of concept — the Parity core and the Host; the earlier fixed-point rebuild is reference only
Last updated: 2026-11-11 (the earlier rebuild moved to EARLIER-REBUILD.md; header shortened)
Summary: how to build, test and run the Godot/C# rebuild of *Battle Engine Aquila*: its floating-point parity core and the Godot host, and where the earlier fixed-point rebuild, the evidence and the contracts are kept.

The rebuild's goal is startup to exit 1:1 with retail, with level 100 playable to completion. What is measured, and the
limits of each comparison, is in [PARITY.md](PARITY.md); this page does not repeat it. It is not a playable release: to
play the game, use [Onslaught CE](https://github.com/dlprentice/onslaught-ce).

Where to read next:

| You want | Read |
| --- | --- |
| to build, test and contribute | the [root README](../README.md#building-and-testing) and [CONTRIBUTING.md](../CONTRIBUTING.md) |
| to run the Host | [Running the Host](#running-the-host) |
| what is measured, what is open, the pass criteria | [PARITY.md](PARITY.md); the dated measurements are in [PARITY-LOG.md](PARITY-LOG.md) |
| the determinism contract | [DETERMINISM.md](DETERMINISM.md) |
| where each unit of code comes from, and the evidence boundary | [PROVENANCE.md](PROVENANCE.md) |
| the sound evidence and its limits | [OnslaughtRebuild.Host/AUDIO.md](OnslaughtRebuild.Host/AUDIO.md) |
| the earlier fixed-point rebuild (Core, Client, Godot, Headless) | [EARLIER-REBUILD.md](EARLIER-REBUILD.md); it cannot be built from a clean clone |

## Terms used in these documents

| Term | Meaning |
| --- | --- |
| retail | The original 2003 PC release of *Battle Engine Aquila*, the game the rebuild is measured against. |
| Steam install | The game's Steam installation on the user's machine. The Host reads it from `ONSLAUGHT_GAME_DIR`, else from the Linux Steam library's default folder; the film materializer takes it as `--game-dir`, the asset materializer as `--game-root`. |
| pristine specimen | An unmodified copy of the game's executable, kept outside this repository as the byte baseline. Each byte claim names its specimen with a SHA-256 prefix. |
| tape, probe, golden | A tape is a recorded sequence of player input that the parity core replays. A probe is a game-state field sampled at each Present. A golden is a recorded reference run (tape plus probes) that a test compares against. |
| oracle | The results of retail's own code run on captured inputs, used as a reference. |
| Present | The point at which the game hands a finished frame to the display. The parity core is compared with recorded runs at every Present. |
| Parity core | `OnslaughtRebuild.Parity`: the simulation. |
| Host | `OnslaughtRebuild.Host`: the Godot project that runs the parity core and draws the game. |
| Core, Client, Godot, Headless | The earlier fixed-point rebuild, kept as reference only ([EARLIER-REBUILD.md](EARLIER-REBUILD.md)). |
| Onslaught CE (CE) | The native Linux and Windows port of the game, published as [onslaught-ce](https://github.com/dlprentice/onslaught-ce). |
| PC24, PC53 | The x87 precision-control settings for a 24-bit and a 53-bit significand. The parity core's bounded matrix and pose arithmetic uses PC24; PC53 appears as a negative control in the tests. |
| M5, M7 | Milestones in [PARITY.md](PARITY.md). M5 covers the win, the loss, the end-level data and the career save; M7 is the Godot host running the parity core. |
| fl2a | The name of one original-game process in the recorded level-100 win comparison: one of the two debriefing outcomes the original game takes. |
| AYA | The game's packed resource-record format (`.aya`) for textures and meshes. The asset folders keep released records under their AYA names. |
| HFLD | The level's height-field data, a 64 × 64 tiled sample grid that the rebuild reads for ground height. |
| MAPT, MMAP | The released landscape records: MAPT holds the terrain texture sources, and MMAP the variable-length material and weight records. |
| CUnitAI | The original game's unit AI class, by its C++ name. The rebuild carries its close-target scoring kernel. |
| MSL | The `.msl` mission-script files, such as `LevelScript.msl`, materialized locally with the other inputs. |
| V12 | The label of the bounded production-device audio fixture recorded in [AUDIO.md](OnslaughtRebuild.Host/AUDIO.md). |
| Federation Aquila | The released battle-engine model that Level 100 draws; its walker, jet and cockpit meshes are local inputs. |

## Working lane

- The rebuild is developed in this repository (onslaught-godot) since 2026-10-22: work goes on a branch and merges into
  `main` when its gate passes. The copy in Onslaught-Career-Editor's `rebuild/` is retained as a frozen reference.
  Build, test and develop the live Parity core and Host in this repository.
- Builds, test runs and captures happen in a clone on an internal disk (or a worktree of it), never in a checkout on
  slow or removable storage. Your own clone and folder paths are kept in an ignored local file
  (`local-data/rebuild-env.sh`), not in the repository.
- Public source setup and an authored-test example are in the [root README](../README.md#building-and-testing).
  Windows contributors follow its WSL2 route, with the checkout inside the distribution's Linux filesystem;
  enabling WSL or installing a distribution requires the approval of the machine's owner (on your own computer, that is you). Those checks do not establish native Windows acceptance.
  `python tools/public_build.py setup`, `build` and `test` use a pinned public toolchain inside this checkout and
  require no machine wrapper, private profile or retail inputs. Contributors can prepare changes without the
  maintainer's private reference inputs. The
  [contribution gate](../CONTRIBUTING.md#the-gate) requires a maintainer to validate the proposed commit against those
  references before a simulation or Host change merges; authored tests alone do not establish parity.
- The Parity suite compares the simulation with recorded retail and native-port references, which live outside the
  repository. For that reference-bearing gate, set these variables to your actual datasets (for example in your own
  `local-data/rebuild-env.sh`, then `source` it), then run the suite and build the Host from the clone root:

| Variable | Names | Used by |
| --- | --- | --- |
| `ONSLAUGHT_PORT_REFS` | the folder of recorded native-port references (one subfolder per route, each with its `README.json` and hashes) | `RetailGoldenTests` (the native-port routes) |
| `ONSLAUGHT_PORT_CRT` | the native port's per-Present C runtime `rand` call records for level 100 | `RetailGoldenTests` |
| `ONSLAUGHT_PORT_LOGS` | the native port's draw logs of the level-100 load | tests marked `[PortLogFact]` |
| `ONSLAUGHT_RETAIL_ORACLE` | the retail oracle's view folder (results of retail's own code run on captured inputs) | `RetailOracleTests` |
| `ONSLAUGHT_RETAIL_GOLDEN` | the retail replay harness's golden recordings (tapes and probe traces at 640 x 480) | `RetailGoldenTests` |
| `ONSLAUGHT_RETAIL_MCLK2` | the original-game music-clock loss-route recordings, their starting options and captured saves | `OriginalLossRouteTests` |
| `ONSLAUGHT_GOLDEN_VDS` | the golden recordings' value dumps | `RetailGoldenTests` |
| `ONSLAUGHT_BEOBJ_VDS` | the battle engine's object dumps to Present 6000 | `RetailGoldenTests` |
| `ONSLAUGHT_PORT_OCCUPANCY` | the native port's level-100 occupancy data (`paths.txt`) | `OccupancyTests` |
| `ONSLAUGHT_PARITY_DATA` | the legacy replay-harness view (`golden`, `golden-640`, `tapes`) | `ClockTests`, `HarnessTests` |
| `X87_ORACLE_OUT` | the pinned compiler's generated x87 tables, including `manifest.json` | `X87OracleTests` |

```bash
source local-data/rebuild-env.sh          # the variables above, with your own folders
export DOTNET_ROOT="$PWD/local-data/toolchains/rebuild/dotnet"
export DOTNET_ROOT_X64="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version                         # the pinned 10.0.402 SDK installed by public setup
dotnet test rebuild/OnslaughtRebuild.Parity.Tests --logger 'console;verbosity=normal'
npm run build
npm run -s test:docs && npm run -s test:safety
```

Create `local-data/rebuild-env.sh` yourself with the paths of your reference datasets if you use that example; it
is not supplied by setup. The SDK exports select the public local installation. The full Parity command still uses
the caller's configured reference, MSBuild and NuGet environment; the public runner's isolated environment is
scoped to its own build/authored-test subprocesses. `npm test` runs that same full Parity project through `dotnet`
on the configured `PATH`. Tests whose game or recorded-reference inputs are absent report skips;
a missing reference, an empty selection or a new skip is not a pass for the required comparison. The references are
not in the repository; [PARITY.md](PARITY.md) records what each measurement covers and what remains open.

## Proof of concept (2026-10-15)

The maintainer reactivated this lane on 2026-10-15 as a scoped proof of concept: a C# Godot reimplementation that is 1:1 with
retail from startup to exit, with level 100 playable to completion and no other level. 1:1 is measured: retail replay
tapes played into the rebuild must give retail's probe values at every sample, flow screens must match retail
captures frame for frame, and the career saved after a win must be byte-identical to retail's. Milestones and pass
criteria are in [PARITY.md](PARITY.md#proof-of-concept-milestones).

The earlier Core ([EARLIER-REBUILD.md](EARLIER-REBUILD.md)) simulates in integer fixed point (millimetres, microradians) and cannot reproduce retail's
float state, so the parity path is a new assembly, `OnslaughtRebuild.Parity`: a floating-point C# reconstruction of the
decompilation's simulation, with x87 24-bit numerics modelled to the measured bounds in [PARITY.md](PARITY.md), independent of the earlier Core. `OnslaughtRebuild.Parity`
holds the retail tape harness today, run by the Parity suite (`npm run test:rebuild-parity`) against the references
named by the variables above (`ONSLAUGHT_RETAIL_GOLDEN` holds the retail replay harness's tapes and golden probe
traces). A route passes only when every probe matches at every sample with no frame offset. The earlier Headless
replay (`npm run ref:headless-parity -- --tape <retail tape> --golden <retail probes>`) is reference only.

### The front end's host contract

The flow (`OnslaughtRebuild.Parity.Flow`: `FrontEnd` and its pages) owns retail's front-end behaviour and leaves
presentation and the machine to its host (the Godot host, M7). The host supplies, on `FrontEnd`:

| Member | What it provides |
| --- | --- |
| the constructor's `PlatformClock` | the one Present counter and replay timer, shared with the main loop |
| `ReadDataFile` | the game's data files by retail path (today `data\WorldHeaders.dat`); null when missing |
| `MemoryCard` (an `ISaveFolder`) | the `savegames` folder: names in listing order, read, write, delete |
| `CurrentOptions` | the game's live options, written after the career and bindings in every save |
| `CockpitOf` | a Battle Engine configuration's cockpit mesh, from the owner of that data |
| `Crt` | the process's C runtime random state (one `CrtRand`, shared with the simulation) |
| `Video` (an `IFrontEndVideo`) | the front-end video player: which files exist, and opening one at a size |
| `EndLevelData`, `Settings` | the game's `END_LEVEL_DATA` and front-end settings, shared with the game |
| `VirtualKeyboardPage.Font` (an `IFontMetrics`) | text widths and glyphs, needed only to type a name |
| `GoodiesPage.StartMeshLoad` | optional diagnostic goodie-8 mesh and deterministic load-completion observation; no viewer drawing or manual control |

Drawing, sound and music are the host's. Each page's `Render` override holds only the state its retail render changes
(mouse hovers and clicks, scroll clamps). Mouse clicks that hit-test laid-out text (the main menu's items, the message
box's answers, the save list) wait for `IFontMetrics` and are not modelled yet.

### The Godot host (M7)

`OnslaughtRebuild.Host` is the successor host: a Godot 4.8 .NET project, built in code, that runs the parity core and
draws it as retail's renderer does. It replaces `OnslaughtRebuild.Godot`, which is **reference only**: it is not part of the Parity or Host gates and is not extended; the earlier launcher can still build and run it (see [EARLIER-REBUILD.md](EARLIER-REBUILD.md)), and it stays (with Core and Client's earlier simulation) because the asset materializer, the
first-flight launcher, the static-mesh preview and several measurement probes still read its sources and recipes;
pieces are copied into the Host only as needed, noting their origin. By default it renders one level-100
Present; an experimental live mode (`HOST_LIVE=1`) runs level 100 from the parity core with ordinary input, or
replays `HOST_TAPE` using its virtual sound device. `HOST_TRACE` records the live trace. The whole-game mode
(`HOST_LIVE=flow`) runs the game from boot to quit, front end and level, as [PARITY.md](PARITY.md) records (M7).

#### Running the Host

You need Godot 4.8 beta1 .NET and the .NET 10 SDK; `python tools/public_build.py setup` installs the pinned versions into
this checkout's ignored `local-data/`. If you install the engine yourself instead (the "Godot Engine - .NET" download of
4.8 beta1 from [godotengine.org](https://godotengine.org/download/archive/)), its identity must be
`4.8.beta1.mono.official.4d10419ca`, as pinned in [`tools/rebuild_toolchain.json`](../tools/rebuild_toolchain.json). You also need your own PC installation of *Battle Engine Aquila*. The Host reads the game's data files and saves
from that installation: it looks in `ONSLAUGHT_GAME_DIR`, else in the Linux Steam library's default folder
(`~/.local/share/Steam/steamapps/common/Battle Engine Aquila`). The Host reads film headers from that installation. `FilmLibrary` can read a library of decoded films at
`ONSLAUGHT_FILMS`, but no Host code calls `FilmLibrary.FromEnvironment`, so the film lines in the setup block below are optional preparation. Film preparation requires NumPy in the Python environment and FFmpeg with `ffprobe` on `PATH`.
On Linux, `npm run prepare:rebuild-assets` (and the scripts that call it) writes the materialised game data under the
ignored `rebuild-godot` folder of this checkout's `local-lab` directory (the checkout's canonical lab), not under
`local-data`.
Those game data and film assets are not included in the repository; its one tracked game-generated career fixture
is described in [`tests_shared/fixtures/README.md`](../tests_shared/fixtures/README.md).

From the repository root, with the Python environment from the [source setup](../README.md#building-and-testing)
active and its toolchain installed, select the local .NET SDK/runtime and engine, prepare films into ignored local
data and run the Host manually:

```bash
export DOTNET_ROOT="$PWD/local-data/toolchains/rebuild/dotnet"
export DOTNET_ROOT_X64="$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"
export GODOT="$PWD/local-data/toolchains/rebuild/engine/Godot_v4.8-beta1_mono_linux.x86_64"
export ONSLAUGHT_GAME_DIR="/path/to/Battle Engine Aquila"
export ONSLAUGHT_FILMS="$PWD/local-data/films"
python -m pip install numpy
python rebuild/tools/materialize_films.py "$ONSLAUGHT_FILMS" --game-dir "$ONSLAUGHT_GAME_DIR"
npm run run:host                                        # builds and runs the whole game (HOST_LIVE=flow)
```

`DOTNET_ROOT` and `DOTNET_ROOT_X64` make the local runtime available to the Godot .NET engine; `PATH` also selects
that SDK for direct `dotnet` commands in this terminal. These exports apply to the current terminal session.

The materializer's first argument is the output folder. It uses `--game-dir` for the installation and does not read
`ONSLAUGHT_GAME_DIR`; passing it explicitly also works for installations outside Steam's default library.

`run:host` opens the experimental whole-game preview with keyboard input. That `HOST_LIVE=flow` route does not
use the opt-in live audio adapter: a level can stop at an unsupported sound operation (the incoming-missile warning
is one reproduced case).
The whole-game preview does not yet admit physical mouse input; its career and options writes stay in memory and
do not survive closing the Host. At start, the Host copies the installation's `.bes` saves from its `savegames` folder into that memory card, unless `HOST_INSTALL_SAVES=0`. It is not yet a complete playable route or an audio acceptance result. Set `HOST_LIVE=1` for level 100
alone, or `HOST_LIVE=` (empty) to draw a single Present (`HOST_PRESENT=150`, optionally `HOST_CAPTURE=<file.png>`);
`python3 rebuild/OnslaughtRebuild.Host/tools/side_by_side.py <capture> <retail capture> <out.png>` compares a capture
with a retail one.

For the separate level-100 ordinary-input preview, `HOST_LIVE=1 HOST_AUDIO=1` opts into effects and prepared PCM
voices. It requires `ONSLAUGHT_VOICE_PCM`, a real Godot audio driver and supported sound options; it cannot be
combined with `HOST_TAPE`. [`OnslaughtRebuild.Host/AUDIO.md`](OnslaughtRebuild.Host/AUDIO.md) documents preparation,
output selection and limits. A direct production-device fixture captured nonzero PCM through a private null sink
and checked playback lifecycle. Audio during ordinary Host gameplay, physical-device and audible acceptance remain unverified;
this is not full audio parity or validation of the whole-game preview.

It draws the landscape (the lit caches ported from `CHeightField`: the whole map at 512 x 512 and retail's four
wrapping finer levels near the camera, each 8-metre tile's level chosen as CalcLODs does from the lagged view; the four
texture stages with retail's matrices and cloud drift; geometry at full resolution), the sky cube, and meshes with D3D's fixed-function lighting (the
ambient render state and the sun and anti-sun, no light ambient for meshes, `MODULATE2X`) and EXP table fog, all
blended in gamma space as retail does. Water is retail's fixed-function path (the GeForce FX shader path needs a
cardid.txt tweak retail never applies here): the camera-centred grid and the shores' surf strips from the level's SURF
chunk, with retail's stages, texture matrices and drift. Retail-derived textures and film headers are read from the installed game (the `FilmLibrary` reader of local film archives, `ONSLAUGHT_FILMS`, has no caller in the Host); nothing derived is committed. The battle engine is drawn part by part
at its motion controller's bones (CMCMech, in the parity core). Particles are the parity core's (the particle system is deterministic and shares the CRT rand stream with the simulation); the host draws their sprites as retail's RenderSprites builds them (the sun sprite among them). The HUD is drawn from the core's state: the compass ring and its bars, the
radar, the weapon and ammunition panels, the battle line (its map, or the message box's portrait while a message plays),
the objectives' screen markers, the eased crosshair, the guns panel and the target box (the target's mesh in its own
view), each part flashing while the mission script highlights it. In the whole-game mode the front end's pages and the level's 2D (the HUD's text, the message box, the pause and
end-level menus, the end screen) are drawn through the core's canvas, by Godot or by the software rasterizer used as the comparison
oracle (`HOST_SHOTS`, `HOST_SHOT_GODOT`). Not drawn yet: the landscape's geometry levels of detail, particle trails and
meshes, the lock-on pieces and warning-receiver threats (not
ported), and the front end's particle meshes and battle-engine model. The front end now draws its sprite particles
(main-menu fireworks and the debriefing's goodie glow/sparks) behind the cursor and subject to the page's depth masks.
The debriefing's animated glow was inspected in the admitted win replay; completed-campaign fireworks still need
a visible reference comparison. Diagnostics:
`HOST_TEXTURE_ONLY=1`, `HOST_DEBUG=lit|normal|colour|grey` and `HOST_NO_GRID=1` (the surf alone).

For recorded route playback only, `HOST_REPLAY_SOUND_PROFILE=<file>` admits the same measured sound-pool profile
used by the tutorial comparison tests. It requires `HOST_TAPE`, the exact admitted profile, one of the two recorded startup-options containers, and the matching English
sample bank; it does not enable sound output. The profile contains one native process's allocator state and must not
be used as an ordinary-player default. It is private reference data, not included here. With that explicit profile,
the recorded boot-to-debrief route completes in the Host. `HOST_FRONTEND_PARTICLES=0` disables only the new sprite
drawing for comparison. `HOST_FRONTEND_ONLY=1` is a tape-only capture diagnostic: it still executes the entire
simulation but omits level-world drawing. Neither diagnostic changes the tape or advances mission events.

For the separately observed CE 0.1.9 recording, `ReplaySoundProfile.CreateRecordedReplay` is an explicit test API:
it requires the pinned private census receipt, the exact tape and complete sidecar, the same pinned pool file and
English sample bank. It starts from the sidecar's options and careers in memory, with its tape fixed to the run;
the two ancillary start files remain unmodeled. The selected seeds describe that isolated replay process, not the
unobserved memory of the original recording or a general player default. The diagnostic caller must preserve the
returned run's initial state and callbacks and its game and film data; the API does not authenticate those assets.
This API does not enable live sound.

## Before implementation work

Read [PROVENANCE.md](PROVENANCE.md) before implementation work. Retail behavior claims must point to the smallest relevant binary/source/runtime evidence; Core agreement never re-proves retail.
