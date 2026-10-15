// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// One deterministic <c>SimInput</c>-only chain run, shared by its assertions.
/// It posts no mission events. Full-combat acceptance requires all six final
/// drones destroyed, objective 4 complete and no low-hull abort; reaching
/// <c>Won</c> through the authored abort branch does not satisfy that contract.
/// </summary>
public sealed class Level100ChainRunFixture
{
    internal Level100ChainAutopilot Driver { get; }

    internal Level100MissionOutcome Outcome { get; }

    public Level100ChainRunFixture()
    {
        Driver = Level100ChainAutopilot.Create();
        Outcome = Driver.Run(1_200 * SimulationConstants.TicksPerSecond);
    }
}

/// <summary>
/// The same controller with fire suppressed throughout beat 9. This control
/// exercises the authored low-hull abort and causal Blaster accounting
/// independently of whether the main driver currently clears the wave.
/// </summary>
public sealed class Level100AbortControlRunFixture
{
    internal Level100ChainAutopilot Driver { get; }

    internal Level100MissionOutcome Outcome { get; }

    public Level100AbortControlRunFixture()
    {
        Driver = Level100ChainAutopilot.CreateWithWaveTwoTriggerHeldShut();
        Outcome = Driver.Run(1_200 * SimulationConstants.TicksPerSecond);
    }
}

/// <summary>
/// The same control with beat-9 strafing also suppressed, providing a second
/// movement path for causal Blaster accounting without a required hit rate.
/// </summary>
public sealed class Level100AbortNoCrabRunFixture
{
    internal Level100ChainAutopilot Driver { get; }

    internal Level100MissionOutcome Outcome { get; }

    public Level100AbortNoCrabRunFixture()
    {
        Driver = Level100ChainAutopilot.CreateWithWaveTwoTriggerAndCrabHeldShut();
        Outcome = Driver.Run(1_200 * SimulationConstants.TicksPerSecond);
    }
}

public sealed class Level100ChainAimTests
{
    [Theory]
    [InlineData(0, SimActions.Fire)]
    [InlineData(259, SimActions.None)]
    public void WaveTwoFireGateUsesTheSteeringReticleOrigin(
        int aimHeightAboveCamera, SimActions expected)
    {
        WorldSnapshot state = new Simulation(
            0xA100u, Level100TestActorDefinitions.Create()).Snapshot with
        {
            PlayerPosition = SimVector2.Zero,
            PlayerElevationMillimeters = 10_000,
            FacingYawMicroRad = 0,
            FacingPitchMicroRad = 0,
        };
        var aim = new SimVector3(0, 10_000 + aimHeightAboveCamera, 1_000);
        double slant = Math.Sqrt(1_000_000d +
            ((double)aimHeightAboveCamera * aimHeightAboveCamera));

        // At one metre the unchanged tolerance is 0.15 radians. The level
        // point lies on the reticle; the raised point is about 0.253 radians
        // above it. Production launch owns physical-emitter convergence.
        Assert.Equal(expected,
            Level100ChainAutopilot.WaveTwoFireGate(state, aim, slant, 10_000));
    }
}

