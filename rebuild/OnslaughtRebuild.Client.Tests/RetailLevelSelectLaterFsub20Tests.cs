// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later second 20.0 addend — <c>0x00461008</c>
/// <c>fld [esp+0x20]</c> / <c>0x0046100C</c>
/// <c>fsub [0x005D857C]</c> (20.0) / first store
/// <c>0x00461013</c> <c>fstp [esp]</c> — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>
/// (PE32 magic <c>0x010B</c>). File offset = VA − <c>0x400000</c>.
/// Twin <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>
/// is the same size and hash. <c>FEPLevelSelect</c> is absent from
/// the pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460FFE</c> is already the later second 20.0 add
/// <c>fadd [0x005D857C]</c> (<c>d8 05 7c 85 5d 00</c>).
/// <c>0x00461005</c> is already that leftover's first store
/// <c>fstp [esp]</c> (<c>d9 1c 24</c>).
/// <c>0x00461008</c> is <c>fld [esp+0x20]</c>
/// (<c>d9 44 24 20</c>). <c>0x0046100C</c> is
/// <c>fsub [0x005D857C]</c> (<c>d8 25 7c 85 5d 00</c>).
/// <c>0x005D857C</c> is <c>00 00 a0 41</c> (20.0, bits
/// <c>0x41A00000</c>). First store is <c>0x00461013</c>
/// <c>fstp [esp]</c> (<c>d9 1c 24</c>) after
/// <c>push ecx</c> at <c>0x00461012</c>. That is a
/// subtrahend on a later <c>[esp+0x20]</c> local, not dest.
/// The later second <c>fsub [0x005D857C]</c> at
/// <c>0x0046101A</c> is later. The later second 20.0 addend
/// at <c>0x00460FFE</c> is already owned. The later first
/// 20.0 addend at <c>0x00460FF0</c> is already owned. The
/// later 1.0 compare at <c>0x00460FD8</c> is already owned.
/// The later 60.0 / 0.5 / 320.0 scale at <c>0x00460F73</c>
/// is already owned. Dest Y does not. Dest is not 15.5,
/// 322.5, 148.0, 10.0, 138.0, 322.0, 610.0, 90.0, 570.0,
/// 0.75, 4.0, 1.0, 255.0, 0.0, 60.0, 0.5, 320.0, 20.0, or
/// the 2.0 constant. DrawLevelSelect consumes the leftover
/// as the later −20.0 subtrahend, not dest. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px
/// kerning hack. Do not change MeasureText. Do not redo dest
/// leftovers, list colour, list hover, list click, list
/// cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// the later 1.0 fcom, the later 610.0 / 0.0 pair, the later
/// 60.0 scale, the later [esp+0x94]/fcomp 1.0 compare, the
/// later first 20.0 addend, the later second 20.0 addend,
/// Apply pulse, dropdown cosine, language pitch, or the
/// 0x00463669 compare. Do not invent dest from 20.0. Do not
/// invent dest from 1.0. Do not invent a fade.</para>
/// </summary>
public sealed class RetailLevelSelectLaterFsub20Tests
{
    [Fact]
    public void SpecimenSitesAreLaterFsub20SubtrahendNotDestColourHoverClickCancelOrLaterFadd20()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLaterFsub20.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLaterFsub20.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLaterFsub20.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLaterFsub20.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLaterFsub20.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLaterFsub20.LaterEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLaterFsub20.LaterOneSite);
        Assert.Equal(0x00460F30u, RetailLevelSelectLaterFsub20.Later610Site);
        Assert.Equal(0x00460F85u, RetailLevelSelectLaterFsub20.Later60FstpSite);
        Assert.Equal(0x00460FD8u, RetailLevelSelectLaterFsub20.LaterEsp94OneSite);
        Assert.Equal(0x00460FF0u, RetailLevelSelectLaterFsub20.Later20FaddSite);
        Assert.Equal(0x00460FFEu, RetailLevelSelectLaterFsub20.LaterFadd20Site);
        Assert.Equal(0x00461005u, RetailLevelSelectLaterFsub20.LaterFadd20StoreSite);
        Assert.Equal(0x00461008u, RetailLevelSelectLaterFsub20.FldSite);
        Assert.Equal(0x20, RetailLevelSelectLaterFsub20.SourceLocal);
        Assert.Equal(0x0046100Cu, RetailLevelSelectLaterFsub20.FsubSite);
        Assert.Equal(0x005D857Cu, RetailLevelSelectLaterFsub20.SubtrahendConst);
        Assert.Equal(0x41A00000u, RetailLevelSelectLaterFsub20.SubtrahendBits);
        Assert.Equal(0x00461013u, RetailLevelSelectLaterFsub20.FirstStoreSite);
        Assert.Equal(0x0046101Au, RetailLevelSelectLaterFsub20.LaterFsub20Site);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLaterFsub20.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLaterFsub20.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLaterFadd20.LaterFsub20Site,
            RetailLevelSelectLaterFsub20.FsubSite);
        Assert.Equal(
            RetailLevelSelectLaterFadd20.FaddSite,
            RetailLevelSelectLaterFsub20.LaterFadd20Site);
        Assert.Equal(
            RetailLevelSelectLaterFadd20.FirstStoreSite,
            RetailLevelSelectLaterFsub20.LaterFadd20StoreSite);
        Assert.Equal(
            RetailLevelSelectLaterFadd20.AddendConst,
            RetailLevelSelectLaterFsub20.SubtrahendConst);
        Assert.Equal(
            RetailLevelSelectLaterFadd20.AddendBits,
            RetailLevelSelectLaterFsub20.SubtrahendBits);
        Assert.Equal(
            RetailLevelSelectLater20.FaddSite,
            RetailLevelSelectLaterFsub20.Later20FaddSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94One.FldSite,
            RetailLevelSelectLaterFsub20.LaterEsp94OneSite);
        Assert.Equal(
            RetailLevelSelectLater60.FstpSite,
            RetailLevelSelectLaterFsub20.Later60FstpSite);
        Assert.Equal(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLaterFsub20.Later610Site);
        Assert.Equal(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLaterFsub20.LaterOneSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLaterFsub20.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLaterFsub20.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLaterFsub20.SlidingCallSite);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLaterFsub20.LatchSetSite);
        Assert.NotEqual(
            RetailLevelSelectLaterFsub20.FsubSite,
            RetailLevelSelectLaterFsub20.LaterFadd20Site);
        Assert.NotEqual(
            RetailLevelSelectLaterFsub20.FsubSite,
            RetailLevelSelectLaterFsub20.LaterFsub20Site);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLaterFsub20.FsubSite);
        Assert.True(RetailLevelSelectLaterFsub20.LaterFadd20Site < RetailLevelSelectLaterFsub20.LaterFadd20StoreSite);
        Assert.True(RetailLevelSelectLaterFsub20.LaterFadd20StoreSite < RetailLevelSelectLaterFsub20.FldSite);
        Assert.True(RetailLevelSelectLaterFsub20.FldSite < RetailLevelSelectLaterFsub20.FsubSite);
        Assert.True(RetailLevelSelectLaterFsub20.FsubSite < RetailLevelSelectLaterFsub20.FirstStoreSite);
        Assert.True(RetailLevelSelectLaterFsub20.FirstStoreSite < RetailLevelSelectLaterFsub20.LaterFsub20Site);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom20);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom60);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom320);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterFsub20.InventsFade);
        Assert.True(RetailLevelSelectLaterFsub20.IsLaterFsub20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLaterFadd20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLater20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLaterFsub20.IsLater60);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLaterFadd20);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLater20);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLaterEsp94One);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLater60);
        Assert.False(RetailLevelSelectLaterFsub20.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterFsub20.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterFadd20.IsLaterFadd20);
        Assert.True(RetailLevelSelectLater20.IsLater20);
        Assert.True(RetailLevelSelectLaterEsp94One.IsLaterEsp94One);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void OffsetIsLocalMinus20AndDoesNotInventDestFrom20()
    {
        Assert.Equal(20f, RetailLevelSelectLaterFsub20.Subtrahend);
        Assert.Equal(-20f, RetailLevelSelectLaterFsub20.Offset(0f));
        Assert.Equal(300f, RetailLevelSelectLaterFsub20.Offset(320f));
        Assert.Equal(320f, RetailLevelSelectLaterFsub20.Offset(340f));
        Assert.Equal(
            RetailLevelSelectLaterFadd20.Addend,
            RetailLevelSelectLaterFsub20.Subtrahend);
        Assert.Equal(
            320f,
            RetailLevelSelectLaterFsub20.Offset(
                RetailLevelSelectLaterFadd20.Offset(320f)));
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom20);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom1);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom60);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom320);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom255);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom075);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom4);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom610);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFrom0);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestFromPad);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestY15_5);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestX322_5);
        Assert.False(RetailLevelSelectLaterFsub20.InventsDestImmediates);
        Assert.False(RetailLevelSelectLaterFsub20.InventsFade);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLaterFadd20);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLater20);
        Assert.False(RetailLevelSelectLaterFsub20.RedoesLaterEsp94One);
        Assert.False(RetailLevelSelectLaterFsub20.ChangesMeasureText);
        Assert.False(RetailLevelSelectLaterFsub20.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLaterFsub20.IsLaterFsub20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLaterFadd20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLater20);
        Assert.False(RetailLevelSelectLaterFsub20.IsLaterEsp94One);
        Assert.False(RetailLevelSelectLaterFsub20.IsClickHit);
        Assert.False(RetailLevelSelectLaterFsub20.IsHoverHit);
        Assert.False(RetailLevelSelectLaterFsub20.IsCancel);
        Assert.False(RetailLevelSelectLaterFsub20.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesLaterFsub20AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectLaterFsub20", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterFsub20.Offset", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLaterFsub20.Subtrahend", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("20.0f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLaterFsub20", cancel, StringComparison.Ordinal);
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
