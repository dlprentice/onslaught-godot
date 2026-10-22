// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class EventLifetimeTests
{
    [GameDataFact]
    public void LoadingPresentMatchesNativeRadarCountAndPreNoiseRandomBoundary()
    {
        // Frozen native e55cb2d8bf87: codex-l100-route-moving-forward-over-ridge-20261017/probes.txt, P3.
        // Original pre-run ends before the loading Present. Nine unit-noise draws occur only after it returns.
        var session = LevelSession.Load(100, GameData.Read, LevelSession.VirtualVoiceFor(GameData.Read));
        session.AdvanceToNextPresent();
        Assert.Equal(3, session.PresentCount);
        Assert.Equal(6683u, session.Context.Events.TotalEventsProcessed);
        Assert.Equal(4103L, session.Context.Crt.Calls);
        Assert.Equal(0x10181a1eu, session.Context.Crt.HoldRand);
        var engine = Assert.IsType<BattleEngine>(session.Game.GetPlayer(0)!.BattleEngine);
        Assert.NotNull(engine.RadarWarningReceiver);
        var radar = engine.RadarWarningReceiver;
        engine.Shutdown();
        Assert.Null(engine.RadarWarningReceiver);
        Assert.Equal(0, radar.DeletionCount);
    }

    private sealed class TestThing(ThingContext context) : ComplexThing(context, Oid.Thing);
    private sealed class TestUnit(ThingContext context) : Unit(context);

    private static ThingContext Context()
    {
        var pool = new SPtrSetPool();
        pool.Init(128);
        var events = new EventManager();
        events.Init();
        return new ThingContext(pool, events, new SeedUniformRandomNumberStream(123456), null!, new PhysicsRegistry());
    }

    [Theory]
    [InlineData(true, 2u)]
    [InlineData(false, 3u)]
    public void DeletingAnOwnerStopsAnimationButCountsItsAlreadyQueuedNullEvent(bool deleteFirst, uint finalCount)
    {
        ThingContext c = Context();
        var owner = new TestThing(c);
        if (deleteFirst) c.Events.AddEvent(ThingEvent.Shutdown, owner, EventManager.NextFrame);
        var animation = new Animation(owner);
        owner.Animation = animation;
        if (!deleteFirst) c.Events.AddEvent(ThingEvent.Shutdown, owner, EventManager.NextFrame);

        c.Events.Update();
        Assert.Null(owner.Animation);
        Assert.Equal(0, animation.DeletionCount);
        Assert.Equal(2u, c.Events.TotalEventsProcessed);
        c.Events.Update();
        Assert.Equal(finalCount, c.Events.TotalEventsProcessed);
        Assert.Equal(0, c.Events.TotalEvents);
        c.Events.Update();
        Assert.Equal(finalCount, c.Events.TotalEventsProcessed);
    }

    private static RadarWarningReceiver Radar(ThingContext c, Unit unit, Action? alarm = null)
    {
        var radar = new RadarWarningReceiver(c, c.World.Set110, alarm ?? (() => { }));
        radar.Init(new InitEquipment { AttachedTo = unit }, 80.0f, 0.03f);
        return radar;
    }

    private static Round Round(ThingContext c, Thing target, FVector pos, float damage = 1)
    {
        var round = new Round(c, new RoundData("test"u8.ToArray()) { Seek = 1, Damage = damage }) { Pos = pos };
        round.SetTarget(target, false);
        return round;
    }

    [Fact]
    public void CockpitThenRadarInitPreservesTheirSharedEndOfFrameOrder()
    {
        // Native Init: cockpit ctor call 004055dc, radar Init call 00405710. The old combined Skip was before
        // Cockpit in C#, but using that position for real radar Init reverses the priority-2 dispatch order.
        ThingContext c = Context();
        var be = new BattleEngine(c);
        var cockpit = new Cockpit(c, be, null);
        var radar = Radar(c, be);
        var pending = c.Events.Buffer(0, EventPriority.EndOfFrame);
        Assert.Equal(new[] { 2001, RadarWarningReceiver.UpdateEvent }, pending.Select(e => e.EventNum));
        Assert.Same(cockpit, pending[0].ToCall);
        Assert.Same(radar, pending[1].ToCall);
    }

    [Fact]
    public void EmptyRadarScansOncePerTickAtEndOfFrameIncludingInactiveOwnerWithoutRandomDraws()
    {
        ThingContext c = Context();
        var unit = new TestUnit(c) { Flags = ThingFlags.Dying | ThingFlags.Invisible };
        var radar = Radar(c, unit);
        c.Events.Log = [];
        for (int i = 0; i < 60; i++) c.Events.Update();
        Assert.Equal(60u, c.Events.TotalEventsProcessed);
        Assert.Equal(1, c.Events.TotalEvents);
        Assert.Equal(0, c.Random.Draws);
        Assert.Equal(0, c.Crt.Calls);
        Assert.False(radar.NewThreat);
        Assert.Equal(-1.0f, radar.NewThreatTime);
        Assert.All(c.Events.Log.Where(e => e.Kind == 'F'), e => Assert.Equal(2, e.Priority));
        Assert.All(c.Events.Log.Where(e => e.Kind == 'A'), e => Assert.Equal(2, e.Priority));
        radar.Delete();
        c.Events.Update();
        Assert.Equal(61u, c.Events.TotalEventsProcessed);
        Assert.Equal(0, c.Events.TotalEvents);
        c.Events.Update();
        Assert.Equal(61u, c.Events.TotalEventsProcessed);
    }

    [Fact]
    public void RadarUsesTheTargetedPositiveDamageRoundSetRangeAndHeadInsertionOrder()
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        var other = new BattleEngine(c);
        other.SetThingType(0);
        int alarms = 0;
        var radar = Radar(c, be, () => alarms++);
        Round first = Round(c, be, new FVector(0, 80, 0));
        Round second = Round(c, be, new FVector(0, 10, 0));
        Round(c, be, new FVector(0, float.BitIncrement(80.0f), 0));
        Round(c, be, new FVector(0, 10, 0), 0);
        Round(c, be, new FVector(0, 10, 0), -1);
        Round(c, other, new FVector(0, 10, 0));
        c.Events.Log = [];

        radar.Update();

        // Scan sees second then first, and each threat Add inserts at the head: the threat order is first, second.
        Assert.Equal(new Thing?[] { first, second }, radar.Threats.ToList().Select(t => t.Round.ToRead));
        Assert.Equal(80.0f, radar.Threats.First()!.Distance);
        Assert.Equal(2, alarms);
        Assert.True(radar.NewThreat);
        Assert.Equal(0.0f, radar.NewThreatTime);
        Assert.Equal(new[] { RadarWarningReceiver.UpdateEvent, UnitEvent.IncomingMissileWarning },
            c.Events.Log.Where(e => e.Kind == 'A').Select(e => e.EventNum));
        Assert.Equal(0, c.Random.Draws);
        Assert.Equal(0, c.Crt.Calls);
    }

    [Fact]
    public void ExistingThreatUpdatesWithoutAlarmAndReplacementNotifiesBeforeUnseenPruning()
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        int alarms = 0;
        var radar = Radar(c, be, () => alarms++);
        Round old = Round(c, be, new FVector(0, 10, 0));
        radar.Update();
        RadarThreat saved = radar.Threats.First()!;
        old.Pos = new FVector(0, 20, 0);
        c.Events.Log = [];
        radar.Update();
        Assert.Same(saved, radar.Threats.First());
        Assert.Equal(20.0f, saved.Distance);
        Assert.Equal(1, alarms);
        Assert.DoesNotContain(c.Events.Log, e => e.EventNum == UnitEvent.IncomingMissileWarning);

        old.ReleaseTarget();
        Round replacement = Round(c, be, new FVector(0, 30, 0));
        c.Events.Log.Clear();
        radar.Update();
        Assert.Equal(1, radar.Threats.Size);
        Assert.Same(replacement, radar.Threats.First()!.Round.ToRead);
        Assert.Null(saved.Round.ToRead);
        Assert.Contains(c.Events.Log, e => e.EventNum == UnitEvent.IncomingMissileWarning);
    }

    [Theory]
    [InlineData(0, 10, 0, 1)]
    [InlineData(10, 0, 0, 0)]
    [InlineData(0, -10, 0, 0)]
    [InlineData(0, -10, 3.13f, 1)]
    [InlineData(0, -10, -3.13f, 1)]
    public void AlarmUsesWrappedFacingAndOnlyNewThreats(float x, float y, float facing, int expectedAlarms)
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        be.CurrentOrientation.Yaw = facing;
        int alarms = 0;
        var radar = Radar(c, be, () => alarms++);
        Round(c, be, new FVector(x, y, 0));
        radar.Update();
        radar.Update();
        Assert.Equal(expectedAlarms, alarms);
    }

    [Fact]
    public void SetReleaseAndRoundShutdownMaintainTheRealOrderedSetAndThreatReaders()
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        var other = new BattleEngine(c);
        other.SetThingType(0);
        var radar = Radar(c, be);
        Round a = Round(c, be, new FVector(0, 10, 0));
        Round b = Round(c, be, new FVector(0, 20, 0));
        Assert.Equal(new Thing[] { b, a }, c.World.Set110.ToList());
        a.SetTarget(other, true);
        Assert.Equal(new Thing[] { a, b }, c.World.Set110.ToList());
        a.ReleaseTarget();
        Assert.Equal(new Thing[] { b }, c.World.Set110.ToList());
        radar.Update();
        RadarThreat threat = radar.Threats.First()!;
        b.Shutdown();
        Assert.Empty(c.World.Set110.ToList());
        Assert.Null(threat.Round.ToRead);
        radar.Update();
        Assert.Equal(0, radar.Threats.Size);
    }

    [Fact]
    public void ReceiverDeletionReleasesThreatReadersAndDoesNotInventDuplicateSuppression()
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        var radar = Radar(c, be);
        Round round = Round(c, be, new FVector(0, 10, 0));
        round.SetTarget(be, false); // Native Add permits duplicates; Release removes only the first.
        Assert.Equal(2, c.World.Set110.Size);
        radar.Update();
        Assert.Equal(1, radar.Threats.Size);
        RadarThreat threat = radar.Threats.First()!;
        radar.Delete();
        Assert.Null(threat.Round.ToRead);
        Assert.Equal(0, round.DeletionCount);
        Assert.Equal(0, radar.DeletionCount);
        Assert.Equal(0, radar.Threats.Size);
        round.ReleaseTarget();
        Assert.Equal(1, c.World.Set110.Size);
        Assert.Null(round.Target.ToRead);
    }

    [Fact]
    public void ATargetReaderThatDiesIsNotStillAScanCandidateAndOtherEventsDoNotScan()
    {
        ThingContext c = Context();
        var be = new BattleEngine(c);
        be.SetThingType(0);
        var radar = Radar(c, be);
        Round round = Round(c, be, new FVector(0, 10, 0));
        be.Destroy();
        Assert.Null(round.Target.ToRead);
        radar.Update();
        Assert.Equal(0, radar.Threats.Size);
        int pending = c.Events.TotalEvents;
        var unrelated = new Event();
        unrelated.SetNum(9999);
        radar.HandleEvent(unrelated);
        Assert.Equal(pending, c.Events.TotalEvents);
        Assert.False(radar.NewThreat);
    }
}
