// SPDX-License-Identifier: GPL-3.0-only
// CPCSoundManager::DecodeADPCM00517fa0, LoadSampleFromBuffer005172a0 and ConvertAudioFormat00517600,
// pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Sound;

/// <summary>
/// Owned, initialized PCM bytes and their native buffer descriptor. An unwritten native allocation tail is reported,
/// never filled with invented retail samples. This is sample data, not a playback/timing/event-status owner.
/// </summary>
public sealed class SamplePcmData
{
    internal SamplePcmData(SampleBufferFormat format, byte[] data) { Format = format; Data = data; }
    public SampleBufferFormat Format { get; }
    public ReadOnlyMemory<byte> Data { get; }
    public uint UnwrittenTailBytes => Format.BufferBytes - (uint)Data.Length;
    public bool FullyInitialized => UnwrittenTailBytes == 0;
}

/// <summary>
/// Integer-only native sample decoding/conversion. Each call resets the IMA predictor/index to zero, consumes high
/// nibble first and emits exactly floor(original bytes/2) signed LE words. Resource bounds are explicit host inputs;
/// the method reads no clock, environment, RNG, device or filesystem. Temporary plus output storage is at most twice
/// maximumSampleBytes. The separate message loader's 1MiB voice cap does not apply to XAP sample-bank entries.
/// </summary>
public static class SamplePcm
{
    private static ReadOnlySpan<int> IndexTable => [-1, -1, -1, -1, 2, 4, 6, 8];
    private static ReadOnlySpan<int> StepTable =>
    [
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
        34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130,
        143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449,
        494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411,
        1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660,
        4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493,
        10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385,
        24623, 27086, 29794, 32767,
    ];

    public static SamplePcmData DecodeBankSample(ReadOnlySpan<byte> packed, uint originalPcmBytes,
        int soundRate, int maximumSampleBytes)
    {
        SampleBufferFormat format = Admit(originalPcmBytes, soundRate, maximumSampleBytes);
        // Header traversal consumes (size+3)/4 bytes, even when the last nibble is not decoded by the native loop.
        uint required = (originalPcmBytes + 3) / 4;
        if (required > packed.Length) throw new InvalidDataException("Truncated packed sample.");
        int samples = checked((int)(originalPcmBytes / 2));
        byte[] pcm = new byte[samples * 2];
        int predictor = 0, index = 0;
        for (int i = 0; i < samples; i++)
        {
            int nibble = (i & 1) == 0 ? packed[i / 2] >> 4 : packed[i / 2] & 15;
            int step = StepTable[index];
            int difference = step >> 3;
            if ((nibble & 4) != 0) difference += step;
            if ((nibble & 2) != 0) difference += step >> 1;
            if ((nibble & 1) != 0) difference += step >> 2;
            predictor = Math.Clamp(predictor + ((nibble & 8) != 0 ? -difference : difference), -32768, 32767);
            index = Math.Clamp(index + IndexTable[nibble & 7], 0, 88);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (short)predictor);
        }
        return Convert(pcm, format, soundRate);
    }

    /// <summary>
    /// Convert already decoded bytes, for example the later voice loader's actual bounded read. The caller supplies
    /// the real read size and applies its own loader cap; this method neither infers Ogg duration nor truncates voice.
    /// </summary>
    public static SamplePcmData ConvertPcm16(ReadOnlySpan<byte> pcm, int soundRate, int maximumSampleBytes) =>
        Convert(pcm, Admit(checked((uint)pcm.Length), soundRate, maximumSampleBytes), soundRate);

    internal static SampleBufferFormat Admit(uint originalPcmBytes, int soundRate, int maximumSampleBytes)
    {
        if (maximumSampleBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumSampleBytes));
        if (!SampleBufferFormat.TryCreate(originalPcmBytes, soundRate, out var format))
            throw new InvalidDataException("Sample has no admitted native secondary-buffer format.");
        if (originalPcmBytes > maximumSampleBytes || format.BufferBytes > maximumSampleBytes)
            throw new InvalidDataException("Sample exceeds the declared decoded-buffer resource bound.");
        return format;
    }

    private static SamplePcmData Convert(ReadOnlySpan<byte> pcm, SampleBufferFormat format, int soundRate)
    {
        if (soundRate == 0) return new(format, pcm.ToArray());
        int original = checked((int)format.OriginalPcmBytes);
        if (soundRate == 1)
        {
            // Native explicitly writes a zero word for ANY remainder, not an average of one remaining word.
            int pairs = original / 4;
            byte[] output = new byte[(pairs + ((original & 3) != 0 ? 1 : 0)) * 2];
            for (int i = 0; i < pairs; i++)
            {
                int a = BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 4)..]);
                int b = BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 4 + 2)..]);
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(i * 2), (short)((a + b) >> 1));
            }
            return new(format, output);
        }
        // Native default shares rate2's four-word average even when an out-of-range option allocated original size.
        byte[] low = new byte[original / 8];
        for (int i = 0; i < low.Length; i++)
        {
            int sum = 0;
            for (int j = 0; j < 4; j++) sum += BinaryPrimitives.ReadInt16LittleEndian(pcm[(i * 8 + j * 2)..]);
            low[i] = unchecked((byte)((sum >> 10) + 0x80));
        }
        return new(format, low);
    }
}
