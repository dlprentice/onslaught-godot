// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The Level 100 landscape-cache compositor: the CPU re-render that produces
/// every macro texel for texture levels 1-4. It is deliberately free of Godot,
/// filesystem, and clock dependencies so it can be executed and byte-compared
/// under test against the pinned retail-derived root map.
/// </summary>
internal sealed class Level100TerrainCompositor
{
    public const int MapSize = 512;
    public const int TileCountPerAxis = 64;
    public const int TileCount = TileCountPerAxis * TileCountPerAxis;
    public const int TileWidth = 8;
    public const int RootTextureLength = MapSize * MapSize * sizeof(ushort);

    /// <summary>
    /// `_DAT_005db060` = `0x3b800000` = 1/256, the scale `CEngine::SetupLights`
    /// @ `0x0044a2d0` applies to each HFLD light-colour byte before the triple is
    /// copied into the cached light record (`0x0044a431`, `0x0044a457`,
    /// `0x0044a470` and the matching sequence at `0x0044a4f8`-`0x0044a545`).
    /// </summary>
    public const float LightColorByteScale = 1f / 256f;

    /// <summary>
    /// `0x3f4ccccd` = 0.8, the ambient reflectance of the terrain-only
    /// `D3DMATERIAL9` at `0x0083d28c + 0x10`, written by the single initialiser
    /// at `0x004eb9a0`. That record's Diffuse is (0,0,0,1) and its Emissive is
    /// zero, so ambient is the whole of its response.
    /// </summary>
    public const float TerrainMaterialAmbient = 0.8f;

    private const int MaterialCount = 6;
    private const int PaletteEntriesPerMaterial = 256;
    private const int WeightCountPerLayer = 9 * 9;
    private const int ShadowBytesPerTile = 64 * 64 / 8;
    private const string HierarchySourceSha256 =
        "541EACD0AA75FAE8BEFB8A3E1505EA52AE6B1F6C1367C15C65D7DD23B7CFE977";

    private static readonly int[] s_pineLevelOffsets = [0, 1, 5, 21, 85, 341, 1_365];

    private readonly TerrainHierarchy _hierarchy;
    private readonly LightingCoefficient[] _lighting;

    private Level100TerrainCompositor(TerrainHierarchy hierarchy, LightingCoefficient[] lighting)
    {
        _hierarchy = hierarchy;
        _lighting = lighting;
    }

    public static Level100TerrainCompositor Create(
        byte[] hierarchySource,
        uint sunColorRgb24,
        uint ambientColorRgb24) =>
        new(
            TerrainHierarchy.Load(hierarchySource),
            BuildLightingGradient(sunColorRgb24, ambientColorRgb24));

