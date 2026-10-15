// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One released career-graph edge — the 8-byte <c>CCareerNodeLink</c> at
/// <c>CCareer + 0x1904</c>.
/// </summary>
/// <remarks>
/// Owner in the pinned drop: <c>references/Onslaught/Career.h:58-73</c>.
/// Retail identity: stride 8 from <c>0x0041BC8A</c> /
/// <see cref="RetailCareerRecordLayout.LinkStride"/>. The three
/// <c>ECNLinkType</c> values are the enum order
/// <c>CN_NOT_COMPLETE</c>, <c>CN_COMPLETE</c>,
/// <c>CN_COMPLETE_BROKEN</c> — <c>Career.h:58-63</c> — and the save-format
/// walk in <c>reverse-engineering/save-file/career-graph.md</c> reads the
/// same 0 / 1 / 2 out of real <c>.bes</c> files.
/// </remarks>
public sealed class RetailCareerNodeLink
{
    /// <summary><c>CN_NOT_COMPLETE</c> — <c>Career.h:60</c>.</summary>
    public const int NotComplete = 0;

    /// <summary><c>CN_COMPLETE</c> — <c>Career.h:61</c>; <c>*link = 1</c> in <c>ReCalcLinks</c>.</summary>
    public const int Complete = 1;

    /// <summary><c>CN_COMPLETE_BROKEN</c> — <c>Career.h:62</c>; the alternate-parent store.</summary>
    public const int CompleteBroken = 2;

    /// <summary><c>mLinkType</c> — <c>+0x00</c>.</summary>
    public int LinkType { get; set; } = NotComplete;

    /// <summary><c>mToNode</c> — <c>+0x04</c>; a node index, or <c>-1</c> for an unused child slot.</summary>
    public int ToNode { get; set; } = -1;
}

