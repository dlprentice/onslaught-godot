// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>CGame::FillOutEndLevelData on a running level 100 (no retail end-of-level capture exists yet).</summary>
public sealed class EndLevelDataTests(ITestOutputHelper output)
{
    [GameDataFact]
    public void Level100sEndDataNamesItsBaseThingsAliveItsLevelAndItsTime()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 200)
        {
            session.AdvanceToNextPresent();
        }

        var end = new EndLevelData();
        int score = session.Game.Score;
        session.Game.FillOutEndLevelData(end, session.Context.World);
        int bases = session.Context.World.BaseWorldThingNB.Count;
        output.WriteLine($"base things {bases}, alive {end.BaseThingsLeft.Count(b => b == 1)}, ranking {end.Ranking}, score {end.Score}, time {end.TimeTaken}");

        Assert.Equal(100, end.WorldFinished);
        Assert.Equal(EndLevelState.Other, end.FinalState);
        Assert.Equal(session.Context.Events.Time, end.TimeTaken);
        Assert.Equal(score, end.Score);
        Assert.All(end.ThingsKilled, k => Assert.Equal(0, k));
        Assert.Equal(bases, end.BaseThingsLeft.Take(bases).Count(b => b == 1));
        Assert.All(end.BaseThingsLeft.Skip(bases), b => Assert.Equal(0, b));
    }

    /// <summary>
    /// A loss declared in play (the script's LevelLostString) keeps its reason, pauses, and the level-end menu comes up
    /// once the loss count has run; the end data then names the loss. The menu's choice (retry, quit) is not ported, so
    /// the level does not end here.
    /// </summary>
    [GameDataFact]
    public void Level100LostKeepsItsReasonAndFillsTheEndDataAsLost()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        while (session.PresentCount < 200)
        {
            session.AdvanceToNextPresent();
        }

        session.Game.DeclareLevelLost(0x422e330f);
        while (session.PresentCount < 400)
        {
            session.AdvanceToNextPresent();
        }

        var end = new EndLevelData();
        session.Game.FillOutEndLevelData(end, session.Context.World);
        output.WriteLine($"state {session.Game.State}, paused {session.Game.Paused}, quit {session.Game.Quit}, ended {session.Ended}");
        Assert.Equal(GameState.LevelLost, session.Game.State);
        Assert.True(session.Game.Paused);
        Assert.False(session.Ended);
        Assert.Equal(EndLevelState.Lost, end.FinalState);
        Assert.Equal(0x422e330f, end.LevelLostReason);
    }
}
