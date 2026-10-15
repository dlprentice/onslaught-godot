// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One released career-graph node — the 0x40-byte record the shipped career
/// save is an array of — together with the two bit accessors that address its
/// base-things bitmap.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp:86-154</c> and
/// <c>Career.h:76-100</c>. Retail identities in the pristine <c>74154bfa…</c>
/// image, all read at file offset VA - 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x0041B740</c> <c>CCareerNode::Blank</c> — <c>Career.cpp:93-111</c>.</item>
/// <item><c>0x0041B770</c> <c>CCareerNode::SetBaseThingExistTo</c> — <c>Career.cpp:133-154</c>.</item>
/// <item>
/// <c>0x0041BB77-0x0041BB9C</c> <c>CCareerNode::DoesBaseThingExist</c> —
/// <c>Career.cpp:117-129</c>, which has <b>no standalone body</b>: it is
/// inlined into <c>CCareer::DoesBaseThingExist</c> at <c>0x0041BB40</c>.
/// </item>
/// </list>
/// <para>
/// <b>Source and retail agree</b>, and the compiled record confirms
/// <c>Career.h</c>'s declaration order and size exactly. <c>Blank</c> writes
/// <c>+0x10</c> and <c>+0x04</c> and <c>+0x38</c> as zero, <c>+0x08</c> and
/// <c>+0x0C</c> as <c>-1</c>, then <c>rep stosd</c> with <c>ecx = 9</c> and
/// <c>eax = 0xFFFFFFFF</c> from <c>+0x14</c>, then <c>0xBF800000</c> into
/// <c>+0x3C</c>. Nine words is <c>BASE_THINGS_EXISTS_MEM_REQ</c>
/// (<c>288 &gt;&gt; 5</c>, <c>Career.h:13-14</c>), which fixes the record at
/// <c>0x40</c> bytes; <c>CCareer::GetNodeFromWorldNo</c> at <c>0x0041B8F0</c>
/// independently walks the array with <c>add edx, 0x40</c>, so the stride is
/// measured, not assumed.
/// </para>
/// <para>
/// <b><c>Blank</c> deliberately leaves <c>+0x00</c> alone.</b> There is no
/// store to <c>mIsStartOfNewIsland</c> anywhere in <c>0x0041B740-0x0041B769</c>,
/// which matches the source: the field is dead
/// (<c>Career.h:89</c>, and the commented-out assignment at
/// <c>Career.cpp:204</c>) and the constructor's only body is
/// <c>Blank()</c>. A retail node therefore carries whatever was in that word
/// before. This type cannot reproduce indeterminate memory and does not try —
/// the C# field starts at <c>false</c>. What is pinned is the <i>non-touch</i>:
/// a value present before <see cref="Blank"/> survives it.
/// </para>
/// <para>
/// <b>Neither bit accessor has a range guard</b>, and that is not an oversight
/// of the decompiler: <c>0x0041B770</c> begins at the shift, with no compare
/// against 288 and no early return, where the sibling pair
/// <c>CCareer::GetSlot</c>/<c>SetSlot</c> does test its index
/// (<see cref="RetailCareerSlots"/>). An out-of-range offset in retail
/// therefore reads or writes past <c>mBaseThingsExists</c> into
/// <c>mNumAttempts</c>, <c>mRanking</c>, and the next node. Core must not
/// pretend to model a buffer overrun, so this type raises instead of
/// corrupting; the guard-free <i>shape</i> is what is carried over, and the
/// test pins that offset 288 is out of range rather than silently wrapping.
/// </para>
/// <para>
/// <b>The mask law is shared with <see cref="RetailCareerSlots"/> and shipped
/// with a redundant branch.</b> Both sites compile
/// <c>int m = 1; if (b &gt; 0) m = m &lt;&lt; b;</c> into
/// <c>and ecx, 0x1f</c> / <c>jle</c> / <c>shl edx, cl</c>. The <c>jle</c> reads
/// the flags the <c>and</c> left, and the <c>and</c> result can never be
/// negative, so <c>jle</c> here is exactly <c>je</c>: skip the shift when the
/// bit index is zero. <c>shl</c> by <c>cl = 0</c> would have produced the same
/// <c>1</c>, so the branch changes nothing — it is retained here only because
/// retail retains it.
/// </para>
/// </remarks>
public sealed class RetailCareerNode
{
    /// <summary><c>BASE_THINGS_EXISTS_SIZE</c> — <c>Career.h:13</c>.</summary>
    public const int BaseThingsExistsSize = 288;

    /// <summary><c>BASE_THINGS_EXISTS_MEM_REQ</c> — <c>Career.h:14</c>; the <c>rep stosd</c> count at <c>0x0041B754</c>.</summary>
    public const int BaseThingsExistsWords = BaseThingsExistsSize >> 5;

    /// <summary>Compiled record size — the <c>0x40</c> stride at <c>0x0041B90A</c>.</summary>
    public const int RecordSizeInBytes = 0x40;

    /// <summary><c>mRanking</c>'s blanked value — <c>0xBF800000</c> at <c>0x0041B761</c>.</summary>
    public const float BlankRanking = -1.0f;

