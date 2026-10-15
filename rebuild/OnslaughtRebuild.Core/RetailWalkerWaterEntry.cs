// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CBattleEngineWalkerPart::GoingIntoWater</c> — the released predicate that
/// decides whether a walking chassis is about to enter water, as a pure
/// function of its ground contact and four terrain samples.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineWalkerPart.cpp:442-460</c>. Retail
/// identity: <c>0x00413A70</c> in the pristine <c>74154bfa…</c> image, file
/// offset = VA - 0x400000. One constant is read out of <c>.rdata</c>:
/// <c>0x005D8CB4</c> = <c>0x3E99999A</c> = <c>0.3f</c>, loaded by both arms
/// (<c>0x00413AB7</c> and <c>0x00413B67</c>).
/// </para>
/// <para>
/// <b>Source and retail agree on every branch and every direction.</b>
/// <c>IsOnGround</c> is the virtual at <c>[vtable + 0x10C]</c> and
/// <c>IsOnObject</c> is the direct call at <c>0x00401FD0</c>; both are
/// short-circuited ahead of any terrain work, which is the C <c>&amp;&amp;</c>.
/// The water level is the float global at <c>0x006FBDFC</c> — the same one
/// <see cref="RetailJetFriction"/> names — and it is stored to a float slot
/// once and reused by both arms. <c>MAP.Collide</c> is <c>0x0047EB80</c> on the
/// map object at <c>0x006FADC8</c>. Every comparison is
/// <c>test ah, 0x41</c> with <c>jne</c> to the failure label, so each is a plain
/// ordered strictly-greater and an unordered sample fails it, exactly as C
/// <c>&gt;</c> does. There is no divergence to record in this body.
/// </para>
/// <para>
/// <b>The arm selector's strictness is observable, but only above a water
/// line.</b> At exactly the margin the low arm asks
/// <c>ahead - water &gt; 0.3</c> and the high arm asks
/// <c>ahead &gt; float(here)</c>; over a water line at zero those are the same
/// comparison against the same word, so a sea-level experiment cannot tell
/// <c>&gt;</c> from <c>&gt;=</c> at <c>0x00413ABF</c>. Lift the water and the
/// narrowing below becomes a real rounding and the two part company — a chassis
/// standing exactly <c>0.3</c> above a water line at <c>4</c> takes the low arm
/// and answers TRUE for ground ahead that the high arm would reject. Both cases
/// are pinned in the tests.
/// </para>
/// <para>
/// <b>The two arms are not symmetric, and the asymmetry is real.</b> When the
/// ground at the current position stands more than <c>0.3</c> above the water,
/// retail asks whether the ground <i>ahead</i> is higher than the ground
/// <i>here</i> — a climb, not a descent. Otherwise it asks whether the ground
/// ahead stands more than <c>0.3</c> above the water. That is
/// <c>BattleEngineWalkerPart.cpp:448-456</c> read literally, and it means the
/// predicate answers TRUE for a chassis walking uphill away from a shoreline.
/// Nothing here judges that; it is the shipped law.
/// </para>
/// <para>
/// <b>One of the four samples is rounded to float and three are not.</b>
/// <c>MAP.Collide</c> returns in <c>st(0)</c> at the ambient 53-bit precision.
/// The first sample at <c>0x00413AAE</c> is subtracted and compared without
/// ever being stored. The <i>second</i> call on the same position, at
/// <c>0x00413AFC</c>, is immediately <c>fstp dword ptr [esp + 8]</c> — rounded
/// to float — and only then compared against the extended sample ahead. So the
/// inner test at <c>0x00413B13</c> is
/// <c>extended(ahead) &gt; float(here)</c>, and a rebuild that keeps both wide,
/// or narrows both, disagrees whenever the two heights sit within one float ulp.
/// <see cref="GoingIntoWater(bool, bool, float, double, double)"/> takes the
/// height here <b>once</b>, as retail's two calls on an unchanged argument must
/// return, and applies the rounding at the one place retail applies it.
/// </para>
/// <para>
/// <b>The look-ahead position is float-rounded componentwise.</b>
/// <c>0x00413AF0-0x00413AF8</c> stores the three sums with <c>fstp dword</c>
/// before <c>Collide</c> ever sees them. Each sum is one x87 add of two floats
/// under 53-bit precision, which is exact, so the store is a single rounding and
/// <see cref="AdvancePosition"/> matches plain float addition — recorded rather
/// than enforced, because a rebuild that adds in float lands on the same bits.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>IsOnGround</c> and <c>IsOnObject</c> arrive
/// as booleans; their bodies are outside this contract.
/// <c>MAP.Collide</c> is terrain sampling and stays outside Core, so the four
/// heights arrive as measurements — the same treatment
/// <see cref="RetailJetFriction.Altitude"/> gives them. The first arm reads
/// <c>mMainPart + 0x1C</c> directly where the source writes <c>GetPos()</c>;
/// they coincide for a plain field accessor and no claim is made beyond that.
/// </para>
/// </remarks>
public static class RetailWalkerWaterEntry
{
    /// <summary>The shoreline margin — <c>0x005D8CB4</c>, bits <c>0x3E99999A</c>.</summary>
    public const float ShoreDepth = 0.3f;

    /// <summary>
    /// One component of <c>GetPos() + GetVelocity()</c> as retail stores it —
    /// <c>fadd</c> at the ambient precision then <c>fstp dword</c>
    /// (<c>0x00413AD4-0x00413AF8</c>).
    /// </summary>
    public static float AdvancePosition(float position, float velocity) =>
        (float)((double)position + (double)velocity);

    /// <summary>
    /// <c>CBattleEngineWalkerPart::GoingIntoWater</c> —
    /// <c>BattleEngineWalkerPart.cpp:442-460</c>, <c>0x00413A70</c>.
    /// </summary>
    /// <param name="isOnGround">The virtual at <c>[vtable + 0x10C]</c>.</param>
    /// <param name="isOnObject">The call at <c>0x00401FD0</c>.</param>
    /// <param name="waterLevel">The float global at <c>0x006FBDFC</c>.</param>
    /// <param name="groundHere"><c>MAP.Collide(pos)</c>, unrounded as retail leaves it.</param>
    /// <param name="groundAhead"><c>MAP.Collide(pos + velocity)</c>, unrounded.</param>
    public static bool GoingIntoWater(
        bool isOnGround,
        bool isOnObject,
        float waterLevel,
        double groundHere,
        double groundAhead)
    {
        if (!isOnGround || isOnObject)
        {
            return false;
        }

        if (groundHere - (double)waterLevel > (double)ShoreDepth)
        {
            // fstp dword ptr [esp + 8] at 0x00413B01: the height HERE is
            // narrowed to float before the comparison, the height AHEAD is not.
            return groundAhead > (double)(float)groundHere;
        }

        return groundAhead - (double)waterLevel > (double)ShoreDepth;
    }

    /// <summary>
    /// <see cref="GoingIntoWater(bool, bool, float, double, double)"/> for a
    /// caller that only has float samples. The inner narrowing is then a no-op,
    /// which is precisely the case that cannot distinguish the two models.
    /// </summary>
    public static bool GoingIntoWater(
        bool isOnGround,
        bool isOnObject,
        float waterLevel,
        float groundHere,
        float groundAhead) =>
        GoingIntoWater(isOnGround, isOnObject, waterLevel, (double)groundHere, (double)groundAhead);
}
