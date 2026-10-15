// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Ordered CUnit weapon-list input for the finite, current-mode, nonballistic
/// selection route. Float words remain unscaled; identity is the attachment's
/// identity, not its position in this list or a weapon-kind enum.
/// </summary>
public readonly record struct RetailUnitWeaponSelectionCandidate(
    int Identity,
    int ActiveWord,
    uint TargetMask,
    int BurstCounter,
    int BurstSize,
    int ReadyAtTimeFloatBits,
    int MinimumRangeFloatBits,
    int MaximumRangeFloatBits,
    int MinimumTargetHeightFloatBits,
    int MaximumTargetHeightFloatBits,
    bool HasCurrentMode = true,
    bool UsesBallisticArc = false,
    bool HasProjectileDefinition = true);

/// <summary>CUnit +140 and the identity held by its +144 spawner reader.</summary>
public readonly record struct RetailUnitAttackSelection(int? WeaponIdentity, int? SpawnerIdentity);

/// <summary>
/// Weapon-only route of pristine 74154bfa…7750 at [004fb840,004fbc8b).
/// Empty spawner lists and nonnull modes with nonnull nonballistic projectile
/// definitions are explicit admissions.
/// This selects a provider; it does not establish that the provider can fire.
/// </summary>
/// <remarks>
/// The September 12 isolated ELF32 experiment runs this body and nine original
/// helper bodies at their retail addresses. Terrain sampling and clearing the
/// monitored spawner reader are explicit stubs. PC24/RN matches the bounded
/// copied-retail Plane observation. Common-AI invocation, reference lifetime,
/// null-mode catalog lookup, ballistic range and spawners remain separate.
/// </remarks>
public static class RetailUnitWeaponSelection
{
    public static RetailUnitAttackSelection Select(
        IReadOnlyList<RetailUnitWeaponSelectionCandidate> weapons,
        int spawnerCount,
        RetailUnitAttackSelection previous,
        Level100FloatVector3Bits ownerPosition,
        Level100FloatVector3Bits? targetPosition,
        uint targetMask,
        int terrainHeightFloatBits,
        int waterHeightFloatBits,
        int nowFloatBits)
    {
        ArgumentNullException.ThrowIfNull(weapons);
        if (spawnerCount != 0)
            throw new NotSupportedException("Unit spawner selection is not admitted by this weapon-only route.");

        // Admission is separate from the native early returns. Do not silently
        // model a missing mode or a ballistic helper as a constant-range mode.
        foreach (RetailUnitWeaponSelectionCandidate weapon in weapons)
        {
            if (!weapon.HasCurrentMode || weapon.UsesBallisticArc || !weapon.HasProjectileDefinition)
                throw new NotSupportedException("Unit selection requires a current mode and nonballistic projectile definition.");
        }

        // 50a290 uses a signed counter and does not require an active weapon.
        foreach (RetailUnitWeaponSelectionCandidate weapon in weapons)
            if (weapon.BurstCounter != 0 && weapon.BurstCounter < weapon.BurstSize)
                return previous;
        if (targetPosition is not { } target)
            return previous;

        float distance = RawDistance(ownerPosition, target);
        float now = Read(nowFloatBits);
        var selected = new RetailUnitAttackSelection(null, null);
        float bestScore = -1;
        foreach (RetailUnitWeaponSelectionCandidate weapon in weapons)
        {
            float? score = Score(weapon, target, targetMask, distance,
                terrainHeightFloatBits, waterHeightFloatBits, now);
            // A tie retains the earlier attachment. No hard range rejection.
            if (score is { } value && value > bestScore)
            {
                bestScore = value;
                selected = new(weapon.Identity, null);
            }
        }
        return selected;
    }

    internal static float RawDistance(Level100FloatVector3Bits owner, Level100FloatVector3Bits target)
    {
        double x = RetailFloat24.Subtract(Read(target.X), Read(owner.X));
        double y = RetailFloat24.Subtract(Read(target.Y), Read(owner.Y));
        // The Z difference spills before the norm; X and Y stay on x87.
        float z = Store(RetailFloat24.Subtract(Read(target.Z), Read(owner.Z)));
        double xy = RetailFloat24.Add(RetailFloat24.Multiply(x, x), RetailFloat24.Multiply(y, y));
        return Store(RetailFloat24.Sqrt(RetailFloat24.Add(xy, RetailFloat24.Multiply(z, z))));
    }