    private readonly int[] _baseThingsExists = new int[BaseThingsExistsWords];

    /// <summary>Constructs and blanks, as <c>CCareerNode::CCareerNode</c> does — <c>Career.cpp:86-89</c>.</summary>
    public RetailCareerNode() => Blank();

    /// <summary><c>mIsStartOfNewIsland</c> — <c>+0x00</c>. Dead, and never written by <see cref="Blank"/>.</summary>
    public int IsStartOfNewIsland { get; set; }

    /// <summary><c>mComplete</c> — <c>+0x04</c>. A <c>BOOL</c>, compared against literal 1 by its readers.</summary>
    public int Complete { get; set; }

    /// <summary><c>mLowerLink</c> — <c>+0x08</c>.</summary>
    public int LowerLink { get; set; }

    /// <summary><c>mHigherLink</c> — <c>+0x0C</c>.</summary>
    public int HigherLink { get; set; }

    /// <summary><c>mWorldNumber</c> — <c>+0x10</c>; the key <c>GetNodeFromWorldNo</c> scans.</summary>
    public int WorldNumber { get; set; }

    /// <summary><c>mNumAttempts</c> — <c>+0x38</c>.</summary>
    public int NumAttempts { get; set; }

    /// <summary><c>mRanking</c> — <c>+0x3C</c>; the input to <see cref="RetailCareerGrade"/>.</summary>
    public float Ranking { get; set; }

    /// <summary>The raw <c>mBaseThingsExists</c> words at <c>+0x14</c>.</summary>
    public IReadOnlyList<int> BaseThingsExistsWordsView => _baseThingsExists;

    /// <summary>
    /// <c>CCareerNode::Blank</c> — <c>Career.cpp:93-111</c>, <c>0x0041B740</c>.
    /// Every base thing starts present, every link starts <c>-1</c>, and the
    /// ranking starts at <c>-1.0f</c> — a value <see cref="RetailCareerGrade"/>
    /// maps to <c>'E'</c>.
    /// </summary>
    public void Blank()
    {
        LowerLink = -1;
        HigherLink = -1;
        WorldNumber = 0;
        Complete = 0;
        NumAttempts = 0;

        for (int word = 0; word < BaseThingsExistsWords; word++)
        {
            _baseThingsExists[word] = unchecked((int)0xFFFFFFFF);
        }

        Ranking = BlankRanking;
    }

    /// <summary>
    /// <c>CCareerNode::DoesBaseThingExist</c> — <c>Career.cpp:117-129</c>,
    /// inlined at <c>0x0041BB77</c>. Returns the retail <c>BOOL</c>: the
    /// <c>neg</c>/<c>sbb</c>/<c>neg</c> tail at <c>0x0041BB96</c> normalises the
    /// masked word to 1 or 0, so unlike
    /// <see cref="RetailWeaponStoreReadouts.IsEnergyWeapon"/> this reader does
    /// <b>not</b> leak a raw stored value.
    /// </summary>
    public int DoesBaseThingExist(int offset)
    {
        int word = offset >> 5;
        int mask = MaskFor(offset);
        RequireInRange(word, offset);
        return (_baseThingsExists[word] & mask) != 0 ? 1 : 0;
    }

    /// <summary>
    /// <c>CCareerNode::SetBaseThingExistTo</c> — <c>Career.cpp:133-154</c>,
    /// <c>0x0041B770</c>.
    /// </summary>
    /// <param name="value">
    /// The retail <c>BOOL</c>. <c>0x0041B788</c> is
    /// <c>cmp dword ptr [esp + 0xC], 1</c>, so <b>only literal 1 sets the
    /// bit</b>; every other value — including a "true" of 2 — takes the clear
    /// arm. That matters because <c>Career.cpp:526</c> forwards
    /// <c>END_LEVEL_DATA.mBaseThingsLeft[i]</c> straight in, and nothing on
    /// that path guarantees the array holds canonical <c>BOOL</c>s.
    /// </param>
    public void SetBaseThingExistTo(int offset, int value)
    {
        int word = offset >> 5;
        int mask = MaskFor(offset);
        RequireInRange(word, offset);

        if (value == 1)
        {
            _baseThingsExists[word] |= mask;
        }
        else
        {
            _baseThingsExists[word] &= ~mask;
        }
    }

    /// <summary>
    /// The shared bit-mask law — <c>Career.cpp:119-126</c> and
    /// <c>:1364-1371</c>, compiled identically at <c>0x0041B77C</c>,
    /// <c>0x0041BB80</c> and <c>0x004214F2</c>.
    /// </summary>
    internal static int MaskFor(int index)
    {
        int bit = index & 31;
        int mask = 1;
        if (bit > 0)
        {
            mask <<= bit;
        }

        return mask;
    }

    private static void RequireInRange(int word, int offset)
    {
        if (word < 0 || word >= BaseThingsExistsWords)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "Retail has no guard here and would read or write past " +
                "mBaseThingsExists; Core refuses instead of modelling the overrun.");
        }
    }
}

