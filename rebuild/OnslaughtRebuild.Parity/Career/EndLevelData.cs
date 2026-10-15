// SPDX-License-Identifier: GPL-3.0-or-later
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
