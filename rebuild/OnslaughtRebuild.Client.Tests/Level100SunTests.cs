// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The in-level sun flare, task #148: retail draws a hard-spiked starburst over
/// the sky cube's painted sun disc and the reconstruction drew nothing.
///
/// <para>The flare is not missing content. <c>data/ParticleSets/MainSet.par</c>
/// ships a <c>Sun Sprite</c> descriptor and <c>Particle%sun3.tga</c> is the
/// starburst itself; the released engine looks the descriptor up by name at
/// <c>references/Onslaught/DXEngine.cpp:220</c>. These tests pin what the
/// shipped record says and what the one unrecoverable value was measured to
/// be, so the drawn effect stays auditable against the authored data.</para>
/// </summary>
public sealed class Level100SunTests
{
    private const string MainSetRelativePath =
        "rebuild/OnslaughtRebuild.Godot/Assets/Level100/ParticleSets/MainSet.par";

    /// <summary><c>SUN_SCALE</c>, <c>references/Onslaught/DXEngine.cpp:975</c>.</summary>
    private const float RetailSunScale = 0.6f;

    /// <summary>
    /// <c>RetailTanVerticalHalfFov</c>, the released projection's
    /// <c>tan(vfov/2)</c>, and the 480-row frame the retail captures are taken
    /// at. Together they give a vertical focal length of 320 px.
    /// </summary>
    private const double RetailTanVerticalHalfFov = 0.75;

    private const double RetailFrameHeightPixels = 480;

    /// <summary>
    /// <b>MEASURED</b> from retail frame
    /// <c><maintainer evidence>\bea-inlevel\t25b-20260727-192619\20260727-192620-57904\f005205.png</c>
    /// against the paired reconstruction frame
    /// a private capture folder.
    ///
    /// <para>The two frames align at zero offset - mean absolute luminance
    /// difference 0.31/255 over a 60x180 px patch of open sky - so the
    /// difference image IS the flare, with no registration to fit. The flare's
    /// centre sits at (509, 58), which for the released projection puts it
    /// <c>cos(theta) = 0.77329</c> off the view axis; its depth is therefore
    /// this fraction of the 0.6 m the placement law puts it at.</para>
    /// </summary>
    private const double MeasuredFlareDepthFraction = 0.77329;

    /// <summary>
    /// <b>MEASURED</b>, same pair. Fitting the shipped <c>sun3.tga</c> over the
    /// reconstruction frame and scoring against retail gives a flat error
    /// minimum between these two on-screen half-extents (mean absolute channel
    /// error 3.76 across the band, against 6.98 for drawing no flare at all).
    /// </summary>
    private const double MeasuredFlareHalfExtentLowPixels = 65;

    private const double MeasuredFlareHalfExtentHighPixels = 69;

    /// <summary>
    /// The shipped record, field by field. Every value here is read out of the
    /// file at test time; the assertions are what the reconstruction is allowed
    /// to rely on.
    /// </summary>
    [Fact]
    public void TheSunFlareIsOneShippedSpriteWithNothingLeftUnresolved()
    {
        ParticleSetFile set = ParticleSetFile.Parse(File.ReadAllBytes(Locate(MainSetRelativePath)));
        ParticleEffectPlan plan = ParticleEffectResolver.Resolve(set, "Sun Sprite");

        // A bare type-1 sprite: no emitter, no timeline, no mesh, no mover, no
        // param-function modifier. If any of that appears the resolver reports
        // it here, and the renderer refuses to draw rather than approximating.
        Assert.Equal(ParticleDescriptorType.Sprite, plan.RootType);
        Assert.Empty(plan.Unimplemented);
        ParticleSpriteLayer sun = Assert.Single(plan.Layers);
        Assert.Equal(1, sun.InstanceCount);
        Assert.Equal(1, plan.TotalInstances);

        // The starburst art, and the blend that selects which shipped copy of it
        // loads. sun3.tga ships ONLY as (0)R5G6B5, so mode 0 is unambiguous.
        Assert.Equal("sun3.tga", sun.TextureName);
        Assert.Equal(0, sun.BlendMode);

        // Texture_Number -1: the whole texture, not a cell of the 4x4 grid its
        // Texture_Size would otherwise select.
        Assert.Equal(-1, sun.StartCell);
        Assert.Equal(4, sun.AtlasColumns);
        Assert.Equal(4, sun.AtlasRows);
        Assert.Equal(ParticleAnimationMode.Static, sun.AnimationMode);
        Assert.False(sun.RandomStartCell);

        // Life 0 - one turn. The engine re-adds the particle every rendered
        // frame (DXEngine.cpp:1064), so the flare is a per-frame redraw and no
        // instance ever ages. That is why Final_Radius and Life_Pct never
        // matter, and why the radius-over-life law - absent from the source
        // drop with the rest of the particle system - is not needed here.
        Assert.Equal(0, sun.LifeTurns);
        Assert.Equal(0.1f, sun.StartRadius);
        Assert.Equal(1f, sun.FinalRadius);
        Assert.Equal(1f, sun.LifeFraction);
        Assert.False(sun.FadeColour);
        Assert.False(sun.Gravity);

        // No emitter, so no shape and no velocity: the sprite is placed by the
        // engine, not scattered.
        Assert.Null(sun.Shape);
        Assert.Equal((0f, 0f, 0f), sun.InitialVelocity);
        Assert.Equal(0f, sun.OutwardVelocity);
    }

