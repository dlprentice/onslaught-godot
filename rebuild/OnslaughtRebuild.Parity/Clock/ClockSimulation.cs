// SPDX-License-Identifier: GPL-3.0-only
// The level-start order (base time, CGame::PreRun, then CGame::MainLoop until quit) follows bea-decomp src/game.cpp
// (CGame::RunLevel), in Stuart's release.

using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Clock;

/// <summary>
/// M2: the game's clock and main loop with no game behind them, driven Present by Present like the retail harness. It
/// reads the clock probes (EVT, FT and LVL) and nothing else. The pre-level Presents (boot, front end, loading screens)
/// are G's flow work (M6); until they exist this simulation Presents through them and is told where the level starts:
/// <list type="bullet">
/// <item>the boot's first GetSysTimeFloat call follows Present 1 (the golden's frame times require it);</item>
/// <item>mBaseTime is read at <see cref="BaseReadPresent"/>;</item>
/// <item>CGame::PreRun runs <see cref="PreRunTicks"/> ticks with no Present (until FINISHED_PRE_RUN at
/// mPreRunTime 3.0 s);</item>
/// <item>the first MainLoop starts at <see cref="LevelStartPresent"/>.</item>
/// </list>
/// </summary>
public sealed class ClockSimulation : IParitySimulation, IMainLoopHost
{
    private readonly PlatformClock _clock = new();
    private readonly EventClock _events = new();
    private readonly GameLoop _loop;
    private IEnumerator<int>? _mainLoop;
    private bool _levelStarted;

    public ClockSimulation(int level, long baseReadPresent, long levelStartPresent, int preRunTicks = 60)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(baseReadPresent, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(levelStartPresent, baseReadPresent);
        Level = level;
        BaseReadPresent = baseReadPresent;
        LevelStartPresent = levelStartPresent;
        PreRunTicks = preRunTicks;
        _loop = new GameLoop(_clock);
    }

    /// <summary>CGame's level number, from the command line at boot.</summary>
    public int Level { get; }

    public long BaseReadPresent { get; }

    public long LevelStartPresent { get; }

    public int PreRunTicks { get; }

    public PlatformClock Clock => _clock;

    public EventClock Events => _events;

    public GameLoop Loop => _loop;

    public long PresentCount => _clock.PresentCount;

    QuitType IMainLoopHost.Quit => QuitType.None;

    bool IMainLoopHost.Paused => false;

    bool IMainLoopHost.FreeCameraOn => false;

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

                if (_clock.PresentCount == BaseReadPresent)
                {
                    _loop.StartLevelTiming();
                }

                if (_clock.PresentCount < LevelStartPresent)
                {
                    _clock.Present(); // a pre-level frame (boot, front end, loading screen)
                    continue;
                }

                _events.Init();
                for (int i = 0; i < PreRunTicks; i++)
                {
                    _events.AdvanceTime(); // CGame::PreRun's Updates: no render, no Present
                }

                _levelStarted = true;
            }

            _mainLoop ??= _loop.MainLoop(this).GetEnumerator();
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
        BinaryPrimitives.WriteSingleLittleEndian(buffer, _loop.FrameTime);
        trace.Add(frame, ProbeCatalog.FrameTime, buffer);
        buffer = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, Level);
        trace.Add(frame, ProbeCatalog.Level, buffer);
    }

    void IMainLoopHost.ResetMouseTransientState()
    {
    }

    QuitType IMainLoopHost.Process() => QuitType.None;

    bool IMainLoopHost.InactivityMeansQuitGame() => false;

    void IMainLoopHost.EnterAttractMode()
    {
    }

    void IMainLoopHost.SetQuit(QuitType quit)
    {
    }

    void IMainLoopHost.Update() => _events.AdvanceTime();

    void IMainLoopHost.AfterUpdate()
    {
    }

    void IMainLoopHost.Render(int numRenders)
    {
    }

    void IMainLoopHost.Flip() => _clock.Present();
}
