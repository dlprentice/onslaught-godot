// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Whether a thing is a <c>CUnit</c>, whether it is a building, and its thing
/// type word (<c>+0x34</c>): the class's slot-38 <c>SetThingType</c> constant
/// plus, for units, the shared Unit setter's <c>0x80000013</c>.
/// </summary>
public readonly record struct Level100ThingClass(bool IsUnit, bool IsBuilding, uint TypeMask);

/// <summary>
/// The native class of every Level 100 thing a crosshair or lock query can
/// meet, by definition name. The registry admits only the Ammunition and
/// Battle Engine type bits, so the unit and building bits the crosshair
/// tests (<c>BattleEngine.cpp:2325-2327</c>) come from each thing's class.
/// </summary>
/// <remarks>
/// Evidence: the RE lane's class table in
/// <c>reverse-engineering/game-mechanics/level100-final-drone-wave.md</c>
/// ("AI owners that draw shared RNG"): base-world rows 0-2, 13-20 and 23-29
/// are <c>CBuilding</c>, the four turrets are <c>CCannon</c>, the icebergs are
/// features, and the level-world Target Tank/Truck are <c>CGroundVehicle</c>,
/// the Warehouse a <c>CBuilding</c>, and Air Trainer and Target Drone
/// <c>CPlane</c>. The city buildings (rows 30-34) are <c>CSimpleBuilding</c>,
/// whose slot 66 uses the Unit small-explosion path
/// (<c>reverse-engineering/binary-analysis/cexplosion-factory-callers-2026-08-10.md</c>);
/// the RE lane's Q16 answer confirms its type constant is CBuilding's.
/// </remarks>
public static class Level100ThingClasses
{
    // Slot-38 class constants (the RE lane's Q16 answer): CBuilding and
    // CSimpleBuilding 0x40100120 (0x00417660), CCannon 0x40040220,
    // CGroundVehicle 0x40020200, CPlane 0x40000400; the Unit setter
    // 0x004fcdc0 adds 0x80000013. The conditional 0x00200000 and the
    // renderability bit 0x00800000 do not meet any lock mask here.
    private const uint UnitSetter = 0x80000013u;
    private static readonly Level100ThingClass s_building = new(true, true, 0x40100120u | UnitSetter);
    private static readonly Level100ThingClass s_cannon = new(true, false, 0x40040220u | UnitSetter);
    private static readonly Level100ThingClass s_groundVehicle = new(true, false, 0x40020200u | UnitSetter);
    private static readonly Level100ThingClass s_plane = new(true, false, 0x40000400u | UnitSetter);
    private static readonly Level100ThingClass s_feature = new(false, false, 0u);

    public static Level100ThingClass Of(string? definitionName) => definitionName switch
    {
        "Control Tower" or "Forseti Pulse Tank Factory" or "Forseti Repair Pad" or
            "Forseti Research Building" or "Forseti Building 1" or "Forseti Building 2" or
            "Forseti Building 3" or "Forseti Solar Pod" or "Forseti Radar Station" or
            "Forseti Light Fighter Airfield" or "Forseti Docks" or "Hangar" or
            "Forseti Tall Building 1" or "Forseti Tall Building 3" or "Warehouse" => s_building,
        "Forseti City Building 1" or "Forseti City Building 2" or
            "Forseti City Building 3" => s_building,
        "SAT Turret" or "Blaster Turret" or "Pulse Turret" => s_cannon,
        "Target Tank" or "Target Truck" => s_groundVehicle,
        "Target Drone" or "Air Trainer" => s_plane,
        "Iceberg 1" or "Iceberg 2" or "Iceberg 3" or "Iceberg 4" => s_feature,
        _ => throw new NotSupportedException(
            $"Level 100 thing class is unadmitted for definition '{definitionName}'."),
    };
}
