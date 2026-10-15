// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CGame::GetNumPrimaryObjectives</c> and
/// <c>GetNumSecondaryObjectives</c> — the scans FillOut uses to
/// decide whether the secondary ranking clamp runs, and the four
/// <c>MOS_COMPLETE</c> primary statuses a Level 100 win copies.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/game.cpp:4056-4078</c>
/// and <c>:965</c>. Retail identities: <c>0x00472670</c> and
/// <c>0x00472690</c> in the pristine <c>74154bfa…</c> image
/// (2,506,752 bytes). File offset = VA − 0x400000. Re-derived this
/// session.
/// </para>
/// <para>
/// <b>The scan counts every non-zero status.</b>
/// <c>0x00472670</c> is <c>xor eax, eax</c> /
/// <c>add ecx, 0x4C</c> / <c>mov edx, 0xA</c> /
/// <c>cmp dword ptr [ecx], 0</c> / <c>je</c> / <c>inc eax</c> /
/// <c>add ecx, 8</c> / <c>dec edx</c> / <c>jnz</c> / <c>ret</c>.
/// <c>0x00472690</c> is the same loop after
/// <c>add ecx, 0x9C</c>. <c>MOS_NOT_DEFINED</c> is the zero
/// sentinel (W004 A11). <c>MOS_FAILED</c> (2) still increments.
/// A rebuild that counted only <c>MOS_COMPLETE</c> would drop a
/// failed row.
/// </para>
/// <para>
/// <b>Level 100 copies four primaries and no secondaries.</b>
/// The shipped script defines objectives 1-4; after a win those
/// four statuses are <c>MOS_COMPLETE</c> (1), and the other six
/// primary slots plus all ten secondaries stay 0. That is not the
/// rebuild mission enum's <c>Level100PrimaryObjectiveStatus.Complete
/// = 2</c>. Do not invent secondary content. FillOut's
/// <c>if (GetNumSecondaryObjectives())</c> therefore stays false
/// and the 0.4 / 0.6 clamp is skipped — already pinned.
/// </para>
/// <para>
/// <b>The mission-layer enum is inverted.</b>
/// <c>IScript::PrimaryObjectiveComplete</c> at <c>0x005343e0</c>
/// writes state 1; <c>IScript::PrimaryObjectiveFailed</c> at
/// <c>0x00534440</c> writes state 2. Both store the script
/// string id at <c>[eax+4]</c> (<c>0x005343fe</c> /
/// <c>0x0053445e</c>). Rebuild
/// <c>Level100PrimaryObjectiveStatus</c> is Failed=1 / Complete=2.
/// <see cref="FromLevel100MissionStatus"/> is the status mapping.
/// <see cref="FromLevel100MissionPrimaryTextIds"/> is the text
/// dword. Score-time ranking stays unclaimed. Career does not consume
/// the primary table.
/// </para>
/// </remarks>
public static class RetailGameObjectiveCount
{
    /// <summary><c>MOS_NOT_DEFINED</c> — the <c>cmp [ecx], 0</c> sentinel.</summary>
    public const int StatusNotDefined = 0;

    /// <summary><c>MOS_COMPLETE</c> — <c>cmp ecx, 1</c> at <c>0x004496F6</c>.</summary>
    public const int StatusComplete = RetailEndLevelObjectives.StatusComplete;

    /// <summary><c>MOS_FAILED</c> — <c>cmp ecx, 2</c> at <c>0x004496FB</c>.</summary>
    public const int StatusFailed = RetailEndLevelObjectives.StatusFailed;

    /// <summary>The <c>mov edx, 0xA</c> loop counter at <c>0x00472675</c>.</summary>
    public const int ObjectiveSlotCount = RetailEndLevelObjectives.PrimaryObjectiveCount;

    /// <summary>Shipped Level 100 primary count — four defined rows.</summary>
    public const int Level100PrimaryCount = 4;

    /// <summary>Shipped Level 100 secondary count — there are none.</summary>
    public const int Level100SecondaryCount = 0;

    /// <summary>
    /// <c>_100_OBJECTIVE_1</c> — <c>msl-scripting.md</c> and the
    /// hash-pinned Level 100 <c>init()</c> <c>PrimaryObjectiveFailed(1, …)</c>.
    /// Stored at <c>[eax+4]</c> (<c>0x0053445e</c>).
    /// </summary>
    public const int Level100InitObjectiveText1 = 110325434;

    /// <summary>
    /// Hash-pinned Level 100 <c>init()</c> text for objective 2.
    /// Same <c>[eax+4]</c> store. Do not invent secondaries.
    /// </summary>
    public const int Level100InitObjectiveText2 = 111145813;

    /// <summary>
    /// Hash-pinned Level 100 <c>init()</c> text for objective 3.
    /// Same <c>[eax+4]</c> store. Do not invent secondaries.
    /// </summary>
    public const int Level100InitObjectiveText3 = 111966192;

