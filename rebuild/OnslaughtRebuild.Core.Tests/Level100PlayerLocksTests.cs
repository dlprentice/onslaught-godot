// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The Battle Engine's lock sets as <c>BattleEngine.cpp:586-1010</c> and
/// <c>:1958-1965</c> keep them; the RE lane measured retail
/// (<c>0x00406560</c>, <c>0x00406fc0</c>, <c>0x00407060</c>,
/// <c>0x00407140</c>, <c>0x004071b0</c>) as matching in structure, order and
/// gates.
/// </summary>
public sealed class Level100PlayerLocksTests
{
    private static readonly Level100ActorId A = new(11);
    private static readonly Level100ActorId B = new(12);
    private static readonly Level100ActorId C = new(13);

    private readonly Dictionary<Level100ActorId, Level100ActorLifecycle> _lifecycle = new()
    {
        [A] = Level100ActorLifecycle.Alive,
        [B] = Level100ActorLifecycle.Alive,
        [C] = Level100ActorLifecycle.Alive,
    };

    private Level100PlayerLocks CreateLocks() => new(id => _lifecycle[id]);

    [Fact]
    public void StartLock_RefusesDyingUnitsAndDuplicatesAndAppendsInOrder()
    {
        Level100PlayerLocks locks = CreateLocks();
        _lifecycle[C] = Level100ActorLifecycle.StartedDying;
        locks.StartLock(A, 0.2f, directLock: true, now: 1.0f);
        locks.StartLock(A, 0.2f, directLock: true, now: 1.5f);
        locks.StartLock(C, 0.2f, directLock: true, now: 1.0f);
        locks.StartLock(B, 0.2f, directLock: false, now: 1.05f);

        Level100PlayerLockSetSnapshot snapshot = locks.Snapshot;
        Assert.Equal([A, B], snapshot.Locks.Select(item => item.Unit));
        Assert.Equal(BitConverter.SingleToUInt32Bits(1.0f), snapshot.Locks[0].StartBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits(1.2f), snapshot.Locks[0].FinishBits);
        Assert.True(snapshot.Locks[0].DirectLock);
        Assert.False(snapshot.Locks[1].DirectLock);
        Assert.Equal(2, locks.CountLocks());
    }

    [Fact]
    public void FireLock_MovesOnlyAFinishedLockToTheHeadOfTheFiredSetWithAHalfSecondWindow()
    {
        Level100PlayerLocks locks = CreateLocks();
        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.StartLock(B, 0.2f, true, 1.0f);

        // Finish 1.2 is not strictly below 1.2: nothing moves.
        locks.FireLock(A, 1.2f);
        Assert.Equal(2, locks.Snapshot.Locks.Count);

        locks.FireLock(A, 1.25f);
        locks.FireLock(B, 1.3f);
        Level100PlayerLockSetSnapshot snapshot = locks.Snapshot;
        Assert.Empty(snapshot.Locks);
        Assert.Equal([B, A], snapshot.FiredLocks.Select(item => item.Unit));
        Assert.Equal(BitConverter.SingleToUInt32Bits(1.3f), snapshot.FiredLocks[0].StartBits);
        Assert.Equal(BitConverter.SingleToUInt32Bits(1.8f), snapshot.FiredLocks[0].FinishBits);
    }

    [Fact]
    public void FireLock_FreesTheEntryWhenTheUnitWasAlreadyFiredAt()
    {
        Level100PlayerLocks locks = CreateLocks();
        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.FireLock(A, 1.5f);
        locks.StartLock(A, 0.2f, true, 2.0f);
        locks.FireLock(A, 2.5f);

        Level100PlayerLockSetSnapshot snapshot = locks.Snapshot;
        Assert.Empty(snapshot.Locks);
        Level100PlayerLockSnapshot fired = Assert.Single(snapshot.FiredLocks);
        Assert.Equal(BitConverter.SingleToUInt32Bits(1.5f), fired.StartBits);
    }

