// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/BattleEngineWalkerPart.cpp and its header (CBattleEngineWalkerPart: UpdateWalkCycle
// 00412ad0, the constructor 00412bc0, Move 00413760, GoingIntoWater 00413a70, ShouldSlide, Slide 00413b90,
// GetCurrentWeapon 00414030, ResetConfiguration 004146b0). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Sound;

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

    /// <summary>The dash's console variables (g_dash_*): start and end thresholds, time, length and velocity.</summary>
    public static float DashTime = 0.2f;
    public static float DashStart = 0.9f;
    public static float DashEnd = 0.8f;
    public static int DashLength = 15;
    public static float DashVelocity = 25.0f;

    /// <summary>GetCurrentAccleration: the configuration's ground velocity / 50 (a multiply by 0.02f, 00412d94).</summary>
    private float CurrentAcceleration() => _main.Configuration!.GroundVelocity * 0.02f;

    /// <summary>A dash: start its owned strafe effect, lose weapon charge and reset zoom; the caller then boosts.</summary>
    private void StartDash()
    {
        // Pristine PC 74154bfa, Forward 00412e5a..00412ed3 and the other three directional arms. Playing the
        // effect consumes the shared CRT stream before charge loss. All four calls use the main BE owner,
        // volume1 and FollowAndDie (2), with once/fade/repeat/ignore=false, pitch1 and Game sound kind.
        if (Context.SoundEffects is { } sounds)
        {
            if (sounds.EventKernelConfigured)
                sounds.PlayEffect(sounds.Find("BE Strafe L/R"), _main, Context.Crt,
                    new SoundPlayback(Volume: 1, Tracking: SoundTracking.FollowAndDie));
            else sounds.PlayEffect(sounds.Find("BE Strafe L/R"), Context.Crt);
        }
        else
        {
            Context.Skip("CBattleEngineWalkerPart's dash sound (no sound-effects owner)");
        }

        LoseWeaponCharge();
        _main.ZoomOutTime = 0.0f;
    }

    private static bool SameName(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);

    /// <summary>EnableWeapon (00414970): the walker's weapons and the primary of that name (strcmp) become active.</summary>
    public void EnableWeapon(byte[] name)
    {
        foreach (Weapon weapon in Weapons)
        {
            if (SameName(name, weapon.Data.Name))
            {
                weapon.WeaponActive = true;
            }
        }

        if (SameName(name, PrimaryWeapon!.Data.Name))
        {
            PrimaryWeapon.WeaponActive = true;
        }
    }

    /// <summary>DisableWeapon (00414a40): those of that name go inactive; the selection moves off a disabled weapon.</summary>
    public void DisableWeapon(byte[] name)
    {
        foreach (Weapon weapon in Weapons.ToList())
        {
            if (SameName(name, weapon.Data.Name))
            {
                weapon.WeaponActive = false;
                if (ReferenceEquals(GetCurrentWeapon(), weapon))
                {
                    ChangeWeapon();
                }
            }
        }

        if (SameName(name, PrimaryWeapon!.Data.Name))
        {
            PrimaryWeapon.WeaponActive = false;
        }

        if (ReferenceEquals(GetCurrentWeapon(), PrimaryWeapon))
        {
            ChangeWeapon();
        }
    }

    /// <summary>GetWeapon: 0 is the primary (or the aug weapon while the aug is active), then the list.</summary>
    public Weapon? GetWeapon(int number)
    {
        if (number == 0 && PrimaryWeapon is not null)
        {
            return _main.AugActive && AugWeapon is not null ? AugWeapon : PrimaryWeapon;
        }

        int n = PrimaryWeapon is not null ? 1 : 0;
        foreach (Weapon weapon in Weapons)
        {
            if (n == number)
            {
                return weapon;
            }

            n++;
        }

        return null;
    }

    public int CountWeapons() => (PrimaryWeapon is not null ? 1 : 0) + Weapons.Count;

    /// <summary>
    /// ChangeWeapon (00413eb0): the next active weapon whose store can pay (a heat store always can), wrapping; on a
    /// change, full speed, the charge lost, and an auto zoom-out when the zoom mode changes.
    /// </summary>
    public void ChangeWeapon()
    {
        int oldZoomMode = GetCurrentWeapon()!.Data.ZoomMode;
        int n = CurrentWeapon + 1;
        int total = CountWeapons();
        bool changed = false;
        while (n != CurrentWeapon)
        {
            if (GetWeapon(n) is { } weapon)
            {
                int store = weapon.Data.AmmoStore;
                if (weapon.WeaponActive && (_main.StoreHeat[store] || _main.StoreValue[store] >= weapon.Data.Consumption))
                {
                    CurrentWeapon = n;
                    changed = true;
                    break;
                }
            }

            n++;
            if (n >= total)
            {
                n = 0;
            }
        }

        if (changed)
        {
            _main.SlowMovement = false;
            LoseWeaponCharge();
            if (oldZoomMode != GetCurrentWeapon()!.Data.ZoomMode)
            {
                _main.DesiredZoom = 1.0f;
            }
        }
    }

    /// <summary>LoseWeaponCharge (00414010).</summary>
    public void LoseWeaponCharge() => GetCurrentWeapon()?.LoseCharge();

    /// <summary>FireWeapon (00413cc0): movement back to full speed, and the current weapon fires if active.</summary>
    public void FireWeapon()
    {
        _main.SlowMovement = false;
        if (GetCurrentWeapon() is { } weapon && weapon.WeaponActive)
        {
            weapon.Fire();
        }
    }

    /// <summary>
    /// ChargeWeapon (00413cf0): an active weapon ready to charge charges while its store has ammunition (or heats) and
    /// is not overheated, slowing the walker unless the weapon allows movement and stopping the shields' recharge; a heat
    /// store fills by the weapon's consumption until it overheats, which fires it. A weapon that cannot charge fires.
    /// </summary>
    public void ChargeWeapon()
    {
        if (GetCurrentWeapon() is not { } weapon || !weapon.WeaponActive || !weapon.ReadyToCharge())
        {
            return;
        }

        if (!weapon.CanCharge())
        {
            FireWeapon();
            return;
        }

        int store = weapon.Data.AmmoStore;
        if ((_main.StoreHeat[store] || _main.StoreValue[store] > 0) && !weapon.FullyCharged() && !_main.StoreOverheat[store])
        {
            _main.SlowMovement = weapon.Data.AllowMovement == 0;
            weapon.Charge();
            ShieldsRecharging = false;
            if (_main.StoreHeat[store])
            {
                if (_main.StoreValue[store] < _main.Configuration!.StoreValue[store] && !_main.StoreOverheat[store])
                {
                    _main.StoreValue[store] += weapon.Data.Consumption;
                }
                else
                {
                    _main.StoreOverheat[store] = true;
                    _main.WeaponOverheated();
                    FireWeapon();
                }
            }
        }
    }

    /// <summary>
    /// WeaponFired (004140d0): the store pays for a shot. A heat store fills by the consumption (a charged shot is free)
    /// until it overheats; an ammunition store empties by it, and an empty one marks ammunition depleted. The walker
    /// list first, then the primary, then the aug weapon (whose shot also spends the aug).
    /// </summary>
    public bool WeaponFired(Weapon inWeapon)
    {
        foreach (Weapon weapon in Weapons)
        {
            if (ReferenceEquals(weapon, inWeapon) && PayForShot(weapon, aug: false))
            {
                return true;
            }
        }

        if (PrimaryWeapon is not null && ReferenceEquals(inWeapon, PrimaryWeapon) && PayForShot(PrimaryWeapon, aug: false))
        {
            return true;
        }

        return AugWeapon is not null && ReferenceEquals(inWeapon, AugWeapon) && PayForShot(AugWeapon, aug: true);
    }

    private bool PayForShot(Weapon weapon, bool aug)
    {
        int store = weapon.Data.AmmoStore;
        if (_main.StoreHeat[store])
        {
            if (weapon.ChargeLevel > 0)
            {
                if (aug)
                {
                    _main.AugValue = 0.0f;
                }

                return true;
            }

            if (_main.StoreValue[store] < _main.Configuration!.StoreValue[store] && !_main.StoreOverheat[store])
            {
                _main.StoreValue[store] += weapon.Data.Consumption;
                ShieldsRecharging = false;
                if (aug)
                {
                    _main.AugValue = 0.0f;
                }

                return true;
            }

            _main.StoreOverheat[store] = true;
            _main.WeaponOverheated();
            return false;
        }

        if (_main.StoreValue[store] > 0)
        {
            _main.StoreValue[store] -= weapon.Data.Consumption;
            if (aug)
            {
                _main.AugValue = 0.0f;
            }

            if (_main.StoreValue[store] < 0)
            {
                _main.StoreValue[store] = 0;
            }

            return true;
        }

        if (_main.AmmoDepletedTime < Context.Events.Time - 8.0f)
        {
            _main.AmmoDepletedTime = Context.Events.Time;
        }

        return false;
    }

    private bool DashWindow(float startTime)
    {
        float now = Context.Events.Time;
        return startTime > now - DashTime && startTime < now - (DashTime * 0.5f);
    }

    /// <summary>Forward (00412d80): -(acceleration * vy) along the heading, when on the ground and not dashing.</summary>
    public void Forward(float vy)
    {
        float val = -(CurrentAcceleration() * vy);
        if (!_main.IsOnGround() || DoingDashCount > 0)
        {
            return;
        }

        if (LastMoveYVal > -DashStart && vy < -DashStart && DoingDashCount == 0)
        {
            LastStartHardForwardTime = Context.Events.Time;
        }

        if (LastMoveYVal > -DashEnd && vy < -DashEnd && DoingDashCount == 0 && DashWindow(LastStartHardBackwardTime))
        {
            StartDash();
            val *= DashVelocity;
            DoingDashCount = DashLength;
        }

        LastMoveYVal = vy;
        AddAlongHeading(val);
    }

    /// <summary>Backward (00412f70): as Forward, the other way.</summary>
    public void Backward(float vy)
    {
        float val = CurrentAcceleration() * vy;
        if (!_main.IsOnGround() || DoingDashCount > 0)
        {
            return;
        }

        if (LastMoveYVal < DashStart && vy > DashStart && DoingDashCount == 0)
        {
            LastStartHardBackwardTime = Context.Events.Time;
        }

        if (LastMoveYVal < DashEnd && vy > DashEnd && DoingDashCount == 0 && DashWindow(LastStartHardForwardTime))
        {
            StartDash();
            val *= DashVelocity;
            DoingDashCount = DashLength;
        }

        LastMoveYVal = vy;
        AddAlongHeading(-val);
    }

    /// <summary>
    /// FMatrix(yaw, 0, 0) * (0, val, 0) as Forward compiles it (the out-of-line constructor 004062d0, then each row's Y
    /// times val, 00412efb..00412f1e), quartered for slow movement (fmul 0.25f), added to the velocity.
    /// </summary>
    private void AddAlongHeading(float val)
    {
        FMatrix ori = FMatrix.MakeRotationOutOfLine(_main.CurrentOrientation.Yaw, 0.0f, 0.0f);
        var move = new FVector(ori.Row0.Y * val, ori.Row1.Y * val, ori.Row2.Y * val);
        if (_main.SlowMovement)
        {
            move = new FVector(move.X * 0.25f, move.Y * 0.25f, move.Z * 0.25f);
        }

        _main.AddVelocity(move);
    }

    /// <summary>StrafeLeft (00413160): -(acceleration * vx) across the heading.</summary>
    public void StrafeLeft(float vx)
    {
        if (!_main.IsOnGround() || DoingDashCount > 0)
        {
            return;
        }

        float val = CurrentAcceleration() * -vx;
        if (LastMoveXVal > -DashStart && vx < -DashStart && DoingDashCount == 0)
        {
            LastStartHardLeftTime = Context.Events.Time;
        }

        if (LastMoveXVal > -DashEnd && vx < -DashEnd && DoingDashCount == 0 && DashWindow(LastStartHardRightTime))
        {
            StartDash();
            val *= DashVelocity;
            _main.Rollvel = 0.08f;
            DoingDashCount = DashLength;
        }

        LastMoveXVal = vx;
        // 004132e4..00413317: negate/store the scalar, call the out-of-line matrix constructor, then multiply
        // only column X. The general Transform's zero products are not executed at this native call site.
        FMatrix ori = FMatrix.MakeRotationOutOfLine(_main.CurrentOrientation.Yaw, 0.0f, 0.0f);
        FVector move = new(ori.Row0.X * -val, ori.Row1.X * -val, ori.Row2.X * -val);
        if (_main.SlowMovement)
        {
            move = new FVector(move.X * 0.25f, move.Y * 0.25f, move.Z * 0.25f);
        }

        _main.AddVelocity(move);
    }

    /// <summary>
    /// StrafeRight (00413360): acceleration * vx across the heading. The source quarters val after computing move;
    /// that unused adjustment emits no instructions, so this direction is not reduced by slow movement in retail.
    /// </summary>
    public void StrafeRight(float vx)
    {
        if (!_main.IsOnGround() || DoingDashCount > 0)
        {
            return;
        }

        float val = CurrentAcceleration() * vx;
        if (LastMoveXVal < DashStart && vx > DashStart && DoingDashCount == 0)
        {
            LastStartHardRightTime = Context.Events.Time;
        }

        if (LastMoveXVal < DashEnd && vx > DashEnd && DoingDashCount == 0 && DashWindow(LastStartHardLeftTime))
        {
            StartDash();
            val *= DashVelocity;
            _main.Rollvel -= 0.08f;
            DoingDashCount = DashLength;
        }

        LastMoveXVal = vx;
        // 004134d7..004135b8: the inlined yaw-only constructor, then column X times val (no zero-term sums).
        FMatrix ori = FMatrix.FromYawPitchRoll(_main.CurrentOrientation.Yaw, 0.0f, 0.0f);
        FVector move = new(ori.Row0.X * val, ori.Row1.X * val, ori.Row2.X * val);
        _main.AddVelocity(move);
    }

    /// <summary>The yaw binding's type: a mouse bound to yaw in either direction ("any") turns 1.7 times as fast.</summary>
    private float LookMultiplier() =>
        Context.Bindings.InputType(1, 0) is Platform.PushType.MouseAxisMinusAny or Platform.PushType.MouseAxisPlusAny ? 1.7f : 1.0f;

    /// <summary>Rotate (00413660): yawvel - zoom modifier * ((mult * vx) * ground turn rate * (1/75)).</summary>
    public void Rotate(float vx)
    {
        float mult = LookMultiplier();
        float yawvel = _main.Yawvel;
        float zm = _main.ZoomModifier(_main.Zoom);
        _main.Yawvel = yawvel - (zm * ((mult * vx) * _main.Configuration!.GroundTurnRate * 0.013333334f));
    }

    /// <summary>Pitch (004136e0): pitchvel - zoom modifier * (mult * vy * (1/117)).</summary>
    public void Pitch(float vy)
    {
        float mult = LookMultiplier();
        float pitchvel = _main.Pitchvel;
        float zm = _main.ZoomModifier(_main.Zoom);
        _main.Pitchvel = pitchvel - (zm * (mult * vy * 0.008547009f));
    }

    /// <summary>
    /// ActivateLandingJets (004135e0): the jets brake the fall: a fortieth of the horizontal speed back, and when falling
    /// faster than 0.01 a thirteenth-and-a-bit of the fall; the thrusters come on.
    /// </summary>
    public void ActivateLandingJets()
    {
        FVector vel = _main.GetVelocity();
        vel.X = -vel.X * 0.025f;
        vel.Y = -vel.Y * 0.025f;
        vel.Z = vel.Z > 0.01f ? -vel.Z * 0.075f : 0;
        _main.AddVelocity(vel);
        _main.ThrustersOn = true;
    }

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
