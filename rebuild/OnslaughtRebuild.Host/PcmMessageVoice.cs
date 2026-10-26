// SPDX-License-Identifier: GPL-3.0-only
// MessageBox.cpp PlayPendingSamples004b7d90: delete old sample before creating/playing the new actual decoded data.
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Host;

/// <summary>A prepared PCM loader facade. Playback/completion belongs exclusively to SoundEffects.</summary>
public sealed class PcmMessageVoice(VoicePcmLibrary library, Func<LiveSoundSources> sources) : ISoundProcessVoice
{
    private SoundProcess? _process;
    private ReadOnlyMemory<byte> _loaded;
    private string? _path;
    private SampleMetadata? _sample;
    public bool SoundInitialised => _process is { Initialised: true } p && p.Effects.Initialised;
    // Prepared bytes were read before starting the level. This is ready data, not a simulated worker-duration timer.
    public bool LoaderBusy => false;
    public bool HasLoadedData => !_loaded.IsEmpty;
    public void BindSoundProcess(SoundProcess process)
    {
        if (_process is not null || HasLoadedData || process.UsesVirtualBuffers)
            throw new InvalidOperationException("A fresh PCM voice requires this external-device sound process.");
        _process = process;
    }
    public void StartLoad(string path) { _loaded = library.Read(path); _path = path; }
    public void PlayLoaded(ScriptMessage? message)
    {
        SoundProcess process = _process ?? throw new InvalidOperationException("Bind PCM voice before use.");
        if (!SoundInitialised || _loaded.IsEmpty) throw new InvalidOperationException("No initialized process/prepared voice data.");
        ReadOnlyMemory<byte> loaded = _loaded; _loaded = default;
        KillVoice();
        var sample = new SampleMetadata(_path!, checked((uint)loaded.Length));
        SamplePcmData pcm = SamplePcm.ConvertPcm16(loaded.Span, process.SoundRate, VoicePcmLibrary.VoiceBufferSize);
        sources().AddVoice(sample, pcm); _sample = sample;
        process.Effects.PlaySample(sample, message,
            new SoundPlayback(Volume: process.Effects.RadioMessageVolume, Tracking: SoundTracking.None,
                InformOwner: true, IgnoreOwnerPosition: true),
            (owner, _) => ((ScriptMessage)owner).SampleFinishedPlaying());
    }
    public void KillVoice()
    {
        if (_sample is not { } sample) return;
        _process!.Effects.KillAllInstancesOfSample(sample);
        sources().RemoveVoice(sample); _sample = null;
    }
    public void UpdateStatus() => throw new InvalidOperationException("Update the shared sound manager; PCM voice owns no timer.");
}
