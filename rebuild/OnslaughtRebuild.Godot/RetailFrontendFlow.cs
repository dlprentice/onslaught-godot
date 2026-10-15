// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Godot;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The bounded released-style frontend path into the Level 100 opening slice.
/// It owns its page/input/loading lifecycle and exposes load, retry, and
/// Main Menu return seams to the existing gameplay host. Mission/HUD and audio
/// presentation remain separate owners.
/// </summary>
public sealed partial class RetailFrontendFlow : Control
{
    // Steam FE virtual stage (cluster hint from CFEPMain / click render).
    private const float DesignWidth = 640f;
    private const float DesignHeight = 480f;
    private const float MenuColumnX = 219f;
    private static float MenuStartY => RetailMainMenuRowY.NonzeroSlotY;
    private const float MenuPitch = 20f;
    private const float MenuHitHalfWidth = 120f;
    // mustbe_Font13PS.tga — 256² atlas, 16px cells, 16 columns, ASCII-32 origin.
    //
    // This is NOT mustbe_TitleFont.tga. TitleFont is a 256² / 32px / 8-column atlas
    // containing uppercase A–Z ONLY — no lowercase, no digits, no punctuation
    // (verified by decoding the DDS and rendering it). It cannot draw "New Game",
    // and it cannot draw "V1.00". Retail draws both, so TitleFont is not the menu
    // font. The previous ToUpperInvariant() call and the lowercase-folding in
    // GlyphIndex existed only to make that wrong atlas appear to work.
    //
    // The binary names three fonts: TitleFont.tga, Font13PS.tga ("small font") and
    // font22.512.tga, at 0x23e0d4 / 0x23e10c / 0x23e178. Font13PS carries the full
    // set including lowercase and the accented glyphs the five languages need
    // (Career.h: NUM_LANGUAGES 5), at the ~13px cap height retail's menu uses.
    private const int GlyphColumns = 16;
    private const int GlyphCellSize = 16;
    private const int FirstGlyph = 32;
    private const int GlyphSlotCount = 256;

    // mustbe_font22.512.tga — the third font the binary names (0x23e178), a
    // 512² atlas with 32px cells on the same 16-column / ASCII-32 grid.
    //
    // MEASURED, and it overturns an assumption this file has carried since the
    // main menu was built: the frontend HEADER TITLES are font22, not Font13PS.
    // Fitting both atlases against the pristine MISSION BRIEFING title band
    // (x280..500, y66..94) by normalised cross-correlation over a free
    // scale/offset sweep scores font22 0.951 at scale 1.00, origin (287,65),
    // against Font13PS 0.569 at its best fit (sx 1.43, sy 1.66). The font22 fit
    // also reproduces HEADER_BAR_X exactly: its advance width for
    // "MISSION BRIEFING" is 205, so origin 287 centres it on x = 389.5.
    //
    // The FEP_DEVSELECT and FEP_LEVEL_SELECT titles above are still drawn with
    // Font13PS at 1.5. They are NOT corrected here: both pages' captures are
    // pinned no-regression baselines for this change, so re-fonting them is a
    // separate, separately-verified edit. The same fit run against those two
    // pages is the way to settle it.
    private const int Font22Columns = 16;
    private const int Font22CellSize = 32;

    // Main-menu row tints. The RGB triples were already exact; the ALPHA BYTE was
    // 2/255 high on all three and is corrected here from retail's own diffuse
    // values, 2026-07-27 d3d9 sweep,
    // <maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\main-menu-settled.csv
    // frame 3000: the six live rows and their shadows carry 0xFD (draws 12-25,
    // e.g. d17 0xFD4F4F4F, d13 0xFDFF6F3F) and the single disabled row carries
    // 0x7D (d14/d15, 0x7D000000 / 0x7D1F1F1F). Counted over that frame: 0xFD x36,
    // 0x7D x6, and no 0xFF or 0x7F anywhere in the row block.
    //
    // 0xFD IS NOT A GLOBAL FRONTEND CONSTANT and is deliberately not applied as
    // one: the same sweep measures 0xFF on the version string and on every
    // options row, and 0xFE on the mission-briefing body. Only the FEP_MAIN rows
    // are 0xFD.
    private static readonly Color ReleasedNormal = RetailColor(0xfd4f4f4f);
    private static readonly Color ReleasedUnavailable = RetailColor(0x7d1f1f1f);
    private static readonly Color ReleasedSelected = RetailColor(0xfdff6f3f);
    private static readonly Color ReleasedBlue = RetailColor(0xff1f4f7f);
    // 0xfeafcfff is an immediate in the image: `and edx,0xffafcfff` /
    // `or edx,0xafcfff` at 0x004642e4 / 0x004642f1 inside CFEPMain__Render.
    //
    // Whether it goes through RetailColor()'s MODULATE2X was tested on 2026-07-26
    // and plain MODULATE was REFUTED — full-frame material rose 18.54 -> 18.73 %
    // and meanD 6.19 -> 7.07 on all 13 paired frames.
    //
    // THAT TEST IS WITHDRAWN, because it was run against a build that was missing
    // the additive sheen (TitleLogoReflectionLayer). Two errors were cancelling:
    // the sheen adds a mean +42/+39/+26 inside the logo footprint, and a
    // 2x-clamped white tint was covering for it. With the sheen restored and
    // subtracted, retail's logo body against ours regresses to
    //   slope 0.589 / 0.704 / 0.850, intercept +9.5 / +10.7 / +14.9
    // over 420k unsaturated footprint pixels on the 12 settled frames. The SLOPE
    // RATIOS 0.692 / 0.828 / 1.000 are the packed tint's own ratios
    // 0xaf/0xff = 0.686, 0xcf/0xff = 0.812, 1.000 — so the hue is real and the 2x
    // that erased it is wrong here. The default render-state block sets stage 0
    // COLOROP = D3DTOP_MODULATE, not MODULATE2X
    // (reverse-engineering/binary-analysis/d3d-default-render-state-block-2026-07-27.md
    // section 7), which is the mechanism, not a fit.
    //
    // STILL UNEXPLAINED, and deliberately NOT tuned away: a uniform residual gain
    // of ~0.86 and an intercept of ~+12 remain after the tint ratios are taken
    // out. Candidates are the 0x3e000000 logo shadow showing through partial
    // alpha, and the DXT2 premultiplied-alpha path. Neither is measured.
    private static readonly Color TitleLogoTint = new(
        0xafu / 255f,
        0xcfu / 255f,
        0xffu / 255f,
        0xfeu / 255f);
    private static readonly Color HighlightTint = RetailColor(0x7e000000);
    // 0xfe7f7f7f, CORRECTED 2026-07-28 from 0xfeffffff. The RGB byte was never
    // measured; it was the neutral guess, and 0xff is the one value MODULATE2X
    // cannot round-trip — (0xff*255)>>7 saturates at 255, so any authored byte
    // above 0x80 renders identically and the constant carried no information.
    // main-menu-settled.csv frame 3000 gives the exact byte on all three decor
    // bodies this tints: draw 27 (left arc, 320x320), draw 29 (right arc,
    // 160x160) and draw 31 (selected-row icon, 128x128) are each 0xFE7F7F7F.
    // A census of the settled frame of every other page in the same sweep
    // (select-level, options-root, options-video, mission-briefing,
    // choose-game-name, select-configuration) finds this class of draw at
    // 0xFE7F7F7F or 0xFF7F7F7F and NEVER at 0xFFFFFFFF, so the correction is
    // toward retail on the pages that share the constant too. The rendered
    // delta is 1/255 per channel; the point is that the byte is now measured.
    private static readonly Color BracketTint = RetailColor(0xfe7f7f7f);
    // 0x3e7f7f7f, CORRECTED 2026-07-28 from 0x3ecfffff. Same class of error and
    // worse: 0xcf was a *hue* that MODULATE2X clamps straight back to 255, so
    // the constant claimed a warm tint that could never reach a pixel. Retail's
    // byte is 0x3E7F7F7F, shared bit-for-bit by both element groups this tints:
    // the three Forseti chrome strips (frame 3000 draws 3, 4 and 5, 128x512
    // DXT2) and the two language chevrons (draws 7 and 9, 64x64 DXT2).
    // Alpha 0x3E was already exact.
    private static readonly Color ChromeTint = RetailColor(0x3e7f7f7f);
    // Language-selector flag tint; see DrawLanguageSelector for the measurement.
    // Alpha 0xfd, CORRECTED 2026-07-28 from 0xff: frame 3000 draw 6 is
    // 0xFD3F3F3F. Same alpha-byte class as the row-label correction at :75-77.
    private static readonly Color FlagTint = RetailColor(0xfd3f3f3f);
    private static readonly Color ShadowTint = RetailColor(0x3e000000);
    private static readonly Color VersionTint = RetailColor(0xff102025);
    // Released overlay format is CFEPMain::Render 0x0046416E:
    // push 0x00629454 "V%1d.%02d". Image-initial major 0x00629410 is 1;
    // minor 0x00679980 is BSS 0. RetailMainMenuVersionOverlay owns the
    // sprintf. The prior value here was "V1.00 - PATCHED", transcribed
    // from a reference capture taken on a safe copy whose
    // version_overlay_* patches repoint the format pointer at 0x0046416f
    // to a code cave at VA 0x005AA444 holding "V%1d.%02d - PATCHED".
    // That suffix is an artifact of the patched capture, not released
    // behavior. Colour at 0x004641B1/B4 is fade<<24 | 0x00102025 =
    // 0xFF102025 settled, which is this VersionTint, so the draw keeps
    // VersionTint and does not call SubmittedColor.

    // FEP_DEVSELECT ("CHOOSE GAME NAME"). See DrawDevSelect for the measurement
    // method; every literal below is either a measured extent from the pristine
    // 640x480 capture or a constant lifted from references/Onslaught/FrontEnd.cpp.
    private const string DevSelectTitle = "CHOOSE GAME NAME";
    // Retail's title occupies x263..513 (251px wide) and y73..88 (16px tall) for
    // 16 glyphs. Font13PS at scale 1 rendered 256px wide but only 10px tall in
    // the first capture of this page, so the page is NOT tracked-out small text:
    // it is the same atlas at ~1.5x, which lands 249px wide and 15px tall.
    // The header title is drawn in font22 at scale 1 on this page as on every
    // other header page; see DrawDevSelect for the glyph-run measurement that
    // replaced the previous Font13PS-at-1.5 reading. HeaderBarCenterX /
    // HeaderTitleTop now carry the placement, so no page-local title constants
    // remain.
    // Rows and the name field are drawn larger than the 20px-pitch main menu:
    // retail's row pitch here is 24 and its glyph bodies are ~14px tall. The
    // first capture measured our "BEA 1" at 53x12 against retail's 69x17, i.e.
    // 1.30-1.42x too small at scale 1.25.
    private const float DevSelectRowScale = 1.4f;
    private const float DevSelectRowPitch = 24f;
    private const float DevSelectRowX = 132f;
    private const float DevSelectRowTop = 137f;
    private const float DevSelectNameTop = 417f;

    // FEP_LEVEL_SELECT ("SELECT LEVEL"). Every literal below is measured from the
    // pristine 640x480 capture; see DrawLevelSelect for the method and the gaps.
    // FrontEndText token — english.json "selectLevel" already carries the exact
    // released string, so the title is drawn from localization (_selectLevelText).
    // "Episode 1" has no resolved string id in the materialized table; it is
    // transcribed from the pristine capture and is the one literal on this page
    // that is not localization-backed.
    private const string LevelSelectEpisodeText = "Episode 1";
    private const float LevelSelectBodyScale = 1.4f;
    private const float LevelSelectEpisodeLeft = 130f;
    private const float LevelSelectEpisodeTop = 130.2f;
    private const float LevelSelectLevelNameTop = 156.8f;
    private const float LevelSelectColumnLabelScale = 1.5f;
    private const float LevelSelectColumnLabelTop = 182.5f;
    // Node graph. Column pitch 60, three rows, all measured (DrawLevelSelect).
    private const float NodeColumnPitch = 60f;
    private const float NodeRowMiddleY = 320f;
    private const float NodeRowTopY = 290f;
    private const float NodeRowBottomY = 350f;
    private const float NodeRingSize = 61f;
    private const float NodeOuterRadius = 21f;
    private const float CurrentNodeRingSize = 80f;
    private const float CurrentNodeInnerRingSize = 62f;
    private const float CurrentNodeOuterRadius = 27f;
    private const float NodeLinkWidth = 1.6f;
    // Episode sweep arcs: circle fitted to 14 measured centre points, residuals
    // under 1.1px (see DrawLevelSelect).
    private const float SweepArcCenterX = 365.84f;
    private const float SweepArcCenterY = 320.38f;
    private const float SweepArcRadius = 249.28f;
    private const float SweepArcStartAngle = 2.5423f;
    private const float SweepArcEndAngle = 3.7253f;
    private const float SweepArcWidth = 1.6f;

    /// <summary>Node centres, in draw order. Index 0 is the current node.</summary>
    private static readonly Vector2[] LevelNodes =
    [
        new(148f, NodeRowMiddleY),
        new(208f, NodeRowMiddleY),
        new(268f, NodeRowMiddleY),
        new(328f, NodeRowTopY),
        new(328f, NodeRowBottomY),
        new(388f, NodeRowTopY),
        new(388f, NodeRowBottomY),
        new(448f, NodeRowTopY),
        new(448f, NodeRowBottomY),
        new(508f, NodeRowMiddleY),
        new(568f, NodeRowTopY),
        new(568f, NodeRowBottomY),
    ];

    /// <summary>Index pairs into <see cref="LevelNodes"/>.</summary>
    private static readonly (int From, int To)[] LevelNodeLinks =
    [
        (0, 1), (1, 2),
        (2, 3), (2, 4),
        (3, 5), (4, 6), (3, 6), (4, 5),
        (5, 7), (6, 8), (5, 8), (6, 7),
        (7, 9), (8, 9),
        (9, 10), (9, 11),
    ];

    /// <summary>Column label text and its measured left edge.</summary>
    private static readonly (string Text, float X)[] LevelColumnLabels =
    [
        ("1", 163f), ("2", 283f), ("3", 524f),
    ];

    /// <summary>Sweep-arc circle-centre X for each drawn arc.</summary>
    private static readonly float[] SweepArcCenters =
    [
        SweepArcCenterX, SweepArcCenterX + 120f, SweepArcCenterX + 360f,
    ];

    // MISSION BRIEFING / SELECT CONFIGURATION. Every literal in this block is
    // measured from the two pristine 640x480 captures taken 2026-07-25:
    //   local-lab/retail-reference-pristine/mission-briefing/05-mission-briefing-640x480.png
    //   local-lab/retail-reference-pristine/select-configuration/06-select-configuration-640x480.png
    // See DrawMissionBriefing / DrawSelectConfiguration for the method per element.
    private const string MissionBriefingTitle = "MISSION BRIEFING";
    private const string SelectConfigurationTitle = "SELECT CONFIGURATION";
    private const string DebriefingTitle = "DEBRIEFING";
    private const float HeaderBarCenterX = 390f;
    private const float HeaderTitleTop = 65f;
    private const float BriefingLevelNameLeft = 178.5f;
    private const float BriefingLevelNameTop = 118f;
    // The level name is the only text on either page drawn non-uniformly: the
    // free sx/sy fit peaks at sx 0.70 / sy 1.00 (score 0.808), and both a
    // uniform font22 and every Font13PS variant score below 0.48.
    private const float BriefingLevelNameScaleX = 0.70f;
    private const float BriefingLevelNameScaleY = 1.00f;
    private const float BriefingBodyLeft = 80f;
    private const float BriefingBodyTop = 163.5f;
    private const float BriefingBodyPitch = 16f;

    /// <summary>
    /// The widest briefing line retail drew: the pristine capture's longest
    /// line carries 286px of this renderer's advances (283px measured ink,
    /// within the known +2..+6 overshoot). WrapBriefingParagraphs breaks a
    /// candidate line only when it would exceed this.
    /// </summary>
    private const float BriefingBodyInkCeiling = 286f;
    // Retail's blank line does NOT advance a full 16: measured ink tops run
    // 167,183,199,215,231,247 then 273,289, so the paragraph break adds 10 on
    // top of the line the last paragraph line already advanced (247+16+10=273).
    private const float BriefingParagraphGap = 10f;
    private const float ConfigurationUnitLeft = 260.5f;
    private const float ConfigurationUnitTop = 99.5f;
    private const float ConfigurationRowLeft = 280f;
    private const float ConfigurationRowPitch = 16f;
    private const float ConfigurationWalkerTop = 210f;
    private const float ConfigurationJetTop = 274f;
    // The rock background quad and the big ring, both fitted — see DrawBriefingStage.
    private const float BriefingBackgroundScale = 1.25f;
    private const float BriefingBackgroundLeft = -70f;
    private const float BriefingBackgroundTop = -80f;
    private const float BriefingRingSize = 990f;
    private const float BriefingRingCenterX = 267f;
    private const float BriefingRingCenterY = 221f;
    // CFEPDebriefing::Render settled positions, pristine 0x00456DD0.
    private const float DebriefingTextLeft = 130f;
    private const float DebriefingLevelTop = 149f;
    private const float DebriefingMissionStatusTop = 184f;
    private const float DebriefingPrimaryTop = 210f;
    private const float DebriefingSecondaryAfterPrimaryTop = 226f;
    private const float DebriefingValueGap = 20f;
    private const float DebriefingGradeLabelTop = 295f;
    private const float DebriefingGradeCenterX = 320f;
    private const float DebriefingGradeCenterY = 310f;
    private const float DebriefingRingCenterX = 285f;
    private const float DebriefingRingCenterY = 225f;
    private const float DebriefingRingScale = 1.6f;
    // Loading page. Bar extents and text origin measured from
    // local-lab/retail-reference-pristine/loading/07-loading-640x480.png.
    private const float LoadingTextLeft = 270f;
    private const float LoadingTextTop = 393.5f;
    private const float LoadingBarLeft = 78f;
    private const float LoadingBarTop = 423f;
    private const float LoadingBarWidth = 485f;
    private const float LoadingBarHeight = 25f;

    /// <summary>
    /// The briefing body, transcribed from the pristine capture. The
    /// localization backing for it WAS later found — english.dat carries the
    /// full sentences (pool slots after 0x015F8838/0x01625641) — so this
    /// literal is now only the world-100 receipt used when no session body
    /// is available, and the wrap-law test target: WrapBriefingParagraphs
    /// must reproduce these breaks from the table text.
    ///
    /// The transcription is corroborated, not assumed: summing this renderer's
    /// per-glyph advances for each line against the measured retail ink widths
    /// gives 286/283, 265/262, 285/283, 251/249, 281/279, 111/107, 271/268 and
    /// 237/231 — every line within the known +2..+6 advance overshoot and none
    /// outside it, which a mis-transcribed line would not be.
    /// </summary>
    private static readonly string[] BriefingBody =
    [
        "Tatiana will take you through the",
        "basics of piloting Battle Engine",
        "Aquila. This will cover everything",
        "from basic movement in both",
        "Walker and Jet modes as well as",
        "Weapons use.",
        "",
        "Listen to her advice and try to",
        "keep Colonel Kramer happy.",
    ];

    private static readonly Color ReleasedTitleText = RetailColor(0xff7f7f7f);
    // Briefing body ink measures (251,220,95) at its brightest; this modulate
    // renders (252,222,95).
    private static readonly Color BriefingBodyText = RetailColor(0xff7e6f30);
    // SELECT CONFIGURATION mode headers measure (249,217,62); this renders
    // (247,215,61). It is a different amber from the briefing body.
    private static readonly Color ConfigurationModeText = RetailColor(0xff7c6c1f);
    private static readonly Color DebriefingLabelText = RetailColor(0xffffaf3f);
    private static readonly Color DebriefingSuccessText = RetailColor(0xff3fff2f);
    private static readonly Color DebriefingFailureText = RetailColor(0xffff3f1f);
    private static readonly Color DebriefingAbortedText = RetailColor(0xff3f3f3f);
    private static readonly Color DebriefingGradeBodyTint = RetailColor(0xfeffffff);
    // The header box is the same FET3_HEADER_TEXT_BOX 0x7f000000 overlay the
    // menu pages use, but these two pages sit over a textured background rather
    // than the flat (23,23,48) fill, so the measured composite constant
    // HeaderBoxTint cannot be reused. Measured inside/outside luminance ratio
    // across the box band is 0.577 (briefing) and 0.478 (configuration) —
    // straddling the 0.5 that alpha 0x7f black predicts.
    private static readonly Color HeaderBoxOverlay = new(0f, 0f, 0f, 0.5f);
    // FE_Rock_Background is drawn through a colour modulate, not at full
    // brightness: a per-channel least-squares fit of retail against the drawn
    // texture over a clean scene band (x150..360, y350..430) gives gains
    // 0.706 / 0.710 / 0.957 with offsets under 3.3, i.e. a pure multiply. This
    // packed value renders 0.702 / 0.702 / 0.953.
    private static readonly Color BriefingBackgroundTint = RetailColor(0xff5a5a7a);
    // The big ring's modulate, fitted where its alpha is fully opaque (22,665
    // pixels): the decoded texel mean is (81,105,136) and retail's mean over
    // exactly those pixels is (101,100,105) on the briefing frame and
    // (103,102,107) on the configuration frame, i.e. per-channel gains of
    // 1.26 / 0.96 / 0.78. The mean of the two frames is used.
    //
    // The RED GAIN IS ABOVE 1, and that is itself a finding rather than a fudge:
    // no value of RetailColor can produce it, because Modulate2X saturates at
    // 1.0. Retail is evidently applying its 2x modulate to the TEXTURE stage as
    // well as to the diffuse colour, which this renderer does not model. The
    // measured gains are therefore stated directly as a Godot modulate (Godot
    // permits components above 1 and clamps at output). Every other bracket
    // draw in this file is a candidate for the same correction; none is changed
    // here because their captures are pinned baselines for this change.
    private static readonly Color BriefingRingTint = new(1.257f, 0.960f, 0.777f, 1f);
    private static readonly Color DevSelectRowText = RetailColor(0xff404040);
    private static readonly Color DevSelectNameHighlight = RetailColor(0xff004050);
    // Panel fills and hairlines are measured framebuffer colours, not modulated
    // sprite tints, so they are stated literally.
    private static readonly Color DevSelectPanelFill = new(9f / 255f, 9f / 255f, 18f / 255f, 1f);
    private static readonly Color DevSelectPanelBorder = new(130f / 255f, 132f / 255f, 139f / 255f, 1f);
    private static readonly Color DevSelectFieldBorder = new(159f / 255f, 162f / 255f, 165f / 255f, 1f);
    private static readonly Color DevSelectScrollDivider = new(127f / 255f, 129f / 255f, 132f / 255f, 1f);
    private static readonly Color DevSelectScrollThumb = new(245f / 255f, 249f / 255f, 245f / 255f, 1f);
    private static readonly Color DevSelectGuide = new(50f / 255f, 51f / 255f, 72f / 255f, 1f);
    // Node-graph link lines and the episode sweep arcs are measured framebuffer
    // colours at the line core, stated literally for the same reason the panel
    // fills are: they are not modulated sprite tints this lane can reproduce.
    //
    // The three sweep arcs are NOT drawn at one brightness. Sampling each arc's
    // predicted core every 20 rows from y=200 to y=460 gives a peak of 141±13 for
    // the leftmost arc and 61±4 for the other two — the current episode's divider
    // is drawn bright and the rest dim, at almost exactly one third the delta
    // over the page background.
    private static readonly Color LevelLinkLine = new(49f / 255f, 50f / 255f, 71f / 255f, 1f);
    private static readonly Color LevelSweepArcCurrent = new(142f / 255f, 142f / 255f, 167f / 255f, 1f);
    private static readonly Color LevelSweepArcOther = new(61f / 255f, 61f / 255f, 86f / 255f, 1f);
    // Unvisited node rings are FE_select_level_ring_bracket01 drawn very dim: the
    // ring peak measures (33,35,62) over the (23,23,48) page background, and the
    // texture's brightest opaque texel is (123,146,189), which is alpha 0.102 -
    // 0x1a. The blue channel is what identifies the texture: the measured ring
    // delta is (10,12,14), whose 1.4 blue/red ratio matches ring_bracket01's 1.49
    // and not ring_bracket02's 1.00.
    private static readonly Color LevelNodeRingTint = RetailColor(0x1cffffff);
    // The guide line survives BEHIND the list panel: retail's y=180 row inside the
    // panel measures (19,19,27) rather than the (9,9,18) panel fill, which is the
    // guide colour attenuated by the panel's own alpha. Stating the measured
    // composite avoids depending on blend rounding to reproduce it.
    private static readonly Color DevSelectGuideOverPanel = new(19f / 255f, 19f / 255f, 27f / 255f, 1f);
    // FrontEnd.cpp:1127 sets col = 0x7f000000; line 1134 draws
    // FET3_HEADER_TEXT_BOX, and the
    // measured header interior is (12,12,24) — exactly that alpha over the
    // (23,23,48) page background. Drawn as the measured composite because an
    // alpha 0x7f fill lands on (11,11,24) after this renderer's blend rounding.
    private static readonly Color HeaderBoxTint = new(12f / 255f, 12f / 255f, 24f / 255f, 1f);
    private const float ShadowScaleBoost = 1.05f;
    // Materialized decode of data/video/FEBack128.vid (128² BIKi → rgb24) at the
    // SHIPPED 30 fps × 572 frames. The old 15 fps / 286-frame strip dropped every
    // second frame; that was harmless while nothing drew it and is not harmless
    // now, because a half-rate strip is at the wrong phase at every instant.
    private const string FeBackStripPath =
        "res://Assets/Frontend/Backgrounds/fe-back-128x128x30.rgb";
    private const int FeBackWidth = 128;
    private const int FeBackHeight = 128;
    private const int FeBackFps = 30;
    private const int FeBackFrameBytes = FeBackWidth * FeBackHeight * 3;
    // Additive gain of the FEBack128 underlay over the flat page fill, MEASURED
    // per channel by least squares against retail captured WITHOUT -skipfmv.
    //
    // Reference: local-lab/retail-reference-pristine/nofmv-frontend-2026-07-26/run1,
    // main-menu frames mm-t001020ms .. mm-t007027ms (13 settled frames), scored on
    // the 222,683 pixels the -skipfmv main-menu capture proves are pure underlay
    // (exactly (23,23,48) within 1 — a geometric mask, not a rectangle).
    //
    // Per-frame fits are stable: R 0.2523..0.2687, G 0.2456..0.2674,
    // B 0.2290..0.2467. The competing alpha-mix model (y = (1-a)·bg + a·frame)
    // fits these pixels WORSE: residual rms 4.6..11.9 against additive's 2.4..5.7.
    //
    // Its fitted `a` also wanders 0.29..0.57 across frames where the additive gain
    // does not move. That is recorded but is NOT independent evidence, and an
    // earlier draft of this comment claimed it was. An independent adversarial
    // pass pointed out the circularity: if the truth is y = bg + g·F then forcing
    // an alpha mix gives a = g·F/(F - bg), which MUST vary with frame content.
    // The residual rms
    // is what carries this conclusion.
    //
    // What is established is narrower than "the compositor is additive": it is
    // that over a flat destination these pixels are described by bg + g·frame.
    // A D3D SRCALPHA/ONE draw of a modulated frame reduces to exactly that form,
    // and this measurement cannot separate the two.
    //
    // 0.2471 = (0x7e/255) × 0.5 — the frontend's own 0x7e7e7e modulate at alpha
    // 0.5 under D3D SRCALPHA/ONE — lands inside the measured band on all three
    // channels. That is a plausible generator and is NOT asserted here: the
    // constants below are the measurements, not the theory.
    private static readonly float[] FeBackUnderlayGain = [0.2610f, 0.2590f, 0.2420f];

