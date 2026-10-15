// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// <c>CGrade</c> — the released one-byte grade and the ordering the goodie
/// requirements are decided with. <b>Better grades compare as smaller
/// bytes</b>, and <c>'S'</c> is outside the ladder entirely.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.h:28-38</c>. Retail
/// identity: <c>0x00420AC0</c> in the pristine <c>74154bfa…</c> image, file
/// offset = VA - 0x400000. The byte <c>'S'</c> is the literal <c>0x53</c> in
/// two <c>cmp al, 0x53</c> instructions at <c>0x00420AC2</c> and
/// <c>0x00420AD2</c>; there is no <c>.rdata</c> constant to read.
/// </para>
/// <para>
/// <b>Source and retail agree instruction for instruction.</b>
/// <c>Career.h:35</c> is three statements and the compiled body is three
/// blocks: <c>this-&gt;grade == 'S'</c> returns the literal <c>1</c>
/// (<c>mov eax, 1</c>), <c>right.grade == 'S'</c> returns the literal <c>0</c>
/// (<c>xor eax, eax</c>), and what remains is
/// <c>xor edx, edx / cmp al, cl / setle dl / mov eax, edx</c>. So the answer is
/// a normalised <c>0</c> or <c>1</c> — unlike the readouts in
/// <see cref="RetailWeaponStoreReadouts"/>, this <c>BOOL</c> really is a
/// boolean.
/// </para>
/// <para>
/// <b>The comparison is signed, and that is observable above <c>0x7F</c>.</b>
/// <c>0x00420AE0</c> is <c>setle</c>, not <c>setbe</c>: MSVC's <c>char</c> is
/// signed, so a grade byte in <c>0x80..0xFF</c> reads as negative and therefore
/// as <i>better</i> than every ASCII letter. That is reachable, not academic:
/// <see cref="RetailCareerGrade.GradeByteFromRanking"/> is an 8-bit
/// <c>'D' - quarters</c> subtract with no clamp, so a ranking of 17.25 or more
/// drives the byte past <c>0x7F</c> and the grade it produces then outranks
/// <c>'A'</c>. A rebuild that stored the grade in a <c>char</c>/<c>byte</c> and
/// compared unsigned would order those the other way round.
/// </para>
/// <para>
/// <b>Only the low byte of the argument is read.</b> <c>CGrade</c> is one byte
/// passed by value, so the caller pushes a whole dword and
/// <c>0x00420ACE</c> reads <c>byte ptr [esp + 4]</c>. The upper three bytes are
/// whatever the caller had; nothing here inspects them. A rebuild that compared
/// 32-bit words would part company on any caller that left them dirty.
/// </para>
/// <para>
/// <b><c>'S'</c> wins even against itself, and that asymmetry is the shipped
/// law.</b> <c>'S' &gt;= 'S'</c> takes the first arm and returns TRUE, which is
/// what a reflexive <c>&gt;=</c> should do; but the guard order also means
/// <c>'S' &gt;= x</c> is TRUE for every <c>x</c> and <c>x &gt;= 'S'</c> is FALSE
/// for every other <c>x</c>. There is no ranking that produces <c>'S'</c>
/// except exactly <c>1.0f</c> (and, per
/// <see cref="RetailCareerGrade"/>, a NaN), so in practice this is "a perfect
/// level satisfies any grade requirement and nothing else satisfies a perfect
/// requirement".
/// </para>
/// <para>
/// <b>No case folding.</b> <c>0x53</c> is upper-case <c>'S'</c> only. A stored
/// <c>'s'</c> (<c>0x73</c>) is not the sentinel and, being the largest of the
/// letters involved, is the <i>worst</i> possible grade.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>CGrade::operator==</c>
/// (<c>Career.h:36</c>) has no out-of-line body anywhere in the image — MSVC
/// inlined every use, including <c>Career.cpp:631</c>. It is modelled below from
/// the source text alone, which is safe because it is a single byte comparison
/// with no branch to get wrong. The <c>WCHAR</c> constructor at
/// <c>Career.h:33</c> is likewise inlined; the truncation it performs is
/// modelled by <see cref="FromWideChar"/>.
/// </para>
/// </remarks>
public readonly struct RetailGrade
{
    /// <summary>The out-of-ladder sentinel — <c>cmp al, 0x53</c> at <c>0x00420AC2</c>.</summary>
    public const sbyte PerfectGradeByte = 0x53;

    /// <summary><c>char grade</c> — <c>Career.h:37</c>. Signed, as MSVC's <c>char</c> is.</summary>
    public sbyte Grade { get; }

    /// <summary><c>CGrade(char g)</c> — <c>Career.h:32</c>.</summary>
    public RetailGrade(sbyte grade) => Grade = grade;

    /// <summary>
    /// <c>CGrade(WCHAR g) { grade = (char)g; }</c> — <c>Career.h:33</c>. The
    /// cast keeps the low eight bits and reinterprets them as signed, which is
    /// how <c>GetGradeFromRanking</c>'s widened result gets back to a byte.
    /// </summary>
    public static RetailGrade FromWideChar(char wide) =>
        new RetailGrade(unchecked((sbyte)(byte)wide));

    /// <summary>
    /// <c>CGrade::operator&gt;=</c> — <c>Career.h:35</c>, <c>0x00420AC0</c>.
    /// Reads "this grade is at least as good as <paramref name="right"/>".
    /// </summary>
    public bool IsAtLeast(RetailGrade right)
    {
        if (Grade == PerfectGradeByte)
        {
            return true;
        }

        if (right.Grade == PerfectGradeByte)
        {
            return false;
        }

        // setle: a SIGNED byte compare, and better grades are smaller letters.
        return Grade <= right.Grade;
    }

    /// <summary>
    /// <c>CGrade::operator==</c> — <c>Career.h:36</c>. Inlined everywhere; see
    /// the type remarks.
    /// </summary>
    public bool IsExactly(RetailGrade right) => Grade == right.Grade;
}

