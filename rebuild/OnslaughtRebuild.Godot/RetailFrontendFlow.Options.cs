// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// FEP_OPTIONS and its three subpages.
///
/// <para><b>Provenance split, stated once here so every constant below is
/// attributable.</b></para>
/// <list type="bullet">
/// <item><b>SOURCE (pinned GPL drop):</b> nothing about the widget layer -
/// <c>FEPOptions.cpp</c>, <c>MenuItem.cpp</c> and <c>PauseMenu.cpp</c> are all
/// absent. What IS ported is the page chrome (<c>FrontEnd.cpp:1101-1105</c>
/// header-bar constants, already consumed by <see cref="DrawHeaderBarTitle"/>)
/// and everything behind the rows (see <see cref="RetailOptionsMenu"/>).</item>
/// <item><b>BYTES (pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
/// <c>74154bfa…</c>):</b> the row inventory, the exact English labels, the row
/// height 20, the range origin 300, the mouse-sensitivity law, the value-row
/// seed bias 0.48, and the three apply timings. All of it is written up in
/// <c>local-lab/OPTIONS-PAGE-RECOVERY-2026-07-27.md</c>.</item>
/// <item><b>PIXELS
/// (<c>local-lab/retail-captures-options-pause-2026-07-27/</c>):</b> every
/// geometry constant in this file, every colour, the bindings-grid font
/// identity, and the fact that "Screen shape:" is absent from the shipped Video
/// page.</item>
/// </list>
///
/// <para><b>Known gaps, drawn nowhere rather than faked.</b></para>
/// The top-left Forseti emblem and the metal header end-caps are the same
/// unidentified art every other page in this lane lacks. The retail root page's
/// title plate is 390px wide and its title reads (215,215,217) where the three
/// subpages are 395px and (254,254,254); that difference is unexplained and the
/// tracked 395/253 treatment is used for all four.
/// </summary>
public sealed partial class RetailFrontendFlow
{
    // ---- Range geometry -------------------------------------------------
    //
    // MEASURED. The first row's cell top is
    //     RangeOriginY - pageHeight/2 - 15
    // which reproduces the retail frames EXACTLY on three pages:
    //     root   4 rows x 20 = 80   -> 245, ink at 248  (measured 248)
    //     sound  9 rows x 20 = 180  -> 195, ink at 198  (measured 198)
    //     video 14 rows x 20 = 280  -> 145, ink at 148  (measured 148)
    // and lands 1.5px high on Controller (predicted 107.5, measured 106). That
    // residual is reported, not tuned away: the Controller page is the only one
    // with mixed row heights, and one of them (the 17px Joystick/Back pitch) has
    // no byte explanation at all.
    //
    // Font13PS puts capital ink 3 rows below the cell top. These measured values
    // are body origins; DrawOptionsBodyText adapts them to the shared primitive's
    // shadow-origin contract.

    /// <summary>
    /// The Label:Value split, FITTED to the retail frames rather than assumed.
    /// Measured on nine such rows across the Sound and Video frames, retail's
    /// label-side ink always ends at x=316 and its value-side ink always begins at
    /// x=323. Right-aligning the label (colon included) so its advance ends at 319
    /// and starting the value from RetailOptionsDropdownValueDest.DestX
    /// reproduces both to within the known per-glyph left-bearing residual.
    /// </summary>
    private const float OptionLabelRightX = 319f;

    private const float OptionRowCenterX = 320f;

    // ---- Value bar ------------------------------------------------------
    //
    // MEASURED on all three bars (Sound Volume, Music Volume, Mouse sensitivity).
    // The whole row - label, both arrows and the bar - is centred on x=320 as one
    // unit, which is why the bar's x differs per row while the plain rows on the
    // same page are colon-aligned.
    //
    //   row                '<' ink   bar ink      '>' ink    segments  filled
    //   Sound Volume       328-336   341-418      422-430    10 x 6/2  8
    //   Music Volume       327-335   340-417      421-429    10 x 6/2  9
    //   Mouse sensitivity  347-355   360-437      441-449    20 x 2/2  3
    //
    // The bar span is 78px in every case (10x8-2 == 20x4-2), and the bar sits at
    // baseline-9 .. baseline+1, i.e. cell top +3 .. +13.
    private const float BarAssemblyWidth = 103f;
    private const float BarLabelPad = 6f;
    private const float BarLeftArrowToBar = 13f;
    private const float BarToRightArrow = 81f;
    private const float BarWidth = 78f;
    private const float BarTopOffset = 3f;
    private const float BarHeight = 11f;

