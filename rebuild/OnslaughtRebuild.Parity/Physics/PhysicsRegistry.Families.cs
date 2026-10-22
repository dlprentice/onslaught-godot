// SPDX-License-Identifier: GPL-3.0-only
// The physics script's weapon, weapon mode, round, spawner, explosion and component statements. Adapted from bea-decomp
// src/CPhysicsScriptStatements.h (each family's value classes: Load and Apply), src/CPhysicsScriptStatements.cpp
// (CreateWeaponValue 00434300, CreateWeaponModeValue 00435010, CreateRoundValue 00437490, CreateSpawnerValue 00439b40,
// CreateExplosionValue 0043a860, CreateComponentValue 0043c500, CreateSeekType 0043dcd0, CreateStateType 0043e540,
// CWeaponLanguageName::Apply 0043e660) and src/WorldPhysicsManager.h (UPhysicsManager's registries and lookups). Not in
// Stuart's release.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Physics;

public sealed partial class PhysicsRegistry
{
    public List<WeaponData> Weapons { get; } = [];
    public List<WeaponModeData> WeaponModes { get; } = [];
    public List<RoundData> Rounds { get; } = [];
    public List<SpawnerData> SpawnerDefinitions { get; } = [];
    public List<ExplosionData> Explosions { get; } = [];

    partial void ExecuteOther(PhysicsStatement s)
    {
        // Each statement appends its definition, then applies its values by the statement's name: the lookups return the
        // first definition with that name.
        switch (s.Type)
        {
            case PhysicsStatementType.Weapon:
                Weapons.Add(new WeaponData(s.Name));
                ApplyAll(s, (v, body) => ApplyWeaponValue(s.Name, v, body));
                break;
            case PhysicsStatementType.WeaponMode:
                WeaponModes.Add(new WeaponModeData(s.Name));
                ApplyAll(s, (v, body) => ApplyWeaponModeValue(s.Name, v, body));
                break;
            case PhysicsStatementType.Round:
                Rounds.Add(new RoundData(s.Name));
                ApplyAll(s, (v, body) => ApplyRoundValue(GetRoundData(s.Name), v, body));
                break;
            case PhysicsStatementType.Spawner:
                SpawnerDefinitions.Add(new SpawnerData(s.Name));
                ApplyAll(s, (v, body) => ApplySpawnerValue(GetSpawnerData(s.Name), v, body));
                break;
            case PhysicsStatementType.Explosion:
                Explosions.Add(new ExplosionData(s.Name));
                ApplyAll(s, (v, body) => ApplyExplosionValue(GetExplosion(s.Name), v, body));
                break;
            case PhysicsStatementType.Component:
                Components.Add(new UnitData(s.Name));
                ApplyAll(s, (v, body) => ApplyComponentValue(GetComponentData(s.Name), v, body));
                break;
        }
    }

    public WeaponData? GetWeaponData(byte[] name) => FindFirst(Weapons, name, w => w.Name);

    public WeaponData? GetWeaponData(int index) => At(Weapons, index);

    public WeaponModeData? GetWeaponModeData(byte[] name) => FindFirst(WeaponModes, name, m => m.Name);

    public int GetWeaponModeIndex(byte[] name) => IndexOf(WeaponModes, name, m => m.Name);

    public RoundData? GetRoundData(byte[] name) => FindFirst(Rounds, name, r => r.Name);

    public int GetRoundIndex(byte[] name) => IndexOf(Rounds, name, r => r.Name);

    /// <summary>GetRound: the round at that position, or null.</summary>
    public RoundData? GetRound(int index) => At(Rounds, index);

    public SpawnerData? GetSpawnerData(byte[] name) => FindFirst(SpawnerDefinitions, name, s => s.Name);

    public SpawnerData? GetSpawnerData(int index) => At(SpawnerDefinitions, index);

    /// <summary>GetExplosion: the explosion definition with that name, or null.</summary>
    public ExplosionData? GetExplosion(byte[] name) => FindFirst(Explosions, name, e => e.Name);

    public ExplosionData? GetExplosionData(int index) => At(Explosions, index);

    public UnitData? GetComponentData(int index) => At(Components, index);

