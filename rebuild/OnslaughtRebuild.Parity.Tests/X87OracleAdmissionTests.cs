// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text.Json;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class X87OracleAdmissionTests
{
    private static JsonDocument Manifest(byte[] input, byte[] output, int count = 2) => JsonDocument.Parse(JsonSerializer.Serialize(new
    {
        ops = new Dictionary<string, object>
        {
            ["add_d"] = new { count, inSha256 = Convert.ToHexString(SHA256.HashData(input)), outSha256 = Convert.ToHexString(SHA256.HashData(output)) },
        },
    }));

    [Theory]
    [InlineData(0, 16)]
    [InlineData(31, 16)]
    [InlineData(32, 0)]
    [InlineData(32, 15)]
    public void EmptyOrMisalignedTablesAreRefusedEvenWithMatchingHashes(int inputBytes, int outputBytes)
    {
        byte[] input = new byte[inputBytes], output = new byte[outputBytes];
        using var manifest = Manifest(input, output);
        Assert.Throws<InvalidDataException>(() => X87OracleTable.Admit(manifest.RootElement, "add_d", input, output, 8, 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void ManifestCountMustMatchBothTablesAndTheRequiredFloor(int count)
    {
        byte[] input = new byte[32], output = new byte[16];
        using var manifest = Manifest(input, output, count);
        Assert.Throws<InvalidDataException>(() => X87OracleTable.Admit(manifest.RootElement, "add_d", input, output, 8, 2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AChangedOperandOrResultCannotUseAnAdmittedManifest(bool changeInput)
    {
        byte[] input = new byte[32], output = new byte[16];
        using var manifest = Manifest(input, output);
        (changeInput ? input : output)[0] = 1;
        Assert.Contains(changeInput ? "inSha256" : "outSha256", Assert.Throws<InvalidDataException>(() =>
            X87OracleTable.Admit(manifest.RootElement, "add_d", input, output, 8, 2)).Message);
    }

    [Fact]
    public void MissingOperationAndSmallTablesCannotMeetTheProductionFloor()
    {
        byte[] input = new byte[32], output = new byte[16];
        using var manifest = Manifest(input, output);
        Assert.Throws<InvalidDataException>(() => X87OracleTable.Admit(manifest.RootElement, "sub_d", input, output, 8, 2));
        Assert.Throws<InvalidDataException>(() => X87OracleTable.Admit(manifest.RootElement, "add_d", input, output, 8, 1_000_000));
        Assert.Equal(2, X87OracleTable.Admit(manifest.RootElement, "add_d", input, output, 8, 2));
    }
}
