// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Guards the 2026-07-26 correction that every retail frontend HEADER TITLE is
/// drawn in font22 at scale 1, on all four header pages — not Font13PS at 1.5
/// on FEP_DEVSELECT and FEP_LEVEL_SELECT.
///
/// <para><b>How the retail numbers below were obtained.</b> Atlas-free, from the
/// pristine 640x480 captures under
/// <c>local-lab/retail-reference-pristine/</c> alone. Cut the header title band
/// (x 200..580, y 70..92 — inside the header box, clear of the arc brackets and
/// the end-cap chrome), threshold luminance &gt; 120 for ink, then segment the
/// ink into per-glyph column runs. No rescaling and no font atlas are involved,
/// so the measurement cannot be biased by which atlas we believe in.</para>
///
/// <code>
///   page                  ink rows   ink x       per-glyph run widths
///   MISSION BRIEFING      72..88     288..490    17,2,13,13,2,15,12,12,12,2,11,10,2,12,13
///   SELECT CONFIGURATION  72..88     249..526    13,11,10,11,13,14,13,15,12,10,2,13,13,12,12,14,2,15,12
///   SELECT LEVEL          72..88     304..471    13,11,10,11,13,14,10,11,14,11,10
///   CHOOSE GAME NAME      72..88     263..513    13,12,15,15,13,11,13,12,17,11,12,12,17,11
/// </code>
///
/// <para>All four share one 17-row ink height, and SELECT LEVEL's first six
/// glyph widths are identical to SELECT CONFIGURATION's — both spell "SELECT".
/// Per-letter mask IoU at 1:1 with no rescaling, against SELECT CONFIGURATION:
/// MISSION BRIEFING 0.992 over 8 shared letters, SELECT LEVEL 0.990 over 5,
/// CHOOSE GAME NAME 0.979 over 7. MISSION BRIEFING and SELECT CONFIGURATION
/// were already fitted to font22 at scale 1 by normalised cross-correlation
/// (0.951 against Font13PS's best 0.569), so the other two are the same draw.
/// A 16px Font13PS cell scaled by 1.5 cannot land on the same integer glyph
/// widths a 32px cell produces at 1:1 — and it did not.</para>
///
/// <para><b>What this gate does NOT cover.</b> It measures glyph advance and ink
/// height only. It says nothing about glyph SHAPE (a wrong atlas with the same
/// advances would pass), nothing about colour, nothing about the header box
/// fill, and nothing about any pixel outside the title band. It also does not
/// cover the pages' backgrounds, which are measured separately — see
/// <c>local-lab/STARTUP-NOFMV-BASELINE-2026-07-26.md</c> §5.1.</para>
///
/// <para><b>Known residual, deliberately not closed here.</b> The reconstruction
/// reproduces every run width exactly but sits <b>one pixel right</b> of retail
/// on all four pages (264 vs 263, 305 vs 304, 250 vs 249). That offset is a
/// property of the shared <c>DrawHeaderBarTitle</c> origin rounding and predates
/// this change; it is recorded rather than tuned, because correcting it moves
/// four pinned baselines at once and <c>HEADER_BAR_X = 390</c> is a source
/// constant (FrontEnd.cpp:1103) that should not be bent to absorb a raster
/// rounding difference.</para>
/// </summary>
public sealed class RetailFrontendHeaderFontTests
{
    /// <summary>Header title band, measured in the pristine retail captures.</summary>
    private const int BandX0 = 200;
    private const int BandX1 = 580;
    private const int BandY0 = 70;
    private const int BandY1 = 92;
    private const int InkThreshold = 120;

    /// <summary>Retail ink rows, identical on all four header pages.</summary>
    private const int RetailInkTop = 72;
    private const int RetailInkBottom = 88;

    private static readonly HeaderPage[] Pages =
    [
        new(
            "FEP_DEVSELECT / CHOOSE GAME NAME",
            "06-dev-select-settled.png",
            [13, 12, 15, 15, 13, 11, 13, 12, 17, 11, 12, 12, 17, 11]),
        new(
            "FEP_LEVEL_SELECT / SELECT LEVEL",
            "08-level-select-settled.png",
            [13, 11, 10, 11, 13, 14, 10, 11, 14, 11, 10]),
    ];

