// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Which chassis part <c>CBattleEngine::LoseWeaponCharge</c> forwards to.
/// </summary>
public enum RetailChargeLossTarget
{
    /// <summary>Neither part — the morph states, where retail falls out of the ladder.</summary>
    None = 0,

    /// <summary><c>mWalkerPart</c> at <c>this + 0x578</c>, called at <c>0x00414010</c>.</summary>
    WalkerPart = 1,

    /// <summary><c>mJetPart</c> at <c>this + 0x57C</c>, called at <c>0x00412000</c>.</summary>
    JetPart = 2,
}

/// <summary>
/// The outcome of one <c>AugmentWeapon</c> call.
/// </summary>
/// <param name="Augmented">Whether the store gate opened at all.</param>
/// <param name="ClearsSlowMovement"><c>mSlowMovement=FALSE</c>, only when the primary weapon is also the current one.</param>
/// <param name="ChargeLossTarget">Which part <c>LoseWeaponCharge</c> reached, if any.</param>
/// <param name="AugValue">The value written to <c>mAugValue</c>.</param>
/// <param name="AugActiveTime">The value written to <c>mAugActiveTime</c>.</param>
/// <param name="AugmentedTime">The value written to <c>mAugmentedTime</c>.</param>
public readonly record struct RetailAugmentResult(
    bool Augmented,
    bool ClearsSlowMovement,
    RetailChargeLossTarget ChargeLossTarget,
    float AugValue,
    float AugActiveTime,
    float AugmentedTime);

/// <summary>
/// <c>CBattleEngine::AugmentWeapon</c> — the released weapon-augment trigger:
/// a store gate, a charge reset that depends on the chassis state, and three
/// stamped fields.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngine.cpp:3302-3326</c>, with the forwarder
/// at <c>BattleEngine.cpp:2020-2026</c> and <c>AutoZoomOut</c> at
/// <c>BattleEngine.cpp:1919-1922</c>. Retail identity: <c>0x0040DE40</c> in the
/// pristine <c>74154bfa…</c> image, file offset = VA - 0x400000.
/// </para>
/// <para>
/// <b>Source and retail agree throughout, and the image pins three numbers the
/// header only names.</b> <c>MAX_AUG_VALUE</c> is
/// <c>BattleEngine.cpp:53</c> and the shipped store is
/// <c>mov dword ptr [esi + 0x2F8], 0x41200000</c> at <c>0x0040DF0C</c> —
/// <c>10.0f</c>. <c>MAX_ZOOM_OUT</c> is <c>BattleEngine.cpp:51</c> and the
/// inlined <c>AutoZoomOut</c> is
/// <c>mov dword ptr [esi + 0x2CC], 0x3F800000</c> at <c>0x0040DEE7</c> —
/// <c>1.0f</c>. The sample name is the literal <c>"hud_weapon_augmented"</c> at
/// <c>0x00623540</c>, matching <c>BattleEngine.cpp:3323</c> character for
/// character.
/// </para>
/// <para>
/// <b>The field offsets corroborate the header's declaration order.</b>
/// <c>BattleEngine.h:381-387</c> declares <c>mAugValue</c>, <c>mAugActive</c>,
/// <c>mAugActiveTime</c>, then (after two unrelated fields)
/// <c>mAugmentedTime</c>; the shipped stores land on <c>+0x2F8</c>,
/// <c>+0x2FC</c>, <c>+0x300</c> and <c>+0x30C</c>, which is that order at four
/// bytes each with the two intervening fields accounted for. That is an
/// independent check on the header rather than a use of it.
/// </para>
/// <para>
/// <b>The <c>AutoZoomOut</c> arm inside this body is dead.</b>
/// <c>BattleEngine.cpp:3316</c> re-reads the current weapon's zoom mode after
/// <c>LoseWeaponCharge()</c> and compares it against the entry value, and
/// retail does exactly that at <c>0x0040DED2-0x0040DEE5</c>. But
/// <c>LoseWeaponCharge</c> resolves to a single
/// <c>mov dword ptr [weapon + 0x60], 0</c> (see
/// <see cref="RetailWeaponCharge.LoseCharge"/>) and the current weapon does not
/// change, so the zoom mode at <c>record + 0x34</c> is provably the same word
/// on both reads and the store to <c>mDesiredZoom</c> is unreachable. The same
/// three lines in <c>ChangeWeapon</c> are <i>not</i> dead, because there the
/// current index moves — see <see cref="RetailWeaponCycle"/>. This is recorded,
/// not modelled: <see cref="RetailAugmentResult"/> has no zoom field because
/// retail never writes one here.
/// </para>
/// <para>
/// <b>The state ladder is a two-way compare, not a switch.</b>
/// <c>0x0040DEA3</c> is <c>cmp eax, 2</c> and <c>0x0040DEC2</c> is
/// <c>cmp eax, 3</c>, against the <c>mState</c> word at <c>+0x260</c>; anything
/// else forwards to neither part. With the ordinals
/// <see cref="RetailBattleEngineState"/> measures, that is
/// <c>BattleEngine.cpp:2022-2025</c> exactly.
/// </para>
/// <para>
/// <b>Not established here.</b> Whether the primary weapon <i>is</i> the current
/// weapon is a pointer identity in retail (<c>cmp edi, eax</c> at
/// <c>0x0040DE89</c>) and arrives here as a boolean. The gate reads the primary
/// weapon's store unconditionally through <c>[mWalkerPart + 0x18]</c>, so a
/// battle engine with no primary weapon faults before the gate — outside this
/// contract. The two time stamps are the raw dword at <c>0x00672FD0</c> moved
/// with <c>mov</c>, not through the x87, so they are the event manager's float
/// bits verbatim; that global lives past <c>0x00661000</c> and so has no bytes
/// in the file image, only a bss address.
/// </para>
/// </remarks>
public static class RetailWeaponAugment
{
    /// <summary><c>MAX_AUG_VALUE</c> — <c>BattleEngine.cpp:53</c>, stored as <c>0x41200000</c> at <c>0x0040DF0C</c>.</summary>
    public const float MaxAugValue = 10.0f;

