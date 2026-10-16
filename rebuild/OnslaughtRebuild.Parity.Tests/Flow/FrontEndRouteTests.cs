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
        string? FirstCrtDifference, int? Returned)
    {
        /// <summary>With the port's front-end probes: the Presents whose EVT and FE lines were compared, and the first that differed.</summary>
        public long TimeCompared { get; set; }

        public string? FirstTimeDifference { get; set; }
    }

    /// <summary>
    /// Runs <paramref name="tape"/> from boot to <paramref name="end"/> (or until the front end quits to the system),
    /// with <paramref name="saves"/> on the memory card first, against the reference in <paramref name="dir"/>
    /// (crt-per-present.txt, frontend-trace.txt).
    /// </summary>
    internal static RouteResult Run(string tape, string dir, long end, params (string Name, string File)[] saves) =>
        Run(tape, dir, end, skipFmv: true, saves);

    internal static RouteResult Run(string tape, string dir, long end, bool skipFmv, params (string Name, string File)[] saves)
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
            new RetailChainTests.InstalledFilms(), skipFmv, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(tape)!),
        };
        foreach ((string name, string file) in saves)
        {
            run.FrontEnd.MemoryCard.Folder.Write(name, File.ReadAllBytes(RetailGolden.File(file)!));
        }

        // The port's EVT (EVENT_MANAGER's time) and FE line (gFrontEndFrameStep, the active page, the transition's pages,
        // count and time, mCounter) while the front end runs, when the reference has them.
        var portTime = new Dictionary<long, (string Evt, string Fe)>();
        string? timeFile = RetailGolden.File(dir + "/port-probes-every1-with-FE.txt");
        if (timeFile is not null && File.Exists(timeFile))
        {
            var evt = new Dictionary<long, string>();
            foreach (string line in File.ReadLines(timeFile))
            {
                string[] f = line.Split(' ');
                if (f.Length < 4 || f[0] != "P")
                {
                    continue;
                }

                long n = long.Parse(f[1], CultureInfo.InvariantCulture);
                if (f[2] == "EVT")
                {
                    evt[n] = f[3];
                }
                else if (f[2] == "FE")
                {
                    portTime[n] = (evt.GetValueOrDefault(n, ""), f[3]);
                }
            }
        }

        long timeCompared = 0;
        string? firstTime = null;
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

            if (firstTime is null && portTime.TryGetValue(p, out var t))
            {
                FrontEnd fe = run.FrontEnd;
                var b = new byte[28];
                BitConverter.TryWriteBytes(b.AsSpan(0), fe.Timing.FrameStep);
                BitConverter.TryWriteBytes(b.AsSpan(4), (int)fe.ActivePage);
                BitConverter.TryWriteBytes(b.AsSpan(8), (int)fe.TransitionFrom);
                BitConverter.TryWriteBytes(b.AsSpan(12), (int)fe.TransitionTo);
                BitConverter.TryWriteBytes(b.AsSpan(16), fe.TransitionCount);
                BitConverter.TryWriteBytes(b.AsSpan(20), fe.TransitionTime);
                BitConverter.TryWriteBytes(b.AsSpan(24), fe.Counter);
                string ours = Convert.ToHexString(b).ToLowerInvariant();
                string oursEvt = Convert.ToHexString(BitConverter.GetBytes(fe.Events.Time)).ToLowerInvariant();
                if (ours != t.Fe || oursEvt != t.Evt)
                {
                    firstTime = $"Present {p}: EVT {t.Evt}/{oursEvt} FE {t.Fe}/{ours} (port/ours)";
                }
                else
                {
                    timeCompared = p;
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

        return new RouteResult(run, portPages, pages, matched, first, returned) { TimeCompared = timeCompared, FirstTimeDifference = firstTime };
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
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; time (EVT, FE) matches through {r.TimeCompared}: {r.FirstTimeDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Equal(SystemFlow.QuitToSystem, r.Returned);
        Assert.Equal(680, r.Run.Clock.PresentCount);
        if (RetailGolden.File("portrefs/fe-r1-quit-port/port-probes-every1-with-FE.txt") is { } time && File.Exists(time))
        {
            // EVENT_MANAGER's time and the front end's frame step, page, transition and counter, every Present.
            Assert.Null(r.FirstTimeDifference);
            Assert.Equal(680, r.TimeCompared);
        }
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(680, r.CrtMatchedThrough);
    }

    /// <summary>
    /// R2 (fe-r2-idle-port): no input. The title's 30 seconds idle end the front end (CFrontEnd::Shutdown and the logo film
    /// at Present 904), and under -skipfmv the film never ends (BinkOpen is skipped, so its last frame never comes; retail's
    /// idle -skipfmv front end hangs there, B's measurement): no front end again by 3500, the reference's end; rand()
    /// untouched throughout.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r2-idle-port/empty.tape", "portrefs/fe-r2-idle-port/crt-per-present.txt",
        "portrefs/fe-r2-idle-port/frontend-trace.txt")]
    public void IdleOnTheTitleMatchesThePort()
    {
        RouteResult r = Run("portrefs/fe-r2-idle-port/empty.tape", "portrefs/fe-r2-idle-port", 3500);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; time (EVT, FE) matches through {r.TimeCompared}: {r.FirstTimeDifference ?? "none"}; flow {string.Join(" | ", r.Run.Flow!.Trace)}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(3500, r.CrtMatchedThrough);
        Assert.Equal(["front end Start", "attract"], r.Run.Flow.Trace);
    }

    /// <summary>
    /// R3 (fe-r3-load-port, the port's final run with its save-file fix): with fe-lose's career save 'BEA 1' on the card,
    /// the title, the main menu's Load, the device step, the load, then the briefing: SetPage at the port's Presents (the
    /// load and the level select at 701, the briefing at 1201), rand() every Present to 1600, and the career after
    /// CCareer::Load equal to the port's 9,404 bytes.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r3-load-port/fer3b.tape", "portrefs/fe-r3-load-port/crt-per-present.txt",
        "portrefs/fe-r3-load-port/frontend-trace.txt", "portrefs/fe-r3-load-port/BEA 1.bes",
        "portrefs/fe-r3-load-port/frontend-trace.txt.career-after-load-P701.bin")]
    public void LoadingASavedCareerMatchesThePort()
    {
        byte[] afterLoad;
        RouteResult r = Run("portrefs/fe-r3-load-port/fer3b.tape", "portrefs/fe-r3-load-port", 1600, ("BEA 1", "portrefs/fe-r3-load-port/BEA 1.bes"));
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; time (EVT, FE) matches through {r.TimeCompared}: {r.FirstTimeDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(1600, r.CrtMatchedThrough);
        afterLoad = r.Run.FrontEnd.Career.Record.Bytes.ToArray();
        Assert.Equal(File.ReadAllBytes(RetailGolden.File("portrefs/fe-r3-load-port/frontend-trace.txt.career-after-load-P701.bin")!), afterLoad);
    }

    /// <summary>
    /// R4 (fe-r4-options-port): the main menu's Options (the options page's CPauseMenu), its sound list, the music volume
    /// three steps down (immediate: the career's volume follows), Apply, Back twice (the menu closes; the page saves
    /// defaultoptions.bea and returns to the main page at 1221), then the main menu's Quit. SetPage at the port's
    /// Presents, rand() every Present to the exit after 1500 (each move, select and back is a sound's draw), and
    /// defaultoptions.bea equal to the port's 10,004 bytes.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r4-options-port/feopt.tape", "portrefs/fe-r4-options-port/crt-per-present.txt",
        "portrefs/fe-r4-options-port/frontend-trace.txt", "portrefs/fe-r4-options-port/defaultoptions.bea")]
    public void TheOptionsSoundVolumeMatchesThePort()
    {
        RouteResult r = Run("portrefs/fe-r4-options-port/feopt.tape", "portrefs/fe-r4-options-port", 3000);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; time (EVT, FE) matches through {r.TimeCompared}: {r.FirstTimeDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(1500, r.CrtMatchedThrough);
        Assert.Equal(SystemFlow.QuitToSystem, r.Returned);
        Assert.Equal(File.ReadAllBytes(RetailGolden.File("portrefs/fe-r4-options-port/defaultoptions.bea")!), r.Run.DefaultOptionsFile);
    }

    /// <summary>
    /// R5 (fe-r5-fullboot-idle-port, no -skipfmv): the boot films (logo, opening), the front end at 2288 with no loading
    /// Present before its title (the boot's last Present was the loading screen's), the title's idle, the attract films,
    /// and the front end again from 5474 (its Init's loading screen renders: the title at 5475). SetPage at the port's
    /// Presents and rand() every Present to 5700.
    /// </summary>
    [RetailGoldenFact("portrefs/fe-r5-fullboot-idle-port/empty.tape", "portrefs/fe-r5-fullboot-idle-port/crt-per-present.txt",
        "portrefs/fe-r5-fullboot-idle-port/frontend-trace.txt")]
    public void TheFullBootIdleMatchesThePort()
    {
        RouteResult r = Run("portrefs/fe-r5-fullboot-idle-port/empty.tape", "portrefs/fe-r5-fullboot-idle-port", 5700, skipFmv: false);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}; time (EVT, FE) matches through {r.TimeCompared}: {r.FirstTimeDifference ?? "none"}; flow {string.Join(" | ", r.Run.Flow!.Trace)}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.Null(r.FirstCrtDifference);
        Assert.Equal(5700, r.CrtMatchedThrough);
        Assert.Equal(["front end Start", "attract", "front end FromAttract"], r.Run.Flow.Trace);
    }
}
