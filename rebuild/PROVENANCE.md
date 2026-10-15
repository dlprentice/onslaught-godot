# Rebuild Provenance

Status: active implementation boundary
Last updated: 2026-09-26 (World 110 on the one construction owner; earlier claims keep their dated evidence).
The 2026-08-31 world-110 admissions and the separate construction stage built
on them were retired on 2026-09-26, when World 110 moved onto the one
construction owner; the standalone ordered `CPlayer::AssignBattleEngine`
mapping remains. The released Battle
Engine finite-cylinder mode-1 round-contact and selected-position boundary,
native-84 completion instrument, native-88 first-Pause mission execution, and current
Thing/Actor base-state, career read/load,
startup/frontend, partial-source inventory, frontend-asset, mouse-sensitivity,
and retained-particle claims retain their narrower dated evidence boundaries.
Summary: the licence boundary, permitted evidence, and authority order for the
`rebuild/` reconstruction lane, plus what the current slice actually covers.

`rebuild/` is a GPL-3.0-or-later, source- and reverse-engineering-informed
reconstruction. It is not a clean-room lane. The root MIT license does not
relicense this subtree or the pinned `references/Onslaught` source.

## Permitted evidence and inputs

- Stuart Gillam's pinned GPL source may be read, ported, and adapted with its
  license and attribution preserved.
- The Steam retail executable and Ghidra database establish released static
  identities and deltas from the reference source.
- Controlled copied-runtime observations establish measured behavior.
- Original design work, deterministic tests, public standards, and engine APIs
  may fill gaps that are clearly labelled provisional.
- Retail assets are user-supplied local inputs. The source tree retains exact
  hashes and bounded extraction/conversion recipes, while the materialized
  payloads remain ignored and outside source/release packages.

Do not import the retail executable, retail asset payloads or conversions,
decompiler output, user saves, raw runtime captures, or separately licensed
third-party code/media into this subtree.
Never describe synthetic or source-only behavior as observed Steam behavior.

## Authority

The reference source is implementation evidence, not automatic proof that the
Steam build is byte- or behavior-identical. When sources disagree, use this
order for the released PC game:

**Read that as a tie-breaker, not a permission gate.** It says what wins *when
sources disagree*; it does not say a port must wait for byte proof. The working
rule is the opposite default:

> **Port Stuart's shape first. Cite the file and line. Override from bytes only
> where a measurement proves divergence.**

Divergences are a tracked exception list, not a per-claim burden. The ones found
so far are few and specific — `InJetMode` 0.3 s in source against 0.5 s in the
shipped bytes, `CPanCamera` length 6.0 in the binary rather than Stuart's value,
a differing weapon resource path. Those are exceptions worth recording precisely
*because* they are exceptions.

Treating every ported line as unproven until re-derived from decompilation is
slower and produces *worse* results, because the decompiler cannot recover
intent, naming or control-flow shape the way the developers' own text can.

**But the drop is PARTIAL, and this is the single most important fact about it.**
A 2026-07-28 re-count found 106 tracked `.h`/`.cpp` source files. Most include
project headers absent from the drop; the exact missing-header count depends on
the system-header filter. No evidence supports a count of missing implementation
files. Do not assume a file exists because the game obviously has that
subsystem. Check first.

Work the partition:

- **Port from source — present in the drop.** Player-vehicle physics and flight
  (`BattleEngine.cpp`, `BattleEngineWalkerPart`, `BattleEngineJetPart`,
  configurations, data manager), the game loop (`game.cpp`, `PCGame`, `PCEngine`,
  `ltshell`, `d3dapp`), frontend page flow (`FrontEnd`, `FEPGoodies`, load/save
  pages, `DXFrontend`), `Career`, `Camera`, `thing.cpp`, `SoundManager`.
- **Read from the retail `data/` folder — authored content, no RE required.**
  `MissionScripts/level***`, `battle engine configurations.dat`,
  `worldheaders.dat`, `default physics.dat`, textures, video, language.
- **Recover from shipped bytes — ABSENT from the drop.** The HUD (`Hud.h`,
  `DXHud.h`, `PCHud.h`), `Cockpit`, `BattleLine`, `MessageBox`, `Unit`, `UnitAi`,
  `Weapon`, the mission-script VM (`MissionScript/vm.h`, `scripteventnb.h`), and
  the core math types `FVector` / `FMatrix`.

That last bullet is why the Ghidra lane is not optional: the subsystems carrying
most of the open render and tutorial defects have no source at all.

> **`FVector::operator^` is now PROVEN, 2026-07-27. This paragraph previously
> barred a class of derivations and no longer does.**
>
> It read: "any conclusion resting on the sign convention of `FVector::operator^`
> is *consistent* rather than *proven* — that operator's definition is not in the
> drop. Derivations that apply the operator an even number of times (such as the
> double cross in the ground-pitch law) are immune and may be relied on; ones that
> apply it once (such as roll) may not."
>
> The operator is at `0x00411a60`, read from the pristine specimen
> (`local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`, sha256
> `74154bfa…` — **not** the installed executable, which is patched). Its whole
> body is nine x87 pairs and three stores:
>
> ```
> fld [ecx]    fmul [eax+4]   fld [ecx+4]  fmul [eax]    fsubp   ; a.x*b.y - a.y*b.x
> fld [ecx+8]  fmul [eax]     fld [eax+8]  fmul [ecx]    fsubp   ; a.z*b.x - a.x*b.z
> fld [ecx+4]  fmul [eax+8]   fld [ecx+8]  fmul [eax+4]  fsubp   ; a.y*b.z - a.z*b.y
> fstp [eax]   fstp [eax+4]   fstp [eax+8]                       ; x, y, z
> ```
>
> Popped in stack order that stores `a.y*b.z − a.z*b.y` to x, `a.z*b.x − a.x*b.z`
> to y, `a.x*b.y − a.y*b.x` to z — **the conventional right-handed cross product,
> a × b, with no sign inversion.** Odd-application derivations, roll included, are
> no longer barred.
>
> **This does not by itself settle the jet ground-effect roll sign.** That was
> blocked by at least four independent things; this removes one. Retail's
> ground-normal horizontal sign, retail's body-right convention in a Z-down frame,
> and Core's own roll convention all remain open and can cancel each other. Task
> #111 is the precedent: only a test caught an inverted pitch law, and no argument
> would have.

1. controlled retail runtime observation;
2. retail binary/static evidence;
3. pinned source implementation and vocabulary;
4. provisional reconstruction design.

Record a source or address only when it makes a current implementation decision
auditable. Generated inventories, human-review gates, and proof-plan chains are
not provenance.

The exact **retail entity → owner → implementation → test** mappings for the
carried behavior contracts live in one place,
[`PARITY.md`](PARITY.md) → *Carried retail contracts*, together with the
specimen anchor each was derived from and the measured mutation that kills its
test. Add a row there when a contract is carried; do not start a second table.

## Current slice

The normal Godot entry path now belongs to a presentation-only frontend state
machine outside Core. With locally materialized media, a plain launch plays the
released Lost Toys logo, opening montage, and splash before click-to-start, then
exposes the released main-menu entries, a quit confirmation, retail's
`CHOOSE GAME NAME` career-name/load page (new-name entry plus caller-injected,
read-only career descriptors), an Options page, Client's career-law selection
state, the bounded Godot level-selector page, the mission-briefing and
select-configuration pages, the released loading image, the
released Level 100 intro cutscene,
and one lifecycle seam that constructs, replaces, or disposes the existing
Level 100 session/world. The `RetailFrontendScreen` enum in
`rebuild/OnslaughtRebuild.Client/RetailFrontendSession.cs` is authoritative for
that list; re-read it rather than quoting this sentence. `--skipfmv`, smoke,
and capture modes suppress the reconstructed video sequences. The materializer
decodes track 0 for the single-track logo/montage and the English Level 100 intro
beside their lossless video frames. Native Linux intro playback produced audio;
the newly added logo/montage streams still need a playback check. The September 6
decode measured these exact source/output identities:

| Source | Source SHA-256 | PCM sample frames, 44.1 kHz stereo 16-bit | Output SHA-256 |
| --- | --- | ---: | --- |
| `data/video/LTLogo.vid` | `2f2819b52b696b2feec5b9bbd60e2d0a1e3f95e48d455ecdc1f3152e06e32b34` | 405,120 | `8051d1a509748e7e0578fcc97c642c82cedaebd638d50f30771a2a71f70d783b` |
| `data/video/OpeningFMV.vid` | `530656d5392ed2e75a75a50e581fa5a0577be71234c0aae0a6af31b24468c7f1` | 3,624,960 | `f6b26ded9f9e3c42a743e1670ec9864df60bea655bc9886f288abb96d130a9bd` |

These match the source track IDs/rate and the existing less-than-one-audio-frame
overhang law. The source was the user's Linux Steam installation; the generated
audio remains under ignored `local-lab/startup-media/`. Decode identity does not
establish native playback timing, volume or mixing parity.

The career reader ports Stuart's raw version-plus-`CCareer` shape
(`Career.cpp:1084-1163`, `Career.h:76-207`) and applies only measured PC deltas:
the version is the released 16-bit `0x4BD1`, the fixed career block is `0x24BC`
bytes, and 16 active option records plus the `0x56` tail make the supported
container 10,004 bytes. `RetailCareerSaveCodec` accepts supplied bytes only,
retains the entire container privately, rejects wrong length/version or a
structurally inconsistent 43-node/86-link graph, and has no serializer. The
reviewed fixture is `tests_shared/fixtures/gold_career_save.bin`, SHA-256
`0c17e47db9d666e9b26ef88d43d0a25e7cbfbf4f88c8005cc748965050e506fb`.
Stuart's `FEPLoadGame.h:32-35` owns separate slot/name identity and
`FEPLoadGame.cpp:128-153` owns its selected-career handoff shape; Client receives
those descriptors already read. Godot recognizes only explicit repeated
`--career-save=<path>` arguments and performs no directory or installed-save
scan. This is read/load selection only: no serializer, write, overwrite,
autosave, default-options apply, debrief persistence, loaded-model Won merge,
or full Career parity.
Core/Client can carry any loaded career's `SuggestedWorldNumber`, and
`SelectWorld` applies the released career unlock law. The current Godot page
does not project that general state: it does not read `SelectedWorldNumber` for
rendering or implement LevelSelect keyboard traversal, its pointer path exposes
only world 100 and unlocked world 110, and the host constructs only world 100.

Core has a direct world-110 mission-program instrument. It accepts only
a definition set stamped world 110 and loads the exact 5,110-byte world-110
`LevelScript` (SHA-256 `f5c157ba…22aa`), executes its one native-88
`SecondaryObjectiveFailed` call, reaching the script's first `Pause`.
Stuart's `game.h:22-24,179-187` owns the
ten-entry, zero-based secondary array and `(num, string_id)` signature; the
pristine `0x00534470` body writes the text dword and `MOS_FAILED=2` to the
distinct secondary base at `0x008A9B2C`. Re-decoding the pinned object in this
pass fixed the call at instruction 22 with slot `1` and text id `114309509`;
instruction 34 is the first `Pause`. The intervening non-waiting
`_110_PROTECT` request uses exact retail `110_protect.ogg` (SHA-256
`03f1fc8e…35d3`, 172,496 samples at 44.1 kHz), which yields 90 ticks under the
already-retained message-duration law. This direct mission probe uses a stamped
Level100 test fixture for native-call receivers; it does not initialize or step
a World110 simulation. The former test-only Simulation route loaded Level100's
Setup and was removed from admission on September 7. Actual World110
construction remains partial and that Simulation request now fails explicitly.
StateHasher schema 43 is checked with synthetic envelopes around direct mission
results; it binds
the non-root world stamp and all ten secondary records; an all-default world-100
mission stays on schema 42 and retains the independently measured 40-step hash
`b8a1c8bc…11216`. No native human-play, full mission, result, client, or visual
parity is claimed.

