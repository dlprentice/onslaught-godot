// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The Battle Engine's crosshair (6002) and auto-aim (6003) refreshes on the
/// shared event clock, the crosshair line's report, and the launch correction
/// that reads it. Evidence: the RE lane's static reads in
/// <c>reverse-engineering/game-mechanics/level100-final-drone-wave.md</c>
/// ("Crosshair and auto-aim refresh") and its Q14 answer on the due-time
/// instructions; source shape <c>BattleEngine.cpp:350-352, :2278-2344, :3016-3067</c>.
/// </summary>
public sealed class Level100BattleEngineRefreshTests
{
    private const uint Seed = 2836905711;

    [Theory]
    [InlineData(0, 0.0f)]
    [InlineData(1, 0.0f)]
    [InlineData(65_535, 0.05f)]
    [InlineData(65_536, 12.35f)]
    [InlineData(123_456_789, 431.2f)]
    [InlineData(int.MaxValue, 7.0f)]
    [InlineData(int.MinValue, 7.0f)]
    public void DueTimes_AreSampleTimesScalePlusNowPlusDelayInSinglePrecision(int draw, float now)
    {
        // r and 0x8000ffff; fild; fmul scale; fadd mTime; fadd delay; fstp.
        int sample = draw % 65536;
        float crosshair = ((sample * BitConverter.UInt32BitsToSingle(0x364ccccd)) + now) + 0.1f;
        float autoAim = ((sample * BitConverter.UInt32BitsToSingle(0x35cccccd)) + now) + 0.2f;

        Assert.Equal(
            BitConverter.SingleToUInt32Bits(crosshair),
            BitConverter.SingleToUInt32Bits(RetailBattleEngineRefresh.CrosshairDueTime(draw, now)));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(autoAim),
            BitConverter.SingleToUInt32Bits(RetailBattleEngineRefresh.AutoAimDueTime(draw, now)));
    }

    [Fact]
    public void ActualMaxRange_IsSpeedTimesLifeOrTheLockRange()
    {
        // Mech Pulse Bolt Medium 35 x 6, Large 20 x 7, both bullets 60 x 1,
        // and the seeking Micro Missile's CWeaponLockRange 100.
        Assert.Equal(210.0f, RetailBattleEngineRefresh.ActualMaxRange(Level100MissionWeapon.PulseCannonPod, false));
        Assert.Equal(140.0f, RetailBattleEngineRefresh.ActualMaxRange(Level100MissionWeapon.PulseCannonPod, true));
        Assert.Equal(60.0f, RetailBattleEngineRefresh.ActualMaxRange(Level100MissionWeapon.MechTwinVulcanCannon, false));
        Assert.Equal(60.0f, RetailBattleEngineRefresh.ActualMaxRange(Level100MissionWeapon.MechVulcanCannon, false));
        Assert.Equal(100.0f, RetailBattleEngineRefresh.ActualMaxRange(Level100MissionWeapon.MissilePod, false));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 1, true)]
    [InlineData(0, 2, false)]
    [InlineData(0, 6, true)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(6, 0, true)]
    [InlineData(6, 1, true)]
    [InlineData(6, 6, false)]
    public void SideGate_OpposesZeroAndOneAndSixAgainstBoth(int mine, int candidate, bool expected) =>
        Assert.Equal(expected, RetailBattleEngineRefresh.IsTargetAllegiance(mine, candidate));

    [Fact]
    public void Construction_FilesTheCrosshairThenTheAutoAimRefreshWithOneDrawEach()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        var simulation = new Simulation(Seed, definitions);
        // The load, before the pre-run's frames deliver anything.
        WorldSnapshot state = simulation.LoadSnapshotForMeasurement!;
        Level100ActorDefinition[] rows = definitions.Actors.OrderBy(row => row.AuthoredOrder).ToArray();
        int battleEngine = Array.FindIndex(rows, row =>
            Level100ConstructionClasses.Of(row.DefinitionName) == Level100ConstructionClass.BattleEngine);
        Assert.Equal(1_481, definitions.BaseWorldPineCount);

        // The load order (the RE lane's construction-order contract and its
        // correction): the base world's pines, the influence map's draw, its
        // rows 0-34, then level row 0's Battle Engine: its Actor draw, 6002's
        // draw and HandleAutoAim(NULL)'s 6003 draw, and then the rest of the
        // level world's rows, the Target Truck and Target Drone warm-ups (two
        // draws each) and the influence map's tail draw. No script runs at the
        // load: the Tank Factory's SpawnThing("Target Tank") waits for its
        // INIT_SCRIPT, which Setup files in frame 1 for frame 2.
        var random = new Level100ReleasedRandom();
        Skip(definitions.BaseWorldPineCount + 1 + Level100ActorWeaponTests.RowDraws(rows[..battleEngine]) + 1);
        float crosshairDue = RetailBattleEngineRefresh.CrosshairDueTime(random.Next(), 0.0f);
        float autoAimDue = RetailBattleEngineRefresh.AutoAimDueTime(random.Next(), 0.0f);
        Skip(Level100ActorWeaponTests.RowDraws(rows[(battleEngine + 1)..]) + 4 + 1);
        Assert.Equal(random.Seed, state.Level100ActorMechanics.ReleasedRandomSeed);

        void Skip(int draws)
        {
            for (int draw = 0; draw < draws; draw++)
            {
                _ = random.Next();
            }
        }

        RetailEventSlotSnapshot[] filed = BattleEngineEvents(state);
        Assert.Equal(
            [RetailBattleEngineRefresh.CrosshairEvent, RetailBattleEngineRefresh.AutoAimEvent],
            filed.Select(slot => (int)slot.EventNum));
        Assert.Equal(BitConverter.SingleToUInt32Bits(crosshairDue), filed[0].TimeBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits(autoAimDue), filed[1].TimeBits);
    }

    [Fact]
    public void Refreshes_KeepExactlyOneOfEachFiledWithinTheirJitterWindows()
    {
        var simulation = new Simulation(Seed, Level100TestActorDefinitions.LoadMaterialized());
        var seenCrosshair = new HashSet<uint>();
        for (int tick = 0; tick < 200; tick++)
        {
            WorldSnapshot state = simulation.Step(SimInput.Idle);
            float now = RetailEventScheduler.TimeAtFrameCount(state.RetailEventFrameCount);
            RetailEventSlotSnapshot[] filed = BattleEngineEvents(state);
            RetailEventSlotSnapshot crosshair = Assert.Single(filed,
                slot => slot.EventNum == RetailBattleEngineRefresh.CrosshairEvent);
            RetailEventSlotSnapshot autoAim = Assert.Single(filed,
                slot => slot.EventNum == RetailBattleEngineRefresh.AutoAimEvent);
            float crosshairDue = BitConverter.UInt32BitsToSingle(crosshair.TimeBits);
            float autoAimDue = BitConverter.UInt32BitsToSingle(autoAim.TimeBits);
            Assert.InRange(crosshairDue, now - 0.051f, now + 0.3f);
            Assert.InRange(autoAimDue, now - 0.051f, now + 0.3f);
            seenCrosshair.Add(crosshair.TimeBits);
        }

        // 200 frames are ten seconds; each delivery re-files 0.1 to 0.3 s later.
        Assert.InRange(seenCrosshair.Count, 34, 101);
    }

    [Fact]
    public void Crosshair_RetainsTheLineReportAndTheUnitUnderIt()
    {
        var simulation = new Simulation(Seed, Level100TestActorDefinitions.LoadMaterialized());
        // The load reports nothing; the pre-run's 6002 deliveries have since
        // reported what lay ahead of the Battle Engine.
        Assert.Equal(Level100CrosshairHitKind.Nothing,
            simulation.LoadSnapshotForMeasurement!.Level100BattleEngineTargeting.CrosshairHitKind);
        WorldSnapshot start = simulation.Snapshot;
        Level100ActorSnapshot tower = start.Level100Actors.Actors.Single(actor =>
            actor.DefinitionName == "Control Tower");
        (int yaw, int pitch) = FacingToward(start, tower.Pose.PositionMillimeters with
        {
            Y = tower.Pose.PositionMillimeters.Y + 3_000,
        });
        simulation.SetFacingForMeasurement(yaw, pitch);

        // The report is retained until the next 6002 delivery, the frame
        // that re-files the crosshair event.
        uint Pending(WorldSnapshot snapshot) => Assert.Single(BattleEngineEvents(snapshot),
            slot => slot.EventNum == RetailBattleEngineRefresh.CrosshairEvent).TimeBits;
        Level100BattleEngineTargetingSnapshot retained = start.Level100BattleEngineTargeting;
        uint pending = Pending(start);
        WorldSnapshot state = start;
        for (int tick = 0; ; tick++)
        {
            Assert.True(tick < 10, "No crosshair refresh within half a second.");
            state = simulation.Step(SimInput.Idle);
            if (Pending(state) != pending)
            {
                break;
            }

            Assert.Equal(retained, state.Level100BattleEngineTargeting);
        }

        Level100BattleEngineTargetingSnapshot targeting = state.Level100BattleEngineTargeting;
        Assert.Equal(Level100CrosshairHitKind.Thing, targeting.CrosshairHitKind);
        Assert.Equal(tower.ActorId, targeting.CrosshairUnit);
        Assert.Equal(tower.ActorId, targeting.CrosshairUnitRegardlessOfRange);
        Assert.InRange(targeting.CrosshairHitDistanceMillimeters, 5_000, 15_000);
    }

    [Fact]
    public void Crosshair_IgnoresFeaturesAsUnitsButLetsThemOccludeTheLine()
    {
        var simulation = new Simulation(Seed, Level100TestActorDefinitions.LoadMaterialized());
        WorldSnapshot start = simulation.Snapshot;
        Level100ActorSnapshot iceberg = start.Level100Actors.Actors.Single(actor =>
            actor.Name == "Iceberg 3");
        (int yaw, int pitch) = FacingToward(start, iceberg.Pose.PositionMillimeters with
        {
            Y = iceberg.Pose.PositionMillimeters.Y + 3_000,
        });
        simulation.SetFacingForMeasurement(yaw, pitch);

        WorldSnapshot state = start;
        for (int tick = 0; tick < 5; tick++)
        {
            state = simulation.Step(SimInput.Idle);
        }

        Level100BattleEngineTargetingSnapshot targeting = state.Level100BattleEngineTargeting;
        Assert.Equal(Level100CrosshairHitKind.Thing, targeting.CrosshairHitKind);
        Assert.Null(targeting.CrosshairUnit);
        Assert.Null(targeting.CrosshairUnitRegardlessOfRange);
    }

    [Fact]
    public void LaunchCorrection_ReusesTheRetainedDistanceAlongTheCurrentViewLine()
    {
        var simulation = new Simulation(Seed, Level100TestActorDefinitions.LoadMaterialized());
        WorldSnapshot start = simulation.Snapshot;
        Level100ActorSnapshot tower = start.Level100Actors.Actors.Single(actor =>
            actor.DefinitionName == "Control Tower");
        (int yaw, int pitch) = FacingToward(start, tower.Pose.PositionMillimeters with
        {
            Y = tower.Pose.PositionMillimeters.Y + 3_000,
        });
        simulation.SetFacingForMeasurement(yaw, pitch);
        WorldSnapshot state = start;
        for (int tick = 0; tick < 5; tick++)
        {
            state = simulation.Step(SimInput.Idle);
        }
        int distance = state.Level100BattleEngineTargeting.CrosshairHitDistanceMillimeters;
        Assert.Equal(Level100CrosshairHitKind.Thing, state.Level100BattleEngineTargeting.CrosshairHitKind);

        // Look straight up without a refresh. A fresh trace would find open
        // sky and keep the facing; retail keeps the retained distance and aims
        // at that point on the new view line, which the emitter sees at an
        // angle because it is offset from the view point.
        const int Up = -1_000_000;
        simulation.SetFacingForMeasurement(yaw, Up);
        var eye = new SimVector3(state.PlayerPosition.X, state.PlayerElevationMillimeters, state.PlayerPosition.Z);
        var emitter = new SimVector3(eye.X + 1_500, eye.Y - 1_000, eye.Z);
        (int launchYaw, int launchPitch) = simulation.LaunchAnglesForMeasurement(emitter);

        double horizontal = Math.Cos(Up / 1e6) * distance;
        double pointX = eye.X - (Math.Sin(yaw / 1e6) * horizontal);
        double pointY = eye.Y - (Math.Sin(Up / 1e6) * distance);
        double pointZ = eye.Z + (Math.Cos(yaw / 1e6) * horizontal);
        double dx = pointX - emitter.X, dy = pointY - emitter.Y, dz = pointZ - emitter.Z;
        int expectedYaw = (int)Math.Round(Math.Atan2(-dx, dz) * 1e6);
        int expectedPitch = (int)Math.Round(Math.Atan2(-dy, Math.Sqrt((dx * dx) + (dz * dz))) * 1e6);
        Assert.InRange(launchYaw - expectedYaw, -2_000, 2_000);
        Assert.InRange(launchPitch - expectedPitch, -2_000, 2_000);
        Assert.True(Math.Abs(launchYaw - yaw) + Math.Abs(launchPitch - Up) > 20_000,
            "The correction should aim at the retained point, not along the facing.");
    }

    private static RetailEventSlotSnapshot[] BattleEngineEvents(WorldSnapshot state)
    {
        RetailEventSchedulerSnapshot events = state.Level100ActorMechanics.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        return events.Lanes
            .SelectMany(lane => lane.Handles)
            .Concat(events.Overflow)
            .Select(handle => slots[handle])
            .Where(slot => slot.Listener == Level100ActorMechanics.BattleEngineListener)
            .ToArray();
    }

    private static (int Yaw, int Pitch) FacingToward(WorldSnapshot state, SimVector3 point)
    {
        double dx = point.X - (double)state.PlayerPosition.X;
        double dy = point.Y - (double)state.PlayerElevationMillimeters;
        double dz = point.Z - (double)state.PlayerPosition.Z;
        return (
            (int)Math.Round(Math.Atan2(-dx, dz) * 1e6),
            (int)Math.Round(Math.Atan2(-dy, Math.Sqrt((dx * dx) + (dz * dz))) * 1e6));
    }
}
