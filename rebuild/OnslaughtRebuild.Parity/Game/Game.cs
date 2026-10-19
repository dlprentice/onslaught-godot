// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/game.h and src/game.cpp (CGame: InitRestartLoop 0046c430, the level start's base time
// read at game.cpp:1294, PreRun, StartPanState, StartPlayingState 0046fec0, HandleEvent 0046ff10), in Stuart's
// release.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>EGameEvent (game.h): the events CGame schedules for itself.</summary>
public enum GameEvent
{
    DemoRestartLevel = 2000,
    FinishedPreRun,
    FinishedPanning,
    RespawnPlayer1,
    RespawnPlayer2,
    PauseGame,
    ContinueFadeOutGameSounds,
}

/// <summary>EGameState (game.h).</summary>
public enum GameState
{
    NotRunning,
    PreRunning,
    Panning,
    Playing,
    LevelLost,
    LevelWon,
    Player1Won,
    Player2Won,
    GameDrawn,
    Quit,
}

/// <summary>
/// CGame: the game's state, its timing and the order a level starts in. The subsystems CGame calls that are not ported
/// yet sit behind <see cref="IGameServices"/>; the defaults do nothing, which the clock probes do not see.
/// </summary>
public sealed partial class Game : Monitor, IMainLoopHost
{
    private readonly IGameServices _services;

    public Game(PlatformClock clock, EventManager events, IGameServices? services = null)
    {
        Clock = clock;
        Events = events;
        Loop = new GameLoop(clock);
        _services = services ?? NullGameServices.Instance;
    }

    public PlatformClock Clock { get; }

    public EventManager Events { get; }

    public GameLoop Loop { get; }

    /// <summary>mCurrentLevel.</summary>
    public int CurrentLevel { get; set; }

    /// <summary>mPlayers: 1 in single player.</summary>
    public int Players { get; set; } = 1;

    /// <summary>mGameState.</summary>
    public GameState State { get; private set; } = GameState.NotRunning;

    /// <summary>mQuit.</summary>
    public QuitType Quit { get; private set; }

    /// <summary>
    /// mPause. Always false in the retail goldens: their tapes never pause (a pause would stop EVT, which advances at
    /// every tick of every golden).
    /// </summary>
    public bool Paused { get; set; }

    /// <summary>mAdvanceFrame: the paused single-step. Always false in the goldens (nothing pauses).</summary>
    public bool AdvanceFrame { get; set; }

    /// <summary>CLIPARAMS.mBuildResources (-buildresources). Always false in the goldens.</summary>
    public bool BuildResources { get; set; }

    /// <summary>CLIPARAMS.mAutoConfigTest (-autoconfigtest). Always false in the goldens.</summary>
    public bool AutoConfigTest { get; set; }

    /// <summary>mFreeCameraOn[0].</summary>
    public bool FreeCameraOn { get; set; }

    /// <summary>mPreRunTime: 3.0f from InitRestartLoop.</summary>
    public float PreRunTime { get; set; } = 3.0f;

    /// <summary>mPanTime: 3.0f from InitRestartLoop.</summary>
    public float PanTime { get; set; } = 3.0f;

    /// <summary>mSGradeScore: the world file's S-grade score.</summary>
    public int SGradeScore { get; private set; }

    /// <summary>mDGradeScore: the world file's D-grade score.</summary>
    public int DGradeScore { get; private set; }

    /// <summary>mTimeLimit (seconds).</summary>
    public float TimeLimit { get; private set; }

    /// <summary>mFullScoreTime (seconds).</summary>
    public float FullScoreTime { get; private set; }

    /// <summary>mPercentageScoreTime (seconds).</summary>
    public float PercentageScoreTime { get; private set; }

    /// <summary>mScorePercentage.</summary>
    public float ScorePercentage { get; private set; }

    /// <summary>mLevelStartTime: the world's time of day at the start (version 49+, real world only).</summary>
    public float LevelStartTime { get; private set; }

