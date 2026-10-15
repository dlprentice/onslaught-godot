// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The Aquila's weapon stores and the Battle Engine shake, from the RE lane's
/// contracts <c>reverse-engineering/game-mechanics/battle-engine-weapon-stores.md</c>
/// and the burst-spawner contract, with the pinned source
/// (<c>BattleEngine.cpp:303-312, :1094-1113, :1222-1233, :1708-1718</c>,
/// <c>BattleEngineJetPart.cpp:778-821</c>, <c>BattleEngineWalkerPart.cpp:519-557, :685-730</c>).
/// </summary>
public sealed class Level100PlayerStoresTests
{
    private static float Store(Level100PlayerStoresSnapshot stores, int index) =>
        BitConverter.UInt32BitsToSingle(index switch
        {
            0 => stores.Store0Bits,
            1 => stores.Store1Bits,
            2 => stores.Store2Bits,
            3 => stores.Store3Bits,
            4 => stores.Store4Bits,
            _ => stores.Store5Bits,
        });

    [Fact]
    public void Construction_FillsAmmoStoresAndEmptiesHeatStores()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        Level100PlayerStoresSnapshot stores = weapons.StoresSnapshot;
        Assert.Equal(Level100PlayerStoresSnapshot.Initial, stores);
        Assert.Equal([2000.0f, 100.0f, 0.0f, 200.0f, 0.0f, 0.0f],
            Enumerable.Range(0, 6).Select(index => Store(stores, index)));
        Assert.True(stores.ShieldsRecharging);
    }

    [Fact]
    public void JetAmmo_SpendsOncePerEventAndRefusesWhenEmpty()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        Assert.True(weapons.WeaponFired(Level100MissionWeapon.MechVulcanCannon, jetPart: true, now: 1.0f));
        Assert.Equal(1999.0f, Store(weapons.StoresSnapshot, 0));
        Assert.True(weapons.WeaponFired(Level100MissionWeapon.MissilePod, jetPart: true, now: 1.0f));
        Assert.Equal(199.0f, Store(weapons.StoresSnapshot, 3));

        for (int shot = 0; shot < 199; shot++)
        {
            Assert.True(weapons.WeaponFired(Level100MissionWeapon.MissilePod, true, 2.0f));
        }
        Assert.Equal(0.0f, Store(weapons.StoresSnapshot, 3));
        Assert.False(weapons.CanWeaponFire(Level100MissionWeapon.MissilePod, walkerPart: false));

        // An empty store refuses and stamps the depleted cue at most every 8 s.
        Assert.False(weapons.WeaponFired(Level100MissionWeapon.MissilePod, true, 10.0f));
        Assert.Equal(BitConverter.SingleToUInt32Bits(10.0f), weapons.StoresSnapshot.AmmoDepletedTimeBits);
        Assert.False(weapons.WeaponFired(Level100MissionWeapon.MissilePod, true, 17.0f));
        Assert.Equal(BitConverter.SingleToUInt32Bits(10.0f), weapons.StoresSnapshot.AmmoDepletedTimeBits);
        Assert.False(weapons.WeaponFired(Level100MissionWeapon.MissilePod, true, 18.5f));
        Assert.Equal(BitConverter.SingleToUInt32Bits(18.5f), weapons.StoresSnapshot.AmmoDepletedTimeBits);
    }

    [Fact]
    public void WalkerHeat_AddsConsumptionUntilCapacityThenOverheatsAndRefuses()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        Assert.True(weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, jetPart: false, now: 1.0f));
        Assert.Equal(4.0f, Store(weapons.StoresSnapshot, 2));
        Assert.False(weapons.ShieldsRecharging);
        weapons.ResumeShieldsRecharging();

        // 150 is reached after 37 more shots (4 x 38 = 152 >= 150).
        for (int shot = 0; shot < 37; shot++)
        {
            Assert.True(weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 1.0f));
        }
        Assert.Equal(152.0f, Store(weapons.StoresSnapshot, 2));
        Assert.False(weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 5.5f));
        Assert.Equal(1 << 2, weapons.StoresSnapshot.OverheatMask);
        Assert.Equal(BitConverter.SingleToUInt32Bits(5.5f), weapons.StoresSnapshot.WeaponOverheatedTimeBits);
        Assert.False(weapons.CanWeaponFire(Level100MissionWeapon.PulseCannonPod, walkerPart: true));
    }

    [Fact]
    public void Cooling_TakesOneEveryMoveAndClearsOverheatStrictlyBelowThreeQuarters()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        for (int shot = 0; shot < 38; shot++)
        {
            weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 1.0f);
        }
        weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 1.0f);
        Assert.Equal(1 << 2, weapons.StoresSnapshot.OverheatMask);

        // 152 cools to 113 in 39 Moves: still overheated at 113, clear at 112 < 112.5.
        for (int move = 0; move < 39; move++)
        {
            weapons.CoolStores();
        }
        Assert.Equal(113.0f, Store(weapons.StoresSnapshot, 2));
        Assert.Equal(1 << 2, weapons.StoresSnapshot.OverheatMask);
        weapons.CoolStores();
        Assert.Equal(112.0f, Store(weapons.StoresSnapshot, 2));
        Assert.Equal(0, weapons.StoresSnapshot.OverheatMask);

        for (int move = 0; move < 200; move++)
        {
            weapons.CoolStores();
        }
        Assert.Equal(0.0f, Store(weapons.StoresSnapshot, 2));
        // Ammo stores never cool or refill.
        Assert.Equal(2000.0f, Store(weapons.StoresSnapshot, 0));
    }

    [Fact]
    public void ChargedPulseShot_CostsNoHeat()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        for (int sample = 0; sample < 10; sample++)
        {
            Assert.False(weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 1.0f));
        }
        // Ten charge calls at rate 10 reach 100 and add 4 heat each.
        Assert.Equal(40.0f, Store(weapons.StoresSnapshot, 2));
        Assert.True(weapons.TryPrepareFire(VehicleMode.Walker, VehicleTransition.None, 1.0f,
            out Level100ProjectileKind round));
        Assert.Equal(Level100ProjectileKind.MechPulseBoltLarge, round);
        Assert.Equal(1, weapons.StoresSnapshot.PulseModeLevel);
        Assert.True(weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 1.0f));
        Assert.Equal(40.0f, Store(weapons.StoresSnapshot, 2));
    }

    [Fact]
    public void ChargingIntoAFullHeatStore_OverheatsAndForcesAFire()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        for (int shot = 0; shot < 37; shot++)
        {
            weapons.WeaponFired(Level100MissionWeapon.PulseCannonPod, false, 1.0f);
        }
        Assert.Equal(148.0f, Store(weapons.StoresSnapshot, 2));
        // 148 < 150: this charge adds 4; the next finds 152 and overheats.
        Assert.False(weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 1.0f));
        Assert.Equal(152.0f, Store(weapons.StoresSnapshot, 2));
        Assert.True(weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 2.0f));
        Assert.Equal(1 << 2, weapons.StoresSnapshot.OverheatMask);
        // With the store overheated, charging is refused outright.
        Assert.False(weapons.AdvanceCharge(VehicleMode.Walker, VehicleTransition.None, 3.0f));
    }

    [Fact]
    public void ChangeWeapon_SkipsAnAmmoWeaponThatCannotPayItsConsumption()
    {
        var weapons = new Level100PlayerWeaponRuntime();
        for (int shot = 0; shot < 1999; shot++)
        {
            weapons.WeaponFired(Level100MissionWeapon.MechVulcanCannon, true, 1.0f);
        }
        Assert.Equal(1.0f, Store(weapons.StoresSnapshot, 0));
        // The Twin Vulcan needs 2 from store 0; only 1 remains.
        Assert.False(weapons.SelectNextActive(VehicleMode.Walker));
        Assert.Equal(Level100MissionWeapon.PulseCannonPod, weapons.WalkerSelectedWeapon);
    }

    [Fact]
    public void Shake_TakesThreeDrawsAboveTheThresholdAndDecaysEachMove()
    {
        var random = new Level100ReleasedRandom();
        var shake = new RetailBattleEngineShake();
        shake.Add(0.0009f, random.Next);
        Assert.Equal(new Level100ReleasedRandom().Seed, random.Seed);

        var expected = new Level100ReleasedRandom();
        int[] draws = [expected.Next(), expected.Next(), expected.Next()];
        shake.Add(0.03f, random.Next);
        Assert.Equal(expected.Seed, random.Seed);

        float scale = 16.0f / 0.03f;
        Level100BattleEngineShakeSnapshot snapshot = shake.Snapshot;
        Assert.Equal(BitConverter.SingleToUInt32Bits((draws[0] % 32) / scale - 0.03f), snapshot.YawBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits((draws[1] % 32) / scale - 0.03f), snapshot.PitchBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits((draws[2] % 32) / scale - 0.03f), snapshot.RollBits);
        Assert.Equal(0u, snapshot.PhaseBits);

        shake.Decay();
        Level100BattleEngineShakeSnapshot decayed = shake.Snapshot;
        Assert.Equal(BitConverter.SingleToUInt32Bits(
            BitConverter.UInt32BitsToSingle(snapshot.YawBits) * 0.8f), decayed.YawBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits(0.8f), decayed.PhaseBits);
    }

    [Theory]
    [InlineData(200, false, 0.025f)]
    [InlineData(200, true, 0.0125f)]
    [InlineData(4, true, 0.00025f)]
    [InlineData(4_000, false, 0.25f)]
    public void DamageShake_IsLifeLostOverEightHalvedWithShieldsAndCapped(
        int lifeLostMilli, bool shieldsRemain, float expected) =>
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(expected),
            BitConverter.SingleToUInt32Bits(RetailBattleEngineShake.DamageAmount(lifeLostMilli, shieldsRemain)));
}
