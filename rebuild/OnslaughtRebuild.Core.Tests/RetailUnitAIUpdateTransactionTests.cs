// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitAIUpdateTransactionTests
{
    [Fact]
    public void JitteredPrimaryArmConsumesFourDrawsAndKeepsStrictHalfSignGate()
    {
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(
                ownerGate150: 1,
                primaryTarget: 7,
                primaryJitter19C: 1,
                primaryForwardTarget: 8,
                eventTime: 12.5f),
            [0, 65535, 32768, 32769]);

        Assert.Equal(RetailUnitAIUpdateRoute.PrimaryJitteredAim, plan.Path);
        Assert.Equal(0.0, plan.ReturnedDelay);
        Assert.Equal(4, plan.RandomResultsConsumed);
        Assert.Equal(BitConverter.SingleToUInt32Bits(12.5f), plan.Event3000DueTimeBits);

        RetailUnitAIJitterState jitter = Assert.IsType<RetailUnitAIJitterState>(
            plan.JitterState);
        Assert.Equal(0x3D23D70A, BitConverter.SingleToInt32Bits(jitter.Field48));
        Assert.Equal(jitter.Field48, jitter.Field4C);
        Assert.Equal(-jitter.Field48, jitter.Field50);
        Assert.True(jitter.Field54 < 0.0f);
        Assert.Equal(jitter.Field54, jitter.Field58);
        Assert.Equal(-jitter.Field54, jitter.Field5C);

        Assert.Equal(
            [
                RetailUnitAIUpdateActionKind.InvokeTargetSupportRefreshSlot4,
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual150,
                RetailUnitAIUpdateActionKind.InvokeTargetVirtual168,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
                RetailUnitAIUpdateActionKind.WriteJitterMagnitude48,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
                RetailUnitAIUpdateActionKind.WriteJitterMagnitude54,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
                RetailUnitAIUpdateActionKind.WriteNegatedJitter54,
                RetailUnitAIUpdateActionKind.WriteDerivedJitterBounds,
                RetailUnitAIUpdateActionKind.InvokeForwardAim,
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual128,
            ],
            plan.Actions.Select(action => action.Kind));
        Assert.Equal(7, plan.Actions[2].TargetIdentity);
        Assert.Equal(8, plan.Actions[11].TargetIdentity);
    }

    [Fact]
    public void DirectPrimaryArmRoundsDelayThenRunsClearTailInReleasedOrder()
    {
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(
                ownerGate150: 1,
                primaryTarget: 19,
                clearProfile128: 1,
                eventTime: 10.0f),
            [0x12345678]);

        float expectedDelay = 0.5f + (0x5678 / 65536.0f);
        Assert.Equal(RetailUnitAIUpdateRoute.PrimaryDirectAim, plan.Path);
        Assert.Equal((double)expectedDelay, plan.ReturnedDelay);
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(10.0f + expectedDelay),
            plan.Event3000DueTimeBits);
        Assert.Equal(
            [
                RetailUnitAIUpdateActionKind.InvokeTargetSupportRefreshSlot4,
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual150,
                RetailUnitAIUpdateActionKind.InvokeForwardAim,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual128,
                RetailUnitAIUpdateActionKind.InvokeSetReaderNull,
                RetailUnitAIUpdateActionKind.InvokeSupportUpdate,
                RetailUnitAIUpdateActionKind.WriteRetainedTargetGate10Zero,
            ],
            plan.Actions.Select(action => action.Kind));
    }

    [Fact]
    public void FireCadenceUsesFreshReadersAndOneFinalX87Rounding()
    {
        float eventTime = BitConverter.Int32BitsToSingle(unchecked((int)0x4192430B));
        float pitch = BitConverter.Int32BitsToSingle(unchecked((int)0x3F2924B6));
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(
                ownerGate150: 0,
                fireGate18: 1,
                fireSupportTarget: 20,
                fireAimTarget: 21,
                fireForwardTarget: 22,
                mountedPitch: pitch,
                eventTime: eventTime),
            []);

        Assert.Equal(RetailUnitAIUpdateRoute.FireSupportCadence, plan.Path);
        Assert.Equal(0x419858FDu, plan.Event3000DueTimeBits);
        Assert.NotEqual(
            BitConverter.SingleToUInt32Bits(eventTime + (pitch + 0.1f)),
            plan.Event3000DueTimeBits);
        Assert.Equal(
            [20, 21, 22],
            plan.Actions
                .Where(action => action.TargetIdentity.HasValue)
                .Select(action => action.TargetIdentity!.Value));
        Assert.Equal(0, plan.RandomResultsConsumed);
    }

    [Fact]
    public void FireCadenceSkipsAimRefreshForJitterProfileOrOwnerModeOne()
    {
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(
                fireGate18: 2,
                fireSupportTarget: 30,
                fireJitter19C: 1,
                ownerMode168: 1,
                mountedPitch: -0.25f),
            []);

        Assert.DoesNotContain(
            plan.Actions,
            action => action.Kind == RetailUnitAIUpdateActionKind.InvokeTargetVirtual168);
        Assert.DoesNotContain(
            plan.Actions,
            action => action.Kind == RetailUnitAIUpdateActionKind.InvokeForwardAim);
        Assert.Equal(-0.25 + (double)BitConverter.Int32BitsToSingle(0x3DCCCCCD),
            plan.ReturnedDelay);
    }

    [Fact]
    public void IdleShortCadenceWritesOwnerStateTransitionsThenDraws()
    {
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(
                idleUndeploy108: 1,
                idleCadence110: 1,
                eventTime: 4.0f,
                callerOverridesDelay: true),
            [65535]);

        Assert.Equal(RetailUnitAIUpdateRoute.IdleShortCadence, plan.Path);
        Assert.Equal(1.5 + (65535.0 / 65536.0), plan.ReturnedDelay);
        Assert.Equal(BitConverter.SingleToUInt32Bits(4.0f), plan.Event3000DueTimeBits);
        Assert.Equal(
            [
                RetailUnitAIUpdateActionKind.InvokeTargetSupportRefreshSlot4,
                RetailUnitAIUpdateActionKind.InvokeOwnerVirtual150,
                RetailUnitAIUpdateActionKind.WriteOwnerField1ECZero,
                RetailUnitAIUpdateActionKind.WriteOwnerField1E8Zero,
                RetailUnitAIUpdateActionKind.InvokeTransitionToUndeploying,
                RetailUnitAIUpdateActionKind.ConsumeReleasedRandom,
            ],
            plan.Actions.Select(action => action.Kind));
    }

    [Fact]
    public void IdleLongCadenceUsesTwiceTheRandomStep()
    {
        RetailUnitAIUpdatePlan plan = RetailUnitAIUpdateTransaction.Plan(
            Request(eventTime: 2.0f),
            [32768]);

        Assert.Equal(RetailUnitAIUpdateRoute.IdleLongCadence, plan.Path);
        Assert.Equal(4.0, plan.ReturnedDelay);
        Assert.Equal(BitConverter.SingleToUInt32Bits(6.0f), plan.Event3000DueTimeBits);
    }

    private static RetailUnitAIUpdateRequest Request(
        int ownerGate150 = 0,
        int? primaryTarget = null,
        int primaryJitter19C = 0,
        int? primaryForwardTarget = null,
        int clearProfile128 = 0,
        int fireGate18 = 0,
        int? fireSupportTarget = null,
        int fireJitter19C = 0,
        int ownerMode168 = 0,
        int? fireAimTarget = 2,
        int? fireForwardTarget = 2,
        float mountedPitch = 0.0f,
        int idleUndeploy108 = 0,
        int idleCadence110 = 0,
        float eventTime = 0.0f,
        bool callerOverridesDelay = false) => new(
            OwnerVirtual150Result: ownerGate150,
            PrimaryTargetIdentity: primaryTarget,
            PrimaryJitterProfile19C: primaryJitter19C,
            PrimaryForwardTargetIdentity: primaryForwardTarget,
            ClearTargetProfile128AfterOwnerVirtual: clearProfile128,
            FireGate18: fireGate18,
            FireTargetAtSupportIdentity: fireSupportTarget,
            FireJitterProfile19CAfterSupport: fireJitter19C,
            OwnerMode168AfterSupport: ownerMode168,
            FireTargetAtAimVirtualIdentity: fireAimTarget,
            FireTargetAtForwardAimIdentity: fireForwardTarget,
            MountedUnitPitch: mountedPitch,
            IdleUndeployProfile108: idleUndeploy108,
            IdleCadenceFlag110AfterTransition: idleCadence110,
            EventManagerTime: eventTime,
            CallerOverridesDelayWithZero: callerOverridesDelay);
}
