// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Drives Level 100 the way a player does — only through <see cref="SimInput"/>
/// — and pins which released tutorial beats the world can generate on its own.
/// No named mission event is posted by any test here; every beat these tests
/// observe is produced by the simulation.
/// </summary>
public sealed class Level100TutorialProgressionTests
{
    private readonly ITestOutputHelper _output;

    public Level100TutorialProgressionTests(ITestOutputHelper output) =>
        _output = output;

    [Fact]
    public void AuthoredGroundVehicles_AreSeatedOnTheTerrain()
    {
        var simulation = new Simulation(1u, Level100TestActorDefinitions.Create());
        Level100ActorSnapshot[] tanks = simulation.Snapshot.Level100Actors.Actors
            .Where(actor => actor.Name is "Target Tank 2" or "Target Tank 3")
            .OrderBy(actor => actor.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, tanks.Length);
        foreach (Level100ActorSnapshot tank in tanks)
        {
            // Authored Y is 0; the released height field puts the firing-range
            // ground at 600 and the Target Tank class origin sits 100 above it.
            Assert.Equal(700, tank.Pose.PositionMillimeters.Y);
        }
    }

    /// <summary>
    /// The released firing-range exercise, played rather than scripted. The
    /// two authored Target Tanks and the Target Warehouse are reached, aimed
    /// at and destroyed by Pulse Cannon rounds alone, and each Target Tank
    /// takes exactly the four hits the recorded 6 -> 4.2 -> 2.4 -> 0.6 -> -1.2
    /// life sequence predicts.
    /// </summary>
    [Fact]
    public void PulseCannonRun_DestroysEveryAuthoredStaticTarget()
    {
        var driver = Level100PlayerDriver.Create();
        driver.Run(9_000, state => state.Level100Actors.Actors
            .Where(actor => actor.Name is "Target Tank 2" or "Target Tank 3" or "Target Warehouse")
            .All(actor => actor.Lifecycle == Level100ActorLifecycle.Destroyed));
        foreach (string line in driver.Report)
        {
            _output.WriteLine(line);
        }

        WorldSnapshot final = driver.Snapshot;
        foreach (string name in new[] { "Target Tank 2", "Target Tank 3", "Target Warehouse" })
        {
            Level100ActorSnapshot actor = final.Level100Actors.Actors
                .Single(item => item.Name == name);
            Assert.Equal(Level100ActorLifecycle.Destroyed, actor.Lifecycle);
        }

        Assert.Equal(
            4,
            driver.ImpactsAtTerminalByActor[final.Level100Actors.Actors
                .Single(item => item.Name == "Target Tank 2").ActorId.Value]);
        Assert.Equal(
            4,
            driver.ImpactsAtTerminalByActor[final.Level100Actors.Actors
                .Single(item => item.Name == "Target Tank 3").ActorId.Value]);
        foreach (string name in new[] { "Target Tank 2", "Target Tank 3" })
        {
            int id = final.Level100Actors.Actors.Single(actor => actor.Name == name).ActorId.Value;
            // In-flight rounds can still hit during the ground shutdown delay.
            Assert.True(driver.ImpactsByActor[id] > driver.ImpactsAtTerminalByActor[id]);
        }
    }

