// SPDX-License-Identifier: GPL-3.0-only
// Adapted from matched onslaught-decomp Round.cpp CRound::Move004d8e40, seeking block004d92c7..004d983a.
// Instruction order read from pristine PC SHA-256
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

public partial class Round
{
    /// <summary>The post-movement target release and seeking turn; called after unwiggling the velocity.</summary>
    internal void UpdateSeeking()
    {
        // Native (a bit test of ah against 1) skips both negative and unordered damage, unlike !(damage < 0).
        if (!(Data.Damage >= 0)) return;
        if (Target.ToRead is { } target)
        {
            if (target.IsA(ThingType.BattleEngine))
            {
                // The distance's unordered result takes the near arm. The BE does not use UnitData.CanBeLocked.
                if (!(LockDistanceSquared(target.Pos, Pos) >= 225) && !((BattleEngine)target).CanBeLocked())
                {
                    ReleaseTarget();
                    Unknown130 = 0; // only losing a nearby BE lock disables later BE reacquisition
                }
            }
            else if (target.IsDying || target is Unit { UnitData.CanBeLocked: 0 }) ReleaseTarget();
        }

        if (!(Data.TurnRate > 0) || Target.ToRead is not { } seeking) return;
        double age = X87.Sub(Context.Events.Time, StartTime); // not spilled before either comparison
        if (Data.SeekDelay >= age || !(Data.SeekTerminationTime > age) || (Data.Torpedo != 0 && !IsInWater())) return;
        if (seeking is not Unit unit) throw new NotSupportedException("CRound::Move seeking requires CUnit::GetTargetablePos");
        FMatrix orientation = Orientation; // copied/transposed before the virtual target-position call
        FVector pos = unit.GetTargetablePos();
        if (Data.Torpedo != 0) pos.Z = Context.Map.WaterLevel;
        FVector d = SeekDirection(orientation, pos, Pos);
        double cosine = X87.Cos(Data.SeekAngle); // native FCOS is compared before a float store
        if (Data.WeirdoSeek == 0 && cosine > d.Y && Data.Torpedo == 0)
        {
            ReleaseTarget();
            return;
        }
        if (Data.WeirdoSeek != 0 && !(cosine > d.Y)) return;
        (float yaw, float pitch) = SeekTurn(d, Data.TurnRate);
        Orientation = SeekOrientation(Orientation, FMatrix.MakeRotationOutOfLine(yaw, pitch, 0));
        Velocity = SeekVelocity(Orientation, GetVelocity());
    }

    internal static double LockDistanceSquared(FVector target, FVector pos)
    {
        double x = X87.Sub(target.X, pos.X), y = X87.Sub(target.Y, pos.Y), z = X87.Sub(target.Z, pos.Z);
        return X87.Add(X87.Add(X87.Mul(x, x), X87.Mul(z, z)), X87.Mul(y, y));
    }

    internal static FVector SeekDirection(FMatrix orientation, FVector target, FVector pos)
    {
        // 004d94ab: the position differences retain their wide exponents. The transpose transform has
        // Z+Y+X in rows0/1, Z+X+Y in row2, then stores each direction component to float.
        double x = X87.Sub(target.X, pos.X), y = X87.Sub(target.Y, pos.Y), z = X87.Sub(target.Z, pos.Z);
        var d = new FVector(
            (float)X87.Add(X87.Add(X87.Mul(orientation.Row2.X, z), X87.Mul(orientation.Row1.X, y)), X87.Mul(orientation.Row0.X, x)),
            (float)X87.Add(X87.Add(X87.Mul(orientation.Row2.Y, z), X87.Mul(orientation.Row1.Y, y)), X87.Mul(orientation.Row0.Y, x)),
            (float)X87.Add(X87.Add(X87.Mul(orientation.Row2.Z, z), X87.Mul(orientation.Row0.Z, x)), X87.Mul(orientation.Row1.Z, y)));
        double magnitude = X87.Sqrt(X87.Add(X87.Add(X87.Mul(d.Y, d.Y), X87.Mul(d.Z, d.Z)), X87.Mul(d.X, d.X)));
        if (magnitude != 0 && !double.IsNaN(magnitude))
        {
            double reciprocal = X87.Div(1, magnitude);
            d = new((float)X87.Mul(reciprocal, d.X), (float)X87.Mul(reciprocal, d.Y), (float)X87.Mul(reciprocal, d.Z));
        }
        return d;
    }

    internal static (float Yaw, float Pitch) SeekTurn(FVector d, float rate)
    {
        double yaw = -X87.Atan2(d.X, d.Y); // retained through its clamp, unlike pitch
        float pitch = (float)X87.Atan2(d.Z, X87.Sqrt(X87.Add(X87.Mul(d.Y, d.Y), X87.Mul(d.X, d.X))));
        static float Clamp(double angle, float limit) => !(Math.Abs(angle) >= limit)
            ? (float)angle : angle > 0 ? limit : -limit;
        return (Clamp(yaw, rate), Clamp(pitch, rate));
    }

    internal static FMatrix SeekOrientation(FMatrix a, FMatrix b)
    {
        // 004d967f..004d97c5: all nine cells use (Z product + Y product) + X product.
        static float Cell(FVector row, float x, float y, float z) =>
            (float)X87.Add(X87.Add(X87.Mul(z, row.Z), X87.Mul(y, row.Y)), X87.Mul(x, row.X));
        FVector Row(FVector row) => new(Cell(row, b.Row0.X, b.Row1.X, b.Row2.X),
            Cell(row, b.Row0.Y, b.Row1.Y, b.Row2.Y), Cell(row, b.Row0.Z, b.Row1.Z, b.Row2.Z));
        return new(Row(a.Row0), Row(a.Row1), Row(a.Row2));
    }

    internal static FVector WiggleVelocity(FMatrix m, FVector v)
    {
        // 004d90d6..004d916a: (X + Y) + Z in every row.
        float Row(FVector r) => (float)X87.Add(X87.Add(X87.Mul(r.X, v.X), X87.Mul(r.Y, v.Y)), X87.Mul(r.Z, v.Z));
        return new(Row(m.Row0), Row(m.Row1), Row(m.Row2));
    }

    internal static FVector UnwiggleVelocity(FMatrix m, FVector v)
    {
        // 004d921e..004d929a: (Y + Z) + X in every row.
        float Row(FVector r) => (float)X87.Add(X87.Add(X87.Mul(r.Y, v.Y), X87.Mul(r.Z, v.Z)), X87.Mul(r.X, v.X));
        return new(Row(m.Row0), Row(m.Row1), Row(m.Row2));
    }

    internal static FVector SeekVelocity(FMatrix m, FVector velocity)
    {
        // 004d97d4: speed is sqrt((X²+Y²)+Z²), stored to float. The call0040d2c0 multiplies
        // by the full (0,speed,0) vector: retaining zero products matters for NaNs and signed zeros.
        float speed = (float)X87.Sqrt(X87.Add(X87.Add(X87.Mul(velocity.X, velocity.X), X87.Mul(velocity.Y, velocity.Y)), X87.Mul(velocity.Z, velocity.Z)));
        float Row(FVector r) => (float)X87.Add(X87.Add(X87.Mul(r.Z, 0), X87.Mul(r.Y, speed)), X87.Mul(r.X, 0));
        return new(Row(m.Row0), Row(m.Row1), Row(m.Row2));
    }
}
