// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The recovered laws of retail's FMV presentation, and the state routing that
/// puts Level 100's intro cutscene where retail puts it.
///
/// <para>These assert LAWS read out of a measurement, not our own arithmetic.
/// The presentation numbers come from a passive D3D9 capture of the released
/// build (<c><maintainer evidence>\bea-d3d9-capture\d3d9-20260728-111552.log</c>, 900 frames /
/// 896 draws, refusals 0, warnings 0, specimen verified unchanged afterwards),
/// written up in <c>local-lab/FMV-PRESENTATION-2026-07-28.md</c>. The routing
/// comes from the pinned source and from the campaign FMV table read out of the
/// pristine specimen.</para>
///
/// <para>They exist because no visual gate can cover this path: every retail
/// reference frame this project compares against was captured with
/// <c>-skipfmv</c>, and the capture rig suppresses the movie for the same
/// reason. A silently broken FMV would otherwise be invisible.</para>
/// </summary>
public sealed class RetailLevel100CutsceneTests
{
    /// <summary>
    /// <c>data/video/cutscenes/01.vid</c>, from its Bink header via ffprobe on
    /// <c>local-lab/safe-copy-bea-pristine</c>: 480x300, 25 fps, 123.80 s.
    /// </summary>
    private static readonly RetailStartupClip Level100Intro = new(3095, 25, 1, 480, 300);

    [Fact]
    public void TheQuadIsTheMeasuredLetterboxedRectangle()
    {
        // The four logged xyzrhw positions: (0,40) (640,40) (640,440) (0,440).
        Assert.Equal(0f, RetailFmvPresentation.QuadLeft);
        Assert.Equal(40f, RetailFmvPresentation.QuadTop);
        Assert.Equal(640f, RetailFmvPresentation.QuadRight);
        Assert.Equal(440f, RetailFmvPresentation.QuadBottom);

        // Letterboxed, not full-screen: equal bars top and bottom of a 640x480
        // viewport, and 640x400 of it drawn.
        Assert.Equal(640f, RetailFmvPresentation.QuadWidth);
        Assert.Equal(400f, RetailFmvPresentation.QuadHeight);
        Assert.Equal(
            RetailFmvPresentation.QuadTop,
            RetailFmvPresentation.StageHeight - RetailFmvPresentation.QuadBottom);
    }

    [Fact]
    public void TheUvPairFixesTheDecodeSizeAtFourEightyByThreeHundred()
    {
        // t0 max was logged as (0.9375, 0.5859) against a 512x512 texture.
        Assert.Equal(0.9375f, RetailFmvPresentation.MaxU, 6);
        Assert.Equal(0.5859f, RetailFmvPresentation.MaxV, 4);

        Assert.Equal(512, RetailFmvPresentation.TextureSize);
        Assert.Equal(
            480f, RetailFmvPresentation.MaxU * RetailFmvPresentation.TextureSize, 4);
        Assert.Equal(
            300f, RetailFmvPresentation.MaxV * RetailFmvPresentation.TextureSize, 4);

        // 0.5859 is the log's four-decimal rendering of 300/512 = 0.5859375,
        // not a separate number. Pinning both directions stops a future edit
        // from "tidying" the literal into something that no longer divides.
        Assert.Equal(0.5859375f, RetailFmvPresentation.MaxV, 7);
    }

    [Fact]
    public void NothingIsStretched()
    {
        // 480x300 and 640x400 are both exactly 1.6, so the upscale is uniform
        // 1.3333x and the letterbox is a consequence of the source aspect
        // rather than a separate authored border.
        float source =
            RetailFmvPresentation.SourceWidth / (float)RetailFmvPresentation.SourceHeight;
        float drawn = RetailFmvPresentation.QuadWidth / RetailFmvPresentation.QuadHeight;

        Assert.Equal(1.6f, source, 6);
        Assert.Equal(source, drawn, 6);
        Assert.Equal(
            RetailFmvPresentation.QuadWidth / RetailFmvPresentation.SourceWidth,
            RetailFmvPresentation.QuadHeight / RetailFmvPresentation.SourceHeight,
            6);
    }

