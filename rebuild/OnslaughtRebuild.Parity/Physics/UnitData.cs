// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/WorldPhysicsManager.h (CUnitData: the constructor, Init 0042efd0, the setters, CopyFrom
// 00433390, EUnitBehaviour) and src/WorldPhysicsManager.cpp (SetExplosion 00511720, SetSmallExplosion 005117c0,
// SetStompExplosion 00511860, AddComponent 00511900, AddWeapon 005119e0, AddSpawner 00511ad0, MapMountName 005115b0).
// Not in Stuart's release.

using System.Text;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>EUnitBehaviour: the unit classes the physics script's behaviour keywords select (CUnitData +0xe0).</summary>
public enum UnitBehaviour
{
    Mech = 0,
    Aircraft,
    GroundUnit,
    Infantry,
    Turret,
    Boat,
    Carrier,
    Building,
    Fighter,
    Bomber,
    GroundAttackAircraft,
    Jeep,
    Dropship,
    Mine,
    HiveBoss,
    Submarine,
    DiveBomber,
    ThunderHead,
    Carver,
    GillM,
    Sentinel,
    Warspite,
    Fenrir,
    WarspiteDome,
    Pod,
    SimpleBuilding,
}

/// <summary>CUnitWeapon / CSpawnerReference: a definition by its registry position, a number and a mount.</summary>
public sealed record UnitWeaponReference(int Index, int Unknown04, int Mount);

/// <summary>CUnitComponentReference.</summary>
public sealed record UnitComponentReference(int Index, int Unknown04);

/// <summary>
/// CUnitData: a unit or component definition. Field names follow the decompilation's, with the physics-script keyword
/// in the comment where the field is still an unknown there. Strings are kept as the bytes the script stored.
/// </summary>
public sealed class UnitData
{
    /// <summary>
    /// mEffectNames: 0 shield effect, 1 activation effect, 2 trails, 3 thrusters, 4 smoke, 5 dust (also Engines),
    /// 6 the effect at +0x94 (only CopyFrom sets it), 7 vent, 8 afterburner, 9 water wake, 10 ground wake, 11 noise,
    /// 12 spawn noise.
    /// </summary>
    public readonly byte[]?[] EffectNames = new byte[]?[13];

    public readonly List<UnitWeaponReference> Weapons = [];
    public readonly List<UnitWeaponReference> Spawners = [];
    public readonly List<UnitComponentReference> Components = [];
    public readonly float[] AttackPriority = new float[7];

    public UnitData(byte[] name)
    {
        Name = name;
        Init();
        string text = Encoding.ASCII.GetString(name);
        if (string.Equals(text, "Fenrir Main Gun", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "Fenrir", StringComparison.OrdinalIgnoreCase))
        {
            Fenrir = 1;
        }
    }

    public byte[] Name { get; }

    public string NameText => Encoding.ASCII.GetString(Name);

    /// <summary>mUnknown2c: the mesh name (Mesh).</summary>
    public byte[]? Mesh;

    /// <summary>mUnknown30: the rubble name ("m-b-rubble" by default).</summary>
    public byte[]? Rubble;

    public float Velocity;              // mUnknownB4
    public float TurnRate;              // mTurnRate
    public float TurretTurnRate;        // mUnknownBC
    public float Life;                  // mUnknownC0
    public float AirShields;
    public float GroundShields;
    public float AirShieldRegeneration;
    public float GroundShieldRegeneration;
    public float Stealth;               // mUnknownD4
    public float MaxYaw;
    public float MaxTurretYaw;          // mUnknownDC
    public UnitBehaviour Behaviour;
    public int SoundMaterial;
    public int Explosion;
    public int SmallExplosion;
    public int StompExplosion;
    public float Threat;
    public float Importance;
    public int NavMap;                  // mUnknownFC
    public int Parachute;
    public int KnockTrees;              // mUnknown104
    public int Deploy;                  // mUnknown108
    public int Hover;                   // mUnknown10C
    public int MultipleSpawn;           // mUnknown110
    public int CanBeLocked;             // mCanBeLocked
    public int Unknown118;
    public int Retreat;                 // mUnknown11C
    public int Wingman;                 // mUnknown120
    public int Big;                     // mUnknown124
    public int Indiscriminate;          // mUnknown128
    public int Tentacle;                // mUnknown12C
    public int Shatter;
    public int PassOnHitEffect;         // mUnknown134
    public int IgnoreThreats;
    public int AnimatedCollision;       // mUnknown13C
    public int MaxLegsLifted;
    public float BodyMotion;
    public float LegSpeed;
    public float LegPlacementArea;
    public float StandingLegPlacementArea;
    public float BounceFactor;
    public float MaxTargetRange;
    public float MinAltitude;
    public float DeployDamageMultiplier; // mUnknown160
    public float StrafeChange;
    public float DetonationDelay;
    public float MinThrusterHeight;
    public float MaxThrusterHeight;
    public int BuildingNumber;
    public int BuildingMeshType;
    public int Detach;                  // mUnknown198
    public int Sweeping;
    public int Unknown1A0;
    public int Fenrir;
    public int Icon;

