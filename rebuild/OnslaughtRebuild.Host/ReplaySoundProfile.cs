// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
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

    public static VirtualSoundProcessOptions Load(string path, Func<string, (string Name, byte[] Bytes)> read)
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
        if (!string.Equals(optionsHash, OptionsHash, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(optionsHash, WholeGameOptionsHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The recorded startup options do not match either admitted reference.");
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