    // The FE_Arrow ink, derived from the image constants rather than fitted: the
    // quad is 0.3 x 64 = 19.2px and the artwork occupies (16,12)-(46,52) of the
    // 64x64 sheet, so the ink is 30 x 0.3 = 9.0 wide and 40 x 0.3 = 12.0 tall, and
    // its top sits 12 x 0.3 = 3.6px below a quad top that is itself 9.6px above the
    // quad centre. Against the measured row that puts the ink at cell top + 2.5.
    // Retail ink measures 9px wide by 12-13px including antialiasing - agreement,
    // not a fit.
    private const float ArrowInkWidth = 9f;
    private const float ArrowInkHeight = 12f;
    private const float ArrowInkTop = 2.5f;

    // ---- Bindings grid --------------------------------------------------
    //
    // The grid font is mustbe_SystemFont, IDENTIFIED rather than assumed: the
    // decoded atlas is a 256x256 A8R8G8B8 sheet of 36 columns x 9-row cells
    // starting at ASCII 32, with a FIXED 7px advance, and 7px/char reproduces the
    // retail frame's centre column to within 2px on every row measured
    // ("Control configuration details" 29 chars: predicted 203, measured ink 201;
    // "Movement: Forward" 17 chars: predicted 119, measured 117; "Player 1"
    // glyph origins 51,58,65,72,79,86,-,100 against measured '1' ink at 101).
    private const int SystemFontColumns = 36;
    private const int SystemFontCellWidth = 7;
    private const int SystemFontCellHeight = 9;
    private const int SystemFontFirstGlyph = 32;

    // ---- Dropdown popup -------------------------------------------------
    //
    // The panel is OPAQUE (proved by the subagent pass: 570 distinct background
    // colours, luma 0..255, all mapping to the single value (40,56,104) - no
    // blend can do that). Entries run on a 16px pitch that is unrelated to the
    // page's 20. Dest and width are RetailOptionsDropdownPanelDest, not the
    // measured 322.5 / 15.5 pair.
    private const float DropdownEntryPitch = 16f;

    // ---- Colours --------------------------------------------------------
    //
    // Every one of these is the modal colour of the glyph cores in the retail
    // frames, and the three that have a packed byte constant agree with it under
    // the established frontend 2x modulate:
    //   normal   0xffd6d6d6 -> (255,255,255), measured peak (255,255,255)
    //   selected 0xffffcc00 -> (255,255,0),   measured peak (255,255,0)
    //   disabled 0x50505050 -> (159,159,159) at alpha 80/255 over the (23,23,48)
    //            page fill = (66,66,83), measured brightest (66,66,83)
    private static readonly Color OptionNormal = RetailColor(0xffd6d6d6);
    private static readonly Color OptionSelected = RetailColor(0xffffcc00);
    private static readonly Color OptionDisabled = RetailColor(0x50505050);

    /// <summary>Measured exactly, unfiltered: the grid renders with hard edges.</summary>
    private static readonly Color BindingWhite = new(253f / 255f, 253f / 255f, 253f / 255f, 1f);

    /// <summary>Measured exactly (2,406 pixels carry it). Packed equivalent 0xff3f7f2f under the 2x modulate.</summary>
    private static readonly Color BindingGreen = new(126f / 255f, 253f / 255f, 94f / 255f, 1f);

    private static readonly Color BarFilled = new(0f, 160f / 255f, 0f, 1f);
    private static readonly Color BarEmpty = new(80f / 255f, 80f / 255f, 80f / 255f, 1f);

    private static readonly Color DropdownPanel = new(40f / 255f, 56f / 255f, 104f / 255f, 1f);

    private static readonly string[] OptionsPageTitles =
    [
        // CText 0x265233 GI_OPTIONS, substituted by CFEPOptions::Update when the
        // page supplies no title of its own; then Localization 3 / 2 / 1.
        "OPTIONS",
        "Controller Options",
        "Video Options",
        "Sound Options",
    ];

    private RetailOptionsMenu _options = null!;
    private Texture2D _systemFont = null!;

    /// <summary>The live options model, so a caller can read the settings the page writes.</summary>
    internal RetailOptionsMenu Options => _options;

    private void InitializeOptions()
    {
        _options = new RetailOptionsMenu(DescribeHost());
        ApplyOptionsToHost();
    }

