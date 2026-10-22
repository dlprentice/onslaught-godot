// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/DXMemBuffer.{h,cpp} (CDXMemBuffer: InitFromFile 00547ec0, Skip 005482d0, Read 00548570,
// ReadString 00548820, EndOfFile 00548d30).

using System.Buffers.Binary;
using System.IO.Compression;

namespace OnslaughtRebuild.Parity.Data;

/// <summary>
/// The game's read buffer over one data file. Files ending ".aya" are packed: a run of blocks, each a 32-bit compressed
/// length followed by a zlib stream. Retail inflates them a megabyte at a time; the bytes it hands out, and where reads,
/// skips and the end-of-file flag land, depend only on the whole decompressed stream, so this holds it in one array.
/// </summary>
public sealed class MemBuffer
{
    /// <summary>gPackBuffer's size (retail 005481a4): the most one packed block may inflate to.</summary>
    public const int PackBufferSize = 0x102927;

    private readonly byte[] _data;
    private int _ptr;

    private MemBuffer(string fileName, byte[] data)
    {
        FileName = fileName;
        _data = data;
    }

    /// <summary>mFileName as passed to InitFromFile.</summary>
    public string FileName { get; }

    /// <summary>mPos: bytes read or skipped so far.</summary>
    public uint Position { get; private set; }

    /// <summary>mEOF: a read or skip ran past the end of the file.</summary>
    public bool EndOfFile { get; private set; }

    /// <summary>The decompressed length of the file.</summary>
    public int Length => _data.Length;

    /// <summary>
    /// InitFromFile: <paramref name="fileBytes"/> are the file's bytes as stored on disk; packed when
    /// <paramref name="fileName"/> ends in ".aya" (any case).
    /// </summary>
    public static MemBuffer FromFile(string fileName, byte[] fileBytes)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(fileBytes);
        return new MemBuffer(fileName, IsPacked(fileName) ? Inflate(fileName, fileBytes) : fileBytes);
    }

    /// <summary>IsPacked: the name ends in ".aya", compared without case.</summary>
    public static bool IsPacked(string name) =>
        name.Length >= 4 && name.EndsWith(".aya", StringComparison.OrdinalIgnoreCase);

    /// <summary>Read: copies up to <paramref name="destination"/>.Length bytes and returns how many it copied.</summary>
    public int Read(Span<byte> destination)
    {
        int size = destination.Length;
        if (_ptr + size > _data.Length)
        {
            EndOfFile = true;
            size = _data.Length - _ptr;
        }

        if (size <= 0)
        {
            return 0;
        }

        _data.AsSpan(_ptr, size).CopyTo(destination);
        _ptr += size;
        Position += (uint)size;
        return size;
    }

    /// <summary>Skip: moves on up to <paramref name="size"/> bytes and returns how many it skipped.</summary>
    public int Skip(int size)
    {
        if (size == 0)
        {
            return 0;
        }

        if (_ptr + size > _data.Length)
        {
            EndOfFile = true;
            size = _data.Length - _ptr;
        }

        if (size <= 0)
        {
            return 0;
        }

        _ptr += size;
        Position += (uint)size;
        return size;
    }

    /// <summary>
    /// ReadString: reads up to <paramref name="maxChars"/> - 1 bytes, stopping after a line feed; a CR LF ending becomes
    /// "\n". Returns the bytes as the game's char array holds them, without the terminating zero.
    /// </summary>
    public byte[] ReadString(int maxChars)
    {
        var text = new List<byte>();
        for (int i = 0; i < maxChars - 1; ++i)
        {
            if (_ptr >= _data.Length)
            {
                EndOfFile = true;
                break;
            }

            byte b = _data[_ptr++];
            text.Add(b);
            if (b == 10)
            {
                break;
            }
        }

        Position += (uint)text.Count;
        if (text.Count > 1 && text[^2] == 0x0d)
        {
            text[^2] = (byte)'\n';
            text.RemoveAt(text.Count - 1);
        }

        return text.ToArray();
    }

    public int ReadInt32()
    {
        Span<byte> b = stackalloc byte[4];
        return Read(b) == 4 ? BinaryPrimitives.ReadInt32LittleEndian(b) : throw new EndOfStreamException(FileName);
    }

    public short ReadInt16()
    {
        Span<byte> b = stackalloc byte[2];
        return Read(b) == 2 ? BinaryPrimitives.ReadInt16LittleEndian(b) : throw new EndOfStreamException(FileName);
    }

    public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());

    public byte ReadByte()
    {
        Span<byte> b = stackalloc byte[1];
        return Read(b) == 1 ? b[0] : throw new EndOfStreamException(FileName);
    }

    /// <summary>The game's <c>char len; Read(name, len)</c> strings: a signed length byte, then that many bytes.</summary>
    public byte[] ReadCountedString()
    {
        int length = (sbyte)ReadByte();
        if (length < 0)
        {
            throw new InvalidDataException($"{FileName}: negative string length at {Position - 1}.");
        }

        var text = new byte[length];
        return Read(text) == length ? text : throw new EndOfStreamException(FileName);
    }

    /// <summary>The game's do/while byte loops: bytes up to and including a zero, returned without it.</summary>
    public byte[] ReadZeroTerminated()
    {
        var text = new List<byte>();
        for (byte b = ReadByte(); b != 0; b = ReadByte())
        {
            text.Add(b);
        }

        return text.ToArray();
    }

    private static byte[] Inflate(string name, byte[] packed)
    {
        using var output = new MemoryStream();
        int at = 0;
        var block = new byte[PackBufferSize];
        while (at + 4 <= packed.Length)
        {
            int length = BinaryPrimitives.ReadInt32LittleEndian(packed.AsSpan(at));
            at += 4;
            if (length < 0 || at + length > packed.Length)
            {
                throw new InvalidDataException($"{name}: truncated packed block at offset {at - 4}.");
            }

            using var zlib = new ZLibStream(new MemoryStream(packed, at, length, writable: false), CompressionMode.Decompress);
            int total = 0;
            int got;
            while ((got = zlib.Read(block, total, block.Length - total)) > 0)
            {
                total += got;
                if (total == block.Length && zlib.ReadByte() >= 0)
                {
                    throw new InvalidDataException($"{name}: a packed block inflates past {PackBufferSize} bytes.");
                }
            }

            output.Write(block, 0, total);
            at += length;
        }

        return output.ToArray();
    }
}
