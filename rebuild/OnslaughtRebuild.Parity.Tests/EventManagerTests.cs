// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;
using OnslaughtRebuild.Parity.Game;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>CEventManager's scheduling, order and ownership rules, and CGame's level start through them.</summary>
public sealed class EventManagerTests
{
    private sealed class Recorder : Monitor
    {
        private readonly List<string> _log;
        private readonly string _name;

        public Recorder(List<string> log, string name)
        {
            _log = log;
            _name = name;
        }

        public Action<ScheduledEvent>? OnEvent { get; set; }

        public override void HandleEvent(Event e)
        {
            _log.Add($"{_name}:{e.EventNum}");
            OnEvent?.Invoke((ScheduledEvent)e);
        }
    }

    private static EventManager Started()
    {
        var events = new EventManager();
        events.Init();
        return events;
    }

    [Fact]
    public void AnEventRunsOnTheTickItsBucketComesUp()
    {
        // FINISHED_PRE_RUN at 3.0 s from time 0 goes into buffer floor((3.0 - 0.001) * 20) = 59: tick 60 runs it.
        var log = new List<string>();
        EventManager events = Started();
        events.AddEventIn(3.0f, 1, new Recorder(log, "a"));
        Assert.Single(events.Buffer(59, EventPriority.StartOfFrame));
        int tick = 0;
        while (log.Count == 0)
        {
            events.Update();
            tick++;
        }

        Assert.Equal(60, tick);
        Assert.Equal(3.0f, events.Time);
    }

    [Fact]
    public void DueByTheNextTickMeansTheCurrentBufferAndNegativeMeansNextFrame()
    {
        var log = new List<string>();
        EventManager events = Started();
        var r = new Recorder(log, "r");
        events.AddEvent(1, r, 0.051f);              // time <= mTime + 0.051: the current buffer
        events.AddEvent(2, r, EventManager.NextFrame); // negative: mTime + 0.0001, the current buffer
        Assert.Equal(0.0001f, events.Buffer(0, EventPriority.StartOfFrame)[1].Time);
        events.Update();
        Assert.Equal(new[] { "r:1", "r:2" }, log);
    }

    [Fact]
    public void PrioritiesRunStartMiddleEndThenTheOverflow()
    {
        var log = new List<string>();
        EventManager events = Started();
        var r = new Recorder(log, "r");
        events.AddEvent(3, r, 0.0f, EventPriority.EndOfFrame);
        events.AddEvent(2, r, 0.0f, EventPriority.MiddleOfFrame);
        events.AddEvent(1, r, 0.0f, EventPriority.StartOfFrame);
        events.AddEvent(4, r, 0.0f, EventPriority.StartOfFrame); // appended: after 1
        events.Update();
        Assert.Equal(new[] { "r:1", "r:4", "r:2", "r:3" }, log);
    }

    [Fact]
    public void TenSecondsAheadGoesToTheOverflowInTimeOrderAndRunsStrictlyBeforeTheTime()
    {
        var log = new List<string>();
        EventManager events = Started();
        var r = new Recorder(log, "r");
        events.AddEvent(2, r, 20.0f);
        events.AddEvent(1, r, 15.0f);
        events.AddEvent(3, r, 20.0f); // equal times keep insertion order
        Assert.Equal(new[] { 1, 2, 3 }, events.Overflow.Select(e => e.EventNum));
        events.AddEvent(9, r, 2_000_000.0f); // past 1,000,000 s: dropped
        Assert.Equal(3, events.Overflow.Count);

        while (events.Time <= 15.0f)
        {
            events.Update(); // at mTime == 15.0 the 15.0 event is not yet strictly before it
        }

        Assert.Equal(new[] { "r:1" }, log);
        Assert.Equal(301, events.FrameCount);
    }

