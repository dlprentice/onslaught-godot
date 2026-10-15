// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The released tactical-scanner projection, asserted as a LAW rather than as a
/// pixel count. The law is recovered from the shipped bytes -
/// <c>CHud__RenderTacticalRadarContacts</c> @ <c>0x00484c50</c> and the .rdata
/// floats it reads - because the HUD is absent from the pinned GPL drop.
/// </summary>
public sealed class Level100ScannerProjectionTests
{
    /// <summary>
    /// The released Level 100 start yaw. Established by controlled copied-retail
    /// observation and recorded in rebuild/PROVENANCE.md; Core reproduces it as
    /// the tick-zero facing.
    /// </summary>
    private const float StartYawRadians = 0.509829998f;

    [Fact]
    public void ConstantsAreTheDecodedRetailFloats()
    {
        // .rdata of the verified safe copy, image base 0x00400000,
        // .rdata VA 0x005d8000 -> file offset 0x1d8000.
        Assert.Equal(40f, Level100ScannerProjection.ScaleNumerator);      // 0x005d8610
        Assert.Equal(96f, Level100ScannerProjection.ScaleDenominator);    // PUSH 0x42c00000 @ 0x00487bab
        Assert.Equal(46f, Level100ScannerProjection.ClampRadiusPixels);   // 0x005dbe6c
        Assert.Equal(8_464f, Level100ScannerProjection.CullRadiusSquaredPixels); // 0x005dbe70
        Assert.Equal(255f, Level100ScannerProjection.AlphaQuantiser);     // 0x005d8c70
        Assert.Equal(68f, Level100ScannerProjection.CentreX);             // 0x005dbb70 (69) - 1
        Assert.Equal(417f, Level100ScannerProjection.CentreY);            // 480 - 44 - 20 + 1

        // The three radius constants are mutually consistent, and that is the
        // check that they were read correctly rather than transcribed: the fade
        // 1 - (r - 46) * 0.021739131 reaches zero at exactly r = 92, and the
        // cull radius is exactly 92^2.
        Assert.Equal(
            1f / Level100ScannerProjection.ClampRadiusPixels,
            Level100ScannerProjection.FadePerPixel,
            6);
        Assert.Equal(
            2f * Level100ScannerProjection.ClampRadiusPixels,
            MathF.Sqrt(Level100ScannerProjection.CullRadiusSquaredPixels),
            4);
    }

    /// <summary>
    /// A contact on the player's forward axis lands straight up from the
    /// scanner centre, at the world-to-pixel scale, for EVERY facing. This is
    /// the rotation half of the law: the mapping is a rotation by minus the
    /// player yaw, so turning the player must sweep the contact around the
    /// scanner and never displace a dead-ahead contact off the vertical.
    /// </summary>
    [Fact]
    public void ForwardContactIsDirectlyAboveCentreUnderEveryFacing()
    {
        const float distanceWorldUnits = 60f;
        for (int step = 0; step < 72; step++)
        {
            float yaw = step * (MathF.Tau / 72f);
            // Core's horizontal axes: at yaw 0 the player faces +Z, and +X is
            // to the player's right. Forward is therefore (-sin, cos).
            float deltaX = -MathF.Sin(yaw) * distanceWorldUnits;
            float deltaZ = MathF.Cos(yaw) * distanceWorldUnits;

            Level100ScannerPlacement placement =
                Level100ScannerProjection.Place(deltaX, deltaZ, yaw);

            Assert.True(placement.Drawn);
            Assert.False(placement.Clamped);
            Assert.Equal(255, placement.Alpha);
            Assert.Equal(0f, placement.OffsetX, 3);
            Assert.Equal(
                -distanceWorldUnits * Level100ScannerProjection.PixelsPerWorldUnit,
                placement.OffsetY,
                3);
        }
    }

