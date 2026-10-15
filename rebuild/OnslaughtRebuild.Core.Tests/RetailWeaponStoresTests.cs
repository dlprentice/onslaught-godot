// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailWeaponStoreReadouts"/> against
/// <c>references/Onslaught/BattleEngineWalkerPart.cpp:826-876</c> and the
/// pristine <c>74154bfa…</c> bytes at <c>0x00414410</c>, <c>0x00414470</c>,
/// <c>0x004144C0</c> and <c>0x004144F0</c>.
/// </summary>
public sealed class RetailWeaponStoresTests
{
    private static RetailWeaponStores Stores(
        float value, float capacity, int heat = 0, int overheat = 0, int store = 0)
    {
        var stores = new RetailWeaponStores();
        stores.StoreValue[store] = value;
        stores.ConfigurationStoreValue[store] = capacity;
        stores.StoreHeat[store] = heat;
        stores.StoreOverheat[store] = overheat;
        return stores;
    }

    // The 0x18 stride between mStoreValue (+0x52C), mStoreOverheat (+0x544) and
    // mStoreHeat (+0x55C) is six four-byte entries. The count is measured from
    // the image, not taken from BattleEngineDataManager.h:11.
    [Fact]
    public void StoreCount_IsTheSixTheArrayStrideMeasures() =>
        Assert.Equal(6, RetailWeaponStores.StoreCount);

