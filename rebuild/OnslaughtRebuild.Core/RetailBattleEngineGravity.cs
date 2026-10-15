// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>EBattleEngineState</c> — the four chassis states, with the ordinals
/// recovered from the shipped jump tables rather than taken from the header.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/BattleEngine.h:28-34</c>.
/// The header declares the four names with no explicit values, so the ordinals
/// are only a claim until something in the image indexes on them. Two shipped
/// tables do: the <c>CBattleEngine::Gravity</c> switch at <c>0x00407520</c> and
/// <c>0x00407530</c>, and the state compares inside
/// <c>CBattleEngine::AugmentWeapon</c> (<c>cmp eax, 2</c> at <c>0x0040DEA3</c>
/// selecting the walker part at <c>+0x578</c>, <c>cmp eax, 3</c> at
/// <c>0x0040DEC2</c> selecting the jet part at <c>+0x57C</c>). Both agree with
/// declaration order, so the header ordering is confirmed, not assumed.
/// </para>
/// <para>
/// <b>Reading the header without this check inverts the gravity law.</b> The
/// only state that scales gravity is ordinal 0. A reader who assumed the
/// obvious ordering — walker first — would give the plain walker the scaled
/// value and the morph the full one, which is the opposite of what ships.
/// </para>
/// </remarks>
public enum RetailBattleEngineState
{
    /// <summary><c>BATTLE_ENGINE_STATE_MORPHING_INTO_WALKER</c> — jump-table slot 0.</summary>
    MorphingIntoWalker = 0,

    /// <summary><c>BATTLE_ENGINE_STATE_MORPHING_INTO_JET</c> — jump-table slot 1.</summary>
    MorphingIntoJet = 1,

    /// <summary><c>BATTLE_ENGINE_STATE_WALKER</c> — slot 2, and the <c>cmp eax, 2</c> at <c>0x0040DEA3</c>.</summary>
    Walker = 2,

    /// <summary><c>BATTLE_ENGINE_STATE_JET</c> — slot 3, and the <c>cmp eax, 3</c> at <c>0x0040DEC2</c>.</summary>
    Jet = 3,
}

/// <summary>
/// <c>CBattleEngine::Gravity</c> and <c>CBattleEngineJetPart::Gravity</c> — the
/// released per-frame downward acceleration for a battle engine, as a pure
/// function of its dying flag, its chassis state, and (in the jet) its energy.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngine.cpp:1064-1088</c> and
/// <c>BattleEngineJetPart.cpp:507-513</c>. Retail identities in the pristine
/// <c>74154bfa…</c> image, file offset = VA - 0x400000: <c>0x004074D0</c>
/// (<c>CBattleEngine::Gravity</c>) and <c>0x004114D0</c>
/// (<c>CBattleEngineJetPart::Gravity</c>, reached by tail <c>jmp</c>). The
/// constants are read out of <c>.rdata</c>:
/// </para>
/// <list type="bullet">
/// <item><c>0x005D8574</c> = <c>0x3C23D70A</c> — <c>0.01f</c>, the inlined <c>SUPERTYPE::Gravity()</c>.</item>
/// <item><c>0x005D8BAC</c> = <c>0x3B03126F</c> — <c>0.002f</c>, the folded <c>SUPERTYPE::Gravity()*0.2f</c>.</item>
/// <item><c>0x005D8CB0</c> = <c>0x3BA3D70A</c> — <c>0.005f</c>, the dead-engine jet value.</item>
/// <item><c>0x005D856C</c> = <c>0x00000000</c> — <c>0.0f</c>, the default arm and the live-engine jet value.</item>
/// </list>
/// <para>
/// <b>Source and retail agree, but only once the enum ordinals are measured.</b>
/// The dispatch is <c>test byte ptr [ecx + 0x2C], 4</c> (the dying flag) then
/// two four-entry jump tables selected by <c>mState</c> at <c>+0x260</c>. The
/// tables read, verbatim from the image:
/// </para>
/// <list type="bullet">
/// <item>dying, <c>0x00407520</c>: <c>{0x4074FF, 0x4074FF, 0x4074FF, 0x4074E8}</c> — <c>0.01f</c>, <c>0.01f</c>, <c>0.01f</c>, jet.</item>
/// <item>alive, <c>0x00407530</c>: <c>{0x407506, 0x4074FF, 0x4074FF, 0x40750D}</c> — <c>0.002f</c>, <c>0.01f</c>, <c>0.01f</c>, jet.</item>
/// </list>
/// <para>
/// With <see cref="RetailBattleEngineState.MorphingIntoWalker"/> at ordinal 0
/// that is exactly the source: only the morph-into-walker arm is scaled, and
/// only while alive. <c>cmp eax, 3 / ja</c> guards both tables, so any state
/// above 3 falls to <c>0x00407518</c> and returns <c>0.0f</c>, which is the
/// source's trailing <c>return 0.0f</c>. Nothing reaches it from a well-formed
/// state; it is modelled because it is in the shipped code.
/// </para>
/// <para>
/// <b>The <c>*0.2f</c> is folded at compile time and the fold is exact.</b>
/// Retail loads the literal <c>0.002f</c> rather than multiplying; the float
/// nearest <c>(double)0.01f * (double)0.2f</c> is <c>0x3B03126F</c>, which is
/// what is in <c>.rdata</c>. A rebuild that multiplies at run time in float
/// lands on the same bits, so this is recorded rather than enforced.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE in the jet arm on an unordered energy, and
/// retail wins.</b> <c>BattleEngineJetPart.cpp:509</c> is
/// <c>if (mMainPart-&gt;mEnergy==0)</c>. The shipped test at <c>0x004114E1</c>
/// is <c>test ah, 0x40</c> — C3 alone — and an unordered compare sets C3, so a
/// NaN energy returns <c>0.005f</c> where C would return <c>0.0f</c>. Written
/// the natural C# way (<c>energy == 0.0f</c>) the NaN would fall the other way,
/// which is why <see cref="JetGravity"/> is written as a negated inequality.
/// </para>
/// <para>
/// <b>Not established here.</b> The dying flag is <c>mFlags &amp; 4</c> read from
/// <c>+0x2C</c>; which bit that is in <c>CThing</c>'s flag word is outside this
/// contract and is modelled as a plain boolean. <c>SUPERTYPE::Gravity()</c> is
/// inlined to a constant at all three call sites here, so this owner cannot say
/// whether <c>CThing::Gravity</c> is a constant for every subclass — only that
/// the three arms the battle engine reaches load <c>0.01f</c>. The value is
/// returned in <c>st(0)</c>, so a caller that keeps it wide sees an extended
/// value; every constant here is exactly representable in float, so the only
/// arm where that could ever matter is one that does arithmetic, and none does.
/// </para>
/// </remarks>
public static class RetailBattleEngineGravity
{
    /// <summary>Inlined <c>SUPERTYPE::Gravity()</c> — <c>0x005D8574</c>.</summary>
    public const float ThingGravity = 0.01f;

