// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D8A4 not/and/xor overlay at
/// <c>0x00463F83</c>, recovered from the pristine specimen
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
/// Independently re-read official 74154bfa this cycle (image base 0x400000).
///
/// <para>The keep-going leftover labeled <c>0x00463F83</c> as a
/// 0.3-scale sheen. Official bytes: a copy of esi into eax at
/// <c>0x00463F83</c>, a left shift by 8, then subtraction of esi, then push of ebp, then left shift by 16,
/// then copy of eax into edx, then bitwise not of edx, then mask with 0x00FFFFFF, then xor with eax, then
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) as
/// <c>(457, 355, z bits 0x3E99999A, 0x0089D8A4, colour, sx=sy
/// from [esp+0x40], mode 4, 0, 1.0, ebp)</c>. The 0.3 push is Z,
/// not scale and not a 29% title-logo. Gate is <c>0x00463E8D</c>: a bit test of
/// ah against 1, branching to 0x00463FC7 when equal — transition &lt; 0.9 only.
/// Settled 255 submits <c>0xFEFFFFFF</c>, which is not capture
/// BracketTint. DrawMainMenu keeps BracketTint. Not a sheen
/// (that is already 0x00464343). Not SetLanguage. Not a Process
/// increment.</para>
/// </summary>
public sealed class RetailMainMenuRightTwinOverlayTests
{
    [Fact]
    public void SpecimenSitesAreTheNotAndXorOverlayAtFourFiftySevenThreeFiftyFive()
    {
        Assert.Equal(0x00463F83u, RetailMainMenuRightTwinOverlay.Site);
        Assert.Equal(0x00463F89u, RetailMainMenuRightTwinOverlay.ShiftSite);
        Assert.Equal(8, RetailMainMenuRightTwinOverlay.ShiftLeft);
        Assert.Equal(0x00463F8Cu, RetailMainMenuRightTwinOverlay.SubSite);
        Assert.Equal(0x00463F8Eu, RetailMainMenuRightTwinOverlay.PushEbpSite);
        Assert.Equal(0x00463F8Fu, RetailMainMenuRightTwinOverlay.Shift16Site);
        Assert.Equal(0x00463F92u, RetailMainMenuRightTwinOverlay.CopySite);
        Assert.Equal(0x00463F99u, RetailMainMenuRightTwinOverlay.NotSite);
        Assert.Equal(0x00463F9Du, RetailMainMenuRightTwinOverlay.AndSite);
        Assert.Equal(0x00463FA6u, RetailMainMenuRightTwinOverlay.XorSite);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightTwinOverlay.RgbMask);
        Assert.Equal(0x00463FA8u, RetailMainMenuRightTwinOverlay.TextureLoadSite);
        Assert.Equal(0x0089D8A4u, RetailMainMenuRightTwinOverlay.TextureGlobal);
        Assert.Equal(0x00463FB0u, RetailMainMenuRightTwinOverlay.ZPushSite);
        Assert.Equal(0x3E99999Au, RetailMainMenuRightTwinOverlay.ZBits);
        Assert.Equal(0.3f, RetailMainMenuRightTwinOverlay.Z);
        Assert.Equal(0x00463FB5u, RetailMainMenuRightTwinOverlay.YPushSite);
        Assert.Equal(355f, RetailMainMenuRightTwinOverlay.DestY);
        Assert.Equal(0x00463FBAu, RetailMainMenuRightTwinOverlay.XPushSite);
        Assert.Equal(457f, RetailMainMenuRightTwinOverlay.DestX);
        Assert.Equal(4, RetailMainMenuRightTwinOverlay.Mode);
        Assert.Equal(0x00463FBFu, RetailMainMenuRightTwinOverlay.CallSite);
        Assert.Equal(0x005563D0u, RetailMainMenuRightTwinOverlay.RenderSurface);
        Assert.Equal(0x00463E8Du, RetailMainMenuRightTwinOverlay.GateSite);
        Assert.Equal(0x00463FC7u, RetailMainMenuRightTwinOverlay.SkipSite);
        Assert.Equal(255, RetailMainMenuRightTwinOverlay.ImageSettledFadeByte);
        Assert.Equal(0xFE7F7F7Fu, RetailMainMenuRightTwinOverlay.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuRightTwinOverlay.SettledSubmitted);
        Assert.False(RetailMainMenuRightTwinOverlay.IsSetLanguage);
        Assert.False(RetailMainMenuRightTwinOverlay.IsButtonPressed);
        Assert.False(RetailMainMenuRightTwinOverlay.InventsSheen);
        Assert.False(RetailMainMenuRightTwinOverlay.InventsTitleLogoScale);
        Assert.False(RetailMainMenuRightTwinOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuRightTwinOverlay.ReplacesBracketTint);
        Assert.False(RetailMainMenuRightTwinOverlay.ReplacesChromeTint);
        Assert.False(RetailMainMenuRightTwinOverlay.ReplacesShadowTint);
    }

    [Fact]
    public void SettledSubmitIsWhiteAlphaNotCaptureGrey()
    {
        Assert.Equal(
            0xFEFFFFFFu,
            RetailMainMenuRightTwinOverlay.SubmittedColor(
                RetailMainMenuRightTwinOverlay.ImageSettledFadeByte));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightTwinOverlay.SubmittedColor(0));
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuRightTwinOverlay.SubmittedColor(256));
        Assert.Equal(0x00FFFFFFu, RetailMainMenuRightTwinOverlay.SubmittedColor(-1));
        Assert.NotEqual(
            RetailMainMenuRightTwinOverlay.CaptureDiffuse,
            RetailMainMenuRightTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailClickToStartTitle.BodyColor(3.0d),
            RetailMainMenuRightTwinOverlay.SubmittedColor(255));
        Assert.Equal(
            RetailMainMenuLanguageChevronColor.FirstPack(255),
            RetailMainMenuRightTwinOverlay.SubmittedColor(255));
    }

    [Fact]
    public void OverlayIssuesOnlyBelowTheTwinGateAndNotOnSettledFrames()
    {
        Assert.True(RetailMainMenuRightTwinOverlay.ShouldDraw(0f));
        Assert.True(RetailMainMenuRightTwinOverlay.ShouldDraw(0.8999f));
        Assert.False(RetailMainMenuRightTwinOverlay.ShouldDraw(0.9f));
        Assert.False(RetailMainMenuRightTwinOverlay.ShouldDraw(1f));
        Assert.Equal(
            RetailMainMenuHitTest.AcceptsTwinFade(0.5f),
            RetailMainMenuRightTwinOverlay.ShouldDraw(0.5f));
        Assert.NotEqual(
            RetailMainMenuHitTest.AcceptsHitTest(0.5f),
            RetailMainMenuRightTwinOverlay.ShouldDraw(0.5f));
    }

    [Fact]
    public void PushZeroPointThreeIsZNotScale()
    {
        Assert.Equal(0x3E99999Au, RetailMainMenuRightTwinOverlay.ZBits);
        Assert.Equal(
            0x3E99999Au,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuRightTwinOverlay.Z));
        Assert.NotEqual(0.3f, RetailMainMenuRightTwinOverlay.DestX);
        Assert.NotEqual(0.29f, RetailMainMenuRightTwinOverlay.Z);
        Assert.False(RetailMainMenuRightTwinOverlay.TreatsZAsScale);
        Assert.False(RetailMainMenuRightTwinOverlay.InventsSheen);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureBracketTintAndDoesNotInventASheen()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuRightTwinOverlay", draw, StringComparison.Ordinal);
        Assert.Contains("BracketTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightTwinOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuRightTwinOverlay.ShouldDraw",
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
