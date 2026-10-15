// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later 148.0 triple — <c>0x00460E34</c> <c>fld [esp+0x94]</c> /
/// <c>0x00460E3B</c> <c>fsub [0x005D8BC4]</c> (0.75) /
/// <c>0x00460E41</c> <c>fmul [0x005D85BC]</c> (4.0) /
/// <c>0x00460E47</c> <c>fcom [0x005D856C]</c> (0.0) — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460E34</c> <c>fld [esp+0x94]</c>
/// (<c>d9 84 24 94 00 00 00</c>), <c>0x00460E3B</c>
/// <c>fsub [0x005D8BC4]</c> (<c>d8 25 c4 8b 5d 00</c>),
/// <c>0x005D8BC4</c> is <c>00 00 40 3f</c> (0.75, bits
/// <c>0x3F400000</c>), <c>0x00460E41</c> <c>fmul [0x005D85BC]</c>
/// (<c>d8 0d bc 85 5d 00</c>), <c>0x005D85BC</c> is
/// <c>00 00 80 40</c> (4.0, bits <c>0x40800000</c>),
/// <c>0x00460E47</c> <c>fcom [0x005D856C]</c>
/// (<c>d8 15 6c 85 5d 00</c>), <c>0x005D856C</c> is
/// <c>00 00 00 00</c> (0.0). First store consumer is
/// <c>0x00460E4D</c> <c>fst [esp+0x40]</c>
/// (<c>d9 54 24 40</c>). That is not dest. The later 148.0
/// triple at <c>0x00460E24</c> is already owned. The 10.0
/// fsub at <c>0x00460C94</c> is already owned. The 148.0 fsub
/// at <c>0x00460B66</c> is already owned. Sliding-borders
/// already owns <c>0x00460B61</c>. Latch SET already owns
/// <c>0x0042D5CF</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. The earlier <c>fld [esp+0x94]</c> at
/// <c>0x00460B76</c> is earlier and has no 0.75 fsub. The
/// later 1.0 fcom at <c>0x00460E62</c> is later. The later
/// 255.0 fmul at <c>0x00460E77</c> is later. The later
/// 610.0 / 0.0 pair at <c>0x00460F30</c> is later. Dest Y
/// does not. Dest is not 15.5, 322.5, 148.0, 10.0, 138.0,
/// 322.0, 610.0, 90.0, 570.0, 0.75, 4.0, 255.0, or the 2.0
/// constant. DrawLevelSelect consumes the leftover as the
/// later stack-local shift, not dest. Do not invent dest
/// Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest
/// from the 2.0 constant, wrap, fade, sheen, or a 2px
/// kerning hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list
/// cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, Apply pulse, dropdown cosine,
/// language pitch, or the 0x00463669 compare. Do not invent
/// dest from 0.75. Do not invent dest from 4.0. Do not
/// invent dest from 255.0. Do not invent that the third FMV
/// latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLaterEsp94Tests
{
    [Fact]
    public void SpecimenSitesAreLaterEsp94ShiftNotDestColourHoverClickCancelOrLater148()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLaterEsp94.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLaterEsp94.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLaterEsp94.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLaterEsp94.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLaterEsp94.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLaterEsp94.FldSite);
        Assert.Equal(0x94, RetailLevelSelectLaterEsp94.StackLocal);
        Assert.Equal(0x00460E3Bu, RetailLevelSelectLaterEsp94.FsubSite);
        Assert.Equal(0x005D8BC4u, RetailLevelSelectLaterEsp94.SubtrahendConst);
        Assert.Equal(0x3F400000u, RetailLevelSelectLaterEsp94.SubtrahendBits);
        Assert.Equal(0x00460E41u, RetailLevelSelectLaterEsp94.FmulSite);
        Assert.Equal(0x005D85BCu, RetailLevelSelectLaterEsp94.FactorConst);
        Assert.Equal(0x40800000u, RetailLevelSelectLaterEsp94.FactorBits);
        Assert.Equal(0x00460E47u, RetailLevelSelectLaterEsp94.FcomSite);
        Assert.Equal(0x005D856Cu, RetailLevelSelectLaterEsp94.ZeroConst);
        Assert.Equal(0x00000000u, RetailLevelSelectLaterEsp94.ZeroBits);
        Assert.Equal(0x00460E4Du, RetailLevelSelectLaterEsp94.FstSite);
        Assert.Equal(0x40, RetailLevelSelectLaterEsp94.StoreLocal);
        Assert.Equal(0x00460B76u, RetailLevelSelectLaterEsp94.EarlierEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLaterEsp94.LaterOneSite);
        Assert.Equal(0x00460E77u, RetailLevelSelectLaterEsp94.Later255Site);
        Assert.Equal(0x005D8C70u, RetailLevelSelectLaterEsp94.Later255Const);
        Assert.Equal(0x437F0000u, RetailLevelSelectLaterEsp94.Later255Bits);
        Assert.Equal(0x00460F30u, RetailLevelSelectLaterEsp94.Later610Site);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLaterEsp94.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLaterEsp94.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLater148.LaterEsp94Site,
            RetailLevelSelectLaterEsp94.FldSite);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLaterEsp94.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLaterEsp94.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectLaterEsp94.Fsub148Site);
        Assert.Equal(
            RetailLevelSelectFsub10.FsubSite,
            RetailLevelSelectLaterEsp94.Fsub10Site);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLaterEsp94.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectLaterEsp94.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectFsub148.WindowLowConst,
            RetailLevelSelectLaterEsp94.ZeroConst);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.Later148Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.Fsub10Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.Fsub148Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.EarlierEsp94Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94.Later610Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94.StackLocal,
            RetailLevelSelectLater148.StackLocal);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLaterEsp94.FldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectLaterEsp94.FldSite);
        Assert.True(RetailLevelSelectLaterEsp94.SlidingCallSite < RetailLevelSelectLaterEsp94.Fsub148Site);
        Assert.True(RetailLevelSelectLaterEsp94.Fsub148Site < RetailLevelSelectLaterEsp94.EarlierEsp94Site);
        Assert.True(RetailLevelSelectLaterEsp94.EarlierEsp94Site < RetailLevelSelectLaterEsp94.Fsub10Site);
        Assert.True(RetailLevelSelectLaterEsp94.Fsub10Site < RetailLevelSelectLaterEsp94.Later148Site);
        Assert.True(RetailLevelSelectLaterEsp94.Later148Site < RetailLevelSelectLaterEsp94.FldSite);
        Assert.True(RetailLevelSelectLaterEsp94.FldSite < RetailLevelSelectLaterEsp94.FsubSite);
        Assert.True(RetailLevelSelectLaterEsp94.FsubSite < RetailLevelSelectLaterEsp94.FmulSite);
        Assert.True(RetailLevelSelectLaterEsp94.FmulSite < RetailLevelSelectLaterEsp94.FcomSite);
        Assert.True(RetailLevelSelectLaterEsp94.FcomSite < RetailLevelSelectLaterEsp94.FstSite);
        Assert.True(RetailLevelSelectLaterEsp94.FstSite < RetailLevelSelectLaterEsp94.LaterOneSite);
        Assert.True(RetailLevelSelectLaterEsp94.LaterOneSite < RetailLevelSelectLaterEsp94.Later255Site);
        Assert.True(RetailLevelSelectLaterEsp94.Later255Site < RetailLevelSelectLaterEsp94.Later610Site);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestX5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY268);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY284);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY304);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFromPad);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom148);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom10);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom138);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom322);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom610);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom90);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom570);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterEsp94.InventsKerningHack);
        Assert.False(RetailLevelSelectLaterEsp94.InventsSheen);
        Assert.False(RetailLevelSelectLaterEsp94.InventsWrapWidth);
        Assert.False(RetailLevelSelectLaterEsp94.InventsFade);
        Assert.False(RetailLevelSelectLaterEsp94.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94.IsLater148);
        Assert.False(RetailLevelSelectLaterEsp94.IsFsub148);
        Assert.False(RetailLevelSelectLaterEsp94.IsFsub10);
        Assert.False(RetailLevelSelectLaterEsp94.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLaterEsp94.IsLatchSet);
        Assert.False(RetailLevelSelectLaterEsp94.IsFmvSkip);
        Assert.False(RetailLevelSelectLaterEsp94.IsClickSound);
        Assert.False(RetailLevelSelectLaterEsp94.IsClickHit);
        Assert.False(RetailLevelSelectLaterEsp94.IsHoverHit);
        Assert.False(RetailLevelSelectLaterEsp94.IsCancel);
        Assert.False(RetailLevelSelectLaterEsp94.IsSetLanguage);
        Assert.False(RetailLevelSelectLaterEsp94.IsButtonPressed);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesFsub148);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesFsub10);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesLater148);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectLaterEsp94.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectLaterEsp94.UsesLanguageCompare);
        Assert.False(RetailLevelSelectLaterEsp94.ChangesMeasureText);
        Assert.True(RetailLevelSelectLater148.IsLater148);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void ShiftedIsLocalMinus075Times4AndDoesNotInventDestFrom075Or4()
    {
        Assert.Equal(0.75f, RetailLevelSelectLaterEsp94.Subtrahend);
        Assert.Equal(4f, RetailLevelSelectLaterEsp94.Factor);
        Assert.Equal(0f, RetailLevelSelectLaterEsp94.CompareZero);
        Assert.Equal(0f, RetailLevelSelectLaterEsp94.Shifted(0.75f));
        Assert.Equal(4f, RetailLevelSelectLaterEsp94.Shifted(1.75f));
        Assert.Equal(-3f, RetailLevelSelectLaterEsp94.Shifted(0f));
        Assert.False(RetailLevelSelectLaterEsp94.BelowZero(RetailLevelSelectLaterEsp94.Shifted(0.75f)));
        Assert.True(RetailLevelSelectLaterEsp94.BelowZero(RetailLevelSelectLaterEsp94.Shifted(0f)));
        Assert.False(RetailLevelSelectLaterEsp94.BelowZero(0f));
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom90);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom570);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestFrom148);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterEsp94.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterEsp94.InventsFade);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesLater148);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesFsub148);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesFsub10);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLaterEsp94.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLaterEsp94.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterEsp94.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94.IsLater148);
        Assert.False(RetailLevelSelectLaterEsp94.IsFsub148);
        Assert.False(RetailLevelSelectLaterEsp94.IsFsub10);
        Assert.False(RetailLevelSelectLaterEsp94.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLaterEsp94.IsLatchSet);
        Assert.False(RetailLevelSelectLaterEsp94.IsFmvSkip);
        Assert.False(RetailLevelSelectLaterEsp94.IsClickHit);
        Assert.False(RetailLevelSelectLaterEsp94.IsHoverHit);
        Assert.False(RetailLevelSelectLaterEsp94.IsCancel);
        Assert.False(RetailLevelSelectLaterEsp94.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLaterEsp94AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLaterEsp94", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterEsp94.Subtrahend", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterEsp94.Factor", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("0.75f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("4.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("255f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94", cancel, StringComparison.Ordinal);
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
