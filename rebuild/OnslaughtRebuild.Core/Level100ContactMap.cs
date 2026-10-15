// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace OnslaughtRebuild.Core;

/// <summary>
/// Millimetre-space vector in Battle Engine Aquila axes: X/Y are horizontal
/// and positive Z is down.
/// </summary>
public readonly record struct Level100Vector3(int X, int Y, int Z)
{
    public static Level100Vector3 Zero => default;
}

/// <summary>
/// Allocation-free model-to-world basis, with each element expressed in
/// parts per million. The mission/native bridge owns the mutable basis.
/// </summary>
public readonly record struct Level100Basis3(
    int M00,
    int M01,
    int M02,
    int M10,
    int M11,
    int M12,
    int M20,
    int M21,
    int M22)
{
    public const int Scale = 1_000_000;

    public static Level100Basis3 Identity => new(
        Scale, 0, 0,
        0, Scale, 0,
        0, 0, Scale);

    internal bool IsOrthonormal =>
        IsElement(M00) && IsElement(M01) && IsElement(M02) &&
        IsElement(M10) && IsElement(M11) && IsElement(M12) &&
        IsElement(M20) && IsElement(M21) && IsElement(M22) &&
        IsUnitRow(M00, M01, M02) &&
        IsUnitRow(M10, M11, M12) &&
        IsUnitRow(M20, M21, M22) &&
        IsPerpendicular(M00, M01, M02, M10, M11, M12) &&
        IsPerpendicular(M00, M01, M02, M20, M21, M22) &&
        IsPerpendicular(M10, M11, M12, M20, M21, M22);

    private static bool IsElement(int value) => value is >= -Scale and <= Scale;

    private static bool IsUnitRow(int x, int y, int z)
    {
        const long tolerance = 10_000_000_000;
        long squaredLength = ((long)x * x) + ((long)y * y) + ((long)z * z);
        long unitSquared = (long)Scale * Scale;
        return Math.Abs(squaredLength - unitSquared) <= tolerance;
    }

    private static bool IsPerpendicular(
        int ax,
        int ay,
        int az,
        int bx,
        int by,
        int bz)
    {
        const long tolerance = 10_000_000_000;
        long dot = ((long)ax * bx) + ((long)ay * by) + ((long)az * bz);
        return Math.Abs(dot) <= tolerance;
    }
}

public readonly record struct Level100Transform3(
    Level100Vector3 Position,
    Level100Basis3 Basis)
{
    public static Level100Transform3 Identity => new(
        Level100Vector3.Zero,
        Level100Basis3.Identity);
}

public enum Level100ContactSurfaceKind : byte
{
    Mesh = 0,
    Terrain = 2,
}

public readonly record struct Level100ContactHit(
    int ActorId,
    int PartIndex,
    Level100ContactSurfaceKind SurfaceKind,
    int TimePartsPerMillion,
    Level100Vector3 ImpactCenter,
    Level100Vector3 SurfacePoint,
    Level100Vector3 NormalPartsPerMillion);

/// <summary>
/// One externally owned actor presented to a contact query. Empty
/// PartActivity means every decoded part is active; otherwise zero disables
/// the corresponding part. Velocity is the actor's world displacement over
/// the same interval as the supplied sweep.
/// </summary>
public readonly struct Level100ContactActor
{
    public Level100ContactActor(
        int actorId,
        bool active,
        Level100Transform3 transform,
        Level100Vector3 sweepVelocity,
        Level100ContactDefinition definition,
        ReadOnlyMemory<byte> partActivity = default)
    {
        if (actorId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorId));
        }
        if (!transform.Basis.IsOrthonormal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transform),
                "The actor transform must contain an orthonormal basis.");
        }
        ArgumentNullException.ThrowIfNull(definition);
        if (!partActivity.IsEmpty &&
            partActivity.Length != definition.PartCount)
        {
            throw new ArgumentException(
                "Part activity must be empty or match the decoded definition.",
                nameof(partActivity));
        }

        ActorId = actorId;
        Active = active;
        Transform = transform;
        SweepVelocity = sweepVelocity;
        Definition = definition;
        PartActivity = partActivity;
    }

    public int ActorId { get; }

    public bool Active { get; }

    public Level100Transform3 Transform { get; }

    public Level100Vector3 SweepVelocity { get; }

    public Level100ContactDefinition Definition { get; }

    public ReadOnlyMemory<byte> PartActivity { get; }
}

/// <summary>
/// The released destruction class a contact definition belongs to, not its
/// identity. <see cref="TargetTank"/> is the whole-body-life class shared by
/// every <c>Unit</c> record that carries the field set
/// <c>{1,3,5,8,9,10,11,23,46,48}</c> with behaviour class 3; both
/// <c>Target Tank</c> and <c>Target Truck</c> are in it (see
/// <see cref="Level100ContactCatalog"/>). <see cref="Warehouse"/> is the
/// per-segment building class.
/// </summary>
public enum Level100DefinitionKind : byte
{
    Static = 0,
    TargetTank = 1,
    Warehouse = 2,

    /// <summary>
    /// The released air-unit whole-body-life class. <c>Target Drone</c>'s
    /// <c>Unit</c> record (<c>default physics.dat</c> sha256
    /// <c>e1fb3ded...ada14</c>, record @0x24e76, name string @0x24e7e) carries
    /// behaviour value 9 in field 8 and field 3 <c>CUnitLife</c> 1.0
    /// (0x3F800000). The created actor is <c>CPlane</c>: vtable 0x005e1930,
    /// COL 0x00617930, type descriptor 0x0063d5a8. The configuration's
    /// <c>CFighterBehaviourType</c> is not that runtime actor class.
    /// It has whole-body life and no per-segment health like
    /// <see cref="TargetTank"/>, but its dying/shutdown path is a different
    /// released class: its field set
    /// {2,3,6,7,8,9,10,11,12,14,21,22,23,36,39,55} does not match the ground
    /// set {1,3,5,8,9,10,11,23,46,48}, so it is not folded into
    /// <see cref="TargetTank"/>. Its destruction record <c>Drone Explosion</c>
    /// @0x5df9 also diverges: field 6 is <c>Water Explosion Small</c> where
    /// fields 2/5/7 are <c>Drone Explosion Effect</c>.
    /// </summary>
    TargetDrone = 3,
}

public sealed class Level100ContactPart
{
    internal Level100ContactPart(
        int index,
        int parent,
        int reference,
        int type,
        string name,
        bool collidable,
        uint segmentValueBits,
        Level100Vector3 center,
        Level100Vector3 halfExtents,
        Level100Basis3 orientation,
        int[] vertices,
        int[] triangles,
        Level100ContactPartFloatGeometry floatGeometry)
    {
        FloatGeometry = floatGeometry;
        Index = index;
        Parent = parent;
        Reference = reference;
        Type = type;
        Name = name;
        Collidable = collidable;
        SegmentValueBits = segmentValueBits;
        Center = center;
        HalfExtents = halfExtents;
        Orientation = orientation;
        VerticesMillimeters = vertices;
        Triangles = triangles;
    }

    public Level100ContactPartFloatGeometry FloatGeometry { get; }

    public int Index { get; }

    public int Parent { get; }

    public int Reference { get; }

    public int Type { get; }

    public string Name { get; }

    public bool Collidable { get; }

    public uint SegmentValueBits { get; }

    public float SegmentValue =>
        BitConverter.Int32BitsToSingle(unchecked((int)SegmentValueBits));

    public Level100Vector3 Center { get; }

    public Level100Vector3 HalfExtents { get; }

    public Level100Basis3 Orientation { get; }

    public ReadOnlyMemory<int> VerticesMillimeters { get; }

    public ReadOnlyMemory<int> Triangles { get; }
}

/// <summary>
/// Original file records, separate from referenced geometry and the quantized preview.
/// BBOX words 3/7 (vector padding), word 8 and transform fourth words stay opaque.
/// CMSP words retain orientation blocks +00/+30 and position blocks +60/+70.
/// Hierarchy rows have 4 position or 12 orientation words; caches use the same strides.
/// Null caches mean absent records, not identity transforms or a selected runtime pose.
/// </summary>
public sealed record Level100ContactPartFloatGeometry(
    uint SourceId, int SourceType, int? Reference, int? Parent,
    ReadOnlyMemory<int> Children, int? Nmic, uint NumNmicWord, uint IsNmicWord,
    ReadOnlyMemory<uint> BoundingBoxWords, ReadOnlyMemory<uint> CmspTransformWords,
    uint Cmsp118Word, uint PositionCacheInheritanceWord, uint OrientationCacheInheritanceWord,
    ReadOnlyMemory<int>? FrameMap,
    IReadOnlyList<ReadOnlyMemory<uint>>? HierarchyPositionWords,
    IReadOnlyList<ReadOnlyMemory<uint>>? HierarchyOrientationWords,
    ReadOnlyMemory<uint>? CachedPositionWords, ReadOnlyMemory<uint>? CachedOrientationWords);

