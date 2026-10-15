// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailCareerGrade"/>,
/// <see cref="RetailCareerSlots"/>, <see cref="RetailCareerCounters"/> and
/// <see cref="RetailCareerEpisodes"/> against
/// <c>references/Onslaught/Career.cpp:538-557</c>, <c>:1178-1202</c>,
/// <c>:1357-1442</c>, and the pristine <c>74154bfa…</c> bytes at
/// <c>0x0041C180</c>, <c>0x00421470</c>, <c>0x004214E0</c>, <c>0x00421550</c>,
/// <c>0x00421560</c> and <c>0x00421570</c>.
/// </summary>
public sealed class RetailCareerProgressTests
{
    // The whole ladder, at the quarter boundaries floorf(f*4) actually turns.
    // These are the values a career editor shows the player, so an off-by-one
    // on any boundary is user-visible. 0.25 must be C and not D: the floor
    // makes each band closed at its bottom edge.
    [Theory]
    [InlineData(-1.0f, (byte)'E')]
    [InlineData(0.0f, (byte)'E')]
    [InlineData(0.0001f, (byte)'D')]
    [InlineData(0.24f, (byte)'D')]
    [InlineData(0.25f, (byte)'C')]
    [InlineData(0.4999f, (byte)'C')]
    [InlineData(0.5f, (byte)'B')]
    [InlineData(0.7499f, (byte)'B')]
    [InlineData(0.75f, (byte)'A')]
    [InlineData(0.9999f, (byte)'A')]
    [InlineData(1.0f, (byte)'S')]
    public void GradeFromRanking_WalksTheShippedLadder(float ranking, byte expected)
    {
        Assert.Equal(expected, RetailCareerGrade.GradeByteFromRanking(ranking));
        Assert.Equal((char)expected, RetailCareerGrade.GradeFromRanking(ranking));
    }

