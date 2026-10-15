// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailWeaponAugment"/> and
/// <see cref="RetailHostileEnvironment"/> against
/// <c>references/Onslaught/BattleEngine.cpp:3269-3278</c>, <c>:3302-3326</c>,
/// <c>:2020-2026</c> and the pristine <c>74154bfa…</c> bytes at
/// <c>0x0040DCE0</c> and <c>0x0040DE40</c>.
/// </summary>
public sealed class RetailBattleEngineAugmentTests
{
    private static RetailWeaponStores Stores(int store, float value, int heat)
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[store] = value;
        stores.StoreHeat[store] = heat;
        return stores;
    }

    // The two immediates the shipped body stores, bit for bit, and the two
    // string literals it pushes. MAX_AUG_VALUE and MAX_ZOOM_OUT are defines the
    // header never exports, so these are the image's word on them.
    [Fact]
    public void Constants_MatchTheImmediatesAndLiteralsTheBodyStores()
    {
        Assert.Equal(
            0x41200000u,
            BitConverter.SingleToUInt32Bits(RetailWeaponAugment.MaxAugValue));
        Assert.Equal(
            0x3F800000u,
            BitConverter.SingleToUInt32Bits(RetailWeaponAugment.MaxZoomOut));
        Assert.Equal("hud_weapon_augmented", RetailWeaponAugment.AugmentSampleName);

        Assert.Equal(
            0x40A00000u,
            BitConverter.SingleToUInt32Bits(RetailHostileEnvironment.AnnounceInterval));
        Assert.Equal("hud_hostile_environment", RetailHostileEnvironment.WarningSampleName);

        // The dormant log line at 0x00623500, double space and all.
        Assert.Equal(
            "playing sample :  hostile environment",
            RetailHostileEnvironment.WarningLogMessage);
    }

    // An energy store always passes without the value being read; an ammunition
    // store passes only on an ordered strictly-positive value. Negative zero
    // compares equal and closes the gate.
    [Theory]
    [InlineData(1, 0.0f, true)]
    [InlineData(1, -50.0f, true)]
    [InlineData(0, 0.001f, true)]
    [InlineData(0, 0.0f, false)]
    [InlineData(0, -1.0f, false)]
    public void IsAugmentable_ReadsTheHeatWordFirst(int heat, float value, bool expected) =>
        Assert.Equal(expected, RetailWeaponAugment.IsAugmentable(Stores(4, value, heat), 4));

    // The compare is against the shared +0.0f word, so a negative zero store
    // reads as equal and closes the gate.
    [Fact]
    public void IsAugmentable_ClosesOnANegativeZeroStore() =>
        Assert.False(RetailWeaponAugment.IsAugmentable(
            Stores(4, BitConverter.UInt32BitsToSingle(0x80000000u), heat: 0), 4));

    [Fact]
    public void IsAugmentable_ClosesOnAnUnorderedStoreValue() =>
        Assert.False(RetailWeaponAugment.IsAugmentable(Stores(4, float.NaN, heat: 0), 4));

    // A closed gate writes nothing at all: 0x0040DE79 jumps past every store.
    [Fact]
    public void AugmentWeapon_WritesNothingWhenTheGateIsClosed()
    {
        RetailAugmentResult result = RetailWeaponAugment.AugmentWeapon(
            Stores(0, value: 0.0f, heat: 0),
            primaryAmmoStore: 0,
            primaryIsCurrentWeapon: true,
            state: (int)RetailBattleEngineState.Walker,
            now: 42.0f);

        Assert.Equal(default, result);
        Assert.False(result.Augmented);
    }

    // cmp eax, 2 selects the walker part at +0x578 and cmp eax, 3 the jet part
    // at +0x57C; the two morph states reach neither. A rebuild that switched on
    // "not jet means walker" - which is what CBattleEngine::GetCurrentWeapon
    // does two dozen lines away - would forward during a morph.
    [Theory]
    [InlineData(RetailBattleEngineState.MorphingIntoWalker, RetailChargeLossTarget.None)]
    [InlineData(RetailBattleEngineState.MorphingIntoJet, RetailChargeLossTarget.None)]
    [InlineData(RetailBattleEngineState.Walker, RetailChargeLossTarget.WalkerPart)]
    [InlineData(RetailBattleEngineState.Jet, RetailChargeLossTarget.JetPart)]
    public void ChargeLossTarget_MatchesTheTwoWayCompare(
        RetailBattleEngineState state, RetailChargeLossTarget expected) =>
        Assert.Equal(expected, RetailWeaponAugment.ChargeLossTarget((int)state));

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    public void ChargeLossTarget_ForwardsNowhereForAnUnknownState(int state) =>
        Assert.Equal(RetailChargeLossTarget.None, RetailWeaponAugment.ChargeLossTarget(state));

    // The slow-movement clear and the charge loss live inside the
    // `primaryWeapon == GetCurrentWeapon()` arm at 0x0040DE8B; the three stamps
    // do not. So augmenting a weapon that is not selected still stamps.
    [Fact]
    public void AugmentWeapon_StampsEvenWhenThePrimaryIsNotCurrent()
    {
        RetailAugmentResult result = RetailWeaponAugment.AugmentWeapon(
            Stores(0, value: 5.0f, heat: 0),
            primaryAmmoStore: 0,
            primaryIsCurrentWeapon: false,
            state: (int)RetailBattleEngineState.Jet,
            now: 42.0f);

        Assert.Equal(
            new RetailAugmentResult(
                Augmented: true,
                ClearsSlowMovement: false,
                ChargeLossTarget: RetailChargeLossTarget.None,
                AugValue: 10.0f,
                AugActiveTime: 42.0f,
                AugmentedTime: 42.0f),
            result);
    }

    [Fact]
    public void AugmentWeapon_ClearsSlowMovementAndDropsChargeWhenThePrimaryIsCurrent()
    {
        RetailAugmentResult result = RetailWeaponAugment.AugmentWeapon(
            Stores(2, value: 0.0f, heat: 1),
            primaryAmmoStore: 2,
            primaryIsCurrentWeapon: true,
            state: (int)RetailBattleEngineState.Walker,
            now: -3.5f);

        Assert.Equal(
            new RetailAugmentResult(
                Augmented: true,
                ClearsSlowMovement: true,
                ChargeLossTarget: RetailChargeLossTarget.WalkerPart,
                AugValue: 10.0f,
                AugActiveTime: -3.5f,
                AugmentedTime: -3.5f),
            result);

        Assert.Equal(0x41200000u, BitConverter.SingleToUInt32Bits(result.AugValue));
    }

    // The warning fires on a strictly-greater elapsed time. Exactly five
    // seconds is silence, because test ah, 0x41 catches C3.
    [Theory]
    [InlineData(0.0f, false)]
    [InlineData(4.9f, false)]
    [InlineData(5.0f, false)]
    [InlineData(5.0001f, true)]
    [InlineData(100.0f, true)]
    public void ShouldWarn_FiresOnlyPastFiveSeconds(float now, bool expected) =>
        Assert.Equal(expected, RetailHostileEnvironment.ShouldWarn(now, lastWarningTime: 0.0f));

    // The subtraction never touches memory - fld the global, fsub the stored
    // float, fcomp - so the comparison sees the exact difference at the ambient
    // 53-bit precision. Here the exact difference is above five seconds and the
    // float-rounded difference is exactly five, so a rebuild that narrows the
    // subtraction goes silent where retail warns.
    [Fact]
    public void ShouldWarn_ComparesTheUnroundedDifference()
    {
        float now = BitConverter.UInt32BitsToSingle(0x40A00001u);
        float last = BitConverter.UInt32BitsToSingle(0x34A10FB0u);

        Assert.True(RetailHostileEnvironment.ShouldWarn(now, last));

        float narrowed = now - last;
        Assert.Equal(0x40A00000u, BitConverter.SingleToUInt32Bits(narrowed));
        Assert.False(narrowed > RetailHostileEnvironment.AnnounceInterval);
    }

    // An unordered elapsed time suppresses the warning, and because the stamp
    // is written on both paths a NaN stamp is permanent.
    [Fact]
    public void ShouldWarn_IsSilentOnAnUnorderedElapsedTime()
    {
        Assert.False(RetailHostileEnvironment.ShouldWarn(float.NaN, 0.0f));
        Assert.False(RetailHostileEnvironment.ShouldWarn(1000.0f, float.NaN));
    }

    // 0x0040DD70 and 0x0040DD83 both write the time global's dword, so the
    // stamp lands whether the warning played or not - and it is a mov, so the
    // bits arrive verbatim.
    [Theory]
    [InlineData(0.0f)]
    [InlineData(100.0f)]
    public void NextLastWarningTime_StampsOnBothPaths(float now)
    {
        Assert.Equal(now, RetailHostileEnvironment.NextLastWarningTime(now, 0.0f));
        Assert.Equal(now, RetailHostileEnvironment.NextLastWarningTime(now, 99.0f));
    }

    [Fact]
    public void NextLastWarningTime_CarriesTheBitsVerbatim()
    {
        float now = BitConverter.UInt32BitsToSingle(0x7FC00001u);

        Assert.Equal(
            0x7FC00001u,
            BitConverter.SingleToUInt32Bits(
                RetailHostileEnvironment.NextLastWarningTime(now, 0.0f)));
    }
}