/// <summary>Original untransformed mesh words. Instance transforms and centre rounding are separate.</summary>
public sealed record Level100ContactFloatGeometry(
    uint BoundingBoxOriginXFloatBits, uint BoundingBoxOriginYFloatBits, uint BoundingBoxOriginZFloatBits,
    uint BoundingBoxRadiusFloatBits, uint MeshRenderRadiusFloatBits, uint PrimaryRadiusScaleFloatBits);

/// <summary>One stored passive mesh-report row, independent of later part activity.</summary>
public readonly record struct Level100PartBoundsContact(int PartIndex, int SignedDistanceFloatBits);

public sealed class Level100ContactDefinition
{
    private readonly Level100ContactPart[] _parts;

    internal Level100ContactDefinition(
        string name,
        string? mesh,
        Level100DefinitionKind kind,
        uint maximumLifeBits,
        string? destructionPhysicsDefinition,
        string? destructionParticleDescriptor,
        string? destructionWaterParticleDescriptor,
        string? destructionSoundDescriptor,
        Level100ContactPart[] parts,
        Level100ContactFloatGeometry? floatGeometry = null)
    {
        FloatGeometry = floatGeometry;
        Name = name;
        Mesh = mesh;
        Kind = kind;
        MaximumLifeBits = maximumLifeBits;
        DestructionPhysicsDefinition = destructionPhysicsDefinition;
        DestructionParticleDescriptor = destructionParticleDescriptor;
        DestructionWaterParticleDescriptor = destructionWaterParticleDescriptor;
        DestructionSoundDescriptor = destructionSoundDescriptor;
        _parts = parts;
        Parts = Array.AsReadOnly(parts);
    }

    public Level100ContactFloatGeometry? FloatGeometry { get; }

    public string Name { get; }

    public string? Mesh { get; }

    public Level100DefinitionKind Kind { get; }

    public uint MaximumLifeBits { get; }

    public float MaximumLife =>
        BitConverter.Int32BitsToSingle(unchecked((int)MaximumLifeBits));

    public string? DestructionPhysicsDefinition { get; }

    public string? DestructionParticleDescriptor { get; }

    /// <summary>
    /// The released <c>CExplosionWaterEffect</c> (explosion value id 6) of this
    /// definition's destruction record, carried separately because it is not
    /// always the same descriptor as the air/ground/unit variants. It is for
    /// <c>Target Tank</c>, <c>Target Truck</c> and <c>Warehouse</c>; it is not
    /// for <c>Target Drone</c>.
    /// </summary>
    public string? DestructionWaterParticleDescriptor { get; }

    public string? DestructionSoundDescriptor { get; }

    public IReadOnlyList<Level100ContactPart> Parts { get; }

    public int PartCount => _parts.Length;

    internal Level100ContactPart[] PartArray => _parts;
}

public readonly record struct Level100PulseRoundContract(
    int RadiusMillimeters,
    string ImpactPhysicsDefinition,
    string ImpactParticleDescriptor,
    string ImpactSoundDescriptor);

/// <summary>
/// Hash-verified, locally materialized Level 100 definitions. Mesh vertices,
/// part transforms and strip topology are deterministically millimetre-quantized
/// projections for Core contact; this is not a claim of bit-identical retail
/// collision geometry. The 33 serialized WRES rows are validated by this loader
/// but are intentionally not turned into actors; the mission/native Simulation
/// bridge owns identity, pose, activation and spawn.
/// </summary>
public sealed class Level100ContactCatalog
{
    private const string ResourceName =
        "OnslaughtRebuild.Core.Assets.Level100.level100-contact-owners.json";
    private const string SourceSha256 =
        "F793060ABF3CD958DE26A63100C5362A72BF606B5D5CE9E0B2E67A7698B72681";
    // Retained historical source identity; SourceSha256 verifies the complete asset.
    private const string StaticSourceAggregateSha256 =
        "8D85C9BFBE366C815E00D3900D8D29B71A33BEF7A60CDDFCE9ED6AC558E06B4C";
    private const string TargetTankSourceSha256 =
        "9B2CFDCEB86ED700ED924051FBFF13C32DC30BD8F8B948EA1CF8AA9FBFE8B97B";
    private const string TargetTruckSourceSha256 =
        "3BD92CE93D0619B7C4B0DD158680641FBAB6CD88580A68C6EF34E5F22F7596C5";
    private const string WarehouseSourceSha256 =
        "61FE5465BD7AFFEDF749AD784209BE02B2E4DD28631E70386C3810302B5F6F15";
    private const string TargetDroneSourceSha256 =
        "48876552AE836750221241719F333FB9B5221F78F1AB8BC03D5950CDBF4E6EC5";

    private readonly Dictionary<string, Level100ContactDefinition> _definitions;

    private Level100ContactCatalog(
        Dictionary<string, Level100ContactDefinition> definitions,
        Level100PulseRoundContract pulseRound)
    {
        _definitions = definitions;
        var orderedDefinitions = new Level100ContactDefinition[definitions.Count];
        int index = 0;
        foreach (Level100ContactDefinition definition in definitions.Values)
        {
            orderedDefinitions[index++] = definition;
        }
        Definitions = Array.AsReadOnly(orderedDefinitions);
        PulseRound = pulseRound;
    }

    public static Level100ContactCatalog Instance { get; } = LoadEmbedded();

    public IReadOnlyList<Level100ContactDefinition> Definitions { get; }

    public Level100PulseRoundContract PulseRound { get; }

