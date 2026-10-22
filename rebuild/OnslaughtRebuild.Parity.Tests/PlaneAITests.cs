// SPDX-License-Identifier: GPL-3.0-only
// Matched PlaneAI.cpp 004d21c0/004164b0 and Plane.cpp 004d1f90..004d209e.
// Pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using OnslaughtRebuild.Parity.Data;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class PlaneAITests
{
    private sealed class TestUnit(ThingContext context) : Unit(context);
    private sealed class ObservedGuide(Unit unit) : Guide(unit)
    {
        public readonly List<FVector> Points = [];
        public Action? BeforeGo;
        public int Stops;
        public override void GoToPoint(FVector point, bool inOverride)
        {
            Assert.False(inOverride); BeforeGo?.Invoke(); Points.Add(point); base.GoToPoint(point, inOverride);
        }
        public override void Stop() { Stops++; base.Stop(); }
    }

    private static (ThingContext Context, Plane Plane, PlaneAI AI, ObservedGuide Guide) Fixture(int seed = 123)
    {
        var pool = new SPtrSetPool(); pool.Init(64);
        var events = new EventManager(); events.Init(); events.Log = [];
        var raw = new byte[24 + HeightField.RecordSize + HeightField.SampleCount * 2];
        var map = HeightField.Load(new ChunkReader(MemBuffer.FromFile("plane-ai-flat-map", raw)));
        var context = new ThingContext(pool, events, new SeedUniformRandomNumberStream(seed), map, new PhysicsRegistry());
        var plane = new Plane(context) { UnitData = new UnitData("test plane"u8.ToArray()) { MinAltitude = 0 }, WingState = 0 };
        var guide = new ObservedGuide(plane); plane.Guide = guide;
        var ai = new PlaneAI(plane, new InitThing()); plane.AI = ai;
        events.Log.Clear();
        return (context, plane, ai, guide);
    }

    private static TestUnit Target(ThingContext context, Plane plane, PlaneAI ai, float distance, bool farWeapon = false)
    {
        var target = new TestUnit(context) { UnitData = new UnitData("target"u8.ToArray()), Pos = new FVector(distance, 0, 0) };
        ai.Target.SetReader(target); ai.Unknown10 = 1; // authored direct-target fixture, not a gameplay acceptance route
        if (farWeapon)
        {
            // A busy weapon keeps weapon selection stable; with no round its maximum range is zero, so band 1.
            var weapon = new Weapon(context, new WeaponData("fixture"u8.ToArray()), 0)
            { Mode = new WeaponModeData("fixture"u8.ToArray()) { BurstSize = 2 }, BurstCount = 1 };
            plane.AppendWeapon(weapon); plane.CurrentWeapon = weapon;
        }
        return target;
    }

    [Fact]
    public void DyingHandlerDoesNotTargetMoveAnimateDrawOrReschedule()
    {
        var (context, plane, ai, guide) = Fixture();
        Target(context, plane, ai, 100); plane.Flags |= ThingFlags.Dying; ai.Unknown60 = 1; plane.WingState = 1;
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(0, context.Random.Draws); Assert.Empty(context.Events.Log!); Assert.Empty(guide.Points);
        Assert.Equal(0, guide.Stops); Assert.Equal(1, ai.Unknown60); Assert.Equal(1, plane.WingState); Assert.Null(plane.Animation);
    }

    [Theory]
    [InlineData(70f, 0, 0, -70f)]
    [InlineData(70.00001f, 0, 1, -70.00001f)]
    [InlineData(25f, 1, 1, 25f)]
    [InlineData(24.999998f, 1, 0, -24.999998f)]
    [InlineData(100f, 1, 1, 100f)]
    public void ApproachRetreatThresholdsAreStrictAndOuterTransitionRetreatsOnce(float distance, int initial, int final, float destination)
    {
        var (context, plane, ai, guide) = Fixture();
        var target = Target(context, plane, ai, distance, farWeapon: true); ai.Unknown60 = initial;
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(final, ai.Unknown60); Assert.Equal(new FVector(destination, 0, 0), Assert.Single(guide.Points));
        Assert.Same(target, ai.Target.ToRead); Assert.Equal(1, target.DeletionCount);
        Assert.Equal(2, context.Random.Draws); // Unknown3's unfired delay, then PlaneAI's independent schedule draw.
        Assert.Equal(0, context.Crt.Calls);
        EventLogEntry scheduled = Assert.Single(context.Events.Log!);
        Assert.Equal(UnitAI.Fight, scheduled.EventNum);
        var expected = new SeedUniformRandomNumberStream(123); expected.Random();
        Assert.Equal(BitConverter.SingleToInt32Bits(PlaneAI.NextFightTime(0, expected.Random())), unchecked((int)scheduled.TimeBits));
    }

    [Fact]
    public void ConsecutiveFightStepsReverseOnlyAfterOuterDistanceArmsTheApproach()
    {
        var (context, plane, ai, guide) = Fixture();
        Target(context, plane, ai, 100, farWeapon: true);
        ai.Unknown9(new Event(UnitAI.Fight));
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(new[] { new FVector(-100, 0, 0), new FVector(100, 0, 0) }, guide.Points);
        Assert.Equal(1, ai.Unknown60); Assert.Equal(4, context.Random.Draws);
    }

    [Fact]
    public void TargetDestructionBetweenStepsClearsReaderAndResumesWaypointWithoutSchedulingDraw()
    {
        var (context, plane, ai, guide) = Fixture();
        TestUnit target = Target(context, plane, ai, 80); var waypoint = new Waypoint(context) { Pos = new FVector(5, 6, 7) };
        ai.Waypoint.SetReader(waypoint); ai.Unknown60 = 1; target.Destroy();
        Assert.Null(ai.Target.ToRead);
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(0, ai.AIMode); Assert.Equal(0, ai.Unknown60); Assert.Equal(1, context.Random.Draws);
        Assert.Empty(guide.Points); Assert.Equal(0, guide.Stops);
        Assert.Equal(UnitAI.FollowWaypoints, Assert.Single(context.Events.Log!).EventNum);
    }

    [Theory]
    [InlineData(120f, true, false)]
    [InlineData(120.00001f, true, true)]
    [InlineData(30f, false, false)]
    [InlineData(29.999998f, false, false)]
    public void EscortClearsOnlyTargetAfterMoveAndKeepsNativeNearStop(float distance, bool hasTarget, bool cleared)
    {
        var (context, plane, ai, guide) = Fixture(); plane.UnitData!.Wingman = 1;
        var game = new ParityGame(new PlatformClock(), context.Events); context.Game = game; game.CreatePlayers(context, false);
        var engine = new BattleEngine(context) { Pos = new FVector(distance, 0, 0) }; game.GetPlayer(0)!.AssignBattleEngine(engine);
        TestUnit? target = hasTarget ? Target(context, plane, ai, 10) : null;
        ai.Unknown10 = 1; ai.Unknown60 = 1;
        if (cleared) guide.BeforeGo = () => Assert.Same(target, ai.Target.ToRead);
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(cleared ? null : target, ai.Target.ToRead); Assert.Equal(hasTarget ? 1 : 0, ai.Unknown10);
        if (cleared) { Assert.Equal(engine.Pos, Assert.Single(guide.Points)); Assert.Equal(0, target!.DeletionCount); }
        Assert.Equal(!hasTarget && distance < 30 ? 1 : 0, guide.Stops);
        Assert.Equal(2, context.Random.Draws);
    }

    [Fact]
    public void WaypointSteersToOldNodeThenAdvancesAndWaitsOneSecondEvenAtEnd()
    {
        var (context, plane, ai, guide) = Fixture();
        var waypoint = new Waypoint(context) { Pos = new FVector(3, 0, 0) };
        ai.Waypoint.SetReader(waypoint); ai.AIMode = 0;
        guide.BeforeGo = () => Assert.Same(waypoint, ai.Waypoint.ToRead);
        ai.Unknown8(new Event(UnitAI.FollowWaypoints));
        Assert.Equal(waypoint.Pos, Assert.Single(guide.Points)); Assert.Null(ai.Waypoint.ToRead);
        Assert.Equal(0, waypoint.DeletionCount); Assert.Equal(0, ai.AIMode); Assert.Equal(1, context.Random.Draws);
        var scheduled = Assert.Single(context.Events.Log!); Assert.Equal(UnitAI.FollowWaypoints, scheduled.EventNum);
        Assert.Equal(BitConverter.SingleToInt32Bits(1f), unchecked((int)scheduled.TimeBits));
        context.Events.Log!.Clear(); ai.Unknown8(new Event(UnitAI.FollowWaypoints));
        Assert.Equal(1, ai.AIMode); Assert.Equal(1, context.Random.Draws);
        Assert.Equal(UnitAI.Fight, Assert.Single(context.Events.Log).EventNum);
    }

    [Theory]
    [InlineData(4f, false)]
    [InlineData(3.9999998f, true)]
    public void WaypointThresholdUsesOnlyHorizontalDistanceAndPreservesTheNextReader(float x, bool advanced)
    {
        var (context, _, ai, guide) = Fixture();
        var waypoint = new Waypoint(context) { Pos = new FVector(x, 0, -1000) };
        var next = new Waypoint(context) { Pos = new FVector(100, 0, -2000) }; waypoint.Next.SetReader(next);
        ai.Waypoint.SetReader(waypoint); ai.AIMode = 0;
        ai.Unknown8(new Event(UnitAI.FollowWaypoints));
        Assert.Same(advanced ? next : waypoint, ai.Waypoint.ToRead);
        Assert.Equal(advanced ? 2 : 1, next.DeletionCount); // next-link and, only after advance, AI reader
        Assert.Equal(waypoint.Pos, Assert.Single(guide.Points)); Assert.Equal(1, context.Random.Draws);
        Assert.Equal(UnitAI.FollowWaypoints, Assert.Single(context.Events.Log!).EventNum);
    }

    [Fact]
    public void LostTargetWhileAlreadyInWaypointModeStillReschedulesTheIncomingFight()
    {
        var (context, _, ai, guide) = Fixture();
        var waypoint = new Waypoint(context); ai.Waypoint.SetReader(waypoint); ai.AIMode = 0;
        ai.Unknown9(new Event(UnitAI.Fight));
        Assert.Equal(0, ai.AIMode); Assert.Same(waypoint, ai.Waypoint.ToRead);
        Assert.Equal(1, guide.Stops); Assert.Equal(2, context.Random.Draws);
        Assert.Equal(UnitAI.Fight, Assert.Single(context.Events.Log!).EventNum);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void UnavailableMovementStillQueuesWaypointButDoesNotTargetOrDraw(bool active, int preparing)
    {
        var (context, plane, ai, guide) = Fixture(); plane.Active = active; plane.Unknown168 = preparing;
        var waypoint = new Waypoint(context); ai.Waypoint.SetReader(waypoint); ai.AIMode = 0;
        ai.Unknown8(new Event(UnitAI.FollowWaypoints));
        Assert.Same(waypoint, ai.Waypoint.ToRead); Assert.Empty(guide.Points); Assert.Equal(0, context.Random.Draws);
        Assert.Equal(UnitAI.FollowWaypoints, Assert.Single(context.Events.Log!).EventNum);
    }

    [Fact]
    public void SchedulerReusesPlaneFightEventAndDoesNotDuplicateItsReader()
    {
        var (context, _, ai, _) = Fixture();
        ScheduledEvent original = Enumerable.Range(0, EventManager.BufferCount)
            .SelectMany(i => context.Events.Buffer(i, EventPriority.StartOfFrame)).Single();
        int readers = ai.DeletionCount;
        context.Events.Update();
        ScheduledEvent next = Enumerable.Range(0, EventManager.BufferCount)
            .SelectMany(i => context.Events.Buffer(i, EventPriority.StartOfFrame)).Single();
        Assert.Same(original, next); Assert.Same(ai, next.ToCall); Assert.Null(next.Data);
        Assert.Equal(readers, ai.DeletionCount); Assert.Equal(1, context.Events.TotalEvents); Assert.Equal(2, context.Random.Draws);
    }

    [Fact]
    public void WingsUseConcreteAnimationCompletionInsteadOfGenericStandingFallback()
    {
        var (_, plane, _, _) = Fixture(); plane.WingState = 1;
        plane.OpenWings(); Assert.Equal(2, plane.WingState); Assert.False(plane.Animation!.ForceLooped);
        // No mesh means its named mode is invalid: the native callback must leave this transition waiting.
        Assert.False(((ComplexThing)plane).FinishedPlayingCurrentAnimation()); Assert.Equal(2, plane.WingState);
        plane.Animation.AnimMode = 11; plane.Animation.Frame = 0; plane.Animation.FrameIncrement = 1;
        plane.Animation.Process(); Assert.Equal(4, plane.WingState); Assert.True(plane.Animation.ForceLooped);
        plane.CloseWings(); Assert.Equal(3, plane.WingState); Assert.False(plane.Animation.ForceLooped);
        plane.Animation.AnimMode = 11; plane.Animation.Frame = 0; plane.Animation.FrameIncrement = 1;
        plane.Animation.Process(); Assert.Equal(1, plane.WingState); Assert.True(plane.Animation.ForceLooped);
        Assert.Equal(-1, plane.Animation.AnimMode); // no invalid generic ModeStand overwrite
    }

    [RetailOracleFact("plane-ai-arithmetic")]
    public void DistancesReflectionAndSchedulingMatchUnchangedOriginalInstructions()
    {
        var lines = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("plane-ai-arithmetic"))
        {
            Assert.Equal("Plane", function); Assert.Equal(10, w.Length);
            float F(int n) => RetailOracle.F(w[n]);
            var aim = new FVector(F(0), F(1), F(2)); var plane = new FVector(F(3), F(4), F(5));
            double escort = PlaneAI.EscortDistance(aim, plane), attack = PlaneAI.AttackDistance(aim, plane, F(6), F(7));
            double waypoint = PlaneAI.WaypointDistance(aim, plane); FVector retreat = PlaneAI.RetreatPoint(plane, aim);
            string line = $"Plane {BitConverter.DoubleToUInt64Bits(escort):x16} {BitConverter.DoubleToUInt64Bits(attack):x16} "
                + $"{BitConverter.DoubleToUInt64Bits(waypoint):x16} {RetailOracle.H(retreat.X)} {RetailOracle.H(retreat.Y)} {RetailOracle.H(retreat.Z)} "
                + RetailOracle.H(PlaneAI.NextFightTime(F(8), unchecked((int)w[9])));
            lines.Add(line); Assert.True(line == expected, $"case{lines.Count-1}:\nours {line}\nretail {expected}");
        }
        Assert.Equal(1024, lines.Count); RetailOracle.WriteOurs("plane-ai-arithmetic", lines.ToArray());
    }
}
