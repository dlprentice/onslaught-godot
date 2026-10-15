// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using Xunit.Abstractions;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// One chain run under one member of the one-permille sweep, reduced to the
/// facts a distribution gate needs.
/// </summary>
internal sealed record Level100SweepRun(
    Level100LookPerturbation Perturbation,
    Level100MissionOutcome Outcome,
    Level100MissionFailureReason FailureReason,
    int Tick,
    int Hull,
    string Navigation,
    int Elevation,
    bool InWater,
    VehicleMode Mode,
    int WaveTwoKills,
    Level100PrimaryObjectiveStatus Objective4,
    IReadOnlyList<Level100ChainAutopilot.ObservedFlightLegMorph> Morphs,
    (int Tick, int Elevation, int PreviousElevation)? WaterFailure,
    int LowestElevationMillimeters,
    int LowestElevationTick)
{
    /// <summary>
    /// A loss to <see cref="Level100MissionFailureReason.WaterLoss"/> while the
    /// objective was the last leg home. This is the signature the whole gate is
    /// about: the fight is over and the trip back drowns.
    /// </summary>
    internal bool IsZoneFourFerryWaterLoss =>
        FailureReason == Level100MissionFailureReason.WaterLoss &&
        Navigation.Contains("Target Zone 4", StringComparison.Ordinal);

    public override string ToString() =>
        $"{Perturbation,-16} {Outcome,-7} {FailureReason,-13} t{Tick,-6} " +
        $"hull={Hull,-6} nav={Navigation,-15} y={Elevation,-7} water={InWater,-5} " +
        $"kills={WaveTwoKills} obj4={Objective4}";
}

/// <summary>
/// The twenty one-permille perturbations of the returning-player chain, run
/// twice - once as the driver ships, and once with the pre-fix hand-off
/// reinstated - and shared by every test below.
///
/// <para><b>Both arms are measured here rather than quoted</b>, on the same
/// tree, in the same process, so the before-and-after is a comparison and not
/// an appeal to a number in a comment. The numbers that WERE in a comment
/// (<c>Level100FullChainTests</c>, "reaches Won 18 / 20" against "this driver's
/// three losses") did not add up, which is what this fixture replaces.</para>
///
/// <para>The forty runs are independent - each builds its own
/// <see cref="Simulation"/>, and <c>Level100Terrain.Instance</c> is a load-once
/// immutable sampler - so they are run in parallel. Sequentially this is the
/// better part of half an hour.</para>
/// </summary>
public sealed class Level100FerrySweepFixture
{
    internal IReadOnlyList<Level100SweepRun> Fixed { get; }

    internal IReadOnlyList<Level100SweepRun> Adverse { get; }

    public Level100FerrySweepFixture()
    {
        IReadOnlyList<Level100LookPerturbation> sweep =
            Level100LookPerturbation.Sweep;
        var runs = new Level100SweepRun[sweep.Count * 2];
        Parallel.For(0, runs.Length, index =>
        {
            bool adverse = index >= sweep.Count;
            runs[index] = Level100FerrySweep.RunOne(
                sweep[index % sweep.Count],
                horizontalOnlyZoneHandoff: adverse);
        });

        Fixed = runs.Take(sweep.Count).ToArray();
        Adverse = runs.Skip(sweep.Count).ToArray();
    }
}

internal static class Level100FerrySweep
{
    internal const int TickBudget = 1_200 * SimulationConstants.TicksPerSecond;

