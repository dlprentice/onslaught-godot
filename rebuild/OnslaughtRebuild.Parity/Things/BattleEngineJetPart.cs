// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngineJetPart.cpp, CBattleEngineJetPart controls (00410310..004109d0),
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

    public void ResetConfiguration()
    {
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
