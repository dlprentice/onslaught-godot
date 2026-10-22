// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D898 *63 alpha shadow pack at
/// <c>0x00463A8F</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x00463A8F</c> as the
/// *63 pack on leftover left-twin 0x0089D898, not D894 / D8A0 /
/// D8A4. Official bytes independently re-read this cycle:
/// <c>mov eax, [0x0089D898]</c> at <c>0x00463A76</c>,
/// <c>mov ecx, esi</c> at <c>0x00463A8F</c>, then
/// <c>shl 6 / sub esi / shl 16 / and 0xFF000000</c>. The leftover
/// 0.35 push at <c>0x00463AA1</c> is CDXSurf Z <c>0x3EB33333</c>,
/// not scale and not a 29% title-logo. Dest addends are
/// <c>fadd [0x005DB5D0]=349.0</c> then
/// <c>fadd [0x005DB5CC]=224.0</c> before <c>CDXSurf__RenderSurface</c>
/// (<c>0x005563D0</c>). Both dest helpers at <c>0x00463AAB</c> and
/// <c>0x00463ABF</c> land on <c>0x00468730</c>. Dest is the leftover
/// left-twin pair (219+5, 344+5), not 0x0089D894 primary and not
/// right 462/365. Body leftover 0x00463AD3 is the not/and/xor pack
/// on the same 0x0089D898 at (219,344) Z 0.3 — not this type.
/// Settled 255 submits <c>0x3E000000</c>, which is capture
/// ShadowTint. DrawMainMenu keeps ShadowTint. Not a sheen (that is
/// already 0x00464343). Not SetLanguage. Not a Process increment.
/// Do not redo 0x00463873, 0x004638B7, 0x00463D1F, 0x00463D63,
/// 0x00463F3F, or 0x00463F83. Do not redo the
/// RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuLeftTwinShadowTests
{
    [Fact]
    public void SpecimenSitesAreTheSixtyThreeAlphaShadowOnDat0089D898()
    {
        Assert.Equal(0x00463A8Fu, RetailMainMenuLeftTwinShadow.Site);
        Assert.Equal(0x00463A91u, RetailMainMenuLeftTwinShadow.ShiftSite);
        Assert.Equal(6, RetailMainMenuLeftTwinShadow.ShiftLeft);
        Assert.Equal(0x00463A94u, RetailMainMenuLeftTwinShadow.SubSite);
        Assert.Equal(0x00463A96u, RetailMainMenuLeftTwinShadow.Shift16Site);
        Assert.Equal(0x00463A99u, RetailMainMenuLeftTwinShadow.AndSite);
        Assert.Equal(0xFF000000u, RetailMainMenuLeftTwinShadow.AlphaMask);
        Assert.Equal(0x00463A76u, RetailMainMenuLeftTwinShadow.TextureLoadSite);
        Assert.Equal(0x0089D898u, RetailMainMenuLeftTwinShadow.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.TextureGlobal,
            RetailMainMenuLeftTwinShadow.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.TextureGlobal,
            RetailMainMenuLeftTwinShadow.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.TextureGlobal,
            RetailMainMenuLeftTwinShadow.TextureGlobal);
        Assert.NotEqual(0x0089D894u, RetailMainMenuLeftTwinShadow.TextureGlobal);
        Assert.Equal(0x00463AA1u, RetailMainMenuLeftTwinShadow.ZPushSite);
        Assert.Equal(0x3EB33333u, RetailMainMenuLeftTwinShadow.ZBits);
        Assert.Equal(0.35f, RetailMainMenuLeftTwinShadow.Z);
        Assert.Equal(0x00463AA6u, RetailMainMenuLeftTwinShadow.SurfThisLoadSite);
        Assert.Equal(0x0089D758u, RetailMainMenuLeftTwinShadow.SurfThis);
        Assert.Equal(0x00463AABu, RetailMainMenuLeftTwinShadow.YHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuLeftTwinShadow.YHelper);
        Assert.Equal(0x00463AB0u, RetailMainMenuLeftTwinShadow.DestYAddSite);
        Assert.Equal(0x005DB5D0u, RetailMainMenuLeftTwinShadow.DestYAddGlobal);
        Assert.Equal(349f, RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.Equal(0x00463ABFu, RetailMainMenuLeftTwinShadow.XHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuLeftTwinShadow.XHelper);
        Assert.Equal(0x00463AC4u, RetailMainMenuLeftTwinShadow.DestXAddSite);
        Assert.Equal(0x005DB5CCu, RetailMainMenuLeftTwinShadow.DestXAddGlobal);
        Assert.Equal(224f, RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.Equal(4, RetailMainMenuLeftTwinShadow.Mode);
        Assert.Equal(0x00463ACEu, RetailMainMenuLeftTwinShadow.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuLeftTwinShadow.RenderSurface);
        Assert.Equal(0x00463AD3u, RetailMainMenuLeftTwinShadow.BodySiblingSite);
        Assert.Equal(0x00463B05u, RetailMainMenuLeftTwinShadow.BodyDestYPushSite);
        Assert.Equal(344f, RetailMainMenuLeftTwinShadow.BodyDestY);
        Assert.Equal(0x00463B0Au, RetailMainMenuLeftTwinShadow.BodyDestXPushSite);
        Assert.Equal(219f, RetailMainMenuLeftTwinShadow.BodyDestX);
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftTwinShadow.BodyZBits);
        Assert.Equal(0x00463AF8u, RetailMainMenuLeftTwinShadow.BodyTextureLoadSite);
        Assert.Equal(0x0089D898u, RetailMainMenuLeftTwinShadow.BodyTextureGlobal);
        Assert.Equal(255, RetailMainMenuLeftTwinShadow.ImageSettledFadeByte);
        Assert.Equal(0x3E000000u, RetailMainMenuLeftTwinShadow.CaptureDiffuse);
        Assert.Equal(0x3E000000u, RetailMainMenuLeftTwinShadow.SettledSubmitted);
        Assert.False(RetailMainMenuLeftTwinShadow.IsSetLanguage);
        Assert.False(RetailMainMenuLeftTwinShadow.IsButtonPressed);
        Assert.False(RetailMainMenuLeftTwinShadow.InventsSheen);
        Assert.False(RetailMainMenuLeftTwinShadow.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftTwinShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftTwinShadow.ReplacesShadowTint);
        Assert.False(RetailMainMenuLeftTwinShadow.ReplacesChromeTint);
        Assert.False(RetailMainMenuLeftTwinShadow.ReplacesBracketTint);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesBodyOverlay);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuLeftTwinShadow.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.Site,
            RetailMainMenuLeftTwinShadow.Site);
        Assert.NotEqual(
            RetailMainMenuLeftDecorOverlay.Site,
            RetailMainMenuLeftTwinShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.Site,
            RetailMainMenuLeftTwinShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.Site,
            RetailMainMenuLeftTwinShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.Site,
            RetailMainMenuLeftTwinShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.Site,
            RetailMainMenuLeftTwinShadow.Site);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureShadowTintAndSiblingSixtyThreePacks()
    {
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuLeftTwinShadow.SubmittedColor(
                RetailMainMenuLeftTwinShadow.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuLeftTwinShadow.SubmittedColor(0));
        Assert.Equal(0x3E000000u, RetailMainMenuLeftTwinShadow.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuLeftTwinShadow.SubmittedColor(-1));
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.CaptureDiffuse,
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuSelectedIconShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightTwinShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightDecorShadow.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.NotEqual(0xFE7F7F7Fu, RetailMainMenuLeftTwinShadow.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuLeftTwinShadow.SubmittedColor(255));
    }

    [Fact]
    public void PushZeroPointThreeFiveIsZNotScaleAndDestIsLeftTwinNotPrimaryOrRight()
    {
        Assert.Equal(0x3EB33333u, RetailMainMenuLeftTwinShadow.ZBits);
        Assert.Equal(
            0x3EB33333u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuLeftTwinShadow.Z));
        Assert.NotEqual(0.35f, RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.NotEqual(0.29f, RetailMainMenuLeftTwinShadow.Z);
        Assert.NotEqual(0.3f, RetailMainMenuLeftTwinShadow.Z);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.ZBits,
            RetailMainMenuLeftTwinShadow.ZBits);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.ZBits,
            RetailMainMenuLeftTwinShadow.ZBits);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuLeftTwinShadow.ZBits);
        Assert.Equal(224f, RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.Equal(349f, RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.Equal(219f + 5f, RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.Equal(344f + 5f, RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.Equal(219f, RetailMainMenuLeftTwinShadow.BodyDestX);
        Assert.Equal(344f, RetailMainMenuLeftTwinShadow.BodyDestY);
        Assert.NotEqual(462f, RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.NotEqual(365f, RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.NotEqual(457f, RetailMainMenuLeftTwinShadow.BodyDestX);
        Assert.NotEqual(355f, RetailMainMenuLeftTwinShadow.BodyDestY);
        Assert.Equal(
            RetailMainMenuLeftTwinShadow.YHelper,
            RetailMainMenuLeftTwinShadow.XHelper);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.YHelper,
            RetailMainMenuLeftTwinShadow.YHelper);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.YHelper,
            RetailMainMenuLeftTwinShadow.YHelper);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.DestXAdd,
            RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.DestYAdd,
            RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.DestXAdd,
            RetailMainMenuLeftTwinShadow.DestXAdd);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.DestYAdd,
            RetailMainMenuLeftTwinShadow.DestYAdd);
        Assert.False(RetailMainMenuLeftTwinShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftTwinShadow.InventsSheen);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesBodyOverlay);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuLeftTwinShadow.RedoesLeftDecorOverlay);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureShadowTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLeftTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D898", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftTwinShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftTwinShadow.ShouldDraw",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLeftDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightDecorOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuRightTwinOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendDecorShadow", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0x3e000000", flow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0xfe7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x3e7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
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
