// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>PlatformInput__PollMouseState</c> latch-to-button SET of
/// <c>0x0089BE28</c> after <c>0x0042D5CA</c> <c>test ah, 0x80</c> /
/// <c>0x0042D5CD</c> <c>je +0x0e</c> /
/// <c>0x0042D5CF</c> <c>mov [0x0089BE28], ecx</c> — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x0042D4D0</c> <c>PlatformInput__PollMouseState</c>,
/// <c>0x0042D4D6</c> <c>xor ebx, ebx</c> (<c>33 db</c>),
/// <c>0x0042D58F</c> <c>mov ecx, 1</c> (<c>b9 01 00 00 00</c>),
/// <c>0x0042D5CA</c> <c>test ah, 0x80</c> (<c>f6 c4 80</c>),
/// <c>0x0042D5CD</c> <c>je +0x0e</c> (target <c>0x0042D5DD</c>),
/// <c>0x0042D5CF</c> unique <c>mov [0x0089BE28], ecx</c>
/// (<c>89 0d 28 be 89 00</c>, one image hit). Cycle 85 retargets the
/// five published sites that sat mid-instruction (0x0042D590 / 0x0042D5C5 /
/// 0x0042D5C8 / 0x0042D5D8 / 0x0042D5CA-as-SET). That is the
/// right-mouse latch SET. Dest Y does not. Colour leftover already
/// consults currentIndex. Hover leftover already owns <c>0x004A3FA6</c>.
/// Click leftover already owns <c>0x004A4010</c>. Cancel leftover
/// already owns <c>0x004A4059</c> load/clear. Click-hit sound leftover
/// already owns <c>0x004A403C</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. Dest is not 15.5, 322.5, 148.0, or the 2.0
/// constant. HandleOptionsPointerCancel consumes the leftover as the
/// latch fed into cancel. Do not invent dest Y=5, dest X=5, dest Y=268,
/// dest Y=284, dest Y=304, dest from the 2.0 constant, wrap, fade,
/// sheen, or a 2px kerning hack. Do not change MeasureText. Do not redo
/// dest leftovers, list colour, list hover, list click, list cancel,
/// click-hit sound, Apply pulse, dropdown cosine, language pitch, or
/// the 0x00463669 compare. Do not invent dest from 148.0. Do not invent
/// that the third FMV latch is dest.</para>
/// </summary>
public sealed class RetailFrontendLatchToButtonTests
{
    [Fact]
    public void SpecimenSitesAreRightMouseLatchSetNotDestColourHoverClickCancelOrFmvOr()
    {
        Assert.Equal(0x0042D4D0u, RetailFrontendLatchToButton.PollSite);
        Assert.Equal(0x0042D4D6u, RetailFrontendLatchToButton.EbxZeroSite);
        Assert.Equal(0x0042D58Fu, RetailFrontendLatchToButton.OneLoadSite);
        Assert.Equal(1u, RetailFrontendLatchToButton.SetValue);
        Assert.Equal(0x0042D5CAu, RetailFrontendLatchToButton.RightMaskSite);
        Assert.Equal(0x80u, RetailFrontendLatchToButton.RightButtonMask);
        Assert.Equal(0x0042D5CDu, RetailFrontendLatchToButton.RightSkipSite);
        Assert.Equal(0x0042D5DDu, RetailFrontendLatchToButton.RightMissTarget);
        Assert.Equal(0x0042D5CFu, RetailFrontendLatchToButton.RightSetSite);
        Assert.Equal(0x0089BE28u, RetailFrontendLatchToButton.Latch);
        Assert.Equal(0x0089BDF8u, RetailFrontendLatchToButton.LeftLatch);
        Assert.Equal(0x0089BE10u, RetailFrontendLatchToButton.MiddleLatch);
        Assert.Equal(0x0053F2EBu, RetailFrontendLatchToButton.FmvOrSite);
        Assert.Equal(0x004A4068u, RetailFrontendLatchToButton.CancelLoadSite);
        Assert.Equal(0x004A407Du, RetailFrontendLatchToButton.CancelClearSite);
        Assert.Equal(0x004A403Cu, RetailFrontendLatchToButton.ClickSoundSite);
        Assert.Equal(
            RetailOptionsDropdownListCancel.Latch,
            RetailFrontendLatchToButton.Latch);
        Assert.Equal(
            RetailOptionsDropdownListCancel.LatchLoadSite,
            RetailFrontendLatchToButton.CancelLoadSite);
        Assert.Equal(
            RetailOptionsDropdownListCancel.LatchClearSite,
            RetailFrontendLatchToButton.CancelClearSite);
        Assert.Equal(
            RetailOptionsDropdownListClickSound.SoundCallSite,
            RetailFrontendLatchToButton.ClickSoundSite);
        Assert.NotEqual(
            RetailFrontendLatchToButton.RightSetSite,
            RetailFrontendLatchToButton.FmvOrSite);
        Assert.NotEqual(
            RetailFrontendLatchToButton.RightSetSite,
            RetailFrontendLatchToButton.CancelLoadSite);
        Assert.NotEqual(
            RetailFrontendLatchToButton.RightSetSite,
            RetailFrontendLatchToButton.CancelClearSite);
        Assert.NotEqual(
            RetailFrontendLatchToButton.RightSetSite,
            RetailFrontendLatchToButton.ClickSoundSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailFrontendLatchToButton.RightSetSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailFrontendLatchToButton.RightSetSite);
        Assert.True(RetailFrontendLatchToButton.PollSite < RetailFrontendLatchToButton.EbxZeroSite);
        Assert.True(RetailFrontendLatchToButton.EbxZeroSite < RetailFrontendLatchToButton.OneLoadSite);
        Assert.True(RetailFrontendLatchToButton.OneLoadSite < RetailFrontendLatchToButton.RightMaskSite);
        Assert.True(RetailFrontendLatchToButton.RightMaskSite < RetailFrontendLatchToButton.RightSkipSite);
        Assert.True(RetailFrontendLatchToButton.RightSkipSite < RetailFrontendLatchToButton.RightSetSite);
        Assert.True(RetailFrontendLatchToButton.RightSetSite < RetailFrontendLatchToButton.RightMissTarget);
        Assert.False(RetailFrontendLatchToButton.InventsDestY5);
        Assert.False(RetailFrontendLatchToButton.InventsDestX5);
        Assert.False(RetailFrontendLatchToButton.InventsDestY268);
        Assert.False(RetailFrontendLatchToButton.InventsDestY284);
        Assert.False(RetailFrontendLatchToButton.InventsDestY304);
        Assert.False(RetailFrontendLatchToButton.InventsDestFromPad);
        Assert.False(RetailFrontendLatchToButton.InventsDestY15_5);
        Assert.False(RetailFrontendLatchToButton.InventsDestX322_5);
        Assert.False(RetailFrontendLatchToButton.InventsDestFrom148);
        Assert.False(RetailFrontendLatchToButton.InventsDestImmediates);
        Assert.False(RetailFrontendLatchToButton.InventsKerningHack);
        Assert.False(RetailFrontendLatchToButton.InventsSheen);
        Assert.False(RetailFrontendLatchToButton.InventsWrapWidth);
        Assert.False(RetailFrontendLatchToButton.InventsFade);
        Assert.False(RetailFrontendLatchToButton.UsesCurrentIndex);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
        Assert.False(RetailFrontendLatchToButton.IsFmvSkip);
        Assert.False(RetailFrontendLatchToButton.IsClickSound);
        Assert.False(RetailFrontendLatchToButton.IsClickHit);
        Assert.False(RetailFrontendLatchToButton.IsHoverHit);
        Assert.False(RetailFrontendLatchToButton.IsCancel);
        Assert.False(RetailFrontendLatchToButton.IsSetLanguage);
        Assert.False(RetailFrontendLatchToButton.IsButtonPressed);
        Assert.False(RetailFrontendLatchToButton.RedoesMenuItemDest);
        Assert.False(RetailFrontendLatchToButton.RedoesMenuItemIconDest);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownDest);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownValueDest);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListDest);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownPanelDest);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListDestY);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListColor);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListHover);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListClick);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListCancel);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListClickSound);
        Assert.False(RetailFrontendLatchToButton.RedoesMenuItemColor);
        Assert.False(RetailFrontendLatchToButton.RedoesApplyPulse);
        Assert.False(RetailFrontendLatchToButton.RedoesLanguagePitch);
        Assert.False(RetailFrontendLatchToButton.UsesTwinFadeGate);
        Assert.False(RetailFrontendLatchToButton.UsesLanguageCompare);
        Assert.False(RetailFrontendLatchToButton.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListCancel.IsCancel);
        Assert.True(RetailOptionsDropdownListClickSound.IsClickSound);
        Assert.True(RetailOptionsDropdownListClick.IsClickHit);
        Assert.True(RetailOptionsDropdownListHover.IsHoverHit);
    }

    [Fact]
    public void LatchSetAppliesOnRightDownAndDoesNotInventDest()
    {
        Assert.True(RetailFrontendLatchToButton.Set(rightDown: true));
        Assert.False(RetailFrontendLatchToButton.Set(rightDown: false));
        Assert.Equal(1u, RetailFrontendLatchToButton.SetValue);
        Assert.Equal(0x80u, RetailFrontendLatchToButton.RightButtonMask);
        Assert.Equal(
            RetailOptionsDropdownListCancel.Latch,
            RetailFrontendLatchToButton.Latch);
        Assert.True(
            RetailOptionsDropdownListCancel.Applies(
                helperNonzero: false,
                latch: RetailFrontendLatchToButton.Set(rightDown: true)));
        Assert.False(
            RetailOptionsDropdownListCancel.Applies(
                helperNonzero: false,
                latch: RetailFrontendLatchToButton.Set(rightDown: false)));
        Assert.False(
            RetailOptionsDropdownListCancel.Applies(
                helperNonzero: true,
                latch: RetailFrontendLatchToButton.Set(rightDown: true)));
        Assert.False(RetailFrontendLatchToButton.InventsDestY15_5);
        Assert.False(RetailFrontendLatchToButton.InventsDestFrom148);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListDestY);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListColor);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListHover);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListClick);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListCancel);
        Assert.False(RetailFrontendLatchToButton.RedoesDropdownListClickSound);
        Assert.False(RetailFrontendLatchToButton.ChangesMeasureText);
        Assert.False(RetailFrontendLatchToButton.UsesCurrentIndex);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
        Assert.False(RetailFrontendLatchToButton.IsFmvSkip);
        Assert.False(RetailFrontendLatchToButton.IsClickHit);
        Assert.False(RetailFrontendLatchToButton.IsHoverHit);
        Assert.False(RetailFrontendLatchToButton.IsCancel);
        Assert.False(RetailFrontendLatchToButton.IsClickSound);

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
        Assert.True(RetailFrontendLatchToButton.Set(rightDown: true));
        Assert.True(menu.CancelExpanded());
        Assert.False(menu.IsExpanded);
        Assert.Equal(committed, menu.SelectedRow.CurrentIndex);
        Assert.Equal(committed, menu.SelectedRow.CommittedIndex);
    }

    [Fact]
    public void HandleOptionsPointerCancelConsumesLatchSetAndDoesNotPileIntoMotionOrClick()
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
        Assert.DoesNotContain("RetailFrontendLatchToButton", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendLatchToButton", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverState", cancel, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailFrontendLatchToButton", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailFrontendLatchToButton", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailFrontendLatchToButton", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailFrontendLatchToButton", click, StringComparison.Ordinal);
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        Assert.DoesNotContain("RetailFrontendLatchToButton", pointerConfirm, StringComparison.Ordinal);
        string handleKey = Slice(flow, "private bool HandleKey(");
        Assert.DoesNotContain("RetailFrontendLatchToButton", handleKey, StringComparison.Ordinal);
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
