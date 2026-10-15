// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;
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

    /// <summary>With ONSLAUGHT_RETAIL_ORACLE_OURS set, our outputs for a group, line for line, for diagnosis.</summary>
    public static void WriteOurs(string group, string[] lines)
    {
        string? dir = Environment.GetEnvironmentVariable("ONSLAUGHT_RETAIL_ORACLE_OURS");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllLines(Path.Combine(dir, $"{group}.txt"), lines);
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
        var meshes = new StringBuilder();
        foreach ((string name, MeshData mesh) in resources.Meshes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            // meshes.txt: per mesh its parts' type, number, referenced part, bucket (in dump order) and bounding box.
            meshes.Append($"mesh {mesh.NumParts:x}");
            int bucket = n;
            foreach (MeshPart part in mesh.Parts)
            {
                BoundingBox? box = part.BoundingBox;
                meshes.Append($" {part.PartType:x} {part.PartNumber:x} {part.ReferenceTo?.Index ?? -1:x} ")
                    .Append($"{(part.PolyBucket is null ? -1 : bucket++):x} {(box is null ? 0 : 1):x} ")
                    .Append(RetailOracle.Hv(box?.Origin ?? default)).Append(' ').Append(RetailOracle.Hv(box?.Axes ?? default));
            }

            meshes.AppendLine();
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

        File.WriteAllText(Path.Combine(dir, "meshes.txt"), meshes.ToString());
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

    /// <summary>
    /// CMeshCollisionVolume's tests against retail's own: ResolveContact, and the swept sphere against a part's box and
    /// against its triangles (level 100's poly buckets, each bucket's search state carried from case to case).
    /// </summary>
    [RetailOracleFact("volume", needsGameData: true)]
    public void MeshCollisionVolumeTestsMatchTheRetailFunctions()
    {
        List<MeshPolyBucket> buckets = Level100Buckets();
        MeshPart marker = LevelResourcesFirstPart();
        var statics = new PolyBucketStatics();
        var mismatches = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var shown = new StringBuilder();
        string dir = RetailOracle.Dir("volume")!;
        string[] cases = File.ReadAllLines(Path.Combine(dir, "cases.txt"));
        string[] retail = File.ReadAllLines(Path.Combine(dir, "retail.txt"));
        Assert.Equal(cases.Length, retail.Length);
        var ourLines = (string[])retail.Clone();
        for (int i = 0; i < cases.Length; i++)
        {
            string[] f = cases[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f[0] == "pbload")
            {
                continue;
            }

            uint[] w = f.Skip(1).Select(x => uint.Parse(x, NumberStyles.HexNumber)).ToArray();
            string ours;
            switch (f[0])
            {
                case "resolve":
                {
                    var r = new CollisionReport
                    {
                        Stuck = w[0] != 0,
                        NumContacts = (int)w[1],
                        ContactPos = RetailOracle.V(w, 3),
                        TestTriangles = (int)w[6],
                        TriangleNormal = RetailOracle.V(w, 7),
                    };
                    r.ContactDistances[r.NumContacts] = RetailOracle.F(w[2]);
                    for (int k = 0; k < 6; k++)
                    {
                        r.Normals[k] = RetailOracle.V(w, 10 + (3 * k));
                    }

                    bool ok = MeshCollisionVolume.ResolveContact(r, RetailOracle.V(w, 28), RetailOracle.V(w, 31), 1.0f,
                        out FVector start, out FVector move);
                    ours = $"resolve {(ok ? 1 : 0):x8} {RetailOracle.Hv(start)} {RetailOracle.Hv(move)} {RetailOracle.Hv(r.Normals[r.NumContacts])}";
                    break;
                }

                case "sbox":
                {
                    CollisionReport r = Report(w, 13);
                    var box = new BoundingBox(RetailOracle.V(w, 7), RetailOracle.V(w, 10), 1, 0);
                    bool hit = MeshCollisionVolume.TestSweptSphereAgainstBox(marker, box, RetailOracle.V(w, 0), RetailOracle.V(w, 3),
                        RetailOracle.F(w[6]), r);
                    ours = $"sbox {(hit ? 1 : 0):x8} {(r.Part == marker ? 1 : 0):x8} {Out(r)}";
                    break;
                }

                default:
                {
                    CollisionReport r = Report(w, 8);
                    MeshPolyBucket b = buckets[(int)w[0]];
                    bool hit = MeshCollisionVolume.TestSweptSphereAgainstPart(statics, marker, b, RetailOracle.V(w, 1),
                        RetailOracle.V(w, 4), RetailOracle.F(w[7]), r);
                    ours = $"spart {(hit ? 1 : 0):x8} {(r.Part == marker ? 1 : 0):x8} {Out(r)} | {b.SearchStamp:x8}";
                    break;
                }
            }

            ourLines[i] = ours;
            counts[f[0]] = counts.GetValueOrDefault(f[0]) + 1;
            if (ours != retail[i])
            {
                mismatches[f[0]] = mismatches.GetValueOrDefault(f[0]) + 1;
                if (mismatches[f[0]] <= 4)
                {
                    shown.AppendLine($"case {i}: {cases[i]}\nretail {retail[i]}\nours   {ours}");
                }
            }
        }

        foreach ((string function, int n) in counts.OrderBy(kv => kv.Key))
        {
            output.WriteLine($"{function}: {n} cases, {mismatches.GetValueOrDefault(function)} differ");
        }

        output.WriteLine(shown.ToString());
        RetailOracle.WriteOurs("volume", ourLines);
        Assert.Empty(mismatches);
    }

    private static MeshPart LevelResourcesFirstPart()
    {
        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, GameData.Read);
        return resources.Meshes.OrderBy(kv => kv.Key, StringComparer.Ordinal).First().Value.Parts[0];
    }

    private sealed class OracleSubject : IRenderSubject
    {
        public int RealAnimIndex => -1;

        public float RenderFrame => 0;

        public FVector RenderPos => default;

        public FMatrix RenderOrientation => FMatrix.Identity;

        public bool HasMotionController => false;

        public bool RenderUseHierarchy(int partNumber) => true;
    }

    private sealed class OracleSource(MeshData mesh, PolyBucketStatics statics) : IMeshCollisionSource
    {
        public MeshData? Mesh { get; } = mesh;

        public MeshPose? Pose => null;

        public IRenderSubject Subject { get; } = new OracleSubject();

        public float Time => 0;

        public int RenderFrameNumber => 2;

        public PolyBucketStatics PolyBuckets { get; } = statics;
    }

    private static string MoveReport(bool hit, CollisionMove mv, CollisionReport r)
    {
        var sb = new StringBuilder();
        sb.Append($" {(hit ? 1 : 0):x8} {RetailOracle.Hv(mv.Move)} {RetailOracle.Hv(mv.Pos)} {(mv.Stopped ? 1 : 0):x8} {r.NumContacts:x8} ")
            .Append($"{(r.UnknownA8 ? 1 : 0):x8} {(r.Stuck ? 1 : 0):x8} {(r.Part is null ? -1 : r.Part.PartNumber):x8}");
        for (int k = 0; k < 6; k++)
        {
            sb.Append($" {r.ContactParts[k]:x8} {RetailOracle.H(r.ContactDistances[k])}");
        }

        sb.Append($" {RetailOracle.Hv(r.ContactPos)} {RetailOracle.Hv(r.TriangleNormal)} {RetailOracle.H(r.Distance)}");
        for (int k = 0; k < 6; k++)
        {
            sb.Append(' ').Append(RetailOracle.Hv(r.Normals[k]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// CMeshCollisionVolume::CollideWithLine and CollideWithSphere against retail's own, on level 100's meshes with given
    /// part transforms (fixed, so neither side recaches): the move and the whole report after each.
    /// </summary>
    [RetailOracleFact("collide", needsGameData: true)]
    public void MeshCollisionVolumeCollisionsMatchTheRetailFunctions()
    {
        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, GameData.Read);
        List<MeshData> meshes = resources.Meshes.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value).ToList();
        var statics = new PolyBucketStatics();
        var mismatches = new Dictionary<string, int>();
        var counts = new Dictionary<string, int>();
        var shown = new StringBuilder();
        string dir = RetailOracle.Dir("collide")!;
        string[] cases = File.ReadAllLines(Path.Combine(dir, "cases.txt"));
        string[] retail = File.ReadAllLines(Path.Combine(dir, "retail.txt"));
        Assert.Equal(cases.Length, retail.Length);
        var ourLines = (string[])retail.Clone();
        for (int i = 0; i < cases.Length; i++)
        {
            string[] f = cases[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (f[0] is "pbload" or "meshes")
            {
                continue;
            }

            uint[] w = f.Skip(1).Select(x => uint.Parse(x, NumberStyles.HexNumber)).ToArray();
            MeshData mesh = meshes[(int)w[0]];
            int at = 1;
            var transforms = new MeshCollisionVolume.PartTransform[mesh.NumParts];
            for (int p = 0; p < mesh.NumParts; p++, at += 12)
            {
                var ori = new FMatrix(RetailOracle.V(w, at), RetailOracle.V(w, at + 3), RetailOracle.V(w, at + 6));
                transforms[p] = new MeshCollisionVolume.PartTransform
                {
                    Orientation = ori,
                    Inverse = ori.Transpose(),
                    Pos = RetailOracle.V(w, at + 9),
                    Frame = 1e30f,
                };
            }

            var volume = new MeshCollisionVolume(new OracleSource(mesh, statics), useFixedTransforms: true, ignoreAnimation: false);
            volume.SetTransforms(transforms);
            var mv = default(CollisionMove);
            bool hit;
            CollisionReport r;
            if (f[0] == "cline")
            {
                mv.Pos = RetailOracle.V(w, at);
                mv.Move = RetailOracle.V(w, at + 3);
                r = Report(w, at + 12);
                hit = volume.CollideWithLine(ref mv, RetailOracle.V(w, at + 6), RetailOracle.V(w, at + 9), r);
            }
            else
            {
                mv.Move = RetailOracle.V(w, at);
                mv.Pos = RetailOracle.V(w, at + 3);
                volume.Unknown18 = (int)w[at + 7];
                r = Report(w, at + 8);
                hit = volume.CollideWithSphere(ref mv, RetailOracle.F(w[at + 6]), r);
            }

            string ours = f[0] + MoveReport(hit, mv, r);
            ourLines[i] = ours;
            counts[f[0]] = counts.GetValueOrDefault(f[0]) + 1;
            if (ours != retail[i])
            {
                mismatches[f[0]] = mismatches.GetValueOrDefault(f[0]) + 1;
                if (mismatches[f[0]] <= 3)
                {
                    shown.AppendLine($"case {i}\nretail {retail[i]}\nours   {ours}");
                }
            }
        }

        foreach ((string function, int n) in counts.OrderBy(kv => kv.Key))
        {
            output.WriteLine($"{function}: {n} cases, {mismatches.GetValueOrDefault(function)} differ");
        }

        output.WriteLine(shown.ToString());
        RetailOracle.WriteOurs("collide", ourLines);
        Assert.Empty(mismatches);
    }

    /// <summary>
    /// X87.Asin and X87.Acos against VC6's runtime (__CIasin, __CIacos) at 0x007F: as stored to a float and as occupancy's
    /// slope uses asin (one 24-bit rounding of it plus pi/2), every result must match. The double result itself may
    /// differ in its last bit where fpatan's extended result rounds differently from Math.Atan2; those are counted.
    /// </summary>
    [RetailOracleFact("asin")]
    public void AsinAndAcosMatchTheRetailRuntime()
    {
        const float HalfPi = 1.57079637f;
        int lastBit = 0;
        var mismatches = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("asin"))
        {
            double x = BitConverter.UInt64BitsToDouble(w[0] | ((ulong)w[1] << 32));
            string[] e = expected.Split(' ');
            double retail = BitConverter.UInt64BitsToDouble(uint.Parse(e[1], NumberStyles.HexNumber)
                | ((ulong)uint.Parse(e[2], NumberStyles.HexNumber) << 32));
            double ours = function == "asin" ? X87.Asin(x) : X87.Acos(x);
            if (BitConverter.DoubleToInt64Bits(ours) == BitConverter.DoubleToInt64Bits(retail))
            {
                continue;
            }

            long ulps = Math.Abs(BitConverter.DoubleToInt64Bits(ours) - BitConverter.DoubleToInt64Bits(retail));
            bool sameFloat = (float)ours == (float)retail;
            bool sameSlope = X87.Add(ours, HalfPi) == X87.Add(retail, HalfPi);
            if (ulps == 1 && sameFloat && sameSlope)
            {
                lastBit++;
            }
            else
            {
                mismatches.Add($"{function}({x:R}): retail {retail:R}, ours {ours:R} ({ulps} ulps)");
            }
        }

        output.WriteLine($"{lastBit} results differ in the last bit of the double only");
        foreach (string m in mismatches.Take(10))
        {
            output.WriteLine(m);
        }

        Assert.Empty(mismatches);
    }
}
