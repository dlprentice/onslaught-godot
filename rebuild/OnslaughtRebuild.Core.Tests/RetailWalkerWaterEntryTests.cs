// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailWalkerWaterEntry"/> against
/// <c>references/Onslaught/BattleEngineWalkerPart.cpp:442-460</c> and the
/// pristine <c>74154bfa…</c> bytes at <c>0x00413A70</c>.
/// </summary>
public sealed class RetailWalkerWaterEntryTests
{
    [Fact]
    public void ShoreDepth_MatchesTheReadOnlyDataBothArmsLoad() =>
        Assert.Equal(
            0x3E99999Au,
            BitConverter.SingleToUInt32Bits(RetailWalkerWaterEntry.ShoreDepth));

    // Both guards are short-circuited ahead of any terrain work, so neither can
    // be reached by a chassis in the air or standing on an object however
    // inviting the terrain is.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void GoingIntoWater_NeedsGroundContactAndNoObject(bool onGround, bool onObject) =>
        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            onGround, onObject, waterLevel: 0.0f, groundHere: 10.0f, groundAhead: 100.0f));

    // Above the shoreline margin the question becomes "is the ground ahead
    // HIGHER than the ground here" - a climb, not a descent. Both directions
    // are pinned so a rebuild cannot quietly flip the comparison to the one the
    // function's name suggests.
    [Theory]
    [InlineData(10.0f, 11.0f, true)]
    [InlineData(10.0f, 10.0f, false)]
    [InlineData(10.0f, 9.0f, false)]
    public void GoingIntoWater_AsksForAClimbOnTheHighArm(
        float here, float ahead, bool expected) =>
        Assert.Equal(
            expected,
            RetailWalkerWaterEntry.GoingIntoWater(
                isOnGround: true, isOnObject: false,
                waterLevel: 0.0f, groundHere: here, groundAhead: ahead));

    // Well away from the seam the two arms give visibly different answers for
    // the same ground ahead: standing high and dry, a lower ground ahead is not
    // an entry; standing at the water line, the same ground ahead is well over
    // the margin and is one. This is what a rebuild that hard-wired either arm
    // would fail.
    //
    // NOT A GUARD ON THE SELECTOR, and that is measured rather than assumed. An
    // "assert the two arms disagree here" guard was added to this method and then
    // WITHDRAWN: hard-wiring the high arm (`groundHere - waterLevel > -inf`) still
    // reproduces both rows and still makes the two probes disagree, because over a
    // water line at zero the high arm's `ahead > float(here)` separates 10.0 from
    // 0.0 on its own. The selector's own contract is guarded by
    // GoingIntoWater_TakesTheLowArmAtExactlyTheMarginAboveWater and
    // GoingIntoWater_MeasuresTheLowArmFromTheWaterLine, which that same mutant
    // does kill. Read this method as what it is: two retail answers, pinned.
    [Theory]
    [InlineData(10.0f, 5.0f, false)]
    [InlineData(0.0f, 5.0f, true)]
    public void GoingIntoWater_AnswersDifferentlyOnTheTwoArms(
        float here, float ahead, bool expected) =>
        Assert.Equal(
            expected,
            RetailWalkerWaterEntry.GoingIntoWater(
                isOnGround: true, isOnObject: false,
                waterLevel: 0.0f, groundHere: here, groundAhead: ahead));

    // Over a water line at zero the two arms coincide at the seam - the low arm
    // asks `ahead - 0 > 0.3` and the high arm asks `ahead > float(0.3)`, which
    // is the same comparison against the same word - so a sea-level test can
    // never tell `>` from `>=` in the selector at 0x00413ABF.
    [Fact]
    public void GoingIntoWater_ArmsCoincideAtTheSeamOverASeaLevelWaterLine()
    {
        double atTheSeam = (double)RetailWalkerWaterEntry.ShoreDepth;
        double justAbove = System.Math.BitIncrement(atTheSeam);

        Assert.False(atTheSeam - 0.0 > (double)RetailWalkerWaterEntry.ShoreDepth);
        Assert.True(justAbove - 0.0 > (double)RetailWalkerWaterEntry.ShoreDepth);
        Assert.Equal(RetailWalkerWaterEntry.ShoreDepth, (float)justAbove);

        foreach (double ahead in new[] { 0.0, 0.2999, atTheSeam, justAbove, 0.31, 100.0 })
        {
            Assert.Equal(
                RetailWalkerWaterEntry.GoingIntoWater(
                    isOnGround: true, isOnObject: false,
                    waterLevel: 0.0f, groundHere: atTheSeam, groundAhead: ahead),
                RetailWalkerWaterEntry.GoingIntoWater(
                    isOnGround: true, isOnObject: false,
                    waterLevel: 0.0f, groundHere: justAbove, groundAhead: ahead));
        }
    }

    // Lift the water line and the coincidence breaks, because the high arm's
    // narrowing of the ground here is then a real rounding. Standing exactly
    // 0.3 above a water line at 4, retail takes the LOW arm - the selector is
    // strictly-greater - and answers TRUE for a ground ahead that the high arm
    // would reject, because rounding 4.3000000119 to float lifts it to
    // 4.3000001907. This is the row that separates `>` from `>=` at
    // 0x00413ABF; a sea-level test cannot.
    //
    // NON-VACUITY: the first three assertions ARE the exercised-population proof
    // and are load-bearing rather than descriptive. The probe is proved to sit
    // exactly ON the selector's threshold, so `>` and `>=` really do disagree
    // here; the narrowed ground here is proved to be the lifted 0x4089999A; and
    // the high arm is proved to REJECT this ground ahead, so the TRUE below can
    // only have come from the low arm. Remove any one of them and the final
    // assertion stops being attributable to the selector.
    [Fact]
    public void GoingIntoWater_TakesTheLowArmAtExactlyTheMarginAboveWater()
    {
        const float waterLevel = 4.0f;
        double here = (double)waterLevel + (double)RetailWalkerWaterEntry.ShoreDepth;
        const double ahead = 4.3000001;

        Assert.Equal((double)RetailWalkerWaterEntry.ShoreDepth, here - (double)waterLevel);
        Assert.False(here - (double)waterLevel > (double)RetailWalkerWaterEntry.ShoreDepth);
        Assert.True(here - (double)waterLevel >= (double)RetailWalkerWaterEntry.ShoreDepth);
        Assert.Equal(0x4089999Au, BitConverter.SingleToUInt32Bits((float)here));
        Assert.False(ahead > (double)(float)here);

        Assert.True(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false, waterLevel, here, ahead));
    }

    // The low arm is the same strictly-greater against the same 0.3f, measured
    // from the water rather than from the ground here.
    [Theory]
    [InlineData(1.0f, 1.3f, false)]
    [InlineData(1.0f, 1.30001f, true)]
    [InlineData(1.0f, 0.0f, false)]
    public void GoingIntoWater_MeasuresTheLowArmFromTheWaterLine(
        float waterLevel, float ahead, bool expected) =>
        Assert.Equal(
            expected,
            RetailWalkerWaterEntry.GoingIntoWater(
                isOnGround: true, isOnObject: false,
                waterLevel, groundHere: waterLevel, groundAhead: ahead));

    // 0x00413B01 narrows the SECOND Collide(pos) sample to float before
    // 0x00413B13 compares the extended sample ahead against it. Here the
    // extended height ahead sits below the extended height here but above its
    // float rounding, so retail says the walker is climbing and a rebuild that
    // kept both samples wide says it is not.
    [Fact]
    public void GoingIntoWater_NarrowsTheGroundHereBeforeTheClimbTest()
    {
        const double here = 2.0000001;
        const double ahead = 2.00000005;

        Assert.Equal(2.0f, (float)here);
        Assert.False(ahead > here);

        Assert.True(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 1.0f, groundHere: here, groundAhead: ahead));
    }

    // The arm selector, by contrast, sees the UNROUNDED height here: it is
    // subtracted straight on the stack at 0x00413AB3 with no store. A sample
    // just above the margin that would round down to it still takes the high
    // arm.
    [Fact]
    public void GoingIntoWater_SelectsTheArmOnTheUnroundedHeight()
    {
        double here = 0.30000001192092896 + 1.0e-12;

        Assert.Equal(RetailWalkerWaterEntry.ShoreDepth, (float)here);
        Assert.True(here - 0.0 > (double)RetailWalkerWaterEntry.ShoreDepth);

        // High arm: ahead is below here, so no climb, so FALSE. On the low arm
        // this same ahead would be well over the margin and give TRUE.
        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 0.0f, groundHere: here, groundAhead: 0.29999998f));
    }

    // Every comparison in the body is test ah, 0x41 with jne to the failure
    // label, so an unordered sample fails each of them - the same way C's `>`
    // does. But failing the SELECTOR is not failing the function: an unordered
    // ground here falls to the low arm, which never reads it again, so the
    // answer comes from the ground ahead alone and can still be TRUE. A model
    // that short-circuited the whole body on a NaN would get that wrong.
    [Fact]
    public void GoingIntoWater_DropsToTheLowArmOnAnUnorderedGroundHere()
    {
        Assert.True(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 0.0f, groundHere: double.NaN, groundAhead: 100.0));

        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 0.0f, groundHere: double.NaN, groundAhead: 0.1));
    }

    [Fact]
    public void GoingIntoWater_IsFalseWhenTheSampleItActuallyReadsIsUnordered()
    {
        // High arm, unordered ground ahead.
        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 0.0f, groundHere: 10.0, groundAhead: double.NaN));

        // Low arm, unordered ground ahead.
        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: 0.0f, groundHere: 0.0, groundAhead: double.NaN));

        // An unordered water level poisons both arms.
        Assert.False(RetailWalkerWaterEntry.GoingIntoWater(
            isOnGround: true, isOnObject: false,
            waterLevel: float.NaN, groundHere: 10.0, groundAhead: 100.0));
    }

    // Each look-ahead component is one x87 add of two floats under 53-bit
    // precision and is then stored with fstp dword, which is a single rounding
    // and therefore plain float addition.
    [Fact]
    public void AdvancePosition_RoundsEachComponentOnce()
    {
        float position = BitConverter.UInt32BitsToSingle(0x4B800000u);
        const float velocity = 0.75f;

        Assert.Equal(
            BitConverter.SingleToUInt32Bits(position + velocity),
            BitConverter.SingleToUInt32Bits(
                RetailWalkerWaterEntry.AdvancePosition(position, velocity)));
        Assert.Equal(
            0x4B800000u,
            BitConverter.SingleToUInt32Bits(
                RetailWalkerWaterEntry.AdvancePosition(position, velocity)));
    }
}
