// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>The squads, the influence map, the fear grid and the simple things, on level 100's height field.</summary>
public sealed class SquadTests
{
    private static readonly Lazy<(HeightField Map, PhysicsRegistry Physics)> Level100 = new(() =>
    {
        (string name, byte[] bytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        var c = new ChunkReader(MemBuffer.FromFile(name, bytes));
        uint tag;
        while ((tag = c.GetNext()) != 0)
        {
            if (tag == ChunkReader.MakeId("ERES"))
            {
                return (EngineResources.Read(c).HeightField, PhysicsRegistry.Build(PhysicsScriptTests.Load()));
            }

            c.Skip();
        }

        throw new InvalidDataException("no ERES chunk");
    });

    private static ThingContext NewContext(bool withGame = false)
    {
        var pool = new SPtrSetPool();
        pool.Init(SPtrSetPool.RetailBlockSize);
        var events = new EventManager();
        events.Init();
        var random = new SeedUniformRandomNumberStream(123456) { Log = [] };
        var context = new ThingContext(pool, events, random, Level100.Value.Map, Level100.Value.Physics);
        if (withGame)
        {
            var game = new ParityGame(new PlatformClock(), events);
            game.InitRestartLoop();
            context.Game = game;
        }

        return context;
    }

    private static byte[] B(string s) => Encoding.ASCII.GetBytes(s);

    /// <summary>A point on level 100's ground (the test unit can go under water, so it stays on the ground).</summary>
    private static readonly Lazy<FVector> DryLand = new(() =>
        new FVector(200, 200, Level100.Value.Map.Collide(new FVector(200, 200, 0))));

    /// <summary>A unit with the behaviour the squad code reads, fixed.</summary>
    private sealed class FakeUnit(ThingContext context) : ComplexThing(context, Oid.Unit), ISquadUnit
    {
        public Squad? SquadOf;
        public int Allegiance = 1;
        public float Threat = 2;
        public float Importance = 3;
        public float Speed = 10;
        public FVector? WentTo;

        public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Unit | ThingType.Vehicle);

        public override float GetRadius() => 2.5f;

        public override bool CanGoUnderWater() => true;

        public override float GetMaxVelocity() => 5;

        public override void GoToPoint(FVector point, bool inOverride = false) => WentTo = point;

        public Squad? GetSquad() => SquadOf;

        public void SetSquad(Squad? squad) => SquadOf = squad;

        public IOccupancyGrid? GetOccupancyGrid() => null;

        public float Unknown111() => Speed;

        public float GetThreat() => Threat;

        public float GetImportance() => Importance;

        public bool IsAThreat() => true;

        public float GetStealth() => 0;

        public int GetAllegiance() => Allegiance;

        public void SetAllegiance(int allegiance) => Allegiance = allegiance;

        public bool Unknown4fb3d0(ISquadUnit target) => true;

        public void Unknown4fb840(Thing target)
        {
        }

        public float GetMinRange(FVector target) => 0;

        public float GetMaxRange(FVector target) => 100;

        public bool Unknown4fb5a0(ISquadUnit target) => true;

        public bool AnyWeaponUnknown94() => true;

        public void GuideUnknown7(FVector pos)
        {
        }

        public void SetAITarget(ISquadUnit? target)
        {
        }

        public ISquadUnit? GetCurrentTarget() => null;

        public bool IsOnGround() => true;
    }

    private static InitThing SquadInit(FVector pos, int amount, int allegiance) => new()
    {
        Kind = InitThingRecord.InitKind.Squad,
        Pos = pos,
        Amount = amount,
        UnitName = B("Target Tank"),
        Allegiance = allegiance,
    };

    [GameDataFact]
    public void ANormalSquadsInitDrawsForItsThreeEventsInRetailOrder()
    {
        ThingContext context = NewContext();
        var units = new List<FakeUnit>();
        var squad = new NormalSquad(context) { SpawnUnit = _ => { var u = new FakeUnit(context); units.Add(u); return u; } };
        squad.Init(SquadInit(DryLand.Value, 1, 1));

        // The three draws of a Target Tank squad's Init (the port's log, draws 1525-1527): event 4000, 4001, 4002.
        Assert.Equal(new[] { "Squad.Unknown4e8100", "Squad.Unknown4e83b0", "Squad.Unknown66" },
            context.Random.Log!.Select(d => d.Site));
        FakeUnit unit = Assert.Single(units);
        Assert.Same(squad, unit.SquadOf);
        Assert.Equal(1, squad.UnknownB4);
        Assert.Equal(2.5f, squad.Unknown104);
        Assert.Equal(10 * 0.06000000238418579f, squad.Unknown10C);
        Assert.Contains(squad, context.World.Set30.ToList());
        Assert.Contains(squad, context.World.SquadNB.ToList());
        Assert.Equal(ThingType.Unknown20000000, squad.ThingTypeBits & ThingType.Unknown20000000);

        // The events: 4000 at FloatRandom()*2 + 2, 4001 at FloatRandom() + 1, 4002 at FloatRandom()*0.01 + 0.99.
        var replay = new SeedUniformRandomNumberStream(123456);
        float r1 = replay.FloatRandom();
        float r2 = replay.FloatRandom();
        float r3 = replay.FloatRandom();
        List<ScheduledEvent> scheduled = AllScheduled(context.Events).Where(e => e.ToCall == squad).ToList();
        Assert.Equal((r1 * 2.0f) + 2.0f, scheduled.Single(e => e.EventNum == SquadEvent.ChooseTarget).Time);
        Assert.Equal(r2 + 1.0f, scheduled.Single(e => e.EventNum == SquadEvent.CheckMembers).Time);
        Assert.Equal((r3 * 0.01f) + 0.99f, scheduled.Single(e => e.EventNum == SquadEvent.Update).Time);
    }

