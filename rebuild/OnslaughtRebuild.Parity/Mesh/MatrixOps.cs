// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/maths.h and the cached-world products in onslaught-ce port patch 0027; the 16-byte
// FVector and 48-byte FMatrix memory layouts have an unset fourth dword per row. The maths source is not in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>The FMatrix operators the mesh code uses that Maths/FMatrix does not have yet.</summary>
public static class MatrixOps
{
    /// <summary>operator+: row by row.</summary>
    public static FMatrix Add(FMatrix a, FMatrix b) => new(a.Row0 + b.Row0, a.Row1 + b.Row1, a.Row2 + b.Row2);

    /// <summary>operator-: row by row.</summary>
    public static FMatrix Sub(FMatrix a, FMatrix b) => new(a.Row0 - b.Row0, a.Row1 - b.Row1, a.Row2 - b.Row2);

    /// <summary>operator*(float): each row scaled.</summary>
    public static FMatrix Scale(FMatrix a, float f) => new(a.Row0 * f, a.Row1 * f, a.Row2 * f);

    // Cached GetWorldTransforms' product: the nine inlined PC24 cell orders at 004b1402..004b153a, also in port
    // patch 0027. A generic FMatrix product has a different order at several cells.
    internal static FMatrix CachedFrameWorldOrientation(FMatrix world, FMatrix local)
    {
        FVector a = world.Row0, b = world.Row1, c = world.Row2;
        return new FMatrix(
            new FVector(((a.Z * local.Row2.X) + (a.Y * local.Row1.X)) + (a.X * local.Row0.X),
                ((a.Y * local.Row1.Y) + (a.Z * local.Row2.Y)) + (a.X * local.Row0.Y),
                ((a.X * local.Row0.Z) + (a.Z * local.Row2.Z)) + (a.Y * local.Row1.Z)),
            new FVector(((b.X * local.Row0.X) + (b.Z * local.Row2.X)) + (b.Y * local.Row1.X),
                ((b.X * local.Row0.Y) + (b.Y * local.Row1.Y)) + (b.Z * local.Row2.Y),
                ((b.Z * local.Row2.Z) + (b.X * local.Row0.Z)) + (b.Y * local.Row1.Z)),
            new FVector(((c.X * local.Row0.X) + (c.Z * local.Row2.X)) + (c.Y * local.Row1.X),
                ((c.X * local.Row0.Y) + (c.Y * local.Row1.Y)) + (c.Z * local.Row2.Y),
                ((c.Z * local.Row2.Z) + (c.X * local.Row0.Z)) + (c.Y * local.Row1.Z)));
    }

    // Cached EvaluatePose's separate product at 004b4f7b..004b50b9. It does not share the cached-frame orders.
    internal static FMatrix CachedPoseWorldOrientation(FMatrix world, FMatrix local)
    {
        FVector a = world.Row0, b = world.Row1, c = world.Row2;
        return new FMatrix(
            new FVector(((a.X * local.Row0.X) + (a.Y * local.Row1.X)) + (a.Z * local.Row2.X),
                ((a.Y * local.Row1.Y) + (a.X * local.Row0.Y)) + (a.Z * local.Row2.Y),
                ((a.Z * local.Row2.Z) + (a.Y * local.Row1.Z)) + (a.X * local.Row0.Z)),
            new FVector(((b.X * local.Row0.X) + (b.Y * local.Row1.X)) + (b.Z * local.Row2.X),
                ((b.X * local.Row0.Y) + (b.Y * local.Row1.Y)) + (b.Z * local.Row2.Y),
                ((b.Z * local.Row2.Z) + (b.X * local.Row0.Z)) + (b.Y * local.Row1.Z)),
            new FVector(((c.X * local.Row0.X) + (c.Y * local.Row1.X)) + (c.Z * local.Row2.X),
                ((c.X * local.Row0.Y) + (c.Y * local.Row1.Y)) + (c.Z * local.Row2.Y),
                ((c.Z * local.Row2.Z) + (c.X * local.Row0.Z)) + (c.Y * local.Row1.Z)));
    }

    // EvaluatePose's position product at 004b4ef2..004b4f3f, before adding the subject's world position.
    internal static FVector CachedPoseWorldPosition(FMatrix world, FVector local) => new(
        ((world.Row0.Y * local.Y) + (world.Row0.X * local.X)) + (world.Row0.Z * local.Z),
        ((world.Row1.X * local.X) + (world.Row1.Y * local.Y)) + (world.Row1.Z * local.Z),
        ((world.Row2.X * local.X) + (world.Row2.Y * local.Y)) + (world.Row2.Z * local.Z));
}

/// <summary>Vectors and matrices as the game stores them: 16 bytes per vector (the fourth dword unused).</summary>
internal static class MeshBytes
{
    public static FVector Vector(ReadOnlySpan<byte> b, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(b[at..]),
        BinaryPrimitives.ReadSingleLittleEndian(b[(at + 4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(b[(at + 8)..]));

    public static FMatrix Matrix(ReadOnlySpan<byte> b, int at) => new(Vector(b, at), Vector(b, at + 16), Vector(b, at + 32));
}
