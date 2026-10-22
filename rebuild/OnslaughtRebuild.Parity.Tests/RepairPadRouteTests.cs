// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// Level 100's tutorial pilot route past the first airborne targets (the lead's p22-fire tape: c17-fire's input to P22150,
/// then the return to the Forseti repair pad, the repair and the flight out) against the native port with patches 0029
/// and 0030 (l100-p22-port-20261022). The pad takes the landed battle engine as its target at P23333 (its charge effects
/// at the Charge emitters), fires once its shot is ready (CBuilding::Move's timer) and repairs it to full life at P23367.
/// Every probe field every Present to <see cref="MatchedThrough"/>, with the route's measured sound-pool profile.
/// </summary>
public sealed class RepairPadRouteTests(ITestOutputHelper output)
{
    /// <summary>The Present through which every field matches the port every Present (never lowered).</summary>
    private const int MatchedThrough = 25289;

    private const string Dir = "portrefs/l100-p22-port-20261022";

    [RetailGoldenFact(Dir + "/port-probes-every1.txt", Dir + "/p22-fire.tape",
        "portrefs/l100-trainer-port-20261021/sound-seeds-admission-root-c.json")]
    public void TheRepairPadRouteMatchesThePortEveryPresent()
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
