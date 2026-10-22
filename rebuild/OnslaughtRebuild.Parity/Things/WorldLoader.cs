// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/world.cpp (CWorld::Load 0050b520, LoadWorld 0050b9c0, LoadHeader 0050d4c0, LoadScripts
// 0050ac70). Not in Stuart's release.

using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// The classes the loader makes that other owners port: ::SpawnThing(OID) for the start, waypoints, level-script
/// things, triggers and safe sides, and UPhysicsManager::SpawnSquad. A null return means retail's NULL.
/// </summary>
public interface IWorldSpawner
{
    /// <summary>::SpawnThing(oid) for the classes not made through the physics data.</summary>
    Thing? SpawnThing(int oid);

    /// <summary>UPhysicsManager::SpawnSquad: a squad for units of that behaviour, or null when it takes none.</summary>
    ComplexThing? SpawnSquad(UnitBehaviour behaviour);

    /// <summary>A unit record turned into a 1-unit squad (world.cpp: mAmount = 1, mUnknown84 = TRUE).</summary>
    void MarkSpawnedFromUnitRecord(ComplexThing squad);

    /// <summary>CWaypointManager::Load: the paths, by indices into the loaded things.</summary>
    void LoadWaypointPaths(IReadOnlyList<WaypointPathRecord> paths, IReadOnlyList<Thing?> things);
}

/// <summary>
/// CWorld::Load and LoadWorld: a level's world, read from its resource file and brought to life in retail's order. A
/// real world with a base world loads the base world at the point it reads its level; trees spawn only where retail
/// spawns them; every thing's Init runs after the trees and the influence map, in file order.
/// </summary>
public sealed class WorldLoader(ThingContext context, LevelResources resources, IWorldSpawner spawner)
{
    /// <summary>The things each world made, by world level, in file order (the things[] array).</summary>
    public Dictionary<int, List<Thing?>> ThingsByWorld { get; } = [];

    /// <summary>CWorld::Load(level, base).</summary>
    public void Load(int level, bool isBase)
    {
        (int baseLevel, int worldLevel, uint basePosition, uint worldPosition) = resources.Worlds;
        uint position = isBase
            ? (level == baseLevel ? basePosition : throw new InvalidDataException($"base world {level} is not in the resource file"))
            : (level == worldLevel ? worldPosition : throw new InvalidDataException($"world {level} is not in the resource file"));
        LoadWorld(level, resources.OpenAt(position), isBase, !isBase);
    }

