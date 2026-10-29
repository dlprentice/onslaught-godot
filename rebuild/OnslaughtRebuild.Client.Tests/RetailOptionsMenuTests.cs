// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Each test here pins a recovered law or a measured retail constant. None of
/// them assert that a property setter round-trips.
/// </summary>
public sealed class RetailOptionsMenuTests
{
    /// <summary>
    /// The mouse-sensitivity lattice. Setter <c>0x004CEFE0</c> is
    /// <c>g_MouseSensitivity = (index + 1) * 3.0f</c> with <c>max_value = 0x14</c>
    /// and an INCLUSIVE clamp in <c>CMenuItem__ButtonPressed</c>, so the row has
    /// 21 stops and the selectable range floors at 3.0 - which is why the
    /// reconstruction's old hard-coded 1.5 was not a retail value at all.
    /// </summary>
    [Fact]
    public void MouseSensitivityLatticeIsTwentyOneStopsFromThreeToSixtyThree()
    {
        Assert.Equal(3f, RetailOptionsMenu.MouseSensitivityValue(0));
        Assert.Equal(63f, RetailOptionsMenu.MouseSensitivityValue(20));
        for (int index = 0; index <= RetailOptionsMenu.MouseSensitivityMaxValue; index++)
        {
            Assert.Equal((index + 1) * 3f, RetailOptionsMenu.MouseSensitivityValue(index));
        }

        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Controller);
        RetailOptionsRow row = menu.Rows[0];
        Assert.Equal(RetailOptionsRowKind.ValueBar, row.Kind);
        Assert.Equal(20, row.MaxValue);

        // Walk hard against both ends: 21 stops, and the clamped presses report
        // no change (retail plays no sound on a clamped press).
        for (int i = 0; i < 64; i++)
        {
            menu.Adjust(1);
        }
        Assert.Equal(20, row.CurrentIndex);
        Assert.False(menu.Adjust(1));
        Assert.Equal(63f, menu.Settings.MouseSensitivity);

