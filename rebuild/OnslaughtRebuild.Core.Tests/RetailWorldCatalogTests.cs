// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Parity tests for the released 43-node career graph
/// (<c>references/Onslaught/Career.cpp:24-70</c>, <c>Career.h:103</c>) and the
/// world laws that hang off it. The graph itself is Stuart's pinned source;
/// the archive law and subtree ordering are pinned in
/// <see cref="RetailWorldCatalog"/>; selectability is the measured
/// <c>ReCalcLinks</c> unlock (PARITY.md,
/// <c>Level100Won_UnlocksWorld110…</c>).
/// </summary>
public sealed class RetailWorldCatalogTests
{
    [Fact]
    public void NodeCount_MatchesCareerHeader()
    {
        Assert.Equal(43, RetailWorldCatalog.NodeCount);
        Assert.Equal(43, RetailWorldCatalog.Nodes.Count);
    }

    [Fact]
    public void Root_IsLevel100_AndEveryRowCarriesADistinctKnownWorld()
    {
        Assert.Equal(100, RetailWorldCatalog.RootWorldNumber);
        Assert.Equal(
            RetailWorldCatalog.Nodes.Select(node => node.WorldNumber)
                .OrderBy(world => world).ToArray(),
            RetailWorldCatalog.Nodes.Select(node => node.WorldNumber).Distinct()
                .OrderBy(world => world).ToArray());
        Assert.All(RetailWorldCatalog.Nodes, node =>
            Assert.InRange(node.WorldNumber, 100, 800));
    }

    /// <summary>
    /// Spot-pins the verbatim table against Career.cpp rows the campaign work
    /// depends on: the root's single lower child, the first branching row, and
    /// the two terminal rows.
    /// </summary>
    [Fact]
    public void LevelStructure_SpotRowsMatchPinnedSource()
    {
        Assert.Equal((100, 1, -1, 110, -1), Row(0));
        Assert.Equal((200, 3, 4, 211, 212), Row(2));
        Assert.Equal((742, 42, -1, -1, -1), Row(41));
        Assert.Equal((800, -1, -1, -1, -1), Row(42));
    }

    [Fact]
    public void ChildWorlds_ResolveThroughNodeIndexes()
    {
        Assert.Equal(110, RetailWorldCatalog.LowerChildWorld(100));
        Assert.Null(RetailWorldCatalog.HigherChildWorld(100));
        Assert.Equal(211, RetailWorldCatalog.LowerChildWorld(200));
        Assert.Equal(212, RetailWorldCatalog.HigherChildWorld(200));
        Assert.Null(RetailWorldCatalog.LowerChildWorld(800));
        Assert.Null(RetailWorldCatalog.Find(999));
        Assert.Null(RetailWorldCatalog.LowerChildWorld(999));
    }

    [Fact]
    public void IsWorldLater_MatchesSubtreeSemantics()
    {
        // A child is later than its ancestor.
        Assert.True(RetailWorldCatalog.IsWorldLater(currentWorld: 110, diesOnWorld: 100));
        // Deeper descendants stay later.
        Assert.True(RetailWorldCatalog.IsWorldLater(currentWorld: 231, diesOnWorld: 200));
        // Siblings in disjoint subtrees are not.
        Assert.False(RetailWorldCatalog.IsWorldLater(currentWorld: 212, diesOnWorld: 221));
        // Equal worlds are false by the caller's own guard.
        Assert.False(RetailWorldCatalog.IsWorldLater(currentWorld: 110, diesOnWorld: 110));
        // Unknown worlds are never admitted.
        Assert.False(RetailWorldCatalog.IsWorldLater(currentWorld: 999, diesOnWorld: 100));
        Assert.False(RetailWorldCatalog.IsWorldLater(currentWorld: 100, diesOnWorld: 999));
    }

    /// <summary>
    /// Cold career: only the root is offered. This is retail's cold default —
    /// the selector lands on the highest available, which on a fresh career is
    /// world 100 (<c>Career.cpp:1065/1118</c>).
    /// </summary>
    [Fact]
    public void IsWorldSelectable_ColdCareerOffersOnlyTheRoot()
    {
        var cold = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        Assert.True(RetailWorldCatalog.IsWorldSelectable(cold, 100));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(cold, 110));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(cold, 200));
    }

    /// <summary>
    /// After a Level 100 Won update, the root's lower link leaves
    /// <c>CN_NOT_COMPLETE</c> and world 110 becomes selectable even though
    /// node 1's own <c>mComplete</c> stays 0 — the measured ReCalcLinks
    /// unlock. World 500 stays locked.
    /// </summary>
    [Fact]
    public void IsWorldSelectable_WonRootUnlocksWorld110ButNotDistantNodes()
    {
        RetailCareerCampaign career = RetailCareerReCalcLinks.CreateColdTrainingSlice();

        Assert.NotNull(Level100WonCareerHandoff.TryApply(
            career,
            Level100MissionOutcome.Won,
            Level100MissionTerminalState.FrontEndHandoffReady));

        Assert.True(RetailWorldCatalog.IsWorldSelectable(career, 110));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(career, 200));
        Assert.False(RetailWorldCatalog.IsWorldSelectable(career, 500));
    }

    private static (int, int, int, int, int) Row(int index) =>
        (RetailWorldCatalog.Nodes[index].WorldNumber,
            RetailWorldCatalog.Nodes[index].LowerChildIndex,
            RetailWorldCatalog.Nodes[index].HigherChildIndex,
            RetailWorldCatalog.Nodes[index].PrimaryBaseWorld,
            RetailWorldCatalog.Nodes[index].SecondaryBaseWorld);
}
