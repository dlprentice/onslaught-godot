// SPDX-License-Identifier: GPL-3.0-or-later
// Adapted from bea-decomp src/mapwho.h and src/mapwho.cpp (CMapWho: Create 004919b0, Clear 00491930, GetLevelForRadius
// 00491c50, AddEntry 00491cd0, RemoveEntry 00491d20, GetFirstEntry 00491d80, GetNextEntry 00491d90, IsValidSector
// 00491da0, SetupNextRadiusLayer 00491df0, GetFirstEntryWithinRadius 00491ea0, GetNextEntryWithinRadius 00492020,
// GetFirstEntryWithinLine 00492110, SetupNextLineLayer 004922f0, AdvanceLineSector 004924b0, GetNextEntryWithinLine
// 004925a0, WorldToSector 00492670, SortEntries 004926e0), src/mapwhoentry.h and src/MapWhoEntry.cpp (CMapWhoEntry:
// Init 00492ba0, RemoveFromMap 00492c70, UpdateEntry 00492ca0) and src/Geometry.cpp (ClipSegmentAgainstAABB3D,
// SegmentOutCode). Not in Stuart's release.

using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>CSector: a cell of one map-who layer (coordinates kept as shorts, as retail stores them).</summary>
public record struct Sector(short X, short Y, int Layer)
{
    public Sector(int x, int y, int layer)
        : this(unchecked((short)x), unchecked((short)y), layer)
    {
    }

    /// <summary>ToParent: up a layer.</summary>
    public readonly Sector ToParent() => new((short)(X >> 1), (short)(Y >> 1), Layer - 1);
}

/// <summary>CMapWhoNode: a sector's entry list and, above layer 4, its four children.</summary>
public sealed class MapWhoNode
{
    public MapWhoNode?[]? Children;
    public MapWhoEntry? First;
}

/// <summary>CMapWhoEntry: a thing's place in the map-who grid (embedded in CThing), linked into its sector's list.</summary>
public sealed class MapWhoEntry(Thing thing)
{
    public MapWhoEntry? Next;
    public MapWhoEntry? Prev;

    /// <summary>mSectorIn; layer -1 until Init.</summary>
    public Sector SectorIn = new(0, 0, -1);

    public Thing Thing { get; } = thing;

    /// <summary>
    /// Init: the sector at the layer the thing's size needs. The size is the bounding box's footprint when the render
    /// thing has a box (sqrt of float operands: fsqrt at 24-bit precision), else the render radius; a force radius of
    /// 0 or more overrides both.
    /// </summary>
    public void Init(MapWho map, FVector pos, float forceRadius)
    {
        Next = null;
        Prev = null;
        float radius = Thing.GetRadius();
        BoundingBox? box = Thing.RenderThing?.MeshOrNull?.BoundingBox;
        if (box is not null)
        {
            float x = MathF.Abs(box.Origin.X) + box.Axes.X;
            float y = MathF.Abs(box.Origin.Y) + box.Axes.Y;
            radius = MathF.Sqrt((x * x) + (y * y));
        }

        if (forceRadius >= 0)
        {
            radius = forceRadius;
        }

        SectorIn = map.WorldToSector(pos, map.GetLevelForRadius(radius));
        map.AddEntry(this);
    }

    /// <summary>RemoveFromMap.</summary>
    public void RemoveFromMap(MapWho map)
    {
        if (SectorIn.Layer != -1)
        {
            map.RemoveEntry(this);
        }
    }

    /// <summary>UpdateEntry: moves to the sector of <paramref name="pos"/> on the same layer; true when it moved.</summary>
    public bool UpdateEntry(MapWho map, FVector pos)
    {
        Sector sector = map.WorldToSector(pos, SectorIn.Layer);
        if (SectorIn != sector)
        {
            RemoveFromMap(map);
            SectorIn = sector;
            map.AddEntry(this);
            return true;
        }

        return false;
    }
}

