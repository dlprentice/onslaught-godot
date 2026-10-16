// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/Equipment.h and src/Equipment.cpp (CInitEquipment; CEquipment::Init 0044a830,
// GetPrimaryPosition 0044a850, GetPrimaryOrientation 0044a930), src/weapon.h and src/weapon.cpp (CWeapon: the
// constructor 00505e00, Fire 00506010, CanLock 005061f0, the lock getters 00506350..00506800, Charge 005068f0,
// HandleEvent 00506930, SpawnRound 005069f0, CWeaponModeData::GetLaunchSequence 005078b0, Unknown509140 00509140,
// CalculateDesiredPitch 005094b0, GetMinRange 005096a0, GetMaxRange 005099a0, GetActualMaxRange 00509c80,
// GetWeaponMode 00509e40, GetCurrentMode 00509e90, ReadyToFire 00509f70, ReadyToCharge 0050a080, Unknown50a0b0
// 0050a0b0, Unknown50a0d0 0050a0d0, GetPredictedTargetPos 0050a0e0, IsFiring 0050a290; CRoundData's ballistic test
// 0040d0f0), src/WorldPhysicsManager.cpp (UPhysicsManager::SpawnWeapon 0050f6d0) and src/maths.h (FVector::Azimuth,
// Elevation, AngleDifference). Not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CInitEquipment: the unit, the emitter index (-1: none) and the emitter type.</summary>
public sealed class InitEquipment
{
    public Unit? AttachedTo;
    public int Number = -1;
    public int Mount;
}

/// <summary>CEquipment: something a unit carries, sitting on one of its mesh's emitters.</summary>
public abstract class Equipment : Monitor
{
    /// <summary>mAttachedTo (+0x08).</summary>
    public Unit? AttachedTo;

    /// <summary>mNumber (+0x0c): the emitter index, -1 for none.</summary>
    public int Number;

    /// <summary>mMount (+0x10): the emitter type.</summary>
    public int Mount;

    /// <summary>CEquipment::Init: copies the three fields.</summary>
    public virtual void Init(InitEquipment init)
    {
        AttachedTo = init.AttachedTo;
        Number = init.Number;
        Mount = init.Mount;
    }

    /// <summary>GetPrimaryPosition: its emitter's position, or the unit's own when it has none or it sits at the origin.</summary>
    public FVector GetPrimaryPosition()
    {
        Unit unit = AttachedTo!;
        if (Number != -1)
        {
            unit.GetEmitter(Mount, Number, out FVector pos, out _);
            if (pos.Equals(new FVector(0, 0, 0)))
            {
                return unit.Pos;
            }

            return pos;
        }

        return unit.Pos;
    }

    /// <summary>GetPrimaryOrientation: its emitter's orientation, or the unit's own.</summary>
    public FMatrix GetPrimaryOrientation()
    {
        Unit unit = AttachedTo!;
        if (Number != -1)
        {
            unit.GetEmitter(Mount, Number, out FVector pos, out FMatrix ori);
            if (!pos.Equals(new FVector(0, 0, 0)))
            {
                return ori;
            }
        }

        return unit.Orientation;
    }
}

/// <summary>Weapon queries' shared maths (maths.h's FVector angles and AngleDifference).</summary>
public static class WeaponMaths
{
    /// <summary>PI as maths.h defines it (3.14159265358979f).</summary>
    public const float Pi = 3.14159265358979f;

    /// <summary>PI_M2: PI * 2.0f.</summary>
    public const float PiM2 = Pi * 2.0f;

    /// <summary>
    /// FVector::Azimuth: -atan2f(X, Y). Modelled with X87.Atan2 rounded to a float; retail's CRT atan2 sequence (fpatan)
    /// is not checked instruction by instruction for these call sites, so each use is counted.
    /// </summary>
    public static float Azimuth(ThingContext context, FVector v)
    {
        context.Skip("FVector::Azimuth (atan2f) not checked against retail's x87 sequence");
        return -(float)X87.Atan2(v.X, v.Y);
    }

