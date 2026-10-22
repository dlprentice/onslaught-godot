// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/waypoint.h and src/Waypoint.cpp (CWaypoint: Init 005057b0, Shutdown 00505810),
// src/LevelScriptThing.cpp (CLevelScriptThing::Init 004900d0), src/SafeSide.cpp (CSafeSide: Init 004de190, Shutdown
// 004de1d0), src/SphereTrigger.cpp (CSphereTrigger: Init 004e5500, Activate 004e5540, Deactivate 004e5680, Shutdown
// 004e5690, Hit 004e5700) and src/Start.cpp (CStart: the constructor 004eacc0, Init 004eae10, SpawnBattleEngine
// 004eaf20, Available 004eb130) and src/WaypointManager.cpp (CWaypointPath::Load 00505960, CWaypointManager: DeleteAll
// 00505ab0, Load 00505ae0, FindNearest 00505c30). Not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Particles;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CWaypoint (class id 18): a point on a waypoint path, a plain thing on the world's waypoint list.</summary>
public sealed class Waypoint(ThingContext context) : Thing(context, 18)
{
    /// <summary>mNext: the init thing's target, the next point.</summary>
    public ActiveReader<Thing> Next { get; } = new();

    public override void SetThingType(uint t) => base.SetThingType(t | ThingType.Waypoint);

    /// <summary>CWaypoint::Init: no collision, CThing::Init, the waypoint list, down to the ground if above it, the next point.</summary>
    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        base.Init(init);
        Context.World.Waypoints.Append(this);
        if (Context.Map.Collide(Pos) < Pos.Z)
        {
            Pos.Z = Context.Map.Collide(Pos);
        }

        Next.SetReader(init.TargetThing);
    }

    public override void Shutdown()
    {
        Context.World.Waypoints.Remove(this);
        base.Shutdown();
    }
}

/// <summary>CLevelScriptThing (class id 27): the thing a level's script runs on.</summary>
public sealed class LevelScriptThing(ThingContext context) : ComplexThing(context, Oid.LevelScriptThing)
{
    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        base.Init(init);
    }
}

/// <summary>CSafeSide (class id 37): a side's safe area, on the world's safe-side list (CUnit::FindNearestSafeSide).</summary>
public sealed class SafeSide(ThingContext context) : ComplexThing(context, Oid.SafeSide)
{
    /// <summary>mAllegiance.</summary>
    public int Allegiance;

    public int GetAllegiance() => Allegiance;

    /// <summary>CSafeSide::Init: no collision, out of the map-who, at the head of the safe-side list, then CComplexThing's.</summary>
    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        Flags &= ~ThingFlags.InMapWho;
        Context.World.SafeSides.Add(this);
        Allegiance = init.Allegiance;
        base.Init(init);
    }

    public override void Shutdown()
    {
        Context.World.SafeSides.Remove(this);
        base.Shutdown();
    }
}

/// <summary>
/// CSphereTrigger (class id 36): a sphere that records the units inside it each frame (through collision Hit) and shows
/// an effect while it is an objective.
/// </summary>
public sealed class SphereTrigger : ComplexThing
{
    public SphereTrigger(ThingContext context)
        : base(context, Oid.SphereTrigger)
    {
        Contained = new SPtrSet<ActiveReader<Thing>>(context.Pool);
    }

    /// <summary>mRadius.</summary>
    public float Radius;

    /// <summary>mContainedTime: the event time of the frame <see cref="Contained"/> belongs to.</summary>
    public float ContainedTime;

    /// <summary>mContained: readers of the units inside this frame.</summary>
    public SPtrSet<ActiveReader<Thing>> Contained { get; }

    public override float GetRadius() => Radius;

    public override void SetThingType(uint t) => base.SetThingType(t | 0x08000000 | ThingType.Stationary);

    /// <summary>
    /// CSphereTrigger::Init: the radius; it seeks collisions with everything but stationary things, as an outer sphere
    /// that is passed through (ECR_PASSIVE); then CComplexThing's.
    /// </summary>
    public override void Init(InitThing init)
    {
        Radius = init.Radius;
        init.InitCst.NotSeekCollisionWith = ThingType.Stationary;
        init.InitCst.MaxCollision = CollisionLevel.OuterSphere;
        init.InitCst.DesiredCollision = CollisionLevel.OuterSphere;
        init.InitCst.CollisionResponse = 0;
        ContainedTime = -1.0f;
        base.Init(init);
    }

    /// <summary>mEffect: the trigger's effect link (a member, made with the trigger).</summary>
    private ParticleEffectLink? _effect;

    private ParticleEffectLink? Effect => _effect ??= Context.Particles is { } p ? new ParticleEffectLink(p.Manager) : null;