/// <summary>
/// CMapWho: five layers of sectors over the 512 x 512 map (64 x 64 cells of 8 at layer 4 up to 4 x 4 cells of 128 at
/// layer 0). Each thing sits in one sector of the layer its size needs. Radius and line queries walk the layers from
/// 4 down to 0 with one shared iterator, as retail does.
/// </summary>
public sealed class MapWho
{
    /// <summary>USE_BIGLIST_FOR (engine.h): things on a layer below this also join WORLD's big-things list.</summary>
    public const int UseBigListFor = 3;

    private MapWhoNode[][]? _layers;
    private readonly int[] _shift = new int[5];
    private readonly int[] _width = new int[5];
    private readonly int[] _height = new int[5];
    private MapWhoEntry? _iterator;
    private int _queryMinX;
    private int _queryMinY;
    private int _queryMaxX;
    private int _queryMaxY;
    private FVector _queryPos;
    private int _queryLayer;
    private float _queryRadius;
    private Sector _querySector;
    private int _lineSide;
    private FVector _lineStart;
    private FVector _lineEnd;
    private int _lineStep;
    private Sector _lineSector;
    private bool _queryActive;
    private bool _lineXMajor;
    private float _lineStepX;
    private float _lineStepY;
    private int _lineStepLimit;
    private FVector _linePosition;
    private Sector _lineBaseSector;

    /// <summary>Create: the layers, each parent node's four children (in child order x + 2y).</summary>
    public void Create()
    {
        _layers = new MapWhoNode[5][];
        int width = 64;
        int height = 64;
        for (int i = 3; i <= 7; i++)
        {
            int layer = 7 - i;
            _layers[layer] = new MapWhoNode[width * height];
            for (int n = 0; n < width * height; n++)
            {
                _layers[layer][n] = new MapWhoNode();
            }

            _shift[layer] = i;
            _width[layer] = width;
            _height[layer] = height;
            width >>= 1;
            height >>= 1;
        }

        width = 64;
        height = 64;
        for (int layer = 4; layer >= 1; layer--)
        {
            for (int i = 0; i < width; i++)
            {
                int childX = i % 2;
                for (int y = 0; y < height; y++)
                {
                    int childY = 2 * (y % 2);
                    var sector = new Sector(i, y, layer);
                    MapWhoNode node = GetSector(sector);
                    MapWhoNode parent = GetSector(sector.ToParent());
                    parent.Children ??= new MapWhoNode?[4];
                    if (parent.Children[childX + childY] is not null)
                    {
                        throw new InvalidOperationException("FATAL ERROR: Mapwho construction gone wrong");
                    }

                    parent.Children[childX + childY] = node;
                }
            }

            width >>= 1;
            height >>= 1;
        }

        _queryActive = false;
    }

    /// <summary>Clear: the layers are freed.</summary>
    public void Clear() => _layers = null;

    public MapWhoNode GetSector(Sector s) => _layers![s.Layer][s.X + (s.Y * (64 >> (4 - s.Layer)))];

    /// <summary>GetLevelForRadius: the finest layer whose cell is at least 2.01 times the radius (0 when none is).</summary>
    public int GetLevelForRadius(float radius)
    {
        radius *= 2.01f;
        int layer = 4;
        while ((1 << _shift[layer]) < radius && layer >= 0)
        {
            layer--;
            if (layer < 0)
            {
                return 0; // "WARNING: Object too big for map who system"
            }
        }

        return layer;
    }

    /// <summary>AddEntry: at the head of its sector's list.</summary>
    public void AddEntry(MapWhoEntry entry)
    {
        MapWhoNode node = GetSector(entry.SectorIn);
        MapWhoEntry? first = node.First;
        if (first is not null)
        {
            first.Prev = entry;
        }

        entry.Next = first;
        entry.Prev = null;
        node.First = entry;
    }