    /// <summary><c>MAX_ZOOM_OUT</c> — <c>BattleEngine.cpp:51</c>, stored as <c>0x3F800000</c> at <c>0x0040DEE7</c> on the dead arm.</summary>
    public const float MaxZoomOut = 1.0f;

    /// <summary>The augment sample name — the literal at <c>0x00623540</c>.</summary>
    public const string AugmentSampleName = "hud_weapon_augmented";

    /// <summary>
    /// <c>CBattleEngine::LoseWeaponCharge</c> —
    /// <c>BattleEngine.cpp:2020-2026</c>, inlined at
    /// <c>0x0040DEA3-0x0040DED0</c>.
    /// </summary>
    public static RetailChargeLossTarget ChargeLossTarget(int state) => state switch
    {
        (int)RetailBattleEngineState.Walker => RetailChargeLossTarget.WalkerPart,
        (int)RetailBattleEngineState.Jet => RetailChargeLossTarget.JetPart,
        _ => RetailChargeLossTarget.None,
    };

    /// <summary>
    /// The store gate at <c>0x0040DE5C-0x0040DE79</c>: an energy store always
    /// passes, an ammunition store passes while it holds more than nothing.
    /// </summary>
    public static bool IsAugmentable(RetailWeaponStores stores, int primaryAmmoStore)
    {
        if (stores is null)
        {
            throw new ArgumentNullException(nameof(stores));
        }

        if (stores.StoreHeat[primaryAmmoStore] != 0)
        {
            return true;
        }

        // test ah, 0x41 with jne to the exit: an unordered value closes the gate.
        return stores.StoreValue[primaryAmmoStore] > 0.0f;
    }

    /// <summary>
    /// <c>CBattleEngine::AugmentWeapon</c> — <c>BattleEngine.cpp:3302-3326</c>,
    /// <c>0x0040DE40</c>.
    /// </summary>
    /// <param name="stores">The chassis stores; the gate reads the primary weapon's.</param>
    /// <param name="primaryAmmoStore"><c>mWalkerPart-&gt;GetPrimaryWeapon()-&gt;GetAmmoStore()</c>.</param>
    /// <param name="primaryIsCurrentWeapon">The pointer identity at <c>0x0040DE89</c>.</param>
    /// <param name="state">The raw <c>mState</c> word at <c>+0x260</c>.</param>
    /// <param name="now">The event manager time word at <c>0x00672FD0</c>.</param>
    public static RetailAugmentResult AugmentWeapon(
        RetailWeaponStores stores,
        int primaryAmmoStore,
        bool primaryIsCurrentWeapon,
        int state,
        float now)
    {
        if (!IsAugmentable(stores, primaryAmmoStore))
        {
            return default;
        }

        RetailChargeLossTarget target = primaryIsCurrentWeapon
            ? ChargeLossTarget(state)
            : RetailChargeLossTarget.None;

        return new RetailAugmentResult(
            Augmented: true,
            ClearsSlowMovement: primaryIsCurrentWeapon,
            ChargeLossTarget: target,
            AugValue: MaxAugValue,
            AugActiveTime: now,
            AugmentedTime: now);
    }
}

