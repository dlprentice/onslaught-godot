// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> click-hit sound leftover
/// after <c>0x004A403A</c> <c>push 1</c> /
/// <c>0x004A403C</c> <c>call 0x00468770</c> /
/// <c>0x004A4041</c> <c>add esp, 4</c> — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A403A</c> <c>push 1</c>,
/// <c>0x004A403C</c> <c>call 0x00468770</c>,
/// <c>0x004A4041</c> <c>add esp, 4</c>.
/// That is <c>CFrontEnd__PlaySound(1)</c> Front End Select after the
/// click writes. Dest Y does not. Colour leftover already consults
/// currentIndex. Hover leftover already owns <c>0x004A3FA6</c>. Click
/// leftover already owns <c>0x004A4010</c>. Cancel leftover already
/// owns <c>0x004A4059</c>. Dest is not 15.5, 322.5, 148.0, or the 2.0
/// constant. HandleOptionsPointerConfirm consumes the leftover.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304,
/// dest from the 2.0 constant, wrap, fade, sheen, or a 2px kerning hack.
/// Do not change MeasureText. Do not redo dest leftovers, list colour,
/// list hover, list click, list cancel, Apply pulse, dropdown cosine,
/// language pitch, or the 0x00463669 compare. Do not invent latch-to-button
/// wiring of <c>0x0089BE28</c> as dest.</para>
/// </summary>
public sealed class RetailOptionsDropdownListClickSoundTests
{
    [Fact]
    public void SpecimenSitesAreExpandedListClickSoundNotDestColourHoverClickOrCancel()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownListClickSound.RenderSite);
        Assert.Equal(0x004A403Au, RetailOptionsDropdownListClickSound.SoundPushSite);
        Assert.Equal(1u, RetailOptionsDropdownListClickSound.SoundId);
        Assert.Equal(0x004A403Cu, RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.Equal(0x00468770u, RetailOptionsDropdownListClickSound.PlaySound);
        Assert.Equal(0x004A4041u, RetailOptionsDropdownListClickSound.SoundPopSite);
        Assert.Equal(4u, RetailOptionsDropdownListClickSound.SoundPop);
        Assert.Equal(0x004A4044u, RetailOptionsDropdownListClickSound.MissJumpTarget);
        Assert.Equal(0x004A4010u, RetailOptionsDropdownListClickSound.ClickHitSite);
        Assert.Equal(0x004A4037u, RetailOptionsDropdownListClickSound.SetCallSite);
        Assert.Equal(0x004A4059u, RetailOptionsDropdownListClickSound.CancelSite);
        Assert.Equal(0x004A3FA6u, RetailOptionsDropdownListClickSound.HoverHitSite);
        Assert.Equal(0x20u, RetailOptionsDropdownListClickSound.CurrentIndexOffset);
        Assert.Equal(0x24u, RetailOptionsDropdownListClickSound.ExpandByteOffset);
        Assert.Equal(
            RetailOptionsDropdownListClick.RenderSite,
            RetailOptionsDropdownListClickSound.RenderSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.PendingSkipTarget,
            RetailOptionsDropdownListClickSound.SoundPushSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.SetCallSite,
            RetailOptionsDropdownListClickSound.SetCallSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailOptionsDropdownListClickSound.ClickHitSite);
        Assert.Equal(
            RetailOptionsDropdownListClick.MissJumpTarget,
            RetailOptionsDropdownListClickSound.MissJumpTarget);
        Assert.Equal(
            RetailOptionsDropdownListCancel.PlaySound,
            RetailOptionsDropdownListClickSound.PlaySound);
        Assert.NotEqual(
            RetailOptionsDropdownListCancel.SoundId,
            RetailOptionsDropdownListClickSound.SoundId);
        Assert.NotEqual(
            RetailOptionsDropdownListCancel.SoundCallSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListClick.ClickHitSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListColor.IdleColorSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListDestY.DestYPushSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.NotEqual(
            RetailOptionsDropdownListCancel.HelperCallSite,
            RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.True(RetailOptionsDropdownListClickSound.ClickHitSite < RetailOptionsDropdownListClickSound.SetCallSite);
        Assert.True(RetailOptionsDropdownListClickSound.SetCallSite < RetailOptionsDropdownListClickSound.SoundPushSite);
        Assert.True(RetailOptionsDropdownListClickSound.SoundPushSite < RetailOptionsDropdownListClickSound.SoundCallSite);
        Assert.True(RetailOptionsDropdownListClickSound.SoundCallSite < RetailOptionsDropdownListClickSound.SoundPopSite);
        Assert.True(RetailOptionsDropdownListClickSound.SoundPopSite < RetailOptionsDropdownListClickSound.MissJumpTarget);
        Assert.True(RetailOptionsDropdownListClickSound.MissJumpTarget < RetailOptionsDropdownListClickSound.CancelSite);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY5);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestX5);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY268);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY284);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY304);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestX322_5);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownListClickSound.InventsKerningHack);
        Assert.False(RetailOptionsDropdownListClickSound.InventsSheen);
        Assert.False(RetailOptionsDropdownListClickSound.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownListClickSound.InventsFade);
        Assert.False(RetailOptionsDropdownListClickSound.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListClickSound.IsClickSound);
        Assert.False(RetailOptionsDropdownListClickSound.IsClickHit);
        Assert.False(RetailOptionsDropdownListClickSound.IsHoverHit);
        Assert.False(RetailOptionsDropdownListClickSound.IsCancel);
        Assert.False(RetailOptionsDropdownListClickSound.IsSetLanguage);
        Assert.False(RetailOptionsDropdownListClickSound.IsButtonPressed);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesMenuItemIconDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownValueDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownPanelDest);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListClick);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListCancel);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownListClickSound.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownListClickSound.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownListClickSound.ChangesMeasureText);
        Assert.True(RetailOptionsDropdownListClick.IsClickHit);
        Assert.True(RetailOptionsDropdownListCancel.IsCancel);
        Assert.True(RetailOptionsDropdownListHover.IsHoverHit);
    }

    [Fact]
    public void ClickSoundAppliesOnHitAndDoesNotInventDest()
    {
        Assert.True(RetailOptionsDropdownListClickSound.Applies(hit: true));
        Assert.False(RetailOptionsDropdownListClickSound.Applies(hit: false));
        Assert.Equal(1u, RetailOptionsDropdownListClickSound.SoundId);
        Assert.Equal(
            RetailFrontendAudioCue.Select,
            RetailOptionsDropdownListClickSound.Cue);
        Assert.NotEqual(
            RetailOptionsDropdownListCancel.SoundId,
            RetailOptionsDropdownListClickSound.SoundId);
        Assert.Equal(
            RetailOptionsDropdownListClickSound.CurrentIndexOffset,
            RetailOptionsDropdownListClick.CurrentIndexOffset);
        Assert.Equal(
            RetailOptionsDropdownListClickSound.ExpandByteOffset,
            RetailOptionsDropdownListDest.ExpandByteOffset);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestY15_5);
        Assert.False(RetailOptionsDropdownListClickSound.InventsDestFrom148);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListDestY);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListColor);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListHover);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListClick);
        Assert.False(RetailOptionsDropdownListClickSound.RedoesDropdownListCancel);
        Assert.False(RetailOptionsDropdownListClickSound.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownListClickSound.UsesCurrentIndex);
        Assert.True(RetailOptionsDropdownListClickSound.IsClickSound);
        Assert.False(RetailOptionsDropdownListClickSound.IsClickHit);
        Assert.False(RetailOptionsDropdownListClickSound.IsHoverHit);
        Assert.False(RetailOptionsDropdownListClickSound.IsCancel);

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
        Assert.True(RetailOptionsDropdownListClickSound.Applies(hit: true));
        Assert.Equal(RetailOptionsSignal.ValueChanged, menu.Confirm());
        Assert.False(menu.IsExpanded);
        Assert.Equal(other, menu.SelectedRow.CurrentIndex);
        Assert.Equal(other, menu.SelectedRow.CommittedIndex);
        Assert.NotEqual(committed, menu.SelectedRow.CommittedIndex);
    }

    [Fact]
    public void HandleOptionsPointerConfirmConsumesClickSoundAndDoesNotPileIntoMotionOrCancel()
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

        Assert.Contains("RetailOptionsDropdownListClickSound", confirm, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListClickSound.Applies", confirm, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownListClick.Contains", confirm, StringComparison.Ordinal);
        Assert.Contains("SelectState", confirm, StringComparison.Ordinal);
        Assert.Contains("ConfirmOptions", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("HoverState", confirm, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", click, StringComparison.Ordinal);
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        Assert.DoesNotContain("RetailOptionsDropdownListClickSound", pointerConfirm, StringComparison.Ordinal);
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
