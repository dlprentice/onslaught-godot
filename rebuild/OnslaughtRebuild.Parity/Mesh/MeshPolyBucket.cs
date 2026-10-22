// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/PolyBucket.cpp (CPolyBucket::Load 004d59f0, CPolyBucketIndexList::ReadBorrowed) and its
// header (the CPolyBucket layout: mWidth +0x68, mHeight +0x6c). Not in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>
/// A part's polygon bucket (collision search grid) as stored: the object record, a cell table of triangle index lists,
/// the packed triangles and the packed (16-bit) vertices. Kept as read for the collision port.
/// </summary>
public sealed partial class MeshPolyBucket
{
    public byte[] Record { get; private set; } = [];

    public int Width => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x68));

    public int Height => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x6c));

    /// <summary>Each cell's triangle indices, or null for an empty cell (rows of Width cells).</summary>
    public short[]?[] Cells { get; private set; } = [];

    /// <summary>Each triangle: its Z mask, three vertex indices and its search stamp.</summary>
    public (uint ZMask, ushort V0, ushort V1, ushort V2, ushort SearchStamp)[] Triangles { get; private set; } = [];

    /// <summary>The packed vertices (X, Y, Z as 16-bit values).</summary>
    public (short X, short Y, short Z)[] Vertices { get; private set; } = [];

    /// <summary>CPolyBucket::Load.</summary>
    internal static MeshPolyBucket Load(ChunkReader c)
    {
        var b = new MeshPolyBucket();
        c.GetNext();
        c.GetNext();
        b.Record = new byte[c.Size];
        c.Read(b.Record);

        int cellCount = b.Height * b.Width;
        b.Cells = new short[]?[Math.Max(cellCount, 0)];
        c.GetNext();
        c.ReadInt32(); // the number of lists
        c.ReadInt32(); // the number of indices
        for (int i = 0; i < cellCount; i++)
        {
            c.GetNext();
            if (c.ReadInt32() == 1)
            {
                c.GetNext();
                c.GetNext();
                int count = c.ReadInt32();
                var list = new short[count];
                for (int k = 0; k < count; k++)
                {
                    list[k] = ReadInt16(c);
                }

                b.Cells[i] = list;
            }
        }

        c.GetNext();
        int triangles = c.ReadInt32();
        c.GetNext();
        b.Triangles = new (uint, ushort, ushort, ushort, ushort)[triangles];
        for (int j = 0; j < triangles; j++)
        {
            c.GetNext();
            uint mask = (uint)c.ReadInt32();
            ushort v0 = (ushort)ReadInt16(c);
            ushort v1 = (ushort)ReadInt16(c);
            ushort v2 = (ushort)ReadInt16(c);
            ushort stamp = (ushort)ReadInt16(c);
            b.Triangles[j] = (mask, v0, v1, v2, stamp);
        }

        c.GetNext();
        int vertices = c.ReadInt32();
        b.Vertices = new (short, short, short)[vertices];
        for (int k = 0; k < vertices; k++)
        {
            c.GetNext();
            b.Vertices[k] = (ReadInt16(c), ReadInt16(c), ReadInt16(c));
        }

        return b;
    }

    private static short ReadInt16(ChunkReader c)
    {
        Span<byte> s = stackalloc byte[2];
        c.Read(s);
        return BinaryPrimitives.ReadInt16LittleEndian(s);
    }
}
