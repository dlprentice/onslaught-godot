// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Canonical Aquila resources at one <c>CBattleEngine::Damage</c> boundary.
/// Values use Core's milli-retail units; life may be negative in the transient
/// result because retail tests the signed float before the actor registry
/// projects it to a non-negative health field.
/// </summary>
internal readonly record struct Level100PlayerDamageState(
    int LifeMilli,
    int EnergyMilli,
    int ShieldMilli,
    int AugmentChargeMilli,
    bool AugmentActive);

internal readonly record struct Level100PlayerDamageResult(
    Level100PlayerDamageState State,
    int ShieldAbsorbedMilliLife,
    int LifeDamageMilliLife,
    bool RequestsDeath);

/// <summary>
/// Integer projection of the released Aquila Prototype damage law.
/// </summary>
internal static class Level100PlayerDamage
{
    internal static Level100PlayerDamageState AdvanceAugment(
        Level100PlayerDamageState state)
    {
        if (state.AugmentChargeMilli < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        int charge = state.AugmentChargeMilli;
        bool active = state.AugmentActive;
        if (active)
        {
            charge -= SimulationConstants.AugmentDrainPerTick;
            if (charge <= 0)
            {
                charge = 0;
                active = false;
            }
        }
        else if (charge >= SimulationConstants.MaximumAugmentCharge)
        {
            charge = SimulationConstants.MaximumAugmentCharge;
            active = true;
        }

        return state with
        {
            AugmentChargeMilli = charge,
            AugmentActive = active,
        };
    }

    internal static Level100PlayerDamageResult Apply(
        Level100PlayerDamageState state,
        int incomingDamageMilliLife,
        bool damageShields,
        bool isWalker)
    {
        if (incomingDamageMilliLife <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incomingDamageMilliLife));
        }
        if (state.EnergyMilli < 0 ||
            state.ShieldMilli < 0 ||
            state.AugmentChargeMilli < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Aquila resource stores cannot be negative.");
        }

        int life = state.LifeMilli;
        int energy = state.EnergyMilli;
        int shield = state.ShieldMilli;
        int augment = state.AugmentChargeMilli;
        int shieldAbsorbed = 0;
        int lifeDamage = 0;
        bool requestsDeath = false;

        // BattleEngine.cpp:2141 gates the mutation on the signed life value.
        // Damage still caps augment and synchronizes walker energy afterward.
        if (life >= 0)
        {
            int remaining = incomingDamageMilliLife;
            if (damageShields && shield >= remaining)
            {
                // Aquila Prototype ships mShieldEfficiency = 98.0. Whole
                // milli-life can represent the float result exactly only for
                // multiples of 50; reject any other sufficient-shield input
                // instead of silently choosing a rounding law retail did not.
                if (remaining % 50 != 0)
                {
                    throw new InvalidOperationException(
                        "Sufficient-shield Aquila damage is exactly representable " +
                        "in milli-life only for inputs divisible by 50.");
                }

                shieldAbsorbed = checked((int)(
                    (long)remaining * SimulationConstants.AquilaShieldEfficiencyPercent /
                    100));
                shield -= shieldAbsorbed;
                remaining -= shieldAbsorbed;
                if (!state.AugmentActive)
                {
                    augment = checked(augment + shieldAbsorbed);
                }
            }

            // Retail's second arm is deliberately strict `>` and, when the
            // original shield store is insufficient, absorbs that store 1:1
            // rather than applying the efficiency percentage again.
            if (damageShields && remaining > shield)
            {
                int overflowAbsorbed = shield;
                if (!state.AugmentActive)
                {
                    augment = checked(augment + overflowAbsorbed);
                }
                shieldAbsorbed = checked(shieldAbsorbed + overflowAbsorbed);
                remaining -= overflowAbsorbed;
                shield = 0;
            }

            lifeDamage = remaining;
            life = checked(life - remaining);
            requestsDeath = life < 0;
        }

        augment = Math.Min(augment, SimulationConstants.MaximumAugmentCharge);
        if (isWalker)
        {
            energy = shield;
        }

        return new Level100PlayerDamageResult(
            new Level100PlayerDamageState(
                life,
                energy,
                shield,
                augment,
                state.AugmentActive),
            shieldAbsorbed,
            lifeDamage,
            requestsDeath);
    }
}
