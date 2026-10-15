// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

public sealed class RetailDebriefingProjectionTests
{
    [Fact]
    public void Lost_MixedPrimaryIsIncomplete_AndGradeIsHidden()
    {
        RetailEndLevelSnapshot snapshot =
            RetailFillOutEndLevelData.ForLevel100Won() with
            {
                FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
                PrimaryStatuses =
                [
                    RetailEndLevelObjectives.StatusComplete,
                    0,
                    RetailEndLevelObjectives.StatusFailed,
                    0, 0, 0, 0, 0, 0, 0,
                ],
                SecondaryStatuses = new int[RetailEndLevelObjectives.SecondaryObjectiveCount],
            };

        RetailDebriefingProjection result =
            RetailDebriefingProjection.From(snapshot, newGoodieCount: 123, firstGoodieFlag: 1);

        Assert.Equal(RetailDebriefingMissionStatus.Defeat, result.MissionStatus);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Incomplete,
            result.PrimaryObjectives);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Hidden,
            result.SecondaryObjectives);
        Assert.Null(result.GradeByte);
        Assert.Equal(99, result.NewGoodieCount);
        Assert.True(result.FirstGoodie);
    }

    [Fact]
    public void WonWorld500_ForcesPrimaryComplete_AndProjectsGradeB()
    {
        RetailEndLevelSnapshot snapshot =
            RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.5f) with
            {
                WorldFinished = 500,
                PrimaryStatuses =
                [
                    RetailEndLevelObjectives.StatusFailed,
                    0, 0, 0, 0, 0, 0, 0, 0, 0,
                ],
                SecondaryStatuses =
                [
                    RetailEndLevelObjectives.StatusComplete,
                    0, 0, 0, 0, 0, 0, 0, 0, 0,
                ],
            };

        RetailDebriefingProjection result =
            RetailDebriefingProjection.From(snapshot, newGoodieCount: -4, firstGoodieFlag: 0);

        Assert.Equal(RetailDebriefingMissionStatus.Victory, result.MissionStatus);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Complete,
            result.PrimaryObjectives);
        Assert.Equal(
            RetailDebriefingObjectiveSummary.Complete,
            result.SecondaryObjectives);
        Assert.Equal((byte)'B', result.GradeByte);
        Assert.Equal(-4, result.NewGoodieCount);
    }
}
