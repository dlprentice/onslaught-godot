// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One <c>CGoodie::mState</c> dword — <c>Career.h:40-55</c>.
/// </summary>
public static class RetailCareerGoodieState
{
    /// <summary><c>GS_UNKNOWN</c> — <c>Career.h:42</c>.</summary>
    public const int Unknown = 0;

    /// <summary><c>GS_INSTRUCTIONS</c> — <c>Career.h:43</c>.</summary>
    public const int Instructions = 1;

    /// <summary><c>GS_NEW</c> — <c>Career.h:44</c>; the store at <c>0x0041c527</c>.</summary>
    public const int New = 2;

    /// <summary><c>GS_OLD</c> — <c>Career.h:45</c>.</summary>
    public const int Old = 3;
}

/// <summary>
/// <c>mGoodies[300]</c> — the dword array at <c>CCareer + 0x1F44</c>.
/// </summary>
public sealed class RetailCareerGoodies
{
    private readonly int[] _states = new int[RetailCareerRecordLayout.GoodieCount];

    /// <summary>The 300 state dwords in index order.</summary>
    public IReadOnlyList<int> States => _states;

    /// <summary><c>GetGoodieState</c> — index into <c>mGoodies</c>.</summary>
    public int Get(int index) => _states[index];

    /// <summary>
    /// Direct store of one <c>mGoodies[i].mState</c> dword. Tests seed
    /// leftover <c>GS_OLD</c> so the overwrite skip is unique, and
    /// leftover <c>GS_INSTRUCTIONS</c> so the store-when-not-done
    /// side is unique.
    /// </summary>
    public void Set(int index, int state) => _states[index] = state;

    /// <summary>
    /// <c>SET_GOODIE_NEW</c> — <c>Career.cpp:566</c>. Stores
    /// <c>GS_NEW</c> only when <c>mState &lt;= GS_INSTRUCTIONS</c>.
    /// Leftover <c>GS_INSTRUCTIONS</c> therefore writes 2; leftover
    /// <c>GS_OLD</c> stays 3.
    /// </summary>
    public void SetNewIfNotDone(int index)
    {
        if (_states[index] <= RetailCareerGoodieState.Instructions)
        {
            _states[index] = RetailCareerGoodieState.New;
        }
    }
}