    private static T? At<T>(List<T> list, int index)
        where T : class => index >= 0 && index < list.Count ? list[index] : null;

    private partial int ExplosionIndex(byte[] name) => IndexOf(Explosions, name, e => e.Name);

    private partial int WeaponIndex(byte[] name) => IndexOf(Weapons, name, w => w.Name);

    private partial int SpawnerIndex(byte[] name) => IndexOf(SpawnerDefinitions, name, s => s.Name);

    /// <summary>A float value applied to a SINT field: this code is built with /QIfist, so it rounds to nearest.</summary>
    private static int ToSint(float v) => X87.Fist(v);

    /// <summary>CWeaponLanguageName::Apply's text ids by keyword (anything else leaves the field).</summary>
    private static int? LanguageNameFor(int keyword) => keyword switch
    {
        1 => 0xfe0572,
        2 => 0xffe389,
        3 => 0x101c1a0,
        4 => 0x1039fb7,
        5 => 0x1057dce,
        6 => 0x1075be5,
        7 => 0x10939fc,
        8 => 0x10b1813,
        9 => 0x10cf62a,
        10 => 0x1b1d4bb,
        11 => 0x1b590e8,
        12 => 0x1b94d15,
        13 => 0x1bd0942,
        _ => null,
    };

    /// <summary>CreateSeekType / CreateStateType: keywords 1..3 give 0..2. Retail's Apply calls through a NULL keyword
    /// object for any other number, so that throws here.</summary>
    private static int KeywordValue(int keyword, string family) => keyword is >= 1 and <= 3
        ? keyword - 1
        : throw new InvalidDataException($"{family} keyword {keyword}: retail applies a NULL keyword object here");

    /// <summary>CreateWeaponValue's classes, Load then Apply to the weapon with the statement's name.</summary>
    private bool ApplyWeaponValue(byte[] name, int type, byte[] body)
    {
        var r = new ValueReader(body);
        WeaponData? w = GetWeaponData(name);
        switch (type)
        {
            case 1:
            {
                int level = r.Int(); byte[] mode = r.String(); r.End();
                if (w is not null && level is >= 0 and < 5)
                {
                    w.ChargeModes[level] = GetWeaponModeIndex(mode);
                }

                return true;
            }
            case 2: { float v = r.Float(); r.End(); if (w is not null) w.ChargeRate = v; return true; }
            case 3: { int v = r.Int(); r.End(); if (w is not null) w.AmmoStore = Math.Clamp(v, 0, 5); return true; }
            case 4: { float v = r.Float(); r.End(); if (w is not null) w.Consumption = v; return true; }
            case 5: { byte[] v = r.String(); r.End(); w?.SetIconName(v); return true; }
            case 6: { float v = r.Float(); r.End(); if (w is not null) w.Smart = v != 0.0f ? 1 : 0; return true; }
            case 7: { float v = r.Float(); r.End(); if (w is not null) w.AdjustAim = v != 0.0f ? 1 : 0; return true; }
            case 8: { int v = r.Int(); r.End(); if (w is not null && v != 0) w.ZoomMode = v; return true; }
            case 9: { int v = r.Int(); r.End(); if (w is not null) w.AllowMovement = v != 0 ? 1 : 0; return true; }
            case 10: { int v = r.Int(); r.End(); if (w is not null && v != 0) w.Placement = v; return true; }
            case 11:
            {
                int v = r.Int(); r.End();
                if (w is not null && LanguageNameFor(v) is { } id)
                {
                    w.LanguageName = id;
                }

                return true;
            }
            case 12: { int v = r.Int(); r.End(); if (w is not null) w.Rating[0] = v; return true; }
            case 13: { int v = r.Int(); r.End(); if (w is not null) w.Rating[1] = v; return true; }
            case 14: { int v = r.Int(); r.End(); if (w is not null) w.Rating[2] = v; return true; }
            default: return false;
        }
    }

