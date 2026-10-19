// SPDX-License-Identifier: GPL-3.0-only
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// LoadSampleBank00517d00, LoadSampleFromBuffer005172a0, CreateSoundBuffer00517440, GetSampleLength00517c60.
// Buffer alignment/admission comes from bea-decomp port/dsound/dsound.cpp, not a claim about every audio driver.
using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>One bank entry's original decoded size; neither compressed audio nor decoded samples are retained.</summary>
public sealed record SampleMetadata(string Name, uint OriginalPcmBytes);

/// <summary>
/// Header-only view of an admitted PC XAP bank. Tags are consumed but not interpreted, as in retail. Malformed or
/// truncated input is rejected here; this is an input boundary, not an emulation of retail's failure/assert paths.
/// Duplicate names stay in load order. Native GetSample finds the most recently loaded one.
/// </summary>
public sealed class SampleBankMetadata
{
    private SampleBankMetadata(List<SampleMetadata> entries, int consumed) { Entries = entries.AsReadOnly(); BytesConsumed = consumed; }
    public IReadOnlyList<SampleMetadata> Entries { get; }
    public int BytesConsumed { get; }
    public SampleMetadata? Find(string name)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            if (StringComparer.OrdinalIgnoreCase.Equals(Entries[i].Name, name)) return Entries[i];
        return null;
    }

    public static SampleBankMetadata Read(ReadOnlySpan<byte> bank)
    {
        if (bank.Length < 8) throw new InvalidDataException("A sample bank needs its tag and count.");
        int count = BinaryPrimitives.ReadInt32LittleEndian(bank[4..]);
        if (count < 0 || count > (bank.Length - 8) / 72) throw new InvalidDataException("Invalid sample-bank count.");
        var entries = new List<SampleMetadata>(count);
        int position = 8;
        for (int i = 0; i < count; i++)
        {
            if (bank.Length - position < 72) throw new InvalidDataException("Truncated sample header.");
            ReadOnlySpan<byte> name = bank.Slice(position + 4, 64);
            int nul = name.IndexOf((byte)0);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(bank[(position + 68)..]);
            // Retail's unsigned (size + 3) >> 2; reject overflowing, inadmissible headers instead of wrapping a seek.
            if (size > uint.MaxValue - 3) throw new InvalidDataException("Sample size overflows its packed length.");
            uint packed = (size + 3) / 4;
            position += 72;
            if (packed > bank.Length - position) throw new InvalidDataException("Truncated compressed sample.");
            entries.Add(new SampleMetadata(Encoding.Latin1.GetString(nul < 0 ? name : name[..nul]), size));
            position += (int)packed;
        }
        // The native loader stops at the declared count; trailing bytes are not a new sample.
        return new SampleBankMetadata(entries, position);
    }
}

/// <summary>
/// A native mono sample's converted DirectSound format. OriginalPcmBytes is deliberately separate: native channel-less
/// event expiry divides that original size by rate*2, whereas a playing buffer consumes the converted whole frames.
/// </summary>
public readonly record struct SampleBufferFormat
{
    private SampleBufferFormat(uint original, uint requested, uint bytes, uint rate, int bits)
    { OriginalPcmBytes = original; RequestedBufferBytes = requested; BufferBytes = bytes; SampleRate = rate; Bits = bits; }
    public uint OriginalPcmBytes { get; }
    public uint RequestedBufferBytes { get; }
    public uint BufferBytes { get; }
    public uint SampleRate { get; }
    public int Bits { get; }
    public int BlockAlign => Bits / 8;
    public uint Frames => BufferBytes / (uint)BlockAlign;

    /// <summary>Native GetSampleLength's signed FILD and PC24 division, before event comparisons.</summary>
    public float NominalEventLength => (float)X87.Div(unchecked((int)OriginalPcmBytes), X87.Add(SampleRate, SampleRate));

    /// <summary>
    /// CreateSoundBuffer's uint arithmetic and the port's secondary-buffer admission (4..0x0fffffff requested bytes).
    /// Rates outside 0..2 retain native's distinct defaults: 11025Hz/8 bits, but no size reduction.
    /// </summary>
    public static bool TryCreate(uint originalPcmBytes, int soundRate, out SampleBufferFormat format)
    {
        uint requested = soundRate switch
        {
            1 => unchecked(originalPcmBytes + 1) / 2,
            2 => unchecked(originalPcmBytes + 7) / 8,
            _ => originalPcmBytes,
        };
        if (requested < 4 || requested > 0x0fffffff) { format = default; return false; }
        uint rate = soundRate switch { 0 => 44100, 1 => 22050, _ => 11025 };
        int bits = soundRate is 0 or 1 ? 16 : 8;
        uint align = (uint)(bits / 8);
        format = new SampleBufferFormat(originalPcmBytes, requested, (requested + align - 1) / align * align, rate, bits);
        return true;
    }
}