/// <summary>
/// The Level 100 slice of <c>CCareer::UpdateGoodieStates</c>.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:684-902</c>.
/// Retail identity: <c>0x0041c470</c>. Training is not exempt — world 100
/// is not an early-out. Kill accumulation still skips world 100
/// (<c>0x0041c188</c>); this body still runs.
/// </para>
/// <para>
/// <b>A first-play S unlocks five slots.</b> <c>COMPLETE_LEVEL(100)</c>
/// writes <c>GS_NEW</c> on 0 and 8. <c>GRADE(100) &gt;= C/B/A</c> writes
/// 78, 121, and 164. <c>GRADE(110) &gt;= C</c> writes goodie 1 and
/// concept-art goodie 79, but world 110 stays incomplete / BlankRanking
/// so that arm is the already-pinned incomplete <c>'E'</c> and both
/// stay <c>GS_UNKNOWN</c>. Isolated closed GRADE(110) names 1;
/// isolated closed concept-art names 79. Leftover complete-110 plus ranking 0.25f
/// (already pinned as C) opens both stores. FrontEndHandoff leftover
/// of that C seed still opens it because <c>TryApply</c> calls
/// ApplyUpdate. Isolated leftover C names ApplyUpdate.
/// First-play FrontEndHandoff still leaves goodie 1 at
/// <c>GS_UNKNOWN</c> because world 110 is incomplete; isolated
/// closed GRADE(110) names ApplyUpdate, not <c>TryApply</c>.
/// First-play FrontEndHandoff still leaves goodie 14 at
/// <c>GS_UNKNOWN</c> because world 110 is unlocked but still
/// incomplete; isolated closed COMPLETE_LEVEL(110) names
/// ApplyUpdate, not <c>TryApply</c>.
/// <c>COMPLETE_LEVEL(110)</c>
/// writes goodie 14, but first-play leaves world 110 incomplete so
/// that arm stays closed. Leftover complete-110 plus ranking 0.0f
/// (already pinned as E) opens that store while
/// <c>GRADE(110) &gt;= C</c> stays closed. Lost leftover of the same
/// seed still opens it because <c>CCareer::Update</c> still calls
/// this body then returns (<c>Career.cpp:382-385</c>).
/// FrontEndHandoff leftover of the same seed still opens it
/// because <c>TryApply</c> calls ApplyUpdate.
/// <c>CGrade::operator&gt;=</c> treats <c>'S'</c> as
/// above every other grade, so the already-pinned FillOut 1.0f unlocks
/// the five world-100 slots together. <c>SET_GOODIE_NEW</c> stores 2
/// only when <c>mState &lt;= GS_INSTRUCTIONS</c>. Leftover
/// <c>GS_INSTRUCTIONS</c> on those five slots therefore writes 2.
/// FrontEndHandoff
/// leftover <c>GS_OLD</c> through <c>TryApply</c> still leaves
/// those five slots at 3. Isolated leftover <c>GS_OLD</c> names
/// ApplyUpdate, not <c>TryApply</c>. Isolated leftover
/// <c>GS_INSTRUCTIONS</c> names ApplyUpdate
/// and does not go through <c>TryApply</c>. FrontEndHandoff
/// leftover <c>GS_INSTRUCTIONS</c> writes 2 through
/// <c>TryApply</c>. Existing FrontEndHandoff
/// S goodies start <c>GS_UNKNOWN</c> and name them as New.
/// </para>
/// <para>
/// <b>Do not invent the rest of the table.</b> Other worlds are not in
/// the cold training slice; <c>COMPLETE_LEVEL</c> / <c>GRADE</c> of a
/// missing world is a NULL deref at <c>0x0041C370</c>. Kill thresholds,
/// <c>TOTAL_S_GRADES</c>, episode instruction marks, and the
/// <c>mPendingExtraGoodies</c> latch stay unclaimed. The first-play
/// CountGoodies delta into <c>new_goodie_count</c> is the already-cited
/// Career.cpp:895-897 arm. Leftover <c>GS_OLD</c> on the five
/// first-play S slots is counted before ApplyUpdate because
/// <c>CountGoodies</c> uses <c>mState &gt;= GS_NEW</c>. Isolated
/// leftover <c>GS_OLD</c> <c>NewGoodieCount=0</c> does not uniquely
/// prove that count. Leftover <c>GS_INSTRUCTIONS</c> on those five
/// slots is not counted before ApplyUpdate because
/// <c>GS_INSTRUCTIONS</c> is below <c>GS_NEW</c>. Isolated leftover
/// <c>GS_INSTRUCTIONS</c> store names the write to 2, not the
/// pre-count.
/// </para>
/// </remarks>
public static class RetailCareerUpdateGoodieStates
{
    /// <summary>Goodie 0 — complete world 100 — <c>Career.cpp:690</c>.</summary>
    public const int CompleteWorld100Bio = 0;

    /// <summary>Goodie 1 — <c>GRADE(110) &gt;= C</c> — <c>Career.cpp:691</c>.</summary>
    public const int GradeCOnWorld110 = 1;

    /// <summary>
    /// Goodie 79 — concept-art <c>GRADE(110) &gt;= C</c> —
    /// <c>Career.cpp:770</c>. Same predicate as
    /// <see cref="GradeCOnWorld110"/>; first-play leaves world 110
    /// incomplete so this stays <c>GS_UNKNOWN</c>. Isolated closed
    /// concept-art names that first-play store. Leftover
    /// complete-110 plus ranking 0.25f opens the store.
    /// </summary>
    public const int GradeCConceptArtOnWorld110 = 79;

    /// <summary>Goodie 8 — complete world 100 — <c>Career.cpp:698</c>.</summary>
    public const int CompleteWorld100Second = 8;

    /// <summary>Goodie 14 — complete world 110 — <c>Career.cpp:704</c>.</summary>
    public const int CompleteWorld110 = 14;

    /// <summary>Goodie 78 — <c>GRADE(100) &gt;= C</c> — <c>Career.cpp:769</c>.</summary>
    public const int GradeCOnWorld100 = 78;

    /// <summary>Goodie 121 — <c>GRADE(100) &gt;= B</c> — <c>Career.cpp:813</c>.</summary>
    public const int GradeBOnWorld100 = 121;

    /// <summary>Goodie 164 — <c>GRADE(100) &gt;= A</c> — <c>Career.cpp:857</c>.</summary>
    public const int GradeAOnWorld100 = 164;

