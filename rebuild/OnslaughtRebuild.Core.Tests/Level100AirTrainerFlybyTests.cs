// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The ambient Air Trainer's opening pass along <c>Flyby Path</c>: which nodes
/// it visits, in which order, and how close to the ground it gets doing it.
/// </summary>
/// <remarks>
/// <para>
/// Retail walks a path from the node nearest the unit and then along each
/// node's own target (<c>reverse-engineering/game-mechanics/waypoint-paths.md</c>,
/// pristine specimen <c>74154BFA…</c>): <c>FollowWaypointWait</c>
/// (<c>0x00537e40</c>) takes the nearest node from <c>0x00505c30</c>, and
/// <c>IScript::UpdateWaypointFollowing</c> (<c>0x00538470</c>) moves to
/// the current waypoint's <c>+0x3c</c> on arrival (<c>0x005384dc</c>). The
/// serialized list only picks the start.
/// </para>
/// <para>
/// <c>Flyby Path</c> chains 41 → 42 → 43. The Air Trainer is authored nearer
/// 42 (squared distance 10,430.8 against 21,186.1 for 41), so it flies 42 → 43
/// and never visits 41. Until 2026-09-26 the rebuild started every walk at the
/// chain head and flew 41 → 42 → 43.
/// </para>
/// </remarks>
public sealed class Level100AirTrainerFlybyTests
{
    private readonly ITestOutputHelper _output;

    public Level100AirTrainerFlybyTests(ITestOutputHelper output) =>
        _output = output;

    /// <summary>
    /// The Air Trainer's authored initial pose, from the hash-pinned manifest
    /// <c>level100-static-world.json</c> (sha256
    /// <c>D6D3F9ED…D493</c>, schema v14), actor <c>wres:rlwd:0040</c>. The Core
    /// fixture now admits this transform at creation. This test asserts it
    /// instead of overwriting the retained state. It is cross-checked against the
    /// manifest by
    /// <see cref="Level100WaypointFixtureTests.ManifestAirTrainer_IsAuthoredWhereTheseTestsPutIt"/>.
    /// </summary>
    /// <remarks>
    /// Re-derived 2026-08-01 from <c>-15000</c> by the vertical-datum
    /// correction (task #154). The retail authored Z is unchanged at
    /// <c>-15.0</c>; what changed is that the producer now converts it into
    /// Core's datum, <c>(-10.0 - (-15.0)) * 1000 = +5000</c>, instead of
    /// writing the down-positive number into an up-positive field.
    /// </remarks>
    internal static readonly SimVector3 AuthoredAirTrainerPosition =
        new(-23_188, 5_000, 149_250);

    /// <summary>
    /// The authored basis, same source. Its third column is
    /// <c>(8.74e-8, 0, -1)</c>: the aircraft starts pointing down -Z, which is
    /// broadly toward both candidate first nodes, so nothing about the result
    /// below is an artefact of the plane being aimed at the answer.
    /// </summary>
    internal static readonly Level100FloatBasis3Bits AuthoredAirTrainerBasis =
        new(
            -1_082_130_432, int.MinValue, 867_941_678,
            int.MinValue, 1_065_353_216, int.MinValue,
            -1_279_541_970, int.MinValue, -1_082_130_432);

    private const int FlightCoreTicks = 200 * SimulationConstants.TicksPerSecond;

    /// <summary>
    /// COUNTABLE (a): the node visit order, observed from the aircraft's own
    /// position rather than from the field under test.
    /// </summary>
    /// <remarks>
    /// The visited node is identified by asking which authored point the
    /// aircraft is inside the arrival radius of at the moment the cursor moves.
    /// That reads the points and the plane's pose, never the targets, so the
    /// assertion is where the aeroplane actually went.
    /// </remarks>
    [Fact]
    public void AirTrainer_FliesFromItsNearestNodeAlongTheTargets()
    {
        Assert.Equal([42, 43], FlyTheAuthoredRoute().Visited);
    }

