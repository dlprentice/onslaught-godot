// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Occupancy.h and src/NavigationMap.cpp (COccupancy: RemoveUnit 004bc3e0, AddUnit 004bc480,
// ClearSteep 004bc8d0, ApplyStatic 004bcbf0, BuildStatic 004bcd60, Rasterize 004bd5c0, BuildStaticShadows 004bd9e0)
// and src/maths.h (FVector::Elevation, through VC6's runtime asin). Not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.World;

/// <summary>What the occupancy builders ask of a unit on the grids (CUnit and its collision-seeking part).</summary>
public interface IOccupancyUnit
{
    /// <summary>GetCentrePos.</summary>
    FVector GetCentrePos();

    /// <summary>GetCST()-&gt;GetCollisionMeshVolume() != NULL.</summary>
    bool HasCollisionMeshVolume();

    /// <summary>
    /// The builders' collision query (ShadowHits): with the unit's move at its position, whether its collision mesh
    /// volume meets a line from (0, 0, -100) to the origin moved to <paramref name="cellPos"/>, or failing that a unit
    /// sphere there.
    /// </summary>
    bool ShadowHits(FVector cellPos);
}

/// <summary>
/// COccupancy: the grids' static operations. Units on the grids close the cells their collision meshes stand on;
/// cells under water close on every grid and too-steep ones on each grid that cannot climb them.
/// </summary>
public static class Occupancy
{
    /// <summary>PI as maths.h has it.</summary>
    private const float Pi = 3.14159265358979f;

    private static OccupancyGrid Grid(OccupancyState s, int i) =>
        s.Grids[i] ?? throw new InvalidOperationException("the occupancy grids are not made (CWorld::InitOccupancy)");

    /// <summary>RemoveUnit: off the list; once the grids are built, its box (one cell wider) is rasterised again.</summary>
    public static void RemoveUnit(OccupancyState s, HeightField map, Thing unit)
    {
        s.Units.Remove(unit);
        if (s.Enabled && s.Grids[0] is not null)
        {
            float r = unit.GetRadius();
            Rasterize(s, map, X87.Fist(unit.Pos.X - r - 1.0f), X87.Fist(unit.Pos.Y - r - 1.0f),
                X87.Fist(r + unit.Pos.X + 1.0f), X87.Fist(r + unit.Pos.Y + 1.0f), true);
        }
    }

    /// <summary>AddUnit: at the head of the list; once the grids are built, its box is rasterised and its shadow laid.</summary>
    public static void AddUnit(OccupancyState s, HeightField map, Thing unit)
    {
        s.Units.Add(unit);
        if (s.Enabled && s.Grids[0] is not null)
        {
            float r = unit.GetRadius();
            Rasterize(s, map, X87.Fist(unit.Pos.X - r), X87.Fist(unit.Pos.Y - r), X87.Fist(r + unit.Pos.X),
                X87.Fist(r + unit.Pos.Y), true);
            BuildStaticShadows(s, map, unit);
        }
    }

    /// <summary>CMap::GetHeight: the sample times the height scale.</summary>
    private static float Height(HeightField map, int x, int y) => map.HeightScale * map.GetHeightSample(x, y);

    /// <summary>
    /// The cell's slope, n.Elevation() + PI / 2, as ClearSteep (004bca4f..004bca70) and Rasterize (004bd8a5..004bd8c6)
    /// compile it: the out-of-line Magnitude (004026b0), Z / m rounded to 24 bits, VC6's runtime asin on it (a double,
    /// <see cref="X87.Asin"/>) and pi/2 (a float constant) added in the register, so the result rounds to 24 bits
    /// once. A zero normal gives pi/2.
    /// </summary>
    private static float Slope(FVector n)
    {
        float m = Collision.Geometry.MagnitudeOutOfLine(n);
        return m > 0.0f ? (float)X87.Add(X87.Asin(n.Z / m), Pi / 2) : Pi / 2;
    }

    /// <summary>The cell test ClearSteep and Rasterize share: under water closes on all grids, too steep on each.</summary>
    private static void CloseIfWetOrSteep(OccupancyState s, HeightField map, float water, int x, int y)
    {
        var pos = new FVector(x + 0.5f, y + 0.5f, 0);
        if (Height(map, X87.Fist(pos.X), X87.Fist(pos.Y)) > water)
        {
            Grid(s, 0).ClearCross(x, y);
            Grid(s, 1).ClearCross(x, y);
            Grid(s, 2).ClearCross(x, y);
        }
        else
        {
            float slope = Slope(map.Normal(pos));
            if (slope > Grid(s, 0).MaxSlope)
            {
                Grid(s, 0).ClearCross(x, y);
            }

            if (slope > Grid(s, 1).MaxSlope)
            {
                Grid(s, 1).ClearCross(x, y);
            }

            if (slope > Grid(s, 2).MaxSlope)
            {
                Grid(s, 2).ClearCross(x, y);
            }
        }
    }

    /// <summary>ClearSteep (worlds before version 47): every cell of the map through the wet-or-steep test.</summary>
    public static void ClearSteep(OccupancyState s, HeightField map)
    {
        float water = map.WaterLevel;
        for (int y = 0; y < 511; y++)
        {
            for (int x = 0; x < 511; x++)
            {
                CloseIfWetOrSteep(s, map, water, x, y);
            }
        }
    }

