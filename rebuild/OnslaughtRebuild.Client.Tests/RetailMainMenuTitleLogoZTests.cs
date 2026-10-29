// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D88C title-logo body dest X/Y
/// and leftover Z at <c>0x004642CE</c>, recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuTitleLogoShadow already owns the pack at
/// <c>0x0046424F</c> and says the body pack at 0x004642E3 / 0x004642F0
/// stays TitleLogoTint. Official bytes independently re-read this
/// cycle: a load of the value at 0x0089D88C into eax at <c>0x004642CE</c>, not 0x004642CB (that byte is the previous
/// call's last 00). A push of constant 0x3F7FBE77 at <c>0x004642FE</c>;
/// a push of constant 0x43020000 at <c>0x00464303</c>;
/// a push of constant 0x43A00000 at <c>0x00464308</c>. Load identity is
/// <c>FrontEnd\v3\FE_BEA_Title2.tga</c> at <c>0x0062A3A0</c>;
/// a store of eax to frame local +0x12C at <c>0x00468E60</c> stores the previous
/// load of that string. Identity scale (a push of constant 0x3F800000) at
/// <c>0x004642DD</c> / <c>0x004642EC</c> / <c>0x004642F7</c>. Nearby
/// push of constant 0x3F866666 at <c>0x00464269</c> is already
/// ShadowScaleBoost 1.05. Calls <c>CDXSurf__RenderSurface</c>
/// (<c>0x005563D0</c>) at <c>0x0046430D</c>; a stack cleanup of 0x2C bytes
/// is eleven dwords. The leftover 0.999 is Z, not scale and not a
/// 29% title-logo. Dest Y is 130. Dest X is 320. Mode 4 is the
/// centre-anchor. Body tint siblings 0x004642E4 / 0x004642F1 stay
/// TitleLogoTint. DrawMainMenu keeps TitleLogoTint, scale 1.0,
/// DestX, and DestY. Not a sheen. Not SetLanguage. Not a Process
/// increment. Do not redo selector-bar Z/X, writing Z/X,
/// 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3, 0x00463D1F,
/// 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuTitleLogoZTests
{
    [Fact]
    public void SpecimenSitesAreTheZeroPointNineNineNineZAndThreeTwentyByOneThirtyOnDat0089D88C()
    {
        Assert.Equal(0x004642CEu, RetailMainMenuTitleLogoZ.TextureLoadSite);
        Assert.Equal(0x0089D88Cu, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.Equal(0x0062A3A0u, RetailMainMenuTitleLogoZ.TexturePathSite);
        Assert.Equal(
            @"FrontEnd\v3\FE_BEA_Title2.tga",
            RetailMainMenuTitleLogoZ.TexturePath);
        Assert.Equal(0x00468E60u, RetailMainMenuTitleLogoZ.TextureStoreSite);
        Assert.Equal(0x12C, RetailMainMenuTitleLogoZ.TextureStoreOffset);
        Assert.Equal(0x004642FEu, RetailMainMenuTitleLogoZ.ZPushSite);
        Assert.Equal(0x00464303u, RetailMainMenuTitleLogoZ.YPushSite);
        Assert.Equal(0x00464308u, RetailMainMenuTitleLogoZ.XPushSite);
        Assert.Equal(0x0046430Du, RetailMainMenuTitleLogoZ.CallSite);
        Assert.Equal(0x004642DDu, RetailMainMenuTitleLogoZ.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailMainMenuTitleLogoZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuTitleLogoZ.Scale);
        Assert.Equal(0x3F7FBE77u, RetailMainMenuTitleLogoZ.ZBits);
        Assert.Equal(0.999f, RetailMainMenuTitleLogoZ.Z);
        Assert.Equal(130f, RetailMainMenuTitleLogoZ.DestY);
        Assert.Equal(0x43020000u, RetailMainMenuTitleLogoZ.DestYBits);
        Assert.Equal(320f, RetailMainMenuTitleLogoZ.DestX);
        Assert.Equal(0x43A00000u, RetailMainMenuTitleLogoZ.DestXBits);
        Assert.Equal(4, RetailMainMenuTitleLogoZ.Mode);
        Assert.Equal(0x005563D0u, RetailMainMenuTitleLogoZ.RenderSurface);
        Assert.Equal(0x004642E4u, RetailMainMenuTitleLogoZ.ColorSiblingSite);
        Assert.Equal(0x004642F1u, RetailMainMenuTitleLogoZ.ColorOrSiblingSite);
        Assert.Equal(0x0046424Fu, RetailMainMenuTitleLogoZ.ShadowColorSiblingSite);
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.Site,
            RetailMainMenuTitleLogoZ.ShadowColorSiblingSite);
        Assert.Equal(0x00464269u, RetailMainMenuTitleLogoZ.ShadowScalePushSite);
        Assert.Equal(0x3F866666u, RetailMainMenuTitleLogoZ.ShadowScaleBits);
        Assert.NotEqual(0x0089D7F0u, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D894u, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D89Cu, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A0u, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A4u, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.False(RetailMainMenuTitleLogoZ.IsSetLanguage);
        Assert.False(RetailMainMenuTitleLogoZ.IsButtonPressed);
        Assert.False(RetailMainMenuTitleLogoZ.InventsSheen);
        Assert.False(RetailMainMenuTitleLogoZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuTitleLogoZ.TreatsZAsScale);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesSelectorBarColor);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesWritingZ);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesWritingColor);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesWritingScroll);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesLeftTwinShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesLeftTwinOverlay);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuTitleLogoZ.UsesTwinFadeGate);
        Assert.NotEqual(RetailMainMenuTitleLogoShadow.Site, RetailMainMenuTitleLogoZ.TextureLoadSite);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.ZPushSite, RetailMainMenuTitleLogoZ.ZPushSite);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.DestX, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(RetailMainMenuWritingZ.DestX, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(0x004642CBu, RetailMainMenuTitleLogoZ.TextureLoadSite);
    }

    [Fact]
    public void PushZeroPointNineNineNineIsZNotScaleAndDestIsThreeTwentyByOneThirty()
    {
        Assert.Equal(0x3F7FBE77u, RetailMainMenuTitleLogoZ.ZBits);
        Assert.Equal(
            0x3F7FBE77u,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuTitleLogoZ.Z));
        Assert.Equal(0x3F800000u, RetailMainMenuTitleLogoZ.ScaleBits);
        Assert.Equal(1f, RetailMainMenuTitleLogoZ.Scale);
        Assert.NotEqual(RetailMainMenuTitleLogoZ.Z, RetailMainMenuTitleLogoZ.Scale);
        Assert.NotEqual(0.29f, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(0.33f, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(0.9f, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(1.05f, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(0.29f, RetailMainMenuTitleLogoZ.Scale);
        Assert.Equal(320f, RetailMainMenuTitleLogoZ.DestX);
        Assert.Equal(130f, RetailMainMenuTitleLogoZ.DestY);
        Assert.NotEqual(219f, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(458f, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(457f, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(RetailMainMenuWritingZ.Z, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.Z, RetailMainMenuTitleLogoZ.Z);
        Assert.NotEqual(RetailMainMenuWritingZ.DestX, RetailMainMenuTitleLogoZ.DestX);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.DestX, RetailMainMenuTitleLogoZ.DestX);
        Assert.False(RetailMainMenuTitleLogoZ.TreatsZAsScale);
        Assert.False(RetailMainMenuTitleLogoZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuTitleLogoZ.InventsSheen);
        Assert.False(RetailMainMenuTitleLogoZ.UsesTwinFadeGate);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuTitleLogoZ.RedoesWritingZ);
        Assert.Equal(
            0x3F866666u,
            (uint)BitConverter.SingleToUInt32Bits(1.05f));
        Assert.NotEqual(
            RetailMainMenuTitleLogoZ.ZBits,
            RetailMainMenuTitleLogoZ.ShadowScaleBits);
    }

    [Fact]
    public void DrawMainMenuKeepsTheTintAndDoesNotScaleByZeroPointNineNineNine()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuTitleLogoZ", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("TitleLogoTint", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowScaleBoost", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D88C", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuTitleLogoZ.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("0.999", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuTitleLogoZ", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuTitleLogoZ", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuTitleLogoZ", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuTitleLogoZ", bar, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuSelectorBarZ.DestX", bar, StringComparison.Ordinal);
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