    private static IEnumerable<ScheduledEvent> AllScheduled(EventManager events)
    {
        for (int i = 0; i < EventManager.BufferCount; i++)
        {
            foreach (EventPriority p in Enum.GetValues<EventPriority>())
            {
                foreach (ScheduledEvent e in events.Buffer(i, p))
                {
                    yield return e;
                }
            }
        }

        foreach (ScheduledEvent e in events.Overflow)
        {
            yield return e;
        }
    }

    [GameDataFact]
    public void ANormalSquadPlacesItsMembersInASquareBlock()
    {
        ThingContext context = NewContext();
        var squad = new NormalSquad(context) { SpawnUnit = _ => new FakeUnit(context) };
        squad.Init(SquadInit(DryLand.Value, 4, 1));

        // Four members, width 2: the marching formation puts columns 0, 1 at +r and -2r+r (offset r for an even width).
        List<NormalSquadMember> members = squad.Members.ToList();
        Assert.Equal(4, members.Count);
        Assert.Equal(4, squad.UnknownB4);
        Assert.Equal(1, squad.UnknownBC);
        Assert.Equal(new[] { 0.0f, 0.0f, -5.0f, -5.0f }, members.Select(m => m.Unknown08).OrderByDescending(v => v));
    }

    [GameDataFact]
    public void ARelaxedSquadSchedulesItsUpdateFiveToTenSecondsOn()
    {
        ThingContext context = NewContext();
        var squad = new RelaxedSquad(context) { SpawnUnit = _ => new FakeUnit(context) };
        squad.Init(SquadInit(DryLand.Value, 2, 1));
        Assert.Equal(new[] { "Squad.Unknown66" }, context.Random.Log!.Select(d => d.Site));
        Assert.Equal(2, squad.Members.Size);
        float r = new SeedUniformRandomNumberStream(123456).FloatRandom();
        Assert.Equal((r * 5.0f) + 5.0f, AllScheduled(context.Events).Single(e => e.ToCall == squad).Time);
    }

    [Fact]
    public void SpawnSquadGivesMechsAndInfantryARelaxedSquadAndLoneKindsNone()
    {
        var context = new ThingContext(new SPtrSetPool(), new EventManager(), new SeedUniformRandomNumberStream(1),
            null!, new PhysicsRegistry());
        Assert.IsType<RelaxedSquad>(Squad.SpawnSquad(context, UnitBehaviour.Mech));
        Assert.IsType<RelaxedSquad>(Squad.SpawnSquad(context, UnitBehaviour.Infantry));
        Assert.IsType<NormalSquad>(Squad.SpawnSquad(context, UnitBehaviour.GroundUnit));
        Assert.IsType<NormalSquad>(Squad.SpawnSquad(context, UnitBehaviour.Warspite));
        Assert.Null(Squad.SpawnSquad(context, UnitBehaviour.Turret));
        Assert.Null(Squad.SpawnSquad(context, UnitBehaviour.SimpleBuilding));
        Assert.True(Squad.IsSquadBehaviour(UnitBehaviour.SimpleBuilding));
        Assert.False(Squad.IsSquadBehaviour(UnitBehaviour.Dropship));
    }

    [GameDataFact]
    public void TheInfluenceMapLoadsBuildsWithOneDrawAndSchedulesItsEvents()
    {
        ThingContext context = NewContext();
        var map = new InfluenceMap(context);
        var record = new InfluenceMapRecord(0,
            [(100, 100, 10, 0), (200, 100, 10, 0), (300, 100, 10, 0)],
            [(0, 1, 1), (1, 2, 1)]);
        var forseti = new FakeUnit(context) { Allegiance = 0 };
        forseti.Init(new InitThing { Pos = new FVector(105, 100, 0) });
        context.World.Set20.Append(forseti);
        var muspell = new FakeUnit(context);
        muspell.Init(new InitThing { Pos = new FVector(300, 100, 0) });
        context.World.Set30.Append(muspell);

        map.Load(record);

        Assert.Equal(new[] { "InfluenceMap.Build" }, context.Random.Log!.Select(d => d.Site));
        InfluenceNode[] nodes = map.Nodes.ToList().ToArray();
        Assert.Equal(3, nodes.Length);
        Assert.All(nodes, n => Assert.Equal((ThingFlags)0, n.Flags & ThingFlags.InMapWho));
        Assert.Equal(2.0f, nodes[0].Threat[0]);
        Assert.Equal(3.0f, nodes[0].Threat[1]);
        Assert.Equal(2.0f, nodes[2].ThreatB[0]);
        // The empty middle node takes a tenth of its neighbours' threat; both sides balance there.
        Assert.Equal(0.2f, nodes[1].Threat[0]);
        Assert.Equal(1.0f, nodes[0].Influence);
        Assert.Equal(-1.0f, nodes[2].Influence);
        Assert.Equal(0.0f, nodes[1].Influence);
        Assert.Equal(new[] { 0, 1, 2 }, nodes.Select(n => n.Distance[1]));

        float r = new SeedUniformRandomNumberStream(123456).FloatRandom();
        List<ScheduledEvent> scheduled = AllScheduled(context.Events).Where(e => e.ToCall == map).ToList();
        Assert.Equal(r + 0.0f + 1.0f, scheduled.Single(e => e.EventNum == InfluenceMap.BuildEvent).Time);
        Assert.Equal(0.5f, scheduled.Single(e => e.EventNum == InfluenceMap.DecayEvent).Time);
        Assert.Same(nodes[1], map.GetNearestNode(205, 95));
    }

