// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded list dest Y leftover
/// after <c>0x004A3F3C</c> <c>fild [esp+0x60]</c> /
/// <c>0x004A3F47</c> <c>fmul [esp+0x18]</c> /
/// <c>0x004A3F8E</c> <c>fld [esp+0x38]</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3DB5</c> <c>push ebp</c>,
/// <c>0x004A3E02</c> <c>imul [esp+0x24]</c> label SIZE.cy leftover,
/// <c>0x004A3E3D</c> <c>mov [esp+0x18], 0x3F800000</c> scale leftover,
/// <c>0x004A3E45</c> <c>fstp [esp+0x10]</c> panel dest Y leftover,
/// <c>0x004A3F00</c> <c>mov ebp, [esp+0x24]</c> aliases that dest Y
/// after the DrawSpriteEx pack start,
/// <c>0x004A3F3C</c> <c>fild [esp+0x60]</c> aliases SIZE.cy after
/// the 0x3C pack,
/// <c>0x004A3F40</c> <c>add esp, 0x3C</c>,
/// <c>0x004A3F47</c> <c>fmul [esp+0x18]</c> scale leftover,
/// <c>0x004A3F4B</c> <c>fstp [esp+0x38]</c>,
/// <c>0x004A3F65</c> <c>mov ebp, [esp+0x10]</c>,
/// <c>0x004A3F8E</c> <c>fld [esp+0x38]</c>,
/// <c>0x004A3F92</c> <c>fadd [esp+0x10]</c>,
/// <c>0x004A3F9A</c> <c>fstp [esp+0x14]</c>,
/// <c>0x004A3FD1</c> <c>push ebp</c> dest Y into DrawText,
/// <c>0x004A3FE1</c> <c>call 0x004659A0</c>,
/// <c>0x004A404D</c> <c>mov [esp+0x10], ecx</c>.
/// Dest Y is the panel dest leftover plus index times (SIZE.cy times
/// scale). Dest Y does not consult currentIndex. Dest is not 15.5,
/// 322.5, 148.0, or the 2.0 constant. DrawOptionDropdown consumes
/// DestY. Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284,
/// dest Y=304, dest from the 2.0 constant, wrap, fade, sheen, or a
/// 2px kerning hack. Do not change MeasureText. Do not redo the
/// expanded list dest X, expanded panel dest, collapsed value dest,
/// label dest, icon dest, CMenuItem dest, colour AND, Apply pulse,
/// dropdown cosine, language pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownListDestYTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListDestYNotDestImmediates()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListDestY.RenderSite);
        Assert.Equal(0x004A3F3Cu, RetailOptionsDropdownListDestY.CyFildSite);
        Assert.Equal(0x004A3F40u, RetailOptionsDropdownListDestY.DrawSpritePopSite);
        Assert.Equal(0x3Cu, RetailOptionsDropdownListDestY.DrawSpritePop);
        Assert.Equal(0x004A3F47u, RetailOptionsDropdownListDestY.ScaleMulSite);
        Assert.Equal(0x004A3F4Bu, RetailOptionsDropdownListDestY.PitchStoreSite);
        Assert.Equal(0x004A3F65u, RetailOptionsDropdownListDestY.LoopDestYLoadSite);
        Assert.Equal(0x004A3F8Eu, RetailOptionsDropdownListDestY.PitchLoadSite);
        Assert.Equal(0x004A3F92u, RetailOptionsDropdownListDestY.DestYAddSite);
        Assert.Equal(0x004A3F9Au, RetailOptionsDropdownListDestY.DestYStoreSite);
        Assert.Equal(0x004A3FD1u, RetailOptionsDropdownListDestY.DestYPushSite);
        Assert.Equal(0x004A3FE1u, RetailOptionsDropdownListDestY.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsDropdownListDestY.DrawText);
        Assert.Equal(0x004A404Du, RetailOptionsDropdownListDestY.AdvanceStoreSite);
        Assert.Equal(0x3F800000u, RetailOptionsDropdownListDestY.ScaleBits);
        Assert.Equal(0x20u, RetailOptionsDropdownListDestY.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListDest.RenderSite,
            RetailOptionsDropdownListDestY.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownPanelDest.DrawSpritePop,
            RetailOptionsDropdownListDestY.DrawSpritePop);
        Assert.Equal(
            RetailOptionsDropdownListDest.DrawText,
            RetailOptionsDropdownListDestY.DrawText);
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestLoadSite,
            RetailOptionsDropdownListDestY.DestYPushSite);
        Assert.NotEqual(
            RetailOptionsDropdownPanelDest.DestYPushSite,
            RetailOptionsDropdownListDestY.DestYPushSite);
        Assert.True(RetailOptionsDropdownListDestY.CyFildSite < RetailOptionsDropdownListDestY.ScaleMulSite);
        Assert.True(RetailOptionsDropdownListDestY.ScaleMulSite < RetailOptionsDropdownListDestY.PitchStoreSite);
        Assert.True(RetailOptionsDropdownListDestY.PitchStoreSite < RetailOptionsDropdownListDestY.LoopDestYLoadSite);
        Assert.True(RetailOptionsDropdownListDestY.LoopDestYLoadSite < RetailOptionsDropdownListDestY.PitchLoadSite);
        Assert.True(RetailOptionsDropdownListDestY.PitchLoadSite < RetailOptionsDropdownListDestY.DestYAddSite);
        Assert.True(RetailOptionsDropdownListDestY.DestYAddSite < RetailOptionsDropdownListDestY.DestYPushSite);
        Assert.True(RetailOptionsDropdownListDestY.DestYPushSite < RetailOptionsDropdownListDestY.DrawTextCallSite);
        Assert.True(RetailOptionsDropdownListDestY.DrawTextCallSite < RetailOptionsDropdownListDestY.AdvanceStoreSite);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY5);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestX5);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY268);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY284);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY304);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListDestY.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListDestY.InventsSheen);
        Assert.False(RetailOptionsDropdownListDestY.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListDestY.InventsFade);
        Assert.False(RetailOptionsDropdownListDestY.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownListDestY.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListDestY.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListDestY.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListDestY.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListDestY.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListDestY.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListDestY.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListDestY.ChangesMeasureText);
    }

    [Fact]
    public void ListDestYIsPanelDestYPlusIndexTimesCyAndDoesNotUseCurrentIndex()
    {
        Assert.Equal(1f, RetailOptionsDropdownListDestY.IdentityScale);
        Assert.Equal(1f, RetailOptionsDropdownListDestY.Scale(3, 16));
        Assert.Equal(259f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.Equal(275f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 1));
        Assert.Equal(291f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 2));
        Assert.Equal(
            RetailOptionsDropdownPanelDest.DestY(275f, 3, 16),
            RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.Equal(
            RetailOptionsDropdownPanelDest.DestY(275f, 3, 16) + 16f,
            RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 1));
        Assert.Equal(
            RetailOptionsDropdownPanelDest.DestY(0f, 1, 16),
            RetailOptionsDropdownListDestY.DestY(0f, 1, 16, 0));
        Assert.NotEqual(275f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(243f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(307f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 2));
        Assert.NotEqual(5f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(15.5f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(148f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(268f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(284f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(304f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(2f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.NotEqual(322.5f, RetailOptionsDropdownListDestY.DestY(275f, 3, 16, 0));
        Assert.False(RetailOptionsDropdownListDestY.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListDestY.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListDestY.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListDestY.ChangesMeasureText);
    }

    [Fact]
    public void DrawOptionDropdownConsumesDestYAndDoesNotInventCurrentIndexDest()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string centered = Slice(options, "private void DrawOptionTextCentered");
        string labelValue = Slice(options, "private void DrawLabelValueRow");
        string valueBar = Slice(options, "private void DrawValueBarRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");

        Assert.Contains("RetailOptionsDropdownListDestY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDestY.DestY", dropdown, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDestY.DestY", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("(i - row.CurrentIndex)", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("(i - expanded.CurrentIndex)", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY.IdentityScale", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY.Scale", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownValueDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("2f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("4f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("0.5f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("IntegerHalf", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", confirm, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListDestY", click, StringComparison.Ordinal);
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
