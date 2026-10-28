// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Game;
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
            + "W 1 move 100 200 0\nK 1 down 41 1e a\nC 1 61 1e\nM 1 7 -3 120 1\n"));
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
            var level = new LevelInput(run.Clock, run.Bindings, run.Input, run.Shared);
            level.Mouse.Init(); level.Mouse.PollState();
            Assert.Equal((327, 237, 120), (run.Input.Mouse.X, run.Input.Mouse.Y, run.Input.Mouse.Wheel));
            Assert.True(run.Input.Mouse.Held[0]);
            throw new StopRun();
        };
        Assert.Throws<StopRun>(() => run.Run());
    }

    [GameDataFact]
    public void StandaloneLevelRefusesLegacyInputItsStartupWouldSkip()
    {
        foreach (int present in new[] { 1, 2 })
        {
            var level = LevelSession.Load(100, GameData.Read);
            level.Tape = RetailTape.Parse(Encoding.ASCII.GetBytes($"K {present} down 41 1e a\n"));
            var error = Assert.Throws<NotSupportedException>(() => level.AdvanceToNextPresent());
            Assert.Contains($"Present {present}", error.Message, StringComparison.Ordinal);
            Assert.Equal(error.Message, Assert.Throws<NotSupportedException>(() => level.AdvanceToNextPresent()).Message);
        }
    }

    [GameDataFact]
    public void TapeCannotBeAttachedAfterItsInputsHaveAlreadyPassed()
    {
        RetailTape tape = RetailTape.Parse(Encoding.ASCII.GetBytes("K 1 down 41 1e a\n"));
        var run = new GameRun(GameData.Read, null, new RetailChainTests.InstalledFilms(), skipFmv: true);
        run.Clock.PresentDevice();
        Assert.Throws<InvalidOperationException>(() => run.Tape = tape);
        Assert.Null(run.Tape);

        var started = LevelSession.Load(100, GameData.Read);
        started.AdvanceToNextPresent();
        Assert.Throws<InvalidOperationException>(() => started.Tape = tape);
        Assert.Null(started.Tape);

        var alreadyPast = LevelSession.Load(100, GameData.Read);
        alreadyPast.Tape = tape;
        alreadyPast.Clock.PresentDevice();
        alreadyPast.Clock.PresentDevice();
        var error = Assert.Throws<NotSupportedException>(() => alreadyPast.AdvanceToNextPresent());
        Assert.Contains("already-passed", error.Message, StringComparison.Ordinal);
    }
}
