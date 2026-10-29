// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Core;

/// <summary>
/// The five charge levels a weapon's physics record carries, and the current
/// charge held on the weapon itself.
/// </summary>
/// <remarks>
/// <para>
/// There is no owner for this in the pinned drop: <c>CWeapon</c> is not one of
/// the 108 files in the partial GPL release, and every citation the manifest
/// offers for the charge readouts (<c>BattleEngineJetPart.cpp:879-885</c>,
/// <c>BattleEngineWalkerPart.cpp:879-885</c>,
/// <c>BattleEngineWalkerPart.cpp:519-559</c>) says only
/// <c>weapon-&gt;GetCharge()</c>, <c>weapon-&gt;CanCharge()</c>,
/// <c>weapon-&gt;FullyCharged()</c>, <c>weapon-&gt;LoseCharge()</c>. So the
/// source agrees with retail at the level it states — it delegates — and
/// everything below is measured from the image alone.
/// </para>
/// <para>
/// The layout falls out of the three bodies that inline those calls in the
/// pristine <c>74154bfa…</c> image, file offset = VA - 0x400000: the physics
/// record is at <c>weapon + 0xA4</c>, the charge-level table is five dwords
/// starting at <c>record + 0x0C</c>, and the live charge is the float at
/// <c>weapon + 0x60</c>. <c>0xFFFFFFFF</c> marks a level as absent; the scans
/// compare against <c>-1</c> as a signed dword.
/// </para>
/// <para>
/// <b>The count of five and the step of 100 are read off the loop bounds, not
/// assumed.</b> Every scan is the same shape: a pointer walking by 4 and a
/// counter walking by <c>0x64</c> against (a compare of eax with 0x1F4, branching when less)
/// (<c>0x004123D5</c>, <c>0x00413DAD</c>), or a plain index against
/// (a compare of eax with 5, branching when less) (<c>0x00413D36</c>, <c>0x00413DC4</c>).
/// <c>0x1F4 / 0x64</c> is five iterations, which is the same five.
/// </para>
/// </remarks>
public sealed class RetailWeaponChargeTable
{
    /// <summary>Five levels — a compare of eax with 0x1F4 against a step of <c>0x64</c>.</summary>
    public const int LevelCount = 5;

    /// <summary>The absent-level marker — a compare of value pointed to by ecx with -1.</summary>
    public const int AbsentLevel = -1;

    /// <summary>The charge value one level is worth — the addition of 0x64 to eax step.</summary>
    public const int ValuePerLevel = 100;

    /// <summary>The table at <c>weapon-&gt;record + 0x0C</c>, five dwords.</summary>
    public int[] Levels { get; } = new int[LevelCount] { AbsentLevel, AbsentLevel, AbsentLevel, AbsentLevel, AbsentLevel };

    /// <summary>
    /// <c>CWeaponChargeRate</c> — the float at <c>record + 0x08</c>, added into
    /// <see cref="Charge"/> by <see cref="RetailWeaponCharge.Charge"/>.
    /// </summary>
    public float ChargeRate { get; set; }

    /// <summary><c>mCharge</c> — the float at <c>weapon + 0x60</c>.</summary>
    public float Charge { get; set; }

    /// <summary>
    /// Next-ready time — the float at <c>weapon + 0x64</c>. Fire stores
    /// <c>now + CWeaponReloadTime</c> here; <see cref="RetailWeaponCharge.ReadyToCharge"/>
    /// compares <c>0x00672FD0</c> against it.
    /// </summary>
    public float ReadyAtTime { get; set; }

    /// <summary>
    /// Whether <c>weapon + 0xA0</c> is non-zero. A configured Pulse Cannon Pod
    /// carries a current weapon-mode pointer here, so the time compare runs.
    /// A null field makes <see cref="RetailWeaponCharge.ReadyToCharge"/> return
    /// true without looking at <see cref="ReadyAtTime"/>.
    /// </summary>
    public bool ReadyToChargeGateActive { get; set; } = true;
}

