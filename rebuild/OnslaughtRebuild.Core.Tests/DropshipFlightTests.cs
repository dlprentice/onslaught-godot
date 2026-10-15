// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Level 100's U-17 in flight, from the RE lane's dropship contracts
/// (<c>reverse-engineering/game-mechanics/dropship-flight.md</c>,
/// <c>dropship-landing.md</c>) and the pristine specimen's bytes cited in
/// <see cref="RetailDropshipMotion"/> and <c>Level100ActorDropshipRuntime</c>.
/// Expected values are worked from the contract's formulas, not read back
/// from the implementation.
/// </summary>
public sealed class DropshipFlightTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_level100 =
        new(Level100TestActorDefinitions.LoadMaterialized);

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

    private static float Value(int bits) => BitConverter.Int32BitsToSingle(bits);

    private static RetailActorPoseSnapshot LevelPose(float x, float y, float z, float yaw = 0f) =>
        new(new(Bits(x), Bits(y), Bits(z)), RetailUnitEuler.BuildBasis(new(Bits(yaw), 0, 0)));

    /// <summary>
    /// Cruise (<c>0x00448a64-0x00448c3d</c>): the yaw target is −atan2(Dx, Dy);
    /// the roll is the remaining turn capped at π/8, negative when the goal
    /// lies along +R; the drive is 0.001 × F whatever the distance.
    /// </summary>
    [Fact]
    public void Cruise_TurnsTowardTheGoalRollsAtMostAnEighthOfPiAndThrustsAlongTheNose()
    {
        RetailActorPoseSnapshot pose = LevelPose(100f, 100f, -20f);
        RetailPlaneGuideOutput output = RetailDropshipMotion.UpdateCruise(
            pose, default, new(Bits(110f), Bits(100f), Bits(-20f)), mode: 1, floor: 0f, 0x40800000);

        Assert.Equal(Bits((float)-Math.Atan2(10f, 0f)), output.DesiredEuler.X);
        Assert.Equal(0, output.DesiredEuler.Y);
        // The goal is along +R (column 0 is (1, 0, -0) at yaw 0): roll -π/8.
        Assert.Equal(unchecked((int)0xbec90fdb), output.DesiredEuler.Z);
        // F is column 1, (-0, 1, 0): the drive keeps the negative zero.
        Assert.Equal(new Level100FloatVector3Bits(unchecked((int)0x80000000), 0x3a83126f, 0), output.Drive);
        Assert.Equal(0, output.BankFlagFloatBits);

        // The mirror image rolls the other way.
        RetailPlaneGuideOutput left = RetailDropshipMotion.UpdateCruise(
            pose, default, new(Bits(90f), Bits(100f), Bits(-20f)), mode: 1, floor: 0f, 0x40800000);
        Assert.Equal(0x3ec90fdb, left.DesiredEuler.Z);
        Assert.Equal(output.Drive, left.Drive);
    }

    /// <summary>
    /// Below MinAltitude (h = floor − z &lt; 4) the heading holds and the
    /// pitch target is −π/5 (<c>0xbf20d97c</c>, <c>0x00448a93-0x00448ab9</c>).
    /// </summary>
    [Fact]
    public void Cruise_BelowMinAltitude_HoldsTheHeadingAndPitchesTheNoseUp()
    {
        RetailActorPoseSnapshot pose = LevelPose(100f, 100f, -20f, yaw: 0.25f);
        var current = new Level100FloatVector3Bits(Bits(0.25f), 0, 0);
        var goal = new Level100FloatVector3Bits(Bits(110f), Bits(100f), Bits(-20f));

        RetailPlaneGuideOutput low = RetailDropshipMotion.UpdateCruise(pose, current, goal, 1, floor: -17f, 0x40800000);
        Assert.Equal(Bits(0.25f), low.DesiredEuler.X);
        Assert.Equal(unchecked((int)0xbf20d97c), low.DesiredEuler.Y);

        // Exactly MinAltitude above the floor is not below it.
        RetailPlaneGuideOutput level = RetailDropshipMotion.UpdateCruise(pose, current, goal, 1, floor: -16f, 0x40800000);
        Assert.Equal(0, level.DesiredEuler.Y);
    }

    /// <summary>
    /// The guide's mode picks the yaw target: mode 0 circles (the current yaw
    /// + π/2) and mode 2 flies away (the goal's yaw − π), then wraps into
    /// [−π, π].
    /// </summary>
    [Fact]
    public void Cruise_ModeZeroCirclesAndModeTwoFliesAway()
    {
        RetailActorPoseSnapshot pose = LevelPose(100f, 100f, -20f, yaw: 0.25f);
        var current = new Level100FloatVector3Bits(Bits(0.25f), 0, 0);
        var ahead = new Level100FloatVector3Bits(Bits(100f), Bits(110f), Bits(-20f));

        Assert.Equal(Bits(0.25f + 1.5707964f),
            RetailDropshipMotion.UpdateCruise(pose, current, ahead, 0, 0f, 0x40800000).DesiredEuler.X);
        // −atan2(0, 10) = −0, and −0 − π is −π exactly: no wrap.
        Assert.Equal(unchecked((int)0xc0490fdb),
            RetailDropshipMotion.UpdateCruise(pose, current, ahead, 2, 0f, 0x40800000).DesiredEuler.X);
    }

    /// <summary>
    /// The remaining turn is taken the short way round
    /// (<c>0x00448afb-0x00448b61</c>): from yaw 3.0 to a target near −3.0 it
    /// is about 0.28, not about 6.
    /// </summary>
    [Fact]
    public void Cruise_TakesTheTurnTheShortWayAcrossPi()
    {
        RetailActorPoseSnapshot pose = LevelPose(100f, 100f, -20f, yaw: 3f);
        var current = new Level100FloatVector3Bits(Bits(3f), 0, 0);
        // A goal whose yaw −atan2(Dx, Dy) is close to −3.0.
        var goal = new Level100FloatVector3Bits(
            Bits(100f + (float)(10 * Math.Sin(3.0))), Bits(100f + (float)(10 * Math.Cos(3.0))), Bits(-20f));
        RetailPlaneGuideOutput output = RetailDropshipMotion.UpdateCruise(pose, current, goal, 1, 0f, 0x40800000);

        double target = Value(output.DesiredEuler.X);
        Assert.InRange(target, -3.001, -2.999);
        double expected = Math.Abs(3.0 - (target + (2 * Math.PI)));
        Assert.InRange(Math.Abs(Value(output.DesiredEuler.Z)), expected - 2e-6, expected + 2e-6);
    }

    /// <summary>
    /// The air step's damping (<c>0x00403038-0x004030d1</c>): a Big craft whose
    /// z + 0.2 × radius is below the water damps by 0.95, otherwise by slot
    /// 73's 0.99; a craft that is not Big never takes the water damping.
    /// </summary>
    [Fact]
    public void Damping_IsTheWaterDampingOnlyForABigCraftAtTheWater()
    {
        const int radius = 0x40f4fcfe; // the lifter mesh, 7.6559
        // 7.6559 × 0.2 = 1.5312: at z = −1.0 the craft reaches 0.53 below water 0.
        Assert.Equal(RetailDropshipMotion.WaterDampingFloatBits,
            RetailDropshipMotion.SelectDamping(true, radius, Bits(-1f), 0f));
        Assert.Equal(RetailDropshipMotion.DampingFloatBits,
            RetailDropshipMotion.SelectDamping(true, radius, Bits(-2f), 0f));
        Assert.Equal(RetailDropshipMotion.DampingFloatBits,
            RetailDropshipMotion.SelectDamping(false, radius, Bits(-1f), 0f));
        Assert.Equal(0.99f, Value(RetailDropshipMotion.DampingFloatBits));
        Assert.Equal(0.95f, Value(RetailDropshipMotion.WaterDampingFloatBits));
    }

    /// <summary>
    /// Slot 61's target (<c>0x00403aa4-0x00403b19</c>): a target below
    /// MinAltitude above the higher of the integer ground and the water is
    /// raised to it; one above is kept bit for bit.
    /// </summary>
    [Fact]
    public void MoveOrder_RaisesTheTargetToMinAltitudeAboveTheFloor()
    {
        Level100Terrain terrain = Level100Terrain.Instance;
        foreach ((float x, float y) in new[] { (357f, 272f), (501f, 296f), (241f, 279.75f) })
        {
            int ix = (int)Math.Round(x, MidpointRounding.ToEven), iy = (int)Math.Round(y, MidpointRounding.ToEven);
            double ground = terrain.SampleAirGuideHeightUnits(ix, iy) * (double)terrain.HeightScale;
            double floor = Math.Min((float)ground, terrain.WaterLevel);
            float limit = (float)(floor - 4.0);

            Level100FloatVector3Bits low = Level100ActorMechanics.ClampAirMoveTarget(
                terrain, new(Bits(x), Bits(y), 0), 0x40800000);
            Assert.Equal(Bits(limit), low.Z);
            var high = new Level100FloatVector3Bits(Bits(x), Bits(y), Bits(limit - 10f));
            Assert.Equal(high, Level100ActorMechanics.ClampAirMoveTarget(terrain, high, 0x40800000));
        }
    }

    /// <summary>
    /// The retreat point (<c>0x004fd910</c>): the origin unless a safe side of
    /// the unit's own side is strictly nearer in 2D; the list is walked newest
    /// first, so of two equally near the newer wins.
    /// </summary>
    [Fact]
    public void RetreatPoint_IsTheNearestOwnSideSafeSideNewestFirst()
    {
        var unit = new Level100FloatVector3Bits(Bits(100f), Bits(100f), Bits(-15f));
        Level100SafeSideDefinition Side(int row, float x, float y, int allegiance) =>
            new(false, row, new(Bits(x), Bits(y), unchecked((int)0x80000000)), allegiance);

        // Rows 1 and 2 are both 10 away; row 2 was built later.
        Level100SafeSideDefinition[] sides =
            [Side(1, 110f, 100f, 0), Side(2, 90f, 100f, 0), Side(3, 100f, 101f, 1)];
        Assert.Equal(sides[1].RetailPositionFloatBits, Level100ActorMechanics.RetreatPoint(unit, 0, sides));
        Assert.Equal(sides[2].RetailPositionFloatBits, Level100ActorMechanics.RetreatPoint(unit, 1, sides));
        // Nothing nearer than the origin: the origin.
        var nearOrigin = new Level100FloatVector3Bits(Bits(1f), Bits(1f), Bits(-15f));
        Assert.Equal(default, Level100ActorMechanics.RetreatPoint(nearOrigin, 0, sides));

        // Level 100's own side (0) has level rows 20 (501, 296) and 39
        // (207, 503.5); from the path's last node (357, 272) row 20 is nearer.
        var node44 = new Level100FloatVector3Bits(Bits(357f), Bits(272f), Bits(-15f));
        Level100SafeSideDefinition row20 = Assert.Single(s_level100.Value.SafeSides, side => !side.BaseWorld && side.Row == 20);
        Assert.Equal(row20.RetailPositionFloatBits,
            Level100ActorMechanics.RetreatPoint(node44, 0, s_level100.Value.SafeSides));
    }

    /// <summary>
    /// <c>UpdateWaypointFollowing</c> (<c>0x00538470</c>): an air unit has
    /// arrived only when its stored 2D distance is below its class radius,
    /// 8.0 for the dropship and 5.0 for the plane (the motion rows carry both).
    /// </summary>
    [Fact]
    public void Arrival_IsStrictlyInsideTheClassRadius()
    {
        var node = new Level100FloatVector4Bits(Bits(100f), Bits(100f), Bits(-20f), 0);
        Assert.False(RetailIScriptFollowWaypoint.Arrived(new(Bits(108f), Bits(100f), Bits(-15f)), node, 8f));
        Assert.True(RetailIScriptFollowWaypoint.Arrived(new(Bits(107.999f), Bits(100f), Bits(-15f)), node, 8f));
        Assert.False(RetailIScriptFollowWaypoint.Arrived(new(Bits(100f), Bits(105f), Bits(-15f)), node, 5f));
        Assert.Equal((8_000, 5_000),
            (s_level100.Value.GetMotionDefinition("U-17 Highside Transporter").ArrivalRadiusMillimeters,
             s_level100.Value.GetMotionDefinition("Target Drone").ArrivalRadiusMillimeters));
    }

    /// <summary>
    /// The U-17's construction: its rates are the unit record's air turn rate
    /// for yaw and a quarter of it for pitch and roll
    /// (<c>0x00446dc2-0x00446e0c</c>); its MOVE is filed with its Actor draw,
    /// ahead of its 4003; its guide starts in mode 0 at the craft.
    /// </summary>
    [Fact]
    public void Construction_QuartersPitchAndRollRatesAndFilesTheMoveBeforeTheRefresh()
    {
        var actors = new Level100ActorRegistry(s_level100.Value);
        var mechanics = new Level100ActorMechanics(actors, s_level100.Value);
        Level100ActorId u17 = actors.GetThingRef("Transporter")!.Value;
        RetailPlaneMotionSnapshot air = actors.GetBaseState(u17).RetailPlane!;
        Assert.Equal(new Level100FloatVector3Bits(0x3BE4C388, 0x3AE4C388, 0x3AE4C388), air.EulerRates);

        Level100ActorMechanicsSnapshot snapshot = mechanics.Snapshot;
        Level100PlaneGuideSnapshot guide = Assert.Single(snapshot.Actors, item => item.ActorId == u17).PlaneGuide!;
        Assert.Equal((0, 0), (guide.Mode, guide.SpeedMode));
        Assert.Equal(actors.GetBaseState(u17).RetailPoses!.Current.PositionFloatBits, guide.Destination);

        RetailEventSchedulerSnapshot events = snapshot.PlaneEvents!;
        var slots = events.Slots.ToDictionary(slot => slot.Handle);
        int[] order = events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow).ToArray();
        int Position(int listener, short eventNum) => Array.FindIndex(order, handle =>
            slots[handle].Listener == listener && slots[handle].EventNum == eventNum);
        int move = Position(Level100ActorMechanics.AirUnitMoveListener(u17), 3000);
        int refresh = Position(Level100ActorMechanics.UnitOwnListener(u17), 4003);
        Assert.InRange(move, 0, refresh - 1);
    }

    /// <summary>
    /// The U-17's whole flight in Level 100: its script follows "Transporter
    /// Path" from node 22 (22 → 23 → 44), then Retreat sends it to the nearer
    /// own-side safe side, level row 20. Within 4.0 of it the craft runs out
    /// 300 along its nose; the next tick it posts its SHUTDOWN and the tick
    /// after that it leaves the world, its script retired and its callbacks
    /// stopped. The tick numbers are this build's determinism pins, not
    /// retail measurements.
    /// </summary>
    [Fact]
    public void U17_FliesItsPathRetreatsToSafeSide20AndLeaves()
    {
        var simulation = new Simulation(1u, s_level100.Value);
        WorldSnapshot state = simulation.Snapshot;
        Level100ActorId u17 = state.Level100Actors.Actors.Single(actor => actor.Name == "Transporter").ActorId;
        var nodes = new List<int>();
        int? runOutTick = null, shutdownFiledTick = null, leftTick = null;
        Level100FloatVector3Bits retreatGoal = default;
        for (int tick = 1; tick <= 4_000 && leftTick is null; tick++)
        {
            state = simulation.Step(SimInput.Idle);
            Level100ActorCommandIntentSnapshot mechanics =
                state.Level100ActorMechanics.Actors.Single(item => item.ActorId == u17);
            if (mechanics.WaypointNodeIndex is { } node && (nodes.Count == 0 || nodes[^1] != node))
            {
                nodes.Add(node);
            }

            Level100PlaneGuideSnapshot guide = mechanics.PlaneGuide!;
            if (guide.SpeedMode == 1)
            {
                retreatGoal = guide.Destination;
            }

            if (guide.SpeedMode == 2 && runOutTick is null)
            {
                runOutTick = tick;
                ThingActorBaseStateSnapshot raw = state.Level100Actors.BaseStates.Single(item => item.ActorId == u17).State;
                Level100FloatVector3Bits target = RetailDropshipMotion.RunOutTarget(raw.RetailPoses!.Current);
                Assert.Equal((target.X, target.Y), (guide.Destination.X, guide.Destination.Y));
            }

            RetailEventSchedulerSnapshot events = state.Level100ActorMechanics.PlaneEvents!;
            var slots = events.Slots.ToDictionary(slot => slot.Handle);
            if (shutdownFiledTick is null && events.Lanes.SelectMany(lane => lane.Handles).Any(handle =>
                    slots[handle].Listener == Level100ActorMechanics.UnitOwnListener(u17) && slots[handle].EventNum == 2000))
            {
                shutdownFiledTick = tick;
            }

            if (state.Level100Actors.Actors.Single(actor => actor.ActorId == u17).Lifecycle ==
                Level100ActorLifecycle.Destroyed)
            {
                leftTick = tick;
            }
        }

        Assert.Equal([22, 23, 44], nodes);
        Assert.Equal((Bits(501f), Bits(296f)), (retreatGoal.X, retreatGoal.Y));
        Assert.Equal(2_977, runOutTick);
        Assert.Equal(runOutTick + 1, shutdownFiledTick);
        Assert.Equal(shutdownFiledTick + 1, leftTick);
        Assert.DoesNotContain(state.Level100ActorScripts.Instances, instance => instance.ActorId == u17);
        Assert.False(state.Level100Actors.Actors.Single(actor => actor.ActorId == u17).Active);

        // Its unit and AI callbacks and its MOVE are gone for good.
        for (int tick = 0; tick < 200; tick++)
        {
            state = simulation.Step(SimInput.Idle);
        }

        RetailEventSchedulerSnapshot after = state.Level100ActorMechanics.PlaneEvents!;
        var live = after.Slots.ToDictionary(slot => slot.Handle);
        Assert.DoesNotContain(after.Lanes.SelectMany(lane => lane.Handles).Concat(after.Overflow), handle =>
            live[handle].Listener == Level100ActorMechanics.UnitOwnListener(u17) ||
            live[handle].Listener == Level100ActorMechanics.UnitAiListener(u17) ||
            live[handle].Listener == Level100ActorMechanics.AirUnitMoveListener(u17));
    }
}