    /// <summary>CSphereTrigger::Activate: an objective shows the "Sphere Trigger Effect" particle (important) at its position.</summary>
    public override void Activate()
    {
        if (IsObjective() && Context.Particles is { } particles && Effect is { } effect)
        {
            effect.Kill();
            particles.Manager.CreateEffect(particles.Manager.Set.GetPD("Sphere Trigger Effect"), effect, new FVector(0, 0, 0), false, true);
            effect.SetPos(Pos);
        }
    }

    /// <summary>CSphereTrigger::Deactivate: the effect stops.</summary>
    public override void Deactivate() => Effect?.Kill();

    public override void Shutdown()
    {
        Contained.RemoveAll();
        Deactivate();
        base.Shutdown();
    }

    /// <summary>
    /// CSphereTrigger::Hit: CComplexThing's (the script hears of it), then a unit is recorded; the first unit hit in a new
    /// frame clears the last frame's list.
    /// </summary>
    public override void Hit(Thing other, Collision.CollisionReport? report)
    {
        base.Hit(other, report);

        if (other.IsA(ThingType.Unit))
        {
            if (ContainedTime != Context.Events.Time)
            {
                Contained.RemoveAll();
                ContainedTime = Context.Events.Time;
            }

            var reader = new ActiveReader<Thing>();
            reader.SetReader(other);
            Contained.Append(reader);
        }
    }
}

/// <summary>
/// CStart (class id 15): a player's start. Its Init keeps a battle engine init thing and spawns the battle engine
/// through <see cref="SpawnBattleEngine"/>, which the battle engine's owner supplies.
/// </summary>
public class Start(ThingContext context) : ComplexThing(context, Oid.Start)
{
    /// <summary>mPlayerObject: the battle engine it spawned.</summary>
    public ActiveReader<Thing> PlayerObject { get; } = new();

    /// <summary>mPlayerNumber.</summary>
    public int PlayerNumber;

    /// <summary>mInit: the CBattleEngineInitThing (its mConfigurationId is <see cref="ConfigurationId"/>).</summary>
    public InitThing BattleEngineInit { get; } = new() { Kind = InitThingRecord.InitKind.Unit };

    /// <summary>mInit.mConfigurationId.</summary>
    public int ConfigurationId;

    /// <summary>UPhysicsManager::SpawnThing(OID_CBattleEngine), set by the battle engine's owner.</summary>
    public Func<Thing?>? SpawnBattleEngineThing { get; set; }

    /// <summary>
    /// CStart::Init: no collision, out of the map-who, the position down to the ground if above it, CComplexThing's,
    /// the head of the start list; the player number (swapped when the sides are inverted), the battle engine's init
    /// thing, then the battle engine.
    /// </summary>
    public override void Init(InitThing init)
    {
        init.InitCst.NotSeekCollisionWith = ThingType.Everything;
        Flags &= ~ThingFlags.InMapWho;
        if (Context.Map.Collide(init.Pos) < init.Pos.Z)
        {
            init.Pos.Z = Context.Map.Collide(init.Pos);
        }

        base.Init(init);
        Context.World.StartNB.Add(this);
        PlayerNumber = init.PlayerNumber;
        Game.FrontEndSettings? settings = Context.Game?.Settings;
        if (settings is null)
        {
            Context.Skip("CStart::Init: GAME's front-end settings (no game attached)");
        }

        if (settings is { InvertSides: true })
        {
            if (PlayerNumber == 1)
            {
                PlayerNumber = 2;
            }
            else if (PlayerNumber == 2)
            {
                PlayerNumber = 1;
            }
        }

        BattleEngineInit.Pos = init.Pos;
        BattleEngineInit.Yaw = init.Yaw;
        BattleEngineInit.Pitch = init.Pitch;
        BattleEngineInit.Roll = init.Roll;
        BattleEngineInit.Allegiance = init.Allegiance;
        ConfigurationId = PlayerNumber == 1 ? settings?.Player1Configuration ?? 0 : settings?.Player2Configuration ?? 0;
        BattleEngineInit.PlaneMode = init.PlaneMode;
        SpawnBattleEngine(false);
    }

    /// <summary>
    /// CStart::SpawnBattleEngine: a battle engine initialised from <see cref="BattleEngineInit"/>, whose collision init
    /// is reset afterwards; with effects, a respawn particle.
    /// </summary>
    public virtual Thing? SpawnBattleEngine(bool effects)
    {
        if (SpawnBattleEngineThing is null)
        {
            Context.Skip("CStart::SpawnBattleEngine (no battle engine spawner)");
            PlayerObject.SetReader(null);
        }
        else
        {
            PlayerObject.SetReader(SpawnBattleEngineThing());
            if (PlayerObject.ToRead is { } engine)
            {
                engine.Init(BattleEngineInit);
                BattleEngineInit.InitCst.Initialise();
            }
        }

        if (effects)
        {
            Context.Skip("CStart::SpawnBattleEngine respawn particle");
        }

        return PlayerObject.ToRead;
    }

