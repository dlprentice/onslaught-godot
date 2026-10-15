// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/FearGrid.h and src/FearGrid.cpp (CFearGrid: the constructor 0044c3d0, HandleEvent
// 0044c420, Rebuild 0044c440, InDanger 0044c720, Unknown44c780, Unknown44c810). Not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using Monitor = OnslaughtRebuild.Parity.Events.Monitor;

namespace OnslaughtRebuild.Parity.Game;

/// <summary>What the fear grid reads from a round on the world's round list (CWorld +0x60).</summary>
public interface IFearRound
{
    /// <summary>mAllegiance: the side that fired it.</summary>
    int RoundAllegiance { get; }

    /// <summary>GetExplosionUnknown34 (004daff0).</summary>
    float GetExplosionUnknown34();

    /// <summary>mDest: where it will land.</summary>
    FVector GetTargetPos();
}

/// <summary>
/// CFearGrid: a side's 64 x 64 grid of 8-unit cells. A cell is feared where a round of this allegiance will land
/// (the grid of the side under fire is built from the other side's rounds by the caller's choice of grid), and unsafe
/// around this side's ground units. It rebuilds every second (event 1000). CGame::InitRestartLoop makes one per side.
/// </summary>
public sealed class FearGrid : Monitor
{
    public const int GridSize = 64;
    public const int RebuildEvent = 1000;

    private readonly EventManager _events;

    /// <summary>mFear[x][y].</summary>
    private readonly int[,] _fear = new int[GridSize, GridSize];

    /// <summary>mSafe[x][y].</summary>
    private readonly int[,] _safe = new int[GridSize, GridSize];

    public FearGrid(EventManager events, int allegiance)
    {
        _events = events;
        Allegiance = allegiance;
        Rebuild();
    }

    public int Allegiance { get; }

    /// <summary>The world the rebuilds read; null until the world wiring attaches it (the grid is empty then).</summary>
    public WorldRegistry? World { get; set; }

    /// <summary>The height field Unknown44c780 reads.</summary>
    public HeightField? Map { get; set; }


    public int Fear(int x, int y) => _fear[x, y];

    public int Safe(int x, int y) => _safe[x, y];

    public override void HandleEvent(Event e)
    {
        if (e.EventNum == RebuildEvent)
        {
            Rebuild();
        }
    }

    /// <summary>FEAR_CELL: (SINT)v / 8, the conversion rounding to nearest (/QIfist), the division truncating.</summary>
    private static int Cell(float v) => X87.Fist(v) / 8;

    /// <summary>Rebuild: clear, mark the landing areas and the units' surroundings, and again in a second.</summary>
    public void Rebuild()
    {
        for (int x = 0; x < GridSize; x++)
        {
            for (int y = 0; y < GridSize; y++)
            {
                _fear[y, x] = 0;
                _safe[y, x] = 1;
            }
        }

        if (World is not null)
        {
            for (Thing? t = World.Set60.First(); t is not null; t = World.Set60.Next())
            {
                var round = t as IFearRound
                    ?? throw new InvalidOperationException($"{t} is on the round list but is not a round");
                if (Allegiance == round.RoundAllegiance)
                {
                    int r = X87.Fist(round.GetExplosionUnknown34() * 0.125f);
                    FVector target = round.GetTargetPos();
                    int y0 = Cell(target.Y) - r;
                    int y1 = Cell(target.Y) + r;
                    int x0 = Cell(target.X) - r;
                    int x1 = Cell(target.X) + r;
                    if (y0 > 0 && y1 < GridSize && x0 > 0 && x1 < GridSize)
                    {
                        for (int y = y0; y <= y1; y++)
                        {
                            for (int x = x0; x <= x1; x++)
                            {
                                _fear[x, y] = 1;
                            }
                        }
                    }
                }
            }

            for (Thing? t = World.UnitNB.First(); t is not null; t = World.UnitNB.Next())
            {
                var unit = t as ISquadUnit ?? throw new InvalidOperationException($"{t} is on the unit list but is not a unit");
                if (!t.IsA(ThingType.AirUnit) && Allegiance == unit.GetAllegiance())
                {
                    int y0 = Cell(t.Pos.Y) - 1;
                    int y1 = Cell(t.Pos.Y) + 1;
                    int x0 = Cell(t.Pos.X) - 1;
                    int x1 = Cell(t.Pos.X) + 1;
                    if (y0 > 0 && y1 < GridSize && x0 > 0 && x1 < GridSize)
                    {
                        for (int y = y0; y <= y1; y++)
                        {
                            for (int x = x0; x <= x1; x++)
                            {
                                _safe[x, y] = 0;
                            }
                        }
                    }
                }
            }
        }

        _events.AddEvent(RebuildEvent, this, _events.Time + 1.0f);
    }

    /// <summary>InDanger: whether the cell is feared (outside the grid: no).</summary>
    public bool InDanger(FVector pos)
    {
        int x = Cell(pos.X);
        int y = Cell(pos.Y);
        if (x >= 0 && y >= 0 && x < GridSize && y < GridSize)
        {
            return _fear[x, y] != 0;
        }

        return false;
    }

    /// <summary>Unknown44c780: whether the cell is safe; underwater by more than 5 or outside the grid counts as safe.</summary>
    public bool Unknown44c780(FVector pos)
    {
        HeightField map = Map ?? throw new InvalidOperationException("the fear grid has no height field attached");
        if (pos.Z - map.Collide(pos) > -5.0f)
        {
            int x = Cell(pos.X);
            int y = Cell(pos.Y);
            if (x >= 0 && y >= 0 && x < GridSize && y < GridSize)
            {
                return _safe[x, y] != 0;
            }
        }

        return true;
    }

    /// <summary>
    /// Unknown44c810: moves the position to the nearest cell nothing is feared in, searching square rings outwards
    /// (rows by y, then x); a position already in an unfeared cell, or outside the grid, is left alone.
    /// </summary>
    public void Unknown44c810(ref FVector pos)
    {
        int cx = Cell(pos.X);
        int cy = Cell(pos.Y);
        if (cx < 0 || cy < 0 || cx >= GridSize || cy >= GridSize)
        {
            return;
        }

        for (int r = 0; r < GridSize; r++)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y >= 0 && y < GridSize)
                {
                    for (int x = cx - r; x <= cx + r; x++)
                    {
                        if (x >= 0 && x < GridSize && _fear[x, y] == 0)
                        {
                            if (r > 0)
                            {
                                pos.X = x * 8.0f;
                                pos.Y = y * 8.0f;
                            }

                            return;
                        }
                    }
                }
            }
        }
    }
}
