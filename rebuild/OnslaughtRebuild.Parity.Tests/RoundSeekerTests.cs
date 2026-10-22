// SPDX-License-Identifier: GPL-3.0-only
// Original query 00425c60, base mask 00426900, centre 004f3ac0 and velocity 00404120 run unchanged
// on authored inputs: pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
// At 00425e01..00425e0e the x87 radius-squared comparison rejects both less-than and unordered.
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class RoundSeekerTests
{
    private sealed class Target(ThingContext c) : Thing(c, Oid.Thing)
    {
        public float Radius = 1.0f;
        public override float GetBoundingRadius() => Radius;
    }

    private static (Round Round, RoundSeeker Seeker, CollisionSeekingThing Other) Pair(
        float velocityX, float targetY = 0, uint targetType = ThingType.Stationary, float radius = 1.0f)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init();
        var c = new ThingContext(pool, events, new SeedUniformRandomNumberStream(1), null!, new PhysicsRegistry());
        var round = new Round(c, new RoundData("test"u8.ToArray()) { Speed = 60 })
            { Velocity = new FVector(velocityX, 0, 0) };
        var target = new Target(c) { Pos = new FVector(2, targetY, 0), Radius = radius };
        target.SetThingType(targetType);
        var init = new InitCollision { StartCollideOnNextFrame = false };
        return (round, new RoundSeeker(round, init), new CollisionSeekingThing(target, init));
    }

    [Theory]
    [InlineData(0.0f, ThingType.Stationary)]
    [InlineData(float.NaN, ThingType.Stationary)]
    [InlineData(float.PositiveInfinity, ThingType.Stationary)]
    [InlineData(0.0f, ThingType.Infantry)]
    [InlineData(float.NaN, ThingType.Infantry)]
    public void StraightRoundsRejectUndefinedTrajectories(float velocityX, uint targetType)
    {
        var p = Pair(velocityX, targetType: targetType);
        Assert.False(p.Seeker.Seeks(p.Other));
    }

    [Theory]
    [InlineData(0.0f, true)]
    [InlineData(1.0f, true)]
    [InlineData(2.0f, false)]
    public void FiniteFlightLineAdmitsTheTangentAndRejectsOutside(float targetY, bool expected)
    {
        var p = Pair(1.0f, targetY);
        Assert.Equal(expected, p.Seeker.Seeks(p.Other));
        p.Other.ForThing.Pos.Y = float.BitIncrement(1.0f);
        Assert.False(p.Seeker.Seeks(p.Other));
    }

    [Theory]
    [InlineData(float.NaN, false)]
    [InlineData(float.PositiveInfinity, true)]
    public void RadiusComparisonRejectsUnorderedButAdmitsInfiniteRadius(float radius, bool expected)
    {
        var p = Pair(1.0f, 2.0f, radius: radius);
        Assert.Equal(expected, p.Seeker.Seeks(p.Other));
    }

    [Fact]
    public void StoppedRoundStillAdmitsTargetsOutsideTheStraightTrajectoryFilter()
    {
        var mobile = Pair(0, targetType: ThingType.None);
        Assert.True(mobile.Seeker.Seeks(mobile.Other));
        var bouncing = Pair(0);
        bouncing.Round.Data.BounceFactor = 1;
        Assert.True(bouncing.Seeker.Seeks(bouncing.Other));
    }

    [Fact]
    public void LauncherAndExcludedTypesRemainRejectedBeforeTheTrajectoryTest()
    {
        var launcher = Pair(1);
        launcher.Round.Launcher.SetReader(launcher.Other.ForThing);
        Assert.False(launcher.Seeker.Seeks(launcher.Other));
        var masked = Pair(1);
        masked.Seeker.SetCollisionMask(ThingType.Stationary);
        Assert.False(masked.Seeker.Seeks(masked.Other));
    }
}