    /// <summary>
    /// Rows whose retail state list is enumerated from the device. Reporting what
    /// this build actually is beats guessing at retail's enumeration: the retail
    /// frame's "640 x 480" / "Intel(R) UHD Graphics" / "8" / "Primary Sound
    /// Driver" are that machine's run state, not released constants.
    /// </summary>
    private static RetailOptionsHostCapabilities DescribeHost()
    {
        Vector2I window = DisplayServer.WindowGetSize();
        string adapter = RenderingServer.GetVideoAdapterName();
        return new RetailOptionsHostCapabilities(
            [$"{window.X} x {window.Y}"],
            [string.IsNullOrWhiteSpace(adapter) ? "Unknown" : adapter],
            // Localization 0xD4 "None" - this renderer requests no multisampling.
            ["None"],
            // The Godot audio server exposes one output device to this lane.
            ["Primary Sound Driver"],
            // Retail's authored default for both of these rows is the CVar
            // registration immediate -1, which both getters resolve to a PER-ADAPTER
            // recommendation ([0x009CC114] and [0x009CC0F4]). The function that
            // computes the recommendation from D3D caps was not recovered and this
            // renderer has no D3D caps path, so these two indices - full-size
            // textures, 32-bit allowed - are what a capable adapter gets. They are
            // HOST values, not authored ones, and they are the page's last open
            // number. See RetailOptionsHostCapabilities.
            RecommendedTextureResolution: 0,
            RecommendedEnable32BitTextures: 2);
    }

    /// <summary>
    /// Push the rows that HAVE a consumer at their value. VSync is the only video
    /// row this client can honour; the rest are presented and remembered only.
    /// </summary>
    private void ApplyOptionsToHost()
    {
        DisplayServer.WindowSetVsyncMode(
            _options.Settings.VSync
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);
        OptionsSettingsChanged?.Invoke(_options.Settings);
    }

    /// <summary>Raised whenever a row with a consumer commits.</summary>
    public event Action<RetailOptionsSettings>? OptionsSettingsChanged;

    // =====================================================================
    // Drawing
    // =====================================================================

    private void DrawOptions()
    {
        // The Options pages carry the identical FEP_DEVSELECT chrome. That is
        // MEASURED, not assumed: differencing the two arc bands of
        // fep-options-root-640x480.png against every 640x480 pristine reference
        // gives 5.79% against choose-game-name (FEP_DEVSELECT) and 53.19%
        // against select-level, so this page uses the FEP_DEVSELECT bracket
        // scale 1.4 at centre (328,343), not the level-select 1.25.
        //
        // Note this contradicts references/Onslaught/FrontEnd.cpp:778-797, whose
        // got_standard_SlidingTextBordersAndMask() list contains FEP_CONTROLLER
        // but NOT FEP_OPTIONS. The retail frames show the chrome on all four
        // pages, so the shipped PC build diverges from the drop here and the
        // pixels win.
        DrawMainUnderlay(1f);
        DrawRect(new Rect2(123f, 0f, 1f, DesignHeight), DevSelectGuide);
        DrawRect(new Rect2(0f, 180f, DesignWidth, 1f), DevSelectGuide);

        const float bracketScale = 1.4f;
        const float bracketShadowScale = bracketScale * ShadowScaleBoost;
        DrawSurfaceCentered(_levelBracket01, 333f, 353f, bracketShadowScale, bracketShadowScale, ShadowTint);
        DrawSurfaceCentered(_levelBracket01, 328f, 343f, bracketScale, bracketScale, BracketTint);

        DrawRect(new Rect2(191f, 69f, 394f, 21f), HeaderBoxTint);
        string title = OptionsPageTitles[(int)_options.Page];
        float titleWidth = MeasureFont22Text(title, 1f);
        DrawFont22Text(
            title,
            new Vector2(HeaderBarCenterX - (titleWidth * 0.5f), HeaderTitleTop),
            1f,
            1f,
            ReleasedTitleText);

        IReadOnlyList<RetailOptionsRow> rows = _options.Rows;
        for (int index = 0; index < rows.Count; index++)
        {
            DrawOptionRow(rows[index], _options.RowTop(index), index == _options.SelectedIndex);
        }

        // The expanded list is drawn last so it lies over the rows beneath it.
        if (_options.IsExpanded)
        {
            DrawOptionDropdown(_options.SelectedRow, _options.RowTop(_options.SelectedIndex));
        }

        // Back-only: the retail frames carry the left chevron and no right one.
        var arrowSource = new Rect2(16f, 12f, 30f, 40f);
        DrawTextureRectRegion(_feArrow, new Rect2(36f, 443f, -27f, 35f), arrowSource, BracketTint);
    }

