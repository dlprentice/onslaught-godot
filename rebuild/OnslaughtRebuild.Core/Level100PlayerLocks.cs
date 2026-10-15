// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>One lock entry, as <c>CLockInfo</c> stores it.</summary>
public sealed record Level100PlayerLockSnapshot(
    Level100ActorId Unit,
    uint StartBits,
    uint FinishBits,
    bool DirectLock);

/// <summary>The Battle Engine's two lock sets and their cursors.</summary>
public sealed record Level100PlayerLockSetSnapshot(
    IReadOnlyList<Level100PlayerLockSnapshot> Locks,
    IReadOnlyList<Level100PlayerLockSnapshot> FiredLocks,
    ushort RecentLocks,
    int CurrentTarget);

/// <summary>
/// The player Battle Engine's lock sets: <c>mLocks</c> (<c>+0x294</c>),
/// <c>mFiredLocks</c> (<c>+0x2a4</c>), <c>mRecentLocks</c> (the word at
/// <c>+0x2b4</c>) and <c>mCurrentTarget</c> (<c>+0x5e0</c>).
/// </summary>
/// <remarks>
/// <para>
/// Source: <c>references/Onslaught/BattleEngine.cpp:586-1010</c> and
/// <c>:1958-1965</c>, which the RE lane measured against the pristine
/// <c>74154bfa…</c> image as matching in structure, order and gates:
/// <c>HandleLocks</c> <c>0x00406560</c>, <c>StartLock</c> <c>0x00406fc0</c>,
/// <c>FireLock</c> <c>0x00407060</c>, <c>LockHit</c> <c>0x00407140</c> and
/// <c>GetCurrentTarget</c> <c>0x004071b0</c>
/// (<c>reverse-engineering/game-mechanics/level100-final-drone-wave.md</c>,
/// "Missile Pod locks"; function notes under
/// <c>reverse-engineering/binary-analysis/functions/BattleEngine.cpp/</c>).
/// </para>
/// <para>
/// Both sets are <c>SPtrSet</c>s (<c>SPtrSet.cpp:150-293</c>): <c>Append</c>
/// adds at the tail, <c>Add</c> at the head, and <c>Remove</c> takes the first
/// match without touching the set's iterator. <c>StartLock</c> appends;
/// <c>FireLock</c> moves an entry to the head of the fired set
/// (<c>CSPtrSet__AddToHead</c> <c>0x004e5a80</c> at <c>0x00407106</c>).
/// </para>
/// <para>
/// An entry's unit is an active reader: it reads as null once the unit is
/// deleted, which Core represents as <see cref="Level100ActorLifecycle.Destroyed"/>.
/// Actor identities are never reused, so a retained identity whose actor is
/// destroyed is exactly a cleared reader. <c>Size()</c> still counts such
/// entries, which is why <see cref="RecordFireWeapon"/> uses the raw counts.
/// </para>
/// </remarks>
internal sealed class Level100PlayerLocks
{
    private sealed class LockInfo
    {
        internal required Level100ActorId Unit { get; init; }
        internal float Start { get; set; }
        internal float Finish { get; set; }
        internal bool DirectLock { get; init; }
    }

    // CLockInfo::Fired's window, file 0x001d85ec = 00 00 00 3f.
    internal const float FiredWindowSeconds = 0.5f;

    private readonly List<LockInfo> _locks = [];
    private readonly List<LockInfo> _firedLocks = [];
    private readonly Func<Level100ActorId, Level100ActorLifecycle> _lifecycle;
    private ushort _recentLocks;
    private int _currentTarget;

    internal Level100PlayerLocks(Func<Level100ActorId, Level100ActorLifecycle> lifecycle)
    {
        _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
    }

    internal Level100PlayerLockSetSnapshot Snapshot => new(
        Array.AsReadOnly(_locks.Select(ToSnapshot).ToArray()),
        Array.AsReadOnly(_firedLocks.Select(ToSnapshot).ToArray()),
        _recentLocks,
        _currentTarget);

    internal ushort RecentLocks => _recentLocks;