/// <summary>
/// The measured field layout of the shipped <c>CCareer</c> record, and
/// <c>CCareer::GetNode</c>'s address arithmetic over it.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.h:108-207</c>.
/// Retail identity: <c>0x00420AF0</c> (<c>GetNode</c>) in the pristine
/// <c>74154bfa…</c> image, with the <c>CAREER</c> singleton itself at
/// <c>0x00660620</c> — the literal in <c>mov ecx, 0x660620</c> at
/// <c>0x004218B0</c> and at <c>0x0051F473</c>.
/// </para>
/// <para>
/// <b>Source and retail agree on <c>GetNode</c> exactly, including the missing
/// upper bound.</b> <c>Career.h:138</c> is
/// <c>if (num &lt; 0) return NULL; return &amp;mNode[num];</c> and the compiled
/// body is <c>test eax, eax / jge / xor eax, eax</c> then
/// <c>shl eax, 6 / lea eax, [eax + ecx + 4]</c>. Nothing checks
/// <c>MAX_NODES</c>, so <see cref="NodeOffset"/> keeps answering past the end of
/// the array — <c>GetNode(100)</c> is the address of <c>mNodeLink[0]</c>, and
/// <c>Career.cpp:596</c> walks <c>node_count &lt; num_nodes</c> with
/// <c>num_nodes</c> a mutable global. This is pinned rather than corrected.
/// </para>
/// <para>
/// <b>The whole prefix layout falls out of five measured displacements, and
/// every one of the header's array bounds is confirmed independently.</b>
/// </para>
/// <list type="bullet">
/// <item><c>+0x0000</c> <c>mPendingExtraGoodies</c> — the one dword <c>lea eax, [eax + ecx + 4]</c> leaves in front of <c>mNode</c>, and the only data member <c>Career.h</c> declares before <c>protected:</c>.</item>
/// <item><c>+0x0004</c> <c>mNode</c> — <c>0x00420B00</c>, and the absolute <c>0x00660624</c> the world scan in <c>GRADE</c> starts from (<c>0x0041C3BE</c>).</item>
/// <item><c>+0x1904</c> <c>mNodeLink</c> — the displacement at <c>0x0041BC8A</c>. <c>0x1904 - 4 = 0x1900 = 100 * 64</c>, i.e. <c>MAX_NODES</c> nodes of <c>sizeof(CCareerNode)</c>, and the stride is the <c>shl eax, 6</c> above.</item>
/// <item><c>+0x1F44</c> <c>mGoodies</c> — the absolute <c>0x00662564</c> the constructor's 300-dword <c>rep stosd</c> starts from (<c>mov edi, 0x662564</c> at <c>0x0041B6DC</c>). <c>0x1F44 - 0x1904 = 0x640 = 200 * 8</c>, i.e. <c>MAX_LINKS</c> links of eight bytes, and the 200-iteration eight-byte init loop at <c>0x0041B6C6</c> over <c>0x00661F24</c> confirms both the count and the stride.</item>
/// <item><c>+0x23F4</c> <c>mKilledThings</c> — <c>0x0041C167</c>. <c>0x23F4 - 0x1F44 = 0x4B0 = 300 * 4</c>, i.e. <c>MAX_NUM_GOODIES</c> goodies of one dword.</item>
/// <item><c>+0x2408</c> <c>mSlots</c> — the displacement <see cref="RetailCareerSlots"/> measures three ways. <c>0x2408 - 0x23F4 = 0x14 = 5 * 4</c>, i.e. <c>TK_TOTAL</c> counters.</item>
/// </list>
/// <para>
/// <c>BASE_THINGS_EXISTS_SIZE = 288</c> is in there too: a 64-byte
/// <c>CCareerNode</c> is five leading ints, then <c>288 &gt;&gt; 5 = 9</c> mask
/// words, then <c>mNumAttempts</c> and <c>mRanking</c> — exactly 64.
/// </para>
/// <para>
/// <b>The shipped record is eight bytes larger than the header describes, and
/// retail wins.</b> <c>CCareer::Load</c> copies <c>rep movsd</c> with
/// <c>ecx = 0x92F</c> — <c>0x00421236</c>, and the same <c>mov ecx, 0x92F</c>
/// again at <c>0x0042136C</c> and <c>0x004213EB</c> — and advances its cursor by
/// <c>0x24BC</c> (<c>0x00421253</c>); <c>0x92F * 4 = 0x24BC</c>, so
/// <see cref="RecordSize"/> is measured five times and agrees with itself.
/// Adding up every member <c>Career.h</c> declares gives <c>0x24B4</c>.
/// </para>
/// <para>
/// <b>The eight extra bytes are a second invert-Y pair at <c>+0x24A4</c>, and
/// the constructor places them.</b> <c>CCareer::CCareer</c> at
/// <c>0x0041B6A0</c> initialises <b>four</b> two-dword pairs behind the two
/// volume floats, where <c>Career.h</c> declares only three past
/// <c>mIsGod</c>: <c>+0x249C</c>/<c>+0x24A0</c> and <c>+0x24A4</c>/<c>+0x24A8</c>
/// to <b>zero</b> (<c>0x0041B701</c>, <c>0x0041B727</c>, <c>0x0041B6FB</c>,
/// <c>0x0041B721</c>), then <c>+0x24AC</c>/<c>+0x24B0</c> and
/// <c>+0x24B4</c>/<c>+0x24B8</c> to <b>one</b> (<c>0x0041B707</c>,
/// <c>0x0041B72D</c>, <c>0x0041B6EF</c>, <c>0x0041B6F6</c>). Every store is an
/// absolute over the <c>CAREER</c> singleton, so each offset is
/// <c>address - 0x00660620</c>. The <c>1</c> defaults pin <c>mVibration</c> and
/// <c>mControllerConfigurationNum</c> to the last two pairs — which is also what
/// ends the record at <c>+0x24BC</c>, exactly <see cref="RecordSize"/> — leaving
/// <b>two</b> zeroed pairs where the header declares one <c>mInvertYAxis</c>.
/// The undeclared pair is therefore the second invert-Y pair at
/// <c>+0x24A4..+0x24AB</c>, and <c>mIsGod</c> keeps
/// <c>+0x2494</c>/<c>+0x2498</c> as the one pair this constructor never writes.
/// So the tail is now laid out rather than guessed; see the offsets below.
/// </para>
/// <para>
/// <b>Which invert pair is flight and which is walker is not this
/// constructor's evidence.</b> Both are zero-initialised, so <c>0x0041B6A0</c>
/// cannot separate them. The flight-then-walker assignment carried by
/// <see cref="FlightInvertYAxisArrayOffset"/> and
/// <see cref="WalkerInvertYAxisArrayOffset"/> comes from the Controls UI reads
/// recorded in <c>reverse-engineering/game-mechanics/god-mode.md</c>, and is the
/// layout the shipped save tooling writes
/// (<c>reverse-engineering/save-file/save-format.md</c>, where the file offset is
/// the career offset plus two). Swapping those two names would still satisfy
/// every byte measured here.
/// </para>
/// <para>
/// <b>Not established here.</b> <c>CCareer::GetLink</c> (<c>Career.h:139</c>)
/// has no out-of-line body — the four sites that touch <c>+0x1904</c> are all
/// inlined into other functions — so <see cref="LinkOffset"/> carries the
/// source's guard with the measured stride, exactly as
/// <see cref="RetailCareerSlots"/> carries its reader.
/// </para>
/// </remarks>
public static class RetailCareerRecordLayout
{
    /// <summary><c>CAREER</c> — <c>mov ecx, 0x660620</c> at <c>0x004218B0</c>.</summary>
    public const uint CareerSingletonAddress = 0x00660620u;

