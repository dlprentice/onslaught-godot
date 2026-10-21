// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/GameInterface.cpp and src/GameInterface.h (CGameInterface: Reset 004729e0,
// ToggleMenuDisplay 00472a90, MenuDown 00472ad0, SelectItem 00472b40, ReceiveButtonAction 00472d50; reconstructed from
// the retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// CGameInterface, GAMEINTERFACE: the in-game menu. Two pages of up to six items: the game menu (continue, message log,
/// level briefing, restart, quit, options) and the options (god mode, auto-aim, back). Its picture is the host's; this is
/// what it decides.
/// </summary>
public sealed class GameInterface : IButtonReceiver
{
    private readonly Game _game;
    private readonly DirectInputMouse _mouse;
    private readonly Mouse _windowMouse;
    private readonly Action<string> _playFrontEndSound;

    // mAvailable[6]; retail's ToggleMenuDisplay reads one past it while every item is unavailable, which is mPage.
    private readonly bool[] _available = new bool[6];

    public GameInterface(Game game, DirectInputMouse mouse, Mouse windowMouse, Action<string> playFrontEndSound)
    {
        _game = game;
        _mouse = mouse;
        _windowMouse = windowMouse;
        _playFrontEndSound = playFrontEndSound;
        Reset();
    }

    /// <summary>mShowJoypad: the joypad picture replaces the menu.</summary>
    public bool ShowJoypad { get; set; }

    /// <summary>mMenuUp.</summary>
    public bool MenuUp { get; private set; }

    /// <summary>mSelection.</summary>
    public int Selection { get; private set; }

    /// <summary>mRenderBackground.</summary>
    public bool RenderBackground { get; set; }

    /// <summary>mYBoxOffset.</summary>
    public int YBoxOffset { get; set; }

    /// <summary>mPage: 1 the game menu, 2 the options.</summary>
    public int Page { get; private set; }

    public bool IsAvailable(int item) => _available[item];

    /// <summary>Init / Reset (004729e0, one function): the menu down on the game page, everything available.</summary>
    public void Reset()
    {
        ShowJoypad = false;
        MenuUp = false;
        Selection = 0;
        RenderBackground = true;
        YBoxOffset = 0;
        Page = 1;
        Array.Fill(_available, true);
    }

    /// <summary>SetNotAvailable.</summary>
    public void SetNotAvailable(int item) => _available[item] = false;

    private bool AvailableOrPage(int item) => item < 6 ? _available[item] : Page != 0;

    /// <summary>
    /// ToggleMenuDisplay (00472a90): up, on the first available item, the joypad picture put away and the DirectInput
    /// mouse released (ShutdownMouse); down, the mouse taken again (InitMouse).
    /// </summary>
    public void ToggleMenuDisplay()
    {
        MenuUp = !MenuUp;
        if (MenuUp)
        {
            Selection = 0;
            while (!AvailableOrPage(Selection) && Selection < 6)
            {
                Selection++;
            }

            ShowJoypad = false;

            // GetCursorPos's answer is the system cursor's, which the window's last mouse position stands for here.
            _mouse.Shutdown(_windowMouse.X, _windowMouse.Y);
        }
        else
        {
            _mouse.Init();
        }
    }

    /// <summary>MenuUp (inlined in ReceiveButtonAction): the previous available item, if there is one.</summary>
    public void MoveUp()
    {
        if (!MenuUp)
        {
            return;
        }

        int old = Selection;
        Selection--;
        bool wrapped = false;
        while (Selection < 0 || (!_available[Selection] && !wrapped))
        {
            Selection--;
            if (Selection < 0)
            {
                wrapped = true;
                Selection = old;
            }
        }

        if (Selection < 0)
        {
            Selection = 0;
        }

        if (Selection != old)
        {
            _playFrontEndSound("Front End Move");
        }
    }

    /// <summary>MenuDown (00472ad0): the next available item, if there is one.</summary>
    public void MoveDown()
    {
        if (!MenuUp)
        {
            return;
        }

        int last = Page == 2 ? 3 : 5;
        int old = Selection;
        Selection++;
        bool wrapped = false;
        while (Selection > last || (!_available[Selection] && !wrapped))
        {
            Selection++;
            if (Selection > last)
            {
                wrapped = true;
                Selection = old;
            }
        }

        if (Selection > last)
        {
            Selection = last;
        }

        if (Selection != old)
        {
            _playFrontEndSound("Front End Move");
        }
    }

    /// <summary>
    /// SelectItem (00472b40): restart and quit set the game's quit. On PC the menu comes up only at a lost or drawn level's
    /// end, after CGame::Update has made continue, message log, level briefing and options unavailable (bea-decomp
    /// src/game.cpp:2105..2129; the Player.cpp:297/306 toggles are commented out and nothing else sets mShowJoypad), so
    /// those items and the joypad picture's close are unreachable and stay unported. The PC pause menu is CPauseMenu.
    /// </summary>
    public void SelectItem(Controller from)
    {
        if (!MenuUp)
        {
            return;
        }

        if (Page == 1)
        {
            switch (Selection)
            {
                case 0:
                    throw new NotSupportedException("CGameInterface::SelectItem continue: unreachable on PC (unavailable at the level-end menu, game.cpp:2105..2129)");
                case 1:
                    throw new NotSupportedException("CGameInterface::SelectItem message log: unreachable on PC (unavailable at the level-end menu, game.cpp:2105..2129)");
                case 2:
                    throw new NotSupportedException("CGameInterface::SelectItem level briefing: unreachable on PC (unavailable at the level-end menu, game.cpp:2105..2129)");
                case 3:
                    _playFrontEndSound("Front End Select");
                    _game.SetQuit(QuitType.RestartLevel);
                    break;
                case 4:
                    _playFrontEndSound("Front End Select");
                    _game.SetQuit(QuitType.UserQuitToFrontEnd);
                    break;
                case 5:
                    Page = 2;
                    Selection = 0;
                    return;
            }
        }

        if (Page == 2)
        {
            throw new NotSupportedException("CGameInterface::SelectItem options page: unreachable on PC (unavailable at the level-end menu, game.cpp:2105..2129)");
        }
    }

    bool IButtonReceiver.CanBeControlledWhenInPause => true;

    ControlType IButtonReceiver.ControlType => ControlType.GameInterface;

    /// <summary>ReceiveButtonAction (00472d50).</summary>
    void IButtonReceiver.ReceiveButtonAction(Controller from, int button, float value)
    {
        switch (button)
        {
            case Buttons.FrontEndMenuUp:
                if (!ShowJoypad)
                {
                    MoveUp();
                }

                break;
            case Buttons.FrontEndMenuDown:
                if (!ShowJoypad)
                {
                    MoveDown();
                }

                break;
            case Buttons.PauseMenuSelect:
                if (!ShowJoypad)
                {
                    SelectItem(from);
                }

                break;
            case Buttons.Help:
                if (ShowJoypad)
                {
                    throw new NotSupportedException("CGameInterface: the joypad picture is unreachable on PC (nothing sets mShowJoypad; Player.cpp:297/306 commented out)");
                }

                break;
        }
    }
}
