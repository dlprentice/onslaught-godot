// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>The released arm selected by <c>CUnitAI</c> virtual slot 3.</summary>
public enum RetailUnitAIUpdateRoute
{
    PrimaryJitteredAim,
    PrimaryDirectAim,
    FireSupportCadence,
    IdleShortCadence,
    IdleLongCadence,
}

/// <summary>One externally visible operation in slot 3's released order.</summary>
public enum RetailUnitAIUpdateActionKind
{
    InvokeTargetSupportRefreshSlot4,
    InvokeOwnerVirtual150,
    InvokeTargetVirtual168,
    ConsumeReleasedRandom,
    WriteJitterMagnitude48,
    WriteJitterMagnitude54,
    WriteNegatedJitter48,
    WriteNegatedJitter54,
    WriteDerivedJitterBounds,
    InvokeForwardAim,
    InvokeOwnerVirtual128,
    InvokeSetReaderNull,
    InvokeSupportUpdate,
    WriteRetainedTargetGate10Zero,
    InvokeOwnerVirtual158,
    InvokeMountedUnitPitch,
    WriteOwnerField1ECZero,
    WriteOwnerField1E8Zero,
    InvokeTransitionToUndeploying,
}

/// <summary>
/// One ordered slot-3 operation. Target-bearing virtual/helper calls carry the
/// freshly observed identity at that exact call site. <paramref name="RawValue"/>
/// carries a virtual result, a complete random result, or exact float32 bits.
/// </summary>
public readonly record struct RetailUnitAIUpdateAction(
    RetailUnitAIUpdateActionKind Kind,
    int? TargetIdentity,
    int? RawValue)
{
    public static RetailUnitAIUpdateAction Simple(
        RetailUnitAIUpdateActionKind kind) => new(kind, null, null);

    public static RetailUnitAIUpdateAction Raw(
        RetailUnitAIUpdateActionKind kind,
        int value) => new(kind, null, value);

    public static RetailUnitAIUpdateAction Target(
        RetailUnitAIUpdateActionKind kind,
        int? target) => new(kind, target, null);

    public static RetailUnitAIUpdateAction FloatBits(
        RetailUnitAIUpdateActionKind kind,
        float value) => new(kind, null, BitConverter.SingleToInt32Bits(value));
}

/// <summary>
/// Final values of the six jitter cells written by the released jittered-aim
/// arm. Offset-bearing names deliberately avoid inventing renderer semantics.
/// </summary>
public readonly record struct RetailUnitAIJitterState(
    float Field48,
    float Field4C,
    float Field50,
    float Field54,
    float Field58,
    float Field5C);

/// <summary>
/// Stage-local observations for one non-faulting slot-3 transaction. Fields
/// read after side-effecting calls are separate so an adapter does not freeze
/// the deletion-aware <c>+0x0C</c> reader or mutable owner/profile state.
/// </summary>
public readonly record struct RetailUnitAIUpdateRequest(
    int OwnerVirtual150Result,
    int? PrimaryTargetIdentity,
    int PrimaryJitterProfile19C,
    int? PrimaryForwardTargetIdentity,
    int ClearTargetProfile128AfterOwnerVirtual,
    int FireGate18,
    int? FireTargetAtSupportIdentity,
    int FireJitterProfile19CAfterSupport,
    int OwnerMode168AfterSupport,
    int? FireTargetAtAimVirtualIdentity,
    int? FireTargetAtForwardAimIdentity,
    float MountedUnitPitch,
    int IdleUndeployProfile108,
    int IdleCadenceFlag110AfterTransition,
    float EventManagerTime,
    bool CallerOverridesDelayWithZero);

/// <summary>Exact deterministic result of one released UnitAI slot-3 pass.</summary>
public sealed record RetailUnitAIUpdatePlan(
    RetailUnitAIUpdateRoute Path,
    double ReturnedDelay,
    uint Event3000DueTimeBits,
    int RandomResultsConsumed,
    RetailUnitAIJitterState? JitterState,
    RetailUnitAIUpdateAction[] Actions)
{
    public float Event3000DueTime =>
        BitConverter.UInt32BitsToSingle(Event3000DueTimeBits);
}

