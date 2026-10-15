# Released frontend assets

This directory owns the ignored, locally materialized retail inputs used by the
bounded startup → main menu → Level 100 → loading → post-Won debriefing path. Run
`npm run prepare:rebuild-assets` to verify a user-provided Steam installation
and reproduce these exact files. The payloads remain outside Git and
release packages and remain copyright of their respective rights holders;
`rebuild/LICENSE` covers reconstruction code only.

Steam's `-skipfmv` path begins at the released click-to-start page. An ordinary
launch first streams the startup movie from the ignored, locally materialized
startup cache; briefing and outcome movies remain outside this bounded lane.
New Game, Load Game, Options and Quit are working main-menu actions. Load Game
reuses the career-name/list surface over caller-injected read-only descriptors;
repeating `--career-save=<path>` is the Godot host's only input adapter. It reads
those exact named files and performs no save-directory discovery or writes.
Continue, Multiplayer and Goodies remain visible; only Continue is drawn dim.
Core/Client can carry any loaded career's suggested world and applies the
released unlock law. The current Godot selector does not render that general
state or traverse it by keyboard: its pointer path exposes only world 100 and
unlocked world 110, while the host constructs only world 100.

> **Corrected 2026-07-28 — both halves of this were false at HEAD.** The
> paragraph above previously read "New Game and Quit are the only working
> main-menu actions. Continue, Load Game, Multiplayer, Goodies, and Options
> remain visible but explicitly unavailable." That predates commit `5e82ae42`
> (2026-07-28), which shipped the Options pages; this file was last touched by
> `32f3ce8b` one day earlier and was not carried along.
>
> Evidence: SOURCE (this reconstruction's own code, at HEAD, not a capture).
> `../../../OnslaughtRebuild.Client/RetailFrontendSession.cs` routes Options to
> a real page in its `MainMenu` `Confirm()` arm — `Screen =
> RetailFrontendScreen.Options; return RetailFrontendSignal.PageChanged;`, under
> the comment "CFEPMain__DoAction case 5 -> SetPage(0x11, 0x46), UNGATED".
> The page is built by `../../../OnslaughtRebuild.Client/RetailOptionsMenu.cs`
> (Root, Controller, Video and Sound, with a live `ApplyPage()`) and presented by
> `../../RetailFrontendFlow.Options.cs`. In the same file's `MainMenuItems`, only
> `ContinueGame` carries `IsAvailable: false`; `LoadGame`, `Multiplayer`,
> `Goodies` and `Options` all carry `IsAvailable: true`, under a comment stating
> that this is measured from the pristine 640×480 main-menu capture. Load Game
> now routes to the injected career-list mode and emits a one-shot selected-career
> handoff; Multiplayer and Goodies still fall through to
> `RetailFrontendSignal.None`.

Loading hands a fresh canonical Level 100 session to the gameplay host. The
gameplay pause owner's Retry and Quit actions reuse that loading/Main Menu
lifecycle. After a Won handoff, this lane now composes the settled
`CFEPDebriefing` status/objective/grade surface. Gameplay outcome production,
the in-game terminal overlay, outcome movies, save writes/autosave, and
persistent subsequent-campaign updates remain outside it.

## Materialized inputs

| Local file | Released source or derivation | SHA-256 |
| --- | --- | --- |
| `Backgrounds/click-to-start.texture.aya` | `FrontEnd%v2%fe_splash1.tga(0)A8R8G8B8.aya` | `46AB45168875B5B686E3534B3F66AB65B5A5B5512F697E5A98B03DD12708731A` |
| `Backgrounds/rock.texture.aya` | `FrontEnd%v2%FE_Rock_Background.tga(0)A8R8G8B8.aya` | `89213B441332F060ACDB3E55AA28C290FA0E530983C16A57B8CE1A7413E9E86D` |
| `Backgrounds/fe-back-128x128x30.rgb` | Exact rgb24 decode of `data/video/FEBack128.vid` (SHA-256 `C251F4BE…0E79BA`, BIKi 128×128) at the shipped 30 fps × 572 frames; CFEPMain underlay via `CDXFrontEndVideo__Render`. Requires local `ffmpeg`. Godot stretches each frame to the 640×480 stage. Replaces the half-rate `fe-back-128x128x15.rgb` (286 frames), whose decimation made the underlay phase wrong at every instant once it was actually drawn. | `6BA092B1B43959DB8EB73F6D0B9434ADDBDCF5DDC030E10D8D35C11208001265` |
| `click-slide.texture.aya` | `FrontEnd%LostToys.tga(0)A8R8G8B8.aya` (`0x0089d7bc`) | `AB1B3654842335983E7170F233137731FEA5A25E8632A1F94CFCADCCF758040B` |
| `forseti-writing-large.texture.aya` | `FrontEnd%v2%FE_Forseti_Writing_large.tga(0)A8R8G8B8.aya` (`0x0089d7f0`) | `6BC5671A482817E4B5702E348433C66D1E87178D068C6DA36500273885B004C9` |
| `reflection-map.texture.aya` | `FrontEnd%v2%FE_Reflection_map.tga(0)A8R8G8B8.aya` (`0x0089d7fc`) | `E480261FBBA5CFAFB646D52F217BC11983BAE1285DE16D6F80A9DE6C017F0121` |
| `title-logo.texture.aya` | `FrontEnd%v3%FE_BEA_Title2.tga(0)A8R8G8B8.aya` | `5AE9B300836D27BD13462A53E3455B649BB46BF8F48C8C326FD8F4F0C18C7EC7` |
| `title-bracket-01.texture.aya` | `FrontEnd%v3%FE_BEA_title_bracket01.tga(0)A8R8G8B8.aya` | `679B5FA6220B3EB54AEEF1D970890C35BE5DF264530226F5D08B22A63AD75064` |
| `title-bracket-02.texture.aya` | `FrontEnd%v3%FE_BEA_title_bracket02.tga(0)A8R8G8B8.aya` | `79F05E8C64B6E25F038C5B7C37DDADFD31EE9376E92FC5DA505B6C427ED9C74F` |
| `title-text-box.texture.aya` | `FrontEnd%v3%FE_BEA_title_text_box.tga(0)A8R8G8B8.aya` | `C007742E1FE9B93E988D198F8A2A4E741E546843FD36218D9015AB2EE6627B9C` |
| `symbol-bracket-01.texture.aya` | `FrontEnd%v3%FE_BEA_title_symbol_bracket01.tga(0)A8R8G8B8.aya` | `3243E641E9AD45CD8B80C4ABEBAA1E6F73B5ED774E0B4DBA1AFBCBBAF81A49A8` |
| `symbol-bracket-02.texture.aya` | `FrontEnd%v3%FE_BEA_title_symbol_bracket02.tga(0)A8R8G8B8.aya` | `92739AF94BEC154D898AFB5E59432694A789BB3F2C37242EB65272684DAEB687` |
| `Icons/new-game.texture.aya` | Released v3 New Game symbol | `D3FF62FBC8193E15BF250C82088F5088B17C667277DBB5FFF92F2980CC3DEB70` |
| `Icons/continue-game.texture.aya` | Released v3 Continue Game symbol | `83C9FA4D7E786AE4353D1F639C75B007BC0C65F1412B447D68967D9E5B4CCA0E` |
| `Icons/load-game.texture.aya` | Released v3 Load Game symbol | `9D1BB0D9EFC450FC2BCE244E01A2975468F07CF785BF6854A5BC9495FFFDC001` |
| `Icons/multiplayer.texture.aya` | Released v3 Multiplayer symbol | `8A7D7DBA563B153B314E04DAAAD4FFA2D0969B65A0603DE043027EAF5B4DF031` |
| `Icons/goodies.texture.aya` | Released v3 Goodies symbol | `EFA9EC1D2317E3CDF2ED9A90CC8B6CB391E6ED1099740DDAEB2C808B49F33358` |
| `Icons/options.texture.aya` | Released v3 Options symbol | `0824D66ACEC9DAD5037BE8BFC2B863201F94404D21795EAC4FAD82D8C4DA2ABA` |
| `Icons/quit.texture.aya` | Released v3 Quit symbol | `7096F573FF30302B5D5DAD8F56EBD633E51F2BD70613D5349B974DADA17B7A93` |
| `Flags/flag-uk.texture.aya` | `FrontEnd%v2%Flag_UK.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `EFBDBA2A567B771F48B5314A941A2196BD83C75363FBCF5DB91050AB9765D7E9` |
| `Flags/flag-fr.texture.aya` | `FrontEnd%v2%Flag_FR.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `1FA4179014B9B0C20F1BBD2336B8680A9F58452B46BDACED042FE685668D7452` |
| `Flags/flag-gr.texture.aya` | `FrontEnd%v2%Flag_GR.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `55ED2BB99B8572B5BDA2EC3A89E9A4897462533FF46C7C6E0B66AA2FD20A23AF` |
| `Flags/flag-it.texture.aya` | `FrontEnd%v2%Flag_IT.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `B397A80418458A253EA83DD54A03BD4FA035843BD629511C846CD28CDCF01432` |
| `Flags/flag-sp.texture.aya` | `FrontEnd%v2%Flag_SP.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `75A99ECA5236DC940032D874F5F039A6C42307CC20534572CEA80E2B8631BF1B` |
| `fe-arrow.texture.aya` | `FrontEnd%v2%FE_Arrow.tga(0)A8R8G8B8.aya` *(row added 2026-07-28)* | `ECF729F9402512B5FCE21CD53D2A239A4B0991230DDDC089C4AF59325105AB82` |
| `level-bracket-01.texture.aya` | Released v3 level-select bracket 1 | `560DB1621169C1B5787FC9C4691F4BEDE1AF292674F84D4D43BE11CA05166AA5` |
| `level-bracket-02.texture.aya` | Released v3 level-select bracket 2 | `7AD21E2A6E64F61998F7A43E92FE92D69AC013B169FD8107B648B1FA69877B27` |
| `level-ring-01.texture.aya` | Released v3 level-select ring 1 | `687EAF0945B701B622BDEBDE805E88CAC394734A4B4420155379993EF9F74E1C` |
| `level-ring-02.texture.aya` | Released v3 level-select ring 2 | `620900D34C153E722B6D78A9FBECAB2D69B8E81ABCDBDA084B0F90EB96142DFF` |
| `Debriefing/metal-ring-transition.texture.aya` | `FrontEnd%v2%FE_metal_ring_trans_from_levsel2.tga(0)A8R8G8B8.aya` | `8B9B79189981DE706FC72DE9552BDC398ADA96D892C0E4E33706C77A907DA869` |
| `Debriefing/ranking-a.texture.aya` | `FrontEnd%RankingA.tga(0)A8R8G8B8.aya` | `88765906F295B8002DC254F9805B05D5F5589D87A08F297B37D90953BA57D625` |
| `Debriefing/ranking-b.texture.aya` | `FrontEnd%RankingB.tga(0)A8R8G8B8.aya` | `5F03CDF1E79E679E706A0B6AB644AD6961447DACE35695DC93D7C3928EBF3D50` |
| `Debriefing/ranking-c.texture.aya` | `FrontEnd%RankingC.tga(0)A8R8G8B8.aya` | `364B5140A18EBEC99BDF4E4CE7C99E3619FF743D4CBE888C0FA5D84E96407A06` |
| `Debriefing/ranking-d.texture.aya` | `FrontEnd%RankingD.tga(0)A8R8G8B8.aya` | `894521839162A5D2D8D73FEBA39435CF0590B0863D65B67DAA5C4A4420485BD0` |
| `Debriefing/ranking-e.texture.aya` | `FrontEnd%RankingE.tga(0)A8R8G8B8.aya` | `C844F938CFCF56A2F3908BB69313EC86153F97E6993F42172C2C8376897E911A` |
| `Debriefing/ranking-s.texture.aya` | `FrontEnd%RankingS.tga(0)A8R8G8B8.aya` | `AAB0817F798FF8379CF6E9E0F86AE466832AD644170E8C14B6F7A50A6A12DE9F` |
| `title-font.texture.aya` | `mustbe_TitleFont.tga(0)A8R8G8B8.aya` | `1941E28A5665665FB7F8F733E7A4854C60DEF33E1D4F1CB9CAA979BC204D0707` |
| `system-font.texture.aya` | `mustbe_SystemFont(0)A8R8G8B8.aya` | `475EDC8C9B95E3D3619E9B78E168DFCDA8575042B728D96DE1598CB8917967EB` |
| `loading-screen.texture.aya` | `LoadingScreen.tga(0)X8R8G8B8.aya` | `E4AD32FEE41A31477E97D4F6F0B280F33C360756E3ABA27BF23746038443FC2C` |
| `mouse-cursor.texture.aya` | `mouse.tga(0)A8R8G8B8.aya` | `366021DEF699DE220AD018C40250EEFACCAAB356C6C5D93FE0AA1B7F5302354C` |
| `SoundEffects/move.wav` | Exact 44.1 kHz PCM decode of XAP record 42, `Front End\N_FE_move`; consumed by the integrating audio lane, not this flow | `76B2458E9C5854DAF7237EA81B4F288AE09963BC10E7651E81E858FDB68CE83B` |
| `SoundEffects/select.wav` | Exact 44.1 kHz PCM decode of XAP record 43, `Front End\N_FE_select`; consumed by the integrating audio lane, not this flow | `F84144C80405FE9F745B8CF4BD352D7FA4F8C0A8BA481C770C2C7C0A9053ADE1` |
| `SoundEffects/back.wav` | Exact 44.1 kHz PCM decode of XAP record 41, `Front End\N_FE_back`; consumed by the integrating audio lane, not this flow | `133B78E813C6B393BE4DBA1D263F69513958B0AB827D6603F952D6E0A82BA02B` |
| `english.json` | Ten menu/launch strings decoded from English `english.dat` SHA-256 `789ECFF619D077092769DF281C540D138A25FCC74D70023466A604888E59371A` | `B27D7B1B3F8CD8AA22B664CACF7C87A8B0907C7DEA4C4F07DFF8DA763DBB70F3` |
| `english-worlds.json` | Per-world selector names and briefing body slots decoded from the same `english.dat` (pool-authoring-order slot law; see `materialize_retail_assets.py::_frontend_world_strings_bytes`) | `FFE3D3F88E07D5F29D21D26EF07BF056D153B622416110250DC1C78BF2C35408` |
| `Music/frontend-track-08.ogg` | Exact copy of `data/Music/BEA_09(Master).ogg`, the released `MUS_FRONTEND` zero-based track 8 of the alphabetical `data\music` `*.ogg` playlist | `4F6166F655E62DEC6993643A8A860BDEA0ABB7D853AD443F5D03E95368BE93A1` |

### Six rows added 2026-07-28

The table above previously listed 30 files while the directory held 38. The
five language flags and `fe-arrow.texture.aya` were absent, and they are not
incidental: they are the language selector added by commit `e9b86162`. No
existing row changed, and all 30 pre-existing hashes were recomputed against
disk in the same pass and all match. In that historical `e9b86162` pass,
differencing the table against the then-current materializer returned exactly
these six, and each hash matched both the file on disk and its pinned value.

**Corrected 2026-07-28:** the table previously excluded
`system-font.texture.aya` and `mouse-cursor.texture.aya`, and the materializer
did not produce the former. Both are now ordinary hash-pinned frontend inputs.

## Cold-start media is NOT in this directory

Retail's splash and intro FMV — `data/video/LTLogo.vid`,
`data/video/OpeningFMV.vid` and `data/textures/splash.tga` — are decoded by
`materialize_retail_assets.py --startup-media` into a cache **outside**
`res://`, defaulting to `%LOCALAPPDATA%/OnslaughtToolkit/startup-media` and
overridable with `--startup-media-root` or `ONSLAUGHT_STARTUP_MEDIA`.

They are deliberately not staged here. Everything in this directory is inside
the Godot project, and a `.gitignore` entry stops a `git commit` but does **not**
stop a Godot export from packing an ignored file into the PCK — the export scans
the project directory, not the git index. Two decoded retail movies are the last
thing that should depend on that distinction. `fe-back-128x128x30.rgb` in
`Backgrounds/` predates this lane and IS exposed to that hazard; no export preset
exists in the project today, so the risk is latent rather than active.

The reconstruction plays them through `RetailStartupSequence`, gated by
`--intro` / `--skipfmv` (retail's own flag name). Capture and smoke runs suppress
them, because every retail reference frame in this repository was captured with
`-skipfmv` on.

## Evidence boundary

- Steam Ghidra: startup handlers `0x0051B660`/`0x0051B6B0`, prompt render
  entry `0x0051B840`, main page vtable `0x005DBAE4`, level-select input/render
  `0x004606B0`/`0x00460B40`, loading renderer `0x0042C810`, and debrief
  vtable `0x005DB9C0` (`Initialize` `0x00456780`, `ButtonPressed`
  `0x004568A0`, `Process` `0x00456930`, `Render` `0x00456DD0`).
- Stuart source: `FrontEnd.cpp` establishes first-run intro, title return,
  debrief return, and initial selected world 100; `PCFrontend.cpp` names the
  move/select records; `game.cpp` establishes the distinct in-game result
  overlay. This lane does not compose the in-game terminal overlay; it now
  composes only the statically recovered settled `CFEPDebriefing` surface.
- Shipped Level 100 `LevelScript.msl` defines four primary objectives,
  `LevelWon()`, and its only `LevelLostString(LOSE_TUTORIAL_BROKE)` reason.
- Startup/main/level placement, widescreen composition, and unavailable-item
  dimming are reconstruction-owned adaptations. The main underlay identity is
  the released FEBack128 Bink path (not Rock); the rebuild draws a verified
  ffmpeg rgb strip rather than linking Bink. The exact loading image and text
  are retained, but no exact layout, timing, control-hint, full pixel-parity,
  terminal-overlay, or complete-debriefing claim is made. Debrief entry/exit
  transitions, Goodie effects/message, grade glint, grade compositing
  equivalence, and retail-frame validation remain open.
