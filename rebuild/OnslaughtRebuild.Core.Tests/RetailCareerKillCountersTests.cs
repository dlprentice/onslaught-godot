// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailCareerKillCounters"/> against
/// <c>references/Onslaught/Career.cpp:531-534</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x0041C160</c>, <c>0x004218F0</c>,
/// <c>0x00421900</c>, <c>0x00421910</c>, <c>0x00421940</c> and
/// <c>0x00421245-0x004212AF</c>.
/// </summary>
public sealed class RetailCareerKillCountersTests
{
    [Fact]
    public void Constants_MatchTheInstructionImmediates()
    {
        Assert.Equal(0x00FFFFFF, RetailCareerKillCounters.KillCountMask);
        Assert.Equal(0x80, RetailCareerKillCounters.ScreenPositionBias);
        Assert.Equal(0x40, RetailCareerKillCounters.ScreenPositionLimit);
        Assert.Equal(5, RetailCareerKillCounters.KilledTypeCount);
    }

    // The whole divergence in one row: the source returns the int, retail keeps
    // 24 bits. A rebuild written from Career.cpp:533 reports 2129920045 here.
    [Fact]
    public void GetNumKilled_KeepsOnlyTheLowTwentyFourBits()
    {
        var counters = new RetailCareerKillCounters();
        counters.SetWord(0, unchecked((int)0x7EF2DEAD));

        Assert.Equal(0x00F2DEAD, counters.GetNumKilled(0));
        Assert.Equal(unchecked((int)0x7EF2DEAD), counters.Words[0]);
    }

    [Fact]
    public void GetNumKilled_IsUnaffectedByTheSignBit()
    {
        var counters = new RetailCareerKillCounters();
        counters.SetWord(1, unchecked((int)0xFF000001));

        Assert.Equal(1, counters.GetNumKilled(1));
    }

    // shr, not sar. An arithmetic shift would answer -256 for the first row and
    // -192 for the third.
    [Theory]
    [InlineData(0x00000000u, -128)]
    [InlineData(0x80000000u, 0)]
    [InlineData(0xC0000000u, 64)]
    [InlineData(0x40000000u, -64)]
    [InlineData(0x7F000000u, -1)]
    [InlineData(0xFF000000u, 127)]
    public void UnpackScreenPosition_IsALogicalShiftThenTheBias(uint word, int expected) =>
        Assert.Equal(expected, RetailCareerKillCounters.UnpackScreenPosition(unchecked((int)word)));

    // The low 24 bits are carried across untouched, and the packed byte survives
    // a round trip over the whole accepted range. Note that the SIGN of the bias
    // is not observable and cannot be tested: `(v + 0x80) << 24` and
    // `(v - 0x80) << 24` differ by 0x100000000, which shifts clean out of a
    // 32-bit word, so MSVC's choice of the sign-extended -0x80 encoding is an
    // exact equivalence rather than a fact a rebuild could get wrong.
    [Theory]
    [InlineData(-64)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(64)]
    public void PackScreenPosition_RoundTripsAndPreservesTheCount(int screenPosition)
    {
        const int count = 0x00ABCDEF;
        int word = RetailCareerKillCounters.PackScreenPosition(count, screenPosition);

        Assert.Equal(screenPosition, RetailCareerKillCounters.UnpackScreenPosition(word));
        Assert.Equal(count, RetailCareerKillCounters.MaskKillCount(word));
    }

    // A zeroed word carries an offset of -128, which Load refuses, so a blank
    // career loads with a centred screen and the word 0x80000000.
    [Fact]
    public void NormaliseWordOnLoad_TurnsAZeroedWordIntoACentredScreen()
    {
        Assert.Equal(-128, RetailCareerKillCounters.UnpackScreenPosition(0));
        Assert.Equal(
            unchecked((int)0x80000000),
            RetailCareerKillCounters.NormaliseWordOnLoad(0));
        Assert.Equal(0, RetailCareerKillCounters.UnpackScreenPosition(
            RetailCareerKillCounters.NormaliseWordOnLoad(0)));
    }

