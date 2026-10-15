// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// 10.0 fsub — <c>0x00460E24</c> <c>fld [0x005DB53C]</c> (148.0) /
/// <c>0x00460E2A</c> <c>fsub [esi+0x3460]</c> /
/// <c>0x00460E30</c> <c>fstp [esp+0x14]</c> — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop. Two image hits of the triple: <c>0x00460B66</c>
/// already owned by <c>RetailLevelSelectFsub148</c>, and this later
/// site.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460E24</c> <c>fld [0x005DB53C]</c>
/// (<c>d9 05 3c b5 5d 00</c>), <c>0x005DB53C</c> is
/// <c>00 00 14 43</c> (148.0), <c>0x00460E2A</c>
/// <c>fsub [esi+0x3460]</c> (<c>d8 a6 60 34 00 00</c>),
/// <c>0x00460E30</c> <c>fstp [esp+0x14]</c>
/// (<c>d9 5c 24 14</c>). First consumers are
/// <c>0x00460E99</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460E9D</c> <c>fcomp [0x00629390]</c> (90.0,
/// bits <c>0x42B40000</c>) and <c>0x00460EAC</c>
/// <c>fld [esp+0x14]</c> / <c>0x00460EB0</c>
/// <c>fcomp [0x00629394]</c> (570.0, bits <c>0x440E8000</c>).
/// <c>test ah, 1</c> / <c>jnz</c> clears the flag when the pad
/// is below 90.0. <c>test ah, 0x41</c> / <c>jnz</c> keeps the
/// flag when the pad is not above 570.0. Window is
/// <c>90.0 &lt;= pad &lt;= 570.0</c>. That is not dest. The
/// earlier identical triple at <c>0x00460B66</c> is already
/// owned. The 10.0 fsub at <c>0x00460C94</c> is already owned.
/// Sliding-borders already owns <c>0x00460B61</c>. Latch SET
/// already owns <c>0x0042D5CF</c>. FMV skip already owns the
/// OR at <c>0x0053F2EB</c>. The later <c>[esp+0x94]</c> fsub
/// at <c>0x00460E34</c> is later. The later 610.0 / 0.0 pair
/// at <c>0x00460F30</c> is later. Dest Y does not. Dest is not
/// 15.5, 322.5, 148.0, 10.0, 138.0, 322.0, 610.0, 90.0, 570.0,
/// or the 2.0 constant. DrawLevelSelect consumes the leftover
/// as the later settled pad window, not dest. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px
/// kerning hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list cancel,
/// click-hit sound, latch-to-button SET, the sliding-borders
/// call, the 148.0 fsub, the 10.0 fsub, Apply pulse, dropdown
/// cosine, language pitch, or the 0x00463669 compare. Do not
/// invent dest from 148.0. Do not invent dest from 90.0. Do
/// not invent dest from 570.0. Do not invent that the third
/// FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLater148Tests
{
    [Fact]
    public void SpecimenSitesAreLater148WindowNotDestColourHoverClickCancelOrFsub10()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLater148.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLater148.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLater148.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLater148.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLater148.FldSite);
        Assert.Equal(0x005DB53Cu, RetailLevelSelectLater148.OffsetConst);
        Assert.Equal(0x43140000u, RetailLevelSelectLater148.OffsetBits);
        Assert.Equal(0x00460E2Au, RetailLevelSelectLater148.FsubSite);
        Assert.Equal(0x3460, RetailLevelSelectLater148.FieldOffset);
        Assert.Equal(0x00460E30u, RetailLevelSelectLater148.FstpSite);
        Assert.Equal(0x14, RetailLevelSelectLater148.StackLocal);
        Assert.Equal(0x00460E99u, RetailLevelSelectLater148.WindowLowFldSite);
        Assert.Equal(0x00460E9Du, RetailLevelSelectLater148.WindowLowFcompSite);
        Assert.Equal(0x00629390u, RetailLevelSelectLater148.WindowLowConst);
        Assert.Equal(0x42B40000u, RetailLevelSelectLater148.WindowLowBits);
        Assert.Equal(0x00460EACu, RetailLevelSelectLater148.WindowHighFldSite);
        Assert.Equal(0x00460EB0u, RetailLevelSelectLater148.WindowHighFcompSite);
        Assert.Equal(0x00629394u, RetailLevelSelectLater148.WindowHighConst);
        Assert.Equal(0x440E8000u, RetailLevelSelectLater148.WindowHighBits);
        Assert.Equal(0x00460E34u, RetailLevelSelectLater148.LaterEsp94Site);
        Assert.Equal(0x00460F30u, RetailLevelSelectLater148.Later610Site);
        Assert.Equal(0x004721D7u, RetailLevelSelectLater148.OtherPairSite);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLater148.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLater148.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectFsub148.LaterTripleSite,
            RetailLevelSelectLater148.FldSite);
        Assert.Equal(
            RetailLevelSelectFsub10.LaterTripleSite,
            RetailLevelSelectLater148.FldSite);
        Assert.Equal(
            RetailLevelSelectFsub148.OffsetConst,
            RetailLevelSelectLater148.OffsetConst);
        Assert.Equal(
            RetailLevelSelectFsub148.OffsetBits,
            RetailLevelSelectLater148.OffsetBits);
        Assert.Equal(
            RetailLevelSelectFsub148.FieldOffset,
            RetailLevelSelectLater148.FieldOffset);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLater148.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectLater148.Fsub148Site);
        Assert.Equal(
            RetailLevelSelectFsub10.FsubSite,
            RetailLevelSelectLater148.Fsub10Site);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLater148.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectLater148.FmvOrSite);
        Assert.NotEqual(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater148.Fsub148Site);
        Assert.NotEqual(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater148.Fsub10Site);
        Assert.NotEqual(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater148.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater148.LaterEsp94Site);
        Assert.NotEqual(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater148.Later610Site);
        Assert.NotEqual(
            RetailLevelSelectLater148.WindowLowConst,
            RetailLevelSelectFsub148.WindowLowConst);
        Assert.NotEqual(
            RetailLevelSelectLater148.WindowHighConst,
            RetailLevelSelectFsub148.WindowHighConst);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLater148.FldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectLater148.FldSite);
        Assert.True(RetailLevelSelectLater148.SlidingCallSite < RetailLevelSelectLater148.Fsub148Site);
        Assert.True(RetailLevelSelectLater148.Fsub148Site < RetailLevelSelectLater148.Fsub10Site);
        Assert.True(RetailLevelSelectLater148.Fsub10Site < RetailLevelSelectLater148.FldSite);
        Assert.True(RetailLevelSelectLater148.FldSite < RetailLevelSelectLater148.FsubSite);
        Assert.True(RetailLevelSelectLater148.FsubSite < RetailLevelSelectLater148.FstpSite);
        Assert.True(RetailLevelSelectLater148.FstpSite < RetailLevelSelectLater148.LaterEsp94Site);
        Assert.True(RetailLevelSelectLater148.LaterEsp94Site < RetailLevelSelectLater148.WindowLowFldSite);
        Assert.True(RetailLevelSelectLater148.WindowLowFldSite < RetailLevelSelectLater148.WindowLowFcompSite);
        Assert.True(RetailLevelSelectLater148.WindowLowFcompSite < RetailLevelSelectLater148.WindowHighFldSite);
        Assert.True(RetailLevelSelectLater148.WindowHighFldSite < RetailLevelSelectLater148.WindowHighFcompSite);
        Assert.True(RetailLevelSelectLater148.WindowHighFcompSite < RetailLevelSelectLater148.Later610Site);
        Assert.False(RetailLevelSelectLater148.InventsDestY5);
        Assert.False(RetailLevelSelectLater148.InventsDestX5);
        Assert.False(RetailLevelSelectLater148.InventsDestY268);
        Assert.False(RetailLevelSelectLater148.InventsDestY284);
        Assert.False(RetailLevelSelectLater148.InventsDestY304);
        Assert.False(RetailLevelSelectLater148.InventsDestFromPad);
        Assert.False(RetailLevelSelectLater148.InventsDestY15_5);
        Assert.False(RetailLevelSelectLater148.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom148);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom10);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom138);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom322);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom90);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom570);
        Assert.False(RetailLevelSelectLater148.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater148.InventsKerningHack);
        Assert.False(RetailLevelSelectLater148.InventsSheen);
        Assert.False(RetailLevelSelectLater148.InventsWrapWidth);
        Assert.False(RetailLevelSelectLater148.InventsFade);
        Assert.False(RetailLevelSelectLater148.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater148.IsLater148);
        Assert.False(RetailLevelSelectLater148.IsFsub148);
        Assert.False(RetailLevelSelectLater148.IsFsub10);
        Assert.False(RetailLevelSelectLater148.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLater148.IsLatchSet);
        Assert.False(RetailLevelSelectLater148.IsFmvSkip);
        Assert.False(RetailLevelSelectLater148.IsClickSound);
        Assert.False(RetailLevelSelectLater148.IsClickHit);
        Assert.False(RetailLevelSelectLater148.IsHoverHit);
        Assert.False(RetailLevelSelectLater148.IsCancel);
        Assert.False(RetailLevelSelectLater148.IsSetLanguage);
        Assert.False(RetailLevelSelectLater148.IsButtonPressed);
        Assert.False(RetailLevelSelectLater148.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectLater148.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownDest);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectLater148.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectLater148.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLater148.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLater148.RedoesFsub148);
        Assert.False(RetailLevelSelectLater148.RedoesFsub10);
        Assert.False(RetailLevelSelectLater148.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectLater148.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectLater148.UsesLanguageCompare);
        Assert.False(RetailLevelSelectLater148.ChangesMeasureText);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void SettledPadAppliesInsideTheLaterWindowAndDoesNotInventDestFrom90Or570()
    {
        Assert.Equal(148f, RetailLevelSelectLater148.Offset);
        Assert.Equal(90f, RetailLevelSelectLater148.WindowLow);
        Assert.Equal(570f, RetailLevelSelectLater148.WindowHigh);
        Assert.Equal(0f, RetailLevelSelectLater148.SettledField);
        Assert.Equal(148f, RetailLevelSelectLater148.SettledPad);
        Assert.Equal(148f, RetailLevelSelectLater148.Pad(0f));
        Assert.Equal(0f, RetailLevelSelectLater148.Pad(148f));
        Assert.Equal(
            RetailLevelSelectFsub148.SettledPad,
            RetailLevelSelectLater148.SettledPad);
        Assert.True(RetailLevelSelectLater148.Applies(RetailLevelSelectLater148.SettledPad));
        Assert.True(RetailLevelSelectLater148.Applies(RetailLevelSelectLater148.Pad(0f)));
        Assert.True(RetailLevelSelectLater148.Applies(90f));
        Assert.True(RetailLevelSelectLater148.Applies(570f));
        Assert.False(RetailLevelSelectLater148.Applies(89f));
        Assert.False(RetailLevelSelectLater148.Applies(571f));
        Assert.False(RetailLevelSelectLater148.Applies(0f));
        Assert.NotEqual(
            RetailLevelSelectFsub148.WindowLow,
            RetailLevelSelectLater148.WindowLow);
        Assert.NotEqual(
            RetailLevelSelectFsub148.WindowHigh,
            RetailLevelSelectLater148.WindowHigh);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom90);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom570);
        Assert.False(RetailLevelSelectLater148.InventsDestFrom148);
        Assert.False(RetailLevelSelectLater148.InventsDestY15_5);
        Assert.False(RetailLevelSelectLater148.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater148.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater148.RedoesFsub148);
        Assert.False(RetailLevelSelectLater148.RedoesFsub10);
        Assert.False(RetailLevelSelectLater148.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLater148.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLater148.ChangesMeasureText);
        Assert.False(RetailLevelSelectLater148.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater148.IsLater148);
        Assert.False(RetailLevelSelectLater148.IsFsub148);
        Assert.False(RetailLevelSelectLater148.IsFsub10);
        Assert.False(RetailLevelSelectLater148.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLater148.IsLatchSet);
        Assert.False(RetailLevelSelectLater148.IsFmvSkip);
        Assert.False(RetailLevelSelectLater148.IsClickHit);
        Assert.False(RetailLevelSelectLater148.IsHoverHit);
        Assert.False(RetailLevelSelectLater148.IsCancel);
        Assert.False(RetailLevelSelectLater148.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLater148AndDoesNotPileIntoMainMenuOrOptions()
    {
        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string level = Slice(flow, "private void DrawLevelSelect()");
        string main = Slice(flow, "private void DrawMainMenu()");
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        string loading = Slice(flow, "private void DrawLoading(");
        string click = Slice(flow, "private void DrawClickToStart()");
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        string handleKey = Slice(flow, "private bool HandleKey(");

        Assert.Contains("RetailLevelSelectLater148", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater148.Applies", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater148.Pad", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater148.SettledField", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("90f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("570f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLater148", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater148", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", cancel, StringComparison.Ordinal);
    }

    private static string Slice(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf("\n    private ", signature.Length, StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }
}
