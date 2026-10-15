// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

public sealed class RetailUnitAISquadSupportProbeTests
{
    [Fact]
    public void LiveCursorPreservesOrderMultiplicitySecondResolutionAndTailAppend()
    {
        List<RetailUnitAISupportSquadObservation> squads =
        [
            Snapshot(1, first: 101, second: 201), // current squad
            Snapshot(2, first: null, second: 202),
            Snapshot(
                3,
                first: 103,
                second: 203,
                allegiance: RetailUnitAITargetSelection.AllegianceForseti),
            Snapshot(4, first: 104, second: 204, linkedSupportEligible: false),
            Snapshot(5, first: 105, second: 205, x: 10.0f), // strict equality
            Snapshot(6, first: 106, second: 206, x: 9.0f),
            Snapshot(6, first: 107, second: null, x: 8.0f),
            Snapshot(7, first: 108, second: 208, x: 9.95f, percent: 1.0f),
            Snapshot(8, first: 109, second: 209, x: 9.95f),
        ];
        var context = new RetailUnitAISupportScanContext(
            ScanEnabled: true,
            CurrentSquadIdentity: 1,
            OwnerAllegiance138: RetailUnitAITargetSelection.AllegianceForseti,
            OwnerIndiscriminate128: 0,
            OwnerPosition: new RetailUnitAITargetPosition(0.0f, 0.0f, 0.0f),
            OwnerMaximumDistance158: 10.0f);
        var calls = new List<RetailUnitAISupportHelperCall>();

        for (int index = 0; index < squads.Count; index++)
        {
            if (!RetailUnitAISquadSupportProbe.TryBuildHelperCall(
                    context,
                    squads[index],
                    out RetailUnitAISupportHelperCall call))
            {
                continue;
            }

            calls.Add(call);
            if (call.TargetUnitIdentity == 206)
            {
                // Retail's helper may spawn/init a squad before the cursor
                // reads node->next, making this tail node part of the scan.
                squads.Add(Snapshot(9, first: 110, second: 210, x: 7.0f));
            }
        }

        Assert.Equal(
            [
                new RetailUnitAISupportHelperCall(6, 206),
                new RetailUnitAISupportHelperCall(6, null),
                new RetailUnitAISupportHelperCall(8, 209),
                new RetailUnitAISupportHelperCall(9, 210),
            ],
            calls);
    }

    [Fact]
    public void DisabledScanDoesNotReadFiniteDomainInputs()
    {
        var context = new RetailUnitAISupportScanContext(
            ScanEnabled: false,
            CurrentSquadIdentity: null,
            OwnerAllegiance138: RetailUnitAITargetSelection.AllegianceForseti,
            OwnerIndiscriminate128: 0,
            OwnerPosition: new RetailUnitAITargetPosition(float.NaN, 0.0f, 0.0f),
            OwnerMaximumDistance158: float.NaN);

        Assert.False(RetailUnitAISquadSupportProbe.TryBuildHelperCall(
            context,
            Snapshot(1, first: 101, second: 201),
            out RetailUnitAISupportHelperCall call));
        Assert.Equal(default, call);
    }

    private static RetailUnitAISupportSquadObservation Snapshot(
        int squadIdentity,
        int? first,
        int? second,
        int allegiance = RetailUnitAITargetSelection.AllegianceMuspell,
        bool linkedSupportEligible = true,
        float x = 1.0f,
        float percent = 0.0f) => new(
            SquadIdentity: squadIdentity,
            FirstRepresentativeUnitIdentity: first,
            FirstRepresentativeAllegiance138: allegiance,
            LinkedSupportEligible: linkedSupportEligible,
            SquadPosition: new RetailUnitAITargetPosition(x, 0.0f, 0.0f),
            RangeReductionPercent: percent,
            SecondRepresentativeUnitIdentity: second);
}
