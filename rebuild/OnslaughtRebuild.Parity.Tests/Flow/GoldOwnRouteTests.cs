// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Things;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The whole game on a player's own input (gold-own-20261023: a maintainer's own session tape from boot to the menu's Quit, moved
/// onto Onslaught CE 0.1.2's timing): the boot films skipped, a new career, the goodies, level 100 from the front end with
/// its intro film skipped, the level won, the outro, the debriefing and the career saved, against the port's replay of the
/// same tape (fe-gold-own-port-20261023: the published 0.1.2 package, every Present). Every probe field the port records
/// is compared every Present: EVT, EVN, the front end's FE line while it runs, FT, SC, LVL, RNG, CRT, POS and LIFE.
/// </summary>
public sealed class GoldOwnRouteTests(ITestOutputHelper output)
{
    private const string Dir = "portrefs/fe-gold-own-port-20261023";

    /// <summary>The level-100 routes' measured sound-pool profile (the frozen e55 -level process's; not this process's).</summary>
    private const string Sounds = "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json";

    private sealed class StopRun : Exception;

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/gold-own-20261023.tape", Dir + "/start/defaultoptions.bea",
        Dir + "/start/Linux Test 1.bes", Dir + "/end/BEA 1.bes", Sounds)]
    public void TheGoldOwnRouteMatchesThePortEveryPresent()
    {
        var port = new Dictionary<(long Present, string Name), string>();
        long last = 0;
        foreach (string line in File.ReadLines(RetailGolden.File(Dir + "/port-probes-every1.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P" && f[2] is not ("ELD" or "ELDEXIT"))
            {
                long p = long.Parse(f[1]);
                port[(p, f[2])] = f[3];
                last = Math.Max(last, p);
            }
        }

        byte[] startOptions = File.ReadAllBytes(RetailGolden.File(Dir + "/start/defaultoptions.bea")!);
        (string, byte[]) Read(string path) =>
            string.Equals(path, OnslaughtRebuild.Parity.Career.DefaultOptions.FileName, StringComparison.OrdinalIgnoreCase)
                ? (path, startOptions) : GameData.Read(path);
        var run = new GameRun(Read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id],
            new RetailChainTests.InstalledFilms(), skipFmv: false, LevelSession.VirtualVoiceFor(GameData.Read),
            OnslaughtRebuild.Host.ReplaySoundProfile.Load(RetailGolden.File(Sounds)!, Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Dir + "/gold-own-20261023.tape")!),
        };
        run.FrontEnd.MemoryCard.Folder.Write("Linux Test 1", File.ReadAllBytes(RetailGolden.File(Dir + "/start/Linux Test 1.bes")!));

        long matched = 0;
        string? first = null;
        int compared = 0;
        var context = new List<string>();
        run.OnPresented = () =>
        {
            long p = run.Clock.PresentCount;
            Dictionary<string, string> ours = Probe(run);
            foreach ((string name, string value) in ours)
            {
                if (!port.TryGetValue((p, name), out string? theirs))
                {
                    first ??= $"Present {p} {name}: the port has none, ours {value}";
                    continue;
                }

                compared++;
                if (theirs != value)
                {
                    first ??= $"Present {p} {name}: port {theirs}, ours {value}";
                }
            }

            foreach (string name in new[] { "FE" })
            {
                if (port.ContainsKey((p, name)) && !ours.ContainsKey(name))
                {
                    first ??= $"Present {p} {name}: the port has {port[(p, name)]}, ours none";
                }
            }

            if (first is null)
            {
                matched = p;
            }
            else if (context.Count < 12)
            {
                context.Add($"P{p}: " + string.Join(" ", ours.Select(kv => $"{kv.Key}={kv.Value}{(port.TryGetValue((p, kv.Key), out string? t) && t != kv.Value ? "/port " + t : "")}")));
            }

            if (p >= last || context.Count >= 12)
            {
                throw new StopRun();
            }
        };

        try
        {
            run.Run();
        }
        catch (StopRun)
        {
        }
        catch (Exception e) when (e is NotSupportedException or NotImplementedException or InvalidOperationException)
        {
            first ??= $"Present {run.Clock.PresentCount + 1}: {e.GetType().Name}: {e.Message}";
        }

        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        foreach (string line in context)
        {
            output.WriteLine(line);
        }

        output.WriteLine($"flow: {string.Join(" | ", run.Flow?.Trace ?? [])}");
        Assert.True(first is null, $"matched through Present {matched}; {first}");
        Assert.Equal(last, matched);
        // END_LEVEL_DATA as the port recorded it at its change (the first Present after the level's close).
        var eld = File.ReadLines(RetailGolden.File(Dir + "/port-probes-every1.txt")!).Select(l => l.Split(' '))
            .Where(f => f.Length == 4 && f[0] == "P" && f[2] == "ELD").Select(f => (Present: long.Parse(f[1]), Bytes: Convert.FromHexString(f[3])))
            .ToList();
        Assert.Equal(2, eld.Count);
        output.WriteLine($"END_LEVEL_DATA recorded by the port at Present {eld[1].Present}; the level ended {run.Level!.Game.Quit}");
        Assert.Equal(eld[1].Bytes, run.FrontEnd.EndLevelData.ToBytes());
        Assert.Equal(OnslaughtRebuild.Parity.Clock.QuitType.QuitToFrontEnd, run.Level.Game.Quit);

        // The career saved at the debriefing and the options file written last, byte for byte.
        byte[]? saved = run.FrontEnd.MemoryCard.Folder.Read("BEA 1");
        Assert.NotNull(saved);
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Dir + "/end/BEA 1.bes")!), saved);
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Dir + "/end/defaultoptions.bea")!), run.DefaultOptionsFile);
        output.WriteLine($"BEA 1.bes: {saved!.Length} bytes equal to the port's; defaultoptions.bea: {run.DefaultOptionsFile!.Length} bytes equal");
    }

    /// <summary>
    /// The port's probe set (bea-decomp port/platform/common/replay.cpp, Probe) on the whole run: the front end's line
    /// while CFrontEnd::Run runs; GAME's fields from the level that ran last; a missing random stream or player reads as
    /// zeros.
    /// </summary>
    private static Dictionary<string, string> Probe(GameRun run)
    {
        static string Hex(ReadOnlySpan<byte> b) => Convert.ToHexString(b).ToLowerInvariant();
        static string U32(uint v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, v); return Hex(b); }
        static string F32(params float[] v) => string.Concat(v.Select(x => U32(BitConverter.SingleToUInt32Bits(x))));
        var ours = new Dictionary<string, string>();
        LevelSession? level = run.Level;
        FrontEnd fe = run.FrontEnd;
        bool inFrontEnd = run.Input.Window.FrontEndRunning;
        ours["EVT"] = F32(level is null || inFrontEnd ? fe.Events.Time : level.Context.Events.Time);
        ours["EVN"] = U32(level is null || inFrontEnd ? fe.Events.TotalEventsProcessed : level.Context.Events.TotalEventsProcessed);
        if (inFrontEnd)
        {
            var b = new byte[28];
            BitConverter.TryWriteBytes(b.AsSpan(0), fe.Timing.FrameStep);
            BitConverter.TryWriteBytes(b.AsSpan(4), (int)fe.ActivePage);
            BitConverter.TryWriteBytes(b.AsSpan(8), (int)fe.TransitionFrom);
            BitConverter.TryWriteBytes(b.AsSpan(12), (int)fe.TransitionTo);
            BitConverter.TryWriteBytes(b.AsSpan(16), fe.TransitionCount);
            BitConverter.TryWriteBytes(b.AsSpan(20), fe.TransitionTime);
            BitConverter.TryWriteBytes(b.AsSpan(24), fe.Counter);
            ours["FE"] = Hex(b);
        }

        if (run.LoadingLevel is int loading)
        {
            // A loading-screen Present inside the load (LoadResources's render, after CGame::Init and InitRestartLoop):
            // EVENT_MANAGER started again at time 0 with no events processed, GAME's level set and its random stream
            // made new with seed 123456 (game.cpp 390); the level's own objects are not built yet. GAME's frame time
            // and score stand from the last level, if any.
            ours["EVT"] = F32(0f);
            ours["EVN"] = U32(0);
            ours["FT"] = F32(level?.Game.Loop.FrameTime ?? 0f);
            ours["SC"] = U32(unchecked((uint)(level?.Game.Score ?? 0)));
            ours["LVL"] = U32(unchecked((uint)loading));
            ours["RNG"] = U32(123456) + U32(123456);
            ours["CRT"] = U32(unchecked((uint)run.Crt.MainThread.Calls)) + U32(unchecked((uint)run.Crt.MainThread.HoldRand));
            ours["POS"] = F32(0, 0, 0);
            ours["LIFE"] = F32(0, 0);
            return ours;
        }

        ours["FT"] = F32(level?.Game.Loop.FrameTime ?? 0f);
        ours["SC"] = U32(unchecked((uint)(level?.Game.Score ?? 0)));
        ours["LVL"] = U32(unchecked((uint)(level?.Game.CurrentLevel ?? -1)));
        ours["RNG"] = level is null || level.RandomStreamDeleted ? U32(0) + U32(0)
            : U32(unchecked((uint)level.Context.Random.Value)) + U32(unchecked((uint)level.Context.Random.Seed));
        ours["CRT"] = U32(unchecked((uint)run.Crt.MainThread.Calls)) + U32(unchecked((uint)run.Crt.MainThread.HoldRand));
        if (level is not null && !level.PlayersDeleted && level.Game.GetPlayer(0)?.BattleEngine is BattleEngine be)
        {
            ours["POS"] = F32(be.Pos.X, be.Pos.Y, be.Pos.Z);
            ours["LIFE"] = F32(be.Life, be.Energy);
        }
        else
        {
            ours["POS"] = F32(0, 0, 0);
            ours["LIFE"] = F32(0, 0);
        }

        return ours;
    }
}
