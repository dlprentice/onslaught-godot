// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace OnslaughtRebuild.Core;

public readonly record struct Level100ActorId(int Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public static class Level100ReleasedThingTypeMasks
{
    public const uint Ammunition = 4;
    public const uint BattleEngine = 8;
    public const uint ProvenBits = Ammunition | BattleEngine;
}

public readonly record struct SimVector3(int X, int Y, int Z)
{
    public static SimVector3 Zero => new(0, 0, 0);
}

/// <summary>
/// Exact IEEE-754 single-precision components retained from the released mesh
/// emitter or a bounded deterministic transform. Word storage preserves signed
/// zeros and keeps engine/presentation vector types outside Core.
/// </summary>
public readonly record struct Level100FloatVector3Bits(int X, int Y, int Z);

public readonly record struct Level100FloatVector4Bits(int X, int Y, int Z, int W);

public readonly record struct Level100FloatBasis3Bits(
    int Row0X,
    int Row0Y,
    int Row0Z,
    int Row1X,
    int Row1Y,
    int Row1Z,
    int Row2X,
    int Row2Y,
    int Row2Z);

public sealed record Level100SpawnerTransform(
    Level100FloatVector3Bits LocalPositionFloatBits,
    Level100FloatBasis3Bits LocalBasisFloatBits);

/// <summary>One authored WaypointA/B CEMT selector and its constant model pose.</summary>
public sealed record Level100SpawnerExitPoint(
    int Selector, Level100SpawnerTransform ModelTransform);

public sealed record Level100AuthoredTransform(
    Level100FloatVector3Bits RetailPositionFloatBits,
    Level100FloatVector3Bits RetailEulerFloatBits,
    Level100FloatBasis3Bits RetailBasisFloatBits);

public sealed record Level100ActorPoseSnapshot(
    SimVector3 PositionMillimeters,
    Level100FloatBasis3Bits BasisFloatBits,
    SimVector3 LinearVelocityMillimetersPerTick,
    SimVector3 AngularVelocityMicroRadiansPerTick);

public sealed record Level100ActorDefinition(
    int AuthoredOrder,
    string DefinitionIdentity,
    string Name,
    string? DefinitionName,
    string? ScriptName,
    string? MeshBinding,
    uint ThingTypeMask,
    bool IsStatic,
    bool Active,
    int InitialHealth,
    Level100AuthoredTransform AuthoredTransform,
    Level100ActorPoseSnapshot InitialPose,
    Level100MissionTargetGroup TargetGroup,
    int TargetOrdinal,
    Level100MissionTrigger? Trigger,
    int Allegiance = 0);

/// <summary>
/// A type-28 squad row: the <c>CNormalSquad</c> thing
/// <paramref name="DefinitionIdentity"/> (an actor definition of its own, whose
/// script is the squad's) and its members <paramref name="MemberIdentities"/>,
/// in order, of profile <paramref name="DefinitionName"/>. <c>CSquad::Init</c>
/// clears the members' scripts and names (<c>0x004e6049-0x004e6076</c>;
/// <c>reverse-engineering/game-mechanics/world-110-construction-order.md</c>,
/// "Type-28 squads").
/// </summary>
public sealed record Level100SquadDefinition(
    string DefinitionIdentity,
    string Name,
    string DefinitionName,
    IReadOnlyList<string> MemberIdentities,
    string? ScriptName,
    bool Active,
    int Allegiance,
    int Mode,
    Level100AuthoredTransform AuthoredTransform);

/// <summary>
/// A component built inside its parent's construction, after the parent's
/// Actor draw: a landing craft's "Dropship Gun Turret" (the World 110
/// construction contract, "Landing craft and their turrets").
/// </summary>
public sealed record Level100ComponentDefinition(
    string ChildIdentity,
    string ParentIdentity,
    string DefinitionName);

public sealed record Level100SpawnDefinition(
    int AuthoredOrder,
    string DefinitionIdentity,
    string OwnerDefinitionIdentity,
    string DefinitionName,
    string SpawnerName,
    string ScriptName,
    string? MeshBinding,
    uint ThingTypeMask,
    bool Active,
    int InitialHealth,
    Level100ActorPoseSnapshot InitialPose,
    Level100SpawnerTransform AuthoredEmitterTransform,
    Level100MissionTargetGroup TargetGroup,
    int FixedTargetOrdinal,
    int MaximumGroupActors,
    IReadOnlyList<Level100SpawnerExitPoint>? SpawnerExitWaypoints = null);

/// <summary>
/// One node of a named path: the <c>CWaypoint</c> at level row
/// <paramref name="NodeIndex"/>, and the node its own <c>target</c> names
/// (<c>CWaypoint::InitAndLink</c> <c>0x005057b0</c> binds it at <c>+0x3c</c>),
/// or null when it names none.
/// </summary>
public sealed record Level100WaypointPointDefinition(
    int NodeIndex,
    SimVector3 PositionMillimeters,
    Level100FloatVector4Bits RetailComponentsFloatBits,
    int? TargetNodeIndex = null)
{
    public SimVector2 HorizontalPositionMillimeters =>
        new(PositionMillimeters.X, PositionMillimeters.Z);
}

/// <summary>
/// One named waypoint path as retail loads it
/// (<c>reverse-engineering/game-mechanics/waypoint-paths.md</c>, pristine
/// specimen <c>74154BFA…</c>).
/// </summary>
/// <remarks>
/// <para>
/// <c>CWaypointManager::LoadWaypoints</c> (<c>0x00505ae0</c>, per path
/// <c>0x00505960</c>) keeps a listed row only when it is a <c>CWaypoint</c> and
/// prepends it (<c>0x004e5a80</c>), so <see cref="Points"/> runs in reverse
/// file order. The list decides only where following starts:
/// <c>FollowWaypointWait</c> (<c>0x00537e40</c>) takes the node nearest the
/// unit (<see cref="NearestPoint"/>), and
/// <c>IScript::UpdateWaypointFollowing</c> (<c>0x00538470</c>) then moves
/// on to each node's own target (<c>0x005384dc</c>) until one has none. A chain
/// can loop (Level 100's <c>Drone Path 1</c> and <c>Target Tank Path 2</c>), and
/// one path can hold several chains (World 110's <c>Fighter Path 1</c>).
/// </para>
/// <para>
/// Retail lets a target leave its path. The levels the rebuild admits keep every
/// target inside its own path, and the definition set holds them to it.
/// </para>
/// </remarks>
/// <param name="Name">The authored path name scripts call it by.</param>
/// <param name="Points">The path's nodes in retail list order.</param>
public sealed record Level100WaypointPathDefinition(
    string Name,
    IReadOnlyList<Level100WaypointPointDefinition> Points)
{
    /// <summary>
    /// A path from the rows its file lists, every one a <c>CWaypoint</c>: the
    /// loader prepends each (<c>0x004e5a80</c>), so the list runs in reverse.
    /// </summary>
    public static Level100WaypointPathDefinition FromFileOrder(
        string name,
        IEnumerable<Level100WaypointPointDefinition> fileOrder) =>
        new(name, Array.AsReadOnly(fileOrder.Reverse().ToArray()));

    /// <summary>The path's node at level row <paramref name="nodeIndex"/>.</summary>
    public Level100WaypointPointDefinition Point(int nodeIndex)
    {
        foreach (Level100WaypointPointDefinition point in Points)
        {
            if (point.NodeIndex == nodeIndex)
            {
                return point;
            }
        }

        throw new InvalidOperationException(
            $"Waypoint path '{Name}' has no node {nodeIndex}.");
    }

    public bool HasNode(int nodeIndex) =>
        Points.Any(point => point.NodeIndex == nodeIndex);

    /// <summary>
    /// The node <c>0x00505c30</c> returns for a unit at
    /// <paramref name="position"/> (its <c>+0x1c..+0x24</c>): walking the list in
    /// order, a node whose squared distance is strictly below the running
    /// minimum (from 9999999.0, kept as a float32) replaces it, so a tie keeps
    /// the earlier node. Null when no node is that close.
    /// </summary>
    public Level100WaypointPointDefinition? NearestPoint(
        Level100FloatVector3Bits position)
    {
        float minimum = 9999999.0f;
        Level100WaypointPointDefinition? nearest = null;
        foreach (Level100WaypointPointDefinition point in Points)
        {
            double distance = SquaredDistance(point.RetailComponentsFloatBits, position);
            if (distance < minimum)
            {
                minimum = (float)distance;
                nearest = point;
            }
        }

        return nearest;
    }

    // On the x87 stack at the game's PC24 precision: dx, dy, dz (waypoint
    // minus unit), then (dx*dx + dz*dz) + dy*dy, so the second horizontal
    // term is added last.
    private static double SquaredDistance(
        Level100FloatVector4Bits node,
        Level100FloatVector3Bits unit)
    {
        double dx = RetailFloat24.Subtract(Float(node.X), Float(unit.X));
        double dy = RetailFloat24.Subtract(Float(node.Y), Float(unit.Y));
        double dz = RetailFloat24.Subtract(Float(node.Z), Float(unit.Z));
        return RetailFloat24.Add(
            RetailFloat24.Add(RetailFloat24.Multiply(dx, dx), RetailFloat24.Multiply(dz, dz)),
            RetailFloat24.Multiply(dy, dy));
    }

    private static double Float(int bits) => BitConverter.Int32BitsToSingle(bits);
}

public enum Level100ActorMotionClass
{
    GroundVehicle = 1,
    Plane = 2,
    Dropship = 3,
}

/// <summary>
/// Exact released class/radius data and the class-specific Unit fields consumed
/// by the bounded Level 100 mechanics owner, shared by authored and spawned
/// instances of each Unit profile. Null weapon mounts mean unavailable input;
/// an empty collection is an admitted profile with no weapons. Model poses
/// alone do not determine live muzzle poses or runtime cache state.
/// </summary>
public sealed record Level100ActorMotionDefinition(
    int AuthoredOrder,
    string DefinitionName,
    Level100ActorMotionClass MotionClass,
    int BehaviorSerializedType,
    int BehaviorInternalId,
    int SteamClassVtableAddress,
    int ArrivalRadiusMillimeters,
    int? MaximumSpeedFloatBits,
    int? MaximumTurnRadiansPerBaseTickFloatBits,
    int? FullGuideBaseTicks,
    int? CoreGroundOriginOffsetMillimeters,
    IReadOnlyList<Level100ActorWeaponMountDefinition>? WeaponMounts = null,
    int? AirVelocityFloatBits = null,
    int? AirTurnRateFloatBits = null,
    bool? Big = null,
    int? MinimumAltitudeFloatBits = null,
    int? MeshRadiusFloatBits = null);

/// <summary>
/// A <c>CSafeSide</c> (a type-37 row, no actor): its Init (<c>0x004de190</c>)
/// prepends it to the list at <c>0x00855160</c> and keeps the row's
/// allegiance, and a unit's retreat point (<c>0x004fd910</c>) is the nearest
/// one of its own side. <paramref name="BaseWorld"/> tells which world file
/// holds <paramref name="Row"/>; a set lists them in construction order.
/// </summary>
public sealed record Level100SafeSideDefinition(
    bool BaseWorld,
    int Row,
    Level100FloatVector3Bits RetailPositionFloatBits,
    int Allegiance);

/// <summary>One ordered Unit weapon use and its exact selected model pose.</summary>
public sealed record Level100ActorWeaponMountDefinition(
    RetailUnitConstructionUse Use,
    int Selector,
    RetailUnitAttachmentPose ModelPose);

/// <summary>
/// Immutable, scenario-supplied Level 100 actor definitions. The product
/// adapter decodes these from the locally materialized released WRES/static
/// world; Core owns only validation and a canonical content identity.
/// </summary>
public sealed class Level100ActorDefinitionSet
{
    private static readonly byte[] s_identityMagic =
        Encoding.ASCII.GetBytes("ONSLAUGHT-LEVEL100-ACTOR-DEFINITIONS");

    private readonly IReadOnlyList<Level100ActorDefinition> _actors;
    private readonly IReadOnlyList<Level100SpawnDefinition> _spawns;
    private readonly IReadOnlyList<Level100WaypointPathDefinition> _waypointPaths;
    private readonly IReadOnlyList<Level100ActorMotionDefinition> _motionDefinitions;
    private readonly IReadOnlyList<Level100SquadDefinition> _squads;
    private readonly IReadOnlyList<Level100ComponentDefinition> _components;
    private readonly IReadOnlyList<Level100SafeSideDefinition> _safeSides;
    private readonly Dictionary<string, Level100ActorDefinition> _actorsByIdentity;
    private readonly Dictionary<string, Level100SpawnDefinition> _spawnsByIdentity;
    private readonly Dictionary<SpawnKey, Level100SpawnDefinition> _spawnsByRequest;
    private readonly Dictionary<string, Level100WaypointPathDefinition> _waypointPathsByName;
    private readonly Dictionary<string, Level100ActorMotionDefinition> _motionDefinitionsByName;

    public Level100ActorDefinitionSet(
        IEnumerable<Level100ActorDefinition> actors,
        IEnumerable<Level100SpawnDefinition> spawns,
        IEnumerable<Level100WaypointPathDefinition>? waypointPaths = null,
        IEnumerable<Level100ActorMotionDefinition>? motionDefinitions = null,
        int worldNumber = RetailWorldCatalog.RootWorldNumber,
        int baseWorldPineCount = 0,
        IEnumerable<Level100SquadDefinition>? squads = null,
        IEnumerable<Level100ComponentDefinition>? components = null,
        IEnumerable<Level100SafeSideDefinition>? safeSides = null)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(spawns);
        ArgumentOutOfRangeException.ThrowIfNegative(baseWorldPineCount);
        BaseWorldPineCount = baseWorldPineCount;
        if (RetailWorldCatalog.Find(worldNumber) is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldNumber),
                $"World {worldNumber} is not a released career node.");
        }

        WorldNumber = worldNumber;

        Level100ActorDefinition[] actorArray = actors.ToArray();
        Level100SpawnDefinition[] spawnArray = spawns.ToArray();
        Level100WaypointPathDefinition[] waypointPathArray =
            waypointPaths?.ToArray() ?? [];
        Level100ActorMotionDefinition[] motionDefinitionArray =
            motionDefinitions?.ToArray() ?? [];
        if (actorArray.Length == 0)
        {
            throw new ArgumentException("Level 100 requires at least one actor definition.", nameof(actors));
        }

        _actorsByIdentity = new Dictionary<string, Level100ActorDefinition>(StringComparer.Ordinal);
        for (int index = 0; index < actorArray.Length; index++)
        {
            Level100ActorDefinition definition = actorArray[index] ??
                throw new ArgumentException("Level 100 actor definitions cannot contain null.", nameof(actors));
            ValidateActorDefinition(definition, index);
            if (!_actorsByIdentity.TryAdd(definition.DefinitionIdentity, definition))
            {
                throw new ArgumentException(
                    $"Duplicate Level 100 actor definition identity '{definition.DefinitionIdentity}'.",
                    nameof(actors));
            }
        }

        _spawnsByIdentity = new Dictionary<string, Level100SpawnDefinition>(StringComparer.Ordinal);
        _spawnsByRequest = [];
        for (int index = 0; index < spawnArray.Length; index++)
        {
            Level100SpawnDefinition definition = spawnArray[index] ??
                throw new ArgumentException("Level 100 spawn definitions cannot contain null.", nameof(spawns));
            ValidateSpawnDefinition(definition, index);
            if (definition.SpawnerExitWaypoints is { } exitPoints)
            {
                // Own the collection before hashing; caller mutation must not
                // change the admitted model inputs beneath a saved identity.
                definition = definition with { SpawnerExitWaypoints = Array.AsReadOnly(exitPoints.ToArray()) };
                spawnArray[index] = definition;
            }
            if (!_actorsByIdentity.ContainsKey(definition.OwnerDefinitionIdentity))
            {
                throw new ArgumentException(
                    $"Level 100 spawn owner '{definition.OwnerDefinitionIdentity}' is undefined.",
                    nameof(spawns));
            }

            var key = new SpawnKey(
                definition.OwnerDefinitionIdentity,
                definition.DefinitionName,
                definition.SpawnerName,
                definition.ScriptName);
            if (!_spawnsByIdentity.TryAdd(definition.DefinitionIdentity, definition) ||
                !_spawnsByRequest.TryAdd(key, definition))
            {
                throw new ArgumentException(
                    $"Duplicate Level 100 spawn definition '{definition.DefinitionIdentity}'.",
                    nameof(spawns));
            }
        }

        _actors = Array.AsReadOnly(actorArray);
        _spawns = Array.AsReadOnly(spawnArray);
        _waypointPathsByName = new Dictionary<string, Level100WaypointPathDefinition>(
            StringComparer.Ordinal);
        for (int pathIndex = 0; pathIndex < waypointPathArray.Length; pathIndex++)
        {
            Level100WaypointPathDefinition path = waypointPathArray[pathIndex] ??
                throw new ArgumentException(
                    "Level 100 waypoint paths cannot contain null.",
                    nameof(waypointPaths));
            if (string.IsNullOrWhiteSpace(path.Name) ||
                path.Points is null ||
                path.Points.Count == 0)
            {
                throw new ArgumentException(
                    $"Invalid Level 100 waypoint path at authored order {pathIndex}.",
                    nameof(waypointPaths));
            }

            Level100WaypointPointDefinition[] points = path.Points.ToArray();
            // A node appears once, and its target, when it names one, is
            // another node of the same path: the admitted levels keep every
            // chain inside its path (waypoint-paths.md), so anything else is a
            // decode defect and stops here.
            if (points.Any(point =>
                    point.NodeIndex < 0 ||
                    !HasFiniteWaypointComponents(point.RetailComponentsFloatBits) ||
                    point.TargetNodeIndex is { } target &&
                        !points.Any(other => other.NodeIndex == target)) ||
                points.Select(point => point.NodeIndex).Distinct().Count() != points.Length ||
                !_waypointPathsByName.TryAdd(
                    path.Name,
                    new Level100WaypointPathDefinition(path.Name, Array.AsReadOnly(points))))
            {
                throw new ArgumentException(
                    $"Invalid or duplicate Level 100 waypoint path '{path.Name}'.",
                    nameof(waypointPaths));
            }
        }

        _waypointPaths = Array.AsReadOnly(
            waypointPathArray
                .Select(path => _waypointPathsByName[path.Name])
                .ToArray());
        _motionDefinitionsByName =
            new Dictionary<string, Level100ActorMotionDefinition>(StringComparer.Ordinal);
        for (int index = 0; index < motionDefinitionArray.Length; index++)
        {
            Level100ActorMotionDefinition definition = motionDefinitionArray[index] ??
                throw new ArgumentException(
                    "Level 100 motion definitions cannot contain null.",
                    nameof(motionDefinitions));
            if (definition.WeaponMounts is { } mounts)
                definition = definition with { WeaponMounts = Array.AsReadOnly(mounts.ToArray()) };
            ValidateMotionDefinition(definition, index);
            motionDefinitionArray[index] = definition;
            if (!actorArray.Any(actor =>
                    StringComparer.Ordinal.Equals(
                        actor.DefinitionName,
                        definition.DefinitionName)) &&
                !spawnArray.Any(spawn =>
                    StringComparer.Ordinal.Equals(
                        spawn.DefinitionName,
                        definition.DefinitionName)))
            {
                throw new ArgumentException(
                    $"Level 100 motion definition '{definition.DefinitionName}' has no actor.",
                    nameof(motionDefinitions));
            }
            if (!_motionDefinitionsByName.TryAdd(
                    definition.DefinitionName,
                    definition))
            {
                throw new ArgumentException(
                    $"Duplicate Level 100 motion definition '{definition.DefinitionName}'.",
                    nameof(motionDefinitions));
            }
        }

        _motionDefinitions = Array.AsReadOnly(motionDefinitionArray);
        _squads = Array.AsReadOnly(AdmitSquads(squads));
        _components = Array.AsReadOnly(AdmitComponents(components));
        _safeSides = Array.AsReadOnly(AdmitSafeSides(safeSides));
        IdentitySha256 = ComputeIdentity(
            actorArray,
            spawnArray,
            _waypointPaths,
            _motionDefinitions,
            baseWorldPineCount,
            _squads,
            _components,
            _safeSides);
    }

    private static Level100SafeSideDefinition[] AdmitSafeSides(IEnumerable<Level100SafeSideDefinition>? safeSides)
    {
        Level100SafeSideDefinition[] array = safeSides?.ToArray() ?? [];
        var rows = new HashSet<(bool, int)>();
        foreach (Level100SafeSideDefinition safeSide in array)
        {
            if (safeSide is null || safeSide.Row < 0 || !rows.Add((safeSide.BaseWorld, safeSide.Row)) ||
                !float.IsFinite(BitConverter.Int32BitsToSingle(safeSide.RetailPositionFloatBits.X)) ||
                !float.IsFinite(BitConverter.Int32BitsToSingle(safeSide.RetailPositionFloatBits.Y)) ||
                !float.IsFinite(BitConverter.Int32BitsToSingle(safeSide.RetailPositionFloatBits.Z)))
            {
                throw new ArgumentException("Invalid Level 100 safe side.", nameof(safeSides));
            }
        }

        return array;
    }

    private Level100SquadDefinition[] AdmitSquads(IEnumerable<Level100SquadDefinition>? squads)
    {
        Level100SquadDefinition[] squadArray = squads?.ToArray() ?? [];
        var members = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < squadArray.Length; index++)
        {
            Level100SquadDefinition squad = squadArray[index] ??
                throw new ArgumentException("Squad definitions cannot contain null.", nameof(squads));
            squad = squad with { MemberIdentities = Array.AsReadOnly(squad.MemberIdentities?.ToArray() ?? []) };
            squadArray[index] = squad;
            if (string.IsNullOrWhiteSpace(squad.DefinitionIdentity) ||
                !_actorsByIdentity.TryGetValue(squad.DefinitionIdentity, out Level100ActorDefinition? thing) ||
                thing.DefinitionName != SquadDefinitionName ||
                thing.ScriptName != squad.ScriptName ||
                thing.Active != squad.Active ||
                thing.Allegiance != squad.Allegiance ||
                !identities.Add(squad.DefinitionIdentity) ||
                string.IsNullOrWhiteSpace(squad.Name) ||
                string.IsNullOrWhiteSpace(squad.DefinitionName) ||
                squad.MemberIdentities.Count == 0 ||
                squad.Allegiance is < 0 or > 2 ||
                squad.AuthoredTransform is null ||
                !HasFiniteAuthoredTransform(squad.AuthoredTransform) ||
                squad.MemberIdentities.Any(member =>
                    !_actorsByIdentity.TryGetValue(member, out Level100ActorDefinition? actor) ||
                    !members.Add(member) ||
                    actor.IsStatic ||
                    actor.ScriptName is not null ||
                    actor.Allegiance != squad.Allegiance ||
                    !StringComparer.Ordinal.Equals(actor.DefinitionName, squad.DefinitionName)))
            {
                throw new ArgumentException($"Invalid squad definition at {index}.", nameof(squads));
            }
        }

        return squadArray;
    }

    private Level100ComponentDefinition[] AdmitComponents(IEnumerable<Level100ComponentDefinition>? components)
    {
        Level100ComponentDefinition[] componentArray = components?.ToArray() ?? [];
        var children = new HashSet<string>(StringComparer.Ordinal);
        foreach (Level100ComponentDefinition component in componentArray)
        {
            if (component is null ||
                !_actorsByIdentity.TryGetValue(component.ChildIdentity, out Level100ActorDefinition? child) ||
                !_actorsByIdentity.ContainsKey(component.ParentIdentity) ||
                !children.Add(component.ChildIdentity) ||
                children.Contains(component.ParentIdentity) ||
                child.ScriptName is not null ||
                !StringComparer.Ordinal.Equals(child.DefinitionName, component.DefinitionName))
            {
                throw new ArgumentException("Invalid component definition.", nameof(components));
            }
        }

        return componentArray;
    }

    /// <summary>
    /// The base world's pines, each of which takes one draw from the gameplay
    /// stream when <c>LoadWorld</c> initialises it (the RE lane's construction
    /// order). Zero for a definition set without the base world's trees.
    /// </summary>
    public int BaseWorldPineCount { get; }

    public IReadOnlyList<Level100ActorDefinition> Actors => _actors;

    public IReadOnlyList<Level100SpawnDefinition> Spawns => _spawns;

    public IReadOnlyList<Level100WaypointPathDefinition> WaypointPaths => _waypointPaths;

    public IReadOnlyList<Level100ActorMotionDefinition> MotionDefinitions =>
        _motionDefinitions;

    /// <summary>
    /// The definition name of a type-28 squad's own thing: the loader's label
    /// for a row with no unit record, as Level 100's manifest names them.
    /// </summary>
    public const string SquadDefinitionName = "Level Actor Type 28";

    /// <summary>The level's type-28 squads, in row order.</summary>
    public IReadOnlyList<Level100SquadDefinition> Squads => _squads;

    /// <summary>The components built inside their parents' construction.</summary>
    public IReadOnlyList<Level100ComponentDefinition> Components => _components;

    /// <summary>The level's <c>CSafeSide</c>s, in construction order.</summary>
    public IReadOnlyList<Level100SafeSideDefinition> SafeSides => _safeSides;

    public string IdentitySha256 { get; }

    /// <summary>
    /// The career world these definitions were built for. Defaults to the
    /// graph root. The set shape is shared; the world number is what stops
    /// a world-110 projection from being silently treated as Level 100.
    /// </summary>
    public int WorldNumber { get; }

    public Level100WaypointPathDefinition GetWaypointPath(string name) =>
        _waypointPathsByName.TryGetValue(name, out Level100WaypointPathDefinition? path)
            ? path
            : throw new KeyNotFoundException($"Level 100 waypoint path '{name}' does not exist.");

    public Level100ActorMotionDefinition GetMotionDefinition(string name) =>
        _motionDefinitionsByName.TryGetValue(name, out Level100ActorMotionDefinition? definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Level 100 actor motion definition '{name}' does not exist.");

    internal Level100ActorMotionDefinition? FindMotionDefinition(string? name) =>
        name is not null &&
        _motionDefinitionsByName.TryGetValue(
            name,
            out Level100ActorMotionDefinition? definition)
            ? definition
            : null;

    internal Level100ActorDefinition GetActorDefinition(string identity) =>
        _actorsByIdentity.TryGetValue(identity, out Level100ActorDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Level 100 actor definition '{identity}' does not exist.");

    internal Level100SpawnDefinition GetSpawnDefinition(string identity) =>
        _spawnsByIdentity.TryGetValue(identity, out Level100SpawnDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Level 100 spawn definition '{identity}' does not exist.");

    internal Level100SpawnDefinition? FindSpawnDefinition(
        string ownerDefinitionIdentity,
        string definitionName,
        string spawnerName,
        string scriptName) => _spawnsByRequest.GetValueOrDefault(
            new SpawnKey(ownerDefinitionIdentity, definitionName, spawnerName, scriptName));

    private static void ValidateActorDefinition(Level100ActorDefinition definition, int expectedOrder)
    {
        if (definition.AuthoredOrder != expectedOrder ||
            string.IsNullOrWhiteSpace(definition.DefinitionIdentity) ||
            definition.Name is null ||
            definition.AuthoredTransform is null ||
            definition.InitialPose is null ||
            !HasFiniteAuthoredTransform(definition.AuthoredTransform) ||
            !HasFinitePose(definition.InitialPose) ||
            (definition.ThingTypeMask & ~Level100ReleasedThingTypeMasks.ProvenBits) != 0 ||
            definition.InitialHealth < 0 ||
            definition.Allegiance is < 0 or > 2 ||
            definition.TargetOrdinal < 0 ||
            (definition.TargetGroup == Level100MissionTargetGroup.None) !=
                (definition.TargetOrdinal == 0) ||
            (definition.Trigger.HasValue && definition.TargetGroup != Level100MissionTargetGroup.None))
        {
            throw new ArgumentException(
                $"Invalid Level 100 actor definition at authored order {expectedOrder}.");
        }
    }

    private static void ValidateSpawnDefinition(Level100SpawnDefinition definition, int expectedOrder)
    {
        if (definition.AuthoredOrder != expectedOrder ||
            string.IsNullOrWhiteSpace(definition.DefinitionIdentity) ||
            string.IsNullOrWhiteSpace(definition.OwnerDefinitionIdentity) ||
            string.IsNullOrWhiteSpace(definition.DefinitionName) ||
            string.IsNullOrWhiteSpace(definition.SpawnerName) ||
            string.IsNullOrWhiteSpace(definition.ScriptName) ||
            definition.InitialPose is null ||
            definition.AuthoredEmitterTransform is null ||
            !HasFinitePose(definition.InitialPose) ||
            (definition.ThingTypeMask & ~Level100ReleasedThingTypeMasks.ProvenBits) != 0 ||
            definition.InitialHealth < 0 ||
            definition.FixedTargetOrdinal < 0 ||
            (definition.TargetGroup == Level100MissionTargetGroup.None &&
                (definition.FixedTargetOrdinal != 0 || definition.MaximumGroupActors != 0)) ||
            (definition.TargetGroup != Level100MissionTargetGroup.None &&
                (definition.MaximumGroupActors <= 0 ||
                 definition.FixedTargetOrdinal > definition.MaximumGroupActors)) ||
            !HasFiniteEmitterTransform(definition.AuthoredEmitterTransform))
        {
            throw new ArgumentException(
                $"Invalid Level 100 spawn definition at authored order {expectedOrder}.");
        }
        if (definition.SpawnerExitWaypoints is { } points &&
            (definition.SpawnerName is not ("SpawnerA" or "SpawnerB") ||
             points.Count == 0 || points.Any(point => point is null || point.Selector <= 0 ||
                 point.ModelTransform is null || !HasFiniteEmitterTransform(point.ModelTransform)) ||
             points.Select(point => point.Selector).Distinct().Count() != points.Count))
            throw new ArgumentException("Invalid or ambiguous spawner exit waypoint input.");
    }

    private static bool HasFiniteEmitterTransform(Level100SpawnerTransform transform)
    {
        int[] components =
        [
            transform.LocalPositionFloatBits.X,
            transform.LocalPositionFloatBits.Y,
            transform.LocalPositionFloatBits.Z,
            transform.LocalBasisFloatBits.Row0X,
            transform.LocalBasisFloatBits.Row0Y,
            transform.LocalBasisFloatBits.Row0Z,
            transform.LocalBasisFloatBits.Row1X,
            transform.LocalBasisFloatBits.Row1Y,
            transform.LocalBasisFloatBits.Row1Z,
            transform.LocalBasisFloatBits.Row2X,
            transform.LocalBasisFloatBits.Row2Y,
            transform.LocalBasisFloatBits.Row2Z,
        ];
        return components.All(component => float.IsFinite(BitConverter.Int32BitsToSingle(component)));
    }

    private static bool HasFiniteAuthoredTransform(Level100AuthoredTransform transform)
    {
        int[] components =
        [
            transform.RetailPositionFloatBits.X,
            transform.RetailPositionFloatBits.Y,
            transform.RetailPositionFloatBits.Z,
            transform.RetailEulerFloatBits.X,
            transform.RetailEulerFloatBits.Y,
            transform.RetailEulerFloatBits.Z,
            transform.RetailBasisFloatBits.Row0X,
            transform.RetailBasisFloatBits.Row0Y,
            transform.RetailBasisFloatBits.Row0Z,
            transform.RetailBasisFloatBits.Row1X,
            transform.RetailBasisFloatBits.Row1Y,
            transform.RetailBasisFloatBits.Row1Z,
            transform.RetailBasisFloatBits.Row2X,
            transform.RetailBasisFloatBits.Row2Y,
            transform.RetailBasisFloatBits.Row2Z,
        ];
        return components.All(component => float.IsFinite(BitConverter.Int32BitsToSingle(component)));
    }

    private static bool HasFinitePose(Level100ActorPoseSnapshot pose) =>
        HasFiniteBasis(pose.BasisFloatBits);

    private static bool HasFiniteBasis(Level100FloatBasis3Bits basis) =>
        new[]
        {
            basis.Row0X, basis.Row0Y, basis.Row0Z,
            basis.Row1X, basis.Row1Y, basis.Row1Z,
            basis.Row2X, basis.Row2Y, basis.Row2Z,
        }.All(component => float.IsFinite(BitConverter.Int32BitsToSingle(component)));

    private static bool HasFiniteWaypointComponents(
        Level100FloatVector4Bits components) =>
        new[] { components.X, components.Y, components.Z, components.W }
            .All(component =>
                float.IsFinite(BitConverter.Int32BitsToSingle(component)));

    private static void ValidateMotionDefinition(
        Level100ActorMotionDefinition definition,
        int expectedOrder)
    {
        bool groundVehicle =
            definition.MotionClass == Level100ActorMotionClass.GroundVehicle;
        bool hasValidGroundFields =
            definition.MaximumSpeedFloatBits.HasValue &&
            definition.MaximumTurnRadiansPerBaseTickFloatBits.HasValue &&
            float.IsFinite(BitConverter.Int32BitsToSingle(
                definition.MaximumSpeedFloatBits.Value)) &&
            BitConverter.Int32BitsToSingle(
                definition.MaximumSpeedFloatBits.Value) > 0.0f &&
            float.IsFinite(BitConverter.Int32BitsToSingle(
                definition.MaximumTurnRadiansPerBaseTickFloatBits.Value)) &&
            BitConverter.Int32BitsToSingle(
                definition.MaximumTurnRadiansPerBaseTickFloatBits.Value) > 0.0f &&
            definition.FullGuideBaseTicks is > 0 &&
            definition.CoreGroundOriginOffsetMillimeters is > 0;
        bool hasNoGroundFields =
            !definition.MaximumSpeedFloatBits.HasValue &&
            !definition.MaximumTurnRadiansPerBaseTickFloatBits.HasValue &&
            !definition.FullGuideBaseTicks.HasValue &&
            !definition.CoreGroundOriginOffsetMillimeters.HasValue;

        if (definition.AuthoredOrder != expectedOrder ||
            string.IsNullOrWhiteSpace(definition.DefinitionName) ||
            !Enum.IsDefined(definition.MotionClass) ||
            definition.BehaviorSerializedType <= 0 ||
            definition.BehaviorInternalId < 0 ||
            definition.SteamClassVtableAddress <= 0 ||
            definition.ArrivalRadiusMillimeters <= 0 ||
            (groundVehicle
                ? !hasValidGroundFields
                : !hasNoGroundFields))
        {
            throw new ArgumentException(
                $"Invalid Level 100 motion definition at authored order {expectedOrder}.");
        }
        if (definition.WeaponMounts is { } mounts &&
            (definition.MotionClass != Level100ActorMotionClass.Plane ||
             mounts.Any(mount => mount is null || mount.Use is null ||
                 string.IsNullOrWhiteSpace(mount.Use.DefinitionName) ||
                 mount.Use.TagName is not ("GunA" or "GunB") || mount.Selector <= 0 ||
                 !HasFiniteBasis(mount.ModelPose.BasisFloatBits) ||
                 !new[] { mount.ModelPose.PositionFloatBits.X, mount.ModelPose.PositionFloatBits.Y,
                     mount.ModelPose.PositionFloatBits.Z }
                     .All(word => float.IsFinite(BitConverter.Int32BitsToSingle(word))))))
            throw new ArgumentException("Invalid aircraft weapon mount input.");
    }

    private static string ComputeIdentity(
        IReadOnlyList<Level100ActorDefinition> actors,
        IReadOnlyList<Level100SpawnDefinition> spawns,
        IReadOnlyList<Level100WaypointPathDefinition> waypointPaths,
        IReadOnlyList<Level100ActorMotionDefinition> motionDefinitions,
        int baseWorldPineCount,
        IReadOnlyList<Level100SquadDefinition> squads,
        IReadOnlyList<Level100ComponentDefinition> components,
        IReadOnlyList<Level100SafeSideDefinition> safeSides)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(s_identityMagic);
            bool hasSpawnerExits = spawns.Any(spawn => spawn.SpawnerExitWaypoints is not null);
            bool hasWeaponMounts = motionDefinitions.Any(definition => definition.WeaponMounts is not null);
            // Format 7 adds spawner exits and 8 weapon mounts, and 8 includes
            // the exit fields even when all exits are null. Format 9 appends
            // the base world's pine count, and only a set that carries pines
            // selects it. Formats 10-13 are 6-9 with each path's nodes in
            // retail list order and each node's own target. Format 14 is 13
            // plus each actor's allegiance, each motion row's air scalars, the
            // squads and the components, and only a set that carries any of
            // them selects it. Format 15 is 14 plus each motion row's Big
            // flag, MinAltitude and render radius and the safe sides; a set
            // that carries any of them selects it.
            bool hasFlight = safeSides.Count > 0 || motionDefinitions.Any(definition =>
                definition.Big is not null || definition.MinimumAltitudeFloatBits is not null ||
                definition.MeshRadiusFloatBits is not null);
            bool hasSides = hasFlight || squads.Count > 0 || components.Count > 0 ||
                actors.Any(actor => actor.Allegiance != 0) ||
                motionDefinitions.Any(definition =>
                    definition.AirVelocityFloatBits is not null || definition.AirTurnRateFloatBits is not null);
            writer.Write(hasFlight ? 15 : hasSides ? 14 : baseWorldPineCount > 0 ? 13 : hasWeaponMounts ? 12 : hasSpawnerExits ? 11 : 10);
            writer.Write(actors.Count);
            foreach (Level100ActorDefinition actor in actors)
            {
                writer.Write(actor.AuthoredOrder);
                writer.Write(actor.DefinitionIdentity);
                writer.Write(actor.Name);
                WriteNullableString(writer, actor.DefinitionName);
                WriteNullableString(writer, actor.ScriptName);
                WriteNullableString(writer, actor.MeshBinding);
                writer.Write(actor.ThingTypeMask);
                writer.Write(actor.IsStatic);
                writer.Write(actor.Active);
                writer.Write(actor.InitialHealth);
                WriteVector(writer, actor.AuthoredTransform.RetailPositionFloatBits);
                WriteVector(writer, actor.AuthoredTransform.RetailEulerFloatBits);
                WriteBasis(writer, actor.AuthoredTransform.RetailBasisFloatBits);
                WritePose(writer, actor.InitialPose);
                writer.Write((int)actor.TargetGroup);
                writer.Write(actor.TargetOrdinal);
                writer.Write(actor.Trigger.HasValue);
                if (actor.Trigger.HasValue)
                {
                    writer.Write((int)actor.Trigger.Value);
                }
            }

            writer.Write(spawns.Count);
            foreach (Level100SpawnDefinition spawn in spawns)
            {
                writer.Write(spawn.AuthoredOrder);
                writer.Write(spawn.DefinitionIdentity);
                writer.Write(spawn.OwnerDefinitionIdentity);
                writer.Write(spawn.DefinitionName);
                writer.Write(spawn.SpawnerName);
                writer.Write(spawn.ScriptName);
                WriteNullableString(writer, spawn.MeshBinding);
                writer.Write(spawn.ThingTypeMask);
                writer.Write(spawn.Active);
                writer.Write(spawn.InitialHealth);
                WritePose(writer, spawn.InitialPose);
                WriteVector(writer, spawn.AuthoredEmitterTransform.LocalPositionFloatBits);
                WriteBasis(writer, spawn.AuthoredEmitterTransform.LocalBasisFloatBits);
                writer.Write((int)spawn.TargetGroup);
                writer.Write(spawn.FixedTargetOrdinal);
                writer.Write(spawn.MaximumGroupActors);
                if (hasSpawnerExits || hasWeaponMounts)
                {
                    writer.Write(spawn.SpawnerExitWaypoints is not null);
                    if (spawn.SpawnerExitWaypoints is { } points)
                    {
                        writer.Write(points.Count);
                        foreach (Level100SpawnerExitPoint point in points)
                        {
                            writer.Write(point.Selector);
                            WriteVector(writer, point.ModelTransform.LocalPositionFloatBits);
                            WriteBasis(writer, point.ModelTransform.LocalBasisFloatBits);
                        }
                    }
                }
            }

            writer.Write(waypointPaths.Count);
            foreach (Level100WaypointPathDefinition path in waypointPaths)
            {
                writer.Write(path.Name);
                writer.Write(path.Points.Count);
                foreach (Level100WaypointPointDefinition point in path.Points)
                {
                    writer.Write(point.NodeIndex);
                    writer.Write(point.PositionMillimeters.X);
                    writer.Write(point.PositionMillimeters.Y);
                    writer.Write(point.PositionMillimeters.Z);
                    writer.Write(point.RetailComponentsFloatBits.X);
                    writer.Write(point.RetailComponentsFloatBits.Y);
                    writer.Write(point.RetailComponentsFloatBits.Z);
                    writer.Write(point.RetailComponentsFloatBits.W);
                    // The target decides the route, so it is part of the
                    // identity; -1 is a node with none.
                    writer.Write(point.TargetNodeIndex ?? -1);
                }
            }

            writer.Write(motionDefinitions.Count);
            foreach (Level100ActorMotionDefinition definition in motionDefinitions)
            {
                writer.Write(definition.AuthoredOrder);
                writer.Write(definition.DefinitionName);
                writer.Write((int)definition.MotionClass);
                writer.Write(definition.BehaviorSerializedType);
                writer.Write(definition.BehaviorInternalId);
                writer.Write(definition.SteamClassVtableAddress);
                writer.Write(definition.ArrivalRadiusMillimeters);
                WriteNullableInt(writer, definition.MaximumSpeedFloatBits);
                WriteNullableInt(
                    writer,
                    definition.MaximumTurnRadiansPerBaseTickFloatBits);
                WriteNullableInt(writer, definition.FullGuideBaseTicks);
                WriteNullableInt(
                    writer,
                    definition.CoreGroundOriginOffsetMillimeters);
                if (hasWeaponMounts)
                {
                    writer.Write(definition.WeaponMounts is not null);
                    if (definition.WeaponMounts is { } mounts)
                    {
                        writer.Write(mounts.Count);
                        foreach (Level100ActorWeaponMountDefinition mount in mounts)
                        {
                            writer.Write(mount.Use.DefinitionName);
                            writer.Write(mount.Use.TagName);
                            writer.Write(mount.Use.RawCreationFlags);
                            writer.Write(mount.Selector);
                            WriteVector(writer, mount.ModelPose.PositionFloatBits);
                            WriteBasis(writer, mount.ModelPose.BasisFloatBits);
                        }
                    }
                }
            }

            if (baseWorldPineCount > 0)
            {
                writer.Write(baseWorldPineCount);
            }

            if (hasSides)
            {
                foreach (Level100ActorDefinition actor in actors)
                {
                    writer.Write(actor.Allegiance);
                }

                foreach (Level100ActorMotionDefinition definition in motionDefinitions)
                {
                    WriteNullableInt(writer, definition.AirVelocityFloatBits);
                    WriteNullableInt(writer, definition.AirTurnRateFloatBits);
                }

                writer.Write(squads.Count);
                foreach (Level100SquadDefinition squad in squads)
                {
                    writer.Write(squad.DefinitionIdentity);
                    writer.Write(squad.Name);
                    writer.Write(squad.DefinitionName);
                    writer.Write(squad.MemberIdentities.Count);
                    foreach (string member in squad.MemberIdentities)
                    {
                        writer.Write(member);
                    }

                    WriteNullableString(writer, squad.ScriptName);
                    writer.Write(squad.Active);
                    writer.Write(squad.Allegiance);
                    writer.Write(squad.Mode);
                    WriteVector(writer, squad.AuthoredTransform.RetailPositionFloatBits);
                    WriteVector(writer, squad.AuthoredTransform.RetailEulerFloatBits);
                    WriteBasis(writer, squad.AuthoredTransform.RetailBasisFloatBits);
                }

                writer.Write(components.Count);
                foreach (Level100ComponentDefinition component in components)
                {
                    writer.Write(component.ChildIdentity);
                    writer.Write(component.ParentIdentity);
                    writer.Write(component.DefinitionName);
                }
            }

            if (hasFlight)
            {
                foreach (Level100ActorMotionDefinition definition in motionDefinitions)
                {
                    writer.Write(definition.Big is { } big ? (big ? 1 : 0) : -1);
                    WriteNullableInt(writer, definition.MinimumAltitudeFloatBits);
                    WriteNullableInt(writer, definition.MeshRadiusFloatBits);
                }

                writer.Write(safeSides.Count);
                foreach (Level100SafeSideDefinition safeSide in safeSides)
                {
                    writer.Write(safeSide.BaseWorld);
                    writer.Write(safeSide.Row);
                    WriteVector(writer, safeSide.RetailPositionFloatBits);
                    writer.Write(safeSide.Allegiance);
                }
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WritePose(BinaryWriter writer, Level100ActorPoseSnapshot pose)
    {
        WriteVector(writer, pose.PositionMillimeters);
        WriteBasis(writer, pose.BasisFloatBits);
        WriteVector(writer, pose.LinearVelocityMillimetersPerTick);
        WriteVector(writer, pose.AngularVelocityMicroRadiansPerTick);
    }

    private static void WriteVector(BinaryWriter writer, SimVector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static void WriteVector(BinaryWriter writer, Level100FloatVector3Bits vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }

    private static void WriteBasis(BinaryWriter writer, Level100FloatBasis3Bits basis)
    {
        writer.Write(basis.Row0X);
        writer.Write(basis.Row0Y);
        writer.Write(basis.Row0Z);
        writer.Write(basis.Row1X);
        writer.Write(basis.Row1Y);
        writer.Write(basis.Row1Z);
        writer.Write(basis.Row2X);
        writer.Write(basis.Row2Y);
        writer.Write(basis.Row2Z);
    }

    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    private static void WriteNullableInt(BinaryWriter writer, int? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
        {
            writer.Write(value.Value);
        }
    }

    private readonly record struct SpawnKey(
        string OwnerDefinitionIdentity,
        string DefinitionName,
        string SpawnerName,
        string ScriptName);
}

public enum Level100ActorLifecycle
{
    Alive = 0,
    StartedDying = 1,
    Destroyed = 2,
    // The script received died(), but physical shutdown has not run yet.
    DiedAwaitingShutdown = 3,
}

public enum Level100ActorFactKind
{
    Hit = 1,
    StartedDying = 2,
    Died = 3,
    TriggerDispatchReady = 4,

    /// <summary>The thing left the world without dying (event 2000).</summary>
    ShutDown = 5,
}

public sealed record Level100ActorFactSnapshot(
    long Sequence,
    Level100ActorFactKind Kind,
    Level100ActorId ActorId,
    Level100ActorId? OtherActorId,
    uint OtherThingTypeMask);

public sealed record Level100ActorSnapshot(
    Level100ActorId ActorId,
    string DefinitionIdentity,
    string Name,
    string? DefinitionName,
    string? ScriptName,
    string? MeshBinding,
    uint ThingTypeMask,
    Level100ActorId? SpawnOwnerId,
    string? SpawnerName,
    bool IsStatic,
    bool Active,
    bool IsObjective,
    Level100ActorLifecycle Lifecycle,
    int Health,
    Level100ActorPoseSnapshot Pose,
    Level100MissionTargetGroup TargetGroup,
    int TargetOrdinal,
    Level100MissionTrigger? Trigger,
    bool TriggerEntered,
    Level100MissionJetModeState? TriggerEntryJetModeState,
    bool TriggerEventDispatched);

public sealed record Level100ActorBaseStateSnapshot(
    Level100ActorId ActorId,
    ThingActorBaseStateSnapshot State);

public sealed record Level100ActorRegistrySnapshot(
    string DefinitionSetIdentitySha256,
    int NextActorId,
    long NextFactSequence,
    IReadOnlyList<Level100ActorSnapshot> Actors,
    IReadOnlyList<Level100ActorFactSnapshot> PendingFacts,
    IReadOnlyList<Level100ActorBaseStateSnapshot> BaseStates)
{
    /// <summary>
    /// The base-world rows an earlier level lost, which this world's load
    /// skipped (the career's carry-over), in row order.
    /// </summary>
    public IReadOnlyList<int> LostBaseRows { get; init; } = [];
}

/// <summary>
/// Native Level 100 object identity and lifecycle owner. It contains no
/// movement, AI, collision, damage, or mission progression policy.
/// </summary>
/// <remarks>
/// The source-shaped <see cref="ThingActorBaseState"/> is canonical mutable
/// base state. Three Level-100 compatibility projections are intentional:
/// <see cref="Level100ActorSnapshot.ThingTypeMask"/> remains the pre-existing
/// leaf-bit contract while the base snapshot carries the full actor lineage;
/// public <see cref="SetPose"/> is a setup/teleport reset that collapses old and
/// current pose while mechanics uses <see cref="AdvancePose"/>; and mechanics
/// stop clears angular velocity after the source CActor linear-velocity stop.
/// Existing Active/lifecycle behavior is retained rather than replaced by a
/// second gameplay framework.
/// </remarks>
public sealed class Level100ActorRegistry
{
    private sealed class Actor
    {
        internal required Level100ActorId ActorId { get; init; }
        internal required string DefinitionIdentity { get; init; }
        internal required string Name { get; init; }
        internal string? DefinitionName { get; init; }
        internal string? ScriptName { get; set; }
        internal string? MeshBinding { get; init; }
        internal Level100ActorId? SpawnOwnerId { get; init; }
        internal string? SpawnerName { get; init; }
        internal bool IsStatic { get; init; }
        internal bool Active { get; set; }
        internal bool IsObjective { get; set; }
        internal int FlagWord { get; set; }
        internal Level100ActorLifecycle Lifecycle { get; set; }
        internal int Health { get; set; }
        internal required ThingActorBaseState BaseState { get; init; }
        internal Level100MissionTargetGroup TargetGroup { get; init; }
        internal int TargetOrdinal { get; init; }
        internal Level100MissionTrigger? Trigger { get; init; }
        internal bool TriggerEntered { get; set; }
        internal Level100MissionJetModeState? TriggerEntryJetModeState { get; set; }
        internal bool TriggerEventDispatched { get; set; }
    }

    private readonly Level100ActorDefinitionSet _definitions;
    private readonly RetailWorldTerrain _terrain;
    private readonly bool _initializeSupport;
    private readonly SortedDictionary<int, Actor> _actors = [];
    private readonly List<Level100ActorFactSnapshot> _pendingFacts = [];
    private int _nextActorId = 1;
    private long _nextFactSequence = 1;

    public Level100ActorRegistry(Level100ActorDefinitionSet definitions)
        : this(definitions, RetailWorldTerrain.World100, initializeSupport: true)
    {
    }

    /// <summary>
    /// A world built from the career's base-world carry-over. Each BSWD row in
    /// <paramref name="lostBaseRows"/> was lost in an earlier level, so the
    /// load skips it (<c>CCareer::DoesBaseThingExist</c>, <c>0x0041bb20</c>,
    /// called at <c>0x0050cf8f</c>) and it never becomes a thing.
    /// </summary>
    public Level100ActorRegistry(Level100ActorDefinitionSet definitions, IReadOnlyCollection<int> lostBaseRows)
        : this(definitions, RetailWorldTerrain.World100, initializeSupport: true, lostBaseRows)
    {
    }

    // Partial world construction retains authored positions until class Init
    // owns its ground/water overrides. The existing Level 100 policy is unchanged.
    internal Level100ActorRegistry(
        Level100ActorDefinitionSet definitions,
        RetailWorldTerrain terrain,
        bool initializeSupport,
        IReadOnlyCollection<int>? lostBaseRows = null)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _initializeSupport = initializeSupport;
        _lostBaseRows = AdmitLostBaseRows(lostBaseRows ?? []);
        foreach (Level100ActorDefinition definition in definitions.Actors)
        {
            if (IsLostBaseRow(definition))
            {
                continue;
            }

            Level100ActorId actorId = AllocateId();
            _actors.Add(
                actorId.Value,
                CreateActor(actorId, definition, SeatOnGround(
                    definition.DefinitionName,
                    definition.InitialPose)));
        }
    }

    public Level100ActorRegistry(
        Level100ActorDefinitionSet definitions,
        Level100ActorRegistrySnapshot snapshot)
        : this(definitions, snapshot, RetailWorldTerrain.World100, initializeSupport: true)
    {
    }

    internal Level100ActorRegistry(
        Level100ActorDefinitionSet definitions,
        Level100ActorRegistrySnapshot snapshot,
        RetailWorldTerrain terrain,
        bool initializeSupport)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _initializeSupport = initializeSupport;
        ArgumentNullException.ThrowIfNull(snapshot);
        _lostBaseRows = AdmitLostBaseRows(snapshot.LostBaseRows ?? []);
        if (!StringComparer.Ordinal.Equals(
                snapshot.DefinitionSetIdentitySha256,
                definitions.IdentitySha256) ||
            snapshot.NextActorId <= 0 ||
            snapshot.NextFactSequence <= 0)
        {
            throw new ArgumentException(
                "Actor registry snapshot does not match its immutable definition set.",
                nameof(snapshot));
        }

        _nextActorId = snapshot.NextActorId;
        _nextFactSequence = snapshot.NextFactSequence;
        ArgumentNullException.ThrowIfNull(snapshot.Actors);
        ArgumentNullException.ThrowIfNull(snapshot.PendingFacts);
        ArgumentNullException.ThrowIfNull(snapshot.BaseStates);
        if (snapshot.Actors.Any(actor => actor is null) ||
            snapshot.PendingFacts.Any(fact => fact is null) ||
            snapshot.BaseStates.Any(item => item is null || item.State is null))
        {
            throw new ArgumentException(
                "Actor registry snapshot contains a null record.",
                nameof(snapshot));
        }

        Dictionary<int, ThingActorBaseStateSnapshot> baseStates =
            snapshot.BaseStates.ToDictionary(
                item => item.ActorId.Value,
                item => item.State);
        if (baseStates.Count != snapshot.BaseStates.Count)
        {
            throw new ArgumentException(
                "Actor registry snapshot has duplicate Thing/Actor base state.",
                nameof(snapshot));
        }

        foreach (Level100ActorSnapshot actor in snapshot.Actors.OrderBy(item => item.ActorId.Value))
        {
            if (actor.ActorId.Value <= 0 || actor.ActorId.Value >= _nextActorId ||
                !baseStates.TryGetValue(actor.ActorId.Value, out ThingActorBaseStateSnapshot? baseState) ||
                !_actors.TryAdd(actor.ActorId.Value, RestoreActor(actor, baseState)))
            {
                throw new ArgumentException("Actor registry snapshot has invalid identities.", nameof(snapshot));
            }
        }

        if (_actors.Count != _nextActorId - 1 ||
            baseStates.Count != _actors.Count ||
            definitions.Actors.Any(definition =>
                _actors.Values.Count(actor =>
                    !actor.SpawnOwnerId.HasValue &&
                    StringComparer.Ordinal.Equals(
                        actor.DefinitionIdentity,
                        definition.DefinitionIdentity)) != (IsLostBaseRow(definition) ? 0 : 1)))
        {
            throw new ArgumentException(
                "Actor registry snapshot does not contain each authored actor exactly once.",
                nameof(snapshot));
        }

        foreach (Actor actor in _actors.Values)
        {
            ValidateRestoredActor(
                actor,
                snapshot.Actors.Single(item => item.ActorId == actor.ActorId),
                snapshot);
        }

        if (_actors.Values
            .Where(actor => actor.TargetGroup != Level100MissionTargetGroup.None)
            .GroupBy(actor => (actor.TargetGroup, actor.TargetOrdinal))
            .Any(group => group.Key.TargetOrdinal <= 0 || group.Count() != 1))
        {
            throw new ArgumentException(
                "Actor registry snapshot has duplicate or invalid mission ordinals.",
                nameof(snapshot));
        }

        _pendingFacts.AddRange(snapshot.PendingFacts.OrderBy(item => item.Sequence));
        if (_pendingFacts.Any(item =>
                item.Sequence <= 0 ||
                item.Sequence >= _nextFactSequence ||
                !Enum.IsDefined(item.Kind) ||
                !_actors.ContainsKey(item.ActorId.Value) ||
                (item.OtherActorId.HasValue && !_actors.ContainsKey(item.OtherActorId.Value.Value))) ||
            _pendingFacts.Any(item =>
                (item.OtherThingTypeMask & ~Level100ReleasedThingTypeMasks.ProvenBits) != 0 ||
                (item.Kind != Level100ActorFactKind.Hit &&
                    (item.OtherActorId.HasValue || item.OtherThingTypeMask != 0)) ||
                (item.Kind == Level100ActorFactKind.Hit && item.OtherActorId.HasValue &&
                    SpecificThingTypeMask(_actors[item.OtherActorId.Value.Value]) !=
                        item.OtherThingTypeMask)) ||
            _pendingFacts.Select(item => item.Sequence).Distinct().Count() != _pendingFacts.Count)
        {
            throw new ArgumentException("Actor registry snapshot has invalid fact sequencing.", nameof(snapshot));
        }
    }

    public Level100ActorRegistrySnapshot Snapshot => new(
        _definitions.IdentitySha256,
        _nextActorId,
        _nextFactSequence,
        Array.AsReadOnly(_actors.Values.Select(SnapshotActor).ToArray()),
        Array.AsReadOnly(_pendingFacts.OrderBy(item => item.Sequence).ToArray()),
        Array.AsReadOnly(_actors.Values
            .Select(actor => new Level100ActorBaseStateSnapshot(
                actor.ActorId,
                actor.BaseState.Snapshot))
            .ToArray()))
    {
        LostBaseRows = _lostBaseRows,
    };

    /// <summary>The base-world rows this world's load skipped, in row order.</summary>
    internal IReadOnlyList<int> LostBaseRows => _lostBaseRows;

    private readonly IReadOnlyList<int> _lostBaseRows;

    /// <summary>A base-world row's definition identity (<c>wres:bswd:NNNN</c>).</summary>
    internal static string BaseRowIdentity(int row) =>
        $"wres:bswd:{row.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)}";

    private bool IsLostBaseRow(Level100ActorDefinition definition) =>
        _lostBaseRows.Count > 0 &&
        _lostBaseRows.Any(row => StringComparer.Ordinal.Equals(BaseRowIdentity(row), definition.DefinitionIdentity));

    /// <summary>
    /// A lost row that Core does not build as an actor (the two SafeSides, rows
    /// 21 and 22) has nothing to skip.
    /// </summary>
    private static IReadOnlyList<int> AdmitLostBaseRows(IReadOnlyCollection<int> lostBaseRows)
    {
        int[] rows = lostBaseRows.Order().ToArray();
        if (rows.Distinct().Count() != rows.Length ||
            rows.Any(row => row < 0 || row >= RetailFillOutEndLevelData.Level100BaseWorldThingCount))
        {
            throw new ArgumentException("Lost base rows must be distinct base-world rows.", nameof(lostBaseRows));
        }

        return Array.AsReadOnly(rows);
    }

    /// <summary>
    /// The immutable definition set this registry was built from. Its
    /// <see cref="Level100ActorDefinitionSet.WorldNumber"/> is the admission
    /// stamp a mission compares with its requested career world.
    /// </summary>
    public Level100ActorDefinitionSet Definitions => _definitions;

    public Level100ActorId? GetThingRef(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Actor[] matches = _actors.Values
            .Where(actor => string.Equals(actor.Name, name, StringComparison.Ordinal))
            .ToArray();
        return matches.Length == 1 ? matches[0].ActorId : null;
    }

    public Level100ActorSnapshot GetActor(Level100ActorId actorId) =>
        SnapshotActor(Require(actorId));

    /// <summary>The career world the registry's definitions were built for.</summary>
    internal int WorldNumber => _definitions.WorldNumber;

    /// <summary>
    /// A thing's authored allegiance; a spawned thing starts at 0 until a
    /// script sets one.
    /// </summary>
    internal int GetAuthoredAllegiance(Level100ActorId actorId)
    {
        Actor actor = Require(actorId);
        return actor.SpawnOwnerId is null ? _definitions.GetActorDefinition(actor.DefinitionIdentity).Allegiance : 0;
    }

    /// <summary>A unit's behaviour selector (its motion row's), or null for a thing with none.</summary>
    internal int? FindMotionBehaviour(Level100ActorId actorId) =>
        _definitions.FindMotionDefinition(Require(actorId).DefinitionName)?.BehaviorInternalId;

    /// <summary>
    /// <c>GetNumUnits(behaviour, allegiance)</c> (<c>0x00535590</c>) reads the
    /// per-side unit counts, indexed by the profile's behaviour selector
    /// (<c>+0xe0</c>): allegiance 0 at <c>0x008551c0</c>, 1 at
    /// <c>0x00855228</c>, and 0 for any other side. <c>CUnit::Init</c>
    /// increments them (<c>0x004f90af</c>, <c>0x004f90c4</c>); the start of a
    /// death (<c>0x004fd140</c>) decrements them and sets
    /// <c>TF_REMOVED_UNIT_TYPE</c>, and a unit removed without dying is
    /// decremented once at its shutdown (<c>0x004f97b6-0x004f97ef</c>).
    /// <c>SetAllegiance</c> (<c>0x004fd830</c>) does not move a unit between
    /// the counts. Here: the living units whose motion row has that selector
    /// and whose authored side is that allegiance.
    /// </summary>
    internal int CountUnits(int behaviour, int allegiance) =>
        allegiance is not (0 or 1)
            ? 0
            : _actors.Values.Count(actor =>
                actor.Lifecycle == Level100ActorLifecycle.Alive &&
                FindMotionBehaviour(actor.ActorId) == behaviour &&
                GetAuthoredAllegiance(actor.ActorId) == allegiance);

    /// <summary>A thing's initial health (its definition's), for <c>GetInitialHealth</c>.</summary>
    internal int GetInitialHealth(Level100ActorId actorId)
    {
        Actor actor = Require(actorId);
        return actor.SpawnOwnerId is null
            ? _definitions.GetActorDefinition(actor.DefinitionIdentity).InitialHealth
            : _definitions.GetSpawnDefinition(actor.DefinitionIdentity).InitialHealth;
    }

    public ThingActorBaseStateSnapshot GetBaseState(Level100ActorId actorId) =>
        Require(actorId).BaseState.Snapshot;

    // World110 initializes the same allocation the registry already owns.
    internal ThingActorBaseState GetConstructionState(Level100ActorId actorId)
    {
        if (_definitions.WorldNumber != 110 || _initializeSupport)
            throw new InvalidOperationException("Retail construction state belongs to the World110 construction route.");
        return Require(actorId).BaseState;
    }

    /// <summary>A plane's or dropship's raw air state.</summary>
    internal ThingActorBaseState GetPlaneState(Level100ActorId actorId)
    {
        Actor actor = Require(actorId);
        if (!IsAdmittedAirUnit(actor.DefinitionName) || !actor.BaseState.HasRetailPlaneMotion)
            throw new InvalidOperationException("The actor has no admitted Level100 air-unit state.");
        return actor.BaseState;
    }

    private Actor RequireMutable(Level100ActorId actorId)
    {
        Actor actor = Require(actorId);
        if (actor.BaseState.HasRetailConstruction && !actor.BaseState.HasRetailPlaneMotion)
            throw new NotSupportedException("Retail construction needs its complete world lifecycle before legacy actor mutation.");
        return actor;
    }

    internal Level100ActorPoseSnapshot GetPose(Level100ActorId actorId) =>
        ToLevel100Pose(Require(actorId).BaseState.Snapshot);

    internal int GetHealth(Level100ActorId actorId) => Require(actorId).Health;

    internal bool IsActive(Level100ActorId actorId) => Require(actorId).Active;

    internal Level100ActorLifecycle GetLifecycle(Level100ActorId actorId) =>
        Require(actorId).Lifecycle;

    public uint GetThingTypeMask(Level100ActorId actorId) =>
        SpecificThingTypeMask(Require(actorId));

    /// <summary>
    /// Isolated <see cref="Level100ActorSnapshot.IsObjective"/> names
    /// the rebuild bool. This is the
    /// <c>or/and [esi+0x2c], 0x20</c> word actor-script
    /// SetObjective / UnsetObjective write. Noticeboard stays
    /// unclaimed.
    /// </summary>
    public int FlagWord(Level100ActorId actorId) => Require(actorId).FlagWord;

    public IReadOnlyList<Level100ActorId> SpawnThing(
        Level100ActorId ownerId,
        string definitionName,
        string spawnerName,
        int count,
        string scriptName,
        int eventTimeFloatBits = 0)
    {
        Actor owner = Require(ownerId);
        ArgumentException.ThrowIfNullOrEmpty(definitionName);
        ArgumentException.ThrowIfNullOrEmpty(spawnerName);
        ArgumentException.ThrowIfNullOrEmpty(scriptName);
        Level100SpawnDefinition? definition = _definitions.FindSpawnDefinition(
            owner.DefinitionIdentity,
            definitionName,
            spawnerName,
            scriptName);
        if (count != 1 || definition is null)
        {
            throw new InvalidOperationException(
                $"SpawnThing request is outside the supplied Level 100 definition set: " +
                $"{definitionName}/{count}.");
        }

        Level100ActorPoseSnapshot pose =
            SeatOnGround(definitionName, definition.InitialPose);
        // Validate the complete raw creation before consuming identity or
        // mission ordinal. InitialPose is only a compatibility projection.
        ThingActorBaseState baseState = IsAdmittedPlane(definitionName)
            ? CreateSpawnedPlaneState(owner, definition, eventTimeFloatBits)
            : CreateBaseState(pose, definition.ThingTypeMask);
        int ordinal = AllocateMissionOrdinal(definition);
        Level100ActorId actorId = AllocateId();
        _actors.Add(actorId.Value, new Actor
        {
            ActorId = actorId,
            DefinitionIdentity = definition.DefinitionIdentity,
            Name = $"{definitionName} #{actorId.Value}",
            DefinitionName = definitionName,
            ScriptName = scriptName,
            MeshBinding = definition.MeshBinding,
            SpawnOwnerId = ownerId,
            SpawnerName = spawnerName,
            IsStatic = false,
            Active = definition.Active,
            Lifecycle = Level100ActorLifecycle.Alive,
            Health = ReleasedInitialHealth(definitionName, definition.InitialHealth),
            BaseState = baseState,
            TargetGroup = definition.TargetGroup,
            TargetOrdinal = ordinal,
        });
        return Array.AsReadOnly(new[] { actorId });
    }

    public void Activate(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle == Level100ActorLifecycle.Destroyed)
        {
            throw new InvalidOperationException("A destroyed Level 100 actor cannot be activated.");
        }

        actor.Active = true;
    }

    public void Deactivate(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        actor.Active = false;
        if (actor.Trigger.HasValue && actor.TriggerEntered)
        {
            actor.TriggerEventDispatched = true;
            actor.IsObjective = false;
        }
    }

    public void SetObjective(Level100ActorId actorId, bool objective)
    {
        Actor actor = RequireMutable(actorId);
        if (objective && actor.Lifecycle == Level100ActorLifecycle.Destroyed)
        {
            throw new InvalidOperationException(
                "A destroyed Level 100 actor cannot become an objective.");
        }

        actor.FlagWord = objective
            ? RetailSetObjective.Mark(actor.FlagWord)
            : RetailSetObjective.Unmark(actor.FlagWord);
        actor.IsObjective = (actor.FlagWord & RetailSetObjective.MarkedBit) != 0;
    }

    public void SetScript(Level100ActorId actorId, string scriptName)
    {
        ArgumentException.ThrowIfNullOrEmpty(scriptName);
        if (!Level100MissionProgram.ProgramNamesFor(_definitions.WorldNumber)
                .Contains(scriptName, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"Unknown Level {_definitions.WorldNumber} script '{scriptName}'.");
        }

        RequireMutable(actorId).ScriptName = scriptName;
    }

    /// <summary>
    /// Level-100 setup/test relocation. This deliberately resets old and
    /// current pose together; movement ticks use <see cref="AdvancePose"/>.
    /// </summary>
    public void SetPose(Level100ActorId actorId, Level100ActorPoseSnapshot pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!HasFinitePose(pose))
        {
            throw new ArgumentException("Actor pose basis must contain finite values.", nameof(pose));
        }

        Actor actor = RequireMutable(actorId);
        actor.BaseState.ResetPose(ToBasePose(pose));
        actor.BaseState.SetVelocity(pose.LinearVelocityMillimetersPerTick);
        actor.BaseState.SetAngularVelocity(pose.AngularVelocityMicroRadiansPerTick);
    }

    /// <summary>
    /// Movement-owned pose update. Unlike public <see cref="SetPose"/>, this
    /// preserves the previous current pose as CActor's old pose.
    /// </summary>
    internal void AdvancePose(Level100ActorId actorId, Level100ActorPoseSnapshot pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!HasFinitePose(pose))
        {
            throw new ArgumentException("Actor pose basis must contain finite values.", nameof(pose));
        }

        Actor actor = RequireMutable(actorId);
        actor.BaseState.AdvancePose(ToBasePose(pose));
        actor.BaseState.SetVelocity(pose.LinearVelocityMillimetersPerTick);
        actor.BaseState.SetAngularVelocity(pose.AngularVelocityMicroRadiansPerTick);
    }

    // LF_MOVE preserves old orientation and retained velocity, even when its
    // terrain clamp changes the displacement. Angular velocity here is the
    // compatibility view's reported turn on this update.
    internal void AdvanceLowFidelityPosition(Level100ActorId actorId, SimVector3 position)
    {
        Actor actor = RequireMutable(actorId);
        actor.BaseState.AdvanceLowFidelityPosition(position);
        actor.BaseState.SetAngularVelocity(SimVector3.Zero);
    }

    /// <summary>
    /// Same-tick synchronization after <see cref="AdvancePose"/> has already
    /// captured old pose, or a velocity/orientation-only update.
    /// </summary>
    internal void UpdateCurrentPose(
        Level100ActorId actorId,
        Level100ActorPoseSnapshot pose)
    {
        ArgumentNullException.ThrowIfNull(pose);
        if (!HasFinitePose(pose))
        {
            throw new ArgumentException("Actor pose basis must contain finite values.", nameof(pose));
        }

        Actor actor = RequireMutable(actorId);
        actor.BaseState.UpdateCurrentPose(ToBasePose(pose));
        actor.BaseState.SetVelocity(pose.LinearVelocityMillimetersPerTick);
        actor.BaseState.SetAngularVelocity(pose.AngularVelocityMicroRadiansPerTick);
    }

    /// <summary>
    /// Level-100 compatibility adapter. Source CActor::Stop zeros only linear
    /// velocity; existing mechanics has always stopped both linear and angular
    /// reported velocity, so it calls the source law and then clears angular.
    /// </summary>
    internal void StopMotion(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        actor.BaseState.Stop();
        actor.BaseState.SetAngularVelocity(SimVector3.Zero);
    }

    public void MakeVisible(Level100ActorId actorId) =>
        RequireMutable(actorId).BaseState.MakeVisible();

    public void MakeInvisible(Level100ActorId actorId) =>
        RequireMutable(actorId).BaseState.MakeInvisible();

    public void DeclareOnGround(Level100ActorId actorId, int eventTimeFloatBits) =>
        RequireMutable(actorId).BaseState.DeclareOnGround(eventTimeFloatBits);

    public void DeclareInWater(Level100ActorId actorId, int eventTimeFloatBits) =>
        RequireMutable(actorId).BaseState.DeclareInWater(eventTimeFloatBits);

    public void DeclareOnObject(Level100ActorId actorId, int eventTimeFloatBits) =>
        RequireMutable(actorId).BaseState.DeclareOnObject(eventTimeFloatBits);

    public void SetHealth(Level100ActorId actorId, int health)
    {
        if (health < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(health));
        }

        RequireMutable(actorId).Health = health;
    }

    public void ReportHit(
        Level100ActorId actorId,
        Level100ActorId? otherActorId = null,
        uint otherThingTypeMask = 0)
    {
        _ = RequireMutable(actorId);
        if ((otherThingTypeMask & ~Level100ReleasedThingTypeMasks.ProvenBits) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(otherThingTypeMask));
        }

        if (otherActorId.HasValue)
        {
            uint actorMask = SpecificThingTypeMask(RequireMutable(otherActorId.Value));
            if (otherThingTypeMask != 0 && otherThingTypeMask != actorMask)
            {
                throw new ArgumentException("A hit source actor and type mask disagree.");
            }
            otherThingTypeMask = actorMask;
        }

        EnqueueFact(Level100ActorFactKind.Hit, actorId, otherActorId, otherThingTypeMask);
    }

    public bool ReportStartedDying(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle != Level100ActorLifecycle.Alive)
        {
            return false;
        }

        if (!actor.BaseState.StartDieProcess())
        {
            throw new InvalidOperationException(
                "Level 100 lifecycle diverged from its Thing/Actor base state.");
        }
        actor.Lifecycle = Level100ActorLifecycle.StartedDying;
        EnqueueFact(Level100ActorFactKind.StartedDying, actorId, null, 0);
        return true;
    }

    public bool ReportDied(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle == Level100ActorLifecycle.Destroyed)
        {
            return false;
        }

        actor.Lifecycle = Level100ActorLifecycle.Destroyed;
        actor.BaseState.DeclareShutdown();
        actor.Active = false;
        actor.IsObjective = false;
        EnqueueFact(Level100ActorFactKind.Died, actorId, null, 0);
        return true;
    }

    internal bool ReportPlaneStartedDying(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.BaseState.Snapshot.RetailPlane is null)
            throw new InvalidOperationException("Plane death requires an admitted raw Plane.");
        if (actor.Lifecycle != Level100ActorLifecycle.Alive) return false;
        // AirUnit 0x00403690 calls CUnit 0x004fd140, whose override marks
        // TF_DYING without the base CThing's immediate shutdown declaration.
        if (!actor.BaseState.MarkUnitDying())
            throw new InvalidOperationException("Plane dying state diverged.");
        actor.Lifecycle = Level100ActorLifecycle.StartedDying;
        EnqueueFact(Level100ActorFactKind.StartedDying, actorId, null, 0);
        return true;
    }

    /// <summary>
    /// A unit's SHUTDOWN (event 2000) without a death, as a dropship's
    /// run-out posts it (slot 116, <c>0x004fe5f0</c>):
    /// <c>CComplexThing::HandleEvent</c> runs the script's <c>shutdown()</c>
    /// (<c>IScript</c> slot 2, <c>0x00533810</c>, built-in 7) and then the
    /// thing's own shutdown (<c>references/Onslaught/thing.cpp:675-684</c>).
    /// <c>CUnit</c>'s (<c>0x004f95d0</c>) takes it off its side's count
    /// (<c>0x004f97b6-0x004f97ef</c>), and the thing leaves the world.
    /// </summary>
    internal bool ReportShutdown(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle != Level100ActorLifecycle.Alive) return false;
        actor.Lifecycle = Level100ActorLifecycle.Destroyed;
        actor.Active = false;
        actor.IsObjective = false;
        EnqueueFact(Level100ActorFactKind.ShutDown, actorId, null, 0);
        return true;
    }

    internal bool ReportGroundUnitDied(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle != Level100ActorLifecycle.Alive) return false;
        if (!actor.BaseState.MarkUnitDying())
            throw new InvalidOperationException("Ground-unit dying state diverged.");
        // 0x004fd140 -> 0x004fd040: StartedDying, then Died and script
        // deletion, then the delayed SHUTDOWN. Neither body sets bit 0.
        actor.Lifecycle = Level100ActorLifecycle.DiedAwaitingShutdown;
        EnqueueFact(Level100ActorFactKind.StartedDying, actorId, null, 0);
        EnqueueFact(Level100ActorFactKind.Died, actorId, null, 0);
        return true;
    }

    internal void ShutdownGroundUnit(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (actor.Lifecycle != Level100ActorLifecycle.DiedAwaitingShutdown)
            throw new InvalidOperationException("Ground-unit shutdown has no pending death.");
        // Event 2000 calls Shutdown/delete directly. Do not invent another
        // AddShutdownEvent or set TF_DECLARED_SHUTDOWN on this path.
        actor.Lifecycle = Level100ActorLifecycle.Destroyed;
        actor.Active = false;
        actor.IsObjective = false;
    }

    public bool BeginTriggerDispatch(
        Level100ActorId actorId,
        Level100MissionJetModeState entryJetModeState)
    {
        Actor actor = RequireMutable(actorId);
        if (!actor.Trigger.HasValue || actor.TriggerEventDispatched)
        {
            return false;
        }

        actor.TriggerEntryJetModeState = entryJetModeState;
        EnqueueFact(Level100ActorFactKind.TriggerDispatchReady, actor.ActorId, null, 0);
        return true;
    }

    public void MarkTriggerEventDispatched(Level100ActorId actorId)
    {
        Actor actor = RequireMutable(actorId);
        if (!actor.Trigger.HasValue || actor.TriggerEventDispatched)
        {
            throw new InvalidOperationException("Trigger dispatch is not ready.");
        }

        actor.TriggerEntered = true;
        actor.TriggerEntryJetModeState ??= Level100MissionJetModeState.NotInJetMode;
        actor.TriggerEventDispatched = true;
        actor.Active = false;
        actor.IsObjective = false;
    }

    public IReadOnlyList<Level100ActorFactSnapshot> DrainFacts()
    {
        Level100ActorFactSnapshot[] result = _pendingFacts
            .OrderBy(item => item.Sequence)
            .ToArray();
        _pendingFacts.Clear();
        return Array.AsReadOnly(result);
    }

    private int AllocateMissionOrdinal(Level100SpawnDefinition definition)
    {
        if (definition.TargetGroup == Level100MissionTargetGroup.None)
        {
            return 0;
        }

        int ordinal = definition.FixedTargetOrdinal > 0
            ? definition.FixedTargetOrdinal
            : _actors.Values.Count(actor => actor.TargetGroup == definition.TargetGroup) + 1;
        if (ordinal > definition.MaximumGroupActors ||
            _actors.Values.Any(actor =>
                actor.TargetGroup == definition.TargetGroup &&
                actor.TargetOrdinal == ordinal))
        {
            throw new InvalidOperationException(
                $"Released Level 100 spawned an invalid {definition.TargetGroup} actor ordinal {ordinal}.");
        }

        return ordinal;
    }

    private Actor Require(Level100ActorId actorId)
    {
        if (actorId.Value <= 0 || !_actors.TryGetValue(actorId.Value, out Actor? actor))
        {
            throw new KeyNotFoundException($"Level 100 actor {actorId.Value} does not exist.");
        }

        return actor;
    }

    private static bool HasFinitePose(Level100ActorPoseSnapshot pose) =>
        new[]
        {
            pose.BasisFloatBits.Row0X, pose.BasisFloatBits.Row0Y, pose.BasisFloatBits.Row0Z,
            pose.BasisFloatBits.Row1X, pose.BasisFloatBits.Row1Y, pose.BasisFloatBits.Row1Z,
            pose.BasisFloatBits.Row2X, pose.BasisFloatBits.Row2Y, pose.BasisFloatBits.Row2Z,
        }.All(component => float.IsFinite(BitConverter.Int32BitsToSingle(component)));

    private Level100ActorId AllocateId() => new(_nextActorId++);

    private void EnqueueFact(
        Level100ActorFactKind kind,
        Level100ActorId actorId,
        Level100ActorId? otherActorId,
        uint otherThingTypeMask) => _pendingFacts.Add(
            new Level100ActorFactSnapshot(
                _nextFactSequence++, kind, actorId, otherActorId, otherThingTypeMask));

    /// <summary>
    /// The released <c>CThing::Init</c> support clamp, applied to every actor
    /// rather than to one motion class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read from the pristine specimen
    /// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
    /// <c>74154BFAE14DDC8ECB87A0766F5BC381C7B7F1AB334ED7A753040EDA1E1E7750</c>.
    /// <c>CThing::Init</c> (<c>0x004F34A0</c>) copies the authored position
    /// into <c>this+0x1c..+0x28</c> and then does exactly two things to the
    /// vertical and nothing else:
    /// </para>
    /// <list type="number">
    ///   <item>it samples the one global height field
    ///   (<c>MOV ECX, 0x006FADC8</c> / <c>CALL 0x0047EB80</c>), compares the
    ///   sample against the authored Z-down position
    ///   (<c>FCOM [ESI+0x24]</c>) and writes the sample in when the authored
    ///   value is below the ground (<c>FSTP [ESP+0x14]</c> at
    ///   <c>0x004F3529</c>, then <c>CALL [EAX+0x50]</c>) — guarded by
    ///   <c>vtable[+0xB0]() != 0</c>;</item>
    ///   <item>it loads the water level (<c>FLD [0x006FBDFC]</c> at
    ///   <c>0x004F3549</c>), compares (<c>FCOM [ESI+0x24]</c>) and writes it in
    ///   the same way (<c>FSTP [ESI+0x24]</c>) — guarded by
    ///   <c>vtable[+0xC4]() == 0</c>.</item>
    /// </list>
    /// <para>
    /// The authored value is ABSOLUTE. It is only ever pulled toward a surface
    /// — never offset, never terrain-relative, and there is no per-class
    /// additive term at <c>Init</c>. Z is down-positive in that space, so both
    /// <c>min</c>s are <c>Math.Max</c> here.
    /// </para>
    /// <para>
    /// <b>Why the clamps are unconditional for Level 100.</b> Measured from
    /// vtable slots 44/49 across all 24 concrete classes: 19 take both clamps
    /// unconditionally; <c>CDropship</c>, <c>CSimpleBuilding</c> and
    /// <c>CBoat</c> gate on <c>+0x2C</c> bit 2; <c>CSubmarine</c> takes
    /// neither. That bit is never set anywhere in the <c>play-level100</c>
    /// trace, and Level 100 instantiates no <c>CSubmarine</c>, so every actor
    /// in this level takes both clamps and no runtime flag is modelled. The
    /// <c>CDropship</c> is the check on that: it takes both and NEITHER MOVES
    /// IT — authored equals seated at retail −15.0 for both ambient aircraft —
    /// while a positive control in the same trace shows the terrain clamp
    /// firing on another object (−15.0 -> −18.10016), so the instrument can see
    /// one fire.
    /// </para>
    /// <para>
    /// <b>Ground support.</b> A <c>GroundVehicle</c> keeps
    /// <c>ground + CoreGroundOriginOffsetMillimeters</c> as its terrain
    /// support, which is the same expression
    /// <see cref="Level100ActorMechanics"/> applies on every base tick once
    /// the actor starts following a waypoint, so it does not jump the moment
    /// it is first commanded to move. During movement retail does have the
    /// 0.1-unit support term: vtable 0x005e297c slot 48 -> 0x0050e960.
    /// Applying it at initial seating remains a separate Core placement
    /// convention; it is deliberately NOT applied to the
    /// water support or to any other class — the renderer's own seat at
    /// <c>Level100StaticWorldAsset.Load</c> spells the unoffset
    /// <c>max(authored, terrain, water)</c>, and Core and the renderer must
    /// agree about where an object is.
    /// </para>
    /// <para>
    /// Level 100 authors every Target Tank and Target Truck at retail Z = 0
    /// while the firing-range ground is at Core Y = 600, and every retail-Z-0
    /// static at a Core Y of −10,000; without this clamp all of them are buried
    /// and their contact meshes are unreachable.
    /// </para>
    /// </remarks>
    private Level100ActorPoseSnapshot SeatOnGround(
        string? definitionName,
        Level100ActorPoseSnapshot pose)
    {
        if (!_initializeSupport)
        {
            return pose;
        }
        Level100ActorMotionDefinition? motion =
            _definitions.FindMotionDefinition(definitionName);
        int groundOriginOffset =
            motion?.MotionClass == Level100ActorMotionClass.GroundVehicle
                ? motion.CoreGroundOriginOffsetMillimeters!.Value
                : 0;
        int seated = Math.Max(
            checked(
                _terrain.SampleGroundElevationMillimeters(
                    new SimVector2(
                        pose.PositionMillimeters.X,
                        pose.PositionMillimeters.Z)) +
                groundOriginOffset),
            _terrain.WaterElevationMillimeters);
        return pose.PositionMillimeters.Y >= seated
            ? pose
            : pose with
            {
                PositionMillimeters = new SimVector3(
                    pose.PositionMillimeters.X,
                    seated,
                    pose.PositionMillimeters.Z),
            };
    }

    /// <summary>
    /// Contact-backed non-static definitions use their catalog maximum life,
    /// including legacy Target Drone spawn definitions that still carry zero.
    /// Air Trainer has no such fallback; the materializer now supplies its
    /// profile life directly. This does not establish generic Unit
    /// initialization or vulnerability.
    /// </summary>
    private static int ReleasedInitialHealth(
        string definitionName,
        int manifestInitialHealth)
    {
        if (!Level100ContactCatalog.Instance.TryGetDefinition(
                definitionName,
                out Level100ContactDefinition? definition) ||
            definition is null ||
            definition.Kind == Level100DefinitionKind.Static)
        {
            return manifestInitialHealth;
        }
        return checked((int)MathF.Round(
            definition.MaximumLife * 1_000f,
            MidpointRounding.AwayFromZero));
    }

    private Actor CreateActor(
        Level100ActorId actorId,
        Level100ActorDefinition definition,
        Level100ActorPoseSnapshot pose) =>
        new()
        {
            ActorId = actorId,
            DefinitionIdentity = definition.DefinitionIdentity,
            Name = definition.Name,
            DefinitionName = definition.DefinitionName,
            ScriptName = definition.ScriptName,
            MeshBinding = definition.MeshBinding,
            IsStatic = definition.IsStatic,
            Active = definition.Active,
            Lifecycle = Level100ActorLifecycle.Alive,
            Health = definition.InitialHealth,
            BaseState = IsAdmittedAirUnit(definition.DefinitionName)
                ? CreateAuthoredAirUnitState(definition)
                : CreateBaseState(pose, definition.ThingTypeMask),
            TargetGroup = definition.TargetGroup,
            TargetOrdinal = definition.TargetOrdinal,
            Trigger = definition.Trigger,
        };

    private bool IsAdmittedPlane(string? definitionName) =>
        _initializeSupport &&
        (_definitions.WorldNumber == 100 && definitionName is "Air Trainer" or "Target Drone" ||
         _definitions.WorldNumber == 110 && definitionName is "Muspell Fighter" or "Muspell Light Fighter") &&
        _definitions.FindMotionDefinition(definitionName)?.MotionClass == Level100ActorMotionClass.Plane;

    /// <summary>
    /// Level 100's U-17 (behaviour 12, <c>CDropship</c>), which keeps raw air
    /// state from construction like the planes. World 110's landing craft
    /// wait for the landing states: one lands at once.
    /// </summary>
    private bool IsAdmittedDropship(string? definitionName) =>
        _initializeSupport &&
        _definitions.WorldNumber == 100 && definitionName is "U-17 Highside Transporter" &&
        _definitions.FindMotionDefinition(definitionName)?.MotionClass == Level100ActorMotionClass.Dropship;

    private bool IsAdmittedAirUnit(string? definitionName) =>
        IsAdmittedPlane(definitionName) || IsAdmittedDropship(definitionName);

    /// <summary>
    /// A dropship's Euler rates: <c>CAirUnit::Init</c> sets all three to the
    /// air turn rate, then <c>CDropship::Init</c> sets pitch and roll to a
    /// quarter of it (0.25 at <c>0x005d858c</c>, <c>0x00446dc2-0x00446e0c</c>).
    /// A plane keeps all three.
    /// </summary>
    private Level100FloatVector3Bits AirUnitRates(string? definitionName)
    {
        int rate = PlaneAirTurnRateFloatBits(definitionName);
        if (!IsAdmittedDropship(definitionName)) return new(rate, rate, rate);
        int quarter = BitConverter.SingleToInt32Bits(
            (float)RetailFloat24.Multiply(BitConverter.Int32BitsToSingle(rate), 0.25));
        return new(rate, quarter, quarter);
    }

    /// <summary>The world's heightfield, for the air units' ground and floor samples.</summary>
    internal Level100Terrain Terrain => _terrain.Heightfield;

    /// <summary>
    /// An air unit's air velocity (unit field id 2, <c>+0xb4</c>), from its
    /// motion row.
    /// </summary>
    internal int PlaneAirVelocityFloatBits(string? definitionName) =>
        _definitions.FindMotionDefinition(definitionName)?.AirVelocityFloatBits ??
        throw new NotSupportedException($"No air velocity for '{definitionName}'.");

    /// <summary>An air unit's air turn rate (unit field id 6, <c>+0xb8</c>).</summary>
    private int PlaneAirTurnRateFloatBits(string? definitionName) =>
        _definitions.FindMotionDefinition(definitionName)?.AirTurnRateFloatBits ??
        throw new NotSupportedException($"No air turn rate for '{definitionName}'.");

    private ThingActorBaseState CreateAuthoredAirUnitState(Level100ActorDefinition definition)
    {
        if (definition.InitialPose.LinearVelocityMillimetersPerTick != SimVector3.Zero ||
            definition.InitialPose.AngularVelocityMicroRadiansPerTick != SimVector3.Zero)
            throw new NotSupportedException("Selected air-unit construction requires the admitted zero initial velocity.");
        Level100AuthoredTransform input = definition.AuthoredTransform;
        return CreatePlaneState(input.RetailPositionFloatBits,
            input.RetailEulerFloatBits, definition.ThingTypeMask, 0,
            AirUnitRates(definition.DefinitionName));
    }

    private ThingActorBaseState CreateSpawnedPlaneState(Actor owner,
        Level100SpawnDefinition definition, int eventTimeFloatBits)
    {
        RetailUnitAttachmentPose parent = GetAirfieldSpawnOwnerPose(owner, definition);
        Level100SpawnerTransform local = definition.AuthoredEmitterTransform;
        // 4b4ef2..4b50bb supplies the PC24 owner/cache product. This admits
        // the supplied emitter as the selected cached frame; it does not
        // implement animation/frame selection or a moving spawn owner.
        RetailUnitAttachmentPose emitter = RetailMeshPartPose.ApplyOwner(parent,
            new(local.LocalPositionFloatBits, local.LocalBasisFloatBits));
        Level100FloatVector3Bits position = emitter.PositionFloatBits;
        if (BitConverter.Int32BitsToSingle(position.X) == 0 &&
            BitConverter.Int32BitsToSingle(position.Y) == 0 &&
            BitConverter.Int32BitsToSingle(position.Z) == 0)
            throw new NotSupportedException("The zero-emitter owner fallback is outside the selected Airfield route.");
        Level100FloatVector3Bits euler = RetailPlaneMotion.EulerFromSpawnerBasis(emitter.BasisFloatBits);
        return CreatePlaneState(position, euler, definition.ThingTypeMask, eventTimeFloatBits,
            AirUnitRates(definition.DefinitionName));
    }

    /// <summary>
    /// Resolve the selected constant Airfield exit model pose by its exact
    /// CEMT selector and apply the seated owner. Null means no such selector;
    /// absent recovered input is rejected. The controller's terrain clamp,
    /// arrival test and event/Ready delivery remain separate operations.
    /// </summary>
    public RetailUnitAttachmentPose? GetPlaneSpawnerExitPoint(Level100ActorId actorId, int selector)
    {
        Actor actor = Require(actorId);
        if (actor.SpawnOwnerId is not { } ownerId || !IsAdmittedPlane(actor.DefinitionName))
            throw new NotSupportedException("Exit-point lookup requires an admitted spawned Plane.");
        Level100SpawnDefinition definition = _definitions.GetSpawnDefinition(actor.DefinitionIdentity);
        IReadOnlyList<Level100SpawnerExitPoint> points = definition.SpawnerExitWaypoints ??
            throw new NotSupportedException("This spawn definition has no recovered exit input.");
        RetailUnitAttachmentPose parent = GetAirfieldSpawnOwnerPose(Require(ownerId), definition);
        Level100SpawnerExitPoint? point = points.FirstOrDefault(item => item.Selector == selector);
        return point is null ? null : RetailMeshPartPose.ApplyOwner(parent,
            new(point.ModelTransform.LocalPositionFloatBits, point.ModelTransform.LocalBasisFloatBits));
    }

    private RetailUnitAttachmentPose GetAirfieldSpawnOwnerPose(Actor owner, Level100SpawnDefinition definition)
    {
        // The selected Airfield remains an immutable seated owner. Do not
        // reverse its rounded public pose into a purported retail transform.
        Level100ActorDefinition input = _definitions.GetActorDefinition(owner.DefinitionIdentity);
        if (owner.SpawnOwnerId is not null || !owner.IsStatic ||
            owner.DefinitionName != "Forseti Light Fighter Airfield" ||
            definition.SpawnerName is not ("SpawnerA" or "SpawnerB") ||
            ToLevel100Pose(owner.BaseState.Snapshot) != SeatOnGround(input.DefinitionName, input.InitialPose))
            throw new NotSupportedException("Plane spawning requires the unchanged authored Airfield pose.");
        return new RetailUnitAttachmentPose(
            SeatRetailPosition(input.AuthoredTransform.RetailPositionFloatBits),
            RetailUnitEuler.BuildBasis(input.AuthoredTransform.RetailEulerFloatBits));
    }

    /// <summary>
    /// <c>CThing::Init</c>'s height clamps: a thing below the heightfield sample
    /// (<c>0x0047eb80</c>) is raised to it, then one below the water level to
    /// that (<c>0x004f34ea-0x004f3559</c>). Waypoints take the same two
    /// (<c>reverse-engineering/game-mechanics/waypoint-paths.md</c>, "Loading").
    /// </summary>
    internal Level100FloatVector3Bits SeatRetailPosition(Level100FloatVector3Bits position)
    {
        float height = RetailWorldTerrain.SampleRetailHeight(_terrain.Heightfield, position);
        float z = BitConverter.Int32BitsToSingle(position.Z);
        // Retail Z points down. Strict comparisons retain the authored zero
        // word when neither clamp acts, unlike unconditional min/negation.
        if (z > height) position = position with { Z = BitConverter.SingleToInt32Bits(height) };
        if (BitConverter.Int32BitsToSingle(position.Z) > _terrain.Heightfield.WaterLevel)
            position = position with { Z = BitConverter.SingleToInt32Bits(_terrain.Heightfield.WaterLevel) };
        return position;
    }

    private ThingActorBaseState CreatePlaneState(Level100FloatVector3Bits position,
        Level100FloatVector3Bits euler, uint specificTypeMask, int eventTimeFloatBits, Level100FloatVector3Bits rates)
    {
        // Pristine 74154bfa…7750: Actor [401255,40131a) and ComplexThing
        // [4f4008,4f40cd) use the same trig/arithmetic/stores as Unit's
        // [4fa724,4fa7f1) suffix. Preserve its signed zeros rather than the
        // materializer's simpler yaw projection. Managed trig remains bounded.
        var pose = new RetailActorPoseSnapshot(position, RetailUnitEuler.BuildBasis(euler));
        var state = new ThingActorBaseState(new(SimVector3.Zero, pose.BasisFloatBits),
            SimVector3.Zero, SimVector3.Zero, specificTypeMask);
        state.BeginRetailPlane(pose, RetailPlaneMotion.CreateInitial(pose, euler, rates),
            eventTimeFloatBits, specificTypeMask);
        // Unit seating happens after Actor Init: the ground arm teleports
        // both positions; the later water clamp changes current position only.
        float ground = RetailWorldTerrain.SampleRetailHeight(_terrain.Heightfield, position);
        if (BitConverter.Int32BitsToSingle(position.Z) > ground)
        {
            position = position with { Z = BitConverter.SingleToInt32Bits(ground) };
            state.TeleportRetailPosition(position);
        }
        if (BitConverter.Int32BitsToSingle(position.Z) > _terrain.Heightfield.WaterLevel)
            state.SetRetailPosition(position with { Z = BitConverter.SingleToInt32Bits(_terrain.Heightfield.WaterLevel) });
        return state;
    }

    private void ValidateRestoredActor(
        Actor actor,
        Level100ActorSnapshot source,
        Level100ActorRegistrySnapshot snapshot)
    {
        if (IsAdmittedAirUnit(source.DefinitionName) && !actor.BaseState.HasRetailPlaneMotion)
            throw new ArgumentException("Selected air-unit snapshots require their creation-owned raw state.", nameof(snapshot));
        if (actor.BaseState.HasRetailPlaneMotion && !IsAdmittedAirUnit(source.DefinitionName))
            throw new NotSupportedException("Raw air motion is admitted only for each world's selected aircraft definitions.");
        if (source.Pose is null ||
            !HasFinitePose(source.Pose) ||
            actor.Health < 0 ||
            !Enum.IsDefined(actor.Lifecycle) ||
            source.Pose.PositionMillimeters !=
                actor.BaseState.Snapshot.CurrentPose.PositionMillimeters ||
            source.Pose.BasisFloatBits !=
                actor.BaseState.Snapshot.CurrentPose.BasisFloatBits ||
            source.Pose.LinearVelocityMillimetersPerTick !=
                actor.BaseState.Snapshot.Velocity ||
            source.Pose.AngularVelocityMicroRadiansPerTick !=
                actor.BaseState.Snapshot.AngularVelocity ||
            actor.BaseState.Snapshot.ThingTypeMask !=
                (source.ThingTypeMask | ThingActorTypeMasks.ActorLineage) ||
            (actor.Lifecycle == Level100ActorLifecycle.Alive &&
                (actor.BaseState.Snapshot.IsDying ||
                 actor.BaseState.Snapshot.IsShuttingDown)) ||
            (actor.Lifecycle == Level100ActorLifecycle.StartedDying &&
                !actor.BaseState.Snapshot.IsDying) ||
            (actor.Lifecycle == Level100ActorLifecycle.DiedAwaitingShutdown &&
                (!actor.BaseState.Snapshot.IsDying ||
                 actor.BaseState.Snapshot.IsShuttingDown || actor.Health != 0 ||
                 actor.DefinitionName is not ("Target Tank" or "Target Truck"))) ||
            (actor.Lifecycle == Level100ActorLifecycle.Destroyed &&
                !actor.BaseState.Snapshot.IsShuttingDown &&
                !actor.BaseState.Snapshot.IsDying) ||
            (actor.Lifecycle == Level100ActorLifecycle.Destroyed &&
                (actor.Active || actor.IsObjective)) ||
            !Enum.IsDefined(actor.TargetGroup) ||
            (actor.ScriptName is not null &&
                !Level100MissionProgram.ProgramNamesFor(_definitions.WorldNumber).Contains(
                    actor.ScriptName,
                    StringComparer.Ordinal)) ||
            (actor.Trigger.HasValue && !Enum.IsDefined(actor.Trigger.Value)) ||
            (actor.TriggerEntryJetModeState.HasValue &&
                !Enum.IsDefined(actor.TriggerEntryJetModeState.Value)) ||
            (!actor.Trigger.HasValue &&
                (actor.TriggerEntered ||
                 actor.TriggerEntryJetModeState.HasValue ||
                 actor.TriggerEventDispatched)) ||
            (actor.Trigger.HasValue &&
                ((actor.TriggerEventDispatched && !actor.TriggerEntered) ||
                 (actor.TriggerEntered && !actor.TriggerEntryJetModeState.HasValue))))
        {
            throw new ArgumentException(
                "Actor registry snapshot contains invalid mutable actor state.",
                nameof(snapshot));
        }

        if (actor.SpawnOwnerId.HasValue)
        {
            Level100SpawnDefinition definition = _definitions.GetSpawnDefinition(actor.DefinitionIdentity);
            Actor owner = _actors.GetValueOrDefault(actor.SpawnOwnerId.Value.Value) ??
                throw new ArgumentException(
                    "Actor registry snapshot has a missing spawn owner.",
                    nameof(snapshot));
            if (!StringComparer.Ordinal.Equals(
                    owner.DefinitionIdentity,
                    definition.OwnerDefinitionIdentity) ||
                !StringComparer.Ordinal.Equals(actor.Name, $"{definition.DefinitionName} #{actor.ActorId.Value}") ||
                !StringComparer.Ordinal.Equals(actor.DefinitionName, definition.DefinitionName) ||
                !StringComparer.Ordinal.Equals(actor.ScriptName, definition.ScriptName) ||
                !StringComparer.Ordinal.Equals(actor.SpawnerName, definition.SpawnerName) ||
                !StringComparer.Ordinal.Equals(actor.MeshBinding, definition.MeshBinding) ||
                source.ThingTypeMask != definition.ThingTypeMask ||
                actor.IsStatic ||
                actor.TargetGroup != definition.TargetGroup ||
                (definition.TargetGroup == Level100MissionTargetGroup.None &&
                    actor.TargetOrdinal != 0) ||
                (definition.TargetGroup != Level100MissionTargetGroup.None &&
                    (actor.TargetOrdinal <= 0 ||
                     actor.TargetOrdinal > definition.MaximumGroupActors ||
                     (definition.FixedTargetOrdinal > 0 &&
                        actor.TargetOrdinal != definition.FixedTargetOrdinal))) ||
                actor.Trigger.HasValue)
            {
                throw new ArgumentException(
                    "Actor registry snapshot changed immutable spawn identity.",
                    nameof(snapshot));
            }
            return;
        }

        Level100ActorDefinition actorDefinition =
            _definitions.GetActorDefinition(actor.DefinitionIdentity);
        if (actor.SpawnerName is not null ||
            !StringComparer.Ordinal.Equals(actor.Name, actorDefinition.Name) ||
            !StringComparer.Ordinal.Equals(actor.DefinitionName, actorDefinition.DefinitionName) ||
            !StringComparer.Ordinal.Equals(actor.MeshBinding, actorDefinition.MeshBinding) ||
            source.ThingTypeMask != actorDefinition.ThingTypeMask ||
            actor.IsStatic != actorDefinition.IsStatic ||
            actor.TargetGroup != actorDefinition.TargetGroup ||
            actor.TargetOrdinal != actorDefinition.TargetOrdinal ||
            actor.Trigger != actorDefinition.Trigger)
        {
            throw new ArgumentException(
                "Actor registry snapshot changed immutable authored identity.",
                nameof(snapshot));
        }
    }

    private static Actor RestoreActor(
        Level100ActorSnapshot actor,
        ThingActorBaseStateSnapshot baseState) => new()
    {
        ActorId = actor.ActorId,
        DefinitionIdentity = actor.DefinitionIdentity,
        Name = actor.Name,
        DefinitionName = actor.DefinitionName,
        ScriptName = actor.ScriptName,
        MeshBinding = actor.MeshBinding,
        SpawnOwnerId = actor.SpawnOwnerId,
        SpawnerName = actor.SpawnerName,
        IsStatic = actor.IsStatic,
        Active = actor.Active,
        IsObjective = actor.IsObjective,
        FlagWord = actor.IsObjective ? RetailSetObjective.MarkedBit : 0,
        Lifecycle = actor.Lifecycle,
        Health = actor.Health,
        BaseState = new ThingActorBaseState(baseState),
        TargetGroup = actor.TargetGroup,
        TargetOrdinal = actor.TargetOrdinal,
        Trigger = actor.Trigger,
        TriggerEntered = actor.TriggerEntered,
        TriggerEntryJetModeState = actor.TriggerEntryJetModeState,
        TriggerEventDispatched = actor.TriggerEventDispatched,
    };

    private static Level100ActorSnapshot SnapshotActor(Actor actor) => new(
        actor.ActorId,
        actor.DefinitionIdentity,
        actor.Name,
        actor.DefinitionName,
        actor.ScriptName,
        actor.MeshBinding,
        SpecificThingTypeMask(actor),
        actor.SpawnOwnerId,
        actor.SpawnerName,
        actor.IsStatic,
        actor.Active,
        actor.IsObjective,
        actor.Lifecycle,
        actor.Health,
        ToLevel100Pose(actor.BaseState.Snapshot),
        actor.TargetGroup,
        actor.TargetOrdinal,
        actor.Trigger,
        actor.TriggerEntered,
        actor.TriggerEntryJetModeState,
        actor.TriggerEventDispatched);

    private static ThingActorBaseState CreateBaseState(
        Level100ActorPoseSnapshot pose,
        uint specificTypeMask) => new(
            ToBasePose(pose),
            pose.LinearVelocityMillimetersPerTick,
            pose.AngularVelocityMicroRadiansPerTick,
            specificTypeMask);

    private static ThingActorPoseSnapshot ToBasePose(
        Level100ActorPoseSnapshot pose) => new(
            pose.PositionMillimeters,
            pose.BasisFloatBits);

    private static Level100ActorPoseSnapshot ToLevel100Pose(
        ThingActorBaseStateSnapshot state) => new(
            state.CurrentPose.PositionMillimeters,
            state.CurrentPose.BasisFloatBits,
            state.Velocity,
            state.AngularVelocity);

    private static uint SpecificThingTypeMask(Actor actor) =>
        actor.BaseState.Snapshot.ThingTypeMask &
        Level100ReleasedThingTypeMasks.ProvenBits;
}