    /// <summary>CreateWeaponModeValue's classes, Load then Apply to the mode with the statement's name.</summary>
    private bool ApplyWeaponModeValue(byte[] name, int type, byte[] body)
    {
        var r = new ValueReader(body);
        WeaponModeData? m = GetWeaponModeData(name);
        switch (type)
        {
            case 1: { float v = r.Float(); r.End(); if (m is not null) m.Inaccuracy = v; return true; }
            case 2: { byte[] v = r.String(); r.End(); if (m is not null) m.Round = GetRound(GetRoundIndex(v)); return true; }
            case 3: { float v = r.Float(); r.End(); if (m is not null) m.ReloadTime = v; return true; }
            case 4: { float v = r.Float(); r.End(); if (m is not null) m.BurstSize = ToSint(v); return true; }
            case 5: { float v = r.Float(); r.End(); if (m is not null) m.BurstDelay = v; return true; }
            case 6: { byte[] v = r.String(); r.End(); m?.SetMuzzleEffect(v); return true; }
            case 8:
            {
                float index = r.Float(); float v = r.Float(); r.End();
                m?.SetLaunchSequence(ToSint(index), ToSint(v));
                return true;
            }
            case 9: { float v = r.Float(); r.End(); if (m is not null) m.MinRange = v; return true; }
            case 10: { float v = r.Float(); r.End(); if (m is not null) m.MaxRange = v; return true; }
            case 11: { float v = r.Float(); r.End(); if (m is not null) m.MinDeflection = v; return true; }
            case 12: { float v = r.Float(); r.End(); if (m is not null) m.MaxDeflection = v; return true; }
            case 13: { float v = r.Float(); r.End(); if (m is not null) m.PreFireDelay = v; return true; }
            case 14: { float v = r.Float(); r.End(); if (m is not null) m.PostFireDelay = v; return true; }
            case 15: { byte[] v = r.String(); r.End(); m?.SetClip(v); return true; }
            case 16: { float v = r.Float(); r.End(); if (m is not null) m.LockTime = v; return true; }
            case 17: { float v = r.Float(); r.End(); if (m is not null) m.LockDeflection = v; return true; }
            case 18: { byte[] v = r.String(); r.End(); m?.SetPreFireEffect(v); return true; }
            case 19: { int v = r.Int(); r.End(); if (m is not null) m.LockMode = v; return true; }
            case 20: { int v = r.Int(); r.End(); if (m is not null) m.LockUnit = v; return true; }
            case 21: { float v = r.Float(); r.End(); if (m is not null) m.LockRange = v; return true; }
            case 22: { float v = r.Float(); r.End(); if (m is not null) m.MaxLocks = ToSint(v); return true; }
            case 23: { float v = r.Float(); r.End(); if (m is not null) m.LockRadius = v; return true; }
            case 24: { byte[] v = r.String(); r.End(); m?.SetLaunchSound(v); return true; }
            case 25:
            {
                // CWeaponBasedOn is a weapon-mode value, but it copies the named WEAPON onto the weapon that has this
                // mode statement's name.
                byte[] v = r.String(); r.End();
                WeaponData? weapon = GetWeaponData(name);
                WeaponData? other = GetWeaponData(v);
                if (weapon is not null && other is not null)
                {
                    weapon.AmmoStore = other.AmmoStore;
                    weapon.Consumption = other.Consumption;
                    weapon.ChargeRate = other.ChargeRate;
                    weapon.AdjustAim = other.AdjustAim;
                    weapon.ZoomMode = other.ZoomMode;
                    weapon.AllowMovement = other.AllowMovement;
                    weapon.Placement = other.Placement;
                    weapon.Smart = other.Smart;
                    weapon.LanguageName = other.LanguageName;
                    Array.Copy(other.ChargeModes, weapon.ChargeModes, 5);
                    Array.Copy(other.Rating, weapon.Rating, 3);
                }

                return true;
            }
            case 26: { float v = r.Float(); r.End(); if (m is not null) m.MinTargetHeight = v; return true; }
            case 27: { float v = r.Float(); r.End(); if (m is not null) m.MaxTargetHeight = v; return true; }
            case 28: { float v = r.Float(); r.End(); if (m is not null) m.VolleySize = ToSint(v); return true; }
            case 29: { float v = r.Float(); r.End(); if (m is not null) m.Track = v != 0.0f ? 1 : 0; return true; }
            case 30: { float v = r.Float(); r.End(); if (m is not null) m.YawTolerance = v; return true; }
            case 31:
            {
                float index = r.Float(); float a = r.Float(); float b = r.Float(); r.End();
                m?.SetLaunchAngle(ToSint(index), a, b);
                return true;
            }
            case 32: { float v = r.Float(); r.End(); if (m is not null) m.Power = v; return true; }
            case 33: { float v = r.Float(); r.End(); if (m is not null) m.Predictive = v > 0.0f ? 1 : 0; return true; }
            case 34: { byte[] v = r.String(); r.End(); m?.SetPreFireSound(v); return true; }
            case 35: { float v = r.Float(); r.End(); if (m is not null) m.SoundPerBurst = v != 0.0f ? 1 : 0; return true; }
            case 36: { byte[] v = r.String(); r.End(); m?.SetPostFireSound(v); return true; }
            case 37: { int v = r.Int(); r.End(); if (m is not null) m.MuzzleLight = v; return true; }
            case 38: { float v = r.Float(); r.End(); if (m is not null) m.MuzzleLightRadius = v; return true; }
            default: return false;
        }
    }

