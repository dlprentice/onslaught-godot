// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/SPtrSet.h and src/SPtrSet.cpp (GenericSPtrSet: Init 004e59f0, Add 004e5a80, Append
// 004e5b20, Remove 004e5bd0, Contains 004e5c30, RemoveAll 004e5c60, At 004e5c90, Shutdown 004e5910,
// ClearAnyDynamicCreatedNodes 004e5990; GenericListIterator), in Stuart's release.

namespace OnslaughtRebuild.Parity.Containers;

/// <summary>
/// The node pool every GenericSPtrSet shares: a block of nodes made at start-up (CSystem::Init asks for 40,000) chained
/// into one free list, plus nodes allocated one at a time when the block runs out. A removed node goes back to the head
/// of the free list with its next link rewritten, so an iteration that had reached it continues into the free list,
/// as retail's does. Nodes are indices; each keeps its item and next link exactly as retail's node does.
/// </summary>
public sealed class SPtrSetPool
{
    /// <summary>The block size CSystem::Init requests.</summary>
    public const int RetailBlockSize = 40000;

    internal const int Null = -1;

    internal object?[] Items = [];
    internal int[] Next = [];
    private int _count;

    /// <summary>mFreeList: the first free node, or -1.</summary>
    internal int FreeList = Null;

    /// <summary>mBlockSize: nodes [0, BlockSize) are the block; later indices were allocated singly.</summary>
    public int BlockSize { get; private set; }

    /// <summary>Whether Init has run (mBlock != NULL).</summary>
    public bool Initialised { get; private set; }

    /// <summary>Nodes allocated singly because the free list was empty (retail logs a warning every 20th).</summary>
    public int DynamicNodes { get; private set; }

    /// <summary>GenericSPtrSet::Init: the block, chained in index order.</summary>
    public void Init(int size)
    {
        if (Initialised)
        {
            return; // retail logs "Warning: Initilise SptrSet twice"
        }

        Items = new object?[size];
        Next = new int[size];
        _count = size;
        BlockSize = size;
        for (int i = 0; i <= size - 2; i++)
        {
            Next[i] = i + 1;
        }

        Next[size - 1] = Null;
        FreeList = 0;
        Initialised = true;
    }

    /// <summary>GenericSPtrSet::Shutdown: the free list and the block are released.</summary>
    public void Shutdown()
    {
        Items = [];
        Next = [];
        _count = 0;
        BlockSize = 0;
        FreeList = Null;
        Initialised = false;
    }

    /// <summary>GenericSPtrSet::ClearAnyDynamicCreatedNodes: singly allocated nodes leave the free list.</summary>
    public void ClearAnyDynamicCreatedNodes()
    {
        int n = FreeList;
        int prev = Null;
        while (n != Null)
        {
            int node = n;
            n = Next[n];
            if (node >= BlockSize)
            {
                if (prev != Null)
                {
                    Next[prev] = n;
                }
                else
                {
                    FreeList = n;
                }
            }
            else
            {
                prev = node;
            }
        }
    }

    /// <summary>A node from the free list, or a new one (Add and Append).</summary>
    internal int Take()
    {
        if (!Initialised)
        {
            throw new InvalidOperationException("FATAL ERROR: SptSet::Add when freelist has not been setup");
        }

        int node = FreeList;
        if (node != Null)
        {
            FreeList = Next[node];
            return node;
        }

        if (_count == Items.Length)
        {
            Array.Resize(ref Items, Math.Max(16, Items.Length * 2));
            Array.Resize(ref Next, Items.Length);
        }

        DynamicNodes++;
        return _count++;
    }
}

