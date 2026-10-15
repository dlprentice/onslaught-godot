// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.World;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// The game-wide singletons a thing reaches in retail through globals (WORLD, MAP, EVENT_MANAGER, GAME's random
/// stream, UPhysicsManager, the mesh registry), handed to each thing explicitly so separate simulations never share
/// state. <see cref="NotPorted"/> counts the calls into subsystems that are not ported yet, by name, so a run states
/// exactly what it skipped.
/// </summary>
public sealed class ThingContext(
    SPtrSetPool pool,
    EventManager events,
    SeedUniformRandomNumberStream random,
    HeightField map,
    PhysicsRegistry physics)
{
    public SPtrSetPool Pool { get; } = pool;

    public EventManager Events { get; } = events;

    public SeedUniformRandomNumberStream Random { get; } = random;

    public HeightField Map { get; } = map;

    public PhysicsRegistry Physics { get; } = physics;

    /// <summary>The meshes the resource file loaded, by name (CMesh's registry).</summary>
    public Dictionary<string, MeshData> Meshes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The world's compiled scripts, by name (CWorld::mScripts).</summary>
    public List<(byte[] Name, ScriptObjectCode Code)> Scripts { get; } = [];

    /// <summary>The world's runtime registries (CWorld's sets), made with the context's pool.</summary>
    public WorldRegistry World { get; } = new(pool);

    /// <summary>UBattleEngineDataManager: the battle engine configurations.</summary>
    public BattleEngineDataManager? BattleEngines { get; set; }

    /// <summary>UBattleEngineConfigurations: the world header's configuration names.</summary>
    public IReadOnlyList<byte[]> WorldBattleEngineConfigurations { get; set; } = [];

    /// <summary>MAP_WHO: made (Create) with the context, as CGame makes it before a level loads (game.cpp:351).</summary>
    public MapWho MapWho { get; } = CreateMapWho();

    /// <summary>COccupancy's statics and WORLD.mOccupancy: the navigation grids and the units on them.</summary>
    public OccupancyState Occupancy { get; } = new(pool);

    /// <summary>The poly buckets' process-wide search statics.</summary>
    public PolyBucketStatics PolyBuckets { get; } = new();

    private static MapWho CreateMapWho()
    {
        var m = new MapWho();
        m.Create();
        return m;
    }

    /// <summary>CThing::sThingCount: the next thing's number (ResetThingCounter at each level load).</summary>
    public int NextThingNumber { get; set; }

    /// <summary>MAP.GetWaterLevel(): the height field's water level.</summary>
    public float WaterLevel => Map.WaterLevel;

    /// <summary>The render frame number CRTMesh poses compare against (1 or more during the load).</summary>
    public int RenderFrameNumber { get; set; } = 2;

    /// <summary>GAME: the fear grids, the front-end settings and the rest of CGame a thing reads (set by the world wiring).</summary>
    public ParityGame? Game { get; set; }

    /// <summary>INFLUENCEMAP (set by the world wiring).</summary>
    public InfluenceMap? InfluenceMap { get; set; }

    /// <summary>Calls into subsystems that are not ported yet.</summary>
    public SortedDictionary<string, int> NotPorted { get; } = new(StringComparer.Ordinal);

    public void Skip(string what) => NotPorted[what] = NotPorted.GetValueOrDefault(what) + 1;

    /// <summary>The PC's mouse look: CBattleEngine::UpdateMouseLookAngles's statics and the platform's mouse.</summary>
    public MouseLookState MouseLook { get; } = new();

}

/// <summary>CWorld's runtime sets, in retail's member order (world.h): what each Init adds itself to.</summary>
public sealed class WorldRegistry(SPtrSetPool pool)
{
    public SPtrSet<Thing> Things { get; } = new(pool);
    public SPtrSet<Thing> SquadNB { get; } = new(pool);
    public SPtrSet<Thing> Set20 { get; } = new(pool);
    public SPtrSet<Thing> Set30 { get; } = new(pool);
    public SPtrSet<Thing> UnitNB { get; } = new(pool);
    public SPtrSet<Thing> Set50 { get; } = new(pool);
    public SPtrSet<Thing> Set60 { get; } = new(pool);
    public SPtrSet<Thing> StartNB { get; } = new(pool);
    public SPtrSet<Thing> SpawnPointNB { get; } = new(pool);
    public SPtrSet<Thing> Waypoints { get; } = new(pool);
    public SPtrSet<Thing> NamedThings { get; } = new(pool);
    public SPtrSet<Thing> ObjectiveThings { get; } = new(pool);

    /// <summary>mSetD0: the safe sides (CSafeSide::Init adds itself at the head; CUnit::FindNearestSafeSide walks it).</summary>
    public SPtrSet<Thing> SafeSides { get; } = new(pool);
    public SPtrSet<Thing> BigThings { get; } = new(pool);

    /// <summary>mCountersA/B: units per behaviour, by allegiance (Forseti, Muspell).</summary>
    public int[] CountersA { get; } = new int[26];

    public int[] CountersB { get; } = new int[26];
}

/// <summary>
/// The state the PC's mouse look reads and keeps: UpdateMouseLookAngles's frame counters (006601f8 and 00622f0c, from
/// 0 and 2), MOUSE_X and MOUSE_Y (the platform's cursor, recentred after each look), the window's size, the options'
/// mouse sensitivity (gMouseSensitivity, 7 by default) and the career's invert-Y setting for player 1.
/// </summary>
public sealed class MouseLookState
{
    public int Frame;
    public int LastFrame = 2;
    public int WindowWidth = 640;
    public int WindowHeight = 480;
    public int MouseX = 320;
    public int MouseY = 240;
    public float Sensitivity = 7.0f;
    public bool InvertY;
}
