// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::InJetMode</c> at <c>0x005380f0</c> on specimen
/// <c>74154bfa…</c>. Official file <c>0x001380f0</c> is
/// <c>8b 49 10 56 33 f6 f6 41 34 08 … be 01 00 00 00</c>.
/// Callee <c>0x00408120</c> is true only for walker state 2
/// whose last ground contact is inside <c>0x005D85EC</c> =
/// 0.5f. The wrapper negates that, so a recently-grounded
/// Battle Engine is FALSE. Isolated
/// <see cref="Level100MissionTiming.JetModeState"/> names
/// the recency predicate and still passes if this type gate
/// is skipped. Isolated
/// <c>PlayerInJetMode == (mode == Jet)</c> names the rebuild
/// bool and still passes if the native keeps that bool.
/// TargetZone2/3/4 <c>hit()</c> SET_CONTEXT the hitter then
/// call this native. Mutation: skip
/// <c>test [ecx+0x34], 8</c> so a non-BE jet is TRUE, or
/// keep <c>mode == Jet</c> so an airborne walker is FALSE.
/// Lock-set / Move / Morph / UpdateCamera stay unclaimed.
/// Live <c>GAME.mSlots</c> stay unclaimed. No new secondaries.
/// </summary>
public sealed class RetailIScriptInJetModeTests
{
    /// <summary>
    /// <c>test byte [ecx+0x34], 8</c> then negate
    /// <c>0x00408120</c>. Isolated
    /// <see cref="Level100MissionTiming.JetModeState"/> still
    /// returns InJetMode for a non-BE jet. Mutation: omit the
    /// type gate.
    /// </summary>
    [Fact]
    public void Evaluate_IsFalseUnlessBattleEngineBit8AndNotARecentlyGroundedWalker()
    {
        Assert.Equal(0x005380f0, RetailIScriptInJetMode.HandlerAddress);
        Assert.Equal(0x00408120, RetailIScriptInJetMode.RecentlyGroundedWalkerAddress);
        Assert.Equal(
            8u,
            RetailIScriptInJetMode.BattleEngineTypeBit);
        Assert.Equal(
            Level100ReleasedThingTypeMasks.BattleEngine,
            RetailIScriptInJetMode.BattleEngineTypeBit);

        Assert.False(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.BattleEngine,
                VehicleMode.Walker,
                VehicleTransition.None,
                0));
        Assert.False(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.BattleEngine,
                VehicleMode.Walker,
                VehicleTransition.None,
                9));
        Assert.True(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.BattleEngine,
                VehicleMode.Walker,
                VehicleTransition.None,
                10));
        Assert.True(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.BattleEngine,
                VehicleMode.Jet,
                VehicleTransition.None,
                0));
        Assert.True(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.BattleEngine,
                VehicleMode.Walker,
                VehicleTransition.WalkerToJet,
                0));

        Assert.False(
            RetailIScriptInJetMode.Evaluate(
                0,
                VehicleMode.Jet,
                VehicleTransition.None,
                0));
        Assert.False(
            RetailIScriptInJetMode.Evaluate(
                Level100ReleasedThingTypeMasks.Ammunition,
                VehicleMode.Jet,
                VehicleTransition.None,
                0));
        Assert.NotEqual(
            Level100MissionTiming.JetModeState(
                VehicleMode.Jet,
                VehicleTransition.None,
                0) == Level100MissionJetModeState.InJetMode,
            RetailIScriptInJetMode.Evaluate(
                0,
                VehicleMode.Jet,
                VehicleTransition.None,
                0));
    }

    /// <summary>
    /// TargetZone2/3/4 <c>hit()</c> SET_CONTEXT the hitter then
    /// call native 125. Isolated
    /// <see cref="Level100ActorScriptRuntimeSnapshot.PlayerInJetMode"/>
    /// stays <c>mode == Jet</c>. Isolated
    /// <see cref="Evaluate_IsFalseUnlessBattleEngineBit8AndNotARecentlyGroundedWalker"/>
    /// names Evaluate and does not go through the native. Mutation:
    /// keep the native on <c>mode == Jet</c> so an airborne walker
    /// stays FALSE.
    /// </summary>
    [Fact]
    public void Native_AirborneWalkerBattleEngineIsTrueAndNonBattleEngineIsFalse()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
        Level100ActorId turret = actors.GetThingRef("Turret 01")!.Value;
        var runtime = new Level100ActorScriptRuntime(actors, player);

        runtime.SetPlayerFlightState(
            VehicleMode.Walker,
            VehicleTransition.None,
            10);
        Assert.False(runtime.Snapshot.PlayerInJetMode);
        Assert.True(runtime.InvokeInJetModeNative(player));
        Assert.False(runtime.InvokeInJetModeNative(turret));

        runtime.SetPlayerFlightState(
            VehicleMode.Walker,
            VehicleTransition.None,
            0);
        Assert.False(runtime.InvokeInJetModeNative(player));

        runtime.SetPlayerFlightState(
            VehicleMode.Jet,
            VehicleTransition.None,
            0);
        Assert.True(runtime.Snapshot.PlayerInJetMode);
        Assert.True(runtime.InvokeInJetModeNative(player));
        Assert.False(runtime.InvokeInJetModeNative(turret));
    }
}