    [GameDataFact]
    public void AResidualFadesByAFifthEachDecay()
    {
        ThingContext context = NewContext();
        var map = new InfluenceMap(context);
        map.Load(new InfluenceMapRecord(1, [(100, 100, 10, BitConverter.SingleToInt32Bits(0.5f))], []));
        Assert.Equal(0.5f, map.Nodes.First()!.Weight);
        map.AddResidual(100, 100, 0.3f, 0);
        map.Decay();
        Assert.Equal(1, map.Residuals.Size);
        map.Decay();
        Assert.Equal(0, map.Residuals.Size);
    }

    [Fact]
    public void TheFearGridFindsTheNearestUnfearedCellInRings()
    {
        var events = new EventManager();
        events.Init();
        var grid = new FearGrid(events, 0);
        Assert.False(grid.InDanger(new FVector(100, 100, 0)));
        var pos = new FVector(100, 100, 0);
        grid.Unknown44c810(ref pos);
        Assert.Equal(new FVector(100, 100, 0), pos); // its own cell is clear: left alone
        Assert.Single(events.Buffer(19, EventPriority.StartOfFrame)); // the rebuild a second on: buffer 19
    }

    [Fact]
    public void InitRestartLoopMakesBothFearGridsAfterThePreRunEvent()
    {
        var events = new EventManager();
        var game = new ParityGame(new PlatformClock(), events);
        game.InitRestartLoop();
        Assert.NotNull(game.ForsetiFearGrid);
        Assert.NotNull(game.MuspellFearGrid);
        Assert.Equal(new[] { game.ForsetiFearGrid, game.MuspellFearGrid },
            events.Buffer(19, EventPriority.StartOfFrame).Select(e => e.ToCall));
        Assert.Equal(3, events.TotalEvents);
    }

    [GameDataFact]
    public void TheSimpleThingsJoinTheirListsAndKeepTheirInits()
    {
        ThingContext context = NewContext(withGame: true);
        var first = new Waypoint(context);
        var second = new Waypoint(context);
        second.Init(new InitThing { Pos = new FVector(100, 100, 5000) });
        first.Init(new InitThing { Pos = new FVector(50, 50, 5000), TargetThing = second });
        Assert.Equal(new Thing[] { second, first }, context.World.Waypoints.ToList());
        Assert.Same(second, first.Next.ToRead);
        // CThing::Init: down to the ground if below it (Z points down), then up to the water level if below that.
        Assert.Equal(MathF.Min(context.Map.Collide(first.Pos), context.WaterLevel), first.Pos.Z);

        var safeA = new SafeSide(context);
        safeA.Init(new InitThing { Allegiance = 0 });
        var safeB = new SafeSide(context);
        safeB.Init(new InitThing { Allegiance = 1 });
        Assert.Equal(new Thing[] { safeB, safeA }, context.World.SafeSides.ToList()); // Add: at the head
        Assert.Equal(1, safeB.GetAllegiance());

        var trigger = new SphereTrigger(context);
        var init = new InitThing { Radius = 12 };
        trigger.Init(init);
        Assert.Equal(12, trigger.GetRadius());
        Assert.Equal(ThingType.Stationary, init.InitCst.NotSeekCollisionWith);
        Assert.Equal(0x08000000u | ThingType.Stationary, trigger.ThingTypeBits & (0x08000000u | ThingType.Stationary));

        context.Game!.Settings.InvertSides = true;
        context.Game.Settings.Player2Configuration = 4;
        var start = new Start(context);
        start.Init(new InitThing { PlayerNumber = 1, PlaneMode = 1, Pos = new FVector(10, 10, 9000) });
        Assert.Equal(2, start.PlayerNumber);
        Assert.Equal(4, start.ConfigurationId);
        Assert.Equal(1, start.BattleEngineInit.PlaneMode);
        Assert.Equal(context.Map.Collide(new FVector(10, 10, 0)), start.BattleEngineInit.Pos.Z);
        Assert.Contains("CStart::SpawnBattleEngine (no battle engine spawner)", context.NotPorted.Keys);
    }
}
