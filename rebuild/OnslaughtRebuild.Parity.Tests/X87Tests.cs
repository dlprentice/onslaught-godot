// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// X87 against an exact reference: each operation's true result as a rational number, rounded once to a 24-bit
/// significand with ties to even. This checks the error-free arithmetic itself; the VC6 oracle tables check that the
/// rounding model is the one retail's FPU applies.
/// </summary>
public sealed class X87Tests
{
    private const int Cases = 2_000_000;

    [Fact]
    public void AddMatchesTheExactReference() => CheckBinary(X87.Add, (a, b) => Exact.Add(a, b), seed: 1);

    [Fact]
    public void SubMatchesTheExactReference() => CheckBinary(X87.Sub, (a, b) => Exact.Add(a, -b), seed: 2);

    [Fact]
    public void MulMatchesTheExactReference() => CheckBinary(X87.Mul, (a, b) => Exact.Mul(a, b), seed: 3);

    [Fact]
    public void DivMatchesTheExactReference() => CheckBinary(X87.Div, (a, b) => Exact.Div(a, b), seed: 4);

    [Fact]
    public void SqrtMatchesTheExactReference()
    {
        var r = new Random(5);
        for (int i = 0; i < Cases; i++)
        {
            double a = Math.Abs(Operand(r));
            AssertSame(Exact.Sqrt(a), X87.Sqrt(a), $"sqrt({a:R})");
        }
    }

    // b = bMantissa * 2^bExponent (C# has no hexadecimal float literals).
    [Theory]
    [InlineData(1.0, 1.0, -24, 1.0)]                                 // a tie rounds to even
    [InlineData(1.0, 3.0, -24, 1.0000002384185791)]                  // a tie rounds to even (up)
    [InlineData(1.0, 1.0000000000000002, -24, 1.0000001192092896)]   // just past the tie rounds up
    [InlineData(1.0000001192092896, -1.0, -80, 1.0000001192092896)]  // a 24-bit value minus a crumb stays
    public void AddRoundsAtTwentyFourBits(double a, double bMantissa, int bExponent, double expected)
    {
        Assert.Equal(expected, X87.Add(a, Math.ScaleB(bMantissa, bExponent)));
    }

    [Fact]
    public void ResultsBeyondFloatRangeStayFinite()
    {
        // 1e60 is far beyond float range; the register's wide exponent keeps it finite, rounded to 24 bits
        // (relative error at most 2^-24).
        double big = X87.Mul(1e30, 1e30);
        Assert.True(double.IsFinite(big));
        Assert.True(Math.Abs(big - 1e60) <= Math.ScaleB(1e60, -24));
        Assert.Equal(big, X87.Round24(big));
        Assert.True(X87.Mul(1e-30, 1e-30) > 0);
    }

    [Fact]
    public void SpecialValuesPassThrough()
    {
        Assert.True(double.IsNaN(X87.Add(double.NaN, 1)));
        Assert.True(double.IsNaN(X87.Sqrt(-1)));
        Assert.Equal(double.PositiveInfinity, X87.Div(1, 0));
        Assert.Equal(double.NegativeInfinity, X87.Div(-1, 0));
        Assert.True(double.IsPositive(X87.Add(0.0, -0.0)));
        Assert.True(double.IsNegative(X87.Mul(-0.0, 5)));
    }

