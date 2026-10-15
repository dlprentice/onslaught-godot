// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CCareer::GetGradeFromRanking</c> — the released ranking-to-letter law.
/// A pure function of one float.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/Career.cpp:1178-1202</c>. Retail identity:
/// <c>0x00421470</c> in the pristine <c>74154bfa…</c> image, and a second,
/// independent copy inlined at <c>0x0041C24E-0x0041C2E7</c> inside the
/// S-grade tally that follows <c>UpdateThingsKilled</c> — the same instruction
/// sequence against the same three constants, which is why the law is treated
/// as measured rather than inferred from a single body.
/// </para>
/// <para>
/// The constants are read out of <c>.rdata</c>: <c>0x005D8568</c> is
/// <c>0x3F800000</c> (<c>1.0f</c>), <c>0x005D856C</c> is <c>0x00000000</c>
/// (<c>0.0f</c>), <c>0x005D85BC</c> is <c>0x40800000</c> (<c>4.0f</c>). The
/// multiply by four is a power of two and therefore exact, so the source's
/// <c>floorf(f * 4.f)</c> and retail's <c>fmul</c>-then-<c>floor</c> agree on
/// every finite input.
/// </para>
/// <para>
/// <b>The comment above the source is wrong about its own range.</b>
/// <c>Career.cpp:1180</c> says "grade from A - F". No arm can produce
/// <c>'F'</c>: the ladder is <c>'S'</c>, then <c>'E'</c>, then
/// <c>'D' - floor(f*4)</c>, which over a ranking in <c>(0,1)</c> yields
/// <c>'D' 'C' 'B' 'A'</c> at the quarter boundaries and nothing else.
/// </para>
/// <para>
/// <b>Source and retail DIVERGE on NaN, and retail wins.</b> The C text
/// <c>if (f == 1.f)</c> is false for a NaN, so the source would fall to the
/// <c>f &lt;= 0.f</c> test and then to the floor. The compiled test at
/// <c>0x00421474</c> is <c>fcomp</c> / <c>fnstsw</c> /
/// <c>test ah, 0x40</c> — it inspects <b>C3 only</b>. An unordered compare
/// sets C3, C2 and C0 together, so <c>ah</c> is <c>0x45</c>, the mask hits, and
/// a NaN ranking returns <c>'S'</c>: the top grade. MSVC's equality idiom
/// simply is not NaN-correct, and this type reproduces what shipped. No NaN
/// ranking has been observed reaching this function; the arm is pinned because
/// it is real, not because it is reachable. The cheapest falsifier is to find
/// any write of a non-finite value to <c>mRanking</c> (<c>node + 0x3C</c>).
/// </para>
/// <para>
/// <b>The result is eight bits wide, and the widening to WCHAR is not
/// modelled.</b> Retail computes <c>mov al, 0x44</c> / <c>sub al, cl</c> — an
/// 8-bit subtract — where <c>cl</c> is the <b>low byte</b> of the 64-bit
/// <c>fistp</c> store at <c>0x004214B2</c>, and only then hands the one-char
/// string to <c>ToWCHAR</c> at <c>0x004F7BF0</c> and returns its first code
/// unit. For every grade the ladder can actually produce the byte is ASCII and
/// the widening is the identity, so <see cref="GradeFromRanking"/> is exact
/// there. Outside that range — a ranking of 17.25 or more drives the byte to
/// <c>0x80</c> and above — the conversion is code-page dependent and this type
/// makes no claim; <see cref="GradeByteFromRanking"/> is the pinned surface.
/// </para>
/// </remarks>
public static class RetailCareerGrade
{
    /// <summary>The perfect-ranking grade — <c>mov al, 0x53</c> at <c>0x00421484</c>.</summary>
    public const byte PerfectGrade = (byte)'S';

    /// <summary>The zero-or-worse grade — <c>mov al, 0x45</c> at <c>0x00421499</c>.</summary>
    public const byte FailedGrade = (byte)'E';

    /// <summary>The ladder base the floored quarter is subtracted from — <c>mov al, 0x44</c> at <c>0x004214BA</c>.</summary>
    public const byte LadderBase = (byte)'D';

    /// <summary>The ranking that scores <see cref="PerfectGrade"/> — <c>0x005D8568</c>.</summary>
    public const float PerfectRanking = 1.0f;