    /// <summary>
    /// Shooting a Target Truck before the script arms it loses the level the
    /// released way, and the dead truck's script stops existing.
    ///
    /// <para><b>This path had never been exercised, and until now it could
    /// not be.</b> <c>TargetTruckN.msl</c> answers <c>died()</c> with
    /// <c>switch (activated)</c> whose <c>case FALSE</c> arm is
    /// <c>PostEvent("Broke Tutorial")</c>, and LevelScript answers that with
    /// <c>LevelLostString(LOSE_TUTORIAL_BROKE)</c>
    /// (<c>level100/LevelScript.msl:310-315</c>). Core reached the loss and
    /// then threw on the way to it: the destroyed truck's script was still in
    /// the runtime's instance table, so the next
    /// <c>event("Activate Static Targets 2")</c> broadcast also ran the dead
    /// truck's activation handler, which calls <c>SetObjective()</c> on its own
    /// destroyed owner. That is unreachable in retail.</para>
    ///
    /// <para><b>The released rule, cited.</b>
    /// <c>references/Onslaught/thing.cpp:711-724</c>,
    /// <c>CComplexThing::AddShutdownEvent</c>, runs
    /// <c>mMissionScript-&gt;Died()</c> and then <c>delete mMissionScript;
    /// mMissionScript = NULL;</c> on the same call - <c>died()</c> is the last
    /// thing a script ever runs, and every later <c>if (mMissionScript)</c> in
    /// the released thing fails silently.
    /// <c>CBattleEngine::StartDieProcess</c> (BattleEngine.cpp:2617-2640) does
    /// the same for the player. So the guard on
    /// <c>Level100ActorRegistry.SetObjective</c> is not what retail relies on -
    /// <c>CThing::SetObjective</c> (thing.cpp:269-287) has no lifecycle test at
    /// all, it is a flag plus a noticeboard membership - the invariant is
    /// upstream and structural, and that is where Core now enforces it.</para>
    ///
    /// <para>The truck is destroyed by the player's own Pulse Cannon through
    /// <see cref="SimInput"/>, not by a fact or a seam, so this is the released
    /// hazard rather than a synthetic one: <c>Target Truck #25</c> is parked
    /// 1.46 m behind beat 3's <c>Target Tank #23</c> and a round that clears
    /// the tank reaches it.</para>
    /// </summary>
    [Fact]
    public void TargetTruckKilledBeforeActivation_LosesTheLevelAndRetiresItsScript()
    {
        var driver = Level100PlayerDriver.Create();

        // Let the released driver walk itself into the firing range and take
        // delivery of the Pulse Cannon, then close on beat 3's objective - the
        // truck is parked next to it.
        WorldSnapshot state = driver.RunUntil(
            snapshot => snapshot.Level100PulseCannonEnabled &&
                DistanceToTruck(snapshot) < 22_000,
            20_000);
        Assert.True(
            state.Level100PulseCannonEnabled,
            "the driver never took delivery of the Pulse Cannon");

        Level100ActorId truckId = Truck(state).ActorId;
        Assert.False(Truck(state).IsObjective, "the truck is armed already");
        Assert.Contains(
            state.Level100ActorScripts.Instances,
            instance => instance.ActorId == truckId);

        // Hand-aim at the truck and fire until it dies. Nothing but SimInput.
        for (int tick = 0; tick < 9_000; tick++)
        {
            Level100ActorSnapshot truck = Truck(state);
            if (truck.Lifecycle == Level100ActorLifecycle.Destroyed ||
                state.Level100Mission.Outcome != Level100MissionOutcome.Running)
            {
                break;
            }

            SimVector3 aim = truck.Pose.PositionMillimeters;
            double deltaX = (double)aim.X - state.PlayerPosition.X;
            double deltaZ = (double)aim.Z - state.PlayerPosition.Z;
            double horizontal = Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
            double yawError = NormalizeRadians(
                Math.Atan2(-deltaX, deltaZ) - (state.FacingYawMicroRad / 1_000_000d));
            double pitchError =
                -Math.Atan2(
                    aim.Y + 600 - state.PlayerElevationMillimeters,
                    Math.Max(1.0, horizontal)) -
                (state.FacingPitchMicroRad / 1_000_000d);
            // Walk right up to it. From the 18 m stand-off the driver arrives
            // at, the player stands on a ridge 5.2 m above this truck and every
            // round that is on the hull is also in the crest between them -
            // the same stance that put 3,578 rounds into the terrain in front
            // of Target Tank #23. Closing collapses the depression angle.
            double tolerance = Math.Clamp(1_100.0 / Math.Max(horizontal, 1.0), 0.02, 0.15);
            SimActions actions =
                Math.Abs(yawError) < tolerance && Math.Abs(pitchError) < tolerance
                    ? SimActions.Fire
                    : SimActions.None;
            state = driver.Step(new SimInput(
                0,
                horizontal > 4_000 ? (sbyte)1 : (sbyte)0,
                actions,
                0,
                0,
                LookAxisCommand.ForResponsePermille((int)(yawError * 2_000)),
                LookAxisCommand.ForResponsePermille((int)(pitchError * 4_000))));
        }

        _output.WriteLine(
            $"t{state.Tick} truck hp={Truck(state).Health} " +
            $"life={Truck(state).Lifecycle} d={DistanceToTruck(state):F0} " +
            $"player=({state.PlayerPosition.X},{state.PlayerPosition.Z}) " +
            $"elev={state.PlayerElevationMillimeters} " +
            $"truckPos={Truck(state).Pose.PositionMillimeters} " +
            $"outcome={state.Level100Mission.Outcome}");
        Assert.Equal(Level100ActorLifecycle.Destroyed, Truck(state).Lifecycle);

        // The released teardown: `died()` ran - it is what posts
        // `Broke Tutorial` - and the script is gone with the same call.
        Assert.DoesNotContain(
            state.Level100ActorScripts.Instances,
            instance => instance.ActorId == truckId);

        // And the level is lost the released way rather than by an exception.
        // The loss is not instantaneous: LevelScript's handler plays
        // TUTORIAL_BROKE_1 and then a blocking TUTORIAL_BROKE_2 before it calls
        // LevelLostString, so the outcome arrives some ticks later.
        for (int tick = 0;
             tick < 9_000 &&
                 state.Level100Mission.Outcome == Level100MissionOutcome.Running;
             tick++)
        {
            state = driver.Step(SimInput.Idle);
        }

        Assert.Equal(Level100MissionOutcome.Lost, state.Level100Mission.Outcome);
        Assert.Equal(
            Level100MissionFailureReason.TutorialBroken,
            state.Level100Mission.FailureReason);
    }

