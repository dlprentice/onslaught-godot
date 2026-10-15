// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/Unit.cpp (CUnit::Unknown77 004fa4b0, UpdateTimer 004fa800, Move 004fa8d0, Unknown92
// 004fb270, ObeyGravity 004fdc90, IsDeploying 004fde10) and src/unit.h (Unknown72 00405dc0, Unknown73 00405dd0,
// Unknown76 00405de0, Unknown109 00405e70), reconstructed; and src/maths.h (AngleDifference,
// Stuart's). Read from the Steam BEA.exe (= the pristine specimen 74154bfa): the constants are float (PI 0x40490fdb,
// 2PI, PI/2, 0.1f); each operation is one 24-bit rounding in the source's order except where noted.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

public abstract partial class Unit
{
    private const float Pi = 3.14159274f;
    private const float TwoPi = 6.28318548f;
    private const float HalfPi = 1.57079637f;

    /// <summary>mUnknown170 / mUnknown174: a value ramping by 0.1 a move towards its target.</summary>
    public float Unknown170;

    public float Unknown174;

    /// <summary>mUnknown248: cleared at the start of each move.</summary>
    public int Unknown248;

    /// <summary>mUnknownE4 / mUnknownF0: the turret yaw and barrel pitch before this move.</summary>
    public float UnknownE4;

    public float UnknownF0;

    /// <summary>CUnit::ObeyGravity (004fdc90): not while following an emitter (AI mode 2).</summary>
    public override bool ObeyGravity() => !(AI is not null && AI.AIMode == 2);

    /// <summary>Unknown72 (slot 72, 00405dc0): the friction on the ground.</summary>
    public virtual float Unknown72() => 0.25f;

    /// <summary>Unknown73 (slot 73, 00405dd0): the friction in the air.</summary>
    public virtual float Unknown73() => 0.98f;

    /// <summary>CUnit::IsDeploying (004fde10): deploy states 3, 4 and 5.</summary>
    public bool IsDeploying() => DeployState is 4 or 3 or 5;

    /// <summary>Unknown76 (slot 76): the unit turns towards its target orientation unless preparing a shot or inactive (and alive).</summary>
    public virtual bool Unknown76() => !((Unknown168 != 0 || !Active) && (Flags & ThingFlags.Dying) == 0);

    /// <summary>Unknown109 (slot 109): the turret turns when no shot is being prepared.</summary>
    public virtual bool Unknown109() => Unknown168 == 0;

    /// <summary>Unknown92 (slot 92): the current yaw.</summary>
    public virtual float Unknown92() => CurrentOrientation.Yaw;

    /// <summary>AngleDifference: a - b, with b taken round by 2PI when the two sit either side of +-PI/2.</summary>
    public static float AngleDifference(float a, float b)
    {
        if (a < -HalfPi && b > HalfPi)
        {
            b -= TwoPi;
        }
        else if (a > HalfPi && b < -HalfPi)
        {
            b += TwoPi;
        }

        return a - b;
    }

