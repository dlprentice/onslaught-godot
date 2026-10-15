// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The check that did not exist, and whose absence let a corrected retail table
/// and a stale test fixture disagree silently.
/// </summary>
/// <remarks>
/// <para>
/// <c>rebuild/TestSupport/Level100TestActorDefinitions.cs</c> transcribes the
/// released waypoint routes by hand so that Core-only tests need no engine and
/// no asset load. Nothing compared that transcription to the manifest the
/// product actually consumes, so it drifted twice:
/// </para>
/// <list type="number">
///   <item><c>Target Tank Path 1</c> was a synthetic triple 610 m out at
///   exactly 45 degrees until 2026-07-26, which made tutorial beat 3
///   unreachable in Core;</item>
///   <item>every route carried the 121-entry navigation graph's coordinates
///   instead of the 30 RLWD thingType-18 marker records' until
///   <c>58d9ce57</c> corrected the materializer — 30 nodes aliased onto 11
///   positions — and the fixture kept the aliased values for a further
///   commit, with a test asserting one of the aliases as retail truth.</item>
/// </list>
/// <para>
/// Both were found by reading, not by a gate. This is the gate. It is
/// deliberately an exact comparison of every field the manifest carries,
/// including <c>NodeIndex</c> and <c>RetailComponentsFloatBits</c>, because
/// both are part of the hashed definition identity even though nothing in the
/// movement path reads them yet.
/// </para>
/// </remarks>
public sealed class Level100WaypointFixtureTests
{
    private static Level100ActorDefinitionSet Manifest()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Level100",
            "StaticWorld",
            "level100-static-world.json");
        Assert.True(
            File.Exists(path),
            $"The materialized Level 100 manifest is missing at '{path}'. " +
            "Run `npm run prepare:rebuild-assets`.");
        return Level100ActorDefinitionManifest.Decode(File.ReadAllBytes(path));
    }

    [Fact]
    public void TestFixtureWaypointPaths_AreTheManifestsWaypointPaths()
    {
        Level100ActorDefinitionSet manifest = Manifest();
        Level100ActorDefinitionSet fixture = Level100TestActorDefinitions.Create();

        Assert.Equal(
            manifest.WaypointPaths.Select(path => path.Name),
            fixture.WaypointPaths.Select(path => path.Name));

        foreach (Level100WaypointPathDefinition expected in manifest.WaypointPaths)
        {
            Level100WaypointPathDefinition actual =
                fixture.GetWaypointPath(expected.Name);

            // Point-by-point rather than record equality: the record holds an
            // IReadOnlyList, so its generated Equals is reference equality on
            // the collection and would pass for any two paths at all.
            Assert.Equal(expected.Points.Count, actual.Points.Count);
            for (int index = 0; index < expected.Points.Count; index++)
            {
                Assert.Equal(expected.Points[index], actual.Points[index]);
            }

            // Record equality on each point includes its target, which decides
            // motion: a fixture that drifted on one would send every Core-only
            // follower down a different route from the product's while every
            // coordinate still matched.
        }
    }

    /// <summary>
    /// The loaded paths as retail holds them: each list is the file order
    /// reversed, and the targets form the chains the RE lane read
    /// (<c>reverse-engineering/game-mechanics/waypoint-paths.md</c>, "Level 100
    /// paths").
    /// </summary>
    [Fact]
    public void ManifestWaypointPaths_AreRetailListsWithTheirTargetChains()
    {
        Level100ActorDefinitionSet manifest = Manifest();

        Level100WaypointPathDefinition flyby = manifest.GetWaypointPath("Flyby Path");
        Assert.Equal([41, 42, 43], flyby.Points.Select(point => point.NodeIndex));
        Assert.Equal([42, 43, null], flyby.Points.Select(point => point.TargetNodeIndex));

        Level100WaypointPathDefinition transporter = manifest.GetWaypointPath("Transporter Path");
        Assert.Equal([23, 22, 44], transporter.Points.Select(point => point.NodeIndex));
        Assert.Equal([44, 23, null], transporter.Points.Select(point => point.TargetNodeIndex));

        Level100WaypointPathDefinition tanks = manifest.GetWaypointPath("Target Tank Path 2");
        Assert.Equal([24, 10, 8, 37, 38], tanks.Points.Select(point => point.NodeIndex));
        Assert.Equal([8, 24, 38, 10, 37], tanks.Points.Select(point => point.TargetNodeIndex!.Value));

        // Exactly the two loops: every node of theirs has a target.
        Assert.Equal(
            ["Target Tank Path 2", "Drone Path 1"],
            manifest.WaypointPaths
                .Where(path => path.Points.All(point => point.TargetNodeIndex is not null))
                .Select(path => path.Name));
    }

    /// <summary>
    /// The authored Air Trainer pose that
    /// <see cref="Level100AirTrainerFlybyTests"/> flies from, checked against
    /// the manifest it was copied out of.
    /// </summary>
    /// <remarks>
    /// The Core fixture parks every non-static actor at the origin, so that
    /// test states the pose itself. A stated constant is a stale constant
    /// waiting to happen, and this is the check that makes it not one.
    /// </remarks>
    [Fact]
    public void ManifestAirTrainer_IsAuthoredWhereTheseTestsPutIt()
    {
        Level100ActorDefinition trainer = Assert.Single(
            Manifest().Actors,
            actor => actor.Name == "Air Trainer");

        Assert.Equal("Flyby", trainer.ScriptName);
        Assert.Equal(
            Level100AirTrainerFlybyTests.AuthoredAirTrainerPosition,
            trainer.InitialPose.PositionMillimeters);
        Assert.Equal(
            Level100AirTrainerFlybyTests.AuthoredAirTrainerBasis,
            trainer.InitialPose.BasisFloatBits);
    }

    [Fact]
    public void TestFixtureMotionDefinitions_AreTheManifestsMotionDefinitions()
    {
        Level100ActorDefinitionSet manifest = Manifest();
        Level100ActorDefinitionSet fixture = Level100TestActorDefinitions.Create();

        Assert.Equal(manifest.MotionDefinitions.Count, fixture.MotionDefinitions.Count);
        foreach (Level100ActorMotionDefinition expected in manifest.MotionDefinitions)
        {
            Level100ActorMotionDefinition actual = fixture.GetMotionDefinition(expected.DefinitionName);
            Assert.Equal(expected with { WeaponMounts = null }, actual with { WeaponMounts = null });
            Assert.Equal(expected.WeaponMounts is null, actual.WeaponMounts is null);
            if (expected.WeaponMounts is { } mounts)
                Assert.Equal(mounts, actual.WeaponMounts!);
        }
    }

    /// <summary>
    /// The property the aliasing broke, asserted on the manifest itself so it
    /// cannot come back through the materializer without a failure.
    /// </summary>
    /// <remarks>
    /// Before <c>58d9ce57</c> the 30 waypoint nodes held only 11 distinct
    /// positions, with 19 aliased pairs; afterwards all 30 are distinct. That
    /// count is what makes this a real check rather than a restatement — a
    /// coordinate lookup against the wrong table is exactly the failure that
    /// produces repeated positions.
    /// </remarks>
    [Fact]
    public void ManifestWaypointNodes_AreDistinctPositionsWithNoAliasing()
    {
        Level100ActorDefinitionSet manifest = Manifest();
        Level100WaypointPointDefinition[] points = manifest.WaypointPaths
            .SelectMany(path => path.Points)
            .ToArray();

        Assert.Equal(30, points.Length);
        Assert.Equal(30, points.Select(point => point.NodeIndex).Distinct().Count());
        Assert.Equal(30, points.Select(point => point.PositionMillimeters).Distinct().Count());

        // The independent corroboration from the correction: exactly four nodes
        // sit off the ground, and they are the two ambient AIRCRAFT routes -
        // -15000 on Flyby Path, the Air Trainer's own spawn altitude, and
        // -20000 on Transporter Path. Nothing in the decode knows which path
        // belongs to which aircraft.
        Assert.Equal(
            new Dictionary<int, int> { [0] = 26, [-15_000] = 2, [-20_000] = 2 },
            points
                .GroupBy(point => point.PositionMillimeters.Y)
                .ToDictionary(group => group.Key, group => group.Count()));
        Assert.All(
            manifest.GetWaypointPath("Flyby Path").Points.SkipLast(1),
            point => Assert.Equal(-15_000, point.PositionMillimeters.Y));
        Assert.All(
            manifest.GetWaypointPath("Transporter Path").Points.SkipLast(1),
            point => Assert.Equal(-20_000, point.PositionMillimeters.Y));
    }
}