    /// <summary>
    /// The Level 100 arms of <c>CCareer::UpdateGoodieStates</c> —
    /// <c>Career.cpp:690 / 691 / 698 / 704 / 769 / 770 / 813 / 857</c>,
    /// <c>0x0041c470</c>, then the already-cited <c>CountGoodies</c>
    /// delta into <c>new_goodie_count</c> and the goodie-0
    /// <c>first_goodie</c> latch (<c>Career.cpp:686 / 688 / 895-900</c>).
    /// After <c>GetAndResetGoodieNewCount</c> /
    /// <c>GetAndResetFirstGoodie</c> a replay <c>ApplyUpdate</c> leaves
    /// both at 0: the count add is delta 0 and the latch is
    /// transition-only. Lost still runs this body
    /// (<c>Career.cpp:382-385</c>) but world 100 stays incomplete, so
    /// both globals stay ctor 0. <c>GRADE(110) &gt;= C</c> stays closed
    /// after first-play: world 110 is incomplete so the lookup is
    /// <c>'E'</c> and goodie 1 stays <c>GS_UNKNOWN</c>. Leftover
    /// complete-110 plus ranking 0.25f opens
    /// <c>SET_GOODIE_NEW(1)</c> and <c>SET_GOODIE_NEW(79)</c>.
    /// Isolated leftover C names 1, not 79. Isolated leftover C
    /// concept-art names ApplyUpdate. Isolated first-play closed
    /// concept-art names ApplyUpdate and leaves 79 at
    /// <c>GS_UNKNOWN</c>. FrontEndHandoff leftover of
    /// that C seed still opens 1 and 79 because <c>TryApply</c>
    /// calls ApplyUpdate. Isolated leftover C does not go through
    /// <c>TryApply</c>. First-play FrontEndHandoff still leaves
    /// goodie 1 at <c>GS_UNKNOWN</c> because world 110 is
    /// incomplete; isolated closed GRADE(110) does not go
    /// through <c>TryApply</c>. First-play FrontEndHandoff still
    /// leaves concept-art 79 at <c>GS_UNKNOWN</c>; isolated
    /// first-play closed concept-art names ApplyUpdate, not
    /// <c>TryApply</c>. <c>COMPLETE_LEVEL(110)</c> stays
    /// closed after first-play: world 110 is unlocked but still
    /// incomplete, so goodie 14 stays <c>GS_UNKNOWN</c>. Isolated
    /// closed COMPLETE_LEVEL(110) does not go through
    /// <c>TryApply</c>. Leftover
    /// complete-110 plus ranking 0.0f (already pinned as E) opens
    /// <c>SET_GOODIE_NEW(14)</c> while <c>GRADE(110) &gt;= C</c> stays
    /// closed. Lost leftover of that same seed still opens 14
    /// because this body still runs on the Lost return
    /// (<c>Career.cpp:382-385</c>). Isolated Won leftover 14 does
    /// not go through Lost ApplyUpdate. FrontEndHandoff leftover of
    /// the same seed still opens 14 because <c>TryApply</c> calls
    /// ApplyUpdate. Isolated leftover 14 and Lost leftover 14 do
    /// not go through <c>TryApply</c>. FrontEndHandoff leftover
    /// <c>GS_OLD</c> on the five first-play S slots still leaves
    /// them at 3 because <c>TryApply</c> calls ApplyUpdate.
    /// Isolated leftover <c>GS_OLD</c> does not go through
    /// <c>TryApply</c>. Isolated leftover
    /// <c>GS_INSTRUCTIONS</c> writes 2 on ApplyUpdate and
    /// does not go through <c>TryApply</c>. FrontEndHandoff leftover
    /// <c>GS_INSTRUCTIONS</c> writes 2 because <c>TryApply</c> calls
    /// ApplyUpdate. Isolated leftover <c>GS_OLD</c> is counted
    /// before ApplyUpdate because <c>CountGoodies</c> uses
    /// <c>mState &gt;= GS_NEW</c>. Isolated leftover
    /// <c>GS_OLD</c> <c>NewGoodieCount=0</c> does not uniquely
    /// prove that count. Isolated leftover <c>GS_INSTRUCTIONS</c>
    /// is not counted before ApplyUpdate because
    /// <c>GS_INSTRUCTIONS</c> is below <c>GS_NEW</c>. Isolated
    /// leftover <c>GS_INSTRUCTIONS</c> store names the write to 2,
    /// not that pre-count. Do not invent
    /// a world-110 FillOut or the rest of the table.
    /// <c>mPendingExtraGoodies</c> and episode instruction marks stay
    /// unclaimed.
    /// </summary>
    public static void Update(RetailCareerCampaign career)
    {
        ArgumentNullException.ThrowIfNull(career);

        int previouslyNew = CountGoodies(career);
        bool goodieZeroWasNotDone =
            career.Goodies.Get(CompleteWorld100Bio) <= RetailCareerGoodieState.Instructions;

        if (career.Nodes.CompleteFlagOf(RetailCareerReCalcLinks.TrainingWorldNumber) == 1)
        {
            career.Goodies.SetNewIfNotDone(CompleteWorld100Bio);
            career.Goodies.SetNewIfNotDone(CompleteWorld100Second);
        }

        if (career.Nodes.CompleteFlagOf(RetailCareerReCalcLinks.TrainingLowerChildWorldNumber) == 1)
        {
            career.Goodies.SetNewIfNotDone(CompleteWorld110);
        }

        List<RetailWorldGradeNode> gradeNodes = GradeNodes(career);
        byte grade = RetailWorldGrade.GradeByteForWorld(
            gradeNodes,
            RetailCareerReCalcLinks.TrainingWorldNumber);
        var held = new RetailGrade(unchecked((sbyte)grade));
        if (held.IsAtLeast(new RetailGrade((sbyte)'C')))
        {
            career.Goodies.SetNewIfNotDone(GradeCOnWorld100);
        }

        if (held.IsAtLeast(new RetailGrade((sbyte)'B')))
        {
            career.Goodies.SetNewIfNotDone(GradeBOnWorld100);
        }

        if (held.IsAtLeast(new RetailGrade((sbyte)'A')))
        {
            career.Goodies.SetNewIfNotDone(GradeAOnWorld100);
        }

        byte grade110 = RetailWorldGrade.GradeByteForWorld(
            gradeNodes,
            RetailCareerReCalcLinks.TrainingLowerChildWorldNumber);
        var held110 = new RetailGrade(unchecked((sbyte)grade110));
        if (held110.IsAtLeast(new RetailGrade((sbyte)'C')))
        {
            career.Goodies.SetNewIfNotDone(GradeCOnWorld110);
            career.Goodies.SetNewIfNotDone(GradeCConceptArtOnWorld110);
        }

        career.Counters.NewGoodieCount += CountGoodies(career) - previouslyNew;
        if (goodieZeroWasNotDone &&
            career.Goodies.Get(CompleteWorld100Bio) > RetailCareerGoodieState.Instructions)
        {
            career.Counters.FirstGoodie = 1;
        }
    }

