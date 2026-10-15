// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitAITargetTransactionTests
{
    [Fact]
    public void Slot4RetainedGateRefreshesInPlaceAndIgnoresSlot11Inputs()
    {
        RetailUnitAITargetTransactionPlan plan =
            RetailUnitAITargetTransaction.PlanFallback(Request(
                currentTarget: 7,
                retainedGate10: 1,
                fastReuseEligible14: 0,
                stealth: 91.0f,
                currentActive: false,
                winner: 9,
                winnerActive: false,
                helperB: -2,
                helperA: 3));

        Assert.Equal(
            RetailUnitAITargetTransactionRoute.Slot4RetainedRefresh,
            plan.Path);
        Assert.Equal(7, plan.ReturnedTargetIdentity);
        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.SupportUpdate(7),
                RetailUnitAITargetTransactionAction.HelperB(7, -2),
                RetailUnitAITargetTransactionAction.HelperA(7, 3),
            ],
            plan.Actions);
    }

    [Fact]
    public void RetainedRefreshStoresZeroBAndExplicitlyZerosResult18()
    {
        RetailUnitAITargetTransactionPlan plan =
            RetailUnitAITargetTransaction.PlanFallback(Request(
                currentTarget: 7,
                retainedGate10: -1,
                helperB: 0,
                helperA: 99));

        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.SupportUpdate(7),
                RetailUnitAITargetTransactionAction.HelperB(7, 0),
                RetailUnitAITargetTransactionAction.WriteResult18(0),
            ],
            plan.Actions);
    }

    [Fact]
    public void PcNaNPassesFastReuseWhileConsoleRunsFullSelection()
    {
        RetailUnitAITargetTransactionRequest request = Request(
            currentTarget: 7,
            retainedGate10: 0,
            fastReuseEligible14: 1,
            stealth: float.NaN,
            currentActive: true,
            winner: 9,
            winnerActive: true,
            helperB: 2,
            helperA: 3);

        RetailUnitAITargetTransactionPlan pc =
            RetailUnitAITargetTransaction.PlanFallback(request);
        RetailUnitAITargetTransactionPlan console =
            RetailUnitAITargetTransaction.PlanFallback(request with
            {
                FloatPolicy = RetailUnitAIFastReuseFloatPolicy.ConsoleOrderedEquality,
            });

        Assert.Equal(RetailUnitAITargetTransactionRoute.Slot11FastReuse, pc.Path);
        Assert.Equal(7, pc.ReturnedTargetIdentity);
        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.SupportUpdate(7),
                RetailUnitAITargetTransactionAction.HelperB(7, 2),
                RetailUnitAITargetTransactionAction.HelperA(7, 3),
            ],
            pc.Actions);

        Assert.Equal(
            RetailUnitAITargetTransactionRoute.Slot11FullSelection,
            console.Path);
        Assert.Equal(9, console.ReturnedTargetIdentity);
    }

    [Fact]
    public void FullWinnerPreservesDoubleSupportAndDoesNotRollBackOnActiveFailure()
    {
        RetailUnitAITargetTransactionPlan plan =
            RetailUnitAITargetTransaction.PlanFallback(Request(
                currentTarget: 7,
                retainedGate10: 0,
                fastReuseEligible14: 0,
                winner: 9,
                winnerActive: false,
                helperB: 44,
                helperA: 55));

        Assert.Equal(
            RetailUnitAITargetTransactionRoute.Slot11FullSelection,
            plan.Path);
        Assert.Equal(9, plan.ReturnedTargetIdentity);
        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.WriteResult18(0),
                RetailUnitAITargetTransactionAction.WriteResult1C(0),
                RetailUnitAITargetTransactionAction.SetReader(9),
                RetailUnitAITargetTransactionAction.SupportUpdate(9),
                RetailUnitAITargetTransactionAction.WriteGate10(0),
                RetailUnitAITargetTransactionAction.SupportUpdate(9),
                RetailUnitAITargetTransactionAction.WriteGate10(0),
            ],
            plan.Actions);
    }

    [Fact]
    public void FullWinnerStoresBThenAAndRetainsDuplicateGateClear()
    {
        RetailUnitAITargetTransactionPlan plan =
            RetailUnitAITargetTransaction.PlanFallback(Request(
                currentTarget: 7,
                retainedGate10: 0,
                fastReuseEligible14: 0,
                winner: 9,
                winnerActive: true,
                helperB: -4,
                helperA: 12));

        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.WriteResult18(0),
                RetailUnitAITargetTransactionAction.WriteResult1C(0),
                RetailUnitAITargetTransactionAction.SetReader(9),
                RetailUnitAITargetTransactionAction.SupportUpdate(9),
                RetailUnitAITargetTransactionAction.WriteGate10(0),
                RetailUnitAITargetTransactionAction.SupportUpdate(9),
                RetailUnitAITargetTransactionAction.HelperB(9, -4),
                RetailUnitAITargetTransactionAction.HelperA(9, 12),
                RetailUnitAITargetTransactionAction.WriteGate10(0),
            ],
            plan.Actions);
    }

    [Fact]
    public void FullNoWinnerStillCallsSetReaderNullThenClearsRuntimeGate()
    {
        RetailUnitAITargetTransactionPlan plan =
            RetailUnitAITargetTransaction.PlanFallback(Request(
                currentTarget: 7,
                retainedGate10: 0,
                fastReuseEligible14: 0,
                winner: null));

        Assert.Null(plan.ReturnedTargetIdentity);
        Assert.Equal(
            [
                RetailUnitAITargetTransactionAction.WriteResult18(0),
                RetailUnitAITargetTransactionAction.WriteResult1C(0),
                RetailUnitAITargetTransactionAction.SetReader(null),
                RetailUnitAITargetTransactionAction.WriteGate10(0),
            ],
            plan.Actions);
    }

    [Theory]
    [InlineData(0.0f, true, true)]
    [InlineData(1.0f, false, false)]
    [InlineData(float.PositiveInfinity, false, false)]
    public void FastReuseStealthPredicateMatchesFiniteAndInfinityCases(
        float stealth,
        bool expectedPc,
        bool expectedConsole)
    {
        Assert.Equal(
            expectedPc,
            RetailUnitAITargetTransaction.PassesFastReuseStealthGate(
                stealth,
                RetailUnitAIFastReuseFloatPolicy.PcC3Only));
        Assert.Equal(
            expectedConsole,
            RetailUnitAITargetTransaction.PassesFastReuseStealthGate(
                stealth,
                RetailUnitAIFastReuseFloatPolicy.ConsoleOrderedEquality));
    }

    [Fact]
    public void NegativeZeroPassesBothReleasedFloatPolicies()
    {
        Assert.True(RetailUnitAITargetTransaction.PassesFastReuseStealthGate(
            -0.0f,
            RetailUnitAIFastReuseFloatPolicy.PcC3Only));
        Assert.True(RetailUnitAITargetTransaction.PassesFastReuseStealthGate(
            -0.0f,
            RetailUnitAIFastReuseFloatPolicy.ConsoleOrderedEquality));
    }

    private static RetailUnitAITargetTransactionRequest Request(
        int? currentTarget = null,
        int retainedGate10 = 0,
        int fastReuseEligible14 = 1,
        float stealth = 0.0f,
        bool currentActive = true,
        int? winner = null,
        bool winnerActive = true,
        int helperB = 1,
        int helperA = 1) => new(
            CurrentTargetIdentity: currentTarget,
            RetainedTargetGate10: retainedGate10,
            FastReuseEligible14: fastReuseEligible14,
            CurrentTargetStealth: stealth,
            CurrentTargetPassesActiveStateGate: currentActive,
            SelectorWinnerIdentity: winner,
            SelectorWinnerPassesActiveStateGate: winnerActive,
            HelperResultB: helperB,
            HelperResultA: helperA,
            FloatPolicy: RetailUnitAIFastReuseFloatPolicy.PcC3Only);
}
