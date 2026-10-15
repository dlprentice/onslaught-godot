// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::EnableFlightMode</c> — the callee
/// <c>mov dword ptr [ecx+0x58c], 1</c>. Isolated
/// <see cref="Level100MissionSnapshot.FlightModeEnabled"/> stays
/// the rebuild bool. Disable's clear / morph stay unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__EnableFlightMode</c> at
/// <c>0x00535070</c> through <c>ret 0xc</c> at <c>0x0053507e</c>
/// (17 bytes) on the official <c>74154bfa…</c> specimen. File
/// offset = VA − 0x400000. Re-derived this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes). Body is
/// <c>8b 49 10 f6 41 34 08 74 05 e8 32 8c ed ff c2 0c 00</c>,
/// SHA-256
/// <c>765a278701bdcb43ce01c53f575357b37c8466150ece560abe63de3f09a665a8</c>.
/// W001 names the inbound CALL at <c>0x00535079</c>.
/// </para>
/// <para>
/// <b>The callee writes literal 1 at <c>this+0x58c</c>.</b>
/// <c>0x0040dcb0</c> is
/// <c>c7 81 8c 05 00 00 01 00 00 00 c3</c> =
/// <c>mov dword ptr [ecx+0x58c], 1</c> / <c>ret</c>. Body
/// SHA-256
/// <c>aac88ccb37a4df2655331f224a89213ae051d37715c820078229e3ebef65b4a7</c>.
/// W001 primary A09 / adversarial B09: two-insn setter
/// <c>*(this+0x58c)=1</c>. PROVENANCE.md already names the
/// released flight flag at <c>0x58C</c> off during the opening.
/// Isolated <c>FlightModeEnabled</c> = true names the rebuild
/// bool; skip this store still leaves that bool true. Mutation:
/// increment so a second Enable becomes 2.
/// </para>
/// <para>
/// The wrapper gate <c>test [ecx+0x34], 8</c> and Disable's
/// <c>0x0040dcc0</c> clear / morph-if-state-3 stay unclaimed.
/// ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed.
/// Live <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailEnableFlightMode
{
    /// <summary>
    /// <c>CBattleEngine</c> flight flag — <c>this+0x58c</c>.
    /// </summary>
    public const int FlagOffset = 0x58c;

    /// <summary>
    /// The <c>mov [ecx+0x58c], 1</c> immediate at
    /// <c>0x0040dcb0</c>.
    /// </summary>
    public const int FlagEnabled = 1;

    /// <summary>
    /// Opening / LoadLevel store. PROVENANCE.md keeps
    /// <c>0x58C</c> off before beat-6 Enable. Disable's clear
    /// is not this pin.
    /// </summary>
    public const int FlagDisabled = 0;

    /// <summary>
    /// The literal-1 store. Mutation: increment so Enable(1)
    /// becomes 2.
    /// </summary>
    public static int Enable(int currentFlag)
    {
        _ = currentFlag;
        return FlagEnabled;
    }
}
