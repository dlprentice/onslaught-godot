// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/Unit.cpp (CUnit: Init's weapon loop at 004f86d0, Unknown4fb3d0 004fb3d0, CanFireAt
// 004fb500, Unknown4fb5a0 004fb5a0, WeaponUnknown509140 004fb650, Unknown4fb670 004fb670, GetMinRange 004fb780,
// GetMaxRange 004fb7e0, Unknown4fb840 004fb840, GetWeaponUnknownA0_88 004fbc90, Unknown86 004fbcb0, Unknown84
// 004fc000, GetEmitter 004fc4e0, Unknown4fc6e0 004fc6e0, GetTargetablePos 004fd4d0, AnyWeaponUnknown94 004fd570,
// AnyWeaponUnknown50a290 004fd760; the AnyWeaponBusy scan), src/actor.cpp (CActor::IsOnGround 00401f70, IsInWater
// 00401fa0), src/GroundUnit.cpp (CGroundUnit::IsOnGround 0047c8b0), src/unit.h (CBuilding::IsOnGround) and
// src/WorldPhysicsManager.cpp (CUnitData::FindPair 00511bc0) and src/map.h (CMap::GetHeightAt). actor.cpp is in
// Stuart's release, hence GPL-3.0-only.

using System.Runtime.CompilerServices;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CUnit's weapons and the queries its AI makes of them.</summary>
public abstract partial class Unit
{
    /// <summary>
    /// CUnitData::mPairs: each emitter's transform in the unit's own space, cached per unit definition the first time a
    /// unit of that definition asks (CUnitPair). Kept beside UnitData here; retail Adds each pair to an SPtrSet, which
    /// takes a node from the shared pool (not taken here).
    /// </summary>
    private static readonly ConditionalWeakTable<UnitData, List<(int Type, int Index, FVector Pos, FMatrix Ori)>> sPairs = new();

    /// <summary>mWeapons: the weapons CUnit::Init made, in its Append order.</summary>
    public readonly List<Weapon> WeaponList = [];

    /// <summary>mCurrentWeapon.</summary>
    public Weapon? CurrentWeapon;

    /// <summary>mUnknown110: 0 from the constructor.</summary>
    public int Unknown110;

    /// <summary>mUnknown168: 1 while a shot is being prepared.</summary>
    public int Unknown168;

    /// <summary>mUnknown16C: when the prepared shot fires.</summary>
    public float Unknown16C;

    /// <summary>mUnknown1E8 / mUnknown1EC: the shot's ready and preparing flags.</summary>
    public int Unknown1E8;

    public int Unknown1EC;

    /// <summary>mUnknownE0: the turret's yaw. mUnknownE8: the barrel's pitch.</summary>
    public float UnknownE0;

    public float UnknownE8;

    /// <summary>mUnknown24C: 1 from the constructor (the final line test ignores the unit itself).</summary>
    public int Unknown24C = 1;

    /// <summary>
    /// CUnit::Init's weapon step for one reference: UPhysicsManager::SpawnWeapon, its Init on the reference's mount
    /// (emitter index 1), the emitter type, and the Append. Returns the weapon (null when none was made) so the caller
    /// sets the turret and barrel flags from the gun's part chain.
    /// </summary>
    public Weapon? AddWeapon(UnitWeaponReference uw)
    {
        Weapon? weapon = Weapon.Spawn(Context, uw.Index, uw.Unknown04);
        if (weapon is null)
        {
            return null;
        }

        weapon.Init(new InitEquipment { AttachedTo = this, Number = 1, Mount = uw.Mount });
        weapon.UnknownAC = uw.Mount;
        return weapon;
    }

    /// <summary>The Append after the part-chain flags are set (CUnit::Init's mWeapons.Append).</summary>
    public void AppendWeapon(Weapon weapon) => WeaponList.Add(weapon);

    /// <summary>
    /// IsOnGround (CUnit's slot 67): CActor's 0.15 s since touching the ground, CGroundUnit's 0.21 s, a building always.
    /// (The overrides are chosen by class here; they belong on the classes.)
    /// </summary>
    public override bool IsOnGround()
    {
        if (this is Building)
        {
            return true;
        }

        float window = this is GroundUnit ? 0.21f : 0.15f;
        return Context.Events.Time - LastTimeOnGround < window;
    }