    public void RemoveEntry(MapWhoEntry entry)
    {
        MapWhoEntry? prev = entry.Prev;
        MapWhoEntry? next = entry.Next;
        if (prev is not null)
        {
            prev.Next = next;
        }

        if (next is not null)
        {
            next.Prev = prev;
        }

        MapWhoNode node = GetSector(entry.SectorIn);
        if (ReferenceEquals(entry, node.First))
        {
            node.First = next;
        }
    }

    public MapWhoEntry? GetFirstEntry(MapWhoNode node)
    {
        _iterator = node.First;
        return _iterator;
    }

    public MapWhoEntry? GetNextEntry()
    {
        if (_iterator is not null)
        {
            _iterator = _iterator.Next;
        }

        return _iterator;
    }

    public static bool IsValidSector(Sector s) =>
        !(s.Layer < 0 || s.Layer >= 5 || s.X < 0 || s.X >= (64 >> (4 - s.Layer)) || s.Y < 0 || s.Y >= (64 >> (4 - s.Layer)));

    /// <summary>WorldToSector: (int) of each coordinate (fistp, to nearest), shifted, clamped to the layer.</summary>
    public Sector WorldToSector(FVector pos, int layer)
    {
        int x = X87.Fist(pos.X) >> _shift[layer];
        int y = X87.Fist(pos.Y) >> _shift[layer];
        if (x >= _width[layer])
        {
            x = _width[layer] - 1;
        }

        if (x < 0)
        {
            x = 0;
        }

        if (y >= _height[layer])
        {
            y = _height[layer] - 1;
        }

        if (y < 0)
        {
            y = 0;
        }

        return new Sector(x, y, layer);
    }

    private bool SetupNextRadiusLayer()
    {
        _queryLayer--;
        if (_queryLayer < 0)
        {
            return false;
        }

        int shift = _shift[_queryLayer];
        float halfCell = (1 << shift) * 0.5f;
        _queryMinX = X87.Fist(_queryPos.X - _queryRadius - halfCell) >> shift;
        float maxX = _queryPos.X;
        maxX += _queryRadius;
        _queryMaxX = X87.Fist(maxX + halfCell) >> shift;
        _queryMinY = X87.Fist(_queryPos.Y - _queryRadius - halfCell) >> shift;
        _queryMaxY = X87.Fist((halfCell + _queryRadius) + _queryPos.Y) >> shift;
        _querySector = new Sector(_queryMinX, _queryMinY, _queryLayer);
        return true;
    }

    /// <summary>GetFirstEntryWithinRadius: the first entry of the square of sectors around the position, layer 4 first.</summary>
    public MapWhoEntry? GetFirstEntryWithinRadius(FVector pos, float radius)
    {
        _queryLayer = 5;
        _queryPos = pos;
        _queryRadius = radius;
        SetupNextRadiusLayer();
        _queryActive = true;
        MapWhoEntry? entry = null;
        if (IsValidSector(_querySector))
        {
            entry = GetFirstEntry(GetSector(_querySector));
        }

        return entry ?? AdvanceRadiusIterator();
    }

    public MapWhoEntry? GetNextEntryWithinRadius() => _queryActive ? AdvanceRadiusIterator() : null;

    private MapWhoEntry? AdvanceRadiusIterator()
    {
        MapWhoEntry? entry = GetNextEntry();
        while (entry is null)
        {
            _querySector.X++;
            if (_querySector.X > _queryMaxX)
            {
                _querySector.Y++;
                if (_querySector.Y <= _queryMaxY)
                {
                    _querySector.X = (short)_queryMinX;
                }
                else if (!SetupNextRadiusLayer())
                {
                    _queryActive = false;
                    return null;
                }
            }

            if (IsValidSector(_querySector))
            {
                entry = GetFirstEntry(GetSector(_querySector));
            }
        }

        return entry;
    }

