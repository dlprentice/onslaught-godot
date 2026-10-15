// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/Guide.h and src/Guide.cpp (CGuide: the constructor 0047e290, GoToPoint 0047e2d0, Unknown5
// 0047e310, Unknown6 0047e340, Unknown7 0047e370, Stop 0047e3d0) and src/AirGuide.cpp (CAirGuide: the constructor
// 00402150, HandleEvent 004026e0, FindNearest 004027c0, UpdateClearance 004028e0). Not in Stuart's release.

using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Maths;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CGuide: what steers a unit towards the point its AI picks. The base keeps the destination and the mode (0 stopped,
/// 1 going to a point, 2 and 3 set by slots 5 to 7); each subclass's Unknown3 (the steering, called from CUnit::Move)
/// is ported with the movement.
/// </summary>
public abstract class Guide : Monitor
{
    protected Guide(Unit unit)
    {
        Unit = unit;
        Destination = unit.Pos;
        Mode = 0;
    }

    public Unit Unit { get; }

    /// <summary>mDestination.</summary>
    public FVector Destination;

    /// <summary>mMode.</summary>
    public int Mode;

    /// <summary>Unknown3: the steering (CUnit::Move's per-frame call).</summary>
    public virtual void Steer() => Unit.Context.Skip($"{GetType().Name}::Unknown3 (steering, ported with the movement)");

    /// <summary>GoToPoint: an AI following its launcher's emitter (mode 2) ignores it unless overridden.</summary>
    public virtual void GoToPoint(FVector point, bool inOverride)
    {
        if (!inOverride && Unit.AI?.AIMode == 2)
        {
            return;
        }

        Mode = 1;
        Destination = point;
    }

    public virtual void Unknown5(FVector v)
    {
        Mode = 2;
        Destination = v;
    }

    public virtual void Unknown6(FVector v)
    {
        Mode = 3;
        Destination = v;
    }

    public virtual void Unknown7(FVector v)
    {
        if (Unit.AI?.AIMode == 2)
        {
            return;
        }

        Mode = 3;
        if (Unit.CurrentWeaponTurnsTurret())
        {
            Mode = 0;
        }

        Destination = v;
    }

    public virtual void Stop()
    {
        Mode = 0;
        Destination = Unit.Pos;
        Unit.Unknown14C = new FVector(0, 0, 0);
    }
}

/// <summary>The guides whose steering is not yet ported: they keep CGuide's state (CGroundVehicleGuide and the rest).</summary>
public sealed class BasicGuide(Unit unit, string name) : Guide(unit)
{
    public string Name { get; } = name;

    public override void Steer() => Unit.Context.Skip($"{Name}::Unknown3 (steering, ported with the movement)");
}

/// <summary>CAirGuide: a flying unit's guide; it keeps its clearance over the ground and the nearest thing.</summary>
public sealed class AirGuide : Guide
{
    public const int ClearanceEvent = 2000;
    public const int NearestEvent = 2001;

    /// <summary>
    /// mCellX/mCellY start as whatever the heap held (the constructor sets neither); the first clearance update compares
    /// the unit's cell with them, which retail's garbage practically never equals, so they start out of range here.
    /// </summary>
    private int _cellX = int.MinValue;
    private int _cellY = int.MinValue;

    public AirGuide(Unit unit)
        : base(unit)
    {
        Clearance = 0;
        Nearest.SetReader(null);
        unit.Context.Events.AddEvent(ClearanceEvent, this, EventManager.NextFrame);
        unit.Context.Events.AddEvent(NearestEvent, this, EventManager.NextFrame);
    }

    private const float Pi = 3.14159265358979f;
    private const float PiM2 = 6.28318530717958f;

    /// <summary>
    /// Unknown3 (00402280): the flight. Heading for the destination (mode 2: away; mode 0: at right angles to the
    /// course, level), climbing at its elevation; unless launching (AI mode 2) it dodges the nearest thing (up or down a
    /// third of a turn), keeps its clearance (down below 5, level below 15, up above 50) and turns back from the map's
    /// edges (outside a retreat); it banks into the turn by the course's difference from the heading (at most PI/2) and
    /// thrusts along its nose at (max velocity / 20) * 4. A dying unit only thrusts.
    /// </summary>
    public override void Steer()
    {
        Unit unit = Unit;
        if (unit.IsDying)
        {
            unit.Unknown174 = 0;
            Thrust(unit);
            return;
        }

        unit.Context.Skip("CAirGuide::Unknown3's dot product and magnitudes (their sum orders are unchecked)");
        FVector d = Destination - unit.Pos;
        float yaw = -(float)Numerics.X87.Atan2(d.X, d.Y);
        float pitch = (float)Numerics.X87.Atan2(d.Z, (float)Numerics.X87.Sqrt((d.X * d.X) + (d.Y * d.Y)));
        FVector v = unit.Velocity;
        float course = -(float)Numerics.X87.Atan2(v.X, v.Y);
        FMatrix o = unit.Orientation;
        var right = new FVector(o.Row0.X, o.Row1.X, o.Row2.X);
        float heading = Mode switch
        {
            0 => course + (Pi / 2),
            2 => yaw - Pi,
            _ => yaw,
        };
        if (Mode == 0)
        {
            pitch = 0;
        }

        if (heading > Pi)
        {
            heading -= PiM2;
        }

        if (heading < -Pi)
        {
            heading += PiM2;
        }

        if (pitch > Pi)
        {
            pitch -= PiM2;
        }

        if (pitch < -Pi)
        {
            pitch += PiM2;
        }

        if (unit.AI!.AIMode != 2)
        {
            if (Nearest.ToRead is { } nearest)
            {
                pitch = -Pi / 3;
                if (!((nearest.Pos - unit.Pos).Z > 0))
                {
                    pitch = Pi / 3;
                }
            }

            if (Clearance < 5.0f)
            {
                pitch = -Pi / 4;
            }
            else if (Clearance < 15.0f && pitch > 0)
            {
                pitch = 0;
            }
            else if (Clearance > 50.0f)
            {
                pitch = Pi / 4;
            }

            if (unit.DeployState is not (1 or 2))
            {
                if (unit.Pos.X < 10.0f)
                {
                    heading = -Pi / 2;
                }
                else if (unit.Pos.X > 502.0f)
                {
                    heading = Pi / 2;
                }
                else if (unit.Pos.Y < 10.0f)
                {
                    heading = 0;
                }
                else if (unit.Pos.Y > 502.0f)
                {
                    heading = Pi;
                }
            }
        }

        float diff = MathF.Abs(Unit.AngleDifference(course, heading));
        if (diff > Pi / 2)
        {
            diff = Pi / 2;
        }

        float dot = (right.X * d.X) + (right.Y * d.Y) + (right.Z * d.Z);
        float roll = dot < 0 ? diff : -diff;
        unit.Unknown174 = MathF.Abs(roll) > 0.1f ? 1.0f : 0;
        unit.Unknown120 = new EulerAngles(heading, pitch, roll);
        Thrust(unit);
    }

    /// <summary>The thrust along the nose: (max velocity / 20) * 4 times the orientation's Y column.</summary>
    private static void Thrust(Unit unit)
    {
        float speed = unit.GetMaxVelocity() * 0.05f * 4.0f;
        FMatrix ori = unit.Orientation;
        unit.Unknown14C = new FVector(speed * ori.Row0.Y, speed * ori.Row1.Y, speed * ori.Row2.Y);
    }

    /// <summary>mClearance: the unit's height over the ground or sea below it, the least of a 9 x 9 sample.</summary>
    public float Clearance;

    /// <summary>mNearest: the nearest thing within 15.</summary>
    public ActiveReader<Thing> Nearest { get; } = new();

    public override void HandleEvent(Event e)
    {
        switch (e.EventNum)
        {
            case ClearanceEvent:
                UpdateClearance();
                Reschedule(ClearanceEvent, e);
                break;
            case NearestEvent:
                FindNearest();
                Reschedule(NearestEvent, e);
                break;
        }
    }

    private void Reschedule(int num, Event e) =>
        Unit.Context.Events.AddEvent(num, this,
            (Unit.Context.Random.FloatRandom() * 0.5f) + Unit.Context.Events.Time + 0.5f, EventPriority.StartOfFrame, null,
            e as ScheduledEvent);

    /// <summary>FindNearest: the thing within 30 whose gap (centre distance squared less both radii squared) is least,
    /// under 225.</summary>
    public void FindNearest()
    {
        Nearest.SetReader(null);
        MapWho map = Unit.Context.MapWho;
        float best = 999999.0f;
        float radius = Unit.GetRadius();
        float radiusSq = radius * radius;
        for (MapWhoEntry? entry = map.GetFirstEntryWithinRadius(Unit.Pos, 30.0f); entry is not null;
             entry = map.GetNextEntryWithinRadius())
        {
            Thing thing = entry.Thing;
            if (ReferenceEquals(thing, Unit) || thing.IsA(ThingType.Ammunition))
            {
                continue;
            }

            float r = thing.GetRadius();
            FVector d = thing.Pos - Unit.Pos;
            float gap = d.MagnitudeSq() - (r * r) - radiusSq;
            if (gap < best && gap < 225.0f)
            {
                Nearest.SetReader(thing);
                best = gap;
            }
        }
    }

    /// <summary>UpdateClearance: re-measured only when the unit's whole-unit cell changed ((int) is fistp).</summary>
    public void UpdateClearance()
    {
        FVector pos = Unit.Pos;
        if (Numerics.X87.Fist(pos.X) == _cellX && Numerics.X87.Fist(pos.Y) == _cellY)
        {
            return;
        }

        _cellX = Numerics.X87.Fist(pos.X);
        _cellY = Numerics.X87.Fist(pos.Y);
        float z = -pos.Z;
        float sea = -Unit.Context.WaterLevel;
        Clearance = 999999.0f;
        int cx = Numerics.X87.Fist(pos.X);
        int cy = Numerics.X87.Fist(pos.Y);
        World.HeightField map = Unit.Context.Map;
        for (int y = cy - 20; y <= cy + 20; y += 5)
        {
            for (int x = cx - 20; x <= cx + 20; x += 5)
            {
                float ground = -(map.HeightScale * map.GetHeightSample(x, y));
                if (ground < sea)
                {
                    ground = sea;
                }

                float clearance = z - ground;
                if (clearance < Clearance)
                {
                    Clearance = clearance;
                }
            }
        }
    }
}