    internal int CurrentTargetCursor => _currentTarget;

    internal void Restore(Level100PlayerLockSetSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _locks.Clear();
        _firedLocks.Clear();
        _locks.AddRange(snapshot.Locks.Select(FromSnapshot));
        _firedLocks.AddRange(snapshot.FiredLocks.Select(FromSnapshot));
        _recentLocks = snapshot.RecentLocks;
        _currentTarget = snapshot.CurrentTarget;
    }

    internal void Clear()
    {
        _locks.Clear();
        _firedLocks.Clear();
        _recentLocks = 0;
        _currentTarget = 0;
    }

    /// <summary>The unit an entry's reader returns, or null once it is deleted.</summary>
    private Level100ActorId? ToRead(LockInfo item) =>
        _lifecycle(item.Unit) == Level100ActorLifecycle.Destroyed ? null : item.Unit;

    private bool IsDying(Level100ActorId unit) =>
        _lifecycle(unit) is Level100ActorLifecycle.StartedDying or
            Level100ActorLifecycle.DiedAwaitingShutdown;

    /// <summary><c>CountLocks</c> — entries whose unit still reads.</summary>
    internal int CountLocks() => _locks.Count(item => ToRead(item) is not null);

    /// <summary><c>Locked</c> — an entry of <c>mLocks</c> reads as this unit.</summary>
    internal bool Locked(Level100ActorId unit) =>
        _locks.Any(item => ToRead(item) == unit);

    /// <summary><c>FiredAt</c> — an entry of <c>mFiredLocks</c> reads as this unit.</summary>
    internal bool FiredAt(Level100ActorId unit) =>
        _firedLocks.Any(item => ToRead(item) == unit);

    /// <summary>
    /// The prune pass at the head of <c>HandleLocks</c>
    /// (<c>BattleEngine.cpp:609-638</c>). The walk restarts from
    /// <c>First()</c> after a removal and then takes <c>Next()</c>, so the
    /// entry that becomes first is not re-examined in the same pass; that is
    /// the source's iteration, reproduced rather than tidied.
    /// </summary>
    /// <param name="keep">Whether a live unit's lock survives: the
    /// deflection test, <c>loseLocks</c> and <c>IsDying</c>.</param>
    internal void Prune(Func<Level100ActorId, bool> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        int cursor = _locks.Count > 0 ? 0 : -1;
        while (cursor >= 0)
        {
            LockInfo info = _locks[cursor];
            bool removed = ToRead(info) is not { } unit || !keep(unit);
            if (removed)
            {
                _locks.RemoveAt(cursor);
                cursor = _locks.Count > 0 ? 0 : -1;
            }

            if (cursor >= 0)
            {
                cursor++;
                if (cursor >= _locks.Count)
                {
                    cursor = -1;
                }
            }
        }
    }

    /// <summary>
    /// <c>StartLock</c> (<c>BattleEngine.cpp:800-827</c>, <c>0x00406fc0</c>):
    /// no lock on a dying unit or one already in <c>mLocks</c>; otherwise
    /// append <c>{unit, now, now + lockTime, direct}</c>.
    /// </summary>
    internal void StartLock(Level100ActorId unit, float lockTime, bool directLock, float now)
    {
        if (IsDying(unit) || _locks.Any(item => ToRead(item) == unit))
        {
            return;
        }

        _locks.Add(new LockInfo
        {
            Unit = unit,
            Start = now,
            Finish = (float)((double)now + lockTime),
            DirectLock = directLock,
        });
    }

