// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/engine.cpp (CEngine::Deserialize 0044a6e0; engine.cpp is in Stuart's release, hence
// GPL-3.0-only), src/maptex.cpp (CMapTex::Deserialize 004916c0) and src/Map.cpp (CMap::Deserialize 00491060).

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.World;

/// <summary>
/// The ERES chunk of a level's resource file as far as the simulation needs it: the seven mixer texture mips (read past;
/// they feed rendering), the map number and the height field.
/// </summary>
public sealed class EngineResources
{
    /// <summary>N_MAPMIPS.</summary>
    public const int MapMips = 7;

    /// <summary>sizeof(CMapTex).</summary>
    private const int MapTexSize = 0x4c;

    private EngineResources(int mapNumber, HeightField heightField)
    {
        MapNumber = mapNumber;
        HeightField = heightField;
    }

    public int MapNumber { get; }

    public HeightField HeightField { get; }

    /// <summary>
    /// CEngine::Deserialize up to CMap::Deserialize's height field. Call after the reader returned the ERES tag; it reads
    /// on through the ENGN chunk and leaves the reader just after the heights (MIXERMAP.Init's data comes next).
    /// </summary>
    public static EngineResources Read(ChunkReader c)
    {
        if (c.GetNext() != ChunkReader.MakeId("ENGN"))
        {
            throw new InvalidDataException("Engine deserialize format failure (no ENGN chunk).");
        }

        int mipCount = c.ReadInt32();
        if (mipCount != MapMips)
        {
            throw new InvalidDataException($"Engine deserialize format failure ({mipCount} mixer textures, expected {MapMips}).");
        }

        Span<byte> mapTex = stackalloc byte[MapTexSize];
        for (int i = 0; i < mipCount; i++)
        {
            c.GetNext();
            c.GetNext();
            c.Read(mapTex);
            uint data = BinaryPrimitives.ReadUInt32LittleEndian(mapTex);
            uint unknown08 = BinaryPrimitives.ReadUInt32LittleEndian(mapTex[0x08..]);
            int texSize = BinaryPrimitives.ReadInt32LittleEndian(mapTex[0x10..]);
            int num = BinaryPrimitives.ReadInt32LittleEndian(mapTex[0x14..]);
            if (data != 0)
            {
                c.GetNext();
                c.File.Skip(num * texSize);
            }

            if (unknown08 != 0)
            {
                c.GetNext();
                c.File.Skip(num * 1024);
            }
        }

        c.GetNext();
        int mapNumber = c.ReadInt32();
        return new EngineResources(mapNumber, HeightField.Load(c));
    }
}
