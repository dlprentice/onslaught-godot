// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Things;
using Xunit;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The two level-100 loss routes against the original game itself: the pristine executable (SHA-256 74154bfa...) recorded under the
/// replay harness with its music on the virtual clock (ONSLAUGHT_RETAIL_MCLK2: the rebuild's private copies of the music-clock
/// pairs mfelose and w1vs, whose README gives the settings). Each of a pair's two processes is compared Present by Present: EVT,
/// EVN, FT, SC, LVL, RNG, POS and LIFE (the original's "null", no player or no stream, equals all-zero bytes), the front end's line
/// (the original's FES, FEP and FEC joined) while the front end runs, END_LEVEL_DATA at every Present, and the battle engine's
/// modelled fields (the tables of RetailGoldenTests.Level100sBattleEngineFollowsRetailsPerPresentObject, at their offsets in the
/// original's BEO and BEW) wherever the original has a battle engine. Both routes start from the original runs' own options file.
/// </summary>
public sealed class OriginalLossRouteTests(ITestOutputHelper output)
{
    private const string Fe = "mclk2/mfelose";
    private const string W1 = "mclk2/win1-mclk2vs";

    private sealed class StopRun : Exception;

    /// <summary>One recorded process: its rows by Present and name (the shim's CR and t= stamps ignored).</summary>
    private sealed class Original
    {
        private readonly Dictionary<(long, string), string> _rows = [];

        public long Last { get; private set; }

        public static Original Load(string file)
        {
            var o = new Original();
            foreach (string line in File.ReadLines(file))
            {
                string[] f = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (f.Length >= 4 && f[0] == "P" && long.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out long p))
                {
                    o._rows[(p, f[2])] = f[3].Trim();
                    o.Last = Math.Max(o.Last, p);
                }
            }

            return o;
        }