    /// <summary><c>mPendingExtraGoodies</c> — the dword ahead of <c>mNode</c>.</summary>
    public const int PendingExtraGoodiesOffset = 0x0000;

    /// <summary><c>mNode</c> — the <c>+ 4</c> in <c>lea eax, [eax + ecx + 4]</c>.</summary>
    public const int NodeArrayOffset = 0x0004;

    /// <summary><c>sizeof(CCareerNode)</c> — the <c>shl eax, 6</c> at <c>0x00420AFD</c>.</summary>
    public const int NodeStride = 64;

    /// <summary><c>MAX_NODES</c> — <c>(0x1904 - 4) / 64</c>.</summary>
    public const int NodeCount = 100;

    /// <summary><c>mNodeLink</c> — the displacement at <c>0x0041BC8A</c>.</summary>
    public const int LinkArrayOffset = 0x1904;

    /// <summary><c>sizeof(CCareerNodeLink)</c> — <c>(0x1F44 - 0x1904) / 200</c>.</summary>
    public const int LinkStride = 8;

    /// <summary><c>MAX_LINKS</c> — <c>MAX_NODES * 2</c>, confirmed by the gap.</summary>
    public const int LinkCount = 200;

    /// <summary><c>mGoodies</c> — the absolute <c>0x00661F44</c> at <c>0x0041B6BA</c>.</summary>
    public const int GoodieArrayOffset = 0x1F44;

