// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/InitThing.h (CInitCSThing, CInitThing and its subclasses' runtime fields; Copy), in
// Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>ECollisionLevel.</summary>
public enum CollisionLevel
{
    OuterSphere = 0,
    ApproxGeometryShapes = 1,
    Mesh = 2,
}

/// <summary>EOrientationType.</summary>
public enum OrientationType
{
    EulerAngles = 0,
    DirectionCosineMatrix = 1,
}

/// <summary>CInitCSThing: how a thing's collision-seeking part is set up (Initialise's defaults).</summary>
public sealed class InitCollision
{
    public Thing? ForThing;
    public Thing? IgnoreThing;
    public uint NotSeekCollisionWith;
    public CollisionLevel DesiredCollision = CollisionLevel.ApproxGeometryShapes;
    public CollisionLevel MinCollision = CollisionLevel.OuterSphere;
    public CollisionLevel MaxCollision = CollisionLevel.Mesh;
    public int CollisionResponse = 2;
    public bool StartCollideOnNextFrame = true;
    public bool UseFixedMeshTransforms;
    public bool DoObbForMeshCollision;
    public float TimeBeforeStart = -1;
    public bool IgnoreAnimationCollision;
    public float RoundLength;

    /// <summary>mShape: the approximate volume to use (null: a sphere of the bounding radius).</summary>
    public Collision.GeneralVolume? Shape;

    /// <summary>CInitCSThing::Initialise: back to the defaults.</summary>
    public void Initialise()
    {
        ForThing = null;
        NotSeekCollisionWith = 0;
        DesiredCollision = CollisionLevel.ApproxGeometryShapes;
        MinCollision = CollisionLevel.OuterSphere;
        MaxCollision = CollisionLevel.Mesh;
        CollisionResponse = 2;
        StartCollideOnNextFrame = true;
        TimeBeforeStart = -1;
        IgnoreThing = null;
        DoObbForMeshCollision = false;
        UseFixedMeshTransforms = false;
        IgnoreAnimationCollision = false;
        RoundLength = 0.0f;
    }
}

/// <summary>
/// CInitThing and the subclasses' fields a thing's Init reads: one class with the union of the fields, its kind
/// recording which subclass SpawnInitThing made. Built from a world file's <see cref="InitThingRecord"/>, or by code.
/// </summary>
public sealed class InitThing
{
    public InitThingRecord.InitKind Kind;
    public FVector Pos;
    public FMatrix Orientation = FMatrix.Identity;
    public float Yaw;
    public float Pitch;
    public float Roll;
    public FVector Velocity;
    public OrientationType OrientationType = OrientationType.EulerAngles;
    public int MeshNo;
    public InitCollision InitCst = new();
    public int Allegiance = 2;
    public int Target;

    /// <summary>mTarget once CWorld::LoadWorld has turned the stored index into the thing it names (world.cpp:773).</summary>
    public Thing? TargetThing;
    public float ForceRadius = -1;
    public byte[] Script = [];
    public byte[] Name = [];
    public byte[] SpawnScript = [];
    public int Active = 1;
    public int AttachScriptsToUnits;
    public Thing? SpawnedBy;
    public int WaypointPath;

    // CUnitInitThing
    public UnitData? Stats;

    // CTreeInitThing
    public bool TreeTypeComplete = true;
    public byte[] TreeType = "DefaultTree0"u8.ToArray();

    // CSquadInitThing
    public int Amount;
    public byte[] UnitName = [];
    public int Mode;

    // CStartInitThing (PlaneMode and PlayerNumber) and CBattleEngineInitThing (ConfigurationId and PlaneMode)
    public int ConfigurationId;
    public int PlaneMode;
    public int PlayerNumber = 1;

    // CRoundInitThing
    public FVector Dest = new(-1, -1, -1);
    public int JumpsPerformed;
    public RoundData? RoundData;
    public float InitialDelay;
    public float LifeSpan;

    // CExplosionInitThing
    public ExplosionData? Behaviour;
    public int ColType;
    public Thing? AttachedTo;
    public bool UseAttachedRadius;
    public bool AllowVolumetric;
    public bool Important;
    public Thing? Originator;

    // CSphereTriggerInitThing
    public float Radius;

    // CFeatureInitThing / CHazardInitThing
    public FeatureData? FeatureData;
    public HazardData? HazardData;

    /// <summary>The thing a world file's record describes (SpawnInitThing then Load).</summary>
    public static InitThing FromRecord(InitThingRecord r) => new()
    {
        Kind = r.Kind,
        Pos = r.Position,
        Yaw = r.Yaw,
        Pitch = r.Pitch,
        Roll = r.Roll,
        MeshNo = r.MeshNo,
        Allegiance = r.Allegiance,
        Target = r.Target,
        Script = r.Script,
        Name = r.Name,
        SpawnScript = r.SpawnScript,
        Active = r.Active,
        AttachScriptsToUnits = r.AttachScriptsToUnits,
        Amount = r.Kind == InitThingRecord.InitKind.Squad ? r.Amount : 0,
        Mode = r.Mode,
        TreeType = r.TreeType,
        PlaneMode = r.PlaneMode,
        PlayerNumber = r.PlayerNumber,
        Radius = r.Radius,
    };

    /// <summary>CInitThing::Copy: the base fields of another init.</summary>
    public void Copy(InitThing from)
    {
        Pos = from.Pos;
        Orientation = from.Orientation;
        Yaw = from.Yaw;
        Pitch = from.Pitch;
        Roll = from.Roll;
        OrientationType = from.OrientationType;
        MeshNo = from.MeshNo;
        Allegiance = from.Allegiance;
        Target = from.Target;
        Script = from.Script;
        Name = from.Name;
        SpawnScript = from.SpawnScript;
        Active = from.Active;
        AttachScriptsToUnits = from.AttachScriptsToUnits;
    }
}