    /// <summary>
    /// The constant vertex colour Direct3D's fixed-function lighting produces
    /// for retail's terrain draw, from the two HFLD light-colour fields.
    ///
    /// `CDXLandscape::Render` (`0x00545410`) brackets the terrain draw and no
    /// other draw in the image: it sets `D3DRS_AMBIENT` to zero
    /// (`0x005454db`), calls `SetMaterial` with the terrain-only record
    /// `0x0083d28c` (`0x005454f4`, device vtable +0xc4 = IDirect3DDevice9 index
    /// 49), and re-uploads every enabled cached light through
    /// `CDXEngine::ApplyCachedLight` with its second argument set to 1, which is
    /// the flag that copies the light's colour into `D3DLIGHT9.Ambient`
    /// (`0x005512be`). Afterwards it restores the register and re-uploads every
    /// light with `Ambient` zero. `D3DRS_LIGHTING` stays enabled, because
    /// `RenderTerrain` only clears it when the `LANDSCAPE_LIGHTING` CVar at
    /// `0x008aa94c` is zero and its registered default is 1 (`0x00544690`).
    ///
    /// The terrain vertex is stride `0x14` - position plus one UV pair, with no
    /// normal - so `N.L` is zero and the diffuse term vanishes. The material's
    /// own Diffuse is black and its Emissive is zero, so the whole surviving
    /// fixed-function term is
    ///
    ///     vertex_diffuse = Ambient_material * (D3DRS_AMBIENT + sum light.Ambient)
    ///                    = 0.8 * sum(light colour) / 256
    ///
    /// which is one colour for the entire terrain, with no positional or
    /// per-vertex dependence. Stage 0 then doubles it under `D3DTOP_MODULATE2X`;
    /// that factor of two lives in the shader, where the stage op does.
    ///
    /// `CEngine::SetupLights` @ `0x0044a2d0` enables exactly lights 0 and 1
    /// (`0x009c68a0`/`0x009c68a1` := 1, `0x009c68a2..a7` := 0) and fills them
    /// from `CHFD+0x107C` and `CHFD+0x1080`. Those two fields are the arguments
    /// here; nothing about this function is specific to Level 100.
    ///
    /// Derivation and its measured residual:
    /// reverse-engineering/binary-analysis/terrain-ambient-light-material-2026-07-26.md.
    /// </summary>
    public static (float Red, float Green, float Blue) TerrainVertexDiffuse(
        uint light0ColorRgb24,
        uint light1ColorRgb24)
    {
        static float Channel(uint light0, uint light1, int shift)
        {
            uint sum = ((light0 >> shift) & 0xFF) + ((light1 >> shift) & 0xFF);
            // Direct3D clamps the lit vertex colour to [0,1]. For Level 100 the
            // sum is (224, 212, 177)/256 and 0.8 x that is (0.700, 0.663, 0.553),
            // so this clamp does not fire; it is the pipeline's, not a fit.
            return Math.Min(TerrainMaterialAmbient * (sum * LightColorByteScale), 1f);
        }

        return (
            Channel(light0ColorRgb24, light1ColorRgb24, 16),
            Channel(light0ColorRgb24, light1ColorRgb24, 8),
            Channel(light0ColorRgb24, light1ColorRgb24, 0));
    }

