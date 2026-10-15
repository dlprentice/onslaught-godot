// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D8A4 *63 alpha shadow pack at
/// <c>0x00463F3F</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>The keep-going leftover labeled <c>0x00463F3F</c> as the
/// *63 pack immediately before the already-shipped 0x00463F83 body
/// overlay. Official bytes: <c>mov ecx, esi</c> at <c>0x00463F3F</c>,
/// then <c>shl 6 / sub esi / shl 16 / and 0xFF000000</c>. Texture is
/// 0x0089D8A4. The leftover 0.35 push at <c>0x00463F51</c> is
/// CDXSurf Z <c>0x3EB33333</c>, not scale and not a 29% title-logo.
/// Dest addends are <c>fadd [0x005DB5C8]=365.0</c> then
/// <c>fadd [0x005DB5C4]=462.0</c> before <c>CDXSurf__RenderSurface</c>
/// (<c>0x005563D0</c>). Gate is <c>0x00463E8D</c>
/// <c>test ah, 1 / je 0x00463FC7</c> — transition &lt; 0.9 only.
/// Settled 255 submits <c>0x3E000000</c>, which is capture
/// ShadowTint. DrawMainMenu keeps ShadowTint. Not a sheen
/// (that is already 0x00464343). Not SetLanguage. Not a Process
/// increment. Do not redo 0x00463F83. Do not redo the
/// RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuRightTwinShadowTests
{
    [Fact]
    public void SpecimenSitesAreTheSixtyThreeAlphaShadowBeforeTheBodyOverlay()
    {
        Assert.Equal(0x00463F3Fu, RetailMainMenuRightTwinShadow.Site);
        Assert.Equal(0x00463F41u, RetailMainMenuRightTwinShadow.ShiftSite);
        Assert.Equal(6, RetailMainMenuRightTwinShadow.ShiftLeft);
        Assert.Equal(0x00463F44u, RetailMainMenuRightTwinShadow.SubSite);
        Assert.Equal(0x00463F46u, RetailMainMenuRightTwinShadow.Shift16Site);
        Assert.Equal(0x00463F49u, RetailMainMenuRightTwinShadow.AndSite);
        Assert.Equal(0xFF000000u, RetailMainMenuRightTwinShadow.AlphaMask);
        Assert.Equal(0x00463F26u, RetailMainMenuRightTwinShadow.TextureLoadSite);
        Assert.Equal(0x0089D8A4u, RetailMainMenuRightTwinShadow.TextureGlobal);
        Assert.Equal(0x00463F51u, RetailMainMenuRightTwinShadow.ZPushSite);
        Assert.Equal(0x3EB33333u, RetailMainMenuRightTwinShadow.ZBits);
        Assert.Equal(0.35f, RetailMainMenuRightTwinShadow.Z);
        Assert.Equal(0x00463F56u, RetailMainMenuRightTwinShadow.SurfThisLoadSite);
        Assert.Equal(0x0089D758u, RetailMainMenuRightTwinShadow.SurfThis);
        Assert.Equal(0x00463F5Bu, RetailMainMenuRightTwinShadow.YHelperSite);
        Assert.Equal(0x00468750u, RetailMainMenuRightTwinShadow.YHelper);
        Assert.Equal(0x00463F60u, RetailMainMenuRightTwinShadow.DestYAddSite);
        Assert.Equal(0x005DB5C8u, RetailMainMenuRightTwinShadow.DestYAddGlobal);
        Assert.Equal(365f, RetailMainMenuRightTwinShadow.DestYAdd);
        Assert.Equal(0x00463F6Fu, RetailMainMenuRightTwinShadow.XHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuRightTwinShadow.XHelper);
        Assert.Equal(0x00463F74u, RetailMainMenuRightTwinShadow.DestXAddSite);
        Assert.Equal(0x005DB5C4u, RetailMainMenuRightTwinShadow.DestXAddGlobal);
        Assert.Equal(462f, RetailMainMenuRightTwinShadow.DestXAdd);
        Assert.Equal(4, RetailMainMenuRightTwinShadow.Mode);
        Assert.Equal(0x00463F7Eu, RetailMainMenuRightTwinShadow.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuRightTwinShadow.RenderSurface);
        Assert.Equal(0x00463E8Du, RetailMainMenuRightTwinShadow.GateSite);
        Assert.Equal(0x00463FC7u, RetailMainMenuRightTwinShadow.SkipSite);
        Assert.Equal(255, RetailMainMenuRightTwinShadow.ImageSettledFadeByte);
        Assert.Equal(0x3E000000u, RetailMainMenuRightTwinShadow.CaptureDiffuse);
        Assert.Equal(0x3E000000u, RetailMainMenuRightTwinShadow.SettledSubmitted);
        Assert.False(RetailMainMenuRightTwinShadow.IsSetLanguage);
        Assert.False(RetailMainMenuRightTwinShadow.IsButtonPressed);
        Assert.False(RetailMainMenuRightTwinShadow.InventsSheen);
        Assert.False(RetailMainMenuRightTwinShadow.InventsTitleLogoScale);
        Assert.False(RetailMainMenuRightTwinShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuRightTwinShadow.ReplacesShadowTint);
        Assert.False(RetailMainMenuRightTwinShadow.ReplacesChromeTint);
        Assert.False(RetailMainMenuRightTwinShadow.ReplacesBracketTint);
        Assert.False(RetailMainMenuRightTwinShadow.RedoesBodyOverlay);
        Assert.False(RetailMainMenuRightTwinShadow.RedoesDecorShadow);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureShadowTintAndSiblingSixtyThreePacks()
    {
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuRightTwinShadow.SubmittedColor(
                RetailMainMenuRightTwinShadow.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuRightTwinShadow.SubmittedColor(0));
        Assert.Equal(0x3E000000u, RetailMainMenuRightTwinShadow.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuRightTwinShadow.SubmittedColor(-1));
        Assert.Equal(
            RetailMainMenuRightTwinShadow.CaptureDiffuse,
            RetailMainMenuRightTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.SubmittedColor(255),
            RetailMainMenuRightTwinShadow.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuSelectedIconShadow.SubmittedColor(255),
            RetailMainMenuRightTwinShadow.SubmittedColor(255));
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.SubmittedColor(255),
            RetailMainMenuRightTwinShadow.SubmittedColor(255));
        Assert.NotEqual(0xFE7F7F7Fu, RetailMainMenuRightTwinShadow.SubmittedColor(255));
        Assert.NotEqual(0x3E7F7F7Fu, RetailMainMenuRightTwinShadow.SubmittedColor(255));
    }

    [Fact]
    public void ShadowIssuesOnlyBelowTheTwinGateAndNotOnSettledFrames()
    {
        Assert.True(RetailMainMenuRightTwinShadow.ShouldDraw(0f));
        Assert.True(RetailMainMenuRightTwinShadow.ShouldDraw(0.8999f));
        Assert.False(RetailMainMenuRightTwinShadow.ShouldDraw(0.9f));
        Assert.False(RetailMainMenuRightTwinShadow.ShouldDraw(1f));
        Assert.Equal(
            RetailMainMenuHitTest.AcceptsTwinFade(0.5f),
            RetailMainMenuRightTwinShadow.ShouldDraw(0.5f));
        Assert.Equal(
            RetailMainMenuRightTwinOverlay.ShouldDraw(0.5f),
            RetailMainMenuRightTwinShadow.ShouldDraw(0.5f));
        Assert.NotEqual(
            RetailMainMenuHitTest.AcceptsHitTest(0.5f),
            RetailMainMenuRightTwinShadow.ShouldDraw(0.5f));
    }

    [Fact]
    public void PushZeroPointThreeFiveIsZNotScaleAndDestAddendsAreFourSixtyTwoThreeSixtyFive()
    {
        Assert.Equal(0x3EB33333u, RetailMainMenuRightTwinShadow.ZBits);
        Assert.Equal(
            0x3EB33333u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuRightTwinShadow.Z));
        Assert.NotEqual(0.35f, RetailMainMenuRightTwinShadow.DestXAdd);
        Assert.NotEqual(0.29f, RetailMainMenuRightTwinShadow.Z);
        Assert.NotEqual(0.3f, RetailMainMenuRightTwinShadow.Z);
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.ZBits,
            RetailMainMenuRightTwinShadow.ZBits);
        Assert.Equal(462f, RetailMainMenuRightTwinShadow.DestXAdd);
        Assert.Equal(365f, RetailMainMenuRightTwinShadow.DestYAdd);
        Assert.Equal(457f + 5f, RetailMainMenuRightTwinShadow.DestXAdd);
        Assert.Equal(355f + 10f, RetailMainMenuRightTwinShadow.DestYAdd);
        Assert.False(RetailMainMenuRightTwinShadow.TreatsZAsScale);
        Assert.False(RetailMainMenuRightTwinShadow.InventsSheen);
        Assert.False(RetailMainMenuRightTwinShadow.RedoesDecorShadow);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureShadowTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuRightTwinShadow", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightTwinShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightTwinShadow.ShouldDraw",
            draw,
            StringComparison.Ordinal);
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
