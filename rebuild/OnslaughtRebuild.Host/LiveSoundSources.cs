// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// Explicit engineering policy for the opt-in live host, not a measured retail allocator/device profile.
/// CLIParams defaults: rate1, hardware off, speakers unflipped. Twelve is native DeviceInit's fallback, not a
/// claim about the voices a particular DirectSound driver exposes. No replay pool/profile is borrowed.
/// </summary>
public sealed class LiveSoundSources
{
    public const int Channels = 12;
    public const int MaximumBankBytes = 64 * 1024 * 1024;
    public const int MaximumSampleBytes = 16 * 1024 * 1024;
    public const int MaximumCachedBytes = 64 * 1024 * 1024;
    private readonly SampleBankPcmCatalog _bank;
    private readonly Dictionary<SampleMetadata, SamplePcmData> _voices = new(ReferenceEqualityComparer.Instance);
    private readonly CareerOptions _menuOptions;
    public int SoundRate { get; }
    public bool FlipSpeakers { get; }

    public LiveSoundSources(byte[] bank, CareerOptions? options)
    {
        _menuOptions = options ?? CareerOptions.Read(new byte[CareerOptions.Size]);
        SoundRate = options?.SoundRate ?? 1;
        FlipSpeakers = options?.FlipSpeakers != 0 && options is not null;
        if (options?.HardwareSound is not null and not 0)
            throw new NotSupportedException("Live audio requires HardwareSound=false; its native pitch/RNG configuration is not implemented.");
        if (SoundRate is < 0 or > 2)
            throw new NotSupportedException("Live audio supports only the three authored native sound rates.");
        if (options?.SoundDevice is not null and not 0 || options?.Sound3DMethod is not null and not -1 and not 2)
            throw new NotSupportedException("Live audio uses Godot's selected device and spatial renderer; DirectSound device indices/HRTF modes are unsupported.");
        _bank = SampleBankPcmCatalog.Read(bank, SoundRate, MaximumBankBytes, MaximumSampleBytes, MaximumCachedBytes);
    }

    public SoundDeviceProcessOptions Options(ISoundBufferDevice device) => new(
        new(SoundRate, Channels, true, FlipSpeakers),
        Enumerable.Repeat(new SoundEventSeed(-1, default), SoundEffects.EventCapacity).ToArray(),
        (name, _) => _bank.Metadata.Find(name), HasBuffer,
        // CThing's inherited GetSoundPos/GetSoundVelocity (thing.h): no native subclass overrides these.
        owner => owner is Thing thing ? new SoundOwnerSpatialState(thing.Pos, default) : null, device);

    public SamplePcmData Pcm(SampleMetadata sample)
    {
        SamplePcmData pcm = _voices.TryGetValue(sample, out var voice) ? voice : _bank.Decode(sample);
        if (!pcm.FullyInitialized)
            throw new NotSupportedException("Live audio refuses the native allocation's unwritten PCM tail.");
        return pcm;
    }
    public bool HasBuffer(SampleMetadata sample) => Pcm(sample).FullyInitialized;
    public void AddVoice(SampleMetadata sample, SamplePcmData pcm)
    {
        if (!pcm.FullyInitialized || pcm.Format.OriginalPcmBytes != sample.OriginalPcmBytes)
            throw new InvalidDataException("Voice PCM and sample descriptor disagree.");
        _voices.Add(sample, pcm);
    }
    public void RemoveVoice(SampleMetadata sample) => _voices.Remove(sample);

    public void CheckOptions(CareerOptions options)
    {
        if (options.SoundRate != _menuOptions.SoundRate || options.HardwareSound != _menuOptions.HardwareSound
            || options.FlipSpeakers != _menuOptions.FlipSpeakers || options.SoundDevice != _menuOptions.SoundDevice
            || options.Sound3DMethod != _menuOptions.Sound3DMethod)
            throw new NotSupportedException("Changing live audio device/rate/speaker options requires process reinitialization; restart this preview.");
    }
}

