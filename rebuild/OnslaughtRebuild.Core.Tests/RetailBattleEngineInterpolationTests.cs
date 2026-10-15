// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailBattleEngineInterpolation"/> against
/// <c>references/Onslaught/BattleEngine.cpp:3187-3196</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x0040D660</c>.
/// </summary>
public sealed class RetailBattleEngineInterpolationTests
{
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    private static float FromBits(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    [Fact]
    public void Constants_MatchTheReadOnlyDataAndEachOther()
    {
        Assert.Equal(0x3FC90FDBu, Bits(RetailBattleEngineInterpolation.WrapThreshold));
        Assert.Equal(0x40C90FDBu, Bits(RetailBattleEngineInterpolation.WrapAmount));

        // The negative threshold at 0x005D85C8 is the same word with the sign
        // bit set, and the wrap amount is exactly four times the threshold -
        // only the exponent field differs.
        Assert.Equal(0xBFC90FDBu, Bits(-RetailBattleEngineInterpolation.WrapThreshold));
        Assert.Equal(
            RetailBattleEngineInterpolation.WrapAmount,
            RetailBattleEngineInterpolation.WrapThreshold * 4.0f);
    }

    // No straddle, no correction - and in particular nothing wraps just because
    // the difference is large, only because the two angles sit in opposite outer
    // quadrants.
    [Theory]
    [InlineData(0.1f, 0.2f)]
    [InlineData(-3.0f, 1.0f)]
    [InlineData(3.0f, -1.0f)]
    [InlineData(3.0f, 3.0f)]
    [InlineData(-3.0f, -3.0f)]
    [InlineData(0.0f, 0.0f)]
    public void AdjustedOldAngle_LeavesTheOldAngleAloneWithoutAStraddle(float current, float old) =>
        Assert.Equal((double)old, RetailBattleEngineInterpolation.AdjustedOldAngle(current, old));

    [Fact]
    public void AdjustedOldAngle_BringsAHighOldAngleDownAcrossTheSeam()
    {
        double expected = 3.0 - (double)RetailBattleEngineInterpolation.WrapAmount;

        Assert.Equal(expected, RetailBattleEngineInterpolation.AdjustedOldAngle(-3.0f, 3.0f));
    }

    [Fact]
    public void AdjustedOldAngle_BringsALowOldAngleUpAcrossTheSeam()
    {
        double expected = -3.0 + (double)RetailBattleEngineInterpolation.WrapAmount;

        Assert.Equal(expected, RetailBattleEngineInterpolation.AdjustedOldAngle(3.0f, -3.0f));
    }

    // Every threshold test is strict. At exactly the threshold nothing moves, on
    // either arm and for either operand.
    [Fact]
    public void AdjustedOldAngle_UsesStrictThresholdsEverywhere()
    {
        const float t = RetailBattleEngineInterpolation.WrapThreshold;
        float justOver = FromBits(Bits(t) + 1u);

        // current exactly at -t takes the positive arm, where it is not > t.
        Assert.Equal(3.0, RetailBattleEngineInterpolation.AdjustedOldAngle(-t, 3.0f));

        // current exactly at +t is not > t either.
        Assert.Equal(-3.0, RetailBattleEngineInterpolation.AdjustedOldAngle(t, -3.0f));

        // old exactly at +t is not > t, so the negative arm does nothing.
        Assert.Equal((double)t, RetailBattleEngineInterpolation.AdjustedOldAngle(-3.0f, t));

        // old exactly at -t is not < -t either.
        Assert.Equal((double)(-t), RetailBattleEngineInterpolation.AdjustedOldAngle(3.0f, -t));

        // One ulp past each threshold and the corrections fire.
        Assert.Equal(
            (double)justOver - (double)RetailBattleEngineInterpolation.WrapAmount,
            RetailBattleEngineInterpolation.AdjustedOldAngle(-3.0f, justOver));
        Assert.Equal(
            (double)(-justOver) + (double)RetailBattleEngineInterpolation.WrapAmount,
            RetailBattleEngineInterpolation.AdjustedOldAngle(3.0f, -justOver));
        Assert.Equal(
            3.0 - (double)RetailBattleEngineInterpolation.WrapAmount,
            RetailBattleEngineInterpolation.AdjustedOldAngle(-justOver, 3.0f));
    }

    // test ah, 1 on the `current < -threshold` test is C0 alone, so an unordered
    // current takes the NEGATIVE arm. A source-faithful model would fall to the
    // positive arm and answer 3.0 here.
    [Fact]
    public void AdjustedOldAngle_SendsAnUnorderedCurrentAngleDownTheNegativeArm()
    {
        Assert.Equal(
            3.0 - (double)RetailBattleEngineInterpolation.WrapAmount,
            RetailBattleEngineInterpolation.AdjustedOldAngle(float.NaN, 3.0f));

        // And on that arm an old angle inside the seam is still left alone.
        Assert.Equal(1.0, RetailBattleEngineInterpolation.AdjustedOldAngle(float.NaN, 1.0f));
    }

    // test ah, 1 on the `old < -threshold` test is C0 alone as well, so an
    // unordered OLD angle gets a full turn added on the positive arm. The result
    // is still unordered, which is exactly why this has to be asserted on the
    // intermediate.
    [Fact]
    public void AdjustedOldAngle_AddsATurnToAnUnorderedOldAngle()
    {
        double adjusted = RetailBattleEngineInterpolation.AdjustedOldAngle(3.0f, float.NaN);

        Assert.True(double.IsNaN(adjusted));

        // The mask is visible in which arm ran: on the negative arm the
        // unordered old angle fails the `> threshold` test and is untouched, and
        // the two are only distinguishable because both are exposed.
        Assert.True(double.IsNaN(RetailBattleEngineInterpolation.AdjustedOldAngle(-3.0f, float.NaN)));
    }

    // Whatever arm an unordered input takes, the component comes out unordered -
    // so the two mask divergences above provably cannot reach the returned
    // angles.
    [Fact]
    public void InterpolateAngle_IsUnorderedOnEveryUnorderedInput()
    {
        Assert.True(float.IsNaN(RetailBattleEngineInterpolation.InterpolateAngle(float.NaN, 3.0f, 0.5f)));
        Assert.True(float.IsNaN(RetailBattleEngineInterpolation.InterpolateAngle(float.NaN, 1.0f, 0.5f)));
        Assert.True(float.IsNaN(RetailBattleEngineInterpolation.InterpolateAngle(3.0f, float.NaN, 0.5f)));
        Assert.True(float.IsNaN(RetailBattleEngineInterpolation.InterpolateAngle(-3.0f, float.NaN, 0.5f)));
        Assert.True(float.IsNaN(RetailBattleEngineInterpolation.InterpolateAngle(1.0f, 2.0f, float.NaN)));
    }

    // A fraction of zero returns the old angle exactly, and a fraction of one
    // returns the current angle exactly - as long as nothing wrapped.
    [Fact]
    public void InterpolateAngle_IsExactAtBothEndsOfTheFraction()
    {
        Assert.Equal(Bits(0.25f), Bits(RetailBattleEngineInterpolation.InterpolateAngle(1.75f, 0.25f, 0.0f)));
        Assert.Equal(Bits(1.75f), Bits(RetailBattleEngineInterpolation.InterpolateAngle(1.75f, 0.25f, 1.0f)));
    }

    // The correction is applied to the DIFFERENCE and the base added back is the
    // original old angle. Halfway between 3.0 and -3.0 across the seam is
    // +float(pi), not -float(pi): a rebuild that interpolated from the adjusted
    // old angle would answer with the sign flipped.
    [Fact]
    public void InterpolateAngle_KeepsTheAnswerNearTheOriginalOldAngle()
    {
        float halfway = RetailBattleEngineInterpolation.InterpolateAngle(-3.0f, 3.0f, 0.5f);

        Assert.Equal(0x40490FDBu, Bits(halfway));
        Assert.True(halfway > 3.0f);

        float back = RetailBattleEngineInterpolation.InterpolateAngle(3.0f, -3.0f, 0.5f);

        Assert.Equal(0xC0490FDBu, Bits(back));
        Assert.True(back < -3.0f);
    }

    // Without the wrap the same pair interpolates the long way round, which is
    // the whole point of the correction.
    [Fact]
    public void InterpolateAngle_DiffersFromTheUnwrappedInterpolation()
    {
        float wrapped = RetailBattleEngineInterpolation.InterpolateAngle(-3.0f, 3.0f, 0.5f);
        float unwrapped = (float)((-3.0 - 3.0) * 0.5 + 3.0);

        Assert.Equal(0.0f, unwrapped);
        Assert.NotEqual(Bits(unwrapped), Bits(wrapped));
    }

    // The adjusted old angle stays at the ambient precision and is never stored
    // back to a float. Here the double result is not representable as a float, and
    // narrowing it moves the answer by one ulp.
    [Fact]
    public void AdjustedOldAngle_StaysWideAndThatIsObservable()
    {
        float old = FromBits(0x3FC90FDEu);
        const float current = -3.0f;
        const float fraction = 0.5f;

        double wide = RetailBattleEngineInterpolation.AdjustedOldAngle(current, old);

        Assert.NotEqual(wide, (double)(float)wide);

        float shipped = RetailBattleEngineInterpolation.InterpolateAngle(current, old, fraction);
        float narrowed = (float)(((double)current - (double)(float)wide) * (double)fraction + (double)old);

        Assert.Equal(0x401B53D2u, Bits(shipped));
        Assert.Equal(0x401B53D3u, Bits(narrowed));
    }

    // The three components run the same body over their own pair, so one can wrap
    // while the others do not.
    [Fact]
    public void GetInterpolatedEulerOrientation_WrapsEachComponentIndependently()
    {
        var current = new RetailEulerAngles(-3.0f, 0.1f, 3.0f);
        var old = new RetailEulerAngles(3.0f, 0.2f, -3.0f);

        var result = RetailBattleEngineInterpolation.GetInterpolatedEulerOrientation(
            current, old, 0.5f);

        Assert.Equal(0x40490FDBu, Bits(result.Yaw));
        Assert.Equal(0x3E19999Au, Bits(result.Pitch));
        Assert.Equal(0xC0490FDBu, Bits(result.Roll));
    }

    // Each component is the same function of its own pair, which pins the member
    // order: swapping yaw and roll in the model would swap them here.
    [Fact]
    public void GetInterpolatedEulerOrientation_MapsEachComponentToItsOwnPair()
    {
        var current = new RetailEulerAngles(1.0f, 2.0f, 3.0f);
        var old = new RetailEulerAngles(0.0f, 0.0f, 0.0f);

        var result = RetailBattleEngineInterpolation.GetInterpolatedEulerOrientation(
            current, old, 0.5f);

        Assert.Equal(0.5f, result.Yaw);
        Assert.Equal(1.0f, result.Pitch);
        Assert.Equal(1.5f, result.Roll);
    }

    // A fraction of zero reproduces the old orientation word for word, which is
    // what a paused render frame has to look like.
    [Fact]
    public void GetInterpolatedEulerOrientation_ReturnsTheOldAnglesAtZeroFraction()
    {
        var old = new RetailEulerAngles(-0.75f, 1.25f, 2.5f);
        var result = RetailBattleEngineInterpolation.GetInterpolatedEulerOrientation(
            new RetailEulerAngles(3.0f, -3.0f, 0.0f), old, 0.0f);

        Assert.Equal(old, result);
    }
}
