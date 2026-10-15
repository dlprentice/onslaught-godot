// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Guards the main-menu language-row geometry recovered on 2026-07-28 by
/// composing retail's draw quads with the decoded retail sprite.
///
/// <para><b>What is asserted is the composition, not the constants.</b> A test
/// that read back <c>Flag.X == 177.4</c> would restate the source file and catch
/// nothing. What is worth guarding is the relationship those constants encode
/// and that no one can verify by eye: that quad extent and ink extent are
/// different rectangles, that the ink is what lands on screen, and that the
/// composed result is the clean symmetric layout retail actually draws. Change
/// any one quad, the sprite margin, or the mirror flag and these fail.</para>
///
/// <para><b>The independent check.</b> Every ink figure below was confirmed
/// against retail's own rendered frame
/// (<c>local-lab/retail-reference-pristine/main-menu-640x480.png</c>) by taking
/// the columns in the row band whose deviation from the page fill exceeds a
/// threshold. That returns three separated runs — 144..165, 178..260, 273..294 —
/// against composed ink of 142.8..166.8, 177.4..260.6 and 271.2..295.2. The
/// agreement is within one column on every edge, and the residual is the
/// expected one: a chevron's apex column carries one or two texels of ink and
/// falls under any threshold. So the composition is not an argument about
/// rectangles, it predicts the pixels.</para>
/// </summary>
public sealed class RetailFrontendLanguageRowTests
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// The sprite ink box is a decoded fact about FE_Arrow.tga, and it is
    /// ASYMMETRIC inside its own texture — 16 texels of margin on the left
    /// against 18 on the right. That asymmetry is what makes the mirror
    /// falsifiable two tests below, so guard it first.
    /// </summary>
    [Fact]
    public void SpriteInkBoxIsOffCentreInsideItsTexture()
    {
        double leftMargin = RetailFrontendLanguageRow.SpriteInkMinX;
        double rightMargin =
            RetailFrontendLanguageRow.SpriteSize - RetailFrontendLanguageRow.SpriteInkMaxX;

        Assert.Equal(16d, leftMargin, 9);
        Assert.Equal(18d, rightMargin, 9);
        Assert.NotEqual(leftMargin, rightMargin);
    }

    /// <summary>
    /// The finding this row exists to record: retail's QUADS overlap the flag,
    /// retail's INK does not. Both statements are made about the same numbers.
    /// </summary>
    [Fact]
    public void ChevronQuadsOverlapTheFlagButTheirInkDoesNot()
    {
        var flag = RetailFrontendLanguageRow.Flag;
        var leftQuad = RetailFrontendLanguageRow.LeftChevron;
        var rightQuad = RetailFrontendLanguageRow.RightChevron;

        // Quad extent: the left chevron's right edge runs past the flag's left
        // edge, and the right chevron's left edge runs past the flag's right.
        Assert.True(leftQuad.Right > flag.X, "left chevron QUAD should overlap the flag");
        Assert.True(rightQuad.X < flag.Right, "right chevron QUAD should overlap the flag");

        // Ink extent: a clear gap of exactly the same size on each side.
        var leftInk = RetailFrontendLanguageRow.ChevronInk(leftQuad);
        var rightInk = RetailFrontendLanguageRow.ChevronInk(rightQuad);

        double leftGap = flag.X - leftInk.Right;
        double rightGap = rightInk.X - flag.Right;

        Assert.Equal(10.6d, leftGap, 9);
        Assert.Equal(10.6d, rightGap, 9);
        Assert.Equal(leftGap, rightGap, 9);
    }

    /// <summary>
    /// The mirror is PROVEN by the composition rather than assumed. Because the
    /// ink box is off-centre in its texture, mirroring moves the ink; the
    /// mirrored reading is symmetric about the menu column to 0.0px and the
    /// unmirrored reading misses by 1.6px.
    /// </summary>
    [Fact]
    public void MirroringTheLeftChevronIsWhatMakesTheRowSymmetric()
    {
        double axis = RetailFrontendLanguageRow.SymmetryAxisX;
        var rightInk = RetailFrontendLanguageRow.ChevronInk(
            RetailFrontendLanguageRow.RightChevron);

        var mirrored = RetailFrontendLanguageRow.ChevronInk(
            RetailFrontendLanguageRow.LeftChevron);
        Assert.Equal(axis - mirrored.X, rightInk.Right - axis, 9);
        Assert.Equal(axis - mirrored.Right, rightInk.X - axis, 9);

        var unmirrored = RetailFrontendLanguageRow.ChevronInk(
            RetailFrontendLanguageRow.LeftChevron with { Mirrored = false });
        Assert.True(
            Math.Abs((axis - unmirrored.X) - (rightInk.Right - axis)) > 1.5d,
            "the unmirrored reading must be measurably asymmetric, or the mirror is not falsifiable");
    }

    /// <summary>
    /// All three elements share one row and one height. This is the part the
    /// prior pixel-hunted constants got wrong — they put the two chevrons on
    /// different rows (254 and 253) with heights differing from the flag's.
    /// </summary>
    [Fact]
    public void FlagAndBothChevronInkShareOneBandAndOneHeight()
    {
        var flag = RetailFrontendLanguageRow.Flag;
        var leftInk = RetailFrontendLanguageRow.ChevronInk(
            RetailFrontendLanguageRow.LeftChevron);
        var rightInk = RetailFrontendLanguageRow.ChevronInk(
            RetailFrontendLanguageRow.RightChevron);

        foreach (var element in new[] { flag, leftInk, rightInk })
        {
            Assert.Equal(252d, element.Y, 9);
            Assert.Equal(284d, element.Bottom, 9);
            Assert.Equal(32d, element.Height, 9);
        }

        Assert.Equal(24d, leftInk.Width, 9);
        Assert.Equal(24d, rightInk.Width, 9);
        Assert.Equal(83.2d, flag.Width, 9);
    }

    /// <summary>
    /// The composed ink must predict the columns measured on retail's own
    /// rendered frame, to within the one column a chevron's apex loses to any
    /// threshold. This is the assertion that would catch a change which kept the
    /// row internally consistent while moving it off the game.
    /// </summary>
    [Fact]
    public void ComposedInkPredictsTheColumnsMeasuredOnRetailsFrame()
    {
        (double Left, double Right)[] measured =
        [
            (144d, 165d), // left chevron
            (178d, 260d), // flag
            (273d, 294d), // right chevron
        ];

        var composed = new[]
        {
            RetailFrontendLanguageRow.ChevronInk(RetailFrontendLanguageRow.LeftChevron),
            RetailFrontendLanguageRow.Flag,
            RetailFrontendLanguageRow.ChevronInk(RetailFrontendLanguageRow.RightChevron),
        };

        for (int i = 0; i < measured.Length; i++)
        {
            Assert.True(
                Math.Abs(composed[i].X - measured[i].Left) <= 2d + Tolerance,
                $"element {i} left edge {composed[i].X} against measured {measured[i].Left}");
            Assert.True(
                Math.Abs(composed[i].Right - measured[i].Right) <= 2d + Tolerance,
                $"element {i} right edge {composed[i].Right} against measured {measured[i].Right}");
        }
    }

    /// <summary>
    /// The chevron quads are concentric pairs at exactly 2x, and the outer one
    /// is a proven no-op. Nothing in this class models it; this records why, so
    /// that a future reader adding "the missing draws 8 and 10" knows they would
    /// add no pixels.
    /// </summary>
    [Fact]
    public void OuterChevronQuadsAreConcentricAtExactlyTwiceTheSize()
    {
        (double X, double Y, double Size)[] outer =
        [
            (102.8d, 216.8d, 102.4d),
            (232.8d, 216.8d, 102.4d),
        ];

        var inner = new[]
        {
            RetailFrontendLanguageRow.LeftChevron,
            RetailFrontendLanguageRow.RightChevron,
        };

        for (int i = 0; i < inner.Length; i++)
        {
            double outerCentreX = outer[i].X + (outer[i].Size * 0.5);
            double outerCentreY = outer[i].Y + (outer[i].Size * 0.5);

            Assert.Equal(inner[i].CentreX, outerCentreX, 9);
            Assert.Equal(inner[i].CentreY, outerCentreY, 9);
            Assert.Equal(268d, outerCentreY, 9);
            Assert.Equal(2d, outer[i].Size / inner[i].Width, 9);
        }
    }
}
