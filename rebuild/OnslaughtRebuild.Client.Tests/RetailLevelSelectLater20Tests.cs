// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later <c>[esp+0x94]</c> / fcomp 1.0 compare —
/// <c>0x00460FEC</c> <c>fld [esp+0x18]</c> /
/// <c>0x00460FF0</c> <c>fadd [0x005D857C]</c> (20.0) /
/// first store <c>0x00460FF7</c> <c>fstp [esp]</c> — recovered
/// from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>
/// (PE32 magic <c>0x010B</c>). File offset = VA − <c>0x400000</c>.
/// Twin <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
/// is the same size and hash. <c>FEPLevelSelect</c> is absent from
/// the pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460FEC</c> is already the later-esp94-one first consumer
/// <c>fld [esp+0x18]</c> (<c>d9 44 24 18</c>).
/// <c>0x00460FF0</c> is <c>fadd [0x005D857C]</c>
/// (<c>d8 05 7c 85 5d 00</c>). <c>0x005D857C</c> is
/// <c>00 00 a0 41</c> (20.0, bits <c>0x41A00000</c>).
/// First store is <c>0x00460FF7</c> <c>fstp [esp]</c>
/// (<c>d9 1c 24</c>) after <c>push ecx</c> at <c>0x00460FF6</c>.
/// That is an addend on the later-60 <c>[esp+0x18]</c> local,
/// not dest. The later second <c>fadd [0x005D857C]</c> at
/// <c>0x00460FFE</c> is later. The later <c>fsub [0x005D857C]</c>
/// at <c>0x0046100C</c> is later. The later 1.0 compare at
/// <c>0x00460FD8</c> is already owned. The later 60.0 / 0.5 /
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
/// as the later +20.0 addend, not dest. Do not invent dest
/// Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest
/// from the 2.0 constant, wrap, fade, sheen, or a 2px
/// kerning hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list
/// cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// the later 1.0 fcom, the later 610.0 / 0.0 pair, the later
/// 60.0 scale, the later [esp+0x94]/fcomp 1.0 compare,
/// Apply pulse, dropdown cosine, language pitch, or the
/// 0x00463669 compare. Do not invent dest from 20.0. Do not
/// invent dest from 1.0. Do not invent dest from 60.0. Do
/// not invent dest from 0.5. Do not invent dest from 320.0.
/// Do not invent a fade. Do not invent that the third FMV
/// latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLater20Tests
{
    [Fact]
    public void SpecimenSitesAreLater20AddendNotDestColourHoverClickCancelOrLaterEsp94One()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLater20.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLater20.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLater20.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLater20.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLater20.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLater20.LaterEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLater20.LaterOneSite);
        Assert.Equal(0x00460F30u, RetailLevelSelectLater20.Later610Site);
        Assert.Equal(0x00460F85u, RetailLevelSelectLater20.Later60FstpSite);
        Assert.Equal(0x00460FD8u, RetailLevelSelectLater20.LaterEsp94OneSite);
        Assert.Equal(0x00460FECu, RetailLevelSelectLater20.PriorFldSite);
        Assert.Equal(0x18, RetailLevelSelectLater20.SourceLocal);
        Assert.Equal(0x00460FF0u, RetailLevelSelectLater20.FaddSite);
        Assert.Equal(0x005D857Cu, RetailLevelSelectLater20.AddendConst);
        Assert.Equal(0x41A00000u, RetailLevelSelectLater20.AddendBits);
        Assert.Equal(0x00460FF7u, RetailLevelSelectLater20.FirstStoreSite);
        Assert.Equal(0x00460FFEu, RetailLevelSelectLater20.LaterFadd20Site);
        Assert.Equal(0x0046100Cu, RetailLevelSelectLater20.LaterFsub20Site);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLater20.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLater20.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.FirstConsumerSite,
            RetailLevelSelectLater20.PriorFldSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.LaterFadd20Site,
            RetailLevelSelectLater20.FaddSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.Later20Const,
            RetailLevelSelectLater20.AddendConst);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.Later20Bits,
            RetailLevelSelectLater20.AddendBits);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.ConsumerLocal,
            RetailLevelSelectLater20.SourceLocal);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.FldSite,
            RetailLevelSelectLater20.LaterEsp94OneSite);
        Assert.Equal(
            RetailLevelSelectLater60.FstpSite,
            RetailLevelSelectLater20.Later60FstpSite);
        Assert.Equal(
            RetailLevelSelectLater60.StoreLocal,
            RetailLevelSelectLater20.SourceLocal);
        Assert.Equal(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater20.Later610Site);
        Assert.Equal(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLater20.LaterOneSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLater20.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater20.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLater20.SlidingCallSite);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLater20.LatchSetSite);
        Assert.NotEqual(
            RetailLevelSelectLater20.FaddSite,
            RetailLevelSelectLater20.LaterEsp94OneSite);
        Assert.NotEqual(
            RetailLevelSelectLater20.FaddSite,
            RetailLevelSelectLater20.LaterFadd20Site);
        Assert.NotEqual(
            RetailLevelSelectLater20.FaddSite,
            RetailLevelSelectLater20.LaterFsub20Site);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLater20.FaddSite);
        Assert.True(RetailLevelSelectLater20.Later60FstpSite < RetailLevelSelectLater20.LaterEsp94OneSite);
        Assert.True(RetailLevelSelectLater20.LaterEsp94OneSite < RetailLevelSelectLater20.PriorFldSite);
        Assert.True(RetailLevelSelectLater20.PriorFldSite < RetailLevelSelectLater20.FaddSite);
        Assert.True(RetailLevelSelectLater20.FaddSite < RetailLevelSelectLater20.FirstStoreSite);
        Assert.True(RetailLevelSelectLater20.FirstStoreSite < RetailLevelSelectLater20.LaterFadd20Site);
        Assert.True(RetailLevelSelectLater20.LaterFadd20Site < RetailLevelSelectLater20.LaterFsub20Site);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom20);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom1);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater20.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom320);
        Assert.False(RetailLevelSelectLater20.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater20.InventsFade);
        Assert.True(RetailLevelSelectLater20.IsLater20);
        Assert.False(RetailLevelSelectLater20.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLater20.IsLater60);
        Assert.False(RetailLevelSelectLater20.IsLater610);
        Assert.False(RetailLevelSelectLater20.IsLaterOne);
        Assert.False(RetailLevelSelectLater20.IsLaterEsp94);
        Assert.False(RetailLevelSelectLater20.RedoesLaterEsp94One);
        Assert.False(RetailLevelSelectLater20.RedoesLater60);
        Assert.False(RetailLevelSelectLater20.RedoesLater610);
        Assert.False(RetailLevelSelectLater20.RedoesLaterOne);
        Assert.False(RetailLevelSelectLater20.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLater20.ChangesMeasureText);
        Assert.False(RetailLevelSelectLater20.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterEsp94One.IsLaterEsp94One);
        Assert.True(RetailLevelSelectLater60.IsLater60);
        Assert.True(RetailLevelSelectLater610.IsLater610);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.True(RetailLevelSelectLaterEsp94.IsLaterEsp94);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void OffsetIsLocalPlus20AndDoesNotInventDestFrom20()
    {
        Assert.Equal(20f, RetailLevelSelectLater20.Addend);
        Assert.Equal(20f, RetailLevelSelectLater20.Offset(0f));
        Assert.Equal(340f, RetailLevelSelectLater20.Offset(320f));
        Assert.Equal(370f, RetailLevelSelectLater20.Offset(350f));
        Assert.Equal(
            RetailLevelSelectLater60.Scaled(1) + RetailLevelSelectLater20.Addend,
            RetailLevelSelectLater20.Offset(RetailLevelSelectLater60.Scaled(1)));
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.Later20,
            RetailLevelSelectLater20.Addend);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom20);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom1);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater20.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom320);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom255);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom075);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom4);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater20.InventsDestFrom0);
        Assert.False(RetailLevelSelectLater20.InventsDestFromPad);
        Assert.False(RetailLevelSelectLater20.InventsDestY15_5);
        Assert.False(RetailLevelSelectLater20.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater20.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater20.InventsFade);
        Assert.False(RetailLevelSelectLater20.RedoesLaterEsp94One);
        Assert.False(RetailLevelSelectLater20.RedoesLater60);
        Assert.False(RetailLevelSelectLater20.RedoesLater610);
        Assert.False(RetailLevelSelectLater20.RedoesLaterOne);
        Assert.False(RetailLevelSelectLater20.RedoesLaterEsp94);
        Assert.False(RetailLevelSelectLater20.ChangesMeasureText);
        Assert.False(RetailLevelSelectLater20.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater20.IsLater20);
        Assert.False(RetailLevelSelectLater20.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLater20.IsLater60);
        Assert.False(RetailLevelSelectLater20.IsLater610);
        Assert.False(RetailLevelSelectLater20.IsClickHit);
        Assert.False(RetailLevelSelectLater20.IsHoverHit);
        Assert.False(RetailLevelSelectLater20.IsCancel);
        Assert.False(RetailLevelSelectLater20.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLater20AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLater20", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater20.Offset", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater20.Addend", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLater20", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater20", cancel, StringComparison.Ordinal);
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
