// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for measured world-300 payload admission from
/// <c>data/resources/300_res_PC.aya</c> (SHA-256
/// <c>7293bcbe…9efe4</c>). The archive's RLWD preamble is a measured variant,
/// not the earlier worlds' one-name header with a different world number.
/// </summary>
public sealed class RetailWorld300AdmissionTests
{
    private const int World300 = 300;

    [Fact]
    public void AllEightScriptObjects_AdmitWithTheirPinnedIdentities()
    {
        string[] expectedNames =
        [
            "dropship", "ForsetiUnit", "Level300script", "messages",
            "oforce", "outpostsphere", "Tank", "TankFactory",
        ];

        Assert.Equal(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            Level100MissionProgram.ProgramNamesFor(World300)
                .OrderBy(name => name, StringComparer.Ordinal).ToArray());
        foreach (string name in expectedNames)
        {
            Level100MissionProgram program = Level100MissionProgram.LoadEmbedded(World300, name);

            Assert.Equal(name, program.Name);
            Assert.NotEmpty(program.Instructions);
        }
    }

    [Fact]
    public void Level300script_StructureMatchesTheMeasuredPin()
    {
        Level100MissionProgram levelScript =
            Level100MissionProgram.LoadEmbedded(World300, "Level300script");

        Assert.Equal(448, levelScript.Instructions.Count);
        Assert.Equal(197, levelScript.Symbols.Count);
        Assert.Equal(27, levelScript.BuiltInEventInstructionPointers[0]);
        Assert.True(levelScript.BuiltInEventInstructionPointers
            .Skip(1).All(pointer => pointer == -1));
        Assert.True(levelScript.RunInitializer);
        Assert.Equal(10, levelScript.NamedEventInstructionPointers.Count);
        Assert.Equal(44, levelScript.NamedEventInstructionPointers["game playing"]);
        Assert.Equal(164, levelScript.NamedEventInstructionPointers["won"]);
        Assert.Equal(
            442,
            levelScript.NamedEventInstructionPointers["Tank Factory Down"]);
    }

    [Fact]
    public void PayloadCensus_PinsWhereTheEarlierHeaderPatternBends()
    {
        RetailWorldPayloadCensus census =
            RetailWorldCatalog.FindPayloadCensus(World300)
            ?? throw new InvalidOperationException("The world-300 census is missing.");

        Assert.Equal(1_927_844, census.ArchiveBytes);
        Assert.Equal(
            "7293BCBE3CBB6C88B2E19A287CB53DE132A8BCADD6C2736B0DA7483726C9EFE4",
            census.ArchiveSha256);
        Assert.Equal(78_848, census.LevelWorldBytes);
        Assert.Equal(50, census.LevelHeader.Version);
        Assert.Equal(new[] { 3, 47, 300 }, census.LevelHeader.HeaderWords.ToArray());
        Assert.Equal(
            new[] { "Standard", "Laser", "Blaster" },
            census.LevelHeader.Names.ToArray());
        Assert.Equal(
            new[] { 1, 1, 1, 0, 3 },
            census.LevelHeader.TrailingWords.ToArray());
        Assert.Equal(8, census.ScriptObjectCount);
        Assert.Equal(10, census.ActorHeaderA);
        Assert.Equal(0, census.ActorHeaderB);
        Assert.Equal(36, census.InitialActorCount);
        Assert.Equal("ERES", census.HeightfieldOwnerTag);
        Assert.Equal(668_660, census.HeightfieldEnvelopeBytes);
        Assert.Equal(
            "68A181F9EC3099A0BE52BF4A063350A35E43C20664F2E07572BDB44D472ACB1A",
            census.HeightfieldEnvelopeSha256);
        Assert.Equal("WRES", census.BaseWorldOwnerTag);
        Assert.Equal(77_113, census.BaseWorldPayloadBytes);
        Assert.Equal(
            "3C153D55605B9E82A8827640D8E11084514F949E043200E9141B2185256C0DFC",
            census.BaseWorldPayloadSha256);
    }

    [Fact]
    public void World300Heightfield_IsItsOwnHashPinnedEnvelope()
    {
        System.Reflection.Assembly assembly = typeof(Level100Terrain).Assembly;
        using System.IO.Stream stream = assembly.GetManifestResourceStream(
            "OnslaughtRebuild.Core.Assets.Level300.level300-heightfield.hfld.bin")
            ?? throw new InvalidOperationException(
                "The world-300 heightfield resource is missing.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        Assert.Equal(
            "68a181f9ec3099a0be52bf4a063350a35e43c20664f2e07572bdb44d472acb1a",
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(memory.ToArray()))
                .ToLowerInvariant());
    }

    [Fact]
    public void World300AndEarlierTerrains_AreDistinctMeasuredWorlds()
    {
        Assert.NotNull(Level100Terrain.World300);
        Assert.NotSame(Level100Terrain.Instance, Level100Terrain.World300);
        Assert.NotSame(Level100Terrain.World110, Level100Terrain.World300);
        Assert.NotSame(Level100Terrain.World200, Level100Terrain.World300);
        Assert.Equal(
            Level100Terrain.World300SourceSha256,
            Level100Terrain.World300.PayloadSha256);
        Assert.NotEqual(
            Level100Terrain.World200SourceSha256,
            Level100Terrain.World300SourceSha256);
    }

    [Fact]
    public void UnmeasuredWorlds_AndWrongMainScriptNamesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(311, "LevelScript"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(300, "LevelScript"));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Level100MissionProgram.LoadEmbedded(300, "Nonexistent"));
    }
}
