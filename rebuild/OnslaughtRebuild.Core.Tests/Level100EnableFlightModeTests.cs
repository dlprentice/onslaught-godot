// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Beat 6 <c>player.EnableFlightMode()</c> is the training path
/// that stores <c>mov [ecx+0x58c], 1</c>. Isolated
/// <see cref="RetailEnableFlightMode.Enable"/> names that
/// immediate without takeoff. Isolated
/// <see cref="WorldSnapshot.Level100FlightEnabled"/> names the
/// rebuild bool and still lets GrantFlight transform if this
/// store is skipped. Disable's clear / morph / wrapper
/// <c>test [ecx+0x34], 8</c> stay unclaimed. ChargeWeapon /
/// ReadyToCharge / Charged-2 stay unclaimed. Live
/// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </summary>
public sealed class Level100EnableFlightModeTests
{
    /// <summary>
    /// <c>GrantFlightLegForMeasurement</c> applies the same
    /// Enable the LevelScript posts at beat 6. Isolated
    /// <c>Level100FlightEnabled</c> = true still passes if the
    /// <c>+0x58c</c> store is skipped. Mutation: skip
    /// <see cref="RetailEnableFlightMode.Enable"/> so takeoff
    /// is rejected, or increment so a second Enable becomes 2.
    /// </summary>
    [Fact]
    public void GrantFlightLeg_StoresCBattleEnginePlus58CSoToggleModeCanTakeOff()
    {
        var simulation = new Simulation(
            1u,
            Level100TestActorDefinitions.Create(),
            new Level100TutorialProgress(true, true, true, true));
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(simulation.Snapshot.Level100PlayerControlEnabled);
        Assert.False(simulation.Snapshot.Level100FlightEnabled);
        Assert.Equal(
            RetailEnableFlightMode.FlagDisabled,
            simulation.Level100FlightModeFlag);

        WorldSnapshot rejected = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));
        Assert.Equal(VehicleTransition.None, rejected.Transition);
        Assert.Contains(
            rejected.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.TransformRejected);
        Assert.Equal(
            RetailEnableFlightMode.FlagDisabled,
            simulation.Level100FlightModeFlag);

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        Assert.True(simulation.Snapshot.Level100FlightEnabled);
        Assert.Equal(
            RetailEnableFlightMode.FlagEnabled,
            simulation.Level100FlightModeFlag);
        Assert.Equal(
            RetailEnableFlightMode.Enable(RetailEnableFlightMode.FlagDisabled),
            simulation.Level100FlightModeFlag);
        Assert.NotEqual(2, simulation.Level100FlightModeFlag);

        WorldSnapshot morphing = simulation.Step(
            new SimInput(0, 0, SimActions.ToggleMode));
        Assert.Equal(VehicleTransition.WalkerToJet, morphing.Transition);
        Assert.Contains(
            morphing.AquilaFlightEventLog,
            item => item.Kind == AquilaFlightEvents.WalkerToJetStarted);
        Assert.Equal(
            RetailEnableFlightMode.FlagEnabled,
            simulation.Level100FlightModeFlag);
        Assert.Equal(
            RetailEnableFlightMode.Enable(RetailEnableFlightMode.FlagEnabled),
            simulation.Level100FlightModeFlag);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }
}
