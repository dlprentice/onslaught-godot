# Rebuild determinism contract

Status: active — the contract a contributor breaks first
Last updated: 2026-09-26 (the retail load order, the pre-run, INIT_SCRIPT events and round events; earlier entries keep their dates)
Evidence: SOURCE and bounded copied-runtime observation — constants and behaviors cited against
`references/Onslaught` (thing.h, eventmanager.cpp) and the tracked Core and
Headless sources named at the bottom; the retail 20 Hz step was MEASURED in
the 20 Hz migration evidence. Precision setup is static byte evidence; the
September 8 Plane control-word sample below is a separate runtime observation.
Specimen: `local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`.
Summary: what "deterministic" means in this rebuild, what is enforced, and
what a contributor must do when a change legitimately moves the trace hash.

## The fixed step

`OnslaughtRebuild.Core` advances on a fixed 20 Hz step:

- `CLOCK_TICK = 0.05 s` / `GAME_FR = 20.0` — `SimulationConstants.cs`, derived
  from `references/Onslaught/thing.h:28-29` and `eventmanager.cpp:296`
  (`mTime = mFrameCount * CLOCK_TICK`).
- Retail floors every scheduled delay onto a whole 20 Hz boundary
  (`delay *= GAME_FR; delay = floorf(delay)`, `eventmanager.cpp:210-212`);
  the rebuild does the same.