        public string? Get(long p, string name) => _rows.GetValueOrDefault((p, name));
    }

    /// <summary>The comparison with one process: counts by field and the first difference.</summary>
    private sealed class Comparison(string label, Original original)
    {
        public SortedDictionary<string, long> Compared { get; } = new(StringComparer.Ordinal);

        public string? First { get; private set; }

        public long Presents { get; set; }

        public long EnginePresents { get; private set; }

        private static bool Same(string ours, string theirs) => ours == theirs || (theirs == "null" && ours.All(c => c == '0'));

        private void Count(string name) => Compared[name] = Compared.GetValueOrDefault(name) + 1;

        public void Field(long p, string name, string ours)
        {
            Count(name);
            string? theirs = original.Get(p, name);
            if (theirs is null || !Same(ours, theirs))
            {
                First ??= $"{label} P{p} {name}: original {theirs ?? "<no row>"}, rebuild {ours}";
            }
        }

        public void FrontEnd(long p, string ours)
        {
            Count("FE");
            string theirs = string.Concat(new[] { "FES", "FEP", "FEC" }.Select(n => original.Get(p, n) ?? "?"));
            if (theirs != ours)
            {
                First ??= $"{label} P{p} FE: original {theirs}, rebuild {ours}";
            }
        }

        public void Engine(long p, BattleEngine? be)
        {
            string? beo = original.Get(p, "BEO");
            string? bew = original.Get(p, "BEW");
            if (beo is null)
            {
                First ??= $"{label} P{p} BEO: no reference row";
                return;
            }

            if (bew is null)
            {
                First ??= $"{label} P{p} BEW: no reference row";
                return;
            }

            if (beo == "null")
            {
                if (be is not null || bew != "null")
                {
                    First ??= $"{label} P{p} BEO: inconsistent original and rebuild engine availability";
                }

                return;
            }

            if (be is null)
            {
                First ??= $"{label} P{p} BEO: the rebuild has no battle engine";
                return;
            }

            if ((bew == "null") != (be.WalkerPart is null))
            {
                First ??= $"{label} P{p} BEW: inconsistent original and rebuild walker availability";
                return;
            }

            EnginePresents++;
            byte[] o = Convert.FromHexString(beo);
            byte[]? w = bew == "null" ? null : Convert.FromHexString(bew);
            foreach ((string probe, string name, int offset, Func<BattleEngine, uint[]> ours) in RetailGoldenTests.ObjectFields())
            {
                byte[]? raw = probe == "BEO" ? o : w;
                if (raw is null || (probe == "BEW" && be.WalkerPart is null))
                {
                    continue;
                }

                Count(probe);
                uint[] mine = ours(be);
                uint[] theirs = [.. Enumerable.Range(0, mine.Length).Select(j => BitConverter.ToUInt32(raw, offset + (4 * j)))];
                if (name.StartsWith("mFlags", StringComparison.Ordinal))
                {
                    theirs[0] &= 0xffff;
                }

                if (!mine.SequenceEqual(theirs))
                {
                    First ??= $"{label} P{p} {probe} {name}: original {string.Join(' ', theirs.Select(x => x.ToString("x8")))}, rebuild {string.Join(' ', mine.Select(x => x.ToString("x8")))}";
                }
            }
        }
    }

    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();

    private static Func<string, (string Name, byte[] Bytes)> StartingFiles(string options)
    {
        byte[] start = File.ReadAllBytes(RetailGolden.File(options)!);
        return path => string.Equals(path, DefaultOptions.FileName, StringComparison.OrdinalIgnoreCase) ? (path, start) : GameData.Read(path);
    }

    /// <summary>
    /// The front-end loss route (fe-level100-lose-quit-7300f.tape, a -skipfmv boot: a new career named and saved, level 100 lost, the
    /// level-end menu's Quit, the debriefing) against both processes of the original's pair mfelose, every Present to 7300, and
    /// the career it saves against both processes' BEA 1.bes byte for byte.
    /// </summary>
    [RetailGoldenFact(Fe + "/rp-mfelose-rr-a.probe.log", Fe + "/rp-mfelose-rr-b.probe.log", Fe + "/fe-level100-lose-quit-7300f.tape",
        Fe + "/start/defaultoptions.bea", Fe + "/files-a/savegames/BEA 1.bes", Fe + "/files-b/savegames/BEA 1.bes")]
    public void TheFrontEndLossRouteEqualsTheOriginalGameEveryPresent()
    {
        const long Last = 7300;
        Comparison[] processes = [new("a", Original.Load(RetailGolden.File(Fe + "/rp-mfelose-rr-a.probe.log")!)),
            new("b", Original.Load(RetailGolden.File(Fe + "/rp-mfelose-rr-b.probe.log")!))];
        Func<string, (string Name, byte[] Bytes)> read = StartingFiles(Fe + "/start/defaultoptions.bea");
        var run = new GameRun(read, id => new RetailPcText(GameData.Read("BEA.exe").Bytes, 0)[id], new RetailChainTests.InstalledFilms(),
            skipFmv: true, LevelSession.VirtualVoiceFor(GameData.Read))
        {
            Tape = RetailTape.Load(RetailGolden.File(Fe + "/fe-level100-lose-quit-7300f.tape")!),
        };
        run.BeforeTapeInput = () =>
        {
            long p = run.Clock.PresentCount;
            Dictionary<string, string> ours = GoldOwnRouteTests.Probe(run);
            ours.TryAdd("POS", new string('0', 24));
            ours.TryAdd("LIFE", new string('0', 16));
            LevelSession? level = run.Level;
            BattleEngine? be = level is not null && !level.PlayersDeleted ? level.Game.GetPlayer(0)?.BattleEngine as BattleEngine : null;
            string eld = Hex(run.FrontEnd.EndLevelData.ToBytes());
            foreach (Comparison c in processes)
            {
                c.Presents++;
                foreach ((string name, string value) in ours)
                {
                    if (name == "FE")
                    {
                        c.FrontEnd(p, value);
                    }
                    else if (name != "CRT")
                    {
                        c.Field(p, name, value);
                    }
                }

                c.Field(p, "ELD", eld);
                c.Engine(p, be);
            }

            if (p >= Last)
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

        foreach (Comparison c in processes)
        {
            output.WriteLine($"{c.Presents} Presents, {string.Join(", ", c.Compared.Select(kv => $"{kv.Key} {kv.Value}"))}; first difference: {c.First ?? "none"}");
            Assert.True(c.First is null, c.First);
            Assert.Equal(Last, c.Presents);
            foreach (string name in new[] { "EVT", "EVN", "FT", "SC", "LVL", "RNG", "POS", "LIFE", "ELD" })
            {
                Assert.Equal(Last, c.Compared[name]);
            }

            Assert.True(c.Compared["FE"] > 1000 && c.EnginePresents > 5000, "the front end's line and the battle engine were compared");
        }

        Assert.Equal(QuitType.UserQuitToFrontEnd, run.Level!.Game.Quit);
        Assert.Equal(["BEA 1"], run.FrontEnd.MemoryCard.Folder.Names);
        byte[] saved = run.FrontEnd.MemoryCard.Folder.Read("BEA 1");
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Fe + "/files-a/savegames/BEA 1.bes")!), saved);
        Assert.Equal(File.ReadAllBytes(RetailGolden.File(Fe + "/files-b/savegames/BEA 1.bes")!), saved);
        Assert.Null(run.DefaultOptionsFile);
    }

    /// <summary>
    /// David's level-100 recording (level100-david-win1-loss-retry.tape, -level 100: a loss by friendly fire, the level-end menu's
    /// Retry, a quit) against both processes of the original's voice-sync pair w1vs, every Present from 3 (the first two are the
    /// boot's, before the level loads) to 8882, including the Present LevelSession.Restart makes while it reloads, which is sampled
    /// from the ended session.
    /// </summary>
    [RetailGoldenFact(W1 + "/win1.mclk2vs-a.probes", W1 + "/win1.mclk2vs-b.probes", W1 + "/level100-david-win1-loss-retry.tape",
        W1 + "/start/defaultoptions.bea")]
    public void DavidsLossAndRetryEqualTheOriginalGameEveryPresent()
    {
        const long Last = 8882;
        Comparison[] processes = [new("a", Original.Load(RetailGolden.File(W1 + "/win1.mclk2vs-a.probes")!)),
            new("b", Original.Load(RetailGolden.File(W1 + "/win1.mclk2vs-b.probes")!))];
        Func<string, (string Name, byte[] Bytes)> read = StartingFiles(W1 + "/start/defaultoptions.bea");
        LevelSession session = LevelSession.Load(100, read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.Tape = RetailTape.Load(RetailGolden.File(W1 + "/level100-david-win1-loss-retry.tape")!);
        var sampled = new HashSet<long>();
        void Sample(LevelSession s)
        {
            long p = s.PresentCount;
            if (p < 3 || p > Last || !sampled.Add(p))
            {
                return;
            }

            Dictionary<string, string> ours = TrainerRouteTests.Probe(s);
            ours.Remove("CRT");
            BattleEngine? be = !s.PlayersDeleted ? s.Game.GetPlayer(0)?.BattleEngine as BattleEngine : null;
            string eld = Hex(s.EndLevelData.ToBytes());
            foreach (Comparison c in processes)
            {
                c.Presents++;
                foreach ((string name, string value) in ours)
                {
                    c.Field(p, name, value);
                }

                c.Field(p, "ELD", eld);
                c.Engine(p, be);
            }
        }

        LevelSession? restarting = null;
        session.Clock.Presented = () =>
        {
            if (restarting is not null)
            {
                Sample(restarting);
            }
        };
        int restarts = 0;
        while (session.PresentCount < Last)
        {
            if (session.Ended)
            {
                Assert.Equal(QuitType.RestartLevel, session.Game.Quit);
                restarting = session;
                session = session.Restart();
                restarting = null;
                restarts++;
            }

            long before = session.PresentCount;
            session.AdvanceToNextPresent();
            if (session.PresentCount != before)
            {
                Sample(session);
            }
        }

        Assert.Equal(1, restarts);
        foreach (Comparison c in processes)
        {
            output.WriteLine($"{c.Presents} Presents, {string.Join(", ", c.Compared.Select(kv => $"{kv.Key} {kv.Value}"))}; first difference: {c.First ?? "none"}");
            Assert.True(c.First is null, c.First);
            Assert.Equal(Last - 2, c.Presents);
            foreach (string name in new[] { "EVT", "EVN", "FT", "SC", "LVL", "RNG", "POS", "LIFE", "ELD" })
            {
                Assert.Equal(Last - 2, c.Compared[name]);
            }

            Assert.True(c.EnginePresents > 5000, "the battle engine was compared across the loss and Retry");
        }
    }
}
