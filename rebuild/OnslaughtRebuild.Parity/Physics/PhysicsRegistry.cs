// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.h (UPhysicsManager's registries, Add*/Get* lookups, CFeatureData,
// CHazardData), src/CPhysicsScript.cpp (CPhysicsScript::Process 0042ea60) and src/CPhysicsScriptStatements.h (each
// statement's Execute and the unit, feature and hazard values' Load and Apply). Not in Stuart's release.

using System.Buffers.Binary;
using System.Text;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Physics;

/// <summary>CFeatureData.</summary>
public sealed class FeatureData(byte[] name)
{
    public byte[] Name { get; } = name;
    public byte[]? MeshName;
    public int Explosion = -1;
    public byte[]? SoundName;
    public int Invincible;
    public int Lockable;                // mUnknown14
    public float Life;
    public int Icon = -1;
}

/// <summary>CHazardData.</summary>
public sealed class HazardData(byte[] name)
{
    public byte[] Name { get; } = name;
    public byte[]? ParticleName;
    public byte[]? SoundName;
    public float Radius = 1.0f;
    public float Damage;
}

/// <summary>
/// UPhysicsManager's definition registries, filled by running the physics script's statements in order
/// (CPhysicsScript::Process). Each statement appends a new definition (even when the name exists), then applies its
/// values to the FIRST definition of that name, as the lookups return the first match. Positions are append order.
/// </summary>
public sealed partial class PhysicsRegistry
{
    public List<UnitData> Units { get; } = [];
    public List<UnitData> Components { get; } = [];
    public List<FeatureData> Features { get; } = [];
    public List<HazardData> Hazards { get; } = [];

    /// <summary>Values whose ids no Create*Value knows (retail skips their bytes).</summary>
    public int SkippedValues { get; private set; }

    public static PhysicsRegistry Build(PhysicsScript script)
    {
        var r = new PhysicsRegistry();
        foreach (PhysicsStatement s in script.Statements)
        {
            r.Execute(s);
        }

        return r;
    }

    private void Execute(PhysicsStatement s)
    {
        switch (s.Type)
        {
            case PhysicsStatementType.Unit:
                Units.Add(new UnitData(s.Name));
                ApplyAll(s, (v, body) => ApplyUnitValue(FindFirst(Units, s.Name, u => u.Name), v, body));
                break;
            case PhysicsStatementType.Feature:
                Features.Add(new FeatureData(s.Name));
                ApplyAll(s, (v, body) => ApplyFeatureValue(FindFirst(Features, s.Name, f => f.Name), v, body));
                break;
            case PhysicsStatementType.Hazard:
                Hazards.Add(new HazardData(s.Name));
                ApplyAll(s, (v, body) => ApplyHazardValue(FindFirst(Hazards, s.Name, h => h.Name), v, body));
                break;
            default:
                ExecuteOther(s);
                break;
        }
    }

    /// <summary>The families of PhysicsRegistry.Families.cs (weapons, weapon modes, rounds, spawners, explosions,
    /// components).</summary>
    partial void ExecuteOther(PhysicsStatement s);

    private void ApplyAll(PhysicsStatement s, Func<int, byte[], bool> apply)
    {
        foreach (PhysicsValue v in s.Values)
        {
            if (!apply(v.Type, v.Body))
            {
                SkippedValues++;
            }
        }
    }

    public UnitData? GetUnitData(byte[] name) => FindFirst(Units, name, u => u.Name);

    public int GetUnitIndex(byte[] name) => IndexOf(Units, name, u => u.Name);

    public UnitData? GetComponentData(byte[] name) => FindFirst(Components, name, c => c.Name);

    public int GetComponentIndex(byte[] name) => IndexOf(Components, name, c => c.Name);

    public FeatureData? GetFeatureData(byte[] name) => FindFirst(Features, name, f => f.Name);

    public int GetFeatureIndex(byte[] name) => IndexOf(Features, name, f => f.Name);

    public HazardData? GetHazardData(byte[] name) => FindFirst(Hazards, name, h => h.Name);

    public int GetHazardIndex(byte[] name) => IndexOf(Hazards, name, h => h.Name);

    internal static T? FindFirst<T>(List<T> list, byte[] name, Func<T, byte[]> key)
        where T : class
    {
        foreach (T item in list)
        {
            if (key(item).AsSpan().SequenceEqual(name))
            {
                return item;
            }
        }

        return null;
    }

