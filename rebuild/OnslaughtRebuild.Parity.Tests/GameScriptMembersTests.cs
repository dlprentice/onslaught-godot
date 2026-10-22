// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>The CGame members the mission scripts call: slots, objectives, score, and declaring the level's end.</summary>
public sealed class GameScriptMembersTests
{
    private static ParityGame Started(int[]? careerSlots = null)
    {
        var game = new ParityGame(new PlatformClock(), new EventManager()) { CareerSlots = careerSlots, CurrentLevel = 100 };
        game.InitRestartLoop();
        return game;
    }

    [Fact]
    public void SlotsAreBitsOfTheCareersWordsWithinTheFirst256()
    {
        ParityGame wiped = Started();
        Assert.False(wiped.GetSlot(63)); // a wiped career: LevelScript's INIT takes its no-slot branch

        int[] career = new int[ParityGame.MaxCareerSlots];
        career[1] = 1 << 31; // slot 63 = word 1, bit 31
        ParityGame game = Started(career);
        Assert.True(game.GetSlot(63));
        game.SetSlot(63, false);
        Assert.False(game.GetSlot(63));
        Assert.Equal(1 << 31, career[1]); // the career's own array is not touched
        game.SetSlot(0, true);
        Assert.Equal(1, game.Slots[0]);
        game.SetSlot(256, true); // outside 0..255: logged, nothing changes
        Assert.False(game.GetSlot(256));
        Assert.Equal(0, game.Slots[8]);
        Assert.False(game.GetSlot(-1));
    }

    [Fact]
    public void ObjectivesAndScoreResetWhenTheLevelStarts()
    {
        ParityGame game = Started();
        game.SetPrimaryObjectiveComplete(2, 77);
        game.SetSecondaryObjectiveFailed(9, 5);
        game.IncScore(150);
        Assert.Equal(MissionObjectiveState.Complete, game.PrimaryObjectives[2].State);
        Assert.Equal(150, game.Score);
        game.InitRestartLoop();
        Assert.Equal(MissionObjectiveState.NotDefined, game.PrimaryObjectives[2].State);
        Assert.Equal(-1, game.SecondaryObjectives[9].StringId);
        Assert.Equal(0, game.Score);
        Assert.True(game.HasHelpTextDisplay);
    }

    [Fact]
    public void WinningPausesWithTheWinCountUnlessTheLevelIsAlreadyOver()
    {
        ParityGame game = Started();
        game.PreRun();
        Assert.Equal(GameState.Panning, game.State);
        game.DeclareLevelWon();
        Assert.Equal(GameState.LevelWon, game.State);
        Assert.Equal(ParityGame.GameCountWhenWon, game.EndLevelCount);
        Assert.True(game.Paused);
        game.DeclareLevelLost(1234);
        Assert.Equal(GameState.LevelWon, game.State); // already over, but the reason is kept
        Assert.Equal(1234, game.LevelLostReason);
    }

    [Fact]
    public void APlayersDeathPausesFifteenSecondsLater()
    {
        ParityGame game = Started();
        game.PreRun();
        int before = game.Events.TotalEvents;
        game.DeclareLevelLost(0, playerDied: true);
        Assert.Equal(GameState.LevelLost, game.State);
        Assert.Equal(ParityGame.GameCountWhenLostOrDraw, game.EndLevelCount);
        Assert.False(game.Paused);
        Assert.Equal(before + 2, game.Events.TotalEvents);
    }

    [Fact]
    public void LosingDuringThePreRunStillPauses()
    {
        ParityGame game = Started();
        game.DeclareLevelLost();
        Assert.Equal(GameState.LevelLost, game.State);
        Assert.True(game.Paused); // Pause tests the state after DeclareLevelLost set LEVEL_LOST, which is past PRE_RUNNING
    }
}