    /// <summary>See <see cref="FeBackFrameIndex"/> - measured, over a full loop.</summary>
    private const int FeBackPhaseFrames = 3;
    // ---- THE PAGE FILL IS A TWO-STEP COMPOSITE, NOT A COLOUR ----
    //
    // MEASURED 2026-07-27 (local-lab/D3D9-FULL-SWEEP-2026-07-27.md section 5.3;
    // <maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\page-index.csv and
    // inventories\main-menu-settled.csv frame 3000 draw 0):
    //
    //   1. Clear(0x001F1F3F) = RGB(31,31,63). page-index.csv records this Clear
    //      colour on EVERY frontend page from frame ~46 onward; only the boot
    //      first page and the loading page clear to 0x00000000.
    //   2. Draw 0 of the page is a full-screen DrawPrimitiveUP TRIFAN spanning
    //      (-40,-3)-(680,483) from a 128x128 DXT2, diffuse 0x3E000000, blend
    //      SRCALPHA/INVSRCALPHA. Its stage-0 COLOROP is MODULATE(TEXTURE,
    //      DIFFUSE) with a BLACK diffuse RGB, so it contributes no colour at all
    //      whatever its texture holds, and it is the only draw on the page whose
    //      stage-0 ALPHAOP is DISABLE, so its alpha is the diffuse alpha
    //      0x3E/255 = 0.24314 flat.
    //
    // The arithmetic closes exactly: 31 x (1 - 0.24314) = 23.46 -> 23 and
    // 63 x 0.75686 = 47.68 -> 48, and the retail pixel at f000800.png(5,470)
    // reads exactly (23,23,48).
    //
    // We used to draw the (23,23,48) ANSWER as one opaque rect. That is right
    // today and wrong in principle: it bakes a result whose two inputs are
    // separately measured, so it silently stops tracking if either moves — and
    // one of them (the FEBack128 underlay this page composites next) is known to
    // be absent under -skipfmv, which is the only condition the fill has ever
    // been measured under.
    private static readonly Color FrontendClearColor = new(31f / 255f, 31f / 255f, 63f / 255f, 1f);
    private static readonly Color FrontendFillDarkener = new(0f, 0f, 0f, 0x3Eu / 255f);

    // The composed result of the two terms above. It is NOT drawn; it is the
    // base the FEBack128 strip is baked against (see BuildFeBackFrames), where a
    // single already-composited colour is what the algebra needs.
    private static readonly Color MainUnderlayFallback = new(23f / 255f, 23f / 255f, 48f / 255f, 1f);

    // ================= THE RELEASED PAGE-TRANSITION MACHINE =================
    //
    // Ported from the pinned GPL drop:
    //
    //   references/Onslaught/FrontEnd.cpp:563-592  CFrontEnd::SetPage(page, time)
    //       time == 0 goes straight there; time > 0 sets mTransitionCount = 0,
    //       mTransitionTime = time and parks mActivePage at FEP_TRANSITION.
    //   references/Onslaught/FrontEnd.cpp:665-675  CFrontEnd::Process()
    //       mTransitionCount++ ONCE per Process, and the destination page becomes
    //       active on the Process where mTransitionCount == mTransitionTime.
    //   references/Onslaught/FrontEnd.cpp:1291     CFrontEnd::Render()
    //       trans = float(mTransitionCount) / float(mTransitionTime).
    //   references/Onslaught/FrontEnd.cpp:1431-1438 CFrontEnd::Run()
    //       exactly one Process per rendered frame;
    //   references/Onslaught/FrontEnd.cpp:1261     Render() refuses to draw until
    //       1/60 s has passed, so a transition length is a FRAME COUNT and one
    //       frame is AT LEAST 1/60 s. Lengths are never stored as milliseconds:
    //       a wall-clock length would make the reveal host-dependent.
    //
    // Frontend.h:97 `#define MAINTIME 70` is NOT this length. It has exactly one
    // caller in the drop — FEPGoodies.cpp:1461, the Goodies BACK edge — and
    // FEPIntro.cpp is absent. The cold-start length came from the shipped bytes
    // instead; see MainMenuEntryTransitionFrames.
    //
    // NOT IMPLEMENTED, and why. FrontEnd.cpp:1291-1334 renders BOTH pages during
    // a transition (higher page ordinal first), the outgoing one at 1 - trans.
    // On this edge the outgoing page is the click page, and retail's own first
    // main-menu frame REFUTES a visible outgoing draw: at t = 14 ms
    // (run1/mm-t000014ms.png) the frame is the flat fill plus the title logo plus
    // the crosshair guides and NOTHING else, while 1 - trans = 0.98 there. The
    // ordinals are known for this edge anyway — SetPage is called with page 0 and
    // CFEPMain__Render tests dest == 0x0c, so to = 0 and from = 12, meaning
    // FrontEnd.cpp:1304 draws the click page FIRST and the main menu over it —
    // so a second draw would have to be visible under the (video-less) main menu
    // and is not. Drawing one would ADD a defect, so this lane keeps the single
    // atomic page swap it already had and records the divergence here.
    //
    /// <summary>
    /// Length of the cold-start click-to-start -> FEP_MAIN transition, in
    /// frontend frames.
    ///
    /// RECOVERED FROM THE SHIPPED BYTES, not from the drop. Read out of
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
    /// (sha256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750;
    /// the installed Steam BEA.exe is deliberately patched and is not a valid
    /// specimen). At VA 0x0051B660 / file 0x11B660, the click page's action
    /// handler:
    /// <code>
    ///   [retail instruction listing removed]
    ///   0051b686  6a 32             push  0x32                  ; time  = 50
    ///   0051b690  6a 00             push  0                     ; page  = 0 = FEP_MAIN
    ///   [retail instruction listing removed]
    ///   [retail instruction listing removed]
    /// </code>
    /// So the released cold-start reveal is 50 frames, and FEP_MAIN's ordinal is 0.
    ///
    /// <para><b>The rate is the unresolved part, and it is not papered over.</b>
    /// 50 frames at the source's own 1/60 s gate settles at 817 ms. Retail's burst
    /// is settled at t = 1020 ms and NOT settled at t = 812 ms (menu-column peak
    /// channel 155 against 253 settled), so retail's realised reveal is somewhat
    /// slower than 50/60 s. FrontEnd.cpp:1261 is a floor, not a target — a frame
    /// whose work or whose <c>PLATFORM.Flip()</c> exceeds 1/60 s simply takes
    /// longer, and FrontEnd.cpp:1437 <c>while(!Render());</c> also burns whole
    /// frames without ticking the count whenever RenderStart fails, which is
    /// exactly what an async-loading video does at page entry. The frame count is
    /// evidenced; the realised frontend frame rate is NOT, and no rate constant is
    /// fitted here to close the ~15 % gap.</para>
    /// </summary>
    private const int MainMenuEntryTransitionFrames = 50;

    // WHICH BRANCH THE MAIN MENU TAKES ON THIS EDGE, and why there is no `dest`
    // variable for it. `dest` is the OTHER page of the transition, as passed at
    // FrontEnd.cpp:1299-1305 — the parameter is named for the destination but each
    // page receives its counterpart. CFEPMain__Render (0x00462D40) branches on
    // `dest == 0x0c` at five sites verified in the pristine specimen — 0x004636B2,
    // 0x00463920, 0x00463B44, 0x00463DCC and 0x0046423D, all `83 fb 0c cmp ebx,0Ch`
    // — and the page we arrive from is the click page, ordinal 12. This lane can
    // therefore only ever be on the 0x0c side, so the constant is folded into the
    // ported laws rather than carried as state. See MainMenuLeftDecor for how that
    // was settled against pixels instead of assumed.

    private int _mainTransitionCount;
    private int _mainTransitionTime;

    private RetailFrontendSession _session = new();
    private readonly Dictionary<RetailFrontendMenuItemKind, string> _menuText = [];

    private Texture2D _clickBackground = null!;
    private Texture2D _clickSlide = null!;
    private Texture2D _rockBackground = null!;
    private Texture2D[] _feBackFrames = [];
    private Texture2D _forsetiWritingLarge = null!;
    private Texture2D _titleLogo = null!;
    private Texture2D _reflectionMap = null!;
    private TitleLogoReflectionLayer? _titleLogoReflection;
    private Texture2D _titleBracket01 = null!;
    private Texture2D _titleBracket02 = null!;
    private Texture2D _titleTextBox = null!;
    private Texture2D _feBlank = null!;
    private Texture2D _symbolBracket01 = null!;
    private Texture2D _symbolBracket02 = null!;
    private Texture2D _levelBracket01 = null!;
    private Texture2D _levelBracket02 = null!;
    private Texture2D _levelRing01 = null!;
    private Texture2D _levelRing02 = null!;
    private Texture2D _debriefingMetalRing = null!;
    private Texture2D[] _debriefingGradeTextures = [];
    private Texture2D _loadingScreen = null!;
    private Texture2D _titleFont = null!;
    private Texture2D _font22 = null!;
    private int[] _font22Widths = [];
    private Texture2D[] _languageFlags = [];
    private Texture2D _feArrow = null!;
    private Texture2D[] _menuIcons = [];
    private int[] _glyphWidths = [];
    private string _selectLevelText = string.Empty;
    // The level-select name band and the briefing page draw the SELECTED
    // node's row through _session.SelectedLevelName; this field stays as the
    // localization-table load receipt (english.json "level100") and feeds
    // nothing on its own.
    private string _level100Text = string.Empty;
    private string _loadingText = string.Empty;
    // Localization__GetStringById(0xe4) — English table in BEA.exe, not english.dat.
    private const string QuitConfirmPrompt = "Are you sure you want to quit the game?";
    private double _animationSeconds;
    private double _clickPulseTimer;
    private double _clickPageSeconds;
    // Seconds since the frontend left click-to-start, which is MEASURED to be
    // FEBack128's phase origin. See FeBackFrameIndex for the phase itself and for
    // the alias that nearly got this wrong.
    private double _feBackSeconds;
    private RetailFrontendScreen _lastDrawnScreen = RetailFrontendScreen.ClickToStart;
    private int _loadingFrames;
    private bool _initialized;
    private bool _loadRequestRaised;
    private bool _level100Ready;
    private bool _gameplayActivationRaised;

    public event Action? Level100LoadRequested;

    public event Action? Level100LoadingStarted;

    public event Action? GameplayActivated;

    public event Action? GameplaySuspended;

    public event Action? ReturnToMainMenuRequested;

    public event Action? ExitRequested;

    internal AudioPlaybackRetirement PlaybackRetirement { get; set; } = null!;

    public event Action<RetailCareerDescriptor>? CareerSelected;

    public event Action<RetailFrontendAudioCue>? AudioCueRequested;

    public event Action<RetailFrontendCursorMode>? CursorModeRequested;

    internal RetailFrontendScreen CurrentScreen => _session.Screen;

