// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The game from boot to the debriefing after a lost level 100 against the port, on one clock (<see cref="GameRun"/>):
/// the port's front-end loss tape (tapes/fe-level100-lose-quit-7300f.tape, a -skipfmv boot, keyboard only: the title,
/// a new career named and saved, level select, briefing, configuration, level 100; lose3's in-level part; the level-end
/// menu's Quit; the debriefing) against its replay's reference (fe-lose-port).
/// </summary>
public sealed class FrontEndLossTests(ITestOutputHelper output)
{
    /// <summary>The Present through which rand() matches the port every Present (never lowered).</summary>
    private const int CrtMatchedThrough = 4941;

    private sealed class StopRun : Exception;

    [RetailGoldenFact("portrefs/tapes/fe-level100-lose-quit-7300f.tape", "portrefs/fe-lose-port/crt-per-present.txt",
        "portrefs/fe-lose-port/frontend-trace.txt", "portrefs/fe-lose-port/port-probes-every1.txt", "portrefs/fe-lose-port/BEA 1.bes")]
    public void TheFrontEndLossRunMatchesThePort()
    {
        var port = new Dictionary<long, (uint Count, uint Hold)>();
        foreach (string line in File.ReadLines(RetailGolden.File("portrefs/fe-lose-port/crt-per-present.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length >= 4 && f[0] == "P" && f[2] == "CRT")
            {
                byte[] b = Convert.FromHexString(f[3]);
                port[long.Parse(f[1], CultureInfo.InvariantCulture)] = (BitConverter.ToUInt32(b, 0), BitConverter.ToUInt32(b, 4));
            }
        }

        // The port's SetPage breakpoints, at the Present in progress (completed Presents + 1).
        var portPages = new List<string>();
        foreach (string line in File.ReadLines(RetailGolden.File("portrefs/fe-lose-port/frontend-trace.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length >= 3 && f[1] == "CFrontEnd::SetPage")
            {
                portPages.Add($"{f[0]} {f[2]}");
            }
        }

        byte[]? portEld = null;
        foreach (string line in File.ReadLines(RetailGolden.File("portrefs/fe-lose-port/port-probes-every1.txt")!))
        {
            if (line.Contains(" ELD ", StringComparison.Ordinal))
            {
                portEld = Convert.FromHexString(line.Split(' ')[3]);
            }
        }

        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), skipFmv: true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File("portrefs/tapes/fe-level100-lose-quit-7300f.tape")!),
        };
        var pages = new List<string>();
        run.FrontEnd.SetPageTrace = (page, time) => pages.Add($"P{run.Clock.PresentCount + 1} page={PageName(page)}");
        long matched = 0;
        string? first = null;
        run.OnPresented = () =>
        {
            long p = run.Clock.PresentCount;
            if (first is null && port.TryGetValue(p, out var want))
            {
                var crt = run.Crt.MainThread;
                if ((uint)crt.Calls != want.Count || crt.HoldRand != want.Hold)
                {
                    first = $"Present {p}: rand() count {want.Count}/{crt.Calls}, holdrand {want.Hold:x8}/{crt.HoldRand:x8} (port/ours)";
                }
                else
                {
                    matched = p;
                }
            }

            if (p >= 7300)
            {
                throw new StopRun();
            }
        };

        Assert.Throws<StopRun>(() => run.Run());
        output.WriteLine($"rand() matches the port through Present {matched}; first difference: {first ?? "none"}");
        Assert.Equal(portPages, pages);
        Assert.True(matched >= CrtMatchedThrough, $"matched through Present {matched}, below {CrtMatchedThrough}");
        Assert.Equal(QuitType.UserQuitToFrontEnd, run.Level!.Game.Quit);
        Assert.Equal(portEld, run.FrontEnd.EndLevelData.ToBytes());
        Assert.Equal(File.ReadAllBytes(RetailGolden.File("portrefs/fe-lose-port/BEA 1.bes")!), run.FrontEnd.MemoryCard.Folder.Read("BEA 1"));
    }

    private static string PageName(FrontEndPage page) => page switch
    {
        FrontEndPage.Intro => "FEP_INTRO",
        FrontEndPage.Main => "FEP_MAIN",
        FrontEndPage.VirtualKeyboard => "FEP_VIRTUAL_KEYBOARD",
        FrontEndPage.SaveGame => "FEP_SAVEGAME",
        FrontEndPage.LevelSelect => "FEP_LEVEL_SELECT",
        FrontEndPage.Briefing => "FEP_BRIEFING",
        FrontEndPage.BeConfig => "FEP_BE_CONFIG",
        FrontEndPage.Debriefing => "FEP_DEBRIEFING",
        _ => page.ToString(),
    };
}
