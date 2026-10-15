// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D894 *63 alpha shadow pack at
/// <c>0x00463873</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x00463873</c> as the
/// *63 pack on 0x0089D894, not D8A0 / D8A4 / D898. Official bytes:
/// <c>mov ecx, esi</c> at <c>0x00463873</c>, then
/// <c>shl 6 / sub esi / shl 16 / and 0xFF000000</c>. Texture load
/// at <c>0x0046385A</c> is <c>mov eax, [0x0089D894]</c>. The leftover
/// 0.35 push at <c>0x00463885</c> is CDXSurf Z <c>0x3EB33333</c>,
/// not scale and not a 29% title-logo. Dest addends are
/// <c>fadd [0x005DB5D0]=349.0</c> then
/// <c>fadd [0x005DB5CC]=224.0</c> before <c>CDXSurf__RenderSurface</c>
/// (<c>0x005563D0</c>). Both dest helpers at <c>0x0046388F</c> and
/// <c>0x004638A3</c> land on <c>0x00468730</c>. Dest is the left-arc
/// pair (219+5, 344+5), not right 462/365. Body sibling 0x004638B7
/// is RetailMainMenuLeftDecorOverlay at (219,344) Z 0.3 — not this
/// type. Left-twin 0x00463A8F / 0x00463AD3 is 0x0089D898 — still
/// leftover. Settled 255 submits <c>0x3E000000</c>, which is capture
/// ShadowTint. DrawMainMenu keeps ShadowTint. Not a sheen (that is
/// already 0x00464343). Not SetLanguage. Not a Process increment.
/// Do not redo 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.
/// Do not redo the RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuLeftDecorShadowTests
{
    [Fact]
    public void SpecimenSitesAreTheSixtyThreeAlphaShadowOnDat0089D894()
    {
        Assert.Equal(0x00463873u, RetailMainMenuLeftDecorShadow.Site);
        Assert.Equal(0x00463875u, RetailMainMenuLeftDecorShadow.ShiftSite);
        Assert.Equal(6, RetailMainMenuLeftDecorShadow.ShiftLeft);
        Assert.Equal(0x00463878u, RetailMainMenuLeftDecorShadow.SubSite);
        Assert.Equal(0x0046387Au, RetailMainMenuLeftDecorShadow.Shift16Site);
        Assert.Equal(0x0046387Du, RetailMainMenuLeftDecorShadow.AndSite);
        Assert.Equal(0xFF000000u, RetailMainMenuLeftDecorShadow.AlphaMask);
        Assert.Equal(0x0046385Au, RetailMainMenuLeftDecorShadow.TextureLoadSite);
        Assert.Equal(0x0089D894u, RetailMainMenuLeftDecorShadow.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.TextureGlobal,
            RetailMainMenuLeftDecorShadow.TextureGlobal);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.TextureGlobal,
            RetailMainMenuLeftDecorShadow.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuLeftDecorShadow.TextureGlobal);
        Assert.Equal(0x00463885u, RetailMainMenuLeftDecorShadow.ZPushSite);
        Assert.Equal(0x3EB33333u, RetailMainMenuLeftDecorShadow.ZBits);
        Assert.Equal(0.35f, RetailMainMenuLeftDecorShadow.Z);
        Assert.Equal(0x0046388Au, RetailMainMenuLeftDecorShadow.SurfThisLoadSite);
        Assert.Equal(0x0089D758u, RetailMainMenuLeftDecorShadow.SurfThis);
        Assert.Equal(0x0046388Fu, RetailMainMenuLeftDecorShadow.YHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuLeftDecorShadow.YHelper);
        Assert.Equal(0x00463894u, RetailMainMenuLeftDecorShadow.DestYAddSite);
        Assert.Equal(0x005DB5D0u, RetailMainMenuLeftDecorShadow.DestYAddGlobal);
        Assert.Equal(349f, RetailMainMenuLeftDecorShadow.DestYAdd);
        Assert.Equal(0x004638A3u, RetailMainMenuLeftDecorShadow.XHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuLeftDecorShadow.XHelper);
        Assert.Equal(0x004638A8u, RetailMainMenuLeftDecorShadow.DestXAddSite);
        Assert.Equal(0x005DB5CCu, RetailMainMenuLeftDecorShadow.DestXAddGlobal);
        Assert.Equal(224f, RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.Equal(4, RetailMainMenuLeftDecorShadow.Mode);
        Assert.Equal(0x004638B2u, RetailMainMenuLeftDecorShadow.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuLeftDecorShadow.RenderSurface);
        Assert.Equal(0x004638B7u, RetailMainMenuLeftDecorShadow.BodySiblingSite);
        Assert.Equal(0x004638E9u, RetailMainMenuLeftDecorShadow.BodyDestYPushSite);
        Assert.Equal(344f, RetailMainMenuLeftDecorShadow.BodyDestY);
        Assert.Equal(0x004638EEu, RetailMainMenuLeftDecorShadow.BodyDestXPushSite);
        Assert.Equal(219f, RetailMainMenuLeftDecorShadow.BodyDestX);
        Assert.Equal(0x3E99999Au, RetailMainMenuLeftDecorShadow.BodyZBits);
        Assert.Equal(0x00463A8Fu, RetailMainMenuLeftDecorShadow.LeftTwinShadowSite);
        Assert.Equal(255, RetailMainMenuLeftDecorShadow.ImageSettledFadeByte);
        Assert.Equal(0x3E000000u, RetailMainMenuLeftDecorShadow.CaptureDiffuse);
        Assert.Equal(0x3E000000u, RetailMainMenuLeftDecorShadow.SettledSubmitted);
        Assert.False(RetailMainMenuLeftDecorShadow.IsSetLanguage);
        Assert.False(RetailMainMenuLeftDecorShadow.IsButtonPressed);
        Assert.False(RetailMainMenuLeftDecorShadow.InventsSheen);
        Assert.False(RetailMainMenuLeftDecorShadow.InventsTitleLogoScale);
        Assert.False(RetailMainMenuLeftDecorShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftDecorShadow.ReplacesShadowTint);
        Assert.False(RetailMainMenuLeftDecorShadow.ReplacesChromeTint);
        Assert.False(RetailMainMenuLeftDecorShadow.ReplacesBracketTint);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesBodyOverlay);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesLeftTwin);
        Assert.False(RetailMainMenuLeftDecorShadow.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.Site,
            RetailMainMenuLeftDecorShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinShadow.Site,
            RetailMainMenuLeftDecorShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.Site,
            RetailMainMenuLeftDecorShadow.Site);
        Assert.NotEqual(
            RetailMainMenuRightDecorOverlay.Site,
            RetailMainMenuLeftDecorShadow.Site);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureShadowTintAndSiblingSixtyThreePacks()
    {
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuLeftDecorShadow.SubmittedColor(
                RetailMainMenuLeftDecorShadow.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuLeftDecorShadow.SubmittedColor(0));
        Assert.Equal(0x3E000000u, RetailMainMenuLeftDecorShadow.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuLeftDecorShadow.SubmittedColor(-1));
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.CaptureDiffuse,
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.SubmittedColor(255),
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuSelectedIconShadow.SubmittedColor(255),
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightTwinShadow.SubmittedColor(255),
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuRightDecorShadow.SubmittedColor(255),
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.NotEqual(0xFE7F7F7Fu, RetailMainMenuLeftDecorShadow.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuLeftDecorShadow.SubmittedColor(255));
    }

    [Fact]
    public void PushZeroPointThreeFiveIsZNotScaleAndDestIsLeftArcNotRight()
    {
        Assert.Equal(0x3EB33333u, RetailMainMenuLeftDecorShadow.ZBits);
        Assert.Equal(
            0x3EB33333u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuLeftDecorShadow.Z));
        Assert.NotEqual(0.35f, RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.NotEqual(0.29f, RetailMainMenuLeftDecorShadow.Z);
        Assert.NotEqual(0.3f, RetailMainMenuLeftDecorShadow.Z);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.ZBits,
            RetailMainMenuLeftDecorShadow.ZBits);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuLeftDecorShadow.ZBits);
        Assert.Equal(224f, RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.Equal(349f, RetailMainMenuLeftDecorShadow.DestYAdd);
        Assert.Equal(219f + 5f, RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.Equal(344f + 5f, RetailMainMenuLeftDecorShadow.DestYAdd);
        Assert.Equal(219f, RetailMainMenuLeftDecorShadow.BodyDestX);
        Assert.Equal(344f, RetailMainMenuLeftDecorShadow.BodyDestY);
        Assert.NotEqual(462f, RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.NotEqual(365f, RetailMainMenuLeftDecorShadow.DestYAdd);
        Assert.NotEqual(457f, RetailMainMenuLeftDecorShadow.BodyDestX);
        Assert.NotEqual(355f, RetailMainMenuLeftDecorShadow.BodyDestY);
        Assert.Equal(
            RetailMainMenuLeftDecorShadow.YHelper,
            RetailMainMenuLeftDecorShadow.XHelper);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.YHelper,
            RetailMainMenuLeftDecorShadow.YHelper);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.XHelper,
            RetailMainMenuLeftDecorShadow.XHelper);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.DestXAdd,
            RetailMainMenuLeftDecorShadow.DestXAdd);
        Assert.NotEqual(
            RetailMainMenuRightDecorShadow.DestYAdd,
            RetailMainMenuLeftDecorShadow.DestYAdd);
        Assert.False(RetailMainMenuLeftDecorShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuLeftDecorShadow.InventsSheen);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesDecorShadow);
        Assert.False(RetailMainMenuLeftDecorShadow.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLeftDecorShadow.RedoesLeftTwin);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureShadowTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLeftDecorShadow", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D894", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftDecorShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLeftDecorShadow.ShouldDraw",
            draw,
            StringComparison.Ordinal);
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
