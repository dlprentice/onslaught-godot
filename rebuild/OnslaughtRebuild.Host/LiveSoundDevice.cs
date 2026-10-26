// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Host;

/// <summary>The small real-player boundary, also allowing engine-free adverse controls of the device adapter.</summary>
public interface ILiveAudioPlayer : IDisposable
{
    bool Playing { get; }
    float PlaybackPosition { get; }
    bool Paused { get; set; }
    void Play(float fromSeconds);
    void Stop();
    void SetLooping(bool looping);
    void SetPitch(float pitch);
    void SetVolume(float decibels, bool muted);
    void SetPosition(FVector position);
}

/// <summary>
/// Main-thread live-device adapter. Status is a real player observation, never a sample-length timer. Native event
/// flags and callbacks remain with PcSoundEventBackend/SoundEffects. Spatial attenuation follows the selected CE
/// no-virtualization curve; Godot supplies spatial panning/resampling, which is not waveform-equivalence evidence.
/// </summary>
public sealed class LiveSoundDevice(Func<SampleMetadata, SamplePcmData> pcm,
    Func<SamplePcmData, ILiveAudioPlayer?> createPlayer) : ISoundBufferDevice, IDisposable
{
    private readonly int _thread = Environment.CurrentManagedThreadId;
    private readonly HashSet<Buffer> _buffers = [];
    private ulong _generation;
    private bool _disposed, _suspended;
    public int BufferCount => _buffers.Count;
    private void Check()
    {
        if (_thread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Live audio belongs to the Godot main thread.");
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
    public ISoundPlaybackBuffer? Duplicate(SampleMetadata sample, SampleBufferFormat format, SoundBufferToken token)
    {
        Check();
        if (token.Channel is < 0 or >= LiveSoundSources.Channels || token.Generation <= _generation
            || _buffers.Any(b => b.Token.Channel == token.Channel))
            throw new InvalidDataException("A live duplicate requires a fresh generation and an unowned channel.");
        SamplePcmData data = pcm(sample);
        if (!data.FullyInitialized || data.Format != format)
            throw new InvalidDataException("The real source PCM disagrees with the duplicate descriptor.");
        _generation = token.Generation;
        ILiveAudioPlayer? player = createPlayer(data);
        if (player is null) return null;
        var buffer = new Buffer(this, player, format, token);
        _buffers.Add(buffer);
        try { player.Paused = _suspended; return buffer; }
        catch { buffer.Release(true); throw; }
    }
    public void SetListener(SoundListenerParameters p)
    {
        Check();
        if (p != new SoundListenerParameters(default, default, new(0, 1, 0), new(0, 0, -1), .7f, 1, 1))
            throw new NotSupportedException("The live device admits only the native camera-local zero-velocity listener.");
    }
    public void CommitDeferredSettings()
    {
        Check();
        foreach (var buffer in _buffers) buffer.Commit();
    }
    // Host focus suspension does not mutate core pause/event state, clocks or callbacks. Resume before any core step.
    public void SetSuspended(bool suspended)
    {
        Check(); _suspended = suspended;
        foreach (var buffer in _buffers) buffer.Player.Paused = suspended;
    }
    public void Dispose()
    {
        if (_disposed) return;
        Check();
        // Try every owned resource even if a platform release fails. No partially live device survives a fault.
        Exception? failure = null;
        foreach (var buffer in _buffers.ToArray())
            try { buffer.Release(true); } catch (Exception e) { failure ??= e; }
        _disposed = true;
        if (failure is not null) throw new InvalidOperationException("Live audio teardown failed.", failure);
    }

    private sealed class Buffer(LiveSoundDevice owner, ILiveAudioPlayer player, SampleBufferFormat format,
        SoundBufferToken token) : ISoundPlaybackBuffer
    {
        public ILiveAudioPlayer Player { get; } = player;
        public SoundBufferToken Token { get; } = token;
        public uint Frequency { get; private set; } = format.SampleRate;
        private uint _cursor;
        private bool _released, _stopped = true, _looping;
        private int _volume;
        private FVector _position, _pending;
        private bool _dirty;
        private void Check() { owner.Check(); ObjectDisposedException.ThrowIf(_released, this); }
        public SoundBufferObservation GetStatus()
        {
            Check();
            bool playing = !_stopped && Player.Playing;
            if (!playing && !_stopped) { _cursor = 0; _stopped = true; }
            return new(Token, playing && !Player.Paused ? (_looping ? 5u : 1u) : 0u);
        }
        public SoundBufferCursor GetCurrentPosition() => throw new NotSupportedException(
            "Godot exposes playback position but no native hardware write cursor; channel cursor inspection is unsupported.");
        private uint ObservedCursor()
        {
            if (_stopped) return _cursor;
            if (!Player.Playing) return 0;
            double frames = Math.Floor(Math.Max(0, Player.PlaybackPosition) * format.SampleRate);
            if (!double.IsFinite(frames)) throw new InvalidDataException("The audio device reported a non-finite cursor.");
            return (uint)(frames % format.Frames) * (uint)format.BlockAlign;
        }
        public void SetCurrentPosition(uint byteOffset)
        {
            Check();
            _cursor = byteOffset % format.BufferBytes / (uint)format.BlockAlign * (uint)format.BlockAlign;
            if (!_stopped) Player.Play((float)(_cursor / (double)(format.SampleRate * format.BlockAlign)));
        }
        public bool Play(bool looping)
        {
            Check(); _looping = looping; Player.SetLooping(looping);
            Player.Play((float)(_cursor / (double)(format.SampleRate * format.BlockAlign)));
            // Play is a request. Its success and subsequent status come from the real source, not this request flag.
            _stopped = false;
            return Player.Playing;
        }
        public void Stop()
        {
            Check(); _cursor = ObservedCursor(); Player.Stop(); _stopped = true;
        }
        public void Release(bool stopFirst)
        {
            if (_released) return;
            Check();
            try { if (stopFirst) Stop(); }
            finally
            {
                _released = true; owner._buffers.Remove(this);
                Player.Dispose();
            }
        }
        public bool TrySetFrequency(uint frequency)
        {
            Check();
            if (frequency != 0 && frequency is < 100 or > 200000) return false;
            uint next = frequency == 0 ? format.SampleRate : frequency;
            Player.SetPitch(next / (float)format.SampleRate); Frequency = next; return true;
        }
        public bool SetVolume(int volume)
        {
            Check(); if (volume is < -10000 or > 0) return false;
            _volume = volume; ApplyVolume(); return true;
        }
        public bool SetSpatial(FVector position, bool deferred)
        {
            Check();
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                throw new NotSupportedException("Non-finite live spatial audio is unsupported.");
            if (deferred) { _pending = position; _dirty = true; }
            else { _position = position; _dirty = false; ApplySpatial(); }
            return true;
        }
        public void Commit()
        {
            if (_dirty) { _position = _pending; _dirty = false; ApplySpatial(); }
        }
        private void ApplySpatial() { Player.SetPosition(_position); ApplyVolume(); }
        private void ApplyVolume()
        {
            // CE port/dsound Buffer::Gains: min3/max50, rolloff.7, MUTE3DATMAXDISTANCE. Godot's own distance
            // attenuation is disabled so this does not apply two distance curves. Its panning remains Godot's.
            float distance = MathF.Sqrt(_position.X * _position.X + _position.Y * _position.Y + _position.Z * _position.Z);
            float attenuation = distance <= 3 ? 1 : 3 / (3 + .7f * (distance - 3));
            bool muted = _volume == -10000 || distance > 50;
            Player.SetVolume(_volume / 100f + 20 * MathF.Log10(attenuation), muted);
        }
    }
}