    // Exact quotient bits, and the clamp. 300/200 must come back as 1.0f
    // exactly, not 1.5f: the ceiling is read from 0x005D8568 and substituted
    // whole (fstp st(0) / fld at 0x0041445B).
    [Theory]
    [InlineData(1.0f, 3.0f, 0x3EAAAAABu)]
    [InlineData(50.0f, 200.0f, 0x3E800000u)]
    [InlineData(200.0f, 200.0f, 0x3F800000u)]
    [InlineData(300.0f, 200.0f, 0x3F800000u)]
    [InlineData(0.0f, 200.0f, 0x00000000u)]
    public void AmmoPercentage_DividesAndClampsExactly(
        float value, float capacity, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailWeaponStoreReadouts.AmmoPercentage(Stores(value, capacity), 0)));

    // THE dead-branch test. BattleEngineWalkerPart.cpp:834-837 reads as a real
    // if/else on mStoreHeat, but 0x0041443F loads the flag, executes
    // `test edx, edx`, and never branches on the flags - both source arms
    // denote the same float division, so the compiler folded them. The
    // percentage must therefore be identical for every heat value, including
    // the non-canonical ones.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    public void AmmoPercentage_IgnoresTheStoreHeatFlagEntirely(int heat) =>
        Assert.Equal(
            0x3EAAAAABu,
            BitConverter.SingleToUInt32Bits(
                RetailWeaponStoreReadouts.AmmoPercentage(
                    Stores(1.0f, 3.0f, heat: heat), 0)));

    // A zero capacity divides by zero on the x87 with the divide exception
    // masked, so the quotient is an infinity or a NaN, and only the infinity is
    // ordered-greater-than one. `fcom / test ah, 0x41 / jne` returns the NaN
    // unclamped; the positive infinity clamps to 1.0f.
    [Fact]
    public void AmmoPercentage_ClampsInfinityButPassesNaNThrough()
    {
        Assert.Equal(
            0x3F800000u,
            BitConverter.SingleToUInt32Bits(
                RetailWeaponStoreReadouts.AmmoPercentage(Stores(7.0f, 0.0f), 0)));

        Assert.True(
            float.IsNaN(RetailWeaponStoreReadouts.AmmoPercentage(Stores(0.0f, 0.0f), 0)));
    }

    // THE rounding divergence. BattleEngineWalkerPart.cpp:854 is a C cast,
    // which truncates toward zero. 0x0041449D is a bare `fistp qword` with no
    // rounding-mode change and no helper call - /QIfist codegen - so the
    // conversion uses the ambient control word, which the CRT leaves at 0x027F:
    // round to nearest, ties to even. The third column is the source text, and
    // it disagrees on four of these six rows.
    //
    // NON-VACUITY: the fourth column declares whether the row separates the two
    // models, and the test proves that classification at run time rather than
    // trusting the comment. Two rows deliberately agree (2.5 and -2.5 are already
    // ties-to-even), so a bare per-row NotEqual would be false; asserting the
    // declared split instead proves the probe set contains four rows on which a
    // truncating rebuild gives a different answer. Flip any `divergent: true` row
    // to false and the test fails, so the discriminating population cannot be
    // silently emptied.
    [Theory]
    [InlineData(2.5f, 2, 2, false)]
    [InlineData(3.5f, 4, 3, true)]
    [InlineData(2.7f, 3, 2, true)]
    [InlineData(1.5f, 2, 1, true)]
    [InlineData(-2.5f, -2, -2, false)]
    [InlineData(-1.5f, -2, -1, true)]
    public void AmmoCount_RoundsToNearestEvenWhereTheSourceTextTruncates(
        float value, int retail, int sourceText, bool divergent)
    {
        Assert.Equal(divergent, retail != sourceText);

        int measured = RetailWeaponStoreReadouts.AmmoCount(Stores(value, 10.0f), 0);

        Assert.Equal(retail, measured);
        Assert.Equal(sourceText, (int)value);

        if (divergent)
        {
            Assert.NotEqual((int)value, measured);
        }
    }

    // An energy store reports no rounds: the `test edx, edx / jne 0x004144AA`
    // at 0x00414492 is a live branch here, unlike its twin in
    // GetWeaponAmmoPercentage. Any non-zero heat takes it.
    [Theory]
    [InlineData(0, 12)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void AmmoCount_IsZeroForAnEnergyStore(int heat, int expected) =>
        Assert.Equal(
            expected,
            RetailWeaponStoreReadouts.AmmoCount(Stores(12.0f, 20.0f, heat: heat), 0));

    // A store value the 64-bit fistp cannot represent stores the x87 integer
    // indefinite, 0x8000000000000000, whose low dword is zero - not a
    // saturated int.MaxValue.
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(1e30f)]
    public void AmmoCount_StoresTheIntegerIndefiniteWhenTheConversionOverflows(float value) =>
        Assert.Equal(0, RetailWeaponStoreReadouts.AmmoCount(Stores(value, 10.0f), 0));

    // Both predicates are a single mov-and-return (0x004144D9, 0x00414509).
    // They hand back the stored word, not a normalised truth value, so a BOOL
    // of 2 is returned as 2 - which matters to every caller in this image that
    // compares against literal 1 rather than testing for non-zero.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-1)]
    public void HeatPredicates_ReturnTheStoredWordVerbatim(int flag)
    {
        var stores = Stores(1.0f, 1.0f, heat: flag, overheat: flag);

        Assert.Equal(flag, RetailWeaponStoreReadouts.IsEnergyWeapon(stores, 0));
        Assert.Equal(flag, RetailWeaponStoreReadouts.IsWeaponOverheated(stores, 0));
    }

    // Every one of the four opens with `call GetCurrentWeapon` and a null test,
    // and every no-weapon arm is a plain zero (0x00414466 loads the zeroed
    // local, the other three are xor eax, eax). Nothing reads a store, so an
    // implementation that defaulted to store 0 would report a live weapon's
    // ammunition with no weapon mounted.
    [Fact]
    public void EveryReadout_IsZeroWithNoCurrentWeapon()
    {
        var stores = Stores(99.0f, 100.0f, heat: 1, overheat: 1);

        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(
                RetailWeaponStoreReadouts.AmmoPercentage(stores, null)));
        Assert.Equal(0, RetailWeaponStoreReadouts.AmmoCount(stores, null));
        Assert.Equal(0, RetailWeaponStoreReadouts.IsEnergyWeapon(stores, null));
        Assert.Equal(0, RetailWeaponStoreReadouts.IsWeaponOverheated(stores, null));
    }

    // The store index comes from the weapon, and each readout indexes its own
    // array with it. A rebuild that shared one index across arrays of different
    // bases would pass on store 0 and fail here.
    [Fact]
    public void Readouts_IndexTheStoreTheWeaponNames()
    {
        var stores = new RetailWeaponStores();
        stores.StoreValue[4] = 8.0f;
        stores.ConfigurationStoreValue[4] = 32.0f;
        stores.StoreOverheat[4] = 1;
        stores.StoreValue[0] = 1000.0f;
        stores.ConfigurationStoreValue[0] = 1000.0f;

        Assert.Equal(
            0x3E800000u,
            BitConverter.SingleToUInt32Bits(
                RetailWeaponStoreReadouts.AmmoPercentage(stores, 4)));
        Assert.Equal(8, RetailWeaponStoreReadouts.AmmoCount(stores, 4));
        Assert.Equal(1, RetailWeaponStoreReadouts.IsWeaponOverheated(stores, 4));
        Assert.Equal(0, RetailWeaponStoreReadouts.IsWeaponOverheated(stores, 0));
    }
}
