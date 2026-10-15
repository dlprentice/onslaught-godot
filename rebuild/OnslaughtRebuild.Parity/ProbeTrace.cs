// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OnslaughtRebuild.Parity;

/// <summary>
/// The probes the parity harness reads, in report order, with their raw little-endian byte lengths. Retail
/// reads them from memory after the Present call of the sample frame returns, before that frame's tape events.
/// </summary>
public static class ProbeCatalog
{
    /// <summary>Event manager time in game seconds (float).</summary>
    public const string EventTime = "EVT";

    /// <summary>CGame frame time (float).</summary>
    public const string FrameTime = "FT";

    /// <summary>CGame score (int).</summary>
    public const string Score = "SC";

    /// <summary>CGame current level number (int).</summary>
    public const string Level = "LVL";

    /// <summary>The game's random stream: last value, then seed (two uint32).</summary>
    public const string Random = "RNG";

    /// <summary>Player 0's battle engine position (three floats).</summary>
    public const string Position = "POS";

    /// <summary>Player 0's battle engine life and energy (two floats).</summary>
    public const string LifeEnergy = "LIFE";

    public static IReadOnlyList<string> Names { get; } =
        [EventTime, FrameTime, Score, Level, Random, Position, LifeEnergy];

    public static int LengthOf(string name) => name switch
    {
        EventTime or FrameTime or Score or Level => 4,
        Random or LifeEnergy => 8,
        Position => 12,
        _ => throw new ArgumentException($"Unknown probe '{name}'.", nameof(name)),
    };

    public static bool IsKnown(string name) => Names.Contains(name);

    /// <summary>A human-readable decoding of a probe's bytes; the comparison itself is always on the bytes.</summary>
    public static string Describe(string name, ReadOnlySpan<byte> bytes)
    {
        static string F(ReadOnlySpan<byte> b, int at) =>
            BinaryPrimitives.ReadSingleLittleEndian(b[at..]).ToString("R", CultureInfo.InvariantCulture);

        return name switch
        {
            EventTime or FrameTime => F(bytes, 0),
            Score or Level => BinaryPrimitives.ReadInt32LittleEndian(bytes).ToString(CultureInfo.InvariantCulture),
            Random => string.Create(CultureInfo.InvariantCulture,
                $"value={BinaryPrimitives.ReadUInt32LittleEndian(bytes):x8} seed={BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]):x8}"),
            Position => $"({F(bytes, 0)}, {F(bytes, 4)}, {F(bytes, 8)})",
            LifeEnergy => $"life={F(bytes, 0)} energy={F(bytes, 4)}",
            _ => Convert.ToHexString(bytes).ToLowerInvariant(),
        };
    }
}

/// <summary>
/// A probe trace: for each sample frame, the raw bytes of each probe. Text form is one line per probe,
/// <c>P &lt;frame&gt; &lt;NAME&gt; &lt;hex&gt;</c>, as the retail harness writes its golden traces. A frame/name
/// pair may repeat only with identical bytes (the retail harness logs its final frame twice).
/// </summary>
public sealed class ProbeTrace
{
    private readonly SortedDictionary<long, Dictionary<string, byte[]>> _samples = new();

    public IEnumerable<long> Frames => _samples.Keys;

    public int SampleCount => _samples.Count;

    public bool TryGet(long frame, string name, out byte[] bytes)
    {
        bytes = [];
        return _samples.TryGetValue(frame, out var probes) && probes.TryGetValue(name, out bytes!);
    }

    public IReadOnlyDictionary<string, byte[]>? At(long frame) =>
        _samples.TryGetValue(frame, out var probes) ? probes : null;

    public void Add(long frame, string name, ReadOnlySpan<byte> bytes)
    {
        if (!ProbeCatalog.IsKnown(name))
        {
            throw new ArgumentException($"Unknown probe '{name}'.", nameof(name));
        }

        if (bytes.Length != ProbeCatalog.LengthOf(name))
        {
            throw new ArgumentException($"Probe {name} needs {ProbeCatalog.LengthOf(name)} bytes, got {bytes.Length}.", nameof(bytes));
        }

        if (!_samples.TryGetValue(frame, out var probes))
        {
            probes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            _samples[frame] = probes;
        }

        if (probes.TryGetValue(name, out byte[]? existing))
        {
            if (!existing.AsSpan().SequenceEqual(bytes))
            {
                throw new InvalidOperationException($"Probe {name} at frame {frame} recorded twice with different bytes.");
            }

            return;
        }

        probes[name] = bytes.ToArray();
    }

    public static ProbeTrace Load(string path) => Parse(File.ReadAllText(path, Encoding.ASCII));

    public static ProbeTrace Parse(string text)
    {
        var trace = new ProbeTrace();
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            if (line.Length == 0 && n == lines.Length - 1)
            {
                break;
            }

            string[] f = line.Split(' ');
            if (f.Length != 4 || f[0] != "P"
                || !long.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out long frame)
                || f[3].Length % 2 != 0)
            {
                throw new FormatException($"Probe trace line {n + 1}: expected 'P <frame> <NAME> <hex>': '{line}'.");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromHexString(f[3]);
            }
            catch (FormatException)
            {
                throw new FormatException($"Probe trace line {n + 1}: '{f[3]}' is not hex.");
            }

            try
            {
                trace.Add(frame, f[2], bytes);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                throw new FormatException($"Probe trace line {n + 1}: {e.Message}");
            }
        }

        return trace;
    }

    /// <summary>Writes the trace in the golden text form, frames ascending, probes in catalog order.</summary>
    public void Write(TextWriter writer)
    {
        foreach (var (frame, probes) in _samples)
        {
            foreach (string name in ProbeCatalog.Names)
            {
                if (probes.TryGetValue(name, out byte[]? bytes))
                {
                    writer.Write(string.Create(CultureInfo.InvariantCulture,
                        $"P {frame} {name} {Convert.ToHexString(bytes).ToLowerInvariant()}\n"));
                }
            }
        }
    }
}
