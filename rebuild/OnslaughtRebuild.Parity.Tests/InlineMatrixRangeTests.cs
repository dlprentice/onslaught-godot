// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>Authored exact-power controls for the register range of the four inline orientation products.</summary>
public sealed class InlineMatrixRangeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SubnormalTermsSurviveUntilTheFinalStore(int product)
    {
        float tiny = MathF.ScaleB(1, -75);
        foreach (float sign in new[] { 1f, -1f })
        {
            FMatrix left = Rows(new(tiny * sign, tiny * sign, 0));
            FMatrix right = Columns(new(tiny, tiny, 0));
            // Each term is half the smallest float subnormal. Their sum is exactly representable.
            AssertCells(Product(product, left, right), sign * float.Epsilon);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SubnormalTermsCancelBeforeTheFinalStore(int product)
    {
        float tiny = MathF.ScaleB(1, -75);
        FMatrix left = Rows(new(tiny, tiny, 0));
        FMatrix right = Columns(new(tiny * 1.5f, -tiny, 0));
        // Three quarters minus one half of the smallest subnormal rounds to zero at the final store.
        // Storing the first product prematurely would round it up to a whole subnormal instead.
        AssertCells(Product(product, left, right), 0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LargeTermsCancelBeforeTheFinalStore(int product)
    {
        float large = MathF.ScaleB(1, 100);
        FMatrix left = Rows(new(large, large, 0));
        FMatrix right = Columns(new(large, -large, 0));
        // Both terms exceed float's range but fit the register; their sum is exactly positive zero.
        AssertCells(Product(product, left, right), 0);
    }

    private static FMatrix Product(int product, FMatrix left, FMatrix right) => product switch
    {
        0 => MCBattleEngine.MechFirstLegProduct(left, right),
        1 => MCBattleEngine.MechSecondLegProduct(left, right),
        2 => MatrixOps.CachedFrameWorldOrientation(left, right),
        3 => MatrixOps.CachedPoseWorldOrientation(left, right),
        _ => throw new ArgumentOutOfRangeException(nameof(product)),
    };

    private static FMatrix Rows(FVector row) => new(row, row, row);

    private static FMatrix Columns(FVector column) => new(
        new(column.X, column.X, column.X),
        new(column.Y, column.Y, column.Y),
        new(column.Z, column.Z, column.Z));

    private static void AssertCells(FMatrix actual, float expected)
    {
        uint bits = BitConverter.SingleToUInt32Bits(expected);
        foreach (FVector row in new[] { actual.Row0, actual.Row1, actual.Row2 })
            foreach (float value in new[] { row.X, row.Y, row.Z })
                Assert.Equal(bits, BitConverter.SingleToUInt32Bits(value));
    }
}
