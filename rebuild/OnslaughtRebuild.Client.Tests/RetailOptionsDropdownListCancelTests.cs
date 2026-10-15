// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> post-loop cancel leftover
/// after <c>0x004A4059</c> <c>mov ecx, 0x675688</c> /
/// <c>0x004A405E</c> <c>call 0x0044DEA0</c> /
/// <c>0x004A4068</c> <c>mov eax, [0x0089BE28]</c> /
/// <c>0x004A4071</c> <c>mov edx, [esi+0x1c]</c> /
/// <c>0x004A4074</c> <c>mov byte [esi+0x24], 0</c> /
/// <c>0x004A4078</c> <c>mov [esi+0x20], edx</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A4059</c> <c>mov ecx, 0x675688</c>,
/// <c>0x004A405E</c> <c>call 0x0044DEA0</c>,
/// <c>0x004A4063</c> <c>test eax, eax</c>,
/// <c>0x004A4065</c> <c>pop ebp</c>,
/// <c>0x004A4066</c> <c>jne 0x004A40CF</c>,
/// <c>0x004A4068</c> <c>mov eax, [0x0089BE28]</c>,
/// <c>0x004A406D</c> <c>test eax, eax</c>,
/// <c>0x004A406F</c> <c>je 0x004A40CF</c>,
/// <c>0x004A4071</c> <c>mov edx, [esi+0x1c]</c>,
/// <c>0x004A4074</c> <c>mov byte [esi+0x24], 0</c>,
/// <c>0x004A4078</c> <c>mov [esi+0x20], edx</c>,
/// <c>0x004A407B</c> <c>push 2</c>,
/// <c>0x004A407D</c> <c>mov [0x0089BE28], 0</c>,
/// <c>0x004A4087</c> <c>call 0x00468770</c>.
/// <c>0x0044DEA0</c> returns 1 only when <c>[ecx+0x1F8C]</c> and
/// <c>[ecx+0x1F98]</c> are both nonzero. Cancel writes currentIndex
/// from committedIndex and the expand byte. Dest Y does not. Colour
/// leftover already consults currentIndex. Hover leftover already owns
/// <c>0x004A3FA6</c>. Click leftover already owns <c>0x004A4010</c>.
/// Dest is not 15.5, 322.5, 148.0, or the 2.0 constant.
/// HandleOptionsPointerCancel consumes the leftover. Click-hit sound
/// at <c>0x004A403C</c> is later.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack.
/// Do not change MeasureText. Do not redo dest leftovers, list colour,
/// list hover, list click, Apply pulse, dropdown cosine, language pitch,
/// or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownListCancelTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListCancelNotDestColourHoverOrClick()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListCancel.RenderSite);
        Assert.Equal(0x004A4059u, RetailOptionsDropdownListCancel.FrontEndLoadSite);
        Assert.Equal(0x00675688u, RetailOptionsDropdownListCancel.FrontEndThis);
        Assert.Equal(0x004A405Eu, RetailOptionsDropdownListCancel.HelperCallSite);
        Assert.Equal(0x0044DEA0u, RetailOptionsDropdownListCancel.Helper);
        Assert.Equal(0x1F8Cu, RetailOptionsDropdownListCancel.HelperFieldA);
        Assert.Equal(0x1F98u, RetailOptionsDropdownListCancel.HelperFieldB);
        Assert.Equal(0x004A4063u, RetailOptionsDropdownListCancel.HelperTestSite);
        Assert.Equal(0x004A4065u, RetailOptionsDropdownListCancel.EbpPopSite);
        Assert.Equal(0x004A4066u, RetailOptionsDropdownListCancel.HelperSkipSite);
        Assert.Equal(0x004A40CFu, RetailOptionsDropdownListCancel.SkipTarget);
        Assert.Equal(0x004A4068u, RetailOptionsDropdownListCancel.LatchLoadSite);
        Assert.Equal(0x0089BE28u, RetailOptionsDropdownListCancel.Latch);
        Assert.Equal(0x004A406Du, RetailOptionsDropdownListCancel.LatchTestSite);
        Assert.Equal(0x004A406Fu, RetailOptionsDropdownListCancel.LatchSkipSite);
        Assert.Equal(0x004A4071u, RetailOptionsDropdownListCancel.CommittedLoadSite);
        Assert.Equal(0x1Cu, RetailOptionsDropdownListCancel.CommittedOffset);
        Assert.Equal(0x004A4074u, RetailOptionsDropdownListCancel.ExpandStoreSite);
        Assert.Equal(0x24u, RetailOptionsDropdownListCancel.ExpandByteOffset);
        Assert.Equal(0x004A4078u, RetailOptionsDropdownListCancel.CurrentIndexStoreSite);
        Assert.Equal(0x20u, RetailOptionsDropdownListCancel.CurrentIndexOffset);
        Assert.Equal(0x004A407Bu, RetailOptionsDropdownListCancel.SoundPushSite);
        Assert.Equal(2u, RetailOptionsDropdownListCancel.SoundId);
        Assert.Equal(0x004A407Du, RetailOptionsDropdownListCancel.LatchClearSite);
        Assert.Equal(0x004A4087u, RetailOptionsDropdownListCancel.SoundCallSite);
        Assert.Equal(0x00468770u, RetailOptionsDropdownListCancel.PlaySound);
        Assert.Equal(0x004A4010u, RetailOptionsDropdownListCancel.ClickHitSite);
        Assert.Equal(0x004A3FA6u, RetailOptionsDropdownListCancel.HoverHitSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.RenderSite,
            RetailOptionsDropdownListCancel.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.CurrentIndexOffset,
            RetailOptionsDropdownListCancel.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListClick.CommittedOffset,
            RetailOptionsDropdownListCancel.CommittedOffset);
        Assert.Equal(
            RetailOptionsDropdownListClick.ExpandByteOffset,
            RetailOptionsDropdownListCancel.ExpandByteOffset);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailOptionsDropdownListCancel.HelperCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailOptionsDropdownListCancel.HelperCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListColor.IdleColorSite,
            RetailOptionsDropdownListCancel.HelperCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDestY.DestYPushSite,
            RetailOptionsDropdownListCancel.CurrentIndexStoreSite);
        Assert.True(RetailOptionsDropdownListCancel.ClickHitSite < RetailOptionsDropdownListCancel.HelperCallSite);
        Assert.True(RetailOptionsDropdownListCancel.HelperCallSite < RetailOptionsDropdownListCancel.LatchLoadSite);
        Assert.True(RetailOptionsDropdownListCancel.LatchLoadSite < RetailOptionsDropdownListCancel.CurrentIndexStoreSite);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY5);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestX5);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY268);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY284);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY304);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListCancel.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListCancel.InventsSheen);
        Assert.False(RetailOptionsDropdownListCancel.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListCancel.InventsFade);
        Assert.True(RetailOptionsDropdownListCancel.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListCancel.IsCancel);
        Assert.False(RetailOptionsDropdownListCancel.IsClickHit);
        Assert.False(RetailOptionsDropdownListCancel.IsHoverHit);
        Assert.False(RetailOptionsDropdownListCancel.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListCancel.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListCancel.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListClick);
        Assert.False(RetailOptionsDropdownListCancel.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListCancel.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListCancel.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListCancel.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListCancel.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListCancel.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListClick.IsClickHit);
        Assert.True(RetailOptionsDropdownListHover.IsHoverHit);
        Assert.False(RetailOptionsDropdownListHover.IsClickHit);
    }

    [Fact]
    public void CancelRevertsCurrentIndexAndExpandAndDoesNotInventDest()
    {
        Assert.True(RetailOptionsDropdownListCancel.HelperNonzero(1, 1));
        Assert.False(RetailOptionsDropdownListCancel.HelperNonzero(0, 1));
        Assert.False(RetailOptionsDropdownListCancel.HelperNonzero(1, 0));
        Assert.False(RetailOptionsDropdownListCancel.HelperNonzero(0, 0));
        Assert.True(RetailOptionsDropdownListCancel.Applies(helperNonzero: false, latch: true));
        Assert.False(RetailOptionsDropdownListCancel.Applies(helperNonzero: true, latch: true));
        Assert.False(RetailOptionsDropdownListCancel.Applies(helperNonzero: false, latch: false));
        Assert.False(RetailOptionsDropdownListCancel.Applies(helperNonzero: true, latch: false));
        Assert.Equal(0, RetailOptionsDropdownListCancel.CurrentIndexAfterCancel(2, 0, true));
        Assert.Equal(2, RetailOptionsDropdownListCancel.CurrentIndexAfterCancel(2, 0, false));
        Assert.Equal(1, RetailOptionsDropdownListCancel.CurrentIndexAfterCancel(1, 1, true));
        Assert.False(RetailOptionsDropdownListCancel.ExpandAfterCancel(true, true));
        Assert.True(RetailOptionsDropdownListCancel.ExpandAfterCancel(true, false));
        Assert.False(RetailOptionsDropdownListCancel.ExpandAfterCancel(false, false));
        Assert.Equal(
            RetailOptionsDropdownListCancel.CurrentIndexOffset,
            RetailOptionsDropdownListHover.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListCancel.ExpandByteOffset,
            RetailOptionsDropdownListDest.ExpandByteOffset);
        Assert.Equal(
            RetailOptionsDropdownListCancel.CommittedOffset,
            RetailOptionsDropdownListClick.CommittedOffset);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListCancel.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListCancel.RedoesDropdownListClick);
        Assert.False(RetailOptionsDropdownListCancel.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListCancel.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListCancel.IsCancel);
        Assert.False(RetailOptionsDropdownListCancel.IsClickHit);
        Assert.False(RetailOptionsDropdownListCancel.IsHoverHit);

        var menu = new RetailOptionsMenu();
        menu.Enter(RetailOptionsPage.Video);
        int found = -1;
        for (int i = 0; i < menu.Rows.Count; i++)
        {
            if (menu.Rows[i].Kind == RetailOptionsRowKind.Dropdown &&
                menu.Rows[i].States.Count > 1)
            {
                found = i;
                break;
            }
        }

        Assert.True(found >= 0);
        menu.Hover(found);
        Assert.Equal(found, menu.SelectedIndex);
        Assert.Equal(RetailOptionsSignal.ValueChanged, menu.Confirm());
        Assert.True(menu.IsExpanded);
        int committed = menu.SelectedRow.CommittedIndex;
        int current = menu.SelectedRow.CurrentIndex;
        int other = current == 0 ? 1 : 0;
        Assert.True(menu.SelectState(other));
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
        Assert.Equal(committed, menu.SelectedRow.CommittedIndex);
        Assert.True(menu.CancelExpanded());
        Assert.False(menu.IsExpanded);
        Assert.Equal(committed, menu.SelectedRow.CurrentIndex);
        Assert.Equal(committed, menu.SelectedRow.CommittedIndex);
        Assert.False(menu.CancelExpanded());
    }

    [Fact]
    public void HandleOptionsPointerCancelConsumesCancelAndDoesNotPileIntoClickOrMotion()
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
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");

        Assert.Contains("RetailFrontendScenePath.AcceptsOptionsPointerCancel", cancel, StringComparison.Ordinal);
        Assert.Contains("CancelExpanded", cancel, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendAudioCue.Back", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectState", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("ConfirmOptions", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverState", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", click, StringComparison.Ordinal);
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        Assert.DoesNotContain("RetailOptionsDropdownListCancel", pointerConfirm, StringComparison.Ordinal);
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
