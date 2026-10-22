// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test against the retail goldens: retail BEA.exe recorded under the replay harness (golden-640: virtual time
/// 1/30 s per Present, probes read after each Present returns; two retail processes identical). They are retail
/// recordings kept out of Git, found through ONSLAUGHT_RETAIL_GOLDEN; skipped, visibly, without them or the game data.
/// </summary>
public sealed class RetailGoldenFactAttribute : FactAttribute
{
    public RetailGoldenFactAttribute(params string[] files)
    {
        if (files.FirstOrDefault(file => RetailGolden.File(file) is null) is { } missing)
        {
            Skip = $"retail golden '{missing}' not found (set ONSLAUGHT_RETAIL_GOLDEN, ONSLAUGHT_GOLDEN_VDS for vds/ or ONSLAUGHT_BEOBJ_VDS for beobjvds/)";
        }
        else if (GameData.Root is null)
        {
            Skip = "game installation not found (set ONSLAUGHT_GAME_DIR)";
        }
    }
}

internal static class RetailGolden
{
    /// <summary>
    /// A golden by name under ONSLAUGHT_RETAIL_GOLDEN (C's golden-640), or, for names starting "vds/", under
    /// ONSLAUGHT_GOLDEN_VDS (B's golden-vds: retail with virtual DirectSound status, the audio model the port and the
    /// parity core run), or for "beobjvds/" under ONSLAUGHT_BEOBJ_VDS (B's per-Present virtual-DS run of level 100 with
    /// the battle engine's object).
    /// </summary>
    public static string? File(string name)
    {
        (string prefix, string variable) = name.StartsWith("vds/", StringComparison.Ordinal) ? ("vds/", "ONSLAUGHT_GOLDEN_VDS")
            : name.StartsWith("beobjvds/", StringComparison.Ordinal) ? ("beobjvds/", "ONSLAUGHT_BEOBJ_VDS")
            : name.StartsWith("portcrt/", StringComparison.Ordinal) ? ("portcrt/", "ONSLAUGHT_PORT_CRT")
            : name.StartsWith("portrefs/", StringComparison.Ordinal) ? ("portrefs/", "ONSLAUGHT_PORT_REFS")
            : ("", "ONSLAUGHT_RETAIL_GOLDEN");
        string? root = Environment.GetEnvironmentVariable(variable);
        name = name[prefix.Length..];
        if (string.IsNullOrEmpty(root))
        {
            return null;
        }

        string path = Path.Combine(root, name);
        return System.IO.File.Exists(path) ? path : null;
    }
}

public sealed class RetailGoldenTests(ITestOutputHelper output)
{
    /// <summary>C's level-100 tape (golden-640/tapes), which both per-Present goldens replay from Present 3300.</summary>
    private const string Level100TapeFile = "tapes/level100-keys-mouse-10000f.tape";

    /// <summary>
    /// B's per-Present virtual-DS run of level 100 with the tape (l100-beobj-vds-6000: the battle engine's object and
    /// EVT, FT and RNG after every Present to <see cref="BeObjLastPresent"/>; ONSLAUGHT_BEOBJ_VDS names its directory).
    /// </summary>
    private const string BeObjFile = "beobjvds/l100.beobj-vds6000-a.probes";

    private const int BeObjLastPresent = 6000;

    /// <summary>The tape; the tests that replay it require it, so a missing tape skips them rather than diverging.</summary>
    private static RetailTape Level100Tape() => RetailTape.Load(RetailGolden.File(Level100TapeFile)!);

    /// <summary>The last Present the Present-by-Present run matches retail's clock and random stream to (never lowered).</summary>
    private const int PresentByPresentFloor = 6000;

    /// <summary>The last sampled Present (every 50) the run matches the virtual-DS golden to (never lowered).</summary>
    private const int VirtualDsFloor = 10000;

    /// <summary>The Present through which the main thread's rand() count and state equal the port's at every Present (never lowered).</summary>
    private const int PortCrtMatchedThrough = 10000;

    /// <summary>The Present through which the firing tape's run equals the port's at every Present (never lowered).</summary>
    private const int FiringTapeMatchedThrough = 6000;

    /// <summary>The Present through which the static-target tape's run equals the port's at every Present (never lowered).</summary>
    private const int TargetTapeMatchedThrough = 8000;

    /// <summary>
    /// The Present through which the tank-kill tape matches the port (never lowered). The reference is the port with its
    /// call arguments evaluated in VC6's order (bea-decomp 12d023ca): before that, clang drew
    /// FVector(RandomScaled, RandomScaled, RandomScaled) X first where VC6 draws Z first (CPDEmitter::Unknown10, 004c1a90),
    /// and the tank's shrapnel flew differently from P6016.
    /// </summary>
    private const int TankKillTapeMatchedThrough = 8000;

    /// <summary>The Present through which the player-recorded level 100 (a loss, a retry, a quit) matches the port (never lowered).</summary>
    private const int DavidTapeMatchedThrough = 8882;

