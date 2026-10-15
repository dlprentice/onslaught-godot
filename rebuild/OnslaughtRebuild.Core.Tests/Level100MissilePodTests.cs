// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The jet Missile Pod's charge, Fire, burst and launch-slot state, from the
/// shipped records in <c>data/default physics.dat</c> (SHA-256
/// <c>e1fb3ded…</c>) and the RE lane's stores and burst-spawner contracts
/// (<c>reverse-engineering/game-mechanics/battle-engine-weapon-stores.md</c>,
/// <c>reverse-engineering/contracts/render-platform/ProjectileBurst__SpawnFromCurrentPreset__005069f0.md</c>).
/// The simulation-level burst, lock and seeking checks are in
/// <see cref="SimulationTests"/>.
/// </summary>
public sealed class Level100MissilePodTests
{
    private static float Charge(Level100PlayerWeaponRuntime weapons) =>
        BitConverter.UInt32BitsToSingle(weapons.PodSnapshot.ChargeBits);

    private static Level100PlayerWeaponRuntime JetWithPod()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        Assert.True(weapons.SelectNextActive(VehicleMode.Jet));
        Assert.Equal(Level100MissionWeapon.MissilePod, weapons.JetSelectedWeapon);
        return weapons;
    }

    [Fact]
    public void Charge_ReachesTheSalvoOnItsThirteenthCallAndStopsThere()
    {
        Level100PlayerWeaponRuntime weapons = JetWithPod();
        for (int call = 1; call <= 12; call++)
        {
            Assert.False(weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f));
            Assert.Equal(8.0f * call, Charge(weapons));
            Assert.Equal(5, weapons.LockParameters(Level100MissionWeapon.MissilePod).MaxLocks);
        }

        // 96 + 8 = 104 rounds to level 1, the salvo; 104 >= 100 is full.
        Assert.False(weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f));
        Assert.Equal(104.0f, Charge(weapons));
        Assert.Equal(10, weapons.LockParameters(Level100MissionWeapon.MissilePod).MaxLocks);
        Assert.False(weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f));
        Assert.Equal(104.0f, Charge(weapons));

        // Charging draws nothing from the pod's ammo store and does not stop
        // the walker's shields recharging.
        Assert.Equal(200.0f, BitConverter.UInt32BitsToSingle(weapons.StoresSnapshot.Store3Bits));
        Assert.True(weapons.StoresSnapshot.ShieldsRecharging);

        Assert.True(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None, 1.0f, out _));
        Assert.Equal(new Level100MissilePodSnapshot(0, 1, true, 0, -1, -1), weapons.PodSnapshot);
        Assert.Equal(10, weapons.PodMode.BurstSize);
    }

    [Fact]
    public void Fire_SetsTheModeBeforeItsReloadCheck()
    {
        Level100PlayerWeaponRuntime weapons = JetWithPod();
        for (int call = 0; call < 13; call++)
        {
            weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f);
        }
        Assert.True(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None, 1.0f, out _));
        weapons.StampReadyAt(Level100MissionWeapon.MissilePod, 1.0f);
        weapons.PodBurstCount = 3;
        Assert.True(weapons.PodIsFiring);

        // A second press inside the 0.8 s reload is refused, but it has
        // already rewritten the level from the empty charge: the rest of the
        // salvo now counts against the launcher's five.
        Assert.False(weapons.TryPrepareFire(VehicleMode.Jet, VehicleTransition.None, 1.5f, out _));
        Assert.Equal(0, weapons.PodSnapshot.ModeLevel);
        Assert.Equal(5, weapons.PodMode.BurstSize);
        weapons.PodBurstCount = 5;
        Assert.False(weapons.PodIsFiring);

        // Reload is 0x3f4ccccd after the burst's start, and strictly after.
        float readyAt = 1.0f + BitConverter.UInt32BitsToSingle(0x3f4ccccdu);
        Assert.Equal(BitConverter.SingleToUInt32Bits(readyAt),
            weapons.Snapshot.MissilePodReadyAtTimeBits);
        Assert.False(weapons.ReadyToFire(Level100MissionWeapon.MissilePod, readyAt));
        Assert.True(weapons.ReadyToFire(Level100MissionWeapon.MissilePod, 1.85f));
    }

    [Fact]
    public void LaunchCounters_StartAtTheFirstSlotAndWrapAfterFive()
    {
        Level100PlayerWeaponRuntime weapons = JetWithPod();
        (int Emitter, int AngleSlot)[] slots = Enumerable.Range(0, 7)
            .Select(_ => weapons.AdvancePodLaunchCounters())
            .ToArray();
        Assert.Equal([4, 3, 5, 2, 6, 4, 3], slots.Select(slot => slot.Emitter));
        Assert.Equal([0, 1, 2, 3, 4, 0, 1], slots.Select(slot => slot.AngleSlot));
        Assert.Equal(1, weapons.PodSnapshot.SequenceCounter);
        Assert.Equal(1, weapons.PodSnapshot.AngleCounter);
    }

    [Fact]
    public void LockParameters_AreTheRecordDefaultsForEveryOtherWeapon()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        foreach (Level100MissionWeapon weapon in new[]
                 {
                     Level100MissionWeapon.PulseCannonPod,
                     Level100MissionWeapon.MechTwinVulcanCannon,
                     Level100MissionWeapon.MechVulcanCannon,
                 })
        {
            Assert.Equal(Level100LockParameters.RecordDefaults, weapons.LockParameters(weapon));
        }

        Level100LockParameters launcher = weapons.LockParameters(Level100MissionWeapon.MissilePod);
        Assert.Equal(new Level100LockParameters(
            5,
            BitConverter.UInt32BitsToSingle(0x3e4ccccdu),
            BitConverter.UInt32BitsToSingle(0xbeb2b8c2u),
            100.0f,
            0x000e8400u), launcher);
    }

    [Fact]
    public void LockUnitMask_TakesPlanesVehiclesAndCannonsButNotBuildings()
    {
        uint mask = Level100MissilePod.Launcher.LockUnitMask;
        Assert.NotEqual(0u, Level100ThingClasses.Of("Target Drone").TypeMask & mask);
        Assert.NotEqual(0u, Level100ThingClasses.Of("Air Trainer").TypeMask & mask);
        Assert.NotEqual(0u, Level100ThingClasses.Of("Target Tank").TypeMask & mask);
        Assert.NotEqual(0u, Level100ThingClasses.Of("Target Truck").TypeMask & mask);
        Assert.NotEqual(0u, Level100ThingClasses.Of("SAT Turret").TypeMask & mask);
        Assert.Equal(0u, Level100ThingClasses.Of("Control Tower").TypeMask & mask);
        Assert.Equal(0u, Level100ThingClasses.Of("Forseti City Building 1").TypeMask & mask);
        Assert.Equal(0u, Level100ThingClasses.Of("Iceberg 1").TypeMask & mask);
    }

    [Fact]
    public void MorphAndWeaponChange_LoseThePodCharge()
    {
        Level100PlayerWeaponRuntime weapons = JetWithPod();
        weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f);
        weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f);
        Assert.Equal(16.0f, Charge(weapons));
        weapons.LoseCurrentWeaponChargesForMorph();
        Assert.Equal(0u, weapons.PodSnapshot.ChargeBits);

        weapons.AdvanceCharge(VehicleMode.Jet, VehicleTransition.None, 1.0f);
        Assert.True(weapons.SelectNextActive(VehicleMode.Jet));
        Assert.True(weapons.SelectNextActive(VehicleMode.Jet));
        Assert.Equal(Level100MissionWeapon.MissilePod, weapons.JetSelectedWeapon);
        Assert.Equal(0u, weapons.PodSnapshot.ChargeBits);
    }
}
