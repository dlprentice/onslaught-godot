// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// When retail's last frontend draw is the 32x32 <c>mouse.tga</c> sprite.
///
/// <para><b>MEASURED</b> by the 2026-07-27 d3d9 sweep
/// (<c>local-lab/D3D9-FULL-SWEEP-2026-07-27.md</c>, inventories under
/// <c><maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\</c>) and the
/// 2026-07-28 FMV presentation log
/// (<c>local-lab/FMV-PRESENTATION-2026-07-28.md</c>). No Godot types.
/// HandleKey, DrawLoading, DrawQuitConfirm, and HandlePointerConfirm stay
/// untouched. No fade is invented.</para>
///
/// <para><b>Boot pages 2-4 are CFEPIntro.</b> <c>boot-second-page.csv</c>
/// frame 50, <c>boot-third-page.csv</c> frame 70, and
/// <c>boot-fourth-page.csv</c> frame 110 (A3, <c>-skipfmv</c>) Clear to
/// <c>0x001F1F3F</c> and end on a 32x32 screen-space TRISTRIP at posted
/// (0,0) after the 1024 splash and the title-logo slam. That is
/// <see cref="RetailFrontendScreen.ClickToStart"/>, not a
/// <see cref="RetailStartupSequence"/> beat. The frontend is hidden only
/// while the sequence owns the screen; those inventories were taken with
/// <c>-skipfmv</c>, so the sequence was never there.</para>
///
/// <para><b>Not drawn on boot-first or LOADING.</b> Both Clear to
/// <c>0x00000000</c> and end on a 512x512 text quad
/// (<c>boot-first-page.csv</c>, <c>loading.csv</c>).</para>
///
/// <para><b>Not drawn on startup media.</b> The no-skipfmv FMV capture is
/// one letterboxed TRIFAN per frame. There is no cold-start producer for a
/// splash-beat cursor in the specimen immediates, so Splash stays false
/// rather than inventing one.</para>
///
/// <para>Quad, UV, and hotspot stay the measured 32 / 124 / (0,0) law
/// already cited on <c>RetailFrontendFlow.Cursor</c>.</para>
/// </summary>
public static class RetailFrontendCursor
{
    /// <summary>Measured 32x32 on all fourteen cursor draws.</summary>
    public const float QuadSize = 32f;

    /// <summary>0.96875 * 128 = 124 texels of the 128x128 page.</summary>
    public const float SourceExtent = 124f;

    /// <summary>
    /// Posted cursor on boot-second/third/fourth before any move.
    /// Cursor globals <c>0x0089BDA8</c>/<c>0x0089BDA4</c> start at 0.
    /// </summary>
    public const float UnmovedPostedX = 0f;

    /// <inheritdoc cref="UnmovedPostedX"/>
    public const float UnmovedPostedY = 0f;

    /// <summary>
    /// Last-draw cursor on the interactive frontend, including
    /// <see cref="RetailFrontendScreen.ClickToStart"/> (boot pages 2-4).
    /// </summary>
    public static bool ShouldDrawOnFrontend(RetailFrontendScreen screen) =>
        screen is not (
            RetailFrontendScreen.Loading or
            RetailFrontendScreen.IntroCutscene or
            RetailFrontendScreen.Gameplay);

    /// <summary>
    /// Startup-media frames do not grow a cursor. Video is one FMV draw;
    /// Splash / Black / Finished have no measured cursor producer.
    /// </summary>
    public static bool ShouldDrawOnStartupMedia(RetailStartupFrameKind _) => false;
}
