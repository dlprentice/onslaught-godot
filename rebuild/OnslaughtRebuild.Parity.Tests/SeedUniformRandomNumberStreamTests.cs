// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SeedUniformRandomNumberStreamTests
{
    [Fact]
    public void FollowsSchragesStepWithThirtyTwoBitWrapping()
    {
        var stream = new SeedUniformRandomNumberStream(123456);
        long value = 123456;
        for (int i = 0; i < 1_000_000; i++)
        {
            // The same step in 64-bit arithmetic, wrapped to 32 bits after each operation as retail's int code does.
            long hi = (int)value / (SeedUniformRandomNumberStream.Modulus / SeedUniformRandomNumberStream.Multiplier);
            long lo = (int)value % (SeedUniformRandomNumberStream.Modulus / SeedUniformRandomNumberStream.Multiplier);
            int t = unchecked((int)((SeedUniformRandomNumberStream.Multiplier * lo) - (int)((SeedUniformRandomNumberStream.Modulus % SeedUniformRandomNumberStream.Multiplier) * hi)));
            value = t > 0 ? t : unchecked(t + SeedUniformRandomNumberStream.Modulus);
            int expected = (int)value < 0 ? unchecked(-(int)value) : (int)value;

            Assert.Equal(expected, stream.Next());
            Assert.Equal((int)value, stream.Value);
        }

        Assert.Equal(123456, stream.Seed);
    }

    [Fact]
    public void ReachesNegativeValuesThroughTheOverflow()
    {
        var stream = new SeedUniformRandomNumberStream(123456);
        bool negative = false;
        for (int i = 0; i < 1_000_000 && !negative; i++)
        {
            stream.Next();
            negative = stream.Value < 0;
        }

        Assert.True(negative);
    }
}
