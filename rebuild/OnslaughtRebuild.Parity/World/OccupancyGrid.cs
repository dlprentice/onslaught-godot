// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/world.h (COccupancyGrid's layout, UnknownCell, TestCell, ClearCross, the CanStep*
// helpers and the search statics) and src/NavigationMap.cpp (COccupancyGrid: the constructor 004bc260, the destructor
// 004bc2d0, Unknown4bc2e0, IsBlocked 004bc510, FindNearest 004bc6d0, SetCell 004bdf70, SkipLegacy 004bdff0, Load
// 004be050, SkipHeader 004be170, BuildPath 004be1d0, Flood 004be420, FindNearestVisited 004beb30, StepDown 004bed30;
// CNormalSquadUnknownD4::Simplify 004beea0). Not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Data;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.World;

/// <summary>
/// The occupancy grids' class statics (retail globals): the search's cost table and cursor, the units on the grids,
/// the static mask STATIC_OCCUPANCY and the world's three grids (CWorld::mOccupancy). One instance per simulation, so
/// separate simulations never share it; within one, every search starts from what the previous one left, as retail's
/// does.
/// </summary>
public sealed class OccupancyState
{
    public const int Size = 256;
    public const int MaxMoves = 500;

    /// <summary>
    /// A state as the program's start leaves it: the statics' constant initialisers, then STATIC_OCCUPANCY's constructor
    /// (the cost table cleared, the bounds emptied, the search off).
    /// </summary>
    public OccupancyState(SPtrSetPool pool)
    {
        Units = new SPtrSet<Thing>(pool);
        EndX = 0;
        EndY = 0;
        Grid = null;
        X = 0;
        Y = 0;
        MinX = 0;
        MinY = 0;
        MaxX = 255;
        MaxY = 255;
        Static = new OccupancyGrid(this, 0.0f);
    }

    /// <summary>sCost[x][y] as one array: 0xffff unvisited, else the depth Flood reached the cell at.</summary>
    public ushort[] Cost { get; } = new ushort[Size * Size];

    /// <summary>sMoves: the step Flood took from each depth.</summary>
    public int[] Moves { get; } = new int[MaxMoves];

    /// <summary>sUnits: the units on the grids (AddUnit puts each at the head).</summary>
    public SPtrSet<Thing> Units { get; }

    /// <summary>sEnabled: the grids are built and the units' changes rasterise into them.</summary>
    public bool Enabled;

    /// <summary>
    /// A diagnostic hook: when set, each path search (Unknown4bc2e0) reports its grid, ends, flag, the statics on entry
    /// (MinX, MinY, MaxX, MaxY, EndX, EndY, X, Y) and the path it built.
    /// </summary>
    public Action<OccupancyGrid, FVector, FVector, int, int[], SquadPath>? OnSearch;

    public int EndX;
    public int EndY;

    /// <summary>sGrid: the grid of the current search.</summary>
    public OccupancyGrid? Grid;

    /// <summary>sX, sY: the search's cell.</summary>
    public int X;
    public int Y;

    /// <summary>sMinX/sMinY/sMaxX/sMaxY: the touched costs' bounds (sMinX and sMaxX track y, the others x).</summary>
    public int MinX;
    public int MinY;
    public int MaxX;
    public int MaxY;

    /// <summary>STATIC_OCCUPANCY: the mask of the cells the static units close.</summary>
    public OccupancyGrid Static { get; }

    /// <summary>CWorld::mOccupancy: the grids for slopes up to 35, 45 and 60 degrees (null until InitOccupancy).</summary>
    public OccupancyGrid?[] Grids { get; } = new OccupancyGrid?[3];

    /// <summary>CWorld::InitOccupancy: the three grids, each constructor resetting the search.</summary>
    public void InitOccupancy()
    {
        Grids[0] = new OccupancyGrid(this, 35.0f);
        Grids[1] = new OccupancyGrid(this, 45.0f);
        Grids[2] = new OccupancyGrid(this, 60.0f);
    }

    public ushort CostAt(int x, int y) => Cost[(x * Size) + y];

    public void SetCost(int x, int y, int value) => Cost[(x * Size) + y] = unchecked((ushort)value);

