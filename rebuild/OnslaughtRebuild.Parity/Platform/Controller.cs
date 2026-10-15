// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Controller.cpp, src/Controller.h and src/PCController.cpp (CController, CPCController,
// UpdateMouseCentre).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Platform;

/// <summary><c>EControlType</c>: what a controlled object is.</summary>
public enum ControlType
{
    FrontEnd,
    Mech,
    Camera,
    GameInterface,
    Game,
    MessageLog,
    LevelBriefingLog,
    GameMenu,
}

/// <summary><c>IController</c>: an object a controller can drive.</summary>
public interface IButtonReceiver
{
    void ReceiveButtonAction(Controller from, int button, float value);
    bool CanBeControlledWhenInPause { get; }
    ControlType ControlType { get; }
}

/// <summary>What a controller reads from the game: pause, multiplayer, and where the hardwired debug buttons go.</summary>
public interface IControllerGame
{
    bool IsPaused { get; }
    bool IsMultiplayer { get; }

    /// <summary><c>GAME.ReceiveButtonAction</c>, for buttons below <see cref="Buttons.StartActionButtons"/> outside the front end.</summary>
    void ReceiveButtonAction(Controller from, int button, float value);
}

/// <summary>The controllers' process-wide state: the static members of <c>CController</c> and the file statics beside it.</summary>
public sealed class ControllerShared
{
    /// <summary><c>CController::mLastTimeAnythingPressed</c>.</summary>
    public float LastTimeAnythingPressed { get; set; }

    /// <summary><c>CController::mNonInteractiveSection</c>.</summary>
    public bool NonInteractiveSection { get; set; }

    /// <summary><c>gMouseSensitivity</c> (an option the career saves; 7 by default).</summary>
    public float MouseSensitivity { get; set; } = 7.0f;

    /// <summary><c>gMouseAxisLocked</c>: per player, the locked mouse axes are ignored.</summary>
    public bool[] MouseAxisLocked { get; } = new bool[2];

    /// <summary><c>sSendingMouseRest</c>: a centred mouse axis is being sent (not activity).</summary>
    public bool SendingMouseRest { get; set; }

    /// <summary><c>sMouseAxisRead</c>: an axis was read since the cursor was last drawn back.</summary>
    public bool MouseAxisRead { get; set; }
}

/// <summary>
/// <c>CPCController</c> on <c>CController</c>: each flush maps the bindings' keys, mouse and joysticks to button actions
/// and sends them to the object on top of the controller's stack. Joysticks are absent (as in the replay harness), so
/// their inputs read 0.
/// </summary>
/// <remarks>
/// Not modelled: the controller's own demo recording and playback (<c>mRecording</c>, <c>mPlaying</c>: nothing on the
/// level-100 path starts them), and the reconnect interface's gating in <c>SendButtonAction</c> (PC controllers are
/// always connected, so it never blocks).
/// </remarks>
public sealed class Controller
{
    private const float InitialRepeatDelay = 0.5f;
    private const float RepeatDelay = 0.12f;
    private const float AnalogueActAsDigitalThreshold = 0.9f;

    // UpdateMouseCentre's reciprocal: (float)(1.0 / 1.7) = 0x3f169697.
    private static readonly float InverseOnePointSeven = BitConverter.Int32BitsToSingle(0x3f169697);

    private readonly List<IButtonReceiver?> _stack = [];
    private uint _buttons1, _buttons2, _buttons3, _buttons1Old, _buttons2Old, _buttons3Old;

    public Controller(int pad, int configuration, IButtonReceiver? toControl)
    {
        Pad = pad;
        Configuration = configuration;
        SetToControl(toControl);
    }

    public int Pad { get; }
    public int Configuration { get; }

    /// <summary><c>mLastRepeatTime</c> and <c>mNextDelayTime</c>: the shared auto-repeat timer.</summary>
    public float LastRepeatTime { get; private set; } = -1f;
    public float NextDelayTime { get; private set; } = 0.1f;

