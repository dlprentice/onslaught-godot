// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailGameEndCountdown"/> against the
/// immediate stores in the pristine <c>74154bfa…</c> image at
/// <c>CGame::DeclareLevelLost</c> <c>0x0046F430</c> and
/// <c>CGame::DeclareLevelWon</c> <c>0x0046F2F0</c>. Source
/// <c>game.cpp:75-76</c> writes 5.0 f for both; retail Lost writes 2.0 f.
/// </summary>
public sealed class RetailGameEndCountdownTests
{
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    [Fact]
    public void LostCountdown_IsTheImmediateTwoNotTheSourceFive()
    {
        // 0x0046F4A8  c7 43 48 00 00 00 40  = mov [ebx+0x48], 0x40000000
        Assert.Equal(0x40000000u, RetailGameEndCountdown.LostCountdownBits);
        Assert.Equal(
            RetailGameEndCountdown.LostCountdownSeconds,
            BitConverter.UInt32BitsToSingle(RetailGameEndCountdown.LostCountdownBits));
        Assert.Equal(0x40000000u, Bits(2.0f));
        Assert.Equal(0x40A00000u, Bits(5.0f));
        Assert.NotEqual(
            RetailGameEndCountdown.LostCountdownBits,
            Bits(5.0f));
        Assert.Equal(
            2 * SimulationConstants.TicksPerSecond,
            RetailGameEndCountdown.LostTicks);
        Assert.NotEqual(
            5 * SimulationConstants.TicksPerSecond,
            RetailGameEndCountdown.LostTicks);
    }

    [Fact]
    public void WonCountdown_IsFiveForEveryWorldThatIsNotTheTwoSpecialCases()
    {
        // 0x0046F338  c7 43 48 00 00 a0 40  = mov [ebx+0x48], 0x40A00000
        // after failing cmp eax, 0x2E5 / 0x2E6 (worlds 741 and 742).
        // Level 100 is world 100, so it takes this arm.
        Assert.Equal(0x40A00000u, RetailGameEndCountdown.WonCountdownBits);
        Assert.Equal(
            RetailGameEndCountdown.WonCountdownSeconds,
            BitConverter.UInt32BitsToSingle(RetailGameEndCountdown.WonCountdownBits));
        Assert.Equal(
            5 * SimulationConstants.TicksPerSecond,
            RetailGameEndCountdown.WonTicks);
        Assert.False(RetailGameEndCountdown.UsesZeroWonCountdown(100));
        Assert.True(RetailGameEndCountdown.UsesZeroWonCountdown(741));
        Assert.True(RetailGameEndCountdown.UsesZeroWonCountdown(742));
        Assert.Equal(0x2E5, 741);
        Assert.Equal(0x2E6, 742);
    }

    [Fact]
    public void Level100MissionTiming_CarriesTheSameImmediates()
    {
        Assert.Equal(
            RetailGameEndCountdown.LostTicks,
            Level100MissionTiming.FailureCountdownTicks);
        Assert.Equal(
            RetailGameEndCountdown.WonTicks,
            Level100MissionTiming.SuccessCountdownTicks);
        Assert.Equal(
            RetailGameEndCountdown.LostTicks,
            Level100MissionTiming.FailureTerminalTicks(
                Level100MissionFailureReason.TutorialBroken));
    }

    /// <summary>
    /// A cold Level 100 that takes the released Broke-Tutorial loss must
    /// start the overlay on the 2.0 s store, not the source 5.0 s. The
    /// player path is the same <c>LevelLostString</c> the early-truck
    /// hazard posts; this test posts the named event rather than driving
    /// the Pulse Cannon so the countdown itself is the only variable.
    /// </summary>
    [Fact]
    public void TutorialBroken_StartsTheTwoSecondLostCountdown()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        var mission = new Level100Mission(
            actors,
            actors.GetThingRef("Player 1")!.Value);

        Assert.True(mission.SubmitInput(Level100MissionInput.BrokeTutorial));

        const int settleTicks = 100 * SimulationConstants.TicksPerSecond;
        for (int tick = 0;
             tick < settleTicks &&
                 mission.Snapshot.Outcome != Level100MissionOutcome.Lost;
             tick++)
        {
            mission.AdvanceTick(SimulationConstants.MaximumHull);
        }

        Assert.Equal(Level100MissionOutcome.Lost, mission.Snapshot.Outcome);
        Assert.Equal(
            Level100MissionFailureReason.TutorialBroken,
            mission.Snapshot.FailureReason);
        Assert.Equal(
            RetailGameEndCountdown.LostTicks,
            mission.Snapshot.TerminalTicksRemaining);
        Assert.NotEqual(
            5 * SimulationConstants.TicksPerSecond,
            mission.Snapshot.TerminalTicksRemaining);
    }
}
