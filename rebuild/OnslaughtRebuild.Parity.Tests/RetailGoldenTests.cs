// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Things;
using Xunit.Abstractions;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// A test against a contributor's retail goldens: retail BEA.exe recorded under the replay harness (golden-640: virtual time
/// 1/30 s per Present, probes read after each Present returns; two retail processes identical). They are retail
/// recordings kept out of Git, found through ONSLAUGHT_RETAIL_GOLDEN; skipped, visibly, without them or the game data.
/// </summary>
public sealed class RetailGoldenFactAttribute : FactAttribute
{
    public RetailGoldenFactAttribute(string file)
    {
        if (RetailGolden.File(file) is null)
        {
            Skip = $"retail golden '{file}' not found (set ONSLAUGHT_RETAIL_GOLDEN, or ONSLAUGHT_GOLDEN_VDS for vds/)";
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
    /// <summary>C's level-100 tape (golden-640/tapes, the one both per-Present goldens replay from Present 3300), when present.</summary>
    private static RetailTape? Level100Tape() =>
        RetailGolden.File("tapes/level100-keys-mouse-10000f.tape") is { } path ? RetailTape.Load(path) : null;

    /// <summary>The Presents our level-100 run matches so far (raised as it reaches further; never lowered).</summary>
    private const int RandomStreamMatchedFloor = 187;

    /// <summary>The last Present the Present-by-Present run matches retail's clock and random stream to (never lowered).</summary>
    private const int PresentByPresentFloor = 3600;

    /// <summary>The last sampled Present (every 50) the run matches the virtual-DS golden to (never lowered).</summary>
    private const int VirtualDsFloor = 3300;

    /// <summary>The Presents over which every compared battle-engine field matches retail's (never lowered).</summary>
    private const int BattleEngineMatchedFloor = 3598;

    /// <summary>The Present through which every compared battle-engine field equals retail's (never lowered).</summary>
    private const int BattleEngineMatchedThrough = 3477;

    /// <summary>
    /// Level 100 run Present by Present against B's virtual-DS golden (level100.vds-a.probes: retail with virtual
    /// DirectSound status, the audio model the port and the core run; probes EVT, FT and RNG every 50 Presents). The
    /// sampled Presents must match through <see cref="VirtualDsFloor"/>; the first difference after it is reported. The
    /// tape's input from Present 3300 is not wired, so the comparison stops there.
    /// </summary>
    [RetailGoldenFact("vds/level100.vds-a.probes")]
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
        static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
        string? first = null;
        long matched = 0;
        string stopped = "ran to Present 3300";
        while (session.PresentCount < 3300)
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
            if (evt == ourEvt && golden[(p, "FT")] == ourFt && golden[(p, "RNG")].StartsWith(ourRng, StringComparison.Ordinal))
            {
                matched = p;
            }
            else
            {
                first = $"Present {p}: EVT {evt}/{ourEvt}, FT {golden[(p, "FT")]}/{ourFt}, RNG {golden[(p, "RNG")]}/{ourRng} (retail/ours)";
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
    [RetailGoldenFact("beobjvds/l100.beobj-vds-a.probes")]
    public void Level100RunPresentByPresentMatchesRetailsClockAndRandomStream()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = Level100Tape();
        var ours = new Dictionary<long, (uint Evt, uint Ft, int Rng)>();
        string stopped = "ran to retail's last probe";
        while (session.PresentCount < 3600)
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
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File("beobjvds/l100.beobj-vds-a.probes")!))
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

    /// <summary>
    /// Level 100's random stream against retail's at every Present (level100.perpresent-3600.probes: RNG is the
    /// stream's mValue and mSeed, EVT the event manager's time). Each retail Present is matched to our state after the
    /// update that reached its event time, so the Present-to-update mapping comes from the recording itself. The
    /// comparison runs until our simulation stops (a part not ported yet) or retail's time passes ours.
    /// </summary>
    [RetailGoldenFact("level100.perpresent-3600.probes")]
    public void Level100sRandomStreamFollowsRetailsPerPresentProbes()
    {
        (ThingContext context, ParityGame game) = LevelLoadTests.Level100Run();
        var states = new Dictionary<uint, (int Update, int Value)>();
        void Record(int u) => states[BitConverter.SingleToUInt32Bits(context.Events.Time)] = (u, context.Random.Value);
        Record(0);
        int update = 0;
        string stopped = "ran to retail's last probe";
        while (update < 2000)
        {
            update++;
            try
            {
                game.Update();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped in U{update}: {e.Message}";
                break;
            }

            Record(update);
        }

        int compared = 0;
        string? first = null;
        uint? evt = null;
        foreach (string line in System.IO.File.ReadLines(RetailGolden.File("level100.perpresent-3600.probes")!))
        {
            string[] f = line.Split(' ');
            if (f.Length != 4 || f[0] != "P" || f[3] == "null")
            {
                continue;
            }

            if (f[2] == "EVT")
            {
                evt = BitConverter.ToUInt32(Convert.FromHexString(f[3]));
                continue;
            }

            if (f[2] != "RNG" || evt is null)
            {
                continue;
            }

            if (!states.TryGetValue(evt.Value, out (int Update, int Value) ours))
            {
                stopped += $"; retail's Present {f[1]} (time {BitConverter.UInt32BitsToSingle(evt.Value):R}) is past our run";
                break;
            }

            int retail = BitConverter.ToInt32(Convert.FromHexString(f[3]), 0);
            if (retail != ours.Value)
            {
                first = $"Present {f[1]} (time {BitConverter.UInt32BitsToSingle(evt.Value):R}, our U{ours.Update}): retail {retail}, ours {ours.Value}";
                break;
            }

            compared++;
            evt = null;
        }

        output.WriteLine($"{stopped}; {compared} Presents matched; first difference: {first ?? "none"}");
        Assert.True(compared >= RandomStreamMatchedFloor);
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
    /// The player's battle engine against retail's object at every Present (level100.beobj-perpresent-3600.probes: BEO
    /// is the whole CBattleEngine, read after each Present), field by field, each retail Present matched to our state
    /// after the update that reached its event time. Reports each field's first difference; asserts the fields matched
    /// so far stay matched through the Presents our run reaches.
    /// </summary>
    [RetailGoldenFact("beobjvds/l100.beobj-vds-a.probes")]
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
        while (session.PresentCount < 3600)
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
        string path = RetailGolden.File("beobjvds/l100.beobj-vds-a.probes")!;
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

        // Every field matches through the floor's Present (differences after it are reported, not gated): the tape's
        // input from Present 3300 is not wired yet, and retail's power-up at Present 2058 is still open.
        Assert.True(compared >= BattleEngineMatchedFloor, $"{compared} Presents compared, below {BattleEngineMatchedFloor}");
        Assert.True(firstBad > BattleEngineMatchedThrough, $"a field differs at Present {firstBad}, at or before {BattleEngineMatchedThrough}");
    }
}
