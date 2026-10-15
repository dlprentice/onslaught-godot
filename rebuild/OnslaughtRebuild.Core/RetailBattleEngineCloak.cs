// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CBattleEngine::HandleCloak</c>, <c>Cloak</c> and <c>Decloak</c> — the
/// released toggle that decides whether a battle engine goes invisible, folded
/// into one body by the compiler.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngine.cpp:3096-3119</c>, with the two fields
/// declared at <c>BattleEngine.h:394</c> (<c>mCloaked</c>) and
/// <c>BattleEngine.h:456</c> (<c>mStealth,mDesiredStealth</c>) and the two
/// configuration fields at <c>BattleEngineDataManager.h:22</c>
/// (<c>mMinTransformEnergy</c>) and <c>:36</c> (<c>mStealth</c>). Retail
/// identity: <c>0x0040D4D0</c> in the pristine <c>74154bfa…</c> image, file
/// offset = VA - 0x400000. One constant is read out of <c>.rdata</c>:
/// <c>0x005D856C</c> = <c>0x00000000</c> = <c>+0.0f</c>, the same word
/// <see cref="RetailJetAutoLevel"/> and <see cref="RetailWeaponFireGate"/> load.
/// </para>
/// <para>
/// <b>Source and retail agree on every branch, and — unusually — on unordered
/// inputs too.</b> Both comparisons were checked against the condition-code mask
/// rather than assumed:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>0x0040D4FA</c> is <c>test ah, 1</c> with <c>jne</c> to the do-nothing
/// exit — C0 alone. An unordered compare sets C0, so a NaN energy takes the
/// exit, and C's <c>mEnergy &gt;= mMinTransformEnergy</c> is also false for a
/// NaN. <b>Agrees.</b>
/// </item>
/// <item>
/// <c>0x0040D50D</c> is <c>test ah, 0x41</c> with <c>jne</c> to the same exit —
/// C0 or C3, either of which an unordered compare sets. So a NaN configuration
/// stealth blocks the cloak, and C's <c>mStealth &gt; 0</c> is false for a NaN
/// too. <b>Agrees.</b>
/// </item>
/// </list>
/// <para>
/// This is worth stating positively: MSVC's <c>&gt;=</c> and <c>&gt;</c> idioms
/// are NaN-correct, and it is the <i>equality</i> and truthiness idioms that are
/// not — compare <see cref="RetailCareerGrade.GradeByteFromRanking"/>, whose
/// <c>test ah, 0x40</c> hands a NaN the top grade, and
/// <see cref="RetailJetThrust.Thrust"/>, whose <c>if (float)</c> gate reads a
/// NaN as zero.
/// </para>
/// <para>
/// <b>Decloaking is unconditional and comes first.</b> <c>0x0040D4D8</c> tests
/// <c>mCloaked</c> at <c>this + 0x4AC</c> for non-zero and, if set, writes zero
/// to <c>mDesiredStealth</c> at <c>this + 0x5DC</c> and to <c>mCloaked</c> and
/// returns — so a cloaked engine with no energy still decloaks. Neither the
/// energy nor the stealth gate is reached on that arm. This is the C
/// <c>if/else</c>, but a rebuild that checked energy first would strand a
/// player cloaked.
/// </para>
/// <para>
/// <b>The desired stealth is a raw dword copy, not an arithmetic
/// assignment.</b> <c>0x0040D512</c> is
/// <c>mov eax, dword ptr [edx + 0xA0]</c> followed by
/// <c>mov dword ptr [ecx + 0x5DC], eax</c> — the configuration's word arrives
/// bit for bit, so a signalling NaN or a trap payload would survive. It cannot
/// be a NaN in practice because the gate above rejected one; it can be a
/// subnormal, and that survives too. <see cref="HandleCloak"/> copies bits.
/// </para>
/// <para>
/// <b><c>mCloaked</c> is a stored word, and only literals go in.</b>
/// <c>0x0040D518</c> writes the literal <c>1</c> and the decloak arm writes the
/// literal <c>0</c>, but the <i>test</i> at <c>0x0040D4D8</c> is
/// <c>cmp edx, eax</c> against a zeroed register — non-zero, not
/// <c>== TRUE</c>. So an externally planted <c>2</c> decloaks, unlike
/// <see cref="RetailCareerSlots"/> whose writer insists on a literal <c>1</c>.
/// It is modelled as a raw <c>int</c> for that reason.
/// </para>
/// <para>
/// <b>Not established here.</b> What consumes <c>mDesiredStealth</c>:
/// <c>BattleEngine.cpp:3124-3126</c> turns <c>mStealth</c> into a render alpha
/// and that is presentation, outside Core. The ramp from
/// <c>mDesiredStealth</c> to <c>mStealth</c> is a separate body and is not
/// claimed here. Nothing in this contract says whether
/// <c>mMinTransformEnergy</c> and the transform gate share a threshold with the
/// walk/fly morph.
/// </para>
/// </remarks>
public sealed class RetailBattleEngineCloak
{
    /// <summary>The stealth floor — <c>fcomp dword ptr [0x005D856C]</c> at <c>0x0040D505</c>.</summary>
    public const float NoStealth = 0.0f;

