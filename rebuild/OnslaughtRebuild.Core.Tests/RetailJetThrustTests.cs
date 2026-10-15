// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailJetThrust"/> against
/// <c>references/Onslaught/BattleEngineJetPart.cpp:22, 64-106</c> and the
/// pristine <c>74154bfa…</c> bytes at <c>0x00410310</c>.
/// </summary>
public sealed class RetailJetThrustTests
{
    // now - 0.2f is exactly -0.1f and now - 0.1f is exactly 0.0 for this clock
    // value, because 0.2f is exactly twice 0.1f. That makes both ends of the
    // loop window exactly representable and the strictness testable.
    private const float Now = 0.1f;

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);

    private static float FromBits(uint bits) => BitConverter.UInt32BitsToSingle(bits);

    private static RetailJetThrustState Armed(float lastMoveYVal = 0.0f, float stamp = -0.05f) =>
        new RetailJetThrustState { LastMoveYVal = lastMoveYVal, LastStartHardForwardTime = stamp };

    private static RetailJetThrustMainPart Powered(float energy = 5.0f, float pitchvel = 0.5f) =>
        new RetailJetThrustMainPart { Energy = energy, Pitchvel = pitchvel };

    [Fact]
    public void Constants_MatchTheReadOnlyDataTheBodyLoads()
    {
        Assert.Equal(0x3F000000u, Bits(RetailJetThrust.ThrusterCentre));
        Assert.Equal(0xBF19999Au, Bits(RetailJetThrust.HardForwardArmThreshold));
        Assert.Equal(0xBF666666u, Bits(RetailJetThrust.HardForwardThreshold));
        Assert.Equal(0x3F4CCCCDu, Bits(RetailJetThrust.LoopPullBackThreshold));
        Assert.Equal(0x3E4CCCCDu, Bits(RetailJetThrust.LoopWindowOldest));
        Assert.Equal(0x3DCCCCCDu, Bits(RetailJetThrust.LoopWindowNewest));
        Assert.Equal(0x3DB851ECu, Bits(RetailJetThrust.MinManoeuvreVelocitySq));
        Assert.Equal(0x3C75C28Fu, Bits(RetailJetThrust.LoopPitchImpulse));
    }

    // kMinManoeuvreVelocitySq is 0.3f*0.3f folded through double, which is nine
    // times the 0.1f*0.1f that AutoLevel loads from a different word. A rebuild
    // that shared one constant between the two sites is wrong at one of them.
    [Fact]
    public void MinManoeuvreVelocitySq_IsNotTheAutoLevelThreshold()
    {
        Assert.NotEqual(
            Bits(RetailJetAutoLevel.MinManoeuvreVelocitySq),
            Bits(RetailJetThrust.MinManoeuvreVelocitySq));
        Assert.Equal(0x3DB851ECu, Bits((float)((double)0.3f * (double)0.3f)));
    }

    // The clock value the window tests rely on really does give exact ends.
    [Fact]
    public void LoopWindow_HasExactEndsForThisClockValue()
    {
        Assert.Equal(-(double)RetailJetThrust.LoopWindowNewest,
            (double)Now - (double)RetailJetThrust.LoopWindowOldest);
        Assert.Equal(0.0, (double)Now - (double)RetailJetThrust.LoopWindowNewest);
    }

    [Theory]
    [InlineData(0.0f, 0x3F000000u)]
    [InlineData(1.0f, 0x00000000u)]
    [InlineData(-1.0f, 0x3F800000u)]
    [InlineData(0.5f, 0x3E800000u)]
    [InlineData(0.9f, 0x3D4CCCD0u)]
    public void ThrusterValueFor_MatchesTheCompiledMultiplyAndReverseSubtract(float vy, uint expected)
    {
        Assert.Equal(expected, Bits(RetailJetThrust.ThrusterValueFor(vy)));

        // The strength reduction is exact: dividing by two and halving are the
        // same IEEE-754 operation.
        Assert.Equal(
            Bits((float)((double)0.5f - (double)vy / 2.0)),
            Bits(RetailJetThrust.ThrusterValueFor(vy)));
    }

    // The barrel-roll return is the ONLY exit that does not record vy.
    [Fact]
    public void Thrust_BarrelRollReturnsWithoutRecordingTheInput()
    {
        var state = Armed(lastMoveYVal: -0.25f);
        state.DoingBarrelCount = 1.0f;
        state.ThrusterValue = 0.125f;
        var mainPart = Powered();

        RetailJetThrust.Thrust(state, mainPart, vy: 0.5f, Now, 1.0f, 0.0f, 0.0f);

        Assert.Equal(-0.25f, state.LastMoveYVal);
        Assert.Equal(0.125f, state.ThrusterValue);
    }

    // test ah, 0x41 with je to the early exit: the exit needs an ordered strictly
    // positive count, so zero and unordered both fall through into the body. C's
    // `> 0` agrees on both.
    [Theory]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(float.NaN)]
    public void Thrust_RunsTheBodyForANonPositiveOrUnorderedBarrelCount(float barrelCount)
    {
        var state = Armed(lastMoveYVal: -0.25f);
        state.DoingBarrelCount = barrelCount;

        RetailJetThrust.Thrust(state, Powered(), vy: 0.5f, Now, 1.0f, 0.0f, 0.0f);

        Assert.Equal(0.5f, state.LastMoveYVal);
        Assert.Equal(Bits(RetailJetThrust.ThrusterValueFor(0.5f)), Bits(state.ThrusterValue));
    }

    // A jet already looping skips the whole throttle body but still records the
    // input, because the store is past the if.
    [Fact]
    public void Thrust_RecordsTheInputWhileLoopingWithoutTouchingTheThrottle()
    {
        var state = Armed();
        state.DoingLoop = 1;
        state.ThrusterValue = 0.125f;

        RetailJetThrust.Thrust(state, Powered(), vy: -0.95f, Now, 1.0f, 0.0f, 0.0f);

        Assert.Equal(-0.95f, state.LastMoveYVal);
        Assert.Equal(0.125f, state.ThrusterValue);
        Assert.Equal(-0.05f, state.LastStartHardForwardTime);
    }

    // `if (mMainPart->mEnergy)` is a float truthiness test, so both zeros skip
    // the body - and the input is still recorded.
    [Theory]
    [InlineData(0x00000000u)]
    [InlineData(0x80000000u)]
    public void Thrust_SkipsTheThrottleWithNoEnergy(uint energyBits)
    {
        var state = Armed();
        state.ThrusterValue = 0.125f;

        RetailJetThrust.Thrust(
            state, Powered(energy: FromBits(energyBits)), vy: 0.5f, Now, 1.0f, 0.0f, 0.0f);

        Assert.Equal(0.125f, state.ThrusterValue);
        Assert.Equal(0.5f, state.LastMoveYVal);
    }

    // test ah, 0x40 is C3 ALONE and an unordered compare sets C3, so retail reads
    // a NaN energy as zero energy. The C text `if (float)` is `!= 0.0`, which is
    // TRUE for a NaN, so a source-faithful rebuild would run the throttle here
    // and leave ThrusterValue unordered.
    [Fact]
    public void Thrust_TreatsAnUnorderedEnergyAsNoEnergy()
    {
        var state = Armed();
        state.ThrusterValue = 0.125f;

        RetailJetThrust.Thrust(state, Powered(energy: float.NaN), vy: 0.5f, Now, 1.0f, 0.0f, 0.0f);

        Assert.Equal(0.125f, state.ThrusterValue);
        Assert.False(float.IsNaN(state.ThrusterValue));
        Assert.Equal(0.5f, state.LastMoveYVal);
    }

    [Fact]
    public void Thrust_StampsTheHardForwardTimeOnAFullForwardPush()
    {
        var state = Armed(lastMoveYVal: 0.0f, stamp: -99.0f);

        RetailJetThrust.Thrust(state, Powered(), vy: -0.95f, Now, 0.0f, 0.0f, 0.0f);

        Assert.Equal(Bits(Now), Bits(state.LastStartHardForwardTime));
    }

    // Both gates on the stamp are strict, and both are pinned at their exact
    // boundary.
    [Theory]
    [InlineData(-0.6f, -0.95f)] // mLastMoveYVal is not > -0.6f
    [InlineData(0.0f, -0.9f)]   // vy is not < -0.9f
    public void Thrust_DoesNotStampAtTheExactThresholds(float lastMoveYVal, float vy)
    {
        var state = Armed(lastMoveYVal, stamp: -99.0f);

        RetailJetThrust.Thrust(state, Powered(), vy, Now, 0.0f, 0.0f, 0.0f);

        Assert.Equal(-99.0f, state.LastStartHardForwardTime);
    }

    // test ah, 1 on the `vy < -0.9f` test is C0 alone, so an unordered vy stamps
    // the time where the C text would not. This is the second divergence.
    [Fact]
    public void Thrust_StampsTheHardForwardTimeForAnUnorderedInput()
    {
        var state = Armed(lastMoveYVal: 0.0f, stamp: -99.0f);

        RetailJetThrust.Thrust(state, Powered(), vy: float.NaN, Now, 0.0f, 0.0f, 0.0f);

        Assert.Equal(Bits(Now), Bits(state.LastStartHardForwardTime));
        Assert.True(float.IsNaN(state.ThrusterValue));
        Assert.Equal(0, state.DoingLoop);
    }

    [Fact]
    public void Thrust_StartsALoopInsideTheWindowWithEnergyAndSpeed()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered(pitchvel: 0.5f);

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, 0.4f, 0.0f, 0.0f);

        Assert.Equal(1, state.DoingLoop);
        Assert.Equal(0, state.LoopHalfway);
        Assert.Equal(0, state.LoopBroken);
        Assert.Equal(0x3EF851ECu, Bits(mainPart.Pitchvel));
        Assert.Equal(-20.0f, mainPart.LowEnergyStartTime);
        Assert.Equal(0.9f, state.LastMoveYVal);
    }

    // The retail-only 0.1f gate. A stamp newer than now - 0.1f is inside the
    // 0.2f window the pinned source checks and outside the shipped one, so a
    // rebuild written from BattleEngineJetPart.cpp:78-80 loops here and retail
    // does not.
    [Fact]
    public void Thrust_RefusesToLoopInsideTheHundredMillisecondDeadBand()
    {
        var state = Armed(stamp: 0.05f);
        var mainPart = Powered();

        Assert.True((double)state.LastStartHardForwardTime >
            (double)Now - (double)RetailJetThrust.LoopWindowOldest);

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, 0.4f, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(0.5f, mainPart.Pitchvel);
    }

    // Both ends of the window are strict, and this clock value makes both ends
    // exact floats.
    [Theory]
    [InlineData(-0.1f)] // exactly now - 0.2f: not strictly greater
    [InlineData(0.0f)]  // exactly now - 0.1f: not strictly less
    public void Thrust_TreatsTheWindowAsOpenAtBothEnds(float stamp)
    {
        var state = Armed(stamp: stamp);
        var mainPart = Powered();

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, 0.4f, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(0.5f, mainPart.Pitchvel);
    }

    // vy > 0.8f is strict too.
    [Fact]
    public void Thrust_NeedsMoreThanTheExactPullBackThreshold()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered();

        RetailJetThrust.Thrust(
            state, mainPart, RetailJetThrust.LoopPullBackThreshold, Now, 0.4f, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
    }

    // The else arm: energy present but not positive stamps the low-energy time
    // and does not loop. Energy has to be non-zero to get past the truthiness
    // gate, so a negative value is the only way in.
    [Fact]
    public void Thrust_StampsTheLowEnergyTimeInsteadOfLooping()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered(energy: -1.0f);

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, 0.4f, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(Bits(Now), Bits(mainPart.LowEnergyStartTime));
        Assert.Equal(0.5f, mainPart.Pitchvel);
    }

    // Too slow to loop, and the low-energy arm is not the one that ran.
    [Fact]
    public void Thrust_RefusesToLoopBelowTheManoeuvreSpeed()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered();
        float justUnder = FromBits(0x3E999999u);

        Assert.True(RetailJetAutoLevel.VelocityMagnitudeSquared(justUnder, 0.0f, 0.0f) <
            (double)RetailJetThrust.MinManoeuvreVelocitySq);

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, justUnder, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(-20.0f, mainPart.LowEnergyStartTime);
    }

    // One ulp higher in a single component and the gate opens. Exactly 0.3f
    // passes only because the threshold was narrowed to float on the way into
    // .rdata: against the double product it would fail.
    [Fact]
    public void Thrust_LoopsAtExactlyTheManoeuvreSpeedBecauseTheConstantWasNarrowed()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered();

        double magnitudeSq = RetailJetAutoLevel.VelocityMagnitudeSquared(0.3f, 0.0f, 0.0f);

        Assert.True(magnitudeSq > (double)RetailJetThrust.MinManoeuvreVelocitySq);
        Assert.False(magnitudeSq > (double)0.3f * (double)0.3f);

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, 0.3f, 0.0f, 0.0f);

        Assert.Equal(1, state.DoingLoop);
    }

    // The squared-speed gate is strictly greater, and the equality case really is
    // reachable - just not with one or two components. This triple makes the wide
    // (x*x + y*y) + z*z land exactly on 0x3DB851EC, and retail then refuses the
    // loop. A rebuild that wrote `>=` would start one.
    [Fact]
    public void Thrust_RefusesToLoopAtExactlyTheManoeuvreThreshold()
    {
        float x = FromBits(0x3E999999u);
        float y = FromBits(0x38FACC17u);
        float z = FromBits(0x35FA6B5Eu);

        Assert.Equal(
            (double)RetailJetThrust.MinManoeuvreVelocitySq,
            RetailJetAutoLevel.VelocityMagnitudeSquared(x, y, z));

        var state = Armed(stamp: -0.05f);
        var mainPart = Powered();

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, x, y, z);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(0.5f, mainPart.Pitchvel);
    }

    // An unordered velocity component fails the squared-speed gate, which C's
    // `>` does too.
    [Fact]
    public void Thrust_RefusesToLoopOnAnUnorderedVelocity()
    {
        var state = Armed(stamp: -0.05f);
        var mainPart = Powered();

        RetailJetThrust.Thrust(state, mainPart, vy: 0.9f, Now, float.NaN, 0.0f, 0.0f);

        Assert.Equal(0, state.DoingLoop);
        Assert.Equal(-20.0f, mainPart.LowEnergyStartTime);
    }

    // mLastMoveYVal is a raw dword copy of the argument, so a negative zero and
    // an unordered payload both survive intact.
    [Theory]
    [InlineData(0x80000000u)]
    [InlineData(0xFFC0DEADu)]
    [InlineData(0x7FC00001u)]
    public void Thrust_RecordsTheInputWordBitForBit(uint vyBits)
    {
        var state = Armed();

        RetailJetThrust.Thrust(state, Powered(), FromBits(vyBits), Now, 0.0f, 0.0f, 0.0f);

        Assert.Equal(vyBits, Bits(state.LastMoveYVal));
    }

    // The two clock stamps are raw dword copies as well.
    [Fact]
    public void Thrust_CopiesTheClockWordBitForBit()
    {
        float oddClock = FromBits(0x3DCCCCCEu);
        var state = Armed(lastMoveYVal: 0.0f, stamp: -99.0f);

        RetailJetThrust.Thrust(state, Powered(), vy: -0.95f, oddClock, 0.0f, 0.0f, 0.0f);

        Assert.Equal(0x3DCCCCCEu, Bits(state.LastStartHardForwardTime));
    }

    [Fact]
    public void Thrust_RejectsNullState()
    {
        Assert.Throws<ArgumentNullException>(
            () => RetailJetThrust.Thrust(null!, Powered(), 0.0f, Now, 0.0f, 0.0f, 0.0f));
        Assert.Throws<ArgumentNullException>(
            () => RetailJetThrust.Thrust(Armed(), null!, 0.0f, Now, 0.0f, 0.0f, 0.0f));
    }
}