/// <summary>
/// The cold-career Level 100 slice of <c>CCareer::Update</c> and
/// <c>CCareer::ReCalcLinks</c> — the post-Won consumer of
/// <see cref="RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete"/>.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:379-515</c>
/// and the first two rows of <c>level_structure</c>
/// (<c>Career.cpp</c> / <c>career-graph.md</c> node 0 = world 100 → node 1 =
/// world 110, higher child <c>-1</c>). Retail identities:
/// <c>0x0041BD00</c> <c>CCareer::Update</c>, <c>0x0041BDF0</c>
/// <c>CCareer::ReCalcLinks</c>, calling <c>0x004496E0</c>.
/// </para>
/// <para>
/// <b>Level 100 has four primaries and no secondaries.</b> The shipped
/// <c>LevelScript.msl</c> defines only those four;
/// <c>CGame::GetNumSecondaryObjectives</c> is therefore 0, so
/// <c>FillOutEndLevelData</c> at <c>game.cpp:1028</c> never consults the
/// secondary predicate for the ranking clamp. The ten secondary status
/// words stay unset (neither 1 nor 2). The predicate itself still runs
/// from <c>ReCalcLinks</c> and takes the no-objectives arm: log, then
/// <c>xor eax, eax</c>. That FALSE is indistinguishable from a real
/// failure. This type must not invent secondary content to make it TRUE.
/// </para>
/// <para>
/// <b>The FALSE does not lock world 110.</b> <c>GetChildLinks</c> always
/// yields the lower and higher link pointers
/// (<c>Career.cpp:268-273</c>). World 100 is not 500, so the higher
/// dummy (<c>mToNode == -1</c>) completes only when the predicate is
/// TRUE — it stays <c>CN_NOT_COMPLETE</c> — and the lower link to node 1
/// completes unconditionally (<c>Career.cpp:488-490</c>). A rebuild that
/// required secondaries for <i>every</i> child would leave training's
/// only exit locked. A rebuild that treated "no secondaries" as success
/// would mark the dummy higher link complete.
/// </para>
/// <para>
/// <b>Lost does not touch the graph.</b> <c>Update</c> returns after
/// <c>UpdateGoodieStates</c> when
/// <c>END_LEVEL_DATA.mFinalState != GAME_STATE_LEVEL_WON</c>
/// (<c>Career.cpp:382-385</c>). <c>GAME_STATE_LEVEL_WON</c> is 5 —
/// <c>con_win</c> stores 5; <c>GAME_STATE_LEVEL_LOST</c> is 4
/// (<c>game.h:42-53</c>, <c>IScript</c> <c>cmp …,4</c>).
/// </para>
/// <para>
/// <b>Ranking is written only onto the finished world, and only if
/// strictly greater.</b>
/// <c>Career.cpp:396-406</c> looks up
/// <c>GetNodeFromWorldNo(mWorldFinished)</c> and stores
/// <c>mRanking</c> there when the snapshot is strictly greater. After
/// a Level 100 win that is world 100 at the already-pinned 1.0f
/// (grade S). A worse replay does not downgrade it. Unlocking world
/// 110 does not copy the ranking; that node stays
/// <c>BlankRanking</c> / grade E.
/// </para>
/// <para>
/// <b><c>mNumAttempts</c> is not a store on this path.</b>
/// <c>CCareer::Update</c> (<c>Career.cpp:379-418</c>) never writes
/// <c>+0x38</c>. The only store is <c>CCareerNode::Blank</c>
/// (<c>Career.cpp:99</c> / <c>0x0041B740</c>). A leftover attempt
/// count on world 100 and world 110 survives first-play ApplyUpdate.
/// </para>
/// <para>
/// <b>Base things land on world 110, not 100.</b>
/// <c>Career.cpp:443-452</c> calls
/// <c>UpdateBaseWorldExistsStuffForNode(GetNodeFromWorldNo(
/// level_structure[node][3]))</c> when that column is not -1.
/// For world 100 that column is 110. The 288-word FillOut vector
/// is forwarded through <c>SetBaseThingExistTo</c>
/// (<c>Career.cpp:519-527</c>); only literal 1 sets a bit, so the
/// first-play zeros at 35..287 clear Blank's all-1s on 110.
/// <c>level_structure[0][4]</c> is -1, so the secondary arm does
/// not run.
/// </para>
/// <para>
/// <b>Not established here.</b> The world-500 slot arm
/// (<c>Career.cpp:468-481</c>). Iceberg player-kill store-0.
/// First-play elapsed and score. ConfirmedKill increments. The
/// ranking clamp constants 0.4 / 0.6 inside
/// <c>FillOutEndLevelData</c> — they are gated on a non-zero
/// secondary count, so Level 100 never reaches them. The Level
/// 100 goodie arms (0, 8, 78, 121, 164) are the already-pinned
/// <see cref="RetailCareerUpdateGoodieStates"/> slice. Goodie 1
/// (<c>GRADE(110) &gt;= C</c>) stays closed on first-play because
/// world 110 is incomplete; leftover complete-110 plus C opens
/// the store. FrontEndHandoff leftover of that C seed still
/// opens it through <c>TryApply</c>. Isolated leftover C names
/// ApplyUpdate. First-play FrontEndHandoff still leaves goodie 1
/// at <c>GS_UNKNOWN</c> because world 110 is incomplete; isolated
/// closed GRADE(110) names ApplyUpdate, not <c>TryApply</c>.
/// Goodie 14 (<c>COMPLETE_LEVEL(110)</c>) stays closed
/// on first-play because world 110 is unlocked but still
/// incomplete. First-play FrontEndHandoff still leaves
/// goodie 14 at <c>GS_UNKNOWN</c>; isolated closed
/// COMPLETE_LEVEL(110) names ApplyUpdate, not
/// <c>TryApply</c>. Leftover complete-110 plus ranking 0.0f (already
/// pinned as E) opens that store. Lost leftover of the same
/// seed still opens it because <c>Update</c> still calls
/// <c>UpdateGoodieStates</c> then returns
/// (<c>Career.cpp:382-385</c>). FrontEndHandoff leftover of
/// the same seed still opens it through <c>TryApply</c>.
/// Isolated leftover 14 names ApplyUpdate; Lost leftover 14
/// names the Lost return. Do not invent a world-110 FillOut.
/// </para>
/// </remarks>
public static class RetailCareerReCalcLinks
{
    /// <summary>World 100 — <c>level_structure[0][0]</c>; <c>cmp eax, 0x64</c> at <c>0x0041C188</c>.</summary>
    public const int TrainingWorldNumber = 100;

