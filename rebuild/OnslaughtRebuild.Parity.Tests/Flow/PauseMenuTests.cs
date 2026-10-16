// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow.Menus;
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

    /// <summary>
    /// The options' controls and display lists, on the port harness's machine (MachineDisplay's defaults): Escape, Down ×3,
    /// Return opens the controls on the mouse sensitivity (7 reads as step 2); Down reaches the preset, and Down again
    /// skips the bindings item and the four joysticks (none, once drawn) to Back, whose Return goes back to the first
    /// page (on Continue). Down ×5 and Return open the display; Down reaches the detail preset, Right opens its list a choice on and
    /// Return chooses it, which sets the other items' choices (not their settings: those wait for Apply); Escape goes back
    /// and Escape again resumes the game.
    /// </summary>
    [GameDataFact]
    public void TheControlsAndDisplayListsOpenAndReturn()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Keys((400, "1b", "01"), (440, "28", "150"), (450, "28", "150"), (460, "28", "150"), (480, "0d", "1c"),
            (520, "28", "150"), (540, "28", "150"), (560, "0d", "1c"), (600, "28", "150"), (610, "28", "150"), (620, "28", "150"),
            (630, "28", "150"), (640, "28", "150"), (660, "0d", "1c"), (700, "28", "150"), (720, "27", "14d"), (740, "0d", "1c"),
            (780, "1b", "01"), (820, "1b", "01"));
        var menu = () => session.Input.PauseMenu!;
        var seen = new List<string>();
        string last = "";
        var detailSeen = new List<string>();
        while (session.PresentCount < 860)
        {
            session.AdvanceToNextPresent();
            PauseMenu m = menu();
            string now = $"paused {session.Game.Paused} menu {m.Active} page {m.Page} sel {m.Lists[m.Page].Selected}";
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

        PauseMenu final = menu();
        MenuOptionsList controls = final.Lists[1];
        MenuOptionsList display = final.Lists[2];
        var sensitivity = (MouseSensitivityMenuItem)controls.Items[0];
        var detail = (VideoDetailLevelMenuItem)display.Items[1];
        output.WriteLine($"sensitivity step {sensitivity.Value}; joysticks enabled {string.Join(",", controls.Items.OfType<JoystickEnableMenuItem>().Select(j => j.Enabled))}");
        output.WriteLine($"detail value {detail.Value} selection {detail.Selection}; resolution {detail.Resolution.Selection}/{detail.Resolution.Value} shadows {detail.ShadowDetail.Selection}/{detail.ShadowDetail.Value} trees {detail.TreeDetail.Selection}/{detail.TreeDetail.Value} multisample choices {detail.MultiSample.GetNumChoices()} 32-bit choices {detail.Bits32.GetNumChoices()}");
        Assert.Equal(2, sensitivity.Value);
        Assert.Contains(seen, l => l.Contains("page 1 sel 7"));
        Assert.Contains(seen, l => l.Contains("page 2 sel 1"));
        Assert.All(controls.Items.OfType<JoystickEnableMenuItem>(), j => Assert.False(j.Enabled));
        Assert.Equal(1, detail.Value);
        Assert.Equal(0, detail.ShadowDetail.Selection);
        Assert.Equal(2, detail.ShadowDetail.Value);
        Assert.Equal(4, detail.MultiSample.GetNumChoices());
        Assert.Equal(3, detail.Bits32.GetNumChoices());
        Assert.False(session.Game.Paused);
        Assert.False(final.Active);
    }
}