    /// <summary>The quarter scale — <c>0x005D85BC</c>, <c>0x40800000</c>.</summary>
    public const float QuarterScale = 4.0f;

    /// <summary>
    /// <c>CCareer::GetGradeFromRanking</c>'s intermediate <c>char c</c>, before
    /// the <c>ToWCHAR</c> widening — <c>Career.cpp:1181-1199</c>,
    /// <c>0x00421470</c>.
    /// </summary>
    public static byte GradeByteFromRanking(float ranking)
    {
        // fcomp / test ah, 0x40 - C3 alone, so NaN lands here too.
        if (ranking == PerfectRanking || float.IsNaN(ranking))
        {
            return PerfectGrade;
        }

        // fcomp / test ah, 0x41 - C3 or C0, i.e. equal-or-below.
        if (ranking <= 0.0f)
        {
            return FailedGrade;
        }

        double quarters = System.Math.Floor((double)ranking * (double)QuarterScale);
        return unchecked((byte)(LadderBase - LowByteOfFistp(quarters)));
    }

    /// <summary>
    /// The value the retail function hands back, for the ASCII range its own
    /// ladder produces. See the type remarks for why the non-ASCII range is not
    /// claimed.
    /// </summary>
    public static char GradeFromRanking(float ranking) => (char)GradeByteFromRanking(ranking);

    /// <summary>
    /// <c>fistp qword ptr</c> followed by an 8-bit read of the store —
    /// <c>0x004214B2</c> and <c>0x004214B6</c>. The operand here is already an
    /// exact integer, so the ambient rounding mode cannot matter; what does
    /// matter is that a value outside <c>long</c> stores the x87 integer
    /// indefinite, whose low byte is zero.
    /// </summary>
    private static byte LowByteOfFistp(double integralValue)
    {
        if (double.IsNaN(integralValue) ||
            integralValue < -9223372036854775808.0 ||
            integralValue >= 9223372036854775808.0)
        {
            return unchecked((byte)long.MinValue);
        }

        return unchecked((byte)(long)integralValue);
    }
}

/// <summary>
/// <c>CCareer</c>'s bit-slot store — the 32-word array the front end and the
/// mission scripts flip persistent flags in.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:1357-1408</c>
/// and <c>Career.h:156-159, 197</c>. Retail identity: <c>0x004214E0</c>
/// (<c>SetSlot</c>), with the array itself at <c>CCareer + 0x2408</c> —
/// confirmed three ways: the four <c>[esi + edx*4 + 0x2408]</c> accesses inside
/// <c>SetSlot</c>, the constructor's <c>lea edi, [edx + 0x2408]</c> /
/// <c>mov ecx, 0x20</c> / <c>rep stosd</c> at <c>0x0041B896</c>, and the
/// 32-iteration copy loop at <c>0x0041BD27</c>.
/// </para>
/// <para>
/// <b>Source and retail agree, including on a shipped range bug.</b> The guard
/// at <c>Career.cpp:1381</c> is <c>num &gt;= MAX_CAREER_SLOTS*8</c>, and
/// <c>0x004214EB</c> is <c>cmp eax, 0x100</c> — 256. But
/// <c>MAX_CAREER_SLOTS</c> is the length of an <c>int</c> array
/// (<c>Career.h:17, 197</c>), so the store actually holds
/// <c>32 * 32 = 1024</c> bits. The guard counts as though the array were bytes.
/// Slots 256 through 1023 exist in the save, are zeroed by the constructor, and
/// can never be reached through <c>GetSlot</c> or <c>SetSlot</c>: only words 0
/// through 7 are addressable. Any editor that offers 1024 slots is offering
/// 768 the game cannot see.
/// </para>
/// <para>
/// <b>Only a literal 1 sets a slot.</b> <c>0x00421505</c> is
/// <c>cmp dword ptr [esp + 0xC], 1</c> / <c>jne</c>, matching
/// <c>if (val == TRUE)</c> at <c>Career.cpp:1396</c> — so a <c>BOOL</c> of 2,
/// or of <c>-1</c>, <b>clears</b> the slot. This is the same shape as
/// <see cref="RetailCareerNode.SetBaseThingExistTo"/>.
/// </para>
/// <para>
/// <b>There is no compiled <c>CCareer::GetSlot</c>.</b> Searching the whole
/// image for the <c>0x00002408</c> displacement finds seven sites: four in
/// <c>SetSlot</c>, and three bulk accesses (constructor and save/load copy) —
/// no read-one-bit body. The reader below is therefore carried from
/// <c>Career.cpp:1357-1375</c> alone; what makes it safe is that its mask and
/// guard are textually identical to the writer's, and the writer's are
/// measured.
/// </para>
/// </remarks>
public sealed class RetailCareerSlots
{
    /// <summary><c>MAX_CAREER_SLOTS</c> — <c>Career.h:17</c>; the <c>rep stosd</c> count at <c>0x0041B8A2</c>.</summary>
    public const int SlotWords = 32;

