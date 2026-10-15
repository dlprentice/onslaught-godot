// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CBattleEngineJetPart::GetFriction</c> — the released per-frame velocity
/// retention for a flying jet, as a pure function of the surface heights below
/// it, its own height, and its velocity.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineJetPart.cpp:609-635</c>. Retail
/// identity: <c>0x00411AA0</c> in the pristine <c>74154bfa…</c> image, file
/// offset = VA - 0x400000. The constants are read out of <c>.rdata</c>:
/// </para>
/// <list type="bullet">
/// <item><c>0x005D8CC4</c> = <c>0x3F7D70A4</c> — <c>0.99f</c>.</item>
/// <item><c>0x005D8B9C</c> = <c>0x3F7AE148</c> — <c>0.98f</c>.</item>
/// <item><c>0x005D8568</c> = <c>0x3F800000</c> — <c>1.0f</c>, both the low altitude bound and the subtrahend base.</item>
/// <item><c>0x005D8CC0</c> = <c>0x40400000</c> — <c>3.0f</c>.</item>
/// <item><c>0x005D8BD8</c> = <c>0x3FC00000</c> — <c>1.5f</c>, the slow-flight gate.</item>
/// <item><c>0x005D8574</c> = <c>0x3C23D70A</c> — <c>0.01f</c>.</item>
/// </list>
/// <para>
/// <b>Source and retail agree</b> on the whole ladder, including the direction
/// of every comparison. The water/ground choice at <c>0x00411AC8</c> is
/// <c>test ah, 1</c> — C0 alone, so strictly-less, matching
/// <c>if (waterLevel&lt;groundLevel)</c>. The altitude subtraction is
/// <c>fsub dword ptr [ecx + 0x24]</c> against the chassis position's Z and is
/// then <b>stored back to a float</b> (<c>fstp dword ptr [esp]</c> at
/// <c>0x00411ADF</c>) before any comparison, which is what makes
/// <c>float altitude</c> in the source exact rather than approximate. The
/// magnitude at <c>0x00411B19-0x00411B31</c> is
/// <c>sqrt((x*x + y*y) + z*z)</c> accumulated entirely on the x87 stack with no
/// intermediate store, i.e. at the ambient 53-bit precision control, and this
/// type reproduces that by accumulating in <c>double</c> in the same order.
/// </para>
/// <para>
/// <b>Every comparison is C0-only, so unordered inputs fall the "less"
/// way.</b> All three tests are <c>fcomp</c> / <c>fnstsw</c> /
/// <c>test ah, 1</c>. An unordered compare sets C0, so a NaN altitude takes the
/// <c>altitude &lt; 1</c> arm and returns <c>0.99f</c>, and a NaN velocity
/// magnitude takes the slow arm and interpolates. C# <c>&lt;</c> is false for
/// NaN, which is why the tests below are written as negated
/// greater-or-equals — writing them the natural way would silently model a
/// different function on the unordered inputs.
/// </para>
/// <para>
/// <b>The interpolated arm is one line, not two segments.</b>
/// <c>0x00411B4A</c> multiplies the altitude by <c>0.01f</c> and
/// <c>fsubr</c>s it from <c>1.0f</c>: friction falls linearly from
/// <c>0.99</c> at altitude 1 to <c>0.97</c> at altitude 3, and it is only
/// reachable below altitude 3, so <c>0.97</c> is approached but never returned.
/// </para>
/// <para>
/// <b>Core's fixed-point jet model now uses this same slow-flight gate.</b>
/// <c>Simulation.JetFrictionNumerator</c> admitted the interpolated arm at
/// <c>speed &lt; 1_000</c> — retail's 1.0 — in the milli-unit scale that gives
/// it altitudes of <c>1_000</c> and <c>3_000</c> for retail's 1 and 3, so the
/// two models disagreed for speeds in <c>[1.0, 1.5)</c> at altitudes in
/// <c>[1, 3)</c>, where retail interpolates and Core returned a flat
/// <c>0.99</c>. That gate is now <c>1_500</c>, from <c>1.5f</c> at both
/// <c>BattleEngineJetPart.cpp:628</c> and <c>0x00411B39</c>. What is still
/// deliberately different is only the arithmetic: Core's ladder is integer
/// fixed point in millimetres, and this owner is the float-exact model.
/// </para>
/// <para>
/// <b>Not established here.</b> Two of the three inputs come from outside Core
/// in retail: <c>MAP.GetWaterLevel()</c> reads the float global at
/// <c>0x006FBDFC</c>, and <c>MAP.Collide</c> (<c>0x0047EB80</c>) is terrain
/// sampling. The water level is a float and arrives here as one; the ground
/// level is returned in <c>st(0)</c> as an extended value and is compared
/// against the float water level <b>before</b> either is rounded, so the model
/// below — which takes both as floats — can differ from retail only when the
/// two levels are within one float ulp of each other. The falsifier is to
/// sample <c>Collide</c>'s raw <c>st(0)</c> in a live pristine run at a
/// shoreline where the two heights coincide. Retail also returns the friction
/// in <c>st(0)</c>, so the interpolated arm hands the caller an unrounded value
/// that this type rounds to float; the same nuance
/// <see cref="RetailMovieCameraZoom"/> documents applies, and for the same
/// reason it is not modelled here.
/// </para>
/// </remarks>
public static class RetailJetFriction
{
    /// <summary>Friction near a surface, and for fast flight below the ceiling — <c>0x005D8CC4</c>.</summary>
    public const float NearSurfaceFriction = 0.99f;

