// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::InJetMode</c> — a bit test of field +0x34 of ecx against 8 then
/// the negation of the recently-grounded-walker callee.
/// Isolated <see cref="Level100MissionTiming.JetModeState"/>
/// names the recency predicate without the type gate.
/// Isolated <c>PlayerInJetMode == (mode == Jet)</c> names the
/// rebuild bool. Lock-set / Move / Morph / UpdateCamera stay
/// unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: independently re-read this session from
/// a private pristine copy
/// (2,506,752 bytes, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>).
/// File offset = VA − 0x400000.
/// <c>IScript__InJetMode</c> at <c>0x005380f0</c> through
/// a return that pops 12 bytes of arguments at <c>0x00538147</c> is 90 bytes, SHA-256
/// <c>0ae6c37da7b854bde6569bf19f41b3b37a9cbad4a93146076f0b1bd3866486f0</c>.
/// Callee <c>0x00408120</c> through <c>ret</c> at
/// <c>0x0040814a</c> is 43 bytes, SHA-256
/// <c>81ab0371f154f611a7c0009a81235ba0a63d0d46e75409a15587af9b7fb7610e</c>.
/// Function note
/// <c>Level100MissionTiming.JetModeState</c> already names
/// this body (byte-exact 2026-08-19).
/// </para>
/// <para>
/// <b>Non-Battle-Engine things are never in jet mode.</b>
/// <c>0x005380f6</c> is a bit test of byte field +0x34 of ecx against 8.
/// <c>je</c> leaves ESI = 0. Isolated
/// <see cref="Level100MissionTiming.JetModeState"/> of a jet
/// is still InJetMode. Mutation: skip the type gate so a
/// non-BE jet is TRUE.
/// </para>
/// <para>
/// <b>The wrapper negates the recently-grounded walker.</b>
/// Callee <c>0x00408120</c> is
/// a compare of field +0x260 of ecx with 2 then
/// a reversed float subtract of the value at 0x00672fd0, then float compare against the value
/// at 0x005d85ec, popping the result, then bit test of ah against 1. Threshold dword is
/// 0.5f, not the source 0.3f.
/// Wrapper (a zero test of eax, branching when not equal) keeps FALSE when the
/// callee is true. One live <c>mode == Jet</c> bool is not
/// unique versus recency: an airborne walker is TRUE here
/// and FALSE there. CVM snapshot / 0.05f /
/// FollowWaypointWait / PlayAnimationWait stay unclaimed.
/// ChargeWeapon stays unclaimed. Live
/// <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailIScriptInJetMode
{
    /// <summary>
    /// Registry 125 handler — <c>0x005380f0</c>.
    /// </summary>
    public const int HandlerAddress = 0x005380f0;

    /// <summary>
    /// <c>0x00408120</c> — walker state 2 and last ground
    /// contact inside 0.5 s.
    /// </summary>
    public const int RecentlyGroundedWalkerAddress = 0x00408120;

    /// <summary>
    /// <c>THING_TYPE_BATTLE_ENGINE</c> — the
    /// bit test of field +0x34 of ecx against 8.
    /// Already the released Battle Engine mask bit.
    /// </summary>
    public const uint BattleEngineTypeBit = Level100ReleasedThingTypeMasks.BattleEngine;

    /// <summary>
    /// The type gate then the already-pinned recency
    /// negation. Mutation: skip the type test so a non-BE
    /// jet becomes TRUE.
    /// </summary>
    public static bool Evaluate(
        uint thingTypeMask,
        VehicleMode mode,
        VehicleTransition transition,
        int ticksSinceGroundContact)
    {
        if ((thingTypeMask & BattleEngineTypeBit) == 0)
        {
            return false;
        }

        return Level100MissionTiming.JetModeState(
            mode,
            transition,
            ticksSinceGroundContact) == Level100MissionJetModeState.InJetMode;
    }
}