    internal static int IndexOf<T>(List<T> list, byte[] name, Func<T, byte[]> key)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (key(list[i]).AsSpan().SequenceEqual(name))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A value body reader that checks the value's Load consumed exactly its stored size.</summary>
    internal ref struct ValueReader(byte[] body)
    {
        private readonly byte[] _body = body;
        private int _at;

        public int Int()
        {
            int v = BinaryPrimitives.ReadInt32LittleEndian(_body.AsSpan(_at));
            _at += 4;
            return v;
        }

        public float Float()
        {
            float v = BinaryPrimitives.ReadSingleLittleEndian(_body.AsSpan(_at));
            _at += 4;
            return v;
        }

        /// <summary>PhysicsReadString: bytes up to and including a zero, returned without it.</summary>
        public byte[] String()
        {
            int end = Array.IndexOf(_body, (byte)0, _at);
            if (end < 0)
            {
                throw new InvalidDataException("unterminated physics string");
            }

            byte[] s = _body[_at..end];
            _at = end + 1;
            return s;
        }

        public readonly void End()
        {
            if (_at != _body.Length)
            {
                throw new InvalidDataException($"a physics value read {_at} of its {_body.Length} bytes");
            }
        }
    }

    /// <summary>CreateBehaviourType's keyword ids and the EUnitBehaviour each GetValue returns (id 2 is the jeep).</summary>
    internal static UnitBehaviour? BehaviourFor(int keyword) => keyword switch
    {
        1 => UnitBehaviour.Mech,
        2 => UnitBehaviour.Jeep,
        3 => UnitBehaviour.GroundUnit,
        4 => UnitBehaviour.Infantry,
        5 => UnitBehaviour.Turret,
        6 => UnitBehaviour.Boat,
        7 => UnitBehaviour.Carrier,
        8 => UnitBehaviour.Building,
        9 => UnitBehaviour.Fighter,
        10 => UnitBehaviour.Bomber,
        11 => UnitBehaviour.GroundAttackAircraft,
        12 => UnitBehaviour.Dropship,
        13 => UnitBehaviour.Mine,
        14 => UnitBehaviour.HiveBoss,
        15 => UnitBehaviour.Submarine,
        16 => UnitBehaviour.DiveBomber,
        17 => UnitBehaviour.ThunderHead,
        18 => UnitBehaviour.Carver,
        19 => UnitBehaviour.GillM,
        20 => UnitBehaviour.Sentinel,
        21 => UnitBehaviour.Warspite,
        22 => UnitBehaviour.Fenrir,
        23 => UnitBehaviour.WarspiteDome,
        24 => UnitBehaviour.Pod,
        25 => UnitBehaviour.SimpleBuilding,
        _ => null,
    };

    /// <summary>CreateNavMapType: water 0, flat 1, steep 2, mountainous 3.</summary>
    internal static int? NavMapFor(int keyword) => keyword is >= 1 and <= 4 ? keyword - 1 : null;

    /// <summary>The explosion position by name (GetExplosionIndex), from PhysicsRegistry.Families.cs.</summary>
    public int GetExplosionIndex(byte[] name) => ExplosionIndex(name);

    private partial int ExplosionIndex(byte[] name);

    public int GetWeaponIndex(byte[] name) => WeaponIndex(name);

    private partial int WeaponIndex(byte[] name);

    public int GetSpawnerIndex(byte[] name) => SpawnerIndex(name);

    private partial int SpawnerIndex(byte[] name);