    /// <summary><c>mCloaked</c> — <c>this + 0x4AC</c>. The raw word, tested for non-zero.</summary>
    public int Cloaked { get; set; }

    /// <summary><c>mDesiredStealth</c> — <c>this + 0x5DC</c>.</summary>
    public float DesiredStealth { get; set; }

    /// <summary>
    /// <c>CBattleEngine::Decloak</c> — <c>BattleEngine.cpp:3115-3119</c>,
    /// inlined at <c>0x0040D4DC</c>. Both stores are the integer zero word, so
    /// <c>mDesiredStealth</c> becomes <c>+0.0f</c> and never <c>-0.0f</c>.
    /// </summary>
    public void Decloak()
    {
        DesiredStealth = BitConverter.UInt32BitsToSingle(0u);
        Cloaked = 0;
    }

    /// <summary>
    /// <c>CBattleEngine::Cloak</c> — <c>BattleEngine.cpp:3105-3112</c>, inlined
    /// at <c>0x0040D4FF</c>.
    /// </summary>
    /// <param name="configurationStealthBits">
    /// <c>mConfiguration-&gt;mStealth</c> as its stored word, because retail
    /// copies the word rather than the value.
    /// </param>
    public void Cloak(uint configurationStealthBits)
    {
        float stealth = BitConverter.UInt32BitsToSingle(configurationStealthBits);

        // test ah, 0x41 with jne: C0 or C3, so an unordered or non-positive
        // stealth leaves both fields alone.
        if (!(stealth > NoStealth))
        {
            return;
        }

        Cloaked = 1;
        DesiredStealth = stealth;
    }

    /// <summary>
    /// <c>CBattleEngine::HandleCloak</c> — <c>BattleEngine.cpp:3096-3102</c>,
    /// <c>0x0040D4D0</c>.
    /// </summary>
    /// <param name="energy"><c>mEnergy</c> — the float at <c>this + 0xFC</c>.</param>
    /// <param name="minTransformEnergy">
    /// <c>mConfiguration-&gt;mMinTransformEnergy</c> — <c>configuration + 0x2C</c>.
    /// </param>
    /// <param name="configurationStealthBits">
    /// <c>mConfiguration-&gt;mStealth</c> — <c>configuration + 0xA0</c>, as bits.
    /// </param>
    public void HandleCloak(
        float energy, float minTransformEnergy, uint configurationStealthBits)
    {
        if (Cloaked != 0)
        {
            Decloak();
            return;
        }

        // test ah, 1 with jne: C0 alone, so an unordered energy is "less than".
        if (!(energy >= minTransformEnergy))
        {
            return;
        }

        Cloak(configurationStealthBits);
    }
}
