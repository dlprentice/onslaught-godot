// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::SetObjective</c> at <c>0x00535ed0</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x00135ed0</c> holds the body
/// (13 B SHA-256
/// <c>e1e368b83a8c664935143709b40f4ad2bf7c6217003492b5d64c2562a48f666b</c>).
/// Twin Unset at <c>0x00535ee0</c> pushes 0 into the same
/// <c>CThing::SetObjective</c> at <c>0x004f3970</c>. That callee
/// at <c>0x004f398e</c> is an or of 0x20 into byte field +0x2c of esi; Unset at
/// <c>0x004f39a5</c> is a mask of byte field +0x2c of esi with 0xdf. Isolated
/// <see cref="Level100ActorSnapshot.IsObjective"/> names the
/// rebuild bool, not these bit ops. Noticeboard Add/Remove stay
/// unclaimed. Mutation: replace so Mark(0x04) becomes 0x20, or
/// Unmark writes 0. No new secondaries.
/// </summary>
public sealed class RetailSetObjectiveTests
{
    /// <summary>
    /// An or of 0x20 into field +0x2c of esi keeps TF_DYING (4). Isolated
    /// <c>IsObjective</c> = true still passes if this OR is
    /// skipped. Mutation: <c>return MarkedBit</c>.
    /// </summary>
    [Fact]
    public void MarkAndUnmark_OrBit20ThenClearItNotBoolReplace()
    {
        Assert.Equal(0x2c, RetailSetObjective.FlagsOffset);
        Assert.Equal(0x20, RetailSetObjective.MarkedBit);
        Assert.Equal(0x04, RetailSetObjective.DyingBit);
        Assert.Equal(0x20, RetailSetObjective.Mark(0));
        Assert.Equal(0x24, RetailSetObjective.Mark(RetailSetObjective.DyingBit));
        Assert.Equal(0x20, RetailSetObjective.Mark(0x20));
        Assert.NotEqual(0x20, RetailSetObjective.Mark(RetailSetObjective.DyingBit));
        Assert.Equal(0x04, RetailSetObjective.Unmark(0x24));
        Assert.Equal(0, RetailSetObjective.Unmark(0x20));
        Assert.Equal(0x04, RetailSetObjective.Unmark(RetailSetObjective.DyingBit));
        Assert.NotEqual(0, RetailSetObjective.Unmark(0x24));
    }
}
