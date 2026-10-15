// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailWeaponFireGate"/> and
/// <see cref="RetailWeaponCycle"/> against
/// <c>references/Onslaught/BattleEngineJetPart.cpp:701-738</c> and
/// <c>:936-958</c>, and the pristine <c>74154bfa…</c> bytes at
/// <c>0x00412570</c>, <c>0x00411E70</c> and <c>0x00413EB0</c>.
/// </summary>
public sealed class RetailWeaponSelectionTests
{
    private static RetailWeaponStores Stores(
        int store, float value, float capacity, int heat, int overheat)
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[store] = value;
        stores.ConfigurationStoreValue[store] = capacity;
        stores.StoreHeat[store] = heat;
        stores.StoreOverheat[store] = overheat;
        return stores;
    }

    private static RetailMountedWeapon Weapon(
        int active, int store, float consumption, int zoomMode = 0) =>
        new() { IsActive = active, AmmoStore = store, Consumption = consumption, ZoomMode = zoomMode };

    // Both arms of CanWeaponFire start behind the inlined GetCurrentWeapon null
    // test at 0x00412584, so no weapon means no fire regardless of the stores.
    [Fact]
    public void CanWeaponFire_IsFalseWithNoCurrentWeapon() =>
        Assert.False(RetailWeaponFireGate.CanWeaponFire(
            Stores(0, value: 99.0f, capacity: 100.0f, heat: 1, overheat: 0), ammoStore: null));

    // The energy arm at 0x004125C4: fire while the value is strictly below
    // capacity AND the overheat word is clear. Exactly at capacity is a refusal
    // (C0 clear), and the overheat word is tested for non-zero, not against
    // TRUE.
    [Theory]
    [InlineData(0.0f, 100.0f, 0, true)]
    [InlineData(99.9f, 100.0f, 0, true)]
    [InlineData(100.0f, 100.0f, 0, false)]
    [InlineData(100.1f, 100.0f, 0, false)]
    [InlineData(0.0f, 100.0f, 1, false)]
    [InlineData(0.0f, 100.0f, 7, false)]
    public void CanWeaponFire_GatesTheEnergyArmOnCapacityAndOverheat(
        float value, float capacity, int overheat, bool expected) =>
        Assert.Equal(
            expected,
            RetailWeaponFireGate.CanWeaponFire(
                Stores(2, value, capacity, heat: 1, overheat), ammoStore: 2));

    // The ammunition arm at 0x004125F1 compares against the shared +0.0f word
    // with test ah, 0x41, so it is a plain ordered strictly-greater. Negative
    // zero compares equal and refuses. The capacity and overheat words are not
    // read on this arm at all.
    [Theory]
    [InlineData(1.0f, false)]
    [InlineData(0.0001f, false)]
    [InlineData(0.0f, true)]
    [InlineData(-1.0f, true)]
    public void CanWeaponFire_GatesTheAmmunitionArmOnAPositiveStore(
        float value, bool expectedRefusal)
    {
        RetailWeaponStores stores = Stores(3, value, capacity: 0.0f, heat: 0, overheat: 1);

        Assert.Equal(!expectedRefusal, RetailWeaponFireGate.CanWeaponFire(stores, ammoStore: 3));
    }

    // The walker's body at 0x00414630 loads [weapon + 0x9C] and tests it; the
    // jet's at 0x00412570 never loads it. Same source line number in the two
    // files, and the manifest has no row for the walker's address at all, but
    // they are different functions: a deactivated current weapon fires on a jet
    // and does not on a walker. A rebuild that shared one implementation
    // between the chassis is wrong for one of them whichever way it picks.
    //
    // NON-VACUITY: the whole contract is that the two chassis DISAGREE, so the
    // third column declares which rows separate them and the test measures the
    // separation rather than describing it. Only the deactivated row can - and
    // the sweep counts that the discriminating population over the three probes
    // is exactly one, so a rebuild sharing one body between the chassis cannot
    // pass by making every row agree.
    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, true, false)]
    [InlineData(-9, true, false)]
    public void CanWalkerWeaponFire_AddsTheActiveGateTheJetDoesNotHave(
        int isActive, bool expected, bool chassisDisagreeHere)
    {
        RetailWeaponStores stores = Stores(1, value: 5.0f, capacity: 0.0f, heat: 0, overheat: 0);

        bool walker = RetailWeaponFireGate.CanWalkerWeaponFire(stores, ammoStore: 1, isActive);
        bool jet = RetailWeaponFireGate.CanWeaponFire(stores, ammoStore: 1);

        Assert.Equal(expected, walker);

        // The jet never reads the word, so it fires either way.
        Assert.True(jet);
        Assert.Equal(chassisDisagreeHere, walker != jet);

        int probesWhereTheChassisDisagree = 0;
        foreach (int word in new[] { 0, 1, -9 })
        {
            if (RetailWeaponFireGate.CanWalkerWeaponFire(stores, ammoStore: 1, word)
                != RetailWeaponFireGate.CanWeaponFire(stores, ammoStore: 1))
            {
                probesWhereTheChassisDisagree++;
            }
        }

        Assert.Equal(1, probesWhereTheChassisDisagree);
    }

    // The compare is against the shared +0.0f word, so a negative zero store
    // reads as equal and refuses.
    [Fact]
    public void CanWeaponFire_RefusesANegativeZeroAmmunitionStore() =>
        Assert.False(RetailWeaponFireGate.CanWeaponFire(
            Stores(3, BitConverter.UInt32BitsToSingle(0x80000000u), 0.0f, heat: 0, overheat: 0),
            ammoStore: 3));

    // The two arms read the status word differently and it shows on a NaN. The
    // energy arm is `test ah, 1` - C0 alone - so an unordered value counts as
    // below capacity and the gate OPENS. The ammunition arm is `test ah, 0x41`
    // with jne and the gate CLOSES. Written the source's way both would close.
    [Fact]
    public void CanWeaponFire_SplitsOnUnorderedStoreValues()
    {
        Assert.True(RetailWeaponFireGate.CanWeaponFire(
            Stores(1, float.NaN, capacity: 100.0f, heat: 1, overheat: 0), ammoStore: 1));

        Assert.False(RetailWeaponFireGate.CanWeaponFire(
            Stores(1, float.NaN, capacity: 100.0f, heat: 0, overheat: 0), ammoStore: 1));

        Assert.False(float.NaN < 100.0f);
    }

    // The acceptance test is `fld consumption / fcomp storeValue / test ah,
    // 0x41 / jne accept`, so equality accepts and an unordered compare accepts.
    // Written as `value >= consumption` in C# the NaN row would reject.
    [Theory]
    [InlineData(0, 5.0f, 10.0f, true)]
    [InlineData(0, 10.0f, 10.0f, true)]
    [InlineData(0, 10.5f, 10.0f, false)]
    [InlineData(1, 999.0f, 0.0f, true)]
    public void IsSelectable_AcceptsAtEqualityAndOnAnyEnergyStore(
        int heat, float consumption, float value, bool expected) =>
        Assert.Equal(
            expected,
            RetailWeaponCycle.IsSelectable(
                Weapon(active: 1, store: 0, consumption),
                Stores(0, value, capacity: 100.0f, heat, overheat: 0)));

    [Fact]
    public void IsSelectable_AcceptsAnUnorderedConsumptionOrStore()
    {
        Assert.True(RetailWeaponCycle.IsSelectable(
            Weapon(active: 1, store: 0, consumption: float.NaN),
            Stores(0, value: 1.0f, capacity: 100.0f, heat: 0, overheat: 0)));

        Assert.True(RetailWeaponCycle.IsSelectable(
            Weapon(active: 1, store: 0, consumption: 1.0f),
            Stores(0, value: float.NaN, capacity: 100.0f, heat: 0, overheat: 0)));

        Assert.False(1.0f >= float.NaN);
    }

    // mActive is loaded and tested for non-zero at 0x00411F1F without
    // normalising, so any non-zero word is active and only zero is inactive.
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-3, true)]
    public void IsSelectable_TestsTheRawActiveWord(int active, bool expected) =>
        Assert.Equal(
            expected,
            RetailWeaponCycle.IsSelectable(
                Weapon(active, store: 0, consumption: 0.0f),
                Stores(0, value: 100.0f, capacity: 100.0f, heat: 0, overheat: 0)));

    // The scan starts at mCurrentWeapon + 1 and takes the FIRST selectable
    // index it meets going forward, wrapping through zero. Starting at 2 of
    // four with only 0 and 3 usable must land on 3, not on 0 and not on 1.
    [Fact]
    public void ChangeWeapon_TakesTheNextSelectableIndexGoingForward()
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[0] = 100.0f;
        List<RetailMountedWeapon> weapons =
        [
            Weapon(1, 0, 1.0f),
            Weapon(0, 0, 1.0f),
            Weapon(1, 0, 1.0f),
            Weapon(1, 0, 1.0f),
        ];

        RetailWeaponCycleResult result = RetailWeaponCycle.ChangeWeapon(weapons, 2, stores);

        Assert.True(result.Changed);
        Assert.Equal(3, result.CurrentWeapon);
    }

    // ... and the wrap is real: from the last index the scan comes round to
    // zero rather than stopping.
    [Fact]
    public void ChangeWeapon_WrapsPastTheEndOfTheList()
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[0] = 100.0f;
        List<RetailMountedWeapon> weapons =
        [
            Weapon(1, 0, 1.0f),
            Weapon(0, 0, 1.0f),
            Weapon(0, 0, 1.0f),
        ];

        RetailWeaponCycleResult result = RetailWeaponCycle.ChangeWeapon(weapons, 2, stores);

        Assert.True(result.Changed);
        Assert.Equal(0, result.CurrentWeapon);
    }

    // With nothing else selectable the loop runs the whole ring and falls out at
    // 0x00411F5B having touched nothing - no index change, no slow-movement
    // clear, no charge loss. The current weapon is never re-examined, so an
    // otherwise selectable current weapon is not "re-selected".
    [Fact]
    public void ChangeWeapon_LeavesEverythingAloneWhenNothingElseIsSelectable()
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[0] = 100.0f;
        List<RetailMountedWeapon> weapons =
        [
            Weapon(1, 0, 1.0f),
            Weapon(0, 0, 1.0f),
            Weapon(0, 0, 1.0f),
        ];

        RetailWeaponCycleResult result = RetailWeaponCycle.ChangeWeapon(weapons, 0, stores);

        Assert.Equal(
            new RetailWeaponCycleResult(0, false, false, false, false),
            result);
    }

    // On a change retail writes the index, clears mSlowMovement at mMainPart +
    // 0x588, runs the LoseCharge store, and only calls AutoZoomOut when the new
    // weapon's zoom mode differs from the entry weapon's.
    [Theory]
    [InlineData(4, 4, false)]
    [InlineData(4, 9, true)]
    public void ChangeWeapon_AutoZoomsOutOnlyOnAZoomModeChange(
        int oldZoom, int newZoom, bool expectedZoomOut)
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[0] = 100.0f;
        List<RetailMountedWeapon> weapons =
        [
            Weapon(1, 0, 1.0f, oldZoom),
            Weapon(1, 0, 1.0f, newZoom),
        ];

        RetailWeaponCycleResult result = RetailWeaponCycle.ChangeWeapon(weapons, 0, stores);

        Assert.Equal(
            new RetailWeaponCycleResult(1, true, true, true, expectedZoomOut),
            result);
    }

    // The wrap only ever produces zero and the loop condition is `n !=
    // mCurrentWeapon`, so a current index outside the list can never be reached
    // and retail spins. An empty list pins n at zero for the same reason. This
    // is asserted through the predicate rather than by running the loop.
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, false)]
    [InlineData(3, 3, false)]
    [InlineData(-1, 3, false)]
    [InlineData(0, 3, true)]
    [InlineData(2, 3, true)]
    public void SearchTerminates_RequiresTheCurrentIndexToBeInTheList(
        int currentWeapon, int weaponCount, bool expected) =>
        Assert.Equal(expected, RetailWeaponCycle.SearchTerminates(currentWeapon, weaponCount));

    [Fact]
    public void ChangeWeapon_RefusesTheInputsRetailFaultsOn()
    {
        RetailWeaponStores stores = new();
        List<RetailMountedWeapon> weapons = [Weapon(1, 0, 0.0f)];

        Assert.Throws<InvalidOperationException>(
            () => RetailWeaponCycle.ChangeWeapon(weapons, 1, stores));
        Assert.Throws<InvalidOperationException>(
            () => RetailWeaponCycle.ChangeWeapon([], 0, stores));
    }

    // A one-weapon list is the case where the whole ring is a single step over
    // the end: n starts at 1, finds nothing, wraps to 0 and stops. It must not
    // re-select the weapon already current.
    [Fact]
    public void ChangeWeapon_DoesNotReselectTheOnlyWeapon()
    {
        RetailWeaponStores stores = new();
        stores.StoreValue[0] = 100.0f;
        List<RetailMountedWeapon> weapons = [Weapon(1, 0, 1.0f)];

        Assert.False(RetailWeaponCycle.ChangeWeapon(weapons, 0, stores).Changed);
    }
}
