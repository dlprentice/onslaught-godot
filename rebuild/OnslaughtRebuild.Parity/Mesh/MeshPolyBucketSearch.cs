// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/PolyBucket.cpp and PolyBucket.h (CPolyBucket::IntersectsCell 004d3b10, StartSearch 004d4c00,
// GetNextPoly 004d4f00, StartLineSearch 004d50d0, AdvanceLineSearch 004d5650, GetNextPolyLine 004d57c0, GetZMask,
// IsSearchCellValid, BucketFloor; CPolyBucketIndexList::First/Next; CPolyBucketPacking::Unpack) and src/MeshPart.cpp
// (CMeshPart::GetFirstPoly 004ae110, GetNextPoly 004ae1a0, GetFirstPolyLine 004ae220, GetNextPolyLine 004ae430),
// reconstructed; not in Stuart's release.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Collision;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Mesh;

/// <summary>
/// The poly buckets' shared statics (one set per simulation, as retail has one per process): the line walk
/// (0082b688..0082b69c, 006316b4/b8) and the index lists' iterator. Not reentrant, as in retail.
/// </summary>
public sealed class PolyBucketStatics
{
    public float LineX;
    public float LineY;
    public int NumLineSteps;
    public bool PendingDiagonalStep;
    public int LineStep;
    public float LineStepX = 0.1f;
    public float LineStepY = 0.1f;

    /// <summary>CPolyBucketIndexList::sIterator.</summary>
    public int Iterator;

    /// <summary>The "search in invalid poly bucket sector" warnings retail logs.</summary>
    public int InvalidSectorWarnings;
}

/// <summary>A packed triangle's three vertices, as the search hands them out (CPolyBucketVertex pointers).</summary>
public readonly record struct PolyTriangle((short X, short Y, short Z) A, (short X, short Y, short Z) B, (short X, short Y, short Z) C);

public sealed partial class MeshPolyBucket
{
    private int[]? _stamps;
    private int _searchStamp;
    private int _minX, _minY, _maxX, _maxY, _currentX, _currentY;
    private short[]? _currentList;
    private int _currentTriangle = -1;
    private uint _searchZMask;
    private FVector _lineStart;
    private FVector _lineEnd;

    /// <summary>mSearchStamp (+0x9c), as the searches have advanced it.</summary>
    public int SearchStamp
    {
        get
        {
            EnsureState();
            return _searchStamp;
        }
    }

    private float Single(int at) => BinaryPrimitives.ReadSingleLittleEndian(Record.AsSpan(at));

    /// <summary>mOrigin (+0x30).</summary>
    public FVector Origin => new(Single(0x30), Single(0x34), Single(0x38));

    /// <summary>mPacking (+0x40): the packed vertices' origin and scale.</summary>
    public FVector PackingOrigin => new(Single(0x40), Single(0x44), Single(0x48));

    public float PackingScale => Single(0x50);

    /// <summary>mCellSizeXY (+0x70) and mCellSizeZ (+0x74).</summary>
    public float CellSizeXY => Single(0x70);

    public float CellSizeZ => Single(0x74);

