// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public interface IRetailMapWhoOwner
{
    int Identity { get; }
    Level100FloatVector3Bits PositionFloatBits { get; }
    uint ThingTypeMask { get; }
}

public readonly record struct RetailMapWhoSector(int X, int Y, int Layer);

/// <summary>
/// Mutable, owner-bound sector lists. Pristine 74154bfa…7750:
/// Init 4919b0, radius 491c50, add/remove 491cd0/491d20, shared cursor
/// 491d80/491d90, coordinate conversion 492670, update 492ca0.
/// Initial collision traversal is 480a30/480e10. Line queries
/// and moved-sector collision effects are not implemented. Sort 4926e0 is
/// exposed for the later PostLoad phase, not run during object construction.
/// </summary>
public sealed class RetailMapWho
{
    public sealed class Entry
    {
        internal Entry(RetailMapWho map, IRetailMapWhoOwner owner, RetailMapWhoSector sector)
        {
            Map = map;
            Owner = owner;
            Sector = sector;
        }
        internal RetailMapWho Map { get; }
        internal Entry? Next;
        internal Entry? Previous;
        public IRetailMapWhoOwner Owner { get; }
        public RetailMapWhoSector Sector { get; internal set; }
        public bool IsRegistered { get; internal set; }
    }

    private readonly Entry?[][] _heads = Enumerable.Range(0, 5)
        .Select(layer => new Entry?[(4 << layer) * (4 << layer)]).ToArray();
    private readonly Dictionary<int, Entry> _entries = [];
    private Entry? _cursor;
    private readonly (int XLow, int XHigh, int YLow, int YHigh)[] _radiusBounds = new (int, int, int, int)[5];
    private int _radiusLayer, _radiusX, _radiusY;
    private bool _radiusActive;

    /// <summary>
    /// FISTP's integer rounding must be selected explicitly. This does not
    /// assert the retail load's unobserved FPU mode or emulate all x87 modes;
    /// floating arithmetic uses binary64 with explicit nearest float32 stores.
    /// </summary>
    public RetailMapWho(MidpointRounding integerRounding)
    {
        if (integerRounding is not (MidpointRounding.ToEven or MidpointRounding.ToZero or
            MidpointRounding.ToNegativeInfinity or MidpointRounding.ToPositiveInfinity))
            throw new ArgumentOutOfRangeException(nameof(integerRounding));
        IntegerRounding = integerRounding;
    }

    public MidpointRounding IntegerRounding { get; }
    public int Count => _entries.Count;

    internal static float MeshSpatialRadius(IReadOnlyList<int> bbox)
    {
        double x = Math.Abs((double)BitConverter.Int32BitsToSingle(bbox[0])) +
            BitConverter.Int32BitsToSingle(bbox[4]);
        double y = Math.Abs((double)BitConverter.Int32BitsToSingle(bbox[1])) +
            BitConverter.Int32BitsToSingle(bbox[5]);
        return (float)Math.Sqrt(y * y + x * x); // 492bd0..492beb, one final store.
    }

