// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Host;

/// <summary>Owns the native keyboard transitions forwarded by the whole-game flow Host.</summary>
internal sealed class FlowKeyboardInput(Action<int, int, bool> postKey, bool replay, bool focused)
{
    private readonly List<(int VirtualKey, int ScanCode)> _held = [];
    private bool _focused = focused;
    private bool _stopped;

    public bool Observe(int virtualKey, int scanCode, bool down)
    {
        if (_stopped || replay || !_focused) return false;

        var key = (virtualKey, scanCode);
        if (down)
        {
            if (!_held.Contains(key)) _held.Add(key);
        }
        else
        {
            _held.Remove(key);
        }
        postKey(virtualKey, scanCode, down);
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
