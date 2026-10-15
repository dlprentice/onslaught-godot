// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay at <c>0x0046416E</c>,
/// recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><c>push 0x00629454</c> is <c>"V%1d.%02d"</c>. Major dword
/// <c>0x00629410</c> is image-initial 1. Minor <c>0x00679980</c> is
/// BSS past the image, so image-initial 0. Settled colour is
/// <c>fade&lt;&lt;24 | 0x00102025</c> = <c>0xFF102025</c>, which is
/// capture VersionTint. WinMain's VS_FIXEDFILEINFO write is not this
/// helper. Not SetLanguage. Not the twin fade. Not a Process
/// increment. Not a title-logo 29% scale.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayTests
{
    [Fact]
    public void SpecimenFormatAndImageInitialsAreVOneDotZeroZero()
    {
        Assert.Equal(0x0046416Eu, RetailMainMenuVersionOverlay.FormatSite);
        Assert.Equal(0x00629454u, RetailMainMenuVersionOverlay.FormatGlobal);
        Assert.Equal("V%1d.%02d", RetailMainMenuVersionOverlay.FormatString);
        Assert.Equal(0x00629410u, RetailMainMenuVersionOverlay.MajorGlobal);
        Assert.Equal(1, RetailMainMenuVersionOverlay.ImageInitialMajor);
        Assert.Equal(0x00679980u, RetailMainMenuVersionOverlay.MinorGlobal);
        Assert.Equal(0, RetailMainMenuVersionOverlay.ImageInitialMinor);
        Assert.Equal(0x004641B1u, RetailMainMenuVersionOverlay.ShiftSite);
        Assert.Equal(24, RetailMainMenuVersionOverlay.ShiftLeft);
        Assert.Equal(0x004641B4u, RetailMainMenuVersionOverlay.OrSite);
        Assert.Equal(0x00102025u, RetailMainMenuVersionOverlay.RgbOr);
        Assert.Equal(255, RetailMainMenuVersionOverlay.ImageSettledFadeByte);
        Assert.Equal(0xFF102025u, RetailMainMenuVersionOverlay.CaptureDiffuse);
        Assert.Equal(0xFF102025u, RetailMainMenuVersionOverlay.SettledSubmitted);
        Assert.False(RetailMainMenuVersionOverlay.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlay.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlay.IsGetFileVersionInfo);
        Assert.False(RetailMainMenuVersionOverlay.InventsTitleLogoScale);
    }

    [Fact]
    public void ImageInitialFormatIsReleasedOverlay()
    {
        Assert.Equal(
            "V1.00",
            RetailMainMenuVersionOverlay.Format(
                RetailMainMenuVersionOverlay.ImageInitialMajor,
                RetailMainMenuVersionOverlay.ImageInitialMinor));
        Assert.Equal("V1.00", RetailMainMenuVersionOverlay.Format(1, 0));
        Assert.Equal("V2.05", RetailMainMenuVersionOverlay.Format(2, 5));
        Assert.Equal("V10.00", RetailMainMenuVersionOverlay.Format(10, 0));
    }

    [Fact]
    public void SettledSubmitMatchesCaptureVersionTint()
    {
        Assert.Equal(
            0xFF102025u,
            RetailMainMenuVersionOverlay.SubmittedColor(
                RetailMainMenuVersionOverlay.ImageSettledFadeByte));
        Assert.Equal(0x00102025u, RetailMainMenuVersionOverlay.SubmittedColor(0));
        Assert.Equal(0xFF102025u, RetailMainMenuVersionOverlay.SubmittedColor(256));
        Assert.Equal(0x00102025u, RetailMainMenuVersionOverlay.SubmittedColor(-1));
    }

    [Fact]
    public void DrawMainMenuWiresTheFormatAndKeepsVersionTint()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlay.Format", draw, StringComparison.Ordinal);
        Assert.Contains("ImageInitialMajor", draw, StringComparison.Ordinal);
        Assert.Contains("ImageInitialMinor", draw, StringComparison.Ordinal);
        Assert.Contains("VersionTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuVersionOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFileVersionInfo", draw, StringComparison.Ordinal);
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