    // CWorld::LoadWorld's setters (game.h inlines). It calls them after the waypoints when (!base) || (base_level == -1),
    // so after InitRestartLoop's 3.0 defaults: base world 1 sets them first, then the real world overwrites them. The
    // pre-run event is already scheduled by then, so only the pan time changes the level's timing.
    public void SetPreRunTime(float time) => PreRunTime = time;

    public void SetPanTime(float time) => PanTime = time;

    public void SetSGradeScore(int score) => SGradeScore = score;

    public void SetDGradeScore(int score) => DGradeScore = score;

    public void SetTimeLimit(float time) => TimeLimit = time;

    public void SetFullScoreTime(float time) => FullScoreTime = time;

    public void SetPercentageScoreTime(float time) => PercentageScoreTime = time;

    public void SetScorePercentage(float percentage) => ScorePercentage = percentage;

    public void SetLevelStartTime(float time) => LevelStartTime = time;

    /// <summary>mUpdateFrameNumber: CGame::Update calls since construction.</summary>
    public int UpdateFrameNumber { get; private set; }

    /// <summary>mEndLevelCount: -1 until a level ends.</summary>
    public float EndLevelCount { get; set; } = -1.0f;

    /// <summary>mFadeOut.</summary>
    public bool FadeOut { get; private set; }

    /// <summary>mPausedAllGameSounds.</summary>
    public bool PausedAllGameSounds { get; private set; }

    /// <summary>mRandomStream: made in InitRestartLoop with seed 123456.</summary>
    public SeedUniformRandomNumberStream? RandomStream { get; private set; }

    /// <summary>
    /// The timing-relevant part of CGame::InitRestartLoop, in retail order: the event manager is reset, the game is
    /// PRE_RUNNING with FINISHED_PRE_RUN scheduled mPreRunTime from now, the two fear grids are made (each scheduling its
    /// rebuild), and the random stream is made. Before that the score, the career slots (game.cpp:321), the lost reason and
    /// both objective lists are reset, in retail's order.
    /// </summary>
    public void InitRestartLoop()
    {
        Score = 0;
        PreRunTime = 3.0f;
        PanTime = 3.0f;
        EndLevelCount = -1.0f;
        GameSoundsMasterVolume = 1.0f;
        Array.Clear(Slots);
        CareerSlots?.CopyTo(Slots, 0);
        LevelLostReason = 0;
        foreach (MissionObjective o in PrimaryObjectives)
        {
            o.Set(MissionObjectiveState.NotDefined, -1);
        }

        foreach (MissionObjective o in SecondaryObjectives)
        {
            o.Set(MissionObjectiveState.NotDefined, -1);
        }

        Quit = QuitType.None;
        Events.Init();
        Paused = false;
        State = GameState.PreRunning;
        Events.AddEventIn(PreRunTime, (int)GameEvent.FinishedPreRun, this);
        AdvanceFrame = false;

        // game.cpp:505: each grid's constructor rebuilds it and schedules event 1000 a second on.
        ForsetiFearGrid = new FearGrid(Events, 0);
        MuspellFearGrid = new FearGrid(Events, 1);

        // The message box, message log, pause menu and level briefing log are the platform's and the script's; the help
        // text display only needs to exist here (game.cpp:511).
        HasHelpTextDisplay = true;
        RandomStream = new SeedUniformRandomNumberStream(123456);
    }

    /// <summary>
    /// mSettings (SFrontEndSettings): the fields things read. CGame is a static object, so they start zero; the front
    /// end fills them for a career level (what -level N leaves there is not measured yet).
    /// </summary>
    public FrontEndSettings Settings { get; } = new();

    /// <summary>mForsetiFearGrid: made in InitRestartLoop.</summary>
    public FearGrid? ForsetiFearGrid { get; private set; }

