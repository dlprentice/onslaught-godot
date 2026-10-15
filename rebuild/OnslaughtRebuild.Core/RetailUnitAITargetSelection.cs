// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Released <c>CUnitAI</c> close-target profile fields. The seven priority
/// cells are the exact output array of <c>CUnitAttackPriority</c> indices
/// 0..6; <c>CUnitIndiscriminate</c> owns <c>config+0x128</c>, and
/// <c>CUnitIgnoreThreats</c> owns <c>config+0x138</c>.
/// </summary>
/// <remarks>
/// Offsets remain in the names so the semantic labels cannot drift away from
/// the measured retail layout.
/// </remarks>
public readonly record struct RetailUnitAITargetProfile(
    int Indiscriminate128,
    int IgnoreThreats138,
    float MaximumDistance158,
    float EmplacementPriority164,
    float VehiclePriority168,
    float BuildingPriority16C,
    float NavalPriority170,
    float InfantryPriority174,
    float AirUnitPriority178,
    float ComponentPriority17C);

/// <summary>
/// One ordered, resolved candidate transcript for the scoring kernel.
/// Candidate pointer resolution, side-list selection, and the capability
/// helper remain outside this type. The state and allegiance gates are reduced
/// here from their exact raw retail fields.
/// </summary>
public readonly record struct RetailUnitAITargetCandidate(
    uint ThingFlags2C,
    uint TypeFlags34,
    int UnitMode244,
    int Allegiance138,
    float DistanceSquared,
    float RangeReductionPercent,
    int CandidateCapabilityGate,
    bool IsAThreat,
    float SelectedProviderMinimumRange,
    float SelectedProviderMaximumRange);

/// <summary>
/// One caller-captured position in the released world coordinate space.
/// </summary>
public readonly record struct RetailUnitAITargetPosition(
    float X,
    float Y,
    float Z);

/// <summary>
/// Fields read from the resolved <c>CUnit</c> after retail transforms a raw
/// world-list payload. Provider ranges are the results of selecting an attack
/// provider for this unit and querying that selected provider.
/// </summary>
public readonly record struct RetailUnitAITargetResolvedUnit(
    uint ThingFlags2C,
    uint TypeFlags34,
    int UnitMode244,
    int Allegiance138,
    float RangeReductionPercent,
    int CandidateCapabilityGate,
    bool IsAThreat,
    float SelectedProviderMinimumRange,
    float SelectedProviderMaximumRange);

/// <summary>
/// One raw payload from an already ordered retail world-list view. Squad and
/// direct-unit identities stay separate because retail resolves fields from
/// one object while retaining the raw payload position for distance.
/// </summary>
public readonly record struct RetailUnitAITargetRawPayload(
    uint ThingTypeFlags34,
    RetailUnitAITargetPosition Position,
    RetailUnitAITargetResolvedUnit? DirectUnit,
    RetailUnitAITargetResolvedUnit? SquadRepresentativeUnit);

/// <summary>
/// The finite-domain scoring kernel in retail PC
/// <c>0x004FF710..0x004FFB57</c>, independently corresponding to the PC demo,
/// two Xbox builds, and three PS2 builds.
/// </summary>
/// <remarks>
/// This is deliberately a transcript reducer, not autonomous UnitAI. It does
/// not populate the three retail world-list views, mutate the lifecycle-aware
/// target reader, select attack providers, populate ballistic-result cells, or
/// replace script-authored <c>Attack(target)</c>. Its indiscriminate arm
/// consumes the caller-supplied released gameplay stream; the caller remains
/// responsible for that shared stream's global draw order.
/// </remarks>
public static class RetailUnitAITargetSelection
{
    public const uint ThingFlagDying = 0x00000004u;

    public const uint ThingTypeUnit = 0x00000010u;
    public const uint ThingTypeSquadClass = 0x20000000u;

    public const int AllegianceForseti = 0;
    public const int AllegianceMuspell = 1;
    public const int AllegianceNeutral = 2;
    public const int AllegianceIndependent = 6;

    public const uint ThingTypeVehicle = 0x00020000u;
    public const uint ThingTypeInfantry = 0x00004000u;
    public const uint ThingTypeAirUnit = 0x00000400u;
    public const uint ThingTypeEmplacement = 0x00040000u;
    public const uint ThingTypeBuilding = 0x00000100u;
    public const uint ThingTypeNaval = 0x00008000u;
    public const uint ThingTypeComponent = 0x00080000u;

    private const float IndiscriminateScoreScale = 1.0f / 8192.0f;

