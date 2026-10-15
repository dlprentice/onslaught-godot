// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the Level 100 water colour envelope against retail pixels.
///
/// <para><b>Why this exists.</b> On 2026-07-25 the reconstruction's water was
/// clipping to pure white and had lost retail's B &gt; G &gt; R hue rule. Two
/// uncited passes were responsible, and both had passed code review: an
/// additive second draw of the shoreline that re-applied <c>water-waves</c>
/// (the only water texture with a hue) on top of a primary pass that already
/// consumed it, and a <c>D3DTOP_MULTIPLYADD</c> stage whose operands were
/// mapped as <c>waves + diffuse * current</c> instead of
/// <c>waves * diffuse + current</c>. Reviewing the shader could not catch
/// either. Only pixels could.</para>
///
/// <para><b>Why it was re-derived on 2026-07-26.</b> The first version of this
/// gate scored three FIXED screen rectangles at four offsets. That was wrong in
/// a way the camera-FOV correction (a382a8e4) exposed: the opening pan is in
/// motion, so a fixed rectangle does not stay on water. Measured over
/// <c>opening-pan-run1</c>, retail's OWN <c>open-sea-right</c> rectangle drops
/// to 65.65% B &gt; G &gt; R by t0+749 ms and to 0.17% by t0+2507 ms, because
/// the pan has swept an island and then the shore through it; retail's own
/// <c>caustic-band</c> rectangle holds 1,241 pixels at 250+ on all three
/// channels at t0+1255 ms, from snow, not water. The gate would have failed the
/// reference it was built from. It was measuring pan geometry, not water.</para>
///
/// <para><b>How the samples below were chosen.</b> A rectangle now belongs to
/// one offset, and it was selected from RETAIL alone, by exhaustive search over
/// x,y in steps of 10 and widths/heights in {20..320}, for the largest
/// rectangle in that retail frame with: zero pixels at 235+ on any channel (no
/// snow, sky glare or specular), and at least 99% of pixels satisfying
/// B &gt; G &gt; R (unambiguous open sea, not the horizon haze band where the
/// fog blend puts G at or under R in retail too). The reconstruction was not
/// consulted in the search. All six were then confirmed by eye to be open sea
/// in retail and in the reconstruction.</para>
///
/// <para>The same search says open water is only ON SCREEN in Level 100 for the
/// first ~1.5 s. At t0+1756 ms and beyond the largest qualifying rectangle is
/// sky or the blue hull of a structure, and from t0+6006 ms the settled cockpit
/// view contains no sea at all - the best "B &gt; G &gt; R" rectangle there,
/// (480,170)-(640,260), stable for ten seconds, is dark blue canopy strut. So
/// the widest honest offset range for a water gate is 2..1255 ms, and the
/// t0+1506 ms rectangle is dropped for being a 2,400 px sliver, under 1% of the
/// frame.</para>
///
/// <para><b>The retail envelope</b>, over
/// <c>local-lab/retail-reference-pristine/level100-gameplay/opening-pan-run1</c>,
/// 73,200 px:</para>
/// <code>
///   offset  rect                  n       median         p99            white  B&gt;G&gt;R
///   2 ms    (  0,200)-(240,260)  14,400  (101,128,150)  (164,188,212)     0    99.69%
///   256 ms  ( 60,160)-(220,250)  14,400  ( 91,108,128)  (152,176,204)     0    99.68%
///   499 ms  (  0,160)-(160,280)  19,200  ( 91,111,130)  (160,179,204)     0    99.07%
///   749 ms  (  0,170)-(120,290)  14,400  ( 82, 96,114)  (159,176,197)     0    99.28%
///   1006 ms ( 30,210)-(150,270)   7,200  ( 76, 86,103)  (134,166,188)     0    99.42%
///   1255 ms (  0,240)-( 60,300)   3,600  ( 79, 94,114)  (151,184,209)     0    99.06%
/// </code>
/// <para>Cross-run: <c>opening-pan-run2</c> at t0+2 ms reads median
/// (100,127,149), p99 (165,188,212), 0 white, 99.67% - retail's own run-to-run
/// spread is 1 unit of median. "white" is pixels at or above 250 on all three
/// channels; retail has none, in any rectangle, at any of these offsets.</para>
///
/// <para><b>Scope.</b> This gate needs a locally produced gameplay capture,
/// which lives on an ignored path:</para>
/// <code>
///   npm run capture:rebuild-godot -- --plan gameplay --purpose production \
///       --retail-offset-manifest local-lab/retail-reference-pristine/level100-gameplay/manifest.json
/// </code>
/// <para>The launcher writes it under <c>local-data/first-flight/</c>; point
/// <c>ONSLAUGHT_WATER_CAPTURE_DIR</c> at that directory, or keep it under
/// <c>local-lab/godot-captures/</c>, where this gate looks by itself.</para>
/// <para>If no capture is scored the pixel assertions cannot run, and
/// <see cref="ShorelineCompositionKeepsTheOperandOrderTheEnvelopeDependsOn"/>
/// is the always-on backstop for the specific regressions above.</para>
/// </summary>
public sealed class Level100WaterEnvelopeTests
{
    /// <summary>
    /// One retail-verified open-sea rectangle, valid at exactly one level
    /// offset, with retail's median there. See the class remarks for how the
    /// rectangle was derived and why it is not shared across offsets.
    /// </summary>
    private readonly record struct WaterSample(
        int OffsetMs, int X0, int Y0, int X1, int Y1, int[] RetailMedian);

