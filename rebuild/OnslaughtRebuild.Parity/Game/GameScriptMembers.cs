// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/game.h and src/game.cpp (CGame: SetSlot 0046d3a0, GetSlot 0046d410, DeclareLevelWon
// 0046f2f0, DeclareLevelLost 0046f430, Pause 0046fb00; the objective, score and help-text accessors) and
// src/MissionObjective.h (CMissionObjective), in Stuart's release.

using OnslaughtRebuild.Parity.Events;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>EMissionObjectiveState.</summary>
public enum MissionObjectiveState
{
    NotDefined = 0,
    Complete,
    Failed,
}

/// <summary>CMissionObjective: a state and its text id.</summary>
public sealed class MissionObjective
{
    public MissionObjectiveState State = MissionObjectiveState.NotDefined;
    public int StringId = -1;

    public void Set(MissionObjectiveState state, int stringId)
    {
        State = state;
        StringId = stringId;
    }

    public bool IsComplete => State == MissionObjectiveState.Complete;
}

public sealed partial class Game
{
    /// <summary>MAX_CAREER_SLOTS: the slot words (GetSlot and SetSlot reach only the first eight: 256 bits).</summary>
    public const int MaxCareerSlots = 32;

    public const int MaxPrimaryObjectives = 10;
    public const int MaxSecondaryObjectives = 10;

    /// <summary>GAME_COUNT_WHEN_WON.</summary>
    public const float GameCountWhenWon = 5.0f;

    /// <summary>mSlots: the career's slot bits, copied at InitRestartLoop.</summary>
    public int[] Slots { get; } = new int[MaxCareerSlots];

    /// <summary>
    /// CAREER.GetSlots(): what InitRestartLoop copies into <see cref="Slots"/>. Null stands for a wiped career (CCareer
    /// zeroes its slots, Career.cpp:269), which is what a plain -level run plays with.
    /// </summary>
    public int[]? CareerSlots { get; set; }

    public MissionObjective[] PrimaryObjectives { get; } = NewObjectives(MaxPrimaryObjectives);

    public MissionObjective[] SecondaryObjectives { get; } = NewObjectives(MaxSecondaryObjectives);

    /// <summary>mScore.</summary>
    public int Score { get; private set; }

    /// <summary>mLevelLostReason: the text id DeclareLevelLost was given.</summary>
    public int LevelLostReason { get; private set; }

    /// <summary>mHelpTextDisplay != NULL (made in InitRestartLoop).</summary>
    public bool HasHelpTextDisplay { get; private set; }

    private static MissionObjective[] NewObjectives(int n)
    {
        var a = new MissionObjective[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = new MissionObjective();
        }

        return a;
    }

    /// <summary>The slot's bit mask: 1 &lt;&lt; (num &amp; 31), as retail builds it.</summary>
    private static int SlotMask(int num)
    {
        int b = num & 31;
        int m = 1;
        if (b > 0)
        {
            m <<= b;
        }

        return m;
    }

    /// <summary>CGame::SetSlot: outside 0..255 it logs and does nothing.</summary>
    public void SetSlot(int num, bool val)
    {
        if (num < 0 || num >= MaxCareerSlots * 8)
        {
            return; // LOG: "Error: Outside slot range (%d) in call to SetSlot"
        }

        int i = num >> 5;
        int m = SlotMask(num);
        if (val)
        {
            Slots[i] |= m;
        }
        else
        {
            Slots[i] &= ~m;
        }
    }

    /// <summary>CGame::GetSlot: outside 0..255 it logs and returns FALSE.</summary>
    public bool GetSlot(int num)
    {
        if (num < 0 || num >= MaxCareerSlots * 8)
        {
            return false; // LOG: "Error: Outside slot range (%d) in call to GetSlot"
        }

        return (Slots[num >> 5] & SlotMask(num)) != 0;
    }

    public void SetPrimaryObjectiveComplete(int num, int stringId) => PrimaryObjectives[num].Set(MissionObjectiveState.Complete, stringId);

    public void SetSecondaryObjectiveComplete(int num, int stringId) => SecondaryObjectives[num].Set(MissionObjectiveState.Complete, stringId);

    public void SetPrimaryObjectiveFailed(int num, int stringId) => PrimaryObjectives[num].Set(MissionObjectiveState.Failed, stringId);

    public void SetSecondaryObjectiveFailed(int num, int stringId) => SecondaryObjectives[num].Set(MissionObjectiveState.Failed, stringId);

    public void IncScore(int score) => Score += score;

    /// <summary>CGame::DeclareLevelWon: only while playing (or before); the win count, 0 for levels 741/742; then Pause.</summary>
    public void DeclareLevelWon()
    {
        if (State > GameState.Playing)
        {
            return;
        }

        // The controllers' rumble is turned off (not modelled).
        State = GameState.LevelWon;
        EndLevelCount = CurrentLevel != 741 && CurrentLevel != 742 ? GameCountWhenWon : 0.0f;
        Pause();
    }

    /// <summary>
    /// CGame::DeclareLevelLost: the reason is kept even when the level is already over; then the loss count, and Pause, or
    /// for a player's death PAUSE_GAME in 15 seconds and the sound fade next frame.
    /// </summary>
    public void DeclareLevelLost(int message = 0, bool playerDied = false)
    {
        LevelLostReason = message;
        if (State > GameState.Playing)
        {
            return;
        }

        State = GameState.LevelLost;
        EndLevelCount = GameCountWhenLostOrDraw;
        if (!playerDied)
        {
            Pause();
        }
        else
        {
            Events.AddEventIn(15.0f, (int)GameEvent.PauseGame, this);
            Events.AddEvent((int)GameEvent.ContinueFadeOutGameSounds, this, EventManager.NextFrame);
        }
    }

    /// <summary>CGame::Pause(FALSE, NULL): pauses once PRE_RUNNING is over (the pause menu path is the platform's).</summary>
    public void Pause()
    {
        if (State > GameState.PreRunning && !Paused)
        {
            Paused = true;
        }
    }
}
