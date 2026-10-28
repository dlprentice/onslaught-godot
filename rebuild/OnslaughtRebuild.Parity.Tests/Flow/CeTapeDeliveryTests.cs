// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class CeTapeDeliveryTests
{
    private sealed class StopRun : Exception;

    [GameDataFact]
    public void FullRunDeliversWindowAndCharacterEventsAtTheRecordedPresent()
    {
        RetailTape tape = RetailTape.Parse(Encoding.ASCII.GetBytes("# onslaught tape 2\nW_CLIENT 640 480\n"
            + "W 1 move 100 200 0\nK 1 down 41 1e a\nC 1 61 1e\n"));
        var run = new GameRun(GameData.Read, null, new RetailChainTests.InstalledFilms(), skipFmv: true) { Tape = tape };
        Assert.Same(run.FrontEnd.NormalFont, Assert.IsType<VirtualKeyboardPage>(run.FrontEnd.VirtualKeyboard).Font);
        var trapped = new List<(int, int)>();
        run.Input.KeyTrap = (key, e) => { trapped.Add((key, e)); return false; };
        run.Input.Mouse.SystemCursor = (99, 99);
        run.OnPresented = () =>
        {
            Assert.Equal(1, run.Clock.PresentCount);
            Assert.Equal((100, 200), run.Input.Mouse.SystemCursor);
            run.Input.Process(run.Clock.QueryPerformanceCounter());
            Assert.Equal((100, 200), (run.Input.Mouse.X, run.Input.Mouse.Y));
            Assert.Equal([(0x1e, KeyTrapEvent.Down), (0x61, KeyTrapEvent.Char)], trapped);
            throw new StopRun();
        };
        Assert.Throws<StopRun>(() => run.Run());
    }
}