    private static Level100ActorSnapshot Truck(WorldSnapshot state) =>
        state.Level100Actors.Actors.Single(actor => actor.Name == "Target Truck #25");

    private static double DistanceToTruck(WorldSnapshot state)
    {
        SimVector3 position = Truck(state).Pose.PositionMillimeters;
        double deltaX = (double)position.X - state.PlayerPosition.X;
        double deltaZ = (double)position.Z - state.PlayerPosition.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private static double NormalizeRadians(double value)
    {
        while (value > Math.PI)
        {
            value -= 2 * Math.PI;
        }

        while (value < -Math.PI)
        {
            value += 2 * Math.PI;
        }

        return value;
    }

    /// <summary>
    /// The Twin Vulcan's Mech Bullet against the released Target Tank life,
    /// which the second firing-range exercise depends on. The round does its
    /// direct 0.08 only: its hit explosion's 0.001 is under
    /// <c>CExplosion::Init</c>'s collision gate (see
    /// <see cref="Level100DestructionState.MechBulletDamageBits"/>).
    /// </summary>
    [Fact]
    public void MechBullet_DestroysATargetTankInSeventySixHits()
    {
        Assert.Equal(0x3DA3D70Au, Level100DestructionState.MechBulletDamageBits);
        Assert.Equal(76, HitsToDestroyATargetTank(Level100DestructionState.MechBulletDamageBits));
    }

    /// <summary>
    /// Beat 4 ("Static Target 2 Destroyed" x3) needs a Target Truck that can
    /// actually be hit. This drives the released mechanism end to end at the
    /// runtime level: the truck is created by the same
    /// <see cref="Level100ActorRegistry.SpawnThing"/> call
    /// <c>TargetTruck1.msl</c> <c>init()</c> makes, and is then destroyed by
    /// Mech Bullet sweeps issued through the same
    /// <see cref="Level100DestructionRuntime.TryApplyRoundSweep"/> entry point
    /// <c>Simulation.LaunchWalkerRound</c> uses for the Twin Vulcan. No
    /// mission event is posted and no destruction state is constructed
    /// directly; the registry lifecycle facts are the observed result.
    ///
    /// Before the contact asset carried a `Target Truck` definition this test
    /// could not report a single hit: the runtime skipped the actor entirely.
    /// </summary>
    [Fact]
    public void TwinVulcanRounds_DestroyASpawnedTargetTruck()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        Level100ActorId factoryId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Tank Factory"));

        Level100ActorId truckId = registry.SpawnThing(
            factoryId,
            "Target Truck",
            "SpawnerA",
            1,
            "TargetTruck1").Single();
        Level100ActorSnapshot spawned = registry.GetActor(truckId);
        Assert.Equal("Target Truck", spawned.DefinitionName);
        Assert.Equal("m_f_truck_training.msh.aya", spawned.MeshBinding);
        // 3.0 life from `Unit / Target Truck` field 3 (0x40400000).
        Assert.Equal(3_000, spawned.Health);

        // Park the truck clear of the terrain so the measurement is of the
        // actor narrowphase and nothing else, then fire straight down through
        // it. Core's vertical axis is up.
        var pose = new Level100ActorPoseSnapshot(
            new SimVector3(4_000, 6_000, -3_000),
            IdentityFloatBasis(),
            SimVector3.Zero,
            SimVector3.Zero);
        registry.SetPose(truckId, pose);

        var start = new SimVector3(4_000, 9_000, -3_000);
        var end = new SimVector3(4_000, 6_100, -3_000);
        int hits = 0;
        while (registry.GetActor(truckId).Lifecycle == Level100ActorLifecycle.Alive &&
               hits < 1_000)
        {
            Assert.True(runtime.TryApplyRoundSweep(
                start,
                end,
                Level100ContactMechanics.PulseRadiusMillimeters,
                Level100DestructionState.MechBulletDamageBits,
                Level100DestructionEffectKind.VulcanImpact,
                out Level100ContactHit hit));
            Assert.Equal(truckId.Value, hit.ActorId);
            Assert.Equal(Level100ContactSurfaceKind.Mesh, hit.SurfaceKind);
            Assert.Equal(0, hit.PartIndex);
            hits++;
        }

        // 3.0 life against the 0.08 Mech Bullet round is 37.5 rounds, so the
        // thirty-eighth carries it terminal.
        Assert.Equal(38, hits);
        Assert.True(registry.GetActor(truckId).Active);
        Assert.Equal(Level100ActorLifecycle.DiedAwaitingShutdown, registry.GetActor(truckId).Lifecycle);
        runtime.FlushStartOfFrame(Assert.Single(runtime.Snapshot.PendingShutdowns).DeliveryFrame);
        Level100ActorSnapshot destroyed = registry.GetActor(truckId);
        Assert.Equal(Level100ActorLifecycle.Destroyed, destroyed.Lifecycle);
        Assert.Equal(0, destroyed.Health);
        Assert.False(destroyed.Active);
        Assert.Contains(
            registry.Snapshot.PendingFacts,
            fact => fact.Kind == Level100ActorFactKind.Died &&
                fact.ActorId == truckId);
        Assert.Contains(
            runtime.Events,
            item => item.ActorId == truckId.Value &&
                item.Kind == Level100DestructionEventKind.Terminal &&
                item.EffectKind == Level100DestructionEffectKind.TargetDestroyed);
    }

