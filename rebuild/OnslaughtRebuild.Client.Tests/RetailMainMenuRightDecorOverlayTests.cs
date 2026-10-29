// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D8A0 not/and/xor overlay at
/// <c>0x00463D63</c>, recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x00463D63</c> as the
/// not/and/xor pack on 0x0089D8A0, not D8A4. Official bytes:
/// a copy of esi into eax at <c>0x00463D63</c>,
/// a load of stack local +0x40 into ecx at <c>0x00463D65</c>, then
/// a left shift by 8, then subtraction of esi, then push of ebp, then left shift by 16, then copy of
/// eax into edx, then bitwise not of edx, then mask with 0x00FFFFFF, then xor with eax. Texture load at
/// <c>0x00463D88</c> is a load of the value at 0x0089D8A0 into eax. Dest
/// immediates <c>0x00463D95</c>=355.0 / <c>0x00463D9A</c>=457.0.
/// The leftover 0.3 push at <c>0x00463D90</c> is CDXSurf Z
/// <c>0x3E99999A</c>, not scale and not a 29% title-logo. Then
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x00463D9F</c>. Dest is the right-arc body, not left.
/// This is not the 0x00463E8D twin-fade gate (that is D8A4).
/// Settled 255 submits <c>0xFEFFFFFF</c>, which is not capture
/// BracketTint. DrawMainMenu keeps BracketTint. Not a sheen
/// (that is already 0x00464343). Not SetLanguage. Not a Process
/// increment. Do not redo 0x00463D1F, 0x00463F3F, or 0x00463F83.
/// Do not redo the RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuRightDecorOverlayTests
{
    [Fact]
    public void SpecimenSitesAreTheNotAndXorOverlayOnDat0089D8A0()
    {
        Assert.Equal(0x00463D63u, RetailMainMenuRightDecorOverlay.Site);
        Assert.Equal(0x00463D65u, RetailMainMenuRightDecorOverlay.Esp40Site);
        Assert.Equal(0x00463D69u, RetailMainMenuRightDecorOverlay.ShiftSite);
        Assert.Equal(8, RetailMainMenuRightDecorOverlay.ShiftLeft);
        Assert.Equal(0x00463D6Cu, RetailMainMenuRightDecorOverlay.SubSite);
        Assert.Equal(0x00463D6Eu, RetailMainMenuRightDecorOverlay.PushEbpSite);
        Assert.Equal(0x00463D6Fu, RetailMainMenuRightDecorOverlay.Shift16Site);
        Assert.Equal(0x00463D72u, RetailMainMenuRightDecorOverlay.CopySite);
        Assert.Equal(0x00463D79u, RetailMainMenuRightDecorOverlay.NotSite);
        Assert.Equal(0x00463D7Du, RetailMainMenuRightDecorOverlay.AndSite);
        Assert.Equal(0x00463D86u, RetailMainMenuRightDecorOverlay.XorSite);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightDecorOverlay.RgbMask);
        Assert.Equal(0x00463D88u, RetailMainMenuRightDecorOverlay.TextureLoadSite);
        Assert.Equal(0x0089D8A0u, RetailMainMenuRightDecorOverlay.TextureGlobal);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.TextureGlobal,
            RetailMainMenuRightDecorOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.TextureGlobal,
            RetailMainMenuRightDecorOverlay.TextureGlobal);
        Assert.Equal(0x00463D90u, RetailMainMenuRightDecorOverlay.ZPushSite);
        Assert.Equal(0x3E99999Au, RetailMainMenuRightDecorOverlay.ZBits);
        Assert.Equal(0.3f, RetailMainMenuRightDecorOverlay.Z);
        Assert.Equal(0x00463D95u, RetailMainMenuRightDecorOverlay.YPushSite);
        Assert.Equal(355f, RetailMainMenuRightDecorOverlay.DestY);
        Assert.Equal(0x00463D9Au, RetailMainMenuRightDecorOverlay.XPushSite);
        Assert.Equal(457f, RetailMainMenuRightDecorOverlay.DestX);
        Assert.Equal(4, RetailMainMenuRightDecorOverlay.Mode);
        Assert.Equal(0x00463D9Fu, RetailMainMenuRightDecorOverlay.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuRightDecorOverlay.RenderSurface);
        Assert.Equal(0x00463E8Du, RetailMainMenuRightDecorOverlay.TwinGateSite);
        Assert.Equal(255, RetailMainMenuRightDecorOverlay.ImageSettledFadeByte);
        Assert.Equal(0xFE7F7F7Fu, RetailMainMenuRightDecorOverlay.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuRightDecorOverlay.SettledSubmitted);
        Assert.False(RetailMainMenuRightDecorOverlay.IsSetLanguage);
        Assert.False(RetailMainMenuRightDecorOverlay.IsButtonPressed);
        Assert.False(RetailMainMenuRightDecorOverlay.InventsSheen);
        Assert.False(RetailMainMenuRightDecorOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuRightDecorOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuRightDecorOverlay.ReplacesBracketTint);
        Assert.False(RetailMainMenuRightDecorOverlay.ReplacesChromeTint);
        Assert.False(RetailMainMenuRightDecorOverlay.ReplacesShadowTint);
        Assert.False(RetailMainMenuRightDecorOverlay.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuRightDecorOverlay.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuRightDecorOverlay.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuRightDecorOverlay.RedoesDecorShadow);
        Assert.False(RetailMainMenuRightDecorOverlay.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.Site,
            RetailMainMenuRightDecorOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.Site,
            RetailMainMenuRightDecorOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.Site,
            RetailMainMenuRightDecorOverlay.Site);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.BodySiblingSite,
            RetailMainMenuRightDecorOverlay.Site);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0xFEFFFFFFu,
            RetailMainMenuRightDecorOverlay.SubmittedColor(
                RetailMainMenuRightDecorOverlay.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightDecorOverlay.SubmittedColor(0));
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuRightDecorOverlay.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightDecorOverlay.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.CaptureDiffuse,
            RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailClickToStartTitle.BodyColor(3.0d),
            RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLanguageChevronColor.FirstPack(255),
            RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.SubmittedColor(255),
            RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E000000u, RetailMainMenuRightDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuRightDecorOverlay.SubmittedColor(255));
    }

    [Fact]
    public void PushZeroPointThreeIsZNotScaleAndDestIsRightArcNotLeft()
    {
        Assert.Equal(0x3E99999Au, RetailMainMenuRightDecorOverlay.ZBits);
        Assert.Equal(
            0x3E99999Au,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuRightDecorOverlay.Z));
        Assert.NotEqual(0.3f, RetailMainMenuRightDecorOverlay.DestX);
        Assert.NotEqual(0.29f, RetailMainMenuRightDecorOverlay.Z);
        Assert.NotEqual(0.35f, RetailMainMenuRightDecorOverlay.Z);
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuRightDecorOverlay.ZBits);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.ZBits,
            RetailMainMenuRightDecorOverlay.ZBits);
        Assert.Equal(457f, RetailMainMenuRightDecorOverlay.DestX);
        Assert.Equal(355f, RetailMainMenuRightDecorOverlay.DestY);
        Assert.NotEqual(219f, RetailMainMenuRightDecorOverlay.DestX);
        Assert.NotEqual(344f, RetailMainMenuRightDecorOverlay.DestY);
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.DestX,
            RetailMainMenuRightDecorOverlay.DestX);
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.DestY,
            RetailMainMenuRightDecorOverlay.DestY);
        Assert.False(RetailMainMenuRightDecorOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuRightDecorOverlay.InventsSheen);
        Assert.False(RetailMainMenuRightDecorOverlay.UsesTwinFadeGate);
        Assert.False(RetailMainMenuRightDecorOverlay.RedoesDecorShadow);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureBracketTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuRightDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("BracketTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D8A0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightDecorOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightDecorOverlay.ShouldDraw",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("457f, 355f, 1f, 1f", draw, StringComparison.Ordinal);
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
