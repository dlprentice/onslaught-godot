// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// The released main-menu language-selector row — flag plus two chevrons —
/// recovered 2026-07-28 by composing retail's own draw quads with the decoded
/// retail sprite, which is a different thing from either one alone.
///
/// <para><b>Why this class exists at all.</b> The two prior write-ups of this
/// row both ended at "blocked on texture contents, which the proxy does not
/// capture", and left the row as the one main-menu element with no verdict. That
/// was the wrong conclusion from a correct premise. The proxy genuinely does not
/// wrap textures — it records quad, format and dimensions and never ink extent —
/// but the sprite is a shipped file sitting on the same machine, and the missing
/// half of the composition was one <c>zlib.decompress</c> away.</para>
///
/// <para><b>The trap, stated plainly, because it generalises to every sprite on
/// every page.</b> A draw-call inventory records where the QUAD is. What a
/// player sees is where the INK is. For a tightly-cropped sprite those are the
/// same rectangle and the distinction is invisible; for a sprite with a
/// transparent margin they are not, and the error is silent. Read as appearance,
/// retail's quads say the left chevron's right edge (179.6) runs 2.2px past the
/// flag's left edge (177.4) — that the chevron sits ON the flag. Composed with
/// the sprite, the same numbers give a 10.6px clear gap. One reading is the
/// opposite of the other and nothing in the inventory distinguishes them.</para>
///
/// <para><b>Evidence — the quads.</b>
/// <c><maintainer evidence>\bea-frontend-pages\SWEEP-2026-07-27\inventories\main-menu-settled.csv</c>,
/// the <c>-skipfmv</c> main menu through the passive d3d9 proxy. Frame 3000
/// (39 draws):</para>
///
/// <code>
///   draw  6  flag           (177.4,252.0)-(260.6,284.0)   83.2 x 32.0   128x128 DXT1  0xFD3F3F3F
///   draw  7  left chevron   (128.4,242.4)-(179.6,293.6)   51.2 x 51.2    64x64  DXT2  0x3E7F7F7F
///   draw  8  left chevron   (102.8,216.8)-(205.2,319.2)  102.4 x 102.4   64x64  DXT2  0x007F7F7F
///   draw  9  right chevron  (258.4,242.4)-(309.6,293.6)   51.2 x 51.2    64x64  DXT2  0x3E7F7F7F
///   draw 10  right chevron  (232.8,216.8)-(335.2,319.2)  102.4 x 102.4   64x64  DXT2  0x007F7F7F
/// </code>
///
/// <para>All five rectangles are bit-identical on frame 4000 and on all 250
/// frames of <c>main-menu-reveal-frames-613-900.csv</c> that carry the sprite,
/// so they are fixed released constants and not a sampled phase. All five sample
/// u,v 0..1 and blend SRCALPHA/INVSRCALPHA.</para>
///
/// <para><b>Evidence — the sprite.</b> <c>FE_Arrow.tga</c>, materialized as
/// <c>Assets/Frontend/fe-arrow.texture.aya</c> from
/// <c>data/resources/dxtntextures/FrontEnd%v2%FE_Arrow.tga(0)A8R8G8B8.aya</c>
/// (manifest sha256 <c>ecf729f9…</c>, byte-identical to the copy under
/// a private pristine copy). It inflates to a DDS of width 64,
/// height 64, FourCC <c>DXT2</c> — which is what the inventory records for
/// draws 7-10, so the file and the draw are the same object. Its explicit 4-bit
/// alpha is a hard 0 or 255 with no feather: the ink bounding box is identical
/// at every threshold from &gt;0 to &gt;127, at texels x 16..46, y 12..52. The
/// glyph is a chevron pointing RIGHT.</para>
///
/// <para><b>The composition.</b> Ink u 0.25..0.71875, v 0.1875..0.8125 through
/// the 51.2px quads gives:</para>
///
/// <code>
///   left chevron ink   (142.8,252.0)-(166.8,284.0)   24.0 x 32.0
///   flag               (177.4,252.0)-(260.6,284.0)   83.2 x 32.0
///   right chevron ink  (271.2,252.0)-(295.2,284.0)   24.0 x 32.0
/// </code>
///
/// <para>Three elements, all exactly 32 tall on exactly the same rows, separated
/// by exactly 10.6px on each side. The flag needs no ink correction: its texture
/// is 128x128 DXT1 with no alpha channel, decodes fully opaque at every texel,
/// and therefore fills its quad.</para>
///
/// <para><b>The left chevron is mirrored, and the composition PROVES it rather
/// than assuming it.</b> The ink box is off-centre inside its own texture — 16
/// texels of margin on the left against 18 on the right — so mirroring is not a
/// no-op and the two hypotheses give different screen positions. Mirrored, the
/// row is symmetric about x = 219.0 to 0.0px: 219-142.8 = 76.2 = 295.2-219, and
/// 219-166.8 = 52.2 = 271.2-219. Unmirrored it misses by 1.6px and the two gaps
/// come out 12.2 and 10.6. A 1.6px asymmetry arising from a 2-texel margin
/// difference is not something a layout would produce by accident.</para>
///
/// <para><b>Draws 8 and 10 are provable no-ops.</b> Each is a second copy of its
/// chevron at exactly twice the size, concentric on the same centre. It cannot
/// produce a pixel: diffuse alpha is 0x00 in 500 of 500 sampled rows across 252
/// distinct frames, stage-0 ALPHAOP is MODULATE(TEXTURE, DIFFUSE) so the result
/// alpha is zero, and ALPHATEST is enabled at GREATEREQUAL with reference 8,
/// which rejects every texel before blending is even reached. They are not
/// modelled here. Note what that costs a draw-count bar: two of retail's 39
/// draws contribute nothing, so 39 is not the number of things a renderer must
/// produce.</para>
///
/// <para><b>What is NOT established.</b> The selected-row sine is pinned by
/// <c>RetailMainMenuLanguageSine</c>; session cannot hold <c>this+0x08 = -1</c>
/// so this class does not light it. Chevron hide/show is
/// <c>RetailMainMenuLanguageBlink</c> (signed <c>mCounter % 64</c> below 50).
/// Absolute phase still depends on a Process increment this lane does not
/// invent. The 2x copies stay unmodelled no-ops.</para>
/// </summary>
public static class RetailFrontendLanguageRow
{
    /// <summary>Decoded FE_Arrow ink bounds, in texels of its 64x64 DDS.</summary>
    public const double SpriteSize = 64.0;

