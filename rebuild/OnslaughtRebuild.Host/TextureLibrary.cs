// SPDX-License-Identifier: GPL-3.0-only
// The texture chunk as CDXTexture::Deserialize (bea-decomp src/DXTexture.cpp, 00559be0) reads it from a resource file
// outside the build tool, and the frame files CDXTexture::LoadFrame (00557300) loads, with the record's fields from
// src/Texture.h (CTexture and CDXTexture).

using System.Buffers.Binary;
using System.Text;
using Godot;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The level's textures by name (ignoring case; the first of a name wins). A resource file's TEXT chunk holds a header,
/// the CDXTexture record (DeviceObject first, so the name is at +8; frames +0x138, format +0x144, compressible +0x154) and each frame's level count;
/// the pixels are in the frame's converted file, a packed DDS image: data\resources\textures\NAME(FRAME)FORMAT.aya
/// (with "mustbe_" before the name unless compressible) or, for a compressible texture, its DXTn copy in
/// data\resources\dxtntextures, which LoadFrame falls back to (the Steam release ships the DXTn copies).
/// </summary>
public sealed class TextureLibrary(Func<string, (string Name, byte[] Bytes)> read)
{
    private static readonly uint TextTag = ChunkReader.MakeId("TEXT");
    private static readonly string[] FormatNames =
        ["UNKNOWN", "A1R5G5B5", "A4R4G4B4", "X8R8G8B8", "A8R8G8B8", "R5G6B5", "DXT1", "DXT2", "DXT4", "V8U8", "Q8W8V8U8"];

    private readonly Dictionary<string, (int Format, bool Compressible)> _records = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImageTexture?> _textures = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _records.Count;

    public int Missing { get; private set; }

    /// <summary>The resource reader's hook: reads a TEXT chunk's record and level counts; the rest is skipped.</summary>
    public bool ReadChunk(uint tag, ChunkReader c)
    {
        if (tag != TextTag)
        {
            return false;
        }

        c.GetNext();
        c.GetNext();
        var record = new byte[c.Size];
        c.Read(record);
        // DeviceObject (its table and mNext) comes first, so CTexture's fields start at +8.
        int nul = Array.IndexOf(record, (byte)0, 8, 0x80);
        string name = Encoding.Latin1.GetString(record, 8, (nul < 0 ? 0x88 : nul) - 8);
        int frames = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x138));
        int format = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x144));
        bool compressible = record[0x154] != 0;
        for (int frame = 0; frame < frames; frame++)
        {
            c.GetNext();
            c.ReadInt32();
        }

        _records.TryAdd(name, (format, compressible));
        return true;
    }

    /// <summary>CDXTexture::FindTexture's load of a texture the resource file may not list, by its format.</summary>
    public ImageTexture? Find(string name, int format)
    {
        _records.TryAdd(name, (format, true));
        return Get(name);
    }

    public ImageTexture? Get(string name)
    {
        if (_textures.TryGetValue(name, out ImageTexture? texture))
        {
            return texture;
        }

        texture = null;
        if (_records.TryGetValue(name, out (int Format, bool Compressible) r) && Load(name, r.Format, r.Compressible) is { } image)
        {
            if (image.IsCompressed())
            {
                image.Decompress();
            }

            if (!image.HasMipmaps())
            {
                image.GenerateMipmaps();
            }

            texture = ImageTexture.CreateFromImage(image);
        }
        else
        {
            Missing++;
        }

        _textures[name] = texture;
        return texture;
    }

    /// <summary>LoadFrame's paths for frame 0: the plain conversion, then (compressible) the DXTn copy.</summary>
    private Image? Load(string name, int format, bool compressible)
    {
        string file = name.Replace('\\', '%');
        string formatName = format >= 0 && format < FormatNames.Length ? FormatNames[format] : "nk";
        string[] paths = compressible
            ? [$@"data\resources\textures\{file}(0){formatName}.aya", $@"data\resources\dxtntextures\{file}(0){formatName}.aya"]
            : [$@"data\resources\textures\mustbe_{file}(0){formatName}.aya"];
        foreach (string path in paths)
        {
            (string Name, byte[] Bytes) found;
            try
            {
                found = read(path);
            }
            catch (FileNotFoundException)
            {
                continue;
            }

            MemBuffer mb = MemBuffer.FromFile(found.Name, found.Bytes);
            var dds = new byte[mb.Length];
            mb.Read(dds);
            var image = new Image();
            if (image.LoadDdsFromBuffer(dds) == Error.Ok)
            {
                return image;
            }
        }

        return null;
    }
}
