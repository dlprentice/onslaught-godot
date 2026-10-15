// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailGrade"/>,
/// <see cref="RetailCareerRecordLayout"/> and <see cref="RetailWorldGrade"/>
/// against <c>references/Onslaught/Career.h:28-38, 138-139</c>,
/// <c>Career.cpp:640-657</c> and the pristine <c>74154bfa…</c> bytes at
/// <c>0x00420AC0</c>, <c>0x00420AF0</c> and <c>0x0041C330</c>.
/// </summary>
public sealed class RetailCareerGradesTests
{
    private static RetailGrade Of(char letter) => new RetailGrade((sbyte)letter);

    [Fact]
    public void PerfectGradeByte_IsTheLiteralTheTwoComparesUse() =>
        Assert.Equal(0x53, RetailGrade.PerfectGradeByte);

    // The first guard wins unconditionally, so a perfect grade satisfies every
    // requirement including a perfect one.
    [Theory]
    [InlineData('S')]
    [InlineData('A')]
    [InlineData('E')]
    [InlineData('\0')]
    public void IsAtLeast_APerfectGradeSatisfiesEverything(char requirement) =>
        Assert.True(Of('S').IsAtLeast(Of(requirement)));

    // The second guard is the mirror: nothing but 'S' satisfies an 'S'
    // requirement. A rebuild that swapped the two guards would answer TRUE here.
    [Theory]
    [InlineData('A')]
    [InlineData('B')]
    [InlineData('E')]
    public void IsAtLeast_NothingElseSatisfiesAPerfectRequirement(char held) =>
        Assert.False(Of(held).IsAtLeast(Of('S')));

    // Better grades are SMALLER letters, so the compiled test is `setle` and not
    // `setge`. Both directions are pinned so the comparison cannot be flipped.
    [Theory]
    [InlineData('A', 'B', true)]
    [InlineData('B', 'A', false)]
    [InlineData('A', 'A', true)]
    [InlineData('A', 'E', true)]
    [InlineData('E', 'A', false)]
    [InlineData('D', 'D', true)]
    public void IsAtLeast_OrdersTheLadderBackwards(char held, char requirement, bool expected) =>
        Assert.Equal(expected, Of(held).IsAtLeast(Of(requirement)));

    // setle is a SIGNED byte compare. A grade byte at or above 0x80 reads as
    // negative and therefore outranks every letter - which is reachable, because
    // GetGradeFromRanking subtracts without clamping. An unsigned compare would
    // give the opposite answer on both rows.
    [Fact]
    public void IsAtLeast_ComparesSigned()
    {
        var high = new RetailGrade(unchecked((sbyte)0x80));

        Assert.True(high.IsAtLeast(Of('A')));
        Assert.False(Of('A').IsAtLeast(high));

        // The reachable route to such a byte: 'D' - floor(17.25 * 4) wraps past
        // 0x7F in eight bits.
        Assert.True(RetailCareerGrade.GradeByteFromRanking(17.25f) >= 0x80);
    }

    // 0x53 is upper case only, so a stored 's' is not the sentinel - and being
    // the largest letter involved it is the worst grade there is.
    [Fact]
    public void IsAtLeast_DoesNotFoldCase()
    {
        Assert.False(Of('s').IsAtLeast(Of('S')));
        Assert.False(Of('s').IsAtLeast(Of('E')));
        Assert.True(Of('E').IsAtLeast(Of('s')));
    }

    [Theory]
    [InlineData('A', 'A', true)]
    [InlineData('A', 'B', false)]
    [InlineData('S', 'S', true)]
    public void IsExactly_IsAPlainByteCompare(char left, char right, bool expected) =>
        Assert.Equal(expected, Of(left).IsExactly(Of(right)));

    // CGrade(WCHAR) truncates to eight bits and reinterprets them as signed.
    [Fact]
    public void FromWideChar_KeepsTheLowByteAndItsSign()
    {
        Assert.Equal((sbyte)'4', RetailGrade.FromWideChar((char)0x1234).Grade);
        Assert.Equal(unchecked((sbyte)0x80), RetailGrade.FromWideChar((char)0x0080).Grade);
        Assert.Equal((sbyte)'S', RetailGrade.FromWideChar('S').Grade);
    }

