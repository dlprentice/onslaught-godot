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
public sealed class LevelInput : IGameServices, IControllerGame, Flow.Menus.IPauseMenuGame
{
    private readonly PlatformClock _clock;
    private readonly NullGameServices _rest;
    private readonly Controller?[] _controllers = new Controller?[2];
    private Game? _game;

    public LevelInput(PlatformClock clock, ControlBindings bindings)
        : this(clock, bindings, new PlatformInput(new PlatformWindow()), new ControllerShared())
    {
    }

    /// <summary>A level's input on the process's window and input (a level started from the front end).</summary>
    public LevelInput(PlatformClock clock, ControlBindings bindings, PlatformInput input, ControllerShared shared)
    {
        _clock = clock;
        _rest = new NullGameServices(clock.Present);
        Window = input.Window;
        Input = input;
        Mouse = new DirectInputMouse(Input.Mouse, Window);
        Shared = shared;
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

    /// <summary>
    /// LoadLevel's controllers: one per player, the player as the object it controls; and the in-game menu, whose front-end
    /// sounds <paramref name="playFrontEndSound"/> plays.
    /// </summary>
    public void AttachPlayers(Game game, Action<string> playFrontEndSound)
    {
        _game = game;
        GameInterface = new GameInterface(game, Mouse, Input.Mouse, playFrontEndSound);
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

    /// <summary>
    /// CGame::ReceiveButtonAction (0046f7e0): the hardwired debug buttons. BUTTON_SKIP_CUTSCENE (Space, Return, Escape)
    /// stops the current cutscene, and none plays here (the script's PlayCutscene is not ported); the console menu's
    /// up, down and select (the numeric keypad) do nothing while its menu is not active (CConsole::MenuUp 0042ba90,
    /// MenuDown 0042bac0), and the console is never opened here; the rest are not ported.
    /// </summary>
    void IControllerGame.ReceiveButtonAction(Controller from, int button, float value)
    {
        if (button is not (Buttons.SkipCutscene or Buttons.ConsoleMenuUp or Buttons.ConsoleMenuDown or Buttons.ConsoleMenuSelect))
        {
            throw new NotSupportedException($"CGame::ReceiveButtonAction (debug button {button}) is not ported");
        }
    }

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

    /// <summary>GAME's mPauseMenu (InitRestartLoop makes it): the in-game menu, rendered after every frame.</summary>
    public Flow.Menus.PauseMenu? PauseMenu { get; private set; }

    /// <summary>InitRestartLoop's mPauseMenu = new CPauseMenu, on the menus' host.</summary>
    public void CreatePauseMenu(Flow.Menus.IMenuHost host, Func<IReadOnlyList<Script.ScriptMessage>> shownMessages)
    {
        PauseMenuHost = host;
        PauseMenu = new Flow.Menus.PauseMenu(host, optionsPage: false, this);
        MessageLog = new Flow.Menus.MessageLog(host, this, shownMessages, Flow.Menus.MessageLog.ShippedCharsPerLine) { PauseMenu = PauseMenu };
        PauseMenu.MessageLog = MessageLog;
        BriefingLog = new Flow.Menus.BriefingLog(host, this) { PauseMenu = PauseMenu };
        PauseMenu.BriefingLog = BriefingLog;
    }

    /// <summary>GAME's mLevelBriefingLog (InitRestartLoop makes it).</summary>
    public Flow.Menus.BriefingLog? BriefingLog { get; private set; }

    /// <summary>GAME's mMessageLog (InitRestartLoop makes it).</summary>
    public Flow.Menus.MessageLog? MessageLog { get; private set; }

    /// <summary>The menus' host the pause menu was made on.</summary>
    public Flow.Menus.IMenuHost? PauseMenuHost { get; private set; }

    /// <summary>CGame::Pause's menu part: the menu opens, the DirectInput mouse goes (ShutdownMouse), the controller takes it.</summary>
    public void ShowPauseMenu(Controller from)
    {
        Flow.Menus.PauseMenu menu = PauseMenu ?? throw new InvalidOperationException("no pause menu (CreatePauseMenu)");
        menu.Activate(true);

        // GetCursorPos's answer is the system cursor's, which the window's last mouse position stands for here.
        Mouse.Shutdown(Input.Mouse.X, Input.Mouse.Y);
        from.SetToControl(menu);
    }

    /// <summary>CGame::UnPause's mPauseMenu->DeActivate(TRUE).</summary>
    public void HidePauseMenu() => PauseMenu?.DeActivate(true);

    void Flow.Menus.IPauseMenuGame.SetQuit(QuitType quit) => _game!.SetQuit(quit);

    void Flow.Menus.IPauseMenuGame.UnPause() => _game!.UnPause();

    void Flow.Menus.IPauseMenuGame.InitMouse() => Mouse.Init();

    void Flow.Menus.IPauseMenuGame.HandControl(IButtonReceiver from, IButtonReceiver to)
    {
        foreach (Controller? controller in _controllers)
        {
            if (controller is not null && controller.ToControl == from)
            {
                controller.SetToControl(to);
            }
        }
    }

    void Flow.Menus.IPauseMenuGame.RelinquishControl(IButtonReceiver menu)
    {
        foreach (Controller? controller in _controllers)
        {
            if (controller is not null && controller.ToControl == menu)
            {
                controller.RelinquishControl();
            }
        }
    }

    bool Flow.Menus.IPauseMenuGame.IsMultiplayer => false;

    int Flow.Menus.IPauseMenuGame.NumPrimaryObjectives => _game!.GetNumPrimaryObjectives();

    int Flow.Menus.IPauseMenuGame.NumSecondaryObjectives => _game!.GetNumSecondaryObjectives();

    int Flow.Menus.IPauseMenuGame.CurrentLevel => _game!.CurrentLevel;

    /// <summary>GAMEINTERFACE.</summary>
    public GameInterface? GameInterface { get; private set; }

    public bool IsMenuUp => GameInterface?.MenuUp == true;

    public void AutoConfigTestFrame(int updateFrameNumber) => _rest.AutoConfigTestFrame(updateFrameNumber);

    public void ControllableCameraUpdateFrameCount() => _rest.ControllableCameraUpdateFrameCount();

    public void MessageBoxPlayPendingSamples() => _rest.MessageBoxPlayPendingSamples();

    public bool ProcessReconnect(int player) => false;

    public bool ReconnectOk(int player) => true;

    public void SetGuideUpdateCounter(int value) => _rest.SetGuideUpdateCounter(value);

    public void AtmosphericsProcessAll() => _rest.AtmosphericsProcessAll();

    public void SetAllSamplesPaused(bool paused) => _rest.SetAllSamplesPaused(paused);

    /// <summary>
    /// CGame::Update's level-end menu (0046ed71): continue, the logs and options unavailable (restart and quit stay), no
    /// background, the box lower, the free camera off, controller 0 to the menu, and the menu up.
    /// </summary>
    public void ShowLevelEndMenu()
    {
        GameInterface menu = GameInterface!;
        menu.SetNotAvailable(0);
        menu.SetNotAvailable(1);
        menu.SetNotAvailable(2);
        menu.SetNotAvailable(5);
        menu.RenderBackground = false;
        menu.YBoxOffset = 70;
        if (_game!.FreeCameraOn)
        {
            throw new NotSupportedException("CGame::ToggleFreeCameraOff is not ported");
        }

        _controllers[0]!.SetToControl(menu);
        menu.ToggleMenuDisplay();
    }

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