    /// <summary><c>mPausedBeforeMap</c>: the game's pause state when the flush began.</summary>
    public bool PausedBeforeMap { get; private set; }

    /// <summary><c>SetToControl</c> (0x0042e610): the new object goes on top of the stack.</summary>
    public void SetToControl(IButtonReceiver? receiver) => _stack.Insert(0, receiver);

    /// <summary><c>RelinquishControl</c> (0x0042e6e0): the top object leaves the stack.</summary>
    public void RelinquishControl()
    {
        if (_stack.Count > 0) _stack.RemoveAt(0);
    }

    public IButtonReceiver? ToControl => _stack.Count > 0 ? _stack[0] : null;

    public void SetButton(int n)
    {
        if (n >= 64) _buttons3 |= 1u << (n - 64);
        else if (n >= 32) _buttons2 |= 1u << (n - 32);
        else _buttons1 |= 1u << n;
    }

    public void UnsetButton(int n)
    {
        if (n >= 64) _buttons3 &= ~(1u << (n - 64));
        else if (n >= 32) _buttons2 &= ~(1u << (n - 32));
        else _buttons1 &= ~(1u << n);
    }

    public bool IsButtonSet(int n)
    {
        uint b;
        if (n >= 64) b = _buttons3 & (1u << (n - 64));
        else if (n >= 32) b = _buttons2 & (1u << (n - 32));
        else b = _buttons1 & (1u << n);
        return b != 0;
    }

    /// <summary>
    /// <c>IsButtonOldSet</c>, with retail's missing <c>else</c>: for buttons 64 and up the third word's result is then
    /// replaced by the second word shifted by (n - 32), whose count the shift instruction takes modulo 32.
    /// </summary>
    public bool IsButtonOldSet(int n)
    {
        uint b = 0;
        if (n >= 64) b = _buttons3Old & (1u << (n - 64));
        if (n >= 32) b = _buttons2Old & (1u << (n - 32));
        else b = _buttons1Old & (1u << n);
        return b != 0;
    }

    /// <summary><c>CController::Flush</c> (0x0042d9d0): this flush's buttons become the old ones, then the mappings run.</summary>
    public void Flush(ControllerContext context)
    {
        _buttons1Old = _buttons1;
        _buttons2Old = _buttons2;
        _buttons3Old = _buttons3;
        _buttons1 = _buttons2 = _buttons3 = 0;
        DoMappings(context);
    }

    /// <summary><c>CController::ResetInactivityTimer</c> (0x0042d7a0).</summary>
    public static void ResetInactivityTimer(ControllerShared shared, float sysTime) =>
        shared.LastTimeAnythingPressed = shared.NonInteractiveSection ? 0f : sysTime;

    private bool RepeatOccurred(bool repeatOn, int button, float sysTime)
    {
        if (!IsButtonOldSet(button))
        {
            NextDelayTime = InitialRepeatDelay;
            LastRepeatTime = sysTime;
            return true;
        }
        if (repeatOn)
        {
            NextDelayTime = RepeatDelay;
            return true;
        }
        SetButton(button);
        return false;
    }

    /// <summary>
    /// <c>CController::DoMappings</c> (0x0042db40): every binding in order; a single-player controller 0 reads both of a
    /// binding's inputs, another controller reads its own. A button already sent this flush is not sent again. Both
    /// shift keys together send the front-end cheat.
    /// </summary>
    private void DoMappings(ControllerContext c)
    {
        float sysTime = c.SysTime();
        bool repeatOn = false;
        // x87: the difference rounds to 24 bits and compares exactly with the float delay.
        if (X87.Sub(c.SysTime(), LastRepeatTime) > NextDelayTime)
        {
            LastRepeatTime = sysTime;
            repeatOn = true;
        }
        PausedBeforeMap = c.Game.IsPaused;

        int count = c.Bindings.Count;
        for (int binding = 0; binding < count; binding++)
        {
            int id = c.Bindings.Id(binding);
            int input = Pad;
            while (true)
            {
                if (c.Bindings.InputPad(binding, input) != -1 && !IsButtonSet(id))
                    MapInput(c, binding, input, id, repeatOn, sysTime);

                if (c.Game.IsMultiplayer || input != 0) break;
                input = 1;
            }
        }

        if (c.Input.KeyOn(Dik.LShift) && c.Input.KeyOn(Dik.RShift)) SendButtonAction(c, Buttons.FrontEndCheat, 0f);
    }