    [Theory]
    [InlineData(2.5, 2)]
    [InlineData(3.5, 4)]
    [InlineData(-2.5, -2)]
    [InlineData(-0.5, 0)]
    [InlineData(0.49999999999999994, 0)]
    [InlineData(2147483647.6, -2147483648)]   // wraps: the 64-bit store keeps the low half
    [InlineData(4294967296.0, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(1e300, 0)]
    public void FistRoundsToNearestEvenAndKeepsTheLowHalf(double x, int expected)
    {
        Assert.Equal(expected, X87.Fist(x));
    }

    [Theory]
    [InlineData(2.9, 2)]
    [InlineData(-2.9, -2)]
    [InlineData(2147483648.5, -2147483648)]
    [InlineData(double.NegativeInfinity, 0)]
    public void FtolTruncatesAndKeepsTheLowHalf(double x, int expected)
    {
        Assert.Equal(expected, X87.Ftol(x));
    }

    private static void CheckBinary(Func<double, double, double> actual, Func<double, double, double> expected, int seed)
    {
        var r = new Random(seed);
        for (int i = 0; i < Cases; i++)
        {
            (double a, double b) = Pair(r);
            double want = expected(a, b);
            AssertSame(want, actual(a, b), $"case {i}: {a:R}, {b:R}");
        }
    }

    private static void AssertSame(double expected, double actual, string what)
    {
        if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual)
            && !(double.IsNaN(expected) && double.IsNaN(actual)))
        {
            Assert.Fail($"{what}: expected {expected:R}, got {actual:R}");
        }
    }

    /// <summary>A float32 value, a full double, or an exact midpoint between two floats, wide magnitudes.</summary>
    private static double Operand(Random r)
    {
        double magnitude = Math.ScaleB(1 + r.NextDouble(), r.Next(-60, 61)) * (r.Next(2) == 0 ? 1 : -1);
        return r.Next(3) switch
        {
            0 => (float)magnitude,
            1 => magnitude,
            _ => Midpoint((float)magnitude),
        };
    }

    private static double Midpoint(float f) => ((double)f + MathF.BitIncrement(f)) / 2;

    private static (double, double) Pair(Random r)
    {
        double a = Operand(r);
        return r.Next(4) switch
        {
            // Operands chosen so the exact result lands on or next to a 24-bit tie.
            0 => (a, Math.ScaleB(a, -r.Next(24, 90)) * (r.Next(2) == 0 ? 1 : -1)),
            1 => (Midpoint((float)a), 1 + (r.Next(2) == 0 ? Math.ScaleB(1.0, -52) : -Math.ScaleB(1.0, -53))),
            _ => (a, Operand(r)),
        };
    }

    /// <summary>Exact rational results rounded once to a 24-bit significand, ties to even.</summary>
    private static class Exact
    {
        public static double Add(double a, double b)
        {
            if (!double.IsFinite(a) || !double.IsFinite(b))
            {
                return a + b;
            }

            (BigInteger na, int ea) = Decompose(a);
            (BigInteger nb, int eb) = Decompose(b);
            int e = Math.Min(ea, eb);
            BigInteger n = (na << (ea - e)) + (nb << (eb - e));
            return n.IsZero ? a + b : Round(n, BigInteger.One, e);
        }

        public static double Mul(double a, double b)
        {
            double p = a * b;
            if (!double.IsFinite(p) || p == 0)
            {
                return p;
            }

            (BigInteger na, int ea) = Decompose(a);
            (BigInteger nb, int eb) = Decompose(b);
            return Round(na * nb, BigInteger.One, ea + eb);
        }

        public static double Div(double a, double b)
        {
            double q = a / b;
            if (!double.IsFinite(q) || q == 0)
            {
                return q;
            }

            (BigInteger na, int ea) = Decompose(a);
            (BigInteger nb, int eb) = Decompose(b);
            return Round(na, nb, ea - eb);
        }

        public static double Sqrt(double a)
        {
            double s = Math.Sqrt(a);
            if (!double.IsFinite(s) || s == 0)
            {
                return s;
            }

            (BigInteger n, int e) = Decompose(a);
            if ((e & 1) != 0)
            {
                n <<= 1;
                e--;
            }

            // sqrt(n * 2^e) = sqrt(n) * 2^(e/2); scale n by 4^k so sqrt has at least 26 integer bits.
            int k = Math.Max(0, (int)((56 - (long)n.GetBitLength()) / 2) + 1);
            BigInteger scaled = n << (2 * k);
            BigInteger root = IntegerSqrt(scaled);
            bool inexact = root * root != scaled;
            return RoundInteger(root, inexact, e / 2 - k);
        }

        /// <summary>value = m * 2^e with m an integer (sign included).</summary>
        private static (BigInteger, int) Decompose(double v)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            int exponentField = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            long m = exponentField == 0 ? fraction : fraction | (1L << 52);
            int e = (exponentField == 0 ? 1 : exponentField) - 1075;
            return (v < 0 ? -m : m, e);
        }

        /// <summary>Rounds num / den * 2^e (den positive) to 24 bits.</summary>
        private static double Round(BigInteger num, BigInteger den, int e)
        {
            if (den.Sign < 0)
            {
                num = -num;
                den = -den;
            }

            int sign = num.Sign;
            num = BigInteger.Abs(num);
            // Scale so the integer quotient has 26+ bits: q = floor(num * 2^s / den).
            int s = 30 + (int)den.GetBitLength() - (int)num.GetBitLength();
            BigInteger q = s >= 0 ? BigInteger.DivRem(num << s, den, out BigInteger rem) : BigInteger.DivRem(num, den << -s, out rem);
            double magnitude = RoundInteger(q, !rem.IsZero, e - s);
            return sign < 0 ? -magnitude : magnitude;
        }

        /// <summary>Rounds the exact value (q + sticky fraction) * 2^e to 24 bits, q having 26 or more bits.</summary>
        private static double RoundInteger(BigInteger q, bool sticky, int e)
        {
            int extra = (int)q.GetBitLength() - 24;
            BigInteger kept = q >> extra;
            BigInteger dropped = q - (kept << extra);
            BigInteger half = BigInteger.One << (extra - 1);
            bool up = dropped > half || (dropped == half && (sticky || !kept.IsEven));
            if (up)
            {
                kept += 1;
            }

            return Math.ScaleB((double)kept, e + extra);
        }

        private static BigInteger IntegerSqrt(BigInteger n)
        {
            BigInteger x = (BigInteger)Math.Sqrt((double)n);
            while (x * x > n)
            {
                x--;
            }

            while ((x + 1) * (x + 1) <= n)
            {
                x++;
            }

            return x;
        }
    }
}