    internal static float? Score(RetailUnitWeaponSelectionCandidate weapon,
        Level100FloatVector3Bits target, uint targetMask, float distance,
        int terrainHeightFloatBits, int waterHeightFloatBits, float now)
    {
        if (weapon.ActiveWord == 0 || (weapon.TargetMask & targetMask) == 0)
            return null;
        double height = RetailFloat24.Subtract(
            Math.Min(Read(terrainHeightFloatBits), Read(waterHeightFloatBits)), Read(target.Z));
        if (!(height > Read(weapon.MinimumTargetHeightFloatBits)) ||
            !(height < Read(weapon.MaximumTargetHeightFloatBits)))
            return null;

        double score = (weapon.TargetMask & targetMask & 0x80000) != 0 ? 2_000_000 : 0;
        float minimum = Read(weapon.MinimumRangeFloatBits);
        float maximum = Read(weapon.MaximumRangeFloatBits);
        if (minimum > distance)
            score = RetailFloat24.Add(RetailFloat24.Subtract(minimum, distance), score);
        else if (maximum < distance)
            score = RetailFloat24.Add(RetailFloat24.Subtract(distance, maximum), score);
        else
            score = RetailFloat24.Add(score, 1_000_000);
        float stored = Store(score);
        if (RetailWeaponCharge.ReadyTimeElapsed(now, Read(weapon.ReadyAtTimeFloatBits)))
            stored = Store(RetailFloat24.Add(stored, 1_000_000));
        return stored;
    }

    private static float Read(int bits)
    {
        float value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value))
            throw new NotSupportedException("Unit selection requires finite float inputs.");
        return value;
    }

    private static float Store(double value)
    {
        float stored = (float)value;
        if (!float.IsFinite(stored))
            throw new NotSupportedException("Unit selection exceeded its finite float-store domain.");
        return stored;
    }
}

/// <summary>
/// One mounted weapon, reduced to the four words the selection laws read.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>CWeapon</c> is not in the partial release, so
/// these are measured from the bodies that inline its accessors in the pristine
/// <c>74154bfa…</c> image, file offset = VA - 0x400000. <c>IsActive</c> is the
/// dword at <c>weapon + 0x9C</c> (<c>0x00411F1F</c>, <c>0x00413D08</c>);
/// the physics record is at <c>weapon + 0xA4</c>, and inside it
/// <c>GetConsumption</c> is the float at <c>+0x20</c> (<c>0x00411F3A</c>),
/// <c>GetAmmoStore</c> the dword at <c>+0x24</c> (<c>0x00411F27</c>,
/// <c>0x004125B6</c>, <c>0x00413D6C</c>, <c>0x0040DE59</c>) and
/// <c>GetZoomMode</c> the dword at <c>+0x34</c> (<c>0x00411EAC</c>,
/// <c>0x0040DEA6</c>).
/// </para>
/// <para>
/// <c>IsActive</c> is kept as the raw word rather than a boolean because
/// <c>0x00411F1F</c> loads and tests it without normalising, the same way the
/// store predicates in <see cref="RetailWeaponStoreReadouts"/> do.
/// </para>
/// </remarks>
public sealed class RetailMountedWeapon
{
    /// <summary><c>mActive</c> — the dword at <c>weapon + 0x9C</c>, tested for non-zero.</summary>
    public int IsActive { get; set; }

    /// <summary><c>GetAmmoStore()</c> — the dword at <c>record + 0x24</c>.</summary>
    public int AmmoStore { get; set; }

    /// <summary><c>GetConsumption()</c> — the float at <c>record + 0x20</c>.</summary>
    public float Consumption { get; set; }

    /// <summary><c>GetZoomMode()</c> — the dword at <c>record + 0x34</c>.</summary>
    public int ZoomMode { get; set; }
}