    private void MapInput(ControllerContext c, int binding, int input, int id, bool repeatOn, float sysTime)
    {
        int key = c.Bindings.InputKey(binding, input);
        PushType type = c.Bindings.InputType(binding, input);
        switch (type)
        {
            // No joysticks: their buttons never press and their axes read 0.
            case PushType.ButtonOnce:
            case PushType.ButtonOn:
            case PushType.ButtonRepeat:
            case PushType.ButtonRelease:
                break;

            case PushType.AnaloguePlus:
            case PushType.AnalogueMinus:
                break;

            case PushType.AnaloguePlusActAsButtonRepeat:
                if (MappedInputValue(key) > AnalogueActAsDigitalThreshold && RepeatOccurred(repeatOn, id, sysTime))
                    SendButtonAction(c, id, 1f);
                break;

            case PushType.AnalogueMinusActAsButtonRepeat:
                if (MappedInputValue(key) < -AnalogueActAsDigitalThreshold && RepeatOccurred(repeatOn, id, sysTime))
                    SendButtonAction(c, id, 1f);
                break;

            case PushType.KeyOnce:
                if (c.Input.KeyOnceThisFrame(key)) SendButtonAction(c, id, 1f);
                break;

            case PushType.KeyRelease:
                if (c.Input.KeyUp(key)) SendButtonAction(c, id, 1f);
                break;

            case PushType.KeyOn:
                if (c.Input.KeyOn(key)) SendButtonAction(c, id, DigitalValueForButton(id));
                break;

            case PushType.MouseAxisPlus:
            case PushType.MouseAxisMinus:
                if (c.Shared.MouseAxisLocked[Pad]) break;
                goto case PushType.MouseAxisPlusAny;
            case PushType.MouseAxisPlusAny:
            case PushType.MouseAxisMinusAny:
            {
                Mouse mouse = c.Input.Mouse;
                // x87: the cursor's distance (an exact int load) times 0.0043333333f, times the sensitivity, each
                // rounded to 24 bits.
                double value = key == 0
                    ? X87.Mul(mouse.X - (c.Input.Window.Width >> 1), 0.0043333333f)
                    : X87.Mul(mouse.Y - (c.Input.Window.Height >> 1), 0.0043333333f);
                float val = (float)X87.Mul(c.Shared.MouseSensitivity, value);
                if (val < -1f) val = -1f;
                else if (val > 1f) val = 1f;
                if ((type == PushType.MouseAxisPlusAny || type == PushType.MouseAxisPlus) && val < 0f) val = 0f;
                if ((type == PushType.MouseAxisMinusAny || type == PushType.MouseAxisMinus) && val > 0f) val = 0f;
                if (c.DirectInputCreated)
                {
                    if (val == 0f) c.Shared.SendingMouseRest = true;
                    SendButtonAction(c, id, val);
                    c.Shared.SendingMouseRest = false;
                }
                c.Shared.MouseAxisRead = true;
                break;
            }

            case PushType.MouseButtonOnce:
            case PushType.MouseButtonRelease:
            {
                Mouse mouse = c.Input.Mouse;
                bool fire = key switch
                {
                    0 => mouse.Buttons[0].Released,
                    1 => mouse.Buttons[1].Released,
                    2 => mouse.Buttons[2].Released,
                    3 => mouse.Wheel > 0,
                    4 => mouse.Wheel < 0,
                    _ => false,
                };
                if (fire) SendButtonAction(c, id, 1f);
                break;
            }

            case PushType.MouseButtonOn:
            {
                Mouse mouse = c.Input.Mouse;
                if (key is >= 0 and <= 2 && mouse.Held[key]) SendButtonAction(c, id, 1f);
                break;
            }
        }
    }

