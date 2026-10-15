// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// The deterministic in-process reconstruction path, once: cold start,
/// frontend, Level 100, played through the client's own player-input surface.
///
/// <para>The complete route is shared by the tests below.</para>
/// </summary>
public sealed class Level100ColdStartRunFixture : IDisposable
{
    internal Level100ColdStartRun Run { get; }

    internal Level100MissionOutcome Outcome { get; }

    public Level100ColdStartRunFixture()
    {
        Run = new Level100ColdStartRun();
        Outcome = Run.Run();
    }

    public void Dispose() => Run.Dispose();
}

/// <summary>
/// The instrument check for <see cref="Level100InteractiveChainHost"/>.
///
/// <para>Same cold career, same seed as the client's, straight into
/// <c>Simulation.Step</c> with the same whole-pixel look commands the client
/// can deliver. Complete state and pose-trace equality check the adapter
/// without requiring a different analogue command stream to win as well.</para>
/// </summary>
public sealed class Level100ClientPointerQuantisedRunFixture
{
    internal Level100ChainAutopilot Driver { get; }

    internal Level100MissionOutcome Outcome { get; }

    public Level100ClientPointerQuantisedRunFixture()
    {
        Driver = Level100ChainAutopilot.Create(
            default,
            quantizeLookToClientPointerPath: true,
            seed: Level100ColdStartRun.SimulationSeed,
            quantizeLookToIntegerMousePixels: true,
            actorDefinitions: Level100TestActorDefinitions.LoadMaterialized());
        Outcome = Driver.Run(1_200 * SimulationConstants.TicksPerSecond);
    }
}

