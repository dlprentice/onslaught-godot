# Parity measurement log

Status: active — the dated measurement entries that PARITY.md used to carry; each entry records what was measured on its date
Last updated: 2026-11-11 (moved out of PARITY.md unchanged; PARITY.md owns the current state)
Evidence: MEASURED — each entry names its own capture, byte comparison or test; counts are the counts on the entry's date and are not re-run here.
Specimen: `BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`
(the retail addresses cited on this page are from the preserved
pristine specimen; do not infer another executable's identity from its folder name).
Summary: dated records of the parity core's comparisons with the native port and the original game, newest gate runs first, then the earlier measurements by milestone.

This page is a record, not a status page. It keeps the measurements that PARITY.md's milestone table points to, with
their dates, inputs and limits. Where an entry says "above" or "below", it means this page. The current state, the
milestones' pass criteria and the standing exceptions are in [PARITY.md](PARITY.md); the terms used here (Present, tape,
probe, golden, Stuart, RED/restored/GREEN and the others) are defined at the top of that page and in the
[glossary](README.md#terms-used-in-these-documents).

## Latest entries (2026-11-02 to 2026-11-04)

**2026-11-04 — inline orientation register range.** The two walker-leg products and the cached-frame and
cached-pose orientation products now use the existing PC24 arithmetic owners until each cell's final float store.
Their per-cell operand orders are unchanged. [InlineMatrixRangeTests](OnslaughtRebuild.Parity.Tests/InlineMatrixRangeTests.cs)
adds twelve authored checks covering subnormal terms that accumulate or cancel and oversized finite terms that
cancel before storage. All twelve fail on the former code and pass with the correction; the twenty-six existing
focused matrix and bounded-arithmetic checks still pass. A separate authored native x87 PC24 control checks all six
term permutations for each of four exact-power cases: all twenty-four agree, while premature float stores disagree.
That control contains no original-game code or recorded inputs; it validates arithmetic, not an observed gameplay pose.

The latest unfiltered Parity run reports **1,184 passed, 0 failed, 3 skipped** of 1,187 found, with the existing reference
views configured, and the Host build passes. The same two occupancy references and one sample-decoder oracle remain
unavailable, so the required reference gate is still **INCOMPLETE**. An earlier attempt reached its 6 GiB memory cap
without producing a final test result; the completed run used the established 10 GiB suite allowance. The failed
attempt is retained, not counted as a pass. Whole-matrix admission avoids repeating the same range checks for each
cell, while mixed-range matrices retain their per-cell fallback. An authored Release microbenchmark with tiered
compilation disabled measures about 134–145 ns per corrected matrix product, versus about 7–8 ns for the former
plain-float calculation, with equal normal-input checksums. This is arithmetic overhead, not measured frame cost;
actual call volume and gameplay performance have not been measured here. Original NaN/infinity behavior,
body-roll and animated-hierarchy orientation sites, the separate inline point products, and whole-game visual
parity retain their own unresolved boundaries.

**2026-11-03 — legacy replay-view cases.** The four cases that read the legacy replay-harness view (three `ClockTests`
comparisons and `HarnessTests.RetailLevel100TapeAndGoldenParse`, the M0 parse) ran and passed on
[888eb05](https://github.com/dlprentice/onslaught-godot/commit/888eb05d2ceb44bd43a9a1e24c17ae9d25abec3f) with
`ONSLAUGHT_PARITY_DATA` set. One unfiltered Parity run reported **1,172 passed, 0 failed, 3 skipped** of 1,175 found.
The required reference gate stays **INCOMPLETE**, now with three open cases instead of seven: two occupancy-reference
checks and one sample decoder/converter oracle check. Conditions, a control and limits are in the
[2026-11-03 entry](#2026-11-03-legacy-replay-view-cases).

**2026-11-02 — flow Host keyboard focus boundary.** The [flow Host](OnslaughtRebuild.Host/Host.Flow.cs)
connects window focus callbacks to the [keyboard owner](OnslaughtRebuild.Host/FlowKeyboardInput.cs), which
releases forwarded held keys on focus loss and ignores native keys while unfocused. Shutdown detaches the
callbacks before disposing the run. The [nine authored tests](OnslaughtRebuild.Parity.Tests/HostFlowKeyboardTests.cs)
passed, including a held-focus causal control, queued presses, replay exclusion and shutdown; the live Host and
its Parity dependency compiled. One unfiltered Parity run on
[699b513](https://github.com/dlprentice/onslaught-godot/commit/699b5136555c11cb5b40920be21eed247bfd9c54)
reported **1,143 passed, 0 failed, 7 skipped**: four legacy replay-view checks, two occupancy-reference checks
and one sample decoder/converter oracle check. The required reference gate is **INCOMPLETE** while those inputs
are unavailable. These checks do not establish native focus-event delivery or gameplay acceptance.

## 2026-11-03: legacy replay-view cases

Four cases read the legacy replay-harness view: `ClockTests.Level100GoldenEventTimeFrameTimeAndLevelMatchEverySample`
(EVT, FT and LVL at every 50th Present to 10,000), the two per-Present clock cases
`ClockTests.Level100PerPresentEventTimeFrameTimeAndLevelMatchEveryPresent` and
`ClockTests.Level100OldSettingsEventTimeFrameTimeAndLevelMatchEveryPresent` (the same probes at every Present to
3,400, for the two recorded start-up settings), and `HarnessTests.RetailLevel100TapeAndGoldenParse`, the M0 parse.
The 2026-11-02 vector-hash run skipped them with `ONSLAUGHT_PARITY_DATA` unset (the recorded reason is that the retail
parity data was not found). The view's four files were there (they date from 2026-10-15), so an earlier "missing or stale
reference inputs" wording was wrong for these four.

With the variable naming that view (read only; the four files hashed the same before and after) and every other
reference variable as on 2026-11-02, a fresh build of
[888eb05](https://github.com/dlprentice/onslaught-godot/commit/888eb05d2ceb44bd43a9a1e24c17ae9d25abec3f) (no warnings)
ran the four cases alone: all four executed and passed. The unfiltered Parity run then reported **1,172 passed,
0 failed, 3 skipped** of 1,175 found. As a control, a view whose two per-Present goldens were swapped (links to the
same files, nothing copied) made both per-Present cases fail their exact-match assertion and left the other two
passing, so these passes depend on the view's content. The Host was not rebuilt: the only commits since the
2026-11-02 vector-hash run on
[ba98155](https://github.com/dlprentice/onslaught-godot/commit/ba98155af2a673c33439ee356b8179e87cc92aa0) changed this
document.

The three remaining skips are not input problems of this kind: two occupancy-reference checks are disabled in the
source until the native port's dumps are regenerated, and the sample decoder/converter oracle check finds no
`sample-pcm` results in the oracle view. The required reference gate therefore remains **INCOMPLETE**. What the four
cases add is bounded: at this source the harness parses the recorded level-100 tape and golden trace and reproduces
their change counts (M0), and the clock model's EVT, FT and LVL probes equal the recorded retail runs at every sampled
Present to 10,000 and at every Present to 3,400. They say nothing about gameplay beyond those probes.

## 2026-11-02: deterministic vector hashing

`FVector.GetHashCode` now combines the same component bit patterns used by equality
with fixed unchecked integer arithmetic. It no longer depends on a process-specific
hash seed. Signed zero and NaN payload distinctions remain consistent with the
existing bitwise equality; vector layout and simulation arithmetic are unchanged.

The two authored hash checks pass within the candidate full Parity suite: 1,175
discovered, 1,168 passed, the same seven reference-dependent skips as the preceding
source, and no failures. The Host build passed. The retained comparison ran six processes per source revision: the preceding hash
varied between processes; the corrected hash stayed equal. This establishes the bounded hash contract, not
new gameplay or cross-platform retail parity. The complete reference gate remains
open while those seven existing cases are skipped: four legacy replay-view cases were skipped because `ONSLAUGHT_PARITY_DATA` was unset, not for missing inputs (corrected in the 2026-11-03 entry), and three reference checks are disabled or lack reference results.

## Entries filed under the 2026-10-29 status

CE version-2 tapes (2026-10-28): the parser and the `GameRun`/level input paths retain ordered window-mouse and
character events, the recorded back-buffer size and replay-owned cursor as in the 0.1.9 recorder. A mismatched size,
unknown event or newer declared format is refused; the K/M-only `ParityRunner` refuses extended tapes. Both the
whole-process `GameRun` and standalone levels deliver input after each Present, including startup and Retry, with probe
observers called before the input. Tapes cannot be attached after their input has passed.
Authored controls cover message ordering, keyboard-trap character/scan, cursor and wheel behavior, size mismatch,
and device mouse state across level replacement (unread motion is consumed once; held buttons persist).
The new player recording parses without dropping any of its 3,083 events; parsing and authored controls alone do not
establish a completed port/rebuild replay, a player's original session, or parity. The port reference, starting files,
film settings and device/sound assumptions must be admitted separately before comparing the route.

Weapon-driven zoom (2026-10-28): `CBattleEngine::Move` selects a charge-dependent or normal target, keeps the
previous zoom for camera interpolation, and moves the current value by at most `0.1f` per update. The pristine
specimen's `004089fb..00408b82` contains the mode branches and uses the `0.1f` constant at `005d85c0`; the previous
rebuild skipped the weapon modes and used `0.05f`. `BattleEngineZoomTests` cover the mode and timer boundaries,
zero maximum charge and a PC24 rounding case. On a private, repeatable CE 0.1.9 full-boot level-100 port reference,
the corrected rebuild's sampled fields match through Present 7480; the prior build first differed in the CRT probe
at 5823 after its camera zoom made different objects visible. A separate capture found equal camera position and view
matrix but different gamut zoom inputs at Presents 5821–5822; the source-backed diagnostic correction produced the
port's gamut inputs on those frames. The route then stops at an unconfigured incoming-missile sound owner. This is a
bounded port comparison with no admitted sound-pool profile, not proof of retail parity or a completed rebuild win.

A separate isolated CE 0.1.9 replay of the same tape and full starting sidecar recorded all 256 sound-pool slots;
their ordered channel and position bits matched the previously admitted pool, and its complete port probes and
copied output-file hashes matched the original reference. `ReplaySoundProfile.CreateRecordedReplay` accepts only
the pinned private census receipt, its exact tape and sidecar, and the existing pinned pool and English sample bank.
It supplies the authenticated options and careers in memory and fixes the tape to that run. The sidecar's two
ancillary files are authenticated but not modeled. With this process-specific sound admission, the sampled port
fields match through Present 11670; the goodie-8 viewer is not implemented, so without a supplied load completion the
rebuild refuses there. With one observed, caller-supplied goodie-load completion the diagnostic run matches through the last
input at P12312 (the 2026-10-28 recorded-win entry below, with its caveats). This does not establish the recording's original
allocator contents, arbitrary process behavior, retail parity or a full rebuild win. The 2026-10-29 entry below compares the
same replay with the original game.

Lead-time sum orders and the recorded win against the original game (2026-10-29): `CBattleEngine::UpdateAutoAim` leads its
target by t = |bp - op| / |bv| * 20, and the pristine specimen adds the squares of both vectors as (Z² + Y²) + X²
(`0040b258..0040b293`). `CWeapon::GetPredictedTargetPos` adds |pos - tpos| the same way (`0050a1ac..0050a1ca`) and |vel| as
(Y² + Z²) + X² (`0050a1d4..0050a1f0`); a sum commutes, so these are one grouping, X last, not maths.h's X, Y, Z. The rebuild had
maths.h's order for both of UpdateAutoAim's magnitudes and for |vel| (the predicted aim's distance already had retail's); all
three now use `FVector.MagnitudeYZX`, as the native port does with its patches 0111 and 0112. `LeadTimeSumOrderTests` holds two
authored encounters (nothing is copied from a recording), each a case where the two groupings give different float32 lead times:
one only through the distance's grouping and one only through the speed's. With the former order three of its four checks fail
(the engine's tracking offsets differ by 1 to 173 ULPs, the predicted position by 1 to 2); the fourth guards the distance grouping
the rebuild already had. Reverting each of the four magnitudes alone, in a scratch copy, fails exactly the check written for it.

The recorded win of 2026-10-28 (the diagnostic admission above) differed from the native port with patch 0111 before the correction
in EVN alone: from Present 9493, at 1,932 Presents, the port one higher. After it the rebuild equals that port run on all 112,053
sampled fields at all 12,312 Presents (EVT, EVN, FT, SC, LVL, RNG, POS, LIFE and CRT, and the front end's line at 1,245), on both
END_LEVEL_DATA records, on the 663 goodie viewer states (against the viewer trace of the build without patch 0111; none was taken
on the patched build) and on the five careers and the options file byte for byte.

Against the original game (the pristine executable under the retail replay harness's shim in Wine, every Present sampled; a
private harness run, not a test in the Parity suite) the same
replay equals the recording's process fl2a, one of the two debriefing outcomes the original takes (2 of the 8 original processes
that reach the debriefing): EVT, EVN, FT, SC, LVL, RNG, POS and LIFE at all 12,312 Presents with none missing and none
different (POS and LIFE are 'null' in the original's probes on 2,477 of those Presents and RNG on 2,476, frames with no player,
which the comparator counts as equal to all-zero), the front end's line (the original's
three front-end lines joined) at 1,245 Presents, END_LEVEL_DATA at its two changes (P1 and P11404), and `BEA 1` to `BEA 4`,
`Linux Test 1` and `defaultoptions.bea` byte for byte; `BEA 4`, the career saved after the win, equals the original's. Before the
correction the same comparison differed in EVN alone, from P9493, the original one higher. The original is not repeatable on this
tape: `CFEPDebriefing::Process` tests a slot of an uninitialised `new FVector[100]`, so the first Enter at the debriefing
(Present 11468) was accepted in 6 of the 8 original processes, which leave the page at Present 11469, and ignored in 2 (fl2a is
one of these); the rebuild's slot is zero (`DebriefingPage.UnshownPositionX`), the ignored path, so it can equal that outcome
only. The original processes also differ from each other in pointer-valued object words.

Not established in this win measurement: the original's object words (the battle engine's, weapons' and jets'), whose auto-aim words differ in the native
port with the old order (that port measurement is not repeated on the rebuild); the game's C runtime random state (the original has
no probe for it); the goodie viewer against the original (it has no trace; the viewer states above are the port's); audio and
pictures; the loss route; any other tape. The sound-pool seed and the goodie-load completion are admitted from port processes, not
observed in the original. An instrumented replay of the same tape (a scratch build, not committed) evaluated UpdateAutoAim's lead
time 769 times, so that correction is exercised, but called `GetPredictedTargetPos` 173 times without once taking its predictive
branch (a weapon mode with `Predictive` set), and the native port's own observer found no sampled run of the port that reaches it;
so |vel|'s grouping rests on the pristine instructions and the authored case, not on a recorded value. Receipts are in the
maintainers' private evidence store.

Subnormal store rounding in the battle engine's decays (2026-10-29): `CBattleEngine::UpdateRotation` (its pitch limits and the
decays of the turn rates, the roll and the shakes), `CBattleEngine::Move` (the vibration), `CBattleEngineWalkerPart::Move` (the
friction and the speed cap), `CBattleEngine::DeclareOnGround` (a jet's slowing) and `CBattleEngineJetPart` (the slowing without
energy and the ground effect's descent) each form a product in the x87 register, a 24-bit significand at PC24 with the register's
wide exponent, and store it to a float; the pristine specimen's ranges are listed in `SubnormalDecayTests`. A subnormal product
therefore rounds twice, and C#'s float multiply, which rounds once, can land one unit in the last place away; a float's squares of
subnormal components are zero where the register keeps them. Compared with the original game, the rebuild's modelled battle-engine
and walker fields (the tables of `Level100sBattleEngineFollowsRetailsPerPresentObject`, at the same Present) were equal on the
recorded win (process fl2a) and on the two loss routes (the fe-lose and win1 music-clock pairs) at every compared Present except
the velocity, the pitch and yaw rates and the vibration, each one unit in the last place in a subnormal value, first at Presents
2531 (win1), 3686 (fe-lose) and 4208 (the recorded win). All eight first differences reproduce exactly as the register's product
followed by the store, with the decay factors 0.7 (the walker's friction, the vibration) and 0.8 (the turn rates).

The sites now use `(float)X87.Mul`, and the walker's speed cap and the jet's speed without energy keep their squares in the
register as `X87` doubles. `SubnormalDecayTests` holds authored cases (found by search from our own starting points; nothing is
copied from a recording) through the production methods; reverting any one of the 21 sites alone to float arithmetic fails the
check written for it, and the sources before the correction fail every site's check. A new oracle table, `mul_f_sub` (the VC6
harness's `mul_f` on 1,000,000 float pairs whose products are subnormal or at the boundary), matches `X87.Mul` followed by the
store's conversion in every case, while C#'s float multiply differs in 62,329 of them (`X87OracleTests`). Not changed: decays
elsewhere (other units, particles, cameras) and magnitudes compared only with a threshold (the battle engine's speed against
0.0001 in `Move`, the jet's squared speeds), which round the same way but change nothing measured. With the correction the
modelled fields equal the original's at every compared Present on all three tapes (9,835 Presents of the recorded win, 5,534
of fe-lose, 8,879 of win1), and every probe was as before: equal on the recorded win and fe-lose, and on win1 equal but for
the EVN difference from Present 5335 (one event more than the original until the Retry). The subsequent loss-route correction
below addresses that difference. With `X87_ORACLE_OUT` naming the tables, the Parity suite passed 1,098 tests with 7
skips (the three x87 oracle theories run). Receipts are in the maintainers' private evidence store.

Loss and Retry event parity (2026-10-29): `GetLaunchPosition` uses the original's out-of-line rotation constructor and
matrix product for the auto-aim endpoint. In `CRound::Init`, the straight-flight ground trace normalises its direction by
adding the Y and Z squares before the X square (retail 004d8848; native-port patch 0055). At one observed launch the
rebuild and the native port had the same round position and velocity, but the previous normalization order caused the
rebuild to miss the ground intersection the port found. Correcting the two call sites produces the original's event count
at the previously divergent Present; the round-direction test uses an authored vector, not captured game data.

No-hit auto-aim (2026-10-29): the original's nonballistic no-hit path also uses the out-of-line matrix product. The
rebuild now uses that product there; its former generic order differs for some authored rotations. `BattleEngineLaunchTests`
exercises the actual no-hit caller and retains controls for the hit path, the unsupported ballistic path and disabled or
unrequested adjustment. The 404 paired launch calls in the earlier win1 diagnostic all had a hit, so they do not establish
the no-hit path's behavior during a recorded game.

`OriginalLossRouteTests` compares the front-end loss route to each of two original music-clock recordings at all 7,300
Presents, including the measured front-end state, end-level data, modelled battle-engine fields and saved career. It
compares the loss-and-Retry route to both original recordings at every Present from 3 through 8,882, including the Present
made during the Retry reload, on EVT, EVN, FT, SC, LVL, RNG, POS, LIFE, end-level data and the available modelled
battle-engine fields. These are two controlled recordings, not evidence for every level, input or original-game outcome;
the tests do not establish audible or visual parity. References are private and a missing reference is a skip, not a pass.
The loader accepts identical repeated rows but rejects conflicting values for the same Present and field; the original
recordings repeat their last rows identically (`OriginalLossReferenceTests`).
The trainer, full-boot, front-end CRT and standard golden readers also reject conflicting duplicates rather than
silently choosing the last row (`ReferenceCoverageTests`); identical observations remain valid. These checks admit
the reference data; they do not prove the simulation itself matches it.

Out-of-line matrix register range (2026-10-29): the rotation constructor `004062d0` and matrix product `0040d320`
keep products and sums in x87 registers until the individual float stores. `FMatrix.MakeRotationOutOfLine` and
`MultiplyOutOfLine` now model that range: a subnormal store can round twice, and terms too small or large for a float can
still add or cancel before the store. `RetailMatrixSubnormalTests` covers authored discriminators for the rotation,
each matrix cell, underflow and overflow cancellation; the former implementation fails the controls. An isolated
execution of the original functions at PC24 agreed with all 45,000 result cells from 3,000 authored finite matrix pairs
and 2,000 authored rotations. At that register-model change, both recorded loss-route comparisons and the full Parity
suite passed. These are bounded arithmetic tests, not a demonstrated gameplay fix.

The out-of-line matrix product now uses integer PC24 arithmetic when each operand is zero or has magnitude between
2^-32 and 2^32, inclusive; otherwise it retains the register model. In this bounded domain a nonzero rounded product
is a multiple of 2^-87 and all sums of up to three products stay below 2^66, so a nonzero result is always normal and
finite as binary32. `BoundedPc24Tests` checks authored midpoint, cancellation, signed-zero, fallback and independent
integer-dyadic controls; its rounding-mode control runs on Linux x64 glibc and reports a skip elsewhere. Two products of
stored float values in the out-of-line rotation constructor use the same bounded primitive. This does not establish
portability of the trigonometric functions, the other rotation operations or the general `X87` model. Original NaN and
infinity behavior, other inline matrix/vector sites and the separate straight-round normalization remain open.

The Parity suite gates each route at its stated floor against references kept outside the repository
([README](README.md#working-lane)); a missing reference is reported as a skip, which is not a pass. Suite counts in
the dated entries below are the counts on their dates.

Pilot-win projectile contact (2026-10-28): the pristine specimen's `CCylinder::CollideWithLine`
instructions at `00440801..0044083d` sum the Y and Z squares before the X square for the two lengths.
On the pinned native port `48738e88ad216323682186a214a2ebd95cce30f213347326f200b5319a139ca5`,
the pilot-win tape's projectile 4034 hit the player cylinder with contact Z `c12dac0d`; the previous
X-first reconstruction returned `c12dac0e` and differed in LIFE starting at Present 26368. A fresh native
observer recorded the input positions, motions and cylinder/line dimensions used in
`CylinderLineTests.LineContactUsesTheRetailMagnitudeOrder`. The test passes with the retail sum order and fails
with the old order. With this correction, `ThePilotWinRouteMatchesThePortThroughTheLevelsEnd` compares all nine
fields every Present through the level's end at 28306, its end-level data, and the outro to 28600 against that
port reference. This is a port replay plus static retail evidence, not a retail win or device-audio acceptance.
The earlier `start` and `delta` squared-length checks use Y+Z+X at `0044062d..0044064f` and
`004406a3..004406bf`; the `end` check remains X+Y+Z. On captured pilot-win inputs, the old and retail sum orders
produce different float32 values by one ULP at both sites. Two authored boundary variants derived from those inputs take different branches under the old model;
an isolated execution of the pristine function at x87 PC24 rejected both, while the adjacent-radius and inside-end
controls passed. `CylinderLineTests` reproduces those four outcomes. This is an isolated original-code experiment,
not an observed retail gameplay branch. Managed float products do not preserve x87's wider exponent, so underflow,
overflow and other exceptional intermediates, and the remaining collision arithmetic, still need separate evidence.

Launch-hit orientation (2026-10-28): pristine `74154bfa…` at `0040d090..0040d0b0` calls the x87 elevation at
`0040d1a0` and the Euler constructor at `0040d1f0`; its floating-point instruction sequence matches `004062d0`, which
the managed `StraightAimOrientation` models. On the fixed level-100 pilot-win **port** replay, two captured adjusted
hit vectors now produce the same orientation as the port, including cases where the former `Math.Asin` or inline
constructor model was one ULP off. The route and all ordinary controls still require their own gates; these observations
do not establish real-player or full-game parity.

Site-specific matrix products (2026-10-28): `CMCMech::UpdateBone`'s two leg products use the per-cell orders in the
pristine instructions `0049ae63..0049afbb` and `0049b02e..0049b18e`. The cached-frame and cached-pose world products
have their own orders (`004b1402..004b153a` and `004b4f7b..004b50b9`); the latter's position uses
`004b4ef2..004b4f3f`. The general product remains its historical left-to-right model rather than imposing one
out-of-line order on every caller. `MatrixOrderTests` drives both cached paths, checks all cells of these four products
with finite cancellation inputs, and rejects the former models on selected cells. A pinned-Clang source-derived
control of the native port's second-leg helper differs from the pristine grouping on two cells for one authored input;
an actual game pose and the port's inlined machine code at those cells remain to be compared. The grouping correction alone did not preserve x87's wider exponent: an isolated PC24 control with authored small
operands showed terms being lost before the final float store. The 2026-11-04 entry above extends these four
orientation products to the register arithmetic model and closes the measured finite range cases. Original
nonfinite behavior, full visual or gameplay parity and other inline sites, including the body-roll and
animated-hierarchy orientation products, still need separate review.

The `CMCMech::UpdateBone` animated-hierarchy **point** product now uses the grouping in pristine
`0049b719..0049b775` (specimen SHA-256 `74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`):
X is `(Z+Y)+X`; Y and Z are `(Z+X)+Y`. An isolated PC24 execution of those unchanged instructions produced the
expected values for two cancellation cases, an identity control and an authored float-rounded rotation/key-position
case. In the latter, the previous generic transform gave Z `403197a4` instead of `403197a3`; the new test exercises
the managed `UpdateBone` hierarchy path, and all three differing cases reject the former code. These are authored
inputs, not captured gameplay poses; the wider-x87-exponent and exceptional-value limits above also apply here.

Opt-in live device audio (2026-10-27, M7 remains incomplete): `HOST_LIVE=1 HOST_AUDIO=1` adds effects and prepared
PCM voices through the shared sound process. It is separate from `HOST_TAPE`'s virtual device and is not used by
`HOST_LIVE=flow`. The direct production-device V12 fixture on commit
`249fd0cda343fb0df465d867191dadcd2b4ce943` passed real Godot/PulseAudio playback, advancing cursor,
pause/resume and focus-suspension ownership, channel-generation reuse, teardown and reinitialization. It captured
nonzero PCM through an independently checked private null-sink route. Dummy and unavailable-output controls
refused before sources or listeners were created, and owned playback references/nodes retired at teardown.

This was a direct caller of the production audio classes, **not ordinary Host gameplay**. It establishes no
physical-device, heard-output, whole-tutorial, waveform-equivalence or full audio-parity result. The receipt and
remaining boundaries are in [the Host audio contract](OnslaughtRebuild.Host/AUDIO.md#measured-device-fixture-2026-10-27).
The existing deterministic-reference and whole-game routes retain their own sound admission and acceptance limits.

CGameInterface on PC (2026-10-21): the in-game menu rises only at a lost or drawn level's end, after
`CGame::Update` has made continue, message log, level briefing and options unavailable (onslaught-decomp
`src/game.cpp:2105..2129`); its other toggles are commented out and nothing sets the joypad picture. Those
items are unreachable on PC and stay unported; the PC pause menu (`CPauseMenu`) carries continue, the
message log and the briefing.

Trainer missile launch and flight (2026-10-21): the level 100 trainer's first LIFE difference
(Present 16206) came from the Forseti Missile's state before its collision, not from the collision.
Per-frame traces of each missile against the pinned native port, with retail's own code run on the
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

The pinned native port's ordinary-input diagnostic route now destroys **all 4 + 3 + 6 ground
targets**. A Parity replay ran it through Present 15,130 and observed all 18 real script posts
at the same completed Present; the observer forwards each original call once and leaves the whole
trace unchanged. After the stopped-round query correction below, **all nine probes agree at
every Present 3..15,130**, across all 136,152 comparisons. This is selected-state agreement with
pinned native port build `e55cb2d8` (not a commit in the public Onslaught repositories),
not a fresh retail whole-route comparison. The tape SHA-256 is
`b083d308cc29bb108d2d4a15bc8ee7a1c17d7086ae386682fb2f9425b9d69919`.
The native route starts with `-level 100 -skipfmv`; cold-start/menu/audio acceptance and full tutorial
flight/refuelling/later combat/win/save remain separate. Its later evasion attempt takes hits;
it is not a perfect-score or no-hit run. Receipts (focused tests, independently executed original code, full ground
probe comparison and 18-post admission) are in the maintainers' private evidence store.

The fixed native route subsequently reaches the first flight zone and genuinely destroys its first
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
Private evidence remains outside this repository, including
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

A subsequent noninterfering native first-hit observer preserves all 145,854 ordinary probe fields
through Present 16,206 and captures eight paired callbacks without errors. Original Explosion.Hit
returns native damage `3ecf1286` on the native inputs and managed damage `3ecf1334` on the
managed inputs. Player position, radius and configuration agree; projectile position already differs
at the direct Round.Damage boundary, before explosion placement. A separate managed collector
preserves its entire 16,204-record trace and confirms unchanged position/velocity from Round.Hit
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

A Parity replay compared all seven primary probes for every Present 3..13,000 with the pinned native port:
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

## Entries filed under the milestones, 2026-10-15 to 2026-10-23

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

Observed and modelled Present schedules (M6 and M7). The boot films (`rebuild/OnslaughtRebuild.Parity/Flow/BootFilms.cs`, from CSystem::Init and
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

Evidence: two offscreen processes of a native-port build (binary SHA-256
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

Career, options and pause saves now refresh the stored display key from the modeled current mode,
following `CCareer::WriteOptions`. This uses the existing `MachineDisplay` list; actual host display
discovery and startup selection of the saved mode remain open.

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
- Slice 2 draws the front end. `rebuild/OnslaughtRebuild.Parity/Flow/Draw` forms the quads as the game forms its vertices (`DrawSprite`,
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
- Measured against the port's back buffer (port build `ae04185f`, which is not a commit in the public Onslaught repositories; fe-r1, every 10th Present from 160 to 680; main page, quit box,
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
- Measured against the port's back buffer (port build `ae04185f`, not a commit in the public Onslaught repositories), as mean per-channel difference from the software oracle on
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
  which the next retail pair can log. Level return/retry and pause or level-end menus now synchronize the modeled
  mouse device and controller context on release and reacquisition. Authored controls cover the state and input
  activity paths; they do not establish physical-device behavior.

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

The following 2026-10-22 and 2026-10-23 entries describe the earlier port build and tests as measured on those dates.
The current route tests use the newer port reference identified in the projectile-contact entry above; the old event
observer was not rerun for those new references.

Measured (the repair pad route, 2026-10-22): on the tutorial pilot's p22-fire tape (c17-fire's input to P22150, then
the return to the Forseti repair pad and the flight out), against the native port's every-Present probes
(`l100-p22-port-20261022`, the fixed 0029+0030 build), the Core matches every probe field through P26720, the
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
fixed 0029+0030 build, to P28600): every probe field matches every Present through P28306, the level's last. The level
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
stealth, so it never cloaks). The run uses the level-100 routes' measured sound-pool profile (the fixed e55 -level
process's 256 seeds), not this process's; every field still matches. Retail is not the reference here: in each of the four
recorded retail pairs (pristine 74154bfa under the shim) the two runs of the same tape differ from each other, first
between P3918 and P5003.

Measured (a separate recorded level-100 win, 2026-10-28): an isolated CE 0.1.9 full-boot replay, with the tape's
copied starting profile, virtual 30 Hz clock, 640×480 offscreen display and dummy audio, reached the won debrief and
the player's goodie-8 mesh viewer. A diagnostic rebuild run using that replay's separately observed sound-pool seed and
one instrumented process's first-poll goodie-load completion matches its 112,053 ordinary sampled fields through the
last input at P12312, both 1,484-byte END_LEVEL_DATA snapshots, 663 sampled goodie grid/viewer states, all five isolated
final career files and default options byte for byte. A second instrumented port run had the same complete probe and
final-profile hashes; it showed why the rebuild must release DirectInput on return before window mouse moves, and why
saves must read the selected device mode rather than retain the initial saved key. These are port-replay comparisons,
not retail, original-recording process-memory, audible or visual parity.

`GoodiesPage.StartMeshLoad` accepts a deterministic caller-supplied completion observation for the diagnostic goodie-8
mesh projection. The resource reader checks its GDIE mesh descriptor and reads the referenced mesh; it skips texture
records and does not draw the item. The native load is asynchronous, so this one observed completion is not a general
schedule. Other goodies, manual mesh manipulation, the viewer's camera/particle effects, text-height-dependent scrolling
and graphical presentation remain incomplete. Without an explicitly supplied load the viewer still refuses to run.
The save-mode model uses its explicitly selected `MachineDisplay` mode; actual host mode discovery and complete startup
mode selection remain separate work. The recording's two ancillary start files are authenticated but not modelled.

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
P12817 preview boundary, not a demonstrated natural game quit. The captures use Godot's Compatibility renderer on the Intel GPU.
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
the fixed port's back buffers (port build `ae04185f`, not a commit in the public Onslaught repositories; every 25 Presents, P8500 to P9500), the count appears at the same Presents
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
`_PC_24` on the maintainer's machine (Intel i7-13700HX), 10^6 inputs per operation including specials. Add, subtract, multiply,
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