    /// <summary>The number of bits the array actually holds.</summary>
    public const int StoredSlotCount = SlotWords * 32;

    /// <summary>The number of bits the guard admits — <c>cmp eax, 0x100</c> at <c>0x004214EB</c>.</summary>
    public const int AddressableSlotCount = SlotWords * 8;

    private readonly int[] _words = new int[SlotWords];

    /// <summary>The raw slot words, as they sit in the save.</summary>
    public IReadOnlyList<int> Words => _words;

    /// <summary>
    /// <c>CCareer::SetSlot</c> — <c>Career.cpp:1379-1408</c>, <c>0x004214E0</c>.
    /// Out-of-range indices log and return, leaving every word untouched.
    /// </summary>
    /// <param name="value">
    /// The retail <c>BOOL</c>. Only literal 1 sets; anything else clears.
    /// </param>
    public void SetSlot(int slot, int value)
    {
        if (slot < 0 || slot >= AddressableSlotCount)
        {
            return;
        }

        int word = slot >> 5;
        int mask = RetailCareerNode.MaskFor(slot);

        if (value == 1)
        {
            _words[word] |= mask;
        }
        else
        {
            _words[word] &= ~mask;
        }
    }

    /// <summary>
    /// <c>CCareer::GetSlot</c> — <c>Career.cpp:1357-1375</c>. Source-only; see
    /// the type remarks. Out of range is <c>FALSE</c>.
    /// </summary>
    public int GetSlot(int slot)
    {
        if (slot < 0 || slot >= AddressableSlotCount)
        {
            return 0;
        }

        return (_words[slot >> 5] & RetailCareerNode.MaskFor(slot)) != 0 ? 1 : 0;
    }

    /// <summary>
    /// The 32-dword assignment <c>CCareer::Update</c> uses for
    /// <c>mSlots = END_LEVEL_DATA.mSlots</c> — <c>0x0041BD37</c>
    /// <c>mov edx, 0x20</c> then <c>mov [eax], edi</c>. Words are
    /// replaced, not OR-ed.
    /// </summary>
    public void CopyWords(IReadOnlyList<int> words)
    {
        if (words is null)
        {
            throw new ArgumentNullException(nameof(words));
        }

        if (words.Count != SlotWords)
        {
            throw new ArgumentException(
                $"The copy loop at 0x0041BD37 runs {SlotWords} times; " +
                "retail writes exactly that many dwords with no early exit.",
                nameof(words));
        }

        for (int index = 0; index < SlotWords; index++)
        {
            _words[index] = words[index];
        }
    }
}

