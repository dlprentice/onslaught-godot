// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Equipment.h and src/Equipment.cpp (CInitEquipment; CEquipment::Init 0044a830,
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
using OnslaughtRebuild.Parity.Particles;
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

    /// <summary>
    /// FVector::Azimuth as an inline caller keeps it: fpatan's result stays on the FPU stack unrounded (the precision
    /// control rounds only arithmetic), so a difference taken from it rounds once. CBattleEngine::UpdateAutoAim's desired
    /// offsets read it so (retail win1 P4199: 3d5b9024 where a rounded azimuth gives 3d5b9020).
    /// </summary>
    public static double AzimuthUnrounded(FVector v) => -X87.Atan2(v.X, v.Y);

    /// <summary>FVector::Elevation kept unrounded as <see cref="AzimuthUnrounded"/>: the CRT asin's double result.</summary>
    public static double ElevationUnrounded(FVector v)
    {
        float m = v.Magnitude();
        return m > 0.0f ? X87.Asin(v.Z / m) : 0.0;
    }

    /// <summary>AngleDifference of a float and an unrounded angle: each subtraction rounds to a float (precision control 24 bits).</summary>
    public static float AngleDifference(float a, double b)
    {
        if (a < -Pi / 2 && b > Pi / 2)
        {
            b = (float)(b - PiM2);
        }
        else if (a > Pi / 2 && b < -Pi / 2)
        {
            b = (float)(b + PiM2);
        }

        return (float)(a - b);
    }

    /// <summary>
    /// CWeapon::Unknown509140's straight aim (005093c6..00509434): the magnitude sqrt((Y*Y + X*X) + Z*Z) stored to a
    /// float; the pitch asin(Z / magnitude) through the CRT's __CIasin when the magnitude is positive, else 0, stored;
    /// the yaw the negated fpatan of X over Y, stored; then the out-of-line FMatrix(yaw, pitch, 0) (004062d0).
    /// </summary>
    public static (float Yaw, float Pitch) StraightAimAngles(FVector d)
    {
        float m = (float)X87.Sqrt(X87.Add(X87.Add(X87.Mul(d.Y, d.Y), X87.Mul(d.X, d.X)), X87.Mul(d.Z, d.Z)));
        float pitch = m > 0 ? (float)X87.Asin(X87.Div(d.Z, m)) : 0;
        return (-(float)X87.Atan2(d.X, d.Y), pitch);
    }

    /// <summary>
    /// The straight aim's orientation: <see cref="StraightAimAngles"/> through the out-of-line constructor. SpawnRound's
    /// tracking aim (00506c2d..00506ce0: the out-of-line FVector::Magnitude 004026b0, the quotient stored before the
    /// asin) gives the same yaw, pitch and matrix: retail's two blocks agree on 5,006 vectors.
    /// </summary>
    public static FMatrix StraightAimOrientation(FVector d)
    {
        (float yaw, float pitch) = StraightAimAngles(d);
        return FMatrix.MakeRotationOutOfLine(yaw, pitch, 0);
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
/// float-exact. Nonballistic reach/fire queries use the world line owner; ballistic queries remain explicitly
/// unsupported until their branch-specific arithmetic and native stack-slot reuse are modeled.
/// </summary>
public sealed partial class Weapon(ThingContext context, WeaponData data, int type) : Equipment
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
        if (AttachedTo!.IsA(ThingType.BattleEngine) && AttachedTo is BattleEngine resetBe)
        {
            resetBe.CurrentTarget = 0; // CBattleEngine::ResetTarget
        }

        if (SpawnRound() && Mode.LaunchSoundName is not null && Mode.SoundPerBurst != 0)
        {
            PlayLaunchSound(Mode);
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

    /// <summary>Weapon destruction: release member links/readers, then notify the monitor's pending event readers.</summary>
    public void Release()
    {
        foreach (ParticleEffectLink? effect in Effects) effect?.Release();
        Target.Release();
        Destroy();
    }

    /// <summary>
    /// MoveEmitter005078f0: the two retained muzzle effects follow their recorded launch indices. The launch-angle
    /// ordinal here is the EFFECT slot, not the recorded launch index. Retail calls the angle constructor004062d0
    /// and matrix product0040d320 out of line; orientate=false updates position only.
    /// </summary>
    public void MoveEmitter(bool orientate)
    {
        if (Mode is not { } mode) return;
        for (int i = 0; i < 2; i++)
        {
            if (Effects[i] is not { Full: true } effect) continue;
            AttachedTo!.GetLaunchPosition(this, Emitters[i], out FVector pos, out FMatrix ori, false);
            effect.SetPos(pos);
            if (!orientate) continue;
            FMatrix angle = FMatrix.MakeRotation(0, 1, 0);
            if (i < mode.LaunchAngles.Count)
            {
                LaunchAngle a = mode.LaunchAngles[i];
                angle = FMatrix.MakeRotationOutOfLine(a.A, a.B, 0);
            }
            effect.SetOrientation(ori.MultiplyOutOfLine(angle));
        }
    }

    /// <summary>mEffects / mEmitters: the two muzzle effects and the launch index each follows.</summary>
    public ParticleEffectLink?[] Effects { get; } = new ParticleEffectLink?[2];

    public int[] Emitters { get; } = new int[2];

    /// <summary>SOUND.PlayEffect of the mode's launch sound (a battle engine's untracked, a unit's following it).</summary>
    private void PlayLaunchSound(WeaponModeData mode)
    {
        if (Context.SoundEffects is { } sounds)
        {
            Sound.SoundEffect? effect = sounds.Find(System.Text.Encoding.Latin1.GetString(mode.LaunchSoundName!));
            if (sounds.EventKernelConfigured)
            {
                // Pristine74154bfa: Fire00506163..0050619c and SpawnRound00506a4d..00506a96.
                // The weapon's owner is the attached unit; a BE shot deliberately has no spatial owner.
                bool player = AttachedTo!.IsA(ThingType.BattleEngine);
                sounds.PlayEffect(effect, player ? null : AttachedTo, Context.Crt,
                    new Sound.SoundPlayback(Volume: .5f, Tracking: player ? Sound.SoundTracking.None : Sound.SoundTracking.FollowDontDie));
            }
            else sounds.PlayEffect(effect, Context.Crt);
        }
        else
        {
            Context.Skip("SOUND.PlayEffect (a weapon's launch sound)");
        }
    }

    /// <summary>
    /// SpawnRound's first launch product, orientation * launchAngle (onslaught-decomp src/weapon.cpp, FSpawnOri), with
    /// each cell summed in retail's order (pristine 74154bfa, 00506ed1..005072c3, as read for the native port's patch
    /// 0101): the bracketed pair first, then the last term.
    /// </summary>
    internal static FMatrix SpawnOrientationProduct(FMatrix a, FMatrix m)
    {
        FVector Row(FVector r, bool lastRow) => new(
            ((r.Z * m.Row2.X) + (r.Y * m.Row1.X)) + (r.X * m.Row0.X),
            ((r.X * m.Row0.Y) + (r.Z * m.Row2.Y)) + (r.Y * m.Row1.Y),
            lastRow ? ((r.X * m.Row0.Z) + (r.Z * m.Row2.Z)) + (r.Y * m.Row1.Z)
                : ((r.Z * m.Row2.Z) + (r.Y * m.Row1.Z)) + (r.X * m.Row0.Z));
        return new(Row(a.Row0, false), Row(a.Row1, false), Row(a.Row2, true));
    }

    /// <summary>
    /// SpawnRound's second launch product, (orientation * launchAngle) * inaccuracy (src/weapon.cpp, FSpawnTurn), each
    /// cell in retail's order (00506ed1..005072c3, port patch 0101).
    /// </summary>
    internal static FMatrix SpawnTurnProduct(FMatrix a, FMatrix m)
    {
        FVector r0 = a.Row0, r1 = a.Row1, r2 = a.Row2;
        return new(
            new FVector(((r0.X * m.Row0.X) + (r0.Z * m.Row2.X)) + (r0.Y * m.Row1.X),
                ((r0.Z * m.Row2.Y) + (r0.Y * m.Row1.Y)) + (r0.X * m.Row0.Y),
                ((r0.Z * m.Row2.Z) + (r0.Y * m.Row1.Z)) + (r0.X * m.Row0.Z)),
            new FVector(((r1.Y * m.Row1.X) + (r1.X * m.Row0.X)) + (r1.Z * m.Row2.X),
                ((r1.Z * m.Row2.Y) + (r1.Y * m.Row1.Y)) + (r1.X * m.Row0.Y),
                ((r1.Z * m.Row2.Z) + (r1.Y * m.Row1.Z)) + (r1.X * m.Row0.Z)),
            new FVector(((r2.Z * m.Row2.X) + (r2.Y * m.Row1.X)) + (r2.X * m.Row0.X),
                ((r2.Z * m.Row2.Y) + (r2.Y * m.Row1.Y)) + (r2.X * m.Row0.Y),
                ((r2.Z * m.Row2.Z) + (r2.Y * m.Row1.Z)) + (r2.X * m.Row0.Z)));
    }

    /// <summary>
    /// SpawnRound (005069f0): a battle engine's firing check, the launch sound (once per shot when not per burst), then a
    /// round per volley shot. Each shot takes the next launch position and launch angle, the attached unit's launch
    /// point (aimed at the target when an aim request tracks it), the round's speed along the launch orientation turned
    /// by two inaccuracy draws (GAME random), the flak lifespan error, the round's target and launcher, the muzzle effect,
    /// a clip's shell, and a battle engine's stats, lock and recoil.
    /// </summary>
    public bool SpawnRound()
    {
        bool fired = false;
        if (AttachedTo is BattleEngine be && !be.WeaponFired(this))
        {
            return false;
        }

        WeaponModeData? mode = Mode;
        if (mode is null)
        {
            return false;
        }

        if (mode.LaunchSoundName is not null && mode.SoundPerBurst == 0)
        {
            PlayLaunchSound(mode);
        }

        for (int shot = 0; shot < mode.VolleySize; shot++)
        {
            Round? round = Round.Spawn(Context, mode.Round);
            if (round is null)
            {
                continue;
            }

            var init = new InitThing();
            LaunchOrdinal++;
            int launchIndex = 1;
            if (Mode is not null)
            {
                if (LaunchOrdinal >= Mode.LaunchSequence.Count)
                {
                    LaunchOrdinal = 0;
                }

                launchIndex = GetLaunchSequence(Mode, LaunchOrdinal);
            }

            AngleOrdinal++;
            int angleIndex;
            if (Mode is not null)
            {
                if (AngleOrdinal >= Mode.LaunchAngles.Count)
                {
                    AngleOrdinal = 0;
                }

                angleIndex = AngleOrdinal;
            }
            else
            {
                angleIndex = 0;
            }

            AttachedTo!.GetLaunchPosition(this, launchIndex, out FVector pos, out FMatrix orientation, true);
            if (AimRequested != 0)
            {
                if (mode.Track != 0 && !WeaponMaths.IsBallistic(mode.Round!) && Target.ToRead is { } tracked)
                {
                    FVector delta = tracked.GetCentrePos() - pos;
                    AimOrientation = WeaponMaths.StraightAimOrientation(delta);
                }

                orientation = AimOrientation;
            }

            LaunchAngle? angle = angleIndex >= 0 && angleIndex < mode.LaunchAngles.Count ? mode.LaunchAngles[angleIndex] : null;
            FMatrix launchAngle = FMatrix.MakeRotation(0, 1, 0);
            if (angle is not null)
            {
                launchAngle = FMatrix.FromYawPitchRoll(angle.A, angle.B, 0);
            }

            var launchVelocity = new FVector(0, round.Data.Beam != 0 ? GetMaxRange(new FVector(0, 0, 0)) : round.Data.Speed / 20.0f, 0);
            float pitchError = ((Context.Random.FloatRandom() * 2) - 1) * mode.Inaccuracy;
            float yawError = ((Context.Random.FloatRandom() * 2) - 1) * mode.Inaccuracy;
            FMatrix inaccuracy = FMatrix.FromYawPitchRoll(yawError, pitchError, 0);
            Unit attached = AttachedTo!;
            init.Pos = pos + attached.GetVelocity();
            init.Dest = AimPosition;
            init.Orientation = SpawnTurnProduct(SpawnOrientationProduct(orientation, launchAngle), inaccuracy);
            init.Velocity = init.Orientation.Transform(launchVelocity);
            init.OrientationType = OrientationType.DirectionCosineMatrix;
            init.Allegiance = attached.Allegiance;
            init.RoundData = mode.Round;
            init.LifeSpan = mode.Round!.LifeSpan;
            Thing? target = attached.GetCurrentLaunchTarget();
            round.Launcher.SetReader(attached);
            if (round.Data.Flak != 0 && target is not null)
            {
                FVector delta = target.Pos - pos;
                float distance = delta.Magnitude();
                float roundSpeed = round.Data.Speed;
                if (roundSpeed * init.LifeSpan > distance)
                {
                    float percent = Context.Random.FloatRandom() * 100.0f;
                    float error = round.Data.FlakInaccuracy;
                    error *= distance;
                    float fraction = percent / 100.0f;
                    init.LifeSpan = ((fraction * error * 2) - error + distance) / roundSpeed;
                }
            }

            if (attached is BattleEngine battleEngine)
            {
                (battleEngine.Player as Game.Player)?.IncStat(Game.PlayerStat.RoundsFired);
                if (battleEngine.DisplayLock(this))
                {
                    battleEngine.FireLock(target);
                }
            }

            round.SetTarget(target, false);
            round.Init(init);
            if (mode.MuzzleEffectName is { } muzzle && Context.Particles is { } particles)
            {
                int effect;
                bool found = false;
                for (effect = 0; effect < 2; effect++)
                {
                    if (Effects[effect]?.Full != true)
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    Effects[effect] ??= new ParticleEffectLink(particles.Manager);
                    bool important = attached.UnitData is not { } ud || ud.Fenrir != 0;
                    particles.Manager.CreateEffect(Context.FindParticle(muzzle), Effects[effect], new FVector(0, 0, 0), false, important);
                    Effects[effect]!.SetPos(pos);
                    Effects[effect]!.SetOrientation(init.Orientation);
                    Emitters[effect] = launchIndex;
                }
                else
                {
                    var link = new ParticleEffectLink(particles.Manager);
                    particles.Manager.CreateEffect(Context.FindParticle(muzzle), link, new FVector(0, 0, 0), false, attached.IsA(ThingType.BattleEngine));
                    if (link.Full)
                    {
                        link.SetPos(pos);
                        link.SetOrientation(init.Orientation);
                    }

                    link.Release();
                }

                if (mode.MuzzleLight != -1)
                {
                    Context.Skip("ENGINE.LogLight (a weapon's muzzle light)");
                }
            }

            if (mode.ClipName is not null)
            {
                throw new NotSupportedException("CShell (a weapon's clip, three GAME randoms) is not ported");
            }

            if (attached is BattleEngine recoiling)
            {
                recoiling.RecoilWeapon(this);
            }

            fired = true;
        }

        return fired;
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

            AimOrientation = WeaponMaths.StraightAimOrientation(d);
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
            // (Z*Z + Y*Y) + X*X for the distance (0050a1ac..0050a1ca; port patch 0098) and (Y*Y + Z*Z) + X*X for the speed
            // (0050a1d4..0050a1f0; port patch 0112): retail's inlined Magnitude at both sites, not maths.h's X, Y, Z order.
            float t = (pos - tpos).MagnitudeYZX() / vel.MagnitudeYZX();
            t *= 20.0f;
            return (tvel * t) + tpos;
        }

        return target.GetTargetablePos();
    }

    /// <summary>CWeaponModeData::GetLaunchSequence: the sequence's value at that position, 0 past its end.</summary>
    public static int GetLaunchSequence(WeaponModeData mode, int index) =>
        index >= 0 && index < mode.LaunchSequence.Count ? mode.LaunchSequence[index] : 0;

    public override string ToString() => $"weapon '{Data.NameText}' mode '{Mode?.NameText}'";
}
