// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The index of decoded startup media, read from a cache directory that lives
/// OUTSIDE <c>res://</c>.
///
/// <para><b>Why outside res://.</b></para>
/// These files are decodes of the user's own retail installation. They are
/// already gitignored, but a <c>.gitignore</c> entry does not stop a Godot
/// export from packing an ignored file into the PCK — the export scans the
/// project directory, not the git index. Keeping the cache outside the project
/// tree makes packing them structurally impossible rather than merely
/// discouraged. (The pre-existing <c>Assets/Frontend/Backgrounds/
/// fe-back-128x128x15.rgb</c> DOES sit inside <c>res://</c> and is exposed to
/// exactly that hazard; this lane does not extend the problem.)
///
/// <para><b>Frames are per-file, never one big strip.</b></para>
/// The decode is one lossless PNG per video frame. Measured on this machine:
/// LTLogo 229 frames = 24 MB, OpeningFMV 2054 frames = 244 MB, against 98.9 MB
/// and 887 MB for raw RGB24 strips of the same footage. Because the frames are
/// separately addressable, the player holds exactly ONE frame resident and
/// updates a single texture in place. The existing FEBack loader — which reads
/// a whole strip and builds one <c>Texture2D</c> per frame — must not be
/// applied at this scale.
///
/// <para><b>Audio is one lossless PCM file per clip, not per frame.</b></para>
/// A clip may also carry one decoded Bink audio track — see
/// <see cref="RetailStartupClipAudio"/> for which track and why. It is canonical
/// 44-byte-header PCM WAV for the same reason the frames are PNG: it IS the Bink
/// decode, so it can be its own parity oracle. At 21.8 MB for the 123.80 s
/// cutscene it is 5 % of that clip's frame data.
/// </summary>
public sealed class RetailStartupMediaIndex
{
    public const string Schema = "onslaught-startup-media.v4";
    private static ReadOnlySpan<byte> FrameSetDomain =>
        "onslaught-startup-frame-set.v1\0"u8;

    private readonly Dictionary<RetailStartupCue, RetailStartupClip> _clips = [];
    private readonly Dictionary<RetailStartupCue, string> _frameFormats = [];
    private readonly Dictionary<RetailStartupCue, RetailStartupClipAudio> _clipAudio = [];
    private readonly Dictionary<RetailStartupCue, string> _audioPaths = [];

    private RetailStartupMediaIndex()
    {
    }

    /// <summary>Directory the index was read from; frame paths are relative to it.</summary>
    public string Root { get; private init; } = string.Empty;

    /// <summary>Decoded video clips, keyed by cue.</summary>
    public IReadOnlyDictionary<RetailStartupCue, RetailStartupClip> Clips => _clips;

    /// <summary>
    /// Decoded audio tracks, keyed by cue. A cue in <see cref="Clips"/> but not
    /// here has no decoded audio and plays SILENT — which is the pre-existing
    /// behaviour and is never a defect on its own.
    /// </summary>
    public IReadOnlyDictionary<RetailStartupCue, RetailStartupClipAudio> ClipAudio =>
        _clipAudio;

    /// <summary>Relative path of the splash still, or null if it was not materialized.</summary>
    public string? SplashRelativePath { get; private init; }

    /// <summary>Why the index is absent or unusable, or null when it loaded.</summary>
    public string? Unavailable { get; private init; }

    public bool HasSplash => SplashRelativePath is not null;

    /// <summary>
    /// Relative path of one frame of a clip. Throws if the cue has no clip, so a
    /// caller cannot silently address media that was never decoded.
    /// </summary>
    public string FrameRelativePath(RetailStartupCue cue, int frameIndex)
    {
        if (!_frameFormats.TryGetValue(cue, out string? format))
        {
            throw new InvalidOperationException(
                $"Startup media has no decoded clip for {cue}.");
        }

        if (!_clips.TryGetValue(cue, out RetailStartupClip clip) ||
            frameIndex < 0 || frameIndex >= clip.FrameCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(frameIndex),
                frameIndex,
                $"Frame index outside {cue}'s decoded range.");
        }

