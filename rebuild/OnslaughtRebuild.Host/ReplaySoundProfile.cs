// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OnslaughtRebuild.Parity;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The explicitly admitted, silent reference-playback sound process used by the tutorial route comparisons.
/// Its allocator pool is one measured native process, not a retail or player default. No profile payload is embedded.
/// The host admits this only with a tape; ordinary player audio still needs a real sound backend.
/// </summary>
public static class ReplaySoundProfile
{
    private const string ProfileHash = "e4b10a4d8b8d0b61140c2e97ac3b1ecfbe92b9caa74f42362bba20925840d009";
    private const string EnglishBankHash = "658c15e3bab844d65dd3c07c4ac880f16f741c0ea116f48c603449bbd4dda8b7";
    private const string OptionsHash = "b5aaad4ef4f95b2926b1373e5ad3e9cbab8aceef04c6762ce966b9d400f25d7e";
    // The whole-game tape starts with a different career container, but its complete 86-byte options trailer is
    // byte-identical to the level-start reference's. Admit these two measured inputs, not arbitrary player settings.
    private const string WholeGameOptionsHash = "b240bf057b98a57da05a6f123000a93e1da8dd2eb393be50ebe173adbfdeee5e";
    // SHA of the private receipt binding the separately observed 0.1.9 process to its complete tape and sidecar.
    // The receipt and its individual input hashes remain outside the public source tree.
    private const string RecordedReplayReceiptHash = "f000fc026799e2b1aeaa3a8a6218ad8c05999bcfd9b9c446693f193b5b50ff8a";

    public static VirtualSoundProcessOptions Load(string path, Func<string, (string Name, byte[] Bytes)> read) =>
        LoadCore(path, read, null);

