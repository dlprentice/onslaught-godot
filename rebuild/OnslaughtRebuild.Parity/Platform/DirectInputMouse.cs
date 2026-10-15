// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Controller.cpp (InitMouse, ShutdownMouse, PollMouseMotion, PollMouseState).

namespace OnslaughtRebuild.Parity.Platform;

/// <summary>
/// The DirectInput mouse the level reads (Controller.cpp). The device stands in for DirectInput as the replay harness
/// drives it: <see cref="AddMouseDelta"/> adds a tape's M event, and the first device-state read after it takes the
/// summed movement and wheel (REPORT-6's rule), so later reads in the same frame see none. The button bits are the
/// current state and persist.
/// </summary>
public sealed class DirectInputMouse(Mouse mouse, PlatformWindow window)
{
    private int _pendingX, _pendingY, _pendingWheel;
    private byte _buttons;

    /// <summary>Whether <c>InitMouse</c> has created the device (<c>sMouse</c>).</summary>
    public bool Created { get; private set; }

    /// <summary>A tape's M event: movement and wheel since the last read, and the current button bits (0x01 left, 0x02 right, 0x04 middle).</summary>
    public void AddMouseDelta(int dx, int dy, int wheel, byte buttons)
    {
        _pendingX = unchecked(_pendingX + dx);
        _pendingY = unchecked(_pendingY + dy);
        _pendingWheel = unchecked(_pendingWheel + wheel);
        _buttons = buttons;
    }

    /// <summary><c>InitMouse</c> (0x0042d310): the device, the transient state reset, the cursor centred, DirectInput driving it.</summary>
    public void Init()
    {
        Created = true;
        mouse.ResetTransientState();
        mouse.X = window.Width >> 1;
        mouse.Y = window.Height >> 1;
        mouse.InputActive = true;
    }

    /// <summary>
    /// <c>ShutdownMouse</c> (0x0042d3b0): the device released and window messages back in charge. The cursor then comes
    /// from the system cursor position (<paramref name="cursorX"/>, <paramref name="cursorY"/>, as <c>GetCursorPos</c>
    /// reports it).
    /// </summary>
    public void Shutdown(int cursorX, int cursorY)
    {
        if (Created)
        {
            mouse.InputActive = false;
            Created = false;
        }
        mouse.X = cursorX;
        mouse.Y = cursorY;
        mouse.ResetTransientState();
    }

    private (int X, int Y, int Wheel, byte Buttons) GetDeviceState()
    {
        (int, int, int, byte) state = (_pendingX, _pendingY, _pendingWheel, _buttons);
        _pendingX = _pendingY = _pendingWheel = 0;
        return state;
    }

    /// <summary><c>PollMouseMotion</c> (0x0042d420): the movement and wheel only.</summary>
    public void PollMotion()
    {
        if (!Created) return;
        (int x, int y, int wheel, _) = GetDeviceState();
        mouse.X = unchecked(mouse.X + x);
        mouse.Y = unchecked(mouse.Y + y);
        mouse.Wheel = unchecked(mouse.Wheel + wheel);
    }

    /// <summary>
    /// <c>PollMouseState</c> (0x0042d4d0): the movement and wheel, then the buttons. DirectInput's buttons 0, 1 and 2
    /// are left, right and middle; a button not down clears its down flag and marks a release if it was held.
    /// </summary>
    public void PollState()
    {
        if (!Created) return;
        (int x, int y, int wheel, byte buttons) = GetDeviceState();
        mouse.X = unchecked(mouse.X + x);
        mouse.Y = unchecked(mouse.Y + y);
        mouse.Wheel = unchecked(mouse.Wheel + wheel);
        Button(0, (buttons & 0x01) != 0);
        Button(2, (buttons & 0x02) != 0);
        Button(1, (buttons & 0x04) != 0);
    }

    private void Button(int index, bool down)
    {
        if (down)
        {
            mouse.Buttons[index].Down = true;
            mouse.Held[index] = true;
            return;
        }
        mouse.Buttons[index].Down = false;
        mouse.Buttons[index].Released |= mouse.Held[index];
        mouse.Held[index] = false;
    }
}
