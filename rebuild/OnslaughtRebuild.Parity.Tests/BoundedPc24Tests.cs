// SPDX-License-Identifier: GPL-3.0-only

using System.Numerics;
using System.Runtime.InteropServices;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Tests;

// Test-platform detection stays outside the simulation; unsupported systems report a visible skip.
public sealed class LinuxX64GlibcFactAttribute : FactAttribute
{
    public LinuxX64GlibcFactAttribute()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            !NativeLibrary.TryLoad("libm.so.6", out IntPtr library))
        {
            Skip = "fenv control requires Linux x64 glibc; other platforms need their own rounding-mode test";
            return;
        }
        NativeLibrary.Free(library);
    }
}

public sealed class BoundedPc24Tests
{
    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static readonly int[][] Order =
    [
        [1, 0, 2], [2, 1, 0], [2, 1, 0],
        [2, 1, 0], [0, 2, 1], [0, 2, 1],
        [2, 1, 0], [0, 2, 1], [2, 1, 0],
    ];

    private static FMatrix Matrix(float[] v) => new(
        new FVector(v[0], v[1], v[2]), new FVector(v[3], v[4], v[5]), new FVector(v[6], v[7], v[8]));

    // Independent test oracle: exact integer-dyadic operations and explicit nearest-even rounding.
    private readonly record struct Dyadic(BigInteger Significand, int Exponent, bool NegativeZero = false);

    private static Dyadic From(float value)
    {
        uint bits = Bits(value), magnitude = bits & 0x7fffffffu;
        if (magnitude == 0) return new Dyadic(0, 0, (bits & 0x80000000u) != 0);
        int exponent = (int)(magnitude >> 23);
        if (exponent is 0 or 255) throw new InvalidOperationException("test input is outside the bounded domain");
        BigInteger mantissa = 0x800000u | (magnitude & 0x7fffffu);
        return new Dyadic((bits & 0x80000000u) == 0 ? mantissa : -mantissa, exponent - 150);
    }

    private static Dyadic Round(Dyadic value)
    {
        if (value.Significand.IsZero) return value;
        BigInteger absolute = BigInteger.Abs(value.Significand);
        int discardedBits = (int)absolute.GetBitLength() - 24;
        if (discardedBits <= 0) return value;
        BigInteger retained = absolute >> discardedBits;
        BigInteger remainder = absolute - (retained << discardedBits);
        BigInteger halfway = BigInteger.One << (discardedBits - 1);
        if (remainder > halfway || (remainder == halfway && !retained.IsEven)) retained++;
        int exponent = value.Exponent + discardedBits;
        if (retained.GetBitLength() > 24) { retained >>= 1; exponent++; }
        return new Dyadic(value.Significand.Sign < 0 ? -retained : retained, exponent);
    }

    private static bool Negative(Dyadic value) => value.Significand.Sign < 0 ||
        (value.Significand.IsZero && value.NegativeZero);

    private static Dyadic Multiply(Dyadic a, Dyadic b) => a.Significand.IsZero || b.Significand.IsZero
        ? new Dyadic(0, 0, Negative(a) ^ Negative(b))
        : Round(new Dyadic(a.Significand * b.Significand, a.Exponent + b.Exponent));

    private static Dyadic Add(Dyadic a, Dyadic b)
    {
        if (a.Significand.IsZero && b.Significand.IsZero) return new Dyadic(0, 0, a.NegativeZero && b.NegativeZero);
        if (a.Significand.IsZero) return b;
        if (b.Significand.IsZero) return a;
        int exponent = Math.Min(a.Exponent, b.Exponent);
        BigInteger sum = (a.Significand << (a.Exponent - exponent)) + (b.Significand << (b.Exponent - exponent));
        return Round(new Dyadic(sum, exponent));
    }

    private static uint StoredBits(Dyadic value) => value.Significand.IsZero
        ? (value.NegativeZero ? 0x80000000u : 0u)
        : Bits((float)Math.ScaleB((double)value.Significand, value.Exponent));