    /// <summary>
    /// FVector::Elevation: asinf(Z / Magnitude()) when the magnitude is positive, else 0. Modelled with a double asin
    /// rounded to a float; the CRT asin sequence (fpatan and fsqrt) is not checked, so each use is counted.
    /// </summary>
    public static float Elevation(ThingContext context, FVector v)
    {
        context.Skip("FVector::Elevation (asinf) not checked against retail's x87 sequence");
        float m = v.Magnitude();
        if (m > 0.0f)
        {
            return (float)Math.Asin(v.Z / m);
        }

        return 0.0f;
    }

    /// <summary>AngleDifference: a - b, with b taken round by 2PI when the two sit either side of +-PI/2.</summary>
    public static float AngleDifference(float a, float b)
    {
        if (a < -Pi / 2 && b > Pi / 2)
        {
            b -= PiM2;
        }
        else if (a > Pi / 2 && b < -Pi / 2)
        {
            b += PiM2;
        }

        return a - b;
    }

    /// <summary>CRoundData::Unknown40d0f0 / IsBallistic: gravity, not a beam, not a torpedo.</summary>
    public static bool IsBallistic(RoundData round) => round.Gravity * 0.025f != 0.0f && round.Beam == 0 && round.Torpedo == 0;
}

/// <summary>
/// CWeapon: a weapon a unit carries. Its mode is the charge level's weapon mode; it fires bursts through event
/// WEAPON_BURST_EVENT. The queries a unit's AI asks (ranges, aim, fire tests) are ported where retail's arithmetic is
/// float-exact; the ballistic branches, line tests and fear-grid tests throw until their x87 sequences and subsystems
/// are ported, and a shot that would spawn a round throws (rounds are not ported, and a round's Init draws).
/// </summary>
public sealed class Weapon(ThingContext context, WeaponData data, int type) : Equipment
{
    /// <summary>WEAPON_BURST_EVENT: a burst's next round.</summary>
    public const int BurstEvent = 5001;

    public ThingContext Context { get; } = context;

    /// <summary>mData (+0xa4).</summary>
    public WeaponData Data { get; } = data;

    /// <summary>mTargetTypes (+0xa8): the thing types it can hit (the unit weapon reference's second field).</summary>
    public uint TargetTypes { get; } = unchecked((uint)type);

    /// <summary>mTarget (+0x2c).</summary>
    public readonly ActiveReader<Thing> Target = new();

    /// <summary>mOrientation (+0x30): identity at first (MakeRotation(0, 0, 0)).</summary>
    public FMatrix AimOrientation = FMatrix.Identity;

    /// <summary>mCharge (+0x60).</summary>
    public float ChargeValue;

    /// <summary>mNextFireTime (+0x64): -200 at first.</summary>
    public float NextFireTime = -200.0f;

    /// <summary>mUnknown68 (+0x68): the charge level fired.</summary>
    public int ChargeLevel;

    /// <summary>mBurstCount (+0x6c).</summary>
    public int BurstCount;

    /// <summary>mUnknown70 (+0x70): the launch ordinal, -1 at first.</summary>
    public int LaunchOrdinal = -1;

    /// <summary>mUnknown74 (+0x74): the launch-angle ordinal, -1 at first.</summary>
    public int AngleOrdinal = -1;

    public int Unknown78;
    public int Unknown7C;

    /// <summary>mUnknown80 (+0x80): an aim request is set.</summary>
    public int AimRequested;

    /// <summary>mUnknown84 (+0x84): the aim request's position, (-1, -1, -1) at first.</summary>
    public FVector AimPosition = new(-1.0f, -1.0f, -1.0f);

    /// <summary>mUnknown94 (+0x94): its gun's part chain turns a turret.</summary>
    public int Unknown94;

    /// <summary>mUnknown98 (+0x98): its gun's part chain turns a barrel.</summary>
    public int Unknown98;

    /// <summary>mActive (+0x9c).</summary>
    public bool WeaponActive = true;

