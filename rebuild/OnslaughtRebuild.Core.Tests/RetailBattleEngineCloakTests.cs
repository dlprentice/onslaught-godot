// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailBattleEngineCloak"/> against
/// <c>references/Onslaught/BattleEngine.cpp:3096-3119</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x0040D4D0</c>.
/// </summary>
public sealed class RetailBattleEngineCloakTests
{
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    [Fact]
    public void NoStealth_IsTheSharedZeroWord() =>
        Assert.Equal(0x00000000u, Bits(RetailBattleEngineCloak.NoStealth));

    // The decloak arm is first and unconditional: no energy and no stealth are
    // consulted. A rebuild that gated it on energy would strand the player
    // invisible.
    [Fact]
    public void HandleCloak_DecloaksWithoutConsultingEnergyOrStealth()
    {
        var engine = new RetailBattleEngineCloak { Cloaked = 1, DesiredStealth = 40.0f };

        engine.HandleCloak(energy: -100.0f, minTransformEnergy: 10.0f, configurationStealthBits: 0u);

        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(0.0f, engine.DesiredStealth);
    }

    // cmp edx, eax against a zeroed register: non-zero, not == TRUE. A rebuild
    // that insisted on a literal 1 would try to cloak here instead.
    [Theory]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void HandleCloak_TreatsAnyNonZeroCloakedWordAsCloaked(int cloaked)
    {
        var engine = new RetailBattleEngineCloak { Cloaked = cloaked, DesiredStealth = 40.0f };

        engine.HandleCloak(energy: 100.0f, minTransformEnergy: 10.0f, configurationStealthBits: Bits(55.0f));

        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(0.0f, engine.DesiredStealth);
    }

    // Decloak stores the integer zero word, so a negative zero left behind by
    // some other writer is replaced by a positive one.
    [Fact]
    public void Decloak_WritesPositiveZero()
    {
        var engine = new RetailBattleEngineCloak { Cloaked = 1, DesiredStealth = -0.0f };

        Assert.Equal(0x80000000u, Bits(engine.DesiredStealth));

        engine.Decloak();

        Assert.Equal(0x00000000u, Bits(engine.DesiredStealth));
    }

    [Fact]
    public void HandleCloak_CloaksWhenEnergyReachesTheThresholdAndStealthIsPositive()
    {
        var engine = new RetailBattleEngineCloak();

        engine.HandleCloak(energy: 25.0f, minTransformEnergy: 25.0f, configurationStealthBits: Bits(60.0f));

        Assert.Equal(1, engine.Cloaked);
        Assert.Equal(60.0f, engine.DesiredStealth);
    }

    // The energy gate is >=, not >. One ulp below the threshold fails it.
    [Fact]
    public void HandleCloak_NeedsAtLeastTheThresholdEnergy()
    {
        var engine = new RetailBattleEngineCloak();
        float justBelow = BitConverter.UInt32BitsToSingle(Bits(25.0f) - 1u);

        engine.HandleCloak(justBelow, minTransformEnergy: 25.0f, configurationStealthBits: Bits(60.0f));

        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(0.0f, engine.DesiredStealth);
    }

    // test ah, 1 - C0 alone - and C's `>=` is false for a NaN too, so here the
    // MSVC idiom and the source text AGREE. The row is pinned so a rebuild
    // cannot "fix" it into cloaking.
    [Fact]
    public void HandleCloak_RefusesAnUnorderedEnergy()
    {
        var engine = new RetailBattleEngineCloak();

        engine.HandleCloak(float.NaN, minTransformEnergy: 25.0f, configurationStealthBits: Bits(60.0f));

        Assert.Equal(0, engine.Cloaked);

        // An unordered threshold poisons it from the other side.
        engine.HandleCloak(100.0f, float.NaN, Bits(60.0f));

        Assert.Equal(0, engine.Cloaked);
    }

    // test ah, 0x41 - C0 or C3 - so zero, negative zero, negative and unordered
    // all block the cloak. C's `> 0` agrees on every one of them.
    [Theory]
    [InlineData(0x00000000u)]
    [InlineData(0x80000000u)]
    [InlineData(0xBF800000u)]
    [InlineData(0x7FC00000u)]
    [InlineData(0xFFC00000u)]
    public void HandleCloak_RefusesANonPositiveOrUnorderedStealth(uint stealthBits)
    {
        var engine = new RetailBattleEngineCloak();

        engine.HandleCloak(energy: 100.0f, minTransformEnergy: 25.0f, configurationStealthBits: stealthBits);

        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(0.0f, engine.DesiredStealth);
    }

    // The store is a raw dword move, so the smallest subnormal survives intact:
    // it is strictly positive, so it opens the gate, and its bits are copied
    // rather than recomputed.
    [Fact]
    public void HandleCloak_CopiesTheStealthWordBitForBit()
    {
        var engine = new RetailBattleEngineCloak();

        engine.HandleCloak(energy: 100.0f, minTransformEnergy: 0.0f, configurationStealthBits: 0x00000001u);

        Assert.Equal(1, engine.Cloaked);
        Assert.Equal(0x00000001u, Bits(engine.DesiredStealth));
    }

    // Cloak on its own leaves mCloaked alone when the stealth gate fails, which
    // matters because HandleCloak has no else arm to fall back on.
    [Fact]
    public void Cloak_LeavesBothFieldsAloneOnAFailedGate()
    {
        var engine = new RetailBattleEngineCloak { DesiredStealth = 7.0f };

        engine.Cloak(Bits(0.0f));

        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(7.0f, engine.DesiredStealth);
    }

    // Two calls with the energy held high alternate, because the first thing the
    // body does is look at mCloaked.
    [Fact]
    public void HandleCloak_TogglesOnRepeatedCalls()
    {
        var engine = new RetailBattleEngineCloak();

        engine.HandleCloak(100.0f, 25.0f, Bits(60.0f));
        Assert.Equal(1, engine.Cloaked);

        engine.HandleCloak(100.0f, 25.0f, Bits(60.0f));
        Assert.Equal(0, engine.Cloaked);
        Assert.Equal(0.0f, engine.DesiredStealth);

        engine.HandleCloak(100.0f, 25.0f, Bits(60.0f));
        Assert.Equal(1, engine.Cloaked);
        Assert.Equal(60.0f, engine.DesiredStealth);
    }
}
