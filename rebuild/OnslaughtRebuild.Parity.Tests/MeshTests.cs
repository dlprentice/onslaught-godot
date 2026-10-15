// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class MeshTests(ITestOutputHelper output)
{
    /// <summary>Every MESH chunk of level 100's resource file, read as CResourceAccumulator::ReadResources does, with
    /// each chunk's end position and the mesh (null when skipped).</summary>
    internal static List<(MeshData? Mesh, uint ChunkEnd, uint ReadEnd)> Level100Meshes(Func<string, bool>? doWeWantMesh = null)
    {
        (string name, byte[] bytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        var c = new ChunkReader(MemBuffer.FromFile(name, bytes));
        var meshes = new List<(MeshData?, uint, uint)>();
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("MESH"))
            {
                uint end = c.WhereAmI + c.Size;
                MeshData? mesh = MeshData.Deserialize(c, GameData.Read, doWeWantMesh);
                meshes.Add((mesh, end, c.WhereAmI));
            }
            else
            {
                c.Skip();
            }
        }

        return meshes;
    }

    private static readonly Subject Rest = new();

    private sealed class Subject : IRenderSubject
    {
        public int RealAnimIndex => -1;
        public float RenderFrame => 0;
        public FVector RenderPos => new(0, 0, 0);
        public FMatrix RenderOrientation => FMatrix.Identity;
        public bool HasMotionController => false;
        public bool RenderUseHierarchy(int partNumber) => true;
    }

    [GameDataFact]
    public void EveryLevel100MeshAndItsFileReadToTheirEnds()
    {
        List<(MeshData? Mesh, uint ChunkEnd, uint ReadEnd)> meshes = Level100Meshes(_ => true);

        Assert.Equal(54, meshes.Count);
        foreach ((MeshData? mesh, uint chunkEnd, uint readEnd) in meshes)
        {
            Assert.NotNull(mesh);
            Assert.Equal(chunkEnd, readEnd);
            if (mesh!.HeaderName.Length > 0)
            {
                Assert.Equal(0, mesh.MeshFileUnread);
            }

            output.WriteLine($"{mesh.HeaderName,-28} parts {mesh.NumParts,3} emitters {mesh.GetNumEmitters(),3} anims {mesh.NumAnimModes,2} chain {Chain(mesh)}");
        }
    }

    [GameDataFact]
    public void OptionalMeshesAreSkippedUnlessTheGameWantsThem()
    {
        List<(MeshData? Mesh, uint ChunkEnd, uint ReadEnd)> meshes = Level100Meshes();

        Assert.Single(meshes, m => m.Mesh is null);
        Assert.All(meshes, m => Assert.Equal(m.ChunkEnd, m.ReadEnd));
    }

    [GameDataFact]
    public void TheLevel100UnitMeshesHaveTheirTurretsBarrelsAndEmitters()
    {
        Dictionary<string, MeshData> byName = Level100Meshes(_ => true)
            .Select(m => m.Mesh!)
            .Where(m => m.HeaderName.Length > 0)
            .ToDictionary(m => m.HeaderName, StringComparer.OrdinalIgnoreCase);
        (string name, byte[] bytes) = GameData.Read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(name, bytes)));

        MeshData MeshOf(string unit)
        {
            string mesh = Encoding.ASCII.GetString(physics.GetUnitData(Encoding.ASCII.GetBytes(unit))!.Mesh!);
            return byName[mesh];
        }

        foreach (string turret in new[] { "SAT Turret", "Blaster Turret", "Pulse Turret" })
        {
            MeshData m = MeshOf(turret);
            output.WriteLine($"{turret}: {string.Join(", ", m.Parts.Select(p => p.Name))}; emitters {string.Join(", ", m.Emitters.Select(e => $"{e.Name}{e.Number}"))}");
            Assert.Contains(m.Parts, p => p.Name.StartsWith("turret", StringComparison.OrdinalIgnoreCase)
                || p.Name.StartsWith("X1 Turret", StringComparison.OrdinalIgnoreCase)
                || p.Name.StartsWith("barrel", StringComparison.OrdinalIgnoreCase));
            Assert.NotEqual(0, m.GetNumEmitters());
        }

        MeshData tank = MeshOf("Target Tank");
        output.WriteLine($"Target Tank: {string.Join(", ", tank.Parts.Select(p => p.Name))}; emitters {string.Join(", ", tank.Emitters.Select(e => $"{e.Name}{e.Number}"))}; anims {string.Join(", ", tank.AnimModes.Select(a => a.ModeName))}");
        foreach (UnitData unit in physics.Units.Where(u => u.Mesh is not null && byName.ContainsKey(Encoding.ASCII.GetString(u.Mesh))))
        {
            MeshData m = byName[Encoding.ASCII.GetString(unit.Mesh!)];
            Assert.All(m.Emitters, e => Assert.NotNull(e.MeshPart));
            if (unit.EffectNames[3] is not null)
            {
                Assert.Contains(m.Emitters, e => string.Equals(e.Name, "Thruster", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [GameDataFact]
    public void EmitterPosesFromThePoseAndFromWorldTransformsAgreeInTheRestPose()
    {
        foreach (MeshData mesh in Level100Meshes(_ => true).Select(m => m.Mesh!).Where(m => m.NumParts > 0))
        {
            var pose = new MeshPose(mesh.NumParts);
            foreach (Emitter e in mesh.Emitters)
            {
                (FVector viaPose, _) = MeshPose.GetRTEmitter(mesh, pose, Rest, e.Name, e.Number, renderFrameNumber: 5);
                (FVector viaWorld, _) = MeshPose.GetRTEmitter(mesh, null, Rest, e.Name, e.Number, renderFrameNumber: 5);

                // The pose evaluates every part from its keys; GetWorldTransforms uses the part's cached frames when it
                // has them. In the rest pose they should agree to rounding.
                Assert.True((viaPose - viaWorld).Magnitude() < 1e-3f, $"{mesh.HeaderName} {e.Name}{e.Number}: {viaPose} vs {viaWorld}");
            }
        }
    }

    private static int Chain(MeshData m)
    {
        int n = 0;
        for (MeshData? c = m.NextInChain; c is not null; c = c.NextInChain)
        {
            n++;
        }

        return n;
    }
}