    /// <summary>The run-time state starts as the stored record has it (Load reads the whole object).</summary>
    private void EnsureState()
    {
        if (_stamps is not null)
        {
            return;
        }

        _stamps = Triangles.Select(t => (int)(short)t.SearchStamp).ToArray();
        _searchStamp = BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0x9c));
        _currentX = BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0xb0));
        _currentY = BinaryPrimitives.ReadInt32LittleEndian(Record.AsSpan(0xb4));
        _currentTriangle = -1;
    }

    /// <summary>BucketFloor: (SINT)value under /QIfist, one less when that rounded up.</summary>
    private static int BucketFloor(float value)
    {
        int result = X87.Fist(value);
        if (value - result < 0.0f)
        {
            result--;
        }

        return result;
    }

    /// <summary>GetZMask: the 32 Z slices from low to high (all of them when the slice size is zero).</summary>
    private uint GetZMask(float low, float high)
    {
        if (CellSizeZ == 0.0)
        {
            return 0xffffffff;
        }

        int first = BucketFloor(low / CellSizeZ);
        int last = BucketFloor(high / CellSizeZ);
        if (first < 0)
        {
            first = 0;
        }

        if (last > 31)
        {
            last = 31;
        }

        uint mask = 0;
        for (int i = first; i <= last; ++i)
        {
            mask |= 1u << i;
        }

        return mask;
    }

    private bool IsSearchCellValid(PolyBucketStatics s)
    {
        if (_currentX < 0 || _currentX >= Width || _currentY < 0 || _currentY >= Height)
        {
            s.InvalidSectorWarnings++;
            return false;
        }

        return true;
    }

    private static int First(PolyBucketStatics s, short[] list)
    {
        if (list.Length <= 0)
        {
            return -1;
        }

        s.Iterator = 0;
        return list[0];
    }

    private static int Next(PolyBucketStatics s, short[] list)
    {
        if (++s.Iterator >= list.Length)
        {
            return -1;
        }

        return list[s.Iterator];
    }

    private void SetCurrentFromFirst(PolyBucketStatics s)
    {
        _currentTriangle = -1;
        if (_currentList is not null)
        {
            int index = First(s, _currentList);
            _currentTriangle = index == -1 ? -1 : index;
        }
    }

    private void SetCurrentFromNext(PolyBucketStatics s)
    {
        int index = Next(s, _currentList!);
        _currentTriangle = index == -1 ? -1 : index;
    }

    /// <summary>A triangle's packed vertices (the searches return triangle indices, -1 at the end).</summary>
    public PolyTriangle TriangleVertices(int triangle)
    {
        (uint _, ushort v0, ushort v1, ushort v2, ushort _) = Triangles[triangle];
        return new PolyTriangle(Vertices[(short)v0], Vertices[(short)v1], Vertices[(short)v2]);
    }

    private bool Eligible(int triangle) => _stamps![triangle] != _searchStamp && (Triangles[triangle].ZMask & _searchZMask) != 0;

    private void Stamp(int triangle) => _stamps![triangle] = (short)_searchStamp;

    /// <summary>StartSearch: the triangles in the cells a sphere's box covers (each once per search), first one.</summary>
    public int StartSearch(PolyBucketStatics s, FVector pos, float radius)
    {
        EnsureState();
        if (Cells.Length == 0 || CellSizeZ < 0.0001f)
        {
            return -1;
        }

        ++_searchStamp;
        FVector centre = new(pos.X - Origin.X, pos.Y - Origin.Y, pos.Z - Origin.Z);
        _searchZMask = GetZMask(centre.Z - radius, radius + centre.Z);
        if (_searchZMask == 0)
        {
            return -1;
        }

        _minX = BucketFloor((centre.X - radius) / CellSizeXY);
        if (_minX >= Width)
        {
            return -1;
        }

        _minY = BucketFloor((centre.Y - radius) / CellSizeXY);
        if (_minY >= Height)
        {
            return -1;
        }

        _maxX = BucketFloor((radius + centre.X) / CellSizeXY);
        if (_maxX < 0)
        {
            return -1;
        }

        _maxY = BucketFloor((radius + centre.Y) / CellSizeXY);
        if (_maxY < 0)
        {
            return -1;
        }

        _minX = Math.Max(_minX, 0);
        _minY = Math.Max(_minY, 0);
        _maxX = Math.Min(_maxX, Width - 1);
        _maxY = Math.Min(_maxY, Height - 1);
        _currentY = _minY;
        _currentX = _minX;
        _currentList = Cells[(_currentY * Width) + _currentX];
        SetCurrentFromFirst(s);
        return GetNextPoly(s);
    }

    /// <summary>GetNextPoly: the next unvisited triangle in the box with a Z slice in the search's, or null.</summary>
    public int GetNextPoly(PolyBucketStatics s)
    {
        EnsureState();
        if (Cells.Length == 0)
        {
            return -1;
        }

        while (true)
        {
            if (_currentTriangle != -1)
            {
                if (Eligible(_currentTriangle))
                {
                    break;
                }

                SetCurrentFromNext(s);
            }
            else if (_currentX < _maxX)
            {
                ++_currentX;
                _currentList = Cells[(_currentY * Width) + _currentX];
                if (_currentList is not null)
                {
                    SetCurrentFromFirst(s);
                }
            }
            else if (_currentY < _maxY)
            {
                ++_currentY;
                _currentX = _minX;
                _currentList = Cells[(_currentY * Width) + _currentX];
                if (_currentList is not null)
                {
                    SetCurrentFromFirst(s);
                }
            }
            else
            {
                return -1;
            }
        }

        int found = _currentTriangle;
        Stamp(found);
        SetCurrentFromNext(s);
        return found;
    }

    /// <summary>
    /// StartLineSearch: the segment in cell units, rejected when wholly outside, clipped to the grid (Z to 32 slices),
    /// then walked cell by cell (the major axis by one, the minor by the slope, with a pending diagonal step).
    /// </summary>
    public int StartLineSearch(PolyBucketStatics s, FVector start, FVector end)
    {
        EnsureState();
        if (Cells.Length == 0 || CellSizeZ < 0.0001f)
        {
            return -1;
        }

        ++_searchStamp;
        FVector origin = Origin;
        var ls = new FVector(start.X - origin.X, start.Y - origin.Y, start.Z - origin.Z);
        var le = new FVector(end.X - origin.X, end.Y - origin.Y, end.Z - origin.Z);
        ls.X /= CellSizeXY;
        le.X /= CellSizeXY;
        if (ls.X < 0.0f && le.X < 0.0f)
        {
            return -1;
        }

        if (ls.X > Width && le.X > Width)
        {
            return -1;
        }

        ls.Y /= CellSizeXY;
        le.Y /= CellSizeXY;
        if (ls.Y < 0.0f && le.Y < 0.0f)
        {
            return -1;
        }

        if (ls.Y > Height && le.Y > Height)
        {
            return -1;
        }

        ls.Z /= CellSizeZ;
        le.Z /= CellSizeZ;
        if (ls.Z < 0.0f && le.Z < 0.0f)
        {
            return -1;
        }

        if (ls.Z > 32.0f && le.Z > 32.0f)
        {
            return -1;
        }

        float[] bounds = [0.0f, 0.0f, Width - 0.0001f, Height - 0.0001f, 0.0f, 31.9999f];
        if (!Geometry.ClipSegmentAgainstAABB3D(ref ls, ref le, bounds))
        {
            _lineStart = ls;
            _lineEnd = le;
            return -1;
        }

        _lineStart = ls;
        _lineEnd = le;
        float low = ls.Z > le.Z ? le.Z : ls.Z;
        float high = ls.Z >= le.Z ? ls.Z : le.Z;
        _searchZMask = GetZMask(low * CellSizeZ, high * CellSizeZ);
        if (_searchZMask == 0)
        {
            return -1;
        }

        _currentTriangle = -1;
        float dx = le.X - ls.X;
        float dy = le.Y - ls.Y;
        if (MathF.Abs(dx) > MathF.Abs(dy))
        {
            s.LineStepX = 1.0f;
            if (dx <= 0.0)
            {
                s.LineStepX = -1.0f;
            }

            s.LineStepY = dy != 0.0f ? dy / MathF.Abs(dx) : 0.0f;
            s.NumLineSteps = BucketFloor(MathF.Abs(dx)) + 1;
        }
        else
        {
            s.LineStepY = 1.0f;
            if (dy <= 0.0)
            {
                s.LineStepY = -1.0f;
            }

            s.LineStepX = dx != 0.0f ? dx / MathF.Abs(dy) : 0.0f;
            s.NumLineSteps = BucketFloor(MathF.Abs(dy)) + 1;
        }

        s.LineX = ls.X;
        s.LineY = ls.Y;
        _currentX = BucketFloor(s.LineX);
        _currentY = BucketFloor(s.LineY);
        s.PendingDiagonalStep = false;
        s.LineStep = 0;
        if (!IsSearchCellValid(s))
        {
            return -1;
        }

        return GetNextPolyLine(s, false);
    }

    /// <summary>IntersectsCell: whether the segment meets the cell (an axis-aligned square, edges in both slopes).</summary>
    private static bool IntersectsCell(float x, float y, float width, float height, FVector start, FVector end)
    {
        float right = x + width;
        if (start.X > right && end.X > right)
        {
            return false;
        }

        if (start.X < x && end.X < x)
        {
            return false;
        }

        float top = y + height;
        if (start.Y > top && end.Y > top)
        {
            return false;
        }

        if (start.Y < y && end.Y < y)
        {
            return false;
        }

        float diffX = end.X - start.X;
        float diffY = end.Y - start.Y;
        if (diffX != 0.0)
        {
            float slope = diffY / diffX;
            float first = ((x - start.X) * slope) + start.Y;
            float last = ((right - start.X) * slope) + start.Y;
            if (first >= top && last >= top)
            {
                return false;
            }

            if (first < y && last < y)
            {
                return false;
            }
        }

        if (diffY != 0.0)
        {
            float slope = diffX / diffY;
            float first = ((y - start.Y) * slope) + start.X;
            float last = ((top - start.Y) * slope) + start.X;
            if (first >= right && last >= right)
            {
                return false;
            }

            if (first < x && last < x)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>AdvanceLineSearch: the walk's next cell (a diagonal move takes two, the side the line crosses first).</summary>
    private bool AdvanceLineSearch(PolyBucketStatics s)
    {
        while (true)
        {
            if (!s.PendingDiagonalStep)
            {
                if (++s.LineStep >= s.NumLineSteps)
                {
                    return false;
                }

                s.LineX += s.LineStepX;
                s.LineY += s.LineStepY;
            }

            s.PendingDiagonalStep = false;
            int x = BucketFloor(s.LineX);
            int y = BucketFloor(s.LineY);
            if (x == _currentX || y == _currentY)
            {
                _currentX = x;
                _currentY = y;
                if (IsSearchCellValid(s))
                {
                    return true;
                }
            }
            else if (IntersectsCell(x, _currentY, 1.0f, 1.0f, _lineStart, _lineEnd))
            {
                _currentX = x;
                s.PendingDiagonalStep = true;
                return true;
            }
            else
            {
                _currentY = y;
                s.PendingDiagonalStep = true;
                return true;
            }
        }
    }

    /// <summary>GetNextPolyLine: the next unvisited triangle along the walk (only this cell's when told to stop after it).</summary>
    public int GetNextPolyLine(PolyBucketStatics s, bool stopAfterCell)
    {
        EnsureState();
        if (Cells.Length == 0)
        {
            return -1;
        }

        while (true)
        {
            if (_currentTriangle != -1)
            {
                SetCurrentFromNext(s);
            }
            else
            {
                _currentList = Cells[(_currentY * Width) + _currentX];
                if (_currentList is not null)
                {
                    SetCurrentFromFirst(s);
                }
            }

            if (_currentTriangle != -1)
            {
                if (Eligible(_currentTriangle))
                {
                    Stamp(_currentTriangle);
                    return _currentTriangle;
                }
            }
            else if (stopAfterCell || !AdvanceLineSearch(s))
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// CPolyBucketVertex::Unpack: ((coordinate * scale) * (1/32767)) + origin per axis (the CollideWithLine copy's
    /// order; 1/32767 is a float constant).
    /// </summary>
    public FVector Unpack((short X, short Y, short Z) v)
    {
        const float InvMax = 1.0f / 32767.0f;
        FVector o = PackingOrigin;
        float scale = PackingScale;
        return new FVector((v.X * scale * InvMax) + o.X, (v.Y * scale * InvMax) + o.Y, (v.Z * scale * InvMax) + o.Z);
    }
}
