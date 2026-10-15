// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailMeshPartPoseTests
{
    private const int One = 0x3f800000;
    private static readonly Level100FloatBasis3Bits Identity = new(One, 0, 0, 0, One, 0, 0, 0, One);
    private static readonly Level100FloatBasis3Bits Ones = new(One, One, One, One, One, One, One, One, One);

    [Fact]
    public void SingleFrameInterpolationNormalizesNegativeZeroWithoutQuantization()
    {
        var frame = new RetailUnitAttachmentPose(new(int.MinValue, 0x3dbec640, int.MinValue),
            Identity with { Row0Y = int.MinValue, Row2X = int.MinValue });
        Assert.Equal(new RetailUnitAttachmentPose(new(0, 0x3dbec640, 0), Identity),
            RetailMeshPartPose.InterpolateSingleFrame(frame));
    }

    [Fact]
    public void LocalSphereQueryUsesCachedTransposeAndCurrentCenterMinusMotion()
    {
        // Synthetic nonorthogonal matrix distinguishes the cached transpose
        // from both a forward transform and a general matrix inverse.
        var part = new RetailUnitAttachmentPose(Vector(10, 20, 30),
            Identity with { Row0Y = BitConverter.SingleToInt32Bits(2) });
        var query = RetailMeshPartPose.ToLocalSphereQuery(part, Vector(14, 25, 36), Vector(1, 2, 3));
        Assert.Equal(Vector(3, 9, 3), query.Position);
        Assert.Equal(Vector(1, 4, 3), query.Displacement);
    }

    [Fact]
    public void LocalSphereQueryRoundsCurrentCenterBeforeSubtractingPartPosition()
    {
        var part = new RetailUnitAttachmentPose(Vector(16_777_216, 0, 0), Identity);
        var query = RetailMeshPartPose.ToLocalSphereQuery(part, part.PositionFloatBits, Vector(-1, 0, 0));
        Assert.Equal(default(Level100FloatVector3Bits), query.Position);
        Assert.Equal(Vector(-1, 0, 0), query.Displacement);
    }

    [Fact]
    public void LocalSphereQueryAccumulatesZThenYThenX()
    {
        var part = new RetailUnitAttachmentPose(default, Ones);
        var query = RetailMeshPartPose.ToLocalSphereQuery(part, Vector(1, -33_554_432, 33_554_432), default);
        Assert.Equal(Vector(1, 1, 1), query.Position);
    }

    [Fact]
    public void StationarySphereStillRotatesSignedZeroDisplacement()
    {
        int minusOne = I(0xbf800000);
        var part = new RetailUnitAttachmentPose(default,
            new(minusOne, minusOne, minusOne, minusOne, minusOne, minusOne, minusOne, minusOne, minusOne));
        var query = RetailMeshPartPose.ToLocalSphereQuery(part, default, default);
        Assert.Equal(new Level100FloatVector3Bits(int.MinValue, int.MinValue, int.MinValue), query.Displacement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TranslationRoundsRegisterAdditionBeforeAddingOwnerOrParent(bool owner)
    {
        // Synthetic PC24 discriminator: the arithmetic instruction rounds
        // 1 + 2^-24 before subtracting 1, even without a float store.
        int halfUlp = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, -24));
        int minusOne = BitConverter.SingleToInt32Bits(-1);
        var parent = new RetailUnitAttachmentPose(new(minusOne, minusOne, minusOne), Ones);
        var local = new RetailUnitAttachmentPose(new(One, halfUlp, 0), Identity);
        var result = owner ? RetailMeshPartPose.ApplyOwner(parent, local)
            : RetailMeshPartPose.ComposeHierarchy(parent, local);
        Assert.Equal(default(Level100FloatVector3Bits), result.PositionFloatBits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HierarchyAndOwnerHaveDistinctMeasuredMatrixOrders(bool negativeMiddle)
    {
        // Synthetic PC24 cancellation probe: the pair summed first changes
        // whether the surviving unit is lost at either sign of 2^25.
        // The negative side of 2^24 still has unit spacing. No orthonormality claim.
        int large = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, 25));
        int negative = BitConverter.SingleToInt32Bits(-MathF.ScaleB(1, 25));
        int middle = negativeMiddle ? negative : One;
        int last = negativeMiddle ? One : negative;
        var local = new RetailUnitAttachmentPose(default, new(
            large, large, large, middle, middle, middle, last, last, last));
        var parent = new RetailUnitAttachmentPose(default, Ones);
        var hierarchy = negativeMiddle
            ? new Level100FloatBasis3Bits(0, 0, 0, 0, One, One, One, One, One)
            : new Level100FloatBasis3Bits(One, 0, 0, 0, 0, 0, 0, 0, 0);
        var owner = negativeMiddle
            ? new Level100FloatBasis3Bits(One, One, 0, One, One, 0, One, One, 0)
            : new Level100FloatBasis3Bits(0, 0, 0, 0, 0, One, 0, 0, One);
        Assert.Equal(hierarchy, RetailMeshPartPose.ComposeHierarchy(parent, local).BasisFloatBits);
        Assert.Equal(owner, RetailMeshPartPose.ApplyOwner(parent, local).BasisFloatBits);
    }

    [Fact]
    public void OwnerRoundsProductsBeforeAdding()
    {
        // (1+2^-23)*(1-2^-23) rounds to 1 at PC24, before subtracting 1.
        var owner = new RetailUnitAttachmentPose(default,
            Identity with { Row0X = I(0xbf800000), Row0Y = 0x3f800001 });
        var local = new RetailUnitAttachmentPose(new(One, 0x3f7ffffe, 0), Identity);
        Assert.Equal(0, RetailMeshPartPose.ApplyOwner(owner, local).PositionFloatBits.X);
    }

    [Fact]
    public void IntermediateProductsRetainExponentRangeUntilFloatStore()
    {
        // PC24 controls significand width, not x87's wider exponent range.
        // These synthetic products overflow ordinary float before cancelling.
        int large = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, 100));
        int factor = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, 40));
        var owner = new RetailUnitAttachmentPose(default,
            Identity with { Row0X = large, Row0Y = large ^ int.MinValue, Row0Z = One });
        var local = new RetailUnitAttachmentPose(new(factor, factor, One), Identity);
        Assert.Equal(One, RetailMeshPartPose.ApplyOwner(owner, local).PositionFloatBits.X);

        // Each 2^-150 term is below the float subnormal range; retaining both
        // until their sum is stored yields the smallest positive float.
        int small = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, -100));
        int tinyFactor = BitConverter.SingleToInt32Bits(MathF.ScaleB(1, -50));
        owner = new(default, Identity with { Row0X = small, Row0Y = small, Row0Z = 0 });
        local = new(new(tinyFactor, tinyFactor, 0), Identity);
        Assert.Equal(1, RetailMeshPartPose.ApplyOwner(owner, local).PositionFloatBits.X);
    }

    [Fact]
    public void WarehouseHierarchyUsesRawSingleFrameInputs()
    {
        // Source-bound arithmetic examples, not observed runtime poses.
        // The selected Warehouse asset is pinned by Level100ContactCatalog.
        var parts = Level100ContactCatalog.Instance.GetDefinition("Warehouse").Parts;
        var poses = new RetailUnitAttachmentPose[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            var raw = parts[i].FloatGeometry;
            Assert.All(raw.FrameMap!.Value.ToArray(), frame => Assert.Equal(0, frame));
            var p = Assert.Single(raw.HierarchyPositionWords!).Span;
            var b = Assert.Single(raw.HierarchyOrientationWords!).Span;
            var local = new RetailUnitAttachmentPose(new(I(p[0]), I(p[1]), I(p[2])), new(
                I(b[0]), I(b[1]), I(b[2]), I(b[4]), I(b[5]), I(b[6]), I(b[8]), I(b[9]), I(b[10])));
            var parent = parts[i].Parent < 0 ? new RetailUnitAttachmentPose(default, Identity)
                : poses[parts[i].Parent];
            // All actual Warehouse hierarchy parents are identity-oriented;
            // rotated leaves do not justify an invented rotated-parent sample.
            Assert.Equal(Identity, parent.BasisFloatBits);
            poses[i] = RetailMeshPartPose.ComposeHierarchy(parent,
                RetailMeshPartPose.InterpolateSingleFrame(local));
        }
        Assert.Equal(new Level100FloatVector3Bits(0x3db709c2, 0x40a1b550, 0x3ca129a0), poses[1].PositionFloatBits);
        Assert.Equal(new Level100FloatVector3Bits(0x3fdfe291, 0x40d8b0ae, I(0xc0231284)), poses[8].PositionFloatBits);
        Assert.Equal(new Level100FloatVector3Bits(I(0xbfdc217e), 0x40c5910f, I(0xc071faf6)), poses[19].PositionFloatBits);
        Assert.Equal(new Level100FloatBasis3Bits(I(0xbf6208da), I(0xb325c382), 0x3ef05e95,
            I(0xb2c733a5), One, 0x3325c382, 0x3ef05e95, I(0xb2c733a5), 0x3f6208da), poses[19].BasisFloatBits);
        Assert.NotEqual(0x3dbe76c9, poses[0].PositionFloatBits.X); // nearest-mm round trip
        Assert.Equal(0x3dbec640, poses[0].PositionFloatBits.X);
    }

    [Fact]
    public void NonFiniteInputsAreRejected()
    {
        int invalid = BitConverter.SingleToInt32Bits(float.NaN);
        var pose = new RetailUnitAttachmentPose(new(invalid, 0, 0), Identity);
        var identity = new RetailUnitAttachmentPose(default, Identity);
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailMeshPartPose.InterpolateSingleFrame(pose));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailMeshPartPose.ComposeHierarchy(identity, pose));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailMeshPartPose.ApplyOwner(pose, identity));
    }

    private static int I(uint bits) => unchecked((int)bits);
    private static Level100FloatVector3Bits Vector(float x, float y, float z) => new(
        BitConverter.SingleToInt32Bits(x), BitConverter.SingleToInt32Bits(y), BitConverter.SingleToInt32Bits(z));
}
