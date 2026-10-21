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
/// menu's Quit; the debriefing) against its replay's reference (fe-lose-port-20261021, the port with patches 0029 and
/// 0030). Its saved career and page trace come from the earlier fe-lose-port run: on the reference build the game's fclose
/// never closed a file, so the earlier save's stream overwrote the debriefing save at exit and dropped career slots 63 and
/// 64 that the game wrote at the debriefing (the reference's README.json).
/// </summary>
public sealed class FrontEndLossTests(ITestOutputHelper output)
{
    /// <summary>The Present through which rand() matches the port every Present (never lowered).</summary>
    private const int CrtMatchedThrough = 7300;

    [RetailGoldenFact("portrefs/tapes/fe-level100-lose-quit-7300f.tape", "portrefs/fe-lose-port-20261021/crt-per-present.txt",
        "portrefs/fe-lose-port-20261021/frontend-trace.txt", "portrefs/fe-lose-port-20261021/port-probes-every1.txt", "portrefs/fe-lose-port-20261021/BEA 1.bes")]
    public void TheFrontEndLossRunMatchesThePort()
    {
        byte[]? portEld = null;
        foreach (string line in File.ReadLines(RetailGolden.File("portrefs/fe-lose-port-20261021/port-probes-every1.txt")!))
        {
            if (line.Contains(" ELD ", StringComparison.Ordinal))
            {
                portEld = Convert.FromHexString(line.Split(' ')[3]);
            }
        }

        FrontEndRouteTests.RouteResult r = FrontEndRouteTests.Run("portrefs/tapes/fe-level100-lose-quit-7300f.tape", "portrefs/fe-lose-port-20261021", 7300);
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.True(r.CrtMatchedThrough >= CrtMatchedThrough, $"matched through Present {r.CrtMatchedThrough}, below {CrtMatchedThrough}");
        Assert.Equal(QuitType.UserQuitToFrontEnd, r.Run.Level!.Game.Quit);
        Assert.Equal(portEld, r.Run.FrontEnd.EndLevelData.ToBytes());
        Assert.Equal(File.ReadAllBytes(RetailGolden.File("portrefs/fe-lose-port-20261021/BEA 1.bes")!), r.Run.FrontEnd.MemoryCard.Folder.Read("BEA 1"));
    }
}
