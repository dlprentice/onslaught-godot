// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render Forseti writing-chrome colour at
/// <c>0x00462DE4</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><c>mov ecx, eax; shl 6; sub ecx, eax; shl 16; or 0x00FFFFFF</c>.
/// EAX is the 0x00462DD2 page-fade byte after
/// <c>fistp(clamp((transition-0.75)*4)*255)</c>. Settled 255 submits
/// <c>0x3EFFFFFF</c>, which is not capture ChromeTint
/// <c>0x3E7F7F7F</c>. DrawMainMenu keeps ChromeTint. Not
/// SetLanguage. Not the twin fade. Not a Process increment.</para>
/// </summary>
public sealed class RetailMainMenuWritingColorTests
{
    [Fact]
    public void SpecimenPackIsFadeTimesSixtyThreeOrWhiteRgb()
    {
        Assert.Equal(0x00462DDDu, RetailMainMenuWritingColor.Site);
        Assert.Equal(0x00462DE4u, RetailMainMenuWritingColor.ShiftSite);
        Assert.Equal(6, RetailMainMenuWritingColor.ShiftLeft);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuWritingColor.RgbOr);
        Assert.Equal(255, RetailMainMenuWritingColor.ImageSettledFadeByte);
        Assert.Equal(0x3E7F7F7Fu, RetailMainMenuWritingColor.CaptureDiffuse);
        Assert.Equal(0x3EFFFFFFu, RetailMainMenuWritingColor.SettledSubmitted);
        Assert.False(RetailMainMenuWritingColor.IsSetLanguage);
        Assert.False(RetailMainMenuWritingColor.IsButtonPressed);
        Assert.False(RetailMainMenuWritingColor.ReplacesChromeTint);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0x3EFFFFFFu,
            RetailMainMenuWritingColor.SubmittedColor(
                RetailMainMenuWritingColor.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuWritingColor.SubmittedColor(0));
        Assert.Equal(0x3EFFFFFFu, RetailMainMenuWritingColor.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuWritingColor.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuWritingColor.CaptureDiffuse,
            RetailMainMenuWritingColor.SubmittedColor(255));
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureChromeTintAndDoesNotWireThePack()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("ChromeTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuWritingColor.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuWritingScroll.TileY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0x3e7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
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