public sealed class Level100ColdStartTests
    : IClassFixture<Level100ColdStartRunFixture>,
      IClassFixture<Level100ClientPointerQuantisedRunFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly Level100ColdStartRunFixture _coldStart;
    private readonly Level100ClientPointerQuantisedRunFixture _quantised;

    public Level100ColdStartTests(
        ITestOutputHelper output,
        Level100ColdStartRunFixture coldStart,
        Level100ClientPointerQuantisedRunFixture quantised)
    {
        _output = output;
        _coldStart = coldStart;
        _quantised = quantised;
    }

    /// <summary>
    /// The cold-start sequence: every stage of the released experience appears
    /// once, in the released order, before the level begins.
    ///
    /// <para>Nothing here is stepped past. The frontend is navigated by
    /// keystrokes and left clicks at 640x480 design-stage coordinates, Level 100
    /// is entered by CLICKING THE LEVEL NODE on SELECT LEVEL, and the Loading
    /// page completes on its own two-frame gate.</para>
    /// </summary>
    [Fact]
    public void ColdStart_VisitsEveryReleasedStageInOrder()
    {
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        Assert.Equal(44, definitions.Actors.Count);
        Assert.Equal(definitions.IdentitySha256,
            _coldStart.Run.Final.Level100Actors.DefinitionSetIdentitySha256);
        Assert.Equal(definitions.IdentitySha256,
            _quantised.Driver.Snapshot.Level100Actors.DefinitionSetIdentitySha256);
        foreach (ColdStartStage stage in _coldStart.Run.Stages)
        {
            _output.WriteLine(stage.ToString());
        }

        string[] expected =
        [
            "startup:LostToysLogo",
            "startup:OpeningMontage",
            "startup:Splash",
            "startup:Finished",
            "frontend:ClickToStart",
            "frontend:MainMenu",
            "frontend:MainMenuInteractive",
            "frontend:DevSelect",
            "frontend:LevelSelect",
            "frontend:MissionBriefing",
            "frontend:SelectConfiguration",
            "frontend:Loading",
            "frontend:Gameplay",
            "level:opening-pan-done",
        ];

        Assert.Equal(
            expected,
            _coldStart.Run.Stages.Select(stage => stage.Name).Take(expected.Length));

        // The reveal that runs on the way into the main menu really does own the
        // input: a click during it changed nothing.
        Assert.True(
            _coldStart.Run.MainMenuRevealSwallowedAClick,
            "a click during the 50-frame main-menu reveal must do nothing");
        Assert.True(_coldStart.Run.LoadRequestFrame > 0, "the load request never fired");
    }

    /// <summary>
    /// Startup through the tutorial through the client's player-input surface.
    /// The client and the direct Core control use the same deliverable mouse
    /// commands. Both must destroy all 22 targets, avoid the low-hull abort,
    /// complete objective 4, and win through the final trigger.
    ///
    /// <para>This omniscient in-process driver does not establish a human or
    /// native-Godot playthrough. Arbitrary different analogue command streams
    /// are not adapter controls; the independent returning-career, abort and
    /// naive-driver scenarios remain in <see cref="Level100FullChainTests"/>.</para>
    /// </summary>
    [Fact]
    public void ColdStart_PlaysLevel100ThroughThePlayerInputSurface()
    {
        Level100ColdStartRun run = _coldStart.Run;
        WorldSnapshot final = run.Final;
        WorldSnapshot controlFinal = _quantised.Driver.Snapshot;

        _output.WriteLine(
            "analogue look commands = {0}; unreachable through the client's " +
            "pointer path (<15 permille) = {1}; finer than one retail mouse " +
            "pixel (<30 permille) = {2}",
            run.Host.LookCommands,
            run.Host.UnreachableLookCommands,
            run.Host.SubRetailPixelLookCommands);
        _output.WriteLine(
            "CLIENT  outcome={0} tick={1} hull={2} reason={3} hash={4}",
            _coldStart.Outcome,
            final.Tick,
            final.Hull,
            final.Level100Mission.FailureReason,
            StateHasher.ComputeHex(final));
        _output.WriteLine(
            "POINTER CONTROL outcome={0} tick={1} hull={2} reason={3} hash={4}",
            _quantised.Outcome,
            controlFinal.Tick,
            controlFinal.Hull,
            controlFinal.Level100Mission.FailureReason,
            StateHasher.ComputeHex(controlFinal));
        _output.WriteLine(
            "first divergent tick = {0}",
            FirstDivergentTick(run.Driver!, _quantised.Driver));
        _output.WriteLine(string.Empty);
        _output.WriteLine("--- client (through InteractiveSession) ---");
        foreach (string line in run.Driver!.Report)
        {
            _output.WriteLine(line);
        }

        // The level really was played: the opening pan ran, the script handed
        // over control, and ALL FIVE trigger volumes dispatched - each of which
        // only dispatches out of jet mode, so the three flight legs were flown
        // and landed.
        Assert.True(final.Tick > SimulationConstants.Level100OpeningPanTicks);
        foreach (Level100MissionTrigger trigger in new[]
        {
            Level100MissionTrigger.TargetZone1,
            Level100MissionTrigger.FiringRange,
            Level100MissionTrigger.TargetZone2,
            Level100MissionTrigger.TargetZone3,
        })
        {
            Assert.True(
                final.Level100Actors.Actors
                    .Single(actor => actor.Trigger == trigger).TriggerEventDispatched,
                $"{trigger} never dispatched.");
        }

        foreach (string name in new[]
        {
            "Target Tank 2", "Target Tank 3", "Target Warehouse",
        })
        {
            Assert.Equal(
                Level100ActorLifecycle.Destroyed,
                final.Level100Actors.Actors
                    .Single(actor => actor.Name == name).Lifecycle);
        }
        Level100ActorSnapshot firstTank = Assert.Single(final.Level100Actors.Actors,
            actor => actor.TargetGroup == Level100MissionTargetGroup.StaticTargets && actor.TargetOrdinal == 1);
        Assert.Equal("Target Tank", firstTank.DefinitionName);
        Assert.Equal("TargetTank1", firstTank.ScriptName);
        Assert.NotNull(firstTank.SpawnOwnerId);
        Assert.Equal(Level100ActorLifecycle.Destroyed, firstTank.Lifecycle);

        // Reported before it is asserted, so a beat-9 move prints its numbers
        // instead of only its first tripped assertion. That ordering cost a
        // re-run during the 2026-08-01 re-derivation.
        _output.WriteLine(
            "CLIENT beats: trucks={0} moving={1} air1={2} air2={3} aborted={4} " +
            "objective4={5}",
            Destroyed(final, Level100MissionTargetGroup.TargetTrucks),
            Destroyed(final, Level100MissionTargetGroup.MovingTargets),
            Destroyed(final, Level100MissionTargetGroup.AirborneTargets1),
            Destroyed(final, Level100MissionTargetGroup.AirborneTargets2),
            final.Level100Mission.Aborted,
            final.Level100Mission.PrimaryObjectives
                .Single(objective => objective.Objective == 4).Status);
        _output.WriteLine(
            "POINTER CONTROL beats: air2={0} aborted={1} objective4={2}",
            controlFinal.Level100Actors.Actors.Count(actor =>
                actor.TargetGroup == Level100MissionTargetGroup.AirborneTargets2 &&
                actor.Lifecycle == Level100ActorLifecycle.Destroyed),
            controlFinal.Level100Mission.Aborted,
            controlFinal.Level100Mission.PrimaryObjectives
                .Single(objective => objective.Objective == 4).Status);

        // Beats 4, 5 and 7, every one of them shot with the weapon the script
        // hands over in the order it hands it over.
        Assert.Equal(3, Destroyed(final, Level100MissionTargetGroup.TargetTrucks));
        Assert.Equal(6, Destroyed(final, Level100MissionTargetGroup.MovingTargets));
        Assert.Equal(3, Destroyed(final, Level100MissionTargetGroup.AirborneTargets1));

        // Beat 9 ends on one of its two released branches (six kills, or the
        // sub-40 % abort), whichever the run's combat produces. The RE lane's
        // contract classes the six-kill expectation this test used to make,
        // failing since September 12, as a driver expectation rather than a
        // retail invariant.
        Level100FinalWaveContract.AssertReleasedBranch(final);

        // The same-input direct Core control takes the same branch with the
        // same kills.
        Level100FinalWaveContract.AssertReleasedBranch(controlFinal);
        Assert.Equal(final.Level100Mission.Aborted, controlFinal.Level100Mission.Aborted);
        Assert.Equal(
            Destroyed(final, Level100MissionTargetGroup.AirborneTargets2),
            controlFinal.Level100Actors.Actors.Count(actor =>
                actor.TargetGroup == Level100MissionTargetGroup.AirborneTargets2 &&
                actor.Lifecycle == Level100ActorLifecycle.Destroyed));

        // THE OUTPUT-CHANNEL CONTRACT, and it is the reason the joined run gets
        // this far at all. The driver is held to the stick positions an INTEGER
        // mouse pixel can produce, which since 2026-07-30 is exactly what the
        // client's own pointer path delivers, so nothing is lost between the
        // two. Before this run was quantised, 2,575 of its 5,439 analogue look
        // commands arrived as ZERO and it lost the level at t5277.
        //
        // This is HALF of player-plausibility, and the half that is cheap. What
        // the driver READS is still omniscient - exact Health, full 3D actor
        // poses, line-of-sight ray tests - so a `Won` here is not yet evidence
        // that a human could reach it. GOAL.md's demotion of the driven run to
        // an acceptance test is aimed at exactly that gap.
        Assert.Equal(0, run.Host.UnreachableLookCommands);
        Assert.True(run.Host.LookCommands > 1_000);

        // THE TERMINAL STATE, and it changed: this run used to end
        // `Lost`/`WaterLoss` at t17699 and now reaches `Won`.
        //
        // What it used to do, because the reason matters more than the verdict:
        // the fight was over and won - objective 4 is `Complete` above - and the
        // ferry home drowned. `NavigateToZone` dropped out of jet mode as soon
        // as the volume was inside 20 m HORIZONTALLY, with no altitude term at
        // all, and on this route that happened 28.7 m up over open water. From
        // that tick the airframe was an airborne walker, and
        // `Simulation.UpdateAirborneWalkerMovement` takes no `SimInput`: the
        // touchdown point was fixed at the morph and it came down 10.1 m past
        // the zone, in the sea.
        //
        // What fixed it is an altitude term on the hand-off - see
        // `Level100ChainAutopilot.ZoneHandoffClearanceMillimeters`. It is NOT a
        // change to the water rule, which is a byte-faithful port of
        // `BattleEngine.cpp:1259-1262` and is now pinned two ways by
        // `Level100FerryLandingTests`.
        //
        // Cold career and shipping definitions, with whole-pixel input through
        // InteractiveSession. The driver reads privileged simulation state;
        // this regression is not GOAL.md's player-observable live acceptance.
        Assert.Equal(Level100MissionOutcome.Won, _coldStart.Outcome);
        Assert.Equal(
            Level100MissionFailureReason.None,
            final.Level100Mission.FailureReason);
        Assert.True(
            final.Level100Actors.Actors
                .Single(actor => actor.Trigger == Level100MissionTrigger.TargetZone4)
                .TriggerEventDispatched,
            "Target Zone 4 never dispatched, so `Won` did not come from " +
            "`event(\"Reached Target Zone 4\")`.");

        // The direct control uses the same deliverable inputs. The next test
        // also requires complete state-hash and pose-trace equality.
        Assert.Equal(Level100MissionOutcome.Won, _quantised.Outcome);
        Assert.Equal(
            Level100MissionFailureReason.None,
            controlFinal.Level100Mission.FailureReason);
    }

    private static int Destroyed(
        WorldSnapshot state,
        Level100MissionTargetGroup group) => state.Level100Actors.Actors
        .Count(actor => actor.TargetGroup == group &&
            actor.Lifecycle == Level100ActorLifecycle.Destroyed);

    /// <summary>
    /// The joined client route and direct Core control consume the same
    /// deliverable commands.
    ///
    /// <para>Both runs are the same cold career on the same seed. One goes
    /// through <c>InteractiveSession</c>; the other goes straight into
    /// <c>Simulation.Step</c> with the look axes passed through
    /// <see cref="Level100InteractiveChainHost.DeliverablePermille"/> and
    /// nothing else changed. Equal complete hashes and pose traces establish
    /// that <see cref="Level100InteractiveChainHost"/> delivers that input
    /// faithfully; they make no outcome claim for different commands.</para>
    /// </summary>
    [Fact]
    public void ClientRoute_IsTheDirectRoutePlusPointerQuantisationAndNothingElse()
    {
        WorldSnapshot client = _coldStart.Run.Final;
        WorldSnapshot quantised = _quantised.Driver.Snapshot;
        _output.WriteLine(
            "CLIENT    tick={0} hull={1} outcome={2} hash={3}",
            client.Tick,
            client.Hull,
            _coldStart.Outcome,
            StateHasher.ComputeHex(client));
        _output.WriteLine(
            "QUANTISED tick={0} hull={1} outcome={2} hash={3}",
            quantised.Tick,
            quantised.Hull,
            _quantised.Outcome,
            StateHasher.ComputeHex(quantised));
        _output.WriteLine(
            "first divergent tick = {0}",
            FirstDivergentTick(_coldStart.Run.Driver!, _quantised.Driver));

        Assert.Equal(_quantised.Outcome, _coldStart.Outcome);
        Assert.Equal(quantised.Tick, client.Tick);
        Assert.Equal(
            StateHasher.ComputeHex(quantised),
            StateHasher.ComputeHex(client));
        Assert.Equal(_quantised.Driver.PoseTrace, _coldStart.Run.Driver!.PoseTrace);
    }

    /// <summary>
    /// The client's player-input surface is <b>not</b> transparent to a driver
    /// that commands an analogue look axis, and this measures the loss without
    /// reference to any particular run.
    ///
    /// <para>The client leaves its look actions unbound on purpose
    /// (<c>FirstFlightGame.ConfigureInputMap</c>), so the only route to the
    /// analogue axis is mouse motion through
    /// <c>InteractiveSession.QueuePointerMotionMilliPixels</c>. That path is
    /// quantised to whole retail mouse pixels, so the axis it can deliver is
    /// the LATTICE <c>{0, ±30, ±61, ±91, …}</c> and nothing in between.</para>
    ///
    /// <para><b>This test used to be called
    /// <c>ClientPointerPath_CannotReachTheFirstFourteenLookStops</c>, and its
    /// premise was half wrong.</b> The client did discard permille ±1..±14 -
    /// an invented half-pixel dead zone, task #141, measured at 44.2 % of one
    /// run's analogue look commands. But it then delivered ±15..±1000
    /// <i>exactly</i>, which is finer than the released build can aim. Retail
    /// has no dead zone on this path at all (the shipped mouse case at
    /// 0x0042DB40 clamps to ±1.0 and sign-gates at 0.0, and the GPL drop's 0.36
    /// <c>ANALOGUE_X_DEAD</c> is a joystick rule that does not occur once in
    /// the shipped image); what it has is a QUANTUM, because the cursor
    /// displacement is an integer pixel count. So the fourteen stops are still
    /// zero and the floor moved by one permille, while everything ABOVE the
    /// floor snapped onto retail's grid. The reconstruction lost a capability
    /// the released build never had.</para>
    ///
    /// <para>This is a property of the reconstruction's input adapter, not of
    /// Core: <c>Simulation.Step</c> accepts every value from 1 to 1000 and
    /// <c>Level100ChainAutopilot</c> commands them. It is recorded here because
    /// every isolated measurement in this suite drives <c>SimInput</c> directly
    /// and cannot see it at all.</para>
    /// </summary>
    [Fact]
    public void ClientPointerPath_DeliversExactlyRetailsWholePixelLattice()
    {
        // Derived from the released law rather than transcribed: n whole pixels
        // are worth n * g_MouseSensitivity * 13/3000 of full deflection, and
        // the image's untouched sensitivity is 7.0.
        var lattice = new HashSet<short>();
        for (int pixels = 1; pixels <= 1_000; pixels++)
        {
            int permille = (int)Math.Round(
                pixels * 91_000 / 3_000d,
                MidpointRounding.AwayFromZero);
            lattice.Add((short)Math.Min(permille, 1_000));
        }

        int exact = 0;
        int swallowed = 0;
        for (short requested = 1; requested <= 1_000; requested++)
        {
            short delivered =
                Level100InteractiveChainHost.DeliverablePermille(requested);

            // Odd symmetry, on every single stop and not just the sampled ones.
            Assert.Equal(
                (short)-delivered,
                Level100InteractiveChainHost.DeliverablePermille((short)-requested));

            if (delivered == 0)
            {
                // The ONLY thing delivered as nothing is a request that rounds
                // to zero whole pixels - below half of one, which at this
                // sensitivity is 15 permille.
                Assert.True(
                    requested <= 15,
                    $"{requested} permille was swallowed; only <= 15 should be");
                swallowed++;
                continue;
            }

            // Everything else lands ON the lattice, at the NEAREST point to
            // what was asked for. It snaps; it never refuses.
            Assert.Contains(delivered, lattice);
            Assert.True(
                Math.Abs(delivered - requested) <= 16,
                $"{requested} permille was delivered as {delivered}, which is " +
                "not the nearest whole pixel");

            if (delivered == requested)
            {
                exact++;
            }
        }

        // 32 whole pixels reach 971 permille and the 33rd saturates the axis.
        Assert.Equal(33, exact);

        // TASK #141'S FOURTEEN STOPS, STATED AS THE COUNT IT ASKED FOR - AND
        // THE ANSWER IS NOT THE ONE THE TASK EXPECTED.
        //
        // Before: 1..14 were delivered as ZERO by a half-pixel dead zone, and
        // 15..1000 were delivered EXACTLY - finer than the released build can
        // aim anywhere above the floor.
        // After:  1..15 are delivered as zero, because retail's first whole
        // pixel is worth 30 permille and half a pixel is 15.17.
        //
        // So the floor moved by ONE permille, 14 -> 15, and the fourteen stops
        // are still zero. What actually changed is everything above the floor:
        // the client no longer aims between retail's pixels. Making 1..14
        // reachable would need sub-pixel cursor state, which the released build
        // does not have - #141 called that out as the outcome to preserve
        // rather than fix, and this is that case.
        Assert.Equal(15, swallowed);

        // The stop immediately above the floor is no longer itself: it is the
        // first pixel, which is where a hand would actually put it.
        Assert.Equal(
            (short)30,
            Level100InteractiveChainHost.DeliverablePermille(16));
        Assert.Equal(
            (short)30,
            Level100InteractiveChainHost.DeliverablePermille(30));
        Assert.Equal(
            (short)0,
            Level100InteractiveChainHost.DeliverablePermille(15));
    }

    /// <summary>
    /// The cold start's own recording is a complete Level 100 tape: replayed
    /// twice through the headless replayer's <see cref="ReplayRunner"/>, it
    /// reproduces the trace and final-state hashes observed live and ends with
    /// the mission won. <c>ONSLAUGHT_WON_TAPE_PATH</c>, when set to an absolute
    /// path, also writes it with the host's create-new <see cref="TapeFile"/>.
    /// </summary>
    [Fact]
    public void ColdStart_RecordsATapeThatReplaysDeterministicallyToAWin()
    {
        Assert.Equal(Level100MissionOutcome.Won, _coldStart.Outcome);
        CommandTape tape = _coldStart.Run.Recorder.BuildObserved(
            "level100-cold-start-won", Level100ColdStartRun.SimulationSeed);
        Assert.Equal(_coldStart.Run.Final.Tick, tape.DurationTicks);

        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.LoadMaterialized();
        ReplayResult first = ReplayRunner.Run(tape, definitions);
        ReplayResult second = ReplayRunner.Run(tape, definitions);
        Assert.Equal(tape.ExpectedTraceHash, first.TraceHash);
        Assert.Equal(tape.ExpectedFinalStateHash, first.FinalStateHash);
        Assert.Equal(first.TraceHash, second.TraceHash);
        Assert.Equal(first.FinalStateHash, second.FinalStateHash);
        Assert.Equal(Level100MissionOutcome.Won, first.FinalState.Level100Mission.Outcome);
        _output.WriteLine(
            $"WON TAPE ticks={tape.DurationTicks} trace={first.TraceHash} state={first.FinalStateHash}");

        if (Environment.GetEnvironmentVariable("ONSLAUGHT_WON_TAPE_PATH") is { Length: > 0 } path)
        {
            TapeFile.WriteNew(path, tape);
            _output.WriteLine($"WON TAPE written to {path}");
        }
    }

    /// <summary>
    /// The first tick at which the two runs' player poses differ. Both traces
    /// start on the tick the opening pan ends.
    /// </summary>
    private static int FirstDivergentTick(
        Level100ChainAutopilot client,
        Level100ChainAutopilot control)
    {
        IReadOnlyList<long> a = client.PoseTrace;
        IReadOnlyList<long> b = control.PoseTrace;
        int shared = Math.Min(a.Count, b.Count);
        for (int index = 0; index < shared; index++)
        {
            if (a[index] != b[index])
            {
                return SimulationConstants.Level100OpeningPanTicks + index;
            }
        }

        return -1;
    }
}