    private static void CheckMatrix(float[] a, float[] b)
    {
        FMatrix result = Matrix(a).MultiplyOutOfLine(Matrix(b));
        float[] actual = [result.Row0.X, result.Row0.Y, result.Row0.Z, result.Row1.X, result.Row1.Y, result.Row1.Z,
                          result.Row2.X, result.Row2.Y, result.Row2.Z];
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            int cell = row * 3 + col;
            int[] order = Order[cell];
            Dyadic p0 = Multiply(From(a[row * 3 + order[0]]), From(b[order[0] * 3 + col]));
            Dyadic p1 = Multiply(From(a[row * 3 + order[1]]), From(b[order[1] * 3 + col]));
            Dyadic p2 = Multiply(From(a[row * 3 + order[2]]), From(b[order[2] * 3 + col]));
            Assert.Equal(StoredBits(Add(Add(p0, p1), p2)), Bits(actual[cell]));
        }
    }

    private static uint Next(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return state;
    }

    private static float NextBounded(ref uint state)
    {
        uint selection = Next(ref state);
        uint magnitude = selection % 19 == 0 ? 0 : 0x2f800000u + (Next(ref state) % 0x20000001u);
        return F(magnitude | (selection & 0x80000000u));
    }

    [Fact]
    public void AdmissionIncludesBothSignedZerosAndTheExactBounds()
    {
        foreach (uint value in new uint[] { 0, 0x80000000, 0x2f800000, 0xaf800000, 0x4f800000, 0xcf800000 })
            Assert.True(BoundedPc24.Admitted(F(value)));
        foreach (uint value in new uint[] { 1, 0x2f7fffff, 0xaf7fffff, 0x4f800001, 0xcf800001,
                                            0x7f800000, 0xff800000, 0x7fc00000 })
            Assert.False(BoundedPc24.Admitted(F(value)));
    }

    [Fact]
    public void ProductsAndSumsRoundMidpointsToEvenAcrossExponentBoundaries()
    {
        Assert.Equal(0x3fc00002u, Bits(BoundedPc24.Store(BoundedPc24.Product(F(0x3f800001), F(0x3fc00000)))));
        Assert.Equal(0x3fc00004u, Bits(BoundedPc24.Store(BoundedPc24.Product(F(0x3f800003), F(0x3fc00000)))));
        BoundedPc24.Value one = BoundedPc24.Product(1, 1);
        BoundedPc24.Value halfUlpAbove = BoundedPc24.Product(1, F(0x33800000)); // 2^-24
        BoundedPc24.Value halfUlpBelowNegative = BoundedPc24.Product(-1, F(0x33000000)); // -2^-25
        Assert.Equal(0x3f800000u, Bits(BoundedPc24.Store(BoundedPc24.Add(one, halfUlpAbove))));
        BoundedPc24.Value next = BoundedPc24.Product(1, F(0x3f800001));
        Assert.Equal(0x3f800002u, Bits(BoundedPc24.Store(BoundedPc24.Add(next, halfUlpAbove))));
        Assert.Equal(0x3f800000u, Bits(BoundedPc24.Store(BoundedPc24.Add(one, halfUlpBelowNegative))));
        BoundedPc24.Value pastHalf = BoundedPc24.Product(-1, F(0x33000001));
        Assert.Equal(0x3f7fffffu, Bits(BoundedPc24.Store(BoundedPc24.Add(one, pastHalf))));
        BoundedPc24.Value tiny = BoundedPc24.Product(F(0x2f800000), F(0x2f800000));
        Assert.Equal(0x3f800000u, Bits(BoundedPc24.Store(BoundedPc24.Add(one, tiny))));
    }

    [Fact]
    public void IndependentDyadicOracleAgreesAcrossBoundsAndCancellation()
    {
        // Check the oracle's tie-to-even and signed-zero rules before using it to judge the candidate.
        Assert.Equal(0x4b800000u, StoredBits(Round(new Dyadic((1 << 24) + 1, 0))));
        Assert.Equal(0x4b800002u, StoredBits(Round(new Dyadic((1 << 24) + 3, 0))));
        Assert.Equal(0xcb800000u, StoredBits(Round(new Dyadic(-((1 << 24) + 1), 0))));
        Assert.Equal(0u, StoredBits(Add(From(-1), From(1))));
        Assert.Equal(0x80000000u, StoredBits(Add(new Dyadic(0, 0, true), new Dyadic(0, 0, true))));
        float low = F(0x2f800000), high = F(0x4f800000), next = F(0x2f800001);
        CheckMatrix([next, -low, 0, 0, 0, 0, 0, 0, 0], [low, 0, 0, low, 0, 0, 0, 0, 0]);
        CheckMatrix([high, -high, 1, 0, 0, 0, 0, 0, 0], [high, 0, 0, high, 0, 0, 1, 0, 0]);
        uint state = 0x12ed5a77;
        float[] a = new float[9], b = new float[9];
        for (int i = 0; i < 20_000; i++)
        {
            for (int j = 0; j < 9; j++) { a[j] = NextBounded(ref state); b[j] = NextBounded(ref state); }
            if (i % 3 == 0)
            {
                int row = (i / 3) % 3, col = (i / 9) % 3;
                a[row * 3 + 1] = -a[row * 3]; b[3 + col] = b[col];
            }
            CheckMatrix(a, b);
        }
    }

    [Fact]
    public void EveryCombinationOfSignedZeroOperandsKeepsTheNearestEvenZeroSign()
    {
        float[] a = new float[9], b = new float[9];
        Span<float> actual = stackalloc float[9];
        for (int signs = 0; signs < (1 << 18); signs++)
        {
            for (int j = 0; j < 9; j++)
            {
                a[j] = F((uint)((signs >> j) & 1) << 31);
                b[j] = F((uint)((signs >> (j + 9)) & 1) << 31);
            }
            FMatrix result = Matrix(a).MultiplyOutOfLine(Matrix(b));
            actual[0] = result.Row0.X; actual[1] = result.Row0.Y; actual[2] = result.Row0.Z;
            actual[3] = result.Row1.X; actual[4] = result.Row1.Y; actual[5] = result.Row1.Z;
            actual[6] = result.Row2.X; actual[7] = result.Row2.Y; actual[8] = result.Row2.Z;
            for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                bool allNegative = true;
                for (int k = 0; k < 3; k++) allNegative &= ((Bits(a[r * 3 + k]) ^ Bits(b[k * 3 + c])) >> 31) != 0;
                Assert.Equal(allNegative ? 0x80000000u : 0u, Bits(actual[r * 3 + c]));
            }
        }
    }

    [Fact]
    public void EachOutOfRangeOperandKeepsThePriorRegisterFallback()
    {
        for (int position = 0; position < 18; position++)
        {
            float[] a = new float[9], b = new float[9];
            int row, col;
            if (position < 9)
            {
                row = position / 3; col = 0;
                a[position] = F(0x00710806); b[(position % 3) * 3] = F(0x3f333333);
            }
            else
            {
                int input = position - 9; row = 0; col = input % 3;
                a[input / 3] = F(0x3f333333); b[input] = F(0x00710806);
            }
            FMatrix matrix = Matrix(a).MultiplyOutOfLine(Matrix(b));
            float[] output = [matrix.Row0.X, matrix.Row0.Y, matrix.Row0.Z, matrix.Row1.X, matrix.Row1.Y,
                              matrix.Row1.Z, matrix.Row2.X, matrix.Row2.Y, matrix.Row2.Z];
            Assert.Equal(0x004f1f38u, Bits(output[row * 3 + col]));
        }
    }

    [Fact]
    public void RotationFloatProductMatchesTheIndependentOracleAndKeepsFallback()
    {
        uint state = 0x4c1bcde9;
        for (int i = 0; i < 20_000; i++)
        {
            float a = NextBounded(ref state), b = NextBounded(ref state);
            Assert.Equal(StoredBits(Multiply(From(a), From(b))), Bits(FMatrix.OutOfLineFloatProduct(a, b)));
        }
        Assert.Equal(0x004f1f38u, Bits(FMatrix.OutOfLineFloatProduct(F(0x00710806), F(0x3f333333))));
        Assert.Equal(0x004f1f38u, Bits(FMatrix.OutOfLineFloatProduct(F(0x3f333333), F(0x00710806))));
    }

    [DllImport("libm.so.6")]
    private static extern int fegetround();
    [DllImport("libm.so.6")]
    private static extern int fesetround(int mode);

    [LinuxX64GlibcFact]
    public void BoundedArithmeticIgnoresTheCurrentFloatingPointRoundingModeOnLinuxX64()
    {
        int original = fegetround();
        Assert.True(original >= 0);
        try
        {
            uint state = 0x1138ab79;
            float[] a = new float[9], b = new float[9];
            foreach (int mode in new[] { 0, 0x400, 0x800, 0xc00 })
            {
                Assert.Equal(0, fesetround(mode));
                Assert.Equal(mode, fegetround());
                for (int i = 0; i < 2_000; i++)
                {
                    for (int j = 0; j < 9; j++) { a[j] = NextBounded(ref state); b[j] = NextBounded(ref state); }
                    CheckMatrix(a, b);
                    Assert.Equal(StoredBits(Multiply(From(a[0]), From(b[0]))), Bits(FMatrix.OutOfLineFloatProduct(a[0], b[0])));
                }
            }
        }
        finally { Assert.Equal(0, fesetround(original)); }
    }
}
