// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::Pause</c> / <c>PlayCharMessageWait</c> —
/// <c>mov dword ptr [0x0089c800], 1</c>. Isolated
/// <see cref="Level100MissionTiming.PauseTicks"/> /
/// <see cref="Level100MissionTiming.MessagePlaybackTicks"/> stay
/// the rebuild sleep. CVM snapshot copy, the 0.05f resume,
/// FollowWaypointWait, PlayAnimationWait, and Run-yield
/// meaning stay unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: independently re-read this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>).
/// File offset = VA − 0x400000.
/// <c>IScript__Pause</c> at <c>0x00537c70</c> through
/// <c>ret 0xc</c> at <c>0x00537d66</c> is 249 bytes, SHA-256
/// <c>2bdf08ea7dc1e678471d738fd05f850037eabc16d4eb271af609ae728d62829f</c>.
/// <c>IScript__PlayCharMessageWait</c> at <c>0x005375f0</c>
/// through <c>ret 0xc</c> at <c>0x005377d5</c> is 488 bytes,
/// SHA-256
/// <c>7d65b4bc6f7b01e86b6cdaccd3a50204a48c4c1d9add09be151235f18eca55e3</c>.
/// Function note <c>IScript.cpp.md</c> already names the
/// singleton store (byte-exact 2026-08-18).
/// </para>
/// <para>
/// <b>Both natives write literal 1 at <c>0x0089c800</c>.</b>
/// <c>0x00537d55</c> (Pause) and <c>0x005376f9</c>
/// (PlayCharMessageWait) are
/// <c>c7 05 00 c8 89 00 01 00 00 00</c> =
/// <c>mov dword ptr [0x0089c800], 1</c>. That address is
/// singleton <c>+0x220</c> (the stop flag). Isolated
/// pause / message duration names the rebuild sleep;
/// skip this store still leaves those sleeps. One live
/// store of 1 is not unique versus increment from 0.
/// Mutation: increment so a second Wait becomes 2.
/// </para>
/// <para>
/// <b>The flag's other readers and writers</b> (the RE lane's audit,
/// re-read here). Pause stores it (<c>0x00537d55</c>) before its
/// <c>AddEvent</c> call (<c>0x00537d5f</c>).
/// <c>CScriptObjectCode::Run</c> (<c>0x00539b00</c>) clears it on
/// entry (<c>0x00539b47</c>), reads it before each instruction and
/// exits when it is set (<c>0x00539b74</c>), and sets it at its
/// 10,000-instruction guard (<c>0x00539bf9</c>); an exit with the flag
/// set zeroes <c>+0x20c</c> and skips the stack-balance check
/// (<c>0x00539c0f-0x00539c1f</c>). <c>CopyState</c> restores it from
/// the snapshot (<c>0x00539948</c>).
/// </para>
/// <para>
/// PlayPCharMessageWait / FollowWaypointWait /
/// PlayAnimationWait, the 0x228 CVM snapshot and the
/// <c>0.05f</c> CLOCK_TICK resume stay unclaimed.
/// ChargeWeapon stays unclaimed. Live
/// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailIScriptWaitStop
{
    /// <summary>
    /// CVM singleton stop flag — <c>0x0089c800</c>.
    /// </summary>
    public const int FlagAddress = 0x0089c800;

    /// <summary>
    /// Singleton <c>+0x220</c> — the stop-flag slot
    /// the Wait snapshot copies before this store.
    /// The copy itself is not this pin.
    /// </summary>
    public const int SingletonOffset = 0x220;

    /// <summary>
    /// The <c>mov [0x0089c800], 1</c> immediate at
    /// <c>0x00537d55</c> / <c>0x005376f9</c>.
    /// </summary>
    public const int FlagStopped = 1;

    /// <summary>
    /// Unread / never-waited dword. That is not a
    /// BSS-init or <c>InitRuntime</c> claim.
    /// <c>InitRuntime</c> does not list <c>+0x220</c>
    /// among its zeroes.
    /// </summary>
    public const int FlagIdle = 0;

    /// <summary>
    /// The literal-1 store. Mutation: increment so
    /// Stop(1) becomes 2.
    /// </summary>
    public static int Stop(int currentFlag)
    {
        _ = currentFlag;
        return FlagStopped;
    }
}
