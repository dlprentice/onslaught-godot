// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for <see cref="RetailJetFriction"/> against
/// <c>references/Onslaught/BattleEngineJetPart.cpp:609-635</c> and the pristine
/// <c>74154bfa…</c> bytes at <c>0x00411AA0</c>.
/// </summary>
public sealed class RetailJetFrictionTests
{
    private const float StationaryComponent = 0.0f;

    // The six .rdata constants the body reads, bit for bit. 0.99f and 0.98f are
    // not representable, so a rebuild that wrote 99/100 in any other form would
    // drift; the bit pattern is the contract.
    [Fact]
    public void Constants_MatchTheReadOnlyDataTheBodyLoads()
    {
        Assert.Equal(
            0x3F7D70A4u,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.NearSurfaceFriction));
        Assert.Equal(
            0x3F7AE148u,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.CruiseFriction));
        Assert.Equal(
            0x3F800000u,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.NearSurfaceAltitude));
        Assert.Equal(
            0x40400000u,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.CruiseAltitude));
        Assert.Equal(
            0x3FC00000u,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.SlowFlightSpeed));
        Assert.Equal(
            0x3C23D70Au,
            BitConverter.SingleToUInt32Bits(RetailJetFriction.AltitudeFrictionRate));
    }

    // The whole ladder against a stationary jet, driven only by altitude. The
    // altitude 1.0 row is the interesting one: the first test is strictly-less,
    // so exactly 1.0 falls through to the interpolated arm - which happens to
    // return the same 0.99f, making the ladder continuous at that seam. The
    // altitude 2.0 row reaches 0.98f bit for bit through the interpolation, not
    // through the cruise arm. 3.0 is the first altitude that is genuinely
    // cruise.
    [Theory]
    [InlineData(0.0f, 0x3F7D70A4u)]
    [InlineData(0.9999f, 0x3F7D70A4u)]
    [InlineData(1.0f, 0x3F7D70A4u)]
    [InlineData(1.25f, 0x3F7CCCCDu)]
    [InlineData(1.5f, 0x3F7C28F6u)]
    [InlineData(2.0f, 0x3F7AE148u)]
    [InlineData(2.9375f, 0x3F787AE1u)]
    [InlineData(3.0f, 0x3F7AE148u)]
    [InlineData(100.0f, 0x3F7AE148u)]
    public void GetFriction_WalksTheAltitudeLadder(float altitude, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailJetFriction.GetFriction(
                    waterLevel: altitude,
                    groundLevel: altitude,
                    positionZ: 0.0f,
                    StationaryComponent,
                    StationaryComponent,
                    StationaryComponent)));

    // The slow-flight gate at 0x00411B39 is 1.5f and it is `test ah, 1` -
    // C0-only - so the interpolated arm needs a magnitude strictly below 1.5.
    // Exactly 1.5 takes the fast arm and gets the flat 0.99f, which at altitude
    // 2 is visibly different from the 0.98f the interpolation would give.
    [Theory]
    [InlineData(0.0f, 0x3F7AE148u)]
    [InlineData(1.4999f, 0x3F7AE148u)]
    [InlineData(1.5f, 0x3F7D70A4u)]
    [InlineData(9.0f, 0x3F7D70A4u)]
    public void GetFriction_GatesTheInterpolatedArmAtOnePointFive(
        float speedAlongX, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailJetFriction.GetFriction(
                    waterLevel: 2.0f,
                    groundLevel: 2.0f,
                    positionZ: 0.0f,
                    speedAlongX,
                    StationaryComponent,
                    StationaryComponent)));

    // THE ROW THAT FOUND THE REBUILD DEFECT, kept under its original name so
    // the GOAL.md entry that cites it still resolves.
    //
    // Core's fixed-point jet model used to open the interpolated arm at a speed
    // of 1.0 in the same scale (Simulation.JetFrictionNumerator, speed < 1_000
    // against altitudes of 1_000 and 3_000), so speeds in [1.0, 1.5) below the
    // ceiling were exactly where the two models parted company. Retail's gate
    // is 1.5 at both BattleEngineJetPart.cpp:628 and 0x00411B39, and Core's is
    // now 1_500. This row is what that fix was graded against and it is still
    // the float-exact pin: 1.2 is inside the window that used to be wrong, and
    // it must interpolate to 0.98 bit for bit.
    [Fact]
    public void GetFriction_InterpolatesAtSpeedsCoreAlreadyTreatsAsFast()
    {
        Assert.Equal(
            0x3F7AE148u,
            BitConverter.SingleToUInt32Bits(
                RetailJetFriction.GetFriction(
                    waterLevel: 2.0f,
                    groundLevel: 2.0f,
                    positionZ: 0.0f,
                    1.2f,
                    StationaryComponent,
                    StationaryComponent)));
    }

    // CORE'S OWN INTEGER LADDER, AND THE ONLY FALSIFIER THE GATE HAS.
    //
    // THIS ROW EXISTS BECAUSE NO REPLAY IN THIS REPOSITORY CAN SEE THE FIX.
    // The gate divides speeds in [1_000, 1_500) at altitudes in [1_000, 3_000).
    // Core's jet throttle drives the magnitude toward a target that tops out at
    // SimulationConstants.JetMaximumSpeedPerTick = 900 (the shipped
    // mMaxAirVelocity 0.9), which is BELOW the band's floor - so steady flight
    // cannot enter the band at all, and only a transient over-speed from an
    // external impulse could. Measured: the Level 100 cold-start trace's jet
    // legs run at 140-417 mm/tick, and moving the gate from 1_000 to 1_500
    // leaves every cold-start trace hash byte-identical. A replay suite that
    // cannot distinguish the two gate values cannot defend either one, so this
    // direct row is the whole guard.
    //
    // Non-vacuity is asserted rather than assumed, the way this suite asserts
    // an exercised population elsewhere: the band is proved non-empty, every
    // probe is proved to lie inside it, and each probe is proved to return
    // something DIFFERENT from the pre-fix answer. Delete the gate, restore it
    // to 1_000, or drop it to 0 and this test fails.
    [Fact]
    public void CoreLadder_GatesTheInterpolatedArmAtOnePointFive()
    {
        const int OldGate = 1_000;
        const int ShippedGate = 1_500;
        const int InterpolatingAltitude = 2_000;

        // The band the fix is about is non-empty, and it sits entirely above
        // the jet's own maximum throttle target - which is WHY no trace reaches
        // it. If a future change raises that cap past the band, this assertion
        // fails and the vacuity argument above must be re-measured.
        Assert.True(ShippedGate > OldGate);
        Assert.True(SimulationConstants.JetMaximumSpeedPerTick < OldGate);

        int[] band = [OldGate, 1_100, 1_250, 1_499];
        Assert.NotEmpty(band);

        foreach (int speed in band)
        {
            // NON-VACUITY: this probe really is in the band that used to be
            // wrong - at or above the old gate, and below the shipped one.
            Assert.InRange(speed, OldGate, ShippedGate - 1);

            // Retail interpolates here. At altitude 2 the interpolation lands
            // exactly on the cruise numerator, which is a DIFFERENT number from
            // the flat near-surface value the old gate returned.
            int actual = Simulation.JetFrictionNumerator(InterpolatingAltitude, speed);
            Assert.Equal(SimulationConstants.JetCruiseFrictionNumerator, actual);
            Assert.NotEqual(SimulationConstants.JetNearSurfaceFrictionNumerator, actual);

            // And it agrees with the float-exact retail model bit for bit, which
            // is the parity claim rather than a self-consistent restatement.
            Assert.Equal(
                0x3F7AE148u,
                BitConverter.SingleToUInt32Bits(
                    RetailJetFriction.GetFriction(
                        waterLevel: 2.0f,
                        groundLevel: 2.0f,
                        positionZ: 0.0f,
                        speed / 1_000.0f,
                        StationaryComponent,
                        StationaryComponent)));
        }

        // The gate is `>=`, exactly as retail's C0-only compare is: 1_499
        // interpolates and 1_500 takes the flat arm.
        Assert.Equal(
            SimulationConstants.JetCruiseFrictionNumerator,
            Simulation.JetFrictionNumerator(InterpolatingAltitude, ShippedGate - 1));
        Assert.Equal(
            SimulationConstants.JetNearSurfaceFrictionNumerator,
            Simulation.JetFrictionNumerator(InterpolatingAltitude, ShippedGate));

        // The interpolated arm is retail's single line 1 - altitude*0.01 across
        // the whole band, not just at its midpoint: 0.99 at altitude 1, 0.985 at
        // 1.5, 0.98 at 2. Speed 0 keeps this reading the same arm.
        Assert.Equal(990_000, Simulation.JetFrictionNumerator(1_000, 0));
        Assert.Equal(985_000, Simulation.JetFrictionNumerator(1_500, 0));
        Assert.Equal(980_000, Simulation.JetFrictionNumerator(2_000, 0));
    }

    // The reference is the numerically SMALLER of water and ground, whichever
    // of the two it happens to be, and the chassis Z is subtracted from it. The
    // first two rows are the same geometry with the roles swapped and must
    // agree; a rebuild that always took the water level, or that took the
    // larger of the pair, reads 10.5 on one of them and returns cruise
    // friction. No claim is made here about which surface that is in world
    // terms - see the remarks on RetailJetFriction.Altitude.
    [Theory]
    [InlineData(10.0f, 0.0f, -0.5f, 0x3F7D70A4u)]
    [InlineData(0.0f, 10.0f, -0.5f, 0x3F7D70A4u)]
    [InlineData(10.0f, 0.0f, -5.0f, 0x3F7AE148u)]
    public void Altitude_MeasuresFromTheLowerOfWaterAndGround(
        float waterLevel, float groundLevel, float positionZ, uint expectedBits) =>
        Assert.Equal(
            expectedBits,
            BitConverter.SingleToUInt32Bits(
                RetailJetFriction.GetFriction(
                    waterLevel,
                    groundLevel,
                    positionZ,
                    StationaryComponent,
                    StationaryComponent,
                    StationaryComponent)));

    // Every compare in this body is `fcomp / fnstsw / test ah, 1`, which reads
    // C0 alone, and an unordered compare sets C0. So a NaN water level is
    // chosen over a finite ground level, and the NaN altitude that follows
    // takes the near-surface arm. Written the natural C# way - `waterLevel <
    // groundLevel` and `altitude < 1.0f` - both would go the other way, which
    // is the mutation this test kills.
    [Fact]
    public void GetFriction_UnorderedInputsFallTheWayC0Does()
    {
        Assert.True(float.IsNaN(RetailJetFriction.Altitude(float.NaN, 5.0f, 0.0f)));

        Assert.Equal(
            0x3F7D70A4u,
            BitConverter.SingleToUInt32Bits(
                RetailJetFriction.GetFriction(
                    waterLevel: float.NaN,
                    groundLevel: 5.0f,
                    positionZ: 0.0f,
                    StationaryComponent,
                    StationaryComponent,
                    StationaryComponent)));
    }

    // The altitude is stored back to a float at 0x00411ADF before anything
    // compares it, so the value the ladder sees is float-rounded, not the
    // extended difference.
    [Fact]
    public void Altitude_IsRoundedToFloatBeforeTheLadderSeesIt()
    {
        float altitude = RetailJetFriction.Altitude(
            waterLevel: 16777216.0f, groundLevel: 16777216.0f, positionZ: -1.0f);

        Assert.Equal(0x4B800000u, BitConverter.SingleToUInt32Bits(altitude));
        Assert.Equal(16777216.0f, altitude);
    }

    // The magnitude at 0x00411B19-0x00411B31 never leaves the x87 stack, so
    // under the CRT's 53-bit precision control every partial product and sum is
    // a double. The second assertion is the same vector accumulated in floats,
    // and it lands one ulp lower - which is the mutation (widen only at the
    // end, or round each square to float) that this pins.
    [Fact]
    public void VelocityMagnitude_AccumulatesInDoubleNotFloat()
    {
        float x = BitConverter.UInt32BitsToSingle(0x401B0C75u);
        float y = BitConverter.UInt32BitsToSingle(0x3FB1D7E5u);
        float z = BitConverter.UInt32BitsToSingle(0x40340B7Eu);

        double magnitude = RetailJetFriction.VelocityMagnitude(x, y, z);

        Assert.Equal(0x400FB66050EFFAF2UL, BitConverter.DoubleToUInt64Bits(magnitude));
        Assert.Equal(0x407DB303u, BitConverter.SingleToUInt32Bits((float)magnitude));

        float allFloat = (float)System.Math.Sqrt(x * x + y * y + z * z);
        Assert.Equal(0x407DB302u, BitConverter.SingleToUInt32Bits(allFloat));

        Assert.Equal(
            0x40140000_00000000UL,
            BitConverter.DoubleToUInt64Bits(
                RetailJetFriction.VelocityMagnitude(3.0f, 4.0f, 0.0f)));
    }
}
