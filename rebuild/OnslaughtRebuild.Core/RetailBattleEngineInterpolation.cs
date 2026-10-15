// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CEulerAngles</c> reduced to the three floats
/// <c>GetInterpolatedEulerOrientation</c> writes.
/// </summary>
/// <remarks>
/// Retail returns the structure through a hidden pointer argument and fills it
/// with three raw dword copies of three <c>fstp dword</c> stores —
/// <c>0x0040D79F</c>, <c>0x0040D7A9</c>, <c>0x0040D7B2</c> — so all three
/// components are single-precision and are rounded exactly once. The member
/// order in the image is yaw, pitch, roll at <c>+0</c>, <c>+4</c>, <c>+8</c>.
/// </remarks>
public readonly record struct RetailEulerAngles(float Yaw, float Pitch, float Roll);

/// <summary>
/// <c>CBattleEngine::GetInterpolatedEulerOrientation</c> and the
/// <c>AngleDifference</c> it inlines three times — the released render-frame
/// interpolation of a battle engine's orientation, with the wrap that stops a
/// chassis spinning the long way round.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngine.cpp:3187-3196</c>. Retail identity:
/// <c>0x0040D660</c> in the pristine <c>74154bfa…</c> image, file offset
/// = VA - 0x400000. Three constants are read out of <c>.rdata</c>:
/// </para>
/// <list type="bullet">
/// <item><c>0x005D85C8</c> = <c>0xBFC90FDB</c> — the negative wrap threshold.</item>
/// <item><c>0x005D85E4</c> = <c>0x3FC90FDB</c> — the positive wrap threshold.</item>
/// <item><c>0x005D85E0</c> = <c>0x40C90FDB</c> — the wrap amount. Exactly four times the threshold, bit for bit, because the two differ only in the exponent field.</item>
/// </list>
/// <para>
/// The interpolation fraction is the float global at <c>0x008A9E44</c>,
/// i.e. <c>GAME.GetFrameRenderFraction()</c>, and it lives past
/// <c>0x00661000</c> so it is <c>.bss</c> — a runtime value, not a constant to
/// read. It arrives here as a parameter.
/// </para>
/// <para>
/// <b><c>AngleDifference</c> is not in the pinned drop and is measured from the
/// image alone.</b> <c>BattleEngine.cpp:3191-3193</c> calls it and
/// <c>BattleEngine.cpp:2399-2562</c> calls it again, but the function itself
/// lives in a maths header the release does not include — the same situation
/// <see cref="RetailWeaponCharge"/> is in with <c>CWeapon</c>. So the source
/// agrees with retail at the level it states (it delegates), and the body below
/// is read off <c>0x0040D66C-0x0040D6B5</c>, which the compiler repeated
/// verbatim at <c>0x0040D6D7</c> and <c>0x0040D73F</c>.
/// </para>
/// <para>
/// <b>The wrap threshold is a quarter turn, not a half turn.</b> Nothing here
/// compares against <c>±π</c>. The rule is: if the current angle is below
/// <c>-π/2</c> and the old angle is above <c>+π/2</c>, bring the old angle down
/// by a full turn; if the current angle is above <c>+π/2</c> and the old angle is
/// below <c>-π/2</c>, bring it up by one. So the correction fires only when the
/// two angles are in opposite outer quadrants — a straddle of <c>±π</c> — and
/// never when both are on the same side. A rebuild that wrapped on
/// <c>|difference| &gt; π</c> would agree on most straddles and disagree
/// whenever one angle sits in an inner quadrant.
/// </para>
/// <para>
/// <b>The correction is applied to the difference and not to the base.</b>
/// <c>0x0040D6B5-0x0040D6BF</c> is
/// <c>fxch / fsub st(1) / fmul fraction / fadd [mOldEulerAngles]</c> — the
/// adjusted old angle is used for the subtraction and then <b>discarded</b>, and
/// the value added back is the original word re-read from memory. So the answer
/// stays near the old angle and is not shifted by a turn. A rebuild that
/// interpolated between <c>current</c> and the adjusted old value would produce
/// results a full turn away on exactly the frames the wrap fires.
/// </para>
/// <para>
/// <b>Everything between the load and the store stays wide.</b> The two
/// thresholds and the wrap amount are floats widened by the x87, the subtract,
/// the multiply and the add all happen at the ambient 53-bit precision control,
/// and the single <c>fstp dword</c> is the only rounding. That matters: the
/// adjusted old angle is <c>old ± 2π_f</c> computed in double, so it is
/// <b>not</b> representable as a float in general, and a rebuild that narrowed
/// it would shift the difference. <see cref="AdjustedOldAngle"/> returns a
/// <c>double</c> for that reason.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on unordered inputs — and the divergence
/// provably cannot reach the output.</b> The masks were read rather than
/// assumed, and they are mixed:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>0x0040D679</c> (<c>current &lt; -π/2</c>) is <c>test ah, 1</c> with
/// <c>je</c> past the arm — C0 alone. An unordered compare sets C0, so a NaN
/// current takes the <i>negative</i> arm where C's <c>&lt;</c> would fall to the
/// positive one.
/// </item>
/// <item>
/// <c>0x0040D6AA</c> (<c>old &lt; -π/2</c>) is <c>test ah, 1</c> with <c>je</c>
/// past the arm — C0 again, so a NaN old angle gets <c>+2π</c> added to it where
/// C would leave it alone.
/// </item>
/// <item>
/// <c>0x0040D686</c> and <c>0x0040D69D</c> (the two <c>&gt; +π/2</c> tests) are
/// <c>test ah, 0x41</c> with <c>jne</c> — C0 or C3, so unordered fails them,
/// which is what C's <c>&gt;</c> does. Those two agree.
/// </item>
/// </list>
/// <para>
/// <b>The two divergences are not equally observable, and the difference was
/// measured.</b> The first one <i>is</i> observable, on an ordered old angle: a
/// NaN current angle with an old angle above <c>+π/2</c> takes the negative arm
/// and moves the old angle by a full turn, where the C reading takes the positive
/// arm and leaves it alone. <see cref="AdjustedOldAngle"/> answers
/// <c>3.0 - 2π</c> against <c>3.0</c>, and the tests pin it. The second one is
/// <b>not</b> observable through any surface: it only fires when the <i>old</i>
/// angle is itself unordered, and <c>NaN + 2π</c> is <c>NaN</c>, so nothing
/// downstream can tell whether the add happened. Writing that one gate the way
/// the C text reads survives the whole suite, which is a proof of equivalence
/// rather than a missing test — and it is recorded here so a reader does not
/// mistake the surviving mutant for uncovered ground. Neither divergence can
/// change <see cref="InterpolateAngle"/>: every path a NaN reaches ends in a
/// subtraction from a NaN, so the component is NaN on all four arms.
/// </para>
/// <para>
/// <b>Not established here.</b> Where <c>mCurrentOrientation</c>
/// (<c>this + 0x114</c>) and <c>mOldEulerAngles</c> (<c>this + 0x590</c>) are
/// written, and whether either is ever outside <c>[-π, π]</c>. If a caller can
/// store an angle beyond one turn the single correction is not enough to
/// normalise it, and retail makes no second pass. The sibling
/// <c>GetInterpolatedAutoAimPos</c> (<c>0x0040D7C0</c>,
/// <c>BattleEngine.cpp:3199-3214</c>) interpolates through <c>FVector</c> and
/// <c>FMatrix</c>, which are absent from the drop, and is deliberately not
/// modelled.
/// </para>
/// </remarks>
public static class RetailBattleEngineInterpolation
{
    /// <summary>
    /// The quarter-turn straddle threshold — <c>0x005D85E4</c>, bits
    /// <c>0x3FC90FDB</c>. The negative form at <c>0x005D85C8</c> is the same
    /// word with the sign bit set.
    /// </summary>
    public const float WrapThreshold = 1.5707963705062866f;

