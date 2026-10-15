// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailCareerNode"/> and
/// <see cref="RetailCareerNodeTable"/> against
/// <c>references/Onslaught/Career.cpp:86-154</c>, <c>:307-320</c> and
/// <c>Career.h:76-100</c>, and the pristine <c>74154bfa…</c> bytes at
/// <c>0x0041B740</c>, <c>0x0041B770</c>, <c>0x0041B8F0</c> and
/// <c>0x0041BB77</c>.
/// </summary>
public sealed class RetailCareerNodesTests
{
    // Pins every store 0x0041B740-0x0041B768 makes, by field. The two links are
    // -1 and not 0: a rebuild that memset the record to zero would produce a
    // node whose links point at node 0, which is a real graph edge. Nine words
    // of 0xFFFFFFFF is BASE_THINGS_EXISTS_MEM_REQ, and it is the count that
    // makes the record 0x40 bytes - the stride GetNodeFromWorldNo walks.
    [Fact]
    public void Blank_WritesTheExactBlankedRecord()
    {
        var node = new RetailCareerNode();

        Assert.Equal(-1, node.LowerLink);
        Assert.Equal(-1, node.HigherLink);
        Assert.Equal(0, node.WorldNumber);
        Assert.Equal(0, node.Complete);
        Assert.Equal(0, node.NumAttempts);
        Assert.Equal(
            0xBF800000u, BitConverter.SingleToUInt32Bits(node.Ranking));

        Assert.Equal(9, RetailCareerNode.BaseThingsExistsWords);
        Assert.Equal(288, RetailCareerNode.BaseThingsExistsSize);
        Assert.Equal(0x40, RetailCareerNode.RecordSizeInBytes);
        Assert.Equal(9, node.BaseThingsExistsWordsView.Count);
        for (int word = 0; word < node.BaseThingsExistsWordsView.Count; word++)
        {
            Assert.Equal(
                unchecked((int)0xFFFFFFFF), node.BaseThingsExistsWordsView[word]);
        }
    }

    // 0x0041B740-0x0041B769 contains no store to +0x00, so mIsStartOfNewIsland
    // survives a Blank. A rebuild that "tidied up" by zeroing the whole record
    // would fail here - and would also silently change the save's byte image.
    [Fact]
    public void Blank_LeavesTheDeadIslandFlagAlone()
    {
        var node = new RetailCareerNode { IsStartOfNewIsland = 0x5A5A5A5A };

        node.Blank();

        Assert.Equal(0x5A5A5A5A, node.IsStartOfNewIsland);
    }

    // THE BOOL-literal test. 0x0041B788 is `cmp dword ptr [esp+0xC], 1`, so the
    // set arm needs exactly 1. Every other value - including the 2 and -1 a
    // sloppy BOOL can hold, and END_LEVEL_DATA.mBaseThingsLeft is forwarded
    // straight in at Career.cpp:526 - lands on the clear arm.
    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    public void SetBaseThingExistTo_SetsOnlyForLiteralOne(int value, bool expectSet)
    {
        var node = new RetailCareerNode();
        node.SetBaseThingExistTo(0, 0);
        Assert.Equal(0, node.DoesBaseThingExist(0));

        node.SetBaseThingExistTo(0, value);

        Assert.Equal(expectSet ? 1 : 0, node.DoesBaseThingExist(0));
        Assert.Equal(expectSet ? 1 : 0, node.BaseThingsExistsWordsView[0] & 1);
    }

    // Pins the shared mask law (0x0041B77C) and the word split (sar by 5).
    // Offset 31 must land on the sign bit of word 0, offset 32 on bit 0 of word
    // 1, offset 287 on the top bit of word 8 - the last bit the 288-bit map
    // holds. An implementation that used `1 << (offset % 32)` on a signed int
    // without the `and 31` would agree here; one that shifted by the whole
    // offset would not.
    [Theory]
    [InlineData(0, 0, 0x00000001u)]
    [InlineData(31, 0, 0x80000000u)]
    [InlineData(32, 1, 0x00000001u)]
    [InlineData(64, 2, 0x00000001u)]
    [InlineData(287, 8, 0x80000000u)]
    public void SetBaseThingExistTo_AddressesTheWordAndBitRetailDoes(
        int offset, int expectedWord, uint expectedMask)
    {
        var node = new RetailCareerNode();
        for (int bit = 0; bit < RetailCareerNode.BaseThingsExistsSize; bit++)
        {
            node.SetBaseThingExistTo(bit, 0);
        }

        node.SetBaseThingExistTo(offset, 1);

        for (int word = 0; word < RetailCareerNode.BaseThingsExistsWords; word++)
        {
            uint expected = word == expectedWord ? expectedMask : 0u;
            Assert.Equal(expected, unchecked((uint)node.BaseThingsExistsWordsView[word]));
        }

        Assert.Equal(1, node.DoesBaseThingExist(offset));
    }

    // Retail has no guard on either accessor - 0x0041B770 starts at the shift -
    // so offset 288 writes into mNumAttempts and offset 300 into the next node.
    // Core cannot honestly reproduce a buffer overrun, so it refuses. This test
    // pins where the honest boundary is, and that the last in-range bit really
    // is 287.
    [Theory]
    [InlineData(288)]
    [InlineData(300)]
    [InlineData(-1)]
    public void BaseThingAccessors_RefuseWhereRetailWouldOverrun(int offset)
    {
        var node = new RetailCareerNode();

        Assert.Throws<ArgumentOutOfRangeException>(() => node.SetBaseThingExistTo(offset, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => node.DoesBaseThingExist(offset));
        Assert.Equal(1, node.DoesBaseThingExist(287));
    }

    // GetNodeFromWorldNo (0x0041B8F0) is a first-match linear scan bounded by
    // num_nodes, which the pristine image ships as 0x2B = 43 = NUM_LEVELS, not
    // MAX_NODES. Duplicates therefore resolve to the lowest index; a rebuild
    // backed by a dictionary would resolve to the last insert.
    [Fact]
    public void NodeTable_ResolvesTheFirstMatchingWorldNumber()
    {
        Assert.Equal(43, RetailCareerNodeTable.ShippedNodeCount);
        Assert.Equal(100, RetailCareerNodeTable.MaxNodes);

        var table = new RetailCareerNodeTable();
        table.Add(worldNumber: 110, complete: 0);
        table.Add(worldNumber: 231, complete: 1);
        table.Add(worldNumber: 110, complete: 1);

        Assert.Equal(3, table.NodeCount);
        Assert.Same(table.Nodes[0], table.Find(110));
        Assert.Equal(0, table.CompleteFlagOf(110));
        Assert.Equal(1, table.CompleteFlagOf(231));
    }

    // Two different misses, kept apart because retail keeps them apart.
    // CCareer::DoesBaseThingExist checks the pointer and answers TRUE for an
    // unknown world (0x0041BB75, Career.cpp:319-320); IsEpisodeAvailable does
    // not check and executes `cmp dword ptr [eax+4]` against the zero
    // 0x0041B924 just returned. Find models the first, CompleteFlagOf the
    // second.
    [Fact]
    public void NodeTable_MissReturnsNullAndFaultsOnlyWhereRetailFaults()
    {
        var table = new RetailCareerNodeTable();
        table.Add(worldNumber: 110, complete: 1);

        Assert.Null(table.Find(999));
        Assert.Throws<InvalidOperationException>(() => table.CompleteFlagOf(999));
    }
}