    /// <summary>The starboard half of the same law.</summary>
    [Fact]
    public void StarboardContactIsDirectlyRightOfCentreUnderEveryFacing()
    {
        const float distanceWorldUnits = 24f;
        for (int step = 0; step < 72; step++)
        {
            float yaw = step * (MathF.Tau / 72f);
            float deltaX = MathF.Cos(yaw) * distanceWorldUnits;
            float deltaZ = MathF.Sin(yaw) * distanceWorldUnits;

            Level100ScannerPlacement placement =
                Level100ScannerProjection.Place(deltaX, deltaZ, yaw);

            Assert.Equal(
                distanceWorldUnits * Level100ScannerProjection.PixelsPerWorldUnit,
                placement.OffsetX,
                3);
            Assert.Equal(0f, placement.OffsetY, 3);
        }
    }

    [Fact]
    public void ScaleIsFortyOverNinetySixPixelsPerWorldUnit()
    {
        // 46 px of clamp radius is 110.4 released world units.
        Level100ScannerPlacement inside =
            Level100ScannerProjection.Place(0f, 110.3f, 0f);
        Level100ScannerPlacement outside =
            Level100ScannerProjection.Place(0f, 110.5f, 0f);

        Assert.False(inside.Clamped);
        Assert.True(outside.Clamped);
        Assert.Equal(
            110.3f * 40f / 96f,
            -inside.OffsetY,
            3);
    }

    [Fact]
    public void ContactsPastTheRimArePinnedToItAndKeepTheirBearing()
    {
        Level100ScannerPlacement near =
            Level100ScannerProjection.Place(30f, 40f, 0.3f);
        Level100ScannerPlacement far =
            Level100ScannerProjection.Place(90f, 120f, 0.3f);

        Assert.False(near.Clamped);
        Assert.True(far.Clamped);
        Assert.Equal(
            Level100ScannerProjection.ClampRadiusPixels,
            MathF.Sqrt((far.OffsetX * far.OffsetX) + (far.OffsetY * far.OffsetY)),
            3);
        // Same bearing, three times the range: the rim position is identical.
        Assert.Equal(
            MathF.Atan2(near.OffsetY, near.OffsetX),
            MathF.Atan2(far.OffsetY, far.OffsetX),
            4);
    }

    [Fact]
    public void AlphaFadesLinearlyFromTheRimToZeroAtTwiceTheRim()
    {
        Assert.Equal(255, Level100ScannerProjection.Place(0f, 110.4f, 0f).Alpha);

        // r = 69 px is halfway from the rim to the cull radius.
        Level100ScannerPlacement half =
            Level100ScannerProjection.Place(0f, 69f / (40f / 96f), 0f);
        Assert.True(half.Clamped);
        Assert.Equal(128, half.Alpha);

        // r just inside 92 px fades to nothing; r at or past 92 px is culled
        // outright by the r^2 < 8464 test.
        Assert.True(Level100ScannerProjection.Place(0f, 91.99f / (40f / 96f), 0f).Alpha <= 1);
        Assert.False(Level100ScannerProjection.Place(0f, 92.01f / (40f / 96f), 0f).Drawn);
    }

    [Fact]
    public void AContactStandingOnThePlayerIsUnclampedAndOpaque()
    {
        Level100ScannerPlacement placement =
            Level100ScannerProjection.Place(0f, 0f, 1.2f);

        Assert.True(placement.Drawn);
        Assert.False(placement.Clamped);
        Assert.Equal(255, placement.Alpha);
        Assert.Equal(0f, placement.OffsetX);
        Assert.Equal(0f, placement.OffsetY);
    }

