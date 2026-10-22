// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OnslaughtRebuild.Parity.Data;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class DataReaderTests
{
    private static byte[] Pack(params byte[][] blocks)
    {
        var file = new MemoryStream();
        foreach (byte[] block in blocks)
        {
            var packed = new MemoryStream();
            using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            {
                z.Write(block);
            }

            var length = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, (int)packed.Length);
            file.Write(length);
            file.Write(packed.ToArray());
        }

        return file.ToArray();
    }

    [Fact]
    public void PackedFilesInflateBlockByBlockAndPlainFilesDoNot()
    {
        byte[] raw = Encoding.ASCII.GetBytes("hello world");
        MemBuffer packed = MemBuffer.FromFile(@"data\resources\x.AYA", Pack(raw[..5], raw[5..]));
        MemBuffer plain = MemBuffer.FromFile(@"data\x.dat", raw);

        Assert.Equal(raw.Length, packed.Length);
        Assert.Equal(raw.Length, plain.Length);
        var got = new byte[raw.Length];
        Assert.Equal(raw.Length, packed.Read(got));
        Assert.Equal(raw, got);
        Assert.False(packed.EndOfFile);
    }

    [Fact]
    public void ReadsAndSkipsPastTheEndStopShortAndSetEndOfFile()
    {
        MemBuffer b = MemBuffer.FromFile("x.dat", [1, 2, 3, 4, 5]);

        Assert.Equal(2, b.Skip(2));
        var got = new byte[10];
        Assert.Equal(3, b.Read(got));
        Assert.True(b.EndOfFile);
        Assert.Equal(5u, b.Position);
        Assert.Equal(0, b.Skip(1));
    }

    [Fact]
    public void ReadStringStopsAfterALineFeedAndFoldsCrLf()
    {
        MemBuffer b = MemBuffer.FromFile("x.dat", Encoding.ASCII.GetBytes("ab\r\ncd\nefgh"));

        Assert.Equal("ab\n", Encoding.ASCII.GetString(b.ReadString(256)));
        Assert.Equal(4u, b.Position);
        Assert.Equal("cd\n", Encoding.ASCII.GetString(b.ReadString(256)));
        Assert.Equal("ef", Encoding.ASCII.GetString(b.ReadString(3)));
        Assert.Equal("gh", Encoding.ASCII.GetString(b.ReadString(256)));
        Assert.True(b.EndOfFile);
    }

    [Fact]
    public void ChunksReadTheirTagAndSizeAndSkipTheRest()
    {
        var file = new MemoryStream();
        void Chunk(string tag, byte[] body)
        {
            Span<byte> h = stackalloc byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(h, ChunkReader.MakeId(tag));
            BinaryPrimitives.WriteUInt32LittleEndian(h[4..], (uint)body.Length);
            file.Write(h);
            file.Write(body);
        }

        Chunk("LVLR", [7, 0, 0, 0, 9, 9]);
        Chunk("TARG", [1, 0, 0, 0]);
        var reader = new ChunkReader(MemBuffer.FromFile("x.dat", file.ToArray()));

        Assert.Equal(ChunkReader.MakeId("LVLR"), reader.GetNext());
        Assert.Equal(7, reader.ReadInt32());
        Assert.Equal(2, reader.Skip());
        Assert.Equal(ChunkReader.MakeId("TARG"), reader.GetNext());
        Assert.Equal(1, reader.ReadInt32());
        Assert.Equal(0u, reader.GetNext());
    }

    [GameDataFact]
    public void TheLevel100ResourceFileWalksToItsEnd()
    {
        (string name, byte[] bytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        var reader = new ChunkReader(MemBuffer.FromFile(name, bytes));
        var tags = new List<string>();
        uint tag;
        while ((tag = reader.GetNext()) != 0)
        {
            tags.Add(Encoding.ASCII.GetString(BitConverter.GetBytes(tag)));
            reader.Skip();
        }

        Assert.Equal("LVLR", tags[0]);
        Assert.True(reader.File.EndOfFile || reader.File.Position == (uint)reader.File.Length);
        Assert.Contains("ERES", tags);
        Assert.Contains("WRES", tags);
    }
}
