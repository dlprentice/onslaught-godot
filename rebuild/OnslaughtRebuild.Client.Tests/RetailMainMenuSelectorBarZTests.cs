// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D89C selector-bar texture, dest X
/// and Z leftover at <c>0x00462FED</c>, recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuSelectorBarColor already owns the pack at
/// <c>0x00462FB9</c> and says DrawMainMenuSelectorBar is the consumer.
/// Official bytes independently re-read this cycle:
/// a load of the value at 0x0089D89C into eax at <c>0x00462FED</c>,
/// then a push of eax; a push of constant 0x3EA8F5C3 at <c>0x00462FF3</c>;
/// a push of ecx; a push of constant 0x435B0000 at <c>0x00462FF9</c>.
/// Load identity is <c>FrontEnd\v3\FE_BEA_title_text_box.tga</c> at
/// <c>0x0062A300</c>; a store of eax to frame local +0x13C at <c>0x00468EC3</c>
/// stores the previous load of that string. Identity scale
/// (a push of constant 0x3F800000) at <c>0x00462FC6</c>. Calls
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x00462FFE</c>; a stack cleanup of 0x2C bytes is eleven dwords. The
/// leftover 0.33 is Z, not scale and not a 29% title-logo. Dest X is
/// 219. Mode 4 is the centre-anchor. Colour sibling 0x00462FB9 is
/// already RetailMainMenuSelectorBarColor. DrawMainMenuSelectorBar
/// keeps SubmittedColor, _titleTextBox, and the measured ink+31
/// width. Not a sheen. Not SetLanguage. Not a Process increment.
/// Do not redo writing Z/X, 0x00463873, 0x004638B7, 0x00463A8F,
/// 0x00463AD3, 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuSelectorBarZTests
{
    [Fact]
    public void SpecimenSitesAreTheZeroPointThreeThreeZAndTwoNineteenXOnDat0089D89C()
    {
        Assert.Equal(0x00462FEDu, RetailMainMenuSelectorBarZ.TextureLoadSite);
        Assert.Equal(0x0089D89Cu, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.Equal(0x0062A300u, RetailMainMenuSelectorBarZ.TexturePathSite);
        Assert.Equal(
            @"FrontEnd\v3\FE_BEA_title_text_box.tga",
            RetailMainMenuSelectorBarZ.TexturePath);
        Assert.Equal(0x00468EC3u, RetailMainMenuSelectorBarZ.TextureStoreSite);
        Assert.Equal(0x13C, RetailMainMenuSelectorBarZ.TextureStoreOffset);
        Assert.Equal(0x00462FF3u, RetailMainMenuSelectorBarZ.ZPushSite);
        Assert.Equal(0x00462FF9u, RetailMainMenuSelectorBarZ.XPushSite);
        Assert.Equal(0x00462FFEu, RetailMainMenuSelectorBarZ.CallSite);
        Assert.Equal(0x00462FC6u, RetailMainMenuSelectorBarZ.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuSelectorBarZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuSelectorBarZ.Scale);
        Assert.Equal(0x3EA8F5C3u, RetailMainMenuSelectorBarZ.ZBits);
        Assert.Equal(0.33f, RetailMainMenuSelectorBarZ.Z);
        Assert.Equal(219f, RetailMainMenuSelectorBarZ.DestX);
        Assert.Equal(0x435B0000u, RetailMainMenuSelectorBarZ.DestXBits);
        Assert.Equal(4, RetailMainMenuSelectorBarZ.Mode);
        Assert.Equal(0x005563D0u, RetailMainMenuSelectorBarZ.RenderSurface);
        Assert.Equal(0x00462FB9u, RetailMainMenuSelectorBarZ.ColorSiblingSite);
        Assert.Equal(
            RetailMainMenuSelectorBarColor.Site,
            RetailMainMenuSelectorBarZ.ColorSiblingSite);
        Assert.NotEqual(0x0089D7F0u, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.NotEqual(0x0089D894u, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A0u, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A4u, RetailMainMenuSelectorBarZ.TextureGlobal);
        Assert.False(RetailMainMenuSelectorBarZ.IsSetLanguage);
        Assert.False(RetailMainMenuSelectorBarZ.IsButtonPressed);
        Assert.False(RetailMainMenuSelectorBarZ.InventsSheen);
        Assert.False(RetailMainMenuSelectorBarZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuSelectorBarZ.TreatsZAsScale);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesSelectorBarColor);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesWritingZ);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesWritingColor);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesWritingScroll);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesLeftTwinShadow);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesLeftTwinOverlay);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuSelectorBarZ.UsesTwinFadeGate);
        Assert.NotEqual(RetailMainMenuSelectorBarColor.Site, RetailMainMenuSelectorBarZ.TextureLoadSite);
        Assert.NotEqual(RetailMainMenuWritingZ.Tile0ZPushSite, RetailMainMenuSelectorBarZ.ZPushSite);
        Assert.NotEqual(RetailMainMenuWritingZ.DestX, RetailMainMenuSelectorBarZ.DestX);
    }

    [Fact]
    public void PushZeroPointThreeThreeIsZNotScaleAndDestXIsTwoNineteen()
    {
        Assert.Equal(0x3EA8F5C3u, RetailMainMenuSelectorBarZ.ZBits);
        Assert.Equal(
            0x3EA8F5C3u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuSelectorBarZ.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuSelectorBarZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuSelectorBarZ.Scale);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.Z, RetailMainMenuSelectorBarZ.Scale);
        Assert.NotEqual(0.29f, RetailMainMenuSelectorBarZ.Z);
        Assert.NotEqual(0.3f, RetailMainMenuSelectorBarZ.Z);
        Assert.NotEqual(0.9f, RetailMainMenuSelectorBarZ.Z);
        Assert.NotEqual(0.35f, RetailMainMenuSelectorBarZ.Z);
        Assert.NotEqual(0.29f, RetailMainMenuSelectorBarZ.Scale);
        Assert.Equal(219f, RetailMainMenuSelectorBarZ.DestX);
        Assert.NotEqual(458f, RetailMainMenuSelectorBarZ.DestX);
        Assert.NotEqual(344f, RetailMainMenuSelectorBarZ.DestX);
        Assert.NotEqual(457f, RetailMainMenuSelectorBarZ.DestX);
        Assert.NotEqual(RetailMainMenuWritingZ.Z, RetailMainMenuSelectorBarZ.Z);
        Assert.NotEqual(RetailMainMenuWritingZ.DestX, RetailMainMenuSelectorBarZ.DestX);
        Assert.False(RetailMainMenuSelectorBarZ.TreatsZAsScale);
        Assert.False(RetailMainMenuSelectorBarZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuSelectorBarZ.InventsSheen);
        Assert.False(RetailMainMenuSelectorBarZ.UsesTwinFadeGate);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesSelectorBarColor);
        Assert.False(RetailMainMenuSelectorBarZ.RedoesWritingZ);
    }

    [Fact]
    public void DrawMainMenuSelectorBarKeepsThePackAndDoesNotScaleByZeroPointThreeThree()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenuSelectorBar");

        Assert.Contains("RetailMainMenuSelectorBarZ", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuSelectorBarZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuSelectorBarColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("_titleTextBox", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D89C", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuSelectorBarZ.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("0.33", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuSelectorBarZ", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuSelectorBarZ", choice, StringComparison.Ordinal);
        // The cited FEMessBox chrome replaced the reconstruction-era
        // HighlightTint/_titleTextBox markers when wt/t_7d9a828d merged.
        Assert.Contains("RetailFeMessBox.HighlightColor", choice, StringComparison.Ordinal);
        Assert.Contains("DrawFont22Text", choice, StringComparison.Ordinal);
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