    [Fact]
    public void AnEventForAMonitorThatShutDownIsSkippedButCounted()
    {
        var log = new List<string>();
        EventManager events = Started();
        var dead = new Recorder(log, "dead");
        events.AddEvent(1, dead, 0.0f);
        dead.Shutdown();
        events.Update();
        Assert.Empty(log);
        Assert.Equal(1u, events.EventsProcessedInLastUpdate);
        Assert.Equal(0, events.TotalEvents);
    }

    [Fact]
    public void ANullTargetIsNotScheduledAndNeitherIsAnythingBeforeInit()
    {
        var events = new EventManager();
        events.AddEvent(1, new Monitor(), 0.0f);
        Assert.False(events.IsValid);
        events.Init();
        events.AddEvent(1, null, 0.0f);
        Assert.Equal(0, events.TotalEvents);
    }

    [Fact]
    public void AHandlerThatReusesItsEventKeepsItAliveAndKeepsItsTarget()
    {
        var log = new List<string>();
        EventManager events = Started();
        var first = new Recorder(log, "first");
        var other = new Recorder(log, "other");
        int runs = 0;
        first.OnEvent = e =>
        {
            if (++runs < 3)
            {
                // Retail's re-use path changes the time and number but not the target.
                events.AddEvent(e.EventNum + 1, other, EventManager.NextFrame, reuseEvent: e);
            }
        };
        events.AddEvent(1, first, 0.0f);
        events.Update();
        events.Update();
        events.Update();
        Assert.Equal(new[] { "first:1", "first:2", "first:3" }, log);
    }

    [Fact]
    public void ThePoolRunsOutAtTwentyThousandEvents()
    {
        EventManager events = Started();
        var r = new Monitor();
        for (int i = 0; i < EventManager.MaxEvents + 5; i++)
        {
            events.AddEvent(1, r, 5.0f);
        }

        Assert.Equal(EventManager.MaxEvents, events.TotalEvents);
        Assert.Null(events.GetNextFreeEvent());
    }

    [Fact]
    public void TheGamePreRunsSixtyTicksThenPansThenPlays()
    {
        var clock = new PlatformClock();
        var events = new EventManager();
        var game = new ParityGame(clock, events) { CurrentLevel = 100 };
        game.InitRestartLoop();
        Assert.Equal(GameState.PreRunning, game.State);
        game.PreRun();
        Assert.Equal(60, events.FrameCount);
        Assert.Equal(GameState.Panning, game.State);
        Assert.Equal(123456, game.RandomStream!.Seed);
        while (game.State == GameState.Panning)
        {
            game.Update();
        }

        Assert.Equal(GameState.Playing, game.State);
        Assert.Equal(120, events.FrameCount); // FINISHED_PANNING 3.0 s after tick 60
        Assert.Equal(120, game.UpdateFrameNumber);
    }

    [Fact]
    public void TheWorldFilesPanTimeSetsWhenPlayStarts()
    {
        // Level 100's world file: pre-run 3, pan 6 (CWorld::LoadWorld runs after InitRestartLoop).
        var events = new EventManager();
        var game = new ParityGame(new PlatformClock(), events) { CurrentLevel = 100 };
        game.InitRestartLoop();
        game.SetPreRunTime(3.0f);
        game.SetPanTime(6.0f);
        game.PreRun();
        while (game.State == GameState.Panning)
        {
            game.Update();
        }

        Assert.Equal(180, events.FrameCount); // FINISHED_PANNING 6.0 s after tick 60: buffer 60 + 119
    }

    [Fact]
    public void APausedGameDoesNotTickUnlessSteppedOneFrame()
    {
        var game = new ParityGame(new PlatformClock(), new EventManager());
        game.InitRestartLoop();
        game.Paused = true;
        game.Update();
        Assert.Equal(0, game.Events.FrameCount);
        game.AdvanceFrame = true;
        game.Update();
        Assert.Equal(1, game.Events.FrameCount);
        Assert.False(game.AdvanceFrame);
        game.Paused = false;
        game.BuildResources = true;
        game.Update();
        Assert.Equal(1, game.Events.FrameCount);
    }
}
