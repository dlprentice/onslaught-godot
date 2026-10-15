// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Containers;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class SPtrSetTests
{
    private static SPtrSetPool Pool(int size = 8)
    {
        var p = new SPtrSetPool();
        p.Init(size);
        return p;
    }

    [Fact]
    public void AddPrependsAndAppendAppends()
    {
        var s = new SPtrSet<string>(Pool());
        s.Append("b");
        s.Add("a");
        s.Append("c");
        Assert.Equal(["a", "b", "c"], s.ToList());
        Assert.Equal("c", s.Last());
        Assert.Equal("b", s.At(1));
    }

    [Fact]
    public void RemovingTheCurrentNodeSendsTheIterationIntoTheFreeList()
    {
        // Retail's Remove pushes the node onto the free list and rewrites its link, so a walk that removed the item it
        // stood on continues with whatever the free list holds next: here the pool's unused block nodes, whose items
        // are null, so the walk ends early and "c" is never visited.
        SPtrSetPool pool = Pool();
        var s = new SPtrSet<string>(pool);
        s.Append("a");
        s.Append("b");
        s.Append("c");
        var seen = new List<string>();
        for (string? item = s.First(); item is not null; item = s.Next())
        {
            seen.Add(item);
            if (item == "b")
            {
                s.Remove(item);
            }
        }

        Assert.Equal(["a", "b"], seen);
        Assert.Equal(["a", "c"], s.ToList());
    }

    [Fact]
    public void FreedNodesAreReusedLastInFirstOut()
    {
        SPtrSetPool pool = Pool(2);
        var s = new SPtrSet<string>(pool);
        s.Append("a");
        s.Append("b");
        s.Append("c"); // the block is used up: a node allocated singly
        Assert.Equal(1, pool.DynamicNodes);
        s.RemoveAll();
        var t = new SPtrSet<string>(pool);
        t.Append("x");
        Assert.Equal(["x"], t.ToList());
        Assert.Equal(1, pool.DynamicNodes);
    }
}