    /// <summary>CWorld::LoadWorld.</summary>
    private void LoadWorld(int level, MemBuffer file, bool isBase, bool real)
    {
        if (real)
        {
            context.InfluenceMap = null;

            // CWorld::InitOccupancy: the three navigation grids by the slopes they climb.
            context.Occupancy.Grids[0] = new OccupancyGrid(context.Occupancy, 35.0f);
            context.Occupancy.Grids[1] = new OccupancyGrid(context.Occupancy, 45.0f);
            context.Occupancy.Grids[2] = new OccupancyGrid(context.Occupancy, 60.0f);
        }

        int version = (ushort)file.ReadInt16();
        if (version < 43 || version > 50)
        {
            throw new InvalidDataException($"world version {version}");
        }

        // LoadHeader: a base world's battle engine configuration names are read past (Skip).
        int headerVersion = file.ReadInt32();
        file.ReadInt32();
        file.ReadInt32();
        IReadOnlyList<byte[]> configurations = BattleEngineConfigurations.Read(file);
        if (!isBase)
        {
            context.WorldBattleEngineConfigurations = configurations;
        }

        if (headerVersion >= 2)
        {
            file.ReadInt32();
            file.ReadInt32();
            file.ReadInt32();
        }

        if (headerVersion >= 3)
        {
            file.ReadInt32();
        }

        if (real)
        {
            context.InfluenceMap ??= new InfluenceMap(context);
        }

        int baseLevel = file.ReadInt32();
        LoadScripts(file);
        if (!isBase && baseLevel != -1)
        {
            Load(baseLevel, true);
        }

        file.ReadInt32(); // the map number (MAP.AlreadyLoaded)
        for (int n = file.ReadInt32(); n > 0; n--)
        {
            file.Read(new byte[file.ReadByte()]); // ENGINE.LoadAllNamedMeshes
        }

        bool spawnsTrees = isBase || baseLevel == -1;
        var things = new List<Thing?>();
        var inits = new List<InitThing>();
        var diesOn = new List<int>();
        LoadThings(file, version, isBase, things, inits, diesOn);
        LoadTrees(file, spawnsTrees);

        InfluenceMapRecord influence = InfluenceMapRecord.Read(file);
        if (isBase || baseLevel == -1)
        {
            (context.InfluenceMap ??= new InfluenceMap(context)).Load(influence);
        }

        for (int i = 0; i < things.Count; i++)
        {
            InitThing init = inits[i];
            init.TargetThing = init.Target >= 0 && init.Target < things.Count ? things[init.Target] : null;
            init.Target = 0;
            if (isBase)
            {
                context.Skip("CAREER.DoesBaseThingExist read as existing (the wiped career)");
                if (diesOn[i] != -1)
                {
                    throw new NotSupportedException("a base thing that dies on a later world (CAREER.IsWorldLater)");
                }
            }

            things[i]!.Init(init);
            if (isBase)
            {
                var reader = new Events.ActiveReader<Thing>();
                reader.SetReader(things[i]);
                context.World.BaseWorldThingNB.Add(reader);
            }
        }

        ThingsByWorld[level] = things;
        spawner.LoadWaypointPaths(ReadWaypointPaths(file, version), things);

        LevelTimings timings = LevelTimings.Read(version, file);
        if (!isBase || baseLevel == -1)
        {
            if (context.Game is { } game)
            {
                game.SetPreRunTime(timings.PreRun);
                game.SetPanTime(timings.Pan);
                game.SetSGradeScore(timings.SGrade);
                game.SetDGradeScore(timings.DGrade);
                game.SetTimeLimit(timings.TimeLimit);
                game.SetFullScoreTime(timings.FullScoreTime);
                game.SetPercentageScoreTime(timings.PercentageScoreTime);
                game.SetScorePercentage(timings.ScorePercentage);
            }
        }

        if (version >= 47)
        {
            // A level on a base world keeps the base world's grids and reads past its own.
            for (int g = 0; g < 3; g++)
            {
                if (!isBase && baseLevel != -1)
                {
                    OccupancyGrid.SkipLegacy(file);
                }
                else if (context.Occupancy.Grids[g] is null)
                {
                    // Only a test loads a world without a real level's InitOccupancy first.
                    context.Skip("COccupancyGrid::Load without CWorld::InitOccupancy (a world loaded on its own)");
                    OccupancyGrid.SkipLegacy(file);
                }
                else
                {
                    context.Occupancy.Grids[g]!.Load(file);
                }
            }

            if (version < 50)
            {
                OccupancyGrid.SkipHeader(file);
            }
        }

        if (version >= 49)
        {
            float startTime = file.ReadSingle();
            if (!isBase)
            {
                context.Game?.SetLevelStartTime(startTime);
            }
        }

        context.Skip("CMissionScriptObjectCode::LoadSpawnedMeshes");
        if (!isBase)
        {
            context.Skip("CWorldMeshList::SpawnUnused");
            context.InfluenceMap!.Build(false);
            context.InfluenceMap.Update();
            if (version < 47)
            {
                Occupancy.ClearSteep(context.Occupancy, context.Map);
            }

            // GAME.IsRestarting() takes ApplyStatic instead; the parity runs start each level afresh.
            Occupancy.BuildStatic(context.Occupancy, context.Map);
        }
    }

    private void LoadScripts(MemBuffer file)
    {
        int count = file.ReadInt32();
        Span<byte> pad = stackalloc byte[10];
        for (int i = 0; i < count; i++)
        {
            byte[] name = Script.ScriptValue.ReadString(file);
            context.Scripts.Add((name, Script.ScriptObjectCode.Read(file)));
            file.Read(pad);
        }
    }

