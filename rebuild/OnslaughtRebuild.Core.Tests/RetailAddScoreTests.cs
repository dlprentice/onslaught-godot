// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::AddScore</c> at <c>0x005343c0</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x001343c0</c> is
/// <c>8b 44 24 04 8b 08 8b 11 ff 52 30 01 05 8c 9b 8a 00 c2 0c 00</c>.
/// <c>0x005343cb</c> is <c>01 05 8c 9b 8a 00</c> =
/// <c>add [0x008a9b8c], eax</c> = <c>CGame+0xf4</c>
/// (<c>0x008a9a98 + 0xf4</c>). Body SHA-256
/// <c>b4b6efb14498a8c71f3d1caa99f5e463f98393736c2f191bc365834245718b88</c>.
/// Source <c>game.h:210</c> is <c>IncScore(SINT) { mScore+=inScore; }</c>.
/// Isolated <see cref="Level100MissionSnapshot.ScoreDelta"/> = 50
/// names the rebuild accumulator, not this add. Isolated FillOut
/// <see cref="RetailFillOutEndLevelData.ScoreWord"/> copies a
/// parameterized dword and does not go through AddScore. First-play
/// elapsed and FillOut score stay unclaimed. Mutation: replace
/// instead of add. No new secondaries.
/// </summary>
public sealed class RetailAddScoreTests
{
    /// <summary>
    /// Two <c>add [0x008a9b8c], eax</c> stores accumulate. First-play
    /// Won only posts one <c>AddScore(50)</c>, so replace and add
    /// both leave 50 there — isolated <c>ScoreDelta</c> is not unique
    /// versus this incrementer. Mutation: <c>return delta</c>.
    /// </summary>
    [Fact]
    public void Add_AddsTheDeltaOntoCGamePlusF4NotReplace()
    {
        Assert.Equal(0x008a9b8cu, RetailAddScore.GameScoreAddress);
        Assert.Equal(0xf4, RetailAddScore.GameScoreOffset);
        Assert.Equal(
            RetailAddScore.GameSingletonAddress + RetailAddScore.GameScoreOffset,
            RetailAddScore.GameScoreAddress);
        Assert.Equal(0, RetailAddScore.LoadLevelZero);
        Assert.Equal(50, RetailAddScore.Add(RetailAddScore.LoadLevelZero, 50));
        Assert.Equal(100, RetailAddScore.Add(50, 50));
        Assert.Equal(-50, RetailAddScore.Add(RetailAddScore.LoadLevelZero, -50));
        Assert.NotEqual(50, RetailAddScore.Add(50, 50));
    }
}
