// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Pins the Level 100 Won consumer that carries FillOut's
/// <c>END_LEVEL_DATA.mSlots</c> into <c>CCareer::Update</c> via
/// <see cref="RetailCareerCampaign.ApplyUpdate"/>. The 32-dword
/// assignment itself is already pinned; this suite only proves
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/> includes
/// the four first-play <c>SetSlotSave</c> bits and that ApplyUpdate
/// replaces career <c>mSlots</c> with them.
/// </summary>
public sealed class RetailCareerCampaignApplyUpdateTests
{
    /// <summary>
    /// After a cold-career first-play win, FillOut's snapshot holds
    /// <c>SLOT_TUTORIAL_1..4</c> (63..66) and nothing else addressable.
    /// <c>ApplyUpdate</c> assigns those 32 words over career
    /// <c>mSlots</c>, so a leftover bit dies. Mutation: OR-ing the
    /// words leaves slot 1 set; skipping the overwrite leaves 63..66
    /// unset; inventing a secondary would mark a status word 1 or 2.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateOverwritesCareerSlotsFromTheFillOutSnapshot()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Slots.SetSlot(1, 1);

        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();
        var snapshotSlots = new RetailCareerSlots();
        snapshotSlots.CopyWords(snapshot.SlotWords);

        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.Equal(0, snapshotSlots.GetSlot(1));
        Assert.Equal(0, snapshotSlots.GetSlot(62));
        Assert.Equal(0, snapshotSlots.GetSlot(67));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));

        career.ApplyUpdate(snapshot);

        Assert.Equal(0, career.Slots.GetSlot(1));
        Assert.Equal(1, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(1, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(1, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(1, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.Equal(snapshot.SlotWords, career.Slots.Words);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.Equal(
            RetailCareerNodeLink.Complete,
            career.GetLink(career.Nodes.Find(100)!.LowerLink)!.LinkType);
    }

    /// <summary>
    /// <c>CCareer::Update</c> never reads the primary table FillOut
    /// copied. Writing the rebuild mission enum <c>Complete=2</c> into
    /// that table must not change the graph. Mutation: requiring
    /// <c>MOS_COMPLETE=1</c> before completing the node leaves 110
    /// locked on the mission-enum snapshot. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateDoesNotConsultPrimaryStatuses()
    {
        RetailCareerCampaign mosComplete = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerCampaign missionEnum = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot completeTwo = won with
        {
            PrimaryStatuses = new[] { 2, 2, 2, 2, 0, 0, 0, 0, 0, 0 },
        };

        mosComplete.ApplyUpdate(won);
        missionEnum.ApplyUpdate(completeTwo);

        RetailCareerNodeLink mosLower =
            mosComplete.GetLink(mosComplete.Nodes.Find(100)!.LowerLink)!;
        RetailCareerNodeLink enumLower =
            missionEnum.GetLink(missionEnum.Nodes.Find(100)!.LowerLink)!;
        Assert.Equal(1, mosComplete.Nodes.Find(100)!.Complete);
        Assert.Equal(1, missionEnum.Nodes.Find(100)!.Complete);
        Assert.Equal(RetailCareerNodeLink.Complete, mosLower.LinkType);
        Assert.Equal(RetailCareerNodeLink.Complete, enumLower.LinkType);
        Assert.All(won.SecondaryStatuses, status => Assert.Equal(0, status));
        Assert.All(completeTwo.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CCareer::Update</c> writes <c>mRanking</c> only onto
    /// <c>GetNodeFromWorldNo(mWorldFinished)</c> (<c>Career.cpp:396-406</c>).
    /// The already-pinned FillOut 1.0f therefore lands on world 100
    /// (grade S) and world 110 stays <c>BlankRanking</c> / grade E.
    /// Mutation: copying the snapshot ranking onto the unlocked child
    /// makes 110 read 1.0 / S. Score-time, base-things, kills, and
    /// goodies stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateWritesRankingOnlyOnTheFinishedWorld()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(1.0f, snapshot.Ranking);
        Assert.Equal(1.0f, training.Ranking);
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(training.Ranking));
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailCareerGrade.GradeByteFromRanking(next.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CCareer::Update</c> never writes <c>CCareerNode.mNumAttempts</c>
    /// (<c>+0x38</c>). The only store is <c>CCareerNode::Blank</c>
    /// (<c>Career.cpp:99</c> / <c>0x0041B740</c>). A leftover attempt
    /// count on world 100 and world 110 therefore survives first-play
    /// ApplyUpdate even though Complete / Ranking / CareerInProgress
    /// change. Isolated Blank ctor 0 does not go through ApplyUpdate.
    /// Existing Won ranking / Complete tests do not name <c>+0x38</c>.
    /// Mutation: increment the finished node's <c>mNumAttempts</c>.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateDoesNotIncrementNumAttempts()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        training.NumAttempts = 7;
        next.NumAttempts = 11;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, training.Complete);
        Assert.Equal(1.0f, training.Ranking);
        Assert.Equal(7, training.NumAttempts);
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(11, next.NumAttempts);
        Assert.Equal(1, career.CareerInProgress);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>Career.cpp:405-406</c> stores <c>mRanking</c> only when the
    /// snapshot is strictly greater. A worse Level 100 replay therefore
    /// leaves the already-pinned first-play 1.0f / S in place. Mutation:
    /// assigning the snapshot even when it is not greater makes world 100
    /// read 0.5. Score-time stays unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateDoesNotDowngradeAnExistingBetterRanking()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won());
        career.ApplyUpdate(RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.5f));

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(1.0f, training.Ranking);
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(training.Ranking));
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailCareerGrade.GradeByteFromRanking(next.Ranking));
        Assert.Equal(1, training.Complete);
        Assert.Equal(0, next.Complete);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Training is not exempt from <c>UpdateGoodieStates</c>
    /// (<c>0x0041c470</c>; <c>Career.cpp:690 / 698 / 769 / 813 / 857</c>).
    /// A first-play FillOut 1.0f is grade S, so complete-100 unlocks 0 and
    /// 8 and <c>GRADE(100) &gt;= C/B/A</c> unlocks 78, 121, and 164.
    /// Mutation: skipping the recompute after ApplyUpdate leaves those
    /// five at <c>GS_UNKNOWN</c>. Score-time, base-things, and kill
    /// totals stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateUnlocksTrainingGoodiesForAnS()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);

        Assert.Equal(1.0f, snapshot.Ranking);
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(0, career.Nodes.Find(110)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Lost still calls <c>UpdateGoodieStates</c> (<c>Career.cpp:382-385</c>)
    /// but world 100 is not complete, so <c>GRADE(100)</c> is the incomplete
    /// <c>'E'</c> and none of 0 / 8 / 78 / 121 / 164 become
    /// <c>GS_NEW</c>. Mutation: unlocking those five without the complete
    /// / grade predicates would fail the zeros. Leftover complete-110
    /// plus ranking 0.0f now names the open goodie-14 store through
    /// this same Lost return; this test does not seed that leftover
    /// or name goodie 14.
    /// </summary>
    [Fact]
    public void Level100Lost_ApplyUpdateDoesNotUnlockTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot lost = won with
        {
            FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
        };

        career.ApplyUpdate(lost);

        Assert.Equal(0, career.Nodes.Find(100)!.Complete);
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(lost.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Lost still calls <c>UpdateGoodieStates</c> then returns
    /// (<c>Career.cpp:382-385</c>). World 100 stays incomplete, so
    /// <c>CountGoodies</c> does not rise and <c>new_goodie_count</c>
    /// adds 0 (<c>Career.cpp:895-897</c>). <c>first_goodie</c> is
    /// transition-only: goodie 0 was <c>GOODIE_NOT_DONE</c> and still
    /// is (<c>Career.cpp:688 / 899-900</c>). The already-pinned Lost
    /// goodie-state zeros do not name these two globals. Mutation: arm
    /// <c>first_goodie</c> whenever goodie 0 was
    /// <c>GOODIE_NOT_DONE</c> at entry. <c>mPendingExtraGoodies</c>
    /// and episode instruction marks stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Lost_ApplyUpdateLeavesGoodieLatchesAtCtorZero()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot lost = won with
        {
            FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
        };

        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);

        career.ApplyUpdate(lost);

        Assert.Equal(0, career.Nodes.Find(100)!.Complete);
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);
        Assert.All(lost.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Lost returns before <c>mSlots = END_LEVEL_DATA.mSlots</c>
    /// (<c>Career.cpp:382-385</c> / <c>392</c>). FillOut still carries
    /// first-play <c>SLOT_TUTORIAL_1..4</c> (63..66); ApplyUpdate does
    /// not assign them, so leftover career bits stay and the tutorial
    /// bits stay unset. Isolated
    /// <see cref="RetailCareerSlotHandoff.ShouldOverwriteFromEndLevel"/>
    /// does not go through ApplyUpdate. Existing Lost goodie / latch
    /// tests do not name slots. Mutation: overwrite <c>mSlots</c> on
    /// the Lost return. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Lost_ApplyUpdateDoesNotOverwriteCareerSlotsFromTheFillOutSnapshot()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Slots.SetSlot(1, 1);
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot lost = won with
        {
            FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
        };
        var snapshotSlots = new RetailCareerSlots();
        snapshotSlots.CopyWords(lost.SlotWords);

        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(1, snapshotSlots.GetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.Equal(0, snapshotSlots.GetSlot(1));
        Assert.False(RetailCareerSlotHandoff.ShouldOverwriteFromEndLevel(lost.FinalState));

        career.ApplyUpdate(lost);

        Assert.Equal(1, career.Slots.GetSlot(1));
        Assert.Equal(0, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(0, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(0, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(0, career.Slots.GetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.NotEqual(lost.SlotWords, career.Slots.Words);
        Assert.Equal(0, career.Nodes.Find(100)!.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.All(lost.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Lost returns before <c>ReCalcLinks</c> /
    /// <c>UpdateBaseWorldExistsStuffForNode</c>
    /// (<c>Career.cpp:382-385</c> / <c>416</c> / <c>443-452</c> /
    /// <c>519-527</c>). FillOut still carries first-play 1 at 0..34 and
    /// 0 at 35..287; ApplyUpdate does not copy them, so leftover Blank
    /// all-1s on world 110 stay set. Isolated
    /// <see cref="RetailCareerCampaign.UpdateBaseWorldExistsStuffForNode"/>
    /// and <c>Level100Lost_DoesNotTouchTheTrainingGraph</c> do not name
    /// leftover bits — the graph test passes no
    /// <c>mBaseThingsLeft</c>. Existing Lost goodie / latch / mSlots
    /// tests do not name them. Mutation: copy FillOut
    /// <c>mBaseThingsLeft</c> onto world 110 on the Lost return. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Lost_ApplyUpdateDoesNotCopyFillOutBaseThingsOntoWorld110()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot lost = won with
        {
            FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
        };

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerReCalcLinks.TrainingPrimaryBaseThingsWorldNumber,
            next.WorldNumber);
        Assert.Equal(RetailCareerNode.BaseThingsExistsSize, lost.BaseThingsLeft.Count);
        Assert.Equal(1, lost.BaseThingsLeft[0]);
        Assert.Equal(1, lost.BaseThingsLeft[34]);
        Assert.Equal(0, lost.BaseThingsLeft[35]);
        Assert.Equal(0, lost.BaseThingsLeft[287]);
        Assert.Equal(1, next.DoesBaseThingExist(35));
        Assert.Equal(
            RetailCareerNode.BaseThingsExistsSize,
            CountExistingBaseThings(next));

        career.ApplyUpdate(lost);

        Assert.Equal(1, next.DoesBaseThingExist(0));
        Assert.Equal(1, next.DoesBaseThingExist(34));
        Assert.Equal(1, next.DoesBaseThingExist(35));
        Assert.Equal(1, next.DoesBaseThingExist(287));
        Assert.Equal(
            RetailCareerNode.BaseThingsExistsSize,
            CountExistingBaseThings(next));
        Assert.Equal(
            RetailCareerNode.BaseThingsExistsSize,
            CountExistingBaseThings(training));
        Assert.Equal(0, training.Complete);
        Assert.Equal(0, next.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.Equal(
            RetailCareerNodeLink.NotComplete,
            career.GetLink(training.LowerLink)!.LinkType);
        Assert.All(lost.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Lost still calls <c>UpdateGoodieStates</c> then returns
    /// (<c>Career.cpp:382-385</c>). Leftover world-110 complete + E
    /// (ranking 0.0f already pinned) therefore still opens
    /// <c>Career.cpp:704</c> <c>SET_GOODIE_NEW(14)</c>. Isolated Won
    /// leftover 14 does not go through Lost ApplyUpdate. Existing Lost
    /// zeros name 0 / 8 / 78 / 121 / 164 and do not seed leftover
    /// complete-110 or name goodie 14. Lost latch does not name 14.
    /// Ranking 0.0f keeps <c>GRADE(110) &gt;= C</c> closed so goodie 1
    /// stays <c>GS_UNKNOWN</c>. World 100 stays incomplete, so the
    /// five first-play S slots stay <c>GS_UNKNOWN</c>. Do not invent a
    /// world-110 FillOut or the rest of the table. Mutation: skip
    /// <c>UpdateGoodieStates</c> on the Lost return. Skipping
    /// <c>SET_GOODIE_NEW(14)</c> is not unique versus the Won leftover
    /// pin. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Lost_ApplyUpdateLeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.0f;
        RetailEndLevelSnapshot won = RetailFillOutEndLevelData.ForLevel100Won();
        RetailEndLevelSnapshot lost = won with
        {
            FinalState = RetailCareerReCalcLinks.GameStateLevelLost,
        };

        career.ApplyUpdate(lost);

        Assert.Equal(0, career.Nodes.Find(100)!.Complete);
        Assert.Equal(1, next.Complete);
        Assert.Equal(0.0f, next.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(lost.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(100) &gt;= C</c> is not <c>&gt;= B</c>. Ranking 0.25f is
    /// already pinned as C, so a Level 100 win at that ranking unlocks
    /// 0, 8, and 78 and leaves 121 / 164 at <c>GS_UNKNOWN</c>. Mutation:
    /// treating any complete as S writes 2 into 121. Score-time stays
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeCUnlocksOnlyTheCTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(
            (byte)'C',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(110) &gt;= C</c> (<c>Career.cpp:691</c>) stays closed after
    /// a Level 100 C-grade Won. World 110 is in the cold slice, still
    /// incomplete, so <c>GRADE</c> is the already-pinned incomplete
    /// <c>'E'</c> (<c>0x0041C3FE</c>) rather than BlankRanking-as-C.
    /// Existing C-band tests unlock goodie 78 and do not name goodie 1
    /// or <c>NewGoodieCount</c>. A <c>GRADE(100)</c> mutation on the S
    /// path is not unique versus the latch count of 5. Mutation: store
    /// <c>GS_NEW</c> on goodie 1 after the CountGoodies add. Do not
    /// invent the rest of the table. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeCLeavesTheWorld110CGoodieUnknown()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(
            (byte)'C',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailWorldGrade.IncompleteGradeByte,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(110) &gt;= C</c> (<c>Career.cpp:770</c>) stays closed after
    /// a Level 100 C-grade Won. World 110 is in the cold slice, still
    /// incomplete, so <c>GRADE</c> is the already-pinned incomplete
    /// <c>'E'</c> (<c>0x0041C3FE</c>) rather than BlankRanking-as-C.
    /// Isolated closed GRADE(110) names goodie 1, not 79. Isolated
    /// leftover C 79 names the open store. FrontEndHandoff leftover
    /// 79 names <c>TryApply</c>. Existing C-band tests unlock 78 and
    /// do not name 79. Mutation: store <c>GS_NEW</c> on goodie 79 after
    /// ApplyUpdate when world 110 is incomplete. Skipping
    /// <c>SET_GOODIE_NEW(79)</c> is not unique versus leftover C
    /// concept-art. Do not invent <c>GRADE(110) &gt;= B</c> / <c>A</c>
    /// or a world-110 FillOut. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeCLeavesTheWorld110CConceptArtGoodieUnknown()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(
            (byte)'C',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailWorldGrade.IncompleteGradeByte,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCConceptArtOnWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Leftover world-110 complete + C (ranking 0.25f already pinned)
    /// opens <c>Career.cpp:691</c> <c>SET_GOODIE_NEW(1)</c>. Isolated
    /// <c>GradeByteForWorld</c> already returns C for complete+0.25 and
    /// does not go through ApplyUpdate. The closed first-play pin does
    /// not name the store: deleting the <c>GRADE(110)</c> arm still
    /// leaves goodie 1 at <c>GS_UNKNOWN</c>. Cold-slice latch tests
    /// stay at 5 because they do not seed leftover complete-110. Do
    /// not invent a world-110 FillOut. The leftover
    /// <c>COMPLETE_LEVEL(110)</c> store is not named here. Mutation:
    /// skip <c>SET_GOODIE_NEW(1)</c> when <c>GRADE(110) &gt;= C</c>.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateLeftoverWorld110CompleteCUnlocksTheWorld110CGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.25f;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, next.Complete);
        Assert.Equal(0.25f, next.Ranking);
        Assert.Equal(
            (byte)'C',
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>COMPLETE_LEVEL(110)</c> (<c>Career.cpp:704</c>) stays closed
    /// after a Level 100 C-grade Won. World 110 is in the cold slice
    /// and ReCalcLinks unlocks it, but it stays incomplete, so goodie
    /// 14 stays <c>GS_UNKNOWN</c>. Existing GRADE(110) tests name
    /// goodie 1 and do not name goodie 14. Leftover complete-110 plus
    /// ranking 0.0f (already pinned as E) now names the open store.
    /// Mutation: store <c>GS_NEW</c> on goodie 14 after
    /// the CountGoodies add. Do not invent the rest of the table.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateLeavesTheWorld110CompleteGoodieUnknown()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Leftover world-110 complete + E (ranking 0.0f already pinned)
    /// opens <c>Career.cpp:704</c> <c>SET_GOODIE_NEW(14)</c>. Isolated
    /// <c>CompleteFlagOf</c> / the closed first-play pin do not go
    /// through leftover ApplyUpdate: deleting the
    /// <c>COMPLETE_LEVEL(110)</c> arm still leaves goodie 14 at
    /// <c>GS_UNKNOWN</c> on first-play. The leftover GRADE(110) C test
    /// already seeds leftover complete-110 + 0.25f and now also opens
    /// 14 in <c>Update()</c> but does not name goodie 14. Ranking 0.0f
    /// keeps <c>GRADE(110) &gt;= C</c> closed so goodie 1 stays
    /// <c>GS_UNKNOWN</c>. Cold-slice latch tests stay at 5 because they
    /// do not seed leftover complete-110. Do not invent a world-110
    /// FillOut or the rest of the table. Mutation: skip
    /// <c>SET_GOODIE_NEW(14)</c> when <c>COMPLETE_LEVEL(110)</c>.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateLeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.0f;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, next.Complete);
        Assert.Equal(0.0f, next.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CountGoodies</c> iterates every slot
    /// (<c>Career.cpp:670-680</c>). Leftover world-110 complete + E
    /// (ranking 0.0f already pinned) writes <c>GS_NEW</c> on goodie 14
    /// while a C-grade Level 100 Won writes 0 / 8 / 78, so the count is
    /// 4. Isolated leftover 14 names the write of 14, not the count.
    /// Isolated leftover Old / Instructions counts seed only the five
    /// first-play S slots. Isolated C-grade does not name
    /// <c>NewGoodieCount</c> (would be 3). Isolated S latch is 5
    /// without leftover 14. Mutation: count only the five first-play S
    /// slots. Skipping <c>SET_GOODIE_NEW(14)</c> is not unique versus
    /// leftover 14. Do not invent a world-110 FillOut or the rest of
    /// the table. <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_CountGoodiesCountsLeftoverWorld110CompleteGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.0f;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        Assert.Equal(0, RetailCareerUpdateGoodieStates.CountGoodies(career));

        career.ApplyUpdate(snapshot);

        Assert.Equal(4, RetailCareerUpdateGoodieStates.CountGoodies(career));
        Assert.Equal(4, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CountGoodies</c> iterates every slot
    /// (<c>Career.cpp:670-680</c>). Leftover world-110 complete + C
    /// (ranking 0.25f already pinned) writes <c>GS_NEW</c> on goodie 1,
    /// leftover 14, and leftover 79, while a C-grade Level 100 Won
    /// writes 0 / 8 / 78, so the count is 6. Isolated leftover C
    /// names the write of 1, not the count. Isolated leftover C
    /// concept-art names the write of 79, not this count. Isolated
    /// leftover 14 CountGoodies uses ranking 0.0f so the count is 4.
    /// Isolated leftover Old / Instructions counts seed only the five
    /// first-play S slots. Isolated C-grade does not name
    /// <c>NewGoodieCount</c> (would be 3). Isolated S latch is 5
    /// without leftover 1 / 14 / 79. Mutation: skip counting goodie
    /// 1. Skipping <c>SET_GOODIE_NEW(1)</c> is not unique versus
    /// leftover C. Skipping <c>SET_GOODIE_NEW(14)</c> is not unique
    /// versus leftover 14. Skipping <c>SET_GOODIE_NEW(79)</c> is not
    /// unique versus leftover C concept-art. Counting only the five
    /// first-play S slots is not unique versus leftover 14
    /// CountGoodies. Do not invent a world-110 FillOut or the rest of
    /// the table. <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_CountGoodiesCountsLeftoverWorld110GradeCGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.25f;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        Assert.Equal(0, RetailCareerUpdateGoodieStates.CountGoodies(career));

        career.ApplyUpdate(snapshot);

        Assert.Equal(6, RetailCareerUpdateGoodieStates.CountGoodies(career));
        Assert.Equal(6, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Leftover world-110 complete + C (ranking 0.25f already pinned)
    /// opens <c>Career.cpp:770</c> <c>SET_GOODIE_NEW(79)</c>. Isolated
    /// leftover C names goodie 1, not 79. FrontEndHandoff
    /// leftover of this seed now names 79 through <c>TryApply</c>.
    /// Isolated leftover C CountGoodies is now 6 because this store
    /// is also New; that count is not unique versus skip
    /// <c>SET_GOODIE_NEW(79)</c>.
    /// Isolated leftover 14 CountGoodies uses ranking 0.0f so
    /// <c>GRADE(110) &gt;= C</c> stays closed. Isolated first-play
    /// closed GRADE(110) names 1; isolated first-play closed
    /// concept-art names 79 at <c>GS_UNKNOWN</c>. World 110 is
    /// in the cold slice, so this is not a missing-world NULL deref.
    /// Do not invent <c>GRADE(110) &gt;= B</c> / <c>A</c> or a
    /// world-110 FillOut. Mutation: skip <c>SET_GOODIE_NEW(79)</c>
    /// when <c>GRADE(110) &gt;= C</c>. Skipping
    /// <c>SET_GOODIE_NEW(1)</c> is not unique versus leftover C.
    /// <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateLeftoverWorld110CompleteCUnlocksTheWorld110CConceptArtGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailCareerNode next = career.Nodes.Find(110)!;
        next.Complete = 1;
        next.Ranking = 0.25f;
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.25f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, next.Complete);
        Assert.Equal(0.25f, next.Ranking);
        Assert.Equal(
            (byte)'C',
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        career.Nodes.Find(100)!.Complete,
                        career.Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(next.WorldNumber, next.Complete, next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCConceptArtOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(100) &gt;= B</c> is not <c>&gt;= A</c>. Ranking 0.5f is
    /// already pinned as B, so a Level 100 win at that ranking unlocks
    /// 0, 8, 78, and 121 and leaves 164 at <c>GS_UNKNOWN</c>. Cite
    /// <c>0x0041ea4f</c> / <c>0x0041f70e</c>. Mutation: unlocking 164
    /// on B writes 2 into 164. Iceberg store-0 and first-play elapsed
    /// stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeBUnlocksOnlyThroughTheBTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.5f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(
            (byte)'B',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(100) &gt;= A</c> at ranking 0.75f (already pinned as A)
    /// unlocks 164. Cite <c>0x0041f70e</c>. Mutation: skip the A arm
    /// leaves 164 at <c>GS_UNKNOWN</c>. Iceberg store-0 and first-play
    /// elapsed stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeAUnlocksTheATrainingGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.75f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(
            (byte)'A',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Ranking 0.0f is already pinned as <c>'E'</c> (<c>fcomp 0</c> /
    /// <c>test ah,0x41</c> at <c>0x0042148c</c>; store <c>0x00421499</c>
    /// <c>mov al,0x45</c>). A zero-score FillOut still Wins, so
    /// <c>COMPLETE_LEVEL(100)</c> writes 0 and 8, but <c>GRADE(100) &gt;= C</c>
    /// stays closed. Cite <c>0x00421499</c> / <c>0x0041de68</c>. Mutation:
    /// unlocking 78 on any complete writes 2 into 78. Iceberg store-0 and
    /// first-play elapsed stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeEUnlocksOnlyTheCompleteTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.0f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.Equal(0.0f, career.Nodes.Find(100)!.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Ranking 0.001f is already pinned as the score-time replacement
    /// after an exact-D scaled score (<c>0x3a83126f</c>). That is
    /// <c>'D'</c> (<c>'D' - floor(0.001*4)</c>), so
    /// <c>GRADE(100) &gt;= C</c> stays closed. Mutation: treating any
    /// ranking above 0 as C writes 2 into 78. Iceberg store-0 and
    /// first-play elapsed stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateGradeDUnlocksOnlyTheCompleteTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(ranking: 0.001f);

        career.ApplyUpdate(snapshot);

        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.Equal(0.001f, career.Nodes.Find(100)!.Ranking);
        Assert.Equal(
            (byte)'D',
            RetailCareerGrade.GradeByteFromRanking(career.Nodes.Find(100)!.Ranking));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Career <c>UpdateThingsKilled</c> still <c>je</c>s world 100
    /// (<c>cmp eax,0x64</c> at <c>0x0041c188</c>). A non-zero FillOut
    /// kill vector — ConfirmedKill increments stay unclaimed as values —
    /// must not accumulate. Mutation: dropping the equality skip writes
    /// the first dword. Iceberg store-0 and first-play elapsed stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateDoesNotAccumulateThingsKilledForWorld100()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        int[] kills = { 1, 2, 3, 4, 5 };
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(
            thingsKilled: kills);

        career.ApplyUpdate(snapshot);

        Assert.Equal(100, snapshot.WorldFinished);
        Assert.Equal(kills, snapshot.ThingsKilled);
        Assert.Equal(new[] { 0, 0, 0, 0, 0 }, career.Counters.KilledThings);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>ReCalcLinks</c> copies FillOut <c>mBaseThingsLeft</c> onto
    /// <c>level_structure[0][3] == 110</c>
    /// (<c>Career.cpp:443-452 / 519-527</c>). First-play is 1 at
    /// 0..34 and 0 at 35..287, so Blank's all-1s on world 110 lose
    /// bits 35..287. World 100 is not the destination. Mutation:
    /// skip the copy leaves bit 35 set. Iceberg store-0 stays open.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateCopiesFillOutBaseThingsOntoWorld110()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerReCalcLinks.TrainingPrimaryBaseThingsWorldNumber,
            next.WorldNumber);
        Assert.Equal(RetailCareerNode.BaseThingsExistsSize, snapshot.BaseThingsLeft.Count);
        Assert.Equal(1, next.DoesBaseThingExist(0));
        Assert.Equal(1, next.DoesBaseThingExist(34));
        Assert.Equal(0, next.DoesBaseThingExist(35));
        Assert.Equal(0, next.DoesBaseThingExist(287));
        Assert.Equal(
            RetailFillOutEndLevelData.Level100BaseWorldThingCount,
            CountExistingBaseThings(next));
        Assert.Equal(
            RetailCareerNode.BaseThingsExistsSize,
            CountExistingBaseThings(training));
        Assert.Equal(0, next.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// After the already-pinned first-play S unlocks, <c>CountGoodies</c>
    /// (<c>Career.cpp:670-680</c>) rises by five (<c>state &gt;= GS_NEW</c>).
    /// <c>UpdateGoodieStates</c> then adds that delta to the
    /// <c>new_goodie_count</c> global at <c>0x00662B20</c>
    /// (<c>Career.cpp:686 / 895-897</c>) and latches <c>first_goodie</c>
    /// because goodie 0 transitioned off <c>GOODIE_NOT_DONE</c>
    /// (<c>Career.cpp:688 / 899-900</c>). Mutation: leave the globals at
    /// ctor 0. <c>mPendingExtraGoodies</c> and episode instruction marks
    /// stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateAddsFiveNewGoodiesAndLatchesFirstGoodie()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);

        career.ApplyUpdate(snapshot);

        Assert.Equal(5, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Counters.FirstGoodie);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// A second <c>ApplyUpdate</c> of the same first-play S does not
    /// raise <c>CountGoodies</c>: <c>SET_GOODIE_NEW</c> stores only when
    /// <c>GOODIE_NOT_DONE</c> (<c>Career.cpp:564-566</c>), so the five
    /// already-<c>GS_NEW</c> slots stay put. The
    /// <c>new_goodie_count</c> add is therefore delta 0
    /// (<c>Career.cpp:895-897</c>) and <c>first_goodie</c> stays latched
    /// because goodie 0 is no longer <c>GOODIE_NOT_DONE</c>
    /// (<c>Career.cpp:688 / 899-900</c>). Mutation: add
    /// <c>CountGoodies</c> without subtracting the previous total.
    /// <c>mPendingExtraGoodies</c> and episode instruction marks stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateReplayDoesNotAddTheSameFirstPlayGoodiesAgain()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);
        Assert.Equal(5, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Counters.FirstGoodie);

        career.ApplyUpdate(snapshot);

        Assert.Equal(5, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Counters.FirstGoodie);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>SET_GOODIE_NEW</c> stores only when <c>GOODIE_NOT_DONE</c>
    /// (<c>Career.cpp:564-566</c>). Seeding the five first-play S slots
    /// as <c>GS_OLD</c> therefore leaves them at 3: an unconditional
    /// <c>GS_NEW</c> store would write 2. Replay of already-<c>GS_NEW</c>
    /// does not uniquely prove this, because <c>CountGoodies</c> still
    /// reads 5 either way. Mutation: store <c>GS_NEW</c> even when
    /// <c>mState &gt; GS_INSTRUCTIONS</c>. <c>mPendingExtraGoodies</c>
    /// and episode instruction marks stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateDoesNotOverwriteAlreadyOldTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Bio,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Second,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeCOnWorld100,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeBOnWorld100,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeAOnWorld100,
            RetailCareerGoodieState.Old);
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);

        career.ApplyUpdate(snapshot);

        Assert.Equal(
            RetailCareerGoodieState.Old,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CountGoodies</c> counts <c>mState &gt;= GS_NEW</c>
    /// (<c>Career.cpp:670-680</c>). Seeding the five first-play S slots
    /// as <c>GS_OLD</c> therefore reads 5 before ApplyUpdate. Isolated
    /// leftover <c>GS_OLD</c> <c>NewGoodieCount=0</c> does not uniquely
    /// prove this: the delta is 0 whether <c>previouslyNew</c> is 0 or
    /// 5. The latch names first-play Unknown-to-New. Replay names
    /// already-<c>GS_NEW</c>. Isolated leftover <c>GS_INSTRUCTIONS</c>
    /// store names the write to 2, not the count of leftover Old.
    /// Isolated leftover Instructions count names the skip of
    /// state 1. Mutation: count only <c>== GS_NEW</c>. Storing
    /// <c>GS_NEW</c>
    /// even when <c>mState &gt; GS_INSTRUCTIONS</c> is not unique
    /// versus
    /// <c>Level100Won_ApplyUpdateDoesNotOverwriteAlreadyOldTrainingGoodies</c>.
    /// <c>mPendingExtraGoodies</c> and episode instruction marks stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_CountGoodiesCountsLeftoverOldTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Bio,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Second,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeCOnWorld100,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeBOnWorld100,
            RetailCareerGoodieState.Old);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeAOnWorld100,
            RetailCareerGoodieState.Old);
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(5, RetailCareerUpdateGoodieStates.CountGoodies(career));

        career.ApplyUpdate(snapshot);

        Assert.Equal(5, RetailCareerUpdateGoodieStates.CountGoodies(career));
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CountGoodies</c> counts <c>mState &gt;= GS_NEW</c>
    /// (<c>Career.cpp:670-680</c>). Seeding the five first-play S slots
    /// as <c>GS_INSTRUCTIONS</c> therefore reads 0 before ApplyUpdate.
    /// Isolated leftover <c>GS_INSTRUCTIONS</c> store names the write
    /// to 2, not the pre-count. Isolated leftover Old count expects 5.
    /// FrontEndHandoff leftover Instructions names the store through
    /// <c>TryApply</c>. Mutation: count <c>mState &gt;= GS_INSTRUCTIONS</c>.
    /// Counting only <c>== GS_NEW</c> still reads 0 on this seed.
    /// Do not invent <c>SET_GOODIE_INSTRUCTION</c> or episode
    /// instruction marks — leftover seed only.
    /// <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_CountGoodiesDoesNotCountLeftoverInstructionTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Bio,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Second,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeCOnWorld100,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeBOnWorld100,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeAOnWorld100,
            RetailCareerGoodieState.Instructions);
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(0, RetailCareerUpdateGoodieStates.CountGoodies(career));

        career.ApplyUpdate(snapshot);

        Assert.Equal(5, RetailCareerUpdateGoodieStates.CountGoodies(career));
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>SET_GOODIE_NEW</c> stores when <c>mState &lt;= GS_INSTRUCTIONS</c>
    /// (<c>Career.cpp:564-566</c>). Seeding the five first-play S slots
    /// as <c>GS_INSTRUCTIONS</c> therefore writes 2. Isolated
    /// leftover <c>GS_OLD</c> and FrontEndHandoff leftover
    /// <c>GS_OLD</c> name the skip for state 3. FrontEndHandoff S
    /// starts at <c>GS_UNKNOWN</c>. Replay names already-<c>GS_NEW</c>.
    /// Mutation: skip <c>SET_GOODIE_NEW</c> when
    /// <c>mState == GS_INSTRUCTIONS</c>. Skipping
    /// <c>SET_GOODIE_NEW</c> when <c>mState &gt; GS_UNKNOWN</c> still
    /// lets first-play S through and leaves isolated <c>GS_OLD</c>
    /// at 3. Storing <c>GS_NEW</c> even when
    /// <c>mState &gt; GS_INSTRUCTIONS</c> is not unique versus
    /// <c>Level100Won_ApplyUpdateDoesNotOverwriteAlreadyOldTrainingGoodies</c>.
    /// Do not invent <c>SET_GOODIE_INSTRUCTION</c> or episode
    /// instruction marks — leftover seed only.
    /// <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateStoresNewOverInstructionTrainingGoodies()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Bio,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.CompleteWorld100Second,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeCOnWorld100,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeBOnWorld100,
            RetailCareerGoodieState.Instructions);
        career.Goodies.Set(
            RetailCareerUpdateGoodieStates.GradeAOnWorld100,
            RetailCareerGoodieState.Instructions);
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);

        career.ApplyUpdate(snapshot);

        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(5, career.Counters.NewGoodieCount);
        Assert.Equal(1, career.Counters.FirstGoodie);
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GetAndResetGoodieNewCount</c> / <c>GetAndResetFirstGoodie</c>
    /// (<c>Career.cpp:1411-1424</c>) consume the already-pinned first-play
    /// latch. A second <c>ApplyUpdate</c> then leaves both globals at 0:
    /// <c>CountGoodies</c> delta is 0 (<c>Career.cpp:895-897</c>) and
    /// <c>first_goodie</c> is transition-only — goodie 0 is no longer
    /// <c>GOODIE_NOT_DONE</c> at entry (<c>Career.cpp:688 / 899-900</c>).
    /// Replay without reset does not uniquely prove this: it leaves the
    /// latch at 1 either way. Mutation: re-arm <c>first_goodie</c> whenever
    /// goodie 0 is currently <c>GS_NEW</c>. <c>mPendingExtraGoodies</c>
    /// and episode instruction marks stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void Level100Won_ApplyUpdateReplayAfterGetAndResetLeavesGoodieLatchesClear()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();

        career.ApplyUpdate(snapshot);
        Assert.Equal(5, career.Counters.GetAndResetGoodieNewCount());
        Assert.Equal(1, career.Counters.GetAndResetFirstGoodie());
        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);

        career.ApplyUpdate(snapshot);

        Assert.Equal(0, career.Counters.NewGoodieCount);
        Assert.Equal(0, career.Counters.FirstGoodie);
        Assert.Equal(
            RetailCareerGoodieState.New,
            career.Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(1, career.Nodes.Find(100)!.Complete);
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    private static int CountExistingBaseThings(RetailCareerNode node)
    {
        int count = 0;
        for (int offset = 0; offset < RetailCareerNode.BaseThingsExistsSize; offset++)
        {
            count += node.DoesBaseThingExist(offset);
        }

        return count;
    }
}