    /// <summary>
    /// COUNTABLE (b): the altitude profile of the first leg. Once airborne the
    /// aircraft makes no ground contact and settles into the released level
    /// band, rather than descending toward the ground-level node.
    /// </summary>
    /// <remarks>
    /// Native guide pitch reads the complete waypoint XYZ before its cached
    /// clearance/avoidance overrides. The former horizontal-only steering
    /// explanation was an approximation, not a retail altitude contract.
    /// These assertions measure the resulting route rather than a fixed-speed
    /// or no-roll model.
    /// </remarks>
    [Fact]
    public void AirTrainer_ClearsTheGroundOnceAirborneAlongTheFirstLeg()
    {
        FlightProfile authored = FlyTheAuthoredRoute();

        Assert.True(
            authored.FirstLegTicks > 0,
            "the aircraft never reached the first node, so there is no first leg to measure");
        Assert.True(
            authored.ClimbedOutAtTick > 0,
            "the aircraft never climbed clear of the terrain at all");
        Assert.True(
            authored.MinimumClearanceAfterClimb > 0,
            $"the Air Trainer touched the terrain {authored.MinimumClearanceAfterClimb} mm " +
            "into it after it had already climbed clear, on the first leg of its " +
            "corrected route.");
        Assert.InRange(
            authored.MinimumClearanceAfterClimb,
            SimulationConstants.Level100PlaneClimbClearanceMillimeters,
            SimulationConstants.Level100PlaneDiveClearanceMillimeters);

        // The datum, pinned so that a move in it is visible here rather than
        // silent. Re-derived 2026-08-01 from -3840 - the aircraft used to be
        // authored 3,840 mm UNDER the terrain - by the #154 producer
        // correction. Core Y +5000 against the same terrain sample of -11,160
        // is +16,160 mm of clearance, and it is inside the released band from
        // the first tick rather than climbing out of the ground into it.
        Assert.Equal(16_160, authored.ClearanceAtSpawn);
    }

    /// <summary>
    /// The start is the node nearest the unit, not the chain's head: the same
    /// aircraft placed on node 41 walks the whole chain.
    /// </summary>
    [Fact]
    public void TheStartIsTheNodeNearestTheUnit()
    {
        Assert.Equal([41, 42, 43], FlyTheAuthoredRoute(startOnNode: 41).Visited);
    }

    /// <summary>
    /// Waypoints take their load-time height: <c>CThing::Init</c> raises one
    /// below the heightfield sample to it, then one below the water level to
    /// that (<c>waypoint-paths.md</c>, "Loading"; z grows downward). Flyby node
    /// 42 is authored at z = -15, 3.1 m inside the hillside (ground -18.10), so
    /// it is raised to the ground; node 43 is authored at z = 0 over the sea
    /// (ground +1.16), so it is raised to the water level (-8.84).
    /// </summary>
    [Fact]
    public void Waypoints_TakeTheirLoadTimeGroundOrWaterHeight()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId trainer = actors.GetThingRef("Air Trainer")!.Value;
        Level100WaypointPathDefinition path = definitions.GetWaypointPath("Flyby Path");
        Level100FloatVector4Bits node42 = path.Point(42).RetailComponentsFloatBits;
        Level100FloatVector4Bits node43 = path.Point(43).RetailComponentsFloatBits;
        float ground42 = RetailWorldTerrain.SampleRetailHeight(
            Level100Terrain.Instance, new(node42.X, node42.Y, node42.Z));
        float ground43 = RetailWorldTerrain.SampleRetailHeight(
            Level100Terrain.Instance, new(node43.X, node43.Y, node43.Z));
        float water = Level100Terrain.Instance.WaterLevel;
        Assert.Equal(-15f, BitConverter.Int32BitsToSingle(node42.Z));
        Assert.True(ground42 < -15f && water > ground42);
        Assert.Equal(0f, BitConverter.Int32BitsToSingle(node43.Z));
        Assert.True(ground43 > 0f && water < 0f);

