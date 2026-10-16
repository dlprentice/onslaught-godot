// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/game.cpp (CGame::MainLoop, 0046eee0; the mBaseTime read in the level start, 0046df.. at
// game.cpp:1294; CGame::PreRun) and src/game.h (CGame::CalcRenderFrameFraction, inline), in Stuart's release.

namespace OnslaughtRebuild.Parity.Clock;

/// <summary>EQuitType (Platform.h).</summary>
public enum QuitType
{
    None = 0,
    QuitToFrontEnd,
    QuitToSystem,
    LoadError,
    RestartLevel,
    QuitTimeout,
    UserQuitToFrontEnd,
    UserQuitToTitleScreen,
}

/// <summary>
/// What CGame::MainLoop calls outside its own timing, at the points retail calls it. The platform calls (the mouse
/// reset, PLATFORM.Process, the controller's inactivity test) belong to the platform layer, CGame::Update to the game,
/// and Render/Flip to the renderer; Flip must make exactly one <see cref="PlatformClock.Present"/>.
/// </summary>
public interface IMainLoopHost
{
    /// <summary>ResetMouseTransientState (retail 0046eee9).</summary>
    void ResetMouseTransientState();

    /// <summary>PLATFORM.Process: the message pump; returns the quit request it saw.</summary>
    QuitType Process();

    /// <summary>CController::InactivityMeansQuitGame (the playable demo's attract-mode timeout).</summary>
    bool InactivityMeansQuitGame();

    /// <summary>CLIPARAMS.mAttractMode = TRUE, when inactivity quits to the front end.</summary>
    void EnterAttractMode();

    /// <summary>CGame::SetQuit.</summary>
    void SetQuit(QuitType quit);

    /// <summary>CGame::mQuit.</summary>
    QuitType Quit { get; }

    /// <summary>CGame::Update: one game tick (EVENT_MANAGER.AdvanceTime inside it, when not paused).</summary>
    void Update();

    /// <summary>SOUND.UpdateStatus, MUSIC.UpdateStatus and ENGINE.UpdatePos for each player's camera.</summary>
    void AfterUpdate();

    /// <summary>CGame::mPause.</summary>
    bool Paused { get; }

    /// <summary>CGame::mFreeCameraOn[0].</summary>
    bool FreeCameraOn { get; }

    /// <summary>GAME.Render(num_renders), called only while mQuit is QT_NONE.</summary>
    void Render(int numRenders);

    /// <summary>PLATFORM.Flip(TRUE): the frame timer, screen dumps and the Present.</summary>
    void Flip();
}

/// <summary>
/// CGame's frame timing and main loop. Every quantity is a float and every operation in the compiled loop has float
/// operands (fld/fadd/fsub/fdiv/fcomp on dwords) with its result rounded to 24 bits, so C# float arithmetic gives the
/// same values here; only GetSysTimeFloat needs the exact x87 helpers, and it has them.
/// </summary>
public sealed class GameLoop
{
    private readonly PlatformClock _clock;

    public GameLoop(PlatformClock clock)
    {
        _clock = clock;
    }

    /// <summary>mFrameTime (+0x398): the FT probe.</summary>
    public float FrameTime { get; private set; }

    /// <summary>mFrameTime2 (+0x39c).</summary>
    public float FrameTime2 { get; private set; }

    /// <summary>
    /// A restart keeps CGame: its frame times stand from the last level's loop until the new loop sets them (the port's
    /// restarts: FT unchanged through the reload's Presents).
    /// </summary>
    public void CarryFrameTimes(GameLoop previous)
    {
        FrameTime = previous.FrameTime;
        FrameTime2 = previous.FrameTime2;
    }

    /// <summary>mBaseTime (+0x3a0).</summary>
    public float BaseTime { get; private set; }

    /// <summary>mFrameLength (+0x3a8): 0.05f from CGame's constructor.</summary>
    public float FrameLength { get; set; } = 0.05f;

    /// <summary>mFraction (+0x3ac).</summary>
    public float Fraction { get; private set; }

    /// <summary>mFraction2 (+0x3b0).</summary>
    public float Fraction2 { get; private set; }

    /// <summary>Renders (and Presents) in the last completed MainLoop: 1 or 2 in steady play.</summary>
    public int LastRenderCount { get; private set; }

    /// <summary>
    /// The level start's timing (game.cpp:1294, loading fraction 0.4): <c>mBaseTime = PLATFORM.GetSysTimeFloat()</c>,
    /// then CalcRenderFrameFraction.
    /// </summary>
    public void StartLevelTiming()
    {
        BaseTime = _clock.GetSysTimeFloat();
        CalcRenderFrameFraction();
    }

    /// <summary>
    /// One CGame::MainLoop, as an iterator that yields after each Flip, so the caller can stop at every Present. It
    /// ends when the loop returns; a quit seen before or during the update ends it early, as in retail.
    /// </summary>
    public IEnumerable<int> MainLoop(IMainLoopHost host)
    {
        host.ResetMouseTransientState();
        QuitType quit = host.Process();
        if (host.InactivityMeansQuitGame())
        {
            host.EnterAttractMode();
            quit = QuitType.QuitToFrontEnd;
        }

        if (quit != QuitType.None)
        {
            host.SetQuit(quit);
            yield break;
        }

        float startTime = _clock.GetSysTimeFloat();

        // Model.
        FrameTime = BaseTime + FrameLength;
        FrameTime2 = BaseTime + FrameLength;
        CalcRenderFrameFraction();
        host.Update();
        if (host.Quit is QuitType.QuitToFrontEnd or QuitType.UserQuitToFrontEnd)
        {
            yield break;
        }

        host.AfterUpdate();

        // Render.
        float afterModelTime = _clock.GetSysTimeFloat();
        if (afterModelTime < startTime)
        {
            afterModelTime = startTime + 0.01f; // the timer wrapped
        }

        int numRenders = 0;
        do
        {
            if (!host.Paused || host.FreeCameraOn)
            {
                FrameTime2 = _clock.GetSysTimeFloat() - (afterModelTime - startTime);
            }
            else
            {
                FrameTime2 = BaseTime;
            }

            FrameTime = host.Paused ? BaseTime : FrameTime2;
            CalcRenderFrameFraction();

            if (host.Quit == QuitType.None)
            {
                host.Render(numRenders);
            }

            numRenders++;
            host.Flip();
            yield return numRenders;
        }
        while (_clock.GetSysTimeFloat() - BaseTime < FrameLength);

        LastRenderCount = numRenders;

        // Adjust the base time.
        if (numRenders == 1 && _clock.GetSysTimeFloat() - startTime > FrameLength)
        {
            BaseTime += _clock.GetSysTimeFloat() - startTime; // running slowly
            if (_clock.GetSysTimeFloat() - startTime > FrameLength * 1.3f)
            {
                BaseTime = _clock.GetSysTimeFloat(); // running very slowly
            }
        }
        else
        {
            BaseTime += FrameLength;
        }

        if (_clock.GetSysTimeFloat() < BaseTime)
        {
            BaseTime = _clock.GetSysTimeFloat() - (afterModelTime - startTime); // the timer wrapped
        }

        CalcRenderFrameFraction();
    }

    /// <summary>CGame::CalcRenderFrameFraction: both fractions of the frame, clamped to [0, 1].</summary>
    public void CalcRenderFrameFraction()
    {
        Fraction = Math.Clamp((FrameTime - BaseTime) / FrameLength, 0.0f, 1.0f);
        Fraction2 = Math.Clamp((FrameTime2 - BaseTime) / FrameLength, 0.0f, 1.0f);
    }
}
