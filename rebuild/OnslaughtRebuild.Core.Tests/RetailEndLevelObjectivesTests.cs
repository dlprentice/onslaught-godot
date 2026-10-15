// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailEndLevelObjectives"/> against
/// <c>references/Onslaught/EndLevelData.cpp:21-49</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x004496E0</c>.
/// </summary>
public sealed class RetailEndLevelObjectivesTests
{
    private static int[] Statuses(params int[] leading)
    {
        var all = new int[RetailEndLevelObjectives.SecondaryObjectiveCount];
        leading.CopyTo(all, 0);
        return all;
    }

    [Fact]
    public void Constants_MatchTheInstructionImmediates()
    {
        Assert.Equal(1, RetailEndLevelObjectives.StatusComplete);
        Assert.Equal(2, RetailEndLevelObjectives.StatusFailed);
        Assert.Equal(10, RetailEndLevelObjectives.SecondaryObjectiveCount);
        Assert.Equal(8, RetailEndLevelObjectives.MissionObjectiveStride);
        Assert.Equal(0x4D0, RetailEndLevelObjectives.SecondaryObjectivesOffset);
    }

    // The whole CEndLevelData prefix has to close, or one of the constants is
    // wrong: mBaseThingsLeft is 288 dwords, the two objective arrays are ten
    // eight-byte records each, and mThingsKilled lands six scalars later.
    [Fact]
    public void Layout_ClosesAgainstTheTwoMeasuredGlobals()
    {
        const int baseThingsLeftBytes = 288 * 4;

        Assert.Equal(0x480, baseThingsLeftBytes);
        Assert.Equal(
            RetailEndLevelObjectives.SecondaryObjectivesOffset,
            baseThingsLeftBytes +
                RetailEndLevelObjectives.PrimaryObjectiveCount *
                RetailEndLevelObjectives.MissionObjectiveStride);

        int worldFinishedOffset =
            RetailEndLevelObjectives.SecondaryObjectivesOffset +
            RetailEndLevelObjectives.SecondaryObjectiveCount *
            RetailEndLevelObjectives.MissionObjectiveStride;

        Assert.Equal(0x520, worldFinishedOffset);
        Assert.Equal(0x18, RetailEndLevelObjectives.ThingsKilledOffset - worldFinishedOffset);

        // The two absolutes RetailCareerCounters already measures.
        Assert.Equal(
            0x00672E18u,
            RetailEndLevelObjectives.EndLevelDataAddress + (uint)worldFinishedOffset);
        Assert.Equal(
            0x00672E30u,
            RetailEndLevelObjectives.EndLevelDataAddress +
                (uint)RetailEndLevelObjectives.ThingsKilledOffset);
    }

    [Fact]
    public void IsAllSecondaryObjectivesComplete_IsTrueWhenEveryStatusIsComplete()
    {
        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(1, 1, 1, 1, 1, 1, 1, 1, 1, 1));

        Assert.True(verdict.Result);
        Assert.True(verdict.AnyObjectiveSet);
    }

    // One completed objective is enough to satisfy is_set; the untouched
    // entries are neither complete nor failed and do not count against it.
    [Fact]
    public void IsAllSecondaryObjectivesComplete_IgnoresStatusesOutsideTheTwo()
    {
        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(0, 1, 0, 3, 0, -1, 0, 99, 0, 0));

        Assert.True(verdict.Result);
        Assert.True(verdict.AnyObjectiveSet);
    }

    [Fact]
    public void IsAllSecondaryObjectivesComplete_IsFalseOnAnyFailure()
    {
        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(1, 1, 2, 1, 1));

        Assert.False(verdict.Result);
        Assert.True(verdict.AnyObjectiveSet);
    }

    // res is never restored, so a completed objective after a failed one cannot
    // rescue the verdict, in either order. Retail also has no early exit - but
    // adding one is a proven EQUIVALENCE rather than an untested corner: is_set
    // is written before the failure check, the loop has no other effect, and so a
    // return on the first failure gives the same (FALSE, TRUE) the full scan
    // gives. Mutating the loop that way survives the whole suite by construction.
    [Fact]
    public void IsAllSecondaryObjectivesComplete_NeverRecoversFromAFailure()
    {
        var failFirst = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(2, 1, 1, 1, 1, 1, 1, 1, 1, 1));
        var failLast = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(1, 1, 1, 1, 1, 1, 1, 1, 1, 2));

        Assert.False(failFirst.Result);
        Assert.False(failLast.Result);
        Assert.True(failFirst.AnyObjectiveSet);
        Assert.True(failLast.AnyObjectiveSet);
    }

    // A failure in the LAST slot proves the loop ran all ten entries: an
    // off-by-one bound would report TRUE.
    [Fact]
    public void IsAllSecondaryObjectivesComplete_ReadsTheTenthEntry()
    {
        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            Statuses(0, 0, 0, 0, 0, 0, 0, 0, 0, 2));

        Assert.False(verdict.Result);
        Assert.True(verdict.AnyObjectiveSet);
    }

    // Nothing set at all logs and returns FALSE - the same FALSE a real failure
    // gives, which is why AnyObjectiveSet exists.
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-2)]
    public void IsAllSecondaryObjectivesComplete_ReportsFailureWhenNothingIsSet(int filler)
    {
        var all = new int[RetailEndLevelObjectives.SecondaryObjectiveCount];
        for (int index = 0; index < all.Length; index++)
        {
            all[index] = filler;
        }

        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(all);

        Assert.False(verdict.Result);
        Assert.False(verdict.AnyObjectiveSet);
    }

    // The two falses are distinguishable here and are not distinguishable to a
    // retail caller; both are pinned so a rebuild cannot merge them.
    [Fact]
    public void IsAllSecondaryObjectivesComplete_SeparatesTheTwoFalses()
    {
        var noObjectives = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(Statuses());
        var oneFailed = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(Statuses(2));

        Assert.Equal(noObjectives.Result, oneFailed.Result);
        Assert.NotEqual(noObjectives.AnyObjectiveSet, oneFailed.AnyObjectiveSet);
    }

    [Fact]
    public void IsAllSecondaryObjectivesComplete_RequiresExactlyTenStatuses()
    {
        Assert.Throws<ArgumentNullException>(
            () => RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(null!));
        Assert.Throws<ArgumentException>(
            () => RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(new[] { 1, 1, 1 }));
    }
}
