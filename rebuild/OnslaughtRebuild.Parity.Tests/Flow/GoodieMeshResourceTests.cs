// SPDX-License-Identifier: GPL-3.0-only
using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Mesh;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class GoodieMeshResourceTests
{
    private static byte[] Chunk(string tag, byte[] body)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes(tag));
        writer.Write(body.Length);
        writer.Write(body);
        return stream.ToArray();
    }

    private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    private static byte[] I(int value) => BitConverter.GetBytes(value);

    // An anonymous, zero-part mesh and two opaque texture descriptors, authored for this test.
    internal static byte[] Resource(int number = 8, int type = 1, int count = 2, string meshTag = "MESH",
        byte[]? meshBody = null, bool namedMesh = false)
    {
        byte[] mesh = new byte[MeshData.RecordSize];
        BinaryPrimitives.WriteSingleLittleEndian(mesh.AsSpan(0x164), 1f);
        byte[] name = new byte[309];
        if (namedMesh) name[0] = (byte)'x';
        byte[] meshChunks = namedMesh ? Chunk("PMS2", name) : Join(Chunk("PMS2", name), Chunk("CMSH", mesh),
            Chunk("CMST", []), Chunk("BBOX", Chunk("DATA", new byte[40])));
        byte[] payload = Join(I(number), I(type), Chunk("MDAT", I(count)), Chunk("TXTR", [1, 2]),
            Chunk("TXTR", [3]), Chunk(meshTag, meshBody ?? Chunk("PMSH", meshChunks)));
        return Join(Chunk("LVLR", I(7)), Chunk("GDIE", Chunk("GDAT", payload)));
    }

    internal static (string Name, byte[] Bytes) NoExternalMesh(string _) =>
        throw new InvalidOperationException("the authored mesh has no external file");

    [Fact]
    public void ReadsTheExpectedMeshWhileKeepingTexturePayloadsOutOfTheSimulation()
    {
        GoodieMeshResource value = GoodieMeshResource.Read("authored.bin", Resource(), 8, NoExternalMesh);
        Assert.Equal(8, value.Number);
        Assert.Equal(2, value.TextureCount);
        Assert.Equal(1f, value.Mesh.Radius);
        Assert.Equal(0, value.Mesh.NumParts);
        Assert.NotNull(value.Mesh.BoundingBox);
    }

    [Fact]
    public void RejectsMismatchedTypesNumbersCountsAndChunkShapes()
    {
        foreach (byte[] bytes in new[] { Resource(number: 9), Resource(type: 0), Resource(count: 1),
                     Resource(count: 3), Resource(count: 0), Resource(count: int.MaxValue), Resource(meshTag: "FAKE") })
            Assert.Throws<InvalidDataException>(() => GoodieMeshResource.Read("authored.bin", bytes, 8, NoExternalMesh));
        Assert.Throws<ArgumentOutOfRangeException>(() => GoodieMeshResource.Read("authored.bin", Resource(), -1, NoExternalMesh));
    }

    [Fact]
    public void RejectsTruncationAndDuplicateGoodieChunks()
    {
        byte[] bytes = Resource();
        foreach (int removed in new[] { 1, 7, 8, 40 })
            Assert.Throws<InvalidDataException>(() => GoodieMeshResource.Read("authored.bin", bytes[..^removed], 8, NoExternalMesh));
        Assert.Throws<InvalidDataException>(() => GoodieMeshResource.Read("authored.bin", Join(bytes, bytes), 8, NoExternalMesh));
    }

    [Fact]
    public void RejectsEmptyTruncatedAndWrongMeshWrappersEvenWhenTheirParentSizesAreValid()
    {
        foreach (byte[] body in new[] { Array.Empty<byte>(), new byte[] { 1 }, Chunk("PMSH", []),
                     Chunk("PMSH", Chunk("PMS2", new byte[309])),
                     Chunk("NOPE", Chunk("PMS2", new byte[309])), Chunk("PMSH", Chunk("NOPE", new byte[309])) })
            Assert.Throws<InvalidDataException>(() => GoodieMeshResource.Read("authored.bin", Resource(meshBody: body), 8, NoExternalMesh));
    }

    [Fact]
    public void RejectsAnExternalMeshWhoseReaderRunsPastItsEnd()
    {
        Assert.Throws<InvalidDataException>(() => GoodieMeshResource.Read("authored.bin", Resource(namedMesh: true), 8,
            _ => ("truncated.bin", Array.Empty<byte>())));
    }

    [GameDataFact]
    public void InstalledGoodieEightProvidesItsMeshAndFiveTextureDescriptors()
    {
        var (name, bytes) = GameData.Read(@"data\resources\goodie_08_res_PC.aya");
        GoodieMeshResource value = GoodieMeshResource.Read(name, bytes, 8, GameData.Read);
        Assert.Equal(5, value.TextureCount);
        Assert.True(value.Mesh.NumParts > 0);
        Assert.NotNull(value.Mesh.BoundingBox);
    }
}
