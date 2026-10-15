// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later 60.0 / 0.5 / 320.0 scale — <c>0x00460FD8</c>
/// <c>fld [esp+0x94]</c> / <c>0x00460FDF</c>
/// <c>fcomp [0x005D8568]</c> (1.0) / <c>0x00460FE5</c>
/// <c>fnstsw ax</c> / <c>test ah, 0x40</c> / <c>jz</c> /
/// first equal-1 consumer <c>0x00460FEC</c>
/// <c>fld [esp+0x18]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460F85</c> is already the later-60 first store
/// <c>fstp [esp+0x18]</c> (<c>d9 5c 24 18</c>).
/// <c>0x00460FD2</c> is <c>jz 0x00461069</c>
/// (<c>0f 84 91 00 00 00</c>). <c>0x00460FD8</c>
/// <c>fld [esp+0x94]</c> (<c>d9 84 24 94 00 00 00</c>),
/// not <c>0x00460FD6</c>. <c>0x00460FDF</c>
/// <c>fcomp [0x005D8568]</c> (<c>d8 1d 68 85 5d 00</c>),
/// <c>0x005D8568</c> is <c>00 00 80 3f</c> (1.0, bits
/// <c>0x3F800000</c>), <c>0x00460FE5</c> <c>fnstsw ax</c>
/// / <c>test ah, 0x40</c> / <c>jz 0x00461069</c>
/// (<c>df e0 f6 c4 40 74 7d</c>). First consumer of the
/// equal-1 fall-through is <c>0x00460FEC</c>
/// <c>fld [esp+0x18]</c> (<c>d9 44 24 18</c>). That is
/// not dest. The later <c>fadd [0x005D857C]</c> (20.0) at
/// <c>0x00460FF0</c> is later. The later 60.0 / 0.5 /
/// 320.0 scale at <c>0x00460F73</c> is already owned. The
/// later 610.0 / 0.0 pair at <c>0x00460F30</c> is already
/// owned. The later 1.0 fcom at <c>0x00460E62</c> is
/// already owned. The later <c>[esp+0x94]</c> shift at
/// <c>0x00460E34</c> is already owned. The later 148.0
/// triple at <c>0x00460E24</c> is already owned. The 10.0
/// fsub at <c>0x00460C94</c> is already owned. The 148.0 fsub
/// at <c>0x00460B66</c> is already owned. Sliding-borders
/// already owns <c>0x00460B61</c>. Latch SET already owns
/// <c>0x0042D5CF</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. Dest Y does not. Dest is not 15.5,
/// 322.5, 148.0, 10.0, 138.0, 322.0, 610.0, 90.0, 570.0,
/// 0.75, 4.0, 1.0, 255.0, 0.0, 60.0, 0.5, 320.0, 20.0, or
/// the 2.0 constant. DrawLevelSelect consumes the leftover
/// as the later equal-1 compare, not dest. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px
/// kerning hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list
/// cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// the later 1.0 fcom, the later 610.0 / 0.0 pair, the later
/// 60.0 scale, Apply pulse, dropdown cosine, language pitch,
/// or the 0x00463669 compare. Do not invent dest from 1.0.
/// Do not invent dest from 20.0. Do not invent dest from
/// 60.0. Do not invent dest from 0.5. Do not invent dest
/// from 320.0. Do not invent a fade. Do not invent that the
/// third FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLaterEsp94OneTests
{
    [Fact]
    public void SpecimenSitesAreLaterEsp94OneCompareNotDestColourHoverClickCancelOrLater60()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLaterEsp94One.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLaterEsp94One.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLaterEsp94One.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLaterEsp94One.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLaterEsp94One.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLaterEsp94One.LaterEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLaterEsp94One.LaterOneSite);
        Assert.Equal(0x00460F30u, RetailLevelSelectLaterEsp94One.Later610Site);
        Assert.Equal(0x00460F85u, RetailLevelSelectLaterEsp94One.Later60FstpSite);
        Assert.Equal(0x00460FD2u, RetailLevelSelectLaterEsp94One.PriorJzSite);
        Assert.Equal(0x00460FD8u, RetailLevelSelectLaterEsp94One.FldSite);
        Assert.Equal(0x94, RetailLevelSelectLaterEsp94One.StackLocal);
        Assert.Equal(0x00460FDFu, RetailLevelSelectLaterEsp94One.FcompSite);
        Assert.Equal(0x005D8568u, RetailLevelSelectLaterEsp94One.OneConst);
        Assert.Equal(0x3F800000u, RetailLevelSelectLaterEsp94One.OneBits);
        Assert.Equal(0x00460FECu, RetailLevelSelectLaterEsp94One.FirstConsumerSite);
        Assert.Equal(0x18, RetailLevelSelectLaterEsp94One.ConsumerLocal);
        Assert.Equal(0x00460FF0u, RetailLevelSelectLaterEsp94One.LaterFadd20Site);
        Assert.Equal(0x005D857Cu, RetailLevelSelectLaterEsp94One.Later20Const);
        Assert.Equal(0x41A00000u, RetailLevelSelectLaterEsp94One.Later20Bits);
        Assert.Equal(0x00461069u, RetailLevelSelectLaterEsp94One.NotEqualSite);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLaterEsp94One.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLaterEsp94One.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLater60.LaterEsp94OneSite,
            RetailLevelSelectLaterEsp94One.FldSite);
        Assert.Equal(
            RetailLevelSelectLater60.LaterEsp18Site,
            RetailLevelSelectLaterEsp94One.FirstConsumerSite);
        Assert.Equal(
            RetailLevelSelectLater60.FstpSite,
            RetailLevelSelectLaterEsp94One.Later60FstpSite);
        Assert.Equal(
            RetailLevelSelectLater60.StoreLocal,
            RetailLevelSelectLaterEsp94One.ConsumerLocal);
        Assert.Equal(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLaterEsp94One.Later610Site);
        Assert.Equal(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterEsp94One.LaterOneSite);
        Assert.Equal(
            RetailLevelSelectLaterOne.OneConst,
            RetailLevelSelectLaterEsp94One.OneConst);
        Assert.Equal(
            RetailLevelSelectLaterOne.OneBits,
            RetailLevelSelectLaterEsp94One.OneBits);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterEsp94One.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.StackLocal,
            RetailLevelSelectLaterEsp94One.StackLocal);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLaterEsp94One.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLaterEsp94One.SlidingCallSite);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLaterEsp94One.LatchSetSite);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94One.FldSite,
            RetailLevelSelectLaterEsp94One.LaterEsp94Site);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94One.FldSite,
            RetailLevelSelectLaterEsp94One.LaterOneSite);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94One.FldSite,
            RetailLevelSelectLaterEsp94One.Later60FstpSite);
        Assert.NotEqual(
            RetailLevelSelectLaterEsp94One.FldSite,
            0x00460FD6u);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLaterEsp94One.FldSite);
        Assert.True(RetailLevelSelectLaterEsp94One.Later60FstpSite < RetailLevelSelectLaterEsp94One.PriorJzSite);
        Assert.True(RetailLevelSelectLaterEsp94One.PriorJzSite < RetailLevelSelectLaterEsp94One.FldSite);
        Assert.True(RetailLevelSelectLaterEsp94One.FldSite < RetailLevelSelectLaterEsp94One.FcompSite);
        Assert.True(RetailLevelSelectLaterEsp94One.FcompSite < RetailLevelSelectLaterEsp94One.FirstConsumerSite);
        Assert.True(RetailLevelSelectLaterEsp94One.FirstConsumerSite < RetailLevelSelectLaterEsp94One.LaterFadd20Site);
        Assert.True(RetailLevelSelectLaterEsp94One.LaterFadd20Site < RetailLevelSelectLaterEsp94One.NotEqualSite);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom20);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom60);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom320);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsFade);
        Assert.True(RetailLevelSelectLaterEsp94One.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLater60);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLater610);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLaterOne);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLater60);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLater610);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLaterOne);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94One.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterEsp94One.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater60.IsLater60);
        Assert.True(RetailLevelSelectLater610.IsLater610);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void AppliesIsEqualOneCompareAndDoesNotInventDestFrom1Or20()
    {
        Assert.Equal(1f, RetailLevelSelectLaterEsp94One.CompareOne);
        Assert.Equal(20f, RetailLevelSelectLaterEsp94One.Later20);
        Assert.True(RetailLevelSelectLaterEsp94One.Applies(1f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(0f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(0.75f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(0.999f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(1.001f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(20f));
        Assert.False(RetailLevelSelectLaterEsp94One.Applies(60f));
        Assert.True(
            RetailLevelSelectLaterEsp94One.Applies(
                RetailLevelSelectLaterEsp94One.CompareOne));
        Assert.Equal(
            RetailLevelSelectLaterOne.CompareOne,
            RetailLevelSelectLaterEsp94One.CompareOne);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom20);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom60);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom320);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom610);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFrom0);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestFromPad);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterEsp94One.InventsFade);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLater60);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLater610);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLaterOne);
        Assert.False(RetailLevelSelectLaterEsp94One.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94One.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterEsp94One.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterEsp94One.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLater60);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLater610);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLaterOne);
        Assert.False(RetailLevelSelectLaterEsp94One.IsLaterEsp94);
        Assert.False(RetailLevelSelectLaterEsp94One.IsClickHit);
        Assert.False(RetailLevelSelectLaterEsp94One.IsHoverHit);
        Assert.False(RetailLevelSelectLaterEsp94One.IsCancel);
        Assert.False(RetailLevelSelectLaterEsp94One.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLaterEsp94OneAndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLaterEsp94One", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterEsp94One.Applies", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterEsp94One.CompareOne", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterEsp94One", cancel, StringComparison.Ordinal);
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
