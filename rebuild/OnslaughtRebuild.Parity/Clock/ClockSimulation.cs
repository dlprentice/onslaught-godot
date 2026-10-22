// SPDX-License-Identifier: GPL-3.0-only
// The level-start order (InitRestartLoop, the base time read, CGame::PreRun, then CGame::MainLoop until quit) follows
// onslaught-decomp src/game.cpp (CGame::RunLevel, RestartLoopRunLevel), in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Clock;

/// <summary>
/// M2: CGame's clock, event manager and main loop with none of the world behind them, driven Present by Present like
/// the retail harness. It reads the clock probes (EVT, FT and LVL) and nothing else. The pre-level Presents (boot, front
/// end, loading screens) are G's flow work (M6); until they exist this simulation Presents through them and is told
/// where the level starts:
/// <list type="bullet">
/// <item>the boot's first GetSysTimeFloat call follows Present 1 (the golden's frame times require it);</item>
/// <item>CGame::InitRestartLoop resets the event manager and schedules FINISHED_PRE_RUN mPreRunTime ahead;</item>
/// <item>mBaseTime is read at <see cref="BaseReadPresent"/>;</item>
/// <item>CGame::PreRun runs at <see cref="LevelStartPresent"/> and ticks with no Present until FINISHED_PRE_RUN fires
/// (tick 60: its buffer is floor((3.0 - 0.001) * 20) = 59);</item>
/// <item>the loading screen's 100% render Presents once (it is not throttled), then the first MainLoop starts.</item>
/// </list>
/// </summary>
public sealed class ClockSimulation : IParitySimulation
{
    private readonly PlatformClock _clock = new();
    private readonly EventManager _events = new();
    private readonly ParityGame _game;
    private IEnumerator<int>? _mainLoop;
    private bool _levelStarted;

    public ClockSimulation(int level, long baseReadPresent, long levelStartPresent, long runLevelPresent = 2,
        bool loadingScreenAfterPreRun = true)
    {
        LoadingScreenAfterPreRun = loadingScreenAfterPreRun;
        ArgumentOutOfRangeException.ThrowIfLessThan(runLevelPresent, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(baseReadPresent, runLevelPresent);
        ArgumentOutOfRangeException.ThrowIfLessThan(levelStartPresent, baseReadPresent);
        Level = level;
        RunLevelPresent = runLevelPresent;
        BaseReadPresent = baseReadPresent;
        LevelStartPresent = levelStartPresent;
        _game = new ParityGame(_clock, _events, new NullGameServices(_clock.Present)) { CurrentLevel = -1 };
    }

    public int Level { get; }

    /// <summary>
    /// Whether the loading screen still exists after PreRun, so its 100% render Presents once. With -skipfmv (or a level
    /// without an intro FMV) it does; when the intro FMV plays, RestartLoopRunLevel's SetLoading(FALSE) before it has
    /// released the loading screen, RenderLoadingScreen returns at once and MainLoop's first Present is the next one.
    /// </summary>
    public bool LoadingScreenAfterPreRun { get; }

    /// <summary>
    /// The Present after which CGame takes the level (mCurrentLevel is -1 until then): Present 2 in the per-Present
    /// retail run of level 100 (C, golden-640), the first of the loading frames when there are any.
    /// </summary>
    public long RunLevelPresent { get; }

    public long BaseReadPresent { get; }

    public long LevelStartPresent { get; }

    public PlatformClock Clock => _clock;

    public EventManager Events => _events;

    public ParityGame Game => _game;

    public GameLoop Loop => _game.Loop;

    public long PresentCount => _clock.PresentCount;

    public void AdvanceToNextPresent()
    {
        long before = _clock.PresentCount;
        while (_clock.PresentCount == before)
        {
            if (!_levelStarted)
            {
                if (_clock.PresentCount == 1)
                {
                    _clock.GetSysTimeFloat(); // the boot's first call: CPCPlatform's first_seen
                }

                if (_clock.PresentCount == RunLevelPresent)
                {
                    _game.CurrentLevel = Level;
                }

                if (_clock.PresentCount == BaseReadPresent)
                {
                    _game.InitRestartLoop();
                    _game.StartLevelTiming();
                }

                if (_clock.PresentCount < LevelStartPresent)
                {
                    _clock.Present(); // a pre-level frame (boot, front end, loading screen)
                    continue;
                }

                _game.PreRun();
                _levelStarted = true;

                // CONSOLE.SetLoadingFraction(1.0f) right after PreRun brings the loading bar to 100%, where
                // RenderLoadingScreen skips its 0.05 s throttle: one Present before the first MainLoop. (The fractions
                // between the base read and PreRun are throttled: no Present has moved the clock since the last one.)
                if (LoadingScreenAfterPreRun)
                {
                    _clock.Present();
                    continue;
                }
            }

            _mainLoop ??= _game.MainLoop().GetEnumerator();
            if (!_mainLoop.MoveNext())
            {
                _mainLoop = null;
            }
        }
    }

    public void DeliverKey(RetailKeyEvent key)
    {
    }

    public void DeliverMouse(RetailMouseEvent mouse)
    {
    }

    public void ReadProbes(long frame, ProbeTrace trace)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, _events.Time);
        trace.Add(frame, ProbeCatalog.EventTime, buffer);
        buffer = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buffer, _game.Loop.FrameTime);
        trace.Add(frame, ProbeCatalog.FrameTime, buffer);
        buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, _game.CurrentLevel);
        trace.Add(frame, ProbeCatalog.Level, buffer);
    }
}
