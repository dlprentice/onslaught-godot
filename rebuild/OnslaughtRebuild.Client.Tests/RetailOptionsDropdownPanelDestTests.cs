// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded panel dest leftover
/// at <c>0x004A3F36</c> / <c>0x004A3F35</c> and the width leftover
/// <c>add ebp, 3</c> at <c>0x004A3EF4</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3D38</c> <c>fadd [0x005D8BA0]</c>,
/// <c>0x004A3D3E</c> <c>fstp [esp+0x18]</c> collapsed dest leftover,
/// <c>0x005D8BA0</c> is <c>00 00 00 40</c> (2.0),
/// <c>0x004A3DA8</c> <c>mov al, [esi+0x24]</c>,
/// <c>0x004A3DAD</c> <c>je 0x004A409B</c>,
/// <c>0x004A3DB5</c> <c>push ebp</c>,
/// <c>0x004A3E01</c> <c>dec eax</c>,
/// <c>0x004A3E02</c> <c>imul [esp+0x24]</c> label SIZE.cy leftover,
/// <c>0x004A3E07</c> <c>cdq</c> / <c>0x004A3E0A</c> <c>sar eax, 1</c>,
/// <c>0x004A3E14</c> <c>fsubr [esp+0x110]</c> incoming dest Y,
/// <c>0x005D856C</c> is 0.0 and <c>0x005DB34C</c> is 480.0,
/// <c>0x004A3EF4</c> <c>add ebp, 3</c>,
/// <c>0x004A3F00</c> <c>mov ebp, [esp+0x24]</c> dest Y leftover,
/// <c>0x004A3F16</c> <c>mov ecx, [esp+0x34]</c> dest X leftover,
/// <c>0x004A3F35</c> <c>push ebp</c>,
/// <c>0x004A3F36</c> <c>push ecx</c>,
/// <c>0x004A3F37</c> <c>call 0x00555BE0</c>,
/// <c>0x004A3F40</c> <c>add esp, 0x3C</c>.
/// Dest X is the collapsed dest leftover, not the 2.0 constant.
/// Dest Y is incoming dest Y minus integer-half of (count-1)*cy.
/// Width is max SIZE.cx plus 3. Dest is not 322.5, 15.5, 3, or 2.
/// DrawOptionDropdown consumes DestX, DestY, and Width. Do not invent
/// dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest from
/// the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack. Do not
/// change MeasureText. Do not redo the expanded list dest, collapsed
/// value dest, label dest, icon dest, CMenuItem dest, colour AND,
/// Apply pulse, dropdown cosine, language pitch, or the 0x00463669
/// compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownPanelDestTests
{
    [Fact]
    public void SpecimenSitesAreExpandedPanelDestNotDestImmediates()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownPanelDest.RenderSite);
        Assert.Equal(0x004A3E01u, RetailOptionsDropdownPanelDest.CountDecSite);
        Assert.Equal(0x004A3E02u, RetailOptionsDropdownPanelDest.PitchImulSite);
        Assert.Equal(0x004A3E07u, RetailOptionsDropdownPanelDest.CdqSite);
        Assert.Equal(0x004A3E0Au, RetailOptionsDropdownPanelDest.HalfSarSite);
        Assert.Equal(0x004A3E14u, RetailOptionsDropdownPanelDest.DestYSubSite);
        Assert.Equal(0x005D856Cu, RetailOptionsDropdownPanelDest.ClampMinGlobal);
        Assert.Equal(0x00000000u, RetailOptionsDropdownPanelDest.ClampMinBits);
        Assert.Equal(0x005DB34Cu, RetailOptionsDropdownPanelDest.ClampMaxGlobal);
        Assert.Equal(0x43F00000u, RetailOptionsDropdownPanelDest.ClampMaxBits);
        Assert.Equal(0x004A3EF4u, RetailOptionsDropdownPanelDest.WidthAddSite);
        Assert.Equal(3, RetailOptionsDropdownPanelDest.WidthPad);
        Assert.Equal(0x004A3EFCu, RetailOptionsDropdownPanelDest.WidthStoreSite);
        Assert.Equal(0x004A3F00u, RetailOptionsDropdownPanelDest.DestYLoadSite);
        Assert.Equal(0x004A3F16u, RetailOptionsDropdownPanelDest.DestXLoadSite);
        Assert.Equal(0x004A3F35u, RetailOptionsDropdownPanelDest.DestYPushSite);
        Assert.Equal(0x004A3F36u, RetailOptionsDropdownPanelDest.DestXPushSite);
        Assert.Equal(0x004A3F37u, RetailOptionsDropdownPanelDest.DrawSpriteCallSite);
        Assert.Equal(0x00555BE0u, RetailOptionsDropdownPanelDest.DrawSpriteEx);
        Assert.Equal(0x3Cu, RetailOptionsDropdownPanelDest.DrawSpritePop);
        Assert.Equal(0x005D8BA0u, RetailOptionsDropdownPanelDest.PadGlobal);
        Assert.Equal(0x40000000u, RetailOptionsDropdownPanelDest.PadBits);
        Assert.Equal(0x24u, RetailOptionsDropdownPanelDest.ExpandByteOffset);
        Assert.Equal(0x004A3DA8u, RetailOptionsDropdownPanelDest.ExpandTestSite);
        Assert.Equal(0x004A3DADu, RetailOptionsDropdownPanelDest.CollapseJumpSite);
        Assert.Equal(0x004A409Bu, RetailOptionsDropdownPanelDest.CollapseTarget);
        Assert.Equal(0x3B83126Fu, RetailOptionsDropdownPanelDest.ZBits);
        Assert.Equal(
            RetailOptionsDropdownValueDest.RenderSite,
            RetailOptionsDropdownPanelDest.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownValueDest.PadGlobal,
            RetailOptionsDropdownPanelDest.PadGlobal);
        Assert.Equal(
            RetailOptionsDropdownValueDest.DestX(319f),
            RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsDropdownValueDest.DestLoadSite,
            RetailOptionsDropdownPanelDest.DestXLoadSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestLoadSite,
            RetailOptionsDropdownPanelDest.DestXLoadSite);
        Assert.True(RetailOptionsDropdownPanelDest.ExpandTestSite < RetailOptionsDropdownPanelDest.CountDecSite);
        Assert.True(RetailOptionsDropdownPanelDest.CountDecSite < RetailOptionsDropdownPanelDest.DestYSubSite);
        Assert.True(RetailOptionsDropdownPanelDest.DestYSubSite < RetailOptionsDropdownPanelDest.WidthAddSite);
        Assert.True(RetailOptionsDropdownPanelDest.WidthAddSite < RetailOptionsDropdownPanelDest.DestYLoadSite);
        Assert.True(RetailOptionsDropdownPanelDest.DestYLoadSite < RetailOptionsDropdownPanelDest.DestXLoadSite);
        Assert.True(RetailOptionsDropdownPanelDest.DestXLoadSite < RetailOptionsDropdownPanelDest.DestYPushSite);
        Assert.True(RetailOptionsDropdownPanelDest.DestYPushSite < RetailOptionsDropdownPanelDest.DestXPushSite);
        Assert.True(RetailOptionsDropdownPanelDest.DestXPushSite < RetailOptionsDropdownPanelDest.DrawSpriteCallSite);
        Assert.True(RetailOptionsDropdownPanelDest.DrawSpriteCallSite < RetailOptionsDropdownListDest.DestLoadSite);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY5);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestX5);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY268);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY284);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY304);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownPanelDest.InventsKerningHack);
        Assert.False(RetailOptionsDropdownPanelDest.InventsSheen);
        Assert.False(RetailOptionsDropdownPanelDest.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownPanelDest.InventsFade);
        Assert.False(RetailOptionsDropdownPanelDest.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownPanelDest.IsSetLanguage);
        Assert.False(RetailOptionsDropdownPanelDest.IsButtonPressed);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownPanelDest.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownPanelDest.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownPanelDest.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownPanelDest.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownPanelDest.UsesIntegerHalfOfWidth);
    }

    [Fact]
    public void PanelDestIsCollapsedLeftoverPlusWidthPadAndCenteredY()
    {
        Assert.Equal(2f, RetailOptionsDropdownPanelDest.Pad);
        Assert.Equal(3, RetailOptionsDropdownPanelDest.WidthPad);
        Assert.Equal(0f, RetailOptionsDropdownPanelDest.ClampMin);
        Assert.Equal(480f, RetailOptionsDropdownPanelDest.ClampMax);
        Assert.Equal(321f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.Equal(322f, RetailOptionsDropdownPanelDest.DestX(320f));
        Assert.Equal(2f, RetailOptionsDropdownPanelDest.DestX(0f));
        Assert.Equal(203f, RetailOptionsDropdownPanelDest.Width(200));
        Assert.Equal(3f, RetailOptionsDropdownPanelDest.Width(0));
        Assert.Equal(16, RetailOptionsDropdownPanelDest.IntegerHalf(32));
        Assert.Equal(8, RetailOptionsDropdownPanelDest.IntegerHalf(16));
        Assert.Equal(0, RetailOptionsDropdownPanelDest.IntegerHalf(0));
        Assert.Equal(259f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.Equal(275f, RetailOptionsDropdownPanelDest.DestY(275f, 1, 16));
        Assert.Equal(267f, RetailOptionsDropdownPanelDest.DestY(275f, 2, 16));
        Assert.Equal(0f, RetailOptionsDropdownPanelDest.DestY(-10f, 3, 16));
        Assert.Equal(432f, RetailOptionsDropdownPanelDest.DestY(460f, 3, 16));
        Assert.Equal(
            RetailOptionsDropdownValueDest.DestX(319f),
            RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestX(319f),
            RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(2f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(3f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(5f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(322.5f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(323f, RetailOptionsDropdownPanelDest.DestX(319f));
        Assert.NotEqual(15.5f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(243.5f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(259.5f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(268f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(284f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(304f, RetailOptionsDropdownPanelDest.DestY(275f, 3, 16));
        Assert.NotEqual(480f, RetailOptionsDropdownPanelDest.DestY(460f, 3, 16));
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownPanelDest.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownPanelDest.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownPanelDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawOptionDropdownConsumesPanelDestAndDoesNotInvent322()
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

        Assert.Contains("RetailOptionsDropdownPanelDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownPanelDest.DestX", dropdown, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownPanelDest.DestY", dropdown, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownPanelDest.Width", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownPanelLeft", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownPanelPad", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownPanelTopInset", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest.Pad", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownValueDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("2f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("3f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("4f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("0.5f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("321f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("322f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("323f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("IntegerHalf", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest.DestX", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownPanelLeft", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", valueBar, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest", click, StringComparison.Ordinal);
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