    private void DrawOptionRow(RetailOptionsRow row, float top, bool selected)
    {
        // CApplyMenuItem::Render 0x004A4310 packs the cosine and forwards
        // it to CMenuItem__Render, which ANDs ESI with that incoming at
        // 0x004A33FC after selected 0xFFFFCC00 / disabled 0x50505050.
        // CMenuItemDropdown::Render 0x004A3C69 uses the same cosine as
        // EDI itself — it does not call CMenuItem__Render.
        // CMenuItem__Render dest leftover is RetailOptionsMenuItemDest:
        // incoming dest X minus integer-half SIZE.cx. Dest Y keeps the
        // row top. Nearby 5.0 is leftover min dest X, not dest Y.
        // CMenuItemDropdown dest leftover is RetailOptionsDropdownDest:
        // incoming dest X minus full SIZE.cx. Dest Y keeps the row top.
        // Nearby 5.0 is leftover min dest X. Nearby 2.0 is not dest.
        // CMenuItemDropdown collapsed value dest leftover is
        // RetailOptionsDropdownValueDest: incoming dest X plus the pad
        // leftover. Dest Y keeps the row top. The pad constant is not dest.
        // CMenuItemDropdown expanded list dest leftover is
        // RetailOptionsDropdownListDest: collapsed dest leftover plus the
        // pad leftover. Dest is not the pad constant.
        // CMenuItemDropdown expanded list dest Y leftover is
        // RetailOptionsDropdownListDestY: panel dest leftover plus index
        // times cy. Dest Y does not consult currentIndex.
        // CMenuItemDropdown expanded list colour leftover is
        // RetailOptionsDropdownListColor: 0xFF404040, or -1 when the
        // loop index equals currentIndex. That is colour, not dest.
        // CMenuItemDropdown expanded list hover leftover is
        // RetailOptionsDropdownListHover: call 0x004693D0 then write
        // currentIndex. That is hover, not dest and not colour.
        // CMenuItemDropdown expanded list click leftover is
        // RetailOptionsDropdownListClick: call 0x00469400 then write
        // currentIndex and the expand byte. That is click, not dest,
        // not colour, and not hover.
        // CMenuItemDropdown expanded panel dest leftover is
        // RetailOptionsDropdownPanelDest: collapsed dest leftover, dest Y
        // incoming minus integer-half of (count-1)*cy, width max cx plus 3.
        // CMenuItem__Render icon dest leftover is RetailOptionsMenuItemIconDest:
        // incoming dest X minus integer-half SIZE.cx via fsubr.
        // RetailOptionsMenuItemIconDest.DestX. Dest Y keeps the row
        // top. No leftover min dest X. Nearby 20.0 is leftover
        // label pitch, not dest. Do not invent a prefix draw.
        float seconds = (float)_animationSeconds;
        Color color;
        if (row.Kind == RetailOptionsRowKind.Dropdown &&
            RetailOptionsApplyPulse.DropdownRowIsPending(row.CommittedIndex, row.CurrentIndex))
        {
            color = RetailColor(RetailOptionsApplyPulse.PackedColor(true, seconds));
        }
        else if (row.Action == RetailOptionsAction.Apply && _options.HasPendingChanges)
        {
            color = RetailColor(RetailOptionsMenuItemColor.PackedColor(
                selected,
                enabled: true,
                RetailOptionsApplyPulse.PackedColor(true, seconds)));
        }
        else
        {
            color = selected ? OptionSelected : OptionNormal;
        }

        switch (row.Kind)
        {
            case RetailOptionsRowKind.Bindings:
                DrawBindingsGrid(top);
                return;

            case RetailOptionsRowKind.Status:
                DrawOptionTextCentered(row.Label, top, OptionDisabled);
                return;

            case RetailOptionsRowKind.ValueBar:
                DrawValueBarRow(row, top, color);
                return;

            case RetailOptionsRowKind.Dropdown:
                DrawLabelValueRow(row.Label, row.CurrentState, top, color);
                return;

            default:
                DrawOptionTextCentered(row.Label, top, color);
                return;
        }
    }

    /// <summary>
    /// Centred on the incoming dest X leftover.
    ///
    /// CMenuItem__Render dest leftover is RetailOptionsMenuItemDest:
    /// incoming dest X minus integer-half SIZE.cx. Dest Y keeps the
    /// row top. Nearby leftover min dest X is not dest Y. The dest
    /// is already a whole pixel, so this is not a half-pixel origin.
    /// The 2px MeasureText residual stays open.
    /// </summary>
    private void DrawOptionTextCentered(string text, float top, Color color) =>
        DrawOptionsBodyText(
            text,
            new Vector2(
                RetailOptionsMenuItemDest.DestX(OptionRowCenterX, (int)MeasureText(text, 1f)),
                top),
            1f,
            color);