        // The format is "<dir>/f{0:D5}.png"; the index is 1-based on disk
        // because that is what `ffmpeg -i in.vid out%05d.png` writes, and
        // renumbering it here would put a silent off-by-one between the cache
        // and any frame a human inspects with an image viewer.
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture, format, frameIndex + 1);
    }

    /// <summary>
    /// Relative path of a clip's decoded audio track. Throws if the cue has no
    /// receipted audio, so a caller cannot silently address a file that was
    /// never decoded or that failed verification.
    /// </summary>
    public string AudioRelativePath(RetailStartupCue cue)
    {
        if (!_audioPaths.TryGetValue(cue, out string? relative))
        {
            throw new InvalidOperationException(
                $"Startup media has no decoded audio track for {cue}.");
        }

        return relative;
    }

    /// <summary>
    /// Reports that no media is available, with the reason. The sequence still
    /// runs; its beats are simply absent.
    /// </summary>
    public static RetailStartupMediaIndex Missing(string reason) =>
        new() { Unavailable = reason };

    /// <summary>
    /// Reads <c>startup-media.json</c> from <paramref name="root"/>. A malformed
    /// manifest, incomplete inventory, byte inventory that no longer matches
    /// the materializer receipt, or invalid edge-frame envelope is DROPPED
    /// rather than repaired. The supported Python producer performs the
    /// independent deep PNG validation before publishing this receipt.
    /// </summary>
    public static RetailStartupMediaIndex Load(string root, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (string.IsNullOrWhiteSpace(root))
        {
            return Missing("No startup media cache directory was configured.");
        }

        string manifest = Path.Combine(root, "startup-media.json");
        if (!fileExists(manifest))
        {
            return Missing(
                $"No startup media index at {manifest}. Run " +
                "`python ./rebuild/tools/materialize_retail_assets.py --startup-media`.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(manifest));
        }
        catch (Exception exception)
        {
            return Missing($"Startup media index at {manifest} is unreadable: {exception.Message}");
        }

        using (document)
        {
            JsonElement rootElement = document.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object ||
                !rootElement.TryGetProperty("schema", out JsonElement schema) ||
                schema.ValueKind != JsonValueKind.String ||
                schema.GetString() != Schema)
            {
                return Missing(
                    $"Startup media index at {manifest} is not schema '{Schema}'.");
            }

            var index = new RetailStartupMediaIndex
            {
                Root = root,
                SplashRelativePath = ReadSplash(rootElement, root, fileExists),
            };

            if (rootElement.TryGetProperty("clips", out JsonElement clips) &&
                clips.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty clip in clips.EnumerateObject())
                {
                    if (!Enum.TryParse(clip.Name, out RetailStartupCue cue))
                    {
                        continue;
                    }

                    try
                    {
                        int frameCount = clip.Value.GetProperty("frameCount").GetInt32();
                        int numerator = clip.Value.GetProperty("fpsNumerator").GetInt32();
                        int denominator = clip.Value.GetProperty("fpsDenominator").GetInt32();
                        int width = clip.Value.GetProperty("width").GetInt32();
                        int height = clip.Value.GetProperty("height").GetInt32();
                        string format = clip.Value.GetProperty("framePathFormat").GetString()
                            ?? string.Empty;
                        string framesSha256 = clip.Value.GetProperty("framesSha256").GetString()
                            ?? string.Empty;

                        if (frameCount <= 0 || numerator <= 0 || denominator <= 0 ||
                            width <= 0 || height <= 0 || format.Length == 0 ||
                            framesSha256.Length != 64)
                        {
                            continue;
                        }

                        bool complete = true;
                        var framePaths = new List<string>(frameCount);
                        for (int frame = 1; frame <= frameCount; frame++)
                        {
                            string relative = string.Format(
                                System.Globalization.CultureInfo.InvariantCulture,
                                format,
                                frame);
                            string framePath = Path.Combine(root, relative);
                            if (!fileExists(framePath))
                            {
                                complete = false;
                                break;
                            }
                            framePaths.Add(framePath);
                        }
                        if (complete)
                        {
                            string first = Path.Combine(
                                root,
                                string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    format,
                                    1));
                            string last = Path.Combine(
                                root,
                                string.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    format,
                                    frameCount));
                            complete =
                                HasExpectedPngEnvelope(first, width, height) &&
                                HasExpectedPngEnvelope(last, width, height) &&
                                string.Equals(
                                    ComputeFrameSetSha256(framePaths),
                                    framesSha256,
                                    StringComparison.OrdinalIgnoreCase);
                        }
                        if (!complete)
                        {
                            continue;
                        }

                        index._clips[cue] = new RetailStartupClip(
                            frameCount, numerator, denominator, width, height);
                        index._frameFormats[cue] = format;
                        ReadClipAudio(index, cue, clip.Value, root, fileExists);
                    }
                    catch (InvalidOperationException)
                    {
                        // One malformed clip is absent; it does not invalidate
                        // independently complete clips in the same local cache.
                    }
                    catch (FormatException)
                    {
                    }
                    catch (OverflowException)
                    {
                    }
                    catch (KeyNotFoundException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            if (index._clips.Count == 0 && index.SplashRelativePath is null)
            {
                return Missing(
                    $"Startup media index at {manifest} listed no usable clip or still.");
            }

            return index;
        }
    }

    /// <summary>
    /// Attaches a clip's decoded audio track if — and only if — the manifest
    /// declares one and the file on disk is still exactly the bytes the
    /// materializer receipted.
    ///
    /// <para><b>A failure here drops the AUDIO, not the CLIP.</b> Everywhere
    /// else in this index an unverifiable artefact drops its whole beat, because
    /// a short or wrong-rate video would be a fabrication of retail footage. A
    /// clip whose frames verify and whose audio does not is different: the
    /// resulting silent movie is exactly the state this reconstruction shipped
    /// before the track was decoded at all, so it substitutes nothing. Dropping
    /// the clip instead would turn a bad 21.8 MB file into 123.80 s of missing
    /// narrative.</para>
    /// </summary>
    private static void ReadClipAudio(
        RetailStartupMediaIndex index,
        RetailStartupCue cue,
        JsonElement clip,
        string root,
        Func<string, bool> fileExists)
    {
        if (!clip.TryGetProperty("audio", out JsonElement audio) ||
            audio.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        int track;
        int sampleRate;
        int channels;
        int bitsPerSample;
        long sampleFrameCount;
        string relative;
        string expectedSha256;
        try
        {
            track = audio.GetProperty("track").GetInt32();
            sampleRate = audio.GetProperty("sampleRate").GetInt32();
            channels = audio.GetProperty("channels").GetInt32();
            bitsPerSample = audio.GetProperty("bitsPerSample").GetInt32();
            sampleFrameCount = audio.GetProperty("sampleFrameCount").GetInt64();
            relative = audio.GetProperty("path").GetString() ?? string.Empty;
            expectedSha256 = audio.GetProperty("outputSha256").GetString() ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or FormatException or
                OverflowException or KeyNotFoundException)
        {
            return;
        }

        if (track < 0 || sampleRate <= 0 || channels <= 0 || bitsPerSample != 16 ||
            sampleFrameCount <= 0 || relative.Length == 0 || expectedSha256.Length != 64)
        {
            return;
        }

        string path = Path.Combine(root, relative);
        if (!fileExists(path) ||
            ReadPcmWavFormat(path) != (sampleRate, channels, bitsPerSample, sampleFrameCount) ||
            !HasSha256(path, expectedSha256))
        {
            return;
        }

        index._clipAudio[cue] = new RetailStartupClipAudio(
            track, sampleRate, channels, bitsPerSample, sampleFrameCount);
        index._audioPaths[cue] = relative;
    }

    /// <summary>
    /// Reads the canonical 44-byte PCM WAV header the materializer writes and
    /// returns <c>(rate, channels, bits, sample frames)</c>, or nulls on
    /// anything else.
    ///
    /// <para>Deliberately strict rather than a general chunk walk: the decode
    /// runs ffmpeg with <c>-fflags +bitexact</c> so that no LIST/INFO chunk
    /// naming the encoder build reaches the file. Tolerating one here would mean
    /// the receipt covers a build-dependent string as well as retail audio, and
    /// the sample-frame count below is what <see cref="RetailStartupClipAudio"/>
    /// uses as the track's length.</para>
    /// </summary>
    private static (int Rate, int Channels, int Bits, long SampleFrames)? ReadPcmWavFormat(
        string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[44];
            if (stream.Length < 44)
            {
                return null;
            }
            stream.ReadExactly(header);

            if (!header[..4].SequenceEqual("RIFF"u8) ||
                !header[8..12].SequenceEqual("WAVE"u8) ||
                !header[12..16].SequenceEqual("fmt "u8) ||
                !header[36..40].SequenceEqual("data"u8))
            {
                return null;
            }

            uint riffSize =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);
            uint formatSize =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[16..20]);
            ushort audioFormat =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[20..22]);
            ushort channels =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[22..24]);
            uint rate =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[24..28]);
            uint byteRate =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[28..32]);
            ushort blockAlign =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[32..34]);
            ushort bits =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(header[34..36]);
            uint dataSize =
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header[40..44]);

            if (formatSize != 16 || audioFormat != 1 || channels == 0 || rate == 0 ||
                bits == 0 || bits % 8 != 0 ||
                blockAlign != channels * (bits / 8) ||
                byteRate != rate * blockAlign ||
                riffSize != stream.Length - 8 ||
                dataSize != stream.Length - 44 ||
                dataSize == 0 ||
                dataSize % blockAlign != 0)
            {
                return null;
            }

            return ((int)rate, channels, bits, dataSize / blockAlign);
        }
        catch (IOException)
        {
            // EndOfStreamException included: a truncated receipt is a dropped
            // track, never a repaired one.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadSplash(
        JsonElement rootElement, string root, Func<string, bool> fileExists)
    {
        if (rootElement.ValueKind != JsonValueKind.Object ||
            !rootElement.TryGetProperty("stills", out JsonElement stills) ||
            stills.ValueKind != JsonValueKind.Object ||
            !stills.TryGetProperty(nameof(RetailStartupCue.Splash), out JsonElement splash) ||
            splash.ValueKind != JsonValueKind.Object ||
            !splash.TryGetProperty("path", out JsonElement path) ||
            path.ValueKind != JsonValueKind.String ||
            !splash.TryGetProperty("outputSha256", out JsonElement outputSha256) ||
            outputSha256.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? relative = path.GetString();
        string? expectedSha256 = outputSha256.GetString();
        string splashPath = Path.Combine(root, relative ?? string.Empty);
        if (string.IsNullOrWhiteSpace(relative) ||
            expectedSha256?.Length != 64 ||
            !fileExists(splashPath) ||
            !HasExpectedPngEnvelope(splashPath, 512, 512) ||
            !HasSha256(splashPath, expectedSha256))
        {
            return null;
        }

        return relative;
    }

    private static bool HasSha256(string path, string expectedSha256)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            return string.Equals(
                Convert.ToHexString(SHA256.HashData(stream)),
                expectedSha256,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasExpectedPngEnvelope(
        string path,
        int expectedWidth,
        int expectedHeight)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> signatureBytes = stackalloc byte[8];
            stream.ReadExactly(signatureBytes);
            ReadOnlySpan<byte> signature =
                [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
            if (!signatureBytes.SequenceEqual(signature))
            {
                return false;
            }

            bool firstChunk = true;
            bool sawHeader = false;
            bool sawImagePayload = false;
            Span<byte> chunkHeader = stackalloc byte[8];
            Span<byte> headerPayload = stackalloc byte[13];
            Span<byte> crc = stackalloc byte[4];
            for (int chunk = 0; chunk < 4096; chunk++)
            {
                stream.ReadExactly(chunkHeader);
                uint length =
                    System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[..4]);
                ReadOnlySpan<byte> kind = chunkHeader[4..8];
                if (stream.Position + length + 4L > stream.Length)
                {
                    return false;
                }

                if (kind.SequenceEqual("IHDR"u8))
                {
                    if (!firstChunk || length != 13)
                    {
                        return false;
                    }
                    stream.ReadExactly(headerPayload);
                    if (System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
                            headerPayload[..4]) !=
                            (uint)expectedWidth ||
                        System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
                            headerPayload[4..8]) !=
                            (uint)expectedHeight)
                    {
                        return false;
                    }
                    sawHeader = true;
                }
                else
                {
                    stream.Seek(length, SeekOrigin.Current);
                }

                stream.ReadExactly(crc);
                if (kind.SequenceEqual("IDAT"u8) && length > 0)
                {
                    sawImagePayload = true;
                }
                if (kind.SequenceEqual("IEND"u8))
                {
                    return length == 0 &&
                        sawHeader &&
                        sawImagePayload &&
                        stream.Position == stream.Length;
                }
                firstChunk = false;
            }
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ComputeFrameSetSha256(IReadOnlyList<string> paths)
    {
        using IncrementalHash digest =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        digest.AppendData(FrameSetDomain);
        byte[] separator = [0];
        byte[] buffer = new byte[1024 * 1024];
        foreach (string path in paths)
        {
            digest.AppendData(Encoding.ASCII.GetBytes(Path.GetFileName(path)));
            digest.AppendData(separator);
            long length = new FileInfo(path).Length;
            digest.AppendData(Encoding.ASCII.GetBytes(
                length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            digest.AppendData(separator);
            using FileStream stream = File.OpenRead(path);
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                digest.AppendData(buffer, 0, read);
            }
        }
        return Convert.ToHexString(digest.GetHashAndReset());
    }
}
