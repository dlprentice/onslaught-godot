# Level 100 pause assets

Status: active
Last updated: 2026-11-06 (materialize command added; header fields added; the 2026-07-28 correction moved to git history and its present reasons stated; commit identifiers removed)
Summary: the four hash-gated pause-menu textures that the local materializer copies, and the state of the Level 100 pause menu.

Terms used in this file are defined in the [glossary](../../../README.md#terms-used-in-these-documents).

This directory retains no retail payload. The local materializer copies four
hash-gated inputs from a user-provided Steam installation to ignored paths.
Run `npm run prepare:rebuild-assets` (see the [Aquila README](../Aquila/README.md))
to materialize these four inputs.

| Local file | Released source | SHA-256 |
| --- | --- | --- |
| `blank.texture.aya` | `data/resources/dxtntextures/FrontEnd%v2%FE_Blank.tga(0)A8R8G8B8.aya` | `01D55E16E994DEC02191E6F62ADCB5A82A1F0CA3B5629E489C9E5C82884FE7E9` |
| `circle-01.texture.aya` | `data/resources/dxtntextures/pausemenu%pause_circle01.tga(0)A8R8G8B8.aya` | `01622D0E442AEF1CCD5D5971CAD5B4C06D2B15D1980DD07A1DDF95A7BC7472C7` |
| `circle-02.texture.aya` | `data/resources/dxtntextures/pausemenu%pause_circle02.tga(0)A8R8G8B8.aya` | `4CD5E553A8A093D3A895F3639136FB6548267371C28914B00D3CCBB3C153516F` |
| `endcurve.texture.aya` | `data/resources/dxtntextures/MessageLog%endcurve.tga(0)A8R8G8B8.aya` | `843DEFB41F22782E01C037D36A1B03B3AB8BA6C81624B9AD6E0B6E61E2568635` |

## `endcurve.texture.aya` — the confirmation panel's corner cell

`CMessageLog__RenderPanelFrame` (`0x004B9010`) holds this texture at `this+0x08`
and draws it into the four 32×32 corner cells of the panel frame, with
`FrontEnd%v2%FE_Blank.tga` filling the four stretched edges and the centre.

Decoded from the 291-byte source above (one AYA zlib record, 1152-byte `DDS `
payload): **32×32, `DXT2` FourCC, no mip chain**, 1024 bytes of block data =
exactly 64 4×4 blocks for a single 32×32 level. The 32×32 read in the panel
frame is therefore the texture's native size, and matches the measured corner
scalar `0x005DB2B8` = 32.0.

Alpha content, from the decoded 1024 texels: 753 fully opaque (255), 208 fully
transparent (0), 63 intermediate spread evenly across the 4-bit ramp — a soft
one-texel antialiased edge. The shape is a filled quarter disc, opaque toward
the lower-left and clear toward the upper-right, so retail flips it per corner.
RGB is effectively white (246..255 on every opaque texel; 36 distinct RGB
triples total), which is consistent with the frame being drawn as a pure alpha
mask under the measured `RGB 0, alpha 192` tint. As with every other DXT2 asset
here the label is a retail toolchain mislabel: alpha is **straight**, not
premultiplied — 31 of the 63 semi-transparent texels already have RGB above
their own alpha, which premultiplied data cannot.

The renderer reuses the existing ignored `Hud/font-22.texture.aya` and
`Hud/font-13ps.texture.aya`. Tracked Steam function summaries for
`PauseMenu__Init` (`0x004CDE60`), `CPauseMenu__Render` (`0x004D11D0`), and
the menu-range/item renderers identify the root, confirmation, render, and
input/action owners. Localized root and confirmation text was checked against
shipped English table SHA-256
`789ECFF619D077092769DF281C540D138A25FCC74D70023466A604888E59371A`;
the three option-root labels also exist as literal Steam strings.

The current renderer's 640×480 coordinate space, placement, fade timing,
circle motion, colors, and row hit regions are bounded reconstruction choices;
the tracked evidence does not establish pixel or runtime presentation parity.
Only the evidenced root and Retry/Quit confirmation flow belong to this
renderer. Message Log and Briefing remain visibly disabled because the current
rebuild has no canonical integrated panel for them. Controller Options, Sound Options and Video Options are also still disabled **here**, although a panel and a live settings-mutation owner for all three exist elsewhere in the reconstruction (see below). Continue
resumes the same deterministic session; Retry replaces it through the existing
frontend Loading seam; Quit returns to the existing Main Menu. No pause subpage
or parallel mission/frontend state is inferred.

The three option rows are constructed disabled: `../../../OnslaughtRebuild.Client/Level100PauseMenu.cs` passes `false` for each, against the record struct's `IsEnabled = true` default. A panel and a live settings-mutation owner for all three exist elsewhere in the reconstruction. `../../../OnslaughtRebuild.Client/RetailOptionsMenu.cs` dispatches `Root`, `Controller`, `Video` and `Sound`, builds the root's three page links citing `PauseMenu.cpp:1346/1347/1348`, and commits changed rows through `ApplyPage()` and `ApplyRow` into `RetailOptionsSettings`. `../../RetailFrontendFlow.Options.cs` acts on them, calling `DisplayServer.WindowSetVsyncMode` and raising `OptionsSettingsChanged`, which `../../FirstFlightGame.cs` applies to the sound, music and mouse-sensitivity values. The frontend Options page and the in-game pause menu use the same pause-menu initializer and widget implementation, but controlled runtime evidence shows distinct instances: frontend Options keeps its own context and resets that context's root session on each entry. Evidence: SOURCE (this reconstruction's own code at HEAD and the pinned GPL source it cites; not a capture).

**What is NOT claimed:** how much work remains to wire these three pause rows to that owner. Nothing here has been measured against retail's in-game Options presentation, and no estimate of the remaining job is made.
