// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailPlayerBattleEngineAssignmentTests
{
    private const int Player = 1001;
    private const int OtherPlayer = 1002;
    private const int EngineA = 2001;
    private const int EngineB = 2002;
    private const int PlayerEngineCell = 3001;
    private const int OtherPlayerEngineCell = 3002;
    private const int EngineAPlayerCell = 4001;
    private const int EngineBPlayerCell = 4002;

    [Fact]
    public void FreshNonGodBindPreservesCallOrderAndTargets()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell);
        graph.CreateReaderCell(EngineBPlayerCell);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0));

        Assert.Collection(
            result.Calls,
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                PlayerEngineCell,
                EngineB,
                Action(RetailActiveReaderActionKind.PublishNewTarget, PlayerEngineCell, EngineB),
                Action(RetailActiveReaderActionKind.AttachNewTarget, PlayerEngineCell, EngineB)),
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
                EngineBPlayerCell,
                Player,
                Action(RetailActiveReaderActionKind.PublishNewTarget, EngineBPlayerCell, Player),
                Action(RetailActiveReaderActionKind.AttachNewTarget, EngineBPlayerCell, Player)));
        Assert.Equal(EngineB, graph.TargetOf(PlayerEngineCell));
        Assert.Equal(Player, graph.TargetOf(EngineBPlayerCell));
    }

    [Fact]
    public void ReassignmentPreservesBothReleasedStaleRelationshipSides()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineA);
        graph.CreateReaderCell(OtherPlayerEngineCell, EngineB);
        graph.CreateReaderCell(EngineAPlayerCell, Player);
        graph.CreateReaderCell(EngineBPlayerCell, OtherPlayer);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0));

        Assert.Collection(
            result.Calls,
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                PlayerEngineCell,
                EngineB,
                Action(RetailActiveReaderActionKind.DetachOldTarget, PlayerEngineCell, EngineA),
                Action(RetailActiveReaderActionKind.PublishNewTarget, PlayerEngineCell, EngineB),
                Action(RetailActiveReaderActionKind.AttachNewTarget, PlayerEngineCell, EngineB)),
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
                EngineBPlayerCell,
                Player,
                Action(RetailActiveReaderActionKind.DetachOldTarget, EngineBPlayerCell, OtherPlayer),
                Action(RetailActiveReaderActionKind.PublishNewTarget, EngineBPlayerCell, Player),
                Action(RetailActiveReaderActionKind.AttachNewTarget, EngineBPlayerCell, Player)));

        Assert.Equal(EngineB, graph.TargetOf(PlayerEngineCell));
        Assert.Equal(Player, graph.TargetOf(EngineAPlayerCell));
        Assert.Equal(Player, graph.TargetOf(EngineBPlayerCell));
        Assert.Equal(EngineB, graph.TargetOf(OtherPlayerEngineCell));
        Assert.Equal([EngineBPlayerCell, EngineAPlayerCell], graph.ReadersNewestFirst(Player));
        Assert.Equal([PlayerEngineCell, OtherPlayerEngineCell], graph.ReadersNewestFirst(EngineB));
    }

    [Fact]
    public void SameEngineStillRepairsReciprocalReader()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineB);
        graph.CreateReaderCell(EngineBPlayerCell, OtherPlayer);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0));

        Assert.Collection(
            result.Calls,
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                PlayerEngineCell,
                EngineB),
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
                EngineBPlayerCell,
                Player,
                Action(RetailActiveReaderActionKind.DetachOldTarget, EngineBPlayerCell, OtherPlayer),
                Action(RetailActiveReaderActionKind.PublishNewTarget, EngineBPlayerCell, Player),
                Action(RetailActiveReaderActionKind.AttachNewTarget, EngineBPlayerCell, Player)));
        Assert.Equal(Player, graph.TargetOf(EngineBPlayerCell));
    }

    [Fact]
    public void AnyNonzeroGodWordRepeatsBothPoliciesAfterBothReaderCalls()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineB);
        graph.CreateReaderCell(EngineBPlayerCell, Player);
        int[] playerReadersBefore = graph.ReadersNewestFirst(Player);
        int[] engineReadersBefore = graph.ReadersNewestFirst(EngineB);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: -7));

        Assert.Collection(
            result.Calls,
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                PlayerEngineCell,
                EngineB),
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
                EngineBPlayerCell,
                Player),
            call => AssertPolicyCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable,
                EngineB,
                rawBooleanArgument: 0),
            call => AssertPolicyCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetInfiniteEnergy,
                EngineB,
                rawBooleanArgument: 1));
        Assert.Equal(playerReadersBefore, graph.ReadersNewestFirst(Player));
        Assert.Equal(engineReadersBefore, graph.ReadersNewestFirst(EngineB));
    }

    [Fact]
    public void ZeroGodWordEmitsNoOppositePolicyReset()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineA);
        graph.CreateReaderCell(EngineBPlayerCell, OtherPlayer);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0));

        Assert.Equal(2, result.Calls.Count);
        Assert.DoesNotContain(
            result.Calls,
            call => call.Kind is
                RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable or
                RetailPlayerBattleEngineAssignmentCallKind.SetInfiniteEnergy);
    }

    [Fact]
    public void MissingReciprocalCellRejectsBeforeFirstMutation()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineA);
        graph.CreateReaderCell(9001, 9002);

        Assert.Throws<ArgumentException>(() =>
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0)));

        Assert.Equal(EngineA, graph.TargetOf(PlayerEngineCell));
        Assert.Equal([PlayerEngineCell], graph.ReadersNewestFirst(EngineA));
        Assert.False(graph.TargetHasReverseContainer(EngineB));
        Assert.Equal(9002, graph.TargetOf(9001));
        Assert.Equal([9001], graph.ReadersNewestFirst(9002));
    }

    [Fact]
    public void MissingPlayerCellRejectsBeforeReciprocalMutation()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(EngineBPlayerCell, OtherPlayer);

        Assert.Throws<ArgumentException>(() =>
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: 0)));

        Assert.Equal(OtherPlayer, graph.TargetOf(EngineBPlayerCell));
        Assert.Equal([EngineBPlayerCell], graph.ReadersNewestFirst(OtherPlayer));
    }

    [Fact]
    public void DuplicateReaderCellRolesRejectBeforeMutation()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell, EngineA);
        RetailPlayerBattleEngineAssignmentRequest request = new(
            Player,
            PlayerEngineCell,
            EngineB,
            PlayerEngineCell,
            PlayerGodWord: -7);

        Assert.Throws<ArgumentException>(() =>
            RetailPlayerBattleEngineAssignment.Assign(graph, request));

        Assert.Equal(EngineA, graph.TargetOf(PlayerEngineCell));
        Assert.Equal([PlayerEngineCell], graph.ReadersNewestFirst(EngineA));
        Assert.False(graph.TargetHasReverseContainer(EngineB));
    }

    [Fact]
    public void TranscriptAndNestedReaderActionsAreDeeplyImmutable()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell);
        graph.CreateReaderCell(EngineBPlayerCell);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(
                graph,
                Request(EngineB, playerGodWord: -7));

        IList<RetailPlayerBattleEngineAssignmentCall> calls =
            Assert.IsAssignableFrom<IList<RetailPlayerBattleEngineAssignmentCall>>(
                result.Calls);
        Assert.Throws<NotSupportedException>(() => calls.Add(result.Calls[0]));
        Assert.Throws<NotSupportedException>(() => calls[0] = result.Calls[1]);

        foreach (RetailPlayerBattleEngineAssignmentCall call in result.Calls)
        {
            IList<RetailActiveReaderAction> actions =
                Assert.IsAssignableFrom<IList<RetailActiveReaderAction>>(
                    call.ReaderActions);
            Assert.Throws<NotSupportedException>(() =>
                actions.Add(Action(
                    RetailActiveReaderActionKind.PublishNewTarget,
                    9991,
                    9992)));
            if (actions.Count > 0)
            {
                Assert.Throws<NotSupportedException>(() =>
                    actions[0] = Action(
                        RetailActiveReaderActionKind.PublishNewTarget,
                        9991,
                        9992));
            }
        }
    }

    [Fact]
    public void TranscriptFactoriesSnapshotCallerOwnedCollections()
    {
        RetailActiveReaderAction originalAction = Action(
            RetailActiveReaderActionKind.PublishNewTarget,
            PlayerEngineCell,
            EngineB);
        RetailActiveReaderAction[] sourceActions = [originalAction];
        RetailPlayerBattleEngineAssignmentCall readerCall =
            RetailPlayerBattleEngineAssignmentCall.ReaderCall(
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                PlayerEngineCell,
                EngineB,
                sourceActions);

        sourceActions[0] = Action(
            RetailActiveReaderActionKind.PublishNewTarget,
            9991,
            9992);

        Assert.Equal([originalAction], readerCall.ReaderActions);

        RetailPlayerBattleEngineAssignmentCall[] sourceCalls = [readerCall];
        var result = new RetailPlayerBattleEngineAssignmentResult(sourceCalls);
        sourceCalls[0] = RetailPlayerBattleEngineAssignmentCall.PolicyCall(
            RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable,
            EngineB,
            rawBooleanArgument: 0);

        Assert.Collection(result.Calls, call => Assert.Same(readerCall, call));
    }

    [Fact]
    public void SecondIdentityTuplePropagatesEveryRequestField()
    {
        const int secondPlayer = 5101;
        const int secondEngine = 6202;
        const int secondPlayerCell = 7303;
        const int secondEngineCell = 8404;
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(secondPlayerCell);
        graph.CreateReaderCell(secondEngineCell);
        RetailPlayerBattleEngineAssignmentRequest request = new(
            secondPlayer,
            secondPlayerCell,
            secondEngine,
            secondEngineCell,
            PlayerGodWord: int.MinValue);

        RetailPlayerBattleEngineAssignmentResult result =
            RetailPlayerBattleEngineAssignment.Assign(graph, request);

        Assert.Collection(
            result.Calls,
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetPlayerBattleEngineReader,
                secondPlayerCell,
                secondEngine,
                Action(RetailActiveReaderActionKind.PublishNewTarget, secondPlayerCell, secondEngine),
                Action(RetailActiveReaderActionKind.AttachNewTarget, secondPlayerCell, secondEngine)),
            call => AssertReaderCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetBattleEnginePlayerReader,
                secondEngineCell,
                secondPlayer,
                Action(RetailActiveReaderActionKind.PublishNewTarget, secondEngineCell, secondPlayer),
                Action(RetailActiveReaderActionKind.AttachNewTarget, secondEngineCell, secondPlayer)),
            call => AssertPolicyCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetVulnerable,
                secondEngine,
                rawBooleanArgument: 0),
            call => AssertPolicyCall(
                call,
                RetailPlayerBattleEngineAssignmentCallKind.SetInfiniteEnergy,
                secondEngine,
                rawBooleanArgument: 1));
        Assert.Equal(secondEngine, graph.TargetOf(secondPlayerCell));
        Assert.Equal(secondPlayer, graph.TargetOf(secondEngineCell));
    }

    [Fact]
    public void ValidAssignmentLeavesUnrelatedGraphStateUntouched()
    {
        var graph = new RetailActiveReaderGraph();
        graph.CreateReaderCell(PlayerEngineCell);
        graph.CreateReaderCell(EngineBPlayerCell);
        graph.CreateReaderCell(9001, 9002);
        graph.CreateReaderCell(9003, 9002);

        RetailPlayerBattleEngineAssignment.Assign(
            graph,
            Request(EngineB, playerGodWord: 0));

        Assert.Equal(9002, graph.TargetOf(9001));
        Assert.Equal(9002, graph.TargetOf(9003));
        Assert.Equal([9003, 9001], graph.ReadersNewestFirst(9002));
    }

    [Fact]
    public void NullGraphIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RetailPlayerBattleEngineAssignment.Assign(
                null!,
                Request(EngineB, playerGodWord: 0)));
    }

    private static RetailPlayerBattleEngineAssignmentRequest Request(
        int battleEngineIdentity,
        int playerGodWord) =>
        new(
            Player,
            PlayerEngineCell,
            battleEngineIdentity,
            EngineBPlayerCell,
            playerGodWord);

    private static void AssertReaderCall(
        RetailPlayerBattleEngineAssignmentCall call,
        RetailPlayerBattleEngineAssignmentCallKind expectedKind,
        int expectedReceiver,
        int expectedTarget,
        params RetailActiveReaderAction[] expectedActions)
    {
        Assert.Equal(expectedKind, call.Kind);
        Assert.Equal(expectedReceiver, call.ReceiverIdentity);
        Assert.Equal(expectedTarget, call.TargetIdentity);
        Assert.Null(call.RawBooleanArgument);
        Assert.Equal(expectedActions, call.ReaderActions);
    }

    private static void AssertPolicyCall(
        RetailPlayerBattleEngineAssignmentCall call,
        RetailPlayerBattleEngineAssignmentCallKind expectedKind,
        int expectedReceiver,
        int rawBooleanArgument)
    {
        Assert.Equal(expectedKind, call.Kind);
        Assert.Equal(expectedReceiver, call.ReceiverIdentity);
        Assert.Null(call.TargetIdentity);
        Assert.Equal(rawBooleanArgument, call.RawBooleanArgument);
        Assert.Empty(call.ReaderActions);
    }

    private static RetailActiveReaderAction Action(
        RetailActiveReaderActionKind kind,
        int? reader,
        int? target) => new(kind, reader, target);
}
