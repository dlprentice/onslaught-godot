// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> leftover after the 148.0
/// fsub window — <c>0x00460C90</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460C94</c> <c>fsub [0x005D85CC]</c> (10.0) /
/// <c>0x00460CE7</c> <c>fstp [esp]</c> — recovered from official
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
/// <c>0x00460C90</c> <c>fld [esp+0x14]</c>
/// (<c>d9 44 24 14</c>), <c>0x00460C94</c>
/// <c>fsub [0x005D85CC]</c> (<c>d8 25 cc 85 5d 00</c>),
/// <c>0x005D85CC</c> is <c>00 00 20 41</c> (10.0). The prior leftover
/// labelled the second opcode <c>fld</c>; the bytes are <c>d8 25</c>
/// (<c>fsub m32</c>), not <c>d9 05</c>. First consumer is
/// <c>0x00460CE7</c> <c>fstp [esp]</c> (<c>d9 1c 24</c>) before
/// <c>0x00460CEA</c> <c>call 0x005563D0</c>
/// (<c>CDXSurf__RenderSurface</c>). Settled pad is 148.0 so settled
/// delta is 138.0. That is not dest. The nearby <c>push 0x43A10000</c>
/// (322.0) at <c>0x00460CE1</c> is later. The later triple at
/// <c>0x00460E24</c> is later. The other
/// <c>fld [esp+0x14]</c> / <c>fsub 10.0</c> pair at
/// <c>0x004721D7</c> is later. Sliding-borders already owns
/// <c>0x00460B61</c>. 148.0 fsub already owns <c>0x00460B66</c>.
/// Latch SET already owns <c>0x0042D5CF</c>. FMV skip already owns
/// the OR at <c>0x0053F2EB</c>. Dest is not 15.5, 322.5, 148.0,
/// 10.0, 138.0, 610.0, or the 2.0 constant. DrawLevelSelect
/// consumes the leftover as pad-minus-ten, not dest. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest
/// from the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack.
/// Do not change MeasureText. Do not redo dest leftovers, list
/// colour, list hover, list click, list cancel, click-hit sound,
/// latch-to-button SET, the sliding-borders call, the 148.0 fsub,
/// Apply pulse, dropdown cosine, language pitch, or the 0x00463669
/// compare. Do not invent dest from 10.0. Do not invent that the
/// third FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectFsub10Tests
{
    [Fact]
    public void SpecimenSitesAreFsub10SlotNotDestColourHoverClickCancelOrLatchSet()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectFsub10.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectFsub10.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectFsub10.Fsub148Site);
        Assert.Equal(0x00460C90u, RetailLevelSelectFsub10.FldSite);
        Assert.Equal(0x14, RetailLevelSelectFsub10.StackLocal);
        Assert.Equal(0x00460C94u, RetailLevelSelectFsub10.FsubSite);
        Assert.Equal(0x005D85CCu, RetailLevelSelectFsub10.TenConst);
        Assert.Equal(0x41200000u, RetailLevelSelectFsub10.TenBits);
        Assert.Equal(0x00460CE7u, RetailLevelSelectFsub10.FstpSite);
        Assert.Equal(0x00460CEAu, RetailLevelSelectFsub10.CallSite);
        Assert.Equal(0x005563D0u, RetailLevelSelectFsub10.RenderSurface);
        Assert.Equal(0x00460CD5u, RetailLevelSelectFsub10.TextureLoadSite);
        Assert.Equal(0x0089D888u, RetailLevelSelectFsub10.TextureGlobal);
        Assert.Equal(0x00460CE1u, RetailLevelSelectFsub10.Later322PushSite);
        Assert.Equal(0x43A10000u, RetailLevelSelectFsub10.Later322Bits);
        Assert.Equal(0x00460CDCu, RetailLevelSelectFsub10.LaterZPushSite);
        Assert.Equal(0x3F747AE1u, RetailLevelSelectFsub10.LaterZBits);
        Assert.Equal(0x00460E24u, RetailLevelSelectFsub10.LaterTripleSite);
        Assert.Equal(0x004721D7u, RetailLevelSelectFsub10.OtherPairSite);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectFsub10.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectFsub10.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectFsub148.LaterTenFldSite,
            RetailLevelSelectFsub10.FldSite);
        Assert.Equal(
            RetailLevelSelectFsub148.LaterTenConst,
            RetailLevelSelectFsub10.TenConst);
        Assert.Equal(
            RetailLevelSelectFsub148.LaterTenBits,
            RetailLevelSelectFsub10.TenBits);
        Assert.Equal(
            RetailLevelSelectFsub148.LaterTripleSite,
            RetailLevelSelectFsub10.LaterTripleSite);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectFsub10.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectFsub10.Fsub148Site);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectFsub10.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectFsub10.FmvOrSite);
        Assert.Equal(
            RetailMainMenuWritingZ.RenderSurface,
            RetailLevelSelectFsub10.RenderSurface);
        Assert.NotEqual(
            RetailLevelSelectFsub10.FldSite,
            RetailLevelSelectFsub10.FsubSite);
        Assert.NotEqual(
            RetailLevelSelectFsub10.FldSite,
            RetailLevelSelectFsub10.Fsub148Site);
        Assert.NotEqual(
            RetailLevelSelectFsub10.FldSite,
            RetailLevelSelectFsub10.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectFsub10.FldSite,
            RetailLevelSelectFsub10.LaterTripleSite);
        Assert.NotEqual(
            RetailLevelSelectFsub10.FldSite,
            RetailLevelSelectFsub10.OtherPairSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectFsub10.FldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectFsub10.FldSite);
        Assert.True(RetailLevelSelectFsub10.SlidingCallSite < RetailLevelSelectFsub10.Fsub148Site);
        Assert.True(RetailLevelSelectFsub10.Fsub148Site < RetailLevelSelectFsub10.FldSite);
        Assert.True(RetailLevelSelectFsub10.FldSite < RetailLevelSelectFsub10.FsubSite);
        Assert.True(RetailLevelSelectFsub10.FsubSite < RetailLevelSelectFsub10.FstpSite);
        Assert.True(RetailLevelSelectFsub10.FstpSite < RetailLevelSelectFsub10.CallSite);
        Assert.True(RetailLevelSelectFsub10.CallSite < RetailLevelSelectFsub10.LaterTripleSite);
        Assert.False(RetailLevelSelectFsub10.InventsDestY5);
        Assert.False(RetailLevelSelectFsub10.InventsDestX5);
        Assert.False(RetailLevelSelectFsub10.InventsDestY268);
        Assert.False(RetailLevelSelectFsub10.InventsDestY284);
        Assert.False(RetailLevelSelectFsub10.InventsDestY304);
        Assert.False(RetailLevelSelectFsub10.InventsDestFromPad);
        Assert.False(RetailLevelSelectFsub10.InventsDestY15_5);
        Assert.False(RetailLevelSelectFsub10.InventsDestX322_5);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom148);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom10);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom138);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom322);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom610);
        Assert.False(RetailLevelSelectFsub10.InventsDestImmediates);
        Assert.False(RetailLevelSelectFsub10.InventsKerningHack);
        Assert.False(RetailLevelSelectFsub10.InventsSheen);
        Assert.False(RetailLevelSelectFsub10.InventsWrapWidth);
        Assert.False(RetailLevelSelectFsub10.InventsFade);
        Assert.False(RetailLevelSelectFsub10.UsesCurrentIndex);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.False(RetailLevelSelectFsub10.IsFld10);
        Assert.False(RetailLevelSelectFsub10.IsFsub148);
        Assert.False(RetailLevelSelectFsub10.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectFsub10.IsLatchSet);
        Assert.False(RetailLevelSelectFsub10.IsFmvSkip);
        Assert.False(RetailLevelSelectFsub10.IsClickSound);
        Assert.False(RetailLevelSelectFsub10.IsClickHit);
        Assert.False(RetailLevelSelectFsub10.IsHoverHit);
        Assert.False(RetailLevelSelectFsub10.IsCancel);
        Assert.False(RetailLevelSelectFsub10.IsSetLanguage);
        Assert.False(RetailLevelSelectFsub10.IsButtonPressed);
        Assert.False(RetailLevelSelectFsub10.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectFsub10.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownDest);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectFsub10.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectFsub10.RedoesLatchToButton);
        Assert.False(RetailLevelSelectFsub10.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectFsub10.RedoesFsub148);
        Assert.False(RetailLevelSelectFsub10.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectFsub10.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectFsub10.UsesLanguageCompare);
        Assert.False(RetailLevelSelectFsub10.ChangesMeasureText);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void SettledDeltaIsPadMinusTenAndDoesNotInventDestFrom10()
    {
        Assert.Equal(10f, RetailLevelSelectFsub10.Ten);
        Assert.Equal(148f, RetailLevelSelectFsub148.SettledPad);
        Assert.Equal(138f, RetailLevelSelectFsub10.Delta(RetailLevelSelectFsub148.SettledPad));
        Assert.Equal(138f, RetailLevelSelectFsub10.Delta(148f));
        Assert.Equal(0f, RetailLevelSelectFsub10.Delta(10f));
        Assert.Equal(322f, BitConverter.UInt32BitsToSingle(RetailLevelSelectFsub10.Later322Bits));
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom10);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom138);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom322);
        Assert.False(RetailLevelSelectFsub10.InventsDestFrom148);
        Assert.False(RetailLevelSelectFsub10.InventsDestX322_5);
        Assert.False(RetailLevelSelectFsub10.InventsDestY15_5);
        Assert.False(RetailLevelSelectFsub10.InventsDestImmediates);
        Assert.False(RetailLevelSelectFsub10.RedoesFsub148);
        Assert.False(RetailLevelSelectFsub10.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectFsub10.RedoesLatchToButton);
        Assert.False(RetailLevelSelectFsub10.ChangesMeasureText);
        Assert.False(RetailLevelSelectFsub10.UsesCurrentIndex);
        Assert.True(RetailLevelSelectFsub10.IsFsub10);
        Assert.False(RetailLevelSelectFsub10.IsFld10);
        Assert.False(RetailLevelSelectFsub10.IsFsub148);
        Assert.False(RetailLevelSelectFsub10.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectFsub10.IsLatchSet);
        Assert.False(RetailLevelSelectFsub10.IsFmvSkip);
        Assert.False(RetailLevelSelectFsub10.IsClickHit);
        Assert.False(RetailLevelSelectFsub10.IsHoverHit);
        Assert.False(RetailLevelSelectFsub10.IsCancel);
        Assert.False(RetailLevelSelectFsub10.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesFsub10AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectFsub10", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectFsub10.Delta", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectFsub148.SettledPad", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("138f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("10f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("322f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectFsub10", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub10", cancel, StringComparison.Ordinal);
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
