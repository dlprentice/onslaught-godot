// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;

namespace OnslaughtRebuild.Core;

/// <summary>
/// The one retained Level 100 HFLD and the released Steam height sampler used
/// for deterministic simulation. Presentation clients may adapt its metadata,
/// but do not own a second terrain sampler.
/// </summary>
public sealed class Level100Terrain
{
    public const string SourceSha256 =
        "7A4C7C5B9400E2C8D2325CECB5C44701CD8A6E6F8609CBC8BC31D449C0620F5D";
    public const int FixedPointUnitsPerRetailUnit = 256;
    public const int MapExtentRetailUnits = 512;
    public const int PlayerStartRetailXFixed = 73_904;
    public const int PlayerStartRetailYFixed = 62_272;
    public const int PlayerStartReferenceElevationMillimeters = -10_000;
    public const int WalkerCenterOfGravityMillimeters = 1_900;
    public const int WaterElevationMillimeters = -1_160;
    public const int MinimumRelativeXMillimeters = -288_688;
    public const int MaximumRelativeXMillimeters = 223_313;
    public const int MinimumRelativeZMillimeters = -243_250;
    public const int MaximumRelativeZMillimeters = 268_750;

    private const string ResourceName =
        "OnslaughtRebuild.Core.Assets.Level100.level100-heightfield.hfld.bin";
    private const int HfldPayloadSize = 668_652;
    private const int ChfdPayloadSize = 5_084;
    private const int HfdtPayloadSize = 663_552;
    private const int HeightScaleOffset = 0x102C;
    private const int MixerSetOffset = 0x1030;
    private const int WaterLevelOffset = 0x1034;
    private const int FogColorOffset = 0x1078;
    private const int SunColorOffset = 0x107C;
    private const int AntiSunColorOffset = 0x1080;
    private const int AmbientColorOffset = 0x108C;
    private const int SkyCubeOffset = 0x1090;
    private const int DetailTextureOffset = 0x1094;
    private const int WaterTextureOffset = 0x1095;
    private const int FogDensityOffset = 0x1098;
    private const int SunPositionOffset = 0x10A4;
    private const int TileCountPerAxis = 64;
    private const int TileWidth = 8;
    private const int SamplesPerTileAxis = 9;
    private const int SamplesPerTile = SamplesPerTileAxis * SamplesPerTileAxis;
    private const int MaximumFixedCoordinate =
        MapExtentRetailUnits * FixedPointUnitsPerRetailUnit;

    private static readonly uint s_hfldTag = Tag("HFLD");
    private static readonly uint s_chfdTag = Tag("CHFD");
    private static readonly uint s_hfdtTag = Tag("HFDT");

    private readonly short[] _heightSamples;
    private readonly int _heightScaleSignificand;
    private readonly long _heightScaleDenominator;
    private HeightBounds? _heightBounds;

    private Level100Terrain(
        short[] heightSamples,
        int heightScaleBits,
        byte mixerSet,
        byte skyCube,
        byte detailTexture,
        float waterLevel,
        byte waterTexture,
        uint fogColor,
        float fogDensity,
        uint sunColor,
        uint antiSunColor,
        uint ambientColor,
        float sunPositionX,
        float sunPositionY,
        float sunPositionZ,
        string payloadSha256)
    {
        _heightSamples = heightSamples;
        PayloadSha256 = payloadSha256;
        HeightScale = BitConverter.Int32BitsToSingle(heightScaleBits);
        (_heightScaleSignificand, _heightScaleDenominator) =
            DecodePositiveFloatScale(heightScaleBits);
        MixerSet = mixerSet;
        SkyCube = skyCube;
        DetailTexture = detailTexture;
        WaterLevel = waterLevel;
        WaterTexture = waterTexture;
        FogColorRgb24 = fogColor;
        FogDensity = fogDensity;
        SunColorRgb24 = sunColor;
        AntiSunColorRgb24 = antiSunColor;
        AmbientColorRgb24 = ambientColor;
        SunPositionX = sunPositionX;
        SunPositionY = sunPositionY;
        SunPositionZ = sunPositionZ;
    }

    public static Level100Terrain Instance { get; } = LoadEmbedded();

