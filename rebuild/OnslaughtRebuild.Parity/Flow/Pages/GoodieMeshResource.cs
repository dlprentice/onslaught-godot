// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/FEPGoodies.cpp (CFEPGoodies::Deserialise 0045c870) and
// src/ResourceAccumulator.cpp (CResourceAccumulator::ReadResources 004d7200), in Stuart's release.

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Flow.Pages;

/// <summary>
/// The simulation-relevant mesh in a goodie's resource file. Texture records are counted and skipped: this reader does
/// not load pixels or render the viewer. Loading completion and the viewer's lifetime are separate inputs.
/// </summary>
public sealed record GoodieMeshResource(int Number, int TextureCount, MeshData Mesh)
{
    /// <summary>
    /// Read the GDIE/GDAT mesh form, using the existing mesh reader for its MESH chunk. Resource wrappers and each
    /// texture chunk are bounded and checked; unsupported image/film forms, wrong numbers and missing meshes fail.
    /// The supplied data reader resolves the mesh's named external file just as <see cref="MeshData.Deserialize"/> does.
    /// </summary>
    public static GoodieMeshResource Read(string fileName, byte[] bytes, int expectedNumber,
        Func<string, (string Name, byte[] Bytes)> meshFile)
    {
        ArgumentNullException.ThrowIfNull(meshFile);
        if (expectedNumber < 0) throw new ArgumentOutOfRangeException(nameof(expectedNumber));
        MemBuffer packed = MemBuffer.FromFile(fileName, bytes);
        byte[] unpacked = new byte[packed.Length];
        if (packed.Read(unpacked) != unpacked.Length) throw new InvalidDataException("truncated goodie resource");
        var c = new ChunkReader(MemBuffer.FromFile("goodie-unpacked", unpacked));
        uint fileEnd = (uint)c.File.Length;
        GoodieMeshResource? result = null;
        while (c.WhereAmI < fileEnd)
        {
            var (tag, end) = Next(c, fileEnd);
            if (tag != ChunkReader.MakeId("GDIE"))
            {
                c.Skip();
                continue;
            }

            if (result is not null) throw new InvalidDataException("multiple GDIE chunks");
            var (dataTag, dataEnd) = Next(c, end);
            if (dataTag != ChunkReader.MakeId("GDAT") || dataEnd != end || dataEnd - c.WhereAmI < 8)
                throw new InvalidDataException("invalid goodie data wrapper");
            int number = c.ReadInt32(), type = c.ReadInt32();
            if (number != expectedNumber || type != 1)
                throw new InvalidDataException("wrong goodie number or unsupported goodie type");

            var (meshDataTag, meshDataEnd) = Next(c, dataEnd);
            if (meshDataTag != ChunkReader.MakeId("MDAT") || meshDataEnd - c.WhereAmI != 4)
                throw new InvalidDataException("invalid mesh goodie descriptor");
            int count = c.ReadInt32();
            if (count <= 0 || count > (dataEnd - c.WhereAmI) / 8)
                throw new InvalidDataException("invalid goodie texture count");
            for (int i = 0; i < count; i++)
            {
                var (textureTag, _) = Next(c, dataEnd);
                if (textureTag != ChunkReader.MakeId("TXTR")) throw new InvalidDataException("missing texture descriptor");
                c.Skip();
            }

            var (meshTag, meshEnd) = Next(c, dataEnd);
            if (meshTag != ChunkReader.MakeId("MESH")) throw new InvalidDataException("missing goodie mesh");
            ValidateMeshHeader(unpacked, c.WhereAmI, meshEnd);
            MeshData mesh = MeshData.Deserialize(c, meshFile) ?? throw new InvalidDataException("goodie mesh was skipped");
            if (mesh.ReadPastEnd || c.WhereAmI != meshEnd || meshEnd != dataEnd)
                throw new InvalidDataException("truncated goodie mesh or incorrect wrapper end");
            result = new GoodieMeshResource(number, count, mesh);
        }
        return result ?? throw new InvalidDataException("missing GDIE chunk");
    }

    private static void ValidateMeshHeader(byte[] unpacked, uint start, uint meshEnd)
    {
        var check = new ChunkReader(MemBuffer.FromFile("goodie-unpacked", unpacked));
        check.File.Skip((int)start);
        var (wrapperTag, wrapperEnd) = Next(check, meshEnd);
        if (wrapperTag != ChunkReader.MakeId("PMSH") || wrapperEnd != meshEnd)
            throw new InvalidDataException("invalid goodie mesh wrapper");
        var (nameTag, nameEnd) = Next(check, wrapperEnd);
        if (nameTag != ChunkReader.MakeId("PMS2") || nameEnd - check.WhereAmI != 309)
            throw new InvalidDataException("invalid goodie mesh name header");
        Span<byte> name = stackalloc byte[300];
        if (!check.Read(name)) throw new InvalidDataException("truncated goodie mesh name");
        check.File.Skip(9); // resource id, optional flag, keep-buckets flag
        if (name[0] == 0)
        {
            var (bodyTag, bodyEnd) = Next(check, wrapperEnd);
            if (bodyTag != ChunkReader.MakeId("CMSH") || bodyEnd - check.WhereAmI != MeshData.RecordSize)
                throw new InvalidDataException("missing embedded goodie mesh record");
        }
    }

    private static (uint Tag, uint End) Next(ChunkReader c, uint parentEnd)
    {
        if (c.WhereAmI > parentEnd || parentEnd - c.WhereAmI < 8)
            throw new InvalidDataException("truncated goodie chunk header");
        uint tag = c.GetNext();
        if (c.Size > parentEnd - c.WhereAmI) throw new InvalidDataException("goodie chunk exceeds its wrapper");
        return (tag, c.WhereAmI + c.Size);
    }
}
