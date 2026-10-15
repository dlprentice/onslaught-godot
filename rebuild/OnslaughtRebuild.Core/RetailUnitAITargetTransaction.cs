// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Released floating-point predicate used before <c>CUnitAI</c> reuses its
/// retained target without running the full selector.
/// </summary>
public enum RetailUnitAIFastReuseFloatPolicy
{
    /// <summary>
    /// PC retail/demo test only x87 status bit C3, so equality and unordered
    /// (NaN) both pass this one gate.
    /// </summary>
    PcC3Only,

    /// <summary>
    /// Xbox parity testing and PS2 <c>c.eq.s</c> accept ordered zero only.
    /// </summary>
    ConsoleOrderedEquality,
}

/// <summary>
/// High-level released path selected by one UnitAI fallback transaction.
/// </summary>
public enum RetailUnitAITargetTransactionRoute
{
    Slot4RetainedRefresh,
    Slot11FastReuse,
    Slot11FullSelection,
}

/// <summary>
/// One adapter-visible operation in released execution order.
/// </summary>
public enum RetailUnitAITargetTransactionActionKind
{
    InvokeSetReader,
    InvokeSupportUpdate,
    InvokeHelperBAndWriteResult1C,
    InvokeHelperAAndWriteResult18,
    WriteRetainedTargetGate10,
    WriteResult18,
    WriteResult1C,
}

/// <summary>
/// One ordered UnitAI transaction action. Target-bearing calls use
/// <paramref name="TargetIdentity"/>; raw dword writes/results use
/// <paramref name="RawValue"/>. A null target on <see
/// cref="RetailUnitAITargetTransactionActionKind.InvokeSetReader"/> is the
/// released clear call.
/// </summary>
public readonly record struct RetailUnitAITargetTransactionAction(
    RetailUnitAITargetTransactionActionKind Kind,
    int? TargetIdentity,
    int? RawValue)
{
    public static RetailUnitAITargetTransactionAction SetReader(int? target) =>
        new(RetailUnitAITargetTransactionActionKind.InvokeSetReader, target, null);

    public static RetailUnitAITargetTransactionAction SupportUpdate(int target) =>
        new(RetailUnitAITargetTransactionActionKind.InvokeSupportUpdate, target, null);

    public static RetailUnitAITargetTransactionAction HelperB(int target, int result) =>
        new(
            RetailUnitAITargetTransactionActionKind.InvokeHelperBAndWriteResult1C,
            target,
            result);

    public static RetailUnitAITargetTransactionAction HelperA(int target, int result) =>
        new(
            RetailUnitAITargetTransactionActionKind.InvokeHelperAAndWriteResult18,
            target,
            result);

    public static RetailUnitAITargetTransactionAction WriteGate10(int value) =>
        new(
            RetailUnitAITargetTransactionActionKind.WriteRetainedTargetGate10,
            null,
            value);

    public static RetailUnitAITargetTransactionAction WriteResult18(int value) =>
        new(RetailUnitAITargetTransactionActionKind.WriteResult18, null, value);

    public static RetailUnitAITargetTransactionAction WriteResult1C(int value) =>
        new(RetailUnitAITargetTransactionActionKind.WriteResult1C, null, value);
}

/// <summary>
/// Caller-captured values needed after UnitAI's slot-4 squad scan. The
/// selector winner is supplied by <see cref="RetailUnitAITargetSelection"/>;
/// this type does not run a second selector. Active-state and helper results
/// are observations captured at their corresponding released call sites, after
/// any preceding support updates; they are not speculative pre-call values.
/// </summary>
public readonly record struct RetailUnitAITargetTransactionRequest(
    int? CurrentTargetIdentity,
    int RetainedTargetGate10,
    int FastReuseEligible14,
    float CurrentTargetStealth,
    bool CurrentTargetPassesActiveStateGate,
    int? SelectorWinnerIdentity,
    bool SelectorWinnerPassesActiveStateGate,
    int HelperResultB,
    int HelperResultA,
    RetailUnitAIFastReuseFloatPolicy FloatPolicy);

/// <summary>
/// Exact ordered adapter plan and the target returned by the released slot.
/// </summary>
public sealed record RetailUnitAITargetTransactionPlan(
    RetailUnitAITargetTransactionRoute Path,
    int? ReturnedTargetIdentity,
    RetailUnitAITargetTransactionAction[] Actions);

