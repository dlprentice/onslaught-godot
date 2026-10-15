// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render selected-row icon shadow colour at
/// <c>0x0046407C</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
/// Independently re-read official 74154bfa this cycle (image base 0x400000).
///
/// <para>The keep-going leftover labeled <c>0x00464075</c> as
/// <c>mov ecx, esi</c>. That byte is <c>7E 05</c> — the signed-clamp
/// <c>jle</c> after <c>cmp esi, 255</c>. The pack is
/// <c>mov ecx, esi</c> at <c>0x0046407C</c>, then
/// <c>shl 6 / sub esi / shl 16 / and 0xFF000000</c>. ESI is the
/// 0x0046405F <c>fistp</c> icon-fade byte after the 0x00464067
/// signed 0..255 clamp. Settled 255 submits <c>0x3E000000</c>,
/// which is capture ShadowTint. DrawMainMenu keeps ShadowTint.
/// Not SetLanguage. Not the twin fade. Not a Process increment.
/// Not a 29% scale. ChromeTint and BracketTint stay put.</para>
/// </summary>
public sealed class RetailMainMenuSelectedIconShadowTests
{
    [Fact]
    public void SpecimenPackIsFadeTimesSixtyThreeAndAlphaOnly()
    {
        Assert.Equal(0x0046407Cu, RetailMainMenuSelectedIconShadow.Site);
        Assert.Equal(0x00464085u, RetailMainMenuSelectedIconShadow.ShiftSite);
        Assert.Equal(6, RetailMainMenuSelectedIconShadow.ShiftLeft);
        Assert.Equal(0x0046408Au, RetailMainMenuSelectedIconShadow.SubSite);
        Assert.Equal(0x00464093u, RetailMainMenuSelectedIconShadow.Shift16Site);
        Assert.Equal(0x0046409Du, RetailMainMenuSelectedIconShadow.AndSite);
        Assert.Equal(0xFF000000u, RetailMainMenuSelectedIconShadow.AlphaMask);
        Assert.Equal(0x0046405Fu, RetailMainMenuSelectedIconShadow.FistpSite);
        Assert.Equal(0x00464075u, RetailMainMenuSelectedIconShadow.ClampJleSite);
        Assert.Equal(255, RetailMainMenuSelectedIconShadow.ImageSettledFadeByte);
        Assert.Equal(0x3E000000u, RetailMainMenuSelectedIconShadow.CaptureDiffuse);
        Assert.Equal(0x3E000000u, RetailMainMenuSelectedIconShadow.SettledSubmitted);
        Assert.False(RetailMainMenuSelectedIconShadow.IsSetLanguage);
        Assert.False(RetailMainMenuSelectedIconShadow.IsButtonPressed);
        Assert.False(RetailMainMenuSelectedIconShadow.InventsTitleLogoScale);
        Assert.False(RetailMainMenuSelectedIconShadow.ReplacesShadowTint);
        Assert.False(RetailMainMenuSelectedIconShadow.ReplacesChromeTint);
        Assert.False(RetailMainMenuSelectedIconShadow.ReplacesBracketTint);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureShadowTint()
    {
        Assert.Equal(
            0x3E000000u,
            RetailMainMenuSelectedIconShadow.SubmittedColor(
                RetailMainMenuSelectedIconShadow.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuSelectedIconShadow.SubmittedColor(0));
        Assert.Equal(0x3E000000u, RetailMainMenuSelectedIconShadow.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuSelectedIconShadow.SubmittedColor(-1));
        Assert.Equal(
            RetailMainMenuSelectedIconShadow.CaptureDiffuse,
            RetailMainMenuSelectedIconShadow.SubmittedColor(255));
    }

    [Fact]
    public void SuggestedLeftoverVaIsTheClampJleNotThePack()
    {
        Assert.NotEqual(0x00464075u, RetailMainMenuSelectedIconShadow.Site);
        Assert.Equal(0x00464075u, RetailMainMenuSelectedIconShadow.ClampJleSite);
        Assert.Equal(0x0046407Cu, RetailMainMenuSelectedIconShadow.Site);
    }

    [Fact]
    public void DrawMainMenuKeepsShadowTintAndDoesNotWireThePack()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuSelectedIconShadow", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuSelectedIconShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.Contains("ShadowScaleBoost", draw, StringComparison.Ordinal);
        Assert.Contains("457f, 355f, 1f, 1f", draw, StringComparison.Ordinal);
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
