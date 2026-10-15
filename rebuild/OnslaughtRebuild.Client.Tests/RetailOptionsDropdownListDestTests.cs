// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded list dest
/// leftover at <c>0x004A3FCD</c> — <c>mov ebx, [esp+0x4C]</c> after
/// vtable +0x44 <c>RET 4</c>, which aliases the earlier
/// <c>fstp [esp+0x34]</c> collapsed dest leftover plus the 2.0 pad —
/// recovered from official 74154bfa
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
/// <c>0x004A3F55</c> <c>fld [esp+0x1C]</c> aliases that
/// <c>[esp+0x18]</c> after <c>push ebp</c>,
/// <c>0x004A3F59</c> <c>fadd [0x005D8BA0]</c>,
/// <c>0x004A3F5F</c> <c>fstp [esp+0x34]</c>,
/// <c>0x004A3FC8</c> <c>call [eax+0x44]</c>,
/// every independently read +0x44 ends <c>RET 4</c>
/// (<c>0x004D01B2</c>, <c>0x004CF102</c>, <c>0x004A4232</c>),
/// <c>0x004A3FCD</c> <c>mov ebx, [esp+0x4C]</c>,
/// <c>0x00515A83</c> font helper <c>RET 4</c>,
/// <c>0x00465A19</c> DrawText <c>RET 32</c>,
/// <c>0x004A3FE1</c> <c>call 0x004659A0</c>.
/// Dest X is the collapsed dest leftover plus the pad leftover.
/// Dest is not the 2.0 constant. Nearby 5.0 is leftover min dest X
/// for the label, not this dest. Dest Y keeps the entry top. Scale
/// stays 1.0. DrawOptionDropdown consumes DestX. Do not invent dest
/// Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, dest from the
/// 2.0 constant, wrap, fade, sheen, or a 2px kerning hack. Do not
/// change MeasureText. Do not redo the collapsed value dest, label
/// dest, icon dest, CMenuItem dest, colour AND, Apply pulse,
/// dropdown cosine, language pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownListDestTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListDestNotDestImmediates()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListDest.RenderSite);
        Assert.Equal(0x004A3F55u, RetailOptionsDropdownListDest.CollapsedLeftoverLoadSite);
        Assert.Equal(0x004A3F59u, RetailOptionsDropdownListDest.PadAddSite);
        Assert.Equal(0x004A3F5Fu, RetailOptionsDropdownListDest.PadStoreSite);
        Assert.Equal(0x005D8BA0u, RetailOptionsDropdownListDest.PadGlobal);
        Assert.Equal(0x40000000u, RetailOptionsDropdownListDest.PadBits);
        Assert.Equal(0x24u, RetailOptionsDropdownListDest.ExpandByteOffset);
        Assert.Equal(0x004A3DA8u, RetailOptionsDropdownListDest.ExpandTestSite);
        Assert.Equal(0x004A3DADu, RetailOptionsDropdownListDest.CollapseJumpSite);
        Assert.Equal(0x004A409Bu, RetailOptionsDropdownListDest.CollapseTarget);
        Assert.Equal(0x44u, RetailOptionsDropdownListDest.GetStateSlot);
        Assert.Equal(0x004A3FC8u, RetailOptionsDropdownListDest.GetStateCallSite);
        Assert.Equal(4u, RetailOptionsDropdownListDest.GetStateRet);
        Assert.Equal(0x004A3FCDu, RetailOptionsDropdownListDest.DestLoadSite);
        Assert.Equal(0x3F800000u, RetailOptionsDropdownListDest.ScaleBits);
        Assert.Equal(0x004A3FDAu, RetailOptionsDropdownListDest.FontCallSite);
        Assert.Equal(0x00515A70u, RetailOptionsDropdownListDest.Font);
        Assert.Equal(4u, RetailOptionsDropdownListDest.FontRet);
        Assert.Equal(0x004A3FE1u, RetailOptionsDropdownListDest.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsDropdownListDest.DrawText);
        Assert.Equal(32u, RetailOptionsDropdownListDest.DrawTextRet);
        Assert.Equal(
            RetailOptionsDropdownValueDest.RenderSite,
            RetailOptionsDropdownListDest.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownValueDest.PadGlobal,
            RetailOptionsDropdownListDest.PadGlobal);
        Assert.Equal(
            RetailOptionsDropdownValueDest.ExpandTestSite,
            RetailOptionsDropdownListDest.ExpandTestSite);
        Assert.NotEqual(
            RetailOptionsDropdownValueDest.DestLoadSite,
            RetailOptionsDropdownListDest.DestLoadSite);
        Assert.NotEqual(
            RetailOptionsDropdownValueDest.GetStateCallSite,
            RetailOptionsDropdownListDest.GetStateCallSite);
        Assert.True(RetailOptionsDropdownListDest.ExpandTestSite < RetailOptionsDropdownListDest.CollapseJumpSite);
        Assert.True(RetailOptionsDropdownListDest.CollapseJumpSite < RetailOptionsDropdownListDest.CollapsedLeftoverLoadSite);
        Assert.True(RetailOptionsDropdownListDest.CollapsedLeftoverLoadSite < RetailOptionsDropdownListDest.PadAddSite);
        Assert.True(RetailOptionsDropdownListDest.PadAddSite < RetailOptionsDropdownListDest.PadStoreSite);
        Assert.True(RetailOptionsDropdownListDest.PadStoreSite < RetailOptionsDropdownListDest.GetStateCallSite);
        Assert.True(RetailOptionsDropdownListDest.GetStateCallSite < RetailOptionsDropdownListDest.DestLoadSite);
        Assert.True(RetailOptionsDropdownListDest.DestLoadSite < RetailOptionsDropdownListDest.FontCallSite);
        Assert.True(RetailOptionsDropdownListDest.FontCallSite < RetailOptionsDropdownListDest.DrawTextCallSite);
        Assert.True(RetailOptionsDropdownListDest.DrawTextCallSite < RetailOptionsDropdownValueDest.DestLoadSite);
        Assert.True(RetailOptionsDropdownDest.DestStoreSite < RetailOptionsDropdownListDest.DestLoadSite);
        Assert.False(RetailOptionsDropdownListDest.InventsDestY5);
        Assert.False(RetailOptionsDropdownListDest.InventsDestX5);
        Assert.False(RetailOptionsDropdownListDest.InventsDestY268);
        Assert.False(RetailOptionsDropdownListDest.InventsDestY284);
        Assert.False(RetailOptionsDropdownListDest.InventsDestY304);
        Assert.False(RetailOptionsDropdownListDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListDest.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListDest.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListDest.InventsSheen);
        Assert.False(RetailOptionsDropdownListDest.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListDest.InventsFade);
        Assert.False(RetailOptionsDropdownListDest.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListDest.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListDest.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListDest.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListDest.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListDest.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListDest.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListDest.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListDest.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListDest.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListDest.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListDest.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownListDest.UsesIntegerHalf);
    }

    [Fact]
    public void ListDestIsCollapsedDestPlusPadAndIsNotTwoOrFive()
    {
        Assert.Equal(1f, RetailOptionsDropdownListDest.IdentityScale);
        Assert.Equal(2f, RetailOptionsDropdownListDest.Pad);
        Assert.Equal(323f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.Equal(324f, RetailOptionsDropdownListDest.DestX(320f));
        Assert.Equal(4f, RetailOptionsDropdownListDest.DestX(0f));
        Assert.Equal(
            RetailOptionsDropdownValueDest.DestX(319f) + RetailOptionsDropdownListDest.Pad,
            RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(2f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(5f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(268f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(284f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(304f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(321f, RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsDropdownValueDest.DestX(319f),
            RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsDropdownDest.DestX(319f, 16),
            RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsMenuItemDest.DestX(319f, 16),
            RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsMenuItemIconDest.DestX(319f, 16),
            RetailOptionsDropdownListDest.DestX(319f));
        Assert.NotEqual(
            RetailOptionsDropdownListDest.Pad,
            RetailOptionsDropdownListDest.IdentityScale);
        Assert.False(RetailOptionsDropdownListDest.InventsDestY5);
        Assert.False(RetailOptionsDropdownListDest.InventsDestX5);
        Assert.False(RetailOptionsDropdownListDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListDest.UsesIntegerHalf);
        Assert.False(RetailOptionsDropdownListDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawOptionDropdownConsumesDestXAndDoesNotInventDestTwo()
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

        Assert.Contains("RetailOptionsDropdownListDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDest.DestX", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownTextLeft", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("OptionValueLeftX", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDest.Pad", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDest.IdentityScale", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownValueDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("2f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("4f", dropdown, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailOptionsDropdownListDest", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDest", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDest", valueBar, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListDest", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListDest", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListDest", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListDest", click, StringComparison.Ordinal);
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