    /// <summary>
    /// Selects retail's owner-side world-list view and transforms its raw
    /// payloads into the scorer transcript without changing traversal order.
    /// The supplied views must already preserve their distinct container
    /// order; the two side views cannot be derived by filtering all things.
    /// </summary>
    public static RetailUnitAITargetCandidate[] BuildOrderedCandidateTranscript(
        int ownerAllegiance138,
        RetailUnitAITargetPosition ownerPosition,
        IReadOnlyList<RetailUnitAITargetRawPayload> allThingsTraversalOrder,
        IReadOnlyList<RetailUnitAITargetRawPayload> allegiance0Or6TraversalOrder,
        IReadOnlyList<RetailUnitAITargetRawPayload> allegiance1Or6TraversalOrder)
    {
        ArgumentNullException.ThrowIfNull(allThingsTraversalOrder);
        ArgumentNullException.ThrowIfNull(allegiance0Or6TraversalOrder);
        ArgumentNullException.ThrowIfNull(allegiance1Or6TraversalOrder);

        RequireFinite(ownerPosition.X, nameof(ownerPosition));
        RequireFinite(ownerPosition.Y, nameof(ownerPosition));
        RequireFinite(ownerPosition.Z, nameof(ownerPosition));

        IReadOnlyList<RetailUnitAITargetRawPayload> selectedView =
            ownerAllegiance138 switch
            {
                AllegianceMuspell => allegiance0Or6TraversalOrder,
                AllegianceForseti => allegiance1Or6TraversalOrder,
                _ => allThingsTraversalOrder,
            };

        var transcript = new List<RetailUnitAITargetCandidate>(selectedView.Count);
        foreach (RetailUnitAITargetRawPayload payload in selectedView)
        {
            RetailUnitAITargetResolvedUnit? resolved =
                (payload.ThingTypeFlags34 & ThingTypeSquadClass) != 0
                    ? payload.SquadRepresentativeUnit
                    : (payload.ThingTypeFlags34 & ThingTypeUnit) != 0
                        ? payload.DirectUnit
                        : null;
            if (resolved is not { } unit)
            {
                continue;
            }

            RequireFinite(payload.Position.X, nameof(selectedView));
            RequireFinite(payload.Position.Y, nameof(selectedView));
            RequireFinite(payload.Position.Z, nameof(selectedView));

            float deltaX = payload.Position.X - ownerPosition.X;
            float deltaY = payload.Position.Y - ownerPosition.Y;
            float deltaZ = payload.Position.Z - ownerPosition.Z;
            float distanceSquared =
                (deltaX * deltaX) +
                (deltaY * deltaY) +
                (deltaZ * deltaZ);
            RequireFinite(distanceSquared, nameof(selectedView));

            transcript.Add(new RetailUnitAITargetCandidate(
                ThingFlags2C: unit.ThingFlags2C,
                TypeFlags34: unit.TypeFlags34,
                UnitMode244: unit.UnitMode244,
                Allegiance138: unit.Allegiance138,
                DistanceSquared: distanceSquared,
                RangeReductionPercent: unit.RangeReductionPercent,
                CandidateCapabilityGate: unit.CandidateCapabilityGate,
                IsAThreat: unit.IsAThreat,
                SelectedProviderMinimumRange: unit.SelectedProviderMinimumRange,
                SelectedProviderMaximumRange: unit.SelectedProviderMaximumRange));
        }

        return transcript.ToArray();
    }

