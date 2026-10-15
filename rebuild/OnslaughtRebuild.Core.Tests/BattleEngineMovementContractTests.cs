// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class BattleEngineMovementContractTests
{
    private static readonly Level100TutorialProgress CompletedTutorialSlots =
        new(Introduction: true, PulseCannon: true, VulcanCannon: true, StatusBars: true);

    [Fact]
    public void WalkerYaw_UsesTheShippedAquilaGroundTurnRate()
    {
        Simulation simulation = CreatePlayingSimulation();

        WorldSnapshot state = simulation.Step(new SimInput(0, 0, LookX: 1));

        Assert.Equal(13_333, state.WalkerYawVelocityMicroRadPerTick);
    }

    [Fact]
    public void WalkerTouchdown_AppliesNormalIncidenceDamageAndVelocityResponse()
    {
        Simulation simulation = CreatePlayingSimulation();
        const int flatGroundX = 10_000;
        const int flatGroundZ = 10_000;
        int ground = Level100Terrain.Instance.SampleGroundElevationMillimeters(
            new SimVector2(flatGroundX, flatGroundZ));
        Assert.Equal(
            SimVector2.Zero,
            Level100Terrain.Instance.SampleGroundGradientPermille(
                new SimVector2(flatGroundX, flatGroundZ)));
        simulation.SetAirborneWalkerContactStateForMeasurement(
            new SimVector3(
                flatGroundX,
                ground + Level100Terrain.WalkerCenterOfGravityMillimeters + 1,
                flatGroundZ),
            new SimVector3(0, -490, 0));

        WorldSnapshot state = simulation.Step(SimInput.Idle);

        Assert.True(state.PlayerOnGround);
        Assert.Equal(500, state.GroundImpactSpeedMillimetersPerTick);
        Assert.Equal(12_000, state.Hull);
        Assert.Equal(SimulationConstants.MaximumShield, state.Shield);
        Assert.Equal(SimVector2.Zero, state.PlayerVelocity);
        Assert.Equal(0, state.PlayerVerticalVelocityMillimetersPerTick);
        Level100PlayerDamageEvent damage = Assert.Single(state.Level100PlayerDamageEvents);
        Assert.Equal(Level100PlayerDamageSource.GroundImpact, damage.Source);
        Assert.Equal(8_000, damage.IncomingDamageMilliLife);
        Assert.Equal(0, damage.ShieldAbsorbedMilliLife);
        Assert.Equal(8_000, damage.LifeDamageMilliLife);
        Assert.Contains(
            state.AquilaFlightEventLog,
            item => item.Kind ==
                AquilaFlightEvents.GroundImpactDamageThresholdCrossed);
    }

    [Fact]
    public void GroundImpact_UsesStrictStateSpecificThresholdsAndJetLowSpeedRetention()
    {
        Simulation.TerrainGroundImpactResponse walkerAtThreshold =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: true,
                isJetState: false,
                walkerDashActive: false,
                new SimVector3(0, -400, 0),
                SimVector2.Zero);
        Simulation.TerrainGroundImpactResponse walkerAboveThreshold =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: true,
                isJetState: false,
                walkerDashActive: false,
                new SimVector3(1, -400, 0),
                SimVector2.Zero);
        Simulation.TerrainGroundImpactResponse jetAtThreshold =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: false,
                isJetState: true,
                walkerDashActive: false,
                new SimVector3(120, -160, 0),
                SimVector2.Zero);
        Simulation.TerrainGroundImpactResponse jetAboveThreshold =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: false,
                isJetState: true,
                walkerDashActive: false,
                new SimVector3(1, -200, 0),
                SimVector2.Zero);
        Simulation.TerrainGroundImpactResponse morphAtLowSpeed =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: false,
                isJetState: false,
                walkerDashActive: false,
                new SimVector3(120, -160, 0),
                SimVector2.Zero);

        Assert.False(walkerAtThreshold.DamageThresholdCrossed);
        Assert.Equal(new SimVector3(0, -400, 0), walkerAtThreshold.Velocity);
        Assert.True(walkerAboveThreshold.DamageThresholdCrossed);
        Assert.False(jetAtThreshold.DamageThresholdCrossed);
        Assert.Equal(new SimVector3(108, -144, 0), jetAtThreshold.Velocity);
        Assert.True(jetAboveThreshold.DamageThresholdCrossed);
        Assert.Equal(new SimVector3(120, -160, 0), morphAtLowSpeed.Velocity);
    }

    [Fact]
    public void GroundImpact_SquaresSurfaceIncidenceForDamageAndWholeVelocityRetention()
    {
        Simulation.TerrainGroundImpactResponse response =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: true,
                isJetState: false,
                walkerDashActive: false,
                new SimVector3(300, -400, 0),
                SimVector2.Zero);

        Assert.Equal(500, response.SpeedMillimetersPerTick);
        Assert.True(response.DamageThresholdCrossed);
        Assert.Equal(5_120, response.DamageMilliLife);
        Assert.Equal(new SimVector3(108, -144, 0), response.Velocity);
    }

    [Theory]
    [InlineData(1_000, 0, 7_840, 6, -8, 0)]
    [InlineData(-1_000, 0, 160, 294, -392, 0)]
    public void GroundImpact_UsesSignedTerrainGradientForIncidence(
        int gradientX,
        int gradientZ,
        int expectedDamage,
        int expectedVelocityX,
        int expectedVelocityY,
        int expectedVelocityZ)
    {
        Simulation.TerrainGroundImpactResponse response =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: true,
                isJetState: false,
                walkerDashActive: false,
                new SimVector3(300, -400, 0),
                new SimVector2(gradientX, gradientZ));

        Assert.Equal(500, response.SpeedMillimetersPerTick);
        Assert.True(response.DamageThresholdCrossed);
        Assert.Equal(expectedDamage, response.DamageMilliLife);
        Assert.Equal(
            new SimVector3(
                expectedVelocityX,
                expectedVelocityY,
                expectedVelocityZ),
            response.Velocity);
    }

    [Fact]
    public void GroundImpact_DashActiveWalkerSkipsSelfDamageAndSourceVelocityResponse()
    {
        var inputVelocity = new SimVector3(300, -400, 0);

        Simulation.TerrainGroundImpactResponse response =
            Simulation.ResolveTerrainGroundImpact(
                isWalkerState: true,
                isJetState: false,
                walkerDashActive: true,
                inputVelocity,
                SimVector2.Zero);

        Assert.Equal(500, response.SpeedMillimetersPerTick);
        Assert.False(response.DamageThresholdCrossed);
        Assert.Equal(0, response.DamageMilliLife);
        Assert.Equal(inputVelocity, response.Velocity);
    }

    private static Simulation CreatePlayingSimulation()
    {
        var simulation = new Simulation(
            1,
            Level100TestActorDefinitions.Create(),
            CompletedTutorialSlots);
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        Assert.True(simulation.Snapshot.Level100PlayerControlEnabled);
        return simulation;
    }
}
