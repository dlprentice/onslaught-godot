// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailJetAutoLevel"/> against
/// <c>references/Onslaught/BattleEngineJetPart.cpp:1049-1061</c> and the
/// pristine <c>74154bfa…</c> bytes at <c>0x00412900</c>.
/// </summary>
public sealed class RetailJetAutoLevelTests
{
    // 0x005D8C60 is 0x3C23D70B, which is `0.1f*0.1f` folded through double -
    // NOT 0.01f, which is 0x3C23D70A and sits at 0x005D8574 in the same image.
    // A rebuild that wrote the tidier literal lands one ulp low.
    [Fact]
    public void MinManoeuvreVelocitySq_IsTheFoldedProductNotPlainHundredth()
    {
        Assert.Equal(
            0x3C23D70Bu,
            BitConverter.SingleToUInt32Bits(RetailJetAutoLevel.MinManoeuvreVelocitySq));

        Assert.Equal(0x3C23D70Au, BitConverter.SingleToUInt32Bits(0.01f));
        Assert.NotEqual(0.01f, RetailJetAutoLevel.MinManoeuvreVelocitySq);
    }

    // Off the ground the velocity gate is never evaluated at all: retail calls
    // IsOnGround first and only then fetches the velocity. A stationary jet in
    // the air still auto-levels.
    [Fact]
    public void AutoLevel_SkipsTheVelocityGateWhenAirborne() =>
        Assert.True(RetailJetAutoLevel.AutoLevel(
            isOnGround: false,
            velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
            energy: 0.0f,
            doingBarrelCount: 0.0f));

    // On the ground the gate is `magnitudeSq >= 0x3C23D70B`. The 0.1f row is
    // the seam: 0.1f squared in double is BELOW the folded constant, because
    // the constant was rounded up when it was folded. So a jet moving at
    // exactly 0.1 along one axis does NOT auto-level.
    //
    // NON-VACUITY: the third column declares whether the row separates the folded
    // constant at 0x005D8C60 (0x3C23D70B) from a rebuild that wrote a plain 0.01f
    // (0x3C23D70A), and the test measures that separation instead of asserting it
    // in a comment. Only the 0.1f row does: 0.1f squared is >= 0x3C23D70A and
    // < 0x3C23D70B, so it is the single probe on which the one-ulp difference is
    // observable at all. The assertion proves that probe is really inside that
    // one-ulp window, so the window cannot quietly become empty.
    [Theory]
    [InlineData(0.0f, false, false)]
    [InlineData(0.09f, false, false)]
    [InlineData(0.1f, false, true)]
    [InlineData(0.10000001f, true, false)]
    [InlineData(5.0f, true, false)]
    public void AutoLevel_GatesOnTheSquaredManoeuvreSpeedWhenGrounded(
        float speedAlongX, bool expected, bool separatesTheFoldedConstant)
    {
        double magnitudeSq = RetailJetAutoLevel.VelocityMagnitudeSquared(
            speedAlongX, 0.0f, 0.0f);
        const float PlainHundredth = 0.01f;

        Assert.Equal(
            separatesTheFoldedConstant,
            magnitudeSq >= PlainHundredth
                && magnitudeSq < RetailJetAutoLevel.MinManoeuvreVelocitySq);

        Assert.Equal(
            expected,
            RetailJetAutoLevel.AutoLevel(
                isOnGround: true,
                velocityX: speedAlongX, velocityY: 0.0f, velocityZ: 0.0f,
                energy: 1.0f,
                doingBarrelCount: 0.0f));
    }

    // The squared magnitude never leaves the x87 stack, so every partial
    // product and sum is at the ambient 53-bit precision. This vector is one
    // where that matters: accumulated in double the sum is below the threshold
    // and the jet is pinned, accumulated in float it lands exactly ON the
    // threshold and the jet levels. Rounding any intermediate to float flips
    // the answer.
    [Fact]
    public void AutoLevel_AccumulatesTheSquareInDoubleNotFloat()
    {
        float x = BitConverter.UInt32BitsToSingle(0x3DCCCBE9u);
        float y = BitConverter.UInt32BitsToSingle(0x3A031247u);
        float z = BitConverter.UInt32BitsToSingle(0x399D492Au);

        double magnitudeSq = RetailJetAutoLevel.VelocityMagnitudeSquared(x, y, z);
        Assert.Equal(0x3F847AE1_5AA08BC7UL, BitConverter.DoubleToUInt64Bits(magnitudeSq));
        Assert.True(magnitudeSq < (double)RetailJetAutoLevel.MinManoeuvreVelocitySq);

        float allFloat = x * x + y * y + z * z;
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(RetailJetAutoLevel.MinManoeuvreVelocitySq),
            BitConverter.SingleToUInt32Bits(allFloat));
        Assert.False(allFloat < RetailJetAutoLevel.MinManoeuvreVelocitySq);

