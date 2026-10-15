// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>Meaningful XYZ/nine basis words; no claim about temporary padding.</summary>
public readonly record struct RetailUnitAttachmentPose(
    Level100FloatVector3Bits PositionFloatBits,
    Level100FloatBasis3Bits BasisFloatBits)
{
    /// <summary>
    /// The eligible local-cache arm of Unit 0x004fc4e0, with matrix product
    /// 0x0040d320. Uses 53-bit intermediates and explicit float32 stores.
    /// This does not implement cache lookup, animation or render interpolation.
    /// </summary>
    /// <remarks>
    /// Static specimen and instruction pins live in the existing
    /// reverse-engineering/binary-analysis/functions/Unit.cpp/CUnit__UpdateTransform.md.
    /// Double models the declared 53-bit/RN arithmetic contract, not every
    /// possible retail caller's x87 control word. World110's four inputs also
    /// have an independent native x87 arithmetic check; no game run is implied.
    /// </remarks>
    public static RetailUnitAttachmentPose Transform(
        RetailUnitAttachmentPose parent, RetailUnitAttachmentPose local)
    {
        ReadOnlySpan<double> a = Components(parent.BasisFloatBits);
        ReadOnlySpan<double> b = Components(local.BasisFloatBits);
        Level100FloatVector3Bits position = TransformPosition(parent, local.PositionFloatBits);
        return new(position, new(
            Product(a, b, 0, 0, 1, 0, 2), Product(a, b, 0, 1, 2, 1, 0), Product(a, b, 0, 2, 2, 1, 0),
            Product(a, b, 1, 0, 2, 1, 0), Product(a, b, 1, 1, 0, 2, 1), Product(a, b, 1, 2, 0, 2, 1),
            Product(a, b, 2, 0, 2, 1, 0), Product(a, b, 2, 1, 0, 2, 1), Product(a, b, 2, 2, 2, 1, 0)));
    }

    /// <summary>
    /// Point transform shared by Unit attachment and Thing centre 0x004f3ac0.
    /// Both keep the X dot wide and spill Y/Z dots before translation.
    /// </summary>
    internal static Level100FloatVector3Bits TransformPosition(
        RetailUnitAttachmentPose parent, Level100FloatVector3Bits local)
    {
        ReadOnlySpan<double> a = Components(parent.BasisFloatBits);
        ReadOnlySpan<double> p = [Read(local.X), Read(local.Y), Read(local.Z)];
        double x = PointDot(a, p, 0);
        double y = PointDot(a, p, 1);
        double z = PointDot(a, p, 2);
        return new(
            Store(x + Read(parent.PositionFloatBits.X)),
            Store((double)(float)y + Read(parent.PositionFloatBits.Y)),
            Store((double)(float)z + Read(parent.PositionFloatBits.Z)));
    }

    /// <summary>
    /// Finite interior branch of 0x0044adb0 used by the four World110 turret
    /// initializers. The called 0x0055dccd arithmetic computes asin, despite
    /// the retained Acos label. Does not clamp an invalid matrix into range.
    /// </summary>
    public Level100FloatVector3Bits ToComponentEuler()
    {
        ReadOnlySpan<double> matrix = Components(BasisFloatBits);
        double sine = matrix[7];
        if (sine <= -1.0 || sine >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(BasisFloatBits),
                "Component Euler conversion requires the admitted finite interior asin branch.");
        }

        // Preserve the helper's product and sqrt before atan2. The four
        // materialized inputs are checked against native FPATAN float stores;
        // arbitrary matrices are not a general x87 transcendental-parity claim.
        double pitch = Math.Atan2(sine, Math.Sqrt((1.0 + sine) * (1.0 - sine)));
        return new(
            Store(Math.Atan2(-matrix[1], matrix[4])),
            Store(pitch),
            Store(Math.Atan2(-matrix[6], matrix[8])));
    }

    private static double[] Components(Level100FloatBasis3Bits value) =>
        [Read(value.Row0X), Read(value.Row0Y), Read(value.Row0Z),
         Read(value.Row1X), Read(value.Row1Y), Read(value.Row1Z),
         Read(value.Row2X), Read(value.Row2Y), Read(value.Row2Z)];

    private static double PointDot(ReadOnlySpan<double> a, ReadOnlySpan<double> p, int row) =>
        (a[row * 3 + 2] * p[2] + a[row * 3 + 1] * p[1]) + a[row * 3] * p[0];

    private static int Product(ReadOnlySpan<double> a, ReadOnlySpan<double> b,
        int row, int column, int k0, int k1, int k2) => Store(
            (a[row * 3 + k0] * b[k0 * 3 + column] + a[row * 3 + k1] * b[k1 * 3 + column])
            + a[row * 3 + k2] * b[k2 * 3 + column]);

    private static double Read(int bits)
    {
        float value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(bits), "Attachment input must be finite.");
        }
        return value;
    }

    private static int Store(double value) => BitConverter.SingleToInt32Bits((float)value);
}