    public void Initialize(IReadOnlyList<RetailCareerDescriptor> careerDescriptors)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("The retail frontend is already initialized.");
        }

        ArgumentNullException.ThrowIfNull(careerDescriptors);
        _session = new RetailFrontendSession(careerDescriptors);
        LoadLocalization();
        LoadTextures();
        _feBackFrames = LoadFeBackFrames();
        _glyphWidths = MeasureGlyphWidths(_titleFont.GetImage(), GlyphCellSize, GlyphColumns);
        _font22Widths = MeasureGlyphWidths(_font22.GetImage(), Font22CellSize, Font22Columns);
        InitializeOptions();

        _initialized = true;
    }

    public void MarkLevel100Ready()
    {
        if (!_loadRequestRaised || _session.Screen != RetailFrontendScreen.Loading)
        {
            throw new InvalidOperationException(
                "Level 100 was marked ready outside the frontend loading seam.");
        }

        _level100Ready = true;
    }

    public void RestartLevel100()
    {
        RetailFrontendScreen origin = _session.Screen;
        RetailFrontendSignal signal = _session.RestartLevel100();
        ResumeFrontendForNavigation(origin);
        HandleNavigationSignal(signal);
        QueueRedraw();
    }

    public void LeaveLevel100ForMainMenu()
    {
        RetailFrontendScreen origin = _session.Screen;
        RetailFrontendSignal signal = _session.LeaveLevel100ForMainMenu();
        ResumeFrontendForNavigation(origin);
        HandleNavigationSignal(signal);
        QueueRedraw();
    }

    /// <summary>
    /// Post-Won re-entry: apply the FillOut update and show FEP_DEBRIEFING.
    /// The host tears the Level 100 world down through the existing
    /// <see cref="ReturnToMainMenuRequested"/> seam; SELECT LEVEL follows only
    /// after the player acknowledges the debriefing page.
    /// </summary>
    /// <summary>A mission's <c>SetSlotSave</c>, stored on the career at once.</summary>
    public void SaveSlot(Level100TutorialSlotSaved saved) => _session.SaveSlot(saved);

    public void AcceptWonHandoff(
        Level100MissionOutcome outcome,
        Level100MissionTerminalState terminalState,
        IReadOnlyList<int> baseThingsLeft)
    {
        RetailFrontendScreen origin = _session.Screen;
        if (!RetailFrontendScenePath.TryAcceptWonHandoff(_session, outcome, terminalState, baseThingsLeft))
        {
            return;
        }

        ResumeFrontendForNavigation(origin);
        HandleNavigationSignal(RetailFrontendSignal.PageChanged);
        ReturnToMainMenuRequested?.Invoke();
        QueueRedraw();
    }

    internal int LaunchWorldNumber => _session.ConsumeLaunchWorldNumber;

    internal bool SelectedWorldIsConstructible => _session.SelectedWorldIsConstructible;

    /// <summary>
    /// Loading admitted a world this reconstruction cannot build. Return to
    /// SELECT LEVEL without constructing Level 100 in its place.
    /// </summary>
    public void ReturnUnconstructibleLaunchToLevelSelect()
    {
        if (!_session.ReturnUnconstructibleLaunchToLevelSelect())
        {
            throw new InvalidOperationException(
                "An unconstructible launch can return only after its request was consumed.");
        }

        _loadRequestRaised = false;
        _level100Ready = false;
        _loadingFrames = 0;
        QueueRedraw();
    }

    internal void ConfirmForSmoke()
    {
        Confirm();
    }

    internal void SelectMainIndexForCapture(int index)
    {
        _session.SelectMainIndex(index);
        QueueRedraw();
    }

    /// <summary>
    /// Hides and freezes the frontend while retail's cold-start media owns the
    /// screen.
    ///
    /// This is not cosmetic. <see cref="_clickPulseTimer"/> and
    /// <see cref="_clickPageSeconds"/> accumulate in <c>_Process</c> whenever
    /// the session is on click-to-start, and the released splash pulse is
    /// <c>((cos(t*pi)+1)*0.375)+0.46875</c> — a function of that timer. Letting
    /// it run for the ~93 s the intro lasts would put the pulse at an arbitrary
    /// phase on the first frame the player actually sees, where retail's starts
    /// from zero because the page has only just been created.
    /// </summary>
    public void SuspendForStartupMedia()
    {
        Visible = false;
        SetProcess(false);
        SetProcessInput(false);
    }

    /// <summary>
    /// Hands the screen back after the cold-start media finishes or is skipped.
    /// The click-to-start timers are reset so the page begins from zero exactly
    /// as it does when the frontend is created cold.
    /// </summary>
    public void ResumeAfterStartupMedia()
    {
        _animationSeconds = 0d;
        _clickPulseTimer = 0d;
        _clickPageSeconds = 0d;
        Visible = true;
        SetProcess(true);
        SetProcessInput(true);
        QueueRedraw();
        _mouseCursorLayer?.QueueRedraw();
    }

    public override void _Ready()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Initialize the retail frontend before adding it to the tree.");
        }

        AnchorRight = 1f;
        AnchorBottom = 1f;
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;

        // A SEPARATE CanvasItem, because Godot exposes blend mode per item and the
        // previous attempt at this layer set Material around a span of _Draw calls
        // and was therefore never additive at all. See TitleLogoReflectionLayer.
        _titleLogoReflection = new TitleLogoReflectionLayer
        {
            Name = "TitleLogoReflection",
            Visible = false,
        };
        AddChild(_titleLogoReflection);
        _titleLogoReflection.Configure(_titleLogo, _reflectionMap);

        _mouseCursorLayer = new RetailMouseCursorLayer
        {
            Name = "RetailMouseCursor",
            ZIndex = 2,
        };
        _mouseCursorLayer.Configure(this);
        AddChild(_mouseCursorLayer);

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        double step = Math.Max(0d, delta);
        _animationSeconds += step;

        // CFrontEnd::SetPage, FrontEnd.cpp:583-591 — arm the transition on the
        // edge, exactly the edge the click page's 0x2C handler takes. Every OTHER
        // way into FEP_MAIN stays instant: FrontEnd.cpp:228-232 re-enters the
        // frontend with SetPage(FEP_MAIN, 0) on FEE_TITLE_SCREEN, and no other
        // released entry length is evidenced, so none is invented.
        if (_session.Screen == RetailFrontendScreen.MainMenu &&
            _lastDrawnScreen == RetailFrontendScreen.ClickToStart)
        {
            _mainTransitionCount = 0;
            _mainTransitionTime = MainMenuEntryTransitionFrames;
        }

        // CFrontEnd::Process, FrontEnd.cpp:665-675 — one increment per Process,
        // and the page goes active on the frame where count == time. Godot's
        // _Process is this lane's Process, and it stages the frame that _Draw
        // then rasterizes, so the first drawn frame carries count == 1 exactly as
        // FrontEnd.cpp:1433-1437 does.
        if (_mainTransitionTime > 0)
        {
            _mainTransitionCount++;
            if (_mainTransitionCount >= _mainTransitionTime)
            {
                _mainTransitionTime = 0;
            }
        }

        if (_session.Screen == RetailFrontendScreen.ClickToStart)
        {
            if (_lastDrawnScreen != RetailFrontendScreen.ClickToStart)
            {
                _clickPulseTimer = 0d;
                _clickPageSeconds = 0d;
            }

            // CFEPIntro::Process 0x0051B6B0: hold this+0x18 at 0 until
            // GetTime()-[this+4] > 1.0, seed 0x3727C5AC, then add 2*dt.
            // See RetailClickToStartPrompt. The 30 s idle write of -3 to
            // 0x008A956C is deliberately not driven here.
            _clickPageSeconds += step;
            _clickPulseTimer = RetailClickToStartPrompt.Advance(
                _clickPulseTimer,
                _clickPageSeconds,
                step);
        }
        else if (_session.Screen != RetailFrontendScreen.IntroCutscene)
        {
            // Free-runs from the moment click-to-start is left, and is NOT reset on
            // later page changes. Retail's underlay is measured to be page-anchored
            // on the main menu and NOT page-anchored on FEP_DEVSELECT or
            // FEP_LEVEL_SELECT (matched-offset cross-run material 7-44 % and
            // 5.8-63 % there against 0.4-1.5 % on the main menu), which is what a
            // single clock started once at frontend entry produces.
            //
            // IntroCutscene is excluded because retail's frontend page machine is
            // not running at all during RunIntroFMV — that call sits inside
            // CGame::RestartLoopRunLevel, not inside the frontend. Letting this
            // clock run would inject 123.8 s of phase into a MEASURED underlay
            // scroll, which is a lab artefact and not a released behaviour.
            _feBackSeconds += step;
        }

        _lastDrawnScreen = _session.Screen;
        UpdateTitleLogoReflection();

        if (_session.Screen == RetailFrontendScreen.Loading)
        {
            _loadingFrames++;
            if (!_loadRequestRaised && _loadingFrames >= 2)
            {
                if (!_session.ConsumeLevel100LaunchRequest())
                {
                    throw new InvalidOperationException("The Level 100 launch edge was lost.");
                }

                _loadRequestRaised = true;
                Level100LoadRequested?.Invoke();
            }

            if (_level100Ready)
            {
                // Retail runs the level's intro FMV HERE — after the load, with
                // the loading screen driven to 100 % and dismissed, before the
                // first gameplay frame (references/Onslaught/game.cpp:1336-1345).
                // See RetailFrontendFlow.Cutscene.cs. When there is no cutscene
                // to play this falls through to the pre-existing handoff.
                if (!TryBeginLevel100IntroCutscene())
                {
                    if (!RetailFrontendScenePath.TryCompleteLoading(
                            _session,
                            startupMediaActive: false,
                            launchConsumed: _loadRequestRaised))
                    {
                        throw new InvalidOperationException(
                            "Level 100 can complete only after its pending launch request is consumed.");
                    }
                }
            }
        }

        if (TryRaiseGameplayActivation())
        {
            return;
        }

        QueueRedraw();
        _mouseCursorLayer?.QueueRedraw();
    }

    /// <summary>
    /// Hands the screen to gameplay on the frame the session first reaches it.
    ///
    /// Extracted from <c>_Process</c> so the intro-cutscene completion can raise
    /// the SAME edge in the SAME frame. The cutscene finishes inside its own
    /// child node's <c>_Process</c>, which Godot runs after this node's, so
    /// leaving the edge to the next frame put the session on Gameplay for one
    /// frame while nothing had been activated — a window the smoke harness
    /// observed and threw on. It is the ordering artefact, not the state, that
    /// this removes.
    /// </summary>
    private bool TryRaiseGameplayActivation()
    {
        if (_session.Screen != RetailFrontendScreen.Gameplay || _gameplayActivationRaised)
        {
            return false;
        }

        _gameplayActivationRaised = true;
        Visible = false;
        SetProcessInput(false);
        SetProcess(false);
        CursorModeRequested?.Invoke(RetailFrontendCursorMode.Captured);
        GameplayActivated?.Invoke();
        return true;
    }

    /// <summary>
    /// Keeps the additive sheen child on FEP_MAIN's design transform and phase.
    ///
    /// It is hidden on QuitConfirm: this lane draws its messbox INSIDE the parent
    /// _Draw, so a child layer would land on top of the dialog. Retail draws the
    /// messbox as a later page over a finished FEP_MAIN and has no such problem.
    /// That is a Godot ordering concession, recorded rather than papered over.
    /// </summary>
    private void UpdateTitleLogoReflection()
    {
        if (_titleLogoReflection is not TitleLogoReflectionLayer layer)
        {
            return;
        }

        bool visible = _session.Screen == RetailFrontendScreen.MainMenu;
        layer.Visible = visible;
        if (!visible)
        {
            return;
        }

        (float scale, Vector2 offset) = DesignTransform();
        layer.Position = offset;
        layer.Scale = new Vector2(scale, scale);
        layer.SetScroll(_feBackSeconds);
    }

    /// <summary>
    /// <c>trans</c> as FrontEnd.cpp:1291 computes it, and 1.0 once the page is
    /// active (FrontEnd.cpp:1310 renders a settled page with a literal 1.f).
    /// </summary>
    private float MainMenuTransition =>
        _mainTransitionTime <= 0
            ? 1f
            : Math.Min(1f, (float)_mainTransitionCount / _mainTransitionTime);

    public override void _Input(InputEvent inputEvent)
    {
        // IntroCutscene is included because the movie owns the screen and the
        // RetailStartupSequence child owns the abort. A frontend page reacting to
        // the keypress that skips the movie would navigate an invisible page.
        if (_session.Screen is RetailFrontendScreen.Loading or
            RetailFrontendScreen.IntroCutscene or
            RetailFrontendScreen.Gameplay)
        {
            return;
        }

        // FrontEnd.cpp:551-552 — while mActivePage == FEP_TRANSITION the
        // button action is never forwarded to a page. CFEPMain::Render
        // hover is not ButtonPressed: 0x004630AC / 0x004631EF run when
        // transition > 0.9 (fcomp [0x005D8BB0]; test ah,0x41 / jne skip).
        // Motion is therefore allowed through; HandlePointerMotion applies
        // the 0.9 gate. Confirm and HandleKey stay swallowed here.
        if (RetailMainMenuHitTest.SwallowsFrontendInput(
                _mainTransitionTime > 0,
                inputEvent is InputEventMouseMotion))
        {
            return;
        }

        bool handled = inputEvent switch
        {
            InputEventMouseMotion motion => HandlePointerMotion(motion.Position),
            InputEventMouseButton button when
                button.Pressed && button.ButtonIndex == MouseButton.Left =>
                HandlePointerConfirm(button.Position),
            InputEventMouseButton button when
                button.Pressed && button.ButtonIndex == MouseButton.Right =>
                HandlePointerCancel(),
            InputEventKey key when key.Pressed && !key.Echo => HandleKey(key),
            _ => false,
        };

        if (handled)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Draw()
    {
        // Letterbox outside the 640-class stage; no invented navy FE clear.
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
        (float scale, Vector2 offset) = DesignTransform();
        DrawSetTransform(offset, 0f, new Vector2(scale, scale));

        switch (_session.Screen)
        {
            case RetailFrontendScreen.ClickToStart:
                DrawClickToStart();
                break;
            case RetailFrontendScreen.MainMenu:
                DrawMainMenu();
                break;
            case RetailFrontendScreen.QuitConfirm:
                DrawMainMenu();
                DrawQuitConfirm();
                break;
            case RetailFrontendScreen.DevSelect:
                DrawDevSelect();
                break;
            case RetailFrontendScreen.Options:
                DrawOptions();
                break;
            case RetailFrontendScreen.Debriefing:
                DrawDebriefing();
                break;
            case RetailFrontendScreen.LevelSelect:
                DrawLevelSelect();
                break;
            case RetailFrontendScreen.MissionBriefing:
                DrawMissionBriefing();
                break;
            case RetailFrontendScreen.SelectConfiguration:
                DrawSelectConfiguration();
                break;
            case RetailFrontendScreen.Loading:
                DrawLoading();
                break;
        }

        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void DrawClickToStart()
    {
        // CFEPIntro::Render 0x0051B866 splash dest — 0x0089d880 / fe_splash1.
        // Scale is min(this+0x18, 1.0) then the stored pulse; dest is the
        // specimen affine, not a scale-free (320, 240) centre.
        float splashScale = RetailClickToStartSplash.Scale(_clickPulseTimer);
        DrawSurfaceCentered(
            _clickBackground,
            RetailClickToStartSplash.X(_clickPulseTimer),
            RetailClickToStartSplash.Y(_clickPulseTimer),
            splashScale,
            splashScale,
            Colors.White);

        // CFEPIntro::Render 0x0051B92F glyph submits — Localization 0x77,
        // five CDXFont__DrawTextScaled calls at Y 401/399/400, sx=sy=1.
        // A capture-derived textScale=2 is not the body. ShouldDraw is the
        // same timer>4 / fmod<2 arm as RetailClickToStartPrompt.
        if (RetailClickToStartGlyphs.ShouldDraw(_clickPulseTimer))
        {
            const string prompt = "Click to start"; // Localization 0x77
            int width = (int)MeasureText(prompt, RetailClickToStartGlyphs.ScaleX);
            foreach (RetailClickToStartGlyphs.Pass pass in RetailClickToStartGlyphs.Passes)
            {
                DrawTextFlat(
                    prompt,
                    new Vector2(RetailClickToStartGlyphs.X(pass, width), pass.Y),
                    RetailClickToStartGlyphs.ScaleX,
                    RetailColor(pass.Color));
            }
        }

        // 0x0089d7bc LostToys sliding pair. No skip after the two byte
        // writes: both mode-0 CDXSurf calls issue even when this+0x18 <= 4
        // (the pair sits 400 px off the left edge). Fade is
        // clamp(timer-4, 0, 1); offset is (1-fade)²*400; dest X is
        // settled − offset.
        if (RetailClickToStartSlide.ShouldDraw(_clickPulseTimer))
        {
            foreach (RetailClickToStartSlide.Pass pass in RetailClickToStartSlide.Passes)
            {
                DrawTextureRect(
                    _clickSlide,
                    new Rect2(
                        RetailClickToStartSlide.X(pass, _clickPulseTimer),
                        pass.Y,
                        _clickSlide.GetWidth(),
                        _clickSlide.GetHeight()),
                    false,
                    RetailColor(pass.Color));
            }
        }

        // CFEPIntro::Render 0x0051BBA0 title slam — 0x0089d88c / FE_BEA_Title2.
        // Gate is page*1.2 > 2; scale slams 2.5→0.5; four z=0.05 outline
        // corners then the z=0.04 body. The previous 0.35 / sin(page*3) stub
        // is not the specimen law.
        if (RetailClickToStartTitle.ShouldDraw(_clickPageSeconds))
        {
            float titleScale = RetailClickToStartTitle.Scale(_clickPageSeconds);
            uint outline = RetailClickToStartTitle.OutlineColor(_clickPageSeconds);
            uint body = RetailClickToStartTitle.BodyColor(_clickPageSeconds);
            foreach (RetailClickToStartTitle.Pass pass in RetailClickToStartTitle.Passes)
            {
                DrawSurfaceCentered(
                    _titleLogo,
                    pass.X,
                    pass.Y,
                    titleScale,
                    titleScale,
                    RetailColor(pass.Outline ? outline : body));
            }
        }

        // CFEPIntro::Render 0x0051BD01 sixth z=0.02 copy. Second gate:
        // 2 < page < 2.25, not page*1.2 > 2. Dest (250, 290), sx=sy=1-v.
        // Not folded into Passes.
        if (RetailClickToStartTitle.ShouldDrawSixth(_clickPageSeconds))
        {
            float sixthScale = RetailClickToStartTitle.SixthScale(_clickPageSeconds);
            DrawSurfaceCentered(
                _titleLogo,
                RetailClickToStartTitle.SixthPass.X,
                RetailClickToStartTitle.SixthPass.Y,
                sixthScale,
                sixthScale,
                RetailColor(RetailClickToStartTitle.SixthColor(_clickPageSeconds)));
        }
    }

    /// <summary>The released clamp idiom, <c>_DAT_005d856c</c> / <c>_DAT_005d8568</c>.</summary>
    private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

    /// <summary>
    /// <c>RangeTransition(t, lo, hi)</c> — the drop's normalise-and-clamp helper.
    ///
    /// Its body is NOT in the drop: Frontend.h:292 includes TransitionHelpers.h,
    /// which is one of the 200 absent headers, and no other file defines it. That
    /// it CLAMPS rather than extrapolating is settled from three independent uses
    /// that are only correct under clamping:
    /// FrontEnd.cpp:856-866 writes <c>alpha = 0</c> for transition &lt; 0.2 and
    /// then immediately overwrites it with <c>MakeAlpha(RangeTransition(t,0.2,0.5))</c>
    /// through a second non-else <c>if</c> — harmless dead code under clamping,
    /// a negative alpha without it; FrontEnd.cpp:1039 uses
    /// <c>MakeAlpha(RangeTransition(t,0,0.5))</c> as an alpha for all t up to 1;
    /// and FEPGoodies.cpp:1849 does <c>SINT(RangeTransition(t,0.75,1)*255)</c> for
    /// t from 0. The shipped main menu computes the same shape inline with an
    /// explicit clamp at every site (0x00462D40), which corroborates it.
    /// </summary>
    private static float RangeTransition(float value, float low, float high) =>
        Clamp01((value - low) / (high - low));

    /// <summary>
    /// <c>MakeAlpha(t)</c> — also absent from the drop; the shipped inline form is
    /// <c>ROUND(clamp(t) * 255.0)</c> clamped to 0..255 (<c>_DAT_005d8c70</c> =
    /// 255.0, verified in the pristine specimen), returned here as 0..1.
    /// </summary>
    private static float MakeAlpha(float value) =>
        Math.Clamp(MathF.Round(Clamp01(value) * 255f), 0f, 255f) / 255f;

    /// <summary>Scale, rotation and alpha of one animated main-menu decoration.</summary>
    private readonly record struct MainMenuDecor(float Scale, float Rotation, float Alpha, bool Draw);

    /// <summary>
    /// Left decoration <c>0x0089d894</c> (title-bracket-01) at (219, 344).
    ///
    /// PORTED FROM THE SHIPPED BYTES, dest == 0x0c branch of CFEPMain__Render
    /// (0x00462D40, pristine BEA.exe.original.backup sha256 74154bfa…). The drop
    /// has no FEPMain.cpp, so this law is not available from source; what the drop
    /// does carry is the same law's SHAPE in CFrontEnd's shared helpers —
    /// FrontEnd.cpp:854-876 for a bordered page whose other side is FEP_MAIN, and
    /// the generic FrontEnd.cpp:881-887 — and the breakpoint/scale idioms match.
    /// The thresholds below are the shipped ones and the float pool addresses are
    /// verified: 0.2 @0x005d8604, 0.4 @0x005d8c40, 0.6 @0x005d8bb8, 5.0 @0x005db564,
    /// 0.25 @0x005d858c, 0.3 @0x005d8cb4.
    ///
    /// <para><b>Why dest == 0x0c and not the generic branch.</b> The generic branch
    /// fades the TITLE LOGO with the page (its alpha is the same clamp as the menu
    /// rows); the 0x0c branch forces the logo to 0xff (0x0046423D). Retail's first
    /// main-menu frame has the logo at ~96 % of its settled value while the menu
    /// column, the language selector and both bracket pairs are still exactly the
    /// flat fill. Only the 0x0c branch can produce that frame.</para>
    /// </summary>
    private static MainMenuDecor MainMenuLeftDecor(float transition)
    {
        float scale = 1.25f;
        float rotation = 0f;
        float alpha = 1f;
        if (transition < 1f)
        {
            if (transition < 0.2f)
            {
                float t = Clamp01(transition * 5f);
                alpha = MakeAlpha(t);
                rotation = -((1f - t) * 0.3f);
                scale = t;
            }
            else if (transition < 0.4f)
            {
                scale = 1f;
            }
            else if (transition < 0.6f)
            {
                scale = (Clamp01((transition - 0.4f) * 5f) * 0.25f) + 1f;
            }
        }

        return new MainMenuDecor(scale, rotation, alpha, Draw: true);
    }

    /// <summary>
    /// Left transition twin <c>0x0089d898</c> (title-bracket-02), same anchor.
    /// Drawn ONLY while transition &lt; 1 and only on the dest == 0x0c side, and
    /// gone from 0.8 onward — the settled main menu has never drawn it, which is
    /// why it is absent from this lane's settled frame today.
    ///
    /// Note the rotation sign flips between the two moving windows in the shipped
    /// code (positive below 0.2, negative in 0.6..0.8). That is reproduced rather
    /// than tidied.
    /// </summary>
    private static MainMenuDecor MainMenuLeftDecorTwin(float transition)
    {
        if (transition >= 1f)
        {
            return default;
        }

        if (transition < 0.2f)
        {
            float t = Clamp01(transition * 5f);
            return new MainMenuDecor(t, (1f - t) * 0.3f, MakeAlpha(t), Draw: true);
        }

        if (transition < 0.6f)
        {
            return new MainMenuDecor(1f, 0f, 1f, Draw: true);
        }

        if (transition < 0.8f)
        {
            float t = 1f - Clamp01((transition - 0.6f) * 5f);
            return new MainMenuDecor(t, -((1f - t) * 0.3f), MakeAlpha(t), Draw: true);
        }

        return default;
    }

    /// <summary>
    /// Right decoration <c>0x0089d8a0</c> (symbol-bracket-01) at (457, 355).
    /// Same dest == 0x0c branch, one breakpoint set earlier than the left pair:
    /// 0.1 @0x005d85c0, 0.3 @0x005d8cb4, 0.5 @0x005d85ec, 0.7 @0x005d8bec.
    /// Below 0.1 the shipped code leaves the scale at 1.25 and zeroes the alpha,
    /// so it is invisible rather than small — reproduced as written.
    /// </summary>
    private static MainMenuDecor MainMenuRightDecor(float transition)
    {
        float scale = 1.25f;
        float rotation = 0f;
        float alpha = 1f;
        if (transition < 1f)
        {
            if (transition < 0.1f)
            {
                alpha = 0f;
            }
            else if (transition < 0.3f)
            {
                float t = Clamp01((transition - 0.1f) * 5f);
                alpha = MakeAlpha(t);
                rotation = -(t * 0.3f);
                scale = t;
            }
            else if (transition < 0.5f)
            {
                scale = 1f;
            }
            else if (transition < 0.7f)
            {
                scale = (Clamp01((transition - 0.5f) * 5f) * 0.25f) + 1f;
            }
        }

        return new MainMenuDecor(scale, rotation, alpha, Draw: alpha > 0f);
    }

    /// <summary>
    /// Right transition twin <c>0x0089d8a4</c> (symbol-bracket-02), same anchor,
    /// drawn only while 0.1 &lt;= transition &lt; 0.9 on the dest == 0x0c side.
    /// </summary>
    private static MainMenuDecor MainMenuRightDecorTwin(float transition)
    {
        if (transition >= 1f || transition < 0.1f)
        {
            return default;
        }

        if (transition < 0.3f)
        {
            float t = Clamp01((transition - 0.1f) * 5f);
            return new MainMenuDecor(t, -(t * 0.3f), MakeAlpha(t), Draw: true);
        }

        if (transition < 0.7f)
        {
            return new MainMenuDecor(1f, 0f, 1f, Draw: true);
        }

        if (transition < 0.9f)
        {
            float t = 1f - Clamp01((transition - 0.7f) * 5f);
            return new MainMenuDecor(t, -(t * 0.3f), MakeAlpha(t), Draw: true);
        }

        return default;
    }

    private void DrawMainMenuDecor(Texture2D texture, Vector2 body, Vector2 shadow, MainMenuDecor decor)
    {
        if (!decor.Draw || decor.Alpha <= 0f)
        {
            return;
        }

        var size = new Vector2(texture.GetWidth(), texture.GetHeight());
        // Shadow first, at scale * 1.05 (_DAT_005db4ac) and the same rotation. Its
        // packed colour is (alpha * 0x3f & 0xff00) << 0x10, i.e. ShadowTint scaled
        // by the decoration's own alpha.
        DrawCenteredRotated(
            texture,
            shadow,
            size * (decor.Scale * ShadowScaleBoost),
            decor.Rotation,
            new Color(ShadowTint.R, ShadowTint.G, ShadowTint.B, ShadowTint.A * decor.Alpha));
        DrawCenteredRotated(
            texture,
            body,
            size * decor.Scale,
            decor.Rotation,
            new Color(BracketTint.R, BracketTint.G, BracketTint.B, BracketTint.A * decor.Alpha));
    }

    private void DrawMainMenu()
    {
        float transition = MainMenuTransition;

        // CFEPMain__Render's page fade, computed once at 0x00462D5x as
        // local_68 = (transition - 0.75) * 4.0 and then clamped at every use:
        // 0.75 @_DAT_005d8bc4, 4.0 @_DAT_005d85bc, both verified in the pristine
        // specimen. It drives the right chrome strips, the language row, every
        // menu label and the version string.
        float fade = Clamp01((transition - 0.75f) * 4f);

        // The selection icon (and, on the literal reading of the same block, the
        // selected row's highlight box) fade on their own later window,
        // clamp((transition - 0.8) * 5): 0.8 @_DAT_005d85f8, 5.0 @_DAT_005db4b8.
        //
        // INFERRED OPERAND, marked because it matters: the highlight box's driver
        // decompiles as an uninitialised stack float rather than as `transition`
        // itself. `(x - 0.8) * 5` is the same expression the icon block uses a few
        // hundred bytes later, so `transition` is the reading taken here, but the
        // operand identity is not proven and both candidates differ only inside
        // 0.75..0.8.
        float iconFade = Clamp01((transition - 0.8f) * 5f);

        DrawMainUnderlay(transition);

        // The faint crosshair guides. FEP_DEVSELECT and FEP_LEVEL_SELECT have drawn
        // these since the font22 work; FEP_MAIN never did, and retail draws them on
        // all three. Counting exact (50,51,72) on the pristine -skipfmv main-menu
        // capture: 618 px, of which 370 are the column x=123 and 248 are the row
        // y=180. Ours held 1.
        //
        // KNOWN RESIDUAL, recorded rather than fudged: retail's guide is NOT an
        // opaque fill, it is a constant ADDITION. At (123,300) the -skipfmv capture
        // reads (50,51,72) over a (23,23,48) fill — delta (+27,+28,+24) — and the
        // no-skipfmv capture reads (64,68,100) over a (39,43,80) underlay at the
        // same pixel — delta (+25,+25,+20). An opaque (50,51,72) is exact on the
        // first and 14 levels low on the second. All three pages draw it opaquely
        // today, so this residual is shared and is not introduced here; closing it
        // needs a per-pixel composite of the guide against the live underlay, which
        // this canvas cannot express as a blend mode. See DrawMainUnderlay.
        //
        // UNGATED, and that is measured rather than assumed: the guides are drawn
        // by the COMMON page (FrontEnd.cpp:1314 renders it with the raw trans and
        // FEP_NONE), not by CFEPMain, and retail's t = 14 ms frame already carries
        // the full-height x = 123 column and the full-width y = 180 row while
        // every CFEPMain element is still absent.
        DrawRect(new Rect2(123f, 0f, 1f, DesignHeight), DevSelectGuide);
        DrawRect(new Rect2(0f, 180f, DesignWidth, 1f), DevSelectGuide);

        // 0x0089D7F0 Forseti writing chrome. Y is CFEPMain::Render 0x00462D46:
        // 175 - fmod(mCounter * 0.3, 350), then +350 / +700. Cold BSS counter
        // is 0, which is the three settled tiles. Colour at 0x00462DE4 is
        // RetailMainMenuWritingColor: settled (255*63)<<16 | 0x00FFFFFF is
        // 0x3EFFFFFF, which is not capture ChromeTint 0x3E7F7F7F, so this
        // draw keeps ChromeTint and does not call SubmittedColor. Z/X at
        // 0x00462DFF is RetailMainMenuWritingZ: three tiles push
        // 0x3F666666 then dest 458. That leftover is Z, not scale, so the
        // draw keeps scale 1.0 and TileX and does not treat the dword as
        // a 29% title-logo. Not a sheen. ChromeTint stays put.
        var chromeTint = new Color(ChromeTint.R, ChromeTint.G, ChromeTint.B, ChromeTint.A * fade);
        float writingCounter = RetailMainMenuWritingScroll.ImageInitialCounter;
        DrawSurfaceCentered(
            _forsetiWritingLarge,
            RetailMainMenuWritingScroll.TileX,
            RetailMainMenuWritingScroll.TileY(writingCounter, 0),
            1f,
            1f,
            chromeTint);
        DrawSurfaceCentered(
            _forsetiWritingLarge,
            RetailMainMenuWritingScroll.TileX,
            RetailMainMenuWritingScroll.TileY(writingCounter, 1),
            1f,
            1f,
            chromeTint);
        DrawSurfaceCentered(
            _forsetiWritingLarge,
            RetailMainMenuWritingScroll.TileX,
            RetailMainMenuWritingScroll.TileY(writingCounter, 2),
            1f,
            1f,
            chromeTint);

        DrawLanguageSelector(fade);

        // The selector bar is drawn BEFORE every row, not interleaved into the
        // row loop. Retail's order is not ambiguous: the bar is frame 3000
        // draw 11 and the fourteen row draws are 12..25, so the bar is under all
        // of them. Interleaved, it was emitted after rows 0..k-1 for a selection
        // at index k, and because the bar is 32 tall on a 20 pitch it reaches
        // 4px into the row above (rowY-16..rowY-12 against that row's glyph box
        // ending at rowY-12). That washed the bottom 4px of the previous label
        // with the bar's 49%-alpha black on every selection except row 0 — which
        // is exactly why nothing caught it: the settled capture selects row 0.
        DrawMainMenuSelectorBar(iconFade);

        for (int index = 0; index < _session.Items.Count; index++)
        {
            RetailFrontendMenuItem item = _session.Items[index];
            // RetailMainMenuRowY: [esp+0x10] seeds 268 / index
            // -1. Nonzero [0x0083D990] overwrites 304 / index 0.
            // Dest Y keeps rowY - 8. Do not invent dest Y.
            // RetailMainMenuLanguagePitch: language fall-through
            // fld / fadd 36.0 at 0x00463647, then the shared
            // tail. Reached from 0x004634EE and after 0x0046363B.
            // Next slot is NonzeroSlotY. Dest Y keeps rowY - 8.
            // Do not invent dest from 36.0 or the nearby 284 push.
            float rowY = RetailMainMenuRowY.NonzeroSlotY + (index * MenuPitch);
            bool selected = index == _session.SelectedMainIndex;
            // Draw the string as authored. english.json holds "Continue Game" /
            // "Load Game" in mixed case and retail renders them that way.
            // Font13PS cells are 16px, so scale 1.0 gives the retail 20px pitch.
            string label = _menuText[item.Kind];
            const float textScale = 1f;
            float textWidth = MeasureText(label, textScale);
            // RetailMainMenuLabelDest: dest X is the measure
            // sibling (219 minus half cx), not a dest immediate.
            // Dest Y keeps rowY - 8. Do not invent dest or a
            // 2px kerning hack.
            var textPos = new Vector2(
                RetailMainMenuLabelDest.DestX(textWidth),
                rowY - 8f);

            if (fade <= 0f)
            {
                continue;
            }

            // RetailMainMenuLabelText: the other CFEPMain::Render
            // DrawTextDynamic at 0x0046316F. Dest is ebx / [esp+0x24],
            // not immediates. 0x0046315A leftover is Z, not writing
            // chrome and not the selector bar. Leftover stack args
            // 10 / 9 / 8 stay unused. Font slot push 1. Scales stay
            // 1.0. Colour stays LabelColor. Dest stays MeasureText.
            // Cite-fix: cmp ebx, 0x3E8 is 0x00465771; 0x00465777 is
            // mov word [eax], 0. Do not invent dest, wrap, fade,
            // sheen, or a 2px kerning hack.
            Color textColor = RetailColor(RetailMainMenuLabelColor.SubmittedColor(
                selected,
                item.IsAvailable,
                RetailMainMenuLabelColor.ImageSettledFadeByte));
            DrawText(label, textPos, textScale, new Color(textColor, textColor.A * fade));
        }

        // Shadows (offset bases; GetShadowOffset* ≈ 0 settled), then bodies. Order
        // is the shipped order: left decoration, its transition twin, right
        // decoration, its transition twin, then the selection icon. At transition
        // >= 1 every state below collapses to the previous constants — scale 1.25,
        // rotation 0, alpha 1, twins not drawn — so the settled frame is unchanged
        // by this whole block.
        // THE SHADOW OFFSETS ARE ANIMATED. Recovered 2026-07-27 from retail's own
        // draw calls over 3,756 settled frames of the A2 main-menu log and
        // re-verified in the main loop on seven frames, two of them outside the
        // reported range. The law, its evidence, its dt-driven clock and its one
        // soft term are all in RetailFrontendDecorShadow; only the wiring is here.
        //
        // The literals this replaces — (224,349) and (462,365), i.e. offsets
        // (+5,+5) and (+5,+10) — are EXACTLY the centre of the measured ellipse,
        // so they were retail's law evaluated at its time-mean. Nothing about
        // them was wrong-signed; the oscillation around them was simply absent.
        // The BODY anchors (219,344) and (457,355) are measured-exact and are
        // untouched.
        var leftShadow = RetailFrontendDecorShadow.LeftArcOffsetAtPhase(
            RetailFrontendDecorShadow.PhaseAtSeconds(_animationSeconds));
        var sharedShadow = RetailFrontendDecorShadow.OffsetAtPhase(
            RetailFrontendDecorShadow.PhaseAtSeconds(_animationSeconds));
        var leftArcBody = new Vector2(219f, 344f);
        var rightArcBody = new Vector2(457f, 355f);
        Vector2 leftArcShadow = leftArcBody + new Vector2((float)leftShadow.X, (float)leftShadow.Y);
        Vector2 rightArcShadow = rightArcBody + new Vector2((float)sharedShadow.X, (float)sharedShadow.Y);

        DrawMainMenuDecor(
            _titleBracket01,
            leftArcBody,
            leftArcShadow,
            MainMenuLeftDecor(transition));
        // Colour at 0x00463873 is RetailMainMenuLeftDecorShadow: 0x0089D894
        // *63 alpha pack, dest fadd 224/349, z push 0x3EB33333 (0.35) not
        // scale. Settled 255 submits 0x3E000000, which is this ShadowTint,
        // so the draw keeps ShadowTint and does not call SubmittedColor.
        // Dest is the left-arc pair (219+5, 344+5), not right 462/365.
        // Both dest helpers land on 0x00468730. The 224/349 addends are
        // the already-shipped ellipse centre — do not redo
        // RetailFrontendDecorShadow. Colour at 0x004638B7 is
        // RetailMainMenuLeftDecorOverlay: 0x0089D894 not/and/xor pack,
        // dest immediates 219/344, z push 0x3E99999A (0.3) not scale.
        // Settled 255 submits 0xFEFFFFFF, which is not this BracketTint
        // 0xFE7F7F7F, so the draw keeps BracketTint and does not call
        // SubmittedColor. Dest is the left-arc body, not right. Not the
        // 0x00463E8D twin gate (that is D8A4). Not a sheen (that is
        // 0x00464343 / TitleLogoReflectionLayer). Not a 29% title-logo
        // scale. ChromeTint and ShadowTint stay put. Do not redo
        // 0x00463873, 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.
        DrawMainMenuDecor(
            _titleBracket02,
            leftArcBody,
            leftArcShadow,
            MainMenuLeftDecorTwin(transition));
        // Colour at 0x00463A8F is RetailMainMenuLeftTwinShadow: 0x0089D898
        // *63 alpha pack, dest fadd 224/349, z push 0x3EB33333 (0.35) not
        // scale. Settled 255 submits 0x3E000000, which is this ShadowTint,
        // so the draw keeps ShadowTint and does not call SubmittedColor.
        // Dest is the leftover left-twin pair (219+5, 344+5), not
        // 0x0089D894 primary and not right 462/365. Both dest helpers
        // land on 0x00468730. The 224/349 addends are the already-shipped
        // ellipse centre — do not redo RetailFrontendDecorShadow. Colour
        // at 0x00463AD3 is RetailMainMenuLeftTwinOverlay: 0x0089D898
        // not/and/xor pack, dest immediates 219/344, z push 0x3E99999A
        // (0.3) not scale. Settled 255 submits 0xFEFFFFFF, which is not
        // this BracketTint 0xFE7F7F7F, so the draw keeps BracketTint and
        // does not call SubmittedColor. Dest is the leftover left-twin
        // body, not 0x0089D894 primary and not right. Not a sheen.
        // Not a 29% title-logo scale. ChromeTint and ShadowTint stay
        // put. Do not redo 0x00463873, 0x004638B7, 0x00463A8F,
        // 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.
        DrawMainMenuDecor(
            _symbolBracket01,
            rightArcBody,
            rightArcShadow,
            MainMenuRightDecor(transition));
        DrawMainMenuDecor(
            _symbolBracket02,
            rightArcBody,
            rightArcShadow,
            MainMenuRightDecorTwin(transition));
        // Colour at 0x00463D1F is RetailMainMenuRightDecorShadow: 0x0089D8A0
        // *63 alpha pack, dest fadd 462/365, z push 0x3EB33333 (0.35) not
        // scale. Settled 255 submits 0x3E000000, which is this ShadowTint,
        // so the draw keeps ShadowTint and does not call SubmittedColor.
        // Dest is the right-arc pair, not left. Not the 0x00463E8D twin
        // gate (that is D8A4). The 462/365 addends are the already-shipped
        // ellipse centre — do not redo RetailFrontendDecorShadow. Not a
        // sheen (that is 0x00464343 / TitleLogoReflectionLayer). Not a
        // 29% title-logo scale. ChromeTint and BracketTint stay put. Do
        // not redo 0x00463F3F or 0x00463F83.
        // Colour at 0x00463D63 is RetailMainMenuRightDecorOverlay:
        // 0x0089D8A0 not/and/xor pack, dest immediates 457/355, z push
        // 0x3E99999A (0.3) not scale. Settled 255 submits 0xFEFFFFFF,
        // which is not this BracketTint 0xFE7F7F7F, so the draw keeps
        // BracketTint and does not call SubmittedColor. Dest is the
        // right-arc body, not left. Not the 0x00463E8D twin gate (that
        // is D8A4). Not a sheen. Not a 29% title-logo scale. ChromeTint
        // and ShadowTint stay put. Do not redo 0x00463D1F.
        // Colour at 0x00463F3F is RetailMainMenuRightTwinShadow: 0x0089D8A4
        // *63 alpha pack, dest fadd 462/365, z push 0x3EB33333 (0.35) not
        // scale. Settled 255 submits 0x3E000000, which is this ShadowTint,
        // so the draw keeps ShadowTint and does not call SubmittedColor.
        // Same 0x00463E8D gate as the body overlay; settled frames skip.
        // The 462/365 addends are the already-shipped ellipse centre —
        // do not redo RetailFrontendDecorShadow. Not a sheen (that is
        // 0x00464343 / TitleLogoReflectionLayer). Not a 29% title-logo
        // scale. ChromeTint and BracketTint stay put.
        // Colour at 0x00463F83 is RetailMainMenuRightTwinOverlay: 0x0089D8A4
        // mode-4 at (457,355), z push 0x3E99999A (0.3) not scale. Settled 255
        // submits 0xFEFFFFFF, which is not this BracketTint 0xFE7F7F7F, so
        // the draw keeps BracketTint and does not call SubmittedColor. Gate
        // 0x00463E8D skips the call once transition >= 0.9; settled frames
        // never issue it. Not a sheen (that is 0x00464343 /
        // TitleLogoReflectionLayer). Not a 29% title-logo scale. ChromeTint
        // and ShadowTint stay put.

        if (iconFade > 0f)
        {
            Texture2D icon = _menuIcons[_session.SelectedMainIndex];
            Color iconTint = _session.SelectedMainItem.IsAvailable ? BracketTint : ReleasedUnavailable;
            // Pair C of the four the shadow law was recovered from: the
            // selected-row icon takes the SHARED (u,v) offset, the same vector as
            // the right arc, off the same body anchor (457,355).
            // Colour at 0x0046407C is RetailMainMenuSelectedIconShadow: settled
            // ((255<<6)-255)<<16 & 0xFF000000 is 0x3E000000, which is this
            // ShadowTint, so the draw keeps ShadowTint and does not call
            // SubmittedColor. Shadow scale stays ShadowScaleBoost (1.05).
            DrawSurfaceCentered(
                icon,
                rightArcShadow.X,
                rightArcShadow.Y,
                ShadowScaleBoost,
                ShadowScaleBoost,
                new Color(ShadowTint, ShadowTint.A * iconFade));
            // Colour at 0x004640DC is RetailMainMenuSelectedIconColor: settled
            // ((255<<8)-255)<<16 | 0x00FFFFFF is 0xFEFFFFFF, which is not
            // this BracketTint 0xFE7F7F7F (frame 3000 draw 31), so the draw
            // keeps BracketTint and does not call SubmittedColor. Body scale
            // stays 1.0; this is not a 29% scale. ChromeTint and ShadowTint
            // stay put.
            DrawSurfaceCentered(icon, 457f, 355f, 1f, 1f, new Color(iconTint, iconTint.A * iconFade));
        }

        if (fade > 0f)
        {
            // SHADOWED, corrected 2026-07-28 from a single flat run. The version
            // string is a shadow/body PAIR like every other text run on the page:
            // frame 3000 draw 32/33 is the already-owned DrawText pair — shadow
            // on the anchor, body at anchor-(1,1), shadow RGB black carrying the
            // body's own alpha. Dest leftover at 0x004641C9 is
            // RetailMainMenuVersionOverlayZ: PLATFORM__GetWindowHeight then
            // sub 0x10, dest X push 0. That leftover is not a dest immediate.
            // 0x004641C4 push 0x3C23D70A is Z, not scale, so this draw keeps
            // VersionTint, Format, DestX, DestY(DesignHeight), and scale 1.0.
            // Font leftover is RetailMainMenuVersionOverlayFont: push 1 selects
            // FONT_SMALL / Font13PS at this+0x20, not this+0x1C. No measure
            // call on the sprintf buffer. Pre-draw leftover is
            // RetailMainMenuVersionOverlayEnable: after sprintf / add esp,10 /
            // lea edx,[esp+0x3C], 0x00464180 stores [0x00679B40]=0. The
            // 0x00465F00 reader is mov al,[0x00679B40]; ret. Widen leftover
            // is RetailMainMenuVersionOverlayWiden: after the enable-byte
            // store, push edx of that sprintf buffer and call
            // Text__AsciiToWideScratch. add esp,4 shows cdecl one-arg.
            // EAX is the wide scratch pointer. Tail leftover is
            // RetailMainMenuVersionOverlayTail: after add esp,4 the
            // three leftover pushes remain as DrawTextDynamic's last
            // three stack slots. The leftover float is past the
            // below-zero / below-quarter / below-half arms, and the
            // second leftover dword is zero so that colour arm is
            // skipped. Do not invent dest, wrap, fade, or sheen from
            // those slots. Post-draw leftover is
            // RetailMainMenuVersionOverlayFlags:
            // after DrawTextDynamic, 0x004641FC/203/20A store
            // [0x00679B40]=1, [0x009C68AC]=0, [0x009C690D]=1 between
            // fcom [0.0] and fnstsw. That fcom is the already-owned
            // title-logo shadow clamp, not a version fade. The MeasureText
            // residual stays open; do not invent a fade or a kerning hack.
            DrawText(
                RetailMainMenuVersionOverlayWiden.Widen(
                    RetailMainMenuVersionOverlay.Format(
                        RetailMainMenuVersionOverlay.ImageInitialMajor,
                        RetailMainMenuVersionOverlay.ImageInitialMinor)),
                new Vector2(
                    RetailMainMenuVersionOverlayZ.DestX,
                    RetailMainMenuVersionOverlayZ.DestY((int)DesignHeight)),
                1f,
                new Color(VersionTint, VersionTint.A * fade));
        }

        // 0x0089d7fc reflection sheen — RESTORED 2026-07-27 from the shipped
        // bytes, as a separate additive CanvasItem clipped to the logo's own
        // alpha-tested footprint. It is NOT drawn here; see
        // TitleLogoReflectionLayer for the disassembly, the ONE/ONE blend, the
        // 0xff7e7e7e tint, the depth-stamp mask and the two measured constants.
        //
        // The 2026-07-26 deletion rationale is kept below because reason (1) was
        // right and is the defect this fixes.
        //
        // Two 512x128 quads were drawn at scale (1,2) centred on (321,120) and
        // (833,120), covering x65..640, y-8..248 of the stage. Two independent
        // measurements say retail does not put that there:
        //
        // 1. It was not additive. The helper set CanvasItem.Material for the
        //    duration of one call, but a CanvasItem's material
        //    applies to the whole item, not to a bracketed span of _Draw commands,
        //    and the trailing `Material = null` leaves the item non-additive at
        //    submit time. The proof is in the pixels, not in that reading: our
        //    captured main menu held (26,24,33) at (320,0) where the page fill is
        //    (23,23,48). Additive blending cannot LOWER a channel by 15.
        // 2. Retail shows no excess there: fitting fill + gain x FEBack128 over
        //    the proven-underlay mask and splitting the residual by this footprint
        //    gives mean -0.43..+0.84 (|mean| 1.5..3.4) over 84,912 pixels INSIDE
        //    it against +0.27..+1.45 (|mean| 1.8..3.1) over 137,771 outside, on 13
        //    frames.
        //
        //    THE STRENGTH OF (2) IS BOUNDED, and an earlier draft of this comment
        //    overstated it as "retail has no such layer". An independent
        //    adversarial pass is right that a mean residual mostly constrains
        //    the DC component: a reflection texture that is near zero-mean
        //    over the footprint can
        //    carry a real gain while moving the mean by nothing, and if it
        //    correlates with FEBack128 the FEBack fit absorbs part of it. What (2)
        //    supports is a DC bound of order one level, not absence. The
        //    projection of the residual onto the reflection texture orthogonalised
        //    to FEBack128 was NOT computed and would settle it.
        //
        //    The deletion does not rest on (2). It rests on (1): what was drawn
        //    here was provably not the blend it claimed to be.
        //
        // WITHDRAWN 2026-07-27: (2)'s footprint was the whole 512x256 rectangle,
        // but the bytes put the layer inside the logo's alpha>=8 texels only. Run
        // the same regression on that footprint and the gain is 0.488/0.489/0.487
        // against 0.025/0.030/0.038 outside it. A rectangle-wide mean could not
        // have seen that, which is exactly the bound the comment already conceded.
        // Pair D of the four the shadow law was recovered from. Retail's title
        // body is (64,2)-(576,258), centre (320,130) — this anchor exactly — and
        // its shadow takes the SHARED (u,v) offset off it, at the same 1.05 scale.
        // The (325,140) that stood here is that offset's time-mean.
        // Colour at 0x0046424F is RetailMainMenuTitleLogoShadow: settled
        // (255*63)<<16 & 0xFF000000 is 0x3E000000, which is this ShadowTint,
        // so the draw keeps ShadowTint and does not call SubmittedColor.
        // dest==0x0c at 0x0046423D forces ESI=255. Body scale stays 1.0;
        // this is not a 29% scale. Body pack stays TitleLogoTint.
        // 0x0089D88C leftover at 0x00464251 is
        // RetailMainMenuTitleLogoShadowZ: same Title2, then push
        // 0x3DCCCCCD. That leftover is Z, not scale. Dest is
        // GetShadowOffsetY + [0x005D8C20] and GetShadowOffsetX +
        // [0x005DB4A8], not RenderSurface immediates, so this draw
        // keeps ShadowTint, ShadowScaleBoost, DestX, DestY, and
        // sharedShadow. Nearby 0x3F866666 is already
        // ShadowScaleBoost. Not a sheen.
        // 0x0089D88C at 0x004642CE is RetailMainMenuTitleLogoZ:
        // FrontEnd\v3\FE_BEA_Title2.tga via ebp+0x12C, then push
        // 0x3F7FBE77, dest Y 130, dest X 320. That leftover is Z, not
        // scale, so this draw keeps TitleLogoTint, scale 1.0, DestX,
        // and DestY and does not treat the dword as a 29% title-logo.
        // Nearby 0x3F866666 is already ShadowScaleBoost. Not a sheen.
        DrawSurfaceCentered(
            _titleLogo,
            RetailMainMenuTitleLogoZ.DestX + (float)sharedShadow.X,
            RetailMainMenuTitleLogoZ.DestY + (float)sharedShadow.Y,
            ShadowScaleBoost,
            ShadowScaleBoost,
            ShadowTint);
        DrawSurfaceCentered(
            _titleLogo,
            RetailMainMenuTitleLogoZ.DestX,
            RetailMainMenuTitleLogoZ.DestY,
            1f,
            1f,
            TitleLogoTint);
    }

    /// <summary>
    /// The selected row's highlight bar — retail's frame 3000 draw 11, emitted
    /// before all fourteen row draws (12..25) rather than between them.
    ///
    /// <para>WIDTH: the label's own ink width plus exactly 31. MEASURED on two
    /// selected rows by scanning every 256x32 selector-bar draw in the A3 run
    /// (<c><maintainer evidence>\bea-frontend-pages\A3-options-20260727-205107\d3d9-draws.log</c>,
    /// frames 1..2600, 1,165 draws), which returns exactly TWO distinct
    /// rectangles because the run hovers Options on its way into that page:</para>
    ///
    /// <code>
    ///   New Game selected  (160.5,288)-(277.5,320)  117 x 32  frames  754..1830
    ///   Options  selected  (173.0,388)-(265.0,420)   92 x 32  frames 1831..1918
    /// </code>
    ///
    /// <para>Both are centred on x = 219 and on their own row centre, both are 32
    /// tall, and both are ink + 31: 86+31 = 117 and 61+31 = 92. Two rows whose
    /// labels differ by 25px give the same constant, so the width tracks the
    /// label and 31 is the padding.</para>
    ///
    /// <para>HEIGHT 32, MEASURED, not 20: draw 11 is a 256x32 DXT2 at u,v 0..1,
    /// diffuse 0x7E000000, identical on frames 3500 and 4000 of the same run.
    /// <c>rowY - 16</c> lands the quad at y 288 for row 0; a D3D9 quad spanning
    /// [288,320) covers pixel rows 288..319, which is what a Godot Rect2 at
    /// y = 288, h = 32 covers.</para>
    ///
    /// <para>0x0089D89C at 0x00462FED is RetailMainMenuSelectorBarZ:
    /// FrontEnd\v3\FE_BEA_title_text_box.tga via ebp+0x13C, then push
    /// 0x3EA8F5C3 and dest 219. That leftover is Z, not scale, so this
    /// draw keeps _titleTextBox, the measured ink+31 width, and DestX
    /// and does not treat the dword as a 29% title-logo. Colour at
    /// 0x00462FB9 stays RetailMainMenuSelectorBarColor. Not a sheen.</para>
    /// </summary>
    private void DrawMainMenuSelectorBar(float iconFade)
    {
        if (iconFade <= 0f)
        {
            return;
        }

        int index = _session.SelectedMainIndex;
        if (index < 0 || index >= _session.Items.Count)
        {
            return;
        }

        float rowY = MenuStartY + (index * MenuPitch);
        float boxWidth = MeasureText(_menuText[_session.Items[index].Kind], 1f) + 31f;

        // 0x0089D89C at 0x00462FED is RetailMainMenuSelectorBarZ.
        // 0x3EA8F5C3 leftover is Z, not scale. Dest X is this DestX.
        DrawTextureRect(
            _titleTextBox,
            new Rect2(
                RetailMainMenuSelectorBarZ.DestX - (boxWidth * 0.5f),
                rowY - 16f,
                boxWidth,
                32f),
            false,
            new Color(
                RetailColor(RetailMainMenuSelectorBarColor.SubmittedColor(
                    RetailMainMenuSelectorBarColor.ImageSettledFadeByte)),
                HighlightTint.A * iconFade));
    }

    /// <summary>
    /// The language selector sitting directly above the menu column.
    ///
    /// GEOMETRY REPLACED 2026-07-28 by retail's own quads. The values below were
    /// previously read off a 640x480 PNG by hunting pixels that differ from the
    /// flat (23,23,48) background — left chevron x 144..165 y 254..283, flag
    /// x 177..261 y 252..284, right chevron x 273..294 y 253..282. That is ink
    /// measurement, and it was close, but it could not recover the quads and it
    /// disagreed with itself: it put the two chevrons on different rows (254 and
    /// 253) and gave them different heights from the flag, which no released
    /// layout does.
    ///
    /// Retail's quads, main-menu-settled.csv frame 3000, confirmed identical on
    /// frame 4000 and on all 250 frames of main-menu-reveal-frames-613-900.csv:
    ///
    ///   draw  6  flag           (177.4,252.0)-(260.6,284.0)   83.2 x 32.0
    ///   draw  7  left chevron   (128.4,242.4)-(179.6,293.6)   51.2 x 51.2
    ///   draw  9  right chevron  (258.4,242.4)-(309.6,293.6)   51.2 x 51.2
    ///
    /// QUAD IS NOT INK, AND THAT DISTINCTION IS THE WHOLE FINDING HERE. Read as
    /// appearance, those rectangles say the left chevron's right edge (179.6)
    /// runs 2.2px PAST the flag's left edge (177.4) — that the chevron overlaps
    /// the flag. It does not. The d3d9 proxy does not wrap textures, so the
    /// inventory carries quad, format and dimensions and never ink extent; the
    /// two prior write-ups both stopped at "blocked on texture contents".
    ///
    /// It is not blocked. The texture is on this machine. FE_Arrow.tga decodes
    /// (Assets/Frontend/fe-arrow.texture.aya, a 64x64 DXT2 DDS, which matches the
    /// inventory's tex0 exactly) to a right-pointing chevron whose alpha is a
    /// hard 0-or-255 with ink bounded by texels (16,12)-(46,52) — 30 x 40 inside
    /// 64 x 64, i.e. u 0.25..0.71875, v 0.1875..0.8125. The draws sample u,v
    /// 0..1, so composing sprite with quad gives the ink:
    ///
    ///   left chevron ink   (142.8,252.0)-(166.8,284.0)   24.0 x 32.0
    ///   flag               (177.4,252.0)-(260.6,284.0)   83.2 x 32.0
    ///   right chevron ink  (271.2,252.0)-(295.2,284.0)   24.0 x 32.0
    ///
    /// So retail leaves a 10.6px CLEAR GAP on each side, and all three elements
    /// are exactly 32 tall on exactly the same rows, y 252..284. The overlap was
    /// entirely in the sprite's transparent margin.
    ///
    /// The left chevron is the MIRRORED draw, and the composition proves it
    /// rather than assuming it: the ink box is off-centre in its texture (16 left
    /// margin against 18 right), so mirroring shifts it. Mirrored, the row is
    /// symmetric about x = 219.0 to 0.0 — 219-142.8 = 76.2 = 295.2-219 and
    /// 219-166.8 = 52.2 = 271.2-219. Unmirrored it would miss by 1.6px. That
    /// symmetry also independently re-confirms MenuColumnX as a centre anchor.
    ///
    /// Draws 8 and 10 are a SECOND COPY of each chevron at exactly twice the
    /// size, concentric on the same centres (154,268) and (284,268). They are not
    /// drawn here because they cannot produce a pixel: their diffuse is
    /// 0x007F7F7F in 500 of 500 sampled rows, stage-0 ALPHAOP is
    /// MODULATE(TEXTURE, DIFFUSE) so the result alpha is zero, and ALPHATEST is
    /// enabled at GREATEREQUAL ref 8, which rejects every texel before blending.
    /// They are real draw calls and they are no-ops; see the diff note for why
    /// that matters to any "39 of 39" claim.
    ///
    /// <paramref name="fade"/> is CFEPMain__Render's page fade. The language row is
    /// drawn inside the same loop as the menu labels and every one of its packed
    /// colours is multiplied by the same alpha byte, so it reveals with them. The
    /// selected-row sine at 0x0046319E is pinned by RetailMainMenuLanguageSine;
    /// session cannot hold this+0x08=-1, so this draw does not light that pack.
    /// Chevron visibility is RetailMainMenuLanguageBlink: fistp(mCounter) signed
    /// remainder 64, draw while below 50. Cold BSS 0 draws. The 2x copies stay
    /// no-ops. Chevron colour at 0x0046336B / 0x004634F4 is
    /// RetailMainMenuLanguageChevronColor: settled unselected submits
    /// 0x3EFFFFFF, which is not capture ChromeTint 0x3E7F7F7F, so this
    /// draw keeps ChromeTint and does not call SubmittedColor.
    /// </summary>
    private void DrawLanguageSelector(float fade)
    {
        if (_languageFlags.Length == 0 || fade <= 0f)
        {
            return;
        }

        int language = (int)_session.Language;
        if (language < 0 || language >= _languageFlags.Length)
        {
            language = 0;
        }

        // Retail draws the flag dimmed, not at full brightness. Its brightest
        // rendered texel is (125,125,125) where the source texture is white, and
        // 0x3f is exactly 125 under the 2x modulate ((0x3f*255)>>7). Measured mean
        // ratio across the flag rect is 0.473/0.476/0.486 - uniform, i.e. a grey
        // tint rather than a per-channel correction.
        DrawTextureRect(
            _languageFlags[language],
            LanguageRowRect(RetailFrontendLanguageRow.Flag),
            false,
            new Color(FlagTint, FlagTint.A * fade));

        // Draw the WHOLE 64x64 texture into retail's whole 51.2x51.2 quad, the
        // way retail does, rather than sourcing the ink sub-rect into an ink-sized
        // destination. Both put the ink in the same place; this one keeps our
        // constants equal to the inventory's, so the next reader diffs numbers
        // against the CSV instead of re-deriving the margin. It also samples the
        // sprite's transparent border the way retail's sampler does, which is
        // where the two differ by a texel of edge feathering.
        var arrowTint = new Color(ChromeTint, ChromeTint.A * fade);
        if (RetailMainMenuLanguageBlink.ShouldDraw(
                RetailMainMenuLanguageBlink.ImageInitialCounter,
                RetailMainMenuLanguageBlink.ImageInitialTimer))
        {
            DrawTextureRect(
                _feArrow,
                LanguageRowRect(RetailFrontendLanguageRow.LeftChevron),
                false,
                arrowTint);
            DrawTextureRect(
                _feArrow,
                LanguageRowRect(RetailFrontendLanguageRow.RightChevron),
                false,
                arrowTint);
        }
    }

    /// <summary>
    /// Retail's language-row quad as a Godot rect.
    ///
    /// <para><b>A negative width flips IN PLACE. It does not move the rect.</b>
    /// Godot turns a negative <c>Rect2</c> size into a flip flag and the absolute
    /// size, leaving <c>position</c> as the LEFT edge either way — so the
    /// position passed here is always <c>quad.X</c>, never <c>quad.Right</c>.
    /// This is not a style note. The code this replaces read
    /// <c>Rect2(166, 254, -22, 30)</c> intending x 144..166, and it rendered at
    /// x 166..188 — 22px right of where it was meant to be, which put the left
    /// chevron ON TOP of the flag. Measured on our own captured frame
    /// (local-lab/godot-captures/gaterepair-head-mainmenu/mainmenu-t008000ms.png,
    /// columns above threshold in the row band): retail shows three separated
    /// runs 144..165 / 178..260 / 273..294, ours showed two, 167..261 and
    /// 274..293, with nothing at all where retail's left chevron lives.</para>
    /// </summary>
    private static Rect2 LanguageRowRect(RetailFrontendLanguageRow.Quad quad) =>
        new(
            (float)quad.X,
            (float)quad.Y,
            quad.Mirrored ? (float)-quad.Width : (float)quad.Width,
            (float)quad.Height);

    /// <summary>
    /// FEP_MAIN's underlay: the flat page fill with FEBack128.vid composited
    /// additively over it.
    ///
    /// THE ARGUMENT THIS REPLACED, AND WHY IT WAS WRONG. The previous
    /// implementation drew a flat (23,23,48) fill, justified by the observation
    /// that two disjoint 120x120 regions of the retail main-menu reference hold
    /// exactly one colour at sd 0.0, which a stretched 128-square video cannot
    /// produce. That observation was correct and the conclusion did not follow:
    /// every capture it rested on was taken with `-skipfmv`, and `-skipfmv`
    /// removes the video. The same "zero variance, so no video" signature had
    /// already been shown to be reproduced on FEP_LEVEL_SELECT, a page that DOES
    /// carry the video. The comment that stood here named the experiment that
    /// would settle it — one main-menu frame captured without `-skipfmv` — and
    /// that frame has now been taken.
    ///
    /// MEASURED, from retail without `-skipfmv`
    /// (local-lab/retail-reference-pristine/nofmv-frontend-2026-07-26/, two runs,
    /// phase-anchored to the click that leaves click-to-start):
    ///
    ///   identity   the underlay IS FEBack128.vid. Scoring every decoded strip
    ///              frame upscaled 128^2 -> 640x480 by normalised cross-correlation
    ///              over the 222,683 pixels the `-skipfmv` reference proves are
    ///              pure underlay peaks at ncc +0.81..+0.92 on five sampled frames.
    ///   rate       best-match frame is 30 at t=1020 ms, 74 at 2529, 119 at 4030,
    ///              164 at 5522, 209 at 7027 — 29.80 fps, i.e. the shipped 30.
    ///   phase      that line passes through frame 0 at t = 13 ms. The clip starts
    ///              when the frontend leaves click-to-start, within one frame.
    ///   composite  flat fill PLUS gain x frame (see FeBackUnderlayGain). Alpha
    ///              mix is refuted on the same pixels.
    ///
    /// The composite is baked into the frame textures at load rather than issued as
    /// an additive draw. That is exact here and not an approximation: this is the
    /// bottom-most layer, it is drawn over a known constant fill, and
    /// fill + gain x frame peaks at 114 so nothing clamps. It also avoids depending
    /// on a canvas blend mode. The only additive helper this file had was
    /// measurably NOT additive (see DrawMainMenu's reflection-streak note), and a
    /// layer this large must not inherit that defect.
    /// </summary>
    /// <param name="transition">
    /// <c>trans</c> for FEP_MAIN. The video ramps IN with the incoming page:
    /// <c>CFrontEnd::DrawStandardVideoBackground</c>, FrontEnd.cpp:1023-1045.
    ///
    /// <para><b>The law is ported; the BRANCH SELECTION is measured, and that
    /// distinction is the honest part of this method.</b> FrontEnd.cpp:1038-1041
    /// offers exactly two alphas —
    /// <c>MakeAlpha(RangeTransition(transition, 0, 0.5))</c> when the other page is
    /// FEP_MAIN and <c>MakeAlpha(RangeTransition(transition, 0.5, 1))</c>
    /// otherwise — and <c>dest</c> at FrontEnd.cpp:1299-1305 is the OTHER page, so
    /// the literal condition for FEP_MAIN's own call selects the second. The second
    /// is REFUTED: fitting retail's underlay strength as
    /// <c>pixel = fill + a * gain * FEBack[k]</c> over the pure-underlay box
    /// (0,181)-(120,300) gives a = 0.000 with zero residual at t = 14 ms (run1) and
    /// t = 29 ms (run2) — the box is bit-exactly the flat fill — and then
    /// a = 1.07 at t = 422 ms, 1.13 at 524 ms, 1.02 at 1020 ms. Full video at
    /// 422 ms cannot come from a (0.5, 1) window on a 50-frame transition. The
    /// (0, 0.5) window does produce it, so that is the branch taken here.</para>
    ///
    /// <para>Neither branch is FEP_MAIN's own: the video draw belongs to
    /// <c>CFEPMain__RenderPreCommon</c> (0x00462B70), which has no source in the
    /// drop and no decompile in this lab, and the source comment at
    /// FrontEnd.cpp:1025-1034 says the DirectX video is async-loading and "needs
    /// time before it's visible" independently of any alpha. So the exact retail
    /// mechanism for the first ~400 ms is NOT established. What is established is
    /// the pair of measurements above, and the ported law is the one of the two
    /// available that reproduces them.</para>
    /// </param>
    private void DrawMainUnderlay(float transition)
    {
        // The flat page fill is drawn FIRST and opaque, then the baked
        // fill+gain*frame composite over it at the video's own alpha. That is
        // exact rather than convenient: a * (fill + gain*frame) + (1-a) * fill
        // == fill + a * gain * frame, which is the released additive composite
        // with the alpha applied only to the video term. Modulating the baked
        // texture alone would have faded the FILL as well, and retail's page fill
        // is present at full strength on the very first frame.
        // Clear first, then the 24.31% black darkener over it — retail's own two
        // terms rather than their product. See FrontendClearColor /
        // FrontendFillDarkener for the measurement. The darkener keeps retail's
        // measured overhang (40px left and right, 3px top and bottom past the
        // client): it lands on the letterbox bars, which _Draw has already filled
        // with black, so 24% black over black leaves them black and the client
        // area is unaffected.
        DrawRect(new Rect2(0f, 0f, DesignWidth, DesignHeight), FrontendClearColor);
        DrawRect(new Rect2(-40f, -3f, DesignWidth + 80f, DesignHeight + 6f), FrontendFillDarkener);

        if (_feBackFrames.Length == 0)
        {
            // Strip missing (materialize not run). The flat fill above is what the
            // page held before the video was identified, and it is strictly better
            // than drawing nothing.
            return;
        }

        float alpha = MakeAlpha(RangeTransition(transition, 0f, 0.5f));
        if (alpha <= 0f)
        {
            return;
        }

        int frame = FeBackFrameIndex(_feBackSeconds, _feBackFrames.Length);
        DrawTextureRect(
            _feBackFrames[frame],
            new Rect2(0f, 0f, DesignWidth, DesignHeight),
            false,
            new Color(1f, 1f, 1f, alpha));
    }

    /// <summary>
    /// Which FEBack128 frame is on screen at <paramref name="seconds"/> after the
    /// frontend left click-to-start. Pure, so it is unit-testable without Godot.
    ///
    /// <para><b>The phase, and the alias that nearly took its place.</b></para>
    /// Fitting best-matching strip frames over a 7 s retail burst gave a clean
    /// 29.80 fps line through frame 0 at t = 13 ms. It also gave, on EVERY sample,
    /// a near-equal runner-up exactly +205 frames away. An independent adversarial
    /// pass flagged that as an alias risk rather than clip self-similarity, and
    /// it was right to: normalised cross-correlation cannot pick the origin of a
    /// self-similar signal from peaks that are within noise of each other, and a
    /// 7 s window is barely a third of the clip's 19.07 s period.
    ///
    /// Settled by capturing 22 s of retail main menu - more than one full loop -
    /// and scoring the WHOLE burst against each candidate offset instead of
    /// scoring each frame independently. Over 56 frames and 521 candidate offsets:
    ///
    ///   offset  -3    mean ncc 0.8546   min  0.6983   frames below 0.5:  0
    ///   offset +202   mean ncc 0.5112   min -0.2441   frames below 0.5: 23
    ///   offset -206   mean ncc 0.3791   min -0.2412   frames below 0.5: 34
    ///
    /// -3 is the global argmax. The +205 coset is refuted, and the true phase is
    /// three frames LATER than the short-window fit said - frame 0 at t = 100 ms,
    /// not 13 ms. That correction was predicted independently: before it was
    /// applied, our sweep's best match to each retail frame sat at a median
    /// -74 ms, i.e. our underlay ran about two and a half frames early.
    /// </summary>
    internal static int FeBackFrameIndex(double seconds, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        // Rounding, not truncation, because the offset above was fitted against
        // round(t x 30 / 1000); truncating here would silently re-introduce half a
        // frame of the very phase error the offset exists to remove.
        long index = (long)Math.Round(seconds * FeBackFps) - FeBackPhaseFrames;
        if (index <= 0)
        {
            return 0;
        }

        return (int)(index % frameCount);
    }

    private void DrawQuitConfirm()
    {
        // Create() width/centre are pinned on RetailFeMessBox. Height is still
        // reconstruction (the 4th stack immediate is 0.1f). Chrome below is
        // FrontEnd.cpp DrawPanel/DrawBox plus the option_mode-2 YESNO stack,
        // not FEMessBox.cpp tiles — that file is absent and there is no
        // quit-confirm capture.
        var box = new Rect2(
            RetailFeMessBox.QuitLeft,
            RetailFeMessBox.BoxTop,
            RetailFeMessBox.QuitWidth,
            RetailFeMessBox.ReconstructionHeight);
        DrawTextureRect(_feBlank, box, false, RetailColor(RetailFeMessBox.PanelColor));
        DrawFeMessBoxEdges(box, RetailColor(RetailFeMessBox.BorderColor));

        Color text = RetailColor(RetailFeMessBox.TextColor);
        float promptWidth = MeasureText(QuitConfirmPrompt, 1f);
        DrawText(
            QuitConfirmPrompt,
            new Vector2(RetailFeMessBox.QuitCenterX - (promptWidth * 0.5f), RetailFeMessBox.PromptTop),
            1f,
            text);

        DrawQuitConfirmChoice(
            RetailFeMessBox.YesLabel,
            RetailFeMessBox.YesChoiceTop,
            _session.SelectedQuitConfirmIndex == 1);
        DrawQuitConfirmChoice(
            RetailFeMessBox.NoLabel,
            RetailFeMessBox.NoChoiceTop,
            _session.SelectedQuitConfirmIndex == 0);
    }

    private void DrawQuitConfirmChoice(string label, float top, bool selected)
    {
        float width = MeasureFont22Text(label, 1f);
        float left = RetailFeMessBox.QuitCenterX - (width * 0.5f);
        if (selected)
        {
            DrawTextureRect(
                _feBlank,
                new Rect2(
                    left - RetailFeMessBox.HighlightPadX,
                    top,
                    width + (RetailFeMessBox.HighlightPadX * 2f),
                    RetailFeMessBox.ChoiceRowHeight),
                false,
                RetailColor(RetailFeMessBox.HighlightColor));
        }

        DrawFont22Text(label, new Vector2(left, top), 1f, 1f, RetailColor(RetailFeMessBox.TextColor));
    }

    private void DrawFeMessBoxEdges(Rect2 box, Color color)
    {
        float w = RetailFeMessBox.BoxLineWidth;
        DrawRect(new Rect2(box.Position.X, box.Position.Y, box.Size.X, w), color);
        DrawRect(new Rect2(box.Position.X, box.End.Y - w, box.Size.X, w), color);
        DrawRect(new Rect2(box.Position.X, box.Position.Y, w, box.Size.Y), color);
        DrawRect(new Rect2(box.End.X - w, box.Position.Y, w, box.Size.Y), color);
    }

    /// <summary>
    /// Retail FEP_DEVSELECT — the "CHOOSE GAME NAME" page reached from New Game
    /// and Load Game.
    ///
    /// New mode renders the editable name field; Load mode renders caller-injected
    /// read-only career rows. Client owns bounded row selection, accept/back, and
    /// the selected-career handoff. This lane carries no career persistence or
    /// implicit save discovery.
    ///
    /// Geometry is MEASURED from the pristine 640x480 retail capture
    /// local-lab/retail-reference-pristine/choose-game-name/choose-game-name-640x480.png
    /// by scanning for the exact fill colours it contains:
    ///   page background      flat (23,23,48)                — same fill proven for the main menu
    ///   header text box      interior (12,12,24), x191..584, y69..89
    ///   title text           white (254,254,254), x263..513, y73..88, centred on x=390
    ///   list panel           border (130,132,139) at x128/x530/y130/y401,
    ///                        interior (9,9,18) x129..529, y131..400
    ///   scrollbar divider    (127,129,132) 1px at x=510, y131..400
    ///   scrollbar thumb      (245,249,245) outline x515..525, y135..396
    ///   name field           border (159,162,165) at x128/x530/y408/y451,
    ///                        interior (9,9,18) x129..529, y409..450
    ///   name highlight       (0,128,159) x293..366, y415..445
    ///   career row text      (128,128,128), left edge x=132, pitch 24
    ///   faint guide lines    (50,51,72) at x=123 and y=180
    ///
    /// Two of those colours corroborate the released source directly:
    /// the header box interior is exactly black at 0x7f alpha over the page
    /// background, which is the literal `col = 0x7f000000` at
    /// references/Onslaught/FrontEnd.cpp:1127, and the title is drawn centred on
    /// HEADER_BAR_X = 390 (FrontEnd.cpp:1103) in 0xff7f7f7f (FrontEnd.cpp:1215).
    ///
    /// KNOWN GAPS, stated rather than faked: the metal header end-cap brackets
    /// (FET3_HEADER_BRACKET1, retail x182..190 and x585..598) and the blue
    /// Forseti emblem at top-left are drawn from textures this lane has not
    /// identified or materialized, so they are not drawn at all here.
    /// </summary>
    private void DrawDevSelect()
    {
        // Settled: the transition length into FEP_DEVSELECT is not evidenced.
        // Passing 1 keeps its settled rendering while both New Game and Load Game
        // use the Client-owned page state.
        DrawMainUnderlay(1f);

        // Faint crosshair guides, present on this page and on the retail main
        // menu; the reconstruction has not drawn them anywhere before now.
        DrawRect(new Rect2(123f, 0f, 1f, DesignHeight), DevSelectGuide);
        DrawRect(new Rect2(0f, 180f, DesignWidth, 1f), DevSelectGuide);

        // FrontEnd.cpp:891-892 — FET3_SELECT_BRACKET1 at SELECT_BRACKET_X/Y
        // (328,343) with SELECT_BRACKET_SCALE 1.25, plus its +5/+10 shadow at
        // scale*1.05 in 0x3F000000. FEP_DEVSELECT is one of the pages
        // got_standard_SlidingTextBordersAndMask() returns TRUE for
        // (FrontEnd.cpp:780), which pins transition to 1 and therefore this
        // settled scale. The outside bracket only draws while dest == FEP_MAIN.
        // MEASURED (re-fit 2026-07-26, tools/frontend_arc_bracket_fit.py): this
        // page really does use SELECT_BRACKET_SCALE2 1.4, but the centre it was
        // previously paired with was wrong. Fitting the bracket alpha mask over
        // 4,000 sampled retail arc pixels peaks at 100.0% coverage at scale 1.40,
        // centre (329,344) — i.e. SELECT_BRACKET_X/Y (328,343) within 1px, the
        // same centre DrawLevelSelect uses. Scale 1.25 reaches only 13.8% there.
        // The response surface is single-peaked: 1.38 -> 97.3, 1.39 -> 99.2,
        // 1.40 -> 100.0, 1.41 -> 95.0. Robust across alpha threshold 16..200,
        // three sample seeds, and thin-run filter 3..20.
        //
        // The earlier 83.3%/1.39/(328,336) fit was contaminated by the level-map
        // episode curves and the FET3_HEADER_BRACKET1 end-caps; a geometric
        // thin-run filter removes both (the arc band is >=25px wide on every
        // scanline it occupies). The same method, run first as a mandatory
        // control on SELECT LEVEL, reaches 99.7% at 1.25/(329,344) — reproducing
        // that page's source constants and beating its previously recorded 96.1%.
        // So both pages are one texture at one centre with two source scales.
        const float bracketScale = 1.4f;
        const float bracketShadowScale = bracketScale * ShadowScaleBoost;
        DrawSurfaceCentered(_levelBracket01, 333f, 353f, bracketShadowScale, bracketShadowScale, ShadowTint);
        DrawSurfaceCentered(_levelBracket01, 328f, 343f, bracketScale, bracketScale, BracketTint);

        // Header text box then the centred title.
        //
        // MEASURED 2026-07-26 — the title is font22 at scale 1, NOT Font13PS at
        // 1.5. Atlas-free proof, from the pristine captures alone: cut the header
        // title band (y70..92, x200..580, ink threshold >120) on all four header
        // pages, segment it into per-glyph column runs, and compare glyphs of the
        // SAME LETTER between pages at 1:1 with no rescaling.
        //
        //   page                  ink rows   ink x        per-glyph run widths
        //   MISSION BRIEFING      72..88     288..490     17,2,13,13,2,15,12,...
        //   SELECT CONFIGURATION  72..88     249..526     13,11,10,11,13,14,...
        //   SELECT LEVEL          72..88     304..471     13,11,10,11,13,14,...
        //   CHOOSE GAME NAME      72..88     263..513     13,12,15,15,13,11,...
        //
        // All four have identical 17-row ink height, and SELECT LEVEL's first six
        // glyph widths are byte-identical to SELECT CONFIGURATION's ("SELECT" on
        // both). Per-letter mask IoU at 1:1 against SELECT CONFIGURATION:
        // MISSION BRIEFING 0.992 (8 letters), SELECT LEVEL 0.990 (5 letters),
        // CHOOSE GAME NAME 0.979 (7 letters). Font13PS at 1.5 would have to be a
        // rescale of a 16px cell and could not land on the same integer glyph
        // widths as a 32px cell at 1:1; it does not.
        //
        // MISSION BRIEFING and SELECT CONFIGURATION are already drawn in font22
        // at scale 1 here (NCC 0.951 fit, DrawHeaderBarTitle), so this page and
        // SELECT LEVEL are corrected to the same call.
        //
        // BASELINE MOVED: this page's pinned no-regression capture changes in the
        // header band. That is intended and is the point of the change.
        DrawRect(new Rect2(191f, 69f, 394f, 21f), HeaderBoxTint);
        float titleWidth = MeasureFont22Text(DevSelectTitle, 1f);
        DrawFont22Text(
            DevSelectTitle,
            new Vector2(HeaderBarCenterX - (titleWidth * 0.5f), HeaderTitleTop),
            1f,
            1f,
            ReleasedTitleText);

        // List panel: border, interior, scrollbar divider and thumb.
        DrawRect(new Rect2(128f, 130f, 403f, 272f), DevSelectPanelBorder);
        DrawRect(new Rect2(129f, 131f, 401f, 270f), DevSelectPanelFill);
        DrawRect(new Rect2(129f, 180f, 401f, 1f), DevSelectGuideOverPanel);
        DrawRect(new Rect2(510f, 131f, 1f, 270f), DevSelectScrollDivider);
        DrawScrollThumbOutline(new Rect2(515f, 135f, 11f, 262f));

        for (int index = 0; index < _session.CareerNames.Count; index++)
        {
            float rowTop = DevSelectRowTop + (index * DevSelectRowPitch);
            if (rowTop + (GlyphCellSize * DevSelectRowScale) > 400f)
            {
                break;
            }

            DrawText(
                _session.CareerNames[index],
                new Vector2(DevSelectRowX, rowTop),
                DevSelectRowScale,
                index == _session.SelectedCareerIndex ? ReleasedTitleText : DevSelectRowText);
        }

        // Name field: border, interior, selection highlight, then the name.
        DrawRect(new Rect2(128f, 408f, 403f, 44f), DevSelectFieldBorder);
        DrawRect(new Rect2(129f, 409f, 401f, 42f), DevSelectPanelFill);

        float nameWidth = Math.Max(0, _session.GameName.Sum(character =>
            _glyphWidths[RetailFrontendSession.GameNameRenderGlyphIndex(character, swapInvertedPunctuation: false)] + 1) - 1) * DevSelectRowScale;
        var nameOrigin = new Vector2(329.5f - (nameWidth * 0.5f), DevSelectNameTop);
        if (_session.GameNameIsFresh)
            DrawRect(new Rect2(nameOrigin.X - 4f, 415f, nameWidth + 8f, 31f), DevSelectNameHighlight);
        DrawAtlasText(_titleFont, _glyphWidths, GlyphCellSize, GlyphColumns,
            _session.GameName, nameOrigin, DevSelectRowScale, DevSelectRowScale, ReleasedTitleText,
            dropShadow: true, retailNameGlyphs: true);

        // Page chevrons. FE_Arrow points right and its artwork occupies only
        // (16,12)-(46,52) of the 64x64 texture, exactly as DrawLanguageSelector
        // measured; the left chevron is the mirrored draw.
        // Measured extents: right chevron x604..631 y437..472, left chevron the
        // mirrored pair six rows lower; both render in the same lit metal as the
        // arcs (~(107,117,131)), not the faint ChromeTint the language selector uses.
        var arrowSource = new Rect2(16f, 12f, 30f, 40f);
        DrawTextureRectRegion(_feArrow, new Rect2(36f, 443f, -27f, 35f), arrowSource, BracketTint);
        DrawTextureRectRegion(_feArrow, new Rect2(604f, 437f, 27f, 35f), arrowSource, BracketTint);
    }

    /// <summary>1px outline for the list scrollbar thumb.</summary>
    private void DrawScrollThumbOutline(Rect2 rect)
    {
        DrawRect(new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, 1f), DevSelectScrollThumb);
        DrawRect(
            new Rect2(rect.Position.X, rect.Position.Y + rect.Size.Y - 1f, rect.Size.X, 1f),
            DevSelectScrollThumb);
        DrawRect(new Rect2(rect.Position.X, rect.Position.Y, 1f, rect.Size.Y), DevSelectScrollThumb);
        DrawRect(
            new Rect2(rect.Position.X + rect.Size.X - 1f, rect.Position.Y, 1f, rect.Size.Y),
            DevSelectScrollThumb);
    }

    /// <summary>
    /// Retail FEP_LEVEL_SELECT — the "SELECT LEVEL" episode/level graph reached
    /// from the CHOOSE GAME NAME page.
    ///
    /// The released source in references/Onslaught/ declares CFEPLevelSelect but
    /// does not ship its implementation (there is no FEPLevelSelect.cpp), so the
    /// page body carries NO source geometry at all. Everything below is measured
    /// from the pristine 640x480 capture
    /// local-lab/retail-reference-pristine/select-level/04-select-level-640x480.png
    /// by scanning for pixels that differ from the page background.
    ///
    ///   page background      flat (23,23,48)  — same fill proven for the main menu
    ///                        (228,102 of 307,200 pixels are exactly that value)
    ///   header text box      interior (12,12,24), x191..584, y69..89 — byte-identical
    ///                        extents and colour to the FEP_DEVSELECT header
    ///   title "SELECT LEVEL" ink x304..471, y73..87; 'S' has atlas bearing (0,3), so
    ///                        origin (304, 68.5) at scale 1.5, i.e. centred on x=390
    ///                        exactly as HEADER_BAR_X (FrontEnd.cpp:1103) requires
    ///   "Episode 1"          white (254,254,254), ink x130..237 y133..148, scale 1.4
    ///   "1.00 - Training..." (254,222,126), ink x130..374 y161..176, scale 1.4
    ///   column labels 1/2/3  (62,157,253) = ReleasedBlue exactly, left edges
    ///                        x=163 / 283 / 524, ink top y=187, scale 1.5
    ///   faint guides         (50,51,72) at x=123 and y=180 — same crosshair as
    ///                        FEP_DEVSELECT and the main menu
    ///   node ring outer      diameter 41 (vertical profile y300..340 at x=208)
    ///   node columns         x = 148 + 60k, k = 0..7; rows y = 290 / 320 / 350
    ///   current node ring    blue band r20..26 at (148,320), peak (123,146,189)
    ///   link lines           core (49,50,71), one pixel wide with ±1 antialiasing
    ///   arc brackets         see below
    ///   chevrons             left x9..36 y438..473, right x604..631 y437..472
    ///
    /// Scale evidence, since two different text scales are in play: summing the
    /// measured per-glyph advances gives 158px from the title's first 'S' to its
    /// last 'L' against 160.5 predicted at 1.5 and 149.8 at 1.4, while the level
    /// name gives 243px against 261.5 at 1.5 and 244.1 at 1.4. Title 1.5, body 1.4
    /// — the same split FEP_DEVSELECT uses.
    ///
    /// KNOWN GAPS, left undrawn rather than approximated, with their measured cost:
    ///   * the blue Forseti emblem at top-left (~x48..160, y20..190) has no
    ///     materialized texture;
    ///   * the metal header end-cap brackets (retail x176..205 and x568..600) are
    ///     the same unidentified FET3_HEADER_BRACKET art FEP_DEVSELECT lacks;
    ///   * the mottled amber "current level" disc inside the highlighted node
    ///     (x138..158, y310..332) is a textured sprite, not a flat fill — its
    ///     texels run (205,161,105) to (253,243,164) — and no materialized asset
    ///     matches it, so the node is drawn as rings with an empty centre;
    ///   * the faint Forseti writing outlines around x55..135, y0..170. Fitting
    ///     FE_Forseti_Writing_large over that band scores only 0.12 correlation at
    ///     its best scale/offset, so this lane cannot claim it is that texture.
    /// </summary>
    private void DrawLevelSelect()
    {
        // Settled, for the same reason as DrawDevSelect.
        DrawMainUnderlay(1f);

        DrawRect(new Rect2(123f, 0f, 1f, DesignHeight), DevSelectGuide);
        DrawRect(new Rect2(0f, 180f, DesignWidth, 1f), DevSelectGuide);

        DrawLevelSweepArcs();
        // RetailLevelSelectFsub148: CFEPLevelSelect::Render leftover
        // after the sliding-borders call is fld [0x005DB53C] (148.0)
        // fsub [esi+0x3460] fstp [esp+0x14]. Official 74154bfa
        // independently re-read this cycle. The local is a window,
        // not dest. 0x00460BE4 fcomp 610.0 / 0x00460BF9 fcomp 0.0.
        // Init fstp [esi+0x3460] at 0x00460464 follows fild of the
        // zeroed [esi+0x3468]. Settled pad is 148.0 - 0. Dest stays
        // the measured node centres. Do not invent dest from 148.0.
        if (RetailLevelSelectFsub148.Applies(
                RetailLevelSelectFsub148.Pad(
                    RetailLevelSelectFsub148.SettledField)))
        {
            // RetailLevelSelectFsub10: later leftover is
            // fld [esp+0x14] / fsub [0x005D85CC] (10.0) /
            // fstp [esp] before call 0x005563D0. Official
            // 74154bfa independently re-read this cycle:
            // 0x00460C94 is d825cc855d00 fsub, not fld.
            // Settled 148.0-10.0 is 138.0. That is not dest.
            // The 322.0 push at 0x00460CE1 is later. Do not
            // invent dest from 10.0.
            _ = RetailLevelSelectFsub10.Delta(
                RetailLevelSelectFsub148.SettledPad);
            // RetailLevelSelectLater148: later leftover after the
            // 10.0 fsub is the second identical fld 148.0 /
            // fsub [esi+0x3460] / fstp [esp+0x14] triple at
            // 0x00460E24. Official 74154bfa independently
            // re-read this cycle. First consumers are
            // 0x00460E9D fcomp 0x00629390 and 0x00460EB0
            // fcomp 0x00629394. The local is a later window,
            // not dest. Do not invent dest from those
            // compares. Dest stays the measured node centres.
            if (RetailLevelSelectLater148.Applies(
                    RetailLevelSelectLater148.Pad(
                        RetailLevelSelectLater148.SettledField)))
            {
                DrawLevelNodeGraph();
                // RetailLevelSelectLaterEsp94: later leftover
                // after the later 148.0 triple is
                // fld [esp+0x94] / fsub [0x005D8BC4] (0.75) /
                // fmul [0x005D85BC] (4.0) / fcom [0x005D856C]
                // (0.0). Official 74154bfa independently
                // re-read this cycle. Different stack local,
                // not dest. First store consumer is
                // 0x00460E4D fst [esp+0x40]. Do not invent
                // dest from 0.75 or 4.0.
                _ = RetailLevelSelectLaterEsp94.Subtrahend;
                _ = RetailLevelSelectLaterEsp94.Factor;
                // RetailLevelSelectLaterOne: later leftover
                // after the later [esp+0x94] shift is
                // fcom [0x005D8568] (1.0) / fmul [0x005D8C70]
                // (255.0). Official 74154bfa independently
                // re-read this cycle. First store consumer is
                // 0x00460E7F fistp [esp+0x4C]. That is a
                // clamp-and-scale of the shifted local, not
                // dest. The later 610.0/0.0 pair at
                // 0x00460F30 is RetailLevelSelectLater610.
                // Do not invent dest from 1.0 or 255.0.
                // Do not invent a fade.
                _ = RetailLevelSelectLaterOne.CompareOne;
                _ = RetailLevelSelectLaterOne.Scale;
                // RetailLevelSelectLater610: later leftover
                // after the later 1.0 fcom is
                // fld [esp+0x14] / fcomp [0x005DB5B0]
                // (610.0) / fld [esp+0x14] /
                // fcomp [0x005D856C] (0.0). Official
                // 74154bfa independently re-read this
                // cycle. First consumer is 0x00460F5A
                // mov eax, [esi+ebx*4+0x4]. First store
                // is 0x00460F5E mov [esp+0x28], 0. That
                // is a later window on the same
                // [esp+0x14] local, not dest. The later
                // fmul 60.0 at 0x00460F73 is
                // RetailLevelSelectLater60. Do not invent
                // dest from 610.0 or 0.0.
                _ = RetailLevelSelectLater610.Applies(
                    RetailLevelSelectLater148.Pad(
                        RetailLevelSelectLater148.SettledField));
                _ = RetailLevelSelectLater610.WindowHigh;
                _ = RetailLevelSelectLater610.WindowLow;
                // RetailLevelSelectLater60: later leftover
                // after the later 610.0/0.0 pair is
                // fild [esp+0x3C] / fmul [0x005DB538]
                // (60.0) / fmul [0x005D85EC] (0.5) /
                // fadd [0x005DB3E8] (320.0) / fstp
                // [esp+0x18]. Official 74154bfa
                // independently re-read this cycle. First
                // store is 0x00460F85 fstp [esp+0x18].
                // That is a scaled local, not dest. The
                // later fld [esp+0x94] / fcomp 1.0 at
                // 0x00460FD8 is
                // RetailLevelSelectLaterEsp94One. Do not
                // invent dest from 60.0, 0.5, or 320.0.
                _ = RetailLevelSelectLater60.Factor;
                _ = RetailLevelSelectLater60.Half;
                _ = RetailLevelSelectLater60.Addend;
                // RetailLevelSelectLaterEsp94One: later leftover
                // after the later 60.0/0.5/320.0 scale is
                // fld [esp+0x94] / fcomp [0x005D8568]
                // (1.0). Official 74154bfa independently
                // re-read this cycle. First consumer of
                // the equal-1 fall-through is 0x00460FEC
                // fld [esp+0x18]. That is a compare of
                // the [esp+0x94] local, not dest. The
                // later fadd 20.0 at 0x00460FF0 is later.
                // Do not invent dest from 1.0 or 20.0.
                _ = RetailLevelSelectLaterEsp94One.Applies(
                    RetailLevelSelectLaterEsp94One.CompareOne);
                _ = RetailLevelSelectLaterEsp94One.CompareOne;
                // RetailLevelSelectLater20: later leftover after
                // the later [esp+0x94]/fcomp 1.0 compare is
                // fld [esp+0x18] / fadd [0x005D857C] (20.0) /
                // fstp [esp]. Official 74154bfa independently
                // re-read this cycle. First store is 0x00460FF7
                // fstp [esp]. That is an addend on the later-60
                // local, not dest. The later second fadd 20.0 at
                // 0x00460FFE is later. Do not invent dest from
                // 20.0 or 1.0.
                _ = RetailLevelSelectLater20.Offset(
                    RetailLevelSelectLater60.Scaled(1));
                _ = RetailLevelSelectLater20.Addend;
                // RetailLevelSelectLaterFadd20: later leftover
                // after the later first 20.0 addend is
                // fld [esp+0x18] / fadd [0x005D857C] (20.0) /
                // fstp [esp]. Official 74154bfa independently
                // re-read this cycle. First store is 0x00461005
                // fstp [esp]. That is a second addend on a later
                // [esp+0x18] local, not dest. The later fsub
                // 20.0 at 0x0046100C is later. Do not invent
                // dest from 20.0 or 1.0.
                _ = RetailLevelSelectLaterFadd20.Offset(
                    RetailLevelSelectLater20.Offset(
                        RetailLevelSelectLater60.Scaled(1)));
                _ = RetailLevelSelectLaterFadd20.Addend;
                // RetailLevelSelectLaterFsub20: later leftover
                // after the later second 20.0 addend is
                // fld [esp+0x20] / fsub [0x005D857C] (20.0) /
                // fstp [esp]. Official 74154bfa independently
                // re-read this cycle. First store is 0x00461013
                // fstp [esp]. That is a subtrahend on a later
                // [esp+0x20] local, not dest. The later second
                // fsub 20.0 at 0x0046101A is later. Do not
                // invent dest from 20.0 or 1.0.
                _ = RetailLevelSelectLaterFsub20.Offset(
                    RetailLevelSelectLater60.Scaled(1));
                _ = RetailLevelSelectLaterFsub20.Subtrahend;
            }
        }

        for (int index = 0; index < LevelColumnLabels.Length; index++)
        {
            (string text, float x) = LevelColumnLabels[index];
            DrawText(
                text,
                new Vector2(x, LevelSelectColumnLabelTop),
                LevelSelectColumnLabelScale,
                ReleasedBlue);
        }

        // RetailLevelSelectSlidingBorders: CFEPLevelSelect::Render first
        // leftover is the unique call 0x00460B61 to
        // CFrontEnd__DrawSlidingTextBordersAndMask. FrontEnd.cpp:891-892 —
        // FET3_SELECT_BRACKET1 at SELECT_BRACKET_X/Y (328,343) with
        // SELECT_BRACKET_SCALE 1.25, plus its +5/+10 shadow at scale*1.05 in
        // 0x3F000000. FEP_LEVEL_SELECT is one of the pages
        // got_standard_SlidingTextBordersAndMask() returns TRUE for
        // (FrontEnd.cpp:783), which pins transition to 1 and therefore this
        // settled scale; the outside bracket only draws while dest == FEP_MAIN.
        // The 148.0 fsub at 0x00460B66 is RetailLevelSelectFsub148
        // and is not dest.
        //
        // MEASURED, and this page does NOT reproduce the 1.4 the FEP_DEVSELECT
        // build settled on: fitting the FE_select_level_bracket01 alpha mask over
        // 4,000 sampled retail metal pixels (header band, emblem, and chevrons
        // excluded) peaks at 96.1% overlap at scale 1.25, centre (329,343), and
        // the whole 1.10-1.74 scale sweep has its maximum there. Scale 1.4 is not
        // a local optimum on this frame. The source constants are therefore used
        // verbatim. The shadow reproduces exactly: 0x3f000000 over (23,23,48) is
        // (17,17,36), which is the third most common colour in the capture.
        if (RetailLevelSelectSlidingBorders.Applies(
                standardPage: true,
                fromVirtualKeyboard: false))
        {
            const float bracketScale = RetailLevelSelectSlidingBorders.SettledInsideScale;
            const float bracketShadowScale = bracketScale * ShadowScaleBoost;
            DrawSurfaceCentered(_levelBracket01, 333f, 353f, bracketShadowScale, bracketShadowScale, ShadowTint);
            DrawSurfaceCentered(_levelBracket01, 328f, 343f, bracketScale, bracketScale, BracketTint);
        }

        // font22 at scale 1, for the glyph-run and per-letter-IoU evidence
        // written out in DrawDevSelect. Retail's ink here is x304..471, y72..88,
        // and its "SELECT" glyph widths are byte-identical to the SELECT
        // CONFIGURATION title that is already drawn in font22.
        //
        // BASELINE MOVED: this page's pinned capture changes in the header band.
        DrawRect(new Rect2(191f, 69f, 394f, 21f), HeaderBoxTint);
        float titleWidth = MeasureFont22Text(_selectLevelText, 1f);
        DrawFont22Text(
            _selectLevelText,
            new Vector2(HeaderBarCenterX - (titleWidth * 0.5f), HeaderTitleTop),
            1f,
            1f,
            ReleasedTitleText);

        DrawText(
            LevelSelectEpisodeText,
            new Vector2(LevelSelectEpisodeLeft, LevelSelectEpisodeTop),
            LevelSelectBodyScale,
            Colors.White);
        // The name band follows the SELECTED node: the language table carries
        // an N.NN row for every career world (english-worlds.json slot law),
        // and retail's own selector reads the loaded career — the band showing
        // the selected node's row is the only law consistent with shipped
        // data (PARITY.md 2026-08-22; FEPLevelSelect.cpp is not in the drop).
        DrawText(
            _session.SelectedLevelName,
            new Vector2(LevelSelectEpisodeLeft, LevelSelectLevelNameTop),
            LevelSelectBodyScale,
            ReleasedSelected);

        // Same FE_Arrow content region and lit-metal tint as FEP_DEVSELECT; both
        // chevrons sit at the same y on this page, unlike the dev-select frame.
        var arrowSource = new Rect2(16f, 12f, 30f, 40f);
        DrawTextureRectRegion(_feArrow, new Rect2(9f, 438f, -28f, 36f), arrowSource, BracketTint);
        DrawTextureRectRegion(_feArrow, new Rect2(604f, 437f, 28f, 36f), arrowSource, BracketTint);
    }

    /// <summary>
    /// The three episode sweep curves behind the node graph.
    ///
    /// A least-squares circle fitted to 279 intensity-weighted centre points of
    /// the middle curve — one per row from y=183 to y=461, the span over which it
    /// is unobstructed — gives centre (485.84, 320.38), radius 249.28 and apex
    /// x=236.56, with a maximum residual of 1.81px (and under 0.4px above y=440).
    /// The other two are the same circle offset by -120 and +240 in x, which the
    /// independently measured apexes confirm: 116.5 / 236.5 / 476.5 against
    /// 116.56 / 236.56 / 476.56 predicted. The drawn span is the measured one.
    ///
    /// This is a measured geometric reconstruction, not an identified sprite: the
    /// retail art that draws these curves has not been located, and the colour is
    /// the measured line core (133,133,158). Retail's own line reads as an
    /// additive composite (its delta over the background is equal in all three
    /// channels), which this alpha-blended draw does not reproduce.
    /// </summary>
    private void DrawLevelSweepArcs()
    {
        for (int index = 0; index < SweepArcCenters.Length; index++)
        {
            DrawArc(
                new Vector2(SweepArcCenters[index], SweepArcCenterY),
                SweepArcRadius,
                SweepArcStartAngle,
                SweepArcEndAngle,
                96,
                index == 0 ? LevelSweepArcCurrent : LevelSweepArcOther,
                SweepArcWidth,
                antialiased: true);
        }
    }

    /// <summary>
    /// The level-node graph: link lines first, then the rings that occlude them.
    ///
    /// Links are trimmed to the node outer radius rather than run centre-to-centre
    /// because retail's rings are open in the middle and no line shows through
    /// them — the y=320 link between the x=208 and x=268 nodes is visible only
    /// across x230..250, which is exactly edge to edge.
    ///
    /// Ring identity is measured, not assumed. Correlating the reference against
    /// each candidate alpha mask over a 47x47 box around an isolated node scores
    /// FE_select_level_ring_bracket01 at 0.63 (best drawn size 64) against
    /// ring_bracket02 at 0.56 (best size 84), and the colour ratio settles it —
    /// see <see cref="LevelNodeRingTint"/>. The current node adds ring_bracket01
    /// at full brightness, best size 79 centred (148,321), over ring_bracket02 at
    /// best size 62; both fits are peaks of an exhaustive size sweep.
    /// </summary>
    private void DrawLevelNodeGraph()
    {
        foreach ((int from, int to) in LevelNodeLinks)
        {
            Vector2 a = LevelNodes[from];
            Vector2 b = LevelNodes[to];
            Vector2 direction = (b - a).Normalized();
            float radiusA = from == 0 ? CurrentNodeOuterRadius : NodeOuterRadius;
            float radiusB = to == 0 ? CurrentNodeOuterRadius : NodeOuterRadius;
            DrawLine(
                a + (direction * radiusA),
                b - (direction * radiusB),
                LevelLinkLine,
                NodeLinkWidth,
                antialiased: true);
        }

        for (int index = 1; index < LevelNodes.Length; index++)
        {
            DrawSurfaceCentered(
                _levelRing01,
                LevelNodes[index].X,
                LevelNodes[index].Y,
                NodeRingSize / _levelRing01.GetWidth(),
                NodeRingSize / _levelRing01.GetHeight(),
                LevelNodeRingTint);
        }

        Vector2 current = LevelNodes[0];
        DrawSurfaceCentered(
            _levelRing01,
            current.X,
            current.Y,
            CurrentNodeRingSize / _levelRing01.GetWidth(),
            CurrentNodeRingSize / _levelRing01.GetHeight(),
            BracketTint);
        DrawSurfaceCentered(
            _levelRing02,
            current.X,
            current.Y,
            CurrentNodeInnerRingSize / _levelRing02.GetWidth(),
            CurrentNodeInnerRingSize / _levelRing02.GetHeight(),
            BracketTint);
    }

    /// <summary>
    /// The stage both MISSION BRIEFING and SELECT CONFIGURATION compose over:
    /// FE_Rock_Background under FE_select_level_bracket02. Neither page uses the
    /// flat (23,23,48) fill the menu pages do.
    ///
    /// BACKGROUND, measured. The underlay is FE_Rock_Background — the same
    /// texture already materialized for the click-to-start lane — drawn at 1.25
    /// (1280x640) from (-70,-80). Method: a high-pass normalised
    /// cross-correlation of a 140x100 soldier-cluster template from the briefing
    /// frame against the texture over a 1.0-4.5 scale sweep peaks at 0.82 in a
    /// 1.24-1.26 plateau, and an independent per-channel least-squares
    /// background fit over four disjoint scene bands lands on scale 1.25,
    /// origin (-70,-80) as its residual minimum. Sub-pixel origin is not
    /// resolvable from one frame: the fit's offset grid is integral.
    ///
    /// RING, measured. FE_select_level_bracket02 — decoded, it is a full metal
    /// annulus, not an arc — fitted by maximising the fraction of retail's
    /// near-neutral bright pixels (max>85, max-min<22, header/text/chevron bands
    /// excluded) that agree with the texture's alpha>96 mask, scored as
    /// INTERSECTION OVER UNION rather than coverage. Coverage alone has no
    /// penalty for a ring that is too large and it first returned 996x996 @
    /// (270,208), which put the drawn ring's inner edge 11px right of retail's
    /// at y=240. Under IoU the peak is 0.760 at 990x990 centred (267,221) on
    /// the briefing frame and 0.773 at the SAME size and centre on the
    /// configuration frame — two independent frames agreeing to the sweep step
    /// is what makes this a measurement rather than a fit artifact.
    ///
    /// The arc-scale dispute recorded in STARTUP-FLOW-FINDINGS does NOT extend
    /// to these pages: they do not draw FE_select_level_bracket01 at all, so
    /// neither 1.25 nor 1.4 is in play here.
    ///
    /// KNOWN GAPS, left undrawn rather than approximated:
    ///   * the background is ANIMATED. The two reference frames disagree by
    ///     9.4% material pixels in a clean sky band and the configuration
    ///     frame's own background best-fit lands at scale 1.275, origin
    ///     (-110,-86) rather than the briefing's 1.25/(-70,-80). This draw is
    ///     static and pinned to the briefing frame, so the configuration page
    ///     carries the full pan error as measured cost.
    ///   * the blue Forseti emblem at top-left, unidentified here as it is on
    ///     every other page.
    /// </summary>
    private void DrawBriefingStage()
    {
        DrawTextureRect(
            _rockBackground,
            new Rect2(
                BriefingBackgroundLeft,
                BriefingBackgroundTop,
                _rockBackground.GetWidth() * BriefingBackgroundScale,
                _rockBackground.GetHeight() * BriefingBackgroundScale),
            false,
            BriefingBackgroundTint);

        DrawSurfaceCentered(
            _levelBracket02,
            BriefingRingCenterX,
            BriefingRingCenterY,
            BriefingRingSize / _levelBracket02.GetWidth(),
            BriefingRingSize / _levelBracket02.GetHeight(),
            BriefingRingTint);
    }

    /// <summary>
    /// Bottom page chevrons. Measured extents on both pages are the ones
    /// FEP_LEVEL_SELECT already carries: left x9..36 y438..473, right
    /// x604..631 y437..472, and the configuration frame's right-chevron pixels
    /// are identical to the level-select frame's.
    /// </summary>
    private void DrawPageChevrons()
    {
        var arrowSource = new Rect2(16f, 12f, 30f, 40f);
        DrawTextureRectRegion(_feArrow, new Rect2(9f, 438f, -28f, 36f), arrowSource, BracketTint);
        DrawTextureRectRegion(_feArrow, new Rect2(604f, 437f, 28f, 36f), arrowSource, BracketTint);
    }

    /// <summary>
    /// Settled retail <c>FEP_DEBRIEFING</c> projection. The page body is ported
    /// from pristine <c>CFEPDebriefing::Render</c>
    /// (<c>0x00456DD0..0x00457CED</c>), not inferred from the partial source
    /// drop. Its common pre-pass calls <c>CFrontEnd::RenderPreCommonFade</c>, so
    /// this uses the existing FEBack common underlay rather than the briefing
    /// page's separate rock scene.
    ///
    /// <para>The exact settled body represented here is: level at (130,149),
    /// mission status at y=184, primary at y=210 when defined, secondary at
    /// y=226 after primary or y=210 by itself, grade label at (130,295), and
    /// grade art centred at (320,310). The value column is 20 pixels after the
    /// widest of the three untranslated label extents. No kill table exists in
    /// this Render body.</para>
    ///
    /// <para>Still open rather than approximated: entry/exit interpolation,
    /// overlay and goodie particle/message effects, the delayed grade glint,
    /// retail Z-function-to-Canvas compositing equivalence for the grade
    /// foreground, and pixel validation against a pristine debriefing capture.
    /// The grade texture and submitted settled colors are exact; Level 100's
    /// current S input remains the reconstruction's unmeasured score/time
    /// shortcut.</para>
    /// </summary>
    private void DrawDebriefing()
    {
        RetailDebriefingProjection debriefing = _session.Debriefing
            ?? throw new InvalidOperationException(
                "The debriefing screen has no END_LEVEL_DATA projection.");

        DrawMainUnderlay(1f);
        DrawSurfaceCentered(
            _debriefingMetalRing,
            DebriefingRingCenterX,
            DebriefingRingCenterY,
            DebriefingRingScale,
            DebriefingRingScale,
            Colors.White);
        DrawDebriefingWritingChrome();

        string levelName = RetailFrontendWorldStrings.LevelName(debriefing.WorldFinished)
            ?? _session.SelectedLevelName;
        DrawFont22Text(
            levelName,
            new Vector2(DebriefingTextLeft, DebriefingLevelTop),
            1f,
            1f,
            Colors.White);

        const string missionStatusLabel = "Mission Status";
        const string primaryLabel = "Primary Objectives";
        const string secondaryLabel = "Secondary Objectives";
        float valueLeft = DebriefingTextLeft
            + MathF.Max(
                MeasureText(missionStatusLabel, 1f),
                MathF.Max(
                    MeasureText(primaryLabel, 1f),
                    MeasureText(secondaryLabel, 1f)))
            + DebriefingValueGap;

        DrawText(
            missionStatusLabel + ": ",
            new Vector2(DebriefingTextLeft, DebriefingMissionStatusTop),
            1f,
            DebriefingLabelText);
        DrawText(
            DebriefingMissionStatusText(debriefing.MissionStatus),
            new Vector2(valueLeft, DebriefingMissionStatusTop),
            1f,
            DebriefingMissionStatusColor(debriefing.MissionStatus));

        if (debriefing.PrimaryObjectives != RetailDebriefingObjectiveSummary.Hidden)
        {
            DrawDebriefingObjectiveRow(
                primaryLabel,
                debriefing.PrimaryObjectives,
                DebriefingPrimaryTop,
                valueLeft);
        }

        if (debriefing.SecondaryObjectives != RetailDebriefingObjectiveSummary.Hidden)
        {
            float secondaryTop =
                debriefing.PrimaryObjectives == RetailDebriefingObjectiveSummary.Hidden
                    ? DebriefingPrimaryTop
                    : DebriefingSecondaryAfterPrimaryTop;
            DrawDebriefingObjectiveRow(
                secondaryLabel,
                debriefing.SecondaryObjectives,
                secondaryTop,
                valueLeft);
        }

        if (debriefing.GradeByte is byte gradeByte)
        {
            DrawFont22Text(
                "Grade:",
                new Vector2(DebriefingTextLeft, DebriefingGradeLabelTop),
                1f,
                1f,
                Colors.White);

            DrawSurfaceCentered(
                _symbolBracket01,
                DebriefingGradeCenterX + 5f,
                DebriefingGradeCenterY + 10f,
                1.3125f,
                1.3125f,
                ShadowTint);
            DrawSurfaceCentered(
                _symbolBracket01,
                DebriefingGradeCenterX,
                DebriefingGradeCenterY,
                1.25f,
                1.25f,
                DebriefingGradeBodyTint);

            Texture2D grade = DebriefingGradeTexture(gradeByte);
            DrawSurfaceCentered(
                grade,
                DebriefingGradeCenterX + 3f,
                DebriefingGradeCenterY + 3f,
                1f,
                1f,
                ShadowTint);
            DrawSurfaceCentered(
                grade,
                DebriefingGradeCenterX,
                DebriefingGradeCenterY,
                1f,
                1f,
                DebriefingGradeBodyTint);
        }

        DrawHeaderBarTitle(DebriefingTitle);
    }

    private void DrawDebriefingWritingChrome()
    {
        // FEPShared::RenderSelectionBrackets 0x00452FD0 is misleadingly named:
        // it draws four FE_Forseti_Writing_large surfaces at x=86, scale 0.5,
        // 180 pixels apart. Its first y is
        // 90 - fmod(frontend_counter * 0.3, 180). The current presenter has no
        // proven mapping for that counter, so it uses the exact cold-BSS phase
        // (y=90) rather than inventing a clock rate.
        for (int index = 0; index < 4; index++)
        {
            DrawSurfaceCentered(
                _forsetiWritingLarge,
                86f,
                90f + (index * 180f),
                0.5f,
                0.5f,
                ChromeTint);
        }
    }

    private void DrawDebriefingObjectiveRow(
        string label,
        RetailDebriefingObjectiveSummary summary,
        float top,
        float valueLeft)
    {
        DrawText(
            label + ": ",
            new Vector2(DebriefingTextLeft, top),
            1f,
            DebriefingLabelText);
        DrawText(
            summary == RetailDebriefingObjectiveSummary.Complete
                ? "Complete"
                : "Incomplete",
            new Vector2(valueLeft, top),
            1f,
            summary == RetailDebriefingObjectiveSummary.Complete
                ? DebriefingSuccessText
                : DebriefingFailureText);
    }

    private static string DebriefingMissionStatusText(
        RetailDebriefingMissionStatus status) => status switch
        {
            RetailDebriefingMissionStatus.Victory => "Victory",
            RetailDebriefingMissionStatus.Defeat => "Defeat",
            _ => "Aborted",
        };

    private static Color DebriefingMissionStatusColor(
        RetailDebriefingMissionStatus status) => status switch
        {
            RetailDebriefingMissionStatus.Victory => DebriefingSuccessText,
            RetailDebriefingMissionStatus.Defeat => DebriefingFailureText,
            _ => DebriefingAbortedText,
        };

    private Texture2D DebriefingGradeTexture(byte grade) => grade switch
    {
        (byte)'A' => _debriefingGradeTextures[0],
        (byte)'B' => _debriefingGradeTextures[1],
        (byte)'C' => _debriefingGradeTextures[2],
        (byte)'D' => _debriefingGradeTextures[3],
        (byte)'E' => _debriefingGradeTextures[4],
        (byte)'S' => _debriefingGradeTextures[5],
        _ => throw new InvalidDataException(
            $"Retail debriefing has no grade surface for byte 0x{grade:X2}."),
    };

    /// <summary>
    /// Retail MISSION BRIEFING, reached from SELECT LEVEL.
    ///
    /// The released source ships neither FEPBriefing.cpp nor its header (only
    /// the include in Frontend.h:14 and the CFEPBriefing member at
    /// Frontend.h:231), and there is no FEP_ page constant for it in any shipped
    /// header, so this page carries NO source geometry at all. Everything is
    /// measured from local-lab/retail-reference-pristine/mission-briefing/
    /// 05-mission-briefing-640x480.png.
    ///
    ///   header box       0x7f black over the scene, x191..584 y69..89 — the
    ///                    identical extent FEP_DEVSELECT and FEP_LEVEL_SELECT use
    ///   title            font22 scale 1, ink x288..490 y73..87, centred x=390
    ///   "1.00 - ..."     font22 sx 0.70 sy 1.00, ink x180..351 y126..145
    ///   body             Font13PS scale 1, ink left x=80/81, tops
    ///                    167,183,199,215,231,247 then 273,289
    ///   body colour      brightest ink (251,220,95)
    ///   chevrons         left x9..36 y438..473, right x604..631 y437..472
    ///
    /// KNOWN GAPS with their honest cause:
    ///   * The inset panel at x378..582 / y176..328 is NOT drawn and MUST NOT be
    ///     compared. Its interior is pure black in the reference only because
    ///     the capture ran with -skipfmv; that is the absence of retail's video,
    ///     not retail's drawn output. The frontend-regions file marks the region
    ///     EXCLUDED for the same reason.
    ///
    ///     MEASURED 2026-07-26, and the missing content is now identified.
    ///     Differencing the -skipfmv reference against the no-skipfmv control
    ///     (no-skipfmv-frontend/05n-mission-briefing-nofmv-b.png) shows 87.60%
    ///     of this page is pixel-identical between them; the ONLY substantial
    ///     difference is one rectangle, x380..580 y178..326, dark (mean
    ///     12,15,21) with the flag and a rendered scene (mean 119,126,145)
    ///     without it. That rectangle is 201 x 149. Every one of the 28 Bink
    ///     streams under data/video/briefings/ is 201x149, and the one named
    ///     PC_100_exact.vid — 396 frames at 25 fps — is Level 100's. So the
    ///     inset is a briefing video drawn at NATIVE 1:1 with no resampling, at
    ///     origin (380,177).
    ///
    ///     It stays undrawn: playing it needs a decode path and a clock policy
    ///     that do not exist yet, and drawing black "to match the reference"
    ///     would encode a -skipfmv artefact as product behaviour. Nothing on
    ///     this page is tinted to close the gap either.
    ///   * The metal header end-cap brackets and the top-left Forseti emblem are
    ///     the same unidentified art every other page lacks.
    /// </summary>
    private void DrawMissionBriefing()
    {
        DrawBriefingStage();
        DrawHeaderBarTitle(MissionBriefingTitle);

        DrawFont22Text(
            _session.SelectedLevelName,
            new Vector2(BriefingLevelNameLeft, BriefingLevelNameTop),
            BriefingLevelNameScaleX,
            BriefingLevelNameScaleY,
            ReleasedTitleText);

        // The briefing body is the SELECTED world's own authored pair from
        // the language table (nine-slot pool law; worlds 611/612/621/622
        // carry a measured third paragraph). The static transcription below
        // stays only as the byte-identical world-100 receipt for the case
        // where no localization document is loaded; an empty session body
        // must draw NOTHING, never another world's copy.
        IReadOnlyList<string> briefingBody =
            _session.SelectedBriefingBody.Count > 0
                ? _session.SelectedBriefingBody
                : BriefingBody;
        float y = BriefingBodyTop;
        foreach (string line in WrapBriefingParagraphs(briefingBody))
        {
            if (line.Length == 0)
            {
                y += BriefingParagraphGap;
                continue;
            }

            DrawText(line, new Vector2(BriefingBodyLeft, y), 1f, BriefingBodyText);
            y += BriefingBodyPitch;
        }

        DrawPageChevrons();
    }

    /// <summary>
    /// Word-wraps the briefing paragraphs at the measured retail ink ceiling.
    ///
    /// The pristine capture's longest briefing line carries 286px of this
    /// renderer's advances (283px measured ink, within the known +2..+6
    /// overshoot), so 286 is the widest line retail drew. Greedy wrapping —
    /// keep adding words while the line fits, else break — reproduces world
    /// 100's transcribed breaks exactly under that ceiling (asserted by
    /// <c>RetailFrontendFlowWrapTests</c>); other worlds get the same law
    /// rather than invented breaks.
    /// </summary>
    private IReadOnlyList<string> WrapBriefingParagraphs(
        IReadOnlyList<string> paragraphs)
    {
        var lines = new List<string>();
        foreach (string paragraph in paragraphs)
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            string current = string.Empty;
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = current.Length == 0
                    ? word
                    : current + " " + word;
                if (current.Length > 0 &&
                    MeasureText(candidate, 1f) > BriefingBodyInkCeiling)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }
        }

        return lines;
    }

    /// <summary>
    /// Retail SELECT CONFIGURATION — the page between briefing and loading that
    /// this reconstruction did not model at all until this change. Reference:
    /// local-lab/retail-reference-pristine/select-configuration/
    /// 06-select-configuration-640x480.png.
    ///
    ///   title            font22 scale 1, ink x249..526 y73..87; its advance
    ///                    width 280 from origin 249 centres on x=389
    ///   unit name        font22 scale 1, fitted origin (260.5, 99.5),
    ///                    ink x259..536 y107..127, white
    ///   mode headers     Font13PS scale 1 at x=280, ink tops 213 and 277,
    ///                    brightest ink (249,217,62)
    ///   weapon rows      Font13PS scale 1 at x=280, ink tops 229,245,293,309,
    ///                    white; row pitch 16, block gap 32
    ///
    /// KNOWN GAPS, left undrawn because no materialized asset matches them, with
    /// their measured extents so the cost is attributable:
    ///   * the circular unit render at roughly x64..256, y152..344 — it is a
    ///     live 3D view of the battle engine, not a sprite.
    ///
    ///     CORROBORATED 2026-07-26, and it corrects a recorded misreading.
    ///     Differencing the -skipfmv reference against the no-skipfmv control
    ///     (no-skipfmv-frontend/06n-select-configuration-nofmv.png) leaves this
    ///     page 90.28% pixel-identical: the painted landscape backdrop is the
    ///     SAME in both, so this page has no video background. All the
    ///     difference above threshold 64 falls in x68..270 y218..367, which is
    ///     this window, and the two frames show the unit in JET form and in
    ///     WALKER form. The earlier reading of "the two reference frames
    ///     disagree by 9.4% in a clean sky band, so the background is animated"
    ///     came from a sample band that was not clean — it contained this
    ///     render. That is the project's region-mean failure mode again, and
    ///     the countermeasure is the one used here: localise the difference by
    ///     row/column density before interpreting it.
    ///   * the three circular mode icons at x449..541, y183..209;
    ///   * the green/red star rating glyphs at x448..545 on the four weapon
    ///     rows. They are 5-pointed star sprites, not the Font13PS asterisk:
    ///     the drawn stars are 5 rows tall with a solid body, and no atlas in
    ///     the materialized set contains them.
    /// </summary>
    private void DrawSelectConfiguration()
    {
        RetailFrontendBattleEngineConfiguration configuration = _session.SelectedConfiguration;

        DrawBriefingStage();
        DrawHeaderBarTitle(SelectConfigurationTitle);

        DrawFont22Text(
            configuration.DisplayName,
            new Vector2(ConfigurationUnitLeft, ConfigurationUnitTop),
            1f,
            1f,
            ReleasedTitleText);

        DrawConfigurationRows(
            "Walker Mode",
            configuration.WalkerPrimary,
            configuration.WalkerSecondary,
            ConfigurationWalkerTop);
        DrawConfigurationRows(
            "Jet Mode",
            configuration.JetPrimary,
            configuration.JetSecondary,
            ConfigurationJetTop);

        DrawPageChevrons();
    }

    private void DrawConfigurationRows(
        string modeName,
        RetailFrontendWeaponConfiguration primary,
        RetailFrontendWeaponConfiguration secondary,
        float top)
    {
        DrawText(
            modeName,
            new Vector2(ConfigurationRowLeft, top),
            1f,
            ConfigurationModeText);
        DrawText(
            primary.DisplayName,
            new Vector2(ConfigurationRowLeft, top + ConfigurationRowPitch),
            1f,
            ReleasedTitleText);
        DrawText(
            secondary.DisplayName,
            new Vector2(ConfigurationRowLeft, top + (2f * ConfigurationRowPitch)),
            1f,
            ReleasedTitleText);
    }

    /// <summary>
    /// Retail LOADING.
    ///
    /// Measured from local-lab/retail-reference-pristine/loading/
    /// 07-loading-640x480.png. The background is LoadingScreen.tga stretched to
    /// the full 640x480 stage: comparing the decoded texture resampled to
    /// 640x480 against the retail frame gives a mean absolute delta of 5.5 per
    /// channel over the whole frame BEFORE any overlay is drawn, and the only
    /// regions that disagree materially are the two overlay elements below.
    ///
    /// The previous implementation of this method drew neither of them
    /// correctly: it painted a 640x60 black band at y420 that retail does not
    /// draw at all, and put "Loading..." left-aligned at (24,436) scale 2 in
    /// Font13PS where retail centres it in font22 at scale 1 with its ink at
    /// x270..365, y401..419.
    ///
    ///   text  font22 scale 1, fitted origin (270, 393.5); its advance width 98
    ///         from x=270 centres on x=319
    ///   bar   x78..562, y423..447
    ///
    /// KNOWN GAP: the bar is the measured opaque black rectangle over that
    /// bbox. The capture is a continuous dark overlay on y435 (x78=(40,41,43),
    /// x100=(7,6,6), x200=(33,28,29), x300=(66,78,88), x400=(32,31,31),
    /// x540=(2,1,1), x562=(39,39,39); outside, x70/x570 stay mid-grey). A
    /// full-width rect is too solid in the middle but at least spans the bbox.
    ///
    /// FrontEnd\BarL/BarC/BarR.tga stay hash-pinned in FRONTEND_ASSETS. They
    /// are CFrontEnd::DrawBar (FrontEnd.cpp:1073) header-bar white masks
    /// (64x64 DXT2), not this page's sprite. CConsole__RenderLoadingScreen
    /// (0x0042C810) does not call DrawBar; its static callee list has one
    /// CVBufTexture__DrawSpriteEx and one CDXSurf__RenderSurface. Do not
    /// invent a DrawBar tile dest here. Draw the recovered sprite (texture,
    /// dest, tint, blend) only when that RenderLoadingScreen call is cited.
    /// Falsifier: local-lab/retail-reference-pristine/loading/07-loading-640x480.png.
    /// </summary>
    private void DrawLoading()
    {
        DrawRect(new Rect2(0f, 0f, DesignWidth, DesignHeight), Colors.Black);
        DrawTextureRect(
            _loadingScreen,
            new Rect2(0f, 0f, DesignWidth, DesignHeight),
            false);

        // Retail outlines this string rather than drop-shadowing it: the glyphs
        // carry a 1px black edge on all four sides, the same treatment the
        // click-to-start prompt uses. Drawing it with the standard +2/+2 shadow
        // instead left 50.6% of the text region materially different at an
        // otherwise pixel-exact ink bbox (ref x270..365 y401..420 against
        // x271..366 y401..420).
        var origin = new Vector2(LoadingTextLeft, LoadingTextTop);
        DrawFont22Outlined(_loadingText, origin + new Vector2(-1f, 1f), Colors.Black);
        DrawFont22Outlined(_loadingText, origin + new Vector2(1f, 1f), Colors.Black);
        DrawFont22Outlined(_loadingText, origin + new Vector2(-1f, -1f), Colors.Black);
        DrawFont22Outlined(_loadingText, origin + new Vector2(1f, -1f), Colors.Black);
        DrawFont22Outlined(_loadingText, origin, Colors.White);

        DrawRect(
            new Rect2(LoadingBarLeft, LoadingBarTop, LoadingBarWidth, LoadingBarHeight),
            Colors.Black);
    }

    private bool HandlePointerMotion(Vector2 position)
    {
        Vector2 design = ToDesignPosition(position);
        if (_session.Screen == RetailFrontendScreen.Options)
        {
            if (!HandleOptionsPointerMotion(design))
            {
                return false;
            }
            QueueRedraw();
            return true;
        }

        if (_session.Screen == RetailFrontendScreen.QuitConfirm)
        {
            int choice = QuitConfirmIndexAt(design);
            if (choice < 0 || !_session.SelectQuitConfirmIndex(choice))
            {
                return false;
            }

            RequestAudioCue(RetailFrontendAudioCue.Move);
            QueueRedraw();
            return true;
        }

        if (_session.Screen != RetailFrontendScreen.MainMenu)
        {
            return false;
        }

        // 0x004630AC / 0x004631EF: hover only when transition > 0.9.
        // Language hover writes this+0x08 = -1; that is not a language
        // swap and not a button confirm. Session cannot hold -1.
        if (!RetailMainMenuHitTest.AcceptsHitTest(MainMenuTransition))
        {
            return false;
        }

        if (RetailMainMenuHitTest.LanguageHoverContains(design.X, design.Y))
        {
            return false;
        }

        int index = MainMenuIndexAt(design);
        // Retail hover requires GetActionCount ≠ 0 — ignore grayed rows.
        if (index < 0 ||
            !_session.Items[index].IsAvailable ||
            !_session.SelectMainIndex(index))
        {
            return false;
        }

        RequestAudioCue(RetailFrontendAudioCue.Move);
        QueueRedraw();
        return true;
    }

    private bool HandlePointerConfirm(Vector2 position)
    {
        Vector2 design = ToDesignPosition(position);
        switch (_session.Screen)
        {
            case RetailFrontendScreen.ClickToStart:
                // CFEPIntro::Process 0x0051B801 submits (0,0,width,width,0x2C)
                // — full window, not a glyph box. See RetailFrontendScenePath.
                if (!RetailFrontendScenePath.AcceptsClickToStartMouse(
                        _session.Screen,
                        design.X,
                        design.Y))
                {
                    return false;
                }

                Confirm();
                return true;

            case RetailFrontendScreen.Options:
                return HandleOptionsPointerConfirm(design);

            case RetailFrontendScreen.MainMenu:
                int index = MainMenuIndexAt(design);
                if (!RetailFrontendScenePath.CanAcceptMainMenuRow(_session, index))
                {
                    return false;
                }
                if (_session.SelectMainIndex(index))
                {
                    RequestAudioCue(RetailFrontendAudioCue.Move);
                }
                Confirm();
                return true;

            case RetailFrontendScreen.QuitConfirm:
                int choice = QuitConfirmIndexAt(design);
                if (choice < 0)
                {
                    return false;
                }
                if (_session.SelectQuitConfirmIndex(choice))
                {
                    RequestAudioCue(RetailFrontendAudioCue.Move);
                }
                Confirm();
                return true;

            case RetailFrontendScreen.DevSelect:
                // Chevron hit rects match the drawn chevrons.
                if (new Rect2(0f, 430f, 46f, 48f).HasPoint(design))
                {
                    if (!RetailFrontendScenePath.TryBackPage(
                            _session,
                            startupMediaActive: false,
                            out RetailFrontendSignal back))
                    {
                        return false;
                    }
                    RequestAudioCue(RetailFrontendAudioCue.Back);
                    HandleNavigationSignal(back);
                    QueueRedraw();
                    return true;
                }
                if (new Rect2(595f, 430f, 45f, 48f).HasPoint(design) ||
                    new Rect2(128f, 408f, 403f, 44f).HasPoint(design))
                {
                    Confirm();
                    return true;
                }
                return false;

            case RetailFrontendScreen.LevelSelect:
                // Chevron hit rects match the drawn chevrons, as on FEP_DEVSELECT.
                if (new Rect2(0f, 430f, 48f, 48f).HasPoint(design))
                {
                    if (!RetailFrontendScenePath.TryBackPage(
                            _session,
                            startupMediaActive: false,
                            out RetailFrontendSignal levelBack))
                    {
                        return false;
                    }
                    RequestAudioCue(RetailFrontendAudioCue.Back);
                    HandleNavigationSignal(levelBack);
                    QueueRedraw();
                    return true;
                }
                if (new Rect2(595f, 430f, 45f, 48f).HasPoint(design))
                {
                    Confirm();
                    return true;
                }

                // LevelNodes[0] at (148,320) — the root, world 100. The
                // measured 60x60 hit box is unchanged.
                if (new Rect2(120f, 265f, 60f, 60f).HasPoint(design))
                {
                    _ = _session.SelectWorld(RetailWorldCatalog.RootWorldNumber);
                    Confirm();
                    return true;
                }

                // LevelNodes[1] is one column pitch (60) to the right of the
                // root — the first child in the measured Episode 1 graph,
                // which the career table says is world 110. Locked until a
                // Won update unlocks it; do not Confirm the root instead.
                if (new Rect2(180f, 265f, 60f, 60f).HasPoint(design))
                {
                    if (!_session.SelectWorld(110))
                    {
                        return false;
                    }

                    Confirm();
                    return true;
                }

                return false;

            case RetailFrontendScreen.Debriefing:
                // Settled CFEPDebriefing::Render dispatches button 0x2C over
                // the entire 640x480 rectangle when mouse input is ready.
                if (!new Rect2(0f, 0f, DesignWidth, DesignHeight).HasPoint(design))
                {
                    return false;
                }

                Confirm();
                return true;

            case RetailFrontendScreen.MissionBriefing:
            case RetailFrontendScreen.SelectConfiguration:
                // Both pages carry the same two chevrons and nothing else
                // clickable this lane models.
                if (new Rect2(0f, 430f, 48f, 48f).HasPoint(design))
                {
                    if (!RetailFrontendScenePath.TryBackPage(
                            _session,
                            startupMediaActive: false,
                            out RetailFrontendSignal pageBack))
                    {
                        return false;
                    }
                    RequestAudioCue(RetailFrontendAudioCue.Back);
                    HandleNavigationSignal(pageBack);
                    QueueRedraw();
                    return true;
                }
                if (new Rect2(595f, 430f, 45f, 48f).HasPoint(design))
                {
                    Confirm();
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    private bool HandlePointerCancel()
    {
        if (_session.Screen != RetailFrontendScreen.Options)
        {
            return false;
        }

        return HandleOptionsPointerCancel(rightDown: true);
    }

    private bool HandleKey(InputEventKey key)
    {
        // FEP_OPTIONS owns its own selection, left/right value stepping, dropdown
        // expansion and back stack, so it takes the whole key path.
        if (_session.Screen == RetailFrontendScreen.Options)
        {
            return HandleOptionsKey(key);
        }

        // New-career FEP_DEVSELECT is editable; Load mode mirrors the selected
        // injected save name and RetailFrontendSession rejects edits.
        if (_session.Screen == RetailFrontendScreen.DevSelect &&
            _session.CareerPageMode == RetailFrontendCareerPageMode.New)
        {
            if (IsKey(key, Key.Backspace))
            {
                if (_session.RemoveGameNameCharacter())
                {
                    QueueRedraw();
                }
                return true;
            }

            if (IsKey(key, Key.Left) || IsKey(key, Key.Up) || IsKey(key, Key.Right) || IsKey(key, Key.Down))
            {
                _session.MoveGameNameCursor(IsKey(key, Key.Right) || IsKey(key, Key.Down));
                QueueRedraw();
                return true;
            }
            if (IsKey(key, Key.Home) || IsKey(key, Key.End) || IsKey(key, Key.Delete)) return true;
            if (key.Unicode >= 32)
            {
                if (key.Unicode <= char.MaxValue &&
                    _session.AppendGameNameCharacter((char)key.Unicode, MeasureGameNameExtent(_session.GameName)))
                    QueueRedraw();
                return true;
            }
        }

        // QuitConfirm is a vertical YESNO stack. Retail
        // CFrontEnd__HandleModalPanelButton 0x0044dd60 option_mode 2:
        // BUTTON_FRONTEND_MENU_UP (0x2a) writes this+0x1fa0 = 1 (Yes, upper);
        // DOWN (0x2b) writes 0 (No, lower). PCController.cpp maps KEYCODE_UP /
        // KEYCODE_DOWN onto those buttons. Left/Right keep the session
        // 0=No / 1=Yes MovePrevious / MoveNext law.
        if (_session.Screen == RetailFrontendScreen.QuitConfirm)
        {
            if (IsKey(key, Key.Up))
            {
                if (_session.SelectQuitConfirmIndex(RetailFeMessBox.YesChoiceIndex))
                {
                    RequestAudioCue(RetailFrontendAudioCue.Move);
                    QueueRedraw();
                }

                return true;
            }

            if (IsKey(key, Key.Down))
            {
                if (_session.SelectQuitConfirmIndex(RetailFeMessBox.DefaultChoiceIndex))
                {
                    RequestAudioCue(RetailFrontendAudioCue.Move);
                    QueueRedraw();
                }

                return true;
            }
        }

        if (IsKey(key, Key.Up) || IsKey(key, Key.Left))
        {
            if (_session.MovePrevious())
            {
                RequestAudioCue(RetailFrontendAudioCue.Move);
                QueueRedraw();
            }
            return true;
        }
        if (IsKey(key, Key.Down) || IsKey(key, Key.Right))
        {
            if (_session.MoveNext())
            {
                RequestAudioCue(RetailFrontendAudioCue.Move);
                QueueRedraw();
            }
            return true;
        }
        if (IsKey(key, Key.Enter) || IsKey(key, Key.KpEnter) || IsKey(key, Key.Space))
        {
            if (_session.Screen == RetailFrontendScreen.ClickToStart
                && !RetailFrontendScenePath.AcceptsClickToStartKey(
                    _session.Screen,
                    ScanCodeFor(key)))
            {
                return true;
            }

            Confirm();
            return true;
        }
        if (IsKey(key, Key.Escape))
        {
            if (RetailFrontendScenePath.TryBackPage(
                    _session,
                    startupMediaActive: false,
                    out RetailFrontendSignal signal))
            {
                RequestAudioCue(RetailFrontendAudioCue.Back);
                HandleNavigationSignal(signal);
                QueueRedraw();
            }
            return true;
        }

        return false;
    }

    private void Confirm()
    {
        if (!RetailFrontendScenePath.TryConfirmPage(
                _session,
                startupMediaActive: false,
                out RetailFrontendSignal signal))
        {
            return;
        }

        // CFEPOptions::TransitionNotification reuses one persistent frontend
        // pause-menu context/tree but starts a fresh root session on each entry.
        if (_session.Screen == RetailFrontendScreen.Options)
        {
            _options.Reset();
        }

        RequestAudioCue(RetailFrontendAudioCue.Select);
        HandleNavigationSignal(signal);
        if (signal == RetailFrontendSignal.ExitRequested)
        {
            ExitRequested?.Invoke();
        }
        QueueRedraw();
    }

    private void ResumeFrontendForNavigation(RetailFrontendScreen origin)
    {
        if (origin == RetailFrontendScreen.Gameplay)
        {
            GameplaySuspended?.Invoke();
        }

        Visible = true;
        SetProcessInput(true);
        SetProcess(true);
        QueueRedraw();
        _mouseCursorLayer?.QueueRedraw();
    }

    private void HandleNavigationSignal(RetailFrontendSignal signal)
    {
        if (signal == RetailFrontendSignal.LevelLaunchRequested)
        {
            _loadRequestRaised = false;
            _level100Ready = false;
            _gameplayActivationRaised = false;
            _loadingFrames = 0;
            Level100LoadingStarted?.Invoke();
            CursorModeRequested?.Invoke(RetailFrontendCursorMode.Hidden);
        }
        else if (signal == RetailFrontendSignal.ReturnToMainMenuRequested)
        {
            CursorModeRequested?.Invoke(RetailFrontendCursorMode.Custom);
            ReturnToMainMenuRequested?.Invoke();
        }
        else if (signal == RetailFrontendSignal.CareerLoadRequested)
        {
            RetailCareerDescriptor selectedCareer =
                _session.ConsumeSelectedCareerLoadRequest()
                ?? throw new InvalidOperationException(
                    "CareerLoadRequested did not carry a selected career descriptor.");
            CareerSelected?.Invoke(selectedCareer);
            CursorModeRequested?.Invoke(RetailFrontendCursorMode.Custom);
        }
        else if (signal == RetailFrontendSignal.PageChanged)
        {
            CursorModeRequested?.Invoke(RetailFrontendCursorMode.Custom);
        }
    }

    private int MainMenuIndexAt(Vector2 designPosition)
    {
        var menuRect = new Rect2(
            MenuColumnX - MenuHitHalfWidth,
            MenuStartY - (MenuPitch * 0.5f),
            MenuHitHalfWidth * 2f,
            MenuPitch * _session.Items.Count);
        if (!menuRect.HasPoint(designPosition))
        {
            return -1;
        }

        int index = (int)((designPosition.Y - MenuStartY + (MenuPitch * 0.5f)) / MenuPitch);
        return Math.Clamp(index, 0, _session.Items.Count - 1);
    }

    private static int QuitConfirmIndexAt(Vector2 designPosition)
    {
        var noRow = new Rect2(
            RetailFeMessBox.QuitLeft,
            RetailFeMessBox.NoChoiceTop,
            RetailFeMessBox.QuitWidth,
            RetailFeMessBox.ChoiceRowHeight);
        if (noRow.HasPoint(designPosition))
        {
            return 0;
        }

        var yesRow = new Rect2(
            RetailFeMessBox.QuitLeft,
            RetailFeMessBox.YesChoiceTop,
            RetailFeMessBox.QuitWidth,
            RetailFeMessBox.ChoiceRowHeight);
        if (yesRow.HasPoint(designPosition))
        {
            return 1;
        }

        return -1;
    }

    private void LoadLocalization()
    {
        const string resourcePath = "res://Assets/Frontend/english.json";
        string source = Godot.FileAccess.GetFileAsString(resourcePath);
        if (string.IsNullOrEmpty(source))
        {
            throw new InvalidDataException($"Released frontend localization is missing: {resourcePath}");
        }

        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schema").GetString() != "onslaught.frontend-strings.v1" ||
            root.GetProperty("culture").GetString() != "en" ||
            root.GetProperty("sourceSha256").GetString() !=
                "789ecff619d077092769df281c540d138a25fcc74d70023466a604888e59371a")
        {
            throw new InvalidDataException("Released frontend localization has unexpected identity.");
        }

        JsonElement strings = root.GetProperty("strings");
        _menuText.Add(RetailFrontendMenuItemKind.NewGame, RequiredString(strings, "newGame"));
        _menuText.Add(RetailFrontendMenuItemKind.ContinueGame, RequiredString(strings, "continueGame"));
        _menuText.Add(RetailFrontendMenuItemKind.LoadGame, RequiredString(strings, "loadGame"));
        _menuText.Add(RetailFrontendMenuItemKind.Multiplayer, RequiredString(strings, "multiplayer"));
        _menuText.Add(RetailFrontendMenuItemKind.Goodies, RequiredString(strings, "goodies"));
        _menuText.Add(RetailFrontendMenuItemKind.Options, RequiredString(strings, "options"));
        // Index 6 menu label = FrontEndText token 8 → english.dat "Quit" (KEEP).
        // Messbox copy is Localization id 0xe4 (EXE table), not drawn on the row.
        _menuText.Add(RetailFrontendMenuItemKind.Quit, RequiredString(strings, "quit"));
        _selectLevelText = RequiredString(strings, "selectLevel");
        // The band and briefing draw the SELECTED node's row through
        // RetailFrontendWorldStrings; this load receipt stays as the proof
        // the menu table itself is present and identity-checked.
        _level100Text = RequiredString(strings, "level100");
        if (_level100Text != OnslaughtRebuild.Core.RetailFrontendWorldStrings.LevelName(100))
        {
            throw new InvalidDataException(
                "english.json level100 row diverged from the decoded world-strings table.");
        }
        _loadingText = RequiredString(strings, "loading");
    }

    private void LoadTextures()
    {
        _clickBackground = LoadTexture(
            "Backgrounds/click-to-start",
            1024,
            1024,
            CuratedAyaTextureLoader.Compression.Dxt1);
        _rockBackground = LoadTexture(
            "Backgrounds/rock",
            1024,
            512,
            CuratedAyaTextureLoader.Compression.Dxt1);
        _clickSlide = LoadTexture("click-slide", 128, 128);
        _forsetiWritingLarge = LoadTexture("forseti-writing-large", 128, 512);
        _titleLogo = LoadTexture("title-logo", 512, 256);
        // FrontEnd\v2\FE_Reflection_map.tga — 0x0089d7fc, the CFEPMain__Render
        // additive sheen. See TitleLogoReflectionLayer.
        _reflectionMap = LoadTexture(
            "reflection-map",
            512,
            128,
            CuratedAyaTextureLoader.Compression.Dxt1);
        _titleBracket01 = LoadTexture("title-bracket-01", 256, 256);
        _titleBracket02 = LoadTexture("title-bracket-02", 256, 256);
        _titleTextBox = LoadTexture("title-text-box", 256, 32);
        // FET2_BLANK — FrontEnd.cpp:755. Same 16×16 FrontEnd%v2%FE_Blank.tga
        // the pause menu already materializes.
        _feBlank = LoadTexture(
            "blank",
            16,
            16,
            CuratedAyaTextureLoader.Compression.Dxt1,
            folder: "PauseMenu");
        _symbolBracket01 = LoadTexture("symbol-bracket-01", 128, 128);
        _symbolBracket02 = LoadTexture("symbol-bracket-02", 128, 128);
        _levelBracket01 = LoadTexture("level-bracket-01", 512, 512);
        _levelBracket02 = LoadTexture("level-bracket-02", 512, 512);
        _levelRing01 = LoadTexture("level-ring-01", 64, 64);
        _levelRing02 = LoadTexture("level-ring-02", 64, 64);
        _debriefingMetalRing = LoadTexture(
            "Debriefing/metal-ring-transition",
            512,
            512,
            CuratedAyaTextureLoader.Compression.Dxt2);
        _debriefingGradeTextures =
        [
            LoadTexture("Debriefing/ranking-a", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
            LoadTexture("Debriefing/ranking-b", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
            LoadTexture("Debriefing/ranking-c", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
            LoadTexture("Debriefing/ranking-d", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
            LoadTexture("Debriefing/ranking-e", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
            LoadTexture("Debriefing/ranking-s", 64, 64, CuratedAyaTextureLoader.Compression.Dxt2),
        ];
        _loadingScreen = LoadTexture(
            "loading-screen",
            512,
            512,
            CuratedAyaTextureLoader.Compression.Dxt1);
        // data/language holds exactly five sets and the released texture set carries
        // exactly five matching flags (Career.h: NUM_LANGUAGES 5). Order matches
        // RetailFrontendLanguage.
        _languageFlags =
        [
            LoadTexture("Flags/flag-uk", 128, 128, CuratedAyaTextureLoader.Compression.Dxt1),
            LoadTexture("Flags/flag-fr", 128, 128, CuratedAyaTextureLoader.Compression.Dxt1),
            LoadTexture("Flags/flag-gr", 128, 128, CuratedAyaTextureLoader.Compression.Dxt1),
            LoadTexture("Flags/flag-it", 128, 128, CuratedAyaTextureLoader.Compression.Dxt1),
            LoadTexture("Flags/flag-sp", 128, 128, CuratedAyaTextureLoader.Compression.Dxt1),
        ];
        _feArrow = LoadTexture("fe-arrow", 64, 64);
        // Retail shares one bitmap font resource between HUD and frontend, so this
        // legitimately reaches into the Hud asset folder rather than duplicating it.
        _titleFont = LoadTexture(
            "font-13ps",
            256,
            256,
            CuratedAyaTextureLoader.Compression.Rgba8,
            folder: "Hud");
        // mustbe_font22.512.tga, shared with the HUD for the same reason.
        _font22 = LoadTexture(
            "font-22",
            512,
            512,
            CuratedAyaTextureLoader.Compression.Rgba8,
            folder: "Hud");
        // mustbe_SystemFont - the fixed-pitch 7x9 sheet the Controller Options
        // bindings grid renders with. See RetailFrontendFlow.Options.cs for how it
        // was identified.
        _systemFont = LoadTexture(
            "system-font",
            256,
            256,
            CuratedAyaTextureLoader.Compression.Rgba8);
        // Must match RetailFrontendSession Steam-drawn row order (Update/icons).
        _menuIcons =
        [
            LoadTexture("Icons/new-game", 128, 128),
            LoadTexture("Icons/continue-game", 128, 128),
            LoadTexture("Icons/load-game", 128, 128),
            LoadTexture("Icons/multiplayer", 128, 128),
            LoadTexture("Icons/goodies", 128, 128),
            LoadTexture("Icons/options", 128, 128),
            LoadTexture("Icons/quit", 128, 128),
        ];
    }

    private static Texture2D[] LoadFeBackFrames()
    {
        string absolute = ProjectSettings.GlobalizePath(FeBackStripPath);
        if (!File.Exists(absolute))
        {
            GD.PushWarning(
                $"FEBack strip missing at {FeBackStripPath}; main underlay uses solid fallback.");
            return [];
        }

        byte[] strip = File.ReadAllBytes(absolute);
        if (strip.Length == 0 || strip.Length % FeBackFrameBytes != 0)
        {
            throw new InvalidDataException(
                $"FEBack strip length {strip.Length} is not a multiple of {FeBackFrameBytes}.");
        }

        int frameCount = strip.Length / FeBackFrameBytes;
        var frames = new Texture2D[frameCount];
        // The flat page fill, pre-added into every frame. See DrawMainUnderlay for
        // why baking the composite is exact rather than an approximation, and for
        // the measurement behind FeBackUnderlayGain.
        byte[] fill =
        [
            (byte)Math.Round(MainUnderlayFallback.R * 255f),
            (byte)Math.Round(MainUnderlayFallback.G * 255f),
            (byte)Math.Round(MainUnderlayFallback.B * 255f),
        ];
        for (int index = 0; index < frameCount; index++)
        {
            byte[] frame = new byte[FeBackFrameBytes];
            Buffer.BlockCopy(strip, index * FeBackFrameBytes, frame, 0, FeBackFrameBytes);
            for (int offset = 0; offset < FeBackFrameBytes; offset++)
            {
                int channel = offset % 3;
                double value = fill[channel] + (FeBackUnderlayGain[channel] * frame[offset]);
                frame[offset] = (byte)Math.Clamp(Math.Round(value), 0d, 255d);
            }

            Image image = Image.CreateFromData(
                FeBackWidth,
                FeBackHeight,
                false,
                Image.Format.Rgb8,
                frame);
            frames[index] = ImageTexture.CreateFromImage(image);
        }

        return frames;
    }

    private static Texture2D LoadTexture(
        string name,
        int width,
        int height,
        CuratedAyaTextureLoader.Compression compression = CuratedAyaTextureLoader.Compression.Dxt2,
        string folder = "Frontend") =>
        CuratedAyaTextureLoader.Load(
            $"res://Assets/{folder}/{name}.texture.aya",
            width,
            height,
            compression);

    private void DrawSurfaceCentered(
        Texture2D texture,
        float centerX,
        float centerY,
        float widthScale,
        float heightScale,
        Color modulate)
    {
        float width = texture.GetWidth() * widthScale;
        float height = texture.GetHeight() * heightScale;
        DrawTextureRect(
            texture,
            new Rect2(centerX - (width * 0.5f), centerY - (height * 0.5f), width, height),
            false,
            modulate);
    }

    private void DrawCenteredRotated(
        Texture2D texture,
        Vector2 center,
        Vector2 size,
        float rotation,
        Color modulate)
    {
        // CONSENSUS_C: single window↔stage map. DrawSetTransform replaces (does not nest);
        // re-apply letterbox around the design-space pivot, then restore design space.
        (float scale, Vector2 offset) = DesignTransform();
        DrawSetTransform(
            offset + (center * scale),
            rotation,
            new Vector2(scale, scale));
        DrawTextureRect(texture, new Rect2(-size * 0.5f, size), false, modulate);
        DrawSetTransform(offset, 0f, new Vector2(scale, scale));
    }

    private void DrawTextCentered(string text, Vector2 center, float scale, Color color)
    {
        float width = MeasureText(text, scale);
        DrawText(text, new Vector2(center.X - (width * 0.5f), center.Y), scale, color);
    }

    private void DrawText(string text, Vector2 position, float scale, Color color) =>
        DrawTextCore(text, position, scale, color, dropShadow: true);

    private void DrawTextFlat(string text, Vector2 position, float scale, Color color) =>
        DrawTextCore(text, position, scale, color, dropShadow: false);

    private void DrawTextCore(string text, Vector2 position, float scale, Color color, bool dropShadow) =>
        DrawAtlasText(
            _titleFont,
            _glyphWidths,
            GlyphCellSize,
            GlyphColumns,
            text,
            position,
            scale,
            scale,
            color,
            dropShadow);

    /// <summary>
    /// Atlas-agnostic glyph run. Font13PS and font22 share the ASCII-32 origin
    /// and the 16-column grid and differ only in cell size, so one routine draws
    /// both. Separate X and Y scales exist because the briefing level name is
    /// measurably non-uniform (see <see cref="BriefingLevelNameScaleX"/>).
    /// </summary>
    private void DrawAtlasText(
        Texture2D atlas,
        int[] widths,
        int cellSize,
        int columns,
        string text,
        Vector2 position,
        float scaleX,
        float scaleY,
        Color color,
        bool dropShadow,
        bool retailNameGlyphs = false)
    {
        float x = position.X;
        foreach (char character in text)
        {
            int glyph = retailNameGlyphs ? RetailFrontendSession.GameNameRenderGlyphIndex(character, swapInvertedPunctuation: false) : GlyphIndex(character);
            float glyphWidth = widths[glyph] * scaleX;
            var source = new Rect2(
                (glyph % columns) * cellSize,
                (glyph / columns) * cellSize,
                widths[glyph],
                cellSize);
            var destination = new Rect2(x, position.Y, glyphWidth, cellSize * scaleY);
            if (dropShadow)
            {
                // THE SHADOW SITS ON THE ANCHOR AND THE BODY IS DRAWN AT (-1,-1).
                // MEASURED from retail's own draw calls, 2026-07-27 d3d9 sweep
                // (local-lab/D3D9-FULL-SWEEP-2026-07-27.md; per-draw CSVs under
                // <maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\). Every
                // shadowed text run in the frontend is a shadow DrawPrimitive
                // followed immediately by a body DrawPrimitive whose rectangle is
                // the shadow's minus (1,1). Six pages, two font atlases, no
                // exception:
                //
                //   main-menu-settled.csv f3000 d12/d13   (175.5,296.5) / (174.5,295.5)
                //   main-menu-settled.csv f3000 d32/d33   (  0.5,464.5) / ( -0.5,463.5)
                //   options-root.csv      f2500 d11/d12   (243.5,245.5) / (242.5,244.5)
                //   options-sound.csv     f4900 d43/d44   ( 44.5,255.5) / ( 43.5,254.5)
                //   mission-briefing.csv  f3600 d18/d19   ( 80.5,164.5) / ( 79.5,163.5)
                //   select-level.csv      f2500 d71/d72   (130.5,153.5) / (129.5,152.5)
                //
                // AND THE SHADOW CARRIES THE BODY'S OWN ALPHA, not a fraction of
                // it. The packed diffuse pairs are 0xFD000000/0xFD4F4F4F and
                // 0x7D000000/0x7D1F1F1F (main menu), 0xFF000000/0xFFD6D6D6
                // (options), 0xFE000000/0xFEFFDF5F (briefing),
                // 0xFF000000/0xFF7F7F7F (select level). The alpha byte is equal on
                // every one of the 63 pairs in those five inventories; the RGB of
                // the shadow is always exactly 0x000000. The previous
                // `color.A * 0.82f` is refuted by all 63.
                //
                // WHY THIS IS AN EXACT MATCH AND NOT A HALF-PIXEL APPROXIMATION.
                // Retail's rectangles are half-integer because D3D9 samples a
                // pixel at its centre, which sits at k + 0.5 in XYZRHW space: a
                // quad spanning [k+0.5, k+0.5+n) covers pixel rows k..k+n-1, which
                // is exactly what a Godot Rect2 at integer k with height n covers.
                // Our anchors were therefore already on retail's SHADOW row; only
                // the assignment of the two quads to it was inverted. On the seven
                // main-menu rows this change takes our body from +1,+1 and our
                // shadow from +2,+2 to a zero-pixel offset on both.
                DrawTextureRectRegion(
                    atlas,
                    destination,
                    source,
                    new Color(0f, 0f, 0f, color.A));
                DrawTextureRectRegion(
                    atlas,
                    new Rect2(destination.Position - Vector2.One, destination.Size),
                    source,
                    color);
            }
            else
            {
                DrawTextureRectRegion(atlas, destination, source, color);
            }
            x += glyphWidth + scaleX;
        }
    }

    private void DrawFont22Text(string text, Vector2 position, float scaleX, float scaleY, Color color) =>
        DrawAtlasText(
            _font22,
            _font22Widths,
            Font22CellSize,
            Font22Columns,
            text,
            position,
            scaleX,
            scaleY,
            color,
            dropShadow: true);

    private void DrawFont22Outlined(string text, Vector2 position, Color color) =>
        DrawAtlasText(
            _font22,
            _font22Widths,
            Font22CellSize,
            Font22Columns,
            text,
            position,
            1f,
            1f,
            color,
            dropShadow: false);

    private int MeasureGameNameExtent(string text)
    {
        int width = 0;
        foreach (char character in text)
        {
            int glyph = RetailFrontendSession.GameNameRenderGlyphIndex(character, swapInvertedPunctuation: true);
            width += _font22Widths[glyph] + 1;
        }
        return width;
    }

    private float MeasureFont22Text(string text, float scaleX)
    {
        float width = 0f;
        foreach (char character in text)
        {
            width += (_font22Widths[GlyphIndex(character)] + 1) * scaleX;
        }
        return Mathf.Max(0f, width - scaleX);
    }

    /// <summary>Header title: font22 at scale 1, centred on HEADER_BAR_X.</summary>
    private void DrawHeaderBarTitle(string title)
    {
        DrawRect(new Rect2(191f, 69f, 394f, 21f), HeaderBoxOverlay);
        float width = MeasureFont22Text(title, 1f);
        DrawFont22Text(
            title,
            new Vector2(HeaderBarCenterX - (width * 0.5f), HeaderTitleTop),
            1f,
            1f,
            ReleasedTitleText);
    }

    private float MeasureText(string text, float scale)
    {
        float width = 0f;
        foreach (char character in text)
        {
            width += (_glyphWidths[GlyphIndex(character)] + 1) * scale;
        }
        return Mathf.Max(0f, width - scale);
    }

    private static int GlyphIndex(char character)
    {
        int code = character;
        if (code is < FirstGlyph or >= FirstGlyph + GlyphSlotCount)
        {
            code = '?';
        }

        return code - FirstGlyph;
    }

    private static int[] MeasureGlyphWidths(Image image, int cellSize, int columns)
    {
        var widths = new int[GlyphSlotCount];
        widths[0] = cellSize / 2;
        int scanMax = cellSize - 2;
        for (int glyph = 1; glyph < widths.Length; glyph++)
        {
            int cellX = (glyph % columns) * cellSize;
            int cellY = (glyph / columns) * cellSize;
            if (cellY + cellSize > image.GetHeight())
            {
                break;
            }
            int rightmost = cellX;
            for (int x = cellX + scanMax; x >= cellX; x--)
            {
                bool occupied = false;
                for (int y = cellY; y < cellY + cellSize - 1; y++)
                {
                    if (image.GetPixel(x, y).A > (16f / 255f))
                    {
                        occupied = true;
                        break;
                    }
                }
                if (occupied)
                {
                    rightmost = x;
                    break;
                }
            }
            widths[glyph] = (rightmost - cellX) + 2;
        }
        return widths;
    }

    private (float Scale, Vector2 Offset) DesignTransform()
    {
        float scale = Mathf.Min(Size.X / DesignWidth, Size.Y / DesignHeight);
        return (
            scale,
            new Vector2(
                (Size.X - (DesignWidth * scale)) * 0.5f,
                (Size.Y - (DesignHeight * scale)) * 0.5f));
    }

    private Vector2 ToDesignPosition(Vector2 viewportPosition)
    {
        (float scale, Vector2 offset) = DesignTransform();
        return scale <= 0f ? Vector2.Zero : (viewportPosition - offset) / scale;
    }

    private static string RequiredString(JsonElement strings, string key)
    {
        string? value = strings.GetProperty(key).GetString();
        return string.IsNullOrEmpty(value)
            ? throw new InvalidDataException($"Released frontend localization is missing '{key}'.")
            : value;
    }

    private static bool IsKey(InputEventKey input, Key key) =>
        input.PhysicalKeycode == key || input.Keycode == key;

    private static int ScanCodeFor(InputEventKey key)
    {
        Key code = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;
        return code switch
        {
            Key.Space => 0x39,
            Key.Enter => 0x1C,
            Key.Escape => 0x01,
            Key.KpEnter => 0x9C,
            _ => 0,
        };
    }

    private void RequestAudioCue(RetailFrontendAudioCue cue) =>
        AudioCueRequested?.Invoke(cue);

    /// <summary>
    /// Converts a packed released ARGB constant to a Godot colour.
    ///
    /// The released frontend composes its text and chrome through a 2x colour
    /// modulate, so a packed RGB channel reaches the framebuffer at
    /// <c>min(255, (c * 255) &gt;&gt; 7)</c> — very nearly double. Reproducing the packed
    /// value literally renders every frontend colour at roughly half intensity,
    /// which is why the reconstruction's menu read as dark grey-on-blue while
    /// retail reads as light grey and bright amber.
    ///
    /// Measured against <c>captures/08-main-retail.png</c> (640x480 retail frame),
    /// brightest glyph texel per row:
    ///   0x4f (normal)   -> predicted 157, measured 157
    ///   0x6f (selected) -> predicted 221, measured 220
    ///   0x3f (selected) -> predicted 125, measured 125
    /// Alpha is NOT modulated: 0x7f disabled text blends at ~0.5 as packed.
    /// </summary>
    private static Color RetailColor(uint argb) => new(
        Modulate2X((argb >> 16) & 0xff),
        Modulate2X((argb >> 8) & 0xff),
        Modulate2X(argb & 0xff),
        ((argb >> 24) & 0xff) / 255f);

    private static float Modulate2X(uint channel) =>
        Math.Min(255u, (channel * 255u) >> 7) / 255f;

    /// <summary>
    /// The scrolling additive sheen CFEPMain__Render lays over the title logo —
    /// RECOVERED FROM THE SHIPPED BYTES, not fitted.
    ///
    /// <para><b>Why this exists.</b></para>
    /// The settled main menu's two worst regions, <c>title-logo</c> (41.21 % gap)
    /// and <c>bg-emblem-topright</c> (35.57 %), are largely the same 512x256 quad,
    /// and our renderer was already exonerated for it: fitted against the decoded
    /// texel our pixels give slope 1.002, intercept -0.30, rms 0.39. Retail simply
    /// puts something else there — an animated layer whose temporal std inside the
    /// opaque logo interior is ~15 while its mean is constant to +-0.6.
    ///
    /// A version of this layer was drawn until 2026-07-26 and was deleted for two
    /// stated reasons. The first was correct and is fixed here: it set
    /// <c>CanvasItem.Material</c> around a bracketed span of <c>_Draw</c> commands,
    /// but a CanvasItem's blend mode applies to the whole item, so the "additive"
    /// draw was never additive. That is why this is a SEPARATE CanvasItem. The
    /// second reason — a DC residual bound — was already withdrawn in that comment
    /// as bounding only the mean, and it does not survive: the layer is confined to
    /// the logo's own alpha footprint, where a footprint-wide mean is the wrong
    /// instrument.
    ///
    /// <para><b>What the bytes say.</b></para>
    /// Specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
    /// sha256 <c>74154bfa…</c> (the PATCHED sibling <c>BEA.exe</c> was not read).
    /// <c>CFEPMain__Render</c> is <c>[0x00462d40, 0x0046449e)</c>; every address
    /// below is inside it. Its tail, in order:
    /// <code>
    /// 0x00464251  logo shadow: 0x0089d88c at (325 + sin, 140 + cos), z 0.1,
    ///             scale 1.05, tint 0x3e000000
    /// 0x004642b5  SetRenderState(0x17 D3DRS_ZFUNC, 8 D3DCMP_ALWAYS)   [0x00513bc0]
    /// 0x004642ce  logo body:   0x0089d88c at (320, 130), z 0.99899 (0x3f7fbe77),
    ///             scale 1, tint 0xfeafcfff  (and 0xafcfff is literally
    ///             `and edx,0xffafcfff` / `or edx,0xafcfff` at 0x004642e4/f1)
    /// 0x0046431a  SetRenderState(0x17 D3DRS_ZFUNC, 4 D3DCMP_LESSEQUAL)
    /// 0x0046435d  CFrontEnd__EnableAdditiveAlpha (0x004681c0) — and this is the
    ///             whole of it: SetRenderState(0x13 SRCBLEND, 2 D3DBLEND_ONE) and
    ///             SetRenderState(0x14 DESTBLEND, 2 D3DBLEND_ONE). ONE/ONE.
    /// 0x004643aa  0x0089d7fc at (321 - m, 120), z 0.99799, scale (1, 2)
    /// 0x004643e7  0x0089d7fc at (321 - m + 512, 120), same
    /// 0x004643f4  CFrontEnd__EnableModulateAlpha (0x004681e0) — SRCBLEND 5
    ///             SRCALPHA / DESTBLEND 6 INVSRCALPHA
    /// </code>
    ///
    /// <b>The texture.</b> <c>0x0089d7fc</c> is loaded at <c>0x00468aa9</c> from
    /// <c>"FrontEnd\v2\FE_Reflection_map.tga"</c> (string at <c>0x0062a83c</c>) and
    /// stored to <c>[ebp+0x9c]</c> at <c>0x00468ac0</c>; the same routine stores
    /// <c>FE_BEA_Title2.tga</c> to <c>[ebp+0x12c]</c> at <c>0x00468e60</c>, and
    /// <c>0x0089d88c - 0x12c = 0x0089d760 = ebp</c>, so <c>ebp + 0x9c</c> is exactly
    /// <c>0x0089d7fc</c>. That arithmetic is the identification; the map note that
    /// carried this global as "path unknown / low confidence" is superseded.
    ///
    /// <b>The tint.</b> <c>0x00464362</c>-<c>0x00464387</c> computes
    /// <c>((a*127)&gt;&gt;8) * 0x010101 - 0x01000000</c>, i.e. <c>0xff7e7e7e</c> at
    /// alpha 255. Under ONE/ONE with stage 0 <c>MODULATE(TEXTURE, DIFFUSE)</c> the
    /// framebuffer gains <c>tex.rgb * 126/255</c>. NOTE: <see cref="RetailColor"/>'s
    /// 2x modulate does NOT apply on this path — measurement below says 0.488, not
    /// 0.988 — so the raw ratio is used.
    ///
    /// <b>The scroll.</b> <c>0x00464331</c>-<c>0x00464359</c>:
    /// <c>m = fmod(FRONTEND.mCounter * 0.6, 512)</c> and <c>x = 256 - m + 65</c>,
    /// with the second copy at <c>+512</c> under <c>D3DTADDRESS_WRAP</c>. The
    /// counter is <c>0x008a9570</c>, the float form of <c>CFrontEnd::mCounter</c>
    /// (<c>FrontEnd.cpp:597</c>): <c>GetShadowOffsetX</c> at <c>0x00468730</c> is
    /// <c>sin(counter * 0.01) * 6</c>, exactly <c>FrontEnd.cpp:1561</c>'s
    /// <c>sinf(counter / SHADOW_PERIOD) * SHADOW_RADIUS_X</c> with
    /// <c>SHADOW_PERIOD 100</c>, <c>SHADOW_RADIUS_X 6</c>.
    ///
    /// <b>The mask, which is the whole trick.</b> The logo body is drawn under
    /// <c>D3DCMP_ALWAYS</c> at z 0.99899 with <c>ZWRITEENABLE</c> and the default
    /// block's <c>ALPHATESTENABLE</c> / <c>ALPHAFUNC GREATEREQUAL</c> /
    /// <c>ALPHAREF 8</c> (d3d-default-render-state-block-2026-07-27.md §5). So it
    /// stamps depth 0.99899 over exactly its alpha&gt;=8 texels and nowhere else.
    /// ZFUNC then goes back to <c>LESSEQUAL</c> and the sheen is submitted at
    /// 0.99799 — which passes inside that stamp and fails against the page behind
    /// it. The sheen is a logo-shaped clip, not a rectangle. That is why the
    /// pedestal has equal core and 1-2 px ring intercepts, and why a spatial
    /// control outside the ink found nothing.
    ///
    /// <para><b>What is measured rather than recovered, stated plainly.</b></para>
    /// A static disassembly cannot say how fast <c>mCounter</c> advances, and
    /// nothing pins its phase at main-menu entry. Both were measured from retail
    /// pixels, on the 12 settled <c>run1</c> frames of
    /// <c>local-lab/retail-reference-pristine/nofmv-frontend-2026-07-26</c>, by
    /// regressing frame-to-frame differences onto the decoded reflection texel
    /// under the recovered model (differences, so the static page cancels and no
    /// baseline is assumed):
    /// <code>
    ///   rate  29.95 design px/s   (= 0.6 px/tick x 49.9 ticks/s)
    ///   phase 133 px at t = 1530 ms after main-menu entry
    /// </code>
    /// The gain was NOT fitted — it is <c>126/255 = 0.4941</c> from the bytes — but
    /// it was checked: least squares inside the alpha&gt;=8 footprint, on
    /// unsaturated pixels, gives R 0.4883, G 0.4887, B 0.4870. Residual rms inside
    /// the footprint drops 28.4 -> 4.3 (R), 26.6 -> 4.6 (G), 20.0 -> 7.8 (B).
    /// OUTSIDE the footprint the same regression gives gain 0.025/0.030/0.038 and
    /// moves rms by nothing (7.35 -> 7.26), which is the depth clip showing up in
    /// the pixels.
    ///
    /// The phase constant is the same class of measured anchor as
    /// <see cref="FeBackPhaseFrames"/> and carries the same caveat: retail's
    /// counter is never reset on a page change, so its value at main-menu entry
    /// depends on how long click-to-start was held.
    /// </summary>
    private sealed partial class TitleLogoReflectionLayer : Node2D
    {
        // Recovered: 126/255 from the 0xff7e7e7e vertex tint under ONE/ONE.
        internal const float Gain = 126f / 255f;

        // Recovered: fmod(counter * 0.6, 512), two copies 512 apart.
        internal const float ScrollPeriodPx = 512f;

        // MEASURED (see the class remarks) — 0.6 px/tick against a counter that
        // advanced 49.9 times a second in the reference capture.
        internal const float ScrollPxPerSecond = 29.95f;

        // MEASURED — chosen so scroll(1.530 s after main-menu entry) = 133 px.
        // 133 - 29.95 * (1.530 + 1/60) = 86.68.
        internal const float ScrollPhasePx = 86.68f;

        // The logo quad: 0x0089d88c is 512x256 drawn centred on (320, 130).
        internal const float LogoLeft = 64f;
        internal const float LogoTop = 2f;
        internal const float LogoWidth = 512f;
        internal const float LogoHeight = 256f;

        // The sheen quad: 512x128 at scale (1, 2) centred on (321 - m, 120), so it
        // spans y -8..248 and, with its +512 twin under WRAP, always covers
        // x 65..577. Screen pixel x samples texel (x - 65 + m) mod 512; screen y
        // samples texel (y + 8) / 2.
        internal const float SheenLeftAtZeroScroll = 65f;
        internal const float SheenTop = -8f;
        internal const float SheenHeight = 256f;

        private static Shader? _shader;
        private readonly ShaderMaterial _material = new();
        private Texture2D _logo = null!;

        public override void _Ready() => Material = _material;

        public void Configure(Texture2D logo, Texture2D reflection)
        {
            _logo = logo;
            _material.Shader = _shader ??= new Shader { Code = ShaderCode };
            _material.SetShaderParameter("reflection", reflection);
            _material.SetShaderParameter("gain", Gain);
        }

        /// <summary>
        /// <paramref name="frontendSeconds"/> is the same clock
        /// <see cref="FeBackFrameIndex"/> consumes: seconds since the frontend left
        /// click-to-start.
        /// </summary>
        public void SetScroll(double frontendSeconds)
        {
            float scroll = Mathf.PosMod(
                ((float)frontendSeconds * ScrollPxPerSecond) + ScrollPhasePx,
                ScrollPeriodPx);
            _material.SetShaderParameter("scroll", scroll);
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_logo is null)
            {
                return;
            }

            // One quad over the logo footprint. The shader clips it to the logo's
            // own alpha>=8 texels, which is what retail's depth stamp does.
            DrawTextureRect(
                _logo,
                new Rect2(LogoLeft, LogoTop, LogoWidth, LogoHeight),
                false,
                Colors.White);
        }

        // The literals here are the const fields above; the shader cannot consume
        // C# consts directly, so they are repeated once and only once:
        //   64/2/512/256  logo quad          (LogoLeft/Top/Width/Height)
        //   65/-8/256     sheen quad         (SheenLeftAtZeroScroll/Top/Height)
        //   512           wrap period        (ScrollPeriodPx)
        private const string ShaderCode = """
            shader_type canvas_item;
            render_mode blend_add, unshaded;

            uniform sampler2D reflection : filter_linear, repeat_enable;
            uniform float scroll;
            uniform float gain;

            void fragment() {
                // Fragment centres, so x is the pixel's integer column + 0.5.
                float x = 64.0 + (UV.x * 512.0);
                float y = 2.0 + (UV.y * 256.0);

                // D3DCMP_ALWAYS z-stamp of the alpha-tested logo body (ALPHAREF 8).
                float mask = step(8.0 / 255.0, texture(TEXTURE, UV).a);
                // The sheen quad's own edges: its left edge is 65 - scroll and its
                // bottom is y = 248. Everything between is covered by it or by its
                // +512 twin, which is what the mod() below expresses.
                mask *= step(65.0 - scroll, x);
                mask *= step(y, 248.0);

                float u = mod(x - 65.0 + scroll, 512.0) / 512.0;
                float v = (y + 8.0) / 256.0;
                COLOR = vec4(texture(reflection, vec2(u, v)).rgb * gain, mask);
            }
            """;
    }
}
