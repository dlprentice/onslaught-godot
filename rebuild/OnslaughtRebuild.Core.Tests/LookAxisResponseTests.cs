// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The released look-axis curve, asserted as a law rather than as a table.
///
/// <para>The table in <see cref="LookAxisResponse"/> is generated data. A test
/// that restated its entries would only prove the file was copied correctly.
/// These tests re-derive the released expression from
/// <c>references/Onslaught/Player.cpp:334-355</c> in double precision and hold
/// the integer table to it, so a mis-generated or hand-edited entry fails.</para>
///
/// <para>The direction of this curve was recorded backwards once. The first
/// test below is the guard against that: retail's response near centre is
/// LOWER than linear, not higher.</para>
///
/// <para>The bound is ±0.5 permille, not the ±1.0 it was while the table was
/// interpolated. See the comment on
/// <see cref="TableMatchesTheReleasedExpression_AtEveryRepresentableInput"/>.</para>
/// </summary>
public sealed class LookAxisResponseTests
{
    private static double ReleasedLaw(double val)
    {
        // Player.cpp:334-355, with the 3.0 left in place so the ported
        // expression can be compared against the source line by line.
        double t1 = val > 0.0
            ? Math.Tan(val * 1.2) * 3.0
            : -(Math.Tan(-val * 1.2) * 3.0);
        double t2 = Math.Tan(1.2) * 3.0;
        return t1 / t2;
    }

    [Fact]
    public void CurveIsCompressive_NotExpansive()
    {
        // The developers' own comment is the specification: "should give a
        // curve so 50% before would result in 25% after (note 0% before = 0%
        // after and 100% before = 100% after)".
        Assert.Equal(0, LookAxisResponse.Apply(0));
        Assert.Equal(1_000, LookAxisResponse.Apply(1_000));

        Assert.InRange(LookAxisResponse.Apply(500), 250, 275);

        // The slope at centre is 1.2 / tan(1.2) = 0.4665, so a tenth of full
        // deflection returns roughly a twentieth. This is the number that was
        // recorded backwards; a curve with the normalising divisor dropped
        // returns ~360 here instead of ~47.
        Assert.InRange(LookAxisResponse.Apply(100), 44, 50);

        // No partial input may come back above linear. Below about 10 permille
        // the compression is smaller than one permille, so quantisation — not
        // the law — decides the last digit; equality is permitted and the two
        // assertions above carry the actual claim.
        for (int input = 1; input < 1_000; input++)
        {
            Assert.True(
                LookAxisResponse.Apply(input) <= input,
                $"input {input} returned {LookAxisResponse.Apply(input)}, " +
                "which is above linear");
        }
    }

    [Fact]
    public void TableMatchesTheReleasedExpression_AtEveryRepresentableInput()
    {
        // Tightened from ±1.0 to ±0.5 on 2026-07-31, when the table went to one
        // entry per representable input. ±1.0 was the bound while the table was
        // sampled every 10 permille and interpolated, and it sat at 94 % of that
        // budget (0.944 at input 985), so an unrelated edit could breach it.
        // 0.5 is the floor for any response that is an integer permille, and the
        // table is now at that floor everywhere; the worst case is 0.4997, at
        // input 348, where the law is 172.5003 and no integer can be closer.
        for (int input = 0; input <= 1_000; input++)
        {
            double truth = ReleasedLaw(input / 1_000.0) * 1_000.0;
            Assert.InRange(LookAxisResponse.Apply(input), truth - 0.5, truth + 0.5);
        }
    }

    /// <summary>
    /// The bound above states the headroom; this states why it cannot erode.
    /// Every response is the nearest integer to the law, so no entry can be
    /// edited by even one permille without failing — the margin between the
    /// worst case (0.4997) and the next reachable value (0.5003, an entry one
    /// permille out at input 348) is what the ±0.5 bound is cutting between.
    /// </summary>
    [Fact]
    public void EveryResponseIsTheNearestIntegerToTheLaw_SoNoEntryCanDriftSilently()
    {
        for (int input = 0; input <= 1_000; input++)
        {
            double truth = ReleasedLaw(input / 1_000.0) * 1_000.0;
            Assert.Equal((int)Math.Floor(truth + 0.5), LookAxisResponse.Apply(input));
        }
    }

    [Fact]
    public void ResponseIsOddSymmetric_SoInversionMayBeAppliedEitherSide()
    {
        // Player.cpp applies GetReverseLookYAxis() at :326-332, BEFORE the
        // curve. Core inverts elsewhere; this is what makes that harmless.
        for (int input = 0; input <= 1_000; input += 7)
        {
            Assert.Equal(-LookAxisResponse.Apply(input), LookAxisResponse.Apply(-input));
        }
    }

    [Fact]
    public void ResponseIsMonotonic_AndClampsBeyondFullDeflection()
    {
        int previous = LookAxisResponse.Apply(0);
        for (int input = 1; input <= 1_000; input++)
        {
            int current = LookAxisResponse.Apply(input);
            Assert.True(current >= previous, $"non-monotonic at {input}");
            previous = current;
        }

        Assert.Equal(1_000, LookAxisResponse.Apply(4_000));
        Assert.Equal(-1_000, LookAxisResponse.Apply(-4_000));
    }
}