        actors.GetPlaneState(trainer).TeleportRetailPosition(
            new(node42.X, node42.Y, BitConverter.SingleToInt32Bits(ground42)));
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1, 0, trainer, Level100ActorScriptCommandKind.FollowWaypointWait, null, path.Name, 0));
        Level100PlaneGuideSnapshot Guide() =>
            mechanics.Snapshot.Actors.Single(actor => actor.ActorId == trainer).PlaneGuide!;
        // The follower's order goes through slot 61 (0x00403a90), which raises
        // the seated node to MinAltitude above the integer floor.
        Assert.Equal(
            Level100ActorMechanics.ClampAirMoveTarget(Level100Terrain.Instance,
                new(node42.X, node42.Y, BitConverter.SingleToInt32Bits(ground42)), 0x40800000),
            Guide().Destination);

        mechanics.AdvanceTick();
        Assert.Equal(43, mechanics.Snapshot.Actors.Single(actor => actor.ActorId == trainer).WaypointNodeIndex);
        Assert.Equal(
            Level100ActorMechanics.ClampAirMoveTarget(Level100Terrain.Instance,
                new(node43.X, node43.Y, BitConverter.SingleToInt32Bits(water)), 0x40800000),
            Guide().Destination);
    }

    /// <summary>
    /// A chain that closes on its own head never ends, so the follower wraps
    /// instead of completing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two of the eight Level 100 paths close - <c>Drone Path 1</c> and
    /// <c>Target Tank Path 2</c> - and until #146 nothing read
    /// <c>isClosed</c> at all, so both were walked once and abandoned.
    /// </para>
    /// <para>
    /// This is not a design choice, it is what the released cursor cannot avoid
    /// doing. <c>IScript::UpdateWaypointFollowing</c> (<c>0x00538470</c>,
    /// pristine specimen sha256 <c>74154BFA…</c>) ends the walk on exactly one
    /// condition: the successor it loaded from <c>[current+0x3c]</c> is NULL
    /// (<c>mov eax,[esi+0x14]</c> / <c>cmp eax,edi</c> at <c>0x00538500</c>
    /// with <c>edi</c> zero). When the tail's successor is the head there is no
    /// NULL on the ring, so the completion path is unreachable.
    /// </para>
    /// <para>
    /// Driven by parking the aircraft on whatever node it is currently steering
    /// at, rather than flying the ring, because what is under test is the cursor
    /// and not the aerodynamics. Note it re-parks on EVERY core tick: arrivals
    /// are only observed on released base ticks, which fire 20 times per 30 core
    /// ticks, so "one tick per node" would be wrong.
    /// </para>
    /// </remarks>
    [Fact]
    public void ClosedChain_WrapsToItsHeadInsteadOfCompleting()
    {
        Level100WaypointPathDefinition path;
        (int[] visited, var completions, var final) = WalkByTeleport("Drone Path 1", out path);

        Assert.Equal([4, 3, 2, 1], path.Points.Select(point => point.NodeIndex));
        Assert.Equal([1, 4, 3, 2], path.Points.Select(point => point.TargetNodeIndex!.Value));

        // From the node nearest the aircraft, round the ring and past the seam
        // twice over.
        Assert.Equal(
            [3, 4, 1, 2, 3, 4, 1, 2, 3],
            visited.Take(9));

        // No wait completion is ever raised, even though the command was
        // FollowWaypointWait: the ring has no end to report.
        Assert.Empty(completions);
        Assert.Equal(Level100ActorCommandIntent.FollowingWaypoint, final.Intent);
        Assert.Equal(path.Name, final.WaypointPath);
    }

    /// <summary>
    /// The contrast that makes the test above mean something: an OPEN chain
    /// still completes, exactly once, and still reports its wait.
    /// </summary>
    [Fact]
    public void OpenChain_StillCompletesAtItsTail()
    {
        Level100WaypointPathDefinition path;
        (int[] visited, var completions, var final) = WalkByTeleport("Flyby Path", out path);

        Assert.Null(path.Point(43).TargetNodeIndex);
        Assert.Equal([42, 43], visited);

        Level100ActorMechanicsWaitCompletion completion = Assert.Single(completions);
        Assert.Equal(path.Name, completion.Argument);
        Assert.Equal(Level100ActorScriptWaitKind.FollowWaypoint, completion.WaitKind);
        Assert.Equal(Level100ActorCommandIntent.Stopped, final.Intent);
    }

    /// <summary>
    /// A waypoint that targets itself logs an error and ends the walk
    /// (<c>UpdateWaypointFollowing</c> <c>0x00538470</c>; waypoint-paths.md,
    /// "Following"). No admitted level carries one, so the path is rewritten.
    /// </summary>
    [Fact]
    public void SelfTargetingWaypoint_EndsTheWalk()
    {
        (int[] visited, var completions, var final) = WalkByTeleport("Flyby Path", out _,
            definitions => new Level100ActorDefinitionSet(
                definitions.Actors,
                definitions.Spawns,
                definitions.WaypointPaths.Select(path => path.Name != "Flyby Path"
                    ? path
                    : path with
                    {
                        Points = path.Points.Select(point => point.NodeIndex == 42
                            ? point with { TargetNodeIndex = 42 }
                            : point).ToArray(),
                    }).ToArray(),
                definitions.MotionDefinitions));

        Assert.Equal([42], visited);
        Assert.Single(completions);
        Assert.Equal(Level100ActorCommandIntent.Stopped, final.Intent);
    }

    /// <summary>
    /// Walks a path by repeatedly teleporting the follower onto the node its
    /// cursor currently names, and records the node index each time the cursor
    /// moves. Stops early once the follower leaves FollowingWaypoint.
    /// </summary>
    private (int[] Visited,
        IReadOnlyList<Level100ActorMechanicsWaitCompletion> Completions,
        Level100ActorCommandIntentSnapshot Final)
        WalkByTeleport(string pathName, out Level100WaypointPathDefinition path,
            Func<Level100ActorDefinitionSet, Level100ActorDefinitionSet>? rewrite = null)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        if (rewrite is not null)
        {
            definitions = rewrite(definitions);
        }
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId trainer = actors.GetThingRef("Air Trainer")!.Value;
        path = definitions.GetWaypointPath(pathName);

        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            trainer,
            Level100ActorScriptCommandKind.FollowWaypointWait,
            null,
            pathName,
            0));

        Level100ActorCommandIntentSnapshot State() =>
            mechanics.Snapshot.Actors.Single(actor => actor.ActorId == trainer);

        var visited = new List<int>();
        var completions = new List<Level100ActorMechanicsWaitCompletion>();
        int? cursor = null;
        // Three core ticks per base tick is the worst case, and nine laps of the
        // longest path is more headroom than any assertion here needs.
        int budget = 9 * 3 * (path.Points.Count + 1);
        for (int tick = 0; tick < budget; tick++)
        {
            Level100ActorCommandIntentSnapshot before = State();
            if (before.Intent != Level100ActorCommandIntent.FollowingWaypoint)
            {
                break;
            }

            Level100FloatVector4Bits point = path.Point(before.WaypointNodeIndex!.Value).RetailComponentsFloatBits;
            actors.GetPlaneState(trainer).TeleportRetailPosition(new(point.X, point.Y, point.Z));
            completions.AddRange(mechanics.AdvanceTick());

            Level100ActorCommandIntentSnapshot after = State();
            bool advanced =
                after.WaypointNodeIndex != before.WaypointNodeIndex ||
                after.Intent != Level100ActorCommandIntent.FollowingWaypoint;
            if (advanced)
            {
                // The node just reached is the one the cursor named going in.
                visited.Add(before.WaypointNodeIndex.Value);
                cursor = after.WaypointNodeIndex;
            }
        }

        _output.WriteLine(
            $"{pathName}: visited [{string.Join(", ", visited)}] " +
            $"completions={completions.Count} finalCursor={cursor}");
        return (visited.ToArray(), completions, State());
    }

    /// <param name="Visited">
    /// The node indices the aircraft actually arrived at, in order.
    /// </param>
    /// <param name="ClearanceAtSpawn">
    /// Terrain clearance at the authored pose, before the first tick moves
    /// anything. Negative means the aircraft is authored underground.
    /// </param>
    /// <param name="ClimbedOutAtTick">
    /// The first core tick at which clearance reached the released climb
    /// threshold, or 0 if it never did.
    /// </param>
    /// <param name="MinimumClearanceAfterClimb">
    /// The smallest clearance held between that tick and the end of the first
    /// leg.
    /// </param>
    /// <param name="FirstLegTicks">Core ticks to the first arrival.</param>
    private readonly record struct FlightProfile(
        int[] Visited,
        int ClearanceAtSpawn,
        int ClimbedOutAtTick,
        int MinimumClearanceAfterClimb,
        int FirstLegTicks);

    private FlightProfile FlyTheAuthoredRoute(int? startOnNode = null)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId trainer = actors.GetThingRef("Air Trainer")!.Value;
        Level100WaypointPathDefinition path = definitions.GetWaypointPath("Flyby Path");
        long arrivalRadius = definitions
            .GetMotionDefinition("Air Trainer").ArrivalRadiusMillimeters;

        Assert.Equal(AuthoredAirTrainerPosition, actors.GetPose(trainer).PositionMillimeters);
        ThingActorBaseStateSnapshot initial = actors.GetPlaneState(trainer).Snapshot;
        Assert.Equal(definitions.Actors.Single(actor => actor.Name == "Air Trainer")
            .AuthoredTransform.RetailEulerFloatBits, initial.RetailPlane!.CurrentEuler);
        Assert.Equal(initial.RetailPlane.CurrentEuler, initial.RetailPlane.DesiredEuler);
        Assert.Equal(default, initial.RetailPlane.Velocity);
        Assert.Equal(default, initial.RetailPlane.Drive);
        if (startOnNode is { } node)
        {
            Level100FloatVector4Bits start = path.Point(node).RetailComponentsFloatBits;
            actors.GetPlaneState(trainer).TeleportRetailPosition(new(start.X, start.Y, start.Z));
        }
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            trainer,
            Level100ActorScriptCommandKind.FollowWaypoint,
            null,
            path.Name,
            0));

        int clearanceAtSpawn = Clearance(actors.GetActor(trainer).Pose);

        var visited = new List<int>();
        int? cursor = mechanics.Snapshot.Actors.Single(actor => actor.ActorId == trainer).WaypointNodeIndex;
        int climbedOutAtTick = 0;
        int minimumClearanceAfterClimb = int.MaxValue;
        int firstLegTicks = 0;
        for (int coreTick = 0; coreTick < FlightCoreTicks; coreTick++)
        {
            mechanics.AdvanceTick();
            Level100ActorCommandIntentSnapshot state =
                mechanics.Snapshot.Actors.Single(actor => actor.ActorId == trainer);
            Level100ActorPoseSnapshot pose = actors.GetActor(trainer).Pose;

            if (visited.Count == 0)
            {
                firstLegTicks = coreTick + 1;
                int clearance = Clearance(pose);
                if (climbedOutAtTick == 0 &&
                    clearance >=
                        SimulationConstants.Level100PlaneClimbClearanceMillimeters)
                {
                    climbedOutAtTick = coreTick + 1;
                }

                if (climbedOutAtTick > 0)
                {
                    minimumClearanceAfterClimb =
                        Math.Min(minimumClearanceAfterClimb, clearance);
                }
            }

            bool advanced = state.WaypointNodeIndex != cursor;
            bool finished = state.Intent != Level100ActorCommandIntent.FollowingWaypoint;
            if (!advanced && !finished)
            {
                continue;
            }

            // Whichever authored node the aircraft is standing inside is the
            // one it just arrived at. Read from Points and the pose only.
            Level100WaypointPointDefinition[] inside = path.Points
                .Where(point =>
                {
                    long deltaX =
                        (long)point.PositionMillimeters.X - pose.PositionMillimeters.X;
                    long deltaZ =
                        (long)point.PositionMillimeters.Z - pose.PositionMillimeters.Z;
                    return (deltaX * deltaX) + (deltaZ * deltaZ) <
                        arrivalRadius * arrivalRadius;
                })
                .ToArray();
            visited.Add(Assert.Single(inside).NodeIndex);
            _output.WriteLine(
                $"tick {coreTick + 1}: arrived node {visited[^1]} at " +
                $"{pose.PositionMillimeters}");
            cursor = state.WaypointNodeIndex;
            if (finished)
            {
                break;
            }
        }

        _output.WriteLine(
            $"visited [{string.Join(", ", visited)}]; first leg {firstLegTicks} core ticks; " +
            $"clearance at spawn {clearanceAtSpawn} mm; climbed clear at tick " +
            $"{climbedOutAtTick}; minimum clearance after that " +
            $"{minimumClearanceAfterClimb} mm");
        return new FlightProfile(
            visited.ToArray(),
            clearanceAtSpawn,
            climbedOutAtTick,
            minimumClearanceAfterClimb,
            firstLegTicks);
    }

    private static int Clearance(Level100ActorPoseSnapshot pose) =>
        pose.PositionMillimeters.Y -
        Level100Terrain.Instance.SampleGroundElevationMillimeters(
            new SimVector2(pose.PositionMillimeters.X, pose.PositionMillimeters.Z));
}
