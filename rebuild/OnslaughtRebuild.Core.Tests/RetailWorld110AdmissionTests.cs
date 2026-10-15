// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for world-110 admission: the second career node's compiled
/// script objects and heightfield, measured from the pinned
/// <c>data/resources/110_res_PC.aya</c> (SHA-256
/// <c>4e041c75…3c2b</c>) on 2026-08-22 and embedded under the Level110
/// resource prefix. Nothing here reuses a Level 100 payload; every hash pin
/// is world 110's own.
/// </summary>
public sealed class RetailWorld110AdmissionTests
{
    private const int World110 = 110;

    /// <summary>
    /// A world-110-stamped projection of the proven Level 100 test fixture.
    /// It is a direct mission-program test instrument only: the actor/spawn/path content
    /// remains the Level 100 fixture and makes no world-110 static-world claim.
    /// </summary>
    internal static Level100ActorDefinitionSet CreateWorld110Definitions()
    {
        Level100ActorDefinitionSet root = Level100TestActorDefinitions.Create();
        return new Level100ActorDefinitionSet(
            root.Actors,
            root.Spawns,
            root.WaypointPaths,
            root.MotionDefinitions,
            worldNumber: World110);
    }

    [Fact]
    public void AllThirteenScriptObjects_AdmitWithTheirPinnedIdentities()
    {
        string[] expectedNames =
        [
            "beacon", "Lander", "Lander2", "Lander3", "LevelScript",
            "MuspellFighter", "MuspellFighter1", "MuspellFighter2",
            "Scout", "Setup", "Victory", "VitalBuilding", "Weather",
        ];

        Assert.Equal(13, expectedNames.Length);
        Assert.Equal(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            Level100MissionProgram.ProgramNamesFor(World110)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray());
        foreach (string name in expectedNames)
        {
            Level100MissionProgram program = Level100MissionProgram.LoadEmbedded(World110, name);

            Assert.Equal(name, program.Name);
            Assert.NotEmpty(program.Instructions);
        }
    }

    [Fact]
    public void LevelScript_StructureMatchesTheMeasuredWorld110Pin()
    {
        Level100MissionProgram levelScript =
            Level100MissionProgram.LoadEmbedded(World110, "LevelScript");

        // Measured on 2026-08-22 from the pinned payload.
        Assert.Equal(181, levelScript.Instructions.Count);
        Assert.Equal(92, levelScript.Symbols.Count);
        Assert.Equal(11, levelScript.BuiltInEventInstructionPointers[0]);
        Assert.True(levelScript.BuiltInEventInstructionPointers
            .Skip(1).All(pointer => pointer == -1));
        Assert.True(levelScript.RunInitializer);
        Assert.Equal(5, levelScript.NamedEventInstructionPointers.Count);

        // The five released named events and their instruction pointers.
        Assert.Equal(100, levelScript.NamedEventInstructionPointers["Enemy Engaged"]);
        Assert.Equal(134, levelScript.NamedEventInstructionPointers["Vital Building Destroyed"]);
        Assert.Equal(145, levelScript.NamedEventInstructionPointers["Lander Escaped"]);
        Assert.Equal(159, levelScript.NamedEventInstructionPointers["Lander Destroyed"]);
        Assert.Equal(170, levelScript.NamedEventInstructionPointers["Lander Withdraws"]);
    }

