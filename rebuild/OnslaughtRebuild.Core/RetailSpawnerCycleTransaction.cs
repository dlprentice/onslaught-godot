// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Released fields that govern one spawner's admitted squad cycles and member
/// waves.
/// </summary>
public sealed record RetailSpawnerCycleConfig(
    int Amount,
    int SquadSize,
    float MemberDelay,
    float SquadDelay,
    bool Infinite);

/// <summary>
/// Deterministic portion of a released spawner's current cycle state. The
/// runtime adapter owns the actual squad reader, factories, initialization,
/// world-list publication, and event delivery.
/// </summary>
public readonly record struct RetailSpawnerCycleState(
    bool Enabled,
    bool Busy,
    int AdmittedCycleCount,
    int SuccessfulMemberCount,
    float NextCycleTime,
    int NextTransformOrdinal,
    bool HasSquadReader);

/// <summary>
/// One adapter-visible operation in released execution order.
/// </summary>
public enum RetailSpawnerCycleActionKind
{
    RequestSquadConstruction,
    AssignSquadReader,
    SetSquadBuildFlag,
    InitializeAndPublishEmptySquadTail,
    ResetSuccessfulMemberCount,
    SetBusy,
    IncrementAdmittedCycleCount,
    InvokeImmediateMemberWave,
    ResolveMemberTransformAndClearance,
    RequestMemberConstruction,
    IncrementTransformOrdinal,
    InitializeMember,
    ApplyMemberSeekCooldownState,
    AttachMemberToSquad,
    IncrementSuccessfulMemberCount,
    ClearSquadBuildFlag,
    ReleaseSquadReader,
    ClearBusy,
    SetNextCycleTime,
    ScheduleEvent3000,
}

/// <summary>
/// One ordered spawner action. <paramref name="TimeValue"/> is populated only
/// for next-cycle-time writes and event scheduling.
/// </summary>
public readonly record struct RetailSpawnerCycleAction(
    RetailSpawnerCycleActionKind Kind,
    float? TimeValue = null);

/// <summary>
/// Result of the synchronous released <c>DoSpawn</c> start prefix, ending at
/// its immediate member-wave invocation.
/// </summary>
public sealed record RetailSpawnerCycleStartPlan(
    bool DoSpawnResult,
    RetailSpawnerCycleState State,
    RetailSpawnerCycleAction[] Actions);

/// <summary>
/// Result of one immediate or event-3000 member-wave entry.
/// </summary>
public sealed record RetailSpawnerMemberWavePlan(
    bool BusyEntryObserved,
    RetailSpawnerCycleState State,
    RetailSpawnerCycleAction[] Actions);

/// <summary>
/// Deterministic transaction boundary for PC retail
/// <c>CSpawnerThng__DoSpawn</c> at <c>0x004E3C60</c>, its member wave at
/// <c>0x004E3F90</c>, and their released-family correspondents.
/// </summary>
/// <remarks>
/// Allocation and virtual initialization outcomes are supplied by the runtime
/// adapter. A true squad-initialized-and-published input means the adapter has
/// completed initialization and the empty squad's live tail
/// publication; retail performs that publication before committing the cycle.
/// A missing authored unit definition is an invariant/fault route in retail
/// and is deliberately not normalized into a false result here.
/// </remarks>
public static class RetailSpawnerCycleTransaction
{
    /// <summary>
    /// Plans one normally returning <c>DoSpawn</c> start prefix. The returned
    /// state is the state passed into the immediate wave; apply <see
    /// cref="PlanMemberWave"/> before treating it as post-call state. Passing
    /// a false squad outcome reproduces retail's null-factory route: the
    /// amount slot is still consumed and the first member wave still runs.
    /// </summary>
    public static RetailSpawnerCycleStartPlan PlanStart(
        RetailSpawnerCycleState state,
        RetailSpawnerCycleConfig? config,
        float currentTime,
        bool spawnTypeResolved,
        bool squadInitializedAndPublished)
    {
        if (!state.Enabled ||
            state.Busy ||
            !(currentTime > state.NextCycleTime) ||
            IsComplete(state, config) ||
            !spawnTypeResolved ||
            config is null)
        {
            return new RetailSpawnerCycleStartPlan(false, state, []);
        }

        var actions = new List<RetailSpawnerCycleAction>
        {
            new(RetailSpawnerCycleActionKind.RequestSquadConstruction),
            new(RetailSpawnerCycleActionKind.AssignSquadReader),
        };
        if (squadInitializedAndPublished)
        {
            actions.Add(new(RetailSpawnerCycleActionKind.SetSquadBuildFlag));
            actions.Add(new(
                RetailSpawnerCycleActionKind.InitializeAndPublishEmptySquadTail));
        }

        actions.Add(new(RetailSpawnerCycleActionKind.ResetSuccessfulMemberCount));
        actions.Add(new(RetailSpawnerCycleActionKind.SetBusy));
        actions.Add(new(RetailSpawnerCycleActionKind.IncrementAdmittedCycleCount));
        actions.Add(new(RetailSpawnerCycleActionKind.InvokeImmediateMemberWave));

        return new RetailSpawnerCycleStartPlan(
            true,
            state with
            {
                Busy = true,
                AdmittedCycleCount = unchecked(state.AdmittedCycleCount + 1),
                SuccessfulMemberCount = 0,
                HasSquadReader = squadInitializedAndPublished,
            },
            actions.ToArray());
    }