    /// <summary>CreateUnitValue's classes, Load then Apply. Returns false for an id with no class (skipped).</summary>
    private bool ApplyUnitValue(UnitData? unit, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1 or 2: { float v = r.Float(); r.End(); if (unit is not null) unit.Velocity = v; return true; }
            case 3: { float v = r.Float(); r.End(); if (unit is not null) unit.Life = v; return true; }
            case 4: { float v = r.Float(); r.End(); if (unit is not null) unit.AirShields = v; return true; }
            case 5 or 6: { float v = r.Float(); r.End(); if (unit is not null) unit.TurnRate = v; return true; }
            case 7:
            {
                byte[] weapon = r.String(); byte[] mount = r.String(); int a = r.Int(); r.End();
                unit?.Weapons.Add(new UnitWeaponReference(GetWeaponIndex(weapon), a, UnitData.MapMountName(mount)));
                return true;
            }
            case 8:
            {
                int keyword = r.Int(); r.End();
                UnitBehaviour? b = BehaviourFor(keyword);
                if (unit is not null && b is { } behaviour)
                {
                    unit.Behaviour = behaviour;
                    switch (behaviour)
                    {
                        case UnitBehaviour.Infantry: unit.NavMap = 3; break;
                        case UnitBehaviour.Mech: unit.NavMap = 2; break;
                        case UnitBehaviour.GroundUnit: unit.NavMap = 1; break;
                        case UnitBehaviour.Boat or UnitBehaviour.Submarine: unit.NavMap = 0; break;
                        case UnitBehaviour.GillM: unit.NavMap = 4; break;
                    }
                }

                return true;
            }
            case 9: { byte[] v = r.String(); r.End(); unit?.SetMesh(v); return true; }
            case 10: { byte[] v = r.String(); r.End(); if (unit is not null) unit.Explosion = GetExplosionIndex(v); return true; }
            case 11: { r.Int(); r.End(); return true; } // Alligence: Apply does nothing
            case 12: return UnitString(unit, ref r, 2);
            case 13: return UnitString(unit, ref r, 3);
            case 14: return UnitString(unit, ref r, 4);
            case 15: { float v = r.Float(); r.End(); if (unit is not null) unit.GroundShields = v; return true; }
            case 16: { float v = r.Float(); r.End(); if (unit is not null) unit.AirShieldRegeneration = v; return true; }
            case 17: { float v = r.Float(); r.End(); if (unit is not null) unit.GroundShieldRegeneration = v; return true; }
            case 18:
            {
                byte[] spawner = r.String(); byte[] mount = r.String(); int a = r.Int(); r.End();
                unit?.Spawners.Add(new UnitWeaponReference(GetSpawnerIndex(spawner), a, UnitData.MapMountName(mount)));
                return true;
            }
            case 19: { float v = r.Float(); r.End(); if (unit is not null) unit.Threat = v; return true; }
            case 20: { float v = r.Float(); r.End(); if (unit is not null) unit.Importance = v; return true; }
            case 21: { float v = r.Float(); r.End(); if (unit is not null) unit.SoundMaterial = X87.Fist(v); return true; }
            case 22: { float v = r.Float(); r.End(); if (unit is not null) unit.StrafeChange = v; return true; }
            case 23: { float v = r.Float(); r.End(); if (unit is not null) unit.MaxTargetRange = v; return true; }
            case 24: return UnitString(unit, ref r, 0);
            case 25:
            {
                int keyword = r.Int(); r.End();
                if (unit is not null && NavMapFor(keyword) is { } nav)
                {
                    unit.NavMap = nav;
                }

                return true;
            }
            case 26: return UnitFlag(ref r, v => { if (unit is not null) unit.MultipleSpawn = v; });
            case 27:
            {
                byte[] name = r.String(); int a = r.Int(); r.End();
                unit?.Components.Add(new UnitComponentReference(GetComponentIndex(name), a));
                return true;
            }
            case 28: { float v = r.Float(); r.End(); if (unit is not null) unit.Stealth = v; return true; }
            case 29:
            {
                float v = r.Float(); int index = r.Int(); r.End();
                if (unit is not null) unit.AttackPriority[index] = v;
                return true;
            }
            case 30: { float v = r.Float(); r.End(); if (unit is not null) unit.BodyMotion = v; return true; }
            case 31: { float v = r.Float(); r.End(); if (unit is not null) unit.LegSpeed = v; return true; }
            case 32: { float v = r.Float(); r.End(); if (unit is not null) unit.LegPlacementArea = v; return true; }
            case 33: { float v = r.Float(); r.End(); if (unit is not null) unit.DetonationDelay = v; return true; }
            case 34: { float v = r.Float(); r.End(); if (unit is not null) unit.BounceFactor = v; return true; }
            case 35: return UnitString(unit, ref r, 9);
            case 36: return UnitString(unit, ref r, 5); // Engines sets the dust effect, as retail does
            case 37: return UnitString(unit, ref r, 5);
            case 38: return UnitString(unit, ref r, 7);
            case 39: { byte[] v = r.String(); r.End(); unit?.CopyFrom(GetUnitData(v)); return true; }
            case 40: { float v = r.Float(); r.End(); if (unit is not null) unit.MinThrusterHeight = v; return true; }
            case 41: { float v = r.Float(); r.End(); if (unit is not null) unit.MaxThrusterHeight = v; return true; }
            case 42: { float v = r.Float(); r.End(); if (unit is not null) unit.MinAltitude = v; return true; }
            case 43: { int v = r.Int(); r.End(); if (unit is not null) unit.BuildingNumber = v; return true; }
            case 44: { int v = r.Int(); r.End(); if (unit is not null) unit.BuildingMeshType = v; return true; }
            case 45: { int v = r.Int(); r.End(); if (unit is not null) unit.Parachute = v; return true; }
            case 46: { int v = r.Int(); r.End(); if (unit is not null) unit.KnockTrees = v; return true; }
            case 47: { float v = r.Float(); r.End(); if (unit is not null) unit.TurretTurnRate = v; return true; }
            case 48: { float v = r.Float(); r.End(); if (unit is not null) unit.Hover = v > 0.0f ? 1 : 0; return true; }
            case 50: return UnitFlag(ref r, v => { if (unit is not null) unit.Deploy = v; });
            case 51: return UnitString(unit, ref r, 1);
            case 52: return UnitFlag(ref r, v => { if (unit is not null) unit.Retreat = v; });
            case 53: return UnitFlag(ref r, v => { if (unit is not null) unit.Wingman = v; });
            case 54: return UnitString(unit, ref r, 11);
            case 55: return UnitString(unit, ref r, 8);
            case 56: return UnitFlag(ref r, v => { if (unit is not null) unit.Big = v; });
            case 57: { byte[] v = r.String(); r.End(); if (unit is not null) unit.SmallExplosion = GetExplosionIndex(v); return true; }
            case 58: { byte[] v = r.String(); r.End(); if (unit is not null) unit.StompExplosion = GetExplosionIndex(v); return true; }
            case 59: { float v = r.Float(); r.End(); if (unit is not null) unit.DeployDamageMultiplier = v; return true; }
            case 60: { float v = r.Float(); r.End(); if (unit is not null) unit.StandingLegPlacementArea = v; return true; }
            case 61: { float v = r.Float(); r.End(); if (unit is not null) unit.MaxLegsLifted = X87.Fist(v); return true; }
            case 62: return UnitFlag(ref r, v => { if (unit is not null) unit.Indiscriminate = v; });
            case 63: return UnitString(unit, ref r, 12);
            case 64: return UnitString(unit, ref r, 10);
            case 65: return UnitFlag(ref r, v => { if (unit is not null) unit.Shatter = v; });
            case 66: return UnitFlag(ref r, v => { if (unit is not null) unit.CanBeLocked = v; });
            case 67: return UnitFlag(ref r, v => { if (unit is not null) unit.Sweeping = v; });
            case 68: return UnitFlag(ref r, v => { if (unit is not null) unit.IgnoreThreats = v; });
            case 69: return UnitFlag(ref r, v => { if (unit is not null) unit.AnimatedCollision = v; });
            case 70: { int v = r.Int(); r.End(); if (unit is not null) unit.Icon = v; return true; }
            default: return false;
        }
    }

    private static bool UnitString(UnitData? unit, ref ValueReader r, int slot)
    {
        byte[] v = r.String();
        r.End();
        unit?.SetEffectName(slot, v);
        return true;
    }

    /// <summary>The SINT flags whose Apply stores TRUE for any nonzero value.</summary>
    private static bool UnitFlag(ref ValueReader r, Action<int> set)
    {
        int v = r.Int();
        r.End();
        set(v != 0 ? 1 : 0);
        return true;
    }

    private bool ApplyFeatureValue(FeatureData? f, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { float v = r.Float(); r.End(); if (f is not null) f.Life = v; return true; }
            case 2: { byte[] v = r.String(); r.End(); if (f is not null) f.MeshName = v; return true; }
            case 3: { byte[] v = r.String(); r.End(); if (f is not null) f.Explosion = GetExplosionIndex(v); return true; }
            case 4: { float v = r.Float(); r.End(); if (f is not null) f.Invincible = v != 0.0f ? 1 : 0; return true; }
            case 5: { byte[] v = r.String(); r.End(); if (f is not null) f.SoundName = v; return true; }
            case 6: { float v = r.Float(); r.End(); if (f is not null) f.Lockable = v != 0.0f ? 1 : 0; return true; }
            case 7: { int v = r.Int(); r.End(); if (f is not null) f.Icon = v; return true; }
            default: return false;
        }
    }

    private static bool ApplyHazardValue(HazardData? h, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { float v = r.Float(); r.End(); if (h is not null) h.Radius = v; return true; }
            case 2: { byte[] v = r.String(); r.End(); if (h is not null) h.ParticleName = v; return true; }
            case 3: { float v = r.Float(); r.End(); if (h is not null) h.Damage = v; return true; }
            case 4: { byte[] v = r.String(); r.End(); if (h is not null) h.SoundName = v; return true; }
            default: return false;
        }
    }
}
