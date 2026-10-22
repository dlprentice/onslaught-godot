// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/BattleEngineJetPart.cpp, CBattleEngineJetPart controls (00410310..004109d0),
// Gravity004114d0, GetIsDoingSpecialAirMove00411b70, AutoLevel00412900 and weapon ownership00411b90..00412830.
// The scalar control operations retain the instruction order in pristine PC SHA-25674154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>The jet's control state and its own ordered weapon collection.</summary>
public sealed class BattleEngineJetPart
{
    private readonly BattleEngine _main;
    private ThingContext Context => _main.Context;
    public BattleEngineJetPart(BattleEngine main)
    {
        _main = main;
        ResetConfiguration();
    }

    public List<Weapon> Weapons { get; } = [];
    public int CurrentWeapon;
    public float OnGround;
    public float ThrusterValue = 0.5f;
    public float LastMoveYVal, LastMoveXVal;
    public bool DoingLoop, LoopHalfway, LoopBroken, DoingBarrelLeft;
    // Despite its name this is a float in the original layout (+0x48).
    public float DoingBarrelCount;
    public float LastStartHardBackwardTime = -10.0f, LastStartHardForwardTime = -10.0f;
    public float LastStartHardLeftTime = -10.0f, LastStartHardRightTime = -10.0f;
    public float StrafingStartTime;

    public bool IsDoingSpecialAirMove() => DoingLoop || DoingBarrelCount != 0;
    public float Gravity() => _main.Energy == 0 ? 0.005f : 0.0f;

    public bool AutoLevel() => !(_main.IsOnGround() && _main.Velocity.MagnitudeSq() < 0.010000001f)
        && !(_main.Energy < 0) && !(DoingBarrelCount > 0);

    public void Thrust(float value)
    {
        if (DoingBarrelCount > 0) return;
        if (!DoingLoop && _main.Energy != 0)
        {
            ThrusterValue = 0.5f - value * 0.5f;
            if (LastMoveYVal > -0.6f && value < -0.9f) LastStartHardForwardTime = Context.Events.Time;
            if (value > 0.8f && InDoubleTapWindow(LastStartHardForwardTime))
            {
                if (_main.Energy > 0)
                {
                    if (_main.Velocity.MagnitudeSq() > 0.09f)
                    {
                        DoingLoop = true;
                        LoopHalfway = false;
                        LoopBroken = false;
                        _main.Pitchvel -= 0.015f;
                    }
                }
                else _main.LowEnergyStartTime = Context.Events.Time;
            }
        }
        LastMoveYVal = value;
    }

    private bool InDoubleTapWindow(float start) => start > Context.Events.Time - 0.2f && start < Context.Events.Time - 0.1f;

    public void Turn(float value)
    {
        if (DoingLoop || DoingBarrelCount != 0) return;
        FVector v = _main.Velocity;
        // This first site sums Z² + X² + Y² (004104ca..004104d8); subsequent speed sites sum X² + Y² + Z².
        if (!(((v.Z * v.Z) + (v.X * v.X)) + (v.Y * v.Y) > 0.0001f)) return;
        float yaw = ((value * _main.Configuration!.AirTurnRate) * 0.010638298f) * _main.ZoomModifier(_main.Zoom);
        float roll = (value * 0.008547009f) * _main.ZoomModifier(_main.Zoom);
        if (v.MagnitudeSq() < 1.0f && _main.IsOnGround())
        {
            float percent = (float)X87.Sqrt(v.MagnitudeSq());
            if (percent < 0.1f) percent = 0;
            yaw *= percent;
            roll *= percent;
        }
        if (_main.SlowMovement) { yaw *= 0.25f; roll *= 0.25f; }
        if (_main.TransformStartTime + 1.5f > Context.Events.Time)
        {
            float mix = (Context.Events.Time - _main.TransformStartTime) * 0.6666667f;
            yaw *= mix;
            roll *= mix;
        }
        _main.Yawvel -= yaw;
        _main.Rollvel -= roll;
    }

