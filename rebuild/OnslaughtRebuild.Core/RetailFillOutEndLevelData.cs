// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The fields <c>CGame::FillOutEndLevelData</c> writes on a Level 100
/// win. Career <c>Update</c> / <c>ReCalcLinks</c> consume the world,
/// state, ranking, secondaries, kills, and slots; the primary table is
/// copied too and is not a career input.
/// </summary>
public readonly record struct RetailEndLevelSnapshot(
    int WorldFinished,
    int FinalState,
    float Ranking,
    IReadOnlyList<int> SecondaryStatuses,
    IReadOnlyList<int> ThingsKilled,
    IReadOnlyList<int> SlotWords,
    IReadOnlyList<int> PrimaryStatuses,
    IReadOnlyList<int> BaseThingsLeft);

/// <summary>
/// <c>CGame::FillOutEndLevelData</c> as it applies to a Level 100 Won —
/// the snapshot career consumes, plus the already-measured score-time
/// rewrite. First-play elapsed and score stay unclaimed, so
/// <see cref="ForLevel100Won"/> still carries the pre-arm 1.0f store.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/game.cpp:910-1043</c>.
/// Retail identity: <c>0x0046D470</c>. Callers include
/// <c>CGame::RestartLoopRunLevel</c>. The secondary predicate at
/// <c>0x004496E0</c> is already pinned; this type only records when
/// FillOut consults it.
/// </para>
/// <para>
/// <b>Level 100 copies four primaries and no secondaries.</b>
/// <c>mWorldFinished = mCurrentLevel</c> is 100.
/// <c>mFinalState = mGameState</c> is <c>GAME_STATE_LEVEL_WON</c> (5).
/// The ten primary <c>GetStatus()</c> words are four
/// <c>MOS_COMPLETE</c> (1) then six unset — already pinned on
/// <see cref="RetailGameObjectiveCount.Level100WonPrimaryStatuses"/>.
/// That is not the rebuild mission enum's
/// <c>Level100PrimaryObjectiveStatus.Complete = 2</c>.
/// <c>mSecondaryObjectives</c> is the ten-entry array with every
/// <c>GetStatus()</c> unset. Do not invent secondary content.
/// </para>
/// <para>
/// <b>A first-play win SetSlotSave's four tutorial bits.</b>
/// <c>SLOT_TUTORIAL_1..4</c> are 63..66. FillOut copies
/// <c>END_LEVEL_DATA.mSlots = GAME.mSlots</c> (<c>game.cpp:971</c>),
/// so those four bits are the only addressable ones on this
/// snapshot. Do not invent other slots. The 32-dword career
/// assignment stays on <see cref="RetailCareerSlotHandoff"/>.
/// </para>
/// <para>
/// <b>The ranking clamp is gated on a non-zero secondary count.</b>
/// <c>game.cpp:1028</c> is <c>if (GetNumSecondaryObjectives())</c>.
/// When that count is 0 the 0.4 floor and 0.6 cap never run, even
/// though <c>IsAllSecondaryObjectivesComplete</c> would return FALSE.
/// The 0.4 / 0.6 immediates are therefore not claimed here.
/// </para>
/// <para>
/// <b>The score-time arm is live on Level 100.</b> Last
/// <c>CWorld::LoadWorld</c> is the outer RLWD parse. <c>this+0x108</c>
/// / <c>+0x10c</c> / <c>+0x110</c> are RLWD <c>+0x147ba</c> /
/// <c>+0x147be</c> / next dword = 300.0 / 500.0 / 1.0, so
/// <c>0x0046d638</c> <c>fld [this+0x10c]</c> / <c>fsub [this+0x108]</c>
/// / <c>fcomp 0.0</c> / <c>test ah,0x41</c> / <c>jne 0x0046d79b</c>
/// does not skip. Last-wins S/D ints at <c>this+0xf8/+0xfc</c> are
/// 210 / 70. A zero score stores ranking 0 at <c>0x0046d772</c> and
/// skips the <c>0x3a83126f</c> (0.001f) replacement that source
/// <c>game.cpp:1021-1024</c> would apply after clamping −1. First-play
/// elapsed and <c>this+0xf4</c> stay unclaimed — do not rewrite the
/// snapshot ranking from an invented score. FillOut still copies
/// <c>[ebp+0xf4]</c> to <c>0x00672e24</c> and <c>[0x00672fd0]</c>
/// to <c>0x00672e28</c> before that arm.
/// </para>
/// <para>
/// <b>Not established here.</b> A player who wrecks an iceberg
/// and still Wins would store 0 on those type-35 indices.
/// First-play elapsed and score. ConfirmedKill increments when
/// the player actually scores those bits. Career
/// <c>UpdateBaseWorldExistsStuffForNode</c> onto world 110 is
/// the already-pinned <see cref="RetailCareerCampaign"/> slice.
/// </para>
/// </remarks>
public static class RetailFillOutEndLevelData
{
    /// <summary>
    /// Last-wins RLWD <c>+0x147ba</c> into <c>CGame+0x108</c>.
    /// </summary>
    public const float Level100FullScoreTime = 300.0f;

