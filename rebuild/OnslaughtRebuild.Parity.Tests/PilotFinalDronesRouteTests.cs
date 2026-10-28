// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route through the final drones, each tape extending the last exactly (k11: p22-fire's input
/// to P26720, then on to P27240; k13: k11's to P27240, then on to P27341; j4: k13's to P27341, then the jet transform and
/// the sixth drone, on to P27569), against the native port build 48738e88. The earlier ae04185f event observer recorded
/// "Airborne Target 2 Destroyed" at P25977, P27055, P27304, (k13) P27324 and (j4) P27511; the newer recordings do not
/// include that observer. Every probe field is compared every Present to each reference's end, with the route's measured
/// sound-pool profile.
/// </summary>
public sealed class PilotFinalDronesRouteTests(ITestOutputHelper output)
{
    private const string Sounds = "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json";

    [RetailGoldenFact("portrefs/l100-k11-port-48738e88/port-probes-every1.txt", "portrefs/l100-k11-port-48738e88/k11.tape", Sounds)]
    public void TheK11RouteMatchesThePortEveryPresent() => _ = Check("portrefs/l100-k11-port-48738e88", "k11.tape", 27400);

    [RetailGoldenFact("portrefs/l100-k13-port-48738e88/port-probes-every1.txt", "portrefs/l100-k13-port-48738e88/k13.tape", Sounds)]
    public void TheK13RouteMatchesThePortEveryPresent() => _ = Check("portrefs/l100-k13-port-48738e88", "k13.tape", 27450);

    [RetailGoldenFact("portrefs/l100-j4-port-48738e88/port-probes-every1.txt", "portrefs/l100-j4-port-48738e88/j4.tape", Sounds)]
    public void TheJ4RouteMatchesThePortEveryPresent() => _ = Check("portrefs/l100-j4-port-48738e88", "j4.tape", 27650);

