// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/WorldPhysicsManager.cpp (UPhysicsManager::SpawnThing 0050df80). Not in Stuart's release.

using OnslaughtRebuild.Parity.Physics;

namespace OnslaughtRebuild.Parity.Things;

/// <summary>UPhysicsManager::SpawnThing: the unit class a definition's behaviour makes.</summary>
public static class UnitFactory
{
    /// <summary>
    /// A new unit for the definition at <paramref name="unitIndex"/>, or null for none (an unknown index, the jeep, or a
    /// class not ported yet, which throws instead so a load never silently drops a thing). Retail also returns null when
    /// the thing heap has under 10 KB free; that check is not modelled.
    /// </summary>
    public static Unit? SpawnThing(ThingContext context, int unitIndex)
    {
        if (unitIndex < 0 || unitIndex >= context.Physics.Units.Count)
        {
            return null;
        }

        UnitBehaviour behaviour = context.Physics.Units[unitIndex].Behaviour;
        return behaviour switch
        {
            UnitBehaviour.GroundUnit => new GroundVehicle(context),
            UnitBehaviour.Turret => new Cannon(context),
            UnitBehaviour.Building => new Building(context),
            UnitBehaviour.Dropship => new Dropship(context),
            UnitBehaviour.Aircraft or UnitBehaviour.Fighter => new Plane(context),
            UnitBehaviour.SimpleBuilding => new SimpleBuilding(context),
            UnitBehaviour.Jeep => null,
            _ => throw new NotSupportedException($"unit behaviour {behaviour} is not ported yet"),
        };
    }

    /// <summary>SpawnThing by name (world.cpp's wrapper around GetUnitIndex).</summary>
    public static Unit? SpawnThing(ThingContext context, byte[] name) => SpawnThing(context, context.Physics.GetUnitIndex(name));
}
