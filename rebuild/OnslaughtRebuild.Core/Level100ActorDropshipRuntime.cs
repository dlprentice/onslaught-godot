// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// A living <c>CDropship</c> in flight: its Actor MOVE, move orders, retreat,
/// run-out and shutdown, from the pristine specimen (<c>74154bfa…7750</c>)
/// and the RE lane's contracts
/// (<c>reverse-engineering/game-mechanics/dropship-flight.md</c> and
/// <c>dropship-landing.md</c>). The craft keeps the plane's raw air state and
/// guide record: the guide's goal and mode, and its <c>+0x244</c> leave state
/// as <see cref="Level100PlaneGuideSnapshot.SpeedMode"/>. Landing (states
/// 2-7), dying flight and water entry are not admitted yet.
/// </summary>
public sealed partial class Level100ActorMechanics
{
    /// <summary>Settles the facts a callback reported, inside the flush; set for one tick.</summary>
    private Action? _settleFacts;

    private bool IsDropship(Level100ActorId actorId) =>
        Level100ConstructionClasses.Of(_actors.GetActor(actorId).DefinitionName) ==
            Level100ConstructionClass.Dropship;

    /// <summary>
    /// A dropship's construction after its Actor draw: <c>CActor::Init</c>
    /// files its MOVE for the next frame (multiplier 1), and the guide starts in
    /// mode 0 with its goal at the craft (<c>0x0047e290</c>).
    /// <c>CDropship::Init</c>'s landing state comes from the ground under the
    /// craft (<c>0x00446e14-0x00446e21</c>): every admitted craft starts in the
    /// air, in state 0.
    /// </summary>
    private void RegisterDropship(RetailEventScheduler events, Level100ActorId actorId)
    {
        ThingActorBaseStateSnapshot physical = _actors.GetBaseState(actorId);
        Level100FloatVector3Bits position = physical.RetailPoses?.Current.PositionFloatBits ??
            throw new InvalidOperationException("A dropship has no raw air state.");
        events.AddEvent(3000, PlaneListener(actorId, 0), RetailEventScheduler.NextFrame);
        Level100FloatVector3Bits authored = _definitions.GetActorDefinition(
            _actors.GetActor(actorId).DefinitionIdentity).AuthoredTransform.RetailPositionFloatBits;
        if (!(RetailWorldTerrain.SampleRetailHeight(_actors.Terrain, position) >
              BitConverter.Int32BitsToSingle(authored.Z)))
        {
            throw new NotSupportedException("A dropship that starts on the ground (state 6) is not admitted yet.");
        }

        if (!_states.TryGetValue(actorId.Value, out ActorState? state))
        {
            state = new ActorState
            {
                ActorId = actorId,
                Intent = Level100ActorCommandIntent.Stopped,
                Allegiance = _actors.GetAuthoredAllegiance(actorId),
            };
            _states.Add(actorId.Value, state);
        }

        state.PlaneGuide = new(position, 0, 0, 0, 0, 1, 0);
    }

    /// <summary>
    /// The Actor's MOVE (<c>0x00401ae6</c>): <c>CDropship::Move</c>, then the
    /// next MOVE for the next frame unless the thing declared its shutdown.
    /// </summary>
    private void DispatchDropshipMove(RetailEventScheduler events, RetailEventDispatch dispatch,
        ActorState state, Level100ActorSnapshot actor)
    {
        if (actor.Lifecycle != Level100ActorLifecycle.Alive)
        {
            throw new NotSupportedException("A dying dropship's flight is not admitted yet.");
        }

        MoveDropship(events, state, actor);
        if (!_actors.GetBaseState(actor.ActorId).IsShuttingDown)
        {
            events.AddEvent(3000, dispatch.Listener, RetailEventScheduler.NextFrame, reuseHandle: dispatch.Handle);
        }
    }

