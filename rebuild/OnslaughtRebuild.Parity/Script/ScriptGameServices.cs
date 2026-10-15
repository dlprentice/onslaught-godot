// SPDX-License-Identifier: GPL-3.0-or-later
// CGame's calls into the mission scripts and the message box (bea-decomp src/game.cpp: SCRIPT_EVENT_NB.PostEvent("game
// playing") 3106, ALLOWED_TO_PLAY_MESSAGES to mMessageBox 3107, mMessageBox->PlayPendingSamples 1912), on top of another
// set of services for everything else.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>
/// The game's services with the script calls wired to a <see cref="ScriptRuntime"/>. The game is made before the
/// things' context, so the runtime is attached once the context exists (<see cref="Runtime"/>); until then the script
/// calls go to the inner services.
/// </summary>
public sealed class ScriptGameServices(IGameServices inner) : IGameServices
{
    /// <summary>The level's script globals (SCRIPT_EVENT_NB, the message box).</summary>
    public ScriptRuntime? Runtime { get; set; }

    public void PostScriptEvent(string name)
    {
        if (Runtime is null)
        {
            inner.PostScriptEvent(name);
            return;
        }

        Runtime.EventNB.PostEvent(System.Text.Encoding.Latin1.GetBytes(name));
    }

    public Monitor? MessageBox => Runtime is null ? inner.MessageBox : Runtime.MessageBox;

    public void MessageBoxPlayPendingSamples()
    {
        if (Runtime is null)
        {
            inner.MessageBoxPlayPendingSamples();
            return;
        }

        Runtime.MessageBox?.PlayPendingSamples();
    }

    public void AutoConfigTestFrame(int updateFrameNumber) => inner.AutoConfigTestFrame(updateFrameNumber);

    public void ControllableCameraUpdateFrameCount() => inner.ControllableCameraUpdateFrameCount();

    public bool HasController(int player) => inner.HasController(player);

    public bool ProcessReconnect(int player) => inner.ProcessReconnect(player);

    public bool PlayerZeroInControl => inner.PlayerZeroInControl;

    public bool ReconnectOk(int player) => inner.ReconnectOk(player);

    public bool IsMultiplayer => inner.IsMultiplayer;

    public bool PlayerOneInControl => inner.PlayerOneInControl;

    public void SetGuideUpdateCounter(int value) => inner.SetGuideUpdateCounter(value);

    public void PollMouseState() => inner.PollMouseState();

    public void UpdateMouseAxisLock(int player) => inner.UpdateMouseAxisLock(player);

    public void FlushController(int player) => inner.FlushController(player);

    public bool IsMenuUp => inner.IsMenuUp;

    public void UpdateMouseCentre() => inner.UpdateMouseCentre();

    public void ClearKeyWasUp() => inner.ClearKeyWasUp();

    public void AtmosphericsProcessAll() => inner.AtmosphericsProcessAll();

    public void SetAllSamplesPaused(bool paused) => inner.SetAllSamplesPaused(paused);

    public void ShowLevelEndMenu() => inner.ShowLevelEndMenu();


    public void HandleGameEvent(GameEvent gameEvent, Event e) => inner.HandleGameEvent(gameEvent, e);

    public void ResetMouseTransientState() => inner.ResetMouseTransientState();

    public QuitType PlatformProcess() => inner.PlatformProcess();

    public bool InactivityMeansQuitGame() => inner.InactivityMeansQuitGame();

    public void EnterAttractMode() => inner.EnterAttractMode();

    /// <summary>SOUND.UpdateStatus (the voice's part) first, then the rest.</summary>
    public void AfterUpdate()
    {
        Runtime?.MessageBox?.Voice.UpdateStatus();
        inner.AfterUpdate();
    }

    public void Render(int numRenders) => inner.Render(numRenders);

    public void Flip() => inner.Flip();
}

/// <summary>
/// The voice side while it is not modelled: sound counts as initialised (retail's is), and starting a voice load stops
/// the run, so a schedule that depends on a voice's length never runs on a guess.
/// </summary>
public sealed class UnmodelledVoice : IMessageVoice
{
    public bool SoundInitialised => true;

    public void StartLoad(string path) =>
        throw new NotSupportedException($"the message box's voice timing is not modelled yet ({path})");

    public bool LoaderBusy => false;

    public bool HasLoadedData => false;

    public void PlayLoaded(ScriptMessage? message) =>
        throw new NotSupportedException("the message box's voice timing is not modelled yet");

    public void KillVoice()
    {
    }

    public void UpdateStatus()
    {
    }
}
