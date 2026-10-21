// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// A run stepped from another thread with random host-side delays (<see cref="SteppedGameRun"/>, the live Host's driver)
/// against the same run straight through: every Present's snapshot line equal, for the god route (5,200 Presents, the
/// front end, a career load, level 100 and its pause menu) and the front-end loss (7,300: a new career, level 100 lost,
/// the level-end menu, the debriefing).
/// </summary>
public sealed class SteppedGameRunTests(ITestOutputHelper output)
{
    private sealed class StopRun : Exception;

    private static GameRun NewRun(string tape, params (string Name, string File)[] saves)
    {
        var run = new GameRun(GameData.Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(tape)!),
        };
        foreach ((string name, string file) in saves)
        {
            run.FrontEnd.MemoryCard.Folder.Write(name, File.ReadAllBytes(RetailGolden.File(file)!));
        }

        return run;
    }

    private static List<string> Straight(GameRun run, long end)
    {
        var lines = new List<string>();
        run.OnPresented = () =>
        {
            lines.Add(FlowSnapshot.Capture(run).Line());
            if (run.Clock.PresentCount >= end)
            {
                throw new StopRun();
            }
        };
        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }

        return lines;
    }

    private static List<string> Stepped(GameRun run, long end, int seed)
    {
        var lines = new List<string>();
        var random = new Random(seed);
        using var stepped = new SteppedGameRun(run);
        while (stepped.Step() is { } snapshot)
        {
            lines.Add(snapshot.Line());
            // The host's own frame work, of any length, between Presents.
            switch (random.Next(4))
            {
                case 0: Thread.Sleep(random.Next(2)); break;
                case 1: Thread.SpinWait(random.Next(5000)); break;
                case 2: Thread.Yield(); break;
            }

            if (snapshot.Present >= end)
            {
                break;
            }
        }

        Assert.Null(stepped.Failure);
        return lines;
    }

    [RetailGoldenFact("portrefs/fe-god-port-20261021/fe-god-route.tape", "portrefs/fe-god-port-20261021/Maladim.bes",
        "portrefs/tapes/fe-level100-lose-quit-7300f.tape")]
    public void ASteppedRunWithRandomHostDelaysMakesTheSamePresentsAsAStraightRun()
    {
        const string god = "portrefs/fe-god-port-20261021";
        (string Name, string File) maladim = ("Maladim", god + "/Maladim.bes");
        List<string> a = Straight(NewRun(god + "/fe-god-route.tape", maladim), 5200);
        List<string> b = Stepped(NewRun(god + "/fe-god-route.tape", maladim), 5200, 20261021);
        List<string> c = Straight(NewRun("portrefs/tapes/fe-level100-lose-quit-7300f.tape"), 7300);
        List<string> d = Stepped(NewRun("portrefs/tapes/fe-level100-lose-quit-7300f.tape"), 7300, 7);
        output.WriteLine($"god route {a.Count} Presents ({a.Count(l => l.Contains(" Level "))} in the level); loss {c.Count} Presents");
        Assert.Equal(a, b);
        Assert.Equal(c, d);
        Assert.True(a.Count + c.Count >= 12_000);
        Assert.Contains(a, l => l.Contains(" FrontEnd ")) ;
        Assert.Contains(a, l => l.Contains(" Level "));
    }
}