    /// <summary>Cruise friction, at or above the ceiling — <c>0x005D8B9C</c>.</summary>
    public const float CruiseFriction = 0.98f;

    /// <summary>Altitude below which friction is flat — <c>0x005D8568</c>.</summary>
    public const float NearSurfaceAltitude = 1.0f;

    /// <summary>Altitude at or above which cruise friction applies — <c>0x005D8CC0</c>.</summary>
    public const float CruiseAltitude = 3.0f;

    /// <summary>Speed at or above which the interpolated arm is skipped — <c>0x005D8BD8</c>.</summary>
    public const float SlowFlightSpeed = 1.5f;

    /// <summary>Friction lost per unit of altitude on the interpolated arm — <c>0x005D8574</c>.</summary>
    public const float AltitudeFrictionRate = 0.01f;

    /// <summary>
    /// <c>BattleEngineJetPart.cpp:612-618</c>, <c>0x00411AA3-0x00411ADF</c>.
    /// The <b>numerically smaller</b> of water and ground is the reference and
    /// the chassis Z is subtracted from it, and the difference is rounded to
    /// float before anything reads it.
    /// </summary>
    /// <remarks>
    /// Which surface "numerically smaller" picks in world terms depends on the
    /// sign of the retail Z axis, and that is not established here: all that is
    /// measured is <c>fcomp</c> then <c>fsub dword ptr [ecx + 0x24]</c>. Core's
    /// own jet model reaches the mirror-image expression
    /// (<c>elevation - Math.Max(ground, water)</c>), which is consistent with
    /// the two working in opposite Z conventions rather than disagreeing; that
    /// reading is untested and only the arithmetic above is claimed.
    /// </remarks>
    public static float Altitude(float waterLevel, float groundLevel, float positionZ)
    {
        // test ah, 1 again: unordered picks the water level, not the ground.
        float reference = !(waterLevel >= groundLevel) ? waterLevel : groundLevel;
        return (float)((double)reference - (double)positionZ);
    }

    /// <summary>
    /// <c>CVector::Magnitude</c> as inlined at <c>0x00411B19-0x00411B31</c>:
    /// <c>(x*x + y*y) + z*z</c> then <c>fsqrt</c>, all at the ambient
    /// precision control, with no rounding back to float in between.
    /// </summary>
    public static double VelocityMagnitude(float velocityX, float velocityY, float velocityZ)
    {
        double x = velocityX;
        double y = velocityY;
        double z = velocityZ;
        return System.Math.Sqrt(x * x + y * y + z * z);
    }

    /// <summary>
    /// <c>CBattleEngineJetPart::GetFriction</c> —
    /// <c>BattleEngineJetPart.cpp:609-635</c>, <c>0x00411AA0</c>.
    /// </summary>
    public static float GetFriction(
        float waterLevel,
        float groundLevel,
        float positionZ,
        float velocityX,
        float velocityY,
        float velocityZ)
    {
        float altitude = Altitude(waterLevel, groundLevel, positionZ);

        // test ah, 1 - C0 alone, so unordered falls here too.
        if (!(altitude >= NearSurfaceAltitude))
        {
            return NearSurfaceFriction;
        }

        if (!(altitude >= CruiseAltitude))
        {
            double speed = VelocityMagnitude(velocityX, velocityY, velocityZ);
            if (speed >= (double)SlowFlightSpeed)
            {
                return NearSurfaceFriction;
            }

            return (float)(1.0 - (double)altitude * (double)AltitudeFrictionRate);
        }

        return CruiseFriction;
    }
}
