// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class Level100PlayerWeaponRuntimeTests
{
    [Fact]
    public void ResetConfiguration_SelectsFirstReleasedSlotAndActivatesAllWeapons()
    {
        var weapons = new Level100PlayerWeaponRuntime();

        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);
        Assert.Equal(Level100MissionWeapon.MechVulcanCannon, weapons.JetSelectedWeapon);
        Assert.Equal(2, weapons.CountActiveWeapons(VehicleMode.Walker));
        Assert.Equal(2, weapons.CountActiveWeapons(VehicleMode.Jet));
        Assert.Equal(Level100PlayerWeaponStateSnapshot.Initial, weapons.Snapshot);

        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, false);
        weapons.SetActive(Level100MissionWeapon.MissilePod, false);
        weapons.ResetConfiguration();

        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);
        Assert.Equal(Level100MissionWeapon.MechVulcanCannon, weapons.JetSelectedWeapon);
        Assert.Equal(2, weapons.CountActiveWeapons(VehicleMode.Walker));
        Assert.Equal(2, weapons.CountActiveWeapons(VehicleMode.Jet));
        Assert.Equal(Level100PlayerWeaponStateSnapshot.Initial, weapons.Snapshot);
    }

    [Theory]
    [InlineData(VehicleMode.Walker, Level100MissionWeapon.PulseCannonPod, 0x3f8ccccdu)]
    [InlineData(VehicleMode.Walker, Level100MissionWeapon.MechTwinVulcanCannon, 0x3f866666u)]
    [InlineData(VehicleMode.Jet, Level100MissionWeapon.MechVulcanCannon, 0x3f866666u)]
    public void FireAndHeldReadiness_RefuseEqualityAndAcceptTheNextFloat(
        VehicleMode mode, Level100MissionWeapon weapon, uint readyBits)
    {
        var weapons = new Level100PlayerWeaponRuntime();
        if (weapon == Level100MissionWeapon.MechTwinVulcanCannon)
            Assert.True(weapons.SelectNextActive(VehicleMode.Walker));
        weapons.StampReadyAt(weapon, 1f);
        float ready = BitConverter.UInt32BitsToSingle(readyBits);

        Assert.False(weapons.TryPrepareFire(mode, VehicleTransition.None,
            MathF.BitDecrement(ready), out _));
        Assert.False(weapons.TryPrepareFire(mode, VehicleTransition.None, ready, out _));
        Assert.False(weapons.TryPrepareFire(mode, VehicleTransition.None, float.NaN, out _));
        Assert.False(weapons.AdvanceCharge(mode, VehicleTransition.None, ready));
        Assert.Equal(0u, weapons.PulseCannonChargeBits);
        Assert.True(weapons.TryPrepareFire(mode, VehicleTransition.None,
            MathF.BitIncrement(ready), out _));
        Assert.Equal(weapon != Level100MissionWeapon.PulseCannonPod,
            weapons.AdvanceCharge(mode, VehicleTransition.None, MathF.BitIncrement(ready)));
        Assert.Equal(weapon == Level100MissionWeapon.PulseCannonPod ? 0x41200000u : 0u,
            weapons.PulseCannonChargeBits);
    }

    [Fact]
    public void RefusedPulseFire_SamplesLargeThenClearsChargeBeforeTestingReadyTime()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        for (int step = 0; step < 10; step++)
            Assert.False(weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 1f));
        Assert.Equal(0x42c80000u, weapons.PulseCannonChargeBits);

        // Isolate shared Fire's ordering with charge retained at a blocked
        // timestamp. Ordinary input cannot charge through this ready gate.
        weapons.StampReadyAt(Level100MissionWeapon.PulseCannonPod, 1f,
            Level100ProjectileKind.MechPulseBoltLarge);
        Assert.Equal(0x3fc00000u, weapons.Snapshot.PulseReadyAtTimeBits);
        Assert.False(weapons.TryPrepareFire(VehicleMode.Walker, VehicleTransition.None,
            1.5f, out Level100ProjectileKind sampled));
        Assert.Equal(Level100ProjectileKind.MechPulseBoltLarge, sampled);
        Assert.Equal(0u, weapons.PulseCannonChargeBits);
        Assert.Equal(0x3fc00000u, weapons.Snapshot.PulseReadyAtTimeBits);

        Assert.True(weapons.TryPrepareFire(VehicleMode.Walker, VehicleTransition.None,
            MathF.BitIncrement(1.5f), out Level100ProjectileKind nextTap));
        Assert.Equal(Level100ProjectileKind.MechPulseBoltMedium, nextTap);
    }

    [Fact]
    public void ReadyTimes_BelongToEachWeaponAcrossSelectionAndReset()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.StampReadyAt(Level100MissionWeapon.PulseCannonPod, 10f);
        Assert.True(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.True(weapons.TryPrepareFire(VehicleMode.Walker, VehicleTransition.None, 10f, out _));
        weapons.StampReadyAt(Level100MissionWeapon.MechTwinVulcanCannon, 20f);
        Assert.True(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None, 10f, out _));
        weapons.StampReadyAt(Level100MissionWeapon.MechVulcanCannon, 30f);

        Assert.Equal(new Level100PlayerWeaponStateSnapshot(
            0, 0x4121999a, 0x41a06666, 0x41f06666, 0xc3480000), weapons.Snapshot);
        Assert.True(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.False(weapons.TryPrepareFire(VehicleMode.Walker, VehicleTransition.None, 10f, out _));
        weapons.ResetConfiguration();
        Assert.Equal(Level100PlayerWeaponStateSnapshot.Initial, weapons.Snapshot);
    }

    [Fact]
    public void ChangingAnotherWeapon_PreservesPulseChargeUntilPulseIsSelectedAgain()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 1f);
        Assert.True(weapons.SelectNextActive(VehicleMode.Jet));
        Assert.Equal(0x41200000u, weapons.PulseCannonChargeBits);
        Assert.True(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.Equal(0x41200000u, weapons.PulseCannonChargeBits);
        Assert.True(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.Equal(0u, weapons.PulseCannonChargeBits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StateHash_RecordsEveryNonInitialWeaponWordIncludingExpiredAndSignedZero(bool legacyEnvelope)
    {
        WorldSnapshot initial = new Simulation(0x1234u, Level100TestActorDefinitions.Create()).Snapshot;
        if (legacyEnvelope) initial = Level100TestActorDefinitions.LegacyHashEnvelope(initial);
        Assert.Equal(Level100PlayerWeaponStateSnapshot.Initial, initial.Level100PlayerWeaponState);
        const int schemaOffset = 23; // ASCII ONSLAUGHT-REBUILD-STATE.
        // Every world built in the retail load order carries unit callbacks,
        // which select schema 52.
        Assert.Equal(legacyEnvelope ? 42 : 52, BitConverter.ToInt32(StateHasher.GetCanonicalBytes(initial), schemaOffset));
        string baseline = StateHasher.ComputeHex(initial);
        Level100PlayerWeaponStateSnapshot weapons = initial.Level100PlayerWeaponState;
        Level100PlayerWeaponStateSnapshot[] variants =
        [
            weapons with { PulseChargeBits = 0x41200000 },
            weapons with { PulseChargeBits = 0x80000000 },
            weapons with { PulseReadyAtTimeBits = 0xc3480001 },
            weapons with { TwinVulcanReadyAtTimeBits = 0xc3480001 },
            weapons with { MechVulcanReadyAtTimeBits = 0xc3480001 },
            weapons with { MissilePodReadyAtTimeBits = 0xc3480001 },
        ];
        foreach (Level100PlayerWeaponStateSnapshot variant in variants)
        {
            WorldSnapshot changed = initial with { Level100PlayerWeaponState = variant };
            Assert.Equal(legacyEnvelope ? 44 : 52, BitConverter.ToInt32(StateHasher.GetCanonicalBytes(changed), schemaOffset));
            Assert.NotEqual(baseline, StateHasher.ComputeHex(changed));
        }
        Assert.Equal(variants.Length, variants.Select(variant => StateHasher.ComputeHex(
            initial with { Level100PlayerWeaponState = variant })).Distinct().Count());
    }

    [Theory]
    [InlineData(
        VehicleMode.Walker,
        Level100MissionWeapon.PulseCannonPod,
        Level100MissionWeapon.MechTwinVulcanCannon)]
    [InlineData(
        VehicleMode.Jet,
        Level100MissionWeapon.MechVulcanCannon,
        Level100MissionWeapon.MissilePod)]
    public void DisablingSelectedWeapon_SelectsNextActiveSlot(
        VehicleMode mode,
        Level100MissionWeapon selected,
        Level100MissionWeapon expected)
    {
        var weapons = new Level100PlayerWeaponRuntime();

        weapons.SetActive(selected, false);

        Assert.Equal(expected, weapons.GetCurrentWeapon(mode));
        Assert.True(weapons.IsActive(expected));
        Assert.Equal(1, weapons.CountActiveWeapons(mode));
    }

    [Fact]
    public void EnablingWeapon_DoesNotStealCurrentSelection()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, false);
        Assert.Equal(
            Level100MissionWeapon.MechTwinVulcanCannon,
            weapons.WalkerSelectedWeapon);

        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, true);

        Assert.Equal(
            Level100MissionWeapon.MechTwinVulcanCannon,
            weapons.WalkerSelectedWeapon);
        Assert.Equal(2, weapons.CountActiveWeapons(VehicleMode.Walker));
    }

    [Theory]
    [InlineData(
        VehicleMode.Walker,
        Level100MissionWeapon.MechTwinVulcanCannon,
        Level100MissionWeapon.PulseCannonPod)]
    [InlineData(
        VehicleMode.Jet,
        Level100MissionWeapon.MissilePod,
        Level100MissionWeapon.MechVulcanCannon)]
    public void ManualCycle_SelectsNextActiveReleasedSlotAndWraps(
        VehicleMode mode,
        Level100MissionWeapon next,
        Level100MissionWeapon wrapped)
    {
        var weapons = new Level100PlayerWeaponRuntime();

        Assert.True(weapons.SelectNextActive(mode));
        Assert.Equal(next, weapons.GetCurrentWeapon(mode));
        Assert.True(weapons.SelectNextActive(mode));
        Assert.Equal(wrapped, weapons.GetCurrentWeapon(mode));
    }

    [Fact]
    public void ManualCycle_WithOneActiveWeapon_DoesNotMoveSelection()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.SetActive(Level100MissionWeapon.MechTwinVulcanCannon, false);

        Assert.False(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);
    }

    [Fact]
    public void NoActiveAlternative_PreservesBoundedCurrentSlot()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        weapons.SetActive(Level100MissionWeapon.MechTwinVulcanCannon, false);
        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, false);

        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);
        Assert.False(weapons.IsActive(weapons.WalkerSelectedWeapon));
        Assert.Equal(0, weapons.CountActiveWeapons(VehicleMode.Walker));
    }

    [Fact]
    public void ReleasedLevel100EnableDisableSequence_RetainsSelectionHistory()
    {
        var weapons = new Level100PlayerWeaponRuntime();

        weapons.SetActive(Level100MissionWeapon.MechTwinVulcanCannon, false);
        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, false);
        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, true);
        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);

        weapons.SetActive(Level100MissionWeapon.MechTwinVulcanCannon, true);
        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, false);
        Assert.Equal(
            Level100MissionWeapon.MechTwinVulcanCannon,
            weapons.WalkerSelectedWeapon);

        weapons.SetActive(Level100MissionWeapon.PulseCannonPod, true);
        Assert.Equal(
            Level100MissionWeapon.MechTwinVulcanCannon,
            weapons.WalkerSelectedWeapon);
    }

    [Fact]
    public void StateHash_DistinguishesBothSelectedSlots()
    {
        WorldSnapshot state = new Simulation(
            0x1234U,
            Level100TestActorDefinitions.Create()).Snapshot;
        string baseline = StateHasher.ComputeHex(state);

        Assert.NotEqual(
            baseline,
            StateHasher.ComputeHex(state with
            {
                Level100WalkerSelectedWeapon =
                    Level100MissionWeapon.MechTwinVulcanCannon,
            }));
        Assert.NotEqual(
            baseline,
            StateHasher.ComputeHex(state with
            {
                Level100JetSelectedWeapon = Level100MissionWeapon.MissilePod,
            }));
        Assert.NotEqual(
            baseline,
            StateHasher.ComputeHex(state with
            {
                TwinVulcanReloadTicksRemaining = 1,
            }));
    }
}
