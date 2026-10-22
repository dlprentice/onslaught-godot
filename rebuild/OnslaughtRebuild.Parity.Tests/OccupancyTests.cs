// SPDX-License-Identifier: GPL-3.0-only

using Xunit.Abstractions;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>COccupancyGrid's cells, line test, path search and loads; COccupancy's statics.</summary>
public sealed class OccupancyTests(ITestOutputHelper output)
{
    private static OccupancyState NewState()
    {
        var pool = new SPtrSetPool();
        pool.Init(1000);
        var s = new OccupancyState(pool);
        s.InitOccupancy();
        return s;
    }

    /// <summary>Closes grid cells (grid coordinates) by world coordinates.</summary>
    private static void Close(OccupancyGrid g, int gx, int gy) => g.SetCell(gx * 2, gy * 2, false);

    [Fact]
    public void ANewGridIsOpenAndCellsAreTwoWorldUnits()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[0]!;
        Assert.All(g.Data, b => Assert.Equal(0xff, b));
        Assert.Equal(35.0f * (3.14159265358979f / 180.0f), g.MaxSlope);
        g.SetCell(21, 41, false); // world (21, 41) is cell (10, 20)
        Assert.Equal(0, g.TestCell(10, 20));
        Assert.False(g.UnknownCell(new FVector(20.4f, 40.0f, 0)));
        Assert.True(g.UnknownCell(new FVector(22.0f, 40.0f, 0)));
        // (SINT) rounds to nearest (/QIfist): 21.5 rounds to 22, cell 11.
        Assert.True(g.UnknownCell(new FVector(21.5f, 40.0f, 0)));
        g.ClearCross(100, 100);
        Assert.Equal(0, g.TestCell(50, 50));
        Assert.Equal(0, g.TestCell(49, 50)); // x - 1 = 99 -> cell 49
        Assert.NotEqual(0, g.TestCell(51, 50)); // x + 1 = 101 -> cell 50 again
    }

    [Fact]
    public void IsBlockedRoundsTheMinorAxisToNearest()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[0]!;
        Assert.True(g.IsBlocked(-1, 0, 5, 5));
        Assert.False(g.IsBlocked(0, 0, 10, 3));
        // Along (0,0)-(10,3) the y steps 0.3 a cell: at x = 5 it is 1.5, which rounds to 2 (truncation would give 1).
        Close(g, 5, 2);
        Assert.True(g.IsBlocked(0, 0, 10, 3));
        OccupancyGrid h = s.Grids[1]!;
        Close(h, 5, 1);
        Assert.False(h.IsBlocked(0, 0, 10, 3));
    }

    [Fact]
    public void AnUnblockedPathIsTheEndCellAlone()
    {
        OccupancyState s = NewState();
        var path = new SquadPath();
        s.Grids[0]!.Unknown4bc2e0(new FVector(20, 20, 0), new FVector(200.4f, 81, 0), 0, path);
        Assert.Equal(1, path.Unknown08);
        Assert.Equal(1, path.Unknown0C);
        Assert.Equal((100, 40), (path.Unknown10[0], path.Unknown18[0]));
        Assert.Equal((200, 81), (path.Unknown00, path.Unknown04));
    }

    [Fact]
    public void APathAroundAWallGoesThroughItsGap()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[0]!;
        for (int y = 0; y < 256; y++)
        {
            if (y != 80)
            {
                Close(g, 50, y);
            }
        }

        var path = new SquadPath();
        g.Unknown4bc2e0(new FVector(60, 120, 0), new FVector(200, 120, 0), 0, path);
        Assert.Equal(1, path.Unknown08);
        Assert.Equal((100, 60), (path.Unknown10[0], path.Unknown18[0])); // the walk starts at the end cell
        var cells = Enumerable.Range(0, path.Unknown0C).Select(i => (X: (int)path.Unknown10[i], Y: (int)path.Unknown18[i])).ToList();
        Assert.All(cells, c => Assert.NotEqual(0, g.TestCell(c.X, c.Y)));
        Assert.Contains(cells, c => Math.Abs(c.X - 50) <= 1 && Math.Abs(c.Y - 80) <= 1);
        // The flood numbered the cells it entered; the next search clears only that box.
        Assert.Contains(s.Cost, c => c != 0xffff);
        var straight = new SquadPath();
        g.Unknown4bc2e0(new FVector(10, 10, 0), new FVector(20, 10, 0), 0, straight);
        Assert.All(s.Cost, c => Assert.Equal(0xffff, c));
    }

    [Fact]
    public void AnEnclosedEndMovesToTheNearestCellTheFloodReached()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[0]!;
        // A closed ring round cell (100, 100): the end is open but cannot be reached.
        for (int d = -2; d <= 2; d++)
        {
            Close(g, 98, 100 + d);
            Close(g, 102, 100 + d);
            Close(g, 100 + d, 98);
            Close(g, 100 + d, 102);
        }

        var path = new SquadPath();
        g.Unknown4bc2e0(new FVector(180, 200, 0), new FVector(200, 200, 0), 0, path);
        Assert.True(path.Unknown0C > 0);
        Assert.NotEqual((100, 100), (s.EndX, s.EndY));
        Assert.NotEqual(0xffff, s.CostAt(s.EndX, s.EndY));
    }

    [Fact]
    public void TheEastRingTestReadsPastTheCostTableIntoTheStatics()
    {
        OccupancyState s = NewState();
        s.X = 0x12345678;
        s.MinX = 0x0000abcd;
        Assert.Equal(0x5678, s.CostPastEnd(256, 0));
        Assert.Equal(0x1234, s.CostPastEnd(256, 1));
        Assert.Equal(0xabcd, s.CostPastEnd(256, 2));
        Assert.Throws<NotSupportedException>(() => s.CostPastEnd(256, 6));
        Assert.Equal(s.CostAt(3, 4), s.CostPastEnd(3, 4));
    }

    [Fact]
    public void SimplifyCopiesOnlyTheLastCellDown()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[0]!;
        var path = new SquadPath();
        foreach ((int x, int y) in new[] { (10, 10), (11, 10), (12, 10), (13, 10), (14, 10), (15, 10) })
        {
            path.AddCell((x * 2) + 1, (y * 2) + 1);
        }

        // All in sight of each other: from the end, i runs to -1 and becomes 1; remove = 5 - 1 - 1 = 3, and only cell 5
        // moves (to 2). Then from the start everything is visible: i = 3, first = 1, nothing more.
        OccupancyGrid.Simplify(path, g);
        Assert.Equal(3, path.Unknown0C);
        Assert.Equal(new byte[] { 10, 11, 15 }, path.Unknown10[..3]);
    }

    [Fact]
    public void LoadReadsBothStoredVersions()
    {
        OccupancyState s = NewState();
        OccupancyGrid g = s.Grids[2]!;
        var v2 = new byte[4 + 0x2000];
        BitConverter.GetBytes(2).CopyTo(v2, 0);
        Array.Fill(v2, (byte)0xff, 4, 0x2000);
        v2[4 + (5 * 32)] = 0x0f; // stored row by row: i = 5, j = 0 is Data[0 * 256 + 5]
        g.Load(MemBuffer.FromFile("grid.bin", v2));
        Assert.Equal(0x0f, g.Data[5]);
        Assert.Equal(0xff, g.Data[6]);

        var v1 = new byte[4 + (512 * 64)];
        BitConverter.GetBytes(1).CopyTo(v1, 0);
        Array.Fill(v1, (byte)0xff, 4, 512 * 64);
        v1[4] = 0b0000_0010; // world row 0, x 0..7: x 0 clear then x 1 set -> cell 0 set (the later wins)
        v1[4 + 1] = 0b0000_0001; // x 8 set, x 9 clear -> cell 4 clear
        // World row 1 is grid row 0 too, and comes later: it decides.
        v1[4 + 64] = 0b0000_0010;
        v1[4 + 64 + 1] = 0b0000_0001;
        g.Load(MemBuffer.FromFile("grid.bin", v1));
        Assert.NotEqual(0, g.TestCell(0, 0));
        Assert.Equal(0, g.TestCell(4, 0));
        v1[4 + 64 + 1] = 0xff; // row 1 sets cell 4 again: the later row wins
        g.Load(MemBuffer.FromFile("grid.bin", v1));
        Assert.NotEqual(0, g.TestCell(4, 0));
    }

    /// <summary>
    /// The native port's occupancy data for level 100, in the folder ONSLAUGHT_PORT_OCCUPANCY names (skipped when it is
    /// unset or has no paths.txt).
    /// </summary>
    private static string? PortData()
    {
        string? dir = Environment.GetEnvironmentVariable("ONSLAUGHT_PORT_OCCUPANCY");
        return dir is not null && File.Exists(Path.Combine(dir, "paths.txt")) ? dir : null;
    }

    [GameDataFact]
    public void BaseWorld1sStoredGridsLoadAsTheNativePortHasThemBeforeBuildStatic()
    {
        string? port = PortData();
        if (port is null)
        {
            return;
        }

        // Level 100 skips its own grids (SkipLegacy) and keeps base world 1's.
        (WorldFile baseWorld, WorldFile world, _, _) = WorldFileTests.Level100Worlds();
        Assert.Equal(3, baseWorld.OccupancyGrids.Count);
        OccupancyState s = NewState();
        for (int g = 0; g < 3; g++)
        {
            (int version, byte[] data) = baseWorld.OccupancyGrids[g];
            byte[] stored = new byte[4 + data.Length];
            BitConverter.GetBytes(version).CopyTo(stored, 0);
            data.CopyTo(stored, 4);
            s.Grids[g]!.Load(MemBuffer.FromFile("grid.bin", stored));
            Assert.Equal(File.ReadAllBytes(Path.Combine(port, "build1-before", $"grid{g}.bin")), s.Grids[g]!.Data);
        }

        Assert.Equal(File.ReadAllBytes(Path.Combine(port, "build1-before", "static.bin")), s.Static.Data);
    }

    /// <summary>
    /// Level 100's load builds its grids as the native port has them when BuildStatic returns: base world 1's stored
    /// grids, then every unit's cells closed where its collision mesh meets the ground's line or unit sphere.
    /// </summary>
    [GameDataFact(Skip = "the native port's level-100 grid dumps clear meshed units' cells, which retail never does (its BuildStatic line move is NaN: B's 521-rr runs, /QIfist fistp reuse at 004bceaf/004bcff7); re-enable against the port's regenerated dumps")]
    public void Level100sGridsAfterBuildStaticAreTheNativePorts()
    {
        string? port = PortData();
        if (port is null)
        {
            return;
        }

        (ThingContext context, _) = LevelLoadTests.Level100Run();
        OccupancyState s = context.Occupancy;
        var differ = new List<string>();
        for (int g = 0; g < 3; g++)
        {
            byte[] expected = File.ReadAllBytes(Path.Combine(port, "build1-after", $"grid{g}.bin"));
            differ.AddRange(DifferingCells($"grid{g}", expected, s.Grids[g]!.Data));
        }

        differ.AddRange(DifferingCells("static", File.ReadAllBytes(Path.Combine(port, "build1-after", "static.bin")), s.Static.Data));
        output.WriteLine($"{s.Units.Size} units on the grids; {differ.Count} cells differ");
        foreach (string d in differ.Take(20))
        {
            output.WriteLine(d);
        }

        for (Thing? u = s.Units.First(); u is not null; u = s.Units.Next())
        {
            output.WriteLine($"unit {u} radius {u.GetBoundingRadius():R} centre {((IOccupancyUnit)u).GetCentrePos()} mesh volume {u.HasCollisionMeshVolume()} "
                + $"mesh {u.RenderThing?.MeshOrNull?.HeaderName}");
        }

        Assert.True(s.Enabled);
        Assert.Equal(34, s.Units.Size);
        Assert.Empty(differ);
    }

    /// <summary>The cells whose bits differ, as "grid (x, y) port/ours" (bit x &amp; 7 of byte (x &gt;&gt; 3) * 256 + y; cells are two units).</summary>
    private static IEnumerable<string> DifferingCells(string grid, byte[] expected, byte[] actual)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            int bits = expected[i] ^ actual[i];
            for (int b = 0; b < 8; b++)
            {
                if ((bits & (1 << b)) != 0)
                {
                    int x = ((i / 256) * 8) + b;
                    int y = i % 256;
                    yield return $"{grid} cell ({x}, {y}) port {(expected[i] >> b) & 1} ours {(actual[i] >> b) & 1}";
                }
            }
        }
    }

    /// <summary>
    /// Level 100 run on to Update 120: its ground vehicles' path searches (CGroundVehicleGuide::Unknown3) as the native
    /// port has them, call by call (the port's searches at Updates 99, 103, 107 and 111), with the statics on entry, the
    /// path and the cost table after.
    /// </summary>
    [GameDataFact(Skip = "the native port's level-100 grid dumps clear meshed units' cells, which retail never does (its BuildStatic line move is NaN: B's 521-rr runs, /QIfist fistp reuse at 004bceaf/004bcff7); re-enable against the port's regenerated dumps")]
    public void Level100sPathSearchesToUpdate120AreTheNativePorts()
    {
        string? port = PortData();
        if (port is null)
        {
            return;
        }

        const int Updates = 120;
        (ThingContext context, ParityGame game) = LevelLoadTests.Level100Run();
        OccupancyState s = context.Occupancy;
        var ours = new List<string>();
        int update = 0;
        s.OnSearch = (grid, start, end, a, entry, path) =>
        {
            int g = Array.IndexOf(s.Grids, grid);
            ours.Add($"U {update} grid {g} start {Bits(start.X)} {Bits(start.Y)} end {Bits(end.X)} {Bits(end.Y)} a {a} "
                + $"| {string.Join(' ', entry)} | out {path.Unknown00} {path.Unknown04} {path.Unknown08} {path.Unknown0C} "
                + $"{Convert.ToHexString(path.Unknown10[..path.Unknown0C]).ToLowerInvariant()} "
                + $"{Convert.ToHexString(path.Unknown18[..path.Unknown0C]).ToLowerInvariant()} crc32 {Crc32(s.Cost):x8}");
        };

        string stopped = $"ran all {Updates}";
        while (update < Updates)
        {
            update++;
            try
            {
                game.Update();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped in U{update}: {e.Message}";
                break;
            }
        }

        var expected = new List<string>();
        foreach (string line in File.ReadLines(Path.Combine(port, "paths.txt")).Where(l => l.StartsWith("C ", StringComparison.Ordinal)))
        {
            string[] f = line.Split(' ');
            int At(string key) => Array.IndexOf(f, key) + 1;
            int u = int.Parse(f[At("U")]);
            if (u > update)
            {
                continue;
            }

            string[] entry = new[] { "sMinX", "sMinY", "sMaxX", "sMaxY", "sEndX", "sEndY", "sX", "sY" }.Select(k => f[At(k)]).ToArray();
            int o = At("out");
            expected.Add($"U {u} grid {f[At("grid")]} start {f[At("start")]} {f[At("start") + 1]} end {f[At("end")]} {f[At("end") + 1]} "
                + $"a {f[At("a")]} | {string.Join(' ', entry)} | out {f[o]} {f[o + 1]} {f[o + 2]} {f[o + 3]} "
                + $"{f[At("cellsX")].ToLowerInvariant()} {f[At("cellsY")].ToLowerInvariant()} crc32 {f[At("crc32")].ToLowerInvariant()}");
        }

        output.WriteLine($"{stopped}; port {expected.Count} searches to U{update}, ours {ours.Count}");
        for (int i = 0; i < Math.Max(expected.Count, ours.Count); i++)
        {
            output.WriteLine($"port {(i < expected.Count ? expected[i] : "-")}");
            output.WriteLine($"ours {(i < ours.Count ? ours[i] : "-")}");
        }

        Assert.Equal(expected, ours);
    }

    private static string Bits(float f) => BitConverter.SingleToUInt32Bits(f).ToString("x8");

    [Fact]
    public void LevelOnesSixSearchesMatchTheNativePortCallByCall()
    {
        string? port = PortData();
        if (port is null)
        {
            return;
        }

        OccupancyState s = NewState();
        for (int g = 0; g < 3; g++)
        {
            File.ReadAllBytes(Path.Combine(port, "build1-after", $"grid{g}.bin")).CopyTo(s.Grids[g]!.Data, 0);
        }

        int calls = 0;
        foreach (string line in File.ReadLines(Path.Combine(port, "paths.txt")).Where(l => l.StartsWith("C ", StringComparison.Ordinal)))
        {
            string[] f = line.Split(' ');
            int At(string key) => Array.IndexOf(f, key) + 1;
            float Bits(int i) => BitConverter.UInt32BitsToSingle(Convert.ToUInt32(f[i], 16));
            int grid = int.Parse(f[At("grid")]);
            var start = new FVector(Bits(At("start")), Bits(At("start") + 1), 0);
            var end = new FVector(Bits(At("end")), Bits(At("end") + 1), 0);
            int a = int.Parse(f[At("a")]);
            int[] entry = new[] { "sMinX", "sMinY", "sMaxX", "sMaxY", "sEndX", "sEndY", "sX", "sY" }.Select(k => int.Parse(f[At(k)])).ToArray();
            Assert.Equal(entry, new[] { s.MinX, s.MinY, s.MaxX, s.MaxY, s.EndX, s.EndY, s.X, s.Y });

            var path = new SquadPath();
            s.Grids[grid]!.Unknown4bc2e0(start, end, a, path);

            int o = At("out");
            Assert.Equal((int.Parse(f[o]), int.Parse(f[o + 1]), int.Parse(f[o + 2]), int.Parse(f[o + 3])),
                (path.Unknown00, path.Unknown04, path.Unknown08, path.Unknown0C));
            Assert.Equal(Convert.FromHexString(f[At("cellsX")]), path.Unknown10[..path.Unknown0C]);
            Assert.Equal(Convert.FromHexString(f[At("cellsY")]), path.Unknown18[..path.Unknown0C]);
            Assert.Equal(Convert.ToUInt32(f[At("crc32")], 16), Crc32(s.Cost));
            calls++;
        }

        Assert.Equal(6, calls);
    }

    /// <summary>zlib's CRC-32 of the cost table as little-endian words.</summary>
    private static uint Crc32(ushort[] words)
    {
        uint crc = 0xffffffff;
        foreach (ushort w in words)
        {
            foreach (byte b in new[] { (byte)w, (byte)(w >> 8) })
            {
                crc ^= b;
                for (int k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
                }
            }
        }

        return ~crc;
    }
}