    /// <summary>
    /// Reduces candidates in their supplied list order. Returns
    /// <c>null</c> when no candidate raises retail's zero-initialized secondary
    /// best. Numeric inputs and all intermediate results must be finite. The
    /// focused contract covers well-separated transcripts; PC x87 versus
    /// binary32 equality/rounding knife edges remain open inside that domain.
    /// </summary>
    public static int? Select(
        RetailUnitAITargetProfile profile,
        int ownerAllegiance138,
        IReadOnlyList<RetailUnitAITargetCandidate> candidates,
        Level100ReleasedRandom? random = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (profile.Indiscriminate128 != 0)
        {
            ArgumentNullException.ThrowIfNull(random);
        }

        RequireFinite(profile.MaximumDistance158, nameof(profile));
        RequireFinite(profile.EmplacementPriority164, nameof(profile));
        RequireFinite(profile.VehiclePriority168, nameof(profile));
        RequireFinite(profile.BuildingPriority16C, nameof(profile));
        RequireFinite(profile.NavalPriority170, nameof(profile));
        RequireFinite(profile.InfantryPriority174, nameof(profile));
        RequireFinite(profile.AirUnitPriority178, nameof(profile));
        RequireFinite(profile.ComponentPriority17C, nameof(profile));

        float bestPrimary = -999999.0f;
        float bestSecondary = 0.0f;
        int? selectedIndex = null;

        for (int index = 0; index < candidates.Count; index++)
        {
            RetailUnitAITargetCandidate candidate = candidates[index];

            // Retail short-circuits state, allegiance, then capability.
            if (!PassesResolvedCandidateStateGate(
                    candidate.ThingFlags2C,
                    candidate.UnitMode244))
            {
                continue;
            }

            if (!PassesTargetAllegianceGate(
                    ownerAllegiance138,
                    candidate.Allegiance138,
                    profile.Indiscriminate128))
            {
                continue;
            }

            if (candidate.CandidateCapabilityGate == 0)
            {
                continue;
            }

            RequireFinite(candidate.DistanceSquared, nameof(candidates));
            RequireFinite(candidate.RangeReductionPercent, nameof(candidates));
            RequireFinite(candidate.SelectedProviderMinimumRange, nameof(candidates));
            RequireFinite(candidate.SelectedProviderMaximumRange, nameof(candidates));
            if (candidate.DistanceSquared < 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(candidates),
                    candidate.DistanceSquared,
                    "A captured squared distance cannot be negative.");
            }

            float rangeFactor = 1.0f - (candidate.RangeReductionPercent * 0.01f);
            float rangeLimit = rangeFactor * profile.MaximumDistance158;
            float rangeLimitSquared = rangeLimit * rangeLimit;
            RequireFinite(rangeFactor, nameof(candidates));
            RequireFinite(rangeLimit, nameof(candidates));
            RequireFinite(rangeLimitSquared, nameof(candidates));
            if (!(candidate.DistanceSquared < rangeLimitSquared))
            {
                continue;
            }

            float primary = PrimaryScore(profile, candidate, random);
            if (primary < bestPrimary)
            {
                continue;
            }

            if (primary > bestPrimary)
            {
                bestPrimary = primary;
                bestSecondary = 0.0f;
                // Retail does not clear its prior winner here. A higher-primary
                // candidate with a non-positive secondary can leave it intact.
            }

            float distance = MathF.Sqrt(candidate.DistanceSquared);
            float secondary = 1000.0f - distance;
            if (candidate.SelectedProviderMinimumRange <= distance)
            {
                secondary += distance <= candidate.SelectedProviderMaximumRange
                    ? 1000000.0f
                    : 10000.0f;
            }

            RequireFinite(distance, nameof(candidates));
            RequireFinite(secondary, nameof(candidates));
            if (secondary > bestSecondary)
            {
                bestSecondary = secondary;
                selectedIndex = index;
            }
        }

        return selectedIndex;
    }

    /// <summary>
    /// Exact field predicate of PC retail <c>0x004FD5B0</c> after the caller
    /// has resolved and null-checked the candidate pointer.
    /// </summary>
    public static bool PassesResolvedCandidateStateGate(
        uint thingFlags2C,
        int unitMode244) =>
        (thingFlags2C & ThingFlagDying) == 0 &&
        unitMode244 is not 1 and not 2;

    /// <summary>
    /// Exact allegiance/indiscriminate truth table of PC retail
    /// <c>0x004FD3D0</c>. The source spelling is
    /// <c>IsTargetAlligence</c> (sic).
    /// </summary>
    public static bool PassesTargetAllegianceGate(
        int ownerAllegiance138,
        int candidateAllegiance138,
        int indiscriminate128)
    {
        if (candidateAllegiance138 == AllegianceNeutral)
        {
            return indiscriminate128 != 0;
        }

        return candidateAllegiance138 switch
        {
            AllegianceForseti =>
                ownerAllegiance138 is AllegianceMuspell or AllegianceIndependent,
            AllegianceMuspell =>
                ownerAllegiance138 is AllegianceForseti or AllegianceIndependent,
            AllegianceIndependent =>
                ownerAllegiance138 is AllegianceForseti or AllegianceMuspell,
            _ => false,
        };
    }

    private static float PrimaryScore(
        RetailUnitAITargetProfile profile,
        RetailUnitAITargetCandidate candidate,
        Level100ReleasedRandom? random)
    {
        if (profile.Indiscriminate128 == 0 &&
            !candidate.IsAThreat &&
            profile.IgnoreThreats138 == 0)
        {
            // This is a zero-score substitution, not candidate rejection. It
            // bypasses both the ladder and the independent component floor.
            return 0.0f;
        }

        uint flags = candidate.TypeFlags34;
        if (profile.Indiscriminate128 != 0)
        {
            // All measured builds jump from this arm directly to the primary
            // comparison, bypassing the deterministic component floor.
            return (random!.Next() %
                    SimulationConstants.Level100ReleasedRandomUnitModulus) *
                IndiscriminateScoreScale;
        }

        float score = (flags & ThingTypeVehicle) != 0 ? profile.VehiclePriority168
            : (flags & ThingTypeInfantry) != 0 ? profile.InfantryPriority174
            : (flags & ThingTypeAirUnit) != 0 ? profile.AirUnitPriority178
            : (flags & ThingTypeEmplacement) != 0 ? profile.EmplacementPriority164
            : (flags & ThingTypeBuilding) != 0 ? profile.BuildingPriority16C
            : (flags & ThingTypeNaval) != 0 ? profile.NavalPriority170
            : 0.0f;

        if ((flags & ThingTypeComponent) != 0 && score < profile.ComponentPriority17C)
        {
            score = profile.ComponentPriority17C;
        }

        return score;
    }

    private static void RequireFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The reproduced UnitAI selector currently admits only finite values.");
        }
    }
}