    /// <summary>mMode (+0xa0).</summary>
    public WeaponModeData? Mode;

    /// <summary>mUnknownAC (+0xac): its emitter's type.</summary>
    public int UnknownAC;

    /// <summary>The constructor's last step: the mode of charge level 0 (mUnknown68 is 0).</summary>
    public void InitialiseMode() => Mode = GetWeaponMode(Data.ChargeModes[ChargeLevel]);

    /// <summary>UPhysicsManager::SpawnWeapon: a weapon of the definition at that position, or null.</summary>
    public static Weapon? Spawn(ThingContext context, int index, int type)
    {
        context.Skip("UPhysicsManager::SpawnWeapon's thing-heap free-space test (10 KB)");
        WeaponData? data = context.Physics.GetWeaponData(index);
        if (data is null)
        {
            return null;
        }

        var weapon = new Weapon(context, data, type);
        weapon.InitialiseMode();
        return weapon;
    }

    /// <summary>GetWeaponMode: the registry's weapon mode at that position, or null.</summary>
    public WeaponModeData? GetWeaponMode(int index) =>
        index >= 0 && index < Context.Physics.WeaponModes.Count ? Context.Physics.WeaponModes[index] : null;

    /// <summary>(SINT)mCharge / 100: the float's conversion rounds to nearest under /QIfist.</summary>
    private int ChargeQuotient() => X87.Fist(ChargeValue) / 100;

    /// <summary>GetCurrentMode: the mode of the highest charge level reached that has one.</summary>
    public WeaponModeData? GetCurrentMode()
    {
        int level = ChargeQuotient();
        WeaponModeData? mode = GetWeaponMode(Data.ChargeModes[level]);
        while (mode is null)
        {
            level--;
            if (level < 0)
            {
                return null;
            }

            mode = GetWeaponMode(Data.ChargeModes[level]);
        }

        return mode;
    }

    public bool CanCharge()
    {
        for (int i = 1; i < 5; i++)
        {
            if (Data.ChargeModes[i] != -1)
            {
                return true;
            }
        }

        return false;
    }

    public int GetMaxCharge()
    {
        int max = 0;
        for (int i = 0; i < 5; i++)
        {
            if (Data.ChargeModes[i] != -1)
            {
                max = i * 100;
            }
        }

        return max;
    }

    public bool FullyCharged() => !CanCharge() || ChargeValue >= GetMaxCharge();

    /// <summary>Charge: the charge rate is added while under 400 (when the weapon can charge).</summary>
    public void Charge()
    {
        if (CanCharge() && ChargeValue < 400.0f)
        {
            ChargeValue += Data.ChargeRate;
        }
    }

    public void LoseCharge() => ChargeValue = 0;

    /// <summary>ReadyToFire: past the next fire time when it has a mode, else whether a current mode exists.</summary>
    public bool ReadyToFire()
    {
        if (Mode is not null)
        {
            return Context.Events.Time > NextFireTime;
        }

        return GetCurrentMode() is not null;
    }

    /// <summary>ReadyToCharge.</summary>
    public bool ReadyToCharge() => !(Mode is not null && Context.Events.Time <= NextFireTime);

    /// <summary>IsFiring: in the middle of a burst.</summary>
    public bool IsFiring() => Mode is not null && BurstCount != 0 && BurstCount < Mode.BurstSize;

    public bool ValidRound() => Mode?.Round is not null;

    /// <summary>GetLaunchVelocity (weapon.h): a beam's 1000, a round's speed, 0 without a round.</summary>
    public float GetLaunchVelocity() => ValidRound() ? (Mode!.Round!.Beam != 0 ? 1000.0f : Mode.Round.Speed) : 0.0f;

    public bool CanPredict() => Mode is not null && Mode.Predictive != 0;

    public bool IsSmart() => Data.Smart != 0;

    public bool AdjustAim() => Data.AdjustAim != 0;

    public float GetMinTargetHeight() => Mode!.MinTargetHeight;

    public float GetMaxTargetHeight() => Mode!.MaxTargetHeight;

