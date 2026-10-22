// SPDX-License-Identifier: GPL-3.0-only

using Godot;
using OnslaughtRebuild.Parity.Platform;
using Button = Godot.MouseButton;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Ordinary Godot window events delivered to the parity platform immediately before its next Present advance.
/// This owns no cursor capture, clock, gameplay action or InputMap. The host owns focus and the choice between this
/// adapter and tape input. Mouse coordinates are for the fixed 640x480 client; scaled presentation needs an explicit
/// coordinate transform before admitting window events. Joysticks, text input and horizontal wheels remain unsupported.
/// </summary>
public sealed class HostInputAdapter(PlatformInput platform, DirectInputMouse device)
{
    private readonly List<PendingEvent> _pending = [];
    private double _fractionX, _fractionY, _fractionWheel;
    private byte _buttons;
    private bool _previousDeviceCreated = device.Created;

    public bool Focused { get; private set; }
    public int PendingEvents => _pending.Count;
    public long AcceptedEvents { get; private set; }

    private enum Kind { Key, Motion, Button, Wheel }
    private readonly record struct PendingEvent(Kind Kind, int A = 0, int B = 0, bool Down = false,
        double X = 0, double Y = 0, double Amount = 0, int ClientX = 0, int ClientY = 0);

    /// <summary>
    /// False menuNavigationEnabled is the limited live host's pause policy: only Escape/Backspace presses may resume
    /// it; releases still pass, so a hidden options menu cannot be driven. This is not a retail menu restriction.
    /// PhysicalKeycode is required: logical Keycode/Unicode never substitutes for an absent physical key.
    /// </summary>
    public bool Observe(InputEvent input, bool menuNavigationEnabled = true) => input switch
    {
        InputEventKey key => QueueKey(key.PhysicalKeycode, key.Location, key.Pressed, menuNavigationEnabled),
        InputEventMouseMotion motion => QueueMotion(motion.ScreenRelative.X, motion.ScreenRelative.Y,
            motion.Position.X, motion.Position.Y, menuNavigationEnabled),
        InputEventMouseButton button => QueueButton(button.ButtonIndex, button.Pressed && !button.Canceled,
            button.Position.X, button.Position.Y, button.Factor, menuNavigationEnabled),
        _ => false,
    };

    // These value-taking entry points are also used by focused checks without requiring a native Godot InputEvent.
    public bool QueueKey(Key physical, KeyLocation location, bool down, bool menuNavigationEnabled = true)
    {
        if (!HostKeyMap.TryMap(physical, location, out HostKeyMap.PcKey key)) return false;
        if (down && (!Focused || (!menuNavigationEnabled && physical is not (Key.Escape or Key.Backspace)))) return false;
        if (!Focused) return false; // ReleaseAll already removed held state at focus loss.
        return Add(new(Kind.Key, key.VirtualKey, key.ScanCode, down));
    }

    public bool QueueMotion(double dx, double dy, double clientX, double clientY, bool menuNavigationEnabled = true)
    {
        if (!Focused || !menuNavigationEnabled) return false;
        RequireFinite(dx, nameof(dx)); RequireFinite(dy, nameof(dy));
        return Add(new(Kind.Motion, X: dx, Y: dy, ClientX: Pixel(clientX), ClientY: Pixel(clientY)));
    }

    public bool QueueButton(Button button, bool down, double clientX, double clientY, double factor = 1,
        bool menuNavigationEnabled = true)
    {
        if (!Focused || (down && !menuNavigationEnabled)) return false;
        int x = Pixel(clientX), y = Pixel(clientY);
        if (button is Button.WheelUp or Button.WheelDown)
        {
            if (!down) return false;
            RequireFinite(factor, nameof(factor));
            // Godot documents zero Factor when precise scrolling is unsupported: one ordinary notch then.
            double notches = factor == 0 ? 1 : Math.Abs(factor);
            return Add(new(Kind.Wheel, Amount: (button == Button.WheelUp ? 120 : -120) * notches, ClientX: x, ClientY: y));
        }

        int bit = button switch { Button.Left => 1, Button.Right => 2, Button.Middle => 4, _ => 0 };
        return bit != 0 && Add(new(Kind.Button, A: bit, Down: down, ClientX: x, ClientY: y));
    }

    private bool Add(PendingEvent e)
    {
        _pending.Add(e);
        AcceptedEvents++;
        return true;
    }