/// <summary>
/// Deterministic transaction boundary for PC retail
/// <c>CUnitAI::Update</c> <c>[0x004FEF40,0x004FF322)</c> and the instruction-
/// shape-identical PC demo body at <c>0x004FEFF0</c>.
/// </summary>
/// <remarks>
/// <para>
/// The caller at <c>0x004FEC60</c> adds this body's x87 return to event-manager
/// time, rounds once to float32, and reuses the incoming scheduled event as
/// event 3000. <see cref="ReturnedDelay"/> is therefore <c>double</c>: the
/// retail Win32 process uses 53-bit x87 precision, and prematurely rounding
/// the mounted-pitch arm changes observable due-time bits.
/// </para>
/// <para>
/// Side-effecting virtual/helper results and random values are caller-captured
/// inputs. The adapter must acquire them at the corresponding action sites;
/// this planner does not consume the shared gameplay RNG before an earlier
/// virtual call has had its own chance to do so.
/// </para>
/// </remarks>
public static class RetailUnitAIUpdateTransaction
{
    public const int EventNumber = 3000;
    public const RetailEventPriority EventPriority = RetailEventPriority.StartOfFrame;
    public const bool ReusesIncomingScheduledEvent = true;

    private static readonly float JitterStep =
        BitConverter.Int32BitsToSingle(unchecked((int)0x3523D70A));
    private static readonly float JitterBase =
        BitConverter.Int32BitsToSingle(unchecked((int)0x3D23D70A));
    private static readonly float UnitStep =
        BitConverter.Int32BitsToSingle(unchecked((int)0x37800000));
    private static readonly float DoubleUnitStep =
        BitConverter.Int32BitsToSingle(unchecked((int)0x38000000));
    private static readonly float MountedPitchBias =
        BitConverter.Int32BitsToSingle(unchecked((int)0x3DCCCCCD));

