// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.Client;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class SimulationTests
{
    private static readonly Level100TutorialProgress CompletedTutorialSlots =
        new(Introduction: true, PulseCannon: true, VulcanCannon: true, StatusBars: true);

    [Fact]
    public void Constructor_RejectsZeroSeed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Simulation(0, Level100TestActorDefinitions.Create()));
    }

    /// <summary>
    /// A world's session starts at its Start row, so a definition set without
    /// one is refused before anything is constructed.
    /// </summary>
    [Fact]
    public void Constructor_RejectsAWorldWithoutItsStart()
    {
        Level100ActorDefinitionSet world = Level100TestActorDefinitions.LoadMaterializedWorld110();
        var withoutStart = new Level100ActorDefinitionSet(
            world.Actors.Where(actor => actor.Name != "Player 1")
                .Select((actor, order) => actor with { AuthoredOrder = order }), world.Spawns, world.WaypointPaths,
            world.MotionDefinitions, worldNumber: 110, baseWorldPineCount: world.BaseWorldPineCount,
            squads: world.Squads, components: world.Components);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            new Simulation(1, withoutStart, worldNumber: 110));
        Assert.Contains("no Start", error.Message);
    }

    /// <summary>
    /// The living Plane's raw state, guide and callbacks select schema 47;
    /// the Battle Engine's retained crosshair report then selects 49, and
    /// the unit callbacks of a world built in the retail load order 52.
    /// Legacy mission-only envelopes still select schema 43 and bind all
    /// ten secondary slots; changing the native-88 text dword must therefore
    /// change the world hash even when every older field is equal.
    /// </summary>
    [Fact]
    public void CanonicalHash_BindsRawPlaneStateAndWorld110SecondaryState()
    {
        var root = new Simulation(
            1,
            Level100TestActorDefinitions.Create(),
            CompletedTutorialSlots);
        WorldSnapshot rootState = root.Snapshot;
        for (int tick = 0; tick < 40; tick++)
        {
            rootState = root.Step(new SimInput(0, 1));
        }

        var repeat = new Simulation(1, Level100TestActorDefinitions.Create(), CompletedTutorialSlots);
        for (int tick = 0; tick < 40; tick++) repeat.Step(new SimInput(0, 1));
        string hash = StateHasher.ComputeHex(rootState);
        Assert.Equal(hash, StateHasher.ComputeHex(repeat.Snapshot));
        // Mount admission changes only definition identity. Keep the prior
        // format-7 run and the older format-6 fingerprint as independent checks.
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var priorDefinitions = new Level100ActorDefinitionSet(definitions.Actors,
            definitions.Spawns, definitions.WaypointPaths,
            definitions.MotionDefinitions.Select(definition => definition with { WeaponMounts = null }));
        var legacyDefinitions = new Level100ActorDefinitionSet(definitions.Actors,
            definitions.Spawns.Select(spawn => spawn with { SpawnerExitWaypoints = null }),
            definitions.WaypointPaths, priorDefinitions.MotionDefinitions);
        var prior = new Simulation(1, priorDefinitions, CompletedTutorialSlots);
        for (int tick = 0; tick < 40; tick++) prior.Step(new SimInput(0, 1));
        WorldSnapshot priorIdentityOnly = rootState with
        {
            Level100Actors = rootState.Level100Actors with
            { DefinitionSetIdentitySha256 = priorDefinitions.IdentitySha256 },
        };
        Assert.Equal(StateHasher.GetCanonicalBytes(prior.Snapshot), StateHasher.GetCanonicalBytes(priorIdentityOnly));
        Assert.Equal("9267f6a7d83403582b2216c9881b1274916706f7fae7478d4d3aeb65f6140967",
            StateHasher.ComputeHex(priorIdentityOnly));
        Assert.Equal("eaadf29f32ea0b4c02f6d11a648e030558a8830a39a160c63ef7268fd2732ec0",
            StateHasher.ComputeHex(rootState with { Level100Actors = rootState.Level100Actors with
                { DefinitionSetIdentitySha256 = legacyDefinitions.IdentitySha256 } }));
        Assert.True(hash == "6fb8f61179c0844b685e019f29eaff19a17b5ea99c62839ae7f044c3d525ff23",
            $"Canonical state hash: {hash}");
        Assert.Equal(52, CanonicalSchemaVersion(rootState));

        var actors = new Level100ActorRegistry(RetailWorld110AdmissionTests.CreateWorld110Definitions());
        var world110 = new Level100Mission(actors, actors.GetThingRef("Player 1")!.Value,
            worldNumber: Level100MissionProgram.WorldNumber110);
        Level100MissionSnapshot mission = world110.Snapshot;
        // A synthetic hash envelope around the real mission-program result.
        // No World110 Simulation or actor lifecycle is executed by this check.
        WorldSnapshot state = Level100TestActorDefinitions.LegacyHashEnvelope(rootState) with
        {
            Level100Mission = mission,
            RetailEventFrameCount = 0,
        };
        RetailSecondaryObjectiveSnapshot[] changedObjectives =
            mission.SecondaryObjectives
                .Select((item, index) => index == 1
                    ? item with { TextId = item.TextId + 1 }
                    : item)
                .ToArray();
        WorldSnapshot changed = state with
        {
            Level100Mission = mission with
            {
                SecondaryObjectives = changedObjectives,
            },
        };

        Assert.Equal(43, CanonicalSchemaVersion(state));
        Assert.NotEqual(StateHasher.ComputeHex(state), StateHasher.ComputeHex(changed));
    }

    private static int CanonicalSchemaVersion(WorldSnapshot state)
    {
        byte[] canonical = StateHasher.GetCanonicalBytes(state);
        const int magicLength = 23; // "ONSLAUGHT-REBUILD-STATE"
        return System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(
            canonical.AsSpan(magicLength, sizeof(int)));
    }

    [Fact]
    public void RetailEventClock_UsesStoredFloatMultiplierAndRestartsWithoutRewindingReplay()
    {
        // Construction includes the 3.0 s pre-run: frames 1-60.
        var simulation = new Simulation(
            1, Level100TestActorDefinitions.Create(), CompletedTutorialSlots);
        Assert.Equal(60u, simulation.Snapshot.RetailEventFrameCount);
        Assert.Equal(60, simulation.Snapshot.Level100Mission.Tick);
        Assert.Equal(0x40400000u, BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));

        for (int tick = 0; tick < 2; tick++)
            simulation.Step(SimInput.Idle);

        // Pristine 0x0044B624/0x0044B62A/0x0044B630: zero-extended integer
        // FILD, multiply stored 0x3D4CCCCD, one FSTP dword. Division by 20
        // instead produces 0x40466666 at frame 62, and repeated addition
        // 0x4046665F.
        Assert.Equal(62u, simulation.Snapshot.RetailEventFrameCount);
        Assert.Equal(0x40466667u,
            BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));

        WorldSnapshot reset = simulation.Step(new SimInput(0, 0, SimActions.Reset));
        Assert.Equal(3, reset.Tick);
        Assert.Equal(60, reset.Level100Mission.Tick);
        Assert.Equal(60u, reset.RetailEventFrameCount);
        Assert.Equal(0x40400000u, BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));

        for (int tick = 0; tick < 2; tick++)
            simulation.Step(SimInput.Idle);
        Assert.Equal(5, simulation.Snapshot.Tick);
        Assert.Equal(62u, simulation.Snapshot.RetailEventFrameCount);
        Assert.Equal(0x40466667u,
            BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));
    }

    /// <summary>
    /// <c>CGame::PreRun</c> (<c>game.cpp:2063-2071</c>): the load files
    /// FINISHED_PRE_RUN at now + 3.0, and whole updates run unrendered until
    /// frame 60's flush delivers it and starts the pan, which FINISHED_PANNING
    /// ends on frame 180 (the RE lane's construction-order contract, "Pre-run,
    /// pan and the first rendered frame"). Tick 0 is frame 60.
    /// </summary>
    [Fact]
    public void Construction_RunsTheThreeSecondPreRunBeforeThePan()
    {
        var simulation = new Simulation(1, Level100TestActorDefinitions.LoadMaterialized());
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        WorldSnapshot start = simulation.Snapshot;
        Assert.Equal(0u, load.RetailEventFrameCount);
        Assert.Equal(0, load.Level100Mission.Tick);

        // Sixty whole frames: both clocks, and the world's draws.
        Assert.Equal(0, start.Tick);
        Assert.Equal(60u, start.RetailEventFrameCount);
        Assert.Equal(60, start.Level100Mission.Tick);
        Assert.Equal(0x40400000u, BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));
        Assert.NotEqual(load.Level100ActorMechanics.ReleasedRandomSeed, start.Level100ActorMechanics.ReleasedRandomSeed);

        // The pan starts with the pre-run's end and runs its full six seconds.
        Assert.Equal(SimulationConstants.Level100OpeningPanTicks, start.Level100OpeningTicksRemaining);
        Assert.Equal(181, start.Level100Mission.MessageBoxAllowedTick);
        WorldSnapshot state = start;
        for (int tick = 1; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            state = simulation.Step(SimInput.Idle);
            Assert.True(state.Level100OpeningTicksRemaining > 0);
        }

        state = simulation.Step(SimInput.Idle);
        Assert.Equal(0, state.Level100OpeningTicksRemaining);
        Assert.Equal(180u, state.RetailEventFrameCount);

        // What the load and the pre-run reported reaches the first tick: the
        // cold career's LevelScript deactivated the player.
        Assert.Contains(start.Level100MissionEvents, item => item is Level100PlayerActivationChanged { Active: false });
    }

    /// <summary>
    /// Scripts start on their INIT_SCRIPT events (the RE lane's construction
    /// contract, "Scripts in the first frames"): each scripted row files its
    /// 2001 first in its construction sequence, and the LevelScript and Setup
    /// carriers file theirs at rows 5 and 17, so the first flush runs them in
    /// row order. Setup's SetScript bindings run a frame later, which is when
    /// the Tank Factory spawns its first Target Tank.
    /// </summary>
    [Fact]
    public void Construction_StartsEveryScriptOnItsInitScriptEvent()
    {
        var simulation = new Simulation(1, Level100TestActorDefinitions.LoadMaterialized());
        WorldSnapshot load = simulation.LoadSnapshotForMeasurement!;
        RetailEventSchedulerSnapshot events = load.Level100ActorMechanics.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        Dictionary<Level100ActorId, Level100ActorSnapshot> actors =
            load.Level100Actors.Actors.ToDictionary(actor => actor.ActorId);
        string[] inits = events.Lanes.Single(lane => lane.LaneIndex == events.CurrentBufferNum * RetailEventScheduler.PriorityLanes)
            .Handles.Select(handle => slots[handle])
            .Where(slot => slot.EventNum == Level100ActorMechanics.InitScriptEvent &&
                Level100ActorMechanics.IsScriptListener(slot.Listener))
            .Select(slot => Level100ActorMechanics.IsCarrierScriptListener(slot.Listener)
                ? $"row {Level100ActorMechanics.CarrierScriptRow(slot.Listener)}"
                : actors[Level100ActorMechanics.ScriptListenerActor(slot.Listener)].DefinitionIdentity)
            .ToArray();
        Assert.Equal(
        [
            "row 5", "wres:rlwd:0009", "wres:rlwd:0011", "wres:rlwd:0012", "wres:rlwd:0013",
            "wres:rlwd:0014", "wres:rlwd:0015", "wres:rlwd:0016", "row 17", "wres:rlwd:0019",
            "wres:rlwd:0021", "wres:rlwd:0040",
        ], inits);
        Assert.DoesNotContain(load.Level100Actors.Actors, actor => actor.SpawnOwnerId.HasValue);

        // After the pre-run: the Tank Factory's init ran on frame 2 and built
        // its first Target Tank there.
        WorldSnapshot start = simulation.Snapshot;
        Level100ActorSnapshot tank = Assert.Single(start.Level100Actors.Actors, actor => actor.ScriptName == "TargetTank1");
        Assert.Equal(2, Assert.Single(start.Level100ActorMechanics.UnitCallbacks!, unit => unit.ActorId == tank.ActorId)
            .ConstructionFrame);
        Assert.All(start.Level100ActorScripts.Instances, instance => Assert.True(instance.Initialized));
    }

    [Fact]
    public void CanonicalHash_BindsEveryClockCountThatDiffersFromMissionTime()
    {
        var simulation = new Simulation(
            1, Level100TestActorDefinitions.Create(), CompletedTutorialSlots);
        simulation.Step(SimInput.Idle);
        WorldSnapshot baseline = Level100TestActorDefinitions.LegacyHashEnvelope(simulation.Snapshot);
        // The pre-run's 60 frames advance both clocks alike.
        Assert.Equal(61, baseline.Level100Mission.Tick);
        Assert.Equal(61u, baseline.RetailEventFrameCount);
        Assert.Equal(42, CanonicalSchemaVersion(baseline));
        string baselineHash = StateHasher.ComputeHex(baseline);
        var hashes = new HashSet<string>();
        foreach (uint count in new[] { 0u, 2u, uint.MaxValue })
        {
            WorldSnapshot changed = baseline with { RetailEventFrameCount = count };
            Assert.Equal(45, CanonicalSchemaVersion(changed));
            string hash = StateHasher.ComputeHex(changed);
            Assert.NotEqual(baselineHash, hash);
            Assert.True(hashes.Add(hash));
        }
    }

    [Fact]
    public void RetailEventClock_CanPassVulcanReadyInOneTickThenLandOnEquality()
    {
        var simulation = new Simulation(
            1, Level100TestActorDefinitions.Create(), CompletedTutorialSlots);

        // The level clock starts after the pre-run. Find its first frame whose
        // stamped Vulcan ready time the next frame's stored word passes, and
        // whose next ready time the frame after lands on exactly (level time
        // 1.0 s shows the pattern, which the pre-run now precedes).
        static bool PassesNextFrame(uint frame, out uint readyBits)
        {
            var probe = new Level100PlayerWeaponRuntime();
            probe.StampReadyAt(Level100MissionWeapon.MechVulcanCannon, RetailEventScheduler.TimeAtFrameCount(frame));
            readyBits = probe.Snapshot.MechVulcanReadyAtTimeBits;
            return probe.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None,
                RetailEventScheduler.TimeAtFrameCount(frame + 1), out _);
        }
        uint first = simulation.Snapshot.RetailEventFrameCount;
        uint frame = first;
        while (!(PassesNextFrame(frame, out _) && !PassesNextFrame(frame + 1, out uint landing) &&
            landing == BitConverter.SingleToUInt32Bits(RetailEventScheduler.TimeAtFrameCount(frame + 2))))
        {
            frame++;
            Assert.True(frame < first + 400, "No frame passes a ready time and then lands on the next.");
        }
        while (simulation.Snapshot.RetailEventFrameCount < frame)
            simulation.Step(SimInput.Idle);

        // Compose the actual level clock with an isolated configured weapon.
        // This is not a claim that the tutorial enables Jet input at that frame.
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.StampReadyAt(Level100MissionWeapon.MechVulcanCannon,
            simulation.EngineTimeSeconds);
        uint firstReady = weapons.Snapshot.MechVulcanReadyAtTimeBits;

        simulation.Step(SimInput.Idle);
        Assert.True(BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds) > firstReady);
        Assert.True(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None,
            simulation.EngineTimeSeconds, out _));
        weapons.StampReadyAt(Level100MissionWeapon.MechVulcanCannon,
            simulation.EngineTimeSeconds);
        uint secondReady = weapons.Snapshot.MechVulcanReadyAtTimeBits;

        simulation.Step(SimInput.Idle);
        Assert.Equal(secondReady, BitConverter.SingleToUInt32Bits(simulation.EngineTimeSeconds));
        Assert.False(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None,
            simulation.EngineTimeSeconds, out _));
    }

    [Fact]
    public void RetailZoom_EasesLookAndProjectionScaleAndMorphForcesZoomOut()
    {
        Simulation simulation = CreatePlayingSimulation();

        Assert.Equal(SimulationConstants.ZoomOutPermille, simulation.Snapshot.ZoomPermille);
        Assert.Equal(
            SimulationConstants.ZoomOutPermille,
            simulation.Snapshot.DesiredZoomPermille);

        int[] zoomInSteps = [900, 800, 700, 600, 500, 400];
        for (int index = 0; index < zoomInSteps.Length; index++)
        {
            WorldSnapshot state = simulation.Step(new SimInput(
                0,
                0,
                index == 0 ? SimActions.ZoomIn : SimActions.None));
            Assert.Equal(zoomInSteps[index], state.ZoomPermille);
            Assert.Equal(SimulationConstants.ZoomInPermille, state.DesiredZoomPermille);
        }

        WorldSnapshot looked = simulation.Step(new SimInput(0, 0, LookX: 1));
        Assert.Equal(5_333, looked.WalkerYawVelocityMicroRadPerTick);

        WorldSnapshot zoomingOut = simulation.Step(
            new SimInput(0, 0, SimActions.ZoomOut));
        Assert.Equal(500, zoomingOut.ZoomPermille);
        Assert.Equal(SimulationConstants.ZoomOutPermille, zoomingOut.DesiredZoomPermille);
        for (int tick = 0; tick < 5; tick++)
        {
            zoomingOut = simulation.Step(SimInput.Idle);
        }
        Assert.Equal(SimulationConstants.ZoomOutPermille, zoomingOut.ZoomPermille);

        simulation.Step(new SimInput(0, 0, SimActions.ZoomIn));
        for (int tick = 0; tick < 5; tick++)
        {
            simulation.Step(SimInput.Idle);
        }
        Assert.Equal(SimulationConstants.ZoomInPermille, simulation.Snapshot.ZoomPermille);

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        WorldSnapshot morphing = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));
        Assert.Equal(VehicleTransition.WalkerToJet, morphing.Transition);
        Assert.Equal(SimulationConstants.ZoomOutPermille, morphing.DesiredZoomPermille);
        Assert.Equal(500, morphing.ZoomPermille);

        for (int tick = 0;
             simulation.Snapshot.Mode != VehicleMode.Jet ||
                 simulation.Snapshot.Transition != VehicleTransition.None;
             tick++)
        {
            Assert.True(tick < 100, "Walker-to-jet morph did not complete.");
            simulation.Step(SimInput.Idle);
        }

        WorldSnapshot jetZoomAttempt = simulation.Step(
            new SimInput(0, 0, SimActions.ZoomIn));
        Assert.Equal(SimulationConstants.ZoomOutPermille, jetZoomAttempt.ZoomPermille);
        Assert.Equal(
            SimulationConstants.ZoomOutPermille,
            jetZoomAttempt.DesiredZoomPermille);
    }

    [Theory]
    [InlineData(73_904, 62_272, -11_153)]
    [InlineData(73_895, 62_287, -11_161)]
    [InlineData(73_647, 62_729, -11_469)]
    public void Level100Terrain_MatchesCopiedRetailGroundSamples(
        int retailXFixed,
        int retailYFixed,
        int expectedHeightUnits)
    {
        Assert.Equal(
            expectedHeightUnits,
            Level100Terrain.Instance.SampleHeightUnitsAtFixed(retailXFixed, retailYFixed));
    }

    [Fact]
    public void Level100Terrain_ExposesReleasedWaterSelection()
    {
        Assert.Equal(
            unchecked((int)0xC10D70A4),
            BitConverter.SingleToInt32Bits(Level100Terrain.Instance.WaterLevel));
        Assert.Equal(0, Level100Terrain.Instance.WaterTexture);
        Assert.Equal(-1_160, Level100Terrain.WaterElevationMillimeters);
    }

    [Theory]
    [InlineData(36, 30, 0x420C2A31)]
    [InlineData(36, 31, 0x416C43B1)]
    [InlineData(35, 30, 0x41A9B2A7)]
    [InlineData(0, 0, 0x00000000)]
    public void Level100Terrain_ReproducesReleasedPatchComplexity(
        int tileX,
        int tileY,
        int expectedFloatBits)
    {
        Assert.Equal(
            expectedFloatBits,
            BitConverter.SingleToInt32Bits(
                Level100Terrain.Instance.GetTileComplexityScore(tileX, tileY)));
    }

    [Fact]
    public void WalkerGroundElevation_IsDeterministicCoreState()
    {
        Simulation first = CreatePlayingSimulation();
        Simulation repeat = CreatePlayingSimulation();

        Assert.Equal(211, first.Snapshot.PlayerGroundElevationMillimeters);
        // 1.333 s, stated against the rate. This was a bare 40, which at 20 Hz
        // would be half a second further down the slope.
        for (int tick = 0;
             tick < 4 * SimulationConstants.TicksPerSecond / 3;
             tick++)
        {
            WorldSnapshot firstState = first.Step(new SimInput(-1, 1));
            WorldSnapshot repeatState = repeat.Step(new SimInput(-1, 1));
            Assert.Equal(
                firstState.PlayerGroundElevationMillimeters,
                repeatState.PlayerGroundElevationMillimeters);
            Assert.Equal(StateHasher.ComputeHex(firstState), StateHasher.ComputeHex(repeatState));
        }

        // Within 92 mm of the 30 Hz pose over the same 1.333 s of held input:
        // the walker model is preserved by the migration, and this is the
        // measurement that says so rather than the argument.
        Assert.Equal(new SimVector2(-3_613, 1_011), first.Snapshot.PlayerPosition);
        Assert.Equal(30, first.Snapshot.PlayerGroundElevationMillimeters);
    }

    [Fact]
    public void PlayerBaseState_CapturesPreviousPoseOncePerSimulationTick()
    {
        Simulation simulation = CreatePlayingSimulation();
        Level100ActorId player = simulation.Snapshot.Level100Actors.Actors
            .Single(actor =>
                actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine)
            .ActorId;
        ThingActorBaseStateSnapshot before = simulation.Snapshot.Level100Actors.BaseStates
            .Single(item => item.ActorId == player)
            .State;

        WorldSnapshot moved = simulation.Step(new SimInput(0, 1));

        ThingActorBaseStateSnapshot after = moved.Level100Actors.BaseStates
            .Single(item => item.ActorId == player)
            .State;
        Assert.Equal(before.CurrentPose, after.OldPose);
        Assert.NotEqual(after.OldPose, after.CurrentPose);

        WorldSnapshot next = simulation.Step(SimInput.Idle);
        ThingActorBaseStateSnapshot nextState = next.Level100Actors.BaseStates
            .Single(item => item.ActorId == player)
            .State;
        Assert.Equal(after.CurrentPose, nextState.OldPose);
    }

    [Fact]
    public void CanonicalHashRetainsResetBaselineGroundDeltaAndExactFootPhase()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot state = simulation.Step(new SimInput(0, 1));
        for (int tick = 0;
             state.WalkerFeet.All(foot => foot.PhaseThirds == 0) && tick < 120;
             tick++)
        {
            state = simulation.Step(new SimInput(0, 1));
        }

        Assert.Contains(state.WalkerFeet, foot => foot.PhaseThirds > 0);
        Level100ActorSnapshot player = state.Level100Actors.Actors.Single(actor =>
            actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine);
        Assert.Equal(
            state.PlayerVerticalVelocityMillimetersPerTick,
            player.Pose.LinearVelocityMillimetersPerTick.Y);

        string hash = StateHasher.ComputeHex(state);
        Assert.NotEqual(hash, StateHasher.ComputeHex(state with
        {
            PlayerGroundDeltaMillimeters = state.PlayerGroundDeltaMillimeters + 1,
        }));
        WalkerFootContactSnapshot changedFoot = state.WalkerFeet[0] with
        {
            PhaseThirds = state.WalkerFeet[0].PhaseThirds + 1,
        };
        Assert.NotEqual(hash, StateHasher.ComputeHex(state with
        {
            WalkerFeet = state.WalkerFeet
                .Select((foot, index) => index == 0 ? changedFoot : foot)
                .ToArray(),
        }));
        Assert.NotEqual(hash, StateHasher.ComputeHex(state with
        {
            InitialLevel100TutorialProgress = default,
        }));

        WorldSnapshot reset = simulation.Step(new SimInput(0, 0, SimActions.Reset));
        Assert.Equal(CompletedTutorialSlots, reset.InitialLevel100TutorialProgress);
    }

    [Fact]
    public void WalkerFeet_RepeatReleasedDiagonalStepsAndSettleOnTheLevel100Slope()
    {
        Simulation first = CreatePlayingSimulation();
        Simulation repeat = CreatePlayingSimulation();
        int[]? firstSwing = null;

        for (int tick = 0; tick < 360; tick++)
        {
            WorldSnapshot firstState = first.Step(new SimInput(0, 1));
            WorldSnapshot repeatState = repeat.Step(new SimInput(0, 1));
            Assert.Equal(StateHasher.ComputeHex(firstState), StateHasher.ComputeHex(repeatState));
            firstSwing ??= firstState.WalkerFeet.Any(foot => foot.StepPhase > 0)
                ? firstState.WalkerFeet
                    .Where(foot => foot.StepPhase > 0)
                    .Select(foot => foot.Id)
                    .ToArray()
                : null;
        }

        Assert.NotNull(firstSwing);
        Assert.Equal([0, 3], firstSwing!);
        for (int tick = 0; tick < 450; tick++)
        {
            WorldSnapshot firstState = first.Step(SimInput.Idle);
            WorldSnapshot repeatState = repeat.Step(SimInput.Idle);
            Assert.Equal(StateHasher.ComputeHex(firstState), StateHasher.ComputeHex(repeatState));
        }

        Assert.All(first.Snapshot.WalkerFeet, foot =>
        {
            Assert.Equal(0, foot.StepPhase);
            Assert.Equal(0, foot.LiftMillimeters);
            Assert.Equal(
                Level100Terrain.Instance.SampleGroundElevationMillimeters(foot.Position),
                foot.GroundElevationMillimeters);
        });
        Assert.True(
            first.Snapshot.WalkerFeet.Max(foot => foot.GroundElevationMillimeters) -
            first.Snapshot.WalkerFeet.Min(foot => foot.GroundElevationMillimeters) >= 500);
    }

    [Fact]
    public void Level100FirstRun_AppliesReleasedMessagesActivationAndTriggerCommands()
    {
        var simulation = new Simulation(1, Level100TestActorDefinitions.Create());
        var attemptedInput = new SimInput(
            0,
            1,
            SimActions.Fire | SimActions.ToggleMode,
            LookX: 1);

        Assert.Equal(SimulationConstants.Level100OpeningPanTicks, simulation.Snapshot.Level100OpeningTicksRemaining);
        Assert.False(simulation.Snapshot.Level100PlayerControlEnabled);
        Assert.False(simulation.Snapshot.Level100FlightEnabled);
        Assert.False(simulation.Snapshot.Level100PulseCannonEnabled);
        Assert.Empty(simulation.Snapshot.Level100Mission.PendingEvents);
        // The greeting is NOT delivered here. CPanCamera::GetShowHUD is false
        // for the whole opening pan, and CGame::StartPlayingState only lets the
        // message box play once the pan is over
        // (Level100MissionTiming.MessageBoxAllowedTick). Delivering HUD_01 on
        // tick 0 is exactly how the reconstruction managed to show no greeting
        // at all: it was over before the HUD appeared.
        Assert.DoesNotContain(
            simulation.Snapshot.Level100MissionEvents.OfType<Level100MessageRequested>(),
            message => message.MessageId == 292562);

        var messages = simulation.Snapshot.Level100MissionEvents
            .OfType<Level100MessageRequested>()
            .Select(message => message.MessageId)
            .ToList();

        for (int tick = 1; tick <= SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            WorldSnapshot state = simulation.Step(attemptedInput);
            messages.AddRange(state.Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .Select(message => message.MessageId));
        }

        Assert.Equal(0, simulation.Snapshot.Level100OpeningTicksRemaining);
        Assert.False(simulation.Snapshot.Level100PlayerControlEnabled);
        Assert.Equal(SimVector2.Zero, simulation.Snapshot.PlayerPosition);
        Assert.Equal(SimulationConstants.Level100PlayerStartYawMicroRad, simulation.Snapshot.FacingYawMicroRad);
        Assert.Equal(VehicleTransition.None, simulation.Snapshot.Transition);
        Assert.Equal(SimulationConstants.MaximumEnergy, simulation.Snapshot.Energy);
        Assert.Empty(simulation.Snapshot.Projectiles);

        AdvanceUntil(
            simulation,
            state => string.Equals(
                state.Level100Mission.NavigationObjective,
                "Target Zone 1",
                StringComparison.Ordinal),
            1_500,
            state => messages.AddRange(state.Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .Select(message => message.MessageId)));

        // Tick 812 of the session; the mission clock also counts the 60
        // pre-run frames before the pan.
        Assert.Equal(812, simulation.Snapshot.Tick);
        Assert.Equal(SimulationConstants.Level100PreRunTicks + 812, simulation.Snapshot.Level100Mission.Tick);

        // TUTORIAL_01 and TUTORIAL_SCANNER are both PlayCharMessage - the
        // script does not wait for them - so the objective is set before either
        // reaches the message box. They arrive at the box's own pace.
        for (int tick = 0; tick < 200; tick++)
        {
            messages.AddRange(simulation.Step(SimInput.Idle).Level100MissionEvents
                .OfType<Level100MessageRequested>()
                .Select(message => message.MessageId));
        }

        Assert.Equal(
            [292562, 293386, 296682, -1575499396, -257967449, 82987417, 4422830, 175347826],
            messages);
        Assert.True(simulation.Snapshot.Level100PlayerActive);
        Assert.True(simulation.Snapshot.Level100PlayerControlEnabled);
        Level100TriggerActorSnapshot trigger = simulation.Snapshot.Level100TriggerActors
            .Single(item => item.Trigger == Level100MissionTrigger.TargetZone1);
        Assert.True(trigger.Active);
        Assert.True(trigger.IsObjective);
        Assert.False(trigger.Reached);
    }

    [Fact]
    public void WalkerForward_AcceleratesToMeasuredCapAndCoastsAfterRelease()
    {
        Simulation simulation = CreatePlayingSimulation();

        // Every value below moved with the 20 Hz migration and none of them is
        // a behaviour change: WalkerAccelerationPerTick is 70 where it was 33
        // (a damped-input conversion, x2.126), and the cap is
        // mMaxWalkVelocity 0.15 = 150 mm/tick where it was 100 - both of which
        // are the SAME 3,000 mm/s. The measured retail sequence this pins,
        // 0 -> 0.07 -> 0.119 -> 0.15 units per RELEASED update, is now
        // reproduced tick for tick rather than through a time-equivalent.
        foreach (SimVector2 expected in new[]
                 {
                     new SimVector2(-34, 61),
                     new SimVector2(-57, 103),
                     new SimVector2(-72, 131),
                     new SimVector2(-72, 131),
                     new SimVector2(-72, 131),
                 })
        {
            WorldSnapshot state = simulation.Step(new SimInput(0, 1));
            Assert.Equal(expected, state.PlayerVelocity);
        }

        foreach (SimVector2 expected in new[]
                 {
                     new SimVector2(-50, 91),
                     new SimVector2(-35, 63),
                     new SimVector2(-24, 44),
                     new SimVector2(-16, 30),
                     new SimVector2(-11, 21),
                 })
        {
            WorldSnapshot state = simulation.Step(SimInput.Idle);
            Assert.Equal(expected, state.PlayerVelocity);
        }
    }

    [Fact]
    public void WalkerStrafe_UsesTheSameMeasuredResponseAsForward()
    {
        Simulation simulation = CreatePlayingSimulation();

        foreach (SimVector2 expected in new[]
                 {
                     new SimVector2(61, 34),
                     new SimVector2(103, 57),
                     new SimVector2(131, 72),
                     new SimVector2(131, 72),
                     new SimVector2(131, 72),
                 })
        {
            WorldSnapshot state = simulation.Step(new SimInput(1, 0));
            Assert.Equal(expected, state.PlayerVelocity);
        }
    }

    [Fact]
    public void WalkerOppositeFlick_TriggersRetailDashAndLocksInputForItsLifecycle()
    {
        Simulation simulation = CreatePlayingSimulation();

        // The opposite hard press three event frames earlier: inside retail's
        // window on every frame (walker-dash.md).
        WorldSnapshot backward = simulation.Step(new SimInput(0, -1));
        Assert.Equal(new SimVector2(34, -61), backward.PlayerVelocity);
        simulation.Step(SimInput.Idle);
        simulation.Step(SimInput.Idle);

        WorldSnapshot triggered = simulation.Step(new SimInput(0, 1));
        // The backward velocity retained over the two idle frames, (11, -20),
        // plus the dash's acceleration, (-854, 1527).
        Assert.Equal(new SimVector2(-843, 1_507), triggered.PlayerVelocity);
        Assert.Equal(14, triggered.WalkerDashTicksRemaining);
        Assert.Single(
            triggered.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.WalkerDashRequested);

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        WorldSnapshot morphRejected = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));
        Assert.Equal(VehicleTransition.None, morphRejected.Transition);
        Assert.Equal(13, morphRejected.WalkerDashTicksRemaining);
        Assert.Contains(
            morphRejected.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.TransformRejected);

        for (int remaining = 12; remaining >= 0; remaining--)
        {
            WorldSnapshot locked = simulation.Step(new SimInput(0, -1));
            Assert.Equal(remaining, locked.WalkerDashTicksRemaining);
            Assert.True(locked.PlayerVelocity.X < 0);
            Assert.True(locked.PlayerVelocity.Z > 0);
        }

        WorldSnapshot released = simulation.Step(new SimInput(0, -1));
        Assert.Equal(-1_000, released.WalkerLastMoveZPermille);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(RetailEventScheduler.TimeAtFrameCount(released.RetailEventFrameCount)),
            released.WalkerLastHardBackwardTimeBits);

        // The retail lateral pair is asymmetric: left assigns +0.08 roll
        // velocity, while right subtracts 0.08 from whatever residual remains.
        var lateral = CreatePlayingSimulation();
        lateral.Step(new SimInput(1, 0));
        lateral.Step(SimInput.Idle);
        lateral.Step(SimInput.Idle);
        WorldSnapshot leftDash = lateral.Step(new SimInput(-1, 0));
        Assert.Equal(
            SimulationConstants.WalkerDashRollVelocityMicroRadPerTick,
            leftDash.RollVelocityMicroRadPerTick);
        Assert.Single(
            leftDash.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.WalkerDashRequested);
        while (lateral.Snapshot.WalkerDashTicksRemaining > 0)
        {
            lateral.Step(SimInput.Idle);
        }
        lateral.Step(SimInput.Idle);
        lateral.Step(new SimInput(-1, 0));
        lateral.Step(SimInput.Idle);
        WorldSnapshot beforeRight = lateral.Step(SimInput.Idle);
        int expectedRightRoll =
            (int)((long)beforeRight.RollVelocityMicroRadPerTick *
                SimulationConstants.WalkerYawRetentionNumerator /
                SimulationConstants.WalkerYawRetentionDenominator) -
            SimulationConstants.WalkerDashRollVelocityMicroRadPerTick;
        WorldSnapshot rightDash = lateral.Step(new SimInput(1, 0));
        Assert.Equal(14, rightDash.WalkerDashTicksRemaining);
        Assert.Equal(expectedRightRoll, rightDash.RollVelocityMicroRadPerTick);
        Assert.Single(
            rightDash.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.WalkerDashRequested);
    }

    /// <summary>
    /// Retail's dash window (walker-dash.md) compares float32 event times:
    /// now - 0.2f &lt; last &lt; now - 0.1f, each difference rounded to float32.
    /// With the hard backward press k event frames before the forward edge on
    /// frame n: k = 3 always dashes; k = 1 and k = 5 never do; k = 2 and k = 4
    /// dash only where the rounding admits them. The frames are the RE lane's
    /// table extended past the pre-run and pan: from frame 185, k = 2 admits
    /// only frame 321 before 400, and k = 4 first admits frame 325.
    /// </summary>
    [Theory]
    [InlineData(1, 200, false)]
    [InlineData(3, 200, true)]
    [InlineData(5, 200, false)]
    [InlineData(2, 320, false)]
    [InlineData(2, 321, true)]
    [InlineData(2, 322, false)]
    [InlineData(4, 324, false)]
    [InlineData(4, 325, true)]
    [InlineData(4, 326, false)]
    public void WalkerDash_AdmitsTheOppositePressOnlyInsideRetailsFloatWindow(int k, int frame, bool dashes)
    {
        Simulation simulation = CreatePlayingSimulation();
        while (simulation.Snapshot.RetailEventFrameCount < frame - k - 1)
        {
            simulation.Step(SimInput.Idle);
        }

        WorldSnapshot backward = simulation.Step(new SimInput(0, -1));
        Assert.Equal((uint)(frame - k), backward.RetailEventFrameCount);
        for (int idle = 1; idle < k; idle++)
        {
            simulation.Step(SimInput.Idle);
        }

        WorldSnapshot forward = simulation.Step(new SimInput(0, 1));
        Assert.Equal((uint)frame, forward.RetailEventFrameCount);
        Assert.Equal(dashes, forward.AquilaFlightEventLog.Any(item => item.Kind == AquilaFlightEvents.WalkerDashRequested));
        Assert.Equal(dashes, forward.WalkerDashTicksRemaining > 0);
    }

    [Fact]
    public void WalkerLook_AcceleratesBodyYawAndCoastsAfterRelease()
    {
        Simulation simulation = CreatePlayingSimulation();

        foreach (int expected in
                 new[] { 13_333, 23_999, 32_532, 39_358, 44_819 })
        {
            WorldSnapshot state = simulation.Step(new SimInput(0, 0, LookX: 1));
            Assert.Equal(expected, state.WalkerYawVelocityMicroRadPerTick);
        }

        WorldSnapshot coast = simulation.Step(SimInput.Idle);
        Assert.Equal(35_855, coast.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(699_726, coast.FacingYawMicroRad);
    }

    /// <summary>
    /// The jet's ground-effect lookahead is HALF A SECOND of travel, not half a
    /// tick.
    ///
    /// <para><c>BattleEngineJetPart.cpp:548</c> is
    /// <c>pos = mMainPart-&gt;mPos + (mMainPart-&gt;mVelocity * GAME_FR * 0.5f)</c>.
    /// <c>GAME_FR</c> is <c>20.0f</c> (<c>thing.h:28</c>) and <c>mVelocity</c> is
    /// per released UPDATE - <c>actor.cpp</c> bounds it with
    /// <c>GetMaxVelocity()/GAME_FR</c>, which only type-checks as a per-update
    /// quantity. So <c>mVelocity * GAME_FR</c> is units per second and the
    /// <c>0.5f</c> makes the sample point half a second ahead.</para>
    ///
    /// <para>This asserts the LAW as a ratio rather than pinning a distance,
    /// because the distance is velocity-dependent and a pinned number would have
    /// to be re-derived every time the flight model moves. Core read
    /// <c>velocity / 2</c> and was 30x too near; the ratio is what was wrong and
    /// the ratio is what is guarded.</para>
    /// </summary>
    [Fact]
    public void JetGroundEffectLookahead_IsHalfASecondOfTravel_NotHalfATick()
    {
        // The released law, stated independently of our constant so that
        // changing the constant cannot make this test agree with itself.
        const double ReleasedSeconds = 0.5;
        int expectedTicks = (int)(ReleasedSeconds * SimulationConstants.TicksPerSecond);

        Assert.Equal(SimulationConstants.JetGroundEffectLookaheadTicks, expectedTicks);

        // And the thing that actually went wrong: it must be a multiplier on
        // velocity, not a divisor. `velocity / 2` satisfies no positive tick
        // count, so this would have failed on the old code for the right reason.
        Assert.True(
            SimulationConstants.JetGroundEffectLookaheadTicks > 1,
            "a lookahead of one tick or less is the defect this guards");

        // 30x is the measured size of the old error at the documented cruise
        // speed, recorded so the magnitude is not lost if the constant moves.
        int CruiseSpeedMillimetresPerTick =
            SimulationConstants.JetMaximumSpeedPerTick;
        Assert.Equal(
            9_000,
            CruiseSpeedMillimetresPerTick * SimulationConstants.JetGroundEffectLookaheadTicks);
    }

    /// <summary>
    /// Skimming water damages the Aquila, and the amount is pinned to the
    /// released anchor rather than to our own arithmetic.
    ///
    /// <para><c>BattleEngineJetPart.cpp:536</c> is
    /// <c>float damage=(0.5f-altitude)*20.0f</c>, applied once per released
    /// 20 Hz update. At zero altitude that is 10.0 released life against the
    /// Aquila Prototype's <c>mLife</c> of 20.0 - so touching the water costs
    /// EXACTLY HALF THE HULL PER RELEASED UPDATE. That halving is the anchor:
    /// it is a property of the released numbers, independent of our tick rate,
    /// our units and our conversion, so it cannot be satisfied by a
    /// self-consistent but wrongly-scaled implementation.</para>
    ///
    /// <para>Ours applied no damage at all until 2026-07-27 - measured by flying
    /// four skim passes at ~500 mm over water with the hull sitting at full
    /// throughout.</para>
    /// </summary>
    [Fact]
    public void WaterSkimDamage_CostsHalfTheHullPerReleasedUpdate_AtZeroAltitude()
    {
        // The released law, restated here from the source rather than read from
        // our constants, so the test cannot agree with itself.
        const int ReleasedDamagePerUnitOfDepth = 20;
        const double ReleasedSkimCeilingUnits = 0.5;
        const double ReleasedAquilaLife = 20.0;

        double releasedDamageAtZeroAltitude =
            ReleasedSkimCeilingUnits * ReleasedDamagePerUnitOfDepth;
        Assert.Equal(ReleasedAquilaLife / 2.0, releasedDamageAtZeroAltitude);

        // Half the hull per released update, expressed in registry milli-life.
        // This is the anchor and it is derived from the released numbers above,
        // not from our conversion.
        int expectedPerReleasedUpdate = SimulationConstants.MaximumHull / 2;
        Assert.Equal(
            (int)(releasedDamageAtZeroAltitude * 1_000),
            expectedPerReleasedUpdate);

        // The same quantity through OUR units, in the direction the simulation
        // actually computes it. Core and retail both run at 20 Hz now, so this
        // ratio is exact; spelling it out still binds the unit-conversion law.
        long milliLifePerCoreTick = SimulationConstants.JetSkimHeightMillimeters *
            (long)SimulationConstants.JetWaterSkimDamagePerReleasedUnit *
            SimulationConstants.RetailTicksPerCoreTickNumerator /
            SimulationConstants.RetailTicksPerCoreTickDenominator;
        long expectedPerCoreTick =
            (long)expectedPerReleasedUpdate *
            SimulationConstants.RetailTicksPerCoreTickNumerator /
            SimulationConstants.RetailTicksPerCoreTickDenominator;

        Assert.Equal(expectedPerCoreTick, milliLifePerCoreTick);

        // Retail starts death strictly below zero. Two 10.0 released updates
        // therefore leave exact-zero life alive and the third kills. Core now
        // runs at retail's 20 Hz rate, so those are also three Core ticks and
        // 0.15 seconds—not a ceil-to-zero shortcut.
        int releasedUpdatesToDeath =
            ((int)ReleasedAquilaLife / (int)releasedDamageAtZeroAltitude) + 1;
        int coreTicksToDeath =
            (SimulationConstants.MaximumHull / (int)expectedPerCoreTick) + 1;
        Assert.Equal(3, releasedUpdatesToDeath);
        Assert.Equal(3, coreTicksToDeath);
    }

    [Fact]
    public void TerrainPitch_IsNegatedAgainstForwardRise_AndRollIsNot()
    {
        // Guards the sign defect directly, rather than inferring it from an
        // endpoint. Both released producers compute
        //     p = (yv x map_normal) x map_normal;  pitch = -p.Z
        // (BattleEngine.cpp:1143, BattleEngineJetPart.cpp:597). Because
        // (a x n) x n = n(a.n) - a, p is anti-parallel to facing, so retail's
        // pitch is NEGATIVE when the ground rises ahead. Roll uses the SINGLE
        // cross r = yv x map_normal and is NOT negated.
        //
        // Ground rising ahead must produce nose-up (negative) pitch; falling
        // away must produce nose-down (positive); flat must produce zero.
        (SimVector2 position, int yaw, int forwardSlopePermille) = FindSlopedSample();
        Assert.NotEqual(0, forwardSlopePermille);

        int pitch = Simulation.SampleTerrainPitchMicroRad(position, yaw);
        Assert.NotEqual(0, pitch);
        Assert.Equal(-Math.Sign(forwardSlopePermille), Math.Sign(pitch));

        // Reversing the facing reverses the forward slope, so the pitch sign
        // must flip too. This catches a constant offset masquerading as a sign.
        int reversedYaw = yaw + 3_141_593;
        int reversedPitch = Simulation.SampleTerrainPitchMicroRad(position, reversedYaw);
        Assert.Equal(-Math.Sign(pitch), Math.Sign(reversedPitch));

        // Roll must keep the SAME sign as the rightward slope - negating it
        // alongside pitch would be a half-applied fix and worse than none.
        // At yaw 0 the rightward slope reduces to gradient.X exactly, so no
        // trig helper is needed and the assertion stays exact.
        SimVector2 gradient =
            Level100Terrain.Instance.SampleGroundGradientPermille(position);
        int rightSlopePermille = gradient.X;
        if (rightSlopePermille != 0)
        {
            int roll = Simulation.SampleTerrainRollMicroRad(position, yaw);
            Assert.Equal(Math.Sign(rightSlopePermille), Math.Sign(roll));
        }
    }

    private static (SimVector2 Position, int Yaw, int ForwardSlopePermille)
        FindSlopedSample()
    {
        // Walk the authored Level 100 heightfield for a sample with a clearly
        // non-zero forward slope at yaw 0, so the assertion is not riding on
        // rounding noise.
        for (int z = -40_000; z <= 40_000; z += 1_000)
        {
            for (int x = -40_000; x <= 40_000; x += 1_000)
            {
                var position = new SimVector2(x, z);
                SimVector2 gradient =
                    Level100Terrain.Instance.SampleGroundGradientPermille(position);
                if (Math.Abs(gradient.Z) >= 200)
                {
                    return (position, 0, gradient.Z);
                }
            }
        }

        throw new InvalidOperationException(
            "no sufficiently sloped Level 100 sample found");
    }

    [Fact]
    public void WalkerVerticalLook_UsesMeasuredInertiaAndReleasedTerrainRelativeLimits()
    {
        Assert.Equal(
            -1_091_250,
            SimulationConstants.ObservedWalkerPitchUpLimitAtLevel100StartMicroRad);
        Assert.Equal(
            532_123,
            SimulationConstants.ObservedWalkerPitchDownLimitAtLevel100StartMicroRad);

        Simulation simulation = CreatePlayingSimulation();

        (int Velocity, int Pitch)[] expected =
        [
            (-8_547, -8_547),
            (-15_384, -23_931),
            (-20_854, -44_785),
            (-25_230, -70_015),
            (-28_731, -98_746),
        ];
        foreach ((int velocity, int pitch) in expected)
        {
            WorldSnapshot state = simulation.Step(new SimInput(0, 0, LookY: -1));
            Assert.Equal(velocity, state.WalkerPitchVelocityMicroRadPerTick);
            Assert.Equal(pitch, state.FacingPitchMicroRad);
        }

        WorldSnapshot coast = simulation.Step(SimInput.Idle);
        Assert.Equal(-22_984, coast.WalkerPitchVelocityMicroRadPerTick);
        Assert.Equal(-121_730, coast.FacingPitchMicroRad);

        for (int tick = 0; tick < 100; tick++)
        {
            simulation.Step(new SimInput(0, 0, LookY: -1));
        }
        // TERRAIN-RELATIVE, derived from released constants - not a window
        // fitted around our own output. BattleEngine.cpp:1145-1176 damps with a
        // 6.0 coefficient from a -0.8 threshold, so the soft limiter's
        // equilibrium is delta = -800_000 - 1_000_000/6 = -966_667. Held input
        // converges toward that bound without passing it. Asserting the DELTA
        // rather than the absolute pitch makes this immune to the separate
        // terrain-gradient shortfall below, and it fails loudly if the limiter
        // itself regresses.
        int upDelta = simulation.Snapshot.FacingPitchMicroRad -
            Simulation.SampleTerrainPitchMicroRad(
                simulation.Snapshot.PlayerPosition,
                simulation.Snapshot.FacingYawMicroRad);
        Assert.InRange(upDelta, -966_667, -900_000);
        Assert.True(simulation.Snapshot.WalkerPitchVelocityMicroRadPerTick <= 0);

        for (int tick = 0; tick < 200; tick++)
        {
            simulation.Step(new SimInput(0, 0, LookY: 1));
        }
        // Same treatment, opposite bound: 500_000 + 1_000_000/6 = 666_667.
        int downDelta = simulation.Snapshot.FacingPitchMicroRad -
            Simulation.SampleTerrainPitchMicroRad(
                simulation.Snapshot.PlayerPosition,
                simulation.Snapshot.FacingYawMicroRad);
        Assert.InRange(downDelta, 600_000, 666_667);

        // PARITY, with a NAMED tolerance rather than a silent one. The two
        // Observed* constants above were previously only ever asserted against
        // their own literals - a tautology that let the file look like it
        // checked retail parity while checking nothing. 60_000 uRad is not a
        // target, it is the CURRENT known shortfall: inverting the limiter
        // equilibrium through both released endpoints brackets retail's ground
        // pitch at the Level 100 start to [-134_544, -124_583], while ours
        // brackets to [-83_663, -69_722]. Disjoint, both negative - the sign is
        // right and the magnitude is not. Our sampled forward slope is roughly
        // 59% of retail's, most likely because SampleGroundGradientPermille
        // central-differences over +/-1000 mm and smooths the slope relative to
        // whatever MAP.Normal does. Tightening that sampler MUST tighten this
        // tolerance; do not widen it to accommodate a regression.
        Assert.InRange(
            Math.Abs(simulation.Snapshot.FacingPitchMicroRad -
                SimulationConstants.ObservedWalkerPitchDownLimitAtLevel100StartMicroRad),
            0,
            60_000);
        Assert.True(simulation.Snapshot.WalkerPitchVelocityMicroRadPerTick >= 0);
    }

    [Fact]
    public void WalkerMovementUsesContinuousBodyYawWithoutResettingLookYaw()
    {
        Simulation simulation = CreatePlayingSimulation();
        // 0.667 s of held look, stated against the rate.
        for (int tick = 0;
             tick < 2 * SimulationConstants.TicksPerSecond / 3;
             tick++)
        {
            simulation.Step(new SimInput(0, 0, LookX: 1));
        }

        WorldSnapshot state = simulation.Step(new SimInput(0, 1));
        Assert.Equal(1, state.FacingX);
        Assert.Equal(1, state.FacingZ);
        // The shipped Aquila record's 1.0 GroundTurnRate leaves the same
        // continuous inertial yaw owner in place; only the superseded fitted
        // 1.7 gain changed.
        Assert.Equal(1_174_857, state.FacingYawMicroRad);
        Assert.Equal(new SimVector2(-65, 27), state.PlayerVelocity);
    }

    [Fact]
    public void SnapshotPlayerOwners_AgreeAfterMovementDamageActivationDeathAndReset()
    {
        Simulation simulation = CreatePlayingSimulation();

        WorldSnapshot state = simulation.Step(
            new SimInput(1, 1, SimActions.ToggleMode, LookX: 1),
            [new Level100PlayerDamageFact(200)]);

        AssertCanonicalPlayer(state);
        Assert.Equal(VehicleTransition.None, state.Transition);

        state = simulation.Step(
            SimInput.Idle,
            [new Level100ActorActivationFact(
                state.Level100Actors.Actors.Single(actor =>
                    actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine).ActorId,
                false)]);
        AssertCanonicalPlayer(state);
        Assert.False(state.Level100PlayerActive);

        Level100ActorId playerId = state.Level100Actors.Actors.Single(actor =>
            actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine).ActorId;
        state = simulation.Step(
            SimInput.Idle,
            [new Level100ActorActivationFact(playerId, true),
             new Level100PlayerDamageFact(
                 SimulationConstants.MaximumHull +
                 SimulationConstants.MaximumShield + 1)]);
        AssertCanonicalPlayer(state);
        Assert.Equal(0, state.Hull);
        Assert.Equal(Level100ActorLifecycle.Destroyed, state.Level100Actors.Actors.Single(
            actor => actor.ActorId == playerId).Lifecycle);

        state = simulation.Step(new SimInput(0, 0, SimActions.Reset));
        AssertCanonicalPlayer(state);
        Assert.Equal(SimulationConstants.MaximumHull, state.Hull);
        Assert.Equal(Level100ActorLifecycle.Alive, state.Level100Actors.Actors.Single(
            actor => actor.ActorId == playerId).Lifecycle);
    }

    [Fact]
    public void SegmentedActorRejectsExternalHealthBeforeMutatingTheTick()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot before = simulation.Snapshot;
        Level100ActorId targetId = before.Level100Actors.Actors.First(actor =>
            actor.DefinitionName == "Target Tank").ActorId;

        Assert.Throws<InvalidOperationException>(() => simulation.Step(
            SimInput.Idle,
            [new Level100ActorHealthFact(targetId, 1)]));

        Assert.Equal(before.Tick, simulation.Snapshot.Tick);
        Assert.Equal(
            StateHasher.ComputeHex(before),
            StateHasher.ComputeHex(simulation.Snapshot));
    }

    [Fact]
    public void SegmentedActorRejectsExternalDyingFactsBeforeMutatingTheTick()
    {
        foreach (bool died in new[] { false, true })
        {
            Simulation simulation = CreatePlayingSimulation();
            WorldSnapshot before = simulation.Snapshot;
            Level100ActorId targetId = before.Level100Actors.Actors.First(actor =>
                actor.DefinitionName == "Target Tank").ActorId;
            Level100SimulationFact fact = died
                ? new Level100ActorDiedFact(targetId)
                : new Level100ActorStartedDyingFact(targetId);

            Assert.Throws<InvalidOperationException>(() => simulation.Step(
                SimInput.Idle,
                [fact]));

            Assert.Equal(before.Tick, simulation.Snapshot.Tick);
            Assert.Equal(
                StateHasher.ComputeHex(before),
                StateHasher.ComputeHex(simulation.Snapshot));
        }
    }

    private static void AssertCanonicalPlayer(WorldSnapshot state)
    {
        Level100ActorSnapshot player = state.Level100Actors.Actors.Single(actor =>
            actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine);
        Assert.Equal(state.PlayerPosition.X, player.Pose.PositionMillimeters.X);
        Assert.Equal(state.PlayerElevationMillimeters, player.Pose.PositionMillimeters.Y);
        Assert.Equal(state.PlayerPosition.Z, player.Pose.PositionMillimeters.Z);
        Assert.Equal(state.PlayerVelocity.X, player.Pose.LinearVelocityMillimetersPerTick.X);
        Assert.Equal(
            state.PlayerVerticalVelocityMillimetersPerTick,
            player.Pose.LinearVelocityMillimetersPerTick.Y);
        Assert.Equal(state.PlayerVelocity.Z, player.Pose.LinearVelocityMillimetersPerTick.Z);
        Assert.Equal(state.Hull, player.Health);
        Assert.Equal(state.Level100PlayerActive, player.Active);
        Assert.Equal(state.Mode == VehicleMode.Jet, state.Level100ActorScripts.PlayerInJetMode);
    }

    [Fact]
    public void LookAxes_OutsideUnitRange_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            new SimInput(0, 0, LookX: 2).Validate);
        Assert.Throws<ArgumentOutOfRangeException>(
            new SimInput(0, 0, LookY: -2).Validate);
        Assert.Throws<ArgumentOutOfRangeException>(
            new SimInput(0, 0, LookXAnalogPermille: 1_001).Validate);
        Assert.Throws<ArgumentOutOfRangeException>(
            new SimInput(0, 0, LookYAnalogPermille: -1_001).Validate);
    }

    [Fact]
    public void WalkerAnalogLook_FollowsTheReleasedCurveAndUsesTheSameRetailCoast()
    {
        // This test asserted PROPORTIONALITY until 2026-07-27 — half input,
        // half rate. That was the defect, not the specification.
        // Player.cpp:334-355 curves every look axis through
        // tan(1.2*val)/tan(1.2) before it reaches the battle engine, so half
        // deflection commands about 27% of full rate. Full deflection is
        // unchanged, which is why nothing else caught this.
        Simulation half = CreatePlayingSimulation();
        Simulation full = CreatePlayingSimulation();

        WorldSnapshot halfInput = half.Step(new SimInput(0, 0, LookXAnalogPermille: 500));
        WorldSnapshot fullInput = full.Step(new SimInput(0, 0, LookXAnalogPermille: 1_000));

        Assert.Equal(13_333, fullInput.WalkerYawVelocityMicroRadPerTick);

        // Asserted as the law rather than as a golden number: whatever full
        // deflection commands, half deflection commands the curve's fraction
        // of it, to within the integer rounding of one scaling step.
        int expectedHalf =
            fullInput.WalkerYawVelocityMicroRadPerTick * LookAxisResponse.Apply(500) / 1_000;
        Assert.InRange(
            halfInput.WalkerYawVelocityMicroRadPerTick, expectedHalf - 1, expectedHalf + 1);

        // And the compression is real, not a rounding artefact: half input
        // turns the walker through well under half the angle.
        long halfTurn =
            halfInput.FacingYawMicroRad - SimulationConstants.Level100PlayerStartYawMicroRad;
        long fullTurn =
            fullInput.FacingYawMicroRad - SimulationConstants.Level100PlayerStartYawMicroRad;
        Assert.True(halfTurn * 3 < fullTurn, $"half turned {halfTurn} against full {fullTurn}");

        WorldSnapshot coast = half.Step(SimInput.Idle);
        Assert.Equal(
            RetainedYawFor(halfInput.WalkerYawVelocityMicroRadPerTick),
            coast.WalkerYawVelocityMicroRadPerTick);
        Assert.Equal(
            halfInput.FacingYawMicroRad + coast.WalkerYawVelocityMicroRadPerTick,
            coast.FacingYawMicroRad);
    }

    private static int RetainedYawFor(int velocity) =>
        (int)((long)velocity * SimulationConstants.WalkerYawRetentionNumerator /
            SimulationConstants.WalkerYawRetentionDenominator);

    [Fact]
    public void LookX_OneTick_DoesNotYetLeaveForwardFacing()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot state = simulation.Step(new SimInput(0, 0, LookX: 1));
        Assert.Equal(0, state.FacingX);
        Assert.Equal(1, state.FacingZ);
        Assert.Equal(523_163, state.FacingYawMicroRad);
    }

    [Fact]
    public void LookX_Negative_TurnsLeftFromTheAuthoredStartYaw()
    {
        Simulation simulation = CreatePlayingSimulation();
        // 0.667 s of held look, stated against the rate. Held for 20 ticks at
        // 20 Hz the walker turns half again as far and leaves this sector.
        for (int tick = 0;
             tick < 2 * SimulationConstants.TicksPerSecond / 3;
             tick++)
        {
            simulation.Step(new SimInput(0, 0, LookX: -1));
        }

        WorldSnapshot state = simulation.Snapshot;
        Assert.Equal(-1, state.FacingX);
        Assert.Equal(1, state.FacingZ);
    }

    [Fact]
    public void JetEnergyDrain_UsesTheAquilaPrototypeThrottleCurve()
    {
        // Level 100's RLWD declares "Aquila Prototype". Its record in
        // data/battle engine configurations.dat carries mEnergy 8.0,
        // mMinAirEnergyCost 0.005 and mMaxAirEnergyCost 0.012, spent once per
        // RETAIL tick. Stored in micro-retail energy so half throttle
        // interpolates exactly (0.0085) and so the values are unchanged by a
        // Core tick-rate move.
        Assert.Equal(8_000, SimulationConstants.MaximumEnergy);
        Assert.Equal(
            SimulationConstants.MaximumEnergy,
            SimulationConstants.MaximumShield);
        Assert.Equal(
            5_000,
            SimulationConstants.JetMinimumEnergyDrainMicroPerRetailTick);
        Assert.Equal(
            12_000,
            SimulationConstants.JetMaximumEnergyDrainMicroPerRetailTick);
        Assert.Equal(20, SimulationConstants.RetailTicksPerSecond);
        Assert.Equal(10, SimulationConstants.JetStrafeAccelerationNumerator);
        Assert.Equal(3, SimulationConstants.JetStrafeAccelerationDenominator);
    }

    [Fact]
    public void CanonicalFlightGate_RejectsTransformUntilReleasedMissionEnablesIt()
    {
        Simulation simulation = CreatePlayingSimulation();

        WorldSnapshot rejected = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));

        Assert.False(rejected.Level100FlightEnabled);
        Assert.Equal(VehicleMode.Walker, rejected.Mode);
        Assert.Equal(VehicleTransition.None, rejected.Transition);
        Assert.Contains(
            rejected.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.TransformRejected);
        Assert.Equal(
            rejected.PlayerGroundElevationMillimeters +
                Level100Terrain.WalkerCenterOfGravityMillimeters,
            rejected.PlayerElevationMillimeters);
        Level100ActorSnapshot player = rejected.Level100Actors.Actors.Single(actor =>
            actor.Name == "Player 1");
        Assert.Equal(
            rejected.PlayerElevationMillimeters,
            player.Pose!.PositionMillimeters.Y);
    }

    [Fact]
    public void Reset_DominatesOtherActionsInTheSameInputSlot()
    {
        Simulation simulation = CreatePlayingSimulation();
        simulation.Step(new SimInput(1, 0, SimActions.Fire));

        WorldSnapshot reset = simulation.Step(new SimInput(
            1,
            1,
            SimActions.Reset | SimActions.Fire | SimActions.ToggleMode));

        Assert.Equal(SimulationConstants.Level100OpeningPanTicks + 2, reset.Tick);
        Assert.Equal(VehicleMode.Walker, reset.Mode);
        Assert.Equal(SimVector2.Zero, reset.PlayerPosition);
        Assert.Equal(SimulationConstants.MaximumEnergy, reset.Energy);
        Assert.Equal(SimulationConstants.MaximumShield, reset.Shield);
        Assert.Empty(reset.Projectiles);
    }

    [Fact]
    public void Movement_IsNotClampedByTheRetiredSyntheticArena()
    {
        Simulation simulation = CreatePlayingSimulation();

        for (int tick = 0; tick < 500; tick++)
        {
            simulation.Step(new SimInput(1, 1));
        }

        long distanceSquared =
            ((long)simulation.Snapshot.PlayerPosition.X * simulation.Snapshot.PlayerPosition.X) +
            ((long)simulation.Snapshot.PlayerPosition.Z * simulation.Snapshot.PlayerPosition.Z);
        Assert.True(distanceSquared > 30_000L * 30_000L);
        Assert.NotEqual(SimVector2.Zero, simulation.Snapshot.PlayerVelocity);
    }

    [Fact]
    public void Level100ObservedFacilityContacts_PreserveSlideAndPreventEntry()
    {
        Simulation towerRun = CreatePlayingSimulation();
        AimAtTargetAndSettle(towerRun, SimulationConstants.Level100ControlTowerPosition);
        bool observedTowerSlide = false;
        for (int tick = 0; tick < 260; tick++)
        {
            WorldSnapshot state = towerRun.Step(new SimInput(0, 1));
            long offsetX = (long)state.PlayerPosition.X -
                SimulationConstants.Level100ControlTowerPosition.X;
            long offsetZ = (long)state.PlayerPosition.Z -
                SimulationConstants.Level100ControlTowerPosition.Z;
            long radius = SimulationConstants.Level100ControlTowerContactRadius;
            Assert.True((offsetX * offsetX) + (offsetZ * offsetZ) >= radius * radius);
            observedTowerSlide |=
                Math.Abs(offsetX * state.PlayerVelocity.Z - offsetZ * state.PlayerVelocity.X) >
                    40_000;
        }

        Assert.True(observedTowerSlide);

        Simulation factoryRun = CreatePlayingSimulation();
        AimAtTargetAndSettle(factoryRun, SimulationConstants.Level100TankFactoryPosition);
        bool reachedFactory = false;
        bool removedFactoryInwardMotion = false;
        for (int tick = 0; tick < 360; tick++)
        {
            WorldSnapshot state = factoryRun.Step(new SimInput(0, 1));
            long offsetX = (long)state.PlayerPosition.X -
                SimulationConstants.Level100TankFactoryPosition.X;
            long offsetZ = (long)state.PlayerPosition.Z -
                SimulationConstants.Level100TankFactoryPosition.Z;
            long radius = SimulationConstants.Level100TankFactoryContactRadius;
            long distanceSquared = (offsetX * offsetX) + (offsetZ * offsetZ);
            Assert.True(distanceSquared >= radius * radius);
            reachedFactory |= distanceSquared <= (radius + 2L) * (radius + 2L);
            if (distanceSquared <= (radius + 2L) * (radius + 2L))
            {
                long radialVelocity =
                    (offsetX * state.PlayerVelocity.X) +
                    (offsetZ * state.PlayerVelocity.Z);
                removedFactoryInwardMotion |= radialVelocity >= -(radius * 2L);
            }
        }

        Assert.True(reachedFactory);
        Assert.True(removedFactoryInwardMotion);
    }

    [Fact]
    public void Level100Triggers_UsePhysicalActorsAndReleasedSideScriptDispatch()
    {
        Simulation simulation = CreatePlayingSimulation();

        AdvanceUntilNavigation(simulation, "Target Zone 1", 500);
        Assert.True(Trigger(simulation, Level100MissionTrigger.TargetZone1).Active);
        Level100ActorScriptContinuationSnapshot targetZonePause =
            DriveUntilTriggerPause(simulation, Level100MissionTrigger.TargetZone1);
        Level100TriggerActorSnapshot targetZone = Trigger(
            simulation,
            Level100MissionTrigger.TargetZone1);
        Assert.False(targetZone.Reached);
        Assert.Equal(Level100ActorScriptWaitKind.Pause, targetZonePause.WaitKind);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(0.5f).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            targetZonePause.WaitArgument);
        Assert.Equal(10, targetZonePause.DueTick - simulation.Snapshot.Level100ActorScripts.Tick);

        // The released Pause is 0.5 s, which is ten ticks at 20 Hz.
        for (int tick = 1; tick < 10; tick++)
        {
            Assert.False(Trigger(
                simulation.Step(SimInput.Idle),
                Level100MissionTrigger.TargetZone1).Reached);
        }

        WorldSnapshot firingRangeAssignment = simulation.Step(SimInput.Idle);
        Assert.True(Trigger(firingRangeAssignment, Level100MissionTrigger.TargetZone1).Reached);
        Assert.Equal("Firing Range", firingRangeAssignment.Level100Mission.NavigationObjective);
        Assert.Contains(
            firingRangeAssignment.Level100MissionEvents.OfType<Level100MessageRequested>(),
            message => message.MessageId == 4458134);

        Level100ActorScriptContinuationSnapshot firingRangePause =
            DriveUntilTriggerPause(simulation, Level100MissionTrigger.FiringRange);
        Assert.Equal(Level100ActorScriptWaitKind.Pause, firingRangePause.WaitKind);
        Assert.Equal(10, firingRangePause.DueTick - simulation.Snapshot.Level100ActorScripts.Tick);
        for (int tick = 0; tick < 10; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(Trigger(simulation, Level100MissionTrigger.FiringRange).Reached);
        Assert.Equal(
            Level100PrimaryObjectiveStatus.Complete,
            simulation.Snapshot.Level100Mission.PrimaryObjectives[0].Status);
        Assert.True(simulation.Snapshot.Level100FiringRangeTargetsActive);
        Assert.False(simulation.Snapshot.Level100PulseCannonEnabled);
        for (int tick = 0; tick < SimulationConstants.TicksPerSecond; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(simulation.Snapshot.Level100PulseCannonEnabled);
        Assert.Single(simulation.Step(new SimInput(0, 0, SimActions.Fire)).Projectiles);
    }

    [Theory]
    [InlineData(SimActions.Fire)]
    [InlineData(SimActions.ChargeWeapon)]
    public void ControllerWeaponInput_PrecedesTheAuthoredEnableCallback(SimActions action)
    {
        Simulation simulation = CreateFiringRangeBeforeWeaponEnable();
        Assert.False(simulation.Snapshot.Level100PulseCannonEnabled);
        Assert.True(simulation.Snapshot.Level100PlayerActive);

        WorldSnapshot enabled = simulation.Step(new SimInput(0, 0, action));
        Assert.True(enabled.Level100PulseCannonEnabled);
        Assert.Empty(enabled.Level100WeaponFireEvents);
        Assert.Empty(enabled.Projectiles);
        Assert.Equal(0u, enabled.Level100PlayerWeaponState.PulseChargeBits);

        WorldSnapshot next = simulation.Step(new SimInput(0, 0, action));
        if (action == SimActions.Fire)
        {
            Assert.Equal(Level100PlayerWeapon.PulseCannonPod,
                Assert.Single(next.Level100WeaponFireEvents).Weapon);
            Assert.Single(next.Projectiles);
        }
        else
        {
            Assert.Equal(0x41200000u, next.Level100PlayerWeaponState.PulseChargeBits);
        }
    }

    [Fact]
    public void ControllerFireAndMove_UseTheRetainedEmitterBeforeMovement()
    {
        Simulation stationary = CreateFiringRangeExerciseSimulation();
        Simulation moving = CreateFiringRangeExerciseSimulation();
        WorldSnapshot before = moving.Snapshot;

        ProjectileSnapshot stationaryRound = Assert.Single(
            stationary.Step(new SimInput(0, 0, SimActions.Fire)).Projectiles);
        WorldSnapshot moved = moving.Step(new SimInput(1, 1, SimActions.Fire, LookX: 1));
        ProjectileSnapshot movingRound = Assert.Single(moved.Projectiles);

        Assert.NotEqual(before.PlayerPosition, moved.PlayerPosition);
        Assert.NotEqual(before.FacingYawMicroRad, moved.FacingYawMicroRad);
        Assert.Equal(stationaryRound.Position, movingRound.Position);
        Assert.Equal(stationaryRound.ElevationMillimeters, movingRound.ElevationMillimeters);
        Assert.Equal(stationaryRound.Velocity, movingRound.Velocity);
        Assert.Equal(stationaryRound.VerticalVelocityMillimetersPerTick,
            movingRound.VerticalVelocityMillimetersPerTick);
    }

    [Fact]
    public void ControllerFire_PrecedesTheAuthoredDisableCallback()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        Assert.True(simulation.Snapshot.Level100PulseCannonEnabled);

        // Isolate dispatch order with the existing mission-input seam. This
        // executes LevelScript's real Abort Airborne Drones handler, including
        // all four DisableWeapon calls; it is not a player-acceptance route.
        WorldSnapshot disabled = simulation.Step(new SimInput(0, 0, SimActions.Fire),
            [new Level100MissionInputFact(Level100MissionInput.AbortAirborneDrones)]);

        Assert.Equal(Level100PlayerWeapon.PulseCannonPod,
            Assert.Single(disabled.Level100WeaponFireEvents).Weapon);
        Assert.Single(disabled.Projectiles);
        Assert.False(disabled.Level100PulseCannonEnabled);
        Assert.False(disabled.Level100VulcanCannonEnabled);
        Assert.False(disabled.Level100MechVulcanCannonEnabled);
        Assert.False(disabled.Level100MissilePodEnabled);
        Assert.Empty(simulation.Step(new SimInput(0, 0, SimActions.Fire))
            .Level100WeaponFireEvents);
    }

    [Fact]
    public void ControllerMorph_BlocksLaterButtonsAndCompletionCannotReopenTheSameInput()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        simulation.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);

        WorldSnapshot started = simulation.Step(new SimInput(0, 0,
            SimActions.ToggleMode | SimActions.ChargeWeapon |
            SimActions.Fire | SimActions.ChangeWeapon));
        Assert.Equal(VehicleTransition.WalkerToJet, started.Transition);
        Assert.Equal(SimulationConstants.WalkerToJetTransitionTicks,
            started.TransformTicksRemaining);
        Assert.Equal(0u, started.Level100PlayerWeaponState.PulseChargeBits);
        Assert.Empty(started.Level100WeaponFireEvents);
        Assert.Equal(Level100MissionWeapon.PulseCannonPod,
            started.Level100WalkerSelectedWeapon);

        for (int tick = 0; simulation.Snapshot.TransformTicksRemaining > 1; tick++)
        {
            Assert.True(tick < 100);
            simulation.Step(SimInput.Idle);
        }

        WorldSnapshot completed = simulation.Step(new SimInput(0, 0,
            SimActions.ChargeWeapon | SimActions.Fire | SimActions.ChangeWeapon));
        Assert.Equal(VehicleMode.Jet, completed.Mode);
        Assert.Equal(VehicleTransition.None, completed.Transition);
        Assert.Empty(completed.Level100WeaponFireEvents);
        Assert.Equal(Level100MissionWeapon.MechVulcanCannon,
            completed.Level100JetSelectedWeapon);
        Assert.Equal(Level100PlayerWeapon.MechVulcanCannon,
            Assert.Single(simulation.Step(new SimInput(0, 0, SimActions.Fire))
                .Level100WeaponFireEvents).Weapon);
    }

    [Fact]
    public void ControllerZoomRequests_FollowShippedOrderAndCannotUseLaterMorphCompletion()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot both = simulation.Step(new SimInput(0, 0,
            SimActions.ZoomIn | SimActions.ZoomOut));
        // ZoomOut row 14 follows ZoomIn row 13.
        Assert.Equal(SimulationConstants.ZoomOutPermille, both.DesiredZoomPermille);
        Assert.Equal(SimulationConstants.ZoomOutPermille, both.ZoomPermille);

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        simulation.Step(new SimInput(0, 0, SimActions.ToggleMode));
        AdvanceUntil(simulation, state => state.Mode == VehicleMode.Jet, 100);
        simulation.Step(new SimInput(0, 0, SimActions.ToggleMode));
        AdvanceUntil(simulation, state => state.TransformTicksRemaining == 1, 100);

        WorldSnapshot completed = simulation.Step(new SimInput(0, 0, SimActions.ZoomIn));
        Assert.Equal(VehicleMode.Walker, completed.Mode);
        Assert.Equal(VehicleTransition.None, completed.Transition);
        Assert.Equal(SimulationConstants.ZoomOutPermille, completed.DesiredZoomPermille);
        Assert.Equal(SimulationConstants.ZoomOutPermille, completed.ZoomPermille);

        WorldSnapshot next = simulation.Step(new SimInput(0, 0, SimActions.ZoomIn));
        Assert.Equal(SimulationConstants.ZoomInPermille, next.DesiredZoomPermille);
        Assert.Equal(SimulationConstants.ZoomOutPermille -
            SimulationConstants.ZoomStepPermillePerTick, next.ZoomPermille);
    }

    /// <summary>
    /// Retail does not launch an adjustable weapon parallel to the centre-screen
    /// ray. <c>CBattleEngine::GetLaunchPosition</c> traces from the current camera
    /// view and rotates the physical cockpit emitter toward that hit point before
    /// projectile inaccuracy is applied. The Pulse Cannon inherits
    /// <c>CWeaponAdjustAim = true</c>; its shipped record carries no override.
    /// </summary>
    [Fact]
    public void PulseCannonEmitter_ConvergesOnTheCameraReticleContact()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank #23");
        AimReticleAtActor(simulation, target, heightAboveOriginMillimeters: 600);

        int healthBefore = target.Health;
        WorldSnapshot state = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Single(state.Projectiles);
        for (int tick = 0; tick < 20 && state.Projectiles.Count > 0; tick++)
        {
            state = simulation.Step(SimInput.Idle);
        }

        Level100ActorSnapshot after = state.Level100Actors.Actors
            .Single(actor => actor.ActorId == target.ActorId);
        Assert.Empty(state.Projectiles);
        Assert.Equal(healthBefore - 1_800, after.Health);
        Assert.Equal(Level100ActorLifecycle.Alive, after.Lifecycle);
        Assert.Equal(Level100MissionOutcome.Running, state.Level100Mission.Outcome);
    }

    [Theory]
    [InlineData(-300)]
    [InlineData(3_000)]
    public void PulseCannonEmitter_DoesNotSnapMissesOutsideTheContactVolume(
        int heightAboveOriginMillimeters)
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank #23");
        AimReticleAtActor(
            simulation,
            target,
            heightAboveOriginMillimeters);

        WorldSnapshot state = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        for (int tick = 0; tick < 120 && state.Projectiles.Count > 0; tick++)
        {
            state = simulation.Step(SimInput.Idle);
        }

        Level100ActorSnapshot after = state.Level100Actors.Actors
            .Single(actor => actor.ActorId == target.ActorId);
        Assert.Empty(state.Projectiles);
        Assert.Equal(target.Health, after.Health);
        Assert.Equal(Level100ActorLifecycle.Alive, after.Lifecycle);
        Assert.Equal(Level100MissionOutcome.Running, state.Level100Mission.Outcome);
    }

    [Fact]
    public void Level100SimulationFailureTape_FirstRunRetainsLossTextAndExactTicks()
    {
        DeterministicSimulationTape first = RunLevel100FailureTape();
        DeterministicSimulationTape repeat = RunLevel100FailureTape();

        Assert.Equal(first.Hashes, repeat.Hashes);
        Assert.Equal(Level100MissionOutcome.Lost, first.Snapshot.Level100Mission.Outcome);
        Assert.Equal(Level100MissionFailureReason.TutorialBroken,
            first.Snapshot.Level100Mission.FailureReason);
        Assert.Equal(1_110_345_999, first.Snapshot.Level100Mission.FailureTextId);
        Assert.Equal(
            Level100MissionTerminalState.FailureCountdownElapsed,
            first.Snapshot.Level100Mission.TerminalState);
        Assert.Equal(0, first.Snapshot.Level100Mission.TerminalTicksRemaining);

        Simulation death = CreatePlayingSimulation();
        WorldSnapshot declared = death.Step(
            SimInput.Idle,
            [new Level100PlayerDeathFact()]);
        Assert.Equal(Level100MissionFailureReason.PlayerDeath,
            declared.Level100Mission.FailureReason);
        Assert.Equal(
            Level100MissionTiming.DeathPauseDelayTicks,
            declared.Level100Mission.TerminalTicksRemaining);
        Assert.Equal(0x3f800000, MixBits(declared));

        int actorScriptTickAtDeclaration = declared.Level100ActorScripts.Tick;
        WorldSnapshot advancing = declared;
        for (int elapsed = 1;
             elapsed < Level100MissionTiming.DeathPauseDelayTicks;
             elapsed++)
        {
            advancing = death.Step(SimInput.Idle);
            Assert.Equal(
                actorScriptTickAtDeclaration + elapsed,
                advancing.Level100ActorScripts.Tick);
            if (elapsed == 1)
            {
                Assert.Equal(0x3f7f3b64, MixBits(advancing));
            }
            if (elapsed == Level100MissionTiming.FailureMenuDelayTicks)
            {
                Assert.Equal(
                    Level100MissionTerminalState.FailureMenuReady,
                    advancing.Level100Mission.TerminalState);
            }
        }

        Assert.Equal(1, advancing.Level100Mission.TerminalTicksRemaining);
        WorldSnapshot paused = death.Step(SimInput.Idle);
        Assert.Equal(0, paused.Level100Mission.TerminalTicksRemaining);
        Assert.Equal(
            Level100MissionTerminalState.FailureCountdownElapsed,
            paused.Level100Mission.TerminalState);
        Assert.Equal(
            actorScriptTickAtDeclaration +
                Level100MissionTiming.DeathPauseDelayTicks - 1,
            paused.Level100ActorScripts.Tick);
        // AdvanceTime precedes the event Flush that delivers PAUSE_GAME,
        // unlike actor-script work which this nominal terminal tick skips.
        Assert.Equal(advancing.RetailEventFrameCount + 1, paused.RetailEventFrameCount);
        Assert.Equal((uint)paused.Level100Mission.Tick, paused.RetailEventFrameCount);
        Assert.Equal(0x3dcccb3b, MixBits(paused));

        uint pausedTimeBits = BitConverter.SingleToUInt32Bits(death.EngineTimeSeconds);
        WorldSnapshot held = death.Step(SimInput.Idle);
        Assert.Equal(paused.Level100ActorScripts.Tick, held.Level100ActorScripts.Tick);
        Assert.Equal(paused.RetailEventFrameCount, held.RetailEventFrameCount);
        Assert.Equal(paused.Level100Mission.Tick + 1, held.Level100Mission.Tick);
        Assert.Equal(pausedTimeBits, BitConverter.SingleToUInt32Bits(death.EngineTimeSeconds));
        // The unit callbacks of the retail load order select schema 52.
        Assert.Equal(52, CanonicalSchemaVersion(held));
        Assert.Equal(held.RetailEventFrameCount, held.Level100ActorMechanics.PlaneEvents!.FrameCount);
        Assert.Equal(0x3dcccb3b, MixBits(held));

        static int MixBits(WorldSnapshot snapshot) => BitConverter.SingleToInt32Bits(
            Level100MissionTiming.GameplayMix(
                snapshot.Level100Mission.Outcome,
                snapshot.Level100Mission.FailureReason,
                snapshot.Level100Mission.TerminalTicksRemaining));
    }

    [Fact]
    public void PlayerWeaponReloads_UseStoredFloatTimesAndRefuseEquality()
    {
        Simulation pulse = CreateFiringRangeExerciseSimulation();
        AlignNextShotToEquality(pulse, 0.1f, 2);

        WorldSnapshot pulseFirst = pulse.Step(new SimInput(0, 0, SimActions.Fire));
        AssertWeaponFire(pulseFirst, Level100PlayerWeapon.PulseCannonPod, 1);
        Assert.All(
            pulseFirst.Projectiles,
            projectile => Assert.Equal(
                Level100ProjectileKind.MechPulseBoltMedium,
                projectile.Kind));
        Assert.Equal(2, pulseFirst.FireCooldownTicksRemaining);
        WorldSnapshot pulseBlocked = pulse.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Empty(pulseBlocked.Level100WeaponFireEvents);
        Assert.Equal(1, pulseBlocked.FireCooldownTicksRemaining);
        WorldSnapshot pulseEquality = pulse.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Empty(pulseEquality.Level100WeaponFireEvents);
        Assert.Equal(0, pulseEquality.FireCooldownTicksRemaining);
        Assert.Equal(pulseFirst.Level100PlayerWeaponState.PulseReadyAtTimeBits,
            BitConverter.SingleToUInt32Bits(pulse.EngineTimeSeconds));
        WorldSnapshot pulseSecond = pulse.Step(new SimInput(0, 0, SimActions.Fire));
        AssertWeaponFire(pulseSecond, Level100PlayerWeapon.PulseCannonPod, 1);
        Assert.Equal(2, pulseSecond.FireCooldownTicksRemaining);

        Simulation jet = CreatePlayingSimulation();
        jet.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        jet.Step(new SimInput(0, 0, SimActions.ToggleMode));
        for (int tick = 0;
             jet.Snapshot.Mode != VehicleMode.Jet ||
                 jet.Snapshot.Transition != VehicleTransition.None;
             tick++)
        {
            Assert.True(tick < 100, "Walker-to-jet morph did not complete.");
            jet.Step(SimInput.Idle);
        }

        AlignNextShotToEquality(jet, 0.05f, 1);
        WorldSnapshot jetFirst = jet.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        AssertWeaponFire(
            jetFirst,
            Level100PlayerWeapon.MechVulcanCannon,
            SimulationConstants.MechVulcanVolleySize);
        Assert.All(
            jetFirst.Projectiles,
            projectile => Assert.Equal(
                Level100ProjectileKind.MechAirBullet,
                projectile.Kind));
        Assert.Equal(1, jetFirst.FireCooldownTicksRemaining);
        WorldSnapshot jetEquality = jet.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        Assert.Empty(jetEquality.Level100WeaponFireEvents);
        Assert.Equal(0, jetEquality.FireCooldownTicksRemaining);
        Assert.Equal(jetFirst.Level100PlayerWeaponState.MechVulcanReadyAtTimeBits,
            BitConverter.SingleToUInt32Bits(jet.EngineTimeSeconds));
        WorldSnapshot jetSecond = jet.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        AssertWeaponFire(
            jetSecond,
            Level100PlayerWeapon.MechVulcanCannon,
            SimulationConstants.MechVulcanVolleySize);
        Assert.Equal(1, jetSecond.FireCooldownTicksRemaining);

        static void AlignNextShotToEquality(Simulation simulation, float reload, uint delay)
        {
            // A nominal delay of one/two ticks can land below, at, or above
            // the separately stored ready word. Choose a real equality; a
            // whole-second shot alone does not guarantee that relationship.
            for (int step = 0; ; step++)
            {
                Assert.True(step < 20, "No equality boundary within twenty frames.");
                uint next = simulation.Snapshot.RetailEventFrameCount + 1;
                float ready = (float)((double)RetailEventScheduler.TimeAtFrameCount(next) + reload);
                if (ready == RetailEventScheduler.TimeAtFrameCount(next + delay))
                    return;
                simulation.Step(SimInput.Idle);
            }
        }

        static void AssertWeaponFire(
            WorldSnapshot snapshot,
            Level100PlayerWeapon weapon,
            int roundCount)
        {
            Level100WeaponFireEvent fired = Assert.Single(snapshot.Level100WeaponFireEvents);
            Assert.Equal(weapon, fired.Weapon);
            Assert.Equal(roundCount, fired.RoundCount);
        }
    }

    [Fact]
    public void HeldJetVulcanButton_ClientSampledInputRepeatsRealVolleysWithoutRelease()
    {
        Simulation jet = CreatePlayingSimulation();
        jet.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        jet.Step(new SimInput(0, 0, SimActions.ToggleMode));
        for (int step = 0; jet.Snapshot.Mode != VehicleMode.Jet ||
            jet.Snapshot.Transition != VehicleTransition.None; step++)
        {
            Assert.True(step < 100);
            jet.Step(SimInput.Idle);
        }

        // Compose the production physical-input sampler with the existing
        // isolated flight fixture. No Fire action is manufactured here, and
        // no claim is made that this setup completes the earlier exercises.
        var inputSampler = new InteractiveSession(1u, Level100TestActorDefinitions.Create());
        inputSampler.ObserveInput(new InteractiveInput(0, 0, true, false, false));
        float expectedReady = -200f;
        int volleys = 0;
        for (int step = 0; step < 20; step++)
        {
            Assert.Equal(1, inputSampler.AdvanceFrameTicks(500_000).StepsAdvanced);
            SimInput input = inputSampler.LastConsumedInput!.Value;
            Assert.True(input.HasAction(SimActions.ChargeWeapon));
            Assert.False(input.HasAction(SimActions.Fire));
            int previousNextId = jet.Snapshot.NextProjectileId;
            WorldSnapshot fired = jet.Step(input);
            float now = RetailEventScheduler.TimeAtFrameCount(fired.RetailEventFrameCount);
            if (now > expectedReady)
            {
                Level100WeaponFireEvent volley = Assert.Single(fired.Level100WeaponFireEvents);
                Assert.Equal(Level100PlayerWeapon.MechVulcanCannon, volley.Weapon);
                Assert.Equal(2, volley.RoundCount);
                Assert.Equal(previousNextId + 2, fired.NextProjectileId);
                expectedReady = (float)((double)now + (double)0.05f);
                volleys++;
            }
            else
            {
                Assert.Empty(fired.Level100WeaponFireEvents);
                Assert.Equal(previousNextId, fired.NextProjectileId);
            }
            Assert.Equal(BitConverter.SingleToUInt32Bits(expectedReady),
                fired.Level100PlayerWeaponState.MechVulcanReadyAtTimeBits);
        }
        Assert.InRange(volleys, 9, 20);
        Assert.Equal(20, inputSampler.Metrics.FireHeldTicksSampled);
        Assert.Equal(0, inputSampler.Metrics.FirePulseEdgesConsumed);
    }

    [Fact]
    public void PlayerProjectilesConsumeReleasedScatterInRetailDrawOrder()
    {
        Simulation pulse = CreateFiringRangeExerciseSimulation();
        Simulation pulseTwin = CreateFiringRangeExerciseSimulation();
        int pulseSeed = pulse.Snapshot.Level100ActorMechanics.ReleasedRandomSeed;
        var pulseRandom = new Level100ReleasedRandom(pulseSeed);
        // `Mech Pulse Cannon Charged` carries +0 CWeaponInaccuracy: the pair
        // is drawn and scaled to nothing.
        (int PulseYaw, int PulsePitch) pulseOffset = NextOffsets(pulseRandom, 0);
        Assert.Equal((0, 0), pulseOffset);
        // Per round the spawner then takes the round's Actor Init draw and,
        // for a Battle Engine weapon with CWeaponPower >= 0.001, RecoilWeapon's
        // three shake draws (Mech Pulse Cannon Charged: 0.03).
        AdvanceAfterScatter(pulseRandom, recoil: true);

        WorldSnapshot pulseBefore = pulse.Snapshot;
        WorldSnapshot pulseShot = pulse.Step(new SimInput(0, 0, SimActions.Fire));
        AdvanceByEventDraws(pulseRandom, pulseTwin);
        Assert.Equal(
            pulseRandom.Seed,
            pulseShot.Level100ActorMechanics.ReleasedRandomSeed);
        AssertDirection(
            Assert.Single(pulseShot.Projectiles),
            pulseBefore.FacingYawMicroRad,
            pulseBefore.FacingPitchMicroRad,
            pulseOffset);

        Simulation chargedPulse = CreateFiringRangeExerciseSimulation();
        Simulation chargedTwin = CreateFiringRangeExerciseSimulation();
        for (int sample = 0; sample < 10; sample++)
        {
            chargedPulse.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
            chargedTwin.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        }
        int chargedSeed = chargedPulse.Snapshot.Level100ActorMechanics.ReleasedRandomSeed;
        var chargedRandom = new Level100ReleasedRandom(chargedSeed);
        // The pristine scatter block calls the shared stream at 0x00506E0A
        // and 0x00506E3E before each mode+0x34 multiply. Charged2's +0
        // inaccuracy removes the offsets, never either draw. This checks only
        // that bounded scatter stage, not every retail constructor/effect draw.
        chargedRandom.Next();
        chargedRandom.Next();
        // Charged 2 carries CWeaponPower 0.05, so its recoil draws too.
        AdvanceAfterScatter(chargedRandom, recoil: true);
        Assert.NotEqual(chargedSeed, chargedRandom.Seed);
        WorldSnapshot chargedBefore = chargedPulse.Snapshot;
        WorldSnapshot chargedShot = chargedPulse.Step(new SimInput(0, 0, SimActions.Fire));
        ProjectileSnapshot chargedRound = Assert.Single(chargedShot.Projectiles);
        Assert.Equal(Level100ProjectileKind.MechPulseBoltLarge, chargedRound.Kind);
        AdvanceByEventDraws(chargedRandom, chargedTwin);
        Assert.Equal(chargedRandom.Seed, chargedShot.Level100ActorMechanics.ReleasedRandomSeed);
        AssertDirection(chargedRound, chargedBefore.FacingYawMicroRad,
            chargedBefore.FacingPitchMicroRad, (0, 0));

        Simulation jet = CreatePlayingSimulation();
        Simulation jetTwin = CreatePlayingSimulation();
        foreach (Simulation flight in new[] { jet, jetTwin })
        {
            flight.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
            flight.Step(new SimInput(0, 0, SimActions.ToggleMode));
            for (int tick = 0;
                 flight.Snapshot.Mode != VehicleMode.Jet ||
                     flight.Snapshot.Transition != VehicleTransition.None;
                 tick++)
            {
                Assert.True(tick < 100, "Walker-to-jet morph did not complete.");
                flight.Step(SimInput.Idle);
            }
        }

        int jetSeed = jet.Snapshot.Level100ActorMechanics.ReleasedRandomSeed;
        var jetRandom = new Level100ReleasedRandom(jetSeed);
        // Each bullet takes its scatter pair then its Actor Init draw; the
        // Mech Vulcan Cannon has no CWeaponPower, so no recoil draws.
        (int Yaw, int Pitch)[] jetOffsets = Enumerable.Range(
                0,
                SimulationConstants.MechVulcanVolleySize)
            .Select(_ =>
            {
                (int Yaw, int Pitch) offsets = NextOffsets(
                    jetRandom,
                    SimulationConstants.PlayerVulcanInaccuracyMicroRadians);
                AdvanceAfterScatter(jetRandom, recoil: false);
                return offsets;
            })
            .ToArray();

        // Controller Fire samples the retained emitter. Jet Move changes the
        // facing later in this same update, after the round already exists.
        WorldSnapshot jetBefore = jet.Snapshot;
        WorldSnapshot jetShot = jet.Step(new SimInput(0, 0, SimActions.Fire));
        AdvanceByEventDraws(jetRandom, jetTwin);
        Assert.Equal(jetRandom.Seed, jetShot.Level100ActorMechanics.ReleasedRandomSeed);
        ProjectileSnapshot[] rounds = jetShot.Projectiles.OrderBy(item => item.Id).ToArray();
        Assert.Equal(jetOffsets.Length, rounds.Length);
        for (int index = 0; index < rounds.Length; index++)
        {
            AssertDirection(
                rounds[index],
                jetBefore.FacingYawMicroRad,
                jetBefore.FacingPitchMicroRad,
                jetOffsets[index]);
        }
        Assert.NotEqual(rounds[0].Velocity, rounds[1].Velocity);
        Assert.NotEqual(
            rounds[0].VerticalVelocityMillimetersPerTick,
            rounds[1].VerticalVelocityMillimetersPerTick);

        // The controller fires before the event flush, so scatter takes the
        // first draws of the update. The flush's own draws (the Battle
        // Engine's refreshes, the aircraft callbacks and every unit callback)
        // do not depend on the shot, so an identical twin stepped idle takes
        // exactly as many; count them along the stream.
        static void AdvanceByEventDraws(Level100ReleasedRandom random, Simulation twin)
        {
            var probe = new Level100ReleasedRandom(twin.Snapshot.Level100ActorMechanics.ReleasedRandomSeed);
            int after = twin.Step(SimInput.Idle).Level100ActorMechanics.ReleasedRandomSeed;
            int draws = 0;
            while (probe.Seed != after)
            {
                probe.Next();
                Assert.True(++draws < 10_000, "The idle twin's draws were not found on the stream.");
            }
            for (int draw = 0; draw < draws; draw++)
            {
                random.Next();
            }
        }

        static void AdvanceAfterScatter(Level100ReleasedRandom random, bool recoil)
        {
            random.Next();
            if (recoil)
            {
                random.Next();
                random.Next();
                random.Next();
            }
        }

        static (int Yaw, int Pitch) NextOffsets(
            Level100ReleasedRandom random,
            int inaccuracyMicroRadians)
        {
            int first = random.NextSignedUnitScaled(inaccuracyMicroRadians);
            int second = random.NextSignedUnitScaled(inaccuracyMicroRadians);
            return (second, first);
        }

        // Every one of these modes lacks CWeaponLaunchAngle entries, so its
        // basis is facing × FMatrix(0, 2π/4096, 0) × the scatter matrix.
        static void AssertDirection(
            ProjectileSnapshot projectile,
            int facingYaw,
            int facingPitch,
            (int Yaw, int Pitch) offset)
        {
            (double expectedYaw, double expectedPitch) = RetailLaunchHeading(
                facingYaw / 1e6,
                facingPitch / 1e6,
                0.0,
                2.0 * Math.PI / 4096.0,
                offset.Yaw / 1e6,
                offset.Pitch / 1e6);
            int actualYaw = (int)Math.Round(
                Math.Atan2(-projectile.Velocity.X, projectile.Velocity.Z) * 1_000_000d,
                MidpointRounding.AwayFromZero);
            long horizontalSquared =
                ((long)projectile.Velocity.X * projectile.Velocity.X) +
                ((long)projectile.Velocity.Z * projectile.Velocity.Z);
            int actualPitch = (int)Math.Round(
                Math.Atan2(
                    -projectile.VerticalVelocityMillimetersPerTick,
                    Math.Sqrt(horizontalSquared)) * 1_000_000d,
                MidpointRounding.AwayFromZero);

            Assert.InRange(
                Normalize(actualYaw - (int)Math.Round(expectedYaw * 1e6)),
                -500,
                500);
            Assert.InRange(
                Normalize(actualPitch - (int)Math.Round(expectedPitch * 1e6)),
                -500,
                500);
        }

        static int Normalize(int angle)
        {
            const int Pi = 3_141_593;
            const int Tau = Pi * 2;
            int normalized = angle % Tau;
            if (normalized > Pi)
            {
                normalized -= Tau;
            }
            else if (normalized < -Pi)
            {
                normalized += Tau;
            }
            return normalized;
        }
    }

    [Theory]
    [InlineData(Level100ProjectileKind.MechBullet, false)]
    [InlineData(Level100ProjectileKind.MechBullet, true)]
    [InlineData(Level100ProjectileKind.MechAirBullet, false)]
    [InlineData(Level100ProjectileKind.MechAirBullet, true)]
    public void VulcanRoundsSelectTheirImpactKindThroughProductionProjectileUpdate(
        Level100ProjectileKind projectileKind,
        bool hitMesh)
    {
        var simulation = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank 2");
        SimVector3 start;
        SimVector3 end;
        int expectedActorId;
        if (hitMesh)
        {
            SimVector3 position = target.Pose.PositionMillimeters;
            start = position with { Y = position.Y + 2_000 };
            end = position with { Y = position.Y - 1_000 };
            expectedActorId = target.ActorId.Value;
        }
        else
        {
            start = new SimVector3(0, 2_000, 0);
            end = new SimVector3(0, -2_000, 0);
            expectedActorId = 0;
        }

        simulation.QueueRoundForContactMeasurement(
            projectileKind,
            start,
            end);
        WorldSnapshot result = simulation.Step(SimInput.Idle);

        Level100DestructionEvent impact = Assert.Single(
            result.Level100DestructionEvents,
            item => item.Kind == Level100DestructionEventKind.VulcanImpact);
        Assert.Equal(Level100DestructionEventKind.VulcanImpact, impact.Kind);
        Assert.Equal(
            Level100DestructionEffectKind.VulcanImpact,
            impact.EffectKind);
        Assert.Equal(expectedActorId, impact.ActorId);
        Assert.Empty(result.Projectiles);
    }

    [Theory]
    [InlineData(Level100ProjectileKind.MechPulseBoltMedium, 1_750, 120)]
    [InlineData(Level100ProjectileKind.MechPulseBoltLarge, 1_000, 140)]
    public void PitchedPulseRound_FollowsViewPitchWithoutInventingVerticalTargetHits(
        Level100ProjectileKind expectedKind, int speedPerTick, int lifetimeTicks)
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        TargetSnapshot target = simulation.Snapshot.Targets.Single(item => item.Id == 1);
        AimAtTarget(simulation, target.Position);
        for (int tick = 0; tick < 100; tick++)
        {
            simulation.Step(new SimInput(0, 0, LookY: -1));
        }
        if (expectedKind == Level100ProjectileKind.MechPulseBoltLarge)
        {
            for (int tick = 0; tick < 10; tick++)
                simulation.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        }

        WorldSnapshot fired = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        ProjectileSnapshot projectile = Assert.Single(fired.Projectiles);
        Assert.Equal(expectedKind, projectile.Kind);
        // CActor::Init files the round's MOVE for the next frame, so the
        // launch frame leaves it at its emitter (the RE lane's round Frames).
        Assert.Equal(lifetimeTicks, projectile.RemainingTicks);
        Assert.InRange(fired.FacingPitchMicroRad, -1_000_000, -800_000);
        Assert.True(projectile.VerticalVelocityMillimetersPerTick > 0);
        long speedSquared =
            ((long)projectile.Velocity.X * projectile.Velocity.X) +
            ((long)projectile.Velocity.Z * projectile.Velocity.Z) +
            ((long)projectile.VerticalVelocityMillimetersPerTick *
                projectile.VerticalVelocityMillimetersPerTick);
        Assert.InRange(speedSquared,
            (long)(speedPerTick - 1) * (speedPerTick - 1),
            (long)(speedPerTick + 1) * (speedPerTick + 1));
        // Gun 1 of cockpit2.msh through the body orientation, roll included.
        double yaw = fired.FacingYawMicroRad / 1_000_000d;
        double pitch = fired.FacingPitchMicroRad / 1_000_000d;
        double roll = fired.BodyRollMicroRad / 1_000_000d;
        (double X, double Y, double Z) gunOffset = CockpitEmitterOffset(
            Level100CockpitEmitters.Gun(Level100CockpitEmitters.PulseGun, walkPose: true), yaw, pitch, roll);
        int expectedEmitterOffsetX = (int)Math.Round(gunOffset.X, MidpointRounding.AwayFromZero);
        int expectedEmitterOffsetZ = (int)Math.Round(gunOffset.Z, MidpointRounding.AwayFromZero);
        Assert.InRange(
            projectile.Position.X - fired.PlayerPosition.X,
            expectedEmitterOffsetX - 1,
            expectedEmitterOffsetX + 1);
        Assert.InRange(
            projectile.Position.Z - fired.PlayerPosition.Z,
            expectedEmitterOffsetZ - 1,
            expectedEmitterOffsetZ + 1);
        int emitterVerticalOffset = (int)Math.Round(gunOffset.Y, MidpointRounding.AwayFromZero);
        Assert.Equal(
            fired.PlayerElevationMillimeters + emitterVerticalOffset,
            projectile.ElevationMillimeters);

        int launchElevation = projectile.ElevationMillimeters;
        projectile = Assert.Single(simulation.Step(SimInput.Idle).Projectiles);
        Assert.Equal(lifetimeTicks - 1, projectile.RemainingTicks);
        Assert.Equal(
            launchElevation + projectile.VerticalVelocityMillimetersPerTick,
            projectile.ElevationMillimeters);
        for (int remaining = lifetimeTicks - 2; remaining >= 1; remaining--)
        {
            projectile = Assert.Single(simulation.Step(SimInput.Idle).Projectiles);
            Assert.Equal(remaining, projectile.RemainingTicks);
        }
        // Frame N + L delivers the life event, then the last MOVE: L Moves
        // in all (k + 1, k = floor((life - 0.001) x 20)).
        Assert.Empty(simulation.Step(SimInput.Idle).Projectiles);

        Assert.Equal(
            SimulationConstants.Level100TargetTankLife,
            simulation.Snapshot.Targets.Single(item => item.Id == 1).Hull);
    }

    [Theory]
    [InlineData(Level100ProjectileKind.MechPulseBoltMedium, false)]
    [InlineData(Level100ProjectileKind.MechPulseBoltLarge, true)]
    public void PulseRadius_ReachesProductionTerrainSweepWithoutFallingBackToMedium(
        Level100ProjectileKind kind, bool touchesTerrain)
    {
        var simulation = new Simulation(1, Level100TestActorDefinitions.Create());
        // Existing retained terrain at (0,0) is 211 mm high. This horizontal
        // segment is 189 mm above it: the Medium 70 mm sphere misses, while
        // the Large 200 mm sphere contacts. No impact event is injected.
        simulation.QueueRoundForContactMeasurement(kind,
            new SimVector3(0, 400, 0), new SimVector3(10, 400, 0));
        WorldSnapshot state = simulation.Step(SimInput.Idle);

        if (touchesTerrain)
        {
            Assert.Empty(state.Projectiles);
            Level100DestructionEvent impact = Assert.Single(state.Level100DestructionEvents);
            Assert.Equal(Level100DestructionEventKind.PulseImpact, impact.Kind);
            Assert.Equal(0, impact.ActorId);
            Assert.Equal(-1, impact.PartIndex);
        }
        else
        {
            Assert.Single(state.Projectiles);
            Assert.Empty(state.Level100DestructionEvents);
        }
    }

    [Theory]
    [InlineData(Level100ProjectileKind.MechPulseBoltMedium, 0x40866666u, 2, false)]
    [InlineData(Level100ProjectileKind.MechPulseBoltLarge, 0xc0000000u, 1, true)]
    public void PulseMeshContact_AppliesLargeDirectEightWithoutAMediumOrFixedLargeBlast(
        Level100ProjectileKind kind, uint remainingLife, int damageStages, bool terminal)
    {
        var simulation = new Simulation(1, Level100TestActorDefinitions.Create());
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank 2");
        SimVector3 position = target.Pose.PositionMillimeters;
        // The existing contact seam only supplies a real swept segment. The
        // production projectile update selects the mesh, damage and lifecycle.
        simulation.QueueRoundForContactMeasurement(kind,
            position with { Y = position.Y + 2_000 },
            position with { Y = position.Y - 1_000 });
        WorldSnapshot state = simulation.Step(SimInput.Idle);

        Assert.Empty(state.Projectiles);
        Level100DestructionEvent impact = Assert.Single(state.Level100DestructionEvents,
            item => item.Kind == Level100DestructionEventKind.PulseImpact);
        Assert.Equal(target.ActorId.Value, impact.ActorId);
        Assert.True(impact.PartIndex >= 0);
        Level100DestructionEvent[] stages = state.Level100DestructionEvents
            .Where(item => item.Kind == Level100DestructionEventKind.SegmentDamaged).ToArray();
        Assert.Equal(damageStages, stages.Length);
        Assert.Equal(remainingLife, stages[^1].RemainingHealthBits);
        Level100DestructionSnapshot destruction = state.Level100Destruction.Actors
            .Single(actor => actor.ActorId == target.ActorId.Value);
        Assert.Equal(remainingLife, destruction.CurrentLifeBits);
        Assert.Equal(terminal, destruction.Terminal);
        // The existing Ground Unit death owner retains it until its scheduled
        // shutdown; damage terminal is not immediate destruction.
        Assert.Equal(terminal ? Level100ActorLifecycle.DiedAwaitingShutdown : Level100ActorLifecycle.Alive,
            state.Level100Actors.Actors.Single(actor => actor.ActorId == target.ActorId).Lifecycle);
    }

    [Fact]
    public void Reset_RestoresDynamicStateWithoutRewindingReplayTick()
    {
        Simulation simulation = CreatePlayingSimulation(42);
        simulation.Step(new SimInput(1, 0));
        simulation.Step(new SimInput(0, 0, SimActions.ToggleMode));

        WorldSnapshot reset = simulation.Step(new SimInput(0, 0, SimActions.Reset));

        Assert.Equal(SimulationConstants.Level100OpeningPanTicks + 3, reset.Tick);
        Assert.Equal(VehicleMode.Walker, reset.Mode);
        Assert.Equal(SimVector2.Zero, reset.PlayerPosition);
        Assert.Equal(SimulationConstants.MaximumEnergy, reset.Energy);
        Assert.Equal(SimulationConstants.MaximumShield, reset.Shield);
        Assert.Equal(0, reset.TargetsDestroyed);
        Assert.Empty(reset.Projectiles);
    }

    [Fact]
    public void HoldingChargeWeapon_FillsThePulseCannonPodInTenTicks()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        Assert.True(simulation.Snapshot.Level100PulseCannonEnabled);
        Assert.Equal(
            Level100MissionWeapon.PulseCannonPod,
            simulation.Snapshot.Level100WalkerSelectedWeapon);

        var charge = new SimInput(0, 0, SimActions.ChargeWeapon);
        charge.Validate();
        Assert.Equal(0x00000000u, simulation.Level100PulseCannonChargeBits);

        for (int sample = 0; sample < 10; sample++)
        {
            simulation.Step(charge);
        }

        Assert.Equal(0x42C80000u, simulation.Level100PulseCannonChargeBits);
        Assert.Equal(0x42C80000u, simulation.Snapshot.Level100PlayerWeaponState.PulseChargeBits);
        simulation.Step(charge);
        Assert.Equal(0x42C80000u, simulation.Level100PulseCannonChargeBits);
    }

    /// <summary>
    /// Retail <c>CBattleEngine::Morph</c> calls the walker and jet part
    /// <c>LoseWeaponCharge</c> methods after its flight/state/special-move
    /// guards and before it selects a transform direction. An accepted
    /// walker-to-jet request therefore clears the currently selected Pulse
    /// Cannon Pod's raw accumulator on the request tick.
    /// </summary>
    [Fact]
    public void AcceptedMorph_LosesCurrentPulseCannonChargeBeforeDirectionSelection()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        simulation.Step(new SimInput(0, 0, SimActions.ChargeWeapon));
        Assert.Equal(0x41200000u, simulation.Level100PulseCannonChargeBits);

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        WorldSnapshot morphing = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));

        Assert.Equal(VehicleTransition.WalkerToJet, morphing.Transition);
        Assert.Equal(0x00000000u, simulation.Level100PulseCannonChargeBits);
    }

    [Fact]
    public void ChargedPulseRelease_ClearsChargeUsesItsReloadAndNextTapSelectsMedium()
    {
        Simulation tap = CreateFiringRangeExerciseSimulation();
        WorldSnapshot tapFired = tap.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Equal(
            Level100ProjectileKind.MechPulseBoltMedium,
            Assert.Single(tapFired.Projectiles).Kind);

        Simulation charged = CreateFiringRangeExerciseSimulation();
        var charge = new SimInput(0, 0, SimActions.ChargeWeapon);
        charge.Validate();
        for (int sample = 0; sample < 10; sample++)
        {
            charged.Step(charge);
        }

        Assert.Equal(0x42C80000u, charged.Level100PulseCannonChargeBits);
        WorldSnapshot chargedFired = charged.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Equal(
            Level100ProjectileKind.MechPulseBoltLarge,
            Assert.Single(chargedFired.Projectiles).Kind);
        Assert.Equal(0u, chargedFired.Level100PlayerWeaponState.PulseChargeBits);
        float ready = (float)((double)charged.EngineTimeSeconds + 0.5d);
        Assert.Equal(BitConverter.SingleToUInt32Bits(ready),
            chargedFired.Level100PlayerWeaponState.PulseReadyAtTimeBits);

        WorldSnapshot nextTap;
        do
        {
            nextTap = charged.Step(new SimInput(0, 0, SimActions.Fire));
            Assert.Equal(0u, nextTap.Level100PlayerWeaponState.PulseChargeBits);
            if (charged.EngineTimeSeconds <= ready)
                Assert.Empty(nextTap.Level100WeaponFireEvents);
            Assert.True(nextTap.Tick <= chargedFired.Tick + 11);
        }
        while (nextTap.Level100WeaponFireEvents.Count == 0);
        Assert.Equal(Level100ProjectileKind.MechPulseBoltMedium,
            nextTap.Projectiles.MaxBy(projectile => projectile.Id)!.Kind);
    }

    [Fact]
    public void AfterPulseFire_ChargeWaitsUntilReloadStrictlyElapses()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        var charge = new SimInput(0, 0, SimActions.ChargeWeapon);
        charge.Validate();

        WorldSnapshot fired = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        Assert.Equal(
            Level100PlayerWeapon.PulseCannonPod,
            Assert.Single(fired.Level100WeaponFireEvents).Weapon);
        Assert.Equal(SimulationConstants.PulseCannonReloadTicks, fired.FireCooldownTicksRemaining);
        Assert.Equal(0x00000000u, simulation.Level100PulseCannonChargeBits);

        // ReadyToCharge at 0x0050A080 is `now > weapon+0x64` (`test ah, 0x41`
        // / jz). Fire stamps +0x64 = now + CWeaponReloadTime 0.1 s, so the
        // equality word is still blocked, but the two-update clock word is
        // not always equal to the separately rounded ready time.
        float ready = BitConverter.UInt32BitsToSingle(
            fired.Level100PlayerWeaponState.PulseReadyAtTimeBits);
        for (int step = 0; ; step++)
        {
            Assert.True(step < 3, "Pulse charge remained blocked beyond its reload.");
            simulation.Step(charge);
            if (simulation.EngineTimeSeconds > ready)
            {
                Assert.Equal(0x41200000u, simulation.Level100PulseCannonChargeBits);
                break;
            }
            Assert.Equal(0u, simulation.Level100PulseCannonChargeBits);
        }
    }

    [Fact]
    public void SnapshotCollections_DoNotExposeMutableArrays()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot state = simulation.Step(new SimInput(0, 0, SimActions.Fire));

        Assert.False(state.Targets.GetType().IsArray);
        Assert.False(state.Projectiles.GetType().IsArray);
        Assert.False(state.Level100MissionEvents.GetType().IsArray);
        Assert.False(state.Level100TriggerActors.GetType().IsArray);
        Assert.False(state.AquilaFlightEventLog.GetType().IsArray);

        var targets = Assert.IsAssignableFrom<IList<TargetSnapshot>>(state.Targets);
        var projectiles = Assert.IsAssignableFrom<IList<ProjectileSnapshot>>(state.Projectiles);
        var flightEvents =
            Assert.IsAssignableFrom<IList<AquilaFlightEvent>>(state.AquilaFlightEventLog);
        Assert.True(targets.IsReadOnly);
        Assert.True(projectiles.IsReadOnly);
        Assert.True(flightEvents.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => targets[0] = targets[0] with { Hull = 0 });
    }

    private static void DriveIntoTrigger(
        Simulation simulation,
        Level100MissionTrigger trigger)
    {
        Level100ActorScriptContinuationSnapshot pause =
            DriveUntilTriggerPause(simulation, trigger);
        // The due tick is on the scripts' clock, which the pre-run started.
        int dueTick = Assert.IsType<int>(pause.DueTick);
        while (simulation.Snapshot.Level100ActorScripts.Tick < dueTick)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(Trigger(simulation, trigger).Reached);
    }

    private static Level100ActorScriptContinuationSnapshot DriveUntilTriggerPause(
        Simulation simulation,
        Level100MissionTrigger trigger)
    {
        Level100TriggerActorSnapshot releasedActor = Trigger(simulation, trigger);
        Assert.True(releasedActor.Active);
        SimVector2 destination = releasedActor.Position;
        for (int tick = 0; tick < 4_000; tick++)
        {
            WorldSnapshot state = simulation.Snapshot;
            Level100ActorSnapshot actor = state.Level100Actors.Actors.Single(
                item => item.Trigger == trigger);
            Level100ActorScriptContinuationSnapshot? pause = state.Level100ActorScripts.Instances
                .Single(item => item.ActorId == actor.ActorId)
                .Continuations
                .SingleOrDefault(item => item.WaitKind == Level100ActorScriptWaitKind.Pause);
            if (pause is not null)
            {
                return pause;
            }

            long deltaX = (long)destination.X - state.PlayerPosition.X;
            long deltaZ = (long)destination.Z - state.PlayerPosition.Z;
            double yaw = state.FacingYawMicroRad / 1_000_000d;
            double localX = (deltaX * Math.Cos(yaw)) + (deltaZ * Math.Sin(yaw));
            double localZ = (-deltaX * Math.Sin(yaw)) + (deltaZ * Math.Cos(yaw));
            sbyte moveX = (sbyte)Math.Sign(localX);
            sbyte moveZ = (sbyte)Math.Sign(localZ);
            simulation.Step(new SimInput(moveX, moveZ));
        }

        Level100ActorSnapshot stalledPlayer = simulation.Snapshot.Level100Actors.Actors.Single(
            actor => actor.Name == "Player 1");
        throw new Xunit.Sdk.XunitException(
            $"Did not start released trigger actor {trigger}; " +
            $"position={simulation.Snapshot.PlayerPosition}; " +
            $"playerActive={simulation.Snapshot.Level100PlayerActive}; " +
            $"controlEnabled={simulation.Snapshot.Level100PlayerControlEnabled}; " +
            $"navigation={simulation.Snapshot.Level100Mission.NavigationObjective}; " +
            $"playerScript={stalledPlayer.ScriptName}.");
    }

    /// <summary>
    /// The jet Missile Pod's launcher burst: Fire spawns the first missile at
    /// once and files event 5001 at now + <c>CWeaponBurstDelay</c>; each
    /// delivery spawns one more until the mode's five. Every missile spends
    /// one from store 3 and is one launch-sound event, and its heading is the
    /// launch orientation times its launch-angle slot as retail matrices
    /// (the RE lane's burst-spawner contract, steps 2, 5 and 7).
    /// </summary>
    [Fact]
    public void MissilePodLauncher_SpawnsFiveMissilesOnTheBurstEventsAlongTheirSlots()
    {
        Simulation simulation = CreateJetWithMissilePod();
        int yaw = simulation.Snapshot.FacingYawMicroRad;
        const int Pitch = -300_000;
        LookIntoOpenSky(simulation, yaw, Pitch);

        simulation.SetFacingForMeasurement(yaw, Pitch);
        float firedAt = RetailEventScheduler.TimeAtFrameCount(simulation.Snapshot.RetailEventFrameCount + 1);
        WorldSnapshot fired = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        RetailEventSchedulerSnapshot events = fired.Level100ActorMechanics.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        RetailEventSlotSnapshot burst = Assert.Single(
            events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow).Select(handle => slots[handle]),
            slot => slot.Listener == Level100ActorMechanics.MissilePodListener);
        Assert.Equal(Level100ActorMechanics.WeaponBurstEvent, (int)burst.EventNum);
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(firedAt + BitConverter.UInt32BitsToSingle(0x3dcccccdu)),
            burst.TimeBits);
        // The pod selects schema 51 on its own; a world built in the retail
        // load order always carries the unit callbacks of 52 as well.
        Assert.Equal(52, CanonicalSchemaVersion(fired));

        var spawnTicks = new List<int>();
        var headings = new List<Level100SeekingRoundSnapshot>();
        var seen = new HashSet<int>();
        int fireEvents = 0;
        WorldSnapshot state = fired;
        for (int step = 0; step < 30; step++)
        {
            fireEvents += state.Level100WeaponFireEvents.Count(item =>
                item.Weapon == Level100PlayerWeapon.MissilePod);
            foreach (ProjectileSnapshot missile in state.Projectiles.Where(item =>
                         item.Kind == Level100ProjectileKind.MicroMissile && seen.Add(item.Id)))
            {
                spawnTicks.Add(state.Tick);
                headings.Add(Assert.IsType<Level100SeekingRoundSnapshot>(missile.Seeking));
                Assert.Null(missile.Seeking!.Target);
            }
            simulation.SetFacingForMeasurement(yaw, Pitch);
            state = simulation.Step(SimInput.Idle);
        }

        Assert.Equal(5, spawnTicks.Count);
        Assert.Equal(5, fireEvents);
        Assert.Equal([0, 2, 4, 6, 8], spawnTicks.Select(tick => tick - spawnTicks[0]));
        Assert.Equal(195.0f, BitConverter.UInt32BitsToSingle(state.Level100PlayerStores.Store3Bits));
        Assert.Equal(new Level100MissilePodSnapshot(0, 0, true, 5, 4, 4), state.Level100MissilePod);

        for (int slot = 0; slot < 5; slot++)
        {
            (double expectedYaw, double expectedPitch) = RetailLaunchHeading(
                yaw / 1e6,
                Pitch / 1e6,
                BitConverter.UInt32BitsToSingle(Level100MissilePod.LaunchAngleYawBits[slot]),
                BitConverter.UInt32BitsToSingle(Level100MissilePod.LaunchAnglePitchBits[slot]));
            Assert.InRange(headings[slot].YawMicroRadians - (expectedYaw * 1e6), -40, 40);
            Assert.InRange(headings[slot].PitchMicroRadians - (expectedPitch * 1e6), -40, 40);
        }

        // A pitched launch is not the angle sum: slot 5's 12-degree yaw turns
        // about the pitched frame's own up axis.
        Assert.True(Math.Abs(headings[4].PitchMicroRadians - Pitch) > 5_000);

        // Unbound and aimed at open sky, each missile lives out its span and,
        // carrying CRoundExplode, bursts in the air on its life event, which
        // comes before that frame's last step: exactly where the previous
        // frame left it (the RE lane's round Frames contract).
        var lastPositions = new Dictionary<int, Level100Vector3>();
        int airBursts = 0;
        for (int step = 0; step < 200 && (step == 0 || lastPositions.Count > 0); step++)
        {
            foreach (Level100DestructionEvent burstEvent in state.Level100DestructionEvents.Where(item =>
                         item.Kind == Level100DestructionEventKind.MicroMissileImpact))
            {
                // Contact axes: Core z forward, retail z down.
                Assert.Equal(0, burstEvent.ActorId);
                Assert.Contains(burstEvent.Position, lastPositions.Values);
                airBursts++;
            }
            lastPositions = state.Projectiles
                .Where(item => item.Kind == Level100ProjectileKind.MicroMissile)
                .ToDictionary(item => item.Id, item => new Level100Vector3(
                    item.Position.X, item.Position.Z, -item.ElevationMillimeters));
            simulation.SetFacingForMeasurement(yaw, Pitch);
            state = simulation.Step(SimInput.Idle);
        }
        Assert.Equal(5, airBursts);
    }

    /// <summary>
    /// With the pod selected and a script-enemy static target under the
    /// crosshair within 100 m, <c>HandleLocks</c> starts a lock that finishes
    /// after <c>CWeaponLockTime</c>; the burst's missiles bind it through
    /// <c>GetCurrentTarget</c>, <c>FireLock</c> moves it to the fired set, and
    /// the round's release calls <c>LockHit</c>, which clears it again
    /// (<c>BattleEngine.cpp:586-1010</c>; the RE lane's Missile Pod answer).
    /// </summary>
    [Fact]
    public void MissilePod_LocksAStaticTargetAndItsMissilesSeekItThenReleaseTheLock()
    {
        Simulation simulation = CreateFiringRangeExerciseSimulation();
        EnterJetWithMissilePod(simulation);
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Where(actor => actor.TargetGroup == Level100MissionTargetGroup.StaticTargets &&
                actor.Active && actor.Lifecycle == Level100ActorLifecycle.Alive)
            .OrderBy(actor => SquaredDistance(simulation.Snapshot, actor.Pose.PositionMillimeters))
            .First();
        Assert.True(SquaredDistance(simulation.Snapshot, target.Pose.PositionMillimeters) < 100_000L * 100_000L);

        WorldSnapshot state = simulation.Snapshot;
        for (int step = 0; step < 20 && state.Level100BattleEngineTargeting.Locks.Locks.Count == 0; step++)
        {
            FaceActor(simulation, target);
            state = simulation.Step(SimInput.Idle);
        }

        Level100PlayerLockSnapshot started = Assert.Single(state.Level100BattleEngineTargeting.Locks.Locks);
        Assert.Equal(target.ActorId, started.Unit);
        Assert.True(started.DirectLock);
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle(started.StartBits) +
                BitConverter.UInt32BitsToSingle(0x3e4ccccdu)),
            started.FinishBits);

        // Let the lock finish (strictly after its finish time), then fire.
        for (int step = 0; step < 5; step++)
        {
            FaceActor(simulation, target);
            state = simulation.Step(SimInput.Idle);
        }
        FaceActor(simulation, target);
        state = simulation.Step(new SimInput(0, 0, SimActions.Fire));
        ProjectileSnapshot first = Assert.Single(state.Projectiles,
            item => item.Kind == Level100ProjectileKind.MicroMissile);
        Assert.Equal(target.ActorId, first.Seeking!.Target);
        Assert.Empty(state.Level100BattleEngineTargeting.Locks.Locks);
        Assert.Equal(target.ActorId, Assert.Single(state.Level100BattleEngineTargeting.Locks.FiredLocks).Unit);

        // Every later missile of the burst finds the target in the fired set.
        var boundIds = new HashSet<int>();
        int impacts = 0;
        for (int step = 0; step < 12; step++)
        {
            state = simulation.Step(SimInput.Idle);
            impacts += CheckFirstImpact(state, impacts);
            foreach (ProjectileSnapshot missile in state.Projectiles.Where(item =>
                         item.Kind == Level100ProjectileKind.MicroMissile))
            {
                Assert.True(missile.Seeking!.Target is null || missile.Seeking.Target == target.ActorId);
                if (missile.Seeking.Target == target.ActorId)
                {
                    boundIds.Add(missile.Id);
                }
            }
        }
        Assert.Equal(5, boundIds.Count + impacts);

        // The seeking missiles strike it; each bound missile's release removes
        // one fired entry, and the burst's five releases leave none.
        for (int step = 0; step < 200 && state.Projectiles.Any(item =>
                 item.Kind == Level100ProjectileKind.MicroMissile); step++)
        {
            state = simulation.Step(SimInput.Idle);
            impacts += CheckFirstImpact(state, impacts);
        }
        Assert.DoesNotContain(state.Projectiles, item => item.Kind == Level100ProjectileKind.MicroMissile);
        Assert.Equal(5, impacts);
        Assert.Empty(state.Level100BattleEngineTargeting.Locks.FiredLocks);

        // The first strike's shutdown releases the one fired entry while the
        // target still lives; no other release can have emptied it yet.
        int CheckFirstImpact(WorldSnapshot state, int earlier)
        {
            int now = state.Level100DestructionEvents.Count(item =>
                item.Kind == Level100DestructionEventKind.MicroMissileImpact && item.ActorId == target.ActorId.Value);
            if (earlier == 0 && now > 0)
            {
                Assert.Equal(Level100ActorLifecycle.Alive, state.Level100Actors.Actors
                    .Single(actor => actor.ActorId == target.ActorId).Lifecycle);
                Assert.Empty(state.Level100BattleEngineTargeting.Locks.FiredLocks);
            }
            return now;
        }
    }

    /// <summary>
    /// The jet's Mech Vulcan fires its two rounds from cockpit Guns 13 and 14,
    /// in its launch sequence's order, through the body orientation (the RE
    /// lane's aiming contract; <c>CWeaponLaunchSequence</c> (1, 13) (2, 14)).
    /// </summary>
    [Fact]
    public void MechVulcanRounds_LeaveGunsThirteenAndFourteenInSequence()
    {
        Simulation jet = CreatePlayingSimulation();
        jet.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        jet.Step(new SimInput(0, 0, SimActions.ToggleMode));
        AdvanceUntil(jet, state => state.Mode == VehicleMode.Jet && state.Transition == VehicleTransition.None, 100);
        WorldSnapshot before = jet.Snapshot;
        WorldSnapshot fired = jet.Step(new SimInput(0, 0, SimActions.Fire));
        ProjectileSnapshot[] rounds = fired.Projectiles.OrderBy(item => item.Id).ToArray();
        Assert.Equal(2, rounds.Length);
        for (int index = 0; index < 2; index++)
        {
            (double X, double Y, double Z) offset = CockpitEmitterOffset(
                Level100CockpitEmitters.Gun(13 + index, walkPose: false),
                before.FacingYawMicroRad / 1e6, before.FacingPitchMicroRad / 1e6, before.BodyRollMicroRad / 1e6);
            // The launch frame leaves each round at its emitter.
            ProjectileSnapshot round = rounds[index];
            Assert.InRange(round.Position.X - (before.PlayerPosition.X + offset.X), -2, 2);
            Assert.InRange(round.Position.Z - (before.PlayerPosition.Z + offset.Z), -2, 2);
            Assert.InRange(round.ElevationMillimeters - (before.PlayerElevationMillimeters + offset.Y), -2, 2);
        }
    }

    /// <summary>
    /// Rounds move in the level event manager's insertion order (the RE lane's
    /// round Frames contract). A round a controller Fire makes is filed into
    /// the current bucket before the flush re-files the MOVEs of the rounds
    /// already in flight, so in the next frame it moves ahead of them.
    /// </summary>
    [Fact]
    public void ControllerRounds_MoveAheadOfRoundsAlreadyInFlight()
    {
        Simulation jet = CreatePlayingSimulation();
        jet.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        jet.Step(new SimInput(0, 0, SimActions.ToggleMode));
        AdvanceUntil(jet, state => state.Mode == VehicleMode.Jet && state.Transition == VehicleTransition.None, 100);
        int[] older = jet.Step(new SimInput(0, 0, SimActions.Fire)).Projectiles
            .Select(round => round.Id).Order().ToArray();
        Assert.Equal(2, older.Length);
        for (int tick = 0; tick < 4; tick++)
        {
            jet.Step(SimInput.Idle);
        }

        WorldSnapshot fired = jet.Step(new SimInput(0, 0, SimActions.Fire));
        int[] newer = fired.Projectiles.Select(round => round.Id).Except(older).Order().ToArray();
        Assert.Equal(2, newer.Length);
        Assert.Equal(4, fired.Projectiles.Count);

        RetailEventSchedulerSnapshot events = fired.Level100ActorMechanics.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        int[] moveOrder = events.Lanes
            .Single(lane => lane.LaneIndex == events.CurrentBufferNum * RetailEventScheduler.PriorityLanes)
            .Handles.Select(handle => slots[handle])
            .Where(slot => slot.EventNum == Level100ActorMechanics.RoundMoveEvent &&
                Level100ActorMechanics.IsPlayerRoundListener(slot.Listener))
            .Select(slot => Level100ActorMechanics.PlayerRoundId(slot.Listener))
            .ToArray();
        Assert.Equal(newer.Concat(older), moveOrder);
    }

    /// <summary>
    /// A dying round's last step still meets things but hits nothing (the RE
    /// lane's "No hit while dying"). The probe round lives two frames: its
    /// first step ends 10 m above the tank, and its second, taken after its
    /// life event, drops through it.
    /// </summary>
    [Fact]
    public void DyingRound_LastStepCrossesATargetWithoutDamagingIt()
    {
        var simulation = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        Level100ActorSnapshot target = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank 2");
        SimVector3 position = target.Pose.PositionMillimeters;
        simulation.QueueRoundForContactMeasurement(
            Level100ProjectileKind.MechBullet,
            position with { Y = position.Y + 20_000 },
            position with { Y = position.Y + 10_000 });

        WorldSnapshot first = simulation.Step(SimInput.Idle);
        Assert.Single(first.Projectiles);
        WorldSnapshot last = simulation.Step(SimInput.Idle);
        Assert.Empty(last.Projectiles);
        Assert.DoesNotContain(
            first.Level100DestructionEvents.Concat(last.Level100DestructionEvents),
            item => item.Kind == Level100DestructionEventKind.VulcanImpact);
        Assert.Equal(target.Health, last.Level100Actors.Actors
            .Single(actor => actor.ActorId == target.ActorId).Health);

        // The same drop taken by a live step hits and damages the tank.
        var control = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        control.QueueRoundForContactMeasurement(
            Level100ProjectileKind.MechBullet,
            position with { Y = position.Y + 10_000 },
            position);
        WorldSnapshot hit = control.Step(SimInput.Idle);
        Assert.Contains(hit.Level100DestructionEvents, item =>
            item.Kind == Level100DestructionEventKind.VulcanImpact && item.ActorId == target.ActorId.Value);
        Assert.True(hit.Level100Actors.Actors.Single(actor => actor.ActorId == target.ActorId).Health <
            target.Health);
    }

    /// <summary>
    /// A cockpit emitter's offset from the Battle Engine in Core millimetres:
    /// model x right, y forward and z down through yaw, nose-down pitch and
    /// roll, in double precision.
    /// </summary>
    private static (double X, double Y, double Z) CockpitEmitterOffset(
        Level100CockpitEmitters.Emitter gun, double yaw, double pitch, double roll)
    {
        (double X, double Y, double Z) forward = (-Math.Sin(yaw) * Math.Cos(pitch), -Math.Sin(pitch), Math.Cos(yaw) * Math.Cos(pitch));
        (double X, double Y, double Z) baseRight = (Math.Cos(yaw), 0.0, Math.Sin(yaw));
        (double X, double Y, double Z) baseUp = (-Math.Sin(pitch) * Math.Sin(yaw), Math.Cos(pitch), Math.Sin(pitch) * Math.Cos(yaw));
        (double X, double Y, double Z) right = (
            (baseRight.X * Math.Cos(roll)) + (baseUp.X * Math.Sin(roll)),
            baseUp.Y * Math.Sin(roll),
            (baseRight.Z * Math.Cos(roll)) + (baseUp.Z * Math.Sin(roll)));
        (double X, double Y, double Z) up = (
            (baseUp.X * Math.Cos(roll)) - (baseRight.X * Math.Sin(roll)),
            baseUp.Y * Math.Cos(roll),
            (baseUp.Z * Math.Cos(roll)) - (baseRight.Z * Math.Sin(roll)));
        double x = gun.XMicrometres / 1000.0, y = gun.YMicrometres / 1000.0, z = gun.ZMicrometres / 1000.0;
        return (
            (x * right.X) + (y * forward.X) - (z * up.X),
            (x * right.Y) + (y * forward.Y) - (z * up.Y),
            (x * right.Z) + (y * forward.Z) - (z * up.Z));
    }

    private static Simulation CreateJetWithMissilePod()
    {
        Simulation simulation = CreatePlayingSimulation();
        EnterJetWithMissilePod(simulation);
        return simulation;
    }

    private static void EnterJetWithMissilePod(Simulation simulation)
    {
        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        simulation.Step(new SimInput(0, 0, SimActions.ToggleMode));
        AdvanceUntil(
            simulation,
            state => state.Mode == VehicleMode.Jet && state.Transition == VehicleTransition.None,
            100);
        WorldSnapshot selected = simulation.Step(new SimInput(0, 0, SimActions.ChangeWeapon));
        Assert.Equal(Level100MissionWeapon.MissilePod, selected.Level100JetSelectedWeapon);
    }

    /// <summary>Holds the view on open sky until a crosshair refresh reports nothing.</summary>
    private static void LookIntoOpenSky(Simulation simulation, int yaw, int pitch)
    {
        for (int step = 0; step < 12; step++)
        {
            simulation.SetFacingForMeasurement(yaw, pitch);
            simulation.Step(SimInput.Idle);
        }
        Assert.Equal(
            Level100CrosshairHitKind.Nothing,
            simulation.Snapshot.Level100BattleEngineTargeting.CrosshairHitKind);
    }

    private static void FaceActor(Simulation simulation, Level100ActorSnapshot actor)
    {
        WorldSnapshot state = simulation.Snapshot;
        SimVector3 point = state.Level100Actors.Actors
            .Single(item => item.ActorId == actor.ActorId).Pose.PositionMillimeters;
        double dx = point.X - (double)state.PlayerPosition.X;
        double dy = point.Y + 500 - (double)state.PlayerElevationMillimeters;
        double dz = point.Z - (double)state.PlayerPosition.Z;
        simulation.SetFacingForMeasurement(
            (int)Math.Round(Math.Atan2(-dx, dz) * 1e6),
            (int)Math.Round(Math.Atan2(-dy, Math.Sqrt((dx * dx) + (dz * dz))) * 1e6));
    }

    private static long SquaredDistance(WorldSnapshot state, SimVector3 point)
    {
        long dx = point.X - (long)state.PlayerPosition.X;
        long dy = point.Y - (long)state.PlayerElevationMillimeters;
        long dz = point.Z - (long)state.PlayerPosition.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    /// <summary>
    /// The forward column of <c>FMatrix(yaw, pitch, 0) × FMatrix(a, b, 0)</c>
    /// in retail axes (x right, y forward, z down), as Core's yaw and
    /// nose-down pitch.
    /// </summary>
    private static (double Yaw, double Pitch) RetailLaunchHeading(
        double yaw,
        double pitch,
        double angleYaw,
        double anglePitch,
        double jitterYaw = 0.0,
        double jitterPitch = 0.0)
    {
        static (double X, double Y, double Z) Rotate(double y, double p, (double X, double Y, double Z) v) => (
            (Math.Cos(y) * v.X) - (Math.Cos(p) * Math.Sin(y) * v.Y) + (Math.Sin(p) * Math.Sin(y) * v.Z),
            (Math.Sin(y) * v.X) + (Math.Cos(p) * Math.Cos(y) * v.Y) - (Math.Sin(p) * Math.Cos(y) * v.Z),
            (Math.Sin(p) * v.Y) + (Math.Cos(p) * v.Z));
        (double x, double yy, double z) = Rotate(yaw, pitch,
            Rotate(angleYaw, anglePitch, Rotate(jitterYaw, jitterPitch, (0.0, 1.0, 0.0))));
        return (Math.Atan2(-x, yy), Math.Atan2(z, Math.Sqrt((x * x) + (yy * yy))));
    }

    private static Simulation CreatePlayingSimulation(uint seed = 1)
    {
        var simulation = new Simulation(
            seed,
            Level100TestActorDefinitions.Create(),
            CompletedTutorialSlots);
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(simulation.Snapshot.Level100PlayerControlEnabled);
        return simulation;
    }

    private static Simulation CreateFiringRangeExerciseSimulation()
    {
        Simulation simulation = CreatePlayingSimulation();
        AdvanceUntilNavigation(simulation, "Target Zone 1", 500);
        DriveIntoTrigger(simulation, Level100MissionTrigger.TargetZone1);
        AdvanceUntilNavigation(simulation, "Firing Range", 100);
        DriveIntoTrigger(simulation, Level100MissionTrigger.FiringRange);
        AdvanceUntil(simulation, state => state.Level100FiringRangeTargetsActive, 100);
        AdvanceUntil(simulation, state => state.Level100PulseCannonEnabled, 100);
        return simulation;
    }

    private static Simulation CreateFiringRangeBeforeWeaponEnable()
    {
        Simulation simulation = CreatePlayingSimulation();
        AdvanceUntilNavigation(simulation, "Target Zone 1", 500);
        DriveIntoTrigger(simulation, Level100MissionTrigger.TargetZone1);
        AdvanceUntilNavigation(simulation, "Firing Range", 100);
        DriveIntoTrigger(simulation, Level100MissionTrigger.FiringRange);
        AdvanceUntil(simulation, state => state.Level100FiringRangeTargetsActive, 100);
        Level100ScriptContinuationSnapshot enable = Assert.Single(
            simulation.Snapshot.Level100Mission.Continuations,
            item => item.Execution.EventName == "Reached Firing Range" &&
                item.WaitKind == Level100ScriptWaitKind.Pause);
        while (simulation.Snapshot.Level100Mission.Tick + 1 < enable.DueTick)
            simulation.Step(SimInput.Idle);
        Assert.Equal(enable.DueTick - 1, simulation.Snapshot.Level100Mission.Tick);
        return simulation;
    }

    private static DeterministicSimulationTape RunLevel100FailureTape()
    {
        var simulation = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        var hashes = new List<string> { StateHasher.ComputeHex(simulation.Snapshot) };
        WorldSnapshot snapshot = simulation.Step(
            SimInput.Idle,
            [new Level100MissionInputFact(Level100MissionInput.BrokeTutorial)]);
        hashes.Add(StateHasher.ComputeHex(snapshot));
        // "Broke Tutorial" is posted on tick 1, while HUD_01 still owns the
        // message box until tick 351. Its own two messages queue behind that
        // and behind each other at the released advance gap, so the terminal
        // call lands at tick 594 rather than 291.
        for (int tick = 0;
             tick < 800 && snapshot.Level100Mission.Outcome != Level100MissionOutcome.Lost;
             tick++)
        {
            snapshot = simulation.Step(SimInput.Idle);
            hashes.Add(StateHasher.ComputeHex(snapshot));
        }

        Assert.Equal(Level100MissionOutcome.Lost, snapshot.Level100Mission.Outcome);
        Assert.Equal(
            Level100MissionTiming.FailureCountdownTicks,
            snapshot.Level100Mission.TerminalTicksRemaining);
        int actorScriptTickAtLoss = snapshot.Level100ActorScripts.Tick;
        uint clockAtLoss = snapshot.RetailEventFrameCount;
        for (int tick = 0; tick < Level100MissionTiming.FailureCountdownTicks; tick++)
        {
            snapshot = simulation.Step(SimInput.Idle);
            Assert.Equal(clockAtLoss, snapshot.RetailEventFrameCount);
            hashes.Add(StateHasher.ComputeHex(snapshot));
        }
        Assert.Equal(actorScriptTickAtLoss, snapshot.Level100ActorScripts.Tick);

        return new DeterministicSimulationTape(snapshot, hashes.AsReadOnly());
    }

    private static void AdvanceUntilNavigation(
        Simulation simulation,
        string thingName,
        int maximumTicks) => AdvanceUntil(
            simulation,
            state => string.Equals(
                state.Level100Mission.NavigationObjective,
                thingName,
                StringComparison.Ordinal),
            maximumTicks);

    private static void AdvanceUntil(
        Simulation simulation,
        Func<WorldSnapshot, bool> predicate,
        int maximumTicks,
        Action<WorldSnapshot>? observe = null)
    {
        for (int tick = 0; tick < maximumTicks && !predicate(simulation.Snapshot); tick++)
        {
            WorldSnapshot state = simulation.Step(SimInput.Idle);
            observe?.Invoke(state);
        }

        Assert.True(
            predicate(simulation.Snapshot),
            $"Condition was not reached by simulation tick {simulation.Snapshot.Tick}.");
    }

    private static Level100TriggerActorSnapshot Trigger(
        Simulation simulation,
        Level100MissionTrigger trigger) => Trigger(simulation.Snapshot, trigger);

    private static Level100TriggerActorSnapshot Trigger(
        WorldSnapshot snapshot,
        Level100MissionTrigger trigger) => snapshot.Level100TriggerActors
            .Single(item => item.Trigger == trigger);

    private sealed record DeterministicSimulationTape(
        WorldSnapshot Snapshot,
        IReadOnlyList<string> Hashes);

    private static void AimAtTarget(Simulation simulation, SimVector2 target)
    {
        for (int tick = 0; tick < 300; tick++)
        {
            WorldSnapshot state = simulation.Snapshot;
            double desired = Math.Atan2(
                -(target.X - state.PlayerPosition.X),
                target.Z - state.PlayerPosition.Z);
            double error = NormalizeRadians(desired - (state.FacingYawMicroRad / 1_000_000d));
            if (Math.Abs(error) < 0.04d)
            {
                return;
            }

            simulation.Step(new SimInput(0, 0, LookX: (sbyte)Math.Sign(error)));
        }

        throw new Xunit.Sdk.XunitException("Could not aim the deterministic Core at the requested target.");
    }

    private static void AimAtTargetAndSettle(Simulation simulation, SimVector2 target)
    {
        for (int tick = 0; tick < 1_200; tick++)
        {
            WorldSnapshot state = simulation.Snapshot;
            double desired = Math.Atan2(
                -(target.X - state.PlayerPosition.X),
                target.Z - state.PlayerPosition.Z);
            double error = NormalizeRadians(desired - (state.FacingYawMicroRad / 1_000_000d));
            if (Math.Abs(error) < 0.035d &&
                Math.Abs(state.WalkerYawVelocityMicroRadPerTick) <
                    SimulationConstants.WalkerYawInputMicroRadPerTick * 2 / 3)
            {
                return;
            }

            double demand = error -
                ((state.WalkerYawVelocityMicroRadPerTick / 1_000_000d) * 5d);
            sbyte look = (sbyte)Math.Sign(demand);
            simulation.Step(new SimInput(0, 0, LookX: look));
        }

        WorldSnapshot final = simulation.Snapshot;
        double finalDesired = Math.Atan2(
            -(target.X - final.PlayerPosition.X),
            target.Z - final.PlayerPosition.Z);
        double finalError = NormalizeRadians(
            finalDesired - (final.FacingYawMicroRad / 1_000_000d));
        throw new Xunit.Sdk.XunitException(
            $"Could not settle the deterministic walker heading: error={finalError:F6}, " +
            $"velocity={final.WalkerYawVelocityMicroRadPerTick}.");
    }

    private static void AimReticleAtActor(
        Simulation simulation,
        Level100ActorSnapshot target,
        int heightAboveOriginMillimeters)
    {
        SimVector3 position = target.Pose.PositionMillimeters;
        for (int tick = 0; tick < 1_200; tick++)
        {
            WorldSnapshot state = simulation.Snapshot;
            double deltaX = (double)position.X - state.PlayerPosition.X;
            double deltaZ = (double)position.Z - state.PlayerPosition.Z;
            double horizontal = Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
            double yawError = NormalizeRadians(
                Math.Atan2(-deltaX, deltaZ) -
                (state.FacingYawMicroRad / 1_000_000d));
            double pitchError =
                -Math.Atan2(
                    position.Y + heightAboveOriginMillimeters -
                        state.PlayerElevationMillimeters,
                    Math.Max(1.0, horizontal)) -
                (state.FacingPitchMicroRad / 1_000_000d);
            if (Math.Abs(yawError) < 0.002d &&
                Math.Abs(pitchError) < 0.002d &&
                Math.Abs(state.WalkerYawVelocityMicroRadPerTick) < 500 &&
                Math.Abs(state.WalkerPitchVelocityMicroRadPerTick) < 500)
            {
                return;
            }

            double yawDemand = yawError -
                ((state.WalkerYawVelocityMicroRadPerTick / 1_000_000d) * 5d);
            double pitchDemand = pitchError -
                ((state.WalkerPitchVelocityMicroRadPerTick / 1_000_000d) * 5d);
            int yawResponsePermille = (int)(
                yawDemand * 45_334d * 1_000d /
                SimulationConstants.WalkerYawInputMicroRadPerTick);
            int pitchResponsePermille = (int)(pitchDemand * 4_000d);
            simulation.Step(new SimInput(
                0,
                0,
                SimActions.None,
                0,
                0,
                LookAxisCommand.ForResponsePermille(yawResponsePermille),
                LookAxisCommand.ForResponsePermille(pitchResponsePermille)));
        }

        throw new Xunit.Sdk.XunitException(
            "Could not settle the reticle on the requested Level 100 actor contact.");
    }

    private static double NormalizeRadians(double value)
    {
        while (value > Math.PI) value -= Math.Tau;
        while (value <= -Math.PI) value += Math.Tau;
        return value;
    }

}
