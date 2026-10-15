// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The post-Won seam from <see cref="Level100Mission"/> onto the already-pinned
/// FillOut snapshot and <c>CCareer::Update</c>.
/// </summary>
/// <remarks>
/// <para>
/// After <c>CGame::DeclareLevelWon</c> the 5.0 f countdown elapses,
/// <c>CGame::RestartLoopRunLevel</c> calls
/// <c>CGame::FillOutEndLevelData</c> (<c>0x0046D470</c>) and then
/// <c>CCareer::Update</c> (<c>0x0041BD00</c>). Mission
/// <see cref="Level100MissionTerminalState.FrontEndHandoffReady"/> is that
/// seam for Level 100. This type does not reimplement FillOut, ReCalcLinks,
/// or the ranking-clamp skip — it only applies
/// <see cref="RetailFillOutEndLevelData.ForLevel100Won"/> to a cold
/// training slice. First-play S still carries the already-pinned
/// <c>CountGoodies</c> delta into <c>new_goodie_count</c> /
/// <c>first_goodie</c> through this <see cref="TryApply"/> seam.
/// Leftover world-110 complete + ranking 0.0f (already pinned as
/// E) still opens <c>SET_GOODIE_NEW(14)</c> through the same
/// seam. Isolated leftover 14 names ApplyUpdate; Lost leftover
/// 14 names the Lost return. Leftover world-110 complete +
/// ranking 0.25f (already pinned as C) still opens
/// <c>SET_GOODIE_NEW(1)</c> through the same seam. Isolated
/// leftover C names ApplyUpdate; FrontEndHandoff leftover 14
/// names 14, not 1. Leftover of that same C seed still
/// opens <c>SET_GOODIE_NEW(79)</c> through the same seam.
/// Isolated leftover C 79 names ApplyUpdate; FrontEndHandoff
/// leftover C names 1, not 79. Isolated first-play closed
/// concept-art names ApplyUpdate and leaves 79 at
/// <c>GS_UNKNOWN</c>. First-play through this seam leaves
/// <c>GRADE(110) &gt;= C</c> closed: world 110 is incomplete so
/// the lookup is the already-pinned incomplete <c>'E'</c> and
/// goodie 1 stays <c>GS_UNKNOWN</c>. Isolated closed GRADE(110)
/// names ApplyUpdate and does not go through this seam.
/// First-play through this seam also leaves concept-art goodie
/// 79 at <c>GS_UNKNOWN</c>. Isolated closed concept-art names
/// ApplyUpdate and does not go through this seam.
/// First-play through this seam also leaves
/// <c>COMPLETE_LEVEL(110)</c> closed: world 110 is unlocked but
/// still incomplete, so goodie 14 stays <c>GS_UNKNOWN</c>.
/// Isolated closed COMPLETE_LEVEL(110) names ApplyUpdate and
/// does not go through this seam.
/// Leftover <c>mNumAttempts</c> on world 100 (7) and world 110
/// (11) also survives this seam: <c>CCareer::Update</c> never
/// writes <c>+0x38</c>. Isolated leftover 7 / 11 names
/// ApplyUpdate and does not go through this seam. Isolated
/// Blank ctor 0 and existing FrontEndHandoff ranking /
/// Complete / CareerInProgress / goodie tests do not name
/// <c>+0x38</c>.
/// Leftover <c>GS_OLD</c> on the five first-play S slots also
/// survives this seam: <c>SET_GOODIE_NEW</c> stores only when
/// <c>GOODIE_NOT_DONE</c> (<c>Career.cpp:564-566</c>). Isolated
/// leftover <c>GS_OLD</c> names ApplyUpdate and does not go
/// through this seam. Existing FrontEndHandoff S goodies start
/// <c>GS_UNKNOWN</c> and name them as New. Replay CountGoodies
/// names already-<c>GS_NEW</c>. Leftover
/// <c>GS_INSTRUCTIONS</c> on those five slots writes 2 through
/// this seam: <c>SET_GOODIE_NEW</c> stores when
/// <c>mState &lt;= GS_INSTRUCTIONS</c>. Isolated leftover
/// <c>GS_INSTRUCTIONS</c> names ApplyUpdate and does not go
/// through this seam.
/// </para>
/// <para>
/// <b>SetSlotSave already persisted the four tutorial bits.</b>
/// Isolated <see cref="RetailSetSlotSave.PersistCareerSlot"/> names
/// that mid-mission store. This seam still overwrites the 32 words
/// from FillOut. Live <c>GAME.mSlots</c> stay unclaimed.
/// </para>
/// <para>
/// <b>Do not invent secondaries.</b> Level 100 ships four primaries and
/// none. The snapshot's ten status words stay unset.
/// </para>
/// <para>
/// <b>Lost does not take this path.</b>
/// <c>END_LEVEL_DATA.mFinalState != GAME_STATE_LEVEL_WON</c> returns
/// from <c>Update</c> before the graph moves
/// (<c>Career.cpp:382-385</c>).
/// </para>
/// <para>
/// <b>SuccessCountdown does not take this path either.</b>
/// <c>RestartLoopRunLevel</c> calls FillOut only after the main
/// loop quits (<c>game.cpp:1552</c>), and that quit waits for the
/// already-pinned 5.0 f Won store (<c>game.cpp:1997-2004</c>).
/// <c>CFrontEnd::Init</c> then calls <c>CAREER.Update</c>
/// (<c>FrontEnd.cpp:67</c>).
/// </para>
/// </remarks>
public static class Level100WonCareerHandoff
{
    /// <summary>
    /// Apply the FillOut Won snapshot to <paramref name="career"/> when the
    /// mission has reached the frontend handoff after a win; returns the
    /// applied snapshot, or null when the mission is not there. The frontend's
    /// Won re-entry is the one caller in the product
    /// (<c>RetailFrontendSession.TryAcceptWonHandoff</c>, as
    /// <c>CFrontEnd::Init</c> calls <c>CCareer::Update</c>), once per win.
    /// </summary>
    /// <param name="baseThingsLeft">
    /// FillOut's base-world survivor list from the level's end state; a
    /// first play that loses no row when omitted.
    /// </param>
    public static RetailEndLevelSnapshot? TryApply(
        RetailCareerCampaign career,
        Level100MissionOutcome outcome,
        Level100MissionTerminalState terminalState,
        IReadOnlyList<int>? baseThingsLeft = null)
    {
        ArgumentNullException.ThrowIfNull(career);
        if (outcome != Level100MissionOutcome.Won ||
            terminalState != Level100MissionTerminalState.FrontEndHandoffReady)
        {
            return null;
        }

        RetailEndLevelSnapshot snapshot = RetailFillOutEndLevelData.ForLevel100Won(baseThingsLeft: baseThingsLeft);
        career.ApplyUpdate(snapshot);
        return snapshot;
    }
}
