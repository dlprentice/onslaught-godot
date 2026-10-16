// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/EndLevelData.h and src/EndLevelData.cpp (CEndLevelData).

namespace OnslaughtRebuild.Parity.Career;

/// <summary>How a level ended, as far as the career reads it (<c>EGameState</c>).</summary>
public enum EndLevelState
{
    Lost,
    Won,
    Other,
}

/// <summary>A secondary objective's state (<c>EMissionObjectiveState</c>).</summary>
public enum ObjectiveState
{
    NotDefined = 0,
    Complete = 1,
    Failed = 2,
}

/// <summary>
/// <c>CEndLevelData</c>, the record the game fills when a level ends and the career and debriefing read. The parity
/// core supplies it; the arrays have the game's sizes (288 base things, 5 kill counters, 32 slot words, 10 secondary
/// objectives).
/// </summary>
public sealed class EndLevelData
{
    public const int BaseThingsCount = 288;
    public const int SecondaryObjectiveCount = 10;

    /// <summary>The values <c>CEndLevelData</c>'s constructor sets: level 100, lost, ranking -1, no kills.</summary>
    public EndLevelData()
    {
    }

    public int WorldFinished { get; set; } = 100;
    public EndLevelState FinalState { get; set; } = EndLevelState.Lost;

    /// <summary>The level's ranking, from 0 to 1.</summary>
    public float Ranking { get; set; } = -1f;

    public int[] ThingsKilled { get; } = new int[CareerRecord.KillTypeCount];
    public int[] Slots { get; } = new int[CareerRecord.SlotWordCount];

    /// <summary>Per base thing, the game's BOOL that it is still alive (1 for alive; the career sets a bit only for exactly 1).</summary>
    public int[] BaseThingsLeft { get; } = new int[BaseThingsCount];

    public ObjectiveState[] SecondaryObjectives { get; } = new ObjectiveState[SecondaryObjectiveCount];

    public ObjectiveState[] PrimaryObjectives { get; } = new ObjectiveState[SecondaryObjectiveCount];

    /// <summary>Each primary objective's string id (CMissionObjective::mStringId; -1 when not defined).</summary>
    public int[] PrimaryStringIds { get; } = Enumerable.Repeat(-1, SecondaryObjectiveCount).ToArray();

    /// <summary>Each secondary objective's string id (CMissionObjective::mStringId; -1 when not defined).</summary>
    public int[] SecondaryStringIds { get; } = Enumerable.Repeat(-1, SecondaryObjectiveCount).ToArray();

    /// <summary>mFinalState as the game stored it (EGameState: GAME_STATE_LEVEL_LOST, 4, until a level fills it).</summary>
    public int FinalGameState { get; set; } = 4;

    /// <summary>The record's size in retail (0x5cc).</summary>
    public const int ByteSize = (BaseThingsCount * 4) + (2 * SecondaryObjectiveCount * 8) + (6 * 4) + (CareerRecord.KillTypeCount * 4)
        + (CareerRecord.SlotWordCount * 4);

    /// <summary>
    /// END_LEVEL_DATA's bytes as retail lays them out: mBaseThingsLeft[288], mPrimaryObjectives[10] and
    /// mSecondaryObjectives[10] (state, string id), mWorldFinished, mFinalState, mRanking, mScore, mTimeTaken,
    /// mLevelLostReason, mThingsKilled[5], mSlots[32]; little-endian.
    /// </summary>
    public byte[] ToBytes()
    {
        var b = new byte[ByteSize];
        int o = 0;
        void I(int v)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o), v);
            o += 4;
        }

        foreach (int v in BaseThingsLeft)
        {
            I(v);
        }

        for (int i = 0; i < SecondaryObjectiveCount; i++)
        {
            I((int)PrimaryObjectives[i]);
            I(PrimaryStringIds[i]);
        }

        for (int i = 0; i < SecondaryObjectiveCount; i++)
        {
            I((int)SecondaryObjectives[i]);
            I(SecondaryStringIds[i]);
        }

        I(WorldFinished);
        I(FinalGameState);
        I(BitConverter.SingleToInt32Bits(Ranking));
        I(Score);
        I(BitConverter.SingleToInt32Bits(TimeTaken));
        I(LevelLostReason);
        foreach (int v in ThingsKilled)
        {
            I(v);
        }

        foreach (int v in Slots)
        {
            I(v);
        }

        return b;
    }

    public int Score { get; set; }

    /// <summary>The event time the level ended at.</summary>
    public float TimeTaken { get; set; }

    /// <summary>The text id DeclareLevelLost was given.</summary>
    public int LevelLostReason { get; set; }

    /// <summary>
    /// <c>CEndLevelData::IsAllSecondaryObjectivesComplete</c> (0x004496e0): at least one secondary objective must be
    /// complete or failed, and none failed.
    /// </summary>
    public bool IsAllSecondaryObjectivesComplete()
    {
        bool result = true;
        bool any = false;
        foreach (ObjectiveState state in SecondaryObjectives)
        {
            if (state is ObjectiveState.Complete or ObjectiveState.Failed) any = true;
            if (state == ObjectiveState.Failed) result = false;
        }
        return any && result;
    }
}
