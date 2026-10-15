// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using OnslaughtRebuild.Client;
using D = Godot.Collections.Dictionary;

namespace OnslaughtRebuild.GodotClient;

/// <summary>AYA texture admission contract for <see cref="CuratedAyaTextureLoader"/>:
/// every actual import use decodes at its expected size; every synthetic
/// malformed file (fresh and user-local) gets its pinned outcome; every
/// truncated-pixel refusal coincides with a real short read in Godot's DDS
/// loader. <c>--aya-expect=REPORT</c> also compares each case with a prior
/// report, including the decoded bytes of the actual textures.</summary>
public sealed partial class AyaTextureChecks : Node
{
    private const string TruncatedPixels = "Curated texture has truncated DDS pixel data.";
    private const string ShortReadOrigin = "file_access_memory.cpp";
    private const string ManifestPath = "res://Assets/Level100/StaticWorld/level100-static-world.json";
    private const int SourceLimit = 2 * 1024 * 1024;
    private const int DecodedLimit = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = true };
    private readonly List<string> _failures = [];
    private readonly List<string> _completed = [];
    private readonly List<CaseReceipt> _cases = [];
    private readonly Dictionary<string, string> _inputHashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _inputHashesAfter = new(StringComparer.Ordinal);
    private int _checks;
    private int _priorCompared;
    private string? _outputDirectory;
    private string _godotVersion = "unavailable";
    private string _godotHash = "unavailable";
    private DiagnosticLogger? _logger;

    private sealed record Spec(string Name, string Path, int Width, int Height, int Compression,
        int? TargetFormat = null, int? MipCount = null, bool MustSucceed = false,
        string[]? DiagnosticOrigins = null, [property: JsonIgnore] byte[]? LoaderProbe = null);
    private sealed record ImageFacts(int Width, int Height, int Format, bool HasMipmaps, int MipmapCount,
        int ByteCount, string Sha256);
    private sealed record Outcome(bool Ok, string? ErrorType = null, string? Error = null, ImageFacts? Image = null);
    private sealed record CaseReceipt(Spec Spec, Outcome Actual, bool ContractPassed);
    private sealed record Diagnostic(string Case, string Phase, string Function, string File, int Line,
        string Code, string Rationale, int ErrorType, bool Expected);
    private sealed record Expected(bool Ok, string? Error, ImageFacts? Image);

    public override void _Ready()
    {
        try
        {
            Require(!Engine.IsEditorHint() && DisplayServer.GetName() == "headless", "AYA checks require a headless runtime.");
            using (D version = Engine.GetVersionInfo())
            using (Variant versionName = version["string"])
            using (Variant versionHash = version["hash"])
            {
                _godotVersion = versionName.AsString();
                _godotHash = versionHash.AsString();
            }
            _outputDirectory = CreateOwnedDirectory();
            _logger = new DiagnosticLogger();
            OS.AddLogger(_logger);
            Input.MouseModeEnum pointer = Input.MouseMode;
            List<Spec> imports = ImportSpecs();
            List<Spec> extras = ExtraSpecs();
            foreach (string path in imports.Concat(extras).Select(spec => spec.Path).Prepend(ManifestPath).Distinct(StringComparer.Ordinal))
                _inputHashes.Add(path, HashInput(path));
            Require(string.Equals(_inputHashes[ManifestPath], Level100ActorDefinitionManifest.ExpectedManifestSha256,
                StringComparison.OrdinalIgnoreCase), "The actual static manifest must retain its pinned identity.");

            foreach (Spec spec in imports) Admit(spec, null);
            _completed.Add("47_import_uses");
            foreach (Spec spec in extras) Admit(spec, null);
            _completed.Add("cursor_and_fonts");
            List<Spec> synthetic = SyntheticSpecs().ToList();
            Check(synthetic.Count == SyntheticOutcomes.Count && synthetic.All(spec => SyntheticOutcomes.ContainsKey(spec.Name["synthetic/".Length..])),
                "Every synthetic fixture must have exactly one pinned outcome.");
            foreach (Spec spec in synthetic) Admit(spec, SyntheticOutcomes.GetValueOrDefault(spec.Name["synthetic/".Length..]));
            _completed.Add("synthetic_admission");
            if (PriorReport() is string prior)
            {
                CompareWithPrior(prior);
                _completed.Add("prior_report");
            }
            Check(Input.MouseMode == pointer, "Texture admission changed pointer ownership.");
        }
        catch (Exception error)
        {
            _failures.Add("Harness aborted: " + error);
        }
        finally
        {
            // Preserve these checks even when an individual case or fixture
            // construction unexpectedly aborts the remaining sections.
            foreach ((string path, string before) in _inputHashes)
            {
                try
                {
                    string after = HashInput(path);
                    _inputHashesAfter.Add(path, after);
                    Check(after == before, "Private source changed: " + path);
                }
                catch (Exception error) { _failures.Add("Cannot recheck input '" + path + "': " + error.Message); }
            }
            if (_inputHashes.Count > 0) _completed.Add("private_inputs_unchanged");
            Diagnostic[] diagnostics = [];
            if (_logger is not null)
            {
                OS.RemoveLogger(_logger);
                diagnostics = _logger.Snapshot();
                _logger.Dispose();
                _logger = null;
            }
            foreach (Diagnostic diagnostic in diagnostics.Where(item => !item.Expected))
                _failures.Add($"Unexpected engine diagnostic in {diagnostic.Case}/{diagnostic.Phase}: {diagnostic.File}:{diagnostic.Line} {diagnostic.Code} {diagnostic.Rationale}");
            string[] required = ["47_import_uses", "cursor_and_fonts", "synthetic_admission", "private_inputs_unchanged"];
            Check(required.All(_completed.Contains), "One or more required AYA sections did not complete.");
            var report = new
            {
                schema = 2, failureCount = _failures.Count, checks = _checks, caseCount = _cases.Count,
                runtime = RuntimeInformation.FrameworkDescription, godot = _godotVersion, godotHash = _godotHash,
                completed = _completed, priorCompared = _priorCompared, inputHashes = _inputHashes,
                inputHashesAfter = _inputHashesAfter, cases = _cases, diagnostics, failures = _failures,
            };
            try
            {
                if (_outputDirectory is not null)
                    File.WriteAllText(Path.Combine(_outputDirectory, "report.json"), JsonSerializer.Serialize(report, JsonOptions));
            }
            catch (Exception error) { _failures.Add("Cannot write report: " + error.Message); }
            GD.Print("AYA_TEXTURE_CHECKS " + JsonSerializer.Serialize(new
            {
                failure_count = _failures.Count, checks = _checks, case_count = _cases.Count,
                runtime = RuntimeInformation.FrameworkDescription, godot = _godotVersion, godot_hash = _godotHash,
                prior_compared = _priorCompared, expected_diagnostics = diagnostics.Count(item => item.Expected),
                unexpected_diagnostics = diagnostics.Count(item => !item.Expected), completed = _completed,
                report = _outputDirectory is null ? null : Path.Combine(_outputDirectory, "report.json"),
            }));
            foreach (string failure in _failures) GD.Print("AYA_FAILURE " + failure);
            GetTree().Quit(_failures.Count == 0 ? 0 : 1);
        }
    }

    private List<Spec> ImportSpecs()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(ProjectSettings.GlobalizePath(ManifestPath)));
        JsonElement textures = Property(manifest.RootElement, "textures");
        Require(textures.ValueKind == JsonValueKind.Object, "Static texture table is not an object.");
        var result = new List<Spec>();
        foreach (JsonProperty row in textures.EnumerateObject())
        {
            JsonElement entry = row.Value;
            string compression = Property(entry, "compression").GetString()!;
            Require(Enum.TryParse(compression, false, out CuratedAyaTextureLoader.Compression parsed),
                "Unknown actual manifest compression: " + compression);
            result.Add(new Spec("static/" + row.Name, Property(entry, "resourcePath").GetString()!,
                Property(entry, "width").GetInt32(), Property(entry, "height").GetInt32(), (int)parsed, MustSucceed: true));
        }
        Require(result.Count == 34, "The production manifest must contribute exactly 34 ordered texture uses.");
        foreach (string face in new[] { "cent", "up", "right", "down", "left" })
            result.Add(new Spec("sky/" + face, $"res://Assets/Level100/Sky/cube25-{face}.texture.aya", 512, 512, 0, MustSucceed: true));
        result.Add(new Spec("shared/chrome3", "res://Assets/Level100/StaticWorld/Textures/meshtex-chrome3.texture.aya", 128, 128, 1, MustSucceed: true));
        result.Add(new Spec("shared/overlay", "res://Assets/Level100/Textures/material-overlay-a8trust5.texture.aya", 128, 128, 1, MustSucceed: true));
        foreach (string name in new[] { "target-tank", "target-truck", "target-warehouse-m001", "target-warehouse-m002", "transporter-lifter01", "transporter-lifter02" })
            result.Add(new Spec("actor/" + name, $"res://Assets/Level100/Textures/{name}.texture.aya", 512, 512, 1, MustSucceed: true));
        Require(result.Count == 47, "All 47 actual import uses, including repeated shared inputs, must be admitted.");
        return result;
    }

    private static List<Spec> ExtraSpecs() =>
    [
        new("cursor/rgba8-eight-levels", "res://Assets/Frontend/mouse-cursor.texture.aya", 128, 128, 1, (int)Image.Format.Rgba8, 8, true),
        new("font/font-13ps", "res://Assets/Hud/font-13ps.texture.aya", 256, 256, 2, MustSucceed: true),
        new("font/font-22", "res://Assets/Hud/font-22.texture.aya", 512, 512, 2, MustSucceed: true),
        new("pause/dxt2-circle", "res://Assets/PauseMenu/circle-01.texture.aya", 256, 256, 1, MustSucceed: true),
    ];

    private void Admit(Spec spec, Expected? expected)
    {
        int failuresBefore = _failures.Count;
        Outcome actual = Phase(spec, "decode", spec.DiagnosticOrigins ?? [], () => Decode(spec));
        if (spec.MustSucceed)
        {
            Check(actual.Ok, spec.Name + ": admitted import failed: " + actual.Error);
            Check(actual.Image is { } image && image.Width == spec.Width && image.Height == spec.Height,
                spec.Name + ": admitted import has the wrong dimensions.");
        }
        if (expected is not null)
        {
            Check(actual.Ok == expected.Ok, $"{spec.Name}: success {actual.Ok}, pinned {expected.Ok}; {actual.Error}");
            if (!expected.Ok)
            {
                Check(actual.ErrorType == nameof(InvalidDataException), spec.Name + ": refusal changed exception type: " + actual.ErrorType);
                Check(actual.Error == expected.Error!.Replace("{path}", spec.Path, StringComparison.Ordinal),
                    $"{spec.Name}: refusal '{actual.Error}', pinned '{expected.Error}'.");
            }
            else
            {
                Check(actual.Image == expected.Image, $"{spec.Name}: image {actual.Image}, pinned {expected.Image}.");
            }
        }
        if (spec.LoaderProbe is byte[] dds)
        {
            // A pixel truncation refusal is only as strict as the loader: the
            // same bytes must make Godot's DDS loader read past the payload.
            Check(actual.Error == TruncatedPixels, spec.Name + ": loader probe given to a case that is not a truncation refusal.");
            Phase(spec, "loader", ["texture_loader_dds.cpp", "image.cpp", ShortReadOrigin], () =>
            {
                using var image = new Image();
                image.LoadDdsFromBuffer(dds);
                return new Outcome(true);
            });
            Check(_logger!.Logged(spec.Name, "loader", ShortReadOrigin), spec.Name + ": truncation refused without a loader short read.");
        }
        _cases.Add(new CaseReceipt(spec, actual, failuresBefore == _failures.Count));
    }

    private Outcome Phase(Spec spec, string phase, string[] origins, Func<Outcome> action)
    {
        GD.Print("AYA_CASE_BEGIN " + JsonSerializer.Serialize(new { name = spec.Name, phase, expected_diagnostic_origins = origins }));
        _logger!.Begin(spec.Name, phase, origins);
        Outcome outcome;
        try { outcome = action(); }
        catch (Exception error) { outcome = new Outcome(false, error.GetType().Name, error.Message); }
        finally { _logger.End(); }
        GD.Print("AYA_CASE_END " + JsonSerializer.Serialize(new { name = spec.Name, phase, ok = outcome.Ok, error_type = outcome.ErrorType }));
        return outcome;
    }

    private static Outcome Decode(Spec spec)
    {
        using Texture2D texture = CuratedAyaTextureLoader.Load(spec.Path, spec.Width, spec.Height,
            (CuratedAyaTextureLoader.Compression)spec.Compression,
            spec.TargetFormat is int format ? (Image.Format)format : null, spec.MipCount);
        using Image image = texture.GetImage();
        byte[] bytes = image.GetData();
        return new Outcome(true, Image: new ImageFacts(image.GetWidth(), image.GetHeight(), (int)image.GetFormat(),
            image.HasMipmaps(), image.GetMipmapCount(), bytes.Length, Hex(bytes)));
    }

    private static string? PriorReport()
    {
        string[] choices = OS.GetCmdlineUserArgs().Where(argument => argument.StartsWith("--aya-expect=", StringComparison.Ordinal)).ToArray();
        Require(choices.Length <= 1, "Supply at most one --aya-expect.");
        return choices.Length == 0 ? null : choices[0]["--aya-expect=".Length..];
    }

    // A prior report is either this check's (schema 2, "actual") or the
    // retired three-decoder comparison's (schema 1, whose "native" outcome was
    // the production decoder). Messages are compared with each run's own paths.
    private void CompareWithPrior(string path)
    {
        using JsonDocument prior = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = prior.RootElement;
        string outcomeName = root.GetProperty("schema").GetInt32() == 1 ? "native" : "actual";
        var previous = new Dictionary<string, (string Path, JsonElement Outcome)>(StringComparer.Ordinal);
        foreach (JsonElement item in root.GetProperty("cases").EnumerateArray())
        {
            JsonElement spec = item.GetProperty("spec");
            previous.Add(spec.GetProperty("name").GetString()!, (spec.GetProperty("path").GetString()!, item.GetProperty(outcomeName).Clone()));
        }
        Check(previous.Count == _cases.Count, $"Prior report has {previous.Count} cases; this run has {_cases.Count}.");
        foreach (CaseReceipt receipt in _cases)
        {
            if (!previous.TryGetValue(receipt.Spec.Name, out var entry))
            {
                Check(false, receipt.Spec.Name + ": missing from the prior report.");
                continue;
            }
            JsonElement old = entry.Outcome;
            Outcome actual = receipt.Actual;
            Check(old.GetProperty("ok").GetBoolean() == actual.Ok, receipt.Spec.Name + ": success differs from the prior report.");
            string? oldError = old.TryGetProperty("error", out JsonElement error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()!.Replace(entry.Path, "{path}", StringComparison.Ordinal) : null;
            Check(oldError == actual.Error?.Replace(receipt.Spec.Path, "{path}", StringComparison.Ordinal),
                $"{receipt.Spec.Name}: refusal '{actual.Error}' differs from the prior '{oldError}'.");
            if (actual.Image is { } image && old.TryGetProperty("image", out JsonElement facts) && facts.ValueKind == JsonValueKind.Object)
            {
                var before = new ImageFacts(facts.GetProperty("width").GetInt32(), facts.GetProperty("height").GetInt32(),
                    facts.GetProperty("format").GetInt32(), facts.GetProperty("has_mipmaps").GetBoolean(),
                    facts.GetProperty("mipmap_count").GetInt32(), facts.GetProperty("byte_count").GetInt32(),
                    facts.GetProperty("sha256").GetString()!);
                Check(before == image, $"{receipt.Spec.Name}: decoded image {image} differs from the prior {before}.");
            }
            _priorCompared++;
        }
    }

    private IEnumerable<Spec> SyntheticSpecs()
    {
        byte[] rgba = Dds(2, 4, 4, 1), dxt1 = Dds(0, 4, 4, 1), dxt2 = Dds(1, 4, 4, 1);
        byte[] compressed = Compress(rgba), valid = Frame(compressed);
        string[] zlib = ["stream_peer_gzip.cpp"];
        string[] ddsDiagnostic = ["texture_loader_dds.cpp", "image.cpp"];
        string[] formatDiagnostic = ["image.cpp"];
        yield return Fixture("rgba-control", valid, mustSucceed: true);
        yield return Fixture("dxt1-control", Frame(Compress(dxt1)), compression: 0, mustSucceed: true);
        yield return Fixture("dxt2-control", Frame(Compress(dxt2)), compression: 1, mustSucceed: true);
        yield return Fixture("rgba-three-mips", Frame(Compress(Dds(2, 4, 4, 3))), mips: 3, mustSucceed: true);
        yield return Fixture("rgba-header-zero-mips", Frame(Compress(Mutate(rgba, 28, 0))), mips: 0, mustSucceed: true);
        yield return Fixture("aya-two-records", Join(Frame(Compress(rgba[..73])), Frame(Compress(rgba[73..]))), mustSucceed: true);
        // The pinned .NET ZLibStream writes nothing for an empty payload, so
        // spell out a complete empty zlib member (fixed block, Adler-32 of 1).
        yield return Fixture("aya-empty-second-record", Join(valid, Frame([0x78, 0x9c, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01])), mustSucceed: true);
        yield return Fixture("aya-zero-length-second-record", Join(valid, [0, 0, 0, 0]));
        yield return Fixture("empty-source", []);
        yield return Fixture("short-record-header", [1, 2, 3]);
        yield return Fixture("zero-record-length", [0, 0, 0, 0]);
        yield return Fixture("oversized-record-length", [255, 255, 255, 255]);
        yield return Fixture("record-past-input", [8, 0, 0, 0, 1]);
        yield return Fixture("trailing-partial-record", Join(valid, [1]));
        yield return Fixture("empty-dds", Frame(Compress([])));
        yield return Fixture("invalid-zlib", Frame([0xff, 0, 0, 0]), origins: zlib);
        byte[] corrupt = (byte[])compressed.Clone(); corrupt[^1] ^= 1;
        yield return Fixture("bad-adler32", Frame(corrupt), origins: zlib);
        for (int missing = 1; missing <= 8; missing++)
            yield return Fixture("zlib-missing-suffix-" + missing, Frame(compressed[..^missing]), origins: [.. zlib, .. ddsDiagnostic]);
        yield return Fixture("small-trailing-bytes", Frame(Join(compressed, [7, 8])), origins: zlib);
        yield return Fixture("concatenated-members", Frame(Join(compressed, Compress([11, 22, 33]))), origins: zlib);
        foreach (int length in new[] { 8191, 8192, 8193 })
            yield return Fixture("short-member-record-" + length, Frame(Pad(compressed, length)), origins: zlib);
        byte[] exactBlock = Exact8192ByteMember();
        yield return Fixture("exact8192-member-no-tail", Frame(exactBlock));
        yield return Fixture("exact8192-member-one-tail-byte", Frame(Join(exactBlock, [0])), origins: zlib);
        byte[] largeDds = Dds(2, 64, 64, 1), largeCompressed = Compress(largeDds);
        Require(largeCompressed.Length > 8192, "Deterministic noisy DDS must exercise more than one managed input buffer.");
        int boundary = (largeCompressed.Length + 8191) / 8192 * 8192;
        foreach (int length in new[] { boundary - 1, boundary, boundary + 1 })
            yield return Fixture("large-member-record-" + length, Frame(Pad(largeCompressed, length)), width: 64, height: 64, origins: zlib);
        yield return Fixture("source-at-limit", Frame(Pad(compressed, SourceLimit - 4)), origins: zlib);
        yield return Fixture("source-over-limit", new byte[SourceLimit + 1]);
        // No allocation exceeds the accepted 8 MiB output limit. The extra
        // byte is streamed into zlib and must be refused before DDS decoding.
        yield return Fixture("decoded-at-limit", Frame(CompressWithPadding(rgba, DecodedLimit)), origins: ddsDiagnostic);
        yield return Fixture("decoded-over-limit", Frame(CompressWithPadding(rgba, DecodedLimit + 1)), origins: zlib);
        byte[] overLimitBadChecksum = CompressWithPadding(rgba, DecodedLimit + 1);
        overLimitBadChecksum[^1] ^= 1;
        yield return Fixture("decoded-over-limit-bad-checksum", Frame(overLimitBadChecksum), origins: zlib);
        yield return Fixture("second-record-crosses-decoded-limit",
            Join(Frame(CompressWithPadding(rgba, DecodedLimit - 1)), Frame(Compress([0, 0]))), origins: zlib);
        yield return Fixture("dds-short-header", Frame(Compress(rgba[..127])));
        yield return Fixture("dds-header-size", Frame(Compress(Mutate(rgba, 4, 123))), origins: ddsDiagnostic);
        yield return Fixture("dds-pixel-format-size", Frame(Compress(Mutate(rgba, 76, 31))), origins: ddsDiagnostic);
        byte[] badMagic = (byte[])rgba.Clone(); badMagic[0] = 0;
        yield return Fixture("dds-bad-magic", Frame(Compress(badMagic)));
        foreach (int offset in new[] { 80, 84, 88, 92, 96, 100, 104 })
            yield return Fixture("rgba-mask-word-" + offset, Frame(Compress(Mutate(rgba, offset, BinaryPrimitives.ReadUInt32LittleEndian(rgba.AsSpan(offset, 4)) ^ 1))));
        yield return Fixture("wrong-fourcc", Frame(Compress(dxt1)), compression: 1);
        foreach (int compression in new[] { -1, 3, int.MaxValue })
            yield return Fixture("compression-" + compression, valid, compression: compression);
        foreach (int? mip in new int?[] { null, 0, 1, 2, -1, -2, int.MinValue })
            yield return Fixture("mip-option-" + (mip?.ToString() ?? "null"), valid, mips: mip);
        foreach (int? target in new int?[] { null, (int)Image.Format.Rgba8, (int)Image.Format.Rgb8, -1, int.MinValue, int.MaxValue })
            yield return Fixture("target-option-" + (target?.ToString() ?? "null"), valid, target: target, origins: formatDiagnostic);
        yield return Fixture("dxt2-rgba-conversion", Frame(Compress(dxt2)), compression: 1, target: (int)Image.Format.Rgba8, mustSucceed: true);
        yield return Fixture("width-mismatch", valid, width: 5);
        yield return Fixture("width-zero", valid, width: 0);
        yield return Fixture("height-negative", valid, height: -1);
        // The loader reads a short surface from uninitialized memory, so the
        // decoder refuses it first; each probe shows the loader's short read.
        byte[] shortDxt1 = dxt1[..129];
        byte[] shortPixels = Frame(Compress(shortDxt1));
        yield return Fixture("short-pixels", shortPixels, compression: 0, probe: shortDxt1);
        yield return Fixture("short-pixels-before-dimension", shortPixels, compression: 0, width: 5, probe: shortDxt1);
        yield return Fixture("format-before-short-pixels", shortPixels, compression: 1);
        yield return Fixture("mips-before-short-pixels", shortPixels, compression: 0, mips: 2);
        yield return Fixture("decode-before-dimension", Frame(Compress(Dds(2, 5, 4, 1))), origins: ddsDiagnostic);
        byte[] wideHeader = Mutate(rgba, 16, 5);
        yield return Fixture("decode-truncated-header-width", Frame(Compress(wideHeader)), probe: wideHeader);
        byte[] odd = Dds(0, 5, 4, 1), mips = Dds(2, 4, 4, 3);
        yield return Fixture("dxt1-odd-width-complete", Frame(Compress(odd)), width: 5, compression: 0, mustSucceed: true, origins: ddsDiagnostic);
        yield return Fixture("dxt1-odd-width-short", Frame(Compress(odd[..^1])), width: 5, compression: 0, probe: odd[..^1]);
        yield return Fixture("rgba-three-mips-short", Frame(Compress(mips[..^1])), mips: 3, probe: mips[..^1]);
        byte[] cube = Mutate(rgba, 112, 0x200), volume = Mutate(Mutate(rgba, 112, 0x200000), 24, 2);
        yield return Fixture("rgba-cubemap-six-faces", Frame(Compress(Join(cube, Surfaces(rgba, 5)))), mustSucceed: true);
        yield return Fixture("rgba-cubemap-one-face", Frame(Compress(cube)), probe: cube);
        yield return Fixture("rgba-volume-two-slices", Frame(Compress(Join(volume, Surfaces(rgba, 1)))), mustSucceed: true);
        yield return Fixture("rgba-volume-one-slice", Frame(Compress(volume)), probe: volume);
        yield return Fixture("format-before-mips-and-dimension", Frame(Compress(Mutate(rgba, 92, 0))), width: 0, mips: 2);
        yield return Fixture("mips-before-dimension", valid, width: 0, mips: 2);
        yield return Fixture("dimension-before-negative-format", valid, width: 0, target: -1);
        yield return Fixture("magic-before-negative-options", Frame(Compress(badMagic)), width: 0, target: -1, mips: -1);
        string absent = Path.Combine(_outputDirectory!, "never-created.texture.aya");
        yield return new Spec("synthetic/missing-file", absent, 4, 4, 2, DiagnosticOrigins: ["file_access.cpp"]);
    }

    // Outcomes of the production decoder on 2026-09-25 (report of the retired
    // three-decoder check at 37cf89b3, its "native" column): every refusal
    // message ({path} is the fixture's own path) and every decoded image.
    private static readonly Dictionary<string, Expected> SyntheticOutcomes = new(StringComparer.Ordinal)
    {
        ["rgba-control"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["dxt1-control"] = Pass(4, 4, 17, false, 0, 8, "aaa530fa0b5178230efe904c27e31f8d148a08c95f3900d6c3f2258d9867c369"),
        ["dxt2-control"] = Pass(4, 4, 18, false, 0, 16, "4a39f6245708ed68c8068b1eb4ca77a1a226d2aec6cde894bf1f61d964b3b418"),
        ["rgba-three-mips"] = Pass(4, 4, 5, true, 2, 84, "2127f4770d758c5a9c36c0eadfb911aab42210dd3eb643289816b3650fbb471a"),
        ["rgba-header-zero-mips"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["aya-two-records"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["aya-empty-second-record"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["aya-zero-length-second-record"] = Refuse("Curated texture has invalid AYA record framing."),
        ["empty-source"] = Refuse("Curated texture '{path}' is missing or exceeds the source limit."),
        ["short-record-header"] = Refuse("Curated texture has a truncated AYA record header."),
        ["zero-record-length"] = Refuse("Curated texture has invalid AYA record framing."),
        ["oversized-record-length"] = Refuse("Curated texture has invalid AYA record framing."),
        ["record-past-input"] = Refuse("Curated texture has invalid AYA record framing."),
        ["trailing-partial-record"] = Refuse("Curated texture has a truncated AYA record header."),
        ["empty-dds"] = Refuse("Curated texture has invalid AYA record framing."),
        ["invalid-zlib"] = Refuse("Curated texture contains an invalid zlib stream."),
        ["bad-adler32"] = Refuse("Curated texture contains an invalid zlib stream."),
        ["zlib-missing-suffix-1"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-2"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-3"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-4"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-5"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-6"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-7"] = Refuse("Curated texture has a truncated zlib stream."),
        ["zlib-missing-suffix-8"] = Refuse("Curated texture has a truncated zlib stream."),
        ["small-trailing-bytes"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["concatenated-members"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["short-member-record-8191"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["short-member-record-8192"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["short-member-record-8193"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["exact8192-member-no-tail"] = Refuse("Curated texture is not an AYA-wrapped DDS image."),
        ["exact8192-member-one-tail-byte"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["large-member-record-24575"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["large-member-record-24576"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["large-member-record-24577"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["source-at-limit"] = Refuse("Curated texture AYA record contains trailing compressed data."),
        ["source-over-limit"] = Refuse("Curated texture '{path}' is missing or exceeds the source limit."),
        ["decoded-at-limit"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["decoded-over-limit"] = Refuse("Curated texture exceeds the decoded DDS limit."),
        ["decoded-over-limit-bad-checksum"] = Refuse("Curated texture contains an invalid zlib stream."),
        ["second-record-crosses-decoded-limit"] = Refuse("Curated texture exceeds the decoded DDS limit."),
        ["dds-short-header"] = Refuse("Curated texture is not an AYA-wrapped DDS image."),
        ["dds-header-size"] = Refuse("Godot could not decode curated texture '{path}' (ParseError)."),
        ["dds-pixel-format-size"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["dds-bad-magic"] = Refuse("Curated texture is not an AYA-wrapped DDS image."),
        ["rgba-mask-word-80"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-84"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-88"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-92"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-96"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-100"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["rgba-mask-word-104"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["wrong-fourcc"] = Refuse("Curated texture does not match the expected Dxt2 DDS pixel format."),
        ["compression--1"] = Refuse("Curated texture does not match the expected -1 DDS pixel format."),
        ["compression-3"] = Refuse("Curated texture does not match the expected 3 DDS pixel format."),
        ["compression-2147483647"] = Refuse("Curated texture does not match the expected 2147483647 DDS pixel format."),
        ["mip-option-null"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["mip-option-0"] = Refuse("Curated texture '{path}' does not contain the expected 0 DDS mip levels."),
        ["mip-option-1"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["mip-option-2"] = Refuse("Curated texture '{path}' does not contain the expected 2 DDS mip levels."),
        ["mip-option--1"] = Refuse("Curated texture '{path}' does not contain the expected -1 DDS mip levels."),
        ["mip-option--2"] = Refuse("Curated texture '{path}' does not contain the expected -2 DDS mip levels."),
        ["mip-option--2147483648"] = Refuse("Curated texture '{path}' does not contain the expected -2147483648 DDS mip levels."),
        ["target-option-null"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["target-option-5"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["target-option-4"] = Pass(4, 4, 4, false, 0, 48, "87452893af805b0047378dd915fc8ba7186d983fa231277028e0e67bb83f8489"),
        ["target-option--1"] = Refuse("Curated texture '{path}' could not be converted to -1."),
        ["target-option--2147483648"] = Refuse("Curated texture '{path}' could not be converted to -2147483648."),
        ["target-option-2147483647"] = Refuse("Curated texture '{path}' could not be converted to 2147483647."),
        ["dxt2-rgba-conversion"] = Pass(4, 4, 5, false, 0, 64, "93ca9d66637eda17fc5211bed231c33179cf7994e3adac0539117d73124cb474"),
        ["width-mismatch"] = Refuse("Curated texture '{path}' decoded as 4x4, expected 5x4."),
        ["width-zero"] = Refuse("Curated texture '{path}' decoded as 4x4, expected 0x4."),
        ["height-negative"] = Refuse("Curated texture '{path}' decoded as 4x4, expected 4x-1."),
        ["short-pixels"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["short-pixels-before-dimension"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["format-before-short-pixels"] = Refuse("Curated texture does not match the expected Dxt2 DDS pixel format."),
        ["mips-before-short-pixels"] = Refuse("Curated texture '{path}' does not contain the expected 2 DDS mip levels."),
        ["decode-before-dimension"] = Refuse("Curated texture '{path}' decoded as 5x4, expected 4x4."),
        ["decode-truncated-header-width"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["dxt1-odd-width-complete"] = Pass(5, 4, 17, false, 0, 16, "4a39f6245708ed68c8068b1eb4ca77a1a226d2aec6cde894bf1f61d964b3b418"),
        ["dxt1-odd-width-short"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["rgba-three-mips-short"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["rgba-cubemap-six-faces"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["rgba-cubemap-one-face"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["rgba-volume-two-slices"] = Pass(4, 4, 5, false, 0, 64, "269467f2c1b00aa1862265b9dcac100824eef1539a687c624d758cafb9bff1e7"),
        ["rgba-volume-one-slice"] = Refuse("Curated texture has truncated DDS pixel data."),
        ["format-before-mips-and-dimension"] = Refuse("Curated texture does not match the expected Rgba8 DDS pixel format."),
        ["mips-before-dimension"] = Refuse("Curated texture '{path}' does not contain the expected 2 DDS mip levels."),
        ["dimension-before-negative-format"] = Refuse("Curated texture '{path}' decoded as 4x4, expected 0x4."),
        ["magic-before-negative-options"] = Refuse("Curated texture is not an AYA-wrapped DDS image."),
        ["missing-file"] = Refuse("Curated texture '{path}' is missing or exceeds the source limit."),
    };

    private static Expected Pass(int width, int height, int format, bool hasMipmaps, int mipmapCount, int byteCount, string sha256) =>
        new(true, null, new ImageFacts(width, height, format, hasMipmaps, mipmapCount, byteCount, sha256));
    private static Expected Refuse(string error) => new(false, error, null);

    private Spec Fixture(string name, byte[] bytes, int width = 4, int height = 4, int compression = 2,
        int? target = null, int? mips = null, bool mustSucceed = false, string[]? origins = null, byte[]? probe = null)
    {
        string path = Path.Combine(_outputDirectory!, name + ".texture.aya");
        using (var output = new FileStream(path, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.None)) output.Write(bytes);
        return new Spec("synthetic/" + name, path, width, height, compression, target, mips, mustSucceed, origins, probe);
    }

    private static byte[] Dds(int compression, int width, int height, int mipCount)
    {
        int payload = 0;
        for (int level = 0, w = width, h = height; level < mipCount; level++, w = Math.Max(1, w / 2), h = Math.Max(1, h / 2))
            payload += compression == 2 ? w * h * 4 : Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * (compression == 0 ? 8 : 16);
        byte[] bytes = new byte[128 + payload];
        "DDS "u8.CopyTo(bytes);
        void Put(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        Put(4, 124); Put(8, (uint)(0x1007 | (compression == 2 ? 8 : 0x80000) | (mipCount > 1 ? 0x20000 : 0)));
        Put(12, (uint)height); Put(16, (uint)width); Put(20, (uint)(compression == 2 ? width * 4 : payload));
        Put(28, (uint)mipCount); Put(76, 32); Put(80, compression == 2 ? 0x41u : 4u);
        Put(108, (uint)(0x1000 | (mipCount > 1 ? 0x400008 : 0)));
        if (compression == 2)
        {
            Put(88, 32); Put(92, 0x00ff0000); Put(96, 0x0000ff00); Put(100, 0x000000ff); Put(104, 0xff000000);
        }
        else (compression == 0 ? "DXT1"u8 : "DXT2"u8).CopyTo(bytes.AsSpan(84));
        uint state = 0x41594131;
        for (int index = 128; index < bytes.Length; index++)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            bytes[index] = (byte)state;
        }
        return bytes;
    }

    private static byte[] Compress(byte[] payload)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(payload);
        return output.ToArray();
    }

    private static byte[] CompressWithPadding(byte[] prefix, int length)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(prefix);
            byte[] chunk = new byte[16 * 1024];
            for (int remaining = length - prefix.Length; remaining > 0; remaining -= Math.Min(remaining, chunk.Length))
                zlib.Write(chunk, 0, Math.Min(remaining, chunk.Length));
        }
        return output.ToArray();
    }

    private static byte[] Exact8192ByteMember()
    {
        // Bounded generation uses the actual pinned .NET compressor rather
        // than mistaking record padding for the compressed member's length.
        byte[] noise = new byte[8192];
        uint state = 0x41594132;
        for (int index = 0; index < noise.Length; index++)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            noise[index] = (byte)state;
        }
        for (int length = 8160; length <= noise.Length; length++)
        {
            byte[] member = Compress(noise[..length]);
            if (member.Length == 8192) return member;
        }
        throw new InvalidOperationException("Bounded compressor search could not produce an exact 8192-byte member.");
    }

    private static byte[] Frame(byte[] compressed)
    {
        byte[] result = new byte[compressed.Length + 4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)compressed.Length);
        compressed.CopyTo(result, 4); return result;
    }
    private static byte[] Surfaces(byte[] dds, int count) { byte[] surface = dds[128..], value = new byte[surface.Length * count]; for (int index = 0; index < count; index++) surface.CopyTo(value, index * surface.Length); return value; }
    private static byte[] Join(byte[] first, byte[] second) { byte[] value = new byte[first.Length + second.Length]; first.CopyTo(value, 0); second.CopyTo(value, first.Length); return value; }
    private static byte[] Pad(byte[] source, int length) { if (length < source.Length) throw new ArgumentOutOfRangeException(nameof(length)); byte[] result = new byte[length]; source.CopyTo(result, 0); return result; }
    private static byte[] Mutate(byte[] source, int offset, uint value) { byte[] result = (byte[])source.Clone(); BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(offset, 4), value); return result; }
    private static JsonElement Property(JsonElement value, string name) => value.EnumerateObject().Single(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    private static string Hex(byte[] source) => Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
    private static string HashInput(string path) => Hex(File.ReadAllBytes(ProjectSettings.GlobalizePath(path)));

    private string CreateOwnedDirectory()
    {
        string root = Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        string[] choices = OS.GetCmdlineUserArgs().Where(argument => argument.StartsWith("--aya-check-dir=", StringComparison.Ordinal)).ToArray();
        Require(choices.Length <= 1, "Supply at most one --aya-check-dir.");
        string path = choices.Length == 0 ? Path.Combine(root, "aya-texture-checks-" + Guid.NewGuid().ToString("N")) : choices[0]["--aya-check-dir=".Length..];
        Require(Path.IsPathFullyQualified(path), "Explicit AYA output directory must be absolute.");
        path = Path.GetFullPath(path);
        Require(path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Directory.Exists(path) && !File.Exists(path), "AYA output must be a fresh descendant of this invocation's user:// directory.");
        for (DirectoryInfo? parent = new DirectoryInfo(path).Parent; parent is not null && parent.FullName.StartsWith(root, StringComparison.Ordinal); parent = parent.Parent)
            Require(!parent.Exists || parent.LinkTarget is null, "AYA output may not traverse an existing symbolic link.");
        Directory.CreateDirectory(path);
        return path;
    }

    private void Check(bool condition, string message) { _checks++; if (!condition) _failures.Add(message); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed partial class DiagnosticLogger : Logger
    {
        private readonly object _gate = new();
        private readonly List<Diagnostic> _entries = [];
        private string _case = "outside-case";
        private string _phase = "outside-phase";
        private string[] _origins = [];
        internal void Begin(string name, string phase, string[] origins) { lock (_gate) { _case = name; _phase = phase; _origins = origins; } }
        internal void End() { lock (_gate) { _case = "outside-case"; _phase = "outside-phase"; _origins = []; } }
        internal Diagnostic[] Snapshot() { lock (_gate) return [.. _entries]; }
        internal bool Logged(string name, string phase, string file)
        {
            lock (_gate) return _entries.Any(item => item.Case == name && item.Phase == phase && item.File.Replace('\\', '/').EndsWith('/' + file, StringComparison.Ordinal));
        }
        public override void _LogError(string function, string file, int line, string code, string rationale,
            bool editorNotify, int errorType, Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
        {
            lock (_gate)
            {
                string origin = file.Replace('\\', '/');
                bool expected = (errorType == (int)ErrorType.Error || errorType == (int)ErrorType.Warning) &&
                    _origins.Any(allowed => origin.EndsWith('/' + allowed, StringComparison.Ordinal) || origin == allowed);
                _entries.Add(new Diagnostic(_case, _phase, function, file, line, code, rationale, errorType, expected));
            }
        }
    }
}