    /// <summary>GetFirstEntryWithinLine: the segment clipped to the map, then the sectors along it, layer 4 first.</summary>
    public MapWhoEntry? GetFirstEntryWithinLine(FVector start, FVector end)
    {
        float[] bounds = [0, 0, 512, 512, -10000, 10000];
        ClipSegmentAgainstAabb3D(ref start, ref end, bounds);
        _queryLayer = 5;
        _lineStart = start;
        _lineEnd = end;
        SetupNextLineLayer();
        _queryActive = true;
        MapWhoEntry? entry = null;
        if (IsValidSector(_lineSector))
        {
            entry = GetFirstEntry(GetSector(_lineSector));
        }

        return entry ?? AdvanceLineEntryIterator();
    }

    public MapWhoEntry? GetNextEntryWithinLine() => _queryActive ? AdvanceLineEntryIterator() : null;

    private bool SetupNextLineLayer()
    {
        _queryLayer--;
        if (_queryLayer < 0)
        {
            return false;
        }

        _lineSide = 0;
        _lineStep = 0;
        float cellSize = 1 << _shift[_queryLayer];
        FVector delta = _lineEnd - _lineStart;
        if (delta.X == 0 && delta.Y == 0)
        {
            _lineStepX = cellSize;
            _lineStepY = 0;
            _lineStepLimit = 2;
            _lineXMajor = true;
        }
        else
        {
            float absX = MathF.Abs(delta.X);
            float absY = MathF.Abs(delta.Y);
            if (absX > absY)
            {
                _lineXMajor = true;
                _lineStepX = delta.X > 0 ? cellSize : -cellSize;
                _lineStepY = (delta.Y / absX) * cellSize;
                _lineStepLimit = X87.Fist(Math.Floor((double)(absX / cellSize))) + 2;
            }
            else
            {
                _lineXMajor = false;
                _lineStepY = delta.Y > 0 ? cellSize : -cellSize;
                _lineStepX = (delta.X / absY) * cellSize;
                _lineStepLimit = X87.Fist(Math.Floor((double)(absY / cellSize))) + 2;
            }
        }

        _linePosition = _lineStart;
        _linePosition.X -= _lineStepX;
        _linePosition.Y -= _lineStepY;
        _lineBaseSector = WorldToSector(_linePosition, _queryLayer);
        _lineSector = _lineBaseSector;
        return true;
    }

    private bool AdvanceLineSector()
    {
        _lineSide++;
        _lineSector = _lineBaseSector;
        if (_lineSide == 1)
        {
            if (_lineXMajor)
            {
                _lineSector.Y++;
            }
            else
            {
                _lineSector.X--;
            }
        }

        if (_lineSide == 2)
        {
            if (_lineXMajor)
            {
                _lineSector.Y--;
            }
            else
            {
                _lineSector.X++;
            }
        }

        if (_lineSide == 3)
        {
            _lineStep++;
            if (_lineStep > _lineStepLimit)
            {
                return false;
            }

            _linePosition.X += _lineStepX;
            _linePosition.Y += _lineStepY;
            _lineBaseSector = WorldToSector(_linePosition, _queryLayer);
            _lineSector = _lineBaseSector;
            _lineSide = 0;
        }

        return true;
    }

    private MapWhoEntry? AdvanceLineEntryIterator()
    {
        MapWhoEntry? entry = GetNextEntry();
        while (entry is null)
        {
            if (!AdvanceLineSector() && !SetupNextLineLayer())
            {
                _queryActive = false;
                return null;
            }

            if (IsValidSector(_lineSector))
            {
                entry = GetFirstEntry(GetSector(_lineSector));
            }
        }

        return entry;
    }

