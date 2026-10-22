// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/GroundVehicleGuide.cpp and GroundVehicleGuide.h (CGroundVehicleGuide: the constructor
// 0047d590, Unknown3 0047d750, GetPath 0048a4b0) and src/maths.h (FVector::Azimuth, Elevation, AngleDifference as
// compiled there). Reconstructed; not in Stuart's release.

using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>
/// CGroundVehicleGuide: a ground vehicle's guide. It plans a path over the unit's occupancy grid and follows the path's
/// cells. When the cell ahead is blocked it turns to the first open neighbour (nearest the heading first), then drives
/// forward (backward in mode 2) once it faces within 0.6 of the heading.
/// </summary>
public sealed class GroundVehicleGuide : Guide
{
    private const float Pi = 3.14159265358979f;

    public GroundVehicleGuide(Unit unit)
        : base(unit)
    {
        Grid = unit.GetOccupancyGrid();
    }

    /// <summary>mGrid: the unit's grid when the guide was made.</summary>
    public IOccupancyGrid? Grid { get; }

    /// <summary>mPath.</summary>
    public SquadPath Path { get; } = new();

    /// <summary>FVector::Azimuth as compiled: fpatan(X, Y), negated, stored as a float.</summary>
    private static float Azimuth(FVector d) => -(float)X87.Atan2(d.X, d.Y);

    /// <summary>FVector::Elevation as compiled here: the out-of-line Magnitude, Z / m, the runtime asin, stored as a float.</summary>
    private static float Elevation(FVector d)
    {
        float m = Geometry.MagnitudeOutOfLine(d);
        return m > 0.0f ? (float)X87.Asin(d.Z / m) : 0.0f;
    }

    /// <summary>
    /// pos + FVector(sin(-yaw), cos(yaw), 0) as compiled: the sine goes into the add straight from the register, the
    /// cosine is stored as a float first.
    /// </summary>
    private static FVector Ahead(FVector pos, float yaw) =>
        new((float)X87.Add(X87.Sin(-yaw), pos.X), (float)X87.Cos(yaw) + pos.Y, pos.Z);

    private bool PathDestinationChanged() =>
        Math.Abs(Path.Unknown00 - X87.Fist(Destination.X)) > 1 || Math.Abs(Path.Unknown04 - X87.Fist(Destination.Y)) > 1;

    /// <summary>Unknown3 (0047d750): the steering, each move.</summary>
    public override void Steer()
    {
        Unit unit = Unit;
        if (unit.IsDying)
        {
            unit.Stop();
            return;
        }

        FVector d = Destination - unit.Pos;
        float distanceSq = (d.X * d.X) + (d.Y * d.Y);
        unit.Unknown14C = new FVector(0, 0, 0);
        if (distanceSq < 0.5f || Mode == 0 || unit.IsDeploying())
        {
            return;
        }

        float yaw = Azimuth(d);
        float current = unit.CurrentOrientation.Yaw;
        if (unit.AI!.AIMode == 2)
        {
            unit.Unknown120.Pitch = Elevation(d);
        }

        if (Mode == 1 && unit.AI.AIMode != 2)
        {
            if (distanceSq > 1.0f && Grid is not null)
            {
                if (Path.Unknown08 == 0 || PathDestinationChanged() || !Grid.UnknownCell(unit.Pos))
                {
                    Grid.Unknown4bc2e0(unit.Pos, Destination, 1, Path);
                }
            }

            if (Path.Unknown08 != 0)
            {
                int x = X87.Fist(unit.Pos.X);
                int y = X87.Fist(unit.Pos.Y);
                Path.GetCurrent(ref x, ref y);
                d = new FVector(x - unit.Pos.X, y - unit.Pos.Y, -unit.Pos.Z);
                while ((d.X * d.X) + (d.Y * d.Y) < 0.5f)
                {
                    if (--Path.Unknown0C <= 0)
                    {
                        d = Destination - unit.Pos;
                        break;
                    }

                    Path.GetCurrent(ref x, ref y);
                    d = new FVector(x - unit.Pos.X, y - unit.Pos.Y, -unit.Pos.Z);
                }

                yaw = Azimuth(d);
                FVector pos = unit.Pos;
                if (Grid!.UnknownCell(pos) && !Grid.UnknownCell(Ahead(pos, yaw)))
                {
                    yaw = OpenNeighbourYaw(pos, yaw);
                }
            }
        }

        unit.Unknown120.Yaw = yaw;
        if (Math.Abs(Unit.AngleDifference(yaw, current)) > 0.6f)
        {
            return;
        }

        if (unit.AI.AIMode != 2)
        {
            FVector pos = unit.Pos;
            if (Grid!.UnknownCell(pos) && !Grid.UnknownCell(Ahead(pos, current)))
            {
                return;
            }
        }

        if (Mode == 3)
        {
            return;
        }

        // (speed / 20) * 4 compiles to two multiplies: by 0.05f, then by 4 (or -4).
        float v = Mode == 1 ? unit.Unknown111() * 0.05f * 4.0f : unit.Unknown111() * 0.05f * -4.0f;
        unit.Unknown14C = unit.GetOrientation().Transform(new FVector(0, v, 0));
    }

    /// <summary>
    /// The cell ahead is blocked: the four neighbours in order of nearness to the heading (by the larger of the
    /// heading's cosine and sine; the float cosine is compared with the sine as fsin leaves it), the first open one
    /// taking the heading; none open keeps it.
    /// </summary>
    private float OpenNeighbourYaw(FVector pos, float yaw)
    {
        double s = X87.Sin(yaw);
        float c = (float)X87.Cos(yaw);
        int[] order = new int[4];
        if (Math.Abs(c) > Math.Abs(s))
        {
            (order[0], order[3]) = c > 0 ? (1, 3) : (3, 1);
            (order[1], order[2]) = s > 0 ? (2, 4) : (4, 2);
        }
        else
        {
            (order[0], order[3]) = s > 0 ? (2, 4) : (4, 2);
            (order[1], order[2]) = c > 0 ? (1, 3) : (3, 1);
        }

        for (int i = 0; i < 4; i++)
        {
            (float a, FVector dir) = order[i] switch
            {
                1 => (0.0f, new FVector(0, 1.0f, 0)),
                3 => (Pi, new FVector(0, -1.0f, 0)),
                2 => (Pi / 2, new FVector(-1.0f, 0, 0)),
                _ => (-Pi / 2, new FVector(1.0f, 0, 0)),
            };
            if (Grid!.UnknownCell(pos + dir))
            {
                return a;
            }
        }

        return yaw;
    }
}
