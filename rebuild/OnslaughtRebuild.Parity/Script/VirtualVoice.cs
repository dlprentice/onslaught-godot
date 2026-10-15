// SPDX-License-Identifier: GPL-3.0-or-later
// The voice's path from bea-decomp src/MessageBox.cpp (COggLoader::Run 004b6d90, VOICE_BUFFER_SIZE, PlayPendingSamples
// 004b7d90), src/pcsoundmanager.cpp (CreateSampleFromData 005176d0, CreateSoundBuffer 00517440: (size + 1) / 2 bytes of
// mono at 22050 Hz for sound rate 1; ConvertAudioFormat 00517600) and src/SoundManager.cpp (UpdateStatus 004e1b20,
// StopSoundEvent 004e0f70); the mixer is the port's virtual-clock DirectSound (bea-decomp port/dsound/dsound.cpp:
// Mixer::CatchUp and Mix at 385809a2), which C's retail harness's virtual DirectSound matches (golden-640
// level100.ds-virtual.ds.log).

using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Events;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>
/// The message box's voice as it plays under a virtual clock (the port with BEA_PORT_CLOCK, and C's retail harness):
/// <list type="bullet">
/// <item>the loader's size is the decoded voice, 16-bit at the stream's channels: the last Ogg page's granule position
/// times the channels times 2 (equal to COggFileRead's output for every voice the traces show), capped at the voice
/// buffer; the load is done by the next PlayPendingSamples (both traces);</item>
/// <item>the sample's buffer is (size + 1) / 2 bytes of 22050 Hz mono, so it lasts that many bytes / 2 frames;</item>
/// <item>the mixer runs at 44100 Hz in 441-frame (10 ms) blocks, mixing every whole block up to the virtual time at each
/// DirectSound call, from the time it started (after Present 1); a 22050 Hz voice steps half a frame per output frame
/// and halts in the block where its cursor reaches its end; GetStatus reports it stopped from then on.</item>
/// </list>
/// So a voice started when <c>b</c> blocks had been mixed stops once <c>b + floor(2 frames / 441) + 1</c> have.
/// </summary>
public sealed class VirtualVoice : IMessageVoice
{
    /// <summary>VOICE_BUFFER_SIZE.</summary>
    public const int VoiceBufferSize = 0x100000;

    /// <summary>The port's shim step (gDtNs: 0.0333333 s rounded to nanoseconds).</summary>
    public const long StepNs = 33_333_300;

    /// <summary>The mix rate and its block (kRate, kVirtualBlock).</summary>
    public const long MixRate = 44100;
    public const long Block = MixRate / 100;

    private readonly PlatformClock _clock;
    private readonly Func<string, (string Name, byte[] Bytes)> _read;
    private readonly long _baseNs;
    private int _loadedSize;
    private (long EndBlocks, ActiveReader<ScriptMessage> Message)? _playing;

    /// <param name="clock">The platform clock the game Presents through.</param>
    /// <param name="read">The game's files by game path.</param>
    /// <param name="baseSteps">The virtual steps when the mixer started (1: after the first Present).</param>
    public VirtualVoice(PlatformClock clock, Func<string, (string Name, byte[] Bytes)> read, long baseSteps = 1)
    {
        _clock = clock;
        _read = read;
        _baseNs = baseSteps * StepNs;
    }

    /// <summary>The voices started and finished, for comparison with the traces: (blocks when played, frames, blocks when found stopped).</summary>
    public List<(string Path, int Size, long PlayedAt, long StoppedAt)> Trace { get; } = [];

    private string? _path;

    public bool SoundInitialised => true;

    public bool LoaderBusy => false;

    public bool HasLoadedData => _loadedSize != 0;

    /// <summary>The blocks mixed by now (Mixer::CatchUp's whole blocks up to the virtual time).</summary>
    public long BlocksMixed()
    {
        long ns = (_clock.PeekSteps * StepNs) - _baseNs;
        if (ns <= 0)
        {
            return 0;
        }

        long target = (long)((ulong)ns * (ulong)MixRate / 1_000_000_000UL);
        return target / Block;
    }

    public void StartLoad(string path)
    {
        (string _, byte[] bytes) = _read(path);
        _path = path;
        _loadedSize = (int)Math.Min(DecodedSize(bytes), VoiceBufferSize);
    }

    public void PlayLoaded(ScriptMessage? message)
    {
        int size = _loadedSize;
        _loadedSize = 0;
        long frames = ((size + 1) / 2) / 2;
        long now = BlocksMixed();
        var reader = new ActiveReader<ScriptMessage>();
        reader.SetReader(message);
        _playing = (now + (2 * frames / Block) + 1, reader);
        Trace.Add((_path ?? "", size, now, -1));
    }

    public void KillVoice()
    {
        if (_playing is { } p)
        {
            p.Message.SetReader(null);
        }

        _playing = null;
    }

    public void UpdateStatus()
    {
        if (_playing is not { } p || BlocksMixed() < p.EndBlocks)
        {
            return;
        }

        _playing = null;
        (string path, int size, long at, long _) = Trace[^1];
        Trace[^1] = (path, size, at, BlocksMixed());
        p.Message.ToRead?.SampleFinishedPlaying();
        p.Message.SetReader(null);
    }

    /// <summary>The decoded length in bytes: the last page's granule position times the channels times 2.</summary>
    public static long DecodedSize(byte[] ogg)
    {
        int channels = 0;
        long last = 0;
        int i = 0;
        while (i + 27 <= ogg.Length)
        {
            if (ogg[i] != 'O' || ogg[i + 1] != 'g' || ogg[i + 2] != 'g' || ogg[i + 3] != 'S')
            {
                throw new InvalidDataException("not an Ogg stream (or a page header is broken)");
            }

            long granule = BitConverter.ToInt64(ogg, i + 6);
            int segments = ogg[i + 26];
            int body = i + 27 + segments;
            int length = 0;
            for (int s = 0; s < segments; s++)
            {
                length += ogg[i + 27 + s];
            }

            if (channels == 0 && body + 12 <= ogg.Length && ogg[body] == 1 && ogg[body + 1] == 'v')
            {
                channels = ogg[body + 11];
            }

            if (granule >= 0)
            {
                last = granule;
            }

            i = body + length;
        }

        return last * channels * 2;
    }
}