    [Fact]
    public void LockHit_RemovesTheFiredEntryForThatUnitOnly()
    {
        Level100PlayerLocks locks = CreateLocks();
        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.StartLock(B, 0.2f, true, 1.0f);
        locks.FireLock(A, 1.5f);
        locks.FireLock(B, 1.5f);
        locks.LockHit(A);
        locks.LockHit(null);
        Assert.Equal([B], locks.Snapshot.FiredLocks.Select(item => item.Unit));
    }

    [Fact]
    public void RecordFireWeapon_CountsEntriesWhoseUnitsNoLongerRead()
    {
        Level100PlayerLocks locks = CreateLocks();
        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.StartLock(B, 0.2f, true, 1.0f);
        _lifecycle[B] = Level100ActorLifecycle.Destroyed;
        locks.RecordFireWeapon();
        Assert.Equal(2, locks.RecentLocks);
        Assert.Equal(1, locks.CountLocks());

        Level100PlayerLocks fired = CreateLocks();
        fired.StartLock(C, 0.2f, true, 1.0f);
        fired.FireLock(C, 1.5f);
        fired.RecordFireWeapon();
        Assert.Equal(1, fired.RecentLocks);
    }

    [Fact]
    public void GetCurrentTarget_PrefersFinishedUnfiredLocksThenRoundRobinsTheFiredSet()
    {
        Level100PlayerLocks locks = CreateLocks();
        Assert.Null(locks.GetCurrentTarget(0.0f));
        Assert.Equal(1, locks.CurrentTargetCursor);

        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.StartLock(B, 0.2f, true, 1.0f);
        locks.StartLock(C, 0.2f, true, 1.0f);
        locks.RecordFireWeapon();
        Assert.Equal(3, locks.RecentLocks);

        // An unfinished lock is not a target yet.
        Assert.Null(locks.GetCurrentTarget(1.1f));

        // Each round fires the first finished unfired lock, as the burst does.
        foreach (Level100ActorId expected in new[] { A, B, C })
        {
            Level100ActorId? target = locks.GetCurrentTarget(1.5f);
            Assert.Equal(expected, target);
            locks.FireLock(target, 1.5f);
        }

        // The fired set is [C, B, A] (head insertion). The walk decrements
        // the saved cursor once per entry and restarts from First() whenever
        // n reaches mRecentLocks (3), so cursors 5, 6, 7 and 8 land on C, B,
        // A and C, as a hand trace of BattleEngine.cpp:945-972 gives.
        Assert.Equal(5, locks.CurrentTargetCursor);
        Level100ActorId?[] next = Enumerable.Range(0, 4)
            .Select(_ => locks.GetCurrentTarget(1.6f))
            .ToArray();
        Assert.Equal(new Level100ActorId?[] { C, B, A, C }, next);
    }

    [Fact]
    public void Prune_SkipsTheNewFirstEntryAfterEachRemoval()
    {
        Level100PlayerLocks locks = CreateLocks();
        locks.StartLock(A, 0.2f, true, 1.0f);
        locks.StartLock(B, 0.2f, true, 1.0f);
        locks.StartLock(C, 0.2f, true, 1.0f);

        // Remove A: the walk restarts at B and moves straight on to C, so B
        // survives this pass even though it would be removed if examined.
        var seen = new List<Level100ActorId>();
        locks.Prune(unit =>
        {
            seen.Add(unit);
            return unit == C;
        });
        Assert.Equal([A, C], seen);
        Assert.Equal([B, C], locks.Snapshot.Locks.Select(item => item.Unit));

        // A deleted unit is removed without consulting the rule.
        _lifecycle[B] = Level100ActorLifecycle.Destroyed;
        seen.Clear();
        locks.Prune(unit =>
        {
            seen.Add(unit);
            return true;
        });
        Assert.Empty(seen);
        Assert.Equal([C], locks.Snapshot.Locks.Select(item => item.Unit));
    }
}