    /// <summary>
    /// Drain once before LevelSession.AdvanceToNextPresent. Keys stay queued for the parity platform pump; an acquired
    /// mouse receives accumulated relative counts/current buttons, otherwise the window receives ordinary messages.
    /// Fractional relative counts carry across drains, never round independently per event. Device motion is not also
    /// sent as a window move. A press and release between device polls resolves to its last state, as tape M does.
    /// </summary>
    public void DrainBeforePresent()
    {
        if (!Focused) { _pending.Clear(); return; }
        bool relative = device.Created;
        if (relative != _previousDeviceCreated)
        {
            // Init/Shutdown model the retail call paths, not the host's discarded input packets. An unpolled packet
            // must not cross this live-device boundary. A close/reopen entirely between drains needs ReleaseAll from
            // the host's pause/restart boundary too; the current Created value alone cannot reveal that transition.
            device.DiscardPendingState();
            _fractionX = _fractionY = _fractionWheel = 0;
            _previousDeviceCreated = relative;
        }
        double dx = _fractionX, dy = _fractionY, wheel = _fractionWheel;
        foreach (PendingEvent e in _pending)
        {
            switch (e.Kind)
            {
                case Kind.Key:
                    platform.PostTapeKey(e.A, e.B, e.Down);
                    break;
                case Kind.Motion:
                    if (relative) { dx += e.X; dy += e.Y; }
                    else platform.PostMouseMessage(MouseMessage.Move, e.ClientX, e.ClientY, WindowButtons());
                    break;
                case Kind.Button:
                    _buttons = e.Down ? (byte)(_buttons | e.A) : (byte)(_buttons & ~e.A);
                    if (!relative) platform.PostMouseMessage(ButtonMessage(e.A, e.Down), e.ClientX, e.ClientY, WindowButtons());
                    break;
                case Kind.Wheel:
                    wheel += e.Amount;
                    if (!relative)
                    {
                        int counts = Whole(ref wheel);
                        if (counts != 0)
                        {
                            if (counts is < short.MinValue or > short.MaxValue) throw new InvalidOperationException("a wheel message exceeds the PC signed-word range");
                            platform.PostMouseMessage(MouseMessage.Wheel, e.ClientX, e.ClientY, (counts << 16) | WindowButtons());
                        }
                    }
                    break;
            }
        }

        if (relative)
        {
            int x = Whole(ref dx), y = Whole(ref dy), w = Whole(ref wheel);
            device.AddMouseDelta(x, y, w, _buttons);
            _fractionX = dx; _fractionY = dy;
        }
        else
        {
            // A released/recreated device does not inherit pre-menu subpixel motion.
            _fractionX = _fractionY = 0;
        }
        _fractionWheel = wheel;
        _pending.Clear();
    }

    public void SetFocused(bool focused)
    {
        if (!focused) ReleaseAll();
        Focused = focused;
    }

    /// <summary>
    /// Host suspension/exit, not a recovered retail operation: discard stale input without dispatching traps, changing
    /// the clock, or creating/destroying the core's mouse. The host also calls this immediately after an observed core
    /// pause/unpause/restart boundary, before admitting the next input batch. That prevents same-batch menu presses and
    /// an unobserved close/reopen of the device retaining old input. Focus is unchanged; no held input survives refocus.
    /// </summary>
    public void ReleaseAll()
    {
        _pending.Clear();
        _fractionX = _fractionY = _fractionWheel = 0;
        _buttons = 0;
        platform.DiscardPendingMessages();
        platform.FlushInputBuffers();
        platform.KeyPressed = false;
        device.DiscardPendingState();
        _previousDeviceCreated = device.Created;
        Array.Clear(platform.Mouse.Held);
        platform.Mouse.ResetTransientState();
    }

    private int WindowButtons() => (_buttons & 3) | ((_buttons & 4) != 0 ? 0x10 : 0);
    private static MouseMessage ButtonMessage(int bit, bool down) => (bit, down) switch
    {
        (1, true) => MouseMessage.LeftDown, (1, false) => MouseMessage.LeftUp,
        (2, true) => MouseMessage.RightDown, (2, false) => MouseMessage.RightUp,
        (4, true) => MouseMessage.MiddleDown, (4, false) => MouseMessage.MiddleUp,
        _ => throw new ArgumentOutOfRangeException(nameof(bit)),
    };
    private static int Whole(ref double value)
    {
        int result = checked((int)Math.Truncate(value));
        value -= result;
        return result;
    }
    private static int Pixel(double value) { RequireFinite(value, nameof(value)); return checked((int)Math.Truncate(value)); }
    private static void RequireFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "input must be finite");
    }
}