    /// <summary>
    /// <c>GetMappedInputValue</c> (0x0042e3d0) with no joystick attached: every axis reads 0 (inside the 0.15 dead zone)
    /// and every button 0.
    /// </summary>
    private static float MappedInputValue(int button) => button < 0 ? 0f : 0f;

    /// <summary>A held digital control is a full deflection, negative for the left, up and forward ones.</summary>
    public static float DigitalValueForButton(int button) => button switch
    {
        Buttons.MechYawLeft or Buttons.MechPitchUp or Buttons.MechStrafeLeft or Buttons.MechForward
            or Buttons.CameraPitchDown or Buttons.CameraYawLeft or Buttons.CameraMoveForward or Buttons.CameraStrafeLeft => -1f,
        _ => 1f,
    };

    /// <summary>
    /// <c>CController::SendButtonAction</c> (0x0042e4d0): the button is marked sent, any press but a centred mouse axis
    /// resets the inactivity timer, debug buttons outside the front end go to the game, and other buttons reach the
    /// controlled object unless the game was paused and it cannot be controlled in pause. The reconnect interface is
    /// always connected on PC and is not modelled.
    /// </summary>
    public void SendButtonAction(ControllerContext c, int button, float value)
    {
        SetButton(button);
        if (_stack.Count == 0) return;
        IButtonReceiver? to = _stack[0];
        if (!c.Shared.SendingMouseRest) ResetInactivityTimer(c.Shared, c.SysTime());
        if (to is null) return;

        if (button < Buttons.StartActionButtons)
        {
            if (to.ControlType != ControlType.FrontEnd) c.Game.ReceiveButtonAction(this, button, value);
            else to.ReceiveButtonAction(this, button, value);
            return;
        }
        if (!PausedBeforeMap || to.CanBeControlledWhenInPause) to.ReceiveButtonAction(this, button, value);
    }

    /// <summary>
    /// <c>UpdateMouseCentre</c> (0x0042da00): after the mouse steered, the cursor is drawn 7/17 of the way back to the
    /// window's centre, at least one pixel. x87: each operation rounds to 24 bits, and the conversions are /QIfist's.
    /// </summary>
    public static void UpdateMouseCentre(PlatformInput input, ControllerShared shared, bool reset)
    {
        Mouse mouse = input.Mouse;
        if (reset)
        {
            mouse.X = input.Window.Width / 2;
            mouse.Y = input.Window.Height / 2;
        }
        if (!shared.MouseAxisRead) return;

        int x = mouse.X, y = mouse.Y;
        int cx = input.Window.Width >> 1;
        int cy = input.Window.Height >> 1;
        int dx = X87.Fist(X87.Sub(X87.Mul(X87.Add(X87.Mul(cx, 0.7f), x), InverseOnePointSeven), x));
        int dy = X87.Fist(X87.Sub(X87.Mul(X87.Add(X87.Mul(cy, 0.7f), y), InverseOnePointSeven), y));
        if (cx != x && dx == 0) dx = x > cx ? -1 : 1;
        if (cy != y && dy == 0) dy = y > cy ? -1 : 1;
        mouse.X = x + dx;
        mouse.Y = y + dy;
        shared.MouseAxisRead = false;
    }
}

/// <summary>What one controller flush reads: the bindings, the platform input, the shared state, the game and the clock.</summary>
public sealed class ControllerContext(ControlBindings bindings, PlatformInput input, ControllerShared shared, IControllerGame game, Func<float> sysTime)
{
    public ControlBindings Bindings { get; } = bindings;
    public PlatformInput Input { get; } = input;
    public ControllerShared Shared { get; } = shared;
    public IControllerGame Game { get; } = game;
    public Func<float> SysTime { get; } = sysTime;

    /// <summary>Whether the DirectInput mouse exists (<c>sMouse</c>): mouse axes send only then.</summary>
    public bool DirectInputCreated { get; set; }
}
