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
}
