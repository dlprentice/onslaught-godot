// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later 1.0 fcom — <c>0x00460F30</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460F34</c> <c>fcomp [0x005DB5B0]</c> (610.0) /
/// <c>0x00460F45</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460F49</c> <c>fcomp [0x005D856C]</c> (0.0) —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460E7F</c> is already the later-one first store
/// consumer <c>fistp [esp+0x4C]</c> (<c>df 7c 24 4c</c>).
/// <c>0x00460F30</c> <c>fld [esp+0x14]</c>
/// (<c>d9 44 24 14</c>), <c>0x00460F34</c>
/// <c>fcomp [0x005DB5B0]</c> (<c>d8 1d b0 b5 5d 00</c>),
/// <c>0x005DB5B0</c> is <c>00 80 18 44</c> (610.0, bits
/// <c>0x44188000</c>), <c>0x00460F45</c>
/// <c>fld [esp+0x14]</c> (<c>d9 44 24 14</c>),
/// <c>0x00460F49</c> <c>fcomp [0x005D856C]</c>
/// (<c>d8 1d 6c 85 5d 00</c>), <c>0x005D856C</c> is
/// <c>00 00 00 00</c> (0.0, bits <c>0x00000000</c>).
/// First consumer is <c>0x00460F5A</c>
/// <c>mov eax, [esi+ebx*4+0x4]</c> (<c>8b 44 9e 04</c>).
/// First store is <c>0x00460F5E</c>
/// <c>mov [esp+0x28], 0</c> (<c>c7 44 24 28 00 00 00 00</c>).
/// That is not dest. The later 1.0 fcom at <c>0x00460E62</c>
/// is already owned. The later <c>[esp+0x94]</c> shift at
/// <c>0x00460E34</c> is already owned. The later 148.0
/// triple at <c>0x00460E24</c> is already owned. The 10.0
/// fsub at <c>0x00460C94</c> is already owned. The 148.0 fsub
/// at <c>0x00460B66</c> is already owned. Sliding-borders
/// already owns <c>0x00460B61</c>. Latch SET already owns
/// <c>0x0042D5CF</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. The earlier identical 610.0 / 0.0 pair
/// at <c>0x00460BE4</c> is already owned. The later
/// <c>fmul 60.0</c> at <c>0x00460F73</c> is later. Dest Y
/// does not. Dest is not 15.5, 322.5, 148.0, 10.0, 138.0,
/// 322.0, 610.0, 90.0, 570.0, 0.75, 4.0, 1.0, 255.0, 0.0,
/// 60.0, or the 2.0 constant. DrawLevelSelect consumes the
/// leftover as the later 610.0 / 0.0 window on
/// <c>[esp+0x14]</c>, not dest. Do not invent dest Y=5,
/// dest X=5, dest Y=268, dest Y=284, dest Y=304, dest from
/// the 2.0 constant, wrap, fade, sheen, or a 2px kerning
/// hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list
/// cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// the later 1.0 fcom, Apply pulse, dropdown cosine,
/// language pitch, or the 0x00463669 compare. Do not invent
/// dest from 610.0. Do not invent dest from 0.0. Do not
/// invent a fade. Do not invent that the third FMV latch is
/// dest.</para>
/// </summary>
public sealed class RetailLevelSelectLater610Tests
{
    [Fact]
    public void SpecimenSitesAreLater610WindowNotDestColourHoverClickCancelOrLaterOne()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLater610.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLater610.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLater610.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLater610.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLater610.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLater610.LaterEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLater610.LaterOneSite);
        Assert.Equal(0x00460E7Fu, RetailLevelSelectLater610.LaterOneFistpSite);
        Assert.Equal(0x00460BE4u, RetailLevelSelectLater610.Earlier610Site);
        Assert.Equal(0x00460F30u, RetailLevelSelectLater610.WindowHighFldSite);
        Assert.Equal(0x00460F34u, RetailLevelSelectLater610.WindowHighFcompSite);
        Assert.Equal(0x005DB5B0u, RetailLevelSelectLater610.WindowHighConst);
        Assert.Equal(0x44188000u, RetailLevelSelectLater610.WindowHighBits);
        Assert.Equal(0x14, RetailLevelSelectLater610.StackLocal);
        Assert.Equal(0x00460F45u, RetailLevelSelectLater610.WindowLowFldSite);
        Assert.Equal(0x00460F49u, RetailLevelSelectLater610.WindowLowFcompSite);
        Assert.Equal(0x005D856Cu, RetailLevelSelectLater610.WindowLowConst);
        Assert.Equal(0x00000000u, RetailLevelSelectLater610.WindowLowBits);
        Assert.Equal(0x00460F5Au, RetailLevelSelectLater610.FirstConsumerSite);
        Assert.Equal(0x00460F5Eu, RetailLevelSelectLater610.FirstStoreSite);
        Assert.Equal(0x28, RetailLevelSelectLater610.StoreLocal);
        Assert.Equal(0x00460F73u, RetailLevelSelectLater610.LaterFmul60Site);
        Assert.Equal(0x005DB538u, RetailLevelSelectLater610.Later60Const);
        Assert.Equal(0x42700000u, RetailLevelSelectLater610.Later60Bits);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLater610.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLater610.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLaterOne.Later610Site,
            RetailLevelSelectLater610.WindowHighFldSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.Later610Site,
            RetailLevelSelectLater610.WindowHighFldSite);
        Assert.Equal(
            RetailLevelSelectLater148.Later610Site,
            RetailLevelSelectLater610.WindowHighFldSite);
        Assert.Equal(
            RetailLevelSelectFsub148.WindowHighConst,
            RetailLevelSelectLater610.WindowHighConst);
        Assert.Equal(
            RetailLevelSelectFsub148.WindowHighBits,
            RetailLevelSelectLater610.WindowHighBits);
        Assert.Equal(
            RetailLevelSelectFsub148.WindowHighFldSite,
            RetailLevelSelectLater610.Earlier610Site);
        Assert.Equal(
            RetailLevelSelectLaterOne.FistpSite,
            RetailLevelSelectLater610.LaterOneFistpSite);
        Assert.Equal(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLater610.LaterOneSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLater610.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater610.Later148Site);
        Assert.Equal(
            RetailLevelSelectLater148.StackLocal,
            RetailLevelSelectLater610.StackLocal);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.ZeroConst,
            RetailLevelSelectLater610.WindowLowConst);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLater610.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectLater610.Fsub148Site);
        Assert.Equal(
            RetailLevelSelectFsub10.FsubSite,
            RetailLevelSelectLater610.Fsub10Site);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLater610.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectLater610.FmvOrSite);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.Earlier610Site);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.LaterOneSite);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.LaterEsp94Site);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.Later148Site);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.Fsub10Site);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.Fsub148Site);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater610.LaterFmul60Site);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLater610.WindowHighFldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectLater610.WindowHighFldSite);
        Assert.True(RetailLevelSelectLater610.SlidingCallSite < RetailLevelSelectLater610.Fsub148Site);
        Assert.True(RetailLevelSelectLater610.Fsub148Site < RetailLevelSelectLater610.Earlier610Site);
        Assert.True(RetailLevelSelectLater610.Earlier610Site < RetailLevelSelectLater610.Fsub10Site);
        Assert.True(RetailLevelSelectLater610.Fsub10Site < RetailLevelSelectLater610.Later148Site);
        Assert.True(RetailLevelSelectLater610.Later148Site < RetailLevelSelectLater610.LaterEsp94Site);
        Assert.True(RetailLevelSelectLater610.LaterEsp94Site < RetailLevelSelectLater610.LaterOneSite);
        Assert.True(RetailLevelSelectLater610.LaterOneSite < RetailLevelSelectLater610.LaterOneFistpSite);
        Assert.True(RetailLevelSelectLater610.LaterOneFistpSite < RetailLevelSelectLater610.WindowHighFldSite);
        Assert.True(RetailLevelSelectLater610.WindowHighFldSite < RetailLevelSelectLater610.WindowHighFcompSite);
        Assert.True(RetailLevelSelectLater610.WindowHighFcompSite < RetailLevelSelectLater610.WindowLowFldSite);
        Assert.True(RetailLevelSelectLater610.WindowLowFldSite < RetailLevelSelectLater610.WindowLowFcompSite);
        Assert.True(RetailLevelSelectLater610.WindowLowFcompSite < RetailLevelSelectLater610.FirstConsumerSite);
        Assert.True(RetailLevelSelectLater610.FirstConsumerSite < RetailLevelSelectLater610.FirstStoreSite);
        Assert.True(RetailLevelSelectLater610.FirstStoreSite < RetailLevelSelectLater610.LaterFmul60Site);
        Assert.False(RetailLevelSelectLater610.InventsDestY5);
        Assert.False(RetailLevelSelectLater610.InventsDestX5);
        Assert.False(RetailLevelSelectLater610.InventsDestY268);
        Assert.False(RetailLevelSelectLater610.InventsDestY284);
        Assert.False(RetailLevelSelectLater610.InventsDestY304);
        Assert.False(RetailLevelSelectLater610.InventsDestFromPad);
        Assert.False(RetailLevelSelectLater610.InventsDestY15_5);
        Assert.False(RetailLevelSelectLater610.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom148);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom10);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom138);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom322);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom90);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom570);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom075);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom4);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom1);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom255);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom0);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater610.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater610.InventsKerningHack);
        Assert.False(RetailLevelSelectLater610.InventsSheen);
        Assert.False(RetailLevelSelectLater610.InventsWrapWidth);
        Assert.False(RetailLevelSelectLater610.InventsFade);
        Assert.False(RetailLevelSelectLater610.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater610.IsLater610);
        Assert.False(RetailLevelSelectLater610.IsLaterOne);
        Assert.False(RetailLevelSelectLater610.IsLaterEsp94);
        Assert.False(RetailLevelSelectLater610.IsLater148);
        Assert.False(RetailLevelSelectLater610.IsFsub148);
        Assert.False(RetailLevelSelectLater610.IsFsub10);
        Assert.False(RetailLevelSelectLater610.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLater610.IsLatchSet);
        Assert.False(RetailLevelSelectLater610.IsFmvSkip);
        Assert.False(RetailLevelSelectLater610.IsClickSound);
        Assert.False(RetailLevelSelectLater610.IsClickHit);
        Assert.False(RetailLevelSelectLater610.IsHoverHit);
        Assert.False(RetailLevelSelectLater610.IsCancel);
        Assert.False(RetailLevelSelectLater610.IsSetLanguage);
        Assert.False(RetailLevelSelectLater610.IsButtonPressed);
        Assert.False(RetailLevelSelectLater610.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectLater610.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownDest);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectLater610.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectLater610.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLater610.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLater610.RedoesFsub148);
        Assert.False(RetailLevelSelectLater610.RedoesFsub10);
        Assert.False(RetailLevelSelectLater610.RedoesLater148);
        Assert.False(RetailLevelSelectLater610.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLater610.RedoesLaterOne);
        Assert.False(RetailLevelSelectLater610.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectLater610.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectLater610.UsesLanguageCompare);
        Assert.False(RetailLevelSelectLater610.ChangesMeasureText);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.True(RetailLevelSelectLater148.IsLater148);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void AppliesIsHalfOpen610WindowAndDoesNotInventDestFrom610Or0()
    {
        Assert.Equal(610f, RetailLevelSelectLater610.WindowHigh);
        Assert.Equal(0f, RetailLevelSelectLater610.WindowLow);
        Assert.True(RetailLevelSelectLater610.Applies(0f));
        Assert.True(RetailLevelSelectLater610.Applies(148f));
        Assert.True(RetailLevelSelectLater610.Applies(609f));
        Assert.False(RetailLevelSelectLater610.Applies(-1f));
        Assert.False(RetailLevelSelectLater610.Applies(610f));
        Assert.True(
            RetailLevelSelectLater610.Applies(
                RetailLevelSelectLater148.Pad(
                    RetailLevelSelectLater148.SettledField)));
        Assert.True(
            RetailLevelSelectLater610.Applies(
                RetailLevelSelectFsub148.SettledPad));
        Assert.Equal(
            RetailLevelSelectFsub148.WindowHigh,
            RetailLevelSelectLater610.WindowHigh);
        Assert.Equal(
            RetailLevelSelectFsub148.WindowLow,
            RetailLevelSelectLater610.WindowLow);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom0);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom1);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom255);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom075);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom4);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom90);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom570);
        Assert.False(RetailLevelSelectLater610.InventsDestFrom148);
        Assert.False(RetailLevelSelectLater610.InventsDestY15_5);
        Assert.False(RetailLevelSelectLater610.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater610.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater610.InventsFade);
        Assert.False(RetailLevelSelectLater610.RedoesLaterOne);
        Assert.False(RetailLevelSelectLater610.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLater610.RedoesLater148);
        Assert.False(RetailLevelSelectLater610.RedoesFsub148);
        Assert.False(RetailLevelSelectLater610.RedoesFsub10);
        Assert.False(RetailLevelSelectLater610.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectLater610.RedoesLatchToButton);
        Assert.False(RetailLevelSelectLater610.ChangesMeasureText);
        Assert.False(RetailLevelSelectLater610.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater610.IsLater610);
        Assert.False(RetailLevelSelectLater610.IsLaterOne);
        Assert.False(RetailLevelSelectLater610.IsLaterEsp94);
        Assert.False(RetailLevelSelectLater610.IsLater148);
        Assert.False(RetailLevelSelectLater610.IsFsub148);
        Assert.False(RetailLevelSelectLater610.IsFsub10);
        Assert.False(RetailLevelSelectLater610.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectLater610.IsLatchSet);
        Assert.False(RetailLevelSelectLater610.IsFmvSkip);
        Assert.False(RetailLevelSelectLater610.IsClickHit);
        Assert.False(RetailLevelSelectLater610.IsHoverHit);
        Assert.False(RetailLevelSelectLater610.IsCancel);
        Assert.False(RetailLevelSelectLater610.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLater610AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLater610", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater610.Applies", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater610.WindowHigh", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater610.WindowLow", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("610f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("610.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLater610", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater610", cancel, StringComparison.Ordinal);
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
