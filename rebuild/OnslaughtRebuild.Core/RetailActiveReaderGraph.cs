// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One observable mutation in the released active-reader relationship.
/// </summary>
public enum RetailActiveReaderActionKind
{
    DetachOldTarget,
    PublishNewTarget,
    AttachNewTarget,
    InvalidateReaderCell,
    ClearTargetReverseMembership,
}

/// <summary>
/// One active-reader action in released order. A null reader identity is used
/// only when the target's reverse-membership container is cleared.
/// </summary>
public readonly record struct RetailActiveReaderAction(
    RetailActiveReaderActionKind Kind,
    int? ReaderCellIdentity,
    int? TargetIdentity);

/// <summary>
/// Deterministic model of retail's non-owning <c>CGenericActiveReader</c> cells
/// and each target monitor's newest-first reverse membership.
/// </summary>
/// <remarks>
/// Integer identities stand for stable runtime storage cells and monitored
/// objects; they are not substitute pointers. The adapter remains responsible
/// for object allocation and for calling <see cref="ShutdownTarget"/> before
/// removing a monitored object. Ordinary use enforces one membership per
/// reader/target pair instead of reproducing retail's malformed duplicate-set
/// possibility or allocation-failure crash.
/// </remarks>
public sealed class RetailActiveReaderGraph
{
    private readonly Dictionary<int, int?> _readerTargets = [];
    private readonly Dictionary<int, List<int>> _targetReadersNewestFirst = [];

    /// <summary>
    /// Creates one stable reader cell. A non-null initial target reproduces a
    /// typed reader constructor/copy: the new cell is registered at the head
    /// of that target's reverse set.
    /// </summary>
    public void CreateReaderCell(int readerCellIdentity, int? initialTarget = null)
    {
        if (!_readerTargets.TryAdd(readerCellIdentity, initialTarget))
        {
            throw new InvalidOperationException(
                $"Reader cell {readerCellIdentity} already exists.");
        }

        if (initialTarget is { } target)
        {
            ReadersFor(target).Insert(0, readerCellIdentity);
        }
    }

    /// <summary>
    /// Reproduces same-target no-op; otherwise detach old, publish new/null,
    /// then attach new. Empty reverse containers remain allocated until target
    /// shutdown, matching retail <c>CSPtrSet::Remove</c>.
    /// </summary>
    public RetailActiveReaderAction[] SetReader(
        int readerCellIdentity,
        int? newTarget)
    {
        int? oldTarget = RequireReader(readerCellIdentity);
        if (oldTarget == newTarget)
        {
            return [];
        }

        var actions = new List<RetailActiveReaderAction>();
        if (oldTarget is { } old)
        {
            RemoveExistingMembership(old, readerCellIdentity);
            actions.Add(new(
                RetailActiveReaderActionKind.DetachOldTarget,
                readerCellIdentity,
                old));
        }

        _readerTargets[readerCellIdentity] = newTarget;
        actions.Add(new(
            RetailActiveReaderActionKind.PublishNewTarget,
            readerCellIdentity,
            newTarget));

        if (newTarget is { } next)
        {
            ReadersFor(next).Insert(0, readerCellIdentity);
            actions.Add(new(
                RetailActiveReaderActionKind.AttachNewTarget,
                readerCellIdentity,
                next));
        }

        return actions.ToArray();
    }

    /// <summary>
    /// Reproduces monitor shutdown: zero every registered reader cell newest
    /// first, then clear the target's reverse container. No owner callback or
    /// adjacent UnitAI-field mutation occurs.
    /// </summary>
    public RetailActiveReaderAction[] ShutdownTarget(int targetIdentity)
    {
        if (!_targetReadersNewestFirst.Remove(
                targetIdentity,
                out List<int>? readers))
        {
            return [];
        }

        var actions = new List<RetailActiveReaderAction>(readers.Count + 1);
        foreach (int readerCellIdentity in readers)
        {
            if (!_readerTargets.ContainsKey(readerCellIdentity))
            {
                throw new InvalidOperationException(
                    $"Target {targetIdentity} references missing reader cell " +
                    $"{readerCellIdentity}.");
            }

            _readerTargets[readerCellIdentity] = null;
            actions.Add(new(
                RetailActiveReaderActionKind.InvalidateReaderCell,
                readerCellIdentity,
                targetIdentity));
        }

        actions.Add(new(
            RetailActiveReaderActionKind.ClearTargetReverseMembership,
            null,
            targetIdentity));
        return actions.ToArray();
    }

    /// <summary>
    /// Reproduces the PC retail/demo <c>CUnitAI</c> destructor order: detach
    /// outbound cells +0x28, +0x24, +0x0C without publishing null into dying
    /// storage, then invalidate inbound readers through the AI's own monitor.
    /// </summary>
    public RetailActiveReaderAction[] DestroyUnitAiReaderOwner(
        int unitAiTargetIdentity,
        int readerCell28,
        int readerCell24,
        int readerCell0C)
    {
        if (new HashSet<int> { readerCell28, readerCell24, readerCell0C }.Count != 3)
        {
            throw new ArgumentException("UnitAI reader-cell identities must be distinct.");
        }

        var actions = new List<RetailActiveReaderAction>();
        DestroyReaderCell(readerCell28, actions);
        DestroyReaderCell(readerCell24, actions);
        DestroyReaderCell(readerCell0C, actions);
        actions.AddRange(ShutdownTarget(unitAiTargetIdentity));
        return actions.ToArray();
    }

    public int? TargetOf(int readerCellIdentity) => RequireReader(readerCellIdentity);

    public bool ContainsReaderCell(int readerCellIdentity) =>
        _readerTargets.ContainsKey(readerCellIdentity);

    public bool TargetHasReverseContainer(int targetIdentity) =>
        _targetReadersNewestFirst.ContainsKey(targetIdentity);

    public int[] ReadersNewestFirst(int targetIdentity) =>
        _targetReadersNewestFirst.TryGetValue(targetIdentity, out List<int>? readers)
            ? readers.ToArray()
            : [];

    private int? RequireReader(int readerCellIdentity)
    {
        if (!_readerTargets.TryGetValue(readerCellIdentity, out int? target))
        {
            throw new KeyNotFoundException(
                $"Unknown reader cell {readerCellIdentity}.");
        }

        return target;
    }

    private List<int> ReadersFor(int targetIdentity)
    {
        if (!_targetReadersNewestFirst.TryGetValue(
                targetIdentity,
                out List<int>? readers))
        {
            readers = [];
            _targetReadersNewestFirst.Add(targetIdentity, readers);
        }

        return readers;
    }

    private void RemoveExistingMembership(
        int targetIdentity,
        int readerCellIdentity)
    {
        if (!_targetReadersNewestFirst.TryGetValue(
                targetIdentity,
                out List<int>? readers) ||
            !readers.Remove(readerCellIdentity))
        {
            throw new InvalidOperationException(
                $"Reader cell {readerCellIdentity} is not registered with target " +
                $"{targetIdentity}.");
        }
    }

    private void DestroyReaderCell(
        int readerCellIdentity,
        List<RetailActiveReaderAction> actions)
    {
        int? target = RequireReader(readerCellIdentity);
        if (target is { } old)
        {
            RemoveExistingMembership(old, readerCellIdentity);
            actions.Add(new(
                RetailActiveReaderActionKind.DetachOldTarget,
                readerCellIdentity,
                old));
        }

        _readerTargets.Remove(readerCellIdentity);
    }
}
