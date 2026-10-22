// SPDX-License-Identifier: GPL-3.0-only
// Adapted from matched bea-decomp PlaneAI.cpp: Unknown9 004d21c0 and Unknown8 004164b0 (folded with BomberAI).
// Pristine PC SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

public sealed partial class PlaneAI
{
    /// <summary>Plane fighting: update targeting/fire first, then escort or approach/retreat, then reuse the fight event.</summary>
    public override void Unknown9(Event e)
    {
        if (ForUnit.IsDying) return;
        Unknown3(); // Its delay is deliberately ignored; its targeting, firing and conditional draws are not.

        if (ForUnit.UnitData!.Wingman != 0 && Context.Game?.GetPlayer(0)?.BattleEngine is { } engine)
        {
            double distance = EscortDistance(engine.Pos, ForUnit.Pos);
            if (distance > 120.0f)
            {
                ForUnit.GoToPoint(engine.Pos, false);
                Target.SetReader(null);
            }
            else if (!(distance >= 30.0f) && Target.ToRead is null)
            {
                // The native C0-only test admits unordered values as well as values below 30.
                ForUnit.Stop();
            }
        }

        if (Target.ToRead is not null)
        {
            ((Plane)ForUnit).OpenWings();
            FVector aim = Target.ToRead!.GetTargetablePos();
            // Both calls above can touch monitored owners; retail rereads rather than caching the entry target.
            Unit target = Target.ToRead!;
            double distance = AttackDistance(aim, ForUnit.Pos, ForUnit.GetRadius(), target.GetRadius());
            int band;
            if (Unknown60 != 0)
            {
                if (!(distance >= 25.0f))
                {
                    band = 2;
                    Unknown60 = 0;
                }
                else band = ForUnit.Unknown4fb670(Target.ToRead);
            }
            else
            {
                if (distance > 70.0f) Unknown60 = 1;
                band = 2; // The first step beyond 70 still retreats; only the following step can approach.
            }

            switch (band)
            {
                case 0:
                case 1: ForUnit.GoToPoint(aim, false); break;
                case 2: ForUnit.GoToPoint(RetreatPoint(ForUnit.Pos, aim), false); break;
            }
        }
        else
        {
            ((Plane)ForUnit).CloseWings();
            Unknown60 = 0;
            if (Unknown5()) return; // A newly queued waypoint walk replaces this fight event: no scheduling draw.
            if (ForUnit.UnitData!.Wingman == 0) ForUnit.Stop();
        }

        Context.Events.AddEvent(Fight, this, NextFightTime(Context.Events.Time, Context.Random.Random()),
            EventPriority.StartOfFrame, null, e as ScheduledEvent);
    }

    /// <summary>The plane's folded waypoint step: steer first, advance the reader, fire, and wait exactly one second.</summary>
    public override void Unknown8(Event e)
    {
        if (Waypoint.ToRead is null)
        {
            Unknown6();
            return;
        }

        if (ForUnit.Unknown76())
        {
            ForUnit.GoToPoint(Waypoint.ToRead.Pos, false);
            Thing waypoint = Waypoint.ToRead!;
            if (!(WaypointDistance(waypoint.Pos, ForUnit.Pos) >= 4.0f))
                Waypoint.SetReader(((Waypoint)waypoint).Next.ToRead);
            Unknown3();
        }

        // This remains a waypoint event even when the reader just reached null; the next event starts fighting.
        Context.Events.AddEvent(FollowWaypoints, this, (float)X87.Add(Context.Events.Time, 1.0f),
            EventPriority.StartOfFrame, null, e as ScheduledEvent);
    }

    // These helpers preserve the native expression order, x87 PC24 wide exponents and explicit float spills.
    // They are internal only to compare against isolated unchanged-instruction oracle blocks.
    internal static double EscortDistance(FVector engine, FVector plane)
    {
        double x = X87.Sub(engine.X, plane.X), y = X87.Sub(engine.Y, plane.Y), z = X87.Sub(engine.Z, plane.Z);
        return X87.Sqrt(X87.Add(X87.Add(X87.Mul(z, z), X87.Mul(y, y)), X87.Mul(x, x)));
    }

    internal static double AttackDistance(FVector aim, FVector plane, float radius, float targetRadius)
    {
        double x = X87.Sub(aim.X, plane.X), y = X87.Sub(aim.Y, plane.Y);
        float z = (float)X87.Sub(aim.Z, plane.Z);
        float magnitude = (float)X87.Sqrt(X87.Add(X87.Add(X87.Mul(y, y), X87.Mul(z, z)), X87.Mul(x, x)));
        float withoutSelf = (float)X87.Sub(magnitude, radius);
        return X87.Sub(withoutSelf, targetRadius);
    }

    internal static FVector RetreatPoint(FVector plane, FVector aim) => new(
        (float)X87.Sub(plane.X, X87.Sub(aim.X, plane.X)),
        (float)X87.Sub(plane.Y, (float)X87.Sub(aim.Y, plane.Y)),
        (float)X87.Sub(plane.Z, (float)X87.Sub(aim.Z, plane.Z)));

    internal static double WaypointDistance(FVector waypoint, FVector plane)
    {
        double x = X87.Sub(waypoint.X, plane.X), y = X87.Sub(waypoint.Y, plane.Y);
        return X87.Sqrt(X87.Add(X87.Mul(y, y), X87.Mul(x, x)));
    }

    internal static float NextFightTime(float time, int random) =>
        (float)X87.Add(X87.Add(X87.Mul(random % 65536, 1.0f / 262144.0f), 0.25f), time);
}
