// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// Beats 6, 8 and 10 of the released Level 100 tutorial — "fly to Target Zone N
/// and land there" — played rather than scripted. The driver here posts no
/// mission event at all; it presses transform, steers, throttles and holds the
/// landing thrusters, and the "Reached Target Zone N" event has to come out of
/// the world exactly as <c>TargetZoneN.msl</c>'s <c>hit()</c> produces it,
/// including that script's <c>InJetMode() == FALSE</c> test.
/// </summary>
public sealed class Level100FlightLegTests
{
    private readonly ITestOutputHelper _output;

    public Level100FlightLegTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The three released flight legs flown back to back from the authored
    /// start, in the order the LevelScript sets them: Target Zone 2, then 3,
    /// then 4. Each leg begins wherever the previous one ended, so the run is
    /// the same continuous flight a player makes.
    /// <para>
    /// Each leg must also be completed <em>on the ground</em>. The released
    /// <c>TargetZoneN.msl</c> <c>hit()</c> is a thing-to-thing contact, not a
    /// map-plane test, and the script teaches HELP_RETRO — the landing
    /// thrusters — immediately before the first of these legs. Until the
    /// trigger test was made three-dimensional every one of the three legs
    /// completed at 11–13 m up, in level flight, having never landed.
    /// </para>
    /// </summary>
    [Fact]
    public void ThreeFlightLegs_AreFlownAndLandedByInputAlone()
    {
        var driver = Level100FlightLegDriver.Create();
        Level100MissionTrigger[] legs =
        [
            Level100MissionTrigger.TargetZone2,
            Level100MissionTrigger.TargetZone3,
            Level100MissionTrigger.TargetZone4,
        ];

        try
        {
            foreach (Level100MissionTrigger leg in legs)
            {
                Assert.True(
                    driver.FlyLeg(leg, 6_000),
                    $"{leg} was not reached by input alone.");
                WorldSnapshot arrival = driver.ArrivalState!;
                Assert.True(
                    arrival.PlayerOnGround,
                    $"{leg} completed while airborne at " +
                    $"{arrival.PlayerElevationMillimeters - arrival.PlayerGroundElevationMillimeters} mm.");
                Assert.Equal(VehicleMode.Walker, arrival.Mode);
            }
        }
        finally
        {
            foreach (string line in driver.Report)
            {
                _output.WriteLine(line);
            }
        }
    }

    /// <summary>
    /// The released <c>InJetMode()</c> builtin (<c>0x005380f0</c> negating
    /// <c>0x00408120</c>) is FALSE only for a walker whose last ground contact
    /// is inside the shipped 0.5 s threshold at <c>0x005D85EC</c>. Every other
    /// state — jet, either transition, or a walker that has been airborne for
    /// half a second — reads as in jet mode, and cannot satisfy
    /// <c>TargetZoneN.msl</c>. See <see cref="Level100MissionTiming.JetModeState"/>.
    /// </summary>
    [Theory]
    // walker, just landed / still within the window
    [InlineData(VehicleMode.Walker, VehicleTransition.None, 0, false)]
    [InlineData(VehicleMode.Walker, VehicleTransition.None, 9, false)]
    // walker, airborne for 0.5 s or more: retail calls this in jet mode.
    // 0.5 s is TEN ticks at 20 Hz where it was fifteen at 30 Hz; the
    // threshold is the shipped _DAT_005d85ec = 0.5f either way.
    [InlineData(VehicleMode.Walker, VehicleTransition.None, 10, true)]
    [InlineData(VehicleMode.Walker, VehicleTransition.None, 400, true)]
    // mid-morph is not the walker state, whichever way it is going
    [InlineData(VehicleMode.Walker, VehicleTransition.WalkerToJet, 0, true)]
    [InlineData(VehicleMode.Jet, VehicleTransition.JetToWalker, 0, true)]
    // jet
    [InlineData(VehicleMode.Jet, VehicleTransition.None, 0, true)]
    public void InJetMode_IsFalseOnlyForARecentlyGroundedWalker(
        VehicleMode mode,
        VehicleTransition transition,
        int ticksSinceGroundContact,
        bool expectedInJetMode)
    {
        Assert.Equal(10, Level100MissionTiming.GroundContactRecencyTicks);
        Assert.Equal(
            expectedInJetMode
                ? Level100MissionJetModeState.InJetMode
                : Level100MissionJetModeState.NotInJetMode,
            Level100MissionTiming.JetModeState(
                mode,
                transition,
                ticksSinceGroundContact));
    }
}

/// <summary>
/// A deterministic autopilot for one released flight leg. It reads only what a
/// player sees — the objective's position, its own attitude, altitude and
/// energy — and answers with transform, look, throttle and landing-thruster
/// input. It never posts a mission event and never writes world state.
/// </summary>
internal sealed class Level100FlightLegDriver
{
    private readonly Simulation _simulation;
    private readonly List<string> _events = [];
    private Level100MissionTrigger _trigger;
    private bool _launched;
    private bool _committedToLanding;

    private Level100FlightLegDriver(Simulation simulation) =>
        _simulation = simulation;

    internal IReadOnlyList<string> Report => _events;

    /// <summary>State on the tick the most recent leg's trigger fired.</summary>
    internal WorldSnapshot? ArrivalState { get; private set; }

    internal static Level100FlightLegDriver Create()
    {
        var simulation = new Simulation(
            1u,
            Level100TestActorDefinitions.Create(),
            new Level100TutorialProgress(true, true, true, true));
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            simulation.Step(SimInput.Idle);
        }

