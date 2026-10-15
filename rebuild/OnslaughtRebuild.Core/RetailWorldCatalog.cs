// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One node of the released career level structure: Stuart's
/// <c>level_structure[NUM_LEVELS][5]</c> row
/// (<c>references/Onslaught/Career.cpp:24-70</c>, <c>Career.h:103</c>
/// <c>NUM_LEVELS 43</c>).
/// </summary>
/// <param name="Index">Row order, which is also the node index retail's
/// <c>mToNode</c> links point at.</param>
/// <param name="WorldNumber">Column 0 — the world this node carries.</param>
/// <param name="LowerChildIndex">Column 1 — the lower child node, or -1.</param>
/// <param name="HigherChildIndex">Column 2 — the higher child node, or -1.</param>
/// <param name="PrimaryBaseWorld">Column 3 — the world whose base-thing
/// existence vector updates when this node's primaries complete, or -1.</param>
/// <param name="SecondaryBaseWorld">Column 4 — the same for all secondary
/// objectives complete, or -1.</param>
public sealed record RetailWorldNode(
    int Index,
    int WorldNumber,
    int LowerChildIndex,
    int HigherChildIndex,
    int PrimaryBaseWorld,
    int SecondaryBaseWorld);

/// <summary>
/// Exact measured RLWD preamble before one world's compiled script objects.
/// The words are intentionally unnamed: available evidence proves their
/// serialized values and position, not their retail field identities.
/// </summary>
public sealed record RetailWorldLevelHeader(
    int Version,
    IReadOnlyList<int> HeaderWords,
    IReadOnlyList<string> Names,
    IReadOnlyList<int> TrailingWords);

/// <summary>
/// Bounded payload census for a measured career world. A census admits bytes
/// and structure only; it does not claim a constructible simulation session.
/// </summary>
public sealed record RetailWorldPayloadCensus(
    int WorldNumber,
    int ArchiveBytes,
    string ArchiveSha256,
    int LevelWorldBytes,
    RetailWorldLevelHeader LevelHeader,
    int ScriptObjectCount,
    int ActorHeaderA,
    int ActorHeaderB,
    int InitialActorCount,
    string HeightfieldOwnerTag,
    int HeightfieldEnvelopeBytes,
    string HeightfieldEnvelopeSha256,
    string BaseWorldOwnerTag,
    int BaseWorldPayloadBytes,
    string BaseWorldPayloadSha256);

/// <summary>
/// The released 43-node career graph and the world laws that hang off it.
///
/// <para><b>Authority.</b> The table is Stuart's pinned
/// <c>Career.cpp:24-70</c> verbatim (<c>num_nodes = 43</c> at
/// <c>Career.cpp:73</c>); the archive path is the retail install's own
/// <c>data/resources/&lt;world&gt;_res_PC.aya</c> layout, the same law
/// <c>rebuild/tools/materialize_retail_assets.py</c> already pins for world
/// 100 and the install confirms for every world; the subtree ordering is
/// <c>CCareer::IsWorldLater</c> / <c>CCareer::Later</c>
/// (<c>Career.cpp:324-374</c>); and selectability is the measured
/// <c>ReCalcLinks</c> unlock already pinned in PARITY.md — completing a node
/// sets its outgoing link <c>CN_COMPLETE</c>, which is what admits the child
/// (world 110 after a Level 100 Won) even though the child's own
/// <c>mComplete</c> stays 0.</para>
///
/// <para><b>What this deliberately does not claim.</b> It does not model
/// retail's <c>SetCurrentLevelToHighestAvailable</c> default-selection rule
/// (that owner is not in the source drop), the world-500 rocket special case
/// beyond what <c>RetailCareerReCalcLinks</c> already carries, or any world's
/// constructed content — a node existing here says nothing about whether the
/// reconstruction can build that world.</para>
/// </summary>
public static class RetailWorldCatalog
{
    /// <summary><c>Career.h:103</c> — <c>#define NUM_LEVELS 43</c>.</summary>
    public const int NodeCount = 43;

    /// <summary>Node 0's world — the graph root every career starts on.</summary>
    public const int RootWorldNumber = 100;

