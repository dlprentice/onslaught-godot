// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render title-logo shadow colour at
/// <c>0x0046424F</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><c>mov ecx, esi; shl 6; sub ecx, esi; shl 16; and 0xFF000000</c>.
/// ESI is the 0x00464240 <c>fistp</c> fade byte, forced to 255 when
/// dest == 0x0c at <c>0x0046423D</c>. Settled 255 submits
/// <c>0x3E000000</c>, which is capture ShadowTint. DrawMainMenu
/// keeps ShadowTint. Not a 29% scale. Not SetLanguage. Not the
/// twin fade. Not a Process increment. The body pack at
/// 0x004642E3 / 0x004642F0 stays TitleLogoTint.</para>
/// </summary>
public sealed class RetailMainMenuTitleLogoShadowTests
{
    [Fact]
    public void SpecimenPackIsFadeTimesSixtyThreeAndAlphaOnly()
    {
        Assert.Equal(0x0046424Fu, RetailMainMenuTitleLogoShadow.Site);
        Assert.Equal(0x00464256u, RetailMainMenuTitleLogoShadow.ShiftSite);
        Assert.Equal(6, RetailMainMenuTitleLogoShadow.ShiftLeft);
        Assert.Equal(0x0046426Eu, RetailMainMenuTitleLogoShadow.AndSite);
        Assert.Equal(0xFF000000u, RetailMainMenuTitleLogoShadow.AlphaMask);
        Assert.Equal(0x0046423Du, RetailMainMenuTitleLogoShadow.DestCompareSite);
        Assert.Equal(0x0C, RetailMainMenuTitleLogoShadow.ClickPageDest);
        Assert.Equal(255, RetailMainMenuTitleLogoShadow.DestForceImmediate);
        Assert.Equal(255, RetailMainMenuTitleLogoShadow.ImageSettledFadeByte);
        Assert.Equal(0x3E000000u, RetailMainMenuTitleLogoShadow.CaptureDiffuse);
        Assert.Equal(0x3E000000u, RetailMainMenuTitleLogoShadow.SettledSubmitted);
        Assert.False(RetailMainMenuTitleLogoShadow.IsSetLanguage);
        Assert.False(RetailMainMenuTitleLogoShadow.IsButtonPressed);
        Assert.False(RetailMainMenuTitleLogoShadow.InventsTitleLogoScale);
        Assert.False(RetailMainMenuTitleLogoShadow.ReplacesShadowTint);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureShadowTint()
    {
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuTitleLogoShadow.SubmittedColor(
                RetailMainMenuTitleLogoShadow.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuTitleLogoShadow.SubmittedColor(0));
        Assert.Equal(0x3E000000u, RetailMainMenuTitleLogoShadow.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuTitleLogoShadow.SubmittedColor(-1));
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.CaptureDiffuse,
            RetailMainMenuTitleLogoShadow.SubmittedColor(255));
    }

    [Fact]
    public void ClickPageDestForcesTheSettledFadeByte()
    {
        Assert.Equal(
            255,
            RetailMainMenuTitleLogoShadow.FadeByte(0, destIsClickPage: true));
        Assert.Equal(
            255,
            RetailMainMenuTitleLogoShadow.FadeByte(40, destIsClickPage: true));
        Assert.Equal(
            40,
            RetailMainMenuTitleLogoShadow.FadeByte(40, destIsClickPage: false));
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuTitleLogoShadow.SubmittedColor(
                RetailMainMenuTitleLogoShadow.FadeByte(0, destIsClickPage: true)));
    }

    [Fact]
    public void DrawMainMenuKeepsShadowTintAndDoesNotInventAScale()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuTitleLogoShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("TitleLogoTint", draw, StringComparison.Ordinal);
        Assert.Contains("1f,", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0x3e000000", flow, StringComparison.OrdinalIgnoreCase);
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