        return new Level100FlightLegDriver(simulation);
    }

    internal bool FlyLeg(Level100MissionTrigger trigger, int maximumTicks)
    {
        _trigger = trigger;
        _launched = false;
        _committedToLanding = false;
        ArrivalState = null;

        // Capability grant only. See Simulation.GrantFlightLegForMeasurement.
        _simulation.GrantFlightLegForMeasurement(trigger);
        _events.Add($"--- leg {trigger}");

        VehicleMode lastMode = _simulation.Snapshot.Mode;
        for (int tick = 0; tick < maximumTicks; tick++)
        {
            WorldSnapshot state = _simulation.Snapshot;
            Level100ActorSnapshot zone = Zone(state);
            if (zone.TriggerEventDispatched || zone.TriggerEntered)
            {
                Log(state, "trigger entered");
                ArrivalState = state;
                return true;
            }

            if (state.Mode != lastMode)
            {
                Log(state, $"mode -> {state.Mode}");
                lastMode = state.Mode;
            }

            if (tick % 90 == 0)
            {
                Log(state, "…");
            }

            _simulation.Step(NextInput(state, zone));
        }

        Log(_simulation.Snapshot, "gave up");
        return false;
    }

    private Level100ActorSnapshot Zone(WorldSnapshot state) =>
        state.Level100Actors.Actors.Single(actor => actor.Trigger == _trigger);

    private void Log(WorldSnapshot state, string note)
    {
        Level100ActorSnapshot zone = Zone(state);
        double horizontal = Horizontal(state, zone);
        _events.Add(
            $"t{state.Tick} {note} mode={state.Mode} tr={state.Transition} " +
            $"d={horizontal:F0} dy={state.PlayerElevationMillimeters - zone.Pose.PositionMillimeters.Y} " +
            $"alt={state.PlayerElevationMillimeters - state.PlayerGroundElevationMillimeters} " +
            $"e={state.Energy} vy={state.PlayerVerticalVelocityMillimetersPerTick} " +
            $"speed={Math.Sqrt((double)((long)state.PlayerVelocity.X * state.PlayerVelocity.X + (long)state.PlayerVelocity.Z * state.PlayerVelocity.Z)):F0} " +
            $"pitch={state.FacingPitchMicroRad} onGround={state.PlayerOnGround}");
    }

    private static double Horizontal(WorldSnapshot state, Level100ActorSnapshot zone)
    {
        double deltaX = (double)zone.Pose.PositionMillimeters.X - state.PlayerPosition.X;
        double deltaZ = (double)zone.Pose.PositionMillimeters.Z - state.PlayerPosition.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private SimInput NextInput(WorldSnapshot state, Level100ActorSnapshot zone)
    {
        if (!state.Level100PlayerControlEnabled)
        {
            return SimInput.Idle;
        }

        double deltaX = (double)zone.Pose.PositionMillimeters.X - state.PlayerPosition.X;
        double deltaZ = (double)zone.Pose.PositionMillimeters.Z - state.PlayerPosition.Z;
        double horizontal = Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        double yawError = NormalizeRadians(
            Math.Atan2(-deltaX, deltaZ) - (state.FacingYawMicroRad / 1_000_000d));
        short lookX = (short)Math.Clamp((int)(yawError * 2_000), -1_000, 1_000);

        // Take off once, from a standstill, the way the help message teaches.
        if (!_launched)
        {
            if (state.Transition != VehicleTransition.None)
            {
                return SimInput.Idle;
            }
            if (state.Mode == VehicleMode.Jet)
            {
                _launched = true;
            }
            else
            {
                return new SimInput(0, 0, SimActions.ToggleMode);
            }
        }

        if (state.Mode == VehicleMode.Jet && state.Transition == VehicleTransition.None)
        {
            // Level the nose, hold a shallow climb until clear of the ground
            // effect, then fly the bearing. Throttle is the released
            // 0.5 - vy/2 axis: MoveZ +1 is full, 0 is the neutral half.
            int altitude =
                state.PlayerElevationMillimeters - state.PlayerGroundElevationMillimeters;
            double targetPitch = altitude < 12_000 ? -0.20 : 0.0;
            double pitchError = targetPitch - (state.FacingPitchMicroRad / 1_000_000d);
            short lookY = (short)Math.Clamp((int)(pitchError * 4_000), -1_000, 1_000);

            if (horizontal < 12_000)
            {
                _committedToLanding = true;
                return new SimInput(0, -1, SimActions.ToggleMode, 0, 0, lookX, lookY);
            }

            sbyte throttle = horizontal > 40_000 ? (sbyte)1 : (sbyte)0;
            return new SimInput(0, throttle, SimActions.None, 0, 0, lookX, lookY);
        }

        if (state.Mode == VehicleMode.Walker && state.Transition == VehicleTransition.None)
        {
            // Out of jet mode: hold the landing thrusters through the descent
            // and walk the remaining metres into the volume.
            SimActions actions = _committedToLanding && !state.PlayerOnGround
                ? SimActions.LandingJets
                : SimActions.None;
            sbyte forward = state.PlayerOnGround && horizontal > 1_500
                ? (sbyte)1
                : (sbyte)0;
            double pitchError = 0 - (state.FacingPitchMicroRad / 1_000_000d);
            short lookY = (short)Math.Clamp((int)(pitchError * 4_000), -1_000, 1_000);
            return new SimInput(0, forward, actions, 0, 0, lookX, lookY);
        }

        return SimInput.Idle;
    }

    private static double NormalizeRadians(double value)
    {
        while (value > Math.PI)
        {
            value -= 2 * Math.PI;
        }
        while (value < -Math.PI)
        {
            value += 2 * Math.PI;
        }
        return value;
    }
}
