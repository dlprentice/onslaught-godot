// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Client;

/// <summary>
/// Bounded decoder for the actor-definition projection inside the exact
/// locally materialized Level 100 static-world manifest. Filesystem ownership
/// remains with the calling client/scenario adapter.
/// </summary>
public static class Level100ActorDefinitionManifest
{
    // Schema v14 retains authored waypoint coordinates and Core's up-positive
    // position datum. The signed retail-to-Core basis correction on 2026-09-08
    // changes only 237 +0 words to -0 across 44 actors and 10 spawns; raw retail
    // transforms, positions, paths and source identities are unchanged. The
    // subsequent Trainer-life correction sets the two initialHealth fields
    // to 3000 from their physics profile; no other manifest field changes.
    // September 12 adds only the four Airfield spawn rows' selected exit
    // waypoints: CEMT selector1 plus constant CPOS/inherited CORI model words.
    // Aircraft profiles additionally carry ordered field7 weapon uses and
    // GunA/B selector1 model poses; live muzzle/cache state remains separate.
    // Definition identity hashes those exact words, so replay pins move too.
    // Schema v15 adds each air unit's flight scalars from its unit record
    // (CUnitAirVelocity, CUnitAirTurnRate, CUnitBig, the MinAltitude default
    // and a Big unit's render radius) and the level's four CSafeSides.
    public const string ExpectedManifestSha256 =
        "350AC9C4FDCCCECBBEA9F6C0D3CB7099EE915DF8971123E6A030BBF574168561";

    private const string ExpectedSchema = "onslaught.level100-static-world.v15";
    private const string ExpectedSourceArchiveSha256 =
        "ED6350C0E214D00AB1BF6A7BD137FBA3E77D0AFE19A6DC4C0607F56AC037496A";
    private const string ExpectedPhysicsSourceSha256 =
        "E1FB3DEDBEB29B4B4151DA2C8CBBDC940B716B1A2321E1D6A9BA1542C74ADA14";
    private const int MaximumManifestBytes = 512_000;

    /// <summary>The materializer's <c>WORLD110_STATIC_WORLD_SHA256</c>.</summary>
    public const string ExpectedWorld110ManifestSha256 =
        "F5E411A1717C3D278E8E314C049BFF54C3081744C3A7E512A9F8857B9584FA58";
    private const string ExpectedWorld110Schema = "onslaught.world110-static-world.v2";
    private const string ExpectedWorld110ArchiveSha256 =
        "4E041C758B9D41BA18311B1FADEACB95FC31AF51320861480B97033BC24E3C2B";

    /// <summary>
    /// The authored WRES <c>allegiance</c> int32 of each of the 33 base-world
    /// objects, keyed by the same <c>wres:bswd:NNNN</c> identity the actor
    /// definitions carry. This is presentation input for the released scanner's
    /// colour partition; it is deliberately NOT folded into
    /// <see cref="Level100ActorDefinitionSet"/>, so no Core state and no state
    /// hash moves. The manifest is the same hash-pinned file
    /// <see cref="Decode"/> validates.
    ///
    /// <para>The 11 level-world actors (Player 1, the Firing Range group, the
    /// Transporter and the Air Trainer) have no allegiance in schema v13, so
    /// they are absent from this map and the caller falls back.</para>
    /// </summary>
    public static IReadOnlyDictionary<string, int> DecodeAuthoredAllegiance(
        ReadOnlySpan<byte> manifestBytes)
    {
        Manifest manifest = ReadValidatedManifest(manifestBytes);
        var allegiance = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (WorldObject worldObject in manifest.Objects)
        {
            allegiance[$"wres:bswd:{worldObject.Ordinal:D4}"] = worldObject.Allegiance;
        }

        if (allegiance.Count != manifest.VisibleObjectCount)
        {
            throw new InvalidDataException(
                "The Level 100 base-world object ordinals are not unique.");
        }

        return allegiance;
    }

    public static Level100ActorDefinitionSet Decode(ReadOnlySpan<byte> manifestBytes)
    {
        Manifest manifest = ReadValidatedManifest(manifestBytes);
        return Decode(manifest);
    }

