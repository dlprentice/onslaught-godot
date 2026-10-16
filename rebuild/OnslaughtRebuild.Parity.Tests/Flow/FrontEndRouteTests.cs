// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The game from boot against the port's front-end runs (a -skipfmv boot, keyboard only, no -level), on one clock
/// (<see cref="GameRun"/>): each SetPage at the port's Present, rand() every Present, and how the run ends.
/// </summary>
public sealed class FrontEndRouteTests(ITestOutputHelper output)
{
    private sealed class StopRun : Exception;

    /// <summary>What a route's run gave, against its port reference.</summary>
    internal sealed record RouteResult(GameRun Run, List<string> PortPages, List<string> Pages, long CrtMatchedThrough,
        string? FirstCrtDifference, int? Returned);

    /// <summary>
    /// Runs <paramref name="tape"/> from boot to <paramref name="end"/> (or until the front end quits to the system),
    /// with <paramref name="saves"/> on the memory card first, against the reference in <paramref name="dir"/>
    /// (crt-per-present.txt, frontend-trace.txt).
    /// </summary>
    internal static RouteResult Run(string tape, string dir, long end, params (string Name, string File)[] saves)
    {
        var port = new Dictionary<long, (uint Count, uint Hold)>();
        foreach (string line in File.ReadLines(RetailGolden.File(dir + "/crt-per-present.txt")!))
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
        foreach (string line in File.ReadLines(RetailGolden.File(dir + "/frontend-trace.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length >= 3 && f[1] == "CFrontEnd::SetPage")
            {
                portPages.Add($"{f[0]} {f[2]}");
            }
        }

        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), skipFmv: true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(tape)!),
        };
        foreach ((string name, string file) in saves)
        {
            run.FrontEnd.MemoryCard.Folder.Write(name, File.ReadAllBytes(RetailGolden.File(file)!));
        }

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

            if (p >= end)
            {
                throw new StopRun();
            }
        };

        int? returned = null;
        try
        {
            returned = run.Run();
        }
        catch (StopRun)
        {
        }

        return new RouteResult(run, portPages, pages, matched, first, returned);
    }

    private static string PageName(FrontEndPage page) => page switch
    {
        FrontEndPage.Intro => "FEP_INTRO",
        FrontEndPage.Main => "FEP_MAIN",
        FrontEndPage.VirtualKeyboard => "FEP_VIRTUAL_KEYBOARD",
        FrontEndPage.SaveGame => "FEP_SAVEGAME",
        FrontEndPage.LoadGame => "FEP_LOADGAME",
        FrontEndPage.LevelSelect => "FEP_LEVEL_SELECT",
        FrontEndPage.Briefing => "FEP_BRIEFING",
        FrontEndPage.BeConfig => "FEP_BE_CONFIG",
        FrontEndPage.Debriefing => "FEP_DEBRIEFING",
        FrontEndPage.Directory => "FEP_DIRECTORY",
        FrontEndPage.Options => "FEP_OPTIONS",
        _ => page.ToString(),
    };

    /// <summary>
    /// R1 (fe-r1-quit-port): the title, the main menu's Quit (Down ×5), its Yes/No box (Up to Yes, the move sound's
    /// draw), Return: the front end returns -1 and the game exits after Present 680, the port's last; rand() equal every
    /// Present.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r1-quit-port/fer1.tape", "portrefs/fe-r1-quit-port/crt-per-present.txt",
        "portrefs/fe-r1-quit-port/frontend-trace.txt")]
    public void TheMainMenusQuitMatchesThePort()
    {
        RouteResult r = Run("portrefs/fe-r1-quit-port/fer1.tape", "portrefs/fe-r1-quit-port", 2000);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Equal(SystemFlow.QuitToSystem, r.Returned);
        Assert.Equal(680, r.Run.Clock.PresentCount);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(680, r.CrtMatchedThrough);
    }

    /// <summary>
    /// R2 (fe-r2-idle-port): no input. The title's 30 seconds idle end the front end (the port's CFrontEnd::Shutdown and
    /// the logo film at Present 904); rand() is untouched. Compared to Present 3000: the port's film playback never ends
    /// under its replay, so the attract films' Presents come from retail's boot (RetailChainTests) instead.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r2-idle-port/empty.tape", "portrefs/fe-r2-idle-port/crt-per-present.txt",
        "portrefs/fe-r2-idle-port/frontend-trace.txt")]
    public void IdleOnTheTitleMatchesThePort()
    {
        RouteResult r = Run("portrefs/fe-r2-idle-port/empty.tape", "portrefs/fe-r2-idle-port", 3000);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; flow {string.Join(" | ", r.Run.Flow!.Trace)}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(3000, r.CrtMatchedThrough);
        Assert.Equal(["front end Start", "attract"], r.Run.Flow.Trace);
    }
}
