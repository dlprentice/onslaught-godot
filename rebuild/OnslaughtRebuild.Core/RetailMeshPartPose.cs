// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Normal-cache, single-frame mesh pose arithmetic. Each operation rounds to
/// 24 significand bits, ties to even; float stores are separate. This is the
/// device-creation precision intent, pending a live control-word measurement.
/// Does not select frames, refresh caches, run controllers or model early ticks.
/// </summary>
public static class RetailMeshPartPose
{
    // Pristine 74154bfa…7750; cround-hit-damage-path-2026-08-10.md owns evidence.
    // Hierarchy [4b57ef,4b59af): SHA256 6c58928375da8cec09558da332bded657c28eb1d37a53b4ecaaf6af8c41c0d7e.
    // Owner [4b4ef2,4b50bb): SHA256 dbc94feb51aeda7dffdaabb87bb69215c1f0e6b3b5b0d9a2969b7f410e993caf.

    /// <summary>
    /// Both interpolation endpoints select the same finite hierarchy frame,
    /// with an initial zero interpolation fraction. Addition of positive zero
    /// preserves finite values but normalizes a negative-zero component.
    /// </summary>
    public static RetailUnitAttachmentPose InterpolateSingleFrame(RetailUnitAttachmentPose frame)
    {
        var p = frame.PositionFloatBits;
        var b = frame.BasisFloatBits;
        return new(new(Initial(p.X), Initial(p.Y), Initial(p.Z)), new(
            Initial(b.Row0X), Initial(b.Row0Y), Initial(b.Row0Z),
            Initial(b.Row1X), Initial(b.Row1Y), Initial(b.Row1Z),
            Initial(b.Row2X), Initial(b.Row2Y), Initial(b.Row2Z)));
    }

    /// <summary>Compose an interpolated local pose with its parent model pose.</summary>
    public static RetailUnitAttachmentPose ComposeHierarchy(
        RetailUnitAttachmentPose parent, RetailUnitAttachmentPose local)
    {
        ReadOnlySpan<double> a = Components(parent.BasisFloatBits);
        ReadOnlySpan<double> b = Components(local.BasisFloatBits);
        return new(Position(parent, local.PositionFloatBits, a, 0, 2, 1, 1, 2, 0), new(
            Product(a, b, 0, 0, 0, 2, 1), Product(a, b, 0, 1, 2, 1, 0), Product(a, b, 0, 2, 2, 1, 0),
            Product(a, b, 1, 0, 1, 2, 0), Product(a, b, 1, 1, 1, 0, 2), Product(a, b, 1, 2, 1, 0, 2),
            Product(a, b, 2, 0, 0, 1, 2), Product(a, b, 2, 1, 0, 1, 2), Product(a, b, 2, 2, 0, 1, 2)));
    }

    /// <summary>Apply the owner pose to a cached model pose, after cache lookup.</summary>
    public static RetailUnitAttachmentPose ApplyOwner(
        RetailUnitAttachmentPose owner, RetailUnitAttachmentPose cached)
    {
        ReadOnlySpan<double> a = Components(owner.BasisFloatBits);
        ReadOnlySpan<double> b = Components(cached.BasisFloatBits);
        return new(Position(owner, cached.PositionFloatBits, a, 1, 0, 2, 0, 1, 2), new(
            Product(a, b, 0, 0, 0, 1, 2), Product(a, b, 0, 1, 1, 0, 2), Product(a, b, 0, 2, 2, 1, 0),
            Product(a, b, 1, 0, 0, 1, 2), Product(a, b, 1, 1, 0, 1, 2), Product(a, b, 1, 2, 2, 0, 1),
            Product(a, b, 2, 0, 0, 1, 2), Product(a, b, 2, 1, 0, 1, 2), Product(a, b, 2, 2, 2, 0, 1)));
    }