    private static Manifest ReadValidatedManifest(ReadOnlySpan<byte> manifestBytes)
    {
        if (manifestBytes.Length is < 1 or > MaximumManifestBytes ||
            !StringComparer.Ordinal.Equals(
                Convert.ToHexString(SHA256.HashData(manifestBytes)),
                ExpectedManifestSha256))
        {
            throw new InvalidDataException(
                "The locally materialized Level 100 actor-definition manifest is missing or changed.");
        }

        Manifest manifest = JsonSerializer.Deserialize<Manifest>(
                manifestBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The Level 100 actor-definition manifest is empty.");
        if (!StringComparer.Ordinal.Equals(manifest.Schema, ExpectedSchema) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                manifest.SourceArchiveSha256,
                ExpectedSourceArchiveSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                manifest.PhysicsSourceSha256,
                ExpectedPhysicsSourceSha256) ||
            manifest.UnitRecordCount != 35 ||
            manifest.VisibleObjectCount != 33 ||
            manifest.ActorDefinitions.Length != 44 ||
            manifest.SpawnDefinitions.Length != 10 ||
            manifest.WaypointPaths.Length != 8 ||
            manifest.MotionDefinitions.Length != 5 ||
            manifest.SafeSides.Length != 4 ||
            manifest.ActorDefinitions.Count(definition =>
                definition.DefinitionIdentity.StartsWith("wres:bswd:", StringComparison.Ordinal)) != 33 ||
            manifest.PineInstanceCount != 1_481 ||
            manifest.Pines.Length != manifest.PineInstanceCount)
        {
            throw new InvalidDataException(
                "The Level 100 actor-definition identity or authored counts changed.");
        }

        return manifest;
    }

    private static Level100ActorDefinitionSet Decode(Manifest manifest)
    {
        Level100ActorDefinition[] actors = manifest.ActorDefinitions.Select(DecodeActor).ToArray();

        var spawns = new Level100SpawnDefinition[manifest.SpawnDefinitions.Length];
        for (int index = 0; index < spawns.Length; index++)
        {
            SpawnDefinition source = manifest.SpawnDefinitions[index];
            spawns[index] = new Level100SpawnDefinition(
                source.AuthoredOrder,
                source.DefinitionIdentity,
                source.OwnerDefinitionIdentity,
                source.DefinitionName,
                source.SpawnerName,
                source.ScriptName,
                EmptyToNull(source.MeshBinding),
                source.ThingTypeMask,
                source.Active,
                source.InitialHealth,
                DecodePose(source.InitialPose),
                DecodeEmitterTransform(source.AuthoredEmitterTransform),
                ParseEnum<Level100MissionTargetGroup>(source.TargetGroup, "target group"),
                source.FixedTargetOrdinal,
                source.MaximumGroupActors,
                source.SpawnerExitWaypoints?.Select(point => new Level100SpawnerExitPoint(
                    point.Selector, DecodeEmitterTransform(point.ModelTransform))).ToArray());
        }

        var waypointPaths =
            new Level100WaypointPathDefinition[manifest.WaypointPaths.Length];
        for (int pathIndex = 0; pathIndex < waypointPaths.Length; pathIndex++)
        {
            WaypointPath source = manifest.WaypointPaths[pathIndex];
            if (source.TargetChainNodeIndices.Length != source.Points.Length)
            {
                throw new InvalidDataException(
                    $"Level 100 waypoint path '{source.Name}' has " +
                    $"{source.TargetChainNodeIndices.Length} chain entries for " +
                    $"{source.Points.Length} nodes.");
            }

            // The manifest keeps the file order, every row a CWaypoint, and
            // the target chain: each node targets the next chain entry, and the
            // last one the head when the chain closes (waypoint-paths.md).
            int[] chain = source.TargetChainNodeIndices;
            var targets = new Dictionary<int, int?>();
            for (int index = 0; index < chain.Length; index++)
            {
                if (!targets.TryAdd(
                    chain[index],
                    index + 1 < chain.Length ? chain[index + 1] : source.IsClosed ? chain[0] : null))
                {
                    throw new InvalidDataException(
                        $"Level 100 waypoint path '{source.Name}' repeats chain node {chain[index]}.");
                }
            }

            var points = new Level100WaypointPointDefinition[source.Points.Length];
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                WaypointPoint point = source.Points[pointIndex];
                if (!targets.TryGetValue(point.NodeIndex, out int? target))
                {
                    throw new InvalidDataException(
                        "A Level 100 waypoint point changed shape.");
                }
                points[pointIndex] = DecodeWaypointPoint(point, target);
            }
            waypointPaths[pathIndex] = Level100WaypointPathDefinition.FromFileOrder(source.Name, points);
        }

