// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The god-mode cheat from boot against the port (fe-god-port-20261021, replayed on the port with patches 0029 and
/// 0030): a career whose save name holds cheat 3's code (a byte copy of fe-r3's real save, renamed Maladim), loaded
/// through the main menu's Load as R3 does, then level select, briefing, configuration and level 100; in the level the
/// pause menu's Controller Options shows God OFF, which is selected (God ON), and the menus close (the pause menu's exit
/// saves the career). SetPage at the port's Presents, rand() every Present, the god flag on the player and the career,
/// and the saved career equal to the port's 10,004 bytes.
/// </summary>
public sealed class GodRouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which rand() matches the port every Present (never lowered).</summary>
    private const int CrtMatchedThrough = 5200;

    private const string Dir = "portrefs/fe-god-port-20261021";

    [RetailGoldenFact(Dir + "/fe-god-route.tape", Dir + "/crt-per-present.txt", Dir + "/frontend-trace.txt",
        Dir + "/Maladim.bes", Dir + "/Maladim-after.bes", Dir + "/frontend-trace.txt.career-after-load-P701.bin")]
    public void TheGodCheatFromALoadedCheatCareerMatchesThePort()
    {
        FrontEndRouteTests.RouteResult r = FrontEndRouteTests.Run(Dir + "/fe-god-route.tape", Dir, 5200, ("Maladim", Dir + "/Maladim.bes"));
        output.WriteLine($"rand() matches the port through Present {r.CrtMatchedThrough}; first difference: {r.FirstCrtDifference ?? "none"}");
        Assert.Equal(r.PortPages, r.Pages);
        Assert.True(r.CrtMatchedThrough >= CrtMatchedThrough, $"matched through Present {r.CrtMatchedThrough}, below {CrtMatchedThrough}");
        Player player = r.Run.Level!.Game.GetPlayer(0)!;
        Assert.True(player.IsGod);
        Assert.Equal(1, player.Stats[(int)PlayerStat.Cheated]);
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Dir + "/Maladim-after.bes")!), r.Run.FrontEnd.MemoryCard.Folder.Read("Maladim"));
    }
}
