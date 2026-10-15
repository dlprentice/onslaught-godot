// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the Level 100 Won consumer of
/// <c>END_LEVEL_DATA.mSlots</c> inside <c>CCareer::Update</c>.
/// Source <c>Career.cpp:392</c>; retail identity the 32-dword
/// assignment at <c>0x0041BD27</c>. FillOut's copy into
/// <c>END_LEVEL_DATA</c> and the mission-path wire are separate
/// owners; this suite only pins the Won overwrite.
/// </summary>
public sealed class RetailCareerSlotHandoffTests
{
    [Fact]
    public void Constants_MatchTheShippedTutorialSlotsAndTheThirtyTwoWordCopy()
    {
        Assert.Equal(63, RetailCareerSlotHandoff.TutorialIntroductionSlot);
        Assert.Equal(64, RetailCareerSlotHandoff.TutorialPulseCannonSlot);
        Assert.Equal(65, RetailCareerSlotHandoff.TutorialVulcanCannonSlot);
        Assert.Equal(66, RetailCareerSlotHandoff.TutorialStatusBarsSlot);
        Assert.Equal(32, RetailCareerSlotHandoff.SlotWordCount);
        Assert.Equal(0x00672E44u, RetailCareerSlotHandoff.EndLevelSlotsAddress);
        Assert.Equal(0x2408, RetailCareerSlotHandoff.CareerSlotsOffset);
    }

    /// <summary>
    /// <c>0x0041BD06</c> is <c>cmp eax, 5</c> / <c>jne</c> over the
    /// copy. Lost is 4, so it never stores. Mutation: treating any
    /// terminal state as a copy would also overwrite on Lost.
    /// </summary>
    [Theory]
    [InlineData(5, true)]
    [InlineData(4, false)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void OnlyGameStateLevelWon_CopiesTheSlotWords(int finalState, bool expected)
    {
        Assert.Equal(
            expected,
            RetailCareerSlotHandoff.ShouldOverwriteFromEndLevel(finalState));
        Assert.Equal(5, RetailCareerReCalcLinks.GameStateLevelWon);
        Assert.Equal(4, RetailCareerReCalcLinks.GameStateLevelLost);
    }

    /// <summary>
    /// After a cold-career Level 100 win the four
    /// <c>SLOT_TUTORIAL_*</c> bits FillOut copied from
    /// <c>GAME.mSlots</c> replace the career array. The store is
    /// assignment, not OR: a leftover bit from an earlier session
    /// is cleared. Mutation: OR-ing the words leaves slot 1 set.
    /// </summary>
    [Fact]
    public void Level100Won_OverwritesCareerSlotsWithTheFourTutorialBits()
    {
        var career = new RetailCareerSlots();
        career.SetSlot(1, 1);

        int[] endLevel = EmptySlotWords();
        SetTutorialBits(endLevel);

        Assert.True(RetailCareerSlotHandoff.ShouldOverwriteFromEndLevel(
            RetailCareerReCalcLinks.GameStateLevelWon));
        RetailCareerSlotHandoff.OverwriteFromEndLevel(career, endLevel);

        Assert.Equal(0, career.GetSlot(1));
        Assert.Equal(1, career.GetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot));
        Assert.Equal(1, career.GetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot));
        Assert.Equal(1, career.GetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot));
        Assert.Equal(1, career.GetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot));
        Assert.Equal(endLevel, career.Words);
    }

    /// <summary>
    /// The loop counter at <c>0x0041BD37</c> is <c>mov edx, 0x20</c>.
    /// Words 8..31 are unreachable through <c>SetSlot</c> but the
    /// assignment still copies them. Mutation: copying only the eight
    /// addressable words leaves the tail behind.
    /// </summary>
    [Fact]
    public void Overwrite_CopiesAllThirtyTwoWordsIncludingTheUnreachableTail()
    {
        var career = new RetailCareerSlots();
        int[] endLevel = EmptySlotWords();
        SetTutorialBits(endLevel);
        endLevel[8] = unchecked((int)0x12345678);

        RetailCareerSlotHandoff.OverwriteFromEndLevel(career, endLevel);

        Assert.Equal(unchecked((int)0x12345678), career.Words[8]);
        Assert.Equal(endLevel, career.Words);
        Assert.Equal(0, career.GetSlot(256));
    }

    private static int[] EmptySlotWords() =>
        new int[RetailCareerSlots.SlotWords];

    private static void SetTutorialBits(int[] words)
    {
        var scratch = new RetailCareerSlots();
        scratch.SetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot, 1);
        scratch.SetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot, 1);
        scratch.SetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot, 1);
        scratch.SetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot, 1);
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = scratch.Words[index];
        }
    }
}
