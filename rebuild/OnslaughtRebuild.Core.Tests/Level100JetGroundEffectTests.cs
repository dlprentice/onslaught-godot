// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The Level 100 jet ground-effect band, flown.
///
/// <para><b>Why this file exists.</b> <see cref="Simulation.ApplyJetGroundEffect"/>
/// only runs below <see cref="SimulationConstants.JetGroundEffectHeightMillimeters"/>
/// (5,000 mm), and nothing else in the suite ever gets there: the Level 100
/// chain autopilot enters jet mode at 15,212 mm and deliberately climbs clear of
/// the band, so the chain's <c>Won</c> tick and the Godot smoke stateHash are
/// both blind to this entire code path. A change in here — right or wrong —
/// produced green gates and an unchanged hash. That is the "gate that cannot see
/// the thing it guards" class, and these tests close it.</para>
///
/// <para><b>The law under test, not a constant.</b>
/// <c>CBattleEngineJetPart::HandleGroundEffect</c>
/// (<c>references/Onslaught/BattleEngineJetPart.cpp:548</c>) samples the terrain
/// <em>ahead</em> of the vehicle, not underneath it:</para>
/// <code>
/// FVector pos = mMainPart->mPos + (mMainPart->mVelocity * GAME_FR * 0.5f);
/// </code>
/// <para><c>GAME_FR</c> is <c>20.0f</c> (<c>references/Onslaught/thing.h:28</c>)
/// and <c>mVelocity</c> is per released 20 Hz update (bounded in
/// <c>actor.cpp</c> as <c>GetMaxVelocity()/GAME_FR</c>), so
/// <c>velocity * 20 * 0.5</c> is <b>half a second of travel</b> — fifteen ticks
/// at Core's 30 Hz, which is
/// <see cref="SimulationConstants.JetGroundEffectLookaheadTicks"/>.</para>
///
/// <para>The observable consequence, and the thing asserted here, is that a jet
/// flying level at rising ground starts responding to the slope <b>while it is
/// still short of it</b>, by the distance it covers in half a second — so the
/// lead <em>distance</em> scales with speed while the lead <em>time</em> does
/// not. Pinning a millimetre figure would prove nothing; three approaches at
/// three speeds, each leading by the same half second, prove the rule.</para>
/// </summary>
public sealed class Level100JetGroundEffectTests
{
    private readonly ITestOutputHelper _output;

    public Level100JetGroundEffectTests(ITestOutputHelper output) =>
        _output = output;

    /// <summary>
    /// Throttle settings that produce three materially different cruise speeds
    /// through the released <c>0.5 - vy/2</c> thruster axis: minimum, neutral
    /// and full.
    /// </summary>
    private static readonly sbyte[] Throttles = [-1, 0, 1];

    /// <summary>
    /// Ground effect engages while the ground <em>directly beneath</em> the
    /// vehicle is still well outside the 5,000 mm band — because the sample
    /// point is half a second downrange, over the hillside the vehicle has not
    /// reached yet.
    /// </summary>
    [Fact]
    public void GroundEffect_EngagesBeforeTheJetIsOverTheRisingGround()
    {
        var driver = Level100LowFlightDriver.Create();
        try
        {
            Level100GroundEffectEngagement engagement = driver.FlyAtRisingGround(0);
            Assert.True(
                engagement.AltitudeAboveSurfaceMillimeters >
                    SimulationConstants.JetGroundEffectHeightMillimeters,
                "Ground effect engaged only once the jet was already inside the " +
                $"band over its own position ({engagement.AltitudeAboveSurfaceMillimeters} mm), " +
                "so it is sampling underneath itself rather than half a second ahead.");

            // The measured margin is ~1,200-2,100 mm on this hillside. A
            // one-tick lookahead samples ~250 mm downrange and would engage
            // within a couple of hundred millimetres of the band.
            Assert.True(
                engagement.AltitudeAboveSurfaceMillimeters >=
                    SimulationConstants.JetGroundEffectHeightMillimeters + 500,
                $"Engagement margin was only {engagement.AltitudeAboveSurfaceMillimeters - SimulationConstants.JetGroundEffectHeightMillimeters} mm.");
        }
        finally
        {
            Report(driver);
        }
    }