    /// <summary>
    /// The pilot's winning tape (pilot-win: j4's input to P27569, then Target Zone 4 and the walk in) under -level 100: the
    /// level is won at P28157, the Victory screen's countdown quits to the front end, and the last MainLoop's two renders
    /// are Presents 28305 and 28306. The next MainLoop's update sees the quit (mFrameTime = mBaseTime + mFrameLength, no
    /// render), RunLevel's close fills END_LEVEL_DATA, and the port's next Present (28307, in the outro film) records it.
    /// Every probe field matches every Present to the level's end, and END_LEVEL_DATA equals the port's byte for byte.
    /// </summary>
    [RetailGoldenFact("portrefs/l100-pilotwin-port-48738e88/port-probes-every1.txt", "portrefs/l100-pilotwin-port-48738e88/pilot-win.tape", Sounds)]
    public void ThePilotWinRouteMatchesThePortThroughTheLevelsEnd()
    {
        const string Dir = "portrefs/l100-pilotwin-port-48738e88";
        // No end Present: the comparison runs until RunLevel returns, so a sample taken after the level's end (a second
        // reading of P28306, whose FT the port has already moved on) is a difference here.
        LevelSession session = Check(Dir, "pilot-win.tape", 28306, toLevelEnd: true);
        Assert.True(session.Ended, $"the level had not ended after Present {session.PresentCount}");
        Assert.Equal(28306, session.PresentCount);
        Assert.Equal(OnslaughtRebuild.Parity.Clock.QuitType.QuitToFrontEnd, session.Game.Quit);

        // The port's ELD line: END_LEVEL_DATA at the first Present after RunLevel's close (P28307), the one change since P1.
        var eld = File.ReadLines(RetailGolden.File(Dir + "/port-probes-every1.txt")!).Select(l => l.Split(' '))
            .Where(f => f.Length == 4 && f[0] == "P" && f[2] == "ELD").Select(f => (Present: long.Parse(f[1]), Bytes: Convert.FromHexString(f[3])))
            .ToList();
        Assert.Equal([1L, 28307L], eld.Select(e => e.Present));
        byte[] port = eld[1].Bytes;
        byte[] ours = session.EndLevelData.ToBytes();
        Assert.Equal(OnslaughtRebuild.Parity.Career.EndLevelData.ByteSize, port.Length);
        int first = Enumerable.Range(0, ours.Length).FirstOrDefault(i => ours[i] != port[i], -1);
        output.WriteLine($"END_LEVEL_DATA: {ours.Length} bytes, final state {session.EndLevelData.FinalState}, first difference {(first < 0 ? "none" : $"0x{first:x}")}");
        Assert.True(first < 0, $"END_LEVEL_DATA differs first at byte 0x{first:x} (port {port[Math.Max(first, 0)]:x2}, ours {ours[Math.Max(first, 0)]:x2})");

        // ShutdownRestartLoop and CGame::Shutdown: RunOutroFMV marks cutscene 2 (goodie 202) new and plays it. Under
        // -skipfmv the film never reaches its last frame, so the port presents in it until the run is stopped (P28600), its
        // probes reading the shut-down game: no random stream, no player. The tape has no input there.
        Assert.True(session.Tape!.LastFrame < 28307, $"the tape has input at Present {session.Tape.LastFrame}, inside the outro");
        Dictionary<(long Present, string Name), string> probes = TrainerRouteTests.ReadPortProbes(Dir + "/port-probes-every1.txt");
        long last = probes.Keys.Max(k => k.Present);
        Assert.Equal(28600, last);
        var films = new FilmPlayer(session.Clock, session.Input.Input, session.Input.Context,
            new UnopenedFilms(new InstalledFilmHeaders(GameData.Read)), new HarnessFilmPacing(session.Clock));
        long filmMatched = session.PresentCount;
        string? filmFirst = null;
        int filmCompared = 0;
        session.Clock.Presented = () =>
        {
            long p = session.PresentCount;
            foreach ((string name, string value) in TrainerRouteTests.Probe(session))
            {
                Assert.True(probes.TryGetValue((p, name), out string? theirs), $"the port has no {name} at Present {p}");
                filmCompared++;
                if (theirs != value)
                {
                    filmFirst ??= $"Present {p} {name}: port {theirs}, ours {value}";
                }
            }

            if (filmFirst is null)
            {
                filmMatched = p;
            }

            if (filmFirst is not null || p >= last)
            {
                throw new OutroStopped();
            }
        };
        Assert.Throws<OutroStopped>(() => session.Shutdown(films));
        output.WriteLine($"the outro: {filmCompared} fields compared; every field matches through Present {filmMatched}; first difference: {filmFirst ?? "none"}");
        Assert.True(filmFirst is null && filmMatched == last, $"the outro matched through Present {filmMatched}; {filmFirst}");
        Assert.Equal(9 * (last - 28306), filmCompared);
        Assert.Equal(OnslaughtRebuild.Parity.Career.CareerGoodieState.New, session.Career!.Record.GoodieState(202));
        Assert.Equal(1, session.Career.Record.PendingExtraGoodies);
    }

    private sealed class OutroStopped : Exception;

    /// <summary>
    /// Every field matches the port every Present through <paramref name="matchedThrough"/> (never lowered). With
    /// <paramref name="toLevelEnd"/> the comparison continues until the level ends and no field may differ at all.
    /// </summary>
    private LevelSession Check(string dir, string tape, int matchedThrough, bool toLevelEnd = false)
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(RetailGolden.File(Sounds)!));
        session.Tape = RetailTape.Load(RetailGolden.File(dir + "/" + tape)!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, dir + "/port-probes-every1.txt", toLevelEnd ? long.MaxValue : matchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= matchedThrough, $"matched through Present {matched}, below {matchedThrough}; {first}");
        Assert.True(!toLevelEnd || first is null, $"a field differs before the level's end: {first}");
        Assert.True(compared >= 9 * (matchedThrough - 3), $"only {compared} fields compared");
        return session;
    }
}