- The prior 30 Hz step was migrated out in the 20 Hz work; constants that
  carry a tick-rate derivation are verbatim retail values where retail fixed
  them (e.g. the landing-thruster factor is retail's 0.975 exactly).

## Retail geometry precision

The mesh-pose and passive sphere/bounds primitives use 24-significand-bit,
round-to-nearest-even operations with explicit float32 stores. `RetailFloat24`
retains a double carrier between operations: x87 precision control does not
reduce its exponent range to float32. This bounded geometry model follows
device-creation intent. September 8 also measured PC24/RN at twenty-five
Plane/guide calls on the private WineD3D route described below; other sites
and rendering backends remain unmeasured.

Fresh September 8 inspection of pristine `BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`, found:

- Startup requests 53-bit precision through `0x00560cb1`, without changing
  rounding control. `[0x00560cb1,0x00560cc3)` hashes to
  `38dcf78aa3dab9d5e661a8e7fbaabcbab0399decb645b9d088bed196a65eef42`;
  the control-word mapper `[0x00569449,0x005695af)` hashes to
  `da7de6a1ae9b07904d40360b02191a2cdc51ff80da00eaabf02926bbc1ac8820`.
- The retail import is `d3d9.dll!Direct3DCreate9`. Enumerated device flags are
  `0x50`, `0x40`, `0x80` or `0x20`; device creation optionally adds `0x100`.
  The call at `0x0052b2d6` omits FPU preservation. Enumeration
  `[0x00529350,0x0052a6f2)` hashes to
  `a8374027bde940888504ac4b3a1a1a701baacc01629cbbce08ab83d560d3a8dc`;
  call setup `[0x0052b296,0x0052b2dd)` hashes to
  `5d1e6f9214f99860e35ec513fb9ba5dbdcb5a9907832448db1c4ff05341c3685`.
  Microsoft documents single-precision/RN initialization when preservation is
  omitted ([D3DCREATE](https://learn.microsoft.com/en-us/windows/win32/direct3d9/d3dcreate)).

Pinned `d3dapp.cpp:337–341` enables preservation only for `_DEBUG`, but that
source drop uses Direct3D 8; it does not establish the retail API or runtime.
Startup precision alone therefore cannot justify 53-bit gameplay arithmetic.
The focused Core tests and a separate native x87 PC24 probe distinguish root
rounding, cancellation, signed zero and retained exponent range. Those isolated
tests do not themselves sample the game or driver. These primitives are not yet
connected to the spatial explosion scan; no whole-simulation precision claim
or replay fingerprint change follows from this correction.

Retail vectors map to Core as `Q(x,y,z) = (x,-z,y)`, apart from the position
datum. Both the materializer and raw Actor pose projection therefore convert
orientation by `Q * B * inverse(Q)`. The unsigned Y/Z swap was incorrect for
pitch and roll. Direct selection and sign-bit changes preserve signed zeros;
Core hashes those words, so even yaw-only definition identities change after
this correction. The quarter-turn offset and renderer checks in
`ThingActorBaseStateTests` and the materializer's `Level100FloatGeometryTests`
test coordinate consistency, not live retail Euler arithmetic or flight parity.

`RetailUnitEuler.Smooth` carries the finite PC24 angle update using retained
retail yaw/pitch/roll words. Nineteen cases match isolated execution of the
unchanged retail routine, including wrap boundaries, signed zero and subnormal
retained steps. `BuildBasis` follows its matrix arithmetic/store order using
managed double trig, matching thirty-one finite native outputs. General x87
transcendental equivalence and cross-host trig identity remain unestablished.
The Level100 mover now retains raw creation/Euler/drive/velocity state and
uses this update after Actor translation. Its living free-flight arithmetic
matched 32 consecutive copied-retail Moves across 352 grouped comparisons,
with observed guide inputs supplied explicitly. Move and clearance/avoidance
cadence use the existing scheduler with explicit PC24 routing arithmetic;
its mode, ordered pool and guide cache are included in schema 47. Aircraft
restore rejects the scheduler's legacy PC53 mode. Earlier formats remain
available for snapshots without this extension. Restoring raw Plane motion requires the matching guide and
event state; a millimeter pose cannot reconstruct them.

Spawned Planes additionally use schema 48 for the monitored spawning owner,
separate raw collision-ignore identity, attachment tag, exit selector/deadline
and explicit handoff to the existing normal-control approximation. Controller
listeners use negative actor IDs; existing positive Actor/Guide listener IDs
stay stable. Exit and pending Ready delivery restore without rerunning Init.
Omitting every exit record is rejected rather than silently selecting schema 47.
Scenes with no spawned Plane retain schema 47 bytes. The recorded 838-step
Headless fingerprint changed only because of the earlier exit-input definition
identity; its test compares every tick's canonical bytes after substituting
only the previous identity and recovers the previous complete trace.

Since September 26 the same level event manager also carries the player
Battle Engine's two self-refreshes, 6002 (crosshair) and 6003 (auto-aim), under
a reserved listener identity, so the scheduler now exists from construction
whether or not an aircraft does. Each construction and delivery takes one
shared draw. Schema 49 adds the Battle Engine's targeting state: both
crosshair readers, the retained crosshair line report (hit class and distance)
and the lock sets. Schema 50 adds the Aquila's six weapon stores, their
overheat flags, the walker's shields-recharging flag, the ammunition-depleted
and overheated cue times, the Pulse's last Fire level and the Battle Engine's
shake offsets and phase; it is selected only once any differs from
construction, so a run that never fires or takes a hit keeps its schema.
Schema 51 adds the jet Missile Pod (its charge, Fire level, mode flag, burst
counter and both launch-slot counters) and, for each seeking round in flight,
its heading, launch time and bound target. It is selected only once the pod
differs from construction or a seeking round flies. The pod's burst
continuation (event 5001) shares the same level event manager under its own
reserved listener. Schema 52 adds every constructed unit's callback state (its
near-camera flag, construction frame and first full Move); the callbacks
themselves (4003, the AI's 3000/3001/3003, fire control's 4001 and the squads'
4000-4002) are ordinary events on that manager. Every world built in the retail
load order selects it. The definition identity's format 9 adds the base world's
pine count, whose draws open the load. A set with the base world also takes the
influence map's two draws and the warm-up units' draws during the load, and its
two influence 1000 chains run on the same manager under their own reserved
listener; they add no snapshot fields. Every round, the Battle Engine's and
the drones', files its MOVE and its life event 4000 there too, under a round
listener (0x1000_0000 + 2 × id, + 1 for a drone's), and moves when its MOVE is
delivered. A round is dying once its life event is no longer filed, so
snapshots and hashes gain no round fields.

Level construction includes the 3.0 s pre-run: 60 whole frames with no input,
as `CGame::PreRun` runs them before the visuals start. Tick 0 is retail frame
60. The event clock and the mission clock count from the load, so both read 60
at tick 0, and a session's tick differs from them by the pre-run (and, after
`SimActions.Reset`, by the reset's tick as well). Code that compares a script
or mission due tick must use that clock, never the session's tick. Replays and
tapes are unchanged: the pre-run is part of constructing the level. Scripts
start on INIT_SCRIPT (2001) and `ready()` (2003) events in that manager, under
script listeners (0x0800_0000 + actor id, 0x0C00_0000 + a carrier's level row),
so a script's `init()` and its draws take their place in the flush's order.

The production script/weapon target bridge, missing avoidance candidate stream,
contact response, complete event/RNG order and effects remain partial. The
two unwritten native clearance-cache coordinates use the observed zero startup
allocation as an explicit deterministic seed. These boundaries preclude an
entire-trajectory or whole-game numerical parity claim.

A copied pristine game running through installed Proton Experimental Wine,
WineD3D and a private Xvfb display reached Level 100 with `-level 100`, without
desktop control. One constructor, twelve cache and twelve Plane Euler samples
read control word `0x007f`; the live constructor/Euler bodies matched pristine
bytes. The native oracle also reproduced eleven consecutive live Euler/basis
transitions. This is a bounded backend observation, not Steam-default, Windows,
cold-start acceptance or a whole-simulation precision claim. See the existing
[Unit function evidence](../reverse-engineering/binary-analysis/functions/CComplexThing.cpp.md).

## What Core may not do

Core simulation truth must be independent of presentation and environment.
Core code does not call:

- presentation, filesystem, clock, process, network, or GPU APIs;
- anything that reads wall time, locale, environment, or thread scheduling.

Clients (the Godot renderer, the headless runner, tests) adapt Core state;
they never own simulation truth. `StateHasher` computes canonical SHA-256
state and trace hashes over ordered, versioned snapshots so a run is
comparable byte-for-byte across hosts.

## The tape and its bounds

The headless runner (`OnslaughtRebuild.Headless`) reads and replays supplied
command tapes. `InteractiveSession` supplies the consumed input and resulting
snapshot to `CommandTapeRecorder`; `BuildObserved` freezes hashes measured during
that session without replaying it. The native Godot host writes a new tape at
exit when `--record-tape=/absolute/path.json` is set. A mostly idle native session
replayed twice on September 6; a substantial player walkthrough remains open in
`PROGRAM.md` P8. See [`README.md`](README.md) for recording commands.

- `MaximumTapeBytes = 8 MiB`
- `MaximumReplaySteps = 100 000`
- A replay runs twice by default (`--repeat` can select another count); each
  additional run must reproduce the first run's hash,
  or the run fails with "Determinism failure: repeated replay produced
  different hashes."
- `--expect <hash>` requires the replay trace hash to equal the expected
  value exactly. It does not suppress an embedded `expectedFinalStateHash`;
  both must match when the tape contains that value.

## What `--expect` means

An expected hash is the frozen fingerprint of one exact scenario at one exact
revision. It is owned by whoever pinned it (usually the test that asserts
it). A hash is a claim about the whole deterministic state machine: inputs,
constants, float/ordering policy, and the hash function itself.

## When a change legitimately moves the trace hash

Fixes that change simulation behavior (a retail constant corrected, a
physics law updated, a float-ordering change) legitimately move every trace
hash downstream. This is expected — but it must be deliberate and recorded:

1. Run the affected scenario(s) and capture the new hash.
2. Re-pin the expected hash in the owning test **in the same commit** as the
   behavior change, with a comment naming the behavior change.
3. Do not re-pin to hide a nondeterminism failure. If the replay produces
   different hashes across identical runs, that is a determinism defect, not
   a pin problem — stop and fix the divergence first.
4. If the change is visual-only or presentation-only, Core trace hashes must
   NOT move. A renderer change that moves Core hashes means the renderer
   leaked into Core state.

## Enforced by

- `Level100ColdStartTests` and the deterministic run fixtures (cold start,
  pointer-quantised, full-chain).
- `InteractiveSessionTests` (100 000-step bounds).
- `HeadlessApplicationTests` (8 MiB tape bound, replay determinism, `--expect`,
  and the pinned first-flight trace/state fingerprint owner).
- `StateHasher` canonical-format tests.

A contributor who touches `Simulation.cs`, `SimulationConstants.cs`,
`SimulationTypes.cs`, or anything in the tick/step path should expect the
cold-start and full-chain suites to be the first (and most sensitive) signal.
