// SPDX-License-Identifier: GPL-3.0-only
namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// An owned XAP snapshot with lazy, identity-keyed PCM decoding. Parsing/duplicate lookup uses SampleBankMetadata;
/// no second bank walker, filesystem access or playback owner exists here. Callers explicitly bound the packed bank,
/// each decoded sample and retained decoded bytes. A refusal leaves the cache/byte budget unchanged.
/// </summary>
public sealed class SampleBankPcmCatalog
{
    private readonly byte[] _packed;
    private readonly Dictionary<SampleMetadata, (int Offset, int Length)> _payloads = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<SampleMetadata, SamplePcmData> _decoded = new(ReferenceEqualityComparer.Instance);
    private readonly int _soundRate, _maximumSampleBytes, _maximumCachedBytes;
    private SampleBankPcmCatalog(ReadOnlySpan<byte> bank, int soundRate, int maximumSampleBytes, int maximumCachedBytes)
    {
        _soundRate = soundRate; _maximumSampleBytes = maximumSampleBytes; _maximumCachedBytes = maximumCachedBytes;
        Metadata = SampleBankMetadata.ReadWithPayloads(bank,
            (sample, offset, length) => _payloads.Add(sample, (offset, length)));
        // Trailing bytes ignored by the native declared-count walk are not retained as sample data.
        _packed = bank[..Metadata.BytesConsumed].ToArray();
    }
    public SampleBankMetadata Metadata { get; }
    public int CachedPcmBytes { get; private set; }
    public int CachedSamples => _decoded.Count;

    public static SampleBankPcmCatalog Read(ReadOnlySpan<byte> bank, int soundRate,
        int maximumBankBytes, int maximumSampleBytes, int maximumCachedBytes)
    {
        if (maximumBankBytes < 0 || maximumSampleBytes < 0 || maximumCachedBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumBankBytes), "Resource bounds must be nonnegative.");
        if (bank.Length > maximumBankBytes) throw new InvalidDataException("Sample bank exceeds its declared resource bound.");
        return new(bank, soundRate, maximumSampleBytes, maximumCachedBytes);
    }

    public SamplePcmData Decode(SampleMetadata sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (!_payloads.TryGetValue(sample, out var payload))
            throw new ArgumentException("Sample identity belongs to another catalog.", nameof(sample));
        if (_decoded.TryGetValue(sample, out var cached)) return cached;
        SampleBufferFormat format = SamplePcm.Admit(sample.OriginalPcmBytes, _soundRate, _maximumSampleBytes);
        // Reserve the full buffer size, including unknown/unwritten tails; no caller can evade the cap with such data.
        if (format.BufferBytes > _maximumCachedBytes - CachedPcmBytes)
            throw new InvalidDataException("Decoded sample cache exceeds its declared resource bound.");
        var data = SamplePcm.DecodeBankSample(_packed.AsSpan(payload.Offset, payload.Length),
            sample.OriginalPcmBytes, _soundRate, _maximumSampleBytes);
        _decoded.Add(sample, data);
        CachedPcmBytes += checked((int)format.BufferBytes);
        return data;
    }
}
