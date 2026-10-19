// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SampleBankMetadataTests
{
    private static byte[] Bank(params (string Name, int Size)[] entries)
    {
        using var s = new MemoryStream(); using var b = new BinaryWriter(s);
        b.Write(0x12345678u); b.Write(entries.Length);
        foreach (var e in entries)
        {
            b.Write(0x87654321u); byte[] name = new byte[64]; Encoding.Latin1.GetBytes(e.Name).CopyTo(name, 0);
            b.Write(name); b.Write(e.Size); b.Write(new byte[(e.Size + 3) / 4]);
        }
        return s.ToArray();
    }

    [Fact]
    public void HeaderWalkSkipsCompressedPayloadPreservesOrderAndLastDuplicateWins()
    {
        byte[] data = Bank(("first", 7), ("other", 12), ("FIRST", 9));
        var bank = SampleBankMetadata.Read(data.Concat(new byte[] { 1, 2, 3 }).ToArray());
        Assert.Equal(data.Length, bank.BytesConsumed); Assert.Equal(3, bank.Entries.Count);
        Assert.Equal(9u, bank.Find("First")!.OriginalPcmBytes); Assert.Null(bank.Find("absent"));
    }

    [Fact]
    public void MalformedMetadataIsRejectedAtTheAdmissionBoundary()
    {
        Assert.Throws<InvalidDataException>(() => SampleBankMetadata.Read(new byte[7]));
        byte[] bad = Bank(("s", 8)); bad[4] = 2;
        Assert.Throws<InvalidDataException>(() => SampleBankMetadata.Read(bad));
        Assert.Throws<InvalidDataException>(() => SampleBankMetadata.Read(Bank(("s", 8)).AsSpan(0, 81)));
        bad = Bank(("s", 8)); BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(76), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => SampleBankMetadata.Read(bad));
    }

    [Theory]
    [InlineData(0, 6944, 6944, 44100, 16, 3472)]
    [InlineData(1, 3472, 3472, 22050, 16, 1736)]
    [InlineData(2, 868, 868, 11025, 8, 868)]
    [InlineData(3, 6944, 6944, 11025, 8, 6944)]
    [InlineData(-1, 6944, 6944, 11025, 8, 6944)]
    public void OriginalSizeAndConvertedBufferLengthAreSeparate(int rate, uint requested, uint bytes, uint hz, int bits, uint frames)
    {
        Assert.True(SampleBufferFormat.TryCreate(6944, rate, out var f));
        Assert.Equal(6944u, f.OriginalPcmBytes); Assert.Equal(requested, f.RequestedBufferBytes);
        Assert.Equal(bytes, f.BufferBytes); Assert.Equal(hz, f.SampleRate); Assert.Equal(bits, f.Bits); Assert.Equal(frames, f.Frames);
        Assert.Equal(6944f / (hz * 2f), f.NominalEventLength);
    }

    [Fact]
    public void OddConvertedLengthRoundsUpWholeFrameAndBackendRejectsTinyBuffers()
    {
        Assert.True(SampleBufferFormat.TryCreate(17, 1, out var f));
        Assert.Equal(9u, f.RequestedBufferBytes); Assert.Equal(10u, f.BufferBytes); Assert.Equal(5u, f.Frames);
        Assert.False(SampleBufferFormat.TryCreate(6, 1, out _));
        Assert.False(SampleBufferFormat.TryCreate(0x10000000, 0, out _));
        Assert.False(SampleBufferFormat.TryCreate(uint.MaxValue, 1, out _));
    }

    [GameDataFact]
    public void InstalledBankSuppliesTheWarningLengthWithoutDecodingOrInventingDuration()
    {
        byte[] data = GameData.Read(@"data\sounds\sounds_english_pc.xap").Bytes;
        Assert.Equal("658C15E3BAB844D65DD3C07C4AC880F16F741C0EA116F48C603449BBD4DDA8B7",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)));
        var bank = SampleBankMetadata.Read(data);
        Assert.Equal(data.Length, bank.BytesConsumed); Assert.Equal(164, bank.Entries.Count);
        Assert.Equal(6944u, bank.Find(@"Battle Engine\N_BE_incoming_missile")!.OriginalPcmBytes);
    }

    [RetailOracleFact("sound-buffer-format")]
    public void FormatAndNominalLengthMatchOriginalCreateSoundBufferAndGetSampleLength()
    {
        var actual = new List<string>();
        foreach ((string name, uint[] w, string expected) in RetailOracle.Cases("sound-buffer-format"))
        {
            Assert.Equal("Format", name); Assert.Equal(2, w.Length);
            bool admitted = SampleBufferFormat.TryCreate(w[0], unchecked((int)w[1]), out var f);
            // The original-code oracle intercepts CreateSoundBuffer and intentionally refuses creation after
            // recording its descriptor; then the actual port's documented admission bounds select these rows.
            Assert.True(admitted);
            string line = $"Format {f.RequestedBufferBytes:x8} {f.SampleRate:x8} {f.Bits:x8} {f.BlockAlign:x8} {RetailOracle.H(f.NominalEventLength)}";
            Assert.True(line == expected, $"case {actual.Count}: {line} != {expected}"); actual.Add(line);
        }
        Assert.Equal(1060, actual.Count);
        RetailOracle.WriteOurs("sound-buffer-format", actual.ToArray());
    }
}
