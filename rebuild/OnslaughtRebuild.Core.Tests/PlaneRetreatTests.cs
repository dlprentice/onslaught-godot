// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// A plane leaves Level 100 as the U-17 does: its waypoint orders go through
/// the shared move order (slot 61, <c>0x00403a90</c>, the same body in the
/// <c>CPlane</c> vtable), it arrives on the follower's float test with radius
/// 5.0 (slot 94, <c>0x0050e8e0</c>), and <c>Retreat</c> (slot 100,
/// <c>0x004fdd00</c>), the run-out and the SHUTDOWN (slot 116,
/// <c>0x004fe5f0</c>) are the unit step's own (<c>0x004fa8d0</c>, reached
/// from the plane's Move <c>0x004d1cd0</c> through the air step). The ambient
/// Air Trainer's <c>Flyby</c> script and the dodge-exercise Air Trainer's
/// "Cease Trainer Attack" both call <c>Retreat()</c>.
/// </summary>
public sealed class PlaneRetreatTests
{
    private static readonly Lazy<Level100ActorDefinitionSet> s_level100 =
        new(Level100TestActorDefinitions.LoadMaterialized);

    private static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

    /// <summary>
    /// The ambient Air Trainer follows "Flyby Path" from its nearest node,
    /// 42 then 43, each order raised to MinAltitude above the floor; then it
    /// retreats to safe side row 20, runs out and leaves. The tick numbers
    /// are this build's determinism pins, not retail measurements.
    /// </summary>
    [Fact]
    public void FlybyTrainer_FliesItsPathRetreatsToSafeSide20AndLeaves()
    {
        var simulation = new Simulation(1u, s_level100.Value);
        WorldSnapshot state = simulation.Snapshot;
        Level100ActorId trainer = state.Level100Actors.Actors.Single(actor => actor.Name == "Air Trainer").ActorId;
        Level100WaypointPathDefinition path = s_level100.Value.GetWaypointPath("Flyby Path");
        var nodes = new List<int>();
        int? runOutTick = null, leftTick = null;
        Level100FloatVector3Bits retreatGoal = default;
        for (int tick = 1; tick <= 2_000 && leftTick is null; tick++)
        {
            state = simulation.Step(SimInput.Idle);
            Level100ActorCommandIntentSnapshot mechanics =
                state.Level100ActorMechanics.Actors.Single(item => item.ActorId == trainer);
            Level100PlaneGuideSnapshot guide = mechanics.PlaneGuide!;
            if (mechanics.WaypointNodeIndex is { } node && (nodes.Count == 0 || nodes[^1] != node))
            {
                nodes.Add(node);
                // The order to each node is the node's position through slot 61.
                Level100FloatVector4Bits seat = path.Point(node).RetailComponentsFloatBits;
                Assert.Equal(Level100ActorMechanics.ClampAirMoveTarget(
                        Level100Terrain.Instance, new(seat.X, seat.Y, seat.Z), 0x40800000),
                    guide.Destination);
            }

            if (guide.SpeedMode == 1)
            {
                retreatGoal = guide.Destination;
            }

            if (guide.SpeedMode == 2)
            {
                runOutTick ??= tick;
            }

            if (state.Level100Actors.Actors.Single(actor => actor.ActorId == trainer).Lifecycle ==
                Level100ActorLifecycle.Destroyed)
            {
                leftTick = tick;
            }
        }

        Assert.Equal([42, 43], nodes);
        Assert.Equal((Bits(501f), Bits(296f)), (retreatGoal.X, retreatGoal.Y));
        Assert.Equal(865, runOutTick);
        Assert.Equal(runOutTick + 2, leftTick);
        Assert.DoesNotContain(state.Level100ActorScripts.Instances, instance => instance.ActorId == trainer);
    }

