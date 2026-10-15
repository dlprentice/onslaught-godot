// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay post-draw flag leftover
/// after dest/Z/font, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuVersionOverlay already owns the sprintf at
/// <c>0x0046416E</c> and the settled pack at <c>0x004641B1</c> /
/// <c>0x004641B4</c>. RetailMainMenuVersionOverlayZ already owns
/// GetWindowHeight-16 dest Y, dest X push 0, and Z 0.01.
/// RetailMainMenuVersionOverlayFont already owns push 1 /
/// FONT_SMALL / Font13PS. Official bytes independently re-read
/// this cycle: after <c>CDXFont__DrawTextDynamic</c> at
/// <c>0x004641ED</c>, <c>fld [esp+0x38]</c> at
/// <c>0x004641F2</c>, <c>fcom [0x005D856C]</c> at
/// <c>0x004641F6</c> (image 0.0f), then
/// <c>mov byte [0x00679B40], 1</c> at <c>0x004641FC</c>,
/// <c>mov byte [0x009C68AC], 0</c> at <c>0x00464203</c>,
/// <c>mov byte [0x009C690D], 1</c> at <c>0x0046420A</c>, then
/// <c>fnstsw ax</c> at <c>0x00464211</c>. The three stores are
/// unconditional. The <c>fcom</c> is consumed by the
/// already-owned title-logo shadow clamp, not a version fade.
/// The 0x00464180 store of 0 to 0x00679B40 is a sibling, not
/// this leftover. DrawMainMenu keeps title-font DrawText.
/// Do not invent a 2px kerning hack. Do not redo version dest/Z,
/// version font slot, title-logo dest/Z, title-logo shadow dest/Z,
/// selector-bar Z/X, writing Z/X, 0x00463873, 0x004638B7,
/// 0x00463A8F, 0x00463AD3, 0x00463D1F, 0x00463D63, 0x00463F3F,
/// or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayFlagsTests
{
    [Fact]
    public void SpecimenSitesAreUnconditionalPostDrawStoresNotAFade()
    {
        Assert.Equal(0x004641EDu, RetailMainMenuVersionOverlayFlags.DrawCallSite);
        Assert.Equal(0x00465710u, RetailMainMenuVersionOverlayFlags.DrawTextDynamic);
        Assert.Equal(0x004641F2u, RetailMainMenuVersionOverlayFlags.FldSite);
        Assert.Equal(0x004641F6u, RetailMainMenuVersionOverlayFlags.FcomSite);
        Assert.Equal(0x005D856Cu, RetailMainMenuVersionOverlayFlags.FcomZeroGlobal);
        Assert.Equal(0x00000000u, RetailMainMenuVersionOverlayFlags.FcomZeroBits);
        Assert.Equal(0f, RetailMainMenuVersionOverlayFlags.FcomZero);
        Assert.Equal(0x00464211u, RetailMainMenuVersionOverlayFlags.FnstswSite);
        Assert.Equal(0x004641FCu, RetailMainMenuVersionOverlayFlags.EnableByteStoreSite);
        Assert.Equal(0x00679B40u, RetailMainMenuVersionOverlayFlags.EnableByteGlobal);
        Assert.Equal(1, RetailMainMenuVersionOverlayFlags.EnableByteAfterDraw);
        Assert.Equal(0x00464180u, RetailMainMenuVersionOverlayFlags.EnableByteBeforeDrawSite);
        Assert.Equal(0, RetailMainMenuVersionOverlayFlags.EnableByteBeforeDraw);
        Assert.Equal(0x00465F00u, RetailMainMenuVersionOverlayFlags.EnableByteReader);
        Assert.Equal(0x00464203u, RetailMainMenuVersionOverlayFlags.StateAStoreSite);
        Assert.Equal(0x009C68ACu, RetailMainMenuVersionOverlayFlags.StateAGlobal);
        Assert.Equal(0, RetailMainMenuVersionOverlayFlags.StateAAfterDraw);
        Assert.Equal(0x00462D5Eu, RetailMainMenuVersionOverlayFlags.StateAPrologueSite);
        Assert.Equal(0x0046420Au, RetailMainMenuVersionOverlayFlags.StateBStoreSite);
        Assert.Equal(0x009C690Du, RetailMainMenuVersionOverlayFlags.StateBGlobal);
        Assert.Equal(1, RetailMainMenuVersionOverlayFlags.StateBAfterDraw);
        Assert.Equal(0x00462D65u, RetailMainMenuVersionOverlayFlags.StateBPrologueSite);
        Assert.Equal(0x0046423Du, RetailMainMenuVersionOverlayFlags.TitleLogoShadowDestCompare);
        Assert.Equal(
            RetailMainMenuVersionOverlayFont.DrawCallSite,
            RetailMainMenuVersionOverlayFlags.DrawCallSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.CallSite,
            RetailMainMenuVersionOverlayFlags.DrawCallSite);
        Assert.Equal(
            RetailMainMenuLanguageBlink.TimerThresholdGlobal,
            RetailMainMenuVersionOverlayFlags.FcomZeroGlobal);
        Assert.Equal(
            RetailMainMenuTitleLogoShadow.DestCompareSite,
            RetailMainMenuVersionOverlayFlags.TitleLogoShadowDestCompare);
        Assert.True(RetailMainMenuVersionOverlayFlags.StoresAreUnconditional);
        Assert.False(RetailMainMenuVersionOverlayFlags.OwnsBeforeDrawStore);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayFlags.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayFlags.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsTitleLogoScale);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesVersionOverlayZ);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesVersionOverlayFont);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayFlags.UsesTwinFadeGate);
    }

    [Fact]
    public void AfterDrawStoresAreOneZeroOneAndDoNotOwnTheBeforeDrawZero()
    {
        Assert.Equal((1, 0, 1), RetailMainMenuVersionOverlayFlags.AfterDrawStores());
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFlags.EnableByteAfterDraw,
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDraw);
        Assert.True(
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDrawSite <
            RetailMainMenuVersionOverlayFlags.DrawCallSite);
        Assert.True(
            RetailMainMenuVersionOverlayFlags.DrawCallSite <
            RetailMainMenuVersionOverlayFlags.FldSite);
        Assert.True(
            RetailMainMenuVersionOverlayFlags.FcomSite <
            RetailMainMenuVersionOverlayFlags.EnableByteStoreSite);
        Assert.True(
            RetailMainMenuVersionOverlayFlags.StateBStoreSite <
            RetailMainMenuVersionOverlayFlags.FnstswSite);
        Assert.True(
            RetailMainMenuVersionOverlayFlags.FnstswSite <
            RetailMainMenuVersionOverlayFlags.TitleLogoShadowDestCompare);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFlags.EnableByteStoreSite,
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDrawSite);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFlags.EnableByteReader,
            RetailMainMenuVersionOverlayFlags.DrawTextDynamic);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.StateAAfterDraw,
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDraw);
        Assert.False(RetailMainMenuVersionOverlayFlags.OwnsBeforeDrawStore);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayFlags.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesVersionOverlayFont);
        Assert.False(RetailMainMenuVersionOverlayFlags.RedoesTitleLogoShadow);
    }

    [Fact]
    public void DrawMainMenuKeepsTitleFontDrawTextAndDoesNotInventFade()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlayFlags", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayFont", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlay.Format", draw, StringComparison.Ordinal);
        Assert.Contains("VersionTint", draw, StringComparison.Ordinal);
        Assert.Contains("DrawText(", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuVersionOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("42f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(" - 2", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.01", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFlags", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFlags", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFlags", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFlags", bar, StringComparison.Ordinal);
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
