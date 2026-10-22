// SPDX-License-Identifier: GPL-3.0-only

using Godot;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The released frontend mouse cursor sprite.
///
/// <para><b>Why this file exists.</b> Until 2026-07-27 this reconstruction drew
/// no cursor at all. It delegated the pointer to the host window
/// (<c>CursorModeRequested</c>), and the parity gate scores the viewport texture,
/// which the OS pointer is not part of — so a systematically missing element
/// survived every green gate. It was found by inventorying retail's own draw
/// calls rather than by comparing pixels.</para>
///
/// <para><b>MEASURED — retail draws it as the LAST draw of every interactive
/// frontend frame.</b> 2026-07-27 d3d9-proxy sweep,
/// <c>local-lab/D3D9-FULL-SWEEP-2026-07-27.md</c>; per-draw inventories under
/// <c><maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\</c>. Thirteen distinct
/// pages, four independent launches, and in every one of them the final draw of
/// the frame is a 32x32 screen-space (<c>D3DFVF_XYZRHW</c>) TRISTRIP whose
/// top-left is exactly the posted cursor position:</para>
///
/// <code>
///   page                  frame  draw   rect                      posted cursor
///   boot-second-page        50     3    (0,0)-(32,32)             (0,0)
///   boot-third-page         70     8    (0,0)-(32,32)             (0,0)
///   boot-fourth-page       110     9    (0,0)-(32,32)             (0,0)
///   click-to-start         600     8    (320,240)-(352,272)       (320,240)
///   main-menu-settled     3000    38    (320,240)-(352,272)       (320,240)
///   options-root          2500    29    (219,404)-(251,436)       (219,404)
///   options-controller    3200   167    (320,253)-(352,285)       (320,253)
///   options-sound         4900    73    (320,273)-(352,305)       (320,273)
///   options-video         6700    73    (320,293)-(352,325)       (320,293)
///   options-credits       3100     7    (320,313)-(352,345)       (320,313)
///   choose-game-name      2100    66    (219,304)-(251,336)       (219,304)
///   select-level          2500    82    (618,450)-(650,482)       (618,450)
///   mission-briefing      3600    41    (618,450)-(650,482)       (618,450)
///   select-configuration  4050   292    (618,450)-(650,482)       (618,450)
/// </code>
///
/// <para>The rect tracks the posted position with zero residual on all fourteen,
/// so the <b>hotspot is (0,0)</b> — the quad's top-left corner, not its centre.
/// The <c>select-level</c> / <c>briefing</c> / <c>select-configuration</c> rows
/// also show retail does <b>not</b> clamp the quad to the client area: at
/// (618,450) it runs 10px past the right edge and 2px past the bottom.</para>
///
/// <para><b>NOT drawn on two pages, and that is measured too.</b> The boot FIRST
/// page and the LOADING page — the only two frontend pages that
/// <c>Clear</c> to <c>0x00000000</c> rather than <c>0x001F1F3F</c> — end their
/// frames on a 512x512 text quad and carry no cursor draw at all
/// (<c>boot-first-page.csv</c>, <c>loading.csv</c>). This file therefore skips
/// <see cref="RetailFrontendScreen.Loading"/>.</para>
///
/// <para><b>The texture identity, recovered from the shipped bytes.</b> The
/// proxy does not wrap textures, so the sweep measured only
/// dimensions/format/mip-count — 128x128, <c>D3DFMT_A8R8G8B8</c>, 8 levels. The
/// name comes from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (verified in this session, <b>not</b> the installed executable, which is
/// deliberately patched):</para>
///
/// <list type="bullet">
/// <item>VA <c>0x00640058</c> (file <c>0x240058</c>) holds the ASCII
/// <c>"mouse.tga\0"</c> — the string the cursor renderer at <c>0x00523A70</c>
/// passes to <c>CTexture::FindTexture</c>.</item>
/// <item>VA <c>0x00640054</c> (file <c>0x240054</c>) holds <c>FF 00 00 00</c>,
/// and the renderer builds its diffuse as <c>(that &lt;&lt; 24) | 0xFFFFFF</c> =
/// <c>0xFFFFFFFF</c> — exactly the diffuse the sweep measured on all fourteen
/// draws.</item>
/// <item>The fallback string at VA <c>0x00625498</c> is
/// <c>meshtex\default.tga</c>. That file is also 128x128/8-mip but is created
/// <c>A1R5G5B5</c>, so the measured <c>A8R8G8B8</c> rules the fallback out and
/// proves the real texture resolved.</item>
/// </list>
///
/// <para>The retail file is
/// <c>data/resources/dxtntextures/mouse.tga(0)A8R8G8B8.aya</c>, sha256
/// <c>366021def699de220ad018c40250eefaccaab356c6c5d93fe0aa1b7f5302354c</c>,
/// which inflates to a 128x128 <b>DXT2</b> DDS with <c>mipMapCount = 8</c>
/// (header read directly in this session). The on-disk DXT2 against the
/// runtime's A8R8G8B8 is not a contradiction: this cursor load validates all
/// eight stored levels and explicitly converts the decoded image to RGBA8
/// before upload. The materializer entry is
/// <c>Frontend/mouse-cursor.texture.aya</c>.</para>
///
/// <para><b>The UV range is a released constant, not a full-texture blit.</b>
/// Every one of the fourteen draws samples <c>u 0..0.96875</c>,
/// <c>v 0..0.96875</c> — 124 of the 128 texels on each axis — into the 32px
/// quad. It is reproduced literally rather than rounded to 0..1.</para>
///
/// <para><b>MODULATE, not MODULATE2X.</b> The cursor is one of only two draws in
/// the settled main-menu frame whose stage-0 COLOROP is plain
/// <c>MODULATE(TEXTURE, DIFFUSE)</c> (<c>s0_cop = 4/2,0</c>); the other 37 are
/// MODULATE2X. So the diffuse is passed through as literal white and must NOT go
/// through <see cref="RetailFrontendFlow.RetailColor"/>, whose 2x is the
/// MODULATE2X law.</para>
///
/// <para>The host keeps frontend movement free while suppressing the OS pointer
/// through <see cref="RetailFrontendCursorMode.Custom"/>. Retail's exact
/// <c>ShowCursor</c> call was not captured; this is the host-side separation
/// needed to avoid drawing both the measured retail sprite and the Windows
/// pointer. Paused gameplay and focus release continue to request
/// <see cref="RetailFrontendCursorMode.Visible"/>.</para>
///
/// <para><b>Boot pages 2-4 are this flow's ClickToStart page.</b> The three
/// inventories that end on a 32x32 cursor at posted (0,0) are the CFEPIntro
/// slam (<c>boot-second/third/fourth-page.csv</c>, A3 <c>-skipfmv</c>), not
/// FMV beats. <see cref="RetailFrontendCursor.ShouldDrawOnFrontend"/> keeps
/// that last draw; <see cref="RetailFrontendCursor.ShouldDrawOnStartupMedia"/>
/// stays false because the 2026-07-28 FMV log is one video draw per frame.
/// Do not invent a splash-beat cursor. Do not draw on boot-first or LOADING.</para>
/// </summary>
public sealed partial class RetailFrontendFlow
{
    /// <summary>Quad edge in stage pixels. Measured 32x32 on all fourteen draws.</summary>
    private const float MouseCursorSize = RetailFrontendCursor.QuadSize;

