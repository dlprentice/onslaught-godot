// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/Unit.cpp, CUnit::GetShouldDoHitEffect (004fd5e0).

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Things;

public abstract partial class Unit
{
    /// <summary>
    /// The drawn part's hit brightness. A recently damaged segment refreshes the unit's countdown while it is
    /// rendered, even if the returned brightness is zero. This is a core render side effect, independent of host
    /// drawing. Retail 004fd649..004fd66a rounds age * 20 to nearest-even (x87 FIST), then subtracts integer steps.
    /// </summary>
    public override int GetShouldDoHitEffect(int partNumber)
    {
        if (Segments is not { } segments)
        {
            return Unknown218;
        }

        double age = X87.Sub(Context.Events.Time, segments.GetLastDamageTime(partNumber));
        // FCOMP tests C0 alone: unordered follows the recent-hit branch too.
        if (age >= 1.0f)
        {
            return 0;
        }

        Unknown107(segments.GetLastDamageAmount(partNumber));
        Unknown21C = 10;
        age = X87.Sub(Context.Events.Time, segments.GetLastDamageTime(partNumber));
        int brightness = unchecked(Unknown218 - X87.Fist(X87.Mul(age, 20.0f)) * Unknown21C);
        return Math.Clamp(brightness, 0, 100);
    }
}
