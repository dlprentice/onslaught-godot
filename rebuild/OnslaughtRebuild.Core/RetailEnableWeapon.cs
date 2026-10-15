// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::EnableWeapon</c> — the walker
/// <c>mov dword ptr [edi+0x9c], 1</c> store.
/// Isolated <see cref="Level100WeaponAvailabilityChanged.Enabled"/>
/// stays the rebuild bool. Disable's store-0 / ChangeWeapon
/// reselect stay unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__EnableWeapon</c> at
/// <c>0x00534fb0</c> through <c>ret 0xc</c> at <c>0x00534fd6</c>
/// (41 bytes) on the official <c>74154bfa…</c> specimen. File
/// offset = VA − 0x400000. Re-derived this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes). Body is
/// <c>56 8b f1 8b 46 10 f6 40 34 10 74 19 8b 4c 24 08 57 8b 38 8b 09 8b 11 ff 52 38 8b 4e 10 50 ff 97 98 01 00 00 5f 5e c2 0c 00</c>,
/// SHA-256
/// <c>b71e37ce12f5817c5ae1279d2db42a7c5f1348213ca46785cc5bc35c92766eb4</c>.
/// <c>0x00534fce</c> is <c>ff 97 98 01 00 00</c> =
/// <c>call [edi+0x198]</c>. W001 / vtable slot 102: the Level 100
/// player override is <c>CBattleEngine</c> <c>0x0040dc30</c>
/// (37 B SHA-256
/// <c>fa72dd3ff85e273d09fc380eeac638fd7559c07bded84a94d796a390eb92f839</c>),
/// which forwards the name to walker <c>0x00414970</c> then jet
/// <c>0x004127a0</c>. Source
/// <c>references/Onslaught/BattleEngine.cpp:3229-3233</c>.
/// </para>
/// <para>
/// <b>The walker helper writes literal 1 at <c>weapon+0x9c</c>.</b>
/// <c>0x004149c7</c> is
/// <c>c7 87 9c 00 00 00 01 00 00 00</c> =
/// <c>mov dword ptr [edi+0x9c], 1</c> after an inline name compare
/// against <c>[weapon+0xa4]</c>. Twin store at <c>0x00414a28</c>
/// for the walker primary. Jet <c>0x004127f7</c> is the same
/// immediate. W001 primary A12 names <c>0x00414970</c>. Source
/// <c>BattleEngineWalkerPart.cpp:1033-1041</c> is
/// <c>weapon-&gt;SetActive(TRUE)</c>. Isolated
/// <c>Enabled</c> = true names the rebuild bool / availability
/// enum; skip this store still leaves that bool. Mutation:
/// increment so a second Enable becomes 2.
/// </para>
/// <para>
/// The wrapper gate <c>test [eax+0x34], 0x10</c>, Disable's
/// store-0 / ChangeWeapon reselect, and the jet dual-forward
/// for names the walker list does not own stay unclaimed.
/// ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed.
/// Live <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailEnableWeapon
{
    /// <summary>
    /// <c>CWeapon</c> active flag — <c>this+0x9c</c>.
    /// Already named by <see cref="RetailMountedWeapon.IsActive"/>.
    /// </summary>
    public const int FlagOffset = 0x9c;

    /// <summary>
    /// The <c>mov [edi+0x9c], 1</c> immediate at
    /// <c>0x004149c7</c>.
    /// </summary>
    public const int FlagEnabled = 1;

    /// <summary>
    /// Opening dword before the first Enable. Disable's
    /// store-0 is not this pin. Start-active at
    /// configuration reset stays unclaimed.
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
