// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/mouse.cpp and src/mouse.h (reconstructed from the retail program; not in Stuart's release).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Platform;

/// <summary>The mouse window messages the game handles (<c>WM_MOUSEMOVE</c>, <c>WM_[LRM]BUTTON*</c>, <c>WM_MOUSEWHEEL</c>).</summary>
public enum MouseMessage
{
    Move = 0x0200,
    LeftDown = 0x0201,
    LeftUp = 0x0202,
    RightDown = 0x0204,
    RightUp = 0x0205,
    MiddleDown = 0x0207,
    MiddleUp = 0x0208,
    Wheel = 0x020A,
}

/// <summary>A key trap's event codes (<c>KEY_EVENT_*</c>): the mouse reports 4 for a button going down and 3 for up.</summary>
public static class TrapEvent
{
    public const int Up = 3;
    public const int Down = 4;
}

/// <summary><c>SMouseButton</c>: one mouse button's transient state.</summary>
public sealed class MouseButton
{
    /// <summary>Went down since the last reset.</summary>
    public bool Down { get; set; }

    /// <summary>Released since it was last read.</summary>
    public bool Released { get; set; }

    /// <summary>Where it went down: window pixels, or the front end's 640×480 while the front end runs.</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>The same position as a fraction of the window.</summary>
    public float FracX { get; set; }
    public float FracY { get; set; }
}

/// <summary>
/// The Windows mouse as the game keeps it (mouse.cpp, the object after mixermap.obj): the cursor, three buttons and
/// the wheel, set by the window's mouse messages (<c>HandleMouseMessage</c> 0x005234e0) or, once DirectInput drives the
/// cursor, by the device polls (<see cref="DirectInputMouse"/>). The front end reads it through rectangle tests.
/// Buttons are indexed left 0, middle 1, right 2. <c>CLIPARAMS.mAutoConfigTest</c>, which disables the handler and the
/// rectangle tests, is false in play and not modelled.
/// </summary>
public sealed class Mouse
{
    public Mouse() => Buttons = [new MouseButton(), new MouseButton(), new MouseButton()];

    /// <summary><c>MOUSE_X</c>, <c>MOUSE_Y</c>.</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>The cursor as a fraction of the window (<c>sMouseFracX</c>, <c>sMouseFracY</c>).</summary>
    public float FracX { get; private set; }
    public float FracY { get; private set; }

    /// <summary><c>sMouseInputActive</c>: DirectInput moves the cursor, so move messages no longer set it.</summary>
    public bool InputActive { get; set; }

    /// <summary><c>sMouseMoved</c>: the cursor moved; the hover test consumes it.</summary>
    public bool Moved { get; set; }

    /// <summary><c>MOUSE_HELD</c>: left, middle, right.</summary>
    public bool[] Held { get; } = new bool[3];

    /// <summary><c>MOUSE_BUTTONS</c>: left, middle, right.</summary>
    public MouseButton[] Buttons { get; }

    public int LastMoveX { get; private set; }
    public int LastMoveY { get; private set; }

    /// <summary><c>MOUSE_WHEEL</c>.</summary>
    public int Wheel { get; set; }

    /// <summary>
    /// <c>GetCursorPos</c>'s answer: the system cursor's position, which <c>ShutdownMouse</c> takes as the cursor when the
    /// DirectInput mouse is released (a level's pause or end menu, the game interface). The host supplies it unless a
    /// CE tape owns the cursor, in which case the last non-wheel W event supplies it. The default is (0, 0).
    /// </summary>
    public (int X, int Y) SystemCursor { get; set; }

    /// <summary><c>MOUSE_CURSOR_ALPHA</c>.</summary>
    public byte CursorAlpha { get; set; } = 255;