    /// <summary>
    /// Last-wins RLWD <c>+0x147be</c> into <c>CGame+0x10c</c>.
    /// </summary>
    public const float Level100PercentageScoreTime = 500.0f;

    /// <summary>
    /// Last-wins RLWD dword after the percentage time, into
    /// <c>CGame+0x110</c>. With this 1.0 the time multiplier is
    /// identically 1.0, so elapsed does not change the scaled score.
    /// </summary>
    public const float Level100ScorePercentage = 1.0f;

    /// <summary>
    /// Last-wins RLWD int at the <c>this+0xf8</c> S-grade slot.
    /// </summary>
    public const int Level100SGradeScore = 210;

    /// <summary>
    /// Last-wins RLWD int at the <c>this+0xfc</c> D-grade slot.
    /// </summary>
    public const int Level100DGradeScore = 70;

    /// <summary>
    /// <c>[0x0085515c]</c> after a first-play Level 100 Won — BSWD
    /// At() membership (27 type-8 + 6 type-35 + 2 type-37
    /// <c>CSafeSide</c>). Not the materializer's 33 visible units.
    /// </summary>
    public const int Level100BaseWorldThingCount = 35;

    /// <summary>Shipped Level 100 primary count — <c>LevelScript.msl</c>.</summary>
    public const int Level100PrimaryCount = 4;

    /// <summary>Shipped Level 100 secondary count — there are none.</summary>
    public const int Level100SecondaryCount = 0;

    /// <summary>
    /// <c>CGame::Init</c> / restart store. <c>EndLevelData.h:27</c>
    /// is 0 when no lost string is defined. <c>DeclareLevelWon</c>
    /// <c>0x0046f2f0</c> writes <c>+0x28</c> and <c>+0x48</c> only —
    /// not <c>+0x114</c>.
    /// </summary>
    public const int Level100WonLostReason = 0;

    /// <summary>
    /// FillOut copies <c>CGame+0x114</c> to <c>0x00672e2c</c>
    /// (<c>0x0046d5d0</c>). A Level 100 Won still holds the init 0
    /// because <c>DeclareLevelWon</c> does not write that slot.
    /// Mutation: adopt a leftover lost-string id. First-play elapsed
    /// and score stay unclaimed.
    /// </summary>
    public static int LostReasonWord(int gameLevelLostReason) => gameLevelLostReason;

    /// <summary>
    /// FillOut <c>0x0046d5cb</c> <c>mov [0x00672e24], eax</c> from
    /// <c>[ebp+0xf4]</c>. The score-time arm later fistp-rewrites
    /// <c>CGame+0xf4</c> at <c>0x0046d6ff</c> and does not store
    /// <c>0x00672e24</c> again. First-play <c>this+0xf4</c> stays
    /// unclaimed — mutation: leftover 999, or the post-arm scaled
    /// score.
    /// </summary>
    public static int ScoreWord(int gameScore) => gameScore;

