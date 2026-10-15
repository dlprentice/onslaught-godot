// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
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
        string path = "$SCRATCH";
        return File.ReadLines(path).Where(l => l.Length > 0 && char.IsDigit(l[0]))
            .Select(l => l.Split('\t'))
            .Select(f => (long.Parse(f[0]), f[1].Split(' ')[0]))
            .ToList();
    }

    /// <summary>Our C# draw sites as retail's function names.</summary>
    private static string RetailName(string site) => site switch
    {
        "Tree.Init" => "CTree::Init",
        "Actor.Init" => "CActor::Init",
        "Unit.Unknown4fb280" => "CUnit::Unknown4fb280",
        "InfluenceMap.Build" => "CInfluenceMap::Build",
        "Units.Init" => "CGroundUnit::Init or CPlane::Init",
        "BattleEngine.Init" => "CBattleEngine::Init",
        "BattleEngine.HandleAutoAim" => "CBattleEngine::HandleAutoAim",
        _ => site,
    };

    [GameDataFact]
    public void BaseWorld1DrawsAsRetailDoesSiteBySite()
    {
        if (!File.Exists("$SCRATCH"))
        {
            return; // the port log is a local file of this machine
        }

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
}
