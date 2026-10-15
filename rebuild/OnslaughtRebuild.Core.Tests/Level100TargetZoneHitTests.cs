// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// TargetZone2/3/4 <c>hit()</c> is the training path that already
/// compiles <c>other_thing.InJetMode() == FALSE</c> then
/// <c>Pause(0.5)</c>. Isolated
/// <see cref="RetailIScriptInJetMode.Evaluate"/> names type-8 +
/// recency without going through that object. Isolated
/// <see cref="Level100MissionTiming.JetModeState"/> names the
/// Simulation pre-filter. Isolated
/// <see cref="RetailIScriptWaitStop.Stop"/> names
/// LevelScript <c>PlayCharMessageWait</c>, not this actor
/// Pause. Mutation: keep native 125 on <c>mode == Jet</c> so
/// an airborne walker posts Reached, or increment the wait-stop
/// flag so one Pause becomes 2. CVM snapshot / 0.05f /
/// FollowWaypointWait stay unclaimed. Noticeboard stays
/// unclaimed. Live <c>GAME.mSlots</c> stay unclaimed. No new
/// secondaries.
/// </summary>
public sealed class Level100TargetZoneHitTests
{
    /// <summary>
    /// Official <c>TargetZone2.msl</c> <c>hit()</c> SET_CONTEXT
    /// the hitter, requires <c>IsA(8)</c>, requires
    /// <c>InJetMode() == FALSE</c>, then <c>Pause(0.5)</c> and
    /// <c>PostEvent("Reached Target Zone 2")</c>. Isolated
    /// <see cref="RetailIScriptInJetModeTests.Native_AirborneWalkerBattleEngineIsTrueAndNonBattleEngineIsFalse"/>
    /// names Evaluate and does not post the event. Isolated
    /// <see cref="RetailIScriptWaitStopTests.Stop_StoresLiteralOneAtCvmSingletonPlus220NotIncrement"/>
    /// names LevelScript waits. Mutation: skip the actor Pause
    /// store, or treat an airborne walker as not in jet mode.
    /// </summary>
    [Fact]
    public void TargetZone2_Hit_PostsReachedOnlyForARecentlyGroundedBattleEngineAndStoresWaitStop()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        var actors = new Level100ActorRegistry(definitions);
        Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
        Level100ActorId zone = actors.GetThingRef("Target Zone 2")!.Value;
        var runtime = new Level100ActorScriptRuntime(actors, player);
        runtime.InitializeReleasedScripts();
        actors.SetObjective(zone, true);
        actors.Activate(zone);
        Assert.Equal(RetailSetObjective.MarkedBit, actors.FlagWord(zone));
        Assert.Equal(RetailSetObjective.Mark(0), actors.FlagWord(zone));
        Assert.NotEqual(0, actors.FlagWord(zone));
        int flagAfterInit = runtime.WaitStopFlag;

        runtime.SetPlayerFlightState(
            VehicleMode.Walker,
            VehicleTransition.None,
            10);
        runtime.DispatchFact(TriggerHit(zone));
        Assert.Empty(ReachedEvents(runtime));
        Assert.Equal(flagAfterInit, runtime.WaitStopFlag);

        runtime.SetPlayerFlightState(
            VehicleMode.Walker,
            VehicleTransition.None,
            0);
        runtime.DispatchFact(TriggerHit(zone));
        Assert.Empty(ReachedEvents(runtime));
        Assert.Equal(
            RetailIScriptWaitStop.FlagStopped,
            runtime.WaitStopFlag);
        Assert.Equal(
            RetailIScriptWaitStop.Stop(flagAfterInit),
            runtime.WaitStopFlag);
        Assert.NotEqual(flagAfterInit + 1, runtime.WaitStopFlag);

        for (int tick = 0; tick < Level100MissionTiming.PauseTicks(0.5f); tick++)
        {
            Assert.Empty(ReachedEvents(runtime));
            runtime.AdvanceTick();
        }

        Assert.Equal(
            ["Reached Target Zone 2"],
            ReachedEvents(runtime).Select(item => item.EventName).ToArray());
        Assert.Equal(0, actors.FlagWord(zone));
        Assert.Equal(
            RetailSetObjective.Unmark(RetailSetObjective.MarkedBit),
            actors.FlagWord(zone));
        Assert.Equal(RetailIScriptWaitStop.FlagStopped, runtime.WaitStopFlag);
        Assert.NotEqual(
            2,
            runtime.WaitStopFlag);
        Assert.All(
            RetailFillOutEndLevelData.ForLevel100Won().SecondaryStatuses,
            status => Assert.Equal(0, status));
    }

    /// <summary>
    /// Simulation must consult the same Evaluate the compiled
    /// <c>hit()</c> calls, on the flight state from this tick's
    /// movement, and Reached must come from that object after
    /// Pause — not from <c>TriggerEntered</c> alone. Isolated
    /// <see cref="Level100MissionTiming.JetModeState"/> still
    /// passes if native 125 is left on <c>mode == Jet</c>.
    /// Mutation: keep TriggerEntered as success, or skip the
    /// post-movement sync so a landing tick sees last tick's
    /// airborne recency and never posts.
    /// </summary>
    [Fact]
    public void TargetZone2_FallInAndLand_PostsReachedFromHitNotFromTriggerEntered()
    {
        var simulation = new Simulation(
            1u,
            Level100TestActorDefinitions.Create(),
            new Level100TutorialProgress(true, true, true, true));
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        simulation.GrantFlightLegForMeasurement(Level100MissionTrigger.TargetZone2);
        Level100ActorSnapshot zone = simulation.Snapshot.Level100Actors.Actors
            .Single(actor => actor.Trigger == Level100MissionTrigger.TargetZone2);
        SimVector3 pose = zone.Pose!.PositionMillimeters;
        simulation.SetAirborneWalkerContactStateForMeasurement(
            new SimVector3(pose.X, pose.Y + 20_000, pose.Z),
            SimVector3.Zero);

        bool posted = false;
        bool enteredBeforePost = false;
        for (int tick = 0; tick < 800; tick++)
        {
            WorldSnapshot state = simulation.Step(SimInput.Idle);
            Level100ActorSnapshot live = state.Level100Actors.Actors
                .Single(actor => actor.Trigger == Level100MissionTrigger.TargetZone2);
            bool reached = state.Level100MissionEvents
                .OfType<Level100MissionEventPosted>()
                .Any(item => item.EventName == "Reached Target Zone 2");
            if (live.TriggerEntered && !live.TriggerEventDispatched && !posted)
            {
                enteredBeforePost = true;
            }

            if (reached || live.TriggerEventDispatched)
            {
                posted = true;
                Assert.True(state.PlayerOnGround);
                Assert.Equal(VehicleMode.Walker, state.Mode);
                Assert.True(live.TriggerEventDispatched);
                break;
            }
        }

        Assert.True(posted, "TargetZone2 hit() never posted Reached after landing.");
        Assert.False(
            enteredBeforePost,
            "TriggerEntered became success before hit() Pause posted Reached.");
    }

    private static Level100ActorFactSnapshot TriggerHit(Level100ActorId zone) =>
        new(1, Level100ActorFactKind.TriggerDispatchReady, zone, null, 0);

    private static List<Level100ActorScriptEventPosted> ReachedEvents(
        Level100ActorScriptRuntime runtime) =>
        runtime.DrainPostedEvents()
            .Where(item => item.EventName == "Reached Target Zone 2")
            .ToList();
}
