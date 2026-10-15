// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

/// <summary>
/// The three labels selected by <c>CFEPDebriefing::Render</c> from
/// <c>END_LEVEL_DATA.mFinalState</c>.
/// </summary>
public enum RetailDebriefingMissionStatus
{
    Aborted,
    Defeat,
    Victory,
}

/// <summary>
/// The visible state of one ten-slot objective group on the released
/// debriefing page.
/// </summary>
public enum RetailDebriefingObjectiveSummary
{
    Hidden,
    Complete,
    Incomplete,
}

/// <summary>
/// Settled, presentation-safe projection of the released PC debriefing page.
/// </summary>
/// <remarks>
/// <para>
/// Owner: pristine <c>74154bfa…</c> <c>CFEPDebriefing::Render</c>,
/// <c>0x00456DD0..0x00457CED</c>, and
/// <c>CFEPDebriefing::TransitionNotification</c> at <c>0x00457CF0</c>.
/// The render body reads world/final-state/ranking and the two ten-entry
/// objective arrays. It does not read or draw <c>mThingsKilled</c>.
/// </para>
/// <para>
/// TransitionNotification reads and clears the two career goodie latches after
/// <c>CCareer::Update</c>. They are retained here because Process consumes them
/// for the transient goodie effects/message; they are not a visible item list.
/// </para>
/// </remarks>
public sealed record RetailDebriefingProjection(
    int WorldFinished,
    RetailDebriefingMissionStatus MissionStatus,
    RetailDebriefingObjectiveSummary PrimaryObjectives,
    RetailDebriefingObjectiveSummary SecondaryObjectives,
    byte? GradeByte,
    int NewGoodieCount,
    bool FirstGoodie)
{
    /// <summary>
    /// Projects the exact settled branches used by retail's Render body.
    /// </summary>
    public static RetailDebriefingProjection From(
        RetailEndLevelSnapshot snapshot,
        int newGoodieCount,
        int firstGoodieFlag)
    {
        RetailDebriefingMissionStatus missionStatus = snapshot.FinalState switch
        {
            RetailCareerReCalcLinks.GameStateLevelLost =>
                RetailDebriefingMissionStatus.Defeat,
            RetailCareerReCalcLinks.GameStateLevelWon =>
                RetailDebriefingMissionStatus.Victory,
            _ => RetailDebriefingMissionStatus.Aborted,
        };

        RetailDebriefingObjectiveSummary primary =
            SummarizeObjectives(snapshot.PrimaryStatuses, nameof(snapshot.PrimaryStatuses));
        RetailDebriefingObjectiveSummary secondary =
            SummarizeObjectives(snapshot.SecondaryStatuses, nameof(snapshot.SecondaryStatuses));

        // Render 0x00457191: world 500 + Won forces the primary row visible
        // and Complete, independently of the ten stored status words.
        if (snapshot.WorldFinished == 500 &&
            snapshot.FinalState == RetailCareerReCalcLinks.GameStateLevelWon)
        {
            primary = RetailDebriefingObjectiveSummary.Complete;
        }

        return new RetailDebriefingProjection(
            snapshot.WorldFinished,
            missionStatus,
            primary,
            secondary,
            snapshot.FinalState == RetailCareerReCalcLinks.GameStateLevelWon
                ? RetailCareerGrade.GradeByteFromRanking(snapshot.Ranking)
                : null,
            // TransitionNotification caps only the upper side. Values below 1
            // drive its no-new-goodie timing arm and are not normalised.
            Math.Min(newGoodieCount, 99),
            firstGoodieFlag != 0);
    }

    private static RetailDebriefingObjectiveSummary SummarizeObjectives(
        IReadOnlyList<int> statuses,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(statuses, parameterName);
        if (statuses.Count != RetailEndLevelObjectives.PrimaryObjectiveCount)
        {
            throw new ArgumentException(
                "CFEPDebriefing::Render reads exactly ten objective entries " +
                "at an eight-byte stride.",
                parameterName);
        }

        RetailDebriefingObjectiveSummary summary =
            RetailDebriefingObjectiveSummary.Hidden;
        foreach (int status in statuses)
        {
            if (status == 0)
            {
                continue;
            }

            if (status == RetailEndLevelObjectives.StatusComplete)
            {
                if (summary == RetailDebriefingObjectiveSummary.Hidden)
                {
                    summary = RetailDebriefingObjectiveSummary.Complete;
                }
            }
            else
            {
                // Any non-zero status other than 1 permanently wins over a
                // Complete encountered before or after it.
                summary = RetailDebriefingObjectiveSummary.Incomplete;
            }
        }

        return summary;
    }
}