    /// <summary>
    /// <c>CDropship::Move</c> (<c>0x00447120</c>) for a living craft in landing
    /// state 0: the air step (<c>0x00402fa0</c>) and its unit step
    /// (<c>0x004fa8d0</c>). The state-0 arm only clears the door progress.
    /// </summary>
    private void MoveDropship(RetailEventScheduler events, ActorState state, Level100ActorSnapshot actor)
    {
        if (state.DropshipLandingState != 0)
        {
            throw new NotSupportedException("A landing dropship's Move is not admitted yet.");
        }

        Level100ActorMotionDefinition motion = _definitions.GetMotionDefinition(actor.DefinitionName!);
        ThingActorBaseState physical = _actors.GetPlaneState(actor.ActorId);
        ThingActorBaseStateSnapshot before = physical.Snapshot;
        RetailPlaneMotionSnapshot air = before.RetailPlane!;
        RetailActorPoseSnapshot pose = before.RetailPoses!.Current;
        Level100PlaneGuideSnapshot guide = state.PlaneGuide!;
        Level100Terrain terrain = _actors.Terrain;
        float water = terrain.WaterLevel;

        // Drive, no gravity (slot 45, 0x00448360), damping, then the cap.
        int damping = RetailDropshipMotion.SelectDamping(motion.Big == true,
            motion.MeshRadiusFloatBits ?? 0, pose.PositionFloatBits.Z, water);
        Level100FloatVector3Bits velocity = RetailPlaneMotion.IntegrateAirVelocity(
            air.Velocity, air.Drive, damping, motion.AirVelocityFloatBits!.Value, guide.SpeedMode);

        // The guide runs while the unit is active (+0x214), from the pose
        // before the move; the floor is the higher of ground and water.
        RetailPlaneGuideOutput? next = null;
        if (actor.Active)
        {
            float ground = RetailWorldTerrain.SampleRetailHeight(terrain, pose.PositionFloatBits);
            next = RetailDropshipMotion.UpdateCruise(pose, air.CurrentEuler, guide.Destination, guide.Mode,
                ground > water ? water : ground, motion.MinimumAltitudeFloatBits!.Value);
        }

        // CActor::Move (0x004015e0): translate; a living craft at or below the
        // ground sits on it and stops (slot 68, 0x004dfc60). Slot 48 is 0.0.
        Level100FloatVector3Bits position = RetailPlaneMotion.Translate(pose.PositionFloatBits, velocity);
        float groundBelow = RetailWorldTerrain.SampleRetailHeight(terrain, position);
        if (!(groundBelow > BitConverter.Int32BitsToSingle(position.Z)))
        {
            position = position with { Z = BitConverter.SingleToInt32Bits(groundBelow) };
            velocity = default;
        }

        if (!(water > BitConverter.Int32BitsToSingle(position.Z)))
        {
            throw new NotSupportedException("A dropship's water entry (slot 69, 0x00402010) is not admitted yet.");
        }

        // The Euler step (slot 77, 0x004fa4b0) while active, skipped when every
        // target equals its angle.
        Level100FloatVector3Bits desired = next?.DesiredEuler ?? air.DesiredEuler;
        Level100FloatVector3Bits euler = air.CurrentEuler;
        Level100FloatBasis3Bits basis = pose.BasisFloatBits;
        if (actor.Active && !SameAngles(euler, desired))
        {
            euler = RetailUnitEuler.Smooth(euler, desired, air.EulerRates, 1f);
            basis = RetailUnitEuler.BuildBasis(euler);
        }

        physical.CommitRetailPlaneMove(new(position, basis), air with
        {
            Velocity = velocity,
            Drive = next?.Drive ?? air.Drive,
            CurrentEuler = euler,
            DesiredEuler = desired,
            BankFlagFloatBits = next?.BankFlagFloatBits ?? air.BankFlagFloatBits,
        }, BitConverter.SingleToInt32Bits(events.Time));

        AdvanceLeaveState(events, state, new(position, basis));

        static bool SameAngles(Level100FloatVector3Bits left, Level100FloatVector3Bits right) =>
            BitConverter.Int32BitsToSingle(left.X) == BitConverter.Int32BitsToSingle(right.X) &&
            BitConverter.Int32BitsToSingle(left.Y) == BitConverter.Int32BitsToSingle(right.Y) &&
            BitConverter.Int32BitsToSingle(left.Z) == BitConverter.Int32BitsToSingle(right.Z);
    }