    /// <summary>Left edge of the chevron ink, texels. Decoded, not authored.</summary>
    public const double SpriteInkMinX = 16.0;

    /// <summary>Right edge of the chevron ink, texels, exclusive.</summary>
    public const double SpriteInkMaxX = 46.0;

    /// <summary>Top edge of the chevron ink, texels.</summary>
    public const double SpriteInkMinY = 12.0;

    /// <summary>Bottom edge of the chevron ink, texels, exclusive.</summary>
    public const double SpriteInkMaxY = 52.0;

    /// <summary>Retail's language flag quad, frame 3000 draw 6.</summary>
    public static Quad Flag { get; } = new(177.4, 252.0, 83.2, 32.0, Mirrored: false);

    /// <summary>Retail's left chevron quad, frame 3000 draw 7. Mirrored.</summary>
    public static Quad LeftChevron { get; } = new(128.4, 242.4, 51.2, 51.2, Mirrored: true);

    /// <summary>Retail's right chevron quad, frame 3000 draw 9.</summary>
    public static Quad RightChevron { get; } = new(258.4, 242.4, 51.2, 51.2, Mirrored: false);

    /// <summary>
    /// The x that both chevrons and the flag are symmetric about. Equal to the
    /// menu column anchor, which is why the symmetry is worth asserting: it is a
    /// second, independent witness that <c>MenuColumnX</c> is a centre.
    /// </summary>
    public const double SymmetryAxisX = 219.0;

    /// <summary>
    /// Where a chevron's visible ink lands, composing its quad with the decoded
    /// sprite ink box. For the flag this is the quad itself — its texture is
    /// DXT1 with no alpha and decodes opaque everywhere.
    /// </summary>
    public static Quad ChevronInk(Quad quad)
    {
        double leftMargin = quad.Mirrored
            ? SpriteSize - SpriteInkMaxX
            : SpriteInkMinX;
        double rightMargin = quad.Mirrored
            ? SpriteInkMinX
            : SpriteSize - SpriteInkMaxX;

        double sx = quad.Width / SpriteSize;
        double sy = quad.Height / SpriteSize;

        return new Quad(
            quad.X + (leftMargin * sx),
            quad.Y + (SpriteInkMinY * sy),
            quad.Width - ((leftMargin + rightMargin) * sx),
            (SpriteInkMaxY - SpriteInkMinY) * sy,
            quad.Mirrored);
    }

    /// <summary>A screen-space quad in the released 640x480 design stage.</summary>
    public readonly record struct Quad(
        double X,
        double Y,
        double Width,
        double Height,
        bool Mirrored)
    {
        /// <summary>Right edge.</summary>
        public double Right => X + Width;

        /// <summary>Bottom edge.</summary>
        public double Bottom => Y + Height;

        /// <summary>Horizontal centre.</summary>
        public double CentreX => X + (Width * 0.5);

        /// <summary>Vertical centre.</summary>
        public double CentreY => Y + (Height * 0.5);
    }
}
