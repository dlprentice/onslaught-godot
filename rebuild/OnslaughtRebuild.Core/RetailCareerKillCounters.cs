// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CCareer::GetNumKilled</c> and the four accessors that share the same
/// words. <b>The top byte of the first two kill counters is not part of the
/// count</b> — it holds a screen-position offset, and the readout masks it off.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:531-534</c>,
/// which is one line — <c>return mKilledThings[(int)type];</c>. Retail
/// identities in the pristine <c>74154bfa…</c> image, file offset
/// = VA - 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x0041C160</c> <c>CCareer::GetNumKilled</c>.</item>
/// <item><c>0x004218F0</c> the unpacker for <c>mKilledThings[0]</c>'s top byte.</item>
/// <item><c>0x00421900</c> the unpacker for <c>mKilledThings[1]</c>'s top byte.</item>
/// <item><c>0x00421910</c> the packer for <c>mKilledThings[0]</c>'s top byte.</item>
/// <item><c>0x00421940</c> the packer for <c>mKilledThings[1]</c>'s top byte.</item>
/// <item><c>0x00421245-0x004212AF</c> the clamp-and-repack inside <c>CCareer::Load</c>.</item>
/// </list>
/// <para>
/// <b>Source and retail DIVERGE, and retail wins.</b> The source returns the
/// whole <c>int</c>. <c>0x0041C16B</c> is <c>and eax, 0xFFFFFF</c> — the shipped
/// readout keeps 24 bits and throws the top eight away. That single instruction
/// is the entry point to a packed field the pinned header does not describe at
/// all.
/// </para>
/// <para>
/// <b>What is in the top byte, and how it is known.</b> Four accessors exist for
/// nothing else: two that answer <c>(word &gt;&gt;&gt; 24) - 0x80</c> and two
/// that rewrite the byte as <c>(value - 0x80) &lt;&lt; 24</c> over a preserved
/// <c>word &amp; 0xFFFFFF</c>. Their <i>only</i> callers in the whole image sit
/// in one contiguous block, <c>0x0051F470-0x0051FD6C</c>, and that block is
/// reached through the virtual table at <c>0x005DB858</c>, whose RTTI complete
/// object locator at <c>0x00613D10</c> names the type descriptor at
/// <c>0x00629DB0</c>: <c>.?AVCFEPScreenPos@@</c>. So the two bytes are the
/// front end's screen-position offsets, stored in the career because the career
/// is what gets saved. <c>0x0051F470</c> reads both into a two-int structure —
/// <c>mKilledThings[0]</c> into <c>+0</c> and <c>mKilledThings[1]</c> into
/// <c>+4</c> — and <c>0x0051F490</c> writes them back in the same order; those
/// two are in turn called only from <c>0x0051FD6C</c> and <c>0x0051FB21</c>,
/// both inside slots of that same virtual table. The page's own adjusters clamp
/// to <c>±0x40</c> and play a UI sample. Which of the two is horizontal is
/// <b>not</b> established here.
/// </para>
/// <para>
/// <b>The encoding is excess-128, and the biasing instruction is a sign-extended
/// <c>-0x80</c>.</b> Both packers are <c>add eax, -0x80</c> before the shift
/// (<c>0x0042194A</c>), which is the same low byte as <c>+0x80</c> because
/// <c>128 ≡ -128 (mod 256)</c>; MSVC picked the shorter encoding. Both unpackers
/// are <c>shr eax, 0x18</c> — a <b>logical</b> shift — then
/// <c>sub eax, 0x80</c>, so the byte reads back over <c>-128..127</c> and a
/// rebuild that used an arithmetic shift would be wrong for every value above
/// zero.
/// </para>
/// <para>
/// <b><c>Load</c> zeroes an out-of-range offset rather than clamping it.</b>
/// <c>0x0042126A-0x00421280</c> is <c>cmp eax, -0x40 / jl</c> then
/// <c>cmp eax, 0x40 / jle</c> with the failure path <c>xor eax, eax</c>. So a
/// career whose stored offset is <c>±65</c> or worse comes back as <b>0</b>, not
/// as <c>±64</c>. Both words get the same treatment and both are repacked
/// unconditionally, so loading and re-saving an in-range career is a fixed
/// point.
/// </para>
/// <para>
/// <b>The pinned <c>Load</c> is the Xbox one and cannot show any of this.</b>
/// <c>Career.cpp:1095-1121</c> is guarded by <c>#if TARGET != PC</c> and is a
/// version check plus <c>memcpy(this, source, sizeof(CCareer))</c> — no
/// unpacking, no clamp, no screen position. The PC declaration is a bare
/// <c>void Load();</c> (<c>Career.h:128-134</c>, the <c>#else</c> arm of
/// <c>#if TARGET != PC</c>) whose body is not in the drop. Everything above is
/// measured from the image alone.
/// </para>
/// <para>
/// <b>The accumulator can eat the packed byte, and that is a shipped bug.</b>
/// <c>CCareer::UpdateThingsKilled</c> stores an <b>unmasked</b> 32-bit sum back
/// into <c>mKilledThings[i]</c> (<c>0x0041C1A9</c>; see
/// <see cref="RetailCareerCounters.UpdateThingsKilled"/>, and note that the mask
/// at <c>0x0041C211</c> is only on the copy handed to the log). So the screen
/// offsets survive exactly as long as the aircraft and vehicle counts stay below
/// <c>2^24</c>, and a carry out of bit 23 silently shifts the player's screen.
/// <see cref="AddKills"/> reproduces that instead of protecting against it.
/// </para>
/// <para>
/// <b>Not established here.</b> Whether the other three counters' top bytes are
/// used for anything: no accessor reads or writes them, and <c>Load</c> clamps
/// only <c>[0]</c> and <c>[1]</c>. The type index is not bounded —
/// <c>0x0041C164</c> is a bare <c>[ecx + eax*4 + 0x23F4]</c> — so
/// <c>GetNumKilled(TK_HACK_AGRADES)</c> (<c>Player.h:36</c>, value 6) reads
/// <c>mSlots[1]</c>; <c>FEPGoodies.cpp:94-135</c> switches those two enumerators
/// away before they can reach here, but nothing in this function stops them.
/// </para>
/// </remarks>
public sealed class RetailCareerKillCounters
{
    /// <summary><c>TK_TOTAL</c> — <c>Player.h:34</c>; the array <c>Load</c> clamps within.</summary>
    public const int KilledTypeCount = 5;

