// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/engine.cpp (CEngine::Deserialize 0044a6e0), src/maptex.cpp (CMapTex::Deserialize
// 004916c0), src/Map.cpp (CMap::Deserialize 00491060), src/HeightField.cpp (CHeightField::Load 0047f750, the colour ramp
// Unknown47e8e0 and the tile texels Unknown47eff0) and src/mixermap.cpp (CMixerMap::Init 005232b0, SMixerSlot::Init
// 00523190). Reconstructed; not in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Host.Landscape;

/// <summary>
/// The landscape's lit texels, as CLandscapeTexture builds each tile of its cache: the mixer textures (six palettised
/// textures per mip; each texel a palette index), the mixer map's per-tile layers (up to six textures, each with a 9 x 9
/// alpha grid per cell) blended over the base texture, then lit by the 512 x 512 light map through the height field's
/// 65-step colour ramp, into R5G6B5. Static shadows are not applied yet (the unshadowed path, as for a tile with no
/// shadow cell).
/// </summary>
public sealed class TerrainTexture
{
    /// <summary>N_MAPMIPS.</summary>
    private const int MapMips = 7;
    private const int MixerMapSlots = 0x1000;
    private const int MixerMapSize = 0x40000;

    private readonly MapTex[] _mapTex;
    private readonly MixerSlot[] _slots;
    private readonly byte[] _light;
    private readonly (int Red, int Green, int Blue)[] _colors = new (int, int, int)[65];

    private TerrainTexture(MapTex[] mapTex, MixerSlot[] slots, byte[] light, byte[] heightField)
    {
        _mapTex = mapTex;
        _slots = slots;
        _light = light;
        Fog = BinaryPrimitives.ReadUInt32LittleEndian(heightField.AsSpan(Properties));
        Ambient = BinaryPrimitives.ReadUInt32LittleEndian(heightField.AsSpan(Properties + 0x14));
        FogDensity = BinaryPrimitives.ReadSingleLittleEndian(heightField.AsSpan(Properties + 0x20));
        KempyCube = heightField[Properties + 0x18];
        WaterNumber = heightField[Properties + 0x18 + 5];
        WaterColour = BinaryPrimitives.ReadUInt32LittleEndian(heightField.AsSpan(Properties + 0x24));
        DetailTexture = heightField[Properties + 0x18 + 4];
        uint sun = BinaryPrimitives.ReadUInt32LittleEndian(heightField.AsSpan(Properties + 0x04));
        AntiSun = BinaryPrimitives.ReadUInt32LittleEndian(heightField.AsSpan(Properties + 0x08));
        BuildColours(sun, Ambient);
        Sun = Rebalance(sun, Ambient);
        SunPos = (BinaryPrimitives.ReadSingleLittleEndian(heightField.AsSpan(Properties + 0x2c)),
            BinaryPrimitives.ReadSingleLittleEndian(heightField.AsSpan(Properties + 0x30)),
            BinaryPrimitives.ReadSingleLittleEndian(heightField.AsSpan(Properties + 0x34)));
    }

    /// <summary>mHFP.Sun as CHeightField::Load leaves it: sun plus ambient scaled down to fit 255, less the ambient.</summary>
    public uint Sun { get; }

    public uint AntiSun { get; }

    /// <summary>mHFP.SunPos: where the sun is; the light travels the other way (CEngine::SetupLights).</summary>
    public (float X, float Y, float Z) SunPos { get; private init; }

    private static uint Rebalance(uint sun, uint ambient)
    {
        int ar = (int)((ambient >> 16) & 0xff);
        int ag = (int)((ambient >> 8) & 0xff);
        int ab = (int)(ambient & 0xff);
        int r = (int)((sun >> 16) & 0xff) + ar;
        int b = (int)(sun & 0xff) + ab;
        int g = (int)((sun >> 8) & 0xff) + ag;
        int m = Math.Max(r, Math.Max(b, g));
        if (m > 255)
        {
            r = r * 255 / m;
            g = g * 255 / m;
            b = b * 255 / m;
        }

        return unchecked((uint)(((r - ar) << 16) | ((g - ag) << 8) | (b - ab)) | 0xff000000);
    }

    /// <summary>CHeightField::mHFP, the map's properties (SHeightFieldProperties).</summary>
    private const int Properties = 0x1078;

    /// <summary>The fog colour (mHFP.Fog).</summary>
    public uint Fog { get; }

    /// <summary>The ambient colour (mHFP.Ambient): the terrain's diffuse through its material's 0.8 ambient.</summary>
    public uint Ambient { get; }

