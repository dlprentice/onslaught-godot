// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render selected-row icon body colour at
/// <c>0x004640DC</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
/// Independently re-read official 74154bfa this cycle (image base 0x400000).
///
/// <para><c>mov edx, esi; shl 8; sub edx, esi; shl 16; or 0x00FFFFFF</c>.
/// ESI is the 0x0046405C <c>fistp</c> icon-fade byte after the
/// 0x00464064 signed 0..255 clamp. Settled 255 submits
/// <c>0xFEFFFFFF</c>, which is not capture BracketTint
/// <c>0xFE7F7F7F</c> (frame 3000 draw 31). DrawMainMenu keeps
/// BracketTint. Not SetLanguage. Not the twin fade. Not a
/// Process increment. Not a 29% scale. ChromeTint and
/// ShadowTint stay put.</para>
/// </summary>
public sealed class RetailMainMenuSelectedIconColorTests
{
    [Fact]
    public void SpecimenPackIsFadeTimesTwoFiftyFiveOrWhiteRgb()
    {
        Assert.Equal(0x004640DCu, RetailMainMenuSelectedIconColor.Site);
        Assert.Equal(0x004640E5u, RetailMainMenuSelectedIconColor.ShiftSite);
        Assert.Equal(8, RetailMainMenuSelectedIconColor.ShiftLeft);
        Assert.Equal(0x004640EAu, RetailMainMenuSelectedIconColor.SubSite);
        Assert.Equal(0x004640F3u, RetailMainMenuSelectedIconColor.Shift16Site);
        Assert.Equal(0x004640FDu, RetailMainMenuSelectedIconColor.OrSite);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuSelectedIconColor.RgbOr);
        Assert.Equal(255, RetailMainMenuSelectedIconColor.ImageSettledFadeByte);
        Assert.Equal(0xFE7F7F7Fu, RetailMainMenuSelectedIconColor.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuSelectedIconColor.SettledSubmitted);
        Assert.False(RetailMainMenuSelectedIconColor.IsSetLanguage);
        Assert.False(RetailMainMenuSelectedIconColor.IsButtonPressed);
        Assert.False(RetailMainMenuSelectedIconColor.ReplacesBracketTint);
        Assert.False(RetailMainMenuSelectedIconColor.InventsTitleLogoScale);
        Assert.False(RetailMainMenuSelectedIconColor.ReplacesChromeTint);
        Assert.False(RetailMainMenuSelectedIconColor.ReplacesShadowTint);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0xFEFFFFFFu,
            RetailMainMenuSelectedIconColor.SubmittedColor(
                RetailMainMenuSelectedIconColor.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuSelectedIconColor.SubmittedColor(0));
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuSelectedIconColor.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuSelectedIconColor.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuSelectedIconColor.CaptureDiffuse,
            RetailMainMenuSelectedIconColor.SubmittedColor(255));
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureBracketTintAndDoesNotWireThePack()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuSelectedIconColor", draw, StringComparison.Ordinal);
        Assert.Contains("BracketTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuSelectedIconColor.SubmittedColor",
            draw,
            StringComparison.Ordinal);
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