    /// <summary>
    /// Composites one landscape tile into <paramref name="destination"/>, an
    /// RGB565 512x512 cache, at the supplied slot. Level 0 writes 8x8-texel
    /// blocks and therefore reproduces the whole root map when run over the
    /// full 64x64 tile grid with slot == tile.
    /// </summary>
    public void RenderTile(
        byte[] destination,
        int level,
        int tileX,
        int tileY,
        int slotX,
        int slotY)
    {
        // Refuse an invalid level before allocating its shifted block: the
        // admitted levels are 0..4, and 1 << level can otherwise reach gigabytes.
        if ((uint)level >= (uint)_hierarchy.Maps.Length)
        {
            throw new IndexOutOfRangeException();
        }
        int scale = 1 << level;
        int blockSize = TileWidth * scale;
        var block = new ushort[blockSize * blockSize];
        MapTexture map = _hierarchy.Maps[level];
        TerrainCell cell = _hierarchy.Cells[(tileY * TileCountPerAxis) + tileX];
        byte[]? shadow = _hierarchy.Shadows[(tileY * TileCountPerAxis) + tileX];

        for (int controlY = 0; controlY < TileWidth; controlY++)
        {
            int shadeY = (tileY * TileWidth) + controlY;
            for (int controlX = 0; controlX < TileWidth; controlX++)
            {
                int shadeX = (tileX * TileWidth) + controlX;
                int shadeTopLeft = Shade(shadeX, shadeY);
                int shadeTopRight = Shade(Math.Min(shadeX + 1, MapSize - 1), shadeY);
                int shadeBottomLeft = Shade(shadeX, Math.Min(shadeY + 1, MapSize - 1));
                int shadeBottomRight = Shade(
                    Math.Min(shadeX + 1, MapSize - 1),
                    Math.Min(shadeY + 1, MapSize - 1));
                int shadeHorizontal = ((shadeTopRight - shadeTopLeft) << 8) >> level;
                int shadeVertical = ((shadeBottomLeft - shadeTopLeft) << 8) >> level;
                int shadeCross = unchecked(
                    (((shadeBottomRight - shadeBottomLeft) -
                      (shadeTopRight - shadeTopLeft)) << 8) >> (level * 2));

                for (int subY = 0; subY < scale; subY++)
                {
                    int pixelY = (controlY * scale) + subY;
                    // The released lighting loop advances both row slopes
                    // before its first texel; material weights do not.
                    int shadeRow = (shadeTopLeft << 8) + ((subY + 1) * shadeVertical);
                    int shadeStep = shadeHorizontal + ((subY + 1) * shadeCross);
                    for (int subX = 0; subX < scale; subX++)
                    {
                        int pixelX = (controlX * scale) + subX;
                        int sourceX = ((tileX & 1) * blockSize) + pixelX;
                        int sourceY = ((tileY & 1) * blockSize) + pixelY;
                        int sourceTexel = (sourceY * map.Width) + sourceX;
                        uint color = LookupColor(map, 0, sourceTexel);

                        for (int layer = 0; layer < cell.MaterialIds.Length; layer++)
                        {
                            int weightOffset = (layer * WeightCountPerLayer) +
                                (controlY * 9) + controlX;
                            int weightTopLeft = (sbyte)cell.Weights[weightOffset];
                            int weightTopRight = (sbyte)cell.Weights[weightOffset + 1];
                            int weightBottomLeft = (sbyte)cell.Weights[weightOffset + 9];
                            int weightBottomRight = (sbyte)cell.Weights[weightOffset + 10];
                            int weightHorizontal = unchecked(
                                ((weightTopRight - weightTopLeft) << 24) >> level);
                            int weightVertical = unchecked(
                                ((weightBottomLeft - weightTopLeft) << 24) >> level);
                            int weightCross = unchecked(
                                (((weightBottomRight - weightBottomLeft) -
                                  (weightTopRight - weightTopLeft)) << 24) >> (level * 2));
                            int weight = unchecked(
                                (weightTopLeft << 24) +
                                (subY * weightVertical) +
                                (subX * (weightHorizontal + (subY * weightCross))));
                            uint candidate = unchecked(
                                LookupColor(map, cell.MaterialIds[layer], sourceTexel) +
                                (uint)(weight & unchecked((int)0xFF000000)));
                            color = BlendMaterial(color, candidate);
                        }

                        // 0x0047f54a is a bare `sar edx,8` with no clamp; this
                        // bound is defensive only. Measured over every island
                        // tile at levels 0-4, the raw index never leaves 0..63,
                        // so the clamp does not fire and is not a divergence.
                        // See terrain-shade-bilinear-decode-2026-07-26.md.
                        int shadeValue = Math.Clamp(
                            (shadeRow + ((subX + 1) * shadeStep)) >> 8,
                            0,
                            63);
                        if (shadow is not null)
                        {
                            int shadowX = (pixelX * 8) / scale;
                            int shadowY = (pixelY * 8) / scale;
                            int shadowBit = (shadowY * 64) + shadowX;
                            if ((shadow[shadowBit >> 3] & (1 << (shadowBit & 7))) != 0)
                            {
                                shadeValue >>= 1;
                            }
                        }

                        LightingCoefficient light = _lighting[shadeValue];
                        uint red = color & 0xFF;
                        uint green = (color >> 8) & 0xFF;
                        uint blue = (color >> 16) & 0xFF;
                        block[(pixelY * blockSize) + pixelX] = (ushort)(
                            ((green * light.Green & 0x07E00000) +
                             (blue * light.Blue & 0x001F0000) +
                             (red * light.Red & 0xF8000000)) >> 16);
                    }
                }
            }
        }

        ApplyPineShadows(block, blockSize, level, tileX, tileY);
        int destinationX = slotX * blockSize;
        int destinationY = slotY * blockSize;
        for (int pixelY = 0; pixelY < blockSize; pixelY++)
        {
            int offset = (((destinationY + pixelY) * MapSize) + destinationX) * sizeof(ushort);
            for (int pixelX = 0; pixelX < blockSize; pixelX++)
            {
                ushort value = block[(pixelY * blockSize) + pixelX];
                destination[offset++] = (byte)value;
                destination[offset++] = (byte)(value >> 8);
            }
        }
    }

    private int Shade(int x, int y) => _hierarchy.Shade[(y * MapSize) + x];