The same admitted world-110 object contains one authored native-84
`SecondaryObjectiveComplete` at instruction 66, attribute `0x00000254`, fed by
the exact constants at instructions 64 and 65 (slot `1`, text id `114309509`)
and followed by the void-result pop at 67. The ordinary opening above does
**not** reach it: that mission remains suspended at the earlier instruction-34
Pause. `RunWorld110SecondaryObjectiveCompleteInstrument` is therefore an
explicit bounded Core instrument over only authored instructions 64..67. It
turns the already-failed slot into `MOS_COMPLETE=1`, leaves the first-Pause
continuation and adjacent slots unchanged, and produces a deterministic
schema-43 hash distinct from the failed state. The static retail authority is
reused rather than remeasured: `game.h:22-24,179-187`, the 44-byte pristine
`0x00534410..0x0053443b` body (SHA-256
`b39a3c58214a8efc7eff0ca11c1407764983888c3a7d249643376162740cd197`),
the literal-1 store at `0x00534432`, and the existing 42-authored / 7-observed
native-corpus row. Reuse preflight disposition: **REUSED** — those four
authorities plus the merged native-88 state/schema owners; **EXTENDED** — the
existing secondary-state owner, mission VM, and exact world-110 admission
tests; **NEW_MEASUREMENT** — 0. No new inventory, census, output root, pristine
read, runtime capture, or Ghidra mutation was created.

