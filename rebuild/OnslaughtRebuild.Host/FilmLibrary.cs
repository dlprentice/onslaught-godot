// SPDX-License-Identifier: GPL-3.0-only
// The films the lead's rebuild/tools/materialize_films.py writes (films.json and one BFA1 archive per film), read for
// the parity core's FilmPlayer and drawn by the host. The archives hold retail-derived frames and stay local.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text.Json;
using OnslaughtRebuild.Parity.Flow;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The materialized films: films.json gives each film's Bink header (frames, size, rate) and its archive; the archive
/// ("BFA1") gives frame k as zlib-compressed BGRA rows, k the k-th BinkDoFrame from 0. A film the manifest does not list
/// does not open, as data\video\&lt;name&gt;.vid would not.
/// </summary>
public sealed class FilmLibrary : IFilmFiles
{
    private const int HeaderSize = 28;
    private const int EntrySize = 12;

    private readonly string _root;
    private readonly Dictionary<string, Entry> _films = new(StringComparer.OrdinalIgnoreCase);

    private sealed record Entry(int Frames, int Width, int Height, int RateNumerator, int RateDenominator, string Archive,
        string? Sound);

    public FilmLibrary(string root)
    {
        _root = root;
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "films.json")));
        foreach (JsonProperty film in manifest.RootElement.EnumerateObject())
        {
            JsonElement f = film.Value;
            _films[film.Name] = new Entry(f.GetProperty("frames").GetInt32(), f.GetProperty("width").GetInt32(),
                f.GetProperty("height").GetInt32(), f.GetProperty("rate_num").GetInt32(), f.GetProperty("rate_den").GetInt32(),
                f.GetProperty("archive").GetString()!,
                f.TryGetProperty("sound", out JsonElement s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null);
        }
    }

    /// <summary>The library under ONSLAUGHT_FILMS, or null when it is not set or has no manifest.</summary>
    public static FilmLibrary? FromEnvironment()
    {
        string? root = System.Environment.GetEnvironmentVariable("ONSLAUGHT_FILMS");
        return root is not null && File.Exists(Path.Combine(root, "films.json")) ? new FilmLibrary(root) : null;
    }

    /// <summary>IFilmFiles.Probe: the film's Bink header, or null when there is no such film.</summary>
    public FilmHeader? Probe(string name) =>
        _films.TryGetValue(name, out Entry? e) ? new FilmHeader(e.Frames, e.RateNumerator, e.RateDenominator) : null;

    /// <summary>The film's frame size.</summary>
    public (int Width, int Height) Size(string name) => (_films[name].Width, _films[name].Height);

    /// <summary>The film's sound (re-encoded: for presentation, never for timing), when it has one.</summary>
    public string? SoundPath(string name) =>
        _films[name].Sound is { } s ? Path.Combine(_root, s) : null;

    /// <summary>Frame <paramref name="index"/> (0-based) as width * height BGRA bytes, rows top to bottom.</summary>
    public byte[] Frame(string name, int index)
    {
        Entry e = _films[name];
        using FileStream file = File.OpenRead(Path.Combine(_root, e.Archive));
        Span<byte> header = stackalloc byte[HeaderSize];
        file.ReadExactly(header);
        if (!header[..4].SequenceEqual("BFA1"u8)
            || BinaryPrimitives.ReadInt32LittleEndian(header[4..]) != e.Width
            || BinaryPrimitives.ReadInt32LittleEndian(header[8..]) != e.Height
            || BinaryPrimitives.ReadInt32LittleEndian(header[12..]) != e.Frames
            || BinaryPrimitives.ReadInt32LittleEndian(header[24..]) != 4)
        {
            throw new InvalidDataException($"film archive '{e.Archive}' does not match its manifest entry");
        }

        if ((uint)index >= (uint)e.Frames)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"{name} has {e.Frames} frames");
        }

        Span<byte> entry = stackalloc byte[EntrySize];
        file.Position = HeaderSize + ((long)index * EntrySize);
        file.ReadExactly(entry);
        long offset = BinaryPrimitives.ReadInt64LittleEndian(entry);
        int length = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
        var compressed = new byte[length];
        file.Position = offset;
        file.ReadExactly(compressed);
        var pixels = new byte[e.Width * e.Height * 4];
        using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        zlib.ReadExactly(pixels);
        return pixels;
    }
}
