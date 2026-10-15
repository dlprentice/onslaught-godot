// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/world.cpp (CWorld::LoadWorld 0050b9c0, LoadHeader 0050d4c0, LoadScripts 0050ac70,
// Deserialize 0050b780), src/engine.cpp (CEngine::LoadAllNamedMeshes 00449dc0), src/InfluenceMap.cpp
// (CInfluenceMap::Load), src/WaypointManager.cpp (CWaypointManager::Load 00505ae0, CWaypointPath::Load 00505960) and
// src/NavigationMap.cpp (COccupancyGrid::Load 004be050, SkipLegacy 004bdff0, SkipHeader 004be170). Not in Stuart's
// release.

using System.Text;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Script;

namespace OnslaughtRebuild.Parity.World;

/// <summary>
/// A level's world as its file stores it, read in CWorld::LoadWorld's order without spawning anything: what each read
/// returns is kept so that a later step can replay the spawns, draws and links in retail's order.
/// </summary>
public sealed class WorldFile
{
    public int Version { get; private set; }

    public int HeaderVersion { get; private set; }

    public IReadOnlyList<byte[]> BattleEngineConfigurations { get; private set; } = [];

    /// <summary>EWorldType (header version 3 and later), kSinglePlayer (0) otherwise.</summary>
    public int WorldType { get; private set; }

    /// <summary>The base world's level, -1 for none.</summary>
    public int BaseLevel { get; private set; } = -1;

    public IReadOnlyList<(byte[] Name, ScriptObjectCode Code)> Scripts { get; private set; } = [];

    public int Map { get; private set; } = -1;

    public IReadOnlyList<byte[]> NamedMeshes { get; private set; } = [];

    /// <summary>Every thing record, in file order (including those the load would drop).</summary>
    public IReadOnlyList<ThingRecord> Things { get; private set; } = [];

    public IReadOnlyList<TreeGroup> TreeGroups { get; private set; } = [];

    public IReadOnlyList<TreeType> TreeTypes { get; private set; } = [];

    public InfluenceMapRecord InfluenceMap { get; private set; } = new(0, [], []);

    public IReadOnlyList<WaypointPathRecord> WaypointPaths { get; private set; } = [];

    public LevelTimings Timings { get; private set; } = default!;

    /// <summary>The three occupancy grids as stored (version, then the bytes), empty before world version 47.</summary>
    public IReadOnlyList<(int Version, byte[] Data)> OccupancyGrids { get; private set; } = [];

    /// <summary>The level's start time (world version 49 and later).</summary>
    public float? StartTime { get; private set; }

    /// <summary>The buffer position after the last read.</summary>
    public uint EndPosition { get; private set; }

    /// <summary>
    /// CWorld::Deserialize for a resource file's WRES chunk: the base and real world levels and where each world's data
    /// starts in the decompressed stream. Call after the reader returned the WRES tag.
    /// </summary>
    public static (int BaseLevel, int WorldLevel, uint BasePosition, uint WorldPosition) ReadResourcePositions(ChunkReader c)
    {
        c.GetNext();
        c.GetNext();
        int baseLevel = c.ReadInt32();
        int worldLevel = c.ReadInt32();
        uint basePosition = uint.MaxValue;
        if (baseLevel != -1)
        {
            c.GetNext();
            basePosition = c.WhereAmI;
            c.Skip();
        }

        c.GetNext();
        uint worldPosition = c.WhereAmI;
        c.Skip();
        return (baseLevel, worldLevel, basePosition, worldPosition);
    }

    /// <summary>
    /// LoadWorld's reads. A base world and a real world store the same layout: the base and real paths differ only in
    /// what they keep (a real world with a base skips its trees, influence map and grids; a base world keeps no starts),
    /// not in what they read. A real world with a base loads the base world right after reading its level.
    /// </summary>
    public static WorldFile Read(MemBuffer file)
    {
        var w = new WorldFile();
        w.Version = (ushort)file.ReadInt16();
        if (w.Version < 43 || w.Version > 50)
        {
            throw new InvalidDataException($"world version {w.Version} is outside 43..50");
        }

        w.ReadHeader(file);
        w.BaseLevel = file.ReadInt32();
        w.ReadScripts(file);
        w.Map = file.ReadInt32();
        w.ReadNamedMeshes(file);
        w.ReadThings(file);
        w.ReadTrees(file);
        w.InfluenceMap = InfluenceMapRecord.Read(file);
        w.ReadWaypoints(file);
        w.Timings = LevelTimings.Read(w.Version, file);
        w.ReadOccupancy(file);
        if (w.Version >= 49)
        {
            w.StartTime = file.ReadSingle();
        }

        w.EndPosition = file.Position;
        return w;
    }

