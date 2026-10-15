// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/DXMeshVB.cpp (CDXMeshVB::Load 0054e160) and src/DXMeshVB.h (the CDXMeshVB layout:
// 0x128 bytes, mNumPieces +0x108, mStride +0x114). DXMeshVB.cpp is in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>One vertex-buffer piece: its counts, its 16-bit indices and its six texture indices (-1 for none).</summary>
public sealed record MeshVertexBufferPiece(int VbSize, int IbSize, int NumIndices, int NumVertices, int NumPrimitives,
    int Built, ushort[] Indices, int[] TextureIndices);

/// <summary>
/// A part's vertex buffers as stored: the 0x128-byte object (read whole, whatever the chunk's size), then per piece its
/// counts, index buffer, vertices (piece 0's; the others' chunks are skipped) and textures. Rendering only; kept so the
/// reader walks the file as retail does.
/// </summary>
public sealed class MeshVertexBuffer
{
    /// <summary>sizeof(CDXMeshVB).</summary>
    public const int RecordSize = 0x128;

    public byte[] Record { get; private set; } = [];

    public int NumPieces => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x108));

    public int Stride => BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x114));

    public IReadOnlyList<MeshVertexBufferPiece> Pieces { get; private set; } = [];

    /// <summary>Piece 0's vertices, raw (Stride bytes each).</summary>
    public byte[] Vertices { get; private set; } = [];

    /// <summary>CDXMeshVB::Load.</summary>
    internal static MeshVertexBuffer Load(ChunkReader c)
    {
        var vb = new MeshVertexBuffer();
        c.GetNext();
        c.GetNext();
        vb.Record = new byte[RecordSize];
        c.Read(vb.Record);

        var pieces = new List<MeshVertexBufferPiece>();
        Span<byte> s = stackalloc byte[2];
        for (int i = 0; i < vb.NumPieces; i++)
        {
            c.GetNext();
            int vbSize = c.ReadInt32();
            int ibSize = c.ReadInt32();
            int numIndices = c.ReadInt32();
            int numVertices = c.ReadInt32();
            int numPrimitives = c.ReadInt32();
            int built = c.ReadInt32();
            c.GetNext();
            var indices = new ushort[numIndices];
            for (int k = 0; k < numIndices; k++)
            {
                c.Read(s);
                indices[k] = BinaryPrimitives.ReadUInt16LittleEndian(s);
            }

            c.GetNext();
            if (i == 0)
            {
                vb.Vertices = new byte[vb.Stride * numVertices];
                c.Read(vb.Vertices);
            }
            else
            {
                vbSize = pieces[0].VbSize;
                numVertices = pieces[0].NumVertices;
                c.Skip();
            }

            c.GetNext();
            var textures = new int[6];
            for (int t = 0; t < 6; t++)
            {
                textures[t] = c.ReadInt32();
            }

            pieces.Add(new MeshVertexBufferPiece(vbSize, ibSize, numIndices, numVertices, numPrimitives, built, indices, textures));
        }

        vb.Pieces = pieces;
        return vb;
    }
}
