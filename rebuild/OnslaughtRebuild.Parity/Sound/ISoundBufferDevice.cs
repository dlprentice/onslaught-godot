// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Maths;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>Process-local immutable duplicate identity. Generations never wrap or identify another channel's voice.</summary>
public readonly record struct SoundBufferToken(int Channel, ulong Generation);
public readonly record struct SoundBufferObservation(SoundBufferToken Token, uint Flags);
public readonly record struct SoundBufferCursor(SoundBufferToken Token, uint Play, uint Write);
public readonly record struct SoundListenerParameters(FVector Position, FVector Velocity, FVector Front, FVector Top,
    float RolloffFactor, float DistanceFactor, float DopplerFactor);

/// <summary>
/// Synchronous explicit device boundary for the PC adapter. The device owns real source-buffer admission and each
/// duplicate. Null is a failed DuplicateSoundBuffer, not successful silent playback. Device creation/status outcomes
/// are inputs: a presentation host must marshal calls to its proper thread and record them for deterministic replay.
/// No device may call SoundEffects or a monitored owner's completion callback asynchronously.
/// </summary>
public interface ISoundBufferDevice
{
    ISoundPlaybackBuffer? Duplicate(SampleMetadata sample, SampleBufferFormat format, SoundBufferToken token);
    void SetListener(SoundListenerParameters parameters);
    void CommitDeferredSettings();
}

/// <summary>
/// One immutable duplicate identity. Status/cursor replies carry that identity; stale replies must never be used as
/// another duplicate's completion. Spatial settings use zero source velocity, min distance3 and max distance50;
/// nonzero source Doppler remains outside this adapter's admission. Flags follow DirectSound PLAYING1/LOOPING4.
/// Release(false) is native replacement's last-reference release. Release(true) performs explicit Stop then release;
/// pause uses Stop alone, preserving the buffer/cursor. Play's result is reported but native ignores that HRESULT.
/// </summary>
public interface ISoundPlaybackBuffer
{
    SoundBufferToken Token { get; }
    uint Frequency { get; }
    SoundBufferObservation GetStatus();
    SoundBufferCursor GetCurrentPosition();
    bool SetVolume(int volume);
    bool SetSpatial(FVector position, bool deferred);
    bool TrySetFrequency(uint frequency);
    void SetCurrentPosition(uint byteOffset);
    bool Play(bool looping);
    void Stop();
    void Release(bool stopFirst);
}

// Compatibility device for the existing virtual backend. Catch-up stays at the original adapter's call sites;
// passive volume/spatial/listener hooks add no clock reads or completion timer. Releasing either way halts its
// last virtual reference exactly once, matching the old backend's Buffer.Stop-before-drop behavior.
internal sealed class VirtualSoundBufferDevice(Func<long> blocksMixed) : ISoundBufferDevice
{
    private sealed class Playback(SampleBufferFormat format, Func<long> blocks, SoundBufferToken token) : ISoundPlaybackBuffer
    {
        private readonly VirtualSoundBuffer _buffer = new(format, blocks);
        public SoundBufferToken Token { get; } = token;
        public uint Frequency => _buffer.Frequency;
        public SoundBufferObservation GetStatus() => new(Token, _buffer.GetStatus());
        public SoundBufferCursor GetCurrentPosition()
        { var p = _buffer.GetCurrentPosition(); return new(Token, p.Play, p.Write); }
        public bool SetVolume(int volume) => volume is >= -10000 and <= 0;
        public bool SetSpatial(FVector position, bool deferred) => true;
        public bool TrySetFrequency(uint frequency) => _buffer.TrySetFrequency(frequency);
        public void SetCurrentPosition(uint byteOffset) => _buffer.SetCurrentPosition(byteOffset);
        public bool Play(bool looping) { _buffer.Play(looping); return true; }
        public void Stop() => _buffer.Stop();
        public void Release(bool stopFirst) => _buffer.Stop();
    }
    public ISoundPlaybackBuffer Duplicate(SampleMetadata sample, SampleBufferFormat format, SoundBufferToken token) =>
        new Playback(format, blocksMixed, token);
    public void SetListener(SoundListenerParameters parameters) { }
    public void CommitDeferredSettings() { }
}
