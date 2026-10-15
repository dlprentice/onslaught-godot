// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The <c>CBattleEngineJetPart</c> fields <c>Thrust</c> reads and writes.
/// </summary>
/// <remarks>
/// Offsets from the pristine <c>74154bfa…</c> image, file offset = VA - 0x400000,
/// every one of them used by <c>0x00410310</c>. They reproduce
/// <c>references/Onslaught/BattleEngineJetPart.h:90-107</c> in declaration
/// order without a gap, which is what makes the identification safe:
/// <c>mMainPart</c> <c>+0x18</c>, <c>mFlightModel</c> <c>+0x1C</c>,
/// <c>mThrusterValue</c> <c>+0x20</c>, <c>mLastMoveYVal</c> <c>+0x24</c>,
/// <c>mLastMoveXVal</c> <c>+0x28</c>, <c>mDoingLoop</c> <c>+0x2C</c>,
/// <c>mLoopHalfway</c> <c>+0x30</c>, <c>mLoopBroken</c> <c>+0x34</c>, the three
/// other hard-direction stamps <c>+0x38</c>, <c>+0x3C</c>, <c>+0x40</c>,
/// <c>mLastStartHardForwardTime</c> <c>+0x44</c>, <c>mDoingBarrelCount</c>
/// <c>+0x48</c> — the last being the float
/// <see cref="RetailJetAutoLevel"/> already measures.
/// </remarks>
public sealed class RetailJetThrustState
{
    /// <summary><c>mThrusterValue</c> — <c>this + 0x20</c>. Initialised to <c>0.5f</c> at <c>BattleEngineJetPart.cpp:49</c>.</summary>
    public float ThrusterValue { get; set; } = RetailJetThrust.ThrusterCentre;

    /// <summary><c>mLastMoveYVal</c> — <c>this + 0x24</c>.</summary>
    public float LastMoveYVal { get; set; }

    /// <summary><c>mDoingLoop</c> — <c>this + 0x2C</c>. The raw <c>BOOL</c> word.</summary>
    public int DoingLoop { get; set; }

    /// <summary><c>mLoopHalfway</c> — <c>this + 0x30</c>.</summary>
    public int LoopHalfway { get; set; }

    /// <summary><c>mLoopBroken</c> — <c>this + 0x34</c>.</summary>
    public int LoopBroken { get; set; }

    /// <summary><c>mLastStartHardForwardTime</c> — <c>this + 0x44</c>. <c>-10.0f</c> at <c>BattleEngineJetPart.cpp:43</c>.</summary>
    public float LastStartHardForwardTime { get; set; } = -10.0f;

    /// <summary><c>mDoingBarrelCount</c> — <c>this + 0x48</c>, a float.</summary>
    public float DoingBarrelCount { get; set; }
}

/// <summary>
/// The <c>CBattleEngine</c> fields <c>Thrust</c> reaches through
/// <c>mMainPart</c>.
/// </summary>
/// <remarks>
/// <c>mEnergy</c> at <c>+0xFC</c> is the same float
/// <see cref="RetailJetAutoLevel"/> and <see cref="RetailBattleEngineCloak"/>
/// read. <c>mPitchvel</c> at <c>+0x280</c> and <c>mLowEnergyStartTime</c> at
/// <c>+0x2E4</c> are new here; the latter is <c>BattleEngine.h:376</c>, seeded to
/// <c>-20.0f</c> at <c>BattleEngine.cpp:268</c>.
/// </remarks>
public sealed class RetailJetThrustMainPart
{
    /// <summary><c>mEnergy</c> — <c>mMainPart + 0xFC</c>.</summary>
    public float Energy { get; set; }

    /// <summary><c>mPitchvel</c> — <c>mMainPart + 0x280</c>.</summary>
    public float Pitchvel { get; set; }

    /// <summary><c>mLowEnergyStartTime</c> — <c>mMainPart + 0x2E4</c>.</summary>
    public float LowEnergyStartTime { get; set; } = -20.0f;
}

