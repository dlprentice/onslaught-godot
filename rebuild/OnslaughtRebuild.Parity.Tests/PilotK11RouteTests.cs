// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route through the final drones (the planner's k11 tape: p22-fire's input exactly to P26720,
/// then on to P27240) against the native port with patches 0029 and 0030 (l100-k11-port-20261022): "Airborne Target 2
/// Destroyed" posts at P25977, P27055 and P27304. Every probe field every Present to <see cref="MatchedThrough"/>, the
/// reference's end, with the route's measured sound-pool profile.
/// </summary>
public sealed class PilotK11RouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 27400;

    private const string Dir = "portrefs/l100-k11-port-20261022";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/k11.tape",
        "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void ThePilotK11RouteMatchesThePortEveryPresent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!));
        session.Tape = RetailTape.Load(RetailGolden.File(Dir + "/k11.tape")!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, Dir + "/port-probes-every1.txt", MatchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= MatchedThrough, $"matched through Present {matched}, below {MatchedThrough}; {first}");
        Assert.True(compared >= 9 * (MatchedThrough - 3), $"only {compared} fields compared");
    }
}
