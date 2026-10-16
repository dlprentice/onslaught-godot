// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>The in-game pause menu (CPauseMenu's game form) in level 100, driven by keys. No port reference yet.</summary>
public sealed class PauseMenuTests(ITestOutputHelper output)
{
    private static RetailTape Keys(params (int Present, string Vk, string Scan)[] keys)
    {
        var text = new StringBuilder();
        foreach ((int p, string vk, string scan) in keys)
        {
            text.Append($"K {p} down {vk} {scan} vk{vk}\nK {p + 1} up {vk} {scan} vk{vk}\n");
        }

        return RetailTape.Parse(Encoding.ASCII.GetBytes(text.ToString()));
    }

    /// <summary>
    /// Escape opens the menu (paused, on Continue); after its 0.4 s lock, Down ×7 reaches Quit (Continue, Message Log,
    /// Briefing, the controls, sound and display entries, Retry, Quit), Return raises the confirmation on No, Down moves to
    /// Yes, and Return quits the level to the front end.
    /// </summary>
    [GameDataFact]
    public void EscapeThenQuitAndYesQuitsTheLevel()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Keys((400, "1b", "01"), (440, "28", "150"), (450, "28", "150"), (460, "28", "150"), (470, "28", "150"),
            (480, "28", "150"), (490, "28", "150"), (500, "28", "150"), (540, "0d", "1c"), (580, "28", "150"), (620, "0d", "1c"));
        long opened = 0;
        while (!session.Ended && session.PresentCount < 1000)
        {
            session.AdvanceToNextPresent();
            if (opened == 0 && session.Input.PauseMenu!.Active)
            {
                opened = session.PresentCount;
                Assert.True(session.Game.Paused);
            }
        }

        var menu = session.Input.PauseMenu!;
        output.WriteLine($"menu opened after P{opened}; ended after P{session.PresentCount}; quit {session.Game.Quit}");
        Assert.Equal(401, opened);
        Assert.Equal(7, menu.Lists[0].Selected);
        Assert.Equal(1, menu.Confirm!.List.Selected);
        Assert.True(session.Ended);
        Assert.Equal(QuitType.UserQuitToFrontEnd, session.Game.Quit);
    }

    /// <summary>
    /// The pause menu's Message Log: Escape, Down (Message Log), Return: the log opens on the newest message, taking the
    /// menu's controller; Down moves a card while the list runs past the panel; Escape goes back to the menu, and Escape
    /// again resumes the game.
    /// </summary>
    [GameDataFact]
    public void TheMessageLogOpensScrollsAndReturnsToTheMenu()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Keys((1200, "1b", "01"), (1240, "28", "150"), (1280, "0d", "1c"), (1320, "28", "150"), (1360, "26", "148"),
            (1400, "1b", "01"), (1440, "1b", "01"));
        var menu = () => session.Input.PauseMenu!;
        var log = () => session.Input.MessageLog!;
        var seen = new List<string>();
        string last = "";
        while (session.PresentCount < 1480)
        {
            session.AdvanceToNextPresent();
            string now = $"paused {session.Game.Paused} menu {menu().Active} log {log().Active} sel {log().Selected} top {log().Top}";
            if (now != last)
            {
                seen.Add($"P{session.PresentCount} {now}");
                last = now;
            }
        }

        foreach (string line in seen)
        {
            output.WriteLine(line);
        }

        output.WriteLine($"messages shown {session.Context.Scripts.Count} log count");
        Assert.Contains(seen, l => l.Contains("menu False log True"));
        Assert.False(session.Game.Paused);
        Assert.False(log().Active);
        Assert.False(menu().Active);
    }

    /// <summary>
    /// The pause menu's Briefing: Escape, Down ×2, Return: the briefing log opens (fade from 0.1 up to 1, a tenth each
    /// 60th of a second), taking the menu's controller; Escape goes back to the menu (the log fades away), and Escape again
    /// resumes the game.
    /// </summary>
    [GameDataFact]
    public void TheBriefingOpensFadesInAndReturnsToTheMenu()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Keys((1200, "1b", "01"), (1240, "28", "150"), (1250, "28", "150"), (1290, "0d", "1c"), (1400, "1b", "01"),
            (1460, "1b", "01"));
        var briefing = () => session.Input.BriefingLog!;
        float maxFade = 0;
        long opened = 0;
        while (session.PresentCount < 1500)
        {
            session.AdvanceToNextPresent();
            if (briefing().Active && opened == 0)
            {
                opened = session.PresentCount;
            }

            maxFade = Math.Max(maxFade, briefing().Fade);
        }

        output.WriteLine($"briefing opened after P{opened}; most fade {maxFade}; fade at the end {briefing().Fade}");
        Assert.Equal(1292, opened);
        Assert.Equal(1.0f, maxFade);
        Assert.False(briefing().Active);
        Assert.True(briefing().Fade <= 0);
        Assert.False(session.Game.Paused);
    }
}
