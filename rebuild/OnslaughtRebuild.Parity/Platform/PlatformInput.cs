// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/ltshell.cpp and src/ltshell.h (PCLTShell's message handling and key state) and
// src/PCPlatform.cpp (CPCPlatform::Process, KeyOn, KeyOnce).

namespace OnslaughtRebuild.Parity.Platform;

/// <summary>A key trap's event codes (<c>KEY_EVENT_*</c>) for keys.</summary>
public static class KeyTrapEvent
{
    public const int Up = 3;
    public const int Down = 4;
    public const int Char = 5;
}

/// <summary>A window message posted to the game window: a key or a mouse message with its wParam and lParam.</summary>
public readonly record struct WindowMessage(int Message, int WParam, int LParam)
{
    public const int KeyDownMessage = 0x0100;
    public const int KeyUpMessage = 0x0101;
}

/// <summary>
/// The platform's input layer: the game window's message queue and its handling (<c>PCLTShell::MsgProc</c> 0x00512e40,
/// <c>HandlingSystemStuff</c> 0x00512490), the key state by scan code, and the mouse. One instance stands for the
/// game's <c>LT</c> input state; the front end and the level loop both pump it through <see cref="Process"/>.
/// Tape events are posted with <see cref="PostKeyMessage"/> and <see cref="PostMouseMessage"/> at Present boundaries.
/// </summary>
/// <remarks>
/// Assumed constant in play: <c>CLIPARAMS.mAutoConfigTest</c> is false (it is set only by its command-line switch,
/// CLIParams.cpp:280), so the key and mouse gating on it never applies; the window is active and ready; the D3D device
/// is not lost. Not modelled yet: <c>WM_CHAR</c> (which <c>TranslateMessage</c> generates and only a key trap reads),
/// the console's key binds (<c>CONSOLE.HandleBind</c>), joysticks, and the inactive-window path (<c>GetMessage</c>).
/// </remarks>
public sealed class PlatformInput
{
    private const int MaxKeysRead = 32;

    private readonly Queue<WindowMessage> _queue = new();
    private readonly List<int> _keysRead = new(MaxKeysRead);
    private long? _lastElapsedCounter;

    public PlatformInput(PlatformWindow window) => Window = window;

    public PlatformWindow Window { get; }
    public Mouse Mouse { get; } = new();

    /// <summary><c>KeyDown</c>: held, by scan code (extended keys above 0x80).</summary>
    public bool[] KeyDown { get; } = new bool[256];

    /// <summary><c>KeyWasDown</c>: pressed since last read.</summary>
    public bool[] KeyWasDown { get; } = new bool[256];

    /// <summary><c>KeyWasUp</c>: released since last cleared.</summary>
    public bool[] KeyWasUp { get; } = new bool[256];

    /// <summary><c>gKeyPressed</c>: set by any key press or mouse button press.</summary>
    public bool KeyPressed { get; set; }

    /// <summary>The installed key trap (<c>mCurrentKeytrap</c>), if any: it sees keys and mouse buttons first.</summary>
    public Func<int, int, bool>? KeyTrap { get; set; }

    /// <summary>Messages posted and not yet handled.</summary>
    public int Pending => _queue.Count;

    /// <summary>
    /// The live host's suspension boundary: drop undelivered window messages and the frame-local once-read cache
    /// without invoking key traps. This is not retail FlushInputBuffers; its existing semantics stay unchanged.
    /// </summary>
    public void DiscardPendingMessages()
    {
        _queue.Clear();
        _keysRead.Clear();
    }

    /// <summary>Posts a key message as a tape's K event delivers it: wParam the virtual key, lParam carrying the scan code.</summary>
    public void PostKeyMessage(int virtualKey, int lParam, bool down) =>
        _queue.Enqueue(new WindowMessage(down ? WindowMessage.KeyDownMessage : WindowMessage.KeyUpMessage, virtualKey, lParam));

    /// <summary>
    /// Posts a tape's K event as the shim does: wParam the virtual key, lParam = 1 | (scan &lt;&lt; 16), with 0xC0000000
    /// added for a key-up.
    /// </summary>
    public void PostTapeKey(int virtualKey, int scanCode, bool down) =>
        PostKeyMessage(virtualKey, unchecked(1 | (scanCode << 16) | (down ? 0 : (int)0xC0000000)), down);

