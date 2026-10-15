// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for world-200 admission: the third career node's compiled
/// script objects, heightfield, and measured level-world facts, from the
/// pinned <c>data/resources/200_res_PC.aya</c> (SHA-256
/// <c>99dbd433…b77</c>) on 2026-08-22 and embedded under the Level200
/// resource prefix. Nothing here reuses an earlier world's payload; every
/// hash pin is world 200's own.
/// </summary>
public sealed class RetailWorld200AdmissionTests
{
    private const int World200 = 200;

    [Fact]
    public void AllFourteenScriptObjects_AdmitWithTheirPinnedIdentities()
    {
        string[] expectedNames =
        [
            "EnemyLander", "EnemyLander2", "EnergyMonitor", "FighterAttack",
            "Hangar", "HealthMonitor", "LandingCraftAlpha", "LandingCraftBeta",
            "LandingCraftDelta", "LandingCraftGamma", "LevelScript", "Tatiana",
            "VitalBuilding", "WestAttacker",
        ];

        Assert.Equal(14, expectedNames.Length);
        Assert.Equal(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            Level100MissionProgram.ProgramNamesFor(World200)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray());
        foreach (string name in expectedNames)
        {
            Level100MissionProgram program = Level100MissionProgram.LoadEmbedded(World200, name);

            Assert.Equal(name, program.Name);
            Assert.NotEmpty(program.Instructions);
        }
    }

    [Fact]
    public void LevelScript_StructureMatchesTheMeasuredWorld200Pin()
    {
        Level100MissionProgram levelScript =
            Level100MissionProgram.LoadEmbedded(World200, "LevelScript");

        // Measured on 2026-08-22 from the pinned payload: 413 instructions,
        // 169 symbols, builtin[0] == 21 with the other twelve -1, sixteen
        // released named events carrying descriptive string values like every
        // measured world, initializer armed.
        Assert.Equal(413, levelScript.Instructions.Count);
        Assert.Equal(169, levelScript.Symbols.Count);
        Assert.Equal(21, levelScript.BuiltInEventInstructionPointers[0]);
        Assert.True(levelScript.BuiltInEventInstructionPointers
            .Skip(1).All(pointer => pointer == -1));
        Assert.True(levelScript.RunInitializer);
        Assert.Equal(16, levelScript.NamedEventInstructionPointers.Count);
        Assert.Equal(115, levelScript.NamedEventInstructionPointers["Alpha Down"]);
        Assert.Equal(
            397, levelScript.NamedEventInstructionPointers["Vital Building Destroyed"]);
    }

    /// <summary>
    /// The measured census pins the structural divergences world 200 shows
    /// against the first two nodes, and the heightfield envelope admits
    /// through the unchanged loader law (same 668,652-byte payload length).
    /// </summary>
    [Fact]
    public void World200Census_PinsTheMeasuredDivergences()
    {
        Assert.Equal(54, RetailWorld200LevelActors.InitialActorCount);
        Assert.Equal(3, RetailWorld200LevelActors.ActorHeaderA);
        Assert.Equal(0, RetailWorld200LevelActors.ActorHeaderB);
        Assert.Equal(2, RetailWorld200LevelActors.HeaderPostZerosWord);
        Assert.Equal(14, RetailWorld200LevelActors.ScriptObjectCount);
        Assert.Equal(80_232, RetailWorld200LevelActors.BaseWorldBytes);
        Assert.Equal(668_660, RetailWorld200LevelActors.HeightfieldEnvelopeBytes);

        // The base world is NOT the island world 110 shares with Level 100
        // (the shared BSWD: 54,669 bytes, SHA-256 04c5a383…10f4).
        Assert.NotEqual(
            "04C5A3838548A2C50819F46DC1F1746F7C20EC4AA34678BD23C8BCD2186010F4",
            RetailWorld200LevelActors.BaseWorldSha256);
        Assert.NotEqual(54_669, RetailWorld200LevelActors.BaseWorldBytes);
    }

    /// <summary>
    /// Reads the embedded world-200 HFLD envelope back out of the test
    /// assembly's Core dependency so the pin is checked against the exact
    /// bytes the simulation would consume.
    /// </summary>
    [Fact]
    public void World200Heightfield_IsItsOwnHashPinnedEnvelope()
    {
        System.Reflection.Assembly assembly = typeof(Level100Terrain).Assembly;
        using System.IO.Stream stream = assembly.GetManifestResourceStream(
            "OnslaughtRebuild.Core.Assets.Level200.level200-heightfield.hfld.bin")
            ?? throw new InvalidOperationException(
                "The world-200 heightfield resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        Assert.Equal("1b8eb8584be552383f10b08c75d9f10e91708343f0e5ee085d5130d369f6b945",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(memory.ToArray()))
                .ToLowerInvariant());
    }

    [Fact]
    public void World200AndEarlierTerrains_AreDistinctMeasuredWorlds()
    {
        // All three statics load lazily at first touch; all must admit without
        // throwing, and each records the exact envelope it was loaded from.
        Assert.NotNull(Level100Terrain.World200);
        Assert.NotNull(Level100Terrain.World110);
        Assert.NotNull(Level100Terrain.Instance);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World110);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World200);
        Assert.NotSame(Level100Terrain.World110, Level100Terrain.World200);
        Assert.Equal(
            Level100Terrain.World200SourceSha256,
            Level100Terrain.World200.PayloadSha256);
        Assert.NotEqual(
            Level100Terrain.SourceSha256,
            Level100Terrain.World200SourceSha256);
        Assert.NotEqual(
            Level100Terrain.World110SourceSha256,
            Level100Terrain.World200SourceSha256);
    }

    [Fact]
    public void UnknownWorlds_AndUnknownScriptsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(210, "LevelScript"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(200, "Nonexistent"));

        // World 300 is now separately measured and admitted; 311 stays out.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(311, "LevelScript"));
    }

    [Fact]
    public void EarlierWorldCallSites_StillLoadThroughTheirOverloads()
    {
        foreach (string name in Level100MissionProgram.ProgramNames)
        {
            Assert.Equal(name, Level100MissionProgram.LoadEmbedded(name).Name);
        }

        foreach (string name in Level100MissionProgram.ProgramNamesFor(110))
        {
            Assert.Equal(name, Level100MissionProgram.LoadEmbedded(110, name).Name);
        }

        Assert.Same(
            Level100Terrain.Instance,
            Level100Terrain.Instance);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World110);
    }
}
