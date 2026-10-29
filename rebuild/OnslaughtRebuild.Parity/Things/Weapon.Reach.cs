// SPDX-License-Identifier: GPL-3.0-only
// Matched onslaught-decomp weapon.cpp: CWeapon::Unknown5088b0 (CanReach), Unknown507ab0 (CanFireAt).
// Re-derived from pristine SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Physics;
using OnslaughtRebuild.Parity.World;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class Weapon
{
    /// <summary>005088b0: reachability, including the deflection band and the first line hit's allegiance.</summary>
    public bool CanReach(Unit target)
    {
        if (GetPrimaryPosition().Z > Context.WaterLevel) return false;
        if (Mode?.Round is { GridOfFear: not 0 })
        {
            // Unlike CanFireAt, every nonzero allegiance selects Muspell here (00508918..00508931).
            var grid = AttachedTo!.Allegiance == 0 ? Context.Game?.ForsetiFearGrid : Context.Game?.MuspellFearGrid;
            if (grid is not null && !grid.Unknown44c780(target.Pos)) return false;
        }

        WeaponModeData mode = RequireQueryMode();
        RoundData round = mode.Round!;
        FVector velocity = QueryForward(GetPrimaryOrientation(), round.Speed);
        FVector targetPos = GetPredictedTargetPos(target);
        FVector delta = targetPos - GetPrimaryPosition();
        float targetPitch = (float)QueryElevation(delta);
        float pitch = Unknown98 != 0 ? AttachedTo!.UnknownE8 : (float)X87.Sub(targetPitch, QueryElevation(velocity));
        if (QueryIsBallistic(round))
            throw new NotSupportedException("CWeapon::CanReach ballistic range/stack-slot reuse is not ported");
        if (round.Torpedo != 0)
            // C0-only comparison (00508e5c): unordered, like strict less-than, takes the true arm.
            return !(Context.WaterLevel >= Context.Map.Collide(AttachedTo!.Pos));
        if (Unknown98 != 0)
        {
            FMatrix absolute = QueryTurretOrientation();
            pitch = QuerySlopePitch(pitch, new FVector(absolute.Row0.Y, absolute.Row1.Y, absolute.Row2.Y),
                Context.Map.Normal(AttachedTo!.Pos));
        }

        if (!ReachPitchInBand(pitch, mode.MinDeflection, mode.MaxDeflection)) return false;
        if (round.Seek != 0) return true;
        return QueryHitsTargetSide(target, targetPos, AttachedTo);
    }

    /// <summary>
    /// 00507ab0: firing direction, followed by an optional line test. Its strict free-barrel pitch bounds differ
    /// from CanReach's inclusive ones; its separately articulated barrel uses the yaw tolerance for pitch too.
    /// </summary>
    public bool CanFireAt(Unit target, bool a)
    {
        if (GetPrimaryPosition().Z > Context.WaterLevel) return false;
        if (Mode?.Round is { GridOfFear: not 0 })
        {
            var grid = AttachedTo!.Allegiance switch
            {
                0 => Context.Game?.ForsetiFearGrid,
                1 => Context.Game?.MuspellFearGrid,
                _ => null,
            };
            if (grid is not null && !grid.Unknown44c780(target.Pos)) return false;
        }

        WeaponModeData mode = RequireQueryMode();
        RoundData round = mode.Round!;
        FVector velocity = QueryForward(GetPrimaryOrientation(), round.Speed);
        FVector yawVelocity = Unknown94 != 0 ? QueryForward(QueryTurretOrientation(), round.Speed) : velocity;
        float yaw = -(float)X87.Atan2(yawVelocity.X, yawVelocity.Y);
        float pitch = Unknown98 != 0 ? AttachedTo!.UnknownE8 : (float)QueryElevation(velocity);
        FVector targetPos = GetPredictedTargetPos(target);
        FVector delta = targetPos - GetPrimaryPosition();
        float targetYaw = -(float)X87.Atan2(delta.X, delta.Y);
        float targetPitch = (float)QueryElevation(delta);
        if (Math.Abs(QueryAngleDifference(targetYaw, yaw)) >= mode.YawTolerance) return false;
        if (QueryIsBallistic(round))
            throw new NotSupportedException("CWeapon::CanFireAt ballistic arcs/short obstruction lines are not ported");
        if (round.Torpedo != 0) return !(Context.WaterLevel >= Context.Map.Collide(AttachedTo!.Pos));
        if (!FirePitchInBand(targetPitch, pitch, mode.MinDeflection, mode.MaxDeflection, mode.YawTolerance, Unknown98 != 0))
            return false;
        if (!a) return true;
        return QueryHitsTargetSide(target, targetPos, AttachedTo!.Unknown24C != 0 ? AttachedTo : null);
    }

    private WeaponModeData RequireQueryMode() => Mode is { Round: not null } mode ? mode
        : throw new InvalidOperationException("CWeapon reach/fire query requires an initialized mode and round");

    private static bool QueryIsBallistic(RoundData round)
    {
        double gravity = X87.Mul(round.Gravity, 0.025f);
        return !double.IsNaN(gravity) && gravity != 0 && round.Beam == 0 && round.Torpedo == 0;
    }

    // These sites fold the zero products before compilation; multiplying arbitrary other columns by zero differs
    // for infinities/NaNs and signed zero. Each surviving Y product is stored to a float (0050897f..005089a4).
    private static FVector QueryForward(FMatrix orientation, float speed) =>
        new(speed * orientation.Row0.Y, speed * orientation.Row1.Y, speed * orientation.Row2.Y);

    private FMatrix QueryTurretOrientation() =>
        FMatrix.MakeRotationOutOfLine(AttachedTo!.UnknownE0, 0, 0).MultiplyOutOfLine(AttachedTo.Orientation);

    private bool QueryHitsTargetSide(Unit target, FVector targetPos, Unit? ignore)
    {
        var report = new WorldLineReport();
        LineHitType hit = WorldLineQuery.FindFirstThingToHitLine(Context, GetPrimaryPosition(), targetPos, ignore, report,
            firstOnly: false, meshCollision: !target.IsA(ThingType.Building), level: 1,
            ignoreTypes: ThingType.Ammunition, mapFlag: true, allowedTypes: uint.MaxValue);
        // A clear line is not success. Another unit of the target's side is a valid first hit, even if it is not target.
        return hit == LineHitType.Thing && report.HitThing is Unit unit && unit.IsA(ThingType.Unit)
            && unit.Allegiance == target.Allegiance;
    }

    /// <summary>004026b0 + caller quotient spill + 0055dcb0: retained magnitude, float argument, retained asin.</summary>
    internal static double QueryElevation(FVector v)
    {
        double magnitude = X87.Sqrt(X87.Add(X87.Add(X87.Mul(v.X, v.X), X87.Mul(v.Y, v.Y)), X87.Mul(v.Z, v.Z)));
        return magnitude > 0 ? X87.Asin((float)X87.Div(v.Z, magnitude)) : 0;
    }

    internal static float QueryRelativePitch(FVector delta, FVector velocity) =>
        (float)X87.Sub((float)QueryElevation(delta), QueryElevation(velocity));

    internal static double QueryAngleDifference(float target, float current)
    {
        double c = current;
        if (target < -WeaponMaths.Pi / 2 && current > WeaponMaths.Pi / 2) c = X87.Sub(c, WeaponMaths.PiM2);
        else if (target > WeaponMaths.Pi / 2 && current < -WeaponMaths.Pi / 2) c = X87.Add(c, WeaponMaths.PiM2);
        double difference = X87.Sub(target, c);
        if (difference < -WeaponMaths.Pi) difference = X87.Add(difference, WeaponMaths.PiM2);
        else if (difference > WeaponMaths.Pi) difference = X87.Sub(difference, WeaponMaths.PiM2);
        return difference;
    }

    // Native status-word masks differ from reconstructed C++ on unordered comparisons (00508fba/00508fcf,
    // 00508712/00508729/0050873a). Preserve those predicates, including the first upper bound's NaN behavior.
    internal static bool ReachPitchInBand(float pitch, float min, float max) => min >= pitch && !(max > pitch);

    internal static bool FirePitchInBand(float targetPitch, float pitch, float min, float max, float tolerance, bool barrel)
    {
        if (barrel) return !(Math.Abs(QueryAngleDifference(targetPitch, pitch)) > tolerance);
        double difference = X87.Sub(targetPitch, pitch);
        return !(difference >= min) && difference > max;
    }

    /// <summary>00508edf..00508fa7: mixed yaw rounding and unspilled double-cross products, one final pitch spill.</summary>
    internal static float QuerySlopePitch(float pitch, FVector turretForward, FVector normal)
    {
        double yaw = -X87.Atan2(turretForward.X, turretForward.Y);
        float x = -(float)X87.Sin((float)yaw);
        float y = (float)X87.Cos(yaw);
        const float z = 0;
        double cy = X87.Sub(X87.Mul(normal.X, z), X87.Mul(normal.Z, x));
        double cx = X87.Sub(X87.Mul(normal.Z, y), X87.Mul(normal.Y, z));
        double slope = X87.Sub(X87.Mul(normal.Y, cx), X87.Mul(normal.X, cy));
        return (float)X87.Add(slope, pitch);
    }
}