    /// <summary>
    /// World 100 admission still works through the unchanged single-argument
    /// overloads — the L100 runtime call sites are untouched by the
    /// generalization.
    /// </summary>
    [Fact]
    public void Level100CallSites_StillLoadThroughSingleArgumentOverloads()
    {
        foreach (string name in Level100MissionProgram.ProgramNames)
        {
            Assert.Equal(name, Level100MissionProgram.LoadEmbedded(name).Name);
        }

        Assert.Same(
            Level100Terrain.Instance,
            Level100Terrain.Instance);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World110);
    }

    [Fact]
    public void World110Heightfield_IsItsOwnHashPinnedEnvelope()
    {
        // The measured HFLD envelope pin from the materializer table.
        Assert.Equal("fd4d076a2926fbc473b7d364703bdbc0c8a0f7a638b0ab71b6f319374da033c2",
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(World110Payload()))
                .ToLowerInvariant());
    }

    [Fact]
    public void World110AndLevel100Terrains_AreDistinctMeasuredWorlds()
    {
        // Both statics load lazily at first touch; both must admit without
        // throwing. Each records the exact envelope it was loaded from, and
        // the two recorded payloads are the two distinct measured worlds:
        // the released HFLD envelopes share the format law (same declared
        // size, same 137x148 CHFD grid, same scales) but differ in height
        // data from the first sample word — world 110 is its own world, not
        // a re-pinned Level 100 envelope.
        Assert.NotNull(Level100Terrain.World110);
        Assert.NotNull(Level100Terrain.Instance);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World110);
        Assert.Equal(
            Level100Terrain.World110SourceSha256,
            Level100Terrain.World110.PayloadSha256);
        Assert.Equal(
            Level100Terrain.SourceSha256,
            Level100Terrain.Instance.PayloadSha256);
        Assert.NotEqual(
            Level100Terrain.SourceSha256,
            Level100Terrain.World110SourceSha256);
    }

    [Fact]
    public void UnknownWorlds_AndUnknownScriptsAreRejected()
    {
        // World 200 was admitted by the generalization slice; the next
        // unmeasured world stays rejected.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(210, "LevelScript"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(110, "Nonexistent"));
    }

    /// <summary>
    /// This run re-derived native 88 directly from the exact 5,110-byte
    /// <c>f5c157ba…22aa</c> LevelScript: instruction 22 has attribute
    /// <c>0x00000258</c> (two arguments, void result), after loads of integer
    /// slot 1 and text id 114309509; instruction 34 is the first Pause. The
    /// source signature is <c>SetSecondaryObjectiveFailed(int num,
    /// int string_id)</c>, and the retail body writes state 2 plus the text
    /// dword into the distinct ten-slot secondary array. Construction must
    /// therefore cross native 88 and stop at that first legitimate wait,
    /// rather than treating an unsupported-native exception as admission.
    /// </summary>
    [Fact]
    public void MissionConstructor_World110CrossesNative88AndStopsAtItsFirstLegitimateWait()
    {
        Level100MissionProgram program =
            Level100MissionProgram.LoadEmbedded(World110, "LevelScript");
        (Level100Instruction Instruction, int Index) native88 = program.Instructions
            .Select((instruction, index) => (instruction, index))
            .Single(item =>
                item.instruction.Opcode == 24 &&
                (item.instruction.Attribute & 0xff) == 88);
        Assert.Equal(22, native88.Index);
        Assert.Equal(0x00000258, native88.Instruction.Attribute);
        Assert.Equal(1, program.Symbols[20].InitialValue.AsInteger());
        Assert.Equal(114309509, program.Symbols[21].InitialValue.AsInteger());
        Assert.Equal(24, program.Instructions[34].Opcode);
        Assert.Equal(4, program.Instructions[34].Attribute & 0xff);

        Level100ActorDefinitionSet definitions = CreateWorld110Definitions();
        var actors = new Level100ActorRegistry(definitions);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;

        var mission = new Level100Mission(
            actors,
            player,
            tutorialProgress: default,
            initialPlayerHealth: SimulationConstants.MaximumHull,
            worldNumber: World110);

        Level100MissionSnapshot snapshot = mission.Snapshot;
        Assert.Equal(World110, mission.WorldNumber);
        Assert.Equal(
            "f5c157ba2c6a9acbee78d895a25be82252951b93bdfdd8886a79ecd7bfe222aa",
            snapshot.ProgramSha256);
        Assert.True(snapshot.InitializerRan);
        Level100ScriptContinuationSnapshot wait = Assert.Single(snapshot.Continuations);
        Assert.Equal(Level100ScriptWaitKind.Pause, wait.WaitKind);
        Assert.Equal(35, wait.Execution.InstructionPointer);
        Level100MessageRequested protectMessage = Assert.Single(snapshot.PendingMessages);
        Assert.Equal(8444036, protectMessage.MessageId);
        Assert.False(protectMessage.ScriptWaitsForDuration);
        Assert.Equal(90, protectMessage.ExpectedPlaybackTicks);
        Assert.Equal(
            new RetailSecondaryObjectiveSnapshot(
                1,
                114309509,
                RetailSecondaryObjectiveStatus.Failed),
            snapshot.SecondaryObjectives[1]);
        Assert.All(
            snapshot.SecondaryObjectives.Where(item => item.Index != 1),
            item => Assert.Equal(RetailSecondaryObjectiveStatus.NotDefined, item.Status));
    }

    /// <summary>
    /// The exact world-110 LevelScript really contains native 84 at instruction
    /// 66 with attribute <c>0x00000254</c>, fed by slot 1 and text id 114309509.
    /// The ordinary opening remains stopped at the first Pause (instruction 34),
    /// so this test uses the explicitly named bounded Core instrument to execute
    /// only authored instructions 64..67. It does not claim the opening path has
    /// reached that later branch.
    /// </summary>
    [Fact]
    public void AuthoredWorld110Native84Branch_CompletesTheFailedSlotAndChangesTheCanonicalHash()
    {
        Level100MissionProgram program =
            Level100MissionProgram.LoadEmbedded(World110, "LevelScript");
        (Level100Instruction Instruction, int Index) native84 = program.Instructions
            .Select((instruction, index) => (instruction, index))
            .Single(item =>
                item.instruction.Opcode == 24 &&
                (item.instruction.Attribute & 0xff) == 84);
        Assert.Equal(66, native84.Index);
        Assert.Equal(0x00000254, native84.Instruction.Attribute);
        Assert.Equal(new Level100Instruction(5, 46), program.Instructions[64]);
        Assert.Equal(new Level100Instruction(5, 47), program.Instructions[65]);
        Assert.Equal(1, program.Symbols[46].InitialValue.AsInteger());
        Assert.Equal(114309509, program.Symbols[47].InitialValue.AsInteger());
        Assert.Equal(new Level100Instruction(14, -1), program.Instructions[67]);

        Level100ActorDefinitionSet definitions = CreateWorld110Definitions();
        Level100Mission CreateMission()
        {
            var actors = new Level100ActorRegistry(definitions);
            return new Level100Mission(
                actors,
                actors.GetThingRef("Player 1")!.Value,
                worldNumber: World110);
        }

        Level100Mission mission = CreateMission();
        Level100MissionSnapshot before = mission.Snapshot;
        Level100ScriptContinuationSnapshot openingWait = Assert.Single(before.Continuations);
        Assert.Equal(Level100ScriptWaitKind.Pause, openingWait.WaitKind);
        Assert.Equal(35, openingWait.Execution.InstructionPointer);
        Assert.Equal(RetailSecondaryObjectiveStatus.Failed, before.SecondaryObjectives[1].Status);

        mission.RunWorld110SecondaryObjectiveCompleteInstrument();

        Level100MissionSnapshot after = mission.Snapshot;
        Assert.Equal(
            new RetailSecondaryObjectiveSnapshot(
                1,
                114309509,
                RetailSecondaryObjectiveStatus.Complete),
            after.SecondaryObjectives[1]);
        Assert.Equal(
            before.SecondaryObjectives.Where(item => item.Index != 1),
            after.SecondaryObjectives.Where(item => item.Index != 1));
        Level100ScriptContinuationSnapshot afterWait = Assert.Single(after.Continuations);
        Assert.Equal(openingWait.Sequence, afterWait.Sequence);
        Assert.Equal(openingWait.DueTick, afterWait.DueTick);
        Assert.Equal(openingWait.WaitKind, afterWait.WaitKind);
        Assert.Equal(openingWait.WaitArgument, afterWait.WaitArgument);
        Assert.Equal(openingWait.Execution.EventName, afterWait.Execution.EventName);
        Assert.Equal(
            openingWait.Execution.InstructionPointer,
            afterWait.Execution.InstructionPointer);
        Assert.Equal(before.PendingMessages.ToArray(), after.PendingMessages.ToArray());
        Assert.Equal(before.Tick, after.Tick);

        Level100Mission repeat = CreateMission();
        repeat.RunWorld110SecondaryObjectiveCompleteInstrument();
        Assert.Equal(after.SecondaryObjectives, repeat.Snapshot.SecondaryObjectives);

        // Synthetic hash envelope only; all World110 script execution above
        // is through the direct mission owner, not an unsupported Simulation.
        WorldSnapshot envelope = Level100TestActorDefinitions.LegacyHashEnvelope(
            new Simulation(1, Level100TestActorDefinitions.Create()).Snapshot);
        string failedHash = StateHasher.ComputeHex(envelope with { Level100Mission = before });
        string completeHash = StateHasher.ComputeHex(envelope with { Level100Mission = after });
        string repeatHash = StateHasher.ComputeHex(
            envelope with { Level100Mission = repeat.Snapshot });
        Assert.NotEqual(failedHash, completeHash);
        Assert.Equal(completeHash, repeatHash);
    }

    /// <summary>
    /// A session's requested world and its definition-set stamp are one
    /// fail-closed identity. The fixture projection is deliberately otherwise
    /// byte-identical, so these assertions cannot pass because unrelated actor
    /// content happens to differ.
    /// </summary>
    [Fact]
    public void MissionAndSimulation_RejectDefinitionWorldMismatchesInBothDirections()
    {
        Level100ActorDefinitionSet root = Level100TestActorDefinitions.Create();
        var rootActors = new Level100ActorRegistry(root);
        Level100ActorId rootPlayer = rootActors.GetThingRef("Player 1")!.Value;
        Level100ActorDefinitionSet world110 = CreateWorld110Definitions();
        var world110Actors = new Level100ActorRegistry(world110);
        Level100ActorId world110Player = world110Actors.GetThingRef("Player 1")!.Value;

        Assert.Throws<ArgumentException>(() =>
            new Level100Mission(
                rootActors,
                rootPlayer,
                worldNumber: World110));
        Assert.Throws<ArgumentException>(() =>
            new Level100Mission(
                world110Actors,
                world110Player,
                worldNumber: Level100MissionProgram.WorldNumber100));
        Assert.Throws<ArgumentException>(() =>
            new Simulation(1, root, worldNumber: World110));
        Assert.Throws<ArgumentException>(() =>
            new Simulation(
                1,
                world110,
                worldNumber: Level100MissionProgram.WorldNumber100));
    }

    [Fact]
    public void MissionAndSimulation_RejectWorldsWithoutAnAdmittedSession()
    {
        Level100ActorDefinitionSet root = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(root);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;

        ArgumentOutOfRangeException missionFailure =
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Level100Mission(actors, player, worldNumber: 210));
        ArgumentOutOfRangeException simulationFailure =
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new Simulation(1, root, worldNumber: 210));

        Assert.Equal("worldNumber", missionFailure.ParamName);
        Assert.Equal("worldNumber", simulationFailure.ParamName);
    }

    /// <summary>
    /// Reads the embedded world-110 HFLD envelope back out of the test
    /// assembly's Core dependency so the pin is checked against the exact
    /// bytes the simulation would consume.
    /// </summary>
    private static byte[] World110Payload()
    {
        System.Reflection.Assembly assembly = typeof(Level100Terrain).Assembly;
        using System.IO.Stream stream = assembly.GetManifestResourceStream(
            "OnslaughtRebuild.Core.Assets.Level110.level110-heightfield.hfld.bin")
            ?? throw new InvalidOperationException(
                "The world-110 heightfield resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