    private void ReadHeader(MemBuffer file)
    {
        HeaderVersion = file.ReadInt32();
        file.ReadInt32();
        file.ReadInt32();
        BattleEngineConfigurations = World.BattleEngineConfigurations.Read(file);
        if (HeaderVersion >= 2)
        {
            file.ReadInt32();
            file.ReadInt32();
            file.ReadInt32();
        }

        if (HeaderVersion >= 3)
        {
            WorldType = file.ReadInt32();
        }
    }

    private void ReadScripts(MemBuffer file)
    {
        int count = file.ReadInt32();
        var scripts = new List<(byte[], ScriptObjectCode)>(count);
        Span<byte> pad = stackalloc byte[10];
        for (int i = 0; i < count; i++)
        {
            byte[] name = ScriptValue.ReadString(file);
            ScriptObjectCode code = ScriptObjectCode.Read(file);
            scripts.Add((name, code));
            file.Read(pad);
        }

        Scripts = scripts;
    }

    private void ReadNamedMeshes(MemBuffer file)
    {
        int count = file.ReadInt32();
        var meshes = new List<byte[]>(count);
        for (int i = 0; i < count; i++)
        {
            // an unsigned char length here, unlike the world's other counted strings
            var name = new byte[file.ReadByte()];
            file.Read(name);
            meshes.Add(name);
        }

        NamedMeshes = meshes;
    }

    private void ReadThings(MemBuffer file)
    {
        int count = (ushort)file.ReadInt16();
        var things = new List<ThingRecord>(count);
        for (int i = 0; i < count; i++)
        {
            int type = file.ReadInt32();
            InitThingRecord.InitKind kind = InitThingRecord.KindFor(type);
            InitThingRecord init;
            byte[]? name = null;
            int diesOn = -1;
            switch (type)
            {
                case Oid.Unit:
                case Oid.Building:
                case Oid.Squad:
                    init = InitThingRecord.Load(kind, Version, file);
                    name = file.ReadCountedString();
                    diesOn = file.ReadInt32();
                    break;
                case Oid.Feature:
                case Oid.Hazard:
                    init = InitThingRecord.Load(kind, Version, file);
                    name = file.ReadCountedString();
                    diesOn = file.ReadInt32();
                    break;
                default:
                    init = InitThingRecord.Load(kind, Version, file);
                    break;
            }

            things.Add(new ThingRecord(type, init, name, diesOn));
        }

        Things = things;
    }

    private void ReadTrees(MemBuffer file)
    {
        int groupCount = file.ReadInt16();
        var groups = new List<TreeGroup>();
        for (; groupCount != 0; groupCount--)
        {
            float y0 = file.ReadSingle();
            float x0 = file.ReadSingle();
            float y1 = file.ReadSingle();
            float x1 = file.ReadSingle();
            int count = file.ReadInt32();
            byte[] treeType = file.ReadCountedString();
            int seed = file.ReadInt32();
            groups.Add(new TreeGroup(x0, y0, x1, y1, count, treeType, seed));
        }

        TreeGroups = groups;
        int typeCount = file.ReadInt32();
        var types = new List<TreeType>();
        for (; typeCount != 0; typeCount--)
        {
            byte[] name = file.ReadCountedString();
            int count = file.ReadInt32();
            var trees = new (float X, float Y, int Variant)[count];
            for (int i = 0; i < count; i++)
            {
                trees[i] = (file.ReadSingle(), file.ReadSingle(), file.ReadInt32());
            }

            types.Add(new TreeType(name, trees));
        }

        TreeTypes = types;
    }

