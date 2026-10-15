// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// <c>IScript::FollowWaypoint</c> at <c>0x00537d70</c> on
/// specimen <c>74154bfa…</c>. Official <c>0x00537e20</c> is
/// <c>89 46 18</c> = <c>mov [esi+0x18], eax</c> after
/// <c>mov eax, 1</c> at <c>0x00537df7</c>. Isolated
/// <see cref="Level100ActorScriptCommandKind.FollowWaypoint"/>
/// names the rebuild command / path, not this store.
/// FollowWaypointWait / CVM / <c>+0x1c</c> stay unclaimed.
/// Mutation: increment so a second Start becomes 2. No new
/// secondaries.
/// </summary>
public sealed class RetailIScriptFollowWaypointTests
{
    /// <summary>
    /// <c>mov [esi+0x18], 1</c> writes literal 1. Isolated
    /// FollowWaypoint emit-command still passes if this
    /// store is skipped. Mutation: <c>return current + 1</c>.
    /// </summary>
    [Fact]
    public void Start_StoresLiteralOneAtIScriptPlus18NotIncrement()
    {
        Assert.Equal(0x00537d70, RetailIScriptFollowWaypoint.HandlerAddress);
        Assert.Equal(0x18, RetailIScriptFollowWaypoint.FlagOffset);
        Assert.Equal(1, RetailIScriptFollowWaypoint.FlagStarted);
        Assert.Equal(0, RetailIScriptFollowWaypoint.FlagIdle);
        Assert.Equal(1, RetailIScriptFollowWaypoint.Start(0));
        Assert.Equal(1, RetailIScriptFollowWaypoint.Start(1));
        Assert.NotEqual(2, RetailIScriptFollowWaypoint.Start(1));
        Assert.NotEqual(0, RetailIScriptFollowWaypoint.Start(0));
        Assert.Equal(1, RetailIScriptFollowWaypoint.Start(7));
    }

    /// <summary>
    /// AirborneDrone1 <c>ready()</c> is the one Level 100
    /// native-0 site. Isolated emit-command names the path
    /// and still passes if this store is skipped. Mutation:
    /// skip <see cref="RetailIScriptFollowWaypoint.Start"/>.
    /// </summary>
    [Fact]
    public void Native_FollowWaypointStoresLiteralOneAndIsolatedCommandDoesNot()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
        Level100ActorId airfield = actors.GetThingRef("Airfield")!.Value;
        var runtime = new Level100ActorScriptRuntime(actors, player);

        Assert.Equal(RetailIScriptFollowWaypoint.FlagIdle, runtime.FollowLoopFlag);

        Level100ActorId drone = actors.SpawnThing(
            airfield,
            "Target Drone",
            "SpawnerB",
            1,
            "AirborneDrone1").Single();
        Assert.Equal(
            RetailIScriptFollowWaypoint.FlagIdle,
            runtime.FollowLoopFlag);

        Assert.Equal(
            RetailIScriptFollowWaypoint.FlagStarted,
            runtime.InvokeFollowWaypointNative(drone, "Drone Path 1", 0));
        Assert.Equal(
            RetailIScriptFollowWaypoint.FlagStarted,
            runtime.FollowLoopFlag);
        Assert.Equal(
            RetailIScriptFollowWaypoint.Start(RetailIScriptFollowWaypoint.FlagIdle),
            runtime.FollowLoopFlag);
        Assert.NotEqual(
            2,
            runtime.InvokeFollowWaypointNative(drone, "Drone Path 1", 0));
    }
}