    /// <summary>
    /// Beat 4's Target Truck takes 38 Mech Bullet rounds at the round's direct
    /// 0.08.
    /// </summary>
    [Fact]
    public void TargetTruck_TakesThirtyEightMechBulletRounds()
    {
        const uint damageBits = Level100DestructionState.MechBulletDamageBits;
        const int expectedHits = 38;
        var state = new Level100DestructionState(
            1,
            Level100ContactCatalog.Instance.GetDefinition("Target Truck"));
        Span<Level100DestructionEvent> events =
            stackalloc Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        var hit = new Level100ContactHit(
            1,
            0,
            Level100ContactSurfaceKind.Mesh,
            0,
            default,
            default,
            default);

        int hits = 0;
        while (!state.Terminal && hits < 10_000)
        {
            state.ApplyRoundHit(
                hit,
                damageBits,
                Level100DestructionEffectKind.VulcanImpact,
                events);
            hits++;
        }

        Assert.Equal(expectedHits, hits);
    }

    /// <summary>
    /// Beat 7's mechanism, end to end at runtime level and with no mission
    /// event posted. The drone is created by exactly the
    /// <see cref="Level100ActorRegistry.SpawnThing"/> call
    /// <c>Hangar.msl</c>'s <c>"Activate Airborne Targets 1"</c> makes, then
    /// commanded with exactly the <c>FollowWaypoint("Drone Path 1", 0)</c> that
    /// <c>AirborneDrone1.msl</c>'s <c>ready()</c> issues, and then destroyed
    /// through the same <c>TryApplyRoundSweep</c> entry point
    /// <c>Simulation.LaunchWalkerRound</c> uses.
    ///
    /// Before this change the drone had no contact geometry, no life and no
    /// motion class: it spawned at the airfield emitter and sat there, and no
    /// weapon at any range could touch it.
    /// </summary>
    [Fact]
    public void TargetDrone_FliesDronePath1AndIsDestroyedByRounds()
    {
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var runtime = new Level100DestructionRuntime(registry);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));

        Level100ActorId droneId = registry.SpawnThing(
            airfieldId,
            "Target Drone",
            "SpawnerB",
            1,
            "AirborneDrone1").Single();
        mechanics.RegisterSpawnedActor(droneId);
        runtime.RegisterActor(droneId);
        Level100ActorSnapshot spawned = registry.GetActor(droneId);
        Assert.Equal("Target Drone", spawned.DefinitionName);
        Assert.Equal("m_FA_F24_training.msh.aya", spawned.MeshBinding);
        // 1.0 life from `Unit / Target Drone` field 3 (0x3F800000). The
        // materialized spawn row carries 0; Core takes the released record.
        Assert.Equal(
            SimulationConstants.Level100TargetDroneLife,
            spawned.Health);

        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            droneId,
            Level100ActorScriptCommandKind.FollowWaypoint,
            null,
            "Drone Path 1",
            0));

        SimVector3 origin = registry.GetPose(droneId).PositionMillimeters;
        var track = new List<(int Tick, SimVector3 Position, int Clearance)>();
        int baseTicks = 0;
        for (int tick = 0; tick < 30 * 40; tick++)
        {
            mechanics.AdvanceTick();
            Level100ActorPoseSnapshot pose = registry.GetPose(droneId);
            if (pose.LinearVelocityMillimetersPerTick != SimVector3.Zero)
            {
                baseTicks++;
            }
            if (tick % 150 == 0)
            {
                int ground = Level100Terrain.Instance
                    .SampleGroundElevationMillimeters(new SimVector2(
                        pose.PositionMillimeters.X,
                        pose.PositionMillimeters.Z));
                track.Add((
                    tick,
                    pose.PositionMillimeters,
                    pose.PositionMillimeters.Y - ground));
            }
        }
        foreach ((int Tick, SimVector3 Position, int Clearance) row in track)
        {
            _output.WriteLine(
                $"t{row.Tick} pos={row.Position} clearance={row.Clearance}");
        }

        SimVector3 flown = registry.GetPose(droneId).PositionMillimeters;
        Assert.NotEqual(origin, flown);

        // Travel per released base tick is CUnitAirVelocity * stored0.05 -
        // 5.5 u/s is 275 mm. The measurement is taken over a straight stretch
        // so the turn does not shorten it.
        int[] speeds = new int[20];
        for (int index = 0; index < speeds.Length; index++)
        {
            SimVector3 before = registry.GetPose(droneId).PositionMillimeters;
            do
            {
                mechanics.AdvanceTick();
            }
            while (registry.GetPose(droneId)
                .LinearVelocityMillimetersPerTick == SimVector3.Zero);
            SimVector3 after = registry.GetPose(droneId).PositionMillimeters;
            long deltaX = (long)after.X - before.X;
            long deltaY = (long)after.Y - before.Y;
            long deltaZ = (long)after.Z - before.Z;
            speeds[index] = (int)Math.Round(Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ)));
        }
        _output.WriteLine(
            "per-base-tick speeds: " + string.Join(", ", speeds));
        Assert.All(speeds, speed => Assert.InRange(speed, 273, 277));

        // The released clearance band (climb below 5 units, dive above 50)
        // holds the drone off the terrain. Nothing else in this reconstruction
        // owns its altitude.
        Level100ActorPoseSnapshot cruise = registry.GetPose(droneId);
        int cruiseGround = Level100Terrain.Instance
            .SampleGroundElevationMillimeters(new SimVector2(
                cruise.PositionMillimeters.X,
                cruise.PositionMillimeters.Z));
        Assert.InRange(
            cruise.PositionMillimeters.Y - cruiseGround,
            SimulationConstants.Level100PlaneClimbClearanceMillimeters,
            SimulationConstants.Level100PlaneDiveClearanceMillimeters);

        // Now shoot it down where it flies.
        SimVector3 target = registry.GetPose(droneId).PositionMillimeters;
        var start = new SimVector3(target.X, target.Y + 4_000, target.Z);
        var end = new SimVector3(target.X, target.Y + 100, target.Z);
        int hits = 0;
        while (registry.GetActor(droneId).Lifecycle !=
                   Level100ActorLifecycle.Destroyed &&
               hits < 1_000)
        {
            Assert.True(runtime.TryApplyRoundSweep(
                start,
                end,
                Level100ContactMechanics.PulseRadiusMillimeters,
                Level100DestructionState.MechBulletDamageBits,
                Level100DestructionEffectKind.VulcanImpact,
                out Level100ContactHit hit));
            Assert.Equal(droneId.Value, hit.ActorId);
            Assert.Equal(Level100ContactSurfaceKind.Mesh, hit.SurfaceKind);
            Assert.Equal(0, hit.PartIndex);
            hits++;
        }

        // 1.0 life against the 0.08 Mech Bullet round is 12.5 rounds, so the
        // thirteenth carries it terminal.
        _output.WriteLine($"mech bullet hits to destroy the drone: {hits}");
        Assert.Equal(13, hits);
        Level100ActorSnapshot destroyed = registry.GetActor(droneId);
        Assert.Equal(Level100ActorLifecycle.Destroyed, destroyed.Lifecycle);
        Assert.Equal(0, destroyed.Health);
        Assert.Contains(
            registry.Snapshot.PendingFacts,
            fact => fact.Kind == Level100ActorFactKind.Died &&
                fact.ActorId == droneId);
        Assert.Contains(
            runtime.Events,
            item => item.ActorId == droneId.Value &&
                item.Kind == Level100DestructionEventKind.Terminal &&
                item.EffectKind == Level100DestructionEffectKind.DroneDestroyed);
    }

    /// <summary>
    /// Beat 9's mechanism: <c>AirborneDrone2.msl</c>'s <c>init()</c> does
    /// <c>Attack(player)</c>, and the released air guide steers at whatever the
    /// guide target holds. The drone must close on the player, not sit still -
    /// which is what it did before this change, because
    /// <c>Level100ActorMechanics</c> implemented no plane motion at all and
    /// <c>BeginAttack</c> only set an intent and zeroed the velocity.
    /// </summary>
    [Fact]
    public void AttackingTargetDrone_ClosesOnThePlayer()
    {
        Level100ActorDefinitionSet definitions =
            Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);
        Level100ActorId airfieldId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));
        Level100ActorId playerId = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Player 1"));

        Level100ActorId droneId = registry.SpawnThing(
            airfieldId,
            "Target Drone",
            "SpawnerA",
            1,
            "AirborneDrone2").Single();
        mechanics.RegisterSpawnedActor(droneId);
        mechanics.ApplyCommand(new Level100ActorScriptCommand(
            1,
            0,
            droneId,
            Level100ActorScriptCommandKind.Attack,
            playerId,
            null,
            0));

        SimVector2 player = new(
            registry.GetPose(playerId).PositionMillimeters.X,
            registry.GetPose(playerId).PositionMillimeters.Z);
        double before = HorizontalDistance(
            registry.GetPose(droneId).PositionMillimeters,
            player);
        double closest = before;
        for (int tick = 0; tick < 30 * 60; tick++)
        {
            mechanics.AdvanceTick();
            closest = Math.Min(
                closest,
                HorizontalDistance(
                    registry.GetPose(droneId).PositionMillimeters,
                    player));
        }
        _output.WriteLine(
            $"attacking drone: {before:F0} mm out at spawn, closed to " +
            $"{closest:F0} mm");
        Assert.True(before > 40_000);
        // Inside the released `Drone Vulcan Cannon` CWeaponMaxRange of 40.0
        // units (0x42200000). Nothing here fires it; this only shows the
        // pursuit reaches weapon range.
        Assert.True(closest < 40_000);
    }

    private static double HorizontalDistance(SimVector3 position, SimVector2 target)
    {
        double deltaX = (double)position.X - target.X;
        double deltaZ = (double)position.Z - target.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private static Level100FloatBasis3Bits IdentityFloatBasis() => new(
        BitConverter.SingleToInt32Bits(1f), 0, 0,
        0, BitConverter.SingleToInt32Bits(1f), 0,
        0, 0, BitConverter.SingleToInt32Bits(1f));

    [Fact]
    public void PulseCannonRound_StillTakesFourHitsToDestroyATargetTank()
    {
        Assert.Equal(
            4,
            HitsToDestroyATargetTank(Level100DestructionState.PulseDamageBits));
    }

    private static int HitsToDestroyATargetTank(uint damageBits)
    {
        var state = new Level100DestructionState(
            1,
            Level100ContactCatalog.Instance.GetDefinition("Target Tank"));
        Span<Level100DestructionEvent> events =
            stackalloc Level100DestructionEvent[Level100DestructionState.MaximumEventsPerHit];
        var hit = new Level100ContactHit(
            1,
            0,
            Level100ContactSurfaceKind.Mesh,
            0,
            default,
            default,
            default);

        int hits = 0;
        while (!state.Terminal && hits < 10_000)
        {
            state.ApplyRoundHit(
                hit,
                damageBits,
                Level100DestructionEffectKind.VulcanImpact,
                events);
            hits++;
        }

        return hits;
    }
}