/// <summary>
/// <c>CBattleEngineJetPart::Thrust</c> — the released throttle law, and the
/// double-tap that starts a loop.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/BattleEngineJetPart.cpp:64-106</c>, with
/// <c>kMinManoeuvreVelocitySq</c> defined at
/// <c>BattleEngineJetPart.cpp:22</c>. Retail identity: <c>0x00410310</c> in the
/// pristine <c>74154bfa…</c> image, file offset = VA - 0x400000. Nine
/// constants are read out of <c>.rdata</c>:
/// </para>
/// <list type="bullet">
/// <item><c>0x005D856C</c> = <c>0x00000000</c> — <c>+0.0f</c>, for both energy tests.</item>
/// <item><c>0x005D85EC</c> = <c>0x3F000000</c> — <c>0.5f</c>, used twice.</item>
/// <item><c>0x005D8C8C</c> = <c>0xBF19999A</c> — <c>-0.6f</c>.</item>
/// <item><c>0x005D8BB4</c> = <c>0xBF666666</c> — <c>-0.9f</c>.</item>
/// <item><c>0x005D85F8</c> = <c>0x3F4CCCCD</c> — <c>0.8f</c>.</item>
/// <item><c>0x005D8604</c> = <c>0x3E4CCCCD</c> — <c>0.2f</c>.</item>
/// <item><c>0x005D85C0</c> = <c>0x3DCCCCCD</c> — <c>0.1f</c>.</item>
/// <item><c>0x005D8C88</c> = <c>0x3DB851EC</c> — the squared manoeuvre speed.</item>
/// <item><c>0x005D8C2C</c> = <c>0x3C75C28F</c> — <c>0.015f</c>.</item>
/// </list>
/// <para>
/// The clock is the float global at <c>0x00672FD0</c>, the same
/// <c>EVENT_MANAGER</c> time word <see cref="RetailWeaponAugment"/> and
/// <see cref="RetailMovieCameraZoom"/> read; it is <c>.bss</c> and arrives as a
/// parameter.
/// </para>
/// <para>
/// <b>Retail has a gate the source does not, and retail wins.</b>
/// <c>BattleEngineJetPart.cpp:78-80</c> asks two things —
/// <c>vy &gt; 0.8f</c> and
/// <c>mLastStartHardForwardTime &gt; EVENT_MANAGER.GetTime() - 0.2f</c>. The
/// shipped body asks <b>three</b>: after the <c>0.2f</c> test at
/// <c>0x004103A9-0x004103B8</c> it loads the clock again, subtracts
/// <c>0x005D85C0</c> (<c>0.1f</c>) at <c>0x004103C4</c>, and requires
/// <c>mLastStartHardForwardTime &lt; now - 0.1f</c>
/// (<c>0x004103CC-0x004103D3</c>, <c>je</c> past the whole loop body). The raw
/// bytes are
/// <c>D9 05 D0 2F 67 00 / D8 25 C0 85 5D 00 / D9 C9 / DE D9 / DF E0 / F6 C4 01 /
/// 0F 84 9C 00 00 00</c>. So the loop trigger needs the hard-forward stamp inside
/// the <b>open interval</b> <c>(now - 0.2, now - 0.1)</c>: pulling back within
/// 100 ms of the forward push does nothing, and after 200 ms it is too late. The
/// released game therefore has a 100 ms dead band the Xbox source does not, and
/// a rebuild written from the source alone would let the player loop
/// instantly.
/// </para>
/// <para>
/// <b>Source and retail agree on the manoeuvre threshold, and it is not the one
/// <see cref="RetailJetAutoLevel"/> uses.</b> <c>0x005D8C88</c> is
/// <c>0x3DB851EC</c>, which is <c>0.3f*0.3f</c> folded through <c>double</c> —
/// <c>BattleEngineJetPart.cpp:22</c> exactly — and also happens to be
/// <c>float(0.09)</c>, so there is no ulp trap here.
/// <c>CBattleEngineJetPart::AutoLevel</c> loads <c>0x005D8C60</c> instead, which
/// is <c>0.1f*0.1f</c>. Two sites, two thresholds, ninefold apart; the macro name
/// belongs to this one. A rebuild that shared a single constant between the two
/// would be wrong at whichever site it did not measure.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on unordered inputs at two gates, and retail
/// wins.</b> Every comparison's mask was read:
/// </para>
/// <list type="bullet">
/// <item>
/// <c>0x00410321</c> (<c>mDoingBarrelCount &gt; 0</c>) is
/// <c>test ah, 0x41</c> with <c>je</c> to the early exit — the exit is taken only
/// when the compare is <i>ordered and strictly greater</i>, so a NaN barrel
/// count falls through into the body. C's <c>&gt;</c> does the same.
/// <b>Agrees.</b>
/// </item>
/// <item>
/// <c>0x00410346</c> (<c>if (mMainPart-&gt;mEnergy)</c>) is
/// <c>test ah, 0x40</c> with <c>jne</c> past the body — <b>C3 alone</b>. An
/// unordered compare sets C3, so a NaN energy is treated as <b>zero energy</b>
/// and the whole throttle body is skipped. The C text is a float used as a
/// truth value, which is <c>!= 0.0</c> and is <b>true</b> for a NaN.
/// <b>Diverges.</b> This is the same non-NaN-correct equality idiom that hands
/// <see cref="RetailCareerGrade.GradeByteFromRanking"/> a perfect grade.
/// </item>
/// <item>
/// <c>0x0041036D</c> (<c>mLastMoveYVal &gt; -0.6f</c>) is
/// <c>test ah, 0x41</c> with <c>jne</c> past the arm. <b>Agrees.</b>
/// </item>
/// <item>
/// <c>0x0041037E</c> (<c>vy &lt; -0.9f</c>) is <c>test ah, 1</c> with <c>je</c>
/// past the arm — C0 alone, which an unordered compare sets. So a NaN
/// <paramref name="vy"/> <b>stamps the hard-forward time</b> where C would not.
/// <b>Diverges.</b>
/// </item>
/// <item>
/// <c>0x00410397</c> (<c>vy &gt; 0.8f</c>) and <c>0x004103B5</c>
/// (the <c>0.2f</c> window) are <c>test ah, 0x41</c> with <c>jne</c>.
/// <b>Agree.</b>
/// </item>
/// <item>
/// <c>0x004103D0</c> (the retail-only <c>0.1f</c> window) is
/// <c>test ah, 1</c> with <c>je</c> — C0 alone, so an unordered stamp passes it.
/// There is no source text to diverge from.
/// </item>
/// <item>
/// <c>0x004103E7</c> (<c>mMainPart-&gt;mEnergy &gt; 0</c>) and <c>0x0041041A</c>
/// (the squared speed) are <c>test ah, 0x41</c> with <c>jne</c>. <b>Agree</b> —
/// and the energy one is unreachable with a NaN, because the truthiness gate
/// above already rejected it.
/// </item>
/// </list>
/// <para>
/// <b>The early return is the only exit that does not record the input.</b>
/// <c>0x00410324</c> jumps to <c>0x0041047C</c>, which is
/// <c>pop esi / add esp, 0x10 / ret 4</c> — past the
/// <c>mov eax, [esp + 0x18] / mov [esi + 0x24], eax</c> at <c>0x00410475</c>
/// that every other exit runs. So a barrel-rolling jet's
/// <c>mLastMoveYVal</c> is frozen at whatever it was when the roll began, which
/// is what makes the <c>-0.6f</c> arm fire on the first frame after the roll
/// ends. The source's bare <c>return;</c> ahead of
/// <c>BattleEngineJetPart.cpp:105</c> says the same thing; it is called out
/// because it is easy to lose in a rebuild that clears state at the top.
/// </para>
/// <para>
/// <b>Three stores are raw dword copies and preserve exact bit patterns.</b>
/// <c>mLastMoveYVal</c> (<c>0x00410475</c>), <c>mLastStartHardForwardTime</c>
/// (<c>0x00410383</c>) and <c>mLowEnergyStartTime</c> (<c>0x00410459</c>) are all
/// <c>mov</c>/<c>mov</c> pairs, never <c>fld</c>/<c>fstp</c>. So a <c>-0.0f</c>
/// or a signalling-NaN <paramref name="vy"/> is stored unchanged, sign bit and
/// payload intact. <see cref="Thrust"/> assigns rather than computes for those
/// three.
/// </para>
/// <para>
/// <b>The halved input is a multiply, and that one really is exact.</b>
/// <c>BattleEngineJetPart.cpp:73</c> is <c>0.5f-vy/2.0f</c>; the shipped body is
/// <c>fmul dword ptr [0x005D85EC]</c> then
/// <c>fsubr dword ptr [0x005D85EC]</c>, i.e. a multiply by <c>0.5f</c>. Dividing
/// by two and multiplying by one half are the same IEEE-754 operation for every
/// input including subnormals, so this is a strength reduction with no
/// observable consequence — recorded so a reviewer does not go looking for one.
/// The single rounding is the <c>fstp dword</c> at <c>0x0041035F</c>.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>GetVelocity</c> is the virtual at
/// <c>[vtable + 0x6C]</c> and retail calls it <i>only</i> after all three window
/// gates and the energy test pass (<c>0x004103F3</c>); the three components
/// arrive here as parameters and are always evaluated, which is a difference in
/// evaluation order and not in value. <c>mFlightModel</c>, <c>mLoopHalfway</c>
/// and <c>mLoopBroken</c> are written but not read by this body, so what they
/// mean is not claimed. The energy is never <i>spent</i> here: no store to
/// <c>mMainPart + 0xFC</c> appears anywhere in the function.
/// </para>
/// </remarks>
public static class RetailJetThrust
{
    /// <summary>The throttle centre and the halving factor — <c>0x005D85EC</c>.</summary>
    public const float ThrusterCentre = 0.5f;

