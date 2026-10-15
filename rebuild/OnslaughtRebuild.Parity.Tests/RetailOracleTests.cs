// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test against the retail oracle's stored results: the retail functions themselves run natively on generated cases at
/// control word 0x007F (rebuild/tools/retail-oracle; its outputs are retail-derived, so they live in an ignored data
/// directory, ONSLAUGHT_RETAIL_ORACLE). Skipped, visibly, where they are missing.
/// </summary>
public sealed class RetailOracleFactAttribute : FactAttribute
{
    public RetailOracleFactAttribute(string group)
    {
        if (RetailOracle.Dir(group) is null)
        {
            Skip = $"retail oracle results for '{group}' not found (set ONSLAUGHT_RETAIL_ORACLE)";
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
}
