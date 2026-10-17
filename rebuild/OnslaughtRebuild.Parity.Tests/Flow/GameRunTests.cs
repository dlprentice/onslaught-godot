// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>The game from boot to the front end's return after a level, on one clock (<see cref="GameRun"/>).</summary>
public sealed class GameRunTests(ITestOutputHelper output)
{
    private sealed class StopRun : Exception;

    private sealed class ObservedFilms(Func<long> present) : IFilmFiles
    {
        private readonly RetailChainTests.InstalledFilms _files = new();
        public List<(string Name, long Present)> Opens { get; } = [];

        public FilmHeader? Probe(string name)
        {
            Opens.Add((name, present()));
            return _files.Probe(name);
        }
    }

    [GameDataFact]
    public void TheDevicePresentEstablishesTheClockBeforeTheBootFilms()
    {
        GameRun? run = null;
        var films = new ObservedFilms(() => run!.Clock.PresentCount);
        run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            films, skipFmv: false);
        var clockSamples = new List<uint>();
        run.OnPresented = () =>
        {
            if (run.Clock.PresentCount >= 2)
            {
                clockSamples.Add(BitConverter.SingleToUInt32Bits(run.Clock.GetSysTimeFloat()));
            }

            if (run.Clock.PresentCount == 3)
            {
                throw new StopRun();
            }
        };

        Assert.Throws<StopRun>(() => run.Run());
        Assert.Equal([("ltlogo", 1L)], films.Opens);
        // Observed on two fresh port boots: first_seen = the device's Present-1 QPC. The next Presents are
        // 333,333 and 666,666 ticks later at 10 MHz, rounded once to 24 bits by GetSysTimeFloat.
        Assert.Equal([0x3d088880u, 0x3d888880u], clockSamples);
    }

    /// <summary>
    /// fe-explore4's clicks from the title to level 100 (the front end at retail's Presents, as
    /// RetailChainTests.TheTitleToLevel100ClickChainSetsEachPageAtRetailsPresent), then the level from the front end on
    /// the same clock and input; a loss declared in play, the level-end menu's Down and Return (Quit), and the front end
    /// entered again: CAREER.Update with the level's END_LEVEL_DATA, the autosave over the new career's save, then the
    /// debriefing.
    /// </summary>
    [GameDataFact]
    public void TheTitleToLevel100ThenALossAndQuitReturnsToTheDebriefing()
    {
        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), skipFmv: false, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            LastPresent = 20000,
        };
        var pages = new List<(long Present, FrontEndPage Page)>();
        run.FrontEnd.SetPageTrace = (page, time) => pages.Add((run.Clock.PresentCount, page));
        long levelStart = 0, lost = 0, menuUp = 0, down = 0, ended = 0;
        int keyStep = 0;
        run.OnPresented = () =>
        {
            long p = run.Clock.PresentCount;
            foreach (var t in RetailChainTests.ClickTape)
            {
                if (t.Present == p)
                {
                    run.Input.PostMouseMessage(t.Message, t.X, t.Y, t.WParam);
                }
            }

            if (run.Level is { Ended: false } level)
            {
                if (levelStart == 0)
                {
                    levelStart = p;
                }
                else if (lost == 0 && p >= levelStart + 300)
                {
                    lost = p;
                    level.Game.DeclareLevelLost(0x422e890f);
                }
                else if (lost != 0 && menuUp == 0 && level.Input.GameInterface!.MenuUp)
                {
                    menuUp = p;
                }
                else if (menuUp != 0 && keyStep < 4 && p >= menuUp + 10 + (keyStep * 5))
                {
                    bool isDown = keyStep < 2;
                    run.Input.PostTapeKey(isDown ? 0x28 : 0x0d, isDown ? 0x150 : 0x1c, keyStep % 2 == 0);
                    if (keyStep == 0)
                    {
                        down = p;
                    }

                    keyStep++;
                }
            }
            else if (run.Level is { Ended: true } && ended == 0)
            {
                ended = p;
            }

            if (pages.Count > 0 && pages[^1].Page == FrontEndPage.Debriefing)
            {
                throw new StopRun();
            }
        };

        Assert.Throws<StopRun>(() => run.Run());
        output.WriteLine($"level from P{levelStart}, lost P{lost}, menu P{menuUp}, Down P{down}, ended by P{ended}; quit {run.Level!.Game.Quit}");
        foreach (var (p, page) in pages)
        {
            output.WriteLine($"page {page} at P{p}");
        }

        Assert.Equal(QuitType.UserQuitToFrontEnd, run.Level.Game.Quit);
        Assert.Equal(EndLevelState.Lost, run.FrontEnd.EndLevelData.FinalState);
        Assert.Equal(0x422e890f, run.FrontEnd.EndLevelData.LevelLostReason);
        Assert.Equal([FrontEndPage.SaveGame, FrontEndPage.Debriefing], pages.SkipWhile(x => x.Page != FrontEndPage.BeConfig).Skip(1).Select(x => x.Page));
        byte[] saved = run.FrontEnd.MemoryCard.Folder.Read("BEA 1");
        Assert.Equal(CareerSaveGame.Size, saved.Length);
    }
}