    /// <summary>CMap::GetHeightAt: the ground sample at the position's integer cell (/QIfist rounding), or the sea.</summary>
    private float GetHeightAt(FVector pos)
    {
        float h = Context.Map.HeightScale * Context.Map.GetHeightSample(X87.Fist(pos.X), X87.Fist(pos.Y));
        if (Context.WaterLevel < h)
        {
            h = Context.WaterLevel;
        }

        return h;
    }

    /// <summary>
    /// GetEmitter (slot 88): the emitter's transform in world space, from the unit data's cache of local transforms
    /// (filled from the mesh pose on first use) for the cacheable types; otherwise straight from the render thing.
    /// </summary>
    public virtual void GetEmitter(int type, int index, out FVector outPos, out FMatrix outOrientation)
    {
        if (Unknown110 == 0 && UnitData is not null && UnitData.Unknown1A0 == 0 && type is not (15 or 16 or 17 or 18 or 19))
        {
            List<(int Type, int Index, FVector Pos, FMatrix Ori)> pairs = sPairs.GetValue(UnitData, _ => []);
            int found = pairs.FindIndex(p => p.Index == index && p.Type == type);
            (int, int, FVector Pos, FMatrix Ori) pair;
            if (found < 0)
            {
                RenderEmitter(type, index, out outPos, out outOrientation, local: true, force: true);
                if (outPos.Equals(new FVector(0, 0, 0)))
                {
                    return;
                }

                pair = (type, index, outPos, outOrientation);
                pairs.Insert(0, pair);
            }
            else
            {
                pair = pairs[found];
            }

            outPos = Orientation.Transform(pair.Pos) + Pos;
            outOrientation = Orientation.Multiply(pair.Ori);
        }
        else
        {
            RenderEmitter(type, index, out outPos, out outOrientation, local: false, force: true);
        }
    }

    /// <summary>Unknown4fc6e0: the emitter types' mount-point names on the unit's mesh.</summary>
    private void RenderEmitter(int type, int index, out FVector pos, out FMatrix ori, bool local, bool force)
    {
        string? name = type switch
        {
            >= 1 and <= 9 => "Gun" + (char)('A' + type - 1),
            >= 10 and <= 14 => "Spawner" + (char)('A' + type - 10),
            >= 15 and <= 19 => "Waypoint" + (char)('A' + type - 15),
            20 => "Component",
            21 => "Vent",
            22 => "Dust",
            23 => "Engine",
            24 => "Trail",
            25 => "Smoke",
            26 => "Thruster",
            27 => "Doorstop",
            28 => "Activation",
            29 => "Charge",
            _ => null,
        };
        if (name is null)
        {
            Context.Skip("CUnit::Unknown4fc6e0: unknown emitter type (retail logs it)");
            pos = new FVector(0, 0, 0);
            ori = FMatrix.Identity;
            return;
        }

        RenderThing render = RenderThing!;
        (pos, ori) = MeshPose.GetRTEmitter(render.Mesh, render.Pose, this, name, index, Context.TransformStatics, local, force);
    }

    /// <summary>
    /// GetLaunchPosition (004fc3c0): a weapon of the unit's launches from its emitter of the weapon's type at that index,
    /// or from the weapon's primary point when there is none (or it sits at the origin).
    /// </summary>
    public virtual void GetLaunchPosition(Weapon weapon, int index, out FVector pos, out FMatrix orientation, bool needOrientation)
    {
        if (WeaponList.Contains(weapon))
        {
            pos = new FVector(0, 0, 0);
            orientation = FMatrix.Identity;
            if (UnitData is not null)
            {
                GetEmitter(weapon.UnknownAC, index, out pos, out orientation);
            }

            if (pos.Equals(new FVector(0, 0, 0)))
            {
                pos = weapon.GetPrimaryPosition();
                orientation = weapon.GetPrimaryOrientation();
            }

            return;
        }

        pos = weapon.GetPrimaryPosition();
        orientation = weapon.GetPrimaryOrientation();
    }

    /// <summary>GetCurrentTarget (slot 81): the AI's target (a weapon's shot aims its round at it).</summary>
    public virtual Thing? GetCurrentLaunchTarget() => AI?.Target.ToRead as Thing;

