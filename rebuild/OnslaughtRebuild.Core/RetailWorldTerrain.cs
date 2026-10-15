// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Binds an admitted world's heightfield to the existing Core coordinate frame.
/// The frame stays at retail (288.6875, 243.25, -10); it is a reconstruction
/// convention, not an assertion about another world's player start.
/// </summary>
public sealed class RetailWorldTerrain
{
    private RetailWorldTerrain(int worldNumber, Level100Terrain heightfield)
    {
        WorldNumber = worldNumber;
        Heightfield = heightfield;
        WaterElevationMillimeters = checked((int)Math.Round(
            Level100Terrain.PlayerStartReferenceElevationMillimeters -
            (double)heightfield.WaterLevel * 1_000,
            MidpointRounding.AwayFromZero));
    }

    public static RetailWorldTerrain World100 { get; } =
        new(100, Level100Terrain.Instance);

    public static RetailWorldTerrain World110 { get; } =
        new(110, Level100Terrain.World110);

    public int WorldNumber { get; }

    public Level100Terrain Heightfield { get; }

    public int WaterElevationMillimeters { get; }

    public int SampleGroundElevationMillimeters(SimVector2 position) =>
        Heightfield.SampleGroundElevationMillimeters(position);

    /// <summary>
    /// Retail 47eb80 coordinate conversion with the declared nearest float
    /// stores. The biased float's bits form the fixed coordinate, not a cast.
    /// </summary>
    internal static float SampleRetailHeight(Level100Terrain terrain, Level100FloatVector3Bits position)
    {
        static int Coordinate(int bits)
        {
            double value = BitConverter.Int32BitsToSingle(bits);
            float biased = (float)((value - BitConverter.Int32BitsToSingle(0x3afffeb0)) + 49152.0);
            return unchecked(BitConverter.SingleToInt32Bits(biased) - 0x47400000);
        }
        return (float)((double)terrain.SampleHeightUnitsAtFixed(
            Coordinate(position.X), Coordinate(position.Y)) * terrain.HeightScale);
    }
}