    /// <summary>How far back the stick must already have been — <c>0x005D8C8C</c>, bits <c>0xBF19999A</c>.</summary>
    public const float HardForwardArmThreshold = -0.6f;

    /// <summary>How far forward the stick must go to stamp the time — <c>0x005D8BB4</c>, bits <c>0xBF666666</c>.</summary>
    public const float HardForwardThreshold = -0.9f;

    /// <summary>How far back the stick must come to try a loop — <c>0x005D85F8</c>, bits <c>0x3F4CCCCD</c>.</summary>
    public const float LoopPullBackThreshold = 0.8f;

    /// <summary>The oldest a usable hard-forward stamp may be — <c>0x005D8604</c>, bits <c>0x3E4CCCCD</c>.</summary>
    public const float LoopWindowOldest = 0.2f;

    /// <summary>
    /// The newest a usable hard-forward stamp may be — <c>0x005D85C0</c>, bits
    /// <c>0x3DCCCCCD</c>. <b>Retail only</b>; see the type remarks.
    /// </summary>
    public const float LoopWindowNewest = 0.1f;

    /// <summary>
    /// <c>kMinManoeuvreVelocitySq</c> — <c>BattleEngineJetPart.cpp:22</c>,
    /// <c>0x005D8C88</c>, bits <c>0x3DB851EC</c>. Nine times
    /// <see cref="RetailJetAutoLevel.MinManoeuvreVelocitySq"/>.
    /// </summary>
    public const float MinManoeuvreVelocitySq = 0.09000000357627869f;