    /// <summary>Folded <c>SUPERTYPE::Gravity()*0.2f</c> — <c>0x005D8BAC</c>.</summary>
    public const float MorphingIntoWalkerGravity = 0.002f;

    /// <summary>Jet gravity with the engine out — <c>0x005D8CB0</c>.</summary>
    public const float DeadEngineJetGravity = 0.005f;

    /// <summary>Jet gravity under power, and the out-of-range default — <c>0x005D856C</c>.</summary>
    public const float NoGravity = 0.0f;

    /// <summary>
    /// <c>CBattleEngineJetPart::Gravity</c> —
    /// <c>BattleEngineJetPart.cpp:507-513</c>, <c>0x004114D0</c>. The energy is
    /// a float at <c>mMainPart + 0xFC</c>.
    /// </summary>
    public static float JetGravity(float energy)
    {
        // test ah, 0x40 reads C3, which fcomp sets for "equal" AND for
        // "unordered". C# has no single operator with that truth table, so the
        // unordered case is spelled out; writing this as `energy == 0.0f` alone
        // would model a different function on a NaN.
        return energy == 0.0f || float.IsNaN(energy) ? DeadEngineJetGravity : NoGravity;
    }

    /// <summary>
    /// <c>CBattleEngine::Gravity</c> — <c>BattleEngine.cpp:1064-1088</c>,
    /// <c>0x004074D0</c>.
    /// </summary>
    /// <param name="isDying"><c>test byte ptr [ecx + 0x2C], 4</c>.</param>
    /// <param name="state">The raw <c>mState</c> word at <c>+0x260</c>, not the
    /// enum, because the <c>ja</c> default arm at <c>0x00407518</c> is only
    /// reachable with a value outside the enum.</param>
    /// <param name="jetEnergy">Forwarded to <see cref="JetGravity"/> on the jet arm.</param>
    public static float Gravity(bool isDying, int state, float jetEnergy)
    {
        // cmp eax, 3 / ja 0x00407518 - unsigned, so a negative state is above 3.
        if ((uint)state > (uint)RetailBattleEngineState.Jet)
        {
            return NoGravity;
        }

        if (state == (int)RetailBattleEngineState.Jet)
        {
            return JetGravity(jetEnergy);
        }

        if (!isDying && state == (int)RetailBattleEngineState.MorphingIntoWalker)
        {
            return MorphingIntoWalkerGravity;
        }

        return ThingGravity;
    }

    /// <summary>
    /// <see cref="Gravity(bool, int, float)"/> over a well-formed state.
    /// </summary>
    public static float Gravity(bool isDying, RetailBattleEngineState state, float jetEnergy) =>
        Gravity(isDying, (int)state, jetEnergy);
}