/// <summary>
/// The released career-graph node array and its only lookup —
/// <c>CCareer::GetNodeFromWorldNo</c>.
/// </summary>
/// <remarks>
/// <para>
/// Owner in the pinned drop: <c>references/Onslaught/Career.cpp</c>,
/// declaration at <c>Career.h:116</c> and <c>Career.h:193</c>. Retail identity:
/// <c>0x0041B8F0</c>, a linear scan of <c>num_nodes</c> records comparing
/// <c>mWorldNumber</c> — the instruction is <c>lea edx, [ecx + 0x14]</c>, which
/// is <c>this + 0x14</c> and therefore <c>node + 0x10</c>, since the array
/// itself begins at <c>this + 4</c>; the inline scan in <c>GRADE</c> starts at
/// the same absolute <c>0x00660634</c> — and returning
/// <c>this + 4 + i * 0x40</c>, which also fixes <c>mNode</c> at <c>CCareer+4</c>
/// with no <c>CSArray</c> header.
/// </para>
/// <para>
/// <b>Source and retail agree, and the shipped node count is measurable.</b>
/// <c>num_nodes</c> lives at <c>0x00624184</c> and the pristine image ships it
/// as <c>0x2B</c> — 43, which is <c>NUM_LEVELS</c> at <c>Career.h:103</c>, not
/// <c>MAX_NODES</c> 100. The scan is first-match and the loop bound is
/// <c>num_nodes</c>, so duplicate world numbers resolve to the lowest index and
/// records past <c>num_nodes</c> are invisible.
/// </para>
/// <para>
/// <b>A miss returns NULL and the callers do not check.</b> <c>0x0041B924</c>
/// logs and returns zero, and every reader in
/// <c>CCareer::IsEpisodeAvailable</c> then executes
/// <c>cmp dword ptr [eax + 4], …</c> against that zero — an access violation at
/// linear address 4, not a false. The one caller that <i>does</i> check is
/// <c>CCareer::DoesBaseThingExist</c> at <c>0x0041BB75</c>, which returns
/// <b>TRUE</b> for an unknown world (<c>Career.cpp:319-320</c>). This type
/// keeps both behaviours distinct: <see cref="Find"/> returns <c>null</c>,
/// <see cref="CompleteFlagOf"/> throws.
/// </para>
/// </remarks>
public sealed class RetailCareerNodeTable
{
    /// <summary><c>MAX_NODES</c> — <c>Career.h:15</c>.</summary>
    public const int MaxNodes = 100;

    /// <summary><c>NUM_LEVELS</c> — <c>Career.h:103</c>; the value <c>num_nodes</c> ships as at <c>0x00624184</c>.</summary>
    public const int ShippedNodeCount = 43;

    private readonly List<RetailCareerNode> _nodes = new();

    /// <summary><c>num_nodes</c> — the scan bound.</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>The nodes in index order.</summary>
    public IReadOnlyList<RetailCareerNode> Nodes => _nodes;

    /// <summary>Appends a node, as career construction fills <c>mNode</c> in <c>level_structure</c> order.</summary>
    public RetailCareerNode Add(int worldNumber, int complete)
    {
        if (_nodes.Count >= MaxNodes)
        {
            throw new InvalidOperationException(
                "MAX_NODES is 100; Career.cpp:192-196 refuses to build past it.");
        }

        var node = new RetailCareerNode { WorldNumber = worldNumber, Complete = complete };
        _nodes.Add(node);
        return node;
    }

    /// <summary>
    /// <c>CCareer::GetNodeFromWorldNo</c> — <c>0x0041B8F0</c>. First match by
    /// <c>mWorldNumber</c>, or <c>null</c>.
    /// </summary>
    public RetailCareerNode? Find(int worldNumber)
    {
        for (int index = 0; index < _nodes.Count; index++)
        {
            if (_nodes[index].WorldNumber == worldNumber)
            {
                return _nodes[index];
            }
        }

        return null;
    }

    /// <summary>
    /// The base-world rows a load of <paramref name="worldNumber"/> skips: those
    /// whose <c>CCareer::DoesBaseThingExist</c> (<c>0x0041bb20</c>, called for
    /// each row at <c>0x0050cf8f</c>) is false. Levels 850-899 keep every row
    /// (<c>0x004725d0</c> tests <c>849 &lt; level &lt; 900</c>), as does a
    /// world with no node (<c>0x0041bb9f</c>).
    /// </summary>
    public IReadOnlyList<int> LostBaseRows(int worldNumber, int rowCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowCount);
        if (worldNumber is > 849 and < 900 || Find(worldNumber) is not { } node)
        {
            return [];
        }

        return Enumerable.Range(0, rowCount).Where(row => node.DoesBaseThingExist(row) == 0).ToArray();
    }

    /// <summary>
    /// The <c>mComplete</c> read every episode gate performs. Throws where
    /// retail dereferences the null the lookup just returned.
    /// </summary>
    public int CompleteFlagOf(int worldNumber) =>
        Find(worldNumber)?.Complete
        ?? throw new InvalidOperationException(
            $"World {worldNumber} is not in the node table; retail reads " +
            "[NULL+4] here and faults.");
}
