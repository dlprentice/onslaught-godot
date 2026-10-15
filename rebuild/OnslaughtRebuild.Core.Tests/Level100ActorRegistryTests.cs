// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100ActorRegistryTests
{
    [Theory]
    [InlineData(false, "Flyby")]
    [InlineData(true, "AirTrainer")]
    public void MaterializedAirTrainer_StartsWithItsPhysicsProfileLife(bool spawn, string script)
    {
        // Retail physics type1 Air Trainer field3 is 0x40400000 (3.0).
        // Both instances must carry that life without a destruction-catalog fallback.
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId id = spawn
            ? Assert.Single(registry.SpawnThing(registry.GetThingRef("Airfield")!.Value,
                "Air Trainer", "SpawnerB", 1, script))
            : registry.GetThingRef("Air Trainer")!.Value;
        Level100ActorSnapshot actor = registry.GetActor(id);
        Assert.Equal("Air Trainer", actor.DefinitionName);
        Assert.Equal(script, actor.ScriptName);
        Assert.Equal(3_000, actor.Health);
        Assert.Equal(Level100ActorLifecycle.Alive, actor.Lifecycle);
        Assert.True(actor.Active);
        Assert.Equal(spawn, actor.SpawnOwnerId is not null);
        int suppliedHealth = spawn
            ? definitions.Spawns.Single(item => item.DefinitionName == "Air Trainer").InitialHealth
            : definitions.Actors.Single(item => item.Name == "Air Trainer").InitialHealth;
        Assert.Equal(3_000, suppliedHealth);
        Assert.Equal(actor, new Level100ActorRegistry(definitions, registry.Snapshot).GetActor(id));
    }

    [Fact]
    public void ReleasedRegistry_ConsumesBaseStateAndPreservesLeafMaskCompatibility()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId player = registry.GetThingRef("Player 1")!.Value;
        Level100ActorSnapshot legacy = registry.GetActor(player);

        ThingActorBaseStateSnapshot initial = registry.GetBaseState(player);
        Assert.Equal(
            Level100ReleasedThingTypeMasks.BattleEngine,
            legacy.ThingTypeMask);
        Assert.Equal(0x8000000Bu, initial.ThingTypeMask);
        Assert.Equal(legacy.Pose.PositionMillimeters, initial.CurrentPose.PositionMillimeters);
        Assert.Equal(initial.CurrentPose, initial.OldPose);
        Assert.Equal(legacy.Pose.LinearVelocityMillimetersPerTick, initial.Velocity);

        registry.MakeInvisible(player);
        registry.DeclareOnGround(player, BitConverter.SingleToInt32Bits(12.5f));
        Assert.True(registry.ReportStartedDying(player));

        ThingActorBaseStateSnapshot changed = registry.GetBaseState(player);
        Assert.True(changed.IsInvisible);
        Assert.True(changed.IsDying);
        Assert.True(changed.IsShuttingDown);
        Assert.Equal(
            BitConverter.SingleToInt32Bits(12.5f),
            changed.LastTimeOnGroundFloatBits);
        Assert.Equal(
            Level100ActorLifecycle.StartedDying,
            registry.GetActor(player).Lifecycle);

        Level100ActorRegistrySnapshot snapshot = registry.Snapshot;
        var restored = new Level100ActorRegistry(definitions, snapshot);
        Assert.Equal(changed, restored.GetBaseState(player));
        Assert.Equal(snapshot.BaseStates, restored.Snapshot.BaseStates);

        WorldSnapshot world = new Simulation(0x100u, definitions).Snapshot;
        Assert.Equal(
            StateHasher.ComputeHex(world with { Level100Actors = snapshot }),
            StateHasher.ComputeHex(world with { Level100Actors = restored.Snapshot }));
    }

    [Fact]
    public void CanonicalHash_RetainsThingActorBaseStateAndRepeatsExactly()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        WorldSnapshot first = new Simulation(0xBACEu, definitions).Snapshot;
        WorldSnapshot second = new Simulation(0xBACEu, definitions).Snapshot;
        string canonical = StateHasher.ComputeHex(first);

        Assert.Equal(canonical, StateHasher.ComputeHex(second));

        Level100ActorBaseStateSnapshot firstActor = first.Level100Actors.BaseStates[0];
        ThingActorBaseStateSnapshot state = firstActor.State;
        ThingActorBaseStateSnapshot[] variants =
        [
            state with { Flags = ThingActorFlags.Invisible },
            state with
            {
                CurrentPose = state.CurrentPose with
                {
                    PositionMillimeters = state.CurrentPose.PositionMillimeters with
                    {
                        X = state.CurrentPose.PositionMillimeters.X + 1,
                    },
                },
            },
            state with
            {
                OldPose = state.OldPose with
                {
                    PositionMillimeters = state.OldPose.PositionMillimeters with
                    {
                        Z = state.OldPose.PositionMillimeters.Z + 1,
                    },
                },
            },
            state with { Velocity = new SimVector3(1, 2, 3) },
            state with { AngularVelocity = new SimVector3(4, 5, 6) },
            state with
            {
                ThingTypeMask = state.ThingTypeMask ^
                    Level100ReleasedThingTypeMasks.Ammunition,
            },
            state with
            {
                LastTimeOnGroundFloatBits = BitConverter.SingleToInt32Bits(1.0f),
            },
            state with
            {
                LastTimeInWaterFloatBits = BitConverter.SingleToInt32Bits(2.0f),
            },
            state with
            {
                LastTimeOnObjectFloatBits = BitConverter.SingleToInt32Bits(3.0f),
            },
        ];

        Assert.All(variants, variant =>
        {
            Level100ActorRegistrySnapshot changedRegistry = first.Level100Actors with
            {
                BaseStates = first.Level100Actors.BaseStates
                    .Select(item => item.ActorId == firstActor.ActorId
                        ? item with { State = variant }
                        : item)
                    .ToArray(),
            };
            Assert.NotEqual(
                canonical,
                StateHasher.ComputeHex(first with { Level100Actors = changedRegistry }));
        });
    }

    [Fact]
    public void ReleasedRegistry_ResolvesStableActorsAndRoundTripsAllMutableState()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId tankFactory = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Tank Factory"));
        Level100ActorId airfield = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Airfield"));
        Level100ActorId targetZone1 = Assert.IsType<Level100ActorId>(
            registry.GetThingRef("Target Zone 1"));

        Assert.Equal(2, tankFactory.Value);
        Assert.Equal(10, airfield.Value);
        Assert.Equal(12, targetZone1.Value);
        Assert.Null(registry.GetThingRef("Not In Level 100"));

        // The integer setup/reset contract belongs to a non-raw actor.
        // Aircraft have separate complete raw-state creation/restore tests.
        Level100ActorId target = Assert.Single(registry.SpawnThing(
            tankFactory,
            "Target Truck",
            "SpawnerA",
            1,
            "TargetTruck1"));
        var pose = new Level100ActorPoseSnapshot(
            new SimVector3(1, 2, 3),
            new Level100FloatBasis3Bits(
                BitConverter.SingleToInt32Bits(1f), 0, 0,
                0, BitConverter.SingleToInt32Bits(1f), 0,
                0, 0, BitConverter.SingleToInt32Bits(1f)),
            new SimVector3(7, 8, 9),
            new SimVector3(10, 11, 12));
        registry.SetPose(target, pose);
        registry.SetHealth(target, 321);
        registry.Deactivate(target);
        registry.SetObjective(target, true);
        registry.ReportHit(target, tankFactory);
        Assert.True(registry.ReportStartedDying(target));
        Assert.True(registry.ReportDied(target));
        Assert.False(registry.ReportDied(target));

        Level100ActorRegistrySnapshot snapshot = registry.Snapshot;
        Level100ActorSnapshot actor = snapshot.Actors.Single(item => item.ActorId == target);
        Assert.Equal(definitions.IdentitySha256, snapshot.DefinitionSetIdentitySha256);
        Assert.Equal(definitions.Actors.Count + 1, target.Value);
        Assert.Equal("Target Truck", actor.DefinitionName);
        Assert.Equal("TargetTruck1", actor.ScriptName);
        Assert.Equal(tankFactory, actor.SpawnOwnerId);
        Assert.Equal("SpawnerA", actor.SpawnerName);
        Assert.Equal(pose, actor.Pose);
        Assert.Equal(321, actor.Health);
        Assert.Equal(Level100ActorLifecycle.Destroyed, actor.Lifecycle);
        Assert.False(actor.Active);
        Assert.False(actor.IsObjective);
        Assert.Throws<InvalidOperationException>(() => registry.Activate(target));
        Assert.Throws<InvalidOperationException>(() => registry.SetObjective(target, true));
        Assert.Equal(
            [
                Level100ActorFactKind.Hit,
                Level100ActorFactKind.StartedDying,
                Level100ActorFactKind.Died,
            ],
            snapshot.PendingFacts.Select(item => item.Kind).ToArray());

        var restored = new Level100ActorRegistry(definitions, snapshot);
        Level100ActorRegistrySnapshot restoredSnapshot = restored.Snapshot;
        Assert.Equal(snapshot.NextActorId, restoredSnapshot.NextActorId);
        Assert.Equal(snapshot.NextFactSequence, restoredSnapshot.NextFactSequence);
        Assert.Equal(snapshot.Actors, restoredSnapshot.Actors);
        Assert.Equal(snapshot.PendingFacts, restoredSnapshot.PendingFacts);

        Level100ActorRegistrySnapshot impossibleLifecycle = snapshot with
        {
            Actors = snapshot.Actors
                .Select(item => item.ActorId == target
                    ? item with { Active = true, IsObjective = true }
                    : item)
                .ToArray(),
        };
        Assert.Throws<ArgumentException>(() =>
            new Level100ActorRegistry(definitions, impossibleLifecycle));

        WorldSnapshot world = new Simulation(0x100u, definitions).Snapshot;
        Assert.Equal(
            StateHasher.ComputeHex(world with { Level100Actors = snapshot }),
            StateHasher.ComputeHex(world with { Level100Actors = restoredSnapshot }));
    }

    [Fact]
    public void InvalidSpawn_DoesNotConsumeIdentityOrMutateRegistry()
    {
        var registry = new Level100ActorRegistry(Level100TestActorDefinitions.Create());
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100ActorRegistrySnapshot before = registry.Snapshot;

        Assert.Throws<InvalidOperationException>(() => registry.SpawnThing(
            airfield,
            "Air Trainer",
            "SpawnerB",
            2,
            "AirTrainer"));

        Level100ActorRegistrySnapshot after = registry.Snapshot;
        Assert.Equal(before.NextActorId, after.NextActorId);
        Assert.Equal(before.NextFactSequence, after.NextFactSequence);
        Assert.Equal(before.Actors, after.Actors);
        Assert.Equal(before.PendingFacts, after.PendingFacts);
    }

    /// <summary>
    /// Two definition sets with identical node coordinates but different
    /// routes are different definition sets.
    /// </summary>
    /// <remarks>
    /// The list order decides which node wins a tie for the start and each
    /// node's target decides where the walk goes next, so both change the
    /// simulation from the same geometry. If <c>ComputeIdentity</c> ignored
    /// either, a snapshot taken under one would restore cleanly under the
    /// other and a replay would silently diverge.
    /// </remarks>
    [Fact]
    public void DefinitionIdentity_SeparatesRoutesThatDifferOnlyInOrderOrTargets()
    {
        Level100ActorDefinitionSet original = Level100TestActorDefinitions.Create();
        var reordered = new Level100ActorDefinitionSet(
            original.Actors,
            original.Spawns,
            original.WaypointPaths
                .Select(path => path with { Points = path.Points.Reverse().ToArray() })
                .ToArray(),
            original.MotionDefinitions);
        var untargeted = new Level100ActorDefinitionSet(
            original.Actors,
            original.Spawns,
            original.WaypointPaths
                .Select(path => path with
                {
                    Points = path.Points.Select(point => point with { TargetNodeIndex = null }).ToArray(),
                })
                .ToArray(),
            original.MotionDefinitions);

        // Same geometry, node for node, on every path.
        foreach (Level100WaypointPathDefinition path in original.WaypointPaths)
        {
            Assert.Equal(
                path.Points.OrderBy(point => point.NodeIndex).Select(point => point.PositionMillimeters),
                reordered.GetWaypointPath(path.Name).Points
                    .OrderBy(point => point.NodeIndex).Select(point => point.PositionMillimeters));
        }

        Assert.NotEqual(original.IdentitySha256, reordered.IdentitySha256);
        Assert.NotEqual(original.IdentitySha256, untargeted.IdentitySha256);
    }

    /// <summary>
    /// <c>0x00505c30</c> replaces its running minimum only on a strictly
    /// smaller distance, walking the list in order from 9999999.0, so a tie
    /// keeps the earlier node and a node that far or farther is never taken
    /// (waypoint-paths.md, "Following").
    /// </summary>
    [Fact]
    public void NearestPoint_KeepsTheEarlierNodeOnATieAndStartsBelow9999999()
    {
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
        static Level100WaypointPointDefinition Node(int index, float x) =>
            new(index, SimVector3.Zero, new Level100FloatVector4Bits(Bits(x), Bits(0), Bits(0), 0));
        var origin = new Level100FloatVector3Bits(Bits(0), Bits(0), Bits(0));

        var tie = new Level100WaypointPathDefinition("Tie", [Node(7, 2), Node(3, -2), Node(5, 9)]);
        Assert.Equal(7, tie.NearestPoint(origin)!.NodeIndex);
        var nearerLater = new Level100WaypointPathDefinition("Later", [Node(7, 2), Node(3, -1)]);
        Assert.Equal(3, nearerLater.NearestPoint(origin)!.NodeIndex);
        Assert.Null(new Level100WaypointPathDefinition("Far", [Node(1, 4_000)]).NearestPoint(origin));
    }

    [Fact]
    public void AircraftWeaponMounts_KeepReleasedUseOrderSelectorsAndRawModelWords()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        var trainer = Assert.Single(definitions.GetMotionDefinition("Air Trainer").WeaponMounts!);
        var drone = definitions.GetMotionDefinition("Target Drone").WeaponMounts!;
        Assert.Equal(new RetailUnitConstructionUse("Forseti Missile Trainer Launcher", "GunB", 0x20400), trainer.Use);
        Assert.Equal(new[] { "Drone Vulcan Cannon", "Forseti Drone Missile Launcher" },
            drone.Select(mount => mount.Use.DefinitionName));
        Assert.Equal(new[] { "GunA", "GunB" }, drone.Select(mount => mount.Use.TagName));
        Assert.All(drone.Append(trainer), mount =>
        {
            Assert.Equal(1, mount.Selector);
            Assert.Equal(0x20400u, mount.Use.RawCreationFlags);
            Assert.Equal(new Level100FloatBasis3Bits(0x3f800000, unchecked((int)0xa818719e), 0,
                0x2818719e, 0x3f800000, 0, 0, 0, 0x3f800000), mount.ModelPose.BasisFloatBits);
        });
        Assert.Equal(new Level100FloatVector3Bits(unchecked((int)0xbd60ceb4), 0x3f6a3a59,
            unchecked((int)0xbd4d3bb0)), drone[0].ModelPose.PositionFloatBits);
        Assert.Equal(new Level100FloatVector3Bits(unchecked((int)0xbb7ae95a), 0x3f5b84b4,
            0x3d88c1bf), drone[1].ModelPose.PositionFloatBits);
        Assert.Equal(trainer.ModelPose, drone[1].ModelPose);
    }

    [Fact]
    public void DefinitionIdentity_OwnsWeaponMountCollectionsInBothViews()
    {
        Level100ActorDefinitionSet original = Level100TestActorDefinitions.Create();
        var supplied = original.GetMotionDefinition("Target Drone").WeaponMounts!.ToList();
        var expected = supplied.ToArray();
        Level100ActorDefinitionSet owned = WithDroneMounts(original, supplied);
        string identity = owned.IdentitySha256;
        supplied.Clear();
        var byName = owned.GetMotionDefinition("Target Drone");
        Assert.Same(byName, owned.MotionDefinitions.Single(item => item.DefinitionName == "Target Drone"));
        Assert.Equal(expected, byName.WeaponMounts!);
        Assert.Equal(identity, owned.IdentitySha256);
        var view = Assert.IsAssignableFrom<IList<Level100ActorWeaponMountDefinition>>(byName.WeaponMounts);
        Assert.Throws<NotSupportedException>(() => view[0] = expected[1]);
    }

    [Fact]
    public void DefinitionIdentity_BindsEveryWeaponUseAndPoseInputAndRejectsCrossSetRestore()
    {
        Level100ActorDefinitionSet original = Level100TestActorDefinitions.Create();
        var mounts = original.GetMotionDefinition("Target Drone").WeaponMounts!;
        var first = mounts[0];
        var changedFirst = new[]
        {
            first with { Use = first.Use with { DefinitionName = "Another weapon" } },
            first with { Use = first.Use with { TagName = "GunB" } },
            first with { Use = first.Use with { RawCreationFlags = first.Use.RawCreationFlags ^ 0x80000000 } },
            first with { Selector = 2 },
            first with { ModelPose = first.ModelPose with { PositionFloatBits = first.ModelPose.PositionFloatBits with
                { X = first.ModelPose.PositionFloatBits.X ^ 1 } } },
            first with { ModelPose = first.ModelPose with { BasisFloatBits = first.ModelPose.BasisFloatBits with
                { Row0Z = int.MinValue } } }, // +0 and -0 must remain distinct.
        };
        var snapshot = new Level100ActorRegistry(original).Snapshot;
        foreach (var changed in changedFirst)
        {
            Level100ActorDefinitionSet candidate = WithDroneMounts(original, [changed, mounts[1]]);
            Assert.NotEqual(original.IdentitySha256, candidate.IdentitySha256);
            Assert.Throws<ArgumentException>(() => new Level100ActorRegistry(candidate, snapshot));
        }
        Assert.NotEqual(original.IdentitySha256, WithDroneMounts(original, mounts.Reverse().ToArray()).IdentitySha256);
        Assert.NotEqual(original.IdentitySha256, WithDroneMounts(original, [first]).IdentitySha256);
        var unavailable = WithDroneMounts(original, null);
        var empty = WithDroneMounts(original, []);
        Assert.Null(unavailable.GetMotionDefinition("Target Drone").WeaponMounts);
        Assert.Empty(empty.GetMotionDefinition("Target Drone").WeaponMounts!);
        Assert.NotEqual(unavailable.IdentitySha256, empty.IdentitySha256);
    }

    [Fact]
    public void WeaponMountAdmission_RejectsMalformedBindingsAndNonfinitePoseWords()
    {
        Level100ActorDefinitionSet original = Level100TestActorDefinitions.Create();
        var first = original.GetMotionDefinition("Target Drone").WeaponMounts![0];
        foreach (var changed in new[]
        {
            null!, first with { Use = null! }, first with { Selector = 0 },
            first with { Use = first.Use with { TagName = "SpawnerA" } },
            first with { Use = first.Use with { DefinitionName = "" } },
            first with { ModelPose = first.ModelPose with { PositionFloatBits = first.ModelPose.PositionFloatBits with
                { X = 0x7fc00000 } } },
            first with { ModelPose = first.ModelPose with { BasisFloatBits = first.ModelPose.BasisFloatBits with
                { Row2Y = 0x7f800000 } } },
        })
            Assert.Throws<ArgumentException>(() => WithDroneMounts(original, [changed]));
    }

    private static Level100ActorDefinitionSet WithDroneMounts(Level100ActorDefinitionSet original,
        IReadOnlyList<Level100ActorWeaponMountDefinition>? mounts) => new(
            original.Actors, original.Spawns, original.WaypointPaths,
            original.MotionDefinitions.Select(definition => definition.DefinitionName == "Target Drone"
                ? definition with { WeaponMounts = mounts } : definition),
            baseWorldPineCount: original.BaseWorldPineCount);

    [Fact]
    public void DefinitionIdentity_OwnsAuthoredDataAndRejectsCrossSetRestore()
    {
        Level100ActorDefinitionSet original = Level100TestActorDefinitions.Create();
        Level100ActorDefinition first = original.Actors[0];
        Level100ActorPoseSnapshot changedPose = first.InitialPose with
        {
            PositionMillimeters = first.InitialPose.PositionMillimeters with
            {
                X = first.InitialPose.PositionMillimeters.X + 1,
            },
        };
        Level100ActorDefinition[] changedActors = original.Actors.ToArray();
        changedActors[0] = first with { InitialPose = changedPose };
        var changed = new Level100ActorDefinitionSet(
            changedActors,
            original.Spawns,
            original.WaypointPaths,
            original.MotionDefinitions);
        var withPath = new Level100ActorDefinitionSet(
            original.Actors,
            original.Spawns,
            [
                new Level100WaypointPathDefinition(
                    "Path",
                    [
                        new Level100WaypointPointDefinition(
                            1,
                            new SimVector3(10, 30, 20),
                            new Level100FloatVector4Bits(1, 2, 3, 4)),
                    ]),
            ],
            original.MotionDefinitions);

        Assert.NotEqual(original.IdentitySha256, changed.IdentitySha256);
        Assert.NotEqual(original.IdentitySha256, withPath.IdentitySha256);
        Level100ActorRegistrySnapshot snapshot = new Level100ActorRegistry(original).Snapshot;
        Assert.Throws<ArgumentException>(() => new Level100ActorRegistry(changed, snapshot));

        WorldSnapshot originalWorld = new Simulation(0x100u, original).Snapshot;
        WorldSnapshot changedWorld = new Simulation(0x100u, changed).Snapshot;
        Assert.NotEqual(
            StateHasher.ComputeHex(originalWorld),
            StateHasher.ComputeHex(changedWorld));

        Level100ActorRegistrySnapshot incomplete = snapshot with
        {
            Actors = snapshot.Actors.Skip(1).ToArray(),
        };
        Assert.Throws<ArgumentException>(() =>
            new Level100ActorRegistry(original, incomplete));

        Level100ActorRegistry registry = new(original);
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100ActorId drone = Assert.Single(registry.SpawnThing(
            airfield,
            "Target Drone",
            "SpawnerB",
            1,
            "AirborneDrone1"));
        Level100ActorRegistrySnapshot spawnedSnapshot = registry.Snapshot;
        Level100ActorSnapshot changedSpawn = registry.GetActor(drone) with { Name = "changed" };
        Level100ActorRegistrySnapshot changedSpawnSnapshot = spawnedSnapshot with
        {
            Actors = spawnedSnapshot.Actors
                .Select(actor => actor.ActorId == drone ? changedSpawn : actor)
                .ToArray(),
        };
        Assert.Throws<ArgumentException>(() =>
            new Level100ActorRegistry(original, changedSpawnSnapshot));
    }

    [Fact]
    public void Restore_AllowsReleasedSetupScriptMutationAndRejectsUnknownScript()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId tankFactory = registry.GetThingRef("Tank Factory")!.Value;
        registry.SetScript(tankFactory, "TankFactory");

        Level100ActorRegistrySnapshot setupSnapshot = registry.Snapshot;
        var restored = new Level100ActorRegistry(definitions, setupSnapshot);
        Assert.Equal("TankFactory", restored.GetActor(tankFactory).ScriptName);

        Level100ActorRegistrySnapshot unknownScript = setupSnapshot with
        {
            Actors = setupSnapshot.Actors
                .Select(actor => actor.ActorId == tankFactory
                    ? actor with { ScriptName = "NotAReleasedProgram" }
                    : actor)
                .ToArray(),
        };
        Assert.Throws<ArgumentException>(() =>
            new Level100ActorRegistry(definitions, unknownScript));
    }

    [Fact]
    public void SpawnThing_ComposesRetainedEmitterAndKeepsDistinctIds()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var registry = new Level100ActorRegistry(definitions);
        Level100ActorId airfield = registry.GetThingRef("Airfield")!.Value;
        Level100SpawnDefinition authored = definitions.Spawns.Single(item =>
            item.ScriptName == "AirborneDrone1");

        Level100ActorId[] actors = Enumerable.Range(0, 3)
            .Select(_ => Assert.Single(registry.SpawnThing(
                airfield,
                "Target Drone",
                "SpawnerB",
                1,
                "AirborneDrone1")))
            .ToArray();

        Assert.Equal(3, actors.Distinct().Count());
        Assert.Null(registry.GetThingRef("AirborneDrone1"));

        // Script SpawnThing uses the current seated owner's emitter. The
        // retained CEMT local Z is -6.1325388f; it is not the child's absolute
        // Core elevation. InitialPose predates this native composition.
        Assert.Equal(unchecked((int)0xc0c43dc2),
            authored.AuthoredEmitterTransform.LocalPositionFloatBits.Z);
        Level100ActorPoseSnapshot seated = registry.GetActor(actors[0]).Pose;
        Assert.Equal(authored.InitialPose.PositionMillimeters.X, seated.PositionMillimeters.X);
        Assert.Equal(authored.InitialPose.PositionMillimeters.Z, seated.PositionMillimeters.Z);
        Assert.InRange(seated.PositionMillimeters.Y - registry.GetActor(airfield).Pose.PositionMillimeters.Y,
            6_132, 6_134);
        Assert.Equal(SimVector3.Zero, seated.LinearVelocityMillimetersPerTick);
        Assert.Equal(SimVector3.Zero, seated.AngularVelocityMicroRadiansPerTick);
        Assert.All(actors, actorId =>
        {
            Assert.Equal(seated, registry.GetActor(actorId).Pose);
            Assert.Equal(0x40060a92, registry.GetBaseState(actorId).RetailPlane!.CurrentEuler.X);
        });
        Assert.DoesNotContain(registry.Snapshot.Actors, actor => actor.Pose is null);
    }

    [Fact]
    public void SetPose_RejectsNonFiniteBasisWithoutMutatingActor()
    {
        var registry = new Level100ActorRegistry(Level100TestActorDefinitions.Create());
        Level100ActorId player = registry.GetThingRef("Player 1")!.Value;
        Level100ActorPoseSnapshot before = registry.GetActor(player).Pose;
        Level100ActorPoseSnapshot invalid = before with
        {
            BasisFloatBits = before.BasisFloatBits with
            {
                Row0X = BitConverter.SingleToInt32Bits(float.NaN),
            },
        };

        Assert.Throws<ArgumentException>(() => registry.SetPose(player, invalid));
        Assert.Equal(before, registry.GetActor(player).Pose);
    }

    [Fact]
    public void SimulationRoutesExactFacilityEventSpellingsFromRegistryActors()
    {
        var tankFactorySimulation = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        Level100ActorId tankFactory = tankFactorySimulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Tank Factory")
            .ActorId;
        WorldSnapshot matching = tankFactorySimulation.Step(
            SimInput.Idle,
            [new Level100ActorDiedFact(tankFactory)]);
        Assert.Contains(
            matching.Level100Mission.Continuations,
            item => item.Execution.EventName == "Friendly Building Destroyed");

        var facilitiesSimulation = new Simulation(
            0x100u,
            Level100TestActorDefinitions.Create());
        Level100ActorId controlTower = facilitiesSimulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Name == "Control Tower")
            .ActorId;
        WorldSnapshot mismatched = facilitiesSimulation.Step(
            SimInput.Idle,
            [new Level100ActorDiedFact(controlTower)]);
        Assert.DoesNotContain(
            mismatched.Level100Mission.Continuations,
            item => item.Execution.EventName is
                "Friendly Building Destroyed" or "Destroyed Friendly Building");
    }
}
