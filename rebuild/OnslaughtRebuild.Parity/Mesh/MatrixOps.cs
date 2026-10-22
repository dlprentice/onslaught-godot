// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/maths.h (FMatrix operator+, operator- and operator*(float), row by row); the 16-byte
// FVector and 48-byte FMatrix memory layouts (maths.h: X, Y, Z and an unset fourth dword per row). Not in Stuart's
// release.

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