    private void ReadWaypoints(MemBuffer file)
    {
        int count = file.ReadInt16();
        var paths = new List<WaypointPathRecord>();
        for (; count != 0; count--)
        {
            byte[] name = file.ReadCountedString();
            var indices = new List<short>();
            if (Version >= 33)
            {
                for (int n = file.ReadInt32(); n != 0; n--)
                {
                    indices.Add(file.ReadInt16());
                }
            }
            else
            {
                indices.Add(file.ReadInt16());
            }

            paths.Add(new WaypointPathRecord(name, indices));
        }

        WaypointPaths = paths;
    }

    private void ReadOccupancy(MemBuffer file)
    {
        if (Version < 47)
        {
            return;
        }

        var grids = new List<(int, byte[])>(3);
        for (int g = 0; g < 3; g++)
        {
            int version = file.ReadInt32();
            int size = version switch
            {
                1 => 512 * 512 / 8,
                2 => 256 * 32,
                _ => 0,
            };
            var data = new byte[size];
            file.Read(data);
            grids.Add((version, data));
        }

        if (Version < 50)
        {
            for (int i = 0; i < 5; i++)
            {
                file.ReadInt32();
            }
        }

        OccupancyGrids = grids;
    }
}

/// <summary>One thing record: its class id, its init, its physics name (units, squads, features, hazards) and the world
/// it dies on (-1 for none).</summary>
public sealed record ThingRecord(int Type, InitThingRecord Init, byte[]? PhysicsName, int DiesOn)
{
    public string PhysicsNameText => PhysicsName is null ? "" : Encoding.ASCII.GetString(PhysicsName);
}

/// <summary>A group of trees scattered over a rectangle from a seed.</summary>
public sealed record TreeGroup(float X0, float Y0, float X1, float Y1, int Count, byte[] TreeType, int Seed);

/// <summary>Single trees of one type, each with its variant number.</summary>
public sealed record TreeType(byte[] Name, IReadOnlyList<(float X, float Y, int Variant)> Trees);

/// <summary>A named waypoint path: indices into the loaded things array.</summary>
public sealed record WaypointPathRecord(byte[] Name, IReadOnlyList<short> ThingIndices);

/// <summary>The influence map as stored: its format version, nodes (x, y, radius, weight) and links (a, b, weight).</summary>
public sealed record InfluenceMapRecord(int Version, IReadOnlyList<(float X, float Y, float Radius, int Weight)> Nodes,
    IReadOnlyList<(int A, int B, int Weight)> Links)
{
    public static InfluenceMapRecord Read(MemBuffer file)
    {
        int version = (ushort)file.ReadInt16();
        var nodes = new List<(float, float, float, int)>();
        var links = new List<(int, int, int)>();
        if (version is 0 or 1)
        {
            for (int count = file.ReadInt32(); count != 0; count--)
            {
                float x = file.ReadSingle();
                float y = file.ReadSingle();
                float radius = file.ReadSingle();
                // version 1 stores the node weight as a float in Load and as a SINT in Skip; it is kept as its bits
                int weight = version == 1 ? file.ReadInt32() : 0;
                nodes.Add((x, y, radius, weight));
            }

            for (int count = file.ReadInt32(); count != 0; count--)
            {
                int a = file.ReadInt32();
                int b = file.ReadInt32();
                int weight = version == 1 ? file.ReadInt32() : 1;
                links.Add((a, b, weight));
            }
        }

        return new InfluenceMapRecord(version, nodes, links);
    }
}

/// <summary>The level's timings and score settings.</summary>
public sealed record LevelTimings(float PreRun, float Pan, int SGrade, int DGrade, float TimeLimit, float FullScoreTime,
    float PercentageScoreTime, float ScorePercentage)
{
    public static LevelTimings Read(int version, MemBuffer file)
    {
        file.ReadSingle();
        file.ReadSingle();
        file.ReadSingle();
        var t = new LevelTimings(file.ReadSingle(), file.ReadSingle(), file.ReadInt32(), file.ReadInt32(), file.ReadSingle(),
            file.ReadSingle(), file.ReadSingle(), file.ReadSingle());
        if (version is >= 45 and < 48)
        {
            for (int i = 0; i < 5; i++)
            {
                file.ReadInt32();
            }
        }

        return t;
    }
}