    public void Pitch(float value)
    {
        if (DoingLoop || DoingBarrelCount != 0 || !(_main.Velocity.MagnitudeSq() > 0.0001f)) return;
        float rate = (value * 0.008547009f) * _main.ZoomModifier(_main.Zoom);
        if (_main.SlowMovement) rate *= 0.25f;
        if (_main.TransformStartTime + 1.5f > Context.Events.Time)
            rate *= (Context.Events.Time - _main.TransformStartTime) * 0.6666667f;
        _main.Pitchvel -= rate;
    }

    /// <summary>YawLeft00410740 / YawRight004109d0: sideways thrust and the opposing-tap barrel/loop controls.</summary>
    public void YawLeft(float value) => Yaw(value, left: true);
    public void YawRight(float value) => Yaw(value, left: false);

    private void Yaw(float value, bool left)
    {
        if (DoingBarrelCount != 0) return;
        bool hard = left ? LastMoveXVal > -0.9f && value < -0.9f : LastMoveXVal < 0.9f && value > 0.9f;
        if (DoingLoop)
        {
            if (LoopBroken) return;
            if (hard)
            {
                if (left) LastStartHardLeftTime = Context.Events.Time;
                else LastStartHardRightTime = Context.Events.Time;
            }
            // Both retail functions use a POSITIVE value here, including YawLeft.
            if (value > 0.8f && InDoubleTapWindow(left ? LastStartHardRightTime : LastStartHardLeftTime)) LoopBroken = true;
            return;
        }
        if (hard)
        {
            if (left) LastStartHardLeftTime = Context.Events.Time;
            else LastStartHardRightTime = Context.Events.Time;
        }
        bool crossed = left ? LastMoveXVal > -0.8f && value < -0.8f : LastMoveXVal < 0.8f && value > 0.8f;
        if (crossed && InDoubleTapWindow(left ? LastStartHardRightTime : LastStartHardLeftTime))
        {
            if (_main.Energy > 0)
            {
                if (_main.Velocity.MagnitudeSq() > 0.09f)
                {
                    DoingBarrelCount = 26;
                    FMatrix yaw = FMatrix.MakeRotationOutOfLine(_main.CurrentOrientation.Yaw, 0, 0);
                    float side = left ? -0.2f : 0.2f;
                    _main.AddVelocity(new FVector(yaw.Row0.X * side, yaw.Row1.X * side, yaw.Row2.X * side));
                    if (left) DoingBarrelLeft = true; // right does not write this flag
                }
            }
            else _main.LowEnergyStartTime = Context.Events.Time;
        }
        LastMoveXVal = value;
        if (_main.Energy != 0 && MathF.Abs(value) > 0.5f)
        {
            float side = value * 0.0033333334f;
            FMatrix ori = _main.Orientation;
            _main.AddVelocity(new FVector(ori.Row0.X * side, ori.Row1.X * side, ori.Row2.X * side));
            StrafingStartTime = Context.Events.Time;
        }
    }