/// <summary>
/// Three released career counters: the two read-and-clear goodie latches, and
/// the end-of-level kill accumulator.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:411-424</c>
/// and <c>:538-557</c>. Retail identities: <c>0x00421550</c>
/// (<c>GetAndResetGoodieNewCount</c>), <c>0x00421560</c>
/// (<c>GetAndResetFirstGoodie</c>), <c>0x0041C180</c>
/// (<c>UpdateThingsKilled</c>).
/// </para>
/// <para>
/// <b>Source and retail agree on all three.</b> The two latches compile to
/// three instructions each — load the global, store zero, return — and the
/// globals are file-scope, not members: <c>new_goodie_count</c> at
/// <c>0x00662B20</c> and <c>first_goodie</c> at <c>0x00662B24</c>. Both are
/// zeroed by the career constructor at <c>0x0041B8BB</c>. Because they are
/// latches, a second read in the same frame yields zero; that is the whole
/// contract and the reason a caller must not poll them.
/// </para>
/// <para>
/// <b>The kill accumulator is exempt on exactly one world.</b>
/// <c>0x0041C188</c> is <c>cmp eax, 0x64</c> / <c>je</c> against
/// <c>END_LEVEL_DATA.mWorldFinished</c> at <c>0x00672E18</c>: an equality test
/// on 100, not a range, so only the training level scores nothing. The loop
/// then runs <c>i &lt; 5</c> (<c>cmp esi, 5</c> at <c>0x0041C230</c>), which is
/// <c>TK_TOTAL</c> from <c>Player.h:27-34</c>, adding
/// <c>END_LEVEL_DATA.mThingsKilled[i]</c> at <c>0x00672E30</c> into
/// <c>mKilledThings[i]</c> at <c>CCareer + 0x23F4</c>. The addition is a plain
/// 32-bit <c>add</c> and wraps; nothing saturates.
/// </para>
/// <para>
/// The offsets corroborate <c>EndLevelData.h:18-32</c> exactly:
/// <c>0x00672E30 - 0x00672E18</c> is <c>0x18</c>, which is
/// <c>mWorldFinished</c> plus the five scalars declared between it and
/// <c>mThingsKilled</c>.
/// </para>
/// <para>
/// <b>One retail detail is logging-only and is not modelled.</b>
/// <c>0x0041C211</c> masks the running total with <c>0x00FFFFFF</c> — but only
/// in the copy pushed to <c>LOG.AddMessage</c>. The value written back to
/// <c>mKilledThings[i]</c> at <c>0x0041C1A9</c> is the unmasked sum, so the
/// counter is 32 bits and only the log line is 24. Core has no log.
/// </para>
/// </remarks>
public sealed class RetailCareerCounters
{
    /// <summary><c>TK_TOTAL</c> — <c>Player.h:34</c>; the loop bound at <c>0x0041C230</c>.</summary>
    public const int KilledTypeCount = 5;

    /// <summary>The world <c>UpdateThingsKilled</c> refuses to score — <c>cmp eax, 0x64</c> at <c>0x0041C188</c>.</summary>
    public const int UnscoredWorldNumber = 100;

    private readonly int[] _killedThings = new int[KilledTypeCount];

    /// <summary><c>new_goodie_count</c> — the global at <c>0x00662B20</c>.</summary>
    public int NewGoodieCount { get; set; }

    /// <summary><c>first_goodie</c> — the global at <c>0x00662B24</c>.</summary>
    public int FirstGoodie { get; set; }

    /// <summary><c>mKilledThings</c> — <c>CCareer + 0x23F4</c>.</summary>
    public IReadOnlyList<int> KilledThings => _killedThings;

    /// <summary><c>CCareer::GetAndResetGoodieNewCount</c> — <c>Career.cpp:1411-1416</c>, <c>0x00421550</c>.</summary>
    public int GetAndResetGoodieNewCount()
    {
        int value = NewGoodieCount;
        NewGoodieCount = 0;
        return value;
    }

    /// <summary><c>CCareer::GetAndResetFirstGoodie</c> — <c>Career.cpp:1419-1424</c>, <c>0x00421560</c>.</summary>
    public int GetAndResetFirstGoodie()
    {
        int value = FirstGoodie;
        FirstGoodie = 0;
        return value;
    }

    /// <summary>
    /// <c>CCareer::UpdateThingsKilled</c> — <c>Career.cpp:538-557</c>,
    /// <c>0x0041C180</c>.
    /// </summary>
    /// <param name="worldFinished"><c>END_LEVEL_DATA.mWorldFinished</c>.</param>
    /// <param name="thingsKilledThisLevel"><c>END_LEVEL_DATA.mThingsKilled</c>, five entries.</param>
    public void UpdateThingsKilled(int worldFinished, IReadOnlyList<int> thingsKilledThisLevel)
    {
        if (thingsKilledThisLevel is null)
        {
            throw new ArgumentNullException(nameof(thingsKilledThisLevel));
        }

        if (thingsKilledThisLevel.Count != KilledTypeCount)
        {
            throw new ArgumentException(
                $"TK_TOTAL is {KilledTypeCount}; retail reads exactly that many entries.",
                nameof(thingsKilledThisLevel));
        }

        if (worldFinished == UnscoredWorldNumber)
        {
            return;
        }

        for (int type = 0; type < KilledTypeCount; type++)
        {
            _killedThings[type] = unchecked(_killedThings[type] + thingsKilledThisLevel[type]);
        }
    }
}