/// <summary>
/// GenericSPtrSet / SPtrSet&lt;T&gt;: a singly linked list over the shared pool, with its own iterator (First/Next) and
/// separate ListIterator cursors. Add puts an item first, Append last; Remove takes out the first node holding the item
/// and returns it to the pool.
/// </summary>
public sealed class SPtrSet<T>(SPtrSetPool pool)
    where T : class
{
    private int _first = SPtrSetPool.Null;
    private int _last = SPtrSetPool.Null;
    private int _iterator = SPtrSetPool.Null;

    public SPtrSetPool Pool { get; } = pool;

    public int Size { get; private set; }

    internal int FirstNode => _first;

    /// <summary>Add: the item at the start of the list.</summary>
    public void Add(T item)
    {
        int node = Pool.Take();
        Pool.Next[node] = _first;
        Pool.Items[node] = item;
        _first = node;
        Size++;
        if (Size == 1)
        {
            _last = _first;
        }
    }

    /// <summary>Append: the item at the end of the list.</summary>
    public void Append(T item)
    {
        int node = Pool.Take();
        Pool.Next[node] = SPtrSetPool.Null;
        Pool.Items[node] = item;
        if (_last != SPtrSetPool.Null)
        {
            Pool.Next[_last] = node;
        }

        _last = node;
        Size++;
        if (Size == 1)
        {
            _first = _last;
        }
    }

    /// <summary>Remove: the first node holding the item goes to the head of the free list.</summary>
    public void Remove(T item)
    {
        int prev = SPtrSetPool.Null;
        int node = _first;
        while (node != SPtrSetPool.Null)
        {
            if (ReferenceEquals(Pool.Items[node], item))
            {
                if (_first == node)
                {
                    _first = Pool.Next[node];
                    if (_first == SPtrSetPool.Null)
                    {
                        _last = SPtrSetPool.Null;
                    }
                }
                else
                {
                    Pool.Next[prev] = Pool.Next[node];
                    if (Pool.Next[prev] == SPtrSetPool.Null)
                    {
                        _last = prev;
                    }
                }

                Pool.Next[node] = Pool.FreeList;
                Pool.FreeList = node;
                Size--;
                return;
            }

            prev = node;
            node = Pool.Next[node];
        }
    }

    /// <summary>RemoveAll: the whole chain onto the free list in one splice.</summary>
    public void RemoveAll()
    {
        if (Size == 0)
        {
            return;
        }

        if (_last != SPtrSetPool.Null)
        {
            Pool.Next[_last] = Pool.FreeList;
        }

        Pool.FreeList = _first;
        _first = SPtrSetPool.Null;
        _last = SPtrSetPool.Null;
        Size = 0;
    }

    public bool Contains(T item)
    {
        for (int n = _first; n != SPtrSetPool.Null; n = Pool.Next[n])
        {
            if (ReferenceEquals(Pool.Items[n], item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>At: the item <paramref name="index"/> nodes in (retail reads past the end without checking).</summary>
    public T? At(int index)
    {
        int current = _first;
        while (current != SPtrSetPool.Null && index > 0)
        {
            current = Pool.Next[current];
            index--;
        }

        return current == SPtrSetPool.Null
            ? throw new InvalidOperationException("SPtrSet::At past the end (retail dereferences NULL)")
            : (T?)Pool.Items[current];
    }

    /// <summary>The list's own iterator: First.</summary>
    public T? First()
    {
        _iterator = _first;
        return _iterator == SPtrSetPool.Null ? null : (T?)Pool.Items[_iterator];
    }

    /// <summary>The list's own iterator: Next (follows the node's link, even if the node was removed meanwhile).</summary>
    public T? Next()
    {
        _iterator = Pool.Next[_iterator];
        return _iterator == SPtrSetPool.Null ? null : (T?)Pool.Items[_iterator];
    }

    public T? Last() => _last == SPtrSetPool.Null ? throw new InvalidOperationException("Last of an empty set") : (T?)Pool.Items[_last];

    /// <summary>The items in list order (a snapshot, for code that does not modify the list while walking it).</summary>
    public List<T> ToList()
    {
        var items = new List<T>(Size);
        for (int n = _first; n != SPtrSetPool.Null; n = Pool.Next[n])
        {
            items.Add((T)Pool.Items[n]!);
        }

        return items;
    }
}

/// <summary>ListIterator&lt;T&gt;: a separate cursor over a set, with retail's First/Next semantics.</summary>
public struct ListIterator<T>(SPtrSet<T> list)
    where T : class
{
    private readonly SPtrSet<T> _list = list;
    private int _current = SPtrSetPool.Null;

    public T? First()
    {
        _current = _list.FirstNode;
        return _current == SPtrSetPool.Null ? null : (T?)_list.Pool.Items[_current];
    }

    public T? Next()
    {
        _current = _list.Pool.Next[_current];
        return _current == SPtrSetPool.Null ? null : (T?)_list.Pool.Items[_current];
    }
}