    /// <summary>
    /// A leaving plane's AI only polls (<c>0x004ff340-0x004ff34f</c>): after
    /// <c>Retreat</c> it neither steers at its attack target nor fires, where
    /// the same attack without it fires; a second <c>Retreat</c> changes
    /// nothing.
    /// </summary>
    [Fact]
    public void LeavingPlane_NeitherSteersAtNorFiresOnItsTarget()
    {
        (Level100ActorMechanics Mechanics, Level100ActorId Trainer) Attack(bool retreat)
        {
            var actors = new Level100ActorRegistry(s_level100.Value);
            var mechanics = new Level100ActorMechanics(actors, s_level100.Value);
            Level100ActorId trainer = actors.GetThingRef("Air Trainer")!.Value;
            Level100ActorId player = actors.GetThingRef("Player 1")!.Value;
            // The player stands dead ahead on the trainer's straight way out
            // to its retreat point, row 39 (207, 503.5), about 40 units past
            // where the turn ends.
            actors.SetPose(player, actors.GetPose(player) with { PositionMillimeters = new(-69_950, 5_000, 206_070) });
            mechanics.ApplyCommand(new(1, 0, trainer, Level100ActorScriptCommandKind.Attack, player, null, 0));
            if (retreat)
            {
                mechanics.ApplyCommand(new(2, 0, trainer, Level100ActorScriptCommandKind.Retreat, null, null, 0));
            }

            return (mechanics, trainer);
        }

        // The first flush delivers each AI's construction 3000: the attacking
        // plane thinks and files its next 3000, the leaving one polls with
        // 3003.
        short FirstAiEvent(Level100ActorMechanics mechanics, Level100ActorId plane)
        {
            _ = mechanics.AdvanceTick();
            RetailEventSchedulerSnapshot events = mechanics.Snapshot.PlaneEvents!;
            var slots = events.Slots.ToDictionary(slot => slot.Handle);
            return Assert.Single(events.Lanes.SelectMany(lane => lane.Handles).Concat(events.Overflow),
                handle => slots[handle].Listener == Level100ActorMechanics.UnitAiListener(plane)) is var handle
                ? slots[handle].EventNum : (short)0;
        }

        (Level100ActorMechanics thinking, Level100ActorId thinker) = Attack(retreat: false);
        Assert.Equal(3000, FirstAiEvent(thinking, thinker));
        (Level100ActorMechanics polling, Level100ActorId poller) = Attack(retreat: true);
        Assert.Equal(3003, FirstAiEvent(polling, poller));

        (Level100ActorMechanics attacking, Level100ActorId attacker) = Attack(retreat: false);
        int firstRound = -1;
        for (int tick = 0; tick < 2_000 && firstRound < 0; tick++)
        {
            _ = attacking.AdvanceTick();
            if (attacking.Snapshot.ActorRounds.Any(round => round.OwnerActorId == attacker)) firstRound = tick;
        }
        Assert.InRange(firstRound, 0, 1_999);

        (Level100ActorMechanics leaving, Level100ActorId trainer) = Attack(retreat: true);
        Level100PlaneGuideSnapshot retreating =
            Assert.Single(leaving.Snapshot.Actors, item => item.ActorId == trainer).PlaneGuide!;
        Assert.Equal((1, 1), (retreating.Mode, retreating.SpeedMode));
        leaving.ApplyCommand(new(3, 0, trainer, Level100ActorScriptCommandKind.Retreat, null, null, 0));
        Assert.Equal(retreating,
            Assert.Single(leaving.Snapshot.Actors, item => item.ActorId == trainer).PlaneGuide!);
        // Until it leaves: on its way out it passes the player, in range and
        // ahead, where the attacking control fires.
        for (int tick = 0; tick < 2_000; tick++)
        {
            _ = leaving.AdvanceTick();
            // Heading for the retreat point until the run-out's own order.
            Level100PlaneGuideSnapshot guide =
                Assert.Single(leaving.Snapshot.Actors, item => item.ActorId == trainer).PlaneGuide!;
            if (guide.SpeedMode == 1) Assert.Equal(retreating.Destination, guide.Destination);
            Assert.DoesNotContain(leaving.Snapshot.ActorRounds, round => round.OwnerActorId == trainer);
        }
    }
}