    /// <summary>The pitch impulse a loop starts with — <c>0x005D8C2C</c>, bits <c>0x3C75C28F</c>.</summary>
    public const float LoopPitchImpulse = 0.015f;

    /// <summary>
    /// <c>mThrusterValue = 0.5f - vy/2.0f</c> as compiled —
    /// <c>0x0041034F-0x0041035F</c>. One rounding, at the store.
    /// </summary>
    public static float ThrusterValueFor(float vy) =>
        (float)((double)ThrusterCentre - (double)vy * (double)ThrusterCentre);

    /// <summary>
    /// <c>CBattleEngineJetPart::Thrust</c> —
    /// <c>BattleEngineJetPart.cpp:64-106</c>, <c>0x00410310</c>.
    /// </summary>
    /// <param name="state">The jet part's own fields.</param>
    /// <param name="mainPart">What retail reaches through <c>mMainPart</c>.</param>
    /// <param name="vy">The throttle axis, as the caller pushed it.</param>
    /// <param name="eventManagerTime"><c>EVENT_MANAGER.GetTime()</c> — <c>0x00672FD0</c>.</param>
    /// <param name="velocityX"><c>mMainPart-&gt;GetVelocity()</c>, component at <c>+0</c>.</param>
    /// <param name="velocityY">Component at <c>+4</c>.</param>
    /// <param name="velocityZ">Component at <c>+8</c>.</param>
    public static void Thrust(
        RetailJetThrustState state,
        RetailJetThrustMainPart mainPart,
        float vy,
        float eventManagerTime,
        float velocityX,
        float velocityY,
        float velocityZ)
    {
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (mainPart is null)
        {
            throw new ArgumentNullException(nameof(mainPart));
        }

        // test ah, 0x41 with je to the exit that skips the mLastMoveYVal store.
        if (state.DoingBarrelCount > 0.0f)
        {
            return;
        }

        if (state.DoingLoop == 0)
        {
            // test ah, 0x40 - C3 alone, so an unordered energy reads as zero.
            if (mainPart.Energy != 0.0f && !float.IsNaN(mainPart.Energy))
            {
                state.ThrusterValue = ThrusterValueFor(vy);

                // test ah, 0x41 then test ah, 1: the first is a plain ordered
                // strictly-greater, the second admits an unordered vy.
                if (state.LastMoveYVal > HardForwardArmThreshold &&
                    !(vy >= HardForwardThreshold))
                {
                    state.LastStartHardForwardTime = eventManagerTime;
                }

                double stamp = state.LastStartHardForwardTime;
                double now = eventManagerTime;

                if (vy > LoopPullBackThreshold &&
                    stamp > now - (double)LoopWindowOldest &&
                    !(stamp >= now - (double)LoopWindowNewest))
                {
                    // test ah, 0x41 with jne to the low-energy arm.
                    if (mainPart.Energy > 0.0f)
                    {
                        double magnitudeSq = RetailJetAutoLevel.VelocityMagnitudeSquared(
                            velocityX, velocityY, velocityZ);

                        // test ah, 0x41 with jne past the loop start.
                        if (magnitudeSq > (double)MinManoeuvreVelocitySq)
                        {
                            state.DoingLoop = 1;
                            state.LoopHalfway = 0;
                            state.LoopBroken = 0;
                            mainPart.Pitchvel = (float)(
                                (double)mainPart.Pitchvel - (double)LoopPitchImpulse);
                        }
                    }
                    else
                    {
                        mainPart.LowEnergyStartTime = eventManagerTime;
                    }
                }
            }
        }

        state.LastMoveYVal = vy;
    }
}
