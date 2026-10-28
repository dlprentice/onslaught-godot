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
        var run = new GameRun(GameData.Read, null, new RetailChainTests.InstalledFilms(), skipFmv: true, fixedTape: tape);
        var events = Assert.IsAssignableFrom<IList<RetailTapeEvent>>(tape.Events);
        Assert.Throws<NotSupportedException>(() => events[0] = new RetailMouseEvent(1, 0, 0, 0, 0));
        Assert.Throws<InvalidOperationException>(() => run.Tape = null);
        RetailTape other = RetailTape.Parse(Encoding.ASCII.GetBytes("K 1 down 42 30 b\n"));
        Assert.Throws<InvalidOperationException>(() => run.Tape = other);
        Assert.Same(tape, run.Tape);
        Assert.Same(run.FrontEnd.NormalFont, Assert.IsType<VirtualKeyboardPage>(run.FrontEnd.VirtualKeyboard).Font);
        var trapped = new List<(int, int)>();
        run.Input.KeyTrap = (key, e) => { trapped.Add((key, e)); return false; };
        run.Input.Mouse.SystemCursor = (99, 99);
        run.BeforeTapeInput = () =>
        {
            Assert.Equal(1, run.Clock.PresentCount);
            Assert.Equal((0, 0), run.Input.Mouse.SystemCursor);
            Assert.Equal(0, run.Input.Pending);
        };
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
    public void StandaloneLevelDeliversEarlyInputAfterEachProbe()
    {
        var level = LevelSession.Load(100, GameData.Read);
        level.Tape = RetailTape.Parse(Encoding.ASCII.GetBytes("# onslaught tape 2\nW_CLIENT 640 480\n"
            + "W 1 move 100 200 0\nK 1 down 41 1e a\nC 1 61 1e\nM 1 7 -3 120 1\n"
            + "W 2 move 200 300 0\nK 2 up 41 1e a\n"));
        var samples = new List<(long Present, int X, int Y)>();
        var trapped = new List<(int, int)>();
        level.Input.Input.KeyTrap = (key, e) => { trapped.Add((key, e)); return false; };
        level.Clock.Presented = () =>
        {
            var (x, y) = level.Input.Input.Mouse.SystemCursor;
            samples.Add((level.Clock.PresentCount, x, y));
        };
        level.AdvanceToNextPresent();
        Assert.Equal([(1L, 0, 0), (2L, 100, 200), (3L, 200, 300)], samples);
        level.Input.Input.Process(level.Clock.QueryPerformanceCounter());
        Assert.Equal([(0x1e, KeyTrapEvent.Down), (0x61, KeyTrapEvent.Char), (0x1e, KeyTrapEvent.Up)], trapped);
        level.Input.Mouse.PollState();
        Assert.True(level.Input.Input.Mouse.Held[0]);
    }

    [GameDataFact]
    public void HostedLevelDoesNotDeliverTheGameRunsTapeTwice()
    {
        var builder = new StringBuilder("# onslaught tape 2\n");
        for (int p = 1; p <= 50; p++) builder.Append($"K {p} down 41 1e a\n");
        RetailTape tape = RetailTape.Parse(Encoding.ASCII.GetBytes(builder.ToString()));
        var run = new GameRun(GameData.Read, null, new RetailChainTests.InstalledFilms(), skipFmv: true) { Tape = tape };
        int downs = 0;
        run.Input.KeyTrap = (_, e) => { if (e == KeyTrapEvent.Down) downs++; return false; };
        run.Input.PrepareTape(tape);
        run.OnPresented = () =>
        {
            run.Input.Process(run.Clock.QueryPerformanceCounter());
            Assert.Equal(run.Clock.PresentCount, downs);
        };
        var host = new LevelHost(run.Clock, run.Input, run.Bindings, run.Shared, run.Crt.MainThread,
            run.FrontEnd.EndLevelData, LevelSession.SlotsOf(run.FrontEnd.Career), run.Loading, run.FrontEnd.Career.SetSlot)
            { Career = run.FrontEnd.Career };
        var level = LevelSession.Load(100, GameData.Read, host: host);
        Assert.Throws<InvalidOperationException>(() => level.Tape = tape);
        level.AdvanceToNextPresent();
        Assert.Null(level.Tape);
        Assert.Null(run.Clock.ReplayPresented);
        Assert.Equal(run.Clock.PresentCount, downs);
    }

    [GameDataFact]
    public void StandaloneRetryKeepsPresentDeliveryThroughThrottledAndRenderedLoads()
    {
        foreach (bool throttled in new[] { true, false })
        {
            var builder = new StringBuilder("# onslaught tape 2\nW_CLIENT 640 480\n");
            for (int p = 1; p <= 100; p++) builder.Append($"W {p} move {p} {p} 0\nK {p} down 41 1e a\n");
            var level = LevelSession.Load(100, GameData.Read);
            level.Tape = RetailTape.Parse(Encoding.ASCII.GetBytes(builder.ToString()));
            int downs = 0;
            level.Input.Input.KeyTrap = (_, e) => { if (e == KeyTrapEvent.Down) downs++; return false; };
            level.Clock.Presented = () =>
            {
                level.Input.Input.Process(level.Clock.QueryPerformanceCounter());
                Assert.Equal(level.Clock.PresentCount - 1, downs); // probe observes the preceding input, exactly once
            };
            level.AdvanceToNextPresent();
            level.Game.SetQuit(OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel);
            for (int i = 0; i < 60 && !level.Ended; i++) level.AdvanceToNextPresent();
            Assert.True(level.Ended);
            var loading = new LoadingScreen(level.Clock, () => level.Input.Input.Process(level.Clock.QueryPerformanceCounter()));
            if (throttled) loading.RenderedByBoot();
            typeof(LevelSession).GetProperty(nameof(LevelSession.Loading))!.SetValue(level, loading);
            long before = level.Clock.PresentCount;
            LevelSession retry = level.Restart();
            if (throttled) Assert.Equal(before, retry.PresentCount);
            else Assert.True(retry.PresentCount > before);
            RetailTape original = Assert.IsType<RetailTape>(retry.Tape);
            RetailTape replacement = RetailTape.Parse(Encoding.ASCII.GetBytes("K 1 down 42 30 b\n"));
            Assert.Throws<InvalidOperationException>(() => retry.Tape = replacement);
            Assert.Throws<InvalidOperationException>(() => retry.Tape = null);
            Assert.Same(original, retry.Tape);
            retry.AdvanceToNextPresent();
            Assert.True(retry.PresentCount > before);
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

        foreach (bool attachBeforePresent in new[] { true, false })
        {
            var atBoundary = LevelSession.Load(100, GameData.Read);
            if (attachBeforePresent) atBoundary.Tape = tape;
            atBoundary.Clock.PresentDevice();
            if (!attachBeforePresent) atBoundary.Tape = tape;
            var error = Assert.Throws<NotSupportedException>(() => atBoundary.AdvanceToNextPresent());
            Assert.Contains("already-passed", error.Message, StringComparison.Ordinal);
        }
    }
}
