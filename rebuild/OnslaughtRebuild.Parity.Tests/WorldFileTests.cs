// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.World;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class WorldFileTests(ITestOutputHelper output)
{
    internal static (WorldFile Base, WorldFile World, uint BaseEnd, uint ChunkEnd) Level100Worlds()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        var c = new ChunkReader(MemBuffer.FromFile(name, bytes));
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("WRES"))
            {
                (int baseLevel, int worldLevel, uint basePosition, uint worldPosition) = WorldFile.ReadResourcePositions(c);
                Assert.Equal(1, baseLevel);
                Assert.Equal(100, worldLevel);
                uint chunkEnd = c.WhereAmI;

                // CWorld::Load opens the resource file again and skips to the world's position; the base world's chunk
                // ends where the real world's chunk header begins.
                MemBuffer baseFile = MemBuffer.FromFile(name, bytes);
                baseFile.Skip((int)basePosition);
                MemBuffer world = MemBuffer.FromFile(name, bytes);
                world.Skip((int)worldPosition);
                return (WorldFile.Read(baseFile), WorldFile.Read(world), worldPosition - 8, chunkEnd);
            }

            c.Skip();
        }

        throw new InvalidDataException("no WRES chunk");
    }

    private void Describe(string title, WorldFile w)
    {
        output.WriteLine($"== {title}: version {w.Version}, header {w.HeaderVersion}, type {w.WorldType}, base {w.BaseLevel}, map {w.Map}");
        output.WriteLine($"BE configurations: {string.Join(", ", w.BattleEngineConfigurations.Select(Encoding.ASCII.GetString))}");
        output.WriteLine($"scripts ({w.Scripts.Count}): {string.Join(", ", w.Scripts.Select(s => $"{Encoding.ASCII.GetString(s.Name)}[{s.Code.Instructions.Count}]"))}");
        output.WriteLine($"named meshes: {w.NamedMeshes.Count}");
        output.WriteLine($"things: {w.Things.Count}");
        for (int i = 0; i < w.Things.Count; i++)
        {
            ThingRecord t = w.Things[i];
            output.WriteLine($"  [{i}] oid {t.Type} '{t.PhysicsNameText}' name '{Encoding.ASCII.GetString(t.Init.Name)}' script '{Encoding.ASCII.GetString(t.Init.Script)}' dies {t.DiesOn} target {t.Init.Target} active {t.Init.Active}");
        }

        output.WriteLine($"tree groups {w.TreeGroups.Count} ({w.TreeGroups.Sum(g => g.Count)} trees), tree types {w.TreeTypes.Count} ({w.TreeTypes.Sum(t => t.Trees.Count)} trees)");
        foreach (TreeType t in w.TreeTypes)
        {
            output.WriteLine($"  tree type '{Encoding.ASCII.GetString(t.Name)}': {t.Trees.Count}, variants {string.Join(",", t.Trees.Select(x => x.Variant).Distinct().Order())}");
        }

        output.WriteLine($"influence map v{w.InfluenceMap.Version}: {w.InfluenceMap.Nodes.Count} nodes, {w.InfluenceMap.Links.Count} links");
        output.WriteLine($"waypoint paths {w.WaypointPaths.Count}: {string.Join(", ", w.WaypointPaths.Select(p => $"{Encoding.ASCII.GetString(p.Name)}({p.ThingIndices.Count})"))}");
        output.WriteLine($"timings {w.Timings}");
        output.WriteLine($"occupancy grids {string.Join(", ", w.OccupancyGrids.Select(g => $"v{g.Version}/{g.Data.Length}"))}, start time {w.StartTime}, end {w.EndPosition}");
    }

    [GameDataFact]
    public void Level100sBaseAndRealWorldsReadToTheEndOfTheirChunks()
    {
        (WorldFile b, WorldFile w, uint baseEnd, uint chunkEnd) = Level100Worlds();
        Describe("base world 1", b);
        Describe("world 100", w);

        Assert.Equal(baseEnd, b.EndPosition);
        Assert.Equal(chunkEnd, w.EndPosition);
        Assert.Equal(-1, b.BaseLevel);
        Assert.Equal(1, w.BaseLevel);
        Assert.Contains(w.Things, t => t.Type == Oid.Start);
    }
}
