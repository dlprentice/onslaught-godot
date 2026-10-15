// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/maths.h (FMatrix, CEulerAngles). Not in Stuart's release.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Maths;

/// <summary>The game's 3x3 matrix: three row vectors.</summary>
public struct FMatrix
{
    public FVector Row0;
    public FVector Row1;
    public FVector Row2;

    public FMatrix(FVector a, FVector b, FVector c)
    {
        Row0 = a;
        Row1 = b;
        Row2 = c;
    }

    public static FMatrix Identity => new(new FVector(1, 0, 0), new FVector(0, 1, 0), new FVector(0, 0, 1));

    /// <summary>
    /// FMatrix(yaw, pitch, roll): MakeRotationF, as CComplexThing::Init's inline copy computes it (004f4008..004f40c5).
    /// fcos and fsin leave 64-bit results; that copy stores cos(yaw), sin(yaw), cos(roll) and cos(pitch) to floats and
    /// keeps sin(roll) and sin(pitch) on the FPU stack, so products with those two see the unrounded values. Every
    /// product, sum and difference rounds to 24 bits. Other inline copies may keep different values in registers
    /// (CActor::Init 00401255 is not checked yet): a call site that differs gets its own sequence.
    /// </summary>
    public static FMatrix FromYawPitchRoll(float yaw, float pitch, float roll)
    {
        float cy = (float)X87.Cos(yaw);
        float sy = (float)X87.Sin(yaw);
        float cr = (float)X87.Cos(roll);
        double sr = X87.Sin(roll);
        float cp = (float)X87.Cos(pitch);
        double sp = X87.Sin(pitch);

        double spsr = X87.Mul(sp, sr);
        double spcr = X87.Mul(sp, cr);
        var m = new FMatrix();
        m.Row0.X = (float)X87.Sub(X87.Mul(cr, cy), X87.Mul(spsr, sy));
        m.Row0.Y = -(cp * sy);
        m.Row0.Z = (float)X87.Add(X87.Mul(spcr, sy), X87.Mul(sr, cy));
        m.Row1.X = (float)X87.Add(X87.Mul(spsr, cy), X87.Mul(cr, sy));
        m.Row1.Y = cp * cy;
        m.Row1.Z = (float)X87.Sub(X87.Mul(sr, sy), X87.Mul(spcr, cy));
        m.Row2.X = -(float)X87.Mul(cp, sr);
        m.Row2.Y = (float)sp;
        m.Row2.Z = cp * cr;
        return m;
    }

    /// <summary>operator*(FVector): each component the row's bracketed products summed left to right.</summary>
    public readonly FVector Transform(FVector v) => new(
        (Row0.X * v.X) + (Row0.Y * v.Y) + (Row0.Z * v.Z),
        (Row1.X * v.X) + (Row1.Y * v.Y) + (Row1.Z * v.Z),
        (Row2.X * v.X) + (Row2.Y * v.Y) + (Row2.Z * v.Z));

    /// <summary>Transpose: the columns as rows.</summary>
    public readonly FMatrix Transpose() => new(
        new FVector(Row0.X, Row1.X, Row2.X),
        new FVector(Row0.Y, Row1.Y, Row2.Y),
        new FVector(Row0.Z, Row1.Z, Row2.Z));

    /// <summary>operator*(FMatrix).</summary>
    public readonly FMatrix Multiply(FMatrix m) => new(
        new FVector((Row0.X * m.Row0.X) + (Row0.Y * m.Row1.X) + (Row0.Z * m.Row2.X),
            (Row0.X * m.Row0.Y) + (Row0.Y * m.Row1.Y) + (Row0.Z * m.Row2.Y),
            (Row0.X * m.Row0.Z) + (Row0.Y * m.Row1.Z) + (Row0.Z * m.Row2.Z)),
        new FVector((Row1.X * m.Row0.X) + (Row1.Y * m.Row1.X) + (Row1.Z * m.Row2.X),
            (Row1.X * m.Row0.Y) + (Row1.Y * m.Row1.Y) + (Row1.Z * m.Row2.Y),
            (Row1.X * m.Row0.Z) + (Row1.Y * m.Row1.Z) + (Row1.Z * m.Row2.Z)),
        new FVector((Row2.X * m.Row0.X) + (Row2.Y * m.Row1.X) + (Row2.Z * m.Row2.X),
            (Row2.X * m.Row0.Y) + (Row2.Y * m.Row1.Y) + (Row2.Z * m.Row2.Y),
            (Row2.X * m.Row0.Z) + (Row2.Y * m.Row1.Z) + (Row2.Z * m.Row2.Z)));
}

/// <summary>CEulerAngles: yaw, pitch and roll.</summary>
public struct EulerAngles(float yaw, float pitch, float roll)
{
    public float Yaw = yaw;
    public float Pitch = pitch;
    public float Roll = roll;
}
