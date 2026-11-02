// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Host;

/// <summary>Owns the native key transitions and text forwarded by the whole-game flow Host.</summary>
internal sealed class FlowKeyboardInput(
    Action<int, int, bool> postKey, Action<char, int> postCharacter, bool replay, bool focused)
{
    private readonly List<(int VirtualKey, int ScanCode)> _held = [];
    private bool _focused = focused;
    private bool _stopped;

    public bool Observe(int virtualKey, int scanCode, bool down, long unicode = 0, bool echo = false)
    {
        if (_stopped || replay || !_focused) return false;

        var key = (virtualKey, scanCode);
        if (echo)
        {
            // A repeat may type again, but must not recreate a key cleared by
            // focus loss or post another physical transition.
            return down && _held.Contains(key) && PostCharacter(unicode, scanCode);
        }
        if (down)
        {
            if (!_held.Contains(key)) _held.Add(key);
        }
        else
        {
            _held.Remove(key);
        }
        postKey(virtualKey, scanCode, down);
        if (down) PostCharacter(unicode, scanCode);
        return true;
    }

    private bool PostCharacter(long unicode, int scanCode)
    {
        // The core character message carries one UTF-16 code unit. Never
        // truncate a supplementary code point or forward a lone surrogate.
        if (unicode < 0 || unicode > char.MaxValue) return false;
        char character = (char)unicode;
        if (char.IsControl(character) || char.IsSurrogate(character)) return false;

        // Backspace, Enter and other controls already use the physical path.
        postCharacter(character, scanCode);
        return true;
    }

    public void SetFocused(bool focused)
    {
        if (_stopped) return;
        _focused = focused;
        if (focused) return;

        // Clearing Godot's pressed state does not release keys already forwarded
        // into the simulation's message queue. Preserve their press order here.
        foreach (var key in _held) postKey(key.VirtualKey, key.ScanCode, false);
        _held.Clear();
    }

    public void Stop()
    {
        _stopped = true;
        _held.Clear();
    }
}
