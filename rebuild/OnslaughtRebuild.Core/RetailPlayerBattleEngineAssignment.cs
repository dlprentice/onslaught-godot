// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Adapter-owned identities and the raw player God word required by the
/// bounded retail <c>CPlayer::AssignBattleEngine</c> operation.
/// </summary>
/// <remarks>
/// Identities are stable deterministic tokens, not retail pointers. Both
/// reader cells must already exist in the supplied
/// <see cref="RetailActiveReaderGraph"/>, and the adapter remains responsible
/// for proving that the engine-side cell belongs to the supplied already-
/// constructed, non-null engine-domain object.
/// </remarks>
public readonly record struct RetailPlayerBattleEngineAssignmentRequest(
    int PlayerIdentity,
    int PlayerBattleEngineReaderCellIdentity,
    int BattleEngineIdentity,
    int BattleEnginePlayerReaderCellIdentity,
    int PlayerGodWord);

/// <summary>
/// One function-level call in the released assignment order.
/// </summary>
public enum RetailPlayerBattleEngineAssignmentCallKind
{
    SetPlayerBattleEngineReader,
    SetBattleEnginePlayerReader,

    /// <summary>
    /// Source-correlated <c>SetVulnerable(FALSE)</c> call intent. The Core
    /// owner records the exact raw argument but does not execute the virtual.
    /// </summary>
    SetVulnerable,

    /// <summary>
    /// Conventional spelling for source <c>SetInfinateEnergy(TRUE)</c>. The
    /// concrete virtual also refills energy from configuration; this owner
    /// records only the exact call intent and reports no final engine state.
    /// </summary>
    SetInfiniteEnergy,
}

/// <summary>
/// Immutable transcript entry for one released function-level call.
/// </summary>
public sealed class RetailPlayerBattleEngineAssignmentCall
{
    private readonly IReadOnlyList<RetailActiveReaderAction> _readerActions;

    private RetailPlayerBattleEngineAssignmentCall(
        RetailPlayerBattleEngineAssignmentCallKind kind,
        int receiverIdentity,
        int? targetIdentity,
        int? rawBooleanArgument,
        IReadOnlyList<RetailActiveReaderAction> readerActions)
    {
        Kind = kind;
        ReceiverIdentity = receiverIdentity;
        TargetIdentity = targetIdentity;
        RawBooleanArgument = rawBooleanArgument;
        _readerActions = Array.AsReadOnly(readerActions.ToArray());
    }

    public RetailPlayerBattleEngineAssignmentCallKind Kind { get; }

    /// <summary>
    /// The reader-cell receiver for a reader call, or the Battle Engine object
    /// receiver for a policy call.
    /// </summary>
    public int ReceiverIdentity { get; }

    /// <summary>
    /// The new target for a reader call; null for a policy call.
    /// </summary>
    public int? TargetIdentity { get; }

    /// <summary>
    /// The exact Win32-BOOL-style argument for a policy call; null for a
    /// reader call.
    /// </summary>
    public int? RawBooleanArgument { get; }

    /// <summary>
    /// Ordered generic-reader mutations. The list is empty for policy calls
    /// and for same-target reader calls, while the outer call remains present.
    /// </summary>
    public IReadOnlyList<RetailActiveReaderAction> ReaderActions =>
        _readerActions;

    internal static RetailPlayerBattleEngineAssignmentCall ReaderCall(
        RetailPlayerBattleEngineAssignmentCallKind kind,
        int receiverIdentity,
        int targetIdentity,
        IReadOnlyList<RetailActiveReaderAction> readerActions)
    {
        if (kind is not
            (RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader or
             RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return new(
            kind,
            receiverIdentity,
            targetIdentity,
            rawBooleanArgument: null,
            readerActions);
    }

    internal static RetailPlayerBattleEngineAssignmentCall PolicyCall(
        RetailPlayerBattleEngineAssignmentCallKind kind,
        int battleEngineIdentity,
        int rawBooleanArgument)
    {
        if (kind is not
            (RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable or
             RetailPlayerBattleEngineAssignmentCallKind.SetInfiniteEnergy))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }

        return new(
            kind,
            battleEngineIdentity,
            targetIdentity: null,
            rawBooleanArgument,
            readerActions: []);
    }
}

/// <summary>
/// Deeply immutable transcript of one bounded assignment operation.
/// </summary>
public sealed class RetailPlayerBattleEngineAssignmentResult
{
    private readonly IReadOnlyList<RetailPlayerBattleEngineAssignmentCall> _calls;

