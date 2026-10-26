// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Host;

/// <summary>
/// Frame-pumped acknowledgement of Godot's asynchronous output request before live-session construction.
/// An exact getter value is logical acknowledgement only; it does not prove a physical playback route.
/// </summary>
internal sealed class LiveAudioOutputStartup
{
    internal const int MaxFrames = 300;
    internal const ulong TimeoutMilliseconds = 5_000;
    private readonly string _device;
    private readonly Func<string> _driver;
    private readonly Func<string[]> _outputs;
    private readonly Func<string> _current;
    private readonly Func<ulong> _milliseconds;
    private readonly ulong _began;
    private ulong _last;
    private int _frames;
    private Action? _start;

    internal LiveAudioOutputStartup(string device, Func<string> driver, Func<string[]> outputs,
        Func<string> current, Action<string> request, Func<ulong> milliseconds, Action start)
    {
        _device = device;
        _driver = driver;
        _outputs = outputs;
        _current = current;
        _milliseconds = milliseconds;
        _start = start;
        _began = _last = milliseconds();
        ValidateAvailable();
        // The setter queues work on the audio thread; do not assert its getter immediately or spin here.
        request(device);
    }

    internal bool Pending => _start is not null;

    /// <summary>Pending polls leave simulation alone; acknowledgement invokes normal startup exactly once.</summary>
    internal bool Pump(double frameDelta)
    {
        if (_start is null) return false;
        try
        {
            if (!double.IsFinite(frameDelta) || frameDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(frameDelta), "Live audio startup needs a finite, nonnegative host delta.");
            ulong now = _milliseconds();
            if (now < _last) throw new InvalidOperationException("The live audio startup clock moved backwards.");
            _last = now;
            if (now - _began >= TimeoutMilliseconds)
                throw new TimeoutException("The audio driver did not acknowledge the selected output within five seconds.");
            ValidateAvailable();
            _frames++;
            if (string.Equals(_current(), _device, StringComparison.Ordinal))
            {
                Action start = _start;
                _start = null;
                start();
                return true;
            }

            if (_frames >= MaxFrames)
                throw new TimeoutException("The audio driver did not acknowledge the selected output within 300 host frames.");
            return false;
        }
        catch
        {
            Cancel();
            throw;
        }
    }

    internal void Cancel() => _start = null;

    private void ValidateAvailable()
    {
        if (string.Equals(_driver(), "Dummy", StringComparison.Ordinal))
            throw new NotSupportedException("Real live audio requires a real audio driver; Dummy is not playback admission.");
        if (!_outputs().Contains(_device, StringComparer.Ordinal))
            throw new NotSupportedException("The explicitly selected live audio output device is unavailable.");
    }
}
