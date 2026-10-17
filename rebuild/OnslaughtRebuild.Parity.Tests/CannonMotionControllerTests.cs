// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class CannonMotionControllerTests
{
    private static readonly string[] Names = ["turret", "barrel", "barrel1", "hull", "Turret", "Barrel", "turretx", "barre", "barrelExtra"];

    private static Cannon NewCannon()
    {
        var pool = new SPtrSetPool();
        pool.Init(64);
        var context = new ThingContext(pool, new EventManager(), new SeedUniformRandomNumberStream(1), null!, null!);
        return new Cannon(context);
    }

    // An authored empty part, using the real record reader. Only its name and number are inputs to this controller.
    private static MeshPart Part(string name, int number)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        void Chunk(string tag, byte[] data)
        {
            writer.Write(ChunkReader.MakeId(tag));
            writer.Write(data.Length);
            writer.Write(data);
        }

        byte[] record = new byte[MeshPart.RecordSize];
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x88), number);
        Encoding.ASCII.GetBytes(name).CopyTo(record, 0xdc);
        Chunk("PART", []);
        Chunk("DATA", record);
        Chunk("BBOX", []);
        Chunk("DATA", new byte[40]);
        Chunk("VERT", []);
        Chunk("DATA", new byte[MeshVertexBuffer.RecordSize]);
        var part = new MeshPart(null!, 0);
        part.Deserialize(new ChunkReader(MemBuffer.FromFile("authored-cannon-part.bin", stream.ToArray())));
        return part;
    }

    private static string MatrixBits(FMatrix m) => $"{RetailOracle.Hv(m.Row0)} {RetailOracle.Hv(m.Row1)} {RetailOracle.Hv(m.Row2)}";

    [RetailOracleFact("cannon")]
    public void CompleteTransformAgreesWithOriginalCodeForBranchesFractionsAndNoncommutingMatrices()
    {
        Cannon cannon = NewCannon();
        var ours = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("cannon"))
        {
            Assert.Equal("Cannon", function);
            Assert.Equal(25, w.Length);
            Assert.Equal(0u, w[3]); // The original-code cases use zero render fraction and vary LastMoveTime.
            cannon.UnknownE4 = RetailOracle.F(w[4]);
            cannon.UnknownE0 = RetailOracle.F(w[5]);
            cannon.UnknownF0 = RetailOracle.F(w[6]);
            cannon.UnknownE8 = RetailOracle.F(w[7]);
            cannon.UnknownF4 = RetailOracle.F(w[8]);
            cannon.CurrentOrientation.Pitch = RetailOracle.F(w[9]);
            cannon.OldBob = RetailOracle.F(w[10]);
            cannon.Bob = RetailOracle.F(w[11]);
            cannon.LastMoveTime = RetailOracle.F(w[24]);
            FVector pos = RetailOracle.V(w, 12);
            FMatrix ori = new(RetailOracle.V(w, 15), RetailOracle.V(w, 18), RetailOracle.V(w, 21));
            var controller = new CannonMotionController(cannon);
            bool Changed() => cannon.UnknownE4 != cannon.UnknownE0 || cannon.UnknownE0 != controller.LastYaw
                || cannon.UnknownF0 != cannon.UnknownE8 || cannon.UnknownE8 != controller.LastPitch;
            bool before = Changed();
            controller.GetPartTransform(Part(Names[w[0]], (int)w[1]), ref pos, ref ori, w[2] != 0);
            string actual = $"Cannon {RetailOracle.Hv(pos)} {MatrixBits(ori)} {RetailOracle.H(controller.LastYaw)} "
                + $"{RetailOracle.H(controller.LastPitch)} {(before ? 1 : 0):x8} {(Changed() ? 1 : 0):x8}";
            ours.Add(actual);
            Assert.Equal(expected, actual);
        }

        Assert.True(ours.Count >= 1000);
        RetailOracle.WriteOurs("cannon", ours.ToArray());
    }

    [Theory]
    [InlineData("Turret")]
    [InlineData("Barrel")]
    [InlineData("turretx")]
    [InlineData("barre")]
    [InlineData("hull")]
    public void OtherNamesLeaveOrientationAloneButRootStillGetsInterpolatedDrop(string name)
    {
        Cannon cannon = NewCannon();
        cannon.UnknownE0 = 0.7f;
        cannon.UnknownE8 = -0.4f;
        cannon.LastMoveTime = -0.1f; // Cannon moves every four ticks: fraction = 0.5.
        cannon.OldBob = 0.125f;
        cannon.Bob = 0.375f;
        var controller = new CannonMotionController(cannon);
        FMatrix ori = new(new FVector(1, 2, 3), new FVector(4, 5, 6), new FVector(7, 8, 9));
        string unchanged = MatrixBits(ori);
        FVector pos = new(2, 3, 4);
        controller.GetPartTransform(Part(name, 0), ref pos, ref ori, false);
        Assert.Equal(unchanged, MatrixBits(ori));
        Assert.Equal(new FVector(2, 3, 3.75f), pos);
        Assert.Equal((0.7f, -0.4f), (controller.LastYaw, controller.LastPitch));
        controller.GetPartTransform(Part(name, 1), ref pos, ref ori, true);
        Assert.Equal(new FVector(2, 3, 3.75f), pos);
    }

    [Theory]
    [InlineData("turret")]
    [InlineData("barrel1")]
    public void PartRotationUsesItsOwnAxisAndCurrentCannonPitch(string name)
    {
        Cannon cannon = NewCannon();
        cannon.UnknownE0 = cannon.UnknownE4 = 0.7f;
        cannon.UnknownE8 = cannon.UnknownF0 = 0.6f;
        cannon.UnknownF4 = 0.1f;
        cannon.CurrentOrientation.Pitch = 0.2f;
        var controller = new CannonMotionController(cannon);
        FVector pos = new(2, 3, 4);
        FMatrix ori = FMatrix.Identity;
        controller.GetPartTransform(Part(name, 1), ref pos, ref ori, true);
        Assert.Equal(new FVector(2, 3, 4), pos);
        Assert.NotEqual(MatrixBits(FMatrix.Identity), MatrixBits(ori));
        if (name == "turret")
        {
            Assert.Equal(1.0f, ori.Row2.Z);
            Assert.True(ori.Row0.Y < -0.6f);
        }
        else
        {
            Assert.Equal(1.0f, ori.Row0.X);
            Assert.InRange(ori.Row2.Y, 0.295f, 0.296f); // sin((0.6 - 0.1) - 0.2), not the vehicle's slope.
        }
    }

    [GameDataFact]
    public void Level100CannonRoutesPoseAndUncachedEmitterThroughControllerWithoutBypassingPoseGate()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read);
        Cannon cannon = session.Context.World.Things.ToList().OfType<Cannon>()
            .First(c => c.RenderThing?.MeshOrNull is { } m && m.Parts.Any(p => p.Name == "turret")
                && m.Emitters.Any(e => e.MeshPart is not null));
        RenderThing render = cannon.RenderThing!;
        MeshData mesh = render.Mesh;
        Assert.NotNull(cannon.MotionController);
        Assert.Same(cannon.MotionController, ((IRenderSubject)cannon).PartController);
        Assert.DoesNotContain("CMCCannon (motion controller)", session.Context.NotPorted.Keys);
        bool OnTurret(MeshPart? p) => p is not null && (p.Name == "turret" || OnTurret(p.Parent));
        Emitter emitter = mesh.Emitters.First(e => OnTurret(e.MeshPart));

        cannon.UnknownE0 = cannon.UnknownE4 = 0.1f;
        session.Context.RenderFrameNumber = 5;
        (FVector Pos, FMatrix Ori) Read(bool cached, bool force = true) => MeshPose.GetRTEmitter(mesh,
            cached ? render.Pose : null, cannon, emitter.Name, emitter.Number, session.Context.TransformStatics, true, force);
        var first = Read(true);
        cannon.UnknownE0 = cannon.UnknownE4 = 0.7f;
        var sameFrame = Read(true);
        Assert.Equal(MatrixBits(first.Ori), MatrixBits(sameFrame.Ori));
        Assert.Equal(0.1f, cannon.MotionController!.LastYaw);
        var uncached = Read(false);
        Assert.NotEqual(MatrixBits(first.Ori), MatrixBits(uncached.Ori));
        Assert.Equal(0.7f, cannon.MotionController.LastYaw);
        session.Context.RenderFrameNumber = 6;
        var unforced = Read(true, force: false);
        Assert.Equal(MatrixBits(first.Ori), MatrixBits(unforced.Ori));
        var refreshed = Read(true);
        Assert.NotEqual(MatrixBits(first.Ori), MatrixBits(refreshed.Ori));
        Assert.Equal(6, render.Pose!.LastFrame);

        // The uncached path also must not reuse the previous same-frame static result when a controller applies.
        cannon.UnknownE0 = cannon.UnknownE4 = -0.4f;
        var movedAgain = Read(false);
        Assert.NotEqual(MatrixBits(uncached.Ori), MatrixBits(movedAgain.Ori));
        Assert.Equal(-0.4f, cannon.MotionController.LastYaw);
    }
}
