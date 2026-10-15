// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Cross-build branch-law checks for PC retail <c>0x004FF710</c>, PC demo
/// <c>0x004FF7C0</c>, Xbox <c>0x001878A0/0x00187910</c>, and PS2
/// <c>0x002BF818/0x002BF8D8/0x002C0040</c>.
/// </summary>
public sealed class RetailUnitAITargetSelectionTests
{
    [Fact]
    public void DeterministicTranscriptPreservesRetailRangeLadderFloorAndTieLaw()
    {
        var profile = new RetailUnitAITargetProfile(
            Indiscriminate128: 0,
            IgnoreThreats138: 0,
            MaximumDistance158: 2000.0f,
            EmplacementPriority164: 40.0f,
            VehiclePriority168: 10.0f,
            BuildingPriority16C: 50.0f,
            NavalPriority170: 60.0f,
            InfantryPriority174: 20.0f,
            AirUnitPriority178: 30.0f,
            ComponentPriority17C: 20.0f);

        RetailUnitAITargetCandidate[] candidates =
        [
            Candidate(
                RetailUnitAITargetSelection.ThingTypeVehicle |
                RetailUnitAITargetSelection.ThingTypeInfantry,
                distance: 1.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 2.0f),
            // Primary rises from 10 to 20, but secondary is exactly zero.
            // Retail keeps A's pointer rather than clearing it here.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeInfantry,
                distance: 1000.0f,
                selectedProviderMinimumRange: 1500.0f,
                selectedProviderMaximumRange: 1600.0f),
            // The post-ladder floor raises 10 to 20; above-band adds 10,000.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeVehicle |
                RetailUnitAITargetSelection.ThingTypeComponent,
                distance: 1500.0f,
                selectedProviderMinimumRange: 1000.0f,
                selectedProviderMaximumRange: 1200.0f),
            Candidate(
                RetailUnitAITargetSelection.ThingTypeInfantry,
                distance: 1999.0f,
                selectedProviderMinimumRange: 1990.0f,
                selectedProviderMaximumRange: 2000.0f),
            // Exact primary/secondary equality retains the earlier D.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeInfantry,
                distance: 1999.0f,
                selectedProviderMinimumRange: 1990.0f,
                selectedProviderMaximumRange: 2000.0f),
            // Strict squared-range equality rejects this otherwise stronger row.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeAirUnit,
                distance: 2000.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 3000.0f),
            // Eligibility short-circuits this otherwise stronger row.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeAirUnit,
                distance: 1.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 2.0f,
                allegiance138: RetailUnitAITargetSelection.AllegianceForseti),
            // A non-threat with IgnoreThreats disabled receives primary zero;
            // the floor is skipped even though its bit is set.
            Candidate(
                RetailUnitAITargetSelection.ThingTypeAirUnit |
                RetailUnitAITargetSelection.ThingTypeComponent,
                distance: 1.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 2.0f,
                isAThreat: false),
        ];

        Assert.Null(Select(profile, candidates[1..2], 1));
        Assert.Equal(0, Select(profile, candidates, 1));
        Assert.Equal(0, Select(profile, candidates, 2));
        Assert.Equal(2, Select(profile, candidates, 3));
        Assert.Equal(3, Select(profile, candidates, 4));
        Assert.Equal(3, Select(profile, candidates, 5));
        Assert.Equal(3, Select(profile, candidates, candidates.Length));
    }

    [Fact]
    public void IndiscriminateArmConsumesOnlyAdmittedCandidatesAndBypassesComponentFloor()
    {
        RetailUnitAITargetProfile profile = DefaultProfile() with
        {
            Indiscriminate128 = 1,
            ComponentPriority17C = 7.0f,
        };
        RetailUnitAITargetCandidate[] candidates =
        [
            Candidate(
                0,
                distance: 1.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 2.0f,
                thingFlags2C: RetailUnitAITargetSelection.ThingFlagDying),
            Candidate(
                0,
                distance: 1.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 2.0f),
            Candidate(
                0,
                distance: 2.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 3.0f),
            Candidate(
                0,
                distance: 2000.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 3000.0f),
            Candidate(
                RetailUnitAITargetSelection.ThingTypeComponent,
                distance: 3.0f,
                selectedProviderMinimumRange: 0.0f,
                selectedProviderMaximumRange: 4.0f,
                isAThreat: false),
        ];
        var random = new Level100ReleasedRandom();

        Assert.Equal(
            2,
            RetailUnitAITargetSelection.Select(
                profile,
                RetailUnitAITargetSelection.AllegianceForseti,
                candidates,
                random));
        Assert.Equal(-1_136_790_067, random.Seed);
    }

    [Fact]
    public void CandidateTranscriptPreservesSideViewOrderAndRawSquadDistance()
    {
        RetailUnitAITargetResolvedUnit squadRepresentative = ResolvedUnit(
            RetailUnitAITargetSelection.ThingTypeAirUnit,
            RetailUnitAITargetSelection.AllegianceMuspell);
        RetailUnitAITargetResolvedUnit directUnit = ResolvedUnit(
            RetailUnitAITargetSelection.ThingTypeVehicle,
            RetailUnitAITargetSelection.AllegianceMuspell);
        RetailUnitAITargetResolvedUnit wrongDirectArm = ResolvedUnit(
            RetailUnitAITargetSelection.ThingTypeBuilding,
            RetailUnitAITargetSelection.AllegianceMuspell);

        RetailUnitAITargetRawPayload[] allThings =
        [
            Payload(
                RetailUnitAITargetSelection.ThingTypeUnit,
                1.0f,
                0.0f,
                0.0f,
                directUnit: wrongDirectArm),
        ];
        RetailUnitAITargetRawPayload[] allegiance0Or6 =
        [
            Payload(
                RetailUnitAITargetSelection.ThingTypeUnit,
                2.0f,
                0.0f,
                0.0f,
                directUnit: wrongDirectArm),
        ];
        RetailUnitAITargetRawPayload[] allegiance1Or6 =
        [
            // Even a supplied unit is ignored when neither class bit is set.
            Payload(0, 0.0f, 0.0f, 0.0f, directUnit: wrongDirectArm),
            // Squad dispatch has priority when both discriminator bits are set.
            // Its representative supplies Unit fields, but the raw squad at
            // (3,4,0) supplies the measured range vector.
            Payload(
                RetailUnitAITargetSelection.ThingTypeSquadClass |
                RetailUnitAITargetSelection.ThingTypeUnit,
                3.0f,
                4.0f,
                0.0f,
                directUnit: wrongDirectArm,
                squadRepresentativeUnit: squadRepresentative),
            Payload(
                RetailUnitAITargetSelection.ThingTypeUnit,
                6.0f,
                8.0f,
                0.0f,
                directUnit: directUnit),
        ];

        RetailUnitAITargetCandidate[] transcript =
            RetailUnitAITargetSelection.BuildOrderedCandidateTranscript(
                RetailUnitAITargetSelection.AllegianceForseti,
                new RetailUnitAITargetPosition(0.0f, 0.0f, 0.0f),
                allThings,
                allegiance0Or6,
                allegiance1Or6);

        Assert.Equal(2, transcript.Length);
        Assert.Equal(
            RetailUnitAITargetSelection.ThingTypeAirUnit,
            transcript[0].TypeFlags34);
        Assert.Equal(25.0f, transcript[0].DistanceSquared);
        Assert.Equal(
            RetailUnitAITargetSelection.ThingTypeVehicle,
            transcript[1].TypeFlags34);
        Assert.Equal(100.0f, transcript[1].DistanceSquared);
    }

    [Fact]
    public void NonThreatIsZeroScoredNotRejectedAndIgnoreThreatsRestoresLadder()
    {
        RetailUnitAITargetCandidate nonThreat = Candidate(
            RetailUnitAITargetSelection.ThingTypeAirUnit |
            RetailUnitAITargetSelection.ThingTypeComponent,
            distance: 1.0f,
            selectedProviderMinimumRange: 0.0f,
            selectedProviderMaximumRange: 2.0f,
            isAThreat: false);
        RetailUnitAITargetCandidate vehicle = Candidate(
            RetailUnitAITargetSelection.ThingTypeVehicle,
            distance: 2.0f,
            selectedProviderMinimumRange: 0.0f,
            selectedProviderMaximumRange: 3.0f);

        RetailUnitAITargetProfile profile = DefaultProfile();
        Assert.Equal(0, Select(profile, [nonThreat], 1));
        Assert.Equal(1, Select(profile, [nonThreat, vehicle], 2));

        profile = profile with { IgnoreThreats138 = 1 };
        Assert.Equal(0, Select(profile, [nonThreat, vehicle], 2));
    }

    [Theory]
    [InlineData(RetailUnitAITargetSelection.AllegianceForseti, 9.0f)]
    [InlineData(RetailUnitAITargetSelection.AllegianceMuspell, 4.0f)]
    [InlineData(RetailUnitAITargetSelection.AllegianceNeutral, 1.0f)]
    [InlineData(RetailUnitAITargetSelection.AllegianceIndependent, 1.0f)]
    public void CandidateTranscriptRoutesTheThreeReleasedWorldViews(
        int ownerAllegiance138,
        float expectedDistanceSquared)
    {
        RetailUnitAITargetResolvedUnit unit = ResolvedUnit(
            RetailUnitAITargetSelection.ThingTypeVehicle,
            RetailUnitAITargetSelection.AllegianceMuspell);

        RetailUnitAITargetCandidate[] transcript =
            RetailUnitAITargetSelection.BuildOrderedCandidateTranscript(
                ownerAllegiance138,
                new RetailUnitAITargetPosition(0.0f, 0.0f, 0.0f),
                [Payload(RetailUnitAITargetSelection.ThingTypeUnit, 1.0f, 0.0f, 0.0f, unit)],
                [Payload(RetailUnitAITargetSelection.ThingTypeUnit, 2.0f, 0.0f, 0.0f, unit)],
                [Payload(RetailUnitAITargetSelection.ThingTypeUnit, 3.0f, 0.0f, 0.0f, unit)]);

        Assert.Single(transcript);
        Assert.Equal(expectedDistanceSquared, transcript[0].DistanceSquared);
    }

    [Theory]
    [InlineData(0u, 0, true)]
    [InlineData(0u, 3, true)]
    [InlineData(RetailUnitAITargetSelection.ThingFlagDying, 0, false)]
    [InlineData(RetailUnitAITargetSelection.ThingFlagDying, 3, false)]
    [InlineData(0u, 1, false)]
    [InlineData(0u, 2, false)]
    public void ResolvedCandidateStateGateMatchesReleasedPredicate(
        uint thingFlags2C,
        int unitMode244,
        bool expected) => Assert.Equal(
            expected,
            RetailUnitAITargetSelection.PassesResolvedCandidateStateGate(
                thingFlags2C,
                unitMode244));

    [Theory]
    [InlineData(0, 1, 0, true)]
    [InlineData(0, 6, 0, true)]
    [InlineData(1, 0, 0, true)]
    [InlineData(1, 6, 0, true)]
    [InlineData(6, 0, 0, true)]
    [InlineData(6, 1, 0, true)]
    [InlineData(0, 0, 0, false)]
    [InlineData(1, 1, 1, false)]
    [InlineData(6, 6, 1, false)]
    [InlineData(0, 2, 0, false)]
    [InlineData(99, 2, 1, true)]
    [InlineData(0, 3, 1, false)]
    public void TargetAllegianceGateMatchesReleasedTruthTable(
        int ownerAllegiance138,
        int candidateAllegiance138,
        int indiscriminate128,
        bool expected) => Assert.Equal(
            expected,
            RetailUnitAITargetSelection.PassesTargetAllegianceGate(
                ownerAllegiance138,
                candidateAllegiance138,
                indiscriminate128));

    private static int? Select(
        RetailUnitAITargetProfile profile,
        RetailUnitAITargetCandidate[] candidates,
        int count) => RetailUnitAITargetSelection.Select(
            profile,
            RetailUnitAITargetSelection.AllegianceForseti,
            candidates[..count]);

    private static RetailUnitAITargetProfile DefaultProfile() => new(
        Indiscriminate128: 0,
        IgnoreThreats138: 0,
        MaximumDistance158: 2000.0f,
        EmplacementPriority164: 40.0f,
        VehiclePriority168: 10.0f,
        BuildingPriority16C: 50.0f,
        NavalPriority170: 60.0f,
        InfantryPriority174: 20.0f,
        AirUnitPriority178: 30.0f,
        ComponentPriority17C: 20.0f);

    private static RetailUnitAITargetCandidate Candidate(
        uint flags,
        float distance,
        float selectedProviderMinimumRange,
        float selectedProviderMaximumRange,
        uint thingFlags2C = 0,
        int unitMode244 = 0,
        int allegiance138 = RetailUnitAITargetSelection.AllegianceMuspell,
        int candidateCapabilityGate = 1,
        bool isAThreat = true) => new(
            ThingFlags2C: thingFlags2C,
            TypeFlags34: flags,
            UnitMode244: unitMode244,
            Allegiance138: allegiance138,
            DistanceSquared: distance * distance,
            RangeReductionPercent: 0.0f,
            CandidateCapabilityGate: candidateCapabilityGate,
            IsAThreat: isAThreat,
            SelectedProviderMinimumRange: selectedProviderMinimumRange,
            SelectedProviderMaximumRange: selectedProviderMaximumRange);

    private static RetailUnitAITargetResolvedUnit ResolvedUnit(
        uint typeFlags34,
        int allegiance138) => new(
            ThingFlags2C: 0,
            TypeFlags34: typeFlags34,
            UnitMode244: 0,
            Allegiance138: allegiance138,
            RangeReductionPercent: 0.0f,
            CandidateCapabilityGate: 1,
            IsAThreat: true,
            SelectedProviderMinimumRange: 0.0f,
            SelectedProviderMaximumRange: 2000.0f);

    private static RetailUnitAITargetRawPayload Payload(
        uint thingTypeFlags34,
        float x,
        float y,
        float z,
        RetailUnitAITargetResolvedUnit? directUnit = null,
        RetailUnitAITargetResolvedUnit? squadRepresentativeUnit = null) => new(
            ThingTypeFlags34: thingTypeFlags34,
            Position: new RetailUnitAITargetPosition(x, y, z),
            DirectUnit: directUnit,
            SquadRepresentativeUnit: squadRepresentativeUnit);
}
