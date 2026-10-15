// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Terminal decision made by the direct/current-target arm of released
/// <c>CUnitAI</c> virtual slot 4.
/// </summary>
public enum RetailUnitAIDirectTargetArmDisposition
{
    FallsThroughToFallback,
    HandledDirectArm,
}

/// <summary>
/// One adapter-visible operation in the direct arm's released order.
/// </summary>
public enum RetailUnitAIDirectTargetArmActionKind
{
    InvokeSetReaderNull,
    InvokeMembershipVirtual83,
    WriteResult18,
    WriteResult1C,
    InvokeActiveStateGate,
    InvokeSupportUpdate,
    InvokeHelperBAndWriteResult1C,
    InvokeHelperAWithZeroContextAndWriteResult18,
}

/// <summary>
/// One ordered direct-arm action. <paramref name="TargetIdentity"/> is the
/// freshly observed reader or virtual receiver for that stage. On a reader
/// clear it is the old target; the adapter invokes SetReader with null.
/// <paramref name="RawValue"/> carries virtual/helper results or dword writes.
/// </summary>
public readonly record struct RetailUnitAIDirectTargetArmAction(
    RetailUnitAIDirectTargetArmActionKind Kind,
    int? TargetIdentity,
    int? RawValue)
{
    public static RetailUnitAIDirectTargetArmAction SetReaderNull(int oldTarget) =>
        new(RetailUnitAIDirectTargetArmActionKind.InvokeSetReaderNull, oldTarget, null);

    public static RetailUnitAIDirectTargetArmAction MembershipVirtual83(
        int membership,
        int result) =>
        new(
            RetailUnitAIDirectTargetArmActionKind.InvokeMembershipVirtual83,
            membership,
            result);

    public static RetailUnitAIDirectTargetArmAction WriteResult18(int value) =>
        new(RetailUnitAIDirectTargetArmActionKind.WriteResult18, null, value);

    public static RetailUnitAIDirectTargetArmAction WriteResult1C(int value) =>
        new(RetailUnitAIDirectTargetArmActionKind.WriteResult1C, null, value);

    public static RetailUnitAIDirectTargetArmAction ActiveStateGate(
        int target,
        bool passes) =>
        new(
            RetailUnitAIDirectTargetArmActionKind.InvokeActiveStateGate,
            target,
            passes ? 1 : 0);

    public static RetailUnitAIDirectTargetArmAction SupportUpdate(int? target) =>
        new(RetailUnitAIDirectTargetArmActionKind.InvokeSupportUpdate, target, null);

    public static RetailUnitAIDirectTargetArmAction HelperB(
        int? target,
        int result) =>
        new(
            RetailUnitAIDirectTargetArmActionKind.InvokeHelperBAndWriteResult1C,
            target,
            result);

    public static RetailUnitAIDirectTargetArmAction HelperA(
        int? target,
        int result) =>
        new(
            RetailUnitAIDirectTargetArmActionKind
                .InvokeHelperAWithZeroContextAndWriteResult18,
            target,
            result);
}

/// <summary>
/// Stage-local observations for the direct arm. Each reader identity is read
/// from the canonical lifecycle-aware cell at its named released call site,
/// after preceding actions; these are not one eagerly captured pointer.
/// A null membership identity means the owner has no recognized membership
/// receiver and the raw virtual result is ignored.
/// </summary>
public readonly record struct RetailUnitAIDirectTargetArmRequest(
    int? EntryReaderIdentity,
    bool EntryReaderIsDying,
    int? MembershipIdentity,
    int MembershipVirtual83Result,
    int? ReaderAtStateGateIdentity,
    bool ReaderPassesActiveStateGate,
    int? ReaderAtSupportIdentity,
    int? ReaderAtHelperBIdentity,
    int HelperResultB,
    int? ReaderAtHelperAIdentity,
    int HelperResultA);

/// <summary>
/// Exact ordered adapter transcript for one direct-arm attempt.
/// </summary>
public sealed record RetailUnitAIDirectTargetArmPlan(
    RetailUnitAIDirectTargetArmDisposition Disposition,
    RetailUnitAIDirectTargetArmAction[] Actions);

/// <summary>
/// Deterministic boundary for PC retail <c>0x004FF4F7..0x004FF57E</c> and
/// the released PC demo, Xbox, and PS2 correspondents.
/// </summary>
/// <remarks>
/// The membership virtual's authored name remains unknown. The recognized PC
/// lifecycle supplies a normal-squad receiver whose slot 83 returns zero; the
/// raw zero/nonzero result is retained here. Concrete readers, monitor reverse
/// sets, virtual dispatch, and support/fire helpers remain adapter-owned.
/// </remarks>
public static class RetailUnitAIDirectTargetArm
{
    public static RetailUnitAIDirectTargetArmPlan Plan(
        RetailUnitAIDirectTargetArmRequest request)
    {
        var actions = new List<RetailUnitAIDirectTargetArmAction>();

        if (request.EntryReaderIdentity is { } dyingEntry &&
            request.EntryReaderIsDying)
        {
            actions.Add(RetailUnitAIDirectTargetArmAction.SetReaderNull(dyingEntry));
        }

        if (request.MembershipIdentity is not { } membership)
        {
            return Plan(
                RetailUnitAIDirectTargetArmDisposition.FallsThroughToFallback,
                actions);
        }

        actions.Add(RetailUnitAIDirectTargetArmAction.MembershipVirtual83(
            membership,
            request.MembershipVirtual83Result));
        if (request.MembershipVirtual83Result != 0)
        {
            return Plan(
                RetailUnitAIDirectTargetArmDisposition.FallsThroughToFallback,
                actions);
        }

        actions.Add(RetailUnitAIDirectTargetArmAction.WriteResult18(0));
        actions.Add(RetailUnitAIDirectTargetArmAction.WriteResult1C(0));

        if (request.ReaderAtStateGateIdentity is not { } stateTarget)
        {
            return Plan(
                RetailUnitAIDirectTargetArmDisposition.HandledDirectArm,
                actions);
        }

        actions.Add(RetailUnitAIDirectTargetArmAction.ActiveStateGate(
            stateTarget,
            request.ReaderPassesActiveStateGate));
        if (!request.ReaderPassesActiveStateGate)
        {
            actions.Add(RetailUnitAIDirectTargetArmAction.SetReaderNull(stateTarget));
            return Plan(
                RetailUnitAIDirectTargetArmDisposition.HandledDirectArm,
                actions);
        }

        actions.Add(RetailUnitAIDirectTargetArmAction.SupportUpdate(
            request.ReaderAtSupportIdentity));
        actions.Add(RetailUnitAIDirectTargetArmAction.HelperB(
            request.ReaderAtHelperBIdentity,
            request.HelperResultB));
        if (request.HelperResultB != 0)
        {
            actions.Add(RetailUnitAIDirectTargetArmAction.HelperA(
                request.ReaderAtHelperAIdentity,
                request.HelperResultA));
        }

        return Plan(
            RetailUnitAIDirectTargetArmDisposition.HandledDirectArm,
            actions);
    }

    private static RetailUnitAIDirectTargetArmPlan Plan(
        RetailUnitAIDirectTargetArmDisposition disposition,
        List<RetailUnitAIDirectTargetArmAction> actions) =>
        new(disposition, actions.ToArray());
}