    /// <summary>
    /// Label right-aligned on the incoming dest leftover. Value dest is
    /// incoming dest X plus the pad leftover. Dest Y keeps the row top.
    /// </summary>
    private void DrawLabelValueRow(string label, string value, float top, Color color)
    {
        // Incoming dest X minus full SIZE.cx. Nearby leftover min dest X
        // is not dest Y. Nearby 2.0 is not dest. The dest is already a
        // whole pixel, so this is not a half-pixel origin. The 2px
        // MeasureText residual stays open. Collapsed value dest leftover
        // is incoming dest X plus the pad leftover.
        // RetailOptionsDropdownValueDest.DestX. Dest Y keeps the row top.
        DrawOptionsBodyText(
            label,
            new Vector2(
                RetailOptionsDropdownDest.DestX(OptionLabelRightX, (int)MeasureText(label, 1f)),
                top),
            1f,
            color);
        DrawOptionsBodyText(
            value,
            new Vector2(RetailOptionsDropdownValueDest.DestX(OptionLabelRightX), top),
            1f,
            color);
    }

    private void DrawValueBarRow(RetailOptionsRow row, float top, Color color)
    {
        float labelWidth = MeasureText(row.Label, 1f);
        float total = labelWidth + BarLabelPad + BarAssemblyWidth;
        float left = Mathf.Floor(OptionRowCenterX - (total * 0.5f));

        DrawOptionsBodyText(row.Label, new Vector2(left, top), 1f, color);

        float leftArrowX = left + labelWidth + BarLabelPad;
        float barX = leftArrowX + BarLeftArrowToBar;
        float rightArrowX = barX + BarToRightArrow;

        // SPRITE IDENTIFIED 2026-07-27. These were Font13PS "<" and ">" glyphs
        // standing in for an unidentified sprite; the sprite is FE_Arrow, the same
        // texture this lane already materializes and already draws as the page
        // chevrons elsewhere in RetailFrontendFlow.
        //
        // CScaleMenuItem's vtable slot +0x34 (0x004A4480) loads exactly two named
        // textures and keeps them at +0x1C and +0x20:
        //   push 0x006290F4 -> [this+0x1C] = "FrontEnd\v2\FE_Arrow.tga"
        //   push 0x00629F68 -> [this+0x20] = "FrontEnd\v2\FE_Blank.tga"  (bar segment)
        // and the loader's path template at 0x00652710 is
        // "data\resources\dxtntextures\%s(%d)%s.aya", which with '\'->'%' and mip 0
        // is the exact shipped filename this lane already hashes.
        // CMenuItem__RenderValueBar (0x004A37C0) then draws [this+0x1C] TWICE, both
        // at scale 0x3E99999A = 0.3 with anchor 4 (centre) and colour argument
        // literal -1: once with rotation 0x40490FDB = pi and once with 0.
        // 0.3 x 64 = a 19.2px quad, whose ink sub-rect (16,12)-(46,52) scales to
        // exactly the 9 x 12 px of measured retail ink.
        // Specimen local-lab/safe-copy-bea-pristine/BEA.exe.original.backup,
        // sha256 74154bfa...
        //
        // Two consequences beyond the texture swap, both from those bytes:
        //  * the colour is WHITE, not the row colour - so a selected row's arrows do
        //    NOT turn yellow with its label;
        //  * a pi rotation of a vertically symmetric chevron is a horizontal flip,
        //    which is the negative-width idiom every other page in this file uses.
        var arrowInk = new Rect2(16f, 12f, 30f, 40f);
        DrawTextureRectRegion(
            _feArrow, new Rect2(leftArrowX + ArrowInkWidth, top + ArrowInkTop, -ArrowInkWidth, ArrowInkHeight),
            arrowInk, Colors.White);
        DrawTextureRectRegion(
            _feArrow, new Rect2(rightArrowX, top + ArrowInkTop, ArrowInkWidth, ArrowInkHeight),
            arrowInk, Colors.White);

        // CMenuItem__RenderValueBar draws max_value segments and NO number.
        int segments = row.MaxValue;
        float pitch = (BarWidth + 2f) / segments;
        float segmentWidth = pitch - 2f;
        for (int i = 0; i < segments; i++)
        {
            DrawRect(
                new Rect2(barX + (i * pitch), top + BarTopOffset, segmentWidth, BarHeight),
                i < row.CurrentIndex ? BarFilled : BarEmpty);
        }
    }

