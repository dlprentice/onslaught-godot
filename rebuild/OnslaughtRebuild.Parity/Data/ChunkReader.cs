// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/chunker.{h,cpp} (CChunkReader: GetNext 00423910, Read 00423960, Skip 00423990).

using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Data;

/// <summary>
/// The game's chunk reader: each chunk is a 32-bit tag and a 32-bit size, then that many bytes. Reads count against the
/// current chunk so Skip can move past whatever was not read.
/// </summary>
public sealed class ChunkReader
{
    private uint _readSinceChunk;

    public ChunkReader(MemBuffer file)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
    }

    public MemBuffer File { get; }

    /// <summary>The current chunk's size (Size).</summary>
    public uint Size { get; private set; }

    /// <summary>MKID: the tag of a four-character name as the chunk files store it (first character in the low byte).</summary>
    public static uint MakeId(string name) =>
        name.Length == 4
            ? (uint)(name[0] | (name[1] << 8) | (name[2] << 16) | (name[3] << 24))
            : throw new ArgumentException("A chunk id has four characters.", nameof(name));

    /// <summary>GetNext: the next chunk's tag, or 0 when the file ends.</summary>
    public uint GetNext()
    {
        _readSinceChunk = 0;
        Span<byte> b = stackalloc byte[4];
        if (File.Read(b) < 4)
        {
            return 0;
        }

        uint tag = BinaryPrimitives.ReadUInt32LittleEndian(b);
        if (File.Read(b) < 4)
        {
            return 0;
        }

        Size = BinaryPrimitives.ReadUInt32LittleEndian(b);
        return tag;
    }

    /// <summary>Read: reads <paramref name="destination"/>.Length bytes of the current chunk; true when all arrived.</summary>
    public bool Read(Span<byte> destination)
    {
        _readSinceChunk += (uint)destination.Length;
        return File.Read(destination) == destination.Length;
    }

    public int ReadInt32()
    {
        Span<byte> b = stackalloc byte[4];
        Read(b);
        return BinaryPrimitives.ReadInt32LittleEndian(b);
    }

    public float ReadSingle()
    {
        Span<byte> b = stackalloc byte[4];
        Read(b);
        return BinaryPrimitives.ReadSingleLittleEndian(b);
    }

    /// <summary>Skip: moves past the rest of the current chunk.</summary>
    public int Skip()
    {
        uint skip = Size - _readSinceChunk;
        _readSinceChunk = Size;
        return File.Skip((int)skip);
    }

    /// <summary>WhereAmI: the buffer's position.</summary>
    public uint WhereAmI => File.Position;
}