    public Level100ContactDefinition GetDefinition(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _definitions.TryGetValue(name, out Level100ContactDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Level 100 has no decoded definition '{name}'.");
    }

    public bool TryGetDefinition(
        string name,
        out Level100ContactDefinition? definition) =>
        _definitions.TryGetValue(name, out definition);

    private static Level100ContactCatalog LoadEmbedded()
    {
        Assembly assembly = typeof(Level100ContactCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidDataException(
                "The locally materialized Level 100 contact asset is missing.");
        var source = new byte[checked((int)stream.Length)];
        stream.ReadExactly(source);
        if (!StringComparer.Ordinal.Equals(
            Convert.ToHexString(SHA256.HashData(source)),
            SourceSha256))
        {
            throw new InvalidDataException(
                "The Level 100 contact asset does not match its exact recipe.");
        }

        DocumentRow document = JsonSerializer.Deserialize<DocumentRow>(
            source,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("The Level 100 contact asset is empty.");
        if (!StringComparer.Ordinal.Equals(
                document.Schema,
                "onslaught.level100-contact-owners.v7") ||
            document.DefinitionCount != 24 ||
            document.InstanceCount != 33 ||
            document.PartCount != 362 ||
            document.StaticMeshCount != 24 ||
            document.TargetDefinitionCount != 4 ||
            document.Definitions.Length != 24 ||
            document.Instances.Length != 33 ||
            document.TargetDefinitions.Length != 4 ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                document.StaticSourceAggregateSha256,
                StaticSourceAggregateSha256) ||
            document.TargetSourceSha256.Count != 4 ||
            !document.TargetSourceSha256.TryGetValue(
                "Target Drone",
                out string? targetDroneSourceSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                targetDroneSourceSha256,
                TargetDroneSourceSha256) ||
            !document.TargetSourceSha256.TryGetValue(
                "Target Tank",
                out string? targetTankSourceSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                targetTankSourceSha256,
                TargetTankSourceSha256) ||
            !document.TargetSourceSha256.TryGetValue(
                "Target Truck",
                out string? targetTruckSourceSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                targetTruckSourceSha256,
                TargetTruckSourceSha256) ||
            !document.TargetSourceSha256.TryGetValue(
                "Warehouse",
                out string? warehouseSourceSha256) ||
            !StringComparer.OrdinalIgnoreCase.Equals(
                warehouseSourceSha256,
                WarehouseSourceSha256))
        {
            throw new InvalidDataException(
                "The Level 100 contact asset identity or counts changed.");
        }

        ValidateInstances(document.Instances);
        Level100PulseRoundContract pulse = ReadPulse(document.PulseRound);
        var definitions = new Dictionary<string, Level100ContactDefinition>(
            26,
            StringComparer.Ordinal);
        int partCount = 0;
        for (int index = 0; index < document.Definitions.Length; index++)
        {
            DefinitionRow row = document.Definitions[index];
            Level100ContactPart[] parts = ReadParts(row.Parts);
            partCount += parts.Length;
            if (!definitions.TryAdd(
                row.Definition,
                new Level100ContactDefinition(
                    row.Definition,
                    row.Mesh,
                    Level100DefinitionKind.Static,
                    0,
                    null,
                    null,
                    null,
                    null,
                    parts)))
            {
                throw new InvalidDataException(
                    "Level 100 has a duplicate decoded definition.");
            }
        }

        for (int index = 0; index < document.TargetDefinitions.Length; index++)
        {
            TargetDefinitionRow row = document.TargetDefinitions[index];
            Level100DefinitionKind kind = row.Definition switch
            {
                // `Target Truck` is byte-identical to `Target Tank` in
                // destruction class: `default physics.dat` (sha256
                // e1fb3ded...ada14) `Unit / Target Truck` at 0x24d9e carries
                // the tank's exact field set {1,3,5,8,9,10,11,23,46,48},
                // behaviour class 3 in field 8, and the same field 10
                // destruction record `Tank Explosion Medium`. Only field 3
                // (life 3.0 = 0x40400000 against 6.0 = 0x40C00000) and field 9
                // (mesh) differ, and both are carried per definition rather
                // than per kind. So it maps to the whole-body-life kind.
                "Target Tank" or "Target Truck" =>
                    Level100DefinitionKind.TargetTank,
                "Warehouse" => Level100DefinitionKind.Warehouse,
                // Whole-body life like the tank, but a different released
                // class; see Level100DefinitionKind.TargetDrone.
                "Target Drone" => Level100DefinitionKind.TargetDrone,
                _ => throw new InvalidDataException(
                    "Level 100 has an unexpected target definition."),
            };
            Level100ContactPart[] parts = ReadParts(row.Parts);
            partCount += parts.Length;
            if (!definitions.TryAdd(
                row.Definition,
                new Level100ContactDefinition(
                    row.Definition,
                    row.Mesh,
                    kind,
                    row.MaximumLifeBits,
                    row.DestructionPhysicsDefinition,
                    row.DestructionParticleDescriptor,
                    row.DestructionWaterParticleDescriptor,
                    row.DestructionSoundDescriptor,
                    parts,
                    ReadFloatGeometry(row.FloatGeometry, kind))))
            {
                throw new InvalidDataException(
                    "Level 100 has a duplicate target definition.");
            }
        }

        ValidateTargetContracts(definitions);
        if (partCount != document.PartCount)
        {
            throw new InvalidDataException(
                "Level 100 decoded part ownership changed.");
        }
        return new Level100ContactCatalog(definitions, pulse);
    }

    private static Level100ContactFloatGeometry ReadFloatGeometry(FloatGeometryRow? row, Level100DefinitionKind kind)
    {
        uint expectedScale = kind == Level100DefinitionKind.TargetTank ? 0x3f4ccccdu : 0x3f800000u;
        if (row is null || row.BoundingBoxOriginFloatBits.Length != 3 ||
            row.PrimaryRadiusScaleFloatBits != expectedScale ||
            row.BoundingBoxOriginFloatBits.Any(bits => !float.IsFinite(BitConverter.Int32BitsToSingle((int)bits))) ||
            !IsPositiveFinite(row.BoundingBoxRadiusFloatBits) || !IsPositiveFinite(row.MeshRenderRadiusFloatBits))
            throw new InvalidDataException("Level 100 original float geometry is missing or invalid.");
        return new(row.BoundingBoxOriginFloatBits[0], row.BoundingBoxOriginFloatBits[1],
            row.BoundingBoxOriginFloatBits[2], row.BoundingBoxRadiusFloatBits,
            row.MeshRenderRadiusFloatBits, row.PrimaryRadiusScaleFloatBits);
    }

    private static bool IsPositiveFinite(uint bits)
    {
        float value = BitConverter.Int32BitsToSingle((int)bits);
        return float.IsFinite(value) && value > 0;
    }

    private static Level100PulseRoundContract ReadPulse(PulseRoundRow row)
    {
        if (row.RadiusMillimeters != Level100ContactMechanics.PulseRadiusMillimeters ||
            !StringComparer.Ordinal.Equals(
                row.ImpactPhysicsDefinition,
                "Mech Pulse Hit Medium") ||
            !StringComparer.Ordinal.Equals(
                row.ImpactParticleDescriptor,
                "Mech Pulse Bolt Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                row.ImpactSoundDescriptor,
                "Explosion Small"))
        {
            throw new InvalidDataException(
                "The Level 100 pulse impact contract changed.");
        }
        return new Level100PulseRoundContract(
            row.RadiusMillimeters,
            row.ImpactPhysicsDefinition,
            row.ImpactParticleDescriptor,
            row.ImpactSoundDescriptor);
    }

    private static void ValidateTargetContracts(
        Dictionary<string, Level100ContactDefinition> definitions)
    {
        Level100ContactDefinition tank = definitions["Target Tank"];
        Level100ContactDefinition truck = definitions["Target Truck"];
        Level100ContactDefinition warehouse = definitions["Warehouse"];
        Level100ContactDefinition drone = definitions["Target Drone"];
        // `Target Drone` decodes from `m_FA_F24_training.msh.aya` (sha256
        // 48876552...6ec5) to 12 CMSH parts of which exactly one, `Object03`,
        // is collidable, with half-extents (519, 1416, 185) mm about centre
        // (-5, 323, -26). Three of the non-collidable parts are named `GUNA`,
        // `GUNA` and `GUNB`, which is the mesh's own corroboration of the two
        // `CUnitUse` weapon slots the Unit record binds - `Drone Vulcan
        // Cannon` to `GunA` and `Forseti Drone Missile Launcher` to `GunB`.
        // None of this volume is authored here.
        if (drone.MaximumLifeBits != 0x3F800000 || drone.PartCount != 12 ||
            drone.Kind != Level100DefinitionKind.TargetDrone ||
            !StringComparer.Ordinal.Equals(
                drone.Mesh,
                "m_FA_F24_training.msh.aya") ||
            !StringComparer.Ordinal.Equals(
                drone.DestructionPhysicsDefinition,
                "Drone Explosion") ||
            !StringComparer.Ordinal.Equals(
                drone.DestructionParticleDescriptor,
                "Drone Explosion Effect") ||
            !StringComparer.Ordinal.Equals(
                drone.DestructionWaterParticleDescriptor,
                "Water Explosion Small") ||
            !StringComparer.Ordinal.Equals(
                drone.DestructionSoundDescriptor,
                "Explosion Small") ||
            !drone.Parts[0].Collidable ||
            !StringComparer.Ordinal.Equals(drone.Parts[0].Name, "Object03") ||
            drone.Parts[0].HalfExtents != new Level100Vector3(519, 1416, 185) ||
            drone.Parts.Count(part => part.Collidable) != 1)
        {
            throw new InvalidDataException(
                "Level 100 Target Drone damage/effect definitions changed.");
        }
        // `Target Truck` carries one collidable CMSH part, `Mesh01`, with
        // 306 vertices, 432 triangles and half-extents (525, 1510, 287) mm
        // about centre (-4, -50, -475). That volume is decoded from the
        // shipped mesh; none of it is authored here.
        if (truck.MaximumLifeBits != 0x40400000 || truck.PartCount != 1 ||
            truck.Kind != Level100DefinitionKind.TargetTank ||
            !StringComparer.Ordinal.Equals(
                truck.Mesh,
                "m_f_truck_training.msh.aya") ||
            !StringComparer.Ordinal.Equals(
                truck.DestructionPhysicsDefinition,
                "Tank Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                truck.DestructionParticleDescriptor,
                "Tank Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                truck.DestructionWaterParticleDescriptor,
                "Tank Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                truck.DestructionSoundDescriptor,
                "Explosion Medium") ||
            !truck.Parts[0].Collidable ||
            truck.Parts[0].HalfExtents != new Level100Vector3(525, 1510, 287))
        {
            throw new InvalidDataException(
                "Level 100 Target Truck damage/effect definitions changed.");
        }
        if (tank.MaximumLifeBits != 0x40C00000 || tank.PartCount != 7 ||
            !StringComparer.Ordinal.Equals(
                tank.Mesh,
                "m_f_pulsetank_training.msh.aya") ||
            !StringComparer.Ordinal.Equals(
                tank.DestructionPhysicsDefinition,
                "Tank Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                tank.DestructionParticleDescriptor,
                "Tank Explosion Medium") ||
            !StringComparer.Ordinal.Equals(
                tank.DestructionSoundDescriptor,
                "Explosion Medium") ||
            warehouse.MaximumLifeBits != 0x42480000 || warehouse.PartCount != 28 ||
            !StringComparer.Ordinal.Equals(
                warehouse.Mesh,
                "m_m_warehouse.msh.aya") ||
            !StringComparer.Ordinal.Equals(
                warehouse.DestructionPhysicsDefinition,
                "Muspell Building Explosion") ||
            !StringComparer.Ordinal.Equals(
                warehouse.DestructionParticleDescriptor,
                "Muspell Building Explosion Effect") ||
            !StringComparer.Ordinal.Equals(
                warehouse.DestructionSoundDescriptor,
                "Explosion Medium Building"))
        {
            throw new InvalidDataException(
                "Level 100 target damage/effect definitions changed.");
        }
    }

    private static void ValidateInstances(InstanceRow[] instances)
    {
        var ids = new HashSet<int>();
        for (int index = 0; index < instances.Length; index++)
        {
            InstanceRow row = instances[index];
            if (!ids.Add(row.Id) ||
                row.PositionMillimeters.Length != 2 ||
                row.RetailPositionBits.Length != 3 ||
                row.RetailYawPitchRollBits.Length != 3 ||
                string.IsNullOrWhiteSpace(row.Definition) ||
                !StringComparer.Ordinal.Equals(
                    row.RootMode,
                    "authored-terrain-water"))
            {
                throw new InvalidDataException(
                    "Level 100 WRES instance metadata changed.");
            }
        }
    }

    private static Level100ContactPart[] ReadParts(PartRow[] rows)
    {
        var parts = new Level100ContactPart[rows.Length];
        for (int index = 0; index < rows.Length; index++)
        {
            PartRow row = rows[index];
            if (row.Index != index ||
                row.Parent >= index || row.Parent < -1 ||
                row.Reference >= index || row.Reference < -1 ||
                row.CenterMillimeters.Length != 3 ||
                row.HalfExtentsMillimeters.Length != 3 ||
                row.OrientationPartsPerMillion.Length != 9 ||
                row.VerticesMillimeters.Length % 3 != 0 ||
                row.Triangles.Length % 3 != 0 ||
                string.IsNullOrEmpty(row.Name))
            {
                throw new InvalidDataException(
                    "Level 100 has invalid decoded part metadata.");
            }
            int vertexCount = row.VerticesMillimeters.Length / 3;
            for (int triangleIndex = 0;
                 triangleIndex < row.Triangles.Length;
                 triangleIndex++)
            {
                if ((uint)row.Triangles[triangleIndex] >= (uint)vertexCount)
                {
                    throw new InvalidDataException(
                        "Level 100 has an invalid mesh triangle index.");
                }
            }
            if (row.Collidable &&
                (vertexCount == 0 || row.Triangles.Length == 0 ||
                 row.HalfExtentsMillimeters[0] <= 0 ||
                 row.HalfExtentsMillimeters[1] <= 0 ||
                 row.HalfExtentsMillimeters[2] <= 0))
            {
                throw new InvalidDataException(
                    "Level 100 has an empty collidable mesh part.");
            }

            parts[index] = new Level100ContactPart(
                row.Index,
                row.Parent,
                row.Reference,
                row.Type,
                row.Name,
                row.Collidable,
                row.SegmentValueBits,
                ReadVector(row.CenterMillimeters),
                ReadVector(row.HalfExtentsMillimeters),
                ReadBasis(row.OrientationPartsPerMillion),
                row.VerticesMillimeters,
                row.Triangles,
                ReadPartFloatGeometry(row));
        }
        return parts;
    }

    private static Level100Vector3 ReadVector(int[] values) =>
        new(values[0], values[1], values[2]);

    private static Level100Basis3 ReadBasis(int[] values)
    {
        var basis = new Level100Basis3(
            values[0], values[1], values[2],
            values[3], values[4], values[5],
            values[6], values[7], values[8]);
        return basis.IsOrthonormal
            ? basis
            : throw new InvalidDataException(
                "Level 100 has an invalid decoded part basis.");
    }

    private sealed class DocumentRow
    {
        public string Schema { get; set; } = string.Empty;
        public int DefinitionCount { get; set; }
        public int InstanceCount { get; set; }
        public int PartCount { get; set; }
        public int StaticMeshCount { get; set; }
        public int TargetDefinitionCount { get; set; }
        public string StaticSourceAggregateSha256 { get; set; } = string.Empty;
        public Dictionary<string, string> TargetSourceSha256 { get; set; } = [];
        public DefinitionRow[] Definitions { get; set; } = [];
        public InstanceRow[] Instances { get; set; } = [];
        public TargetDefinitionRow[] TargetDefinitions { get; set; } = [];
        public PulseRoundRow PulseRound { get; set; } = new();
    }

    private sealed class DefinitionRow
    {
        public string Definition { get; set; } = string.Empty;
        public string Mesh { get; set; } = string.Empty;
        public PartRow[] Parts { get; set; } = [];
    }

    private sealed class FloatGeometryRow
    {
        public uint[] BoundingBoxOriginFloatBits { get; set; } = [];
        public uint BoundingBoxRadiusFloatBits { get; set; }
        public uint MeshRenderRadiusFloatBits { get; set; }
        public uint PrimaryRadiusScaleFloatBits { get; set; }
    }

    private sealed class TargetDefinitionRow
    {
        public FloatGeometryRow? FloatGeometry { get; set; }
        public string Definition { get; set; } = string.Empty;
        public string Mesh { get; set; } = string.Empty;
        public string DestructionPhysicsDefinition { get; set; } = string.Empty;
        public string DestructionParticleDescriptor { get; set; } = string.Empty;
        public string DestructionWaterParticleDescriptor { get; set; } = string.Empty;
        public string DestructionSoundDescriptor { get; set; } = string.Empty;
        public uint MaximumLifeBits { get; set; }
        public PartRow[] Parts { get; set; } = [];
    }

    private sealed class PulseRoundRow
    {
        public string ImpactPhysicsDefinition { get; set; } = string.Empty;
        public string ImpactParticleDescriptor { get; set; } = string.Empty;
        public string ImpactSoundDescriptor { get; set; } = string.Empty;
        public int RadiusMillimeters { get; set; }
    }

    private sealed class InstanceRow
    {
        public int Id { get; set; }
        public string Definition { get; set; } = string.Empty;
        public int[] PositionMillimeters { get; set; } = [];
        public uint[] RetailPositionBits { get; set; } = [];
        public uint[] RetailYawPitchRollBits { get; set; } = [];
        public string RootMode { get; set; } = string.Empty;
    }

    private static Level100ContactPartFloatGeometry ReadPartFloatGeometry(PartRow part)
    {
        PartFloatGeometryRow row = part.FloatGeometry ??
            throw new InvalidDataException("Level 100 is missing original part records.");
        bool hasTrack = row.FrameMap is not null;
        if (row.SourceType != part.Type || row.Reference != (part.Reference < 0 ? null : part.Reference) ||
            row.Parent != (part.Parent < 0 ? null : part.Parent) ||
            row.BoundingBoxWords.Length != 10 || row.CmspTransformWords.Length != 32 ||
            hasTrack != (row.HierarchyPositionWords is not null) ||
            hasTrack != (row.HierarchyOrientationWords is not null) ||
            (hasTrack && (row.FrameMap!.Length == 0 ||
                row.HierarchyPositionWords!.Length == 0 ||
                row.HierarchyPositionWords.Length != row.HierarchyOrientationWords!.Length ||
                row.HierarchyPositionWords.Any(words => words.Length != 4) ||
                row.HierarchyOrientationWords.Any(words => words.Length != 12) ||
                row.FrameMap.Any(frame => frame < 0 || frame >= row.HierarchyPositionWords.Length))) ||
            (row.CachedPositionWords is not null &&
                (row.CachedPositionWords.Length == 0 || row.CachedPositionWords.Length % 4 != 0)) ||
            (row.CachedOrientationWords is not null &&
                (row.CachedOrientationWords.Length == 0 || row.CachedOrientationWords.Length % 12 != 0)))
        {
            throw new InvalidDataException("Level 100 has invalid original part records.");
        }
        return new(row.SourceId, row.SourceType, row.Reference, row.Parent, row.Children, row.Nmic,
            row.NumNmicWord, row.IsNmicWord,
            row.BoundingBoxWords, row.CmspTransformWords, row.Cmsp118Word,
            row.PositionCacheInheritanceWord, row.OrientationCacheInheritanceWord,
            row.FrameMap is null ? (ReadOnlyMemory<int>?)null : new ReadOnlyMemory<int>(row.FrameMap),
            row.HierarchyPositionWords is null ? null : Array.AsReadOnly(row.HierarchyPositionWords
                .Select(words => new ReadOnlyMemory<uint>(words)).ToArray()),
            row.HierarchyOrientationWords is null ? null : Array.AsReadOnly(row.HierarchyOrientationWords
                .Select(words => new ReadOnlyMemory<uint>(words)).ToArray()),
            row.CachedPositionWords is null ? (ReadOnlyMemory<uint>?)null : new ReadOnlyMemory<uint>(row.CachedPositionWords),
            row.CachedOrientationWords is null ? (ReadOnlyMemory<uint>?)null : new ReadOnlyMemory<uint>(row.CachedOrientationWords));
    }

    private sealed class PartFloatGeometryRow
    {
        public uint SourceId { get; set; }
        public int SourceType { get; set; }
        public int? Reference { get; set; }
        public int? Parent { get; set; }
        public int[] Children { get; set; } = [];
        public int? Nmic { get; set; }
        public uint NumNmicWord { get; set; }
        public uint IsNmicWord { get; set; }
        public uint[] BoundingBoxWords { get; set; } = [];
        public uint[] CmspTransformWords { get; set; } = [];
        public uint Cmsp118Word { get; set; }
        public uint PositionCacheInheritanceWord { get; set; }
        public uint OrientationCacheInheritanceWord { get; set; }
        public int[]? FrameMap { get; set; }
        public uint[][]? HierarchyPositionWords { get; set; }
        public uint[][]? HierarchyOrientationWords { get; set; }
        public uint[]? CachedPositionWords { get; set; }
        public uint[]? CachedOrientationWords { get; set; }
    }

    private sealed class PartRow
    {
        public PartFloatGeometryRow? FloatGeometry { get; set; }
        public int Index { get; set; }
        public int Parent { get; set; }
        public int Reference { get; set; }
        public int Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Collidable { get; set; }
        public uint SegmentValueBits { get; set; }
        public int[] CenterMillimeters { get; set; } = [];
        public int[] HalfExtentsMillimeters { get; set; } = [];
        public int[] OrientationPartsPerMillion { get; set; } = [];
        public int[] VerticesMillimeters { get; set; } = [];
        public int[] Triangles { get; set; } = [];
    }
}

/// <summary>
/// The bounded released contact paths used by Level 100. CMSH BBOX records
/// reject candidates only; mesh triangles own every reported actor hit.
/// </summary>
public static class Level100ContactMechanics
{
    public const int PulseRadiusMillimeters = 70;

    private const double AxisScale = Level100Basis3.Scale;
    private const double RetailTriangleEpsilonMillimeters = 10.0;
    private const double GeometryEpsilon = 1e-9;

    /// <summary>
    /// Build the passive bounds report for the admitted Warehouse, in original
    /// part order. Caller supplies selected world poses and collision eligibility
    /// (the completed-break latch), not health or the preview's Collidable flag.
    /// This does not refresh caches, run segment controllers or apply damage.
    /// </summary>
    public static int CollectWarehouseSphereBounds(
        Level100ContactDefinition definition,
        ReadOnlySpan<RetailUnitAttachmentPose> partPoses,
        ReadOnlySpan<byte> partActivity,
        Level100FloatVector3Bits currentCenter,
        Level100FloatVector3Bits displacement,
        int radiusFloatBits,
        Span<Level100PartBoundsContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Kind != Level100DefinitionKind.Warehouse ||
            partPoses.Length != definition.PartCount || partActivity.Length != definition.PartCount ||
            contacts.Length < 6)
            throw new ArgumentException("Warehouse bounds require one explicit pose/activity per part and six report slots.");

        // This admitted Warehouse has only Core/Extra segments; both variant
        // methods return zero. General controller/NMIC selection is excluded.
        for (int i = 0; i < definition.PartCount; i++)
        {
            var raw = definition.Parts[i].FloatGeometry;
            if (raw.NumNmicWord != 0 || raw.IsNmicWord != 0 || raw.Nmic is not null || partActivity[i] > 1)
                throw new ArgumentException("Warehouse controller input exceeds the admitted no-NMIC contract.");
        }

        int count = 0;
        for (int i = 0; i < definition.PartCount; i++)
        {
            var context = definition.Parts[i].FloatGeometry;
            var geometry = context.SourceType is 1 or 3 ? context :
                context.SourceType == 6 && context.Reference is int reference &&
                (uint)reference < (uint)definition.PartCount ? definition.Parts[reference].FloatGeometry : null;
            if (geometry is null || geometry.SourceType != 1 || partActivity[i] == 0) continue;

            // Geometry references supply BBOX data, but keep the original
            // context's cache index and report identity. No recursive ref chase.
            var query = RetailMeshPartPose.ToLocalSphereQuery(partPoses[i], currentCenter, displacement);
            ReadOnlySpan<uint> box = geometry.BoundingBoxWords.Span;
            if (!TryPassiveSphereBounds(query.Position, query.Displacement, radiusFloatBits,
                new(unchecked((int)box[0]), unchecked((int)box[1]), unchecked((int)box[2])),
                new(unchecked((int)box[4]), unchecked((int)box[5]), unchecked((int)box[6])), out int distance)) continue;

            contacts[count++] = new(unchecked((int)context.SourceId), distance);
            if (count == 6) return count; // Retail stops before considering a seventh contact.
        }
        return count;
    }

    /// <summary>
    /// Passive sphere/part-bounds arm of retail 0x004ac140. Inputs are already
    /// in the selected part's local space; no pose, activity or candidate selection
    /// is performed. Operations round to 24 significand bits, ties to even,
    /// following device-creation intent; the live FPU state remains unmeasured.
    /// Inputs and stored intermediates must stay finite.
    /// </summary>
    public static bool TryPassiveSphereBounds(
        Level100FloatVector3Bits position,
        Level100FloatVector3Bits relativeDisplacement,
        int radiusFloatBits,
        Level100FloatVector3Bits boxOrigin,
        Level100FloatVector3Bits boxHalfExtents,
        out int signedDistanceFloatBits)
    {
        // Pristine 74154bfa…7750; cround-hit-damage-path-2026-08-10.md owns pins.
        // [4ac140,4ac31a): 920e1728a21e98800ed01a5aabbd8eb0c4876e9d4206ad4311cb0dc3f8ab8180.
        // X remains in the register stack; Y/Z endpoints spill before subtraction.
        // Register operations still round at PC24; stores also bound exponents.
        double px = ReadBoundsFloat(position.X), py = ReadBoundsFloat(position.Y), pz = ReadBoundsFloat(position.Z);
        double dx = RetailFloat24.Subtract(RetailFloat24.Add(px, ReadBoundsFloat(relativeDisplacement.X)), px);
        double dy = RetailFloat24.Subtract(BoundsFloat(RetailFloat24.Add(py, ReadBoundsFloat(relativeDisplacement.Y))), py);
        double dz = RetailFloat24.Subtract(BoundsFloat(RetailFloat24.Add(pz, ReadBoundsFloat(relativeDisplacement.Z))), pz);
        double radius = ReadBoundsFloat(radiusFloatBits);
        double hx = ReadBoundsFloat(boxHalfExtents.X), hy = ReadBoundsFloat(boxHalfExtents.Y), hz = ReadBoundsFloat(boxHalfExtents.Z);
        if (radius < 0 || hx < 0 || hy < 0 || hz < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusFloatBits), "Bounds radius and half extents must be nonnegative.");
        }
        double lengthSquared = RetailFloat24.Add(
            RetailFloat24.Add(RetailFloat24.Multiply(dz, dz), RetailFloat24.Multiply(dy, dy)),
            RetailFloat24.Multiply(dx, dx));
        double effectiveRadius = BoundsFloat(RetailFloat24.Add(
            RetailFloat24.Multiply(RetailFloat24.Sqrt(lengthSquared), 0.5), radius));
        double mx = BoundsFloat(RetailFloat24.Add(RetailFloat24.Multiply(dx, 0.5), px));
        double my = BoundsFloat(RetailFloat24.Add(BoundsFloat(RetailFloat24.Multiply(BoundsFloat(dy), 0.5)), py));
        double mz = BoundsFloat(RetailFloat24.Add(BoundsFloat(RetailFloat24.Multiply(BoundsFloat(dz), 0.5)), pz));
        double qx = BoundsFloat(RetailFloat24.Subtract(mx, ReadBoundsFloat(boxOrigin.X)));
        double qy = BoundsFloat(RetailFloat24.Subtract(my, ReadBoundsFloat(boxOrigin.Y)));
        double qz = BoundsFloat(RetailFloat24.Subtract(mz, ReadBoundsFloat(boxOrigin.Z)));
        if (!double.IsFinite(effectiveRadius) || !double.IsFinite(qx) ||
            !double.IsFinite(qy) || !double.IsFinite(qz))
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Bounds inputs must keep intermediate float stores finite.");
        }
        signedDistanceFloatBits = 0;
        if (RetailFloat24.Subtract(qx, effectiveRadius) > hx || RetailFloat24.Add(qx, effectiveRadius) < -hx ||
            RetailFloat24.Subtract(qy, effectiveRadius) > hy || RetailFloat24.Add(qy, effectiveRadius) < -hy ||
            RetailFloat24.Subtract(qz, effectiveRadius) > hz || RetailFloat24.Add(qz, effectiveRadius) < -hz)
        {
            return false;
        }

        // Compare before the accepted report's float store.
        double distance = RetailFloat24.Subtract(RetailDistanceOutsideBounds(qx, qy, qz, hx, hy, hz), effectiveRadius);
        if (!(distance <= 0))
        {
            return false;
        }
        signedDistanceFloatBits = BitConverter.SingleToInt32Bits((float)distance);
        return true;
    }

    private static double RetailDistanceOutsideBounds(double x, double y, double z,
        double hx, double hy, double hz)
    {
        // [479770,4798ca): 709e7fe8806057eb4b0549d8a4d1039d36c14249dd2c0ac0bf906ccf9d8e2d44.
        double gx = RetailFloat24.Subtract(Math.Abs(x), Math.Abs(hx));
        double gy = RetailFloat24.Subtract(Math.Abs(y), Math.Abs(hy));
        double gz = RetailFloat24.Subtract(Math.Abs(z), Math.Abs(hz));
        int outside = (gx > 0 ? 1 : 0) | (gy > 0 ? 2 : 0) | (gz > 0 ? 4 : 0);
        x = BoundsFloat(gx); y = BoundsFloat(gy); z = BoundsFloat(gz);
        return outside switch
        {
            0 => 0,
            1 => x,
            2 => y,
            4 => z,
            3 => RetailFloat24.Sqrt(RetailFloat24.Add(RetailFloat24.Multiply(y, y), RetailFloat24.Multiply(x, x))),
            5 => RetailFloat24.Sqrt(RetailFloat24.Add(RetailFloat24.Multiply(z, z), RetailFloat24.Multiply(x, x))),
            6 => RetailFloat24.Sqrt(RetailFloat24.Add(RetailFloat24.Multiply(z, z), RetailFloat24.Multiply(y, y))),
            // Shipped 0x004798a4 is DC C0 (FADD ST0,ST0), not a square.
            7 => RetailFloat24.Sqrt(RetailFloat24.Add(
                RetailFloat24.Add(RetailFloat24.Multiply(y, y), RetailFloat24.Multiply(x, x)),
                RetailFloat24.Add(z, z))),
            _ => throw new InvalidOperationException("Invalid bounds classification."),
        };
    }

    private static double BoundsFloat(double value) => (float)value;

    private static double ReadBoundsFloat(int bits)
    {
        float value = BitConverter.Int32BitsToSingle(bits);
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(bits), "Bounds inputs must be finite.");
        }
        return value;
    }

    public static bool TrySweepPulse(
        Level100Vector3 start,
        Level100Vector3 end,
        ReadOnlySpan<Level100ContactActor> actors,
        out Level100ContactHit hit) =>
        TrySweepActors(
            start,
            end,
            PulseRadiusMillimeters,
            actors,
            out hit);

    /// <summary>
    /// Deterministic Level 100 pulse/heightfield contact. The subdivision walks
    /// the exact 24.8 sampler lattice and searches each crossed interval in
    /// order, so a ridge cannot be skipped merely because both endpoints are
    /// clear.
    /// </summary>
    public static bool TrySweepPulseAgainstTerrain(
        Level100Vector3 start,
        Level100Vector3 end,
        out Level100ContactHit hit) =>
        TrySweepRoundAgainstTerrain(start, end, PulseRadiusMillimeters, out hit);

    public static bool TrySweepRoundAgainstTerrain(
        Level100Vector3 start,
        Level100Vector3 end,
        int contactRadiusMillimeters,
        out Level100ContactHit hit) =>
        TrySweepRoundAgainstTerrain(start, end, contactRadiusMillimeters, prune: true, out hit);

    /// <summary>
    /// The same search without the height-bound pruning, for the test that
    /// proves pruning never changes a result.
    /// </summary>
    internal static bool TrySweepRoundAgainstTerrainWithoutPruning(
        Level100Vector3 start,
        Level100Vector3 end,
        int contactRadiusMillimeters,
        out Level100ContactHit hit) =>
        TrySweepRoundAgainstTerrain(start, end, contactRadiusMillimeters, prune: false, out hit);

    private static bool TrySweepRoundAgainstTerrain(
        Level100Vector3 start,
        Level100Vector3 end,
        int contactRadiusMillimeters,
        bool prune,
        out Level100ContactHit hit)
    {
        int high;
        if (TouchesTerrain(start, contactRadiusMillimeters))
        {
            high = 0;
        }
        else if (!TryFindFirstTerrainContact(
            start,
            end,
            0,
            Level100Basis3.Scale,
            contactRadiusMillimeters,
            prune,
            out high))
        {
            hit = default;
            return false;
        }

        Level100Vector3 center = Interpolate(start, end, high);
        int groundElevation = SampleTerrain(center.X, center.Y);
        Level100Vector3 normal = TerrainNormal(center.X, center.Y);
        hit = new Level100ContactHit(
            0,
            -1,
            Level100ContactSurfaceKind.Terrain,
            high,
            center,
            new Level100Vector3(center.X, center.Y, -groundElevation),
            normal);
        return true;
    }

    public static bool TrySweepPulseWithTerrain(
        Level100Vector3 start,
        Level100Vector3 end,
        ReadOnlySpan<Level100ContactActor> actors,
        out Level100ContactHit hit) =>
        TrySweepRoundWithTerrain(
            start,
            end,
            PulseRadiusMillimeters,
            actors,
            out hit);

    public static bool TrySweepRoundWithTerrain(
        Level100Vector3 start,
        Level100Vector3 end,
        int contactRadiusMillimeters,
        ReadOnlySpan<Level100ContactActor> actors,
        out Level100ContactHit hit)
    {
        bool hasActor = TrySweepActors(
            start,
            end,
            contactRadiusMillimeters,
            actors,
            out Level100ContactHit actorHit);
        bool hasTerrain = TrySweepRoundAgainstTerrain(
            start,
            end,
            contactRadiusMillimeters,
            out Level100ContactHit terrainHit);
        if (!hasActor && !hasTerrain)
        {
            hit = default;
            return false;
        }
        if (!hasTerrain ||
            (hasActor && actorHit.TimePartsPerMillion <= terrainHit.TimePartsPerMillion))
        {
            hit = actorHit;
            return true;
        }
        hit = terrainHit;
        return true;
    }

    private static bool TrySweepActors(
        Level100Vector3 start,
        Level100Vector3 end,
        int meshSphereRadius,
        ReadOnlySpan<Level100ContactActor> actors,
        out Level100ContactHit hit)
    {
        bool found = false;
        double bestTime = double.PositiveInfinity;
        int bestActorId = int.MaxValue;
        int bestPart = int.MaxValue;
        Level100ContactHit bestHit = default;
        for (int actorIndex = 0; actorIndex < actors.Length; actorIndex++)
        {
            ref readonly Level100ContactActor actor = ref actors[actorIndex];
            if (!actor.Active)
            {
                continue;
            }

            if (TrySweepMesh(
                    start,
                    end,
                    meshSphereRadius,
                    actor,
                    out double meshTime,
                    out Level100ContactHit meshHit) &&
                IsPreferred(
                    meshTime,
                    actor.ActorId,
                    meshHit.PartIndex,
                    bestTime,
                    bestActorId,
                    bestPart))
            {
                found = true;
                bestTime = meshTime;
                bestActorId = actor.ActorId;
                bestPart = meshHit.PartIndex;
                bestHit = meshHit;
            }
        }
        hit = bestHit;
        return found;
    }

    private static bool IsPreferred(
        double time,
        int actorId,
        int part,
        double bestTime,
        int bestActorId,
        int bestPart) =>
        time < bestTime - GeometryEpsilon ||
        (Math.Abs(time - bestTime) <= GeometryEpsilon &&
            (actorId < bestActorId ||
             (actorId == bestActorId && part < bestPart)));

    private static bool TrySweepMesh(
        Level100Vector3 worldStart,
        Level100Vector3 worldEnd,
        int radius,
        in Level100ContactActor actor,
        out double bestTime,
        out Level100ContactHit hit)
    {
        DVector3 start = ToActorModel(
            worldStart,
            actor.Transform.Position,
            actor.Transform.Basis);
        Level100Vector3 movedPosition = Add(
            actor.Transform.Position,
            actor.SweepVelocity);
        DVector3 end = ToActorModel(
            worldEnd,
            movedPosition,
            actor.Transform.Basis);
        DVector3 delta = end - start;
        Level100ContactPart[] parts = actor.Definition.PartArray;
        ReadOnlySpan<byte> activity = actor.PartActivity.Span;
        bool found = false;
        bestTime = double.PositiveInfinity;
        int selectedPart = -1;
        DVector3 selectedNormal = default;

        for (int partIndex = 0; partIndex < parts.Length; partIndex++)
        {
            Level100ContactPart part = parts[partIndex];
            if (!part.Collidable ||
                (!activity.IsEmpty && activity[partIndex] == 0) ||
                !SweptSphereIntersectsBounds(start, end, radius, part))
            {
                continue;
            }

            ReadOnlySpan<int> vertices = part.VerticesMillimeters.Span;
            ReadOnlySpan<int> triangles = part.Triangles.Span;
            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                DVector3 a = ReadVertex(vertices, triangles[triangle]);
                DVector3 b = ReadVertex(vertices, triangles[triangle + 1]);
                DVector3 c = ReadVertex(vertices, triangles[triangle + 2]);
                if (TrySweepSphereTriangle(
                        start,
                        delta,
                        radius,
                        a,
                        b,
                        c,
                        out double time,
                        out DVector3 normal) &&
                    (time < bestTime - GeometryEpsilon ||
                     (Math.Abs(time - bestTime) <= GeometryEpsilon &&
                      part.Index < selectedPart)))
                {
                    found = true;
                    bestTime = time;
                    selectedPart = part.Index;
                    selectedNormal = normal;
                }
            }
        }

        if (!found)
        {
            hit = default;
            return false;
        }
        hit = BuildActorHit(
            actor.ActorId,
            selectedPart,
            Level100ContactSurfaceKind.Mesh,
            worldStart,
            worldEnd,
            radius,
            bestTime,
            selectedNormal,
            actor.Transform.Basis);
        return true;
    }

    private static Level100ContactHit BuildActorHit(
        int actorId,
        int partIndex,
        Level100ContactSurfaceKind kind,
        Level100Vector3 start,
        Level100Vector3 end,
        int radius,
        double time,
        DVector3 modelNormal,
        Level100Basis3 actorBasis)
    {
        DVector3 worldNormal = Normalize(ToWorldVector(modelNormal, actorBasis));
        DVector3 worldCenter = FromInt(start) +
            ((FromInt(end) - FromInt(start)) * time);
        DVector3 surface = worldCenter - (worldNormal * radius);
        return new Level100ContactHit(
            actorId,
            partIndex,
            kind,
            QuantizeTime(time),
            RoundVector(worldCenter),
            RoundVector(surface),
            new Level100Vector3(
                RoundAway(worldNormal.X * AxisScale),
                RoundAway(worldNormal.Y * AxisScale),
                RoundAway(worldNormal.Z * AxisScale)));
    }

    private static bool SweptSphereIntersectsBounds(
        DVector3 start,
        DVector3 end,
        int radius,
        Level100ContactPart part)
    {
        DVector3 localStart = ToPartLocal(start, part);
        DVector3 localEnd = ToPartLocal(end, part);
        DVector3 half = FromInt(part.HalfExtents);
        return SegmentIntersectsBox(
            localStart,
            localEnd,
            new DVector3(
                half.X + radius,
                half.Y + radius,
                half.Z + radius));
    }

    private static DVector3 ToPartLocal(
        DVector3 modelPoint,
        Level100ContactPart part)
    {
        DVector3 delta = modelPoint - FromInt(part.Center);
        return TransposeMultiply(part.Orientation, delta);
    }

    private static bool SegmentIntersectsBox(
        DVector3 start,
        DVector3 end,
        DVector3 half)
    {
        DVector3 delta = end - start;
        double minimum = 0;
        double maximum = 1;
        if (!ClipSlab(start.X, delta.X, half.X, ref minimum, ref maximum) ||
            !ClipSlab(start.Y, delta.Y, half.Y, ref minimum, ref maximum) ||
            !ClipSlab(start.Z, delta.Z, half.Z, ref minimum, ref maximum))
        {
            return false;
        }
        return minimum <= maximum;
    }

    private static bool ClipSlab(
        double start,
        double delta,
        double half,
        ref double minimum,
        ref double maximum)
    {
        if (Math.Abs(delta) <= GeometryEpsilon)
        {
            return start >= -half && start <= half;
        }
        double first = (-half - start) / delta;
        double second = (half - start) / delta;
        if (first > second)
        {
            (first, second) = (second, first);
        }
        minimum = Math.Max(minimum, first);
        maximum = Math.Min(maximum, second);
        return minimum <= maximum;
    }

    private static bool TrySweepSphereTriangle(
        DVector3 start,
        DVector3 delta,
        double radius,
        DVector3 a,
        DVector3 b,
        DVector3 c,
        out double time,
        out DVector3 hitNormal)
    {
        DVector3 cross = Cross(b - a, c - a);
        double crossLength = Length(cross);
        if (crossLength <= GeometryEpsilon)
        {
            time = 0;
            hitNormal = default;
            return false;
        }
        DVector3 triangleNormal = cross / crossLength;
        double normalSweep = Dot(triangleNormal, delta);
        if (normalSweep >= RetailTriangleEpsilonMillimeters ||
            Dot(a - start, triangleNormal) >= RetailTriangleEpsilonMillimeters)
        {
            time = 0;
            hitNormal = default;
            return false;
        }

        DVector3 closest = ClosestPointOnTriangle(start, a, b, c);
        DVector3 initialOffset = start - closest;
        double radiusSquared = radius * radius;
        if (LengthSquared(initialOffset) <= radiusSquared + GeometryEpsilon)
        {
            time = 0;
            hitNormal = LengthSquared(initialOffset) > GeometryEpsilon
                ? Normalize(initialOffset)
                : triangleNormal;
            return true;
        }

        bool found = false;
        double best = double.PositiveInfinity;
        DVector3 bestNormal = default;
        double startDistance = Dot(start - a, triangleNormal);
        if (normalSweep < -GeometryEpsilon)
        {
            double planeTime = (radius - startDistance) / normalSweep;
            if (planeTime is >= 0 and <= 1)
            {
                DVector3 center = start + (delta * planeTime);
                DVector3 point = center - (triangleNormal * radius);
                if (PointInTriangle(point, a, b, c, triangleNormal))
                {
                    found = true;
                    best = planeTime;
                    bestNormal = triangleNormal;
                }
            }
        }

        TryEdgeCandidate(start, delta, radius, a, b, ref found, ref best, ref bestNormal);
        TryEdgeCandidate(start, delta, radius, b, c, ref found, ref best, ref bestNormal);
        TryEdgeCandidate(start, delta, radius, c, a, ref found, ref best, ref bestNormal);

        time = best;
        hitNormal = bestNormal;
        return found;
    }

    private static void TryEdgeCandidate(
        DVector3 start,
        DVector3 delta,
        double radius,
        DVector3 a,
        DVector3 b,
        ref bool found,
        ref double best,
        ref DVector3 bestNormal)
    {
        DVector3 edge = b - a;
        double edgeLengthSquared = LengthSquared(edge);
        if (edgeLengthSquared > GeometryEpsilon)
        {
            DVector3 fromA = start - a;
            double startProjection = Dot(fromA, edge) / edgeLengthSquared;
            double deltaProjection = Dot(delta, edge) / edgeLengthSquared;
            DVector3 radialStart = fromA - (edge * startProjection);
            DVector3 radialDelta = delta - (edge * deltaProjection);
            if (TryFirstQuadraticRoot(
                    LengthSquared(radialDelta),
                    2 * Dot(radialStart, radialDelta),
                    LengthSquared(radialStart) - (radius * radius),
                    out double cylinderTime))
            {
                double edgeParameter = startProjection +
                    (deltaProjection * cylinderTime);
                if (edgeParameter is >= 0 and <= 1)
                {
                    DVector3 center = start + (delta * cylinderTime);
                    DVector3 point = a + (edge * edgeParameter);
                    SelectCandidate(
                        cylinderTime,
                        center - point,
                        ref found,
                        ref best,
                        ref bestNormal);
                }
            }
        }
        TryPointCandidate(start, delta, radius, a, ref found, ref best, ref bestNormal);
        TryPointCandidate(start, delta, radius, b, ref found, ref best, ref bestNormal);
    }

    private static void TryPointCandidate(
        DVector3 start,
        DVector3 delta,
        double radius,
        DVector3 point,
        ref bool found,
        ref double best,
        ref DVector3 bestNormal)
    {
        DVector3 offset = start - point;
        if (TryFirstQuadraticRoot(
            LengthSquared(delta),
            2 * Dot(offset, delta),
            LengthSquared(offset) - (radius * radius),
            out double candidate))
        {
            SelectCandidate(
                candidate,
                (start + (delta * candidate)) - point,
                ref found,
                ref best,
                ref bestNormal);
        }
    }

    private static void SelectCandidate(
        double time,
        DVector3 normal,
        ref bool found,
        ref double best,
        ref DVector3 bestNormal)
    {
        if (time is < 0 or > 1 ||
            (found && time >= best - GeometryEpsilon) ||
            LengthSquared(normal) <= GeometryEpsilon)
        {
            return;
        }
        found = true;
        best = time;
        bestNormal = Normalize(normal);
    }

    private static bool TryFirstQuadraticRoot(
        double a,
        double b,
        double c,
        out double time)
    {
        if (c <= 0)
        {
            time = 0;
            return true;
        }
        if (a <= GeometryEpsilon)
        {
            time = 0;
            return false;
        }
        double discriminant = (b * b) - (4 * a * c);
        if (discriminant < 0)
        {
            time = 0;
            return false;
        }
        double root = (-b - Math.Sqrt(discriminant)) / (2 * a);
        if (root is < 0 or > 1)
        {
            time = 0;
            return false;
        }
        time = root;
        return true;
    }

    private static DVector3 ClosestPointOnTriangle(
        DVector3 point,
        DVector3 a,
        DVector3 b,
        DVector3 c)
    {
        DVector3 ab = b - a;
        DVector3 ac = c - a;
        DVector3 ap = point - a;
        double d1 = Dot(ab, ap);
        double d2 = Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0)
        {
            return a;
        }

        DVector3 bp = point - b;
        double d3 = Dot(ab, bp);
        double d4 = Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3)
        {
            return b;
        }

        double vc = (d1 * d4) - (d3 * d2);
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
        {
            double v = d1 / (d1 - d3);
            return a + (ab * v);
        }

        DVector3 cp = point - c;
        double d5 = Dot(ab, cp);
        double d6 = Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6)
        {
            return c;
        }

        double vb = (d5 * d2) - (d1 * d6);
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
        {
            double w = d2 / (d2 - d6);
            return a + (ac * w);
        }

        double va = (d3 * d6) - (d5 * d4);
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0)
        {
            double w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + ((c - b) * w);
        }

        double denominator = 1 / (va + vb + vc);
        double barycentricV = vb * denominator;
        double barycentricW = vc * denominator;
        return a + (ab * barycentricV) + (ac * barycentricW);
    }

    private static bool PointInTriangle(
        DVector3 point,
        DVector3 a,
        DVector3 b,
        DVector3 c,
        DVector3 normal) =>
        Dot(Cross(b - a, point - a), normal) >= -GeometryEpsilon &&
        Dot(Cross(c - b, point - b), normal) >= -GeometryEpsilon &&
        Dot(Cross(a - c, point - c), normal) >= -GeometryEpsilon;

    private static DVector3 ReadVertex(ReadOnlySpan<int> vertices, int index)
    {
        int offset = index * 3;
        return new DVector3(
            vertices[offset],
            vertices[offset + 1],
            vertices[offset + 2]);
    }

    private static DVector3 ToActorModel(
        Level100Vector3 point,
        Level100Vector3 actorPosition,
        Level100Basis3 basis) =>
        TransposeMultiply(
            basis,
            new DVector3(
                (long)point.X - actorPosition.X,
                (long)point.Y - actorPosition.Y,
                (long)point.Z - actorPosition.Z));

    private static DVector3 TransposeMultiply(
        Level100Basis3 basis,
        DVector3 value) =>
        new(
            ((basis.M00 * value.X) +
             (basis.M10 * value.Y) +
             (basis.M20 * value.Z)) / AxisScale,
            ((basis.M01 * value.X) +
             (basis.M11 * value.Y) +
             (basis.M21 * value.Z)) / AxisScale,
            ((basis.M02 * value.X) +
             (basis.M12 * value.Y) +
             (basis.M22 * value.Z)) / AxisScale);

    private static DVector3 ToWorldVector(
        DVector3 value,
        Level100Basis3 basis) =>
        new(
            ((basis.M00 * value.X) +
             (basis.M01 * value.Y) +
             (basis.M02 * value.Z)) / AxisScale,
            ((basis.M10 * value.X) +
             (basis.M11 * value.Y) +
             (basis.M12 * value.Z)) / AxisScale,
            ((basis.M20 * value.X) +
             (basis.M21 * value.Y) +
             (basis.M22 * value.Z)) / AxisScale);

    private static bool TouchesTerrain(Level100Vector3 center, int radius) =>
        center.Z + radius >= -SampleTerrain(center.X, center.Y);

    private static bool TryFindFirstTerrainContact(
        Level100Vector3 start,
        Level100Vector3 end,
        int lowTime,
        int highTime,
        int contactRadiusMillimeters,
        bool prune,
        out int contactTime)
    {
        Level100Vector3 low = Interpolate(start, end, lowTime);
        Level100Vector3 high = Interpolate(start, end, highTime);
        // Interpolation is monotonic in time on every axis, so the span lies
        // inside its endpoints' box and its lowest point is at an endpoint. If
        // that point clears the highest ground the box can sample, every test
        // below would report no contact, so skipping the span changes nothing.
        if (prune && SpanClearsTerrain(low, high, contactRadiusMillimeters))
        {
            contactTime = 0;
            return false;
        }

        if (TouchesTerrain(low, contactRadiusMillimeters))
        {
            contactTime = lowTime;
            return true;
        }

        bool highTouches = TouchesTerrain(high, contactRadiusMillimeters);
        (int lowX, int lowY) = GetTerrainSampleCoordinates(low);
        (int highX, int highY) = GetTerrainSampleCoordinates(high);
        bool sameSample = lowX == highX && lowY == highY;
        if (!highTouches && sameSample)
        {
            contactTime = 0;
            return false;
        }

        if (highTime - lowTime <= 1)
        {
            if (highTouches ||
                TerrainBoundaryCouldTouch(
                    low,
                    high,
                    lowX,
                    lowY,
                    highX,
                    highY,
                    contactRadiusMillimeters))
            {
                contactTime = highTime;
                return true;
            }

            contactTime = 0;
            return false;
        }

        int middleTime = lowTime + ((highTime - lowTime) / 2);
        if (TryFindFirstTerrainContact(
            start,
            end,
            lowTime,
            middleTime,
            contactRadiusMillimeters,
            prune,
            out contactTime))
        {
            return true;
        }
        return TryFindFirstTerrainContact(
            start,
            end,
            middleTime,
            highTime,
            contactRadiusMillimeters,
            prune,
            out contactTime);
    }

    private static bool SpanClearsTerrain(
        Level100Vector3 low,
        Level100Vector3 high,
        int contactRadiusMillimeters)
    {
        (int lowX, int lowY) = GetTerrainSampleCoordinates(low);
        (int highX, int highY) = GetTerrainSampleCoordinates(high);
        int highestGround = Level100Terrain.Instance.MaximumGroundElevationMillimetersAtFixed(
            Math.Min(lowX, highX),
            Math.Min(lowY, highY),
            Math.Max(lowX, highX),
            Math.Max(lowY, highY));
        long lowestSphereDown = (long)Math.Max(low.Z, high.Z) + contactRadiusMillimeters;
        return lowestSphereDown < -(long)highestGround;
    }

    private static bool TerrainBoundaryCouldTouch(
        Level100Vector3 low,
        Level100Vector3 high,
        int lowX,
        int lowY,
        int highX,
        int highY,
        int contactRadiusMillimeters)
    {
        Level100Terrain terrain = Level100Terrain.Instance;
        int minimumSurfaceDown = int.MaxValue;
        Span<int> xValues = stackalloc int[] { lowX, highX };
        Span<int> yValues = stackalloc int[] { lowY, highY };
        foreach (int x in xValues)
        {
            foreach (int y in yValues)
            {
                minimumSurfaceDown = Math.Min(
                    minimumSurfaceDown,
                    -terrain.SampleGroundElevationMillimetersAtFixed(x, y));
            }
        }

        int maximumSphereDown = checked(
            Math.Max(low.Z, high.Z) + contactRadiusMillimeters);
        return maximumSphereDown >= minimumSurfaceDown;
    }

    private static (int X, int Y) GetTerrainSampleCoordinates(
        Level100Vector3 point) =>
        Level100Terrain.Instance.GetRetailFixedCoordinates(
            new SimVector2(point.X, point.Y));

    private static int SampleTerrain(int x, int y) =>
        Level100Terrain.Instance.SampleGroundElevationMillimeters(
            new SimVector2(x, y));

    private static Level100Vector3 TerrainNormal(int x, int y)
    {
        const int sampleOffset = 1_000;
        int downX0 = -SampleTerrain(x - sampleOffset, y);
        int downX1 = -SampleTerrain(x + sampleOffset, y);
        int downY0 = -SampleTerrain(x, y - sampleOffset);
        int downY1 = -SampleTerrain(x, y + sampleOffset);
        DVector3 normal = Normalize(new DVector3(
            downX1 - downX0,
            downY1 - downY0,
            -(sampleOffset * 2)));
        return new Level100Vector3(
            RoundAway(normal.X * AxisScale),
            RoundAway(normal.Y * AxisScale),
            RoundAway(normal.Z * AxisScale));
    }

    private static Level100Vector3 Interpolate(
        Level100Vector3 start,
        Level100Vector3 end,
        int timePartsPerMillion) =>
        new(
            checked(start.X + DivideRoundNearest(
                ((long)end.X - start.X) * timePartsPerMillion,
                Level100Basis3.Scale)),
            checked(start.Y + DivideRoundNearest(
                ((long)end.Y - start.Y) * timePartsPerMillion,
                Level100Basis3.Scale)),
            checked(start.Z + DivideRoundNearest(
                ((long)end.Z - start.Z) * timePartsPerMillion,
                Level100Basis3.Scale)));

    private static Level100Vector3 Add(
        Level100Vector3 left,
        Level100Vector3 right) =>
        new(
            checked(left.X + right.X),
            checked(left.Y + right.Y),
            checked(left.Z + right.Z));

    private static DVector3 FromInt(Level100Vector3 value) =>
        new(value.X, value.Y, value.Z);

    private static Level100Vector3 RoundVector(DVector3 value) =>
        new(RoundAway(value.X), RoundAway(value.Y), RoundAway(value.Z));

    private static int QuantizeTime(double time) =>
        Math.Clamp(RoundAway(time * Level100Basis3.Scale), 0, Level100Basis3.Scale);

    private static int RoundAway(double value) => checked((int)Math.Round(
        value,
        MidpointRounding.AwayFromZero));

    private static int DivideRoundNearest(long numerator, long denominator)
    {
        long half = denominator / 2;
        return checked((int)(numerator >= 0
            ? (numerator + half) / denominator
            : (numerator - half) / denominator));
    }

    private static double Dot(DVector3 left, DVector3 right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static DVector3 Cross(DVector3 left, DVector3 right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static double LengthSquared(DVector3 value) => Dot(value, value);

    private static double Length(DVector3 value) => Math.Sqrt(LengthSquared(value));

    private static DVector3 Normalize(DVector3 value)
    {
        double length = Length(value);
        return length <= GeometryEpsilon
            ? new DVector3(0, 0, -1)
            : value / length;
    }

    private readonly record struct DVector3(double X, double Y, double Z)
    {
        public static DVector3 operator +(DVector3 left, DVector3 right) =>
            new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static DVector3 operator -(DVector3 left, DVector3 right) =>
            new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static DVector3 operator *(DVector3 value, double scale) =>
            new(value.X * scale, value.Y * scale, value.Z * scale);

        public static DVector3 operator /(DVector3 value, double scale) =>
            new(value.X / scale, value.Y / scale, value.Z / scale);
    }
}