    /// <summary>
    /// sCost[x][y] for a row past the table, as FindNearestVisited's retail bug reads it. Retail's .bss order (the
    /// addresses bea-decomp's world.h records for the matched build, specimen 74154bfa…): sCost[256][256] at 00809dc0
    /// (0x20000 bytes, so it ends at 00829dc0), then sX at 00829dc0, sMinX at 00829dc4, sMinY at 00829dc8. The words
    /// after those are not modelled; a read there throws. In the pristine exe the ring tests compare
    /// [edx*2+0x809dc0] (004bec45, 004bec6d) and Flood stores sX at 0x829dc0.
    /// </summary>
    public ushort CostPastEnd(int x, int y)
    {
        if (x < Size)
        {
            return CostAt(x, y);
        }

        int offset = (((x * Size) + y) * 2) - (Size * Size * 2);
        int word = offset >> 2;
        int value = word switch
        {
            0 => X,
            1 => MinX,
            2 => MinY,
            _ => throw new NotSupportedException(
                $"FindNearestVisited reads sCost[{x}][{y}], {offset} bytes past the table, beyond the modelled statics"),
        };
        return (ushort)((offset & 2) == 0 ? value & 0xffff : (value >> 16) & 0xffff);
    }
}

/// <summary>
/// COccupancyGrid: 256 x 256 bits of two-unit cells, a set bit open; bit x &amp; 7 of byte (x &gt;&gt; 3) * 256 + y. The
/// path search (Unknown4bc2e0) floods a cost table from the start and walks down it from the end.
/// </summary>
public sealed class OccupancyGrid : IOccupancyGrid
{
    private readonly OccupancyState _state;

    /// <summary>
    /// The constructor: the search is off and its cost table cleared, the bounds emptied, and every cell open. The
    /// slope arrives in degrees and is kept in radians (max_slope * (PI / 180.0f); whether VC6 folded PI/180 in float or
    /// double is for D's oracle).
    /// </summary>
    public OccupancyGrid(OccupancyState state, float maxSlope)
    {
        _state = state;
        state.Enabled = false;
        MaxSlope = maxSlope * (3.14159265358979f / 180.0f);
        Array.Fill(state.Cost, (ushort)0xffff);
        state.MinX = 0xff;
        state.MinY = 0xff;
        state.MaxX = 0;
        state.MaxY = 0;
        Array.Fill(Data, (byte)0xff);
    }

    /// <summary>mData.</summary>
    public byte[] Data { get; } = new byte[0x2000];

    /// <summary>mMaxSlope: radians.</summary>
    public float MaxSlope { get; }

    /// <summary>The destructor: the unit list empties.</summary>
    public void Destroy() => _state.Units.RemoveAll();

    /// <summary>UnknownCell: whether the cell under a world position is open ((SINT) rounds: /QIfist).</summary>
    public bool UnknownCell(FVector pos)
    {
        int x = X87.Fist(pos.X) >> 1;
        int y = X87.Fist(pos.Y) >> 1;
        return (Data[((x >> 3) * 256) + y] & (1 << (x % 8))) != 0;
    }

    /// <summary>TestCell (grid coordinates).</summary>
    public int TestCell(int x, int y) => (1 << (x % 8)) & Data[((x >> 3) * 256) + y];

    /// <summary>SetCell (world coordinates): outside the grid nothing changes.</summary>
    public void SetCell(int x, int y, bool set)
    {
        x >>= 1;
        y >>= 1;
        if (x < 0 || x >= 256 || y < 0 || y >= 256)
        {
            return;
        }

        int i = ((x >> 3) * 256) + y;
        if (set)
        {
            Data[i] |= (byte)(1 << (x % 8));
        }
        else
        {
            Data[i] &= (byte)(0xff - (1 << (x % 8)));
        }
    }

    /// <summary>ClearCross (world coordinates): the cell and its four neighbours close.</summary>
    public void ClearCross(int x, int y)
    {
        SetCell(x - 1, y, false);
        SetCell(x, y - 1, false);
        SetCell(x, y, false);
        SetCell(x + 1, y, false);
        SetCell(x, y + 1, false);
    }

    /// <summary>SkipLegacy: a stored grid read past (version 1: 0x8000 bytes, 2: 0x2000).</summary>
    public static void SkipLegacy(MemBuffer file)
    {
        int version = file.ReadInt32();
        switch (version)
        {
            case 1:
                file.Skip(0x8000);
                break;
            case 2:
                file.Skip(0x2000);
                break;
        }
    }