        for (int i = 0; i < 64; i++)
        {
            menu.Adjust(-1);
        }
        Assert.Equal(0, row.CurrentIndex);
        Assert.False(menu.Adjust(-1));
        Assert.Equal(3f, menu.Settings.MouseSensitivity);
    }

    /// <summary>
    /// <c>index = ROUND(sensitivity/3 + 0.5) - 1</c> is NOT the inverse of
    /// <c>(index + 1) * 3</c>, and the asymmetry is the point.
    ///
    /// Under round-to-nearest the map is idempotent only on ODD indices; an even
    /// index reads back one stop higher. That is the mechanism behind the
    /// recovery note's warning that the stored value can walk, and it is
    /// corroborated by the retail Controller Options frame, whose bar shows 3
    /// filled segments - index 3, value 12.0 - which is exactly the fixed point
    /// the shipped 7.0 converges to (7 -> 9 -> 12 -> 12).
    ///
    /// This reconstruction deliberately does NOT reproduce the walk: retail
    /// re-invokes the setter every frame from the committed index for any
    /// unselected value row, which is what advances it, and doing that here would
    /// silently move the untouched default off 7.0. Read this test as pinning the
    /// index expression, not as a claim that the walk is desired.
    /// </summary>
    [Fact]
    public void MouseSensitivityIndexIsIdempotentOnlyOnOddIndices()
    {
        Assert.Equal(2, RetailOptionsMenu.MouseSensitivityIndex(7f));
        Assert.Equal(3, RetailOptionsMenu.MouseSensitivityIndex(9f));
        Assert.Equal(3, RetailOptionsMenu.MouseSensitivityIndex(12f));

        for (int index = 0; index <= RetailOptionsMenu.MouseSensitivityMaxValue; index++)
        {
            int readBack = RetailOptionsMenu.MouseSensitivityIndex(
                RetailOptionsMenu.MouseSensitivityValue(index));
            int expected = index % 2 == 1
                ? index
                : Math.Min(index + 1, RetailOptionsMenu.MouseSensitivityMaxValue);
            Assert.Equal(expected, readBack);
        }

        // Both ends stay inside the row.
        Assert.Equal(20, RetailOptionsMenu.MouseSensitivityIndex(63f));
        Assert.Equal(20, RetailOptionsMenu.MouseSensitivityIndex(1000f));
        Assert.Equal(0, RetailOptionsMenu.MouseSensitivityIndex(0.1f));
    }

    /// <summary>
    /// The untouched slider must sit on the image's own static initialiser, 7.0
    /// at VA <c>0x006254F4</c>, because the consumer's axis scale is
    /// <c>sensitivity * 13/3000</c> and 7.0 is what makes that the 91/3000 the
    /// session already carried. The behavioural half of this guard lives in
    /// <c>InteractiveSessionTests.PointerMotion_SensitivitySliderScalesTheAxisAndSevenIsTheDefault</c>.
    /// </summary>
    [Fact]
    public void UntouchedSliderSitsOnTheImageDefaultOfSeven()
    {
        Assert.Equal(7f, new RetailOptionsMenu().Settings.MouseSensitivity);
        Assert.Equal(7f, RetailOptionsMenu.DefaultMouseSensitivity);

        // 7.0 is off the lattice, so it is NOT a value the slider can produce -
        // which is why entering the page must not be allowed to rewrite it.
        Assert.DoesNotContain(
            Enumerable.Range(0, RetailOptionsMenu.MouseSensitivityMaxValue + 1)
                .Select(RetailOptionsMenu.MouseSensitivityValue),
            value => value == 7f);
    }

    /// <summary>
    /// <c>ROUND(max_value * scale + 0.48)</c>, the 0.48 being the float32 at
    /// <c>0x005DC560</c>. Corroborated by pixels: the retail Sound Options frame
    /// has exactly 8 filled segments on Sound Volume and 9 on Music Volume, and
    /// the shipped defaults are 0.8 and 0.9 (<c>Career.cpp:173-174</c>).
    /// </summary>
    [Fact]
    public void VolumeRowsSeedFromTheShippedDefaultsAsTheRetailFrameShowsThem()
    {
        Assert.Equal(8, RetailOptionsMenu.VolumeIndex(0.8f));
        Assert.Equal(9, RetailOptionsMenu.VolumeIndex(0.9f));

        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Sound);
        Assert.Equal("Sound Volume", menu.Rows[0].Label);
        Assert.Equal("Music Volume", menu.Rows[1].Label);
        Assert.Equal(8, menu.Rows[0].CurrentIndex);
        Assert.Equal(9, menu.Rows[1].CurrentIndex);
        Assert.Equal(10, menu.Rows[0].MaxValue);
    }

    /// <summary>
    /// The three apply timings that coexist on one menu. This is the load-bearing
    /// behavioural finding of the byte recovery, cross-checked 19/19 against the
    /// dropdown constructor arguments in <c>PauseMenu__Init</c>.
    /// </summary>
    [Fact]
    public void ThreeApplyTimingsCoexistOnOneMenu()
    {
        // (a) LIVE - a value bar reaches the setting on the keypress itself.
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Sound);
        menu.Adjust(-1);
        Assert.Equal(0.7f, menu.Settings.SoundVolume, 5);

        // (c) ON APPLY - a deferred dropdown shows its pending state but has NOT
        // reached the setting.
        menu.Enter(RetailOptionsPage.Sound);
        int soundQuality = IndexOfLabel(menu, "Sound quality:");
        Assert.Equal(RetailOptionsApplyTiming.OnApply, menu.Rows[soundQuality].Timing);
        SelectRow(menu, soundQuality);
        menu.Adjust(1);
        menu.Confirm();
        Assert.Equal(1, menu.Rows[soundQuality].CurrentIndex);
        Assert.Equal(0, menu.Settings.SoundQuality);
        Assert.True(menu.HasPendingChanges);

        int apply = IndexOfLabel(menu, "Apply");
        SelectRow(menu, apply);
        Assert.Equal(RetailOptionsSignal.Applied, menu.Confirm());
        Assert.Equal(1, menu.Settings.SoundQuality);
        Assert.False(menu.HasPendingChanges);

        // (b) ON ROW CLOSE - a non-deferred dropdown commits when the row closes,
        // with no Apply involved.
        //
        // This steps UP, not down, since the state order was corrected on
        // 2026-07-27: every Yes/No row goes through the shared CBOOLMenuItem label
        // vfunc 0x004A4220, whose state 0 is Localization 0x06 "No". The shipped
        // flag is false, so the row starts on index 0 and a downward press is a
        // no-op clamp.
        int swap = IndexOfLabel(menu, "Swap left/right speakers:");
        Assert.Equal(RetailOptionsApplyTiming.OnRowClose, menu.Rows[swap].Timing);
        Assert.Equal(0, menu.Rows[swap].CurrentIndex);
        SelectRow(menu, swap);
        menu.Adjust(1);
        Assert.False(menu.Settings.SwapSpeakers);
        menu.Confirm();
        Assert.True(menu.Settings.SwapSpeakers);
    }

    /// <summary>
    /// Back never reverts a deferred value and never prompts; the value is
    /// discarded instead by the page's own re-sync on the NEXT entry, which is
    /// the <c>ResetIterator</c> + slot <c>+0x3C</c> GET walk.
    /// </summary>
    [Fact]
    public void BackDiscardsAPendingDeferredValueByResyncingOnReEntry()
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Sound);
        int soundQuality = IndexOfLabel(menu, "Sound quality:");
        SelectRow(menu, soundQuality);
        menu.Adjust(1);
        menu.Confirm();
        Assert.Equal(1, menu.Rows[soundQuality].CurrentIndex);

        Assert.Equal(RetailOptionsSignal.PageChanged, menu.Back());
        Assert.Equal(RetailOptionsPage.Root, menu.Page);

        menu.Enter(RetailOptionsPage.Sound);
        Assert.Equal(0, menu.Rows[soundQuality].CurrentIndex);
        Assert.Equal(0, menu.Settings.SoundQuality);
        Assert.False(menu.HasPendingChanges);
    }

    /// <summary>
    /// The range-centring law, against the four measured first-row cell tops on
    /// the retail frames. These are the numbers that place every row on every
    /// page; if this drifts, all four pages move.
    /// </summary>
    [Theory]
    [InlineData(RetailOptionsPage.Root, 245f)]
    [InlineData(RetailOptionsPage.Sound, 195f)]
    [InlineData(RetailOptionsPage.Video, 145f)]
    [InlineData(RetailOptionsPage.Controller, 106f)]
    public void FirstRowTopMatchesTheRetailFrame(RetailOptionsPage page, float expected)
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(page);
        Assert.Equal(expected, menu.FirstRowTop);
    }

    /// <summary>
    /// The Video page has 14 rows and NO "Screen shape:" row. That row exists in
    /// the executable (<c>PauseMenu.cpp:1427</c>, <c>CScreenShape</c>) but is
    /// gated on <c>0x0089C0AC</c> (ALLOW_WIDESCREEN_MODES), and the retail
    /// frame's 14 rows show the shipped gate is off. Re-adding it from the byte
    /// inventory alone would push every row on the page 10px up.
    /// </summary>
    [Fact]
    public void ShippedVideoPageHasFourteenRowsAndNoScreenShapeRow()
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Video);
        Assert.Equal(14, menu.Rows.Count);
        Assert.DoesNotContain(menu.Rows, row => row.Label.StartsWith("Screen shape", StringComparison.Ordinal));
        Assert.Equal("Extra graphical features:", menu.Rows[0].Label);
        Assert.Equal("Apply", menu.Rows[12].Label);
        Assert.Equal("Back", menu.Rows[13].Label);
    }

    /// <summary>
    /// The bindings grid and the Joystick status rows are drawn but cannot be
    /// selected: the grid's own vtable has <c>IsEnabled</c> = a zeroing of eax and
    /// <c>ButtonPressed</c> = a return that pops 12 bytes of arguments, and the retail frame draws the Joystick
    /// rows in the disabled colour. A selection walk that stopped on either would
    /// strand the player.
    /// </summary>
    [Fact]
    public void SelectionWalkSkipsTheBindingsGridAndTheJoystickRows()
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Controller);
        var visited = new List<string>();
        for (int step = 0; step < menu.Rows.Count; step++)
        {
            visited.Add(menu.SelectedRow.Label);
            menu.MoveSelection(1);
        }

        Assert.Equal(
            ["Mouse sensitivity:", "Configuration:", "Back"],
            visited.Distinct());
    }

    /// <summary>
    /// Retail's rendered invert-Y word is the OPPOSITE of the effect: the render
    /// path draws "Off" when the flag is non-zero, while
    /// <c>references/Onslaught/Player.cpp:325-332</c> negates pitch when it is
    /// TRUE. The shipped default is FALSE (<c>Career.cpp:176</c>), so an untouched
    /// install draws "On". Reproduce it; do not fix it.
    /// </summary>
    [Fact]
    public void InvertYRendersTheOppositeWordToItsEffect()
    {
        Assert.Equal("On", RetailControlBindings.InvertYLabel(inverted: false));
        Assert.Equal("Off", RetailControlBindings.InvertYLabel(inverted: true));
        Assert.False(new RetailOptionsMenu().Settings.InvertYWalkerPlayer1);
    }

    /// <summary>
    /// 22 grid rows over action codes 0x37-0x4C, four of which are blank. Both
    /// numbers come from <c>ControlsUI__RenderBindingsList</c>
    /// (<c>action_code = rowIndex + 0x37</c>, loop gate <c>rowIndex &lt; 0x16</c>),
    /// and the retail frame carries exactly 18 non-blank lines.
    /// </summary>
    [Fact]
    public void BindingsGridIsTwentyTwoRowsWithEighteenDrawn()
    {
        Assert.Equal(RetailControlBindings.RowCount, RetailControlBindings.Rows.Count);
        Assert.Equal(0x37, RetailControlBindings.Rows[0].ActionCode);
        Assert.Equal(0x4C, RetailControlBindings.Rows[^1].ActionCode);
        for (int i = 0; i < RetailControlBindings.Rows.Count; i++)
        {
            Assert.Equal(0x37 + i, RetailControlBindings.Rows[i].ActionCode);
        }

        Assert.Equal(
            18,
            RetailControlBindings.Rows.Count(
                row => row.Kind != RetailControlBindingRowKind.Spacer));
    }

    /// <summary>
    /// The index -> label order of every fixed-state dropdown, recovered on
    /// 2026-07-27 from each row's <c>+0x44</c> label vfunc in the pristine specimen
    /// a private pristine copy (sha256
    /// <c>74154bfa...</c>). Before that, these orders were INFERRED from Localization
    /// id adjacency, and two of them were wrong.
    ///
    /// The trap this pins: <c>Landscape resolution:</c>'s ids run DOWNWARDS
    /// (<c>0x25</c> Lowest -> <c>0x22</c> High) as the index runs up, while
    /// <c>Texture Resolution:</c>'s run downwards too (<c>0x1A</c> High -> <c>0x1C</c>
    /// Low). Ordering either by id adjacency gives the reverse of the truth, and that
    /// is exactly the error that shipped in Texture Resolution.
    /// </summary>
    [Theory]
    // Localization(state + 0xE0), vfunc 0x004D01A0.
    [InlineData(RetailOptionsPage.Video, "Overall Detail level:", "Custom|Lowest|Medium|High")]
    // Localization(state + 0x10), vfunc 0x004CEED0, shared with Geometry detail.
    [InlineData(RetailOptionsPage.Video, "Shadow detail:", "Low|Medium|High")]
    [InlineData(RetailOptionsPage.Video, "Geometry detail:", "Low|Medium|High")]
    // Explicit ladder 0x25/0x24/0x23/0x22, vfunc 0x004CF590.
    [InlineData(RetailOptionsPage.Video, "Landscape resolution:", "Lowest|Low|Medium|High")]
    // Explicit ladder 0x1A/0x1B/0x1C, vfunc 0x004CF700.
    [InlineData(RetailOptionsPage.Video, "Texture Resolution:", "High|Medium|Low")]
    // Explicit ladder 0x06/0x1E/0x05, vfunc 0x004CF7B0.
    [InlineData(RetailOptionsPage.Video, "Enable 32 bit textures:", "No|Only Where Obvious|Yes")]
    // Shared CBOOLMenuItem vfunc 0x004A4220: 0x06 "No" for state 0, else 0x05 "Yes".
    [InlineData(RetailOptionsPage.Video, "Trilinear mipmapping:", "No|Yes")]
    [InlineData(RetailOptionsPage.Video, "VSync:", "No|Yes")]
    [InlineData(RetailOptionsPage.Sound, "Swap left/right speakers:", "No|Yes")]
    [InlineData(RetailOptionsPage.Sound, "3D Sound hardware acceleration:", "No|Yes")]
    // Localization(state + 0xD9), vfunc 0x004CF0F0.
    [InlineData(
        RetailOptionsPage.Sound,
        "Sound quality:",
        "High (44 Khz, 16 bit)|Medium (22 Khz, 16 bit)|Low (11 Khz, 8 bit)")]
    // Localization(state + 0x0B), vfunc 0x004CF2A0.
    [InlineData(
        RetailOptionsPage.Sound,
        "3D Sound Quality:",
        "High ('Full HRTF')|Medium ('Light HRTF')|Low (Left/Right panning)")]
    public void DropdownStateOrderMatchesItsLabelVfunc(
        RetailOptionsPage page,
        string label,
        string expected)
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(page);
        RetailOptionsRow row = menu.Rows[IndexOfLabel(menu, label)];
        Assert.Equal(expected, string.Join('|', row.States));
    }

    /// <summary>
    /// The out-of-box state of every row whose authored default was recovered from
    /// the image rather than read off a machine.
    ///
    /// This is GOAL.md's defaults rule applied to the whole page. Retail ships no
    /// <c>defaultoptions.bea</c> - <c>INSTALL.LOG</c>'s 5,773 lines contain no
    /// <c>.bea</c> at all - so every fresh install is a virgin install and the
    /// authored default is whatever the image itself holds: a <c>.data</c>
    /// initialiser, a CVar registration immediate, or BSS zero.
    ///
    /// Three of these changed on 2026-07-27 and had been guesses:
    /// Shadow detail Medium -> Low, VSync Yes -> No, 3D Sound Quality Low -> High.
    /// </summary>
    [Fact]
    public void AuthoredDefaultsComeFromTheImageAndNotFromAnyMachine()
    {
        var settings = new RetailOptionsSettings();

        // BSS byte pair [0x009C7C54],[0x009C7C56] zero at load; GET 0x004CEF30.
        Assert.Equal(0, settings.ShadowDetail);

        // .data 30.0 at 0x006321A0, mid-band of the 15.0/40.0 classifier 0x004DD770.
        Assert.Equal(1, settings.GeometryDetail);

        // CVar RENDERSTATE_DISALLOW_MIPMAPPING registered 0 at 0x004CDE30; the row
        // is its negation, so 0 is "Yes".
        Assert.True(settings.TrilinearMipmapping);

        // [0x0066306C] BSS zero; consumer 0x0052B15E picks INTERVAL_IMMEDIATE.
        Assert.False(settings.VSync);

        // CVar LANDSCAPE_MAXLEVELS_USER registered 5 at 0x00544660; GET maps 5 -> 3.
        Assert.Equal(3, settings.LandscapeResolution);

        // Both of these are the CVar registration immediate -1, meaning "unset,
        // resolve to the adapter's recommendation" - a rule, not an index.
        Assert.Equal(-1, settings.TextureResolution);
        Assert.Equal(-1, settings.Enable32BitTextures);

        // [0x00663070] / [0x00663074] / [0x00663080] / [0x00663084], all BSS zero.
        Assert.False(settings.SwapSpeakers);
        Assert.False(settings.HardwareSound);
        Assert.Equal(0, settings.SoundQuality);
        Assert.Equal(0, settings.SoundMethod3D);

        // CVideoDetailLevel's GET 0x004CFFD0 falls through all three preset compares
        // to a zeroing of eax, then return. Every arm needs Shadow == Geometry, and the shipped
        // pair is 0 and 1, so no arm can match.
        Assert.Equal(0, settings.OverallDetail);
        Assert.NotEqual(settings.ShadowDetail, settings.GeometryDetail);
    }

    /// <summary>
    /// Retail never draws more than ONE "(Recommended)" state on a row. Both vfuncs
    /// that have the treatment (<c>0x004CF700</c>, <c>0x004CF7B0</c>) are a single
    /// equality against one per-adapter global selecting between two COMPLETE
    /// three-way ladders, so the suffix follows the recommendation and cannot sit on
    /// two states at once.
    ///
    /// The list this replaced was
    /// <c>["No (Recommended)", "Only Where Obvious", "Yes (Recommended)"]</c>, which
    /// carried two suffixes and omitted Localization <c>0x21</c>
    /// "Only Where Obvious (Recommended)" entirely - so it could not render correctly
    /// on any machine, and not at all on one whose recommendation is index 1.
    /// </summary>
    [Theory]
    [InlineData("Texture Resolution:")]
    [InlineData("Enable 32 bit textures:")]
    public void ExactlyOneStateCarriesTheRecommendedTreatment(string label)
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Video);
        RetailOptionsRow row = menu.Rows[IndexOfLabel(menu, label)];

        int suffixed = 0;
        for (int i = 0; i < row.States.Count; i++)
        {
            if (row.StateLabel(i).EndsWith("(Recommended)", StringComparison.Ordinal))
            {
                suffixed++;
                Assert.Equal(row.RecommendedIndex, i);
            }
        }

        Assert.Equal(1, suffixed);
        Assert.Equal(row.States.Count, row.RecommendedStates.Count);
    }

    /// <summary>
    /// The <c>Configuration:</c> row's index IS the preset scheme integer, with no
    /// remap in either direction - <c>CConfigName</c>'s SET (<c>0x004D01D0</c>)
    /// passes it straight to <c>Controls__ApplyPreset</c> (<c>0x00453780</c>), whose
    /// first act is a store of ebp to the value at 0x00677D70, and its GET (<c>0x004D01E0</c>)
    /// reads that same global back. That is what was missing when this mapping was
    /// a guess.
    ///
    /// Scheme 0 is a pure marker: a zero test of ebp, branching when equal returns before touching a
    /// single binding, which is what "Custom" means. Scheme 1 stamps the one preset
    /// block that exists in the shipped data. The row has two states because
    /// <c>+0x40</c> is a literal (a load of constant 2 into eax), so the three further names in the
    /// string table (<c>0xF1</c>, <c>0xF2</c>, <c>0xF4</c>) are unreachable.
    /// </summary>
    [Fact]
    public void ControllerConfigurationIndexIsThePresetSchemeInteger()
    {
        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Controller);
        RetailOptionsRow row = menu.Rows[IndexOfLabel(menu, "Configuration:")];

        Assert.Equal(["Custom", "WASD + mouse"], row.States);

        // references/Onslaught/Career.cpp:175 mControllerConfigurationNum.SetAll(1),
        // and the virgin-install path at 0x004EFB10 calls ApplyPreset(1).
        Assert.Equal(1, menu.Settings.ControllerConfiguration);
        Assert.Equal("WASD + mouse", row.CurrentState);
    }

    private static int IndexOfLabel(RetailOptionsMenu menu, string label)
    {
        for (int i = 0; i < menu.Rows.Count; i++)
        {
            if (menu.Rows[i].Label == label)
            {
                return i;
            }
        }
        throw new InvalidOperationException($"No row labelled '{label}' on {menu.Page}.");
    }

    private static void SelectRow(RetailOptionsMenu menu, int index)
    {
        while (menu.SelectedIndex != index)
        {
            Assert.True(menu.MoveSelection(1));
        }
    }
}
