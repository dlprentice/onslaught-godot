// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using Godot;

namespace OnslaughtRebuild.GodotClient;

internal static class CuratedAyaTextureLoader
{
    internal enum Compression
    {
        Dxt1,
        Dxt2,
        Rgba8,
    }

    private const int MaximumSourceBytes = 2 * 1024 * 1024;
    private const int MaximumDdsBytes = 8 * 1024 * 1024;
    private const int StreamBufferBytes = 65536;
    private const int DdsHeaderBytes = 128;
    private const uint DdsdMipmapCount = 0x20000;
    private const uint Ddscaps2Cubemap = 0x200;
    private const uint Ddscaps2Volume = 0x200000;

    public static Texture2D Load(
        string resourcePath,
        int expectedWidth,
        int expectedHeight,
        Compression expectedCompression = Compression.Dxt2,
        Image.Format? expectedTargetFormat = null,
        int? expectedMipCount = null)
    {
        byte[] source = Godot.FileAccess.GetFileAsBytes(resourcePath);
        if (source.Length is 0 or > MaximumSourceBytes)
        {
            throw new InvalidDataException($"Curated texture '{resourcePath}' is missing or exceeds the source limit.");
        }

        byte[] dds = InflateAya(source);
        if (dds.Length < 128 || !dds.AsSpan(0, 4).SequenceEqual("DDS "u8))
        {
            throw new InvalidDataException(
                "Curated texture is not an AYA-wrapped DDS image.");
        }
        bool expectedPixelFormat = expectedCompression switch
        {
            Compression.Dxt1 => dds.AsSpan(84, 4).SequenceEqual("DXT1"u8),
            Compression.Dxt2 => dds.AsSpan(84, 4).SequenceEqual("DXT2"u8),
            Compression.Rgba8 =>
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(80, 4)) == 0x41 &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(84, 4)) == 0 &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(88, 4)) == 32 &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(92, 4)) == 0x00FF0000 &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(96, 4)) == 0x0000FF00 &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(100, 4)) == 0x000000FF &&
                BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(104, 4)) == 0xFF000000,
            _ => false,
        };
        if (!expectedPixelFormat)
        {
            throw new InvalidDataException(
                $"Curated texture does not match the expected {expectedCompression} DDS pixel format.");
        }
        if (expectedMipCount is int mipCount &&
            BinaryPrimitives.ReadUInt32LittleEndian(dds.AsSpan(28, 4)) != (uint)mipCount)
        {
            throw new InvalidDataException(
                $"Curated texture '{resourcePath}' does not contain the expected {mipCount} DDS mip levels.");
        }
        // Godot's loader fills a short surface from uninitialized memory instead of
        // failing, so a payload shorter than its read is refused before decoding.
        long available = dds.Length - DdsHeaderBytes;
        if (available < DdsPayloadBytes(dds, expectedCompression, available))
        {
            throw new InvalidDataException("Curated texture has truncated DDS pixel data.");
        }

        using var image = new Image();
        Error result = image.LoadDdsFromBuffer(dds);
        if (result != Error.Ok || image.IsEmpty())
        {
            throw new InvalidDataException($"Godot could not decode curated texture '{resourcePath}' ({result}).");
        }
        if (image.GetWidth() != expectedWidth || image.GetHeight() != expectedHeight)
        {
            throw new InvalidDataException(
                $"Curated texture '{resourcePath}' decoded as {image.GetWidth()}x{image.GetHeight()}, " +
                $"expected {expectedWidth}x{expectedHeight}.");
        }
        if (expectedTargetFormat is Image.Format targetFormat && image.GetFormat() != targetFormat)
        {
            if (image.IsCompressed() && image.Decompress() != Error.Ok)
            {
                throw new InvalidDataException(
                    $"Curated texture '{resourcePath}' could not be decompressed for {targetFormat} upload.");
            }
            image.Convert(targetFormat);
        }
        if (expectedTargetFormat is Image.Format requiredFormat && image.GetFormat() != requiredFormat)
        {
            throw new InvalidDataException(
                $"Curated texture '{resourcePath}' could not be converted to {requiredFormat}.");
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Payload bytes the pinned loader (modules/dds/texture_loader_dds.cpp at
    /// 8898c2b3d) reads after the header for the three admitted layouts,
    /// including its cubemap faces and volume slices. Results above
    /// <paramref name="limit"/> return <c>limit + 1</c>.
    /// </summary>
    internal static long DdsPayloadBytes(ReadOnlySpan<byte> dds, Compression compression, long limit)
    {
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(dds[8..]);
        long mipmaps = (flags & DdsdMipmapCount) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(dds[28..]) : 1;
        uint caps2 = BinaryPrimitives.ReadUInt32LittleEndian(dds[112..]);
        long width = BinaryPrimitives.ReadUInt32LittleEndian(dds[16..]);
        long height = BinaryPrimitives.ReadUInt32LittleEndian(dds[12..]);
        int block = compression switch { Compression.Rgba8 => 4, Compression.Dxt1 => 8, _ => 16 };
        bool compressed = compression != Compression.Rgba8;
        if ((caps2 & Ddscaps2Cubemap) != 0)
        {
            return Math.Min(6 * DdsLayerBytes(width, height, mipmaps, block, compressed, limit), limit + 1);
        }
        if ((caps2 & Ddscaps2Volume) == 0)
        {
            return DdsLayerBytes(width, height, mipmaps, block, compressed, limit);
        }
        long depth = BinaryPrimitives.ReadUInt32LittleEndian(dds[24..]);
        long total = 0;
        for (long mip = 0; mip < mipmaps; mip++)
        {
            if (depth > limit)
            {
                return limit + 1;
            }
            total += depth * DdsLayerBytes(width, height, 1, block, compressed, limit);
            if (total > limit)
            {
                return limit + 1;
            }
            width = Math.Max(1, width >> 1);
            height = Math.Max(1, height >> 1);
            depth = Math.Max(1, depth >> 1);
        }
        return total;
    }

    private static long DdsLayerBytes(long width, long height, long mipmaps, int block, bool compressed, long limit)
    {
        if (width > limit || height > limit)
        {
            return limit + 1;
        }
        long w = width;
        long h = height;
        long size = width * height * block;
        if (compressed)
        {
            // The loader pads by the remainder, not to a multiple of four.
            w += w % 4;
            h += h % 4;
            size = Math.Max(1, (w + 3) >> 2) * Math.Max(1, (h + 3) >> 2) * block;
        }
        for (long level = 1; level < mipmaps && size <= limit; level++)
        {
            w = Math.Max(1, w >> 1);
            h = Math.Max(1, h >> 1);
            size += (compressed ? Math.Max(1, (w + 3) >> 2) * Math.Max(1, (h + 3) >> 2) : w * h) * block;
        }
        return Math.Min(size, limit + 1);
    }

    private static byte[] InflateAya(byte[] source)
    {
        using var output = new MemoryStream();
        int position = 0;
        while (position < source.Length)
        {
            if (source.Length - position < sizeof(uint))
            {
                throw new InvalidDataException("Curated texture has a truncated AYA record header.");
            }

            uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(position, sizeof(uint)));
            position += sizeof(uint);
            if (declaredLength is 0 or > int.MaxValue || declaredLength > source.Length - position)
            {
                throw new InvalidDataException("Curated texture has invalid AYA record framing.");
            }

            int compressedLength = checked((int)declaredLength);
            InflateRecord(source.AsSpan(position, compressedLength), output, MaximumDdsBytes - (int)output.Length);
            position += compressedLength;
        }

        return output.ToArray();
    }

    // Godot's zlib stream reports the bytes zlib actually consumed. .NET's
    // ZLibStream reads ahead, so it accepted a valid stream followed by data
    // inside the declared record and a stream missing its end; both are refused.
    private static void InflateRecord(ReadOnlySpan<byte> compressed, MemoryStream output, int remainingLimit)
    {
        using var stream = new StreamPeerGZip();
        if (stream.StartDecompression(true, StreamBufferBytes) != Error.Ok)
        {
            throw new InvalidDataException("Cannot initialize the curated zlib decoder.");
        }
        long start = output.Length;
        int consumed = 0;
        while (consumed < compressed.Length)
        {
            (Error status, int accepted) = PutPartial(stream, compressed[consumed..]);
            if (status != Error.Ok)
            {
                throw new InvalidDataException("Curated texture contains an invalid zlib stream.");
            }
            consumed += accepted;
            int available = stream.GetAvailableBytes();
            if (output.Length - start + available > remainingLimit)
            {
                throw new InvalidDataException("Curated texture exceeds the decoded DDS limit.");
            }
            if (available > 0)
            {
                Drain(stream, output, available);
            }
            if (accepted == 0 && available == 0)
            {
                throw new InvalidDataException("Curated texture AYA record contains trailing compressed data.");
            }
        }
        // The stream API does not expose Z_STREAM_END. A completed stream leaves
        // a probe byte untouched; an incomplete one consumes it or fails. Drain
        // any output buffered internally without accepting that byte.
        while (true)
        {
            (Error status, int accepted) = PutPartial(stream, [0]);
            if (status != Error.Ok || accepted != 0)
            {
                throw new InvalidDataException("Curated texture has a truncated zlib stream.");
            }
            int available = stream.GetAvailableBytes();
            if (output.Length - start + available > remainingLimit)
            {
                throw new InvalidDataException("Curated texture exceeds the decoded DDS limit.");
            }
            if (available == 0)
            {
                break;
            }
            Drain(stream, output, available);
        }
        stream.Clear();
    }

    private static (Error Status, int Accepted) PutPartial(StreamPeerGZip stream, ReadOnlySpan<byte> data)
    {
        using Godot.Collections.Array result = stream.PutPartialData(data);
        using Variant status = result[0];
        using Variant accepted = result[1];
        return ((Error)status.AsInt32(), accepted.AsInt32());
    }

    private static void Drain(StreamPeerGZip stream, MemoryStream output, int available)
    {
        using Godot.Collections.Array read = stream.GetData(available);
        using Variant status = read[0];
        using Variant bytes = read[1];
        if ((Error)status.AsInt32() != Error.Ok)
        {
            throw new InvalidDataException("Cannot read the decoded curated texture.");
        }
        output.Write(bytes.AsByteArray());
    }
}
