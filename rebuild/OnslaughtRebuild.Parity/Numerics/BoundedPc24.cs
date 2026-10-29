// SPDX-License-Identifier: GPL-3.0-only

using System.Numerics;
using System.Runtime.CompilerServices;

namespace OnslaughtRebuild.Parity.Numerics;

/// <summary>
/// Nearest-even PC24 products and sums for float inputs whose nonzero magnitudes are within [2^-32, 2^32].
/// Uses only integer arithmetic and raw float bits; it does not read or depend on the host's rounding mode.
/// A nonzero rounded product is a multiple of 2^-87. Adding up to three products preserves that lattice and
/// stays below 2^66, so a nonzero result is always a normal finite binary32 value.
/// </summary>
internal static class BoundedPc24
{
    private const uint Sign = 0x80000000u;

    internal readonly struct Value(uint magnitude, int exponent, bool negative)
    {
        // Nonzero values have a 24-bit normalized magnitude and represent ±magnitude * 2^exponent.
        internal readonly uint Magnitude = magnitude;
        internal readonly int Exponent = exponent;
        internal readonly bool Negative = negative;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Admitted(float value)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value) & ~Sign;
        return bits == 0 || (bits >= 0x2f800000u && bits <= 0x4f800000u);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Value Product(float left, float right)
    {
        // Both inputs must have passed Admitted; this helper is not a general binary32 multiplication.
        uint a = BitConverter.SingleToUInt32Bits(left), b = BitConverter.SingleToUInt32Bits(right);
        bool negative = ((a ^ b) & Sign) != 0;
        if ((a & ~Sign) == 0 || (b & ~Sign) == 0) return new Value(0, 0, negative);
        uint ma = (a & 0x007fffffu) | 0x00800000u;
        uint mb = (b & 0x007fffffu) | 0x00800000u;
        int exponent = (int)((a >> 23) & 0xff) + (int)((b >> 23) & 0xff) - 300;
        return Round((ulong)ma * mb, exponent, negative);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Value Add(Value a, Value b)
    {
        if (a.Magnitude == 0 && b.Magnitude == 0) return new Value(0, 0, a.Negative && b.Negative);
        if (a.Magnitude == 0) return b;
        if (b.Magnitude == 0) return a;
        Value hi = a.Exponent >= b.Exponent ? a : b;
        Value lo = a.Exponent >= b.Exponent ? b : a;
        int shift = hi.Exponent - lo.Exponent;
        // With this gap the smaller term is strictly below half the closest rounding interval, including
        // immediately below a power of two. The larger term is unchanged under nearest-even rounding.
        if (shift > 32) return hi;
        long x = (long)((ulong)hi.Magnitude << shift);
        long y = lo.Magnitude;
        long sum = (hi.Negative ? -x : x) + (lo.Negative ? -y : y);
        if (sum == 0) return new Value(0, 0, false);
        return Round((ulong)(sum < 0 ? -sum : sum), lo.Exponent, sum < 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Store(Value value)
    {
        uint sign = value.Negative ? Sign : 0;
        if (value.Magnitude == 0) return BitConverter.UInt32BitsToSingle(sign);
        int exponentField = value.Exponent + 150;
        // All callers use products or sums of three admitted operands. Any nonzero result is normal finite.
        if (exponentField is <= 0 or >= 255) throw new InvalidOperationException("PC24 result outside bounded domain");
        uint bits = sign | ((uint)exponentField << 23) | (value.Magnitude & 0x007fffffu);
        return BitConverter.UInt32BitsToSingle(bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Value Round(ulong magnitude, int exponent, bool negative)
    {
        int shift = BitOperations.Log2(magnitude) - 23;
        if (shift > 0)
        {
            ulong kept = magnitude >> shift;
            ulong dropped = magnitude & ((1UL << shift) - 1);
            ulong half = 1UL << (shift - 1);
            if (dropped > half || (dropped == half && (kept & 1) != 0)) kept++;
            exponent += shift;
            if (kept == (1UL << 24)) { kept >>= 1; exponent++; }
            return new Value((uint)kept, exponent, negative);
        }
        return new Value((uint)(magnitude << -shift), exponent + shift, negative);
    }
}
