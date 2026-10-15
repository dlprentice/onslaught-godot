// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Parity.Numerics;

/// <summary>
/// Arithmetic as retail's x87 FPU performs it under control word 007f: precision control 24 bits, round to nearest
/// even, every exception masked. Every add, subtract, multiply, divide and square root rounds its exact result once to
/// a 24-bit significand, whatever the operands' precision (a double constant or local keeps all 53 bits as an
/// operand), and the register's wide exponent range keeps results beyond float range finite and normal until they are
/// stored. Results are carried in a double, which holds any 24-bit significand exactly.
/// </summary>
/// <remarks>
/// Each operation computes its rounding error exactly (TwoSum, a fused multiply-add for products, remainders for
/// quotients and square roots) and rounds the exact value once, so no double rounding occurs. Plain C# float
/// arithmetic already matches when both operands are floats and the result stays inside float range.
/// </remarks>
public static class X87
{
    private const int DroppedBits = 52 - 23;
    private const long DroppedMask = (1L << DroppedBits) - 1;
    private const long Half = 1L << (DroppedBits - 1);

    /// <summary>The exact sum rounded to 24 bits.</summary>
    public static double Add(double a, double b)
    {
        double s = a + b;
        if (!double.IsFinite(s))
        {
            return s;
        }

        double bv = s - a;
        double av = s - bv;
        double error = (a - av) + (b - bv);
        return Round24(s, error);
    }

    /// <summary>The exact difference rounded to 24 bits.</summary>
    public static double Sub(double a, double b) => Add(a, -b);

    /// <summary>The exact product rounded to 24 bits.</summary>
    public static double Mul(double a, double b)
    {
        double p = a * b;
        if (!double.IsFinite(p) || p == 0)
        {
            return p;
        }

        return Round24(p, Math.FusedMultiplyAdd(a, b, -p));
    }

    /// <summary>The exact quotient rounded to 24 bits.</summary>
    public static double Div(double a, double b)
    {
        double q = a / b;
        if (!double.IsFinite(q) || q == 0)
        {
            return q;
        }

        double remainder = Math.FusedMultiplyAdd(-q, b, a);
        return Round24(q, remainder / b);
    }

    /// <summary>The exact square root rounded to 24 bits (fsqrt honours precision control).</summary>
    public static double Sqrt(double a)
    {
        double s = Math.Sqrt(a);
        if (!double.IsFinite(s) || s == 0)
        {
            return s;
        }

        double remainder = Math.FusedMultiplyAdd(-s, s, a);
        return Round24(s, remainder / (2 * s));
    }

    /// <summary>
    /// The value fsin leaves in the register for <paramref name="x"/>, approximated to double precision. Precision
    /// control does not apply to fsin: it produces a 64-bit significand, rounded where it is stored or by the next
    /// arithmetic operation. Finite arguments of magnitude 2^63 or more are left unchanged; infinities give NaN.
    /// </summary>
    /// <remarks>
    /// Measured against the VC6 oracle on this host's CPU (Intel i7-13700HX), 10^6 inputs: stored to a float, every
    /// result matches. Stored to a double, about 0.13% of results within ±2π differ in the last bits (the double
    /// rounding of fsin's 64-bit result), and about 10% up to ±10^4, where fsin reduces its argument with a 66-bit π.
    /// A float result, or a product or sum rounded to 24 bits, is unaffected except near a 24-bit tie.
    /// </remarks>
    public static double Sin(double x) =>
        double.IsInfinity(x) ? double.NaN : Math.Abs(x) >= 9.2233720368547758e18 ? x : Math.Sin(x);

    /// <summary>The value fcos leaves in the register, approximated to double precision (see <see cref="Sin"/>).</summary>
    public static double Cos(double x) =>
        double.IsInfinity(x) ? double.NaN : Math.Abs(x) >= 9.2233720368547758e18 ? x : Math.Cos(x);

    /// <summary>
    /// The value fpatan leaves in the register for atan2(y, x), approximated to double precision. Stored to a float every
    /// oracle result matches; stored to a double about 0.06% differ by one ulp (see <see cref="Sin"/>).
    /// </summary>
    public static double Atan2(double y, double x) => Math.Atan2(y, x);

