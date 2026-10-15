// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded list colour leftover
/// after <c>0x004A3F6C</c> <c>mov ebx, 0xFF404040</c> /
/// <c>0x004A3F75</c> <c>or ebx, -1</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3F43</c> <c>xor edi, edi</c>,
/// <c>0x004A3F65</c> <c>mov ebp, [esp+0x10]</c> dest Y leftover,
/// <c>0x004A3F69</c> <c>mov eax, [esi+0x20]</c> currentIndex,
/// <c>0x004A3F6C</c> <c>mov ebx, 0xFF404040</c>,
/// <c>0x004A3F71</c> <c>cmp edi, eax</c>,
/// <c>0x004A3F73</c> <c>jne 0x004A3F78</c>,
/// <c>0x004A3F75</c> <c>or ebx, -1</c> (<c>83 CB FF</c>),
/// <c>0x004A3FCC</c> <c>push ebx</c> colour into DrawText,
/// <c>0x004A3FCD</c> <c>mov ebx, [esp+0x4C]</c> dest X leftover,
/// <c>0x004A3FE1</c> <c>call 0x004659A0</c>.
/// Idle is <c>0xFF404040</c>. When the loop index equals
/// currentIndex, ebx becomes <c>0xFFFFFFFF</c>. Colour consults
/// currentIndex. Dest Y does not. Dest is not 15.5, 322.5, 148.0,
/// or the 2.0 constant. DrawOptionDropdown consumes PackedColor.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284,
/// dest Y=304, dest from the 2.0 constant, wrap, fade, sheen, or a
/// 2px kerning hack. Do not change MeasureText. Do not redo the
/// expanded list dest X, expanded list dest Y, expanded panel dest,
/// collapsed value dest, label dest, icon dest, CMenuItem dest,
/// colour AND, Apply pulse, dropdown cosine, language pitch, or
/// the 0x00463669 compare. Hover and click hits are later
/// leftovers.</para>
/// </summary>
public sealed class RetailOptionsDropdownListColorTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListColorNotDestImmediates()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListColor.RenderSite);
        Assert.Equal(0x004A3F43u, RetailOptionsDropdownListColor.LoopIndexZeroSite);
        Assert.Equal(0x004A3F69u, RetailOptionsDropdownListColor.CurrentIndexLoadSite);
        Assert.Equal(0x20u, RetailOptionsDropdownListColor.CurrentIndexOffset);
        Assert.Equal(0x004A3F6Cu, RetailOptionsDropdownListColor.IdleColorSite);
        Assert.Equal(0xFF404040u, RetailOptionsDropdownListColor.IdlePackedColor);
        Assert.Equal(0x004A3F71u, RetailOptionsDropdownListColor.CompareSite);
        Assert.Equal(0x004A3F73u, RetailOptionsDropdownListColor.SelectedSkipSite);
        Assert.Equal(0x004A3F78u, RetailOptionsDropdownListColor.SelectedSkipTarget);
        Assert.Equal(0x004A3F75u, RetailOptionsDropdownListColor.SelectedOrSite);
        Assert.Equal(0xFFFFFFFFu, RetailOptionsDropdownListColor.SelectedOrImmediate);
        Assert.Equal(0x004A3FCCu, RetailOptionsDropdownListColor.ColorPushSite);
        Assert.Equal(0x004A3FE1u, RetailOptionsDropdownListColor.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsDropdownListColor.DrawText);
        Assert.Equal(0x004A3FA6u, RetailOptionsDropdownListColor.HoverHitSite);
        Assert.Equal(0x004693D0u, RetailOptionsDropdownListColor.HoverHit);
        Assert.Equal(0x004A4010u, RetailOptionsDropdownListColor.ClickHitSite);
        Assert.Equal(0x00469400u, RetailOptionsDropdownListColor.ClickHit);
        Assert.Equal(
            RetailOptionsDropdownListDest.RenderSite,
            RetailOptionsDropdownListColor.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownListDestY.DrawText,
            RetailOptionsDropdownListColor.DrawText);
        Assert.Equal(
            RetailOptionsDropdownListDestY.CurrentIndexOffset,
            RetailOptionsDropdownListColor.CurrentIndexOffset);
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestLoadSite,
            RetailOptionsDropdownListColor.IdleColorSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDestY.DestYPushSite,
            RetailOptionsDropdownListColor.ColorPushSite);
        Assert.NotEqual(
            RetailOptionsDropdownPanelDest.DestYPushSite,
            RetailOptionsDropdownListColor.ColorPushSite);
        Assert.True(RetailOptionsDropdownListColor.LoopIndexZeroSite < RetailOptionsDropdownListColor.CurrentIndexLoadSite);
        Assert.True(RetailOptionsDropdownListColor.CurrentIndexLoadSite < RetailOptionsDropdownListColor.IdleColorSite);
        Assert.True(RetailOptionsDropdownListColor.IdleColorSite < RetailOptionsDropdownListColor.CompareSite);
        Assert.True(RetailOptionsDropdownListColor.CompareSite < RetailOptionsDropdownListColor.SelectedOrSite);
        Assert.True(RetailOptionsDropdownListColor.SelectedOrSite < RetailOptionsDropdownListColor.HoverHitSite);
        Assert.True(RetailOptionsDropdownListColor.HoverHitSite < RetailOptionsDropdownListColor.ColorPushSite);
        Assert.True(RetailOptionsDropdownListColor.ColorPushSite < RetailOptionsDropdownListDest.DestLoadSite);
        Assert.True(RetailOptionsDropdownListColor.ColorPushSite < RetailOptionsDropdownListColor.DrawTextCallSite);
        Assert.True(RetailOptionsDropdownListColor.DrawTextCallSite < RetailOptionsDropdownListColor.ClickHitSite);
        Assert.False(RetailOptionsDropdownListColor.InventsDestY5);
        Assert.False(RetailOptionsDropdownListColor.InventsDestX5);
        Assert.False(RetailOptionsDropdownListColor.InventsDestY268);
        Assert.False(RetailOptionsDropdownListColor.InventsDestY284);
        Assert.False(RetailOptionsDropdownListColor.InventsDestY304);
        Assert.False(RetailOptionsDropdownListColor.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListColor.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListColor.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListColor.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListColor.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListColor.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListColor.InventsSheen);
        Assert.False(RetailOptionsDropdownListColor.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListColor.InventsFade);
        Assert.True(RetailOptionsDropdownListColor.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownListDestY.UsesCurrentIndex);
        Assert.False(RetailOptionsDropdownListColor.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListColor.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListColor.IsHoverHit);
        Assert.False(RetailOptionsDropdownListColor.IsClickHit);
        Assert.False(RetailOptionsDropdownListColor.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListColor.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListColor.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListColor.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListColor.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListColor.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListColor.ChangesMeasureText);
    }

    [Fact]
    public void PackedColorIsIdleOrMinusOneWhenIndexEqualsCurrentIndex()
    {
        Assert.Equal(0xFF404040u, RetailOptionsDropdownListColor.PackedColor(1, 0));
        Assert.Equal(0xFF404040u, RetailOptionsDropdownListColor.PackedColor(2, 1));
        Assert.Equal(0xFFFFFFFFu, RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.Equal(0xFFFFFFFFu, RetailOptionsDropdownListColor.PackedColor(2, 2));
        Assert.Equal(
            RetailOptionsDropdownListColor.IdlePackedColor,
            RetailOptionsDropdownListColor.PackedColor(1, 0));
        Assert.Equal(
            RetailOptionsDropdownListColor.IdlePackedColor | RetailOptionsDropdownListColor.SelectedOrImmediate,
            RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.NotEqual(0xFFD6D6D6u, RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.NotEqual(0xFFFFCC00u, RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.NotEqual(0x50505050u, RetailOptionsDropdownListColor.PackedColor(1, 0));
        Assert.NotEqual(5u, RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.NotEqual(0x41780000u, RetailOptionsDropdownListColor.PackedColor(0, 0));
        Assert.False(RetailOptionsDropdownListColor.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListColor.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListColor.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListColor.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListColor.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListColor.UsesCurrentIndex);
    }

    [Fact]
    public void DrawOptionDropdownConsumesPackedColorAndDoesNotInventDest()
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

        Assert.Contains("RetailOptionsDropdownListColor", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListColor.PackedColor", dropdown, StringComparison.Ordinal);
        Assert.Contains("RetailColor(RetailOptionsDropdownListColor.PackedColor", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownEntrySelected", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("DropdownEntry)", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Colors.White", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("128f", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("(i - row.CurrentIndex)", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("(i - expanded.CurrentIndex)", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListColor", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY.IdentityScale", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListDestY.Scale", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownValueDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemColor", dropdown, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailOptionsDropdownListColor", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListColor", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListColor", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", confirm, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListColor", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListColor", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListColor", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListColor", click, StringComparison.Ordinal);
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
