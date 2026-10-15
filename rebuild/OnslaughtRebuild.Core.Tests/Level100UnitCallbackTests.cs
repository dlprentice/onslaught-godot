// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Level 100's construction draws and every unit's recurring callbacks, each
/// checked in a world of one materialized row so that every draw and due time
/// is predictable. Evidence: the RE lane's construction-order contract
/// (<c>reverse-engineering/game-mechanics/level100-construction-order.md</c>,
/// commits <c>b8a19b68</c>, <c>c3fff6c4</c> and <c>0aa1ceac</c>) and the
/// final-wave contract's AI-owner, 4003 and fire-control sections.
/// </summary>
public sealed class Level100UnitCallbackTests
{
    /// <summary>One materialized row and its motion definition, if any.</summary>
    private static Level100ActorDefinitionSet Definitions(string name)
    {
        Level100ActorDefinitionSet materialized = Level100TestActorDefinitions.LoadMaterialized();
        Level100ActorDefinition row = materialized.Actors.First(item => item.Name == name) with { AuthoredOrder = 0 };
        return new Level100ActorDefinitionSet(
            [row],
            [],
            materialized.WaypointPaths,
            materialized.MotionDefinitions.Where(motion => motion.DefinitionName == row.DefinitionName));
    }

    private static Level100ActorMechanics World(string name, out Level100ActorRegistry registry,
        out Level100ActorId actorId)
    {
        Level100ActorDefinitionSet definitions = Definitions(name);
        registry = new Level100ActorRegistry(definitions);
        actorId = Assert.IsType<Level100ActorId>(registry.GetThingRef(name));
        return new Level100ActorMechanics(registry, definitions);
    }