    /// <summary><c>MAX_NUM_GOODIES</c> — <c>(0x23F4 - 0x1F44) / 4</c>.</summary>
    public const int GoodieCount = 300;

    /// <summary><c>mKilledThings</c> — the displacement at <c>0x0041C167</c>.</summary>
    public const int KilledThingsOffset = 0x23F4;

    /// <summary><c>mSlots</c> — the displacement <see cref="RetailCareerSlots"/> measures.</summary>
    public const int SlotArrayOffset = 0x2408;

    /// <summary><c>mCareerInProgress</c> — <c>mov [0x00662AA8], 0</c> at <c>0x0041B6E5</c>.</summary>
    public const int CareerInProgressOffset = 0x2488;

    /// <summary><c>mSoundVolume</c> — the <c>0.8f</c> (<c>0x3F4CCCCD</c>) store at <c>0x0041B70D</c>.</summary>
    public const int SoundVolumeOffset = 0x248C;

    /// <summary><c>mMusicVolume</c> — the <c>0.9f</c> (<c>0x3F666666</c>) store at <c>0x0041B717</c>.</summary>
    public const int MusicVolumeOffset = 0x2490;

    /// <summary>
    /// <c>mIsGod[2]</c> — one flag per player, the one two-dword pair <c>0x0041B6A0</c> never
    /// writes, so its position is the header's declaration order rather than a store. The
    /// <c>CPlayer</c> constructor loads it (<c>0x004d27f3</c>), <c>AssignBattleEngine</c> applies a
    /// nonzero flag (<c>0x004d30a1-0x004d30ba</c>: not vulnerable, infinite energy), and
    /// <c>SetIsGod</c> (<c>0x004d3020</c>) writes both copies.
    /// </summary>
    public const int IsGodArrayOffset = 0x2494;

