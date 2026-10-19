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
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
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

    /// <summary>
    /// ConfirmedKill (004fe030, slot 101): an enemy killed; a wingman (the unit data's chatter flag) says a line of
    /// three, at most every 40 seconds (a GAME random and a message; not ported, it throws).
    /// </summary>
    public virtual void ConfirmedKill(Unit killed)
    {
        if (!IsTargetAlligence(killed.Allegiance))
        {
            return;
        }

        if (UnitData is { Wingman: not 0 })
        {
            throw new NotSupportedException("CUnit::ConfirmedKill's wingman line (GAME.Random, CMessage) is not ported");
        }
    }

    /// <summary>
    /// Damage (004f9a90, slot 40): a round from a unit tells the squad it was shot at; a dying big unit ignores explosions;
    /// a deployed unit takes its deploy multiple. Damage (when vulnerable; a nexus unit only at its nexus, a weak point
    /// five times) goes to the segments, else marks the hit (Unknown107), takes the shields first and then life; at
    /// death a battle engine's round counts a unit destroyed, and the unit (unless it retreats instead) starts dying and
    /// is told how (Unknown71). Shields over 2 show their effect. Negative damage repairs to full life. A wingman then
    /// says a line (a GAME random; the message is not ported).
    /// </summary>
    public override void Damage(float amount, Thing? byThis, bool damageShields = true, int meshPart = -1)
    {
        if (Squad is { } squad && byThis is Round round && byThis.IsA(ThingType.Ammunition) && round.Launcher.ToRead is Unit shooter
            && shooter.IsA(ThingType.Unit))
        {
            squad.Unknown4e6660(shooter);
        }

        if (IsDying && byThis is not null && byThis.IsA(ThingType.Explosion) && UnitData is { Big: not 0 })
        {
            return;
        }

        if (DeployState is 4 or 3 or 5)
        {
            amount *= UnitData!.DeployDamageMultiplier;
        }

        if (amount > 0)
        {
            if (!Vulnerable)
            {
                return;
            }

            if (Unknown228Nexus && byThis is not null && byThis.IsA(ThingType.Explosion))
            {
                return;
            }

            if ((Unknown228Nexus || Unknown22CWeakPoint) && meshPart != -1)
            {
                throw new NotSupportedException("CUnit::Damage to a nexus or weak-point part is not ported");
            }

            if (Segments is { } segments)
            {
                segments.Damage(meshPart, amount, byThis);
                return;
            }

            Unknown107(amount);
            if (damageShields)
            {
                if (Shields >= amount)
                {
                    Shields -= amount;
                    return;
                }

                if (amount > Shields)
                {
                    amount -= Shields;
                    Shields = 0;
                }
            }

            Life -= amount;
            if (Life < 0 && !IsDying)
            {
                if (byThis is Round r && byThis.IsA(ThingType.Ammunition) && r.Launcher.ToRead is BattleEngine be
                    && be.IsA(ThingType.BattleEngine))
                {
                    (be.Player as Game.Player)?.IncStat(Game.PlayerStat.UnitsDestroyed);
                }

                if (UnitData!.Retreat == 0)
                {
                    StartDieProcess();
                    Unknown71(amount, byThis);
                }

                Unknown1F0 = 0;
            }

            if (Shields > 2.0f && UnitData!.EffectNames[0] is { } shieldEffect && Context.Particles is { } particles)
            {
                particles.Manager.CreateEffect(Context.FindParticle(shieldEffect), null, byThis!.Pos, false, false);
            }
        }
        else if (Life < UnitData!.Life && Life > 0)
        {
            if (UnitData.Life - Life < -amount)
            {
                Life = UnitData.Life;
            }
            else
            {
                Life -= amount;
            }
        }

        if (UnitData!.Retreat != 0 && Life < 0)
        {
            throw new NotSupportedException("CUnit::Unknown100 (a retreating unit going to its safe side) is not ported");
        }

        if (UnitData.Wingman != 0)
        {
            _ = Context.Random.Random() % 3;
            if ((Life < 0 && !SaidDying) || (UnitData.Life * 0.25f > Life && !SaidQuarterLife) || (UnitData.Life * 0.5f > Life && !SaidHalfLife))
            {
                throw new NotSupportedException("CUnit::Damage's wingman line (CMessage) is not ported");
            }
        }
    }

    /// <summary>GetSoundMaterial (slot 43): the unit data's.</summary>
    public override int GetSoundMaterial() => UnitData?.SoundMaterial ?? 0;

    /// <summary>Unknown112 (slot 112): how long after its removal a unit shuts down.</summary>
    public virtual float Unknown112() => 0.05f;

    /// <summary>
    /// Remove (004fd040): the children released (killed when dying), the spawners cleared, the unit's explosion, its
    /// script told it died and deleted, and its shutdown after Unknown112 seconds.
    /// </summary>
    public void Remove()
    {
        if (UnitData?.Components.Count > 0)
        {
            throw new NotSupportedException("CUnit::ReleaseChildren (a unit's components) is not ported");
        }

        if (UnitData?.Spawners.Count > 0)
        {
            Context.Skip("CUnit::ClearSpawners (the unit's idle spawners)");
        }

        SpawnUnitExplosion();
        if (Script is not null)
        {
            Script.Died();
            Script.Delete();
            Script = null;
        }

        Context.Events.AddEvent(ThingEvent.Shutdown, this, Unknown112() + Context.Events.Time);
    }

    /// <summary>
    /// SpawnExplosion (004fd230): the unit data's explosion at the unit, its radius the unit's, its effects free to scatter
    /// over the unit; on the ground, in water or in the air as the unit is.
    /// </summary>
    public void SpawnUnitExplosion()
    {
        if (UnitData is null)
        {
            return;
        }

        int index = UnitData.Explosion;
        Explosion? explosion = Explosion.Spawn(Context, index);
        var init = new InitThing
        {
            Behaviour = index >= 0 && index < Context.Physics.Explosions.Count ? Context.Physics.Explosions[index] : null,
            Allegiance = Allegiance,
            AttachedTo = this,
            UseAttachedRadius = true,
            AllowVolumetric = true,
            ColType = IsOnGround() ? 1 : IsInWater() ? 2 : 0,
        };
        if (explosion is not null)
        {
            init.Pos = Pos;
            explosion.Init(init);
        }
    }

    /// <summary>mSaidDying / mSaidQuarterLife / mSaidHalfLife: a wingman's lines already said.</summary>
    public bool SaidDying, SaidQuarterLife, SaidHalfLife;

    /// <summary>Unknown107 (004fe540): the hit's flash, a countdown the 4005 event runs down.</summary>
    public void Unknown107(float a)
    {
        if (Unknown218 == 0)
        {
            Context.Events.AddEvent(UnitEvent.Event4005, this, EventManager.NextFrame);
        }

        Unknown218 += Numerics.X87.Fist(a * 100.0f);
        if (Unknown218 > 100)
        {
            Unknown218 = 100;
        }

        Unknown21C = 10;
    }

    /// <summary>Unknown71 (slot 71): how the unit was killed (nothing for most; mechs override it).</summary>
    public virtual void Unknown71(float amount, Thing? byThis)
    {
    }

    /// <summary>
    /// CUnit::StartDieProcess (004fd140): dying (no shutdown yet: the unit's own Move ends it), off its side's counter with
    /// the score moved by its behaviour's priority, the segments told, the mission script told, the spawners cleared.
    /// </summary>
    public override bool StartDieProcess()
    {
        if (IsDying)
        {
            return false;
        }

        Context.Skip("SOUND.KillSamplesForThing (presentation)");
        Flags |= ThingFlags.Dying;
        if (UnitData is not null)
        {
            int behaviour = (int)UnitData.Behaviour;
            if (Allegiance == 0)
            {
                Context.World.CountersA[behaviour]--;
                Context.Game?.IncScore(-BehaviourPriority(UnitData.Behaviour));
                Flags |= ThingFlags.RemovedUnitType;
            }
            else if (Allegiance == 1)
            {
                Context.World.CountersB[behaviour]--;
                Context.Game?.IncScore(BehaviourPriority(UnitData.Behaviour));
                Flags |= ThingFlags.RemovedUnitType;
            }
        }

        Segments?.OwnerDying();

        Script?.StartedDying();
        if (UnitData?.Spawners.Count > 0)
        {
            Context.Skip("CUnit::ClearSpawners (the unit's idle spawners)");
        }

        return true;
    }

    /// <summary>UPhysicsManager::GetBehaviourPriority (00511510): the score a unit of that behaviour is worth.</summary>
    public static int BehaviourPriority(UnitBehaviour b) => b switch
    {
        UnitBehaviour.Mech or UnitBehaviour.Boat or UnitBehaviour.Building or UnitBehaviour.Fighter or UnitBehaviour.GroundAttackAircraft
            or UnitBehaviour.DiveBomber or UnitBehaviour.Pod => 10,
        UnitBehaviour.GroundUnit or UnitBehaviour.Turret or UnitBehaviour.Jeep => 5,
        UnitBehaviour.Bomber or UnitBehaviour.Dropship or UnitBehaviour.Submarine => 20,
        UnitBehaviour.Infantry or UnitBehaviour.Mine => 1,
        UnitBehaviour.Carrier or UnitBehaviour.HiveBoss or UnitBehaviour.ThunderHead or UnitBehaviour.Carver or UnitBehaviour.GillM
            or UnitBehaviour.Sentinel or UnitBehaviour.Warspite or UnitBehaviour.Fenrir or UnitBehaviour.WarspiteDome => 100,
        _ => 0,
    };

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
                if (Context.SoundEffects is { EventKernelConfigured: true } sounds)
                {
                    // Pristine74154bfa call004fbdad, after the preparation state writes and before charge effects.
                    sounds.PlayEffect(sounds.Find(System.Text.Encoding.Latin1.GetString(CurrentWeapon.Mode.PreFireSoundName)), this, Context.Crt,
                        new Sound.SoundPlayback(Volume: .7f, Tracking: Sound.SoundTracking.InitialPosition));
                }
                else Context.Skip("SOUND.PlayEffect (CUnit::Unknown86's pre-fire sound)");
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
