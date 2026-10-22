// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class HeightFieldTests
{
    private static EngineResources Level100()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        var c = new ChunkReader(MemBuffer.FromFile(name, bytes));
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("ERES"))
            {
                return EngineResources.Read(c);
            }

            c.Skip();
        }

        throw new InvalidDataException("no ERES chunk");
    }

    [GameDataFact]
    public void Level100sHeightFieldLoadsWithAPlausibleScaleAndGrid()
    {
        EngineResources e = Level100();
        HeightField h = e.HeightField;

        Assert.True(h.HeightScale > 0 && h.HeightScale < 1, $"height scale {h.HeightScale}");
        Assert.Equal(9, h.GridShiftX);
        Assert.Equal(9, h.GridShiftY);
        Assert.Contains(h.Heights.ToArray(), s => s != 0);
    }

    [GameDataFact]
    public void CollideInterpolatesTheSamplesAndAgreesAtSamplePoints()
    {
        HeightField h = Level100().HeightField;

        // At an exact sample point the interpolation weights are zero, so the height is the sample times the scale.
        for (int x = 0; x < 512; x += 37)
        {
            for (int y = 0; y < 512; y += 41)
            {
                float expected = h.GetHeightSample(x, y) * h.HeightScale;
                Assert.Equal(expected, h.Collide(new FVector(x, y, 0)));
            }
        }

        Assert.Equal(0f, h.Collide(new FVector(-3, 10, 0)));
    }

    [GameDataFact]
    // The world's Z axis points down (actor.cpp's altitude is MAP.Collide(pos) - COfGHeight()), so up is -Z.
    public void NormalsAreUnitAndPointUp()
    {
        HeightField h = Level100().HeightField;
        var r = new Random(100);
        for (int i = 0; i < 10_000; i++)
        {
            FVector n = h.Normal(new FVector((float)(r.NextDouble() * 511), (float)(r.NextDouble() * 511), 0));
            Assert.InRange(n.Magnitude(), 0.9999f, 1.0001f);
            Assert.True(n.Z < 0);
        }

        Assert.Equal(new FVector(0, 0, 1), h.Normal(new FVector(600, 10, 0)));
    }

    [GameDataFact]
    // 0047ec7c..0047eca0: floor, then a 64-bit fistp keeping the low dword. A coordinate past 2^63 gives the integer
    // indefinite (low dword 0), so retail carries on at sample 0 with both fractions 0: the near triangle of square
    // (0, 0). D's oracle found the saturating cast returning the off-map (0, 0, 1) instead.
    public void AHugeCoordinateWrapsToSampleZeroAsTheFistpDoes()
    {
        HeightField h = Level100().HeightField;
        FVector origin = h.Normal(new FVector(0, 0, 0));
        Assert.Equal(origin, h.Normal(new FVector(3.4e38f, 3.4e38f, 0)));
        Assert.Equal(origin, h.Normal(new FVector(3.4e38f, 0, 0)));
        Assert.Equal(origin, h.Normal(new FVector(-3.4e38f, 0, 0)));   // negative too: the indefinite's low dword is 0
        Assert.Equal(new FVector(0, 0, 1), h.Normal(new FVector(-1.0f, 0, 0)));
    }

    [GameDataFact]
    public void TheWaterLevelLiesWithinTheTerrainsHeights()
    {
        HeightField h = Level100().HeightField;
        int above = 0, below = 0;
        float min = float.MaxValue, max = float.MinValue;
        for (int x = 0; x < 512; x += 2)
        {
            for (int y = 0; y < 512; y += 2)
            {
                float z = h.Collide(new FVector(x, y, 0));
                min = MathF.Min(min, z);
                max = MathF.Max(max, z);
                if (z < h.WaterLevel)
                {
                    above++; // Z points down: smaller is higher, so this ground is above the water
                }
                else
                {
                    below++;
                }
            }
        }

        Assert.True(above > 0 && below > 0, $"water {h.WaterLevel}, ground {min}..{max}, dry {above}, wet {below}");
        _output?.WriteLine($"water {h.WaterLevel}, ground {min}..{max}, dry {above}, wet {below}");
    }

    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    public HeightFieldTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
}