    private void ApplyPineShadows(
        ushort[] block,
        int blockSize,
        int logicalLevel,
        int tileX,
        int tileY)
    {
        int scale = 1 << logicalLevel;
        int blockOriginX = tileX * TileWidth * scale;
        int blockOriginY = tileY * TileWidth * scale;
        for (int descriptorIndex = _hierarchy.Pines.Length - 1;
             descriptorIndex >= 0;
             descriptorIndex--)
        {
            PineShadow pine = _hierarchy.Pines[descriptorIndex];
            int alphaLevel = pine.RootLevel + logicalLevel;
            int dimension = 1 << alphaLevel;
            int localTopX = (pine.TopX * scale) - blockOriginX;
            int localTopY = (pine.TopY * scale) - blockOriginY;
            if (localTopX >= blockSize || localTopY >= blockSize ||
                localTopX + dimension <= 0 || localTopY + dimension <= 0)
            {
                continue;
            }

            int alphaOffset = s_pineLevelOffsets[alphaLevel];
            for (int sourceY = 0; sourceY < dimension; sourceY++)
            {
                int targetY = localTopY + sourceY;
                if ((uint)targetY >= blockSize)
                {
                    continue;
                }
                for (int sourceX = 0; sourceX < dimension; sourceX++)
                {
                    int targetX = localTopX + sourceX;
                    if ((uint)targetX >= blockSize)
                    {
                        continue;
                    }
                    int amount = _hierarchy.PineAlpha[
                        alphaOffset + (sourceY * dimension) + sourceX];
                    if (amount >= 32)
                    {
                        continue;
                    }
                    int target = (targetY * blockSize) + targetX;
                    uint destination = block[target];
                    uint pair = ((destination << 16) | destination) & 0x07E0F81F;
                    uint scaled = ((pair * (uint)amount) >> 5) & 0x07E0F81F;
                    block[target] = (ushort)((scaled >> 16) + scaled);
                }
            }
        }
    }

    private static uint LookupColor(MapTexture map, int material, int texel)
    {
        int paletteIndex = map.Indices[(material * map.Width * map.Width) + texel];
        return map.Palette[(material * PaletteEntriesPerMaterial) + paletteIndex];
    }

    private static uint BlendMaterial(uint color, uint candidate)
    {
        int difference = unchecked((int)(candidate - color));
        if (difference > 0x1FFFFFFF)
        {
            return candidate;
        }
        if (difference < 0)
        {
            return color;
        }

        uint blend = (uint)difference >> 26;
        return unchecked(
            ((((color & 0x00F8F8FF) * (7 - blend)) +
               ((candidate & 0x00F8F8FF) * blend)) >> 3) +
            (candidate & 0xFF000000));
    }

    private static LightingCoefficient[] BuildLightingGradient(uint sunColor, uint ambientColor)
    {
        int redBase = (int)((((ambientColor >> 16) & 0xFF) << 8) /
            (((sunColor >> 16) & 0xFE) + 1));
        int greenBase = (int)((ambientColor & 0xFF00) /
            (((sunColor >> 8) & 0xFE) + 1));
        int blueBase = (int)(((ambientColor & 0xFF) << 8) /
            ((sunColor & 0xFE) + 1));
        int red = redBase << 8;
        int green = greenBase << 8;
        int blue = blueBase << 8;
        var result = new LightingCoefficient[64];
        for (int index = 0; index < result.Length; index++)
        {
            uint redValue = (uint)Math.Min(((red >> 8) << 16) * 2, 0x00F80000) & 0x00F80000;
            uint greenValue = (uint)Math.Min(((green >> 8) << 11) * 2, 0x0007E000) & 0x0007E000;
            uint blueValue = (uint)Math.Min(((blue >> 3) & unchecked((int)0xFFFFFFE0)) * 2, 0x00001F00) & 0x00001F00;
            result[index] = new LightingCoefficient(redValue, greenValue, blueValue);
            red += (255 - redBase) * 4;
            green += (255 - greenBase) * 4;
            blue += (255 - blueBase) * 4;
        }
        return result;
    }

    private readonly record struct LightingCoefficient(uint Red, uint Green, uint Blue);

    private sealed record MapTexture(int Width, byte[] Indices, uint[] Palette);

    private sealed record TerrainCell(byte[] MaterialIds, byte[] Weights);

    private readonly record struct PineShadow(short TopX, short TopY, byte RootLevel);