    /// <summary>
    /// Create the specifically observed silent CE 0.1.9 full-boot replay. The pinned private receipt binds the tape,
    /// complete recorded sidecar and sound pool to a separate, repeatable port process. This is not an assertion
    /// about the original recording process, retail parity or physical audio. The two ancillary start files are
    /// authenticated with the sidecar but are not modeled by <see cref="GameRun"/>.
    /// </summary>
    public static GameRun CreateRecordedReplay(string profilePath, string receiptPath, byte[] tapeBytes, byte[] sidecarBytes,
        Func<string, (string Name, byte[] Bytes)> read, Func<int, string>? pcText, IFilmFiles films)
    {
        byte[] receiptBytes = File.ReadAllBytes(receiptPath);
        RequireHash(receiptBytes, RecordedReplayReceiptHash, "recorded replay receipt");
        using JsonDocument receipt = JsonDocument.Parse(receiptBytes);
        JsonElement admitted = receipt.RootElement;
        if (admitted.GetProperty("measured_seed_slot_count").GetInt32() != 256
            || !admitted.GetProperty("seeds_match_older_process").GetBoolean()
            || !admitted.GetProperty("probe_bytes_equal_reference_a").GetBoolean()
            || !admitted.GetProperty("copied_output_files_equal_reference_a").GetBoolean()
            || !string.Equals(admitted.GetProperty("measured_e55_profile_sha256").GetString(), ProfileHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The recorded replay receipt does not establish the required sound pool.");

        // Clone each caller-owned array before checking or parsing it; subsequent caller mutations cannot change the run.
        byte[] tapeSnapshot = tapeBytes.ToArray();
        byte[] sidecarSnapshot = sidecarBytes.ToArray();
        RequireHash(tapeSnapshot, admitted.GetProperty("tape_sha256").GetString()!, "recorded replay tape");
        RequireHash(sidecarSnapshot, admitted.GetProperty("sidecar_sha256").GetString()!, "recorded replay sidecar");
        RetailTape tape = RetailTape.Parse(tapeSnapshot);
        using JsonDocument sidecar = JsonDocument.Parse(sidecarSnapshot);
        JsonElement metadata = sidecar.RootElement;
        if (metadata.GetProperty("format").GetString() != "onslaught-ce session tape 2"
            || metadata.GetProperty("ce_version").GetString() != "0.1.9"
            || metadata.GetProperty("platform").GetString() != "linux-i686"
            || metadata.GetProperty("presents_per_second").GetInt32() != 30
            || metadata.GetProperty("clock").GetString() != "shim"
            || metadata.GetProperty("game_args").GetArrayLength() != 0)
            throw new InvalidDataException("The recorded replay startup parameters are not admitted.");

        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty item in metadata.GetProperty("start_files").EnumerateObject())
        {
            string name = item.Name;
            if (name.Contains('\\') || name.Split('/').Any(part => part is "" or "." or "..")
                || !files.TryAdd(name, item.Value.GetBytesFromBase64()))
                throw new InvalidDataException("The recorded starting-file inventory is invalid.");
        }
        if (files.Count != 7 || !files.TryGetValue("defaultoptions.bea", out byte[]? options)
            || !files.ContainsKey("cardid.txt") || !files.ContainsKey("setuphistory.txt"))
            throw new InvalidDataException("The recorded starting-file inventory is incomplete.");
        var saves = files.Where(kv => kv.Key.StartsWith("savegames/", StringComparison.OrdinalIgnoreCase)
                && kv.Key.EndsWith(".bes", StringComparison.OrdinalIgnoreCase))
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (saves.Length != 4 || saves.Any(kv => kv.Key[10..].Contains('/') || kv.Key.Any(c => c > 127)))
            throw new InvalidDataException("The recorded starting careers are not admitted.");
        RequireHash(options, admitted.GetProperty("startup_options_sha256").GetString()!, "recorded startup options");

        // This read returns a fresh copy of the very bytes authenticated above, even if the caller's source changes.
        (string Name, byte[] Bytes) Read(string name) => name.Equals("defaultoptions.bea", StringComparison.OrdinalIgnoreCase)
            ? (name, options.ToArray()) : read(name);
        VirtualSoundProcessOptions sound = LoadCore(profilePath, Read, admitted.GetProperty("startup_options_sha256").GetString()!);
        var run = new GameRun(Read, pcText, films, skipFmv: false, LevelSession.VirtualVoiceFor(Read), sound, fixedTape: tape);
        foreach (var save in saves)
            run.FrontEnd.MemoryCard.Folder.Write(Path.GetFileNameWithoutExtension(save.Key), save.Value.ToArray());
        return run;
    }

    private static VirtualSoundProcessOptions LoadCore(string path, Func<string, (string Name, byte[] Bytes)> read,
        string? recordedOptionsHash)
    {
        byte[] bytes = File.ReadAllBytes(path);
        RequireHash(bytes, ProfileHash, "recorded sound profile");
        using JsonDocument json = JsonDocument.Parse(bytes);
        JsonElement root = json.RootElement;
        JsonElement profile = root.GetProperty("seed_profile");
        if (!root.GetProperty("admitted").GetBoolean() || profile.GetProperty("node_count").GetInt32() != 256)
            throw new InvalidDataException("The recorded sound pool is not admitted or has the wrong size.");
        SoundEventSeed[] seeds = profile.GetProperty("seeds").EnumerateArray().Select((e, i) =>
        {
            if (e.GetProperty("slot").GetInt32() != i) throw new InvalidDataException("The recorded sound slots are not ordered.");
            byte[] p = Convert.FromHexString(e.GetProperty("position_xyz_raw").GetString()!);
            if (p.Length != 12) throw new InvalidDataException("A recorded sound position has the wrong size.");
            // These are observed uninitialized words: retain their bits, including non-finite values.
            return new SoundEventSeed(e.GetProperty("channel").GetInt32(), new FVector(
                BinaryPrimitives.ReadSingleLittleEndian(p), BinaryPrimitives.ReadSingleLittleEndian(p.AsSpan(4)),
                BinaryPrimitives.ReadSingleLittleEndian(p.AsSpan(8))));
        }).ToArray();
        if (seeds.Length != 256) throw new InvalidDataException("The recorded sound pool must contain all 256 slots.");
        byte[] bankBytes = read(@"data\sounds\sounds_english_pc.xap").Bytes;
        RequireHash(bankBytes, EnglishBankHash, "recorded English sample bank");
        string optionsHash = Convert.ToHexString(SHA256.HashData(read("defaultoptions.bea").Bytes));
        if (recordedOptionsHash is null
            ? !string.Equals(optionsHash, OptionsHash, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(optionsHash, WholeGameOptionsHash, StringComparison.OrdinalIgnoreCase)
            : !string.Equals(optionsHash, recordedOptionsHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(recordedOptionsHash is null
                ? "The recorded startup options do not match either admitted reference."
                : "The recorded startup options do not match the admitted replay.");
        SampleBankMetadata bank = SampleBankMetadata.Read(bankBytes);
        var samples = new HashSet<SampleMetadata>(bank.Entries, ReferenceEqualityComparer.Instance);
        // This is the existing measured single-player admission, not an inference from movement velocity.
        var owners = new HashSet<Type> { typeof(BattleEngine), typeof(GroundVehicle), typeof(Cannon), typeof(Building),
            typeof(SimpleBuilding), typeof(Plane), typeof(Dropship), typeof(Round), typeof(Explosion), typeof(Feature), typeof(Tree),
            typeof(LevelScriptThing), typeof(SafeSide), typeof(SphereTrigger), typeof(Start), typeof(Waypoint), typeof(NormalSquad),
            typeof(RelaxedSquad) };
        return new(new(1, 12, true, false), seeds, (name, _) => bank.Find(name),
            sample => samples.Contains(sample) && SampleBufferFormat.TryCreate(sample.OriginalPcmBytes, 1, out _),
            owner => owner is Thing thing && owners.Contains(owner.GetType()) ? new SoundOwnerSpatialState(thing.Pos, default) : null);
    }

    private static void RequireHash(byte[] bytes, string expected, string name)
    {
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {name} does not match the admitted reference.");
    }
}
