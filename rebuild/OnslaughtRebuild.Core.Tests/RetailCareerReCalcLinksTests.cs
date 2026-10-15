// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the Level 100 post-Won consumer of
/// <see cref="RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete"/>
/// inside <c>CCareer::ReCalcLinks</c> / <c>CCareer::Update</c>. Source
/// <c>Career.cpp:379-515</c>; retail identities <c>0x0041BD00</c> and
/// <c>0x0041BDF0</c>. The predicate itself is already pinned at
/// <c>0x004496E0</c>; this suite pins the career graph that reads it.
/// </summary>
public sealed class RetailCareerReCalcLinksTests
{
    private static int[] UnsetSecondaries() =>
        new int[RetailEndLevelObjectives.SecondaryObjectiveCount];

    [Fact]
    public void Constants_MatchTheShippedTrainingSliceAndGameStateWon()
    {
        Assert.Equal(100, RetailCareerReCalcLinks.TrainingWorldNumber);
        Assert.Equal(110, RetailCareerReCalcLinks.TrainingLowerChildWorldNumber);
        Assert.Equal(-1, RetailCareerReCalcLinks.TrainingHigherChildNodeIndex);
        Assert.Equal(4, RetailCareerReCalcLinks.TrainingPrimaryObjectiveCount);
        Assert.Equal(0, RetailCareerReCalcLinks.TrainingSecondaryObjectiveCount);
        Assert.Equal(5, RetailCareerReCalcLinks.GameStateLevelWon);
        Assert.Equal(4, RetailCareerReCalcLinks.GameStateLevelLost);
        Assert.Equal(0, RetailCareerNodeLink.NotComplete);
        Assert.Equal(1, RetailCareerNodeLink.Complete);
        Assert.Equal(2, RetailCareerNodeLink.CompleteBroken);
    }

    /// <summary>
    /// Level 100 ships four primaries and no secondaries. FillOut copies
    /// ten unset secondary statuses; the already-proven predicate logs and
    /// returns FALSE. A rebuild that treated "no secondaries" as success
    /// would unlock a higher child the graph does not have.
    /// </summary>
    [Fact]
    public void Level100Won_SecondaryPredicateIsTheNoObjectivesFalse()
    {
        var verdict = RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
            UnsetSecondaries());

        Assert.False(verdict.Result);
        Assert.False(verdict.AnyObjectiveSet);
        Assert.Equal(0, RetailCareerReCalcLinks.TrainingSecondaryObjectiveCount);
        Assert.False(RetailCareerReCalcLinks.AppliesSecondaryRankingClamp);
    }

    /// <summary>
    /// After a cold-career Level 100 win, <c>CCareer::Update</c> marks world
    /// 100 complete and <c>ReCalcLinks</c> promotes the lower child (world
    /// 110) even though the secondary predicate is FALSE. The higher link
    /// exists as a dummy (<c>mToNode == -1</c>) and stays
    /// <c>CN_NOT_COMPLETE</c>. World 100 is the unscored training world, so
    /// kill totals do not move. Mutation: requiring the predicate for the
    /// lower link leaves 110 locked; treating the no-objectives FALSE as
    /// TRUE completes the dummy higher link.
    /// </summary>
    [Fact]
    public void Level100Won_UnlocksWorld110EvenThoughTheSecondaryPredicateIsTheNoObjectivesFalse()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Counters.UpdateThingsKilled(200, new[] { 4, 5, 6, 7, 8 });

        career.ApplyUpdate(
            RetailCareerReCalcLinks.GameStateLevelWon,
            RetailCareerReCalcLinks.TrainingWorldNumber,
            ranking: 1.0f,
            UnsetSecondaries(),
            thingsKilledThisLevel: new[] { 3, 5, 7, 11, 13 });

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        RetailCareerNodeLink higher = career.GetLink(training.HigherLink)!;

        Assert.Equal(1, training.Complete);
        Assert.Equal(1, career.CareerInProgress);
        Assert.Equal(0, next.Complete);
        Assert.Equal(1, lower.ToNode);
        Assert.Equal(RetailCareerNodeLink.Complete, lower.LinkType);
        Assert.Equal(-1, higher.ToNode);
        Assert.Equal(RetailCareerNodeLink.NotComplete, higher.LinkType);
        Assert.Equal(new[] { 4, 5, 6, 7, 8 }, career.Counters.KilledThings);
        Assert.Equal(1.0f, training.Ranking);
    }

    [Fact]
    public void Level100Lost_DoesNotTouchTheTrainingGraph()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        career.ApplyUpdate(
            RetailCareerReCalcLinks.GameStateLevelLost,
            RetailCareerReCalcLinks.TrainingWorldNumber,
            ranking: 1.0f,
            UnsetSecondaries(),
            thingsKilledThisLevel: new[] { 3, 5, 7, 11, 13 });

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;

        Assert.Equal(0, training.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.NotComplete, lower.LinkType);
        Assert.Equal(RetailCareerNode.BlankRanking, training.Ranking);
    }
}
