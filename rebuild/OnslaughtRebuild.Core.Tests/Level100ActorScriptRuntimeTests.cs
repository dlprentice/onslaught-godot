// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100ActorScriptRuntimeTests
{
    [Fact]
    public void ReadyTargetsOneSpawnWithoutResumingOrRepeatingItsPausedInit()
    {
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var actors = new Level100ActorRegistry(definitions);
        var scripts = new Level100ActorScriptRuntime(actors, actors.GetThingRef("Player 1")!.Value);
        Level100ActorId Spawn()
        {
            var id = Assert.Single(actors.SpawnThing(actors.GetThingRef("Airfield")!.Value,
                "Target Drone", "SpawnerB", 1, "AirborneDrone1"));
            scripts.AttachAndInitializeSpawnedActor(id, "AirborneDrone1");
            return id;
        }
        Level100ActorId first = Spawn(), second = Spawn();
        Assert.Empty(scripts.DrainCommands()); // Ready must not run at attachment.
        var before = scripts.Snapshot;
        Assert.All(before.Instances, instance => Assert.Single(instance.Continuations));
        scripts.DispatchReady(first);
        Level100ActorScriptCommand command = Assert.Single(scripts.DrainCommands());
        Assert.Equal(first, command.ActorId);
        Assert.Equal(Level100ActorScriptCommandKind.FollowWaypoint, command.Kind);
        Assert.Equal("Drone Path 1", command.Argument);
        for (int i = 0; i < before.Instances.Count; i++)
            Assert.Equal(JsonSerializer.Serialize(before.Instances[i].Continuations),
                JsonSerializer.Serialize(scripts.Snapshot.Instances[i].Continuations));

        actors.ReportDied(first);
        foreach (var fact in actors.DrainFacts()) scripts.DispatchFact(fact);
        scripts.DrainCommands();
        scripts.DispatchReady(first); // Deleted script is not recreated.
        scripts.DispatchReady(new Level100ActorId(123456)); // Absent recipient is harmless.
        Assert.Empty(scripts.DrainCommands());
        scripts.DispatchReady(second);
        Assert.Equal(second, Assert.Single(scripts.DrainCommands()).ActorId);
    }

    [Fact]
    public void RestoreDuringHangarPausePreservesNativeSpawnAdmissionAndCurrentClock()
    {
        // This isolates the shipped Hangar callback and its real Pause/
        // SpawnThing continuation; publishing the event is not player acceptance.
        var definitions = Level100TestActorDefinitions.LoadMaterialized();
        var actors = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(actors, definitions);
        var destruction = new Level100DestructionRuntime(actors);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
        Level100ActorId airfield = actors.GetThingRef("Airfield")!.Value;
        uint eventFrame = 0;
        int eventTimeBits = 0;
        var originalAdmissions = new List<Level100ActorId>();
        Level100ActorScriptRuntime? scripts = null;
        scripts = new Level100ActorScriptRuntime(actors, player,
            id => RegisterBeforeInitializer(actors, mechanics, destruction,
                scripts!, id, originalAdmissions),
            () => eventTimeBits);
        scripts.InitializeReleasedScripts();
        mechanics.ConsumeCommands(scripts.DrainCommands());
        scripts.PublishEvent("Activate Airborne Targets 1");

        AdvanceClock();
        Advance(actors, mechanics, scripts);
        Level100ActorId firstDrone = Assert.Single(actors.Snapshot.Actors,
            actor => actor.ScriptName == "AirborneDrone1").ActorId;
        Level100ActorScriptRuntimeSnapshot pending = scripts.Snapshot;
        Level100ActorScriptContinuationSnapshot pause = Assert.Single(
            pending.Instances.Single(instance => instance.ActorId == airfield).Continuations,
            continuation => continuation.WaitKind == Level100ActorScriptWaitKind.Pause);
        int spawnTick = pause.DueTick!.Value;
        Assert.InRange(spawnTick, pending.Tick + 1, pending.Tick + 200);

        var restoredActors = new Level100ActorRegistry(definitions, actors.Snapshot);
        var restoredMechanics = new Level100ActorMechanics(restoredActors, definitions, mechanics.Snapshot);
        var restoredDestruction = new Level100DestructionRuntime(restoredActors, destruction.Snapshot);
        var restoredAdmissions = new List<Level100ActorId>();
        Level100ActorScriptRuntime? restoredScripts = null;
        restoredScripts = new Level100ActorScriptRuntime(restoredActors, player, pending,
            id => RegisterBeforeInitializer(restoredActors, restoredMechanics, restoredDestruction,
                restoredScripts!, id, restoredAdmissions),
            () => eventTimeBits);
        Assert.Empty(restoredAdmissions); // Restoration must not replay init or its spawns.

        while (scripts.Snapshot.Tick < spawnTick)
        {
            AdvanceClock();
            Advance(actors, mechanics, scripts);
            Advance(restoredActors, restoredMechanics, restoredScripts);
            if (scripts.Snapshot.Tick < spawnTick)
                Assert.Empty(restoredAdmissions);
        }

        Level100ActorId secondDrone = Assert.Single(actors.Snapshot.Actors,
            actor => actor.ScriptName == "AirborneDrone1" && actor.ActorId != firstDrone).ActorId;
        Assert.Equal(secondDrone, Assert.Single(restoredAdmissions));
        Assert.Contains(secondDrone, originalAdmissions);
        Assert.Equal(eventTimeBits, restoredActors.GetBaseState(secondDrone).RetailMotion!.LastMoveTimeFloatBits);
        Assert.True(restoredScripts.Snapshot.Instances.Single(instance => instance.ActorId == secondDrone).Initialized);
        Assert.Equal(JsonSerializer.Serialize(actors.Snapshot), JsonSerializer.Serialize(restoredActors.Snapshot));
        Assert.Equal(JsonSerializer.Serialize(mechanics.Snapshot), JsonSerializer.Serialize(restoredMechanics.Snapshot));
        Assert.Equal(JsonSerializer.Serialize(destruction.Snapshot), JsonSerializer.Serialize(restoredDestruction.Snapshot));
        Assert.Equal(JsonSerializer.Serialize(scripts.Snapshot), JsonSerializer.Serialize(restoredScripts.Snapshot));

        void AdvanceClock()
        {
            eventFrame++;
            eventTimeBits = BitConverter.SingleToInt32Bits(RetailEventScheduler.TimeAtFrameCount(eventFrame));
        }

        void Advance(Level100ActorRegistry registry, Level100ActorMechanics motion,
            Level100ActorScriptRuntime runtime)
        {
            motion.AdvanceEventClock(eventFrame);
            runtime.AdvanceTick();
            motion.ConsumeCommands(runtime.DrainCommands());
            foreach (Level100ActorMechanicsWaitCompletion completion in motion.AdvanceTick(eventFrame, id =>
                { runtime.DispatchReady(id); motion.ConsumeCommands(runtime.DrainCommands()); }))
                Assert.True(runtime.CompleteMechanicsWait(completion.ActorId, completion.WaitKind, completion.Argument));
            motion.ConsumeCommands(runtime.DrainCommands());
        }

        void RegisterBeforeInitializer(Level100ActorRegistry registry, Level100ActorMechanics motion,
            Level100DestructionRuntime damage, Level100ActorScriptRuntime runtime,
            Level100ActorId id, List<Level100ActorId> admissions)
        {
            Assert.DoesNotContain(runtime.Snapshot.Instances, instance => instance.ActorId == id);
            motion.RegisterSpawnedActor(id);
            damage.RegisterActor(id);
            if (registry.GetBaseState(id).RetailPlane is not null)
            {
                Assert.Equal(eventTimeBits, registry.GetBaseState(id).RetailMotion!.LastMoveTimeFloatBits);
                Assert.NotNull(motion.Snapshot.Actors.Single(actor => actor.ActorId == id).PlaneGuide);
                Assert.Contains(damage.Snapshot.Actors, actor => actor.ActorId == id.Value);
            }
            admissions.Add(id);
        }
    }
}