    /// <summary>Unknown50a0b0: active, and the target's type is one it can hit.</summary>
    public bool CanHitThing(Thing target) => WeaponActive && (TargetTypes & target.ThingTypeBits) != 0;

    /// <summary>Unknown50a0d0: the type is one it can hit.</summary>
    public bool CanHitType(uint thingType) => (TargetTypes & thingType) != 0;

    public int GetMaxLocks() => GetCurrentMode()?.MaxLocks ?? 0;

    public float GetLockTime() => GetCurrentMode()?.LockTime ?? 0.0f;

    public int GetLockMode() => GetCurrentMode()?.LockMode ?? 0;

    public float GetMaxDeflection() => GetCurrentMode()?.LockDeflection ?? 0.0f;

    public float GetLockRange() => GetCurrentMode()?.LockRange ?? 0.0f;

    public float GetLockRadius() => GetCurrentMode()?.LockRadius ?? 0.0f;

    /// <summary>CanLock: an active, lockable unit without a nexus or weak point, of a type the current mode locks on.</summary>
    public bool CanLock(Unit target)
    {
        if (!target.Active)
        {
            return false;
        }

        if ((target.UnitData is null || target.UnitData.Tentacle == 0) && target.Unknown228Nexus)
        {
            return false;
        }

        if (target.Unknown22CWeakPoint)
        {
            return false;
        }

        if (target.UnitData is not null && target.UnitData.CanBeLocked == 0)
        {
            return false;
        }

        return (unchecked((uint)GetCurrentMode()!.LockUnit) & target.ThingTypeBits) != 0;
    }

    /// <summary>
    /// Fire: the charge level's mode (walking down to one that exists), the fire-time and burst bookkeeping, the round,
    /// and the burst's next event.
    /// </summary>
    public bool Fire()
    {
        ChargeLevel = ChargeQuotient();
        ChargeValue = 0;
        Mode = GetWeaponMode(Data.ChargeModes[ChargeLevel]);
        while (Mode is null)
        {
            ChargeLevel--;
            if (ChargeLevel < 0)
            {
                return false;
            }

            Mode = GetWeaponMode(Data.ChargeModes[ChargeLevel]);
        }

        if (!ReadyToFire())
        {
            return false;
        }

        if (Mode?.Round is null)
        {
            return false;
        }

        NextFireTime = Context.Events.Time + Mode.ReloadTime;
        BurstCount = 0;
        if (AttachedTo!.IsA(ThingType.BattleEngine))
        {
            Context.Skip("CBattleEngine::ResetTarget (from CWeapon::Fire)");
        }

        if (SpawnRound() && Mode.LaunchSoundName is not null && Mode.SoundPerBurst != 0)
        {
            Context.Skip("SOUND.PlayEffect (CWeapon::Fire's burst sound)");
        }

        if (Mode.BurstSize > 0)
        {
            BurstCount = 1;
            Context.Events.AddEvent(BurstEvent, this, Context.Events.Time + Mode.BurstDelay);
        }

        return true;
    }

    /// <summary>HandleEvent: WEAPON_BURST_EVENT fires the burst's next round while the unit lives.</summary>
    public override void HandleEvent(Event e)
    {
        if (e.EventNum == BurstEvent && !AttachedTo!.IsDying)
        {
            WeaponModeData? mode = GetWeaponMode(Data.ChargeModes[ChargeLevel]);
            if (mode is not null && BurstCount < mode.BurstSize)
            {
                SpawnRound();
                BurstCount++;
                Context.Events.AddEvent(BurstEvent, this, Context.Events.Time + mode.BurstDelay);
            }
        }
    }