    /// <summary>Move00410c50: the jet's energy, thrust, alignment, landing and special-manoeuvre state machine.</summary>
    public void Move()
    {
        (_main.Player as Game.Player)?.IncStat(Game.PlayerStat.TimeAsJet);
        foreach (Weapon weapon in Weapons) weapon.MoveEmitter(true);
        float now = Context.Events.Time;
        var config = _main.Configuration!;
        if (!_main.InfinateEnergy && _main.Energy != 0)
        {
            float cost = (config.MaxAirEnergyCost - config.MinAirEnergyCost) * ThrusterValue + config.MinAirEnergyCost;
            _main.Energy -= cost;
            if (_main.Energy < 0) _main.Energy = 0;
        }
        _main.EngineState = _main.Energy != 0 ? (ThrusterValue > 0.75f ? 0 : 1) : BattleEngine.EnginesOff;
        if (_main.Energy != 0 && !_main.IsOnGround())
        {
            if (_main.TransformStartTime + 2.5f < now)
            {
                if (!_main.Stalling)
                {
                    if (_main.Velocity.MagnitudeSq() < 0.022500001f) { _main.Stalling = true; _main.StallTime = now; }
                }
                else if (_main.Velocity.MagnitudeSq() >= 0.022500001f) _main.Stalling = false;
                else if (_main.StallTime + 2.5f < now) _main.Morph();
            }
            else _main.Stalling = false;
            HandleGroundEffect();
            if (!IsDoingSpecialAirMove())
            {
                float target = (config.MaxAirVelocity - config.MinAirVelocity) * ThrusterValue + config.MinAirVelocity;
                float forward = (target - _main.Velocity.Magnitude()) * 0.04f;
                _main.AddVelocity(ForwardColumn(forward));
            }
            ThrusterValue = 0.5f;
            if (DoingBarrelCount == 0)
            {
                FVector velocity = _main.Velocity;
                FVector next = ForwardColumn(velocity.Magnitude());
                if (_main.TransformStartTime + 2.5f > now)
                {
                    float mix = (now - _main.TransformStartTime) * 0.4f;
                    next = velocity * (1.0f - mix) + next * mix;
                }
                else if (StrafingStartTime + 4.0f > now)
                {
                    float mix = (now - StrafingStartTime) * 0.25f;
                    next = velocity * (1.0f - mix) + next * mix;
                }
                _main.Velocity = next * GetFriction();
            }
            OnGround = 0;
        }
        else
        {
            FVector velocity = _main.Velocity * 0.95f;
            _main.Velocity = velocity;
            // 004110e2..00411108: this site's sum is (Z² + X²) + Y².
            float speed = (float)X87.Sqrt((velocity.Z * velocity.Z + velocity.X * velocity.X) + velocity.Y * velocity.Y);
            _main.AddVelocity(ForwardColumn(speed) * 0.05f);
        }
        if (_main.IsOnGround() && _main.Velocity.MagnitudeSq() < 0.010000001f && OnGround == 0)
            OnGround = now + 2.5f;
        if (_main.TransformStartTime + 1.0f <= now
            && ((now > OnGround && OnGround != 0) || _main.Velocity.MagnitudeSq() < 0.00062500004f))
            _main.Morph();
        if (DoingBarrelCount > 0)
        {
            if (DoingBarrelCount < 10) _main.Velocity *= 0.9f;
            _main.Rollvel += DoingBarrelLeft ? 0.05f : -0.05f;
            DoingBarrelCount -= 1.0f;
            if (DoingBarrelCount == 0)
            {
                if (LoopBroken)
                {
                    _main.CurrentOrientation = EulerAngles.FromMatrix(_main.Orientation);
                    _main.Orientation = OrientationAfterBrokenLoop();
                    LoopBroken = false;
                    DoingLoop = false;
                }
                DoingBarrelLeft = false;
            }
        }
        if (DoingLoop && DoingBarrelCount == 0)
        {
            _main.Pitchvel -= 0.015f;
            if (LoopHalfway)
            {
                if (_main.CurrentOrientation.Pitch < 0.4f && _main.CurrentOrientation.Pitch > -1.5707964f) DoingLoop = false;
            }
            else if (_main.CurrentOrientation.Pitch < -2.7415926f)
            {
                LoopHalfway = true;
                if (LoopBroken) DoingBarrelCount = 13;
            }
        }
        _main.Shields = 0;
        _main.GroundParticleEffect();
        HandleSkimming();
    }

    private FVector ForwardColumn(float value) => new(_main.Orientation.Row0.Y * value,
        _main.Orientation.Row1.Y * value, _main.Orientation.Row2.Y * value);

    /// <summary>HandleGroundEffect00411630: predicted position, acceleration, downward damping and terrain alignment.</summary>
    public void HandleGroundEffect()
    {
        FVector pos = _main.Pos + (_main.Velocity * 20.0f) * 0.5f;
        float water = Context.Map.WaterLevel;
        float ground = Context.Map.Collide(pos);
        float altitude = (water < ground ? water : ground) - pos.Z;
        if (_main.Energy == 0 || !(altitude < 5)) return;
        if (altitude < 0) altitude = 0;
        _main.AddVelocity(ForwardColumn((5.0f - altitude) * 0.0025f));
        if (DoingLoop || DoingBarrelCount != 0) return;
        if (_main.Velocity.Z > 0) _main.Velocity.Z *= 0.9f;
        float yaw = _main.CurrentOrientation.Yaw;
        var heading = new FVector(-(float)X87.Sin(yaw), (float)X87.Cos(yaw), 0);
        FVector normal = ground < water ? Context.Map.Normal(pos) : new FVector(0, 0, -1);
        FVector across = FVector.Cross(heading, normal);
        FVector along = FVector.Cross(across, normal);
        along.Normalise();
        float mix = 1.0f - altitude * 0.2f;
        // Retail applies mix BEFORE 0.02, unlike the unoptimized source expression.
        _main.Pitchvel += ((-along.Z - _main.CurrentOrientation.Pitch) * mix) * 0.02f;
        across.Normalise();
        _main.Rollvel += ((across.Z * 0.5f - _main.CurrentOrientation.Roll) * mix) * 0.02f;
    }