World 110 is constructed by the same owners as Level 100 since September 26:
the materializer writes its static world from the pinned
`data/resources/110_res_PC.aya` (SHA-256 `4e041c75…3c2b`), the Client decodes
it, and `Simulation` builds it in the retail load order (VALIDATION.md, "World
110 construction and start state"). The earlier, separate World 110 stage was
retired then, because it duplicated that construction without ever running in
the product: the definition, seed and player-start admissions, the tree,
Building, SAT and Feature prefix, the player-start list resolution and
assignment sequence, and the `CStart::Init` height prefix. Its retail
evidence stays with the RE lane
([`world-110-initial-constructor-seeds.md`](../reverse-engineering/game-mechanics/world-110-initial-constructor-seeds.md),
[`world-110-player-start-admission.md`](../reverse-engineering/game-mechanics/world-110-player-start-admission.md)),
and its code, tests and mutation receipts stay in Git history and the ignored
`local-lab/rebuild-world110-*` receipts.

The Level-100 configuration page now owns the one row named by the released
`WorldHeaders.dat`: page-list index 0 selects `Aquila Prototype`, catalog record
3, with authored Walker keys `Pulse Cannon Pod` / `Mech Twin Vulcan Cannon` and
Jet keys `Mech Vulcan Cannon` / `Missile Pod`. The owner deliberately keeps
those data keys separate from the shorter strings visible in the pristine
frame. This is the released one-row projection only; generic configuration
loading, localization lookup, property ratings, pips/icons, live preview, input
timing/sound, and pixel parity remain open.

Steam's
`-skipfmv` flag at
`CLIParams__ParseCommandLine` (`0x00423BC0`) skips that movie but still reaches
the click page. That page's Steam handlers at `0x0051B660`/`0x0051B6B0` accept
action `0x2C` and full-window mouse input; its render entry at `0x0051B840`
requests localized string index `0x77` (`Click to start`). Main-menu evidence is
the vtable at `0x005DBAE4`, input/action/render entries
`0x00462250`/`0x004623E0`/`0x00462D40`, and Stuart's `FrontEnd.cpp` and
`PCFrontend.cpp`. Level select uses Steam input/render entries
`0x004606B0`/`0x00460B40`. The reconstruction's Core/Client state applies the
career unlock law and can carry any loaded `SuggestedWorldNumber`; its current
Godot page neither renders that general selection nor traverses it by keyboard.
The pointer path exposes only world 100 and unlocked world 110, and the host
constructs only world 100. The loading page
uses the exact image and `Loading...` text established by
`CConsole__RenderLoadingScreen` (`0x0042C810`). The Options page is retail's
own rather than a new surface: read from the pristine specimen
(`local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`, SHA-256
`74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`,
2,506,752 bytes) during this pass, the vtable at `0x005DB8A8` holds eight
entries inside one `.text` cluster (`0x0051F4B0`–`0x0051F7E0`), and within that
cluster at `0x0051F8A6` the image executes `6a 01 8b c8 e8 b1 e5 fa ff` —
`push 1; mov ecx,eax; call 0x004CDE60`. That branch lazily creates the frontend
Options context with mode `1`; it is not taken on every entry. A replayable
manual trace observed the same non-null frontend context reused across two
entries. Static branch evidence shows that state takes the reuse path, and both
observed transitions called `CPauseMenu__InitPauseSession`, which resets the
session each time. The frontend and in-game menus therefore share
initializer/widget behavior but are distinct runtime instances. **Those bytes
and positive calls establish the branch/session shape;
the names `CFEPOptions`, `PauseMenu__Init`, and the FEP page id `0x11` come from
the reviewed Ghidra/source mapping rather than from this byte sequence alone.**
**Thirty-two** exact AYA textures —
`rebuild/tools/materialize_retail_assets.py`'s `FRONTEND_ASSETS` is
authoritative for that count and should be re-read rather than quoted from
here — three exact XAP PCM decodes, and ten English strings decoded from the
supported shipped table are materialized to ignored frontend paths. This lane
emits move/select/back cue identities; it does not load or play those WAVs, so
the integrating audio owner remains singular.

> **Superseded 2026-07-28 — one page list and one count in the paragraph
> above.**
>
> 1. **The page list was incomplete.** It read: "It begins at click-to-start,
>    then exposes the released main-menu entries, a world-100-only level
>    selector, the released loading image, and one lifecycle seam that
>    constructs, replaces, or disposes the existing Level 100 session/world."
>    Five further screens — quit confirmation, DevSelect, Options, mission
>    briefing and select configuration — were already declared and shipping.
>    Nothing was withdrawn; the list was simply short, and
>    [`../CURRENT_CAPABILITIES.md`](../CURRENT_CAPABILITIES.md) already listed
>    briefing and configuration select on the run path, so the two governance
>    documents disagreed with each other.
>
> 2. **The exact frontend-asset count is no longer duplicated here.**
>    `materialize_retail_assets.py::FRONTEND_ASSETS` is the count owner and the
>    frontend asset README enumerates the materialized files and hashes. This
>    removes a repeatedly stale second copy of the number. **Unchanged:** the
>    three XAP PCM decodes and the bounded English projections remain exact.

Result ownership remains split. The in-game terminal overlay is owned by the
HUD during the countdown. After `FrontEndHandoffReady`, Client applies the
pinned Career update and owns a presentation-safe projection of Steam
`CFEPDebriefing::Render`/`TransitionNotification`; Godot composes the settled
mission-status, objective-summary, and win-only grade page and routes
acknowledgement to Level Select. It does not read `mThingsKilled` or invent a
Goodie list. The deterministic mission remains the owner of gameplay outcome
and failure reason.

This is not a claim of full post-Won parity: the outro, live score/time join,
transition/effect/message/glint phases, save persistence, broad campaign
construction, and runtime pixel parity remain open.

The deterministic Core and command-tape/hash format are reconstruction-owned
infrastructure. The Godot Level 100 Opening Slice consumes the released
Federation walker, jet, and cockpit as exact 63-, 54-, and 21-part AYA
hierarchies, plus bounded static conversions of 24 Level 100 static-world mesh
types, four `pinesnow` variants, Target Tank, and Warehouse meshes. Static Steam
evidence separates three tree owners. `CRTTree` submits each exact pine mesh at
or inside the selected profile's horizontal mesh-quality distance and queues
its six-view imposter outside that boundary. **That distance is retail's
authored default `30.0`, corrected from `70.0` on 2026-07-27 under GOAL.md's
defaults rule.** The pristine specimen
(`local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`, sha256
`74154bfa…`) dispatches "Geometry detail" at `0x004DD6B0` to three arms writing
`10.0` / `30.0` / `70.0` to `0x006321A0`, and the image's own static
initialisers are uniquely the middle arm, so the released out-of-box state is
Medium. **File offset is `VA − 0x400000` uniformly** — the three section deltas
(`.text` `0x401000`/`0x1000`, `.rdata` `0x5D8000`/`0x1D8000`, `.data`
`0x622000`/`0x222000`) are all `0x400000`:

| global | VA | file | bytes | value |
| --- | --- | --- | --- | --- |
| mesh-quality distance | `0x006321A0` | `0x2321A0` | `00 00 f0 41` | `30.0` |
| LOD bias | `0x00631E88` | `0x231E88` | `00 00 80 3f` | `1.0` |
| quality scale | `0x00630E0C` | `0x230E0C` | `00 00 80 3f` | `1.0` |

> **Corrected 2026-07-27.** This table first shipped with the file offsets
> `0x231CA0`, `0x231988` and `0x230F0C`. All three were wrong — `0x231CA0`
> holds the ASCII `"ing "` — and they were not even a consistent alternative
> convention (two implied a `0x400500` delta, one `0x3FFF00`). The **values**
> were right; only the offsets were wrong. Recorded rather than silently
> replaced because a reader who checks `0x231CA0`, finds a string fragment and
> concludes the finding was fabricated is the exact failure this file exists to
> prevent.

Two independent corroborations, neither needed for the result but both
narrowing it. The getter pair at `0x004DD770`/`0x004DD786` classifies the live
value against `.rdata` `[0x005D85D4] = 15.0` and `[0x005D8610] = 40.0`
(`<15 → 0`, `15..40 → 1`, `>40 → 2`), so `30.0` sits **mid-band** of index 1
rather than merely equalling arm 1's immediate. And the only route to those
arms is an options page: `0x004DD6B0` has one caller, the thunk `0x004CEF50`,
which is slot 14 of the vtable at `0x005DE478` whose RTTI type descriptor
`0x006313A0` names **`CTreeDetail`**. No startup path reaches it.

`0x006321A0` has exactly **five** writers and ten readers. The fifth,
`0x004DD832`, writes `45.0` but is **dead in the shipped image**: its gate
`0x00662F10` has two `.text` references and both are reads, and it lies past
`.data`'s raw size so it is BSS and zero at load.

The previous `70.0` came from this machine's `defaultoptions.bea` at OptionsTail
`+0x0C` (file `0x26CA`), which is persisted run state. The decisive proof is in
code, not in file comparison: the only writer of that file
(`0x0051F595 → Serialise → OptionsTail_Write 0x00420B10`) reads the **live
globals** — `mov ecx,[0x6321a0]` at `0x00420B9E` — so the file is by
construction a snapshot of current state, and no path writes it from authored
constants. `INSTALL.LOG` lists `cardid.txt` but no `.bea` at all, so the absence
is meaningful rather than a category gap. A value read from that file is a
**user setting**, not a default. The static-world manifest (schema
`onslaught.level100-static-world.v14`) owns the corrected value.

> **Do not cite `proof_defaultoptions.bea` as a pristine specimen.** It is also
> run state — it holds `12.0` at OptionsTail `+0x04` where the image initialiser
> `0x006254F4` is `7.0`, so it is simply a run whose geometry detail happened to
> sit at Medium. It corroborates that the field is user state; it is not
> independent evidence of the authored value.
After the world and global-imposter
passes, `CDXTrees` submits one standing fast card selected by
`(tree_object_address >> 4) & 3` and a fifth-view horizontal card only when the
camera differs from sampled ground height by more than 20 units. A manifest
ordinal is not the retail selection input. The client preserves the close,
six-face far, always-on standing, and height-gated horizontal owners separately.
It does not infer Steam identity from its own heap. That manifest instead pins
an explicit phase-0 ordinal cycle for deterministic reconstruction and validates
all 1,481 selected views with counts `371/370/370/370`. Steam's exact tree
allocation/view sequence and address-selector phase remain the precise
unresolved runtime boundary.

> **Corrected 2026-07-28 — a dead version label, twice.** Both sentences above
> named "**Manifest v7**"; there is no manifest v7. It existed briefly and was
> superseded seven revisions ago. **Both claims attached to that label are
> unchanged and still true** — the manifest owns the corrected `30.0`, and it
> pins `fastStandingViewPhase` `0` — so this is a stale citation, not a false
> claim. MEASURED: the manifest declares
> `onslaught.level100-static-world.v14`, and three tracked consumers reject
> anything else — `OnslaughtRebuild.Client/Level100ActorDefinitionManifest.cs`,
> `OnslaughtRebuild.Godot/Level100StaticWorldAsset.cs`, and
> `tools/materialize_retail_assets.py` in two places. The text now names the
> schema string rather than a bare version number, so the next bump is
> greppable. The same dead label was corrected in
> `OnslaughtRebuild.Godot/Assets/Level100/README.md` in the same pass.

Exact source/output hashes live in the materializer and ignored generated manifest;
detailed card, atlas, and render-state evidence lives in the Level 100 asset note and
`reverse-engineering/binary-analysis/functions/DXTrees.cpp.md`.
The released Level 100 WRES
records now set the player start heading, all 33 visible base-world objects,
1,481 Steam-instantiated pines, trigger locations, and four Firing Range targets. The client
maps BEA `(X, Y, Z-down)` consistently to Godot `(X, -Z, -Y)` for terrain,
retained meshes, facilities, sky, light, camera, and Core-relative positions.
The supplied base-turret comparison resolves specifically to WRES type `8`
(`CUnitInitThing`), object `Turret 03`, definition `SAT Turret`, physics Unit
index `58`, mesh `ft_sam`, and released runtime class `CCannon`; it is not a
Target Tank. The authored transform is `(252.5, 261.25, -0.0)` with zero
yaw/pitch/roll. Stuart's `CThing::Init` clips the authored pivot through
`MAP.Collide` and then the water level, while Steam `CThing__Init` at
`0x004F34A0` dispatches the `CCannon` clip slot (`+0xB0`, true), samples HFLD
at `0x0047EB80`, then dispatches its underwater slot (`+0xC4`, false). HFLD
unit `-10485` gives terrain Z `-9.599889755249023`, above water in the released
Z-down relationship, so the initial retail transform is
`(252.5, 261.25, -9.599889755249023)` with identity orientation. The 16-part
mesh hierarchy is `base -> turretbase -> support -> barrel -> Emit01..08`, with
`Emit09..12` directly below `base`; its lower bound is
`-0.22822660952806473` relative to the pivot. The client now consumes the
manifest's existing definition and omits only the `SAT Turret` lower-bound lift,
preserving its authored below-pivot skirt without a per-instance offset. The
remaining static types keep their prior converted clearances and are not
claimed to share this released grounding relationship.
The loose mission scripts establish their order and 0.5-second event delays.
The retained `HFLD` uses the released loader's 64×64 tiled sample layout,
height scale, and complete 513×513 sample lattice. Core embeds the hash-verified
chunk and implements Steam's `0x0047EB80` 24.8 fixed-point signed interpolation
for hashed player-ground elevation. The released `MAPT`/`MMAP`, lighting-gradient,
30-owner `SSHD`, and base `DMKR` paths produce the initial 512×512 root landscape
texture. The materializer processes all 1,481 pine placements through the
released stamp rules and verifies the exact
RGB565 payload as SHA-256
`6EB202F450926097930BEDCA440F0163A1886572981E3C69B4EDF9289A68AE2B`.
The archive serializes seven `MAPT` sources; the single-player landscape calls
`CDXLandscape__CreateMipLevels` (`0x005447E0`) with five and selects mixer widths
`16/32/64/128/256`. Those sources, all variable-length `MMAP` records, the
lighting mask, sparse structure-shadow cells, and pine shadow descriptors are
retained in the 1,382,734-byte hierarchy payload with SHA-256
`541EACD0AA75FAE8BEFB8A3E1505EA52AE6B1F6C1367C15C65D7DD23B7CFE977`.
Level 100
selects exact 512×512 DXT1 `detail00`; the released terrain render path at
`0x00545590` supplies its two world-coordinate scales, offset, exact 256×256
DXT1 moving cloud-shadow stage, scroll rates, and observed modulation modes.
The macro compositor follows the released row-major tile, texel, weight, and
shade-mask addressing. `CHeightField__InitColorGradient` (`0x0047E8E0`) builds
the 64-entry coefficients; the load tail at `0x0047F932` doubles, clamps, and
masks them before `CLandscapeTexture__BlitTileRegionWithLightingMask` produces
RGB565 texels. Steam's 20-byte terrain vertices contain position plus repeated
landscape coordinates, but no normal or diffuse-color channel, so that prelit
macro owns base terrain illumination. An uninterrupted copied-runtime sample
measured the cloud offset advancing by `(0.01993, 0.00996)` cycles per wall-clock
second. Stage 0 wraps and plainly modulates the root texture, stage 1 plainly
modulates detail, and the cloud and rotated-detail stages use `MODULATE2X`.
Steam uses anisotropic root minification, but its five logical landscape levels
are separate one-level 512×512 cyclic caches rather than one hardware mip chain.
Their absolute-coordinate spans are `512/256/128/64/32`; cache ownership follows
the selected landscape tile rather than normalized mesh UVs.
The client preserves each retained mesh group's complete
six-slot `TEXR` assignment and directly decodes every AYA-wrapped texture
selected by its active passes. The PC
lighting setup at `CEngine::SetupLights` supplies packed ambient plus opposing
sun and anti-sun directions; its directional colors divide by 256, and the
base texture stage uses `MODULATE2X`. Five exact DXT1 cube-25
textures use the released face order and geometry. Steam runtime state confirms
Level 100's terrain capability flag and `MODULATE2X` state are both enabled, as
well as packed fog color `#D8D8FC`, density `0.0084`, and `D3DFOG_EXP` mode;
the shared Godot material applies that exponential path from camera-space depth
to terrain, static geometry, cockpit, targets, and water. Godot's
`OUTPUT_IS_SRGB` contract selects the final transfer so the GL Compatibility
renderer is not converted twice. Steam
`CMeshRenderer__RenderMeshWithLayerPasses` (`0x0054D530`) evaluates the slots in
order; `CDXMeshVB__Load` (`0x0054E160`) treats only `0xFFFFFFFF` as absent.
Controlled Level 100 runtime state enabled modes `0`, `1`, `2`, and `4` while
disabling modes `3` and `5`. `CVBufTexture__RenderModePass` establishes mode 1
as model-space `DOTPRODUCT3`, mode 2 as camera-space reflection coordinates with
the released half-scale/offset matrix, and mode 4 as the regular-UV alpha overlay
using the serialized `TEXB` offset and scale. For the Level 100 Tank Factory,
all four material groups own `Chrome3` in slot 2 at serialized strength
`0.19999998807907104`. Mode 2 is a separate draw which retains the active lit
stage-0 `MODULATE2X`; stage 1 multiplies its otherwise-opaque source alpha by
the byte-quantized `0x33FFFFFF` texture factor before
`SRCALPHA`/`INVSRCALPHA` framebuffer blending. It inherits the world's wrapping,
linear-mip, anisotropic stage-0 sampler and `-1` LOD bias. The client preserves
that encoded-channel, saturating equation rather than blending raw Chrome3 RGB.
Its special base path tests `CTexture +0xB4`, disables alpha test/blending, and uses
`BLENDTEXTUREALPHA`; parsing all 273 Level 100 `DXTX/CTEX` records identifies only
`meshtex\\A8_FB_hangermorebits_lit.tga` for that path. The client therefore blends
that texture over the lit current color while retaining normal alpha cutouts for
the other currently materialized base textures. The
runtime light vector `(-0.03407396, -0.9086333, 0.4162026)` matches normalized
negative Level 100 sun position. These active passes are shared by the current
static objects, targets, Aquila, jet, and cockpit; the visible-sun particle is
still absent. Water follows the released active fixed-function path: the exact
HFLD level and color, camera-following 25×25 grid, two caustic stages, authored
`reflection00` imagery sampled at the released absolute-world `1/256` transform,
the stage-3 disable before the main grid, shoreline-only wave passes, and two
exact `SURF` shoreline bands. Static
Steam evidence at `CWaterRenderSystem__RenderMainPass` (`0x0055B6C0`) establishes
the first-shoreline, grid, alpha-tested sun, and late additive-shoreline order.
`CDXSurf__Render` requests projection depth-bias index `4` for its shoreline
draw and then restores index `0`; `CDXEngine__SetProjectionDepthBiasIndex`
at `0x005514A0` subtracts `index * ZBIAS_SCALER` from projection slot 14, while
the shipped `cardid.txt` value is `0.00014`. The client therefore applies
the index-4 delta `0.00056` in reversed-Z clip space instead of translating
the shoreline `0.002` world units. The separate index-6 water caller and
pixel-level sign validation remain open.
The sun uses texture-factor color `#E8E8FF`, alpha reference `0xC0`, and a quad
whose center, half-width, and half-length are respectively `6`, `2`, and `8`
times camera height. The late shore pass uses `SRCALPHA`/`ONE`, no depth write,
and no fog. One uninterrupted copied-Steam sample measured the main phase at
`1` radian per second and both wave scrolls at `0.06` texture cycles per second;
the client advances those presentation phases from frame delta outside Core.
The animated half-scale reflection transform belongs to the optional advanced
path, which remained inactive in controlled Steam observation. Static analysis
of `CDXLandscape__UpdateLOD` (`0x00546B40`) and
`CLandscapeIB__CreateIndexBuffer` (`0x0048DF20`) establishes the complete
eight-step base, the 4/2/1-step grids and 16 edge-stitch index variants,
midpoint-error LOD score, camera-smoothed texture rings, and absolute cache
coordinates. It does not establish the exact stateful gamut-row
clipping or the exhaustion/reuse order of Steam's bounded `800/300/90` patch
pools; the client emits eight-step coverage for unselected tiles plus the
selected patches and leaves triangle clipping to Godot's renderer. Dynamic scene
reflection/refraction and pixel identity outside this bounded active pass are not
claimed.
Steep-slope sliding, structure collision
beyond the two observed facilities, targets, weapons, resources, jet/morph
handling, reverse-transform presentation, and unimplemented HUD behavior remain provisional unless
specific retained evidence says otherwise.

The exact walker AYA supplies 63 reciprocal parent/reference parts, 54 expanded
base-material surfaces, and the 100 usable frames in `LegMotion`. Steam
`CMCMech` does not replay those frames as one gait cycle: it precomputes each
leg's root-to-`Footbase` extension and chooses the closest frame independently
for the current planted-foot distance. The retained chains are legs
`18/21/22/23/24`, `28/30/31/32/33`, `46/51/52/53/54`, and
`3/8/9/10/11`, with Footbase parts `25`, `34`, `55`, and `12`.
Steam `CMeshPart__InterpolateSegmentTransform` at `0x004B0D00` linearly blends
all nine stored `HORI` matrix components and the position; the Godot loader now
preserves that componentwise law instead of quaternion-slerping fractional
poses. The current Level 100 walker, jet, and cockpit callers supply integral
frames, so the separate `0x004B24D0` adjuster/round/wrap path remains explicitly
unmodeled rather than being guessed into this API.

`c_D3DXQuaternionSlerp` at `0x00577EAA` (pristine `74154bfa…`; linked D3DX
library code that the RE lane matched byte for byte to `d3dx9.lib` on
2026-09-26, formerly labelled `Math__InterpolateVec4ByRatio`) separately
establishes a shortest-sign, sine-weighted spherical interpolation law for unit
quaternions.
The presentation-only proper-rotation path now uses that law, retaining its
near-parallel normalized-linear and non-orthonormal componentwise fallbacks.
The static body does not yet prove that this exact retail helper owned every
actor-render interpolation caller, so that wider call-path identity remains open.

One fresh no-input control and two uninterrupted copied-retail repetitions used
the same three-second idle, twelve-second forward hold, and fifteen-second rest
over the authored Level 100 slope. Both active runs repeated the exact start
`(288.6875, 243.25, -12.111499)` and end
`(270.926941, 275.010376, -12.886998)`, then settled all four phase/lift fields
to zero. Steam `CMCMech` establishes the consumed controller contract: body-local
foot offsets `(-0.957,1.078)`, `(0.937,1.089)`, `(-0.882,-1.527)`, and
`(0.937,-1.505)`; diagonal scheduling with at most two early swings; phase rate
`400` per second through `180`; `0.4` lift; and moving/stationary thresholds
`1.0`/`0.05`. Every planted foot repeated the exact HFLD height while the final
contacts spanned about `0.96` vertical units. Core consumes that fixed-step
controller subset and Godot directs each exact five-part chain toward its Core
contact. Retail keeps the Battle Engine body level at its 1.9-unit clearance;
exact toe-normal alignment, CMC sway, non-heightfield surfaces, and steep-slope
response remain outside this proof.

One clean Level 100 control and two fresh repeated copies establish the walker
translation and body-turn loop: equal forward/strafe acceleration, a 3.0-unit/s
cap, `0.7` per-retail-update coast, yaw-velocity accumulation, and `0.8`
retention. Core runs at that same 20 Hz, so those responses transfer without a
rate conversion. The shipped `Aquila Prototype` configuration record independently
stores `mGroundTurnRate = 1.0` (`0x3F800000`) at record offset `0x2F6` in
`battle engine configurations.dat`, SHA-256
`58722b12a04cae97ad2163acb2cc2c1699f95a0688318bd8a86696714d94454a`.
Joined to pinned `BattleEngineWalkerPart.cpp:347-350`, that makes full walker
yaw input `1/75` radian, or `13,333` integer micro-radians per 20 Hz update. Core
therefore no longer uses the superseded fitted `1.7/75` gain that made turning
1.7 times too fast. The same control/repeat discipline maps raw states
`2 → 1 → 3` to a walker-to-jet transition, which is now the shipped
`BATTLE_ENGINE_TRANSFORM_TIME 0.5 f` × `GAME_FR 20` = 10 ticks rather than the
16 the 30 Hz Core fitted to a measured 535–537 ms window. Jet forward speed and energy drain retain
earlier bounded measurements.

Pinned `BattleEngine.cpp:2949-2995` and pristine PC retail
`CBattleEngine::DeclareOnGround` at `0x0040C750-0x0040C983` establish the bounded
terrain-touchdown algorithm now consumed by Core. The retail body is the primary
vtable's slot 68, has a normalized-identical PC-demo twin, and carries the same
branches and literal bytes: `0.2` at `0x005D8604`, `0.4` at `0x005D8C40`, `16.0`
at `0x005D8BC0`, and `0.90` at `0x005D8BB0`. The resulting contract is a strict
speed threshold of `0.4` released units/update in walker state and `0.2`
otherwise; a nonzero walker dash counter returns before self-damage and the
specialized velocity response; damaging contact applies
`speed * 16 * cos(surface)^2` life through the four-argument virtual Damage call
with null source and false shield flag, then retains the complete velocity by
`1-cos(surface)^2`; and a non-damaging jet contact retains `0.90` while a
non-damaging walker or morph contact does not alter velocity in this override.
The static identity/shape receipt is
[`../reverse-engineering/binary-analysis/cbattleengine-vtable-semantics-2026-08-11.md`](../reverse-engineering/binary-analysis/cbattleengine-vtable-semantics-2026-08-11.md),
whose machine-readable table has SHA-256
`1bf959a26bc390b8b6d3dfb44eef543b64d2d93839dfc5efe2356314fc429e4e`.
Millimetres-to-released-units and released-life-to-milli-life cancel, as in the
existing water-skim mapping. Core uses its retained HFLD gradient for the normal
and deterministically quantizes the squared incidence to one part per million.
This bounded algorithm is therefore **SOURCE + RETAIL-STATIC**, not source-only.
Runtime path frequency, observed hull/effect outcomes, and parity of Core's
integer normal/incidence quantization remain open. Object-supported contact, the
dying branch, and the actor's separate generic post-declaration vertical bounce
are outside this bounded port.

A later clean control and two fresh copies with only the proven Level 100
early-flight byte change isolated the corresponding presentation. Transform was
bound through copied `defaultoptions.bea`; launches used only `-skipfmv -level
100`. The clean control delivered the same action but remained in walker state
`2` with no render, animation, cockpit, or camera change. Both modified runs
swapped the active render reader as state `1` began, committed state `3` after
540.045 and 549.598 ms, and retained one first-person camera pointer/vtable.
Steam `CBattleEngine::Morph` (`0x0040A580`) and the render-reader swap
(`0x00406460`) establish that the 54-part jet hierarchy owns the external
transition. `CBattleEngine::FinishedPlayingCurrentAnimation` (`0x0040EEB0`)
then changes its `walktofly` animation, virtual frames 25–50 at 20 Hz, to
looping `fly` frame 0 after 1.243 and 1.241 seconds. The 21-part cockpit begins
its independent `walktofly` path one step into its 26–50 table, displays frames
27–49, and selects `fly` frame 0 after 1.138 and 1.141 seconds. The same entry
starts exact XAP records 25 (`N_BE_engine_takeoff`) and 23
(`N_BE_engine_inflight`). Runtime copies, sampler output, and debugger helpers
were disposable; only the consumed hashes and timings are retained.

One no-input control and two uninterrupted fixed-yaw forward holds per facility
then establish the only retained structure contacts. The Control Tower repeated
a `2.5736`-unit centre separation while removing inward velocity and retaining
tangent motion; the Tank Factory settled at `8.4333` units and removed the
head-on velocity. Both held raw walker state `2`, the expected `0.15`-unit
released update speed before contact, and stable body yaw. Stuart's
`ECR_SLIDE` response and the released single-player `0.4` BattleEngine radius
support the interpretation. Core consumes rounded `2.574` and `8.434` contact
envelopes only; these are not general mesh bounds, arbitrary actor collision,
or facility-destruction behavior.

A fresh no-input Level 100 control and two repeated pointer, forward, coast, and
strafe runs also traversed a `6.232587`-radian
`CBattleEngineWalkerPart::UpdateWalkCycle` scalar. Static `CMCMech` analysis now
establishes that this separate scalar does not index every leg through
`LegMotion`; the retired renderer incorrectly conflated the two. The sampled
ground normal remained `(0, 0, -0.99999976)` on that earlier flat route.

A pair of fresh, uninterrupted, no-input app-owned Level 100 runs repeated the
same released opening-camera lifecycle. At event time `3.0`, Steam installed a
`CPanCamera` (`0x004198D0`, vtable `0x005D92A8`) with length `6.0`, not Stuart's
in-house `3.0` default. Both runs began at
`(283.807220, 251.978271, -16.411499)` and ended at
`(290.115509, 240.701736, -12.195276)` around the stationary Battle Engine at
`(288.6875, 243.25, -12.111499)`. Steam's `CPlayer__GotoPanView` at `0x004D2C10`
uses the released orientation with local points `(0,10,-4.3)`, `(5,0,1.3)`,
`(0,-9,-1.3)`, and `(0,-2.5,0)` through its order-three clamped quadratic
`CBSpline`. The camera changed to the first-person `CThingCamera` at event time
`8.95`; game state remained panning until `9.0`. `CPlayer__ReceiveButtonAction`
at `0x004D3110` rejects normal player actions below playing state. That establishes
the six-second camera-state boundary: 180 ticks in the historical 30 Hz Core
sample coordinates, or 120 ticks in the current 20 Hz Core. It does not itself enable Level 100 input;
the later mission power gate is documented below. `CPanCamera::GetShowHUD` is
false; the control camera owns the HUD-visible handoff. Raw sampler output and
copied games were disposable and are not retained.

A clean copied Level 100 run starts player zero with current/preferred view `1`.
After that opening fly-in, five uninterrupted samples held the same active camera
pointer and first-person `CThingCamera` vtable `0x005DBB88`. The Battle Engine
position remained `(288.6875, 243.25, -12.111499)`, yaw remained `0.509829998`,
and the horizontal forward column remained `(-0.488029, 0.872827)`. The camera
position is the Battle Engine position, and the Steam 16:9/zoom-1 projection term
`0.5625` gives a 58.7155-degree vertical field of view. The cockpit pointer at
Battle Engine offset `0x528` selected animation index `1`; the exact `cockpit2.msh`
`CAMD` table identifies that as `walk`, authored hierarchy frame 25. Runtime
also reported the cockpit render flag enabled and no local position offset.
The retained 21-part cockpit loads its exact hierarchy as ten material-group
surfaces at that frame. Godot's camera child uses a bounded 6 cm depth and 1 cm
vertical presentation adjustment selected against the clean retail frame and
near-plane path; that adapter offset is not claimed as a retail model value.

The attached-view zoom chain is now bounded across controller/player routing,
Battle Engine initialization/move/zoom/morph, and the projection consumer at
`0x0042E4D0`, `0x004D3110`, `0x00404DD0`, `0x004081C0`,
`0x00409E80`–`0x00409EC0`, `0x0040A580`, and `0x00550B10`. Retail initializes
current and desired zoom to `1.0`, normal-weapon Zoom In selects `0.4`, Zoom Out
selects `1.0`, and each 20 Hz move approaches the target by `0.1`; morphing
forces the desired value back to `1.0`. Level 100's two walker weapons carry
the normal zoom mode while its two jet weapons do not. Core retains that law as
`1000/400/100` fixed-point state, scales look input by current zoom, and the
Godot frustum consumes the interpolated value. Mouse Wheel Down/Up route to the
two shipped actions. Charge zoom mode `2`, exact float32 intermediary bits,
weapon-cycle/augment zoom changes, multiplayer aspect handling, and pixel-scored
runtime projection remain open.

A no-input control and two uninterrupted repetitions then bound attached-view
aim at the same authored start. `Look Up`, `Look Down`, and `Look Left` were
bound only through each copied `defaultoptions.bea`; launch used
`-skipfmv -level 100`. Raw state remained walker `2`, view remained `1`, and
position remained `(288.6875, 243.25, -12.111499)` during the aim phases. The
first vertical input changed pitch by `0.008547009` radians (`1/117`) and left
stored velocity `0.0068376074`; subsequent coast retained exactly `0.8`. Both
runs stabilized at pitch `+0.5321228`; their opposite endpoints were
`-1.0911411` and `-1.0912496`. This establishes absolute bounds on the Level
100 start slope, not the released terrain-normal rule elsewhere.

For a bounded shot witness, a disposable one-byte copied-archive setup changed
only the initial `Pulse Cannon Pod` call descriptor from `DisableWeapon` to
`EnableWeapon`. Two fresh runs produced player-owned `CRound` objects whose
unit directions were `(-0.226261, 0.404663, -0.886032)` and
`(-0.226194, 0.404543, -0.886105)` in Steam X/Y/Z axes. Their contemporaneous
BattleEngine yaw/pitch values predict
`(-sin(yaw)cos(pitch), cos(yaw)cos(pitch), sin(pitch))` with maximum component
error `0.00119`. Core consumes the shipped pitch input `1/117` rad and
retention `0.8` verbatim (they were the 30 Hz time-equivalents `0.003938` and
`0.861774` before the 20 Hz migration), the bounded start-slope endpoints, and that three-axis
shot direction. Terrain-relative limits, mouse inversion, auto-aim, and vertical
target collision remain unimplemented. The setup patch,
copies, and raw samples were disposable.

`ProjectileBurst__SpawnFromCurrentPreset` at `0x005069F0` closes the bounded
per-projectile scatter law shared by actor and player weapons: it consumes two
samples from the global gameplay stream, applies the second to yaw and the first
to pitch, and scales both by the weapon mode's `CWeaponInaccuracy`. The exact
Level 100 modes carry `0.008726646` rad for the charged Pulse Cannon and
`0.006981317` rad for both player Vulcans. Core now routes all three player modes
through the already-canonical actor-weapon stream and leaves the measured
cockpit emitter unrotated while scattering the round direction. The focused
test pins draw count, order, snapshot seed state, and emitted directions. This
does not claim that Core has the same absolute stream phase as every retail
shot: the released stream is global and other shipped consumers remain absent.

The same stream now supports the separately bounded `CUnitAI` close-target
selector. PC retail/demo, two Xbox builds, and three PS2 builds share the exact
candidate gate/range shape, deterministic category ladder, and indiscriminate
score conversion `(Random() % 65536) / 8192`. `CUnitAttackPriority` owns the
seven profile cells for emplacement, vehicle, building, naval, infantry, air,
and component; `CUnitIndiscriminate` owns the normalized switch at config
`+0x128`. Candidate virtual slot 89 is source-backed as `IsAThreat()`, while
shipped property `CUnitIgnoreThreats` owns normalized config `+0x138`. When both
are false/zero, retail substitutes primary zero without rejecting the candidate
and bypasses the deterministic component floor. The random arm consumes one draw
only after a candidate passes the earlier gates and strict range test; all
measured builds skip the `IsAThreat()` call, `CUnitIgnoreThreats` read, category
ladder, and component floor on that arm. `RetailUnitAITargetSelection`
now computes the first two ordered gates directly: resolved candidates must
have `TF_DYING` clear and Unit field `+0x244` outside `{1,2}`, then must satisfy
the exact Forseti/Muspell/Independent opposing-pair table; Neutral is accepted
only when `CUnitIndiscriminate` is nonzero. Stuart's source binds the flag and
allegiance enum and supplies the spelling `IsTargetAlligence`; it does not name
Unit field `+0x244`, which is distinct from the source-crosswalked `EAIState`
cell at `+0x210`. The reducer keeps the third ordered capability result as a
caller-captured oracle because retail evaluates a candidate virtual, a linked
child/spawner-like list, an ordered active-weapon list, category masks, terrain
height, and the selected weapon profile. The new upstream transcript adapter
closes the immediately preceding world-list transform. Retail's `CWorld+0x00`
`GetThingNB` set is head-inserted; its `+0x20` allegiance-`0/6` and `+0x30`
allegiance-`1/6` indexes are independently tail-appended. Owner allegiance `1`
selects the `0/6` view, owner `0` selects the `1/6` view, and every other value
falls back to all things. Each raw payload with squad discriminator
`0x20000000` resolves through virtual slot `+0x128`; otherwise unit bit `0x10`
keeps the payload itself. Downstream fields come from that resolved unit, but
the squared range vector and `1000-distance` term come from the raw payload.
Core accepts all three already ordered views rather than incorrectly deriving
the side indexes by filtering all things. It returns the winning transcript
index and does not claim the retail global draw phase, world-index population
or mutation, monitor execution, helper side effects, or autonomous actor wiring.

The enclosing UnitAI slot-4 body independently contributes a second pure Core
boundary. PC retail `[0x004FF4F0,0x004FF70B)` reads the source-bound
`CWorld::GetSquadNB()` set, walks its physical `CSquad*` nodes in retained
order, skips the owner's current squad, and resolves a first representative for
the side/capability gates. Squad virtual results supply position and percentage
for the strict squared-range test. Every passing squad is resolved a second
time and dispatched to `0x004FDAD0`; the loop does not select, deduplicate, or
stop, and the caller invokes the helper even with a null second result. It reads
the live node successor only after that helper returns, allowing a newly spawned
and tail-appended squad to enter the same scan. `RetailUnitAISquadSupportProbe`
therefore reproduces one finite interaction step rather than freezing the list:
its caller executes the helper before acquiring the successor. It does not
claim mutable `CWorld` ownership, virtual-call purity, the helper's ordered
spawner lifecycle, or actual spawn products.

The following reader/result transaction is now a third bounded Core owner.
`CUnitAI`'s `+0x0C` cell uses the exact source-matching SetReader order:
same-target no-op, unlink old, store new, register new. `+0x10` is a runtime
caller-supplied retained-target gate whose only proved nonzero producer writes
literal `1` after hierarchy propagation; `+0x14` is construction-fixed fast-
reuse eligibility, initialized to `1` and disabled by five PC construction
paths with no accepted later writer. Slot 4 refreshes a retained gated target
without rebinding. Slot 11 otherwise attempts current-target reuse, or pre-
clears results and commits the selector winner before two ordered support
updates and conditional B/A evaluation. Post-commit failure does not roll back
the reader. PC's C3-only stealth-zero test also admits unordered/NaN, while
Xbox/PS2 require ordered zero. `RetailUnitAITargetTransaction` emits this exact
ordered adapter plan. Concrete pointer ownership, virtual helper execution, and
autonomous actor scheduling remain outside Core.

The preceding direct/current-target arm is a distinct phased owner. PC retail
`[0x004FF4F7,0x004FF57E)` first clears a dying `+0x0C` reader, then admits the
arm only for a nonnull owner `+0x148` receiver whose raw virtual slot-83 result
is zero. The recognized lifecycle binds that cell to `CNormalSquad`, but the
virtual's authored name is not recovered. Admission writes `+0x18=0` before
`+0x1C=0`; null stops, state rejection clears the reader, and success reloads
the canonical reader separately for support, B, and conditional A(context 0).
PC demo, both measured Xbox images, and PS2 demo/Europe/USA preserve the same
order. `RetailUnitAIDirectTargetArm` emits those stage-local observations and
actions without merging them into the later selector/fallback transaction.

The enclosing shared UnitAI slot 3 is now a fifth deterministic boundary. PC
retail `CUnitAI__Update` `[0x004FEF40,0x004FF322)` is 994 bytes / 300
instructions (SHA-256 `7aca029c…fffe`); demo `[0x004FEFF0,0x004FF3D2)` has
the same 300-instruction normalized stream `d0cd8f0e…8b34`. It always invokes
slot 4, then chooses a four-draw jittered-aim arm returning zero, a one-draw
direct-aim arm returning `0.5+low16/65536`, a no-draw fire arm returning mounted
pitch plus exact float32 `0.1`, or one-draw idle arms returning
`1.5+low16/65536` / `3+low16*(2/65536)`. The tracked contract records the
ordered target virtuals, fresh reader loads, cleanup, and owner writes without
inventing field names. Its caller is also PC retail/demo shape-identical and
reuses the incoming event as event 3000 after one final float32 store of
manager time plus the x87 delay. `RetailUnitAIUpdateTransaction` carries those
actions, draws, final jitter fields, delay, caller-zero override, and exact due
bits. Concrete matrix/aim execution, helper effects, monitors, and actor/event
wiring remain outside Core; no runtime or console equivalence is claimed.

Deletion-aware reader feedback is now its own deterministic owner. Pristine PC
`CGenericActiveReader__SetReader` `[0x00401000,0x00401034)` (52 bytes, SHA-256
`5540848cb8c7cd9fd46fc6a2d068b76527166c61510dd33c36b2c4dc1e41dca2`)
and `CMonitor__dtor_base` `[0x004BAC40,0x004BACA7)` (103 bytes, SHA-256
`3f174f5a2ca14159ac4a5141ed32b7f292d79f9d0efe899eaeb9c3f1c4087adf`; the
RE lane's second label cohort renamed it from `CMonitor__Shutdown` on
2026-09-26) establish same-target no-op, detach/publish/attach rebind order,
newest-first reverse membership, direct reader-cell zeroing, and
clear-after-walk teardown.
The 159-byte PC UnitAI destructor independently orders outbound detachments as
`+0x28`, `+0x24`, `+0x0C` before invalidating inbound readers. PC demo closes
the same lifecycle, Xbox closes target-death behavior, and all three PS2
setters close rebind order under the narrower limits recorded in
[`CMonitor.cpp.md`](../reverse-engineering/binary-analysis/functions/CMonitor.cpp.md).
`RetailActiveReaderGraph` carries that ordinary protocol with stable cell
identities and safe unique membership. Target shutdown nulls the registered
UnitAI `+0x0C` cell only; `+0x10/+0x18/+0x1C` remain unchanged until a later AI
path writes them. Runtime storage, allocation, and object deletion remain
adapter work.

The same graph now supports one separately bounded player contract. Pristine
PC `CPlayer::AssignBattleEngine` `[0x004d3080,0x004d30c5)` is 69 bytes / 26
instructions, SHA-256
`17f1f2e24aa271c93f1a223b7ad871f34487e91d4e1e640c37056cb112593a10`.
It calls `SetReader` first on player `+0x1c` with the engine, then on engine
`+0x574` with the player. It tests the complete dword at player `+0x20`; any
nonzero value dispatches primary-vtable `+0xe0` with raw zero, then `+0x154`
with raw one. Pinned `Player.cpp:254-266` and `BattleEngine.h:229,234` supply
the source-compatible `SetPlayer`, `SetVulnerable(FALSE)`, and
`SetInfinateEnergy(TRUE)` names. The concrete vtable resolves the latter two
calls to static writes at engine `+0x15c`, `+0x160`, and `+0xfc`, but the new
Core owner deliberately emits only policy-call intents until a cohesive Battle
Engine state/configuration owner exists.

`RetailPlayerBattleEngineAssignment` performs both graph operations in that
order and snapshots each function-level call with an immutable nested action
list. Same-target calls remain visible. Rebinding P from A to B leaves A's
player reader aimed at P; displacing P2 from B leaves P2's engine reader aimed
at B. Both are retained rather than repaired. Both distinct cells are
preflighted before graph mutation. Null graph, duplicate reader-cell roles,
and missing required cells are rejected before the first graph write under
deterministic single-threaded Core use. The graph cannot validate that the
engine-side cell belongs to the supplied engine; that remains an adapter
invariant. This is not a retail atomicity claim: null pointers, configuration
faults, allocator failure, rollback, lifetime, and concurrent mutation remain
excluded.

Reuse disposition for this seam: **REUSED** — the exact
`CGenericActiveReader::SetReader` law, its accepted deterministic graph owner,
pinned GPL source, and the pristine specimen; **EXTENDED** — one standalone
Core function owner and focused tests; **NEW_MEASUREMENT** — the complete
69-byte body identity/order and concrete God-policy vtable targets/effects.
There was no runtime capture, Ghidra mutation, retail payload in Git, engine
construction, session integration, or campaign-grade promotion.

The controlled reciprocal-call mutation receipt is ignored repo-local machine
evidence at
`local-lab/rebuild-player-assign-battle-engine-mutation-kill-20260830/RECEIPT.md`,
SHA-256
`63b97ad75ddb73a39c2f8a92a48c8471548c5c2fd93c1837e0788780aa9ca401`.
Omitting the second graph call while retaining its transcript label failed the
exact fresh-bind fact. Byte restoration returned the production owner to
`f8df01f2ad11d72061359487f625f0aeb68a6e34e291c0c59fb78c7e300cebce`,
the same fact to 1/1, and the adjacent assignment/reader/start gate to 54/54.

The spawner reached by that helper is now a separately bounded Core
transaction. Pristine PC retail `CSpawnerThng__DoSpawn`
`[0x004E3C60,0x004E3F8B)` (811 bytes, SHA-256
`a21eb3fd7aad249edaca00b3dad1f9b42af222e5cb7b16251118130eb12316cc`),
its member wave `[0x004E3F90,0x004E43BB)` (1,067 bytes, SHA-256
`b2783b8993c6fae83fc3989f3e2d581d29568d23013f9485d3e206b46885f4d7`),
the 39-byte completion predicate, and the 20-byte event callback establish the
ordered start/retry/completion law. Pinned `InitThing.h` supplies the spawner and
zero-member squad-init field names/defaults. PC demo, two independently rehashed
Xbox mapped bodies, and normalized PS2 demo/EU/USA closures corroborate the
same released transaction. `RetailSpawnerCycleTransaction` preserves strict
admission, empty tail-publication before count/busy commit, null-squad amount
consumption, immediate first wave, member retry without rollback, optional
attachment, and final reader/busy/time order. It does not own factories,
virtual init, world-list or reader mutation, transforms/clearance, attachment,
or event delivery; those remain explicit adapter effects. Full evidence and
open runtime reach are recorded in
[`spawner-squad-cycle.md`](../reverse-engineering/game-mechanics/spawner-squad-cycle.md).

The transcript's former `SupportMinimum`/`SupportMaximum` names are corrected
to selected attack-provider range results. Exact PC bodies show that
`0x004FB840` mutates a `CUnit`'s selected weapon/spawner for one target, then
`0x004FB780/0x004FB7E0` query the chosen provider's minimum/maximum range with
weapon-over-spawner priority and positive-zero fallback. PC demo, Xbox, and PS2
correspondents preserve that transaction. Core still accepts those two results
as caller-captured cells; reproducing provider selection itself requires actor
lists, active-reader state, terrain sampling, time, and full weapon ballistics.

The copied Steam options bind Movement Forward/Backward/Left/Right to both
`WASD` and the matching arrow keys, while Look Left/Right/Up/Down consume the
mouse axes. Steam `CController::DoMappings` at `0x0042DB40` maps each centered
cursor displacement as `clamp(sensitivity * pixels * 0.004333333, -1, 1)`;
`Input::UpdateCursorCenterWithWindowScale` at `0x0042DA00` retains `10/17` of
that displacement per 20 Hz update. Stuart's player and BattleEngine paths
corroborate that the resulting analogue axes add walker yaw at
`GroundTurnRate/75`, pitch at `1/117`, and then retain angular velocity by
`0.8`. One no-input control and two fresh uninterrupted copies configured only
through `defaultoptions.bea` repeated the same sensitivity-`1.5` pointer and
movement sequence without focus loss; both active runs produced the same
sampled yaw delta `-0.019985914`, pitch delta `-0.021745417`, and checkpoint
states. The Godot adapter consumes that bounded proportional mapping at its
20 Hz fixed step. Other sensitivity values, inversion, and jet mouse response
remain unproven.

> **Amended 2026-07-28 — the measurement stands; `1.5` is not a released
> default or UI-selectable value.**
> The paragraph above is a record of what was actually run and is not withdrawn:
> those copies really were configured at sensitivity `1.5`, and the sampled yaw
> and pitch deltas are what they produced. What must not be carried away from it
> is that `1.5` is a retail baseline. It was a copied-options test setting
> consumed by the retail executable. The slider law is
> `g_MouseSensitivity = (index + 1) * 3.0f` with max
> index `0x14`, so the selectable set is `{3, 6, … 63}` and `1.5` sits below the
> floor; it is the value in the copied `defaultoptions.bea` those runs were
> configured through, which is persisted run state under `GOAL.md`'s defaults
> rule and not an authored default. Read
> during this pass from the pristine specimen
> (`local-lab/safe-copy-bea-pristine/BEA.exe.original.backup`, SHA-256
> `74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750`): file
> `0x2254F4` (VA `0x006254F4`) = `00 00 e0 40` = `7.0f`, file `0x1D8CC0`
> (VA `0x005D8CC0`) = `3.0f`, file `0x1D97C8` (VA `0x005D97C8`) =
> `0.004333333f`. This document already knew the first of those — see the
> `proof_defaultoptions.bea` warning above, which cites `0x006254F4` as `7.0` —
> and simply never joined the two halves. The mapping measured here is
> proportional, so it transfers; the shipped adapter now runs at the image
> default `7.0` (`fed5829b`). `../CURRENT_CAPABILITIES.md` carries the same
> withdrawal. **Unchanged, and deliberately so:** "Other sensitivity values,
> inversion, and jet mouse response remain unproven" is still exactly true. A
> sensitivity slider now exists in the client, but no run has been *measured* at
> any value other than the one above, and "unproven" is a statement about
> evidence, not about implementation.

Separately, a disposable expected-byte-only change to the player constructor's
preferred-view immediate
from `1` to `2` selected the released third-person vtable at `0x005D9230`; the
copy was restored and no retail patch is retained. The third-person constructor
at `0x00418EF0`, position path at `0x004191C0`, and orientation path at
`0x00419540` establish the previously inspected pitch-zero third-person
geometry: camera five units behind and 3.25 units above the 1.9-unit center of
gravity, looking six units ahead. Retained mesh bounds, the released
Level 100 ground/start relationship, and copied-runtime framing independently
agree on scale `1.0`; the client grounds the walker from its composed standing
pose. Static placement retains measured lower-bound metadata, but only the
`SAT Turret` currently has the type-specific released pivot-grounding correction
described above.

Fifty-five HUD textures and `Dial.raw` are **retained by the asset materializer**
(`rebuild/tools/materialize_retail_assets.py`, which is authoritative for this
count) and are exact released files named
by the Steam binary or the pinned Stuart weapon resource path. **This is the
retention count, not the composition count**: the client loads and composes
**twenty-nine** of them, which is the figure `CURRENT_CAPABILITIES.md` and
`rebuild/README.md` quote. The two numbers count different sets and do not
conflict.
*(Updated 2026-07-27: was "Fifty-four retained HUD textures"; `cdf8979d` added
`Hud/forseti-icon.texture.aya`. The manifest now carries 56 unique `Hud/`
entries, one of which is `Hud/dial.raw`.)* A clean
copied-runtime frame,
the complete Level 100 mission script, and the released render paths establish
the first-person composition now used by Godot: the central threat compass and
target layers, classified lower-left scanner/weapon stack, lower-right Level
100 influence map or message portrait, objective/world markers, and the
conditional segmented message panel.
`CHud__RenderObjectiveProgressGaugeAndHeadingNeedle` at
`0x004858D0`, `CHud__RenderBattleline` at `0x00487D10`,
`CMessageBox__RenderOverlay` at `0x004B8850`, and the `CDXCompass` render path
provide the retained edge offsets, 45/46-unit scanner north/contact radii, and
111.5/96/110/98-unit threat, damage, gauge-needle, and objective radii plus
rotations, packed tints, and state ownership.
`CDXCompass__BuildByteSpriteOverlayTexture` identifies `Dial.raw` frame zero as
the heading-rotated north treatment. `CDXCompass__BuildRingGeometry` supplies
the 50/40 segment counts and 31/27-percent thickness inputs. Level 100's version-1
BSWD supplies 13 translated radius-10 nodes and 22 exact links.
`CDXBattleLine__BuildMesh` establishes that the released interior is a
continuously triangulated terrain-extent mesh with inserted influence points
and relaxed edges, not a drawing of the BSWD links. Its dynamic influence
magnitudes and render mesh are not available from the current mission producer,
so Godot retains the typed state consumer but draws no inferred interior.

`CMessageBox__RenderOverlay` supplies the native 120-pixel bar pieces,
bottom-centre anchors, and five 15-pixel line offsets. `CDXFont__CreateFromTexture`
scans alpha above `0x10` to derive proportional glyph widths. The client uses
those released Font13PS metrics to wrap and paginate within the 232×76 text
rectangle and clips every glyph and shadow draw to that rectangle; it does not
use a fixed character-count estimate. Exact 128×128 DXT2 `oo`/`ee`/`mm`/`aa`
frames supply the four Tatiana, technician, and Kramer poses. The released
CircleMask is opaque at the square corners and transparent at its portrait
aperture, so the client first applies the released 0.75 portrait scale, then
multiplies every portrait's alpha by inverse mask alpha before normal alpha
composition. This is the retained mask operation that prevents the opaque black
source square from being rendered.

`CMessageBox__RenderBattleLinePulseSprites` supplies portrait ordering and
8/12/40/40 selection weights. Static evidence does not expose Steam's
process-global RNG seed/initial phase, and this owner does not establish phoneme
analysis. The HUD accepts read-only active-message/playback state from the
integrated audio owner. Page advancement follows actual playback position, and
the deterministic weighted portrait sample remains a presentation
reconstruction rather than a claim of Steam's exact RNG phase. A deterministic
ignored manifest is derived from exact
`LevelScript.msl`, Level 100 `English.txt`, global `text.stf`, and `english.dat`;
Godot verifies its hash and uses native signed ID/text/audio identities while
validating the ordered `PlayCharMessage` speaker/highlight identities. The
presentation projection drains the actual mission events and preserves their
speaker, message, highlight, and help order without feeding HUD timing back
into Core or using a C# fallback message catalog.
The Level 100 script and its
51 exact English audio references use
only Tatiana, the technician, and Kramer; there is no Level 100 video command or
Bink portrait asset.

The canonical mission snapshot supplies enabled weapon gates and HUD emphasis;
its ordered events supply message and help delivery. The canonical actor
registry supplies active objective identities and full three-dimensional poses.
The Godot projection preserves emitted collection order and retains typed
actor IDs plus full three-dimensional objective positions until the renderer's
final horizontal projection. Core's current Walker/Jet selection now feeds the
retained Pulse/Vulcan HUD icon identities; Missile Pod remains absent because
its exact HUD icon is not independently identified. Successful actor-round
damage now preserves the released source-relative yaw, 15-entry cap, two-second
lifetime, and strict one-expired-entry-per-update list law. The Godot compass
projects only positive-intensity entries and draws the exact retained 128x32
sprite at the single-player 96-pixel radius, fading opaque grayscale RGB under
the ONE/ONE pass as retail does. External damage facts and water skim carry no
source object and deliberately create no directional flash. It leaves
selection-panel state, weapon resources, threats, target prediction, and
active-help lifetime absent until their mechanics owners exist; the current
node-influence reconstruction remains explicitly provisional. The HUD does not
draw a parallel frontend result screen. The bounded Won handoff, Career
update/world-110 unlock, settled frontend debriefing page, and
acknowledgement-to-LevelSelect edge now exist.
During Core's existing terminal countdown only, the in-level HUD now reproduces
the retail black darkener, Victory/Defeat title, and the three retained Level
100 loss-reason strings. The outro, live score/time join, dynamic debriefing
phases and effects, save persistence, broad campaign construction, and runtime
pixel parity remain open.
This is an ownership boundary, not a claim that every released HUD value or
render pass is complete.
Steam's exact dynamically written 16-bit ring pixels and exact portrait RNG
initial phase remain unproven.

One clean control and two fresh, uninterrupted app-owned Level 100 runs then
repeated the first eight message boundaries within one 50 ms retail sample.
In the historical 30 Hz Core sample coordinates, with tick zero aligned to
Steam's game-time-`3.0` pan start, their intervals
are HUD introduction `182..351`, threat circle `357..567`, scanner `573..756`,
message log `762..926`, technician `932..998`, movement `1004..1220`, Target
Zone 1 instruction `1226..1387`, and objective-scanner instruction `1393..1530`.
The Battle Engine power flag at offset `0x580` changed `0 → 1` at tick `1000`;
the released flight flag at `0x58C` and both initial weapon gates remained off.
At tick `1223`, the object uniquely identified at Target Zone 1's authored
position changed its `CThing` flags at offset `0x2C` from `0x0002` to `0x0022`,
setting objective bit `0x20`. Exact English strings decoded from `english.dat`
and the eight opening Ogg/Vorbis files drive the client.

Two fresh uninterrupted player-input runs then repeated the first objective
handoff. Target Zone 1's radius-5 volume remained active outside centre
distances `5.44` and `5.54`, then overlapped the released Battle Engine's
single-player `0.4` radius by distances `5.29` and `5.39`. Eleven 20 Hz updates
later, both runs changed Target Zone 1 flags `0x22 → 0x02`, Firing Range flags
`0x02 → 0x22`, and the active message to ID `4458134` in the same update. Steam
Battle Engine vtable slot 16 at `0x0040DF80` independently returns `0.4` outside
multiplayer. `CHud__RenderTacticalRadarContacts` at `0x00484C50` supplies the
objective path's yaw rotation, 46-unit clamp, and fixed `0xFFFFFF00` tint used
with the exact 16×16 DXT2 `CompassObjectiveMarker`. The ninth retained voice is
exact `tutorial_02.ogg`, 237871 samples at 44.1 kHz.

One clean control and three fresh uninterrupted runs then followed a
predeclared observer from the Firing Range objective through the first weapon
exercise. Steam's exact objective-list head at module RVA `0x455140` avoided a
broad heap scan. Every accepted run cleared the range objective, deactivated the
player, advanced through message IDs for `TUTORIAL_03`, `HUD_05`,
`TUTORIAL_PULSE_CANNON`, `TUTORIAL_OPEN_FIRE`, and
`TUTORIAL_PULSE_CANNON_2`, and added the same four `CThing` pointers as
objectives at Open Fire. One second later the player power gate changed `0 → 1`
and only the Pulse Cannon's active gate changed `0 → 1`. The copied `Fire`
binding changed Steam's live current-weapon state, proving delivery to player
one independently of the mission messages.

The four pointers repeated bit-identical positions, yaws, and vtable identities
for three Target Tanks and one Warehouse; their exact values and retained asset
hashes live in the Level 100 asset README. The five new Ogg files supply exact
voice lengths. Core consumes the released script's explicit pauses and the
already demonstrated message post-roll/handoff, not variable wall-clock memory
scan latency. The exact overlap-to-event endpoint was not separately sampled,
so `FiringRange.msl`'s 0.5-second dispatch remains source-derived. This proves
the first Pulse Cannon exercise's gates, objectives, ordering, text, and audio,
but does not by itself prove completion, non-objective contacts, or pixel parity.

A no-fire control and fresh isolated copied-runtime runs then followed each of
the three Target Tank pointers, player-owned round list, and objective set. Four
releases at the first active charge bucket (`10`) created normal rounds with
definition speed `35` and exact movement magnitude `1.75` per 20 Hz update.
Generation 21 separately binds the shared retail slot-66 body at `0x004D8E40`
to observed strict-`CRound` dispatch. Retained Level 522 and Level 741 traces
contain 7,513 call-entry pairs through vtable `0x005DE82C`; the receiver is
continuous in every pair, 7,204 returns are gap-free, and 309 raw returns are
honestly orphaned across trace barriers. No `CMissile`-style vtable
`0x005E3BA4` call was observed. This corroborates the placement and call
envelope used by `AdvanceActorRounds` / `SteerSeekingRound`, while leaving
receiver writes, branch ordering, complete contact/lifetime/effect behavior,
the shared `CMissile` placement, and source spelling open. The focused
`ForsetiMissile_HomesOnAMovingTarget` test passes, but the mapping remains
`PARTIAL_CONTRACT`; no reconstruction behavior changed for this admission.
Generation 22 separately binds the shared retail slot-0 body at `0x004D9910`
to an observed strict-`CRound` event-routing envelope. Retained Level 521 and
independent Level 512 recordings contain 2,555 call-entry-arm paths through
dispatcher `0x0044B68A` and vtable `0x005DE82C`, with receiver/event-pointer
continuity and exactly one selected arm per invocation. Event 4002 and the
shared `CMissile` placement were not observed; arm writes, callees, ordering,
transitive effects, source spelling, and complete subclass behavior remain
open. The nearest reconstruction owner is `AdvanceActorRounds`, which has no
explicit retail event queue or direct event-routing parity test. The focused
`ActorArmament_IsCanonicalReplayState` nearest-owner test passes, but the
mapping remains `PARTIAL_CONTRACT`; this admission changes no reconstruction
behavior.

The inbound Level 100 actor-round contact now uses the released player shape
rather than the former target-origin sphere shortcut. Pristine
`CBattleEngine::SetCollisionShape` `[0x004063B0,0x00406459)` constructs a
`CCylinder` with single-player radius `0.4` and half-height `0.95`; default
`SetShape` `[0x00426370,0x004263E6)` translates its centre, and
`CThing::GetCentrePos` places that centre at owner retail Z `+0.76`. Retail
Z-down maps it to Core Y `-760` mm. The closed round-line dispatch reaches
`[0x00440510,0x00440AB2)`, whose world-vertical radial and cap comparisons
retain equality and apply no owner orientation. Core clips the
integer-millimetre segment against the inclusive Y slab and evaluates the X/Z
radial minimum as an exact rational comparison with radius 400 mm and
half-height 950 mm. An independent review rejected the initial `double` draft
on a real exact tangent (`160000.00000000003` after rounding); the exact
prefilter accepts that tangent and rejects its one-millimetre-outside parallel
line in either endpoint order.

The same pristine mode-1 body proves that overlap is not the complete response.
It constructs two independently clamped parameters equivalent to
`(|P0|-radius)/|V|` and `(|P0|+radius)/|V|` from full three-dimensional norms,
rejects only when both resulting candidates lie strictly beyond the same cap,
and selects candidate zero before downstream callbacks. This can reject a
mathematical finite-cylinder hit and can choose a point other than geometric
first contact. Core reproduces the ordering, same-cap guard, and selected
integer-millimetre impact position with deterministic Q32 square roots. Q32 is
an explicit approximation of the released binary32/x87 spills; it is not a
sub-millimetre identity claim. This mapping remains player-Battle-Engine-
specific; arbitrary actor shapes, impact normals, simultaneous-candidate
behavior, and stock occurrence of zero-displacement rounds remain open. All three
measured Xbox builds preserve the ordinary finite policy and movement-before-
callback ordering, but diverge on a generated zero-length line: Xbox retains
NaN roots and writes NaN XYZ, while PC clamps to zero and selects finite `P0`.
Core follows the pristine PC behavior; natural-gameplay reachability of that
Xbox degenerate case is now structurally closed but stock-play occurrence and
ordinary finite rounding-boundary equivalence remain open. The released Round
producer runs after old-position capture and velocity integration, immediately
before dispatch: it forms old-to-current, extends by authored `RoundLength`
only when that value is strictly above `0.001f` and displacement is nonzero,
and emits a zero-length line when old equals current. Canonical physics data
contains 61 line-backed Rounds (51 length zero, ten length `0.5f`), and every
Round has positive authored speed. PS2 German demo, Europe retail, and USA
retail independently preserve the
ordinary finite Boolean, minus-root-only rewrite, second-root cap guard, and
movement/velocity-before-callback ordering; their resolver emits no report
contact or normal. PS2 binary32/VU threshold equivalence to PC x87 is not
claimed. Xbox additionally selects a 1,000-mm
radius for strict level range 850--899 or serialized world type 1/2; Level 100
has exact type 0, so carrying that currently unused multiplayer state would not
change this owner and is deferred until a multiplayer world is admitted.

Each tank began at life `6` with no shield. Retained Level 100 observations
recorded aggregate losses `6 → 4.2 → 2.4 → 0.6 → -1.2`; each target set its
destroyed bit and left the objective set on shot four. One separate glancing
mesh-part hit removed `1.0`. Pristine `CRound::Hit` (`0x004D8AE0`) sends the
configured direct `0.8` first, then creates an already-live small explosion.
That explosion is not statically bound to the direct receiver: its initializer
synchronously scans neighboring MapWho candidates, and `CExplosion::Hit`
(`0x0044BF10`) applies radial falloff whose configured `1.0` is only the
maximum. Existing generic traces prove six direct-then-explosion same-receiver
`CUnit` pairs and part `-1`. Static Target Tank construction independently
closes the same part: its segment-controller field remains null from factory
through init.

The immediate explosion does not use MapWho's public radius query. Its
radius-`0.5f` owner is inserted at level 4, then the collision detector walks
3-by-3 X/Y sector neighborhoods from level 4 down through 0, applying mutual
component masks, outer bounds, readiness/ignore/invisibility/dying gates,
level negotiation, and selected-shape contact before calling explosion `Hit`
first and candidate `Hit` second. It has no generic creator, active, life, or
dead exclusion, and continues after an accepted receiver. A newly terminal
Target Tank remains MapWho/collision registered under `TF_DYING` until its
delayed `+0.5f` shutdown and can take a positive part-`-1` explosion store
without dispatching death twice.

The contact catalog preserves raw global BBOX origin/radius, mesh render-radius
and class scale words. The
[serialized geometry evidence](../reverse-engineering/binary-analysis/cround-hit-damage-path-2026-08-10.md#serialized-geometry-and-terminal-target-tank-behavior)
corrects the former alternate-loader estimates: they were static recomputations,
not measured normal-live radii. The serialized loader copies the original
words, and GroundUnit separately applies stored `0.8f` to its primary sphere.
Instance centres and pose-cache rounding still need their own implementation;
quantized part bounds cannot recover discarded input precision.

`RetailMeshPartPose` implements original RE-informed arithmetic for the
single-frame normal-cache hierarchy and owner composition, with distinct
operation orders and explicit float32 stores under the declared 53-bit/RN
contract. Tests use retained Warehouse hierarchy words and labeled synthetic
rounding cases. It does not yet execute collision cache selection, controller
changes or the explosion scan, and does not claim live FPU equivalence. The
round/contact evidence linked above owns its specimen and instruction pins.

Core currently retains two full whole-body stores per Pulse, preserving the
observed aggregate and intermediate `0.8` store but approximating the second
transaction as an unconditional `1.0` on the direct receiver. Tests pin that
approximation; they are not spatial-explosion parity proof. With raw input
geometry now carried by the existing materializer, the next boundary is a
distinct synchronous explosion at the resolved round position minus
`normalize(velocity)*0.1` can then apply primary-volume admission and
per-candidate falloff. The
[Warehouse report evidence](../reverse-engineering/binary-analysis/cround-hit-damage-path-2026-08-10.md#warehouse-segmented-explosion-report)
now establishes up to six ordered part contacts, each using its own signed
bounds distance and falloff. Its damage override forwards each part to the
segment controller; part `-1` does not apply aggregate segment damage.
Natural Level 100 candidate order, actual Warehouse contact rows and cache/controller
execution remain unresolved. The current one-part aggregate shortcut remains an
explicit implementation gap.
Generation 20 adds a narrower retained-trace check on the retail carrier: ten
internal slot-40 calls across three independent TTD sessions cover both
`CExplosion::Hit` damage arms and carry source equal to the explosion object,
`applyShields=1`, and mesh part `-1` in every observed call. Six same-receiver
`CUnit` pairs use direct parts `8/0/1/0/0/8` but explosion part `-1`, refuting
direct-part reuse for those pairs. No observed receiver is the Warehouse or a
controller-bearing segmented target, and the function entry, return, owned
writes, nonnegative-part path, and universal behavior remain open. The campaign
therefore keeps this reconstruction mapping `PARTIAL_CONTRACT`; it does not
broaden the Warehouse implementation or claim engine-wide explosion parity.
`CUnit__ApplyDamage` (`0x004F9A90`) receives the mesh-part index and
`CUnit__MarkDestroyedAndCleanupLinks` (`0x004FD140`) owns the removal, so Core
does not generalize the unmeasured part multiplier. It consumes only the
three independently demonstrated direct-hit paths, the retained mesh bound,
and the released per-update speed, which is now Core's per-tick speed.
The pristine specimen also fixes the whole-body terminal threshold exactly.
At VA `0x004F9E61` / file offset `0x000F9E61`, the 32-byte range through
`0x004F9E80` is SHA-256
`F6A5A6D8AAB47FFDDD06A16B493FBEDACD0583519277AF2F9E035FF314B087A2`:
it loads life, subtracts damage, stores the remainder, compares the retained
x87 value with the `+0.0f` constant at VA `0x005D856C` (file offset
`0x001D856C`, bytes `00 00 00 00`, SHA-256
`DF3F619804A92FDB4057192DC43DD748EA778ADC52BC498CE80524C014B81119`),
then tests x87 C0 and skips the destruction lane when C0 is clear. Finite
positive and exact-zero remainders therefore survive; finite negative values
enter the lane. A later `[ESI+0x2C] & 4` gate can still bypass cleanup, so this
proves the comparison threshold rather than asserting that every negative
`CUnit` is immediately terminal. The current Level 100 authored life and
damage values are finite; retail's unordered/NaN C0 behavior remains outside
this reconstruction contract.
The speed-`35` physics record names `Mech Pulse Bolt Medium`; its released
five-entry particle descriptor references four unique texture archives: Blue
Spark 2, Blue Trail, Halo, and Energy Trail. Those exact archives and their
authored base dimensions supply the bounded projectile presentation. Exact
`data/ParticleSets/MainSet.par` (SHA-256
`A51FE4419B55E1AF132E31C6B3CD8133C937745D8F4AB691EB5A0D81017DED06`)
supplies the retained small-impact and medium tank-destruction primary sprite
layers, atlas ranges, scales, and lifetimes.

The three shipped `data/ParticleSets/*.par` files are now materialized runtime
inputs and `OnslaughtRebuild.Client.ParticleSetFile` decodes them. The decoder
is falsifiable and falsified: it re-emits all three byte-identically, and the
corpus census it reports (1,479 descriptors over twelve record types) is
recomputed from the files at test time rather than copied into a constant. Only
the in-level sun consumes a resolved effect today - `Sun Sprite`, per
`references/Onslaught/DXEngine.cpp:220`. Exact
`data/sounds/sounds_english_pc.xap` (SHA-256
`658C15E3BAB844D65DD3C07C4AC880F16F741C0EA116F48C603449BBD4DDA8B7`)
records 35, 106, and 102 supply the retained 44.1 kHz mono fire, small-impact,
and medium-explosion PCM respectively. The record names, decoded lengths,
high-nibble-first IMA-ADPCM output, and retained hashes were independently
validated. A same-return CDB capture at released
`CBattleEngine__GetLaunchPosition` (`0x0040C990`) then resolved cockpit emitter
`Gun`, weapon index `1`, to `-0.005619` right, `+0.080066` forward, and
`+0.259300` up in the live BattleEngine basis. Core consumes the corresponding
rounded millimetre offset; the debugger stop supplied only that static return
value, never timing. Descriptor color ranges, mode-1 tank-smoke blend,
secondary emitters, debris, and wreck geometry remain absent.

The complete-Level-100 audio retention extends that same decode contract
without extending current mission simulation. The accepted canonical Level 100
message table has 51 character-message identifiers; only those exact English
Ogg files are retained. Version-103 `sounds.sfx` resolves the exact PCM records
used by the bounded adapter and supplies their effect volume, pitch variance,
loop, and language fields. Stuart's `CSoundManager::PlayEffect`, `PauseAllSamples`,
`UnPauseAllSamples`, and `KillAllSamples` establish selection/randomization and
lifecycle architecture. Canonical Steam bodies at `0x00404DD0`
`CBattleEngine__Init`, `0x004081C0` `CBattleEngine__Move`, `0x00468770`
`CFrontEnd__PlaySound`, `0x0046FAE0`/`0x0046FB00` game unpause/pause, and
`0x004E1B20` `CSoundManager__UpdateStatus` independently retain the released
effect identities and pause boundary. The adapter consumes ordered numeric
`Level100MessageRequested`, `AquilaFlightEvent`, and
`Level100DestructionEvent` streams from `FrameAdvanceResult`. Its Aquila
emitter binds to the canonical `Player 1` Battle Engine ActorId and updates from
that actor's full three-dimensional registry pose; impact and destruction
samples use the contact positions carried by their owning events. Character-
message clips queue by exact retained ID, while frontend and pause lifecycles
call the same adapter directly. No playback edge is inferred from a presentation
snapshot delta. Script waits and playback-duration gates remain deterministic
mission state.
Released PC `CSoundManager::SetMasterVolume` at `0x004E04C0` stores the supplied
sound-option float directly; the retained-source tangent curve was not shipped.
That direct master value and the externally supplied game-sound mix are
presentation-only adapter inputs, so audio applies but never advances a failure
fade or other ducking timeline.

Tracked Steam function summaries for `PauseMenu__Init` at `0x004CDE60`,
`CPauseMenu__Render` at `0x004D11D0`, input dispatch at `0x004D15D0`, action
dispatch at `0x004D0810`, and resume helper `0x004D06E0` identify the retained
Level 100 root, Retry/Quit confirmation, safe default to No, and back behavior.
The retained English table supplies the localized copy; three exact locally
materialized pause textures and the existing HUD fonts supply the asset inputs.
The current renderer's placement, fade gate, circle transition, colors, and hit
regions are bounded reconstruction presentation, not a claim of exact visual or
runtime parity.

Stuart's source demonstrates the single paused-game flag, blocked event advance,
sample pause/unpause, and kill-then-Select level-exit boundary. The client owns
one `AuthenticMenu` pause reason: it advances zero Core steps, clears held and
pending input, pauses the existing gameplay-audio owner, and resumes only after
a neutral input sample. Continue resumes the same session; confirmed Retry and
Quit complete that existing audio boundary once before calling the existing
frontend lifecycle, whose teardown preserves the new Select cue. Message Log,
Briefing, and settings rows are visible but disabled because no canonical
integrated owner exists; no substitute subpage or settings state is inferred.

Stuart's Level 100 entry calls `PlaySelection(MUS_TUTORIAL)`. The playlist is
alphabetically ordered and `GetSong` is zero-based, so selection index `3`
resolves to exact `data/Music/BEA_04(Master).ogg` (SHA-256
`32D3E338964D74F50D0094536C585375F1E14AA2BAE6087487803F3529EAF360`).
Selection playback repeats that track at completion. Released
`CMusic::SetVolume` at `0x004BBA10` stores `round(option * 127)` as its integer
set volume while preserving the original career float. The adapter normalizes
that integer only at the Godot presentation boundary; this is not a claim about
DirectSound or audible-volume parity. Music remains outside `PauseAllSamples`
and is stopped by the level-exit owner.

A shallow read-only parse of the supported copied `default physics.dat`
correlates the Level 100 unit, weapon-mode, and explosion assignments. It
establishes the Air Trainer's Forsetti flyby loop, the transport's bomber loop,
Target Drone's silent engine and silent missile-launch modes, Drone Vulcan's
`Blaster 2`, shared Forseti/Micro Missile medium-impact audio, target/truck
medium destruction, drone small-debris destruction,
facility medium-building destruction, Battle Engine huge destruction, and the
repair idle/charge/full triplet. No substitute sound is selected for a missing
assignment. The materializer verifies the decoded WAV envelopes, all 51 voice
Ogg hashes, and the tutorial-music Ogg hash; playback/mixing and stream lifetime
remain exclusively in the Godot adapter. The integrated flight stream currently
drives takeoff/in-flight/landing and Mech Vulcan playback, and the destruction
stream drives Pulse impacts plus target/facility terminal effects. Pulse launch,
missile, warning, trainer, transport, repair, and debris records remain retained
but silent until their canonical mechanics or actor events are present.

Static Steam and reference-source evidence establish that Warehouse damage is
forwarded through a 28-entry destructible-segment controller rather than the
root life field used by the tanks. Two fresh uninterrupted app-owned copies
isolated that objective by changing the compiled LevelScript target count from
`4` to `1` at the exact serialized integer byte; archive length and every other
payload byte remained unchanged. Each accepted run required an untouched
Warehouse immediately before the first explicit `Fire`, used only the first
active charge bucket (`10`), and removed the objective on exactly release 12.
The fixed-aim observer could not identify which segments received each earlier
hit or the precise child-break order on the terminal hit, so it does not prove
a synchronous cascade of the remaining intact parts. Core follows the static
controller evidence instead: `CDestructableSegmentsController__ProcessNode`
(`0x00444C10`) uses each non-root segment's greatest BBOX half extent as its
weight; `CDestructableSegment__VFunc11` (`0x00442870`) scales that weight by the
definition maximum life; and `CDestroyableCoreSegment__VFunc11` (`0x00443590`)
applies the `5.0` core multiplier and zeros the first core root. The controller
sums active, unbroken segments' initial health—not partially reduced current
health—and reports terminal when root core children are gone or that sum is
strictly below `30%` of cached total health. Only the contacted depleted
segment detaches. Child-cascade scheduling uses the shared CRT random owner,
whose live phase is unresolved, so Core stops at a typed terminal effect
boundary without inventing cascade timing or debris trajectories.

`CMeshCollisionVolume__VFunc_03` (`0x004AC6E0`) uses each part's BBOX as
rejection metadata before dispatching mesh mode to the one-sided swept-sphere
triangle path at `0x00478510`; explicit sphere and cylinder owners retain their
analytic primitive dispatch in Steam. This bounded Core owner consumes only the
hash-verified, deterministically millimetre-quantized mesh projection needed by
the current Pulse path rather than exposing unused generic primitive APIs or
claiming bit-identical collision geometry. The canonical actor registry remains the
sole owner of identity, definition/mesh binding, active state, full pose,
velocity, health and lifecycle; destruction reports hit/dying/died facts back
through that owner.

The Thing/Actor base-state seam is source-first from pinned references/Onslaught
commit 5352a81cdb838b145a57f7febc5d9fc4b0129ebb
(thing.h/thing.cpp/actor.h/actor.cpp) and reviewed W2 receipt
07fca645affb4d0483d35a52d0e70f39c784d15a, merged by
561c2099acaaec7b2bc65e19b45cc734121967e1. Retail identity comes from the
promoted CThing/CComplexThing/CActor semantic tables; this implementation made
no new specimen, runtime, Ghidra, GUI, or raw-corpus measurement.

Both isolated runs then repeated the released zero-target continuation: player
power changed to `0`, `TUTORIAL_VULCAN_CANNON` played after the script's
one-second pause, and player power returned with Vulcan active and Pulse Cannon
inactive while three moving Target Truck objectives were added. The exact
voice granules and established 18-tick post-roll place the Core weapon handoff
at tick `269` after completion.

The schema-13 Level 100 manifest retains the released class identities and
arrival radii for the ground vehicle, plane, and dropship definitions, plus the
exact Target Tank/Target Truck speed, turn, full-guide cadence, and converted
Core ground-origin offset. Core consumes the released actor command stream,
observes it at the exact 20 Hz base cadence, which is now the simulation tick
itself, and
mutates only the canonical registry pose and current-tick velocity. Focused
product-path checks establish Target Tank 1 naturally following its released
unobstructed route and all three script-spawned Target Trucks entering their
authored paths. The client keys those visuals by canonical ActorId, resolves
their actor definition/mesh binding to the exact materialized Target Truck
mesh and texture, and projects the registry's full pose without owning a
second transform. The waypoint command scalar is retained and hashed but remains
uninterpreted. The released occupancy/path-grid adjustment and initial runtime
scheduling phase are not yet reconstructed, so neither the exact trajectory
nor retail arrival tick is claimed. Plane and dropship class identities/radii
are retained evidence only; their movement is not implemented.
The current exact contact materialization has no Target Truck volume or
destruction definition; Target Truck hit behavior and the Vulcan exercise
therefore remain unimplemented.

These slices do not make the surrounding vehicle model retail-faithful.
Walker acceleration and the bounded projectile path now use the released
continuous yaw/pitch basis; the eight-way projection remains only in provisional jet
movement. Terrain
response beyond grounded height following, dash behavior, terrain-relative pitch and
occlusion, jet-to-walker simulation, exact backend attenuation, secondary Pulse Cannon
visuals, remaining weapon simulation, and flight
dynamics remain provisional.

A passing replay proves repeatability of the encoded state and input history.
A prior native smoke on the opening-slice base proved the client starts; loads
112 Aquila, 111 static-world,
six target, and ten cockpit material surfaces; instantiates all 1,481 pines and
the 625-vertex/1,152-triangle camera-following water grid plus 2,056 shoreline
triangles; decodes the exact locally materialized mesh, nine
Pulse/target-effect, twenty-nine then-retained HUD, five sky, and five water textures;
validates five PCM sound envelopes; and consumes the
retained heightfield, macro/detail/cloud-shadow terrain inputs, and Core-owned
ground elevation. Its deterministic route enters through the cold click page,
Main Menu, world-100 selection, and Loading before it reaches the first Firing
Range exercise, renders the exact target models and shipped objective markers,
resolves the fourteenth message, samples the bounded four-shot input sequence,
preserves the expected Core hash, exercises focus/cursor release, requests a
fresh retry, and returns to the same Main Menu before exiting.
The smoke produces no screenshot and proves no viewport or pixel parity. It
does not prove disabled or unreferenced material modes,
procedural leg solving, collision beyond the two observed facilities,
the separately proven Warehouse completion/Vulcan handoff, mesh-part damage,
secondary effects, complete environment
shading, the inactive optional advanced-water path or dynamic scene
reflection/refraction, the complete mission simulation, later HUD state
production, terrain-relative pitch/occlusion, exact dynamic ring pixels, exact
portrait RNG phase, or visual parity. This HUD milestone deliberately did not
launch Godot or retail; its additional assets and renderer paths are covered by
exact hash materialization, managed compilation, and deterministic Core tests,
not a new runtime visual-parity claim.