        Assert.False(RetailJetAutoLevel.AutoLevel(
            isOnGround: true,
            velocityX: x, velocityY: y, velocityZ: z,
            energy: 1.0f,
            doingBarrelCount: 0.0f));
    }

    // The association is (x*x + y*y) + z*z, in that order - fld z, fld y, fld
    // x, then two faddp. A vector whose components are wildly different in
    // magnitude separates that from x*x + (y*y + z*z).
    [Fact]
    public void VelocityMagnitudeSquared_KeepsTheShippedAssociation()
    {
        float x = BitConverter.UInt32BitsToSingle(0x4E800000u);
        const float y = 10.0f;
        const float z = 10.0f;

        double shipped = RetailJetAutoLevel.VelocityMagnitudeSquared(x, y, z);
        double other = (double)x * x + ((double)y * y + (double)z * z);

        Assert.Equal(0x43B0000000000000UL, BitConverter.DoubleToUInt64Bits(shipped));
        Assert.Equal(0x43B0000000000001UL, BitConverter.DoubleToUInt64Bits(other));
    }

    // The energy gate is `test ah, 1` - C0 alone - so it is a strict less-than
    // against +0.0f. Exactly zero passes; negative zero compares equal and also
    // passes.
    [Theory]
    [InlineData(0.0f, true)]
    [InlineData(0.001f, true)]
    [InlineData(-0.001f, false)]
    [InlineData(-1000.0f, false)]
    public void AutoLevel_BlocksOnlyOnStrictlyNegativeEnergy(float energy, bool expected) =>
        Assert.Equal(
            expected,
            RetailJetAutoLevel.AutoLevel(
                isOnGround: false,
                velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
                energy,
                doingBarrelCount: 0.0f));

    // Both gates compare against the shared +0.0f word, so a negative zero
    // energy and a negative zero barrel counter both pass.
    [Fact]
    public void AutoLevel_PassesOnNegativeZero()
    {
        float negativeZero = BitConverter.UInt32BitsToSingle(0x80000000u);

        Assert.True(RetailJetAutoLevel.AutoLevel(
            isOnGround: false,
            velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
            energy: negativeZero,
            doingBarrelCount: 0.0f));

        Assert.True(RetailJetAutoLevel.AutoLevel(
            isOnGround: false,
            velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
            energy: 1.0f,
            doingBarrelCount: negativeZero));
    }

    // BattleEngineJetPart.h:105 declares mDoingBarrelCount as a float and
    // 0x00412971 is `fld dword`, so a fractional count between zero and one
    // blocks the auto-level. An int-typed rebuild would truncate 0.5 to 0 and
    // let it through.
    [Theory]
    [InlineData(0.0f, true)]
    [InlineData(-3.0f, true)]
    [InlineData(0.5f, false)]
    [InlineData(1.0f, false)]
    public void AutoLevel_ReadsTheBarrelCounterAsAFloat(float count, bool expected) =>
        Assert.Equal(
            expected,
            RetailJetAutoLevel.AutoLevel(
                isOnGround: false,
                velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
                energy: 1.0f,
                doingBarrelCount: count));

    // Two of the three gates read C0 alone, so an unordered value falls the
    // "less" way and blocks. The third reads C0 or C3 with jne, so an unordered
    // value passes. Writing all three the natural C# way would flip the first
    // two.
    [Fact]
    public void AutoLevel_UnorderedInputsFallTheWayTheStatusWordDoes()
    {
        Assert.False(RetailJetAutoLevel.AutoLevel(
            isOnGround: true,
            velocityX: float.NaN, velocityY: 0.0f, velocityZ: 0.0f,
            energy: 1.0f,
            doingBarrelCount: 0.0f));

        Assert.False(RetailJetAutoLevel.AutoLevel(
            isOnGround: false,
            velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
            energy: float.NaN,
            doingBarrelCount: 0.0f));

        Assert.True(RetailJetAutoLevel.AutoLevel(
            isOnGround: false,
            velocityX: 0.0f, velocityY: 0.0f, velocityZ: 0.0f,
            energy: 1.0f,
            doingBarrelCount: float.NaN));
    }
}