    /// <summary>The CUnitWeapon and CSpawnerReference records CopyFrom allocates and never adds (retail leaks them).</summary>
    public int LeakedCopies { get; private set; }

    /// <summary>CUnitData::Init: the defaults.</summary>
    private void Init()
    {
        Behaviour = (UnitBehaviour)3;
        NavMap = 3;
        TurnRate = 0;
        Velocity = 0;
        Mesh = null;
        Rubble = Encoding.ASCII.GetBytes("m-b-rubble");
        Life = 0;
        AirShields = 0;
        GroundShields = 0;
        Stealth = 0;
        AirShieldRegeneration = 0;
        GroundShieldRegeneration = 0;
        Threat = 1.0f;
        Importance = 1.0f;
        BodyMotion = 0;
        LegSpeed = 200.0f;
        LegPlacementArea = 1.0f;
        StandingLegPlacementArea = 0.05f;
        MaxLegsLifted = 2;
        Parachute = 0;
        KnockTrees = 0;
        Deploy = 0;
        DeployDamageMultiplier = 1.0f;
        MultipleSpawn = 0;
        BounceFactor = 0;
        SoundMaterial = 0;
        MinThrusterHeight = 3.0f;
        MaxThrusterHeight = 12.0f;
        MaxTargetRange = 1000.0f;
        MinAltitude = 4.0f;
        Explosion = -1;
        SmallExplosion = -1;
        StompExplosion = -1;
        MaxYaw = MathF.PI * 2;
        MaxTurretYaw = MathF.PI * 2;
        Unknown118 = 0;
        Retreat = 0;
        Wingman = 0;
        Big = 0;
        Array.Fill(AttackPriority, 1.0f);
        BuildingNumber = 0;
        BuildingMeshType = 0;
        StrafeChange = 0;
        TurretTurnRate = 0;
        DetonationDelay = 0;
        Hover = 0;
        Indiscriminate = 0;
        Tentacle = 0;
        Shatter = 0;
        Detach = 0;
        Sweeping = 0;
        PassOnHitEffect = 0;
        IgnoreThreats = 0;
        AnimatedCollision = 0;
        Unknown1A0 = 0;
        Fenrir = 0;
        Icon = -1;
        CanBeLocked = 1;
    }

    /// <summary>The string setters: a null name leaves the field as it was.</summary>
    public void SetEffectName(int slot, byte[]? name)
    {
        if (name is not null)
        {
            EffectNames[slot] = name;
        }
    }

    public void SetMesh(byte[]? name) => Mesh = name ?? Mesh;

    public void SetRubble(byte[]? name) => Rubble = name ?? Rubble;

