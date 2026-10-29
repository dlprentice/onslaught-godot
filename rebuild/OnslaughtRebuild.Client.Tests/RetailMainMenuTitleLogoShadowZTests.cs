// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render 0x0089D88C title-logo shadow dest
/// leftover and Z at <c>0x00464251</c>, recovered from official
/// 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuTitleLogoShadow already owns the pack at
/// <c>0x0046424F</c> and says <c>0x00464251</c> is the texture
/// load, not the pack. Official bytes independently re-read this
/// cycle: a load of the value at 0x0089D88C into eax at <c>0x00464251</c>. A push of constant 0x3DCCCCCD at
/// <c>0x0046427B</c>. Dest is not an immediate 325/140:
/// a direct call to 0x00468750 at <c>0x00464285</c> then
/// a float add of the value at 0x005D8C20 (value 140.0); a direct call to 0x00468730 at
/// <c>0x00464299</c> then a float add of the value at 0x005DB4A8 (value 325.0). Load
/// identity is already Title2 via ebp+0x12C. Shadow scale
/// (a push of constant 0x3F866666) at <c>0x00464269</c> /
/// <c>0x00464274</c> is already ShadowScaleBoost 1.05. Calls
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) at
/// <c>0x004642A8</c>; a stack cleanup of 0x2C bytes is eleven dwords.
/// The leftover 0.1 is Z, not scale and not a 29% title-logo.
/// Body dest/Z sibling 0x004642CE is already
/// RetailMainMenuTitleLogoZ. DrawMainMenu keeps ShadowTint,
/// ShadowScaleBoost, and body dest + sharedShadow. Not a sheen.
/// Not SetLanguage. Not a Process increment. Do not invent dest
/// immediates. Do not redo body dest/Z, selector-bar Z/X,
/// writing Z/X, 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83. Do not
/// redo the RetailFrontendDecorShadow ellipse.</para>
/// </summary>
public sealed class RetailMainMenuTitleLogoShadowZTests
{
    [Fact]
    public void SpecimenSitesAreTheZeroPointOneZAndHelperPlusAddendDestOnDat0089D88C()
    {
        Assert.Equal(0x00464251u, RetailMainMenuTitleLogoShadowZ.TextureLoadSite);
        Assert.Equal(0x0089D88Cu, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.Equal(0x0062A3A0u, RetailMainMenuTitleLogoShadowZ.TexturePathSite);
        Assert.Equal(
            @"FrontEnd\v3\FE_BEA_Title2.tga",
            RetailMainMenuTitleLogoShadowZ.TexturePath);
        Assert.Equal(0x00468E60u, RetailMainMenuTitleLogoShadowZ.TextureStoreSite);
        Assert.Equal(0x12C, RetailMainMenuTitleLogoShadowZ.TextureStoreOffset);
        Assert.Equal(0x0046427Bu, RetailMainMenuTitleLogoShadowZ.ZPushSite);
        Assert.Equal(0x00464280u, RetailMainMenuTitleLogoShadowZ.SurfThisLoadSite);
        Assert.Equal(0x0089D758u, RetailMainMenuTitleLogoShadowZ.SurfThis);
        Assert.Equal(0x00464285u, RetailMainMenuTitleLogoShadowZ.YHelperSite);
        Assert.Equal(0x00468750u, RetailMainMenuTitleLogoShadowZ.YHelper);
        Assert.Equal(0x0046428Au, RetailMainMenuTitleLogoShadowZ.DestYAddSite);
        Assert.Equal(0x005D8C20u, RetailMainMenuTitleLogoShadowZ.DestYAddGlobal);
        Assert.Equal(140f, RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.Equal(0x430C0000u, RetailMainMenuTitleLogoShadowZ.DestYAddBits);
        Assert.Equal(0x00464299u, RetailMainMenuTitleLogoShadowZ.XHelperSite);
        Assert.Equal(0x00468730u, RetailMainMenuTitleLogoShadowZ.XHelper);
        Assert.Equal(0x0046429Eu, RetailMainMenuTitleLogoShadowZ.DestXAddSite);
        Assert.Equal(0x005DB4A8u, RetailMainMenuTitleLogoShadowZ.DestXAddGlobal);
        Assert.Equal(325f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.Equal(0x43A28000u, RetailMainMenuTitleLogoShadowZ.DestXAddBits);
        Assert.Equal(0x004642A8u, RetailMainMenuTitleLogoShadowZ.CallSite);
        Assert.Equal(0x00464269u, RetailMainMenuTitleLogoShadowZ.ScalePushSite);
        Assert.Equal(0x00464274u, RetailMainMenuTitleLogoShadowZ.ScaleYPushSite);
        Assert.Equal(0x3F866666u, RetailMainMenuTitleLogoShadowZ.ScaleBits);
        Assert.Equal(1.05f, RetailMainMenuTitleLogoShadowZ.Scale);
        Assert.Equal(0x3DCCCCCDu, RetailMainMenuTitleLogoShadowZ.ZBits);
        Assert.Equal(0.1f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.Equal(4, RetailMainMenuTitleLogoShadowZ.Mode);
        Assert.Equal(0x005563D0u, RetailMainMenuTitleLogoShadowZ.RenderSurface);
        Assert.Equal(0x0046424Fu, RetailMainMenuTitleLogoShadowZ.ColorSiblingSite);
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.Site,
            RetailMainMenuTitleLogoShadowZ.ColorSiblingSite);
        Assert.Equal(0x004642CEu, RetailMainMenuTitleLogoShadowZ.BodySiblingSite);
        Assert.Equal(
            RetailMainMenuTitleLogoZ.TextureLoadSite,
            RetailMainMenuTitleLogoShadowZ.BodySiblingSite);
        Assert.Equal(0x0089D88Cu, RetailMainMenuTitleLogoZ.TextureGlobal);
        Assert.NotEqual(0x0089D7F0u, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.NotEqual(0x0089D894u, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.NotEqual(0x0089D898u, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.NotEqual(0x0089D89Cu, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A0u, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.NotEqual(0x0089D8A4u, RetailMainMenuTitleLogoShadowZ.TextureGlobal);
        Assert.False(RetailMainMenuTitleLogoShadowZ.IsSetLanguage);
        Assert.False(RetailMainMenuTitleLogoShadowZ.IsButtonPressed);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsSheen);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuTitleLogoShadowZ.TreatsZAsScale);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsDestImmediates);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesSelectorBarColor);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesWritingZ);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesWritingColor);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesWritingScroll);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesDecorShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesLeftDecorShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesLeftDecorOverlay);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesLeftTwinShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesLeftTwinOverlay);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesRightDecorShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesRightDecorOverlay);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesRightTwinShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesRightTwinOverlay);
        Assert.False(RetailMainMenuTitleLogoShadowZ.UsesTwinFadeGate);
        Assert.NotEqual(
            RetailMainMenuTitleLogoShadow.Site,
            RetailMainMenuTitleLogoShadowZ.TextureLoadSite);
        Assert.NotEqual(
            RetailMainMenuTitleLogoZ.TextureLoadSite,
            RetailMainMenuTitleLogoShadowZ.TextureLoadSite);
        Assert.NotEqual(
            RetailMainMenuTitleLogoZ.ZPushSite,
            RetailMainMenuTitleLogoShadowZ.ZPushSite);
        Assert.NotEqual(0x004642CBu, RetailMainMenuTitleLogoShadowZ.TextureLoadSite);
    }

    [Fact]
    public void PushZeroPointOneIsZNotScaleAndDestIsHelperPlusAddendNotImmediate()
    {
        Assert.Equal(0x3DCCCCCDu, RetailMainMenuTitleLogoShadowZ.ZBits);
        Assert.Equal(
            0x3DCCCCCDu,
            (uint)BitConverter.SingleToUInt32Bits(RetailMainMenuTitleLogoShadowZ.Z));
        Assert.Equal(0x3F866666u, RetailMainMenuTitleLogoShadowZ.ScaleBits);
        Assert.Equal(1.05f, RetailMainMenuTitleLogoShadowZ.Scale);
        Assert.NotEqual(RetailMainMenuTitleLogoShadowZ.Z, RetailMainMenuTitleLogoShadowZ.Scale);
        Assert.NotEqual(0.29f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(0.33f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(0.9f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(0.999f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(1.05f, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(0.1f, RetailMainMenuTitleLogoShadowZ.Scale);
        Assert.Equal(325f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.Equal(140f, RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.Equal(
            RetailMainMenuTitleLogoZ.DestX + (float)RetailFrontendDecorShadow.OffsetCenterX,
            RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.Equal(
            RetailMainMenuTitleLogoZ.DestY + (float)RetailFrontendDecorShadow.OffsetCenterY,
            RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.NotEqual(219f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.NotEqual(224f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.NotEqual(458f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.NotEqual(462f, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.NotEqual(349f, RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.NotEqual(365f, RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.NotEqual(RetailMainMenuTitleLogoZ.DestX, RetailMainMenuTitleLogoShadowZ.DestXAdd);
        Assert.NotEqual(RetailMainMenuTitleLogoZ.DestY, RetailMainMenuTitleLogoShadowZ.DestYAdd);
        Assert.NotEqual(RetailMainMenuTitleLogoZ.Z, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(RetailMainMenuSelectorBarZ.Z, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.NotEqual(RetailMainMenuWritingZ.Z, RetailMainMenuTitleLogoShadowZ.Z);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.YHelper,
            RetailMainMenuTitleLogoShadowZ.YHelper);
        Assert.Equal(
            RetailMainMenuRightDecorShadow.XHelper,
            RetailMainMenuTitleLogoShadowZ.XHelper);
        Assert.NotEqual(
            RetailMainMenuLeftDecorShadow.YHelper,
            RetailMainMenuTitleLogoShadowZ.YHelper);
        Assert.False(RetailMainMenuTitleLogoShadowZ.TreatsZAsScale);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsTitleLogoScale);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsDestImmediates);
        Assert.False(RetailMainMenuTitleLogoShadowZ.InventsSheen);
        Assert.False(RetailMainMenuTitleLogoShadowZ.UsesTwinFadeGate);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuTitleLogoShadowZ.RedoesDecorShadow);
        Assert.Equal(
            0x3F866666u,
            (uint)BitConverter.SingleToUInt32Bits(1.05f));
        Assert.NotEqual(
            RetailMainMenuTitleLogoShadowZ.ZBits,
            RetailMainMenuTitleLogoShadowZ.ScaleBits);
    }

    [Fact]
    public void DrawMainMenuKeepsShadowTintAndDoesNotTreatZeroPointOneAsScaleOrDestImmediate()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuTitleLogoShadowZ", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuTitleLogoZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("sharedShadow", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowTint", draw, StringComparison.Ordinal);
        Assert.Contains("ShadowScaleBoost", draw, StringComparison.Ordinal);
        Assert.Contains("0x0089D88C", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuTitleLogoShadowZ.DestXAdd",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuTitleLogoShadowZ.DestYAdd",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuTitleLogoShadow.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("0.1", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuTitleLogoShadowZ", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuTitleLogoShadowZ", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuTitleLogoShadowZ", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuTitleLogoShadowZ", bar, StringComparison.Ordinal);
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