    [Fact]
    public void EveryDecodedClipMatchesTheSourceSizeTheUvsImply()
    {
        // If a decode is ever retargeted, the UVs above stop describing it.
        Assert.Equal(RetailFmvPresentation.SourceWidth, Level100Intro.Width);
        Assert.Equal(RetailFmvPresentation.SourceHeight, Level100Intro.Height);
    }

    [Fact]
    public void FullBrightnessIsTheMeasuredDiffuseAndIsNotWhite()
    {
        // Every vertex carried diff=0xFFFEFEFE. With stage 0 MODULATE that is
        // what full brightness multiplies by; rounding it to 0xFF would discard
        // the only direct evidence of retail's fade mechanism.
        Assert.Equal(0xFE, RetailFmvPresentation.FullBrightnessChannel);
        Assert.NotEqual(0xFF, RetailFmvPresentation.FullBrightnessChannel);
    }

    [Fact]
    public void TheDecoderIsDoubleBufferedAndAlternatesStrictly()
    {
        // frame 2 -> 0x0F220EE0, frame 3 -> 0x0F2214C0, frame 4 -> 0x0F220EE0 …
        // with no exception across 896 draws.
        Assert.Equal(2, RetailFmvPresentation.BufferCount);

        var seen = new HashSet<int>();
        int previous = RetailFmvPresentation.BufferIndexForFrame(0);
        seen.Add(previous);
        for (int frame = 1; frame < 3095; frame++)
        {
            int buffer = RetailFmvPresentation.BufferIndexForFrame(frame);
            Assert.NotEqual(previous, buffer);
            seen.Add(buffer);
            previous = buffer;
        }

        // Alternation over exactly two buffers, and never a third.
        Assert.Equal(RetailFmvPresentation.BufferCount, seen.Count);
        Assert.All(seen, buffer => Assert.InRange(buffer, 0, RetailFmvPresentation.BufferCount - 1));
    }

