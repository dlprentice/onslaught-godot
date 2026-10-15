// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// The renderer draws between two 30 Hz snapshots. These cover the two halves
/// of that contract: non-player entities move continuously between the pair,
/// and no entity is ever interpolated across a spawn, despawn, or teleport.
/// </summary>
public sealed class Level100RenderInterpolationTests
{
    private static readonly Level100ActorId ActorA = new(11);
    private static readonly Level100ActorId ActorB = new(12);

    [Fact]
    public void NonPlayerTargetMovesContinuouslyBetweenSnapshots()
    {
        Level100TargetVisualDescriptor previous = Target(
            ActorA,
            new Level100RenderVector3(10f, 2f, -30f));
        Level100TargetVisualDescriptor current = Target(
            ActorA,
            new Level100RenderVector3(12f, 3f, -34f));

        Level100TargetVisualDescriptor quarter =
            Level100RenderInterpolation.Interpolate(previous, current, 0.25f);
        Assert.Equal(10.5f, quarter.Position.X, 5);
        Assert.Equal(2.25f, quarter.Position.Y, 5);
        Assert.Equal(-31f, quarter.Position.Z, 5);

        Level100TargetVisualDescriptor start =
            Level100RenderInterpolation.Interpolate(previous, current, 0f);
        Assert.Equal(previous.Position, start.Position);

        Level100TargetVisualDescriptor end =
            Level100RenderInterpolation.Interpolate(previous, current, 1f);
        Assert.Equal(current.Position.X, end.Position.X, 5);
        Assert.Equal(current.Position.Y, end.Position.Y, 5);
        Assert.Equal(current.Position.Z, end.Position.Z, 5);
    }

    [Fact]
    public void TargetOrientationTakesTheShortestArcBetweenSnapshots()
    {
        Level100TargetVisualDescriptor previous = Target(
            ActorA,
            new Level100RenderVector3(0f, 0f, 0f),
            YawBasis(0f));
        Level100TargetVisualDescriptor current = Target(
            ActorA,
            new Level100RenderVector3(0f, 0f, 0f),
            YawBasis(MathF.PI / 2f));

        Level100RenderBasis3 half = Level100RenderInterpolation
            .Interpolate(previous, current, 0.5f)
            .Basis;
        Level100RenderBasis3 expected = YawBasis(MathF.PI / 4f);

        AssertBasisEqual(expected, half);

        // A 350 degree turn is a 10 degree turn the other way; a naive
        // component lerp would swing the long way through the midpoint.
        Level100RenderBasis3 wrap = Level100RenderInterpolation.InterpolateBasis(
            YawBasis(-MathF.PI * 175f / 180f),
            YawBasis(MathF.PI * 175f / 180f),
            0.5f);
        AssertBasisEqual(YawBasis(MathF.PI), wrap);
    }

    [Fact]
    public void TargetOrientationUsesSphericalRatherThanLinearQuaternionWeights()
    {
        Level100RenderBasis3 quarter = Level100RenderInterpolation.InterpolateBasis(
            YawBasis(0f),
            YawBasis(2f * MathF.PI / 3f),
            0.25f);

        AssertBasisEqual(YawBasis(MathF.PI / 6f), quarter);
    }

    [Fact]
    public void SpawnDespawnAndTeleportSuppressInterpolation()
    {
        Level100TargetVisualDescriptor previous = Target(
            ActorA,
            new Level100RenderVector3(10f, 2f, -30f));
        Level100TargetVisualDescriptor current = Target(
            ActorA,
            new Level100RenderVector3(12f, 3f, -34f));

        // Spawn: the actor had no previous-tick state, so it must appear where
        // Core put it rather than sliding in from anywhere.
        Assert.Equal(
            current.Position,
            Level100RenderInterpolation.Interpolate(null, current, 0.5f).Position);

        // Despawn and respawn: an actor hidden on either side of the pair has a
        // stale pose and must not be smeared back into view.
        Assert.Equal(
            current.Position,
            Level100RenderInterpolation
                .Interpolate(previous with { Visible = false }, current, 0.5f)
                .Position);
        Level100TargetVisualDescriptor hiddenNow = current with { Visible = false };
        Assert.Equal(
            hiddenNow.Position,
            Level100RenderInterpolation
                .Interpolate(previous, hiddenNow, 0.5f)
                .Position);

        // Identity mismatch: were the caller ever to pair by list ordinal, the
        // mismatch is refused instead of sliding one actor toward another.
        Assert.Equal(
            current.Position,
            Level100RenderInterpolation
                .Interpolate(previous with { ActorId = ActorB }, current, 0.5f)
                .Position);

        // Teleport: further than one plausible tick of travel.
        Level100TargetVisualDescriptor teleported = current with
        {
            Position = new Level100RenderVector3(410f, 2f, -30f),
        };
        Assert.Equal(
            teleported.Position,
            Level100RenderInterpolation
                .Interpolate(previous, teleported, 0.5f)
                .Position);
    }

