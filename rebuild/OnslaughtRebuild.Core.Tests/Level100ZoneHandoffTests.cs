// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The chain autopilot's jet-to-walker hand-off rule, tested directly rather
/// than through a route that may or may not carry the ferry above the tier.
/// It stays outside the ferry sweep's class so it runs in the default Core
/// gate without the forty-run fixture.
/// </summary>
public sealed class Level100ZoneHandoffTests
{
    /// <summary>
    /// The hand-off rule itself, independent of where any route happens to
    /// carry the ferry: the fixed rule refuses a jet-to-walker morph from the
    /// cruise above <c>ZoneHandoffClearanceMillimeters</c> and allows it
    /// below; the reinstated horizontal-only defect allows both.
    /// </summary>
    [Fact]
    public void ZoneHandoff_RefusesTheCruiseUnlessTheDefectIsReinstated()
    {
        var progress = new Level100TutorialProgress(true, true, true, true);
        Level100ChainAutopilot fixedDriver = Level100ChainAutopilot.Create(progress);
        Level100ChainAutopilot adverseDriver = Level100ChainAutopilot.Create(
            progress, horizontalOnlyZoneHandoff: true);
        WorldSnapshot grounded = fixedDriver.Snapshot;
        int surface = Math.Max(
            grounded.PlayerGroundElevationMillimeters,
            Level100Terrain.WaterElevationMillimeters);
        int tier = Level100ChainAutopilot.ZoneHandoffClearanceMillimeters;
        WorldSnapshot cruise = grounded with
        {
            PlayerOnGround = false,
            PlayerElevationMillimeters = surface + tier + 5_000,
        };
        WorldSnapshot deck = grounded with
        {
            PlayerOnGround = false,
            PlayerElevationMillimeters = surface + tier - 5_000,
        };

        Assert.False(fixedDriver.ClearedToLeaveJetMode(cruise));
        Assert.True(fixedDriver.ClearedToLeaveJetMode(deck));
        Assert.True(adverseDriver.ClearedToLeaveJetMode(cruise));
        Assert.True(adverseDriver.ClearedToLeaveJetMode(deck));
    }

}