    /// <summary>
    /// <c>FireLock</c> (<c>BattleEngine.cpp:842-866</c>, <c>0x00407060</c>):
    /// the first finished entry of <c>mLocks</c> for this unit leaves it; it
    /// goes to the head of <c>mFiredLocks</c> with a fresh 0.5 s window unless
    /// the unit has already been fired at, in which case it is freed.
    /// </summary>
    internal void FireLock(Level100ActorId? unit, float now)
    {
        if (unit is not { } target)
        {
            return;
        }

        for (int index = 0; index < _locks.Count; index++)
        {
            LockInfo item = _locks[index];
            if (!(item.Finish < now) || ToRead(item) != target)
            {
                continue;
            }

            _locks.RemoveAt(index);
            if (!FiredAt(target))
            {
                // fld mTime / fst [esi+4] / fadd 0.5f / fstp [esi+8].
                item.Start = now;
                item.Finish = (float)((double)now + FiredWindowSeconds);
                _firedLocks.Insert(0, item);
            }
            return;
        }
    }

    /// <summary>
    /// <c>LockHit</c> (<c>BattleEngine.cpp:882-897</c>, <c>0x00407140</c>):
    /// the first fired entry for this unit is removed.
    /// </summary>
    internal void LockHit(Level100ActorId? unit)
    {
        if (unit is not { } target)
        {
            return;
        }

        int index = _firedLocks.FindIndex(item => ToRead(item) == target);
        if (index >= 0)
        {
            _firedLocks.RemoveAt(index);
        }
    }

    /// <summary>
    /// <c>CBattleEngine::FireWeapon</c> (<c>BattleEngine.cpp:1958-1965</c>):
    /// <c>mRecentLocks = mLocks.Size()</c>, or <c>mFiredLocks.Size()</c> when
    /// that is zero, stored as a <c>UWORD</c>.
    /// </summary>
    internal void RecordFireWeapon()
    {
        int size = _locks.Count != 0 ? _locks.Count : _firedLocks.Count;
        _recentLocks = unchecked((ushort)size);
    }

    /// <summary>
    /// <c>CWeapon::Fire</c> zeroes the Battle Engine's <c>mCurrentTarget</c>
    /// (<c>+0x5e0</c>, <c>0x00506137</c>) once its reload check passes.
    /// </summary>
    internal void ResetCurrentTarget() => _currentTarget = 0;

    /// <summary>
    /// <c>GetCurrentTarget</c> (<c>BattleEngine.cpp:913-977</c>, vtable slot 81,
    /// <c>0x004071b0</c>): advance the cursor, then return the first finished
    /// unfired lock, else walk the fired set round-robin, wrapping every
    /// <c>mRecentLocks</c> steps.
    /// </summary>
    internal Level100ActorId? GetCurrentTarget(float now)
    {
        int target = _currentTarget;
        _currentTarget = unchecked(_currentTarget + 1);

        if (_recentLocks == 0)
        {
            return null;
        }

        foreach (LockInfo item in _locks)
        {
            if (item.Finish < now && ToRead(item) is { } unit)
            {
                return unit;
            }
        }

        if (!_firedLocks.Any(item => ToRead(item) is not null))
        {
            return null;
        }

        // UWORD n=1; for (item=First(); TRUE; item=Next()) { ... }.
        ushort n = 1;
        int cursor = 0;
        while (true)
        {
            if (n == _recentLocks)
            {
                cursor = 0;
                n = 0;
            }

            if (cursor >= _firedLocks.Count)
            {
                // Next() ran off the end: item is null, restart from First().
                cursor = 0;
                if (_firedLocks.Count == 0)
                {
                    return null;
                }
            }

            LockInfo item = _firedLocks[cursor];
            if (target == 0)
            {
                if (ToRead(item) is { } unit)
                {
                    return unit;
                }
            }
            else
            {
                target--;
                n = unchecked((ushort)(n + 1));
            }

            cursor++;
        }
    }

    private static Level100PlayerLockSnapshot ToSnapshot(LockInfo item) => new(
        item.Unit,
        BitConverter.SingleToUInt32Bits(item.Start),
        BitConverter.SingleToUInt32Bits(item.Finish),
        item.DirectLock);

    private static LockInfo FromSnapshot(Level100PlayerLockSnapshot item) => new()
    {
        Unit = item.Unit,
        Start = BitConverter.UInt32BitsToSingle(item.StartBits),
        Finish = BitConverter.UInt32BitsToSingle(item.FinishBits),
        DirectLock = item.DirectLock,
    };
}