/// <summary>
/// <c>CBattleEngineJetPart::CanWeaponFire</c> and
/// <c>CBattleEngineWalkerPart::CanWeaponFire</c> — the released two-arm gate
/// that decides whether the current weapon may fire. <b>The two are not the
/// same function</b>, and the difference is one gate.
/// </summary>
/// <remarks>
/// <para>
/// Owners in the pinned drop:
/// <c>references/Onslaught/BattleEngineJetPart.cpp:936-958</c> and
/// <c>BattleEngineWalkerPart.cpp:936-961</c>. Retail identities in the pristine
/// <c>74154bfa…</c> image: <c>0x00412570</c> (jet) and <c>0x00414630</c>
/// (walker). The jet's leading list walk to <c>0x004125A8</c> is
/// <c>GetCurrentWeapon</c> inlined; the walker calls it out of line at
/// <c>0x00414030</c>. As in <see cref="RetailWeaponStoreReadouts"/> the "no
/// current weapon" arm is modelled as a null <c>ammoStore</c>.
/// </para>
/// <para>
/// <b>The walker checks <c>IsActive</c> and the jet does not.</b> The two
/// bodies sit at the same line number in their two files and the manifest
/// treats them as twins, but they are not: the walker's source has an extra
/// <c>if (weapon-&gt;IsActive())</c> at
/// <c>BattleEngineWalkerPart.cpp:940</c> wrapping the whole body, and the
/// shipped walker has it too —
/// <c>mov ecx, dword ptr [eax + 0x9C]</c> at <c>0x0041463C</c> followed by
/// <c>test ecx, ecx / je</c> to the failure label. The jet's body goes straight
/// from the null test at <c>0x004125AB</c> to
/// <c>mov edx, dword ptr [eax + 0xA4]</c> with no such load anywhere in it. So
/// a deactivated weapon that is somehow current can still fire on a jet and
/// cannot on a walker, and a rebuild that shares one implementation between
/// the two chassis is wrong for one of them whichever way it picks. Both are
/// modelled below; each agrees with its own source.
/// </para>
/// <para>
/// <b><c>0x00414630</c> is not in the parity manifest.</b> It is a
/// hundred-and-twenty-eight-byte gap between
/// <c>CBattleEngineWalkerPart::GetWeaponIconName</c> at <c>0x00414610</c> and
/// <c>CBattleEngineWalkerPart::ResetConfiguration</c> at <c>0x004146B0</c>,
/// with no row of its own. The identity above is this owner's, recovered from
/// the bytes and from the source shape, not carried over from the manifest.
/// </para>
/// <para>
/// <b>Source and retail agree on the structure.</b> The heat word at
/// <c>store + 0x55C</c> selects the arm (<c>0x004125B9</c>): an energy store
/// fires while its value is below the configuration capacity at
/// <c>configuration + 0x88</c> and its overheat word at <c>+0x544</c> is clear;
/// an ammunition store fires while its value is above zero.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on an unordered store value in the energy arm,
/// and retail wins.</b> <c>0x004125DA</c> is <c>test ah, 1</c> — C0 alone — and
/// an unordered compare sets C0, so a NaN store value is treated as
/// <i>below</i> capacity and the gate opens if the overheat word is clear. The
/// C text (<c>mStoreValue[store] &lt; mConfiguration-&gt;mStoreValue[store]</c>)
/// is false for a NaN and would close the gate. The ammunition arm is different
/// and does <b>not</b> diverge: <c>0x00412600</c> is <c>test ah, 0x41</c> with
/// <c>jne</c> to the failure label, and an unordered compare sets both bits, so
/// a NaN there closes the gate exactly as <c>&gt; 0</c> would.
/// </para>
/// <para>
/// <b>Not established here.</b> The zero the ammunition arm compares against is
/// the shared <c>0x005D856C</c> word, so it is <c>+0.0f</c> and a store value of
/// <c>-0.0f</c> compares equal and closes the gate. Whether a store can hold
/// <c>-0.0f</c> is outside this contract. Why the two chassis disagree is also
/// open — nothing here says whether the jet's missing gate is deliberate or a
/// copy that was never finished.
/// </para>
/// </remarks>
public static class RetailWeaponFireGate
{
    /// <summary>
    /// <c>CBattleEngineWalkerPart::CanWeaponFire</c> —
    /// <c>BattleEngineWalkerPart.cpp:936-961</c>, <c>0x00414630</c>. The jet's
    /// body has no <paramref name="isActive"/> gate; see the type remarks.
    /// </summary>
    public static bool CanWalkerWeaponFire(
        RetailWeaponStores stores, int? ammoStore, int isActive)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        // mov ecx, [weapon + 0x9C] / test ecx, ecx / je: the raw word, tested
        // for non-zero exactly as the selection laws test it.
        return isActive != 0 && CanWeaponFire(stores, ammoStore);
    }

    /// <summary>
    /// <c>CBattleEngineJetPart::CanWeaponFire</c> —
    /// <c>BattleEngineJetPart.cpp:936-958</c>, <c>0x00412570</c>.
    /// </summary>
    public static bool CanWeaponFire(RetailWeaponStores stores, int? ammoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (ammoStore is not int store)
        {
            return false;
        }

        if (stores.StoreHeat[store] != 0)
        {
            // test ah, 1 - C0 alone, so an unordered value counts as below capacity.
            if (stores.StoreValue[store] >= stores.ConfigurationStoreValue[store])
            {
                return false;
            }

            return stores.StoreOverheat[store] == 0;
        }

        // test ah, 0x41 with jne: an unordered value closes the gate.
        return stores.StoreValue[store] > 0.0f;
    }
}