    /// <summary>
    /// The unit step's leave states (<c>0x004fb053-0x004fb135</c>), shared by
    /// the plane and the dropship, from the moved pose: within 4.0 of the
    /// retreat point the unit runs out 300 along its nose, and while running
    /// out it posts its own SHUTDOWN for the next frame (slot 116,
    /// <c>0x004fe5f0</c>).
    /// </summary>
    private void AdvanceLeaveState(RetailEventScheduler events, ActorState state, RetailActorPoseSnapshot moved)
    {
        Level100PlaneGuideSnapshot guide = state.PlaneGuide!;
        if (guide.SpeedMode == 1)
        {
            if (RetailDropshipMotion.ReachedRunOut(moved.PositionFloatBits, guide.Destination))
            {
                state.PlaneGuide = guide with { SpeedMode = 2 };
                IssueAirMoveOrder(state, RetailDropshipMotion.RunOutTarget(moved), force: false);
            }
        }
        else if (guide.SpeedMode == 2)
        {
            events.AddEvent(2000, UnitListener(state.ActorId, UnitCallbackOwner.Unit), RetailEventScheduler.NextFrame);
        }
    }

    /// <summary>
    /// An air unit's move order, slot 61 (<c>0x00403a90</c>), which the plane
    /// and the dropship share, then guide slot 4 (<c>0x0047e2d0</c>): an
    /// unforced order is ignored while the AI's mode (<c>+0x20</c>) is 2, a
    /// plane leaving its spawner; otherwise the guide takes mode 1 and the
    /// goal.
    /// </summary>
    private void IssueAirMoveOrder(ActorState state, Level100FloatVector3Bits target, bool force)
    {
        Level100PlaneGuideSnapshot guide = state.PlaneGuide ??
            throw new InvalidOperationException("A move order needs an air guide.");
        Level100FloatVector3Bits goal = ClampAirMoveTarget(_actors.Terrain, target,
            _definitions.GetMotionDefinition(_actors.GetActor(state.ActorId).DefinitionName!)
                .MinimumAltitudeFloatBits ?? MinimumAltitudeDefaultFloatBits);
        if (!force && guide.ControllerState == 2)
        {
            return;
        }

        state.PlaneGuide = guide with { Mode = 1, Destination = goal };
    }

    /// <summary>The profile constructor's MinAltitude, 4.0 (<c>0x0042f0e9</c>).</summary>
    private const int MinimumAltitudeDefaultFloatBits = 0x40800000;

