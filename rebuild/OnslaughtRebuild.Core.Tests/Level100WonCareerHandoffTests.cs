// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Pins the mission-path wire from Level 100
/// <see cref="Level100MissionTerminalState.FrontEndHandoffReady"/> after Won
/// through the already-pinned
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/> snapshot into
/// <see cref="RetailCareerCampaign.ApplyUpdate"/>. FillOut and ReCalcLinks
/// stay as they are; this suite only proves the mission reaches them.
/// </summary>
public sealed class Level100WonCareerHandoffTests
{
    /// <summary>
    /// The eleven named events the released side scripts post, in first-play
    /// order. Copied from
    /// <c>Level100MissionTests.ReleasedLevelScript_RunsTheCompleteFirstPlayTutorialToLevelWon</c>
    /// so this test does not invent a shorter script path.
    /// </summary>
    private static readonly string[] ReleasedFirstPlayEvents =
    [
        "Reached Target Zone 1",
        "Reached Firing Range",
        "Static Target Destroyed", "Static Target Destroyed",
        "Static Target Destroyed", "Static Target Destroyed",
        "Static Target 2 Destroyed", "Static Target 2 Destroyed",
        "Static Target 2 Destroyed",
        "Moving Target Destroyed", "Moving Target Destroyed",
        "Moving Target Destroyed", "Moving Target Destroyed",
        "Moving Target Destroyed", "Moving Target Destroyed",
        "Reached Target Zone 2",
        "Airborne Target 1 Destroyed", "Airborne Target 1 Destroyed",
        "Airborne Target 1 Destroyed",
        "Reached Target Zone 3",
        "Airborne Target 2 Destroyed", "Airborne Target 2 Destroyed",
        "Airborne Target 2 Destroyed", "Airborne Target 2 Destroyed",
        "Airborne Target 2 Destroyed", "Airborne Target 2 Destroyed",
        "Reached Target Zone 4",
    ];

