// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailSpawnerCycleTransactionTests
{
    private static readonly RetailSpawnerCycleConfig Config = new(
        Amount: 3,
        SquadSize: 2,
        MemberDelay: 0.25f,
        SquadDelay: 1.5f,
        Infinite: false);

    [Fact]
    public void AdmissionRejectsEachReleasedGateWithoutMutation()
    {
        RetailSpawnerCycleState ready = State();
        var cases = new (RetailSpawnerCycleState State, RetailSpawnerCycleConfig? Config,
            float Time, bool TypeResolved)[]
        {
            (ready with { Enabled = false }, Config, 2.0f, true),
            (ready with { Busy = true }, Config, 2.0f, true),
            (ready, Config, 1.0f, true),
            (ready with { AdmittedCycleCount = 3 }, Config, 2.0f, true),
            (ready, Config, 2.0f, false),
            (ready, null, 2.0f, true),
            (ready, Config, float.NaN, true),
        };

        foreach ((RetailSpawnerCycleState state, RetailSpawnerCycleConfig? config,
                     float time, bool typeResolved) in cases)
        {
            RetailSpawnerCycleStartPlan plan =
                RetailSpawnerCycleTransaction.PlanStart(
                    state,
                    config,
                    time,
                    typeResolved,
                    squadInitializedAndPublished: true);

            Assert.False(plan.DoSpawnResult);
            Assert.Equal(state, plan.State);
            Assert.Empty(plan.Actions);
        }
    }

    [Fact]
    public void PublishedEmptySquadPrecedesCycleCommitAndImmediateWave()
    {
        RetailSpawnerCycleStartPlan plan =
            RetailSpawnerCycleTransaction.PlanStart(
                State(successfulMembers: 9),
                Config,
                currentTime: 2.0f,
                spawnTypeResolved: true,
                squadInitializedAndPublished: true);

        Assert.True(plan.DoSpawnResult);
        Assert.True(plan.State.Busy);
        Assert.True(plan.State.HasSquadReader);
        Assert.Equal(1, plan.State.AdmittedCycleCount);
        Assert.Equal(0, plan.State.SuccessfulMemberCount);
        Assert.Equal(
            [
                Action(RetailSpawnerCycleActionKind.RequestSquadConstruction),
                Action(RetailSpawnerCycleActionKind.AssignSquadReader),
                Action(RetailSpawnerCycleActionKind.SetSquadBuildFlag),
                Action(RetailSpawnerCycleActionKind.InitializeAndPublishEmptySquadTail),
                Action(RetailSpawnerCycleActionKind.ResetSuccessfulMemberCount),
                Action(RetailSpawnerCycleActionKind.SetBusy),
                Action(RetailSpawnerCycleActionKind.IncrementAdmittedCycleCount),
                Action(RetailSpawnerCycleActionKind.InvokeImmediateMemberWave),
            ],
            plan.Actions);
    }

    [Fact]
    public void NullSquadStillConsumesAmountSlotAndInvokesFirstMemberWave()
    {
        RetailSpawnerCycleStartPlan plan =
            RetailSpawnerCycleTransaction.PlanStart(
                State(),
                Config,
                currentTime: 2.0f,
                spawnTypeResolved: true,
                squadInitializedAndPublished: false);

        Assert.True(plan.DoSpawnResult);
        Assert.True(plan.State.Busy);
        Assert.False(plan.State.HasSquadReader);
        Assert.Equal(1, plan.State.AdmittedCycleCount);
        Assert.DoesNotContain(
            plan.Actions,
            action => action.Kind is
                RetailSpawnerCycleActionKind.SetSquadBuildFlag or
                RetailSpawnerCycleActionKind.InitializeAndPublishEmptySquadTail);
        Assert.Equal(
            RetailSpawnerCycleActionKind.InvokeImmediateMemberWave,
            plan.Actions[^1].Kind);
    }

    [Fact]
    public void ClearanceAndFactoryFailuresRetryWithoutAdvancingCycle()
    {
        RetailSpawnerCycleState busy = State(busy: true, hasSquad: true);

        RetailSpawnerMemberWavePlan blocked =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                busy, Config, 10.0f, positionIsClear: false,
                memberFactoryReturnedObject: true);
        RetailSpawnerMemberWavePlan allocationFailure =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                busy, Config, 10.0f, positionIsClear: true,
                memberFactoryReturnedObject: false);

        Assert.Equal(busy, blocked.State);
        Assert.Equal(busy, allocationFailure.State);
        Assert.Equal(
            [
                Action(RetailSpawnerCycleActionKind.ResolveMemberTransformAndClearance),
                Action(RetailSpawnerCycleActionKind.ScheduleEvent3000, 10.25f),
            ],
            blocked.Actions);
        Assert.Equal(
            [
                Action(RetailSpawnerCycleActionKind.ResolveMemberTransformAndClearance),
                Action(RetailSpawnerCycleActionKind.RequestMemberConstruction),
                Action(RetailSpawnerCycleActionKind.ScheduleEvent3000, 10.25f),
            ],
            allocationFailure.Actions);
    }

    [Fact]
    public void PartialMemberSuccessAttachesThenCountsAndSchedulesNextWave()
    {
        RetailSpawnerCycleState busy = State(busy: true, hasSquad: true);
        RetailSpawnerMemberWavePlan plan =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                busy, Config, 10.0f, positionIsClear: true,
                memberFactoryReturnedObject: true);

        Assert.True(plan.State.Busy);
        Assert.Equal(1, plan.State.SuccessfulMemberCount);
        Assert.Equal(2, plan.State.NextTransformOrdinal);
        Assert.Equal(
            [
                Action(RetailSpawnerCycleActionKind.ResolveMemberTransformAndClearance),
                Action(RetailSpawnerCycleActionKind.RequestMemberConstruction),
                Action(RetailSpawnerCycleActionKind.IncrementTransformOrdinal),
                Action(RetailSpawnerCycleActionKind.InitializeMember),
                Action(RetailSpawnerCycleActionKind.ApplyMemberSeekCooldownState),
                Action(RetailSpawnerCycleActionKind.AttachMemberToSquad),
                Action(RetailSpawnerCycleActionKind.IncrementSuccessfulMemberCount),
                Action(RetailSpawnerCycleActionKind.ScheduleEvent3000, 10.25f),
            ],
            plan.Actions);
    }

    [Fact]
    public void FinalMemberClearsSquadReaderBeforeBusyAndNextCycleTime()
    {
        RetailSpawnerCycleState busy = State(
            busy: true,
            successfulMembers: 1,
            hasSquad: true);
        RetailSpawnerMemberWavePlan plan =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                busy, Config, 10.0f, positionIsClear: true,
                memberFactoryReturnedObject: true);

        Assert.False(plan.State.Busy);
        Assert.False(plan.State.HasSquadReader);
        Assert.Equal(2, plan.State.SuccessfulMemberCount);
        Assert.Equal(11.5f, plan.State.NextCycleTime);
        Assert.Equal(
            [
                Action(RetailSpawnerCycleActionKind.ResolveMemberTransformAndClearance),
                Action(RetailSpawnerCycleActionKind.RequestMemberConstruction),
                Action(RetailSpawnerCycleActionKind.IncrementTransformOrdinal),
                Action(RetailSpawnerCycleActionKind.InitializeMember),
                Action(RetailSpawnerCycleActionKind.ApplyMemberSeekCooldownState),
                Action(RetailSpawnerCycleActionKind.AttachMemberToSquad),
                Action(RetailSpawnerCycleActionKind.IncrementSuccessfulMemberCount),
                Action(RetailSpawnerCycleActionKind.ClearSquadBuildFlag),
                Action(RetailSpawnerCycleActionKind.ReleaseSquadReader),
                Action(RetailSpawnerCycleActionKind.ClearBusy),
                Action(RetailSpawnerCycleActionKind.SetNextCycleTime, 11.5f),
            ],
            plan.Actions);
    }

    [Fact]
    public void NullSquadCanCompleteAndDisableDoesNotCancelBusyWave()
    {
        RetailSpawnerCycleState busy = State(
            enabled: false,
            busy: true,
            hasSquad: false);
        RetailSpawnerCycleConfig sizeOne = Config with { SquadSize = 1 };
        RetailSpawnerMemberWavePlan plan =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                busy, sizeOne, 10.0f, positionIsClear: true,
                memberFactoryReturnedObject: true);

        Assert.True(plan.BusyEntryObserved);
        Assert.False(plan.State.Busy);
        Assert.DoesNotContain(
            plan.Actions,
            action => action.Kind is
                RetailSpawnerCycleActionKind.AttachMemberToSquad or
                RetailSpawnerCycleActionKind.ClearSquadBuildFlag);
        Assert.Contains(
            Action(RetailSpawnerCycleActionKind.ReleaseSquadReader),
            plan.Actions);
    }

    [Fact]
    public void EventAfterCompletionIsInertAndInfiniteIgnoresAmount()
    {
        RetailSpawnerCycleState state = State(admittedCycles: 99);
        RetailSpawnerMemberWavePlan wave =
            RetailSpawnerCycleTransaction.PlanMemberWave(
                state, Config, 10.0f, positionIsClear: true,
                memberFactoryReturnedObject: true);

        Assert.False(wave.BusyEntryObserved);
        Assert.Equal(state, wave.State);
        Assert.Empty(wave.Actions);
        Assert.True(RetailSpawnerCycleTransaction.IsComplete(state, Config));
        Assert.False(RetailSpawnerCycleTransaction.IsComplete(
            state,
            Config with { Infinite = true }));
        Assert.True(RetailSpawnerCycleTransaction.IsComplete(state, null));
    }

    private static RetailSpawnerCycleState State(
        bool enabled = true,
        bool busy = false,
        int admittedCycles = 0,
        int successfulMembers = 0,
        bool hasSquad = false) => new(
            Enabled: enabled,
            Busy: busy,
            AdmittedCycleCount: admittedCycles,
            SuccessfulMemberCount: successfulMembers,
            NextCycleTime: 1.0f,
            NextTransformOrdinal: 1,
            HasSquadReader: hasSquad);

    private static RetailSpawnerCycleAction Action(
        RetailSpawnerCycleActionKind kind,
        float? time = null) => new(kind, time);
}