    /// <summary>CStart::Available: no other non-infantry, non-component unit within 4 of it (MAP_WHO: not ported).</summary>
    public bool Available() => throw new NotSupportedException("CStart::Available needs MAP_WHO, which is not ported");
}

/// <summary>CWaypointPath: a named path and its waypoints.</summary>
public sealed class WaypointPath(SPtrSetPool pool, byte[] name)
{
    /// <summary>mName.</summary>
    public byte[] Name { get; } = name;

    /// <summary>mWaypoints: each Add puts the next stored waypoint at the head, so the list runs last-stored first.</summary>
    public SPtrSet<Thing> Waypoints { get; } = new(pool);
}

/// <summary>CWaypointManager: every waypoint path of the loaded worlds (a class-static set in retail).</summary>
public sealed class WaypointManager(SPtrSetPool pool)
{
    /// <summary>mPaths, in load order.</summary>
    public SPtrSet<WaypointPath> Paths { get; } = new(pool);

    /// <summary>
    /// CWaypointManager::Load (world version 33 and later): each path takes the stored things that are waypoints; a
    /// negative or empty index is skipped.
    /// </summary>
    public void Load(IReadOnlyList<WaypointPathRecord> records, IReadOnlyList<Thing?> things)
    {
        foreach (WaypointPathRecord record in records)
        {
            var path = new WaypointPath(Paths.Pool, record.Name);
            foreach (short index in record.ThingIndices)
            {
                if (index >= 0 && things[index] is { } thing && (thing.ThingTypeBits & ThingType.Waypoint) != 0)
                {
                    path.Waypoints.Add(thing);
                }
            }

            Paths.Append(path);
        }
    }

    public void DeleteAll() => Paths.RemoveAll();

    /// <summary>FindNearest: the path by name, ignoring case, and its waypoint nearest the position (Y, Z, X summed).</summary>
    public Waypoint? FindNearest(byte[] name, FVector pos)
    {
        string wanted = System.Text.Encoding.ASCII.GetString(name);
        for (WaypointPath? path = Paths.First(); path is not null; path = Paths.Next())
        {
            if (string.Equals(System.Text.Encoding.ASCII.GetString(path.Name), wanted, StringComparison.OrdinalIgnoreCase))
            {
                float best = 9999999.0f;
                Waypoint? nearest = null;
                for (Thing? w = path.Waypoints.First(); w is not null; w = path.Waypoints.Next())
                {
                    FVector d = w.Pos - pos;
                    float dist = (d.Y * d.Y) + (d.Z * d.Z) + (d.X * d.X);
                    if (dist < best)
                    {
                        best = dist;
                        nearest = (Waypoint)w;
                    }
                }

                return nearest;
            }
        }

        return null;
    }
}

/// <summary>
/// The loader's spawner for the classes ported here: the start, waypoints, level-script things, sphere triggers and safe
/// sides (::SpawnThing by object id), the squads (UPhysicsManager::SpawnSquad, their members from the unit factory) and
/// the waypoint paths.
/// </summary>
public sealed class ParitySpawner(ThingContext context) : IWorldSpawner
{
    /// <summary>The waypoint paths the loads read.</summary>
    public WaypointManager Waypoints { get; } = new(context.Pool);

    /// <summary>SpawnThing(OID_CBattleEngine) for the starts (the battle engine's owner sets it).</summary>
    public Func<Thing?>? SpawnBattleEngineThing { get; set; }

    public Thing? SpawnThing(int oid) => oid switch
    {
        Oid.Start => new Start(context) { SpawnBattleEngineThing = SpawnBattleEngineThing },
        18 => new Waypoint(context),
        Oid.LevelScriptThing => new LevelScriptThing(context),
        Oid.SphereTrigger => new SphereTrigger(context),
        Oid.SafeSide => new SafeSide(context),
        _ => null,
    };

    public ComplexThing? SpawnSquad(UnitBehaviour behaviour)
    {
        Squad? squad = Squad.SpawnSquad(context, behaviour);
        if (squad is not null)
        {
            squad.SpawnUnit = index => UnitFactory.SpawnThing(context, index);
        }

        return squad;
    }

    public void MarkSpawnedFromUnitRecord(ComplexThing squad) => ((Squad)squad).SpawnedFromUnitRecord = true;

    public void LoadWaypointPaths(IReadOnlyList<WaypointPathRecord> paths, IReadOnlyList<Thing?> things) =>
        Waypoints.Load(paths, things);
}