/// <summary>
/// <c>CWeapon::GetCharge</c>, <c>CanCharge</c>, <c>FullyCharged</c>,
/// <c>LoseCharge</c> and <c>Charge</c>, recovered from the three battle-engine
/// bodies that inline the first four and from the increment helper they call.
/// Each is a pure function of the charge-level table, the charge rate, and the
/// live charge.
/// </summary>
/// <remarks>
/// <para>
/// Retail identities in the pristine <c>74154bfa…</c> image:
/// </para>
/// <list type="bullet">
/// <item><c>0x00412370</c> <c>CBattleEngineJetPart::GetWeaponCharge</c> — <c>GetCharge</c> inlined whole at <c>0x004123B9-0x00412406</c>.</item>
/// <item><c>0x00412000</c> <c>CBattleEngineJetPart::LoseWeaponCharge</c> — <c>LoseCharge</c> inlined to one store at <c>0x0041203B</c>.</item>
/// <item><c>0x00413CF0</c> <c>CBattleEngineWalkerPart::ChargeWeapon</c> — <c>CanCharge</c> at <c>0x00413D2B</c> and <c>FullyCharged</c> at <c>0x00413D91-0x00413DE1</c>.</item>
/// <item><c>0x00411F96</c> — the same <c>LoseCharge</c> store again, inside <c>ChangeWeapon</c>.</item>
/// <item><c>0x005068F0</c> <c>CWeapon__AdvanceChargeProgressIfAnySlotAssigned</c> — <c>weapon-&gt;Charge()</c>, called from <c>0x00413E04</c>.</item>
/// </list>
/// <para>
/// <b><c>GetCharge</c> is a fraction of the highest present level, and it
/// reports zero when the only level present is the first.</b>
/// <c>0x004123C8-0x004123DA</c> walks all five entries with no early exit,
/// setting the running maximum to <c>index * 100</c> at every entry that is not
/// <c>-1</c>, so the answer is the <b>last</b> present index, not the first and
/// not a count. The maximum starts at zero and index 0 also contributes zero, so
/// a weapon whose table is <c>{n, -1, -1, -1, -1}</c> is indistinguishable from
/// one whose table is empty: a zero test of esi, branching when equal at <c>0x004123DC</c>
/// returns <c>0.0f</c> for both. That is a real property of the shipped code and
/// a rebuild that returned <c>charge / 0</c>, or that treated index 0 as worth
/// 100, would part company on exactly those weapons.
/// </para>
/// <para>
/// <b>The scan runs twice and the second run is redundant, not different.</b>
/// <c>0x004123C8</c> and <c>0x004123EA</c> are the same loop over the same
/// immutable table, the first filling a register and the second filling the
/// stack slot the <c>fild</c> reads. Nothing between them writes the table. This
/// is recorded so a reader does not go looking for a distinction; the model runs
/// it once.
/// </para>
/// <para>
/// <b><c>CanCharge</c> ignores the first level.</b> <c>0x00413D28</c> is
/// an address computation (ebx+0x10) into ebp — <c>record + 0x10</c>, i.e. index 1 — and the
/// counter starts at <c>1</c>. So a weapon with only level 0 present cannot
/// charge, which is the same population that <c>GetCharge</c> reports as zero.
/// </para>
/// <para>
/// <b><c>FullyCharged</c> reduces to one comparison on every reachable
/// path.</b> The inlined body at <c>0x00413DB4</c> re-runs the <c>CanCharge</c>
/// scan and bails out of <c>ChargeWeapon</c> if it finds nothing, but control
/// only arrives there through the scan at <c>0x00413D2B</c>, which has already
/// proved a present level in 1..4 over the same unchanged table. So that arm is
/// <b>dead</b> in the shipped code, exactly as the stranded <c>mStoreHeat</c>
/// load in <see cref="RetailWeaponStoreReadouts"/> is, and what remains is
/// an integer-to-float load of max, then float compare against mCharge, popping the result,
/// then bit test of ah against 0x41, branching when not equal: not fully charged iff
/// the maximum is <b>ordered and strictly greater</b> than the charge. The dead
/// arm is still modelled by <see cref="FullyCharged"/>, because a caller other
/// than <c>ChargeWeapon</c> could reach it.
/// </para>
/// <para>
/// <b>An unordered charge reads as fully charged.</b> a bit test of ah against 0x41 is
/// C0 or C3 and an unordered compare sets both, so a NaN <c>mCharge</c> takes
/// the bail-out arm. The maximum is an integer widened by <c>fild</c> and can
/// never be unordered, so that is the only way in.
/// </para>
/// <para>
/// <b><c>Charge</c> adds the rate under a 400.0 cap that is not MaxCharge.</b>
/// <c>0x005068F0</c> re-runs the CanCharge scan from <c>record + 0x10</c> and
/// returns if nothing in 1..4 is present. Otherwise it
/// <c>fcomp</c>s <c>mCharge</c> against <c>0x005DB358</c> =
/// 0x43C80000 = <c>400.0f</c> with a bit test of ah against 1, branching when equal, then
/// a float load of field +8 of record, then float add of field +0x60 of weapon, then float store to field +0x60 of weapon. So a
/// charge of 100 still increments, and a charge of 400 does not. The
/// Pulse Cannon Pod fills at 100 only because <c>ChargeWeapon</c> at
/// <c>0x00413CF0</c> stops calling this helper once
/// <see cref="FullyCharged"/> is true.
/// </para>
/// <para>
/// <b>Not established here.</b> What the five levels <i>mean</i> is not
/// claimed — only that they are read as present/absent markers and that their
/// index scales the charge. Whether <c>-1</c> is the only absent marker, and
/// whether the table is ever written at run time, are open; every access seen
/// here is a read. The division at <c>0x00412404</c> is <c>fdivr</c> on the x87
/// stack and is returned in <c>st(0)</c>, so the caller receives a
/// double-precision quotient; this type rounds it to float, which is safe for
/// the reason <see cref="RetailWeaponStoreReadouts"/> sets out at length —
/// binary64 carries more than twice binary32's significand, so the double
/// rounding is innocuous for a single divide. <c>ReadyToCharge</c> at
/// <c>0x0050A080</c> gates the increment; the energy-store add of
/// consumption and overheat-to-fire remain the next ChargeWeapon arms. Fire
/// at FullyCharged selects Charged 2 / <c>Mech Pulse Bolt Large</c>.
/// </para>
/// </remarks>
public static class RetailWeaponCharge
{
    /// <summary>
    /// The running maximum of <c>index * 100</c> over present levels —
    /// <c>0x004123C8-0x004123DA</c>. Zero when nothing but level 0 is present.
    /// </summary>
    public static int MaxCharge(RetailWeaponChargeTable weapon)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        int max = 0;
        for (int index = 0; index < RetailWeaponChargeTable.LevelCount; index++)
        {
            if (weapon.Levels[index] != RetailWeaponChargeTable.AbsentLevel)
            {
                max = index * RetailWeaponChargeTable.ValuePerLevel;
            }
        }

