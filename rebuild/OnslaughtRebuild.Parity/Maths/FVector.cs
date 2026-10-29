// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/maths.h (FVector). Not in Stuart's release.

namespace OnslaughtRebuild.Parity.Maths;

/// <summary>
/// The game's three-float vector. Operations use the associations shown below; retail can use different associations
/// at individual inlined sites. Matching association in C# float and x87's 24-bit precision control does not guarantee
/// identical bits: x87 has a wider exponent range, and underflow, overflow and exceptional inputs need separate evidence.
/// </summary>
public struct FVector : IEquatable<FVector>
{
    public float X;
    public float Y;
    public float Z;

    public FVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static FVector operator +(FVector a, FVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static FVector operator -(FVector a, FVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static FVector operator -(FVector a) => new(-a.X, -a.Y, -a.Z);

    public static FVector operator *(FVector a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>operator^: the cross product, (Y*aZ - Z*aY, Z*aX - aZ*X, aY*X - Y*aX).</summary>
    public static FVector Cross(FVector v, FVector a) =>
        new((v.Y * a.Z) - (v.Z * a.Y), (v.Z * a.X) - (a.Z * v.X), (a.Y * v.X) - (v.Y * a.X));

    /// <summary>Magnitude: sqrtf((X*X) + (Y*Y) + (Z*Z)).</summary>
    public readonly float Magnitude() => MathF.Sqrt((X * X) + (Y * Y) + (Z * Z));

    /// <summary>MagnitudeSq: (X*X) + (Y*Y) + (Z*Z).</summary>
    public readonly float MagnitudeSq() => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>
    /// MagnitudeYZX: sqrtf(((Y*Y) + (Z*Z)) + (X*X)), the grouping retail's inlined Magnitude takes at the sites the native
    /// port's patches 0055, 0093-0098, 0111 and 0112 name (each read from pristine 74154bfa at its address). Retail adds Z*Z
    /// before Y*Y at some of them; a sum commutes, so that is the same value.
    /// </summary>
    public readonly float MagnitudeYZX() => MathF.Sqrt(((Y * Y) + (Z * Z)) + (X * X));

    /// <summary>MagnitudeXY: sqrtf((X*X) + (Y*Y)).</summary>
    public readonly float MagnitudeXY() => MathF.Sqrt((X * X) + (Y * Y));

    /// <summary>Normalise: divides by the magnitude through its reciprocal, unless the magnitude is zero.</summary>
    public void Normalise()
    {
        float m = MathF.Sqrt((X * X) + (Y * Y) + (Z * Z));
        if (m != 0)
        {
            m = 1.0f / m;
            X *= m;
            Y *= m;
            Z *= m;
        }
    }

    /// <summary>Normalise at a verified inline site which adds the Y and Z squares before X.</summary>
    public void NormaliseYZX()
    {
        float m = MagnitudeYZX();
        if (m != 0)
        {
            m = 1.0f / m;
            X *= m;
            Y *= m;
            Z *= m;
        }
    }

    public readonly bool Equals(FVector other) =>
        BitConverter.SingleToInt32Bits(X) == BitConverter.SingleToInt32Bits(other.X)
        && BitConverter.SingleToInt32Bits(Y) == BitConverter.SingleToInt32Bits(other.Y)
        && BitConverter.SingleToInt32Bits(Z) == BitConverter.SingleToInt32Bits(other.Z);

    public override readonly bool Equals(object? obj) => obj is FVector v && Equals(v);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override readonly string ToString() => $"({X:R}, {Y:R}, {Z:R})";
}
