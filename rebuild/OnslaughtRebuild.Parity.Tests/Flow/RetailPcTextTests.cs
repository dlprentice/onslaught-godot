// SPDX-License-Identifier: GPL-3.0-only
// Localization_GetStringById (0x00524830) is recognised by the SHA-256 of its opcode bytes; the source carries no program bytes.

using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Tests;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// The PC texts are read from the player's own BEA.exe after the function's language branch is checked against a pinned
/// SHA-256 of its six opcode bytes (a bounds compare, an out-of-range branch and an indexed jump-table dispatch). The
/// check is exactly as strict as the byte comparison it replaced: one changed opcode byte, in any of the six places, is
/// refused, and the values between the opcodes, which differ per language, are read but not checked.
/// </summary>
public sealed class RetailPcTextTests
{
    [GameDataFact]
    public void ASingleChangedOpcodeByteOfTheLanguageBranchFailsTheCheck()
    {
        byte[] exe = GameData.Read("BEA.exe").Bytes;
        var installed = new RetailPcText(exe, 0);
        int at = installed.BranchOffset();
        Assert.True(installed.BranchHasExpectedShape(at));

        // The six opcode positions, written out so that a check that drops one (with its digest pinned again) fails here.
        int[] opcodePositions = [0, 5, 6, 11, 12, 13];
        Assert.Equal(opcodePositions, RetailPcText.BranchShapeOffsets);

        foreach (int position in opcodePositions)
        {
            byte[] changed = (byte[])exe.Clone();
            changed[at + position] ^= 0x01;
            var refused = new RetailPcText(changed, 0);
            Assert.False(refused.BranchHasExpectedShape(at), $"the opcode byte at +{position} was changed and still accepted");
            var error = Assert.Throws<InvalidDataException>(() => refused[0]);
            Assert.Contains("language branch", error.Message, StringComparison.Ordinal);
        }

        // The largest id, the branch displacement and the table address sit between the opcodes: read, not checked.
        byte[] retuned = (byte[])exe.Clone();
        foreach (int position in new[] { 1, 2, 3, 4, 7, 8, 9, 10, 14, 15, 16, 17 })
            retuned[at + position] ^= 0x01;
        Assert.True(new RetailPcText(retuned, 0).BranchHasExpectedShape(at));
    }
}