/// <summary>
/// <c>CCareer::IsEpisodeAvailable</c> — the released episode-unlock gate.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/Career.cpp:1428-1442</c>, over the
/// <c>COMPLETE_LEVEL</c> macro at <c>Career.cpp:565</c>. Retail identity:
/// <c>0x00421570</c>, dispatched through the jump table at <c>0x004218CC</c>.
/// </para>
/// <para>
/// <b>Source and retail agree on every world number.</b> Read straight out of
/// the compiled arms: episode 2 tests <c>0x6E</c>; episode 3 tests
/// <c>0xE7</c> then <c>0xE8</c>; episode 4 <c>0x14B</c>, <c>0x14C</c>; episode
/// 5 <c>0x1AF</c>, <c>0x1B0</c>; episode 6 <c>0x209</c>, <c>0x20A</c>,
/// <c>0x20B</c>, <c>0x20C</c>; episode 7 <c>0x26D</c>, <c>0x26E</c>; episode 8
/// <c>0x2E5</c>, <c>0x2E6</c>. That is 110; 231/232; 331/332; 431/432;
/// 521-524; 621/622; 741/742 — the source list exactly.
/// </para>
/// <para>
/// <b>The bound is unsigned.</b> <c>0x00421575</c> is <c>cmp eax, 8</c> /
/// <c>ja</c>, so a negative episode takes the default arm and returns FALSE
/// rather than indexing the table backwards. Episodes 0 and 1 return TRUE
/// through a shared stub at <c>0x00421585</c> with no lookup at all.
/// </para>
/// <para>
/// <b>Evaluation short-circuits, and that is observable.</b> Each arm returns
/// as soon as one world reports complete (<c>je 0x004218BF</c>), so the later
/// world numbers in a multi-world episode are never looked up. Combined with
/// the null dereference described on
/// <see cref="RetailCareerNodeTable.CompleteFlagOf"/>, a node table missing
/// world 232 is survivable when 231 is complete and fatal when it is not.
/// </para>
/// <para>
/// <b>Not established here.</b> Whether the shipped <c>level_structure</c>
/// actually contains all fourteen gate worlds. <c>GetLevelStructure</c>
/// (<c>0x0041B7B0</c>) is a one-instruction return of the address
/// <c>0x00623E28</c>, and the table's contents are a separate measurement.
/// </para>
/// </remarks>
public static class RetailCareerEpisodes
{
    /// <summary>The highest episode the switch admits — <c>cmp eax, 8</c> at <c>0x00421575</c>.</summary>
    public const int HighestEpisode = 8;

    /// <summary>The last episode that is unconditionally open — the shared TRUE stub at <c>0x00421585</c>.</summary>
    public const int LastUnconditionalEpisode = 1;

    private static readonly int[] Episode2 = { 110 };
    private static readonly int[] Episode3 = { 231, 232 };
    private static readonly int[] Episode4 = { 331, 332 };
    private static readonly int[] Episode5 = { 431, 432 };
    private static readonly int[] Episode6 = { 521, 522, 523, 524 };
    private static readonly int[] Episode7 = { 621, 622 };
    private static readonly int[] Episode8 = { 741, 742 };
    private static readonly int[] None = System.Array.Empty<int>();

    /// <summary>
    /// The worlds an episode's arm tests, in the order retail tests them.
    /// Empty for episodes 0, 1 and anything out of range.
    /// </summary>
    public static IReadOnlyList<int> QualifyingWorlds(int episode) => episode switch
    {
        2 => Episode2,
        3 => Episode3,
        4 => Episode4,
        5 => Episode5,
        6 => Episode6,
        7 => Episode7,
        8 => Episode8,
        _ => None,
    };

    /// <summary>
    /// <c>CCareer::IsEpisodeAvailable</c> — <c>Career.cpp:1428-1442</c>,
    /// <c>0x00421570</c>.
    /// </summary>
    public static bool IsEpisodeAvailable(int episode, RetailCareerNodeTable nodes)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        if ((uint)episode > (uint)HighestEpisode)
        {
            return false;
        }

        if (episode <= LastUnconditionalEpisode)
        {
            return true;
        }

        IReadOnlyList<int> worlds = QualifyingWorlds(episode);
        for (int index = 0; index < worlds.Count; index++)
        {
            if (nodes.CompleteFlagOf(worlds[index]) == 1)
            {
                return true;
            }
        }

        return false;
    }
}
