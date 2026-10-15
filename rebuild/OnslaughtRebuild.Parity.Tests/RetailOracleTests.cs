// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test against the retail oracle's stored results: the retail functions themselves run natively on generated cases at
/// control word 0x007F (rebuild/tools/retail-oracle; its outputs are retail-derived, so they live in an ignored data
/// directory, ONSLAUGHT_RETAIL_ORACLE). Skipped, visibly, where they are missing.
/// </summary>
public sealed class RetailOracleFactAttribute : FactAttribute
{
    public RetailOracleFactAttribute(string group, bool needsGameData = false)
    {
        if (RetailOracle.Dir(group) is null)
        {
            Skip = $"retail oracle results for '{group}' not found (set ONSLAUGHT_RETAIL_ORACLE)";
        }
        else if (needsGameData && GameData.Root is null)
        {
            Skip = "game installation not found (set ONSLAUGHT_GAME_DIR)";
        }
    }
}

internal static class RetailOracle
{
    public static string? Dir(string group)
    {
        string? root = Environment.GetEnvironmentVariable("ONSLAUGHT_RETAIL_ORACLE");
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        string dir = Path.Combine(root, group);
        return File.Exists(Path.Combine(dir, "cases.txt")) && File.Exists(Path.Combine(dir, "retail.txt")) ? dir : null;
    }

    public static IEnumerable<(string Function, uint[] Words, string Expected)> Cases(string group)
    {
        string dir = Dir(group)!;
        string[] cases = File.ReadAllLines(Path.Combine(dir, "cases.txt"));
        string[] retail = File.ReadAllLines(Path.Combine(dir, "retail.txt"));
        Assert.Equal(cases.Length, retail.Length);
        for (int i = 0; i < cases.Length; i++)
        {
            string[] f = cases[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            uint[] words = f.Skip(1).Select(w => uint.Parse(w, NumberStyles.HexNumber)).ToArray();
            yield return (f[0], words, retail[i]);
        }
    }

    public static float F(uint w) => BitConverter.UInt32BitsToSingle(w);

    public static string H(float f) => BitConverter.SingleToUInt32Bits(f).ToString("x8");

    public static FVector V(uint[] w, int at) => new(F(w[at]), F(w[at + 1]), F(w[at + 2]));

    public static string Hv(FVector v) => $"{H(v.X)} {H(v.Y)} {H(v.Z)}";
}

public sealed class RetailOracleTests(ITestOutputHelper output)
{
    private static CollisionReport Report(uint[] w, int at) => new()
    {
        UnknownA8 = w[at] != 0,
        Distance = RetailOracle.F(w[at + 1]),
        TestTriangles = (int)w[at + 2],
        UnknownD0 = (int)w[at + 3],
    };

    private static string Out(CollisionReport r) =>
        $"{(r.UnknownA8 ? 1 : 0):x8} {(r.Unknown94 ? 1 : 0):x8} {RetailOracle.H(r.Distance)} {(r.Stuck ? 1 : 0):x8} "
        + $"{RetailOracle.Hv(r.ContactPos)} {RetailOracle.Hv(r.TriangleNormal)}";

    private static string Run(string function, uint[] w)
    {
        static FVector V(uint[] w, int at) => RetailOracle.V(w, at);
        switch (function)
        {
            case "TestSegmentAgainstTriangle":
            {
                CollisionReport r = Report(w, 15);
                bool hit = Geometry.TestSegmentAgainstTriangle(V(w, 0), V(w, 3), V(w, 6), V(w, 9), V(w, 12), r);
                return $"{function} {(hit ? 1 : 0):x8} {Out(r)}";
            }

            case "TestSweptSphereAgainstTriangle":
            {
                CollisionReport r = Report(w, 16);
                bool hit = Geometry.TestSweptSphereAgainstTriangle(V(w, 0), V(w, 3), V(w, 6), V(w, 9), V(w, 12),
                    RetailOracle.F(w[15]), r);
                return $"{function} {(hit ? 1 : 0):x8} {Out(r)}";
            }

            case "PointInsideTrianglePrism":
                return $"{function} {(Geometry.PointInsideTrianglePrism(V(w, 0), V(w, 3), V(w, 6), V(w, 9), V(w, 12)) ? 1 : 0):x8}";
            case "SelectClosestPointOnTriangleEdges":
                return $"{function} {RetailOracle.Hv(Geometry.SelectClosestPointOnTriangleEdges(V(w, 0), V(w, 3), V(w, 6), V(w, 9)))}";
            case "RaySphereEntryDistance":
                return $"{function} {RetailOracle.H(Geometry.RaySphereEntryDistance(V(w, 0), V(w, 3), RetailOracle.F(w[6])))}";
            case "DistanceOutsideBox":
                return $"{function} {RetailOracle.H(Geometry.DistanceOutsideBox(V(w, 0), V(w, 3)))}";
            case "ClipSegmentAgainstAABB3D":
            {
                FVector s = V(w, 0);
                FVector e = V(w, 3);
                float[] bounds = Enumerable.Range(6, 6).Select(i => RetailOracle.F(w[i])).ToArray();
                bool ok = Geometry.ClipSegmentAgainstAABB3D(ref s, ref e, bounds);
                return $"{function} {(ok ? 1 : 0):x8} {RetailOracle.Hv(s)} {RetailOracle.Hv(e)}";
            }

            default:
                throw new InvalidOperationException(function);
        }
    }

