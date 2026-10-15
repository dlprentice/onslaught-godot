// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailActiveReaderGraphTests
{
    [Fact]
    public void SameTargetIsExactNoOpAndPreservesNewestFirstPosition()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);
        graph.CreateReaderCell(24, 7);

        RetailActiveReaderAction[] actions = graph.SetReader(12, 7);

        Assert.Empty(actions);
        Assert.Equal(7, graph.TargetOf(12));
        Assert.Equal([24, 12], graph.ReadersNewestFirst(7));
    }

    [Fact]
    public void RebindOrdersDetachPublishAttachAndKeepsOldEmptyContainer()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);

        RetailActiveReaderAction[] actions = graph.SetReader(12, 9);

        Assert.Equal(
            [
                Action(RetailActiveReaderActionKind.DetachOldTarget, 12, 7),
                Action(RetailActiveReaderActionKind.PublishNewTarget, 12, 9),
                Action(RetailActiveReaderActionKind.AttachNewTarget, 12, 9),
            ],
            actions);
        Assert.Equal(9, graph.TargetOf(12));
        Assert.True(graph.TargetHasReverseContainer(7));
        Assert.Empty(graph.ReadersNewestFirst(7));
        Assert.Equal([12], graph.ReadersNewestFirst(9));
    }

    [Fact]
    public void NullClearDetachesThenPublishesNullWithoutAttach()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);

        RetailActiveReaderAction[] actions = graph.SetReader(12, null);

        Assert.Equal(
            [
                Action(RetailActiveReaderActionKind.DetachOldTarget, 12, 7),
                Action(RetailActiveReaderActionKind.PublishNewTarget, 12, null),
            ],
            actions);
        Assert.Null(graph.TargetOf(12));
        Assert.Empty(graph.ReadersNewestFirst(7));
    }

    [Fact]
    public void TargetShutdownInvalidatesNewestFirstThenClearsReverseContainer()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);
        graph.CreateReaderCell(24, 7);
        graph.CreateReaderCell(40, 7);

        RetailActiveReaderAction[] actions = graph.ShutdownTarget(7);

        Assert.Equal(
            [
                Action(RetailActiveReaderActionKind.InvalidateReaderCell, 40, 7),
                Action(RetailActiveReaderActionKind.InvalidateReaderCell, 24, 7),
                Action(RetailActiveReaderActionKind.InvalidateReaderCell, 12, 7),
                Action(RetailActiveReaderActionKind.ClearTargetReverseMembership, null, 7),
            ],
            actions);
        Assert.Null(graph.TargetOf(12));
        Assert.Null(graph.TargetOf(24));
        Assert.Null(graph.TargetOf(40));
        Assert.False(graph.TargetHasReverseContainer(7));
    }

    [Fact]
    public void TargetDeathTouchesOnlyRegisteredCellNotAdjacentUnitAiState()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);
        int retainedGate10 = 1;
        int result18 = -4;
        int result1C = 9;

        graph.ShutdownTarget(7);

        Assert.Null(graph.TargetOf(12));
        Assert.Equal(1, retainedGate10);
        Assert.Equal(-4, result18);
        Assert.Equal(9, result1C);
    }

    [Fact]
    public void UnitAiDestructionDetachesOutboundBeforeInvalidatingInbound()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(40, 7);
        graph.CreateReaderCell(36, 8);
        graph.CreateReaderCell(12, 9);
        graph.CreateReaderCell(900, 100);
        graph.CreateReaderCell(901, 100);

        RetailActiveReaderAction[] actions = graph.DestroyUnitAiReaderOwner(
            unitAiTargetIdentity: 100,
            readerCell28: 40,
            readerCell24: 36,
            readerCell0C: 12);

        Assert.Equal(
            [
                Action(RetailActiveReaderActionKind.DetachOldTarget, 40, 7),
                Action(RetailActiveReaderActionKind.DetachOldTarget, 36, 8),
                Action(RetailActiveReaderActionKind.DetachOldTarget, 12, 9),
                Action(RetailActiveReaderActionKind.InvalidateReaderCell, 901, 100),
                Action(RetailActiveReaderActionKind.InvalidateReaderCell, 900, 100),
                Action(RetailActiveReaderActionKind.ClearTargetReverseMembership, null, 100),
            ],
            actions);
        Assert.False(graph.ContainsReaderCell(40));
        Assert.False(graph.ContainsReaderCell(36));
        Assert.False(graph.ContainsReaderCell(12));
        Assert.Null(graph.TargetOf(900));
        Assert.Null(graph.TargetOf(901));
    }

    [Fact]
    public void OrdinaryApiCannotDuplicateReaderMembership()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(12, 7);

        Assert.Empty(graph.SetReader(12, 7));
        Assert.Empty(graph.SetReader(12, 7));
        Assert.Equal([12], graph.ReadersNewestFirst(7));
        Assert.Throws<InvalidOperationException>(() =>
            graph.CreateReaderCell(12, 7));
    }

    private static RetailActiveReaderAction Action(
        RetailActiveReaderActionKind kind,
        int? reader,
        int? target) => new(kind, reader, target);
}