    /// <summary>
    /// Plans one member-wave entry after the adapter has resolved clearance
    /// and, when clear, the member-factory result. A true member outcome means
    /// its initializer returns normally. Failed clearance or allocation keeps
    /// the same cycle busy and schedules event 3000 without advancing counters.
    /// </summary>
    public static RetailSpawnerMemberWavePlan PlanMemberWave(
        RetailSpawnerCycleState state,
        RetailSpawnerCycleConfig config,
        float currentTime,
        bool positionIsClear,
        bool memberFactoryReturnedObject)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!state.Busy)
        {
            return new RetailSpawnerMemberWavePlan(false, state, []);
        }

        var actions = new List<RetailSpawnerCycleAction>
        {
            new(RetailSpawnerCycleActionKind.ResolveMemberTransformAndClearance),
        };
        if (!positionIsClear)
        {
            return Retry(state, config, currentTime, actions);
        }

        actions.Add(new(RetailSpawnerCycleActionKind.RequestMemberConstruction));
        if (!memberFactoryReturnedObject)
        {
            return Retry(state, config, currentTime, actions);
        }

        actions.Add(new(RetailSpawnerCycleActionKind.IncrementTransformOrdinal));
        actions.Add(new(RetailSpawnerCycleActionKind.InitializeMember));
        actions.Add(new(RetailSpawnerCycleActionKind.ApplyMemberSeekCooldownState));
        if (state.HasSquadReader)
        {
            actions.Add(new(RetailSpawnerCycleActionKind.AttachMemberToSquad));
        }
        actions.Add(new(RetailSpawnerCycleActionKind.IncrementSuccessfulMemberCount));

        int successfulMemberCount = unchecked(state.SuccessfulMemberCount + 1);
        RetailSpawnerCycleState advanced = state with
        {
            SuccessfulMemberCount = successfulMemberCount,
            NextTransformOrdinal = unchecked(state.NextTransformOrdinal + 1),
        };
        if (successfulMemberCount < config.SquadSize)
        {
            return Retry(advanced, config, currentTime, actions);
        }

        if (state.HasSquadReader)
        {
            actions.Add(new(RetailSpawnerCycleActionKind.ClearSquadBuildFlag));
        }
        actions.Add(new(RetailSpawnerCycleActionKind.ReleaseSquadReader));
        actions.Add(new(RetailSpawnerCycleActionKind.ClearBusy));
        float nextCycleTime = currentTime + config.SquadDelay;
        actions.Add(new(
            RetailSpawnerCycleActionKind.SetNextCycleTime,
            nextCycleTime));
        return new RetailSpawnerMemberWavePlan(
            true,
            advanced with
            {
                Busy = false,
                NextCycleTime = nextCycleTime,
                HasSquadReader = false,
            },
            actions.ToArray());
    }

    /// <summary>
    /// Released finite/infinite completion predicate. A missing config is
    /// complete; infinite mode is never complete through the amount counter.
    /// </summary>
    public static bool IsComplete(
        RetailSpawnerCycleState state,
        RetailSpawnerCycleConfig? config) =>
        config is null ||
        (!config.Infinite && state.AdmittedCycleCount >= config.Amount);

    private static RetailSpawnerMemberWavePlan Retry(
        RetailSpawnerCycleState state,
        RetailSpawnerCycleConfig config,
        float currentTime,
        List<RetailSpawnerCycleAction> actions)
    {
        actions.Add(new(
            RetailSpawnerCycleActionKind.ScheduleEvent3000,
            currentTime + config.MemberDelay));
        return new RetailSpawnerMemberWavePlan(true, state, actions.ToArray());
    }
}
