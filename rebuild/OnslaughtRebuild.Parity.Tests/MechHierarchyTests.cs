// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class MechHierarchyTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    // These are authored inputs, not captured game poses. The pristine instruction block 0049b719..0049b775
    // produces the expected words in isolated PC24 execution; the two cancellation cases distinguish Y/Z's
    // (Z+X)+Y from the generic (Z+Y)+X, while X still uses the generic order.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AnimatedHierarchyUsesItsOwnPointProductOrders(int fixture)
    {
        FVector ones = new(1, 1, 1);
        var repeated = new FMatrix(ones, ones, ones);
        var rotation = new FMatrix(
            new FVector(F(0xbe8d1ea2), F(0xbf3e26ab), F(0x3f1c3463)),
            new FVector(F(0x3eea817d), F(0xbf28d878), F(0xbf189308)),
            new FVector(F(0x3f585aa2), F(0x3debeea2), F(0x3f05a17a)));
        float small = F(0x33000000);
        if (fixture == 0) Check(repeated, new FVector(1, small, -1), 0, 0x33000000, 0x33000000);
        if (fixture == 1) Check(repeated, new FVector(small, 1, -1), 0x33000000, 0, 0);
        // A separately generated, float-rounded rotation matrix and a plausible nonzero key position.
        if (fixture == 2) Check(rotation, new FVector(5, 1, -3), 0xc07ce41a, 0x405ac916, 0x403197a3);
    }

    [Fact]
    public void AnimatedHierarchyPreservesIdentityControl()
    {
        Check(FMatrix.Identity, new FVector(1, 2, -3), 0x3f800000, 0x40000000, 0xc0400000);
    }

    private static void Check(FMatrix orientation, FVector keyPosition, uint x, uint y, uint z)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var controller = new MCBattleEngine(context, new BattleEngine(context));
        var mesh = (MeshData)RuntimeHelpers.GetUninitializedObject(typeof(MeshData));
        var part = new MeshPart(mesh, 1);
        byte[] record = new byte[MeshPart.RecordSize];
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x88), 1); // a non-root, ordinary hierarchy part
        typeof(MeshPart).GetProperty(nameof(MeshPart.Record))!.SetValue(part, record);
        typeof(MeshPart).GetProperty(nameof(MeshPart.VHFrameMap))!.SetValue(part, new byte[] { 0 });
        typeof(MeshPart).GetProperty(nameof(MeshPart.HFramePositions))!.SetValue(part, new[] { keyPosition });
        typeof(MeshPart).GetProperty(nameof(MeshPart.HFrameOrientations))!.SetValue(part, new[] { FMatrix.Identity });
        Set("_mesh", mesh); Set("_numFrames", 1); Set("_partTypes", new int[2]);
        Set("_partPos", new FVector[2]); Set("_oldPartPos", new FVector[2]);
        Set("_partOri", new FMatrix[2]); Set("_oldPartOri", new FMatrix[2]);
        Set("_partFrame", new float[2]); Set("_oldPartFrame", new float[2]);
        object[] args = [new FVector(), orientation, part, -1, 0, 0.0f];
        typeof(MCBattleEngine).GetMethod("UpdateBone", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, args);
        var pos = (FVector)args[0];
        Assert.Equal(x, Bits(pos.X)); Assert.Equal(y, Bits(pos.Y)); Assert.Equal(z, Bits(pos.Z));
        void Set(string name, object value) => typeof(MCBattleEngine)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, value);
    }
}
