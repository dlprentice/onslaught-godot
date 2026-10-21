// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Sound;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's trainer route (the native tutorial pilot's fire3-trainer-missile-dodge tape) against the native port with
/// patches 0029 and 0030 (l100-trainer-port-20261021), every Present to 17300 and every probe field: the event time and
/// processed-event count, the frame time, score, level, game random stream, rand(), and the player's position, life and
/// energy. The route's two Forseti Missiles launch, wiggle, seek, hit and explode, so this pins the straight aim, the
/// wiggle and the seek turn as retail computes them. The replay uses the frozen port process's measured sound-pool profile
/// (its 256 seeds, the English bank, the selected options), as the route's own runs do.
/// </summary>
public sealed class TrainerRouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 17300;

    private const string Dir = "portrefs/l100-trainer-port-20261021";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/fire3-trainer-missile-dodge-20261019.tape", Dir + "/sound-seeds-admission-root-c.json")]
    public void TheTrainerRouteMatchesThePortEveryPresent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: SoundProfile(RetailGolden.File(Dir + "/sound-seeds-admission-root-c.json")!));
        session.Tape = RetailTape.Load(RetailGolden.File(Dir + "/fire3-trainer-missile-dodge-20261019.tape")!);
        (long matched, string? first, int compared) = CompareEveryPresent(session, Dir + "/port-probes-every1.txt", MatchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= MatchedThrough, $"matched through Present {matched}, below {MatchedThrough}; {first}");
        Assert.True(compared >= 9 * (MatchedThrough - 3), $"only {compared} fields compared");
    }

    /// <summary>
    /// Advances the session Present by Present to <paramref name="end"/> (or its first difference), comparing every probe
    /// field the port's every-Present reference has: EVT, EVN, FT, SC, LVL, RNG, CRT, POS and LIFE.
    /// </summary>
    internal static (long Matched, string? First, int Compared) CompareEveryPresent(LevelSession session, string probesFile, long end)
    {
        var port = new Dictionary<(long Present, string Name), string>();
        foreach (string line in File.ReadLines(RetailGolden.File(probesFile)!))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P" && f[2] is not ("ELD" or "ELDEXIT" or "FE"))
            {
                port[(long.Parse(f[1]), f[2])] = f[3];
            }
        }

        static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();
        static string U32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return Hex(b); }
        static string F32(params float[] v) => string.Concat(v.Select(x => U32(BitConverter.SingleToUInt32Bits(x))));
        long matched = 0;
        string? first = null;
        int compared = 0;
        while (session.PresentCount < end && first is null)
        {
            session.AdvanceToNextPresent();
            long p = session.PresentCount;
            var ours = new Dictionary<string, string>
            {
                ["EVT"] = F32(session.Context.Events.Time),
                ["EVN"] = U32(session.Context.Events.TotalEventsProcessed),
                ["FT"] = F32(session.Game.Loop.FrameTime),
                ["SC"] = U32(unchecked((uint)session.Game.Score)),
                ["LVL"] = U32(unchecked((uint)session.Game.CurrentLevel)),
                ["RNG"] = U32(unchecked((uint)session.Context.Random.Value)) + U32(unchecked((uint)session.Context.Random.Seed)),
                ["CRT"] = U32(unchecked((uint)session.Context.Crt.Calls)) + U32(unchecked((uint)session.Context.Crt.HoldRand)),
            };
            if (session.Game.GetPlayer(0)?.BattleEngine is BattleEngine be)
            {
                ours["POS"] = F32(be.Pos.X, be.Pos.Y, be.Pos.Z);
                ours["LIFE"] = F32(be.Life, be.Energy);
            }

            foreach ((string name, string value) in ours)
            {
                if (!port.TryGetValue((p, name), out string? theirs))
                {
                    continue;
                }

                compared++;
                if (theirs != value)
                {
                    first ??= $"Present {p} {name}: port {theirs}, ours {value}";
                }
            }

            if (first is null)
            {
                matched = p;
            }
        }

        return (matched, first, compared);
    }

    /// <summary>
    /// The frozen e55 process's measured sound-pool profile (the route's runs' fourth argument): VirtualSoundBackendProfile
    /// (1, 12, true, false), the 256 measured pool seeds in order, the English bank (164 entries), the selected
    /// defaultoptions.bea, and the 18 owner types whose spatial state is their position with zero sound velocity. These
    /// are measured process inputs, not universal retail device defaults.
    /// </summary>
    internal static VirtualSoundProcessOptions SoundProfile(string path)
    {
        byte[] profileBytes = File.ReadAllBytes(path);
        Assert.Equal("e4b10a4d8b8d0b61140c2e97ac3b1ecfbe92b9caa74f42362bba20925840d009", Convert.ToHexString(SHA256.HashData(profileBytes)).ToLowerInvariant());
        using var json = JsonDocument.Parse(profileBytes);
        JsonElement root = json.RootElement;
        Assert.True(root.GetProperty("admitted").GetBoolean());
        SoundEventSeed[] seeds = root.GetProperty("seed_profile").GetProperty("seeds").EnumerateArray().Select((e, i) =>
        {
            Assert.Equal(i, e.GetProperty("slot").GetInt32());
            byte[] b = Convert.FromHexString(e.GetProperty("position_xyz_raw").GetString()!);
            return new SoundEventSeed(e.GetProperty("channel").GetInt32(), new FVector(BinaryPrimitives.ReadSingleLittleEndian(b),
                BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(8))));
        }).ToArray();
        byte[] bankBytes = GameData.Read(@"data\sounds\sounds_english_pc.xap").Bytes;
        Assert.Equal("658c15e3bab844d65dd3c07c4ac880f16f741c0ea116f48c603449bbd4dda8b7", Convert.ToHexString(SHA256.HashData(bankBytes)).ToLowerInvariant());
        SampleBankMetadata bank = SampleBankMetadata.Read(bankBytes);
        var samples = new HashSet<SampleMetadata>(bank.Entries, ReferenceEqualityComparer.Instance);
        Assert.Equal("b5aaad4ef4f95b2926b1373e5ad3e9cbab8aceef04c6762ce966b9d400f25d7e",
            Convert.ToHexString(SHA256.HashData(GameData.Read("defaultoptions.bea").Bytes)).ToLowerInvariant());
        var owners = new HashSet<Type> { typeof(BattleEngine), typeof(GroundVehicle), typeof(Cannon), typeof(Building),
            typeof(SimpleBuilding), typeof(Plane), typeof(Dropship), typeof(Round), typeof(Explosion), typeof(Feature), typeof(Tree),
            typeof(LevelScriptThing), typeof(SafeSide), typeof(SphereTrigger), typeof(Start), typeof(Waypoint), typeof(NormalSquad),
            typeof(RelaxedSquad) };
        return new(new(1, 12, true, false), seeds, (name, _) => bank.Find(name),
            sample => samples.Contains(sample) && SampleBufferFormat.TryCreate(sample.OriginalPcmBytes, 1, out _),
            owner => owner is Thing thing && owners.Contains(owner.GetType()) ? new SoundOwnerSpatialState(thing.Pos, default) : null);
    }
}
