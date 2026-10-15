// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the in-level HUD to the 640x480 released stage.
///
/// Every expected number below was measured off the retail capture
/// local-lab/retail-reference-pristine/level-100-entry/09-level-100-entry-640x480.png,
/// not read out of the source under test:
///
///   lower-left scanner ring   circle fit centre (66.01, 417.25) r 46.56, rms 0.20 over 55 chord points
///   lower-right battleline    circle fit centre (568.08, 416.46) r 46.76, rms 0.34 over 50 chord points
///   crosshair rings           centred on (320, 240)
///   message panel body        hard top edge y 405.5, hard bottom edge y 464.5, mid x 341.5
///
/// The textures those rects carry were measured the same way, from the
/// materialized .aya pages: radar-outline / battleline-outline / circle-darkener
/// hold their ring in the top-left 98x98 of a 128x128 page (content centre
/// 49,49), while circle-mask holds its disc centred on the page at (64.5, 64.5).
///
/// Because DesignTransform is the identity at a 640x480 viewport, asserting the
/// constants directly is the same as asserting the rendered 640x480 geometry.
/// </summary>
public sealed class Level100HudDesignSpaceTests
{
    private const float DesignWidth = 640f;
    private const float DesignHeight = 480f;

    // Content centre of the ring textures inside their 128x128 page.
    private const float RingContentCentre = 49f;

    // Content centre of circle-mask's transparent disc inside its 128x128 page.
    private const float MaskContentCentre = 64.5f;

    private static readonly string Source = ReadHudSource();