    /// <summary>
    /// Hash-pinned Level 100 <c>init()</c> text for objective 4.
    /// Same <c>[eax+4]</c> store. Do not invent secondaries.
    /// </summary>
    public const int Level100InitObjectiveText4 = 112786571;

    /// <summary>
    /// <c>CGame::GetNumPrimaryObjectives</c> — <c>0x00472670</c>.
    /// </summary>
    public static int GetNumPrimaryObjectives(IReadOnlyList<int> statuses) =>
        CountDefined(statuses);

    /// <summary>
    /// <c>CGame::GetNumSecondaryObjectives</c> — <c>0x00472690</c>.
    /// </summary>
    public static int GetNumSecondaryObjectives(IReadOnlyList<int> statuses) =>
        CountDefined(statuses);

    /// <summary>
    /// The mission-layer enum is inverted from retail MOS.
    /// <c>IScript::PrimaryObjectiveComplete</c> at <c>0x005343e0</c>
    /// writes state 1; <c>IScript::PrimaryObjectiveFailed</c> at
    /// <c>0x00534440</c> writes state 2 (Wave580 plate). Rebuild
    /// <see cref="Level100PrimaryObjectiveStatus.Failed"/> is 1 and
    /// <see cref="Level100PrimaryObjectiveStatus.Complete"/> is 2.
    /// Identity-cast is the mutation this mapping kills.
    /// </summary>
    public static int FromLevel100MissionStatus(Level100PrimaryObjectiveStatus status) =>
        status switch
        {
            Level100PrimaryObjectiveStatus.Uninitialized => StatusNotDefined,
            Level100PrimaryObjectiveStatus.Failed => StatusFailed,
            Level100PrimaryObjectiveStatus.Complete => StatusComplete,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

    /// <summary>
    /// Ten FillOut primary words from a live Level 100 snapshot.
    /// Init writes four failed rows; a win later overwrites those
    /// with complete. The other six slots stay the zero sentinel.
    /// </summary>
    public static int[] FromLevel100MissionPrimaries(
        IReadOnlyList<Level100PrimaryObjectiveSnapshot> primaries)
    {
        ArgumentNullException.ThrowIfNull(primaries);

        int[] statuses = new int[ObjectiveSlotCount];
        foreach (Level100PrimaryObjectiveSnapshot primary in primaries)
        {
            if (primary.Objective < 1 || primary.Objective > Level100PrimaryCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primaries),
                    $"Level 100 only authors objectives 1..{Level100PrimaryCount}.");
            }

            statuses[primary.Objective - 1] = FromLevel100MissionStatus(primary.Status);
        }

        return statuses;
    }

    /// <summary>
    /// Ten <c>CMissionObjective+4</c> text words from a live Level 100
    /// snapshot. <c>IScript::PrimaryObjectiveFailed</c> at
    /// <c>0x0053445e</c> is <c>mov [eax+4], edi</c>. Init writes
    /// four authored text ids; the other six slots stay 0. Isolated
    /// status mapping does not name this dword. Mutation: leave
    /// every word at 0.
    /// </summary>
    public static int[] FromLevel100MissionPrimaryTextIds(
        IReadOnlyList<Level100PrimaryObjectiveSnapshot> primaries)
    {
        ArgumentNullException.ThrowIfNull(primaries);

        int[] texts = new int[ObjectiveSlotCount];
        foreach (Level100PrimaryObjectiveSnapshot primary in primaries)
        {
            if (primary.Objective < 1 || primary.Objective > Level100PrimaryCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(primaries),
                    $"Level 100 only authors objectives 1..{Level100PrimaryCount}.");
            }

            texts[primary.Objective - 1] = primary.TextId;
        }

        return texts;
    }

    /// <summary>
    /// The ten primary <c>GetStatus()</c> words FillOut copies after
    /// a Level 100 win.
    /// </summary>
    public static int[] Level100WonPrimaryStatuses()
    {
        int[] statuses = new int[ObjectiveSlotCount];
        for (int index = 0; index < Level100PrimaryCount; index++)
        {
            statuses[index] = StatusComplete;
        }

        return statuses;
    }

    /// <summary>
    /// The ten unset secondary words — Level 100 authors none.
    /// </summary>
    public static int[] Level100WonSecondaryStatuses() =>
        RetailFillOutEndLevelData.UnsetSecondaryStatuses();

    private static int CountDefined(IReadOnlyList<int> statuses)
    {
        if (statuses is null)
        {
            throw new ArgumentNullException(nameof(statuses));
        }

        if (statuses.Count != ObjectiveSlotCount)
        {
            throw new ArgumentException(
                $"The loop counter at 0x00472675 is {ObjectiveSlotCount}; " +
                "retail reads exactly that many entries with no early exit.",
                nameof(statuses));
        }

        int found = 0;
        for (int index = 0; index < ObjectiveSlotCount; index++)
        {
            if (statuses[index] != StatusNotDefined)
            {
                found++;
            }
        }

        return found;
    }
}
