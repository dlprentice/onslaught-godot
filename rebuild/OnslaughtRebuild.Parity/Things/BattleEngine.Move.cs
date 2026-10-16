// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngine.cpp (CBattleEngine: Gravity 004074d0, UpdateMouseLookAngles 00407540,
// UpdateRotation 00407a50, IsWalkerOnGroundOrObject 004080f0, IsWalking 00408120, DeclareInWater 00408150, Move
// 004081c0, DeclareOnObject 0040c720, DeclareOnGround 0040c750, GetRadius 0040df80, COfGHeight 0040dfa0,
// ProcessDamageFlashes, HandleEngines 0040ebf0), in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.World;
using OnslaughtRebuild.Parity.Numerics;

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

    /// <summary>kWeaponCoolRate (weapon.h): what an overheated store cools by each move.</summary>
    public const float WeaponCoolRate = 0.5f;

    public BattleEngineWalkerPart? WalkerPart;
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
    public int EngineState;
    public int OldEngineState;
    public bool ThrustersOn;
    public bool InfinateEnergy;
    public FVector StandingOnObjectMovement;

    /// <summary>mPlayer.ToRead().</summary>
    public object? PlayerReader => Player;

    /// <summary>mSlowMovement (cleared by the constructor and by Init).</summary>
    public bool SlowMovement;

    /// <summary>mRecentLocks: the locks (or fired locks) when the weapon last fired.</summary>
    public int RecentLocks;

    /// <summary>ChargeWeapon (00409ef0): the walker's (the jet is not ported).</summary>
    public void ChargeWeapon()
    {
        if (State == BattleEngineState.Walker)
        {
            WalkerPart!.ChargeWeapon();
        }
        else if (State == BattleEngineState.Jet)
        {
            throw new NotSupportedException("CBattleEngineJetPart::ChargeWeapon is not ported");
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
            throw new NotSupportedException("CBattleEngineJetPart::FireWeapon is not ported");
        }
    }

    /// <summary>
    /// WeaponFired (0040c2e0): the jet part's weapons (none of the walker's) or the walker part's pay for the shot, and
    /// the cloak's stealth drops to 0.
    /// </summary>
    public bool WeaponFired(Weapon weapon)
    {
        if (WalkerPart!.WeaponFired(weapon))
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
        if (State is not (BattleEngineState.Jet or BattleEngineState.MorphingIntoJet))
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
                throw new NotSupportedException("CBattleEngineJetPart::Gravity (the jet part is not ported yet)");
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
        Context.Skip("CBattleEngine::MassiveHackPutUsInRightMesh");
        if (IsDying)
        {
            throw new NotSupportedException("CBattleEngine::Explode (not ported yet)");
        }

        float altitude = Pos.Z - Context.Map.WaterLevel;
        if (altitude > -0.2f && Vulnerable)
        {
            StartDieProcess();
        }

        base.DeclareInWater();
    }

    /// <summary>DeclareOnObject (0040c720).</summary>
    public override void DeclareOnObject(Thing? obj)
    {
        Context.Skip("CBattleEngine::MassiveHackPutUsInRightMesh");
        StandingOnThing.SetReader(obj);
        base.DeclareOnObject(obj);
    }

    /// <summary>The thing it stands on (mStandingOnThing).</summary>
    public ActiveReader<Thing> StandingOnThing { get; } = new();

    /// <summary>DeclareOnGround (0040c750): a hard landing damages it (not on an object, not dashing).</summary>
    public override void DeclareOnGround()
    {
        Context.Skip("CBattleEngine::MassiveHackPutUsInRightMesh");
        base.DeclareOnGround();
        if (IsDying)
        {
            throw new NotSupportedException("CBattleEngine::Explode (not ported yet)");
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

        float velocitySq = (Velocity.X * Velocity.X) + (Velocity.Y * Velocity.Y) + (Velocity.Z * Velocity.Z);
        if (velocitySq > cap * cap)
        {
            throw new NotSupportedException("CBattleEngine::DeclareOnGround: a hard landing's damage (not ported yet)");
        }

        if (State == BattleEngineState.Jet)
        {
            Velocity = new FVector(Velocity.X * 0.9f, Velocity.Y * 0.9f, Velocity.Z * 0.9f);
        }
    }

    /// <summary>The CBattleEngine part of Init that the movement needs: the parts and the starting values (00404dd0).</summary>
    private void InitMovement()
    {
        CurrentSafeTry = 0;
        WalkerPart = new BattleEngineWalkerPart(this);
        Context.Skip("CBattleEngineJetPart");
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
        Context.Skip("CBattleEngine::ProcessDamageFlashes (no damage flashes yet)");
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
            throw new NotSupportedException("CBattleEngine::Move while dying (not ported yet)");
        }

        if (Context.Game?.State > GameState.Panning)
        {
            if (MotionController is not null)
            {
                MotionController.AnimationBlend = 0.0f;
            }
        }

        if (AugActive)
        {
            throw new NotSupportedException("CBattleEngine::Move: the aug weapon (not ported yet)");
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
                throw new NotSupportedException("CBattleEngine::Move: morphing into a jet (not ported yet)");
            }

            WalkerPart!.Move();
        }
        else
        {
            throw new NotSupportedException("CBattleEngine::Move: the jet part (not ported yet)");
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
                else if (StoreValue[n] < Configuration.StoreValue[n] * 0.75f)
                {
                    StoreOverheat[n] = false;
                }
            }
        }

        Context.Skip("CBattleEngine::UpdateAutoAim (not ported yet)");
        Context.Skip("CBattleEngine::HandleSounds");
        if (ThrustersOn)
        {
            throw new NotSupportedException("CBattleEngine::Move: the thrusters (not ported yet)");
        }

        Context.Skip("CBattleEngine::DeactivateThrusters");
        ThrustersOn = false;
    }

    private Weapon? CurrentWeaponForZoom() => State == BattleEngineState.Jet ? null : WalkerPart?.GetCurrentWeapon();

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
            // Off the ground (or a jet): the pitch is damped beyond +-1.17 unless the jet part is looping. A walker's
            // jet part never loops; the jet is not ported.
            if (State == BattleEngineState.Jet)
            {
                throw new NotSupportedException("CBattleEngine::UpdateRotation for the jet (CBattleEngineJetPart::DoingLoop) is not ported");
            }

            if (CurrentOrientation.Pitch > 1.17f && Pitchvel > 0.0f)
            {
                float mult = 1.0f - ((CurrentOrientation.Pitch - 1.17f) * 1.5f);
                if (mult < 0.0f)
                {
                    mult = 0.0f;
                }

                Pitchvel *= mult;
            }

            if (CurrentOrientation.Pitch < -1.17f && Pitchvel < 0.0f)
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

        sounds.PlayEffect(sounds.Find("hud\\" + name), Context.Crt);
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
}