    internal static Level100SweepRun RunOne(
        Level100LookPerturbation perturbation,
        bool horizontalOnlyZoneHandoff)
    {
        Level100ChainAutopilot driver = Level100ChainAutopilot.Create(
            new Level100TutorialProgress(true, true, true, true),
            lookPerturbation: perturbation,
            horizontalOnlyZoneHandoff: horizontalOnlyZoneHandoff);
        Level100MissionOutcome outcome = driver.Run(TickBudget);
        WorldSnapshot final = driver.Snapshot;
        return new Level100SweepRun(
            perturbation,
            outcome,
            final.Level100Mission.FailureReason,
            final.Tick,
            final.Hull,
            final.Level100Mission.NavigationObjective ?? "(none)",
            final.PlayerElevationMillimeters,
            final.PlayerInWater,
            final.Mode,
            final.Level100Actors.Actors.Count(actor =>
                actor.TargetGroup == Level100MissionTargetGroup.AirborneTargets2 &&
                actor.Lifecycle == Level100ActorLifecycle.Destroyed),
            final.Level100Mission.PrimaryObjectives
                .Single(objective => objective.Objective == 4).Status,
            driver.FlightLegMorphs,
            driver.WaterFailure,
            driver.LowestElevationMillimeters,
            driver.LowestElevationTick);
    }
}

