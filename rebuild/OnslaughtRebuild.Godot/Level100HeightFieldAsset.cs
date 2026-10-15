// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.GodotClient;

internal readonly record struct Level100TerrainTileSelection(
    int TileX,
    int TileY,
    int GeometryLevel,
    int TextureLevel,
    int EdgeFlags);

internal sealed class Level100HeightFieldAsset
{
    public const float PlayerStartX =
        Level100Terrain.PlayerStartRetailXFixed /
        (float)Level100Terrain.FixedPointUnitsPerRetailUnit;
    public const float PlayerStartZ =
        Level100Terrain.PlayerStartRetailYFixed /
        (float)Level100Terrain.FixedPointUnitsPerRetailUnit;
    public const float PlayerStartElevation =
        Level100Terrain.PlayerStartReferenceElevationMillimeters / 1_000f;

    private const int TileCountPerAxis = 64;
    private const int TileCount = TileCountPerAxis * TileCountPerAxis;
    private const int TileWidth = 8;
    private const int RootGeometryLevel = -1;
    private const float CameraSmoothing = 0.03f;
    private const float FinestGeometryRadiusSquared = 32f * 32f;
    private const float RootTextureRadiusSquared = 128f * 128f;

    // CLandscapeIB edge bits: top, right, bottom, left.
    private const int StitchTop = 1;
    private const int StitchRight = 2;
    private const int StitchBottom = 4;
    private const int StitchLeft = 8;
    private const int StitchVariantCount = 16;

    private static readonly int[] s_rootTileIndices = BuildRetailTileIndices(1, 0);
    private static readonly int[][] s_patchTileIndexVariants =
        BuildRetailPatchIndexVariants();

    private readonly Level100Terrain _terrain;
    private readonly float[] _tileComplexity = new float[TileCount];
    private readonly float[] _tileMidHeight = new float[TileCount];
    private readonly int[] _geometryLevels = new int[TileCount];
    private readonly int[] _textureLevels = new int[TileCount];
    private readonly int[] _edgeFlags = new int[TileCount];
    private readonly List<Level100TerrainTileSelection> _selections = new(TileCount);
    private bool _hasSmoothedCamera;
    private Vector3 _smoothedCamera;
    private Vector3 _smoothedForward;
    private ulong _meshSignature;

    private Level100HeightFieldAsset(Level100Terrain terrain)
    {
        _terrain = terrain;
        MixerSet = terrain.MixerSet;
        SkyCube = terrain.SkyCube;
        DetailTexture = terrain.DetailTexture;
        WaterLevel = terrain.WaterLevel;
        WaterTexture = terrain.WaterTexture;
        FogColor = ToColor(terrain.FogColorRgb24);
        FogDensity = terrain.FogDensity;
        SunColor = ToColor(terrain.SunColorRgb24);
        AntiSunColor = ToColor(terrain.AntiSunColorRgb24);
        AmbientColor = ToColor(terrain.AmbientColorRgb24);
        SunColorRgb24 = terrain.SunColorRgb24;
        AntiSunColorRgb24 = terrain.AntiSunColorRgb24;
        AmbientColorRgb24 = terrain.AmbientColorRgb24;

        var beaSunPosition = new Vector3(
            terrain.SunPositionX,
            terrain.SunPositionY,
            terrain.SunPositionZ);
        SunPosition = beaSunPosition;
        Vector3 beaLightDirection = -beaSunPosition.Normalized();
        SunlightDirection = new Vector3(
            beaLightDirection.X,
            -beaLightDirection.Z,
            -beaLightDirection.Y);

        for (int tileY = 0; tileY < TileCountPerAxis; tileY++)
        {
            for (int tileX = 0; tileX < TileCountPerAxis; tileX++)
            {
                int tileIndex = TileIndex(tileX, tileY);
                _tileComplexity[tileIndex] = terrain.GetTileComplexityScore(tileX, tileY);
                int minimum = int.MaxValue;
                int maximum = int.MinValue;
                for (int localY = 0; localY <= TileWidth; localY++)
                {
                    for (int localX = 0; localX <= TileWidth; localX++)
                    {
                        int sample = terrain.SampleGridHeightUnits(
                            (tileX * TileWidth) + localX,
                            (tileY * TileWidth) + localY);
                        minimum = Math.Min(minimum, sample);
                        maximum = Math.Max(maximum, sample);
                    }
                }
                _tileMidHeight[tileIndex] = ((minimum + maximum) * 0.5f) * terrain.HeightScale;
            }
        }
    }

