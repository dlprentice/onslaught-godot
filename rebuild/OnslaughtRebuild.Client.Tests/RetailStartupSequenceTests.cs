// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Cover for retail's cold-start media: the Lost Toys logo movie, the opening
/// montage and the splash card.
///
/// These tests exist because the 13 pinned startup screenshots CANNOT cover
/// this path. Every retail reference frame this project compares against was
/// captured with <c>-skipfmv</c>, so the capture rig suppresses the intro for
/// the same reason retail's own flag does — which leaves the intro with no
/// automated visual gate at all. That is exactly how <c>_feBackFrames</c> came
/// to be loaded and never drawn. The deterministic half of the lane is
/// therefore kept free of Godot types and tested here directly.
/// </summary>
public sealed class RetailStartupSequenceTests
{
    /// <summary>LTLogo.vid, from its Bink header: 480x300, 229 frames, 25 fps.</summary>
    private static readonly RetailStartupClip LostToysLogo = new(229, 25, 1, 480, 300);

    /// <summary>OpeningFMV.vid, from its Bink header: 480x300, 2054 frames, 25 fps.</summary>
    private static readonly RetailStartupClip OpeningMontage = new(2054, 25, 1, 480, 300);

    private static RetailStartupSchedule FullSchedule() =>
        new(
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.LostToysLogo] = LostToysLogo,
                [RetailStartupCue.OpeningMontage] = OpeningMontage,
            },
            splashPresent: true);

    [Fact]
    public void ClipDurationsMatchTheShippedBinkHeaders()
    {
        // ffprobe on the shipped files reports exactly these durations.
        Assert.Equal(9.16, LostToysLogo.DurationSeconds, 3);
        Assert.Equal(82.16, OpeningMontage.DurationSeconds, 3);
    }

    [Fact]
    public void SequenceIsLogoThenMontageThenSplash()
    {
        RetailStartupSchedule schedule = FullSchedule();

        // The order is the disassembled sequencer's: Play("ltlogo") then
        // Play("openingfmv"), with splash.tga released after the chain.
        Assert.Equal(RetailStartupCue.LostToysLogo, schedule.Sample(0.0).Cue);
        Assert.Equal(RetailStartupCue.LostToysLogo, schedule.Sample(9.0).Cue);

        Assert.Equal(RetailStartupCue.OpeningMontage, schedule.Sample(9.5).Cue);
        Assert.Equal(RetailStartupCue.OpeningMontage, schedule.Sample(88.0).Cue);

        Assert.Equal(RetailStartupFrameKind.Splash, schedule.Sample(92.0).Kind);
        Assert.Equal(RetailStartupFrameKind.Finished, schedule.Sample(200.0).Kind);
    }

    /// <summary>
    /// The clips are CONTIGUOUS. The ~1.9 s of black seen between them in two
    /// 2026-07-26 runs is Bink close/open latency on a cold file cache, not an
    /// authored pause: in the 2026-07-25 run the frame-matched clip starts are
    /// 0.987 s and 10.200 s against LTLogo's end at 10.147 s, a gap of +0.053 s.
    /// </summary>
    [Fact]
    public void ThereIsNoAuthoredGapBetweenTheClips()
    {
        Assert.Equal(0d, RetailStartupSchedule.InterClipBlackSeconds);

        RetailStartupSchedule schedule = FullSchedule();
        double boundary = LostToysLogo.DurationSeconds;
        Assert.Equal(RetailStartupCue.LostToysLogo, schedule.Sample(boundary - 1e-6).Cue);
        Assert.Equal(RetailStartupCue.OpeningMontage, schedule.Sample(boundary).Cue);
        Assert.Equal(0, schedule.Sample(boundary).FrameIndex);
    }

    [Fact]
    public void TotalLengthIsTheSumOfTheMeasuredBeats()
    {
        double expected =
            LostToysLogo.DurationSeconds +
            RetailStartupSchedule.InterClipBlackSeconds +
            OpeningMontage.DurationSeconds +
            RetailStartupSchedule.SplashFadeInSeconds +
            RetailStartupSchedule.SplashHoldSeconds;

        Assert.Equal(expected, FullSchedule().TotalSeconds, 6);
        Assert.Equal(95.82, FullSchedule().TotalSeconds, 2);
    }

    [Fact]
    public void TheGapOnlyExistsBetweenTwoPresentClips()
    {
        // Holding black between a clip and nothing would invent a beat retail
        // never shows on that path.
        var logoOnly = new RetailStartupSchedule(
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.LostToysLogo] = LostToysLogo,
            },
            splashPresent: false);

        Assert.Equal(LostToysLogo.DurationSeconds, logoOnly.TotalSeconds, 6);
        Assert.Contains(RetailStartupCue.OpeningMontage, logoOnly.MissingCues);
        Assert.Contains(RetailStartupCue.Splash, logoOnly.MissingCues);
    }

    [Fact]
    public void AnAbsentClipContributesNoTimeAndIsReported()
    {
        var none = new RetailStartupSchedule(
            new Dictionary<RetailStartupCue, RetailStartupClip>(),
            splashPresent: false);

        Assert.True(none.IsEmpty);
        Assert.Equal(0d, none.TotalSeconds);
        Assert.Equal(RetailStartupFrameKind.Finished, none.Sample(0d).Kind);
        Assert.Equal(3, none.MissingCues.Count);
    }

    [Fact]
    public void VideoFrameIndexIsFloorOfElapsedTimesRate()
    {
        RetailStartupSchedule schedule = FullSchedule();

        Assert.Equal(0, schedule.Sample(0.0).FrameIndex);
        Assert.Equal(0, schedule.Sample(0.039).FrameIndex);
        Assert.Equal(1, schedule.Sample(0.040).FrameIndex);
        Assert.Equal(25, schedule.Sample(1.0).FrameIndex);

        // The last sample inside the clip is its last frame, never one past it.
        Assert.Equal(228, schedule.Sample(LostToysLogo.DurationSeconds - 1e-6).FrameIndex);
    }

    [Fact]
    public void SamplingIsAPureFunctionOfTime()
    {
        // The capture clock advances by exactly 1/60 s per engine frame, so a
        // sequence sampled twice at the same tick must give the same frame.
        RetailStartupSchedule first = FullSchedule();
        RetailStartupSchedule second = FullSchedule();
        for (int tick = 0; tick < 6_000; tick++)
        {
            double time = tick / 60d;
            Assert.Equal(first.Sample(time), second.Sample(time));
        }
    }

    [Fact]
    public void SplashRampsToFullAndThenHolds()
    {
        RetailStartupSchedule schedule = FullSchedule();
        double splashStart =
            LostToysLogo.DurationSeconds +
            RetailStartupSchedule.InterClipBlackSeconds +
            OpeningMontage.DurationSeconds;

        Assert.Equal(0f, schedule.Sample(splashStart).Alpha, 3);
        Assert.Equal(0.5f, schedule.Sample(splashStart + 0.75).Alpha, 3);
        Assert.Equal(1f, schedule.Sample(splashStart + 1.5).Alpha, 3);
        Assert.Equal(1f, schedule.Sample(splashStart + 4.0).Alpha, 3);

        // Measured: the splash cuts to black, it does not fade out.
        Assert.Equal(RetailStartupFrameKind.Finished, schedule.Sample(splashStart + 4.6).Kind);
    }

    [Fact]
    public void TheNvidiaScreenIsNotInTheSequence()
    {
        // TWIMTBP_GefFX_640x480_Audio.vid ships and IS reachable: 0x83d404 is
        // the value field of a CVar named "TWIMTBP" constructed at 0x83d3f8
        // (0x004efae0), written object-relative by CVar::Init's
        // `mov [eax+0xc], ecx`, and the shipped cardid.txt sets
        // `Tweak:TWIMTBP 1` for Vendor:10DE Device:0330 GeForce FX 5900 Ultra.
        // It is omitted because no modern adapter matches that one entry and it
        // did not play on the measured hardware — the released PC path this
        // reconstruction targets does not include it.
        Assert.DoesNotContain(
            "TWIMTBP",
            string.Join(",", Enum.GetNames<RetailStartupCue>()),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MediaIndexRejectsAForeignSchema()
    {
        string root = NewCacheDirectory();
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            """{"schema":"something-else","clips":{}}""");

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.NotNull(index.Unavailable);
        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexReportsAMissingCacheRatherThanThrowing()
    {
        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(
            Path.Combine(Path.GetTempPath(), "onslaught-no-such-startup-media"),
            File.Exists);

        Assert.NotNull(index.Unavailable);
        Assert.False(index.HasSplash);
        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsAClipWhoseLastFrameIsMissing()
    {
        // A truncated decode must produce an ABSENT beat, not a short one. This
        // is the same defect class as the half-rate FEBack strip, where a
        // stable hash certified that a wrong recipe had run consistently.
        string root = NewCacheDirectory();
        Directory.CreateDirectory(Path.Combine(root, "lost-toys-logo"));
        File.WriteAllBytes(Path.Combine(root, "lost-toys-logo", "f00001.png"), [0]);
        WriteIndex(root, frameCount: 229);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsAClipWhoseMiddleFrameIsMissing()
    {
        string root = NewCacheDirectory();
        Directory.CreateDirectory(Path.Combine(root, "lost-toys-logo"));
        WritePngStructure(Path.Combine(root, "lost-toys-logo", "f00001.png"), 480, 300);
        WritePngStructure(Path.Combine(root, "lost-toys-logo", "f00003.png"), 480, 300);
        WriteIndex(root, frameCount: 3);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsACompleteSetOfNonPngFrames()
    {
        string root = NewCacheDirectory();
        Directory.CreateDirectory(Path.Combine(root, "lost-toys-logo"));
        for (int frame = 1; frame <= 3; frame++)
        {
            File.WriteAllText(
                Path.Combine(root, "lost-toys-logo", $"f{frame:D5}.png"),
                "not-a-png");
        }
        WriteIndex(root, frameCount: 3);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsAClipWhoseMiddleFrameChangedAfterMaterialization()
    {
        string root = NewCacheDirectory();
        string folder = Path.Combine(root, "lost-toys-logo");
        Directory.CreateDirectory(folder);
        for (int frame = 1; frame <= 3; frame++)
        {
            WritePngStructure(Path.Combine(folder, $"f{frame:D5}.png"), 480, 300);
        }
        WriteIndex(root, frameCount: 3);
        File.WriteAllText(Path.Combine(folder, "f00002.png"), "corrupt-middle-frame");

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsHeaderOnlyPngFrames()
    {
        string root = NewCacheDirectory();
        Directory.CreateDirectory(Path.Combine(root, "lost-toys-logo"));
        for (int frame = 1; frame <= 3; frame++)
        {
            WritePngHeaderOnly(
                Path.Combine(root, "lost-toys-logo", $"f{frame:D5}.png"),
                480,
                300);
        }
        WriteIndex(root, frameCount: 3);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexDropsMalformedClipButKeepsReading()
    {
        string root = NewCacheDirectory();
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            "{\"schema\":\"" + RetailStartupMediaIndex.Schema +
            "\",\"clips\":{\"LostToysLogo\":{\"frameCount\":\"bad\"}}}");

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.NotNull(index.Unavailable);
        Assert.Empty(index.Clips);
    }

    [Fact]
    public void MediaIndexAcceptsOnlyTheReceiptedSplashBytes()
    {
        string root = NewCacheDirectory();
        string splash = Path.Combine(root, "splash.png");
        WritePngStructure(splash, 512, 512);
        WriteSplashIndex(root, splash);

        RetailStartupMediaIndex exact =
            RetailStartupMediaIndex.Load(root, File.Exists);
        Assert.True(exact.HasSplash);

        WriteDifferentPngStructure(splash, 512, 512);
        RetailStartupMediaIndex changed =
            RetailStartupMediaIndex.Load(root, File.Exists);
        Assert.False(changed.HasSplash);
        Assert.NotNull(changed.Unavailable);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"schema":"onslaught-startup-media.v4","clips":[]}""")]
    [InlineData("""{"schema":"onslaught-startup-media.v4","clips":{"LostToysLogo":{}}}""")]
    public void MediaIndexTreatsWrongJsonShapesAsUnavailableRatherThanThrowing(string json)
    {
        string root = NewCacheDirectory();
        File.WriteAllText(Path.Combine(root, "startup-media.json"), json);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.NotNull(index.Unavailable);
        Assert.Empty(index.Clips);
        Assert.False(index.HasSplash);
    }

    [Fact]
    public void MediaIndexRejectsLegacyUnreceiptedSplashCaches()
    {
        string root = NewCacheDirectory();
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            """{"schema":"onslaught-startup-media.v2","clips":{},"stills":{}}""");

        RetailStartupMediaIndex index =
            RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.NotNull(index.Unavailable);
        Assert.False(index.HasSplash);
    }

    [Fact]
    public void MediaIndexFramePathsAreOneBasedOnDisk()
    {
        string root = NewCacheDirectory();
        Directory.CreateDirectory(Path.Combine(root, "lost-toys-logo"));
        WritePngStructure(Path.Combine(root, "lost-toys-logo", "f00001.png"), 480, 300);
        WritePngStructure(Path.Combine(root, "lost-toys-logo", "f00002.png"), 480, 300);
        WritePngStructure(Path.Combine(root, "lost-toys-logo", "f00003.png"), 480, 300);
        WriteIndex(root, frameCount: 3);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        // ffmpeg writes f00001.png first, so frame 0 is f00001.png. Renumbering
        // here would put a silent off-by-one between the cache and any frame a
        // human inspects in an image viewer.
        Assert.Equal(
            Path.Combine("lost-toys-logo", "f00001.png").Replace('\\', '/'),
            index.FrameRelativePath(RetailStartupCue.LostToysLogo, 0).Replace('\\', '/'));
        Assert.Equal(
            Path.Combine("lost-toys-logo", "f00003.png").Replace('\\', '/'),
            index.FrameRelativePath(RetailStartupCue.LostToysLogo, 2).Replace('\\', '/'));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => index.FrameRelativePath(RetailStartupCue.LostToysLogo, 3));
    }

    /// <summary>
    /// The materialize step must not pass any rate argument to the startup-media
    /// decode. <c>-r</c> or an <c>fps=</c> filter silently drops frames while
    /// preserving total duration, which is exactly what produced the half-rate
    /// FEBack strip (572 frames at 30 fps decoded as 286 at 15).
    /// </summary>
    [Fact]
    public void StartupMediaDecodePassesNoRateArgument()
    {
        string script = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "materialize_retail_assets.py"));

        int start = script.IndexOf("def _materialize_startup_media", StringComparison.Ordinal);
        Assert.True(start >= 0, "materialize_retail_assets.py has no _materialize_startup_media.");
        int end = script.IndexOf("\ndef ", start + 1, StringComparison.Ordinal);
        string body = end < 0 ? script[start..] : script[start..end];

        Assert.Contains("\"-pix_fmt\", \"rgb24\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"-r\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("fps=", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"-vf\"", body, StringComparison.Ordinal);

        // And the shipped-but-unreachable nVidia clip must not be materialized.
        Assert.DoesNotContain("TWIMTBP_GefFX", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The startup media cache must not be written under <c>res://</c>. A
    /// <c>.gitignore</c> entry stops a git commit but does not stop a Godot
    /// export from packing an ignored file into the PCK.
    /// </summary>
    [Fact]
    public void StartupMediaCacheDefaultsOutsideTheGodotProject()
    {
        string script = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "materialize_retail_assets.py"));

        int start = script.IndexOf("def _default_startup_media_root", StringComparison.Ordinal);
        Assert.True(start >= 0, "materialize_retail_assets.py has no _default_startup_media_root.");
        int end = script.IndexOf("\ndef ", start + 1, StringComparison.Ordinal);
        string body = end < 0 ? script[start..] : script[start..end];

        Assert.DoesNotContain("GODOT_ASSETS", body, StringComparison.Ordinal);
        Assert.DoesNotContain("OnslaughtRebuild.Godot", body, StringComparison.Ordinal);
    }

    private static string NewCacheDirectory()
    {
        string root = Path.Combine(
            Path.GetTempPath(), "onslaught-startup-media-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteIndex(string root, int frameCount)
    {
        string folder = Path.Combine(root, "lost-toys-logo");
        string[] existingFrames = Enumerable.Range(1, frameCount)
            .Select(frame => Path.Combine(folder, $"f{frame:D5}.png"))
            .Where(File.Exists)
            .ToArray();
        var index = new
        {
            schema = RetailStartupMediaIndex.Schema,
            clips = new Dictionary<string, object>
            {
                [nameof(RetailStartupCue.LostToysLogo)] = new
                {
                    width = 480,
                    height = 300,
                    fpsNumerator = 25,
                    fpsDenominator = 1,
                    frameCount,
                    framePathFormat = "lost-toys-logo/f{0:D5}.png",
                    framesSha256 = ComputeFrameSetSha256(existingFrames),
                },
            },
        };
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            JsonSerializer.Serialize(index));
    }

    private static void WriteSplashIndex(string root, string splash)
    {
        var index = new
        {
            schema = RetailStartupMediaIndex.Schema,
            clips = new Dictionary<string, object>(),
            stills = new Dictionary<string, object>
            {
                [nameof(RetailStartupCue.Splash)] = new
                {
                    path = Path.GetFileName(splash),
                    outputSha256 = Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            File.ReadAllBytes(splash))).ToLowerInvariant(),
                },
            },
        };
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            JsonSerializer.Serialize(index));
    }

    private static string ComputeFrameSetSha256(IReadOnlyList<string> paths)
    {
        using System.Security.Cryptography.IncrementalHash digest =
            System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256);
        digest.AppendData("onslaught-startup-frame-set.v1\0"u8);
        foreach (string path in paths)
        {
            digest.AppendData(System.Text.Encoding.ASCII.GetBytes(Path.GetFileName(path)));
            digest.AppendData([0]);
            long length = new FileInfo(path).Length;
            digest.AppendData(System.Text.Encoding.ASCII.GetBytes(
                length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            digest.AppendData([0]);
            digest.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    private static void WritePngStructure(string path, int width, int height)
    {
        byte[] header = PngHeader(width, height);
        var bytes = new List<byte>(header);
        byte[] pixels = new byte[(width * 3 + 1) * height];
        using var compressed = new MemoryStream();
        using (var stream = new System.IO.Compression.ZLibStream(
            compressed,
            System.IO.Compression.CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            stream.Write(pixels);
        }
        AppendChunk(bytes, "IDAT"u8, compressed.ToArray());
        AppendChunk(bytes, "IEND"u8, []);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static void WriteDifferentPngStructure(string path, int width, int height)
    {
        byte[] header = PngHeader(width, height);
        var bytes = new List<byte>(header);
        byte[] pixels = new byte[(width * 3 + 1) * height];
        pixels[^1] = 1;
        using var compressed = new MemoryStream();
        using (var stream = new System.IO.Compression.ZLibStream(
            compressed,
            System.IO.Compression.CompressionLevel.SmallestSize,
            leaveOpen: true))
        {
            stream.Write(pixels);
        }
        AppendChunk(bytes, "IDAT"u8, compressed.ToArray());
        AppendChunk(bytes, "IEND"u8, []);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static void WritePngHeaderOnly(string path, int width, int height) =>
        File.WriteAllBytes(path, PngHeader(width, height));

    private static byte[] PngHeader(int width, int height)
    {
        List<byte> bytes =
        [
            0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A,
        ];
        byte[] payload =
        [
            (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
            (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
            8, 2, 0, 0, 0,
        ];
        AppendChunk(bytes, "IHDR"u8, payload);
        return [.. bytes];
    }

    private static void AppendChunk(
        List<byte> destination,
        ReadOnlySpan<byte> kind,
        ReadOnlySpan<byte> payload)
    {
        destination.Add((byte)(payload.Length >> 24));
        destination.Add((byte)(payload.Length >> 16));
        destination.Add((byte)(payload.Length >> 8));
        destination.Add((byte)payload.Length);
        destination.AddRange(kind.ToArray());
        destination.AddRange(payload.ToArray());
        uint crc = PngCrc(kind, payload);
        destination.Add((byte)(crc >> 24));
        destination.Add((byte)(crc >> 16));
        destination.Add((byte)(crc >> 8));
        destination.Add((byte)crc);
    }

    private static uint PngCrc(ReadOnlySpan<byte> kind, ReadOnlySpan<byte> payload)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in kind)
        {
            crc = UpdatePngCrc(crc, value);
        }
        foreach (byte value in payload)
        {
            crc = UpdatePngCrc(crc, value);
        }
        return ~crc;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0
                ? 0xEDB88320u ^ (crc >> 1)
                : crc >> 1;
        }
        return crc;
    }
}