/// <summary>
/// A minimal deterministic autopilot. It reads only what the HUD shows a
/// player — the current objective actor and its position — and answers with
/// look, move and fire. It never posts a mission event and never writes world
/// state.
/// </summary>
internal sealed class Level100PlayerDriver
{
    private readonly Simulation _simulation;
    private readonly List<string> _events = [];
    private readonly SortedDictionary<int, int> _impactsByActor = [];
    private readonly SortedDictionary<int, int> _impactsAtTerminalByActor = [];

    private Level100PlayerDriver(Simulation simulation) => _simulation = simulation;

    internal IReadOnlyList<string> Report => _events;

    internal IReadOnlyDictionary<int, int> ImpactsByActor => _impactsByActor;

    internal IReadOnlyDictionary<int, int> ImpactsAtTerminalByActor => _impactsAtTerminalByActor;

    internal WorldSnapshot Snapshot => _simulation.Snapshot;

    internal static Level100PlayerDriver Create()
    {
        var simulation = new Simulation(
            1u,
            Level100TestActorDefinitions.Create(),
            new Level100TutorialProgress(true, true, true, true));
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        return new Level100PlayerDriver(simulation);
    }

    /// <summary>
    /// Drives this driver's own input until <paramref name="stop"/> is
    /// satisfied. Still input-only: it changes nothing about what the driver
    /// commands, it only decides when the caller takes over.
    /// </summary>
    internal WorldSnapshot RunUntil(Func<WorldSnapshot, bool> stop, int maximumTicks)
    {
        for (int tick = 0; tick < maximumTicks; tick++)
        {
            WorldSnapshot state = _simulation.Snapshot;
            if (stop(state) ||
                state.Level100Mission.Outcome != Level100MissionOutcome.Running)
            {
                return state;
            }

            _simulation.Step(NextInput(state));
        }

        return _simulation.Snapshot;
    }