    /// <summary>Posts a mouse message as a tape's W event delivers it: lParam = MAKELPARAM(x, y) in client pixels.</summary>
    public void PostMouseMessage(MouseMessage message, int x, int y, int wParam) =>
        _queue.Enqueue(new WindowMessage((int)message, wParam, (y << 16) | (x & 0xffff)));

    /// <summary>
    /// <c>CPCPlatform::Process</c> (0x00515880), which calls <c>PCLTShell::HandlingSystemStuff</c> (0x00512490) until it
    /// returns false: each call handles one queued message, and the call that finds the queue empty does the frame step.
    /// That step (ltshell.cpp:730-844) returns early when <c>0.0f == fElapsedAppTime &amp;&amp; m_bFrameMoving</c>, where the
    /// elapsed time is the frame timer's since its last check. <c>m_bFrameMoving</c> is always true (set once, in the
    /// D3D application's constructor, d3dapp.cpp:165), and the elapsed time is zero exactly when the performance counter
    /// has not moved, so the step returns early when no Present has happened since the last check; otherwise it starts
    /// a new frame's list of keys read once. <paramref name="performanceCounter"/> is the platform clock's reading.
    /// </summary>
    public void Process(long performanceCounter)
    {
        while (_queue.TryDequeue(out WindowMessage message)) Handle(message);

        // The frame timer's elapsed time is zero exactly when the counter has not moved since its last check.
        bool elapsed = _lastElapsedCounter != performanceCounter;
        _lastElapsedCounter = performanceCounter;
        if (!elapsed) return;
        _keysRead.Clear();
    }

    /// <summary><c>PCLTShell::MsgProc</c> for one message: keys by scan code, then mouse messages.</summary>
    private void Handle(WindowMessage message)
    {
        // Keys are recorded by scan code (lParam bits 16-23), extended keys (bit 24) above 0x80.
        int key = (message.LParam >> 16) & 0xff;
        if ((message.LParam & 0x1000000) != 0) key = (key + 0x80) & 0xff;

        if (message.Message == WindowMessage.KeyDownMessage)
        {
            KeyPressed = true;
            if (KeyTrap is not null)
            {
                KeyTrap(key, KeyTrapEvent.Down);
            }
            else
            {
                KeyDown[key] = true;
                KeyWasDown[key] = true;
            }
        }
        else if (message.Message == WindowMessage.KeyUpMessage)
        {
            KeyTrap?.Invoke(key, KeyTrapEvent.Up);
            // Key-ups propagate even when trapped, to avoid stuck keys.
            KeyDown[key] = false;
            KeyWasUp[key] = true;
        }
        else if (message.Message >= (int)MouseMessage.Move && message.Message <= (int)MouseMessage.Wheel)
        {
            if (Mouse.HandleMessage((MouseMessage)message.Message, message.WParam, message.LParam, Window, KeyTrap)) KeyPressed = true;
        }
    }

    /// <summary><c>CPCPlatform::KeyOn</c> (0x00515970): held.</summary>
    public bool KeyOn(int key) => KeyDown[key];

    /// <summary><c>CPCPlatform::KeyOnce</c> (0x00515980): pressed since last read, and cleared by reading.</summary>
    public bool KeyOnce(int key)
    {
        bool pressed = KeyWasDown[key];
        KeyWasDown[key] = false;
        return pressed;
    }

    /// <summary><c>PCLTShell::xKeyOnceThisFrame</c> (0x00513a90): a key read once stays readable for the rest of the frame.</summary>
    public bool KeyOnceThisFrame(int key)
    {
        bool pressed = KeyWasDown[key];
        KeyWasDown[key] = false;
        if (pressed)
        {
            if (_keysRead.Count < MaxKeysRead) _keysRead.Add(key);
            return true;
        }
        return _keysRead.Contains(key);
    }

    /// <summary><c>PCLTShell::xKeyUp</c> (0x00513a80).</summary>
    public bool KeyUp(int key) => KeyWasUp[key];

    /// <summary><c>PCLTShell::ClearKeyWasUp</c> (0x00512470): run at the start of each front-end process.</summary>
    public void ClearKeyWasUp() => Array.Clear(KeyWasUp);

    /// <summary><c>PCLTShell::FlushInputBuffers</c> (0x00512fc0): every key released and unread.</summary>
    public void FlushInputBuffers()
    {
        Array.Clear(KeyDown);
        Array.Clear(KeyWasDown);
        Array.Clear(KeyWasUp);
    }
}