    /// <summary>mMuspellFearGrid.</summary>
    public FearGrid? MuspellFearGrid { get; private set; }

    /// <summary>The level start's timing (RestartLoopRunLevel, loading fraction 0.4): mBaseTime from the clock.</summary>
    public void StartLevelTiming() => Loop.StartLevelTiming();

    /// <summary>CGame::PreRun: Update until FINISHED_PRE_RUN moves the game out of PRE_RUNNING, with no render.</summary>
    public void PreRun()
    {
        if (BuildResources)
        {
            return;
        }

        while (State == GameState.PreRunning)
        {
            Update();
        }
    }

    /// <summary>One CGame::MainLoop, yielding after each Present.</summary>
    public IEnumerable<int> MainLoop() => Loop.MainLoop(this);

    public void SetQuit(QuitType quit) => Quit = quit;

    /// <summary>CGame::HandleEvent: the game's own events.</summary>
    public override void HandleEvent(Event e)
    {
        switch ((GameEvent)e.EventNum)
        {
            case GameEvent.DemoRestartLevel:
                SetQuit(QuitType.QuitToFrontEnd);
                break;
            case GameEvent.FinishedPreRun:
                StartPanState();
                break;
            case GameEvent.FinishedPanning:
                StartPlayingState();
                break;
            case GameEvent.PauseGame:
                Pause();
                break;
            case GameEvent.ContinueFadeOutGameSounds:
                ContinueFadeOutGameSounds();
                break;
            default:
                _services.HandleGameEvent((GameEvent)e.EventNum, e);
                break;
        }
    }

    /// <summary>CGame::StartPanState: the players pan for mPanTime, then FINISHED_PANNING.</summary>
    public void StartPanState()
    {
        if (State == GameState.Panning)
        {
            return;
        }

        if (PanTime <= 0.0f)
        {
            StartPlayingState();
            return;
        }

        State = GameState.Panning;
        for (int i = 0; i < Players; i++)
        {
            _player[i]?.GotoPanView(this, PanTime);
        }

        Events.AddEventIn(PanTime, (int)GameEvent.FinishedPanning, this);
    }

    /// <summary>CGame::StartPlayingState: "game playing" to the scripts, and the message box may speak next frame.</summary>
    public void StartPlayingState()
    {
        if (State == GameState.Playing)
        {
            return;
        }

        State = GameState.Playing;
        _services.PostScriptEvent("game playing");
        Events.AddEvent(MessageBoxEvents.AllowedToPlayMessages, _services.MessageBox, EventManager.NextFrame);
    }

    void IMainLoopHost.ResetMouseTransientState() => _services.ResetMouseTransientState();

    QuitType IMainLoopHost.Process() => _services.PlatformProcess();

    bool IMainLoopHost.InactivityMeansQuitGame() => _services.InactivityMeansQuitGame();

    void IMainLoopHost.EnterAttractMode() => _services.EnterAttractMode();

    void IMainLoopHost.AfterUpdate() => _services.AfterUpdate();

    void IMainLoopHost.Render(int numRenders) => _services.Render(numRenders);

    void IMainLoopHost.Flip() => _services.Flip();
}

/// <summary>The message box's event numbers CGame uses (EMessageBoxEvent, MessageBox.h).</summary>
public static class MessageBoxEvents
{
    /// <summary>ALLOWED_TO_PLAY_MESSAGES (3003), scheduled by CGame::StartPlayingState (0046fef2).</summary>
    public const int AllowedToPlayMessages = 3003;
}

/// <summary>SFrontEndSettings: the fields the ported things read.</summary>
public sealed class FrontEndSettings
{
    /// <summary>mInvertSides: player 1 and 2 swap starts.</summary>
    public bool InvertSides;

    /// <summary>mPlayer1Configuration: the battle engine configuration player 1 starts with.</summary>
    public int Player1Configuration;

    public int Player2Configuration;
}
