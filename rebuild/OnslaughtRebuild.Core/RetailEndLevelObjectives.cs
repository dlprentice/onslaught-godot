// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The outcome of one <c>IsAllSecondaryObjectivesComplete</c> call.
/// </summary>
/// <param name="Result">
/// The <c>BOOL</c> retail returns — a normalised <c>1</c> or <c>0</c>.
/// </param>
/// <param name="AnyObjectiveSet">
/// <c>is_set</c>. False is the arm that logs and forces <c>Result</c> false; a
/// caller cannot tell the two falses apart from the return value alone, and
/// retail's own callers cannot either.
/// </param>
public readonly record struct RetailSecondaryObjectiveVerdict(
    bool Result,
    bool AnyObjectiveSet);

/// <summary>
/// <c>CEndLevelData::IsAllSecondaryObjectivesComplete</c> — the released
/// debriefing predicate, and the <c>CEndLevelData</c> layout it measures.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/EndLevelData.cpp:21-49</c> and
/// <c>EndLevelData.h:14-32</c>. Retail identity: <c>0x004496E0</c> in the
/// pristine <c>74154bfa…</c> image, file offset = VA - 0x400000.
/// <c>END_LEVEL_DATA</c> itself is at <c>0x006728F8</c>, derived from
/// <c>mWorldFinished</c> at <c>0x00672E18</c> and <c>mThingsKilled</c> at
/// <c>0x00672E30</c> — the two globals
/// <see cref="RetailCareerCounters.UpdateThingsKilled"/> already measures.
/// </para>
/// <para>
/// <b>Source and retail agree on every branch.</b> The loop is
/// <c>mov esi, 0xA</c> / <c>add edx, 8</c> / <c>dec esi</c> / <c>jne</c> over
/// <c>this + 0x4D0</c>, and the body is
/// <c>cmp ecx, 1 / je / cmp ecx, 2 / jne next</c> then
/// <c>cmp ecx, 2 / mov edi, 1 / jne next / xor eax, eax</c>. That is
/// <c>EndLevelData.cpp:27-38</c> read literally, with one nicety: the
/// <c>mov edi, 1</c> sits <i>between</i> the second <c>cmp</c> and its
/// <c>jne</c>, because <c>mov</c> does not disturb the flags. There are no
/// floating-point comparisons anywhere in this body, so there is no unordered
/// behaviour to record.
/// </para>
/// <para>
/// <b>The two status values are read off the image, not off a header.</b>
/// <c>MissionObjective.h</c> is not one of the files in the partial GPL release,
/// so <c>MOS_COMPLETE</c> and <c>MOS_FAILED</c> arrive only as
/// <c>cmp ecx, 1</c> and <c>cmp ecx, 2</c> at <c>0x004496F6</c> and
/// <c>0x004496FB</c>. Any other status — including <c>0</c> — is neither
/// "set" nor a failure, so it is invisible to both halves of the predicate.
/// </para>
/// <para>
/// <b>"No objectives" is reported as failure, and it is indistinguishable from
/// a real failure.</b> <c>0x00449712</c> is <c>test edi, edi / jne</c> around a
/// <c>LOG.AddMessage</c> of the string at <c>0x00628AD8</c> —
/// <c>"ERROR: No secondary objectives in call to
/// 'IsAllSecondaryObjectivesComplete'"</c> — and then <c>xor eax, eax</c>. The
/// author expected this to be unreachable and shipped a diagnostic rather than a
/// guard. <see cref="RetailSecondaryObjectiveVerdict.AnyObjectiveSet"/> exposes
/// the distinction that retail only writes to a log.
/// </para>
/// <para>
/// <b>The <c>CEndLevelData</c> prefix is fully determined by two
/// displacements.</b> <c>mSecondaryObjectives</c> is at <c>+0x4D0</c> and
/// <c>mThingsKilled</c> at <c>+0x538</c>. <c>mBaseThingsLeft</c> is
/// <c>288 * 4 = 0x480</c> bytes (<c>EndLevelData.h:19</c>,
/// <c>BASE_THINGS_EXISTS_SIZE</c>), which leaves <c>0x480..0x4D0</c> for
/// <c>mPrimaryObjectives</c> — <c>0x50</c> bytes. The secondary array is ten
/// entries of stride eight, so <c>sizeof(CMissionObjective)</c> is <b>8</b> and
/// both objective arrays hold <b>10</b>. That closes the arithmetic: the six
/// scalars <c>EndLevelData.h:22-27</c> declares between
/// <c>mSecondaryObjectives</c> and <c>mThingsKilled</c> occupy
/// <c>0x538 - 0x520 = 0x18</c>, and <c>0x520</c> is exactly
/// <c>0x4D0 + 10 * 8</c>. <c>MAX_PRIMARY_OBJECTIVES</c> and
/// <c>MAX_SECONDARY_OBJECTIVES</c> are therefore measured, not assumed.
/// </para>
/// <para>
/// <b>Not established here.</b> What the second dword of each
/// <c>CMissionObjective</c> holds. <c>GetStatus()</c> is the first dword — the
/// loop only ever reads <c>[edx]</c> — and nothing in this body touches
/// <c>[edx + 4]</c>.
/// </para>
/// </remarks>
public static class RetailEndLevelObjectives
{
    /// <summary><c>MOS_COMPLETE</c> — <c>cmp ecx, 1</c> at <c>0x004496F6</c>.</summary>
    public const int StatusComplete = 1;

