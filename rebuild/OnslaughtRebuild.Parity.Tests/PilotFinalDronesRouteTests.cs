// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route through the final drones, each tape extending the last exactly (k11: p22-fire's input
/// to P26720, then on to P27240; k13: k11's to P27240, then on to P27341; j4: k13's to P27341, then the jet transform and
/// the sixth drone, on to P27569), against the native port with patches 0029 and 0030: "Airborne Target 2 Destroyed" posts
/// at P25977, P27055, P27304, (k13) P27324 and (j4) P27511. Every probe field every Present to each reference's end, with
/// the route's measured sound-pool profile.
/// </summary>
public sealed class PilotFinalDronesRouteTests(ITestOutputHelper output)
{
    private const string Sounds = "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json";

    [RetailGoldenFact("portrefs/l100-k11-port-20261022/port-probes-every1.txt", "portrefs/l100-k11-port-20261022/k11.tape", Sounds)]
    public void TheK11RouteMatchesThePortEveryPresent() => Check("portrefs/l100-k11-port-20261022", "k11.tape", 27400);

    [RetailGoldenFact("portrefs/l100-k13-port-20261022/port-probes-every1.txt", "portrefs/l100-k13-port-20261022/k13.tape", Sounds)]
    public void TheK13RouteMatchesThePortEveryPresent() => Check("portrefs/l100-k13-port-20261022", "k13.tape", 27450);

    [RetailGoldenFact("portrefs/l100-j4-port-20261022/port-probes-every1.txt", "portrefs/l100-j4-port-20261022/j4.tape", Sounds)]
    public void TheJ4RouteMatchesThePortEveryPresent() => Check("portrefs/l100-j4-port-20261022", "j4.tape", 27650);

    /// <summary>Every field matches the port every Present through <paramref name="matchedThrough"/> (never lowered).</summary>
    private void Check(string dir, string tape, int matchedThrough)
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(RetailGolden.File(Sounds)!));
        session.Tape = RetailTape.Load(RetailGolden.File(dir + "/" + tape)!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, dir + "/port-probes-every1.txt", matchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= matchedThrough, $"matched through Present {matched}, below {matchedThrough}; {first}");
        Assert.True(compared >= 9 * (matchedThrough - 3), $"only {compared} fields compared");
    }
}