    private void DrawOptionDropdown(RetailOptionsRow row, float top)
    {
        if (row.States.Count == 0)
        {
            return;
        }

        // StateLabel, not States[i]: on the two rows that have a "(Recommended)"
        // treatment, retail picks between two complete label ladders on a single
        // equality against a per-adapter global, so exactly one entry in the expanded
        // list carries the suffix. Measuring States[i] would size the panel off the
        // unsuffixed text and clip the one entry that is wider.
        float widest = 0f;
        for (int i = 0; i < row.States.Count; i++)
        {
            widest = Mathf.Max(widest, MeasureText(row.StateLabel(i), 1f));
        }

        // Retail anchors the panel dest leftover on incoming dest Y minus
        // integer-half of (count-1)*cy. Dest X is the collapsed dest leftover.
        // Width is max cx plus the add ebp,3 leftover. Dest is not the pad.
        float height = row.States.Count * DropdownEntryPitch;
        DrawRect(
            new Rect2(
                RetailOptionsDropdownPanelDest.DestX(OptionLabelRightX),
                RetailOptionsDropdownPanelDest.DestY(
                    top,
                    row.States.Count,
                    (int)DropdownEntryPitch),
                RetailOptionsDropdownPanelDest.Width((int)widest),
                height),
            DropdownPanel);

        for (int i = 0; i < row.States.Count; i++)
        {
            // Incoming dest X plus collapsed pad plus the pad leftover.
            // Dest is not the pad constant. Dest Y is the panel dest
            // leftover plus index times cy. Dest Y does not consult
            // currentIndex. Colour leftover is
            // RetailOptionsDropdownListColor: 0xFF404040, or -1 when
            // the loop index equals currentIndex. That is colour, not
            // dest.
            DrawOptionsBodyText(
                row.StateLabel(i),
                new Vector2(
                    RetailOptionsDropdownListDest.DestX(OptionLabelRightX),
                    RetailOptionsDropdownListDestY.DestY(
                        top,
                        row.States.Count,
                        (int)DropdownEntryPitch,
                        i)),
                1f,
                RetailColor(RetailOptionsDropdownListColor.PackedColor(i, row.CurrentIndex)));
        }
    }

    /// <summary>
    /// Options row constants were measured from the body ink, while the shared
    /// retail text primitive accepts the shadow origin and draws body ink one
    /// pixel up-left. Adapt only this page's measured body origins; changing the
    /// shared primitive would regress every other frontend page.
    /// </summary>
    private void DrawOptionsBodyText(
        string text,
        Vector2 bodyOrigin,
        float scale,
        Color color) =>
        DrawText(text, bodyOrigin + Vector2.One, scale, color);

    /// <summary>
    /// The three-column bindings grid: slot 0 left-aligned at x=51, the action
    /// label centred on x=320, slot 1 right-aligned to x=589. All three constants
    /// are the measured ink edges of the retail frame.
    /// </summary>
    private void DrawBindingsGrid(float widgetTop)
    {
        float first = widgetTop + RetailControlBindings.TopPad;
        RetailOptionsSettings settings = _options.Settings;

        for (int index = 0; index < RetailControlBindings.Rows.Count; index++)
        {
            RetailControlBindingRow row = RetailControlBindings.Rows[index];
            if (row.Kind == RetailControlBindingRowKind.Spacer)
            {
                continue;
            }

            float top = first + (index * RetailControlBindings.RowPitch);
            DrawSystemTextCentered(row.Label, OptionRowCenterX, top, BindingGreen);

            (string slot0, string slot1) = row.Kind switch
            {
                RetailControlBindingRowKind.Header =>
                    (RetailControlBindings.Player1Header, RetailControlBindings.Player2Header),
                RetailControlBindingRowKind.InvertWalker => (
                    RetailControlBindings.InvertYLabel(settings.InvertYWalkerPlayer1),
                    RetailControlBindings.InvertYLabel(settings.InvertYWalkerPlayer2)),
                RetailControlBindingRowKind.InvertFlight => (
                    RetailControlBindings.InvertYLabel(settings.InvertYFlightPlayer1),
                    RetailControlBindings.InvertYLabel(settings.InvertYFlightPlayer2)),
                _ => (row.Slot0, row.Slot1),
            };

            Color columnColor = row.Kind == RetailControlBindingRowKind.Header
                ? BindingGreen
                : BindingWhite;
            DrawSystemText(slot0, RetailControlBindings.LeftColumnX, top, columnColor);
            DrawSystemText(
                slot1,
                RetailControlBindings.RightColumnRight - MeasureSystemText(slot1),
                top,
                columnColor);
        }
    }

    private static float MeasureSystemText(string text) => text.Length * SystemFontCellWidth;

    private void DrawSystemTextCentered(string text, float centerX, float top, Color color) =>
        DrawSystemText(text, Mathf.Floor(centerX - (MeasureSystemText(text) * 0.5f)), top, color);