public sealed class Level100FullChainTests
    : IClassFixture<Level100ChainRunFixture>,
      IClassFixture<Level100AbortControlRunFixture>,
      IClassFixture<Level100AbortNoCrabRunFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly Level100ChainRunFixture _chain;
    private readonly Level100AbortControlRunFixture _abortControl;
    private readonly Level100AbortNoCrabRunFixture _abortNoCrab;

    public Level100FullChainTests(
        ITestOutputHelper output,
        Level100ChainRunFixture chain,
        Level100AbortControlRunFixture abortControl,
        Level100AbortNoCrabRunFixture abortNoCrab)
    {
        _output = output;
        _chain = chain;
        _abortControl = abortControl;
        _abortNoCrab = abortNoCrab;
    }

    /// <summary>
    /// The naive walker autopilot - fixed 18 m stand-off, fire whenever the
    /// reticle is on the objective, never check what is in between.
    ///
    /// <para>It clears the firing range but never wins, with more rounds in the
    /// terrain than in every target combined.</para>
    ///
    /// <para><b>It used to stall a beat earlier, and #146 is why.</b> Beat 3's
    /// fourth target, <c>Target Tank #23</c>, follows <c>Target Tank Path 1</c>.
    /// Walked in the level file's SERIALIZED order that route opens at node 18,
    /// (-68688, 80000), which took the tank away from the firing range and out
    /// of this driver's fixed 18 m stand-off forever. Walked in the order the
    /// markers' own <c>target</c> pointers chain them - [6, 7, 18] - it opens at
    /// node 6, (-25438, 20500), which brings it into range on its first leg. The
    /// tank now dies and beat 3 completes. The exact later navigation objective
    /// and terminal branch remain timing-sensitive measurements, not properties
    /// of this deliberately incompetent driver.</para>
    ///
    /// <para><b>The terminal details are reported but not asserted.</b> They have
    /// changed more than once as retail-correct timing was restored. On the
    /// 2026-08-13 branch the run reaches <c>Lost/TutorialBroken</c> at t13010
    /// with <c>Firing Range</c> still selected. Earlier branches stayed
    /// <c>Running</c> at <c>Target Zone 2</c>. Neither exact branch is the
    /// contract: clearing all four firing-range statics, following the authored
    /// route, wasting most rounds on terrain, and still not winning are.</para>
    ///
    /// <para>This is kept, and kept failing-forward rather than deleted,
    /// because it is the control for
    /// <see cref="ChainAutopilot_ReachesWonByInputAlone"/>: the same world and
    /// the same weapons, and the entire difference is that the competent driver
    /// checks the ground and the surrounding structures before it pulls the
    /// trigger. Shooting without looking does not merely cost rounds in this
    /// level - it does not finish it at all.</para>
    /// </summary>
    [Fact]
    public void NaiveWalkerAutopilot_ClearsTheFiringRangeAndStillNeverFinishes()
    {
        var driver = Level100PlayerDriver.Create();
        driver.Run(30 * 900);
        foreach (string line in driver.Report)
        {
            _output.WriteLine(line);
        }

        WorldSnapshot final = driver.Snapshot;
        Level100ActorSnapshot tank = final.Level100Actors.Actors
            .Single(actor => actor.Name == "Target Tank #23");
        Level100ActorCommandIntentSnapshot? intent =
            final.Level100ActorMechanics.Actors
                .FirstOrDefault(actor => actor.ActorId == tank.ActorId);
        _output.WriteLine(
            $"tank pose={tank.Pose.PositionMillimeters} intent={intent?.Intent} " +
            $"player={final.PlayerPosition} y={final.PlayerElevationMillimeters}");
        _output.WriteLine(
            $"FINAL outcome={final.Level100Mission.Outcome} " +
            $"reason={final.Level100Mission.FailureReason} " +
            $"textId={final.Level100Mission.FailureTextId} " +
            $"nav={final.Level100Mission.NavigationObjective} " +
            $"mode={final.Mode} hull={final.Hull} " +
            $"destroyed={string.Join(",", final.Level100Actors.Actors
                .Where(actor => actor.Lifecycle == Level100ActorLifecycle.Destroyed)
                .Select(actor => actor.Name))}");

        // Beat 3, three of four.
        foreach (string name in
                 new[] { "Target Tank 2", "Target Tank 3", "Target Warehouse" })
        {
            Assert.Equal(
                Level100ActorLifecycle.Destroyed,
                final.Level100Actors.Actors
                    .Single(actor => actor.Name == name).Lifecycle);
        }

        // The released path is now followed to its end, which the synthetic
        // fixture path could never do.
        //
        // Asserted against the route DATA rather than against a coordinate.
        // This used to be `Z > 60_000`, a number lifted from where the tank
        // happened to stop under the pre-`58d9ce57` waypoint table - which
        // resolved node indices against the wrong RLWD structure, so the route
        // it described was not the released one at all. A magic number cannot
        // tell "followed the route to its end" from "the route moved", and when
        // the route was corrected this assertion failed while the behaviour it
        // was written to check was still exactly right.
        Assert.Equal(Level100ActorCommandIntent.Stopped, intent?.Intent);
        Level100ActorDefinitionSet definitions = Level100TestActorDefinitions.Create();
        // The route's last node is 18, the one whose target is none:
        // `Target Tank Path 1` chains 6 -> 7 -> 18 (waypoint-paths.md).
        Level100WaypointPathDefinition tankPath =
            definitions.GetWaypointPath("Target Tank Path 1");
        Level100WaypointPointDefinition lastNode =
            Assert.Single(tankPath.Points, point => point.TargetNodeIndex is null);
        Assert.Equal(18, lastNode.NodeIndex);
        long arrivalRadius = definitions
            .GetMotionDefinition("Target Tank").ArrivalRadiusMillimeters;
        long deltaX = tank.Pose.PositionMillimeters.X - (long)lastNode.PositionMillimeters.X;
        long deltaZ = tank.Pose.PositionMillimeters.Z - (long)lastNode.PositionMillimeters.Z;
        Assert.True(
            (deltaX * deltaX) + (deltaZ * deltaZ) < arrivalRadius * arrivalRadius,
            $"Target Tank #23 stopped at {tank.Pose.PositionMillimeters}, which is not " +
            $"inside the {arrivalRadius} mm arrival radius of its route's final node " +
            $"{lastNode.PositionMillimeters}.");

        // THE HONEST NEGATIVE: shooting without looking does not finish this
        // level. Exact failure time, reason, and selected navigation objective
        // have moved as retail-correct timing changed, so none is the invariant.
        //
        // Everything around it moved under #146, and the move is real rather
        // than cosmetic, so the old assertions are re-derived from the driver's
        // own report rather than nudged. This test used to be called
        // `…StallsOnBeatThreeAndNeverFinishes` and asserted that Target Tank #23
        // was still ALIVE and the objective still "Firing Range". Both were
        // consequences of walking `Target Tank Path 1` in SERIALIZED order
        // [18, 6, 7]: node 18 is at (-68688, 80000), so the tank's first move
        // was away from the firing range and out of the naive driver's reach.
        // The authored `target` chain is [6, 7, 18] and node 6 is at
        // (-25438, 20500) - toward the player - so the tank now drives into
        // range on its first leg and dies there.
        //
        // Measured on this tree (driver report above): 10 actors destroyed and
        // Lost/TutorialBroken at t13010 with "Firing Range" selected. Those are
        // retained as a dated observation, not promoted into a brittle branch
        // assertion.
        Assert.NotEqual(Level100MissionOutcome.Won, final.Level100Mission.Outcome);
        Assert.Equal(Level100ActorLifecycle.Destroyed, tank.Lifecycle);

        // The waste is still the point, restated as what is now measurable.
        // 2,396 rounds went into the terrain against 422 into every target
        // combined, so more than half of everything fired still hits nothing -
        // which is the same claim the old `> 1_000` bound was making before the
        // corrected routes let the driver connect at all.
        Assert.True(
            driver.ImpactsByActor.TryGetValue(0, out int terrainImpacts),
            "the naive driver must still be recording terrain impacts");
        int targetImpacts = driver.ImpactsByActor
            .Where(pair => pair.Key != 0)
            .Sum(pair => pair.Value);
        Assert.True(
            terrainImpacts > targetImpacts,
            $"the naive driver put {terrainImpacts} rounds into terrain against " +
            $"{targetImpacts} into all targets combined; its failure mode is " +
            "supposed to be that most rounds hit nothing.");
    }

    /// <summary>
    /// The whole released chain, played end to end by one autopilot that drives
    /// <see cref="SimInput"/> and nothing else.
    ///
    /// <para><b>No mission event is posted by this test or by the driver.</b>
    /// Every one of the eleven named progression events comes out of the world:
    /// the two volumes are entered on foot, the four beat-3 statics and the
    /// three beat-4 trucks and the six beat-5 moving spawns are shot with the
    /// weapons the script hands over in the order it hands them over, the three
    /// flight legs are flown and landed so that <c>TargetZoneN.msl</c>'s
    /// <c>InJetMode() == FALSE</c> is satisfied, and the beat-7 drones are shot
    /// down in jet mode because the script has disabled both walker weapons by
    /// then. <c>LevelWon()</c> is called by the released
    /// <c>event("Reached Target Zone 4")</c> and by nothing else.</para>
    ///
    /// <para><b>Prior measured branch, re-pinned 2026-08-21 with a bisect
    /// receipt.</b> The 2026-08-14 branch (pinned at 383d5b3e "Model player
    /// weapon scatter") reached <c>Won</c> at t8404 with 8,428 milli-life.
    /// Across 4a978c5b..988f2db0 the evidenced InJetMode contract cluster
    /// left that route unreachable — the chain exhausted its budget stuck at
    /// Target Zone 2 in walker mode — and e633b511 ("Pin Level 100
    /// TargetZone hit() InJetMode and Pause wait-stop") restored winning by
    /// a different, contract-correct route: the modern trajectory (Won at
    /// t6572, hull 18,244) first exists at e633b511 and is stable through
    /// HEAD. Established by a tick-pin-only bisect (predicate greps the
    /// 8404 assertion itself; commits where the chain cannot win are skips,
    /// because they have no tick to drift): e633b511 BAD with Expected 8404
    /// / Actual 6572 at this file's tick pin, hull 18244; last GOOD
    /// ancestor b56cd36a; all nine commits between are skips. Re-pinning is
    /// justified by that measured attribution — these are evidenced retail
    /// contract changes, not a silent regression. The OPEN parity question
    /// (whether training now plays easier than retail, hull 18,244 vs the
    /// route's old 8,428) is unchanged and belongs to the Level 100 lane
    /// with retail-side evidence. The prior abort-path trajectories remain
    /// dated evidence in the local-lab reports, not the active expectation
    /// of this test.</para>
    ///
    /// <para><b>Current measured branch, re-pinned 2026-08-28 for the released
    /// Battle Engine finite-cylinder contact.</b> A fresh full-suite candidate
    /// run reached <c>Won</c> at t6992 with 9,900 milli-life. Every
    /// semantic assertion above these two scalar pins passed: all 22 combat
    /// actors died, all five trigger events dispatched, wave 2 recorded six
    /// kills / six damaged spawns / 6,000 damage, the abort stayed false, and
    /// objective 4 completed. The trajectory moved because all three modeled
    /// player weapons now use <c>CBattleEngine::GetLaunchPosition</c>'s retained
    /// camera-ray-to-cockpit-emitter adjustment before their unchanged scatter.
    /// This is the one terminal measurement authorized by the run's process-loop
    /// guard; independent repetition is required before integration.</para>
    /// </summary>
    [Fact]
    public void ChainAutopilot_ReachesWonByInputAlone()
    {
        Level100ChainAutopilot driver = _chain.Driver;
        Level100MissionOutcome outcome = _chain.Outcome;
        foreach (string line in driver.Report)
        {
            _output.WriteLine(line);
        }

        WorldSnapshot final = driver.Snapshot;
        Assert.Equal(Level100MissionOutcome.Won, outcome);
        Assert.Equal(Level100MissionOutcome.Won, final.Level100Mission.Outcome);

        // Beats 1-5: every authored and spawned ground target the script
        // activates is destroyed by the player's rounds.
        foreach (string name in new[]
        {
            "Target Tank 2", "Target Tank 3", "Target Warehouse", "Target Tank #23",
        })
        {
            Assert.Equal(
                Level100ActorLifecycle.Destroyed,
                final.Level100Actors.Actors
                    .Single(actor => actor.Name == name).Lifecycle);
        }

        Assert.Equal(
            3,
            CountDestroyed(final, Level100MissionTargetGroup.TargetTrucks));
        Assert.Equal(
            6,
            CountDestroyed(final, Level100MissionTargetGroup.MovingTargets));

        // Beat 7: the first drone wave, which only the jet's Mech Vulcan Cannon
        // can touch.
        Assert.Equal(
            3,
            CountDestroyed(final, Level100MissionTargetGroup.AirborneTargets1));

        // Beats 6, 8 and 10: all three volumes were entered, and each one only
        // dispatches out of jet mode.
        foreach (Level100MissionTrigger trigger in new[]
        {
            Level100MissionTrigger.TargetZone1,
            Level100MissionTrigger.FiringRange,
            Level100MissionTrigger.TargetZone2,
            Level100MissionTrigger.TargetZone3,
            Level100MissionTrigger.TargetZone4,
        })
        {
            Assert.True(
                final.Level100Actors.Actors
                    .Single(actor => actor.Trigger == trigger).TriggerEventDispatched,
                $"{trigger} never dispatched.");
        }

        // Beat 9's numbers are REPORTED BEFORE they are asserted, deliberately.
        // They used to be written out below the first beat-9 assertion, so a
        // run that tripped that assertion printed nothing about why - which is
        // exactly the shape that cost a re-run during the 2026-08-01
        // re-derivation.
        _output.WriteLine(
            $"beat 9: kills=" +
            $"{CountDestroyed(final, Level100MissionTargetGroup.AirborneTargets2)} " +
            $"damage={driver.WaveTwoDamageDealt} " +
            $"spawnsDamaged={driver.WaveTwoSpawnsDamaged} " +
            $"aborted={final.Level100Mission.Aborted} " +
            $"objective4=" +
            $"{final.Level100Mission.PrimaryObjectives.Single(objective => objective.Objective == 4).Status}");

        // The wave ends on one of its two released branches, whichever the
        // run's combat produces: six kills complete objective 4, or the
        // sub-40 % poll aborts it with the survivors switched off and
        // friendly. The RE contract classes the six-kill result as a driver
        // expectation, not a retail invariant.
        Level100FinalWaveContract.AssertReleasedBranch(final);
        // September 26 readings after the load took retail's construction
        // draws (the base world's pines and rows, the Battle Engine, the level
        // units) and every unit's recurring 4003, AI, fire-control and squad
        // draws, player rounds left their weapons' cockpit Gun emitters, every
        // round moved on its own MOVE and life events, the level took its
        // three-second pre-run, every script started on its INIT_SCRIPT, every
        // waypoint walk started at the unit's nearest node and followed the
        // nodes' own targets at their load-time heights, the fixture's
        // scriptless U-17 and Air Trainer thought with their classes' AI
        // cadences (CDropshipAI, CPlaneAI), the walker dashed only inside
        // retail's float32 window, and the air units flew their move orders
        // (slot 61) and left on Retreat: the wave ends on the abort branch
        // with three kills. These are reconstruction fixture readings, not
        // retail timing, hull or branch.
        Assert.True(final.Level100Mission.Aborted);
        Assert.Equal(3, CountDestroyed(final, Level100MissionTargetGroup.AirborneTargets2));
        Assert.Equal(5_961, final.Tick);
        Assert.Equal(7_300, final.Hull);
    }

    /// <summary>
    /// Match completed Blaster launches to production damage and disappearance
    /// receipts across the crabbing and stationary controls. Both hits and
    /// misses must be exercised. Launch velocity/range ratios are not contact
    /// invariants: the finite cylinder, scatter and subsequent movement can
    /// change outcomes, so an earlier sortie's hit percentages are not gates.
    /// </summary>
    [Fact]
    public void BlasterDamageMatchesCausalRoundReceipts()
    {
        IReadOnlyList<Level100ChainAutopilot.ObservedBlaster> blasters =
        [
            .. _abortControl.Driver.Blasters,
            .. _abortNoCrab.Driver.Blasters,
        ];
        Assert.All(blasters, shot => Assert.True(shot.Hit.HasValue,
            $"Direct-host Blaster {shot.RoundId} has no causal outcome receipt."));
        Assert.Contains(blasters, shot => shot.Hit == true);
        Assert.Contains(blasters, shot => shot.Hit == false);

        // The hit label is causal rather than geometric: a 200-damage event on
        // tick T carries the exact internal identity of its actor round, and
        // that named round must disappear on T. Other rounds may independently
        // expire on the same tick. Reconstructed closest approach and cylinder
        // contact remain diagnostics only; neither is allowed to define the
        // production outcome it audits.
        static int ReportEventAccounting(
            string tag,
            Level100ChainAutopilot driver,
            ITestOutputHelper output)
        {
            int damageEvents = driver.PlayerDamageEvents.Count(damage =>
                damage.Source == Level100PlayerDamageSource.ActorRound &&
                damage.IncomingDamageMilliLife == 200);
            Level100ChainAutopilot.ObservedBlaster[] hits = driver.Blasters
                .Where(shot => shot.Hit == true)
                .ToArray();
            int reconstructedContacts = driver.Blasters.Count(shot =>
                shot.ReconstructedCylinderContact);
            int hitWithoutReconstructedContact = hits.Count(shot =>
                !shot.ReconstructedCylinderContact);
            int reconstructedContactWithoutHit = driver.Blasters.Count(shot =>
                shot.ReconstructedCylinderContact && shot.Hit == false);
            output.WriteLine(
                $"{tag}: damage={damageEvents} causalHits={hits.Length} " +
                $"reconstructedCylinderContacts={reconstructedContacts} " +
                $"hitWithoutReconstruction={hitWithoutReconstructedContact} " +
                $"reconstructionWithoutHit={reconstructedContactWithoutHit}");
            Assert.Equal(hits.Select(hit => hit.RoundId).Distinct().Count(), hits.Length);
            return damageEvents;
        }

        int controlDamageEvents = ReportEventAccounting(
            "abortControl",
            _abortControl.Driver,
            _output);
        int noCrabDamageEvents = ReportEventAccounting(
            "abortNoCrab",
            _abortNoCrab.Driver,
            _output);
        Assert.True(
            controlDamageEvents + noCrabDamageEvents > 0,
            "The two controls produced no Blaster damage boundary.");
    }

    /// <summary>
    /// <c>SetAIState(AI_OFF)</c> silences a unit's weapons.
    ///
    /// <para><b>Why this test exists, and the trap it closes.</b> The AI_OFF
    /// gate in <c>Level100ActorWeaponRuntime.AdvanceActorWeapons</c> was once
    /// believed inert, because adding it left the chain trace byte-identical:
    /// hull 17500 / 16300 / 11200 / 7500 and <c>Won</c> at t10504, unchanged.
    /// It was not inert. <b>The chain's last hull checkpoint is t10028 and the
    /// abort lands at t10030</b>, so a real, load-bearing behaviour change was
    /// invisible to every number anyone was looking at. Nothing in this suite
    /// observed anything after the abort. This test is that observable, and it
    /// is deliberately the only assertion here that lives past t10030.</para>
    ///
    /// <para><b>The law, not a number.</b> The assertion is "an actor whose
    /// <c>AiState</c> is <c>AI_OFF</c> launches no further rounds", not a
    /// pinned hull figure. Hull values move whenever any damage constant moves,
    /// and a test pinned to 6700-versus-6500 gets deleted rather than fixed.
    /// Rounds already in flight at the abort are unaffected and are expected to
    /// keep arriving - the released gate stops the weapon, not the
    /// ammunition.</para>
    ///
    /// <para>The mechanism is <c>AirborneDrone2.msl</c>: <c>init()</c> issues
    /// <c>Attack(player)</c> at line 26, and
    /// <c>event("Abort Airborne Drones")</c> at lines 40-43 answers with
    /// <c>SetAIState(AI_OFF)</c>. LevelScript's beat-9 health poll posts that
    /// event below 40 % hull.</para>
    /// </summary>
    [Fact]
    public void AbortAirborneDrones_SilencesTheDronesThatWereAttacking()
    {
        Level100ChainAutopilot driver = _abortControl.Driver;
        int abortTick = Assert.IsType<int>(driver.AbortTick);

        // The test is only meaningful if the abort actually put a *firing*
        // actor into AI_OFF. Without this, a run that never reached beat 9
        // would pass vacuously.
        Level100ActorCommandIntentSnapshot[] silenced =
            driver.MechanicsAtAbort
                .Where(actor =>
                    actor.AiState == SimulationConstants.ReleasedAiStateOff &&
                    driver.RoundLaunches.Any(launch => launch.Tick <= abortTick &&
                        launch.OwnerActorId == actor.ActorId.Value))
                .ToArray();
        _output.WriteLine(
            $"abort at t{abortTick}; AI_OFF owners that fired before abort: " +
            string.Join(", ", silenced.Select(actor => actor.ActorId.Value)));
        Assert.NotEmpty(silenced);
        // Retail SetAIState(1), 0x4fdcb0, also clears the controller's target
        // reader and attack flag. Its post-abort intent is no longer Attacking.
        Assert.All(silenced, actor => Assert.Null(actor.TargetActorId));

        // Every round that was ever launched by an AI_OFF owner after the
        // abort. The released gate makes this set empty.
        Level100ChainAutopilot.ObservedRoundLaunch[] violations =
            driver.RoundLaunches
                .Where(launch =>
                    launch.Tick > abortTick &&
                    launch.OwnerAiState == SimulationConstants.ReleasedAiStateOff)
                .ToArray();
        foreach (Level100ChainAutopilot.ObservedRoundLaunch launch in violations)
        {
            _output.WriteLine(
                $"  t{launch.Tick} round {launch.RoundId} launched by actor " +
                $"{launch.OwnerActorId} while AiState=AI_OFF");
        }

        Assert.True(
            violations.Length == 0,
            $"{violations.Length} round(s) were launched after t{abortTick} by " +
            "actors the released script had already put into AI_OFF. " +
            "SetAIState(AI_OFF) must silence a unit's weapons - see " +
            "Level100ActorWeaponRuntime.AdvanceActorWeapons.");

        // And the control: the same drones did fire before the abort, so the
        // gate is what emptied the set above rather than the run never having
        // armed them.
        Assert.Contains(
            driver.RoundLaunches,
            launch => launch.Tick <= abortTick &&
                silenced.Any(actor => actor.ActorId.Value == launch.OwnerActorId));
    }

    /// <summary>
    /// The weapon-fire cue is emitted once per weapon RELEASE, never once per
    /// ROUND.
    ///
    /// <para><b>Why this is the assertion.</b> Retail issues exactly one
    /// <c>CSoundManager::PlayEffect</c> per launch instant and then spawns the
    /// whole volley. Byte-verified in the pristine specimen
    /// (<c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, sha256
    /// <c>74154bfa…</c> - not the installed executable, which is patched):
    /// <c>ProjectileBurst__SpawnFromCurrentPreset</c> at <c>0x005069f0</c>
    /// calls <c>0x004e1940</c> at <c>0x00506a96</c>, loads
    /// <c>[weaponMode+0x48]</c> (CWeaponVolleySize) at <c>0x00506a9b</c>, and
    /// only then enters the spawn loop whose head is <c>0x00506aaa</c> - the
    /// target of the back edge <c>JL</c> at <c>0x0050788b</c>. The call is
    /// outside the loop.</para>
    ///
    /// <para><b>Why it needs the full chain.</b> The Pulse Cannon's volley size
    /// is 1, so the firing range cannot tell the two laws apart. The Twin
    /// Vulcan's is 4 and the jet Mech Vulcan's is 2, and only a run that
    /// reaches beats 4 and 7 fires them. The discriminating case is asserted to
    /// have actually occurred rather than assumed, so this cannot pass
    /// vacuously on a run that only ever tapped the Pulse Cannon.</para>
    /// </summary>
    [Fact]
    public void PlayerWeaponFire_IsOneEventPerReleaseAndNotOnePerRound()
    {
        Level100ChainAutopilot.ObservedPlayerWeaponRelease[] releases =
            _chain.Driver.PlayerWeaponReleases.ToArray();
        Assert.NotEmpty(releases);

        foreach (Level100ChainAutopilot.ObservedPlayerWeaponRelease release in releases)
        {
            // TryFire admits at most one weapon per tick, so a tick that
            // carries two events is a producer emitting per round.
            Level100WeaponFireEvent fired = Assert.Single(release.Events);
            Assert.Equal(release.Tick, fired.Tick);
            Assert.NotEqual(Level100PlayerWeapon.None, fired.Weapon);

            // The one event accounts for the whole volley: RoundCount is what
            // the release created, and the projectile-id watermark agrees.
            if (release.RoundsCreated >= 0)
            {
                Assert.Equal(fired.RoundCount, release.RoundsCreated);
            }
        }

        // The volley weapons the law is about were actually fired. Without
        // this, a run that never left the Pulse Cannon would pass on a
        // per-round producer too.
        Level100WeaponFireEvent[] fireEvents = releases
            .SelectMany(release => release.Events)
            .ToArray();
        _output.WriteLine(
            "releases by weapon: " +
            string.Join(
                ", ",
                fireEvents
                    .GroupBy(item => item.Weapon)
                    .OrderBy(group => group.Key)
                    .Select(group =>
                        $"{group.Key}x{group.Count()} " +
                        $"volley={string.Join("/", group.Select(item => item.RoundCount).Distinct().Order())}")));

        Assert.Contains(
            fireEvents,
            item => item.Weapon == Level100PlayerWeapon.MechTwinVulcanCannon &&
                item.RoundCount == SimulationConstants.TwinVulcanVolleySize);
        Assert.Contains(
            fireEvents,
            item => item.Weapon == Level100PlayerWeapon.MechVulcanCannon &&
                item.RoundCount == SimulationConstants.MechVulcanVolleySize);
        Assert.Contains(
            fireEvents,
            item => item.Weapon == Level100PlayerWeapon.PulseCannonPod &&
                item.RoundCount == 1);
    }

    private static int CountDestroyed(
        WorldSnapshot state,
        Level100MissionTargetGroup group) =>
        state.Level100Actors.Actors.Count(actor =>
            actor.TargetGroup == group &&
            actor.Lifecycle == Level100ActorLifecycle.Destroyed);
}
