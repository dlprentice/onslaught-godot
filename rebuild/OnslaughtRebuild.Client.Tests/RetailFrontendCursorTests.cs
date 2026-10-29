// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the frontend mouse-cursor last-draw law measured by the 2026-07-27
/// d3d9 sweep (the private lab note D3D9-FULL-SWEEP-2026-07-27.md, inventories
/// under <c><maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\</c>).
///
/// <para><b>Boot pages 2-4 are CFEPIntro, not FMV.</b> <c>boot-second-page.csv</c>
/// / <c>boot-third-page.csv</c> / <c>boot-fourth-page.csv</c> (A3, <c>-skipfmv</c>)
/// end on a 32x32 <c>A8R8G8B8</c> 8-mip TRISTRIP at posted (0,0) after the
/// 1024 splash and the title-logo slam. That is <c>FEP_INTRO</c> /
/// <see cref="RetailFrontendScreen.ClickToStart"/>, which this reconstruction
/// already owns. Those pages are not <see cref="RetailStartupSequence"/> beats:
/// the 2026-07-28 no-skipfmv FMV capture is one letterboxed video draw per
/// frame and has no cursor.</para>
///
/// <para><b>Not drawn.</b> <c>boot-first-page.csv</c> and <c>loading.csv</c>
/// Clear to <c>0x00000000</c> and end on a 512x512 text quad. Startup-media
/// Video / Splash / Black / Finished do not grow a cursor either — there is
/// no cold-start producer for one, and inventing it would put a sprite on
/// the FMV path the 2026-07-28 log refutes.</para>
///
/// <para>The mutations these cases kill are drawing <c>mouse.tga</c> from
/// <c>RetailStartupSequence._Draw</c>, skipping ClickToStart, or drawing on
/// Loading / IntroCutscene / Gameplay.</para>
/// </summary>
public sealed class RetailFrontendCursorTests
{
    [Fact]
    public void BootSecondThirdAndFourthPagesAreClickToStartAndDrawTheCursor()
    {
        // page-index.csv: boot-second/third/fourth Clear 0x001F1F3F and end
        // on the 32x32 cursor at posted (0,0). That is CFEPIntro, not FMV.
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.ClickToStart));
        Assert.Equal(0f, RetailFrontendCursor.UnmovedPostedX);
        Assert.Equal(0f, RetailFrontendCursor.UnmovedPostedY);
        Assert.Equal(32f, RetailFrontendCursor.QuadSize);
        Assert.Equal(124f, RetailFrontendCursor.SourceExtent);
    }

    [Fact]
    public void InteractiveFrontendPagesDrawTheCursorAndLoadingDoesNot()
    {
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.MainMenu));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.Options));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.DevSelect));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.Debriefing));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.LevelSelect));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.MissionBriefing));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.SelectConfiguration));
        Assert.True(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.QuitConfirm));

        // boot-first-page.csv / loading.csv: Clear 0x00000000, no cursor.
        Assert.False(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.Loading));
        Assert.False(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.IntroCutscene));
        Assert.False(RetailFrontendCursor.ShouldDrawOnFrontend(
            RetailFrontendScreen.Gameplay));
    }

    [Fact]
    public void StartupMediaNeverDrawsTheCursor()
    {
        // FMV-PRESENTATION-2026-07-28: one TRIFAN per frame. No mouse.tga.
        // Splash cursor is unmeasured; do not invent one.
        Assert.False(RetailFrontendCursor.ShouldDrawOnStartupMedia(
            RetailStartupFrameKind.Video));
        Assert.False(RetailFrontendCursor.ShouldDrawOnStartupMedia(
            RetailStartupFrameKind.Splash));
        Assert.False(RetailFrontendCursor.ShouldDrawOnStartupMedia(
            RetailStartupFrameKind.Black));
        Assert.False(RetailFrontendCursor.ShouldDrawOnStartupMedia(
            RetailStartupFrameKind.Finished));
    }

    [Fact]
    public void CursorLayerConsumesTheFrontendLawAndStartupSequenceDoesNotDrawMouseTga()
    {
        string cursor = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.Cursor.cs"));
        string sequence = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailStartupSequence.cs"));

        Assert.Contains("RetailFrontendCursor.ShouldDrawOnFrontend", cursor, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendCursor.QuadSize", cursor, StringComparison.Ordinal);
        Assert.Contains("RetailFrontendCursor.SourceExtent", cursor, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendScreen.Loading or", cursor, StringComparison.Ordinal);

        Assert.DoesNotContain("mouse.tga", sequence, StringComparison.Ordinal);
        Assert.DoesNotContain("LoadMouseCursorTexture", sequence, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailFrontendCursor", sequence, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", cursor, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", cursor, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", cursor, StringComparison.Ordinal);
    }
}