    /// <summary>
    /// <c>CCareer::CountGoodies</c> — <c>Career.cpp:670-680</c>. Counts
    /// slots whose <c>mState &gt;= GS_NEW</c>. Leftover <c>GS_OLD</c>
    /// on the five first-play S slots therefore reads 5 before
    /// ApplyUpdate. Isolated leftover <c>GS_OLD</c>
    /// <c>NewGoodieCount=0</c> does not uniquely prove that count.
    /// Leftover <c>GS_INSTRUCTIONS</c> on those five slots therefore
    /// reads 0 before ApplyUpdate. Isolated leftover
    /// <c>GS_INSTRUCTIONS</c> store names the write to 2, not that
    /// pre-count. Leftover world-110 complete + E therefore reads 4
    /// after ApplyUpdate because goodie 14 is also <c>GS_NEW</c>.
    /// Isolated leftover 14 names the write of 14, not that count.
    /// Leftover world-110 complete + C therefore reads 6 after
    /// ApplyUpdate because goodie 1 and concept-art 79 are also
    /// <c>GS_NEW</c>. Isolated leftover C names the write of 1, not
    /// that count. Isolated leftover C concept-art names the write
    /// of 79, not that count. Isolated leftover 14 CountGoodies uses
    /// ranking 0.0f so the count is 4.
    /// </summary>
    public static int CountGoodies(RetailCareerCampaign career)
    {
        ArgumentNullException.ThrowIfNull(career);

        int total = 0;
        for (int index = 0; index < career.Goodies.States.Count; index++)
        {
            if (career.Goodies.Get(index) >= RetailCareerGoodieState.New)
            {
                total++;
            }
        }

        return total;
    }

    private static List<RetailWorldGradeNode> GradeNodes(RetailCareerCampaign career)
    {
        var nodes = new List<RetailWorldGradeNode>(career.Nodes.NodeCount);
        for (int index = 0; index < career.Nodes.NodeCount; index++)
        {
            RetailCareerNode node = career.Nodes.Nodes[index];
            nodes.Add(new RetailWorldGradeNode(node.WorldNumber, node.Complete, node.Ranking));
        }

        return nodes;
    }
}
