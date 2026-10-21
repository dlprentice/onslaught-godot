// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route through the trainer, the take-off and the first airborne targets (the lead's c17-fire
/// tape) against the native port with patches 0029 and 0030 (l100-c17-port-20261021): three airborne targets shot down at
/// Presents 19821, 21964 and 22123 (the port's "Airborne Target 1 Destroyed" posts), so each one's death, smoke, death
/// roll and explosion. Every probe field every Present to 22150, with the route's measured sound-pool profile. The Core
/// replays the tape as the port delivers it: the port drops a key line written after a later frame (replay.cpp), which
/// the port-delivered copy leaves out.
/// </summary>
public sealed class AirborneTargetsRouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 22150;

    private const string Dir = "portrefs/l100-c17-port-20261021";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/c17-fire-port-delivered.tape",
        "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void TheFirstAirborneTargetsRouteMatchesThePortEveryPresent()
    {
        LevelSession session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read),
            soundOptions: TrainerRouteTests.SoundProfile(RetailGolden.File("portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")!));
        session.Tape = RetailTape.Load(RetailGolden.File(Dir + "/c17-fire-port-delivered.tape")!);
        (long matched, string? first, int compared) = TrainerRouteTests.CompareEveryPresent(session, Dir + "/port-probes-every1.txt", MatchedThrough);
        output.WriteLine($"{compared} fields compared; every field matches through Present {matched}; first difference: {first ?? "none"}");
        Assert.True(matched >= MatchedThrough, $"matched through Present {matched}, below {MatchedThrough}; {first}");
        Assert.True(compared >= 9 * (MatchedThrough - 3), $"only {compared} fields compared");
    }
}
