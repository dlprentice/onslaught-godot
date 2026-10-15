// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Core.Tests;

public sealed class ThingActorBaseStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetailPoses_RotationAgreesWithConvertedOffset(bool pitch)
    {
        const int one = 0x3f800000;
        const int minusOne = unchecked((int)0xbf800000);
        // Exact quarter turns isolate the axis conversion from Euler/trig math.
        var basis = pitch
            ? new Level100FloatBasis3Bits(one, 0, 0, 0, 0, minusOne, 0, one, 0)
            : new Level100FloatBasis3Bits(0, 0, one, 0, one, 0, minusOne, 0, 0);
        var origin = new Level100FloatVector3Bits(
            BitConverter.SingleToInt32Bits(288.6875f),
            BitConverter.SingleToInt32Bits(243.25f),
            BitConverter.SingleToInt32Bits(-10f));
        var raw = new RetailActorPoseSnapshot(origin, basis);
        var state = new ThingActorBaseState(new(SimVector3.Zero, IdentityBasis()),
            SimVector3.Zero, SimVector3.Zero, 0);
        state.BeginRetailInitialization(raw, raw, 0);

        // Local retail offset (2,3,5) becomes Core (2,-5,3). Rotating it in
        // retail first gives (2,-5,3) for pitch or (5,3,-2) for roll.
        SimVector3 expected = pitch ? new(2000, -3000, -5000) : new(5000, 2000, 3000);
        foreach (ThingActorPoseSnapshot pose in new[] { state.Snapshot.CurrentPose, state.Snapshot.OldPose })
        {
            Level100FloatBasis3Bits b = pose.BasisFloatBits;
            static float F(int bits) => BitConverter.Int32BitsToSingle(bits);
            var actual = new SimVector3(
                (int)(F(b.Row0X) * 2000 - F(b.Row0Y) * 5000 + F(b.Row0Z) * 3000),
                (int)(F(b.Row1X) * 2000 - F(b.Row1Y) * 5000 + F(b.Row1Z) * 3000),
                (int)(F(b.Row2X) * 2000 - F(b.Row2Y) * 5000 + F(b.Row2Z) * 3000));
            Assert.Equal(expected, actual);
        }
        Assert.Equal(raw, state.Snapshot.RetailPoses!.Current);

        var target = new TargetSnapshot(new(1), 1, "Target Drone", "m_FA_F24_training.msh.aya",
            SimVector2.Zero, 1000, true,
            new(SimVector3.Zero, state.Snapshot.CurrentPose.BasisFloatBits,
                SimVector3.Zero, SimVector3.Zero));
        Level100RenderBasis3 render = Level100TargetPresentation.Project(target).Basis;
        // The mesh converter plus its -90-degree X child rotation maps the
        // same local retail point to Godot (2,-5,-3). No renderer sign repair.
        var renderedPoint = new Level100RenderVector3(
            render.XAxis.X * 2 - render.YAxis.X * 5 - render.ZAxis.X * 3,
            render.XAxis.Y * 2 - render.YAxis.Y * 5 - render.ZAxis.Y * 3,
            render.XAxis.Z * 2 - render.YAxis.Z * 5 - render.ZAxis.Z * 3);
        Assert.Equal(pitch ? new(2, -3, 5) : new Level100RenderVector3(5, 2, -3), renderedPoint);
    }

    [Fact]
    public void RetailPoses_ProjectionPreservesSignedPermutationWords()
    {
        const int negativeZero = int.MinValue;
        var raw = new RetailActorPoseSnapshot(new(0x43880000, 0x43700000, unchecked((int)0xc1200000)),
            IdentityBasis() with { Row0Y = negativeZero, Row1X = negativeZero,
                Row1Z = negativeZero, Row2Y = negativeZero });
        var state = new ThingActorBaseState(new(SimVector3.Zero, IdentityBasis()),
            SimVector3.Zero, SimVector3.Zero, 0);
        state.BeginRetailInitialization(raw, raw, 0);
        var expected = IdentityBasis() with { Row0Y = negativeZero, Row0Z = negativeZero,
            Row1X = negativeZero, Row2X = negativeZero };
        Assert.Equal(expected, state.Snapshot.CurrentPose.BasisFloatBits);
        Assert.Equal(expected, state.Snapshot.OldPose.BasisFloatBits);
        Assert.Same(raw, state.Snapshot.RetailPoses!.Current);
    }

    [Fact]
    public void RetailPoses_PreserveExactWordsAndPositionOnlyOperations()
    {
        var state = new ThingActorBaseState(new(SimVector3.Zero, IdentityBasis()),
            SimVector3.Zero, SimVector3.Zero, 0);
        var current = new RetailActorPoseSnapshot(new(0x43880001, 0x43700001, unchecked((int)0xc1200001)),
            IdentityBasis() with { Row0Y = int.MinValue });
        var old = current with { BasisFloatBits = IdentityBasis() with { Row2X = int.MinValue } };
        state.BeginRetailInitialization(current, old, 0x40100130);
        state.SetRetailMotion(0, 1);
        Assert.Same(current, state.Snapshot.RetailPoses!.Current);
        Assert.Same(old, state.Snapshot.RetailPoses!.Old);
        Assert.Equal(new SimVector3(-16687, 0, -3250), state.Snapshot.CurrentPose.PositionMillimeters);
        Assert.Equal(new RetailActorMotionSnapshot(0, 1), state.Snapshot.RetailMotion);
        var next = current.PositionFloatBits with { Z = unchecked((int)0xc1300001) };
        state.SetRetailPosition(next);
        Assert.Equal(old, state.Snapshot.RetailPoses.Old);
        state.CopyRetailPositionToOld();
        Assert.Equal(next, state.Snapshot.RetailPoses.Old.PositionFloatBits);
        Assert.Equal(old.BasisFloatBits, state.Snapshot.RetailPoses.Old.BasisFloatBits);
        state.TeleportRetailPosition(current.PositionFloatBits);
        Assert.Equal(current, state.Snapshot.RetailPoses.Current);
        Assert.Equal(old, state.Snapshot.RetailPoses.Old);
        Assert.Throws<NotSupportedException>(() => new ThingActorBaseState(state.Snapshot));
    }

    [Fact]
    public void RetailPoses_RejectUnprojectableAndLegacyMutationsAtomically()
    {
        var state = new ThingActorBaseState(new(SimVector3.Zero, IdentityBasis()),
            SimVector3.Zero, SimVector3.Zero, 0);
        var valid = new RetailActorPoseSnapshot(new(0x43880000, 0x43700000, unchecked((int)0xc1200000)),
            IdentityBasis());
        var overflowing = valid with { PositionFloatBits = valid.PositionFloatBits with
            { X = BitConverter.SingleToInt32Bits(3_000_000f) } };
        var fresh = state.Snapshot;
        Assert.Throws<OverflowException>(() => state.BeginRetailInitialization(valid, overflowing, 0));
        Assert.Equal(fresh, state.Snapshot);
        state.BeginRetailInitialization(valid, valid, 0);
        var before = state.Snapshot;
        Assert.Throws<OverflowException>(() => state.SetRetailPosition(overflowing.PositionFloatBits));
        Assert.Throws<OverflowException>(() => state.TeleportRetailPosition(overflowing.PositionFloatBits));
        Assert.Throws<ArgumentException>(() => state.SetRetailPosition(valid.PositionFloatBits with { X = 0x7fc00000 }));
        Assert.Throws<NotSupportedException>(() => state.ResetPose(fresh.CurrentPose));
        Assert.Throws<NotSupportedException>(() => state.AdvancePose(fresh.CurrentPose));
        Assert.Throws<NotSupportedException>(() => state.SetVelocity(new(1, 2, 3)));
        Assert.Equal(before, state.Snapshot);
    }

    [Fact]
    public void Visibility_UsesTheReleasedInvisibleFlagAndIsIdempotent()
    {
        ThingActorBaseState state = CreateState();

        Assert.False(state.Snapshot.IsInvisible);
        Assert.Equal(ThingActorFlags.None, state.Snapshot.Flags);

        state.MakeInvisible();
        state.MakeInvisible();

        Assert.True(state.Snapshot.IsInvisible);
        Assert.Equal(ThingActorFlags.Invisible, state.Snapshot.Flags);

        state.MakeVisible();
        state.MakeVisible();

        Assert.False(state.Snapshot.IsInvisible);
        Assert.Equal(ThingActorFlags.None, state.Snapshot.Flags);
    }

    [Fact]
    public void DyingAndShutdown_FollowTheReleasedOneShotFlagOrdering()
    {
        ThingActorBaseState dying = CreateState();

        Assert.True(dying.StartDieProcess());
        Assert.False(dying.StartDieProcess());
        Assert.True(dying.Snapshot.IsDying);
        Assert.True(dying.Snapshot.IsShuttingDown);
        Assert.Equal(
            ThingActorFlags.Dying | ThingActorFlags.DeclaredShutdown,
            dying.Snapshot.Flags);

        ThingActorBaseState shutdownOnly = CreateState();
        Assert.True(shutdownOnly.DeclareShutdown());
        Assert.False(shutdownOnly.DeclareShutdown());
        Assert.False(shutdownOnly.Snapshot.IsDying);
        Assert.True(shutdownOnly.Snapshot.IsShuttingDown);
    }

    [Fact]
    public void PoseTransition_CapturesOldPoseAndResetCollapsesInterpolation()
    {
        ThingActorBaseState state = CreateState();
        var next = new ThingActorPoseSnapshot(
            new SimVector3(15, 18, 41),
            IdentityBasis());

        state.AdvancePose(next);

        Assert.Equal(new SimVector3(10, 20, 30), state.Snapshot.OldPose.PositionMillimeters);
        Assert.Equal(next, state.Snapshot.CurrentPose);
        Assert.Equal(new SimVector3(5, -2, 11), state.Snapshot.LocalLastFrameMovement);

        var teleported = new ThingActorPoseSnapshot(
            new SimVector3(-1, -2, -3),
            IdentityBasis());
        state.ResetPose(teleported);

        Assert.Equal(teleported, state.Snapshot.OldPose);
        Assert.Equal(teleported, state.Snapshot.CurrentPose);
        Assert.Equal(SimVector3.Zero, state.Snapshot.LocalLastFrameMovement);
    }

    [Fact]
    public void Velocity_UpdateAddAndStopPreserveUnrelatedPoseAndAngularState()
    {
        ThingActorBaseState state = CreateState();
        ThingActorPoseSnapshot pose = state.Snapshot.CurrentPose;

        state.SetVelocity(new SimVector3(10, 20, 30));
        state.AddVelocity(new SimVector3(-2, 4, 8));

        Assert.Equal(new SimVector3(8, 24, 38), state.Snapshot.Velocity);

        state.Stop();

        Assert.Equal(SimVector3.Zero, state.Snapshot.Velocity);
        Assert.Equal(new SimVector3(4, 5, 6), state.Snapshot.AngularVelocity);
        Assert.Equal(pose, state.Snapshot.CurrentPose);
        Assert.Equal(pose, state.Snapshot.OldPose);
    }

    [Fact]
    public void ThingTypeMask_ComposesActorLineageAndReplacesTheSpecificMask()
    {
        ThingActorBaseState state = CreateState();

        Assert.Equal(0x80000043u, state.Snapshot.ThingTypeMask);
        Assert.True(state.Snapshot.IsA(ThingActorTypeMasks.Thing));
        Assert.True(state.Snapshot.IsA(ThingActorTypeMasks.ComplexThing));
        Assert.True(state.Snapshot.IsA(ThingActorTypeMasks.Actor));
        Assert.True(state.Snapshot.IsA(0x40));
        Assert.False(state.Snapshot.IsA(0x08));

        state.SetThingType(0x80);

        Assert.Equal(0x80000083u, state.Snapshot.ThingTypeMask);
        Assert.False(state.Snapshot.IsA(0x40));
        Assert.True(state.Snapshot.IsA(0x80));
    }

    [Fact]
    public void ContactTimestamps_StartAtReleasedSentinelAndUpdateIndependently()
    {
        ThingActorBaseState state = CreateState();
        int sentinel = BitConverter.SingleToInt32Bits(-100.0f);
        Assert.Equal(sentinel, state.Snapshot.LastTimeOnGroundFloatBits);
        Assert.Equal(sentinel, state.Snapshot.LastTimeInWaterFloatBits);
        Assert.Equal(sentinel, state.Snapshot.LastTimeOnObjectFloatBits);

        int ground = BitConverter.SingleToInt32Bits(1.25f);
        int water = BitConverter.SingleToInt32Bits(2.5f);
        int onObject = BitConverter.SingleToInt32Bits(3.75f);
        state.DeclareOnGround(ground);
        state.DeclareInWater(water);
        state.DeclareOnObject(onObject);

        Assert.Equal(ground, state.Snapshot.LastTimeOnGroundFloatBits);
        Assert.Equal(water, state.Snapshot.LastTimeInWaterFloatBits);
        Assert.Equal(onObject, state.Snapshot.LastTimeOnObjectFloatBits);

        int nan = BitConverter.SingleToInt32Bits(float.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.DeclareOnGround(nan));
        Assert.Equal(ground, state.Snapshot.LastTimeOnGroundFloatBits);
    }

    [Fact]
    public void SnapshotRestore_RoundTripsAndRejectsImpossibleSourceState()
    {
        ThingActorBaseState state = CreateState();
        state.MakeInvisible();
        Assert.True(state.StartDieProcess());
        state.AdvancePose(new ThingActorPoseSnapshot(
            new SimVector3(40, 50, 60),
            IdentityBasis()));
        state.SetVelocity(new SimVector3(7, 8, 9));
        state.DeclareOnGround(BitConverter.SingleToInt32Bits(4.5f));
        ThingActorBaseStateSnapshot snapshot = state.Snapshot;

        var restored = new ThingActorBaseState(snapshot);

        Assert.Equal(snapshot, restored.Snapshot);

        ThingActorBaseStateSnapshot dyingWithoutShutdown = snapshot with
        {
            Flags = ThingActorFlags.Dying,
        };
        // CUnit's override marks TF_DYING without CThing's immediate
        // AddShutdownEvent/TF_DECLARED_SHUTDOWN path.
        Assert.Equal(dyingWithoutShutdown,
            new ThingActorBaseState(dyingWithoutShutdown).Snapshot);

        ThingActorBaseStateSnapshot missingActorLineage = snapshot with
        {
            ThingTypeMask = 0x40,
        };
        Assert.Throws<ArgumentException>(() =>
            new ThingActorBaseState(missingActorLineage));
    }

    [Fact]
    public void NonFinitePose_IsRejectedBeforeAnyStateMutation()
    {
        ThingActorBaseState state = CreateState();
        ThingActorBaseStateSnapshot before = state.Snapshot;
        var invalid = new ThingActorPoseSnapshot(
            new SimVector3(1, 2, 3),
            IdentityBasis() with
            {
                Row0X = BitConverter.SingleToInt32Bits(float.NaN),
            });

        Assert.Throws<ArgumentException>(() => state.AdvancePose(invalid));
        Assert.Equal(before, state.Snapshot);
        Assert.Throws<ArgumentException>(() => state.ResetPose(invalid));
        Assert.Equal(before, state.Snapshot);
        Assert.Throws<ArgumentException>(() => new ThingActorBaseState(
            invalid,
            SimVector3.Zero,
            SimVector3.Zero,
            specificTypeMask: 0));
    }

    private static ThingActorBaseState CreateState() => new(
        new ThingActorPoseSnapshot(
            new SimVector3(10, 20, 30),
            IdentityBasis()),
        new SimVector3(1, 2, 3),
        new SimVector3(4, 5, 6),
        specificTypeMask: 0x40);

    private static Level100FloatBasis3Bits IdentityBasis() => new(
        BitConverter.SingleToInt32Bits(1f), 0, 0,
        0, BitConverter.SingleToInt32Bits(1f), 0,
        0, 0, BitConverter.SingleToInt32Bits(1f));
}
