// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Controller.cpp (InitMouse, ShutdownMouse, PollMouseMotion, PollMouseState).

namespace OnslaughtRebuild.Parity.Platform;

// PortReplayMouse's per-process input: reads consume movement and wheel but leave the button level in place.
internal sealed class DeviceMouseState
{
    private int _x, _y, _wheel;
    private byte _buttons;

    internal void Add(int x, int y, int wheel, byte buttons)
    {
        _x = unchecked(_x + x); _y = unchecked(_y + y); _wheel = unchecked(_wheel + wheel);
        _buttons = buttons;
    }

    internal (int X, int Y, int Wheel, byte Buttons) Read()
    {
        var result = (_x, _y, _wheel, _buttons);
        _x = _y = _wheel = 0;
        return result;
    }

    internal void Discard()
    {
        _x = _y = _wheel = 0;
        _buttons = 0;
    }
}

/// <summary>
/// The DirectInput mouse the level reads (Controller.cpp). The device stands in for DirectInput as the replay harness
/// drives it: <see cref="AddMouseDelta"/> adds a tape's M event, and the first device-state read after it takes the
/// summed movement and wheel (REPORT-6's rule), so later reads in the same frame see none. The button bits are the
/// current state and persist. They use the harness's convention (0x01 left, 0x02 right, 0x04 middle), standing in for
/// DirectInput's <c>rgbButtons[0..2] &amp; 0x80</c>. Not modelled: the re-acquire loop after a failed read (the device
/// never fails here), and <c>CLIPARAMS.mAutoConfigTest</c>, which is false in play.
/// </summary>
public sealed class DirectInputMouse
{
    private readonly Mouse mouse;
    private readonly PlatformWindow window;
    private DeviceMouseState _state;

    public DirectInputMouse(Mouse mouse, PlatformWindow window) : this(mouse, window, new DeviceMouseState()) { }

    internal DirectInputMouse(Mouse mouse, PlatformWindow window, DeviceMouseState state)
    {
        this.mouse = mouse;
        this.window = window;
        _state = state;
    }

    internal void UseReplayState(DeviceMouseState state) => _state = state;

    /// <summary>Whether <c>InitMouse</c> has created the device (<c>sMouse</c>).</summary>
    public bool Created { get; private set; }

    // LevelInput keeps its controller's sMouse check in step with every Init/Shutdown path.
    internal Action<bool>? CreationChanged { get; set; }

    /// <summary>
    /// The live host's suspension boundary: discard queued movement/wheel and device buttons without creating,
    /// polling or releasing the device. This is host policy, not a change to retail Init/Shutdown/Poll semantics.
    /// </summary>
    public void DiscardPendingState() => _state.Discard();

    /// <summary>A tape's M event: movement and wheel since the last read, and the current button bits (0x01 left, 0x02 right, 0x04 middle).</summary>
    public void AddMouseDelta(int dx, int dy, int wheel, byte buttons) => _state.Add(dx, dy, wheel, buttons);

    /// <summary><c>InitMouse</c> (0x0042d310): the device, the transient state reset, the cursor centred, DirectInput driving it.</summary>
    public void Init()
    {
        Created = true;
        mouse.ResetTransientState();
        mouse.X = window.Width >> 1;
        mouse.Y = window.Height >> 1;
        mouse.InputActive = true;
        CreationChanged?.Invoke(Created);
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
        CreationChanged?.Invoke(Created);
    }

    private (int X, int Y, int Wheel, byte Buttons) GetDeviceState() => _state.Read();

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