    // Out of range is zero, not the nearest limit. A saturating rebuild would
    // answer 64 and -64 on the middle two rows.
    [Theory]
    [InlineData(64, 64)]
    [InlineData(-64, -64)]
    [InlineData(65, 0)]
    [InlineData(-65, 0)]
    [InlineData(127, 0)]
    [InlineData(-128, 0)]
    [InlineData(0, 0)]
    public void ClampScreenPositionOnLoad_ZeroesAnythingOutsideTheLimit(int raw, int expected) =>
        Assert.Equal(expected, RetailCareerKillCounters.ClampScreenPositionOnLoad(raw));

    [Fact]
    public void NormaliseWordOnLoad_KeepsTheCountWhileZeroingABadOffset()
    {
        int word = RetailCareerKillCounters.PackScreenPosition(0x00001234, 100);
        int loaded = RetailCareerKillCounters.NormaliseWordOnLoad(word);

        Assert.Equal(0x00001234, RetailCareerKillCounters.MaskKillCount(loaded));
        Assert.Equal(0, RetailCareerKillCounters.UnpackScreenPosition(loaded));
    }

    // An in-range career is a fixed point of load-and-resave.
    [Fact]
    public void NormaliseWordOnLoad_IsIdempotentOnAnAcceptedOffset()
    {
        int word = RetailCareerKillCounters.PackScreenPosition(0x00000539, -12);

        Assert.Equal(word, RetailCareerKillCounters.NormaliseWordOnLoad(word));
        Assert.Equal(
            word,
            RetailCareerKillCounters.NormaliseWordOnLoad(
                RetailCareerKillCounters.NormaliseWordOnLoad(word)));
    }

    // Load touches words 0 and 1 only; the other three keep whatever their top
    // bytes hold.
    [Fact]
    public void NormaliseOnLoad_LeavesTheOtherThreeCountersAlone()
    {
        var counters = new RetailCareerKillCounters();
        for (int type = 0; type < RetailCareerKillCounters.KilledTypeCount; type++)
        {
            counters.SetWord(type, unchecked((int)0xFF000042));
        }

        // 127 is outside +-0x40, so Load zeroes the offset on the two words it
        // owns and leaves the other three words untouched, packed byte included.
        Assert.Equal(127, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[2]));

        counters.NormaliseOnLoad();

        Assert.Equal(0, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[0]));
        Assert.Equal(0, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[1]));
        Assert.Equal(0x42, counters.GetNumKilled(0));
        Assert.Equal(0x42, counters.GetNumKilled(1));
        Assert.Equal(unchecked((int)0xFF000042), counters.Words[2]);
        Assert.Equal(unchecked((int)0xFF000042), counters.Words[3]);
        Assert.Equal(unchecked((int)0xFF000042), counters.Words[4]);
    }

    // The accumulator stores an unmasked sum, so a carry out of bit 23 walks the
    // player's screen sideways. This is the shipped bug, pinned.
    [Fact]
    public void AddKills_CarriesIntoThePackedScreenPosition()
    {
        var counters = new RetailCareerKillCounters();
        counters.SetWord(0, RetailCareerKillCounters.PackScreenPosition(0x00FFFFFF, 16));

        Assert.Equal(16, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[0]));
        Assert.Equal(0x00FFFFFF, counters.GetNumKilled(0));

        counters.AddKills(0, 1);

        Assert.Equal(0, counters.GetNumKilled(0));
        Assert.Equal(17, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[0]));
    }

    // Below the carry the offset is untouched, which is why nobody noticed.
    [Fact]
    public void AddKills_LeavesThePackedByteAloneBelowTheCarry()
    {
        var counters = new RetailCareerKillCounters();
        counters.SetWord(0, RetailCareerKillCounters.PackScreenPosition(0x00000010, -8));

        counters.AddKills(0, 7);

        Assert.Equal(0x17, counters.GetNumKilled(0));
        Assert.Equal(-8, RetailCareerKillCounters.UnpackScreenPosition(counters.Words[0]));
    }

    // The index is not bounded: TK_HACK_AGRADES is 6 and would read past
    // mKilledThings into mSlots. Core refuses rather than modelling the overrun,
    // which is the only place this type departs from the bytes.
    [Fact]
    public void GetNumKilled_RefusesTheOutOfRangeKilledTypes()
    {
        var counters = new RetailCareerKillCounters();

        Assert.Throws<IndexOutOfRangeException>(() => counters.GetNumKilled(6));
        Assert.Throws<IndexOutOfRangeException>(() => counters.GetNumKilled(7));
    }
}
