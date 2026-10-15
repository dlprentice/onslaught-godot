// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Text;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class LevelLoadTests(ITestOutputHelper output)
{
    /// <summary>
    /// TEST STAND-IN for classes another owner is porting (the safe side, a contributor): a complex thing whose Init is
    /// CComplexThing's. Retail's safe-side Init draws nothing (the native port's draw log), so the stand-in cannot hide a
    /// draw; it is used only where the real class has not landed.
    /// </summary>
    private sealed class StandIn(ThingContext context, int oid) : ComplexThing(context, oid);

    private sealed class StandInSpawner(ThingContext context) : IWorldSpawner
    {
        public Thing? SpawnThing(int oid) => oid == Oid.SafeSide
            ? new StandIn(context, oid)
            : throw new NotSupportedException($"OID {oid} is not ported yet");

        public ComplexThing? SpawnSquad(UnitBehaviour behaviour) => behaviour == UnitBehaviour.GroundUnit
            ? throw new NotSupportedException("squads are not ported yet")
            : null;

        public void MarkSpawnedFromUnitRecord(ComplexThing squad)
        {
        }

        public void LoadWaypointPaths(IReadOnlyList<WaypointPathRecord> paths, IReadOnlyList<Thing?> things)
        {
        }
    }

    internal static ThingContext Level100Context(out LevelResources resources)
    {
        (string pname, byte[] pbytes) = GameData.Read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(pname, pbytes)));
        (string bname, byte[] bbytes) = GameData.Read(@"data\battle engine configurations.dat");
        BattleEngineDataManager battleEngines = BattleEngineDataManager.Load(MemBuffer.FromFile(bname, bbytes));
        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        resources = LevelResources.Read(rname, rbytes, GameData.Read);

        var events = new EventManager();
        events.Init();
        var pool = new SPtrSetPool();
        pool.Init(SPtrSetPool.RetailBlockSize);
        var random = new SeedUniformRandomNumberStream(123456) { Log = [] };
        var context = new ThingContext(pool, events, random, resources.HeightField, physics) { BattleEngines = battleEngines };
        foreach ((string name, Mesh.MeshData mesh) in resources.Meshes)
        {
            context.Meshes[name] = mesh;
        }

        return context;
    }

    /// <summary>a contributor's native-port log of the level-100 load: (draw number, retail function) for every non-tree draw.</summary>
    private static List<(long Draw, string Site)> PortLog()
    {
        string path = PortLogs.File("draws-l100-load.txt");
        return File.ReadLines(path).Where(l => l.Length > 0 && char.IsDigit(l[0]))
            .Select(l => l.Split('\t'))
            .Select(f => (long.Parse(f[0]), f[1].Split(' ')[0]))
            .ToList();
    }

    /// <summary>Our C# draw sites as retail's function names.</summary>
    private static string RetailName(string site) => site switch
    {
        "Squad.Unknown4e8100" => "CNormalSquad::Unknown4e8100",
        "Squad.Unknown4e83b0" => "CNormalSquad::Unknown4e83b0",
        "Squad.Unknown66" => "CNormalSquad::Unknown66",
        "Unit.HandleEvent" => "CUnit::HandleEvent",
        "UnitAI.Unknown3" => "CUnitAI::Unknown3",
        "UnitAI.HandleEvent" => "CUnitAI::HandleEvent",
        "UnitAI.Unknown10Step" => "CUnitAI::Unknown10",
        "UnitAI.Unknown8" => "CUnitAI::Unknown8",
        "Guide.Reschedule" => "Reschedule",
        "BattleEngine.CalcUnitOverCrossHair" => "CBattleEngine::CalcUnitOverCrossHair",
        "Tree.Init" => "CTree::Init",
        "Actor.Init" => "CActor::Init",
        "Unit.Unknown4fb280" => "CUnit::Unknown4fb280",
        "InfluenceMap.Build" => "CInfluenceMap::Build",
        "Units.Init" => "CGroundUnit::Init or CPlane::Init",
        "BattleEngine.Init" => "CBattleEngine::Init",
        "BattleEngine.HandleAutoAim" => "CBattleEngine::HandleAutoAim",
        _ => site,
    };

    [PortLogFact]
    public void BaseWorld1DrawsAsRetailDoesSiteBySite()
    {
        ThingContext context = Level100Context(out LevelResources resources);
        var loader = new WorldLoader(context, resources, new StandInSpawner(context));
        loader.Load(1, isBase: true);

        List<(long Draw, string Site)> ours = context.Random.Log!;
        foreach ((string what, int count) in context.NotPorted)
        {
            output.WriteLine($"not ported: {what} x{count}");
        }

        Assert.Equal(1481, ours.Count(d => d.Site == "Tree.Init"));
        List<(long Draw, string Site)> retail = PortLog().Where(d => d.Draw <= ours.Count).ToList();
        List<(long Draw, string Site)> ourNonTree = ours.Where(d => d.Site != "Tree.Init").ToList();
        Assert.Equal(retail.Count, ourNonTree.Count);
        for (int i = 0; i < retail.Count; i++)
        {
            string mine = RetailName(ourNonTree[i].Site);
            Assert.True(retail[i].Draw == ourNonTree[i].Draw && (mine == retail[i].Site || mine.Contains(retail[i].Site)),
                $"draw {retail[i].Draw}: retail {retail[i].Site}, ours {ourNonTree[i].Draw} {ourNonTree[i].Site}");
        }

        output.WriteLine($"base world 1: {ours.Count} draws, {ourNonTree.Count} outside trees, all at retail's numbers and sites");
    }

    [GameDataFact]
    public void TheBaseWorldsThingsAreInTheMapWhoAndRadiusQueriesFindThem()
    {
        ThingContext context = Level100Context(out LevelResources resources);
        var loader = new WorldLoader(context, resources, new StandInSpawner(context));
        loader.Load(1, isBase: true);

        List<Thing> things = context.World.Things.ToList();
        Assert.All(things.Where(t => (t.Flags & ThingFlags.InMapWho) != 0), t => Assert.InRange(t.MapWhoEntry.SectorIn.Layer, 0, 4));
        Assert.Equal(13, things.Count(t => (t.Flags & ThingFlags.InMapWho) == 0)); // the influence map's nodes
        Unit turret = things.OfType<Cannon>().First();
        var found = new List<Thing>();
        for (MapWhoEntry? e = context.MapWho.GetFirstEntryWithinRadius(turret.Pos, 1.0f); e is not null;
             e = context.MapWho.GetNextEntryWithinRadius())
        {
            found.Add(e.Thing);
        }

        Assert.Contains(turret, found);
        output.WriteLine($"{things.Count} things, {context.World.BigThings.Size} big; {found.Count} near the first turret");
    }

    /// <summary>
    /// Level 100 as retail runs it: CGame::InitRestartLoop (the event manager reset, FINISHED_PRE_RUN, the reseed, the
    /// fear grids), then CWorld::Load(100), which loads base world 1 at its level. Every draw is compared with the native
    /// port's log by number and retail function: 1,537 in all, 56 outside the trees.
    /// </summary>
    /// <summary>Level 100 as retail starts it: CGame::InitRestartLoop, then CWorld::Load(100) with the real spawner.</summary>
    internal static (ThingContext Context, ParityGame Game) Level100Run()
    {
        (string pname, byte[] pbytes) = GameData.Read(@"data\default physics.dat");
        PhysicsRegistry physics = PhysicsRegistry.Build(PhysicsScript.Read(MemBuffer.FromFile(pname, pbytes)));
        (string bname, byte[] bbytes) = GameData.Read(@"data\battle engine configurations.dat");
        BattleEngineDataManager battleEngines = BattleEngineDataManager.Load(MemBuffer.FromFile(bname, bbytes));
        (string rname, byte[] rbytes) = GameData.Read(@"data\Resources\100_res_PC.aya");
        LevelResources resources = LevelResources.Read(rname, rbytes, GameData.Read);

        var clock = new Clock.PlatformClock();
        var events = new EventManager();
        var services = new ScriptGameServices(NullGameServices.Instance);
        var game = new ParityGame(clock, events, services) { CurrentLevel = 100 };
        game.InitRestartLoop();
        var pool = new SPtrSetPool();
        pool.Init(SPtrSetPool.RetailBlockSize);
        SeedUniformRandomNumberStream random = game.RandomStream!;
        random.Log = [];
        var context = new ThingContext(pool, events, random, resources.HeightField, physics)
        {
            BattleEngines = battleEngines,
            Game = game,
        };
        foreach ((string name, Mesh.MeshData mesh) in resources.Meshes)
        {
            context.Meshes[name] = mesh;
        }

        // CGame::InitRestartLoop's script part: SCRIPT_EVENT_NB.Init (game.cpp:366) and the message box (384), before
        // the load; TEXT is the English language file.
        ScriptRuntime scripts = ScriptRuntime.Of(context);
        scripts.EventNB.Init();
        services.Runtime = scripts;
        TextDatabase text = TextDatabase.Load(GameData.Read(TextDatabase.FileName(0, americanText: false)).Bytes);
        scripts.Text = text;
        scripts.MessageBox = new Script.MessageBox(context, text, new UnmodelledVoice());

        var spawner = new ParitySpawner(context) { SpawnBattleEngineThing = () => new BattleEngine(context) };
        scripts.Spawner = spawner;
        scripts.Waypoints = spawner.Waypoints;
        new WorldLoader(context, resources, spawner).Load(100, isBase: false);

        // CGame::LoadLevel's players after the load (game.cpp:716), then PostLoadProcess's start positions (783).
        game.CreatePlayers(context, multiplayerWorld: false);
        game.AssignPlayersToStarts(context);
        scripts.Player = n => game.GetPlayer(n) is { } player ? (true, player.BattleEngine) : (false, null);
        return (context, game);
    }

    [PortLogFact]
    public void TheLevel100LoadDrawsAsRetailDoesSiteBySite()
    {
        (ThingContext context, ParityGame game) = Level100Run();
        SeedUniformRandomNumberStream random = context.Random;

        List<(long Draw, string Site)> ours = random.Log!;
        foreach ((string what, int count) in context.NotPorted)
        {
            output.WriteLine($"not ported: {what} x{count}");
        }

        List<(long Draw, string Site)> retail = PortLog();
        List<(long Draw, string Site)> ourNonTree = ours.Where(d => d.Site != "Tree.Init").ToList();
        output.WriteLine($"ours: {ours.Count} draws, {ourNonTree.Count} outside trees; retail: 1537, {retail.Count}");
        for (int i = 0; i < Math.Min(retail.Count, ourNonTree.Count); i++)
        {
            string mine = RetailName(ourNonTree[i].Site);
            Assert.True(retail[i].Draw == ourNonTree[i].Draw && (mine == retail[i].Site || mine.Contains(retail[i].Site)),
                $"draw {retail[i].Draw}: retail {retail[i].Site}, ours {ourNonTree[i].Draw} {ourNonTree[i].Site}");
        }

        Assert.Equal(retail.Count, ourNonTree.Count);
        Assert.Equal(1537, ours.Count);
    }

    /// <summary>
    /// The first 100 Updates after level 100's load (PreRun's 60, then panning), draw by draw against the native port's
    /// trace (draw number, Update, retail function). A measurement while the handlers are being ported: it reports the
    /// first divergence and asserts the matched prefix never shrinks below <see cref="PreRunMatchedFloor"/>.
    /// </summary>
    [PortLogFact]
    public void PreRunDrawsFollowRetailsTrace()
    {
        (ThingContext context, ParityGame game) = Level100Run();
        SeedUniformRandomNumberStream random = context.Random;
        var updateOfDraw = new List<int>();
        int update = 0;
        string stopped = $"ran all {TraceUpdates}";
        while (update < TraceUpdates)
        {
            update++;
            long before = random.Draws;
            try
            {
                game.Update();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped in U{update}: {e.Message}";
                break;
            }
            finally
            {
                for (long d = before; d < random.Draws; d++)
                {
                    updateOfDraw.Add(update);
                }
            }
        }

        var retail = new List<(long Draw, int Update, string Event, string Site)>();
        foreach (string line in File.ReadLines(PortLogs.File("l100-prerun-draws.txt")))
        {
            string[] f = line.Split('\t');
            if (f.Length < 4 || !char.IsDigit(f[0][0]) || f[1] == "U0")
            {
                continue;
            }

            int u = int.Parse(f[1][1..]);
            if (u <= TraceUpdates)
            {
                retail.Add((long.Parse(f[0]), u, f[2], f[3].Split(' ')[0]));
            }
        }

        List<(long Draw, string Site)> ours = random.Log!.Where(d => d.Draw > 1537).ToList();
        int matched = 0;
        string divergence = "none";
        for (; matched < Math.Min(retail.Count, ours.Count); matched++)
        {
            (long draw, int u, string ev, string site) = retail[matched];
            string mine = RetailName(ours[matched].Site);
            int ourUpdate = updateOfDraw[matched];
            if (ours[matched].Draw != draw || ourUpdate != u || !(mine == site || mine.Contains(site)))
            {
                divergence = $"draw {draw}: retail U{u} {ev} {site}; ours U{ourUpdate} {ours[matched].Site}";
                break;
            }
        }

        output.WriteLine($"PreRun: {stopped}; retail {retail.Count} draws, ours {ours.Count}; matched prefix {matched}; first divergence: {divergence}");
        foreach ((string what, int count) in context.NotPorted.Where(kv => !kv.Key.EndsWith("Move (the movement is not ported yet)", StringComparison.Ordinal)))
        {
            output.WriteLine($"not ported: {what} x{count}");
        }

        Assert.True(matched >= PreRunMatchedFloor, $"matched prefix {matched} fell below {PreRunMatchedFloor}: {divergence}");
    }

    /// <summary>
    /// The PreRun event schedule against the native port's log (l100-prerun-events.txt): every AddEvent (event number,
    /// target class and the absolute time it received, as float bits) and every dispatch, Update by Update through U60,
    /// compared in order, with each AddEvent's priority list. The classes not ported yet (<see cref="UnportedEventClasses"/>)
    /// are left out on both sides, keeping the others' order. The port's trace logs no dispatch from the middle- and
    /// end-of-frame lists (its breakpoint at eventmanager.cpp:341 catches the start-of-frame pass only: none of the 1,275
    /// AddEvents with priority 1 or 2 has a dispatch line), so ours leave those out too. A measurement: it reports the first divergence with the lines around it, and asserts
    /// the matched prefix never shrinks below <see cref="PreRunEventsMatchedFloor"/>.
    /// </summary>
    [PortLogFact]
    public void PreRunEventsFollowRetailsLog()
    {
        (ThingContext context, ParityGame game) = Level100Run();
        var ours = new List<string>();
        context.Events.Log = [];
        int update = 0;
        string stopped = $"ran all {TraceUpdates}";
        void Take()
        {
            foreach (EventLogEntry e in context.Events.Log!)
            {
                string target = RetailClass(e.Target);
                if (UnportedEventClasses.Contains(target) || (e.Kind == 'F' && e.Priority != 0))
                {
                    continue;
                }

                ours.Add(e.Kind == 'A'
                    ? $"A U{update} ev {e.EventNum} {target} t {e.TimeBits:x8} p {e.Priority}"
                    : $"F U{update} ev {e.EventNum} {target}");
            }

            context.Events.Log!.Clear();
        }

        while (update < TraceUpdates)
        {
            update++;
            try
            {
                game.Update();
            }
            catch (NotSupportedException e)
            {
                stopped = $"stopped in U{update}: {e.Message}";
                break;
            }
            finally
            {
                Take();
            }
        }

        var retail = new List<string>();
        foreach (string line in File.ReadLines(PortLogs.File("l100-prerun-events.txt")))
        {
            string[] f = line.Split(' ');
            if (f.Length < 5 || f[0] is not ("A" or "F") || f[1].Length < 2 || f[1][0] != 'U' || f[2] != "ev"
                || !int.TryParse(f[1][1..], out int u) || u == 0 || u > update || UnportedEventClasses.Contains(f[4]))
            {
                continue;
            }

            retail.Add(f[0] == "A" ? $"A {f[1]} ev {f[3]} {f[4]} t {f[6]} p {f[^1]}" : $"F {f[1]} ev {f[3]} {f[4]}");
        }

        int matched = 0;
        while (matched < Math.Min(retail.Count, ours.Count) && retail[matched] == ours[matched])
        {
            matched++;
        }

        output.WriteLine($"PreRun events: {stopped}; retail {retail.Count} lines, ours {ours.Count}; matched prefix {matched}");
        for (int i = Math.Max(0, matched - 6); i < Math.Min(matched + 6, Math.Max(retail.Count, ours.Count)); i++)
        {
            output.WriteLine($"{i,5} {(i == matched ? ">>" : "  ")} retail {(i < retail.Count ? retail[i] : "-"),-50} ours {(i < ours.Count ? ours[i] : "-")}");
        }

        Assert.True(matched >= PreRunEventsMatchedFloor, $"matched prefix {matched} fell below {PreRunEventsMatchedFloor}");
    }

    /// <summary>Retail's class name for an event target (the port's log names the C++ class).</summary>
    private static string RetailClass(string? type) => type switch
    {
        null => "(null)",
        "IScript" => "IScript",
        "Game" => "CDXGame",
        "ScriptVm" => "CVM",
        _ => "C" + type,
    };

    /// <summary>The event targets not ported yet, left out of the event comparison on both sides.</summary>
    private static readonly HashSet<string> UnportedEventClasses =
    [
        "CCSPersistentThing", "CHLCollisionDetector", "CRadarWarningReceiver", "CDXCockpit", "CMCBattleEngine",
    ];

    /// <summary>The Updates the native port's level-100 logs cover: PreRun's 60, then panning (state 2) to U100.</summary>
    private const int TraceUpdates = 100;

    /// <summary>The PreRun event lines matched so far (raised as the handlers are ported; never lowered).</summary>
    private const int PreRunEventsMatchedFloor = 14037;

    /// <summary>The PreRun draws matched so far (raised as the handlers are ported; never lowered).</summary>
    private const int PreRunMatchedFloor = 499;
}