    public ArrayMesh Mesh { get; } = new();

    public int VertexCount { get; private set; }

    public int TriangleCount { get; private set; }

    public byte MixerSet { get; }

    public byte SkyCube { get; }

    public byte DetailTexture { get; }

    public float WaterLevel { get; }

    /// <summary>
    /// The water support of the same clamp: `FLD [0x006FBDFC]` at 0x004F3549
    /// compared against `[ESI + 0x24]` and stored by `FSTP [ESI + 0x24]` at
    /// 0x004F3559, again with no second term. `CHFD + 0x1034` holds -8.84.
    /// </summary>
    public float WaterRelativeHeight => PlayerStartElevation - WaterLevel;

    public byte WaterTexture { get; }

    public Color FogColor { get; }

    public float FogDensity { get; }

    public Color SunColor { get; }

    public Color AntiSunColor { get; }

    public Color AmbientColor { get; }

    public uint SunColorRgb24 { get; }

    public uint AntiSunColorRgb24 { get; }

    public uint AmbientColorRgb24 { get; }

    public Vector3 SunlightDirection { get; }

    /// <summary>
    /// The level's authored sun vector in the engine's own basis, exactly as
    /// <c>CHFD + 0x10A4</c> holds it and un-normalised.
    ///
    /// <para><see cref="SunlightDirection"/> is the negated, normalised, Godot-
    /// basis form used for shading. The sun FLARE needs the raw vector instead,
    /// because <c>references/Onslaught/DXEngine.cpp:1043-1046</c> scales
    /// <c>FVector(hfp.SunPosX, hfp.SunPosY, hfp.SunPosZ)</c> by <c>SUN_SCALE</c>
    /// without normalising it - so on a level whose sun vector is not unit
    /// length the flare distance follows the authored magnitude. Level 100's is
    /// unit length to within a float epsilon.</para>
    /// </summary>
    public Vector3 SunPosition { get; }

    public static Level100HeightFieldAsset Load() => new(Level100Terrain.Instance);

    /// <summary>
    /// The ground support used by the released static placement clamp. This is
    /// the up-positive form of the height `CThing__Init` @ 0x004F34A0 obtains
    /// from `CALL 0x0047EB80` at 0x004F34FB and compares against the authored
    /// Z-down position at `[ESI + 0x24]`. The clamp stores that height into the
    /// pivot unchanged (`FSTP [ESP + 0x14]` at 0x004F3529), so callers add no
    /// mesh-derived term to the value returned here.
    /// </summary>
    public float SampleRelativeHeight(float relativeX, float relativeZ) =>
        PlayerStartElevation - SampleRetailHeight(relativeX + PlayerStartX, relativeZ + PlayerStartZ);

    public IReadOnlyList<Level100TerrainTileSelection> Update(Camera3D camera)
    {
        Vector3 cameraPosition = camera.GlobalPosition;
        Vector3 cameraForward = -camera.GlobalTransform.Basis.Z;
        var retailCamera = new Vector3(
            cameraPosition.X + PlayerStartX,
            PlayerStartZ - cameraPosition.Z,
            PlayerStartElevation - cameraPosition.Y);
        var retailForward = new Vector3(
            cameraForward.X,
            -cameraForward.Z,
            -cameraForward.Y);

        if (!_hasSmoothedCamera)
        {
            _smoothedCamera = retailCamera;
            _smoothedForward = retailForward;
            _hasSmoothedCamera = true;
        }
        else
        {
            _smoothedCamera = _smoothedCamera.Lerp(retailCamera, CameraSmoothing);
            _smoothedForward = _smoothedForward.Lerp(retailForward, CameraSmoothing);
        }

        SelectTiles();
        ulong signature = ComputeSignature();
        if (signature != _meshSignature || Mesh.GetSurfaceCount() == 0)
        {
            BuildSelectedMesh();
            _meshSignature = signature;
        }
        return _selections;
    }

