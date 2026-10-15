// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailBattleEngineGravity"/> against
/// <c>references/Onslaught/BattleEngine.cpp:1064-1088</c>,
/// <c>BattleEngineJetPart.cpp:507-513</c> and the pristine <c>74154bfa…</c>
/// bytes at <c>0x004074D0</c> and <c>0x004114D0</c>.
/// </summary>
public sealed class RetailBattleEngineGravityTests
{
    // The four .rdata words the two bodies load, bit for bit. 0.01f, 0.002f and
    // 0.005f are all inexact, so a rebuild that wrote them as ratios of
    // integers, or that computed 0.01f*0.2f in the wrong precision, would drift.
    [Fact]
    public void Constants_MatchTheReadOnlyDataTheBodiesLoad()
    {
        Assert.Equal(
            0x3C23D70Au,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.ThingGravity));
        Assert.Equal(
            0x3B03126Fu,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.MorphingIntoWalkerGravity));
        Assert.Equal(
            0x3BA3D70Au,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.DeadEngineJetGravity));
        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.NoGravity));
    }

    // The enum ordinals are load-bearing: they are the jump-table indices at
    // 0x00407520 and 0x00407530. Reading the header as "walker first" would
    // swap which state gets the scaled gravity.
    [Fact]
    public void States_KeepTheOrdinalsTheJumpTablesIndexOn()
    {
        Assert.Equal(0, (int)RetailBattleEngineState.MorphingIntoWalker);
        Assert.Equal(1, (int)RetailBattleEngineState.MorphingIntoJet);
        Assert.Equal(2, (int)RetailBattleEngineState.Walker);
        Assert.Equal(3, (int)RetailBattleEngineState.Jet);
    }

    // The alive table at 0x00407530 is {0.002f, 0.01f, 0.01f, jet} and the
    // dying table at 0x00407520 is {0.01f, 0.01f, 0.01f, jet}. Every entry is
    // asserted, so moving the scaled arm to any other state - or applying it
    // while dying - fails.
    //
    // NON-VACUITY: the scaled arm sits at ONE table index, so the whole contract
    // is which index that is - and reading BattleEngine.h:28-34 as "walker first"
    // moves it. The fourth column declares whether this state is the one the two
    // tables disagree on, and the test measures the disagreement rather than
    // taking the ordinals on trust. Exactly one of the three non-jet states may
    // answer that description, which the sweep at the end counts.
    [Theory]
    [InlineData(false, RetailBattleEngineState.MorphingIntoWalker, 0x3B03126Fu, true)]
    [InlineData(false, RetailBattleEngineState.MorphingIntoJet, 0x3C23D70Au, false)]
    [InlineData(false, RetailBattleEngineState.Walker, 0x3C23D70Au, false)]
    [InlineData(true, RetailBattleEngineState.MorphingIntoWalker, 0x3C23D70Au, true)]
    [InlineData(true, RetailBattleEngineState.MorphingIntoJet, 0x3C23D70Au, false)]
    [InlineData(true, RetailBattleEngineState.Walker, 0x3C23D70Au, false)]
    public void Gravity_WalksBothJumpTables(
        bool isDying,
        RetailBattleEngineState state,
        uint expectedBits,
        bool tablesDisagreeHere)
    {
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(isDying, state, jetEnergy: 1.0f)));

        Assert.Equal(
            tablesDisagreeHere,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(false, state, jetEnergy: 1.0f))
                != BitConverter.SingleToUInt32Bits(
                    RetailBattleEngineGravity.Gravity(true, state, jetEnergy: 1.0f)));

        // The exercised population, counted: across the three non-jet slots the
        // alive table differs from the dying table at exactly one index, and it
        // is index 0. If a rebuild moved the scaled arm the count stays 1 and
        // this assertion still catches it, because the index is named.
        RetailBattleEngineState[] nonJet =
        [
            RetailBattleEngineState.MorphingIntoWalker,
            RetailBattleEngineState.MorphingIntoJet,
            RetailBattleEngineState.Walker,
        ];
        int scaledSlots = 0;
        int scaledIndex = -1;
        foreach (RetailBattleEngineState slot in nonJet)
        {
            if (BitConverter.SingleToUInt32Bits(
                    RetailBattleEngineGravity.Gravity(false, slot, jetEnergy: 1.0f))
                == 0x3B03126Fu)
            {
                scaledSlots++;
                scaledIndex = (int)slot;
            }
        }

        Assert.Equal(1, scaledSlots);
        Assert.Equal(0, scaledIndex);
    }

    // Slot 3 forwards to the jet part in BOTH tables - 0x004074E8 and
    // 0x0040750D are the same two instructions - so the dying flag must not
    // reach the jet arm.
    [Theory]
    [InlineData(false, 1.0f, 0x00000000u)]
    [InlineData(true, 1.0f, 0x00000000u)]
    [InlineData(false, 0.0f, 0x3BA3D70Au)]
    [InlineData(true, 0.0f, 0x3BA3D70Au)]
    public void Gravity_ForwardsTheJetSlotRegardlessOfTheDyingFlag(
        bool isDying, float energy, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(isDying, RetailBattleEngineState.Jet, energy)));

    // cmp eax, 3 / ja 0x00407518 is unsigned, so 4 and -1 both fall to the
    // default 0.0f arm. A rebuild that switched on the enum and threw, or that
    // fell through to the walker value, is killed here.
    [Theory]
    [InlineData(4)]
    [InlineData(1000)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Gravity_ReturnsZeroForAnyStateOutsideTheTable(int state)
    {
        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(isDying: false, state, jetEnergy: 0.0f)));
        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(isDying: true, state, jetEnergy: 0.0f)));
    }

    // The energy test at 0x004114E1 is `test ah, 0x40` - C3 alone - and a
    // negative zero compares equal to zero, so both zeroes take the dead-engine
    // arm. Anything else, of either sign, is under power.
    [Theory]
    [InlineData(0.0f, 0x3BA3D70Au)]
    [InlineData(0.0001f, 0x00000000u)]
    [InlineData(-0.0001f, 0x00000000u)]
    [InlineData(1000.0f, 0x00000000u)]
    public void JetGravity_TreatsBothZeroesAsADeadEngine(float energy, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.JetGravity(energy)));

    // fcomp against the shared +0.0f word, so -0.0f compares equal and is also
    // a dead engine. Written as a bit test rather than a value test it would
    // not be.
    [Fact]
    public void JetGravity_TreatsNegativeZeroAsADeadEngineToo() =>
        Assert.Equal(
            0x3BA3D70Au,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.JetGravity(
                    BitConverter.UInt32BitsToSingle(0x80000000u))));

    // An unordered compare sets C3, and the shipped test reads C3 alone, so a
    // NaN energy is a dead engine. Written the source's way - `mEnergy == 0` -
    // C# would return 0.0f. This is the divergence the type documents.
    [Fact]
    public void JetGravity_TreatsAnUnorderedEnergyAsADeadEngine()
    {
        Assert.Equal(
            0x3BA3D70Au,
            BitConverter.SingleToUInt32Bits(RetailBattleEngineGravity.JetGravity(float.NaN)));

        Assert.Equal(
            0x3BA3D70Au,
            BitConverter.SingleToUInt32Bits(
                RetailBattleEngineGravity.Gravity(
                    isDying: false, RetailBattleEngineState.Jet, float.NaN)));

        // The C reading, pinned so the divergence cannot be quietly closed.
        Assert.False(float.NaN == 0.0f);
    }
}