    /// <summary>World 110 — <c>level_structure[0][1]</c> is node index 1, whose world is 110.</summary>
    public const int TrainingLowerChildWorldNumber = 110;

    /// <summary>
    /// <c>level_structure[0][3]</c> — the world
    /// <c>UpdateBaseWorldExistsStuffForNode</c> writes after a world-100
    /// primary. Same number as the lower child, different column.
    /// </summary>
    public const int TrainingPrimaryBaseThingsWorldNumber = 110;

    /// <summary>
    /// <c>level_structure[0][4]</c> — no higher-tier base-things
    /// destination, and Level 100 secondaries are the no-objectives
    /// FALSE anyway.
    /// </summary>
    public const int TrainingSecondaryBaseThingsWorldNumber = -1;

    /// <summary>The unused higher child of world 100 — <c>level_structure[0][2]</c>.</summary>
    public const int TrainingHigherChildNodeIndex = -1;

    /// <summary>Shipped Level 100 <c>LevelScript.msl</c> primary count.</summary>
    public const int TrainingPrimaryObjectiveCount = 4;

    /// <summary>Shipped Level 100 secondary count — there are none.</summary>
    public const int TrainingSecondaryObjectiveCount = 0;

    /// <summary><c>GAME_STATE_LEVEL_WON</c> — <c>game.h:49</c>; <c>con_win</c> stores 5.</summary>
    public const int GameStateLevelWon = 5;

    /// <summary><c>GAME_STATE_LEVEL_LOST</c> — <c>game.h:48</c>; <c>cmp …,4</c>.</summary>
    public const int GameStateLevelLost = 4;

    /// <summary>
    /// Whether <c>FillOutEndLevelData</c> at <c>game.cpp:1028</c> consults the
    /// secondary predicate. Level 100's count is 0, so the ranking clamp is
    /// skipped even though the predicate would return FALSE.
    /// </summary>
    public static bool AppliesSecondaryRankingClamp =>
        TrainingSecondaryObjectiveCount != 0;

    /// <summary>
    /// A fresh career whose first two nodes match the shipped training
    /// slice: world 100 with lower child 110 and a dummy higher link.
    /// </summary>
    public static RetailCareerCampaign CreateColdTrainingSlice()
    {
        var career = new RetailCareerCampaign();
        RetailCareerNode training = career.Nodes.Add(TrainingWorldNumber, complete: 0);
        RetailCareerNode next = career.Nodes.Add(TrainingLowerChildWorldNumber, complete: 0);

        career.Links.Add(new RetailCareerNodeLink { ToNode = 1 });
        career.Links.Add(new RetailCareerNodeLink { ToNode = TrainingHigherChildNodeIndex });
        career.Links.Add(new RetailCareerNodeLink());
        career.Links.Add(new RetailCareerNodeLink());

        training.LowerLink = 0;
        training.HigherLink = 1;
        next.LowerLink = 2;
        next.HigherLink = 3;
        return career;
    }
}

/// <summary>
/// The nodes, links, kill counters, slots, and Level 100 goodie
/// arms <see cref="RetailCareerReCalcLinks"/> mutates. Not a full
/// <c>CCareer</c> — only the fields the Level 100 Won path reads.
/// </summary>
public sealed class RetailCareerCampaign
{
    private const int World500 = 500;

    /// <summary><c>mNode</c>.</summary>
    public RetailCareerNodeTable Nodes { get; } = new();

    /// <summary><c>mNodeLink</c>.</summary>
    public List<RetailCareerNodeLink> Links { get; } = new();