    /// <summary>
    /// The declared <c>mInvertYAxis</c> pair — zeroed at <c>0x0041B701</c> and
    /// <c>0x0041B727</c>. Flight rather than walker per the type remarks.
    /// </summary>
    public const int FlightInvertYAxisArrayOffset = 0x249C;

    /// <summary>
    /// The pair <c>Career.h</c> does not declare, and the whole of the record's eight extra
    /// bytes — zeroed at <c>0x0041B6FB</c> and <c>0x0041B721</c>.
    /// </summary>
    public const int WalkerInvertYAxisArrayOffset = 0x24A4;

    /// <summary><c>mVibration</c> — set to <c>1</c> at <c>0x0041B707</c> and <c>0x0041B72D</c>.</summary>
    public const int VibrationArrayOffset = 0x24AC;

    /// <summary>
    /// <c>mControllerConfigurationNum</c> — set to <c>1</c> at <c>0x0041B6EF</c> and
    /// <c>0x0041B6F6</c>. Two dwords past this is <see cref="RecordSize"/>.
    /// </summary>
    public const int ControllerConfigurationArrayOffset = 0x24B4;

    /// <summary>
    /// <c>sizeof(CCareer)</c> as the shipped <c>Load</c> copies it —
    /// <c>0x92F</c> dwords at <c>0x00421236</c>, <c>0x0042136C</c> and
    /// <c>0x004213EB</c>, <c>0x24BC</c> bytes at <c>0x00421253</c>. Eight more
    /// than the pinned header accounts for, and the eight are
    /// <see cref="WalkerInvertYAxisArrayOffset"/>.
    /// </summary>
    public const int RecordSize = 0x24BC;

    /// <summary>
    /// <c>CCareer::GetNode</c> — <c>Career.h:138</c>, <c>0x00420AF0</c>.
    /// Returns the byte offset of the node from the record base, or
    /// <c>null</c> for retail's one <c>NULL</c> arm. Not bounded above; see the
    /// type remarks.
    /// </summary>
    public static int? NodeOffset(int nodeNumber) =>
        nodeNumber < 0 ? null : unchecked(NodeArrayOffset + nodeNumber * NodeStride);

    /// <summary>
    /// <c>CCareer::GetLink</c> — <c>Career.h:139</c>; inlined in the image, see
    /// the type remarks.
    /// </summary>
    public static int? LinkOffset(int linkNumber) =>
        linkNumber < 0 ? null : unchecked(LinkArrayOffset + linkNumber * LinkStride);
}

/// <summary>
/// The three <c>CCareerNode</c> words the <c>GRADE</c> lookup reads.
/// </summary>
/// <remarks>
/// <c>mComplete</c> at <c>node + 4</c>, <c>mWorldNumber</c> at <c>node + 0x10</c>
/// and <c>mRanking</c> at <c>node + 0x3C</c> — the three displacements
/// <c>0x0041C370</c>, <c>0x0041C349</c> (via the absolute <c>0x00660634</c>) and
/// <c>0x0041C3D1</c> use.
/// </remarks>
public readonly record struct RetailWorldGradeNode(
    int WorldNumber,
    int Complete,
    float Ranking);

