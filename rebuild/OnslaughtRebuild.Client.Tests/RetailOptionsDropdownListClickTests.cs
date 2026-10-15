// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded list click leftover
/// after <c>0x004A4010</c> <c>call 0x00469400</c> /
/// <c>0x004A4018</c> <c>test al, al</c> /
/// <c>0x004A401F</c> <c>mov [esi+0x20], edi</c> /
/// <c>0x004A4024</c> <c>mov byte [esi+0x24], 0</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3FE6</c> <c>fild [esp+0x24]</c> leftover SIZE.cy,
/// <c>0x004A3FEA</c> <c>fadd [esp+0x10]</c> dest Y leftover,
/// <c>0x004A3FEE</c> <c>fstp [esp+0x30]</c> bottom leftover,
/// <c>0x004A3FF2</c> <c>fild [esp+0x20]</c> leftover label SIZE.cx,
/// <c>0x004A3FFA</c> <c>push ecx</c> bottom,
/// <c>0x004A3FFB</c> <c>fadd [esp+0x20]</c> collapsed dest leftover,
/// <c>0x004A3FFF</c> <c>fadd [0x005D8BA0]</c> pad leftover,
/// <c>0x004A400D</c> <c>push edx</c> right,
/// <c>0x004A400E</c> <c>push ebp</c> dest Y leftover,
/// <c>0x004A400F</c> <c>push ebx</c> dest X leftover,
/// <c>0x004A4010</c> <c>call 0x00469400</c>,
/// <c>0x004A4015</c> <c>add esp, 0x10</c>,
/// <c>0x004A4018</c> <c>test al, al</c>,
/// <c>0x004A401A</c> <c>je 0x004A4044</c>,
/// <c>0x004A401C</c> <c>mov al, [esi+0x25]</c>,
/// <c>0x004A401F</c> <c>mov [esi+0x20], edi</c>,
/// <c>0x004A4022</c> <c>test al, al</c>,
/// <c>0x004A4024</c> <c>mov byte [esi+0x24], 0</c>,
/// <c>0x004A4028</c> <c>jne 0x004A403A</c>,
/// <c>0x004A402A</c> <c>cmp [esi+0x1c], edi</c>,
/// <c>0x004A4034</c> <c>mov [esi+0x1c], edi</c>,
/// <c>0x004A4037</c> <c>call [eax+0x38]</c>.
/// Click writes currentIndex and the expand byte. Dest Y does not.
/// Colour leftover already consults currentIndex. Hover leftover already
/// owns <c>0x004A3FA6</c>. Dest is not 15.5, 322.5, 148.0, or the 2.0
/// constant. HandleOptionsPointerConfirm consumes the leftover.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack.
/// Do not change MeasureText. Do not redo dest leftovers, list colour,
/// list hover, Apply pulse, dropdown cosine, language pitch, or the
/// 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownListClickTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListClickNotDestColourOrHover()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListClick.RenderSite);
        Assert.Equal(0x004A3FE6u, RetailOptionsDropdownListClick.CyFildSite);
        Assert.Equal(0x004A3FEAu, RetailOptionsDropdownListClick.DestYAddSite);
        Assert.Equal(0x004A3FEEu, RetailOptionsDropdownListClick.BottomStoreSite);
        Assert.Equal(0x004A3FF2u, RetailOptionsDropdownListClick.LabelCxFildSite);
        Assert.Equal(0x004A3FF6u, RetailOptionsDropdownListClick.BottomLoadSite);
        Assert.Equal(0x004A3FFAu, RetailOptionsDropdownListClick.BottomPushSite);
        Assert.Equal(0x004A3FFBu, RetailOptionsDropdownListClick.CollapsedAddSite);
        Assert.Equal(0x004A3FFFu, RetailOptionsDropdownListClick.PadAddSite);
        Assert.Equal(0x005D8BA0u, RetailOptionsDropdownListClick.PadGlobal);
        Assert.Equal(0x40000000u, RetailOptionsDropdownListClick.PadBits);
        Assert.Equal(0x004A4005u, RetailOptionsDropdownListClick.RightStoreSite);
        Assert.Equal(0x004A4009u, RetailOptionsDropdownListClick.RightLoadSite);
        Assert.Equal(0x004A400Du, RetailOptionsDropdownListClick.RightPushSite);
        Assert.Equal(0x004A400Eu, RetailOptionsDropdownListClick.TopPushSite);
        Assert.Equal(0x004A400Fu, RetailOptionsDropdownListClick.LeftPushSite);
        Assert.Equal(0x004A4010u, RetailOptionsDropdownListClick.ClickHitSite);
        Assert.Equal(0x00469400u, RetailOptionsDropdownListClick.ClickHit);
        Assert.Equal(0x10u, RetailOptionsDropdownListClick.ClickHitPop);
        Assert.Equal(0x004A4018u, RetailOptionsDropdownListClick.HitTestSite);
        Assert.Equal(0x004A401Au, RetailOptionsDropdownListClick.MissJumpSite);
        Assert.Equal(0x004A4044u, RetailOptionsDropdownListClick.MissJumpTarget);
        Assert.Equal(0x004A401Cu, RetailOptionsDropdownListClick.PendingLoadSite);
        Assert.Equal(0x25u, RetailOptionsDropdownListClick.PendingByteOffset);
        Assert.Equal(0x004A401Fu, RetailOptionsDropdownListClick.CurrentIndexStoreSite);
        Assert.Equal(0x20u, RetailOptionsDropdownListClick.CurrentIndexOffset);
        Assert.Equal(0x004A4022u, RetailOptionsDropdownListClick.PendingTestSite);
        Assert.Equal(0x004A4024u, RetailOptionsDropdownListClick.ExpandStoreSite);
        Assert.Equal(0x24u, RetailOptionsDropdownListClick.ExpandByteOffset);
        Assert.Equal(0x004A4028u, RetailOptionsDropdownListClick.PendingSkipSite);
        Assert.Equal(0x004A403Au, RetailOptionsDropdownListClick.PendingSkipTarget);
        Assert.Equal(0x004A402Au, RetailOptionsDropdownListClick.CommittedCompareSite);
        Assert.Equal(0x1Cu, RetailOptionsDropdownListClick.CommittedOffset);
        Assert.Equal(0x004A4034u, RetailOptionsDropdownListClick.CommittedStoreSite);
        Assert.Equal(0x38u, RetailOptionsDropdownListClick.SetSlot);
        Assert.Equal(0x004A4037u, RetailOptionsDropdownListClick.SetCallSite);
        Assert.Equal(0x00523CC0u, RetailOptionsDropdownListClick.PointInRect);
        Assert.Equal(0x004A3FA6u, RetailOptionsDropdownListClick.HoverHitSite);
        Assert.Equal(0x004693D0u, RetailOptionsDropdownListClick.HoverHit);
        Assert.Equal(
            RetailOptionsDropdownListHover.RenderSite,
            RetailOptionsDropdownListClick.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownListHover.ClickHitSite,
            RetailOptionsDropdownListClick.ClickHitSite);
        Assert.Equal(
            RetailOptionsDropdownListHover.ClickHit,
            RetailOptionsDropdownListClick.ClickHit);
        Assert.Equal(
            RetailOptionsDropdownListHover.CurrentIndexOffset,
            RetailOptionsDropdownListClick.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListDest.PadGlobal,
            RetailOptionsDropdownListClick.PadGlobal);
        Assert.Equal(
            RetailOptionsDropdownListDest.ExpandByteOffset,
            RetailOptionsDropdownListClick.ExpandByteOffset);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailOptionsDropdownListClick.ClickHitSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.PointInRect,
            RetailOptionsDropdownListClick.PointInRect);
        Assert.NotEqual(
            RetailOptionsDropdownListColor.IdleColorSite,
            RetailOptionsDropdownListClick.ClickHitSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDestY.DestYPushSite,
            RetailOptionsDropdownListClick.CurrentIndexStoreSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestLoadSite,
            RetailOptionsDropdownListClick.CurrentIndexStoreSite);
        Assert.True(RetailOptionsDropdownListClick.HoverHitSite < RetailOptionsDropdownListClick.ClickHitSite);
        Assert.True(RetailOptionsDropdownListClick.CyFildSite < RetailOptionsDropdownListClick.ClickHitSite);
        Assert.True(RetailOptionsDropdownListClick.ClickHitSite < RetailOptionsDropdownListClick.CurrentIndexStoreSite);
        Assert.True(RetailOptionsDropdownListClick.CurrentIndexStoreSite < RetailOptionsDropdownListClick.ExpandStoreSite);
        Assert.False(RetailOptionsDropdownListClick.InventsDestY5);
        Assert.False(RetailOptionsDropdownListClick.InventsDestX5);
        Assert.False(RetailOptionsDropdownListClick.InventsDestY268);
        Assert.False(RetailOptionsDropdownListClick.InventsDestY284);
        Assert.False(RetailOptionsDropdownListClick.InventsDestY304);
        Assert.False(RetailOptionsDropdownListClick.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListClick.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListClick.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListClick.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListClick.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListClick.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListClick.InventsSheen);
        Assert.False(RetailOptionsDropdownListClick.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListClick.InventsFade);
        Assert.True(RetailOptionsDropdownListClick.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListClick.IsClickHit);
        Assert.False(RetailOptionsDropdownListClick.IsHoverHit);
        Assert.False(RetailOptionsDropdownListClick.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListClick.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListClick.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListClick.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListClick.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListClick.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListClick.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListClick.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListClick.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownListHover.IsClickHit);
        Assert.False(RetailOptionsDropdownListColor.IsClickHit);
    }

    [Fact]
    public void ClickWritesCurrentIndexAndExpandAndDoesNotInventDest()
    {
        Assert.Equal(2, RetailOptionsDropdownListClick.CurrentIndexAfterClick(0, 2, true));
        Assert.Equal(0, RetailOptionsDropdownListClick.CurrentIndexAfterClick(0, 2, false));
        Assert.Equal(1, RetailOptionsDropdownListClick.CurrentIndexAfterClick(1, 1, true));
        Assert.False(RetailOptionsDropdownListClick.ExpandAfterClick(true, true));
        Assert.True(RetailOptionsDropdownListClick.ExpandAfterClick(true, false));
        Assert.False(RetailOptionsDropdownListClick.ExpandAfterClick(false, false));
        Assert.True(RetailOptionsDropdownListClick.AppliesLive(0, 0, 2));
        Assert.False(RetailOptionsDropdownListClick.AppliesLive(1, 0, 2));
        Assert.False(RetailOptionsDropdownListClick.AppliesLive(0, 2, 2));
        Assert.Equal(
            RetailOptionsDropdownListClick.CurrentIndexOffset,
            RetailOptionsDropdownListHover.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListClick.ExpandByteOffset,
            RetailOptionsDropdownListDest.ExpandByteOffset);
        Assert.True(
            RetailOptionsDropdownListClick.Contains(323f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListClick.Contains(322.9f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListClick.Contains(403f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListClick.Contains(323f, 199.9f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListClick.Contains(323f, 216f, 323f, 200f, 80, 16));
        Assert.Equal(
            RetailOptionsDropdownListDest.DestX(319f),
            RetailOptionsDropdownListClick.Left(319f));
        Assert.Equal(
            RetailOptionsDropdownListDestY.DestY(245f, 4, 16, 2),
            RetailOptionsDropdownListClick.Top(245f, 4, 16, 2));
        Assert.Equal(
            RetailOptionsDropdownListDest.DestX(319f) + 80,
            RetailOptionsDropdownListClick.Right(319f, 80));
        Assert.Equal(
            RetailOptionsDropdownListDestY.DestY(245f, 4, 16, 2) + 16,
            RetailOptionsDropdownListClick.Bottom(245f, 4, 16, 2));
        Assert.NotEqual(5f, RetailOptionsDropdownListClick.Left(319f));
        Assert.NotEqual(15.5f, RetailOptionsDropdownListClick.Top(245f, 4, 16, 0));
        Assert.NotEqual(148f, RetailOptionsDropdownListClick.Top(245f, 4, 16, 0));
        Assert.NotEqual(2f, RetailOptionsDropdownListClick.Left(319f));
        Assert.NotEqual(
            RetailOptionsDropdownPanelDest.Width(80),
            RetailOptionsDropdownListClick.Right(319f, 80) -
            RetailOptionsDropdownListClick.Left(319f));
        Assert.NotEqual(
            RetailOptionsDropdownPanelDest.DestX(319f),
            RetailOptionsDropdownListClick.Left(319f));
        Assert.False(RetailOptionsDropdownListClick.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListClick.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListClick.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListClick.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListClick.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListClick.IsClickHit);
        Assert.False(RetailOptionsDropdownListClick.IsHoverHit);

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
        Assert.Equal(RetailOptionsSignal.ValueChanged, menu.Confirm());
        Assert.False(menu.IsExpanded);
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
        Assert.Equal(other, menu.SelectedRow.CommittedIndex);
        Assert.NotEqual(committed, menu.SelectedRow.CommittedIndex);
    }

    [Fact]
    public void HandleOptionsPointerConfirmConsumesClickAndDoesNotPileIntoMotion()
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

        Assert.Contains("RetailOptionsDropdownListClick", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListClick.Contains", confirm, StringComparison.Ordinal);
        Assert.Contains("SelectState", confirm, StringComparison.Ordinal);
        Assert.Contains("ConfirmOptions", confirm, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDest.DestX", confirm, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDestY.DestY", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownPanelDest.DestX", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverState", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClick", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListClick", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListClick", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListClick", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListClick", click, StringComparison.Ordinal);
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        Assert.DoesNotContain("RetailOptionsDropdownListClick", pointerConfirm, StringComparison.Ordinal);
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