    /// <summary>
    /// SpawnRound: a battle engine's firing check, the launch sound, then a round per volley shot. Each shot first asks
    /// UPhysicsManager::SpawnRound for a round, which retail makes whenever the thing heap has 200 KB free. Rounds are
    /// not ported, and the shot's later draws (the inaccuracy errors, the flak roll, a clip's shell) and the round's own
    /// Init (CActor's draw) depend on it, so a shot throws here rather than drop or invent draws.
    /// </summary>
    public bool SpawnRound()
    {
        if (AttachedTo is BattleEngine be && !be.WeaponFired(this))
        {
            return false;
        }

        WeaponModeData? mode = Mode;
        if (mode is not null)
        {
            if (mode.LaunchSoundName is not null && mode.SoundPerBurst == 0)
            {
                Context.Skip("SOUND.PlayEffect (CWeapon::SpawnRound's per-shot sound)");
            }

            if (mode.VolleySize > 0 && mode.Round is not null)
            {
                throw new NotSupportedException(
                    "UPhysicsManager::SpawnRound: rounds are not ported (a shot draws for its inaccuracy, flak and shell, and the round's Init draws)");
            }
        }

        return false;
    }

    /// <summary>
    /// Unknown509140: points the weapon at a target and a place. A mode that adjusts its aim at a ballistic round
    /// searches pitches for the nearest arc (its x87 sequence is not transcribed: it throws); a straight round points
    /// along the line; otherwise the emitter's orientation. Then the aim request is published.
    /// </summary>
    public void Aim(Thing? target, FVector pos)
    {
        FVector d = pos - GetPrimaryPosition();
        if (Data.AdjustAim != 0)
        {
            if (WeaponMaths.IsBallistic(Mode!.Round!))
            {
                throw new NotSupportedException("CWeapon::Unknown509140's ballistic pitch search: x87 sequence not transcribed");
            }

            AimOrientation = FMatrix.FromYawPitchRoll(WeaponMaths.Azimuth(Context, d), WeaponMaths.Elevation(Context, d), 0);
            Context.Skip("CWeapon::Unknown509140's FMatrix(yaw, pitch, 0) copy not checked against retail's register use");
        }
        else
        {
            AimOrientation = GetPrimaryOrientation();
        }

        AimRequested = 1;
        AimPosition = pos;
        Target.SetReader(target);
    }

    /// <summary>CalculateDesiredPitch: a straight round's elevation to the target (the ballistic search throws).</summary>
    public float CalculateDesiredPitch(FVector target)
    {
        Unit unit = AttachedTo!;
        FVector origin = unit.Pos;
        if (Unknown98 != 0)
        {
            throw new NotSupportedException("CUnit::Unknown113 (the barrel's origin, from CalculateDesiredPitch) is not ported");
        }

        FVector delta = target - origin;
        if (WeaponMaths.IsBallistic(Mode!.Round!))
        {
            throw new NotSupportedException("CWeapon::CalculateDesiredPitch's ballistic search: x87 sequence not transcribed");
        }

        return WeaponMaths.Elevation(Context, delta);
    }

    /// <summary>GetMinRange: the mode's minimum range for a straight round (a ballistic round's throws), 0 without one.</summary>
    public float GetMinRange(FVector target)
    {
        if (Mode?.Round is not null)
        {
            if (WeaponMaths.IsBallistic(Mode.Round))
            {
                throw new NotSupportedException("CWeapon::GetMinRange's ballistic ranges: x87 sequence not transcribed");
            }

            return Mode.MinRange;
        }

        return 0;
    }

    /// <summary>GetMaxRange: the mode's maximum range for a straight round (a ballistic round's throws), 0 without one.</summary>
    public float GetMaxRange(FVector target)
    {
        if (Mode?.Round is not null)
        {
            if (WeaponMaths.IsBallistic(Mode.Round))
            {
                throw new NotSupportedException("CWeapon::GetMaxRange's ballistic ranges: x87 sequence not transcribed");
            }

            return Mode.MaxRange;
        }

        return 0;
    }