    /// <summary>
    /// Convert a sphere's current centre/displacement through an already selected
    /// part pose, in common retail world coordinates and units. The collision
    /// cache uses the transpose, not a general inverse.
    /// Cache selection and refresh remain the caller's responsibility.
    /// </summary>
    public static (Level100FloatVector3Bits Position, Level100FloatVector3Bits Displacement)
        ToLocalSphereQuery(RetailUnitAttachmentPose part, Level100FloatVector3Bits currentCenter,
            Level100FloatVector3Bits displacement)
    {
        // Pristine [4ac8e5,4ac9ce): a8aa3320d32ff7b32536ed1c6511d048abac2d4f89a384196583f46a222dc4af.
        // Initial centre-minus-displacement stores precede the part subtraction.
        double dx = Read(displacement.X), dy = Read(displacement.Y), dz = Read(displacement.Z);
        double bx = (float)RetailFloat24.Subtract(Read(currentCenter.X), dx);
        double by = (float)RetailFloat24.Subtract(Read(currentCenter.Y), dy);
        double bz = (float)RetailFloat24.Subtract(Read(currentCenter.Z), dz);
        double x = (float)RetailFloat24.Subtract(bx, Read(part.PositionFloatBits.X));
        double y = (float)RetailFloat24.Subtract(by, Read(part.PositionFloatBits.Y));
        double wideZ = RetailFloat24.Subtract(bz, Read(part.PositionFloatBits.Z));
        double z = (float)wideZ;
        ReadOnlySpan<double> a = Components(part.BasisFloatBits);
        return (new(TransposeDot(a, 0, x, y, z), TransposeDot(a, 1, x, y, z),
                TransposeDot(a, 2, x, y, wideZ)),
            new(TransposeDot(a, 0, dx, dy, dz), TransposeDot(a, 1, dx, dy, dz),
                TransposeDot(a, 2, dx, dy, dz)));
    }

    private static int TransposeDot(ReadOnlySpan<double> a, int column,
        double x, double y, double z) => Store(RetailFloat24.Add(
            RetailFloat24.Add(RetailFloat24.Multiply(z, a[6 + column]),
                RetailFloat24.Multiply(y, a[3 + column])),
            RetailFloat24.Multiply(x, a[column])));

    private static Level100FloatVector3Bits Position(RetailUnitAttachmentPose parent,
        Level100FloatVector3Bits local, ReadOnlySpan<double> a,
        int x0, int x1, int x2, int y0, int y1, int y2)
    {
        ReadOnlySpan<double> p = [Read(local.X), Read(local.Y), Read(local.Z)];
        double x = Dot(a, p, 0, x0, x1, x2);
        double y = Dot(a, p, 1, y0, y1, y2);
        double z = Dot(a, p, 2, 0, 1, 2);
        return new(Store(RetailFloat24.Add(x, Read(parent.PositionFloatBits.X))),
            Store(RetailFloat24.Add((float)y, Read(parent.PositionFloatBits.Y))),
            Store(RetailFloat24.Add((float)z, Read(parent.PositionFloatBits.Z))));
    }

    private static double Dot(ReadOnlySpan<double> a, ReadOnlySpan<double> p,
        int row, int k0, int k1, int k2) =>
        RetailFloat24.Add(RetailFloat24.Add(
            RetailFloat24.Multiply(a[row * 3 + k0], p[k0]),
            RetailFloat24.Multiply(a[row * 3 + k1], p[k1])),
            RetailFloat24.Multiply(a[row * 3 + k2], p[k2]));

    private static int Product(ReadOnlySpan<double> a, ReadOnlySpan<double> b,
        int row, int column, int k0, int k1, int k2) => Store(
            RetailFloat24.Add(RetailFloat24.Add(
                RetailFloat24.Multiply(a[row * 3 + k0], b[k0 * 3 + column]),
                RetailFloat24.Multiply(a[row * 3 + k1], b[k1 * 3 + column])),
                RetailFloat24.Multiply(a[row * 3 + k2], b[k2 * 3 + column])));

    private static double[] Components(Level100FloatBasis3Bits b) =>
    [
        Read(b.Row0X), Read(b.Row0Y), Read(b.Row0Z),
        Read(b.Row1X), Read(b.Row1Y), Read(b.Row1Z),
        Read(b.Row2X), Read(b.Row2Y), Read(b.Row2Z)
    ];

    private static int Initial(int bits) => Store(RetailFloat24.Add(Read(bits), 0.0));
    private static int Store(double value) => BitConverter.SingleToInt32Bits((float)value);
    private static double Read(int bits)
    {
        float value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(bits), "Pose input must be finite.");
        return value;
    }
}