    /// <summary>
    /// The lead is a <b>time</b>, not a distance. Flown at three speeds, each
    /// approach begins responding to the same hillside after covering a
    /// different distance but the same half second.
    /// </summary>
    [Fact]
    public void GroundEffectLead_IsHalfASecondOfTravelAtEverySpeed()
    {
        var driver = Level100LowFlightDriver.Create();
        var engagements = new List<Level100GroundEffectEngagement>();
        try
        {
            foreach (sbyte throttle in Throttles)
            {
                engagements.Add(driver.FlyAtRisingGround(throttle));
            }

            // Guard against a degenerate pass: if the three approaches all flew
            // at the same speed, a constant lead distance would satisfy the
            // lead-time assertion for the wrong reason.
            double slowest = engagements.Min(e => e.SpeedMillimetersPerTick);
            double fastest = engagements.Max(e => e.SpeedMillimetersPerTick);
            Assert.True(
                fastest > slowest * 2.0,
                $"Approach speeds were not separated enough: {slowest:F0} .. {fastest:F0} mm/tick.");

            foreach (Level100GroundEffectEngagement engagement in engagements)
            {
                double leadTicks = engagement.LeadTicks;
                Assert.True(
                    leadTicks is >= 8.7 and <= 11.0,
                    $"Throttle {engagement.Throttle}: ground effect engaged " +
                    $"{engagement.LeadDistanceMillimeters} mm short of the slope at " +
                    $"{engagement.SpeedMillimetersPerTick:F1} mm/tick, which is {leadTicks:F2} ticks. " +
                    "BattleEngineJetPart.cpp:548 samples half a second of travel ahead, " +
                    $"which is {SimulationConstants.TicksPerSecond / 2} ticks at Core's " +
                    $"{SimulationConstants.TicksPerSecond} Hz " +
                    "(JetGroundEffectLookaheadTicks is currently " +
                    $"{SimulationConstants.JetGroundEffectLookaheadTicks}).");
            }

            // Lead distance scales with speed: same time, different ground.
            Level100GroundEffectEngagement slow =
                engagements.MinBy(e => e.SpeedMillimetersPerTick)!;
            Level100GroundEffectEngagement fast =
                engagements.MaxBy(e => e.SpeedMillimetersPerTick)!;
            double speedRatio = fast.SpeedMillimetersPerTick / slow.SpeedMillimetersPerTick;
            double distanceRatio =
                (double)fast.LeadDistanceMillimeters / slow.LeadDistanceMillimeters;
            Assert.True(
                Math.Abs(distanceRatio - speedRatio) < 0.20 * speedRatio,
                $"Lead distance did not scale with speed: speed ratio {speedRatio:F2}, " +
                $"lead-distance ratio {distanceRatio:F2}.");
        }
        finally
        {
            Report(driver);
        }
    }

    private void Report(Level100LowFlightDriver driver)
    {
        foreach (string line in driver.Report)
        {
            _output.WriteLine(line);
        }
    }
}

/// <summary>One measured ground-effect engagement.</summary>
/// <param name="Throttle">The held <c>MoveZ</c> axis for the approach.</param>
/// <param name="SpeedMillimetersPerTick">Horizontal cruise speed on the tick
/// before engagement — the same velocity the released sample point is built
/// from.</param>
/// <param name="LeadDistanceMillimeters">Distance from the vehicle, along its
/// own track, to the first point where the terrain rises far enough to put the
/// vehicle inside the 5,000 mm band.</param>
/// <param name="AltitudeAboveSurfaceMillimeters">Clearance over the ground
/// <em>directly beneath</em> the vehicle at that moment.</param>
internal sealed record Level100GroundEffectEngagement(
    sbyte Throttle,
    double SpeedMillimetersPerTick,
    int LeadDistanceMillimeters,
    int AltitudeAboveSurfaceMillimeters)
{
    internal double LeadTicks => LeadDistanceMillimeters / SpeedMillimetersPerTick;
}

