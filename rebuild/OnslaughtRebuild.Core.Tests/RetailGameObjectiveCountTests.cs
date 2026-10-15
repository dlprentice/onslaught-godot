// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <c>CGame::GetNumPrimaryObjectives</c> /
/// <c>GetNumSecondaryObjectives</c> as FillOut uses them after a
/// Level 100 win. Source <c>game.cpp:4056-4078</c>; retail
/// identities <c>0x00472670</c> and <c>0x00472690</c>. The
/// secondary clamp already pins the authored count as 0; this
/// suite pins the scan that produces that 0, and the four
/// <c>MOS_COMPLETE</c> primary statuses FillOut copies.
/// </summary>
public sealed class RetailGameObjectiveCountTests
{
    [Fact]
    public void Constants_MatchTheTenSlotTablesAndTheZeroSentinel()
    {
        Assert.Equal(0, RetailGameObjectiveCount.StatusNotDefined);
        Assert.Equal(1, RetailGameObjectiveCount.StatusComplete);
        Assert.Equal(2, RetailGameObjectiveCount.StatusFailed);
        Assert.Equal(10, RetailGameObjectiveCount.ObjectiveSlotCount);
        Assert.Equal(4, RetailGameObjectiveCount.Level100PrimaryCount);
        Assert.Equal(0, RetailGameObjectiveCount.Level100SecondaryCount);
        Assert.Equal(
            RetailEndLevelObjectives.StatusComplete,
            RetailGameObjectiveCount.StatusComplete);
        Assert.NotEqual(
            (int)Level100PrimaryObjectiveStatus.Complete,
            RetailGameObjectiveCount.StatusComplete);
    }

    /// <summary>
    /// <c>0x00472670</c> is <c>cmp dword [ecx], 0</c> / <c>je</c> /
    /// <c>inc eax</c> over ten stride-8 records. A failed primary
    /// still counts; only the zero sentinel does not. Mutation:
    /// counting only <c>MOS_COMPLETE</c> drops a failed row.
    /// This table is not Level 100 content.
    /// </summary>
    [Fact]
    public void GetNumPrimaryObjectives_CountsEveryNonZeroStatus()
    {
        int[] statuses =
        {
            RetailGameObjectiveCount.StatusComplete,
            RetailGameObjectiveCount.StatusFailed,
            RetailGameObjectiveCount.StatusNotDefined,
            RetailGameObjectiveCount.StatusComplete,
            0, 0, 0, 0, 0, 0,
        };

        Assert.Equal(3, RetailGameObjectiveCount.GetNumPrimaryObjectives(statuses));
        Assert.NotEqual(
            statuses.Count(status => status == RetailGameObjectiveCount.StatusComplete),
            RetailGameObjectiveCount.GetNumPrimaryObjectives(statuses));
    }

    /// <summary>
    /// Level 100 ships four primaries and no secondaries. After a
    /// win FillOut copies four <c>MOS_COMPLETE</c> (1) words and
    /// six unset slots, not the rebuild mission enum's
    /// <c>Complete = 2</c>. Mutation: writing 2 for complete would
    /// still count as defined but would fail the snapshot equality.
    /// </summary>
    [Fact]
    public void Level100Won_PrimaryTableIsFourCompleteOnesNotTheMissionEnumTwo()
    {
        int[] primaries = RetailGameObjectiveCount.Level100WonPrimaryStatuses();
        int[] secondaries = RetailGameObjectiveCount.Level100WonSecondaryStatuses();

        Assert.Equal(10, primaries.Length);
        Assert.Equal(new[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 }, primaries);
        Assert.All(
            primaries.Take(4),
            status => Assert.Equal(RetailGameObjectiveCount.StatusComplete, status));
        Assert.DoesNotContain((int)Level100PrimaryObjectiveStatus.Complete, primaries);
        Assert.Equal(
            4,
            RetailGameObjectiveCount.GetNumPrimaryObjectives(primaries));
        Assert.Equal(
            0,
            RetailGameObjectiveCount.GetNumSecondaryObjectives(secondaries));
        Assert.Equal(
            RetailFillOutEndLevelData.UnsetSecondaryStatuses(),
            secondaries);
    }
}
