# Aquila render and cockpit meshes

This directory owns the ignored local Federation Aquila inputs consumed by the
Level 100 slice. Run `npm run prepare:rebuild-assets` to materialize the exact
supported files from a user-provided retail installation. These payloads are
not tracked or packaged; `rebuild/LICENSE` covers reconstruction code only.

The three source meshes come from the released PC game's
`data/resources/meshes` directory. Stuart Gillam's pinned source independently
names `f_be1.msh`, `f_be2.msh`, and `cockpit2.msh` for the same walker, jet, and
first-person roles.

| Local materialized file | Role | SHA-256 |
| --- | --- | --- |
| `Source/m_f_be1.msh.aya` | Released walker CMSH archive | `D4C8FA752229AF4111B31EFA5FF5928C892736FAA6A807915412767F3CD3C6B2` |
| `Source/m_f_be2.msh.aya` | Released jet CMSH archive | `35AADA1313C3CBB796BA75DB071321035F7005096DA7C148A7514944F4772B4C` |
| `Source/m_cockpit2.msh.aya` | Released first-person cockpit CMSH archive | `008B9292C59A5564BA3696F65D5BD51030D3E57250BC792D9D2B7F01292CDD4A` |
| `Textures/cockpit.texture.aya` | Released 512×512 `meshtex%cockpit.tga(0)A1R5G5B5.aya` texture | `C62D0C668226F056DB7455C8A5A8FA7D55AB7621ADE1E58392D6AAAD3C00F0CC` |
| `Textures/bluegun-light.texture.aya` | Released 64×64 `meshtex%A8_bluegunlight_LIT.tga(0)A8R8G8B8.aya` texture | `85858E7809A974B74F3DB5A169E081FC9DD506558F1CA99FA47C7832D8552FC5` |
| `Textures/be-tex-a.texture.aya` | Released 512×512 `meshtex%BE_texA.tga(0)A1R5G5B5.aya` texture | `86F9F54AE97BA4E3782C65909D1D93B86566228B1132829EBB93816EB5A4705B` |
| `Textures/be-tex-b.texture.aya` | Released 1024×1024 `meshtex%BE_texB.tga(0)A1R5G5B5.aya` texture | `EA01431A4023ABD517DAF5A27066EB7EDF706100FB3991566726FB4530490B60` |
| `SoundEffects/strafe.wav` | Exact XAP record 20, `Battle Engine\N_BE_dash` | `8F22E6201B2FE8AC003D7D07C635F26A1B99277791F5262447F0400C747ECAB1` |
| `SoundEffects/energy-critical.wav` | Exact XAP record 21, `Battle Engine\N_BE_energy_critical` | `600776048D576D88839CF134372E38D739671DE30AD27FBEC880C0CE3A09CF18` |
| `SoundEffects/energy-low.wav` | Exact XAP record 22, `Battle Engine\N_BE_energy_low` | `6F1434FBAE6A2373A7C5FDA5D921A5C61BB3006AB2A8EEB47245E1F11D55C70D` |
| `SoundEffects/engine-takeoff.wav` | Exact XAP record 25, `Battle Engine\N_BE_engine_takeoff` | `3698A4419C000AB982CBC92C6553AC2639272FB85930DF52A26528B232F00798` |
| `SoundEffects/engine-inflight.wav` | Exact XAP record 23, `Battle Engine\N_BE_engine_inflight` | `0E6EB03AA2C2991C0E59C3483956B1C608A79700E0DE179C855491CFF548AC04` |
| `SoundEffects/engine-land.wav` | Exact XAP record 24, `Battle Engine\N_BE_engine_land` | `07A03B5AF4980D0D2E7C16BB5DE833365483237CB4C83FF83B78D9AAB61FBB36` |
| `SoundEffects/target-locked.wav` | Exact XAP record 29, `Battle Engine\N_BE_homing_missile_lock` | `32BEF3C615361B472C5B36D8952B6CA0D03BDA74CDF8F9617D1E99B17D08D6D6` |
| `SoundEffects/target-acquired.wav` | Exact XAP record 30, `Battle Engine\N_BE_homing_missile_target` | `3B0542A1DF0541A3E19EC108D4C2E198556BFD406ABA2F386E85B803CA04D5BC` |
| `SoundEffects/hydraulics.wav` | Exact XAP record 31, `Battle Engine\N_BE_hydraulics_02` | `F02C11C62ABB998EA35D38EDE180AAF9B4CCAEAAAE8F76E017E239A65570BF21` |
| `SoundEffects/incoming-missile.wav` | Exact XAP record 32, `Battle Engine\N_BE_incoming_missile` | `7F00CE0CF622FDC65CA0AE3F06C7E68F093453DB35486552B56715A71B9DCD0D` |
| `SoundEffects/micro-missile-fire.wav` | Exact XAP record 33, `Battle Engine\N_BE_micro_missiles_fire` | `1C381C693CCCFAE6C8759A5BF417587AF4F43845CCFF827E25EA388DF9A34F20` |
| `SoundEffects/vulcan-cannon-fire.wav` | Exact XAP record 40, `Battle Engine\N_BE_vulcan_cannon_fire` | `BED47939FE924BF5D9A5BA273F9F61D6269813749EA43ACA7DB8C2BC1152078D` |