    // Every array bound Career.h declares falls out of the measured
    // displacements. If any constant here is wrong the arithmetic stops closing.
    [Fact]
    public void RecordLayout_ReproducesEveryDeclaredArrayBound()
    {
        Assert.Equal(
            RetailCareerRecordLayout.NodeCount * RetailCareerRecordLayout.NodeStride,
            RetailCareerRecordLayout.LinkArrayOffset - RetailCareerRecordLayout.NodeArrayOffset);
        Assert.Equal(
            RetailCareerRecordLayout.LinkCount * RetailCareerRecordLayout.LinkStride,
            RetailCareerRecordLayout.GoodieArrayOffset - RetailCareerRecordLayout.LinkArrayOffset);
        Assert.Equal(
            RetailCareerRecordLayout.GoodieCount * 4,
            RetailCareerRecordLayout.KilledThingsOffset - RetailCareerRecordLayout.GoodieArrayOffset);
        Assert.Equal(
            RetailCareerKillCounters.KilledTypeCount * 4,
            RetailCareerRecordLayout.SlotArrayOffset - RetailCareerRecordLayout.KilledThingsOffset);

        // The absolute the GRADE scan starts from, and the one ShutDown-style
        // scans of mNode use.
        Assert.Equal(
            0x00660624u,
            RetailCareerRecordLayout.CareerSingletonAddress +
                (uint)RetailCareerRecordLayout.NodeArrayOffset);

        // mWorldNumber is at node + 0x10: the scan starts at this + 0x14.
        Assert.Equal(
            0x00660634u,
            RetailCareerRecordLayout.CareerSingletonAddress + 0x14u);
    }

    // rep movsd with ecx = 0x92F and the cursor advance of 0x24BC are the same
    // number, and it is eight more than the header accounts for.
    [Fact]
    public void RecordLayout_RecordSizeIsMeasuredTwiceAndExceedsTheHeader()
    {
        Assert.Equal(0x92F * 4, RetailCareerRecordLayout.RecordSize);
        Assert.Equal(0x24BC, RetailCareerRecordLayout.RecordSize);

        const int headerAccountedFor =
            RetailCareerRecordLayout.SlotArrayOffset + 32 * 4 // mSlots
            + 4     // mCareerInProgress
            + 4 + 4 // mSoundVolume, mMusicVolume
            + 8 + 8 + 8 + 8; // mIsGod, mInvertYAxis, mVibration, mControllerConfigurationNum

        Assert.Equal(0x24B4, headerAccountedFor);
        Assert.Equal(8, RetailCareerRecordLayout.RecordSize - headerAccountedFor);
    }

    [Fact]
    public void NodeOffset_IsNullOnlyBelowZeroAndHasNoUpperBound()
    {
        Assert.Null(RetailCareerRecordLayout.NodeOffset(-1));
        Assert.Null(RetailCareerRecordLayout.NodeOffset(int.MinValue));
        Assert.Equal(RetailCareerRecordLayout.NodeArrayOffset, RetailCareerRecordLayout.NodeOffset(0));
        Assert.Equal(0x44, RetailCareerRecordLayout.NodeOffset(1));

        // MAX_NODES is not checked, so one past the end is mNodeLink[0].
        Assert.Equal(
            RetailCareerRecordLayout.LinkArrayOffset,
            RetailCareerRecordLayout.NodeOffset(RetailCareerRecordLayout.NodeCount));
    }

    [Fact]
    public void LinkOffset_UsesTheEightByteStride()
    {
        Assert.Null(RetailCareerRecordLayout.LinkOffset(-1));
        Assert.Equal(RetailCareerRecordLayout.LinkArrayOffset, RetailCareerRecordLayout.LinkOffset(0));
        Assert.Equal(RetailCareerRecordLayout.LinkArrayOffset + 8, RetailCareerRecordLayout.LinkOffset(1));
        Assert.Equal(
            RetailCareerRecordLayout.GoodieArrayOffset,
            RetailCareerRecordLayout.LinkOffset(RetailCareerRecordLayout.LinkCount));
    }