/// <summary>
/// A deterministic driver that takes off and cruises the Aquila at a fixed
/// <em>absolute</em> elevation straight at the hillside north-west of the
/// authored Level 100 start.
///
/// <para><b>Player input only.</b> The single Core mutator it calls is
/// <see cref="Simulation.Step(SimInput, IReadOnlyList{Level100SimulationFact}?)"/>,
/// plus the pre-existing <see cref="Simulation.GrantFlightLegForMeasurement"/>
/// capability grant that <see cref="Level100FlightLegDriver"/> already uses — the
/// released <c>player.EnableFlightMode()</c> the LevelScript performs at beat 6.
/// Nothing here writes world state, teleports the vehicle, or reads private
/// simulation fields.</para>
///
/// <para><b>Why it holds absolute elevation rather than altitude.</b> An
/// altitude-hold controller reacts to the hillside itself, which would
/// contaminate the very response being measured. Holding a fixed elevation
/// leaves ground effect as the only terrain-dependent term in the loop.</para>
///
/// <para><b>How engagement is detected, with no private state.</b>
/// <see cref="WorldSnapshot.RollVelocityMicroRadPerTick"/>. In jet mode with
/// <c>LookX</c> and <c>MoveX</c> both zero, <c>UpdateJetOrientation</c>
/// contributes exactly zero roll input and its retention of zero is zero, so the
/// <em>only</em> writer of roll velocity is
/// <c>ApplyJetGroundEffect</c>'s terrain-roll follow. Roll velocity leaving zero
/// is therefore the tick ground effect first engaged, observed from the public
/// snapshot the renderer already consumes.</para>
///
/// <para>Like <see cref="Level100ChainAutopilot"/>, this driver is
/// input-equivalent to a player but not perception-equivalent: it samples
/// <see cref="Level100Terrain"/> directly to <em>measure</em> where the slope
/// crosses the band. That sampling never feeds the control loop.</para>
/// </summary>
internal sealed class Level100LowFlightDriver
{
    /// <summary>Absolute elevation the cruise holds, in Core millimetres.</summary>
    private const int CruiseElevationMillimeters = 10_000;

    /// <summary>Clearance required before an approach is considered settled.</summary>
    private const int SettledClearanceMillimeters = 8_000;

    private const int MaximumApproachTicks = 30 * SimulationConstants.TicksPerSecond;

    private readonly List<string> _log = [];

    private Level100LowFlightDriver()
    {
    }

    internal IReadOnlyList<string> Report => _log;

    internal static Level100LowFlightDriver Create() => new();

    /// <summary>
    /// Takes off, settles at <see cref="CruiseElevationMillimeters"/> on the
    /// authored start heading, and flies until ground effect first engages.
    /// Each call starts from a fresh session so the three approaches are
    /// independent.
    /// </summary>
    internal Level100GroundEffectEngagement FlyAtRisingGround(sbyte throttle)
    {
        Simulation simulation = FreshSession();
        _log.Add($"--- approach throttle={throttle}");
        TakeOff(simulation);

        bool settled = false;
        WorldSnapshot? previous = null;
        for (int step = 0; step < MaximumApproachTicks; step++)
        {
            WorldSnapshot state = simulation.Snapshot;
            Assert.Equal(VehicleMode.Jet, state.Mode);

            if (!settled &&
                state.RollVelocityMicroRadPerTick == 0 &&
                state.JetTicksSinceTransform > SimulationConstants.TicksPerSecond &&
                state.PlayerAltitudeAboveSurfaceMillimeters > SettledClearanceMillimeters)
            {
                settled = true;
                Log(state, "settled");
            }

            if (settled && state.RollVelocityMicroRadPerTick != 0)
            {
                WorldSnapshot approach = previous!;
                Log(state, "GROUND EFFECT ENGAGED");
                return Measure(throttle, approach);
            }

            previous = state;
            simulation.Step(new SimInput(
                0,
                throttle,
                SimActions.None,
                0,
                0,
                0,
                ElevationHoldLookY(state)));
        }

        Log(simulation.Snapshot, "no engagement");
        throw new InvalidOperationException(
            $"Throttle {throttle}: the approach never entered ground effect. " +
            "See the driver report for the flown track.");
    }