    /// <summary>
    /// Slot 61's target (<c>0x00403aa4-0x00403b19</c>): x and y rounded to
    /// integers (<c>fistp</c>, nearest even), the integer height grid there
    /// (<c>0x0047ea20</c>) times the height scale, the higher of that and the
    /// water, and the target raised to MinAltitude above it.
    /// </summary>
    internal static Level100FloatVector3Bits ClampAirMoveTarget(Level100Terrain terrain,
        Level100FloatVector3Bits target, int minimumAltitudeFloatBits)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        int x = checked((int)Math.Round(BitConverter.Int32BitsToSingle(target.X), MidpointRounding.ToEven));
        int y = checked((int)Math.Round(BitConverter.Int32BitsToSingle(target.Y), MidpointRounding.ToEven));
        double ground = RetailFloat24.Multiply(terrain.SampleAirGuideHeightUnits(x, y), terrain.HeightScale);
        double floor = ground > terrain.WaterLevel ? terrain.WaterLevel : ground;
        double limit = RetailFloat24.Subtract(floor, BitConverter.Int32BitsToSingle(minimumAltitudeFloatBits));
        return BitConverter.Int32BitsToSingle(target.Z) > limit
            ? target with { Z = BitConverter.SingleToInt32Bits((float)limit) }
            : target;
    }

    /// <summary>
    /// <c>CUnit::Retreat</c> (slot 100, <c>0x004fdd00</c> for the plane and
    /// the dropship, and the <c>Retreat()</c> native): nothing while already
    /// leaving (<c>+0x244</c> is 1 or 2); otherwise an unforced move order to
    /// the retreat point, then <c>+0x244</c> = 1. A leaving unit's AI only
    /// polls, so it no longer steers at or fires on a target.
    /// </summary>
    private void RetreatAirUnit(ActorState state)
    {
        if (state.PlaneGuide!.SpeedMode is 1 or 2)
        {
            return;
        }

        IssueAirMoveOrder(state, RetreatPoint(state), force: false);
        state.PlaneGuide = state.PlaneGuide! with { SpeedMode = 1 };
    }

    /// <summary>
    /// The retreat point (<c>0x004fd910</c>): the world origin, or the
    /// <c>CSafeSide</c> of the unit's own side (slot 66, <c>0x004bfc10</c>,
    /// against <c>+0x138</c>) strictly nearer in 2D than the best so far. The
    /// list at <c>0x00855160</c> is built by prepending (<c>0x004e5a80</c>),
    /// so it is walked newest first, and a tie keeps the newer.
    /// </summary>
    private Level100FloatVector3Bits RetreatPoint(ActorState state) =>
        RetreatPoint(_actors.GetBaseState(state.ActorId).RetailPoses!.Current.PositionFloatBits,
            state.Allegiance, _definitions.SafeSides);

    /// <param name="safeSides">The world's safe sides in construction order.</param>
    internal static Level100FloatVector3Bits RetreatPoint(Level100FloatVector3Bits unit, int allegiance,
        IReadOnlyList<Level100SafeSideDefinition> safeSides)
    {
        double ux = BitConverter.Int32BitsToSingle(unit.X), uy = BitConverter.Int32BitsToSingle(unit.Y);
        Level100FloatVector3Bits best = default;
        foreach (Level100SafeSideDefinition safeSide in safeSides.Reverse())
        {
            if (safeSide.Allegiance != allegiance)
            {
                continue;
            }

            double bestX = RetailFloat24.Subtract(ux, BitConverter.Int32BitsToSingle(best.X));
            double bestY = RetailFloat24.Subtract(uy, BitConverter.Int32BitsToSingle(best.Y));
            double current = RetailFloat24.Sqrt(RetailFloat24.Add(
                RetailFloat24.Multiply(bestY, bestY), RetailFloat24.Multiply(bestX, bestX)));
            Level100FloatVector3Bits candidate = safeSide.RetailPositionFloatBits;
            double x = (float)RetailFloat24.Subtract(ux, BitConverter.Int32BitsToSingle(candidate.X));
            double y = (float)RetailFloat24.Subtract(uy, BitConverter.Int32BitsToSingle(candidate.Y));
            if (RetailFloat24.Sqrt(RetailFloat24.Add(RetailFloat24.Multiply(y, y), RetailFloat24.Multiply(x, x))) <
                current)
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// A unit's SHUTDOWN (2000) as its run-out posts it: its script's
    /// <c>shutdown()</c> and its removal
    /// (<see cref="Level100ActorRegistry.ReportShutdown"/>); a living parent's
    /// component children shut down with it (<c>CUnit::Shutdown</c>,
    /// <c>0x004f9641-0x004f968a</c>, child slot 2).
    /// </summary>
    private void ShutDownUnit(Level100ActorId actorId)
    {
        if (!_actors.ReportShutdown(actorId))
        {
            return;
        }

        string parent = _actors.GetActor(actorId).DefinitionIdentity;
        foreach (Level100ComponentDefinition component in _definitions.Components.Where(item =>
                     StringComparer.Ordinal.Equals(item.ParentIdentity, parent)))
        {
            Level100ActorId childId = _actors.Snapshot.Actors.Single(item =>
                StringComparer.Ordinal.Equals(item.DefinitionIdentity, component.ChildIdentity)).ActorId;
            _ = _actors.ReportShutdown(childId);
        }

        _settleFacts?.Invoke();
    }
}