    /// <summary>
    /// The authored colour, and why the reconstruction may treat it as a
    /// constant.
    ///
    /// <para>How a <c>Colour_Range</c> interpolates over a particle's life is
    /// NOT recovered. It does not have to be for this effect: <c>Sun Colour</c>
    /// authors start and end equal and no transition, so every interpolation law
    /// agrees on one value for the sprite's whole life. This test is what makes
    /// that argument checkable - if the file ever stops making the tie, the flat
    /// modulation stops being justified and this fails.</para>
    /// </summary>
    [Fact]
    public void TheSunColourRangeIsConstantSoItsInterpolationLawIsNotNeeded()
    {
        ParticleSetFile set = ParticleSetFile.Parse(File.ReadAllBytes(Locate(MainSetRelativePath)));
        ParticleSpriteLayer sun = Assert.Single(
            ParticleEffectResolver.Resolve(set, "Sun Sprite").Layers);

        Assert.NotNull(sun.ColourRange);
        ParticleColourRange range = sun.ColourRange.Value;
        Assert.Equal("Sun Colour", range.Name);

        // 0.501961 is exactly 128/255 on every channel - a neutral half-scale.
        Assert.Equal(128f / 255f, range.Start.R, 5);
        Assert.Equal(128f / 255f, range.Start.G, 5);
        Assert.Equal(128f / 255f, range.Start.B, 5);

        // The tie that makes the interpolation law irrelevant.
        Assert.False(range.UseTransition);
        Assert.Equal(range.Start, range.End);

        // And the record does author a transition colour (1, 0.501961, 0) that
        // Use_Transition 0 makes dead. Pinned so nobody revives it as "the
        // authored orange".
        Assert.Equal(1f, range.Transition.R, 5);
        Assert.Equal(128f / 255f, range.Transition.G, 5);
        Assert.Equal(0f, range.Transition.B, 5);
    }