        Level100ActorMotionDefinition[] motionDefinitions =
            manifest.MotionDefinitions.Select(DecodeMotion).ToArray();

        // Each of the base world's 1,481 pines takes one gameplay draw when
        // LoadWorld initialises it (the RE lane's construction order).
        return new Level100ActorDefinitionSet(
            actors,
            spawns,
            waypointPaths,
            motionDefinitions,
            baseWorldPineCount: manifest.PineInstanceCount,
            safeSides: manifest.SafeSides.Select(DecodeSafeSide));
    }

    /// <summary>
    /// Decodes the materialized World 110 static world
    /// (<c>level110-static-world.json</c>, schema
    /// <c>onslaught.world110-static-world.v2</c>): the shared base world with
    /// each building's life, the level rows with their sides, the five squads,
    /// the four landing craft's turret children, the named paths as retail
    /// loads them and the base world's two CSafeSides. Filesystem ownership
    /// remains with the caller.
    /// </summary>
    public static Level100ActorDefinitionSet DecodeWorld110(ReadOnlySpan<byte> manifestBytes)
    {
        if (manifestBytes.Length is < 1 or > MaximumManifestBytes ||
            !StringComparer.Ordinal.Equals(
                Convert.ToHexString(SHA256.HashData(manifestBytes)),
                ExpectedWorld110ManifestSha256))
        {
            throw new InvalidDataException(
                "The locally materialized World 110 static world is missing or changed.");
        }

        Manifest manifest = JsonSerializer.Deserialize<Manifest>(
                manifestBytes,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The World 110 static world is empty.");
        if (!StringComparer.Ordinal.Equals(manifest.Schema, ExpectedWorld110Schema) ||
            manifest.WorldNumber != 110 ||
            !StringComparer.OrdinalIgnoreCase.Equals(manifest.SourceArchiveSha256, ExpectedWorld110ArchiveSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(manifest.PhysicsSourceSha256, ExpectedPhysicsSourceSha256) ||
            manifest.ActorDefinitions.Length != 77 ||
            manifest.ActorDefinitions.Count(definition =>
                definition.DefinitionIdentity.StartsWith("wres:bswd:", StringComparison.Ordinal)) != 33 ||
            manifest.Squads.Length != 5 ||
            manifest.Components.Length != 4 ||
            manifest.WaypointPaths.Length != 4 ||
            manifest.MotionDefinitions.Length != 6 ||
            manifest.SafeSides.Length != 2 ||
            manifest.PineInstanceCount != 1_481 ||
            manifest.Pines.Length != manifest.PineInstanceCount ||
            // The level world's pan length, settings word 4 (0x0050d2c5).
            manifest.Settings?.PanLengthFloatBits != BitConverter.SingleToInt32Bits(
                SimulationConstants.OpeningPanTicks(110) / (float)SimulationConstants.TicksPerSecond))
        {
            throw new InvalidDataException("The World 110 static world's identity or counts changed.");
        }

        var paths = new Level100WaypointPathDefinition[manifest.WaypointPaths.Length];
        for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
        {
            WaypointPath source = manifest.WaypointPaths[pathIndex];
            // The file lists only CWaypoint rows here; the dropped rows are
            // recorded for review. Each keeps its own target.
            paths[pathIndex] = Level100WaypointPathDefinition.FromFileOrder(
                source.Name,
                source.Points.Select(point => DecodeWaypointPoint(point, point.TargetNodeIndex)));
        }

        return new Level100ActorDefinitionSet(
            manifest.ActorDefinitions.Select(DecodeActor),
            [],
            paths,
            manifest.MotionDefinitions.Select(DecodeMotion),
            worldNumber: 110,
            baseWorldPineCount: manifest.PineInstanceCount,
            squads: manifest.Squads.Select(squad => new Level100SquadDefinition(
                squad.DefinitionIdentity,
                squad.Name,
                squad.DefinitionName,
                squad.MemberIdentities,
                EmptyToNull(squad.ScriptName),
                squad.Active,
                squad.Allegiance,
                squad.Mode,
                DecodeAuthoredTransform(squad.AuthoredTransform))),
            components: manifest.Components.Select(component => new Level100ComponentDefinition(
                component.ChildIdentity,
                component.ParentIdentity,
                component.DefinitionName)),
            safeSides: manifest.SafeSides.Select(DecodeSafeSide));
    }

    private static Level100SafeSideDefinition DecodeSafeSide(SafeSide source) =>
        new(
            source.World switch
            {
                "base" => true,
                "level" => false,
                _ => throw new InvalidDataException($"A safe side's world is invalid: {source.World}."),
            },
            source.Row,
            DecodeFloatVector(source.RetailPositionFloatBits, "safe-side position"),
            source.Allegiance);

    private static Level100ActorDefinition DecodeActor(ActorDefinition source) =>
        new(
            source.AuthoredOrder,
            source.DefinitionIdentity,
            source.Name,
            EmptyToNull(source.DefinitionName),
            EmptyToNull(source.ScriptName),
            EmptyToNull(source.MeshBinding),
            source.ThingTypeMask,
            source.IsStatic,
            source.Active,
            source.InitialHealth,
            DecodeAuthoredTransform(source.AuthoredTransform),
            DecodePose(source.InitialPose),
            ParseEnum<Level100MissionTargetGroup>(source.TargetGroup, "target group"),
            source.TargetOrdinal,
            source.Trigger is null
                ? null
                : ParseEnum<Level100MissionTrigger>(source.Trigger, "trigger"),
            source.Allegiance);

    private static Level100ActorMotionDefinition DecodeMotion(MotionDefinition source) =>
        new(
            source.AuthoredOrder,
            source.DefinitionName,
            ParseEnum<Level100ActorMotionClass>(source.MotionClass, "motion class"),
            source.BehaviorSerializedType,
            source.BehaviorInternalId,
            source.SteamClassVtableAddress,
            source.ArrivalRadiusMillimeters,
            source.MaximumSpeedFloatBits,
            source.MaximumTurnRadiansPerBaseTickFloatBits,
            source.FullGuideBaseTicks,
            source.CoreGroundOriginOffsetMillimeters,
            source.WeaponMounts?.Select(mount => new Level100ActorWeaponMountDefinition(
                mount.Use ?? throw new InvalidDataException("An aircraft weapon use is missing."),
                mount.Selector,
                new RetailUnitAttachmentPose(
                    DecodeFloatVector(mount.ModelTransform.LocalPositionFloatBits, "weapon model position"),
                    DecodeBasis(mount.ModelTransform.LocalBasisFloatBits, "weapon model basis"))))
                .ToArray(),
            source.AirVelocityFloatBits,
            source.AirTurnRateFloatBits,
            source.Big,
            source.MinimumAltitudeFloatBits,
            source.MeshRadiusFloatBits);

    private static Level100WaypointPointDefinition DecodeWaypointPoint(WaypointPoint point, int? target)
    {
        if (point.PositionMillimeters.Length != 3 || point.RetailComponentsFloatBits.Length != 4)
        {
            throw new InvalidDataException("A waypoint point changed shape.");
        }

        return new Level100WaypointPointDefinition(
            point.NodeIndex,
            new SimVector3(point.PositionMillimeters[0], point.PositionMillimeters[1], point.PositionMillimeters[2]),
            new Level100FloatVector4Bits(
                point.RetailComponentsFloatBits[0],
                point.RetailComponentsFloatBits[1],
                point.RetailComponentsFloatBits[2],
                point.RetailComponentsFloatBits[3]),
            target);
    }

    private static Level100ActorPoseSnapshot DecodePose(Pose source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Level100ActorPoseSnapshot(
            DecodeVector(source.PositionMillimeters, "position"),
            DecodeBasis(source.BasisFloatBits, "basis"),
            DecodeVector(source.LinearVelocityMillimetersPerTick, "linear velocity"),
            DecodeVector(source.AngularVelocityMicroRadiansPerTick, "angular velocity"));
    }

    private static Level100AuthoredTransform DecodeAuthoredTransform(AuthoredTransform source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Level100AuthoredTransform(
            DecodeFloatVector(source.RetailPositionFloatBits, "authored position"),
            DecodeFloatVector(source.RetailEulerFloatBits, "authored Euler"),
            DecodeBasis(source.RetailBasisFloatBits, "authored basis"));
    }

    private static Level100FloatVector3Bits DecodeFloatVector(int[] values, string role)
    {
        if (values.Length != 3)
        {
            throw new InvalidDataException($"A Level 100 actor {role} changed shape.");
        }

        return new Level100FloatVector3Bits(values[0], values[1], values[2]);
    }

    private static Level100FloatBasis3Bits DecodeBasis(int[] values, string role)
    {
        if (values.Length != 9)
        {
            throw new InvalidDataException($"A Level 100 actor {role} changed shape.");
        }

        return new Level100FloatBasis3Bits(
            values[0], values[1], values[2],
            values[3], values[4], values[5],
            values[6], values[7], values[8]);
    }

    private static Level100SpawnerTransform DecodeEmitterTransform(EmitterTransform source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.LocalPositionFloatBits.Length != 3 || source.LocalBasisFloatBits.Length != 9)
        {
            throw new InvalidDataException("A Level 100 authored emitter transform changed shape.");
        }

        return new Level100SpawnerTransform(
            new Level100FloatVector3Bits(
                source.LocalPositionFloatBits[0],
                source.LocalPositionFloatBits[1],
                source.LocalPositionFloatBits[2]),
            new Level100FloatBasis3Bits(
                source.LocalBasisFloatBits[0],
                source.LocalBasisFloatBits[1],
                source.LocalBasisFloatBits[2],
                source.LocalBasisFloatBits[3],
                source.LocalBasisFloatBits[4],
                source.LocalBasisFloatBits[5],
                source.LocalBasisFloatBits[6],
                source.LocalBasisFloatBits[7],
                source.LocalBasisFloatBits[8]));
    }

    private static SimVector3 DecodeVector(int[] values, string role)
    {
        if (values.Length != 3)
        {
            throw new InvalidDataException($"A Level 100 actor {role} changed shape.");
        }

        return new SimVector3(values[0], values[1], values[2]);
    }

    private static T ParseEnum<T>(string value, string role)
        where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T result) && Enum.IsDefined(result)
            ? result
            : throw new InvalidDataException($"A Level 100 actor {role} is invalid: {value}.");

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private sealed record Manifest
    {
        public string Schema { get; init; } = string.Empty;
        public string SourceArchiveSha256 { get; init; } = string.Empty;
        public string PhysicsSourceSha256 { get; init; } = string.Empty;
        public int UnitRecordCount { get; init; }
        public int VisibleObjectCount { get; init; }
        public ActorDefinition[] ActorDefinitions { get; init; } = [];
        public MotionDefinition[] MotionDefinitions { get; init; } = [];
        public SpawnDefinition[] SpawnDefinitions { get; init; } = [];
        public WaypointPath[] WaypointPaths { get; init; } = [];
        public WorldObject[] Objects { get; init; } = [];
        public int PineInstanceCount { get; init; }
        public float[][] Pines { get; init; } = [];
        public int WorldNumber { get; init; }
        public Squad[] Squads { get; init; } = [];
        public Component[] Components { get; init; } = [];
        public SafeSide[] SafeSides { get; init; } = [];
        public Settings? Settings { get; init; }
    }

    private sealed record SafeSide
    {
        public int Allegiance { get; init; }
        public int[] RetailPositionFloatBits { get; init; } = [];
        public int Row { get; init; }
        public string World { get; init; } = string.Empty;
    }

    private sealed record Squad
    {
        public bool Active { get; init; }
        public int Allegiance { get; init; }
        public AuthoredTransform AuthoredTransform { get; init; } = new();
        public string DefinitionIdentity { get; init; } = string.Empty;
        public string DefinitionName { get; init; } = string.Empty;
        public string[] MemberIdentities { get; init; } = [];
        public int Mode { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? ScriptName { get; init; }
    }

    private sealed record Component
    {
        public string ChildIdentity { get; init; } = string.Empty;
        public string DefinitionName { get; init; } = string.Empty;
        public string ParentIdentity { get; init; } = string.Empty;
    }

    private sealed record Settings
    {
        public int PanLengthFloatBits { get; init; }
        public int PreRunWordBits { get; init; }
    }

    private sealed record WorldObject
    {
        public int Ordinal { get; init; }
        public int Allegiance { get; init; }
    }

    private sealed record MotionDefinition
    {
        public int ArrivalRadiusMillimeters { get; init; }
        public int AuthoredOrder { get; init; }
        public int BehaviorInternalId { get; init; }
        public int BehaviorSerializedType { get; init; }
        public string DefinitionName { get; init; } = string.Empty;
        public int? FullGuideBaseTicks { get; init; }
        public int? CoreGroundOriginOffsetMillimeters { get; init; }
        public int? MaximumSpeedFloatBits { get; init; }
        public int? MaximumTurnRadiansPerBaseTickFloatBits { get; init; }
        public string MotionClass { get; init; } = string.Empty;
        public int SteamClassVtableAddress { get; init; }
        public WeaponMount[]? WeaponMounts { get; init; }
        public int? AirVelocityFloatBits { get; init; }
        public int? AirTurnRateFloatBits { get; init; }
        public bool? Big { get; init; }
        public int? MinimumAltitudeFloatBits { get; init; }
        public int? MeshRadiusFloatBits { get; init; }
    }

    private sealed record WeaponMount
    {
        public RetailUnitConstructionUse? Use { get; init; }
        public int Selector { get; init; }
        public EmitterTransform ModelTransform { get; init; } = new();
    }

    private sealed record ActorDefinition
    {
        public bool Active { get; init; }
        public AuthoredTransform AuthoredTransform { get; init; } = new();
        public int AuthoredOrder { get; init; }
        public string DefinitionIdentity { get; init; } = string.Empty;
        public string? DefinitionName { get; init; }
        public int InitialHealth { get; init; }
        public Pose InitialPose { get; init; } = new();
        public bool IsStatic { get; init; }
        public string? MeshBinding { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? ScriptName { get; init; }
        public uint ThingTypeMask { get; init; }
        public string TargetGroup { get; init; } = string.Empty;
        public int TargetOrdinal { get; init; }
        public string? Trigger { get; init; }
        public int Allegiance { get; init; }
    }

    private sealed record SpawnDefinition
    {
        public bool Active { get; init; }
        public EmitterTransform AuthoredEmitterTransform { get; init; } = new();
        public SpawnerExitPoint[]? SpawnerExitWaypoints { get; init; }
        public int AuthoredOrder { get; init; }
        public string DefinitionIdentity { get; init; } = string.Empty;
        public string DefinitionName { get; init; } = string.Empty;
        public int FixedTargetOrdinal { get; init; }
        public int InitialHealth { get; init; }
        public Pose InitialPose { get; init; } = new();
        public int MaximumGroupActors { get; init; }
        public string? MeshBinding { get; init; }
        public string OwnerDefinitionIdentity { get; init; } = string.Empty;
        public string ScriptName { get; init; } = string.Empty;
        public string SpawnerName { get; init; } = string.Empty;
        public string TargetGroup { get; init; } = string.Empty;
        public uint ThingTypeMask { get; init; }
    }

    private sealed record SpawnerExitPoint
    {
        public int Selector { get; init; }
        public EmitterTransform ModelTransform { get; init; } = new();
    }

    private sealed record WaypointPath
    {
        public string Name { get; init; } = string.Empty;
        public WaypointPoint[] Points { get; init; } = [];

        // The chain gives each node's target; see
        // Level100WaypointPathDefinition for how retail walks it.
        public int[] TargetChainNodeIndices { get; init; } = [];
        public bool IsClosed { get; init; }

        // World 110: the listed rows the loader drops (not CWaypoints).
        public int[] DroppedNodeIndices { get; init; } = [];
    }

    private sealed record WaypointPoint
    {
        public int NodeIndex { get; init; }
        public int[] PositionMillimeters { get; init; } = [];
        public int[] RetailComponentsFloatBits { get; init; } = [];
        public int? TargetNodeIndex { get; init; }
    }

    private sealed record Pose
    {
        public int[] AngularVelocityMicroRadiansPerTick { get; init; } = [];
        public int[] BasisFloatBits { get; init; } = [];
        public int[] LinearVelocityMillimetersPerTick { get; init; } = [];
        public int[] PositionMillimeters { get; init; } = [];
    }

    private sealed record AuthoredTransform
    {
        public int[] RetailBasisFloatBits { get; init; } = [];
        public int[] RetailEulerFloatBits { get; init; } = [];
        public int[] RetailPositionFloatBits { get; init; } = [];
    }

    private sealed record EmitterTransform
    {
        public int[] LocalBasisFloatBits { get; init; } = [];
        public int[] LocalPositionFloatBits { get; init; } = [];
    }
}