    /// <summary>
    /// The world-110 heightfield, admitted by the same envelope/hash law as
    /// <see cref="Instance"/>: extracted HFLD envelope (tag + CHFD + HFDT)
    /// from <c>data/resources/110_res_PC.aya</c>, SHA-256
    /// <c>FD4D076A…33C2</c>, materialized and embedded next to the Level 100
    /// resource. Not consumed by any live simulation yet — the world-110
    /// session owner does not exist — but the deterministic Core owner and its
    /// hash pin land with the world-110 admission work so the future session
    /// cannot invent a second sampler shape.
    /// </summary>
    public static Level100Terrain World110 { get; } = LoadEmbeddedWorld(
        110, World110SourceSha256);

    /// <summary>
    /// The world-110 heightfield's SHA-256, measured from the pinned
    /// <c>data/resources/110_res_PC.aya</c> on 2026-08-22. Deliberately not
    /// Level 100's <see cref="SourceSha256"/>: the two worlds are distinct
    /// released envelopes under the same format law.
    /// </summary>
    public const string World110SourceSha256 =
        "FD4D076A2926FBC473B7D364703BDBC0C8A0F7A638B0AB71B6F319374DA033C2";

    /// <summary>
    /// The world-200 heightfield, admitted by the same envelope/hash law:
    /// the extracted HFLD envelope from
    /// <c>data/resources/200_res_PC.aya</c>, SHA-256 <c>1B8EB858…B945</c>.
    /// The whole-image owner scan places it inside ERES, as it does for worlds
    /// 100 and 110; the payload length is the same 668,652-byte envelope law,
    /// so this loader admits it unchanged.
    /// Not consumed by any live simulation yet — the world-200 session owner
    /// does not exist — but the deterministic Core owner and its hash pin
    /// land with the world-200 admission work.
    /// </summary>
    public static Level100Terrain World200 { get; } = LoadEmbeddedWorld(
        200, World200SourceSha256);

    /// <summary>
    /// The world-200 heightfield's SHA-256, measured from the pinned
    /// <c>data/resources/200_res_PC.aya</c> (archive SHA-256
    /// <c>99dbd433…b77</c>) on 2026-08-22. Distinct from Level 100's and
    /// world 110's envelopes under the same format law.
    /// </summary>
    public const string World200SourceSha256 =
        "1B8EB8584BE552383F10B08C75D9F10E91708343F0E5EE085D5130D369F6B945";

    /// <summary>
    /// World 300's independently measured HFLD envelope, extracted from ERES
    /// in <c>data/resources/300_res_PC.aya</c> (archive SHA-256
    /// <c>7293bcbe…9efe4</c>). Its payload retains the common 668,652-byte HFLD
    /// law even though the enclosing RLWD header is a distinct three-name
    /// variant. No live simulation consumes this terrain yet.
    /// </summary>
    public static Level100Terrain World300 { get; } = LoadEmbeddedWorld(
        300, World300SourceSha256);

    /// <summary>The exact framed world-300 HFLD envelope SHA-256.</summary>
    public const string World300SourceSha256 =
        "68A181F9EC3099A0BE52BF4A063350A35E43C20664F2E07572BDB44D472ACB1A";

    /// <summary>
    /// The SHA-256 of the envelope bytes this instance was actually loaded
    /// from — the same string the loader compared against its expected pin
    /// (the per-world <c>SourceSha256</c> / <c>World110SourceSha256</c>
    /// constant). Records exactly which measured world each static admits.
    /// </summary>
    public string PayloadSha256 { get; }

    public float HeightScale { get; }

    public byte MixerSet { get; }

    public byte SkyCube { get; }

    public byte DetailTexture { get; }

    public float WaterLevel { get; }

    public byte WaterTexture { get; }

    public uint FogColorRgb24 { get; }

    public float FogDensity { get; }

    public uint SunColorRgb24 { get; }

    public uint AntiSunColorRgb24 { get; }

    public uint AmbientColorRgb24 { get; }

    public float SunPositionX { get; }

    public float SunPositionY { get; }

    public float SunPositionZ { get; }