    private static readonly WaterSample[] WaterSamples =
    [
        new(2, 0, 200, 240, 260, [101, 128, 150]),
        new(256, 60, 160, 220, 250, [91, 108, 128]),
        new(499, 0, 160, 160, 280, [91, 111, 130]),
        new(749, 0, 170, 120, 290, [82, 96, 114]),
        new(1006, 30, 210, 150, 270, [76, 86, 103]),
        new(1255, 0, 240, 60, 300, [79, 94, 114]),
    ];

    /// <summary>
    /// Retail has zero pixels at or above 250 on all three channels in any of
    /// these rectangles, at any of these offsets, across both opening-pan runs
    /// (87,600 px). No allowance: a single one means a stage saturated. The
    /// 2026-07-25 defect produced 3,394.
    /// </summary>
    private const int MaxWhitePixels = 0;

    /// <summary>
    /// Per-channel 99th-percentile ceiling, per offset. Retail's worst reading
    /// over both runs is (165,188,212); the margin is a flat +25 on each
    /// channel, about 13%, which is 25x retail's own 1-unit run-to-run spread.
    /// The defective build read (255,255,255) at five of the six offsets.
    /// </summary>
    private static readonly int[] P99Ceiling = [190, 213, 237];

    /// <summary>
    /// Retail's water hue rule, per offset. Retail's weakest reading over these
    /// samples is 99.06%, so 0.90 leaves 9.06 points of margin below retail.
    /// The 2026-07-25 defect scored 51.26-67.86% - it fails at every offset,
    /// its best still 22.1 points under the floor.
    /// </summary>
    private const double MinBlueOverGreenOverRedFraction = 0.90;

    /// <summary>
    /// Per-channel median tolerance against retail. This is the tooth that
    /// catches a water that is merely the WRONG COLOUR rather than saturating:
    /// the ceiling, white count and hue rule are all one-sided. Retail's own
    /// run-to-run median spread is 1 unit; 24 is 24x that.
    /// </summary>
    private const int MedianTolerance = 24;

    /// <summary>
    /// xUnit v2 cannot dynamically skip after a test starts. Resolve the
    /// optional local capture during discovery so a missing or stale capture
    /// is visible as skipped, never as a passing rendered-pixel assertion.
    /// </summary>
    private sealed class CurrentWaterCaptureFactAttribute : FactAttribute
    {
        public CurrentWaterCaptureFactAttribute()
        {
            string? explicitCapture =
                Environment.GetEnvironmentVariable("ONSLAUGHT_WATER_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(explicitCapture) &&
                ResolveCaptureDirectory() is null)
            {
                Skip = "No current production Level 100 water capture is available to score.";
            }
        }
    }