    /// <summary>
    /// The law measured against RETAIL'S OWN FRAME, which is what makes the
    /// rest of this file more than internal consistency.
    ///
    /// <para>Retail frame
    /// <c>local-lab/retail-reference-pristine/level100-gameplay/hud-timeline-run1/level100-t025065ms.png</c>
    /// carries eight-connected components of the decoded friendly tint
    /// <c>0x5050AF</c> = (80, 80, 174) inside the scanner. Their centroids are
    /// the right-hand column below and were measured off that PNG, not
    /// predicted. The left-hand column is this law applied to the hash-pinned
    /// manifest's authored pose for the same object, from the released Level
    /// 100 start pose. Agreement is inside half a pixel on all seven.</para>
    ///
    /// <para>This simultaneously validates the scale (40/96), the rotation
    /// convention, the scanner centre (68, 417), and the sprite anchor being
    /// the quad CENTRE rather than its corner - a corner anchor would put every
    /// one of these eight pixels off.</para>
    /// </summary>
    [Theory]
    [InlineData("wres:bswd:0000", 64.3f, 412.2f)] // Control Tower
    [InlineData("wres:bswd:0001", 76.1f, 411.0f)] // Tank Factory
    [InlineData("wres:bswd:0002", 49.0f, 401.1f)] // Health Pad
    [InlineData("wres:bswd:0013", 54.5f, 409.7f)] // Forseti Research Building 1
    [InlineData("wres:bswd:0020", 25.9f, 404.7f)] // Radar Station
    [InlineData("wres:bswd:0023", 88.1f, 421.1f)] // Airfield
    [InlineData("wres:bswd:0025", 86.0f, 407.1f)] // Hangar
    public void PredictsRetailsMeasuredBlobCentroids(
        string definitionIdentity,
        float measuredX,
        float measuredY)
    {
        Level100ActorDefinition definition = LoadActorDefinitions().Actors
            .Single(actor => StringComparer.Ordinal.Equals(
                actor.DefinitionIdentity,
                definitionIdentity));

        // Core's world origin IS the released player start, so the authored
        // initial pose is already the player-relative offset at tick zero.
        SimVector3 position = definition.InitialPose.PositionMillimeters;
        Level100ScannerPlacement placement =
            Level100ScannerProjection.PlaceInDesignSpace(
                position.X / 1_000f,
                position.Z / 1_000f,
                StartYawRadians);

        Assert.True(placement.Drawn);
        Assert.Equal(measuredX, placement.OffsetX, 0.5f);
        Assert.Equal(measuredY, placement.OffsetY, 0.5f);
    }

    /// <summary>
    /// The colour partition is the released allegiance numbering joined to the
    /// three decoded packed tints. <c>0x5050AF</c> is confirmed at the pixel
    /// level on the retail frame.
    /// </summary>
    [Fact]
    public void TintsAreTheDecodedPackedConstants()
    {
        Assert.Equal(0x5050AF, Level100ScannerProjection.TintRgb(Level100HudAllegiance.Friendly));
        Assert.Equal(0xAF0808, Level100ScannerProjection.TintRgb(Level100HudAllegiance.Enemy));
        Assert.Equal(0x606060, Level100ScannerProjection.TintRgb(Level100HudAllegiance.Neutral));

        // Retail brightens by flag 0x400 at unit+0x34; the sums are the second
        // literal in each draw loop.
        Assert.Equal(
            0x9090FF,
            Level100ScannerProjection.FriendlyTintRgb +
            Level100ScannerProjection.FriendlyHighlightAdd);
        Assert.Equal(
            0xFF5050,
            Level100ScannerProjection.EnemyTintRgb +
            Level100ScannerProjection.EnemyHighlightAdd);
        Assert.Equal(
            0x707070,
            Level100ScannerProjection.NeutralTintRgb +
            Level100ScannerProjection.NeutralHighlightAdd);
    }