    /// <summary><c>mKilledThings</c> plus the world-100 skip.</summary>
    public RetailCareerCounters Counters { get; } = new();

    /// <summary><c>mSlots</c> — overwritten from FillOut on a Won update.</summary>
    public RetailCareerSlots Slots { get; } = new();

    /// <summary><c>mGoodies</c> — recomputed after a Won or Lost update.</summary>
    public RetailCareerGoodies Goodies { get; } = new();

    /// <summary><c>mCareerInProgress</c> — set only on a Won update.</summary>
    public int CareerInProgress { get; private set; }

    /// <summary><c>CCareer::GetLink</c> — <c>Career.h:139</c>.</summary>
    public RetailCareerNodeLink? GetLink(int index)
    {
        if (index < 0 || index >= Links.Count)
        {
            return null;
        }

        return Links[index];
    }

    /// <summary>
    /// <c>CCareer::Update</c> then <c>ReCalcLinks</c> —
    /// <c>Career.cpp:379-515</c>, <c>0x0041BD00</c> / <c>0x0041BDF0</c>.
    /// </summary>
    public void ApplyUpdate(RetailEndLevelSnapshot snapshot) =>
        ApplyUpdate(
            snapshot.FinalState,
            snapshot.WorldFinished,
            snapshot.Ranking,
            snapshot.SecondaryStatuses,
            snapshot.ThingsKilled,
            snapshot.SlotWords,
            snapshot.BaseThingsLeft);

    /// <summary>
    /// <c>CCareer::Update</c> then <c>ReCalcLinks</c> —
    /// <c>Career.cpp:379-515</c>, <c>0x0041BD00</c> / <c>0x0041BDF0</c>.
    /// The 32-dword slot store is the already-pinned
    /// <see cref="RetailCareerSlotHandoff.OverwriteFromEndLevel"/>;
    /// this method does not reimplement that copy. Lost returns
    /// before that store and before <c>ReCalcLinks</c> /
    /// <c>UpdateBaseWorldExistsStuffForNode</c>
    /// (<c>Career.cpp:382-385</c> / <c>392</c> / <c>416</c> /
    /// <c>443-452</c> / <c>519-527</c>). Lost leftover world-110
    /// complete + ranking 0.0f still opens
    /// <c>SET_GOODIE_NEW(14)</c> because this method still calls
    /// <c>UpdateGoodieStates</c> on that return. This method does not
    /// write <c>CCareerNode.mNumAttempts</c> (<c>+0x38</c>);
    /// <c>Career.cpp:379-418</c> never does.
    /// </summary>
    public void ApplyUpdate(
        int finalState,
        int worldFinished,
        float ranking,
        IReadOnlyList<int> secondaryObjectiveStatuses,
        IReadOnlyList<int> thingsKilledThisLevel,
        IReadOnlyList<int>? slotWords = null,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        if (finalState != RetailCareerReCalcLinks.GameStateLevelWon)
        {
            RetailCareerUpdateGoodieStates.Update(this);
            return;
        }

        if (slotWords is not null)
        {
            RetailCareerSlotHandoff.OverwriteFromEndLevel(Slots, slotWords);
        }

        Counters.UpdateThingsKilled(worldFinished, thingsKilledThisLevel);

        RetailCareerNode? node = Nodes.Find(worldFinished);
        if (node is null)
        {
            return;
        }

        if (ranking > node.Ranking)
        {
            node.Ranking = ranking;
        }

        node.Complete = 1;
        CareerInProgress = 1;
        ReCalcLinks(worldFinished, secondaryObjectiveStatuses, baseThingsLeft);
        RetailCareerUpdateGoodieStates.Update(this);
    }