    internal RetailPlayerBattleEngineAssignmentResult(
        IReadOnlyList<RetailPlayerBattleEngineAssignmentCall> calls)
    {
        _calls = Array.AsReadOnly(calls.ToArray());
    }

    public IReadOnlyList<RetailPlayerBattleEngineAssignmentCall> Calls => _calls;
}

/// <summary>
/// Carries the valid-object portion of pristine PC
/// <c>CPlayer::AssignBattleEngine</c> at
/// <c>[0x004d3080, 0x004d30c5)</c>.
/// </summary>
/// <remarks>
/// The operation performs the two reader calls in released order and emits
/// the two God-only policy-call intents. It does not execute Battle Engine
/// virtual methods, construct objects, clear stale reciprocal links, or claim
/// rollback parity for retail pointer/configuration faults. Preflight merely
/// prevents missing or duplicate required reader-cell roles from being half-
/// applied under Core's deterministic single-threaded use; it is not general
/// atomicity.
/// </remarks>
public static class RetailPlayerBattleEngineAssignment
{
    public static RetailPlayerBattleEngineAssignmentResult Assign(
        RetailActiveReaderGraph graph,
        RetailPlayerBattleEngineAssignmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (request.PlayerBattleEngineReaderCellIdentity ==
            request.BattleEnginePlayerReaderCellIdentity)
        {
            throw new ArgumentException(
                "Player and Battle Engine reader-cell identities must be distinct.",
                nameof(request));
        }

        RequireReaderCell(
            graph,
            request.PlayerBattleEngineReaderCellIdentity,
            nameof(request));
        RequireReaderCell(
            graph,
            request.BattleEnginePlayerReaderCellIdentity,
            nameof(request));

        var calls = new List<RetailPlayerBattleEngineAssignmentCall>(capacity: 4);

        RetailActiveReaderAction[] playerReaderActions = graph.SetReader(
            request.PlayerBattleEngineReaderCellIdentity,
            request.BattleEngineIdentity);
        calls.Add(RetailPlayerBattleEngineAssignmentCall.ReaderCall(
            RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
            request.PlayerBattleEngineReaderCellIdentity,
            request.BattleEngineIdentity,
            playerReaderActions));

        RetailActiveReaderAction[] engineReaderActions = graph.SetReader(
            request.BattleEnginePlayerReaderCellIdentity,
            request.PlayerIdentity);
        calls.Add(RetailPlayerBattleEngineAssignmentCall.ReaderCall(
            RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
            request.BattleEnginePlayerReaderCellIdentity,
            request.PlayerIdentity,
            engineReaderActions));

        if (request.PlayerGodWord != 0)
        {
            calls.Add(RetailPlayerBattleEngineAssignmentCall.PolicyCall(
                RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable,
                request.BattleEngineIdentity,
                rawBooleanArgument: 0));
            calls.Add(RetailPlayerBattleEngineAssignmentCall.PolicyCall(
                RetailPlayerBattleEngineAssignmentCallKind.SetInfiniteEnergy,
                request.BattleEngineIdentity,
                rawBooleanArgument: 1));
        }

        return new(calls);
    }

    private static void RequireReaderCell(
        RetailActiveReaderGraph graph,
        int readerCellIdentity,
        string parameterName)
    {
        if (!graph.ContainsReaderCell(readerCellIdentity))
        {
            throw new ArgumentException(
                $"Reader cell {readerCellIdentity} must already exist.",
                parameterName);
        }
    }
}
