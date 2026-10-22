// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D894 not/and/xor overlay at
/// <c>0x004638B7</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x004638B7</c> as the
/// not/and/xor pack on 0x0089D894, not D8A0 / D8A4 / D898. Official
/// bytes independently re-read this cycle:
/// <c>mov eax, esi</c> at <c>0x004638B7</c> (<c>8b c6</c>),
/// <c>mov ecx, [esp+0x40]</c> at <c>0x004638B9</c>, then
/// <c>shl 8 / sub esi / push ebp / shl 16 / mov edx, eax / push
/// 0x3F800000 / not edx / and 0x00FFFFFF / xor eax</c>. Texture
/// load at <c>0x004638DC</c> is <c>mov eax, [0x0089D894]</c>. Dest
/// immediates <c>0x004638E9</c>=344.0 / <c>0x004638EE</c>=219.0.
/// The leftover 0.3 push at <c>0x004638E4</c> is CDXSurf Z
/// <c>0x3E99999A</c>, not scale and not a 29% title-logo. Then
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x004638F3</c>. Dest is the left-arc body, not right.
/// This is not the 0x00463E8D twin-fade gate (that is D8A4).
/// Settled 255 submits <c>0xFEFFFFFF</c>, which is not capture
/// BracketTint. DrawMainMenu keeps BracketTint. Not a sheen
/// (that is already 0x00464343). Not SetLanguage. Not a Process
/// increment. Do not redo 0x00463873, 0x00463D1F, 0x00463D63,
/// 0x00463F3F, or 0x00463F83. Do not redo the
/// RetailFrontendDecorShadow ellipse. Left-twin 0x00463A8F /
/// 0x00463AD3 is 0x0089D898 leftover — not this type.</para>
/// </summary>
public sealed class RetailMainMenuLeftDecorOverlayTests
{
    [Fact]
    public void SpecimenSitesAreTheNotAndXorOverlayOnDat0089D894()
    {
        Assert.Equal(0x004638B7u, RetailMainMenuLeftDecorOverlay.Site);
        Assert.Equal(0x004638B9u, RetailMainMenuLeftDecorOverlay.Esp40Site);
        Assert.Equal(0x004638BDu, RetailMainMenuLeftDecorOverlay.ShiftSite);
        Assert.Equal(8, RetailMainMenuLeftDecorOverlay.ShiftLeft);
        Assert.Equal(0x004638C0u, RetailMainMenuLeftDecorOverlay.SubSite);
        Assert.Equal(0x004638C2u, RetailMainMenuLeftDecorOverlay.PushEbpSite);
        Assert.Equal(0x004638C3u, RetailMainMenuLeftDecorOverlay.Shift16Site);
        Assert.Equal(0x004638C6u, RetailMainMenuLeftDecorOverlay.CopySite);
        Assert.Equal(0x004638C8u, RetailMainMenuLeftDecorOverlay.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuLeftDecorOverlay.ScaleBits);
        Assert.Equal(1f, RetailMainMenuLeftDecorOverlay.Scale);
        Assert.Equal(0x004638CDu, RetailMainMenuLeftDecorOverlay.NotSite);
        Assert.Equal(0x004638D1u, RetailMainMenuLeftDecorOverlay.AndSite);
        Assert.Equal(0x004638DAu, RetailMainMenuLeftDecorOverlay.XorSite);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftDecorOverlay.RgbMask);
        Assert.Equal(0x004638DCu, RetailMainMenuLeftDecorOverlay.TextureLoadSite);
        Assert.Equal(0x0089D894u, RetailMainMenuLeftDecorOverlay.TextureGlobal);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.TextureGlobal,
            RetailMainMenuLeftDecorOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.TextureGlobal,
            RetailMainMenuLeftDecorOverlay.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.TextureGlobal,
            RetailMainMenuLeftDecorOverlay.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuLeftDecorOverlay.TextureGlobal);
        Assert.Equal(0x004638E4u, RetailMainMenuLeftDecorOverlay.ZPushSite);
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftDecorOverlay.ZBits);
        Assert.Equal(0.3f, RetailMainMenuLeftDecorOverlay.Z);
        Assert.Equal(0x004638E9u, RetailMainMenuLeftDecorOverlay.YPushSite);
        Assert.Equal(344f, RetailMainMenuLeftDecorOverlay.DestY);
        Assert.Equal(0x004638EEu, RetailMainMenuLeftDecorOverlay.XPushSite);
        Assert.Equal(219f, RetailMainMenuLeftDecorOverlay.DestX);
        Assert.Equal(4, RetailMainMenuLeftDecorOverlay.Mode);
        Assert.Equal(0x004638F3u, RetailMainMenuLeftDecorOverlay.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuLeftDecorOverlay.RenderSurface);
        Assert.Equal(0x00463E8Du, RetailMainMenuLeftDecorOverlay.TwinGateSite);
        Assert.Equal(0x00463A8Fu, RetailMainMenuLeftDecorOverlay.LeftTwinShadowSite);
        Assert.Equal(255, RetailMainMenuLeftDecorOverlay.ImageSettledFadeByte);
        Assert.Equal(0xFE7F7F7Fu, RetailMainMenuLeftDecorOverlay.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuLeftDecorOverlay.SettledSubmitted);
        Assert.False(RetailMainMenuLeftDecorOverlay.IsSetLanguage);
        Assert.False(RetailMainMenuLeftDecorOverlay.IsButtonPressed);
        Assert.False(RetailMainMenuLeftDecorOverlay.InventsSheen);
        Assert.False(RetailMainMenuLeftDecorOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftDecorOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftDecorOverlay.ReplacesBracketTint);
        Assert.False(RetailMainMenuLeftDecorOverlay.ReplacesChromeTint);
        Assert.False(RetailMainMenuLeftDecorOverlay.ReplacesShadowTint);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesLeftTwin);
        Assert.False(RetailMainMenuLeftDecorOverlay.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.Site,
            RetailMainMenuLeftDecorOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.Site,
            RetailMainMenuLeftDecorOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.Site,
            RetailMainMenuLeftDecorOverlay.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.Site,
            RetailMainMenuLeftDecorOverlay.Site);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.BodySiblingSite,
            RetailMainMenuLeftDecorOverlay.Site);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0xFEFFFFFFu,
            RetailMainMenuLeftDecorOverlay.SubmittedColor(
                RetailMainMenuLeftDecorOverlay.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftDecorOverlay.SubmittedColor(0));
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuLeftDecorOverlay.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLeftDecorOverlay.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuLeftDecorOverlay.CaptureDiffuse,
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailClickToStartTitle.BodyColor(3.0d),
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLanguageChevronColor.FirstPack(255),
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightDecorOverlay.SubmittedColor(255),
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.SubmittedColor(255),
            RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E000000u, RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuLeftDecorOverlay.SubmittedColor(255));
    }

    [Fact]
    public void PushZeroPointThreeIsZNotScaleAndDestIsLeftArcNotRight()
    {
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftDecorOverlay.ZBits);
        Assert.Equal(
            0x3E99999Au,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuLeftDecorOverlay.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuLeftDecorOverlay.ScaleBits);
        Assert.Equal(1f, RetailMainMenuLeftDecorOverlay.Scale);
        Assert.NotEqual(0.3f, RetailMainMenuLeftDecorOverlay.DestX);
        Assert.NotEqual(0.29f, RetailMainMenuLeftDecorOverlay.Z);
        Assert.NotEqual(0.35f, RetailMainMenuLeftDecorOverlay.Z);
        Assert.NotEqual(0.29f, RetailMainMenuLeftDecorOverlay.Scale);
        Assert.Equal(
            RetailMainMenuRightDecorOverlay.ZBits,
            RetailMainMenuLeftDecorOverlay.ZBits);
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuLeftDecorOverlay.ZBits);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.ZBits,
            RetailMainMenuLeftDecorOverlay.ZBits);
        Assert.Equal(219f, RetailMainMenuLeftDecorOverlay.DestX);
        Assert.Equal(344f, RetailMainMenuLeftDecorOverlay.DestY);
        Assert.NotEqual(457f, RetailMainMenuLeftDecorOverlay.DestX);
        Assert.NotEqual(355f, RetailMainMenuLeftDecorOverlay.DestY);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.BodyDestX,
            RetailMainMenuLeftDecorOverlay.DestX);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.BodyDestY,
            RetailMainMenuLeftDecorOverlay.DestY);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.DestX,
            RetailMainMenuLeftDecorOverlay.DestX);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.DestY,
            RetailMainMenuLeftDecorOverlay.DestY);
        Assert.False(RetailMainMenuLeftDecorOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftDecorOverlay.InventsSheen);
        Assert.False(RetailMainMenuLeftDecorOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftDecorOverlay.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftDecorOverlay.RedoesLeftTwin);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureBracketTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLeftDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("BracketTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D894", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftDecorOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftDecorOverlay.ShouldDraw",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftDecorShadow", draw, StringComparison.Ordinal);
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
