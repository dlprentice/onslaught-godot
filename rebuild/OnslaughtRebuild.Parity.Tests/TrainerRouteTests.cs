// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Game;
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
    /// field the port's every-Present reference has: EVT, EVN, FT, SC, LVL, RNG, CRT, POS and LIFE. It stops when the level
    /// ends (RunLevel returns), since that makes no Present to sample.
    /// </summary>
    internal static (long Matched, string? First, int Compared) CompareEveryPresent(LevelSession session, string probesFile, long end)
    {
        Dictionary<(long Present, string Name), string> port = ReadPortProbes(probesFile);

        long matched = 0;
        string? first = null;
        int compared = 0;
        while (session.PresentCount < end && first is null && !session.Ended)
        {
            session.AdvanceToNextPresent();
            if (session.Ended)
            {
                // RunLevel returned without another Present (its close fills END_LEVEL_DATA): nothing new to sample.
                break;
            }

            long p = session.PresentCount;
            Dictionary<string, string> ours = Probe(session);

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

    /// <summary>The port's every-Present probe fields by Present and name (without its ELD, ELDEXIT and FE records).</summary>
    internal static Dictionary<(long Present, string Name), string> ReadPortProbes(string probesFile)
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

        return port;
    }

    /// <summary>
    /// The port's probe set at a Present (bea-decomp port/platform/common/replay.cpp, Probe): a deleted random stream or
    /// player reads as zero bytes, as the port's probe of a null pointer does.
    /// </summary>
    internal static Dictionary<string, string> Probe(LevelSession session)
    {
        static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();
        static string U32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return Hex(b); }
        static string F32(params float[] v) => string.Concat(v.Select(x => U32(BitConverter.SingleToUInt32Bits(x))));
        var ours = new Dictionary<string, string>
        {
            ["EVT"] = F32(session.Context.Events.Time),
            ["EVN"] = U32(session.Context.Events.TotalEventsProcessed),
            ["FT"] = F32(session.Game.Loop.FrameTime),
            ["SC"] = U32(unchecked((uint)session.Game.Score)),
            ["LVL"] = U32(unchecked((uint)session.Game.CurrentLevel)),
            ["RNG"] = session.RandomStreamDeleted ? U32(0) + U32(0)
                : U32(unchecked((uint)session.Context.Random.Value)) + U32(unchecked((uint)session.Context.Random.Seed)),
            ["CRT"] = U32(unchecked((uint)session.Context.Crt.Calls)) + U32(unchecked((uint)session.Context.Crt.HoldRand)),
        };
        if (session.PlayersDeleted)
        {
            ours["POS"] = F32(0, 0, 0);
            ours["LIFE"] = F32(0, 0);
        }
        else if (session.Game.GetPlayer(0)?.BattleEngine is BattleEngine be)
        {
            ours["POS"] = F32(be.Pos.X, be.Pos.Y, be.Pos.Z);
            ours["LIFE"] = F32(be.Life, be.Energy);
        }

        return ours;
    }

    /// <summary>
    /// The frozen e55 process's measured sound-pool profile (the route's runs' fourth argument): VirtualSoundBackendProfile
    /// (1, 12, true, false), the 256 measured pool seeds in order, the English bank (164 entries), the selected
    /// defaultoptions.bea, and the 18 owner types whose spatial state is their position with zero sound velocity. These
    /// are measured process inputs, not universal retail device defaults.
    /// </summary>
    internal static VirtualSoundProcessOptions SoundProfile(string path)
    {
        return OnslaughtRebuild.Host.ReplaySoundProfile.Load(path, GameData.Read);
    }
}
