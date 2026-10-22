// SPDX-License-Identifier: GPL-3.0-only
// Adapted from bea-decomp src/MissionScript/IScript.cpp (the natives table gScriptFunctions 0064ce20, filled by
// 0052ff30; the natives SetSlotSave 00533900 .. PostEvent 005383c0 named on each method) and vminternal.h
// (SScriptFunction), reconstructed from the retail executable; not in Stuart's release.

using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Maths;
using OnslaughtRebuild.Parity.Mesh;
using OnslaughtRebuild.Parity.Numerics;
using OnslaughtRebuild.Parity.Things;
using OnslaughtRebuild.Parity.World;
using ParityGame = OnslaughtRebuild.Parity.Game.Game;

namespace OnslaughtRebuild.Parity.Script;

/// <summary>A native's handler: the popped arguments, their count, and the result (null for none).</summary>
public delegate void NativeFunction(IScript script, ScriptData?[] args, int numArgs, ref ScriptData? result);

/// <summary>
/// SScriptFunction: a native's name, argument count, whether it returns a value, its argument types (EDataType, 7 for
/// any) and return type, and its handler.
/// </summary>
public sealed record ScriptNative(string Name, int NumArgs, bool ReturnsValue, int[] ArgTypes, int ReturnType,
    NativeFunction Function);