    /// <summary>Load: version 1 stores world-coordinate rows of bits (two per cell, the later winning); version 2 the bytes.</summary>
    public void Load(MemBuffer file)
    {
        int version = file.ReadInt32();
        switch (version)
        {
            case 1:
                for (int y = 0; y < 512; y++)
                {
                    int gridY = y >> 1;
                    for (int x = 0; x < 512; x += 8)
                    {
                        byte b = file.ReadByte();
                        for (int bit = 0; bit < 8; bit++)
                        {
                            bool set = ((b >> bit) & 1) != 0;
                            int cellX = (x + bit) >> 1;
                            if (cellX >= 0 && cellX < 256 && gridY >= 0 && gridY < 256)
                            {
                                int i = ((cellX >> 3) * 256) + gridY;
                                if (set)
                                {
                                    Data[i] |= (byte)(1 << (cellX % 8));
                                }
                                else
                                {
                                    Data[i] &= (byte)(0xff - (1 << (cellX % 8)));
                                }
                            }
                        }
                    }
                }

                break;
            case 2:
                for (int i = 0; i < 256; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        Data[(j * 256) + i] = file.ReadByte();
                    }
                }

                break;
        }
    }

    /// <summary>SkipHeader: five dwords (worlds before version 50).</summary>
    public static void SkipHeader(MemBuffer file)
    {
        for (int i = 0; i < 5; i++)
        {
            file.ReadInt32();
        }
    }

    /// <summary>
    /// Unknown4bc2e0: the part of the cost table the last search touched (its bounds widened by one and clamped) is
    /// cleared, the bounds emptied, and the path built.
    /// </summary>
    public void Unknown4bc2e0(FVector start, FVector end, int a, SquadPath path)
    {
        OccupancyState s = _state;
        if (s.OnSearch is null)
        {
            Search(start, end, a, path);
            return;
        }

        int[] entry = [s.MinX, s.MinY, s.MaxX, s.MaxY, s.EndX, s.EndY, s.X, s.Y];
        Search(start, end, a, path);
        s.OnSearch(this, start, end, a, entry, path);
    }

    private void Search(FVector start, FVector end, int a, SquadPath path)
    {
        OccupancyState s = _state;
        s.MinX--;
        s.MinY--;
        s.MaxX++;
        s.MaxY++;
        if (s.MinX < 0)
        {
            s.MinX = 0;
        }

        if (s.MinY < 0)
        {
            s.MinY = 0;
        }

        if (s.MaxX > 256)
        {
            s.MaxX = 256;
        }

        if (s.MaxY > 256)
        {
            s.MaxY = 256;
        }

        for (int r = s.MinY; r < s.MaxY; r++)
        {
            for (int c = s.MinX; c < s.MaxX; c++)
            {
                s.SetCost(r, c, 0xffff);
            }
        }

        s.MinX = 0xff;
        s.MinY = 0xff;
        s.MaxX = 0;
        s.MaxY = 0;
        BuildPath(s, start, end, this, a, path);
    }

    /// <summary>
    /// IsBlocked (grid coordinates): whether a closed cell lies on the line, or an end is off the grid. Each axis is
    /// ordered on its own, so a falling line is walked as a rising one; the minor coordinate steps in float and rounds
    /// to its cell (/QIfist).
    /// </summary>
    public bool IsBlocked(int x0, int y0, int x1, int y1)
    {
        if (x0 < 0 || x0 > 255 || y0 < 0 || y0 > 255 || x1 < 0 || x1 > 255 || y1 < 0 || y1 > 255)
        {
            return true;
        }

        if (x0 > x1)
        {
            (x0, x1) = (x1, x0);
        }

        if (y0 > y1)
        {
            (y0, y1) = (y1, y0);
        }

        int dx = x1 - x0;
        int dy = y1 - y0;
        if (dx != 0 || dy != 0)
        {
            float step = 0;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                float fy = y0;
                if (dx != 0)
                {
                    step = (float)dy / dx;
                }

                for (int x = x0; x < x1 + 1; x++)
                {
                    int y = X87.Fist(fy);
                    if ((Data[((x >> 3) * 256) + y] & (1 << (x % 8))) == 0)
                    {
                        return true;
                    }

                    if (x == x1 && y == y1)
                    {
                        break;
                    }

                    fy += step;
                }
            }
            else
            {
                float fx = x0;
                if (dy != 0)
                {
                    step = (float)dx / dy;
                }

                for (int y = y0; y < y1 + 1; y++)
                {
                    int x = X87.Fist(fx);
                    if (x == x1 && y == y1)
                    {
                        break;
                    }

                    if ((Data[((x >> 3) * 256) + y] & (1 << (x % 8))) == 0)
                    {
                        return true;
                    }

                    fx += step;
                }
            }
        }

        return false;
    }

    /// <summary>FindNearest: a blocked cell moves to the nearest open one, square rings, top and bottom rows first.</summary>
    public bool FindNearest(ref int px, ref int py)
    {
        for (int r = 0; r < 256; r++)
        {
            for (int x = px - r; x <= px + r; x++)
            {
                if (x >= 0 && x < 256)
                {
                    if (py - r >= 0 && py - r < 256 && TestCell(x, py - r) != 0)
                    {
                        px = x;
                        py -= r;
                        return true;
                    }

                    if (py + r >= 0 && py + r < 256 && TestCell(x, py + r) != 0)
                    {
                        px = x;
                        py += r;
                        return true;
                    }
                }
            }

            for (int y = py - r; y <= py + r; y++)
            {
                if (y >= 0 && y < 256)
                {
                    if (px - r >= 0 && px - r < 256 && TestCell(px - r, y) != 0)
                    {
                        px -= r;
                        py = y;
                        return true;
                    }

                    if (px + r >= 0 && px + r < 256 && TestCell(px + r, y) != 0)
                    {
                        px += r;
                        py = y;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// BuildPath: straight to the end cell when nothing blocks the line; else both ends move to open cells, the flood
    /// runs from the start (its end moved to the nearest reached cell when it fails), and the walk down the costs from
    /// the end makes the path, simplified by line of sight.
    /// </summary>
    public static void BuildPath(OccupancyState s, FVector start, FVector end, OccupancyGrid grid, int a, SquadPath path)
    {
        int sx = X87.Fist(start.X) >> 1;
        int sy = X87.Fist(start.Y) >> 1;
        s.Grid = grid;
        int ex = X87.Fist(end.X);
        s.EndX = ex >> 1;
        int ey = X87.Fist(end.Y);
        s.EndY = ey >> 1;
        path.Unknown00 = ex;
        path.Unknown04 = ey;
        path.Unknown08 = 0;
        path.Unknown0C = 0;

        if (!grid.IsBlocked(sx, sy, s.EndX, s.EndY))
        {
            path.AddCell((s.EndX * 2) + 1, (s.EndY * 2) + 1);
            path.Unknown08 = 1;
            return;
        }

        if (!grid.FindNearest(ref s.EndX, ref s.EndY))
        {
            return;
        }

        if (!grid.FindNearest(ref sx, ref sy))
        {
            return;
        }

        s.X = sx;
        s.Y = sy;
        s.MinY = sx;
        s.MinX = sy;
        s.MaxX = sy + 1;
        s.MaxY = sx + 1;
        if (!Flood(s))
        {
            FindNearestVisited(s);
        }

        int x = s.EndX;
        int y = s.EndY;
        int px;
        int py;
        do
        {
            px = x;
            py = y;
            path.AddCell((x * 2) + 1, (y * 2) + 1);
            StepDown(s, ref x, ref y);
            if (x == px && y == py)
            {
                return;
            }
        }
        while (x != sx || y != sy);

        Simplify(path, s.Grid!);
        path.Unknown08 = 1;
    }

    private static bool CanStepNorth(OccupancyState s) =>
        s.X > 0 && s.CostAt(s.X - 1, s.Y) == 0xffff && s.Grid!.TestCell(s.X - 1, s.Y) != 0;

    private static bool CanStepWest(OccupancyState s) =>
        s.Y > 0 && s.CostAt(s.X, s.Y - 1) == 0xffff && s.Grid!.TestCell(s.X, s.Y - 1) != 0;

    private static bool CanStepSouth(OccupancyState s) =>
        s.X < 255 && s.CostAt(s.X + 1, s.Y) == 0xffff && s.Grid!.TestCell(s.X + 1, s.Y) != 0;

    private static bool CanStepEast(OccupancyState s) =>
        s.Y < 255 && s.CostAt(s.X, s.Y + 1) == 0xffff && s.Grid!.TestCell(s.X, s.Y + 1) != 0;

    /// <summary>
    /// The step from (sX, sY) toward the end (1 west, 2 north, 3 east, 4 south; 0 none): along the longer axis first,
    /// then the other axis toward the end, then away from it, then back along the longer axis.
    /// </summary>
    private static int ChooseStep(OccupancyState s)
    {
        if (Math.Abs(s.X - s.EndX) > Math.Abs(s.Y - s.EndY))
        {
            if (s.X > s.EndX)
            {
                if (CanStepNorth(s))
                {
                    return 2;
                }

                if (s.Y > s.EndY)
                {
                    if (CanStepWest(s))
                    {
                        return 1;
                    }

                    if (CanStepEast(s))
                    {
                        return 3;
                    }
                }
                else
                {
                    if (CanStepEast(s))
                    {
                        return 3;
                    }

                    if (CanStepWest(s))
                    {
                        return 1;
                    }
                }

                if (CanStepSouth(s))
                {
                    return 4;
                }
            }
            else
            {
                if (CanStepSouth(s))
                {
                    return 4;
                }

                if (s.Y > s.EndY)
                {
                    if (CanStepWest(s))
                    {
                        return 1;
                    }

                    if (CanStepEast(s))
                    {
                        return 3;
                    }
                }
                else
                {
                    if (CanStepEast(s))
                    {
                        return 3;
                    }

                    if (CanStepWest(s))
                    {
                        return 1;
                    }
                }

                if (CanStepNorth(s))
                {
                    return 2;
                }
            }
        }
        else if (s.Y > s.EndY)
        {
            if (CanStepWest(s))
            {
                return 1;
            }

            if (s.X > s.EndX)
            {
                if (CanStepNorth(s))
                {
                    return 2;
                }

                if (CanStepSouth(s))
                {
                    return 4;
                }
            }
            else
            {
                if (CanStepSouth(s))
                {
                    return 4;
                }

                if (CanStepNorth(s))
                {
                    return 2;
                }
            }

            if (CanStepEast(s))
            {
                return 3;
            }
        }
        else
        {
            if (CanStepEast(s))
            {
                return 3;
            }

            if (s.X > s.EndX)
            {
                if (CanStepNorth(s))
                {
                    return 2;
                }

                if (CanStepSouth(s))
                {
                    return 4;
                }
            }
            else
            {
                if (CanStepSouth(s))
                {
                    return 4;
                }

                if (CanStepNorth(s))
                {
                    return 2;
                }
            }

            if (CanStepWest(s))
            {
                return 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// Flood: a depth-first walk from (sX, sY) toward the end, numbering each cell it enters with its depth and backing
    /// up from dead ends; it gives up after 500 cells entered.
    /// </summary>
    public static bool Flood(OccupancyState s)
    {
        int depth = 0;
        int steps = 0;
        for (;;)
        {
            steps++;
            depth++;
            s.SetCost(s.X, s.Y, depth);
            if (s.X == s.EndX && s.Y == s.EndY)
            {
                return true;
            }

            for (;;)
            {
                if (steps >= OccupancyState.MaxMoves)
                {
                    return false;
                }

                int step = ChooseStep(s);
                if (step == 1)
                {
                    s.Y--;
                    if (s.Y < s.MinX)
                    {
                        s.MinX = s.Y;
                    }

                    s.Moves[depth] = 1;
                    break;
                }

                if (step == 2)
                {
                    s.X--;
                    if (s.X < s.MinY)
                    {
                        s.MinY = s.X;
                    }

                    s.Moves[depth] = 2;
                    break;
                }

                if (step == 3)
                {
                    s.Y++;
                    if (s.Y + 1 > s.MaxX)
                    {
                        s.MaxX = s.Y + 1;
                    }

                    s.Moves[depth] = 3;
                    break;
                }

                if (step == 4)
                {
                    s.X++;
                    if (s.X + 1 > s.MaxY)
                    {
                        s.MaxY = s.X + 1;
                    }

                    s.Moves[depth] = 4;
                    break;
                }

                // A dead end: back along the move that led here, and choose again.
                if (depth <= 1)
                {
                    return false;
                }

                depth--;
                switch (s.Moves[depth])
                {
                    case 1:
                        s.Y++;
                        continue;
                    case 3:
                        s.Y--;
                        continue;
                    case 2:
                        s.X++;
                        continue;
                    case 4:
                        s.X--;
                        continue;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// FindNearestVisited: the end moves to the nearest cell the flood reached, square rings, top and bottom rows first.
    /// Retail's east test indexes sCost[sEndX + x][y] with the finished row loop's x (004bec65), not the radius; its
    /// bounds check and the end it moves to use the radius.
    /// </summary>
    public static void FindNearestVisited(OccupancyState s)
    {
        for (int r = 0; r < 256; r++)
        {
            int x;
            for (x = s.EndX - r; x <= s.EndX + r; x++)
            {
                if (x >= 0 && x < 256)
                {
                    if (s.EndY - r >= 0 && s.EndY - r < 256 && s.CostAt(x, s.EndY - r) != 0xffff)
                    {
                        s.EndX = x;
                        s.EndY -= r;
                        return;
                    }

                    if (s.EndY + r >= 0 && s.EndY + r < 256 && s.CostAt(x, s.EndY + r) != 0xffff)
                    {
                        s.EndX = x;
                        s.EndY += r;
                        return;
                    }
                }
            }

            for (int y = s.EndY - r; y <= s.EndY + r; y++)
            {
                if (y >= 0 && y < 256)
                {
                    if (s.EndX - r >= 0 && s.EndX - r < 256 && s.CostAt(s.EndX - r, y) != 0xffff)
                    {
                        s.EndX -= r;
                        s.EndY = y;
                        return;
                    }

                    if (s.EndX + r >= 0 && s.EndX + r < 256 && s.CostPastEnd(s.EndX + x, y) != 0xffff)
                    {
                        s.EndX += r;
                        s.EndY = y;
                        return;
                    }
                }
            }
        }
    }

    /// <summary>StepDown: to the cheapest of the eight neighbours (the four sides first, a tie keeping the earlier).</summary>
    public static void StepDown(OccupancyState s, ref int px, ref int py)
    {
        int x = px;
        int y = py;
        int best = s.CostAt(x, y);
        int bx = x;
        int by = y;
        void Try(bool ok, int cx, int cy, bool setY)
        {
            if (ok && s.CostAt(cx, cy) < best)
            {
                best = s.CostAt(cx, cy);
                bx = cx;
                if (setY)
                {
                    by = cy;
                }
            }
        }

        Try(x > 0, x - 1, y, false);
        Try(x < 255, x + 1, y, true);
        Try(y > 0, x, y - 1, true);
        Try(y < 255, x, y + 1, true);
        Try(x > 0 && y > 0, x - 1, y - 1, true);
        Try(x < 255 && y > 0, x + 1, y - 1, true);
        Try(x > 0 && y < 255, x - 1, y + 1, true);
        Try(x < 255 && y < 255, x + 1, y + 1, true);
        px = bx;
        py = by;
    }

    /// <summary>
    /// CNormalSquadUnknownD4::Simplify: from the end, the cells between the last cell and the furthest one it can see
    /// drop out (only the last cell is copied down); then the same from the start.
    /// </summary>
    public static void Simplify(SquadPath path, OccupancyGrid grid)
    {
        int i;
        int remove;
        for (i = path.Unknown0C - 1; i >= 0; i--)
        {
            if (grid.IsBlocked(path.Unknown10[path.Unknown0C - 1], path.Unknown18[path.Unknown0C - 1],
                path.Unknown10[i], path.Unknown18[i]))
            {
                break;
            }
        }

        i += 2;
        int last = path.Unknown0C - 1;
        if (i < last)
        {
            remove = last - i - 1;
            for (int j = last; j < path.Unknown0C; j++)
            {
                path.Unknown10[j - remove] = path.Unknown10[j];
                path.Unknown18[j - remove] = path.Unknown18[j];
            }

            path.Unknown0C -= remove;
        }

        for (i = 0; i < path.Unknown0C; i++)
        {
            if (grid.IsBlocked(path.Unknown10[0], path.Unknown18[0], path.Unknown10[i], path.Unknown18[i]))
            {
                break;
            }
        }

        int first = i - 2;
        if (first > 0)
        {
            remove = first - 1;
            for (int j = first; j < path.Unknown0C; j++)
            {
                path.Unknown10[j - remove] = path.Unknown10[j];
                path.Unknown18[j - remove] = path.Unknown18[j];
            }

            path.Unknown0C -= remove;
        }
    }
}
