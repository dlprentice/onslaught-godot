// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// The released four-page options widget tree, as presentation-owned state.
///
/// <para><b>Where this comes from.</b></para>
/// <c>FEPOptions.cpp</c>, <c>MenuItem.cpp</c> and <c>PauseMenu.cpp</c> are all
/// ABSENT from the pinned GPL drop (verified: <c>references/Onslaught/</c> has
/// only the <c>#include "FEPOptions.h"</c> at <c>Frontend.h:30</c>, the member at
/// <c>Frontend.h:241</c>, and the page-table slot at <c>FrontEnd.cpp:126</c>).
/// So the widget layer is recovered from the shipped bytes and from real retail
/// pixels, not ported. The byte recovery is
/// <c>local-lab/OPTIONS-PAGE-RECOVERY-2026-07-27.md</c>; the pixels are the nine
/// PNGs in <c>local-lab/retail-captures-options-pause-2026-07-27/</c>.
///
/// What IS in the drop, and is ported here rather than re-derived, is the career
/// option fields and their defaults (<c>Career.h:201-207</c>,
/// <c>Career.cpp:173-177</c>), the invert-Y pitch negation
/// (<c>Player.cpp:325-332</c>), and the four controller configurations
/// (<c>PCController.cpp:91-136</c>). Released PC volume setters diverge from the
/// retained curves: sound stores the supplied float directly, while music stores
/// <c>round(volume * 127)</c> and preserves the original career float. Downstream
/// device math and audible parity remain unproved.
///
/// <para><b>What retail proves about the tree's shape.</b></para>
/// One <c>PauseMenu__Init</c> (<c>0x004CDE60</c>) builds both the in-game pause
/// menu (mode 0, row origin y=240) and the frontend Options page (mode 1, row
/// origin y=300); <c>CFEPOptions</c> builds no rows of its own. This type models
/// the mode-1 tree only. The pause menu keeps its existing owner
/// (<see cref="Level100PauseMenu"/>) and is NOT wired to this one yet.
///
/// <para><b>Determinism.</b></para>
/// No filesystem, clock, process, network or GPU dependency. Rows whose retail
/// state set depends on the host (screen mode, video adapter) take their labels
/// from the caller, so this type stays reproducible in a test.
/// </summary>
public sealed class RetailOptionsMenu
{
    /// <summary>
    /// Row heights, in released design pixels.
    ///
    /// <c>CMenuItem__GetRowHeight</c> returns <c>0x14</c> and the <c>0x28</c>
    /// branch needs <c>item+0x0C != 0</c>, which no options row sets, so 20 is
    /// the released height for a plain row. The other two are MEASURED off
    /// <c>fep-options-controller-640x480.png</c> and have no byte explanation:
    /// the Joystick status rows and the Back row beneath them sit on a 17px
    /// pitch, and the bindings sub-widget occupies 230px (22 grid rows on a 10px
    /// pitch plus a 5px pad at each end).
    /// </summary>
    public const float RowHeight = 20f;
    public const float BindingsRowHeight = 230f;
    public const float JoystickRowHeight = 17f;

    /// <summary>
    /// Row origin for the frontend (mode 1) tree. <c>PauseMenu__Init</c> stores
    /// 300.0 for any mode other than 0.
    /// </summary>
    public const float RangeOriginY = 300f;

    /// <summary>
    /// The released range's top inset. Derived from the capture and confirmed by
    /// the existing pause-menu implementation, which already carries the same 15:
    /// first row top = origin - totalHeight/2 - 15.
    ///
    /// This reproduces the measured first-row cell top EXACTLY on all four pages:
    /// root 4x20=80 -> 245, Sound 9x20=180 -> 195, Video 14x20=280 -> 145, and
    /// Controller 20+20+230+4x17+20=358 -> 106. The Controller number is the one
    /// that constrains the mixed row heights: only Back-at-20 with the Joysticks
    /// at 17 puts the bindings grid's first line on 151, the Joystick rows on
    /// 376/393/410/427 and Back on 444 - every one of which is a measured ink
    /// position on the retail frame.
    /// </summary>
    public const float RangeTopInset = 15f;

    /// <summary>
    /// <c>g_MouseSensitivity = (index + 1) * 3.0f</c>. Setter <c>0x004CEFE0</c>,
    /// the 3.0 is the float32 at <c>0x005D8CC0</c>, read from the pristine
    /// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
    /// (sha256 <c>74154bfa...</c>) - never from the installed, deliberately
    /// patched <c>BEA.exe</c>.
    /// </summary>
    public const float MouseSensitivityStep = 3f;

    /// <summary>
    /// <c>max_value = 0x14</c> at <c>PauseMenu.cpp:1373</c>, and
    /// <c>CMenuItem__ButtonPressed</c> (<c>0x004A43A0</c>) clamps INCLUSIVELY, so
    /// the row has 21 stops: 3, 6, ... 63.
    /// </summary>
    public const int MouseSensitivityMaxValue = 20;

    /// <summary>
    /// The image's own static initialiser for <c>g_MouseSensitivity</c> at VA
    /// <c>0x006254F4</c>. It is OFF the (index+1)*3 lattice, which is why the
    /// index it maps to does not map back to it.
    /// </summary>
    public const float DefaultMouseSensitivity = 7f;

    /// <summary>11 stops on both volume rows (<c>max_value = 10</c>).</summary>
    public const int VolumeMaxValue = 10;

    /// <summary>
    /// The rounding bias <c>CMenuItem__Init</c> applies when it seeds a value
    /// row's index: <c>ROUND(max_value * scale + 0.48)</c>, the 0.48 being the
    /// float32 at <c>0x005DC560</c>. Corroborated by pixels: Sound 0.8 gives
    /// <c>ROUND(8.48) = 8</c> and Music 0.9 gives <c>ROUND(9.48) = 9</c>, and the
    /// retail Sound Options frame has exactly 8 and 9 filled segments.
    /// </summary>
    public const float ValueRowSeedBias = 0.48f;

    private readonly List<RetailOptionsRow> _root;
    private readonly List<RetailOptionsRow> _controller;
    private readonly List<RetailOptionsRow> _video;
    private readonly List<RetailOptionsRow> _sound;

    public RetailOptionsMenu(RetailOptionsHostCapabilities? host = null)
    {
        Host = host ?? RetailOptionsHostCapabilities.Unknown;
        Settings = new RetailOptionsSettings();
        _root = BuildRoot();
        _controller = BuildController();
        _video = BuildVideo(Host);
        _sound = BuildSound(Host);
        SyncFromSettings();
    }

    /// <summary>Host-supplied labels for the rows whose retail state set is enumerated from the device.</summary>
    public RetailOptionsHostCapabilities Host { get; }

    /// <summary>The values the rows read and write. Only a subset has a consumer - see the row table.</summary>
    public RetailOptionsSettings Settings { get; }

    public RetailOptionsPage Page { get; private set; } = RetailOptionsPage.Root;