    /// <summary>
    /// The filed events, less the world's script carriers: those belong to the
    /// level, not to the one row these worlds build.
    /// </summary>
    private static RetailEventSlotSnapshot[] Filed(Level100ActorMechanics mechanics)
    {
        RetailEventSchedulerSnapshot events = mechanics.Snapshot.PlaneEvents!;
        Dictionary<int, RetailEventSlotSnapshot> slots = events.Slots.ToDictionary(slot => slot.Handle);
        return events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow)
            .Select(handle => slots[handle])
            .Where(slot => !Level100ActorMechanics.IsCarrierScriptListener(slot.Listener))
            .ToArray();
    }

    private static uint Bits(double value) => BitConverter.SingleToUInt32Bits((float)value);

    private static double Add(double left, double right) => RetailFloat24.Add(left, right);

    private static double Multiply(double left, double right) => RetailFloat24.Multiply(left, right);

    /// <summary><c>AddEvent(time_from_now)</c>: the float delay added to now and stored as a float.</summary>
    private static uint TimeFromNow(float now, double delay) =>
        BitConverter.SingleToUInt32Bits((float)Add(now, (float)delay));

    [Fact]
    public void Building_TakesItsActorDrawThenRefreshesAndIdlesOnTheFirstFrame()
    {
        Level100ActorMechanics mechanics = World("Control Tower", out _, out _);
        var random = new Level100ReleasedRandom();
        random.Next(); // Actor Init
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        Assert.Equal([4003, 3000], Filed(mechanics).Select(slot => (int)slot.EventNum));

        // Frame 1 delivers both in filing order. 4003: one draw, then now +
        // 3.0 + (r mod 65536)/65536. The active AI with no target takes
        // Update's idle arm: one draw, far from any camera now + 3.0 +
        // (r mod 65536)·2^-15.
        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        int refresh = random.Next() % 65536;
        int idle = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot[] filed = Filed(mechanics);
        Assert.Equal(TimeFromNow(now, Add(3.0, Multiply(refresh, 1.0 / 65536.0))),
            Assert.Single(filed, slot => slot.EventNum == 4003).TimeBits);
        Assert.Equal(Bits(Add(now, Add(Multiply(idle, 2.0 / 65536.0), 3.0))),
            Assert.Single(filed, slot => slot.EventNum == 3000).TimeBits);
        Assert.False(Assert.Single(mechanics.Snapshot.UnitCallbacks!).NearCamera);
    }

    [Fact]
    public void InactiveFactory_PollsThroughThreeThousandThree()
    {
        Level100ActorMechanics mechanics = World("Tank Factory", out _, out _);
        var random = new Level100ReleasedRandom();
        random.Next();
        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        random.Next(); // 4003
        int poll = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot pending = Assert.Single(Filed(mechanics), slot => slot.EventNum == 3003);
        Assert.Equal(Bits(Add(Add(now, 2.0), Multiply(poll, 1.0 / 32768.0))), pending.TimeBits);

        // 3003 files 3000 for the next frame (stored at now + 0.0001), and
        // that 3000 polls again with a new 3003.
        uint frame = 1;
        while (Filed(mechanics).Any(slot => slot.EventNum == 3003))
        {
            mechanics.AdvanceTick();
            frame++;
            Assert.True(frame < 200, "The poll's 3003 was never delivered.");
        }
        float deliveredAt = RetailEventScheduler.TimeAtFrameCount(frame);
        Assert.Equal(TimeFromNow(deliveredAt, 0.0001f),
            Assert.Single(Filed(mechanics), slot => slot.EventNum == 3000).TimeBits);
        mechanics.AdvanceTick();
        Assert.DoesNotContain(Filed(mechanics), slot => slot.EventNum == 3000);
        Assert.Single(Filed(mechanics), slot => slot.EventNum == 3003);
    }

    [Fact]
    public void Turret_TakesFireControlAtConstructionAndRefreshesItUntilDying()
    {
        Level100ActorMechanics mechanics = World("Turret 01", out Level100ActorRegistry registry, out Level100ActorId turret);
        var random = new Level100ReleasedRandom();
        int actor = random.Next();
        int fireControl = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        // Filed 4001, 4003, 3000; the 4001 lands in bucket 1 when its delay
        // exceeds the 0.051 immediate window.
        Assert.Equal([3000, 4001, 4003], Filed(mechanics).Select(slot => (int)slot.EventNum).Order());
        Assert.Equal([4003, 3000], Filed(mechanics).Select(slot => (int)slot.EventNum).Where(num => num != 4001));
        Assert.Equal(Bits(Add(Multiply(fireControl, 1.52587890625e-06f), 0.0)),
            Assert.Single(Filed(mechanics), slot => slot.EventNum == 4001).TimeBits);

        // Multiplier 4: q = r mod 4 decides whether the first full Move is on
        // frame 1, 2 or 3.
        Level100UnitCallbackSnapshot unit = Assert.Single(mechanics.Snapshot.UnitCallbacks!);
        Assert.Equal(Level100ConstructionClass.Cannon, unit.Class);
        Assert.Equal(1 + Math.Max((actor % 4) - 1, 0), unit.FirstMoveFrame);

        // A dying turret's refresh returns without a draw or a requeue.
        registry.SetHealth(turret, 0);
        Assert.True(registry.ReportGroundUnitDied(turret) || registry.GetLifecycle(turret) != Level100ActorLifecycle.Alive);
        for (int frame = 0; frame < 6; frame++)
        {
            mechanics.AdvanceTick();
        }
        Assert.DoesNotContain(Filed(mechanics), slot => slot.EventNum == 4001);
    }

    [Fact]
    public void Warehouse_StartsWithItsTargetAndRefreshesThreeThousandOne()
    {
        // Its script's INIT_SCRIPT first, then 4003, its ready() and the AI
        // (the construction contract's row 11: 2001; ...; 4003; 2003; AI 3001).
        Level100ActorMechanics mechanics = World("Target Warehouse", out _, out Level100ActorId warehouse);
        Assert.Equal([2001, 4003, 2003, 3001], Filed(mechanics).Select(slot => (int)slot.EventNum));
        Assert.Equal(
            [Level100ActorMechanics.ActorScriptListener(warehouse), Level100ActorMechanics.ActorScriptListener(warehouse)],
            Filed(mechanics).Where(slot => slot.EventNum is 2001 or 2003).Select(slot => slot.Listener));
        var random = new Level100ReleasedRandom();
        random.Next();
        mechanics.AdvanceTick();
        float now = RetailEventScheduler.TimeAtFrameCount(1);
        random.Next(); // 4003
        int search = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        Assert.Equal(Bits(Add(Add(now, 1.0), Multiply(search, 1.0 / 65536.0))),
            Assert.Single(Filed(mechanics), slot => slot.EventNum == 3001).TimeBits);
    }

    [Fact]
    public void TargetTank_TakesItsSquadDrawsAndSeedsItsMovePhase()
    {
        Level100ActorMechanics mechanics = World("Target Tank 2", out _, out Level100ActorId tank);
        var random = new Level100ReleasedRandom();
        int actor = random.Next();
        random.Next(); // hover
        int squad4000 = random.Next() % 65536;
        int squad4001 = random.Next() % 65536;
        int squad4002 = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot[] filed = Filed(mechanics);
        Assert.Equal(TimeFromNow(0.0f, Add(2.0, Multiply(squad4000, 1.0 / 32768.0))),
            Assert.Single(filed, slot => slot.EventNum == 4000).TimeBits);
        Assert.Equal(TimeFromNow(0.0f, Add(1.0, Multiply(squad4001, 1.0 / 65536.0))),
            Assert.Single(filed, slot => slot.EventNum == 4001).TimeBits);
        Assert.Equal(TimeFromNow(0.0f, Add(0.99f, Multiply(squad4002, 1.5258789e-07f))),
            Assert.Single(filed, slot => slot.EventNum == 4002).TimeBits);

        // The mechanics state exists from construction; its phase reaches 0
        // on the Actor draw's first full Move.
        int lowFrequencyMoves = Math.Max((actor % 4) - 1, 0);
        Level100ActorCommandIntentSnapshot state = Assert.Single(mechanics.Snapshot.Actors, item => item.ActorId == tank);
        Assert.Equal((4 - lowFrequencyMoves) % 4, state.GroundFullGuideBaseTickPhase);
    }

    [Fact]
    public void RefreshSetsNearCameraStrictlyInsideFiftyUnits()
    {
        Level100ActorMechanics mechanics = World("Control Tower", out Level100ActorRegistry registry, out Level100ActorId tower);
        SimVector3 position = registry.GetPose(tower).PositionMillimeters;
        mechanics.AdvanceEventClock(1);
        mechanics.AdvanceTick(1, cameraPosition: position with { X = position.X + 49_999 });
        Assert.True(Assert.Single(mechanics.Snapshot.UnitCallbacks!).NearCamera);

        Level100ActorMechanics far = World("Control Tower", out _, out _);
        far.AdvanceEventClock(1);
        far.AdvanceTick(1, cameraPosition: position with { X = position.X + 50_000 });
        Assert.False(Assert.Single(far.Snapshot.UnitCallbacks!).NearCamera);
    }

    [Fact]
    public void BaseWorldPass_StartsTwoInfluenceChainsAroundTheWarmUps()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        var registry = new Level100ActorRegistry(definitions);
        var mechanics = new Level100ActorMechanics(registry, definitions);

        // Pines, then the influence map's first draw and 1000; the rows; the
        // Target Truck and Target Drone warm-ups; the tail's draw and 1000.
        var random = new Level100ReleasedRandom();
        for (int pine = 0; pine < definitions.BaseWorldPineCount; pine++) random.Next();
        int first = random.Next() % 65536;
        int rows = Level100ActorWeaponTests.RowDraws(definitions.Actors);
        for (int draw = 0; draw < rows + 4; draw++) random.Next();
        int tail = random.Next() % 65536;
        Assert.Equal(random.Seed, mechanics.Snapshot.ReleasedRandomSeed);
        RetailEventSlotSnapshot[] influence = Filed(mechanics)
            .Where(slot => slot.Listener == Level100ActorMechanics.InfluenceMapListener).ToArray();
        Assert.Equal(2, influence.Length);
        Assert.All(influence, slot => Assert.Equal(1000, slot.EventNum));
        Assert.Equal(
            new[] { first, tail }.Select(sample => TimeFromNow(0.0f, Add(1.0, Multiply(sample, 1.0 / 65536.0)))).Order(),
            influence.Select(slot => slot.TimeBits).Order());

        // Each delivery takes one draw and files the chain's next 1000.
        int delivered = 0;
        for (int frame = 0; frame < 30 && delivered == 0; frame++)
        {
            mechanics.AdvanceTick();
            delivered = Filed(mechanics).Count(slot =>
                slot.Listener == Level100ActorMechanics.InfluenceMapListener &&
                BitConverter.UInt32BitsToSingle(slot.TimeBits) > 2.0f);
        }
        Assert.Equal(1, delivered);
        Assert.Equal(2, Filed(mechanics).Count(slot => slot.Listener == Level100ActorMechanics.InfluenceMapListener));
    }

    [Fact]
    public void UnitCallbacks_RestoreFromTheSnapshotAndReplayIdentically()
    {
        Level100ActorMechanics mechanics = World("Turret 01", out Level100ActorRegistry registry, out _);
        for (int frame = 0; frame < 3; frame++)
        {
            mechanics.AdvanceTick();
        }
        Level100ActorDefinitionSet definitions = Definitions("Turret 01");
        var restoredRegistry = new Level100ActorRegistry(definitions, registry.Snapshot);
        var restored = new Level100ActorMechanics(restoredRegistry, definitions, mechanics.Snapshot);
        for (int frame = 0; frame < 40; frame++)
        {
            mechanics.AdvanceTick();
            restored.AdvanceTick();
        }
        Assert.Equal(mechanics.Snapshot.ReleasedRandomSeed, restored.Snapshot.ReleasedRandomSeed);
        Assert.Equal(Filed(mechanics).Select(slot => (slot.EventNum, slot.TimeBits)),
            Filed(restored).Select(slot => (slot.EventNum, slot.TimeBits)));
    }
}