    /// <summary>
    /// Samples the signed HFLD unit used by Steam at 0x0047EB80. Coordinates
    /// are unsigned 24.8 fixed point; interpolation truncates after each axis.
    /// Outside the 512-unit map, the released helper returns the flat zero.
    /// </summary>
    public int SampleHeightUnitsAtFixed(int retailXFixed, int retailYFixed)
    {
        if ((uint)retailXFixed >= MaximumFixedCoordinate ||
            (uint)retailYFixed >= MaximumFixedCoordinate)
        {
            return 0;
        }

        int tileX = (retailXFixed >> 11) & 0x3F;
        int tileY = (retailYFixed >> 11) & 0x3F;
        int localX = (retailXFixed >> 8) & 0x07;
        int localY = (retailYFixed >> 8) & 0x07;
        int sampleIndex =
            (((tileX * TileCountPerAxis) + tileY) * SamplesPerTile) +
            (localY * SamplesPerTileAxis) +
            localX;
        int fractionX = retailXFixed & 0xFF;
        int fractionY = retailYFixed & 0xFF;

        int nearLeft = _heightSamples[sampleIndex];
        int near = nearLeft +
            (((_heightSamples[sampleIndex + 1] - nearLeft) * fractionX) >> 8);
        int farLeft = _heightSamples[sampleIndex + SamplesPerTileAxis];
        int far = farLeft +
            (((_heightSamples[sampleIndex + SamplesPerTileAxis + 1] - farLeft) * fractionX) >> 8);
        return near + (((far - near) * fractionY) >> 8);
    }

