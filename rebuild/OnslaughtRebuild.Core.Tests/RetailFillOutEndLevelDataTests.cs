// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the Level 100 Won snapshot
/// <c>CGame::FillOutEndLevelData</c> hands to career. Source
/// <c>game.cpp:910-1043</c>; retail identity <c>0x0046D470</c>.
/// The secondary predicate is already pinned; this suite pins that
/// FillOut does not consult it when the authored secondary count is 0.
/// </summary>
public sealed class RetailFillOutEndLevelDataTests
{
    [Fact]
    public void Level100Won_SnapshotIsWorld100StateWonWithNoSecondaries()
    {
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(100, snapshot.WorldFinished);
        Assert.Equal(RetailCareerReCalcLinks.GameStateLevelWon, snapshot.FinalState);
        Assert.Equal(
            RetailEndLevelObjectives.SecondaryObjectiveCount,
            snapshot.SecondaryStatuses.Count);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
        Assert.Equal(0, RetailFillOutEndLevelData.Level100SecondaryCount);
        Assert.Equal(4, RetailFillOutEndLevelData.Level100PrimaryCount);

        RetailSecondaryObjectiveVerdict verdict =
            RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
                snapshot.SecondaryStatuses);
        Assert.False(verdict.Result);
        Assert.False(verdict.AnyObjectiveSet);
    }

    /// <summary>
    /// Last LoadWorld on Level 100 is outer RLWD. <c>CGame+0x108/+0x10c</c>
    /// are RLWD <c>+0x147ba/+0x147be</c> = 300.0 / 500.0, so
    /// <c>test ah,0x41 / jne 0x0046d79b</c> does not skip. A zero score
    /// against last-wins D=70 stores 0 at <c>0x0046d772</c> and jumps
    /// past the 0.001 replacement. Mutation: inventing a skip leaves the
    /// pre-arm 1.0f. First-play elapsed and score stay unclaimed — this
    /// does not rewrite <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
    /// Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ScoreTimeArmRewritesAZeroScoreToZeroBecauseRlwdDeltaIsPositive()
    {
        Assert.False(
            RetailFillOutEndLevelData.ScoreTimeArmSkips(
                RetailFillOutEndLevelData.Level100FullScoreTime,
                RetailFillOutEndLevelData.Level100PercentageScoreTime));

        float early = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: 0,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);
        float late = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 600.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: 0,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);

        Assert.Equal(0.0f, early);
        Assert.Equal(0.0f, late);
        Assert.NotEqual(1.0f, early);
        Assert.NotEqual(0.001f, early);
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Last-wins RLWD dword after the percentage time is
    /// <c>+0x147c2</c> = 1.0 into <c>CGame+0x110</c> (not the
    /// <c>LoadLevel</c> leftover 0.5). Mid-band elapsed therefore does
    /// not scale the score. Mutation: adopting 0.5 turns a 140 mid-band
    /// score at elapsed 400 into ranking 0.25. First-play elapsed and
    /// score stay unclaimed — this does not rewrite
    /// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>. Do not
    /// invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ScoreTimeArmDoesNotScaleAMidScoreByElapsedBecauseScorePercentageIsOne()
    {
        const int midScore = 140;

        float early = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: midScore,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);
        float midBand = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 400.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: midScore,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);

        Assert.Equal(0.5f, early);
        Assert.Equal(0.5f, midBand);
        Assert.NotEqual(0.25f, midBand);
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Last-wins D is 70 at <c>this+0xfc</c>. Independently re-read
    /// specimen: <c>0x0046d724</c> <c>jl 0x0046d772</c> stores 0 only
    /// when the fistp'd score is strictly below D. Equality falls
    /// through the interpolate-to-0 path and the already-cited
    /// <c>0x3a83126f</c> (0.001) replacement. First-play elapsed and
    /// score stay unclaimed — this does not rewrite
    /// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
    /// Mutation: skip the 0.001 replacement leaves 0. Do not invent
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ScoreTimeArmStoresPointZeroZeroOneAtExactLastWinsD()
    {
        float atD = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: RetailFillOutEndLevelData.Level100DGradeScore,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);
        float belowD = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: RetailFillOutEndLevelData.Level100DGradeScore - 1,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);

        Assert.Equal(0.001f, atD);
        Assert.Equal(0.0f, belowD);
        Assert.NotEqual(0.0f, atD);
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Last-wins S is 210 at <c>this+0xf8</c>. Independently re-read
    /// specimen <c>74154bfa…</c> and inflated <c>100_res_PC.aya</c>
    /// <c>115ede05…2df4</c>: <c>0x0050d2f7</c>
    /// <c>mov [0x008a9b90], edx</c> from RLWD payload <c>+0x147ae</c>
    /// = 210. <c>0x0046d6e5</c> loads <c>[ebp+0xf8]</c>,
    /// <c>0x0046d6f9</c> <c>cmp eax,ecx</c>, <c>0x0046d707</c>
    /// <c>jl 0x0046d718</c> so equality stores <c>0x3f800000</c> at
    /// <c>0x0046d709</c>. Not leftover BSWD 1000. First-play elapsed
    /// and score stay unclaimed — this does not rewrite
    /// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
    /// Mutation: adopt leftover BSWD 1000. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ScoreTimeArmStoresOneAtExactLastWinsS()
    {
        Assert.Equal(210, RetailFillOutEndLevelData.Level100SGradeScore);
        Assert.NotEqual(1000, RetailFillOutEndLevelData.Level100SGradeScore);

        float atS = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: 210,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);

        Assert.Equal(1.0f, atS);
        Assert.NotEqual(140.0f / 930.0f, atS);
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Last-wins D is 70 at <c>this+0xfc</c>. Independently re-read
    /// specimen <c>74154bfa…</c> and inflated <c>100_res_PC.aya</c>
    /// <c>115ede05…2df4</c>: <c>0x0050d307</c>
    /// <c>mov [0x008a9b94], edx</c> from RLWD payload <c>+0x147b2</c>
    /// = 70. A fistp'd 70 against leftover BSWD 200 is strictly below
    /// D and stores 0 at <c>0x0046d772</c>. First-play elapsed and
    /// score stay unclaimed — this does not rewrite
    /// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
    /// Mutation: adopt leftover BSWD 200. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ScoreTimeArmUsesLastWinsDSeventyNotLeftoverBswdTwoHundred()
    {
        Assert.Equal(70, RetailFillOutEndLevelData.Level100DGradeScore);
        Assert.NotEqual(200, RetailFillOutEndLevelData.Level100DGradeScore);

        float atLastWinsD = RetailFillOutEndLevelData.AfterScoreTimeArm(
            preArmRanking: 1.0f,
            elapsedTime: 0.0f,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            RetailFillOutEndLevelData.Level100ScorePercentage,
            score: 70,
            RetailFillOutEndLevelData.Level100SGradeScore,
            RetailFillOutEndLevelData.Level100DGradeScore);

        Assert.Equal(0.001f, atLastWinsD);
        Assert.NotEqual(0.0f, atLastWinsD);
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>game.cpp:967</c> stores <c>mRanking = 1.0f</c> before the
    /// score-time arm. Level 100's secondary count is 0, so the 0.4 / 0.6
    /// clamp never runs. Mutation: defaulting the snapshot ranking to the
    /// failed-secondary 0.6 cap fails this equality. First-play elapsed
    /// and score stay unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_SnapshotRankingIsThePreClampOnePointZero()
    {
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(1.0f, snapshot.Ranking);
        Assert.NotEqual(0.6f, snapshot.Ranking);
        Assert.NotEqual(0.4f, snapshot.Ranking);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FillOut walks Size=35 and stores 1 at 0..34 (including two
    /// type-37 <c>CSafeSide</c>). Slots 35..287 stay 0. Mutation:
    /// adopting the materializer's 33 visible units leaves index 33
    /// and 34 unset. Iceberg player-kill store-0 stays open. Do not
    /// invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FillOutStoresOneForEachOfThirtyFiveBaseThings()
    {
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(RetailCareerNode.BaseThingsExistsSize, snapshot.BaseThingsLeft.Count);
        Assert.Equal(35, RetailFillOutEndLevelData.Level100BaseWorldThingCount);
        Assert.NotEqual(33, RetailFillOutEndLevelData.Level100BaseWorldThingCount);
        Assert.Equal(1, snapshot.BaseThingsLeft[33]);
        Assert.Equal(1, snapshot.BaseThingsLeft[34]);
        Assert.Equal(0, snapshot.BaseThingsLeft[35]);
        Assert.Equal(35, snapshot.BaseThingsLeft.Count(value => value == 1));
        Assert.All(
            snapshot.BaseThingsLeft.Take(35),
            value => Assert.Equal(1, value));
        Assert.All(
            snapshot.BaseThingsLeft.Skip(35),
            value => Assert.Equal(0, value));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Independently re-read specimen: <c>0x0046d4d1</c>
    /// <c>test byte [eax+0x2c],4</c> / <c>jne 0x0046d4df</c> stores 0.
    /// A live pointer with TF_DYING set is therefore 0, same as a null
    /// reader. First-play still stores 1 at 0..34 — player iceberg-kill
    /// values stay unclaimed. Mutation: store 1 on TF_DYING. Do not
    /// invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FillOutStoresZeroWhenBaseThingIsDying()
    {
        Assert.Equal(
            1,
            RetailFillOutEndLevelData.BaseThingLeftWord(thingLive: true, thingFlags2c: 0));
        Assert.Equal(
            0,
            RetailFillOutEndLevelData.BaseThingLeftWord(
                thingLive: true,
                thingFlags2c: RetailFillOutEndLevelData.ThingFlagDying));
        Assert.Equal(
            0,
            RetailFillOutEndLevelData.BaseThingLeftWord(thingLive: false, thingFlags2c: 0));
        Assert.Equal(1, RetailFillOutEndLevelData.ForLevel100Won().BaseThingsLeft[4]);
        Assert.Equal(1, RetailFillOutEndLevelData.ForLevel100Won().BaseThingsLeft[9]);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Kill readout is ctor-zero plus <c>0x004d30d0</c> ConfirmedKill
    /// increments, not an authored L100 constant. A first-play Won that
    /// never takes that increment snapshots five zeros. Career still
    /// skips world 100. Mutation: writing a non-zero authored vector
    /// fails the zeros. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FirstPlayKillReadoutIsFiveZerosUnlessConfirmedKill()
    {
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            snapshot.ThingsKilled);
        Assert.Equal(
            RetailCareerCounters.KilledTypeCount,
            snapshot.ThingsKilled.Count);
        Assert.Equal(
            RetailFillOutEndLevelData.FirstPlayThingsKilled(),
            snapshot.ThingsKilled);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Independently re-read specimen <c>74154bfa…</c>:
    /// <c>0x0046d5f9</c> <c>mov eax,[ebp+0x2a4]</c> /
    /// <c>0x0046d5ff</c> <c>test eax,eax</c> / <c>je 0x0046d61d</c>
    /// stores five zeros at <c>0x00672e30</c>. Live player copies
    /// five dwords from <c>player+8</c> (<c>cmp eax,0x14</c>).
    /// Mutation: copy leftover words when the player pointer is
    /// null. First-play still has a live player 0. Do not invent
    /// first-play totals or secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FillOutStoresFiveZeroKillsWhenPlayerPointerIsNull()
    {
        int[] leftover = [1, 2, 3, 4, 5];

        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ThingsKilledReadout(
                playerPresent: false,
                playerKillWords: leftover));
        Assert.Equal(
            leftover,
            RetailFillOutEndLevelData.ThingsKilledReadout(
                playerPresent: true,
                playerKillWords: leftover));
        Assert.Equal(
            new[] { 0, 0, 0, 0, 0 },
            RetailFillOutEndLevelData.ForLevel100Won().ThingsKilled);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Independently re-read specimen <c>74154bfa…</c>:
    /// <c>DeclareLevelWon</c> <c>0x0046f323</c> stores
    /// <c>GAME_STATE_LEVEL_WON=5</c> at <c>+0x28</c> and
    /// <c>0x0046f33a</c> stores 5.0f at <c>+0x48</c>. No store to
    /// <c>+0x114</c>. FillOut <c>0x0046d5d0</c> copies that dword to
    /// <c>0x00672e2c</c>. Init is 0 (<c>EndLevelData.h:27</c>).
    /// Mutation: leftover lost-string id 123. First-play elapsed and
    /// score stay unclaimed. Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FillOutLostReasonStaysInitZeroBecauseDeclareWonDoesNotWriteIt()
    {
        Assert.Equal(0, RetailFillOutEndLevelData.Level100WonLostReason);
        Assert.Equal(
            RetailFillOutEndLevelData.Level100WonLostReason,
            RetailFillOutEndLevelData.LostReasonWord(
                RetailFillOutEndLevelData.Level100WonLostReason));
        Assert.NotEqual(
            123,
            RetailFillOutEndLevelData.LostReasonWord(
                RetailFillOutEndLevelData.Level100WonLostReason));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Independently re-read specimen <c>74154bfa…</c>:
    /// <c>0x0046d5af</c> <c>mov ecx,[0x00672fd0]</c> /
    /// <c>0x0046d5bf</c> <c>mov eax,[ebp+0xf4]</c> /
    /// <c>0x0046d5c5</c> <c>mov [0x00672e28], ecx</c> /
    /// <c>0x0046d5cb</c> <c>mov [0x00672e24], eax</c>. Those are the
    /// only FillOut stores of <c>mTimeTaken</c> / <c>mScore</c>. The
    /// score-time arm's <c>0x0046d6ff</c> <c>mov [ebp+0xf4], eax</c>
    /// rewrites <c>CGame.mScore</c> only. First-play elapsed and score
    /// stay unclaimed — this does not rewrite
    /// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
    /// Mutation: leftover score 999 / leftover time 12.5, or the
    /// post-arm scaled score under leftover <c>LoadLevel</c> 0.5.
    /// Do not invent secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_FillOutScoreAndTimeAreThePreArmCopiesNotTheScaledRewrite()
    {
        const int preArmScore = 140;
        const float eventManagerTime = 400.0f;

        int scaled = RetailFillOutEndLevelData.AfterScoreTimeArmGameScore(
            elapsedTime: eventManagerTime,
            RetailFillOutEndLevelData.Level100FullScoreTime,
            RetailFillOutEndLevelData.Level100PercentageScoreTime,
            leftoverScorePercentage: 0.5f,
            score: preArmScore);

        Assert.Equal(
            preArmScore,
            RetailFillOutEndLevelData.ScoreWord(preArmScore));
        Assert.NotEqual(
            999,
            RetailFillOutEndLevelData.ScoreWord(preArmScore));
        Assert.NotEqual(
            scaled,
            RetailFillOutEndLevelData.ScoreWord(preArmScore));
        Assert.Equal(105, scaled);
        Assert.Equal(
            eventManagerTime,
            RetailFillOutEndLevelData.TimeTakenWord(eventManagerTime));
        Assert.NotEqual(
            12.5f,
            RetailFillOutEndLevelData.TimeTakenWord(eventManagerTime));
        Assert.Equal(1.0f, RetailFillOutEndLevelData.ForLevel100Won().Ranking);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FillOut copies the ten primary <c>GetStatus()</c> words. After a
    /// Level 100 win those are four <c>MOS_COMPLETE</c> (1) and six
    /// unset slots — already pinned on
    /// <see cref="RetailGameObjectiveCount.Level100WonPrimaryStatuses"/>.
    /// That is not the rebuild mission enum's
    /// <c>Level100PrimaryObjectiveStatus.Complete = 2</c>. Mutation:
    /// writing 2 for complete fails the snapshot equality. Secondaries
    /// stay unset; do not invent them.
    /// </summary>
    [Fact]
    public void Level100Won_SnapshotCarriesFourMosCompletePrimariesNotTheMissionEnumTwo()
    {
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(
            RetailGameObjectiveCount.Level100WonPrimaryStatuses(),
            snapshot.PrimaryStatuses);
        Assert.Equal(new[] { 1, 1, 1, 1, 0, 0, 0, 0, 0, 0 }, snapshot.PrimaryStatuses);
        Assert.DoesNotContain(
            (int)Level100PrimaryObjectiveStatus.Complete,
            snapshot.PrimaryStatuses);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>game.cpp:1028</c> is <c>if (GetNumSecondaryObjectives())</c>.
    /// Level 100's count is 0, so neither the 0.4 floor nor the 0.6 cap
    /// runs. Mutation: applying the failed-secondary 0.6 cap because the
    /// predicate is FALSE would turn a 1.0 ranking into 0.6; applying the
    /// completed-secondary 0.4 floor would turn a 0.0 ranking into 0.4.
    /// </summary>
    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.0f)]
    [InlineData(0.75f)]
    public void Level100Won_DoesNotClampRankingBecauseThereAreNoSecondaries(float ranking)
    {
        float after = RetailFillOutEndLevelData.AfterSecondaryRankingClamp(
            ranking,
            RetailFillOutEndLevelData.Level100SecondaryCount,
            RetailFillOutEndLevelData.UnsetSecondaryStatuses());

        Assert.Equal(ranking, after);
        Assert.NotEqual(0.6f, after);
        Assert.NotEqual(0.4f, after == ranking ? -1.0f : after);
    }

    [Fact]
    public void Level100Won_FillOutSnapshotDrivesTheAlreadyPinnedCareerUpdate()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        RetailCareerNodeLink higher = career.GetLink(training.HigherLink)!;

        Assert.Equal(1, training.Complete);
        Assert.Equal(RetailCareerNodeLink.Complete, lower.LinkType);
        Assert.Equal(RetailCareerNodeLink.NotComplete, higher.LinkType);
    }
}