    /// <summary>
    /// The full turn a straddling old angle is moved by — <c>0x005D85E0</c>,
    /// bits <c>0x40C90FDB</c>.
    /// </summary>
    public const float WrapAmount = 6.2831854820251465f;

    /// <summary>
    /// The old angle after the straddle correction —
    /// <c>0x0040D66C-0x0040D6B5</c>, at the ambient precision and never stored
    /// back to a float. See the type remarks for the two unordered arms.
    /// </summary>
    public static double AdjustedOldAngle(float currentAngle, float oldAngle)
    {
        double old = oldAngle;

        // test ah, 1 with je past the arm: C0 alone, so an unordered current
        // angle lands on the negative side.
        if (!(currentAngle >= -WrapThreshold))
        {
            // test ah, 0x41 with jne to the shared tail: strictly greater, and
            // an unordered old angle fails it.
            if (oldAngle > WrapThreshold)
            {
                return old - (double)WrapAmount;
            }

            return old;
        }

        // test ah, 0x41 with jne to the tail: strictly greater, unordered fails.
        if (!(currentAngle > WrapThreshold))
        {
            return old;
        }

        // test ah, 1 with je past the arm: C0 alone, so an unordered old angle
        // gets the turn added.
        if (!(oldAngle >= -WrapThreshold))
        {
            return old + (double)WrapAmount;
        }

        return old;
    }

    /// <summary>
    /// <c>AngleDifference(current, old)</c> as inlined —
    /// <c>fxch / fsub st(1)</c> at <c>0x0040D6B5</c>.
    /// </summary>
    public static double AngleDifference(float currentAngle, float oldAngle) =>
        (double)currentAngle - AdjustedOldAngle(currentAngle, oldAngle);

    /// <summary>
    /// One component of <c>GetInterpolatedEulerOrientation</c> —
    /// <c>old + AngleDifference(current, old) * fraction</c>, all wide, rounded
    /// once by the <c>fstp dword</c>.
    /// </summary>
    public static float InterpolateAngle(
        float currentAngle, float oldAngle, float frameRenderFraction) =>
        (float)(AngleDifference(currentAngle, oldAngle) * (double)frameRenderFraction +
            (double)oldAngle);

    /// <summary>
    /// <c>CBattleEngine::GetInterpolatedEulerOrientation</c> —
    /// <c>BattleEngine.cpp:3187-3196</c>, <c>0x0040D660</c>.
    /// </summary>
    /// <param name="currentOrientation"><c>mCurrentOrientation</c> — <c>this + 0x114</c>.</param>
    /// <param name="oldEulerAngles"><c>mOldEulerAngles</c> — <c>this + 0x590</c>.</param>
    /// <param name="frameRenderFraction">
    /// <c>GAME.GetFrameRenderFraction()</c> — the <c>.bss</c> float at
    /// <c>0x008A9E44</c>.
    /// </param>
    public static RetailEulerAngles GetInterpolatedEulerOrientation(
        RetailEulerAngles currentOrientation,
        RetailEulerAngles oldEulerAngles,
        float frameRenderFraction) =>
        new RetailEulerAngles(
            InterpolateAngle(currentOrientation.Yaw, oldEulerAngles.Yaw, frameRenderFraction),
            InterpolateAngle(currentOrientation.Pitch, oldEulerAngles.Pitch, frameRenderFraction),
            InterpolateAngle(currentOrientation.Roll, oldEulerAngles.Roll, frameRenderFraction));
}