/// <summary>
/// The outcome of one <c>ChangeWeapon</c> call.
/// </summary>
/// <param name="CurrentWeapon">The index after the search — unchanged when nothing was selectable.</param>
/// <param name="Changed">Whether the search selected a different weapon.</param>
/// <param name="ClearsSlowMovement"><c>mMainPart-&gt;mSlowMovement=FALSE</c>, only on a change.</param>
/// <param name="LosesChargeOnNewWeapon"><c>LoseWeaponCharge()</c>, only on a change.</param>
/// <param name="AutoZoomsOut">Whether the new weapon's zoom mode differs from the old one's.</param>
public readonly record struct RetailWeaponCycleResult(
    int CurrentWeapon,
    bool Changed,
    bool ClearsSlowMovement,
    bool LosesChargeOnNewWeapon,
    bool AutoZoomsOut);

/// <summary>
/// <c>CBattleEngineJetPart::ChangeWeapon</c> and
/// <c>CBattleEngineWalkerPart::ChangeWeapon</c> — the released cyclic search for
/// the next usable weapon.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineJetPart.cpp:701-738</c>, repeated
/// verbatim for the walker at <c>BattleEngineWalkerPart.cpp:562-599</c>. Retail
/// identities in the pristine <c>74154bfa…</c> image: <c>0x00411E70</c> (jet)
/// and <c>0x00413EB0</c> (walker). The two are separate compilations of the same
/// text and the search core is instruction-for-instruction the same shape — the
/// advance is <c>inc / cmp against the count / jl / xor to zero / compare
/// against the current index / jne</c> at <c>0x00411F4B</c> and
/// <c>0x00413F69</c>.
/// </para>
/// <para>
/// <b>Source and retail agree on the search, the acceptance test and the
/// aftermath.</b> The scan starts at <c>mCurrentWeapon + 1</c>
/// (<c>lea ebx, [ecx + 1]</c> at <c>0x00411EBA</c>), the count is a full walk of
/// the weapon list taken <b>before</b> the loop, an index past the end yields no
/// weapon rather than wrapping, and the wrap is <c>n &gt;= total → 0</c> applied
/// after the increment. On acceptance retail writes the new index, clears
/// <c>mSlowMovement</c> at <c>mMainPart + 0x588</c>, runs the same
/// <c>mov dword ptr [weapon + 0x60], 0</c> that
/// <see cref="RetailWeaponCharge.LoseCharge"/> documents, and compares the new
/// weapon's zoom mode against the entry one, calling <c>AutoZoomOut</c> at
/// <c>0x00409E80</c> when they differ. On no acceptance it returns having
/// touched nothing (<c>0x00411F5B</c>).
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on an unordered consumption or store value, and
/// retail wins.</b> <c>0x00411F46</c> is <c>test ah, 0x41</c> with <c>jne</c> to
/// the <i>acceptance</i> label, so C0 or C3 accepts — and an unordered compare
/// sets both. The C text is
/// <c>mStoreValue[store] &gt;= weapon-&gt;GetConsumption()</c>, which is false
/// for a NaN and would reject. The comparison is also written the other way
/// round in the image (<c>fld consumption</c> then
/// <c>fcomp mStoreValue</c>), which is why the model below is
/// <c>!(consumption &gt; value)</c> and not <c>value &gt;= consumption</c>.
/// </para>
/// <para>
/// <b>The wrap boundary itself is not observable, and that is a proof rather
/// than an untested corner.</b> Retail wraps at <c>n &gt;= total</c>
/// (<c>cmp ebx, eax / jl</c>); wrapping at <c>n &gt; total</c> instead would
/// only add a probe of the one-past-the-end index, which
/// <c>GetWeapon</c> answers with nothing, and would then wrap on the following
/// step. An exhaustive replay over every list of length 1..6, every
/// selectability pattern and every starting index — 642 cases — finds no pair
/// of runs that differ in result or in termination. The shipped boundary is
/// implemented anyway, because it is the one that is in the image.
/// </para>
/// <para>
/// <b>The search does not always terminate, in the source and in the shipped
/// code alike.</b> The loop condition is <c>n != mCurrentWeapon</c> and the
/// wrap only ever produces <c>0</c>, so if <c>mCurrentWeapon</c> is not itself a
/// valid index the scan can never reach it. With an empty list the wrap fires
/// every iteration and <c>n</c> is pinned at <c>0</c>. Retail hangs;
/// <see cref="ChangeWeapon"/> reproduces the loop faithfully rather than
/// inventing a bound, and <see cref="SearchTerminates"/> reports the condition
/// so a caller — or a test — can ask without running it.
/// </para>
/// <para>
/// <b>Not established here.</b> Entering with no current weapon faults in both:
/// <c>BattleEngineJetPart.cpp:703</c> calls
/// <c>GetCurrentWeapon()-&gt;GetZoomMode()</c> unguarded and retail reaches
/// <c>mov ecx, dword ptr [eax + 0xA4]</c> at <c>0x00411EA4</c> with
/// <c>eax</c> zeroed one instruction earlier. An access violation is not a
/// value, so <see cref="ChangeWeapon"/> throws instead of pretending to have an
/// answer. The list walk itself is <c>SPtrSet</c> plumbing that the task brief
/// rules out of this lane; it is flattened here to an ordered list, which is
/// what <c>GetWeapon(n)</c> and <c>CountWeapons()</c> reduce to for any list
/// whose nodes all carry payloads.
/// </para>
/// </remarks>
public static class RetailWeaponCycle
{
    /// <summary>
    /// Whether the scan can reach its stopping index. False means retail spins
    /// forever unless some weapon is accepted first.
    /// </summary>
    public static bool SearchTerminates(int currentWeapon, int weaponCount) =>
        currentWeapon >= 0 && currentWeapon < weaponCount;

