// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/maths.h (FMatrix, CEulerAngles). Not in Stuart's release.

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

    /// <summary>
    /// operator*(FVector) as VC6 /O2 compiles the inline: the source sums each row's products X + Y + Z, but the compiled
    /// code evaluates (Z + Y) + X (a reassociation, not a commutation: the 24-bit roundings differ). Read from the
    /// pristine specimen at CThing::GetCentrePos (004f3b9c) and CUnit::GetEmitter (004fc60f); other inline sites are
    /// checked as they are ported (the per-function oracle), and a site that differs gets its own method.
    /// </summary>
    public readonly FVector Transform(FVector v) => new(
        ((Row0.Z * v.Z) + (Row0.Y * v.Y)) + (Row0.X * v.X),
        ((Row1.Z * v.Z) + (Row1.Y * v.Y)) + (Row1.X * v.X),
        ((Row2.Z * v.Z) + (Row2.Y * v.Y)) + (Row2.X * v.X));

    /// <summary>Transpose: the columns as rows.</summary>
    public readonly FMatrix Transpose() => new(
        new FVector(Row0.X, Row1.X, Row2.X),
        new FVector(Row0.Y, Row1.Y, Row2.Y),
        new FVector(Row0.Z, Row1.Z, Row2.Z));

    /// <summary>
    /// FMatrix(yaw, pitch, roll) out of line (004062d0, the constructor CBattleEngine::UpdateRotation and
    /// CBattleEngineWalkerPart::Forward call): cos(yaw) and sin(pitch) stay on the FPU stack at full precision; sin(yaw),
    /// cos(roll), sin(roll), cos(pitch) and sin(pitch) * cos(roll) are stored to floats; every product, sum and
    /// difference rounds to 24 bits.
    /// </summary>
    public static FMatrix MakeRotationOutOfLine(float yaw, float pitch, float roll)
    {
        double cy = X87.Cos(yaw);
        float sy = (float)X87.Sin(yaw);
        float cr = (float)X87.Cos(roll);
        float sr = (float)X87.Sin(roll);
        float cp = (float)X87.Cos(pitch);
        double sp = X87.Sin(pitch);

        double spsr = X87.Mul(sp, sr);
        float spcr = (float)X87.Mul(sp, cr);
        var m = new FMatrix();
        m.Row0.X = (float)X87.Sub(X87.Mul(cr, cy), X87.Mul(spsr, sy));
        m.Row0.Y = -OutOfLineFloatProduct(cp, sy);
        m.Row0.Z = (float)X87.Add(X87.Mul(spcr, sy), X87.Mul(sr, cy));
        m.Row1.X = (float)X87.Add(X87.Mul(spsr, cy), X87.Mul(cr, sy));
        m.Row1.Y = (float)X87.Mul(cp, cy);
        m.Row1.Z = (float)X87.Sub(X87.Mul(sr, sy), X87.Mul(spcr, cy));
        m.Row2.X = -(float)X87.Mul(cp, sr);
        m.Row2.Y = (float)sp;
        m.Row2.Z = OutOfLineFloatProduct(cp, cr);
        return m;
    }

    /// <summary>
    /// FMatrix::MakeRotation(SINT yaw, SINT pitch, SINT roll) (004f8140, in Unit.cpp): angles in 4096ths of a turn; the
    /// roll's matrix, then the pitch's times it, then the yaw's times that. With zero angles the -sin(0) entries leave
    /// signed zeros (Row0.Y is +0, so CEulerAngles of it has a -0 yaw).
    /// </summary>
    public static FMatrix MakeRotation(int yaw, int pitch, int roll)
    {
        const float Step = 6.28318530717958f / 4096;
        float r = roll * Step;
        float sr = (float)X87.Sin(r);
        var m = new FMatrix(new FVector((float)X87.Cos(r), 0.0f, sr), new FVector(0.0f, 1.0f, 0.0f),
            new FVector(-(float)X87.Sin(r), 0.0f, (float)X87.Cos(r)));
        float pa = pitch * Step;
        float cp = (float)X87.Cos(pa);
        float sp = (float)X87.Sin(pa);
        var p = new FMatrix(new FVector(1.0f, 0.0f, 0.0f), new FVector(0.0f, cp, -(float)X87.Sin(pa)),
            new FVector(0.0f, sp, (float)X87.Cos(pa)));
        m = p.Multiply(m);
        float yw = yaw * Step;
        p = new FMatrix(new FVector((float)X87.Cos(yw), -(float)X87.Sin(yw), 0.0f),
            new FVector((float)X87.Sin(yw), (float)X87.Cos(yw), 0.0f), new FVector(0.0f, 0.0f, 1.0f));
        return p.Multiply(m);
    }

    /// <summary>
    /// General matrix product. The historical default sums each cell left to right; a caller with a verified inlined or
    /// out-of-line retail order must use its own method. This default is not a claim about all retail call sites.
    /// </summary>
    public readonly FMatrix Multiply(FMatrix m) => MultiplyLeftToRight(m);

    /// <summary>
    /// A product with every cell summed left to right, (X + Y) + Z. Individual inlined retail products may use different
    /// orders and need their own methods; this is not a general claim about the decompilation's file-local helpers.
    /// </summary>
    public readonly FMatrix MultiplyLeftToRight(FMatrix m) => new(
        new FVector((Row0.X * m.Row0.X) + (Row0.Y * m.Row1.X) + (Row0.Z * m.Row2.X),
            (Row0.X * m.Row0.Y) + (Row0.Y * m.Row1.Y) + (Row0.Z * m.Row2.Y),
            (Row0.X * m.Row0.Z) + (Row0.Y * m.Row1.Z) + (Row0.Z * m.Row2.Z)),
        new FVector((Row1.X * m.Row0.X) + (Row1.Y * m.Row1.X) + (Row1.Z * m.Row2.X),
            (Row1.X * m.Row0.Y) + (Row1.Y * m.Row1.Y) + (Row1.Z * m.Row2.Y),
            (Row1.X * m.Row0.Z) + (Row1.Y * m.Row1.Z) + (Row1.Z * m.Row2.Z)),
        new FVector((Row2.X * m.Row0.X) + (Row2.Y * m.Row1.X) + (Row2.Z * m.Row2.X),
            (Row2.X * m.Row0.Y) + (Row2.Y * m.Row1.Y) + (Row2.Z * m.Row2.Y),
            (Row2.X * m.Row0.Z) + (Row2.Y * m.Row1.Z) + (Row2.Z * m.Row2.Z)));


    /// <summary>
    /// operator* as retail's out-of-line copy computes it (0040d320, CTree::UpdateFallingTree's call): this times
    /// <paramref name="m"/>, each element summing its three products in its own order (rows done 2, 1, 0). The
    /// intermediate results retain the x87 register's exponent range until each finished element is stored.
    /// </summary>
    public readonly FMatrix MultiplyOutOfLine(FMatrix m)
    {
        FVector r0 = Row0, r1 = Row1, r2 = Row2;
        if (BoundedRow(r0) && BoundedRow(r1) && BoundedRow(r2)
            && BoundedRow(m.Row0) && BoundedRow(m.Row1) && BoundedRow(m.Row2))
        {
            var p2 = new FVector(
                BoundedCell(r2.Z, m.Row2.X, r2.Y, m.Row1.X, r2.X, m.Row0.X),
                BoundedCell(r2.X, m.Row0.Y, r2.Z, m.Row2.Y, r2.Y, m.Row1.Y),
                BoundedCell(r2.Z, m.Row2.Z, r2.Y, m.Row1.Z, r2.X, m.Row0.Z));
            var p1 = new FVector(
                BoundedCell(r1.Z, m.Row2.X, r1.Y, m.Row1.X, r1.X, m.Row0.X),
                BoundedCell(r1.X, m.Row0.Y, r1.Z, m.Row2.Y, r1.Y, m.Row1.Y),
                BoundedCell(r1.X, m.Row0.Z, r1.Z, m.Row2.Z, r1.Y, m.Row1.Z));
            var p0 = new FVector(
                BoundedCell(r0.Y, m.Row1.X, r0.X, m.Row0.X, r0.Z, m.Row2.X),
                BoundedCell(r0.Z, m.Row2.Y, r0.Y, m.Row1.Y, r0.X, m.Row0.Y),
                BoundedCell(r0.Z, m.Row2.Z, r0.Y, m.Row1.Z, r0.X, m.Row0.Z));
            return new FMatrix(p0, p1, p2);
        }
        var c2 = new FVector(
            OutOfLineCell(r2.Z, m.Row2.X, r2.Y, m.Row1.X, r2.X, m.Row0.X),
            OutOfLineCell(r2.X, m.Row0.Y, r2.Z, m.Row2.Y, r2.Y, m.Row1.Y),
            OutOfLineCell(r2.Z, m.Row2.Z, r2.Y, m.Row1.Z, r2.X, m.Row0.Z));
        var c1 = new FVector(
            OutOfLineCell(r1.Z, m.Row2.X, r1.Y, m.Row1.X, r1.X, m.Row0.X),
            OutOfLineCell(r1.X, m.Row0.Y, r1.Z, m.Row2.Y, r1.Y, m.Row1.Y),
            OutOfLineCell(r1.X, m.Row0.Z, r1.Z, m.Row2.Z, r1.Y, m.Row1.Z));
        var c0 = new FVector(
            OutOfLineCell(r0.Y, m.Row1.X, r0.X, m.Row0.X, r0.Z, m.Row2.X),
            OutOfLineCell(r0.Z, m.Row2.Y, r0.Y, m.Row1.Y, r0.X, m.Row0.Y),
            OutOfLineCell(r0.Z, m.Row2.Z, r0.Y, m.Row1.Z, r0.X, m.Row0.Z));
        return new FMatrix(c0, c1, c2);
    }

    private static float OutOfLineCell(float a0, float b0, float a1, float b1, float a2, float b2) =>
        (float)X87.Add(X87.Add(X87.Mul(a0, b0), X87.Mul(a1, b1)), X87.Mul(a2, b2));

    private static bool BoundedRow(FVector r) =>
        BoundedPc24.Admitted(r.X) && BoundedPc24.Admitted(r.Y) && BoundedPc24.Admitted(r.Z);

    private static float BoundedCell(float a0, float b0, float a1, float b1, float a2, float b2) =>
        BoundedPc24.Store(BoundedPc24.Add(BoundedPc24.Add(BoundedPc24.Product(a0, b0),
            BoundedPc24.Product(a1, b1)), BoundedPc24.Product(a2, b2)));

    internal static float OutOfLineFloatProduct(float a, float b) => BoundedPc24.Admitted(a) && BoundedPc24.Admitted(b)
        ? BoundedPc24.Store(BoundedPc24.Product(a, b)) : (float)X87.Mul(a, b);

    /// <summary>operator+ (maths.h): row by row.</summary>
    public readonly FMatrix Add(FMatrix m) => new(Row0 + m.Row0, Row1 + m.Row1, Row2 + m.Row2);

    /// <summary>operator*(float) (maths.h): each row scaled.</summary>
    public readonly FMatrix Scale(float f) => new(Row0 * f, Row1 * f, Row2 * f);

    /// <summary>Inverse (maths.h): the cofactors, transposed, over the determinant.</summary>
    public readonly FMatrix Inverse()
    {
        var c0 = new FVector((Row1.Y * Row2.Z) - (Row1.Z * Row2.Y), -((Row1.X * Row2.Z) - (Row2.X * Row1.Z)), (Row1.X * Row2.Y) - (Row2.X * Row1.Y));
        var c1 = new FVector(-((Row0.Y * Row2.Z) - (Row0.Z * Row2.Y)), (Row0.X * Row2.Z) - (Row2.X * Row0.Z), -((Row0.X * Row2.Y) - (Row2.X * Row0.Y)));
        var c2 = new FVector((Row0.Y * Row1.Z) - (Row0.Z * Row1.Y), -((Row0.X * Row1.Z) - (Row1.X * Row0.Z)), (Row0.X * Row1.Y) - (Row1.X * Row0.Y));
        float det = (Row0.X * c0.X) + (Row0.Y * c0.Y) + (Row0.Z * c0.Z);
        var t = new FMatrix(new FVector(c0.X, c1.X, c2.X), new FVector(c0.Y, c1.Y, c2.Y), new FVector(c0.Z, c1.Z, c2.Z));
        return new FMatrix(
            new FVector(t.Row0.X / det, t.Row0.Y / det, t.Row0.Z / det),
            new FVector(t.Row1.X / det, t.Row1.Y / det, t.Row1.Z / det),
            new FVector(t.Row2.X / det, t.Row2.Y / det, t.Row2.Z / det));
    }

    /// <summary>
    /// MakeFromDirection (maths.h): columns side, top and back from a direction and an up vector (back = -dir,
    /// side = back ^ up, top = back ^ side, each normalised).
    /// </summary>
    public static FMatrix MakeFromDirection(FVector dir, FVector up)
    {
        FVector back = -dir;
        FVector side = FVector.Cross(back, up);
        FVector top = FVector.Cross(back, side);
        side.Normalise();
        top.Normalise();
        back.Normalise();
        return new FMatrix(new FVector(side.X, top.X, back.X), new FVector(side.Y, top.Y, back.Y), new FVector(side.Z, top.Z, back.Z));
    }

    /// <summary>Normalise (maths.h): rows 0 and 1 normalised, row 2 = row0 ^ row1 normalised, row 1 = row2 ^ row0.</summary>
    public readonly FMatrix NormaliseRows()
    {
        FVector r0 = Row0;
        FVector r1 = Row1;
        r0.Normalise();
        r1.Normalise();
        FVector r2 = FVector.Cross(r0, r1);
        r2.Normalise();
        r1 = FVector.Cross(r2, r0);
        return new FMatrix(r0, r1, r2);
    }

    /// <summary>MakeRotationPitchF (maths.h): about X, the float cosine and sine.</summary>
    public static FMatrix MakeRotationPitchF(float pitch)
    {
        float c = (float)Math.Cos(pitch);
        float s = (float)Math.Sin(pitch);
        return new FMatrix(new FVector(1, 0, 0), new FVector(0, c, -s), new FVector(0, s, c));
    }
}

