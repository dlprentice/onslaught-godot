// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render row-Y slot init at
/// <c>0x00462E96</c> / <c>0x00462EF5</c> — 268.0 and
/// index -1, or 304.0 and index 0 when
/// <c>[0x0083D990]</c> is nonzero — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00462E96</c> <c>mov [esp+0x10], 0x43860000</c>
/// (268.0), <c>0x00462EE7</c> <c>mov eax, [0x0083D990]</c>,
/// <c>0x00462EEC</c> <c>or ebp, -1</c>,
/// <c>0x00462EEF</c> <c>test eax, eax</c>,
/// <c>0x00462EF1</c> <c>je 0x00462EFD</c>,
/// <c>0x00462EF3</c> <c>xor ebp, ebp</c>,
/// <c>0x00462EF5</c> <c>mov [esp+0x10], 0x43980000</c>
/// (304.0). <c>pe_read_va</c> of <c>0x0083D990</c> is
/// uninitialised <c>.data</c>. <c>0x005DB5D8</c> is
/// <c>00 00 10 42</c> (36.0); labels <c>jmp 0x0046364D</c>
/// skip that add. Dest Y stays the dest leftover. DrawMainMenu
/// keeps dest Y as rowY-8. Do not invent dest Y=268 or
/// dest Y=304. Do not invent dest, wrap, fade, sheen, or a
/// 2px kerning hack. Do not redo label dest, label
/// DrawTextDynamic, version DrawTextDynamic tail, writing
/// Z/X, selector-bar Z/X, 0x00463873, 0x004638B7,
/// 0x00463A8F, 0x00463AD3, 0x00463D1F, 0x00463D63,
/// 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuRowYTests
{
    [Fact]
    public void SpecimenSitesAreSlotInitNotDestImmediates()
    {
        Assert.Equal(0x00462E96u, RetailMainMenuRowY.SeedSite);
        Assert.Equal(0x43860000u, RetailMainMenuRowY.LanguageSlotBits);
        Assert.Equal(0x00462EE7u, RetailMainMenuRowY.FlagLoadSite);
        Assert.Equal(0x0083D990u, RetailMainMenuRowY.FlagGlobal);
        Assert.Equal(0x00462EECu, RetailMainMenuRowY.LanguageIndexSite);
        Assert.Equal(-1, RetailMainMenuRowY.LanguageIndex);
        Assert.Equal(0x00462EEFu, RetailMainMenuRowY.FlagTestSite);
        Assert.Equal(0x00462EF1u, RetailMainMenuRowY.FlagJeSite);
        Assert.Equal(0x00462EFDu, RetailMainMenuRowY.FlagJeTarget);
        Assert.Equal(0x00462EF3u, RetailMainMenuRowY.RegularIndexSite);
        Assert.Equal(0, RetailMainMenuRowY.RegularIndex);
        Assert.Equal(0x00462EF5u, RetailMainMenuRowY.NonzeroSlotSite);
        Assert.Equal(0x43980000u, RetailMainMenuRowY.NonzeroSlotBits);
        Assert.Equal(0u, RetailMainMenuRowY.ImageInitialFlag);
        Assert.Equal(0x00463647u, RetailMainMenuRowY.SkippedPitchSite);
        Assert.Equal(0x005DB5D8u, RetailMainMenuRowY.SkippedPitchGlobal);
        Assert.Equal(0x42100000u, RetailMainMenuRowY.SkippedPitchBits);
        Assert.Equal(
            RetailMainMenuHitTest.LanguageCenterFlagGlobal,
            RetailMainMenuRowY.FlagGlobal);
        Assert.Equal(
            RetailMainMenuLabelText.PostCallJmpTarget,
            0x0046364Du);
        Assert.True(RetailMainMenuRowY.SeedSite < RetailMainMenuRowY.FlagLoadSite);
        Assert.True(RetailMainMenuRowY.FlagJeSite < RetailMainMenuRowY.RegularIndexSite);
        Assert.True(RetailMainMenuRowY.RegularIndexSite < RetailMainMenuRowY.NonzeroSlotSite);
        Assert.True(RetailMainMenuRowY.NonzeroSlotSite < RetailMainMenuRowY.FlagJeTarget);
        Assert.True(RetailMainMenuRowY.FlagJeTarget < RetailMainMenuLabelDest.SizeLeaSite);
        Assert.True(RetailMainMenuRowY.SkippedPitchSite < RetailMainMenuLabelText.PostCallJmpTarget);
        Assert.False(RetailMainMenuRowY.InventsDestY268);
        Assert.False(RetailMainMenuRowY.InventsDestY304);
        Assert.False(RetailMainMenuRowY.InventsDestImmediates);
        Assert.False(RetailMainMenuRowY.InventsSkippedPitchAsDest);
        Assert.False(RetailMainMenuRowY.InventsKerningHack);
        Assert.False(RetailMainMenuRowY.InventsSheen);
        Assert.False(RetailMainMenuRowY.InventsWrapWidth);
        Assert.False(RetailMainMenuRowY.InventsFade);
        Assert.False(RetailMainMenuRowY.IsSetLanguage);
        Assert.False(RetailMainMenuRowY.IsButtonPressed);
        Assert.False(RetailMainMenuRowY.RedoesLabelDest);
        Assert.False(RetailMainMenuRowY.RedoesLabelText);
        Assert.False(RetailMainMenuRowY.RedoesHitTest);
        Assert.False(RetailMainMenuRowY.RedoesVersionOverlayTail);
        Assert.False(RetailMainMenuRowY.RedoesWritingZ);
        Assert.False(RetailMainMenuRowY.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuRowY.UsesTwinFadeGate);
        Assert.False(RetailMainMenuRowY.ChangesMeasureText);
    }

    [Fact]
    public void SlotIsTwoSixtyEightOrThreeOhFourAndIsNotDest()
    {
        Assert.Equal(268f, RetailMainMenuRowY.LanguageSlotY);
        Assert.Equal(304f, RetailMainMenuRowY.NonzeroSlotY);
        Assert.Equal(36f, RetailMainMenuRowY.SkippedPitch);
        Assert.Equal(268f, RetailMainMenuRowY.SlotY(0u));
        Assert.Equal(304f, RetailMainMenuRowY.SlotY(1u));
        Assert.Equal(-1, RetailMainMenuRowY.StartingIndex(0u));
        Assert.Equal(0, RetailMainMenuRowY.StartingIndex(1u));
        Assert.Equal(
            RetailMainMenuHitTest.LanguageHoverCenterY,
            RetailMainMenuRowY.LanguageSlotY);
        Assert.Equal(
            RetailMainMenuHitTest.AlternateLanguageHoverCenterY,
            RetailMainMenuRowY.NonzeroSlotY);
        Assert.Equal(
            RetailMainMenuHitTest.LanguageHoverCenterYFor(0u),
            RetailMainMenuRowY.SlotY(0u));
        Assert.Equal(
            RetailMainMenuHitTest.LanguageHoverCenterYFor(1u),
            RetailMainMenuRowY.SlotY(1u));
        Assert.NotEqual(268f, RetailMainMenuLabelDest.DestY(268f, 16));
        Assert.NotEqual(304f, RetailMainMenuLabelDest.DestY(304f, 16));
        Assert.Equal(260f, RetailMainMenuLabelDest.DestY(RetailMainMenuRowY.LanguageSlotY, 16));
        Assert.Equal(296f, RetailMainMenuLabelDest.DestY(RetailMainMenuRowY.NonzeroSlotY, 16));
        Assert.NotEqual(
            RetailMainMenuHitTest.LanguageHoverHalfExtent,
            RetailMainMenuRowY.SkippedPitch);
        Assert.False(RetailMainMenuRowY.InventsDestY268);
        Assert.False(RetailMainMenuRowY.InventsDestY304);
        Assert.False(RetailMainMenuRowY.InventsSkippedPitchAsDest);
        Assert.False(RetailMainMenuRowY.InventsDestImmediates);
        Assert.False(RetailMainMenuRowY.ChangesMeasureText);
    }

    [Fact]
    public void DrawMainMenuConsumesNonzeroSlotAndDoesNotInventDestY()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuRowY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRowY.NonzeroSlotY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelDest.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("MeasureText", draw, StringComparison.Ordinal);
        Assert.Contains("DrawText(", draw, StringComparison.Ordinal);
        Assert.Contains("rowY - 8f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLabelDest.DestY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuRowY.LanguageSlotY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuRowY.SlotY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DestY(268", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DestY(304", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("36f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.32", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000.0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x447A0000", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x3EA3D70A", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(" - 2", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuRowY", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuRowY", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuRowY", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuRowY", bar, StringComparison.Ordinal);
        string language = Slice(flow, "private void DrawLanguageSelector");
        Assert.DoesNotContain("RetailMainMenuRowY", language, StringComparison.Ordinal);
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
