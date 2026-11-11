# Rebuild parity contract

Status: active — proof-of-concept milestones; the milestone and status tables grade the Parity core
Last updated: 2026-11-11 (dated entries moved to PARITY-LOG.md; the earlier lane's contract table moved to PARITY-EARLIER-LANE.md)
Evidence: SOURCE — the authority order and the known divergences are recorded in `PROVENANCE.md` and in the earlier
lane's contract table ([PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md)); gate capabilities are MEASURED claims of the
tracked harnesses named in the dimensions table.
Specimen: `BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`
(the retail addresses cited in the divergences table are from the preserved
pristine specimen; do not infer another executable's identity from its folder name).
Summary: the gradeable dimensions of parity, the gate each currently has (or
lacks), and the standing exceptions. This document names the gap; it does not
claim the gap is closed.

Terms used in this file (Present, tape, probe, golden, frame, falsifier, RED/restored/GREEN, Stuart, the Toolkit and others) are defined in the [glossary](README.md#terms-used-in-these-documents).

Claims that cite *private evidence* or the maintainers' receipts cannot be re-run from a clone; tests named in the tables run from a clone only when they need no reference data ([CONTRIBUTING.md](../CONTRIBUTING.md#what-you-can-and-cannot-reproduce)).

Where the pages are: this page holds the current state, the milestones' pass criteria, the authority order, the known
divergences and the gates. [PARITY-LOG.md](PARITY-LOG.md) holds the dated measurements behind the milestone table.
[PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md) holds the earlier fixed-point rebuild's contract table.

## Where it stands

Milestone status as of 2026-10-29. The measurements behind each row are dated entries in [PARITY-LOG.md](PARITY-LOG.md);
the milestones and their pass criteria are in [Proof-of-concept milestones](#proof-of-concept-milestones).

**The required reference gate is incomplete.** In the latest dated run (the first entry of the
[log](PARITY-LOG.md#latest-entries-2026-11-02-to-2026-11-04)) three cases were skipped: two occupancy-reference checks,
whose references are unavailable, and one sample decoder/converter oracle check, whose oracle results are unavailable.
A missing reference, an empty selection and a new skip are not a pass.

| # | Status |
| --- | --- |
| M0 | Met (2026-10-15): the harness parses retail's level-100 tape and golden trace and reproduces its change counts. |
| M1 | Met for every operation measured, with one measured ceiling: `fsin`, `fcos` and `fpatan` stored to a double differ in the last bits for some arguments (the M1 entry in the [log](PARITY-LOG.md)). A float product with a subnormal result rounds twice, in the register and at the store: `X87.Mul` followed by the store's conversion matches the oracle on 1,000,000 such products, and C#'s float multiply does not (the 2026-10-29 subnormal-store entry in the log). |
| M2-M4 | Met on the keyboard-and-mouse tape: the sampled probes (EVT, FT, RNG, SC, LVL, POS, LIFE) equal retail's virtual-DirectSound golden through Present 10000 (`RetailGoldenTests.Level100RunMatchesTheVirtualDsGolden`). |
| M5 | Win route met against the original game's fl2a process (one of the two debriefing outcomes the original takes; 2 of the 8 original processes that reach the debriefing, the pristine game under the replay shim in Wine) on one separately recorded level-100 win (measured 2026-10-29; a private harness run, not a suite test); two recorded loss routes now match both original music-clock processes on the bounded measurements in the log; otherwise met against the native port on two win routes (2026-10-23). The gate below asks for retail's career after the win. On the recorded win (a player's own game from boot, recorded 2026-10-28) the rebuild equals that original process on EVT, EVN, FT, SC, LVL, RNG, POS and LIFE at all 12,312 of its Presents, on the front end's line, on both END_LEVEL_DATA records, and on the saved careers and the options file byte for byte; it also equals the native port with its patch 0111 on every field the port records (the 2026-10-29 entry in the log). The original is not repeatable on this tape: its debriefing reads an uninitialised slot, so the first Enter is accepted in 6 of 8 original processes and ignored in the other 2, and the rebuild equals the ignored path only. The diagnostic run uses that replay's separately observed sound-pool seed and one instrumented process's goodie-load completion, both observed in port processes, not in the original. Selected battle-engine and walker fields were also compared on the win; its other object words, audio and pictures remain unverified against the original game. A player's own game from boot (`GoldOwnRouteTests`: the boot films skipped, a new career, the goodies page, level 100 from the front end with its intro skipped, the win, the outro, the debriefing, the main menu's Quit) equals the port on every probe field every Present to the tape's end, and the saved career and the options file equal the port's byte for byte. The tutorial pilot's -level win route equals the port through the level's end, its end-level data and the outro (`PilotFinalDronesRouteTests`). |
| M6 | In progress: the boot films and the front end's page changes land on retail's Presents (the M6 entries in the log); the front end's float expressions still need a per-function oracle. |
| M7 | In progress: the Host runs the whole game from boot to quit (`HOST_LIVE=flow`), and its drawing is compared with the port's back buffers part by part (the dated entries in the log). |

## Proof-of-concept milestones

The target is retail under the retail replay harness's shim: `BEA.exe -level 100 -skipfmv -forcewindowed -res 640 480`
in Wine with a virtual clock of 1/30 s per Present (QueryPerformanceFrequency 10 MHz, 333333 counts per Present from
the first Present), tapes indexed by Present number, probes read every 50 Presents. Present counts are defined only
under that clock. A comparison that matches only under a non-zero frame offset is never reported as 1:1.

| # | Deliverable | Pass criterion |
| --- | --- | --- |
| M0 | Retail tape harness: tape and golden parsers, byte comparator, Present-driven runner, `parity` command | Real level-100 tape (4,896 events) and golden (200 samples × 7 probes) parse; retail's change counts reproduce the harness report; comparator and runner order pinned by tests |
| M1 | Exact x87 numerics (24-bit precision control): mixed float/double arithmetic, conversions, transcendentals | 0 mismatches against oracle tables from a VC6 `/O2 /QIfist` harness under Wine at `_PC_24`, at least 10^6 random inputs per operation plus edge cases |
| M2 | Clock and main loop: virtual clock, render-repeat loop, base-time adjustment, event-manager time, CGame frame time | EVT, FT and LVL equal the golden at every sample of the level-100 run |
| M3 | The game's random stream and level-start draws | RNG equal at the level-start sample |
| M4 | Level-100 parity core ported from the decompilation (onslaught-decomp) | EVT, FT, SC, LVL, RNG, POS and LIFE equal at all 200 samples over 10,000 frames on the keyboard-and-mouse tape |
| M5 | Win and loss, end-level data and the career save | Probes equal through the win; the career after the win byte-identical to retail's from the same starting career |
| M6 | Flow from boot to quit, Present-exact | Per screen, the same Present count as retail; captures at the same frame and resolution compared with retail's, every difference listed with its cause |
| M7 | The Godot host on the parity core | The same probes through the host as headless; capture comparison as in M6 |

## Porting notes for the parity core

- The world's Z axis points down: heights are negative upward (actor.cpp's altitude is `MAP.Collide(pos) -
  COfGHeight()`, src/AirGuide.cpp in onslaught-decomp has the ground as `-MAP.GetHeight(x, y)`), so a ground normal pointing up has negative Z
  (`HeightField.Normal`; off the map it returns retail's (0, 0, 1)).
- Float expressions keep the decompilation's association order. With float operands at 24-bit precision control,
  C# float arithmetic in the same order gives retail's bits; double operands go through `X87`.
- The C runtime's `rand()` does not reach the probes in level 100: retail's callers over the 10,000-Present
  golden-640 run (a detour on `rand`, 348,459 calls) are the particle descriptors (render-paced, about 342,000; roughly 2,300 of the calls have no caller named here),
  `CSnow::Init` at load (4,100) and `CSoundManager::PlayEffect` (26, sound parameters only). `CrtRand` must still be
  exact, in per-Present order, for render captures and sound. This holds per level: rerun the detour on each new
  level before relying on it.
- The mesh port (`rebuild/OnslaughtRebuild.Parity/Mesh/`) covers the resource-file reader (`CMesh::Deserialize`, the level's packed `m_*.aya` mesh
  files), not `CMesh::InitFromMem` (loose `.msh` files, versions 2.01-2.07, with its inline quaternion helper). A
  complete level resource file never takes that path; if a later milestone needs it, check its quaternion helper's
  compiled evaluation on the real FPU first.
- Loading-screen Present counts are observed constants from the retail runs (the project's rule, 2026-10-15): the Present
  numbers of screen boundaries repeat between retail runs while the timer reads per loading Present differ, and every
  Present comes from the frame flip (0x00512644) with no spin escape (the harness's logs). What paces the loading screen is an
  open question; exact modelling starts at the first front-end Process or the first MainLoop. No draw comes from the
  loading screen, so the RNG is unaffected.
- `.aya` positions such as the world's resource offset are positions in the decompressed stream (a packed file
  cannot seek, so `InitFromFile`'s start skip becomes a `Skip` after the first block).

## The earlier rebuild

The earlier fixed-point rebuild (`OnslaughtRebuild.Core`, `Client`, `Godot`, `Headless`) is reference only. Its grades
are not reproduced here: they describe its parking point (the old tag archive/godot-rebuild-20260926), which is in the
private RE archive. Its contract table is kept in [PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md).

## Authority order

This section is this file's own summary. The quoted working rule is from PROVENANCE.md, whose Authority section introduces an ordered list that is not in that file.

> **Port Stuart's shape first. Cite the file and line. Override from bytes
> only where measurement proves divergence.**

The pinned `references/Onslaught` source is the architecture and intent
authority. The pristine retail specimen (`74154bfa…`) is the behavior
authority. Where they disagree, the shipped bytes win — but only after a
measurement proves the divergence, and the divergence is recorded.

The earlier lane's World 110 construction history and its September 19 contract notes are in
[PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md#world-110-construction-and-the-september-19-contract-notes).

The explicit PC53 scheduler path agrees with the controlled PC64 result for the bounded operands of the projectile-readiness queue and maximum-speed-provider contracts; it is not a general
PC64 implementation. **Open question:** which x87 precision context applies at
the live scheduler entry for each relevant retail caller? The cheapest direct
falsifier is a controlled observation of the control word and operands at the
named scheduler entry during the corresponding retail call. The isolated PC64
and PC24 runs do not select a global simulation mode.

The provider evidence distinguishes maximum speed from current velocity, a
parent's primary interface from its renderer interface, and normal monitored
construction from arbitrary cyclic links. Those are constraints for the later
collision port, not claims that the current simulation closes full-shot RNG,
monitor allocation or collision-pair delivery.

## Known divergences (measured)

| Divergence | Stuart source | Shipped bytes | Where recorded |
|---|---|---|---|
| `InJetMode` | 0.3 s | 0.5 s | PROVENANCE.md |
| `CGame::DeclareLevelLost` countdown | 5.0 s | 2.0 s | the Carried retail contracts table in [PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md#carried-retail-contracts--entity-owner-implementation-test), `RetailGameEndCountdownTests.TutorialBroken_StartsTheTwoSecondLostCountdown` (`0x0046F4A8`) |
| `CGame::FillOutEndLevelData` score below D | `game.cpp:1011-1024` stores −1 then clamps to 0 then 0.001 | `0x0046d772` stores 0 and jumps to `0x0046d79b`, skipping `0x3a83126f` | the Carried retail contracts table in [PARITY-EARLIER-LANE.md](PARITY-EARLIER-LANE.md#carried-retail-contracts--entity-owner-implementation-test), `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmRewritesAZeroScoreToZeroBecauseRlwdDeltaIsPositive` |
| `CPanCamera` length | Stuart value | 6.0 | PROVENANCE.md (VA 0x004198D0, vtable 0x005D92A8) |
| Weapon resource path | pinned path | differing path | PROVENANCE.md |

These are exceptions to record precisely, not templates for loose porting.

## Parity dimensions and their gates

| Dimension | What it means | Current gate | What the gate does / does not prove |
|---|---|---|---|
| **Deterministic sim state** | Identical tick-by-tick simulation given identical inputs | The Parity suite's per-Present comparisons with recorded runs (reference-bearing; [milestones](#proof-of-concept-milestones)); the earlier lane's Core cold-start and full-chain tests with `--expect` trace hashes | The recorded comparisons prove equality on the probes and Present ranges each [log](PARITY-LOG.md) entry states, and nothing outside them. The earlier lane's hash tests prove determinism and self-consistency only, not retail equality |
| **Visual frame** | Rendered output matches retail within tolerance | `npm run ref:godot-capture -- --plan mainmenu` (through the `godot-offscreen` wrapper, which is on the maintainer's machine and not in this repository) + `tools/score_frontend_capture.py` + `frontend-regions-*.json`, `gameplay-regions-level100.json` (the reference frames are retail-derived and live under ignored local paths, so a fresh clone scores nothing: see rebuild/README.md) | Proves region-level visual regression against captured retail references. The JSON thresholds are **regression ceilings, not parity claims** (rebuild README is explicit) |
| **Audio** | Playback behavior matches retail | Shared sound-process and adapter tests; bounded production-device V12 fixture ([scope](OnslaughtRebuild.Host/AUDIO.md#measured-device-fixture-2026-10-27)) | Checks the stated source contracts and isolated Godot output/lifecycle. Ordinary Host gameplay, physical/audible acceptance and retail audio equivalence remain ungraded. |
| **Timing / feel** | Response latency, animation cadence feel equivalent | None dedicated | Not gradeable today; 20 Hz step + floor semantics are the strongest proxy |
| **Content completeness** | All retail content reachable/present | Materializer hash checks (200+ pinned inputs), level-100 slice | Proves the retail inputs consumed are byte-exact. Does NOT prove every level plays |

## The honest reading

The visual-frame work runs a scoring harness that is documented as *not* measuring the
thing that work exists to achieve. Visual parity gates are regression
ceilings; there is no *whole-run* gate in the Parity suite that compares rebuild
behavior with the original game directly. The one recorded win that was compared
with the original game ran in a private harness outside the suite (M5 above,
[log](PARITY-LOG.md)). This gap is named here so it is not mistaken for closure.

What the earlier lane's [Carried retail contracts table](PARITY-EARLIER-LANE.md#carried-retail-contracts--entity-owner-implementation-test) adds is narrower and real: **per-contract** gates that do
compare rebuild behavior to retail behavior, because the expected value in each
is read out of the pristine specimen rather than out of the rebuild. These per-contract gates are not parity. It is the first set of rows where a wrong rebuild is
mechanically detected instead of merely plausible-looking, and the mutation kill
is what distinguishes the two.

## What "done" would require (per dimension)

- Sim: a whole-run comparison with the original game that runs inside the suite. Retail-derived goldens cover the
  level-100 runs to Present 10000, and one recorded win was compared with the original in a private harness
  (M5, [log](PARITY-LOG.md)); neither is a suite gate for the whole run.
- Visual: measured tolerance windows per region against retail frames, not
  just self-regression.
- Audio: a playback contract (tracks, triggers, volume semantics).
- Timing/feel: a documented comparison protocol (the maintainer's ear is a
  legitimate instrument for feel; it is not a substitute for the others).
- Content: per-level playthrough evidence, not only input hashes.

## Standing rule

No claim of parity may be published from this file's existence. Parity is
per-dimension, measured, and gated — or it is not parity.