/// <summary>
/// Local, prepared voice bytes. A manifest binds each actual Ogg to the result of one capped native decoder read;
/// lengths are never inferred from headers. Decoder provenance records an explicitly supplied local tool, not
/// automatic retail equivalence approval. Neither PCM nor the manifest is shipped with the program.
/// </summary>
public sealed class VoicePcmLibrary
{
    public const int VoiceBufferSize = 0x100000;
    public const string Contract = "COggFileRead.Read-1048576-PC53-PCM16LE-v1";
    private readonly Dictionary<string, byte[]> _pcm = new(StringComparer.OrdinalIgnoreCase);
    public VoicePcmLibrary(string manifest, Func<string, (string Name, byte[] Bytes)> read)
    {
        var manifestInfo = new FileInfo(manifest);
        if (manifestInfo.LinkTarget is not null || manifestInfo.Length is < 1 or > 4 * 1024 * 1024)
            throw new InvalidDataException("Voice manifest must be a regular file within its byte bound.");
        byte[] bytes = File.ReadAllBytes(manifest);
        if (bytes.Length != manifestInfo.Length) throw new InvalidDataException("Voice manifest changed while reading it.");
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        if (root.GetProperty("schema").GetInt32() != 1 || root.GetProperty("contract").GetString() != Contract)
            throw new InvalidDataException("Voice manifest has an unsupported decoder contract.");
        var decoder = root.GetProperty("decoder");
        RequireHashText(decoder.GetProperty("executable_sha256").GetString());
        RequireHashText(decoder.GetProperty("library_sha256").GetString());
        RequireHashText(decoder.GetProperty("reader_sha256").GetString());
        if (decoder.GetProperty("control_word").GetString() != "027f")
            throw new InvalidDataException("Native voice decoding requires its worker's PC53 control word.");
        string directory = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        int total = 0, count = 0;
        foreach (var entry in root.GetProperty("voices").EnumerateArray())
        {
            if (++count > 4096) throw new InvalidDataException("Too many prepared voices.");
            string path = Normalize(entry.GetProperty("path").GetString()!);
            if (!path.StartsWith("data\\sounds\\", StringComparison.OrdinalIgnoreCase) || !path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                || path.Split('\\').Any(p => p is "" or "." or ".." || p.Contains(':')))
                throw new InvalidDataException("Voice path must be a game-relative sound Ogg.");
            RequireHash(read(path).Bytes, entry.GetProperty("ogg_sha256").GetString());
            string file = entry.GetProperty("pcm_file").GetString()!;
            if (file != Path.GetFileName(file) || file.Contains('\\') || file is "" or "." or "..")
                throw new InvalidDataException("PCM files must be direct children of the manifest folder.");
            string pcmPath = Path.Combine(directory, file);
            var info = new FileInfo(pcmPath);
            if (info.LinkTarget is not null || info.Length is < 4 or > VoiceBufferSize || (info.Length & 1) != 0)
                throw new InvalidDataException("Voice PCM must be a regular, bounded PCM16 file.");
            if (entry.GetProperty("decoded_bytes").GetInt32() != info.Length)
                throw new InvalidDataException("The actual native voice read size disagrees with the manifest.");
            total = checked(total + (int)info.Length);
            if (total > 256 * 1024 * 1024) throw new InvalidDataException("Prepared voice library exceeds its byte bound.");
            byte[] pcm = File.ReadAllBytes(pcmPath);
            if (pcm.Length != info.Length) throw new InvalidDataException("Voice PCM changed while reading it.");
            RequireHash(pcm, entry.GetProperty("pcm_sha256").GetString());
            if (!_pcm.TryAdd(path, pcm)) throw new InvalidDataException("Duplicate prepared voice path.");
        }
        if (count == 0) throw new InvalidDataException("An empty prepared voice library cannot play messages.");
    }
    public ReadOnlyMemory<byte> Read(string path) => _pcm.TryGetValue(Normalize(path), out var pcm) ? pcm
        : throw new NotSupportedException($"Voice PCM was not prepared: {path}");
    public static string Normalize(string path) => path.Replace('/', '\\');
    private static void RequireHashText(string? expected)
    {
        if (expected is null || expected.Length != 64 || expected.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Decoder/source hashes must be SHA-256 values.");
    }
    private static void RequireHash(byte[] bytes, string? expected)
    {
        RequireHashText(expected);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Prepared voice source/PCM hash mismatch.");
    }
}

/// <summary>Godot AudioStreamWav expects signed8 PCM, whereas native WAVE PCM8 has its unsigned128 bias.</summary>
public static class LiveAudioEncoding
{
    public static byte[] ForGodot(SamplePcmData pcm)
    {
        byte[] data = pcm.Data.ToArray();
        if (pcm.Format.Bits == 8)
            for (int i = 0; i < data.Length; ++i) data[i] ^= 0x80;
        return data;
    }
}