    /// <summary>ApplyStatic: the static mask's closed cells close on every grid; the grids are then in use.</summary>
    public static void ApplyStatic(OccupancyState s)
    {
        for (int j = 0, y = 0; y < 512; j++, y += 2)
        {
            for (int i = 0, x = 0; x < 512; i++, x += 2)
            {
                if (s.Static.TestCell(i, j) == 0)
                {
                    Grid(s, 0).SetCell(x, y, false);
                    Grid(s, 1).SetCell(x, y, false);
                    Grid(s, 2).SetCell(x, y, false);
                }
            }
        }

        s.Enabled = true;
    }

    /// <summary>
    /// BuildStatic: the static mask opens again; then every cell across each unit's bounding radius closes on the mask and
    /// on every grid when the unit has no collision mesh. A meshed unit's cells were meant to close where its mesh reaches
    /// the ground, but retail's compiled cell position is NaN there, so its mesh tests run and never hit.
    /// </summary>
    public static void BuildStatic(OccupancyState s, HeightField map)
    {
        float IndefiniteNaN = BitConverter.Int32BitsToSingle(unchecked((int)0xffc00000));
        Array.Fill(s.Static.Data, (byte)0xff);
        for (Thing? unit = s.Units.First(); unit is not null; unit = s.Units.Next())
        {
            var u = (IOccupancyUnit)unit;
            float radius = unit.GetBoundingRadius();
            FVector centre = u.GetCentrePos();
            for (int y = X87.Fist(Math.Floor(centre.Y - radius)); y < X87.Fist(Math.Ceiling(centre.Y + radius)); y++)
            {
                for (int x = X87.Fist(Math.Floor(centre.X - radius)); x < X87.Fist(Math.Ceiling(centre.X + radius)); x++)
                {
                    var pos = new FVector(x + 0.5f, y + 0.5f, 0);
                    pos.Z = Height(map, X87.Fist(pos.X), X87.Fist(pos.Y));

                    // As compiled (/QIfist, 004bce80..004bd003): the cell centre's fadd 0.5 is consumed by a popping
                    // fistp, then stored from the emptied register, so the line move's X and Y are the x87 indefinite
                    // NaN (ffc00000) and only the height is real. The mesh tests then never hit (StartLineSearch exits at
                    // its first check), so retail clears only a meshless unit's cells (B, 521-rr runs).
                    pos.X = IndefiniteNaN;
                    pos.Y = IndefiniteNaN;
                    if (!u.HasCollisionMeshVolume() || u.ShadowHits(pos))
                    {
                        s.Static.ClearCross(x, y);
                        Grid(s, 0).ClearCross(x, y);
                        Grid(s, 1).ClearCross(x, y);
                        Grid(s, 2).ClearCross(x, y);
                    }
                }
            }
        }

        s.Enabled = true;
    }

    /// <summary>
    /// Rasterize: a box (world coordinates, clamped to the map) opens on every grid, then its wet or steep cells close
    /// again; unless told not to, every unit's shadow is laid back down. The grids are then in use.
    /// </summary>
    public static void Rasterize(OccupancyState s, HeightField map, int x0, int y0, int x1, int y1, bool a)
    {
        if (x0 < 0)
        {
            x0 = 0;
        }

        if (y0 < 0)
        {
            y0 = 0;
        }

        if (x1 > 511)
        {
            x1 = 511;
        }

        if (y1 > 511)
        {
            y1 = 511;
        }

        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                Grid(s, 0).SetCell(x, y, true);
                Grid(s, 1).SetCell(x, y, true);
                Grid(s, 2).SetCell(x, y, true);
            }
        }

        float water = map.WaterLevel;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                CloseIfWetOrSteep(s, map, water, x, y);
            }
        }

        if (!a)
        {
            for (Thing? unit = s.Units.First(); unit is not null; unit = s.Units.Next())
            {
                BuildStaticShadows(s, map, unit);
            }
        }

        s.Enabled = true;
    }

    /// <summary>
    /// BuildStaticShadows: across a unit's bounding radius, every other cell whose ground (MAP.Collide at the cell's far
    /// corner) its collision mesh reaches closes on every grid. Retail queries the mesh volume without testing for one.
    /// </summary>
    public static void BuildStaticShadows(OccupancyState s, HeightField map, Thing unit)
    {
        var u = (IOccupancyUnit)unit;
        float radius = unit.GetBoundingRadius();
        FVector centre = u.GetCentrePos();
        for (int y = X87.Fist(Math.Floor(centre.Y - radius)); y < X87.Fist(Math.Ceiling(centre.Y + radius)); y += 2)
        {
            for (int x = X87.Fist(Math.Floor(centre.X - radius)); x < X87.Fist(Math.Ceiling(centre.X + radius)); x += 2)
            {
                var pos = new FVector(x + 1.0f, y + 1.0f, 0);
                pos.Z = map.Collide(pos);
                if (u.ShadowHits(pos))
                {
                    Grid(s, 0).ClearCross(x, y);
                    Grid(s, 1).ClearCross(x, y);
                    Grid(s, 2).ClearCross(x, y);
                }
            }
        }
    }
}