    [CurrentWaterCaptureFact]
    public void CapturedWaterStaysInsideTheRetailEnvelope()
    {
        string captureDirectory = ResolveCaptureDirectory() ??
            throw new InvalidOperationException(
                "ONSLAUGHT_WATER_CAPTURE_DIR does not contain the required " +
                "gameplay frames, or the selected capture disappeared after discovery.");

        var failures = new List<string>();
        foreach (WaterSample sample in WaterSamples)
        {
            string file = Path.Combine(
                captureDirectory,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "level100-t{0:D6}ms.png",
                    sample.OffsetMs));
            Assert.True(File.Exists(file), $"Capture is missing {file}.");

            List<(int R, int G, int B)> pixels = ReadBox(file, sample);
            Assert.NotEmpty(pixels);
            string where = $"t0+{sample.OffsetMs}ms " +
                $"({sample.X0},{sample.Y0})-({sample.X1},{sample.Y1})";

            int white = pixels.Count(p => p.R >= 250 && p.G >= 250 && p.B >= 250);
            if (white > MaxWhitePixels)
            {
                failures.Add(
                    $"{where}: {white} of {pixels.Count} px are >= 250 on all " +
                    $"channels; retail has 0. A water stage is saturating.");
            }

            int[] p99 =
            [
                Percentile(pixels.Select(p => p.R), 99),
                Percentile(pixels.Select(p => p.G), 99),
                Percentile(pixels.Select(p => p.B), 99),
            ];
            for (int channel = 0; channel < 3; channel++)
            {
                if (p99[channel] > P99Ceiling[channel])
                {
                    failures.Add(
                        $"{where}: p99 channel {channel} is {p99[channel]}, over " +
                        $"the retail ceiling {P99Ceiling[channel]}.");
                }
            }

            int[] median =
            [
                Percentile(pixels.Select(p => p.R), 50),
                Percentile(pixels.Select(p => p.G), 50),
                Percentile(pixels.Select(p => p.B), 50),
            ];
            for (int channel = 0; channel < 3; channel++)
            {
                int delta = Math.Abs(median[channel] - sample.RetailMedian[channel]);
                if (delta > MedianTolerance)
                {
                    failures.Add(
                        $"{where}: median channel {channel} is {median[channel]}, " +
                        $"{delta} off retail's {sample.RetailMedian[channel]} " +
                        $"(tolerance {MedianTolerance}).");
                }
            }

            double blueRule =
                pixels.Count(p => p.B > p.G && p.G > p.R) / (double)pixels.Count;
            if (blueRule < MinBlueOverGreenOverRedFraction)
            {
                failures.Add(
                    $"{where}: only {blueRule:P2} of px satisfy B > G > R; retail " +
                    $"reads 99.06-99.69% here. A hue-bearing stage is " +
                    $"over-contributing, or the sample is no longer on water.");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Water capture '{captureDirectory}' left the retail envelope:{Environment.NewLine}" +
            string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Always-on backstop for the two specific compositions that produced the
    /// 2026-07-25 overbright water. This asserts on source text, which is not
    /// evidence that the product is correct - only
    /// <see cref="CapturedWaterStaysInsideTheRetailEnvelope"/> is. It exists so
    /// the exact defects that were measured cannot silently return on a machine
    /// with no capture.
    /// </summary>
    [Fact]
    public void ShorelineCompositionKeepsTheOperandOrderTheEnvelopeDependsOn()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "godot-water-source",
            "Level100WaterAsset.cs");
        Assert.True(File.Exists(path), $"Water source was not copied to the test output: {path}");
        // Comment lines are dropped: the file documents the removed passes by
        // name, and a note about a defect must not read as the defect.
        string source = string.Join(
            '\n',
            File.ReadLines(path).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // D3DTOP_MULTIPLYADD is result = Arg1 * Arg2 + Arg0, i.e.
        // waves * diffuse + current. Adding the wave term outside the modulate
        // applies the only hue-bearing water texture at full strength
        // regardless of the shoreline vertex-colour ramp.
        Assert.Contains("(wave.rgb * COLOR.rgb) + base_water", source, StringComparison.Ordinal);
        Assert.DoesNotContain("wave.rgb + (COLOR.rgb * base_water)", source, StringComparison.Ordinal);

        // No additive water pass. No decoded D3DRS_SRCBLEND / DESTBLEND /
        // ALPHABLENDENABLE write exists anywhere in reverse-engineering/ for
        // the water, and an additive draw of the shoreline mesh doubled the
        // wave contribution.
        Assert.DoesNotContain("blend_add", source, StringComparison.Ordinal);

        // RenderMainPass now settles the bounded sun-glint subpass that the
        // earlier uncited full-slab attempt was missing: two transformed
        // reflection samples plus the blob, SELECT/ADD/ADD alpha, 0xC0 test,
        // one #E8E8FF quad, camera-height-relative 6/2/8 geometry, and bias 6.
        Assert.Equal(
            2,
            source.Split(
                "texture(sun_reflection_texture",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("texture(sun_blob_texture, UV)", source, StringComparison.Ordinal);
        Assert.Contains("varying vec2 sun_reflection_coordinates;", source, StringComparison.Ordinal);
        Assert.Contains("(sun_reflection_coordinates.x * 0.1)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("(UV.x * 0.1)", source, StringComparison.Ordinal);
        Assert.Contains("reflection_a.a + reflection_b.a + blob.a", source, StringComparison.Ordinal);
        Assert.Contains("glint_alpha < (192.0 / 255.0)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("glint_alpha <=", source, StringComparison.Ordinal);
        Assert.Contains("vec3(232.0 / 255.0, 232.0 / 255.0, 1.0)", source, StringComparison.Ordinal);
        Assert.Contains("private const float SunGlintCenterHeightScale = 6f;", source, StringComparison.Ordinal);
        Assert.Contains("private const float SunGlintHalfWidthHeightScale = 2f;", source, StringComparison.Ordinal);
        Assert.Contains("private const float SunGlintHalfLengthHeightScale = 8f;", source, StringComparison.Ordinal);
        Assert.Contains("private const int SunGlintDepthBiasIndex = 6;", source, StringComparison.Ordinal);
        Assert.Contains("SunGlintDepthBiasIndex * RetailDepthBiasScale", source, StringComparison.Ordinal);
        Assert.Contains(
            "_sunGlintMaterial.SetShaderParameter(\"glint_phase\", _causticPhase)",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SunGlintPhaseRadiansPerSecond", source, StringComparison.Ordinal);
        Assert.DoesNotContain("cameraHeight > 0f", source, StringComparison.Ordinal);

        // CDXSurf requests projection depth-bias index 4 while drawing the
        // authored shoreline. Retail's cardid value is 0.00014, and
        // CDXEngine applies index * scale to projection slot 14. Keep that in
        // clip space; the former 0.002 world-space lift was a different law.
        Assert.Contains("private const float RetailDepthBiasScale = 0.00014f;", source, StringComparison.Ordinal);
        Assert.Contains("private const int ShorelineDepthBiasIndex = 4;", source, StringComparison.Ordinal);
        Assert.Contains("uniform float projection_depth_bias;", source, StringComparison.Ordinal);
        Assert.Contains("POSITION.z += projection_depth_bias * POSITION.w;", source, StringComparison.Ordinal);
        Assert.Contains("ShorelineDepthBiasIndex * RetailDepthBiasScale", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Position = Vector3.Up *", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Picks the capture to score.
    ///
    /// <para><c>ONSLAUGHT_WATER_CAPTURE_DIR</c> always wins: naming a directory
    /// is the operator taking responsibility for what is in it, and it is how
    /// this gate is validated against a known-bad capture.</para>
    ///
    /// <para>Otherwise the newest capture under
    /// <c>local-lab/godot-captures/</c> is taken, subject to two filters. It
    /// must be at least as new as <c>Level100WaterAsset.cs</c> - a capture that
    /// predates the current water source describes a build that no longer
    /// exists, so it is ignored rather than judged. And its manifest must
    /// declare <c>"capturePurpose": "production"</c>.</para>
    ///
    /// <para>The purpose filter exists because the unfiltered version of this
    /// method was wrong twice on 2026-07-26. It scored
    /// <c>probe-macro-only</c>, taken with the terrain shader cut to
    /// <c>ALBEDO = macro_color</c>, and it scored captures from a camera FOV
    /// sweep - deliberately modified builds, judged as if they were the
    /// product, purely because they were the newest directory on disk.
    /// <c>first_flight.py capture --purpose production</c> writes the marker, and
    /// refuses to write it when <c>rebuild/OnslaughtRebuild.Godot</c> has
    /// uncommitted changes. An unmarked capture is never auto-scored: the
    /// failure mode was an unlabelled experiment being mistaken for evidence,
    /// so unlabelled has to mean ignored.</para>
    /// </summary>
    private static string? ResolveCaptureDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable("ONSLAUGHT_WATER_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return HasGameplayFrames(configured) ? configured : null;
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

        string waterSource = Path.Combine(
            repoRoot, "rebuild", "OnslaughtRebuild.Godot", "Level100WaterAsset.cs");
        DateTime floor = File.Exists(waterSource)
            ? File.GetLastWriteTimeUtc(waterSource)
            : DateTime.MinValue;

        return Directory.EnumerateDirectories(captureRoot)
            .Where(HasGameplayFrames)
            .Where(IsProductionCapture)
            .Where(directory => File.GetLastWriteTimeUtc(
                Path.Combine(directory, "level100-t000002ms.png")) >= floor)
            .OrderByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// True only when the capture manifest declares itself a production
    /// capture. Missing manifest, missing marker and unreadable manifest all
    /// mean "not production" - this fails closed on purpose.
    /// </summary>
    private static bool IsProductionCapture(string directory)
    {
        string manifest = Path.Combine(directory, "capture-manifest.json");
        if (!File.Exists(manifest))
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
            return document.RootElement.TryGetProperty("capturePurpose", out JsonElement purpose) &&
                purpose.ValueKind == JsonValueKind.String &&
                string.Equals(purpose.GetString(), "production", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasGameplayFrames(string directory) =>
        File.Exists(Path.Combine(directory, "level100-t000002ms.png"));

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

    private static int Percentile(IEnumerable<int> values, int percentile)
    {
        int[] sorted = [.. values.Order()];
        int index = (int)Math.Round(
            (percentile / 100.0) * (sorted.Length - 1),
            MidpointRounding.AwayFromZero);
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static List<(int R, int G, int B)> ReadBox(string pngPath, WaterSample box)
    {
        (int width, int height, byte[] rgba) = DecodePng(File.ReadAllBytes(pngPath));
        Assert.True(
            box.X1 <= width && box.Y1 <= height,
            $"{pngPath} is {width}x{height}, too small for the t0+{box.OffsetMs}ms sample.");
        var pixels = new List<(int R, int G, int B)>((box.X1 - box.X0) * (box.Y1 - box.Y0));
        for (int y = box.Y0; y < box.Y1; y++)
        {
            int row = y * width * 4;
            for (int x = box.X0; x < box.X1; x++)
            {
                int offset = row + (x * 4);
                pixels.Add((rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        return pixels;
    }

    /// <summary>
    /// Minimal decoder for the exact PNG subset Godot's <c>Image.save_png</c>
    /// emits for a viewport readback: 8-bit, colour type 6 (RGBA), no
    /// interlacing. Anything else throws rather than being silently
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

    private static int ReadBigEndianInt32(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
}
