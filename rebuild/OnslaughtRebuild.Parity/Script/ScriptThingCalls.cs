// SPDX-License-Identifier: GPL-3.0-or-later
// The thing calls the script natives make whose owners (Things/, a contributor and a contributor) have not ported them yet, in one
// place: src/unit.h CUnit::Unknown94
// (00405e60: 2; CPlane 5, CDropship 8), EnableWeapon 004fe2b0, DisableWeapon 004fe310, GetLife
// 004f99f0, Unknown100 004fdd00; src/BattleEngine.cpp EnableFlightMode 0040dcb0, DisableFlightMode 0040dcc0,
// EnableWeapon 0040dc30, DisableWeapon 0040dc60, IsWalking 00408120. Each line becomes a direct call when its owner
// lands the member; until then it records a skip, or throws where a wrong default would change motion.

using OnslaughtRebuild.Parity.Things;

namespace OnslaughtRebuild.Parity.Script;

internal static class ScriptThingCalls
{
    /// <summary>CThing::SetObjective (Thing.SetObjective).</summary>
    public static void SetObjective(ScriptRuntime runtime, Thing thing, bool value) => thing.SetObjective(value);

    /// <summary>CUnit::Unknown94 (slot 94): the waypoint radius, 2 for a unit, 5 for a plane, 8 for a dropship.</summary>
    public static float Unknown94(Unit unit) => unit switch
    {
        Plane => 5.0f,
        Dropship => 8.0f,
        _ => 2.0f,
    };

    /// <summary>Stop (slot 64): CThing's does nothing; a unit's is Unit.Stop.</summary>
    public static void Stop(ScriptRuntime runtime, ComplexThing thing)
    {
        if (thing is Unit unit)
        {
            unit.Stop();
        }
    }

    /// <summary>CUnit::GetLife: the segments' life when the unit has segments, else mLife.</summary>
    public static float GetLife(ScriptRuntime runtime, Unit unit)
    {
        runtime.Context.Skip("CUnit::GetLife: mSegments not checked (CDestructableSegmentsController is not ported)");
        return unit.Life;
    }

    public static void EnableWeapon(ScriptRuntime runtime, Unit unit, byte[] name) =>
        runtime.Context.Skip(unit is BattleEngine ? "CBattleEngine::EnableWeapon" : "CUnit::EnableWeapon");

    public static void DisableWeapon(ScriptRuntime runtime, Unit unit, byte[] name) =>
        runtime.Context.Skip(unit is BattleEngine ? "CBattleEngine::DisableWeapon" : "CUnit::DisableWeapon");

    /// <summary>CBattleEngine::EnableFlightMode: mFlightModeActive = TRUE.</summary>
    public static void EnableFlightMode(ScriptRuntime runtime, BattleEngine be) =>
        runtime.Context.Skip("CBattleEngine::EnableFlightMode");

    /// <summary>CBattleEngine::DisableFlightMode: mFlightModeActive = FALSE, and a jet morphs back.</summary>
    public static void DisableFlightMode(ScriptRuntime runtime, BattleEngine be) =>
        runtime.Context.Skip("CBattleEngine::DisableFlightMode");

    /// <summary>CUnit::Unknown100 (the Retreat native).</summary>
    public static void Retreat(ScriptRuntime runtime, Unit unit) => unit.Unknown100();

    /// <summary>CBattleEngine::IsWalking: walker state and on the ground within the last 0.5 s.</summary>
    public static bool IsWalking(ScriptRuntime runtime, BattleEngine be) =>
        throw new NotSupportedException("CBattleEngine::IsWalking is not ported");
}
