// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The six ammunition/heat stores a battle-engine chassis carries, plus the
/// configuration capacities they are measured against.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/BattleEngine.h:428-430</c>
/// and <c>BattleEngineDataManager.h:11, 33</c>. The retail offsets fall out of
/// the readout bodies in <see cref="RetailWeaponStoreReadouts"/>:
/// <c>mStoreValue</c> at <c>mMainPart + 0x52C</c>, <c>mStoreOverheat</c> at
/// <c>+0x544</c>, <c>mStoreHeat</c> at <c>+0x55C</c>, <c>mConfiguration</c> at
/// <c>+0x4B0</c>, and the configuration's own <c>mStoreValue</c> at
/// <c>+0x88</c>. The <c>0x18</c> gap between consecutive arrays is six
/// four-byte entries, which measures <c>kBattleEngineStores</c> as 6 without
/// taking the header's word for it.
/// </para>
/// <para>
/// The heat and overheat arrays are <c>BOOL</c>, i.e. <c>int</c> here, because
/// their readers return the stored word verbatim rather than a normalised
/// truth value.
/// </para>
/// </remarks>
public sealed class RetailWeaponStores
{
    /// <summary><c>kBattleEngineStores</c> — <c>BattleEngineDataManager.h:11</c>; measured as the <c>0x18</c> array stride.</summary>
    public const int StoreCount = 6;

    /// <summary><c>mStoreValue</c> — <c>mMainPart + 0x52C</c>. Current charge or round count.</summary>
    public float[] StoreValue { get; } = new float[StoreCount];

    /// <summary><c>mStoreOverheat</c> — <c>mMainPart + 0x544</c>.</summary>
    public int[] StoreOverheat { get; } = new int[StoreCount];

    /// <summary><c>mStoreHeat</c> — <c>mMainPart + 0x55C</c>. Marks the store as an energy store.</summary>
    public int[] StoreHeat { get; } = new int[StoreCount];

    /// <summary><c>mConfiguration-&gt;mStoreValue</c> — <c>configuration + 0x88</c>. Capacity.</summary>
    public float[] ConfigurationStoreValue { get; } = new float[StoreCount];
}

/// <summary>
/// Four released weapon readouts, shared verbatim by the walker and the jet.
/// Each is a pure function of the current weapon's ammunition store.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineWalkerPart.cpp:826-876</c>, repeated
/// for the jet at <c>BattleEngineJetPart.cpp:826-876</c>. Retail identities in
/// the pristine <c>74154bfa…</c> image, file offset = VA - 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x00414410</c> <c>CBattleEngineWalkerPart::GetWeaponAmmoPercentage</c>.</item>
/// <item><c>0x00414470</c> <c>CBattleEngineWalkerPart::GetWeaponAmmoCount</c>.</item>
/// <item><c>0x004144C0</c> <c>CBattleEngineWalkerPart::IsEnergyWeapon</c>.</item>
/// <item><c>0x004144F0</c> <c>CBattleEngineWalkerPart::IsWeaponOverheated</c>.</item>
/// <item><c>0x004121B0</c>, <c>0x004122B0</c>, <c>0x00412310</c> — the jet twins.</item>
/// </list>
/// <para>
/// All four begin with <c>call 0x00414030</c> (<c>GetCurrentWeapon</c>) and a
/// null test. That walker/jet part is a linked-list walk over mounted weapons
/// and stays outside Core; the "no current weapon" arm is modelled here as a
/// null <paramref name="ammoStore"/>, which is exactly the information the four
/// bodies use.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE in <c>GetWeaponAmmoCount</c>, and retail
/// wins.</b> <c>BattleEngineWalkerPart.cpp:854</c> is
/// <c>(SINT)mMainPart-&gt;mStoreValue[store]</c>, a C cast, which truncates
/// toward zero. The shipped body is <c>fld dword ptr [store]</c> then
/// <c>fistp qword ptr [esp + 4]</c> then a 32-bit read of that slot — no call
/// to a conversion helper and no rounding-mode change anywhere near it. That is
/// <c>/QIfist</c> codegen: the conversion uses the <b>ambient</b> x87 rounding
/// mode, which the CRT leaves at round-to-nearest-even (the control word the
/// CRT itself checks for is <c>0x027F</c>, at <c>0x0055DCD4</c>). So a store
/// value of 2.5 reports <b>2</b>, 3.5 reports <b>4</b>, and 2.7 reports
/// <b>3</b> where the source text says 2.
/// </para>
/// <para>
/// <b>What that rests on, stated exactly.</b> Three things are measured: the
/// body between <c>0x00414470</c> and <c>0x004144B0</c> contains no
/// control-word instruction and no call between entry and the conversion; the
/// image converts float to int inline rather than through a helper, with 668
/// occurrences of the <c>DF 7C</c> (<c>fistp qword ptr</c>) opcode pair; and the
/// CRT's own guard tests for <c>0x027F</c>. What is <b>assumed</b> is that no
/// caller on the path in has left a truncating control word in place. A
/// whole-image proof of that needs real instruction boundaries — a byte scan
/// for <c>D9 /5</c> returns mostly unaligned noise — and has not been attempted
/// here. The cheapest falsifier is to read the on-screen ammunition count in a
/// live pristine run while a store sits at a known fractional value; the second
/// cheapest is to break at <c>0x0041449D</c> and read the control word.
/// </para>
/// <para>
/// <b>The <c>mStoreHeat</c> branch in <c>GetWeaponAmmoPercentage</c> is
/// dead.</b> <c>BattleEngineWalkerPart.cpp:834-837</c> reads as a real
/// distinction — energy stores divide floats, ammunition stores cast both sides
/// to float first. Retail loads <c>mStoreHeat[store]</c> at <c>0x00414431</c>
/// and executes <c>test edx, edx</c> at <c>0x0041443F</c> whose flags are
/// <b>never consumed</b>: there is no conditional jump between it and the
/// division. Both source arms denote the same operation, because
/// <c>mStoreValue</c> is already <c>float</c> on both sides, so the compiler
/// folded them and left the load stranded. A rebuild that branches on heat here
/// is modelling a distinction the shipped code does not make.
/// </para>
/// <para>
/// <b>The two predicates return raw stored words, not booleans.</b>
/// <c>0x004144D9</c> and <c>0x00414509</c> are a single
/// <c>mov eax, [array + store*4]</c> followed by <c>ret</c> — no
/// normalisation. A <c>mStoreHeat</c> entry of 2 is returned as 2. This is
/// visible to any caller that compares against <c>TRUE</c> rather than testing
/// for non-zero, which is exactly what
/// <see cref="RetailCareerSlots.SetSlot"/> does elsewhere in the image.
/// </para>
/// <para>
/// <b>One precision nuance is not claimed.</b> The percentage is divided on the
/// x87 stack and returned in <c>st(0)</c>, so under the CRT's 53-bit precision
/// control the caller receives a double-precision quotient and rounds it to
/// float only on assignment. This type returns the float-rounded value, and the
/// clamp is applied to the unrounded quotient as retail applies it. The
/// falsifier is a consumer that keeps the quotient in a register across further
/// arithmetic; none has been found.
/// </para>
/// <para>
/// <b>Do not go looking for a case where the double divide matters.</b> There
/// is none, and that is a theorem rather than a sample: binary64 carries 53
/// significant bits and binary32 carries 24, and <c>53 &gt;= 2*24 + 2</c>, so
/// rounding a quotient to double and then to float always lands where rounding
/// it straight to float would (Figueroa's innocuous-double-rounding condition,
/// which covers add, subtract, multiply, divide and square root). A sweep of
/// 2e8 random operand pairs found zero disagreements, as expected. The
/// consequence for review: mutating this division to single precision is an
/// <i>equivalent</i> transformation, not an untested one, and the clamp
/// decision cannot disagree either because rounding is monotone. The place the
/// precision genuinely is observable is
/// <see cref="RetailJetFriction.VelocityMagnitude"/>, where the intermediates —
/// not just the final result — stay wide.
/// </para>
/// </remarks>
public static class RetailWeaponStoreReadouts
{
    /// <summary>The percentage ceiling — <c>fcom dword ptr [0x005D8568]</c> at <c>0x0041444E</c>.</summary>
    public const float FullAmmoPercentage = 1.0f;

