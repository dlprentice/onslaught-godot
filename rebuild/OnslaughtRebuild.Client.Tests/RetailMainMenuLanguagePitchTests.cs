// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render language-row fall-through
/// increment at <c>0x00463647</c> — <c>fld [esp+0x10]</c>
/// then <c>fadd [0x005DB5D8]</c> (36.0) into the shared
/// tail — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004634EE</c> <c>jne 0x00463643</c>,
/// <c>0x0046363B</c> <c>call 0x005563D0</c> then
/// <c>0x00463640</c> <c>add esp, 0x58</c> fall through,
/// <c>0x00463643</c> <c>fld [esp+0x10]</c>,
/// <c>0x00463647</c> <c>fadd [0x005DB5D8]</c>,
/// <c>0x005DB5D8</c> is <c>00 00 10 42</c> (36.0),
/// <c>0x0046364D</c> shared tail <c>mov ebx, [esp+0x20]</c>,
/// <c>0x00463653</c> <c>fstp [esp+0x10]</c>,
/// <c>0x00463657</c> <c>inc ebx</c>. Labels
/// <c>jmp 0x0046364D</c> after their own <c>fadd [0x005D857C]</c>
/// (20.0). Nearby <c>push 0x438E0000</c> at
/// <c>0x00463636</c> is 284.0 and is not dest. Dest Y stays
/// the dest leftover. DrawMainMenu keeps dest Y as rowY-8
/// and regular rows at NonzeroSlotY. Do not invent dest
/// Y=268, dest Y=284, dest Y=304, wrap, fade, sheen, or a
/// 2px kerning hack. Do not change MeasureText. Do not redo
/// row-Y slot init, label dest, label DrawTextDynamic,
/// version DrawTextDynamic tail, writing Z/X, selector-bar
/// Z/X, 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuLanguagePitchTests
{
    [Fact]
    public void SpecimenSitesAreLanguageFallthroughNotDestImmediates()
    {
        Assert.Equal(0x00463643u, RetailMainMenuLanguagePitch.FldSite);
        Assert.Equal(0x00463647u, RetailMainMenuLanguagePitch.AddSite);
        Assert.Equal(0x005DB5D8u, RetailMainMenuLanguagePitch.PitchGlobal);
        Assert.Equal(0x42100000u, RetailMainMenuLanguagePitch.PitchBits);
        Assert.Equal(0x0046364Du, RetailMainMenuLanguagePitch.SharedTailSite);
        Assert.Equal(0x00463653u, RetailMainMenuLanguagePitch.FstpSite);
        Assert.Equal(0x00463657u, RetailMainMenuLanguagePitch.IncSite);
        Assert.Equal(0x004634EEu, RetailMainMenuLanguagePitch.ReachJneSite);
        Assert.Equal(0x00463643u, RetailMainMenuLanguagePitch.ReachJneTarget);
        Assert.Equal(0x0046363Bu, RetailMainMenuLanguagePitch.FallthroughCallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuLanguagePitch.RenderSurface);
        Assert.Equal(0x00463640u, RetailMainMenuLanguagePitch.FallthroughAddEspSite);
        Assert.Equal(0x58, RetailMainMenuLanguagePitch.FallthroughAddEspImmediate);
        Assert.Equal(0x00463636u, RetailMainMenuLanguagePitch.NearbyDestYPushSite);
        Assert.Equal(0x438E0000u, RetailMainMenuLanguagePitch.NearbyDestYBits);
        Assert.Equal(0x0046318Cu, RetailMainMenuLanguagePitch.LabelJmpSite);
        Assert.Equal(0x00463178u, RetailMainMenuLanguagePitch.LabelPitchAddSite);
        Assert.Equal(0x005D857Cu, RetailMainMenuLanguagePitch.LabelPitchGlobal);
        Assert.Equal(0x41A00000u, RetailMainMenuLanguagePitch.LabelPitchBits);
        Assert.Equal(
            RetailMainMenuRowY.SkippedPitchSite,
            RetailMainMenuLanguagePitch.AddSite);
        Assert.Equal(
            RetailMainMenuRowY.SkippedPitchGlobal,
            RetailMainMenuLanguagePitch.PitchGlobal);
        Assert.Equal(
            RetailMainMenuRowY.SkippedPitchBits,
            RetailMainMenuLanguagePitch.PitchBits);
        Assert.Equal(
            RetailMainMenuLabelText.PostCallJmpSite,
            RetailMainMenuLanguagePitch.LabelJmpSite);
        Assert.Equal(
            RetailMainMenuLabelText.PostCallJmpTarget,
            RetailMainMenuLanguagePitch.SharedTailSite);
        Assert.True(RetailMainMenuLanguagePitch.ReachJneSite < RetailMainMenuLanguagePitch.FldSite);
        Assert.True(RetailMainMenuLanguagePitch.NearbyDestYPushSite < RetailMainMenuLanguagePitch.FallthroughCallSite);
        Assert.True(RetailMainMenuLanguagePitch.FallthroughCallSite < RetailMainMenuLanguagePitch.FallthroughAddEspSite);
        Assert.True(RetailMainMenuLanguagePitch.FallthroughAddEspSite < RetailMainMenuLanguagePitch.FldSite);
        Assert.True(RetailMainMenuLanguagePitch.FldSite < RetailMainMenuLanguagePitch.AddSite);
        Assert.True(RetailMainMenuLanguagePitch.AddSite < RetailMainMenuLanguagePitch.SharedTailSite);
        Assert.True(RetailMainMenuLanguagePitch.SharedTailSite < RetailMainMenuLanguagePitch.FstpSite);
        Assert.True(RetailMainMenuLanguagePitch.FstpSite < RetailMainMenuLanguagePitch.IncSite);
        Assert.True(RetailMainMenuLanguagePitch.LabelPitchAddSite < RetailMainMenuLanguagePitch.LabelJmpSite);
        Assert.NotEqual(
            RetailMainMenuLanguagePitch.AddSite,
            RetailMainMenuLanguagePitch.SharedTailSite);
        Assert.NotEqual(
            RetailMainMenuLanguagePitch.NearbyDestYPushSite,
            RetailMainMenuLanguagePitch.FldSite);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY268);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY284);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY304);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestImmediates);
        Assert.False(RetailMainMenuLanguagePitch.InventsKerningHack);
        Assert.False(RetailMainMenuLanguagePitch.InventsSheen);
        Assert.False(RetailMainMenuLanguagePitch.InventsWrapWidth);
        Assert.False(RetailMainMenuLanguagePitch.InventsFade);
        Assert.False(RetailMainMenuLanguagePitch.IsSetLanguage);
        Assert.False(RetailMainMenuLanguagePitch.IsButtonPressed);
        Assert.False(RetailMainMenuLanguagePitch.RedoesRowY);
        Assert.False(RetailMainMenuLanguagePitch.RedoesLabelDest);
        Assert.False(RetailMainMenuLanguagePitch.RedoesLabelText);
        Assert.False(RetailMainMenuLanguagePitch.RedoesHitTest);
        Assert.False(RetailMainMenuLanguagePitch.RedoesLanguageSine);
        Assert.False(RetailMainMenuLanguagePitch.RedoesLanguageBlink);
        Assert.False(RetailMainMenuLanguagePitch.RedoesVersionOverlayTail);
        Assert.False(RetailMainMenuLanguagePitch.RedoesWritingZ);
        Assert.False(RetailMainMenuLanguagePitch.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuLanguagePitch.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLanguagePitch.ChangesMeasureText);
    }

    [Fact]
    public void LanguageFallthroughAddsThirtySixAndIsNotDest()
    {
        Assert.Equal(36f, RetailMainMenuLanguagePitch.Pitch);
        Assert.Equal(20f, RetailMainMenuLanguagePitch.LabelPitch);
        Assert.Equal(284f, RetailMainMenuLanguagePitch.NearbyDestY);
        Assert.Equal(304f, RetailMainMenuLanguagePitch.NextRegularSlotY);
        Assert.Equal(
            RetailMainMenuRowY.NonzeroSlotY,
            RetailMainMenuLanguagePitch.NextRegularSlotY);
        Assert.Equal(
            RetailMainMenuRowY.LanguageSlotY + RetailMainMenuLanguagePitch.Pitch,
            RetailMainMenuLanguagePitch.NextRegularSlotY);
        Assert.Equal(
            304f,
            RetailMainMenuLanguagePitch.NextSlotY(RetailMainMenuRowY.LanguageSlotY));
        Assert.Equal(
            340f,
            RetailMainMenuLanguagePitch.NextSlotY(RetailMainMenuRowY.NonzeroSlotY));
        Assert.Equal(
            RetailMainMenuRowY.SkippedPitch,
            RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(
            RetailMainMenuLanguagePitch.NearbyDestY,
            RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(
            RetailMainMenuLanguagePitch.LabelPitch,
            RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(268f, RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(284f, RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(304f, RetailMainMenuLanguagePitch.Pitch);
        Assert.NotEqual(268f, RetailMainMenuLabelDest.DestY(268f, 16));
        Assert.NotEqual(284f, RetailMainMenuLabelDest.DestY(284f, 16));
        Assert.NotEqual(304f, RetailMainMenuLabelDest.DestY(304f, 16));
        Assert.Equal(260f, RetailMainMenuLabelDest.DestY(RetailMainMenuRowY.LanguageSlotY, 16));
        Assert.Equal(296f, RetailMainMenuLabelDest.DestY(RetailMainMenuRowY.NonzeroSlotY, 16));
        Assert.NotEqual(
            RetailMainMenuHitTest.LanguageHoverHalfExtent,
            RetailMainMenuLanguagePitch.Pitch);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY268);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY284);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestY304);
        Assert.False(RetailMainMenuLanguagePitch.InventsDestImmediates);
        Assert.False(RetailMainMenuLanguagePitch.ChangesMeasureText);
    }

    [Fact]
    public void DrawMainMenuConsumesLanguagePitchAndDoesNotInventDestY()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLanguagePitch", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRowY.NonzeroSlotY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelDest.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("MeasureText", draw, StringComparison.Ordinal);
        Assert.Contains("DrawText(", draw, StringComparison.Ordinal);
        Assert.Contains("rowY - 8f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLabelDest.DestY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLanguagePitch.Pitch", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLanguagePitch.NextRegularSlotY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLanguagePitch.NearbyDestY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuRowY.LanguageSlotY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DestY(268", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DestY(284", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DestY(304", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("36f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.32", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000.0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x447A0000", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x3EA3D70A", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x438E0000", draw, StringComparison.OrdinalIgnoreCase);
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
        Assert.DoesNotContain("RetailMainMenuLanguagePitch", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuLanguagePitch", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuLanguagePitch", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuLanguagePitch", bar, StringComparison.Ordinal);
        string language = Slice(flow, "private void DrawLanguageSelector");
        Assert.DoesNotContain("RetailMainMenuLanguagePitch", language, StringComparison.Ordinal);
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
