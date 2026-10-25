# Rebuild parity contract

Status: active — proof-of-concept milestones (2026-10-15); the tables after them grade the earlier fixed-point rebuild
Last updated: 2026-10-25 (reference admission, Retry canvas and canonical Host phases; Host frontend sprites and explicit replay admission; 2026-10-23: M5 met against the port, open against retail: a player's whole game from boot to the saved career, and the pilot's win through its level end and outro; the goodies page, the level intro and outro films, the film-skip timing, the cloak button; earlier, 2026-10-22: a "Where it stands" summary first; maintainers' roles and private stores named plainly; the pilot's j4 route matches to P27650; the HUD's parts flash as the port's; the HUD's text drawn and compared with the port; the pan camera's orientation measured against retail's block; private evidence named by its store, not a machine path; earlier, 2026-10-21: whole-game host slices 1-4: the front end drawn page by page against the port, the level inside the flow through a parked-run inspect, the level's 2D and HUD target passes; earlier rows keep their dates)
Evidence: SOURCE — authority order and the known divergences are
recorded in `PROVENANCE.md` plus the Lost-countdown row of this table; gate capabilities are MEASURED claims of the
tracked harnesses named in the table. Every row of *Carried retail contracts*
is MEASURED: its anchor re-derived from the pristine specimen and its mutation
kill observed.
Specimen: `BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`
(the retail addresses cited in the divergences table are from the preserved
pristine specimen; do not infer another executable's identity from its folder name).
Summary: the gradeable dimensions of parity, the gate each currently has (or
lacks), and the standing exceptions. This document names the gap; it does not
claim the gap is closed.

## Where it stands (2026-10-23)

The milestones and their pass criteria are in [Proof-of-concept milestones](#proof-of-concept-milestones-2026-10-15);
the dated entries below record each measurement.

| # | Status |
| --- | --- |
| M0 | Met (2026-10-15): the harness parses retail's level-100 tape and golden trace and reproduces its change counts. |
| M1 | Met for every operation measured, with one measured ceiling: `fsin`, `fcos` and `fpatan` stored to a double differ in the last bits for some arguments (M1 entry below). |
| M2-M4 | Met on the keyboard-and-mouse tape: the sampled probes (EVT, FT, RNG, SC, LVL, POS, LIFE) equal retail's virtual-DirectSound golden through Present 10000 (`RetailGoldenTests.Level100RunMatchesTheVirtualDsGolden`). |
| M5 | Open against retail; met against the native port on two win routes (2026-10-23). The gate below asks for retail's career after the win, and retail is not yet a reference for a win: its paired runs of the same tape differ from each other. A player's own game from boot (`GoldOwnRouteTests`: the boot films skipped, a new career, the goodies page, level 100 from the front end with its intro skipped, the win, the outro, the debriefing, the main menu's Quit) equals the port on every probe field every Present to the tape's end, and the saved career and the options file equal the port's byte for byte. The tutorial pilot's -level win route equals the port through the level's end, its end-level data and the outro (`PilotFinalDronesRouteTests`). |
| M6 | In progress: the boot films and the front end's page changes land on retail's Presents (M6 entries below); the front end's float expressions still need a per-function oracle. |
| M7 | In progress: the Host runs the whole game from boot to quit (`HOST_LIVE=flow`), and its drawing is compared with the port's back buffers part by part (the dated entries below). |

The Parity suite gates each route at its stated floor against references kept outside the repository
([README](README.md#working-lane)); a missing reference is reported as a skip, which is not a pass. Suite counts in
the dated entries below are the counts on their dates.

CGameInterface on PC (2026-10-21): the in-game menu rises only at a lost or drawn level's end, after
`CGame::Update` has made continue, message log, level briefing and options unavailable (onslaught-decomp
`src/game.cpp:2105..2129`); its other toggles are commented out and nothing sets the joypad picture. Those
items are unreachable on PC and stay unported; the PC pause menu (`CPauseMenu`) carries continue, the
message log and the briefing.

Trainer missile launch and flight (2026-10-21): the level 100 trainer's first LIFE difference
(Present 16206) came from the Forseti Missile's state before its collision, not from the collision.
Per-frame traces of each missile against the frozen native port, with retail's own code run on the
captured inputs, separate three causes:

- Launch: `CWeapon::Unknown509140`'s straight aim (`005093c6..00509434`) stores the magnitude
  sqrt((Y² + X²) + Z²), the CRT asin pitch and the fpatan yaw, then calls the out-of-line
  `FMatrix(yaw, pitch, 0)` at `004062d0`. The Core used the inline-model constructor and a double asin
  (row 1's Y one unit off at the Present 16144 launch). Corrected; `StraightAimTests` holds nine cases
  whose yaw, pitch and matrix come from retail's block and constructor. The native port already agreed.
- Wiggle: `CRound::Move` builds both wiggle matrices with `004062d0` and sums the wiggle product
  (X + Y) + Z (`004d90d6..004d916a`) and the unwiggle (Y + Z) + X (`004d921e..004d929a`). On 92 captured
  frames retail's blocks agree with the corrected Core in all 92 and with the former Core in 65 and 84.
  Corrected; `RoundWiggleTests` holds 39 retail cases. The native port differs here too (a port
  correction, separate from this rebuild change).
- Seek turn: the Core already follows retail's direction and product orders; the native port's differ
  (port patch 0029). With 0029 and the launch fix the first missile's hit is equal through Present 16628.

Both rebuild corrections leave every existing Parity test's outcome and output unchanged. The trainer
route is compared again with the native port once the port's wiggle correction is in; until then the
remaining LIFE differences are the port's. Evidence (private): the maintainers' private evidence store (seek and launch captures, oracle receipts).

Death, landing and render-lifetime integration (2026-10-19): hard ground contact now executes
BattleEngine damage with the retail cap, component spills and shield-bypass rules. The BE has its
own lethal transition, loss camera, script ordering, wreck motion, contact explosion, delayed pause
and fade state. Destruction releases modeled parts, controllers, weapon targets/effect links and
owned repeating sounds. Generic render teardown unlinks the mesh and emitter links; actual mesh
render queries refresh the unit hit countdown even when host drawing is disabled.

The maintainers rebuilt and reran 1,024 original dying-movement cases, 1,036 camera cases,
569 hit-countdown cases and 1,028 original hard-landing cases under two precision modes and two
negative variants (6,168 calls). The managed cosine check uses only the 1,015 original rows that
actually evaluated it; 13 branch-skipped scratch values are not treated as evidence. All 85 joint
focused tests pass without skips, including actual shutdown and landing paths. The complete integrated
Parity suite passed **537 tests, 9 unchanged optional-reference skips, 0 failures**. The retail camera
constructor leaves two timestamp words uninitialized: four heap-seed controls show its ambiguous
first-use cache. The reconstruction explicitly starts with an invalid cache, not an invented claim
of universal retail heap equality. Device rumble, visible damage-flash pixels and audible fade remain open.

The frozen native port's ordinary-input diagnostic route now destroys **all 4 + 3 + 6 ground
targets**. A Parity replay ran it through Present 15,130 and observed all 18 real script posts
at the same completed Present; the observer forwards each original call once and leaves the whole
trace unchanged. After the stopped-round query correction below, **all nine probes agree at
every Present 3..15,130**, across all 136,152 comparisons. This is selected-state agreement with
frozen native port `e55cb2d8`,
not a fresh retail whole-route comparison. The tape SHA-256 is
`b083d308cc29bb108d2d4a15bc8ee7a1c17d7086ae386682fb2f9425b9d69919`.
The native route starts with `-level100 -skipfmv`; cold-start/menu/audio acceptance and full tutorial
flight/refuelling/later combat/win/save remain separate. Its later evasion attempt takes hits;
it is not a perfect-score or no-hit run. Receipts (focused tests, independently executed original code, full ground
probe comparison and 18-post admission) are in the maintainers' private evidence store.

The frozen native route subsequently reaches the first flight zone and genuinely destroys its first
airborne target at Present 19,821. At 20,800 it has landed on the island and is recharging, with health
19.8097916 and energy 4.4644375. Its required target groups stand at **4/4, 3/3, 6/6, 1/3 and 0/6**;
all 26 recorded script posts and the ordinary-input prefix are preserved. The maintainers inspected the fresh
landing image and admission receipt. This is native-port progress, not a managed-rebuild run or full
retail acceptance. The first airborne group, final combat group, completion and save remain open.
The private tape is `fire3-airborne-recharge-land-20261019.tape`, SHA-256
`09bc6c00b742401c997bf6c175b7bae608f1c04d6ef62a1417218c04121d7400`.

Stopped-round and plane follow-up (2026-10-19): retail's final straight-projectile query comparison
rejects unordered results. The previous greater-than check admitted the NaN produced by zero velocity,
scheduling three extra tree collisions at Present 9,365 that survived as null events at 9,372.
The fix changes that comparison alone. The maintainers executed 12 original query cases and all 12 focused tests;
the full ground replay above now has no EVN differences. The bounded native dispatch observer retained
all 84,600 ordinary probe values through Present 9,400 while recording 1,708 contiguous dispatches.

The Plane attack/escort handler, distinct waypoint handler and wing animation transitions now preserve
target-reader order, 25/70 approach/retreat hysteresis, 30/120 escort thresholds and conditional random
draws. An independent reproduction ran 1,024 cases executing five unchanged retail arithmetic blocks; the PC53 negative
control differs in 765 cases. All 21 focused tests pass without skips. Radius callbacks and RNG returns
are authored oracle boundaries; that oracle does not execute targeting, animation or the scheduler.
The script Attack now routes through Unit/squad target ownership, preserving monitored readers and
the native target-selection rules. Aircraft launch thrusters use their actual afterburner descriptors,
emitter transforms and cleanup. The missing CRT draw at 15,669 comes from the afterburner timeline's
emitter child, not effect allocation; no compensating random call was added. The integrated replay
now agrees on **all nine fields at every Present 3..16,066: 144,576 comparisons, zero differences**.
The nonballistic weapon reach/firing queries now execute through the trainer's first missile launch;
an independent reproduction ran 2,073 original cases (20,730 output words), with 600 cases distinguished by the
PC53 negative control, and all 13 focused cases pass. Subsequent missile seeking and BattleEngine
lock-loss integration reproduces 2,320 original cases (39,440 words); PC53 changes 1,056 cases.
The configured replay now completes the trainer's attack-and-cease sequence through Present 17,300.
Its previously missing event 4002 plays the spoken missile warning, independently of the radar beep,
with the original strict two-second deadline. An independent reproduction ran 14 original-handler boundary cases and
19 focused tests; restoring this actual call removes every CRT difference from Present 16,067 onward.
Across **155,682 comparisons**, eight of nine fields agree at every Present 3..17,300. LIFE differs at
283 Presents, first at 16,206: health agrees at that hit, while energy differs by 11 float ULPs.
A noninterfering private collector and eight original Damage executions reproduce the managed results
exactly on the captured inputs; the explosion's incoming damage/trajectory needs investigation rather
than an adjustment to Damage. This does not yet establish full trainer parity or tutorial completion.
The complete integrated suite passed **748 tests, 9 unchanged optional-reference skips, 0 failures**;
all 749 outcomes from the spoken-warning integration are unchanged and eight new cases execute.
Private evidence remains in the existing death-integration owner, including
`trainer-reach-fixed-admission.json`, `weapon-reach-root/`, `weapon-reach-full.trx` and
`weapon-reach-suite-admission.json`, `round-seeking-root/`, `trainer-seeking-fixed-admission.json`,
`sound-integrated-full.trx`, `trainer-incoming-fixed-admission.json`, `incoming-voice-root/`,
`trainer-damage-root/` and `trainer-integrated-suite-admission.json`.

Explosion falloff correction (2026-10-19): pristine `CExplosion::Hit` at `0044bf65..0044bf9b`
sums squared displacement as `(Z² + Y²) + X²`, retaining the x87 exponent range until the
magnitude store. The previous generic vector norm used a different order and float intermediates.
An independent reproduction ran 5,347 isolated original Hit cases; 521 distinguish the former order. Eight managed
counterexamples cover summation order and intermediate overflow/underflow, and all eight fail with
the former implementation. The configured trainer trace remains byte-identical after this correction.
Its first explosion still gives `3ecf1334` in both Core and original code on the captured Core inputs,
so this correction does not explain the first LIFE divergence. Original-code execution uses an authored
radius callback and Damage sink; it establishes bounded arithmetic, not the collision trajectory.
Receipts: `explosion-hit-root/`, `explosion-order-full.trx` and
`explosion-order-regression-admission.json` in the same private death-integration owner.

A subsequent noninterfering native first-hit observer preserves all145854 ordinary probe fields
through Present16206 and captures eight paired callbacks without errors. Original Explosion.Hit
returns native damage `3ecf1286` on the native inputs and managed damage `3ecf1334` on the
managed inputs. Player position, radius and configuration agree; projectile position already differs
at the direct Round.Damage boundary, before explosion placement. A separate managed collector
preserves its entire16204-record trace and confirms unchanged position/velocity from Round.Hit
through GoOff. Original placement on those managed inputs reproduces the managed explosion.
Native velocity at that boundary was not observed, so it is not assumed equal. The next collision
observer is a private, unexecuted draft, not another accepted fix. Receipts are in the maintainers' private evidence
store. The full suite count above was unchanged.

Sound primitives are prerequisites only: 1,060 original format/length cases and 512 sequences
against the actual native port's virtual mixer were independently reproduced. Converted byte
length, block-clock progress, cursor, pause/resume and completion boundaries now have tested
owners. A shared sound-event kernel now implements pool admission, channel retention, ordered ranking,
pause/stop, deferred deletion and owner callbacks. An independent reproduction rebuilt and reran 328 original
kernel cases, 640 effect-admission cases and 64 platform PlaySound controls, including controls that
distinguish clock reads, retained channels and stopped-event revival; all 26 focused tests execute.
The concrete spatial/buffer adapter and explicit process binding are now integrated. An independent reproduction ran
184,576 original gain outputs and 512 actual-port buffer sequences, plus bounded spatial checks.
Voices, cockpit warnings, engine loops, dashes, weapon/impact/explosion and initial unit sounds use
the same event owner. Once/repeat, fade identity, pause, menu gain and restart callbacks have focused
coverage; 99 joint checks pass without skips. No independent warning/voice duration timer replaces
channel admission. A private harness can select the measured 256-node e55 startup pool and an
explicit native-device profile; these are not universal retail defaults. The normal host does not
silently select that private profile. Feature loops and unit post-fire sound now retain their original
owner/cleanup ordering; an independent reproduction ran 23 original feature/post-fire cases and 100 related tests.
Complete world/device teardown, unimplemented component effects/shadows, full-route comparison beyond
the trainer and audible acceptance remain open. Original-code outputs stay
private under the existing death-integration owner's `sound-events/`, `sound-adapter-root/` and
`sound-numerics-root/` directories.

Flight and damage integration (2026-10-19): ordinary jet controls now reach the measured jet
movement, ground-effect, friction and maneuver paths. BattleEngine damage uses its own shield,
repair, augmentation, statistics and shake rules rather than inherited Unit damage. Damage flashes
expire through scheduled movement. The maintainers reproduced 5,000 yaw and 7,000 movement cases, plus
1,035 damage/shake and 1,006 flash cases, against isolated original code. Precision-negative
controls change 75 yaw, 1,179 movement and 257 damage results. The integrated suite passed
**462 tests, 9 unchanged optional-reference skips, 0 failures**.

The extended native ordinary-input tape destroys two of six moving targets at Presents 14,184
and 14,304, after the two static ranges; it retains health 20 and energy 8 with no friendly-fire
loss, abort or Won. Headless Parity executes this tape through Present 14,350: all seven primary
probes plus CRT agree for every Present 3..14,350, across 129,132 total nine-probe comparisons.
EVN alone still first differs at 6,017 (8,334 differing later records). This is selected-state
agreement with the native port, not a fresh retail whole-route comparison or a complete tutorial.
The tape SHA-256 is `84fbc81038f43117bf49584d2d3808c2e069cccbd7668b751eb2cb2f2f52ad81`.
Receipts are in the maintainers' private evidence store.

The Space-input flight fixture explicitly enables flight permission before exercising real input
and scheduled takeoff/landing/refill; it does not establish the tutorial's permission route.
Hard landing and lethal death/teardown are implemented in the later integration above. Configured
incoming warnings now use the shared event lifetime; the legacy unconfigured path still refuses them.
Damage-flash drawing, device audio and player acceptance remain open.

Water skimming follow-up: `HandleSkimming00411500` now retains the original x87 speed/product
precision and calls the BattleEngine damage owner with a null attacker and shields disabled,
then reports the hostile environment. An independent reproduction rebuilt the private oracle and reproduced all **4,151**
original-code cases, including guard boundaries, large finite velocities and invulnerability;
the PC53 negative changes **710** outputs. All **26** focused flight, skimming and damage tests
pass without skips. Terrain, sound lookup and authored objects remain explicit oracle boundaries;
this does not establish a tutorial water-skimming playthrough. The receipt is in the maintainers' private evidence
store.

Weapon-change input (2026-10-19): the native tutorial tape now runs through Present 13,000 in
headless Parity. Previously its right-mouse release reached an unsupported `MechChangeWeapon`
handler at Present 11,613. The restored `CBattleEngine::ChangeWeapon` wrapper preserves the
active-count guard, selection/ammunition distinction, movement reset, change timestamp and HUD
sound's shared random draw. Four focused tests execute, including ordinary right-mouse release,
held-button non-repetition and the empty-secondary case. The old prefix through 11,612 is unchanged.

A Parity replay compared all seven primary probes for every Present 3..13,000 with the frozen native port:
all 90,986 values equal. CRT count/state also equals from Present 4 through 13,000; its first
recorded sample differs. The additional EVN event-count probe differs on 12,997 records and is
under investigation. These limits are explicit: this is not nine-probe parity or complete tutorial
acceptance. The private tape SHA-256 is
`eff3538300257457779df55692c3c67b59a82db4e169d485307f8089ef949ae4`;
receipts are in the maintainers' private evidence store. No physical device or audio acceptance is claimed.

Following event-lifetime repair (2026-10-19): the real radar receiver now scans the ordered
round set, retains monitored threats and schedules its next update; it is initialized after the
cockpit, as native calls 004055dc then 00405710 require. ComplexThing deletion releases its
animation monitor after its script, preserving the already-queued null event but preventing
further dead-owner animation scheduling. These fix the missing 60 pre-run radar events and the
later orphaned aircraft animations. The loading Present now returns before unit-init sound draws,
matching the native probe boundary; the draws still occur before the first gameplay update.

The same tape now matches **all nine probes at every Present 3..6,016**. The seven primary
probes and CRT state/count match through 13,000. EVN first differs by one at Present 6,017
and differs on 6,984 later records; this is still an open event-lifecycle question, not full-state
parity. All 116,982 comparisons were performed. The integrated suite passed **443 tests,
9 existing optional-reference skips, 0 failures** on that date. Private receipts are in the maintainers' private
evidence store. Radar bearing retains the existing explicit
atan2 approximation; missile lock admission, nonloop warning playback and complete battle-engine
teardown are not established by this slice.

## Proof-of-concept milestones (2026-10-15)

Walker dodge and flight prerequisites (2026-10-19): the four directional dash paths now preserve
their strict double-tap window, charge loss, shared sound draw, asymmetric roll assignments and
retail left/right matrix paths. The jet has its own weapons and measured thrust/turn/pitch/gravity/
auto-level controls. Morph admission preserves permission, energy and special-move ordering,
mesh/controller ownership and scheduled transitions. These are prerequisites: ordinary jet input,
the complete jet movement loop, damaging landings and full flight acceptance remain unfinished.

The maintainers independently compiled and ran the original-code fixtures: **1,864 dash cases**
and **10,000 jet-control cases**, with all recorded output words equal. Changing x87 precision to
53 bits changes 364 dash and 106 jet-control cases, respectively. The integrated Parity suite passed
**428 tests, 9 existing optional-reference skips, 0 failures**; no reference expectation was changed.
The host builds with zero warnings/errors. The existing ordinary-input tutorial tape still reaches
Present 13,000 and all 12,998 selected-state records equal the preceding weapon-change build.
This preserves its seven-probe result and its explicit EVN/P3-CRT limitations above.

Repeating sound state is logical and silent: its fade now advances once at MainLoop's
`SOUND.UpdateStatus` boundary (0046eff6), after the pause transition and before repeated renders.
A real LevelSession test drives ordinary Escape pause/resume and distinguishes update from render
cadence. Device channels, audibility and nonloop duration are not established by this model.
Private original-code fixtures and their reproduction are in the maintainers' private evidence store. These are isolated execution and reconstruction checks,
not a complete retail or Godot tutorial playthrough.

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

Observed and modelled Present schedules (M6 and M7). The boot films (`Flow/BootFilms.cs`, from CSystem::Init and
CDXFMV::Play) follow retail's default PAL path: logo then opening film; the vendor tweak is off and the PC release
has no NTSC films. A film makes one Present before its loop, one for each frame but the last, and one after. The
logo's 229 frames cost 230 Presents; the opening's 2,054 cost 2,055. Waiting for the next video frame never presents.
Under the retail replay harness the films are paced by their sound tracks on Wine's real-time mixer (its log of every Present
from 2190 to 2300: one flip site, 0x00512644, and no spin escape). `HarnessFilmPacing` models each frame as due,
without those real-time waits; the port's virtual-clock rule (`BEA_SLEEP_VIRTUAL`, sleeps moving the clock) plugs in
as another `IFilmPacing`. The opening film's copyright splash starts at film frame 1,934, fading in 7 steps per frame.
An earlier capture at 2204 showed that splash, and Present 2220 was inspected at full opacity: the Lost Toys
copyright screen, not a loading screen. Those visual observations are retained independently of the corrected boot
boundary interpretation below.

Clock-origin correction (2026-10-17): the device makes Present 1 before CSystem::Init establishes GetSysTimeFloat's
origin. The logo then occupies Presents 2–231, the opening 232–2286, and the loading screen presents at 2287. The
previous model placed the device Present after the films and initialized the clock at Present 0; although the total
Present count was right, it gave the wrong filtered front-end step from Present 2289. `GameRun` now establishes the
origin after the device Present, before either film; no clock offset or numerical formula was changed.

Evidence: two offscreen processes of port commit `89a8b037` (binary SHA-256
`e42adf1f30770d9254bdc52a2605c7a7333646e6acfee71615c25e89690f27a8`), private runs in the maintainers' private
evidence store, showed `first_seen = gQpcBase + 333333` and identical
clock/step values at every front-end Process observed from completed Present 2287 to 2300, with no spin escapes or
virtual sleeps. Their first breakpoint was after the first Present sampled its QPC base. The existing R5 call trace
independently places the logo and opening entries at completed Presents 1 and 231. This is new port evidence; the
older retail film-boundary interpretation was not re-recorded in this check. The loading screen's 0.05-second throttle
and the number of its clock reads remain open; the reproduced boundary counts are still observed inputs.

`FrontEndRouteTests.TheFullBootIdleMatchesThePort` now requires the existing reference's EVENT_MANAGER time and
complete front-end state (filtered step, page, transition and counter) to agree at every reference Present through
5700, including the attract films and return. Before the correction this assertion fails at 2289; after it every
compared time-state and CRT probe agrees through 5700 (1,127 front-end time-state samples and 5,700 CRT samples; both counts are asserted). `GameRunTests.TheDevicePresentEstablishesTheClockBeforeTheBootFilms`
also checks the logo's entry and the first two post-origin clock values. Level-100 and -skipfmv clock origins are unchanged.

Measured (2026-10-17, controls and saved startup settings): the keyboard/mouse preset now applies
the retail table rather than throwing or substituting the single-input default bindings. Preset zero
preserves all bytes. With no enabled joystick, every signed nonzero saved selector copies each entire
32-byte keyboard record to the first matching control ID; unknown IDs, later duplicates and fixed
records remain untouched. Retail repeats the same table for positive selectors above one; these
idempotent passes are collapsed. Startup applies the saved preset after loading raw bindings, then
blanks campaign progress; ordinary career selection still does not load those settings. Menu selection
commits only on confirmation. Enabled-joystick preset effects, the real display/resource reset and live UI
acceptance remain open; this implements the current keyboard/mouse host, not joystick parity.

The evidence is isolated execution of pristine retail table initializers 00514210/00453460/00453630,
their constructors, first-ID lookup 0042db10 and ApplyControlPreset 00453780, without imported-call
stubs. Ten original cases and 27 additional signed-selector/device cases were independently reproduced
with native x86; bounded Unicorn agrees on 36 of the 37 cases. The 32767/no-pad repetition case ran
natively only. Enabled-pad cases demonstrate the omitted device-dependent behavior. Private inputs,
outputs and reproduction receipts are in the maintainers' private evidence store
(`control-preset/`). `KeyboardPresetTests` binds complete
record outputs to those observations and tests menu confirmation, saved selectors from -32768 to
32767, campaign reset and original-fixture preservation. `ControllerTests` checks the restored Up/W
alternate binding. These are code and isolated-execution checks, not a retail UI playthrough.

Display Apply transaction (2026-10-17): every changed menu setting now commits before the next
platform Flip consumes a single process-owned reset request. Previously the first display setter
threw immediately, leaving later settings unapplied. Static inspection of pristine `CVSync::SetValue`
(004cf500) and `CPlatform::Flip` (004d2580) establishes the queue/return and Present/reset ordering:
clear the request before resetting, and save current career/options only after a successful state-2
reset. State 1 does not perform that save. A raw device Present, including the initial boot Present,
does not consume the platform reset request. Career-load suppression reads the same request owner.

`DisplayResetTests` exercises those paths, multi-setting Apply, reset failure and reentrant loading
Presents, current bindings and selected-mode persistence, and preservation of all other bytes in the
real tracked career fixture. The mode-key encoding follows `CDisplayModeKey::Set` (00420d10): low
16-bit width, 15-bit height, and the format-classification bit; subsequent ordinary saves retain it.
These are static-code and reconstruction-flow checks. **The Godot host still has no real
device/resource/input reset handler** and fails explicitly at the correct reset boundary. The live
ReadOptions path that can queue state 2 is also not implemented; its admission rule is tested through
the explicit API. This milestone does not establish working display Apply in the running host.

Cannon motion controller (2026-10-17): `CMCCannon::GetPartTransform` (004952a0) now supplies
case-sensitive turret yaw, barrel pitch and root-height adjustments through the existing pose and
uncached-emitter paths. Barrel pitch subtracts the cannon's current Euler pitch, unlike the ground
vehicle's slope query. The controller records its last angles after every part. Retail's pose updater
calls the angle-comparison slot but ignores its return value, so the implementation preserves the
same-render-frame refusal and the 30-frame/force gates instead of adding angle-driven invalidation.

Evidence: 1,200 isolated executions of the pristine original function and its dependencies at x87
control word 007f, each with 25 authored input words. Cases vary nine part names, root/non-root,
world/local, old/current angles, noncommuting matrices and interpolation timing. Global render
fraction is zero; varying LastMoveTime covers the tested interpolation/clamp paths. An independent reproduction
rebuilt the oracle and reproduced all output words, then ran the C# Parity suite: **376 passed,
9 existing optional-reference skips, 0 failed**, including all nine new controller tests. The consumer
test uses a real Level-100 cannon to check cached and uncached emitter dispatch and pose refresh.
No reference expectation was changed. Private inputs, oracle, hashes and receipt are in
the maintainers' private evidence store (`cannon-controller/`), with the suite receipt.
These are bounded original-code and reconstruction checks, not a live retail playthrough or Godot
visual acceptance. BattleEngine and Buggy controllers remain separate work.

Dropship motion controller (2026-10-17): `CMCDropship::AdjustFrame` (00496100) now
adjusts door animation frames through both cached poses and uncached emitters. It selects the first
case-insensitive opening/closing mode, uses the global render fraction, and preserves the original
integer-load and x87 arithmetic order. Non-door parts leave controller state untouched; a missing
mode leaves the frame untouched but updates the saved door state. `ResolveFrameNumber` now calls
the adjustment hook in both its animated and fallback branches, as retail does at 004b2534/004b2591.
Existing same-frame and 30-frame pose-cache gates remain intact.

An independent reproduction rebuilt the original-code oracle and reproduced **1,800 cases**, each comparing
19 output words, then ran the integrated Parity suite: **388 passed, 9 existing optional-reference
skips, 0 failed**. All 12 added cases executed. Tests exercise opening/closing, missing modes,
non-door parts, integer frame parameters, global interpolation, cache behavior and emitter consumers.
The real Level-100 U-17 uses `f_lifter.msh`, whose 21 parts contain no door-prefix names: that fixture
proves controller wiring and unchanged non-door behavior, not a visible tutorial door correction.
Active door animation uses an authored mesh fixture through the existing mesh reader and emitter path.
Landing behavior remains separate. Private original-code inputs, hashes and reproduction are in
the maintainers' private evidence store (`dropship-controller/`), with the integrated suite's receipt.
These are bounded original-code and reconstruction checks, not live Godot or retail visual acceptance.

Front-end particles (2026-10-17): the front end now loads its shipped particle descriptors and
mesh resources into the same particle implementation used by levels. Events advance before the
one-turn particle step, which precedes controller/page work, including reconnect holds (00466ba0).
Debriefing goodies carry full positions and linked frames of reference, stop when leaving the page
and release their links at shutdown (00456930). Completed-career fireworks use the shipped effect
name and the original Z-then-X random draw order (00462640). Noise, frame/allocation counters and
sprite numbering belong to one simulated process and survive front-end/level/restart transitions;
separate simulated runs no longer share the CLR-static sprite counter.

An independent reproduction rebuilt the pristine InitNoiseTable oracle (004c7db0, unchanged rand), reproducing eight seeds
and all 1,024 float words per seed, with 1,023 draws on first initialization and none on the second.
Only Win32 TLS/last-error imports are adapted. The integrated Parity suite passed **394 tests,
9 existing optional-reference skips, 0 failures**, including all six new front-end particle cases
and all twelve dropship cases. Shipped goodie/firework closures load real resource meshes, create
particles and expire; tests also cover link positions, shutdown, reconnect timing, failed BeginScene
and process-state isolation. No golden expectations changed. Private original-code evidence is in
the maintainers' private evidence store (`frontend-particles/`), with the suite and command receipts.

This completes this front-end simulation/lifecycle slice, not its Godot drawing or full particle
numerical parity. General particle-motion arithmetic and retail heap values for not-yet-shown
goodies remain explicit limits. No live retail or Godot visual acceptance is claimed here.

Experimental live Level 100 host (2026-10-17, M7 remains incomplete): `HOST_LIVE=1` retains
one parity session and advances fixed Presents, with retained terrain/object/water/surf views and
ordinary keyboard/mouse input. The preview uses silent virtual voice timing. A supplied `HOST_TAPE`
exclusively owns input; untaped focus loss suspends pacing and discards held/pending input. The
limited pause view permits resume and blocks hidden settings navigation. Default snapshot mode
remains the default. Front-end navigation, debrief/retry and actual audio are not in this host slice.

The integrated closeout suite passed **397 tests, 9 existing optional-reference skips, 0 failures**.
An independent reproduction ran 19 adapter checks and the retained-terrain comparison (1,572,864 level selections,
6,291,456 expanded indices, 1,048,576 cache texels, four reversed-order negative controls). Six GPU
runs exited zero offscreen. Default snapshots at Presents 30 and 3900 are pixel-identical to the
prior snapshot implementation. Drawing on/off and a standalone headless session agree on every
recorded seven-probe and extended-state field through Present 4020 (4,018 records from Present 3;
four comparison negative controls reject changes). This is selected-state equality, not proof of
all simulation state or retail visual parity.

At the October 17 checkpoint, live images differed from snapshot mode in 4,701 pixels at Present 30
and 7,464 at Present 3900, with missing trees. That placement defect is corrected below. A 124-frame,
640x480, 30fps movie of Presents 3900-4020 plus settling frames was recorded; full moving-footage
review, longer resource-growth/performance checks and actual Godot event delivery remain pending.
The real-LevelSession adapter checks cover same-batch pause/resume and focus loss after the pan.
Intro skipping was unsupported at that checkpoint; the preserved correction is integrated below.

Private runs and comparisons are under
the maintainers' private evidence store (`live-host/`), with the headless trace, build and suite records. This is an
opt-in experimental implementation and safe resume point, not completed M7 or full tutorial acceptance.

Live host corrections (2026-10-19): `MeshView.LiveSet` now places each tree from its own Thing
position/orientation, as the snapshot view does. Trees share a render subject whose position is
zero and orientation is identity; using that subject put all live trees at the origin. Other
subjects keep their interpolated placement. No simulation, controller or pose callbacks changed.
Fresh offscreen captures show the trees restored. Compared with the retained snapshot images,
the difference falls to **7 pixels at Present 30 and 73 at Present 3900**, each differing by at
most one RGB code value. Those residual differences remain unclassified; pixel equality and
retail visual parity are not claimed.

`Player.ReceiveButtonAction` now restores the preferred control camera and starts play when
SkipPanning is admitted, preserving the guards, fall-through and original scheduled deadlines.
The maintainers rechecked pristine instructions 004d3160..004d3199 and 0046fec0..0046ff0b,
then reproduced all four installed-data tests: Space, Return, keypad Enter, and Escape (which
skips then pauses in the same controller flush). The old camera's monitored readers clear; the
scheduled callbacks still run without replacing the control camera or repeating message admission.
Third-person view remains unsupported. The host now advertises Enter to skip the intro.

The integrated suite passed **401 tests, the same 9 optional-reference skips, and zero failures**;
all earlier outcomes are preserved. Host build has zero warnings/errors. Three fresh offscreen
Godot runs exit zero. Live traces agree with the retained headless trace on all selected fields
through Present 3900 (3,898 records). A new ordinary Return-key tape skips at Present 31 and
agrees with a fresh independent headless session through Present 600 (598 records); all four
comparison negative controls reject alterations. This exercises tape delivery in the running
Godot host, not physical input-event delivery, audio, or the complete tutorial.

Private capture/trace commands, hashes and comparisons are in
the maintainers' private evidence store (`live-host/resume-20261019/`), with the suite and build receipts.

Whole-game host, slices 1 and 2 (2026-10-21, M7 in progress):
- Slice 1: `HOST_LIVE=flow` runs the whole game (`GameRun`, boot to quit) on its own thread through `SteppedGameRun`. Godot's
  thread reads only the immutable `FlowSnapshot` each Present hands back, and keys reach the run through its input queue.
  `rebuild/tools/host_flow_check.py` holds each route's stepped trace equal to its straight one and to the port's fields.
- Slice 2 draws the front end. `Parity/Flow/Draw` forms the quads as the game forms its vertices (`DrawSprite`,
  `DrawTextScaled`/`DrawTextDynamic`, `DrawLine`/`DrawPanel`/`DrawBox`, `RenderMouseCursor`, and the video quad with no film
  open, which is every -skipfmv run) and hands them to a pure canvas (`IFrontEndCanvas`: no Godot types, called only from
  the render path). It covers the main page, the common page's lines, the message box and the cursor.
- `FrontEndCanvasTests`: the front-end loss route to Present 1400 makes the same snapshot every Present with no canvas, a
  null canvas and a recording canvas, and two recordings draw the same frames.
- The Host draws each frame through Godot (`FrontEndView`): meshes under an orthographic camera with retail's state
  (alpha test at 8, depth LESSEQUAL with writes, source-alpha or additive blend, draw order by render priority). A quad
  drawn with ZFUNC ALWAYS draws its depth again in a second pass, because OpenGL writes no depth with the test off.
- `FrontEndRaster` is a software rasterizer of the same frames: a repeatable oracle that runs headless.
- Long frame names are cut as LoadFrame cuts them (61 characters past the folder), which is how the install stores two
  of the title's symbols.
- Measured against the port's back buffer (ae04185f, fe-r1, every 10th Present from 160 to 680; main page, quit box,
  cursor): mean absolute difference per channel 0.12–0.13 for the oracle and 0.39–0.49 for Godot. 0.10–0.11% of pixels
  differ by more than 16 in either; the remaining differences are along anti-aliased glyph and sprite edges.
- Not drawn yet: the intro page (Present 150 differs throughout), the other pages, particles and the level.
- Font colours are doubled for a plain modulate, as the game does without modulate 2x; a channel can differ where the
  doubled colour clamps.

Slice 3 (2026-10-21): the front end's other pages draw.
- Pages: intro, name keyboard and save list, save, load, level select, briefing, Battle Engine configuration (without its
  3D Battle Engine), debriefing, storage device, and options with its pause menus.
- Shared drawing: `FrontEnd.Draw.cs` (title bar, borders and mask, video background, small logo and the Forseti writing,
  selection marker) and textures found by name.
- `FrontEndDraw.Modulate2x` tracks the colour stage: every sprite leaves it at 2x, while the video quad and the
  briefing's plain modulate clear it. Text doubles its colours only under 2x.
- Retail's renders make state-changing calls that Parity lacked; they are now made whether or not anything is drawn:
  - each page title's `FET.GetText`, and the other texts the renders read (each checks the X key);
  - the clock reads the renders make (each counts toward the spin escapes);
  - the keyboard page's refresh of the save list;
  - the save and load pages' timed boxes, which now read their texts and are laid out;
  - the options menus' cursor hover and clicks, first-render time and title.
  The front-end route tests are unchanged by all of them.
- text.stf's negative ids now parse.
- The flow Host puts the installation's own saves on the card, as the game run from its folder (and the port) sees them.
- Measured against the port's back buffer (ae04185f), as mean per-channel difference from the software oracle on
  settled frames:

| Page | Mean difference |
|---|---|
| Intro | 0.41 |
| Keyboard and save | 0.11–0.14 |
| Level select | 0.10 |
| Briefing | 0.41 |
| Battle Engine configuration, outside the Battle Engine | 0.38 |
| Debriefing | 0.20 |
| Options, first list | 0.09–0.11 |
| Options, sound list | 0.58 |

  - Before this slice Parity's 3D sound method was 0. Retail chooses 2 with a sound device and the options at -1, and
    the port shows 2.
- Open, not drawn yet:
  - the configuration page's 3D Battle Engine;
  - front-end particles (the main page's fireworks, the debriefing's);
  - the controls page's key list (debug font, Windows key names).
- The cursor after a level: `ShutdownMouse` takes `GetCursorPos`, the system cursor, which no tape records. It is now a
  platform input (`Mouse.SystemCursor`), machine state like the clock, defaulting to the port harness's offscreen answer
  (0, 0); the debriefing then matches the port at 0.11. Open: what retail's harness (Wine on the same Xvfb) answers,
  which the next retail pair can log.

Slice 4 (2026-10-21, groundwork): the level inside the flow.
- `SteppedGameRun.Inspect` is the host's one way into the run's objects. It runs only while the run's thread is parked
  at a Present and throws otherwise.
- `InspectReadsTheRunOnlyWhileItIsParkedAndChangesNothing` is the stress check. Over fe-lose to Present 7300, each inspect
  sees the run standing at its Present despite random host delays, and every snapshot equals the straight run's.
- `GameRun.LevelChunks` hands the host each level's textures and surf.
- The flow Host builds the live level views when a level starts, updates them through `Inspect` and frees them when it
  ends.

Slice 4, the level's 2D (2026-10-21), compared with the port's fe-lose shots (`HOST_SHOTS`, Godot and the `levelraster`
oracle):
- The end screen, the in-game menu, the message box's panel, text and portrait match. The portrait clips to the battle
  line's circle through `CircleMask`'s depth, and the battle line leaves out its map while a message plays, as
  `CDXBattleLine::Render` does. At P1500 to P6100 the circle's mean colour is within 1 to 6 of the port's.
- The HUD's reads that touch the game now run in the core's HUD render (`WorldRender.RenderHud`), as retail makes them.
  These are each objective's point (a segmented building's core pose, through the newly ported
  `CDestructableSegmentsController::GetCentre`), the speaker's point, and the target box's choice with its clock read. The
  host draws from what that render kept and evaluates no pose.
- Drawn and matching the port: the objectives' screen markers, the eased crosshair, the guns panel, and the target box
  (the target's mesh turning in its own view, with its health bar).
- Open:
  - At P5000 the target's health bar ends about a pixel short of the port's full bar, on a route whose reference is
    known to drift from P4280.
  - Not drawn yet: the lock-on pieces and warning-receiver threats (neither is ported), the Thunderhead's imposter frame, the terrain's detail texture, and
    P2800's dome.

Measured (the repair pad route, 2026-10-22): on the tutorial pilot's p22-fire tape (c17-fire's input to P22150, then
the return to the Forseti repair pad and the flight out), against the native port's every-Present probes
(`l100-p22-port-20261022`, the frozen 0029+0030 build), the Core matches every probe field through P26720, the
reference's end, past the earlier floor at P22150 (`PilotP22RouteTests`). The pad takes the landed battle engine as its target at P23333
(`CRepairPadAI::Unknown11`: within 3 across and up to 5 below), creates its weapon's charge effects at the Charge
emitters (`CUnit::Unknown86`, the port's 18 extra C runtime draws there), fires once `CBuilding::Move`'s shot timer
readies the shot, and repairs the engine to full life at P23367. At P25290 the walker's augmentation is full and
`CBattleEngine::AugmentWeapon` (0040de40) augments its weapon; its "weapon augmented" HUD sample is the port's one extra
C runtime draw there (the port's call chain, logged under gdb, against ours). "Airborne Target 2 Destroyed" posts at
P25977. The planner's k11 tape (p22-fire's input to P26720, then on through the final drones) matches the
port's every-Present probes (`l100-k11-port-20261022`) on every field through P27400, with the kills at P27055 and
P27304, k13 (k11's input to P27240, then on) through P27450, with the fifth at P27324, and j4 (k13's input to
P27341, then the jet transform) through P27650, with the sixth and last at P27511 (`PilotFinalDronesRouteTests`).

Measured (the win, 2026-10-23): the pilot's winning tape (`pilot-win`: j4's input to P27569, then Target Zone 4 and the
walk in) under `-level 100 -skipfmv` against the native port's every-Present probes (`l100-pilotwin-port-20261022`, the
frozen 0029+0030 build, to P28600): every probe field matches every Present through P28306, the level's last. The level
is won at P28157; the Victory screen's countdown quits to the front end; the last MainLoop renders twice (P28305 and
P28306, both at mFrameTime = mBaseTime while paused) and the next one's update sees the quit and returns before
rendering (onslaught-decomp `src/game.cpp:2222`), so RunLevel's close follows with no Present. The "extra Present at
P28306" noted on 2026-10-22 was the comparison sampling the ended session a second time, not a missing Present.
END_LEVEL_DATA as the close fills it equals the port's 1,484 bytes (its ELD line at P28307). The rest of RunLevel is
modelled now (`LevelSession.Shutdown`): ShutdownRestartLoop deletes the random stream (`src/game.cpp:567`; the players
went at the close, `:1516`), and CGame::Shutdown's RunOutroFMV (`:1104`) marks the level's outro (cutscene 2 for level
100, `src/fmvlookup.cpp`) new among the goodies with one more pending extra goodie and plays it unskippable by select.
Under -skipfmv the film never ends, and the port's P28307-P28600 (294 Presents, 2,646 fields) equal ours: the game's
time, frame time, score, level and rand() standing, no random stream and no player.

Measured (a player's whole game, 2026-10-23): the `gold-own-20261023` tape (a maintainer's own keyboard and mouse
input from boot to the main menu's Quit, moved onto Onslaught CE 0.1.2's timing) against the published 0.1.2 package's
replay of it (`fe-gold-own-port-20261023`: shim clock, every Present, no game arguments, the tape's start files) on one
clock (`GameRun`, `GoldOwnRouteTests`): every probe field the port records (EVT, EVN, the front end's FE line, FT, SC,
LVL, RNG, CRT, POS, LIFE) matches every Present through P12817, the tape's end (117,330 fields); END_LEVEL_DATA equals
the port's at its change (P12167); the career saved at the debriefing (`BEA 1.bes`, 10,004 bytes) and the last
`defaultoptions.bea` (10,004 bytes) equal the port's byte for byte. What it took, each at its first difference: the
film player's harness pacing (a skip seen in the poll after a Present ends the film unpresented: retail's binkw32 finds
the next frame not yet due at its first look after each Present, measured on pristine 74154bfa under the shim; port
patch 0053), the goodies page (`CFEPGoodies`: the cursor, its sounds and Back's options save; the viewer is not
ported), the level's intro film from the front end (RunIntroFMV after the 100% loading screen, `src/game.cpp:1259`),
the tape's mouse outside a level adding up for the next level's first device read (the harness's DirectInput, as the
port's `PortReplayMouse`), and the cloak button (`CBattleEngine::HandleCloak`, 0040d4d0; the tutorial's engine has no
stealth, so it never cloaks). The run uses the level-100 routes' measured sound-pool profile (the frozen e55 -level
process's 256 seeds), not this process's; every field still matches. Retail is not the reference here: in each of the four
recorded retail pairs (pristine 74154bfa under the shim) the two runs of the same tape differ from each other, first
between P3918 and P5003.

Measured (frontend sprite presentation, 2026-10-24): the Host now draws the existing frontend sprite particles
through `ParticleView`, after the page and its depth masks and before the cursor. This adds the goodie glow/sparks
at debriefing and admits the main menu's firework sprites; particle meshes (including the goodie logo) and the
configuration model remain undrawn. The source projection is `CFrontEnd::RenderStart` (004685f0: near 0.1, far 700),
with the render ordering from `CDXFrontEnd::RenderEnd`. There is no new particle simulation.

The `gold-own-20261023` input was replayed through the actual offscreen Host to P12817. Particle drawing enabled,
disabled, and a frontend-only capture run produced identical complete flow traces (2,036,045 bytes, SHA-256
`838c83ea02a48cf1c4aa370ca971f77bbc13c9ff293a260ad5b9fbba7fd6bca6`). The consecutive P12220–12280 capture sequence
was inspected at native 640 × 480: blue rings enter from the left, move and fade behind the page, and follow its
transition. P12265's enabled/disabled control differs at 7,540 pixels in that effect. The private evidence owner is
`presentation-20261024` (`candidate-b`, `disabled-c`, `motion-d`); the frame sequence is also encoded at the replay
clock's 30 Presents per second. These are Godot pictures and a presentation-on/off control, not a new matched
retail frame comparison or GPU performance result. The enabled candidate also drew the level, while the
disabled and motion captures selected frontend-only drawing; this was not a single-switch whole-renderer
comparison. The unchanged traces cover the fields emitted by Host; the separate GoldOwn test compares the
larger reference set, end-level bytes and complete save/options files. These Host runs stop at the selected
P12817 preview boundary, not a demonstrated natural game quit. The captures use Intel Compatibility rendering.
Completed-campaign fireworks still need a visible comparison.

The full Host replay needs the explicitly selected `HOST_REPLAY_SOUND_PROFILE`, with a tape, the pinned measured
sound-pool file, one of the two recorded startup-options containers and the pinned English sample bank. The shared
`ReplaySoundProfile` admission replaces duplicated test setup; the whole-game test supplies its actual starting
options reader. The profile remains the previously measured native process's pool, not this replay process's or
retail's universal allocator state. It is silent. Ordinary-player Host runs still lack a configured sound-event
backend: without the explicit replay profile, this tape stops at the incoming-missile warning after P7429. That is
an open production-audio dependency, not a completed playable or audio acceptance route.

Fresh read-only observation of this reconstruction replay also distinguishes its win from a low-health shortcut:
all six planes spawned in the final armed wave (P10480–P10855) reached nonpositive life by P11380, before the
completion dialogue at P11618. The earlier three airborne targets died at P9687, P9763 and P9846. This agrees with
the end-level record's nine-aircraft count and is recorded in `presentation-20261024/diagnostic-combat.log`.
Training level 100 does not add these kills to the saved career counters. This does not add a
retail per-plane oracle. The route's existing every-Present probe and save-byte comparisons remain the native-port
acceptance evidence.

Presentation cannot consume the core's random stream: it evaluates sprite functions through a copied draw context
and refuses unadmitted random-valued functions. An authored headless negative confirmed refusal without changing
the core's random seed or call count. A second negative forced a Host flow presentation error and confirmed a
failed finish and process exit 1, rather than a logged exception followed by apparent success. Missing-tape,
changed-profile, changed-bank and changed-options refusals are checked independently. No simulated mission events
or gameplay constants were changed.

The final Parity run passed 836 cases with five skips: two existing occupancy tests deliberately refuse stale
native-port dumps, and three x87 oracle theories could not run because their retained tables were unavailable in
the inspected locations. These are limitations, not passes; no skip was added by this change. The Host built with
zero warnings/errors, and the documentation, public-payload and diff gates passed. The complete legacy clock and
tape checks ran after their existing references were routed into the private test environment.

Foundation admission and lifecycle (2026-10-25): `LevelSession.Restart` retains the route's canvas sink, so Retry
continues recording its HUD text, messages and menus. The Host consumes the core's existing `RenderFrameLength`
for cloud, water and surf phases; its former independent division differs from the compiled reciprocal on an actual
level-100 render schedule (`RenderFrameLengthTests`). Reference gates require their stated Presents and fields,
rather than accepting a surviving endpoint. Empty trace comparisons fail admission. The x87 oracle reader checks
nonempty aligned records, the manifest's exact counts and SHA-256 hashes, and the milestone's minimum case count;
absent tables still report skips. Authored admission negatives and an in-memory missing-probe copy of the retained
retail reference exercise refusals without changing reference files. These are engine-free checks; this change adds
no Godot picture, sound-device, ordinary-player or multiplayer acceptance.

Measured (the HUD's text, 2026-10-22): the core's HUD render writes the text of `Render00485d50` (the weapon's name,
fading in from a change and out after three seconds, and the warnings: two centred rows, each showing the first of its
lines still within five seconds) and `Render00486940` (a non-energy weapon's ammunition count), and the overlay draws it
through the canvas before the battle line, as `CDXEngine::PostRender` does. The HUD's part states (`mPartState`, the
script's `HighlightHudPart`/`UnHighlightHudPart`, the flash) are ported; the guns panel's sprites leave the stage at 2x,
so the name and warnings draw doubled while the weapon part shows. The objective panel (`Render00483530`) opens only for
a world text slot, which level 100's script never fills (it never calls `InitVariable`), so it draws nothing; the warning
receiver's line needs the receiver, which is not ported. Against the port's fe-lose back buffers every 100 Presents from
P1500 to P6200: the warning ("weapon overheating", P5200 to P6200) shows at the same Presents on both, with a mean
difference of 1.5 to 7.3 on its pixels (no pixel off by more than 40), no more than the level beneath it differs; neither
shows a weapon name (no weapon change on the route) or an ammunition count (an energy weapon). `HudTextTests` holds the
warning's place, colour and quads.
The ammunition count was compared on the tutorial pilot's c17 route (the Core equals the port on every probe field there
through P22150), where the walker's current weapon becomes the twin vulcan cannon, a store weapon, at P8533. Against
the frozen port's back buffers (ae04185f, every 25 Presents, P8500 to P9500), the count appears at the same Presents
(none at P8500 and P8525) and reads the same at every one (2000 to 1854 while firing). Its left edge matches to the
pixel, including the one-pixel shifts as the digits' widths change, and its colour is the plain one, not doubled
(`HudTextTests`). No tape presses the weapon-change button, so the weapon's name (shown only after a change) has no
port reference yet.

Measured (the HUD's parts, 2026-10-22): the host draws each HUD part only while the core's `HudParts` shows it
(`IsComponentVisible`): the compass's outer ring, the health and energy halves of its inner ring and their bar lines,
the radar outline, and the weapon's charge bar, heat bar and guns panel. Level 100's script highlights the compass
(P1668 to P1883), the radar (P1884 to P2073) and the current weapon (P3424 to P3690) on the fe-lose route
(`HudTextTests`). Against the port's back buffers every 25 Presents through those windows, each part is lit or dark
at the same sampled Presents (compass 011011011, radar 01101101, weapon charge 111011111010 on both). The health and
energy parts are never highlighted on this route.

Measured (the pan camera's orientation, 2026-10-22): `PanCamera.Facing` follows retail's inline block in
`CPanCamera::Update` (00419bb0..00419d07, the block the native port's patch 0036 reproduces): the magnitude summed
(Y*Y + Z*Z) + X*X, and the elevation and azimuth kept at full precision into fsin and fcos instead of rounded to float
for the out-of-line constructor. Against retail's own block run natively at control word 0x7F on 20,006 position
pairs (the retail oracle's `pan-camera-orientation-20261022` group, `PanCameraOracleTests`), all nine elements match on
every case; the earlier form missed 16,251 of them by a last bit or more.

The observed constants, from captures of `BEA.exe -forcewindowed -res 640 480` with no input (two retail processes
identical at every captured Present):

| Constant | Present | What retail shows |
| --- | --- | --- |
| `FrontEndInitPresents` | 2287 | completed Presents when `CFrontEnd::Init` starts; this total is unchanged. The earlier assignment of both preceding Presents to loading is superseded by the clock/call evidence above. |
| `TitleLastPresent` | 3188 | the title page's last frame with no input; the attract videos follow at 3189 |

Measured (M6 boot, 2026-10-15; code breakpoints logging completed Presents, two retail processes identical):
`CFrontEnd::Init` starts at 2287 completed Presents. Both of its `CFEPIntro::TransitionNotification` calls and its
first `CFrontEnd::Process` run at 2287, then one `Process` per Present. The title page's 30 s timeout quits in the
`Process` at 3188 = 2287 + 901, as the flow models it (`ThirtySecondsIdleOnTheTitleQuitsToTheAttractVideos`): from
the front end's first `Process` on, retail and the model agree to the Present.

Measured (M6 front end, 2026-10-15): a click tape from the title page to level 100 (fe-explore4: the title,
New Game, the name keyboard's forward arrow, a miss beside the level node, then forward through the level select,
briefing and configuration) replayed into the flow from `FrontEndInitPresents`. Every page change lands on retail's
Present (breakpoints on `CFrontEnd::SetPage`): Intro 2287, Main 2596, the name keyboard 2704, the save 2804,
the level select 2817 (13 Presents later, after the autosave), the briefing 3104 and the configuration 3304. The
front end's last `Process` is at 3504 with the level set to 100, as in retail
(`RetailChainTests.TheTitleToLevel100ClickChainSetsEachPageAtRetailsPresent`, which reads the player's own
installation for the fonts, texts and data). The front end's float expressions (frame-step filter, counters,
transitions, cursor and layout maths) still need a per-function oracle before they count as exact (the project's rule).

Measured so far (M0, 2026-10-15): the harness parses retail's level-100 tape and golden trace and reproduces its
138 / 94 / 0 / 0 / 138 changes (EVT / POS / LIFE / SC / RNG); no parity simulation exists yet, so every probe is
missing at the first sample.

Measured (M1, 2026-10-15): `X87` against oracle tables from a VC6 SP5 `/O2 /QIfist` harness run under Wine at
`_PC_24` on this host (Intel i7-13700HX), 10^6 inputs per operation including specials. Add, subtract, multiply,
divide and square root on double operands, float square root and atan2, double-to-float conversion, `/QIfist`'s
64-bit fistp and `_ftol` match every result. Float operators match on float operands. `fsin`, `fcos` and `fpatan`
match every result once stored to a float; stored to a double they differ in the last bits for about 0.13% of
arguments within ±2π (the double rounding of the FPU's 64-bit result) and about 10% up to ±10^4 (`fsin`'s 66-bit π),
a measured ceiling, not exactness. If an M4 probe ever diverges near a trigonometric result, the planned remedy is
an exact x87 emulation of `fsin`, `fcos` and `fpatan` (66-bit π argument reduction, 64-bit internal result, then the
store's rounding). The oracle also shows that VC6 at `/O2` does not round a `(float)` cast of a
double held in a register, so ports follow the compiled instruction sequence, not the source casts.

Measured (M3 start, 2026-10-15): the ported random stream contains all 139 distinct RNG values of the level-100
golden from seed 123456: 1,537 draws by Present 50, 2,470 by Present 3300 and 22,068 by Present 10000.

Porting notes for the parity core:

- The world's Z axis points down: heights are negative upward (actor.cpp's altitude is `MAP.Collide(pos) -
  COfGHeight()`, AirGuide.cpp's ground is `-MAP.GetHeight(x, y)`), so a ground normal pointing up has negative Z
  (`HeightField.Normal`; off the map it returns retail's (0, 0, 1)).
- Float expressions keep the decompilation's association order. With float operands at 24-bit precision control,
  C# float arithmetic in the same order gives retail's bits; double operands go through `X87`.
- The C runtime's `rand()` does not reach the probes in level 100: retail's callers over the 10,000-Present
  golden-640 run (a detour on `rand`, 348,459 calls) are the particle descriptors (render-paced, about 342,000),
  `CSnow::Init` at load (4,100) and `CSoundManager::PlayEffect` (26, sound parameters only). `CrtRand` must still be
  exact, in per-Present order, for render captures and sound. This holds per level: rerun the detour on each new
  level before relying on it.
- The mesh port (`Mesh/`) covers the resource-file reader (`CMesh::Deserialize`, the level's packed `m_*.aya` mesh
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

## The earlier rebuild's grades

These grades describe the earlier fixed-point rebuild at its parking point (the old tag archive/godot-rebuild-20260926, now in the private RE archive).

## Authority order (from PROVENANCE.md)

> **Port Stuart's shape first. Cite the file and line. Override from bytes
> only where measurement proves divergence.**

The pinned `references/Onslaught` source is the architecture and intent
authority. The pristine retail specimen (`74154bfa…`) is the behavior
authority. Where they disagree, the shipped bytes win — but only after a
measurement proves the divergence, and the divergence is recorded.

Since September 26 `Simulation` constructs World 110 from its materialized
static world through its start state, with Level 100's surviving base world
(the World 110 rows below). The earlier, separate World 110 construction stage
was retired that day in favour of that one owner; its retail evidence stays in
[`world-110-initial-constructor-seeds.md`](https://github.com/dlprentice/onslaught-preservation/blob/main/levels/world-110-initial-constructor-seeds.md)
and
[`world-110-player-start-admission.md`](https://github.com/dlprentice/onslaught-preservation/blob/main/levels/world-110-player-start-admission.md).

RE commit `d34f565d` (now in this branch) supplies the September 19
projectile-readiness queue and maximum-speed-provider contracts in
`reverse-engineering/binary-analysis/functions/CEventManager.cpp.md` and
`collisionseekingthing.cpp.md`. The existing explicit PC53 scheduler path agrees
with the controlled PC64 result for those bounded operands; it is not a general
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
| `CGame::DeclareLevelLost` countdown | 5.0 s | 2.0 s | this table (`0x0046F4A8`) |
| `CGame::FillOutEndLevelData` score below D | `game.cpp:1011-1024` stores −1 then clamps to 0 then 0.001 | `0x0046d772` stores 0 and jumps to `0x0046d79b`, skipping `0x3a83126f` | this table |
| `CPanCamera` length | Stuart value | 6.0 | PROVENANCE.md (VA 0x004198D0, vtable 0x005D92A8) |
| Weapon resource path | pinned path | differing path | PROVENANCE.md |

These are exceptions to record precisely, not templates for loose porting.

The September 8 Level100 aircraft update replaces the integer, constant-speed
Plane mover with retained raw pose/Euler/drive/velocity and the measured living
free-flight operation order. Its 32-call copied-retail comparison supplies
observed guide inputs; it does not close full aircraft parity. Production now
schedules the clearance cache and preserves its state through restore/hash,
but common AI/controller callbacks, avoidance memberships/readers, contacts,
effects and dying flight remain open. The September 12 update adds the
spawned controller's exit and scheduled Ready handoff, including separate
monitored/collision-ignore ownership and both altitude clamps. Original-code
experiments and in-process restore/ordering tests support this prefix; normal
control still resumes the older approximate script/weapon bridge. The unit's
evidence (kept in the private RE archive) separates those boundaries from the measured arithmetic.

## Carried retail contracts — entity, owner, implementation, test

Recorded 2026-08-17, with the Pulse Cannon increment, end-level countdown,
ReCalcLinks, FillOut, and FrontEndHandoffReady career-handoff rows added 2026-08-18, and the
Pulse Cannon ReadyToCharge / Charged-2 rows merged 2026-08-21, and the bounded
world-110 native-84/native-88/session rows added 2026-08-24 (the world-110
definition, seed and player-start admission rows of 2026-08-24 to 2026-08-31
were retired with the separate World 110 stage on 2026-09-26). Every retail
anchor below was **re-derived from the
pristine specimen** for this table (PE headers parsed directly; flat mapping
file offset = VA − 0x400000 for `.text`/`.rdata`/`.data`, whose raw ends are
0x005D8000 / 0x00622000 / 0x00661000 — `.rsrc` is **not** flat and 0x00672FD0 is
bss). Every row's mutation kill was **measured**: the named implementation was
changed to the plausible wrong value, the named test was observed to fail, the
owner was restored and verified byte-identical by SHA-256, and the test was
observed to pass. `Cases` is the number of test cases
`--filter FullyQualifiedName=<test>` selects, measured — it is the
`expectedTests` a rebuild-parity gate needs.

Owner paths are relative to the repository root; test names are relative to
`rebuild/OnslaughtRebuild.Core.Tests/` and the
`OnslaughtRebuild.Core.Tests` namespace.

> **Receipts.** The 17 mutation records behind this table — one per row plus the
> one deliberate `SURVIVED` control — live at
> `local-lab/rebuild-parity-mutation-kills-2026-08-17/`, with
> `mutation-results.json` (per row: target test, mutation meaning, every failing
> test observed, and `ownerSha256Before`/`After`), the `mutate.py` harness that
> produced them, the read-only specimen reader used to re-derive every anchor
> here, and `MANIFEST.sha256`. `local-lab/` is **ignored by git and invisible to
> a fresh clone**, so a clone sees this table's claims without its evidence;
> `local-lab/INDEX.md` carries the entry. Re-deriving a row costs one narrow
> `--filter` run, so treat a missing receipt as a reason to re-measure rather
> than to trust the table.
>
> The world-110 player-start row has its separate measured receipt in the
> ignored repo-local lab at
> `local-lab/rebuild-world110-player-start-mutation-kill-20260830/RECEIPT.md`,
> SHA-256
> `900f22187dea14262846d968a229e7a324ec1a292302c3214ddf656ec7e56b3d`.
> It is machine-local evidence inside the canonical checkout but excluded from
> Git and absent from fresh clones and child worktrees.
>
> The adjacent bounded `CStart::Init` height row has its own receipt at
> `local-lab/rebuild-world110-player-start-height-clamp-mutation-kill-20260830/RECEIPT.md`,
> SHA-256
> `9acb79d7a5e092725c1767358eb1d574853531b6caea0aa5ef30a752c6e03c40`.
> It records the four discriminating failures produced by inverting the strict
> comparison and the seven-test green run after byte-verified restoration.
>
> The standalone `CPlayer::AssignBattleEngine` row has its focused receipt at
> `local-lab/rebuild-player-assign-battle-engine-mutation-kill-20260830/RECEIPT.md`,
> SHA-256
> `63b97ad75ddb73a39c2f8a92a48c8471548c5c2fd93c1837e0788780aa9ca401`.
> Omitting the reciprocal graph mutation made the exact fresh-bind fact fail;
> byte restoration returned that fact to 1/1 and the adjacent gate to 54/54.
>
> The ordered authored-start assignment sequence has its first-only/final-only
> receipt at
> `local-lab/rebuild-world110-assignment-sequence-mutation-kill-20260831/RECEIPT.md`,
> SHA-256
> `bb600b6c439e24fc503a648c0203f8f6bf026a22d0d942d5cdadd922e1496c79`.
> Each wrong loop shape failed three ordered/per-match tests; exact source
> restoration returned the focused class to 11/11.
>
> The complete World-110 serialized initial-object seed row has its controlled
> amount/mode-swap receipt at
> `local-lab/rebuild-world110-all40-initial-object-seed-mutation-kill-20260830/RECEIPT.md`,
> SHA-256
> `fe300ff9fdfc13522922bdd81e860ecece1e54b521f719aeafec535d1b82e382`.
> The exact squad fact went RED, byte restoration returned it to 1/1, and the
> adjacent World-110/start/height/session/hash gate passed 66/66.

| Retail entity | Anchor, and what the bytes say | Owner | Implementation | Test | Cases | Mutation that was killed |
|---|---|---|---|---|---|---|
| Unit weapon-provider selection, bounded nonballistic route | `004fb840..004fbc8b`; `004fbc42..004fbc4b` admits only strictly greater scores, retaining linked-list order. Original-code probe uses empty spawner lists and current modes with nonnull nonballistic projectiles. | `rebuild/OnslaughtRebuild.Core/RetailWeaponSelection.cs` | `RetailUnitWeaponSelection.Select` (not yet wired to actor firing) | `RetailUnitWeaponSelectionTests.EqualScoresRetainAttachmentOrder` | 2 | `>` to `>=` makes both attachment orders fail; exact restoration and final 117/117 in `local-data/test-runs/linux-route-20260906-af1sa_l9/provider-last-wins-20260912.trx` and `provider-final-20260912.trx` |
| Unit provider raw distance, PC24/RN | `004fb89f..004fb8ea`; original vector `(0.1f,0.1f,1.1f)` produces `3f8df578`, widened arithmetic `3f8df579`. This isolated environment does not establish all retail backends. | `rebuild/OnslaughtRebuild.Core/RetailWeaponSelection.cs` | `RetailUnitWeaponSelection.RawDistance` | `RetailUnitWeaponSelectionTests.RawDistanceMatchesOriginalPc24Observations` | 4 | Widened double norm fails the fourth case; first three survived. Exact restoration and final 117/117 in the same owner's `provider-wide-distance-discriminator-20260912.trx`, `provider-mutations-20260912.results.json` and `provider-final-20260912.trx` |
| `CBattleEngineJetPart::GetFriction` slow-flight gate | `0x00411B39` `d81dd88b5d00` = `fcomp dword ptr [0x005D8BD8]`; that dword is `00 00 c0 3f` = `1.5f`; `test ah,1` at `0x00411B41` | `rebuild/OnslaughtRebuild.Core/Simulation.cs` | `Simulation.JetFrictionNumerator` | `RetailJetFrictionTests.CoreLadder_GatesTheInterpolatedArmAtOnePointFive` | 1 | gate back to `1_000` (retail 1.0) |
| `CBattleEngineJetPart::GetFriction` ladder | `0x00411AA0`; `0x005D8CC4`=0.99f, `0x005D8B9C`=0.98f, `0x005D8568`=1.0f, `0x005D8CC0`=3.0f, `0x005D8574`=0.01f; source `BattleEngineJetPart.cpp:609-635` | `rebuild/OnslaughtRebuild.Core/RetailJetFriction.cs` | `RetailJetFriction.GetFriction` | `RetailJetFrictionTests.GetFriction_GatesTheInterpolatedArmAtOnePointFive` | 4 | `SlowFlightSpeed` written as `1.0f` |
| `CCareer::SetSlot` | `0x004214EB` `cmp eax,0x100` / `jge`, over a store indexed `sar edx,5` into `[esi+edx*4+0x2408]` — a 1024-bit array reachable only to 256 | `rebuild/OnslaughtRebuild.Core/RetailCareerProgress.cs` | `RetailCareerSlots.SetSlot` | `RetailCareerProgressTests.Slots_GuardStopsAt256WhileTheStoreHolds1024` | 1 | guard raised to the store's real 1024 |
| `CCareerNode::SetBaseThingExistTo` | `0x0041B777` `mov edx,1`; `0x0041B77E` `sar eax,5`; `0x0041B781` `and ecx,0x1f`; `0x0041B786` `shl edx,cl` — bit 0 is the low bit | `rebuild/OnslaughtRebuild.Core/RetailCareerNodes.cs` | `RetailCareerNode.SetBaseThingExistTo` | `RetailCareerNodesTests.SetBaseThingExistTo_AddressesTheWordAndBitRetailDoes` | 5 | bits numbered from the top of the word |
| `CCareer::Load` kill-counter clamp | `0x00421245`/`0x0042124B` read `[ebx+0x23f4]` and `[ebx+0x23f8]` — **two** words only; `0x0042126A` `cmp eax,-0x40`, `0x0042126F` `cmp eax,0x40`, else `xor eax,eax` | `rebuild/OnslaughtRebuild.Core/RetailCareerKillCounters.cs` | `RetailCareerKillCounters.NormaliseOnLoad` | `RetailCareerKillCountersTests.NormaliseOnLoad_LeavesTheOtherThreeCountersAlone` | 1 | normalise all five counter words |
| `CEventManager::AddEvent(CScheduledEvent*)` | `0x0044B32E` `0fbf4604` = `movsx eax, word ptr [esi+4]` — the event number is int16 | `rebuild/OnslaughtRebuild.Core/RetailEventScheduler.cs` | `RetailEventScheduler.AddEvent` | `RetailEventSchedulerTests.AddEvent_StoresTheEventNumberAsSignedSixteenBits` | 1 | store the `int` the source signature advertises |
| `CPCController::GetAnalogueLeftX` | `0x0051465E` `fild dword ptr [eax]` then `0x00514660` `fmul dword ptr [0x005DC6E4]`, that dword `6f12833a` = `0.001f`; `0x3EB851EC` (`0.36f`) occurs **zero** times in the image, so `PCController.cpp`'s dead-zone stage never shipped | `rebuild/OnslaughtRebuild.Core/RetailAnalogueControls.cs` | `RetailAnalogueControls.NormalizeLeftX` | `RetailAnalogueControlsTests.NormalizeLeftX_MultipliesByTheRoundedReciprocalNotDividesByAThousand` | 5 | implement `PCController.cpp:155`'s `/1000.0f` |
| `CBattleEngine::GetInterpolatedEulerOrientation` seam wrap | `0x0040D660`; `0x005D85E4`=+1.5707963705062866, `0x005D85C8`=−1.5707963705062866, `0x005D85E0`=6.2831854820251465; no store between the wrap and its use | `rebuild/OnslaughtRebuild.Core/RetailBattleEngineInterpolation.cs` | `RetailBattleEngineInterpolation.AdjustedOldAngle` | `RetailBattleEngineInterpolationTests.AdjustedOldAngle_StaysWideAndThatIsObservable` | 1 | round the wrapped angle back to float |
| `CBattleEngine::HandleHostileEnvironment` | `0x0040DCE0` `fld [0x00672FD0]`, `0x0040DCEF` `fsub [esi+0x510]`, `0x0040DCF5` `fcomp [0x005D85D8]` = 5.0f, `test ah,0x41` — strictly greater, on the **unrounded** difference | `rebuild/OnslaughtRebuild.Core/RetailBattleEngineAugment.cs` | `RetailHostileEnvironment.ShouldWarn` | `RetailBattleEngineAugmentTests.ShouldWarn_ComparesTheUnroundedDifference` | 1 | narrow the elapsed difference to float |
| `CBattleEngineWalkerPart::GetWeaponAmmoCount` | `0x00414470`; `0x0041449D` `df7c2404` = bare `fistp qword ptr [esp+4]` under `/QIfist`, so it **rounds to nearest even** where `BattleEngineWalkerPart.cpp:854` casts | `rebuild/OnslaughtRebuild.Core/RetailWeaponStores.cs` | `RetailWeaponStoreReadouts.AmmoCount` | `RetailWeaponStoresTests.AmmoCount_RoundsToNearestEvenWhereTheSourceTextTruncates` | 6 | implement the source's truncating `(SINT)` cast |
| `CMovieCamera::GetZoom` | `0x0041A681` `fmul dword ptr [0x005D9338]`, that dword `610b363c` = `float(1/90)` and occurs **once** in the whole image, then `0x0041A687` `fmul [0x005D85EC]` = 0.5f — two multiplies where `Camera.cpp:650` writes two divides | `rebuild/OnslaughtRebuild.Core/RetailCameraLaws.cs` | `RetailMovieCameraZoom.GetZoom` | `RetailCameraLawsTests.MovieCameraZoom_MultipliesByTheRoundedReciprocalOfNinety` | 4 | implement `(fov/90)/2` |
| `CBattleEngine::Gravity` | `0x004074D0`; jump table `0x00407520` = {`0x004074FF`,`0x004074FF`,`0x004074FF`,`0x004074E8`} and `0x00407530` = {`0x00407506`,`0x004074FF`,`0x004074FF`,`0x0040750D`}, so **index 0** takes `0x005D8BAC` = `6f12033b` = 0.002f. Index 0 is `MORPHING_INTO_WALKER` (`BattleEngine.h:32`), not the walker | `rebuild/OnslaughtRebuild.Core/RetailBattleEngineGravity.cs` | `RetailBattleEngineGravity.Gravity` | `RetailBattleEngineGravityTests.Gravity_WalksBothJumpTables` | 6 | read the header as walker-first |
| `CBattleEngineWalkerPart::CanWeaponFire` | `0x0041463C` `8b889c000000` = `mov ecx,[eax+0x9C]` then `test ecx,ecx` / `je`. The jet's body `0x00412570`–`0x0041260B` contains the displacement `9c 00 00 00` **zero** times; the walker's contains it once | `rebuild/OnslaughtRebuild.Core/RetailWeaponSelection.cs` | `RetailWeaponFireGate.CanWalkerWeaponFire` | `RetailWeaponSelectionTests.CanWalkerWeaponFire_AddsTheActiveGateTheJetDoesNotHave` | 3 | share one body across both chassis |
| `CBattleEngineWalkerPart::GoingIntoWater` arm selector | `0x00413A70`; selector at `0x00413ABF` against `0x005D8CB4` = `9a99993e` = 0.3f, on the unrounded height | `rebuild/OnslaughtRebuild.Core/RetailWalkerWaterEntry.cs` | `RetailWalkerWaterEntry.GoingIntoWater` | `RetailWalkerWaterEntryTests.GoingIntoWater_TakesTheLowArmAtExactlyTheMarginAboveWater` | 1 | make the selector inclusive; **and** hard-wire one arm |
| `CBattleEngineJetPart::AutoLevel` | `0x00412900`; `0x0041293A` `fcomp dword ptr [0x005D8C60]`, that dword `0bd7233c` = 0.010000000707805157 = `float(0.1f)²` — **not** `0.01f`, which is `0ad7233c` at `0x005D8574` | `rebuild/OnslaughtRebuild.Core/RetailJetAutoLevel.cs` | `RetailJetAutoLevel.AutoLevel` | `RetailJetAutoLevelTests.AutoLevel_GatesOnTheSquaredManoeuvreSpeedWhenGrounded` | 5 | write the threshold as plain `0.01f` |
| `CChunkReader::Read` / `::Skip` over-read | `0x00423965` `imul esi,[esp+0x10]` then `0x00423971` `add edx,esi` — the charge is **unclamped**; `0x00423998` `sub eax,esi` wraps unsigned and `0x0042399A` resets the accounting to `Size`, so `CDXMemBuffer::Skip`'s `size>0` guard drops it | `rebuild/OnslaughtRebuild.Core/RetailChunkReader.cs` | `RetailChunkReader.Skip` | `RetailChunkReaderTests.Skip_AfterOverReadingAChunkIsASilentNoOpRatherThanARewind` | 1 | clamp the over-read charge at the chunk size |
| `CWeapon__AdvanceChargeProgressIfAnySlotAssigned` | `0x005068F0`; `0x005DB358`=`00 00 c8 43`=`400.0f`; `test ah,1` / `je` then `fld [record+8]` / `fadd [weapon+0x60]`. CanCharge scan starts at `record+0x10`. Level 100 `Pulse Cannon Pod` @`0x17463` of `default physics.dat` (`e1fb3ded…ada14`) has rate `0x41200000`=`10.0f` and levels 0+1 present, so ten 20 Hz samples fill MaxCharge 100 | `rebuild/OnslaughtRebuild.Core/RetailWeaponCharge.cs` and `rebuild/OnslaughtRebuild.Core/Level100PulseCannonCharge.cs` | `RetailWeaponCharge.Charge` | `RetailWeaponChargeTests.Charge_AddsTheRecordRateWhileBelowFourHundred` | 1 | cap written as `MaxCharge` (`100.0f`); **and** Pulse Cannon rate written as `1.0f` |
| `CGame::DeclareLevelLost` / `::DeclareLevelWon` countdown | `0x0046F4A8` `c7 43 48 00 00 00 40` = `mov [ebx+0x48], 0x40000000` = `2.0f`; instruction `0x0046F338` starts `mov ecx,ebx` and store `0x0046F33A` `c7 43 48 00 00 a0 40` = `5.0f` with its imm32 at `0x0046F33D` after `cmp eax,0x2E5` / `0x2E6` miss (`patches/PATCH-SURFACE-CATALOG.md`). Source `game.cpp:75` writes `GAME_COUNT_WHEN_LOST_OR_DRAW 5.0f` for both | `rebuild/OnslaughtRebuild.Core/RetailGameEndCountdown.cs` and `rebuild/OnslaughtRebuild.Core/Level100MissionTiming.cs` | `RetailGameEndCountdown.LostTicks` | `RetailGameEndCountdownTests.TutorialBroken_StartsTheTwoSecondLostCountdown` | 1 | Lost written as the source `5.0f` (`0x40A00000`) |
| `CCareer::ReCalcLinks` after Level 100 Won | `0x0041BDF0` calls `0x004496E0`; world 100 `level_structure[0]` is lower child node 1 / world 110 and higher `mToNode=-1`. Level 100 ships four primaries and no secondaries, so the predicate is the no-objectives FALSE; the lower link still completes (`Career.cpp:488-490`) and the dummy higher stays `CN_NOT_COMPLETE` | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerReCalcLinksTests.Level100Won_UnlocksWorld110EvenThoughTheSecondaryPredicateIsTheNoObjectivesFalse` | 1 | require `IsAllSecondaryObjectivesComplete` for the lower child too |
| `CGame::FillOutEndLevelData` Level 100 Won snapshot | `0x0046D470`; `mWorldFinished=100`, `mFinalState=5`, ten unset secondary statuses. `game.cpp:1028` `if (GetNumSecondaryObjectives())` is false, so the 0.4/0.6 ranking clamp is skipped even though `0x004496E0` would return FALSE | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.AfterSecondaryRankingClamp` | `RetailFillOutEndLevelDataTests.Level100Won_DoesNotClampRankingBecauseThereAreNoSecondaries` | 3 | apply the failed-secondary `0.6` cap when the authored count is 0 |
| `CGame::FillOutEndLevelData` Level 100 Won primary statuses | `0x0046D470` copies ten primary `GetStatus()` words: four `MOS_COMPLETE=1` then six unset — not the rebuild mission enum `Level100PrimaryObjectiveStatus.Complete=2`. Secondaries stay unset | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.ForLevel100Won` | `RetailFillOutEndLevelDataTests.Level100Won_SnapshotCarriesFourMosCompletePrimariesNotTheMissionEnumTwo` | 1 | write mission-enum `Complete=2` into the primary table |
| `CGame::FillOutEndLevelData` then `CCareer::Update` from Level 100 `FrontEndHandoffReady` | After the already-pinned Won 5.0 f countdown, `RestartLoopRunLevel` calls FillOut `0x0046D470` then Update `0x0041BD00`. Mission `FrontEndHandoffReady` is that seam: `ForLevel100Won()` then `ApplyUpdate`. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_AppliesFillOutAndUnlocksWorld110` | 1 | skip `ApplyUpdate` on the handoff |
| Level 100 player-input Won to `FrontEndHandoffReady` | GOAL.md: one `Level100ChainRunFixture` reaches `Won` by `SimInput` alone. After `SuccessCountdown`, `Step(SimInput.Idle)` for `RetailGameEndCountdown.WonTicks` lands `FrontEndHandoffReady` and `ApplyUpdate` unlocks world 110. Never `QueueExternalEvent`. First-play elapsed / iceberg-kill store-0 / secondaries / ChargeWeapon stay unclaimed | `rebuild/OnslaughtRebuild.Core/Level100Mission.cs` | `Level100Mission.AdvanceTick` | `Level100PlayerInputWonHandoffTests.SimInputOnlyWon_AfterSuccessCountdown_ReachesFrontEndHandoffAndUnlocksWorld110` | 1 | stop the idle steps before the overlay elapses |
| `CCareer::Update` Lost skip at Level 100 handoff | Lost is 4; `cmp eax,5` at `0x0041BD06` skips the 32-dword copy. `TryApply` returns false even if `FrontEndHandoffReady` is claimed. Broke-Tutorial never leaves `FailureMenuReady`. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.LostDoesNotApplyFillOutEvenIfFrontEndHandoffReadyIsClaimed` | 1 | drop the Won check on `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::Update` Level 100 first-play slots | First-play `SetSlotSave` writes `SLOT_TUTORIAL_1..4` (63..66). FillOut copies those 32 words; `ApplyUpdate` assigns them over career `mSlots`, so a leftover bit dies. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.FirstPlayTutorialSlotWords` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_OverwritesCareerSlotsFromFillOutTutorialBits` | 1 | `ForLevel100Won` carries empty slot words |
| `CGame::FillOutEndLevelData` Level 100 Won ranking | `game.cpp:967` stores `mRanking=1.0f` before the score-time arm. Level 100's secondary count is 0, so the 0.4/0.6 clamp never rewrites it. First-play elapsed and score stay unclaimed | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.ForLevel100Won` | `RetailFillOutEndLevelDataTests.Level100Won_SnapshotRankingIsThePreClampOnePointZero` | 1 | default ranking written as the failed-secondary `0.6` cap |
| `CGame::FillOutEndLevelData` Level 100 score-time arm | Last LoadWorld is outer RLWD. `0x0046d638` `fld [this+0x10c]` / `fsub [this+0x108]` / `fcomp 0.0` / `test ah,0x41` / `jne 0x0046d79b`. RLWD `+0x147ba/+0x147be` = 300.0 / 500.0 so the arm is live. Zero score vs last-wins D=70 stores 0 at `0x0046d772` (not pre-arm 1.0, not source 0.001). First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.AfterScoreTimeArm` | `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmRewritesAZeroScoreToZeroBecauseRlwdDeltaIsPositive` | 1 | invent the skip (leave pre-arm 1.0f) |
| `CGame::FillOutEndLevelData` Level 100 score-percentage last-wins | Last LoadWorld stores RLWD `+0x147c2` = 1.0 at `CGame+0x110` (`0x0050d301` `fstp [0x008a9ba8]`). Not the `LoadLevel` leftover 0.5. Mid-band elapsed therefore does not scale a 140 score. First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.Level100ScorePercentage` | `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmDoesNotScaleAMidScoreByElapsedBecauseScorePercentageIsOne` | 1 | adopt the `LoadLevel` leftover `0.5` |
| `CGame::FillOutEndLevelData` Level 100 exact-D score | Independently re-read specimen: `0x0046d724` `jl 0x0046d772` stores 0 only when fistp'd score is strictly below last-wins D=70. Equality interpolates to 0 then `0x0046d791` stores `0x3a83126f` (0.001). First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.AfterScoreTimeArm` | `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmStoresPointZeroZeroOneAtExactLastWinsD` | 1 | skip the 0.001 replacement |
| `CGame::FillOutEndLevelData` Level 100 last-wins S-score | Independently re-read specimen `74154bfa…` + inflated `100_res_PC.aya` `115ede05…2df4`: `0x0050d2f7` `mov [0x008a9b90], edx` from RLWD `+0x147ae` = 210. `0x0046d6e5` / `0x0046d6f9` / `0x0046d707` `jl` so equality stores `0x3f800000` at `0x0046d709`. Not leftover BSWD 1000. First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.Level100SGradeScore` | `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmStoresOneAtExactLastWinsS` | 1 | adopt leftover BSWD 1000 |
| `CGame::FillOutEndLevelData` Level 100 last-wins D-score | Independently re-read specimen: `0x0050d307` `mov [0x008a9b94], edx` from RLWD `+0x147b2` = 70. A fistp'd 70 against leftover BSWD 200 is strictly below D (`0x0046d724` `jl`) and stores 0 at `0x0046d772`. First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.Level100DGradeScore` | `RetailFillOutEndLevelDataTests.Level100Won_ScoreTimeArmUsesLastWinsDSeventyNotLeftoverBswdTwoHundred` | 1 | adopt leftover BSWD 200 |
| `CGame::FillOutEndLevelData` Level 100 first-play base-things | `[0x0085515c]` is 35 (At() membership: 27 type-8 + 6 type-35 + 2 type-37 `CSafeSide`). Store 1 at `0x006728f8+i*4` for `i=0..34`; `35..287` stay 0. Not the materializer's 33. Iceberg player-kill store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.FirstPlayBaseThingsLeft` | `RetailFillOutEndLevelDataTests.Level100Won_FillOutStoresOneForEachOfThirtyFiveBaseThings` | 1 | adopt the materializer's 33 visible units |
| `CGame::FillOutEndLevelData` Level 100 TF_DYING store-0 | Independently re-read specimen: `0x0046d4d1` `test byte [eax+0x2c],4` / `jne 0x0046d4df` stores 0. Null reader also stores 0. First-play still 1 at 0..34; player iceberg-kill values stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.BaseThingLeftWord` | `RetailFillOutEndLevelDataTests.Level100Won_FillOutStoresZeroWhenBaseThingIsDying` | 1 | store 1 on TF_DYING |
| `CCareer::UpdateBaseWorldExistsStuffForNode` after Level 100 Won | `Career.cpp:443-452` / `519-527`. `level_structure[0][3]==110` is the primary destination; `[0][4]==-1` so the secondary arm does not run. First-play zeros at `35..287` clear Blank's all-1s on world 110. World 100 stays Blank. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.UpdateBaseWorldExistsStuffForNode` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateCopiesFillOutBaseThingsOntoWorld110` | 1 | skip the copy (leave world 110 bit 35 set) |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateBaseWorldExistsStuffForNode` from Level 100 `FrontEndHandoffReady` | After the already-pinned Won countdown, first-play FillOut copies onto world 110. Mutation is skip `ApplyUpdate` on the handoff. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_CopiesFillOutBaseThingsOntoWorld110` | 1 | skip `ApplyUpdate` on the handoff |
| `CGame::FillOutEndLevelData` Level 100 first-play kills | Copy five dwords from `player+8` (`0x0046d60f`) if player 0 is live. `CPlayer__ctor` zeros `+8..+18`. Only `0x004d30d0` increments them. A first-play Won that never takes ConfirmedKill is `0,0,0,0,0`. Career `0x0041c180` still `je` world 100. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.FirstPlayThingsKilled` | `RetailFillOutEndLevelDataTests.Level100Won_FirstPlayKillReadoutIsFiveZerosUnlessConfirmedKill` | 1 | write a non-zero authored L100 kill vector |
| `CGame::FillOutEndLevelData` Level 100 player-null kill zeros | Independently re-read specimen: `0x0046d5f9` `mov eax,[ebp+0x2a4]` / `0x0046d5ff` `test eax,eax` / `je 0x0046d61d` stores five zeros at `0x00672e30`. Live player copies five dwords from `player+8` (`cmp eax,0x14`). First-play still has a live player 0. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.ThingsKilledReadout` | `RetailFillOutEndLevelDataTests.Level100Won_FillOutStoresFiveZeroKillsWhenPlayerPointerIsNull` | 1 | copy leftover words when the player pointer is null |
| `CGame::FillOutEndLevelData` Level 100 Won lost-reason | Independently re-read specimen: `DeclareLevelWon` `0x0046f323` stores 5 at `+0x28` and `0x0046f33a` stores 5.0f at `+0x48` — no store to `+0x114`. FillOut `0x0046d5d0` copies `[ebp+0x114]` to `0x00672e2c`. Init is 0 (`EndLevelData.h:27`). First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.LostReasonWord` | `RetailFillOutEndLevelDataTests.Level100Won_FillOutLostReasonStaysInitZeroBecauseDeclareWonDoesNotWriteIt` | 1 | leftover lost-string id 123 |
| `CGame::FillOutEndLevelData` Level 100 pre-arm score/time | Independently re-read specimen: `0x0046d5af` `mov ecx,[0x00672fd0]` / `0x0046d5bf` `mov eax,[ebp+0xf4]` / `0x0046d5c5` `mov [0x00672e28],ecx` / `0x0046d5cb` `mov [0x00672e24],eax`. Those are the only FillOut stores of `mTimeTaken` / `mScore`. Arm `0x0046d6ff` rewrites `CGame+0xf4` only. First-play elapsed and score stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs` | `RetailFillOutEndLevelData.ScoreWord` | `RetailFillOutEndLevelDataTests.Level100Won_FillOutScoreAndTimeAreThePreArmCopiesNotTheScaledRewrite` | 1 | leftover score 999 / leftover time 12.5 / post-arm scaled 105 |
| `CCareer::Update` ignores Level 100 primary statuses | FillOut copies four `MOS_COMPLETE=1` primaries, but `CCareer::Update` at `0x0041BD00` never reads that table. Writing mission-enum `Complete=2` does not change the graph | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateDoesNotConsultPrimaryStatuses` | 1 | require `MOS_COMPLETE=1` before completing the node |
| `CCareer::Update` Level 100 ranking target | `Career.cpp:396-406` stores `mRanking` only on `GetNodeFromWorldNo(mWorldFinished)`. World 100 gets the already-pinned FillOut 1.0f (grade S). World 110 stays `BlankRanking` `-1.0f` (grade E) and incomplete | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateWritesRankingOnlyOnTheFinishedWorld` | 1 | copy the snapshot ranking onto the unlocked child too |
| `CGame::FillOutEndLevelData` then `CCareer::Update` Level 100 ranking target | `FrontEndHandoffReady` applies the already-pinned finished-world ranking: world 100 is 1.0f / S, world 110 stays `BlankRanking` / E. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_StoresFillOutRankingOnlyOnWorld100` | 1 | stamp FillOut ranking onto the unlocked child after `TryApply` |
| `CCareer::Update` Level 100 ranking only-if-greater | `Career.cpp:405-406` stores `mRanking` only when the snapshot is strictly greater. A worse Level 100 replay leaves the already-pinned first-play 1.0f / S. Score-time stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateDoesNotDowngradeAnExistingBetterRanking` | 1 | assign the snapshot ranking even when it is not greater |
| `CCareer::Update` Level 100 mNumAttempts skip | `Career.cpp:379-418` never writes `CCareerNode+0x38`. `CCareerNode::Blank` at `Career.cpp:99` / `0x0041B740` is the only store. Leftover 7 on world 100 and 11 on world 110 survive first-play S even though Complete / Ranking / CareerInProgress change. Isolated Blank ctor 0 does not go through ApplyUpdate. Existing ranking / Complete tests do not name `+0x38`. FrontEndHandoff leftover now names the same 7 / 11 through `TryApply`. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateDoesNotIncrementNumAttempts` | 1 | increment the finished node's `mNumAttempts` |
| `CGame::FillOutEndLevelData` then `CCareer::Update` wait for the Level 100 Won countdown | `RestartLoopRunLevel` FillOut at `game.cpp:1552` after the main loop quits; that quit waits for the already-pinned 5.0 f store (`game.cpp:1997-2004`). `CFrontEnd::Init` then calls `CAREER.Update` (`FrontEnd.cpp:67`). `TryApply` returns false on `SuccessCountdown`. Score-time stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.SuccessCountdownDoesNotApplyFillOutEvenIfWonIsClaimed` | 1 | accept `SuccessCountdown` on `TryApply` |
| `CCareer::UpdateGoodieStates` after Level 100 Won | `0x0041c470`. Training is not an early-out. Re-read on specimen `74154bfa…`: `0x0041c4c7` `83 3a 64` = `cmp [edx], 0x64`; `0x0041de68` `6a 43` = `push 'C'`; `0x0041ea4f` `6a 42` = `push 'B'` (one byte after the note's `0x0041ea4e`); `0x0041f70e` `6a 41` = `push 'A'`. `COMPLETE_LEVEL(100)` stores `GS_NEW=2` on 0 and 8; `GRADE(100) >= C/B/A` stores 78 / 121 / 164. FillOut 1.0f is S so all five unlock. Score-time / base-things / kill totals stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateUnlocksTrainingGoodiesForAnS` | 1 | skip `UpdateGoodieStates` after a Won `ApplyUpdate` |
| `CCareer::UpdateGoodieStates` Level 100 grade bands | Same body. Ranking 0.25f is already pinned as C (`'D' - floor(0.25*4)`). `GRADE(100) >= C` unlocks 78; `>= B` / `>= A` stay closed so 121 and 164 stay `GS_UNKNOWN`. Score-time stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeCUnlocksOnlyTheCTrainingGoodies` | 1 | unlock 121 / 164 on any complete |
| `CCareer::UpdateGoodieStates` Level 100 B-grade band | Same body. Ranking 0.5f is already pinned as B. `GRADE(100) >= B` unlocks 121; `>= A` stays closed so 164 stays `GS_UNKNOWN`. Cite `0x0041ea4f` / `0x0041f70e`. Iceberg store-0 and first-play elapsed stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeBUnlocksOnlyThroughTheBTrainingGoodies` | 1 | unlock 164 on B |
| `CCareer::UpdateGoodieStates` Level 100 A-grade band | Same body. Ranking 0.75f is already pinned as A. `GRADE(100) >= A` unlocks 164. Cite `0x0041f70e`. Iceberg store-0 and first-play elapsed stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeAUnlocksTheATrainingGoodie` | 1 | skip the A arm |
| `CCareer::UpdateGoodieStates` Level 100 E-grade Won | Same body. Ranking 0.0f is already pinned as E (`0x00421499` `mov al,0x45`). A zero-score FillOut still Wins, so `COMPLETE_LEVEL(100)` writes 0 and 8; `GRADE(100) >= C` stays closed. Cite `0x00421499` / `0x0041de68`. Iceberg store-0 and first-play elapsed stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeEUnlocksOnlyTheCompleteTrainingGoodies` | 1 | unlock 78 on any complete |
| `CCareer::UpdateGoodieStates` Level 100 D-grade band | Same body. Ranking 0.001f is already pinned as the score-time exact-D replacement (`0x3a83126f`). That is D (`'D' - floor(0.001*4)`), so `GRADE(100) >= C` stays closed. Iceberg store-0 and first-play elapsed stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeDUnlocksOnlyTheCompleteTrainingGoodies` | 1 | treat any ranking above 0 as C |
| `CCareer::UpdateGoodieStates` Level 100 GRADE(110) C-goodie | Same body. `Career.cpp:691` is `if (GRADE(110) >= GRADE_C) SET_GOODIE_NEW(1)`. After a C-grade Level 100 Won, world 110 is still incomplete / BlankRanking so `GRADE` is the already-pinned incomplete `'E'` (`0x0041C3FE`) and goodie 1 stays `GS_UNKNOWN`. Existing C-band tests unlock 78 and do not name goodie 1 or `NewGoodieCount`. Isolated closed concept-art now names 79. A `GRADE(100)` mutation on the S path is not unique versus the latch count of 5. Do not invent the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeCLeavesTheWorld110CGoodieUnknown` | 1 | store `GS_NEW` on goodie 1 after the CountGoodies add |
| `CCareer::UpdateGoodieStates` Level 100 GRADE(110) C concept-art goodie closed | Same body. `Career.cpp:770` is `if (GRADE(110) >= GRADE_C) SET_GOODIE_NEW(79)`. After a C-grade Level 100 Won, world 110 is still incomplete / BlankRanking so `GRADE` is the already-pinned incomplete `'E'` (`0x0041C3FE`) and goodie 79 stays `GS_UNKNOWN`. Isolated closed GRADE(110) names 1, not 79. Isolated leftover C 79 names the open store. FrontEndHandoff leftover 79 names TryApply. Existing C-band tests unlock 78 and do not name 79. Skipping `SET_GOODIE_NEW(79)` is not unique versus leftover C concept-art. Do not invent `GRADE(110) >= B` / `A` or a world-110 FillOut. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateGradeCLeavesTheWorld110CConceptArtGoodieUnknown` | 1 | store `GS_NEW` on goodie 79 after ApplyUpdate when world 110 is incomplete |
| `CCareer::UpdateGoodieStates` Level 100 leftover GRADE(110) C-goodie | Same body. Leftover world-110 complete + ranking 0.25f (already pinned as C) opens `Career.cpp:691`. Isolated `GradeByteForWorld` already returns C for complete+0.25 and does not go through ApplyUpdate. The closed first-play pin does not name the store. Cold-slice latch tests stay at 5 because they do not seed leftover complete-110. Do not invent a world-110 FillOut. The leftover `COMPLETE_LEVEL(110)` store is not named here. Isolated leftover C concept-art now names 79. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateLeftoverWorld110CompleteCUnlocksTheWorld110CGoodie` | 1 | skip `SET_GOODIE_NEW(1)` when `GRADE(110) >= C` |
| `CCareer::UpdateGoodieStates` Level 100 leftover GRADE(110) C concept-art goodie | Same body. `Career.cpp:770` is `if (GRADE(110) >= GRADE_C) SET_GOODIE_NEW(79)`. Leftover world-110 complete + ranking 0.25f opens that store. Isolated leftover C names 1, not 79. Isolated leftover C CountGoodies is now 6 because 79 is also New; skip `SET_GOODIE_NEW(79)` is not unique versus that count. Isolated leftover 14 CountGoodies uses ranking 0.0f so `GRADE(110) >= C` stays closed. Isolated first-play closed GRADE(110) names 1; isolated first-play closed concept-art names 79 at `GS_UNKNOWN`. World 110 is in the cold slice. FrontEndHandoff leftover of this seed now names 79 through `TryApply`. Do not invent `GRADE(110) >= B` / `A` or a world-110 FillOut. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateLeftoverWorld110CompleteCUnlocksTheWorld110CConceptArtGoodie` | 1 | skip `SET_GOODIE_NEW(79)` when `GRADE(110) >= C` |
| `CCareer::UpdateGoodieStates` Level 100 COMPLETE_LEVEL(110) goodie | Same body. `Career.cpp:704` is `if (COMPLETE_LEVEL(110) ) SET_GOODIE_NEW(14)`. After a C-grade Level 100 Won, world 110 is unlocked but still incomplete, so goodie 14 stays `GS_UNKNOWN`. Existing GRADE(110) tests name goodie 1 and do not name goodie 14. Leftover complete-110 plus ranking 0.0f now names the open store. Do not invent the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateLeavesTheWorld110CompleteGoodieUnknown` | 1 | store `GS_NEW` on goodie 14 after the CountGoodies add |
| `CCareer::UpdateGoodieStates` Level 100 leftover COMPLETE_LEVEL(110) goodie | Same body. Leftover world-110 complete + ranking 0.0f (already pinned as E) opens `Career.cpp:704`. Isolated `CompleteFlagOf` / the closed first-play pin do not go through leftover ApplyUpdate: deleting the `COMPLETE_LEVEL(110)` arm still leaves goodie 14 at `GS_UNKNOWN` on first-play. The leftover GRADE(110) C test already seeds leftover complete-110 + 0.25f and now also opens 14 in `Update()` but does not name goodie 14. Ranking 0.0f keeps `GRADE(110) >= C` / goodie 1 closed. Cold-slice latch tests stay at 5 because they do not seed leftover complete-110. Lost leftover of the same seed now names the Lost return. Do not invent a world-110 FillOut or the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateLeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie` | 1 | skip `SET_GOODIE_NEW(14)` when `COMPLETE_LEVEL(110)` |
| `CCareer::UpdateGoodieStates` Level 100 Lost leftover COMPLETE_LEVEL(110) goodie | Same body, via the Lost early return (`Career.cpp:382-385`). Leftover world-110 complete + ranking 0.0f still opens `SET_GOODIE_NEW(14)` because Lost still calls `UpdateGoodieStates` then returns. Isolated Won leftover 14 does not go through Lost ApplyUpdate. Existing Lost zeros name 0/8/78/121/164 and do not seed leftover complete-110 or name goodie 14. Lost latch does not name 14. Ranking 0.0f keeps `GRADE(110) >= C` / goodie 1 closed. World 100 stays incomplete so the five first-play S slots stay `GS_UNKNOWN`. Do not invent a world-110 FillOut or the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Lost_ApplyUpdateLeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie` | 1 | skip `UpdateGoodieStates` on the Lost return |
| `CCareer::UpdateGoodieStates` Level 100 new-goodie latch | Same body. `CountGoodies` (`Career.cpp:670-680`) counts `mState >= GS_NEW`. First-play S raises that count by five; `new_goodie_count` at `0x00662B20` adds the delta (`Career.cpp:895-897`) and `first_goodie` at `0x00662B24` latches because goodie 0 left `GOODIE_NOT_DONE` (`Career.cpp:688 / 899-900`). `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.CountGoodies` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateAddsFiveNewGoodiesAndLatchesFirstGoodie` | 1 | leave the globals at ctor 0 |
| `CCareer::UpdateGoodieStates` Level 100 replay CountGoodies delta | Same body. A second `ApplyUpdate` of the already-pinned first-play S does not raise `CountGoodies`: `SET_GOODIE_NEW` stores only when `GOODIE_NOT_DONE` (`Career.cpp:564-566`). `new_goodie_count` therefore adds 0 (`Career.cpp:895-897`) and `first_goodie` stays latched. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateReplayDoesNotAddTheSameFirstPlayGoodiesAgain` | 1 | add `CountGoodies` without subtracting the previous total |
| `CCareer::UpdateGoodieStates` Level 100 SET_GOODIE_NEW GS_OLD overwrite | Same body. Seeding the five first-play S slots as `GS_OLD` leaves them at 3: `SET_GOODIE_NEW` stores only when `GOODIE_NOT_DONE` (`Career.cpp:564-566`). Replay of already-`GS_NEW` does not uniquely prove this. `new_goodie_count` / `first_goodie` stay ctor 0 because CountGoodies does not rise and goodie 0 was already done. Isolated leftover Old count now names that `CountGoodies` read of 5. FrontEndHandoff leftover now names the same five slots through `TryApply`. Isolated leftover `GS_INSTRUCTIONS` now names the store-when-not-done side of the same `<=`. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerGoodies.SetNewIfNotDone` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateDoesNotOverwriteAlreadyOldTrainingGoodies` | 1 | store `GS_NEW` even when `mState > GS_INSTRUCTIONS` |
| `CCareer::CountGoodies` Level 100 leftover GS_OLD count | Same owner. `Career.cpp:670-680` counts `mState >= GS_NEW`. Seeding the five first-play S slots as `GS_OLD` therefore reads 5 before ApplyUpdate. Isolated leftover `GS_OLD` `NewGoodieCount=0` does not uniquely prove this: the delta is 0 whether `previouslyNew` is 0 or 5. The latch names first-play Unknown-to-New. Replay names already-`GS_NEW`. Isolated leftover `GS_INSTRUCTIONS` store names the write to 2. Isolated leftover Instructions count names the skip of state 1. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.CountGoodies` | `RetailCareerCampaignApplyUpdateTests.Level100Won_CountGoodiesCountsLeftoverOldTrainingGoodies` | 1 | count only `== GS_NEW` |
| `CCareer::CountGoodies` Level 100 leftover GS_INSTRUCTIONS count | Same owner. `Career.cpp:670-680` counts `mState >= GS_NEW`. Seeding the five first-play S slots as `GS_INSTRUCTIONS` therefore reads 0 before ApplyUpdate. Isolated leftover `GS_INSTRUCTIONS` store names the write to 2, not the pre-count. Isolated leftover Old count expects 5. FrontEndHandoff leftover Instructions names the store through `TryApply`. Counting only `== GS_NEW` still reads 0 on this seed. Do not invent `SET_GOODIE_INSTRUCTION` or episode instruction marks. `mPendingExtraGoodies` stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.CountGoodies` | `RetailCareerCampaignApplyUpdateTests.Level100Won_CountGoodiesDoesNotCountLeftoverInstructionTrainingGoodies` | 1 | count `mState >= GS_INSTRUCTIONS` |
| `CCareer::CountGoodies` Level 100 leftover COMPLETE_LEVEL(110) goodie | Same owner. `Career.cpp:670-680` iterates every slot. Leftover world-110 complete + E writes `GS_NEW` on goodie 14 while a C-grade Level 100 Won writes 0 / 8 / 78, so the count is 4. Isolated leftover 14 names the write of 14, not the count. Isolated leftover Old / Instructions counts seed only the five first-play S slots. Isolated C-grade does not name `NewGoodieCount` (would be 3). Isolated S latch is 5 without leftover 14. Skipping `SET_GOODIE_NEW(14)` is not unique versus leftover 14. Do not invent a world-110 FillOut. `mPendingExtraGoodies` stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.CountGoodies` | `RetailCareerCampaignApplyUpdateTests.Level100Won_CountGoodiesCountsLeftoverWorld110CompleteGoodie` | 1 | count only the five first-play S slots |
| `CCareer::CountGoodies` Level 100 leftover GRADE(110) C-goodie | Same owner. `Career.cpp:670-680` iterates every slot. Leftover world-110 complete + C writes `GS_NEW` on goodie 1, leftover 14, and leftover 79 while a C-grade Level 100 Won writes 0 / 8 / 78, so the count is 6. Isolated leftover C names the write of 1, not the count. Isolated leftover C concept-art names the write of 79, not the count. Isolated leftover 14 CountGoodies uses ranking 0.0f so the count is 4. Isolated leftover Old / Instructions counts seed only the five first-play S slots. Isolated C-grade does not name `NewGoodieCount` (would be 3). Isolated S latch is 5 without leftover 1 / 14 / 79. Skipping `SET_GOODIE_NEW(1)` is not unique versus leftover C. Skipping `SET_GOODIE_NEW(14)` is not unique versus leftover 14. Skipping `SET_GOODIE_NEW(79)` is not unique versus leftover C concept-art. Counting only the five first-play S slots is not unique versus leftover 14 CountGoodies. Do not invent a world-110 FillOut. `mPendingExtraGoodies` stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.CountGoodies` | `RetailCareerCampaignApplyUpdateTests.Level100Won_CountGoodiesCountsLeftoverWorld110GradeCGoodie` | 1 | skip counting goodie 1 |
| `CCareer::UpdateGoodieStates` Level 100 SET_GOODIE_NEW GS_INSTRUCTIONS store | Same body. Seeding the five first-play S slots as `GS_INSTRUCTIONS` writes 2: `SET_GOODIE_NEW` stores when `mState <= GS_INSTRUCTIONS` (`Career.cpp:564-566`). Isolated leftover `GS_OLD` and FrontEndHandoff leftover `GS_OLD` name the skip for state 3. FrontEndHandoff leftover of the same seed now names the store through `TryApply`. FrontEndHandoff S starts at `GS_UNKNOWN`. Replay names already-`GS_NEW`. Do not invent `SET_GOODIE_INSTRUCTION` or episode instruction marks — leftover seed only. `mPendingExtraGoodies` stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerGoodies.SetNewIfNotDone` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateStoresNewOverInstructionTrainingGoodies` | 1 | skip `SET_GOODIE_NEW` when `mState == GS_INSTRUCTIONS` |
| `CCareer::UpdateGoodieStates` Level 100 GetAndReset replay latch | Same body. `GetAndResetGoodieNewCount` / `GetAndResetFirstGoodie` (`Career.cpp:1411-1424`) consume the first-play latch. A second `ApplyUpdate` then leaves both globals at 0: CountGoodies delta is 0 and `first_goodie` is transition-only (`Career.cpp:688 / 899-900`). Replay without reset leaves the latch at 1 either way. Isolated GetAndReset is already pinned. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateReplayAfterGetAndResetLeavesGoodieLatchesClear` | 1 | re-arm `first_goodie` whenever goodie 0 is currently `GS_NEW` |
| `CCareer::UpdateGoodieStates` Level 100 Lost latch | Same body, via the Lost early return (`Career.cpp:382-385`). World 100 stays incomplete, so CountGoodies delta is 0 (`Career.cpp:895-897`) and `first_goodie` stays ctor 0: goodie 0 was `GOODIE_NOT_DONE` and still is (`Career.cpp:688 / 899-900`). Existing Lost goodie-state zeros do not name these two globals. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerUpdateGoodieStates.cs` | `RetailCareerUpdateGoodieStates.Update` | `RetailCareerCampaignApplyUpdateTests.Level100Lost_ApplyUpdateLeavesGoodieLatchesAtCtorZero` | 1 | arm `first_goodie` whenever goodie 0 was `GOODIE_NOT_DONE` at entry |
| `CCareer::Update` Level 100 Lost skips mSlots | Lost returns before `mSlots = END_LEVEL_DATA.mSlots` (`Career.cpp:382-385` / `392`). FillOut still carries first-play `SLOT_TUTORIAL_1..4` (63..66); ApplyUpdate does not assign them, so leftover career bits stay. Isolated `ShouldOverwriteFromEndLevel` does not go through ApplyUpdate. Existing Lost goodie/latch tests do not name slots. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Lost_ApplyUpdateDoesNotOverwriteCareerSlotsFromTheFillOutSnapshot` | 1 | overwrite `mSlots` on the Lost return |
| `CCareer::Update` Level 100 Lost skips ReCalcLinks base things | Lost returns before `ReCalcLinks` / `UpdateBaseWorldExistsStuffForNode` (`Career.cpp:382-385` / `416` / `443-452` / `519-527`). FillOut still carries first-play 1 at 0..34 and 0 at 35..287; ApplyUpdate does not copy them, so leftover Blank all-1s on world 110 stay set. Isolated `UpdateBaseWorldExistsStuffForNode` and `Level100Lost_DoesNotTouchTheTrainingGraph` do not name leftover bits (graph test passes no `mBaseThingsLeft`). Existing Lost goodie/latch/mSlots tests do not name them. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerReCalcLinks.cs` | `RetailCareerCampaign.ApplyUpdate` | `RetailCareerCampaignApplyUpdateTests.Level100Lost_ApplyUpdateDoesNotCopyFillOutBaseThingsOntoWorld110` | 1 | copy FillOut `mBaseThingsLeft` onto world 110 on the Lost return |
| `CCareer::UpdateThingsKilled` after Level 100 Won | `0x0041c180`. `cmp eax,0x64` / `je` at `0x0041c188` still refuses world 100. A non-zero FillOut kill vector does not accumulate. ConfirmedKill increment values stay unclaimed. Iceberg store-0 and first-play elapsed stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailCareerProgress.cs` | `RetailCareerCounters.UpdateThingsKilled` | `RetailCareerCampaignApplyUpdateTests.Level100Won_ApplyUpdateDoesNotAccumulateThingsKilledForWorld100` | 1 | drop the equality skip |
| `CBattleEngine::ConfirmedKill` Level 100 allegiance gate | Independently re-read specimen: `0x0040a564` `cmp [eax+0x138],1` / `jne 0x0040a57d` skips `0x004d30d0`. One inbound `E8` at `0x0040a578`. First-play totals stay unclaimed — a Won that never takes this path still snapshots five zeros. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailConfirmedKill.cs` | `RetailConfirmedKill.Apply` | `RetailConfirmedKillTests.Level100Won_ConfirmedKillDoesNotIncrementWhenThingAllegianceIsNotOne` | 1 | increment even when `+0x138` is not 1 |
| `CBattleEngine::ConfirmedKill` Level 100 incrementer remaining slots | Independently re-read specimen: `0x004d30eb` `test [eax+0x34],0x40000` / `inc [ecx+0x10]`; `0x004d30fa` `test dh,0x40` = `0x4000` / `inc [ecx+0x14]`; `0x004d3105` `test dh,8` = `0x800` / `inc [ecx+0x18]`. First-play totals stay unclaimed. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailConfirmedKill.cs` | `RetailConfirmedKill.Apply` | `RetailConfirmedKillTests.Level100Won_ConfirmedKillIncrementsSlotTwoOnFlag40000` | 1 | write `0x40000` into slot 0 |
| `CBattleEngine::ConfirmedKill` Level 100 null player-reader skip | Independently re-read specimen: `0x0040a56d` `mov ecx,[ecx+0x574]` / `0x0040a573` `test ecx,ecx` / `je 0x0040a57d` skips `0x004d30d0`. Bytes are a null pointer, not source `ToRead()` dying-flag. First-play totals stay unclaimed. Iceberg store-0 stays open. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailConfirmedKill.cs` | `RetailConfirmedKill.Apply` | `RetailConfirmedKillTests.Level100Won_ConfirmedKillDoesNotIncrementWhenPlayerReaderIsNull` | 1 | increment even when the reader is null |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` from Level 100 `FrontEndHandoffReady` | After the already-pinned Won countdown, first-play FillOut 1.0f / S unlocks goodies 0, 8, 78, 121, and 164. Cite `0x0041de68` / `0x0041ea4f` / `0x0041f70e`. Score-time / base-things / kill totals stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_UnlocksTrainingGoodiesForAnS` | 1 | skip `ApplyUpdate` on the handoff |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff latch | Same seam. First-play S raises `CountGoodies` by five; `new_goodie_count` at `0x00662B20` adds the delta (`Career.cpp:895-897`) and `first_goodie` at `0x00662B24` latches because goodie 0 left `GOODIE_NOT_DONE` (`Career.cpp:688 / 899-900`). Isolated ApplyUpdate latch does not go through `TryApply`. Existing FrontEndHandoff goodie-state test does not name these two globals. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_AddsFiveNewGoodiesAndLatchesFirstGoodie` | 1 | clear both globals after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff leftover COMPLETE_LEVEL(110) goodie | Same seam. Leftover world-110 complete + ranking 0.0f (already pinned as E) opens `Career.cpp:704` through `TryApply`. Isolated leftover 14 names ApplyUpdate and does not go through `TryApply`. Lost leftover 14 names ApplyUpdate's Lost return, not `TryApply`. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 14. Ranking 0.0f keeps `GRADE(110) >= C` / goodie 1 closed. Do not invent a world-110 FillOut or the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie` | 1 | clear goodie 14 after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff leftover GRADE(110) C-goodie | Same seam. Leftover world-110 complete + ranking 0.25f (already pinned as C) opens `Career.cpp:691` through `TryApply`. Isolated leftover C names ApplyUpdate and does not go through `TryApply`. FrontEndHandoff leftover 14 names 14, not 1. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 1. Do not invent a world-110 FillOut or the rest of the table. The leftover `COMPLETE_LEVEL(110)` store is not named here. Isolated leftover C concept-art names 79 through ApplyUpdate and is not named here. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteCUnlocksTheWorld110CGoodie` | 1 | clear goodie 1 after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff leftover GRADE(110) C concept-art goodie | Same seam. Leftover world-110 complete + ranking 0.25f (already pinned as C) opens `Career.cpp:770` through `TryApply`. Isolated leftover C 79 names ApplyUpdate and does not go through `TryApply`. FrontEndHandoff leftover C names 1, not 79. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 79. Do not invent `GRADE(110) >= B` / `A` or a world-110 FillOut. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteCUnlocksTheWorld110CConceptArtGoodie` | 1 | clear goodie 79 after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff GRADE(110) C-goodie closed | Same seam. First-play S through `TryApply` leaves world 110 incomplete so `GRADE` is the already-pinned incomplete `'E'` (`0x0041C3FE`) and goodie 1 stays `GS_UNKNOWN`. Isolated closed GRADE(110) names ApplyUpdate and does not go through `TryApply`. Leftover C FrontEndHandoff names 1 as New. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 1. Isolated closed concept-art now names 79 on ApplyUpdate. Do not invent a world-110 FillOut or the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeavesTheWorld110CGoodieUnknown` | 1 | store `GS_NEW` on goodie 1 after `TryApply` when world 110 is incomplete |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff GRADE(110) C concept-art goodie closed | Same seam. First-play S through `TryApply` leaves world 110 incomplete so `GRADE` is the already-pinned incomplete `'E'` (`0x0041C3FE`) and goodie 79 stays `GS_UNKNOWN`. Isolated closed concept-art names ApplyUpdate and does not go through `TryApply`. FrontEndHandoff closed GRADE(110) names 1, not 79. Leftover C FrontEndHandoff names 79 as New. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 79. Skipping `SET_GOODIE_NEW(79)` is not unique versus leftover C concept-art. Skipping `ApplyUpdate` on the handoff is not unique versus existing FrontEndHandoff tests. Do not invent `GRADE(110) >= B` / `A` or a world-110 FillOut. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeavesTheWorld110CConceptArtGoodieUnknown` | 1 | store `GS_NEW` on goodie 79 after `TryApply` when world 110 is incomplete |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff COMPLETE_LEVEL(110) goodie closed | Same seam. First-play S through `TryApply` leaves world 110 unlocked but incomplete so goodie 14 stays `GS_UNKNOWN`. Isolated closed COMPLETE_LEVEL(110) names ApplyUpdate and does not go through `TryApply`. Leftover 14 FrontEndHandoff names 14 as New. Existing FrontEndHandoff S goodies name 0/8/78/121/164, not 14. Do not invent a world-110 FillOut or the rest of the table. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_LeavesTheWorld110CompleteGoodieUnknown` | 1 | store `GS_NEW` on goodie 14 after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::Update` Level 100 FrontEndHandoff leftover mNumAttempts skip | Same seam. Leftover 7 on world 100 and 11 on world 110 survive first-play S through `TryApply` because `Career.cpp:379-418` never writes `+0x38`. Isolated leftover 7 / 11 names ApplyUpdate and does not go through `TryApply`. Isolated Blank ctor 0 does not go through `TryApply`. Existing FrontEndHandoff ranking / Complete / CareerInProgress / goodie tests do not name `+0x38`. Incrementing inside ApplyUpdate is not unique versus the isolated pin. Skipping `ApplyUpdate` on the handoff is not unique versus existing FrontEndHandoff tests. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_DoesNotIncrementLeftoverNumAttempts` | 1 | increment the finished node after `TryApply` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff leftover SET_GOODIE_NEW GS_OLD overwrite | Same seam. Seeding the five first-play S slots as `GS_OLD` leaves them at 3 through `TryApply`: `SET_GOODIE_NEW` stores only when `GOODIE_NOT_DONE` (`Career.cpp:564-566`). Isolated leftover `GS_OLD` names ApplyUpdate and does not go through `TryApply`. Existing FrontEndHandoff S goodies start `GS_UNKNOWN` and name them as New. Replay CountGoodies names already-`GS_NEW`. `new_goodie_count` / `first_goodie` stay ctor 0. Storing `GS_NEW` even when `mState > GS_INSTRUCTIONS` is not unique versus the isolated pin. Skipping `ApplyUpdate` on the handoff is not unique versus existing FrontEndHandoff tests. `mPendingExtraGoodies` and episode instruction marks stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_DoesNotOverwriteAlreadyOldTrainingGoodies` | 1 | store `GS_NEW` on the five slots after `TryApply` when they were `GS_OLD` |
| `CGame::FillOutEndLevelData` then `CCareer::UpdateGoodieStates` Level 100 FrontEndHandoff leftover SET_GOODIE_NEW GS_INSTRUCTIONS store | Same seam. Seeding the five first-play S slots as `GS_INSTRUCTIONS` writes 2 through `TryApply`: `SET_GOODIE_NEW` stores when `mState <= GS_INSTRUCTIONS` (`Career.cpp:564-566`). Isolated leftover `GS_INSTRUCTIONS` names ApplyUpdate and does not go through `TryApply`. Isolated leftover `GS_OLD` and FrontEndHandoff leftover `GS_OLD` name the skip for state 3. FrontEndHandoff S starts `GS_UNKNOWN`. Replay names already-`GS_NEW`. Skipping `SET_GOODIE_NEW` when `mState == GS_INSTRUCTIONS` is not unique versus the isolated pin. Skipping `ApplyUpdate` on the handoff is not unique versus existing FrontEndHandoff tests. Do not invent `SET_GOODIE_INSTRUCTION` or episode instruction marks. `mPendingExtraGoodies` stays unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Level100WonCareerHandoff.cs` | `Level100WonCareerHandoff.TryApply` | `Level100WonCareerHandoffTests.FrontEndHandoffReadyAfterWon_StoresNewOverInstructionTrainingGoodies` | 1 | restore the five slots to `GS_INSTRUCTIONS` after `TryApply` when they were `GS_INSTRUCTIONS` |
| `IScript::SetSlotSave` Level 100 tutorial persist | `0x00533900` calls `CGame::SetSlot` `0x0046d3a0` then `CCareer::SetSlot` `0x004214e0`. First-play `SetSlotSave(SLOT_TUTORIAL_1..4, TRUE)` writes career bits 63..66 during the tutorial, before `DeclareLevelWon` and before FillOut / `ApplyUpdate`. Isolated FrontEndHandoff overwrite names the 32-dword assignment after `TryApply`; empty FillOut slot words still leave 63..66 set here. World 100 stays incomplete. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailSetSlotSave.cs` | `RetailSetSlotSave.PersistCareerSlot` | `Level100WonCareerHandoffTests.SetSlotSave_PersistsTutorialBitsBeforeApplyUpdate` | 1 | skip `PersistCareerSlot` so 63..66 stay 0 on `SuccessCountdown` |
| `IScript::PrimaryObjectiveFailed` Level 100 init MOS | `0x00534440` writes state 2 (Wave580 plate). Level 100 `init()` calls it for objectives 1..4 before the first `PlayCharMessageWait`. Rebuild `Level100PrimaryObjectiveStatus.Failed` is 1, so an identity cast is not `MOS_FAILED`. Isolated FillOut Won names four `MOS_COMPLETE` (1) and does not go through init. `GetNumPrimaryObjectives` counting non-zero is not unique versus mapping Failed to 1. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailGameObjectiveCount.cs` | `RetailGameObjectiveCount.FromLevel100MissionStatus` | `Level100MissionTests.Init_PrimaryObjectiveFailedWritesRetailMosFailedTwo` | 1 | identity-cast the mission enum so Failed stores 1 |
| `IScript::PrimaryObjectiveFailed` Level 100 init text dword | `0x0053445e` `89 78 04` = `mov [eax+4], edi` on specimen `74154bfa…`. Twin Complete at `0x005343fe`. Table base `lea eax, [eax*8+0x8a9adc]`. Level 100 `init()` stores `_100_OBJECTIVE_1..4` = 110325434 / 111145813 / 111966192 / 112786571 (first cited in `msl-scripting.md`; 2..4 from hash-pinned LevelScript). Isolated MOS-failed names state 2, not `+4`. Isolated FillOut Won names four `GetStatus()` words and does not copy the text dword. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailGameObjectiveCount.cs` | `RetailGameObjectiveCount.FromLevel100MissionPrimaryTextIds` | `Level100MissionTests.Init_PrimaryObjectiveFailedWritesRetailObjectiveTextDword` | 1 | leave the ten text words at 0 |
| `IScript::AddScore` Level 100 incrementer | `0x005343c0` body `8b442404 8b08 8b11 ff5230 01058c9b8a00 c20c00` on specimen `74154bfa…`. `0x005343cb` is `01 05 8c 9b 8a 00` = `add [0x008a9b8c], eax` = `CGame+0xf4`. Source `game.h:210` is `mScore+=inScore`. LoadLevel writes 0. Isolated `ScoreDelta` = 50 names the rebuild accumulator; one live `AddScore(50)` is not unique versus replace. Isolated FillOut `ScoreWord` copies a parameterized dword. First-play elapsed and FillOut score stay unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailAddScore.cs` | `RetailAddScore.Add` | `RetailAddScoreTests.Add_AddsTheDeltaOntoCGamePlusF4NotReplace` | 1 | replace so a second +50 stays 50 |
| `IScript::EnableFlightMode` Level 100 flight flag | `0x00535070` body `8b4910f64134087405e8328cedffc20c00` on specimen `74154bfa…`. `0x00535079` is `e8 32 8c ed ff` = `call 0x0040dcb0` (W001 inbound). Callee `0x0040dcb0` is `c7 81 8c 05 00 00 01 00 00 00 c3` = `mov [ecx+0x58c], 1`. Isolated `FlightModeEnabled` = true names the rebuild bool; skip store Expected 1 Actual 0. One live store of 1 is not unique versus increment from 0. Wrapper gate `test [ecx+0x34], 8` and Disable clear / morph stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailEnableFlightMode.cs` | `RetailEnableFlightMode.Enable` | `RetailEnableFlightModeTests.Enable_StoresLiteralOneAtCBattleEnginePlus58CNotIncrement` | 1 | increment so Enable(1) becomes 2 |
| `IScript::HighlightHudPart` / `UnHighlightHudPart` Level 100 HUD words | `0x00535e60` body `8b4424048b088b11ff5230c704851ca58a0002000000c20c00` on specimen `74154bfa…` (25 B SHA-256 `d2a93e3b…e479fc39`). `0x00535e6b` is `c7 04 85 1c a5 8a 00 02 00 00 00` = `mov dword [eax*4+0x008aa51c], 2`. Twin store `0x00535e8b`, in the `0x00535e80` body with SHA-256 `d273f5d1…c3c5a581`, stores immediate 1, not 0. Isolated `Emphasized` true/false names the rebuild bitmask; skip UnHighlight after Highlight leaves 2. Array extent and state-1/2 HUD meaning stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailHighlightHudPart.cs` | `RetailHighlightHudPart.Unhighlight` | `RetailHighlightHudPartTests.HighlightAndUnhighlight_StoreTwoThenOneNotBoolMask` | 1 | Unhighlight writes 0 so Unhighlight(2) becomes 0 |
| `IScript::EnableWeapon` Level 100 walker active flag | `0x00534fb0` body SHA-256 `b71e37ce…92766eb4` on specimen `74154bfa…`. `0x00534fce` is `ff 97 98 01 00 00` = `call [edi+0x198]`. Player override `0x0040dc30` (37 B SHA-256 `fa72dd3f…eb92f839`) forwards to walker `0x00414970`. `0x004149c7` is `c7 87 9c 00 00 00 01 00 00 00` = `mov [edi+0x9c], 1`. Isolated `Enabled` = true names the rebuild bool; skip store Expected 1 Actual 0. One live store of 1 is not unique versus increment from 0. Wrapper gate `test [eax+0x34], 0x10` and Disable store-0 / ChangeWeapon stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailEnableWeapon.cs` | `RetailEnableWeapon.Enable` | `RetailEnableWeaponTests.Enable_StoresLiteralOneAtWeaponPlus9CNotIncrement` | 1 | increment so Enable(1) becomes 2 |
| `IScript::SetObjective` / `UnsetObjective` Level 100 thing flag | `0x00535ed0` body `8b49106a01e896dafbffc20c00` on specimen `74154bfa…` (13 B SHA-256 `e1e368b8…a48f666b`). Twin Unset `0x00535ee0` pushes 0. Callee `0x004f3970` 61 B SHA-256 `da733fdd…353980b0`. `0x004f398e` is `80 4e 2c 20` = `or [esi+0x2c], 0x20`. Unset `0x004f39a5` is `80 66 2c df` = `and [esi+0x2c], 0xdf`. Isolated `IsObjective` / navigation name stay the rebuild bool / string; skip store Expected 0x20 Actual 0. One live store of 0x20 from 0 is not unique versus replace. Isolated Mark(0x04)=0x24. Noticeboard Add/Remove stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailSetObjective.cs` | `RetailSetObjective.Mark` | `RetailSetObjectiveTests.MarkAndUnmark_OrBit20ThenClearItNotBoolReplace` | 1 | replace so Mark(0x04) becomes 0x20 |
| `IScript::Pause` / `PlayCharMessageWait` Level 100 wait stop flag | `0x00537c70` through `ret 0xc` at `0x00537d66` (249 B SHA-256 `2bdf08ea…829f`) and `0x005375f0` through `ret 0xc` at `0x005377d5` (488 B SHA-256 `7d65b4bc…55e3`) on specimen `74154bfa…`. `0x00537d55` / `0x005376f9` are `c7 05 00 c8 89 00 01 00 00 00` = `mov [0x0089c800], 1` (singleton `+0x220`). Isolated `PauseTicks` / `MessagePlaybackTicks` name the rebuild sleep; skip store Expected 1 Actual 0. One live store of 1 is not unique versus increment from 0. First-play init issues six `PlayCharMessageWait`s; increment would be 6. CVM snapshot / 0.05f / FollowWaypointWait / PlayAnimationWait / Run-yield stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailIScriptWaitStop.cs` | `RetailIScriptWaitStop.Stop` | `RetailIScriptWaitStopTests.Stop_StoresLiteralOneAtCvmSingletonPlus220NotIncrement` | 1 | increment so Stop(1) becomes 2 |
| `IScript::InJetMode` Level 100 type+recency | `0x005380f0` through `ret 0xc` at `0x00538147` (90 B SHA-256 `0ae6c37d…866486f0`) on specimen `74154bfa…`. `0x005380f6` is `f6 41 34 08` = `test [ecx+0x34], 8`. Callee `0x00408120` (43 B SHA-256 `81ab0371…7fb7610e`) is walker `+0x260==2` and `now - +0xcc < 0.5f` at `0x005d85ec`. Wrapper `test eax,eax / jne` keeps FALSE when the callee is true. Isolated `JetModeState` names recency and still returns InJetMode for a non-BE jet. Isolated `PlayerInJetMode == (mode == Jet)` names the rebuild bool; an airborne walker is TRUE here and FALSE there. TargetZone2/3/4 `hit()` SET_CONTEXT the hitter then call this native. EnableFlightMode wrapper gate / Disable / lock-set / Move / Morph / UpdateCamera stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailIScriptInJetMode.cs` | `RetailIScriptInJetMode.Evaluate` | `RetailIScriptInJetModeTests.Evaluate_IsFalseUnlessBattleEngineBit8AndNotARecentlyGroundedWalker` | 1 | skip `test [ecx+0x34], 8` so a non-BE jet is TRUE |
| `IScript::FollowWaypoint` Level 100 start-follow flag | `0x00537d70` through `ret 0xc` at `0x00537e34` (199 B SHA-256 `6f03ae4a…ed9fd3`) on specimen `74154bfa…`. Twin official backup matches. `0x00537df7` is `b8 01 00 00 00` = `mov eax, 1`. `0x00537e20` is `89 46 18` = `mov [esi+0x18], eax`. Isolated FollowWaypoint emit-command names the rebuild path / flag 0; skip store Expected 1 Actual 0. One live store of 1 is not unique versus increment from 0. Level 100's one compiled native-0 site is AirborneDrone1 `ready()` `FollowWaypoint("Drone Path 1", 0)` on beat 7. FollowWaypointWait early-out / CVM / `+0x1c` / `+0x14` / `+0x24` / AddEvent 2000 stay unclaimed. ChargeWeapon stays unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/RetailIScriptFollowWaypoint.cs` | `RetailIScriptFollowWaypoint.Start` | `RetailIScriptFollowWaypointTests.Start_StoresLiteralOneAtIScriptPlus18NotIncrement` | 1 | increment so Start(1) becomes 2 |
| `TargetZone2/3/4 hit()` InJetMode then Pause wait-stop | Compiled `hit()` SET_CONTEXT the hitter, `IsA(8)`, `InJetMode() == FALSE`, `Pause(0.5)`, then `PostEvent("Reached Target Zone N")`. Isolated `Evaluate` names type-8 without posting Reached. Isolated `JetModeState` names the old `TriggerEntered` pre-filter. Isolated `Stop` names LevelScript `PlayCharMessageWait`. Simulation now consults `Evaluate` on this tick's flight state; `TriggerEntered` is not success. Actor-script `SetObjective` ORs bit 0x20 on the same registry word `hit()` Unset clears. CVM snapshot / 0.05f / FollowWaypointWait / IsObjective HUD-reader of bit 0x20 stay unclaimed. ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Simulation.cs` | `RetailIScriptInJetMode.Evaluate` | `Level100TargetZoneHitTests.TargetZone2_FallInAndLand_PostsReachedFromHitNotFromTriggerEntered` | 1 | keep `TriggerEntered` as success, or skip the actor Pause store |
| `IScript::EnableFlightMode` Level 100 takeoff store | Same callee `mov [ecx+0x58c], 1`. Beat 6 Enable and `GrantFlightLegForMeasurement` now store that immediate on Simulation. Isolated `Level100FlightEnabled` / `FlightModeEnabled` name the rebuild bool and still pass if this store is skipped. Isolated `RetailEnableFlightMode.Enable` names literal-1 without takeoff. Mutation: skip the store so ToggleMode is rejected, or increment so a second Enable becomes 2. Wrapper gate `test [ecx+0x34], 8` and Disable clear / morph stay unclaimed. ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed. Live `GAME.mSlots` stay unclaimed. No new secondaries | `rebuild/OnslaughtRebuild.Core/Simulation.cs` | `RetailEnableFlightMode.Enable` | `Level100EnableFlightModeTests.GrantFlightLeg_StoresCBattleEnginePlus58CSoToggleModeCanTakeOff` | 1 | skip the `+0x58c` store so GrantFlight still sets the bool but ToggleMode is rejected |
| Career level-select offer law (`level_structure` + measured `ReCalcLinks` unlock) | `references/Onslaught/Career.cpp:24-70` is the shipped 43-node table (`num_nodes = 43`, `Career.cpp:73`); the offer law is the already-pinned ReCalcLinks unlock (row `CCareer::ReCalcLinks` after Level 100 Won): a node is offered once an incoming link has left `CN_NOT_COMPLETE`, while the node's own `mComplete` stays 0; the cold-career default lands on the root (`Career.cpp:1065/1118`) | `rebuild/OnslaughtRebuild.Core/RetailWorldCatalog.cs` | `RetailWorldCatalog.IsWorldSelectable` | `RetailWorldCatalogTests.IsWorldSelectable_WonRootUnlocksWorld110ButNotDistantNodes` | 1 | also require the child node's own `mComplete != 0` — locks world 110 forever because the unlock leaves it 0 |
| `CCareer::IsWorldLater` | `Career.cpp:324-374`: walk the `diesOn` node's subtree through lower then higher child links and answer whether the `current` node is inside it; the caller's own `currentNode != diesOnNode` guard makes equal worlds FALSE, and disjoint subtrees are FALSE because the walk never reaches them | `rebuild/OnslaughtRebuild.Core/RetailWorldCatalog.cs` | `RetailWorldCatalog.IsWorldLater` | `RetailWorldCatalogTests.IsWorldLater_MatchesSubtreeSemantics` | 1 | swap the arguments so the walk starts at `currentNode` (ancestor/descendant inverted) |
| World-110 script-object admission (second career node, version-50 layout) | `data/resources/110_res_PC.aya` measured 2026-08-22, whole-archive SHA-256 `4e041c758b9d41ba18311b1fadeacb95fc31af51320861480b97033bc24e3c2b`; RLWD header `(3, 41, 110)`; 13 hash-pinned objects — LevelScript 5110 B `f5c157ba…22aa` with 181 instructions, 92 symbols, `builtin[0]=11`, five named events (`Enemy Engaged` 100, `Vital Building Destroyed` 134, `Lander Escaped` 145, `Lander Destroyed` 159, `Lander Withdraws` 170) | `rebuild/OnslaughtRebuild.Core/Level100MissionProgram.cs` | `Level100MissionProgram.LoadEmbedded(world, name)` | `RetailWorld110AdmissionTests.AllThirteenScriptObjects_AdmitWithTheirPinnedIdentities` | 1 | re-pin `beacon` to `Lander`'s payload hash (cross-object admission accepted) |
| World-110 HFLD admission | Same archive; extracted envelope (tag + CHFD + HFDT) is 668660 B, SHA-256 `fd4d076a2926fbc473b7d364703bdbc0c8a0f7a638b0ab71b6f319374da033c2`; same CHFD format law as Level 100 (grid `0x89/0x94/0x1a8/0x168`, same scales) with height data differing from the first sample word — a distinct measured world under the shared envelope law, recorded via `PayloadSha256` | `rebuild/OnslaughtRebuild.Core/Level100Terrain.cs` | `Level100Terrain.World110` (per-world `LoadEmbedded(name, sha)`) | `RetailWorld110AdmissionTests.World110Heightfield_IsItsOwnHashPinnedEnvelope` | 1 | re-pin `World110SourceSha256` to Level 100's envelope hash (also failed `World110AndLevel100Terrains_AreDistinctMeasuredWorlds`) |
| `CPlayer::AssignBattleEngine` ordered two-reader assignment | Pristine `[0x004d3080,0x004d30c5)` is 69 B / `17f1f2e2…593a10`. It calls `SetReader(player+0x1c, engine)` then `SetReader(engine+0x574, player)`, tests the full player `+0x20` dword, and on any nonzero value dispatches vtable `+0xe0` with raw 0 then `+0x154` with raw 1. Pinned `Player.cpp:254-266` names the two reader binds and God-only policies. Same-target calls retain their outer boundaries; reassignment does not clear either stale reciprocal side. Preflight rejects missing or duplicate required reader-cell roles before mutation; it is not retail rollback, and the policy entries are call intents rather than virtual execution | `rebuild/OnslaughtRebuild.Core/RetailPlayerBattleEngineAssignment.cs` | `RetailPlayerBattleEngineAssignment.Assign` over `RetailActiveReaderGraph` | `RetailPlayerBattleEngineAssignmentTests.FreshNonGodBindPreservesCallOrderAndTargets` | 1 | omit the reciprocal engine-player `SetReader` while retaining its transcript label: the exact fact fails on the empty nested actions; restore byte-for-byte and it passes 1/1, adjacent gate 54/54 |
| `IScript::SecondaryObjectiveFailed` native 88 | Stuart `game.h:22-24,179-187` owns ten zero-based secondary slots and `SetSecondaryObjectiveFailed(int num, int string_id)`. Pristine `0x00534470` unboxes two integers, writes text at `[0x008A9B2C + num*8 + 4]`, then stores `MOS_FAILED=2` at `[0x008A9B2C + num*8]`; the exact world-110 LevelScript has one native-88 instruction at index 22, attribute `0x00000258`, fed by slot `1` and `_110_SECONDARY_1=114309509` | `rebuild/OnslaughtRebuild.Core/Level100MissionTypes.cs` and `rebuild/OnslaughtRebuild.Core/Level100Mission.cs` | `RetailSecondaryObjectiveState.SetFailed`; `Level100Mission.InvokeNative` case 88 | `Level100MissionTests.SecondaryObjectiveFailed_WritesTheIndexedRetailSlotAndRejectsSwappedOrOutOfRangeArguments` | 1 | write `Complete=1` instead of `Failed=2` (observed Expected Failed / Actual Complete); swapped text/index and both range edges are also rejected without mutation |
| `IScript::SecondaryObjectiveComplete` native 84 | Same source owns `SetSecondaryObjectiveComplete(int num, int string_id)`. Pristine `0x00534410..0x0053443b` is 44 bytes, SHA-256 `b39a3c58214a8efc7eff0ca11c1407764983888c3a7d249643376162740cd197`; `0x00534432` stores `MOS_COMPLETE=1` at `[0x008A9B2C + num*8]` after writing the text dword at `+4`. The admitted `f5c157ba…22aa` world-110 LevelScript has one exact native-84 instruction at index 66, attribute `0x00000254`, fed by slot `1` and text id `114309509`; the ordinary opening is still suspended at its earlier instruction-34 Pause | `rebuild/OnslaughtRebuild.Core/Level100MissionTypes.cs` and `rebuild/OnslaughtRebuild.Core/Level100Mission.cs` | `RetailSecondaryObjectiveState.SetComplete`; `Level100Mission.InvokeNative` case 84; exact-instruction `RunWorld110SecondaryObjectiveCompleteInstrument` | `Level100MissionTests.SecondaryObjectiveComplete_WritesOnlyTheIndexedRetailSlotAndRejectsSwappedOrOutOfRangeArguments`; `RetailWorld110AdmissionTests.AuthoredWorld110Native84Branch_CompletesTheFailedSlotAndChangesTheCanonicalHash` | 2 | route native 84 to `SetFailed` (Expected Complete / Actual Failed); the adverse failed neighbor, swapped arguments, range edges, authored argument/void shape, and complete-vs-failed schema-43 hashes also falsify drift |
| World-110 direct mission initialization | Exact `f5c157ba…22aa` program: native 88 at instruction 22, first `Pause` at instruction 34. The intervening non-waiting `_110_PROTECT` message id 8444036 maps to exact retail `110_protect.ogg`, SHA-256 `03f1fc8e…35d3`, 172,496 samples at 44.1 kHz; the retained duration law gives 90 ticks. Definition/world mismatches fail in both directions. The stamped Level 100 fixture is a direct mission instrument, not a World110 Simulation or authored world-110 content | `rebuild/OnslaughtRebuild.Core/Level100Mission.cs` and `rebuild/OnslaughtRebuild.Core/Level100MissionTiming.cs` | direct world-110 mission constructor; `Level100MissionTiming.MessagePlaybackTicks` | `RetailWorld110AdmissionTests.MissionConstructor_World110CrossesNative88AndStopsAtItsFirstLegitimateWait` | 1 | change the measured `_110_PROTECT` duration from 90 to 91 ticks (observed Expected 90 / Actual 91) |
| `CBattleEngine::Init` refresh events 6002/6003 | `0x00404dd0` draws at `0x0040586e` and files 6002 at `0x004058b2` (`fmul [0x005d8808]` = `0x364ccccd`, `fadd mTime`, `fadd [0x005d85c0]` = 0.1f), then `HandleAutoAim(NULL)` `0x0040b6d0` draws at `0x0040bf57` and files 6003 (`0x35cccccd`, 0.2f); both priority 0. Order after other constructors awaits the RE first-flush answer | `rebuild/OnslaughtRebuild.Core/RetailBattleEngineRefresh.cs`, `SimulationBattleEngine.cs` | `RetailBattleEngineRefresh.CrosshairDueTime`/`AutoAimDueTime`, `Simulation.InitializeBattleEngineRefreshEvents` | `Level100BattleEngineRefreshTests.DueTimes_AreSampleTimesScalePlusNowPlusDelayInSinglePrecision`, `.Construction_FilesTheCrosshairThenTheAutoAimRefreshWithOneDrawEach` | 7, 1 | 6002 scale set to 6003's `0x35cccccd`; 6003 delay 0.2f set to 0.1f; one extra construction draw — each RED, restored byte-identical, GREEN |
| `CBattleEngine::GetLaunchPosition` reuses the event report | `0x0040c990` (`BattleEngine.cpp:3049-3063`): `hitPoint = cam_pos + unit(view) × mWlcr.mDistToImpact`, where `mWlcr` is written only by the 6002 path of `CalcUnitOverCrossHair` `0x0040acc0` | `rebuild/OnslaughtRebuild.Core/Simulation.cs`, `SimulationBattleEngine.cs` | `Simulation.ReticleAdjustedLaunchAngles`, `Simulation.CalcUnitOverCrossHair` | `Level100BattleEngineRefreshTests.LaunchCorrection_ReusesTheRetainedDistanceAlongTheCurrentViewLine` | 1 | launch correction re-traces the current line instead of reading the report — RED, restored, GREEN |
| Aquila weapon stores and `WeaponFired` | Config `0x35f`: (0, 2000) (0, 100) (1, 150) (0, 200) (1, 100) (1, 100). Jet ammo `0x0041215d-0x00412192` −consumption clamped at 0; jet heat `0x00412135` +consumption − `kWeaponCoolRate` (int 1 at `0x006236a4`); walker heat +consumption; `Charged()` is `+0x68 > 0`; once per burst event, before the volley | `rebuild/OnslaughtRebuild.Core/Level100PlayerWeaponRuntime.cs` | `Level100PlayerWeaponRuntime.WeaponFired`, `.StoreOf` | `Level100PlayerStoresTests` | 12 | Mech Vulcan consumption 1→2; a charged heat shot pays heat — each RED, restored, GREEN |
| `CBattleEngine::Move` heat-store cooling | `0x004095d6-0x00409633`: each heat store −1.0 (`fild [0x00622f08]`), below 0 → 0, else overheat cleared when strictly below `0.75 × capacity` (`0x005d8bc4`) | `rebuild/OnslaughtRebuild.Core/Level100PlayerWeaponRuntime.cs` | `Level100PlayerWeaponRuntime.CoolStores` | `Level100PlayerStoresTests.Cooling_TakesOneEveryMoveAndClearsOverheatStrictlyBelowThreeQuarters` | 1 | cool rate 1→2; clear threshold 0.75→0.8 — each RED, restored, GREEN |
| `CBattleEngine::AddShockShake` and the round's Init draw | `0x00407940`: return below 0.001 (double `0x005d8bc8`), cap 0.75, three draws `(r mod 32)/(16/a) − a`; called per spawned round with `CWeaponPower` (`0x0040c340`) and from `Damage` (`0x0040ab75-0x0040abc2`). `CRound::Init` `0x004d8410` → `CActor::Init` `0x004d867b` takes one draw per round | `rebuild/OnslaughtRebuild.Core/RetailBattleEngineShake.cs`, `Simulation.cs`, `Level100ActorWeaponRuntime.cs` | `RetailBattleEngineShake.Add`/`.Decay`/`.DamageAmount`, `Simulation.SpawnPlayerBurst` | `Level100PlayerStoresTests.Shake_TakesThreeDrawsAboveTheThresholdAndDecaysEachMove`, `SimulationTests.PlayerProjectilesConsumeReleasedScatterInRetailDrawOrder` | 1, 1 | threshold 0.001→0.1; the round Init draw removed — each RED, restored, GREEN |
| Cockpit Gun emitters for player rounds | `GetLaunchPosition` (`0x0040c990`) starts from the Gun emitter's world pose, M·p + P, M the body orientation `+0x3c`; `cockpit2.msh` Gun table composed from HPOS/HORI (fly frame 0, walk 25); launch sequences Pulse (1, 1), Twin Vulcan (1..4 → 9..12), Mech Vulcan (1, 13) (2, 14), pod (1, 4) (2, 3) (3, 5) (4, 2) (5, 6) | `rebuild/OnslaughtRebuild.Core/Level100CockpitEmitters.cs`, `Simulation.cs` | `Level100CockpitEmitters.Gun`, `Simulation.LaunchGun`, `.CockpitEmitterWorldPosition` | `Level100CockpitEmitterTests`, `SimulationTests.MechVulcanRounds_LeaveGunsThirteenAndFourteenInSequence`, `SimulationTests.PulseCannonEmitter_ConvergesOnTheCameraReticleContact` | 9, 1, 1 | Gun 1 below the origin; model z taken as up; Twin Vulcan and Mech Vulcan sequences reordered — each RED, restored, GREEN |
| Level 100 load order and construction draws | `CWorld::LoadWorld`: base world's 1,481 pines one draw each, then `CInfluenceMapManager::Load` (`0x0048b010`) → `0x0048b8e0` one draw (`0x0048bf0f`) and 1000; base rows 0-34, then level rows; after them `SpawnInitialThings` (`0x0050dcb0`) warms up each script-spawned type no row built (Target Truck: Actor and hover draws; Target Drone: Actor and `0x004d1bae` draws) and `0x0048b8e0(0)` takes a second draw and 1000; row 0's `CStart::SpawnBattleEngine` (`0x004eaf20`) builds the Battle Engine inline; per class: `CActor::Init` draw (`0x0040135d`), cannon fire control (`0x004f90ce`), ground hover (`0x0047c869`), squad 4000/4001/4002 draws (`0x004e8177`, `0x004e8486`, `0x004e709c`), `CPlane::Init` last draw (`0x004d1bae`) | `rebuild/OnslaughtRebuild.Core/Level100UnitCallbacks.cs` | `Level100ActorMechanics.ConstructLevel`, `.ConstructUnit` | `Level100BattleEngineRefreshTests.Construction_FilesTheCrosshairThenTheAutoAimRefreshWithOneDrawEach`, `Level100UnitCallbackTests` | 1, 8 | no pine draws; no construction fire-control draw; Battle Engine refresh before its Actor draw; no influence draw after the pines or at the tail; no warm-ups; warm-ups of types the rows built — each RED, restored, GREEN |
| Unit 4003, UnitAI 3000/3001/3003, fire control 4001, squads 4000-4002 | 4003 `0x004f98a8-0x004f9972` (camera within 50 units, one draw, now + 3 + r/65536); `CUnitAI::HandleEvent` `0x004ff330` poll (one draw, 3003 at now + 2 + r·2⁻¹⁵) and `0x004fec60` → `Update` idle arm (1.5 + r·2⁻¹⁶ near, 3 + r·2⁻¹⁵ far); Warehouse 3001 `0x004feac0`; fire control `0x004fb280` (dying returns); squad `0x4e8100`, `0x4e83b0`, `0x4e7070`; influence 1000 `0x0048c120` → `0x0048b8e0(1)` (one draw, next 1000 at now + 1.0 + (r mod 65536) × 2⁻¹⁶) | `rebuild/OnslaughtRebuild.Core/Level100UnitCallbacks.cs`, `Level100ActorPlaneRuntime.cs` | `Level100ActorMechanics.DispatchUnitCallback`, `.FileInfluenceMapRefresh` | `Level100UnitCallbackTests` | 8 | 4003 delay 3→2; near/far swapped; poll 2→3; Warehouse without target; squad 4002 0.99→1.0; camera test inclusive; dying turret keeps refreshing; influence scale 2⁻¹⁶→2⁻¹⁵; the influence chain stops after one delivery — each RED, restored, GREEN |
| Actor multiplier-4 Move phase | `CActor::Init` q = r mod 4: q − 1 &gt; 0 files LF_MOVE with that counter, else MOVE with the counter reset; first full Move one, two or three frames after construction | `rebuild/OnslaughtRebuild.Core/Level100UnitCallbacks.cs`, `Level100ActorMechanics.cs` | `Level100ActorMechanics.SeedGroundMovePhase`, `.ConstructedThisFrame` | `Level100UnitCallbackTests.TargetTank_TakesItsSquadDrawsAndSeedsItsMovePhase` | 1 | phase not seeded from the Actor draw — RED, restored, GREEN |
| Launch basis and the default launch angle | Burst spawner basis = orientation × angle × jitter, each `FMatrix(yaw, pitch, 0)` (`0x00506ed1-0x005072d0`); with no `CWeaponLaunchAngle` slot the angle is `0x004f8140(0, 1, 0)` = pitch float(2π/4096) (Euler-constructor control, 36 cases); `Mech Pulse Cannon Charged` @`0x134e3` first node `CWeaponInaccuracy` = `00000000` | `rebuild/OnslaughtRebuild.Core/Level100ActorWeaponRuntime.cs`, `Simulation.cs` | `Level100ActorMechanics.ComposeLaunchDirection`, `.DefaultLaunchAngle`, `Simulation.LaunchWalkerRound`, `Level100ActorMechanics.LaunchActorRound` | `SimulationTests.PlayerProjectilesConsumeReleasedScatterInRetailDrawOrder`, `Level100ActorWeaponTests.AttackingTargetDrone_FiresItsVulcanAndReachesThePlayer` | 1, 1 | no default pitch; the Small bolt's 0.5° scatter restored; the drones' aim pitch and stored pitch each fed with the wrong sign — each RED, restored, GREEN |
| Missile Pod charge and Fire | `Weapon "Missile Pod"` @`0x17746`: `CWeaponChargeRate` `0x41000000` (8.0), levels 0 and 1, store 3, consumption 1. `CWeapon::Fire` `0x00506010` writes the rounded level and mode (`0x00506952`) before its reload check and stamps `now + 0x3f4ccccd` from the burst's start | `rebuild/OnslaughtRebuild.Core/Level100PlayerWeaponRuntime.cs`, `Level100MissilePod.cs` | `Level100PlayerWeaponRuntime.AdvanceCharge`, `.TryPrepareFire`, `.StampReadyAt` | `Level100MissilePodTests.Charge_ReachesTheSalvoOnItsThirteenthCallAndStopsThere`, `.Fire_SetsTheModeBeforeItsReloadCheck` | 2 | charge rate 8→10; the level taken only after the reload check — each RED, restored, GREEN |
| Missile Pod burst event and launch slots | `CWeapon__HandleFireBurstEvent` `0x00506930` re-files 5001 at now + `CWeaponBurstDelay` (`0x3dcccccd` launcher, `0x3d4ccccd` salvo) while the counter is below the burst size; the counters `+0x70`/`+0x74` start at −1 (`0x00505e7b`) and advance before each round; the basis is orientation × `FMatrix(a, b, 0)` × jitter (`0x00506ed1-0x005072d0`), with the five `CWeaponLaunchAngle` pairs from the mode records | `rebuild/OnslaughtRebuild.Core/SimulationBattleEngine.cs`, `Simulation.cs` | `Simulation.FireMissilePod`, `.HandleMissilePodBurst`, `.SpawnPodBurst`, `.ComposeLaunchDirection` | `SimulationTests.MissilePodLauncher_SpawnsFiveMissilesOnTheBurstEventsAlongTheirSlots`, `Level100MissilePodTests.LaunchCounters_StartAtTheFirstSlotAndWrapAfterFive` | 2 | burst delay 0.1→0.2; sequence counter starts at 0; angles added to the aim instead of composed — each RED, restored, GREEN |
| Battle Engine locks for the pod | `HandleLocks` `BattleEngine.cpp:586-800` (lock loss `0x00406724`, direct acquisition `0x00406ce4`), `StartLock` `0x00406fc0`, `FireLock` `0x00407060` from the spawner at `0x005074c9`, `LockHit` `0x00407140`; parameters from the current charge level (`0x00506350`-`0x00506800`); `CWeaponLockUnit` `0x000e8400` | `rebuild/OnslaughtRebuild.Core/SimulationBattleEngine.cs`, `Level100PlayerLocks.cs` | `Simulation.HandleLocks`, `Level100PlayerLocks.StartLock`/`.FireLock`/`.LockHit` | `SimulationTests.MissilePod_LocksAStaticTargetAndItsMissilesSeekItThenReleaseTheLock`, `Level100MissilePodTests.LockUnitMask_TakesPlanesVehiclesAndCannonsButNotBuildings` | 2 | lock time ignored; `FireLock` skipped; `LockHit` skipped at shutdown; lock unit narrowed to planes — each RED, restored, GREEN |
| Seeking Micro Missile flight and end of life | `CRound::Move` `0x004d8e40`: two wiggle draws (`0x004d8ffc`, `0x004d9036`), release on a dying target, steering after the seek delay inside the seek cone (`0x004d93cc-0x004d9838`); `CRound::Init` files 4000 at now + life (`0x004d86a6`), whose delivery bursts a `CRoundExplode` round in the air (`0x004d9a54` → `0x004d9f30`); `Micro Missile` @`0x8e74`, `Micro Missile Hit` @`0x3a59` | `rebuild/OnslaughtRebuild.Core/Simulation.cs`, `SimulationBattleEngine.cs`, `Level100Destruction.cs` | `Simulation.MovePlayerRound`, `.HandlePlayerRoundEvent`, `.SteerSeekingPlayerRound`, `Level100DestructionRuntime.ReportAirBurst` | `SimulationTests.MissilePod_LocksAStaticTargetAndItsMissilesSeekItThenReleaseTheLock`, `.MissilePodLauncher_SpawnsFiveMissilesOnTheBurstEventsAlongTheirSlots` | 2 | nose-down pitch fed to the nose-up steering law; no air burst at the end of life — each RED, restored, GREEN |
| Scripts start on INIT_SCRIPT | `SetScript` binds the VM and files 2001 at −1 for the thing (`0x004f42b1-0x004f42da`); a constructed thing binds first in its sequence; a `CLevelScriptThing` (vtable `0x005dcfe8`, Init `0x004900d0`) files only that event, at its row; a scripted unit's AI constructor files 2003 (`ready()`, `0x005335a0`) before its AI; Setup's bindings run in frame 2 | `rebuild/OnslaughtRebuild.Core/Level100ScriptEvents.cs`, `Level100UnitCallbacks.cs`, `Level100ActorScriptRuntime.cs`, `Level100Mission.cs`, `SimulationBattleEngine.cs` | `Level100ActorMechanics.FileScriptInit`, `.FileCarrierScriptInits`, `Level100ActorScriptRuntime.AttachReleasedScripts`, `.RunScriptInit`, `Simulation.HandleScriptEvent` | `SimulationTests.Construction_StartsEveryScriptOnItsInitScriptEvent`, `Level100UnitCallbackTests.Warehouse_StartsWithItsTargetAndRefreshesThreeThousandOne`, `Level100FullChainTests.ChainAutopilot_ReachesWonByInputAlone` | 1, 1, 1 | no INIT_SCRIPT for a scripted row; carriers after every row; `SetScript` running at once; no `ready()` event; a spawned script starting at once — each RED, restored, GREEN |
| Level pre-run before the pan | `CGame::InitRestartLoop` files FINISHED_PRE_RUN at now + 3.0 before the load (`game.cpp:371-373`, `0x0046c5f0`); `CGame::PreRun` loops whole updates, unrendered, while the state is 1 (`game.cpp:2063-2071`, `0x0046e040-0x0046e05d`); frame 60's flush starts the pan, and FINISHED_PANNING lands on frame 60 + the world's pan (180 for Level 100, 100 for World 110) | `rebuild/OnslaughtRebuild.Core/Simulation.cs`, `SimulationConstants.cs`, `Level100MissionTiming.cs` | `Simulation.RunPreRun`, `SimulationConstants.OpeningPanTicks`, `Level100MissionTiming.MessageBoxAllowedTickFor` | `SimulationTests.Construction_RunsTheThreeSecondPreRunBeforeThePan`, `Level100MissionTests.ReleasedMessageBox_ReproducesTheRetailOpeningDeliverySchedule`, `Level100SkipPanningTests` | 1, 1, 9 | no pre-run; a pre-run one frame short; no pan after it; its mission events dropped; a message gate without it — each RED, restored, GREEN |
| Round MOVE and life events, insertion order and the dying step | `CRound::Init`: `CActor::Init` files MOVE at −1 (multiplier 1.0), then 4000 at now + life (`0x004d86a6`); each MOVE re-files itself on delivery (`0x00401b41`), so rounds move in insertion order; the 4000 comes before its frame's MOVE, which still steps (`0x00401ae6-0x00401af4`) and is not re-filed: k + 1 Moves; a dying round's slot 39 (`0x004d8ae0`) returns at `0x004d8af7` before damage, impact and death | `rebuild/OnslaughtRebuild.Core/Level100ActorWeaponRuntime.cs`, `Simulation.cs`, `Level100Destruction.cs`, `RetailEventScheduler.cs` | `Level100ActorMechanics.FileRoundEvents`, `.DispatchActorRoundEvent`, `Simulation.HandlePlayerRoundEvent`, `Level100DestructionRuntime.TryReportDyingRoundContact` | `SimulationTests.PitchedPulseRound_FollowsViewPitchWithoutInventingVerticalTargetHits`, `.ControllerRounds_MoveAheadOfRoundsAlreadyInFlight`, `.DyingRound_LastStepCrossesATargetWithoutDamagingIt`, `.MissilePodLauncher_SpawnsFiveMissilesOnTheBurstEventsAlongTheirSlots`, `Level100ActorWeaponTests.ActorRounds_MoveOnTheirOwnEventsUntilTheirLifeEnds`, `.ActorRound_LastStepMeetsThePlayerWithoutDamage`, `Level100DestructionContactTests.DyingRoundContact_ReportsTheHitButLeavesTheTargetUnharmed` | 2, 1, 1, 1, 2, 2, 1 | first Move two frames late; a new round queued behind the re-filed MOVEs; player and drone rounds that outlive their life event; the air burst where the last step ends; dying player and drone steps that damage; a dying contact the script never hears — each RED, restored, GREEN |
| Waypoint walks: nearest start, node targets, load-time heights, walk ends | The loader keeps `CWaypoint` rows (bit `0x1000`, `0x00505a29-0x00505a35`) and prepends each (`0x004e5a80`); `0x00505c30` returns the listed node nearest the unit: dx, dy, dz (waypoint minus `+0x1c/+0x20/+0x24`), (dx² + dz²) + dy² at PC24, `fcom` C0 (strict `<`) against a float32 minimum from 9999999.0 (`0x4b18967f`), stored with `fstp`; on arrival the next node is the waypoint's `+0x3c` (`0x005384dc`), and a self-target logs `0x0064fe50` and ends the walk; `CThing::Init` raises a waypoint to the heightfield sample (`0x0047eb80`, `0x004f34fb-0x004f3534`) and then the water level (`0x006fbdfc`, `0x004f3549-0x004f3559`); slot 64 at a walk's end: `CDropship` `ret` (`0x00459990`), `CPlane` guide slot 8 (`0x00422750` → `0x0047e3d0`), ground Unit default (`0x004fcf00`) | `rebuild/OnslaughtRebuild.Core/Level100ActorRegistry.cs`, `Level100ActorMechanics.cs`, `Level100ActorPlaneRuntime.cs`, `rebuild/OnslaughtRebuild.Client/Level100ActorDefinitionManifest.cs` | `Level100WaypointPathDefinition.FromFileOrder`, `.NearestPoint`, `Level100ActorMechanics.SeatWaypoints`, `.BeginWaypoint`, `.ObserveWaypointArrival` | `Level100AirTrainerFlybyTests.AirTrainer_FliesFromItsNearestNodeAlongTheTargets`, `.Waypoints_TakeTheirLoadTimeGroundOrWaterHeight`, `.SelfTargetingWaypoint_EndsTheWalk`, `Level100ActorMechanicsTests.TransporterWalkEnd_KeepsItsVelocity`, `Level100WaypointFixtureTests.ManifestWaypointPaths_AreRetailListsWithTheirTargetChains`, `Level100ActorRegistryTests.NearestPoint_KeepsTheEarlierNodeOnATieAndStartsBelow9999999` | 1, 1, 1, 1, 1, 1 | a start at the list head; no load-time lift; a dropship stopped at the walk's end; a self-target that keeps walking; the file order kept; a tie that keeps the later node — each RED, restored, GREEN |
| World 110 load and construction | The base world loads as Level 100's; the 40 level rows follow in file order (`world-110-construction-order.md`, "Level-world rows"). Squad members take `CGroundVehicle::Init`'s Actor draw (multiplier 4), fire control, 4003, hover (`0x0047c869`) and AI. Then the squad binds its own script (`0x004e61b4`) and takes its draws before 4000 (`0x004e8177`) and 4001 (`0x004e8486`), and before 4002 (`0x004e709c`) when `Process` returns 0 (rows 16 and 18 return 1 under the static target estimate). A landing craft builds its turret child (`CComponent` Init `0x00427b80`) after its own Actor draw; the child's MOVE draws once every frame (`0x00428110` → `0x004284a1`). Fighters end `CPlane::Init` with `0x004d1bae`; the inactive spawner files nothing that draws (`0x004e3322`); there are no warm-up units. Level rows 102 draws, tail 1 | `rebuild/OnslaughtRebuild.Core/Level100UnitCallbacks.cs`, `Level100ActorRegistry.cs`, `rebuild/tools/materialize_retail_assets.py` | `Level100ActorMechanics.ConstructSquad`, `.ConstructComponents`, `.DispatchComponentMove`, `.WarmUpCandidates` | `World110ConstructionTests.Load_TakesTheContractDraws`, `.Load_FilesEveryInitScriptAtItsRow`, `.PreRun_RunsEveryWorld110ScriptInit` | 1, 1, 1 | the member fire-control draw removed; `Process` never 1; the turret child's Actor draw removed; the squad's script before its members; Weather not attached — each RED, restored, GREEN |
| Landing-craft and fighter AI thinks | `CUnitAI::HandleEvent` (`0x004ff330`) on a `CDropship` in AI_ONF runs `CDropshipAI::Update` (`0x004487e0`, draw `0x0044880f`), then polls (draw `0x004ff371`, 3003 at now + 2 + (r mod 65536)·2⁻¹⁵). `CDropshipAI` slot 9 (`0x00448580`) draws once (`0x00448763`) and files 3000 at ((r mod 65536)·2⁻¹⁶ + 1.0) + now. `CPlaneAI` slot 9 (`0x004d21c0`) takes the Update's arm draw and one more (`0x004d2434`), then 3000 at ((r mod 65536)·2⁻¹⁸ + 0.25) + now. `Land` (`0x005361d0` → slot 93) sets landing state 2. A fighter's air velocity and turn rate are unit fields 2 and 6 (`0x00431bb0` → `0x00432908`; apply bodies `0x00432af0` `+0xb4`, `0x00432b00` `+0xb8`) | `rebuild/OnslaughtRebuild.Core/Level100UnitCallbacks.cs`, `Level100ActorMechanics.cs`, `Level100ActorRegistry.cs` | `Level100ActorMechanics.HandleAiEvent`, `.RequeueAi`, `Level100ActorRegistry.PlaneAirVelocityFloatBits` | `World110ConstructionTests.LandingCraft_InAiOnf_RunsItsUpdateThenPolls`, `.LandedCraft_ThinksOnItsOwnOneToTwoSecondCadence`, `.Fighter_ThinksOnTheCPlaneAiCadence`, `.LandedCraft_RestoresItsLandingStateAndIsNotHashed` | 1, 1, 1, 1 | AI_ONF without its Update draw; a landed craft on the `CUnitAI` far cadence; the turret child's MOVE without its draw; `Land` ignored; the craft and the fighters thinking as `CUnitAI`; a fighter's think without the arm draw; the landing state dropped on restore — each RED, restored, GREEN |
| World 110 script natives | `Rand` `0x00538230`: one draw on the shared stream (`0x008a9d9c`), then `idiv` boxes the remainder. `GetNumUnits` `0x00535590` reads per-side counts (`0x008551c0`, `0x00855228`) indexed by profile `+0xe0`, and 0 for any other side. `CUnit::Init` increments them (`0x004f90af`, `0x004f90c4`) and the start of a death (`0x004fd140`) decrements them. `SpawnersEmpty` `0x00535a90` → `0x004fd7e0` walks `+0x18c`: true with no spawners, false for a non-unit. `GetInitialHealth` `0x00535a30` is a unit's slot 78, else 0.0. VitalBuilding's compiled switch ends each case with JMP (opcode `0x14`, executor `0x0052e9b0`) | `rebuild/OnslaughtRebuild.Core/Level100ActorScriptRuntime.cs`, `Level100Mission.cs`, `Level100ActorRegistry.cs` | `Level100ActorScriptRuntime` natives 6, 28, 36, 72, 73, 92; `Level100Mission` natives 11, 40; `Level100ActorRegistry.CountUnits` | `World110ScriptNativeTests.VitalBuilding_TakesOneRandAndPlaysOneWarning`, `.Lander2_WithdrawsOnlyWhenItsCraftHasNoSpawners`, `.GetNumUnits_CountsLivingUnitsBySelectorAndSide` | 1, 1, 1 | `Rand` returning r; `Rand` without its draw; a loaded craft's spawners reported empty; `GetNumUnits` counting the dying, ignoring the side, or reading side 2 — each RED, restored, GREEN |
| World 110 start state | FINISHED_PRE_RUN on frame 60 starts the 2.0 s pan; FINISHED_PANNING lands on frame 100, whose handler (`0x00470024`, an inlined `StartPlayingState`) sets game state 3 and posts "game playing". Scout answers with "Enemy Engaged", and LevelScript's handler waits two seconds, then activates the Airfield and the player (frame 140) | `rebuild/OnslaughtRebuild.Core/Simulation.cs` | `Simulation.PostGamePlaying` | `World110ConstructionTests.StartState_ActivatesThePlayerTwoSecondsAfterGamePlaying` | 1 | "game playing" before the mission tick; every side 0 — each RED, restored, GREEN |
| World 110 base-world carry-over | FillOut's base list (`0x0046d4cb-0x0046d4d1`: a null reader or `TF_DYING` stores 0); `ReCalcLinks` copies it onto node 110; the load asks `CCareer::DoesBaseThingExist` per row (`0x0041bb20` at `0x0050cf8f`; levels 850-899 keep every row, `0x004725d0`; no node keeps every row) and does not build a lost row; a lost building (type bit `0x100`, building setter `0x00417660`) takes ten pairs of draws, Y then X, stamping damage type 6 at its position + ((r mod 65536)·2⁻¹⁶ − 0.5) × 5.0 (`0x0050d09c-0x0050d123`) | `rebuild/OnslaughtRebuild.Core/RetailFillOutEndLevelData.cs`, `RetailCareerNodes.cs`, `Level100ActorRegistry.cs`, `Level100UnitCallbacks.cs`, `rebuild/OnslaughtRebuild.Client/RetailFrontendSession.cs` | `RetailFillOutEndLevelData.BaseThingsLeft`, `RetailCareerNodeTable.LostBaseRows`, `Level100ActorMechanics.StampLostBaseBuilding` | `World110CarryOverTests`, `RetailCampaignFlowTests.WonHandoff_CarriesTheLevel100SurvivorsIntoWorld110sSession` | 5, 1 | no draws for a lost building; X before Y; cannons stamping; lost rows built; FillOut ignoring the dying bit; FillOut dropping the SafeSides; levels 850-899 reading their node; a 2.5 spread — each RED, restored, GREEN |
| Walker dash window | The four walker gestures (`Forward` window `0x00412e1f-0x00412e58`; Backward `0x00413009-0x00413042`, StrafeLeft `0x00413203-0x0041323c`, StrafeRight `0x004133fd-0x00413436`) need now − mDashTime < last < now − 0.5 · mDashTime, both strict, on float32 event times with each difference rounded to float32; `mDashTime` 0.2f (`0x006236ac`), 0.5f (`0x005d85ec`); the hard-press times start at −10.0f (`0x00412c14-0x00412c3e`) | `rebuild/OnslaughtRebuild.Core/Simulation.cs`, `SimulationConstants.cs` | `Simulation.WalkerDashWindowContains`, `.ObserveWalkerForwardGesture`, `.ObserveWalkerStrafeGesture` | `SimulationTests.WalkerDash_AdmitsTheOppositePressOnlyInsideRetailsFloatWindow`, `.WalkerOppositeFlick_TriggersRetailDashAndLocksInputForItsLifecycle` | 9, 1 | the source's window alone; the differences at double precision; either bound inclusive; the hard press stamped a frame late — each RED, restored, GREEN |
| U-17 dropship flight | `CDropship::Move` (`0x00447120`): the air step (`0x00402fa0`) adds the drive, damps by slot 73's 0.99 (`0x0050eb60` → `0x005d8cc4`, or 0.95 for a Big craft whose z + 0.2 × radius is below the water) and caps at `+0xb4` × 0.05 (× 1.5 while leaving); the unit step (`0x004fa8d0`) runs the guide (`0x00448930`: yaw −atan2(Dx, Dy) by mode, nose up −π/5 below MinAltitude, roll the remaining turn capped at π/8 the short way round, drive 0.001 × F), translates with the ground stop (slot 68), steps the Euler angles (slot 77) and, while leaving, runs out 300 along the nose within 4.0 of the goal and then posts its SHUTDOWN (slot 116, 2000 at −1); move orders raise the target to MinAltitude above the integer floor (`0x00403a90`); `Retreat` (`0x004fdd00`) flies to the nearest own-side `CSafeSide`, list newest first (`0x004fd910`, `0x004e5a80`); arrival is the follower's stored 2D distance below 8.0 (`0x00538470`, `0x0050ead0`); SHUTDOWN runs `shutdown()` (built-in 7, `0x00533810`) and removes the unit (`references/Onslaught/thing.cpp:675-684`); pitch and roll rates are a quarter of `+0xb8` (`0x00446dc2-0x00446e0c`) | `rebuild/OnslaughtRebuild.Core/RetailDropshipMotion.cs`, `Level100ActorDropshipRuntime.cs`, `RetailPlaneMotion.cs`, `Level100ActorRegistry.cs` | `RetailDropshipMotion.UpdateCruise`, `.SelectDamping`, `.Arrived`, `.ReachedRunOut`, `Level100ActorMechanics.MoveDropship`, `.ClampAirMoveTarget`, `.RetreatPoint`, `.ShutDownUnit` | `DropshipFlightTests` (10) | 10 | no short-way shift; roll sign; water damping inverted; retreat tie keeping the older; inclusive arrival; unquartered rates; SHUTDOWN posted with the run-out; inclusive MinAltitude; a move order without MinAltitude — each RED, restored |
| Plane move orders and retreat | A plane's waypoint orders use the same slot 61 (`0x00403a90` in the `CPlane` vtable `0x005e1930`) as the dropship, so each target is raised to MinAltitude above the integer floor; arrival is the follower's stored 2D distance below 5.0 (slot 94, `0x0050e8e0`); `Retreat` (slot 100, `0x004fdd00`), the run-out and the SHUTDOWN (slot 116, `0x004fe5f0`) are the unit step's (`0x004fa8d0`, reached from the plane's Move `0x004d1cd0` through the air step `0x00402fa0`); a leaving unit's AI only polls (`0x004ff340-0x004ff34f`), so it neither thinks nor fires | `rebuild/OnslaughtRebuild.Core/Level100ActorPlaneRuntime.cs`, `Level100ActorDropshipRuntime.cs`, `Level100UnitCallbacks.cs`, `Level100ActorWeaponRuntime.cs`, `RetailIScriptFollowWaypoint.cs` | `Level100ActorMechanics.SetPlaneWaypointDestination`, `.RetreatAirUnit`, `.AdvanceLeaveState`, `.HandleAiEvent`, `RetailIScriptFollowWaypoint.Arrived` | `PlaneRetreatTests` (2), `Level100AirTrainerFlybyTests.Waypoints_TakeTheirLoadTimeGroundOrWaterHeight` | 2 | plane orders skipping slot 61; plane Retreat circling; a leaving plane steering at its target; a leaving plane firing; the plane's leave tail skipped; a leaving plane's AI thinking — each RED, restored |

The September 26 Battle Engine rows have their mutation receipts in the
rebuild worktree's ignored
`local-data/test-runs/be-refresh-20260926/mutation-kills/` (the refresh rows:
four mutations) and `local-data/test-runs/stores-shake-20260926/mutation-kills/`
(the store, cooling and shake rows: six mutations) and
`local-data/test-runs/missile-pod-20260926/mutation-kills/` (the four Missile
Pod rows: eleven mutations) and
`local-data/test-runs/launch-basis-20260926/mutation-kills/` (the launch-basis
row: four mutations) and
`local-data/test-runs/construction-order-20260926/mutation-kills/` (the three
load-order and unit-callback rows: eleven mutations) and
`local-data/test-runs/script-inits-20260926/mutation-kills/` (the INIT_SCRIPT
row: five mutations) and
`local-data/test-runs/pre-run-20260926/mutation-kills/` (the pre-run row:
five mutations, plus one equivalent) and
`local-data/test-runs/round-frames-20260926/mutation-kills/` (the round
MOVE and life event row: eight mutations) and
`local-data/test-runs/influence-warmup-20260926/mutation-kills/` (the
influence-map and warm-up additions to the load-order and callback rows: six
mutations) and
`local-data/test-runs/emitters-20260926/mutation-kills/` (the emitter row: four
mutations) and `local-data/test-runs/waypoint-routes-20260926/mutation-kills/`
(the waypoint-walk row: six mutations) and
`local-data/test-runs/world110-construction-20260926/mutation-kills/` (the four
World 110 rows: twenty-three mutations) and
`local-data/test-runs/walker-dash-20260926/mutation-kills/` (the walker-dash
row: five mutations) and
`local-data/test-runs/world110-carry-over-20260926/mutation-kills/` (the
carry-over row: eight mutations), each with `mutate.py` and
`mutation-results.json`; every mutation went RED, was restored byte-identical by
SHA-256, then went GREEN. The shake offsets are not yet applied to the Battle
Engine's orientation (`BattleEngine.cpp:1222-1224`), and the damage shake's
draws come where Core applies the hit, after the actor rounds' Move, not inside
the round's Move as in retail. Open questions they leave, each with its cheapest
falsifier: the Battle Engine Init's place among the other constructors' draws
(the RE lane's first-flush order); the view point, which Core takes as the
Battle Engine's position and facing because it has no camera (a copied-retail
read of `CPlayer::GetCurrentViewPoint` in cockpit view); whether the line
query skips inactive things, as every Core contact query does (a static read
of `0x0050b030`'s candidate walk); and whether `CSimpleBuilding` city
buildings carry the unit and building type bits (their class type mask).
The Missile Pod rows leave six more:
- Its missiles now leave the pod's Gun 2-6 emitters (the emitter row above);
  the cockpit tilt and render-fraction terms of their pose remain open.
- Core moves a player round in its launch frame and removes it after 160 Moves.
  Retail's first Move frame and its 4000 delivery frame are open. Falsifier: a
  static read of `CRound::Init`'s Move event and the frame order.
- Steering keeps only yaw and pitch: it drops the roll a composed launch basis
  carries, and the wiggle bends one step's travel. Falsifier: a static read of
  how `0x004d93cc-0x004d9838` rebuilds the orientation.
- The air burst's 1.0-radius, 0.5-damage explosion damages nothing. Falsifier:
  the explosion's damage walk at `0x004d9f30`.
- `weapon->ReadyToFire` (`0x00509f70`) is taken as the strict now > `+0x64`
  test. Falsifier: a static read of `0x00509f70`.
- A lock needs a side set by the target's script, because Core has no authored
  allegiance for the other level things. Falsifier: the world file's side field
  for those rows.

The load-order and unit-callback rows leave five more:
- Core runs every script's init at construction, so the Tank Factory's first
  `SpawnThing` takes its squad's five draws before frame 1, where retail runs
  that init on frame 2 (Setup binds it into bucket 1). Falsifier: the RE lane's
  first-flush order, applied by scheduling script inits on the event manager.
- Core has no pan camera, so `+0x110` uses the Battle Engine's position, which
  is only exact for the first-person camera. Falsifier: the Client's pan spline
  moved into Core, compared with a copied-retail read of the camera position
  during the pan.
- A drone spawned through a spawner exit files no AI loop after its exit.
  Falsifier: a static read of the exit controller's hand-off to `0x004ff330`.
- The squads' target branch (`+0xc4` set) is not traced, and Core treats any
  waypoint-following member's squad as moving.
- The Air Trainer's `+0x284` draw is taken but its ±0.8 is not used by Core's
  plane motion.

Two things this table deliberately does **not** claim. It does not claim these
contracts are graded `REBUILD_READY`: that grade is a campaign artifact and
needs the ceremony in `tools/re_campaign.py`
(`_validate_rebuild_ready_gate`), which stamps owner/test/project SHA-256s, a
`rebuildMapping`, and a re-run of the focused test. And it does not claim replay
coverage. **Many carried implementations remain unreachable from the
simulation and replay path** — measured by searching `Simulation.cs`,
`ReplayRunner.cs`, `CommandTape.cs`, `StateHasher.cs` and every `Level100*.cs`
for the owner types: `Simulation.JetFrictionNumerator` is wired,
`RetailWeaponCharge.Charge` is reached from `Simulation.TryChargeWeapon`
when the player holds `SimActions.ChargeWeapon` on the Pulse Cannon Pod,
`RetailWeaponCharge.ReadyToCharge` (`0x0050A080`, `test ah,0x41`) blocks that
increment until engine time is strictly greater than the Fire-stamped
`now + CWeaponReloadTime` (0.1 s on `Mech Pulse Cannon Charged`), and Fire at
FullyCharged selects `Mech Pulse Cannon Charged 2` @`0x135b3` /
`Mech Pulse Bolt Large` @`0xacda` of `default physics.dat` (`e1fb3ded…ada14`);
tap-fire at charge 0 stays Medium. The September 8 wiring uses
`RetailEventScheduler.TimeAtFrameCount` for the level's stored clock, including
reset and gameplay pause. Large now projects its authored speed 20, lifetime 7,
radius 0.20 and direct damage 8 through the existing motion/contact owners.
Its zero inaccuracy still consumes both scatter random draws, checked by
`SimulationTests.PlayerProjectilesConsumeReleasedScatterInRetailDrawOrder`.
`SimulationTests.PitchedPulseRound_FollowsViewPitchWithoutInventingVerticalTargetHits`
and the `PulseRadius_*` / `PulseMeshContact_*` checks cover those bounded changes.
Direct Morph/Charge/Fire/ChangeWeapon/Zoom button calls now precede actor and
mission callbacks, in the shipped order. Enable/disable and morph-completion
regressions prevent a later callback from changing an earlier button result;
combined movement/fire uses the retained launch pose. Full axis/event dispatch,
spatial blasts and float motion/expiry remain
open in the [round owner](https://github.com/dlprentice/onslaught-preservation/blob/main/mechanics/cround-hit-damage-path.md).
ReadyToCharge is pinned by
`SimulationTests.AfterPulseFire_ChargeWaitsUntilReloadStrictlyElapses`;
Charged-2 / Large fire is pinned by
`SimulationTests.ChargedPulseRelease_ClearsChargeUsesItsReloadAndNextTapSelectsMedium` and
`Level100PulseCannonChargeTests.FireAtFullyCharged_SelectsCharged2LargeBolt`,
`RetailIScriptInJetMode.Evaluate` and actor-script `RetailIScriptWaitStop.Stop`
are reached from TargetZone2/3/4 `hit()` (Simulation no longer treats
`TriggerEntered` as Reached), `RetailEnableFlightMode.Enable` is reached
from Simulation takeoff after beat-6 Enable /
`GrantFlightLegForMeasurement` (isolated `Level100FlightEnabled` still
names the rebuild bool),
`RetailGameEndCountdown.LostTicks` is reached from
`Level100Mission.DeclareLost` on the released Broke-Tutorial /
`LevelLostString` path. `Level100WonCareerHandoff.TryApply` (which calls
the already-pinned `ForLevel100Won` / `ApplyUpdate`) is the one owner of the
Won career update: the frontend's Won re-entry
(`RetailFrontendSession.TryAcceptWonHandoff`) calls it when the host reports
`FrontEndHandoffReady` after Won, and the tests and the SimInput-only chain
fixture follow their runs' mission events the same way. The mission itself no
longer keeps a career; its `SetSlotSave` reaches the career as a
`Level100TutorialSlotSaved` event that the frontend stores at once. The world-admission
rows, `IsWorldSelectable`, `IsWorldLater` and `Level100Terrain.World110`
remain catalog/admission law. `RetailPlayerBattleEngineAssignment` separately
carries the valid-object two-reader call transcript and God-only policy
intents; no live owner invokes it. World 110's session is built by the same
construction owner as Level 100 (the World 110 rows above), on Level 100's
identical heights; the separate World 110 stage and its admission, start and
height-clamp owners were retired on 2026-09-26. The native-88 secondary state reaches
the first-Pause Core session; native 84 is later in the same exact authored
program but is reached only by the explicitly named instructions-64..67
instrument, not by that opening path. Schema 43 binds both states; the Godot
host plays only world 100. The Lost-skip
row names the same `TryApply` owner; it does not add a twenty-third
implementation. The first-play slot row names
`FirstPlayTutorialSlotWords` on the already-pinned FillOut owner.
The ranking-target row names the same `ApplyUpdate` owner as the
primary-status skip. The only-if-greater row names that same
`ApplyUpdate` owner; it does not add another implementation. The
FrontEndHandoff ranking-target row names the
same `TryApply` owner as the Won handoff. The SuccessCountdown skip
row names that same `TryApply` owner; it does not add another
implementation. The training-goodie row names
`RetailCareerUpdateGoodieStates.Update`; `ApplyUpdate` is the
already-pinned caller. The grade-band row names that same
`Update` owner; it does not add another implementation. The
B-grade row names that same `Update` owner; it does not add
another implementation. The A-grade row names that same
`Update` owner; it does not add another implementation. The
E-grade Won row names that same `Update` owner; it does not
add another implementation. The D-grade row names that same
`Update` owner; it does not add another implementation. The
GRADE(110) C-goodie row names that same `Update` owner; it
does not invent the rest of the table. The GRADE(110) C
concept-art closed row names that same `Update` owner and
`Career.cpp:770`; isolated closed GRADE(110) names 1, not 79.
The leftover
GRADE(110) C-goodie row names that same `Update` owner and
the leftover complete-110 + C store; it does not invent a
world-110 FillOut. The leftover GRADE(110) C concept-art
row names that same `Update` owner and `Career.cpp:770`
`SET_GOODIE_NEW(79)`; isolated leftover C names 1, not 79.
Isolated first-play closed concept-art names 79 at
`GS_UNKNOWN`.
The COMPLETE_LEVEL(110) goodie row names
that same `Update` owner; first-play leaves world 110
incomplete so goodie 14 stays `GS_UNKNOWN`. The leftover
COMPLETE_LEVEL(110) goodie row names that same `Update`
owner and the leftover complete-110 + E store; it does not
invent a world-110 FillOut. The Lost leftover
COMPLETE_LEVEL(110) goodie row names `ApplyUpdate`; isolated
Won leftover 14 does not uniquely prove the Lost return still
runs `UpdateGoodieStates`. The
new-goodie latch row names `CountGoodies` on that same owner;
`mPendingExtraGoodies` stays unclaimed. The
replay CountGoodies-delta row names that same `Update` owner;
it does not add another implementation. The
SET_GOODIE_NEW GS_OLD overwrite row names `SetNewIfNotDone` on
that same owner; it does not add another implementation.
FrontEndHandoff leftover of that same seed now names
`TryApply`. The leftover `GS_OLD` CountGoodies row names
`CountGoodies` on that same owner; isolated leftover
`GS_OLD` `NewGoodieCount=0` does not uniquely prove the
count of leftover Old. The leftover `GS_INSTRUCTIONS`
CountGoodies row names that same `CountGoodies` owner;
isolated leftover `GS_INSTRUCTIONS` store names the write
to 2, not the pre-count of 0. The leftover COMPLETE_LEVEL(110)
CountGoodies row names that same `CountGoodies` owner;
isolated leftover 14 names the write of 14, not the count of
4. The leftover GRADE(110) C CountGoodies row names that
same `CountGoodies` owner; isolated leftover C names the
write of 1, not the count of 6. Isolated leftover C
concept-art names the write of 79, not that count. Isolated leftover 14
CountGoodies uses ranking 0.0f so the count is 4. The
SET_GOODIE_NEW GS_INSTRUCTIONS store row names that same
`SetNewIfNotDone` owner; isolated leftover `GS_OLD` names
the skip for state 3 and does not uniquely prove the store
when `mState == GS_INSTRUCTIONS`. The
GetAndReset replay-latch row names that same `Update` owner;
it does not add another implementation. Isolated GetAndReset
is already pinned on `RetailCareerCounters`. The
Lost-latch row names that same `Update` owner via the Lost
early return. The Lost mSlots-skip row names `ApplyUpdate`;
isolated `ShouldOverwriteFromEndLevel` does not uniquely
prove the early return. The Lost ReCalcLinks / base-things
skip row names that same `ApplyUpdate` owner; isolated
`UpdateBaseWorldExistsStuffForNode` and
`Level100Lost_DoesNotTouchTheTrainingGraph` do not uniquely
prove leftover Blank all-1s. The
world-100 kill-skip row names `RetailCareerCounters.UpdateThingsKilled`;
`ApplyUpdate` is the already-pinned caller. The
FrontEndHandoff goodie row names the same `TryApply` owner as the
Won handoff. The FrontEndHandoff latch row names that same
`TryApply` owner; isolated ApplyUpdate latch and the
FrontEndHandoff goodie-state test do not name the two globals.
The FrontEndHandoff leftover COMPLETE_LEVEL(110) goodie row
names that same `TryApply` owner; isolated leftover 14 and
Lost leftover 14 do not go through `TryApply`.
The FrontEndHandoff leftover GRADE(110) C-goodie row names
that same `TryApply` owner; isolated leftover C names
ApplyUpdate and FrontEndHandoff leftover 14 names 14, not 1.
The leftover C concept-art store is not named there.
The FrontEndHandoff leftover GRADE(110) C concept-art
row names that same `TryApply` owner; isolated leftover C
79 names ApplyUpdate and FrontEndHandoff leftover C names
1, not 79.
The FrontEndHandoff GRADE(110) C-goodie closed row names
that same `TryApply` owner; isolated closed GRADE(110) names
ApplyUpdate and leftover C FrontEndHandoff names 1 as New.
The FrontEndHandoff GRADE(110) C concept-art closed row
names that same `TryApply` owner; isolated closed concept-art
names ApplyUpdate and leftover C FrontEndHandoff names 79
as New.
The FrontEndHandoff COMPLETE_LEVEL(110) goodie closed row
names that same `TryApply` owner; isolated closed
COMPLETE_LEVEL(110) names ApplyUpdate and leftover 14
FrontEndHandoff names 14 as New.
The FrontEndHandoff leftover mNumAttempts row names
that same `TryApply` owner; isolated leftover 7 / 11
names ApplyUpdate and existing FrontEndHandoff ranking /
Complete / CareerInProgress / goodie tests do not name
`+0x38`.
The FrontEndHandoff leftover SET_GOODIE_NEW GS_OLD
overwrite row names that same `TryApply` owner; isolated
leftover `GS_OLD` names ApplyUpdate and existing
FrontEndHandoff S goodies start `GS_UNKNOWN`. Replay
CountGoodies names already-`GS_NEW`.
The FrontEndHandoff leftover SET_GOODIE_NEW GS_INSTRUCTIONS
store row names that same `TryApply` owner; isolated leftover
`GS_INSTRUCTIONS` names ApplyUpdate and does not uniquely
prove the store through `TryApply`.
The SetSlotSave persist row names `PersistCareerSlot` on
`RetailSetSlotSave`; isolated FrontEndHandoff overwrite
names the 32-dword assignment after `TryApply` and does
not uniquely prove the mid-mission `CCareer::SetSlot`.
Live `GAME.mSlots` stay unclaimed.
The init MOS-failed row names `FromLevel100MissionStatus` on
`RetailGameObjectiveCount`; isolated FillOut Won names four
`MOS_COMPLETE` (1) and does not uniquely prove init's
`MOS_FAILED` (2).
The init text-dword row names `FromLevel100MissionPrimaryTextIds`
on that same owner; isolated MOS-failed names state 2 and
does not uniquely prove `[eax+4]`. Isolated FillOut Won
names four `GetStatus()` words and does not copy the text
dword. Live `GAME.mSlots` stay unclaimed.
The AddScore incrementer row names `RetailAddScore.Add`; isolated
`ScoreDelta` = 50 names the rebuild accumulator and does
not uniquely prove `add [0x008a9b8c], eax`. One live
`AddScore(50)` is not unique versus replace. Isolated
FillOut `ScoreWord` copies a parameterized dword. First-play
elapsed and FillOut score stay unclaimed.
The EnableFlightMode flight-flag row names
`RetailEnableFlightMode.Enable`; isolated
`FlightModeEnabled` = true names the rebuild bool and
does not uniquely prove `mov [ecx+0x58c], 1`. One live
store of 1 is not unique versus increment from 0.
The takeoff-store row names that same `Enable` owner on
Simulation: skip store leaves `Level100FlightEnabled` true
and ToggleMode rejected. Wrapper gate `test [ecx+0x34], 8`
and Disable clear / morph stay unclaimed. ChargeWeapon
stays unclaimed.
The HighlightHudPart / UnHighlightHudPart HUD-word row
names `RetailHighlightHudPart.Unhighlight`; isolated
`Emphasized` = false names the rebuild bool and does
not uniquely prove `mov [eax*4+0x008aa51c], 1`. Skip
UnHighlight after Highlight leaves 2. Array extent and
state-1/2 HUD meaning stay unclaimed. ChargeWeapon
stays unclaimed.
The EnableWeapon walker-active-flag row names
`RetailEnableWeapon.Enable`; isolated `Enabled` = true
names the rebuild bool and does not uniquely prove
`mov [edi+0x9c], 1`. One live store of 1 is not unique
versus increment from 0. Wrapper gate
`test [eax+0x34], 0x10` and Disable store-0 /
ChangeWeapon stay unclaimed. ChargeWeapon stays
unclaimed.
The SetObjective / UnsetObjective thing-flag row names
`RetailSetObjective.Mark`; isolated `IsObjective` /
navigation name do not uniquely prove
`or [esi+0x2c], 0x20`. One live store of 0x20 from 0
is not unique versus replace. Isolated Mark(0x04)=0x24.
Noticeboard Add/Remove stay unclaimed. ChargeWeapon
stays unclaimed.
The Pause / PlayCharMessageWait stop-flag row names
`RetailIScriptWaitStop.Stop`; isolated `PauseTicks` /
`MessagePlaybackTicks` name the rebuild sleep and do
not uniquely prove `mov [0x0089c800], 1`. One live
store of 1 is not unique versus increment from 0.
CVM snapshot / 0.05f / FollowWaypointWait /
PlayAnimationWait / Run-yield stay unclaimed.
ChargeWeapon stays unclaimed.
The InJetMode type+recency row names
`RetailIScriptInJetMode.Evaluate`; isolated
`JetModeState` names recency without
`test [ecx+0x34], 8`. Isolated
`PlayerInJetMode == (mode == Jet)` names the
rebuild bool. An airborne walker Battle Engine is
TRUE here. EnableFlightMode wrapper gate /
Disable / lock-set / Move / Morph / UpdateCamera
stay unclaimed. ChargeWeapon stays unclaimed.
The FollowWaypoint start-follow-flag row names
`RetailIScriptFollowWaypoint.Start`; isolated
FollowWaypoint emit-command names the rebuild
path / flag 0 and does not uniquely prove
`mov [esi+0x18], 1`. One live store of 1 is
not unique versus increment from 0.
FollowWaypointWait early-out / CVM snapshot /
0.05f / `+0x1c` / `+0x14` / `+0x24` /
AddEvent 2000 stay unclaimed. ChargeWeapon
stays unclaimed.
The score-time arm row names `AfterScoreTimeArm` on
the already-pinned FillOut owner; it does not rewrite
`ForLevel100Won`. The score-percentage last-wins row names
`Level100ScorePercentage` on that same FillOut owner. The
exact-D row names the same `AfterScoreTimeArm` owner; it does
not rewrite `ForLevel100Won`. The last-wins S-score row names
`Level100SGradeScore` on that same FillOut owner. The last-wins
D-score row names `Level100DGradeScore` on that same FillOut
owner. The ConfirmedKill allegiance row names
`RetailConfirmedKill.Apply`; first-play totals stay unclaimed.
The remaining incrementer-slot row names that same
`Apply` owner; it does not rewrite first-play totals.
The null player-reader row names that same `Apply`
owner; first-play still has a live player 0.
The TF_DYING store-0 row names `BaseThingLeftWord` on the
already-pinned FillOut owner. The player-null kill-zero row
names `ThingsKilledReadout` on that same FillOut owner;
first-play still has a live player 0. The pre-arm score/time
row names `ScoreWord` / `TimeTakenWord` on that same FillOut
owner; first-play elapsed and `this+0xf4` stay unclaimed.
Charge is not in
`StateHasher` as a field: the hashed field it now changes is projectile
`Kind` (FullyCharged fires Large). The career graph is not in
`StateHasher` because it does not change fire, movement, or any other
hashed field. So no cold-start or
full-chain trace can reach the other twenty, and the focused test is the
only falsifier they have. That is exactly
the precedent the jet-friction row set: a green replay suite there was
*vacuous* with respect to the constant it was supposed to guard, because the
jet throttle caps below the gate's band. The seventeenth through
forty-first rows' mutation kills were measured on 2026-08-18, 2026-08-19,
and 2026-08-22 in this worktree; they are
not among the 17 files
under `local-lab/rebuild-parity-mutation-kills-2026-08-17/`.

## Closing the two selector/briefing gaps (2026-08-22)

A reviewed selector run exposed two reconstruction gaps: the SELECT LEVEL name
band was pinned to world 100 after another node was selected, and MISSION
BRIEFING composed world 100's Tatiana copy for world 110. Both shared one root:
the reconstruction only ever decoded TEN strings out of the released English
language table, so it had no per-world text to draw. The table itself
(`data/language/english.dat`, SHA-256 `789ecff6…`, v3, count 2571) carries far
more:

- **Per-world name rows.** N.NN-titled strings exist for every node of the
  career graph — `1.00 - Training Level` (the already-pinned
  `level100` row), `1.10 - Blackout`, `2.00 - Interception`, … through
  `8.00 - The Sentinel Awakes`. The materializer now refuses a document
  that lacks any of them.
- **Briefing pages in pool-authoring order.** The table's text IDs are a
  hash-scrambled space with no pinned derivation (none invented), but the
  TEXT POOL preserves authoring order, and from world 100's Tatiana pair
  onward it is consecutive nine-slot groups in career-node order: two body
  paragraphs plus reserved empties. World 110's group carries its own
  authored copy ("Communications with the mainland have been lost…" /
  "Defend the base and prevent the invasion force…"), byte-present in the
  shipped data all along. Exactly four worlds (611/612/621/622) carry a
  measured third paragraph; every other reserved slot in every group is
  empty. MEASURED 2026-08-22 from the pinned table by
  `materialize_retail_assets.py::_frontend_world_strings_bytes`; the whole
  document is exact-reproduced at SHA-256 `ffe3d3f8…5408`
  (`Assets/Frontend/english-worlds.json`).

What changed, by owner:

- `materialize_retail_assets.py` — the decoder above. Latest-main
  materialization reproduces 379 exact files, including the new pinned JSON.
  The landed shared later-world owner still admits worlds 200 and 300 through
  their own `_WorldLevelHeader` rows; the selector merge neither duplicates nor
  weakens those header, script, HFLD, BSWD, actor, or hash assertions.
- `RetailFrontendWorldStrings.cs` (Core) — the 43 name rows and 43 briefing
  bodies as generated literals (mechanically generated from the pinned JSON,
  never hand-typed); empty means draw-nothing.
- `RetailFrontendSession.cs` (Client) — `SelectedLevelName` and
  `SelectedBriefingBody` expose the SELECTED node's rows without replacing the
  caller-injected read-only career descriptor/selection/handoff state.
- `RetailFrontendFlow.cs` (Godot) — the SELECT LEVEL band draws
  `_session.SelectedLevelName` instead of the unconditional `_level100Text`;
  MISSION BRIEFING draws `_session.SelectedLevelName` plus
  `_session.SelectedBriefingBody` wrapped greedily at the measured 286px ink
  ceiling (`WrapBriefingParagraphs`), with the transcribed literal demoted
  to the world-100 receipt; `LoadLocalization` cross-checks english.json's
  `level100` row against the decoded table. The injected career list and its
  selected-career handoff remain the page's source.

Honest boundaries of this closure:

- **The selector-band law is measured-consistent, not source-proven.** The
  band following the selection is inferred from shipped data (a name row exists
  for every selectable node) because `FEPLevelSelect.cpp` remains absent from
  the source drop and no retail capture of a selected non-root node exists here.
  If a future capture shows retail pinning "1.00" while the ship marker sits
  elsewhere, this inference is falsified and reverts.
- **World-100 pixels are unchanged by construction**: cold selection is world
  100, whose row and wrapped body reproduce the transcribed lines the earlier
  probe screen-matched. No GUI launch was spent proving what the session tests
  already pin mechanically.
- **Wrapping for worlds other than 100 is law-driven, not pixel-measured**: no
  retail reference frame shows another world's briefing page. The breaks follow
  the same greedy 286px rule that reproduces world 100's transcribed breaks
  exactly; they are not claimed to match unobserved retail pixels.
- The briefing VIDEO inset (`PC_<world>_exact.vid`) remains undrawn; nothing
  here changes that earlier finding.

Thing/Actor base state is no longer absent: Core now owns source-backed
visibility/dying/shutdown flags, current/old pose, linear/angular velocity, the
source-composed actor lineage mask, and exact contact-time bits through
ThingActorBaseState. Level100ActorRegistry consumes that owner while preserving
its released leaf type-mask projection, setup/teleport pose reset, and existing
full-stop behavior. Render/audio virtual defaults and a generic gameplay
framework remain open.

## Parity dimensions and their gates

| Dimension | What it means | Current gate | What the gate does / does not prove |
|---|---|---|---|
| **Deterministic sim state** | Identical tick-by-tick simulation given identical inputs | Core cold-start + full-chain tests, `--expect` trace hashes | Proves determinism and self-consistency. Does NOT prove retail equality — retail has no tape to replay against |
| **Visual frame** | Rendered output matches retail within tolerance | `npm run ref:godot-capture -- --plan mainmenu` (through `godot-offscreen`) + `score_frontend_capture.py` + `frontend-regions-*.json`, `gameplay-regions-level100.json` | Proves region-level visual regression against captured retail references. The JSON thresholds are **regression ceilings, not parity claims** (rebuild README is explicit) |
| **Audio** | Playback behavior matches retail | None (no automated audio gate) | Not gradeable today |
| **Timing / feel** | Response latency, animation cadence feel equivalent | None dedicated | Not gradeable today; 20 Hz step + floor semantics are the strongest proxy |
| **Content completeness** | All retail content reachable/present | Materializer hash checks (200+ pinned inputs), level-100 slice | Proves the retail inputs consumed are byte-exact. Does NOT prove every level plays |

## The honest reading

The lane runs a scoring harness that is documented as *not* measuring the
thing the lane exists to achieve. Visual parity gates are regression
ceilings; there is no *whole-run* gate today that compares rebuild behavior to
retail behavior directly, because retail is not automatable in the same
harness. This gap is named here so it is not mistaken for closure.

What the table above adds is narrower and real: **per-contract** gates that do
compare rebuild behavior to retail behavior, because the expected value in each
is read out of the pristine specimen rather than out of the rebuild. Twenty
laws is not parity. It is the first set of rows where a wrong rebuild is
mechanically detected instead of merely plausible-looking, and the mutation kill
is what distinguishes the two.

## What "done" would require (per dimension)

- Sim: a retail-derived expected-trace source (e.g. a verified capture of
  retail's own sequence) or an accepted contract per system.
- Visual: measured tolerance windows per region against retail frames, not
  just self-regression.
- Audio: a playback contract (tracks, triggers, volume semantics).
- Timing/feel: a documented comparison protocol (the maintainer's ear is a
  legitimate instrument for feel; it is not a substitute for the others).
- Content: per-level playthrough evidence, not only input hashes.

## Standing rule

No claim of parity may be published from this file's existence. Parity is
per-dimension, measured, and gated — or it is not parity.