    /// <summary>One player-input tick, for a caller driving by hand.</summary>
    internal WorldSnapshot Step(SimInput input) => _simulation.Step(input);

    internal void Run(int maximumTicks, Func<WorldSnapshot, bool>? stopWhen = null)
    {
        string? lastNavigation = null;
        int lastDestroyed = -1;
        for (int tick = 0; tick < maximumTicks; tick++)
        {
            WorldSnapshot state = _simulation.Snapshot;
            if (stopWhen?.Invoke(state) == true) break;
            if (state.Level100Mission.Outcome != Level100MissionOutcome.Running)
            {
                _events.Add($"t{state.Tick} outcome {state.Level100Mission.Outcome}");
                return;
            }

            string? navigation = state.Level100Mission.NavigationObjective;
            if (navigation != lastNavigation)
            {
                _events.Add($"t{state.Tick} navigation -> {navigation ?? "(none)"}");
                lastNavigation = navigation;
            }

            int destroyed = state.Level100Actors.Actors.Count(
                actor => actor.Lifecycle == Level100ActorLifecycle.Destroyed);
            if (destroyed != lastDestroyed)
            {
                _events.Add($"t{state.Tick} destroyed actors = {destroyed}");
                lastDestroyed = destroyed;
            }

            WorldSnapshot next = _simulation.Step(NextInput(state));
            foreach (Level100DestructionEvent destruction in next.Level100DestructionEvents)
            {
                if (destruction.Kind == Level100DestructionEventKind.Terminal)
                    _impactsAtTerminalByActor.Add(destruction.ActorId, _impactsByActor[destruction.ActorId]);
                if (destruction.Kind is not (
                        Level100DestructionEventKind.PulseImpact or
                        Level100DestructionEventKind.VulcanImpact))
                {
                    continue;
                }
                _impactsByActor.TryGetValue(destruction.ActorId, out int count);
                _impactsByActor[destruction.ActorId] = count + 1;
            }
        }

        WorldSnapshot final = _simulation.Snapshot;
        _events.Add(
            $"t{final.Tick} stopped, outcome {final.Level100Mission.Outcome}, " +
            $"pulse={final.Level100PulseCannonEnabled} " +
            $"twinVulcan={final.Level100VulcanCannonEnabled}");
        _events.Add(
            "impactsByActor=" +
            string.Join(",", _impactsByActor.Select(pair => $"{pair.Key}:{pair.Value}")));
        foreach (Level100ActorSnapshot actor in final.Level100Actors.Actors
            .Where(actor => actor.TargetGroup != Level100MissionTargetGroup.None)
            .OrderBy(actor => actor.ActorId.Value))
        {
            _events.Add(
                $"  actor {actor.ActorId.Value} {actor.Name} def={actor.DefinitionName} " +
                $"grp={actor.TargetGroup} obj={actor.IsObjective} active={actor.Active} " +
                $"life={actor.Lifecycle} hp={actor.Health}");
        }
    }