    /// <summary>Geometry.cpp's tests, case by case against the retail functions run at 0x007F.</summary>
    [RetailOracleFact("geometry")]
    public void GeometryMatchesTheRetailFunctions()
    {
        var mismatches = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var shown = new StringBuilder();
        foreach ((string function, uint[] words, string expected) in RetailOracle.Cases("geometry"))
        {
            counts[function] = counts.GetValueOrDefault(function) + 1;
            string ours = Run(function, words);
            if (ours != expected)
            {
                mismatches[function] = mismatches.GetValueOrDefault(function) + 1;
                if (mismatches[function] <= 3)
                {
                    shown.AppendLine($"retail {expected}\nours   {ours}");
                }
            }
        }

        foreach ((string function, int n) in counts.OrderBy(kv => kv.Key))
        {
            output.WriteLine($"{function}: {n} cases, {mismatches.GetValueOrDefault(function)} differ");
        }

        output.WriteLine(shown.ToString());
        Assert.Empty(mismatches);
    }

    /// <summary>
    /// Writes level 100's poly buckets for the oracle (ONSLAUGHT_RETAIL_ORACLE_DUMP set): per bucket its stored record,
    /// cell lists, triangles and vertices. A tool step, skipped unless asked for.
    /// </summary>
    [GameDataFact]
    public void DumpLevel100PolyBucketsForTheOracle()
    {
        string? dir = Environment.GetEnvironmentVariable("ONSLAUGHT_RETAIL_ORACLE_DUMP");
        if (string.IsNullOrEmpty(dir))
        {
            output.WriteLine("ONSLAUGHT_RETAIL_ORACLE_DUMP not set: nothing written");
            return;
        }

        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, GameData.Read);
        Directory.CreateDirectory(dir);
        int n = 0;
        foreach ((string name, MeshData mesh) in resources.Meshes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            foreach (MeshPart part in mesh.Parts)
            {
                if (part.PolyBucket is not { } b)
                {
                    continue;
                }

                using var w = new BinaryWriter(File.Create(Path.Combine(dir, $"bucket{n:000}.bin")));
                w.Write(b.Record.Length);
                w.Write(b.Record);
                w.Write(b.Cells.Length);
                foreach (short[]? cell in b.Cells)
                {
                    w.Write(cell is null ? -1 : cell.Length);
                    foreach (short t in cell ?? [])
                    {
                        w.Write(t);
                    }
                }

                w.Write(b.Triangles.Length);
                foreach ((uint mask, ushort v0, ushort v1, ushort v2, ushort stamp) in b.Triangles)
                {
                    w.Write(mask);
                    w.Write(v0);
                    w.Write(v1);
                    w.Write(v2);
                    w.Write(stamp);
                }

                w.Write(b.Vertices.Length);
                foreach ((short x, short y, short z) in b.Vertices)
                {
                    w.Write(x);
                    w.Write(y);
                    w.Write(z);
                }

                output.WriteLine($"bucket{n:000}: {name} part {part.Index} {b.Width}x{b.Height}, {b.Triangles.Length} triangles");
                n++;
            }
        }
    }

    private static List<MeshPolyBucket> Level100Buckets()
    {
        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, GameData.Read);
        return resources.Meshes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .SelectMany(kv => kv.Value.Parts).Select(p => p.PolyBucket).OfType<MeshPolyBucket>().ToList();
    }

    /// <summary>
    /// The poly bucket searches, against retail's own on level 100's buckets: the triangles each line walk and sphere box
    /// hands out, in order, and the search stamp after (each bucket's state carries from case to case, as in retail).
    /// </summary>
    [RetailOracleFact("polybucket", needsGameData: true)]
    public void PolyBucketSearchesMatchTheRetailFunctions()
    {
        List<MeshPolyBucket> buckets = Level100Buckets();
        var statics = new PolyBucketStatics();
        var mismatches = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var shown = new StringBuilder();
        string dir = RetailOracle.Dir("polybucket")!;
        string[] cases = File.ReadAllLines(Path.Combine(dir, "cases.txt"));
        string[] retail = File.ReadAllLines(Path.Combine(dir, "retail.txt"));
        Assert.Equal(cases.Length, retail.Length);
        int loaded = 0;
        for (int i = 0; i < cases.Length; i++)
        {
            string[] f = cases[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f[0] == "pbload")
            {
                loaded++;
                continue;
            }

            uint[] w = f.Skip(1).Select(x => uint.Parse(x, NumberStyles.HexNumber)).ToArray();
            MeshPolyBucket b = buckets[(int)w[0]];
            var sb = new StringBuilder(f[0]);
            int count = 0;
            if (f[0] == "pbline")
            {
                bool stop = w[7] != 0;
                for (int t = b.StartLineSearch(statics, RetailOracle.V(w, 1), RetailOracle.V(w, 4)); t != -1 && count < 4000;
                     t = b.GetNextPolyLine(statics, stop), count++)
                {
                    sb.Append(' ').Append(t);
                }
            }
            else
            {
                for (int t = b.StartSearch(statics, RetailOracle.V(w, 1), RetailOracle.F(w[4])); t != -1 && count < 4000;
                     t = b.GetNextPoly(statics), count++)
                {
                    sb.Append(' ').Append(t);
                }
            }

            sb.Append($" | {b.SearchStamp:x8}");
            counts[f[0]] = counts.GetValueOrDefault(f[0]) + 1;
            if (sb.ToString() != retail[i])
            {
                mismatches[f[0]] = mismatches.GetValueOrDefault(f[0]) + 1;
                if (mismatches[f[0]] <= 3)
                {
                    shown.AppendLine($"case {i}: {cases[i]}\nretail {retail[i]}\nours   {sb}");
                }
            }
        }

        Assert.Equal(buckets.Count, loaded);
        foreach ((string function, int n) in counts.OrderBy(kv => kv.Key))
        {
            output.WriteLine($"{function}: {n} cases, {mismatches.GetValueOrDefault(function)} differ");
        }

        output.WriteLine(shown.ToString());
        Assert.Empty(mismatches);
    }
}
