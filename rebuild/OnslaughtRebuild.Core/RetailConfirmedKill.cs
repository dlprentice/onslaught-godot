// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CBattleEngine::ConfirmedKill</c> then the five-dword incrementer
/// at <c>0x004d30d0</c>. First-play Level 100 totals stay unclaimed —
/// a Won that never takes this path still snapshots five zeros.
/// </summary>
/// <remarks>
/// <para>
/// Independently re-read official specimen
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// Caller <c>0x0040a560</c>: <c>cmp [thing+0x138],1</c> at
/// <c>0x0040a564</c> / <c>jne 0x0040a57d</c>, then
/// <c>0x0040a56d</c> <c>mov ecx,[ecx+0x574]</c> /
/// <c>0x0040a573</c> <c>test ecx,ecx</c> / <c>je 0x0040a57d</c>,
/// then <c>E8</c> at <c>0x0040a578</c>. That is a null pointer,
/// not source <c>ToRead()</c> dying-flag.
/// <c>tools/call_xref_scan.py</c> on
/// <c>0x004d30d0</c> is that one inbound call.
/// </para>
/// <para>
/// Incrementer <c>0x004d30d0</c> tests <c>[thing+0x34]</c> bits
/// <c>0x400</c> / <c>0x20000</c> / <c>0x40000</c> / <c>0x4000</c> /
/// <c>0x800</c> into <c>player+8/+c/+10/+14/+18</c>. Source
/// <c>Player.cpp:273-277</c> writes <c>IsA(THING_TYPE_*)</c>; those
/// type names are not claimed here. Career still <c>je</c> world 100.
/// </para>
/// </remarks>
public static class RetailConfirmedKill
{
    /// <summary>Muspell allegiance the caller compares at <c>thing+0x138</c>.</summary>
    public const int EnemyAllegiance = 1;

    /// <summary><c>[thing+0x34]</c> bit that increments <c>player+8</c>.</summary>
    public const int Slot0Flag = 0x400;

    /// <summary><c>[thing+0x34]</c> bit that increments <c>player+0xc</c>.</summary>
    public const int Slot1Flag = 0x20000;

    /// <summary><c>[thing+0x34]</c> bit that increments <c>player+0x10</c>.</summary>
    public const int Slot2Flag = 0x40000;

    /// <summary><c>[thing+0x34]</c> bit that increments <c>player+0x14</c>.</summary>
    public const int Slot3Flag = 0x4000;

    /// <summary><c>[thing+0x34]</c> bit that increments <c>player+0x18</c>.</summary>
    public const int Slot4Flag = 0x800;

    /// <summary>
    /// Apply the caller gate then the five incrementer bits. Returns a
    /// new five-word vector. Does not mutate <paramref name="current"/>.
    /// </summary>
    public static int[] Apply(
        IReadOnlyList<int> current,
        int thingFlags,
        int thingAllegiance,
        bool playerReaderNonNull = true)
    {
        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        if (current.Count != RetailCareerCounters.KilledTypeCount)
        {
            throw new ArgumentException(
                $"FillOut copies {RetailCareerCounters.KilledTypeCount} kill dwords.",
                nameof(current));
        }

        var next = new int[RetailCareerCounters.KilledTypeCount];
        for (int index = 0; index < next.Length; index++)
        {
            next[index] = current[index];
        }

        if (thingAllegiance != EnemyAllegiance)
        {
            return next;
        }

        if (!playerReaderNonNull)
        {
            return next;
        }

        if ((thingFlags & Slot0Flag) != 0)
        {
            next[0]++;
        }

        if ((thingFlags & Slot1Flag) != 0)
        {
            next[1]++;
        }

        if ((thingFlags & Slot2Flag) != 0)
        {
            next[2]++;
        }

        if ((thingFlags & Slot3Flag) != 0)
        {
            next[3]++;
        }

        if ((thingFlags & Slot4Flag) != 0)
        {
            next[4]++;
        }

        return next;
    }
}