    /// <summary>
    /// The authored allegiance the scanner colours by is decoded from the same
    /// hash-pinned manifest, and it is the released WRES field: the Federation
    /// facilities are FRIENDLY_ALLIGENCE (0) and the scenery is
    /// NEUTRAL_ALLEGIANCE (2).
    /// </summary>
    [Fact]
    public void AuthoredAllegianceIsDecodedForAllThirtyThreeBaseWorldObjects()
    {
        IReadOnlyDictionary<string, int> allegiance =
            Level100ActorDefinitionManifest.DecodeAuthoredAllegiance(
                File.ReadAllBytes(ManifestPath));

        Assert.Equal(33, allegiance.Count);
        Assert.Equal((int)Level100HudAllegiance.Friendly, allegiance["wres:bswd:0000"]); // Control Tower
        Assert.Equal((int)Level100HudAllegiance.Friendly, allegiance["wres:bswd:0020"]); // Radar Station
        Assert.Equal((int)Level100HudAllegiance.Friendly, allegiance["wres:bswd:0025"]); // Hangar
        Assert.Equal((int)Level100HudAllegiance.Neutral, allegiance["wres:bswd:0004"]);  // Iceberg 1
        Assert.Equal((int)Level100HudAllegiance.Neutral, allegiance["wres:bswd:0024"]);  // Forseti Docks
        Assert.Equal(11, allegiance.Values.Count(value => value == 0));
        Assert.Equal(22, allegiance.Values.Count(value => value == 2));
    }

    /// <summary>
    /// The objective pass differs from the three allegiance passes in exactly
    /// two ways, and both are absences in the instruction stream rather than
    /// extra constants: between the rotation at <c>0x004853c1</c> and the draw
    /// at <c>0x00485462</c> there is no <c>fcom [0x005dbe70]</c> (no cull) and
    /// no <c>fmul [0x005d8c70]</c> (no fade). The clamp is byte-identical.
    /// </summary>
    [Fact]
    public void AnObjectiveIsNeverCulledAndNeverFades()
    {
        // Twice the cull radius in world units: an allegiance contact here is
        // gone, an objective is pinned to the rim at full alpha.
        const float farAway = 2f * 92f / (40f / 96f);

        Assert.False(Level100ScannerProjection.Place(0f, farAway, 0f).Drawn);

        Level100ScannerPlacement objective =
            Level100ScannerProjection.PlaceObjective(0f, farAway, 0f);

        Assert.True(objective.Drawn);
        Assert.True(objective.Clamped);
        Assert.Equal(255, objective.Alpha);
        Assert.Equal(
            Level100ScannerProjection.ClampRadiusPixels,
            MathF.Sqrt((objective.OffsetX * objective.OffsetX) +
                (objective.OffsetY * objective.OffsetY)),
            3);
    }

    /// <summary>
    /// Inside the rim the objective placement is the SAME projection as a
    /// contact's - the two paths share the rotation and the scale, so an
    /// objective marker must land exactly on the blob of the unit it marks.
    /// </summary>
    [Fact]
    public void AnObjectiveInsideTheRimLandsExactlyOnItsContact()
    {
        foreach (float yaw in new[] { 0f, 0.509829998f, 2.7f, -1.3f })
        {
            Level100ScannerPlacement contact =
                Level100ScannerProjection.Place(37f, -52f, yaw);
            Level100ScannerPlacement objective =
                Level100ScannerProjection.PlaceObjective(37f, -52f, yaw);

            Assert.False(contact.Clamped);
            Assert.False(objective.Clamped);
            Assert.Equal(contact.OffsetX, objective.OffsetX, 5);
            Assert.Equal(contact.OffsetY, objective.OffsetY, 5);
        }
    }

    /// <summary>
    /// <c>PUSH 0xFFFFFF00</c> at <c>0x00485424</c>. Confirmed at the pixel
    /// level, not merely decoded: retail
    /// <c>hud-timeline-run1/level100-t041063ms.png</c> carries 13 pixels of
    /// <c>(254, 254, 0)</c> inside the scanner disc, in the same 5x5 diamond
    /// footprint as every other blob on that frame.
    /// </summary>
    [Fact]
    public void TheObjectiveTintIsTheHardYellowImmediate()
    {
        Assert.Equal(0xFFFF00, Level100ScannerProjection.ObjectiveTintRgb);
    }

    private static string ManifestPath => Path.Combine(
        AppContext.BaseDirectory,
        "Assets",
        "Level100",
        "StaticWorld",
        "level100-static-world.json");

    private static Level100ActorDefinitionSet LoadActorDefinitions() =>
        Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(ManifestPath));
}