    /// <summary>
    /// Returns an exact integer HFLD lattice sample, including the 512-unit
    /// far edge. The released heightfield stores one sample per retail unit.
    /// </summary>
    public int SampleGridHeightUnits(int retailX, int retailY)
    {
        if ((uint)retailX > MapExtentRetailUnits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retailX),
                "Level 100 grid coordinates must be between 0 and 512.");
        }
        if ((uint)retailY > MapExtentRetailUnits)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retailY),
                "Level 100 grid coordinates must be between 0 and 512.");
        }

        int tileX = Math.Min(retailX / TileWidth, TileCountPerAxis - 1);
        int tileY = Math.Min(retailY / TileWidth, TileCountPerAxis - 1);
        int localX = retailX == MapExtentRetailUnits ? TileWidth : retailX % TileWidth;
        int localY = retailY == MapExtentRetailUnits ? TileWidth : retailY % TileWidth;
        int sampleIndex =
            (((tileX * TileCountPerAxis) + tileY) * SamplesPerTile) +
            (localY * SamplesPerTileAxis) +
            localX;
        return _heightSamples[sampleIndex];
    }

    /// <summary>
    /// Released integer lookup 47ea20 used by the AirGuide clearance scan.
    /// Its mask aliases 512..1023 to edge arms and ignores high bits. The X
    /// edge really shifts Y by EIGHT; this is distinct from a regular lattice
    /// sample or a clamp. The signed interpretation belongs to this consumer.
    /// </summary>
    public int SampleAirGuideHeightUnits(int retailX, int retailY)
    {
        int xMask = retailX & 0x3ffe00, yMask = retailY & 0x3ffe00;
        int index;
        if ((xMask | yMask) == 0)
            index = 81 * (64 * ((retailX >> 3) & 63) + ((retailY >> 3) & 63)) +
                9 * (retailY & 7) + (retailX & 7);
        else if (xMask == 512 && yMask == 512) index = 331775;
        else if (xMask == 512) index = 326600 + 81 * ((retailY >> 8) & 63) + 9 * (retailY & 7);
        else if (yMask == 512) index = 5175 + 5184 * ((retailX >> 3) & 63) + (retailX & 7);
        else return 0;
        return _heightSamples[index];
    }

    /// <summary>
    /// Returns the released PC patch-complexity score for one 8x8 terrain
    /// tile. Steam evaluates the missing midpoint error at 2-, 4-, and 8-unit
    /// subdivisions, retains the greatest integer average, applies the HFLD
    /// height scale, then the fixed 128 projection factor. Presentation clients
    /// use this value for terrain geometry LOD; simulation does not consume it.
    /// </summary>
    public float GetTileComplexityScore(int tileX, int tileY)
    {
        if ((uint)tileX >= TileCountPerAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(tileX));
        }
        if ((uint)tileY >= TileCountPerAxis)
        {
            throw new ArgumentOutOfRangeException(nameof(tileY));
        }

        int greatestAverage = 0;
        for (int step = 2; step <= TileWidth; step <<= 1)
        {
            int halfStep = step >> 1;
            int cellCount = TileWidth / step;
            int errorSum = 0;

            for (int localY = 0; localY < TileWidth; localY += step)
            {
                for (int localX = 0; localX < TileWidth; localX += step)
                {
                    int topLeft = TileSample(tileX, tileY, localX, localY);
                    errorSum += Math.Abs(
                        ((topLeft + TileSample(
                            tileX,
                            tileY,
                            localX + step,
                            localY + step)) / 2) -
                        TileSample(
                            tileX,
                            tileY,
                            localX + halfStep,
                            localY + halfStep));
                    errorSum += Math.Abs(
                        ((topLeft + TileSample(
                            tileX,
                            tileY,
                            localX + step,
                            localY)) / 2) -
                        TileSample(
                            tileX,
                            tileY,
                            localX + halfStep,
                            localY));
                    errorSum += Math.Abs(
                        ((topLeft + TileSample(
                            tileX,
                            tileY,
                            localX,
                            localY + step)) / 2) -
                        TileSample(
                            tileX,
                            tileY,
                            localX,
                            localY + halfStep));
                }

                errorSum += Math.Abs(
                    ((TileSample(tileX, tileY, TileWidth, localY) +
                      TileSample(tileX, tileY, TileWidth, localY + step)) / 2) -
                    TileSample(tileX, tileY, TileWidth, localY + halfStep));
            }

            for (int localX = 0; localX < TileWidth; localX += step)
            {
                errorSum += Math.Abs(
                    ((TileSample(tileX, tileY, localX, TileWidth) +
                      TileSample(tileX, tileY, localX + step, TileWidth)) / 2) -
                    TileSample(tileX, tileY, localX + halfStep, TileWidth));
            }

            int average = errorSum / ((cellCount * 3 + 2) * cellCount);
            greatestAverage = Math.Max(greatestAverage, average);
        }

        float scaled = HeightScale * greatestAverage;
        return scaled * 128f;
    }

    /// <summary>
    /// Returns Level 100 ground elevation relative to the authored -10-unit
    /// presentation plane. Core positions are millimetres from player start.
    /// </summary>
    public int SampleGroundElevationMillimeters(SimVector2 relativePosition)
    {
        (int retailXFixed, int retailYFixed) =
            GetRetailFixedCoordinates(relativePosition);
        return SampleGroundElevationMillimetersAtFixed(
            retailXFixed,
            retailYFixed);
    }

    internal (int X, int Y) GetRetailFixedCoordinates(
        SimVector2 relativePosition) =>
        (
            checked(
                PlayerStartRetailXFixed +
                (int)FloorDivide(
                    (long)relativePosition.X * FixedPointUnitsPerRetailUnit,
                    1_000)),
            checked(
                PlayerStartRetailYFixed +
                (int)FloorDivide(
                    (long)relativePosition.Z * FixedPointUnitsPerRetailUnit,
                    1_000)));

    internal int SampleGroundElevationMillimetersAtFixed(
        int retailXFixed,
        int retailYFixed) =>
        GroundElevationMillimetersFromHeightUnits(
            SampleHeightUnitsAtFixed(retailXFixed, retailYFixed));

    private int GroundElevationMillimetersFromHeightUnits(int heightUnits)
    {
        long relativeGroundNumerator =
            ((long)PlayerStartReferenceElevationMillimeters * _heightScaleDenominator) -
            ((long)heightUnits * _heightScaleSignificand * 1_000L);
        return checked((int)RoundDivideAwayFromZero(
            relativeGroundNumerator,
            _heightScaleDenominator));
    }

    /// <summary>
    /// An upper bound on <see cref="SampleGroundElevationMillimetersAtFixed"/>
    /// anywhere in the fixed-coordinate rectangle [x0, x1] x [y0, y1]. Each
    /// sample interpolates, with truncation, between the four samples of its
    /// own tile's lattice cell, so it stays within their range; outside the
    /// map the sampler returns height zero; and the elevation conversion is
    /// monotonic in height units. Sweeps use this only to skip spans that
    /// cannot touch the ground, never to decide a contact.
    /// </summary>
    internal int MaximumGroundElevationMillimetersAtFixed(int x0, int y0, int x1, int y1)
    {
        int minimumUnits = int.MaxValue;
        int maximumUnits = int.MinValue;
        if (x0 < 0 || y0 < 0 || x1 >= MaximumFixedCoordinate || y1 >= MaximumFixedCoordinate)
        {
            minimumUnits = 0;
            maximumUnits = 0;
        }

        int cellX0 = Math.Max(x0, 0) >> 8;
        int cellY0 = Math.Max(y0, 0) >> 8;
        int cellX1 = Math.Min(x1, MaximumFixedCoordinate - 1) >> 8;
        int cellY1 = Math.Min(y1, MaximumFixedCoordinate - 1) >> 8;
        if (x1 >= 0 && y1 >= 0 && x0 < MaximumFixedCoordinate && y0 < MaximumFixedCoordinate)
        {
            _heightBounds ??= new HeightBounds(_heightSamples);
            (int minimum, int maximum) = _heightBounds.Query(cellX0, cellY0, cellX1, cellY1);
            minimumUnits = Math.Min(minimumUnits, minimum);
            maximumUnits = Math.Max(maximumUnits, maximum);
        }

        return Math.Max(
            GroundElevationMillimetersFromHeightUnits(minimumUnits),
            GroundElevationMillimetersFromHeightUnits(maximumUnits));
    }

    /// <summary>
    /// Minimum and maximum HFLD height units over power-of-two blocks of the
    /// 512 x 512 lattice cells, each cell bounded by the four samples its own
    /// tile stores (a tile's edge samples need not equal its neighbour's).
    /// </summary>
    private sealed class HeightBounds
    {
        private const int Cells = MapExtentRetailUnits;
        private readonly short[][] _minimum;
        private readonly short[][] _maximum;

        internal HeightBounds(short[] samples)
        {
            int levels = 1;
            while ((Cells >> (levels - 1)) > 1)
            {
                levels++;
            }

            _minimum = new short[levels][];
            _maximum = new short[levels][];
            _minimum[0] = new short[Cells * Cells];
            _maximum[0] = new short[Cells * Cells];
            for (int cellX = 0; cellX < Cells; cellX++)
            {
                for (int cellY = 0; cellY < Cells; cellY++)
                {
                    int index =
                        ((((cellX >> 3) * TileCountPerAxis) + (cellY >> 3)) * SamplesPerTile) +
                        ((cellY & 7) * SamplesPerTileAxis) +
                        (cellX & 7);
                    short a = samples[index];
                    short b = samples[index + 1];
                    short c = samples[index + SamplesPerTileAxis];
                    short d = samples[index + SamplesPerTileAxis + 1];
                    _minimum[0][(cellX * Cells) + cellY] = Math.Min(Math.Min(a, b), Math.Min(c, d));
                    _maximum[0][(cellX * Cells) + cellY] = Math.Max(Math.Max(a, b), Math.Max(c, d));
                }
            }

            for (int level = 1; level < levels; level++)
            {
                int side = Cells >> level;
                int childSide = side * 2;
                _minimum[level] = new short[side * side];
                _maximum[level] = new short[side * side];
                for (int x = 0; x < side; x++)
                {
                    for (int y = 0; y < side; y++)
                    {
                        int c00 = ((2 * x) * childSide) + (2 * y);
                        int c10 = c00 + childSide;
                        short[] minimum = _minimum[level - 1];
                        short[] maximum = _maximum[level - 1];
                        _minimum[level][(x * side) + y] = Math.Min(
                            Math.Min(minimum[c00], minimum[c00 + 1]),
                            Math.Min(minimum[c10], minimum[c10 + 1]));
                        _maximum[level][(x * side) + y] = Math.Max(
                            Math.Max(maximum[c00], maximum[c00 + 1]),
                            Math.Max(maximum[c10], maximum[c10 + 1]));
                    }
                }
            }
        }

        /// <summary>
        /// Bounds over the cell rectangle [x0, x1] x [y0, y1] from at most
        /// four blocks of the finest level at which it spans two blocks per
        /// axis; the blocks cover a superset, so the bounds are conservative.
        /// </summary>
        internal (int Minimum, int Maximum) Query(int x0, int y0, int x1, int y1)
        {
            int level = 0;
            while (((x1 >> level) - (x0 >> level)) > 1 || ((y1 >> level) - (y0 >> level)) > 1)
            {
                level++;
            }

            int side = Cells >> level;
            int minimum = int.MaxValue;
            int maximum = int.MinValue;
            for (int x = x0 >> level; x <= x1 >> level; x++)
            {
                for (int y = y0 >> level; y <= y1 >> level; y++)
                {
                    minimum = Math.Min(minimum, _minimum[level][(x * side) + y]);
                    maximum = Math.Max(maximum, _maximum[level][(x * side) + y]);
                }
            }
            return (minimum, maximum);
        }
    }

    /// <summary>
    /// Returns terrain rise in millimetres per retail unit along Core X/Z.
    /// This is the integer counterpart of the one-lattice central difference
    /// used by the Level 100 presentation mesh. At an edge it becomes the
    /// corresponding one-sided difference.
    /// </summary>
    public SimVector2 SampleGroundGradientPermille(SimVector2 relativePosition)
    {
        if (relativePosition.X < MinimumRelativeXMillimeters ||
            relativePosition.X > MaximumRelativeXMillimeters ||
            relativePosition.Z < MinimumRelativeZMillimeters ||
            relativePosition.Z > MaximumRelativeZMillimeters)
        {
            return SimVector2.Zero;
        }

        int leftX = Math.Max(MinimumRelativeXMillimeters, relativePosition.X - 1_000);
        int rightX = Math.Min(MaximumRelativeXMillimeters, relativePosition.X + 1_000);
        int backZ = Math.Max(MinimumRelativeZMillimeters, relativePosition.Z - 1_000);
        int forwardZ = Math.Min(MaximumRelativeZMillimeters, relativePosition.Z + 1_000);

        int riseX = SampleGroundElevationMillimeters(
            new SimVector2(rightX, relativePosition.Z)) -
            SampleGroundElevationMillimeters(new SimVector2(leftX, relativePosition.Z));
        int riseZ = SampleGroundElevationMillimeters(
            new SimVector2(relativePosition.X, forwardZ)) -
            SampleGroundElevationMillimeters(new SimVector2(relativePosition.X, backZ));

        return new SimVector2(
            checked((int)RoundDivideAwayFromZero(
                (long)riseX * 1_000,
                Math.Max(1, rightX - leftX))),
            checked((int)RoundDivideAwayFromZero(
                (long)riseZ * 1_000,
                Math.Max(1, forwardZ - backZ))));
    }

    private static Level100Terrain LoadEmbedded() => LoadEmbedded(ResourceName, SourceSha256);

    private static Level100Terrain LoadEmbeddedWorld(int worldNumber, string expectedSha256) =>
        LoadEmbedded(
            $"OnslaughtRebuild.Core.Assets.Level{worldNumber}." +
            $"level{worldNumber}-heightfield.hfld.bin",
            expectedSha256);

    private static Level100Terrain LoadEmbedded(string resourceName, string expectedSha256)
    {
        Assembly assembly = typeof(Level100Terrain).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidDataException($"The retained heightfield resource '{resourceName}' is missing.");
        if (stream.Length != HfldPayloadSize + 8)
        {
            throw new InvalidDataException("The retained heightfield has an unexpected length.");
        }

        var source = new byte[stream.Length];
        stream.ReadExactly(source);
        string hash = Convert.ToHexString(SHA256.HashData(source));
        if (!StringComparer.Ordinal.Equals(hash, expectedSha256))
        {
            throw new InvalidDataException("The retained heightfield hash does not match its provenance.");
        }

        if (ReadUInt32(source, 0) != s_hfldTag || ReadUInt32(source, 4) != HfldPayloadSize)
        {
            throw new InvalidDataException("The retained Level 100 HFLD has an unexpected envelope.");
        }

        const int chfdHeaderOffset = 8;
        const int chfdPayloadOffset = chfdHeaderOffset + 8;
        if (ReadUInt32(source, chfdHeaderOffset) != s_chfdTag ||
            ReadUInt32(source, chfdHeaderOffset + 4) != ChfdPayloadSize)
        {
            throw new InvalidDataException("The retained Level 100 HFLD is missing CHFD metadata.");
        }

        int hfdtHeaderOffset = chfdPayloadOffset + ChfdPayloadSize;
        int hfdtPayloadOffset = hfdtHeaderOffset + 8;
        if (ReadUInt32(source, hfdtHeaderOffset) != s_hfdtTag ||
            ReadUInt32(source, hfdtHeaderOffset + 4) != HfdtPayloadSize ||
            hfdtPayloadOffset + HfdtPayloadSize != source.Length)
        {
            throw new InvalidDataException("The retained Level 100 HFLD is missing its exact sample block.");
        }

        int heightScaleBits = BinaryPrimitives.ReadInt32LittleEndian(
            source.AsSpan(chfdPayloadOffset + HeightScaleOffset, sizeof(int)));
        float heightScale = BitConverter.Int32BitsToSingle(heightScaleBits);
        if (!float.IsFinite(heightScale) || heightScale <= 0f)
        {
            throw new InvalidDataException("The retained Level 100 HFLD has an invalid height scale.");
        }

        float fogDensity = ReadSingle(source, chfdPayloadOffset + FogDensityOffset);
        float waterLevel = ReadSingle(source, chfdPayloadOffset + WaterLevelOffset);
        float sunPositionX = ReadSingle(source, chfdPayloadOffset + SunPositionOffset);
        float sunPositionY = ReadSingle(source, chfdPayloadOffset + SunPositionOffset + sizeof(float));
        float sunPositionZ = ReadSingle(source, chfdPayloadOffset + SunPositionOffset + (sizeof(float) * 2));
        if (!float.IsFinite(fogDensity) || fogDensity < 0f ||
            !float.IsFinite(waterLevel) ||
            !float.IsFinite(sunPositionX) ||
            !float.IsFinite(sunPositionY) ||
            !float.IsFinite(sunPositionZ) ||
            (sunPositionX == 0f && sunPositionY == 0f && sunPositionZ == 0f))
        {
            throw new InvalidDataException("The retained Level 100 HFLD has invalid environment metadata.");
        }

        var samples = new short[HfdtPayloadSize / sizeof(short)];
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(
                source.AsSpan(hfdtPayloadOffset + (index * sizeof(short)), sizeof(short)));
        }

        return new Level100Terrain(
            samples,
            heightScaleBits,
            source[chfdPayloadOffset + MixerSetOffset],
            source[chfdPayloadOffset + SkyCubeOffset],
            source[chfdPayloadOffset + DetailTextureOffset],
            waterLevel,
            source[chfdPayloadOffset + WaterTextureOffset],
            ReadUInt32(source, chfdPayloadOffset + FogColorOffset),
            fogDensity,
            ReadUInt32(source, chfdPayloadOffset + SunColorOffset),
            ReadUInt32(source, chfdPayloadOffset + AntiSunColorOffset),
            ReadUInt32(source, chfdPayloadOffset + AmbientColorOffset),
            sunPositionX,
            sunPositionY,
            sunPositionZ,
            expectedSha256);
    }

    private int TileSample(
        int tileX,
        int tileY,
        int localX,
        int localY)
    {
        int sampleIndex =
            (((tileX * TileCountPerAxis) + tileY) * SamplesPerTile) +
            (localY * SamplesPerTileAxis) +
            localX;
        return _heightSamples[sampleIndex];
    }

    private static (int Significand, long Denominator) DecodePositiveFloatScale(int bits)
    {
        int exponentBits = (bits >> 23) & 0xFF;
        int fraction = bits & 0x7FFFFF;
        if (bits < 0 || exponentBits is 0 or 0xFF)
        {
            throw new InvalidDataException("The Level 100 height scale is not a positive normal float.");
        }

        int significand = (1 << 23) | fraction;
        int binaryExponent = exponentBits - 127 - 23;
        if (binaryExponent > 0 || binaryExponent < -62)
        {
            throw new InvalidDataException("The Level 100 height scale is outside the exact fixed range.");
        }

        return (significand, 1L << -binaryExponent);
    }

    private static long FloorDivide(long value, long denominator)
    {
        long quotient = value / denominator;
        return value % denominator < 0 ? quotient - 1 : quotient;
    }

    private static long RoundDivideAwayFromZero(long value, long denominator)
    {
        if (value >= 0)
        {
            return (value + (denominator / 2)) / denominator;
        }

        return -((-value + (denominator / 2)) / denominator);
    }

    private static uint ReadUInt32(byte[] source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(offset, sizeof(uint)));

    private static float ReadSingle(byte[] source, int offset) =>
        BitConverter.Int32BitsToSingle(
            BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, sizeof(float))));

    private static uint Tag(string value) =>
        (uint)value[0] |
        ((uint)value[1] << 8) |
        ((uint)value[2] << 16) |
        ((uint)value[3] << 24);
}