    /// <summary>
    /// FillOut <c>0x0046d5c5</c> <c>mov [0x00672e28], ecx</c> from
    /// <c>[0x00672fd0]</c> (<c>EVENT_MANAGER.GetTime()</c>). The arm
    /// rereads that dword for the multiplier and never writes
    /// <c>0x00672e28</c> again. First-play elapsed stays unclaimed —
    /// mutation: leftover 12.5.
    /// </summary>
    public static float TimeTakenWord(float eventManagerTime) => eventManagerTime;

    /// <summary>The ten unset <c>GetStatus()</c> words FillOut copies for Level 100.</summary>
    public static int[] UnsetSecondaryStatuses() =>
        new int[RetailEndLevelObjectives.SecondaryObjectiveCount];

    /// <summary>
    /// <c>GAME.mSlots</c> after a first-play Level 100 win
    /// <c>SetSlotSave</c>'d <c>SLOT_TUTORIAL_1..4</c>. FillOut copies
    /// these 32 words into <c>END_LEVEL_DATA.mSlots</c>.
    /// </summary>
    public static int[] FirstPlayTutorialSlotWords()
    {
        var slots = new RetailCareerSlots();
        slots.SetSlot(RetailCareerSlotHandoff.TutorialIntroductionSlot, 1);
        slots.SetSlot(RetailCareerSlotHandoff.TutorialPulseCannonSlot, 1);
        slots.SetSlot(RetailCareerSlotHandoff.TutorialVulcanCannonSlot, 1);
        slots.SetSlot(RetailCareerSlotHandoff.TutorialStatusBarsSlot, 1);
        var words = new int[RetailCareerSlots.SlotWords];
        for (int index = 0; index < words.Length; index++)
        {
            words[index] = slots.Words[index];
        }

        return words;
    }

    /// <summary>
    /// FillOut <c>mBaseThingsLeft</c> after a first-play Level 100 Won
    /// that does not wreck a list member. Size is 35; slots 0..34 are
    /// 1; 35..287 stay 0. Iceberg player-kill store-0 stays open.
    /// </summary>
    public static int[] FirstPlayBaseThingsLeft()
    {
        var left = new int[RetailCareerNode.BaseThingsExistsSize];
        for (int index = 0; index < Level100BaseWorldThingCount; index++)
        {
            left[index] = 1;
        }

        return left;
    }

    /// <summary>
    /// FillOut <c>mBaseThingsLeft</c> from a level's end state (the base list
    /// at <c>world+0xc0</c>, read at <c>0x0046d4cb-0x0046d4d1</c>): for each
    /// base-world row, 1 when the load built it and it is not dying, else 0.
    /// A row the load skipped (the career's carry-over) has a null reader.
    /// Core builds no actor for the two SafeSides (rows 21 and 22); they are
    /// built and never die. Words 35..287 stay 0.
    /// </summary>
    public static int[] BaseThingsLeft(WorldSnapshot state, Level100ActorDefinitionSet definitions)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(definitions);
        var left = new int[RetailCareerNode.BaseThingsExistsSize];
        for (int row = 0; row < Level100BaseWorldThingCount; row++)
        {
            if (state.Level100Actors.LostBaseRows.Contains(row))
            {
                continue;
            }

            string identity = Level100ActorRegistry.BaseRowIdentity(row);
            if (!definitions.Actors.Any(definition => StringComparer.Ordinal.Equals(definition.DefinitionIdentity, identity)))
            {
                left[row] = 1;
                continue;
            }

            Level100ActorSnapshot? actor = state.Level100Actors.Actors.SingleOrDefault(item =>
                item.SpawnOwnerId is null && StringComparer.Ordinal.Equals(item.DefinitionIdentity, identity));
            left[row] = BaseThingLeftWord(
                actor is not null,
                actor?.Lifecycle == Level100ActorLifecycle.Alive ? 0 : ThingFlagDying);
        }

