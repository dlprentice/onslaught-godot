// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Posts physical flow input through the stepped run's worker queue. Focus changes
/// are host observations; the host never changes the simulation from its UI thread.
/// </summary>
public sealed class FlowInputBridge(Action<Action<PlatformInput>> post)
{
    public bool Focused { get; private set; } = true;

    public bool QueueKey(int virtualKey, int scanCode, bool down)
    {
        if (!Focused) return false;
        post(input => input.PostTapeKey(virtualKey, scanCode, down));
        return true;
    }

    public void SetFocused(bool focused)
    {
        if (Focused == focused) return;
        Focused = focused;
        if (!focused) post(ReleaseAll);
    }

    private static void ReleaseAll(PlatformInput input)
    {
        input.DiscardPendingMessages();
        input.FlushInputBuffers();
        input.KeyPressed = false;
        Array.Clear(input.Mouse.Held);
        input.Mouse.ResetTransientState();
    }
}