    /// <summary>
    /// GetActualMaxRange: a ballistic round's maximum range; otherwise the current mode's (or the first charge level's
    /// mode's): a beam's maximum range, a seeking turner's lock range, else its speed times its life span.
    /// </summary>
    public float GetActualMaxRange(FVector target)
    {
        if (Mode?.Round is { } round && round.Gravity * 0.025f != 0.0f && round.Beam == 0 && round.Torpedo == 0)
        {
            return GetMaxRange(target);
        }

        WeaponModeData? mode = GetCurrentMode();
        WeaponModeData? fallback = null;
        if (mode is null)
        {
            for (int i = 0; i < 5; i++)
            {
                fallback = GetWeaponMode(Data.ChargeModes[i]);
                if (fallback is not null)
                {
                    break;
                }
            }
        }

        WeaponModeData? use = fallback ?? mode;
        if (use?.Round is { } r)
        {
            if (r.Beam != 0)
            {
                return use.MaxRange;
            }

            if (r.TurnRate > 0.0f && r.Seek != 0)
            {
                return use.LockRange;
            }

            return r.Speed * r.LifeSpan;
        }

        return 0.0f;
    }

    /// <summary>
    /// GetPredictedTargetPos: for a predictive mode, the target's position led by its velocity over the round's flight
    /// time (in ticks); otherwise its targetable position.
    /// </summary>
    public FVector GetPredictedTargetPos(Unit target)
    {
        if (Mode is not null && Mode.Predictive != 0)
        {
            FVector pos = GetPrimaryPosition();
            FVector tpos = target.GetTargetablePos();
            float speed;
            if (Mode.Round is not null)
            {
                speed = Mode.Round.Beam != 0 ? 1000.0f : Mode.Round.Speed;
            }
            else
            {
                speed = 0;
            }

            FVector vel = GetPrimaryOrientation().Transform(new FVector(0, speed, 0));
            FVector tvel = target.Velocity;
            float t = (pos - tpos).Magnitude() / vel.Magnitude();
            t *= 20.0f;
            return (tvel * t) + tpos;
        }

        return target.GetTargetablePos();
    }

    /// <summary>
    /// Unknown5088b0 (CUnit::Unknown4fb5a0's test): below the sea, clear of the fear grid, then a ballistic range band,
    /// a torpedo's water, or the deflection band, and a clear line. Only the parts before a fear-grid test, a ballistic
    /// branch or a line test run here; those throw until ported.
    /// </summary>
    public bool CanReach(Unit target)
    {
        if (GetPrimaryPosition().Z > Context.WaterLevel)
        {
            return false;
        }

        if (Mode?.Round is { GridOfFear: not 0 })
        {
            throw new NotSupportedException("CWeapon::Unknown5088b0's fear-grid test: the fear grids are not reachable from the weapon yet");
        }

        throw new NotSupportedException(
            "CWeapon::Unknown5088b0 past its first tests (the pitch band uses Elevation, the line test WORLD.FindFirstThingToHitLine)");
    }

    /// <summary>
    /// Unknown507ab0 (CUnit::CanFireAt's test): below the sea, clear of the fear grid, within the yaw tolerance, then the
    /// range or deflection tests and a clear line. As <see cref="CanReach"/>, only the opening tests run here.
    /// </summary>
    public bool CanFireAt(Unit target, bool a)
    {
        if (GetPrimaryPosition().Z > Context.WaterLevel)
        {
            return false;
        }

        if (Mode is not null && Mode.Round is { GridOfFear: not 0 })
        {
            throw new NotSupportedException("CWeapon::Unknown507ab0's fear-grid test: the fear grids are not reachable from the weapon yet");
        }

        throw new NotSupportedException(
            "CWeapon::Unknown507ab0 past its first tests (yaw tolerance via Azimuth, ballistic ranges, WORLD.FindFirstThingToHitLine)");
    }

    /// <summary>CWeaponModeData::GetLaunchSequence: the sequence's value at that position, 0 past its end.</summary>
    public static int GetLaunchSequence(WeaponModeData mode, int index) =>
        index >= 0 && index < mode.LaunchSequence.Count ? mode.LaunchSequence[index] : 0;

    public override string ToString() => $"weapon '{Data.NameText}' mode '{Mode?.NameText}'";
}