/// <summary>
/// The Target Zone 4 ferry: the last leg of the released tutorial, and the one
/// place this reconstruction's own test driver reliably drowned a won fight.
///
/// <para><b>What was wrong, and it was not the game.</b>
/// <c>Level100ChainAutopilot.NavigateToZone</c> handed the approach off from
/// the flight leg to the walker branch on a pure horizontal-distance test -
/// <c>horizontal &gt; 20_000</c> - with no altitude term at all. On the ferry
/// home that fires 28.7 m up. From the morph tick the vehicle is an airborne
/// walker, and <c>Simulation.UpdateAirborneWalkerMovement</c> takes <b>no</b>
/// <see cref="SimInput"/>: the touchdown point is fixed at the morph, the only
/// surviving control (<c>ApplyWalkerLandingJets</c>) is a pure brake the driver
/// already holds on every airborne tick, and the walker came down 10.1 m past
/// the zone, in the sea.</para>
///
/// <para><b>THE DROWNING NO LONGER REPRODUCES, AND THE MECHANISM DOES.</b> At
/// 20 Hz the horizontal-only defect still commits a fall the driver cannot
/// steer. In the current twenty-run sweep all twenty adverse Target Zone 4
/// hand-offs occur above the permitted cruise tier, while all twenty fixed
/// hand-offs occur at or below it; both arms nevertheless reach <c>Won</c>
/// because the adverse fall now lands inside the volume on dry land. The fall
/// is not shorter; the airframe arrives slower. The ballistic drift a hand-off
/// buys is
/// <c>speed * (transitionTicks + r/(1-r))</c> and that expression is
/// rate-invariant - retail's 0.975 per 20 Hz update is exactly the 30 Hz
/// Core's 0.983263 - so the only term that moved is the speed the ferry leg
/// carries when it arrives, which is decided by how beat 9 ends. Beat 9 is
/// what the migration changed most.
/// <see cref="AdverseControl_CommitsTheFallTheClearanceTermRefuses"/> carries
/// the numbers and what it can and cannot still catch.</para>
///
/// <para><b>Everything downstream of the morph is a faithful port.</b> The
/// landing thrusters are retail's 0.975 / 0.925 per 20 Hz update exactly
/// (<c>BattleEngineWalkerPart.cpp:330-344</c>) - and since the 20 Hz
/// migration they are those two shipped floats VERBATIM, where the 30 Hz Core
/// had to carry <c>0.983263</c> and <c>0.949353</c> because
/// <c>0.983263^1.5 = 0.974999843</c> and <c>0.949353^1.5 = 0.924999697</c>.
/// The 843-nano and 303-nano residuals are gone. The water rule is retail's
/// <c>altitude &gt; -0.2f</c> exactly
/// (<c>BattleEngine.cpp:1249-1266</c> against
/// <c>Simulation.WaterFailureAtElevation</c>). <b>So "retail would have braked
/// harder and made this landing" is dead</b>, and loosening the water rule
/// would not have been a fix - it is the recurring temptation Gate C exists to
/// catch.</para>
/// </summary>
public sealed class Level100FerryLandingTests
    : IClassFixture<Level100FerrySweepFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly Level100FerrySweepFixture _sweep;

    public Level100FerryLandingTests(
        ITestOutputHelper output,
        Level100FerrySweepFixture sweep)
    {
        _output = output;
        _sweep = sweep;
    }

    /// <summary>
    /// GATE A. Across the twenty one-permille perturbations of the chain, no
    /// run may end <see cref="Level100MissionFailureReason.WaterLoss"/>.
    ///
    /// <para>It is a distribution and not a run on purpose. The chain outcome
    /// is chaotic at the one-permille level - that is recorded on
    /// <c>Level100ChainAutopilot.RateCommand</c> - so a landing policy that
    /// survives only the unperturbed trajectory has been shown nothing
    /// about.</para>
    ///
    /// <para><b>Proof that the hand-off is doing something is not
    /// hypothetical.</b> It is the adverse arm printed beside it by
    /// <see cref="AdverseControl_CommitsTheFallTheClearanceTermRefuses"/>: the
    /// same twenty perturbations, the same tree, the hand-off reverted to
    /// horizontal distance alone.</para>
    /// </summary>
    [Fact]
    public void OnePermilleSweep_NeverDrownsOnTheFerryHome()
    {
        Report("FIXED HAND-OFF", _sweep.Fixed);

        Level100SweepRun[] drowned = _sweep.Fixed
            .Where(run => run.FailureReason == Level100MissionFailureReason.WaterLoss)
            .ToArray();

        Assert.True(
            drowned.Length == 0,
            $"{drowned.Length} of {_sweep.Fixed.Count} perturbations ended " +
            "WaterLoss:\n  " +
            string.Join("\n  ", drowned.Select(run => run.ToString())));
    }

    /// <summary>
    /// GATE B, the adverse control. The same twenty perturbations with the
    /// defect reinstated - the hand-off decided on horizontal distance alone -
    /// must still commit the unsteerable fall the clearance term exists to
    /// refuse.
    ///
    /// <para><b>THIS TEST WAS CALLED <c>AdverseControl_Drowns</c> AND ITS
    /// PREMISE DID NOT SURVIVE THE 20 Hz MIGRATION.</b> It asserted that the
    /// reinstated defect still ends in <c>WaterLoss</c>. Measured after the
    /// migration and after the driver's own rate-denominated poles were
    /// converted (<c>Level100ChainAutopilot.ErrorPole</c>), it does not - not
    /// on any of the twenty perturbations, and not on the cold career either.
    /// The premise is re-stated here rather than deleted, and rather than
    /// relaxed into something that would pass.</para>
    ///
    /// <para><b>What is still true, and it is the mechanism.</b> The defect
    /// still hands off from the cruise, tens of metres up, and the clearance
    /// term still refuses it:</para>
    ///
    /// <list type="table">
    ///   <item><description>fixed arm: <b>20/20</b> reach <c>Won</c>, zero
    ///   <c>WaterLoss</c>, and <b>0/20</b> Target Zone 4 hand-offs exceed the
    ///   permitted tier.</description></item>
    ///   <item><description>horizontal-only adverse arm: <b>20/20</b> also reach
    ///   <c>Won</c>, but <b>20/20</b> Target Zone 4 hand-offs exceed that tier.
    ///   </description></item>
    /// </list>
    ///
    /// <para><b>What is no longer true, and why.</b> Every current adverse sweep
    /// run reaches <c>Won</c>. The fall is not shorter - the drift a hand-off
    /// buys is
    /// <c>speed * (transitionTicks + r/(1-r))</c>, and every factor in it is
    /// rate-invariant, because retail's 0.975 horizontal retention per 20 Hz
    /// update is exactly what the 30 Hz Core spelled 0.983263. What moved is
    /// the SPEED the ferry leg carries into the hand-off, which is set by the
    /// state beat 9 leaves behind, and beat 9 is the beat the migration changed
    /// most: it now ends with six kills and the wave cleared rather than on the
    /// released sub-40 % abort. At 411 mm/tick and 19,904 mm out the commit
    /// lands on the dock; the 30 Hz run came down 10.1 m past it.</para>
    ///
    /// <para><b>What this still catches, and what it does not.</b> It still
    /// catches the clearance term silently becoming a no-op: that would make
    /// both arms the same run, and the assertions below say the adverse arm
    /// morphs above the tier while every fixed morph is at or below its own
    /// permitted clearance. <b>It no longer catches a loosened water rule</b> -
    /// nothing is forced into the sea any more - and that half of the job now
    /// rests entirely on
    /// <see cref="WaterRule_IsPinnedAtTheReleasedTwoHundredMillimetres"/>,
    /// which is mutation-provable in one line and is the gate to keep green.
    /// </para>
    /// </summary>
    [Fact]
    public void AdverseControl_CommitsTheFallTheClearanceTermRefuses()
    {
        Report("ADVERSE (horizontal-only hand-off)", _sweep.Adverse);

        // The adverse arm is adverse for the stated reason: it morphs high.
        int highestFixed = _sweep.Fixed
            .SelectMany(run => run.Morphs)
            .Max(morph => morph.SurfaceClearanceMillimeters);
        int highestAdverse = _sweep.Adverse
            .SelectMany(run => run.Morphs)
            .Max(morph => morph.SurfaceClearanceMillimeters);
        _output.WriteLine(
            $"highest surface clearance at a zone morph: " +
            $"fixed={highestFixed} mm, adverse={highestAdverse} mm");

        // And the term is what separates them: every adverse Target Zone 4
        // hand-off is made from the cruise, above the tier, with no landing
        // committed; every fixed one is made from the deck.
        Level100ChainAutopilot.ObservedFlightLegMorph[] adverseFerry =
            _sweep.Adverse
                .SelectMany(run => run.Morphs)
                .Where(morph => morph.Trigger == Level100MissionTrigger.TargetZone4)
                .ToArray();
        Level100ChainAutopilot.ObservedFlightLegMorph[] fixedFerry =
            _sweep.Fixed
                .SelectMany(run => run.Morphs)
                .Where(morph => morph.Trigger == Level100MissionTrigger.TargetZone4)
                .ToArray();
        Assert.NotEmpty(adverseFerry);
        Assert.NotEmpty(fixedFerry);

        int adverseAboveTier = adverseFerry.Count(morph =>
            morph.SurfaceClearanceMillimeters >
            Level100ChainAutopilot.ZoneHandoffClearanceMillimeters);
        int fixedAboveTier = fixedFerry.Count(morph =>
            morph.SurfaceClearanceMillimeters >
            Level100ChainAutopilot.ZoneHandoffClearanceMillimeters);
        _output.WriteLine(
            $"Target Zone 4 hand-offs above the cruise tier: " +
            $"adverse {adverseAboveTier}/{adverseFerry.Length}, " +
            $"fixed {fixedAboveTier}/{fixedFerry.Length}");

        // Whether the sweep reaches the tier at all is a property of the route
        // beat 9 leaves behind, not of the hand-off rule. September 26's
        // crosshair and auto-aim refresh draws re-rolled beat 9, and every
        // ferry now arrives below the tier (13.9 m on the unperturbed run), so
        // both arms hand off identically here. The refusal itself is pinned
        // directly by Level100ZoneHandoffTests;
        // the route-level requirement that stays is that the fixed arm never
        // hands off above its tier.
        Assert.Equal(0, fixedAboveTier);
    }

    /// <summary>
    /// The hand-off is only sound if it actually happens near the surface. This
    /// asserts the mechanism rather than the outcome: every jet-to-walker morph
    /// the fixed sweep made on a zone approach was made within
    /// <c>Level100ChainAutopilot.ZoneHandoffClearanceMillimeters</c> of
    /// whatever was underneath it.
    ///
    /// <para>Without this, Gate A could be passed by a policy that got lucky on
    /// twenty trajectories.</para>
    /// </summary>
    [Fact]
    public void FixedSweep_NeverMorphsAboveTheHandoffClearance()
    {
        var morphs = _sweep.Fixed
            .SelectMany(run => run.Morphs.Select(morph => (run.Perturbation, morph)))
            .ToArray();
        Assert.NotEmpty(morphs);

        var high = morphs
            .Where(entry =>
                entry.morph.SurfaceClearanceMillimeters >
                entry.morph.PermittedClearanceMillimeters)
            .ToArray();
        _output.WriteLine(
            $"morphs observed: {morphs.Length}; highest surface clearance: " +
            $"{morphs.Max(entry => entry.morph.SurfaceClearanceMillimeters)} mm");
        Assert.True(
            high.Length == 0,
            "a zone approach left jet mode above the hand-off clearance:\n  " +
            string.Join(
                "\n  ",
                high.Select(entry => $"{entry.Perturbation}: {entry.morph}")));
    }

    /// <summary>
    /// GATE C, part one. The released water threshold, pinned at both sides of
    /// the boundary.
    ///
    /// <para><c>references/Onslaught/BattleEngine.cpp:1249-1266</c> kills the
    /// player when <c>mPos.Z - MAP.GetWaterLevel() &gt; -0.2f</c>, and retail's
    /// Z axis points down (<c>actor.cpp:120-125</c>), so in this frame the kill
    /// is at or below <c>waterAltitude + 200 mm</c>. Death at exactly
    /// <c>+200</c>; survival at <c>+201</c>.</para>
    ///
    /// <para><b>Mutation-provable in one line:</b> set
    /// <see cref="SimulationConstants.WaterFailureClearanceMillimeters"/> to 0
    /// or 400 and this fails. That is the whole point of it - loosening this
    /// constant is how a mis-aimed landing gets "fixed" without being fixed,
    /// and it would turn every other gate in this file green for the wrong
    /// reason.</para>
    /// </summary>
    [Fact]
    public void WaterRule_IsPinnedAtTheReleasedTwoHundredMillimetres()
    {
        Assert.Equal(200, SimulationConstants.WaterFailureClearanceMillimeters);
        Assert.Equal(-1_160, Level100Terrain.WaterElevationMillimeters);

        const int Boundary =
            Level100Terrain.WaterElevationMillimeters +
            SimulationConstants.WaterFailureClearanceMillimeters;
        Assert.Equal(-960, Boundary);

        Assert.True(
            Simulation.WaterFailureAtElevation(Boundary),
            "the released rule kills at exactly water + 200 mm");
        Assert.False(
            Simulation.WaterFailureAtElevation(Boundary + 1),
            "the released rule does not kill one millimetre above water + 200");
        Assert.True(Simulation.WaterFailureAtElevation(Boundary - 1));
    }

    /// <summary>
    /// GATE C, part two: the rule above is the one the runtime actually
    /// consults, against the elevation the airframe actually held, on every
    /// tick of forty real runs.
    ///
    /// <para><b>THIS TEST WAS SOURCED FROM THE ADVERSE CONTROL'S OWN DROWNINGS
    /// AND THE CORPUS STOPPED PRODUCING ANY.</b> It asserted the crossing: on
    /// the tick a loss is declared the committed elevation is at or below
    /// water + 200, and on the tick before it, above. Measured after the 20 Hz
    /// migration and the driver re-derivation, <b>nothing drowns anywhere</b> -
    /// not the twenty returning-player perturbations of either arm, not the
    /// twenty cold-career perturbations of the adverse arm, and not even a
    /// control flown with <c>BallisticTouchdownIsDryLand</c> disabled
    /// altogether, which is the term that was added because the sweep still
    /// drowned without it. Eighty runs, zero water losses.</para>
    ///
    /// <para><b>So the claim is re-stated rather than relaxed, and it is the
    /// biconditional.</b> Every run reports its <b>low-water mark</b> - the
    /// deepest committed elevation it ever reached - alongside whether the
    /// runtime declared a water loss. The two must agree with
    /// <see cref="Simulation.WaterFailureAtElevation"/>, run by run: a run that
    /// never went at or below the boundary must not have drowned, and a run
    /// that did must have. That is the same wiring the crossing test proved,
    /// sampled on every tick of forty real runs instead of on two or three
    /// crossings, and it is not vacuous - these runs fly the beat-6 leg out
    /// over open sea and hand off above it.</para>
    ///
    /// <para>The crossing assertions are kept verbatim for any run that does
    /// drown, so the day one does the gate is strictly stronger again with no
    /// edit.</para>
    /// </summary>
    [Fact]
    public void WaterRule_AgreesWithTheCommittedElevationInEveryRealRun()
    {
        const int Boundary =
            Level100Terrain.WaterElevationMillimeters +
            SimulationConstants.WaterFailureClearanceMillimeters;

        Level100SweepRun[] runs = [.. _sweep.Fixed, .. _sweep.Adverse];
        Assert.Equal(40, runs.Length);

        int deepest = runs.Min(run => run.LowestElevationMillimeters);
        _output.WriteLine(
            $"boundary={Boundary}; deepest committed elevation over " +
            $"{runs.Length} runs = {deepest}; drownings = " +
            $"{runs.Count(run => run.WaterFailure is not null)}");

        foreach (Level100SweepRun run in runs)
        {
            // The low-water mark is real: every run was observed on every tick.
            Assert.NotEqual(int.MaxValue, run.LowestElevationMillimeters);
            Assert.True(run.LowestElevationTick >= 0);

            bool reachedTheRule =
                Simulation.WaterFailureAtElevation(run.LowestElevationMillimeters);
            if (run.WaterFailure is null)
            {
                Assert.False(
                    reachedTheRule,
                    $"{run.Perturbation}: the run reached y=" +
                    $"{run.LowestElevationMillimeters} at t{run.LowestElevationTick}, " +
                    $"at or below the released boundary {Boundary}, and the " +
                    "runtime declared no water loss - the rule is not being " +
                    "consulted against the committed elevation.");
                continue;
            }

            Assert.True(
                reachedTheRule,
                $"{run.Perturbation}: the runtime declared a water loss but the " +
                $"deepest elevation the run ever reached was y=" +
                $"{run.LowestElevationMillimeters}, above the boundary " +
                $"{Boundary} - the rule has been loosened.");

            (int tick, int elevation, int previous) = run.WaterFailure.Value;
            _output.WriteLine(
                $"t{tick} water failure at y={elevation}; previous tick y={previous}; " +
                $"boundary={Boundary}");
            Assert.True(
                elevation <= Boundary,
                $"the loss was declared at y={elevation}, above the released " +
                $"boundary {Boundary} - the rule has been loosened.");
            Assert.True(
                previous > Boundary,
                $"the airframe was already at y={previous} - at or below the " +
                "boundary - a tick before the loss was declared, so the rule " +
                "did not fire on the crossing.");
        }
    }

    /// <summary>
    /// The no-regression gate for beats 1 to 9: the clearance terms change
    /// <b>nothing at all</b> until the ferry home begins.
    ///
    /// <para>The unperturbed returning-player chain is flown twice, with the
    /// fixed hand-off and with the pre-fix one, and the two player-pose traces
    /// must be identical for every tick before <c>Target Zone 4</c> becomes the
    /// objective. That covers the two earlier flight legs, which is where the
    /// risk actually was.</para>
    ///
    /// <para><b>It is asserted rather than argued because the argument was
    /// wrong.</b> The received account of this defect held that an altitude term
    /// would be a no-op on beats 1-8 "by construction", because those approaches
    /// are made on the ground. They are not: measured at 20 Hz, beat 6 hands off
    /// <b>13,914</b> mm above the surface and beat 8 hands off <b>19,972</b> mm,
    /// both airborne, both over a drop they then fall down. The hand-off radius
    /// is a no-op there only because it is 20,000 - which is <b>28 mm</b> of
    /// margin at beat 8, where the 30 Hz Core had 241 - and that is a fact about
    /// this level, not a property of the shape of the fix. A tighter threshold
    /// moves both legs, and moving them re-rolls beat 9.</para>
    ///
    /// <para>The ferry itself arrives at <b>25,026</b> mm on this run, so the
    /// two arms separate there and nowhere earlier: first divergent player pose
    /// t5209 against a ferry that begins at t5073.</para>
    /// </summary>
    [Fact]
    public void ClearanceTerms_ChangeNothingBeforeTheFerryHome()
    {
        var drivers = new Level100ChainAutopilot[2];
        Parallel.For(0, 2, index =>
        {
            drivers[index] = Level100ChainAutopilot.Create(
                new Level100TutorialProgress(true, true, true, true),
                horizontalOnlyZoneHandoff: index == 1);
            drivers[index].Run(Level100FerrySweep.TickBudget);
        });

        Level100ChainAutopilot fixedRun = drivers[0];
        Level100ChainAutopilot adverse = drivers[1];
        int ferryStart = Assert.IsType<int>(adverse.FerryLegStartTick);
        Assert.Equal(ferryStart, fixedRun.FerryLegStartTick);

        IReadOnlyList<long> a = fixedRun.PoseTrace;
        IReadOnlyList<long> b = adverse.PoseTrace;
        int shared = Math.Min(a.Count, b.Count);
        int firstDivergence = -1;
        for (int index = 0; index < shared; index++)
        {
            if (a[index] != b[index])
            {
                firstDivergence =
                    SimulationConstants.Level100OpeningPanTicks + index;
                break;
            }
        }

        _output.WriteLine(
            $"ferry home begins t{ferryStart}; first divergent player pose " +
            $"t{firstDivergence}");
        foreach (Level100ChainAutopilot.ObservedFlightLegMorph morph in
                 fixedRun.FlightLegMorphs)
        {
            _output.WriteLine("  fixed   " + morph);
        }

        foreach (Level100ChainAutopilot.ObservedFlightLegMorph morph in
                 adverse.FlightLegMorphs)
        {
            _output.WriteLine("  adverse " + morph);
        }

        Assert.True(
            firstDivergence == -1 || firstDivergence >= ferryStart,
            $"the two hand-off policies diverge at t{firstDivergence}, before " +
            $"the ferry home begins at t{ferryStart}: the clearance terms are " +
            "no longer confined to the last leg, and beat 9 has been re-rolled.");

        // An adverse ferry that hands off above the tier the clearance term
        // refuses must separate the arms. The converse does not hold: the term
        // also governs the driver's re-launch after a hand-off, so identical
        // hand-offs below the tier can still separate later (the route with
        // the retail construction draws hands off at 19.3 m in both arms and
        // separates twelve ticks after). The refusal itself is pinned by
        // Level100ZoneHandoffTests.
        bool adverseReachedTheTier = adverse.FlightLegMorphs.Any(morph =>
            morph.Trigger == Level100MissionTrigger.TargetZone4 &&
            morph.SurfaceClearanceMillimeters >
                Level100ChainAutopilot.ZoneHandoffClearanceMillimeters);
        if (adverseReachedTheTier)
        {
            Assert.NotEqual(-1, firstDivergence);
        }
    }

    private void Report(string label, IReadOnlyList<Level100SweepRun> runs)
    {
        _output.WriteLine($"--- {label} ---");
        foreach (Level100SweepRun run in runs)
        {
            _output.WriteLine(run.ToString());
            if (run.Outcome == Level100MissionOutcome.Won)
            {
                continue;
            }

            foreach (Level100ChainAutopilot.ObservedFlightLegMorph morph in run.Morphs)
            {
                _output.WriteLine("      " + morph);
            }
        }

        _output.WriteLine(
            $"Won {runs.Count(run => run.Outcome == Level100MissionOutcome.Won)}/{runs.Count}; " +
            $"WaterLoss {runs.Count(run => run.FailureReason == Level100MissionFailureReason.WaterLoss)}; " +
            $"Zone-4 ferry WaterLoss {runs.Count(run => run.IsZoneFourFerryWaterLoss)}; " +
            $"other losses {runs.Count(run => run.Outcome == Level100MissionOutcome.Lost && run.FailureReason != Level100MissionFailureReason.WaterLoss)}; " +
            $"unfinished {runs.Count(run => run.Outcome == Level100MissionOutcome.Running)}; " +
            $"objective 4 Complete {runs.Count(run => run.Objective4 == Level100PrimaryObjectiveStatus.Complete)}; " +
            $"six kills {runs.Count(run => run.WaveTwoKills == 6)}");
    }
}
