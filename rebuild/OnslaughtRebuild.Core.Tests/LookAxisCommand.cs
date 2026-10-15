// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The inverse of the released look-axis curve, for test drivers that compute a
/// desired <b>turn rate</b> and have to hand the simulation a <b>stick
/// position</b>.
///
/// <para><b>Why this exists.</b> Every autopilot in this suite is a
/// proportional controller: it measures an aim error in radians, multiplies by
/// a gain, and hands the result to <see cref="SimInput"/> as an analog look
/// axis. That was correct while Core's look axis was linear, because stick
/// position and turn rate were the same number. They are not the same number in
/// the released game — <c>references/Onslaught/Player.cpp:334-355</c> curves
/// every look axis through <c>tan(1.2*v)/tan(1.2)</c>, ported to
/// <see cref="LookAxisResponse"/>, and that curve is compressive: its slope at
/// centre is 1.2 / tan(1.2) = 0.4665, so a small commanded deflection produces
/// roughly half the rate it used to.</para>
///
/// <para><b>What a human does about it, and what this does.</b> A player who
/// wants a given rate pushes the stick until the aeroplane turns at that rate;
/// they do not push proportionally to their aim error and accept whatever
/// comes out. Inverting the curve at the point where a rate becomes a stick
/// position is that, and nothing more. It is deliberately preferred to
/// re-fitting the drivers' gains: the gains encode what the drivers want the
/// airframe to do and are separately motivated (see the remarks on
/// <c>Level100ChainAutopilot.EngageWaveTwo</c>), while this is a unit
/// conversion that stays correct if the curve is ever corrected again.</para>
///
/// <para><b>It is not a bypass of the curve.</b> Core still applies
/// <see cref="LookAxisResponse"/> to whatever this returns, on every axis, in
/// both modes. The round trip is exact to a permille for small commands, where
/// the curve is finely sampled, and full deflection is a fixed point of both
/// directions, so a saturated command is unaffected by either.</para>
/// </summary>
internal static class LookAxisCommand
{
    /// <summary>
    /// For each response magnitude in permille, the smallest input magnitude
    /// that <see cref="LookAxisResponse.Apply"/> maps to at least it. Built
    /// once by scanning the released curve rather than by re-deriving it, so
    /// the two cannot drift apart.
    /// </summary>
    private static readonly short[] InputForResponse = BuildInverse();

    /// <summary>
    /// The analog look input that produces <paramref name="responsePermille"/>
    /// of the axis's full rate. Odd-symmetric, and clamped to full deflection.
    /// </summary>
    internal static short ForResponsePermille(int responsePermille)
    {
        int magnitude = Math.Clamp(Math.Abs(responsePermille), 0, 1_000);
        short input = InputForResponse[magnitude];
        return responsePermille < 0 ? (short)-input : input;
    }

    private static short[] BuildInverse()
    {
        // LookAxisResponse.Apply is monotonic (asserted by
        // LookAxisResponseTests.ResponseIsMonotonic_AndClampsBeyondFullDeflection),
        // so one forward scan finds every threshold.
        var inverse = new short[1_001];
        int input = 0;
        for (int response = 0; response <= 1_000; response++)
        {
            while (input < 1_000 && LookAxisResponse.Apply(input) < response)
            {
                input++;
            }

            inverse[response] = (short)input;
        }

        return inverse;
    }
}
