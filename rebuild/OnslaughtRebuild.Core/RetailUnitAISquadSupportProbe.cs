// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// One squad observation captured when the released
/// <c>CWorld::GetSquadNB()</c> cursor reaches that live node.
/// </summary>
/// <remarks>
/// Retail resolves the representative twice. The two identities deliberately
/// remain separate because intervening virtual calls can mutate squad state.
/// </remarks>
public readonly record struct RetailUnitAISupportSquadObservation(
    int SquadIdentity,
    int? FirstRepresentativeUnitIdentity,
    int FirstRepresentativeAllegiance138,
    bool LinkedSupportEligible,
    RetailUnitAITargetPosition SquadPosition,
    float RangeReductionPercent,
    int? SecondRepresentativeUnitIdentity);

/// <summary>
/// Stable owner inputs for one released all-squads scan.
/// </summary>
public readonly record struct RetailUnitAISupportScanContext(
    bool ScanEnabled,
    int? CurrentSquadIdentity,
    int OwnerAllegiance138,
    int OwnerIndiscriminate128,
    RetailUnitAITargetPosition OwnerPosition,
    float OwnerMaximumDistance158);

/// <summary>
/// One ordered call that retail makes to its downstream support/spawner helper.
/// A null target is retained because the released caller still invokes the
/// helper; that helper performs its own null rejection.
/// </summary>
public readonly record struct RetailUnitAISupportHelperCall(
    int SquadIdentity,
    int? TargetUnitIdentity);

/// <summary>
/// Pure finite-domain interaction step for the ordered all-squads probe in PC
/// retail <c>0x004FF592..0x004FF6AE</c> and its released-family correspondents.
/// </summary>
/// <remarks>
/// This class does not own squad-list lifecycle, invoke virtual methods, walk
/// spawner objects, or perform spawning. The caller must execute a returned
/// helper call before reading the live node's successor: that helper can spawn
/// and tail-append another squad which retail may visit during the same scan.
/// </remarks>
public static class RetailUnitAISquadSupportProbe
{
    /// <summary>
    /// Reproduces retail's filtering and strict squared-range gate for one
    /// encountered live squad node. Returns true when retail calls the
    /// downstream helper, including when its freshly resolved target is null.
    /// </summary>
    public static bool TryBuildHelperCall(
        RetailUnitAISupportScanContext context,
        RetailUnitAISupportSquadObservation squad,
        out RetailUnitAISupportHelperCall call)
    {
        call = default;
        if (!context.ScanEnabled)
        {
            return false;
        }

        RequireFinite(context.OwnerPosition.X, nameof(context));
        RequireFinite(context.OwnerPosition.Y, nameof(context));
        RequireFinite(context.OwnerPosition.Z, nameof(context));
        RequireFinite(context.OwnerMaximumDistance158, nameof(context));

        if (context.CurrentSquadIdentity is { } current &&
            squad.SquadIdentity == current)
        {
            return false;
        }

        if (squad.FirstRepresentativeUnitIdentity is null)
        {
            return false;
        }

        if (!RetailUnitAITargetSelection.PassesTargetAllegianceGate(
                context.OwnerAllegiance138,
                squad.FirstRepresentativeAllegiance138,
                context.OwnerIndiscriminate128))
        {
            return false;
        }

        if (!squad.LinkedSupportEligible)
        {
            return false;
        }

        RequireFinite(squad.SquadPosition.X, nameof(squad));
        RequireFinite(squad.SquadPosition.Y, nameof(squad));
        RequireFinite(squad.SquadPosition.Z, nameof(squad));
        RequireFinite(squad.RangeReductionPercent, nameof(squad));

        float deltaX = squad.SquadPosition.X - context.OwnerPosition.X;
        float deltaY = squad.SquadPosition.Y - context.OwnerPosition.Y;
        float deltaZ = squad.SquadPosition.Z - context.OwnerPosition.Z;
        float distanceSquared =
            (deltaX * deltaX) +
            (deltaY * deltaY) +
            (deltaZ * deltaZ);
        float rangeFactor = 1.0f - (squad.RangeReductionPercent * 0.01f);
        float rangeLimit = rangeFactor * context.OwnerMaximumDistance158;
        float rangeLimitSquared = rangeLimit * rangeLimit;

        RequireFinite(distanceSquared, nameof(squad));
        RequireFinite(rangeFactor, nameof(squad));
        RequireFinite(rangeLimit, nameof(squad));
        RequireFinite(rangeLimitSquared, nameof(squad));
        if (!(distanceSquared < rangeLimitSquared))
        {
            return false;
        }

        call = new RetailUnitAISupportHelperCall(
            squad.SquadIdentity,
            squad.SecondRepresentativeUnitIdentity);
        return true;
    }

    private static void RequireFinite(float value, string parameterName)
    {
        if (!float.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The reproduced UnitAI support probe currently admits only finite values.");
        }
    }
}