    /// <summary>GetFriction00411aa0: less than one unit, one-to-three, then the high-altitude coefficient.</summary>
    public float GetFriction()
    {
        float water = Context.Map.WaterLevel;
        float ground = Context.Map.Collide(_main.Pos);
        float altitude = (water < ground ? water : ground) - _main.Pos.Z;
        if (altitude < 1.0f) return 0.99f;
        if (altitude < 3.0f) return _main.Velocity.Magnitude() < 1.5f ? 1.0f - altitude * 0.01f : 0.99f;
        return 0.98f;
    }

    /// <summary>HandleSkimming00411500: only water above terrain, below half a unit, with sufficient horizontal speed.</summary>
    public void HandleSkimming()
    {
        float water = Context.Map.WaterLevel;
        if (!(water < Context.Map.Collide(_main.Pos))) return;
        float altitude = water - _main.Pos.Z;
        if (!(altitude < 0.5f)) return;
        FVector velocity = _main.Velocity;
        // 0041156e..004115c1: FSQRT stays in x87 through both products. Preserve its exponent range,
        // including the squared operands, rather than forcing an intermediate float store.
        double speed = X87.Sqrt(X87.Add(X87.Mul(velocity.X, velocity.X), X87.Mul(velocity.Y, velocity.Y)));
        if (!(speed > 0.3f)) return;
        _main.Velocity.Z = (float)X87.Sub(_main.Velocity.Z, X87.Mul(speed, 0.3f));
        _main.Pitchvel = (float)X87.Sub(_main.Pitchvel, X87.Mul(speed, 0.01f));
        _main.Velocity *= 0.8f;
        _main.Damage((0.5f - altitude) * 20.0f, null, false, -1);
        _main.HostileEnvironment();
    }

    // Inline matrix constructor at 00411307..00411415: sin(yaw) and sin(pitch) stay in x87 registers; cos(yaw),
    // roll trig and cos(pitch) are stored. This differs from both shared constructor sequences.
    private FMatrix OrientationAfterBrokenLoop()
    {
        double c = X87.Cos(_main.ShakeR);
        float yaw = (float)X87.Add(X87.Mul(c, _main.YawShake), _main.CurrentOrientation.Yaw);
        float pitch = (float)X87.Add(X87.Mul(c, _main.PitchShake), _main.CurrentOrientation.Pitch);
        float roll = (float)X87.Add(X87.Mul(c, _main.RollShake), _main.CurrentOrientation.Roll);
        float cy = (float)X87.Cos(yaw), cr = (float)X87.Cos(roll), sr = (float)X87.Sin(roll), cp = (float)X87.Cos(pitch);
        double sy = X87.Sin(yaw), sp = X87.Sin(pitch);
        double spsr = X87.Mul(sp, sr);
        float spcr = (float)X87.Mul(sp, cr);
        return new FMatrix(
            new FVector((float)X87.Sub(cr * cy, X87.Mul(spsr, sy)), -(float)X87.Mul(cp, sy),
                (float)X87.Add(X87.Mul(spcr, sy), sr * cy)),
            new FVector((float)X87.Add(X87.Mul(spsr, cy), X87.Mul(cr, sy)), cp * cy,
                (float)X87.Sub(X87.Mul(sr, sy), spcr * cy)),
            new FVector(-(cp * sr), (float)sp, cp * cr));
    }