    /// <summary>CopyFrom: a BasedOn copy of another definition, in retail's order and with its quirks.</summary>
    public void CopyFrom(UnitData? other)
    {
        if (other is null)
        {
            return;
        }

        Behaviour = other.Behaviour;
        SetMesh(other.Mesh);
        SetRubble(other.Rubble);
        SetEffectName(11, other.EffectNames[11]);
        SetEffectName(12, other.EffectNames[12]);
        SetEffectName(0, other.EffectNames[0]);
        Explosion = other.Explosion;
        SmallExplosion = other.SmallExplosion;
        StompExplosion = other.StompExplosion;
        Life = other.Life;
        AirShields = other.AirShields;
        GroundShields = other.GroundShields;
        Stealth = other.Stealth;
        AirShieldRegeneration = other.AirShieldRegeneration;
        GroundShieldRegeneration = other.GroundShieldRegeneration;
        Threat = other.Threat;
        Importance = other.Importance;
        Parachute = other.Parachute;
        KnockTrees = other.KnockTrees;
        MultipleSpawn = other.MultipleSpawn;
        Deploy = other.Deploy;
        DeployDamageMultiplier = other.DeployDamageMultiplier;
        BodyMotion = other.BodyMotion;
        LegSpeed = other.LegSpeed;
        LegPlacementArea = other.LegPlacementArea;
        StandingLegPlacementArea = other.StandingLegPlacementArea;
        MaxLegsLifted = other.MaxLegsLifted;
        Unknown118 = other.Unknown118;
        Retreat = other.Retreat;
        Wingman = other.Wingman;
        Big = 0;
        BounceFactor = other.BounceFactor;
        SoundMaterial = other.SoundMaterial;
        Hover = other.Hover;
        MinThrusterHeight = other.MinThrusterHeight;
        MaxThrusterHeight = other.MaxThrusterHeight;
        MaxTargetRange = other.MaxTargetRange;
        MinAltitude = other.MinAltitude;
        TurretTurnRate = other.TurretTurnRate;
        MaxYaw = other.MaxYaw;
        MaxTurretYaw = other.MaxTurretYaw;
        StrafeChange = other.StrafeChange;
        DetonationDelay = other.DetonationDelay;
        Icon = other.Icon;
        SetEffectName(2, other.EffectNames[2]);
        SetEffectName(3, other.EffectNames[3]);
        SetEffectName(4, other.EffectNames[4]);
        SetEffectName(6, other.EffectNames[6]);
        SetEffectName(7, other.EffectNames[7]);
        SetEffectName(5, other.EffectNames[5]);
        SetEffectName(1, other.EffectNames[1]);
        SetEffectName(8, other.EffectNames[8]);
        SetEffectName(9, other.EffectNames[9]);
        SetEffectName(10, other.EffectNames[10]);
        Indiscriminate = other.Indiscriminate;
        Tentacle = other.Tentacle;
        Shatter = other.Shatter;
        Detach = other.Detach;
        CanBeLocked = other.CanBeLocked;
        Sweeping = other.Sweeping;
        PassOnHitEffect = other.PassOnHitEffect;
        IgnoreThreats = other.IgnoreThreats;
        AnimatedCollision = other.AnimatedCollision;
        Unknown1A0 = other.Unknown1A0;

        // Retail allocates copies of the weapons and spawners but never adds them; its spawner copies read the finished
        // weapon loop's pointer, which is NULL, so a source with spawners would fault there.
        LeakedCopies += other.Weapons.Count;
        if (other.Spawners.Count > 0)
        {
            throw new InvalidOperationException(
                $"BasedOn '{other.NameText}' has spawners: retail's CopyFrom reads a NULL weapon pointer here");
        }

        foreach (UnitComponentReference c in other.Components)
        {
            Components.Add(new UnitComponentReference(c.Index, c.Unknown04));
        }

        Array.Copy(other.AttackPriority, AttackPriority, 7);
    }

    /// <summary>UPhysicsManager::MapMountName: GunA..GunI are 1..9, SpawnerA..SpawnerE 10..14, anything else 0.</summary>
    public static int MapMountName(byte[] name)
    {
        string text = Encoding.ASCII.GetString(name);
        string[] mounts = ["GunA", "GunB", "GunC", "GunD", "GunE", "GunF", "GunG", "GunH", "GunI",
            "SpawnerA", "SpawnerB", "SpawnerC", "SpawnerD", "SpawnerE"];
        for (int i = 0; i < mounts.Length; i++)
        {
            if (string.Equals(text, mounts[i], StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }

        return 0;
    }

    public override string ToString() => $"{NameText} ({Behaviour})";
}