    /// <summary>
    /// CreateRoundValue's classes. VC6 compiles the percentages' `/ 100.0f` as a multiply by the float 0.01f (0x3c23d70a): CRoundRearm
    /// 004382ca, CRoundGravity 0043853a and CRoundFlakInaccuracy 0043883a in the pristine exe.
    /// </summary>
    private bool ApplyRoundValue(RoundData? d, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { float v = r.Float(); r.End(); if (d is not null) d.LifeSpan = v; return true; }
            case 2: { float v = r.Float(); r.End(); if (d is not null) d.Damage = v; return true; }
            case 3: { float v = r.Float(); r.End(); if (d is not null) d.Speed = v; return true; }
            case 4:
            {
                int keyword = r.Int(); r.End();
                if (d is not null) d.Seek = KeywordValue(keyword, "seek");
                return true;
            }
            case 5: { float v = r.Float(); r.End(); if (d is not null) d.TurnRate = v; return true; }
            case 6: { float v = r.Float(); r.End(); if (d is not null) d.Gravity = v * 0.01f; return true; }
            case 7: { float v = r.Float(); r.End(); if (d is not null) d.BounceFactor = v; return true; }
            case 8: { byte[] v = r.String(); r.End(); d?.SetEffect(v); return true; }
            case 9: { byte[] v = r.String(); r.End(); d?.SetExplosion(v); return true; }
            case 10: { float v = r.Float(); r.End(); if (d is not null) d.SeekDelay = v; return true; }
            case 11: { float v = r.Float(); r.End(); if (d is not null) d.Wiggle = v; return true; }
            case 12: { float v = r.Float(); r.End(); if (d is not null) d.Radius = v; return true; }
            case 13: { r.End(); if (d is not null) d.Flak = 1; return true; }
            case 14: { float v = r.Float(); r.End(); if (d is not null) d.FlakInaccuracy = v * 0.01f; return true; }
            case 15: { float v = r.Float(); r.End(); if (d is not null) d.SeekAngle = v; return true; }
            case 16: { r.End(); if (d is not null) d.Beam = 1; return true; }
            case 17: { float v = r.Float(); r.End(); if (d is not null) d.SoundMaterial = ToSint(v); return true; }
            case 18: { r.End(); if (d is not null) d.WeirdoSeek = 1; return true; }
            case 19: { float v = r.Float(); r.End(); if (d is not null) d.Rearm = v * 0.01f; return true; }
            case 20: { byte[] v = r.String(); r.End(); d?.CopyFrom(GetRoundData(v)); return true; }
            case 21: { float v = r.Float(); r.End(); if (d is not null) d.SeekTerminationTime = v; return true; }
            case 22: { float v = r.Float(); r.End(); if (d is not null) d.Missile = v != 0.0f ? 1 : 0; return true; }
            case 23: { float v = r.Float(); r.End(); if (d is not null) d.Proximity = v; return true; }
            case 24: { float v = r.Float(); r.End(); if (d is not null) d.GridOfFear = ToSint(v); return true; }
            case 25: { float v = r.Float(); r.End(); if (d is not null) d.CanGoUnderWater = ToSint(v); return true; }
            case 26: { float v = r.Float(); r.End(); if (d is not null) d.GroundHugging = ToSint(v); return true; }
            case 27: { float v = r.Float(); r.End(); if (d is not null) d.PassiveCollision = ToSint(v); return true; }
            case 28: { float v = r.Float(); r.End(); if (d is not null) d.Jumps = ToSint(v); return true; }
            case 29: { float v = r.Float(); r.End(); if (d is not null) d.JumpRange = v; return true; }
            case 30: { float v = r.Float(); r.End(); if (d is not null) d.JumpDelay = v; return true; }
            case 31: { int v = r.Int(); r.End(); if (d is not null) d.Explode = v > 0 ? 1 : 0; return true; }
            case 32: { float v = r.Float(); r.End(); if (d is not null) d.Torpedo = v != 0.0f ? 1 : 0; return true; }
            case 33: { byte[] v = r.String(); r.End(); d?.SetWaterEffect(v); return true; }
            case 34: { float v = r.Float(); r.End(); if (d is not null) d.Fire = v != 0.0f ? 1 : 0; return true; }
            case 35:
            {
                int keyword = r.Int(); r.End();
                if (d is not null) d.TreeCollision = KeywordValue(keyword, "tree collision");
                return true;
            }
            case 36: { byte[] v = r.String(); r.End(); d?.SetMesh(v); return true; }
            case 37: { float v = r.Float(); r.End(); if (d is not null) d.Smart = v != 0.0f ? 1 : 0; return true; }
            case 38: { float v = r.Float(); r.End(); if (d is not null) d.Length = v; return true; }
            default: return false;
        }
    }

    /// <summary>CreateSpawnerValue's classes.</summary>
    private bool ApplySpawnerValue(SpawnerData? d, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { byte[] v = r.String(); r.End(); d?.SetThingName(v); return true; }
            case 2: { float v = r.Float(); r.End(); if (d is not null) d.Delay = v; return true; }
            case 3: { int v = r.Int(); r.End(); if (d is not null) d.Amount = v; return true; }
            case 4: { float v = r.Float(); r.End(); if (d is not null) d.SeekDelay = v; return true; }
            case 5: { r.End(); if (d is not null) d.Recall = 1; return true; }
            case 6: { float v = r.Float(); r.End(); if (d is not null) d.MinRange = v; return true; }
            case 7: { float v = r.Float(); r.End(); if (d is not null) d.MaxRange = v; return true; }
            case 8: { float v = r.Float(); r.End(); if (d is not null) d.PreSpawnDelay = v; return true; }
            case 9: { float v = r.Float(); r.End(); if (d is not null) d.PostSpawnDelay = v; return true; }
            case 10: { byte[] v = r.String(); r.End(); d?.CopyFrom(GetSpawnerData(v)); return true; }
            case 11: { int v = r.Int(); r.End(); if (d is not null) d.SquadSize = v; return true; }
            case 12: { float v = r.Float(); r.End(); if (d is not null) d.SquadDelay = v; return true; }
            case 13: { int v = r.Int(); r.End(); if (d is not null) d.Infinite = v; return true; }
            case 14: { int v = r.Int(); r.End(); if (d is not null) d.Conditions = v; return true; }
            default: return false;
        }
    }

    /// <summary>CreateExplosionValue's classes.</summary>
    private bool ApplyExplosionValue(ExplosionData? d, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { byte[] v = r.String(); r.End(); d?.CopyFrom(GetExplosion(v)); return true; }
            case 2: { byte[] v = r.String(); r.End(); d?.SetAirEffect(v); return true; }
            case 3: { float v = r.Float(); r.End(); if (d is not null) d.Radius = v; return true; }
            case 4: { float v = r.Float(); r.End(); if (d is not null) d.Damage = v; return true; }
            case 5: { byte[] v = r.String(); r.End(); d?.SetGroundEffect(v); return true; }
            case 6: { byte[] v = r.String(); r.End(); d?.SetWaterEffect(v); return true; }
            case 7: { byte[] v = r.String(); r.End(); d?.SetUnitEffect(v); return true; }
            case 8: { int v = r.Int(); r.End(); if (d is not null) d.Volumetric = v; return true; }
            case 9: { int v = r.Int(); r.End(); if (d is not null) d.Time = v; return true; }
            case 10: { byte[] v = r.String(); r.End(); d?.SetSound(v); return true; }
            case 11: { int v = r.Int(); r.End(); if (d is not null) d.Smart = v; return true; }
            case 12: { int v = r.Int(); r.End(); if (d is not null) d.Light = v; return true; }
            case 13: { int v = r.Int(); r.End(); if (d is not null) d.Oriented = v; return true; }
            case 14: { r.Float(); r.End(); return true; } // Shockwave: read and never applied
            case 15: { byte[] v = r.String(); r.End(); d?.SetWaterSound(v); return true; }
            default: return false;
        }
    }

    /// <summary>CreateComponentValue's classes (a component is a CUnitData with its own value ids).</summary>
    private bool ApplyComponentValue(UnitData? c, int type, byte[] body)
    {
        var r = new ValueReader(body);
        switch (type)
        {
            case 1: { float v = r.Float(); r.End(); if (c is not null) c.Life = v; return true; }
            case 2:
            {
                byte[] weapon = r.String(); byte[] mount = r.String(); int a = r.Int(); r.End();
                c?.Weapons.Add(new UnitWeaponReference(GetWeaponIndex(weapon), a, UnitData.MapMountName(mount)));
                return true;
            }
            case 3: { byte[] v = r.String(); r.End(); c?.SetMesh(v); return true; }
            case 4: { byte[] v = r.String(); r.End(); if (c is not null) c.Explosion = GetExplosionIndex(v); return true; }
            case 6: { float v = r.Float(); r.End(); if (c is not null) c.MaxTargetRange = v; return true; }
            case 7: { float v = r.Float(); r.End(); if (c is not null) c.MaxTurretYaw = v; return true; }
            case 8: { float v = r.Float(); r.End(); if (c is not null) c.MaxYaw = v; return true; }
            case 9: { float v = r.Float(); r.End(); if (c is not null) c.TurnRate = v; return true; }
            case 10: { byte[] v = r.String(); r.End(); c?.CopyFrom(GetComponentData(v)); return true; }
            case 11: { float v = r.Float(); r.End(); if (c is not null) c.TurretTurnRate = v; return true; }
            case 12: { byte[] v = r.String(); r.End(); c?.SetEffectName(11, v); return true; } // Noise
            case 13: { float v = r.Float(); r.End(); if (c is not null) c.Big = v != 0.0f ? 1 : 0; return true; }
            case 14: { byte[] v = r.String(); r.End(); if (c is not null) c.SmallExplosion = GetExplosionIndex(v); return true; }
            case 15: { float v = r.Float(); r.End(); if (c is not null) c.Indiscriminate = v != 0.0f ? 1 : 0; return true; }
            case 16: { float v = r.Float(); r.End(); if (c is not null) c.Deploy = v != 0.0f ? 1 : 0; return true; }
            case 17: { float v = r.Float(); r.End(); if (c is not null) c.DeployDamageMultiplier = v; return true; }
            case 18: { float v = r.Float(); r.End(); if (c is not null) c.Tentacle = v != 0.0f ? 1 : 0; return true; }
            case 19:
            {
                byte[] spawner = r.String(); byte[] mount = r.String(); int a = r.Int(); r.End();
                c?.Spawners.Add(new UnitWeaponReference(GetSpawnerIndex(spawner), a, UnitData.MapMountName(mount)));
                return true;
            }
            case 20:
            {
                float v = r.Float(); int index = r.Int(); r.End();
                if (c is not null) c.AttackPriority[index] = v;
                return true;
            }
            case 21: { float v = r.Float(); r.End(); if (c is not null) c.Detach = v != 0.0f ? 1 : 0; return true; }
            case 22: { float v = r.Float(); r.End(); if (c is not null) c.CanBeLocked = v != 0.0f ? 1 : 0; return true; }
            case 23: { float v = r.Float(); r.End(); if (c is not null) c.Sweeping = v != 0.0f ? 1 : 0; return true; }
            case 24: { float v = r.Float(); r.End(); if (c is not null) c.PassOnHitEffect = v != 0.0f ? 1 : 0; return true; }
            case 25: { byte[] v = r.String(); r.End(); c?.SetEffectName(7, v); return true; } // Vent
            default: return false;
        }
    }
}