    /// <summary>
    /// <c>GetWeaponAmmoPercentage</c> — <c>BattleEngineWalkerPart.cpp:826-844</c>,
    /// <c>0x00414410</c>. Zero when there is no current weapon.
    /// </summary>
    public static float AmmoPercentage(RetailWeaponStores stores, int? ammoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (ammoStore is not int store)
        {
            return 0.0f;
        }

        double value = (double)stores.StoreValue[store] /
            (double)stores.ConfigurationStoreValue[store];

        // fcom / test ah, 0x41 / jne: the clamp arm is taken only when the
        // quotient is ordered and strictly greater than one, so a NaN quotient
        // is returned unclamped.
        return value > (double)FullAmmoPercentage ? FullAmmoPercentage : (float)value;
    }

    /// <summary>
    /// <c>GetWeaponAmmoCount</c> — <c>BattleEngineWalkerPart.cpp:847-858</c>,
    /// <c>0x00414470</c>. Zero for an energy store, and zero when there is no
    /// current weapon. See the type remarks for the rounding divergence.
    /// </summary>
    public static int AmmoCount(RetailWeaponStores stores, int? ammoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (ammoStore is not int store)
        {
            return 0;
        }

        if (stores.StoreHeat[store] != 0)
        {
            return 0;
        }

        return LowDwordOfFistp((double)stores.StoreValue[store]);
    }

    /// <summary>
    /// <c>IsEnergyWeapon</c> — <c>BattleEngineWalkerPart.cpp:861-867</c>,
    /// <c>0x004144C0</c>. Returns <c>mStoreHeat[store]</c> verbatim.
    /// </summary>
    public static int IsEnergyWeapon(RetailWeaponStores stores, int? ammoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        return ammoStore is int store ? stores.StoreHeat[store] : 0;
    }

    /// <summary>
    /// <c>IsWeaponOverheated</c> — <c>BattleEngineWalkerPart.cpp:870-876</c>,
    /// <c>0x004144F0</c>. Returns <c>mStoreOverheat[store]</c> verbatim.
    /// </summary>
    public static int IsWeaponOverheated(RetailWeaponStores stores, int? ammoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        return ammoStore is int store ? stores.StoreOverheat[store] : 0;
    }

    /// <summary>
    /// <c>fistp qword ptr</c> under the ambient control word, then the 32-bit
    /// read of the low half — <c>0x0041449D</c> and <c>0x004144A1</c>.
    /// Round-to-nearest-even; out-of-range and NaN store the x87 integer
    /// indefinite, whose low dword is zero.
    /// </summary>
    private static int LowDwordOfFistp(double value)
    {
        double rounded = System.Math.Round(value, System.MidpointRounding.ToEven);
        if (double.IsNaN(rounded) ||
            rounded < -9223372036854775808.0 ||
            rounded >= 9223372036854775808.0)
        {
            return unchecked((int)long.MinValue);
        }

        return unchecked((int)(long)rounded);
    }
}