    /// <summary>The Presents through which the pause and restart tapes match the port (never lowered).</summary>
    private const int PauseQuitMatchedThrough = 3861;

    /// <summary>The Presents through which the pause menu's controls and display lists match the port (never lowered).</summary>
    private const int PauseControlsMatchedThrough = 4000;
    private const int PauseVideoMatchedThrough = 4100;
    private const int PauseRetryMatchedThrough = 5200;
    private const int LoseRetryMatchedThrough = 9000;

    /// <summary>The Presents over which every compared battle-engine field matches retail's (never lowered).</summary>
    private const int BattleEngineMatchedFloor = 5998;

    /// <summary>The Present through which every compared battle-engine field equals retail's (never lowered).</summary>
    private const int BattleEngineMatchedThrough = 6000;

    /// <summary>
    /// Level 100 run Present by Present against the source port's main-thread rand() (crt-per-present.txt: after each
    /// Present, the C runtime's call count and holdrand, from bea-decomp's port under the same tape and virtual clock;
    /// ONSLAUGHT_PORT_CRT names its directory). Particles, snow, sounds, the cockpit shake and the simulation share
    /// that stream, so its count and state at each Present check every draw's existence and order. Both are compared
    /// as the Present returns; they must equal the port's at every Present from 3 through
    /// <see cref="PortCrtMatchedThrough"/>, and the first difference is reported. Presents 1 and 2 are the boot's, before
    /// the port loads the level; the core's session loads it first (no draw falls between, so the order is the same).
    /// </summary>
    [RetailGoldenFact("portcrt/crt-per-present.txt", Level100TapeFile)]
    public void Level100MainThreadRandMatchesThePortEveryPresent()
    {
        var port = new Dictionary<long, (uint Count, uint Hold)>();
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File("portcrt/crt-per-present.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length >= 4 && f[0] == "P" && f[2] == "CRT")
            {
                byte[] b = Convert.FromHexString(f[3]);
                port[long.Parse(f[1], CultureInfo.InvariantCulture)] = (BitConverter.ToUInt32(b, 0), BitConverter.ToUInt32(b, 4));
            }
        }

        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Level100Tape();
        long last = port.Keys.Max();
        long matched = 0;
        string? first = null;
        session.Clock.Presented = () =>
        {
            long p = session.Clock.PresentCount;
            if (p < 3 || first is not null || !port.TryGetValue(p, out var want))
            {
                return;
            }

            var crt = session.Context.Crt;
            if ((uint)crt.Calls == want.Count && crt.HoldRand == want.Hold)
            {
                matched = p;
            }
            else
            {
                first = $"Present {p}: count {want.Count}/{crt.Calls}, holdrand {want.Hold:x8}/{crt.HoldRand:x8} (port/ours)";
            }
        };
        string stopped = $"ran to Present {last}";
        while (session.PresentCount < last && first is null)
        {
            try
            {
                session.AdvanceToNextPresent();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped after Present {session.PresentCount}: {e.Message}";
                break;
            }
        }

        output.WriteLine($"{stopped}; rand() matches the port through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= PortCrtMatchedThrough, $"matched through Present {matched}, below {PortCrtMatchedThrough}");
    }

    /// <summary>
    /// Level 100 on the port's firing tape (tapes/level100-fire1-6000f.tape, recorded by the port's autopilot: C's tape
    /// to Present 3300, then to Target Zone 1 and the firing range, where the script enables the Pulse Cannon Pod, and
    /// three bursts from Present 5653) against the port's every-Present reference (l100-fire1-port: the main-thread
    /// rand() count and holdrand, and the probes EVT, FT, RNG, POS and LIFE). ONSLAUGHT_PORT_REFS names the shared
    /// directory holding both. Every Present from 3 must match through <see cref="FiringTapeMatchedThrough"/>; the
    /// first difference is reported. It covers the shot, the round in flight and its explosion.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-fire1-6000f.tape", "portrefs/l100-fire1-port/crt-per-present.txt",
        "portrefs/l100-fire1-port/port-probes-every1.txt")]
    public void Level100FiringTapeMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-fire1-6000f.tape", "portrefs/l100-fire1-port", 6000);
        Assert.True(matched >= FiringTapeMatchedThrough, $"matched through Present {matched}, below {FiringTapeMatchedThrough}");
    }

    /// <summary>
    /// Level 100 on the port's static-target tape (tapes/level100-fire2-8000f.tape, the port's autopilot: the firing range,
    /// then bursts at the first target tank that fly over it into the warehouse, then the second tank and the warehouse)
    /// against the port's every-Present reference of a replay (l100-fire2-port), as the firing tape's test. It covers a
    /// round's damage to a segmented building, the segments' rubble and pickup explosions. Floor:
    /// <see cref="TargetTapeMatchedThrough"/>.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-fire2-8000f.tape", "portrefs/l100-fire2-port/crt-per-present.txt",
        "portrefs/l100-fire2-port/port-probes-every1.txt")]
    public void Level100TargetTapeMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-fire2-8000f.tape", "portrefs/l100-fire2-port", 8000);
        Assert.True(matched >= TargetTapeMatchedThrough, $"matched through Present {matched}, below {TargetTapeMatchedThrough}");
    }

    /// <summary>
    /// Level 100 on the port's tank-kill tape (tapes/level100-fire3-8000f.tape, the port's autopilot: the firing range,
    /// bursts that kill the first target tank at P6016, then bursts at the second tank and the warehouse, which dies at
    /// P6961) against the port's every-Present reference of a replay (l100-fire3-port), as the firing tape's test. It
    /// covers a ground vehicle's death, its explosion's damage to a segmented building and the wreck's particles.
    /// Floor: <see cref="TankKillTapeMatchedThrough"/>.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-fire3-8000f.tape", "portrefs/l100-fire3-port/crt-per-present.txt",
        "portrefs/l100-fire3-port/port-probes-every1.txt")]
    public void Level100TankKillTapeMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-fire3-8000f.tape", "portrefs/l100-fire3-port", 8000);
        Assert.True(matched >= TankKillTapeMatchedThrough, $"matched through Present {matched}, below {TankKillTapeMatchedThrough}");
    }

    /// <summary>
    /// Level 100 lost on the port's lose tape (tapes/level100-lose2-10000f.tape, the port's autopilot: the firing range,
    /// 70 bursts into the big friendly Facilities, so the script's 21st "Hit Friendly Building" loses the level; then
    /// the level-end menu's Return, which with the tape's non-extended Down still on Restart restarts it). The level
    /// must end on the restart, and END_LEVEL_DATA as RunLevel's close fills it must equal the port's bytes
    /// (port-probes-every50-with-ELD.txt: its ELD line after the close) exactly.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-lose2-10000f.tape", "portrefs/l100-lose2-port/port-probes-every50-with-ELD.txt")]
    public void Level100LoseTapeEndsTheLevelWithThePortsEndLevelData() =>
        LoseTapeEndsWithThePortsEndLevelData("portrefs/tapes/level100-lose2-10000f.tape",
            "portrefs/l100-lose2-port/port-probes-every50-with-ELD.txt", "ELD", QuitType.RestartLevel, 8674);

    /// <summary>
    /// As the lose tape, with the level-end menu's Down (the arrow, an extended key) before Return (tapes/level100-lose3-quit.tape):
    /// Quit, so the level ends to the front end after Present 7833, the port's last (its -level run then exits), and
    /// END_LEVEL_DATA equals the port's on the way out (its ELDEXIT line).
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-lose3-quit.tape", "portrefs/l100-lose3-port/port-probes-every50-with-ELD.txt")]
    public void Level100LoseThenQuitTapeEndsWithThePortsEndLevelData() =>
        LoseTapeEndsWithThePortsEndLevelData("portrefs/tapes/level100-lose3-quit.tape",
            "portrefs/l100-lose3-port/port-probes-every50-with-ELD.txt", "ELDEXIT", QuitType.UserQuitToFrontEnd, 7833);

    private void LoseTapeEndsWithThePortsEndLevelData(string tape, string reference, string label, QuitType quit, long endedAfter)
    {
        byte[]? port = null;
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File(reference)!))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P" && f[2] == label)
            {
                port = Convert.FromHexString(f[3]);
            }
        }

        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = RetailTape.Load(RetailGolden.File(tape)!);
        while (!session.Ended && session.PresentCount < 10000)
        {
            session.AdvanceToNextPresent();
        }

        output.WriteLine($"ended after Present {session.PresentCount}: quit {session.Game.Quit}, lost reason {session.EndLevelData.LevelLostReason:x8}");
        Assert.True(session.Ended);
        Assert.Equal(quit, session.Game.Quit);
        Assert.Equal(endedAfter, session.PresentCount);
        byte[] ours = session.EndLevelData.ToBytes();
        Assert.Equal(EndLevelData.ByteSize, port!.Length);
        int first = Enumerable.Range(0, ours.Length).FirstOrDefault(i => ours[i] != port[i], -1);
        Assert.True(first < 0, $"END_LEVEL_DATA differs first at byte 0x{first:x} (port {port[Math.Max(first, 0)]:x2}, ours {ours[Math.Max(first, 0)]:x2})");
    }

    /// <summary>
    /// Level 100 as a player played it (tapes/level100-david-win1-loss-retry.tape, a human recording: jumps and landing jets,
    /// fire, a loss by friendly fire, the level-end menu's Retry, a quit) against the port's every-Present reference of a
    /// replay (l100-david-win1-port: the port with patch 0022, equal to his live recording on every probe), as the firing
    /// tape's test. Floor: <see cref="DavidTapeMatchedThrough"/>.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-david-win1-loss-retry.tape", "portrefs/l100-david-win1-port/crt-per-present.txt",
        "portrefs/l100-david-win1-port/port-probes-every1.txt")]
    public void Level100DavidsRecordingMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-david-win1-loss-retry.tape", "portrefs/l100-david-win1-port", 9000);
        Assert.True(matched >= DavidTapeMatchedThrough, $"matched through Present {matched}, below {DavidTapeMatchedThrough}");
    }

    /// <summary>
    /// Level 100 paused at P3400, the pause menu's Quit and its confirmation's Yes (tapes/level100-pausequit.tape) against
    /// the port's every-Present reference (l100-pausequit-port): the level quits to the front end after P3861.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-pausequit.tape", "portrefs/l100-pausequit-port/crt-per-present.txt",
        "portrefs/l100-pausequit-port/port-probes-every1.txt")]
    public void Level100PauseQuitMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-pausequit.tape", "portrefs/l100-pausequit-port", 4000);
        Assert.True(matched >= PauseQuitMatchedThrough, $"matched through Present {matched}, below {PauseQuitMatchedThrough}");
    }

    /// <summary>
    /// Level 100 paused at P3400 (tapes/level100-pausecontrols.tape): Controller Options, the mouse sensitivity right and
    /// left, down and up the list, Escape back and Escape to resume, against the port (l100-pausecontrols-port, to 4000).
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-pausecontrols.tape", "portrefs/l100-pausecontrols-port/crt-per-present.txt",
        "portrefs/l100-pausecontrols-port/port-probes-every1.txt")]
    public void Level100PauseControlsMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-pausecontrols.tape", "portrefs/l100-pausecontrols-port", 4000);
        Assert.True(matched >= PauseControlsMatchedThrough, $"matched through Present {matched}, below {PauseControlsMatchedThrough}");
    }

    /// <summary>
    /// Level 100 paused at P3400 (tapes/level100-pausevideo.tape): Video Options, the overall detail level's list opened,
    /// moved in and closed, Escape back and Escape to resume, against the port (l100-pausevideo-port, to 4100).
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-pausevideo.tape", "portrefs/l100-pausevideo-port/crt-per-present.txt",
        "portrefs/l100-pausevideo-port/port-probes-every1.txt")]
    public void Level100PauseVideoMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-pausevideo.tape", "portrefs/l100-pausevideo-port", 4100);
        Assert.True(matched >= PauseVideoMatchedThrough, $"matched through Present {matched}, below {PauseVideoMatchedThrough}");
    }

    /// <summary>
    /// Level 100 paused at P3400, the pause menu's Retry and Yes (tapes/level100-pauseretry.tape) against the port
    /// (l100-pauseretry-port): the level restarts after P3863 and runs on to 5200.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-pauseretry.tape", "portrefs/l100-pauseretry-port/crt-per-present.txt",
        "portrefs/l100-pauseretry-port/port-probes-every1.txt")]
    public void Level100PauseRetryMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-pauseretry.tape", "portrefs/l100-pauseretry-port", 5200);
        Assert.True(matched >= PauseRetryMatchedThrough, $"matched through Present {matched}, below {PauseRetryMatchedThrough}");
    }

    /// <summary>
    /// The lose tape with its level-end menu's Retry (tapes/level100-lose2-10000f.tape) against the port's every-Present
    /// replay to 9000 (l100-lose2-restart-port): the loss, the restart after P8674 and the reloaded level.
    /// </summary>
    [RetailGoldenFact("portrefs/tapes/level100-lose2-10000f.tape", "portrefs/l100-lose2-restart-port/crt-per-present.txt",
        "portrefs/l100-lose2-restart-port/port-probes-every1.txt")]
    public void Level100LoseThenRetryMatchesThePortEveryPresent()
    {
        long matched = PortTapeMatchedThrough("portrefs/tapes/level100-lose2-10000f.tape", "portrefs/l100-lose2-restart-port", 9000);
        Assert.True(matched >= LoseRetryMatchedThrough, $"matched through Present {matched}, below {LoseRetryMatchedThrough}");
    }

    private long PortTapeMatchedThrough(string tape, string dir, long last)
    {
        var port = new Dictionary<long, (uint Count, uint Hold)>();
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File(dir + "/crt-per-present.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length >= 4 && f[0] == "P" && f[2] == "CRT")
            {
                byte[] b = Convert.FromHexString(f[3]);
                port[long.Parse(f[1], CultureInfo.InvariantCulture)] = (BitConverter.ToUInt32(b, 0), BitConverter.ToUInt32(b, 4));
            }
        }

        var probes = new Dictionary<(long Present, string Name), string>();
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File(dir + "/port-probes-every1.txt")!))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P" && f[2] is "EVT" or "FT" or "RNG" or "POS" or "LIFE")
            {
                probes[(long.Parse(f[1], CultureInfo.InvariantCulture), f[2])] = f[3];
            }
        }

        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = RetailTape.Load(RetailGolden.File(tape)!);
        static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
        long matched = 0;
        string? first = null;
        session.Clock.Presented = () =>
        {
            long p = session.Clock.PresentCount;
            if (p < 3 || first is not null || !port.TryGetValue(p, out var want))
            {
                return;
            }

            var crt = session.Context.Crt;
            if ((uint)crt.Calls != want.Count || crt.HoldRand != want.Hold)
            {
                first = $"Present {p}: rand() count {want.Count}/{crt.Calls}, holdrand {want.Hold:x8}/{crt.HoldRand:x8} (port/ours)";
            }
        };
        string stopped = $"ran to Present {last}";
        while (session.PresentCount < last && first is null)
        {
            try
            {
                if (session.Ended)
                {
                    if (session.Game.Quit != OnslaughtRebuild.Parity.Clock.QuitType.RestartLevel)
                    {
                        stopped = $"the level ended after Present {session.PresentCount} ({session.Game.Quit})";
                        break;
                    }

                    session = session.Restart();
                }

                long before = session.PresentCount;
                session.AdvanceToNextPresent();
                if (session.PresentCount == before)
                {
                    // The level ended without a further Present (its last update set the quit): nothing new to compare.
                    continue;
                }
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped after Present {session.PresentCount}: {e.Message}";
                break;
            }

            long p = session.PresentCount;
            if (first is not null || !probes.ContainsKey((p, "EVT")))
            {
                continue;
            }

            var ours = new Dictionary<string, string>
            {
                ["EVT"] = Hex(BitConverter.GetBytes(session.Context.Events.Time)),
                ["FT"] = Hex(BitConverter.GetBytes(session.Game.Loop.FrameTime)),
            };
            if (session.Game.GetPlayer(0)?.BattleEngine is Unit be)
            {
                ours["POS"] = Hex([.. BitConverter.GetBytes(be.Pos.X), .. BitConverter.GetBytes(be.Pos.Y), .. BitConverter.GetBytes(be.Pos.Z)]);
                ours["LIFE"] = Hex([.. BitConverter.GetBytes(be.Life), .. BitConverter.GetBytes(be.Energy)]);
            }

            string? bad = ours.Where(kv => probes.TryGetValue((p, kv.Key), out string? g) && g != kv.Value)
                .Select(kv => $"{kv.Key} {probes[(p, kv.Key)]}/{kv.Value}").FirstOrDefault();
            string ourRng = Hex(BitConverter.GetBytes(session.Context.Random.Value));
            if (bad is null && probes.TryGetValue((p, "RNG"), out string? rng) && !rng.StartsWith(ourRng, StringComparison.Ordinal))
            {
                bad = $"RNG {rng}/{ourRng}";
            }

            if (bad is null)
            {
                matched = p;
            }
            else
            {
                first = $"Present {p}: {bad} (port/ours)";
            }
        }

        output.WriteLine($"{stopped}; {tape} matches the port through Present {matched}; first difference: {first ?? "none"}");
        return matched;
    }

    /// <summary>
    /// Level 100 run Present by Present against B's virtual-DS golden (level100.vds-a.probes: retail with virtual
    /// DirectSound status, the audio model the port and the core run; probes EVT, FT, RNG, SC, LVL and the battle engine's
    /// POS and LIFE every 50 Presents). The
    /// sampled Presents must match through <see cref="VirtualDsFloor"/>; the first difference after it is reported. The
    /// golden replays C's tape from Present 3300 and runs to Present 10000.
    /// </summary>
    [RetailGoldenFact("vds/level100.vds-a.probes", Level100TapeFile)]
    public void Level100RunMatchesTheVirtualDsGolden()
    {
        var golden = new Dictionary<(long Present, string Name), string>();
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File("vds/level100.vds-a.probes")!))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P")
            {
                golden[(long.Parse(f[1]), f[2])] = f[3];
            }
        }

        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Level100Tape();
        long last = golden.Keys.Max(k => k.Present);
        static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
        string? first = null;
        long matched = 0;
        string stopped = $"ran to Present {last}";
        while (session.PresentCount < last)
        {
            try
            {
                session.AdvanceToNextPresent();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped after Present {session.PresentCount}: {e.Message}";
                break;
            }

            long p = session.PresentCount;
            if (first is not null || !golden.TryGetValue((p, "EVT"), out string? evt))
            {
                continue;
            }

            string ourEvt = Hex(BitConverter.GetBytes(session.Context.Events.Time));
            string ourFt = Hex(BitConverter.GetBytes(session.Game.Loop.FrameTime));
            string ourRng = Hex(BitConverter.GetBytes(session.Context.Random.Value));
            var ours = new Dictionary<string, string>
            {
                ["SC"] = Hex(BitConverter.GetBytes(session.Game.Score)),
                ["LVL"] = Hex(BitConverter.GetBytes(session.Game.CurrentLevel)),
            };
            if (session.Game.GetPlayer(0)?.BattleEngine is OnslaughtRebuild.Parity.Things.Unit be)
            {
                ours["POS"] = Hex([.. BitConverter.GetBytes(be.Pos.X), .. BitConverter.GetBytes(be.Pos.Y), .. BitConverter.GetBytes(be.Pos.Z)]);
                ours["LIFE"] = Hex([.. BitConverter.GetBytes(be.Life), .. BitConverter.GetBytes(be.Energy)]);
            }

            string? other = ours.Where(kv => golden.TryGetValue((p, kv.Key), out string? g) && g != kv.Value)
                .Select(kv => $"{kv.Key} {golden[(p, kv.Key)]}/{kv.Value}").FirstOrDefault();
            if (evt == ourEvt && golden[(p, "FT")] == ourFt && golden[(p, "RNG")].StartsWith(ourRng, StringComparison.Ordinal)
                && other is null)
            {
                matched = p;
            }
            else
            {
                first = $"Present {p}: EVT {evt}/{ourEvt}, FT {golden[(p, "FT")]}/{ourFt}, RNG {golden[(p, "RNG")]}/{ourRng}"
                    + $"{(other is null ? "" : ", " + other)} (retail/ours)";
            }
        }

        output.WriteLine($"{stopped}; sampled Presents match through {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= VirtualDsFloor, $"matched through Present {matched}, below {VirtualDsFloor}");
    }

    /// <summary>
    /// Level 100 run Present by Present, as the host draws it (LevelSession.AdvanceToNextPresent: the harness's level
    /// start, then CGame::MainLoop with its Presents), against retail's probes at the same Present number: the event
    /// time (EVT), mFrameTime (FT) and the random stream's value (RNG). Present numbers line up without help from the
    /// recording, so the frames the host renders are retail's.
    /// </summary>
    [RetailGoldenFact(BeObjFile, Level100TapeFile)]
    public void Level100RunPresentByPresentMatchesRetailsClockAndRandomStream()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Level100Tape();
        var ours = new Dictionary<long, (uint Evt, uint Ft, int Rng)>();
        string stopped = "ran to retail's last probe";
        while (session.PresentCount < BeObjLastPresent)
        {
            try
            {
                session.AdvanceToNextPresent();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped after Present {session.PresentCount}: {e.Message}";
                break;
            }

            ours[session.PresentCount] = (BitConverter.SingleToUInt32Bits(session.Context.Events.Time),
                BitConverter.SingleToUInt32Bits(session.Game.Loop.FrameTime), session.Context.Random.Value);
        }

        int compared = 0;
        string? first = null;
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File(BeObjFile)!))
        {
            string[] f = line.Split(' ');
            if (f.Length != 4 || f[0] != "P" || f[2] is not ("EVT" or "FT" or "RNG") || f[3] == "null")
            {
                continue;
            }

            long present = long.Parse(f[1]);
            if (!ours.TryGetValue(present, out (uint Evt, uint Ft, int Rng) mine))
            {
                // Presents 1 and 2 come before the level (the first advance runs to Present 3).
                if (present < ours.Keys.Min())
                {
                    continue;
                }

                break;
            }

            byte[] bytes = Convert.FromHexString(f[3]);
            (long retail, long value) = f[2] switch
            {
                "EVT" => (BitConverter.ToUInt32(bytes), mine.Evt),
                "FT" => (BitConverter.ToUInt32(bytes), mine.Ft),
                _ => ((long)BitConverter.ToInt32(bytes, 0), (long)mine.Rng),
            };
            if (retail != value)
            {
                first = $"Present {present} {f[2]}: retail {retail:x}, ours {value:x}";
                break;
            }

            if (f[2] == "RNG")
            {
                compared = (int)present;
            }
        }

        output.WriteLine($"{stopped}; Presents {ours.Keys.Min()}..{compared} match; first difference: {first ?? "none"}");
        foreach ((string path, int size, long played, long stopped2) in ((VirtualVoice)ScriptRuntime.Of(session.Context).MessageBox!.Voice).Trace)
        {
            output.WriteLine($"voice {path} size {size:x} played at block {played}, found stopped at block {stopped2}");
        }
        Assert.True(compared >= PresentByPresentFloor, $"matched to Present {compared}, below {PresentByPresentFloor}");
    }

    private static uint[] Words(params float[] values) => [.. values.Select(BitConverter.SingleToUInt32Bits)];

    private static uint[] Words(FVector v) => Words(v.X, v.Y, v.Z);

    private static uint[] Words(EulerAngles e) => Words(e.Yaw, e.Pitch, e.Roll);

    /// <summary>
    /// The battle engine's fields the parity core models, at their offsets in retail's CBattleEngine (1,596 bytes; the
    /// layout from the decomp's headers as clang lays them out for the native port, whose size equals the dump's).
    /// </summary>
    private static readonly (string Name, int Offset, Func<BattleEngine, uint[]> Ours)[] BattleEngineFields =
    [
        ("mPos", 0x1c, b => Words(b.Pos)),
        ("mVelocity", 0x7c, b => Words(b.Velocity)),
        ("mOldPos", 0x8c, b => Words(b.OldPos)),
        ("mLastTimeOnGround", 0xcc, b => Words(b.LastTimeOnGround)),
        ("mLastTimeInWater", 0xd0, b => Words(b.LastTimeInWater)),
        ("mLastTimeOnObject", 0xd4, b => Words(b.LastTimeOnObject)),
        ("mLastMoveTime", 0xd8, b => Words(b.LastMoveTime)),
        ("mDoFullMoveCount", 0xdc, b => [(uint)b.DoFullMoveCount]),
        ("mLife", 0xf8, b => Words(b.Life)),
        ("mEnergy", 0xfc, b => Words(b.Energy)),
        ("mCurrentOrientation", 0x114, b => Words(b.CurrentOrientation)),
        ("mUnknown120", 0x120, b => Words(b.Unknown120)),
        ("mUnknown12C", 0x12c, b => Words(b.Unknown12C)),
        ("mZoom", 0x2c8, b => Words(b.Zoom)),
        ("mFlags (low 16 bits)", 44, b => [(uint)(ushort)b.Flags]),
        ("mOrientation.Row0", 60, b => Words(b.Orientation.Row0)),
        ("mOrientation.Row1", 76, b => Words(b.Orientation.Row1)),
        ("mOrientation.Row2", 92, b => Words(b.Orientation.Row2)),
        ("mShields", 256, b => Words(b.Shields)),
        ("mState", 608, b => [(uint)StateNumber(b.State)]),
        ("mYawvel/Rollvel/Pitchvel", 632, b => Words(b.Yawvel, b.Rollvel, b.Pitchvel)),
        ("mDesiredZoom/OldZoom", 716, b => Words(b.DesiredZoom, b.OldZoom)),
        ("mOldLife..mZoomOutTime", 728, b => Words(b.OldLife, b.OldEnergy, b.LowArmourStartTime, b.LowEnergyStartTime, b.DangerStartTime, b.ZoomOutTime)),
        ("mTakeOffTime/AugValue", 756, b => Words(b.TakeOffTime, b.AugValue)),
        ("mNotMovingCount", 784, b => [(uint)b.NotMovingCount]),
        ("mInSafeCollisionPlace", 788, b => [b.InSafeCollisionPlace ? 1u : 0u]),
        ("mSafePosTime", 1112, b => Words(b.SafePosTime)),
        ("mCurrentSafeTry/Cloaked", 1192, b => [(uint)b.CurrentSafeTry, b.Cloaked ? 1u : 0u]),
        ("mShakes", 1208, b => Words(b.YawShake, b.PitchShake, b.RollShake, b.ShakeR)),
        ("mWlcr.mDistToImpact", 1244, b => Words(b.Wlcr.DistToImpact)),
        ("mDesired/AutoAimYawOffset", 1252, b => Words(b.DesiredAutoAimYawOffset, b.AutoAimYawOffset)),
        ("mDesired/AutoAimPitchOffset", 1264, b => Words(b.DesiredAutoAimPitchOffset, b.AutoAimPitchOffset)),
        ("mPlayed*Sound", 1300, b => Words(b.PlayedIncomingWarheadSound, b.PlayedAmmunitionDepletedSound, b.PlayedWeaponOverheatedSound)),
        ("mStoreValue", 1324, b => Words(b.StoreValue)),
        ("mPoweredUp", 1408, b => [b.PoweredUp ? 1u : 0u]),
        ("mOldEulerAngles", 1424, b => Words(b.OldEulerAngles)),
        ("mStealth/DesiredStealth", 1496, b => Words(b.Stealth, b.DesiredStealth)),
        ("mVibration..WeaponOverheatedTime", 1540, b => Words(b.Vibration, b.AmmoDepletedTime, b.WeaponOverheatedTime)),
        ("mEngineState/Old/ThrustersOn", 1584, b => [(uint)b.EngineState, (uint)b.OldEngineState, b.ThrustersOn ? 1u : 0u]),
    ];

    /// <summary>EBattleEngineState's values: the C# enum keeps retail's order.</summary>
    private static int StateNumber(BattleEngineState state) => (int)state;

    /// <summary>CBattleEngineWalkerPart's fields (76 bytes, BEW), as the parity core models them.</summary>
    private static readonly (string Name, int Offset, Func<BattleEngine, uint[]> Ours)[] WalkerFields =
    [
        ("mCurrentWeapon", 16, b => [(uint)b.WalkerPart!.CurrentWeapon]),
        ("mShieldsRecharging", 20, b => [b.WalkerPart!.ShieldsRecharging ? 1u : 0u]),
        ("mWalkCycle/OldWalkCycle", 36, b => Words(b.WalkerPart!.WalkCycle, b.WalkerPart.OldWalkCycle)),
        ("mLastStartHard*Time", 44, b => Words(b.WalkerPart!.LastStartHardRightTime, b.WalkerPart.LastStartHardLeftTime, b.WalkerPart.LastStartHardForwardTime, b.WalkerPart.LastStartHardBackwardTime)),
        ("mLastMoveX/YVal", 60, b => Words(b.WalkerPart!.LastMoveXVal, b.WalkerPart.LastMoveYVal)),
        ("mDoingDashCount", 68, b => [(uint)b.WalkerPart!.DoingDashCount]),
    ];

    /// <summary>
    /// The player's battle engine against retail's object at every Present (<see cref="BeObjFile"/>: BEO
    /// is the whole CBattleEngine, read after each Present), field by field, each retail Present matched to our state
    /// after the update that reached its event time. Reports each field's first difference; asserts the fields matched
    /// so far stay matched through the Presents our run reaches.
    /// </summary>
    [RetailGoldenFact(BeObjFile, Level100TapeFile)]
    public void Level100sBattleEngineFollowsRetailsPerPresentObject()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Level100Tape();
        ThingContext context = session.Context;
        var engine = (BattleEngine)session.Game.GetPlayer(0)!.BattleEngine!;
        var tables = new[] { ("BEO", BattleEngineFields), ("BEW", WalkerFields) };
        var states = new Dictionary<uint, (int Update, uint[][] Fields)>();
        void Record(int u) => states[BitConverter.SingleToUInt32Bits(context.Events.Time)] =
            (u, [.. tables.SelectMany(t => t.Item2).Select(f => f.Ours(engine))]);
        string stopped = "ran to retail's last probe";
        while (session.PresentCount < BeObjLastPresent)
        {
            try
            {
                session.AdvanceToNextPresent();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped after Present {session.PresentCount}: {e.Message}";
                break;
            }

            Record((int)session.Game.UpdateFrameNumber);
        }

        var all = tables.SelectMany(t => t.Item2.Select(f => (Probe: t.Item1, f.Name, f.Offset))).ToArray();
        var firstDifference = new string?[all.Length];
        int firstBad = int.MaxValue;
        int compared = 0;

        // Each Present's lines start with its BEO, BEW and BEJ and give its EVT after them: read the times first.
        string path = RetailGolden.File(BeObjFile)!;
        var times = new Dictionary<int, uint>();
        foreach (string line in System.IO.File.ReadLines(path))
        {
            string[] f = line.Split(' ');
            if (f.Length == 4 && f[0] == "P" && f[2] == "EVT" && f[3] != "null")
            {
                times[int.Parse(f[1])] = BitConverter.ToUInt32(Convert.FromHexString(f[3]));
            }
        }

        var counted = new HashSet<int>();
        foreach (string line in System.IO.File.ReadLines(path))
        {
            string[] f = line.Split(' ');
            if (f.Length != 4 || f[0] != "P" || f[2] is not ("BEO" or "BEW") || f[3] == "null")
            {
                continue;
            }

            int present = int.Parse(f[1]);
            if (!times.TryGetValue(present, out uint evt) || !states.TryGetValue(evt, out (int Update, uint[][] Fields) ours))
            {
                stopped += $"; retail's Present {present} is past our run";
                break;
            }

            byte[] retail = Convert.FromHexString(f[3]);
            for (int k = 0; k < all.Length; k++)
            {
                if (all[k].Probe != f[2] || firstDifference[k] is not null)
                {
                    continue;
                }

                uint[] mine = ours.Fields[k];
                uint[] theirs = [.. Enumerable.Range(0, mine.Length).Select(j => BitConverter.ToUInt32(retail, all[k].Offset + (4 * j)))];
                if (all[k].Name.StartsWith("mFlags", StringComparison.Ordinal))
                {
                    theirs[0] &= 0xffff;
                }
                if (!mine.SequenceEqual(theirs))
                {
                    firstBad = Math.Min(firstBad, present);
                    firstDifference[k] = $"Present {present} (U{ours.Update}): retail {string.Join(' ', theirs.Select(x => x.ToString("x8")))}, ours {string.Join(' ', mine.Select(x => x.ToString("x8")))}";
                }
            }

            if (counted.Add(present))
            {
                compared++;
            }
        }

        output.WriteLine($"{stopped}; {compared} Presents compared");
        for (int k = 0; k < all.Length; k++)
        {
            output.WriteLine($"{all[k].Probe} {all[k].Name,-34} {firstDifference[k] ?? "matches throughout"}");
        }

        // Every field matches through the floor's Present (differences after it are reported, not gated).
        Assert.True(compared >= BattleEngineMatchedFloor, $"{compared} Presents compared, below {BattleEngineMatchedFloor}");
        Assert.True(firstBad > BattleEngineMatchedThrough, $"a field differs at Present {firstBad}, at or before {BattleEngineMatchedThrough}");
    }
}
