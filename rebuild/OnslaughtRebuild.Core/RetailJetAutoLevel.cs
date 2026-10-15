// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CBattleEngineJetPart::AutoLevel</c> — the released three-gate predicate
/// that decides whether a flying jet is allowed to right itself, as a pure
/// function of its ground contact, its velocity, its energy, and its barrel
/// roll counter.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineJetPart.cpp:1049-1061</c>. Retail
/// identity: <c>0x00412900</c> in the pristine <c>74154bfa…</c> image, file
/// offset = VA - 0x400000. Two constants are read out of <c>.rdata</c>:
/// </para>
/// <list type="bullet">
/// <item><c>0x005D8C60</c> = <c>0x3C23D70B</c> — the squared manoeuvre speed.</item>
/// <item><c>0x005D856C</c> = <c>0x00000000</c> — <c>0.0f</c>, used by the energy and barrel gates.</item>
/// </list>
/// <para>
/// <b>Source and retail agree on all three gates, including short-circuit
/// order.</b> <c>IsOnGround</c> is the virtual at <c>[vtable + 0x10C]</c> and is
/// called first; the velocity is only fetched (<c>[vtable + 0x6C]</c>, into a
/// caller-supplied slot) when it returns non-zero, which is the C <c>&amp;&amp;</c>.
/// The energy is the float at <c>mMainPart + 0xFC</c> and the barrel counter is
/// the float at <c>this + 0x48</c>.
/// </para>
/// <para>
/// <b>The barrel counter really is a float, in both the source and the
/// image.</b> <c>BattleEngineJetPart.h:105</c> declares
/// <c>float mDoingBarrelCount</c>, and <c>0x00412971</c> is
/// <c>fld dword ptr [esi + 0x48]</c>. A rebuild that types it as the integer
/// its name suggests would still pass the ordinary cases and would only part
/// company on a fractional value, so it is pinned here as a float.
/// </para>
/// <para>
/// <b>The threshold is not <c>0.01f</c>.</b>
/// <c>BattleEngineJetPart.cpp:1051</c> writes <c>0.1f*0.1f</c>, which MSVC
/// folds through <c>double</c> and rounds to <c>0x3C23D70B</c>. Plain
/// <c>0.01f</c> is <c>0x3C23D70A</c>, one ulp lower, and it is a constant that
/// really is in this image — <see cref="RetailBattleEngineGravity.ThingGravity"/>
/// loads it from <c>0x005D8574</c>. So the two live side by side in
/// <c>.rdata</c> and a rebuild that writes the tidier literal here is measurably
/// wrong.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on unordered inputs at two of the three gates,
/// and retail wins.</b> The ground gate at <c>0x00412946</c> and the energy gate
/// at <c>0x00412965</c> are <c>test ah, 1</c> — C0 alone — and an unordered
/// compare sets C0, so a NaN speed or a NaN energy returns <c>FALSE</c> where
/// the C text (<c>&lt;</c> against a NaN) would fall through to <c>TRUE</c>.
/// The barrel gate at <c>0x0041297C</c> is <c>test ah, 0x41</c> with
/// <c>jne</c> — C0 or C3, either of which an unordered compare sets — so a NaN
/// barrel counter returns <c>TRUE</c>, and there C and retail agree because
/// <c>NaN &gt; 0</c> is also false. The three are written below as the
/// comparisons that reproduce that, not as the comparisons the source text
/// reads.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>IsOnGround</c> is a virtual whose body is
/// outside this contract and arrives as a boolean. The velocity accessor writes
/// three floats into a caller slot and the squared magnitude is accumulated
/// entirely on the x87 stack with no intermediate store — the same shape
/// <see cref="RetailJetFriction.VelocityMagnitude"/> documents, and reproduced
/// the same way, in <c>double</c> and in the same association. Retail returns
/// the literal words 1 and 0 in <c>eax</c> (<c>mov eax, 1</c> at
/// <c>0x00412988</c>, <c>xor eax, eax</c> at three sites), so unlike the
/// readouts in <see cref="RetailWeaponStoreReadouts"/> this one really is a
/// normalised boolean and is modelled as one.
/// </para>
/// </remarks>
public static class RetailJetAutoLevel
{
    /// <summary>
    /// <c>0.1f*0.1f</c> as MSVC folded it — <c>0x005D8C60</c>, bits
    /// <c>0x3C23D70B</c>. One ulp above <c>0.01f</c>.
    /// </summary>
    public const float MinManoeuvreVelocitySq = 0.010000000707805157f;

    /// <summary>
    /// <c>CVector::MagnitudeSq</c> as inlined at
    /// <c>0x0041292A-0x00412938</c>: <c>(x*x + y*y) + z*z</c>, all at the
    /// ambient precision control with no rounding back to float in between.
    /// </summary>
    public static double VelocityMagnitudeSquared(
        float velocityX, float velocityY, float velocityZ)
    {
        double x = velocityX;
        double y = velocityY;
        double z = velocityZ;
        return x * x + y * y + z * z;
    }

    /// <summary>
    /// <c>CBattleEngineJetPart::AutoLevel</c> —
    /// <c>BattleEngineJetPart.cpp:1049-1061</c>, <c>0x00412900</c>.
    /// </summary>
    public static bool AutoLevel(
        bool isOnGround,
        float velocityX,
        float velocityY,
        float velocityZ,
        float energy,
        float doingBarrelCount)
    {
        if (isOnGround)
        {
            double magnitudeSq = VelocityMagnitudeSquared(velocityX, velocityY, velocityZ);

            // test ah, 1 - C0 alone, so an unordered magnitude lands here.
            if (!(magnitudeSq >= (double)MinManoeuvreVelocitySq))
            {
                return false;
            }
        }

        // test ah, 1 again: an unordered energy is "less than zero".
        if (!(energy >= 0.0f))
        {
            return false;
        }

        // test ah, 0x41 with jne: C0 or C3 returns true, so only an ordered
        // strictly-positive counter blocks the auto-level.
        return !(doingBarrelCount > 0.0f);
    }
}