    /// <summary>
    /// Fixed 7px advance out of the 36-column SystemFont sheet. No drop shadow:
    /// the retail grid renders as a single exact colour with hard edges, which a
    /// shadowed draw could not produce.
    /// </summary>
    private void DrawSystemText(string text, float x, float top, Color color)
    {
        foreach (char character in text)
        {
            int code = character;
            if (code is < SystemFontFirstGlyph or > 126)
            {
                code = '?';
            }
            int glyph = code - SystemFontFirstGlyph;
            var source = new Rect2(
                (glyph % SystemFontColumns) * SystemFontCellWidth,
                (glyph / SystemFontColumns) * SystemFontCellHeight,
                SystemFontCellWidth,
                SystemFontCellHeight);
            DrawTextureRectRegion(
                _systemFont,
                new Rect2(x, top, SystemFontCellWidth, SystemFontCellHeight),
                source,
                color);
            x += SystemFontCellWidth;
        }
    }

    // =====================================================================
    // Capture hooks. Same code paths the player drives, so a capture cannot
    // show a page the keyboard could not reach.
    // =====================================================================

    internal void SelectOptionsRowForCapture(int index)
    {
        _options.Hover(index);
        QueueRedraw();
    }

    internal void ConfirmOptionsForCapture() => ConfirmOptions();

    internal void BackFromOptionsForCapture() => BackFromOptions();

    internal bool CancelOptionsForCapture() => HandleOptionsPointerCancel(rightDown: true);

    // =====================================================================
    // Input
    // =====================================================================

    private bool HandleOptionsKey(InputEventKey key)
    {
        if (IsKey(key, Key.Up))
        {
            return NotifyOptions(_options.MoveSelection(-1), RetailFrontendAudioCue.Move);
        }
        if (IsKey(key, Key.Down))
        {
            return NotifyOptions(_options.MoveSelection(1), RetailFrontendAudioCue.Move);
        }
        if (IsKey(key, Key.Left))
        {
            return NotifyOptions(_options.Adjust(-1), RetailFrontendAudioCue.Move);
        }
        if (IsKey(key, Key.Right))
        {
            return NotifyOptions(_options.Adjust(1), RetailFrontendAudioCue.Move);
        }
        if (IsKey(key, Key.Enter) || IsKey(key, Key.KpEnter) || IsKey(key, Key.Space))
        {
            ConfirmOptions();
            return true;
        }
        if (IsKey(key, Key.Escape))
        {
            BackFromOptions();
            return true;
        }
        return false;
    }

    private bool NotifyOptions(bool changed, RetailFrontendAudioCue cue)
    {
        if (changed)
        {
            // An in-range adjustment plays CFrontEnd__PlaySound(0); a clamped one
            // is silent, which is why this is gated on the model reporting change.
            RequestAudioCue(cue);
            ApplyOptionsToHost();
            QueueRedraw();
        }
        return true;
    }

    private void ConfirmOptions()
    {
        if (!RetailFrontendScenePath.TryConfirmOptions(
                _options,
                startupMediaActive: false,
                out RetailOptionsSignal signal))
        {
            return;
        }

        if (signal == RetailOptionsSignal.Closed)
        {
            BackFromOptions();
            return;
        }

        RequestAudioCue(RetailFrontendAudioCue.Select);
        ApplyOptionsToHost();
        QueueRedraw();
    }

    private void BackFromOptions()
    {
        RetailOptionsSignal signal = _options.Back();
        if (signal is RetailOptionsSignal.PageChanged or RetailOptionsSignal.ValueChanged)
        {
            RequestAudioCue(RetailFrontendAudioCue.Back);
            QueueRedraw();
            return;
        }

        if (!RetailFrontendScenePath.TryBackPage(
                _session,
                startupMediaActive: false,
                out RetailFrontendSignal frontend))
        {
            return;
        }
        RequestAudioCue(RetailFrontendAudioCue.Back);
        HandleNavigationSignal(frontend);
        QueueRedraw();
    }

    private bool HandleOptionsPointerMotion(Vector2 design)
    {
        // CMenuItemDropdown expanded list hover leftover is
        // RetailOptionsDropdownListHover: call 0x004693D0 then write
        // currentIndex. That is hover, not dest and not colour. Click
        // at 0x004A4010 is a later leftover.
        if (_options.IsExpanded)
        {
            RetailOptionsRow expanded = _options.SelectedRow;
            float rowTop = _options.RowTop(_options.SelectedIndex);
            int labelCx = (int)MeasureText(expanded.Label, 1f);
            for (int i = 0; i < expanded.States.Count; i++)
            {
                if (!RetailOptionsDropdownListHover.Contains(
                    design.X,
                    design.Y,
                    RetailOptionsDropdownListDest.DestX(OptionLabelRightX),
                    RetailOptionsDropdownListDestY.DestY(
                        rowTop,
                        expanded.States.Count,
                        (int)DropdownEntryPitch,
                        i),
                    labelCx,
                    (int)DropdownEntryPitch))
                {
                    continue;
                }

                if (!_options.HoverState(i))
                {
                    return false;
                }

                RequestAudioCue(RetailFrontendAudioCue.Move);
                return true;
            }

            return false;
        }

        // Hovering IS selecting: the retail hover path sets the selected index and
        // does not consult IsEnabled, and the click path then injects the same
        // 0x2C/0x33 pair the keyboard produces.
        int index = _options.RowAt(design.Y);
        if (index < 0 || !_options.Hover(index))
        {
            return false;
        }
        RequestAudioCue(RetailFrontendAudioCue.Move);
        return true;
    }

