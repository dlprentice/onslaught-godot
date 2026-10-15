// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.GodotClient;

// Godot 4.7.2 stops playback asynchronously: AudioServer marks it for deletion,
// then a later mix and main-thread callback release it. Remember every start,
// including tracks replaced just before Quit. These weak references neither
// own the playback nor change its audible lifetime.
internal sealed class AudioPlaybackRetirement : IDisposable
{
    private readonly List<WeakRef> _pending = [];

    public void Observe(AudioStreamPlayer player)
    {
        if (player.HasStreamPlayback())
        {
            // The observer is the sole managed consumer of playback handles;
            // dispose this binding so it cannot extend the native lifetime.
            using AudioStreamPlayback playback = player.GetStreamPlayback();
            Observe(playback);
        }
    }

    public void Observe(AudioStreamPlayer3D player)
    {
        if (player.HasStreamPlayback())
        {
            using AudioStreamPlayback playback = player.GetStreamPlayback();
            Observe(playback);
        }
    }

    private void Observe(AudioStreamPlayback playback)
    {
        _ = PendingCount;
        _pending.Add(GodotObject.WeakRef(playback) ??
            throw new InvalidOperationException("Audio playback was invalid at its start."));
    }

    public int PendingCount
    {
        get
        {
            for (int index = _pending.Count - 1; index >= 0; index--)
            {
                if (HasRetired(_pending[index]))
                {
                    _pending[index].Dispose();
                    _pending.RemoveAt(index);
                }
            }
            return _pending.Count;
        }
    }

    private static bool HasRetired(WeakRef playback)
    {
        // Inspect only the Variant type and release its temporary native Ref
        // before returning. AsGodotObject (and C# IsInstanceIdValid) can create
        // a new managed binding that keeps the playback alive until GC.
        using Variant reference = playback.GetRef();
        return reference.VariantType == Variant.Type.Nil;
    }

    public void Dispose()
    {
        foreach (WeakRef playback in _pending)
        {
            playback.Dispose();
        }
        _pending.Clear();
    }
}