    /// <summary>The readout mask — <c>and eax, 0xFFFFFF</c> at <c>0x0041C16B</c>.</summary>
    public const int KillCountMask = 0x00FFFFFF;

    /// <summary>The excess-128 bias — <c>sub eax, 0x80</c> / <c>add eax, -0x80</c>.</summary>
    public const int ScreenPositionBias = 0x80;

    /// <summary>The magnitude <c>Load</c> accepts — <c>cmp eax, 0x40</c> at <c>0x0042126F</c>.</summary>
    public const int ScreenPositionLimit = 0x40;

    private readonly int[] _words = new int[KilledTypeCount];

    /// <summary>
    /// <c>mKilledThings</c> at <c>CCareer + 0x23F4</c>, as stored: counts in the
    /// low 24 bits, and a biased screen offset in the top byte of the first two.
    /// </summary>
    public IReadOnlyList<int> Words => _words;

    /// <summary>Sets one raw stored word, packed byte included.</summary>
    public void SetWord(int type, int word) => _words[type] = word;

    /// <summary>
    /// <c>CCareer::GetNumKilled</c> — <c>Career.cpp:531-534</c>,
    /// <c>0x0041C160</c>. The count only; see the type remarks for the mask.
    /// </summary>
    public int GetNumKilled(int type) => MaskKillCount(_words[type]);

    /// <summary>
    /// <c>CCareer::UpdateThingsKilled</c>'s inner store as it affects <i>this</i>
    /// contract — <c>0x0041C1A9</c>, an unmasked 32-bit add. Carrying out of bit
    /// 23 corrupts the packed byte; that is the shipped behaviour.
    /// </summary>
    public void AddKills(int type, int killedThisLevel) =>
        _words[type] = unchecked(_words[type] + killedThisLevel);

    /// <summary>The masking half of <c>GetNumKilled</c>, on a loose word.</summary>
    public static int MaskKillCount(int word) => word & KillCountMask;

    /// <summary>
    /// The unpackers at <c>0x004218F0</c> and <c>0x00421900</c> —
    /// <c>(word &gt;&gt;&gt; 24) - 0x80</c>, a logical shift.
    /// </summary>
    public static int UnpackScreenPosition(int word) =>
        unchecked((int)((uint)word >> 24) - ScreenPositionBias);

    /// <summary>
    /// The packers at <c>0x00421910</c> and <c>0x00421940</c> —
    /// <c>((value - 0x80) &lt;&lt; 24) + (word &amp; 0xFFFFFF)</c>. Nothing
    /// range-checks <paramref name="screenPosition"/> here; only <c>Load</c>
    /// does.
    /// </summary>
    public static int PackScreenPosition(int word, int screenPosition) =>
        unchecked(((screenPosition - ScreenPositionBias) << 24) + MaskKillCount(word));

    /// <summary>
    /// <c>Load</c>'s range gate — <c>0x0042126A-0x00421280</c>. Out of range is
    /// <b>zero</b>, not the nearest limit.
    /// </summary>
    public static int ClampScreenPositionOnLoad(int screenPosition) =>
        screenPosition < -ScreenPositionLimit || screenPosition > ScreenPositionLimit
            ? 0
            : screenPosition;

    /// <summary>
    /// What <c>CCareer::Load</c> does to one of the two packed words —
    /// unpack, gate, repack over the preserved count.
    /// </summary>
    public static int NormaliseWordOnLoad(int word) =>
        PackScreenPosition(word, ClampScreenPositionOnLoad(UnpackScreenPosition(word)));

    /// <summary>
    /// <c>CCareer::Load</c>'s treatment of the whole pair —
    /// <c>0x00421245-0x004212AF</c>. Only words 0 and 1 are touched.
    /// </summary>
    public void NormaliseOnLoad()
    {
        _words[0] = NormaliseWordOnLoad(_words[0]);
        _words[1] = NormaliseWordOnLoad(_words[1]);
    }
}