    /// <summary>
    /// <c>CCareer::ReCalcLinks</c> — <c>Career.cpp:423-515</c>,
    /// <c>0x0041BDF0</c>. World 500 is a different slot-gated arm and is
    /// refused here rather than silently taking the secondary-objective path.
    /// </summary>
    public void ReCalcLinks(
        int worldFinished,
        IReadOnlyList<int> secondaryObjectiveStatuses,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        if (worldFinished == World500)
        {
            throw new InvalidOperationException(
                "World 500 completes child links from slots 61/62, not from " +
                "IsAllSecondaryObjectivesComplete; that arm is not this owner.");
        }

        RetailCareerNode? finished = Nodes.Find(worldFinished);
        if (finished is null)
        {
            return;
        }

        if (baseThingsLeft is not null)
        {
            CopyLevel100PrimaryBaseThings(worldFinished, baseThingsLeft);
        }

        RetailSecondaryObjectiveVerdict verdict =
            RetailEndLevelObjectives.IsAllSecondaryObjectivesComplete(
                secondaryObjectiveStatuses);

        // GetChildLinks always yields lower then higher, even when ToNode is -1.
        TryCompleteChild(GetLink(finished.LowerLink), isHigher: false, verdict);
        TryCompleteChild(GetLink(finished.HigherLink), isHigher: true, verdict);
    }

    /// <summary>
    /// <c>CCareer::UpdateBaseWorldExistsStuffForNode</c> —
    /// <c>Career.cpp:519-527</c>. Forwards each of the 288 FillOut
    /// dwords through <see cref="RetailCareerNode.SetBaseThingExistTo"/>.
    /// </summary>
    public static void UpdateBaseWorldExistsStuffForNode(
        RetailCareerNode node,
        IReadOnlyList<int> baseThingsLeft)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(baseThingsLeft);

        for (int offset = 0; offset < RetailCareerNode.BaseThingsExistsSize; offset++)
        {
            int value = offset < baseThingsLeft.Count ? baseThingsLeft[offset] : 0;
            node.SetBaseThingExistTo(offset, value);
        }
    }

    private void CopyLevel100PrimaryBaseThings(
        int worldFinished,
        IReadOnlyList<int> baseThingsLeft)
    {
        if (worldFinished != RetailCareerReCalcLinks.TrainingWorldNumber)
        {
            return;
        }

        RetailCareerNode? destination = Nodes.Find(
            RetailCareerReCalcLinks.TrainingPrimaryBaseThingsWorldNumber);
        if (destination is null)
        {
            return;
        }

        UpdateBaseWorldExistsStuffForNode(destination, baseThingsLeft);
    }

    private void TryCompleteChild(
        RetailCareerNodeLink? link,
        bool isHigher,
        RetailSecondaryObjectiveVerdict verdict)
    {
        if (link is null || link.LinkType == RetailCareerNodeLink.Complete)
        {
            return;
        }

        bool complete = isHigher ? verdict.Result : true;
        if (!complete)
        {
            return;
        }

        link.LinkType = RetailCareerNodeLink.Complete;
        MarkCompetingParentsBroken(link);
    }

    private void MarkCompetingParentsBroken(RetailCareerNodeLink completed)
    {
        RetailCareerNode? toNode = NodeAt(completed.ToNode);
        if (toNode is null)
        {
            return;
        }

        for (int index = 0; index < Nodes.NodeCount; index++)
        {
            RetailCareerNode node = Nodes.Nodes[index];
            MarkIfCompeting(GetLink(node.LowerLink), completed, toNode);
            MarkIfCompeting(GetLink(node.HigherLink), completed, toNode);
        }
    }

    private void MarkIfCompeting(
        RetailCareerNodeLink? candidate,
        RetailCareerNodeLink completed,
        RetailCareerNode toNode)
    {
        if (candidate is null ||
            ReferenceEquals(candidate, completed) ||
            candidate.LinkType != RetailCareerNodeLink.Complete)
        {
            return;
        }

        if (ReferenceEquals(NodeAt(candidate.ToNode), toNode))
        {
            candidate.LinkType = RetailCareerNodeLink.CompleteBroken;
        }
    }

    private RetailCareerNode? NodeAt(int index)
    {
        if (index < 0 || index >= Nodes.NodeCount)
        {
            return null;
        }

        return Nodes.Nodes[index];
    }
}