/// <summary>CEulerAngles: yaw, pitch and roll.</summary>
public struct EulerAngles(float yaw, float pitch, float roll)
{
    public float Yaw = yaw;
    public float Pitch = pitch;
    public float Roll = roll;

    /// <summary>
    /// CEulerAngles(const FMatrix &amp;) (0044adb0, read from the pristine specimen 74154bfa): the pitch is asin(Row2.Y)
    /// through the CRT's _CIasin (0055dcb0), which keeps the caller's 24-bit precision control (0x5615a5 ORs 0x7f into
    /// the old control word), so 1 + x, 1 - x, their product and its square root each round to 24 bits before fpatan;
    /// the 64-bit result is compared with ±π and stored as a float. Yaw and roll are fpatan of the matrix entries.
    /// </summary>
    public static EulerAngles FromMatrix(FMatrix m)
    {
        double x = m.Row2.Y;
        double asin = x is > 1.0 or < -1.0 || double.IsNaN(x)
            ? double.NaN
            : X87.Atan2(x, X87.Sqrt(X87.Mul(X87.Add(1.0, x), X87.Sub(1.0, x))));
        var e = new EulerAngles { Pitch = (float)asin };
        if (asin < MathF.PI && asin > -MathF.PI)
        {
            e.Yaw = (float)X87.Atan2(-m.Row0.Y, m.Row1.Y);
            e.Roll = (float)X87.Atan2(-m.Row2.X, m.Row2.Z);
        }
        else
        {
            e.Yaw = 0;
            e.Roll = 0;
        }

        return e;
    }
}
