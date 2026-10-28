// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class MatrixOrderTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    private static FMatrix CancellationLeft()
    {
        var row = new FVector(1, -1, 1);
        return new FMatrix(row, row, row);
    }

    private static FMatrix CancellationRight()
    {
        float small = F(0x33000000);
        return new FMatrix(new FVector(1, 1, 1), new FVector(1, 1, 1), new FVector(small, small, small));
    }

    [Fact]
    public void FirstMechLegKeepsTheInlinedRetailCellOrders()
    {
        FMatrix m = MCBattleEngine.MechFirstLegProduct(CancellationLeft(), CancellationRight());
        Assert.Equal(0x33000000u, Bits(m.Row0.X));
        Assert.Equal(0u, Bits(m.Row0.Y)); // 0049af76..0049af96: (Z+Y)+X
        Assert.Equal(0u, Bits(m.Row0.Z));
        Assert.Equal(0u, Bits(m.Row1.X));
        Assert.Equal(0x33000000u, Bits(m.Row1.Y));
        Assert.Equal(0x33000000u, Bits(m.Row1.Z));
        Assert.Equal(0x33000000u, Bits(m.Row2.X));
        Assert.Equal(0u, Bits(m.Row2.Y));
        Assert.Equal(0u, Bits(m.Row2.Z));
    }

    [Fact]
    public void SecondMechLegKeepsTheInlinedRetailCellOrders()
    {
        FMatrix m = MCBattleEngine.MechSecondLegProduct(CancellationLeft(), CancellationRight());
        Assert.Equal(0u, Bits(m.Row0.X));
        Assert.Equal(0u, Bits(m.Row0.Y)); // 0049b14a..0049b169: (X+Z)+Y
        Assert.Equal(0u, Bits(m.Row0.Z)); // 0049b127..0049b147: (X+Z)+Y
        Assert.Equal(0x33000000u, Bits(m.Row1.X));
        Assert.Equal(0x33000000u, Bits(m.Row1.Y));
        Assert.Equal(0x33000000u, Bits(m.Row1.Z));
        Assert.Equal(0u, Bits(m.Row2.X));
        Assert.Equal(0u, Bits(m.Row2.Y));
        Assert.Equal(0u, Bits(m.Row2.Z));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachProductCellUsesItsOwnRetailGrouping(int activeOrder)
    {
        // Each finite input leaves 2^-25 only for one grouping: X=(X+Y)+Z, Y=(Y+Z)+X, Z=(X+Z)+Y.
        // The per-cell order tables come from the four retail instruction regions named by the production methods.
        float small = F(0x33000000);
        float[][] terms = [[1, -1, small], [1, small, -1], [small, 1, -1]];
        float[] t = terms[activeOrder];
        var ones = new FVector(1, 1, 1);
        var left = new FMatrix(ones, ones, ones);
        var right = new FMatrix(new FVector(t[0], t[0], t[0]), new FVector(t[1], t[1], t[1]),
            new FVector(t[2], t[2], t[2]));
        char active = "XZY"[activeOrder];

        Check(MCBattleEngine.MechFirstLegProduct(left, right), ["XYY", "YXX", "XYZ"], active, "first leg");
        Check(MCBattleEngine.MechSecondLegProduct(left, right), ["YZZ", "XXX", "ZZZ"], active, "second leg");
        Check(MatrixOps.CachedFrameWorldOrientation(left, right), ["YYZ", "ZXZ", "ZXZ"], active, "cached frame");
        Check(MatrixOps.CachedPoseWorldOrientation(left, right), ["XXY", "XXZ", "XXZ"], active, "cached pose");
    }

    private static void Check(FMatrix matrix, string[] orders, char active, string owner)
    {
        FVector[] rows = [matrix.Row0, matrix.Row1, matrix.Row2];
        for (int r = 0; r < 3; r++)
        {
            float[] cells = [rows[r].X, rows[r].Y, rows[r].Z];
            for (int c = 0; c < 3; c++)
            {
                uint expected = orders[r][c] == active ? 0x33000000u : 0u;
                uint actual = Bits(cells[c]);
                Assert.True(actual == expected, $"{owner} row {r} column {c}: expected {expected:x8}, got {actual:x8}");
            }
        }
    }

    [Fact]
    public void CachedPartWorldTransformKeepsItsRetailOrders()
    {
        FMatrix world = new(
            new FVector(F(0xbe8d1ea2), F(0xbf3e26ab), F(0x3f1c3463)),
            new FVector(F(0x3eea817d), F(0xbf28d878), F(0xbf189308)),
            new FVector(F(0x3f585aa2), F(0x3debeea2), F(0x3f05a17a)));
        FMatrix pose = new(
            new FVector(F(0x3e9b84c0), F(0x3c9e4920), F(0x3f73dac2)),
            new FVector(F(0xbef0bb67), F(0xbf5dd4db), F(0x3e2b86a8)),
            new FVector(F(0x3f5422b7), F(0xbeff5c78), F(0xbe821c25)));
        MeshPart part = EmptyPart();
        BinaryPrimitives.WriteInt32LittleEndian(part.Record.AsSpan(0x118), 1);
        typeof(MeshPart).GetProperty(nameof(MeshPart.CachedPosition))!.SetValue(part, new[] { new FVector() });
        typeof(MeshPart).GetProperty(nameof(MeshPart.CachedOrientation))!.SetValue(part, new[] { pose });

        part.GetWorldTransforms(0, 0, 0, new Subject(world), out FMatrix actual, out _, false, false,
            new MeshTransformStatics(() => 5));

        Assert.Equal(0xbd36dad8u, Bits(actual.Row1.X));
        Assert.Equal(0x3f60ac08u, Bits(actual.Row1.Y)); // 004b1484..004b149e: (X+Y)+Z
        Assert.Equal(0x3f229599u, Bits(actual.Row2.X));
    }

    [Fact]
    public void CachedPoseWorldTransformUsesItsOwnMatrixAndPositionOrders()
    {
        MeshPart part = EmptyPart();
        var mesh = (MeshData)RuntimeHelpers.GetUninitializedObject(typeof(MeshData));
        var pose = new MeshPose(1);
        typeof(MeshPose).GetProperty(nameof(MeshPose.LastFrame))!.SetValue(pose, 5);
        pose.Orientations[0] = CancellationRight();
        pose.Positions[0] = new FVector(1, 1, F(0x33000000));

        MeshPose.EvaluatePose(mesh, pose, new Subject(CancellationLeft()), part, new MeshTransformStatics(() => 5),
            out FVector position, out FMatrix orientation, false, false);

        Assert.Equal(0x33000000u, Bits(position.X));
        Assert.Equal(0x33000000u, Bits(position.Y));
        Assert.Equal(0x33000000u, Bits(position.Z));
        Assert.Equal(0u, Bits(orientation.Row1.Z));
        Assert.Equal(0u, Bits(orientation.Row2.Z));
    }

    private static MeshPart EmptyPart()
    {
        var part = (MeshPart)RuntimeHelpers.GetUninitializedObject(typeof(MeshPart));
        typeof(MeshPart).GetProperty(nameof(MeshPart.Record))!.SetValue(part, new byte[MeshPart.RecordSize]);
        return part;
    }

    private sealed class Subject(FMatrix orientation) : IRenderSubject
    {
        public int RealAnimIndex => -1;
        public float RenderFrame => 0;
        public FVector RenderPos => new(0, 0, 0);
        public FMatrix RenderOrientation => orientation;
        public bool HasMotionController => false;
        public bool RenderUseHierarchy(int partNumber) => true;
    }
}