    /// <summary>
    /// The acceptance test at <c>0x00411F19-0x00411F49</c>: active, and either
    /// an energy store or a store holding at least the consumption.
    /// </summary>
    public static bool IsSelectable(
        RetailMountedWeapon weapon, RetailWeaponStores stores)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (weapon.IsActive == 0)
        {
            return false;
        }

        if (stores.StoreHeat[weapon.AmmoStore] != 0)
        {
            return true;
        }

        // fld consumption / fcomp mStoreValue / test ah, 0x41 / jne accept:
        // unordered accepts, so this is a negated strictly-greater.
        return !(weapon.Consumption > stores.StoreValue[weapon.AmmoStore]);
    }

    /// <summary>
    /// <c>ChangeWeapon</c> — <c>BattleEngineJetPart.cpp:701-738</c>,
    /// <c>0x00411E70</c> and <c>0x00413EB0</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When <paramref name="currentWeapon"/> does not index
    /// <paramref name="weapons"/>: retail faults on the entry
    /// <c>GetZoomMode</c>, and a fault is not a value this can return.
    /// </exception>
    public static RetailWeaponCycleResult ChangeWeapon(
        IReadOnlyList<RetailMountedWeapon> weapons,
        int currentWeapon,
        RetailWeaponStores stores)
    {
        if (weapons is null)
        {
            throw new ArgumentNullException(nameof(weapons));
        }

        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (!SearchTerminates(currentWeapon, weapons.Count))
        {
            throw new InvalidOperationException(
                "Retail dereferences the null GetCurrentWeapon() at 0x00411EA4 and, " +
                "if it survives that, cannot reach its stopping index; see " +
                "RetailWeaponCycle.SearchTerminates.");
        }

        int oldZoomMode = weapons[currentWeapon].ZoomMode;
        int total = weapons.Count;
        int n = currentWeapon + 1;

        while (n != currentWeapon)
        {
            // GetWeapon(n) walks the list and yields nothing past the end.
            if ((uint)n < (uint)total && IsSelectable(weapons[n], stores))
            {
                return new RetailWeaponCycleResult(
                    CurrentWeapon: n,
                    Changed: true,
                    ClearsSlowMovement: true,
                    LosesChargeOnNewWeapon: true,
                    AutoZoomsOut: oldZoomMode != weapons[n].ZoomMode);
            }

            n++;
            if (n >= total)
            {
                n = 0;
            }
        }

        return new RetailWeaponCycleResult(
            CurrentWeapon: currentWeapon,
            Changed: false,
            ClearsSlowMovement: false,
            LosesChargeOnNewWeapon: false,
            AutoZoomsOut: false);
    }
}