    private void SelectTiles()
    {
        for (int tileY = 0; tileY < TileCountPerAxis; tileY++)
        {
            for (int tileX = 0; tileX < TileCountPerAxis; tileX++)
            {
                int tileIndex = TileIndex(tileX, tileY);
                float offsetX = ((tileX * TileWidth) + (TileWidth * 0.5f)) - _smoothedCamera.X;
                float offsetY = ((tileY * TileWidth) + (TileWidth * 0.5f)) - _smoothedCamera.Y;
                float offsetHeight = _tileMidHeight[tileIndex] - _smoothedCamera.Z;
                float distanceSquared =
                    (offsetX * offsetX) +
                    (offsetY * offsetY) +
                    (offsetHeight * offsetHeight);

                float projectedSize = 8f;
                if (distanceSquared >= FinestGeometryRadiusSquared)
                {
                    projectedSize =
                        (256f + _tileComplexity[tileIndex]) /
                        MathF.Sqrt(distanceSquared);
                }
                int roundedSize = Math.Clamp((int)MathF.Round(projectedSize), 0, 8);
                int geometryLevel = roundedSize switch
                {
                    <= 1 => RootGeometryLevel,
                    <= 3 => 0,
                    <= 7 => 1,
                    _ => 2,
                };
                _geometryLevels[tileIndex] = geometryLevel;
                _textureLevels[tileIndex] = geometryLevel == RootGeometryLevel
                    ? 0
                    : SelectTextureLevel(offsetX, offsetY, offsetHeight, distanceSquared);
            }
        }

        Array.Clear(_edgeFlags);
        for (int tileY = 0; tileY < TileCountPerAxis; tileY++)
        {
            for (int tileX = 0; tileX < TileCountPerAxis; tileX++)
            {
                int tileIndex = TileIndex(tileX, tileY);
                int level = _geometryLevels[tileIndex];
                if (level == RootGeometryLevel)
                {
                    continue;
                }

                int flags = 0;
                if (tileY > 0 && _geometryLevels[TileIndex(tileX, tileY - 1)] < level)
                {
                    flags |= StitchTop;
                }
                if (tileX + 1 < TileCountPerAxis &&
                    _geometryLevels[TileIndex(tileX + 1, tileY)] < level)
                {
                    flags |= StitchRight;
                }
                if (tileY + 1 < TileCountPerAxis &&
                    _geometryLevels[TileIndex(tileX, tileY + 1)] < level)
                {
                    flags |= StitchBottom;
                }
                if (tileX > 0 && _geometryLevels[TileIndex(tileX - 1, tileY)] < level)
                {
                    flags |= StitchLeft;
                }
                _edgeFlags[tileIndex] = flags;
            }
        }

        _selections.Clear();
        for (int tileY = 0; tileY < TileCountPerAxis; tileY++)
        {
            for (int tileX = 0; tileX < TileCountPerAxis; tileX++)
            {
                int tileIndex = TileIndex(tileX, tileY);
                _selections.Add(new Level100TerrainTileSelection(
                    tileX,
                    tileY,
                    _geometryLevels[tileIndex],
                    _textureLevels[tileIndex],
                    _edgeFlags[tileIndex]));
            }
        }
    }

