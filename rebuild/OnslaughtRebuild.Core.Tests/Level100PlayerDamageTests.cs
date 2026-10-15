// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100PlayerDamageTests
{
    private static readonly Level100TutorialProgress CompletedTutorialSlots =
        new(Introduction: true, PulseCannon: true, VulcanCannon: true, StatusBars: true);

    [Fact]
    public void Apply_ProjectsTheObservedLevel521FiftyMilliWitnessAtCoreResolution()
    {
        Level100PlayerDamageResult result = Apply(
            life: 23_444,
            energy: 4_940,
            shield: 4_940,
            augment: 2_940,
            active: false,
            incoming: 50,
            damageShields: true,
            isWalker: true);

        Assert.Equal(
            new Level100PlayerDamageState(23_443, 4_891, 4_891, 2_989, false),
            result.State);
        Assert.Equal(49, result.ShieldAbsorbedMilliLife);
        Assert.Equal(1, result.LifeDamageMilliLife);
        Assert.False(result.RequestsDeath);
    }

    [Theory]
    [InlineData(200, 19_996, 7_804, 196, 196, 4)]
    [InlineData(2_500, 19_950, 5_550, 2_450, 2_450, 50)]
    public void Apply_RoutesKnownIncomingAmountsThroughNinetyEightPercentShields(
        int incoming,
        int expectedLife,
        int expectedShield,
        int expectedAugment,
        int expectedAbsorbed,
        int expectedLifeDamage)
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            8_000,
            8_000,
            0,
            false,
            incoming,
            damageShields: true,
            isWalker: true);

        Assert.Equal(expectedLife, result.State.LifeMilli);
        Assert.Equal(expectedShield, result.State.ShieldMilli);
        Assert.Equal(expectedShield, result.State.EnergyMilli);
        Assert.Equal(expectedAugment, result.State.AugmentChargeMilli);
        Assert.Equal(expectedAbsorbed, result.ShieldAbsorbedMilliLife);
        Assert.Equal(expectedLifeDamage, result.LifeDamageMilliLife);
        Assert.False(result.RequestsDeath);
    }

    [Fact]
    public void Apply_ForsetiAggregateTopologyRemainsAVisibleBoundaryQuestion()
    {
        Level100PlayerDamageState start =
            new(20_000, 2_470, 2_470, 0, false);

        Level100PlayerDamageResult aggregate = Level100PlayerDamage.Apply(
            start,
            incomingDamageMilliLife: 2_500,
            damageShields: true,
            isWalker: true);
        Level100PlayerDamageResult round = Level100PlayerDamage.Apply(
            start,
            incomingDamageMilliLife: 2_000,
            damageShields: true,
            isWalker: true);
        Level100PlayerDamageResult roundThenExplosion = Level100PlayerDamage.Apply(
            round.State,
            incomingDamageMilliLife: 500,
            damageShields: true,
            isWalker: true);

        Assert.Equal(
            new Level100PlayerDamageState(19_970, 0, 0, 2_470, false),
            aggregate.State);
        Assert.Equal(
            new Level100PlayerDamageState(19_950, 20, 20, 2_450, false),
            roundThenExplosion.State);
        Assert.NotEqual(aggregate.State, roundThenExplosion.State);
    }

    [Fact]
    public void Apply_InsufficientShieldsAbsorbOneForOneBeforeLife()
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            1_000,
            1_000,
            500,
            false,
            2_500,
            damageShields: true,
            isWalker: true);

        Assert.Equal(
            new Level100PlayerDamageState(18_500, 0, 0, 1_500, false),
            result.State);
        Assert.Equal(1_000, result.ShieldAbsorbedMilliLife);
        Assert.Equal(1_500, result.LifeDamageMilliLife);
    }

    [Fact]
    public void Apply_StrictOverflowArmDistinguishesEqualAndInsufficientShields()
    {
        Level100PlayerDamageResult equal = Apply(
            100, 50, 50, 0, false, 50, true, true);
        Level100PlayerDamageResult insufficient = Apply(
            100, 49, 49, 0, false, 50, true, true);

        Assert.Equal(
            new Level100PlayerDamageState(99, 1, 1, 49, false),
            equal.State);
        Assert.Equal(
            new Level100PlayerDamageState(99, 0, 0, 49, false),
            insufficient.State);
    }

    [Fact]
    public void Apply_ShieldBypassLeavesShieldAndAugmentUntouched()
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            8_000,
            8_000,
            400,
            false,
            200,
            damageShields: false,
            isWalker: true);

        Assert.Equal(
            new Level100PlayerDamageState(19_800, 8_000, 8_000, 400, false),
            result.State);
        Assert.Equal(0, result.ShieldAbsorbedMilliLife);
        Assert.Equal(200, result.LifeDamageMilliLife);
    }

    [Fact]
    public void Apply_JetShieldBypassDoesNotSynchronizeEnergy()
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            3_000,
            8_000,
            400,
            false,
            200,
            damageShields: false,
            isWalker: false);

        Assert.Equal(19_800, result.State.LifeMilli);
        Assert.Equal(3_000, result.State.EnergyMilli);
        Assert.Equal(8_000, result.State.ShieldMilli);
        Assert.Equal(400, result.State.AugmentChargeMilli);
    }

    [Fact]
    public void Apply_ShieldedDamageWithZeroJetShieldFallsEntirelyOnLife()
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            3_000,
            0,
            400,
            false,
            200,
            damageShields: true,
            isWalker: false);

        Assert.Equal(
            new Level100PlayerDamageState(19_800, 3_000, 0, 400, false),
            result.State);
        Assert.Equal(0, result.ShieldAbsorbedMilliLife);
        Assert.Equal(200, result.LifeDamageMilliLife);
    }

    [Fact]
    public void Apply_ActiveAugmentDoesNotGainAbsorbedShieldCharge()
    {
        Level100PlayerDamageResult result = Apply(
            20_000,
            8_000,
            8_000,
            700,
            true,
            200,
            damageShields: true,
            isWalker: true);

        Assert.Equal(700, result.State.AugmentChargeMilli);
        Assert.True(result.State.AugmentActive);
    }

    [Fact]
    public void Apply_DeathIsStrictlyBelowZero()
    {
        Level100PlayerDamageResult exactZero = Apply(
            20_000, 8_000, 8_000, 0, false, 28_000, true, true);
        Level100PlayerDamageResult negative = Apply(
            20_000, 8_000, 8_000, 0, false, 28_001, true, true);

        Assert.Equal(0, exactZero.State.LifeMilli);
        Assert.False(exactZero.RequestsDeath);
        Assert.Equal(-1, negative.State.LifeMilli);
        Assert.True(negative.RequestsDeath);
    }

    [Fact]
    public void Apply_AlreadyNegativeLifeOnlyCapsAndSynchronizesResources()
    {
        Level100PlayerDamageResult result = Apply(
            -1,
            900,
            800,
            12_000,
            false,
            200,
            damageShields: true,
            isWalker: true);

        Assert.Equal(
            new Level100PlayerDamageState(-1, 800, 800, 10_000, false),
            result.State);
        Assert.Equal(0, result.ShieldAbsorbedMilliLife);
        Assert.Equal(0, result.LifeDamageMilliLife);
        Assert.False(result.RequestsDeath);
    }

    [Fact]
    public void Apply_RejectsAnUnrepresentableSufficientShieldSplit()
    {
        Assert.Throws<InvalidOperationException>(() => Apply(
            20_000,
            8_000,
            8_000,
            0,
            false,
            137,
            damageShields: true,
            isWalker: true));
    }

    [Fact]
    public void DamageCapsChargeButMoveOwnsActivationAndDrain()
    {
        Level100PlayerDamageResult damage = Apply(
            20_000,
            8_000,
            8_000,
            9_900,
            false,
            150,
            damageShields: true,
            isWalker: true);

        Assert.Equal(10_000, damage.State.AugmentChargeMilli);
        Assert.False(damage.State.AugmentActive);

        Level100PlayerDamageState activated =
            Level100PlayerDamage.AdvanceAugment(damage.State);
        Assert.Equal(10_000, activated.AugmentChargeMilli);
        Assert.True(activated.AugmentActive);

        Level100PlayerDamageState draining =
            Level100PlayerDamage.AdvanceAugment(activated);
        Assert.Equal(9_990, draining.AugmentChargeMilli);
        Assert.True(draining.AugmentActive);

        Level100PlayerDamageState expired =
            Level100PlayerDamage.AdvanceAugment(draining with
            {
                AugmentChargeMilli = SimulationConstants.AugmentDrainPerTick,
            });
        Assert.Equal(0, expired.AugmentChargeMilli);
        Assert.False(expired.AugmentActive);
    }

    [Fact]
    public void Simulation_AppliesDamageBeforeWalkerRegenerationAndEmitsTheContract()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot state = simulation.Step(
            SimInput.Idle,
            [new Level100PlayerDamageFact(200)]);

        Assert.Equal(19_996, state.Hull);
        Assert.Equal(7_854, state.Shield);
        Assert.Equal(7_854, state.Energy);
        Assert.Equal(196, state.AugmentCharge);
        Assert.False(state.AugmentActive);
        Level100PlayerDamageEvent damage = Assert.Single(state.Level100PlayerDamageEvents);
        Assert.Equal(Level100PlayerDamageSource.ExternalFact, damage.Source);
        Assert.Equal(200, damage.IncomingDamageMilliLife);
        Assert.Equal(196, damage.ShieldAbsorbedMilliLife);
        Assert.Equal(4, damage.LifeDamageMilliLife);
        Assert.False(damage.RequestsDeath);

        string hash = StateHasher.ComputeHex(state);
        Assert.NotEqual(hash, StateHasher.ComputeHex(state with
        {
            AugmentCharge = state.AugmentCharge + 1,
        }));
        Assert.NotEqual(hash, StateHasher.ComputeHex(state with
        {
            Level100PlayerDamageEvents = Array.Empty<Level100PlayerDamageEvent>(),
        }));
        Assert.Empty(state.Level100DamageFlashes);
    }

    [Fact]
    public void ActorRoundDamage_RetainsTheReleasedDirectionalFlashListAndExpiryLaw()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot before = simulation.Snapshot;
        Level100ActorSnapshot player = Player(before);
        Level100ActorId ownerActorId = before.Level100Actors.Actors
            .First(actor => actor.ActorId != player.ActorId)
            .ActorId;
        var source = new SimVector3(
            player.Pose.PositionMillimeters.X + 1_000,
            player.Pose.PositionMillimeters.Y,
            player.Pose.PositionMillimeters.Z);

        for (int hit = 0; hit < 16; hit++)
        {
            simulation.QueueActorRoundImpactForMeasurement(
                ownerActorId,
                Level100ActorRoundKind.Blaster,
                source);
        }

        WorldSnapshot damaged = simulation.Step(SimInput.Idle);

        Assert.Equal(16, damaged.Level100PlayerDamageEvents.Count);
        Assert.All(
            damaged.Level100PlayerDamageEvents,
            damage => Assert.Equal(Level100PlayerDamageSource.ActorRound, damage.Source));
        Assert.Equal(
            SimulationConstants.Level100DamageFlashCapacity,
            damaged.Level100DamageFlashes.Count);
        Assert.All(
            damaged.Level100DamageFlashes,
            flash =>
            {
                // Source is +X from the player. Retail stores current yaw minus
                // the source yaw, so 509830 - (-pi/2) = 2080626 microradians.
                Assert.Equal(2_080_626, flash.RelativeYawMicroRad);
                Assert.Equal(damaged.Tick, flash.StartTick);
            });

        string hash = StateHasher.ComputeHex(damaged);
        Assert.NotEqual(hash, StateHasher.ComputeHex(damaged with
        {
            Level100DamageFlashes = Array.Empty<Level100DamageFlashSnapshot>(),
        }));

        WorldSnapshot atZeroIntensity = damaged;
        for (int tick = 0;
             tick < SimulationConstants.Level100DamageFlashLifetimeTicks;
             tick++)
        {
            atZeroIntensity = simulation.Step(SimInput.Idle);
        }

        // The render intensity is now zero, but strict start+2s<now means the
        // retained list has not removed an entry yet.
        Assert.Equal(
            SimulationConstants.Level100DamageFlashCapacity,
            atZeroIntensity.Level100DamageFlashes.Count);
        Assert.Equal(
            damaged.Tick + SimulationConstants.Level100DamageFlashLifetimeTicks,
            atZeroIntensity.Tick);

        WorldSnapshot firstCleanup = simulation.Step(SimInput.Idle);
        Assert.Equal(
            SimulationConstants.Level100DamageFlashCapacity - 1,
            firstCleanup.Level100DamageFlashes.Count);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    public void WalkerRecharge_UsesThePristinePointThreeSecondGroundGate(
        int ticksSinceGroundContact,
        bool expected)
    {
        Assert.Equal(6, SimulationConstants.WalkerRechargeGroundContactTicks);
        Assert.Equal(
            expected,
            Simulation.CanRechargeWalkerEnergy(ticksSinceGroundContact));
    }

    [Fact]
    public void Simulation_LeavesExactZeroAliveButDestroysNegativeLife()
    {
        Simulation exactZero = CreatePlayingSimulation();
        WorldSnapshot zero = exactZero.Step(
            SimInput.Idle,
            [new Level100PlayerDamageFact(28_000)]);
        Assert.Equal(0, zero.Hull);
        Assert.Equal(
            Level100ActorLifecycle.Alive,
            Player(zero).Lifecycle);
        Assert.False(Assert.Single(zero.Level100PlayerDamageEvents).RequestsDeath);

        Simulation negative = CreatePlayingSimulation();
        WorldSnapshot dead = negative.Step(
            SimInput.Idle,
            [new Level100PlayerDamageFact(28_001)]);
        Assert.Equal(0, dead.Hull);
        Assert.Equal(
            Level100ActorLifecycle.Destroyed,
            Player(dead).Lifecycle);
        Assert.True(Assert.Single(dead.Level100PlayerDamageEvents).RequestsDeath);
    }

    [Fact]
    public void Simulation_RejectsUnrepresentableFactsBeforeMutatingTheTick()
    {
        Simulation simulation = CreatePlayingSimulation();
        WorldSnapshot before = simulation.Snapshot;

        Assert.Throws<InvalidOperationException>(() => simulation.Step(
            SimInput.Idle,
            [new Level100PlayerDamageFact(137)]));

        Assert.Equal(before.Tick, simulation.Snapshot.Tick);
        Assert.Equal(
            StateHasher.ComputeHex(before),
            StateHasher.ComputeHex(simulation.Snapshot));
    }

    private static Level100PlayerDamageResult Apply(
        int life,
        int energy,
        int shield,
        int augment,
        bool active,
        int incoming,
        bool damageShields,
        bool isWalker) => Level100PlayerDamage.Apply(
            new Level100PlayerDamageState(life, energy, shield, augment, active),
            incoming,
            damageShields,
            isWalker);

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

    private static Level100ActorSnapshot Player(WorldSnapshot state) =>
        state.Level100Actors.Actors.Single(actor =>
            actor.ThingTypeMask == Level100ReleasedThingTypeMasks.BattleEngine);
}
