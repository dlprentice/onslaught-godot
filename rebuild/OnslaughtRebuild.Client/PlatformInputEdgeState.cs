// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

public readonly record struct PlatformInputKeyByte(int KeyCode, byte Value);

public readonly record struct PlatformInputJoyButtonByte(int Joypad, int Button, byte Value);

public sealed record PlatformInputEdgeSnapshot(
    long FrameIndex,
    long ResetGeneration,
    IReadOnlyList<PlatformInputKeyByte> HeldKeys,
    IReadOnlyList<PlatformInputKeyByte> ConsumeOnceKeys,
    IReadOnlyList<PlatformInputJoyButtonByte> PreviousJoyButtons,
    IReadOnlyList<PlatformInputJoyButtonByte> CurrentJoyButtons);

/// <summary>
/// Deterministic host-input bytes kept outside Core and native input APIs.
/// </summary>
/// <remarks>
/// Stuart's <c>PCLTShell</c> keeps separate held and one-shot key bytes
/// (<c>ltshell.h:78-79,291-292</c>); the Steam bodies at
/// <c>0x00515970</c>/<c>0x00515980</c> independently confirm held read and
/// consume-and-clear. Its joy helpers compare previous/current button bytes for
/// rising, held, and falling predicates (<c>ltshell.h:306-319</c>).
///
/// <para>This owner carries only those laws. Key identifiers and joypad/button
/// identifiers come from the host adapter, so its sparse maps do not claim the
/// source table's runtime owner or a DirectInput layout. The host explicitly
/// advances one frame after event routing and resets the state at lifecycle,
/// pause, and focus-loss boundaries. Retail key-repeat policy and joystick poll
/// cadence remain unresolved.</para>
/// </remarks>
public sealed class PlatformInputEdgeState
{
    private readonly Dictionary<int, byte> _heldKeys = [];
    private readonly Dictionary<int, byte> _consumeOnceKeys = [];
    private readonly Dictionary<JoyButtonId, byte> _previousJoyButtons = [];
    private readonly Dictionary<JoyButtonId, byte> _currentJoyButtons = [];

    public long FrameIndex { get; private set; }

    public long ResetGeneration { get; private set; }

    public void ObserveKey(int keyCode, bool pressed, bool echo)
    {
        // Godot marks OS repeat messages as Echo. The existing gameplay adapter
        // has always rejected those messages, so keep that no-repeat policy at
        // this boundary. Retail's runtime repeat policy beyond the proven local
        // held/read-and-clear bodies remains unresolved.
        if (echo)
        {
            return;
        }

        if (pressed)
        {
            // A non-echo host event is a discrete observed press. Latch it even
            // if the held byte is already set; deciding whether such events are
            // hardware repeats belongs to the adapter, not this state owner.
            _consumeOnceKeys[keyCode] = 1;
            _heldKeys[keyCode] = 1;
        }
        else
        {
            _heldKeys.Remove(keyCode);
        }
    }

    public byte GetHeldKey(int keyCode) =>
        _heldKeys.TryGetValue(keyCode, out byte value) ? value : (byte)0;

    public byte ConsumeKeyOnce(int keyCode)
    {
        if (!_consumeOnceKeys.Remove(keyCode, out byte value))
        {
            return 0;
        }

        return value;
    }

    public void ObserveJoyButton(int joypad, int button, byte value)
    {
        var id = new JoyButtonId(joypad, button);
        if (value == 0)
        {
            _currentJoyButtons.Remove(id);
        }
        else
        {
            _currentJoyButtons[id] = value;
        }
    }

    public byte GetPreviousJoyButton(int joypad, int button) =>
        GetJoyButton(_previousJoyButtons, new JoyButtonId(joypad, button));

    public byte GetCurrentJoyButton(int joypad, int button) =>
        GetJoyButton(_currentJoyButtons, new JoyButtonId(joypad, button));

    public bool IsJoyButtonRising(int joypad, int button) =>
        GetPreviousJoyButton(joypad, button) == 0 &&
        GetCurrentJoyButton(joypad, button) != 0;

    public bool IsJoyButtonHeld(int joypad, int button) =>
        GetCurrentJoyButton(joypad, button) != 0;

    public bool IsJoyButtonFalling(int joypad, int button) =>
        GetPreviousJoyButton(joypad, button) != 0 &&
        GetCurrentJoyButton(joypad, button) == 0;

    public void AdvanceFrame()
    {
        _previousJoyButtons.Clear();
        foreach ((JoyButtonId id, byte value) in _currentJoyButtons)
        {
            _previousJoyButtons.Add(id, value);
        }
        FrameIndex++;
    }

    public void Reset()
    {
        _heldKeys.Clear();
        _consumeOnceKeys.Clear();
        _previousJoyButtons.Clear();
        _currentJoyButtons.Clear();
        ResetGeneration++;
    }

    public PlatformInputEdgeSnapshot Capture() => new(
        FrameIndex,
        ResetGeneration,
        CaptureKeys(_heldKeys),
        CaptureKeys(_consumeOnceKeys),
        CaptureJoyButtons(_previousJoyButtons),
        CaptureJoyButtons(_currentJoyButtons));

    private static IReadOnlyList<PlatformInputKeyByte> CaptureKeys(
        IReadOnlyDictionary<int, byte> keys) =>
        Array.AsReadOnly(keys
            .OrderBy(pair => pair.Key)
            .Select(pair => new PlatformInputKeyByte(pair.Key, pair.Value))
            .ToArray());

    private static IReadOnlyList<PlatformInputJoyButtonByte> CaptureJoyButtons(
        IReadOnlyDictionary<JoyButtonId, byte> buttons) =>
        Array.AsReadOnly(buttons
            .OrderBy(pair => pair.Key.Joypad)
            .ThenBy(pair => pair.Key.Button)
            .Select(pair => new PlatformInputJoyButtonByte(
                pair.Key.Joypad,
                pair.Key.Button,
                pair.Value))
            .ToArray());

    private static byte GetJoyButton(
        IReadOnlyDictionary<JoyButtonId, byte> buttons,
        JoyButtonId id) =>
        buttons.TryGetValue(id, out byte value) ? value : (byte)0;

    private readonly record struct JoyButtonId(int Joypad, int Button);
}