    /// <summary>SortEntries: in each sector of layers 4..1, trees move behind the original tail, in encounter order.</summary>
    public void SortEntries()
    {
        int width = 64;
        int height = 64;
        for (int layer = 4; layer >= 1; layer--)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    MapWhoNode node = GetSector(new Sector(x, y, layer));
                    MapWhoEntry? tail = node.First;
                    while (tail?.Next is not null)
                    {
                        tail = tail.Next;
                    }

                    if (tail is null)
                    {
                        continue;
                    }

                    MapWhoEntry? prev = null;
                    MapWhoEntry? entry = node.First;
                    MapWhoEntry originalTail = tail;
                    while (entry is not null && !ReferenceEquals(entry, originalTail) && entry.Next is not null)
                    {
                        MapWhoEntry? next = entry.Next;
                        if (entry.Thing.IsA(ThingType.Tree))
                        {
                            if (prev is not null)
                            {
                                prev.Next = next;
                            }
                            else
                            {
                                node.First = next;
                            }

                            if (next is not null)
                            {
                                next.Prev = prev;
                            }

                            entry.Next = null;
                            entry.Prev = tail;
                            tail.Next = entry;
                            tail = entry;
                        }
                        else
                        {
                            prev = entry;
                        }

                        entry = next;
                    }
                }
            }

            width >>= 1;
            height >>= 1;
        }
    }

    private static int SegmentOutCode(float x, float y, float z, float[] b)
    {
        int code = 0;
        if (y > b[3])
        {
            code |= 1;
        }
        else if (y < b[1])
        {
            code |= 2;
        }

        if (x > b[2])
        {
            code |= 4;
        }
        else if (x < b[0])
        {
            code |= 8;
        }

        if (z > b[5])
        {
            code |= 16;
        }
        else if (z < b[4])
        {
            code |= 32;
        }

        return code;
    }

    /// <summary>ClipSegmentAgainstAABB3D (Geometry.cpp): Cohen-Sutherland in three dimensions.</summary>
    public static bool ClipSegmentAgainstAabb3D(ref FVector s, ref FVector e, float[] b)
    {
        int first = SegmentOutCode(s.X, s.Y, s.Z, b);
        int second = SegmentOutCode(e.X, e.Y, e.Z, b);
        while ((first | second) != 0)
        {
            if ((first & second) != 0)
            {
                return false;
            }

            int code = first != 0 ? first : second;
            float x, y, z;
            if ((code & 1) != 0)
            {
                float f = (b[3] - s.Y) / (e.Y - s.Y);
                x = s.X + ((e.X - s.X) * f);
                z = s.Z + ((e.Z - s.Z) * f);
                y = b[3];
            }
            else if ((code & 2) != 0)
            {
                float f = (b[1] - s.Y) / (e.Y - s.Y);
                x = s.X + ((e.X - s.X) * f);
                z = s.Z + ((e.Z - s.Z) * f);
                y = b[1];
            }
            else if ((code & 4) != 0)
            {
                x = b[2];
                float f = (b[2] - s.X) / (e.X - s.X);
                y = s.Y + ((e.Y - s.Y) * f);
                z = s.Z + ((e.Z - s.Z) * f);
            }
            else if ((code & 8) != 0)
            {
                x = b[0];
                float f = (b[0] - s.X) / (e.X - s.X);
                y = s.Y + ((e.Y - s.Y) * f);
                z = s.Z + ((e.Z - s.Z) * f);
            }
            else if ((code & 16) != 0)
            {
                z = b[5];
                float f = (b[5] - s.Z) / (e.Z - s.Z);
                x = s.X + ((e.X - s.X) * f);
                y = s.Y + ((e.Y - s.Y) * f);
            }
            else
            {
                z = b[4];
                float f = (b[4] - s.Z) / (e.Z - s.Z);
                x = s.X + ((e.X - s.X) * f);
                y = s.Y + ((e.Y - s.Y) * f);
            }

            if (code == first)
            {
                s = new FVector(x, y, z);
                first = SegmentOutCode(s.X, s.Y, s.Z, b);
            }
            else
            {
                e = new FVector(x, y, z);
                second = SegmentOutCode(e.X, e.Y, e.Z, b);
            }
        }

        return true;
    }
}
