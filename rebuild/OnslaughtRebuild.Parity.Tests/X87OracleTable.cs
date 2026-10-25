// SPDX-License-Identifier: GPL-3.0-only

using System.Security.Cryptography;
using System.Text.Json;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>Admits an oracle operation's exact manifest count and bytes before numerical comparisons can pass.</summary>
internal static class X87OracleTable
{
    public static (byte[] Input, byte[] Output, int Count) Read(string root, string op, int resultBytes, int minimumCount)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "manifest.json")));
        byte[] input = File.ReadAllBytes(Path.Combine(root, $"{op}.in.bin"));
        byte[] output = File.ReadAllBytes(Path.Combine(root, $"{op}.out.bin"));
        int count = Admit(manifest.RootElement, op, input, output, resultBytes, minimumCount);
        return (input, output, count);
    }

    internal static int Admit(JsonElement manifest, string op, ReadOnlySpan<byte> input, ReadOnlySpan<byte> output,
        int resultBytes, int minimumCount)
    {
        if (resultBytes is not (4 or 8)) throw new ArgumentOutOfRangeException(nameof(resultBytes));
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumCount, 1);
        if (manifest.ValueKind != JsonValueKind.Object || !manifest.TryGetProperty("ops", out JsonElement ops)
            || ops.ValueKind != JsonValueKind.Object || !ops.TryGetProperty(op, out JsonElement operation)
            || operation.ValueKind != JsonValueKind.Object || !operation.TryGetProperty("count", out JsonElement declared)
            || declared.ValueKind != JsonValueKind.Number || !declared.TryGetInt32(out int count) || count < minimumCount)
            throw new InvalidDataException($"Oracle {op} needs a manifest count of at least {minimumCount} cases.");
        if (input.Length == 0 || input.Length % 16 != 0)
            throw new InvalidDataException($"Oracle {op} input must contain nonempty, aligned 16-byte operand records.");
        if (output.Length == 0 || output.Length % resultBytes != 0)
            throw new InvalidDataException($"Oracle {op} output must contain nonempty, aligned {resultBytes}-byte results.");
        if (input.Length / 16 != count || output.Length / resultBytes != count)
            throw new InvalidDataException($"Oracle {op} input/output counts must equal the manifest's {count} cases.");
        RequireHash(operation, "inSha256", input, op);
        RequireHash(operation, "outSha256", output, op);
        return count;
    }

    private static void RequireHash(JsonElement operation, string field, ReadOnlySpan<byte> bytes, string op)
    {
        if (!operation.TryGetProperty(field, out JsonElement hash) || hash.ValueKind != JsonValueKind.String
            || hash.GetString() is not { Length: 64 } expected
            || !string.Equals(expected, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Oracle {op} {field} does not match its manifest.");
    }
}
