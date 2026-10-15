// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitAttachmentPoseTests
{
    private const int One = 0x3f800000;
    private static readonly Level100FloatBasis3Bits Identity = new(
        One, 0, 0, 0, One, 0, 0, 0, One);
    private static readonly Level100FloatBasis3Bits Ones = new(
        One, One, One, One, One, One, One, One, One);

    [Fact]
    public void Translation_SpillsYAndZBeforeAddingParentButKeepsXWide()
    {
        // At the float32 tie, (1 + 2^-24) rounds to 1. Subtracting 1
        // before that store preserves 2^-24; subtracting after it gives zero.
        int minusOne = BitConverter.SingleToInt32Bits(-1.0f);
        int halfUlp = BitConverter.SingleToInt32Bits(MathF.ScaleB(1.0f, -24));
        var parent = new RetailUnitAttachmentPose(new(minusOne, minusOne, minusOne), Ones);
        var local = new RetailUnitAttachmentPose(new(One, halfUlp, 0), Identity);
        Assert.Equal(new Level100FloatVector3Bits(halfUlp, 0, 0),
            RetailUnitAttachmentPose.Transform(parent, local).PositionFloatBits);
    }

    [Fact]
    public void Matrix_UsesTheMeasuredTermOrderForEachOfNineComponents()
    {
        // With 53-bit intermediates, (2^54 + -2^54) + 1 is 1, while
        // (2^54 + 1) + -2^54 is 0. This distinguishes the actual nine
        // accumulation orders from one generic dot-product loop.
        int large = BitConverter.SingleToInt32Bits(MathF.ScaleB(1.0f, 54));
        int negative = BitConverter.SingleToInt32Bits(-MathF.ScaleB(1.0f, 54));
        var local = new RetailUnitAttachmentPose(default, new(
            large, large, large, One, One, One, negative, negative, negative));
        var parent = new RetailUnitAttachmentPose(default, Ones);
        Assert.Equal(new Level100FloatBasis3Bits(0, 0, 0, 0, One, One, 0, One, 0),
            RetailUnitAttachmentPose.Transform(parent, local).BasisFloatBits);
    }

    [Fact]
    public void UnsupportedInputsAreRejectedWithoutClamping()
    {
        int notFinite = BitConverter.SingleToInt32Bits(float.NaN);
        var invalid = new RetailUnitAttachmentPose(new(notFinite, 0, 0), Identity);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RetailUnitAttachmentPose.Transform(invalid, new(default, Identity)));
        var boundary = new RetailUnitAttachmentPose(default, Identity with { Row2Y = One });
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.ToComponentEuler());
    }
}