        return left;
    }

    /// <summary>
    /// <c>TF_DYING</c> — <c>0x0046d4d1</c> <c>test byte [eax+0x2c],4</c>.
    /// </summary>
    public const int ThingFlagDying = 4;

    /// <summary>
    /// One FillOut base-thing store. <c>0x0046d4cb</c> null-reader and
    /// <c>0x0046d4d1</c> TF_DYING both write 0. First-play does not
    /// take that arm. Player iceberg-kill values stay unclaimed.
    /// </summary>
    public static int BaseThingLeftWord(bool thingLive, int thingFlags2c)
    {
        if (!thingLive || (thingFlags2c & ThingFlagDying) != 0)
        {
            return 0;
        }

        return 1;
    }

    /// <summary>
    /// FillOut copies five dwords from <c>player+8</c>. Those start at
    /// ctor-zero and only <c>0x004d30d0</c> increments them. A first-play
    /// Won that never takes ConfirmedKill is therefore five zeros.
    /// Career still skips world 100.
    /// </summary>
    public static int[] FirstPlayThingsKilled() =>
        new int[RetailCareerCounters.KilledTypeCount];

    /// <summary>
    /// FillOut kill copy. <c>0x0046d5ff</c> <c>test eax,eax</c> /
    /// <c>je 0x0046d61d</c> stores five zeros. A live player copies
    /// five dwords from <c>player+8</c>. First-play is the live
    /// ctor-zero path. Do not invent those totals.
    /// </summary>
    public static int[] ThingsKilledReadout(
        bool playerPresent,
        IReadOnlyList<int>? playerKillWords = null)
    {
        var words = new int[RetailCareerCounters.KilledTypeCount];
        if (!playerPresent)
        {
            return words;
        }

        IReadOnlyList<int> source = playerKillWords ?? FirstPlayThingsKilled();
        if (source.Count != words.Length)
        {
            throw new ArgumentException(
                $"FillOut copies {words.Length} kill dwords.",
                nameof(playerKillWords));
        }

        for (int index = 0; index < words.Length; index++)
        {
            words[index] = source[index];
        }

        return words;
    }

    /// <summary>
    /// The post-Won snapshot Level 100 hands to career. Ranking defaults to
    /// the <c>mRanking=1.0f</c> store at <c>game.cpp:967</c> before the
    /// score-time arm; callers may override. First-play elapsed and score
    /// stay unclaimed, so this does not run
    /// <see cref="AfterScoreTimeArm"/>.
    /// </summary>
    public static RetailEndLevelSnapshot ForLevel100Won(
        float ranking = 1.0f,
        IReadOnlyList<int>? thingsKilled = null,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        return new RetailEndLevelSnapshot(
            RetailCareerReCalcLinks.TrainingWorldNumber,
            RetailCareerReCalcLinks.GameStateLevelWon,
            ranking,
            UnsetSecondaryStatuses(),
            thingsKilled ?? FirstPlayThingsKilled(),
            FirstPlayTutorialSlotWords(),
            RetailGameObjectiveCount.Level100WonPrimaryStatuses(),
            baseThingsLeft ?? FirstPlayBaseThingsLeft());
    }

    /// <summary>
    /// The <c>game.cpp:1028-1042</c> ranking clamp. A zero secondary count
    /// is a skip. Non-zero counts are a different, unmeasured arm.
    /// </summary>
    public static float AfterSecondaryRankingClamp(
        float ranking,
        int secondaryCount,
        IReadOnlyList<int> secondaryObjectiveStatuses)
    {
        if (secondaryObjectiveStatuses is null)
        {
            throw new ArgumentNullException(nameof(secondaryObjectiveStatuses));
        }

        if (secondaryCount == 0)
        {
            return ranking;
        }

        throw new InvalidOperationException(
            "FillOutEndLevelData only consults IsAllSecondaryObjectivesComplete " +
            "when GetNumSecondaryObjectives() is non-zero. The 0.4 / 0.6 " +
            "immediates are not measured for this owner; Level 100 never " +
            "reaches that arm.");
    }

    /// <summary>
    /// <c>0x0046d638</c> <c>fcomp 0.0</c> / <c>test ah,0x41</c> /
    /// <c>jne 0x0046d79b</c> — skip when (percentage − full) ≤ 0.
    /// </summary>
    public static bool ScoreTimeArmSkips(float fullScoreTime, float percentageScoreTime)
    {
        return !(percentageScoreTime - fullScoreTime > 0.0f);
    }

    /// <summary>
    /// Live score-time rewrite at <c>0x0046d659</c>–<c>0x0046d79b</c>.
    /// A skip returns <paramref name="preArmRanking"/>. Below D stores
    /// 0 and does not apply the 0.001 replacement.
    /// </summary>
    public static float AfterScoreTimeArm(
        float preArmRanking,
        float elapsedTime,
        float fullScoreTime,
        float percentageScoreTime,
        float scorePercentage,
        int score,
        int sGradeScore,
        int dGradeScore)
    {
        if (ScoreTimeArmSkips(fullScoreTime, percentageScoreTime))
        {
            return preArmRanking;
        }

        float multiplier;
        if (elapsedTime < fullScoreTime)
        {
            multiplier = 1.0f;
        }
        else if (elapsedTime < percentageScoreTime)
        {
            float delta = percentageScoreTime - fullScoreTime;
            multiplier = scorePercentage
                - ((elapsedTime - percentageScoreTime) / delta)
                * (1.0f - scorePercentage);
        }
        else
        {
            multiplier = scorePercentage;
        }

        if (!(multiplier <= 1.0f))
        {
            multiplier = 1.0f;
        }
        else if (multiplier < 0.0f)
        {
            multiplier = 0.0f;
        }

        int scaled = LowDwordOfFistp(score * (double)multiplier);
        if (scaled >= sGradeScore)
        {
            return 1.0f;
        }

        if (scaled < dGradeScore)
        {
            return 0.0f;
        }

        float ranking = (scaled - (float)dGradeScore)
            / (sGradeScore - (float)dGradeScore);
        if (!(ranking <= 1.0f))
        {
            return 1.0f;
        }

        if (ranking < 0.0f)
        {
            return 0.0f;
        }

        if (ranking == 0.0f)
        {
            return PointZeroZeroOne;
        }

        return ranking;
    }

    /// <summary>
    /// The fistp'd <c>CGame.mScore</c> rewrite at <c>0x0046d6ff</c>.
    /// FillOut does not copy this back to <c>0x00672e24</c>. A skip
    /// leaves the pre-arm score. Leftover <c>LoadLevel</c> 0.5 at
    /// elapsed 400 turns 140 into 105.
    /// </summary>
    public static int AfterScoreTimeArmGameScore(
        float elapsedTime,
        float fullScoreTime,
        float percentageScoreTime,
        float leftoverScorePercentage,
        int score)
    {
        if (ScoreTimeArmSkips(fullScoreTime, percentageScoreTime))
        {
            return score;
        }

        float multiplier;
        if (elapsedTime < fullScoreTime)
        {
            multiplier = 1.0f;
        }
        else if (elapsedTime < percentageScoreTime)
        {
            float delta = percentageScoreTime - fullScoreTime;
            multiplier = leftoverScorePercentage
                - ((elapsedTime - percentageScoreTime) / delta)
                * (1.0f - leftoverScorePercentage);
        }
        else
        {
            multiplier = leftoverScorePercentage;
        }

        if (!(multiplier <= 1.0f))
        {
            multiplier = 1.0f;
        }
        else if (multiplier < 0.0f)
        {
            multiplier = 0.0f;
        }

        return LowDwordOfFistp(score * (double)multiplier);
    }

    private const float PointZeroZeroOne = 0.001f;

    /// <summary>
    /// <c>0x0046d6ed</c> <c>fistp qword ptr [esp+0x18]</c> under ambient
    /// <c>/QIfist</c> — round-to-nearest-even, then the low dword.
    /// </summary>
    private static int LowDwordOfFistp(double value)
    {
        double rounded = Math.Round(value, MidpointRounding.ToEven);
        if (double.IsNaN(rounded) ||
            rounded < -9223372036854775808.0 ||
            rounded >= 9223372036854775808.0)
        {
            return unchecked((int)long.MinValue);
        }

        return unchecked((int)(long)rounded);
    }
}
