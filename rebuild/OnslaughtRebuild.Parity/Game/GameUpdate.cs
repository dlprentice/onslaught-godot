// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/game.cpp (CGame::Update, 0046e910), in Stuart's release.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// What CGame calls that is not ported yet, by retail name. <see cref="NullGameServices"/> does nothing (and reports no
/// controller, no menu and connected pads), which leaves the clock, the event manager and the game state exactly as
/// retail's sequence leaves them.
/// </summary>
public interface IGameServices
{
    /// <summary>The -autoconfigtest script's frame switch (pause menu presses, screen dumps).</summary>
    void AutoConfigTestFrame(int updateFrameNumber);

    /// <summary>CControllableCamera::UpdateFrameCount.</summary>
    void ControllableCameraUpdateFrameCount();

    /// <summary>mMessageBox->PlayPendingSamples.</summary>
    void MessageBoxPlayPendingSamples();

    /// <summary>mMessageBox, the target of ALLOWED_TO_PLAY_MESSAGES (null drops the event, as a null target does).</summary>
    Monitor? MessageBox { get; }

    /// <summary>mController[player] != NULL.</summary>
    bool HasController(int player);

    /// <summary>RECONNECT_INTERFACE[player].Process(mController[player], true): whether something reconnected.</summary>
    bool ProcessReconnect(int player);

    /// <summary>mController[0]->GetToControl() == mPlayer[0].</summary>
    bool PlayerZeroInControl { get; }

    /// <summary>RECONNECT_INTERFACE[player].GetState() == CReconnectInterface::OK.</summary>
    bool ReconnectOk(int player);

    /// <summary>GAME.IsMultiplayer().</summary>
    bool IsMultiplayer { get; }

    /// <summary>mController[1]->GetToControl() == mPlayer[1].</summary>
    bool PlayerOneInControl { get; }

    /// <summary>CUnit::mGuideUpdateCounter = mUpdateFrameNumber % 8.</summary>
    void SetGuideUpdateCounter(int value);

    /// <summary>PollMouseState (retail 0046eb62).</summary>
    void PollMouseState();

    /// <summary>gMouseAxisLocked[player] from the player's battle engine (retail 0046eb83).</summary>
    void UpdateMouseAxisLock(int player);

    /// <summary>mController[player]->Flush().</summary>
    void FlushController(int player);

    /// <summary>GAMEINTERFACE.IsMenuUp().</summary>
    bool IsMenuUp { get; }

    /// <summary>UpdateMouseCentre(false) (retail 0046ebac).</summary>
    void UpdateMouseCentre();

    /// <summary>LT.ClearKeyWasUp().</summary>
    void ClearKeyWasUp();

    /// <summary>CAtmospherics::ProcessAll().</summary>
    void AtmosphericsProcessAll();

    /// <summary>SOUND.PauseAllSamples / UnPauseAllSamples.</summary>
    void SetAllSamplesPaused(bool paused);

    /// <summary>The level-end menu (lost, multiplayer won or drawn) once the end count runs down.</summary>
    void ShowLevelEndMenu();

    /// <summary>CGame::Pause's menu part: mPauseMenu->Activate(TRUE), ShutdownMouse, the controller takes the menu.</summary>
    void ShowPauseMenu(Platform.Controller from);

    /// <summary>CGame::UnPause's mPauseMenu->DeActivate(TRUE).</summary>
    void HidePauseMenu();


    /// <summary>SCRIPT_EVENT_NB.PostEvent(name).</summary>
    void PostScriptEvent(string name);

    /// <summary>CGame::HandleEvent's other cases (respawns, pause, the sound fade).</summary>
    void HandleGameEvent(GameEvent gameEvent, Event e);

    // CGame::MainLoop's platform and renderer calls (IMainLoopHost).
    void ResetMouseTransientState();

    QuitType PlatformProcess();

    bool InactivityMeansQuitGame();

    void EnterAttractMode();

    void AfterUpdate();

    void Render(int numRenders);

    /// <summary>PLATFORM.Flip: must call <see cref="PlatformClock.Present"/> exactly once.</summary>
    void Flip();
}

public sealed partial class Game
{
    /// <summary>GAME_COUNT_WHEN_LOST_OR_DRAW: 2.0f in retail (Stuart had 5.0f).</summary>
    public const float GameCountWhenLostOrDraw = 2.0f;

    /// <summary>The constant at 005dbbf4: 0.15f - CLOCK_TICK folded in float, 0x3dccccce.</summary>
    private static readonly float EndCountFloor = BitConverter.Int32BitsToSingle(0x3dccccce);

    /// <summary>The function-static lLastTime of the level-end count.</summary>
    private float _endCountLastTime;

    /// <summary>
    /// CGame::Update, in retail order. The tick (EVENT_MANAGER.AdvanceTime, the input polls, Flush, the atmospherics)
    /// runs when <c>(!mPause || mAdvanceFrame) &amp;&amp; !CLIPARAMS.mBuildResources</c>; in every retail golden all
    /// three are false (no pause, no single-step, no resource build), so it runs on every call. The -buildresources
    /// branch at the end exists only in RESBUILDER builds; retail is not one.
    /// </summary>
    /// <summary>A diagnostic hook called as each Update starts (null in normal runs).</summary>
    public Action? UpdateStarting { get; set; }