    /// <summary><c>MOS_FAILED</c> — <c>cmp ecx, 2</c> at <c>0x004496FB</c>.</summary>
    public const int StatusFailed = 2;

    /// <summary><c>MAX_SECONDARY_OBJECTIVES</c> — <c>mov esi, 0xA</c> at <c>0x004496EF</c>.</summary>
    public const int SecondaryObjectiveCount = 10;

    /// <summary><c>MAX_PRIMARY_OBJECTIVES</c> — <c>(0x4D0 - 0x480) / 8</c>.</summary>
    public const int PrimaryObjectiveCount = 10;

    /// <summary><c>sizeof(CMissionObjective)</c> — the <c>add edx, 8</c> stride.</summary>
    public const int MissionObjectiveStride = 8;

    /// <summary><c>mSecondaryObjectives</c> — <c>lea edx, [ecx + 0x4D0]</c> at <c>0x004496E9</c>.</summary>
    public const int SecondaryObjectivesOffset = 0x4D0;

    /// <summary><c>mThingsKilled</c> — <c>0x00672E30 - 0x006728F8</c>.</summary>
    public const int ThingsKilledOffset = 0x538;

    /// <summary><c>END_LEVEL_DATA</c> — <c>0x00672E18 - 0x520</c>.</summary>
    public const uint EndLevelDataAddress = 0x006728F8u;

    /// <summary>
    /// <c>CEndLevelData::IsAllSecondaryObjectivesComplete</c> —
    /// <c>EndLevelData.cpp:21-49</c>, <c>0x004496E0</c>.
    /// </summary>
    /// <param name="secondaryObjectiveStatuses">
    /// The ten <c>GetStatus()</c> words. Retail reads all ten unconditionally,
    /// so exactly ten are required.
    /// </param>
    public static RetailSecondaryObjectiveVerdict IsAllSecondaryObjectivesComplete(
        IReadOnlyList<int> secondaryObjectiveStatuses)
    {
        if (secondaryObjectiveStatuses is null)
        {
            throw new ArgumentNullException(nameof(secondaryObjectiveStatuses));
        }

        if (secondaryObjectiveStatuses.Count != SecondaryObjectiveCount)
        {
            throw new ArgumentException(
                $"The loop counter at 0x004496EF is {SecondaryObjectiveCount}; " +
                "retail reads exactly that many entries with no early exit.",
                nameof(secondaryObjectiveStatuses));
        }

        bool result = true;
        bool anySet = false;

        for (int index = 0; index < SecondaryObjectiveCount; index++)
        {
            int status = secondaryObjectiveStatuses[index];

            if (status != StatusComplete && status != StatusFailed)
            {
                continue;
            }

            anySet = true;

            if (status == StatusFailed)
            {
                result = false;
            }
        }

        // test edi, edi / jne: no objective in either state logs and returns 0.
        return anySet
            ? new RetailSecondaryObjectiveVerdict(result, true)
            : new RetailSecondaryObjectiveVerdict(false, false);
    }
}
