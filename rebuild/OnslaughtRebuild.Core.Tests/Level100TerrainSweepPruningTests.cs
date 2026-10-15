// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using OnslaughtRebuild.Core;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The terrain sweep skips spans whose lowest point clears the highest ground
/// their box can sample. That pruning must never change a result: every ray
/// here must produce the identical contact record with and without it.
/// </summary>
public sealed class Level100TerrainSweepPruningTests
{
    private readonly ITestOutputHelper _output;

    public Level100TerrainSweepPruningTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PruningNeverChangesATerrainContact()
    {
        Level100Terrain terrain = Level100Terrain.Instance;
        uint state = 0x5357_4550;
        int Next(int exclusiveMaximum)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)exclusiveMaximum);
        }

        int contacts = 0;
        long prunedTicks = 0;
        long plainTicks = 0;
        int[] radii = [0, 250, 1_000, 2_500];
        for (int ray = 0; ray < 400; ray++)
        {
            // Mostly inside the map, some starting past its edge (flat zero).
            int x = Next(620_000) - 330_000;
            int z = Next(620_000) - 290_000;
            int ground = terrain.SampleGroundElevationMillimeters(new SimVector2(x, z));
            // From 2 m below the ground (already touching) to 60 m above it.
            int elevation = ground + Next(62_000) - 2_000;
            int length = Next(1_000_000) + 1;
            double yaw = Next(628_319) / 100_000.0;
            double pitch = (Next(100_000) - 70_000) / 100_000.0; // -0.70 .. +0.30 rad
            int radius = radii[Next(radii.Length)];
            var start = new Level100Vector3(x, z, -elevation);
            var end = new Level100Vector3(
                x + (int)Math.Round(-Math.Sin(yaw) * Math.Cos(pitch) * length),
                z + (int)Math.Round(Math.Cos(yaw) * Math.Cos(pitch) * length),
                -elevation - (int)Math.Round(Math.Sin(pitch) * length));

            long t0 = Stopwatch.GetTimestamp();
            bool pruned = Level100ContactMechanics.TrySweepRoundAgainstTerrain(start, end, radius, out Level100ContactHit prunedHit);
            long t1 = Stopwatch.GetTimestamp();
            bool plain = Level100ContactMechanics.TrySweepRoundAgainstTerrainWithoutPruning(start, end, radius, out Level100ContactHit plainHit);
            long t2 = Stopwatch.GetTimestamp();
            prunedTicks += t1 - t0;
            plainTicks += t2 - t1;

            Assert.True(plain == pruned && plainHit == prunedHit,
                $"ray {ray}: start={start} end={end} radius={radius}: plain {plain}/{plainHit} pruned {pruned}/{prunedHit}");
            contacts += plain ? 1 : 0;
        }

        // Both outcomes must be exercised, or the comparison proves little.
        Assert.InRange(contacts, 40, 360);
        _output.WriteLine(
            $"400 rays, {contacts} contacts: pruned {prunedTicks * 1000.0 / Stopwatch.Frequency:F0} ms, " +
            $"unpruned {plainTicks * 1000.0 / Stopwatch.Frequency:F0} ms");
    }
}