    public static int GetLevelForRadius(float radius)
    {
        if (!float.IsFinite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        float diameter = (float)((double)radius * BitConverter.Int32BitsToSingle(0x4000a3d7));
        for (int layer = 4; layer >= 0; layer--)
            if ((1 << (7 - layer)) >= diameter) return layer;
        return 0; // Retail also emits a diagnostic for this oversized arm.
    }

    public RetailMapWhoSector WorldToSector(Level100FloatVector3Bits position, int layer)
    {
        if ((uint)layer > 4) throw new ArgumentOutOfRangeException(nameof(layer));
        int Coordinate(int bits)
        {
            double coordinate = BitConverter.Int32BitsToSingle(bits);
            // The supported world inputs are finite. Keep the low signed word
            // of FISTP64, rather than narrowing directly to a saturating int.
            if (!double.IsFinite(coordinate) || coordinate < long.MinValue || coordinate >= 9223372036854775808.0)
                throw new ArgumentOutOfRangeException(nameof(position));
            long rounded = checked((long)Math.Round(coordinate, IntegerRounding));
            return Math.Clamp(unchecked((int)rounded) >> (7 - layer), 0, (4 << layer) - 1);
        }
        return new(Coordinate(position.X), Coordinate(position.Y), layer);
    }

    public Entry Add(IRetailMapWhoOwner owner, float radius)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Identity <= 0 || _entries.ContainsKey(owner.Identity))
            throw new ArgumentException("MapWho requires a unique positive owned identity.", nameof(owner));
        var entry = new Entry(this, owner, WorldToSector(owner.PositionFloatBits, GetLevelForRadius(radius)));
        _entries.Add(owner.Identity, entry);
        InsertHead(entry);
        return entry;
    }

    public bool UpdatePosition(Entry entry) => UpdatePosition(entry, entry.Owner.PositionFloatBits);

    /// <summary>Retail accepts a supplied position; Actor.Move can pass old pose.</summary>
    public bool UpdatePosition(Entry entry, Level100FloatVector3Bits position)
    {
        RequireRegistered(entry);
        RetailMapWhoSector next = WorldToSector(position, entry.Sector.Layer);
        if (next == entry.Sector) return false;
        Unlink(entry);
        entry.Sector = next;
        InsertHead(entry);
        return true;
    }

    public void Remove(Entry entry)
    {
        RequireRegistered(entry);
        Unlink(entry);
        _entries.Remove(entry.Owner.Identity);
        entry.IsRegistered = false;
        // Retail leaves the removed links, sector and shared cursor intact.
        // Repeated removal is invalid use; the managed boundary refuses it.
    }

    public Entry? FirstInSector(RetailMapWhoSector sector)
    {
        if (!InBounds(sector)) throw new ArgumentOutOfRangeException(nameof(sector));
        return _cursor = _heads[sector.Layer][Index(sector)];
    }

    public Entry? NextInSector() => _cursor = _cursor?.Next;

    /// <summary>
    /// Retail PostLoad (call at 46d23a) sorts layers 4 through 1. Each sector's
    /// original tail is a stop marker and is never examined; preceding trees
    /// move to the current tail. An all-tree [A,B,C] becomes [C,A,B], so this
    /// is neither a stable partition nor an idempotent operation. Entries,
    /// owner state and the shared query cursor survive the relinking.
    /// </summary>
    public void SortAfterLoad()
    {
        for (int layer = 4; layer >= 1; layer--)
            for (int x = 0; x < (4 << layer); x++)
                for (int y = 0; y < (4 << layer); y++)
                {
                    Entry? current = _heads[layer][Index(new(x, y, layer))];
                    if (current is null) continue;
                    Entry originalTail = current;
                    while (originalTail.Next is not null) originalTail = originalTail.Next;
                    Entry tail = originalTail;
                    while (current is not null && !ReferenceEquals(current, originalTail) &&
                           current.Next is not null)
                    {
                        Entry next = current.Next;
                        if ((current.Owner.ThingTypeMask & 0x02000000) != 0)
                        {
                            Unlink(current);
                            current.Next = null;
                            current.Previous = tail;
                            tail.Next = current;
                            tail = current;
                        }
                        current = next;
                    }
                }
    }

    /// <summary>
    /// Walks actual lists, including the caller entry. The collision owner must
    /// exclude self and apply mutual masks. Callbacks can mutate membership or
    /// overwrite the global cursor; continuation reads that live cursor.
    /// </summary>
    public void VisitInitialCollisionNeighbors(Entry entry, Action<Entry> visit)
    {
        RequireRegistered(entry);
        ArgumentNullException.ThrowIfNull(visit);
        int initialLayer = entry.Sector.Layer;
        for (int layer = initialLayer; layer >= 0; layer--)
        {
            RetailMapWhoSector center = WorldToSector(entry.Owner.PositionFloatBits, layer);
            for (int x = center.X - 1; x <= center.X + 1; x++)
                for (int y = center.Y - 1; y <= center.Y + 1; y++)
                {
                    var sector = new RetailMapWhoSector(x, y, layer);
                    if (InBounds(sector)) Visit(sector, layer == initialLayer, visit);
                }
        }
    }

    /// <summary>
    /// PC24/RN rectangular radius traversal: layers 4..0, ascending Y then X,
    /// then actual cell links. No distance, Z, type or liveness filtering.
    /// Query rounding is nearest-even independently of membership rounding.
    /// </summary>
    public void VisitRadius(Level100FloatVector3Bits center, int radiusFloatBits, Action<Entry> visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        for (Entry? entry = FirstInRadius(center, radiusFloatBits); entry is not null; entry = NextInRadius())
            visit(entry);
    }

    /// <summary>
    /// Shared radius cursor, pristine74154bfa…7750. Setup [491df0,491e92)
    /// SHA256 7c56dc514b2a8581049166d94635ed6a98c9a8e090258eab435f35e7750815b1;
    /// First [491ea0,49201a) a9a7cddb2d1525c99ff4e0d18413e997b5bcef0f0ddf59acdc4401048f8f991f;
    /// Next [492020,49210b) 83af29f512f604e432ab9730d03619919d32eac61d0aeb50902691eb4b2f7226.
    /// Nested radius queries replace the outer query; sector queries replace its
    /// entry cursor. An invalid first cell advances the pre-existing cursor.
    /// </summary>
    public Entry? FirstInRadius(Level100FloatVector3Bits center, int radiusFloatBits)
    {
        double x = BitConverter.Int32BitsToSingle(center.X);
        double y = BitConverter.Int32BitsToSingle(center.Y);
        double radius = BitConverter.Int32BitsToSingle(radiusFloatBits);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(center), "Query XY and radius must be finite, radius nonnegative.");
        static int Coordinate(double value, int shift)
        {
            // FISTP64 followed by the low signed word, not saturating int conversion.
            double rounded = Math.Round(value, MidpointRounding.ToEven);
            if (rounded < long.MinValue || rounded >= 9223372036854775808.0)
                throw new ArgumentOutOfRangeException(nameof(center), "Query exceeds the admitted FISTP64 domain.");
            int coordinate = unchecked((int)checked((long)rounded)) >> shift;
            // Native current-cell counters are signed 16-bit. Admit only
            // ranges whose inclusive traversal cannot wrap those counters.
            if (coordinate < short.MinValue || coordinate >= short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(center), "Query exceeds the admitted cell-counter domain.");
            return coordinate;
        }
        // Validate every layer before invoking a callback.
        Span<(int XLow, int XHigh, int YLow, int YHigh)> bounds = stackalloc (int, int, int, int)[5];
        for (int layer = 4; layer >= 0; layer--)
        {
            int shift = 7 - layer;
            double half = RetailFloat24.Multiply(1 << shift, 0.5);
            bounds[layer] = (
                Coordinate(RetailFloat24.Subtract(RetailFloat24.Subtract(x, radius), half), shift),
                Coordinate(RetailFloat24.Add(RetailFloat24.Add(x, radius), half), shift),
                Coordinate(RetailFloat24.Subtract(RetailFloat24.Subtract(y, radius), half), shift),
                Coordinate(RetailFloat24.Add(RetailFloat24.Add(half, radius), y), shift));
        }
        bounds.CopyTo(_radiusBounds);
        _radiusLayer = 4;
        _radiusX = _radiusBounds[4].XLow;
        _radiusY = _radiusBounds[4].YLow;
        _radiusActive = true;
        var first = new RetailMapWhoSector(_radiusX, _radiusY, _radiusLayer);
        if (InBounds(first) && FirstInSector(first) is { } entry) return entry;
        return NextInRadius();
    }

    public Entry? NextInRadius()
    {
        if (!_radiusActive) return null;
        if (NextInSector() is { } next) return next;
        while (true)
        {
            var bounds = _radiusBounds[_radiusLayer];
            if (++_radiusX > bounds.XHigh)
            {
                if (++_radiusY > bounds.YHigh)
                {
                    if (--_radiusLayer < 0)
                    {
                        _radiusActive = false;
                        return null;
                    }
                    bounds = _radiusBounds[_radiusLayer];
                    _radiusY = bounds.YLow;
                }
                _radiusX = bounds.XLow;
            }
            var sector = new RetailMapWhoSector(_radiusX, _radiusY, _radiusLayer);
            if (InBounds(sector) && FirstInSector(sector) is { } entry) return entry;
        }
    }

    private void Visit(RetailMapWhoSector sector, bool descend, Action<Entry> visit)
    {
        if (descend && sector.Layer < 4)
            for (int child = 0; child < 4; child++)
                Visit(new(sector.X * 2 + (child & 1), sector.Y * 2 + (child >> 1),
                    sector.Layer + 1), true, visit);
        for (Entry? next = FirstInSector(sector); next is not null; next = NextInSector())
            visit(next);
    }

    private void InsertHead(Entry entry)
    {
        Entry? head = _heads[entry.Sector.Layer][Index(entry.Sector)];
        if (head is not null) head.Previous = entry;
        entry.Next = head;
        entry.Previous = null;
        _heads[entry.Sector.Layer][Index(entry.Sector)] = entry;
        entry.IsRegistered = true;
    }

    private void Unlink(Entry entry)
    {
        if (entry.Previous is not null) entry.Previous.Next = entry.Next;
        if (entry.Next is not null) entry.Next.Previous = entry.Previous;
        if (ReferenceEquals(_heads[entry.Sector.Layer][Index(entry.Sector)], entry))
            _heads[entry.Sector.Layer][Index(entry.Sector)] = entry.Next;
    }

    private void RequireRegistered(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!ReferenceEquals(entry.Map, this) || !entry.IsRegistered)
            throw new ArgumentException("Entry is not registered in this MapWho.", nameof(entry));
    }

    private static int Index(RetailMapWhoSector sector) => sector.Y * (4 << sector.Layer) + sector.X;
    private static bool InBounds(RetailMapWhoSector sector) => (uint)sector.Layer <= 4 &&
        (uint)sector.X < (4u << sector.Layer) && (uint)sector.Y < (4u << sector.Layer);
}
