// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailPlaneMotionTests
{
    // A bounded recurrence from 32 consecutive copied-retail calls, pristine
    // 74154bfa…7750, private WineD3D/PC24 run observe-plane-motion-a. Cache
    // values are supplied observations, not a reconstruction of its events.
    // No pointer words, matrix padding or raw capture payload is embedded.
    [Fact]
    public void ObservedTrainerRecurrence_RetainsDriveOldPoseAndRestoresMidFlight()
    {
        ThingActorBaseState actor = InitialTrainer();
        ThingActorBaseState? restored = null;
        for (int tick = 1; tick <= 32; tick++)
        {
            int clearance = tick switch
            {
                1 => 0,
                < 13 => 0x40c51eb8,
                < 29 => 0x40ccc3a8,
                _ => 0x40cadd7c,
            };
            RetailActorPoseSnapshot previous = actor.Snapshot.RetailPoses!.Current;
            Advance(actor, clearance, tick);
            Assert.Equal(previous, actor.Snapshot.RetailPoses!.Old);
            Assert.Equal(BitConverter.SingleToInt32Bits(tick * .05f),
                actor.Snapshot.RetailMotion!.LastMoveTimeFloatBits);
            Assert.Equal(1, actor.Snapshot.RetailMotion.MoveCountdown);
            Assert.Equal(ThingActorBaseState.InitialContactTimeFloatBits,
                actor.Snapshot.LastTimeOnGroundFloatBits);
            if (tick == 1)
            {
                Assert.Equal(previous.PositionFloatBits, actor.Snapshot.RetailPoses.Current.PositionFloatBits);
                Assert.Equal(W(0x80000000, 0x80000000, 0x80000000), actor.Snapshot.RetailPlane!.Velocity);
                Assert.Equal(W(0x348ccde2, 0xc0400000, 0), actor.Snapshot.RetailPlane.Drive);
                Assert.Equal(W(0x4048fdcf, 0xbd32b8c2, 0xbd32b8c2), actor.Snapshot.RetailPlane.CurrentEuler);
                Assert.Equal(0x3f800000, actor.Snapshot.RetailPlane.BankFlagFloatBits);
            }
            if (tick == 2)
            {
                Assert.Equal(W(0x4384c000, 0x43c4051f, 0xc1700000), actor.Snapshot.RetailPoses.Current.PositionFloatBits);
                Assert.Equal(W(0xba7c173e, 0xbeeb4570, 0xbcacf91f), actor.Snapshot.RetailPlane!.Velocity);
                Assert.Equal(0, actor.Snapshot.RetailPlane.BankFlagFloatBits);
            }
            if (restored is not null)
            {
                Advance(restored, clearance, tick);
                Assert.Equal(actor.Snapshot, restored.Snapshot);
            }
            if (tick == 16) restored = new(actor.Snapshot);
        }
        Assert.Equal(W(0x4384b1b6, 0x43bd21ff, 0xc17d37c4), actor.Snapshot.RetailPoses!.Current.PositionFloatBits);
        Assert.Equal(W(0xbba68f4f, 0xbeeafaf2, 0xbcfb8c91), actor.Snapshot.RetailPlane!.Velocity);
        Assert.Equal(W(0x40485a67, 0xbd88d02d, 0xbb5345e4), actor.Snapshot.RetailPlane.CurrentEuler);
        Assert.Equal(W(0x40485301, 0xbd8af62e, 0xba101000), actor.Snapshot.RetailPlane.DesiredEuler);
        Assert.Equal(W(0xbd072b70, 0xc03f8fc4, 0xbe4cb5ba), actor.Snapshot.RetailPlane.Drive);
        Assert.Equal(B(0xbf7ffbcc, 0xbc350b13, 0x3b22cf89,
            0x3c31ebe4, 0xbf7f69cf, 0xbd88c685,
            0x3b52cd28, 0xbd88b622, 0x3f7f6d7b), actor.Snapshot.RetailPoses.Current.BasisFloatBits);
    }

    [Fact]
    public void AirSpeedCap_UsesStoredFloatTickRatherThanDivisionByTwenty()
    {
        Level100FloatVector3Bits velocity = RetailPlaneMotion.IntegrateVelocity(
            default, W(0, 0xc0400000, 0), 0x41133333, 0);
        Assert.Equal(W(0, 0xbeeb851f, 0), velocity);
        Assert.NotEqual(BitConverter.SingleToInt32Bits(-9.2f / 20), velocity.Y);
    }

    [Fact]
    public void Clearance_FirstObservedCallbackUsesRoundedIntegerTerrainScan()
    {
        Assert.Equal(0x40c51eb8, RetailPlaneMotion.ComputeClearance(Level100Terrain.Instance,
            InitialTrainer().Snapshot.RetailPoses!.Current.PositionFloatBits));
        Assert.Equal(0x497423f0, RetailPlaneMotion.ComputeClearance(Level100Terrain.Instance,
            W(0x4384c000, 0x43c44000, 0xca000000)));
    }

    [Theory]
    [InlineData(512, 100, 512, 4)]
    [InlineData(513, 100, 512, 4)]
    [InlineData(511, 512, 511, 512)]
    [InlineData(512, 512, 512, 512)]
    [InlineData(768, 768, 512, 512)]
    [InlineData(4194305, 4194306, 1, 2)]
    public void IntegerLookup_RetainsReleasedMaskAndAsymmetricEdgeIndex(int x, int y, int sampleX, int sampleY)
    {
        Assert.Equal(Level100Terrain.Instance.SampleGridHeightUnits(sampleX, sampleY),
            Level100Terrain.Instance.SampleAirGuideHeightUnits(x, y));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    [InlineData(1024, 10)]
    public void IntegerLookup_UnmatchedMaskReturnsZero(int x, int y) =>
        Assert.Equal(0, Level100Terrain.Instance.SampleAirGuideHeightUnits(x, y));

    [Fact]
    public void RawAdmission_RejectsPartialAndConflictingRestoreWithoutOverwritingState()
    {
        ThingActorBaseState actor = InitialTrainer();
        ThingActorBaseStateSnapshot before = actor.Snapshot;
        Assert.Throws<ArgumentException>(() => new ThingActorBaseState(before with { RetailPoses = null }));
        Assert.Throws<ArgumentException>(() => new ThingActorBaseState(before with { RetailMotion = null }));
        Assert.Throws<ArgumentException>(() => new ThingActorBaseState(before with { Velocity = new(1, 0, 0) }));
        Assert.Throws<ArgumentException>(() => actor.CommitRetailPlaneMove(before.RetailPoses!.Current,
            before.RetailPlane! with { Drive = W(0x7fc00000, 0, 0) }, 0));
        Assert.Throws<OverflowException>(() => actor.CommitRetailPlaneMove(before.RetailPoses!.Current,
            before.RetailPlane! with { Velocity = W(0x7f7fffff, 0, 0) }, 0));
        Assert.Throws<OverflowException>(() => actor.CommitRetailPlaneMove(before.RetailPoses!.Current,
            before.RetailPlane! with { CurrentEuler = W(0x7f7fffff, 0, 0) }, 0));
        Assert.Throws<NotSupportedException>(() => actor.AdvancePose(before.CurrentPose));
        Assert.Equal(before, actor.Snapshot);
        Assert.Equal(before, new ThingActorBaseState(before).Snapshot);
    }

    private static ThingActorBaseState InitialTrainer()
    {
        var basis = B(0xbf800000, 0x33bbbd2e, 0x80000000,
            0xb3bbbd2e, 0xbf800000, 0, 0x80000000, 0, 0x3f800000);
        var actor = new ThingActorBaseState(new(SimVector3.Zero, basis), default, default, 0);
        var euler = W(0x40490fdb, 0, 0);
        actor.BeginRetailPlane(new(W(0x4384c000, 0x43c44000, 0xc1700000), basis),
            new(default, default, euler, euler, W(0x3d32b8c2, 0x3d32b8c2, 0x3d32b8c2), 0), 0, 0);
        return actor;
    }

    private static void Advance(ThingActorBaseState actor, int clearance, int tick)
    {
        RetailPlaneMotion.AdvanceFreeFlight(actor,
            new(W(0x43843000, 0x43913000, 0xc1ae661a), 1, clearance, 1, 0, null),
            0x41133333, BitConverter.SingleToInt32Bits(tick * .05f));
    }

    private static Level100FloatVector3Bits W(uint x, uint y, uint z) =>
        new(unchecked((int)x), unchecked((int)y), unchecked((int)z));
    private static Level100FloatBasis3Bits B(params uint[] words) => new(
        unchecked((int)words[0]), unchecked((int)words[1]), unchecked((int)words[2]),
        unchecked((int)words[3]), unchecked((int)words[4]), unchecked((int)words[5]),
        unchecked((int)words[6]), unchecked((int)words[7]), unchecked((int)words[8]));
}
