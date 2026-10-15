// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay pre-draw enable-byte leftover
/// after sprintf and before dest/Z/font/draw, recovered from official
/// 74154bfa
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
/// FONT_SMALL / Font13PS. RetailMainMenuVersionOverlayFlags already
/// owns the post-draw restore of <c>[0x00679B40]=1</c> at
/// <c>0x004641FC</c>. Official bytes independently re-read this
/// cycle: after sprintf <c>0x00464174</c> / <c>add esp, 0x10</c> at
/// <c>0x00464179</c> / <c>lea edx, [esp+0x3C]</c> at
/// <c>0x0046417C</c>, <c>0x00464180</c> is
/// <c>mov byte [0x00679B40], 0</c>. The two-instruction reader at
/// <c>0x00465F00</c> is <c>mov al, [0x00679B40]; ret</c> with six
/// CALL sites. The <c>0x00464191</c> <c>Text__AsciiToWideScratch</c>
/// call is a sibling, not this leftover. DrawMainMenu keeps
/// title-font DrawText. Do not invent a fade, a sheen, dest
/// immediates, or a 2px kerning hack. Do not redo version dest/Z,
/// version font slot, version post-draw flags, title-logo dest/Z,
/// title-logo shadow dest/Z, selector-bar Z/X, writing Z/X,
/// 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3, 0x00463D1F,
/// 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayEnableTests
{
    [Fact]
    public void SpecimenSitesArePreDrawZeroAfterSprintfNotAFade()
    {
        Assert.Equal(0x00464174u, RetailMainMenuVersionOverlayEnable.SprintfSite);
        Assert.Equal(0x0055DE9Bu, RetailMainMenuVersionOverlayEnable.Sprintf);
        Assert.Equal(0x00464179u, RetailMainMenuVersionOverlayEnable.AddEspSite);
        Assert.Equal(0x10, RetailMainMenuVersionOverlayEnable.AddEspImmediate);
        Assert.Equal(0x0046417Cu, RetailMainMenuVersionOverlayEnable.LeaSite);
        Assert.Equal(0x3C, RetailMainMenuVersionOverlayEnable.LeaDisp);
        Assert.Equal(0x00464180u, RetailMainMenuVersionOverlayEnable.StoreSite);
        Assert.Equal(0x00679B40u, RetailMainMenuVersionOverlayEnable.EnableByteGlobal);
        Assert.Equal(0, RetailMainMenuVersionOverlayEnable.EnableByteBeforeDraw);
        Assert.Equal(0x00464187u, RetailMainMenuVersionOverlayEnable.NextInstructionSite);
        Assert.Equal(0x00464191u, RetailMainMenuVersionOverlayEnable.AsciiToWideSiblingSite);
        Assert.Equal(0x004F7BF0u, RetailMainMenuVersionOverlayEnable.AsciiToWideScratch);
        Assert.Equal(0x00465F00u, RetailMainMenuVersionOverlayEnable.EnableByteReader);
        Assert.Equal(0xA0u, RetailMainMenuVersionOverlayEnable.ReaderMovAlOpcode);
        Assert.Equal(0xC3u, RetailMainMenuVersionOverlayEnable.ReaderRetOpcode);
        Assert.Equal(
            new uint[]
            {
                0x005235A7u,
                0x00523705u,
                0x0052384Bu,
                0x0052398Fu,
                0x00540061u,
                0x005562BDu,
            },
            RetailMainMenuVersionOverlayEnable.ReaderCallSites);
        Assert.Equal(0x00468500u, RetailMainMenuVersionOverlayEnable.RunStoreOneSiblingSite);
        Assert.Equal(0x0046858Bu, RetailMainMenuVersionOverlayEnable.RunStoreZeroSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlay.FormatSite,
            RetailMainMenuVersionOverlayEnable.FormatSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteStoreSite,
            RetailMainMenuVersionOverlayEnable.AfterDrawSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteGlobal,
            RetailMainMenuVersionOverlayEnable.EnableByteGlobal);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDrawSite,
            RetailMainMenuVersionOverlayEnable.StoreSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteBeforeDraw,
            RetailMainMenuVersionOverlayEnable.EnableByteBeforeDraw);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteReader,
            RetailMainMenuVersionOverlayEnable.EnableByteReader);
        Assert.Equal(
            RetailMainMenuVersionOverlayFlags.EnableByteAfterDraw,
            RetailMainMenuVersionOverlayEnable.EnableByteAfterDraw);
        Assert.True(RetailMainMenuVersionOverlayEnable.OwnsBeforeDrawStore);
        Assert.False(RetailMainMenuVersionOverlayFlags.OwnsBeforeDrawStore);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsAfterDrawStore);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsCFrontEndRunStores);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsAsciiToWide);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayEnable.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayEnable.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsTitleLogoScale);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesVersionOverlayZ);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesVersionOverlayFont);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesVersionOverlayFlags);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesTitleLogoShadow);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayEnable.UsesTwinFadeGate);
    }

    [Fact]
    public void BeforeDrawStoreIsZeroBetweenSprintfAndDestZ()
    {
        Assert.Equal(0, RetailMainMenuVersionOverlayEnable.BeforeDrawStore());
        Assert.NotEqual(
            RetailMainMenuVersionOverlayEnable.EnableByteBeforeDraw,
            RetailMainMenuVersionOverlayEnable.EnableByteAfterDraw);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.FormatSiblingSite <
            RetailMainMenuVersionOverlayEnable.SprintfSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.SprintfSite <
            RetailMainMenuVersionOverlayEnable.AddEspSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.AddEspSite <
            RetailMainMenuVersionOverlayEnable.LeaSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.LeaSite <
            RetailMainMenuVersionOverlayEnable.StoreSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.StoreSite <
            RetailMainMenuVersionOverlayEnable.NextInstructionSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.NextInstructionSite <
            RetailMainMenuVersionOverlayEnable.AsciiToWideSiblingSite);
        Assert.True(
            RetailMainMenuVersionOverlayEnable.AsciiToWideSiblingSite <
            RetailMainMenuVersionOverlayZ.ScalePushSite);
        Assert.True(
            RetailMainMenuVersionOverlayZ.CallSite <
            RetailMainMenuVersionOverlayEnable.AfterDrawSiblingSite);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayEnable.StoreSite,
            RetailMainMenuVersionOverlayEnable.AfterDrawSiblingSite);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayEnable.EnableByteReader,
            RetailMainMenuVersionOverlayEnable.AsciiToWideScratch);
        Assert.Equal(6, RetailMainMenuVersionOverlayEnable.ReaderCallSites.Length);
        Assert.DoesNotContain(
            RetailMainMenuVersionOverlayEnable.StoreSite,
            RetailMainMenuVersionOverlayEnable.ReaderCallSites);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsAfterDrawStore);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsAsciiToWide);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayEnable.RedoesVersionOverlayFlags);
        Assert.False(RetailMainMenuVersionOverlayEnable.InventsDestImmediates);
    }

    [Fact]
    public void DrawMainMenuKeepsTitleFontDrawTextAndDoesNotInventFade()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlayEnable", draw, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailMainMenuVersionOverlayEnable", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayEnable", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayEnable", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayEnable", bar, StringComparison.Ordinal);
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