    /// <summary>
    /// After the released first-play script reaches
    /// <c>FrontEndHandoffReady</c>, career must already hold the FillOut Won
    /// snapshot: world 100 complete, lower link to 110 complete, dummy higher
    /// still <c>CN_NOT_COMPLETE</c>. Mutation: reaching the handoff without
    /// calling <c>ApplyUpdate(ForLevel100Won())</c> leaves 110 locked.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_AppliesFillOutAndUnlocksWorld110()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerCampaign career = CareerOf(mission);
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
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoffReady applies the already-pinned FillOut 1.0f
    /// ranking only onto world 100 (grade S). World 110 stays
    /// <c>BlankRanking</c> / grade E. Mutation: stamping 1.0 onto the
    /// unlocked child after <c>TryApply</c> fails the 110 equality.
    /// Score-time, base-things, kills, and goodies stay unclaimed.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_StoresFillOutRankingOnlyOnWorld100()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode training = CareerOf(mission).Nodes.Find(100)!;
        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(1.0f, training.Ranking);
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(training.Ranking));
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailCareerGrade.GradeByteFromRanking(next.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// First-play <c>SetSlotSave</c> writes <c>SLOT_TUTORIAL_1..4</c>
    /// (63..66). FillOut copies those 32 words and <c>ApplyUpdate</c>
    /// assigns them over career <c>mSlots</c>, so a leftover bit dies.
    /// Mutation: <c>ForLevel100Won</c> carrying empty slot words leaves
    /// 63..66 unset after the handoff. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_OverwritesCareerSlotsFromFillOutTutorialBits()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(
            career => career.Slots.SetSlot(1, 1));

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won();
        Assert.Equal(
            snapshot.SlotWords,
            CareerOf(mission).Slots.Words);
        Assert.Equal(0, CareerOf(mission).Slots.GetSlot(1));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.All(snapshot.SecondaryStatuses, status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoffReady after the released first-play script applies
    /// the already-pinned FillOut 1.0f / S, so <c>UpdateGoodieStates</c>
    /// writes <c>GS_NEW</c> on 0, 8, 78, 121, and 164. Mutation: skipping
    /// <c>ApplyUpdate</c> on the handoff leaves those five at
    /// <c>GS_UNKNOWN</c>. Score-time, base-things, and kill totals stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_UnlocksTrainingGoodiesForAnS()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(CareerOf(mission).Nodes.Find(100)!.Ranking));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoffReady after the released first-play script applies
    /// the already-pinned first-play S, so <c>CountGoodies</c> rises by
    /// five (<c>Career.cpp:670-680</c>). <c>new_goodie_count</c> at
    /// <c>0x00662B20</c> adds that delta (<c>Career.cpp:895-897</c>) and
    /// <c>first_goodie</c> latches because goodie 0 left
    /// <c>GOODIE_NOT_DONE</c> (<c>Career.cpp:688 / 899-900</c>). Isolated
    /// <c>Level100Won_ApplyUpdateAddsFiveNewGoodiesAndLatchesFirstGoodie</c>
    /// does not go through <c>TryApply</c>. The already-pinned
    /// FrontEndHandoff goodie-state test does not name these two
    /// globals. Mutation: clear both after <c>TryApply</c>.
    /// <c>mPendingExtraGoodies</c> and episode instruction marks stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_AddsFiveNewGoodiesAndLatchesFirstGoodie()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);
        Assert.Equal(5, CareerOf(mission).Counters.NewGoodieCount);
        Assert.Equal(1, CareerOf(mission).Counters.FirstGoodie);
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover world-110 complete + E (ranking 0.0f
    /// already pinned) opens <c>Career.cpp:704</c>
    /// <c>SET_GOODIE_NEW(14)</c> through <c>TryApply</c>. Isolated
    /// leftover 14 names ApplyUpdate and does not go through
    /// <c>TryApply</c>. Lost leftover 14 names ApplyUpdate's Lost
    /// return, not <c>TryApply</c>. Existing FrontEndHandoff S
    /// goodies name 0 / 8 / 78 / 121 / 164, not 14. Ranking 0.0f
    /// keeps <c>GRADE(110) &gt;= C</c> closed so goodie 1 stays
    /// <c>GS_UNKNOWN</c>. Do not invent a world-110 FillOut or the
    /// rest of the table. Mutation: clear goodie 14 after
    /// <c>TryApply</c>. Skipping <c>SET_GOODIE_NEW(14)</c> is not
    /// unique versus the isolated leftover pin. Skipping
    /// <c>UpdateGoodieStates</c> on the Lost return is not unique
    /// versus the Lost leftover pin. Skipping <c>ApplyUpdate</c> on
    /// the handoff is not unique versus existing FrontEndHandoff
    /// tests. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteEUnlocksTheWorld110CompleteGoodie()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
            RetailCareerNode next = career.Nodes.Find(110)!;
            next.Complete = 1;
            next.Ranking = 0.0f;
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode leftover = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(1, leftover.Complete);
        Assert.Equal(0.0f, leftover.Ranking);
        Assert.Equal(
            RetailCareerGrade.FailedGrade,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        CareerOf(mission).Nodes.Find(100)!.Complete,
                        CareerOf(mission).Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(
                        leftover.WorldNumber,
                        leftover.Complete,
                        leftover.Ranking),
                },
                leftover.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover world-110 complete + C (ranking 0.25f
    /// already pinned) opens <c>Career.cpp:691</c>
    /// <c>SET_GOODIE_NEW(1)</c> through <c>TryApply</c>. Isolated
    /// leftover C names ApplyUpdate and does not go through
    /// <c>TryApply</c>. FrontEndHandoff leftover 14 names 14, not 1.
    /// Existing FrontEndHandoff S goodies name 0 / 8 / 78 / 121 / 164,
    /// not 1. Do not invent a world-110 FillOut or the rest of the
    /// table. The leftover <c>COMPLETE_LEVEL(110)</c> store is not
    /// named here. Isolated leftover C concept-art names 79 through
    /// ApplyUpdate and is not named here. Mutation: clear goodie 1
    /// after <c>TryApply</c>. Skipping <c>SET_GOODIE_NEW(1)</c> is
    /// not unique versus the isolated leftover C pin. Skipping
    /// <c>ApplyUpdate</c> on the handoff is not unique versus
    /// existing FrontEndHandoff tests. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteCUnlocksTheWorld110CGoodie()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
            RetailCareerNode next = career.Nodes.Find(110)!;
            next.Complete = 1;
            next.Ranking = 0.25f;
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode leftover = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(1, leftover.Complete);
        Assert.Equal(0.25f, leftover.Ranking);
        Assert.Equal(
            (byte)'C',
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        CareerOf(mission).Nodes.Find(100)!.Complete,
                        CareerOf(mission).Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(
                        leftover.WorldNumber,
                        leftover.Complete,
                        leftover.Ranking),
                },
                leftover.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover world-110 complete + C (ranking 0.25f
    /// already pinned) opens <c>Career.cpp:770</c>
    /// <c>SET_GOODIE_NEW(79)</c> through <c>TryApply</c>. Isolated
    /// leftover C 79 names ApplyUpdate and does not go through
    /// <c>TryApply</c>. FrontEndHandoff leftover C names 1, not 79.
    /// Existing FrontEndHandoff S goodies name 0 / 8 / 78 / 121 / 164,
    /// not 79. Do not invent <c>GRADE(110) &gt;= B</c> / <c>A</c> or a
    /// world-110 FillOut. Mutation: clear goodie 79 after
    /// <c>TryApply</c>. Skipping <c>SET_GOODIE_NEW(79)</c> is not
    /// unique versus the isolated leftover C 79 pin. Skipping
    /// <c>ApplyUpdate</c> on the handoff is not unique versus existing
    /// FrontEndHandoff tests. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeftoverWorld110CompleteCUnlocksTheWorld110CConceptArtGoodie()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
            RetailCareerNode next = career.Nodes.Find(110)!;
            next.Complete = 1;
            next.Ranking = 0.25f;
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode leftover = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(1, leftover.Complete);
        Assert.Equal(0.25f, leftover.Ranking);
        Assert.Equal(
            (byte)'C',
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        CareerOf(mission).Nodes.Find(100)!.Complete,
                        CareerOf(mission).Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(
                        leftover.WorldNumber,
                        leftover.Complete,
                        leftover.Ranking),
                },
                leftover.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCConceptArtOnWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(110) &gt;= C</c> (<c>Career.cpp:691</c>) stays closed
    /// after first-play S through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateGradeCLeavesTheWorld110CGoodieUnknown</c>
    /// names ApplyUpdate and does not go through <c>TryApply</c>.
    /// Leftover C FrontEndHandoff names goodie 1 as <c>GS_NEW</c>.
    /// Existing FrontEndHandoff S goodies name 0 / 8 / 78 / 121 / 164,
    /// not 1. DriveReleasedFirstPlayToTerminal with no leftover seed
    /// leaves world 110 incomplete so <c>GRADE</c> is the already-pinned
    /// incomplete <c>'E'</c> (<c>0x0041C3FE</c>). Do not invent a
    /// world-110 FillOut or the rest of the table. Mutation: store
    /// <c>GS_NEW</c> on goodie 1 after <c>TryApply</c> when world 110
    /// is incomplete. Skipping <c>SET_GOODIE_NEW(1)</c> is not unique
    /// versus leftover C. Skipping <c>ApplyUpdate</c> on the handoff
    /// is not unique versus existing FrontEndHandoff tests. Storing
    /// <c>GS_NEW</c> on goodie 1 after every <c>TryApply</c> is not
    /// unique versus leftover-14's closed goodie-1 assert. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeavesTheWorld110CGoodieUnknown()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(
                CareerOf(mission).Nodes.Find(100)!.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailWorldGrade.IncompleteGradeByte,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        CareerOf(mission).Nodes.Find(100)!.Complete,
                        CareerOf(mission).Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(
                        next.WorldNumber,
                        next.Complete,
                        next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>GRADE(110) &gt;= C</c> (<c>Career.cpp:770</c>) stays closed
    /// after first-play S through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateGradeCLeavesTheWorld110CConceptArtGoodieUnknown</c>
    /// names ApplyUpdate and does not go through <c>TryApply</c>.
    /// Leftover C FrontEndHandoff names goodie 79 as <c>GS_NEW</c>.
    /// FrontEndHandoff closed GRADE(110) names 1, not 79. Existing
    /// FrontEndHandoff S goodies name 0 / 8 / 78 / 121 / 164, not 79.
    /// DriveReleasedFirstPlayToTerminal with no leftover seed leaves
    /// world 110 incomplete so <c>GRADE</c> is the already-pinned
    /// incomplete <c>'E'</c> (<c>0x0041C3FE</c>). Do not invent
    /// <c>GRADE(110) &gt;= B</c> / <c>A</c> or a world-110 FillOut.
    /// Mutation: store <c>GS_NEW</c> on goodie 79 after
    /// <c>TryApply</c> when world 110 is incomplete. Skipping
    /// <c>SET_GOODIE_NEW(79)</c> is not unique versus leftover C
    /// concept-art. Skipping <c>ApplyUpdate</c> on the handoff is
    /// not unique versus existing FrontEndHandoff tests. Storing
    /// <c>GS_NEW</c> on goodie 79 after every <c>TryApply</c> is not
    /// unique versus leftover C FrontEndHandoff. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeavesTheWorld110CConceptArtGoodieUnknown()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(
                CareerOf(mission).Nodes.Find(100)!.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailWorldGrade.IncompleteGradeByte,
            RetailWorldGrade.GradeByteForWorld(
                new[]
                {
                    new RetailWorldGradeNode(
                        RetailCareerReCalcLinks.TrainingWorldNumber,
                        CareerOf(mission).Nodes.Find(100)!.Complete,
                        CareerOf(mission).Nodes.Find(100)!.Ranking),
                    new RetailWorldGradeNode(
                        next.WorldNumber,
                        next.Complete,
                        next.Ranking),
                },
                next.WorldNumber));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld110));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCConceptArtOnWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>COMPLETE_LEVEL(110)</c> (<c>Career.cpp:704</c>) stays closed
    /// after first-play S through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateLeavesTheWorld110CompleteGoodieUnknown</c>
    /// names ApplyUpdate and does not go through <c>TryApply</c>.
    /// Leftover 14 FrontEndHandoff names goodie 14 as <c>GS_NEW</c>.
    /// Existing FrontEndHandoff S goodies name 0 / 8 / 78 / 121 / 164,
    /// not 14. DriveReleasedFirstPlayToTerminal with no leftover seed
    /// leaves world 110 unlocked but incomplete. Do not invent a
    /// world-110 FillOut or the rest of the table. Mutation: store
    /// <c>GS_NEW</c> on goodie 14 after <c>TryApply</c>. Skipping
    /// <c>SET_GOODIE_NEW(14)</c> is not unique versus leftover 14.
    /// Skipping <c>ApplyUpdate</c> on the handoff is not unique
    /// versus existing FrontEndHandoff tests. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_LeavesTheWorld110CompleteGoodieUnknown()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailCareerGrade.GradeByteFromRanking(
                CareerOf(mission).Nodes.Find(100)!.Ranking));
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Unknown,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld110));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover <c>mNumAttempts</c> (+0x38) survives
    /// first-play S through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateDoesNotIncrementNumAttempts</c>
    /// names leftover 7 / 11 on ApplyUpdate and does not go through
    /// <c>TryApply</c>. Isolated Blank ctor 0 does not go through
    /// <c>TryApply</c>. Existing FrontEndHandoff ranking / Complete /
    /// CareerInProgress / goodie tests do not name <c>+0x38</c>.
    /// <c>Career.cpp:379-418</c> never writes the field;
    /// <c>CCareerNode::Blank</c> at <c>Career.cpp:99</c> /
    /// <c>0x0041B740</c> is the only store. Mutation: increment the
    /// finished node after <c>TryApply</c>. Incrementing inside
    /// ApplyUpdate is not unique versus the isolated pin. Skipping
    /// <c>ApplyUpdate</c> on the handoff is not unique versus
    /// existing FrontEndHandoff tests. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_DoesNotIncrementLeftoverNumAttempts()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
            career.Nodes.Find(100)!.NumAttempts = 7;
            career.Nodes.Find(110)!.NumAttempts = 11;
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode training = CareerOf(mission).Nodes.Find(100)!;
        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(1, training.Complete);
        Assert.Equal(1.0f, training.Ranking);
        Assert.Equal(7, training.NumAttempts);
        Assert.Equal(0, next.Complete);
        Assert.Equal(RetailCareerNode.BlankRanking, next.Ranking);
        Assert.Equal(11, next.NumAttempts);
        Assert.Equal(1, CareerOf(mission).CareerInProgress);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover <c>GS_OLD</c> on the five first-play S
    /// slots survives first-play S through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateDoesNotOverwriteAlreadyOldTrainingGoodies</c>
    /// names <c>SET_GOODIE_NEW</c> does not overwrite <c>GS_OLD</c> on
    /// ApplyUpdate and does not go through <c>TryApply</c>. Existing
    /// FrontEndHandoff S goodies start <c>GS_UNKNOWN</c> and name them
    /// as New. Replay CountGoodies names already-<c>GS_NEW</c> and
    /// does not uniquely prove the <c>GS_OLD</c> store skip.
    /// <c>SET_GOODIE_NEW</c> stores only when <c>GOODIE_NOT_DONE</c>
    /// (<c>Career.cpp:564-566</c>). <c>new_goodie_count</c> /
    /// <c>first_goodie</c> stay ctor 0 because CountGoodies does not
    /// rise and goodie 0 was already done. Mutation: store
    /// <c>GS_NEW</c> on the five slots after <c>TryApply</c> when they
    /// were <c>GS_OLD</c>. Storing <c>GS_NEW</c> even when
    /// <c>mState &gt; GS_INSTRUCTIONS</c> is not unique versus the
    /// isolated pin. Skipping <c>ApplyUpdate</c> on the handoff is
    /// not unique versus existing FrontEndHandoff tests (seeded
    /// leftover stays 3 either way).
    /// <c>mPendingExtraGoodies</c> and episode instruction marks stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_DoesNotOverwriteAlreadyOldTrainingGoodies()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
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
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        Assert.Equal(
            RetailCareerGoodieState.Old,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.Old,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(0, CareerOf(mission).Counters.NewGoodieCount);
        Assert.Equal(0, CareerOf(mission).Counters.FirstGoodie);
        Assert.Equal(1, CareerOf(mission).Nodes.Find(100)!.Complete);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoff leftover <c>GS_INSTRUCTIONS</c> on the five
    /// first-play S slots writes 2 through <c>TryApply</c>. Isolated
    /// <c>Level100Won_ApplyUpdateStoresNewOverInstructionTrainingGoodies</c>
    /// names the store on ApplyUpdate and does not go through
    /// <c>TryApply</c>. Isolated leftover <c>GS_OLD</c> and
    /// FrontEndHandoff leftover <c>GS_OLD</c> name the skip for
    /// state 3. FrontEndHandoff S starts at <c>GS_UNKNOWN</c>.
    /// Replay CountGoodies names already-<c>GS_NEW</c>.
    /// <c>SET_GOODIE_NEW</c> stores when
    /// <c>mState &lt;= GS_INSTRUCTIONS</c>
    /// (<c>Career.cpp:564-566</c>). Mutation: restore the five
    /// slots to <c>GS_INSTRUCTIONS</c> after <c>TryApply</c> when
    /// they were <c>GS_INSTRUCTIONS</c>. Skipping
    /// <c>SET_GOODIE_NEW</c> when
    /// <c>mState == GS_INSTRUCTIONS</c> is not unique versus the
    /// isolated pin. Skipping <c>ApplyUpdate</c> on the handoff is
    /// not unique versus existing FrontEndHandoff tests. Do not
    /// invent <c>SET_GOODIE_INSTRUCTION</c> or episode instruction
    /// marks — leftover seed only.
    /// <c>mPendingExtraGoodies</c> stays unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_StoresNewOverInstructionTrainingGoodies()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal(career =>
        {
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
        });

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Bio));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.CompleteWorld100Second));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeCOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeBOnWorld100));
        Assert.Equal(
            RetailCareerGoodieState.New,
            CareerOf(mission).Goodies.Get(RetailCareerUpdateGoodieStates.GradeAOnWorld100));
        Assert.Equal(5, CareerOf(mission).Counters.NewGoodieCount);
        Assert.Equal(1, CareerOf(mission).Counters.FirstGoodie);
        Assert.Equal(1, CareerOf(mission).Nodes.Find(100)!.Complete);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// FrontEndHandoffReady applies the already-pinned
    /// <c>UpdateBaseWorldExistsStuffForNode</c> copy onto world 110.
    /// First-play zeros at 35..287 clear Blank's all-1s there; world
    /// 100 stays Blank. Mutation: skipping <c>ApplyUpdate</c> on the
    /// handoff leaves bit 35 set. Iceberg store-0 stays open. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_CopiesFillOutBaseThingsOntoWorld110()
    {
        Level100Mission mission = DriveReleasedFirstPlayToTerminal();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode training = CareerOf(mission).Nodes.Find(100)!;
        RetailCareerNode next = CareerOf(mission).Nodes.Find(110)!;
        Assert.Equal(
            RetailCareerReCalcLinks.TrainingPrimaryBaseThingsWorldNumber,
            next.WorldNumber);
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
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CGame::RestartLoopRunLevel</c> calls FillOut only after the
    /// main loop quits (<c>game.cpp:1552</c>), and that quit waits for
    /// the already-pinned 5.0 f Won store (<c>game.cpp:1997-2004</c>).
    /// <c>CFrontEnd::Init</c> then calls <c>CAREER.Update</c>
    /// (<c>FrontEnd.cpp:67</c>). Mutation: letting
    /// <see cref="Level100WonCareerHandoff.TryApply"/> accept
    /// <c>SuccessCountdown</c> unlocks world 110 before the overlay
    /// elapses. Score-time, base-things, kills, and goodies stay
    /// unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void SuccessCountdownDoesNotApplyFillOutEvenIfWonIsClaimed()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        Assert.Null(Level100WonCareerHandoff.TryApply(
            career,
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.SuccessCountdown));

        AssertCareerStillCold(career);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// First-play <c>LevelWon</c> starts the already-pinned 5.0 f
    /// overlay and leaves the cold career locked until those ticks
    /// elapse. Mutation: applying FillOut from <c>DeclareWon</c>
    /// unlocks 110 while <c>SuccessCountdown</c> is still running.
    /// </summary>
    [Fact]
    public void FrontEndHandoffReadyAfterWon_WaitsForTheFiveSecondCountdown()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.SuccessCountdown,
            mission.Snapshot.TerminalState);
        Assert.Equal(
            RetailGameEndCountdown.WonTicks,
            mission.Snapshot.TerminalTicksRemaining);
        AssertCareerStillCold(CareerOf(mission));

        for (int tick = 0; tick < RetailGameEndCountdown.WonTicks; tick++)
        {
            mission.AdvanceTick(SimulationConstants.MaximumHull);
        }

        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);
        Assert.Equal(0, mission.Snapshot.TerminalTicksRemaining);
        RetailCareerNode training = CareerOf(mission).Nodes.Find(100)!;
        RetailCareerNodeLink lower = CareerOf(mission).GetLink(training.LowerLink)!;
        Assert.Equal(1, training.Complete);
        Assert.Equal(1, CareerOf(mission).CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.Complete, lower.LinkType);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::SetSlotSave</c> at <c>0x00533900</c> calls
    /// <c>CCareer::SetSlot</c> immediately. First-play
    /// <c>SetSlotSave(SLOT_TUTORIAL_1..4, TRUE)</c> therefore writes
    /// bits 63..66 before <c>DeclareLevelWon</c> and before FillOut /
    /// <c>ApplyUpdate</c>. Isolated FrontEndHandoff overwrite names
    /// the 32-dword assignment after <c>TryApply</c> and does not
    /// uniquely prove this persist: empty FillOut slot words still
    /// leave 63..66 set here. Mutation: skip
    /// <see cref="RetailSetSlotSave.PersistCareerSlot"/> so the four
    /// bits stay 0 while <c>Complete</c> is still 0. Live
    /// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void SetSlotSave_PersistsTutorialBitsBeforeApplyUpdate()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.SuccessCountdown,
            mission.Snapshot.TerminalState);
        AssertCareerStillCold(CareerOf(mission));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(
            1,
            CareerOf(mission).Slots.GetSlot(
                RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::AddScore</c> at <c>0x005343cb</c> is
    /// <c>add [0x008a9b8c], eax</c>. First-play
    /// <c>TUTORIAL_DODGE_GOOD</c> posts one <c>AddScore(50)</c>,
    /// so <c>CGame+0xf4</c> is 50 before FillOut. Isolated
    /// <c>ScoreDelta</c> = 50 names the rebuild accumulator and
    /// still passes if this store is skipped. Isolated FillOut
    /// <see cref="RetailFillOutEndLevelData.ScoreWord"/> copies a
    /// parameterized dword and does not go through AddScore.
    /// Isolated <see cref="RetailAddScore.Add"/> names add-not-replace;
    /// one live +50 is not unique versus replace. Mutation: skip
    /// the <c>CGame+0xf4</c> store. First-play elapsed and FillOut
    /// score stay unclaimed. Live <c>GAME.mSlots</c> stay unclaimed.
    /// No new secondaries.
    /// </summary>
    [Fact]
    public void AddScore_FirstPlayWonWritesCGamePlusF4()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(50, mission.Snapshot.ScoreDelta);
        Assert.Equal(50, mission.GameScore);
        Assert.Equal(
            RetailAddScore.Add(RetailAddScore.LoadLevelZero, 50),
            mission.GameScore);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::EnableFlightMode</c> at <c>0x00535079</c>
    /// calls <c>0x0040dcb0</c>, which is
    /// <c>mov dword ptr [ecx+0x58c], 1</c>. First-play beat 6
    /// posts one Enable, so <c>CBattleEngine+0x58c</c> is 1
    /// before FillOut. Isolated
    /// <c>FlightModeEnabled</c> = true names the rebuild bool
    /// and still passes if this store is skipped. Isolated
    /// <see cref="RetailEnableFlightMode.Enable"/> names
    /// literal-1; one live store of 1 is not unique versus
    /// increment from 0. Mutation: skip the
    /// <c>+0x58c</c> store. Disable's clear / morph stay
    /// unclaimed. ChargeWeapon stays unclaimed. Live
    /// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void EnableFlightMode_FirstPlayWonWritesCBattleEnginePlus58C()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.True(mission.Snapshot.FlightModeEnabled);
        Assert.Equal(
            RetailEnableFlightMode.FlagEnabled,
            mission.FlightModeFlag);
        Assert.Equal(
            RetailEnableFlightMode.Enable(RetailEnableFlightMode.FlagDisabled),
            mission.FlightModeFlag);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::UnHighlightHudPart</c> at <c>0x00535e8b</c>
    /// stores 1 after Highlight's 2. First-play init then the
    /// dodge pair leave <c>HUD_COMPASS</c> at 1 before FillOut.
    /// Isolated last <c>Emphasized</c> = false names the rebuild
    /// bool and still passes if this store is skipped. Isolated
    /// <see cref="RetailHighlightHudPart.Unhighlight"/> names
    /// literal-1; skip UnHighlight after Highlight leaves 2.
    /// Mutation: skip the UnHighlight store. Array extent and
    /// state-1/2 HUD meaning stay unclaimed. ChargeWeapon stays
    /// unclaimed. Live <c>GAME.mSlots</c> stay unclaimed. No new
    /// secondaries.
    /// </summary>
    [Fact]
    public void UnHighlightHudPart_FirstPlayWonWritesOneAtCompass()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Equal(
            RetailHighlightHudPart.Unhighlighted,
            mission.HudPartWord(RetailHighlightHudPart.CompassIndex));
        Assert.Equal(
            RetailHighlightHudPart.Unhighlight(RetailHighlightHudPart.Highlight(0)),
            mission.HudPartWord(RetailHighlightHudPart.CompassIndex));
        Assert.NotEqual(
            RetailHighlightHudPart.Highlighted,
            mission.HudPartWord(RetailHighlightHudPart.CompassIndex));
        Assert.Contains(
            mission.Snapshot.PendingEvents.OfType<Level100HudEmphasisChanged>(),
            item => item.PartId == RetailHighlightHudPart.CompassIndex &&
                    !item.Emphasized);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::EnableWeapon</c> at <c>0x00534fce</c>
    /// calls thing vfunc <c>+0x198</c>. The Level 100 player
    /// override <c>0x0040dc30</c> forwards to walker
    /// <c>0x00414970</c>, which is
    /// <c>mov dword ptr [edi+0x9c], 1</c>. First-play
    /// <c>EnableWeapon("Mech Twin Vulcan Cannon")</c> therefore
    /// stores 1 at Twin's <c>+0x9c</c> before FillOut. Isolated
    /// last <c>Enabled</c> = true names the rebuild bool and
    /// still passes if this store is skipped. Isolated
    /// <see cref="RetailEnableWeapon.Enable"/> names
    /// literal-1; one live store of 1 is not unique versus
    /// increment from 0. Mutation: skip the <c>+0x9c</c>
    /// store. Disable's store-0 / ChangeWeapon stay
    /// unclaimed. ChargeWeapon stays unclaimed. Live
    /// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void EnableWeapon_FirstPlayWonWritesOneAtTwinVulcanPlus9C()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Contains(
            mission.Snapshot.PendingEvents.OfType<Level100WeaponAvailabilityChanged>(),
            item => item.Weapon == Level100MissionWeapon.MechTwinVulcanCannon &&
                    item.Enabled);
        Assert.Equal(
            RetailEnableWeapon.FlagEnabled,
            mission.WeaponActiveWord(Level100MissionWeapon.MechTwinVulcanCannon));
        Assert.Equal(
            RetailEnableWeapon.Enable(RetailEnableWeapon.FlagDisabled),
            mission.WeaponActiveWord(Level100MissionWeapon.MechTwinVulcanCannon));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>IScript::SetObjective</c> at <c>0x00535ed5</c> is
    /// <c>push 1 / call 0x004f3970</c>. Official
    /// <c>CThing::SetObjective</c> at <c>0x004f398e</c> is
    /// <c>or byte ptr [esi+0x2c], 0x20</c>. First-play
    /// LevelScript <c>GetThingRef("Target Zone 4")</c> then
    /// <c>SetObjective</c> therefore ORs bit 0x20 at that
    /// thing's <c>+0x2c</c> before FillOut. Isolated last
    /// navigation name / <c>IsObjective</c> = true names the
    /// rebuild string and bool and still pass if this OR is
    /// skipped. Isolated <see cref="RetailSetObjective.Mark"/>
    /// names the OR; one live store of 0x20 from 0 is not
    /// unique versus replace. Mutation: skip the
    /// <c>+0x2c</c> OR. Noticeboard Add/Remove stay
    /// unclaimed. ChargeWeapon stays unclaimed. Live
    /// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
    /// </summary>
    [Fact]
    public void SetObjective_FirstPlayWonOrsBit20AtTargetZone4Plus2C()
    {
        Level100Mission mission = DriveReleasedFirstPlayUntilWon();

        Assert.Equal(Level100MissionOutcome.Won, mission.Snapshot.Outcome);
        Assert.Contains(
            mission.Snapshot.PendingEvents.OfType<Level100NavigationObjectiveChanged>(),
            item => item.ThingName == "Target Zone 4");
        Assert.Equal(
            RetailSetObjective.MarkedBit,
            mission.ThingFlagWord("Target Zone 4"));
        Assert.Equal(
            RetailSetObjective.Mark(0),
            mission.ThingFlagWord("Target Zone 4"));
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// <c>CCareer::Update</c> at <c>0x0041BD06</c> is
    /// <c>cmp eax, 5</c> / <c>jne</c>. Lost is 4, so FillOut is never
    /// applied even if the handoff state is claimed. Mutation: dropping
    /// the Won check from <see cref="Level100WonCareerHandoff.TryApply"/>
    /// unlocks world 110 on Lost.
    /// </summary>
    [Fact]
    public void LostDoesNotApplyFillOutEvenIfFrontEndHandoffReadyIsClaimed()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        Assert.Null(Level100WonCareerHandoff.TryApply(
            career,
            Level100MissionOutcome.Lost,
            Level100MissionTerminalState.FrontEndHandoffReady));

        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        Assert.Equal(0, training.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.NotComplete, lower.LinkType);
    }

    /// <summary>
    /// Player-reproducible Lost: Broke-Tutorial reaches
    /// <c>FailureMenuReady</c>, never <c>FrontEndHandoffReady</c>, and
    /// the cold career stays locked. Mutation: calling
    /// <c>TryApply</c> from the Lost countdown unlocks 110.
    /// </summary>
    [Fact]
    public void BrokeTutorialLost_DoesNotApplyFillOutOrUnlockWorld110()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        var mission = new Level100Mission(
            actors,
            actors.GetThingRef("Player 1")!.Value);

        Assert.True(mission.SubmitInput(Level100MissionInput.BrokeTutorial));

        const int settleTicks = 100 * SimulationConstants.TicksPerSecond;
        for (int tick = 0; tick < settleTicks; tick++)
        {
            mission.AdvanceTick(SimulationConstants.MaximumHull);
            if (mission.Snapshot.TerminalState ==
                Level100MissionTerminalState.FailureMenuReady)
            {
                break;
            }
        }

        Assert.Equal(Level100MissionOutcome.Lost, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionFailureReason.TutorialBroken,
            mission.Snapshot.FailureReason);
        Assert.Equal(
            Level100MissionTerminalState.FailureMenuReady,
            mission.Snapshot.TerminalState);
        Assert.NotEqual(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.Snapshot.TerminalState);

        RetailCareerNode training = CareerOf(mission).Nodes.Find(100)!;
        RetailCareerNodeLink lower = CareerOf(mission).GetLink(training.LowerLink)!;
        Assert.Equal(0, training.Complete);
        Assert.Equal(0, CareerOf(mission).CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.NotComplete, lower.LinkType);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Level100Mission, FrontendCareer> s_careers = new();

    /// <summary>
    /// The career a mission's run reaches, followed the way the frontend
    /// follows the host's mission events: each <c>SetSlotSave</c> stored at
    /// once (<see cref="RetailSetSlotSave.PersistCareerSlot"/>), and the Won
    /// update applied once when the mission stands at its handoff
    /// (<see cref="Level100WonCareerHandoff.TryApply"/>).
    /// </summary>
    private static RetailCareerCampaign CareerOf(Level100Mission mission)
    {
        FrontendCareer frontend = s_careers.GetValue(mission, _ => new FrontendCareer());
        foreach (Level100MissionEvent missionEvent in mission.DrainEvents())
        {
            if (missionEvent is Level100TutorialSlotSaved saved)
            {
                RetailSetSlotSave.PersistCareerSlot(frontend.Career.Slots, saved.Slot, saved.Value);
            }
        }

        if (!frontend.Applied)
        {
            frontend.Applied = Level100WonCareerHandoff.TryApply(
                frontend.Career, mission.Snapshot.Outcome, mission.Snapshot.TerminalState) is not null;
        }

        return frontend.Career;
    }

    private sealed class FrontendCareer
    {
        public RetailCareerCampaign Career { get; } = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        public bool Applied { get; set; }
    }

    private static void AssertCareerStillCold(RetailCareerCampaign career)
    {
        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        Assert.Equal(0, training.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.NotComplete, lower.LinkType);
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

    private static Level100Mission DriveReleasedFirstPlayUntilWon()
    {
        Level100Mission mission = CreateFirstPlayMission();
        const int settleTicks = 100 * SimulationConstants.TicksPerSecond;

        Settle(mission, settleTicks);
        for (int index = 0; index < ReleasedFirstPlayEvents.Length; index++)
        {
            string eventName = ReleasedFirstPlayEvents[index];
            Assert.True(
                mission.QueueExternalEvent(eventName),
                $"The released LevelScript refused the event '{eventName}'.");
            bool lastEvent = index == ReleasedFirstPlayEvents.Length - 1;
            if (lastEvent)
            {
                for (int tick = 0; tick < settleTicks; tick++)
                {
                    mission.AdvanceTick(SimulationConstants.MaximumHull);
                    if (mission.Snapshot.Outcome == Level100MissionOutcome.Won)
                    {
                        return mission;
                    }
                }

                Assert.Fail("The released first-play script never declared Won.");
            }

            Settle(mission, settleTicks);
        }

        return mission;
    }

    private static Level100Mission CreateFirstPlayMission(
        Action<RetailCareerCampaign>? seedCareer = null)
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
        var mission = new Level100Mission(
            actors,
            player,
            new Level100TutorialProgress(false, false, false, false),
            initialPlayerHealth: SimulationConstants.MaximumHull);
        seedCareer?.Invoke(CareerOf(mission));
        return mission;
    }

    private static void Settle(Level100Mission mission, int settleTicks)
    {
        for (int index = 0; index < settleTicks; index++)
        {
            mission.AdvanceTick(SimulationConstants.MaximumHull);
        }
    }

    private static Level100Mission DriveReleasedFirstPlayToTerminal(
        Action<RetailCareerCampaign>? seedCareer = null)
    {
        Level100Mission mission = CreateFirstPlayMission(seedCareer);

        const int settleTicks = 100 * SimulationConstants.TicksPerSecond;
        void Settle()
        {
            for (int index = 0; index < settleTicks; index++)
            {
                mission.AdvanceTick(SimulationConstants.MaximumHull);
            }
        }

        Settle();
        foreach (string eventName in ReleasedFirstPlayEvents)
        {
            Assert.True(
                mission.QueueExternalEvent(eventName),
                $"The released LevelScript refused the event '{eventName}'.");
            Settle();
        }

        return mission;
    }
}
