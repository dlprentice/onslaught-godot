// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route past the first airborne targets (the lead's p22-fire tape: c17-fire's input to P22150,
/// then the return to the Forseti repair pad, the repair, the flight out and the second airborne target) against the
/// native port build 48738e88. An earlier ae04185f event observer recorded the pad taking the landed battle engine as its
/// target at P23333, repairing it at P23367, the walker's weapon augmenting at P25290 and "Airborne Target 2 Destroyed"
/// posting at P25977; the newer recording does not include that observer. Every probe field every Present to
/// <see cref="MatchedThrough"/>, the reference's end, with the route's measured sound-pool profile.
/// </summary>
public sealed class PilotP22RouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 26720;

    private const string Dir = "portrefs/l100-p22-port-48738e88";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/p22-fire.tape",
        "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void ThePilotP22RouteMatchesThePortEveryPresent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!));
        session.Tape = RetailTape.Load(RetailGolden.File(Dir + "/p22-fire.tape")!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, Dir + "/port-probes-every1.txt", MatchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= MatchedThrough, $"matched through Present {matched}, below {MatchedThrough}; {first}");
        Assert.True(compared >= 9 * (MatchedThrough - 3), $"only {compared} fields compared");
    }
}