    private int SelectTextureLevel(
        float offsetX,
        float offsetY,
        float offsetHeight,
        float distanceSquared)
    {
        if (distanceSquared > RootTextureRadiusSquared)
        {
            return 0;
        }

        float verticalTerm = Math.Max((offsetHeight * offsetHeight) - 64f, 0f);
        if (ShiftedDistanceSquared(offsetX, offsetY, verticalTerm, 60f) >= 64f * 64f)
        {
            return 1;
        }
        if (ShiftedDistanceSquared(offsetX, offsetY, verticalTerm, 28f) >= 32f * 32f)
        {
            return 2;
        }
        if (ShiftedDistanceSquared(offsetX, offsetY, verticalTerm, 12f) >= 16f * 16f)
        {
            return 3;
        }
        return 4;
    }

    private float ShiftedDistanceSquared(
        float offsetX,
        float offsetY,
        float verticalTerm,
        float forwardDistance)
    {
        float shiftedX = offsetX - (_smoothedForward.X * forwardDistance);
        float shiftedY = offsetY - (_smoothedForward.Y * forwardDistance);
        return (shiftedX * shiftedX) + (shiftedY * shiftedY) + verticalTerm;
    }

    private ulong ComputeSignature()
    {
        const ulong offsetBasis = 14_695_981_039_346_656_037UL;
        const ulong prime = 1_099_511_628_211UL;
        ulong result = offsetBasis;
        for (int index = 0; index < TileCount; index++)
        {
            result ^= (uint)(_geometryLevels[index] + 1);
            result *= prime;
            result ^= (uint)_textureLevels[index];
            result *= prime;
            result ^= (uint)_edgeFlags[index];
            result *= prime;
        }
        return result;
    }

    private void BuildSelectedMesh()
    {
        var vertices = new List<Vector3>(40_000);
        var textureCoordinates = new List<Vector2>(40_000);
        var textureLevels = new List<Vector2>(40_000);
        var indices = new List<int>(120_000);

        foreach (Level100TerrainTileSelection selection in _selections)
        {
            int step = selection.GeometryLevel == RootGeometryLevel
                ? TileWidth
                : 4 >> selection.GeometryLevel;
            int cellCount = TileWidth / step;
            int vertexCountPerAxis = cellCount + 1;
            int baseVertex = vertices.Count;
            int retailOriginX = selection.TileX * TileWidth;
            int retailOriginY = selection.TileY * TileWidth;

            for (int localY = 0; localY <= cellCount; localY++)
            {
                int retailY = retailOriginY + (localY * step);
                for (int localX = 0; localX <= cellCount; localX++)
                {
                    int retailX = retailOriginX + (localX * step);
                    float height = PlayerStartElevation - SampleGridRetailHeight(retailX, retailY);
                    vertices.Add(new Vector3(
                        retailX - PlayerStartX,
                        height,
                        PlayerStartZ - retailY));
                    textureCoordinates.Add(new Vector2(retailX, retailY));
                    textureLevels.Add(new Vector2(selection.TextureLevel, 0f));
                }
            }

            foreach (int localIndex in GetRetailTileIndices(
                         selection.GeometryLevel,
                         selection.EdgeFlags))
            {
                indices.Add(baseVertex + localIndex);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = textureCoordinates.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.TexUV2] = textureLevels.ToArray();
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices.ToArray();

        Mesh.ClearSurfaces();
        Mesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);
        VertexCount = vertices.Count;
        TriangleCount = indices.Count / 3;
    }

    private static ReadOnlySpan<int> GetRetailTileIndices(
        int geometryLevel,
        int edgeFlags) =>
        geometryLevel == RootGeometryLevel
            ? s_rootTileIndices
            : s_patchTileIndexVariants[(geometryLevel * StitchVariantCount) + edgeFlags];

    private static int[][] BuildRetailPatchIndexVariants()
    {
        var result = new int[3 * StitchVariantCount][];
        for (int geometryLevel = 0; geometryLevel < 3; geometryLevel++)
        {
            int cellCount = 2 << geometryLevel;
            for (int edgeFlags = 0; edgeFlags < StitchVariantCount; edgeFlags++)
            {
                result[(geometryLevel * StitchVariantCount) + edgeFlags] =
                    BuildRetailTileIndices(cellCount, edgeFlags);
            }
        }
        return result;
    }