    private sealed record TerrainHierarchy(
        MapTexture[] Maps,
        TerrainCell[] Cells,
        byte[] Shade,
        byte[]?[] Shadows,
        byte[] PineAlpha,
        PineShadow[] Pines)
    {
        public static TerrainHierarchy Load(byte[] source)
        {
            if (!StringComparer.Ordinal.Equals(
                    Convert.ToHexString(SHA256.HashData(source)),
                    HierarchySourceSha256))
            {
                throw new InvalidDataException(
                    "Level 100 terrain hierarchy does not match its retail-derived identity.");
            }

            using var stream = new MemoryStream(source, writable: false);
            using var reader = new BinaryReader(stream);
            if (!reader.ReadBytes(4).SequenceEqual("LTH1"u8.ToArray()) ||
                reader.ReadUInt32() != 1 ||
                reader.ReadUInt32() != 5)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid header.");
            }

            var maps = new MapTexture[5];
            for (int level = 0; level < maps.Length; level++)
            {
                int width = reader.ReadInt32();
                int expectedWidth = 16 << level;
                int dataLength = reader.ReadInt32();
                if (width != expectedWidth || dataLength != MaterialCount * width * width)
                {
                    throw new InvalidDataException(
                        $"Level 100 MAPT level {level} has invalid dimensions.");
                }
                byte[] indices = ReadExactly(reader, dataLength);
                int paletteLength = reader.ReadInt32();
                if (paletteLength != MaterialCount * PaletteEntriesPerMaterial)
                {
                    throw new InvalidDataException(
                        $"Level 100 MAPT level {level} has an invalid palette.");
                }
                var palette = new uint[paletteLength];
                for (int index = 0; index < palette.Length; index++)
                {
                    palette[index] = reader.ReadUInt32();
                }
                maps[level] = new MapTexture(width, indices, palette);
            }

            if (reader.ReadInt32() != TileCount)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid cell count.");
            }
            var cells = new TerrainCell[TileCount];
            for (int index = 0; index < cells.Length; index++)
            {
                int layerCount = reader.ReadByte();
                if (layerCount is < 1 or > 5)
                {
                    throw new InvalidDataException(
                        $"Level 100 terrain cell {index} has an invalid layer count.");
                }
                byte[] materialIds = ReadExactly(reader, layerCount);
                if (materialIds.Any(material => material >= MaterialCount))
                {
                    throw new InvalidDataException(
                        $"Level 100 terrain cell {index} has an invalid material.");
                }
                cells[index] = new TerrainCell(
                    materialIds,
                    ReadExactly(reader, layerCount * WeightCountPerLayer));
            }

            int shadeLength = reader.ReadInt32();
            if (shadeLength != MapSize * MapSize)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid shade map.");
            }
            byte[] shade = ReadExactly(reader, shadeLength);
            if (shade.Any(value => value > 63))
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid shade value.");
            }

            int shadowCount = reader.ReadInt32();
            if (shadowCount != 211)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid shadow count.");
            }
            var shadows = new byte[]?[TileCount];
            for (int index = 0; index < shadowCount; index++)
            {
                int tileIndex = reader.ReadUInt16();
                if ((uint)tileIndex >= TileCount || shadows[tileIndex] is not null)
                {
                    throw new InvalidDataException("Level 100 terrain hierarchy has an invalid shadow tile.");
                }
                shadows[tileIndex] = ReadExactly(reader, ShadowBytesPerTile);
            }

            int pineAlphaLength = reader.ReadInt32();
            if (pineAlphaLength != 5_461)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has invalid pine alpha.");
            }
            byte[] pineAlpha = ReadExactly(reader, pineAlphaLength);
            int pineCount = reader.ReadInt32();
            if (pineCount != 1_481)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has an invalid pine count.");
            }
            var pines = new PineShadow[pineCount];
            for (int index = 0; index < pines.Length; index++)
            {
                short topX = reader.ReadInt16();
                short topY = reader.ReadInt16();
                byte rootLevel = reader.ReadByte();
                if (rootLevel is < 1 or > 2)
                {
                    throw new InvalidDataException(
                        $"Level 100 pine shadow {index} has an invalid level.");
                }
                pines[index] = new PineShadow(topX, topY, rootLevel);
            }
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy has trailing data.");
            }
            return new TerrainHierarchy(maps, cells, shade, shadows, pineAlpha, pines);
        }

        private static byte[] ReadExactly(BinaryReader reader, int count)
        {
            byte[] result = reader.ReadBytes(count);
            if (result.Length != count)
            {
                throw new InvalidDataException("Level 100 terrain hierarchy is truncated.");
            }
            return result;
        }
    }
}
