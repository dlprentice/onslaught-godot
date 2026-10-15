// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> first leftover — unique
/// <c>0x00460B61</c> <c>call 0x00467200</c>
/// <c>CFrontEnd__DrawSlidingTextBordersAndMask</c> — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460B40</c> <c>sub esp, 0x80</c> (<c>81 ec 80 00 00 00</c>),
/// <c>0x00460B46</c> <c>mov eax, [esp+0x88]</c>,
/// <c>0x00460B50</c> <c>mov esi, ecx</c>,
/// <c>0x00460B53</c> <c>mov ecx, [esp+0x94]</c>,
/// <c>0x00460B5A</c> / <c>0x00460B5B</c> push dest then transition,
/// <c>0x00460B5C</c> <c>mov ecx, 0x0089D758</c>,
/// <c>0x00460B61</c> unique <c>call 0x00467200</c>
/// (<c>e8 9a 66 00 00</c>, one image hit).
/// <c>0x00460B66</c> <c>fld [0x005DB53C]</c> (148.0) is later.
/// Dest Y does not. Latch SET already owns <c>0x0042D5CF</c>.
/// FMV skip already owns the OR at <c>0x0053F2EB</c>. Dest is not
/// 15.5, 322.5, 148.0, or the 2.0 constant. DrawLevelSelect
/// consumes the leftover as the settled inside scale. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest
/// from the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack.
/// Do not change MeasureText. Do not redo dest leftovers, list
/// colour, list hover, list click, list cancel, click-hit sound,
/// latch-to-button SET, Apply pulse, dropdown cosine, language
/// pitch, or the 0x00463669 compare. Do not invent dest from 148.0.
/// Do not invent that the third FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectSlidingBordersTests
{
    [Fact]
    public void SpecimenSitesAreSlidingBordersCallNotDestColourHoverClickCancelOrLatchSet()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectSlidingBorders.RenderSite);
        Assert.Equal(0x00460B40u, RetailLevelSelectSlidingBorders.PrologueSite);
        Assert.Equal(0x80, RetailLevelSelectSlidingBorders.PrologueImmediate);
        Assert.Equal(0x00460B46u, RetailLevelSelectSlidingBorders.DestPageLoadSite);
        Assert.Equal(0x00460B50u, RetailLevelSelectSlidingBorders.ThisSaveSite);
        Assert.Equal(0x00460B53u, RetailLevelSelectSlidingBorders.TransitionLoadSite);
        Assert.Equal(0x00460B5Au, RetailLevelSelectSlidingBorders.DestPagePushSite);
        Assert.Equal(0x00460B5Bu, RetailLevelSelectSlidingBorders.TransitionPushSite);
        Assert.Equal(0x00460B5Cu, RetailLevelSelectSlidingBorders.FrontendThisLoadSite);
        Assert.Equal(0x0089D758u, RetailLevelSelectSlidingBorders.FrontendThis);
        Assert.Equal(0x00460B61u, RetailLevelSelectSlidingBorders.CallSite);
        Assert.Equal(0x00467200u, RetailLevelSelectSlidingBorders.DrawSlidingTextBordersAndMask);
        Assert.Equal(0x00460B66u, RetailLevelSelectSlidingBorders.LaterFldSite);
        Assert.Equal(0x005DB53Cu, RetailLevelSelectSlidingBorders.LaterConst);
        Assert.Equal(0x43140000u, RetailLevelSelectSlidingBorders.LaterConstBits);
        Assert.Equal(0x00460B6Cu, RetailLevelSelectSlidingBorders.LaterFsubSite);
        Assert.Equal(0x00460B72u, RetailLevelSelectSlidingBorders.LaterFstpSite);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectSlidingBorders.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectSlidingBorders.FmvOrSite);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.SurfThis,
            RetailLevelSelectSlidingBorders.FrontendThis);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectSlidingBorders.LatchSetSite);
        Assert.Equal(
            RetailFrontendLatchToButton.FmvOrSite,
            RetailLevelSelectSlidingBorders.FmvOrSite);
        Assert.NotEqual(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectSlidingBorders.LaterFldSite);
        Assert.NotEqual(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectSlidingBorders.LatchSetSite);
        Assert.NotEqual(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectSlidingBorders.FmvOrSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectSlidingBorders.CallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailLevelSelectSlidingBorders.CallSite);
        Assert.True(RetailLevelSelectSlidingBorders.PrologueSite < RetailLevelSelectSlidingBorders.DestPageLoadSite);
        Assert.True(RetailLevelSelectSlidingBorders.DestPageLoadSite < RetailLevelSelectSlidingBorders.ThisSaveSite);
        Assert.True(RetailLevelSelectSlidingBorders.ThisSaveSite < RetailLevelSelectSlidingBorders.TransitionLoadSite);
        Assert.True(RetailLevelSelectSlidingBorders.TransitionLoadSite < RetailLevelSelectSlidingBorders.DestPagePushSite);
        Assert.True(RetailLevelSelectSlidingBorders.DestPagePushSite < RetailLevelSelectSlidingBorders.TransitionPushSite);
        Assert.True(RetailLevelSelectSlidingBorders.TransitionPushSite < RetailLevelSelectSlidingBorders.FrontendThisLoadSite);
        Assert.True(RetailLevelSelectSlidingBorders.FrontendThisLoadSite < RetailLevelSelectSlidingBorders.CallSite);
        Assert.True(RetailLevelSelectSlidingBorders.CallSite < RetailLevelSelectSlidingBorders.LaterFldSite);
        Assert.True(RetailLevelSelectSlidingBorders.LaterFldSite < RetailLevelSelectSlidingBorders.LaterFsubSite);
        Assert.True(RetailLevelSelectSlidingBorders.LaterFsubSite < RetailLevelSelectSlidingBorders.LaterFstpSite);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestX5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY268);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY284);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY304);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestFromPad);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY15_5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestX322_5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestFrom148);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestImmediates);
        Assert.False(RetailLevelSelectSlidingBorders.InventsKerningHack);
        Assert.False(RetailLevelSelectSlidingBorders.InventsSheen);
        Assert.False(RetailLevelSelectSlidingBorders.InventsWrapWidth);
        Assert.False(RetailLevelSelectSlidingBorders.InventsFade);
        Assert.False(RetailLevelSelectSlidingBorders.UsesCurrentIndex);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectSlidingBorders.IsLatchSet);
        Assert.False(RetailLevelSelectSlidingBorders.IsFmvSkip);
        Assert.False(RetailLevelSelectSlidingBorders.IsClickSound);
        Assert.False(RetailLevelSelectSlidingBorders.IsClickHit);
        Assert.False(RetailLevelSelectSlidingBorders.IsHoverHit);
        Assert.False(RetailLevelSelectSlidingBorders.IsCancel);
        Assert.False(RetailLevelSelectSlidingBorders.IsSetLanguage);
        Assert.False(RetailLevelSelectSlidingBorders.IsButtonPressed);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesMenuItemDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesMenuItemIconDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownValueDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownPanelDest);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListColor);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListHover);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListClick);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListCancel);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListClickSound);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesLatchToButton);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesLanguagePitch);
        Assert.False(RetailLevelSelectSlidingBorders.UsesTwinFadeGate);
        Assert.False(RetailLevelSelectSlidingBorders.UsesLanguageCompare);
        Assert.False(RetailLevelSelectSlidingBorders.ChangesMeasureText);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void SettledStandardPageAppliesAndDoesNotInventDestFrom148()
    {
        Assert.True(
            RetailLevelSelectSlidingBorders.Applies(
                standardPage: true,
                fromVirtualKeyboard: false));
        Assert.False(
            RetailLevelSelectSlidingBorders.Applies(
                standardPage: false,
                fromVirtualKeyboard: false));
        Assert.False(
            RetailLevelSelectSlidingBorders.Applies(
                standardPage: true,
                fromVirtualKeyboard: true));
        Assert.Equal(1.25f, RetailLevelSelectSlidingBorders.SettledInsideScale);
        Assert.Equal(148f, RetailLevelSelectSlidingBorders.LaterConstValue);
        Assert.NotEqual(
            RetailLevelSelectSlidingBorders.SettledInsideScale,
            RetailLevelSelectSlidingBorders.LaterConstValue);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestFrom148);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestY15_5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestX322_5);
        Assert.False(RetailLevelSelectSlidingBorders.InventsDestImmediates);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesDropdownListDestY);
        Assert.False(RetailLevelSelectSlidingBorders.RedoesLatchToButton);
        Assert.False(RetailLevelSelectSlidingBorders.ChangesMeasureText);
        Assert.False(RetailLevelSelectSlidingBorders.UsesCurrentIndex);
        Assert.True(RetailLevelSelectSlidingBorders.IsSlidingBordersCall);
        Assert.False(RetailLevelSelectSlidingBorders.IsLatchSet);
        Assert.False(RetailLevelSelectSlidingBorders.IsFmvSkip);
        Assert.False(RetailLevelSelectSlidingBorders.IsClickHit);
        Assert.False(RetailLevelSelectSlidingBorders.IsHoverHit);
        Assert.False(RetailLevelSelectSlidingBorders.IsCancel);
        Assert.False(RetailLevelSelectSlidingBorders.IsClickSound);
    }

    [Fact]
    public void DrawLevelSelectConsumesSlidingBordersAndDoesNotPileIntoMainMenuOrOptions()
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

        Assert.Contains("RetailLevelSelectSlidingBorders", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectSlidingBorders.Applies", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectSlidingBorders.SettledInsideScale", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectSlidingBorders", cancel, StringComparison.Ordinal);
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
