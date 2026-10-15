// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay dest leftover and Z
/// after <c>0x0046416E</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuVersionOverlay already owns the sprintf at
/// <c>0x0046416E</c> and the settled pack at <c>0x004641B1</c> /
/// <c>0x004641B4</c>. Official bytes independently re-read this
/// cycle: <c>call 0x00515B00</c> at <c>0x004641C9</c> is
/// <c>PLATFORM__GetWindowHeight</c> (<c>mov eax, [0x00888A0C]; ret</c>).
/// That global sits past the 2,506,752-byte image, so
/// image-initial dest is not 464. <c>sub eax, 0x10</c> at
/// <c>0x004641CE</c> then <c>fild</c> / <c>fstp [esp]</c>. Dest X
/// is <c>push 0</c> at <c>0x004641E2</c>, not a 464 immediate.
/// <c>push 0x3C23D70A</c> at <c>0x004641C4</c> is Z 0.01, not
/// scale. Identity scale <c>push 0x3F800000</c> at
/// <c>0x004641BA</c> / <c>0x004641BF</c>. <c>push 1</c> at
/// <c>0x004641E4</c> then <c>CPlatform__Font</c>
/// (<c>0x00515A70</c>, <c>RET 4</c>) then
/// <c>CDXFont__DrawTextDynamic</c> (<c>0x00465710</c>,
/// <c>RET 0x28</c>) at <c>0x004641ED</c>. DrawMainMenu keeps
/// VersionTint, Format, DestX, DestY(DesignHeight), and scale 1.0.
/// Do not invent dest immediates. Do not invent a 2px kerning
/// hack. Do not redo title-logo dest/Z, title-logo shadow dest/Z,
/// selector-bar Z/X, writing Z/X, 0x00463873, 0x004638B7,
/// 0x00463A8F, 0x00463AD3, 0x00463D1F, 0x00463D63, 0x00463F3F,
/// or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayZTests
{
    [Fact]
    public void SpecimenSitesAreGetWindowHeightMinusSixteenAndPushZeroDestX()
    {
        Assert.Equal(0x004641C9u, RetailMainMenuVersionOverlayZ.HeightHelperSite);
        Assert.Equal(0x00515B00u, RetailMainMenuVersionOverlayZ.HeightHelper);
        Assert.Equal(0x00888A0Cu, RetailMainMenuVersionOverlayZ.HeightGlobal);
        Assert.Equal(0x004641CEu, RetailMainMenuVersionOverlayZ.DestYSubSite);
        Assert.Equal(0x10, RetailMainMenuVersionOverlayZ.DestYSubtract);
        Assert.Equal(0x004641E2u, RetailMainMenuVersionOverlayZ.DestXPushSite);
        Assert.Equal(0f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.Equal(0x004641C4u, RetailMainMenuVersionOverlayZ.ZPushSite);
        Assert.Equal(0x3C23D70Au, RetailMainMenuVersionOverlayZ.ZBits);
        Assert.Equal(0.01f, RetailMainMenuVersionOverlayZ.Z);
        Assert.Equal(0x004641BAu, RetailMainMenuVersionOverlayZ.ScalePushSite);
        Assert.Equal(0x004641BFu, RetailMainMenuVersionOverlayZ.ScaleYPushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuVersionOverlayZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuVersionOverlayZ.Scale);
        Assert.Equal(0x0088A0A8u, RetailMainMenuVersionOverlayZ.FontThis);
        Assert.Equal(0x00515A70u, RetailMainMenuVersionOverlayZ.FontHelper);
        Assert.Equal(0x004641E4u, RetailMainMenuVersionOverlayZ.FontSlotPushSite);
        Assert.Equal(1, RetailMainMenuVersionOverlayZ.FontSlot);
        Assert.Equal(0x004641EDu, RetailMainMenuVersionOverlayZ.CallSite);
        Assert.Equal(0x00465710u, RetailMainMenuVersionOverlayZ.DrawTextDynamic);
        Assert.Equal(0x0046416Eu, RetailMainMenuVersionOverlayZ.FormatSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlay.FormatSite,
            RetailMainMenuVersionOverlayZ.FormatSiblingSite);
        Assert.Equal(0x004641B1u, RetailMainMenuVersionOverlayZ.ColorSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlay.ShiftSite,
            RetailMainMenuVersionOverlayZ.ColorSiblingSite);
        Assert.False(RetailMainMenuVersionOverlayZ.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayZ.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuVersionOverlayZ.TreatsZAsScale);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesSelectorBarColor);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesWritingColor);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesWritingScroll);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesLeftTwinShadow);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesLeftTwinOverlay);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuVersionOverlayZ.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuVersionOverlay.FormatSite,
            RetailMainMenuVersionOverlayZ.HeightHelperSite);
        Assert.NotEqual(
            RetailMainMenuTitleLogoZ.ZPushSite,
            RetailMainMenuVersionOverlayZ.ZPushSite);
        Assert.NotEqual(
            RetailMainMenuTitleLogoShadowZ.ZPushSite,
            RetailMainMenuVersionOverlayZ.ZPushSite);
    }

    [Fact]
    public void PushZeroPointZeroOneIsZNotScaleAndDestYIsHelperMinusSixteenNotImmediate()
    {
        Assert.Equal(0x3C23D70Au, RetailMainMenuVersionOverlayZ.ZBits);
        Assert.Equal(
            0x3C23D70Au,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuVersionOverlayZ.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuVersionOverlayZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuVersionOverlayZ.Scale);
        Assert.NotEqual(RetailMainMenuVersionOverlayZ.Z, RetailMainMenuVersionOverlayZ.Scale);
        Assert.NotEqual(0.1f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(0.29f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(0.33f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(0.9f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(0.999f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(1.05f, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(0.01f, RetailMainMenuVersionOverlayZ.Scale);
        Assert.Equal(0f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.Equal(464f, RetailMainMenuVersionOverlayZ.DestY(480));
        Assert.Equal(-16f, RetailMainMenuVersionOverlayZ.DestY(0));
        Assert.NotEqual(130f, RetailMainMenuVersionOverlayZ.DestY(480));
        Assert.NotEqual(140f, RetailMainMenuVersionOverlayZ.DestY(480));
        Assert.NotEqual(219f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.NotEqual(320f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.NotEqual(325f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.NotEqual(458f, RetailMainMenuVersionOverlayZ.DestX);
        Assert.NotEqual(RetailMainMenuTitleLogoZ.Z, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(RetailMainMenuTitleLogoShadowZ.Z, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.Z, RetailMainMenuVersionOverlayZ.Z);
        Assert.NotEqual(RetailMainMenuWritingZ.Z, RetailMainMenuVersionOverlayZ.Z);
        Assert.Equal(
            RetailClickToStartProcessHead.GetTimeObject,
            RetailMainMenuVersionOverlayZ.FontThis);
        Assert.False(RetailMainMenuVersionOverlayZ.TreatsZAsScale);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayZ.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayZ.UsesTwinFadeGate);
        Assert.False(RetailMainMenuVersionOverlayZ.RedoesVersionOverlay);
        Assert.Equal(
            0x3F800000u,
            (uint)BitConverter.SingleToUInt32Bits(1f));
        Assert.NotEqual(
            RetailMainMenuVersionOverlayZ.ZBits,
            RetailMainMenuVersionOverlayZ.ScaleBits);
    }

    [Fact]
    public void DrawMainMenuKeepsVersionTintAndDoesNotTreatZeroPointZeroOneAsScaleOrDestImmediate()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlayZ", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlay.Format", draw, StringComparison.Ordinal);
        Assert.Contains("VersionTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuVersionOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("464f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DesignHeight - 16", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.01", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayZ", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayZ", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayZ", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayZ", bar, StringComparison.Ordinal);
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
