// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/BattleEngine.cpp (CBattleEngine: Gravity 004074d0, UpdateMouseLookAngles 00407540,
// UpdateRotation 00407a50, IsWalkerOnGroundOrObject 004080f0, IsWalking 00408120, DeclareInWater 00408150, Move
// 004081c0, DeclareOnObject 0040c720, DeclareOnGround 0040c750, GetRadius 0040df80, COfGHeight 0040dfa0,
// ProcessDamageFlashes, HandleEngines 0040ebf0), in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class BattleEngine
{
    private const float Pi = 3.14159265358979f;
    private const float PiM2 = 6.28318530717958f;

    /// <summary>BATTLE_ENGINE_COFGHEIGHT.</summary>
    public const float CofGHeight = 1.9f;

    /// <summary>EEngineState: kAfterburnerEngines 0, kNormalEngines 1, kEnginesOff 2.</summary>
    public const int EnginesOff = 2;

    /// <summary>kBattleEngineStores.</summary>
    public const int Stores = 6;

    /// <summary>kWeaponCoolRate (weapon.h: <c>static int kWeaponCoolRate = 1</c>): what a heat store cools by each move.</summary>
    public const int WeaponCoolRate = 1;

    public BattleEngineWalkerPart? WalkerPart;
    public BattleEngineJetPart? JetPart;
    public float TransformStartTime = -20.0f;
    public float Yawvel;
    public float Rollvel;
    public float Pitchvel;
    public float DesiredZoom = 1.0f;
    public float OldZoom = 1.0f;
    public float OldLife;
    public float OldEnergy;
    public float LowArmourStartTime;
    public float LowEnergyStartTime;
    public float DangerStartTime;
    public float ZoomOutTime;
    public float TakeOffHeight;
    public float TakeOffTime;
    public float AugValue;
    public bool AugActive;
    public int NotMovingCount;
    public bool InSafeCollisionPlace = true;
    public FVector[] SafePos { get; } = new FVector[20];
    public float[] SafePosTime { get; } = new float[20];
    public int CurrentSafeTry;
    public bool Cloaked;
    public float YawShake;
    public float PitchShake;
    public float RollShake;
    public float ShakeR;
    public float PlayedIncomingWarheadSound;
    public float PlayedAmmunitionDepletedSound;
    public float PlayedWeaponOverheatedSound;
    public float[] StoreValue { get; } = new float[Stores];
    public bool[] StoreOverheat { get; } = new bool[Stores];
    public bool[] StoreHeat { get; } = new bool[Stores];
    public EulerAngles OldEulerAngles;
    public bool HitDamageDone;
    public float Stealth;
    public float DesiredStealth;
    public float Vibration;
    public float AmmoDepletedTime;
    public float WeaponOverheatedTime;

    /// <summary>mAugmentedTime: when the weapon was last augmented (-99999 from the constructor); the HUD's "augmented" line reads it.</summary>
    public float AugmentedTime = -99999.0f;

    /// <summary>mAugActiveTime: when the augmentation last started (-20 from the constructor).</summary>
    public float AugActiveTime;

    /// <summary>MAX_AUG_VALUE: the augmentation value that augments the weapon, and its value while augmented.</summary>
    public const float MaxAugValue = 10.0f;

    /// <summary>AUG_DECREASE_RATE: the augmentation drained each Move while augmented.</summary>
    public const float AugDecreaseRate = 0.01f;
    public int EngineState;
    public int OldEngineState;
    public bool ThrustersOn;
    public bool InfinateEnergy;

    /// <summary>SetInfinateEnergy (battleengine.h inline, 00405f20): CUnit's flag, then the energy filled to GetMaxEnergy.</summary>
    public void SetInfinateEnergy(bool value)
    {
        InfinateEnergy = value;
        Energy = Configuration!.Energy;
    }
    public FVector StandingOnObjectMovement;

    /// <summary>mPlayer.ToRead().</summary>
    public object? PlayerReader => Player;

    /// <summary>mSlowMovement (cleared by the constructor and by Init).</summary>
    public bool SlowMovement;

    /// <summary>mRecentLocks: the locks (or fired locks) when the weapon last fired.</summary>
    public int RecentLocks;

    /// <summary>ChargeWeapon (00409ef0): the selected stable form's weapon.</summary>
    public void ChargeWeapon()
    {
        if (State == BattleEngineState.Walker)
        {
            WalkerPart!.ChargeWeapon();
        }
        else if (State == BattleEngineState.Jet)
        {
            JetPart!.ChargeWeapon();
        }
    }

    /// <summary>FireWeapon (00409f20): full speed, the recent locks (none: locks are not ported), the part's fire.</summary>
    public void FireWeapon()
    {
        SlowMovement = false;
        Context.Skip("CBattleEngine::FireWeapon's lock counts (locks are not ported; read as none)");
        RecentLocks = 0;
        if (State == BattleEngineState.Walker)
        {
            WalkerPart!.FireWeapon();
        }
        else if (State == BattleEngineState.Jet)
        {
            JetPart!.FireWeapon();
        }
    }

    /// <summary>
    /// WeaponFired (0040c2e0): the jet part's weapons (none of the walker's) or the walker part's pay for the shot, and
    /// the cloak's stealth drops to 0.
    /// </summary>
    public bool WeaponFired(Weapon weapon)
    {
        if ((JetPart?.WeaponFired(weapon) ?? false) || WalkerPart!.WeaponFired(weapon))
        {
            Stealth = 0.0f;
            return true;
        }

        return false;
    }

    /// <summary>WeaponOverheated (0040f110): the warning time, at most every 4 seconds.</summary>
    public void WeaponOverheated()
    {
        if (WeaponOverheatedTime + 4.0f < Context.Events.Time)
        {
            WeaponOverheatedTime = Context.Events.Time;
        }
    }

    /// <summary>BounceFactor: 0.01 (BattleEngine.h).</summary>
    public override float BounceFactor() => 0.01f;

    /// <summary>ZoomModifier (00409e60): 1 - (1 - z).</summary>
    public float ZoomModifier(float z) => 1.0f - (1.0f - z);

    /// <summary>CActor::AddVelocity: mVelocity += v.</summary>
    public void AddVelocity(FVector v) => Velocity = new FVector(Velocity.X + v.X, Velocity.Y + v.Y, Velocity.Z + v.Z);

    /// <summary>Activate (00406000): powered up (the thing stays as it was).</summary>
    public override void Activate() => PoweredUp = true;

    /// <summary>Deactivate (00406020): powered down.</summary>
    public override void Deactivate() => PoweredUp = false;

    /// <summary>GetRenderPos (00406050): CActor's, raised by the centre-of-gravity height while the walker mesh is shown.</summary>
    public override FVector GetRenderPos()
    {
        FVector pos = base.GetRenderPos();
        // mHackRenderThingIsWalker: MassiveHackPutUsInRightMesh shows the jet mesh while jetting or morphing into a jet.
        if (RenderThingIsWalker)
        {
            pos.Z += COfGHeight();
        }

        return pos;
    }

    /// <summary>GetRenderOrientation (004060b0): CActor's; a walker (or morphing) engine is drawn by its yaw alone.</summary>
    public override FMatrix GetRenderOrientation()
    {
        FMatrix ori = base.GetRenderOrientation();
        if (State is BattleEngineState.Walker or BattleEngineState.MorphingIntoWalker or BattleEngineState.MorphingIntoJet)
        {
            return FMatrix.FromYawPitchRoll(EulerAngles.FromMatrix(ori).Yaw, 0.0f, 0.0f);
        }

        return ori;
    }

    /// <summary>COfGHeight (0040dfa0).</summary>
    public override float COfGHeight() => CofGHeight;

    /// <summary>GetRadius (0040df80): 0.4 outside multiplayer.</summary>
    public override float GetRadius() => 0.4f;

    /// <summary>Gravity (004074d0): the walker's own, a fifth of it while morphing into a walker; the jet's in jet mode.</summary>
    public override float Gravity()
    {
        switch (State)
        {
            case BattleEngineState.Walker:
            case BattleEngineState.MorphingIntoJet:
                return base.Gravity();
            case BattleEngineState.MorphingIntoWalker:
                return IsDying ? base.Gravity() : base.Gravity() * 0.2f;
            default:
                return JetPart!.Gravity();
        }
    }

    /// <summary>GetThreat (0040e8e0): 5 within 2 of the ground, else 0.</summary>
    public override float GetThreat() => Context.Map.Collide(Pos) - 2.0f < Pos.Z ? 5.0f : 0.0f;

    /// <summary>GetImportance (0040e910): 5 on the ground (not an object), else 0.</summary>
    public override float GetImportance() => IsOnGround() && !IsOnObject() ? 5.0f : 0.0f;

    /// <summary>IsWalkerOnGroundOrObject (004080f0).</summary>
    public bool IsWalkerOnGroundOrObject() => State == BattleEngineState.Walker && (IsOnGround() || IsOnObject());

    /// <summary>IsWalking (00408120): a walker on the ground within the last half second.</summary>
    public bool IsWalking() => State == BattleEngineState.Walker && Context.Events.Time - LastTimeOnGround < 0.5f;

    /// <summary>DeclareInWater (00408150): a battle engine in deep enough water dies.</summary>
    public override void DeclareInWater()
    {
        PutUsInRightMesh();
        if (IsDying)
        {
            Explode();
            AddShutdownEvent();
        }
        else if (Pos.Z - Context.Map.WaterLevel > -0.2f && Vulnerable)
        {
            StartDieProcess();
        }

        base.DeclareInWater();
    }

    /// <summary>DeclareOnObject (0040c720).</summary>
    public override void DeclareOnObject(Thing? obj)
    {
        PutUsInRightMesh();
        StandingOnThing.SetReader(obj);
        base.DeclareOnObject(obj);
    }

    /// <summary>The thing it stands on (mStandingOnThing).</summary>
    public ActiveReader<Thing> StandingOnThing { get; } = new();

    /// <summary>DeclareOnGround (0040c750): a hard landing damages it (not on an object, not dashing).</summary>
    public override void DeclareOnGround()
    {
        PutUsInRightMesh();
        base.DeclareOnGround();
        if (IsDying)
        {
            Explode();
            AddShutdownEvent();
            return;
        }

        if (IsOnObject())
        {
            return;
        }

        float cap = 0.2f;
        if (State == BattleEngineState.Walker)
        {
            if (WalkerPart!.GetDashCount() != 0)
            {
                return;
            }

            cap = 0.4f;
        }

        double velocitySq = X87.Add(X87.Add(X87.Mul(Velocity.X, Velocity.X), X87.Mul(Velocity.Y, Velocity.Y)),
            X87.Mul(Velocity.Z, Velocity.Z));
        if (velocitySq > X87.Mul(cap, cap))
        {
            float cosSquared = HardLandingSquaredCosine(Velocity, Context.Map.Normal(Pos));
            // 0040c8fa reloads the speed-squared spill; sqrt remains in the x87 register through both products.
            float damage = (float)X87.Mul(X87.Mul(X87.Sqrt((float)velocitySq), cosSquared), 16.0f);
            Damage(damage, null, false);
            // Reload velocity after Damage, which can itself start the death sequence.
            double scale = X87.Sub(1.0f, cosSquared);
            Velocity = new FVector((float)X87.Mul(Velocity.X, scale), (float)X87.Mul(Velocity.Y, scale),
                (float)X87.Mul(Velocity.Z, scale));
        }
        else if (State == BattleEngineState.Jet)
        {
            Velocity = new FVector(Velocity.X * 0.9f, Velocity.Y * 0.9f, Velocity.Z * 0.9f);
        }
    }

    /// <summary>
    /// DeclareOnGround0040c808..0040c8f4: normalize the terrain normal and velocity, then square their dot product.
    /// All normal components and velocity X/Y spill to float; velocity Z stays on x87. Equality OR unordered skips
    /// normalization. This local instruction sequence deliberately does not change shared vector normalization.
    /// </summary>
    internal static float HardLandingSquaredCosine(FVector velocity, FVector normal)
    {
        double length = X87.Sqrt(X87.Add(X87.Add(X87.Mul(normal.Z, normal.Z), X87.Mul(normal.Y, normal.Y)),
            X87.Mul(normal.X, normal.X)));
        if (length != 0 && !double.IsNaN(length))
        {
            double reciprocal = X87.Div(1.0f, length);
            normal = new FVector((float)X87.Mul(normal.X, reciprocal), (float)X87.Mul(normal.Y, reciprocal),
                (float)X87.Mul(normal.Z, reciprocal));
        }

        length = X87.Sqrt(X87.Add(X87.Add(X87.Mul(velocity.Y, velocity.Y), X87.Mul(velocity.Z, velocity.Z)),
            X87.Mul(velocity.X, velocity.X)));
        float x = velocity.X, y = velocity.Y;
        double z = velocity.Z;
        if (length != 0 && !double.IsNaN(length))
        {
            double reciprocal = X87.Div(1.0f, length);
            x = (float)X87.Mul(x, reciprocal);
            y = (float)X87.Mul(y, reciprocal);
            z = X87.Mul(z, reciprocal);
        }

        double dot = X87.Add(X87.Add(X87.Mul(z, normal.Z), X87.Mul(normal.Y, y)), X87.Mul(x, normal.X));
        return (float)X87.Mul(dot, dot);
    }

    /// <summary>The CBattleEngine part of Init that the movement needs: the parts and the starting values (00404dd0).</summary>
    private void InitMovement()
    {
        CurrentSafeTry = 0;
        WalkerPart = new BattleEngineWalkerPart(this);
        JetPart = new BattleEngineJetPart(this);
        StandingOnObjectMovement = new FVector(0, 0, 0);
        NotMovingCount = 0;
        TakeOffTime = 0.0f;
        ZoomOutTime = 0.0f;
        HitDamageDone = false;
        Stealth = 0.0f;
        DesiredStealth = 0.0f;
        Vibration = 0;
        InSafeCollisionPlace = true;
    }

    /// <summary>The part of Init after CUnit::Init that the movement needs.</summary>
    private void InitMovementAfterUnit()
    {
        Yawvel = 0.0f;
        Pitchvel = 0.0f;
        Rollvel = 0.0f;
        DesiredZoom = 1.0f;
        OldZoom = 1.0f;
        YawShake = 0.0f;
        RollShake = 0.0f;
        PitchShake = 0.0f;
        ShakeR = 0.0f;
        OldLife = Life;
        OldEnergy = Energy;
        LowArmourStartTime = -20.0f;
        LowEnergyStartTime = -20.0f;
        DangerStartTime = -20.0f;
        AugActiveTime = -20.0f;
        PlayedAmmunitionDepletedSound = -20.0f;
        PlayedIncomingWarheadSound = -20.0f;
        PlayedWeaponOverheatedSound = -20.0f;
        Array.Fill(SafePos, Pos);
        Array.Fill(SafePosTime, Context.Events.Time);
        ThrustersOn = false;
        for (int n = 0; n < Stores; n++)
        {
            StoreOverheat[n] = false;
            StoreHeat[n] = Configuration!.StoreHeat[n] != 0;
            StoreValue[n] = !StoreHeat[n] ? Configuration.StoreValue[n] : 0;
        }

        AugActive = false;
        AugValue = 0.0f;
        Cloaked = false;
        OldEulerAngles = CurrentOrientation;
        AmmoDepletedTime = -99999.0f;
        WeaponOverheatedTime = -99999.0f;
        EngineState = EnginesOff;
        OldEngineState = EnginesOff;
    }

    /// <summary>
    /// Move (004081c0): the damage flashes and engines, stealth, the safe positions, the warnings and zoom, the locks,
    /// the part for the mode, the not-moving count, gravity, the map's edges, CActor::Move, a thing it stands on, the
    /// step up onto an object, the rotation, the stores, auto-aim, sounds and thrusters.
    /// </summary>
    public override void Move()
    {
        ProcessDamageFlashes();
        HandleEngines();
        HitDamageDone = false;

        const float StealthSpeed = 5.0f;
        if (Stealth < DesiredStealth - StealthSpeed)
        {
            Stealth += StealthSpeed;
        }
        else if (Stealth > DesiredStealth + StealthSpeed)
        {
            Stealth -= StealthSpeed;
        }
        else
        {
            Stealth = DesiredStealth;
        }

        if (InSafeCollisionPlace)
        {
            if (CurrentSafeTry > 0)
            {
                CurrentSafeTry--;
                SafePos[CurrentSafeTry] = Pos;
                SafePosTime[CurrentSafeTry] = Context.Events.Time;
            }
            else
            {
                for (int i = SafePos.Length - 2; i >= 0; i--)
                {
                    SafePos[i + 1] = SafePos[i];
                    SafePosTime[i + 1] = SafePosTime[i];
                }

                SafePos[0] = Pos;
                SafePosTime[0] = Context.Events.Time;
            }
        }

        InSafeCollisionPlace = true;
        Vibration *= 0.7f;
        if (PlayerReader is not null && Context.Game?.Paused != true && !IsDying)
        {
            Context.Skip("CController::SetVibration");
        }

        if (IsDying)
        {
            MoveDyingBattleEngine();
        }
        else
        {
            if (Context.Game?.State > GameState.Panning)
            {
                if (MotionController is not null)
                {
                    MotionController.AnimationBlend = 0.0f;
                }
            }

            // The augmentation: drained while active and dropped when spent; full, it augments the weapon.
            if (AugActive)
            {
                AugValue -= AugDecreaseRate;
                if (AugValue <= 0)
                {
                    UnaugmentWeapon();
                }
            }
            else if (AugValue >= MaxAugValue)
            {
                AugmentWeapon();
            }

            if (Cloaked)
            {
                throw new NotSupportedException("CBattleEngine::Move: the cloak (not ported yet)");
            }

            float lowLife = Configuration!.Life / 4.0f;
            float lowEnergy = Configuration.Energy / 4.0f;
            float now = Context.Events.Time;
            if (OldLife > lowLife && Life <= lowLife)
            {
                LowArmourStartTime = now;
                PlayHudSample("hud_armour_low");
            }

            if (OldEnergy > lowEnergy && Energy <= lowEnergy)
            {
                LowEnergyStartTime = now;
                PlayHudSample("hud_energy_low");
            }

            if (Life <= lowLife && Life <= OldLife && now - LowArmourStartTime > 15.0f)
            {
                LowArmourStartTime = now;
                PlayHudSample("hud_armour_low");
            }

            if (Energy <= lowEnergy && Energy <= OldEnergy && now - LowEnergyStartTime > 15.0f)
            {
                LowEnergyStartTime = now;
                PlayHudSample("hud_energy_low");
            }

            if (DangerStartTime + 8.0f > now && PlayedIncomingWarheadSound + 8.0f < now)
            {
                PlayHudSample("hud_incoming_warhead");
                PlayedIncomingWarheadSound = now;
            }

            if (AmmoDepletedTime + 8.0f > now && PlayedAmmunitionDepletedSound + 8.0f < now)
            {
                PlayHudSample("hud_ammunition_depleted");
                PlayedAmmunitionDepletedSound = now;
            }

            if (WeaponOverheatedTime + 8.0f > now && PlayedWeaponOverheatedSound + 8.0f < now)
            {
                PlayHudSample("HUD_Weapon_Overheating");
                PlayedWeaponOverheatedSound = now;
            }

            OldLife = Life;
            OldEnergy = Energy;
            OldZoom = Zoom;
            if (CurrentWeaponForZoom() is not null)
            {
                Context.Skip("CBattleEngine::Move: the weapon's zoom mode (not ported yet)");
            }

            const float ZoomSpeed = 0.05f;
            if (MathF.Abs(DesiredZoom - Zoom) < ZoomSpeed)
            {
                Zoom = DesiredZoom;
            }
            else if (DesiredZoom > Zoom)
            {
                Zoom += ZoomSpeed;
            }
            else if (DesiredZoom < Zoom)
            {
                Zoom -= ZoomSpeed;
            }

            Context.Skip("CBattleEngine::HandleLocks (not ported yet)");
            if (State != BattleEngineState.Jet)
            {
                ThingTypeBits |= ThingType.GroundUnit | ThingType.Vehicle;
                ThingTypeBits &= ~ThingType.AirUnit;
            }
            else
            {
                ThingTypeBits |= ThingType.AirUnit;
                ThingTypeBits &= ~(ThingType.GroundUnit | ThingType.Vehicle);
            }

            if (State != BattleEngineState.Jet)
            {
                if (PlayerReader is not null)
                {
                    Context.Skip("CPlayer::GetPreferredCurrentViewMode (third person goes to the control view)");
                }

                if (State == BattleEngineState.MorphingIntoJet)
                {
                    AdvanceTakeoff();
                }

                WalkerPart!.Move();
            }
            else
            {
                JetPart!.Move();
            }
        }

        float speed = (float)X87.Sqrt((Velocity.X * Velocity.X) + (Velocity.Y * Velocity.Y) + (Velocity.Z * Velocity.Z));
        if (speed < 0.0001f)
        {
            NotMovingCount++;
            if (NotMovingCount > 5)
            {
                DeclareOnGround();
            }
        }
        else
        {
            NotMovingCount = 0;
        }

        Velocity.Z += Gravity();
        ClampToTheMap();
        ActorMove();
        if (IsStandingOnAMovingObject())
        {
            throw new NotSupportedException("CBattleEngine::Move: standing on a moving object (not ported yet)");
        }

        StandingOnObjectMovement = new FVector(0, 0, 0);

        // The forced movement (mPos less the position before the standing-on-object move): none, since that move throws
        // above.
        MotionController?.AddMovement(new FVector(0, 0, 0));
        // The step-up: a line 2.4 down from the centre; a walkable thing hit closer than the centre of gravity's height
        // lifts the battle engine onto it.
        var wlcr = new WorldLineReport();
        WorldLineQuery.FindFirstThingToHitLine(Context, Pos, Pos + new FVector(0.0f, 0.0f, 2.4f), this, wlcr, false, true, 2,
            ThingType.Ammunition, false);
        float dist = wlcr.DistToImpact;
        if (dist > 0.0f && dist < COfGHeight())
        {
            if (wlcr.HitThing is { } hitThing && hitThing.IsA(ThingType.CanBeWalkedOn))
            {
                Pos.Z += dist - COfGHeight();
                DeclareOnObject(hitThing);
                DeclareOnGround();
            }
        }

        UpdateRotation();
        for (int n = 0; n < Stores; n++)
        {
            if (StoreHeat[n])
            {
                StoreValue[n] -= WeaponCoolRate;
                if (StoreValue[n] < 0)
                {
                    StoreValue[n] = 0;
                }
                else if (StoreValue[n] < Configuration!.StoreValue[n] * 0.75f)
                {
                    StoreOverheat[n] = false;
                }
            }
        }

        UpdateAutoAim();
        HandleSounds();
        if (ThrustersOn)
        {
            // Burn things that are beneath you.
            for (MapWhoEntry? entry = Context.MapWho.GetFirstEntryWithinRadius(Pos, 4); entry is not null;
                 entry = Context.MapWho.GetNextEntryWithinRadius())
            {
                if (entry.Thing is { } thing && thing.IsA(ThingType.Unit) && thing != this && Pos.Z - 2.0f < thing.Pos.Z)
                {
                    FVector direction = thing.Pos - Pos;
                    float magnitude = direction.Magnitude();
                    if (magnitude < 2.5f)
                    {
                        thing.Damage(0.2f / magnitude, this, true, -1);
                    }
                }
            }

            GroundParticleEffect();
            ActivateThrusters();
        }
        else
        {
            DeactivateThrusters();
        }

        ThrustersOn = false;
    }

    /// <summary>
    /// GroundParticleEffect (0040ef20): within 10 of the ground or the water below, a land or water effect there, half as
    /// far again below as the altitude.
    /// </summary>
    internal void GroundParticleEffect()
    {
        float waterLevel = Context.Map.WaterLevel;
        float groundLevel = Context.Map.Collide(Pos);
        float altitude = waterLevel < groundLevel ? waterLevel - Pos.Z : groundLevel - Pos.Z;
        if (altitude < 10)
        {
            FVector particlePos = Pos;
            particlePos.Z += altitude * 1.5f;
            if (Context.Particles is { } particles)
            {
                var link = new Particles.ParticleEffectLink(particles.Manager);
                particles.Manager.AddParticle(particles.Manager.Set.GetPD(waterLevel < groundLevel
                    ? "BE Ground Effect Water Effect" : "BE Ground Effect Land Effect"), link);
                link.SetPos(particlePos);
                link.Release();
            }
            else
            {
                Context.Skip("CBattleEngine::GroundParticleEffect");
            }
        }
    }

    /// <summary>ActivateThrusters (0040e940): single player, only the in-flight sound when they start.</summary>
    internal void ActivateThrusters()
    {
        if (!ThrustersActive)
        {
            if (Context.SoundEffects is { } sounds)
            {
                sounds.PlayRepeatingEffect(sounds.Find("BE Engines(in-flight)"), this, Context.Crt, once: true);
            }
            else
            {
                Context.Skip("CBattleEngine::ActivateThrusters: the in-flight sound");
            }
        }

        ThrustersActive = true;
    }

    /// <summary>DeactivateThrusters (0040eb50): the in-flight sound fades (in the walker).</summary>
    internal void DeactivateThrusters()
    {
        if (!ThrustersActive) return;
        if (State == BattleEngineState.Walker && Context.SoundEffects is { } sounds)
            sounds.FadeRepeatingSample(sounds.Find("BE Engines(in-flight)"), this, 0, .02f);
        ThrustersActive = false;
    }

    private Weapon? CurrentWeaponForZoom() => GetCurrentWeapon();

    /// <summary>IsStandingOnAMoveingObject: on an object that is moving.</summary>
    private bool IsStandingOnAMovingObject()
    {
        if (!IsOnObject() || StandingOnThing.ToRead is not { } thing)
        {
            return false;
        }

        FVector v = thing.GetVelocity();
        return (v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z) > 0.0f;
    }

    /// <summary>The velocity clamps that stop the engine leaving the map (20 beyond its edges, 150 below).</summary>
    private void ClampToTheMap()
    {
        if (Pos.X < 0.0f)
        {
            if (Velocity.X < 0.0f)
            {
                Velocity.X *= Math.Max((20.0f + Pos.X) / 20.0f, 0.0f);
            }
        }
        else if (Pos.X > 512.0f && Velocity.X > 0.0f)
        {
            Velocity.X *= Math.Max(1.0f - ((Pos.X - 512.0f) / 20.0f), 0.0f);
        }

        if (Pos.Y < 0.0f)
        {
            if (Velocity.Y < 0.0f)
            {
                Velocity.Y *= Math.Max((20.0f + Pos.Y) / 20.0f, 0.0f);
            }
        }
        else if (Pos.Y > 512.0f && Velocity.Y > 0.0f)
        {
            Velocity.Y *= Math.Max(1.0f - ((Pos.Y - 512.0f) / 20.0f), 0.0f);
        }

        if (Pos.Z < -150.0f && Velocity.Z < 0.0f)
        {
            Velocity.Z *= Math.Max((140.0f + Pos.Z) / 10.0f, 0.0f);
        }
    }

    /// <summary>HandleEngines (0040ebf0): the engine particles while panning (presentation), else stopped.</summary>
    private void HandleEngines()
    {
        if (Context.Game?.State == GameState.Panning)
        {
            Context.Skip("CBattleEngine::HandleEngines: the engines' particles");
            OldEngineState = EngineState;
        }
        else
        {
            Context.Skip("CBattleEngine::StopEngineEffects");
        }
    }

    /// <summary>
    /// UpdateRotation (00407a50): a walker on the ground keeps its pitch near the ground's; then the turn rates apply,
    /// the angles wrap, the rates and (out of jet mode) the roll decay, and the orientation is rebuilt with the shake.
    /// </summary>
    public void UpdateRotation()
    {
        OldEulerAngles = CurrentOrientation;
        if (State == BattleEngineState.Walker && (IsOnGround() || IsOnObject()))
        {
            float groundPitch = GroundPitch();
            float d = CurrentOrientation.Pitch - groundPitch;
            if (MouseLookActive())
            {
            }
            else if (d > 0.5f)
            {
                if (d > 0.7f)
                {
                    Pitchvel -= (d - 0.7f) * 0.03f;
                }
                else if (Pitchvel > 0.0f)
                {
                    float mult = Math.Max(1.0f - ((d - 0.5f) * 6.0f), 0.0f);
                    Pitchvel *= mult;
                }
            }
            else if (d < -0.8f)
            {
                if (d < -1.0f)
                {
                    Pitchvel -= (d + 1.0f) * 0.03f;
                }
                else if (Pitchvel < 0.0f)
                {
                    float mult = Math.Max(1.0f - MathF.Abs((d + 0.8f) * 6.0f), 0.0f);
                    Pitchvel *= mult;
                }
            }

            Context.Skip("CBattleEngine::UpdateRotation's ground-pitch sequence (its orders are unchecked)");
        }
        else
        {
            // The jet-part loop flag suppresses both pitch limits in every airborne/morph state.
            if (CurrentOrientation.Pitch > 1.17f && Pitchvel > 0.0f && JetPart?.DoingLoop != true)
            {
                float mult = 1.0f - ((CurrentOrientation.Pitch - 1.17f) * 1.5f);
                if (mult < 0.0f)
                {
                    mult = 0.0f;
                }

                Pitchvel *= mult;
            }

            if (CurrentOrientation.Pitch < -1.17f && Pitchvel < 0.0f && JetPart?.DoingLoop != true)
            {
                float mult = 1.0f - MathF.Abs((CurrentOrientation.Pitch + 1.17f) * 1.5f);
                if (mult < 0.0f)
                {
                    mult = 0.0f;
                }

                Pitchvel *= mult;
            }
        }

        CurrentOrientation.Yaw += Yawvel;
        CurrentOrientation.Pitch += Pitchvel;
        CurrentOrientation.Roll += Rollvel;
        UpdateMouseLookAngles();
        CurrentOrientation.Yaw = Wrap(CurrentOrientation.Yaw);
        CurrentOrientation.Pitch = Wrap(CurrentOrientation.Pitch);
        CurrentOrientation.Roll = Wrap(CurrentOrientation.Roll);
        Yawvel *= 0.8f;
        Pitchvel *= 0.8f;
        Rollvel *= 0.8f;
        if (State != BattleEngineState.Jet)
        {
            CurrentOrientation.Roll *= 0.8f;
        }
        else if (JetPart!.AutoLevel()) CurrentOrientation.Roll *= 0.97f;

        Orientation = ShakenOrientation();
        if (MathF.Abs(YawShake) > 0.001f || MathF.Abs(PitchShake) > 0.001f || MathF.Abs(RollShake) > 0.001f)
        {
            YawShake *= 0.8f;
            PitchShake *= 0.8f;
            RollShake *= 0.8f;
            ShakeR += 0.8f;
        }
    }

    private static float Wrap(float a)
    {
        if (a > Pi)
        {
            a -= PiM2;
        }

        if (a < -Pi)
        {
            a += PiM2;
        }

        return a;
    }

    /// <summary>
    /// FMatrix(yaw + cos(shake) * yawShake, ...): the orientation with the shake added, as UpdateRotation compiles it
    /// (00408000..0040804b): fcos(mShakeR) stays on the FPU stack, each product with a shake and each sum with an angle
    /// rounds to 24 bits, then the out-of-line constructor (004062d0).
    /// </summary>
    private FMatrix ShakenOrientation()
    {
        double c = X87.Cos(ShakeR);
        return FMatrix.MakeRotationOutOfLine((float)X87.Add(X87.Mul(c, YawShake), CurrentOrientation.Yaw),
            (float)X87.Add(X87.Mul(c, PitchShake), CurrentOrientation.Pitch),
            (float)X87.Add(X87.Mul(c, RollShake), CurrentOrientation.Roll));
    }

    /// <summary>The ground's pitch under the engine's heading: -p.Z of (heading ^ normal) ^ normal, normalised.</summary>
    private float GroundPitch()
    {
        FMatrix ym = FMatrix.FromYawPitchRoll(CurrentOrientation.Yaw, 0, 0);
        FVector yv = ym.Transform(new FVector(0.0f, 1.0f, 0.0f));
        FVector mapNormal = IsOnGround() && !IsOnObject() ? Context.Map.Normal(Pos) : new FVector(0, 0, -1);
        FVector p = FVector.Cross(FVector.Cross(yv, mapNormal), mapNormal);
        p.Normalise();
        return -p.Z;
    }

    /// <summary>
    /// MouseLookActive (inline on the PC): a walker on the ground or an object, powered and alive, in a running game,
    /// with its turn (CONTROL_BINDINGS[1]) bound to a mouse axis for its player.
    /// </summary>
    private bool MouseLookActive()
    {
        if (Context.Game?.Paused == true || Context.Game?.State != GameState.Playing || IsDying || !PoweredUp
            || State != BattleEngineState.Walker)
        {
            return false;
        }

        if (Context.Bindings.InputType(1, 0) is not (Platform.PushType.MouseAxisMinus or Platform.PushType.MouseAxisPlus))
        {
            return false;
        }

        return IsOnGround() || IsOnObject();
    }


    /// <summary>
    /// UpdateMouseLookAngles (00407540): each call counts a frame; with mouse look active, a call straight after the last
    /// active one turns the engine by the mouse's offset from the window's centre (yaw, and half as much pitch, inverted
    /// unless the career inverts Y), rebuilds the orientation and clamps the pitch to the ground's pitch -0.9..+0.6;
    /// every active call recentres the mouse.
    /// </summary>
    private void UpdateMouseLookAngles()
    {
        MouseLookState m = Context.MouseLook;
        m.Frame++;
        if (!MouseLookActive())
        {
            return;
        }

        if (m.Frame == m.LastFrame + 1)
        {
            float k = m.Sensitivity * 0.002f;
            CurrentOrientation.Yaw -= (float)(m.MouseX - (m.WindowWidth / 2)) * k;
            float pitch = (float)(m.MouseY - (m.WindowHeight / 2)) * k * 0.5f;
            if (m.InvertY)
            {
                CurrentOrientation.Pitch += pitch;
            }
            else
            {
                CurrentOrientation.Pitch -= pitch;
            }

            Orientation = ShakenOrientation();
            OldOrientation = Orientation;
            Context.Skip("CBattleEngine::UpdateMouseLookAngles's ground pitch (MakeRotationYawF and its orders are unchecked)");
            float groundPitch = GroundPitch();
            if (CurrentOrientation.Pitch - groundPitch > 0.6f)
            {
                CurrentOrientation.Pitch = groundPitch + 0.6f;
            }
            else if (CurrentOrientation.Pitch - groundPitch < -0.9f)
            {
                CurrentOrientation.Pitch = groundPitch - 0.9f;
            }
        }

        m.MouseX = m.WindowWidth / 2;
        m.MouseY = m.WindowHeight / 2;
        m.LastFrame = m.Frame;
    }

    /// <summary>PlayHudSample(name) (0040d5f0): the effect "hud\\" + name through PlayEffect, which draws rand for its chain and pitch.</summary>
    private void PlayHudSample(string name)
    {
        if (Context.SoundEffects is not { } sounds)
        {
            Context.Skip("CBattleEngine::PlayHudSample");
            return;
        }

        PlayHudEffect(sounds.Find("hud\\" + name));
    }

    // Both native HUD overloads keep the BE as monitored owner while ignoring its spatial position.
    private void PlayHudEffect(SoundEffect? effect)
    {
        if (Context.SoundEffects is not { } sounds) return;
        if (sounds.EventKernelConfigured)
            sounds.PlayEffect(effect, this, Context.Crt,
                new SoundPlayback(Volume: sounds.HudMessageVolume, IgnoreOwnerPosition: true));
        else sounds.PlayEffect(effect, Context.Crt); // explicit selection-only legacy profile
    }

    /// <summary>
    /// TestDanger (0040dda0), from the HUD's render: at most every 8 seconds, standing in a cell either side's fear grid
    /// fears starts the danger warning (Move plays its sample).
    /// </summary>
    public void TestDanger()
    {
        float now = Context.Events.Time;
        if (DangerStartTime < now - 8.0f)
        {
            bool danger = false;
            if (IsOnGround())
            {
                if (Context.Game?.ForsetiFearGrid?.InDanger(Pos) == true)
                {
                    danger = true;
                }

                if (Context.Game?.MuspellFearGrid?.InDanger(Pos) == true)
                {
                    danger = true;
                }
            }

            if (danger)
            {
                DangerStartTime = now;
            }
        }
    }

    /// <summary>mAutoAimTarget: the target HandleAutoAim chose.</summary>
    public ActiveReader<Thing> AutoAimTarget { get; } = new();

    /// <summary>mHadAutoAimTarget: what the aim sound last played for.</summary>
    public ActiveReader<Thing> HadAutoAimTarget { get; } = new();

    /// <summary>mWalkSoundTime: the distance walked since the last step's count.</summary>
    public float WalkSoundTime;

    /// <summary>mWalkSounds: the steps counted since the battle engine last stood still.</summary>
    public int WalkSounds;

    /// <summary>
    /// HandleSounds (00409950). The configured process sound owner handles hum, warnings and incoming sample
    /// admission/lifetime; the legacy selection-only profile still refuses lifetime-dependent incoming/hum paths.
    /// Weapon-lock completion sounds remain with the unimplemented lock list. The aim sound plays when the
    /// auto-aim target is first set, or else the unit over the crosshair (CalcUnitOverCrossHair) is first an enemy. The walking block: on the ground as a walker, every 1.5 units walked
    /// counts a step, and standing still after more than 5 steps plays the hydraulics (PlayHudSample).
    /// </summary>
    internal void HandleSounds()
    {
        if (Context.SoundEffects is not { } sounds)
        {
            Context.Skip("CBattleEngine::HandleSounds");
            return;
        }

        if (sounds.Find("BE On 02") is { } hum)
        {
            if (!sounds.EventKernelConfigured)
                throw new NotSupportedException("CBattleEngine::HandleSounds: the hum requires configured sound-event lifetime");
            if (!sounds.IsEffectPlaying(hum, this)) PlayHudEffect(hum);
        }

        var healthLow = sounds.Find("BE Energy Critical");
        var energyLow = sounds.Find("BE Energy Low");
        // mEnergyVeryLowSound is NULL in the retail Init (00404dd0); its old source branch is inactive.
        if (Life < 7.0f)
        {
            sounds.StopRepeatingSample(energyLow, this);
            if (!sounds.IsRepeatingEffectPlaying(healthLow, this)) sounds.PlayRepeatingEffect(healthLow, this, Context.Crt);
        }
        else if (Energy < 2.0f)
        {
            sounds.StopRepeatingSample(healthLow, this);
            if (!sounds.IsRepeatingEffectPlaying(energyLow, this)) sounds.PlayRepeatingEffect(energyLow, this, Context.Crt);
        }
        else
        {
            sounds.FadeRepeatingSample(energyLow, this, 0, 0.02f);
            sounds.FadeRepeatingSample(healthLow, this, 0, 0.02f);
        }

        // 00409b3f..00409b96 consumes this flag only after querying the nonloop effect's playback state.
        // A loop-only query or an unconditional draw would invent timing and RNG behavior.
        if (PlayIncomingMissileSound)
        {
            if (!sounds.EventKernelConfigured)
                throw new NotSupportedException("CBattleEngine::HandleSounds: incoming missile playback needs the configured sound event owner");
            SoundEffect? incoming = sounds.Find("BE Incoming Missile");
            if (!sounds.IsEffectPlaying(incoming, this)) PlayHudEffect(incoming);
            PlayIncomingMissileSound = false;
        }

        // Aiming: a new auto-aim target, or else an enemy unit coming under the crosshair, plays the aim sound once.
        if (AutoAimTarget.ToRead is { } aimTarget)
        {
            if (HadAutoAimTarget.ToRead is null)
            {
                PlayHudEffect(sounds.Find("BE Target"));
                HadAutoAimTarget.SetReader(aimTarget);
            }
        }
        else if (CurrentUnitOverCrosshair.ToRead is { } over && IsTargetAlligence(over.Allegiance))
        {
            if (HadAutoAimTarget.ToRead is null)
            {
                PlayHudEffect(sounds.Find("BE Target"));
                HadAutoAimTarget.SetReader(over);
            }
        }
        else
        {
            HadAutoAimTarget.SetReader(null);
        }

        if (IsOnGround() && State == BattleEngineState.Walker)
        {
            float magnitude = Velocity.Magnitude();
            WalkSoundTime += magnitude;
            if (WalkSoundTime > 1.5f)
            {
                WalkSoundTime -= 1.5f;
                WalkSounds++;
            }

            if (magnitude < 0.01f)
            {
                if (WalkSounds > 5)
                {
                    PlayHudEffect(sounds.Find("BE Hydraulics 02"));
                }

                WalkSounds = 0;
            }
        }
        else
        {
            WalkSounds = 0;
        }
    }

    /// <summary>mOldAutoAimYawOffset / mOldAutoAimPitchOffset: the last update's offsets (the HUD interpolates).</summary>
    public float OldAutoAimYawOffset, OldAutoAimPitchOffset;

    /// <summary>mDesiredAutoAimYawOffset / mDesiredAutoAimPitchOffset.</summary>
    public float DesiredAutoAimYawOffset, DesiredAutoAimPitchOffset;

    /// <summary>mTrackingYawOffset / mTrackingPitchOffset and their last update's values; mTrackingActive.</summary>
    public float TrackingYawOffset, TrackingPitchOffset, OldTrackingYawOffset, OldTrackingPitchOffset;

    public bool TrackingActive;

    /// <summary>
    /// UpdateAutoAim (0040b120): towards the auto-aim target's targetable point, predicted along its velocity by the
    /// round's flight time for a predicting weapon (a lead over 0.2 rad becomes the tracking offsets instead); the aim
    /// offsets then ease half way to the desired ones.
    /// </summary>
    private void UpdateAutoAim()
    {
        Weapon? weapon = GetCurrentWeapon();
        float currentYaw = CurrentOrientation.Yaw;
        float currentPitch = CurrentOrientation.Pitch;
        OldTrackingYawOffset = TrackingYawOffset;
        OldTrackingPitchOffset = TrackingPitchOffset;
        if (AutoAimTarget.ToRead is { } target)
        {
            FVector op = target is Unit unit && target.IsA(ThingType.Unit) ? unit.GetTargetablePos() : target.GetCentrePos();
            if (weapon!.CanPredict())
            {
                FVector bp = Pos + GetVelocity();
                var launchVector = new FVector(0, weapon.GetLaunchVelocity(), 0);
                FVector bv = Orientation.Transform(launchVector);
                FVector ov = target.GetVelocity();
                float t = (bp - op).Magnitude() / bv.Magnitude() * 20.0f;
                FVector predictedPos = op + (ov * t);
                FVector diff = predictedPos - (Pos + GetVelocity());
                DesiredAutoAimYawOffset = -WeaponMaths.AngleDifference(currentYaw, WeaponMaths.AzimuthUnrounded(diff));
                DesiredAutoAimPitchOffset = -WeaponMaths.AngleDifference(currentPitch, WeaponMaths.ElevationUnrounded(diff));
                if (MathF.Abs(DesiredAutoAimYawOffset) + MathF.Abs(DesiredAutoAimPitchOffset) > 0.2f)
                {
                    TrackingYawOffset = DesiredAutoAimYawOffset;
                    TrackingPitchOffset = DesiredAutoAimPitchOffset;
                    if (!TrackingActive)
                    {
                        OldTrackingYawOffset = TrackingYawOffset;
                        OldTrackingPitchOffset = TrackingPitchOffset;
                    }

                    TrackingActive = true;
                    DesiredAutoAimYawOffset = 0;
                    DesiredAutoAimPitchOffset = 0;
                }
                else
                {
                    TrackingActive = false;
                }
            }
            else
            {
                FVector diff = op - (Pos + GetVelocity());
                DesiredAutoAimYawOffset = -WeaponMaths.AngleDifference(currentYaw, WeaponMaths.AzimuthUnrounded(diff));
                DesiredAutoAimPitchOffset = -WeaponMaths.AngleDifference(currentPitch, WeaponMaths.ElevationUnrounded(diff));
                TrackingActive = false;
            }
        }
        else
        {
            DesiredAutoAimYawOffset = 0.0f;
            DesiredAutoAimPitchOffset = 0.0f;
            TrackingActive = false;
        }

        OldAutoAimYawOffset = AutoAimYawOffset;
        OldAutoAimPitchOffset = AutoAimPitchOffset;
        // On the x87 the sum keeps the extended exponent range and rounds to a denormal float only at its store: the
        // eased offsets halve towards zero through denormals exactly (retail win1 P4571 pitch 003daf78).
        AutoAimYawOffset = (float)X87.Add(AutoAimYawOffset, X87.Mul(X87.Sub(DesiredAutoAimYawOffset, AutoAimYawOffset), 0.5));
        AutoAimPitchOffset = (float)X87.Add(AutoAimPitchOffset, X87.Mul(X87.Sub(DesiredAutoAimPitchOffset, AutoAimPitchOffset), 0.5));
    }

    /// <summary>mCurrentTarget: the fired-lock index GetCurrentTarget walks (ResetTarget zeroes it).</summary>
    public int CurrentTarget;

    /// <summary>
    /// GetCurrentTarget (004071b0): the counter moves on; without recent locks there is none (the locks are not ported,
    /// so with recent locks it throws).
    /// </summary>
    public override Thing? GetCurrentLaunchTarget()
    {
        CurrentTarget++;
        if (RecentLocks == 0)
        {
            return null;
        }

        throw new NotSupportedException("CBattleEngine::GetCurrentTarget with recent locks (the locks are not ported)");
    }

    /// <summary>DisplayLock (00407310): whether the weapon is the current one.</summary>
    public bool DisplayLock(Weapon weapon) => ReferenceEquals(GetCurrentWeapon(), weapon);

    /// <summary>FireLock (00407060): the target's due lock moves to the fired locks (the lock lists are not ported: empty).</summary>
    public void FireLock(Thing? unit)
    {
        if (unit is not null)
        {
            Context.Skip("CBattleEngine::FireLock's lock lists (not ported; empty)");
        }
    }

    /// <summary>RecoilWeapon (0040c340): a shock shake of the mode's power, and twice that in vibration.</summary>
    public void RecoilWeapon(Weapon weapon)
    {
        AddShockShake(weapon.Mode!.Power);
        Vibration += weapon.Mode.Power * 2.0f;
    }

    /// <summary>
    /// AddShockShake (00407940): from 0.001, clamped to 0.75, three GAME random kicks of yaw, pitch and roll, the shake
    /// restarted, the cockpit's own shake (main-thread rand), and the pad's rumble.
    /// </summary>
    public void AddShockShake(float amount)
    {
        if (amount < 0.001)
        {
            return;
        }

        if (amount > 0.75f)
        {
            amount = 0.75f;
        }

        float s = 16.0f / amount;
        float s1 = 1.0f * amount;
        YawShake = ((float)(Context.Random.Random() % 32) / s) - s1;
        PitchShake = ((float)(Context.Random.Random() % 32) / s) - s1;
        RollShake = ((float)(Context.Random.Random() % 32) / s) - s1;
        ShakeR = 0.0f;
        Cockpit?.AddShockShake(amount);
        Context.Skip("PLATFORM.TriggerRumble");
    }

    /// <summary>
    /// GetLaunchPosition (0040c990): the cockpit's "Gun" emitter at that index (else the weapon's primary point); for the
    /// orientation, a ballistic weapon pitches to reach where the crosshair's line met something (at -45 degrees else) on
    /// the battle engine's yaw plus the aim offset; a forward-facing weapon that adjusts its aim points at that place, or
    /// along the battle engine's orientation turned by the aim offsets when the line met nothing.
    /// </summary>
    public override void GetLaunchPosition(Weapon weapon, int index, out FVector pos, out FMatrix orientation, bool needOrientation)
    {
        pos = new FVector(0, 0, 0);
        orientation = FMatrix.Identity;
        if (Cockpit?.RenderThing is { } rt)
        {
            (pos, orientation) = Mesh.MeshPose.GetRTEmitter(rt.Mesh, rt.Pose, Cockpit, "Gun", index, Context.TransformStatics, false, true);
        }

        if (pos.Equals(new FVector(0, 0, 0)))
        {
            pos = weapon.GetPrimaryPosition();
            orientation = weapon.GetPrimaryOrientation();
        }

        if (!needOrientation)
        {
            return;
        }

        FMatrix autoAim = FMatrix.FromYawPitchRoll(AutoAimYawOffset, AutoAimPitchOffset, 0.0f);
        Game.Camera? camera = (Player as Game.Player) is { } player ? Context.Game?.GetCurrentCamera(player.Number - 1) : null;
        FVector camPos = camera?.GetPos() ?? new FVector(0, 0, 0);
        FMatrix camOri = camera?.GetOrientation() ?? FMatrix.Identity;
        if (weapon.ValidRound() && WeaponMaths.IsBallistic(weapon.Mode!.Round!))
        {
            throw new NotSupportedException("CBattleEngine::GetLaunchPosition for a ballistic weapon (CalculateDesiredPitch's search) is not ported");
        }

        FVector vectorA = weapon.GetPrimaryOrientation().Transform(new FVector(0, 1, 0));
        FVector vectorB = orientation.Transform(new FVector(0, 1, 0));
        float angle = (vectorA.X * vectorB.X) + (vectorA.Y * vectorB.Y) + (vectorA.Z * vectorB.Z);
        if (angle > 0.9f && weapon.Data.AdjustAim != 0)
        {
            FVector endPoint = camPos + camOri.Multiply(autoAim).Transform(new FVector(0.0f, 200.0f, 0.0f));
            if (Wlcr.HitType != World.LineHitType.Nothing)
            {
                FVector targetVector = endPoint - camPos;
                FVector hitPoint = (targetVector * (Wlcr.DistToImpact / targetVector.Magnitude())) + camPos;
                FVector adjusted = hitPoint - pos;
                orientation = FMatrix.FromYawPitchRoll(WeaponMaths.Azimuth(Context, adjusted), WeaponMaths.Elevation(Context, adjusted), 0.0f);
            }
            else
            {
                orientation = Orientation.Multiply(autoAim);
            }
        }
    }

    /// <summary>ConfirmedKill (0040a560): a Muspell unit killed counts for the player.</summary>
    public override void ConfirmedKill(Unit killed)
    {
        if (killed.Allegiance != 1)
        {
            return;
        }

        (Player as Game.Player)?.KilledEnemyThing(killed);
    }

    /// <summary>CBattleEngine::StartDieProcess0040bfd0; BE-specific loss/script/explosion/smoke ordering.</summary>
    public override bool StartDieProcess() => StartBattleEngineDeath();

    /// <summary>GetSoundMaterial (00405f40): SM_BATTLE_ENGINE.</summary>
    public override int GetSoundMaterial() => 0x68;

    /// <summary>Rearm (0040ac50): each ammunition store (not heat) gains that share of its full amount, up to full.</summary>
    public void Rearm(float amount)
    {
        if (amount > 0)
        {
            for (int n = 0; n < StoreValue.Length; n++)
            {
                if (!StoreHeat[n])
                {
                    StoreValue[n] += amount * Configuration!.StoreValue[n];
                    if (StoreValue[n] > Configuration.StoreValue[n])
                    {
                        StoreValue[n] = Configuration.StoreValue[n];
                    }
                }
            }
        }
    }
}
