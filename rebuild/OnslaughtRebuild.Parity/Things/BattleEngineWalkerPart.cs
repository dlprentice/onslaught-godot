// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/BattleEngineWalkerPart.cpp and its header (CBattleEngineWalkerPart: UpdateWalkCycle
// 00412ad0, the constructor 00412bc0, Move 00413760, GoingIntoWater 00413a70, ShouldSlide, Slide 00413b90,
// GetCurrentWeapon 00414030, ResetConfiguration 004146b0). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CBattleEngineWalkerPart: the battle engine on its legs. Each move it recharges the energy while on the ground (half
/// as fast while the shields are not recharging), stops at the water's edge, slides down steep ground, and applies the
/// walking friction and speed limit.
/// </summary>
public sealed class BattleEngineWalkerPart
{
    private const float Pi = 3.14159265358979f;
    private const float PiM2 = 6.28318530717958f;

    /// <summary>mDashFriction: the dash turns left when friction comes back (a console variable, 5 by default).</summary>
    public static int DashFriction = 5;

    private readonly BattleEngine _main;

    public BattleEngineWalkerPart(BattleEngine mainPart)
    {
        _main = mainPart;
        WalkCycle = -Pi;
        OldWalkCycle = -Pi;
        CurrentWeapon = 0;
        PrimaryWeapon = null;
        AugWeapon = null;
        ResetConfiguration();
        ShieldsRecharging = true;
        LastStartHardRightTime = -10.0f;
        LastStartHardLeftTime = -10.0f;
        LastStartHardForwardTime = -10.0f;
        LastStartHardBackwardTime = -10.0f;
        DoingDashCount = 0;
    }

    public float WalkCycle;
    public float OldWalkCycle;
    public int CurrentWeapon;
    public Weapon? PrimaryWeapon;
    public Weapon? AugWeapon;
    public List<Weapon> Weapons { get; } = [];
    public bool ShieldsRecharging;
    public float LastMoveXVal;
    public float LastMoveYVal;
    public float LastStartHardRightTime;
    public float LastStartHardLeftTime;
    public float LastStartHardForwardTime;
    public float LastStartHardBackwardTime;
    public int DoingDashCount;

    public int GetDashCount() => DoingDashCount;

    private ThingContext Context => _main.Context;

    private Weapon? SpawnWeapon(byte[]? name)
    {
        if (name is null)
        {
            return null;
        }

        Weapon? weapon = Weapon.Spawn(Context, Context.Physics.GetWeaponIndex(name), unchecked((int)ThingType.Everything));
        weapon?.Init(new InitEquipment { AttachedTo = _main });
        return weapon;
    }

    /// <summary>ResetConfiguration: the configuration's walker weapons, its primary and its aug weapon, the first current.</summary>
    public void ResetConfiguration()
    {
        Weapons.Clear();
        PrimaryWeapon = null;
        AugWeapon = null;
        foreach (byte[] name in _main.Configuration!.WalkerWeapons)
        {
            if (SpawnWeapon(name) is { } weapon)
            {
                Weapons.Add(weapon);
            }
        }

        PrimaryWeapon = SpawnWeapon(_main.Configuration.PrimaryWeapon);
        AugWeapon = SpawnWeapon(_main.Configuration.AugWeapon);
        CurrentWeapon = 0;
    }

    /// <summary>GetCurrentWeapon: the primary (or the aug weapon while the aug is active) at 0, then the list in order.</summary>
    public Weapon? GetCurrentWeapon()
    {
        if (CurrentWeapon == 0)
        {
            if (_main.AugActive && AugWeapon is not null)
            {
                return AugWeapon;
            }

            if (PrimaryWeapon is not null)
            {
                return PrimaryWeapon;
            }
        }

        int n = PrimaryWeapon is not null ? 1 : 0;
        foreach (Weapon weapon in Weapons)
        {
            if (n == CurrentWeapon)
            {
                return weapon;
            }

            n++;
        }

        CurrentWeapon = 0;
        return PrimaryWeapon ?? (Weapons.Count > 0 ? Weapons[0] : null);
    }

    /// <summary>UpdateWalkCycle: the cycle advances by the velocity along the engine's larger axis, wrapped to +-PI.</summary>
    public void UpdateWalkCycle()
    {
        OldWalkCycle = WalkCycle;
        FMatrix ori = _main.Orientation.Transpose();
        FVector rel = ori.Transform(_main.Velocity);
        if (MathF.Abs(rel.X) > MathF.Abs(rel.Y))
        {
            WalkCycle += rel.X * 2.5f;
        }
        else
        {
            WalkCycle += rel.Y * 3.0f;
        }

        if (WalkCycle > Pi)
        {
            WalkCycle -= PiM2;
        }

        if (WalkCycle < -Pi)
        {
            WalkCycle += PiM2;
        }
    }

