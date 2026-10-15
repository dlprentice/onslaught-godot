// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailAnalogueControls"/> against
/// <c>references/Onslaught/PCController.cpp:152-191</c>,
/// <c>Controller.cpp:218-417</c>, and the pristine <c>74154bfa…</c> bytes at
/// <c>0x00514640</c>, <c>0x00514670</c>, <c>0x005146A0</c>, <c>0x005146D0</c>
/// and <c>0x0042DB40</c>.
/// </summary>
public sealed class RetailAnalogueControlsTests
{
    // Pins the scale factor as BITS, not as a decimal. Retail's fmul operand at
    // 0x005DC6E4 is 0x3A83126F and its lRz scale at 0x005D8DE4 is 0x38000000
    // (exactly 2^-15); the re-centring subtrahend at 0x005E4928 is 0x47000000.
    // A rebuild that wrote 1.0f/1000.0f would produce the same bits by luck -
    // the point of this row is that the LAW is a multiply, which the next test
    // proves is a different function from the source's divide.
    [Fact]
    public void Constants_MatchThePristineOperandsBitForBit()
    {
        Assert.Equal(
            0x3A83126Fu,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.AxisScale));
        Assert.Equal(
            0x38000000u,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.RightYScale));
        Assert.Equal(
            0x47000000u,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.RightYCentre));
        Assert.Equal(
            0x3F666666u,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.ActAsDigitalThreshold));
        Assert.Equal(
            0x3F000000u,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.InitialRepeatDelay));
        Assert.Equal(
            0x3DF5C28Fu,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.RepeatDelay));
    }

    // THE divergence test. PCController.cpp:155 says lx / 1000.0f; the shipped
    // code at 0x00514640 says lx * 0.001f. Raw 5 is the smallest input where
    // those disagree: retail gives 0x3BA3D70B and a faithful divide gives
    // 0x3BA3D70A. 584 of the 1000 raw values in 1..1000 disagree, so this is the
    // normal case, not a corner. Implementing the source text fails every
    // divergent row here; implementing the multiply passes all of them,
    // including the exactly-representable ones where both agree.
    [Theory]
    [InlineData(5, 0x3BA3D70Bu, 0x3BA3D70Au)]
    [InlineData(9, 0x3C1374BDu, 0x3C1374BCu)]
    [InlineData(10, 0x3C23D70Bu, 0x3C23D70Au)]
    [InlineData(11, 0x3C343959u, 0x3C343958u)]
    [InlineData(-5, 0xBBA3D70Bu, 0xBBA3D70Au)]
    public void NormalizeLeftX_MultipliesByTheRoundedReciprocalNotDividesByAThousand(
        int rawAxis, uint retailBits, uint sourceTextBits)
    {
        Assert.NotEqual(retailBits, sourceTextBits);
        Assert.Equal(
            retailBits,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.NormalizeLeftX(rawAxis)));
        Assert.Equal(
            sourceTextBits,
            BitConverter.SingleToUInt32Bits(rawAxis / 1000.0f));
    }

    // Pins that all three signed axes share one law, and pins the exactly
    // representable landmarks where the divergence vanishes: full deflection is
    // bit-exact 1.0f, so a rebuild cannot be caught out at the stops.
    [Theory]
    [InlineData(1000, 0x3F800000u)]
    [InlineData(-1000, 0xBF800000u)]
    [InlineData(500, 0x3F000000u)]
    [InlineData(250, 0x3E800000u)]
    [InlineData(768, 0x3F449BA6u)]
    [InlineData(0, 0x00000000u)]
    public void NormalizeSignedAxes_AgreeWithEachOtherAndPinTheirLandmarks(
        int rawAxis, uint expectedBits)
    {
        float x = RetailAnalogueControls.NormalizeLeftX(rawAxis);
        Assert.Equal(expectedBits, BitConverter.SingleToUInt32Bits(x));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(x),
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.NormalizeLeftY(rawAxis)));
        Assert.Equal(
            BitConverter.SingleToUInt32Bits(x),
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.NormalizeRightX(rawAxis)));
    }

    // Pins the fourth axis's completely different shape (PCController.cpp:186-190
    // / 0x005146D0): lRz arrives as an unsigned 0..65535 range and is re-centred
    // and halved, so raw 0 is -1.0f and raw 32768 is dead centre - where the
    // other three axes would read 0.0f and 32.768f respectively. Both the source
    // and the binary agree here, and the scale is a power of two so the divide
    // and the multiply are the same function.
    [Theory]
    [InlineData(0, 0xBF800000u)]
    [InlineData(16_384, 0xBF000000u)]
    [InlineData(32_768, 0x00000000u)]
    [InlineData(49_152, 0x3F000000u)]
    [InlineData(65_535, 0x3F7FFE00u)]
    [InlineData(40_000, 0x3E620000u)]
    public void NormalizeRightY_RecentresAnUnsignedAxisAroundZero(
        int rawAxis, uint expectedBits)
    {
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.NormalizeRightY(rawAxis)));
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits((rawAxis - 32_768.0f) / 32_768.0f));
    }

    // Pins the four analogue arms of DoMappings and the fact that all four
    // comparisons are STRICT (0x0042DDFA, 0x0042DE31, 0x0042DE64, 0x0042DEEF):
    // a centred axis fires nothing, and an axis sitting exactly on +-0.9f does
    // not arm the act-as-digital repeat. Any >= or <= here fails a row.
    [Theory]
    [InlineData(0.0f, false, false, false, false)]
    [InlineData(0.0001f, true, false, false, false)]
    [InlineData(-0.0001f, false, true, false, false)]
    [InlineData(0.5f, true, false, false, false)]
    [InlineData(0.9f, true, false, false, false)]
    [InlineData(0.90001f, true, false, true, false)]
    [InlineData(-0.9f, false, true, false, false)]
    [InlineData(-0.90001f, false, true, false, true)]
    [InlineData(1.0f, true, false, true, false)]
    public void AnalogueArms_AreAllStrictComparisons(
        float value, bool plus, bool minus, bool plusRepeat, bool minusRepeat)
    {
        Assert.Equal(plus, RetailAnalogueControls.AnaloguePlusFires(value));
        Assert.Equal(minus, RetailAnalogueControls.AnalogueMinusFires(value));
        Assert.Equal(plusRepeat, RetailAnalogueControls.AnaloguePlusRepeatArms(value));
        Assert.Equal(minusRepeat, RetailAnalogueControls.AnalogueMinusRepeatArms(value));
    }

    // Pins the REFUTATION, and would fail the moment someone "restores" the
    // source's dead zone. Controller.cpp:227-233 zeroes any axis under 0.36f;
    // 0.36f (0x3EB851EC) occurs nowhere in the 2,506,752-byte pristine image,
    // nor does the double 0.36, and 0x0042DB40 has no axis read or magnitude
    // compare in its prologue at all. So a raw 300 must survive as 0.3f and not
    // collapse to zero. This pins the absence, not a replacement law: what the
    // shipped build does about a resting stick is still open.
    [Fact]
    public void NoZeroPointThreeSixDeadZoneSurvivesIntoTheReleasedMapping()
    {
        float justUnderTheSourceDeadZone = RetailAnalogueControls.NormalizeLeftX(300);

        Assert.Equal(
            0x3E99999Au,
            BitConverter.SingleToUInt32Bits(justUnderTheSourceDeadZone));
        Assert.NotEqual(0.0f, justUnderTheSourceDeadZone);
        Assert.True(RetailAnalogueControls.AnaloguePlusFires(justUnderTheSourceDeadZone));
        Assert.Equal(
            0x00000000u,
            BitConverter.SingleToUInt32Bits(RetailAnalogueControls.AbsentPadAxis));
    }
}
