// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later <c>[esp+0x94]</c> shift — <c>0x00460E62</c>
/// <c>fcom [0x005D8568]</c> (1.0) / <c>0x00460E77</c>
/// <c>fmul [0x005D8C70]</c> (255.0) — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460E4D</c> is already the later-esp94 first store
/// consumer <c>fst [esp+0x40]</c> (<c>d9 54 24 40</c>).
/// <c>0x00460E62</c> <c>fcom [0x005D8568]</c>
/// (<c>d8 15 68 85 5d 00</c>), <c>0x005D8568</c> is
/// <c>00 00 80 3f</c> (1.0, bits <c>0x3F800000</c>),
/// <c>0x00460E77</c> <c>fmul [0x005D8C70]</c>
/// (<c>d8 0d 70 8c 5d 00</c>), <c>0x005D8C70</c> is
/// <c>00 00 7f 43</c> (255.0, bits <c>0x437F0000</c>).
/// First store consumer is <c>0x00460E7F</c>
/// <c>fistp [esp+0x4C]</c> (<c>df 7c 24 4c</c>). That is
/// not dest. The later <c>[esp+0x94]</c> shift at
/// <c>0x00460E34</c> is already owned. The later 148.0
/// triple at <c>0x00460E24</c> is already owned. The 10.0
/// fsub at <c>0x00460C94</c> is already owned. The 148.0 fsub
/// at <c>0x00460B66</c> is already owned. Sliding-borders
/// already owns <c>0x00460B61</c>. Latch SET already owns
/// <c>0x0042D5CF</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. The earlier <c>fmul 255.0</c> at
/// <c>0x00460B8D</c> is earlier. The later 610.0 / 0.0 pair
/// at <c>0x00460F30</c> is later. Dest Y does not. Dest is
/// not 15.5, 322.5, 148.0, 10.0, 138.0, 322.0, 610.0, 90.0,
/// 570.0, 0.75, 4.0, 1.0, 255.0, or the 2.0 constant.
/// DrawLevelSelect consumes the leftover as the later
/// 1.0-compare / 255.0-scale of the shifted local, not dest.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284,
/// dest Y=304, dest from the 2.0 constant, wrap, fade, sheen,
/// or a 2px kerning hack. Do not change MeasureText. Do not
/// redo dest leftovers, list colour, list hover, list click,
/// list cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// Apply pulse, dropdown cosine, language pitch, or the
/// 0x00463669 compare. Do not invent dest from 1.0. Do not
/// invent dest from 255.0. Do not invent a fade. Do not
/// invent that the third FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLaterOneTests
{
    [Fact]
    public void SpecimenSitesAreLaterOneScaleNotDestColourHoverClickCancelOrLaterEsp94()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLaterOne.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLaterOne.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLaterOne.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLaterOne.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLaterOne.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLaterOne.LaterEsp94Site);
        Assert.Equal(0x00460E4Du, RetailLevelSelectLaterOne.LaterEsp94FstSite);
        Assert.Equal(0x00460E5Au, RetailLevelSelectLaterOne.ClampLowSite);
        Assert.Equal(0x00460E62u, RetailLevelSelectLaterOne.FcomSite);
        Assert.Equal(0x005D8568u, RetailLevelSelectLaterOne.OneConst);
        Assert.Equal(0x3F800000u, RetailLevelSelectLaterOne.OneBits);
        Assert.Equal(0x00460E71u, RetailLevelSelectLaterOne.ClampHighSite);
        Assert.Equal(0x00460E77u, RetailLevelSelectLaterOne.FmulSite);
        Assert.Equal(0x005D8C70u, RetailLevelSelectLaterOne.ScaleConst);
        Assert.Equal(0x437F0000u, RetailLevelSelectLaterOne.ScaleBits);
        Assert.Equal(0x00460E7Fu, RetailLevelSelectLaterOne.FistpSite);
        Assert.Equal(0x4C, RetailLevelSelectLaterOne.StoreLocal);
        Assert.Equal(0x1C, RetailLevelSelectLaterOne.IntLocal);
        Assert.Equal(0x005D856Cu, RetailLevelSelectLaterOne.ZeroConst);
        Assert.Equal(0x00000000u, RetailLevelSelectLaterOne.ZeroBits);
        Assert.Equal(0x00460B8Du, RetailLevelSelectLaterOne.Earlier255Site);
        Assert.Equal(0x00460F30u, RetailLevelSelectLaterOne.Later610Site);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLaterOne.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLaterOne.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.LaterOneSite,
            RetailLevelSelectLaterOne.FcomSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.Later255Site,
            RetailLevelSelectLaterOne.FmulSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.Later255Const,
            RetailLevelSelectLaterOne.ScaleConst);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.Later255Bits,
            RetailLevelSelectLaterOne.ScaleBits);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterOne.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FstSite,
            RetailLevelSelectLaterOne.LaterEsp94FstSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.ZeroConst,
            RetailLevelSelectLaterOne.ZeroConst);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLaterOne.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLaterOne.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectLaterOne.Fsub148Site);
        Assert.Equal(
            RetailLevelSelectFsub10.FsubSite,
            RetailLevelSelectLaterOne.Fsub10Site);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLaterOne.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectLaterOne.FmvOrSite);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.LaterEsp94Site);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.Later148Site);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.Fsub10Site);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.Fsub148Site);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FmulSite,
            RetailLevelSelectLaterOne.Earlier255Site);
        Assert.NotEqual(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterOne.Later610Site);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLaterOne.FcomSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectLaterOne.FcomSite);
        Assert.True(RetailLevelSelectLaterOne.SlidingCallSite < RetailLevelSelectLaterOne.Fsub148Site);
        Assert.True(RetailLevelSelectLaterOne.Fsub148Site < RetailLevelSelectLaterOne.Earlier255Site);
        Assert.True(RetailLevelSelectLaterOne.Earlier255Site < RetailLevelSelectLaterOne.Fsub10Site);
        Assert.True(RetailLevelSelectLaterOne.Fsub10Site < RetailLevelSelectLaterOne.Later148Site);
        Assert.True(RetailLevelSelectLaterOne.Later148Site < RetailLevelSelectLaterOne.LaterEsp94Site);
        Assert.True(RetailLevelSelectLaterOne.LaterEsp94Site < RetailLevelSelectLaterOne.LaterEsp94FstSite);
        Assert.True(RetailLevelSelectLaterOne.LaterEsp94FstSite < RetailLevelSelectLaterOne.ClampLowSite);
        Assert.True(RetailLevelSelectLaterOne.ClampLowSite < RetailLevelSelectLaterOne.FcomSite);
        Assert.True(RetailLevelSelectLaterOne.FcomSite < RetailLevelSelectLaterOne.ClampHighSite);
        Assert.True(RetailLevelSelectLaterOne.ClampHighSite < RetailLevelSelectLaterOne.FmulSite);
        Assert.True(RetailLevelSelectLaterOne.FmulSite < RetailLevelSelectLaterOne.FistpSite);
        Assert.True(RetailLevelSelectLaterOne.FistpSite < RetailLevelSelectLaterOne.Later610Site);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestX5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY268);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY284);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY304);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFromPad);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom148);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom10);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom138);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom322);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom610);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom90);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom570);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterOne.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterOne.InventsKerningHack);
        Assert.False(RetailLevelSelectLaterOne.InventsSheen);
        Assert.False(RetailLevelSelectLaterOne.InventsWrapWidth);
        Assert.False(RetailLevelSelectLaterOne.InventsFade);
        Assert.False(RetailLevelSelectLaterOne.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.False(RetailLevelSelectLaterOne.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterOne.IsLater148);
        Assert.False(RetailLevelSelectLaterOne.IsFsub148);
        Assert.False(RetailLevelSelectLaterOne.IsFsub10);
        Assert.False(RetailLevelSelectLaterOne.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLaterOne.IsLatchSet);
        Assert.False(RetailLevelSelectLaterOne.IsFmvSkip);
        Assert.False(RetailLevelSelectLaterOne.IsClickSound);
        Assert.False(RetailLevelSelectLaterOne.IsClickHit);
        Assert.False(RetailLevelSelectLaterOne.IsHoverHit);
        Assert.False(RetailLevelSelectLaterOne.IsCancel);
        Assert.False(RetailLevelSelectLaterOne.IsSetLanguage);
        Assert.False(RetailLevelSelectLaterOne.IsButtonPressed);
        Assert.False(RetailLevelSelectLaterOne.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectLaterOne.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectLaterOne.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLaterOne.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLaterOne.RedoesFsub148);
        Assert.False(RetailLevelSelectLaterOne.RedoesFsub10);
        Assert.False(RetailLevelSelectLaterOne.RedoesLater148);
        Assert.False(RetailLevelSelectLaterOne.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLaterOne.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectLaterOne.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectLaterOne.UsesLanguageCompare);
        Assert.False(RetailLevelSelectLaterOne.ChangesMeasureText);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.True(RetailLevelSelectLater148.IsLater148);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void ScaledIsClampedShiftedTimes255AndDoesNotInventDestFrom1Or255()
    {
        Assert.Equal(1f, RetailLevelSelectLaterOne.CompareOne);
        Assert.Equal(255f, RetailLevelSelectLaterOne.Scale);
        Assert.Equal(0f, RetailLevelSelectLaterOne.CompareZero);
        Assert.Equal(0f, RetailLevelSelectLaterOne.Clamped(-3f));
        Assert.Equal(0f, RetailLevelSelectLaterOne.Clamped(0f));
        Assert.Equal(0.5f, RetailLevelSelectLaterOne.Clamped(0.5f));
        Assert.Equal(1f, RetailLevelSelectLaterOne.Clamped(1f));
        Assert.Equal(1f, RetailLevelSelectLaterOne.Clamped(4f));
        Assert.False(RetailLevelSelectLaterOne.AboveOne(1f));
        Assert.True(RetailLevelSelectLaterOne.AboveOne(4f));
        Assert.Equal(0f, RetailLevelSelectLaterOne.Scaled(-3f));
        Assert.Equal(0f, RetailLevelSelectLaterOne.Scaled(0f));
        Assert.Equal(127.5f, RetailLevelSelectLaterOne.Scaled(0.5f));
        Assert.Equal(255f, RetailLevelSelectLaterOne.Scaled(1f));
        Assert.Equal(255f, RetailLevelSelectLaterOne.Scaled(4f));
        Assert.Equal(
            0f,
            RetailLevelSelectLaterOne.Scaled(
                RetailLevelSelectLaterEsp94.Shifted(0.75f)));
        Assert.Equal(
            255f,
            RetailLevelSelectLaterOne.Scaled(
                RetailLevelSelectLaterEsp94.Shifted(1f)));
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom90);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom570);
        Assert.False(RetailLevelSelectLaterOne.InventsDestFrom148);
        Assert.False(RetailLevelSelectLaterOne.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterOne.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterOne.InventsFade);
        Assert.False(RetailLevelSelectLaterOne.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLaterOne.RedoesLater148);
        Assert.False(RetailLevelSelectLaterOne.RedoesFsub148);
        Assert.False(RetailLevelSelectLaterOne.RedoesFsub10);
        Assert.False(RetailLevelSelectLaterOne.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLaterOne.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLaterOne.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterOne.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.False(RetailLevelSelectLaterOne.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterOne.IsLater148);
        Assert.False(RetailLevelSelectLaterOne.IsFsub148);
        Assert.False(RetailLevelSelectLaterOne.IsFsub10);
        Assert.False(RetailLevelSelectLaterOne.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLaterOne.IsLatchSet);
        Assert.False(RetailLevelSelectLaterOne.IsFmvSkip);
        Assert.False(RetailLevelSelectLaterOne.IsClickHit);
        Assert.False(RetailLevelSelectLaterOne.IsHoverHit);
        Assert.False(RetailLevelSelectLaterOne.IsCancel);
        Assert.False(RetailLevelSelectLaterOne.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLaterOneAndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLaterOne", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterOne.CompareOne", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterOne.Scale", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("255f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLaterOne", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterOne", cancel, StringComparison.Ordinal);
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