    [Fact]
    public void ProjectileFirstFrameRunsFromItsDerivedMuzzleState()
    {
        var velocity = new Level100RenderVector3(4_000f, 0f, 0f);
        var muzzle = new Level100ProjectileVisualState(
            new Level100RenderVector3(1f, 2f, -3f),
            velocity);
        var current = new Level100ProjectileVisualState(
            new Level100RenderVector3(5f, 2f, -3f),
            velocity);

        // No previous-tick entry: the bolt travels out of the barrel across the
        // frame instead of appearing a whole tick downrange.
        Level100ProjectileVisualState first =
            Level100RenderInterpolation.Interpolate(null, muzzle, current, 0f);
        Assert.Equal(muzzle.Position.X, first.Position.X, 5);

        Level100ProjectileVisualState firstHalf =
            Level100RenderInterpolation.Interpolate(null, muzzle, current, 0.5f);
        Assert.Equal(3f, firstHalf.Position.X, 5);

        // Once the bolt has a previous-tick entry the muzzle state is ignored.
        var previous = new Level100ProjectileVisualState(
            new Level100RenderVector3(5f, 2f, -3f),
            velocity);
        var next = new Level100ProjectileVisualState(
            new Level100RenderVector3(9f, 2f, -3f),
            velocity);
        Level100ProjectileVisualState continued =
            Level100RenderInterpolation.Interpolate(previous, muzzle, next, 0.5f);
        Assert.Equal(7f, continued.Position.X, 5);
    }

    [Fact]
    public void NonRotationBasisFallsBackToComponentLerp()
    {
        var scaled = new Level100RenderBasis3(
            new Level100RenderVector3(2f, 0f, 0f),
            new Level100RenderVector3(0f, 2f, 0f),
            new Level100RenderVector3(0f, 0f, 2f));
        var identity = YawBasis(0f);

        Level100RenderBasis3 half =
            Level100RenderInterpolation.InterpolateBasis(scaled, identity, 0.5f);
        Assert.Equal(1.5f, half.XAxis.X, 5);
        Assert.Equal(1.5f, half.YAxis.Y, 5);
        Assert.Equal(1.5f, half.ZAxis.Z, 5);
    }

    private static Level100RenderBasis3 YawBasis(float yaw)
    {
        float sin = MathF.Sin(yaw);
        float cos = MathF.Cos(yaw);
        return new Level100RenderBasis3(
            new Level100RenderVector3(cos, 0f, -sin),
            new Level100RenderVector3(0f, 1f, 0f),
            new Level100RenderVector3(sin, 0f, cos));
    }

    private static Level100TargetVisualDescriptor Target(
        Level100ActorId actorId,
        Level100RenderVector3 position,
        Level100RenderBasis3? basis = null) =>
        new(
            actorId,
            "Target Truck",
            "m_f_truck_training.msh.aya",
            true,
            position,
            basis ?? YawBasis(0f));

    private static void AssertBasisEqual(
        Level100RenderBasis3 expected,
        Level100RenderBasis3 actual)
    {
        AssertVectorEqual(expected.XAxis, actual.XAxis);
        AssertVectorEqual(expected.YAxis, actual.YAxis);
        AssertVectorEqual(expected.ZAxis, actual.ZAxis);
    }

    private static void AssertVectorEqual(
        Level100RenderVector3 expected,
        Level100RenderVector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }
}