/// <summary>gScriptFunctions: the natives by the CALL instruction's index (retail's table order).</summary>
public static class ScriptNatives
{
    public static readonly ScriptNative[] Table =
    [
        new("FollowWaypoint", 2, false, [3,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.FollowWaypoint(a, n, ref r)), // 0
        new("FollowWaypointWait", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.FollowWaypointWait(a, n, ref r)), // 1
        new("SetSpeed", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSpeed(a, n, ref r)), // 2
        new("SpawnThing", 4, false, [3,3,1,3,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SpawnThing(a, n, ref r)), // 3
        new("Pause", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Pause(a, n, ref r)), // 4
        new("PostEvent", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PostEvent(a, n, ref r)), // 5
        new("Rand", 1, true, [1,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Rand(a, n, ref r)), // 6
        new("IsFriendly", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsFriendly(a, n, ref r)), // 7
        new("LevelLost", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.LevelLost(a, n, ref r)), // 8
        new("LevelWon", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.LevelWon(a, n, ref r)), // 9
        new("PlaySample", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlaySample(a, n, ref r)), // 10
        new("Print", 1, false, [7,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Print(a, n, ref r)), // 11
        new("Exists", 1, true, [5,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Exists(a, n, ref r)), // 12
        new("Die", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Die(a, n, ref r)), // 13
        new("GetThingRef", 1, true, [5,0,0,0,0,0,0], 5, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetThingRef(a, n, ref r)), // 14
        new("Activate", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Activate(a, n, ref r)), // 15
        new("PrintText", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PrintText(a, n, ref r)), // 16
        new("AddMessage", 2, false, [1,2,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.AddMessage(a, n, ref r)), // 17
        new("GetHealth", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetHealth(a, n, ref r)), // 18
        new("SetTimer", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetTimer(a, n, ref r)), // 19
        new("GetDistToObj", 1, true, [5,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetDistToObj(a, n, ref r)), // 20
        new("GetPlayer", 1, true, [1,0,0,0,0,0,0], 5, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetPlayer(a, n, ref r)), // 21
        new("SetVisible", 1, false, [4,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetVisible(a, n, ref r)), // 22
        new("SetObjective", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetObjective(a, n, ref r)), // 23
        new("SetAIState", 1, false, [1,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetAIState(a, n, ref r)), // 24
        new("IsA", 1, true, [1,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsA(a, n, ref r)), // 25
        new("Teleport", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Teleport(a, n, ref r)), // 26
        new("PlayCutscene", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayCutscene(a, n, ref r)), // 27
        new("PlayCharMessage", 3, false, [1,1,2,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayCharMessage(a, n, ref r)), // 28
        new("Deactivate", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Deactivate(a, n, ref r)), // 29
        new("UnsetObjective", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.UnsetObjective(a, n, ref r)), // 30
        new("IsObjective", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsObjective(a, n, ref r)), // 31
        new("SetVulnerable", 1, false, [4,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetVulnerable(a, n, ref r)), // 32
        new("NumContained", 1, true, [3,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.NumContained(a, n, ref r)), // 33
        new("HighlightHudPart", 1, false, [1,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.HighlightHudPart(a, n, ref r)), // 34
        new("UnHighlightHudPart", 1, false, [1,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.UnHighlightHudPart(a, n, ref r)), // 35
        new("PlayCharMessageWait", 3, false, [1,1,2,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayCharMessageWait(a, n, ref r)), // 36
        new("GetWeaponAmmo", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetWeaponAmmo(a, n, ref r)), // 37
        new("GetWeaponName", 0, true, [0,0,0,0,0,0,0], 3, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetWeaponName(a, n, ref r)), // 38
        new("GetWeaponCharge", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetWeaponCharge(a, n, ref r)), // 39
        new("GetNumUnits", 2, true, [1,1,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetNumUnits(a, n, ref r)), // 40
        new("SetAllegiance", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetAllegiance(a, n, ref r)), // 41
        new("SetWindVector", 3, false, [2,2,2,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetWindVector(a, n, ref r)), // 42
        new("SetRainDensity", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetRainDensity(a, n, ref r)), // 43
        new("SetSnowDensity", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSnowDensity(a, n, ref r)), // 44
        new("SetLightningDensity", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetLightningDensity(a, n, ref r)), // 45
        new("SetHealth", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetHealth(a, n, ref r)), // 46
        new("GetRatioBattleLineNodes", 1, true, [1,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetRatioBattleLineNodes(a, n, ref r)), // 47
        new("SetScript", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetScript(a, n, ref r)), // 48
        new("GetPos", 0, true, [0,0,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetPos(a, n, ref r)), // 49
        new("SpawnParticle", 2, false, [3,6,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SpawnParticle(a, n, ref r)), // 50
        new("SetSegmentHealth", 2, false, [3,2,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSegmentHealth(a, n, ref r)), // 51
        new("SetAllSegmentsHealth", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetAllSegmentsHealth(a, n, ref r)), // 52
        new("PlayAnimation", 3, false, [3,4,4,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayAnimation(a, n, ref r)), // 53
        new("PlayAnimationWait", 3, false, [3,4,4,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayAnimationWait(a, n, ref r)), // 54
        new("GetMapHeight", 1, true, [6,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetMapHeight(a, n, ref r)), // 55
        new("GetWaterHeight", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetWaterHeight(a, n, ref r)), // 56
        new("GetX", 1, true, [6,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetX(a, n, ref r)), // 57
        new("GetY", 1, true, [6,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetY(a, n, ref r)), // 58
        new("GetZ", 1, true, [6,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetZ(a, n, ref r)), // 59
        new("SetX", 2, true, [6,2,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetX(a, n, ref r)), // 60
        new("SetY", 2, true, [6,2,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetY(a, n, ref r)), // 61
        new("SetZ", 2, true, [6,2,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetZ(a, n, ref r)), // 62
        new("SetGoalPoint", 1, false, [6,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetGoalPoint(a, n, ref r)), // 63
        new("GetSafePos", 0, true, [0,0,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetSafePos(a, n, ref r)), // 64
        new("GetComponent", 1, true, [1,0,0,0,0,0,0], 5, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetComponent(a, n, ref r)), // 65
        new("CreatePosition", 3, true, [2,2,2,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.CreatePosition(a, n, ref r)), // 66
        new("GetFloatRand", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetFloatRand(a, n, ref r)), // 67
        new("SetVar", 2, false, [3,7,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetVar(a, n, ref r)), // 68
        new("Damage", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Damage(a, n, ref r)), // 69
        new("GameTime", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GameTime(a, n, ref r)), // 70
        new("IsEnemy", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsEnemy(a, n, ref r)), // 71
        new("Land", 0, false, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Land(a, n, ref r)), // 72
        new("SpawnersEmpty", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SpawnersEmpty(a, n, ref r)), // 73
        new("Dive", 0, false, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Dive(a, n, ref r)), // 74
        new("Surface", 0, false, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Surface(a, n, ref r)), // 75
        new("InitVariable", 2, false, [1,1,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.InitVariable(a, n, ref r)), // 76
        new("SetVariable", 3, false, [1,2,2,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetVariable(a, n, ref r)), // 77
        new("ShutdownVariable", 1, false, [1,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.ShutdownVariable(a, n, ref r)), // 78
        new("Shutdown", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Shutdown(a, n, ref r)), // 79
        new("GetEnergy", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetEnergy(a, n, ref r)), // 80
        new("GetVariable", 1, true, [2,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetVariable(a, n, ref r)), // 81
        new("Retreat", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Retreat(a, n, ref r)), // 82
        new("PrimaryObjectiveComplete", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PrimaryObjectiveComplete(a, n, ref r)), // 83
        new("SecondaryObjectiveComplete", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SecondaryObjectiveComplete(a, n, ref r)), // 84
        new("AddScore", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.AddScore(a, n, ref r)), // 85
        new("Attack", 1, false, [5,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Attack(a, n, ref r)), // 86
        new("PrimaryObjectiveFailed", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PrimaryObjectiveFailed(a, n, ref r)), // 87
        new("SecondaryObjectiveFailed", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SecondaryObjectiveFailed(a, n, ref r)), // 88
        new("GetAngle", 1, true, [5,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetAngle(a, n, ref r)), // 89
        new("PlayPCharMessage", 4, false, [1,1,2,1,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayPCharMessage(a, n, ref r)), // 90
        new("PlayPCharMessageWait", 4, false, [1,1,2,1,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.PlayPCharMessageWait(a, n, ref r)), // 91
        new("GetInitialHealth", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetInitialHealth(a, n, ref r)), // 92
        new("SpawnersInUse", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SpawnersInUse(a, n, ref r)), // 93
        new("SetSpawnScript", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSpawnScript(a, n, ref r)), // 94
        new("Stop", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Stop(a, n, ref r)), // 95
        new("Deploy", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Deploy(a, n, ref r)), // 96
        new("Undeploy", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Undeploy(a, n, ref r)), // 97
        new("EnableWeapon", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.EnableWeapon(a, n, ref r)), // 98
        new("DisableWeapon", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.DisableWeapon(a, n, ref r)), // 99
        new("EnableFlightMode", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.EnableFlightMode(a, n, ref r)), // 100
        new("DisableFlightMode", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.DisableFlightMode(a, n, ref r)), // 101
        new("GetSquad", 0, true, [0,0,0,0,0,0,0], 5, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetSquad(a, n, ref r)), // 102
        new("GetTarget", 0, true, [0,0,0,0,0,0,0], 5, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetTarget(a, n, ref r)), // 103
        new("Normalise", 1, true, [6,0,0,0,0,0,0], 6, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Normalise(a, n, ref r)), // 104
        new("Magnitude", 1, true, [6,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Magnitude(a, n, ref r)), // 105
        new("LevelLostString", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.LevelLostString(a, n, ref r)), // 106
        new("IsFiring", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsFiring(a, n, ref r)), // 107
        new("IsNumberBetween", 3, true, [2,2,2,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsNumberBetween(a, n, ref r)), // 108
        new("SetSegmentVulnerable", 2, false, [3,4,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSegmentVulnerable(a, n, ref r)), // 109
        new("SetAllSegmentsVulnerable", 1, false, [4,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetAllSegmentsVulnerable(a, n, ref r)), // 110
        new("GetRealHealth", 0, true, [0,0,0,0,0,0,0], 2, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetRealHealth(a, n, ref r)), // 111
        new("SwitchMessagesOn", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SwitchMessagesOn(a, n, ref r)), // 112
        new("SwitchMessagesOff", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SwitchMessagesOff(a, n, ref r)), // 113
        new("Goto3PointPanCamera", 5, false, [5,6,6,6,2,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Goto3PointPanCamera(a, n, ref r)), // 114
        new("Goto4PointPanCamera", 6, false, [5,6,6,6,6,2,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Goto4PointPanCamera(a, n, ref r)), // 115
        new("GotoPlayerCamera", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GotoPlayerCamera(a, n, ref r)), // 116
        new("GetConfiguration", 0, true, [0,0,0,0,0,0,0], 3, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetConfiguration(a, n, ref r)), // 117
        new("AddHelpMessage", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.AddHelpMessage(a, n, ref r)), // 118
        new("SetGoodieState", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetGoodieState(a, n, ref r)), // 119
        new("GetGoodieState", 1, true, [1,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetGoodieState(a, n, ref r)), // 120
        new("MPDeclarePlayerWon", 1, false, [1,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.MPDeclarePlayerWon(a, n, ref r)), // 121
        new("MPDeclareGameDrawn", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.MPDeclareGameDrawn(a, n, ref r)), // 122
        new("SetSlot", 2, false, [1,4,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSlot(a, n, ref r)), // 123
        new("GetSlot", 1, true, [1,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetSlot(a, n, ref r)), // 124
        new("InJetMode", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.InJetMode(a, n, ref r)), // 125
        new("SetPlayerLives", 2, false, [1,1,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetPlayerLives(a, n, ref r)), // 126
        new("Launch", 1, false, [6,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.Launch(a, n, ref r)), // 127
        new("HalfDestroy", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.HalfDestroy(a, n, ref r)), // 128
        new("SetVelocity", 1, false, [6,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetVelocity(a, n, ref r)), // 129
        new("TriggerHitEffect", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.TriggerHitEffect(a, n, ref r)), // 130
        new("GetNumber", 0, true, [0,0,0,0,0,0,0], 1, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.GetNumber(a, n, ref r)), // 131
        new("SpawnEscapePod", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SpawnEscapePod(a, n, ref r)), // 132
        new("SetSlotSave", 2, false, [1,4,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetSlotSave(a, n, ref r)), // 133
        new("ResetSegmentHealth", 2, false, [3,2,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.ResetSegmentHealth(a, n, ref r)), // 134
        new("SetPos", 1, false, [6,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetPos(a, n, ref r)), // 135
        new("SetLockable", 1, false, [4,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetLockable(a, n, ref r)), // 136
        new("ToggleCockpit", 0, false, [0,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.ToggleCockpit(a, n, ref r)), // 137
        new("SetStealth", 1, false, [2,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetStealth(a, n, ref r)), // 138
        new("TeleportOrientation", 3, false, [2,2,2,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.TeleportOrientation(a, n, ref r)), // 139
        new("EnableSpawner", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.EnableSpawner(a, n, ref r)), // 140
        new("DisableSpawner", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.DisableSpawner(a, n, ref r)), // 141
        new("SetName", 1, false, [3,0,0,0,0,0,0], 0, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.SetName(a, n, ref r)), // 142
        new("IsOverWater", 0, true, [0,0,0,0,0,0,0], 4, (IScript s, ScriptData?[] a, int n, ref ScriptData? r) => s.IsOverWater(a, n, ref r)), // 143
    ];
}

public sealed partial class IScript
{
    // ---- The natives level 100's scripts call (the census in ScriptCensusTests), in retail's address order ----

    /// <summary>SetSlotSave 00533900: the game's slot and the career's.</summary>
    public void SetSlotSave(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        Game.SetSlot(args[0]!.GetInt(), args[1]!.IsTrue());
        if (Game.CareerSetSlot is { } careerSetSlot)
        {
            careerSetSlot(args[0]!.GetInt(), args[1]!.IsTrue() ? 1 : 0);
        }
        else
        {
            _runtime.Context.Skip("IScript::SetSlotSave: CCareer::SetSlot (no career: a -level run)");
        }
    }

    /// <summary>GetSlot 005339a0.</summary>
    public void GetSlot(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        result = new BoolData(Game.GetSlot(args[0]!.GetInt()));

    /// <summary>AddHelpMessage 00533b30: only string 2302408 shows help text 0xdd.</summary>
    public void AddHelpMessage(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Game.HasHelpTextDisplay && args[0]!.GetInt() == 2302408)
        {
            _runtime.Context.Skip("IScript::AddHelpMessage: CHelpTextDisplay::AddMessage");
        }
    }

    /// <summary>AddScore 005343c0.</summary>
    public void AddScore(ScriptData?[] args, int numArgs, ref ScriptData? result) => Game.IncScore(args[0]!.GetInt());

    /// <summary>PrimaryObjectiveComplete 005343e0: objectives count from 1 in scripts.</summary>
    public void PrimaryObjectiveComplete(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        Game.SetPrimaryObjectiveComplete(args[0]!.GetInt() - 1, args[1]!.GetInt());

    /// <summary>PrimaryObjectiveFailed 00534440.</summary>
    public void PrimaryObjectiveFailed(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        Game.SetPrimaryObjectiveFailed(args[0]!.GetInt() - 1, args[1]!.GetInt());

    /// <summary>Stop 00534f30: the thing heads for where it is (no unit check in retail).</summary>
    public void Stop(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        var unit = (ComplexThing)Thing!;
        unit.GoToPoint(unit.Pos);
    }

    /// <summary>EnableWeapon 00534fb0.</summary>
    public void EnableWeapon(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.Unit))
        {
            ScriptThingCalls.EnableWeapon(_runtime, (Unit)Thing, args[0]!.GetString()!);
        }
    }

    /// <summary>DisableWeapon 00534fe0.</summary>
    public void DisableWeapon(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.Unit))
        {
            ScriptThingCalls.DisableWeapon(_runtime, (Unit)Thing, args[0]!.GetString()!);
        }
    }

    /// <summary>EnableFlightMode 00535070.</summary>
    public void EnableFlightMode(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.BattleEngine))
        {
            ScriptThingCalls.EnableFlightMode(_runtime, (BattleEngine)Thing);
        }
    }

    /// <summary>DisableFlightMode 00535090.</summary>
    public void DisableFlightMode(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.BattleEngine))
        {
            ScriptThingCalls.DisableFlightMode(_runtime, (BattleEngine)Thing);
        }
    }

    /// <summary>SetAllegiance 00535560.</summary>
    public void SetAllegiance(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.Unit))
        {
            ((Unit)Thing).SetAllegiance(args[0]!.GetInt());
        }
    }

    /// <summary>GetHealth 00535920: a unit's CUnit::GetLife, else 0.</summary>
    public void GetHealth(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        float value = 0.0f;
        if (Thing!.IsA(ThingType.Unit))
        {
            value = ScriptThingCalls.GetLife(_runtime, (Unit)Thing);
        }

        result = new FloatData(value);
    }

    /// <summary>SetScript 00535c50: the thing's script is replaced (CComplexThing::SetScript).</summary>
    public void SetScript(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        ((ComplexThing)Thing!).SetScript(args[0]!.GetString()!);

    /// <summary>Retreat 00535d30: CUnit::Unknown100.</summary>
    public void Retreat(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (Thing!.IsA(ThingType.Unit))
        {
            ScriptThingCalls.Retreat(_runtime, (Unit)Thing);
        }
    }

    /// <summary>Activate 00535d50.</summary>
    public void Activate(ScriptData?[] args, int numArgs, ref ScriptData? result) => Thing!.Activate();

    /// <summary>Deactivate 00535d60.</summary>
    public void Deactivate(ScriptData?[] args, int numArgs, ref ScriptData? result) => Thing!.Deactivate();

    /// <summary>HighlightHudPart 00535e60: HUD.mPartState[part] = 2.</summary>
    public void HighlightHudPart(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        _runtime.Context.Skip("IScript::HighlightHudPart: HUD.mPartState");

    /// <summary>UnHighlightHudPart 00535e80: HUD.mPartState[part] = 1.</summary>
    public void UnHighlightHudPart(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        _runtime.Context.Skip("IScript::UnHighlightHudPart: HUD.mPartState");

    /// <summary>SetObjective 00535ed0.</summary>
    public void SetObjective(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        ScriptThingCalls.SetObjective(_runtime, Thing!, true);

    /// <summary>UnsetObjective 00535ee0.</summary>
    public void UnsetObjective(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        ScriptThingCalls.SetObjective(_runtime, Thing!, false);

    /// <summary>IsObjective 00535ef0.</summary>
    public void IsObjective(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        result = new BoolData(Thing!.IsObjective());

    /// <summary>
    /// Attack 00535fa0: a unit attacks a unit or the squad's slot-74 member (CUnit::SetSquadTarget); a squad attacks
    /// a squad or a unit's squad (CSquad::Unknown85). This sets targets, not movement, events or AI state.
    /// </summary>
    public void Attack(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        Thing? target = args[0]!.GetThing();
        if (target is null)
        {
            _runtime.LogMessage("Warning: Called Attack with NULL object");
            return;
        }

        if (Thing!.IsA(ThingType.Unit))
        {
            Unit? unit = target.IsA(ThingType.Unit) ? (Unit)target
                : target.IsA(ThingType.Unknown20000000) ? ((Squad)target).Unknown74() as Unit : null;
            if (unit is not null) ((Unit)Thing).SetSquadTarget(unit);
        }
        else if (Thing.IsA(ThingType.Unknown20000000))
        {
            Squad? squad = target.IsA(ThingType.Unit) ? ((Unit)target).GetSquad()
                : target.IsA(ThingType.Unknown20000000) ? (Squad)target : null;
            if (squad is not null) ((Squad)Thing).Unknown85(squad);
        }
    }

    /// <summary>SetAIState 005361a0.</summary>
    public void SetAIState(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        Thing!.SetAIState((AIState)args[0]!.GetInt());

    /// <summary>IsA 00536350: the thing type bits.</summary>
    public void IsA(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        result = new BoolData(Thing!.IsA(unchecked((uint)args[0]!.GetInt())));

    /// <summary>
    /// GetPlayer 005363e0: player n's battle engine (players count from 1; 0 or less logs and takes player 2). With no
    /// such player retail makes a value of the script's thing, loses it, and returns a null battle engine.
    /// </summary>
    public void GetPlayer(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        Func<int, (bool HasPlayer, Thing? BattleEngine)> players = _runtime.Player
            ?? throw new NotSupportedException("IScript::GetPlayer: the players are not wired (ScriptRuntime.Player)");
        int number = args[0]!.GetInt() - 1;
        if (number < 0)
        {
            _runtime.LogMessage($"Warning: sorry no player {args[0]!.GetInt()} returning default player");
            number = 1;
        }

        (bool hasPlayer, Thing? be) = players(number);
        if (!hasPlayer)
        {
            _runtime.LogMessage($"Fatal error: Player {args[0]!.GetInt()} has no battle engine!!!");
            be = null;
        }

        result = new ThingPtrData(be);
    }

    /// <summary>GetThingRef 005367c0: CWorld::FindThingByName over the named things (strcmp), or a null pointer.</summary>
    public void GetThingRef(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        byte[] name = args[0]!.GetString()!;
        SPtrSet<Thing> named = _runtime.Context.World.NamedThings;
        for (Thing? t = named.First(); t is not null; t = named.Next())
        {
            if (ScriptData.SameString(name, ((ComplexThing)t).Name))
            {
                result = new ThingPtrData(t);
                return;
            }
        }

        _runtime.LogMessage($"FATAL ERROR: Can't find object '{System.Text.Encoding.Latin1.GetString(name)}' in call to 'GetThingRef'");
        result = new ThingPtrData(null);
    }

    /// <summary>Exists 00536920.</summary>
    public void Exists(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        result = new BoolData(args[0]!.GetThing() is not null);

    /// <summary>
    /// SpawnThing 00536cd0: a unit of the named type at one of this thing's emitters (world space), facing along the
    /// emitter's Y axis, or where this thing is and as it faces when the emitter is at the origin; with the named script,
    /// this thing's allegiance if it is a unit (else 2), collisions from 1.5 s, and SpawnerA..E's waypoint emitter types.
    /// A unit spawner gives the new unit a squad of its own, assembled around it.
    /// </summary>
    public void SpawnThing(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        byte[] type = args[0]!.GetString()!;
        byte[] script = args[3]!.GetString()!;
        byte[] emitter = args[1]!.GetString()!;
        int emitterIndex = args[2]!.GetInt();
        ThingContext context = _runtime.Context;

        RenderThing? render = Thing!.RenderThing;
        if (render is null)
        {
            return;
        }

        (FVector pos, FMatrix ori) = MeshPose.GetRTEmitter(render.MeshOrNull, render.Pose, (IRenderSubject)Thing,
            System.Text.Encoding.Latin1.GetString(emitter), emitterIndex, context.TransformStatics, local: false, force: false);

        Unit? thing = UnitFactory.SpawnThing(context, type);
        if (thing is null)
        {
            return;
        }

        var init = new InitThing { Kind = InitThingRecord.InitKind.Unit, Stats = context.Physics.GetUnitData(type) };
        (init.Yaw, init.Pitch) = SpawnDirection(ori);
        if (script.Length != 0)
        {
            init.Script = script;
        }

        if (pos.X == 0.0f && pos.Y == 0.0f && pos.Z == 0.0f)
        {
            init.Pos = Thing.Pos;
            EulerAngles angles = EulerAngles.FromMatrix(((ComplexThing)Thing).Orientation);
            init.Yaw = angles.Yaw;
            init.Pitch = angles.Pitch;
            init.Roll = angles.Roll;
        }
        else
        {
            init.Pos = pos;
        }

        init.Velocity = new FVector(0.0f, 0.0f, 0.0f);
        init.Allegiance = 2;
        if (Thing.IsA(ThingType.Unit))
        {
            init.Allegiance = ((Unit)Thing).GetAllegiance();
        }

        init.InitCst.TimeBeforeStart = 1.5f;
        init.SpawnedBy = Thing;
        init.WaypointPath = SpawnerEmitterType(emitter);

        thing.Init(init);

        if (Thing.IsA(ThingType.Unit))
        {
            var squadInit = new InitThing
            {
                Kind = InitThingRecord.InitKind.Squad,
                Pos = thing.Pos,
                Orientation = thing.Orientation,
                Allegiance = thing.GetAllegiance(),
                UnitName = type,
            };

            IWorldSpawner spawner = _runtime.Spawner
                ?? throw new NotSupportedException("IScript::SpawnThing needs UPhysicsManager::SpawnSquad (ScriptRuntime.Spawner)");
            if (spawner.SpawnSquad(context.Physics.GetUnitData(type)!.Behaviour) is Squad squad)
            {
                squad.SpawnedFromUnitRecord = true;
                squad.Assembling = true;
                squad.Init(squadInit);
                squad.Unknown67(thing, 0, 0);
                squad.Assembling = false;
            }
        }
    }

    /// <summary>
    /// The spawn direction: (ori * FVector(0, 100, 0)).Azimuth() and .Elevation(), as 00536ddb..00536e61 computes them.
    /// The product keeps only each row's Y term (one fmul by 100 each); the yaw is -fpatan(X, Y); the elevation divides
    /// Z by FVector::Magnitude (004026b0: fsqrt((X*X + Y*Y) + Z*Z)) and takes the CRT's asin (_CIasin 0055dcb0 at the
    /// caller's 24-bit precision: fpatan(x, fsqrt((1 + x) * (1 - x)))), or 0 when the magnitude is not positive.
    /// </summary>
    internal static (float Yaw, float Pitch) SpawnDirection(FMatrix ori)
    {
        float x = ori.Row0.Y * 100.0f;
        float y = ori.Row1.Y * 100.0f;
        float z = ori.Row2.Y * 100.0f;
        float yaw = (float)-X87.Atan2(x, y);
        float magnitude = (float)X87.Sqrt((x * x) + (y * y) + (z * z));
        float pitch = 0.0f;
        if (magnitude > 0.0f)
        {
            double s = z / magnitude;
            pitch = (float)(s is > 1.0 or < -1.0
                ? double.NaN
                : X87.Atan2(s, X87.Sqrt(X87.Mul(X87.Add(1.0, s), X87.Sub(1.0, s)))));
        }

        return (yaw, pitch);
    }

    /// <summary>The emitter type a spawner emitter's name gives the new unit (stricmp): SpawnerA..E are 15..19, else 0.</summary>
    private static int SpawnerEmitterType(byte[] emitter)
    {
        string name = System.Text.Encoding.Latin1.GetString(emitter);
        string[] spawners = ["SpawnerA", "SpawnerB", "SpawnerC", "SpawnerD", "SpawnerE"];
        for (int i = 0; i < spawners.Length; i++)
        {
            if (string.Equals(name, spawners[i], StringComparison.OrdinalIgnoreCase))
            {
                return 15 + i;
            }
        }

        return 0;
    }

    /// <summary>PlayCharMessage 00537500: a message with no resume event, if there is a message box.</summary>
    public void PlayCharMessage(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        MessageBox? box = _runtime.MessageBox;
        if (box is null)
        {
            _runtime.Context.Skip("IScript::PlayCharMessage: no message box");
            return;
        }

        string? speaker = Text.GetString(unchecked((uint)args[0]!.GetInt()));
        string text = MessageText(args[1]!.GetInt());
        float time = (float)args[2]!.GetFloat();
        Thing? from = Thing!.IsA(ThingType.Unit) ? Thing : null;
        box.AddMessage(new ScriptMessage(speaker, text, args[1]!.GetInt(), time, from, null, 10));
    }

    /// <summary>
    /// PlayCharMessageWait 005375f0: the machine is saved and stopped, and a free event (RESUME, 0.05 s, this script, the
    /// saved machine) goes with the message; the box posts it when the message has been shown. With no free event, or
    /// no message box, the script never resumes (and the event is lost), as in retail.
    /// </summary>
    public void PlayCharMessageWait(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        string? speaker = Text.GetString(unchecked((uint)args[0]!.GetInt()));
        string text = MessageText(args[1]!.GetInt());
        float time = (float)args[2]!.GetFloat();

        ScriptVm vm = SuspendRunningVm();
        ScheduledEvent? done = _runtime.Context.Events.GetNextFreeEvent();
        if (done is null)
        {
            return;
        }

        done.Set(MessageResume, 0.05f, this, vm);
        Thing? from = Thing!.IsA(ThingType.Unit) ? Thing : null;
        var message = new ScriptMessage(speaker, text, args[1]!.GetInt(), time, from, done, 10);
        if (_runtime.MessageBox is { } box)
        {
            box.AddMessage(message);
        }
        else
        {
            _runtime.LogMessage("SHIT this should never happen");
            message.Delete();
        }
    }

    /// <summary>Print 00537ad0: a value of any type to the log (a thing as its number: retail prints its address).</summary>
    public void Print(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        ScriptData value = args[0]!;
        string line = value.Type switch
        {
            DataType.Int => value.GetInt().ToString(System.Globalization.CultureInfo.InvariantCulture),
            DataType.Float => value.GetFloat().ToString("F6", System.Globalization.CultureInfo.InvariantCulture),
            DataType.Bool => value.IsTrue() ? "TRUE" : "FALSE",
            DataType.String => System.Text.Encoding.Latin1.GetString(value.GetString()!),
            DataType.ThingPtr => $"thing {value.GetThing()}",
            DataType.Position => FormattableString.Invariant(
                $"x = {value.GetPosition().X:F4}, y = {value.GetPosition().Y:F4}, z = {value.GetPosition().Z:F4}"),
            _ => "unknown type",
        };
        _runtime.LogMessage(line);
    }

    /// <summary>Pause 00537c70: the machine is saved and stopped; it resumes after the time (absolute: now + time).</summary>
    public void Pause(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        float time = (float)args[0]!.GetFloat();
        ScriptVm vm = SuspendRunningVm();
        EventManager events = _runtime.Context.Events;
        events.AddEvent(MessageResume, this, events.Time + time, EventPriority.StartOfFrame, vm);
    }

    /// <summary>
    /// FollowWaypoint 00537d70: the thing heads for the named path's nearest point; when it is the script's own thing it
    /// follows the path, and Arrived gets the value at the end.
    /// </summary>
    public void FollowWaypoint(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        byte[] name = args[0]!.GetString()!;
        Waypoint? waypoint = Waypoints.FindNearest(name, Thing!.Pos);
        if (waypoint is null)
        {
            _runtime.LogMessage($"Cant find waypoint path '{System.Text.Encoding.Latin1.GetString(name)}'");
            return;
        }

        ArrivedValue = args[1]!.GetInt();
        ((ComplexThing)Thing).GoToPoint(waypoint.Pos);
        if (ReferenceEquals(Thing, DefaultThing))
        {
            Waypoint = waypoint;
            WaitingForWaypoints = false;
            if (!FollowingWaypoints)
            {
                FollowingWaypoints = true;
                _runtime.Context.Events.AddEvent(MessageFollowWaypoint, this, EventManager.NextFrame);
            }
        }
    }

    /// <summary>
    /// FollowWaypointWait 00537e40: as FollowWaypoint for the script's own thing, but the machine is saved and stopped
    /// until the path's end. Nothing happens while already following.
    /// </summary>
    public void FollowWaypointWait(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        if (FollowingWaypoints)
        {
            return;
        }

        byte[] name = args[0]!.GetString()!;
        Waypoint = Waypoints.FindNearest(name, Thing!.Pos);
        if (Waypoint is null)
        {
            _runtime.LogMessage($"FATAL ERROR: Cant find waypoint path '{System.Text.Encoding.Latin1.GetString(name)}'");
            return;
        }

        if (!ReferenceEquals(Thing, DefaultThing))
        {
            _runtime.LogMessage("FATAL ERROR:  Cant Follow waypoint way for other object");
            return;
        }

        DefaultThing.GoToPoint(Waypoint.Pos);
        WaitingForWaypoints = true;
        WaypointVm = SuspendRunningVm();
        FollowingWaypoints = true;
        _runtime.Context.Events.AddEvent(MessageFollowWaypoint, this, EventManager.NextFrame);
    }

    /// <summary>InJetMode 005380f0: a battle engine that is not walking.</summary>
    public void InJetMode(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        bool jet = false;
        if (Thing!.IsA(ThingType.BattleEngine))
        {
            jet = !ScriptThingCalls.IsWalking(_runtime, (BattleEngine)Thing);
        }

        result = new BoolData(jet);
    }

    /// <summary>LevelLostString 005381c0: the reason is a string id.</summary>
    public void LevelLostString(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        Game.DeclareLevelLost(args[0]!.GetInt());

    /// <summary>LevelWon 005381e0.</summary>
    public void LevelWon(ScriptData?[] args, int numArgs, ref ScriptData? result) => Game.DeclareLevelWon();

    /// <summary>SetSnowDensity 00538380: CAtmospherics::mSnowDensity.</summary>
    public void SetSnowDensity(ScriptData?[] args, int numArgs, ref ScriptData? result) =>
        _runtime.Context.Skip("IScript::SetSnowDensity: CAtmospherics::mSnowDensity");

    /// <summary>
    /// PostEvent 005383c0: the name reaches SCRIPT_EVENT_NB next frame (start of frame), carried by a CPostEventData.
    /// </summary>
    public void PostEvent(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
        var data = new PostEventData(args[0]!.Clone());
        _runtime.Context.Events.AddEvent(ScriptEventNB.PostEventNum, _runtime.EventNB, EventManager.NextFrame,
            EventPriority.StartOfFrame, data);
    }

    private ParityGame Game => _runtime.Context.Game
        ?? throw new NotSupportedException("a script native needs CGame (ThingContext.Game is not set)");

    private IGameText Text => _runtime.Text
        ?? throw new NotSupportedException("a message native needs TEXT (ScriptRuntime.Text is not set)");

    private WaypointManager Waypoints => _runtime.Waypoints
        ?? throw new NotSupportedException("a waypoint native needs the waypoint paths (ScriptRuntime.Waypoints is not set)");

    /// <summary>TEXT.GetString of a message's text; retail's wcslen would fault on a missing string.</summary>
    private string MessageText(int id) =>
        Text.GetString(unchecked((uint)id)) ?? throw new InvalidDataException($"no text for message string {id} (retail faults)");

    // ---- The rest of the table: not called by level 100's scripts ----

    /// <summary>SetSpeed: the shared no-op (00453ac0).</summary>
    public void SetSpeed(ScriptData?[] args, int numArgs, ref ScriptData? result)
    {
    }

    /// <summary>Rand (00538230): not called by level 100.</summary>
    public void Rand(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Rand");

    /// <summary>IsFriendly (00537fd0): not called by level 100.</summary>
    public void IsFriendly(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("IsFriendly");

    /// <summary>LevelLost (005381a0): not called by level 100.</summary>
    public void LevelLost(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("LevelLost");

    /// <summary>PlaySample (005381f0): not called by level 100.</summary>
    public void PlaySample(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlaySample");

    /// <summary>Die (00535cd0): not called by level 100.</summary>
    public void Die(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Die");

    /// <summary>PrintText (00537c40): not called by level 100.</summary>
    public void PrintText(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PrintText");

    /// <summary>AddMessage (00537410): not called by level 100.</summary>
    public void AddMessage(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("AddMessage");

    /// <summary>SetTimer (005358e0): not called by level 100.</summary>
    public void SetTimer(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetTimer");

    /// <summary>GetDistToObj (00536070): not called by level 100.</summary>
    public void GetDistToObj(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetDistToObj");

    /// <summary>SetVisible (00535ea0): not called by level 100.</summary>
    public void SetVisible(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetVisible");

    /// <summary>Teleport (005369b0): not called by level 100.</summary>
    public void Teleport(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Teleport");

    /// <summary>PlayCutscene (00535890): not called by level 100.</summary>
    public void PlayCutscene(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlayCutscene");

    /// <summary>SetVulnerable (00535f70): not called by level 100.</summary>
    public void SetVulnerable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetVulnerable");

    /// <summary>NumContained (00535d70): not called by level 100.</summary>
    public void NumContained(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("NumContained");

    /// <summary>GetWeaponAmmo (00535610): not called by level 100.</summary>
    public void GetWeaponAmmo(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetWeaponAmmo");

    /// <summary>GetWeaponName (00535670): not called by level 100.</summary>
    public void GetWeaponName(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetWeaponName");

    /// <summary>GetWeaponCharge (00535750): not called by level 100.</summary>
    public void GetWeaponCharge(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetWeaponCharge");

    /// <summary>GetNumUnits (00535590): not called by level 100.</summary>
    public void GetNumUnits(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetNumUnits");

    /// <summary>SetWindVector (00538300): not called by level 100.</summary>
    public void SetWindVector(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetWindVector");

    /// <summary>SetRainDensity (00538360): not called by level 100.</summary>
    public void SetRainDensity(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetRainDensity");

    /// <summary>SetLightningDensity (005383a0): not called by level 100.</summary>
    public void SetLightningDensity(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetLightningDensity");

    /// <summary>SetHealth (00535c10): not called by level 100.</summary>
    public void SetHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetHealth");

    /// <summary>GetRatioBattleLineNodes (005353a0): not called by level 100.</summary>
    public void GetRatioBattleLineNodes(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetRatioBattleLineNodes");

    /// <summary>GetPos (00536c00): not called by level 100.</summary>
    public void GetPos(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetPos");

    /// <summary>SpawnParticle (00536b70): not called by level 100.</summary>
    public void SpawnParticle(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SpawnParticle");

    /// <summary>SetSegmentHealth (00535480): not called by level 100.</summary>
    public void SetSegmentHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetSegmentHealth");

    /// <summary>SetAllSegmentsHealth (00535500): not called by level 100.</summary>
    public void SetAllSegmentsHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetAllSegmentsHealth");

    /// <summary>PlayAnimation (00535160): not called by level 100.</summary>
    public void PlayAnimation(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlayAnimation");

    /// <summary>PlayAnimationWait (005351d0): not called by level 100.</summary>
    public void PlayAnimationWait(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlayAnimationWait");

    /// <summary>GetMapHeight (00534ac0): not called by level 100.</summary>
    public void GetMapHeight(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetMapHeight");

    /// <summary>GetWaterHeight (00534b30): not called by level 100.</summary>
    public void GetWaterHeight(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetWaterHeight");

    /// <summary>GetX (00534b80): not called by level 100.</summary>
    public void GetX(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetX");

    /// <summary>GetY (00534c10): not called by level 100.</summary>
    public void GetY(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetY");

    /// <summary>GetZ (00534ca0): not called by level 100.</summary>
    public void GetZ(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetZ");

    /// <summary>SetX (00534d30): not called by level 100.</summary>
    public void SetX(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetX");

    /// <summary>SetY (00534dc0): not called by level 100.</summary>
    public void SetY(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetY");

    /// <summary>SetZ (00534e50): not called by level 100.</summary>
    public void SetZ(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetZ");

    /// <summary>SetGoalPoint (00534ee0): not called by level 100.</summary>
    public void SetGoalPoint(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetGoalPoint");

    /// <summary>GetSafePos (005350b0): not called by level 100.</summary>
    public void GetSafePos(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetSafePos");

    /// <summary>GetComponent (005349b0): not called by level 100.</summary>
    public void GetComponent(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetComponent");

    /// <summary>CreatePosition (00534910): not called by level 100.</summary>
    public void CreatePosition(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("CreatePosition");

    /// <summary>GetFloatRand (00538290): not called by level 100.</summary>
    public void GetFloatRand(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetFloatRand");

    /// <summary>SetVar (005348f0): not called by level 100.</summary>
    public void SetVar(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetVar");

    /// <summary>Damage (005348c0): not called by level 100.</summary>
    public void Damage(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Damage");

    /// <summary>GameTime (00534770): not called by level 100.</summary>
    public void GameTime(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GameTime");

    /// <summary>IsEnemy (00538060): not called by level 100.</summary>
    public void IsEnemy(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("IsEnemy");

    /// <summary>Land (005361d0): not called by level 100.</summary>
    public void Land(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Land");

    /// <summary>SpawnersEmpty (00535a90): not called by level 100.</summary>
    public void SpawnersEmpty(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SpawnersEmpty");

    /// <summary>Dive (005361f0): not called by level 100.</summary>
    public void Dive(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Dive");

    /// <summary>Surface (00536210): not called by level 100.</summary>
    public void Surface(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Surface");

    /// <summary>InitVariable (00536230): not called by level 100.</summary>
    public void InitVariable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("InitVariable");

    /// <summary>SetVariable (00536260): not called by level 100.</summary>
    public void SetVariable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetVariable");

    /// <summary>ShutdownVariable (00536330): not called by level 100.</summary>
    public void ShutdownVariable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("ShutdownVariable");

    /// <summary>Shutdown (00535d00): not called by level 100.</summary>
    public void Shutdown(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Shutdown");

    /// <summary>GetEnergy (00535bb0): not called by level 100.</summary>
    public void GetEnergy(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetEnergy");

    /// <summary>GetVariable (005362a0): not called by level 100.</summary>
    public void GetVariable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetVariable");

    /// <summary>SecondaryObjectiveComplete (00534410): not called by level 100.</summary>
    public void SecondaryObjectiveComplete(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SecondaryObjectiveComplete");

    /// <summary>SecondaryObjectiveFailed (00534470): not called by level 100.</summary>
    public void SecondaryObjectiveFailed(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SecondaryObjectiveFailed");

    /// <summary>GetAngle (00534680): not called by level 100.</summary>
    public void GetAngle(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetAngle");

    /// <summary>PlayPCharMessage (005377e0): not called by level 100.</summary>
    public void PlayPCharMessage(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlayPCharMessage");

    /// <summary>PlayPCharMessageWait (005378e0): not called by level 100.</summary>
    public void PlayPCharMessageWait(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("PlayPCharMessageWait");

    /// <summary>GetInitialHealth (00535a30): not called by level 100.</summary>
    public void GetInitialHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetInitialHealth");

    /// <summary>SpawnersInUse (00535af0): not called by level 100.</summary>
    public void SpawnersInUse(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SpawnersInUse");

    /// <summary>SetSpawnScript (00535ca0): not called by level 100.</summary>
    public void SetSpawnScript(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetSpawnScript");

    /// <summary>Deploy (00534f70): not called by level 100.</summary>
    public void Deploy(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Deploy");

    /// <summary>Undeploy (00534f90): not called by level 100.</summary>
    public void Undeploy(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Undeploy");

    /// <summary>GetSquad (005365c0): not called by level 100.</summary>
    public void GetSquad(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetSquad");

    /// <summary>GetTarget (005366c0): not called by level 100.</summary>
    public void GetTarget(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetTarget");

    /// <summary>Normalise (00534500): not called by level 100.</summary>
    public void Normalise(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Normalise");

    /// <summary>Magnitude (005345d0): not called by level 100.</summary>
    public void Magnitude(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Magnitude");

    /// <summary>IsFiring (00535b50): not called by level 100.</summary>
    public void IsFiring(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("IsFiring");

    /// <summary>IsNumberBetween (005347b0): not called by level 100.</summary>
    public void IsNumberBetween(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("IsNumberBetween");

    /// <summary>SetSegmentVulnerable (00534300): not called by level 100.</summary>
    public void SetSegmentVulnerable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetSegmentVulnerable");

    /// <summary>SetAllSegmentsVulnerable (00534390): not called by level 100.</summary>
    public void SetAllSegmentsVulnerable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetAllSegmentsVulnerable");

    /// <summary>GetRealHealth (005359d0): not called by level 100.</summary>
    public void GetRealHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetRealHealth");

    /// <summary>SwitchMessagesOn (005342c0): not called by level 100.</summary>
    public void SwitchMessagesOn(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SwitchMessagesOn");

    /// <summary>SwitchMessagesOff (005342e0): not called by level 100.</summary>
    public void SwitchMessagesOff(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SwitchMessagesOff");

    /// <summary>Goto3PointPanCamera (00533b70): not called by level 100.</summary>
    public void Goto3PointPanCamera(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Goto3PointPanCamera");

    /// <summary>Goto4PointPanCamera (00533eb0): not called by level 100.</summary>
    public void Goto4PointPanCamera(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Goto4PointPanCamera");

    /// <summary>GotoPlayerCamera (005342b0): not called by level 100.</summary>
    public void GotoPlayerCamera(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GotoPlayerCamera");

    /// <summary>GetConfiguration (005357b0): not called by level 100.</summary>
    public void GetConfiguration(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetConfiguration");

    /// <summary>SetGoodieState (00533a70): not called by level 100.</summary>
    public void SetGoodieState(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetGoodieState");

    /// <summary>GetGoodieState (00533aa0): not called by level 100.</summary>
    public void GetGoodieState(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetGoodieState");

    /// <summary>MPDeclarePlayerWon (00533a40): not called by level 100.</summary>
    public void MPDeclarePlayerWon(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("MPDeclarePlayerWon");

    /// <summary>MPDeclareGameDrawn (00533a60): not called by level 100.</summary>
    public void MPDeclareGameDrawn(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("MPDeclareGameDrawn");

    /// <summary>SetSlot (005338d0): not called by level 100.</summary>
    public void SetSlot(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetSlot");

    /// <summary>SetPlayerLives (005338a0): not called by level 100.</summary>
    public void SetPlayerLives(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetPlayerLives");

    /// <summary>Launch (005344a0): not called by level 100.</summary>
    public void Launch(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("Launch");

    /// <summary>HalfDestroy (00534370): not called by level 100.</summary>
    public void HalfDestroy(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("HalfDestroy");

    /// <summary>SetVelocity (00534340): not called by level 100.</summary>
    public void SetVelocity(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetVelocity");

    /// <summary>TriggerHitEffect (00536ca0): not called by level 100.</summary>
    public void TriggerHitEffect(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("TriggerHitEffect");

    /// <summary>GetNumber (00535980): not called by level 100.</summary>
    public void GetNumber(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("GetNumber");

    /// <summary>SpawnEscapePod (005371e0): not called by level 100.</summary>
    public void SpawnEscapePod(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SpawnEscapePod");

    /// <summary>ResetSegmentHealth (005354c0): not called by level 100.</summary>
    public void ResetSegmentHealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("ResetSegmentHealth");

    /// <summary>SetPos (00536c70): not called by level 100.</summary>
    public void SetPos(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetPos");

    /// <summary>SetLockable (00533950): not called by level 100.</summary>
    public void SetLockable(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetLockable");

    /// <summary>ToggleCockpit (00533980): not called by level 100.</summary>
    public void ToggleCockpit(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("ToggleCockpit");

    /// <summary>SetStealth (00535530): not called by level 100.</summary>
    public void SetStealth(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetStealth");

    /// <summary>TeleportOrientation (00536a60): not called by level 100.</summary>
    public void TeleportOrientation(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("TeleportOrientation");

    /// <summary>EnableSpawner (00535010): not called by level 100.</summary>
    public void EnableSpawner(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("EnableSpawner");

    /// <summary>DisableSpawner (00535040): not called by level 100.</summary>
    public void DisableSpawner(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("DisableSpawner");

    /// <summary>SetName (00535c70): not called by level 100.</summary>
    public void SetName(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("SetName");

    /// <summary>IsOverWater (00538150): not called by level 100.</summary>
    public void IsOverWater(ScriptData?[] args, int numArgs, ref ScriptData? result) => throw NotPorted("IsOverWater");

    private static NotSupportedException NotPorted(string name) => new($"IScript::{name} is not ported (level 100 does not call it)");
}
