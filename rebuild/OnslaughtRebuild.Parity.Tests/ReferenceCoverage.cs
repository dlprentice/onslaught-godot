// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>The required reference schedule, independent of which rows happen to survive in a trace file.</summary>
internal static class ReferenceCoverage
{
    /// <summary>Preserve repeated identical observations, but reject ambiguous values for the same reference key.</summary>
    public static void AddChecked<TKey, TValue>(Dictionary<TKey, TValue> rows, TKey key, TValue value, string label)
        where TKey : notnull
    {
        if (rows.TryGetValue(key, out TValue? previous) && !EqualityComparer<TValue>.Default.Equals(previous, value))
            throw new InvalidDataException($"Conflicting reference rows for {label}");
        rows[key] = value;
    }

    public static void RequireFrames(IEnumerable<long> frames, long first, long last, int step = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(first, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
        ArgumentOutOfRangeException.ThrowIfLessThan(step, 1);
        var present = frames.ToHashSet();
        for (long p = first; p <= last; p += step)
        {
            if (!present.Contains(p)) throw new InvalidDataException($"Reference lacks required Present {p} ({first}..{last}, step {step}).");
            if (last - p < step) break;
        }
    }

    public static void Require(IReadOnlyDictionary<(long Present, string Name), string> probes,
        long first, long last, int step, params string[] names)
    {
        if (names.Length == 0) throw new ArgumentException("At least one required reference field is needed.", nameof(names));
        RequireFrames(probes.Keys.Select(k => k.Present), first, last, step);
        foreach (string name in names)
        {
            for (long p = first; p <= last; p += step)
            {
                if (!probes.TryGetValue((p, name), out string? value))
                    throw new InvalidDataException($"Reference lacks required {name} at Present {p}.");
                RequireValue(name, value, p);
                if (last - p < step) break;
            }
        }
    }

    public static void RequireValue(string name, string value, long present)
    {
        int bytes = name switch
        {
            "EVT" or "EVN" or "FT" or "SC" or "LVL" => 4,
            "RNG" or "CRT" or "LIFE" => 8,
            "POS" => 12,
            _ => throw new ArgumentException($"Unknown required reference field '{name}'.", nameof(name)),
        };
        // Retail records null when a player's object does not exist; preserve that explicit value.
        if (value == "null" && name is "POS" or "LIFE") return;
        if (value.Length != bytes * 2 || !value.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Reference {name} at Present {present} must contain {bytes} bytes of hex.");
    }
}