/// <summary>
/// <c>GRADE(int world_num)</c> — the released "what grade does this world hold"
/// lookup the debriefing and goodie tests are written in terms of.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop:
/// <c>references/Onslaught/Career.cpp:640-657</c>, over the
/// <c>COMPLETE_LEVEL</c> macro at <c>Career.cpp:565</c>. Retail identity:
/// <c>0x0041C330</c> in the pristine <c>74154bfa…</c> image, file offset
/// = VA - 0x400000. The manifest files this as <c>CCareer__GetGradeForWorld</c>;
/// it is a free function, not a member — it takes its world number on the stack
/// and reaches <c>CAREER</c> through the absolutes <c>0x00660634</c> and
/// <c>0x00660624</c>.
/// </para>
/// <para>
/// <b>Source and retail agree, including on the fact that the guard is on the
/// wrong side of the dereference.</b> <c>COMPLETE_LEVEL(world_num)</c> expands to
/// <c>CAREER.GetNodeFromWorldNo(a)-&gt;mComplete == TRUE</c> with no null check,
/// and the shipped body does exactly that: a miss in the first scan falls to
/// <c>LOG.AddMessage</c> at <c>0x0041C360</c>, then <c>xor eax, eax</c> at
/// <c>0x0041C36E</c>, then <c>cmp dword ptr [eax + 4], 1</c> at
/// <c>0x0041C370</c> — an access violation at linear address 4. The source's
/// <c>if (cn) … else LOG.AddMessage(…)</c> guard sits <i>after</i> that, on a
/// <b>second</b> scan of the same unchanged table, so it can only fire if the
/// first scan succeeded and the second failed. It is compiled
/// (<c>0x0041C3A4</c>) and unreachable. <see cref="GradeByteForWorld"/> throws
/// where retail faults.
/// </para>
/// <para>
/// <b><c>mComplete</c> is compared against the literal 1.</b>
/// <c>0x0041C370</c> is <c>cmp dword ptr [eax + 4], 1</c>, so a
/// <c>BOOL</c> of 2 counts as <i>incomplete</i> — the same
/// <c>== TRUE</c> shape <see cref="RetailCareerSlots"/> and
/// <see cref="RetailCareerNode.SetBaseThingExistTo"/> have.
/// </para>
/// <para>
/// <b>The <c>-9999</c> sentinel does not exist at run time.</b>
/// <c>Career.cpp:642</c> initialises <c>r</c> to <c>-9999</c> and passes it to
/// <c>GetGradeFromRanking</c> on the failure paths; MSVC folded the whole call.
/// Both failure arms are a bare <c>mov al, 0x45</c> — <c>'E'</c> — at
/// <c>0x0041C3FE</c> (world not complete) and <c>0x0041C3B7</c> (the unreachable
/// second-scan miss), with no float loaded and no comparison performed. So there
/// is no path on which a rebuild could observe the sentinel, and
/// <see cref="IncompleteGradeByte"/> is the pinned surface instead.
/// </para>
/// <para>
/// <b>Not established here.</b> The ranking path is
/// <see cref="RetailCareerGrade.GradeByteFromRanking"/> inlined verbatim
/// (<c>0x0041C3D8-0x0041C424</c>, including the same
/// <c>test ah, 0x40</c> that hands a NaN the top grade), and the widening to
/// <c>WCHAR</c> through <c>0x004F7BF0</c> is the same one that owner declines to
/// claim outside ASCII. The node scan is
/// <see cref="RetailCareerNodeTable.Find"/>.
/// </para>
/// </remarks>
public static class RetailWorldGrade
{
    /// <summary>
    /// The grade an incomplete world holds — <c>mov al, 0x45</c> at
    /// <c>0x0041C3FE</c>. Equal to
    /// <see cref="RetailCareerGrade.FailedGrade"/>, but constant-folded rather
    /// than computed.
    /// </summary>
    public const byte IncompleteGradeByte = (byte)'E';

    /// <summary>
    /// <c>GRADE(int world_num)</c> — <c>Career.cpp:640-657</c>,
    /// <c>0x0041C330</c>.
    /// </summary>
    /// <param name="nodes">The nodes in index order; the scan is first-match.</param>
    /// <param name="worldNumber">The world to grade.</param>
    /// <exception cref="InvalidOperationException">
    /// When no node carries <paramref name="worldNumber"/>: retail reads
    /// <c>[NULL + 4]</c> at <c>0x0041C370</c>.
    /// </exception>
    public static byte GradeByteForWorld(
        IReadOnlyList<RetailWorldGradeNode> nodes, int worldNumber)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        for (int index = 0; index < nodes.Count; index++)
        {
            if (nodes[index].WorldNumber != worldNumber)
            {
                continue;
            }

            // cmp dword ptr [eax + 4], 1: a literal TRUE, not "non-zero".
            if (nodes[index].Complete != 1)
            {
                return IncompleteGradeByte;
            }

            // The second scan re-finds the same node over an unchanged table.
            return RetailCareerGrade.GradeByteFromRanking(nodes[index].Ranking);
        }

        throw new InvalidOperationException(
            $"World {worldNumber} is not in the node table; COMPLETE_LEVEL " +
            "dereferences the NULL that GetNodeFromWorldNo just returned, at " +
            "0x0041C370.");
    }
}