    private bool HandleOptionsPointerConfirm(Vector2 design)
    {
        if (new Rect2(0f, 430f, 46f, 48f).HasPoint(design))
        {
            BackFromOptions();
            return true;
        }

        if (_options.IsExpanded)
        {
            // CMenuItemDropdown expanded list click leftover is
            // RetailOptionsDropdownListClick: call 0x00469400 then write
            // currentIndex and the expand byte. That is click, not dest
            // and not colour. Hover leftover already owns 0x004A3FA6.
            // CMenuItemDropdown click-hit sound leftover is
            // RetailOptionsDropdownListClickSound: push 1 then
            // call 0x00468770. That is not dest, not colour, not
            // hover, not click, and not cancel.
            RetailOptionsRow expanded = _options.SelectedRow;
            float rowTop = _options.RowTop(_options.SelectedIndex);
            int labelCx = (int)MeasureText(expanded.Label, 1f);
            for (int i = 0; i < expanded.States.Count; i++)
            {
                if (!RetailOptionsDropdownListClick.Contains(
                    design.X,
                    design.Y,
                    RetailOptionsDropdownListDest.DestX(OptionLabelRightX),
                    RetailOptionsDropdownListDestY.DestY(
                        rowTop,
                        expanded.States.Count,
                        (int)DropdownEntryPitch,
                        i),
                    labelCx,
                    (int)DropdownEntryPitch))
                {
                    continue;
                }

                _options.SelectState(i);
                if (RetailOptionsDropdownListClickSound.Applies(hit: true))
                {
                    ConfirmOptions();
                }

                return true;
            }
            ConfirmOptions();
            return true;
        }

        int index = _options.RowAt(design.Y);
        if (index < 0)
        {
            return false;
        }

        RetailOptionsRow row = _options.Rows[index];
        if (!row.IsSelectable)
        {
            return false;
        }

        if (_options.Hover(index))
        {
            RequestAudioCue(RetailFrontendAudioCue.Move);
        }

        // A value bar's own +/- hotspots: CMenuItem__RenderValueBar maps a click in
        // [x, x+40] x [y, y+16] to button 0x36 and [x+60, x+120] to 0x37.
        if (row.Kind == RetailOptionsRowKind.ValueBar)
        {
            float labelWidth = MeasureText(row.Label, 1f);
            float left = OptionRowCenterX -
                ((labelWidth + BarLabelPad + BarAssemblyWidth) * 0.5f);
            float leftArrowX = left + labelWidth + BarLabelPad;
            float barX = leftArrowX + BarLeftArrowToBar;
            if (design.X < barX)
            {
                return NotifyOptions(_options.Adjust(-1), RetailFrontendAudioCue.Move);
            }
            if (design.X >= barX + BarToRightArrow)
            {
                return NotifyOptions(_options.Adjust(1), RetailFrontendAudioCue.Move);
            }
            return true;
        }

        ConfirmOptions();
        return true;
    }

    private bool HandleOptionsPointerCancel(bool rightDown)
    {
        // CMenuItemDropdown post-loop cancel leftover is
        // RetailOptionsDropdownListCancel: call 0x0044DEA0 then, when
        // 0x0089BE28 is set, write currentIndex from committedIndex and
        // the expand byte. That is cancel, not dest, not colour, not
        // hover, and not click. Click leftover already owns 0x004A4010.
        // Hover leftover already owns 0x004A3FA6. Latch-to-button SET
        // leftover is RetailFrontendLatchToButton: test ah, 0x80 then
        // mov [0x0089BE28], 1. That is the SET cancel leftover reads.
        // FMV skip already owns the OR at 0x0053F2EB.
        if (!RetailFrontendScenePath.AcceptsOptionsPointerCancel(rightDown))
        {
            return false;
        }

        if (!_options.CancelExpanded())
        {
            return false;
        }

        RequestAudioCue(RetailFrontendAudioCue.Back);
        QueueRedraw();
        return true;
    }
}