    /// <summary>
    /// <c>HandleMouseMessage</c> (0x005234e0). A move clears the held flags of buttons not in <paramref name="wParam"/>,
    /// sets the cursor from <paramref name="lParam"/> unless DirectInput drives it, and, while the front end runs,
    /// rescales the cursor to 640×480 (applied even when DirectInput set it). A button-down records where it went down
    /// (rescaled likewise) once per press; a button-up marks it released. A key trap sees each button first and
    /// swallows it by returning true. Returns whether any key was pressed (<c>gKeyPressed</c>'s source).
    /// </summary>
    public bool HandleMessage(MouseMessage message, int wParam, int lParam, PlatformWindow window, Func<int, int, bool>? trap)
    {
        bool keyPressed = false;
        switch (message)
        {
            case MouseMessage.Move:
                if ((wParam & 0x01) == 0) Held[0] = false;
                if ((wParam & 0x02) == 0) Held[2] = false;
                if ((wParam & 0x10) == 0) Held[1] = false;
                if (!InputActive)
                {
                    X = lParam & 0xffff;
                    Y = (int)((uint)lParam >> 16);
                    // x87: int-to-float loads are exact; the quotient rounds to 24 bits and is stored to a float.
                    FracX = (float)X87.Div(X, window.Width);
                    FracY = (float)X87.Div(Y, window.Height);
                }
                if (window.FrontEndRunning)
                {
                    X = unchecked(X * 640) / window.Width;
                    Y = unchecked(Y * 480) / window.Height;
                }
                LastMoveX = X;
                LastMoveY = Y;
                Moved = true;
                break;

            case MouseMessage.LeftUp:
                ButtonUp(0, trap);
                break;
            case MouseMessage.RightUp:
                ButtonUp(2, trap);
                break;
            case MouseMessage.MiddleUp:
                ButtonUp(1, trap);
                break;

            case MouseMessage.LeftDown:
                keyPressed = true;
                ButtonDown(0, lParam, window, trap);
                break;
            case MouseMessage.RightDown:
                keyPressed = true;
                ButtonDown(2, lParam, window, trap);
                break;
            case MouseMessage.MiddleDown:
                keyPressed = true;
                ButtonDown(1, lParam, window, trap);
                break;

            case MouseMessage.Wheel:
                short delta = unchecked((short)(wParam >> 16));
                if (trap is not null && trap(delta > 0 ? 3 : 4, TrapEvent.Up)) break;
                Wheel = unchecked(Wheel + delta);
                break;
        }
        return keyPressed;
    }

    private void ButtonUp(int button, Func<int, int, bool>? trap)
    {
        if (trap is not null && trap(button, TrapEvent.Up)) return;
        Held[button] = false;
        Buttons[button].Released = true;
    }

    private void ButtonDown(int button, int lParam, PlatformWindow window, Func<int, int, bool>? trap)
    {
        if (trap is not null && trap(button, TrapEvent.Down)) return;
        Held[button] = true;
        MouseButton state = Buttons[button];
        if (state.Down) return;
        state.Down = true;
        state.X = lParam & 0xffff;
        state.Y = (int)((uint)lParam >> 16);
        state.FracX = (float)X87.Div(state.X, window.Width);
        state.FracY = (float)X87.Div(state.Y, window.Height);
        if (window.FrontEndRunning)
        {
            state.X = unchecked(state.X * 640) / window.Width;
            state.Y = unchecked(state.Y * 480) / window.Height;
        }
    }

    /// <summary><c>ResetMouseTransientState</c> (0x00523db0): run at the start of each front-end process.</summary>
    public void ResetTransientState()
    {
        CursorAlpha = 255;
        foreach (MouseButton button in Buttons)
        {
            button.Down = false;
            button.Released = false;
        }
        Moved = false;
        Wheel = 0;
    }

    private bool At(float left, float top, float right, float bottom) =>
        X >= left && X < right && Y >= top && Y < bottom;

    /// <summary><c>MouseInRect</c> (0x00523b50): the cursor has moved and is inside, and no key trap is set.</summary>
    public bool InRect(float left, float top, float right, float bottom, bool trapSet) =>
        Moved && !trapSet && At(left, top, right, bottom);

    /// <summary><c>MouseClickedInRect</c> (0x00523cc0): a left release inside the rectangle, consumed by this test.</summary>
    public bool ClickedInRect(float left, float top, float right, float bottom, Func<int, int, bool>? trap)
    {
        if (trap is not null && trap(0, TrapEvent.Down)) return false;
        if (Buttons[0].Released && At(left, top, right, bottom))
        {
            Buttons[0].Released = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// <c>MouseDispatchClickInRect</c> (0x00523bc0): a click inside raises <paramref name="click"/> (the front end's
    /// button action from controller 0); returns whether the cursor is inside.
    /// </summary>
    public bool DispatchClickInRect(float left, float top, float right, float bottom, Func<int, int, bool>? trap, Action click)
    {
        if (trap is not null && trap(0, TrapEvent.Down)) return false;
        if (ClickedInRect(left, top, right, bottom, trap)) click();
        return InRect(left, top, right, bottom, trap is not null);
    }

    /// <summary><c>MouseOverRect</c> (0x00523d40): the cursor moved and is inside; consumes the move.</summary>
    public bool OverRect(float left, float top, float right, float bottom, Func<int, int, bool>? trap)
    {
        if (trap is not null && trap(0, TrapEvent.Down)) return false;
        if (Moved && At(left, top, right, bottom))
        {
            Moved = false;
            return true;
        }
        return false;
    }
}

/// <summary>The game window as the input code reads it: its client size, and whether the front end is running.</summary>
public sealed class PlatformWindow
{
    public int Width { get; set; } = 640;
    public int Height { get; set; } = 480;

    /// <summary><c>FRONTEND.IsRunning()</c>: the front end rescales mouse positions to 640×480 while it runs.</summary>
    public bool FrontEndRunning { get; set; }
}
