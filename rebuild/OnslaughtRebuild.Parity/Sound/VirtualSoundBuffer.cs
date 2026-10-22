// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-ce port/dsound/dsound.cpp: Buffer::{Play,Stop,SetFrequency,SetCurrentPosition,GetStatus,
// GetCurrentPosition}, Mixer::{CatchUp,Mix}. This is the port/retail-harness virtual backend, not retail hardware.
namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// Cursor/status of one admitted mono buffer under the existing virtual mix clock. Supply whole blocks from the
/// process's clock (for example VirtualVoice.BlocksMixed); this class makes no platform clock reads of its own.
/// No channel allocation, event lifetime, sample admission, spatial Doppler, notifications, mixing or audio device.
/// Callers must supply an admitted voice with fixed nonspatial pitch and own the separate sound-event lifecycle.
/// </summary>
public sealed class VirtualSoundBuffer
{
    public const int MixRate = 44100;
    public const int BlockFrames = 441;
    private readonly Func<long> _blocksMixed;
    private long _lastBlock;
    private double _cursor;
    private bool _playing, _looping;

    public VirtualSoundBuffer(SampleBufferFormat format, Func<long> blocksMixed)
    {
        if (format.BufferBytes == 0 || format.BlockAlign == 0) throw new ArgumentException("An admitted sample format is required.", nameof(format));
        Format = format;
        _blocksMixed = blocksMixed ?? throw new ArgumentNullException(nameof(blocksMixed));
        _lastBlock = blocksMixed();
        Frequency = format.SampleRate;
    }

    public SampleBufferFormat Format { get; }
    public uint Frequency { get; private set; }

    /// <summary>DirectSound PLAYING=1, LOOPING=4. Polling catches up backend state, not its owning game event.</summary>
    public uint GetStatus() { CatchUp(); return _playing ? (_looping ? 5u : 1u) : 0u; }

    public (uint Play, uint Write) GetCurrentPosition()
    {
        CatchUp();
        uint play = (uint)_cursor * (uint)Format.BlockAlign;
        uint ahead = Format.SampleRate / 100 * (uint)Format.BlockAlign;
        return (play, (play + ahead) % Format.BufferBytes);
    }

    public void Play(bool looping = false)
    {
        CatchUp();
        _looping = looping;
        _playing = true; // The host has already admitted this nonempty, nonprimary voice.
    }

    /// <summary>Native pause/resume uses Stop then Play: an explicit stop retains the fractional cursor.</summary>
    public void Stop() { CatchUp(); _playing = false; }

    public void SetCurrentPosition(uint byteOffset)
    {
        CatchUp();
        _cursor = (byteOffset % Format.BufferBytes) / (uint)Format.BlockAlign;
    }

    /// <summary>Even a rejected frequency change first catches up at the OLD frequency. Zero restores the format rate.</summary>
    public bool TrySetFrequency(uint frequency)
    {
        CatchUp();
        if (frequency != 0 && (frequency < 100 || frequency > 200000)) return false;
        Frequency = frequency == 0 ? Format.SampleRate : frequency;
        return true;
    }

    private void CatchUp()
    {
        long now = _blocksMixed();
        if (now < _lastBlock) throw new InvalidOperationException("A virtual mixer clock cannot move backwards.");
        if (_playing)
        {
            double step = (double)Frequency / MixRate;
            uint frames = Format.Frames;
            for (long block = _lastBlock; block < now && _playing; block++)
            {
                // Preserve repeated binary64 addition and the check BEFORE each output frame. Multiplying step by
                // elapsed frames changes rounding; treating cursor==end at the last frame as stopped ends too soon.
                for (int i = 0; i < BlockFrames; i++)
                {
                    if ((uint)_cursor >= frames)
                    {
                        if (!_looping) { _cursor = 0; _playing = false; break; }
                        _cursor -= frames;
                        if ((uint)_cursor >= frames) _cursor = 0;
                    }
                    _cursor += step;
                }
                if (_looping && _cursor >= frames) _cursor %= frames;
            }
        }
        _lastBlock = now;
    }
}