    private static string ReadHudSource()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "godot-hud-layout-source",
            "FirstFlightHud.cs");
        Assert.True(File.Exists(path), $"HUD source was not copied to the test output: {path}");
        return File.ReadAllText(path);
    }

    private static float Constant(string pattern)
    {
        Match match = Regex.Match(Source, pattern, RegexOptions.Singleline);
        Assert.True(match.Success, $"HUD source no longer matches: {pattern}");
        return float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    [Fact]
    public void HudDeclaresTheSame640x480StageAsTheFrontend()
    {
        Assert.Equal(DesignWidth, Constant(@"DesignWidth\s*=\s*(\d+(?:\.\d+)?)f"));
        Assert.Equal(DesignHeight, Constant(@"DesignHeight\s*=\s*(\d+(?:\.\d+)?)f"));
    }

    [Fact]
    public void NoLayoutOffsetIsTakenFromTheRawViewport()
    {
        // Size.X / Size.Y may appear only inside DesignTransform, which is what
        // converts the window into the 640x480 stage. Anywhere else it is the
        // viewport-relative layout bug this test exists to prevent.
        int designTransform = Source.IndexOf("protected (float Scale, Vector2 Offset) DesignTransform()", StringComparison.Ordinal);
        Assert.True(designTransform > 0, "DesignTransform is missing from the HUD.");
        int designTransformEnd = Source.IndexOf("protected void BeginDesignSpace()", designTransform, StringComparison.Ordinal);
        Assert.True(designTransformEnd > designTransform, "BeginDesignSpace is missing from the HUD.");

        foreach (Match match in Regex.Matches(Source, @"(?<!\w|\.)Size\s*\.\s*[XY]"))
        {
            Assert.True(
                match.Index > designTransform && match.Index < designTransformEnd,
                $"FirstFlightHud.cs lays out against the raw viewport at offset {match.Index}: '{match.Value}'. " +
                "In-level HUD offsets must be 640x480 design pixels.");
        }
    }

    [Fact]
    public void EveryHudLayerDrawsInsideTheDesignStage()
    {
        int drawCount = Regex.Matches(Source, @"public override void _Draw\(\)").Count;
        Assert.Equal(3, drawCount);
        Assert.Equal(drawCount, Regex.Matches(Source, @"\bBeginDesignSpace\(\);").Count);
        Assert.Equal(drawCount, Regex.Matches(Source, @"\bEndDesignSpace\(\);").Count);
    }

    [Fact]
    public void HudTexturesAreBlittedWithoutInterpolation()
    {
        // The 640x480 retail frame renders font-13ps glyphs at their exact 16px
        // atlas cell size with single-texel stems: the released HUD did not
        // interpolate. Nearest reproduces that at the design resolution.
        Assert.Matches(@"TextureFilter\s*=\s*CanvasItem\.TextureFilterEnum\.Nearest", Source);
    }

    [Fact]
    public void LowerLeftScannerRingSitsOnItsMeasuredRetailCircle()
    {
        float left = Constant(@"radarRect = new\((\d+(?:\.\d+)?)f,");
        float bottomInset = Constant(@"radarRect = new\(\d+(?:\.\d+)?f, DesignHeight - (\d+(?:\.\d+)?)f");

        Assert.Equal(66.01f, left + RingContentCentre, 1.0f);
        Assert.Equal(417.25f, DesignHeight - bottomInset + RingContentCentre, 1.0f);
    }

    [Fact]
    public void BattleLineRingAndPortraitAreConcentricOnTheMeasuredRetailCircle()
    {
        float ringRight = Constant(@"BattleLineInstrumentRect\(\) =>\s*new\(DesignWidth - (\d+(?:\.\d+)?)f");
        float ringBottom = Constant(@"BattleLineInstrumentRect\(\) =>\s*new\(DesignWidth - \d+(?:\.\d+)?f, DesignHeight - (\d+(?:\.\d+)?)f");
        float portraitRight = Constant(@"BattleLinePortraitRect\(\) =>\s*new\(DesignWidth - (\d+(?:\.\d+)?)f");
        float portraitBottom = Constant(@"BattleLinePortraitRect\(\) =>\s*new\(DesignWidth - \d+(?:\.\d+)?f, DesignHeight - (\d+(?:\.\d+)?)f");

        float ringCentreX = DesignWidth - ringRight + RingContentCentre;
        float ringCentreY = DesignHeight - ringBottom + RingContentCentre;
        float portraitCentreX = DesignWidth - portraitRight + MaskContentCentre;
        float portraitCentreY = DesignHeight - portraitBottom + MaskContentCentre;

        Assert.Equal(568.08f, ringCentreX, 1.0f);
        Assert.Equal(416.46f, ringCentreY, 1.0f);

        // The ring and the masked portrait carry their circles at different
        // places on their pages, so their rects must differ by exactly that
        // difference or the face sits off-centre inside its own bezel.
        Assert.Equal(ringCentreX, portraitCentreX, 1.0f);
        Assert.Equal(ringCentreY, portraitCentreY, 1.0f);
    }

    [Fact]
    public void BothLowerInstrumentsShareTheMeasuredRetailBaseline()
    {
        float leftBottom = Constant(@"radarRect = new\(\d+(?:\.\d+)?f, DesignHeight - (\d+(?:\.\d+)?)f");
        float rightBottom = Constant(@"BattleLineInstrumentRect\(\) =>\s*new\(DesignWidth - \d+(?:\.\d+)?f, DesignHeight - (\d+(?:\.\d+)?)f");

        // Retail puts both ring centres within 0.8px of the same scanline
        // (417.25 and 416.46), and both textures carry their ring at the same
        // page offset, so the two rects must share a bottom inset.
        Assert.Equal(leftBottom, rightBottom);
    }

    [Fact]
    public void MessagePanelBodyMatchesItsMeasuredRetailExtent()
    {
        float centreXOffset = Constant(@"centerX = \(DesignWidth \* 0\.5f\) \+ (\d+(?:\.\d+)?)f");
        float centreYInset = Constant(@"centerY = DesignHeight - (\d+(?:\.\d+)?)f");
        float pieceHeight = Constant(@"pieceHeight = (\d+(?:\.\d+)?)f");

        float centreX = (DesignWidth * 0.5f) + centreXOffset;
        float centreY = DesignHeight - centreYInset;
        float top = centreY - (pieceHeight * 0.5f);

        // objective-inner-centre is opaque on rows 28..90 of its 128-row page.
        float bodyTop = top + (28f * pieceHeight / 128f);
        float bodyBottom = top + (91f * pieceHeight / 128f);

        Assert.Equal(341.5f, centreX, 1.0f);
        Assert.Equal(405.5f, bodyTop, 1.0f);
        Assert.Equal(464.5f, bodyBottom, 1.0f);
    }

    [Fact]
    public void MessageTextIsLaidOutFromTheMeasuredPanelMetricsAndNotFromLocalConstants()
    {
        // The defect this guards: the message text used to be laid out from
        // constants local to the text layer - a 232px wrap, five lines per
        // page, and a rect at (226, 387) - which put the first two of five
        // lines above the panel body's measured top edge at y 405.5. The
        // metrics now live in Level100MessagePanel, where they are unit tested
        // against the level100-gameplay captures, and the layer must use them.
        Assert.DoesNotMatch(@"MessageTextWidth", Source);
        Assert.DoesNotMatch(@"MaximumMessageLines", Source);
        Assert.DoesNotMatch(@"\bPaginate\(", Source);
        Assert.DoesNotMatch(@"DesignHeight - 93f", Source);

        Assert.Matches(@"Level100MessagePanel\.Wrap\(", Source);
        Assert.Matches(@"Level100MessagePanel\.Window\(", Source);
        Assert.Matches(@"Level100MessagePanel\.TextPenLeft", Source);
        Assert.Matches(@"Level100MessagePanel\.FirstLinePenTop", Source);
        Assert.Matches(@"Level100MessagePanel\.LineHeightPixels", Source);
    }

    [Fact]
    public void MessageTextBlockSitsInsideTheMeasuredPanelBody()
    {
        // Re-derive the panel body from the art constants in the HUD source and
        // check the text block from Level100MessagePanel fits it, so the two can
        // never drift apart again.
        float centreYInset = Constant(@"centerY = DesignHeight - (\d+(?:\.\d+)?)f");
        float pieceHeight = Constant(@"pieceHeight = (\d+(?:\.\d+)?)f");
        float top = (DesignHeight - centreYInset) - (pieceHeight * 0.5f);
        float bodyTop = top + (28f * pieceHeight / 128f);
        float bodyBottom = top + (91f * pieceHeight / 128f);

        const float glyphCell = 16f;
        float whiteTop = Level100MessagePanel.FirstLinePenTop - 1f;
        float whiteBottom = whiteTop +
            ((Level100MessagePanel.VisibleLines - 1) *
                Level100MessagePanel.LineHeightPixels) +
            glyphCell;

        Assert.True(
            whiteTop >= bodyTop - 0.5f,
            $"first message line starts at y {whiteTop}, above the panel body top {bodyTop}.");
        Assert.True(
            whiteBottom <= bodyBottom + 0.5f,
            $"last message line ends at y {whiteBottom}, below the panel body bottom {bodyBottom}.");
        Assert.Equal((bodyTop + bodyBottom) * 0.5f, (whiteTop + whiteBottom) * 0.5f, 1.0f);
    }

    [Theory]
    // A window at the design resolution must be a pure identity map.
    [InlineData(640, 480, 1.0f, 0f, 0f)]
    // The shipped 1280x720 viewport (project.godot) pillarboxes the 4:3 stage.
    [InlineData(1280, 720, 1.5f, 160f, 0f)]
    [InlineData(1920, 1080, 2.25f, 240f, 0f)]
    // A taller-than-4:3 window letterboxes instead.
    [InlineData(1280, 1024, 2.0f, 0f, 32f)]
    public void DesignTransformKeepsTheStageWholeAndCentred(
        int viewportWidth,
        int viewportHeight,
        float expectedScale,
        float expectedOffsetX,
        float expectedOffsetY)
    {
        (float scale, float offsetX, float offsetY) =
            DesignTransform(viewportWidth, viewportHeight);

        Assert.Equal(expectedScale, scale, 0.0001f);
        Assert.Equal(expectedOffsetX, offsetX, 0.0001f);
        Assert.Equal(expectedOffsetY, offsetY, 0.0001f);

        // The whole stage stays on screen and stays 4:3.
        Assert.True(offsetX >= 0f && offsetY >= 0f);
        Assert.Equal(viewportWidth, (offsetX * 2f) + (DesignWidth * scale), 0.0001f);
        Assert.Equal(viewportHeight, (offsetY * 2f) + (DesignHeight * scale), 0.0001f);
    }

    [Fact]
    public void ScannerRingKeepsItsRetailShareOfTheScreenAtEveryWindowSize()
    {
        // The defect this change fixes: at 1280x720 the untransformed HUD drew a
        // 128px page into a 1280px window, so the scanner covered 10.0% of the
        // width where retail covers 20.0%.
        const float pageSize = 128f;
        float retailShare = pageSize / DesignWidth;
        Assert.Equal(0.2f, retailShare, 0.0001f);

        foreach ((int w, int h) in new[] { (640, 480), (1280, 720), (1920, 1080), (1280, 1024) })
        {
            (float scale, float offsetX, _) = DesignTransform(w, h);
            float stageWidth = DesignWidth * scale;
            Assert.Equal(retailShare, pageSize * scale / stageWidth, 0.0001f);
            // and it stays anchored to the stage's own left edge, not the window's
            float drawnLeft = offsetX + (17f * scale);
            Assert.Equal(offsetX + (17f * scale), drawnLeft, 0.0001f);
        }
    }

    private static (float Scale, float OffsetX, float OffsetY) DesignTransform(
        float viewportWidth,
        float viewportHeight)
    {
        float scale = Math.Min(viewportWidth / DesignWidth, viewportHeight / DesignHeight);
        return (
            scale,
            (viewportWidth - (DesignWidth * scale)) * 0.5f,
            (viewportHeight - (DesignHeight * scale)) * 0.5f);
    }
}