    public void Update()
    {
        UpdateStarting?.Invoke();
        UpdateFrameNumber++;

        if (AutoConfigTest)
        {
            _services.AutoConfigTestFrame(UpdateFrameNumber);
        }

        _services.ControllableCameraUpdateFrameCount();
        _services.MessageBoxPlayPendingSamples();

        // The reconnect prompt pauses only once PRE_RUNNING is over.
        if (State > GameState.PreRunning)
        {
            bool somethingReconnected = false;
            for (int i = 0; i < Players; i++)
            {
                if (!_services.HasController(i))
                {
                    continue;
                }

                if (_services.ProcessReconnect(i))
                {
                    somethingReconnected = true;
                }
            }

            if (somethingReconnected && _services.PlayerZeroInControl && Paused
                && _services.ReconnectOk(0) && _services.ReconnectOk(1))
            {
                if (_services.IsMultiplayer)
                {
                    if (_services.PlayerOneInControl)
                    {
                        Paused = false;
                    }
                }
                else
                {
                    Paused = false;
                }
            }

            _services.SetGuideUpdateCounter(UpdateFrameNumber % 8);
        }

        if ((!Paused || AdvanceFrame) && !BuildResources)
        {
            Events.AdvanceTime();
            _services.PollMouseState();
            for (int i = 0; i < Players; i++)
            {
                if (_services.HasController(i))
                {
                    _services.UpdateMouseAxisLock(i);
                    _services.FlushController(i);
                }
            }

            if (!_services.IsMenuUp)
            {
                _services.UpdateMouseCentre();
            }

            _services.ClearKeyWasUp();
            Events.Flush();
            _services.AtmosphericsProcessAll();
        }
        else if (State <= GameState.Quit)
        {
            for (int i = 0; i < Players; i++)
            {
                if (_services.HasController(i))
                {
                    _services.FlushController(i);
                }
            }
        }

        if (PausedAllGameSounds && !Paused)
        {
            _services.SetAllSamplesPaused(false);
            PausedAllGameSounds = false;
        }

        if (!PausedAllGameSounds && Paused)
        {
            _services.SetAllSamplesPaused(true);
            PausedAllGameSounds = true;
        }

        AdvanceFrame = false;

        // The win count runs in ticks (the event manager stops when the level ends).
        if (State == GameState.LevelWon && EndLevelCount >= 0)
        {
            EndLevelCount -= EventManager.ClockTick;
            if (EndLevelCount <= EventManager.ClockTick
                && (!_services.ReconnectOk(0) || (_services.IsMultiplayer && !_services.ReconnectOk(1))))
            {
                EndLevelCount = EventManager.ClockTick; // holds while a controller is disconnected (0046ec88)
            }

            if (CurrentLevel == 741 || CurrentLevel == 742 || EndLevelCount < 0)
            {
                SetQuit(QuitType.QuitToFrontEnd);
            }
        }

        if (State is GameState.LevelLost or GameState.Player1Won or GameState.Player2Won or GameState.GameDrawn)
        {
            if (EndLevelCount > 0.15f)
            {
                // The count follows the system clock, under 5 s a step, stopping a tick above the fade (0046ecfe).
                float time = Clock.GetSysTimeFloat();
                if (_endCountLastTime < time)
                {
                    float delta = time - _endCountLastTime;
                    if (delta < 5.0f && EndLevelCount - delta > EndCountFloor)
                    {
                        EndLevelCount -= delta;
                    }
                    else
                    {
                        EndLevelCount -= EventManager.ClockTick;
                    }
                }
                else
                {
                    EndLevelCount -= EventManager.ClockTick;
                }

                _endCountLastTime = time;
            }
            else if (!Paused)
            {
                FadeOut = true;
            }

            if (EndLevelCount <= GameCountWhenLostOrDraw - 0.5f && !_services.IsMenuUp)
            {
                _services.ShowLevelEndMenu();
            }
        }
    }
}

/// <summary>
/// The do-nothing services: one player with no controller, no menu and pads connected. Flip runs the given action
/// (a clock-only host passes its PlatformClock.Present), or nothing.
/// </summary>
public sealed class NullGameServices : IGameServices
{
    private readonly Action? _flip;

    public NullGameServices(Action? flip = null)
    {
        _flip = flip;
    }

    public static NullGameServices Instance { get; } = new();

    public Monitor? MessageBox => null;

    public bool PlayerZeroInControl => false;

    public bool IsMultiplayer => false;

    public bool PlayerOneInControl => false;

    public bool IsMenuUp => false;

    public void AutoConfigTestFrame(int updateFrameNumber)
    {
    }

    public void ControllableCameraUpdateFrameCount()
    {
    }

    public void MessageBoxPlayPendingSamples()
    {
    }

    public bool HasController(int player) => false;

    public bool ProcessReconnect(int player) => false;

    public bool ReconnectOk(int player) => true;

    public void SetGuideUpdateCounter(int value)
    {
    }

    public void PollMouseState()
    {
    }

    public void UpdateMouseAxisLock(int player)
    {
    }

    public void FlushController(int player)
    {
    }

    public void UpdateMouseCentre()
    {
    }

    public void ClearKeyWasUp()
    {
    }

    public void AtmosphericsProcessAll()
    {
    }

    public void SetAllSamplesPaused(bool paused)
    {
    }

    public void ShowLevelEndMenu()
    {
    }

    public void ShowPauseMenu(Platform.Controller from) => throw new NotSupportedException("the pause menu needs the level's input");

    public void HidePauseMenu()
    {
    }


    public void PostScriptEvent(string name)
    {
    }

    public void HandleGameEvent(GameEvent gameEvent, Event e)
    {
    }

    public void ResetMouseTransientState()
    {
    }

    public QuitType PlatformProcess() => QuitType.None;

    public bool InactivityMeansQuitGame() => false;

    public void EnterAttractMode()
    {
    }

    public void AfterUpdate()
    {
    }

    public void Render(int numRenders)
    {
    }

    public void Flip() => _flip?.Invoke();
}