    public int SelectedIndex { get; private set; }

    /// <summary>True while a dropdown row is expanded and owns up/down.</summary>
    public bool IsExpanded { get; private set; }

    /// <summary>
    /// Set whenever a deferred row holds an uncommitted change. Retail pulses the
    /// Apply row on a cosine while this is set (<c>CApplyMenuItem::Render</c>
    /// <c>0x004A4310</c>, gate <c>0x00704A88</c>).
    /// </summary>
    public bool HasPendingChanges
    {
        get
        {
            foreach (RetailOptionsRow row in Rows)
            {
                if (row.Timing == RetailOptionsApplyTiming.OnApply &&
                    row.CurrentIndex != row.CommittedIndex)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public IReadOnlyList<RetailOptionsRow> Rows => Page switch
    {
        RetailOptionsPage.Root => _root,
        RetailOptionsPage.Controller => _controller,
        RetailOptionsPage.Video => _video,
        RetailOptionsPage.Sound => _sound,
        _ => throw new InvalidOperationException($"Unsupported options page {Page}."),
    };

    public RetailOptionsRow SelectedRow => Rows[SelectedIndex];

    /// <summary>Total height of the current page's rows, in design pixels.</summary>
    public float PageHeight
    {
        get
        {
            float total = 0f;
            foreach (RetailOptionsRow row in Rows)
            {
                total += row.Height;
            }
            return total;
        }
    }

    /// <summary>Cell top of the first row on the current page.</summary>
    public float FirstRowTop => RangeOriginY - (PageHeight * 0.5f) - RangeTopInset;

    /// <summary>Cell top of <paramref name="index"/> on the current page.</summary>
    public float RowTop(int index)
    {
        float y = FirstRowTop;
        IReadOnlyList<RetailOptionsRow> rows = Rows;
        for (int i = 0; i < index; i++)
        {
            y += rows[i].Height;
        }
        return y;
    }

    /// <summary>Row under a design-space y, or -1.</summary>
    public int RowAt(float designY)
    {
        float y = FirstRowTop;
        IReadOnlyList<RetailOptionsRow> rows = Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (designY >= y && designY < y + rows[i].Height)
            {
                return i;
            }
            y += rows[i].Height;
        }
        return -1;
    }

    /// <summary>Enter the page fresh, re-syncing every row from the live values.</summary>
    public void Enter(RetailOptionsPage page)
    {
        Page = page;
        IsExpanded = false;
        SyncFromSettings();
        SelectedIndex = 0;
        EnsureSelectable(1);
    }

    public void Reset()
    {
        Page = RetailOptionsPage.Root;
        IsExpanded = false;
        SelectedIndex = 0;
        SyncFromSettings();
        EnsureSelectable(1);
    }

    /// <summary>
    /// Up/down. Retail's <c>CPauseMenu::HandleKeyPress</c> skips disabled rows and
    /// wraps only when the page has three or more rows.
    /// </summary>
    public bool MoveSelection(int direction)
    {
        if (direction == 0)
        {
            return false;
        }

        IReadOnlyList<RetailOptionsRow> rows = Rows;
        if (IsExpanded)
        {
            return AdjustExpanded(Math.Sign(direction));
        }

        int step = Math.Sign(direction);
        int candidate = SelectedIndex;
        bool wraps = rows.Count >= 3;
        for (int attempt = 0; attempt < rows.Count; attempt++)
        {
            candidate += step;
            if (candidate < 0 || candidate >= rows.Count)
            {
                if (!wraps)
                {
                    return false;
                }
                candidate = (candidate + rows.Count) % rows.Count;
            }
            if (rows[candidate].IsSelectable)
            {
                bool moved = candidate != SelectedIndex;
                SelectedIndex = candidate;
                return moved;
            }
        }
        return false;
    }

    public bool Hover(int index)
    {
        IReadOnlyList<RetailOptionsRow> rows = Rows;
        if (IsExpanded || index < 0 || index >= rows.Count || !rows[index].IsSelectable)
        {
            return false;
        }
        bool moved = index != SelectedIndex;
        SelectedIndex = index;
        return moved;
    }

    /// <summary>
    /// Expanded list hover leftover. Official 74154bfa writes
    /// <c>[this+0x20]</c> when <c>0x004693D0</c> returns true. That is
    /// currentIndex, not dest, not colour, and not a Live apply. Click
    /// at <c>0x004A4010</c> is a later leftover.
    /// </summary>
    public bool HoverState(int index)
    {
        if (!IsExpanded)
        {
            return false;
        }

        RetailOptionsRow row = SelectedRow;
        if (index < 0 || index >= row.States.Count)
        {
            return false;
        }

        int next = RetailOptionsDropdownListHover.CurrentIndexAfterHover(
            row.CurrentIndex,
            index,
            hit: true);
        if (next == row.CurrentIndex)
        {
            return false;
        }

        row.CurrentIndex = next;
        return true;
    }

    /// <summary>
    /// Post-loop cancel leftover. Official 74154bfa writes
    /// <c>[this+0x20]</c> from <c>[this+0x1c]</c> and
    /// <c>[this+0x24]=0</c> when <c>0x0044DEA0</c> returns 0 and
    /// <c>[0x0089BE28]</c> is set. That is not dest, not colour, not
    /// hover, and not click. Back (<c>0x2E</c>) does not own this
    /// leftover.
    /// </summary>
    public bool CancelExpanded()
    {
        if (!IsExpanded)
        {
            return false;
        }

        RetailOptionsRow row = SelectedRow;
        row.CurrentIndex = RetailOptionsDropdownListCancel.CurrentIndexAfterCancel(
            row.CurrentIndex,
            row.CommittedIndex,
            apply: true);
        IsExpanded = RetailOptionsDropdownListCancel.ExpandAfterCancel(
            IsExpanded,
            apply: true);
        return true;
    }

    /// <summary>
    /// Left/right (buttons <c>0x36</c>/<c>0x37</c>). On a value bar this steps the
    /// index directly - value rows are not entered first. On a dropdown, retail
    /// EXPANDS the list and moves by one in the same press.
    /// </summary>
    public bool Adjust(int direction)
    {
        if (direction == 0)
        {
            return false;
        }

        RetailOptionsRow row = SelectedRow;
        int step = Math.Sign(direction);

        if (row.Kind == RetailOptionsRowKind.Dropdown)
        {
            IsExpanded = true;
            return AdjustExpanded(step);
        }

        if (row.Kind != RetailOptionsRowKind.ValueBar)
        {
            return false;
        }

        int next = row.CurrentIndex + step;
        if (next < 0)
        {
            // Retail floors at 0 and plays no sound on a clamped press.
            return false;
        }
        if (next > row.MaxValue)
        {
            return false;
        }

        row.CurrentIndex = next;
        // notify_on_change = 1 on all three value rows, so the setter runs and
        // the row commits on every in-range press.
        row.CommittedIndex = next;
        ApplyRow(row);
        return true;
    }

    /// <summary>Select (<c>0x2C</c>) / activate (<c>0x33</c>).</summary>
    public RetailOptionsSignal Confirm()
    {
        RetailOptionsRow row = SelectedRow;

        if (IsExpanded)
        {
            IsExpanded = false;
            if (row.Timing == RetailOptionsApplyTiming.OnRowClose &&
                row.CurrentIndex != row.CommittedIndex)
            {
                row.CommittedIndex = row.CurrentIndex;
                ApplyRow(row);
            }
            return RetailOptionsSignal.ValueChanged;
        }

        switch (row.Kind)
        {
            case RetailOptionsRowKind.PageLink:
                Enter(row.TargetPage);
                return RetailOptionsSignal.PageChanged;

            case RetailOptionsRowKind.Dropdown:
                IsExpanded = true;
                return RetailOptionsSignal.ValueChanged;

            case RetailOptionsRowKind.Action when row.Action == RetailOptionsAction.Apply:
                ApplyPage();
                return RetailOptionsSignal.Applied;

            case RetailOptionsRowKind.Action when row.Action == RetailOptionsAction.Back:
                return Back();

            case RetailOptionsRowKind.Action when row.Action == RetailOptionsAction.Credits:
                // Retail draws Credits enabled and enters FEP_CREDITS. This lane
                // has no credits page, so nothing navigates - the same bounded
                // absence RetailFrontendSession already uses for Load/Multiplayer.
                return RetailOptionsSignal.None;

            default:
                return RetailOptionsSignal.None;
        }
    }

    /// <summary>
    /// Back (<c>0x2E</c>). Byte-recovered behaviour: an expanded dropdown just
    /// collapses; a subpage returns to the root; the root leaves Options. Back
    /// NEVER reverts a deferred value and there is no confirm prompt.
    /// </summary>
    public RetailOptionsSignal Back()
    {
        if (IsExpanded)
        {
            IsExpanded = false;
            return RetailOptionsSignal.ValueChanged;
        }

        if (Page == RetailOptionsPage.Root)
        {
            return RetailOptionsSignal.Closed;
        }

        Enter(RetailOptionsPage.Root);
        return RetailOptionsSignal.PageChanged;
    }

    /// <summary>
    /// The Apply row's walk: <c>CApplyMenuItem::ButtonPressed</c>
    /// (<c>0x004A4290</c>) calls slot <c>+0x2C</c> on EVERY row of the page, and
    /// every dropdown's <c>+0x2C</c> is <c>CMenuItemDropdown__CommitSelection</c>
    /// (<c>0x004A40F0</c>), which reaches the runtime only when pending differs
    /// from committed.
    /// </summary>
    public void ApplyPage()
    {
        foreach (RetailOptionsRow row in Rows)
        {
            if (row.CurrentIndex == row.CommittedIndex)
            {
                continue;
            }
            row.CommittedIndex = row.CurrentIndex;
            ApplyRow(row);
        }
    }

    /// <summary>
    /// Re-seed every row's index from the live values. Retail does this on page
    /// entry (<c>ResetIterator</c> then each row's GET at slot <c>+0x3C</c>),
    /// which is what silently discards a pending deferred change.
    /// </summary>
    public void SyncFromSettings()
    {
        foreach (List<RetailOptionsRow> page in new[] { _root, _controller, _video, _sound })
        {
            foreach (RetailOptionsRow row in page)
            {
                int index = row.Read?.Invoke(Settings) ?? row.CurrentIndex;
                row.CurrentIndex = index;
                row.CommittedIndex = index;
            }
        }
    }

    /// <summary>
    /// <c>index = ROUND(sensitivity / 3 + 0.5) - 1</c>, the inline expression at
    /// <c>0x004CF000</c> / <c>0x004CE292</c>. The x87 rounding mode at that
    /// <c>fistp</c> is UNPROVEN (recovery note section 8.2); round-to-nearest is
    /// used here because the release build does not pass
    /// <c>D3DCREATE_FPU_PRESERVE</c> (<c>references/Onslaught/d3dapp.cpp:337-340</c>
    /// puts that flag inside <c>#ifdef _DEBUG</c>).
    /// </summary>
    public static int MouseSensitivityIndex(float sensitivity)
    {
        int index = (int)Math.Round(
            (sensitivity / MouseSensitivityStep) + 0.5f,
            MidpointRounding.ToEven) - 1;
        return Math.Clamp(index, 0, MouseSensitivityMaxValue);
    }

    public static float MouseSensitivityValue(int index) =>
        (Math.Clamp(index, 0, MouseSensitivityMaxValue) + 1) * MouseSensitivityStep;

    /// <summary><c>ROUND(max_value * scale + 0.48)</c>.</summary>
    public static int VolumeIndex(float scale) => Math.Clamp(
        (int)Math.Round((VolumeMaxValue * scale) + ValueRowSeedBias, MidpointRounding.AwayFromZero),
        0,
        VolumeMaxValue);

    public static float VolumeValue(int index) =>
        Math.Clamp(index, 0, VolumeMaxValue) / (float)VolumeMaxValue;

    // These rows retain the raw slider/career float. Released PC conversion
    // splits downstream: CSoundManager::SetMasterVolume at 0x004E04C0 stores the
    // sound float directly, while CMusic::SetVolume at 0x004BBA10 stores
    // round(volume * 127) and preserves the original career float. The Godot
    // audio owner adapts those distinct values at its presentation boundary;
    // this Client lane does not infer DirectSound or audible-volume device math.

    /// <summary>
    /// Move an expanded dropdown's pending selection straight to
    /// <paramref name="index"/>. The mouse path reaches an entry directly; the
    /// keyboard path only ever steps by one.
    /// </summary>
    public bool SelectState(int index)
    {
        RetailOptionsRow row = SelectedRow;
        if (!IsExpanded || index < 0 || index >= row.States.Count || index == row.CurrentIndex)
        {
            return false;
        }
        return AdjustExpanded(index - row.CurrentIndex);
    }

    private bool AdjustExpanded(int step)
    {
        RetailOptionsRow row = SelectedRow;
        int next = Math.Clamp(row.CurrentIndex + step, 0, row.States.Count - 1);
        if (next == row.CurrentIndex)
        {
            return false;
        }
        row.CurrentIndex = next;
        if (row.Timing == RetailOptionsApplyTiming.Live)
        {
            row.CommittedIndex = next;
            ApplyRow(row);
        }
        return true;
    }

    private void ApplyRow(RetailOptionsRow row) => row.Write?.Invoke(Settings, row.CommittedIndex);

    private void EnsureSelectable(int direction)
    {
        if (Rows[SelectedIndex].IsSelectable)
        {
            return;
        }
        MoveSelection(direction);
    }

    // ---------------------------------------------------------------------
    // The row table. Labels are the exact English strings recovered from the
    // executable's own Localization arm (English is language index 4, extent
    // 0x00524830-0x00527960) and independently confirmed against the retail
    // frames; the recovery note reproduces the project's established anchor
    // (index 0x77 -> "Click to start") from the same decode.
    // ---------------------------------------------------------------------

    private static List<RetailOptionsRow> BuildRoot() =>
    [
        // PauseMenu.cpp:1346/1347/1348 - Controller, Sound, Video in that order,
        // which fep-options-root-640x480.png reproduces exactly.
        RetailOptionsRow.Link("Controller Options", RetailOptionsPage.Controller),
        RetailOptionsRow.Link("Sound Options", RetailOptionsPage.Sound),
        RetailOptionsRow.Link("Video Options", RetailOptionsPage.Video),
        // PauseMenu.cpp:1353, CText 0x437A3E JCL_CREDITS - present in mode 1 only.
        RetailOptionsRow.Act("Credits", RetailOptionsAction.Credits),
    ];

    private static List<RetailOptionsRow> BuildController() =>
    [
        // PauseMenu.cpp:1373 - CMenuItem__InitWithIcon(item, 0x4E, 3, 0.0f, NULL,
        // 0x14, 1): 21 stops, notify_on_change = 1.
        RetailOptionsRow.Bar(
            "Mouse sensitivity:",
            MouseSensitivityMaxValue,
            static s => MouseSensitivityIndex(s.MouseSensitivity),
            static (s, i) => s.MouseSensitivity = MouseSensitivityValue(i)),

        // PauseMenu.cpp:1378, CConfigName. INDEX -> PRESET READ 2026-07-27; this was
        // the recovery note's open item 8.4 and the implementation note's GUESS 3.
        //
        // The chain closes without inference. Its +0x40 state count (0x004059C0) is a
        // body whose whole text is `mov eax,2; ret`, and its +0x44 (0x004D01F0)
        // returns Localization 0xF0 "Custom" for state 0 and 0xF3 "WASD + mouse" for
        // anything else. What was missing was whether the dropdown INDEX is the same
        // integer as the preset SCHEME. It is, in both directions and with no remap:
        //   SET +0x38 (0x004D01D0) passes the index straight to Controls__ApplyPreset
        //     (0x00453780), whose first act is `mov [0x00677D70], ebp` at 0x0045378A
        //     - the index stored verbatim;
        //   GET +0x3C (0x004D01E0) is `mov eax,[0x00677D70]; ret`.
        // ApplyPreset's own shape corroborates the labels: `test ebp,ebp; je` at
        // 0x00453787 means scheme 0 applies NO bindings, which is exactly what
        // "Custom" denotes; nonzero schemes walk the 0x20-byte binding records at
        // 0x00677AF0, using the scheme integer as the count of terminators to skip.
        // The virgin-install path calls Controls__ApplyPreset(1) (0x004EFB10), and
        // Career.cpp:175 sets mControllerConfigurationNum to 1 independently.
        // => index 0 = Custom, index 1 = WASD + mouse, shipped default 1.
        //
        // The other three names in the table - 0xF1 "Player 1 joystick, player 2
        // joystick", 0xF2 "WASD + mouse, joystick", 0xF4 "Multiplayer keyboard +
        // mouse" - are unreachable because +0x40 returns a literal 2.
        //
        // NOT the same mechanism as references/Onslaught/PCController.cpp:91-136's
        // four PAD layouts, and `data/battle engine configurations.dat` (loaded by
        // BattleEngineConfigurations__Load 0x0040F180) is the player VEHICLE's
        // configuration table and has nothing to do with this row.
        RetailOptionsRow.Drop(
            "Configuration:",
            ["Custom", "WASD + mouse"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.ControllerConfiguration,
            static (s, i) => s.ControllerConfiguration = i),

        // PauseMenu.cpp:1392 - the CControllerDefinition bindings sub-widget.
        RetailOptionsRow.Bindings(),

        // PauseMenu.cpp:1394-1397. GetText override 0x004D0310 ignores +0x18 and
        // composes from the joystick index at +0x1C.
        RetailOptionsRow.Status("Joystick 1: Not present", JoystickRowHeight),
        RetailOptionsRow.Status("Joystick 2: Not present", JoystickRowHeight),
        RetailOptionsRow.Status("Joystick 3: Not present", JoystickRowHeight),
        RetailOptionsRow.Status("Joystick 4: Not present", JoystickRowHeight),

        RetailOptionsRow.Act("Back", RetailOptionsAction.Back),
    ];

    private static List<RetailOptionsRow> BuildVideo(RetailOptionsHostCapabilities host) =>
    [
        // PauseMenu.cpp:1408, CnVidia. Two states when PS2.0 is present, else one:
        // Localization 0xF8 "Not available". This renderer has no such path.
        RetailOptionsRow.Drop(
            "Extra graphical features:",
            ["Not available"],
            RetailOptionsApplyTiming.OnApply,
            null,
            null),

        // STATE ORDER READ, 2026-07-27, was inferred from id adjacency. CVideoDetailLevel's
        // +0x44 label vfunc (0x004D01A0) is `Localization(state + 0xE0)`, and the English arm
        // gives 0xE0 Custom, 0xE1 Lowest, 0xE2 Medium, 0xE3 High. Specimen
        // local-lab/safe-copy-bea-pristine/BEA.exe.original.backup, sha256 74154bfa...
        RetailOptionsRow.Drop(
            "Overall Detail level:",
            ["Custom", "Lowest", "Medium", "High"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.OverallDetail,
            static (s, i) => s.OverallDetail = i),

        RetailOptionsRow.Drop(
            "Screen mode:",
            host.ScreenModes,
            RetailOptionsApplyTiming.OnApply,
            null,
            null),

        // STATE ORDER READ: CShadowDetail shares CGeometryDetail's +0x44 vfunc
        // (0x004CEED0), which is `Localization(state + 0x10)` = 0x10 Low, 0x11 Medium,
        // 0x12 High. Its SET (0x004CEEF0) writes the byte pair
        // [0x009C7C54],[0x009C7C56] as 0->(0,0), 1->(0,1), 2->(1,1) and its GET
        // (0x004CEF30) inverts that exactly.
        RetailOptionsRow.Drop(
            "Shadow detail:",
            ["Low", "Medium", "High"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.ShadowDetail,
            static (s, i) => s.ShadowDetail = i),

        // The three states' constants are hand-read from the setter 0x004DD6B0:
        // distance 10/30/70, LOD bias 3.0/1.0/0.3, scale 0.1/1.0/2.0. The image's
        // own static initialisers are 30.0/1.0/1.0 - exactly state 1, Medium.
        RetailOptionsRow.Drop(
            "Geometry detail:",
            ["Low", "Medium", "High"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.GeometryDetail,
            static (s, i) => s.GeometryDetail = i),

        // STATE ORDER READ: CTrilinear's +0x44 is the shared CBOOLMenuItem label vfunc
        // 0x004A4220 - state 0 returns Localization 0x06 "No", any other state 0x05
        // "Yes". So No is index 0 on EVERY Yes/No row, not Yes.
        RetailOptionsRow.Drop(
            "Trilinear mipmapping:",
            ["No", "Yes"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.TrilinearMipmapping ? 1 : 0,
            static (s, i) => s.TrilinearMipmapping = i != 0),

        RetailOptionsRow.Drop(
            "Video adapter:",
            host.VideoAdapters,
            RetailOptionsApplyTiming.OnApply,
            null,
            null),

        // "Screen shape:" (PauseMenu.cpp:1427) is deliberately absent. It is gated
        // on 0x0089C0AC (ALLOW_WIDESCREEN_MODES) and the retail Video Options
        // frame has 14 rows with no such row, so the shipped build's gate is off.
        // CVSync's +0x44 (0x004CF550) delegates to the same 0x004A4220, so No is 0.
        // Its "forced" arm - which would return a bare "Yes" and make +0x40
        // (0x004CF580) report ONE state - is gated on [0x0089C07C], and that global
        // is BSS with five .text references image-wide, ALL reads. It is never
        // written, so the shipped row is always two-state.
        RetailOptionsRow.Drop(
            "VSync:",
            ["No", "Yes"],
            RetailOptionsApplyTiming.OnApply,
            static s => s.VSync ? 1 : 0,
            static (s, i) => s.VSync = i != 0),

        // STATE ORDER READ. This row is the trap: its Localization ids run DOWNWARDS
        // as the index runs up, so ordering by id adjacency gives exactly the wrong
        // answer. CLandscapeRes's +0x44 (0x004CF590) is an explicit ladder -
        // 0 -> 0x25 "Lowest", 1 -> 0x24 "Low", 2 -> 0x23 "Medium", else 0x22 "High".
        // The SET (0x004CF620) agrees: its arms write cvar 1.0 / 3.0 / 4.0 / 5.0 and
        // the GET (0x004CF690) maps 1 -> 0, 3 -> 1, 4 -> 2, else -> 3.
        RetailOptionsRow.Drop(
            "Landscape resolution:",
            ["Lowest", "Low", "Medium", "High"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.LandscapeResolution,
            static (s, i) => s.LandscapeResolution = i),

        // STATE ORDER READ, and it was REVERSED before. CTextureRes's +0x44
        // (0x004CF700) returns 0x1A "High" / 0x1B "Medium" / 0x1C "Low" for states
        // 0/1/2, swapping to the 0x17/0x18/0x19 "(Recommended)" variants for the one
        // state that equals [0x009CC114]. The consumer agrees: the index is used
        // directly as a mip-loss shift in LoadTextureFromFile (0x00557300), so index
        // 0 shifts by nothing and IS the largest texture.
        RetailOptionsRow.Recommended(
            "Texture Resolution:",
            ["High", "Medium", "Low"],
            ["High (Recommended)", "Medium (Recommended)", "Low (Recommended)"],
            host.RecommendedTextureResolution,
            RetailOptionsApplyTiming.OnApply,
            s => s.TextureResolution < 0 ? host.RecommendedTextureResolution : s.TextureResolution,
            static (s, i) => s.TextureResolution = i),

        // STATE ORDER READ: CEnable32Bit's +0x44 (0x004CF7B0) returns 0x06 "No" /
        // 0x1E "Only Where Obvious" / 0x05 "Yes" for 0/1/2, swapping to the
        // 0x20/0x21/0x1F "(Recommended)" variants for the ONE state that equals the
        // value from vtable slot +0x48 ([0x009CC0F4]). The order was already right;
        // the labels were not - the suffix was baked onto two states at once, and
        // retail never draws more than one suffixed state.
        RetailOptionsRow.Recommended(
            "Enable 32 bit textures:",
            ["No", "Only Where Obvious", "Yes"],
            ["No (Recommended)", "Only Where Obvious (Recommended)", "Yes (Recommended)"],
            host.RecommendedEnable32BitTextures,
            RetailOptionsApplyTiming.OnApply,
            s => s.Enable32BitTextures < 0 ? host.RecommendedEnable32BitTextures : s.Enable32BitTextures,
            static (s, i) => s.Enable32BitTextures = i),

        RetailOptionsRow.Drop(
            "Full-screen anti-aliasing level:",
            host.AntiAliasingLevels,
            RetailOptionsApplyTiming.OnApply,
            null,
            null),

        // PauseMenu.cpp:1436. GetText resolves Localization 0 = "Apply".
        RetailOptionsRow.Act("Apply", RetailOptionsAction.Apply),
        RetailOptionsRow.Act("Back", RetailOptionsAction.Back),
    ];

    private static List<RetailOptionsRow> BuildSound(RetailOptionsHostCapabilities host) =>
    [
        // PauseMenu.cpp:1450/1451 - CScaleMenuItem, max_value 10,
        // notify_on_change 1. Defaults 0.8/0.9 from Career.cpp:173-174.
        RetailOptionsRow.Bar(
            "Sound Volume",
            VolumeMaxValue,
            static s => VolumeIndex(s.SoundVolume),
            static (s, i) => s.SoundVolume = VolumeValue(i)),
        RetailOptionsRow.Bar(
            "Music Volume",
            VolumeMaxValue,
            static s => VolumeIndex(s.MusicVolume),
            static (s, i) => s.MusicVolume = VolumeValue(i)),

        // STATE ORDER READ, and it was INVERTED before. Both of these rows use the
        // shared CBOOLMenuItem label vfunc 0x004A4220, which returns Localization
        // 0x06 "No" for state 0 and 0x05 "Yes" for anything else. The displayed word
        // is unchanged because both flags are false by default, but the INDEX behind
        // it was wrong, and the index is what a value bar, a save and a deferred
        // commit all carry.
        RetailOptionsRow.Drop(
            "Swap left/right speakers:",
            ["No", "Yes"],
            RetailOptionsApplyTiming.OnRowClose,
            static s => s.SwapSpeakers ? 1 : 0,
            static (s, i) => s.SwapSpeakers = i != 0),

        RetailOptionsRow.Drop(
            "3D Sound hardware acceleration:",
            ["No", "Yes"],
            RetailOptionsApplyTiming.OnApply,
            static s => s.HardwareSound ? 1 : 0,
            static (s, i) => s.HardwareSound = i != 0),

        // STATE ORDER READ: C3DSoundQuality's +0x44 (0x004CF0F0) is
        // Localization(state + 0xD9) = 0xD9 High / 0xDA Medium / 0xDB Low.
        RetailOptionsRow.Drop(
            "Sound quality:",
            ["High (44 Khz, 16 bit)", "Medium (22 Khz, 16 bit)", "Low (11 Khz, 8 bit)"],
            RetailOptionsApplyTiming.OnApply,
            static s => s.SoundQuality,
            static (s, i) => s.SoundQuality = i),

        RetailOptionsRow.Drop(
            "Select Sound Device:",
            host.SoundDevices,
            RetailOptionsApplyTiming.OnApply,
            null,
            null),

        // STATE ORDER READ: C3DSoundMethod's +0x44 (0x004CF2A0) is
        // Localization(state + 0x0B) = 0x0B High ('Full HRTF') / 0x0C Medium /
        // 0x0D Low. The GET (0x004CF290) carries a -1 sentinel that falls back to the
        // device-preferred method at [0x00896A44], but the backing global
        // [0x00663084] is BSS and zero at load, not -1, so the sentinel is not the
        // shipped state - index 0 is.
        RetailOptionsRow.Drop(
            "3D Sound Quality:",
            ["High ('Full HRTF')", "Medium ('Light HRTF')", "Low (Left/Right panning)"],
            RetailOptionsApplyTiming.OnApply,
            static s => s.SoundMethod3D,
            static (s, i) => s.SoundMethod3D = i),

        RetailOptionsRow.Act("Apply", RetailOptionsAction.Apply),
        RetailOptionsRow.Act("Back", RetailOptionsAction.Back),
    ];
}

public enum RetailOptionsPage
{
    Root,
    Controller,
    Video,
    Sound,
}

public enum RetailOptionsRowKind
{
    /// <summary>Navigates to another options page.</summary>
    PageLink,

    /// <summary>Apply, Back, Credits.</summary>
    Action,

    /// <summary>A segmented bar with left/right arrows and no number drawn.</summary>
    ValueBar,

    /// <summary>Label:Value with an expanding state list.</summary>
    Dropdown,

    /// <summary>Drawn, never selectable - the four Joystick rows.</summary>
    Status,

    /// <summary>The 22-row, three-column key bindings sub-widget.</summary>
    Bindings,
}

/// <summary>
/// The THREE apply timings that coexist on this one menu. The fourth argument to
/// <c>CMenuItemDropdown__Init</c> is a <c>defer_commit</c> flag, and that reading
/// is cross-checked 19/19 against the constructor arguments in
/// <c>PauseMenu__Init</c>.
/// </summary>
public enum RetailOptionsApplyTiming
{
    /// <summary>Applies on every in-range keypress (<c>notify_on_change = 1</c>).</summary>
    Live,

    /// <summary>Commits when the row closes (<c>defer_commit == 0</c>).</summary>
    OnRowClose,

    /// <summary>Reaches the runtime only when the Apply row is selected.</summary>
    OnApply,
}

public enum RetailOptionsAction
{
    None,
    Apply,
    Back,
    Credits,
}

public enum RetailOptionsSignal
{
    None,
    PageChanged,
    ValueChanged,
    Applied,

    /// <summary>Back from the Options root - the caller leaves the page.</summary>
    Closed,
}

public sealed class RetailOptionsRow
{
    private RetailOptionsRow(
        RetailOptionsRowKind kind,
        string label,
        IReadOnlyList<string> states,
        RetailOptionsApplyTiming timing,
        float height)
    {
        Kind = kind;
        Label = label;
        States = states;
        Timing = timing;
        Height = height;
    }

    public RetailOptionsRowKind Kind { get; }

    public string Label { get; }

    public IReadOnlyList<string> States { get; }

    public RetailOptionsApplyTiming Timing { get; }

    public float Height { get; }

    public RetailOptionsPage TargetPage { get; private init; }

    public RetailOptionsAction Action { get; private init; } = RetailOptionsAction.None;

    /// <summary>Segment count on a value bar; the row has <c>MaxValue + 1</c> stops.</summary>
    public int MaxValue { get; private init; }

    public int CurrentIndex { get; internal set; }

    public int CommittedIndex { get; internal set; }

    internal Func<RetailOptionsSettings, int>? Read { get; private init; }

    internal Action<RetailOptionsSettings, int>? Write { get; private init; }

    /// <summary>
    /// The bindings grid is deliberately excluded: its own vtable
    /// (<c>0x005DB404</c>) has <c>ButtonPressed = RET 0xC</c> and
    /// <c>IsEnabled = xor eax,eax</c>, so the keyboard walk cannot land on it.
    /// Status rows are drawn disabled in the retail frame.
    /// </summary>
    public bool IsSelectable =>
        Kind is not (RetailOptionsRowKind.Bindings or RetailOptionsRowKind.Status);

    /// <summary>
    /// The index whose label carries the "(Recommended)" treatment, or -1 on a row
    /// that has no such treatment.
    ///
    /// Retail does NOT bind the suffix to a slot. Both rows that have one pick
    /// between two complete three-way label ladders on a single equality test -
    /// <c>CTextureRes</c> against <c>[0x009CC114]</c> (<c>0x004CF700</c>) and
    /// <c>CEnable32Bit</c> against its own vtable slot <c>+0x48</c>, which returns
    /// <c>[0x009CC0F4]</c> (<c>0x004CF7B0</c> / <c>0x004CF850</c>). So EXACTLY ONE
    /// state carries the suffix at any moment, and which one is a property of the
    /// adapter, not of the row.
    /// </summary>
    public int RecommendedIndex { get; private init; } = -1;

    /// <summary>The alternate ladder used for <see cref="RecommendedIndex"/>.</summary>
    public IReadOnlyList<string> RecommendedStates { get; private init; } = [];

    /// <summary>The label retail draws for <paramref name="index"/>.</summary>
    public string StateLabel(int index)
    {
        if (States.Count == 0)
        {
            return string.Empty;
        }
        // The else arms of both ladders test >= 2 rather than == 2, so an
        // out-of-range index renders as the LAST label instead of faulting. That is
        // what lets the detail-level presets write 4 and 5 into a four-state row.
        int clamped = Math.Clamp(index, 0, States.Count - 1);
        return clamped == RecommendedIndex && RecommendedStates.Count == States.Count
            ? RecommendedStates[clamped]
            : States[clamped];
    }

    /// <summary>The state text a dropdown draws to the right of its label.</summary>
    public string CurrentState => StateLabel(CurrentIndex);

    internal static RetailOptionsRow Link(string label, RetailOptionsPage target) =>
        new(RetailOptionsRowKind.PageLink, label, [], RetailOptionsApplyTiming.Live,
            RetailOptionsMenu.RowHeight)
        { TargetPage = target };

    internal static RetailOptionsRow Act(
        string label,
        RetailOptionsAction action,
        float height = RetailOptionsMenu.RowHeight) =>
        new(RetailOptionsRowKind.Action, label, [], RetailOptionsApplyTiming.Live, height)
        { Action = action };

    internal static RetailOptionsRow Status(string label, float height) =>
        new(RetailOptionsRowKind.Status, label, [], RetailOptionsApplyTiming.Live, height);

    internal static RetailOptionsRow Bindings() =>
        new(RetailOptionsRowKind.Bindings, string.Empty, [], RetailOptionsApplyTiming.Live,
            RetailOptionsMenu.BindingsRowHeight);

    internal static RetailOptionsRow Bar(
        string label,
        int maxValue,
        Func<RetailOptionsSettings, int> read,
        Action<RetailOptionsSettings, int> write) =>
        new(RetailOptionsRowKind.ValueBar, label, [], RetailOptionsApplyTiming.Live,
            RetailOptionsMenu.RowHeight)
        { MaxValue = maxValue, Read = read, Write = write };

    internal static RetailOptionsRow Drop(
        string label,
        IReadOnlyList<string> states,
        RetailOptionsApplyTiming timing,
        Func<RetailOptionsSettings, int>? read,
        Action<RetailOptionsSettings, int>? write) =>
        new(RetailOptionsRowKind.Dropdown, label, states, timing, RetailOptionsMenu.RowHeight)
        { Read = read, Write = write };

    /// <summary>A dropdown with the two-ladder "(Recommended)" treatment.</summary>
    internal static RetailOptionsRow Recommended(
        string label,
        IReadOnlyList<string> states,
        IReadOnlyList<string> recommendedStates,
        int recommendedIndex,
        RetailOptionsApplyTiming timing,
        Func<RetailOptionsSettings, int>? read,
        Action<RetailOptionsSettings, int>? write) =>
        new(RetailOptionsRowKind.Dropdown, label, states, timing, RetailOptionsMenu.RowHeight)
        {
            Read = read,
            Write = write,
            RecommendedIndex = recommendedIndex,
            RecommendedStates = recommendedStates,
        };
}

/// <summary>
/// Rows whose retail state list is ENUMERATED FROM THE DEVICE rather than fixed.
/// Supplying them from outside keeps <see cref="RetailOptionsMenu"/> free of any
/// process, GPU or audio dependency, and keeps its tests reproducible.
/// </summary>
public sealed record RetailOptionsHostCapabilities(
    IReadOnlyList<string> ScreenModes,
    IReadOnlyList<string> VideoAdapters,
    IReadOnlyList<string> AntiAliasingLevels,
    IReadOnlyList<string> SoundDevices,
    int RecommendedTextureResolution = 0,
    int RecommendedEnable32BitTextures = 2)
{
    /// <summary>
    /// What retail draws when the enumeration is empty: Localization <c>0xD4</c>
    /// "None" for FSAA and <c>0xD7</c> "No sound device available".
    ///
    /// <para><b>The two recommended indices are the one genuinely open value on this
    /// page.</b></para>
    /// Retail's authored default for both <c>Texture Resolution:</c> and
    /// <c>Enable 32 bit textures:</c> is the CVar registration immediate <c>-1</c>
    /// (<c>USER_TEXTURE_RES_LOSS_SHIFT</c> at <c>0x00556C30</c>,
    /// <c>USER_TEXTURE_ALLOW_32_BIT</c> at <c>0x00556C90</c>), which both getters
    /// read as "unset" and resolve to a PER-ADAPTER recommendation
    /// (<c>[0x009CC114]</c> / <c>[0x009CC0F4]</c>). So the READ default is that
    /// resolution rule, not a constant - but the function retail uses to compute the
    /// recommendation from D3D caps was NOT recovered, and this renderer has no D3D
    /// caps path to feed it. 0 (High) and 2 (Yes) are what a capable adapter gets and
    /// are what the retail capture shows; they are host values, not authored ones,
    /// and they are the last unresolved number on this page.
    /// </summary>
    public static RetailOptionsHostCapabilities Unknown { get; } = new(
        ["640 x 480"],
        ["Unknown"],
        ["None"],
        ["No sound device available"]);
}

/// <summary>
/// The values behind the rows.
///
/// Only <see cref="SoundVolume"/>, <see cref="MusicVolume"/>,
/// <see cref="MouseSensitivity"/> and <see cref="VSync"/> currently have a
/// consumer. The rest are presented, remembered and drawn exactly as retail draws
/// them, but no renderer or audio path reads them - that is an ownership boundary,
/// not a claim that the row does nothing in retail.
/// </summary>
public sealed class RetailOptionsSettings
{
    /// <summary>
    /// <c>references/Onslaught/Career.cpp:173</c> supplies the authored raw career
    /// value. Released <c>CSoundManager::SetMasterVolume</c> at <c>0x004E04C0</c>
    /// stores that supplied float directly; the retained curve is not applied.
    /// </summary>
    public float SoundVolume { get; set; } = 0.8f;

    /// <summary><c>references/Onslaught/Career.cpp:174</c>.</summary>
    public float MusicVolume { get; set; } = 0.9f;

    /// <summary>
    /// Image static initialiser at VA <c>0x006254F4</c>. It is deliberately NOT
    /// the value the bar's own index maps back to - see the implementation note in
    /// the Godot options page.
    /// </summary>
    public float MouseSensitivity { get; set; } = RetailOptionsMenu.DefaultMouseSensitivity;

    /// <summary><c>mControllerConfigurationNum</c>, <c>Career.cpp:175</c>.</summary>
    public int ControllerConfiguration { get; set; } = 1;

    /// <summary><c>mInvertYAxis</c> per player, <c>Career.cpp:176</c>, FALSE.</summary>
    public bool InvertYWalkerPlayer1 { get; set; }

    public bool InvertYWalkerPlayer2 { get; set; }

    public bool InvertYFlightPlayer1 { get; set; }

    public bool InvertYFlightPlayer2 { get; set; }

    /// <summary>
    /// Custom. READ 2026-07-27, was inferred. <c>CVideoDetailLevel</c>'s GET
    /// (<c>0x004CFFD0</c>, running to <c>0x004D0199</c>) is three sequential
    /// nine-field tuple compares against the Lowest / Medium / High presets, and the
    /// sole fall-through for all three arms is
    /// <c>0x004D0196: xor eax,eax; ret</c> - Custom. Every arm requires Shadow and
    /// Geometry to be EQUAL (0/0, 1/1, 2/2), and the shipped pair is Shadow 0 with
    /// Geometry 1, so all three arms fail on that pair alone. The verdict does not
    /// depend on any other child row's default.
    /// </summary>
    public int OverallDetail { get; set; }

    /// <summary>
    /// Low. READ 2026-07-27, was a GUESS of Medium.
    ///
    /// <c>CShadowDetail</c>'s SET (<c>0x004CEEF0</c>) writes the byte pair
    /// <c>[0x009C7C54]</c>,<c>[0x009C7C56]</c> as 0 -> (0,0), 1 -> (0,1), 2 -> (1,1),
    /// and its GET (<c>0x004CEF30</c>) inverts that exactly. Both bytes are past
    /// <c>.data</c>'s raw end (VA <c>0x00661000</c>), so they are BSS and ZERO at
    /// load, which reads back as index 0.
    ///
    /// Graded CORROBORATED rather than PROVEN, and the reason is named rather than
    /// hidden: <c>CDXLandscape::Reset</c> (<c>0x00545070</c>) also writes
    /// <c>[0x009C7C54]</c>, on both of its arms, so the High byte is partly
    /// engine-owned once a level has loaded. Medium is nevertheless positively
    /// EXCLUDED - <c>[0x009C7C56]</c> has no writer image-wide other than this row's
    /// own SET and <c>OptionsTail_Read</c>, and that pair is what distinguishes
    /// Medium from Low. On the cold frontend this page is reached from, no landscape
    /// has been reset.
    ///
    /// Specimen for every address here:
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
    /// sha256 <c>74154bfa...</c>. File offset is VA - 0x400000 uniformly.
    /// </summary>
    public int ShadowDetail { get; set; }

    /// <summary>
    /// Medium. The image's static initialisers (distance 30.0, LOD bias 1.0,
    /// scale 1.0) are exactly state 1 of the setter's three-arm table. Independently:
    /// <c>CTreeDetail</c>'s GET tail-calls <c>0x004DD770</c>, which classifies by
    /// threshold - below <c>[0x005D85D4]</c> = 15.0 gives 0, below
    /// <c>[0x005D8610]</c> = 40.0 gives 1 - and the shipped 30.0 sits mid-band of 1.
    /// </summary>
    public int GeometryDetail { get; set; } = 1;

    /// <summary>
    /// Yes. READ 2026-07-27, was a GUESS that happened to be right.
    ///
    /// The CVar behind this row is registered at <c>0x004CDE30</c> -
    /// <c>push 0; push 0x00631198; mov ecx,0x0082B468; call 0x00528AA0</c> - so its
    /// authored default is <b>0</b> and its name is
    /// <c>RENDERSTATE_DISALLOW_MIPMAPPING</c>. That name settles the inversion the
    /// recovery note flagged: the CVar is the NEGATION of the row. The GET
    /// (<c>0x004CF6F0</c>) is literally <c>(cvar == 0) ? 1 : 0</c>, so cvar 0 is
    /// index 1, "Yes". The consumer agrees - <c>0x00551420</c> selects
    /// <c>D3DSAMP_MIPFILTER = LINEAR</c> when the cvar is 0.
    /// </summary>
    public bool TrilinearMipmapping { get; set; } = true;

    /// <summary>
    /// No. READ 2026-07-27, was a GUESS of Yes - and it is the one video row this
    /// client actually consumes, so the guess was shipping as behaviour.
    ///
    /// <c>[0x0066306C]</c> is BSS (past VA <c>0x00661000</c>) and zero at load. Its
    /// only two writers image-wide are this row's SET (<c>0x004CF50A</c>) and
    /// <c>OptionsTail_Read</c> (<c>0x00420EA4</c>) - and <c>defaultoptions.bea</c> is
    /// not shipped (0 matches for <c>.bea</c> in <c>INSTALL.LOG</c>'s 5,773 lines),
    /// so on a fresh install nothing writes it at all. The consumer confirms the
    /// sense: <c>0x0052B15E</c> picks <c>D3DPRESENT_INTERVAL_IMMEDIATE</c>
    /// (<c>0x80000000</c>) when the global is zero and <c>INTERVAL_ONE</c> when it is
    /// not. Retail ships with vsync OFF.
    /// </summary>
    public bool VSync { get; set; }

    /// <summary>
    /// High. READ 2026-07-27, was a GUESS that happened to be right.
    ///
    /// CVar <c>LANDSCAPE_MAXLEVELS_USER</c> on object <c>0x008AA950</c> is registered
    /// at <c>0x00544660</c> with <c>push 5</c>, so its authored value is 5. The GET
    /// (<c>0x004CF690</c>) maps cvar 1 -> 0, 3 -> 1, 4 -> 2, anything else -> 3, and
    /// the SET's arms write 1.0 / 3.0 / 4.0 / 5.0 in that order. 5 is index 3.
    /// </summary>
    public int LandscapeResolution { get; set; } = 3;

    /// <summary>
    /// <b>-1 means "unset, resolve to the adapter's recommendation"</b>, which is the
    /// authored value, not a constant index. READ 2026-07-27, replacing a value taken
    /// off the captured machine's frame.
    ///
    /// The CVar is registered at <c>0x00556C30</c> as
    /// <c>push -1; push 0x00652610; mov ecx,0x009CC0F8</c> - name
    /// <c>USER_TEXTURE_RES_LOSS_SHIFT</c>, authored value <c>-1</c> - and the GET
    /// (<c>0x004CF7A0</c>) returns <c>[0x009CC104]</c> unless it is <c>&lt;= -1</c>,
    /// in which case it returns the recommendation at <c>[0x009CC114]</c>.
    /// </summary>
    public int TextureResolution { get; set; } = -1;

    /// <summary>
    /// <b>-1, the same "unset" sentinel.</b> CVar <c>USER_TEXTURE_ALLOW_32_BIT</c> on
    /// object <c>0x009CC0D8</c> is registered at <c>0x00556C90</c> with
    /// <c>push -1</c>; the GET (<c>0x004CF860</c>) asks the CVar whether it was ever
    /// set and returns <c>[0x009CC0F4]</c>, the recommendation, when it was not.
    /// </summary>
    public int Enable32BitTextures { get; set; } = -1;

    /// <summary>
    /// No (index 0). <c>[0x00663070]</c> is BSS and zero at load; its only writers are
    /// this row's SET and <c>OptionsTail_Read</c>.
    /// </summary>
    public bool SwapSpeakers { get; set; }

    /// <summary>
    /// No (index 0). <c>[0x00663074]</c> is BSS and zero at load, same writer set.
    /// </summary>
    public bool HardwareSound { get; set; }

    /// <summary>
    /// High (44 Khz, 16 bit). <c>[0x00663080]</c> is BSS and zero at load, and index 0
    /// is High. That the retained Level 100 PCM is decoded at 44.1 kHz is a
    /// coincidence worth noting, not the evidence.
    /// </summary>
    public int SoundQuality { get; set; }

    /// <summary>
    /// High ('Full HRTF'). READ 2026-07-27, was a GUESS of Low justified by what this
    /// client does rather than by what retail ships.
    ///
    /// <c>[0x00663084]</c> is BSS and zero at load. The GET (<c>0x004CF290</c>) has a
    /// <c>-1</c> sentinel that would fall back to the device-preferred method at
    /// <c>[0x00896A44]</c>, but zero is not <c>-1</c>, so the sentinel is not the
    /// shipped state. The only writers of <c>[0x00663084]</c> are this row's SET and
    /// <c>OptionsTail_Read</c>; the sound system at <c>0x00516C38</c> only READS it.
    ///
    /// This row has no consumer in this lane - it is presented and remembered. Under
    /// GOAL.md's defaults rule the page reports retail's authored option state, not
    /// this renderer's capability, so it now says High.
    /// </summary>
    public int SoundMethod3D { get; set; }
}