    /// <summary>
    /// The released level structure, <c>Career.cpp:24-70</c> verbatim.
    /// Columns: world, lower child node, higher child node, primary base
    /// world, secondary base world.
    /// </summary>
    private static readonly (int World, int LowerChild, int HigherChild, int PrimaryBase, int SecondaryBase)[]
        s_levelStructure =
        [
            (100, 1, -1, 110, -1),   //  0
            (110, 2, -1, -1, -1),    //  1
            (200, 3, 4, 211, 212),   //  2
            (211, 5, 6, 231, 232),   //  3
            (212, 5, 6, 231, 232),   //  4
            (221, 7, 8, -1, -1),     //  5
            (222, 7, 8, -1, -1),     //  6
            (231, 9, -1, -1, -1),    //  7
            (232, 9, -1, -1, -1),    //  8
            (300, 10, 11, 311, 312), //  9
            (311, 12, 13, 321, 322), // 10
            (312, 12, 13, 321, 322), // 11
            (321, 14, 15, -1, -1),   // 12
            (322, 14, 15, -1, -1),   // 13
            (331, 16, -1, -1, -1),   // 14
            (332, 16, -1, -1, -1),   // 15
            (400, 17, 18, 411, 412), // 16
            (411, 19, 20, 431, 432), // 17
            (412, 19, 20, 431, 432), // 18
            (421, 21, 22, -1, -1),   // 19
            (422, 21, 22, -1, -1),   // 20
            (431, 23, -1, -1, -1),   // 21
            (432, 23, -1, -1, -1),   // 22
            (500, 24, 25, -1, -1),   // 23
            (511, 26, 27, -1, -1),   // 24
            (512, 28, 29, -1, -1),   // 25
            (521, 30, -1, -1, -1),   // 26
            (522, 30, -1, -1, -1),   // 27
            (523, 30, -1, -1, -1),   // 28
            (524, 30, -1, -1, -1),   // 29
            (600, 31, 32, -1, -1),   // 30
            (611, 33, 34, 621, 622), // 31
            (612, 33, 34, 621, 622), // 32
            (621, 35, -1, -1, -1),   // 33
            (622, 35, -1, -1, -1),   // 34
            (700, 36, -1, -1, -1),   // 35
            (710, 37, -1, 720, -1),  // 36
            (720, 38, 39, 731, 732), // 37
            (731, 40, -1, -1, -1),   // 38
            (732, 41, -1, -1, -1),   // 39
            (741, -1, -1, -1, -1),   // 40
            (742, 42, -1, -1, -1),   // 41
            (800, -1, -1, -1, -1),   // 42
        ];

    private static readonly RetailWorldNode[] s_nodes =
        s_levelStructure
            .Select((row, index) => new RetailWorldNode(
                index,
                row.World,
                row.LowerChild,
                row.HigherChild,
                row.PrimaryBase,
                row.SecondaryBase))
            .ToArray();

    /// <summary>
    /// Generalized census entries added after the earlier specialized
    /// world-110/world-200 measurements. This is deliberately not a claim that
    /// every graph node has been measured. World 300 is the first payload whose
    /// three-name/non-zero RLWD preamble falsified the old shared-header model.
    /// </summary>
    private static readonly RetailWorldPayloadCensus[] s_payloadCensuses =
    [
        new(
            300,
            1_927_844,
            "7293BCBE3CBB6C88B2E19A287CB53DE132A8BCADD6C2736B0DA7483726C9EFE4",
            78_848,
            new RetailWorldLevelHeader(
                50,
                [3, 47, 300],
                ["Standard", "Laser", "Blaster"],
                [1, 1, 1, 0, 3]),
            8,
            10,
            0,
            36,
            "ERES",
            668_660,
            "68A181F9EC3099A0BE52BF4A063350A35E43C20664F2E07572BDB44D472ACB1A",
            "WRES",
            77_113,
            "3C153D55605B9E82A8827640D8E11084514F949E043200E9141B2185256C0DFC"),
    ];

    public static IReadOnlyList<RetailWorldNode> Nodes => s_nodes;

    /// <summary>
    /// Non-exhaustive measured payload censuses. Graph membership remains
    /// authoritative in <see cref="Nodes"/>; a row here does not make a world
    /// constructible.
    /// </summary>
    public static IReadOnlyList<RetailWorldPayloadCensus> MeasuredPayloadCensuses =>
        s_payloadCensuses;

    /// <summary>
    /// The released level-archive law: every career world's resources ship as
    /// <c>data/resources/&lt;world&gt;_res_PC.aya</c>. World 100's copy is the
    /// archive <c>materialize_retail_assets.py</c> has always pinned; the
    /// world-110 archive was measured at the same path on 2026-08-22
    /// (SHA-256 <c>4e041c75…3c2b</c>).
    /// </summary>
    public static string ArchiveRelativePath(int worldNumber) =>
        $"data/resources/{worldNumber}_res_PC.aya";