    private void LoadThings(MemBuffer file, int version, bool isBase, List<Thing?> things, List<InitThing> inits,
        List<int> diesOn)
    {
        int count = (ushort)file.ReadInt16();
        for (int i = 0; i < count; i++)
        {
            int type = file.ReadInt32();
            InitThingRecord.InitKind kind = InitThingRecord.KindFor(type);
            if (type is Oid.Unit or Oid.Building)
            {
                InitThing init = InitThing.FromRecord(InitThingRecord.Load(InitThingRecord.InitKind.Unit, version, file));
                byte[] name = file.ReadCountedString();
                int dies = file.ReadInt32();
                init.Stats = context.Physics.GetUnitData(name);
                if (init.Stats is null)
                {
                    continue;
                }

                ComplexThing? squad = spawner.SpawnSquad(init.Stats.Behaviour);
                if (squad is not null)
                {
                    InitThing sinit = new() { Kind = InitThingRecord.InitKind.Squad };
                    sinit.Copy(init);
                    sinit.UnitName = name;
                    sinit.Amount = 1;
                    spawner.MarkSpawnedFromUnitRecord(squad);
                    Add(squad, sinit, dies);
                }
                else
                {
                    Unit? unit = UnitFactory.SpawnThing(context, name);
                    InitThing uinit = new() { Kind = InitThingRecord.InitKind.Unit };
                    uinit.Copy(init);
                    uinit.Stats = init.Stats;
                    Add(unit, uinit, dies);
                }

                context.Skip("CWorldMeshList::Add");
            }
            else if (type == Oid.Squad)
            {
                InitThing init = InitThing.FromRecord(InitThingRecord.Load(kind, version, file));
                init.UnitName = file.ReadCountedString();
                UnitData? data = context.Physics.GetUnitData(init.UnitName);
                int dies = file.ReadInt32();
                ComplexThing? squad = data is null ? null : spawner.SpawnSquad(data.Behaviour);
                if (squad is not null)
                {
                    Add(squad, init, dies);
                }
            }
            else if (type is Oid.Feature or Oid.Hazard)
            {
                InitThing init = InitThing.FromRecord(InitThingRecord.Load(kind, version, file));
                byte[] name = file.ReadCountedString();
                int dies = file.ReadInt32();
                if (type == Oid.Hazard)
                {
                    throw new NotSupportedException("hazards are not ported yet");
                }

                init.FeatureData = context.Physics.GetFeatureData(name);
                if (init.FeatureData is not null)
                {
                    Add(new Feature(context), init, dies);
                }
            }
            else if (type == Oid.Start)
            {
                Thing? start = spawner.SpawnThing(type);
                InitThing init = InitThing.FromRecord(InitThingRecord.Load(kind, version, file));
                if (start is not null && !isBase)
                {
                    Add(start, init, -1);
                }
            }
            else
            {
                Thing? thing = spawner.SpawnThing(type) ?? throw new NotSupportedException(
                    $"::SpawnThing({type}) made nothing; retail would leave its record unread");
                Add(thing, InitThing.FromRecord(InitThingRecord.Load(kind, version, file)), -1);
            }
        }

        void Add(Thing? thing, InitThing init, int dies)
        {
            if (thing is null)
            {
                return;
            }

            things.Add(thing);
            inits.Add(init);
            diesOn.Add(dies);
        }
    }

    /// <summary>The trees: groups scattered from a seed (CSeedUniformRandomNumberStream), then single typed trees.</summary>
    private void LoadTrees(MemBuffer file, bool spawn)
    {
        for (int groups = file.ReadInt16(); groups != 0; groups--)
        {
            float y0 = file.ReadSingle();
            float x0 = file.ReadSingle();
            float y1 = file.ReadSingle();
            float x1 = file.ReadSingle();
            int count = file.ReadInt32();
            byte[] treeType = file.ReadCountedString();
            int seed = file.ReadInt32();
            if (spawn)
            {
                var random = new SeedUniformRandomNumberStream(seed);
                for (; count != 0; count--)
                {
                    var tree = new Tree(context);
                    float fx = (float)(random.Next() % 65536) / 65536.0f;
                    float fy = (float)(random.Next() % 65536) / 65536.0f;
                    var init = new InitThing
                    {
                        Kind = InitThingRecord.InitKind.Tree,
                        TreeType = treeType,
                        TreeTypeComplete = false,
                        Pos = new FVector(x0 + ((x1 - x0) * fx), y0 + ((y1 - y0) * fy), 0),
                    };
                    tree.Init(init);
                }
            }
        }

        for (int types = file.ReadInt32(); types != 0; types--)
        {
            byte[] name = file.ReadCountedString();
            for (int count = file.ReadInt32(); count != 0; count--)
            {
                float x = file.ReadSingle();
                float y = file.ReadSingle();
                int variant = file.ReadInt32();
                if (!spawn)
                {
                    continue;
                }

                byte[] full = [.. name, .. System.Text.Encoding.ASCII.GetBytes(variant.ToString(System.Globalization.CultureInfo.InvariantCulture))];
                string text = System.Text.Encoding.ASCII.GetString(full);
                if (!text.StartsWith("fern", StringComparison.OrdinalIgnoreCase)
                    && !text.StartsWith("bush", StringComparison.OrdinalIgnoreCase))
                {
                    new Tree(context).Init(new InitThing
                    {
                        Kind = InitThingRecord.InitKind.Tree,
                        TreeType = full,
                        TreeTypeComplete = true,
                        Pos = new FVector(x, y, 0),
                    });
                }
            }
        }
    }

    private static List<WaypointPathRecord> ReadWaypointPaths(MemBuffer file, int version)
    {
        var paths = new List<WaypointPathRecord>();
        for (int count = file.ReadInt16(); count != 0; count--)
        {
            byte[] name = file.ReadCountedString();
            var indices = new List<short>();
            for (int n = file.ReadInt32(); n != 0; n--)
            {
                indices.Add(file.ReadInt16());
            }

            paths.Add(new WaypointPathRecord(name, indices));
        }

        return paths;
    }
}