    // The source comment at Career.cpp:1180 claims "A - F". No arm can emit
    // 'F': 'D' minus a non-negative floor only ever counts downwards from 'D',
    // and the two arms above it are 'S' and 'E'. Pinned so the claim cannot
    // creep back in as a sixth band.
    [Fact]
    public void GradeFromRanking_NeverEmitsF()
    {
        for (int step = 0; step <= 10_000; step++)
        {
            float ranking = step / 10_000.0f;
            byte grade = RetailCareerGrade.GradeByteFromRanking(ranking);
            Assert.NotEqual((byte)'F', grade);
            Assert.Contains(grade, new[] { (byte)'S', (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E' });
        }
    }

    // THE divergence test. Career.cpp:1183 is `if (f == 1.f)`, which C says is
    // false for a NaN. 0x00421474 compiles it to fcomp / test ah, 0x40 - C3
    // only - and an unordered compare sets C3, C2 and C0 together, so ah is
    // 0x45, the mask hits, and a NaN ranking is graded S. The second assertion
    // is the source text, and it disagrees.
    [Fact]
    public void GradeFromRanking_GradesNaNAsPerfectBecauseTheCompiledTestReadsC3Only()
    {
        Assert.Equal((byte)'S', RetailCareerGrade.GradeByteFromRanking(float.NaN));

        // The C text this was compiled from.
        Assert.False(float.NaN == 1.0f);
        Assert.False(float.NaN <= 0.0f);
    }

    // Above 1.0 the ladder keeps counting down past 'A' into punctuation, and
    // the subtraction is eight bits wide (mov al, 0x44 / sub al, cl at
    // 0x004214BA), so it wraps rather than saturating. 17.25 drives the byte to
    // 0xFF - past ASCII, which is exactly why GradeByteFromRanking is the
    // pinned surface and the WCHAR widening is not claimed there.
    [Theory]
    [InlineData(1.25f, (byte)0x3F)]
    [InlineData(1.5f, (byte)0x3E)]
    [InlineData(2.0f, (byte)0x3C)]
    [InlineData(17.25f, (byte)0xFF)]
    public void GradeFromRanking_WrapsInEightBitsAboveTheLadder(float ranking, byte expected) =>
        Assert.Equal(expected, RetailCareerGrade.GradeByteFromRanking(ranking));

    // The 256-slot guard (cmp eax, 0x100 at 0x004214EB) against a 32-word
    // store. Slot 255 is the last one the game can reach; slot 256 exists in
    // the save and is unreachable, so an editor that writes it changes nothing.
    // Words 8..31 must stay zero after every legal write.
    [Fact]
    public void Slots_GuardStopsAt256WhileTheStoreHolds1024()
    {
        Assert.Equal(32, RetailCareerSlots.SlotWords);
        Assert.Equal(256, RetailCareerSlots.AddressableSlotCount);
        Assert.Equal(1024, RetailCareerSlots.StoredSlotCount);

        var slots = new RetailCareerSlots();
        for (int slot = 0; slot < RetailCareerSlots.StoredSlotCount; slot++)
        {
            slots.SetSlot(slot, 1);
        }

        for (int word = 0; word < 8; word++)
        {
            Assert.Equal(0xFFFFFFFFu, unchecked((uint)slots.Words[word]));
        }

        for (int word = 8; word < RetailCareerSlots.SlotWords; word++)
        {
            Assert.Equal(0u, unchecked((uint)slots.Words[word]));
        }

        Assert.Equal(1, slots.GetSlot(255));
        Assert.Equal(0, slots.GetSlot(256));
        Assert.Equal(0, slots.GetSlot(-1));
    }

    // Exact bit placement, and the literal-1 rule again (cmp [esp+0xC], 1 at
    // 0x00421505). Slot 32 must move word 1, not word 0 bit 0.
    [Theory]
    [InlineData(0, 0, 0x00000001u)]
    [InlineData(31, 0, 0x80000000u)]
    [InlineData(32, 1, 0x00000001u)]
    [InlineData(255, 7, 0x80000000u)]
    public void Slots_PlaceTheBitRetailPlaces(int slot, int expectedWord, uint expectedMask)
    {
        var slots = new RetailCareerSlots();

        slots.SetSlot(slot, 1);

        for (int word = 0; word < RetailCareerSlots.SlotWords; word++)
        {
            uint expected = word == expectedWord ? expectedMask : 0u;
            Assert.Equal(expected, unchecked((uint)slots.Words[word]));
        }

        // A BOOL of 2 clears, it does not set.
        slots.SetSlot(slot, 2);
        Assert.Equal(0, slots.GetSlot(slot));
        Assert.Equal(0u, unchecked((uint)slots.Words[expectedWord]));
    }

    // Both goodie readers are three instructions: load, store zero, return. The
    // second read in the same frame must be zero, which is what makes them
    // latches and why nothing may poll them.
    [Fact]
    public void GoodieLatches_ClearOnRead()
    {
        var counters = new RetailCareerCounters { NewGoodieCount = 7, FirstGoodie = 1 };

        Assert.Equal(7, counters.GetAndResetGoodieNewCount());
        Assert.Equal(0, counters.GetAndResetGoodieNewCount());
        Assert.Equal(0, counters.NewGoodieCount);

        Assert.Equal(1, counters.GetAndResetFirstGoodie());
        Assert.Equal(0, counters.GetAndResetFirstGoodie());
        Assert.Equal(0, counters.FirstGoodie);
    }

    // 0x0041C188 is `cmp eax, 0x64` / je: an equality test, not a range. World
    // 100 scores nothing; worlds 99 and 101 score normally. A rebuild that
    // exempted "the tutorial episode" rather than that one world number would
    // lose kills on every neighbouring level.
    [Theory]
    [InlineData(100, 0)]
    [InlineData(99, 3)]
    [InlineData(101, 3)]
    [InlineData(110, 3)]
    public void UpdateThingsKilled_ExemptsExactlyWorld100(int worldFinished, int expectedFirst)
    {
        var counters = new RetailCareerCounters();

        counters.UpdateThingsKilled(worldFinished, new[] { 3, 5, 7, 11, 13 });

        Assert.Equal(expectedFirst, counters.KilledThings[0]);
        Assert.Equal(worldFinished == 100 ? 0 : 13, counters.KilledThings[4]);
    }

    // Five types (cmp esi, 5 at 0x0041C230), accumulated across levels with a
    // plain 32-bit add that wraps. The wrap is pinned because retail neither
    // saturates nor widens: the log line masks to 24 bits at 0x0041C211 but the
    // stored counter at 0x0041C1A9 does not.
    [Fact]
    public void UpdateThingsKilled_AccumulatesFiveTypesAndWrapsAtThirtyTwoBits()
    {
        Assert.Equal(5, RetailCareerCounters.KilledTypeCount);

        var counters = new RetailCareerCounters();
        counters.UpdateThingsKilled(200, new[] { 1, 2, 3, 4, 5 });
        counters.UpdateThingsKilled(300, new[] { 10, 20, 30, 40, 50 });

        Assert.Equal(new[] { 11, 22, 33, 44, 55 }, counters.KilledThings.ToArray());

        var wrapping = new RetailCareerCounters();
        wrapping.UpdateThingsKilled(200, new[] { int.MaxValue, 0, 0, 0, 0 });
        wrapping.UpdateThingsKilled(200, new[] { 1, 0, 0, 0, 0 });

        Assert.Equal(int.MinValue, wrapping.KilledThings[0]);
        Assert.Equal(0x00000000, wrapping.KilledThings[0] & 0x00FFFFFF);
    }

    // The full episode table, read out of the compiled arms rather than the
    // header: 0x6E; 0xE7/0xE8; 0x14B/0x14C; 0x1AF/0x1B0; 0x209-0x20C;
    // 0x26D/0x26E; 0x2E5/0x2E6. Order matters because evaluation
    // short-circuits.
    [Fact]
    public void Episodes_TestExactlyTheShippedWorldNumbersInOrder()
    {
        Assert.Empty(RetailCareerEpisodes.QualifyingWorlds(0));
        Assert.Empty(RetailCareerEpisodes.QualifyingWorlds(1));
        Assert.Equal(new[] { 110 }, RetailCareerEpisodes.QualifyingWorlds(2).ToArray());
        Assert.Equal(new[] { 231, 232 }, RetailCareerEpisodes.QualifyingWorlds(3).ToArray());
        Assert.Equal(new[] { 331, 332 }, RetailCareerEpisodes.QualifyingWorlds(4).ToArray());
        Assert.Equal(new[] { 431, 432 }, RetailCareerEpisodes.QualifyingWorlds(5).ToArray());
        Assert.Equal(new[] { 521, 522, 523, 524 }, RetailCareerEpisodes.QualifyingWorlds(6).ToArray());
        Assert.Equal(new[] { 621, 622 }, RetailCareerEpisodes.QualifyingWorlds(7).ToArray());
        Assert.Equal(new[] { 741, 742 }, RetailCareerEpisodes.QualifyingWorlds(8).ToArray());
        Assert.Empty(RetailCareerEpisodes.QualifyingWorlds(9));
    }

    // 0x00421585 is a shared stub reached by table entries 0 and 1: TRUE with
    // no node lookup at all, which an empty table proves. 0x00421575 is
    // `cmp eax, 8` / ja - unsigned - so -1 lands on the default arm instead of
    // indexing the jump table backwards.
    [Fact]
    public void Episodes_ZeroAndOneAreUnconditionalAndTheBoundIsUnsigned()
    {
        var empty = new RetailCareerNodeTable();

        Assert.True(RetailCareerEpisodes.IsEpisodeAvailable(0, empty));
        Assert.True(RetailCareerEpisodes.IsEpisodeAvailable(1, empty));
        Assert.False(RetailCareerEpisodes.IsEpisodeAvailable(9, empty));
        Assert.False(RetailCareerEpisodes.IsEpisodeAvailable(-1, empty));
        Assert.False(RetailCareerEpisodes.IsEpisodeAvailable(int.MinValue, empty));
    }

    // mComplete is compared against literal 1 (`cmp edx, esi` with esi = 1 at
    // 0x00421632, and sete against 1 at 0x004215DE), so a node flagged 2 does
    // not unlock. And any one qualifying world is enough.
    [Fact]
    public void Episodes_UnlockOnLiteralOneFromAnyQualifyingWorld()
    {
        var table = new RetailCareerNodeTable();
        table.Add(110, complete: 0);
        table.Add(231, complete: 0);
        table.Add(232, complete: 2);

        Assert.False(RetailCareerEpisodes.IsEpisodeAvailable(2, table));
        Assert.False(RetailCareerEpisodes.IsEpisodeAvailable(3, table));

        table.Nodes[2].Complete = 1;
        Assert.True(RetailCareerEpisodes.IsEpisodeAvailable(3, table));

        table.Nodes[0].Complete = 1;
        Assert.True(RetailCareerEpisodes.IsEpisodeAvailable(2, table));
    }

    // Short-circuit is observable because the miss is fatal. With 231 complete
    // retail returns before it ever looks 232 up, so a table missing 232
    // survives; flip 231 to incomplete and the same table faults. A rebuild
    // that gathered all the flags first and then OR-ed them would fault in both
    // cases.
    [Fact]
    public void Episodes_ShortCircuitBeforeTheLookupThatWouldFault()
    {
        var table = new RetailCareerNodeTable();
        table.Add(231, complete: 1);

        Assert.True(RetailCareerEpisodes.IsEpisodeAvailable(3, table));

        table.Nodes[0].Complete = 0;
        Assert.Throws<InvalidOperationException>(
            () => RetailCareerEpisodes.IsEpisodeAvailable(3, table));
    }
}