    [Fact]
    public void TheCutsceneScheduleIsOneClipWithNothingAroundIt()
    {
        RetailStartupSchedule schedule = RetailStartupSchedule.ForSingleClip(
            RetailStartupCue.Level100IntroCutscene,
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.Level100IntroCutscene] = Level100Intro,
            });

        // ffprobe on cutscenes/01.vid reports exactly this duration.
        Assert.Equal(123.80, schedule.TotalSeconds, 2);
        Assert.Equal(123.80, Level100Intro.DurationSeconds, 2);
        Assert.Empty(schedule.MissingCues);

        // No splash, no inter-clip black, no other cue.
        Assert.Equal(RetailStartupCue.Level100IntroCutscene, schedule.Sample(0.0).Cue);
        Assert.Equal(RetailStartupFrameKind.Video, schedule.Sample(0.0).Kind);
        Assert.Equal(RetailStartupFrameKind.Video, schedule.Sample(123.0).Kind);
        Assert.Equal(RetailStartupFrameKind.Finished, schedule.Sample(123.80).Kind);
        Assert.Equal(3094, schedule.Sample(Level100Intro.DurationSeconds - 1e-6).FrameIndex);
    }

    [Fact]
    public void AnUndecodedCutsceneIsAbsentRatherThanPadded()
    {
        RetailStartupSchedule schedule = RetailStartupSchedule.ForSingleClip(
            RetailStartupCue.Level100IntroCutscene,
            new Dictionary<RetailStartupCue, RetailStartupClip>());

        Assert.True(schedule.IsEmpty);
        Assert.Equal(0d, schedule.TotalSeconds);
        Assert.Equal([RetailStartupCue.Level100IntroCutscene], schedule.MissingCues);
    }

    [Fact]
    public void TheCutsceneIsNotPartOfTheColdStartChain()
    {
        // RunIntroFMV is called from CGame::RestartLoopRunLevel, not from the
        // startup sequencer at 0x004efce3-0x004efee9. Handing the cue to the
        // cold-start schedule must therefore contribute nothing.
        var schedule = new RetailStartupSchedule(
            new Dictionary<RetailStartupCue, RetailStartupClip>
            {
                [RetailStartupCue.Level100IntroCutscene] = Level100Intro,
            },
            splashPresent: false);

        Assert.True(schedule.IsEmpty);
        Assert.Equal(0d, schedule.TotalSeconds);
    }

    [Fact]
    public void RetailsSkipFmvFlagSuppressesTheLevelCutsceneToo()
    {
        // CGame::GetIntroFMV returns -1 when CLIPARAMS.mSkipFMV is set
        // (references/Onslaught/game.cpp:1108-1109), so one flag suppresses
        // both the cold-start chain and the level cutscene.
        Assert.True(RetailStartupSchedule.IsSuppressedByArguments(["--skipfmv"]));
        Assert.False(RetailStartupSchedule.IsSuppressedByArguments([]));

        // --intro is ours and overrides every suppression, so a human or a
        // future rig can always force the movie on.
        Assert.False(RetailStartupSchedule.IsSuppressedByArguments(["--skipfmv", "--intro"]));

        // The frame-counted harnesses must never sit through 123.8 s of video.
        Assert.True(RetailStartupSchedule.IsSuppressedByArguments(["--smoke"]));
        Assert.True(RetailStartupSchedule.IsSuppressedByArguments(["--capture-dir=x"]));
        Assert.True(RetailStartupSchedule.IsSuppressedByArguments(["--capture-plan=x"]));
    }

    /// <summary>
    /// <c>CGame::GetIntroFMV</c> (<c>references/Onslaught/game.cpp:1103-1119</c>)
    /// is the one retail suppress owner. The reconstruction's owner is
    /// <see cref="RetailFrontendScenePath.IsStartupSuppressed"/>, which
    /// forwards to <see cref="RetailStartupSchedule.IsSuppressedByArguments"/>.
    /// Cold-start media must call that path rather than keep a second copy of
    /// the <c>--skipfmv</c> / <c>--smoke</c> / capture / <c>--intro</c> rule.
    /// </summary>
    [Fact]
    public void StartRetailStartupMediaUsesTheSharedSuppressOwner()
    {
        string game = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "FirstFlightGame.cs"));
        string method = ExtractMethod(game, "private void StartRetailStartupMedia()");

        Assert.Contains(
            "RetailFrontendScenePath.IsStartupSuppressed",
            method,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_skipStartupMedia || _smokeMode || _captureArgumentsPresent",
            method,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheCutsceneSitsBetweenLoadingAndGameplay()
    {
        RetailFrontendSession frontend = LoadedLevel100();

        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.True(frontend.Level100IntroCutscenePending);

        frontend.BeginLevel100IntroCutscene();
        Assert.Equal(RetailFrontendScreen.IntroCutscene, frontend.Screen);

        frontend.CompleteLevel100IntroCutscene();
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);
    }

    [Fact]
    public void SuppressingTheCutsceneStillConsumesTheFirstRound()
    {
        RetailFrontendSession frontend = LoadedLevel100();

        frontend.CompleteLevel100Load();

        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);
        Assert.False(frontend.Level100IntroCutscenePending);

        frontend.RestartLevel100();
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.Throws<InvalidOperationException>(frontend.BeginLevel100IntroCutscene);
    }

    [Fact]
    public void ARetryDoesNotReplayTheCutsceneButLeavingAndReenteringDoes()
    {
        // mFirstTimeRound: set TRUE when the level is entered
        // (references/Onslaught/game.cpp:1607) and FALSE at the bottom of each
        // restart-loop iteration (game.cpp:1691).
        RetailFrontendSession frontend = LoadedLevel100();
        frontend.BeginLevel100IntroCutscene();
        frontend.CompleteLevel100IntroCutscene();

        frontend.RestartLevel100();
        Assert.False(frontend.Level100IntroCutscenePending);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        Assert.Throws<InvalidOperationException>(frontend.BeginLevel100IntroCutscene);

        // The Retry still reaches gameplay; it simply does so without a movie.
        frontend.CompleteLevel100Load();
        Assert.Equal(RetailFrontendScreen.Gameplay, frontend.Screen);

        // Leaving to the frontend ends the restart loop, so the next entry is a
        // first time round again.
        frontend.LeaveLevel100ForMainMenu();
        Assert.True(frontend.Level100IntroCutscenePending);
    }

    [Fact]
    public void TheCutsceneCannotStartBeforeTheLevelHasLoaded()
    {
        var frontend = new RetailFrontendSession();
        Assert.Throws<InvalidOperationException>(frontend.BeginLevel100IntroCutscene);
        Assert.Throws<InvalidOperationException>(frontend.CompleteLevel100IntroCutscene);

        // Reaching Loading is not enough: the launch request has to be consumed
        // first, which is what says the world is actually being built.
        for (int page = 0; page < PagesFromClickToStartToLoading; page++)
        {
            frontend.Confirm();
        }

        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.Throws<InvalidOperationException>(frontend.BeginLevel100IntroCutscene);
    }

    /// <summary>
    /// The cutscene's voice track, end to end through the index that gates it.
    ///
    /// <para>Three things are asserted, and each of them is a way the movie
    /// could go silent or go wrong without anything else noticing:</para>
    /// <list type="number">
    /// <item>the receipted track loads and reports <b>Bink track 0</b> — English,
    /// read out of the pristine specimen; see
    /// <see cref="RetailStartupClipAudio"/> for the chain;</item>
    /// <item>its length COVERS the video and overruns it by less than one
    /// 2,048-sample binkaudio frame. Against the real numbers — 5,460,480 sample
    /// frames at 44.1 kHz versus 3,095 video frames at 25 fps — the overhang is
    /// 900 samples. A length mismatch is the cheapest possible falsification
    /// that this track belongs to this clip at all;</item>
    /// <item>a byte that changes after materialization drops the AUDIO and KEEPS
    /// the video, because a silent movie is the state this shipped in and
    /// substitutes nothing, whereas dropping the clip would cost 123.80 s of
    /// narrative over a bad 21.8 MB file.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheVoiceTrackIsBinkTrackZeroAndCoversTheWholeVideo()
    {
        const int SampleRate = 44_100;
        const long SampleFrames = 5_460_480L;

        string root = NewCacheDirectory();
        string relative = "level100-intro-cutscene/voice-track00.wav";
        string audioPath = Path.Combine(root, "level100-intro-cutscene", "voice-track00.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(audioPath)!);
        // A short stand-in for the 21.8 MB decode: the index checks the WAV
        // envelope and the receipt, and the declared sample-frame count is what
        // carries the length. Writing 21.8 MB here would test the disk.
        WritePcmWav(audioPath, SampleRate, channels: 2, sampleFrames: 64);
        WriteCutsceneIndex(root, relative, audioPath, SampleRate, 2, sampleFrames: 64);

        RetailStartupMediaIndex index = RetailStartupMediaIndex.Load(root, File.Exists);

        RetailStartupClipAudio voice =
            Assert.Contains(RetailStartupCue.Level100IntroCutscene, index.ClipAudio);
        Assert.Equal(0, voice.Track);
        Assert.Equal(SampleRate, voice.SampleRate);
        Assert.Equal(2, voice.Channels);
        Assert.Equal(
            relative,
            index.AudioRelativePath(RetailStartupCue.Level100IntroCutscene)
                .Replace('\\', '/'));

        // The length law, against the measured decode rather than the stand-in.
        var measured = new RetailStartupClipAudio(0, SampleRate, 2, 16, SampleFrames);
        long videoSampleFrames =
            (long)Level100Intro.FrameCount * SampleRate / Level100Intro.FramesPerSecondNumerator;
        long overhang = measured.SampleFrameCount - videoSampleFrames;
        Assert.Equal(5_459_580L, videoSampleFrames);
        Assert.Equal(900L, overhang);
        Assert.InRange(overhang, 0L, BinkAudioFrameSamples - 1);
        Assert.Equal(123.820408, measured.DurationSeconds, 6);
        Assert.True(
            measured.DurationSeconds >= Level100Intro.DurationSeconds,
            "the voice track must cover the whole movie, never end early");

        // A byte changes after materialization: audio out, video intact.
        byte[] tampered = File.ReadAllBytes(audioPath);
        tampered[^1] ^= 0x01;
        File.WriteAllBytes(audioPath, tampered);

        RetailStartupMediaIndex reloaded = RetailStartupMediaIndex.Load(root, File.Exists);

        Assert.Empty(reloaded.ClipAudio);
        Assert.Contains(RetailStartupCue.Level100IntroCutscene, reloaded.Clips);
        Assert.Throws<InvalidOperationException>(
            () => reloaded.AudioRelativePath(RetailStartupCue.Level100IntroCutscene));
    }

    /// <summary>
    /// binkaudio's frame length at 44.1 kHz: ffmpeg's decoder uses
    /// <c>frame_len_bits = 11</c> for rates at or above 44100
    /// (<c>libavcodec/binkaudio.c</c>), so the last frame overruns the video by
    /// whatever it takes to fill.
    /// </summary>
    private const long BinkAudioFrameSamples = 2048L;

    private static string NewCacheDirectory()
    {
        string root = Path.Combine(
            Path.GetTempPath(), "onslaught-cutscene-audio-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Writes the canonical 44-byte-header PCM WAV the materializer produces.</summary>
    private static void WritePcmWav(string path, int sampleRate, int channels, int sampleFrames)
    {
        int blockAlign = channels * 2;
        int dataSize = sampleFrames * blockAlign;
        var wave = new byte[44 + dataSize];
        Span<byte> header = wave.AsSpan(0, 44);
        "RIFF"u8.CopyTo(header);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            header[4..], (uint)(wave.Length - 8));
        "WAVEfmt "u8.CopyTo(header[8..]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16u);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            header[28..], (uint)(sampleRate * blockAlign));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)blockAlign);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)dataSize);
        for (int index = 0; index < dataSize; index++)
        {
            wave[44 + index] = (byte)(index * 7);
        }
        File.WriteAllBytes(path, wave);
    }

    private static void WriteCutsceneIndex(
        string root,
        string audioRelativePath,
        string audioPath,
        int sampleRate,
        int channels,
        int sampleFrames)
    {
        string folder = Path.Combine(root, "level100-intro-cutscene");
        Directory.CreateDirectory(folder);
        string[] frames = Enumerable.Range(1, 3)
            .Select(frame => Path.Combine(folder, $"f{frame:D5}.png"))
            .ToArray();
        foreach (string frame in frames)
        {
            WriteMinimalRgbPng(frame, Level100Intro.Width, Level100Intro.Height);
        }

        var index = new
        {
            schema = RetailStartupMediaIndex.Schema,
            clips = new Dictionary<string, object>
            {
                [nameof(RetailStartupCue.Level100IntroCutscene)] = new
                {
                    width = Level100Intro.Width,
                    height = Level100Intro.Height,
                    fpsNumerator = Level100Intro.FramesPerSecondNumerator,
                    fpsDenominator = Level100Intro.FramesPerSecondDenominator,
                    frameCount = frames.Length,
                    framePathFormat = "level100-intro-cutscene/f{0:D5}.png",
                    framesSha256 = FrameSetSha256(frames),
                    audio = new
                    {
                        track = 0,
                        path = audioRelativePath,
                        sampleRate,
                        channels,
                        bitsPerSample = 16,
                        sampleFrameCount = sampleFrames,
                        outputSha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                File.ReadAllBytes(audioPath))),
                    },
                },
            },
        };
        File.WriteAllText(
            Path.Combine(root, "startup-media.json"),
            System.Text.Json.JsonSerializer.Serialize(index));
    }

    private static string FrameSetSha256(IReadOnlyList<string> paths)
    {
        using System.Security.Cryptography.IncrementalHash digest =
            System.Security.Cryptography.IncrementalHash.CreateHash(
                System.Security.Cryptography.HashAlgorithmName.SHA256);
        digest.AppendData("onslaught-startup-frame-set.v1\0"u8);
        foreach (string path in paths)
        {
            digest.AppendData(System.Text.Encoding.ASCII.GetBytes(Path.GetFileName(path)));
            digest.AppendData([0]);
            digest.AppendData(System.Text.Encoding.ASCII.GetBytes(
                new FileInfo(path).Length.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
            digest.AppendData([0]);
            digest.AppendData(File.ReadAllBytes(path));
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }

    private static void WriteMinimalRgbPng(string path, int width, int height)
    {
        List<byte> bytes = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        AppendPngChunk(
            bytes,
            "IHDR"u8,
            [
                (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width,
                (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height,
                8, 2, 0, 0, 0,
            ]);
        using var compressed = new MemoryStream();
        using (var stream = new System.IO.Compression.ZLibStream(
            compressed, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(new byte[((width * 3) + 1) * height]);
        }
        AppendPngChunk(bytes, "IDAT"u8, compressed.ToArray());
        AppendPngChunk(bytes, "IEND"u8, []);
        File.WriteAllBytes(path, [.. bytes]);
    }

    private static void AppendPngChunk(
        List<byte> destination, ReadOnlySpan<byte> kind, ReadOnlySpan<byte> payload)
    {
        destination.Add((byte)(payload.Length >> 24));
        destination.Add((byte)(payload.Length >> 16));
        destination.Add((byte)(payload.Length >> 8));
        destination.Add((byte)payload.Length);
        destination.AddRange(kind.ToArray());
        destination.AddRange(payload.ToArray());
        uint crc = uint.MaxValue;
        foreach (byte value in kind)
        {
            crc = UpdatePngCrc(crc, value);
        }
        foreach (byte value in payload)
        {
            crc = UpdatePngCrc(crc, value);
        }
        crc = ~crc;
        destination.Add((byte)(crc >> 24));
        destination.Add((byte)(crc >> 16));
        destination.Add((byte)(crc >> 8));
        destination.Add((byte)crc);
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }

    /// <summary>
    /// Click-to-start, FEP_MAIN (New Game), CHOOSE GAME NAME, SELECT LEVEL,
    /// MISSION BRIEFING, SELECT CONFIGURATION — six confirmations reach LOADING.
    /// </summary>
    private const int PagesFromClickToStartToLoading = 6;

    /// <summary>
    /// Drives the released page chain to the point retail runs the intro FMV:
    /// click-to-start, main menu, CHOOSE GAME NAME, SELECT LEVEL, MISSION
    /// BRIEFING, SELECT CONFIGURATION, LOADING, world built.
    /// </summary>
    private static RetailFrontendSession LoadedLevel100()
    {
        var frontend = new RetailFrontendSession();
        for (int page = 0; page < PagesFromClickToStartToLoading; page++)
        {
            frontend.Confirm();
        }

        Assert.Equal(RetailFrontendScreen.Loading, frontend.Screen);
        Assert.True(frontend.ConsumeLevel100LaunchRequest());
        return frontend;
    }

    private static string ExtractMethod(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Missing method signature: {signature}");
        int openingBrace = source.IndexOf('{', signatureIndex);
        Assert.True(openingBrace >= 0, $"Missing method body: {signature}");

        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return source[(openingBrace + 1)..index];
            }
        }

        throw new InvalidOperationException($"Unterminated method body: {signature}");
    }
}
