// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// GOAL.md acceptance for Level 100: the already-paid
/// <see cref="Level100ChainRunFixture"/> reaches <c>Won</c> by
/// <see cref="SimInput"/> alone, then idle ticks carry the already-pinned
/// 5.0 f overlay to <c>FrontEndHandoffReady</c> and
/// <see cref="RetailCareerCampaign.ApplyUpdate"/> unlocks world 110.
/// </summary>
/// <remarks>
/// <para>
/// This class reuses one chain. It does not call
/// <c>Level100ChainAutopilot.Create().Run()</c>, it does not live on
/// <see cref="Level100FullChainTests"/> (that type also constructs the two
/// abort fixtures), and it never calls
/// <see cref="Level100Mission.QueueExternalEvent"/>. Autopilot that posts a
/// mission event proves nothing.
/// </para>
/// <para>
/// First-play elapsed / <c>this+0xf4</c> score, player iceberg-kill store-0,
/// secondaries, and ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed.
/// </para>
/// </remarks>
public sealed class Level100PlayerInputWonHandoffTests
    : IClassFixture<Level100ChainRunFixture>
{
    private readonly Level100ChainRunFixture _chain;

    public Level100PlayerInputWonHandoffTests(Level100ChainRunFixture chain)
    {
        _chain = chain;
    }

    /// <summary>
    /// After the fixture's SimInput-only <c>Won</c> /
    /// <c>SuccessCountdown</c>, <see cref="SimInput.Idle"/> for
    /// <see cref="RetailGameEndCountdown.WonTicks"/> must land
    /// <c>FrontEndHandoffReady</c> and the already-pinned FillOut
    /// <c>ApplyUpdate</c> must unlock world 110.
    /// </summary>
    [Fact]
    public void SimInputOnlyWon_AfterSuccessCountdown_ReachesFrontEndHandoffAndUnlocksWorld110()
    {
        Level100ChainAutopilot driver = _chain.Driver;
        Assert.Equal(Level100MissionOutcome.Won, _chain.Outcome);
        Assert.Equal(Level100MissionOutcome.Won, driver.Snapshot.Level100Mission.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.SuccessCountdown,
            driver.Snapshot.Level100Mission.TerminalState);
        Assert.Equal(
            RetailGameEndCountdown.WonTicks,
            driver.Snapshot.Level100Mission.TerminalTicksRemaining);
        AssertCareerStillCold(driver.Career);

        for (int tick = 0; tick < RetailGameEndCountdown.WonTicks; tick++)
        {
            driver.Step(SimInput.Idle);
        }

        Level100MissionSnapshot mission = driver.Snapshot.Level100Mission;
        Assert.Equal(Level100MissionOutcome.Won, mission.Outcome);
        Assert.Equal(
            Level100MissionTerminalState.FrontEndHandoffReady,
            mission.TerminalState);
        Assert.Equal(0, mission.TerminalTicksRemaining);

        RetailCareerCampaign career = driver.Career;
        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNode next = career.Nodes.Find(110)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        RetailCareerNodeLink higher = career.GetLink(training.HigherLink)!;

        Assert.Equal(1, training.Complete);
        Assert.Equal(1, career.CareerInProgress);
        Assert.Equal(0, next.Complete);
        Assert.Equal(1, lower.ToNode);
        Assert.Equal(RetailCareerNodeLink.Complete, lower.LinkType);
        Assert.Equal(-1, higher.ToNode);
        Assert.Equal(RetailCareerNodeLink.NotComplete, higher.LinkType);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    private static void AssertCareerStillCold(RetailCareerCampaign career)
    {
        RetailCareerNode training = career.Nodes.Find(100)!;
        RetailCareerNodeLink lower = career.GetLink(training.LowerLink)!;
        Assert.Equal(0, training.Complete);
        Assert.Equal(0, career.CareerInProgress);
        Assert.Equal(RetailCareerNodeLink.NotComplete, lower.LinkType);
    }
}