    public void ResetConfiguration()
    {
        foreach (Weapon old in Weapons) old.Release();
        Weapons.Clear();
        foreach (byte[] name in _main.Configuration!.JetWeapons)
        {
            Weapon? weapon = Weapon.Spawn(Context, Context.Physics.GetWeaponIndex(name), unchecked((int)ThingType.Everything));
            if (weapon is null) continue;
            weapon.Init(new InitEquipment { AttachedTo = _main });
            Weapons.Add(weapon);
        }
        CurrentWeapon = 0;
    }

    public Weapon? GetCurrentWeapon() => CurrentWeapon >= 0 && CurrentWeapon < Weapons.Count ? Weapons[CurrentWeapon] : null;
    public void LoseWeaponCharge() => GetCurrentWeapon()?.LoseCharge();
    public void EnableWeapon(byte[] name)
    {
        foreach (Weapon weapon in Weapons) if (name.AsSpan().SequenceEqual(weapon.Data.Name)) weapon.WeaponActive = true;
    }
    public void DisableWeapon(byte[] name)
    {
        foreach (Weapon weapon in Weapons)
        {
            if (!name.AsSpan().SequenceEqual(weapon.Data.Name)) continue;
            weapon.WeaponActive = false;
            if (ReferenceEquals(GetCurrentWeapon(), weapon)) ChangeWeapon();
        }
    }
    public void ChangeWeapon()
    {
        Weapon? current = GetCurrentWeapon();
        if (current is null) return;
        int oldZoom = current.Data.ZoomMode;
        for (int n = (CurrentWeapon + 1) % Weapons.Count; n != CurrentWeapon; n = (n + 1) % Weapons.Count)
        {
            Weapon weapon = Weapons[n];
            int store = weapon.Data.AmmoStore;
            if (!weapon.WeaponActive || !(_main.StoreHeat[store] || _main.StoreValue[store] >= weapon.Data.Consumption)) continue;
            CurrentWeapon = n;
            _main.SlowMovement = false;
            LoseWeaponCharge();
            if (oldZoom != weapon.Data.ZoomMode) _main.DesiredZoom = 1.0f;
            return;
        }
    }
    public void FireWeapon()
    {
        _main.SlowMovement = false;
        if (GetCurrentWeapon() is { WeaponActive: true } weapon) weapon.Fire();
    }
    public void ChargeWeapon()
    {
        if (GetCurrentWeapon() is not { WeaponActive: true } weapon || !weapon.ReadyToCharge()) return;
        if (!weapon.CanCharge()) { FireWeapon(); return; }
        int store = weapon.Data.AmmoStore;
        if (!(_main.StoreHeat[store] || _main.StoreValue[store] > 0) || weapon.FullyCharged() || _main.StoreOverheat[store]) return;
        _main.SlowMovement = weapon.Data.AllowMovement == 0;
        weapon.Charge();
        if (!_main.StoreHeat[store]) return;
        if (_main.StoreValue[store] < _main.Configuration!.StoreValue[store] && !_main.StoreOverheat[store])
            _main.StoreValue[store] += weapon.Data.Consumption;
        else { _main.StoreOverheat[store] = true; _main.WeaponOverheated(); FireWeapon(); }
    }
    public bool WeaponFired(Weapon candidate)
    {
        foreach (Weapon weapon in Weapons)
        {
            if (!ReferenceEquals(candidate, weapon)) continue;
            int store = weapon.Data.AmmoStore;
            if (_main.StoreHeat[store])
            {
                if (weapon.ChargeLevel > 0) return true;
                if (_main.StoreValue[store] < _main.Configuration!.StoreValue[store] && !_main.StoreOverheat[store])
                {
                    _main.StoreValue[store] += weapon.Data.Consumption - BattleEngine.WeaponCoolRate;
                    return true;
                }
                _main.StoreOverheat[store] = true;
                _main.WeaponOverheated();
            }
            else if (_main.StoreValue[store] > 0)
            {
                _main.StoreValue[store] -= weapon.Data.Consumption;
                if (_main.StoreValue[store] < 0) _main.StoreValue[store] = 0;
                return true;
            }
            else if (_main.AmmoDepletedTime < Context.Events.Time - 8.0f) _main.AmmoDepletedTime = Context.Events.Time;
        }
        return false;
    }
}
