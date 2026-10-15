// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>IScript::SetObjective</c> / <c>UnsetObjective</c> — the
/// <c>or/and byte ptr [esi+0x2c], imm</c> stores.
/// Isolated <see cref="Level100ActorSnapshot.IsObjective"/> and
/// <see cref="Level100MissionSnapshot.NavigationObjective"/> stay
/// the rebuild bool / string. Noticeboard Add/Remove stay
/// unclaimed.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__SetObjective</c> at
/// <c>0x00535ed0</c> through <c>ret 0xc</c> at <c>0x00535eda</c>
/// (13 bytes) on the official <c>74154bfa…</c> specimen. File
/// offset = VA − 0x400000. Re-derived this session from
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>
/// (2,506,752 bytes). Body is
/// <c>8b 49 10 6a 01 e8 96 da fb ff c2 0c 00</c>, SHA-256
/// <c>e1e368b83a8c664935143709b40f4ad2bf7c6217003492b5d64c2562a48f666b</c>.
/// Twin <c>IScript__UnsetObjective</c> at <c>0x00535ee0</c>
/// (13 B SHA-256
/// <c>0ec7dfff6ad0dba017b45b0a9840f6b587b899e88aaedb29d1d0eabfb842b35f</c>)
/// pushes 0 into the same callee. W007 names
/// <c>CThing__SetObjective</c> at <c>0x004f3970</c>. Source
/// <c>references/Onslaught/thing.cpp:269-287</c> /
/// <c>thing.h:48</c> is <c>TF_MARKED_OBJECTIVE = 32</c>.
/// </para>
/// <para>
/// <b>The callee ORs bit 0x20; Unset ANDs ~0x20.</b>
/// Full body 61 B SHA-256
/// <c>da733fdd7e7575a433875b1f7179c538834892d555e8d02db320a269353980b0</c>.
/// <c>0x004f398e</c> is <c>80 4e 2c 20</c> =
/// <c>or byte ptr [esi+0x2c], 0x20</c>.
/// <c>0x004f39a5</c> is <c>80 66 2c df</c> =
/// <c>and byte ptr [esi+0x2c], 0xdf</c>. FillOut already
/// names <c>+0x2c</c> as <c>mFlags</c> via
/// <c>test byte [eax+0x2c], 4</c> (TF_DYING). Isolated
/// <c>IsObjective</c> = true names the rebuild bool; skip
/// this OR still leaves that bool / the navigation name.
/// Mutation: replace so Mark(0x04) becomes 0x20, or Unmark
/// writes 0 so Unmark(0x24) becomes 0.
/// </para>
/// <para>
/// Noticeboard <c>CSPtrSet</c> Add/Remove at
/// <c>0x004e5a80</c> / <c>0x004e5bd0</c> stay unclaimed.
/// ChargeWeapon / ReadyToCharge / Charged-2 stay unclaimed.
/// Live <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </para>
/// </remarks>
public static class RetailSetObjective
{
    /// <summary>
    /// <c>CThing.mFlags</c> — <c>this+0x2c</c>. Already named
    /// by FillOut's TF_DYING test.
    /// </summary>
    public const int FlagsOffset = 0x2c;

    /// <summary>
    /// <c>TF_MARKED_OBJECTIVE</c> at <c>thing.h:48</c> /
    /// the <c>or …, 0x20</c> immediate at <c>0x004f398e</c>.
    /// </summary>
    public const int MarkedBit = 0x20;

    /// <summary>
    /// <c>TF_DYING</c> — already pinned by FillOut
    /// <c>test byte [eax+0x2c], 4</c>. Used only so Mark
    /// cannot be a replace-with-0x20.
    /// </summary>
    public const int DyingBit = 0x04;

    /// <summary>
    /// The <c>or [esi+0x2c], 0x20</c> store. Mutation:
    /// return <see cref="MarkedBit"/> so Mark(4) becomes 0x20.
    /// </summary>
    public static int Mark(int currentFlags) => currentFlags | MarkedBit;

    /// <summary>
    /// The <c>and [esi+0x2c], 0xdf</c> store. Mutation:
    /// return 0 so Unmark(0x24) becomes 0.
    /// </summary>
    public static int Unmark(int currentFlags) => currentFlags & ~MarkedBit;
}