    /// <summary>
    /// CUnit::Move: the guide steers (or the unit stops), the shot timer, CActor's move, the turn towards the target
    /// orientation, shields, the ramp, the turret and barrel slews towards the AI's target, and the deploy states. The
    /// particle links (vents, firing effect, dust) and the spawner's sound are presentation (counted).
    /// </summary>
    public override void Move()
    {
        if (Guide is not null && (Active || IsDying))
        {
            Guide.Steer();
        }
        else
        {
            Stop();
        }

        Unknown248 = 0;
        UpdateTimer();

        base.Move();

        if (Unknown76())
        {
            if (Unknown120.Yaw != CurrentOrientation.Yaw || Unknown120.Pitch != CurrentOrientation.Pitch
                || Unknown120.Roll != CurrentOrientation.Roll)
            {
                Unknown77(ref CurrentOrientation, Unknown120, Unknown12C, out Orientation);
            }
        }

        if (!IsDying)
        {
            Shields += ShieldRegeneration;
            if (Shields > MaxShields)
            {
                Shields = MaxShields;
            }
        }
        else if (UnitData!.Big != 0)
        {
            throw new NotSupportedException("CUnit::Unknown95 (a big unit's dying explosions) is not ported");
        }

        if (Unknown170 + 0.1f < Unknown174)
        {
            Unknown170 += 0.1f;
        }
        else if (Unknown170 - 0.1f > Unknown174)
        {
            Unknown170 -= 0.1f;
        }
        else
        {
            Unknown170 = Unknown174;
        }

        if (UnitData!.Hover != 0 && IsOnGround())
        {
            Context.Skip("CUnit::Move's dust particles");
        }

        if (!IsDying && !(Life < 0) && Unknown1F0 != 0 && UnitData.Spawners.Count > 0)
        {
            throw new NotSupportedException("CSpawner::DoSpawn (from CUnit::Move) is not ported");
        }

        UnknownE4 = UnknownE0;
        UnknownF0 = UnknownE8;
        float mult = GetMoveMultiplier();
        if (AI is not null && !IsDying && Unknown109())
        {
            SlewTurret(mult);
        }

        if (DeployState == 1)
        {
            if (Guide is not null)
            {
                FVector dest = Guide.Destination;
                float dx = Pos.X - dest.X;
                float dy = Pos.Y - dest.Y;
                if ((float)X87.Sqrt((dy * dy) + (dx * dx)) < 4.0f)
                {
                    DeployState = 2;
                    GoToPoint(new FVector((Orientation.Row0.Y * 300.0f) + Pos.X, (Orientation.Row1.Y * 300.0f) + Pos.Y, 0.0f + Pos.Z));
                }
            }
        }
        else if (DeployState == 2)
        {
            throw new NotSupportedException("CUnit::Unknown116 (deploying) is not ported");
        }
        else if (DeployState == 4)
        {
            if (GetCurrentTarget() is null)
            {
                StartUndeploying();
            }
        }

        if (UnitData.Spawners.Count > 0)
        {
            Context.Skip("CUnit::Move's spawner working sound (mSounds[1], presentation)");
        }

        UpdatePosition();
    }

    /// <summary>
    /// The turret's yaw towards the AI's target, from the muzzle (orientation * mUnknown1F8 + position, rows summed
    /// (Z + Y) + X at 004fa9b6..004fa9e2): the azimuth -fpatan(dX, dY) less the current yaw, wrapped, limited to
    /// mult * the turret's arc, slewed at mult * the turn rate, then held within the arc; and the barrel's pitch towards
    /// mUnknownEC at the same rate.
    /// </summary>
    private void SlewTurret(float mult)
    {
        float delta = UnknownE0;
        if (AI!.Target.ToRead is { } target)
        {
            FVector aim = target.GetTargetablePos();
            FMatrix o = Orientation;
            FVector v = Unknown1F8;
            float muzzleX = (o.Row0.Z * v.Z) + (o.Row0.Y * v.Y) + (v.X * o.Row0.X) + Pos.X;
            float muzzleY = (o.Row1.Z * v.Z) + (o.Row1.Y * v.Y) + (v.X * o.Row1.X) + Pos.Y;
            float azimuth = (float)-X87.Atan2(aim.X - muzzleX, aim.Y - muzzleY);
            delta = azimuth - Unknown92();
            Unknown20C = Context.Events.Time + 10.0f;
        }
        else if (Unknown20C < Context.Events.Time)
        {
            delta = 0;
        }

        if (delta > Pi)
        {
            delta -= TwoPi;
        }
        else if (delta < -Pi)
        {
            delta += TwoPi;
        }

        float limit = mult * UnitData!.MaxTurretYaw;
        if (delta > limit)
        {
            delta = limit;
        }
        else if (delta < -limit)
        {
            delta = -limit;
        }

        float rate = mult * UnitData.TurretTurnRate;
        if (rate < MathF.Abs(AngleDifference(UnknownE0, delta)))
        {
            if (delta > UnknownE0)
            {
                UnknownE0 = delta - UnknownE0 > Pi ? UnknownE0 - rate : UnknownE0 + rate;
            }
            else
            {
                UnknownE0 = UnknownE0 - delta > Pi ? UnknownE0 + rate : UnknownE0 - rate;
            }

            if (UnknownE0 > Pi)
            {
                UnknownE0 -= TwoPi;
            }
            else if (UnknownE0 < -Pi)
            {
                UnknownE0 += TwoPi;
            }
        }
        else
        {
            UnknownE0 = delta;
        }

        if (UnknownE0 > UnitData.MaxTurretYaw)
        {
            UnknownE0 = UnitData.MaxTurretYaw;
        }
        else if (UnknownE0 < -UnitData.MaxTurretYaw)
        {
            UnknownE0 = -UnitData.MaxTurretYaw;
        }

        if (HasBarrel)
        {
            float pitchRate = mult * UnitData.TurretTurnRate;
            if (pitchRate < MathF.Abs(UnknownE8 - UnknownEC))
            {
                UnknownE8 = UnknownE8 < UnknownEC ? UnknownE8 + pitchRate : UnknownE8 - pitchRate;
            }
            else
            {
                UnknownE8 = UnknownEC;
            }
        }
    }