/// <summary>
/// Deterministic transaction boundary for PC retail slot 4
/// <c>0x004FF6AF..0x004FF70A</c>, slot 11
/// <c>0x004FF710..0x004FFB57</c>, and their released-family correspondents.
/// </summary>
/// <remarks>
/// This deterministic owner emits the reference transaction transcript; it
/// does not schedule the side-effecting calls whose outcomes the request
/// captures. In particular, SetReader owns same-target no-op / unlink-old /
/// store-new / register-new behavior. Target invalidation is out of band
/// relative to this planner but synchronous inside target shutdown before
/// deletion; it clears the reader cell without clearing adjacent UnitAI
/// fields. This planner owns neither monitor lifetime nor virtual calls.
/// </remarks>
public static class RetailUnitAITargetTransaction
{
    /// <summary>
    /// Plans the slot-4 retained refresh when its runtime gate is live;
    /// otherwise plans slot 11's fast-reuse or full-selection transaction.
    /// Raw helper results are stored verbatim and helper A is consumed only
    /// after nonzero helper B.
    /// </summary>
    public static RetailUnitAITargetTransactionPlan PlanFallback(
        RetailUnitAITargetTransactionRequest request)
    {
        if (request.CurrentTargetIdentity is { } retainedTarget &&
            request.RetainedTargetGate10 != 0)
        {
            return PlanResultRefresh(
                RetailUnitAITargetTransactionRoute.Slot4RetainedRefresh,
                retainedTarget,
                request.HelperResultB,
                request.HelperResultA);
        }

        if (request.CurrentTargetIdentity is { } currentTarget &&
            PassesFastReuseStealthGate(
                request.CurrentTargetStealth,
                request.FloatPolicy) &&
            request.FastReuseEligible14 != 0 &&
            request.CurrentTargetPassesActiveStateGate)
        {
            return PlanResultRefresh(
                RetailUnitAITargetTransactionRoute.Slot11FastReuse,
                currentTarget,
                request.HelperResultB,
                request.HelperResultA);
        }

        var actions = new List<RetailUnitAITargetTransactionAction>
        {
            RetailUnitAITargetTransactionAction.WriteResult18(0),
            RetailUnitAITargetTransactionAction.WriteResult1C(0),
        };

        if (request.SelectorWinnerIdentity is not { } winner)
        {
            actions.Add(RetailUnitAITargetTransactionAction.SetReader(null));
            actions.Add(RetailUnitAITargetTransactionAction.WriteGate10(0));
            return new RetailUnitAITargetTransactionPlan(
                RetailUnitAITargetTransactionRoute.Slot11FullSelection,
                null,
                actions.ToArray());
        }

        actions.Add(RetailUnitAITargetTransactionAction.SetReader(winner));
        actions.Add(RetailUnitAITargetTransactionAction.SupportUpdate(winner));
        actions.Add(RetailUnitAITargetTransactionAction.WriteGate10(0));
        actions.Add(RetailUnitAITargetTransactionAction.SupportUpdate(winner));

        if (request.SelectorWinnerPassesActiveStateGate)
        {
            actions.Add(RetailUnitAITargetTransactionAction.HelperB(
                winner,
                request.HelperResultB));
            if (request.HelperResultB != 0)
            {
                actions.Add(RetailUnitAITargetTransactionAction.HelperA(
                    winner,
                    request.HelperResultA));
            }
        }

        // Retail writes this once before the second support update and again
        // on every full-selection exit. The duplicate successful-arm write is
        // observable ordering, not a normalization opportunity.
        actions.Add(RetailUnitAITargetTransactionAction.WriteGate10(0));
        return new RetailUnitAITargetTransactionPlan(
            RetailUnitAITargetTransactionRoute.Slot11FullSelection,
            winner,
            actions.ToArray());
    }

    /// <summary>
    /// Reproduces the released fast-reuse comparison, including PC's
    /// unordered-pass x87 status-bit quirk.
    /// </summary>
    public static bool PassesFastReuseStealthGate(
        float stealth,
        RetailUnitAIFastReuseFloatPolicy policy) => policy switch
        {
            RetailUnitAIFastReuseFloatPolicy.PcC3Only =>
                stealth == 0.0f || float.IsNaN(stealth),
            RetailUnitAIFastReuseFloatPolicy.ConsoleOrderedEquality =>
                stealth == 0.0f,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, null),
        };

    private static RetailUnitAITargetTransactionPlan PlanResultRefresh(
        RetailUnitAITargetTransactionRoute path,
        int target,
        int helperResultB,
        int helperResultA)
    {
        var actions = new List<RetailUnitAITargetTransactionAction>
        {
            RetailUnitAITargetTransactionAction.SupportUpdate(target),
            RetailUnitAITargetTransactionAction.HelperB(target, helperResultB),
        };

        actions.Add(helperResultB != 0
            ? RetailUnitAITargetTransactionAction.HelperA(target, helperResultA)
            : RetailUnitAITargetTransactionAction.WriteResult18(0));

        return new RetailUnitAITargetTransactionPlan(
            path,
            target,
            actions.ToArray());
    }
}
