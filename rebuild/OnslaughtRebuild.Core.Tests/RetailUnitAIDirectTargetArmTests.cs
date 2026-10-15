// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitAIDirectTargetArmTests
{
    [Fact]
    public void DyingReaderClearsBeforeNullMembershipFallsThroughWithoutWrites()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                entryReader: 7,
                entryDying: true,
                membership: null));

        Assert.Equal(
            RetailUnitAIDirectTargetArmDisposition.FallsThroughToFallback,
            plan.Disposition);
        Assert.Equal(
            [RetailUnitAIDirectTargetArmAction.SetReaderNull(7)],
            plan.Actions);
    }

    [Fact]
    public void NonzeroMembershipVirtualFallsThroughWithResultsPreserved()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                membership: 40,
                membershipResult: 2));

        Assert.Equal(
            RetailUnitAIDirectTargetArmDisposition.FallsThroughToFallback,
            plan.Disposition);
        Assert.Equal(
            [RetailUnitAIDirectTargetArmAction.MembershipVirtual83(40, 2)],
            plan.Actions);
    }

    [Fact]
    public void SelectedDirectArmPreclearsAThenBAndStopsOnNullReader()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                membership: 40,
                membershipResult: 0,
                stateReader: null));

        Assert.Equal(
            RetailUnitAIDirectTargetArmDisposition.HandledDirectArm,
            plan.Disposition);
        Assert.Equal(
            [
                RetailUnitAIDirectTargetArmAction.MembershipVirtual83(40, 0),
                RetailUnitAIDirectTargetArmAction.WriteResult18(0),
                RetailUnitAIDirectTargetArmAction.WriteResult1C(0),
            ],
            plan.Actions);
    }

    [Fact]
    public void FailedStateGateClearsReaderAndDoesNotRunSupportOrHelpers()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                membership: 40,
                stateReader: 8,
                statePasses: false));

        Assert.Equal(
            [
                RetailUnitAIDirectTargetArmAction.MembershipVirtual83(40, 0),
                RetailUnitAIDirectTargetArmAction.WriteResult18(0),
                RetailUnitAIDirectTargetArmAction.WriteResult1C(0),
                RetailUnitAIDirectTargetArmAction.ActiveStateGate(8, false),
                RetailUnitAIDirectTargetArmAction.SetReaderNull(8),
            ],
            plan.Actions);
    }

    [Fact]
    public void ZeroHelperBStopsAfterItsRawResultWrite()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                membership: 40,
                stateReader: 8,
                supportReader: 9,
                helperBReader: 10,
                helperB: 0,
                helperAReader: 11,
                helperA: 91));

        Assert.Equal(
            [
                RetailUnitAIDirectTargetArmAction.MembershipVirtual83(40, 0),
                RetailUnitAIDirectTargetArmAction.WriteResult18(0),
                RetailUnitAIDirectTargetArmAction.WriteResult1C(0),
                RetailUnitAIDirectTargetArmAction.ActiveStateGate(8, true),
                RetailUnitAIDirectTargetArmAction.SupportUpdate(9),
                RetailUnitAIDirectTargetArmAction.HelperB(10, 0),
            ],
            plan.Actions);
    }

    [Fact]
    public void EachConsequentialStageConsumesItsFreshReaderIdentity()
    {
        RetailUnitAIDirectTargetArmPlan plan =
            RetailUnitAIDirectTargetArm.Plan(Request(
                membership: 40,
                stateReader: 8,
                supportReader: 9,
                helperBReader: 10,
                helperB: -2,
                helperAReader: 11,
                helperA: 3));

        Assert.Equal(
            [
                RetailUnitAIDirectTargetArmAction.MembershipVirtual83(40, 0),
                RetailUnitAIDirectTargetArmAction.WriteResult18(0),
                RetailUnitAIDirectTargetArmAction.WriteResult1C(0),
                RetailUnitAIDirectTargetArmAction.ActiveStateGate(8, true),
                RetailUnitAIDirectTargetArmAction.SupportUpdate(9),
                RetailUnitAIDirectTargetArmAction.HelperB(10, -2),
                RetailUnitAIDirectTargetArmAction.HelperA(11, 3),
            ],
            plan.Actions);
    }

    private static RetailUnitAIDirectTargetArmRequest Request(
        int? entryReader = null,
        bool entryDying = false,
        int? membership = 1,
        int membershipResult = 0,
        int? stateReader = 2,
        bool statePasses = true,
        int? supportReader = 2,
        int? helperBReader = 2,
        int helperB = 1,
        int? helperAReader = 2,
        int helperA = 1) => new(
            EntryReaderIdentity: entryReader,
            EntryReaderIsDying: entryDying,
            MembershipIdentity: membership,
            MembershipVirtual83Result: membershipResult,
            ReaderAtStateGateIdentity: stateReader,
            ReaderPassesActiveStateGate: statePasses,
            ReaderAtSupportIdentity: supportReader,
            ReaderAtHelperBIdentity: helperBReader,
            HelperResultB: helperB,
            ReaderAtHelperAIdentity: helperAReader,
            HelperResultA: helperA);
}
