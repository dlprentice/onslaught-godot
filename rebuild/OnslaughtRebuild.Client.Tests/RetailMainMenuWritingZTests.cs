// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D7F0 writing-chrome Z and dest X
/// leftover at <c>0x00462DFF</c>, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuWritingScroll owns the Y prologue and says it
/// does not own the 458 X or the 0.9 Z. Official bytes independently
/// re-read this cycle: tile 0 <c>push 0x3F666666</c> at
/// <c>0x00462DFF</c> then <c>push 0x43E50000</c> at <c>0x00462E05</c>;
/// tile 1 <c>0x00462E39</c> / <c>0x00462E42</c>; tile 2
/// <c>0x00462E76</c> / <c>0x00462E7F</c>. Texture load at
/// <c>0x00462DC4</c> is <c>mov edx, [0x0089D7F0]</c>. Identity scale
/// <c>push 0x3F800000</c> at <c>0x00462DCC</c>. Calls
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x00462E0A</c> / <c>0x00462E47</c> / <c>0x00462E84</c>. The
/// leftover 0.9 is Z, not scale and not a 29% title-logo. Dest X is
/// 458 on all three tiles. Colour sibling 0x00462DDD is already
/// RetailMainMenuWritingColor. Y sibling 0x00462D46 is already
/// RetailMainMenuWritingScroll. DrawMainMenu keeps ChromeTint and
/// scale 1.0. Not a sheen. Not SetLanguage. Not a Process increment.
/// Do not redo 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuWritingZTests
{
    [Fact]
    public void SpecimenSitesAreTheZeroPointNineZAndFourFiftyEightXOnDat0089D7F0()
    {
        Assert.Equal(0x00462DFFu, RetailMainMenuWritingZ.Tile0ZPushSite);
        Assert.Equal(0x00462E05u, RetailMainMenuWritingZ.Tile0XPushSite);
        Assert.Equal(0x00462E0Au, RetailMainMenuWritingZ.Tile0CallSite);
        Assert.Equal(0x00462E39u, RetailMainMenuWritingZ.Tile1ZPushSite);
        Assert.Equal(0x00462E42u, RetailMainMenuWritingZ.Tile1XPushSite);
        Assert.Equal(0x00462E47u, RetailMainMenuWritingZ.Tile1CallSite);
        Assert.Equal(0x00462E76u, RetailMainMenuWritingZ.Tile2ZPushSite);
        Assert.Equal(0x00462E7Fu, RetailMainMenuWritingZ.Tile2XPushSite);
        Assert.Equal(0x00462E84u, RetailMainMenuWritingZ.Tile2CallSite);
        Assert.Equal(0x00462DC4u, RetailMainMenuWritingZ.TextureLoadSite);
        Assert.Equal(0x0089D7F0u, RetailMainMenuWritingZ.TextureGlobal);
        Assert.Equal(0x00462DCCu, RetailMainMenuWritingZ.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuWritingZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuWritingZ.Scale);
        Assert.Equal(0x3F666666u, RetailMainMenuWritingZ.ZBits);
        Assert.Equal(0.9f, RetailMainMenuWritingZ.Z);
        Assert.Equal(458f, RetailMainMenuWritingZ.DestX);
        Assert.Equal(0x43E50000u, RetailMainMenuWritingZ.DestXBits);
        Assert.Equal(4, RetailMainMenuWritingZ.Mode);
        Assert.Equal(3, RetailMainMenuWritingZ.TileCount);
        Assert.Equal(0x005563D0u, RetailMainMenuWritingZ.RenderSurface);
        Assert.Equal(0x00462DDDu, RetailMainMenuWritingZ.ColorSiblingSite);
        Assert.Equal(0x00462D46u, RetailMainMenuWritingZ.ScrollSiblingSite);
        Assert.Equal(
            RetailMainMenuWritingScroll.TileX,
            RetailMainMenuWritingZ.DestX);
        Assert.Equal(
            RetailMainMenuWritingScroll.TileCount,
            RetailMainMenuWritingZ.TileCount);
        Assert.NotEqual(0x0089D894u, RetailMainMenuWritingZ.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuWritingZ.TextureGlobal);
        Assert.NotEqual(0x0089D89Cu, RetailMainMenuWritingZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A0u, RetailMainMenuWritingZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A4u, RetailMainMenuWritingZ.TextureGlobal);
        Assert.False(RetailMainMenuWritingZ.IsSetLanguage);
        Assert.False(RetailMainMenuWritingZ.IsButtonPressed);
        Assert.False(RetailMainMenuWritingZ.InventsSheen);
        Assert.False(RetailMainMenuWritingZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuWritingZ.TreatsZAsScale);
        Assert.False(RetailMainMenuWritingZ.ReplacesChromeTint);
        Assert.False(RetailMainMenuWritingZ.ReplacesBracketTint);
        Assert.False(RetailMainMenuWritingZ.ReplacesShadowTint);
        Assert.False(RetailMainMenuWritingZ.RedoesWritingColor);
        Assert.False(RetailMainMenuWritingZ.RedoesWritingScroll);
        Assert.False(RetailMainMenuWritingZ.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuWritingZ.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuWritingZ.RedoesLeftTwinShadow);
        Assert.False(RetailMainMenuWritingZ.RedoesLeftTwinOverlay);
        Assert.False(RetailMainMenuWritingZ.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuWritingZ.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuWritingZ.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuWritingZ.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuWritingZ.UsesTwinFadeGate);
        Assert.NotEqual(RetailMainMenuWritingColor.Site, RetailMainMenuWritingZ.Tile0ZPushSite);
        Assert.NotEqual(RetailMainMenuWritingScroll.CounterGlobal, RetailMainMenuWritingZ.Tile0ZPushSite);
    }

    [Fact]
    public void PushZeroPointNineIsZNotScaleAndDestXIsFourFiftyEightOnEveryTile()
    {
        Assert.Equal(0x3F666666u, RetailMainMenuWritingZ.ZBits);
        Assert.Equal(
            0x3F666666u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuWritingZ.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuWritingZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuWritingZ.Scale);
        Assert.NotEqual(RetailMainMenuWritingZ.Z, RetailMainMenuWritingZ.Scale);
        Assert.NotEqual(0.29f, RetailMainMenuWritingZ.Z);
        Assert.NotEqual(0.3f, RetailMainMenuWritingZ.Z);
        Assert.NotEqual(0.33f, RetailMainMenuWritingZ.Z);
        Assert.NotEqual(0.35f, RetailMainMenuWritingZ.Z);
        Assert.NotEqual(0.29f, RetailMainMenuWritingZ.Scale);
        Assert.Equal(458f, RetailMainMenuWritingZ.DestX);
        Assert.NotEqual(219f, RetailMainMenuWritingZ.DestX);
        Assert.NotEqual(344f, RetailMainMenuWritingZ.DestX);
        Assert.NotEqual(457f, RetailMainMenuWritingZ.DestX);
        Assert.Equal(
            RetailMainMenuWritingZ.Tile0ZBits,
            RetailMainMenuWritingZ.Tile1ZBits);
        Assert.Equal(
            RetailMainMenuWritingZ.Tile0ZBits,
            RetailMainMenuWritingZ.Tile2ZBits);
        Assert.Equal(
            RetailMainMenuWritingZ.Tile0DestXBits,
            RetailMainMenuWritingZ.Tile1DestXBits);
        Assert.Equal(
            RetailMainMenuWritingZ.Tile0DestXBits,
            RetailMainMenuWritingZ.Tile2DestXBits);
        Assert.False(RetailMainMenuWritingZ.TreatsZAsScale);
        Assert.False(RetailMainMenuWritingZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuWritingZ.InventsSheen);
        Assert.False(RetailMainMenuWritingZ.UsesTwinFadeGate);
        Assert.False(RetailMainMenuWritingZ.RedoesWritingColor);
        Assert.False(RetailMainMenuWritingZ.RedoesWritingScroll);
    }

    [Fact]
    public void DrawMainMenuKeepsCaptureChromeTintAndDoesNotScaleByZeroPointNine()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuWritingZ", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuWritingScroll.TileX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuWritingScroll.TileY", draw, StringComparison.Ordinal);
        Assert.Contains("ChromeTint", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D7F0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuWritingZ.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuWritingColor.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("0.9f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
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
