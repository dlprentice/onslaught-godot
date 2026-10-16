// SPDX-License-Identifier: GPL-3.0-or-later
// The level's input as bea-decomp src/game.cpp drives it: InitRestartLoop's InitMouse (0046c44e) and
// UpdateMouseCentre(true) (0046c96f), LoadLevel's controllers (game.cpp:736: one per player, the player as receiver),
// and CGame::Update's tick: PollMouseState (0046eb62), gMouseAxisLocked from IsWalkerOnGroundOrObject (0046eb83), each
// controller's Flush, UpdateMouseCentre(false) (0046ebac) and LT.ClearKeyWasUp; CGame::MainLoop's PLATFORM.Process.

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Platform;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// The game's services for a level with a player at the keyboard and mouse: the platform's window messages, the
/// DirectInput mouse as the replay harness drives it, the control bindings and one controller per player whose receiver
/// is the player. Everything else is the do-nothing services'. A replay tape's events go in through
/// <see cref="Deliver"/> after the Present they follow.
/// </summary>
public sealed class LevelInput : IGameServices, IControllerGame
{
    private readonly PlatformClock _clock;
    private readonly NullGameServices _rest;
    private readonly Controller?[] _controllers = new Controller?[2];
    private Game? _game;

    public LevelInput(PlatformClock clock, ControlBindings bindings)
    {
        _clock = clock;
        _rest = new NullGameServices(clock.Present);
        Window = new PlatformWindow();
        Input = new PlatformInput(Window);
        Mouse = new DirectInputMouse(Input.Mouse, Window);
        Shared = new ControllerShared();
        Context = new ControllerContext(bindings, Input, Shared, this, () => clock.GetSysTimeFloat());
    }

    public PlatformWindow Window { get; }

    public PlatformInput Input { get; }

    public DirectInputMouse Mouse { get; }

    public ControllerShared Shared { get; }

    public ControllerContext Context { get; }

    /// <summary>InitRestartLoop's InitMouse and UpdateMouseCentre(true).</summary>
    public void InitMouse()
    {
        Mouse.Init();
        Context.DirectInputCreated = true;
        Controller.UpdateMouseCentre(Input, Shared, reset: true);
    }

    /// <summary>LoadLevel's controllers: one per player, the player as the object it controls.</summary>
    public void AttachPlayers(Game game)
    {
        _game = game;
        for (int i = 0; i < game.Players && i < _controllers.Length; i++)
        {
            if (game.GetPlayer(i) is { } player)
            {
                _controllers[i] = new Controller(i, 0, player);
            }
        }
    }

    /// <summary>A tape's events after the Present they follow: keys as window messages, mouse into DirectInput.</summary>
    public void Deliver(ReadOnlySpan<RetailTapeEvent> events)
    {
        foreach (RetailTapeEvent e in events)
        {
            switch (e)
            {
                case RetailKeyEvent key:
                    Input.PostTapeKey(key.VirtualKey, key.ScanCode, key.Down);
                    break;
                case RetailMouseEvent mouse:
                    Mouse.AddMouseDelta(mouse.DeltaX, mouse.DeltaY, mouse.Wheel, (byte)mouse.Buttons);
                    break;
            }
        }
    }

    bool IControllerGame.IsPaused => _game?.Paused == true;

    bool IControllerGame.IsMultiplayer => false;

    /// <summary>CGame::ReceiveButtonAction: the hardwired debug buttons (none of the tape's).</summary>
    void IControllerGame.ReceiveButtonAction(Controller from, int button, float value) =>
        throw new NotSupportedException($"CGame::ReceiveButtonAction (debug button {button}) is not ported");

    public bool HasController(int player) => player < _controllers.Length && _controllers[player] is not null;

    public void PollMouseState() => Mouse.PollState();

    public void UpdateMouseAxisLock(int player) =>
        Shared.MouseAxisLocked[player] = _game?.GetPlayer(player)?.BattleEngine is Things.BattleEngine be && be.IsWalkerOnGroundOrObject();

    public void FlushController(int player) => _controllers[player]?.Flush(Context);

    public void UpdateMouseCentre() => Controller.UpdateMouseCentre(Input, Shared, reset: false);

    public void ClearKeyWasUp() => Input.ClearKeyWasUp();

    public void ResetMouseTransientState() => Input.Mouse.ResetTransientState();

    public QuitType PlatformProcess()
    {
        Input.Process(_clock.QueryPerformanceCounter());
        return QuitType.None;
    }

    // The rest: the do-nothing services'.
    public Monitor? MessageBox => _rest.MessageBox;

    public bool PlayerZeroInControl => _rest.PlayerZeroInControl;

    public bool IsMultiplayer => false;

    public bool PlayerOneInControl => _rest.PlayerOneInControl;

    public bool IsMenuUp => false;

    public void AutoConfigTestFrame(int updateFrameNumber) => _rest.AutoConfigTestFrame(updateFrameNumber);

    public void ControllableCameraUpdateFrameCount() => _rest.ControllableCameraUpdateFrameCount();

    public void MessageBoxPlayPendingSamples() => _rest.MessageBoxPlayPendingSamples();

    public bool ProcessReconnect(int player) => false;

    public bool ReconnectOk(int player) => true;

    public void SetGuideUpdateCounter(int value) => _rest.SetGuideUpdateCounter(value);

    public void AtmosphericsProcessAll() => _rest.AtmosphericsProcessAll();

    public void SetAllSamplesPaused(bool paused) => _rest.SetAllSamplesPaused(paused);

    public void ShowLevelEndMenu() => _rest.ShowLevelEndMenu();

    public void PostScriptEvent(string name) => _rest.PostScriptEvent(name);

    public void HandleGameEvent(GameEvent gameEvent, Event e) => _rest.HandleGameEvent(gameEvent, e);

    public bool InactivityMeansQuitGame() => false;

    public void EnterAttractMode() => _rest.EnterAttractMode();

    public void AfterUpdate() => _rest.AfterUpdate();

    /// <summary>The render's effect on the simulation: drawing asks the motion controllers for their parts.</summary>
    public Action<int>? Rendered { get; set; }

    public void Render(int numRenders)
    {
        _rest.Render(numRenders);
        Rendered?.Invoke(numRenders);
    }

    public void Flip() => _rest.Flip();
}
