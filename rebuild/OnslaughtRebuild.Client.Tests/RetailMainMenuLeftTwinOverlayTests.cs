// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D898 not/and/xor overlay at
/// <c>0x00463AD3</c>, recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x00463AD3</c> as the
/// not/and/xor pack on leftover left-twin 0x0089D898, not D894 /
/// D8A0 / D8A4. Official bytes independently re-read this cycle:
/// a copy of esi into eax at <c>0x00463AD3</c>,
/// a load of stack local +0x40 into ecx at <c>0x00463AD5</c>, then
/// a left shift by 8, then subtraction of esi, then push of ebp, then left shift by 16, then copy of eax into edx,
/// then push of constant 0x3F800000, then bitwise not of edx, then mask with 0x00FFFFFF, then xor with eax. Texture
/// load at <c>0x00463AF8</c> is a load of the value at 0x0089D898 into eax. Dest
/// immediates <c>0x00463B05</c>=344.0 / <c>0x00463B0A</c>=219.0.
/// The leftover 0.3 push at <c>0x00463B00</c> is CDXSurf Z
/// <c>0x3E99999A</c>, not scale and not a 29% title-logo. Then
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x00463B0F</c>. Dest is the leftover left-twin body, not
/// 0x0089D894 primary and not right. This is not the 0x00463E8D
/// twin-fade gate (that is D8A4). Shadow sibling 0x00463A8F is
/// already RetailMainMenuLeftTwinShadow. Settled 255 submits
/// <c>0xFEFFFFFF</c>, which is not capture BracketTint.
/// DrawMainMenu keeps BracketTint. Not a sheen (that is already
/// 0x00464343). Not SetLanguage. Not a Process increment. Do not
/// redo 0x00463873, 0x004638B7, 0x00463A8F, 0x00463D1F,
/// 0x00463D63, 0x00463F3F, or 0x00463F83. Do not redo the
/// RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuLeftTwinOverlayTests
{
    [Fact]
    public void SpecimenSitesAreTheNotAndXorOverlayOnDat0089D898()
    {
        Assert.Equal(0x00463AD3u, RetailMainMenuLeftTwinOverlay.Site);
        Assert.Equal(0x00463AD5u, RetailMainMenuLeftTwinOverlay.Esp40Site);
        Assert.Equal(0x00463AD9u, RetailMainMenuLeftTwinOverlay.ShiftSite);
        Assert.Equal(8, RetailMainMenuLeftTwinOverlay.ShiftLeft);
        Assert.Equal(0x00463ADCu, RetailMainMenuLeftTwinOverlay.SubSite);
        Assert.Equal(0x00463ADEu, RetailMainMenuLeftTwinOverlay.PushEbpSite);
        Assert.Equal(0x00463ADFu, RetailMainMenuLeftTwinOverlay.Shift16Site);
        Assert.Equal(0x00463AE2u, RetailMainMenuLeftTwinOverlay.CopySite);
        Assert.Equal(0x00463AE4u, RetailMainMenuLeftTwinOverlay.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuLeftTwinOverlay.ScaleBits);
        Assert.Equal(1f, RetailMainMenuLeftTwinOverlay.Scale);
        Assert.Equal(0x00463AE9u, RetailMainMenuLeftTwinOverlay.NotSite);
        Assert.Equal(0x00463AEDu, RetailMainMenuLeftTwinOverlay.AndSite);
        Assert.Equal(0x00463AF6u, RetailMainMenuLeftTwinOverlay.XorSite);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftTwinOverlay.RgbMask);
        Assert.Equal(0x00463AF8u, RetailMainMenuLeftTwinOverlay.TextureLoadSite);
        Assert.Equal(0x0089D898u, RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.TextureGlobal,
            RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuLeftDecorOverlay.TextureGlobal,
            RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.TextureGlobal,
            RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.TextureGlobal,
            RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.NotEqual(0x0089D894u, RetailMainMenuLeftTwinOverlay.TextureGlobal);
        Assert.Equal(0x00463B00u, RetailMainMenuLeftTwinOverlay.ZPushSite);
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftTwinOverlay.ZBits);
        Assert.Equal(0.3f, RetailMainMenuLeftTwinOverlay.Z);
        Assert.Equal(0x00463B05u, RetailMainMenuLeftTwinOverlay.YPushSite);
        Assert.Equal(344f, RetailMainMenuLeftTwinOverlay.DestY);
        Assert.Equal(0x00463B0Au, RetailMainMenuLeftTwinOverlay.XPushSite);
        Assert.Equal(219f, RetailMainMenuLeftTwinOverlay.DestX);
        Assert.Equal(4, RetailMainMenuLeftTwinOverlay.Mode);
        Assert.Equal(0x00463B0Fu, RetailMainMenuLeftTwinOverlay.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuLeftTwinOverlay.RenderSurface);
        Assert.Equal(0x00463A8Fu, RetailMainMenuLeftTwinOverlay.ShadowSiblingSite);
        Assert.Equal(255, RetailMainMenuLeftTwinOverlay.ImageSettledFadeByte);
        Assert.Equal(0xFE7F7F7Fu, RetailMainMenuLeftTwinOverlay.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuLeftTwinOverlay.SettledSubmitted);
        Assert.False(RetailMainMenuLeftTwinOverlay.IsSetLanguage);
        Assert.False(RetailMainMenuLeftTwinOverlay.IsButtonPressed);
        Assert.False(RetailMainMenuLeftTwinOverlay.InventsSheen);
        Assert.False(RetailMainMenuLeftTwinOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftTwinOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftTwinOverlay.ReplacesBracketTint);
        Assert.False(RetailMainMenuLeftTwinOverlay.ReplacesChromeTint);
        Assert.False(RetailMainMenuLeftTwinOverlay.ReplacesShadowTint);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesShadowSibling);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftTwinOverlay.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuLeftTwinShadow.Site,
            RetailMainMenuLeftTwinOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuLeftDecorOverlay.Site,
            RetailMainMenuLeftTwinOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.Site,
            RetailMainMenuLeftTwinOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.Site,
            RetailMainMenuLeftTwinOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.Site,
            RetailMainMenuLeftTwinOverlay.Site);
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.BodySiblingSite,
            RetailMainMenuLeftTwinOverlay.Site);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0xFEFFFFFFu,
            RetailMainMenuLeftTwinOverlay.SubmittedColor(
                RetailMainMenuLeftTwinOverlay.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftTwinOverlay.SubmittedColor(0));
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuLeftTwinOverlay.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftTwinOverlay.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuLeftTwinOverlay.CaptureDiffuse,
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailClickToStartTitle.BodyColor(3.0d),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLanguageChevronColor.FirstPack(255),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightDecorOverlay.SubmittedColor(255),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuLeftTwinShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E000000u, RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuLeftTwinOverlay.SubmittedColor(255));
    }

    [Fact]
    public void PushZeroPointThreeIsZNotScaleAndDestIsLeftTwinNotPrimaryOrRight()
    {
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftTwinOverlay.ZBits);
        Assert.Equal(
            0x3E99999Au,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuLeftTwinOverlay.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuLeftTwinOverlay.ScaleBits);
        Assert.Equal(1f, RetailMainMenuLeftTwinOverlay.Scale);
        Assert.NotEqual(0.3f, RetailMainMenuLeftTwinOverlay.DestX);
        Assert.NotEqual(0.29f, RetailMainMenuLeftTwinOverlay.Z);
        Assert.NotEqual(0.35f, RetailMainMenuLeftTwinOverlay.Z);
        Assert.NotEqual(0.29f, RetailMainMenuLeftTwinOverlay.Scale);
        Assert.Equal(
            RetailMainMenuLeftDecorOverlay.ZBits,
            RetailMainMenuLeftTwinOverlay.ZBits);
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuLeftTwinOverlay.ZBits);
        Assert.NotEqual(
            RetailMainMenuLeftTwinShadow.ZBits,
            RetailMainMenuLeftTwinOverlay.ZBits);
        Assert.Equal(219f, RetailMainMenuLeftTwinOverlay.DestX);
        Assert.Equal(344f, RetailMainMenuLeftTwinOverlay.DestY);
        Assert.NotEqual(457f, RetailMainMenuLeftTwinOverlay.DestX);
        Assert.NotEqual(355f, RetailMainMenuLeftTwinOverlay.DestY);
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.BodyDestX,
            RetailMainMenuLeftTwinOverlay.DestX);
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.BodyDestY,
            RetailMainMenuLeftTwinOverlay.DestY);
        Assert.Equal(
            RetailMainMenuLeftDecorOverlay.DestX,
            RetailMainMenuLeftTwinOverlay.DestX);
        Assert.Equal(
            RetailMainMenuLeftDecorOverlay.DestY,
            RetailMainMenuLeftTwinOverlay.DestY);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.DestX,
            RetailMainMenuLeftTwinOverlay.DestX);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.DestY,
            RetailMainMenuLeftTwinOverlay.DestY);
        Assert.False(RetailMainMenuLeftTwinOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftTwinOverlay.InventsSheen);
        Assert.False(RetailMainMenuLeftTwinOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftTwinOverlay.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesShadowSibling);
        Assert.False(RetailMainMenuLeftTwinOverlay.RedoesLeftDecorOverlay);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureBracketTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLeftTwinOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("BracketTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D898", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftTwinOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftTwinOverlay.ShouldDraw",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("219f, 344f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0xfe7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x3e7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x3e000000", flow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TitleLogoReflectionLayer", draw, StringComparison.Ordinal);
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
