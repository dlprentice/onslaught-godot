// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SamplePcmTests
{
    private static byte[] Words(params short[] samples)
    {
        byte[] pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++) BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), samples[i]);
        return pcm;
    }

    private static byte[] Bank(params (string Name, uint Size, byte[] Packed)[] entries)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x50435850); writer.Write(entries.Length);
        foreach (var entry in entries)
        {
            Assert.Equal((entry.Size + 3) / 4, (uint)entry.Packed.Length);
            writer.Write(0x504d5350); byte[] name = new byte[64]; Encoding.Latin1.GetBytes(entry.Name).CopyTo(name, 0);
            writer.Write(name); writer.Write(entry.Size); writer.Write(entry.Packed);
        }
        return stream.ToArray();
    }

    [Fact]
    public void IMAUsesHighNibbleFirstZeroStateAndSignedLittleEndianWords()
    {
        var decoded = SamplePcm.DecodeBankSample(new byte[] { 0x17, 0x8f, 0x24 }, 12, 0, 64);
        Assert.Equal(Words(1, 12, 10, -15, 3, 34), decoded.Data.ToArray());
        Assert.True(decoded.FullyInitialized); Assert.Equal(44100u, decoded.Format.SampleRate);
        // Every bank sample starts a fresh native predictor; it must not inherit the preceding sample's index/value.
        Assert.Equal(decoded.Data.ToArray(), SamplePcm.DecodeBankSample(new byte[] { 0x17, 0x8f, 0x24 }, 12, 0, 64).Data.ToArray());
    }

    [Theory]
    [InlineData(0x77, 32767)]
    [InlineData(0xff, -32768)]
    public void PredictorAndIndexSaturateRatherThanWrapping(byte packed, short expected)
    {
        var decoded = SamplePcm.DecodeBankSample(Enumerable.Repeat(packed, 128).ToArray(), 512, 0, 512);
        Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(decoded.Data.Span[^2..]));
    }

    [Theory]
    [InlineData(5, 4, 2)]
    [InlineData(7, 6, 2)]
    [InlineData(9, 8, 2)]
    public void OddDeclaredSizeDoesNotDecodeAnInventedFinalHalfSample(uint original, int initialized, uint tail)
    {
        var decoded = SamplePcm.DecodeBankSample(new byte[(original + 3) / 4], original, 0, 64);
        Assert.Equal(initialized, decoded.Data.Length); Assert.Equal(tail, decoded.UnwrittenTailBytes);
        Assert.False(decoded.FullyInitialized);
    }

    [Fact]
    public void TrailingLowNibbleIsNotDecodedWhenOnlyOneWordWasRequested()
    {
        // 6 original bytes are three words: the final f must not alter the returned PCM.
        var a = SamplePcm.DecodeBankSample(new byte[] { 0x17, 0x80 }, 6, 0, 64);
        var b = SamplePcm.DecodeBankSample(new byte[] { 0x17, 0x8f }, 6, 0, 64);
        Assert.Equal(Words(1, 12, 10), a.Data.ToArray()); Assert.Equal(a.Data.ToArray(), b.Data.ToArray());
    }

    [Fact]
    public void RateOneUsesArithmeticSignedPairAverageAndExplicitRemainderZero()
    {
        var data = SamplePcm.ConvertPcm16(Words(-32768, 32767, -2, -1, 30000), 1, 64);
        Assert.Equal(Words(-1, -2, 0), data.Data.ToArray()); Assert.True(data.FullyInitialized);
        Assert.Equal(22050u, data.Format.SampleRate); Assert.Equal(16, data.Format.Bits);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void RateOneRemaindersWriteAWholeZeroWordEvenForOddOriginalSize(uint original)
    {
        var data = SamplePcm.DecodeBankSample(Enumerable.Repeat((byte)0x77, (int)((original + 3) / 4)).ToArray(), original, 1, 64);
        Assert.True(data.FullyInitialized); Assert.Equal(data.Format.BufferBytes, (uint)data.Data.Length);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(data.Data.Span[^2..]));
    }

    [Fact]
    public void LowRateAveragesFourWordsWithShiftTenAndUnsignedBias()
    {
        var pcm = Words(-32768, -32768, -32768, -32768, 32767, 32767, 32767, 32767,
            -1, -1, -1, 0, 0, 0, 0, 0);
        var data = SamplePcm.ConvertPcm16(pcm, 2, 64);
        Assert.Equal(new byte[] { 0, 255, 127, 128 }, data.Data.ToArray());
        Assert.True(data.FullyInitialized); Assert.Equal(11025u, data.Format.SampleRate); Assert.Equal(8, data.Format.Bits);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 25)]
    [InlineData(-1, 25)]
    public void NativeUnwrittenLowRateTailIsReportedAndNeverExposedAsZeroPcm(int rate, uint unwritten)
    {
        var data = SamplePcm.ConvertPcm16(new byte[28], rate, 64);
        Assert.Equal(3, data.Data.Length); Assert.Equal(unwritten, data.UnwrittenTailBytes);
        Assert.False(data.FullyInitialized);
    }

    [Fact]
    public void DecodingAndConversionHaveExplicitPreallocationBounds()
    {
        Assert.Throws<InvalidDataException>(() => SamplePcm.DecodeBankSample(new byte[1], 8, 0, 64));
        Assert.Throws<InvalidDataException>(() => SamplePcm.DecodeBankSample(new byte[2], 8, 0, 7));
        // Compressed/converted small output does not bypass the scratch decoded-size bound.
        Assert.Throws<InvalidDataException>(() => SamplePcm.DecodeBankSample(new byte[16], 64, 2, 8));
        Assert.Throws<InvalidDataException>(() => SamplePcm.DecodeBankSample([], uint.MaxValue, 1, int.MaxValue));
        Assert.Throws<InvalidDataException>(() => SamplePcm.ConvertPcm16(new byte[3], 0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => SamplePcm.ConvertPcm16(new byte[8], 0, -1));
    }

    [Fact]
    public void CatalogOwnsTheInputAndPreservesLastDuplicateIdentityWithLazyIndependentDecodes()
    {
        byte[] bytes = Bank(("same", 8, new byte[] { 0x17, 0x8f }), ("SAME", 8, new byte[] { 0x77, 0x77 }));
        var catalog = SampleBankPcmCatalog.Read(bytes, 0, bytes.Length, 64, 16);
        var first = catalog.Metadata.Entries[0]; var last = catalog.Metadata.Entries[1];
        Assert.Same(last, catalog.Metadata.Find("Same")); Assert.Equal(0, catalog.CachedSamples);
        Array.Fill(bytes, (byte)0); // Mutating the source owner cannot change the admitted snapshot.
        var a = catalog.Decode(first); var b = catalog.Decode(last);
        Assert.Equal(Words(1, 12, 10, -15), a.Data.ToArray()); Assert.NotEqual(a.Data.ToArray(), b.Data.ToArray());
        Assert.Same(a, catalog.Decode(first)); Assert.Equal(2, catalog.CachedSamples); Assert.Equal(16, catalog.CachedPcmBytes);
        Assert.Throws<ArgumentException>(() => catalog.Decode(new SampleMetadata(first.Name, first.OriginalPcmBytes)));
    }

    [Fact]
    public void CatalogBudgetFailureDoesNotEvictOrAdmitPartialStateAndChargesUnknownTails()
    {
        byte[] bytes = Bank(("first", 28, new byte[7]), ("second", 28, new byte[7]));
        var catalog = SampleBankPcmCatalog.Read(bytes, 2, bytes.Length, 64, 7);
        var a = catalog.Decode(catalog.Metadata.Entries[0]); Assert.Equal(1u, a.UnwrittenTailBytes);
        Assert.Equal(4, catalog.CachedPcmBytes);
        Assert.Throws<InvalidDataException>(() => catalog.Decode(catalog.Metadata.Entries[1]));
        Assert.Equal(1, catalog.CachedSamples); Assert.Equal(4, catalog.CachedPcmBytes);
        Assert.Same(a, catalog.Decode(catalog.Metadata.Entries[0]));
        Assert.Throws<InvalidDataException>(() => SampleBankPcmCatalog.Read(bytes, 0, bytes.Length - 1, 64, 64));
        Assert.Throws<InvalidDataException>(() => SampleBankPcmCatalog.Read(bytes[..^1], 0, bytes.Length, 64, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => SampleBankPcmCatalog.Read(bytes, 0, -1, 64, 64));
    }

    [GameDataFact]
    public void InstalledWarningPcmMatchesPinnedMaterializerAtNativeRateAndConvertedSize()
    {
        byte[] bank = GameData.Read(@"data\sounds\sounds_english_pc.xap").Bytes;
        Assert.Equal("658C15E3BAB844D65DD3C07C4AC880F16F741C0EA116F48C603449BBD4DDA8B7", Convert.ToHexString(SHA256.HashData(bank)));
        var catalog = SampleBankPcmCatalog.Read(bank, 0, bank.Length, 1 << 24, 1 << 24);
        SampleMetadata metadata = catalog.Metadata.Find(@"Battle Engine\N_BE_incoming_missile")!;
        var data = catalog.Decode(metadata);
        Assert.Equal(6944, data.Data.Length); Assert.True(data.FullyInitialized);
        // Independent existing materializer's pinned incoming-missile WAV hash is 7f00ce0c...b9dcd0d;
        // this is its PCM payload, with no RIFF header. No converted retail bytes are embedded in this test.
        Assert.Equal("538982D77B11D3D2AC434AB3CA679DDE57DD372C14350DA30641FE50B686E518",
            Convert.ToHexString(SHA256.HashData(data.Data.Span)));
        var reduced = SamplePcm.ConvertPcm16(data.Data.Span, 1, 1 << 24);
        Assert.Equal(3472, reduced.Data.Length); Assert.True(reduced.FullyInitialized);
    }

    [GameDataFact]
    public void CompleteInstalledBankMatchesIndependentMaterializerWithoutChangingSampleOrder()
    {
        byte[] bank = GameData.Read(@"data\sounds\sounds_english_pc.xap").Bytes;
        Assert.Equal("658C15E3BAB844D65DD3C07C4AC880F16F741C0EA116F48C603449BBD4DDA8B7", Convert.ToHexString(SHA256.HashData(bank)));
        var catalog = SampleBankPcmCatalog.Read(bank, 0, bank.Length, 1 << 20, 32 << 20);
        using var ordered = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (SampleMetadata metadata in catalog.Metadata.Entries)
        {
            var pcm = catalog.Decode(metadata);
            Assert.True(pcm.FullyInitialized); Assert.Equal(metadata.OriginalPcmBytes, (uint)pcm.Data.Length);
            ordered.AppendData(SHA256.HashData(pcm.Data.Span));
        }
        Assert.Equal(164, catalog.CachedSamples); Assert.Equal(21_537_072, catalog.CachedPcmBytes);
        Assert.Equal("546F74BA46BA2B95A1288B89ADC774619A21C15FCF7E7247F21BEEF9070C7A40",
            Convert.ToHexString(ordered.GetHashAndReset()));
    }

    [RetailOracleFact("sample-pcm")]
    public void AuthoredInputsMatchIsolatedOriginalDecoderAndConverterIncludingUnwrittenTails()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] words, string expected) in RetailOracle.Cases("sample-pcm"))
        {
            int rate = unchecked((int)words[0]); uint original = words[1]; int length = checked((int)words[2]);
            byte[] input = new byte[(words.Length - 3) * 4];
            for (int i = 3; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan((i - 3) * 4), words[i]);
            Assert.True(length <= input.Length);
            var data = function switch
            {
                "Bank" => SamplePcm.DecodeBankSample(input.AsSpan(0, length), original, rate, 4096),
                "Raw" when original == length => SamplePcm.ConvertPcm16(input.AsSpan(0, length), rate, 4096),
                _ => throw new InvalidDataException("Unknown authored PCM oracle case."),
            };
            string line = $"Pcm {data.Format.RequestedBufferBytes:x8} {data.Format.BufferBytes:x8} {data.Data.Length:x8} "
                + Convert.ToHexString(SHA256.HashData(data.Data.Span)).ToLowerInvariant();
            Assert.True(line == expected, $"case {lines.Count}: {line} != {expected}"); lines.Add(line);
        }
        Assert.Equal(616, lines.Count);
        RetailOracle.WriteOurs("sample-pcm", lines.ToArray());
    }
}
