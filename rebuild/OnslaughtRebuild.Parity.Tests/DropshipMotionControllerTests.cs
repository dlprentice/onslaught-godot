// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class DropshipMotionControllerTests
{
    private static readonly string[] Names = ["door", "Door1", "DOOR_1", "doors", "Door", "dor", "Doorway", "dOOr", "hatch", ""];
    private static readonly string[] Modes = ["dooropening", "DoorOpening", "doorclosing", "DoorClosing", "dooropen", "doorclosed", "Other", "opening_extra"];

    private static Dropship NewDropship(float fraction)
    {
        // Obtain a real frame time from the existing deterministic loop; changing its public tick length yields
        // exact binary fractions without reflection or a production-only test setter for GAME.mFraction.
        var sim = new ClockSimulation(100, 2, 2);
        if (fraction != 0)
        {
            while (sim.Loop.FrameTime <= sim.Loop.BaseTime)
            {
                sim.AdvanceToNextPresent();
            }

            sim.Loop.FrameLength = (sim.Loop.FrameTime - sim.Loop.BaseTime) / fraction;
            sim.Loop.CalcRenderFrameFraction();
        }

        Assert.Equal(fraction, sim.Loop.Fraction);
        var pool = new SPtrSetPool();
        pool.Init(64);
        var context = new ThingContext(pool, sim.Events, new SeedUniformRandomNumberStream(1), null!, null!) { Game = sim.Game };
        return new Dropship(context);
    }

    private static void Int(byte[] data, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
    private static void Float(byte[] data, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset), value);

    // Authored one-part mesh, through the real record readers. Key N is translated by 10*N along X.
    private static MeshData DoorMesh(string name, IReadOnlyList<AnimMode> modes, int frames = 5)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        void Chunk(string tag, byte[] data)
        {
            writer.Write(ChunkReader.MakeId(tag)); writer.Write(data.Length); writer.Write(data);
        }

        Chunk("MESH", []);
        Chunk("NAME", new byte[309]);
        byte[] mesh = new byte[MeshData.RecordSize];
        Int(mesh, 0x14, modes.Count); Int(mesh, 0x1c, 1); Int(mesh, 0x15c, 1);
        Chunk("CMSH", mesh); Chunk("CMST", []);
        byte[] part = new byte[MeshPart.RecordSize];
        Int(part, 0x8c, 1); Int(part, 0xb8, frames); Int(part, 0xbc, frames);
        Int(part, 0xc8, 1); Int(part, 0x10c, 1);
        Encoding.ASCII.GetBytes(name).CopyTo(part, 0xdc);
        Chunk("MESP", []); Chunk("CMPR", part);
        Chunk("BBOX", []); Chunk("DATA", new byte[40]);
        Chunk("VHFM", Enumerable.Range(0, frames).Select(i => (byte)i).ToArray());
        byte[] orientations = new byte[48 * frames], positions = new byte[16 * frames];
        for (int i = 0; i < frames; i++)
        {
            Float(orientations, i * 48, 1); Float(orientations, (i * 48) + 20, 1); Float(orientations, (i * 48) + 40, 1);
            Float(positions, i * 16, i * 10);
        }

        Chunk("HORI", orientations); Chunk("HPOS", positions);
        Chunk("VERT", []); Chunk("DATA", new byte[MeshVertexBuffer.RecordSize]);
        if (modes.Count > 0)
        {
            byte[] records = new byte[36 * modes.Count];
            for (int i = 0; i < modes.Count; i++)
            {
                AnimMode m = modes[i];
                Encoding.ASCII.GetBytes(m.ModeName).CopyTo(records, i * 36);
                Int(records, (i * 36) + 16, m.Mode); Int(records, (i * 36) + 20, m.Start);
                Int(records, (i * 36) + 24, m.Last); Int(records, (i * 36) + 28, m.FrameScaler);
                Float(records, (i * 36) + 32, m.Rate);
            }

            Chunk("CAMD", records);
        }

        Chunk("BBOX", []); Chunk("DATA", new byte[40]);
        byte[] emitter = new byte[4 + 0x150 + 4];
        Int(emitter, 0, 0x150); Int(emitter, 4 + 0x40, 1); Int(emitter, 4 + 0x14c, 1);
        Encoding.ASCII.GetBytes("DoorEmitter").CopyTo(emitter, 4 + 0x4c);
        Chunk("CEMT", emitter);
        return MeshData.Deserialize(new ChunkReader(MemBuffer.FromFile("authored-door.bin", stream.ToArray())),
            _ => throw new InvalidOperationException("Authored mesh has no external assets"))!;
    }

    private static AnimMode Mode(string name, int start, int scaler) => new(name, 1000, start, start + scaler, scaler, 0);

    [RetailOracleFact("dropship")]
    public void CompleteDoorFrameAgreesWithUnmodifiedOriginalCode()
    {
        var units = new Dictionary<float, Dropship>();
        var ours = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("dropship"))
        {
            Assert.Equal("Dropship", function); Assert.Equal(19, w.Length);
            float fraction = RetailOracle.F(w[4]);
            if (!units.TryGetValue(fraction, out Dropship? unit))
            {
                units[fraction] = unit = NewDropship(fraction);
            }

            unit.DoorState = unchecked((int)w[1]); unit.Unknown2A8 = RetailOracle.F(w[2]); unit.Unknown2A4 = RetailOracle.F(w[3]);
            var modes = new List<AnimMode>();
            for (int i = 0; i < w[6]; i++)
            {
                modes.Add(Mode(Modes[w[7 + (3 * i)]], unchecked((int)w[8 + (3 * i)]), unchecked((int)w[9 + (3 * i)])));
            }

            var controller = new DropshipMotionController(unit);
            bool Changed() => controller.LastDoorState != unit.DoorState || unit.Unknown2A8 != unit.Unknown2A4
                || controller.LastDoorOpen != unit.Unknown2A4;
            bool before = Changed();
            float time = RetailOracle.F(w[5]);
            controller.AdjustFrame(DoorMesh(Names[w[0]], modes).Parts[0], ref time);
            string actual = $"Dropship {RetailOracle.H(time)} {unchecked((uint)controller.LastDoorState):x8} "
                + $"{RetailOracle.H(controller.LastDoorOpen)} {(before ? 1 : 0):x8} {(Changed() ? 1 : 0):x8}";
            ours.Add(actual);
            Assert.Equal(expected, actual);
        }

        Assert.True(ours.Count >= 1000);
        RetailOracle.WriteOurs("dropship", ours.ToArray());
    }

    [Theory]
    [InlineData(0, 0.5f)]
    [InlineData(5, 2.5f)]
    [InlineData(7, 2.5f)]
    [InlineData(6, 0.5f)]
    public void DoorFramesSelectFirstMatchingModeAndTheGlobalRenderFraction(int state, float expected)
    {
        Dropship unit = NewDropship(0.25f);
        unit.DoorState = state; unit.Unknown2A8 = 0; unit.Unknown2A4 = 1;
        unit.LastMoveTime = -20; // Air units move every tick; an old LastMoveTime must not replace global0.25.
        Assert.Equal(0.25f, unit.GetFractionTime());
        MeshData mesh = DoorMesh("DoOr1", [Mode("DoorOpening", 0, 2), Mode("dooropening", 99, 8), Mode("DOORCLOSING", 2, 2)]);
        float time = 71;
        var controller = new DropshipMotionController(unit);
        controller.AdjustFrame(mesh.Parts[0], ref time);
        Assert.Equal(expected, time);
        Assert.Equal(state, controller.LastDoorState); Assert.Equal(1, controller.LastDoorOpen);
    }

    [Fact]
    public void MissingDoorModeKeepsFrameButNonDoorKeepsBothFrameAndCachedState()
    {
        Dropship unit = NewDropship(0.5f);
        unit.DoorState = 5; unit.Unknown2A4 = 0.75f;
        var controller = new DropshipMotionController(unit);
        float frame = 7.25f;
        controller.AdjustFrame(DoorMesh("hatch", [Mode("doorclosing", 2, 2)]).Parts[0], ref frame);
        Assert.Equal((7.25f, -1, -999.0f), (frame, controller.LastDoorState, controller.LastDoorOpen));
        controller.AdjustFrame(DoorMesh("Door", [Mode("dooropening", 0, 2)]).Parts[0], ref frame);
        Assert.Equal((7.25f, 5, 0.75f), (frame, controller.LastDoorState, controller.LastDoorOpen));
    }

    private sealed class Subject(IPartController controller) : IRenderSubject
    {
        public int RealAnimIndex { get; set; }
        public float RenderFrame => 0.9f;
        public FVector RenderPos => new(0, 0, 0);
        public FMatrix RenderOrientation => FMatrix.Identity;
        public bool HasMotionController => false;
        public bool RenderUseHierarchy(int partNumber) => true;
        public IPartController PartController => controller;
    }

    [Fact]
    public void CachedAndUncachedEmitterConsumersUseDoorFramesWithTheirOwnRetailFractionRules()
    {
        Dropship unit = NewDropship(0.25f);
        unit.Unknown2A8 = 0; unit.Unknown2A4 = 1;
        var controller = new DropshipMotionController(unit);
        var subject = new Subject(controller);
        MeshData mesh = DoorMesh("Door", [Mode("dooropening", 0, 2), Mode("doorclosing", 2, 2)]);
        var pose = new MeshPose(1);
        int renderFrame = 5;
        var statics = new MeshTransformStatics(() => renderFrame);
        FVector Read(MeshPose? cached, bool force = true) => MeshPose.GetRTEmitter(mesh, cached, subject, "DoorEmitter", 1,
            statics, true, force).Pos;
        Assert.Equal(5, Read(pose).X); // door frame0.5: cached pose interpolates keys0 and1.
        Assert.Equal(0, Read(null).X); // retail uncached EvaluatePose deliberately discards the fractional remainder.
        unit.Unknown2A8 = unit.Unknown2A4 = 1;
        Assert.Equal(5, Read(pose).X); // same render frame cannot rebuild even when forced.
        Assert.Equal(20, Read(null).X); // AdjustFrame must reach the uncached ResolveFrameNumber call.
        renderFrame++;
        Assert.Equal(5, Read(pose, force: false).X);
        Assert.Equal(20, Read(pose).X);
        unit.DoorState = 5; unit.Unknown2A8 = 0; unit.Unknown2A4 = 1;
        renderFrame++;
        Assert.Equal(25, Read(pose).X);
        Assert.Equal(20, Read(null).X);
        subject.RealAnimIndex = -1;
        Assert.Equal(0, Read(null).X); // EvaluatePose's no-animation branch bypasses ResolveFrameNumber altogether.
    }

    [Theory]
    [InlineData(5, 0, 2, 0.5f)]
    [InlineData(2, 0, 0, 0.5f)]
    [InlineData(1, 0, 2, 0.5f)]
    [InlineData(5, -1, 2, 0.5f)]
    public void ResolveFrameCallsTheHookInAnimatedAndFallbackBranches(int frames, int anim, int expected, float fraction)
    {
        Dropship unit = NewDropship(0.25f);
        unit.DoorState = 5; unit.Unknown2A8 = 0; unit.Unknown2A4 = 1;
        MeshData mesh = DoorMesh("Door", [Mode("doorclosing", 2, 2)], frames);
        int actual = mesh.Parts[0].ResolveFrameNumber(0, anim, out float remainder, new DropshipMotionController(unit));
        Assert.Equal(expected, actual); Assert.Equal(fraction, remainder);
    }

    [GameDataFact]
    public void Level100U17ConstructsTheControllerWithoutInventingDoorNamedPartsOrLanding()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read);
        Dropship unit = Assert.Single(session.Context.World.Things.ToList().OfType<Dropship>());
        Assert.NotNull(unit.MotionController);
        Assert.Same(unit.MotionController, ((IRenderSubject)unit).PartController);
        Assert.DoesNotContain("CMCDropship (motion controller)", session.Context.NotPorted.Keys);
        MeshData mesh = unit.RenderThing!.Mesh;
        Assert.Equal("f_lifter.msh", mesh.HeaderName);
        Assert.Equal(21, mesh.NumParts);
        Assert.DoesNotContain(mesh.Parts, p => p.Name.StartsWith("door", StringComparison.OrdinalIgnoreCase));
        foreach (MeshPart part in mesh.Parts)
        {
            float frame = 1.25f;
            unit.MotionController!.AdjustFrame(part, ref frame);
            Assert.Equal(1.25f, frame);
        }

        session.Context.RenderFrameNumber = 5;
        var posed = MeshPose.GetRTEmitter(mesh, unit.RenderThing.Pose, unit, "Thruster", 1, session.Context.TransformStatics, true, true);
        var uncached = MeshPose.GetRTEmitter(mesh, null, unit, "Thruster", 1, session.Context.TransformStatics, true, true);
        Assert.NotEqual(new FVector(0, 0, 0), posed.Pos);
        Assert.Equal(posed.Pos, uncached.Pos);
        Assert.Equal(0, unit.DoorState); // This fixture does not implement or execute the still-unsupported landing states.
    }
}