    /// <summary>GetTargetablePos (slot 90): the segments' centre, else the centre position. Cannons, mechs and boats override it.</summary>
    public FVector GetTargetablePos()
    {
        if (HasSegments)
        {
            throw new NotSupportedException("CDestructableSegmentsController::GetCentre is not ported");
        }

        FVector pos = GetCentrePos();
        if (this is Cannon)
        {
            // CCannon::GetTargetablePos (0041b540): halfway between CUnit's point and its position.
            pos += Pos;
            pos *= 0.5f;
        }

        return pos;
    }

    /// <summary>The AnyWeaponBusy scan: a weapon in the middle of a burst.</summary>
    private bool AnyWeaponBusy() => WeaponList.Any(w => w.IsFiring());

    /// <summary>AnyWeaponUnknown50a290.</summary>
    public bool AnyWeaponUnknown50a290() => AnyWeaponBusy();

    /// <summary>AnyWeaponUnknown94: a weapon turns a turret.</summary>
    public bool AnyWeaponUnknown94() => WeaponList.Any(w => w.Unknown94 != 0);

    private void RequireNoSpawners(string where)
    {
        if (UnitData is not null && UnitData.Spawners.Count > 0)
        {
            throw new NotSupportedException($"{where}: the unit's spawners are not ported");
        }
    }

    /// <summary>Unknown4fb670: 2 too near (or no target or weapon), 1 too far, 0 in range of the current weapon.</summary>
    public int RangeBand(Thing? target)
    {
        if (target is null)
        {
            return 2;
        }

        FVector offset = target.Pos - Pos;
        float dist = offset.Magnitude();
        if (CurrentWeapon is not null)
        {
            if (CurrentWeapon.GetMinRange(target.Pos) > dist)
            {
                return 2;
            }

            if (CurrentWeapon.GetMaxRange(target.Pos) < dist)
            {
                return 1;
            }

            return 0;
        }

        RequireNoSpawners("CUnit::Unknown4fb670");
        return 2;
    }

    /// <summary>GetMinRange: the current weapon's.</summary>
    public float GetMinRange(FVector target)
    {
        if (CurrentWeapon is not null)
        {
            return CurrentWeapon.GetMinRange(target);
        }

        RequireNoSpawners("CUnit::GetMinRange");
        return 0;
    }

    /// <summary>GetMaxRange: the current weapon's.</summary>
    public float GetMaxRange(FVector target)
    {
        if (CurrentWeapon is not null)
        {
            return CurrentWeapon.GetMaxRange(target);
        }

        RequireNoSpawners("CUnit::GetMaxRange");
        return 0;
    }

    /// <summary>
    /// Unknown4fb840: unless a weapon is mid-burst, picks the weapon (or spawner) to use on the target: one that can hit
    /// it, its height over the ground or sea within the weapon's band, scored by range fit and readiness.
    /// </summary>
    public void ChooseWeapon(Thing? target)
    {
        if (AnyWeaponBusy() || target is null)
        {
            return;
        }

        CurrentWeapon = null;
        FVector offset = target.Pos - Pos;
        float dist = offset.Magnitude();
        float best = -1.0f;
        RequireNoSpawners("CUnit::Unknown4fb840");
        foreach (Weapon weapon in WeaponList)
        {
            if (!weapon.CanHitThing(target))
            {
                continue;
            }

            float height = Context.Map.Collide(target.Pos);
            if (Context.WaterLevel < height)
            {
                height = Context.WaterLevel;
            }

            height -= target.Pos.Z;
            if (height > weapon.GetMinTargetHeight() && height < weapon.GetMaxTargetHeight())
            {
                float score = 0;
                if (target.IsA(ThingType.Component) && weapon.CanHitType(ThingType.Component))
                {
                    score = 2000000.0f;
                }

                if (weapon.GetMinRange(new FVector(0, 0, 0)) > dist)
                {
                    score += weapon.GetMinRange(new FVector(0, 0, 0)) - dist;
                }
                else if (weapon.GetMaxRange(new FVector(0, 0, 0)) < dist)
                {
                    score += dist - weapon.GetMaxRange(new FVector(0, 0, 0));
                }
                else
                {
                    score += 1000000.0f;
                }

                if (weapon.ReadyToFire())
                {
                    score += 1000000.0f;
                }

                if (score > best)
                {
                    CurrentWeapon = weapon;
                    best = score;
                }
            }
        }
    }

