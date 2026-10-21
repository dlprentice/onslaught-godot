// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/GameInterface.cpp and src/GameInterface.h (CGameInterface: Reset 004729e0,
// ToggleMenuDisplay 00472a90, MenuDown 00472ad0, SelectItem 00472b40, ReceiveButtonAction 00472d50, DrawBackground
// 00472e50, Render 00472f10; reconstructed from the retail program, not in Stuart's release).

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>
/// CGameInterface, GAMEINTERFACE: the in-game menu. Two pages of up to six items: the game menu (continue, message log,
/// level briefing, restart, quit, options) and the options (god mode, auto-aim, back). <see cref="Render"/> fades it and
/// the joypad picture, takes its mouse clicks and draws it.
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

    /// <summary>mUnknown10: the joypad picture's fade, 0 to 255.</summary>
    public int JoypadFade { get; private set; }

    /// <summary>mUnknown18: the menu's fade, 0 to 255.</summary>
    public int MenuFade { get; private set; }

    /// <summary>Init / Reset (004729e0, one function): the menu down on the game page, everything available.</summary>
    public void Reset()
    {
        JoypadFade = 0;
        ShowJoypad = false;
        MenuFade = 0;
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

            _mouse.Shutdown(_windowMouse.SystemCursor.X, _windowMouse.SystemCursor.Y);
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

    /// <summary>
    /// <c>CGameInterface::Render</c> (0x00472f10), at the end of every level render: the joypad picture fades in or out by
    /// 16 and the menu by 32. While the menu shows (and the free camera is off), the game page draws its title and six
    /// items centred in the normal font from <see cref="YBoxOffset"/> below the middle, the selected one pulsing red (each
    /// pulse reads the clock), unavailable ones dim or hidden; restart and quit take the mouse (hover selects, a click
    /// picks the item for the controller that has the menu), and the cursor is drawn. The fades, the mouse and the clock
    /// reads happen whether or not anything is drawn. Not drawn: the options page (page 2), reached only through the game
    /// page's options item, which the PC never offers (its pause is CPauseMenu, and the level-end menu marks it
    /// unavailable).
    /// </summary>
    public void Render(PlatformInput input, Controller?[] controllers, PlatformClock clock, Flow.Draw.FrontEndDraw? draw,
        Text.BitmapFont? small, Text.BitmapFont? normal, Text.FrontEndTexts? texts)
    {
        JoypadFade = ShowJoypad ? Math.Min(JoypadFade + 16, 255) : Math.Max(JoypadFade - 16, 0);
        bool drawing = draw is not null && small is not null && normal is not null && texts is not null;
        if (JoypadFade != 0 && drawing)
        {
            uint col = ((uint)Numerics.X87.Fist(JoypadFade * 0.75f) << 24) | 0xffffff;
            // The picture; its seven labels are the program's own English strings (sJoypadLabels), which are not carried
            // in this source.
            const string joypad = @"Interface\Joypad.tga";
            if (draw!.Textures.NamedSize(joypad) is { } size)
                draw.DrawNamedSprite(joypad, 320 - size.Width / 2, 240 - size.Height / 2, 0.001f, col);
        }

        MenuFade = MenuUp ? Math.Min(MenuFade + 32, 255) : Math.Max(MenuFade - 32, 0);
        bool clicked = false;
        if (Page == 1 && MenuFade != 0 && !_game.FreeCameraOn)
        {
            int alpha = Numerics.X87.Fist(MenuFade * 0.75f);
            int back = Math.Min(alpha, 0xa0);
            if (RenderBackground && drawing)
                draw!.DrawNamedSprite(@"hud\Menu background.tga", 320f, 255f, 0.001f, ((uint)back << 24) | 0x505050, 0.34f, 0.8f,
                    Flow.Draw.SpriteAlign.Centre, format: "X8R8G8B8");
            int y = YBoxOffset - (normal?.Height ?? 32) * 5 / 2 + 240;
            if (texts is not null && normal is not null)
            {
                string title = texts.ById(0x130ed7);
                (int _, int titleHeight) = normal.GetTextExtent(title);
                if (RenderBackground && drawing)
                    Text(draw!, normal, title, y, ((uint)alpha << 24) | 0x505050, shadow: false);
                y += titleHeight;
            }

            uint[] ids = [0x41d6e9, 0x1f08cea, 0x3fc4f9, 0xcada9, 0x7a211, 0x265233];
            for (int item = 0; item < 6; item++)
            {
                uint col = Selection == item ? Pulse(alpha, clock)
                    : _available[item] ? ((uint)alpha << 24) | 0xf0f0f0 : ((uint)alpha << 24) | 0x202020;
                if (texts is not null && normal is not null && (_available[item] || RenderBackground))
                {
                    string text = texts.ById(ids[item]);
                    (int cx, int cy) = normal.GetTextExtent(text);
                    if (item is 3 or 4)
                    {
                        float left = 320f - cx / 2, right = 320f + cx / 2;
                        if (input.Mouse.InRect(left, y, right, y + cy, input.KeyTrap is not null)) Selection = item;
                        if (input.Mouse.ClickedInRect(left, y, right, y + cy, input.KeyTrap))
                        {
                            Selection = item;
                            clicked = true;
                        }
                    }

                    if (drawing) Text(draw!, normal, text, y, col, shadow: item != 0);
                }

                y += 0x18;
            }

            if (drawing) draw!.DrawMouseCursor(input.Mouse.X, input.Mouse.Y, input.Mouse.CursorAlpha);
        }

        if (clicked)
        {
            foreach (Controller? controller in controllers.Take(2))
            {
                if (controller is not null && controller.ToControl == this)
                {
                    SelectItem(controller);
                    break;
                }
            }
        }
    }

    // The selected item's colour, pulsing red (Pulse: the clock read each time).
    private static uint Pulse(int alpha, PlatformClock clock)
    {
        int p = Math.Abs(Numerics.X87.Fist((float)(clock.GetSysTimeFloat() * 2.7f) * 100f) % 256 - 128);
        return unchecked((uint)(((alpha << 24) | 0x400000) + (p << 16) + ((p / 4) << 8) + p / 4));
    }

    private static void Text(Flow.Draw.FrontEndDraw draw, Text.BitmapFont normal, string text, int y, uint col, bool shadow)
    {
        float x = 320f - normal.GetTextExtent(text).Width / 2;
        var kind = new Flow.Draw.QuadTexture(Flow.Draw.CanvasTextureKind.NormalFont);
        if (shadow) draw.DrawTextWithShadow(normal, kind, x, y, col, text, 0, 0f);
        else draw.DrawTextScaled(normal, kind.Kind, x, y, 1f, 1f, 1f, col, text, 0);
    }

}