    /// <summary>
    /// xUnit v2 cannot turn an executing test into a skip. Resolve the optional
    /// local-evidence prerequisite during discovery instead, so an absent or
    /// stale capture is reported as skipped rather than as a green pixel gate.
    /// </summary>
    private sealed class CurrentStartupCaptureFactAttribute : FactAttribute
    {
        public CurrentStartupCaptureFactAttribute()
        {
            string? explicitCapture =
                Environment.GetEnvironmentVariable("ONSLAUGHT_STARTUP_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(explicitCapture) &&
                ResolveStartupCaptureDirectory() is null)
            {
                Skip = "No current startup capture is available to score.";
            }
        }
    }

    /// <summary>
    /// The two pages this change corrected must reproduce retail's per-glyph
    /// advance pattern and ink height exactly. Font13PS at 1.5 does not.
    /// </summary>
    [CurrentStartupCaptureFact]
    public void CorrectedHeaderTitlesReproduceRetailGlyphRuns()
    {
        string captureDirectory = ResolveStartupCaptureDirectory() ??
            throw new InvalidOperationException(
                "ONSLAUGHT_STARTUP_CAPTURE_DIR does not contain the required " +
                "startup frames, or the selected capture disappeared after discovery.");

        var failures = new List<string>();
        foreach (HeaderPage page in Pages)
        {
            string file = Path.Combine(captureDirectory, page.File);
            Assert.True(File.Exists(file), $"Startup capture is missing {file}.");

            (int inkTop, int inkBottom, int[] runs) = MeasureTitleBand(file);

            if (inkTop != RetailInkTop || inkBottom != RetailInkBottom)
            {
                failures.Add(
                    $"{page.Name}: title ink rows are y{inkTop}..{inkBottom}; retail is " +
                    $"y{RetailInkTop}..{RetailInkBottom}. A 17-row cap height is what " +
                    "separates font22 at scale 1 from Font13PS at 1.5.");
            }

            if (!runs.SequenceEqual(page.RetailRunWidths))
            {
                failures.Add(
                    $"{page.Name}: per-glyph run widths are [{Join(runs)}]; retail is " +
                    $"[{Join(page.RetailRunWidths)}].");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Header titles in '{captureDirectory}' no longer match retail:" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static string Join(IEnumerable<int> values) =>
        string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Thresholds the header title band and returns the ink row extent plus the
    /// width of every contiguous ink column run. One run is one glyph: retail's
    /// header titles never let adjacent glyphs touch in this band.
    /// </summary>
    private static (int InkTop, int InkBottom, int[] RunWidths) MeasureTitleBand(string pngPath)
    {
        (int width, int height, byte[] rgba) = DecodePng(File.ReadAllBytes(pngPath));
        Assert.True(
            width >= BandX1 && height >= BandY1,
            $"{pngPath} is {width}x{height}, too small for the header title band.");

        int bandWidth = BandX1 - BandX0;
        var columnHasInk = new bool[bandWidth];
        int inkTop = int.MaxValue;
        int inkBottom = int.MinValue;

        for (int y = BandY0; y < BandY1; y++)
        {
            int row = y * width * 4;
            for (int x = BandX0; x < BandX1; x++)
            {
                int offset = row + (x * 4);
                // ITU-R BT.601 luma, matching PIL's "L" conversion used to take
                // the retail numbers this gate compares against.
                double luma = (0.299 * rgba[offset]) +
                    (0.587 * rgba[offset + 1]) +
                    (0.114 * rgba[offset + 2]);
                if (luma > InkThreshold)
                {
                    columnHasInk[x - BandX0] = true;
                    inkTop = Math.Min(inkTop, y);
                    inkBottom = Math.Max(inkBottom, y);
                }
            }
        }

        Assert.True(inkBottom >= inkTop, $"{pngPath} has no header title ink in the band.");

        var runs = new List<int>();
        int index = 0;
        while (index < bandWidth)
        {
            if (!columnHasInk[index])
            {
                index++;
                continue;
            }
            int end = index;
            while (end + 1 < bandWidth && columnHasInk[end + 1])
            {
                end++;
            }
            runs.Add(end - index + 1);
            index = end + 1;
        }

        return (inkTop, inkBottom, [.. runs]);
    }

    /// <summary>
    /// Picks the startup capture to score. <c>ONSLAUGHT_STARTUP_CAPTURE_DIR</c>
    /// always wins — naming a directory is the operator taking responsibility
    /// for what is in it. Otherwise the newest directory under
    /// <c>local-lab/godot-captures/</c> that holds a startup plan's shots and is
    /// at least as new as <c>RetailFrontendFlow.cs</c> is used; a capture that
    /// predates the current frontend source describes a build that no longer
    /// exists, so it is ignored rather than judged.
    /// </summary>
    private static string? ResolveStartupCaptureDirectory()
    {
        string? named = Environment.GetEnvironmentVariable("ONSLAUGHT_STARTUP_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(named))
        {
            return HasStartupShots(named) ? named : null;
        }

        string? repoRoot = FindRepositoryRoot();
        if (repoRoot is null)
        {
            return null;
        }

        string captureRoot = Path.Combine(repoRoot, "local-lab", "godot-captures");
        if (!Directory.Exists(captureRoot))
        {
            return null;
        }

        string flowSource = Path.Combine(
            repoRoot, "rebuild", "OnslaughtRebuild.Godot", "RetailFrontendFlow.cs");
        DateTime floor = File.Exists(flowSource)
            ? File.GetLastWriteTimeUtc(flowSource)
            : DateTime.MinValue;

        return Directory.EnumerateDirectories(captureRoot)
            .Where(HasStartupShots)
            .Where(directory => Directory.GetLastWriteTimeUtc(directory) >= floor)
            .Where(IsFrontendPlan)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static bool HasStartupShots(string directory) =>
        Pages.All(page => File.Exists(Path.Combine(directory, page.File)));

    /// <summary>
    /// The startup plan is identified from the capture manifest rather than from
    /// the directory name, so an experimental capture cannot be mistaken for a
    /// different plan's output.
    /// </summary>
    private static bool IsFrontendPlan(string directory)
    {
        string manifest = Path.Combine(directory, "capture-manifest.json");
        if (!File.Exists(manifest))
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
            return document.RootElement.TryGetProperty("plan", out JsonElement plan) &&
                plan.ValueKind == JsonValueKind.String &&
                string.Equals(plan.GetString(), "startup", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "rebuild")) &&
                File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    /// <summary>
    /// Minimal decoder for the exact PNG subset Godot's <c>Image.save_png</c>
    /// emits for a viewport readback: 8-bit, colour type 6 (RGBA), no
    /// interlacing. Anything else fails rather than being silently
    /// mis-measured. Kept local so the gate needs no image package.
    /// </summary>
    private static (int Width, int Height, byte[] Rgba) DecodePng(byte[] png)
    {
        ReadOnlySpan<byte> signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(png.Length > 8 && png.AsSpan(0, 8).SequenceEqual(signature), "Not a PNG.");

        int width = 0;
        int height = 0;
        using var idat = new MemoryStream();
        int position = 8;
        while (position + 8 <= png.Length)
        {
            int length = ReadBigEndianInt32(png, position);
            string type = Encoding.ASCII.GetString(png, position + 4, 4);
            int dataStart = position + 8;
            if (type == "IHDR")
            {
                width = ReadBigEndianInt32(png, dataStart);
                height = ReadBigEndianInt32(png, dataStart + 4);
                Assert.Equal(8, png[dataStart + 8]);
                Assert.Equal(6, png[dataStart + 9]);
                Assert.Equal(0, png[dataStart + 12]);
            }
            else if (type == "IDAT")
            {
                idat.Write(png, dataStart, length);
            }
            else if (type == "IEND")
            {
                break;
            }
            position = dataStart + length + 4;
        }

        Assert.True(width > 0 && height > 0, "PNG had no IHDR.");
        idat.Position = 0;
        using var inflate = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        int read = 0;
        while (read < raw.Length)
        {
            int got = inflate.Read(raw, read, raw.Length - read);
            if (got == 0)
            {
                break;
            }
            read += got;
        }
        Assert.Equal(raw.Length, read);

        var rgba = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            int source = (y * (stride + 1)) + 1;
            int target = y * stride;
            for (int x = 0; x < stride; x++)
            {
                int left = x >= 4 ? rgba[target + x - 4] : 0;
                int up = y > 0 ? rgba[target - stride + x] : 0;
                int upLeft = (x >= 4 && y > 0) ? rgba[target - stride + x - 4] : 0;
                int value = raw[source + x];
                rgba[target + x] = filter switch
                {
                    0 => (byte)value,
                    1 => (byte)(value + left),
                    2 => (byte)(value + up),
                    3 => (byte)(value + ((left + up) / 2)),
                    4 => (byte)(value + Paeth(left, up, upLeft)),
                    _ => throw new InvalidDataException($"Unsupported PNG filter {filter}."),
                };
            }
        }
        return (width, height, rgba);
    }

    private static int ReadBigEndianInt32(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }
        return pb <= pc ? b : c;
    }

    private sealed record HeaderPage(string Name, string File, int[] RetailRunWidths);
}