    /// <summary>
    /// Sampled sub-rectangle of the 128x128 page: 0.96875 * 128 = 124 texels.
    /// </summary>
    private const float MouseCursorSourceExtent = RetailFrontendCursor.SourceExtent;

    private RetailMouseCursorLayer? _mouseCursorLayer;
    private Vector2? _captureMouseCursorDesignPosition;

    internal void SetMouseCursorDesignPositionForCapture(Vector2? position) =>
        _captureMouseCursorDesignPosition = position;

    private static Texture2D? LoadMouseCursorTexture()
    {
        const string path = "res://Assets/Frontend/mouse-cursor.texture.aya";
        return Godot.FileAccess.FileExists(path)
            ? CuratedAyaTextureLoader.Load(
                path,
                128,
                128,
                CuratedAyaTextureLoader.Compression.Dxt2,
                expectedTargetFormat: Image.Format.Rgba8,
                expectedMipCount: 8)
            : null;
    }

    /// <summary>
    /// A separate CanvasItem is required here. A child reflection layer renders
    /// after the frontend parent's <c>_Draw</c>; drawing the cursor in that
    /// parent therefore let the title sheen brighten the cursor. Retail's
    /// measured cursor draw is last, so this child owns an explicit higher
    /// Z-order.
    /// </summary>
    private sealed partial class RetailMouseCursorLayer : Node2D
    {
        private RetailFrontendFlow _owner = null!;
        private Texture2D? _texture;

        public void Configure(RetailFrontendFlow owner) => _owner = owner;

        public override void _Draw()
        {
            if (!RetailFrontendCursor.ShouldDrawOnFrontend(_owner._session.Screen))
            {
                return;
            }

            _texture ??= LoadMouseCursorTexture();
            if (_texture is null)
            {
                return;
            }

            (float scale, Vector2 offset) = _owner.DesignTransform();
            DrawSetTransform(offset, 0f, new Vector2(scale, scale));

            // Retail uses the posted stage coordinate as the quad top-left.
            // Capture runs use a deterministic design-space position so the
            // operator's live pointer cannot contaminate scored page bands.
            Vector2 position =
                _owner._captureMouseCursorDesignPosition ??
                _owner.ToDesignPosition(_owner.GetLocalMousePosition());
            DrawTextureRectRegion(
                _texture,
                new Rect2(position.X, position.Y, MouseCursorSize, MouseCursorSize),
                new Rect2(0f, 0f, MouseCursorSourceExtent, MouseCursorSourceExtent),
                Colors.White);

            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