    private static RetailWorldGradeNode[] OneNode(int world, int complete, float ranking) =>
        new[] { new RetailWorldGradeNode(world, complete, ranking) };

    // COMPLETE_LEVEL dereferences the NULL that GetNodeFromWorldNo returned; the
    // source's own `if (cn)` guard is on the far side of that read.
    [Fact]
    public void GradeByteForWorld_FaultsOnAnUnknownWorld()
    {
        var nodes = OneNode(110, 1, 1.0f);

        Assert.Throws<InvalidOperationException>(
            () => RetailWorldGrade.GradeByteForWorld(nodes, 231));
        Assert.Throws<InvalidOperationException>(
            () => RetailWorldGrade.GradeByteForWorld(System.Array.Empty<RetailWorldGradeNode>(), 110));
    }

    // cmp dword ptr [eax + 4], 1: a BOOL of 2 is not complete. A rebuild that
    // tested for non-zero would grade this level on its ranking.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void GradeByteForWorld_NeedsALiteralTrueToScore(int complete) =>
        Assert.Equal(
            RetailWorldGrade.IncompleteGradeByte,
            RetailWorldGrade.GradeByteForWorld(OneNode(110, complete, 1.0f), 110));

    // The -9999 sentinel was folded to a bare mov al, 0x45 on both failure arms.
    [Fact]
    public void IncompleteGradeByte_IsTheFoldedFailedGrade()
    {
        Assert.Equal((byte)'E', RetailWorldGrade.IncompleteGradeByte);
        Assert.Equal(RetailCareerGrade.FailedGrade, RetailWorldGrade.IncompleteGradeByte);
        Assert.Equal(RetailWorldGrade.IncompleteGradeByte, RetailCareerGrade.GradeByteFromRanking(-9999.0f));
    }

    [Theory]
    [InlineData(1.0f, (byte)'S')]
    [InlineData(0.9f, (byte)'A')]
    [InlineData(0.75f, (byte)'A')]
    [InlineData(0.5f, (byte)'B')]
    [InlineData(0.25f, (byte)'C')]
    [InlineData(0.1f, (byte)'D')]
    [InlineData(0.0f, (byte)'E')]
    public void GradeByteForWorld_RunsTheRankingLadderOnACompleteWorld(float ranking, byte expected) =>
        Assert.Equal(expected, RetailWorldGrade.GradeByteForWorld(OneNode(110, 1, ranking), 110));

    // The inlined GetGradeFromRanking carries the same test ah, 0x40 that hands
    // an unordered ranking the top grade.
    [Fact]
    public void GradeByteForWorld_GivesAnUnorderedRankingThePerfectGrade() =>
        Assert.Equal(
            RetailCareerGrade.PerfectGrade,
            RetailWorldGrade.GradeByteForWorld(OneNode(110, 1, float.NaN), 110));

    // The scan is first-match, exactly as GetNodeFromWorldNo is.
    [Fact]
    public void GradeByteForWorld_TakesTheFirstNodeCarryingTheWorldNumber()
    {
        var nodes = new[]
        {
            new RetailWorldGradeNode(110, 1, 0.25f),
            new RetailWorldGradeNode(110, 1, 1.0f),
        };

        Assert.Equal((byte)'C', RetailWorldGrade.GradeByteForWorld(nodes, 110));
    }

    // The grade a completed world holds feeds straight into the ordering, so the
    // two contracts have to agree about which way round 'A' and 'C' sit.
    [Fact]
    public void GradeByteForWorld_ComposesWithTheOrdering()
    {
        var nodes = OneNode(110, 1, 0.9f);
        var held = new RetailGrade(unchecked((sbyte)RetailWorldGrade.GradeByteForWorld(nodes, 110)));

        Assert.True(held.IsAtLeast(Of('C')));
        Assert.True(held.IsAtLeast(Of('A')));
        Assert.False(held.IsAtLeast(Of('S')));
    }
}