/// <summary>
/// <c>CBattleEngine::HostileEnvironment</c> — the released five-second gate on
/// the hostile-environment warning.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngine.cpp:3269-3278</c>. Retail identity:
/// <c>0x0040DCE0</c> in the pristine <c>74154bfa…</c> image. The interval is
/// <c>0x005D85D8</c> = <c>0x40A00000</c> = <c>5.0f</c>, and both literals the
/// source names are in the image verbatim: <c>"hud_hostile_environment"</c> at
/// <c>0x00623528</c> and the log line at <c>0x00623500</c>, double space and
/// all.
/// </para>
/// <para>
/// <b>Source and retail agree, including the unconditional stamp.</b> The
/// warning fires when the elapsed time is strictly more than five seconds
/// (<c>test ah, 0x41</c> with <c>jne</c> to the skip label at
/// <c>0x0040DD00</c>, so C0 or C3 — and an unordered elapsed time — suppresses
/// it, exactly as <c>&gt; 5.0f</c> would). <c>mLastTimeInHostileEnviroment</c>
/// at <c>+0x510</c> is then rewritten on <b>both</b> paths, at
/// <c>0x0040DD70</c> and <c>0x0040DD83</c>, which is the source's placement of
/// the assignment after the <c>if</c>. So repeated calls inside the window keep
/// pushing the window out and the warning can be starved indefinitely.
/// </para>
/// <para>
/// <b>The elapsed time is never rounded to float.</b> <c>0x0040DCE0</c> loads
/// the time global and <c>0x0040DCEF</c> subtracts the stored float straight on
/// the x87 stack, with no intervening store, so the comparison sees the exact
/// difference of two floats at the ambient 53-bit precision. This type
/// reproduces that in <c>double</c>. Rounding the subtraction to float first
/// would be observable: two times far enough apart in magnitude round the
/// difference across the five-second boundary.
/// </para>
/// <para>
/// <b>Not established here.</b> The stamp is written with a plain
/// <c>mov</c> of the global's dword, not through the x87, so it carries the
/// event manager's bits verbatim — including a NaN, which then makes every
/// subsequent elapsed time unordered and suppresses the warning forever. Whether
/// the time global can hold a NaN is outside this contract. The sample and log
/// calls are presentation and are not modelled; only whether they happen is.
/// </para>
/// </remarks>
public static class RetailHostileEnvironment
{
    /// <summary>The re-announce interval — <c>0x005D85D8</c>, bits <c>0x40A00000</c>.</summary>
    public const float AnnounceInterval = 5.0f;

    /// <summary>The warning sample name — the literal at <c>0x00623528</c>.</summary>
    public const string WarningSampleName = "hud_hostile_environment";

    /// <summary>The dormant log line — the literal at <c>0x00623500</c>.</summary>
    public const string WarningLogMessage = "playing sample :  hostile environment";

    /// <summary>
    /// Whether this call plays the warning —
    /// <c>BattleEngine.cpp:3271</c>, <c>0x0040DCE0-0x0040DD00</c>.
    /// </summary>
    public static bool ShouldWarn(float now, float lastWarningTime) =>
        (double)now - (double)lastWarningTime > (double)AnnounceInterval;

    /// <summary>
    /// The value written to <c>mLastTimeInHostileEnviroment</c> —
    /// <c>BattleEngine.cpp:3277</c>, <c>0x0040DD70</c> and <c>0x0040DD83</c>.
    /// Unconditional, and the time global's bits verbatim.
    /// </summary>
    public static float NextLastWarningTime(float now, float lastWarningTime)
    {
        _ = lastWarningTime;
        return now;
    }
}