    public float FogDensity { get; }

    /// <summary>mHFP.mUnknown18[5]: ENGINE.SetWater's number (CDXWater::Load's mixers\caustic%.2d and reflection%.2d).</summary>
    public int WaterNumber { get; }

    /// <summary>MAP.GetWaterColour (mHFP +0x24): the water's texture factor.</summary>
    public uint WaterColour { get; }

    /// <summary>mHFP.mUnknown18[0]: ENGINE.SetKempyCube's sky number.</summary>
    public int KempyCube { get; }

    /// <summary>mHFP.mUnknown18[4]: CLandscape::LoadMixerTexture's "mixers\detail%.2d.tga".</summary>
    public int DetailTexture { get; }

    public int LightOutOfRange { get; private set; }

    private sealed record MapTex(int TexSize, int Num, int Size, byte[] Data, uint[] Palette);

    private sealed record MixerSlot(int Count, ushort[] Textures, sbyte[] Alphas);

    /// <summary>CEngine::Deserialize's chunk through MIXERMAP.Init, from a reader at the ERES contents.</summary>
    public static TerrainTexture Read(ChunkReader c)
    {
        if (c.GetNext() != ChunkReader.MakeId("ENGN"))
        {
            throw new InvalidDataException("no ENGN chunk");
        }

        int mips = c.ReadInt32();
        if (mips != MapMips)
        {
            throw new InvalidDataException($"{mips} mixer textures, expected {MapMips}");
        }

        var mapTex = new MapTex[mips];
        var record = new byte[0x4c];
        for (int i = 0; i < mips; i++)
        {
            c.GetNext();
            c.GetNext();
            c.Read(record);
            uint data = BinaryPrimitives.ReadUInt32LittleEndian(record);
            uint palette = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0x08));
            int texSize = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x10));
            int num = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x14));
            int size = BinaryPrimitives.ReadInt32LittleEndian(record.AsSpan(0x18));
            byte[] texels = [];
            uint[] colours = [];
            if (data != 0)
            {
                c.GetNext();
                texels = new byte[num * texSize];
                c.Read(texels);
            }

            if (palette != 0)
            {
                // mNum * 1024 DWORDs allocated, mNum * 1024 bytes read: 256 colours per texture.
                c.GetNext();
                var bytes = new byte[num * 1024];
                c.Read(bytes);
                colours = new uint[num * 256];
                for (int k = 0; k < colours.Length; k++)
                {
                    colours[k] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(k * 4));
                }
            }

            mapTex[i] = new MapTex(texSize, num, size, texels, colours);
        }

        // CMap::Deserialize: the map number, then CHeightField::Load (its record, then the heights).
        c.GetNext();
        c.ReadInt32();
        c.GetNext();
        c.GetNext();
        var heightField = new byte[c.Size];
        c.Read(heightField);
        c.GetNext();
        c.File.Skip(64 * 64 * 81 * 2);

        // MIXERMAP.Init: the slots (each a 0x14-byte record and, when it has data, its entries' 81 alphas), then the map.
        c.GetNext();
        var slots = new MixerSlot[MixerMapSlots];
        var slotRecord = new byte[0x14];
        for (int i = 0; i < MixerMapSlots; i++)
        {
            c.GetNext();
            c.GetNext();
            c.Read(slotRecord);
            int count = BinaryPrimitives.ReadInt32LittleEndian(slotRecord);
            uint hasData = BinaryPrimitives.ReadUInt32LittleEndian(slotRecord.AsSpan(4));
            var textures = new ushort[6];
            for (int t = 0; t < 6; t++)
            {
                textures[t] = BinaryPrimitives.ReadUInt16LittleEndian(slotRecord.AsSpan(8 + (2 * t)));
            }

            sbyte[] alphas = [];
            if (hasData != 0)
            {
                c.GetNext();
                var raw = new byte[count * 81];
                c.Read(raw);
                alphas = (sbyte[])(Array)raw;
            }

            slots[i] = new MixerSlot(count, textures, alphas);
        }

        c.GetNext();
        // Retail's last rows read the corner texels past the map's end (light[x + 0x201]); zeros stand in there.
        var light = new byte[MixerMapSize + 0x202];
        c.File.Read(light.AsSpan(0, MixerMapSize));
        return new TerrainTexture(mapTex, slots, light, heightField);
    }

    /// <summary>
    /// CHeightField's colour ramp (Unknown47e8e0, from the file's sun and ambient before Load rebalances the sun), then
    /// Load's doubling and clamping into the R5G6B5 fields.
    /// </summary>
    private void BuildColours(uint sun, uint ambient)
    {
        int red = (int)((((ambient >> 16) & 0xff) << 8) / (((sun >> 16) & 0xfe) + 1));
        int green = (int)((ambient & 0xff00) / (((sun >> 8) & 0xfe) + 1));
        int blue = (int)(((ambient & 0xff) << 8) / ((sun & 0xfe) + 1));
        int stepRed = (0xff - red) << 2;
        int stepGreen = (0xff - green) << 2;
        int stepBlue = (0xff - blue) << 2;
        red <<= 8;
        green <<= 8;
        blue <<= 8;
        for (int i = 0; i < 64; i++)
        {
            _colors[i] = (unchecked((int)(((uint)red & 0xffffff00) << 8)), unchecked((int)(((uint)green & 0xffffff00) << 3)),
                unchecked((int)(((uint)(blue >> 3)) & 0xffffffe0)));
            red += stepRed;
            green += stepGreen;
            blue += stepBlue;
        }

        _colors[64] = _colors[63];
        for (int i = 0; i < 65; i++)
        {
            (int r, int g, int b) = _colors[i];
            r <<= 1;
            if ((uint)r > 0xf80000)
            {
                r = 0xf80000;
            }

            g <<= 1;
            if ((uint)g > 0x7e000)
            {
                g = 0x7e000;
            }

            b <<= 1;
            if ((uint)b > 0x1f00)
            {
                b = 0x1f00;
            }

            _colors[i] = (r & 0xf80000, g & 0x7e000, b & 0x1f00);
        }
    }

    /// <summary>
    /// The whole map at one mip shift: 512 << shift texels a side, R5G6B5, row-major, the map's x across and y down, as
    /// CLandscapeTexture lays its tiles.
    /// </summary>
    /// <summary>
    /// One of CDXLandscape's wrapping texture levels (CLandscapeTexture, 512 x 512 R5G6B5): each listed 8-metre tile built
    /// at the level's mip shift (CHeightField::Unknown47eff0) into its slot, (tile x * 8) mod (512 >> level) << level
    /// across (CLandscapeTexture::UpdateTile), so that world x * 2^level / 512, wrapped, finds it.
    /// </summary>
    public ushort[] BuildCacheLevel(int level, IEnumerable<int> tiles)
    {
        int step = 1 << level;
        MapTex maptex = _mapTex.FirstOrDefault(m => m.Size == 16 * step)
            ?? throw new NotSupportedException($"no mixer mip of {16 * step} texels for level {level}");
        int tileMask = (512 >> level) - 1;
        var map = new ushort[512 * 512];
        var scratch = new int[1024];
        foreach (int tile in tiles)
        {
            int x = (tile & 0x3f) << 3;
            int y = (tile >> 6) << 3;
            int origin = (((y & tileMask) << level) * 512) + ((x & tileMask) << level);
            Tile(level, maptex, map, origin, 512, tile, scratch);
        }

        return map;
    }

    public ushort[] BuildMap(int shift)
    {
        int step = 1 << shift;
        MapTex maptex = _mapTex.FirstOrDefault(m => m.Size == 16 * step)
            ?? throw new NotSupportedException($"no mixer mip of {16 * step} texels for shift {shift}");
        int pitch = 512 * step;
        var map = new ushort[pitch * pitch];
        var scratch = new int[1024];
        for (int tile = 0; tile < 4096; tile++)
        {
            int origin = ((tile >> 6) * 8 * step * pitch) + ((tile & 63) * 8 * step);
            Tile(shift, maptex, map, origin, pitch, tile, scratch);
        }

        return map;
    }

    /// <summary>CHeightField::Unknown47eff0 for a whole tile (x0 = y0 = 0, x1 = y1 = 8) with no shadow cell.</summary>
    private void Tile(int shift, MapTex maptex, ushort[] dst, int origin, int pitch, int tile, int[] scratch)
    {
        unchecked
        {
            int step = 1 << shift;
            int size = maptex.Size;
            int @base = 0;
            int back = size - step;
            int rowsize = size * step;
            if ((tile & 1) != 0)
            {
                @base = step * 8;
            }

            if ((tile & 0x40) != 0)
            {
                @base += rowsize * 8;
            }

            int tx = (tile % 64) << 3;
            int ty = (tile / 64) << 3;
            MixerSlot slot = _slots[tile];
            int outRow = origin;
            for (int y = 0; y < 8; y++)
            {
                int offset = rowsize * y;
                int @out = outRow;
                for (int x = 0; x < 8; x++)
                {
                    int texel = (step * x) + offset + @base;

                    // the base texture
                    int src = texel;
                    int p = 0;
                    for (int i = 0; i < step; i++)
                    {
                        for (int j = 0; j < step; j++)
                        {
                            scratch[p + j] = (int)maptex.Palette[maptex.Data[src + j]];
                        }

                        src += size;
                        p += step;
                    }

                    // the mixer layers
                    int alpha = (y * 9) + x;
                    for (int l = 0; l < slot.Count; l++)
                    {
                        int t = slot.Textures[l];
                        int pal = t << 8;
                        sbyte[] al = slot.Alphas;
                        int a = al[alpha] << 24;
                        int ax = (al[alpha + 1] << 24) - a;
                        int a9 = al[alpha + 9] << 24;
                        int axy = ((al[alpha + 10] << 24) - a9) - ax;
                        int ay = a9 - a;
                        ax >>= shift;
                        ay >>= shift;
                        axy >>= shift * 2;
                        int tsrc = (maptex.TexSize * t) + texel;
                        int tp = 0;
                        for (int i = step; i != 0; i--)
                        {
                            int aa = a;
                            a += ay;
                            int j = step;
                            while (j != 0)
                            {
                                int c = (int)((uint)(aa & unchecked((int)0xff000000)) + maptex.Palette[pal + maptex.Data[tsrc]]);
                                int d = c - scratch[tp];
                                if (d > 0)
                                {
                                    bool rowEnded = false;
                                    while (true)
                                    {
                                        bool skip = false;
                                        while (d < 0x20000000)
                                        {
                                            if (d < 0)
                                            {
                                                skip = true;
                                                break;
                                            }

                                            int f = d >> 26;
                                            scratch[tp] = ((((scratch[tp] & 0xf8f8ff) * (7 - f)) + ((c & 0xf8f8ff) * f)) >> 3) + (int)((uint)c & 0xff000000);
                                            tp++;
                                            tsrc++;
                                            aa += ax;
                                            if (--j == 0)
                                            {
                                                rowEnded = true;
                                                break;
                                            }

                                            c = (int)((uint)(aa & unchecked((int)0xff000000)) + maptex.Palette[pal + maptex.Data[tsrc]]);
                                            d = c - scratch[tp];
                                        }

                                        if (skip || rowEnded)
                                        {
                                            break;
                                        }

                                        scratch[tp] = c;
                                        tp++;
                                        tsrc++;
                                        aa += ax;
                                        if (--j == 0)
                                        {
                                            rowEnded = true;
                                            break;
                                        }

                                        c = (int)((uint)(aa & unchecked((int)0xff000000)) + maptex.Palette[pal + maptex.Data[tsrc]]);
                                        d = c - scratch[tp];
                                    }

                                    if (rowEnded)
                                    {
                                        break;
                                    }
                                }

                                // skip
                                tp++;
                                tsrc++;
                                aa += ax;
                                j--;
                            }

                            ax += axy;
                            tsrc += back;
                        }

                        alpha += 81;
                    }

                    // lit (the unshadowed path)
                    int light = ((ty + y) * 512) + tx;
                    int l00 = _light[light + x] << 8;
                    int lx = (_light[light + x + 1] << 8) - l00;
                    int l10 = _light[light + x + 0x200] << 8;
                    int lxy = ((_light[light + x + 0x201] << 8) - l10) - lx;
                    int ly = l10 - l00;
                    lx >>= shift;
                    ly >>= shift;
                    lxy >>= shift * 2;
                    int o = @out;
                    int q = 0;
                    for (int i = 0; i < step; i++)
                    {
                        l00 += ly;
                        lx += lxy;
                        int lv = l00;
                        for (int j = 0; j < step; j++)
                        {
                            lv += lx;
                            int level = lv >> 8;
                            if (level is < 0 or > 64)
                            {
                                LightOutOfRange++;
                                level = Math.Clamp(level, 0, 64);
                            }

                            (int red, int green, int blue) = _colors[level];
                            uint texelColour = (uint)scratch[q];
                            int b0 = (int)(texelColour & 0xff);
                            int b1 = (int)((texelColour >> 8) & 0xff);
                            int b2 = (int)((texelColour >> 16) & 0xff);
                            dst[o + j] = (ushort)((((b2 * blue) & 0x1f0000) + ((b1 * green) & 0x7e00000) + (int)((uint)(b0 * red) & 0xf8000000)) >> 16);
                            q++;
                        }

                        o += pitch;
                    }

                    @out += step;
                }

                outRow += pitch * step;
            }
        }
    }
}