    private static int[] BuildRetailTileIndices(
        int cellCount,
        int edgeFlags)
    {
        int vertexCountPerAxis = cellCount + 1;
        var working = new int[cellCount * cellCount * 6];
        int write = 0;
        for (int localY = 0; localY < cellCount; localY++)
        {
            for (int localX = 0; localX < cellCount; localX++)
            {
                int topLeft = (localY * vertexCountPerAxis) + localX;
                int bottomLeft = topLeft + vertexCountPerAxis;
                int bottomRight = bottomLeft + 1;
                working[write++] = topLeft;
                working[write++] = bottomLeft;
                working[write++] = bottomRight;
                working[write++] = topLeft;
                working[write++] = bottomRight;
                working[write++] = topLeft + 1;
            }
        }

        if ((edgeFlags & StitchTop) != 0)
        {
            int cursor = 6;
            for (int pair = 0; pair < cellCount / 2; pair++)
            {
                working[cursor - 1]++;
                working[cursor]++;
                ClearTriangle(working, cursor + 3);
                cursor += 12;
            }
        }

        if ((edgeFlags & StitchRight) != 0)
        {
            int cursor = (cellCount * 6) - 6;
            for (int pair = 0; pair < cellCount / 2; pair++)
            {
                working[cursor + 2] -= vertexCountPerAxis;
                working[cursor + (cellCount * 6) + 5] -= vertexCountPerAxis;
                ClearTriangle(working, cursor + 3);
                cursor += cellCount * 12;
            }
            if ((edgeFlags & StitchTop) != 0)
            {
                ClearTriangle(working, (cellCount * 6) - 6);
            }
        }

        if ((edgeFlags & StitchLeft) != 0)
        {
            int cursor = 0;
            for (int pair = 0; pair < cellCount / 2; pair++)
            {
                working[cursor + 1] += vertexCountPerAxis;
                working[cursor + (cellCount * 6) + 3] += vertexCountPerAxis;
                ClearTriangle(working, cursor + (cellCount * 6));
                cursor += cellCount * 12;
            }
        }

        if ((edgeFlags & StitchBottom) != 0)
        {
            int cursor = (cellCount - 1) * cellCount * 6;
            for (int pair = 0; pair < cellCount / 2; pair++)
            {
                working[cursor + 4]--;
                working[cursor + 7]--;
                ClearTriangle(working, cursor);
                cursor += 12;
            }
            if ((edgeFlags & StitchLeft) != 0)
            {
                ClearTriangle(working, ((cellCount - 1) * cellCount * 6) + 3);
            }
        }

        var result = new List<int>(working.Length);
        for (int index = 0; index < working.Length; index += 3)
        {
            if (working[index] == 0 && working[index + 1] == 0 && working[index + 2] == 0)
            {
                continue;
            }
            result.Add(working[index]);
            result.Add(working[index + 1]);
            result.Add(working[index + 2]);
        }
        return result.ToArray();
    }

    private static void ClearTriangle(int[] indices, int start)
    {
        indices[start] = 0;
        indices[start + 1] = 0;
        indices[start + 2] = 0;
    }

    private float SampleRetailHeight(float x, float z)
    {
        int fixedX = ToFixedCoordinate(x);
        int fixedZ = ToFixedCoordinate(z);
        return _terrain.SampleHeightUnitsAtFixed(fixedX, fixedZ) * _terrain.HeightScale;
    }

    private float SampleGridRetailHeight(int x, int z) =>
        _terrain.SampleGridHeightUnits(x, z) * _terrain.HeightScale;

    private static int ToFixedCoordinate(float coordinate)
    {
        float clamped = Math.Clamp(
            coordinate,
            0f,
            MathF.BitDecrement(Level100Terrain.MapExtentRetailUnits));
        return (int)MathF.Floor(clamped * Level100Terrain.FixedPointUnitsPerRetailUnit);
    }

    private static int TileIndex(int tileX, int tileY) =>
        (tileY * TileCountPerAxis) + tileX;

    private static Color ToColor(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f);
}
