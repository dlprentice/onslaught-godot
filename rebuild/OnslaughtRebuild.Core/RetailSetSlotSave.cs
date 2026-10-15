// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The Level 100 half of <c>IScript::SetSlotSave</c> — the immediate
/// <c>CCareer::SetSlot</c> persist. Live <c>GAME.mSlots</c> and FillOut's
/// copy of those words stay on the already-pinned
/// <see cref="RetailFillOutEndLevelData.FirstPlayTutorialSlotWords"/>.
/// </summary>
/// <remarks>
/// <para>
/// Retail identity: <c>IScript__SetSlotSave</c> at <c>0x00533900</c>
/// (<c>RET 0xc</c>, 30 instructions). After <c>CGame::SetSlot</c>
/// at <c>0x0046d3a0</c> it calls <c>CCareer::SetSlot</c> at
/// <c>0x004214e0</c>. Function note
/// <c>reverse-engineering/binary-analysis/functions/Script.cpp/CGame__SetSlot.md</c>
/// and the Wave579 plate name that dual write. First-play Level 100
/// <c>SetSlotSave(SLOT_TUTORIAL_1..4, TRUE)</c> therefore writes
/// career <c>mSlots</c> during the tutorial, before
/// <c>DeclareLevelWon</c> and before FillOut / <c>ApplyUpdate</c>.
/// World 100 stays incomplete until the already-pinned handoff.
/// </para>
/// <para>
/// Isolated FrontEndHandoff overwrite names the 32-dword assignment
/// after <c>TryApply</c>. Isolated Lost skip names that assignment
/// not running. Neither names the mid-mission persist: a first-play
/// Won that is still on <c>SuccessCountdown</c> already holds
/// bits 63..66 even though <c>Complete</c> is still 0.
/// </para>
/// <para>
/// <b>Do not invent live <c>GAME.mSlots</c>.</b> <c>CGame::SetSlot</c>
/// (sets the bit only when the value is 1 and clears it otherwise: <c>cmp
/// [esp+0xc],1; jne</c> at <c>0x0046d3c5</c>, as <c>CCareer::SetSlot</c> at
/// <c>0x00421505</c>) and FillOut's copy of those 32 words stay on
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>. This owner
/// only persists through the already-pinned
/// <see cref="RetailCareerSlots.SetSlot"/> literal-1 store.
/// <c>SET_GOODIE_INSTRUCTION</c>, <c>mPendingExtraGoodies</c>,
/// secondaries, and ChargeWeapon stay unclaimed.
/// </para>
/// </remarks>
public static class RetailSetSlotSave
{
    /// <summary>
    /// The <c>CCareer::SetSlot</c> arm of <c>SetSlotSave</c>. First-play
    /// <c>TRUE</c> is literal 1. Mutation: skip this store so bits
    /// 63..66 stay 0 until <c>ApplyUpdate</c>.
    /// </summary>
    public static void PersistCareerSlot(RetailCareerSlots careerSlots, int slot, bool value)
    {
        ArgumentNullException.ThrowIfNull(careerSlots);
        careerSlots.SetSlot(slot, value ? 1 : 0);
    }
}