    /// <summary>
    /// <b>The one value neither the file nor the source carries: how an authored
    /// <c>Radius</c> becomes a billboard extent.</b>
    ///
    /// <para>The particle system is absent from the GPL drop, so the convention
    /// cannot be ported. It can be measured, and it was. Placement is fully
    /// determined - <c>camera + SunPos * 0.6</c>,
    /// <c>references/Onslaught/DXEngine.cpp:1043-1064</c>, over a Level 100
    /// <c>SunPos</c> that is unit length - so the flare's distance from the eye
    /// is known, its on-screen size was measured off a retail frame, and the
    /// world extent follows by arithmetic.</para>
    ///
    /// <para>The reading that survives is <b>Radius is the half-extent</b>: a
    /// 0.2 m quad. The competing reading, Radius as the full extent, predicts
    /// half the size and is refuted below - at 34.5 px its fit error is 6.10
    /// against 6.98 for drawing nothing at all, i.e. it barely improves on an
    /// absent flare.</para>
    /// </summary>
    [Fact]
    public void TheAuthoredRadiusIsTheBillboardHalfExtent()
    {
        ParticleSetFile set = ParticleSetFile.Parse(File.ReadAllBytes(Locate(MainSetRelativePath)));
        ParticleSpriteLayer sun = Assert.Single(
            ParticleEffectResolver.Resolve(set, "Sun Sprite").Layers);
        Level100Terrain terrain = Level100Terrain.Instance;

        // The placement law scales the raw authored vector, so its magnitude is
        // load-bearing. Level 100's is unit length.
        double sunVectorLength = Math.Sqrt(
            ((double)terrain.SunPositionX * terrain.SunPositionX) +
            ((double)terrain.SunPositionY * terrain.SunPositionY) +
            ((double)terrain.SunPositionZ * terrain.SunPositionZ));
        Assert.Equal(1.0, sunVectorLength, 6);

        double distanceFromEye = sunVectorLength * RetailSunScale;
        Assert.Equal(0.6, distanceFromEye, 6);

        // The flare is off the view axis, so what governs its projected size is
        // its DEPTH, not its radial distance.
        double depth = distanceFromEye * MeasuredFlareDepthFraction;
        double focalPixels = (RetailFrameHeightPixels / 2) / RetailTanVerticalHalfFov;
        Assert.Equal(320.0, focalPixels, 6);

        double halfExtentAsHalf = sun.StartRadius / depth * focalPixels;
        double halfExtentAsFull = (sun.StartRadius / 2) / depth * focalPixels;

        Assert.InRange(
            halfExtentAsHalf,
            MeasuredFlareHalfExtentLowPixels,
            MeasuredFlareHalfExtentHighPixels);
        Assert.False(
            halfExtentAsFull >= MeasuredFlareHalfExtentLowPixels,
            $"Radius-as-full-extent predicts {halfExtentAsFull:F1} px, which the " +
            "retail measurement was supposed to refute.");

        // Reading the sprite at Final_Radius instead - which is what "Life 0
        // means the particle is already at the end of its life" would give -
        // makes the flare taller than the whole frame. Also refuted.
        double halfExtentAtFinalRadius = sun.FinalRadius / depth * focalPixels;
        Assert.True(
            halfExtentAtFinalRadius > RetailFrameHeightPixels,
            $"Final_Radius predicts a {halfExtentAtFinalRadius:F0} px half-extent, which " +
            "was supposed to overflow the 480-row frame and be obviously wrong.");
    }

    /// <summary>
    /// Always-on backstop for the Godot side, which no test here can instantiate.
    /// This asserts on source text and is NOT evidence that the drawn frame is
    /// right - only a capture is. It exists so the decoded values cannot be
    /// quietly replaced by hand-authored ones on a machine with no capture,
    /// which is the specific failure mode task #148 warns about.
    /// </summary>
    [Fact]
    public void TheSunAssetTakesItsNumbersFromTheDecodedDescriptor()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "godot-sun-source",
            "Level100SunAsset.cs");
        Assert.True(File.Exists(path), $"Sun source was not copied to the test output: {path}");
        string source = string.Join(
            '\n',
            File.ReadLines(path)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Where(line => !line.TrimStart().StartsWith("///", StringComparison.Ordinal)));

        // The effect is resolved by name out of the shipped set, not rebuilt.
        Assert.Contains("ParticleSetFile.Parse", source, StringComparison.Ordinal);
        Assert.Contains("ParticleEffectResolver.Resolve", source, StringComparison.Ordinal);
        Assert.Contains("\"Sun Sprite\"", source, StringComparison.Ordinal);

        // Size, colour and placement all read from decoded values. The quad side
        // must come from the one owner of the half-extent law, not from a
        // hand-doubled literal - see ParticleQuadSizeConventionTests.
        Assert.Contains(
            "ParticleEffectResolver.BillboardQuadSide(layer.StartRadius)",
            source,
            StringComparison.Ordinal);
        Assert.Contains("range.Start.R, range.Start.G, range.Start.B", source, StringComparison.Ordinal);
        Assert.Contains("terrain.SunPosition", source, StringComparison.Ordinal);
        Assert.Contains("RetailSunScale = 0.6f", source, StringComparison.Ordinal);

        // Blend_Mode 0 is additive; an alpha-blended sun would be a different
        // claim about the shipped data.
        Assert.Contains("BlendModeEnum.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BlendModeEnum.Mix", source, StringComparison.Ordinal);

        // The plan must be rejected, not trimmed, if it stops being one clean
        // sprite.
        Assert.Contains("plan.Unimplemented.Count != 0", source, StringComparison.Ordinal);
    }

    private static string Locate(string repositoryRelativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate '{repositoryRelativePath}'. Run 'npm run prepare:rebuild-assets'.");
    }
}