    private static Simulation FreshSession()
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
        return simulation;
    }

    private void TakeOff(Simulation simulation)
    {
        for (int step = 0; step < 4 * SimulationConstants.TicksPerSecond; step++)
        {
            WorldSnapshot state = simulation.Snapshot;
            if (state.Mode == VehicleMode.Jet &&
                state.Transition == VehicleTransition.None)
            {
                Log(state, "airborne");
                return;
            }

            simulation.Step(
                state.Mode == VehicleMode.Walker &&
                state.Transition == VehicleTransition.None
                    ? new SimInput(0, 0, SimActions.ToggleMode)
                    : SimInput.Idle);
        }

        throw new InvalidOperationException("The Aquila never reached jet mode.");
    }

    /// <summary>
    /// A proportional-plus-rate hold on absolute elevation, expressed the only
    /// way a player can express it: the pitch axis. It reads elevation, vertical
    /// velocity and pitch from the snapshot and nothing else.
    /// </summary>
    private static short ElevationHoldLookY(WorldSnapshot state)
    {
        double elevationError =
            (CruiseElevationMillimeters - state.PlayerElevationMillimeters) / 10_000d;
        double targetPitch = Math.Clamp(
            -elevationError - (state.PlayerVerticalVelocityMillimetersPerTick / 400d),
            -0.35,
            0.35);
        double pitchError = targetPitch - (state.FacingPitchMicroRad / 1_000_000d);
        return (short)Math.Clamp((int)(pitchError * 4_000), -1_000, 1_000);
    }

    /// <summary>
    /// How far ahead of itself, along its own track, the vehicle had to look to
    /// see ground high enough to put it inside the band. This is measurement
    /// only; it never steers the vehicle.
    /// </summary>
    private static Level100GroundEffectEngagement Measure(
        sbyte throttle,
        WorldSnapshot approach)
    {
        double speed = Math.Sqrt(
            ((double)approach.PlayerVelocity.X * approach.PlayerVelocity.X) +
            ((double)approach.PlayerVelocity.Z * approach.PlayerVelocity.Z));
        Assert.True(speed > 1.0, "The approach was not moving.");

        int bandEntrySupport =
            approach.PlayerElevationMillimeters -
            SimulationConstants.JetGroundEffectHeightMillimeters;
        const int SampleStepMillimeters = 25;
        const int SearchLimitMillimeters = 200_000;
        for (int distance = 0;
            distance < SearchLimitMillimeters;
            distance += SampleStepMillimeters)
        {
            var probe = new SimVector2(
                (int)(approach.PlayerPosition.X +
                    (approach.PlayerVelocity.X / speed * distance)),
                (int)(approach.PlayerPosition.Z +
                    (approach.PlayerVelocity.Z / speed * distance)));
            if (SupportElevationMillimeters(probe) >= bandEntrySupport)
            {
                return new Level100GroundEffectEngagement(
                    throttle,
                    speed,
                    distance,
                    approach.PlayerAltitudeAboveSurfaceMillimeters);
            }
        }

        throw new InvalidOperationException(
            "Ground effect engaged with no rising ground on the vehicle's track.");
    }

    /// <summary>
    /// The surface the released ground effect measures against: terrain, or the
    /// water plane wherever the terrain is below it.
    /// </summary>
    private static int SupportElevationMillimeters(SimVector2 position) =>
        Math.Max(
            Level100Terrain.Instance.SampleGroundElevationMillimeters(position),
            Level100Terrain.WaterElevationMillimeters);

    private void Log(WorldSnapshot state, string note) =>
        _log.Add(
            $"t{state.Tick} {note} pos=({state.PlayerPosition.X},{state.PlayerPosition.Z}) " +
            $"elev={state.PlayerElevationMillimeters} " +
            $"altSurface={state.PlayerAltitudeAboveSurfaceMillimeters} " +
            $"v=({state.PlayerVelocity.X},{state.PlayerVelocity.Z}) " +
            $"vy={state.PlayerVerticalVelocityMillimetersPerTick} " +
            $"pitch={state.FacingPitchMicroRad} roll={state.BodyRollMicroRad} " +
            $"rollVel={state.RollVelocityMicroRadPerTick} energy={state.Energy} " +
            $"hull={state.Hull}");
}
