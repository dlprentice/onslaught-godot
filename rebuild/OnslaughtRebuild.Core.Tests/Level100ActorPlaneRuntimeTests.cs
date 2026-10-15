// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100ActorPlaneRuntimeTests
{
    [Fact]
    public void SpawnedExitAndReadyContinueAcrossBothRestoreBoundaries()
    {
        var run = new ExitRun();
        Level100ActorId id = run.Spawn();
        var initial = run.State(id);
        Assert.Equal(2, initial.PlaneGuide!.ControllerState);
        Assert.Equal(1, initial.PlaneSpawnerExit!.Selector);
        Assert.Equal(0x41200000, initial.PlaneSpawnerExit.DeadlineFloatBits);
        Assert.False(initial.PlaneSpawnerExit.ScriptControlResumed);
        Assert.Equal(initial.PlaneSpawnerExit.SpawningOwnerId, initial.PlaneSpawnerExit.CollisionIgnoredActorId);
        Assert.DoesNotContain(run.Commands, command => command.ActorId == id &&
            command.Kind == Level100ActorScriptCommandKind.FollowWaypoint);
        var restored = run.Restore();
        Assert.Empty(restored.Ready);
        int previousExitHandle = Pending(run.Mechanics.Snapshot, -id.Value, 3002).Handle;
        for (int i = 0; i < 220 && run.State(id).PlaneGuide!.ControllerState == 2; i++)
        {
            previousExitHandle = Pending(run.Mechanics.Snapshot, -id.Value, 3002).Handle;
            run.Step(); restored.Step();
            AssertSame(run, restored);
        }
        var complete = run.State(id);
        Assert.Equal(1, complete.PlaneGuide!.ControllerState);
        Assert.False(complete.PlaneSpawnerExit!.ScriptControlResumed);
        Assert.Equal(initial.PlaneSpawnerExit.SpawningOwnerId, complete.PlaneSpawnerExit.SpawningOwnerId);
        Assert.Null(complete.PlaneSpawnerExit.CollisionIgnoredActorId);
        Assert.Empty(run.Ready);
        Assert.NotEqual(previousExitHandle, Pending(run.Mechanics.Snapshot, -id.Value, 3000).Handle);
        Assert.NotEqual(previousExitHandle, Pending(run.Mechanics.Snapshot, id.Value * 2, 2003).Handle);

        restored = run.Restore(); // After exit, before the two new callbacks.
        Assert.Empty(restored.Ready);
        run.Step(); restored.Step();
        AssertSame(run, restored);
        Assert.Equal(id, Assert.Single(run.Ready));
        Assert.Equal(id, Assert.Single(restored.Ready));
        Assert.True(run.State(id).PlaneSpawnerExit!.ScriptControlResumed);
        Assert.Equal("Drone Path 1", run.State(id).WaypointPath);
        Assert.Single(run.Commands, command => command.ActorId == id &&
            command.Kind == Level100ActorScriptCommandKind.FollowWaypoint);
        restored = run.Restore();
        for (int i = 0; i < 25; i++) { run.Step(); restored.Step(); }
        AssertSame(run, restored);
        Assert.Single(run.Ready);
        Assert.Empty(restored.Ready); // Completed handoff is not replayed.
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void ExitArrivalIsStrictAndUsesRawThreeDimensionalPosition(int ulps, int selector)
    {
        var run = new ExitRun();
        Level100ActorId id = run.Spawn();
        Level100FloatVector3Bits point = run.Actors.GetPlaneSpawnerExitPoint(id, 1)!.Value.PositionFloatBits;
        int x = BitConverter.SingleToInt32Bits(BitConverter.Int32BitsToSingle(point.X) + 2.5f) + ulps;
        // Explicit boundary fixture: preserve complete raw pose state while
        // arranging a position adjacent to the 2.5-unit native threshold.
        run.Actors.GetPlaneState(id).TeleportRetailPosition(point with { X = x });
        run.Step();
        Assert.Equal(selector, run.State(id).PlaneSpawnerExit!.Selector);
        Assert.Equal(2, run.State(id).PlaneGuide!.ControllerState);
        Assert.Equal(1, run.State(id).PlaneGuide!.Mode);
        Assert.Empty(run.Ready);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void ExitDeadlineIsStrictAtTheCurrentEventTime(int ulps, int controllerState)
    {
        var run = new ExitRun();
        Level100ActorId id = run.Spawn();
        run.ChangeState(id, state => state with
        { PlaneSpawnerExit = state.PlaneSpawnerExit! with { DeadlineFloatBits = 0x3d4ccccd + ulps } });
        run.Step();
        Assert.Equal(controllerState, run.State(id).PlaneGuide!.ControllerState);
        Assert.Empty(run.Ready);
        if (controllerState == 1)
        {
            Assert.Null(run.State(id).PlaneSpawnerExit!.CollisionIgnoredActorId);
            run.Step();
            Assert.Equal(id, Assert.Single(run.Ready));
        }
    }

    [Fact]
    public void ExitCompletionClearsCollisionIgnoreWithoutClearingGuideWords()
    {
        var run = new ExitRun();
        Level100ActorId id = run.Spawn();
        var position = run.Actors.GetBaseState(id).RetailPoses!.Current.PositionFloatBits;
        run.ChangeState(id, state => state with
        {
            PlaneSpawnerExit = state.PlaneSpawnerExit! with { Selector = 2 }, // absent native selector
            PlaneGuide = state.PlaneGuide! with
            {
                Mode = 1, ClearanceFloatBits = 0x41880000,
                ClearanceCellX = (int)Math.Round(BitConverter.Int32BitsToSingle(position.X)),
                ClearanceCellY = (int)Math.Round(BitConverter.Int32BitsToSingle(position.Y)),
            },
        });
        var before = run.State(id);
        run.Step();
        Assert.Equal(before.PlaneGuide! with { ControllerState = 1 }, run.State(id).PlaneGuide);
        Assert.Null(run.State(id).PlaneSpawnerExit!.CollisionIgnoredActorId);
        Assert.Equal(before.PlaneSpawnerExit!.SpawningOwnerId, run.State(id).PlaneSpawnerExit!.SpawningOwnerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LostSpawnerStartsDeathWithoutReadyOrImmediateDestruction(bool deleted)
    {
        var run = new ExitRun();
        Level100ActorId id = run.Spawn();
        Level100ActorId owner = run.State(id).PlaneSpawnerExit!.SpawningOwnerId!.Value;
        if (deleted) run.Actors.ReportDied(owner); else run.Actors.ReportStartedDying(owner);
        run.Step();
        Assert.Equal(Level100ActorLifecycle.StartedDying, run.Actors.GetActor(id).Lifecycle);
        Assert.True(run.Actors.GetBaseState(id).IsDying);
        Assert.False(run.Actors.GetBaseState(id).IsShuttingDown);
        Assert.Empty(run.Ready);
        Assert.False(run.State(id).PlaneSpawnerExit!.ScriptControlResumed);
        Assert.Equal(deleted ? null : owner, run.State(id).PlaneSpawnerExit!.SpawningOwnerId);
        Assert.Equal(owner, run.State(id).PlaneSpawnerExit!.CollisionIgnoredActorId); // raw pointer, separate lifetime
        Assert.DoesNotContain(Filed(run.Mechanics.Snapshot), slot => slot.Listener == -id.Value);
        var restored = run.Restore();
        run.Step(); restored.Step();
        AssertSame(run, restored);
        Assert.Contains(Filed(run.Mechanics.Snapshot), slot => slot.Listener == id.Value * 2 && slot.EventNum == 3000);
    }

    [Fact]
    public void GoToAppliesItsOwnAltitudeClampAfterExitArrival()
    {
        var original = Level100TestActorDefinitions.LoadMaterialized();
        // Explicit altered input for the boundary: place the model exit below
        // terrain, so both independently sampled height clamps must act.
        var definitions = new Level100ActorDefinitionSet(original.Actors,
            original.Spawns.Select(spawn => spawn.SpawnerExitWaypoints is null ? spawn : spawn with
            {
                SpawnerExitWaypoints = spawn.SpawnerExitWaypoints.Select(point => point with
                { ModelTransform = point.ModelTransform with
                    { LocalPositionFloatBits = point.ModelTransform.LocalPositionFloatBits with
                        { Z = BitConverter.SingleToInt32Bits(20f) } } }).ToArray(),
            }), original.WaypointPaths, original.MotionDefinitions,
            baseWorldPineCount: original.BaseWorldPineCount);
        var run = new ExitRun(definitions);
        var id = run.Spawn();
        var point = run.Actors.GetPlaneSpawnerExitPoint(id, 1)!.Value.PositionFloatBits;
        float firstZ = (float)RetailFloat24.Subtract(RetailWorldTerrain.SampleRetailHeight(Level100Terrain.Instance, point), 0.1f);
        run.Actors.GetPlaneState(id).TeleportRetailPosition(point with { Z = BitConverter.SingleToInt32Bits(firstZ) });
        int x = (int)Math.Round(BitConverter.Int32BitsToSingle(point.X));
        int y = (int)Math.Round(BitConverter.Int32BitsToSingle(point.Y));
        float secondZ = Math.Min(Level100Terrain.Instance.SampleAirGuideHeightUnits(x, y) *
            Level100Terrain.Instance.HeightScale, Level100Terrain.Instance.WaterLevel) - 4.0f;
        Assert.True(firstZ - secondZ > 2.5f);
        run.Step();
        Assert.Equal(2, run.State(id).PlaneSpawnerExit!.Selector); // arrival before second clamp
        Assert.Equal(point with { Z = BitConverter.SingleToInt32Bits(secondZ) }, run.State(id).PlaneGuide!.Destination);
    }

    [Fact]
    public void NewBurstWaitsForTheNormalControlCallbackNotJustExitCompletion()
    {
        var exiting = new ExitRun();
        var id = exiting.Spawn("AirborneDrone2");
        exiting.PlacePlayerAheadOf(id);
        Assert.Equal(Level100ActorCommandIntent.Attacking, exiting.State(id).Intent);
        Assert.Equal(2, exiting.Mechanics.Snapshot.ActorWeapons.Count(weapon => weapon.ActorId == id));
        var completing = exiting.Restore();
        completing.ChangeState(id, state => state with
        { PlaneSpawnerExit = state.PlaneSpawnerExit! with { Selector = 2 } });

        exiting.Step(); completing.Step();
        Assert.Equal(2, exiting.State(id).PlaneGuide!.ControllerState);
        Assert.Equal(1, completing.State(id).PlaneGuide!.ControllerState);
        Assert.False(completing.State(id).PlaneSpawnerExit!.ScriptControlResumed);
        Assert.Empty(exiting.Mechanics.Snapshot.ActorRounds);
        Assert.Empty(completing.Mechanics.Snapshot.ActorRounds);
        Assert.All(completing.Mechanics.Snapshot.ActorWeapons, weapon =>
            Assert.Equal(0, weapon.ReloadBaseTicksRemaining));

        completing.PlacePlayerAheadOf(id);
        completing.Step(); // Actual queued normal-control callback.
        Assert.True(completing.State(id).PlaneSpawnerExit!.ScriptControlResumed);
        Assert.NotEmpty(completing.Mechanics.Snapshot.ActorRounds); // Positive aim/range control.
        Assert.All(completing.Mechanics.Snapshot.ActorRounds, round => Assert.Equal(id, round.OwnerActorId));
    }

    [Fact]
    public void ExitKeepsPendingBurstAndMovesItsNewRoundOnlyOnTheNextTick()
    {
        var run = new ExitRun();
        var id = run.Spawn("AirborneDrone2");
        run.PlacePlayerAheadOf(id);
        // An explicit continuation fixture, not an assertion that a fresh
        // native spawn already has a burst. It distinguishes ownership gates.
        run.SeedPendingVulcanShot(id);
        var restored = run.Restore();
        run.Step(); restored.Step();
        AssertSame(run, restored);
        var born = Assert.Single(run.Mechanics.Snapshot.ActorRounds);
        Assert.Equal(Level100ActorRoundKind.Blaster, born.Kind);
        Assert.Equal(run.Actors.GetPose(id).PositionMillimeters, born.PositionMillimeters);
        Assert.Equal(0, born.ElapsedBaseTicks);
        Assert.Equal(2, run.State(id).PlaneGuide!.ControllerState);
        run.Step(); restored.Step();
        AssertSame(run, restored);
        var moved = Assert.Single(run.Mechanics.Snapshot.ActorRounds);
        Assert.Equal(born.Id, moved.Id);
        Assert.Equal(1, moved.ElapsedBaseTicks);
        Assert.NotEqual(born.PositionMillimeters, moved.PositionMillimeters);
        Assert.Equal(born.RemainingBaseTicks - 1, moved.RemainingBaseTicks);
    }

    [Fact]
    public void DeletedReadyRecipientRestoresWithoutResurrectingItsScript()
    {
        var run = new ExitRun();
        var id = run.Spawn();
        run.ChangeState(id, state => state with
        { PlaneSpawnerExit = state.PlaneSpawnerExit! with { Selector = 2 } });
        run.Step();
        _ = Pending(run.Mechanics.Snapshot, id.Value * 2, 2003);
        run.Actors.ReportDied(id);
        foreach (var fact in run.Actors.DrainFacts()) run.Scripts.DispatchFact(fact);
        run.Mechanics.ConsumeCommands(run.Scripts.DrainCommands());
        var restored = run.Restore();
        run.Step(); restored.Step();
        AssertSame(run, restored);
        Assert.Empty(run.Ready);
        Assert.DoesNotContain(run.Scripts.Snapshot.Instances, instance => instance.ActorId == id);
        Assert.DoesNotContain(Filed(run.Mechanics.Snapshot), slot => slot.Listener == -id.Value || slot.Listener / 2 == id.Value);
    }

    [Fact]
    public void ExitHashBindsBothOwnersAndRejectsOmittedOrOrphanedExitState()
    {
        var run = new ExitRun();
        var id = run.Spawn();
        // The envelope carries the standalone run's own event clock.
        WorldSnapshot world = new Simulation(1, run.Definitions).Snapshot with
        {
            Level100Actors = run.Actors.Snapshot,
            Level100ActorMechanics = run.Mechanics.Snapshot,
            RetailEventFrameCount = run.Mechanics.Snapshot.PlaneEvents!.FrameCount,
        };
        string baseline = StateHasher.ComputeHex(world);
        // The exit fields of 48 are written inside 52, which the unit
        // callbacks of the retail load order select.
        Assert.Equal(52, BitConverter.ToInt32(StateHasher.GetCanonicalBytes(world), "ONSLAUGHT-REBUILD-STATE".Length));
        var exit = run.State(id).PlaneSpawnerExit!;
        foreach (var changed in new[]
        {
            exit with { DeadlineFloatBits = exit.DeadlineFloatBits + 1 },
            exit with { Selector = 2 }, exit with { SpawningOwnerId = null },
            exit with { CollisionIgnoredActorId = null }, exit with { ScriptControlResumed = true },
        })
            Assert.NotEqual(baseline, StateHasher.ComputeHex(world with
            { Level100ActorMechanics = world.Level100ActorMechanics with
                { Actors = world.Level100ActorMechanics.Actors.Select(state => state.ActorId == id ?
                    state with { PlaneSpawnerExit = changed } : state).ToArray() } }));
        var missing = run.Mechanics.Snapshot with
        { Actors = run.Mechanics.Snapshot.Actors.Select(state => state with { PlaneSpawnerExit = null }).ToArray() };
        Assert.Throws<ArgumentException>(() => StateHasher.ComputeHex(world with { Level100ActorMechanics = missing }));
        Assert.Throws<ArgumentException>(() => new Level100ActorMechanics(run.Actors, run.Definitions, missing));
        var events = run.Mechanics.Snapshot.PlaneEvents!;
        foreach (int listener in new[] { int.MinValue, -123456 })
        {
            var bad = run.Mechanics.Snapshot with
            { PlaneEvents = events with { Slots = events.Slots.Select(slot => slot.Listener == -id.Value ?
                slot with { Listener = listener } : slot).ToArray() } };
            Assert.Throws<ArgumentException>(() => new Level100ActorMechanics(run.Actors, run.Definitions, bad));
        }
    }

    private static RetailEventSlotSnapshot[] Filed(Level100ActorMechanicsSnapshot snapshot)
    {
        var events = snapshot.PlaneEvents!;
        var slots = events.Slots.ToDictionary(slot => slot.Handle);
        return events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow).Select(handle => slots[handle]).ToArray();
    }

    private static RetailEventSlotSnapshot Pending(Level100ActorMechanicsSnapshot snapshot, int listener, int eventNum) =>
        Assert.Single(Filed(snapshot), slot => slot.Listener == listener && slot.EventNum == eventNum);

    private static void AssertSame(ExitRun first, ExitRun second)
    {
        Assert.Equal(JsonSerializer.Serialize(first.Actors.Snapshot), JsonSerializer.Serialize(second.Actors.Snapshot));
        Assert.Equal(JsonSerializer.Serialize(first.Mechanics.Snapshot), JsonSerializer.Serialize(second.Mechanics.Snapshot));
        Assert.Equal(JsonSerializer.Serialize(first.Scripts.Snapshot), JsonSerializer.Serialize(second.Scripts.Snapshot));
    }

    // An instrumented composition of the real three owners. It exercises
    // authored scripts and queue delivery, not a player-acceptance route.
    private sealed class ExitRun
    {
        public Level100ActorDefinitionSet Definitions { get; }
        public Level100ActorRegistry Actors { get; }
        public Level100ActorMechanics Mechanics { get; private set; }
        public Level100ActorScriptRuntime Scripts { get; }
        public List<Level100ActorId> Ready { get; } = [];
        public List<Level100ActorScriptCommand> Commands { get; } = [];
        private uint _frame;

        public ExitRun(Level100ActorDefinitionSet? definitions = null)
        {
            Definitions = definitions ?? Level100TestActorDefinitions.LoadMaterialized();
            Actors = new(Definitions);
            Mechanics = new(Actors, Definitions);
            Scripts = new(Actors, Actors.GetThingRef("Player 1")!.Value);
            Scripts.InitializeReleasedScripts();
            Consume();
        }

        private ExitRun(ExitRun source)
        {
            Definitions = source.Definitions;
            Actors = new(Definitions, source.Actors.Snapshot);
            Mechanics = new(Actors, Definitions, source.Mechanics.Snapshot);
            Scripts = new(Actors, Actors.GetThingRef("Player 1")!.Value, source.Scripts.Snapshot);
            _frame = source._frame;
        }

        public Level100ActorId Spawn(string script = "AirborneDrone1")
        {
            var id = Assert.Single(Actors.SpawnThing(Actors.GetThingRef("Airfield")!.Value,
                "Target Drone", "SpawnerB", 1, script, BitConverter.SingleToInt32Bits(RetailEventScheduler.TimeAtFrameCount(_frame))));
            Mechanics.RegisterSpawnedActor(id);
            Scripts.AttachAndInitializeSpawnedActor(id, script);
            Consume();
            return id;
        }

        public Level100ActorCommandIntentSnapshot State(Level100ActorId id) =>
            Mechanics.Snapshot.Actors.Single(state => state.ActorId == id);

        public void ChangeState(Level100ActorId id, Func<Level100ActorCommandIntentSnapshot, Level100ActorCommandIntentSnapshot> change) =>
            Mechanics = new(Actors, Definitions, Mechanics.Snapshot with
            { Actors = Mechanics.Snapshot.Actors.Select(state => state.ActorId == id ? change(state) : state).ToArray() });

        public ExitRun Restore() => new(this);

        public void PlacePlayerAheadOf(Level100ActorId actorId)
        {
            var playerId = Actors.GetThingRef("Player 1")!.Value;
            var owner = Actors.GetPose(actorId);
            var basis = owner.BasisFloatBits;
            static int Offset(int bits) => (int)Math.Round(BitConverter.Int32BitsToSingle(bits) * 30_000.0);
            Actors.Activate(playerId);
            Actors.SetPose(playerId, Actors.GetPose(playerId) with
            {
                PositionMillimeters = new(owner.PositionMillimeters.X + Offset(basis.Row0Z),
                    owner.PositionMillimeters.Y + Offset(basis.Row1Z), owner.PositionMillimeters.Z + Offset(basis.Row2Z)),
            });
        }

        public void SeedPendingVulcanShot(Level100ActorId actorId) =>
            Mechanics = new(Actors, Definitions, Mechanics.Snapshot with
            { ActorWeapons = Mechanics.Snapshot.ActorWeapons.Select(weapon =>
                weapon.ActorId == actorId && weapon.Weapon == Level100ActorWeaponKind.DroneVulcanCannon
                    ? weapon with { BurstShotsRemaining = 1, BurstDelayBaseTicksRemaining = 0 }
                    : weapon).ToArray() });

        public void Step()
        {
            Mechanics.AdvanceEventClock(++_frame);
            Scripts.AdvanceTick();
            Consume();
            foreach (var completion in Mechanics.AdvanceTick(_frame, id =>
            {
                Ready.Add(id); Scripts.DispatchReady(id); Consume();
            }, id =>
            {
                Actors.ReportPlaneStartedDying(id);
                foreach (var fact in Actors.DrainFacts()) Scripts.DispatchFact(fact);
                Consume();
            })) Assert.True(Scripts.CompleteMechanicsWait(completion.ActorId, completion.WaitKind, completion.Argument));
            Consume();
        }

        private void Consume()
        {
            var commands = Scripts.DrainCommands();
            Commands.AddRange(commands);
            Mechanics.ConsumeCommands(commands);
        }
    }

    [Fact]
    public void CreationOwnsIdleMovementBeforeAnyScriptCommand()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId id = actors.GetThingRef("Air Trainer")!.Value;
        ThingActorBaseStateSnapshot initial = actors.GetBaseState(id);
        Assert.NotNull(initial.RetailPlane);
        Assert.NotNull(mechanics.Snapshot.Actors.Single(actor => actor.ActorId == id).PlaneGuide);
        // Move and the two guide callbacks; the unit's 4003 and AI 3000 are
        // the unit-callback owner's, filed between and after them.
        Assert.Equal([3000, 2000, 2001], Filed(mechanics.Snapshot)
            .Where(slot => slot.Listener > 0 && slot.Listener / 2 == id.Value)
            .Select(slot => (int)slot.EventNum));
        Assert.True(mechanics.Snapshot.PlaneEvents!.Float24Arithmetic);

        mechanics.AdvanceTick();
        ThingActorBaseStateSnapshot first = actors.GetBaseState(id);
        Assert.Equal(initial.RetailPoses!.Current.PositionFloatBits, first.RetailPoses!.Current.PositionFloatBits);
        Assert.Equal(initial.RetailPoses.Current, first.RetailPoses.Old);
        Assert.NotEqual(default, first.RetailPlane!.Drive);
        Assert.Equal(0x3d4ccccd, first.RetailMotion!.LastMoveTimeFloatBits);
        // Clearance is filled AFTER this first Move, not injected before it.
        Assert.Equal(0x40c51eb8, mechanics.Snapshot.Actors.Single(actor => actor.ActorId == id)
            .PlaneGuide!.ClearanceFloatBits);
        // All three were delivered in the first flush and each re-filed itself.
        Assert.Equal(3, Filed(mechanics.Snapshot)
            .Count(slot => slot.Listener > 0 && slot.Listener / 2 == id.Value));

        mechanics.AdvanceTick();
        Assert.NotEqual(first.RetailPoses.Current.PositionFloatBits,
            actors.GetBaseState(id).RetailPoses!.Current.PositionFloatBits);
        Assert.Equal(0, mechanics.Snapshot.LastConsumedCommandSequence);
    }

    [Fact]
    public void StopClearsGuideAndDriveWithoutDestroyingRetainedVelocity()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId id = actors.GetThingRef("Air Trainer")!.Value;
        mechanics.AdvanceTick(); mechanics.AdvanceTick();
        ThingActorBaseStateSnapshot before = actors.GetBaseState(id);
        mechanics.ApplyCommand(new(1, 0, id, Level100ActorScriptCommandKind.Stop, null, null, 0));
        ThingActorBaseStateSnapshot stopped = actors.GetBaseState(id);
        Assert.Equal(before.RetailPlane!.Velocity, stopped.RetailPlane!.Velocity);
        Assert.Equal(default, stopped.RetailPlane.Drive);
        Assert.Equal(before.RetailPoses, stopped.RetailPoses);
        Level100PlaneGuideSnapshot guide = mechanics.Snapshot.Actors.Single(actor => actor.ActorId == id).PlaneGuide!;
        Assert.Equal(0, guide.Mode);
        Assert.Equal(before.RetailPoses!.Current.PositionFloatBits, guide.Destination);
        mechanics.AdvanceTick();
        Assert.NotEqual(before.RetailPoses.Current.PositionFloatBits, actors.GetBaseState(id).RetailPoses!.Current.PositionFloatBits);
    }

    [Fact]
    public void RegistryAndMechanicsRestoreContinueRawFlightAndOrderedCallbacks()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        Level100ActorId id = actors.GetThingRef("Air Trainer")!.Value;
        mechanics.ApplyCommand(new(1, 0, id, Level100ActorScriptCommandKind.FollowWaypoint,
            null, "Flyby Path", 0));
        for (int i = 0; i < 35; i++) mechanics.AdvanceTick();
        var restoredActors = new Level100ActorRegistry(definitions, actors.Snapshot);
        var restored = new Level100ActorMechanics(restoredActors, definitions, mechanics.Snapshot);
        for (int i = 0; i < 90; i++)
        {
            Assert.Equal(mechanics.AdvanceTick(), restored.AdvanceTick());
            Assert.Equal(actors.GetBaseState(id), restoredActors.GetBaseState(id));
            Assert.Equal(JsonSerializer.Serialize(mechanics.Snapshot), JsonSerializer.Serialize(restored.Snapshot));
        }
        Assert.Throws<ArgumentException>(() => new Level100ActorMechanics(restoredActors, definitions,
            restored.Snapshot with { PlaneEvents = null }));
        Assert.Throws<ArgumentException>(() => new Level100ActorMechanics(restoredActors, definitions,
            restored.Snapshot with { PlaneEvents = restored.Snapshot.PlaneEvents! with { Float24Arithmetic = false } }));
        Assert.Throws<ArgumentException>(() => new Level100ActorMechanics(restoredActors, definitions,
            restored.Snapshot with { Actors = restored.Snapshot.Actors.Select(actor => actor with { PlaneGuide = null }).ToArray() }));
    }

    [Fact]
    public void HashBindsSubprojectionMotionGuideCacheAndCallbackOrder()
    {
        // The load, where the Air Trainer still rests at the Airfield.
        var simulation = new Simulation(1, Level100TestActorDefinitions.LoadMaterialized());
        WorldSnapshot baseline = simulation.LoadSnapshotForMeasurement!;
        Level100ActorId trainer = baseline.Level100Actors.Actors.Single(actor => actor.Name == "Air Trainer").ActorId;
        Level100ActorBaseStateSnapshot plane = baseline.Level100Actors.BaseStates.Single(item => item.ActorId == trainer);
        string initial = StateHasher.ComputeHex(baseline);
        WorldSnapshot WithPhysical(ThingActorBaseStateSnapshot state) => baseline with
        {
            Level100Actors = baseline.Level100Actors with
            {
                BaseStates = baseline.Level100Actors.BaseStates.Select(item => item.ActorId == plane.ActorId ? item with { State = state } : item).ToArray(),
            },
        };
        foreach (int word in new[] { int.MinValue, 1 })
        {
            // -0 and the least positive subnormal both project to 0 mm.
            Assert.NotEqual(initial, StateHasher.ComputeHex(WithPhysical(plane.State with
            { RetailPlane = plane.State.RetailPlane! with { Velocity = new(word, 0, 0) } })));
            Assert.NotEqual(initial, StateHasher.ComputeHex(WithPhysical(plane.State with
            { RetailPlane = plane.State.RetailPlane! with { Drive = new(word, 0, 0) } })));
        }
        // One float step that stays inside the same millimetre: whichever
        // direction does not cross a rounding boundary where the plane is.
        ThingActorBaseStateSnapshot? subMillimeter = null;
        foreach (int step in new[] { 1, -1 })
        {
            var shifted = plane.State.RetailPoses!.Current.PositionFloatBits with
            { Y = plane.State.RetailPoses.Current.PositionFloatBits.Y + step };
            var candidate = plane.State with
            { RetailPoses = plane.State.RetailPoses with { Current = plane.State.RetailPoses.Current with { PositionFloatBits = shifted } } };
            try
            {
                if (new ThingActorBaseState(candidate).Snapshot.CurrentPose == plane.State.CurrentPose)
                {
                    subMillimeter = candidate;
                    break;
                }
            }
            catch (ArgumentException)
            {
                // This direction crossed into the next millimetre.
            }
        }
        Assert.NotNull(subMillimeter);
        Assert.NotEqual(initial, StateHasher.ComputeHex(WithPhysical(subMillimeter)));
        Assert.NotEqual(initial, StateHasher.ComputeHex(baseline with
        {
            Level100ActorMechanics = baseline.Level100ActorMechanics with
            { Actors = baseline.Level100ActorMechanics.Actors.Select(actor => actor.PlaneGuide is null ? actor : actor with
                { PlaneGuide = actor.PlaneGuide with { ClearanceCellX = 1 } }).ToArray() },
        }));
        RetailEventSchedulerSnapshot events = baseline.Level100ActorMechanics.PlaneEvents!;
        Assert.NotEqual(initial, StateHasher.ComputeHex(baseline with
        {
            Level100ActorMechanics = baseline.Level100ActorMechanics with
            { PlaneEvents = events with { Float24Arithmetic = false } },
        }));
        Assert.NotEqual(initial, StateHasher.ComputeHex(baseline with
        {
            Level100ActorMechanics = baseline.Level100ActorMechanics with
            { PlaneEvents = events with { Lanes = events.Lanes.Select(lane => lane with { Handles = lane.Handles.Reverse().ToArray() }).ToArray() } },
        }));
        Assert.Throws<NotSupportedException>(() => StateHasher.ComputeHex(baseline with
        { Level100Mission = baseline.Level100Mission with { WorldNumber = 110 } }));
    }
}