    /// <summary>
    /// CUnit::Unknown77: each angle slews towards its target by (|current - target| * mult) * 0.1, at most mult times
    /// its rate (yaw and roll the short way round, wrapped to +-PI); then the orientation from the angles (the same
    /// inline FMatrix(yaw, pitch, roll) sequence as CComplexThing::Init's, 004fa6c6..004fa7ec).
    /// </summary>
    public void Unknown77(ref EulerAngles cur, EulerAngles target, EulerAngles rate, out FMatrix result)
    {
        float mult = GetMoveMultiplier();
        if (cur.Yaw != target.Yaw)
        {
            cur.Yaw = SlewWrapped(cur.Yaw, target.Yaw, mult * rate.Yaw, mult);
        }

        if (cur.Pitch != target.Pitch)
        {
            float maxStep = mult * rate.Pitch;
            float step = MathF.Abs(MathF.Abs(cur.Pitch - target.Pitch)) * mult * 0.1f;
            if (step > maxStep)
            {
                step = maxStep;
            }

            cur.Pitch = cur.Pitch > target.Pitch ? cur.Pitch - step : cur.Pitch + step;
        }

        if (cur.Roll != target.Roll)
        {
            cur.Roll = SlewWrapped(cur.Roll, target.Roll, mult * rate.Roll, mult);
        }

        result = FMatrix.FromYawPitchRoll(cur.Yaw, cur.Pitch, cur.Roll);
    }

    /// <summary>Unknown77's yaw and roll arms (the same text twice in the source).</summary>
    private static float SlewWrapped(float current, float target, float maxStep, float mult)
    {
        float t = target;
        float c = current;
        if (c < -HalfPi && t > HalfPi)
        {
            t -= TwoPi;
        }
        else if (c > HalfPi && t < -HalfPi)
        {
            t += TwoPi;
        }

        float step = MathF.Abs(c - t) * mult * 0.1f;
        if (step > maxStep)
        {
            step = maxStep;
        }

        float r = current;
        if (current < target)
        {
            r = target - current > Pi ? r - step : r + step;
        }
        else
        {
            r = current - target > Pi ? r + step : r - step;
        }

        if (r > Pi)
        {
            r -= TwoPi;
        }
        else if (r < -Pi)
        {
            r += TwoPi;
        }

        return r;
    }

    /// <summary>
    /// CUnit::UpdateTimer: the prepared shot fires when due (Unknown87, not ported) or is marked ready; a cooling
    /// unit (2) waits; a recoiling one (3) keeps its old orientation. While waiting the unit stops.
    /// </summary>
    public void UpdateTimer()
    {
        switch (Unknown168)
        {
            case 1:
                if (Unknown16C <= Context.Events.Time)
                {
                    if (Unknown1EC != 0)
                    {
                        Unknown1E8 = 1;
                        Unknown168 = 0;
                    }
                    else
                    {
                        throw new NotSupportedException("CUnit::Unknown87 (firing the prepared shot) is not ported");
                    }

                    return;
                }

                Unknown1E8 = 0;
                Stop();
                break;
            case 2:
                Unknown1E8 = 0;
                if (Unknown16C < Context.Events.Time)
                {
                    Unknown168 = 0;
                    return;
                }

                Stop();
                break;
            case 3:
                if (Unknown16C < Context.Events.Time)
                {
                    Unknown168 = 0;
                }

                Orientation = OldOrientation;
                break;
        }
    }
}