    public static RetailWorldNode? Find(int worldNumber) =>
        s_nodes.FirstOrDefault(node => node.WorldNumber == worldNumber);

    /// <summary>Finds a measured payload census without inventing missing rows.</summary>
    public static RetailWorldPayloadCensus? FindPayloadCensus(int worldNumber) =>
        s_payloadCensuses.FirstOrDefault(census => census.WorldNumber == worldNumber);

    /// <summary>Column 1 of the finished node's row — the primary next world.</summary>
    public static int? LowerChildWorld(int worldNumber) => ChildWorld(Find(worldNumber)?.LowerChildIndex);

    /// <summary>Column 2 of the finished node's row — the secondary next world.</summary>
    public static int? HigherChildWorld(int worldNumber) => ChildWorld(Find(worldNumber)?.HigherChildIndex);

    private static int? ChildWorld(int? childIndex) =>
        childIndex is int index && index >= 0 ? s_nodes[index].WorldNumber : null;

    /// <summary>
    /// Stuart's <c>CCareer::IsWorldLater</c> (<c>Career.cpp:324-338</c>):
    /// TRUE when <paramref name="diesOnWorld"/>'s node subtree — reached
    /// through lower then higher child links, <c>Career.cpp:341-374</c> —
    /// contains <paramref name="currentWorld"/>'s node. Equal worlds are
    /// FALSE by the caller's own <c>currentNode != diesOnNode</c> guard, and
    /// worlds in disjoint subtrees are FALSE because the walk never reaches
    /// them.
    /// </summary>
    public static bool IsWorldLater(int currentWorld, int diesOnWorld)
    {
        RetailWorldNode? currentNode = Find(currentWorld);
        RetailWorldNode? diesOnNode = Find(diesOnWorld);
        if (currentNode is null || diesOnNode is null || currentNode == diesOnNode)
        {
            return false;
        }

        return Later(diesOnNode.Index, currentNode.Index, new HashSet<int>());
    }

    /// <summary>
    /// <c>CCareer::Later</c> (<c>Career.cpp:341-374</c>). The shipped graph is
    /// a tree so retail's unguarded recursion terminates; the visited set only
    /// makes that precondition explicit and is unreachable on this table.
    /// </summary>
    private static bool Later(int diesOnNodeIndex, int currentNodeIndex, HashSet<int> visited)
    {
        if (diesOnNodeIndex == currentNodeIndex)
        {
            return true;
        }

        if (!visited.Add(diesOnNodeIndex))
        {
            return false;
        }

        RetailWorldNode node = s_nodes[diesOnNodeIndex];
        return (node.LowerChildIndex >= 0 &&
                Later(node.LowerChildIndex, currentNodeIndex, visited)) ||
               (node.HigherChildIndex >= 0 &&
                Later(node.HigherChildIndex, currentNodeIndex, visited));
    }

    /// <summary>
    /// Whether the level selector may offer this world given a career.
    ///
    /// <para>The root world is always offered (a cold career's selector lands
    /// on it — <c>Career.cpp:1065/1118</c> point the player at the highest
    /// available, which on a cold career is world 100). Any other node is
    /// offered once at least one link pointing at it has left
    /// <c>CN_NOT_COMPLETE</c>: that is exactly what the already-pinned
    /// <c>ReCalcLinks</c> unlock produces for world 110 after a Level 100 Won
    /// (PARITY.md, <c>Level100Won_UnlocksWorld110…</c>) — the child's own
    /// <c>mComplete</c> stays 0, so requiring it would lock the second world
    /// forever. <c>CN_COMPLETE_BROKEN</c> parents still count: the broken
    /// marker means a sibling path completed the node first, and the node was
    /// reached either way (<c>Career.cpp:484-497</c>).</para>
    /// </summary>
    public static bool IsWorldSelectable(RetailCareerCampaign career, int worldNumber)
    {
        ArgumentNullException.ThrowIfNull(career);
        RetailWorldNode? node = Find(worldNumber);
        if (node is null)
        {
            return false;
        }

        if (node.WorldNumber == RootWorldNumber)
        {
            return true;
        }

        return career.Links.Any(link =>
            link.ToNode == node.Index &&
            link.LinkType != RetailCareerNodeLink.NotComplete);
    }
}
