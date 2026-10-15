// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> expanded list hover leftover
/// after <c>0x004A3FA6</c> <c>call 0x004693D0</c> /
/// <c>0x004A3FAE</c> <c>test al, al</c> /
/// <c>0x004A3FB2</c> <c>mov [esi+0x20], edi</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3F55</c> <c>fld [esp+0x1C]</c> collapsed dest leftover,
/// <c>0x004A3F59</c> <c>fadd [0x005D8BA0]</c> pad leftover,
/// <c>0x004A3F5F</c> <c>fstp [esp+0x34]</c> dest X leftover,
/// <c>0x004A3F65</c> <c>mov ebp, [esp+0x10]</c> dest Y leftover,
/// <c>0x004A3F78</c> <c>fild [esp+0x20]</c> leftover label SIZE.cx,
/// <c>0x004A3F7C</c> <c>mov ecx, [esp+0x34]</c> left,
/// <c>0x004A3F80</c> <c>fadd [esp+0x1C]</c>,
/// <c>0x004A3F84</c> <c>fadd [0x005D8BA0]</c>,
/// <c>0x004A3F8A</c> <c>fstp [esp+0x30]</c> right,
/// <c>0x004A3F8E</c> <c>fld [esp+0x38]</c> pitch leftover,
/// <c>0x004A3F92</c> <c>fadd [esp+0x10]</c>,
/// <c>0x004A3F9A</c> <c>fstp [esp+0x14]</c> bottom,
/// <c>0x004A3FA2</c> <c>push edx</c>,
/// <c>0x004A3FA3</c> <c>push eax</c>,
/// <c>0x004A3FA4</c> <c>push ebp</c>,
/// <c>0x004A3FA5</c> <c>push ecx</c>,
/// <c>0x004A3FA6</c> <c>call 0x004693D0</c>,
/// <c>0x004A3FAB</c> <c>add esp, 0x10</c>,
/// <c>0x004A3FAE</c> <c>test al, al</c>,
/// <c>0x004A3FB0</c> <c>je 0x004A3FB5</c>,
/// <c>0x004A3FB2</c> <c>mov [esi+0x20], edi</c>.
/// Hover writes currentIndex. Dest Y does not. Colour leftover already
/// consults currentIndex. Dest is not 15.5, 322.5, 148.0, or the 2.0
/// constant. HandleOptionsPointerMotion consumes the leftover. Click
/// at <c>0x004A4010</c> is a later leftover. Do not invent dest Y=5,
/// dest X=5, dest Y=268, dest Y=284, dest Y=304, dest from the 2.0
/// constant, wrap, fade, sheen, or a 2px kerning hack. Do not change
/// MeasureText. Do not redo dest leftovers, list colour, Apply pulse,
/// dropdown cosine, language pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownListHoverTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListHoverNotDestOrColour()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListHover.RenderSite);
        Assert.Equal(0x004A3F55u, RetailOptionsDropdownListHover.CollapsedLeftoverLoadSite);
        Assert.Equal(0x004A3F59u, RetailOptionsDropdownListHover.PadAddSite);
        Assert.Equal(0x005D8BA0u, RetailOptionsDropdownListHover.PadGlobal);
        Assert.Equal(0x40000000u, RetailOptionsDropdownListHover.PadBits);
        Assert.Equal(0x004A3F5Fu, RetailOptionsDropdownListHover.LeftStoreSite);
        Assert.Equal(0x004A3F65u, RetailOptionsDropdownListHover.TopLoadSite);
        Assert.Equal(0x004A3F78u, RetailOptionsDropdownListHover.LabelCxFildSite);
        Assert.Equal(0x004A3F7Cu, RetailOptionsDropdownListHover.LeftLoadSite);
        Assert.Equal(0x004A3F8Au, RetailOptionsDropdownListHover.RightStoreSite);
        Assert.Equal(0x004A3F9Au, RetailOptionsDropdownListHover.BottomStoreSite);
        Assert.Equal(0x004A3FA2u, RetailOptionsDropdownListHover.BottomPushSite);
        Assert.Equal(0x004A3FA3u, RetailOptionsDropdownListHover.RightPushSite);
        Assert.Equal(0x004A3FA4u, RetailOptionsDropdownListHover.TopPushSite);
        Assert.Equal(0x004A3FA5u, RetailOptionsDropdownListHover.LeftPushSite);
        Assert.Equal(0x004A3FA6u, RetailOptionsDropdownListHover.HoverHitSite);
        Assert.Equal(0x004693D0u, RetailOptionsDropdownListHover.HoverHit);
        Assert.Equal(0x10u, RetailOptionsDropdownListHover.HoverHitPop);
        Assert.Equal(0x004A3FAEu, RetailOptionsDropdownListHover.HitTestSite);
        Assert.Equal(0x004A3FB0u, RetailOptionsDropdownListHover.MissJumpSite);
        Assert.Equal(0x004A3FB5u, RetailOptionsDropdownListHover.MissJumpTarget);
        Assert.Equal(0x004A3FB2u, RetailOptionsDropdownListHover.CurrentIndexStoreSite);
        Assert.Equal(0x20u, RetailOptionsDropdownListHover.CurrentIndexOffset);
        Assert.Equal(0x00523B50u, RetailOptionsDropdownListHover.PointInRect);
        Assert.Equal(0x004A4010u, RetailOptionsDropdownListHover.ClickHitSite);
        Assert.Equal(0x00469400u, RetailOptionsDropdownListHover.ClickHit);
        Assert.Equal(
            RetailOptionsDropdownListColor.RenderSite,
            RetailOptionsDropdownListHover.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownListColor.HoverHitSite,
            RetailOptionsDropdownListHover.HoverHitSite);
        Assert.Equal(
            RetailOptionsDropdownListColor.HoverHit,
            RetailOptionsDropdownListHover.HoverHit);
        Assert.Equal(
            RetailOptionsDropdownListColor.CurrentIndexOffset,
            RetailOptionsDropdownListHover.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListDest.PadGlobal,
            RetailOptionsDropdownListHover.PadGlobal);
        Assert.NotEqual(
            RetailOptionsDropdownListColor.IdleColorSite,
            RetailOptionsDropdownListHover.HoverHitSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDestY.DestYPushSite,
            RetailOptionsDropdownListHover.CurrentIndexStoreSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDest.DestLoadSite,
            RetailOptionsDropdownListHover.CurrentIndexStoreSite);
        Assert.True(RetailOptionsDropdownListHover.LeftStoreSite < RetailOptionsDropdownListHover.HoverHitSite);
        Assert.True(RetailOptionsDropdownListHover.HoverHitSite < RetailOptionsDropdownListHover.CurrentIndexStoreSite);
        Assert.True(RetailOptionsDropdownListHover.CurrentIndexStoreSite < RetailOptionsDropdownListHover.ClickHitSite);
        Assert.False(RetailOptionsDropdownListHover.InventsDestY5);
        Assert.False(RetailOptionsDropdownListHover.InventsDestX5);
        Assert.False(RetailOptionsDropdownListHover.InventsDestY268);
        Assert.False(RetailOptionsDropdownListHover.InventsDestY284);
        Assert.False(RetailOptionsDropdownListHover.InventsDestY304);
        Assert.False(RetailOptionsDropdownListHover.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListHover.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListHover.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListHover.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListHover.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListHover.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListHover.InventsSheen);
        Assert.False(RetailOptionsDropdownListHover.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListHover.InventsFade);
        Assert.True(RetailOptionsDropdownListHover.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListHover.IsHoverHit);
        Assert.False(RetailOptionsDropdownListHover.IsClickHit);
        Assert.False(RetailOptionsDropdownListHover.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListHover.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListHover.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListHover.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListHover.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListHover.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListHover.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListHover.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListHover.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownListColor.IsHoverHit);
    }

    [Fact]
    public void HoverWritesCurrentIndexWhenHitAndDoesNotInventDest()
    {
        Assert.Equal(2, RetailOptionsDropdownListHover.CurrentIndexAfterHover(0, 2, true));
        Assert.Equal(0, RetailOptionsDropdownListHover.CurrentIndexAfterHover(0, 2, false));
        Assert.Equal(1, RetailOptionsDropdownListHover.CurrentIndexAfterHover(1, 1, true));
        Assert.Equal(
            RetailOptionsDropdownListHover.CurrentIndexOffset,
            RetailOptionsDropdownListColor.CurrentIndexOffset);
        Assert.True(
            RetailOptionsDropdownListHover.Contains(323f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListHover.Contains(322.9f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListHover.Contains(403f, 200f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListHover.Contains(323f, 199.9f, 323f, 200f, 80, 16));
        Assert.False(
            RetailOptionsDropdownListHover.Contains(323f, 216f, 323f, 200f, 80, 16));
        Assert.Equal(
            RetailOptionsDropdownListDest.DestX(319f),
            RetailOptionsDropdownListHover.Left(319f));
        Assert.Equal(
            RetailOptionsDropdownListDestY.DestY(245f, 4, 16, 2),
            RetailOptionsDropdownListHover.Top(245f, 4, 16, 2));
        Assert.Equal(
            RetailOptionsDropdownListDest.DestX(319f) + 80,
            RetailOptionsDropdownListHover.Right(319f, 80));
        Assert.Equal(
            RetailOptionsDropdownListDestY.DestY(245f, 4, 16, 2) + 16,
            RetailOptionsDropdownListHover.Bottom(245f, 4, 16, 2));
        Assert.NotEqual(5f, RetailOptionsDropdownListHover.Left(319f));
        Assert.NotEqual(15.5f, RetailOptionsDropdownListHover.Top(245f, 4, 16, 0));
        Assert.NotEqual(148f, RetailOptionsDropdownListHover.Top(245f, 4, 16, 0));
        Assert.NotEqual(2f, RetailOptionsDropdownListHover.Left(319f));
        Assert.NotEqual(
            RetailOptionsDropdownPanelDest.Width(80),
            RetailOptionsDropdownListHover.Right(319f, 80) -
            RetailOptionsDropdownListHover.Left(319f));
        Assert.False(RetailOptionsDropdownListHover.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListHover.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListHover.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListHover.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListHover.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListHover.IsHoverHit);
        Assert.False(RetailOptionsDropdownListHover.IsClickHit);

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
        Assert.True(menu.HoverState(other));
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
        Assert.Equal(committed, menu.SelectedRow.CommittedIndex);
        Assert.False(menu.HoverState(other));
        Assert.False(menu.Hover(found + 1));
        Assert.True(menu.IsExpanded);
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
    }

    [Fact]
    public void HandleOptionsPointerMotionConsumesHoverAndDoesNotPileIntoConfirm()
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

        Assert.Contains("RetailOptionsDropdownListHover", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListHover.Contains", motion, StringComparison.Ordinal);
        Assert.Contains("HoverState", motion, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDest.DestX", motion, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListDestY.DestY", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverState", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListHover", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectState", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("15.5", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("148f", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("322.5", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownListHover", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListHover", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListHover", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListHover", click, StringComparison.Ordinal);
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        Assert.DoesNotContain("RetailOptionsDropdownListHover", pointerConfirm, StringComparison.Ordinal);
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