    public static RetailUnitAIUpdatePlan Plan(
        RetailUnitAIUpdateRequest request,
        IReadOnlyList<int> randomResults)
    {
        ArgumentNullException.ThrowIfNull(randomResults);

        var actions = new List<RetailUnitAIUpdateAction>
        {
            RetailUnitAIUpdateAction.Simple(
                RetailUnitAIUpdateActionKind.InvokeTargetSupportRefreshSlot4),
            RetailUnitAIUpdateAction.Raw(
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual150,
                request.OwnerVirtual150Result),
        };

        if (request.OwnerVirtual150Result != 0 &&
            request.PrimaryTargetIdentity is { } primaryTarget)
        {
            return request.PrimaryJitterProfile19C != 0
                ? PlanPrimaryJitter(request, primaryTarget, randomResults, actions)
                : PlanPrimaryDirect(request, primaryTarget, randomResults, actions);
        }

        if (request.FireGate18 != 0 &&
            request.FireTargetAtSupportIdentity is { } fireTarget)
        {
            RequireRandomCount(randomResults, 0, RetailUnitAIUpdateRoute.FireSupportCadence);
            actions.Add(RetailUnitAIUpdateAction.Target(
                RetailUnitAIUpdateActionKind.InvokeSupportUpdate,
                fireTarget));

            if (request.FireJitterProfile19CAfterSupport == 0 &&
                request.OwnerMode168AfterSupport != 1)
            {
                if (request.FireTargetAtAimVirtualIdentity is not { } aimTarget)
                {
                    throw new ArgumentException(
                        "The released fire-support correction arm dereferences its refreshed reader.",
                        nameof(request));
                }

                actions.Add(RetailUnitAIUpdateAction.Target(
                    RetailUnitAIUpdateActionKind.InvokeTargetVirtual168,
                    aimTarget));
                actions.Add(RetailUnitAIUpdateAction.Target(
                    RetailUnitAIUpdateActionKind.InvokeForwardAim,
                    request.FireTargetAtForwardAimIdentity));
            }

            actions.Add(RetailUnitAIUpdateAction.Simple(
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual158));
            actions.Add(RetailUnitAIUpdateAction.FloatBits(
                RetailUnitAIUpdateActionKind.InvokeMountedUnitPitch,
                request.MountedUnitPitch));

            double delay =
                (double)request.MountedUnitPitch + (double)MountedPitchBias;
            return Finish(
                RetailUnitAIUpdateRoute.FireSupportCadence,
                delay,
                request,
                0,
                null,
                actions);
        }

        RequireRandomCount(randomResults, 1, request.IdleCadenceFlag110AfterTransition != 0
            ? RetailUnitAIUpdateRoute.IdleShortCadence
            : RetailUnitAIUpdateRoute.IdleLongCadence);
        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.WriteOwnerField1ECZero));
        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.WriteOwnerField1E8Zero));
        if (request.IdleUndeployProfile108 != 0)
        {
            actions.Add(RetailUnitAIUpdateAction.Simple(
                RetailUnitAIUpdateActionKind.InvokeTransitionToUndeploying));
        }

        int randomResult = randomResults[0];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            randomResult));
        int sample = ReleasedRemainder65536(randomResult);
        bool shortCadence = request.IdleCadenceFlag110AfterTransition != 0;
        double idleDelay = shortCadence
            ? ((double)sample * (double)UnitStep) + 1.5
            : ((double)sample * (double)DoubleUnitStep) + 3.0;
        return Finish(
            shortCadence
                ? RetailUnitAIUpdateRoute.IdleShortCadence
                : RetailUnitAIUpdateRoute.IdleLongCadence,
            idleDelay,
            request,
            1,
            null,
            actions);
    }

    /// <summary>
    /// Reproduces the caller's final x87 addition and single float32 store.
    /// </summary>
    public static uint ComputeEvent3000DueTimeBits(
        float eventManagerTime,
        double returnedDelay,
        bool callerOverridesDelayWithZero)
    {
        double effectiveDelay = callerOverridesDelayWithZero ? 0.0 : returnedDelay;
        float dueTime = (float)((double)eventManagerTime + effectiveDelay);
        return BitConverter.SingleToUInt32Bits(dueTime);
    }

    private static RetailUnitAIUpdatePlan PlanPrimaryJitter(
        RetailUnitAIUpdateRequest request,
        int primaryTarget,
        IReadOnlyList<int> randomResults,
        List<RetailUnitAIUpdateAction> actions)
    {
        RequireRandomCount(
            randomResults,
            4,
            RetailUnitAIUpdateRoute.PrimaryJitteredAim);
        actions.Add(RetailUnitAIUpdateAction.Target(
            RetailUnitAIUpdateActionKind.InvokeTargetVirtual168,
            primaryTarget));

        int first = randomResults[0];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            first));
        float field48 = JitterMagnitude(first);
        actions.Add(RetailUnitAIUpdateAction.FloatBits(
            RetailUnitAIUpdateActionKind.WriteJitterMagnitude48,
            field48));

        int second = randomResults[1];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            second));
        float field54 = JitterMagnitude(second);
        actions.Add(RetailUnitAIUpdateAction.FloatBits(
            RetailUnitAIUpdateActionKind.WriteJitterMagnitude54,
            field54));

        int third = randomResults[2];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            third));
        if (ReleasedRemainder65536(third) > 32768)
        {
            field48 = -field48;
            actions.Add(RetailUnitAIUpdateAction.FloatBits(
                RetailUnitAIUpdateActionKind.WriteNegatedJitter48,
                field48));
        }

        int fourth = randomResults[3];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            fourth));
        if (ReleasedRemainder65536(fourth) > 32768)
        {
            field54 = -field54;
            actions.Add(RetailUnitAIUpdateAction.FloatBits(
                RetailUnitAIUpdateActionKind.WriteNegatedJitter54,
                field54));
        }

        var jitter = new RetailUnitAIJitterState(
            Field48: field48,
            Field4C: field48,
            Field50: -field48,
            Field54: field54,
            Field58: field54,
            Field5C: -field54);
        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.WriteDerivedJitterBounds));
        actions.Add(RetailUnitAIUpdateAction.Target(
            RetailUnitAIUpdateActionKind.InvokeForwardAim,
            request.PrimaryForwardTargetIdentity));
        AddPrimaryTail(request, actions);

        return Finish(
            RetailUnitAIUpdateRoute.PrimaryJitteredAim,
            0.0,
            request,
            4,
            jitter,
            actions);
    }

    private static RetailUnitAIUpdatePlan PlanPrimaryDirect(
        RetailUnitAIUpdateRequest request,
        int primaryTarget,
        IReadOnlyList<int> randomResults,
        List<RetailUnitAIUpdateAction> actions)
    {
        RequireRandomCount(
            randomResults,
            1,
            RetailUnitAIUpdateRoute.PrimaryDirectAim);
        actions.Add(RetailUnitAIUpdateAction.Target(
            RetailUnitAIUpdateActionKind.InvokeForwardAim,
            primaryTarget));
        int randomResult = randomResults[0];
        actions.Add(RetailUnitAIUpdateAction.Raw(
            RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            randomResult));
        int sample = ReleasedRemainder65536(randomResult);

        // Retail stores this arm to a float local before the common tail.
        float delay = (float)(((double)sample * (double)UnitStep) + 0.5);
        AddPrimaryTail(request, actions);
        return Finish(
            RetailUnitAIUpdateRoute.PrimaryDirectAim,
            delay,
            request,
            1,
            null,
            actions);
    }

    private static void AddPrimaryTail(
        RetailUnitAIUpdateRequest request,
        List<RetailUnitAIUpdateAction> actions)
    {
        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.InvokeOwnerVirtual128));
        if (request.ClearTargetProfile128AfterOwnerVirtual == 0)
        {
            return;
        }

        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.InvokeSetReaderNull));
        actions.Add(RetailUnitAIUpdateAction.Target(
            RetailUnitAIUpdateActionKind.InvokeSupportUpdate,
            null));
        actions.Add(RetailUnitAIUpdateAction.Simple(
            RetailUnitAIUpdateActionKind.WriteRetainedTargetGate10Zero));
    }

    private static RetailUnitAIUpdatePlan Finish(
        RetailUnitAIUpdateRoute path,
        double returnedDelay,
        RetailUnitAIUpdateRequest request,
        int randomResultsConsumed,
        RetailUnitAIJitterState? jitterState,
        List<RetailUnitAIUpdateAction> actions) =>
        new(
            path,
            returnedDelay,
            ComputeEvent3000DueTimeBits(
                request.EventManagerTime,
                returnedDelay,
                request.CallerOverridesDelayWithZero),
            randomResultsConsumed,
            jitterState,
            actions.ToArray());

    private static float JitterMagnitude(int randomResult)
    {
        int sample = ReleasedRemainder65536(randomResult);
        return (float)(((double)sample * (double)JitterStep) + (double)JitterBase);
    }

    private static int ReleasedRemainder65536(int randomResult)
    {
        unchecked
        {
            int masked = randomResult & (int)0x8000FFFFu;
            if (masked < 0)
            {
                masked = ((masked - 1) | (int)0xFFFF0000u) + 1;
            }
            return masked;
        }
    }

    private static void RequireRandomCount(
        IReadOnlyList<int> randomResults,
        int expected,
        RetailUnitAIUpdateRoute path)
    {
        if (randomResults.Count != expected)
        {
            throw new ArgumentException(
                $"{path} consumes exactly {expected} released random result(s).",
                nameof(randomResults));
        }
    }
}
