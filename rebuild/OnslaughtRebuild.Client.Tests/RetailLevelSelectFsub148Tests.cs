// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> leftover after the
/// sliding-borders call — <c>0x00460B66</c> <c>fld [0x005DB53C]</c>
/// (148.0) / <c>0x00460B6C</c> <c>fsub [esi+0x3460]</c> /
/// <c>0x00460B72</c> <c>fstp [esp+0x14]</c> — recovered from
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
/// <c>0x00460B66</c> <c>fld [0x005DB53C]</c>
/// (<c>d9 05 3c b5 5d 00</c>), <c>0x005DB53C</c> is
/// <c>00 00 14 43</c> (148.0), <c>0x00460B6C</c>
/// <c>fsub [esi+0x3460]</c> (<c>d8 a6 60 34 00 00</c>),
/// <c>0x00460B72</c> <c>fstp [esp+0x14]</c>
/// (<c>d9 5c 24 14</c>). First consumers are
/// <c>0x00460BE4</c> <c>fcomp [0x005DB5B0]</c> (610.0) and
/// <c>0x00460BF9</c> <c>fcomp [0x005D856C]</c> (0.0). Init
/// zeros <c>[esi+0x3468]</c> at <c>0x004603D8</c> and
/// <c>fstp [esi+0x3460]</c> at <c>0x00460464</c>. Settled
/// pad is 148.0 and sits inside the window. The later triple
/// at <c>0x00460E24</c> is later. Dest Y does not. Latch SET
/// already owns <c>0x0042D5CF</c>. FMV skip already owns the
/// OR at <c>0x0053F2EB</c>. Sliding-borders already owns
/// <c>0x00460B61</c>. Dest is not 15.5, 322.5, 148.0, or the
/// 2.0 constant. DrawLevelSelect consumes the leftover as the
/// settled node-graph window. Do not invent dest Y=5, dest X=5,
/// dest Y=268, dest Y=284, dest Y=304, dest from the 2.0
/// constant, wrap, fade, sheen, or a 2px kerning hack. Do not
/// change MeasureText. Do not redo dest leftovers, list colour,
/// list hover, list click, list cancel, click-hit sound,
/// latch-to-button SET, the sliding-borders call, Apply pulse,
/// dropdown cosine, language pitch, or the 0x00463669 compare.
/// Do not invent dest from 148.0. Do not invent that the third
/// FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectFsub148Tests
{
    [Fact]
    public void SpecimenSitesAreFsub148WindowNotDestColourHoverClickCancelOrLatchSet()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectFsub148.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectFsub148.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectFsub148.FldSite);
        Assert.Equal(0x005DB53Cu, RetailLevelSelectFsub148.OffsetConst);
        Assert.Equal(0x43140000u, RetailLevelSelectFsub148.OffsetBits);
        Assert.Equal(0x00460B6Cu, RetailLevelSelectFsub148.FsubSite);
        Assert.Equal(0x3460, RetailLevelSelectFsub148.FieldOffset);
        Assert.Equal(0x00460B72u, RetailLevelSelectFsub148.FstpSite);
        Assert.Equal(0x14, RetailLevelSelectFsub148.StackLocal);
        Assert.Equal(0x00460BE4u, RetailLevelSelectFsub148.WindowHighFldSite);
        Assert.Equal(0x005DB5B0u, RetailLevelSelectFsub148.WindowHighConst);
        Assert.Equal(0x44188000u, RetailLevelSelectFsub148.WindowHighBits);
        Assert.Equal(0x00460BF9u, RetailLevelSelectFsub148.WindowLowFldSite);
        Assert.Equal(0x005D856Cu, RetailLevelSelectFsub148.WindowLowConst);
        Assert.Equal(0x00000000u, RetailLevelSelectFsub148.WindowLowBits);
        Assert.Equal(0x00460E24u, RetailLevelSelectFsub148.LaterTripleSite);
        Assert.Equal(0x00460C90u, RetailLevelSelectFsub148.LaterTenFldSite);
        Assert.Equal(0x005D85CCu, RetailLevelSelectFsub148.LaterTenConst);
        Assert.Equal(0x41200000u, RetailLevelSelectFsub148.LaterTenBits);
        Assert.Equal(0x004603D8u, RetailLevelSelectFsub148.InitZeroSite);
        Assert.Equal(0x00460464u, RetailLevelSelectFsub148.InitFstpSite);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectFsub148.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectFsub148.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectFsub148.SlidingCallSite);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.LaterFldSite,
            RetailLevelSelectFsub148.FldSite);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.LaterConst,
            RetailLevelSelectFsub148.OffsetConst);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.LaterConstBits,
            RetailLevelSelectFsub148.OffsetBits);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.LaterFsubSite,
            RetailLevelSelectFsub148.FsubSite);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.LaterFstpSite,
            RetailLevelSelectFsub148.FstpSite);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectFsub148.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectFsub148.FmvOrSite);
        Assert.NotEqual(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectFsub148.SlidingCallSite);
        Assert.NotEqual(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectFsub148.LaterTripleSite);
        Assert.NotEqual(
            RetailLevelSelectFsub148.FldSite,
            RetailLevelSelectFsub148.LaterTenFldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectFsub148.FldSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectFsub148.FldSite);
        Assert.True(RetailLevelSelectFsub148.SlidingCallSite < RetailLevelSelectFsub148.FldSite);
        Assert.True(RetailLevelSelectFsub148.FldSite < RetailLevelSelectFsub148.FsubSite);
        Assert.True(RetailLevelSelectFsub148.FsubSite < RetailLevelSelectFsub148.FstpSite);
        Assert.True(RetailLevelSelectFsub148.FstpSite < RetailLevelSelectFsub148.WindowHighFldSite);
        Assert.True(RetailLevelSelectFsub148.WindowHighFldSite < RetailLevelSelectFsub148.WindowLowFldSite);
        Assert.True(RetailLevelSelectFsub148.WindowLowFldSite < RetailLevelSelectFsub148.LaterTenFldSite);
        Assert.True(RetailLevelSelectFsub148.LaterTenFldSite < RetailLevelSelectFsub148.LaterTripleSite);
        Assert.True(RetailLevelSelectFsub148.InitZeroSite < RetailLevelSelectFsub148.InitFstpSite);
        Assert.True(RetailLevelSelectFsub148.InitFstpSite < RetailLevelSelectFsub148.RenderSite);
        Assert.False(RetailLevelSelectFsub148.InventsDestY5);
        Assert.False(RetailLevelSelectFsub148.InventsDestX5);
        Assert.False(RetailLevelSelectFsub148.InventsDestY268);
        Assert.False(RetailLevelSelectFsub148.InventsDestY284);
        Assert.False(RetailLevelSelectFsub148.InventsDestY304);
        Assert.False(RetailLevelSelectFsub148.InventsDestFromPad);
        Assert.False(RetailLevelSelectFsub148.InventsDestY15_5);
        Assert.False(RetailLevelSelectFsub148.InventsDestX322_5);
        Assert.False(RetailLevelSelectFsub148.InventsDestFrom148);
        Assert.False(RetailLevelSelectFsub148.InventsDestImmediates);
        Assert.False(RetailLevelSelectFsub148.InventsKerningHack);
        Assert.False(RetailLevelSelectFsub148.InventsSheen);
        Assert.False(RetailLevelSelectFsub148.InventsWrapWidth);
        Assert.False(RetailLevelSelectFsub148.InventsFade);
        Assert.False(RetailLevelSelectFsub148.UsesCurrentIndex);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.False(RetailLevelSelectFsub148.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectFsub148.IsLatchSet);
        Assert.False(RetailLevelSelectFsub148.IsFmvSkip);
        Assert.False(RetailLevelSelectFsub148.IsClickSound);
        Assert.False(RetailLevelSelectFsub148.IsClickHit);
        Assert.False(RetailLevelSelectFsub148.IsHoverHit);
        Assert.False(RetailLevelSelectFsub148.IsCancel);
        Assert.False(RetailLevelSelectFsub148.IsSetLanguage);
        Assert.False(RetailLevelSelectFsub148.IsButtonPressed);
        Assert.False(RetailLevelSelectFsub148.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectFsub148.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownDest);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectFsub148.RedoesLatchToButton);
        Assert.False(RetailLevelSelectFsub148.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectFsub148.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectFsub148.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectFsub148.UsesLanguageCompare);
        Assert.False(RetailLevelSelectFsub148.ChangesMeasureText);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void SettledPadAppliesInsideTheWindowAndDoesNotInventDestFrom148()
    {
        Assert.Equal(148f, RetailLevelSelectFsub148.Offset);
        Assert.Equal(610f, RetailLevelSelectFsub148.WindowHigh);
        Assert.Equal(0f, RetailLevelSelectFsub148.WindowLow);
        Assert.Equal(0f, RetailLevelSelectFsub148.SettledField);
        Assert.Equal(148f, RetailLevelSelectFsub148.SettledPad);
        Assert.Equal(148f, RetailLevelSelectFsub148.Pad(0f));
        Assert.Equal(0f, RetailLevelSelectFsub148.Pad(148f));
        Assert.True(RetailLevelSelectFsub148.Applies(RetailLevelSelectFsub148.SettledPad));
        Assert.True(RetailLevelSelectFsub148.Applies(RetailLevelSelectFsub148.Pad(0f)));
        Assert.True(RetailLevelSelectFsub148.Applies(0f));
        Assert.True(RetailLevelSelectFsub148.Applies(609f));
        Assert.False(RetailLevelSelectFsub148.Applies(-1f));
        Assert.False(RetailLevelSelectFsub148.Applies(610f));
        Assert.NotEqual(
            RetailLevelSelectSlidingBorders.SettledInsideScale,
            RetailLevelSelectFsub148.Offset);
        Assert.False(RetailLevelSelectFsub148.InventsDestFrom148);
        Assert.False(RetailLevelSelectFsub148.InventsDestY15_5);
        Assert.False(RetailLevelSelectFsub148.InventsDestX322_5);
        Assert.False(RetailLevelSelectFsub148.InventsDestImmediates);
        Assert.False(RetailLevelSelectFsub148.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectFsub148.RedoesLatchToButton);
        Assert.False(RetailLevelSelectFsub148.RedoesSlidingBorders);
        Assert.False(RetailLevelSelectFsub148.ChangesMeasureText);
        Assert.False(RetailLevelSelectFsub148.UsesCurrentIndex);
        Assert.True(RetailLevelSelectFsub148.IsFsub148);
        Assert.False(RetailLevelSelectFsub148.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectFsub148.IsLatchSet);
        Assert.False(RetailLevelSelectFsub148.IsFmvSkip);
        Assert.False(RetailLevelSelectFsub148.IsClickHit);
        Assert.False(RetailLevelSelectFsub148.IsHoverHit);
        Assert.False(RetailLevelSelectFsub148.IsCancel);
        Assert.False(RetailLevelSelectFsub148.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesFsub148AndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectFsub148", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectFsub148.Applies", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectFsub148.Pad", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectFsub148.SettledField", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectFsub148", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectFsub148", cancel, StringComparison.Ordinal);
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