    private static SimInput NextInput(WorldSnapshot state)
    {
        if (!state.Level100PlayerControlEnabled)
        {
            return SimInput.Idle;
        }

        Level100ActorSnapshot? target = SelectTarget(state);
        if (target is null)
        {
            return SimInput.Idle;
        }

        // A player leads a moving target. The released Pulse Cannon round
        // travels ProjectileSpeedPerTick, so the driver aims where the target
        // will be when the round arrives; without this it lands behind every
        // moving Target Tank and Target Truck and beats 3, 4 and 5 stall on
        // pure aim error rather than on any missing mechanism. The target's
        // reported velocity is public snapshot state a player can see.
        double rawX = (double)target.Pose.PositionMillimeters.X - state.PlayerPosition.X;
        double rawZ = (double)target.Pose.PositionMillimeters.Z - state.PlayerPosition.Z;
        double flightTicks =
            Math.Sqrt((rawX * rawX) + (rawZ * rawZ)) /
            SimulationConstants.ProjectileSpeedPerTick;
        long deltaX = (long)(rawX +
            (target.Pose.LinearVelocityMillimetersPerTick.X * flightTicks));
        long deltaZ = (long)(rawZ +
            (target.Pose.LinearVelocityMillimetersPerTick.Z * flightTicks));
        double yawError = NormalizeRadians(
            Math.Atan2(-deltaX, deltaZ) - (state.FacingYawMicroRad / 1_000_000d));
        // Rate in, stick position out. Retail curves the look axis
        // (Player.cpp:334-355, ported to LookAxisResponse) and the curve is
        // compressive, so a controller that hands its desired rate straight to
        // SimInput gets 0.4665 of it near centre and never converges: measured,
        // this driver put 386 rounds into the terrain over 900 released seconds
        // and left every Target Tank on full health. See LookAxisCommand.
        short lookX = LookAxisCommand.ForResponsePermille((int)(yawError * 2_000));
        double horizontal = Math.Sqrt((double)((deltaX * deltaX) + (deltaZ * deltaZ)));

        // A player sweeps the target's body rather than shooting at its pivot.
        // Scanning the vertical extent keeps "the world cannot be hit" from
        // being confused with "the driver aimed at the ground".
        int aimHeight = 200 + (((state.Tick / 11) % 12) * 200);
        double pitchError =
            -Math.Atan2(
                target.Pose.PositionMillimeters.Y + aimHeight -
                    state.PlayerElevationMillimeters,
                horizontal) -
            (state.FacingPitchMicroRad / 1_000_000d);
        short lookY = LookAxisCommand.ForResponsePermille((int)(pitchError * 4_000));

        if (target.Trigger.HasValue)
        {
            return new SimInput(
                0,
                horizontal > 1_500 ? (sbyte)1 : (sbyte)0,
                SimActions.None,
                0,
                0,
                lookX,
                lookY);
        }

        SimActions actions = Math.Abs(yawError) < 0.02 && Math.Abs(pitchError) < 0.02
            ? SimActions.Fire
            : SimActions.None;
        return new SimInput(
            0,
            horizontal > 18_000 ? (sbyte)1 : (sbyte)0,
            actions,
            0,
            0,
            lookX,
            lookY);
    }

    private static Level100ActorSnapshot? SelectTarget(WorldSnapshot state) =>
        state.Level100Actors.Actors
            .Where(actor =>
                actor.IsObjective &&
                actor.Active &&
                actor.Lifecycle == Level100ActorLifecycle.Alive)
            .OrderBy(actor => Distance(state, actor))
            .FirstOrDefault();

    private static double Distance(WorldSnapshot state, Level100ActorSnapshot actor)
    {
        double deltaX = (double)actor.Pose.PositionMillimeters.X - state.PlayerPosition.X;
        double deltaZ = (double)actor.Pose.PositionMillimeters.Z - state.PlayerPosition.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private static double NormalizeRadians(double value)
    {
        while (value > Math.PI)
        {
            value -= 2 * Math.PI;
        }
        while (value < -Math.PI)
        {
            value += 2 * Math.PI;
        }
        return value;
    }
}