        return max;
    }

    /// <summary>
    /// <c>CWeapon::GetCharge</c> as inlined at <c>0x004123B9-0x00412406</c>.
    /// </summary>
    public static float GetCharge(RetailWeaponChargeTable weapon)
    {
        int max = MaxCharge(weapon);
        if (max == 0)
        {
            return 0.0f;
        }

        // fild max / fdivr mCharge: the dividend is the weapon's charge.
        return (float)((double)weapon.Charge / (double)max);
    }

    /// <summary>
    /// <c>CWeapon::CanCharge</c> as inlined at <c>0x00413D2B-0x00413D39</c>.
    /// Levels 1..4 only.
    /// </summary>
    public static bool CanCharge(RetailWeaponChargeTable weapon)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        for (int index = 1; index < RetailWeaponChargeTable.LevelCount; index++)
        {
            if (weapon.Levels[index] != RetailWeaponChargeTable.AbsentLevel)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>CWeapon::FullyCharged</c> as inlined at
    /// <c>0x00413DB4-0x00413DE1</c>. See the type remarks: the
    /// <see cref="CanCharge"/> half is dead on the only path retail takes.
    /// </summary>
    public static bool FullyCharged(RetailWeaponChargeTable weapon)
    {
        if (!CanCharge(weapon))
        {
            return true;
        }

        // A bit test of ah against 0x41 with jne: an unordered charge bails out too.
        return !((double)MaxCharge(weapon) > (double)weapon.Charge);
    }

    /// <summary>
    /// <c>CWeapon::LoseCharge</c> as inlined at <c>0x0041203B</c> and
    /// <c>0x00411F96</c>: a store of constant 0 to field +0x60 of weapon, an integer
    /// store of the all-zero word, i.e. <c>+0.0f</c> and not <c>-0.0f</c>.
    /// </summary>
    public static void LoseCharge(RetailWeaponChargeTable weapon)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        weapon.Charge = BitConverter.UInt32BitsToSingle(0u);
    }

    /// <summary>
    /// The increment-body cap — <c>0x005DB358</c> = 0x43C80000 =
    /// <c>400.0f</c>. This is not <see cref="MaxCharge"/>; Pulse Cannon Pod
    /// fills at 100 because <c>ChargeWeapon</c> stops calling this helper.
    /// </summary>
    public const float IncrementCap = 400.0f;

    /// <summary>
    /// <c>CWeapon__AdvanceChargeProgressIfAnySlotAssigned</c> at
    /// <c>0x005068F0</c>. The CanCharge scan starts at <c>record + 0x10</c>;
    /// when a later level is present and the live charge is strictly below
    /// <see cref="IncrementCap"/> (a bit test of ah against 1, branching when equal), it does
    /// (a float load of field +8 of record, then float add of field +0x60 of weapon, then float store to field +0x60 of weapon).
    /// </summary>
    public static void Charge(RetailWeaponChargeTable weapon)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        if (!CanCharge(weapon))
        {
            return;
        }

        // A bit test of ah against 1 / je: skip when C0 is clear, i.e. charge >= 400 ordered.
        // An unordered charge sets C0 and still adds.
        if (weapon.Charge >= IncrementCap)
        {
            return;
        }

        weapon.Charge = (float)((double)weapon.ChargeRate + (double)weapon.Charge);
    }

    /// <summary>
    /// <c>CWeapon::ReadyToCharge</c> at <c>0x0050A080</c> in the pristine
    /// <c>74154bfa…</c> image (35 bytes, <c>1ba024f1f14e…</c>). The Ghidra
    /// name <c>TargetProfileContext__CanProceedByTargetRangeGate</c> is the
    /// stale owner; <c>CBattleEngineWalkerPart::ChargeWeapon</c> at
    /// <c>0x00413CF0</c> calls this on the current <c>CWeapon</c> as
    /// <c>weapon-&gt;ReadyToCharge()</c>.
    /// </summary>
    /// <remarks>
    /// A load of field +0xA0 of ecx into eax, then zero test of eax, branching when zero then
    /// a float load of the value at 0x00672FD0, then float compare against field +0x64 of
    /// ecx, popping the result, then FPU status-word read, then bit test of ah against 0x41,
    /// branching when zero, then zeroing of eax. With a non-null mode, engine time must be
    /// ordered-greater than +0x64. Equality and unordered comparisons refuse
    /// both Charge and Fire; Fire uses the same test at <c>0x005060E5</c>.
    /// </remarks>
    public static bool ReadyToCharge(RetailWeaponChargeTable weapon, float now)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        if (!weapon.ReadyToChargeGateActive)
        {
            return true;
        }

        return ReadyTimeElapsed(now, weapon.ReadyAtTime);
    }

    /// <summary>
    /// The charge-level mode a weapon's mode getters and Fire select
    /// (<c>CWeapon::Fire</c> <c>0x00506010</c>; the lock getters
    /// <c>0x00506350</c>-<c>0x00506800</c>; <c>GetActualMaxRange</c>
    /// <c>0x00509c80</c>): <c>fistp</c> the live charge under the ambient
    /// round-to-nearest-even control word, divide by 100 with truncation, and
    /// fall back to the nearest present lower level.
    /// </summary>
    /// <returns>The level index, or -1 when no level at or below it is present.</returns>
    public static int ModeLevel(RetailWeaponChargeTable weapon)
    {
        if (weapon is null)
        {
            throw new ArgumentNullException(nameof(weapon));
        }

        double rounded = Math.Round((double)weapon.Charge, MidpointRounding.ToEven);
        if (!(rounded >= 0.0) || rounded >= (double)(RetailWeaponChargeTable.LevelCount * RetailWeaponChargeTable.ValuePerLevel))
        {
            throw new InvalidOperationException(
                "A weapon charge outside the five-level table has no admitted mode.");
        }

        for (int level = (int)rounded / RetailWeaponChargeTable.ValuePerLevel; level >= 0; level--)
        {
            if (weapon.Levels[level] != RetailWeaponChargeTable.AbsentLevel)
            {
                return level;
            }
        }

        return -1;
    }

    /// <summary>
    /// The ordered strict comparison shared by Fire and ReadyToCharge after
    /// a current mode is resolved. A bit test of ah against 0x41, branching when zero accepts only
    /// C0|C3 clear; NaN, equality and earlier time all refuse.
    /// </summary>
    public static bool ReadyTimeElapsed(float now, float readyAt) => now > readyAt;
}