Godot reads these exact AYA hierarchies directly. The bounded loader validates
archive hashes, CMSH counts, the complete parent/reference graph, virtual-frame
maps, transform streams, animation tables, geometry, and six ordered material
slots. It expands 63 walker parts into 54 surfaces, 54 jet parts into 58
surfaces, and 21 cockpit parts into 10 surfaces. No flattened Aquila or cockpit
OBJ is a runtime input.

Steam `CMeshPart::InterpolateSegmentTransform` maps a virtual frame through
`VHFM`, interpolates `HPOS`/`HORI`, and the mesh controller recursively composes
that local transform through each parent. The serialized base transform agrees
with the reviewed walker pose but is not an invariant for the animated jet or
cockpit, so each exact hierarchy is validated at its consumed frame instead.

One clean Level 100 control and two uninterrupted copies with the proven
early-flight mission change establish the walker-to-jet presentation:

- The same Transform action is rejected by the clean control with no render,
  animation, cockpit, or camera change.
- Both modified runs swap to the jet hierarchy as raw state `2 → 1` begins.
  The external `walktofly` animation advances from virtual frame 25 at 20 Hz
  and switches to looping `fly` frame 0 after 1.243 and 1.241 seconds.
- The cockpit has an independent `walktofly` range. Runtime has already
  advanced it to frame 27 on the first transition sample, shows through frame
  49, and switches to `fly` frame 0 after 1.138 and 1.141 seconds.
- The raw state commits to jet after 540 and 550 ms; that state boundary does
  not truncate either visual animation. The first-person camera pointer and
  type remain unchanged.
- Takeoff and looping in-flight effects begin at transition entry. The client
  consumes their exact decoded PCM records; exact retail spatial mixing and
  fade timing are not claimed.

Stuart's `CBattleEngine::HandleSounds` and the released effect lookup establish
the remaining bounded contract. Strafe is an immediate effect; the low-energy
and critical-damage tones repeat until their owning state clears; landing stops
the in-flight loop; and the loop pitch target is `1 + thruster * 0.25`. The
shipped descriptor has no `BE On 02` effect, the foot playback calls are
disabled, and hydraulics is a stop-after-walking cue. The adapter therefore
does not invent a walker idle or footstep loop. The two shipped bullet
damage/shield descriptors are marked `NOT WORKING`, are not loaded by the
released Battle Engine initialization, and are likewise not retained or
played.

The released `LegMotion` table supplies 100 usable per-leg extension poses, not
one shared movement-driven gait. Steam `CMCMech` identifies four exact five-part
chains and selects the closest frame independently for each planted-foot
distance. Core owns the four deterministic contacts and released diagonal
swing subset over the Level 100 HFLD; Godot only adapts those contacts to the
retained hierarchy.

This is bounded support for the walker pose and walker-to-jet presentation. It
does not establish exact toe-normal alignment, CMC body sway, non-heightfield
contact, damage states, jet handling, jet-to-walker presentation, cockpit FOV
animation, exact backend attenuation, or occlusion.