    /// <summary>
    /// asin as VC6's runtime computes it (__CIasin 0055dcb0, which code compiled by VC6 calls with the argument in st0):
    /// fpatan(x, fsqrt((1 + x) * (1 - x))), whose add, subtract, multiply and square root round to the caller's 24 bits
    /// and whose fpatan does not; |x| = 1 gives the extended pi/2 with the sign of x; |x| &gt; 1, NaN and infinities
    /// the indefinite NaN. Its exit (00561547) rounds the result to double whenever the control word is not 0x27F and
    /// the sticky inexact flag is set (always, in the game). The fpatan is approximated by Math.Atan2, which differs from
    /// the double-rounded extended result in the last bit of a double now and then (see <see cref="Atan2"/>).
    /// </summary>
    public static double Asin(double x)
    {
        if (double.IsNaN(x) || Math.Abs(x) > 1)
        {
            return double.NaN;
        }

        if (Math.Abs(x) == 1)
        {
            return x > 0 ? Math.PI / 2 : -Math.PI / 2;
        }

        return Atan2(x, AsinAcosRoot(x));
    }

    /// <summary>acos as VC6's runtime computes it (__CIacos 0055f380): fpatan(fsqrt((1 + x) * (1 - x)), x); see <see cref="Asin"/>.</summary>
    public static double Acos(double x)
    {
        if (double.IsNaN(x) || Math.Abs(x) > 1)
        {
            return double.NaN;
        }

        if (Math.Abs(x) == 1)
        {
            return x > 0 ? 0 : Math.PI;
        }

        return Atan2(AsinAcosRoot(x), x);
    }

    private static double AsinAcosRoot(double x) => Sqrt(Mul(Add(1, x), Sub(1, x)));

    /// <summary>Rounds <paramref name="value"/> to a 24-bit significand, ties to even, keeping its exponent.</summary>
    public static double Round24(double value) => Round24(value, 0);

    /// <summary>
    /// <c>(int)x</c> in code compiled with /QIfist: fistp to a 64-bit integer in the current rounding mode (nearest
    /// even), keeping the low 32 bits. NaN, infinities and values outside the 64-bit range give the integer
    /// indefinite, whose low half is 0.
    /// </summary>
    public static int Fist(double x)
    {
        if (!double.IsFinite(x) || Math.Abs(x) >= 9.2233720368547758e18)
        {
            return 0;
        }

        return unchecked((int)(long)Math.Round(x, MidpointRounding.ToEven));
    }

    /// <summary>
    /// <c>(int)x</c> without /QIfist (only CFrameTimer in retail): the CRT's _ftol truncates toward zero through a
    /// 64-bit fistp and keeps the low 32 bits.
    /// </summary>
    public static int Ftol(double x)
    {
        if (!double.IsFinite(x) || Math.Abs(x) >= 9.2233720368547758e18)
        {
            return 0;
        }

        return unchecked((int)(long)Math.Truncate(x));
    }

    /// <summary>
    /// Rounds the exact value <paramref name="hi"/> + <paramref name="lo"/> to 24 bits, where <paramref name="hi"/> is
    /// that value rounded to a double and |lo| is at most half a double ulp of it.
    /// </summary>
    private static double Round24(double hi, double lo)
    {
        if (!double.IsFinite(hi) || hi == 0)
        {
            return hi;
        }

        long bits = BitConverter.DoubleToInt64Bits(hi);
        long exponentField = (bits >> 52) & 0x7FF;
        if (exponentField == 0)
        {
            // A double subnormal: scale into the normal range, round there, scale back. The x87 register would hold
            // this value normalised, with a full 24-bit significand.
            return Math.ScaleB(Round24(Math.ScaleB(hi, 128), Math.ScaleB(lo, 128)), -128);
        }

        long dropped = bits & DroppedMask;
        long kept = bits & ~DroppedMask;
        bool roundUp;
        if (dropped > Half)
        {
            roundUp = true;
        }
        else if (dropped < Half)
        {
            roundUp = false;
        }
        else if (lo != 0)
        {
            // hi is exactly a midpoint between two 24-bit neighbours: the exact value lies beyond it in lo's
            // direction, towards the larger magnitude when lo has hi's sign.
            roundUp = (lo > 0) == (hi > 0);
        }
        else
        {
            roundUp = ((kept >> DroppedBits) & 1) != 0;
        }

        return BitConverter.Int64BitsToDouble(roundUp ? kept + (1L << DroppedBits) : kept);
    }
}