    /// <summary>Unknown4fb3d0: a weapon (or spawner) can hit the target at its height over the ground or sea.</summary>
    public bool CanEngage(Unit? target)
    {
        // target->Unknown108(): TRUE for every unit but the carver (00405930, FALSE), which is not ported.
        if (target is null)
        {
            return false;
        }

        RequireNoSpawners("CUnit::Unknown4fb3d0");
        foreach (Weapon weapon in WeaponList)
        {
            if (weapon.CanHitThing(target))
            {
                float height = GetHeightAt(target.Pos) - target.Pos.Z;
                return height >= weapon.GetMinTargetHeight() && height <= weapon.GetMaxTargetHeight();
            }
        }

        return false;
    }

    /// <summary>CanFireAt: in range, at a height in the weapon's band, and the weapon's own test.</summary>
    public bool CanFireAt(Unit target, bool a)
    {
        if (RangeBand(target) != 0)
        {
            return false;
        }

        if (CurrentWeapon is not null)
        {
            float height = Context.Map.Collide(target.Pos);
            if (Context.WaterLevel < height)
            {
                height = Context.WaterLevel;
            }

            height -= target.Pos.Z;
            if (height >= CurrentWeapon.GetMinTargetHeight() && height <= CurrentWeapon.GetMaxTargetHeight())
            {
                return CurrentWeapon.CanFireAt(target, a);
            }
        }

        RequireNoSpawners("CUnit::CanFireAt");
        return false;
    }

    /// <summary>Unknown4fb5a0: in range, at a height in the weapon's band (0 on the ground), and the weapon's reach test.</summary>
    public bool CanReach(Unit target)
    {
        if (RangeBand(target) != 0)
        {
            return false;
        }

        if (CurrentWeapon is not null)
        {
            float height;
            if (target.IsOnGround())
            {
                height = 0;
            }
            else
            {
                height = Context.Map.Collide(target.Pos);
                if (Context.WaterLevel < height)
                {
                    height = Context.WaterLevel;
                }

                height -= target.Pos.Z;
            }

            if (height >= CurrentWeapon.GetMinTargetHeight() && height <= CurrentWeapon.GetMaxTargetHeight())
            {
                return CurrentWeapon.CanReach(target);
            }
        }

        RequireNoSpawners("CUnit::Unknown4fb5a0");
        return false;
    }

    /// <summary>WeaponUnknown509140: the current weapon aims at the target and place.</summary>
    public void AimWeapon(Thing? target, FVector pos) => CurrentWeapon?.Aim(target, pos);

    /// <summary>GetWeaponUnknownA0_88: the current mode's pre-fire delay.</summary>
    public float GetWeaponUnknownA0_88() => CurrentWeapon is not null ? CurrentWeapon.Mode!.PreFireDelay : 0;

    /// <summary>Unknown84: the prepared shot is ready and the weapon can fire.</summary>
    public bool ShotReady()
    {
        if (CurrentWeapon is not null)
        {
            return Unknown1E8 != 0 && CurrentWeapon.ReadyToFire();
        }

        RequireNoSpawners("CUnit::Unknown84");
        return false;
    }

    /// <summary>
    /// Unknown86: starts preparing a shot with the current weapon (a deploying unit deploys first): the fire time, the
    /// flags, the pre-fire sound and the charge effects at the "Charge" emitters.
    /// </summary>
    public bool PrepareShot()
    {
        if (UnitData!.Deploy != 0 && DeployState != 4)
        {
            throw new NotSupportedException("CUnit::StartDeploying (from Unknown86) is not ported");
        }

        if (CurrentWeapon is not null)
        {
            if (Unknown168 == 1 || Unknown1E8 != 0 || !CurrentWeapon.ReadyToFire())
            {
                return false;
            }

            Unknown16C = Context.Events.Time + GetWeaponUnknownA0_88();
            Unknown168 = 1;
            Unknown1EC = 1;
            if (CurrentWeapon.Mode!.PreFireSoundName is not null)
            {
                Context.Skip("SOUND.PlayEffect (CUnit::Unknown86's pre-fire sound)");
            }

            if (CurrentWeapon.Mode.PreFireEffectName is not null)
            {
                Context.Skip("PARTICLE_MANAGER.CreateEffect (CUnit::Unknown86's charge effects at the Charge emitters)");
            }

            if (Unknown16C <= Context.Events.Time)
            {
                Unknown1E8 = 1;
            }

            return true;
        }

        RequireNoSpawners("CUnit::Unknown86");
        return false;
    }
}