    /// <summary>Move (00413760).</summary>
    public void Move()
    {
        _main.EngineState = BattleEngine.EnginesOff;
        if (GetCurrentWeapon() is null)
        {
            CurrentWeapon = 0;
        }

        if (_main.PlayerReader is not null)
        {
            Context.Skip("CPlayer::IncStat(PS_TIMEASWALKER)");
        }

        foreach (Weapon weapon in Weapons)
        {
            Context.Skip("CWeapon::MoveEmitter (the walker's weapons)");
        }

        if (Context.Events.Time - _main.LastTimeOnGround < 0.3f && !_main.InfinateEnergy && !_main.Cloaked)
        {
            float recharge = _main.Configuration!.GroundEnergyIncrease;
            if (!ShieldsRecharging)
            {
                recharge /= 2;
            }

            _main.Energy += recharge;
            if (_main.Energy > _main.Configuration.Energy)
            {
                _main.Energy = _main.Configuration.Energy;
            }
        }

        ShieldsRecharging = true;
        _main.Shields = _main.Energy;
        if (GoingIntoWater())
        {
            _main.Velocity = new FVector(0, 0, _main.Velocity.Z);
        }
        else if (ShouldSlide())
        {
            _main.Flags |= ThingFlags.Slide;
            Slide();
        }
        else
        {
            _main.Flags &= ~ThingFlags.Slide;
        }

        if (!GoingIntoWater() && (_main.IsOnGround() || DoingDashCount != 0))
        {
            float friction = _main.Configuration!.WalkFriction;
            _main.Velocity = new FVector(_main.Velocity.X * friction, _main.Velocity.Y * friction, _main.Velocity.Z * friction);
            var temp = new FVector(_main.Velocity.X, _main.Velocity.Y, 0);
            float m = (float)X87.Sqrt((temp.X * temp.X) + (temp.Y * temp.Y) + (temp.Z * temp.Z));
            float mv = _main.Configuration.MaxWalkVelocity;
            if (m > mv && DoingDashCount < DashFriction)
            {
                float k = mv / m;
                _main.Velocity = new FVector(temp.X * k, temp.Y * k, _main.Velocity.Z);
            }
        }

        if (DoingDashCount > 0)
        {
            DoingDashCount--;
        }

        if (_main.IsWalking())
        {
            UpdateWalkCycle();
        }
    }

    /// <summary>GoingIntoWater: on the ground (not an object), the step would take it down to the water's edge.</summary>
    public bool GoingIntoWater()
    {
        if (_main.IsOnGround() && !_main.IsOnObject())
        {
            float waterLevel = Context.Map.WaterLevel;
            FVector next = _main.Pos + _main.Velocity;
            if (Context.Map.Collide(_main.Pos) - waterLevel > 0.3f)
            {
                if (Context.Map.Collide(next) > Context.Map.Collide(_main.Pos))
                {
                    return true;
                }
            }
            else if (Context.Map.Collide(next) - waterLevel > 0.3f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ShouldSlide: on the ground (or sliding) and not on an object, where the ground ahead is steeper than 40 degrees.</summary>
    public bool ShouldSlide()
    {
        if ((_main.IsOnGround() || (_main.Flags & ThingFlags.Slide) != 0) && !_main.IsOnObject())
        {
            Context.Skip("CBattleEngineWalkerPart::ShouldSlide: the slope's order is unchecked");
            FVector n = Context.Map.Normal(_main.Pos + _main.Velocity);
            float m = Collision.Geometry.MagnitudeOutOfLine(n);
            float elevation = m > 0.0f ? (float)X87.Asin(n.Z / m) : 0.0f;
            float angle = (Pi / 2) + elevation;
            if (angle > 40.0f * Pi / 180)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Slide: the velocity loses its component into the slope's horizontal normal, up to six times.</summary>
    public void Slide()
    {
        Context.Skip("CBattleEngineWalkerPart::Slide (its sum orders are unchecked)");
        for (int count = 0; count < 6; count++)
        {
            FVector vel = _main.Velocity;
            FVector n = Context.Map.Normal(_main.Pos + vel);
            n.Z = 0.0f;
            n.Normalise();
            float dot = (vel.X * n.X) + (vel.Y * n.Y) + (vel.Z * n.Z);
            if (dot > -0.00001f)
            {
                return;
            }

            _main.Velocity = new FVector(vel.X - (n.X * dot), vel.Y - (n.Y * dot), vel.Z - (n.Z * dot));
        }
    }
}
