// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Authored operands checked against the original out-of-line rotation and matrix-product functions at PC24.
/// A register keeps a wider exponent than a float, so the final float store can round a second time and terms
/// which would underflow or overflow in float can still add or cancel before the result is stored.
/// </summary>
public sealed class RetailMatrixSubnormalTests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    [Fact]
    public void RotationStoresYawAndRollProductsFromTheRegister()
    {
        float smallAngle = F(0x00710806);
        float pitch = F(0x3f4b9f42);
        FMatrix yaw = FMatrix.MakeRotationOutOfLine(smallAngle, pitch, 0);
        FMatrix roll = FMatrix.MakeRotationOutOfLine(0, pitch, smallAngle);
        Assert.Equal(0x804f1f38u, Bits(yaw.Row0.Y));
        Assert.Equal(0x804f1f38u, Bits(roll.Row2.X));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public void MatrixProductStoresEachSubnormalCellFromTheRegister(int row, int column)
    {
        float small = F(0x00710806);
        float factor = F(0x3f333333);
        var selectedRow = new FVector(0, small, 0);
        var a = row switch
        {
            0 => new FMatrix(selectedRow, default, default),
            1 => new FMatrix(default, selectedRow, default),
            _ => new FMatrix(default, default, selectedRow),
        };
        var factorRow = column switch
        {
            0 => new FVector(factor, 0, 0),
            1 => new FVector(0, factor, 0),
            _ => new FVector(0, 0, factor),
        };
        var b = new FMatrix(default, factorRow, default);
        FMatrix result = a.MultiplyOutOfLine(b);
        FVector resultRow = row switch { 0 => result.Row0, 1 => result.Row1, _ => result.Row2 };
        float cell = column switch { 0 => resultRow.X, 1 => resultRow.Y, _ => resultRow.Z };
        Assert.Equal(0x004f1f38u, Bits(cell));
    }

    [Fact]
    public void MatrixProductAccumulatesTermsBelowTheFloatSubnormalRange()
    {
        float tiny = F(0x1a000000); // 2^-75: its square is half the smallest float subnormal.
        var a = new FMatrix(new FVector(tiny, tiny, 0), default, default);
        var b = new FMatrix(new FVector(tiny, 0, 0), new FVector(tiny, 0, 0), default);
        Assert.Equal(0x00000001u, Bits(a.MultiplyOutOfLine(b).Row0.X));
    }

    [Fact]
    public void MatrixProductCancelsTermsAboveTheFloatRangeBeforeTheStore()
    {
        float large = F(0x71800000); // 2^100: its square does not fit in a float.
        var a = new FMatrix(new FVector(large, -large, 1), default, default);
        var b = new FMatrix(new FVector(large, 0, 0), new FVector(large, 0, 0), new FVector(1, 0, 0));
        Assert.Equal(0x3f800000u, Bits(a.MultiplyOutOfLine(b).Row0.X));
    }
}
