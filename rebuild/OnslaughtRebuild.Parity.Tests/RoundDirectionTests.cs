// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class RoundDirectionTests
{
    [Fact]
    public void StraightFlightNormalisationUsesTheOriginalsSquareSumOrder()
    {
        // Authored vector: the two sum orders round to different magnitudes in float32.
        var direction = new FVector(0.1f, 0.1f, 0.6f);
        direction.NormaliseYZX();
        Assert.Equal(0x3e261d60u, BitConverter.SingleToUInt32Bits(direction.X));
        Assert.Equal(0x3e261d60u, BitConverter.SingleToUInt32Bits(direction.Y));
        Assert.Equal(0x3f792c11u, BitConverter.SingleToUInt32Bits(direction.Z));
    }

    [Fact]
    public void ZeroDirectionRemainsZero()
    {
        var direction = new FVector(0, 0, 0);
        direction.NormaliseYZX();
        Assert.Equal(new FVector(0, 0, 0), direction);
    }
}
