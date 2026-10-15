// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The <c>CCareer::Update</c> Won arm that replaces career
/// <c>mSlots</c> with the 32 words FillOut copied into
/// <c>END_LEVEL_DATA.mSlots</c>.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:379-392</c>
/// and <c>EndLevelData.h:32</c>. Retail identity: <c>0x0041BD00</c>
/// <c>CCareer::Update</c> in the pristine <c>74154bfa…</c> image
/// (2,506,752 bytes). File offset = VA − 0x400000.
/// </para>
/// <para>
/// <b>Lost never copies.</b> <c>0x0041BD00</c> is
/// <c>mov eax, [0x00672E1C]</c> — <c>END_LEVEL_DATA.mFinalState</c> —
/// then <c>cmp eax, 5</c> / <c>jne 0x0041BDD9</c> at
/// <c>0x0041BD06</c> / <c>0x0041BD0C</c>. 5 is
/// <c>GAME_STATE_LEVEL_WON</c>; Lost is 4. The 32-dword store sits
/// after that gate, so a Lost update leaves career slots alone.
/// </para>
/// <para>
/// <b>The store is assignment, not OR.</b> Re-derived this session:
/// <c>0x0041BD27</c> <c>8d 86 08 24 00 00</c> =
/// <c>lea eax, [esi+0x2408]</c> (career <c>mSlots</c>),
/// <c>0x0041BD2D</c> <c>b9 44 2e 67 00</c> =
/// <c>mov ecx, 0x00672E44</c> (<c>END_LEVEL_DATA.mSlots</c>),
/// <c>0x0041BD37</c> <c>ba 20 00 00 00</c> = <c>mov edx, 32</c>,
/// then <c>8b 3c 01 / 89 38 / 83 c0 04 / 4a / 75 f5</c>.
/// Each word is replaced. A leftover bit from an earlier session
/// does not survive a win.
/// </para>
/// <para>
/// <b>Level 100 writes four addressable bits.</b>
/// <c>SLOT_TUTORIAL_1..4</c> are 63..66
/// (<c>onsldef.msl</c>; <c>Level100Mission</c> <c>SetSlotSave</c>).
/// Words 8..31 are unreachable through <c>SetSlot</c>'s 256-bit
/// guard and are still copied. Do not invent other slots.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>CGame::SetSlot</c>'s
/// any-nonzero setter versus <c>CCareer::SetSlot</c>'s literal-1
/// setter — this owner copies words, it does not call SetSlot.
/// The mid-mission <c>IScript::SetSlotSave</c> persist lives on
/// <see cref="RetailSetSlotSave"/>. FillOut's first-play snapshot
/// bits live on
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/>.
/// </para>
/// </remarks>
public static class RetailCareerSlotHandoff
{
    /// <summary><c>SLOT_TUTORIAL_1</c> — introduction.</summary>
    public const int TutorialIntroductionSlot = 63;

    /// <summary><c>SLOT_TUTORIAL_2</c> — Pulse Cannon.</summary>
    public const int TutorialPulseCannonSlot = 64;

    /// <summary><c>SLOT_TUTORIAL_3</c> — Vulcan Cannon.</summary>
    public const int TutorialVulcanCannonSlot = 65;

    /// <summary><c>SLOT_TUTORIAL_4</c> — status bars.</summary>
    public const int TutorialStatusBarsSlot = 66;

    /// <summary>
    /// The loop counter at <c>0x0041BD37</c> — <c>mov edx, 0x20</c>.
    /// </summary>
    public const int SlotWordCount = 32;

    /// <summary>
    /// <c>END_LEVEL_DATA.mSlots</c> — the literal at <c>0x0041BD2D</c>.
    /// </summary>
    public const uint EndLevelSlotsAddress = 0x00672E44u;

    /// <summary>
    /// Career <c>mSlots</c> — the displacement at <c>0x0041BD27</c>.
    /// </summary>
    public const int CareerSlotsOffset = 0x2408;

    /// <summary>
    /// Whether <c>CCareer::Update</c> reaches the 32-dword copy.
    /// Only <c>GAME_STATE_LEVEL_WON</c> (5) does.
    /// </summary>
    public static bool ShouldOverwriteFromEndLevel(int finalState) =>
        finalState == RetailCareerReCalcLinks.GameStateLevelWon;

    /// <summary>
    /// The 32-dword assignment at <c>0x0041BD27</c>. Each career word
    /// is replaced. Callers that should not copy (Lost) must not call
    /// this — they take the <c>jne</c> at <c>0x0041BD0C</c> instead.
    /// </summary>
    public static void OverwriteFromEndLevel(
        RetailCareerSlots careerSlots,
        IReadOnlyList<int> endLevelSlotWords)
    {
        if (careerSlots is null)
        {
            throw new ArgumentNullException(nameof(careerSlots));
        }

        careerSlots.CopyWords(endLevelSlotWords);
    }
}
