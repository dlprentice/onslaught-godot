// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100RawPlaneCreationTests
{
    [Theory]
    [InlineData("SpawnerA")]
    [InlineData("SpawnerB")]
    public void ExitPointUsesItsOwnSelectorAndSeatedOwner(string spawner)
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100ActorId plane = Assert.Single(registry.SpawnThing(airfield,
            "Target Drone", spawner, 1, "AirborneDrone2"));
        RetailUnitAttachmentPose exit = registry.GetPlaneSpawnerExitPoint(plane, 1)!.Value;
        // Independent PC24 calculation from the shipped CEMT/CPOS/CORI and
        // HFLD samples. Seated owner Z=c11d3775; this is an arithmetic/input
        // comparison, not a captured retail cached-world pose.
        Assert.Equal(spawner == "SpawnerA"
            ? W(0x43b06060, 0x438608f1, 0xc17f31aa)
            : W(0x439fb03f, 0x4378ae2e, 0xc17f31aa), exit.PositionFloatBits);
        Assert.Equal(new Level100FloatBasis3Bits(
            unchecked((int)0xbf000001), unchecked((int)0xbf5db3d7), 0,
            0x3f5db3d7, unchecked((int)0xbf000001), 0,
            0, 0, 0x3f800000), exit.BasisFloatBits);
        Assert.Null(registry.GetPlaneSpawnerExitPoint(plane, 0));
        Assert.Null(registry.GetPlaneSpawnerExitPoint(plane, 2));
        Assert.NotEqual(registry.GetBaseState(plane).RetailPoses!.Current.PositionFloatBits,
            exit.PositionFloatBits);
        Assert.Equal(exit, new Level100ActorRegistry(definitions, registry.Snapshot)
            .GetPlaneSpawnerExitPoint(plane, 1));
        Assert.Throws<NotSupportedException>(() => registry.GetPlaneSpawnerExitPoint(airfield, 1));
    }

    [Fact]
    public void ExitInputIsOwnedAndBoundToDefinitionIdentity()
    {
        var original = Level100TestActorDefinitions.LoadMaterialized();
        Level100SpawnDefinition source = original.Spawns.First(item => item.SpawnerExitWaypoints is not null);
        Level100SpawnerExitPoint point = Assert.Single(source.SpawnerExitWaypoints!);
        var callerOwned = new List<Level100SpawnerExitPoint> { point };
        Level100ActorDefinitionSet With(IReadOnlyList<Level100SpawnerExitPoint>? points) => new(
            original.Actors, original.Spawns.Select(item => item == source
                ? item with { SpawnerExitWaypoints = points } : item), original.WaypointPaths, original.MotionDefinitions,
            baseWorldPineCount: original.BaseWorldPineCount, safeSides: original.SafeSides);
        Level100ActorDefinitionSet owned = With(callerOwned);
        callerOwned.Clear();
        Assert.Equal(original.IdentitySha256, owned.IdentitySha256);
        Assert.Single(owned.Spawns.Single(item => item.DefinitionIdentity == source.DefinitionIdentity).SpawnerExitWaypoints!);
        foreach (Level100ActorDefinitionSet changed in new[]
        {
            With(null), With([point with { Selector = 2 }]),
            With([point with { ModelTransform = point.ModelTransform with
            { LocalPositionFloatBits = point.ModelTransform.LocalPositionFloatBits with
                { X = point.ModelTransform.LocalPositionFloatBits.X ^ 1 } } }]),
            With([point with { ModelTransform = point.ModelTransform with
            { LocalBasisFloatBits = point.ModelTransform.LocalBasisFloatBits with { Row0Y = int.MinValue } } }]),
        })
        {
            Assert.NotEqual(original.IdentitySha256, changed.IdentitySha256);
            Assert.Throws<ArgumentException>(() => new Level100ActorRegistry(changed,
                new Level100ActorRegistry(original).Snapshot));
        }
        Assert.Throws<ArgumentException>(() => With([]));
        Assert.Throws<ArgumentException>(() => With([point, point]));
        Assert.Throws<ArgumentException>(() => With([point with { Selector = 0 }]));
        Assert.Throws<ArgumentException>(() => With([point with { ModelTransform = point.ModelTransform with
        { LocalPositionFloatBits = new(0x7f800000, 0, 0) } }]));
    }

    [Fact]
    public void LegacyMissingExitInputIsNotAMissingSelector()
    {
        var source = Level100TestActorDefinitions.LoadMaterialized();
        var legacy = new Level100ActorDefinitionSet(source.Actors,
            source.Spawns.Select(spawn => spawn with { SpawnerExitWaypoints = null }),
            source.WaypointPaths, source.MotionDefinitions, baseWorldPineCount: source.BaseWorldPineCount);
        var registry = new Level100ActorRegistry(legacy);
        Level100ActorId id = Assert.Single(registry.SpawnThing(registry.GetThingRef("Airfield")!.Value,
            "Air Trainer", "SpawnerB", 1, "AirTrainer"));
        Assert.Throws<NotSupportedException>(() => registry.GetPlaneSpawnerExitPoint(id, 1));
        Assert.Throws<NotSupportedException>(() => registry.GetPlaneSpawnerExitPoint(id, 2));
    }

    [Fact]
    public void AuthoredTrainerHasCompleteRawStateBeforeAnyCommand()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId id = registry.GetThingRef("Air Trainer")!.Value;
        ThingActorBaseStateSnapshot state = registry.GetBaseState(id);
        var motion = Assert.IsType<RetailPlaneMotionSnapshot>(state.RetailPlane);
        var poses = Assert.IsType<RetailActorPosePair>(state.RetailPoses);

        // Authored input and the first paired copied-runtime Plane entry.
        // The initializer's two negative-zero matrix components are material.
        Assert.Equal(W(0x4384c000, 0x43c44000, 0xc1700000), poses.Current.PositionFloatBits);
        Assert.Equal(new Level100FloatBasis3Bits(
            unchecked((int)0xbf800000), 0x33bbbd2e, int.MinValue,
            unchecked((int)0xb3bbbd2e), unchecked((int)0xbf800000), 0,
            int.MinValue, 0, 0x3f800000), poses.Current.BasisFloatBits);
        Assert.Equal(poses.Current, poses.Old);
        Assert.Equal(W(0x40490fdb, 0, 0), motion.CurrentEuler);
        Assert.Equal(motion.CurrentEuler, motion.DesiredEuler);
        Assert.Equal(W(0, 0, 0), motion.Velocity);
        Assert.Equal(W(0, 0, 0), motion.Drive);
        Assert.Equal(W(0x3d32b8c2, 0x3d32b8c2, 0x3d32b8c2), motion.EulerRates);
        Assert.Equal(0, motion.BankFlagFloatBits);
        Assert.Equal(new RetailActorMotionSnapshot(0, 1), state.RetailMotion);
        Assert.Equal(ThingActorBaseState.InitialContactTimeFloatBits, state.LastTimeOnGroundFloatBits);
        Assert.Equal(3_000, registry.GetActor(id).Health);
    }

    [Theory]
    [InlineData("Air Trainer", "AirTrainer")]
    [InlineData("Target Drone", "AirborneDrone1")]
    public void SpawnRetainsCreationTimeAndComposesTheSeatedAirfield(string definition, string script)
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId owner = registry.GetThingRef("Airfield")!.Value;
        int creationTime = BitConverter.SingleToInt32Bits(12.5f);
        Level100ActorId id = Assert.Single(registry.SpawnThing(owner, definition,
            "SpawnerB", 1, script, creationTime));
        ThingActorBaseStateSnapshot state = registry.GetBaseState(id);
        var poses = Assert.IsType<RetailActorPosePair>(state.RetailPoses);
        var motion = Assert.IsType<RetailPlaneMotionSnapshot>(state.RetailPlane);

        Assert.Equal(new RetailActorMotionSnapshot(creationTime, 1), state.RetailMotion);
        Assert.Equal(poses.Current, poses.Old);
        Assert.Equal(W(0, 0, 0), motion.Velocity);
        Assert.Equal(W(0, 0, 0), motion.Drive);
        Assert.Equal(motion.CurrentEuler, motion.DesiredEuler);
        Assert.Equal(0, motion.CurrentEuler.Z);
        Assert.Equal(0x40060a92, motion.CurrentEuler.X);

        Level100SpawnDefinition input = definitions.Spawns.Single(item =>
            item.DefinitionName == definition && item.ScriptName == script);
        // The old precomputed pose composed an unseated owner and then
        // clamped the child itself. The released emitter remains above the
        // seated Airfield by its own local height.
        float localZ = BitConverter.Int32BitsToSingle(input.AuthoredEmitterTransform.LocalPositionFloatBits.Z);
        int localHeight = checked((int)Math.Round(-localZ * 1_000.0, MidpointRounding.AwayFromZero));
        int ownerY = registry.GetActor(owner).Pose.PositionMillimeters.Y;
        Assert.InRange(registry.GetActor(id).Pose.PositionMillimeters.Y - ownerY,
            localHeight - 1, localHeight + 1);
        Assert.NotEqual(input.InitialPose.PositionMillimeters.Y,
            registry.GetActor(id).Pose.PositionMillimeters.Y);

        var restored = new Level100ActorRegistry(definitions, registry.Snapshot);
        Assert.Equal(state, restored.GetBaseState(id));
    }

    [Fact]
    public void AirfieldEmittersRemainDistinctInBothFixtureAndProduction()
    {
        var definitions = Level100TestActorDefinitions.Create();
        var materialized = Level100TestActorDefinitions.LoadMaterialized();
        Assert.Equal(materialized.Actors.Single(item => item.Name == "Airfield").AuthoredTransform,
            definitions.Actors.Single(item => item.Name == "Airfield").AuthoredTransform);
        Assert.Equal(materialized.Actors.Single(item => item.Name == "Air Trainer").AuthoredTransform,
            definitions.Actors.Single(item => item.Name == "Air Trainer").AuthoredTransform);
        foreach (Level100SpawnDefinition row in definitions.Spawns.Where(item => item.DefinitionName is "Air Trainer" or "Target Drone"))
        {
            Level100SpawnDefinition source = materialized.Spawns.Single(item =>
                item.DefinitionName == row.DefinitionName && item.SpawnerName == row.SpawnerName && item.ScriptName == row.ScriptName);
            Assert.Equal(source.AuthoredEmitterTransform, row.AuthoredEmitterTransform);
        }
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId owner = registry.GetThingRef("Airfield")!.Value;
        Level100ActorId a = Assert.Single(registry.SpawnThing(owner, "Target Drone", "SpawnerA", 1, "AirborneDrone2"));
        Level100ActorId b = Assert.Single(registry.SpawnThing(owner, "Target Drone", "SpawnerB", 1, "AirborneDrone2"));
        ThingActorBaseStateSnapshot first = registry.GetBaseState(a), second = registry.GetBaseState(b);
        Assert.NotEqual(first.RetailPoses!.Current.PositionFloatBits, second.RetailPoses!.Current.PositionFloatBits);
        Assert.True(BitConverter.Int32BitsToSingle(first.RetailPlane!.CurrentEuler.X) < 0);
        Assert.True(BitConverter.Int32BitsToSingle(second.RetailPlane!.CurrentEuler.X) > 0);
    }

    [Fact]
    public void GroundTeleportPrecedesCurrentOnlyWaterClamp()
    {
        var released = Level100TestActorDefinitions.LoadMaterialized();
        // Explicit synthetic initializer: the admitted outside-grid terrain
        // sample is zero. This discriminates initialization order, not an
        // authored underwater Trainer or a playable route.
        var definitions = new Level100ActorDefinitionSet(released.Actors.Select(item =>
            item.Name != "Air Trainer" ? item : item with
            {
                AuthoredTransform = item.AuthoredTransform with
                {
                    RetailPositionFloatBits = W(0x44160000, 0x44160000, 0x42c80000),
                },
                InitialPose = item.InitialPose with
                {
                    PositionMillimeters = new(311_313, -110_000, 356_750),
                },
            }), released.Spawns, released.WaypointPaths, released.MotionDefinitions,
            baseWorldPineCount: released.BaseWorldPineCount);
        var registry = new Level100ActorRegistry(definitions);
        ThingActorBaseStateSnapshot state = registry.GetBaseState(registry.GetThingRef("Air Trainer")!.Value);
        Assert.Equal(0, state.RetailPoses!.Old.PositionFloatBits.Z);
        Assert.Equal(BitConverter.SingleToInt32Bits(Level100Terrain.Instance.WaterLevel),
            state.RetailPoses.Current.PositionFloatBits.Z);
        Assert.Equal(-10_000, state.OldPose.PositionMillimeters.Y);
        Assert.Equal(Level100Terrain.WaterElevationMillimeters, state.CurrentPose.PositionMillimeters.Y);
        Assert.Equal(state.RetailPoses.Current.BasisFloatBits, state.RetailPoses.Old.BasisFloatBits);
        Assert.Equal(state, new Level100ActorRegistry(definitions, registry.Snapshot)
            .GetBaseState(registry.GetThingRef("Air Trainer")!.Value));
    }

    [Fact]
    public void MovedSpawnOwnerAndInvalidClockAreRefusedBeforeIdentityAllocation()
    {
        var registry = new Level100ActorRegistry(Level100TestActorDefinitions.LoadMaterialized());
        Level100ActorId owner = registry.GetThingRef("Airfield")!.Value;
        Level100ActorRegistrySnapshot before = registry.Snapshot;
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.SpawnThing(owner,
            "Air Trainer", "SpawnerB", 1, "AirTrainer", 0x7fc00000));
        Assert.Equal(before.NextActorId, registry.Snapshot.NextActorId);
        Assert.Equal(before.Actors, registry.Snapshot.Actors);

        Level100ActorPoseSnapshot pose = registry.GetActor(owner).Pose;
        registry.SetPose(owner, pose with
        {
            PositionMillimeters = pose.PositionMillimeters with { X = pose.PositionMillimeters.X + 1 },
        });
        before = registry.Snapshot;
        Assert.Throws<NotSupportedException>(() => registry.SpawnThing(owner,
            "Air Trainer", "SpawnerB", 1, "AirTrainer"));
        Assert.Equal(before.NextActorId, registry.Snapshot.NextActorId);
        Assert.Equal(before.Actors, registry.Snapshot.Actors);
        Assert.Equal(before.BaseStates, registry.Snapshot.BaseStates);
    }

    [Fact]
    public void RawOwnerRejectsProjectedOverwriteAndSurvivesExistingLifecycle()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId id = registry.GetThingRef("Air Trainer")!.Value;
        ThingActorBaseState owner = registry.GetPlaneState(id);
        ThingActorBaseStateSnapshot before = owner.Snapshot;
        Assert.Throws<NotSupportedException>(() => registry.SetPose(id, registry.GetActor(id).Pose));
        Assert.Equal(before, owner.Snapshot);
        registry.MakeInvisible(id);
        registry.SetHealth(id, 321);
        registry.SetObjective(id, true);
        Assert.True(registry.ReportStartedDying(id));
        Assert.True(registry.ReportDied(id));
        Assert.Same(owner, registry.GetPlaneState(id));
        Assert.Equal(before.RetailPoses, owner.Snapshot.RetailPoses);
        Assert.Equal(before.RetailPlane, owner.Snapshot.RetailPlane);
        Assert.Equal(owner.Snapshot, new Level100ActorRegistry(definitions, registry.Snapshot).GetBaseState(id));
        // This tests compatibility with the existing terminal adapter. It does
        // not establish the still-unimplemented native dying-flight lifetime.
    }

    [Fact]
    public void RestoreAndHashRetainDriveThatTheProjectedPoseCannotExpress()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        WorldSnapshot world = new Simulation(0x100u, definitions).Snapshot;
        Level100ActorBaseStateSnapshot source = world.Level100Actors.BaseStates.Single(item =>
            world.Level100Actors.Actors.Single(actor => actor.ActorId == item.ActorId).Name == "Air Trainer");
        ThingActorBaseStateSnapshot changed = source.State with
        {
            RetailPlane = source.State.RetailPlane! with { Drive = W(0x3f800000, 0x80000000, 0) },
        };
        Level100ActorRegistrySnapshot registry = world.Level100Actors with
        {
            BaseStates = world.Level100Actors.BaseStates.Select(item =>
                item.ActorId == source.ActorId ? item with { State = changed } : item).ToArray(),
        };
        var restored = new Level100ActorRegistry(definitions, registry);
        Assert.Equal(changed, restored.GetBaseState(source.ActorId));
        Assert.Equal(world.Level100Actors.Actors, restored.Snapshot.Actors);
        Assert.NotEqual(StateHasher.ComputeHex(world), StateHasher.ComputeHex(world with { Level100Actors = registry }));
        Assert.Equal(StateHasher.ComputeHex(world with { Level100Actors = registry }),
            StateHasher.ComputeHex(world with { Level100Actors = restored.Snapshot }));

        restored.GetPlaneState(source.ActorId).ClearRetailPlaneDrive();
        Assert.Equal(W(0, 0, 0), restored.GetBaseState(source.ActorId).RetailPlane!.Drive);
        Assert.Equal(changed.RetailPlane!.Velocity, restored.GetBaseState(source.ActorId).RetailPlane!.Velocity);
    }

    private static Level100FloatVector3Bits W(uint x, uint y, uint z) =>
        new(unchecked((int)x), unchecked((int)y), unchecked((int)z));
}
