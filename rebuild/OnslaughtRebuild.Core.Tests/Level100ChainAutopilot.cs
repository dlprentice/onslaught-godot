// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

/// <summary>
/// A single deterministic autopilot for the whole released Level 100 chain.
///
/// <para><b>It drives <see cref="SimInput"/> only.</b> It posts no mission
/// event, writes no world state, and calls no measurement seam: the single Core
/// entry point it touches is <c>Simulation.Step(SimInput)</c>. Every beat it
/// reaches is produced by the released scripts running on the world state that
/// input creates.</para>
///
/// <para><b>It is input-equivalent to a player, not perception-equivalent.</b>
/// It reads <c>actor.Health</c> as an integer where a player reads a bar, and
/// it samples <see cref="Level100Terrain"/> directly where a player looks at a
/// hillside. That is stated here because the difference matters to anyone
/// citing this run as evidence of what a human can do.</para>
///
/// <para>The two things the earlier segment drivers lacked, and which are why
/// the chain stopped on beat 3's fourth target:</para>
/// <list type="number">
///   <item><description><b>A line-of-sight test before firing.</b> The old
///   driver walked to a fixed 18 m stand-off and fired regardless of what was
///   between it and the target. Against <c>Target Tank #23</c> that stand-off
///   is on a ridge 5.5 m above the parked tank, and the pitch that hits the
///   tank puts the round into the intervening crest: 3,578 consecutive rounds
///   landed on terrain. Adding target leading changed that by exactly zero
///   rounds, because leading moves the aim point and the problem was the
///   stance.</description></item>
///   <item><description><b>Repositioning when the stance does not work.</b> The
///   trigger is not a model of why - it is the fact a player uses, that the
///   target's health is not going down.</description></item>
///   <item><description><b>Fire discipline when a miss would hit something the
///   script has not armed yet.</b> See
///   <see cref="CollateralRiskBehind"/>.</description></item>
/// </list>
///
/// <para><b>This driver no longer works around the terrain-pitch sign
/// defect.</b> <c>Simulation.SampleTerrainPitchMicroRad</c> used to have the
/// opposite sign to the two released producers of that quantity, so a walker on
/// a bank could not aim downhill and an earlier revision of this driver walked
/// down the bank instead. With the sign corrected the walker aims downhill from
/// where it stands, and the bank-walking workaround has been removed: it now
/// stops closing and holds still for a precision shot instead.</para>
/// </summary>
internal sealed class Level100ChainAutopilot
{
    /// <summary>Terrain clearance a round needs along the whole ray.</summary>
    private const int LineOfSightClearanceMillimeters = 250;

    /// <summary>Samples taken along the firing ray.</summary>
    private const int LineOfSightSamples = 48;

    /// <summary>Preferred ground stand-off once the shot is clear.</summary>
    private const int GroundStandOffMillimeters = 14_000;

    /// <summary>
    /// Below this the autopilot stops closing and strafes instead: walking
    /// into the target does not clear a crest that is behind the target.
    /// </summary>
    private const int MinimumGroundStandOffMillimeters = 3_500;

    /// <summary>
    /// The stand-off a shot is taken from while <see cref="CollateralRiskBehind"/>
    /// is true, replacing <see cref="GroundStandOffMillimeters"/>.
    ///
    /// <para><b>Why closing is the answer, measured rather than assumed.</b> On
    /// a cold first career the driver reached <c>(21171, 79180)</c>, 13.5 m from
    /// <c>Target Tank #23</c> and 4.2 m above it, and held the trigger down with
    /// a converged reticle from t4716. <b>Every round it fired for the next
    /// twenty-five ticks missed the tank and hit
    /// <c>Target Truck #25</c></b> - which is parked 1.13 m beyond the tank
    /// along the shot line and 0.93 m off it - and the tank did not take its
    /// first damage until t4747, six ticks after the truck was destroyed
    /// unarmed and <c>TargetTruck1.msl</c>'s <c>died()</c> <c>case FALSE</c> arm
    /// had already posted <c>Broke Tutorial</c>.</para>
    ///
    /// <para>That is not a stray. At <c>atan(4241/13513)</c> = 0.304 rad of
    /// depression a round threads the gap over the tank's hull - its
    /// <c>tankbody</c> part tops out 514 mm above the actor origin and
    /// <see cref="AimPoint"/> aims at 600 mm - and is still 231 mm above the
    /// truck's origin when it arrives, inside the 188..762 mm band the truck's
    /// single collidable part occupies. Closing removes the gap: at short range
    /// the same aim height plunges through the hull, so every round is in the
    /// tank or in the dirt and the tank cannot help but die first.</para>
    ///
    /// <para><b>The number, and the cliff either side of it.</b> Beat 3 was run
    /// on the cold career at 3.6, 4, 5, 6, 8, 9, 10, 11, 12, 13 and 14 m. From
    /// 3.6 m to 10 m the truck is <b>never touched at all</b>; at 11 m and
    /// beyond it dies before the tank and the run is lost. Measured separately
    /// with the trigger gate alone, a shot taken at 8,992 mm is safe and one at
    /// 9,494 mm is not, so the transition is between 9.0 and 9.5 m of RANGE at
    /// this stance. 10 m is chosen and not a deeper 6 m because the returning
    /// player's own first beat-3 shot is at 8,204 mm, so 10 m is a measured
    /// <b>no-op</b> on <c>ChainAutopilot_ReachesWonByInputAlone</c> - trace for
    /// trace, <c>Won</c> at t16301 with 10,900 hull either way - while 6 m moves
    /// that run onto the released sub-40 % abort branch with two of six wave-2
    /// drones. The deeper constant is the better one for beat 3 and it may not
    /// be taken without weakening assertions elsewhere.</para>
    ///
    /// <para><b>It is not a lucky basin.</b> Under twenty +-1 permille
    /// perturbations of every look command at or above thresholds of 100 to
    /// 1000 - the generalisation of the "one permille shaved off every command
    /// &gt;= 500" control that broke this same guard before - the committed
    /// driver loses the cold career <b>20 of 20</b> times, and 20 of 20 again
    /// with the look axis restricted to whole mouse pixels. With this stand-off
    /// it is <b>20 of 20 clean</b> sub-pixel and <b>19 of 20</b> on whole
    /// pixels.</para>
    /// </summary>
    private const int PrecisionStandOffMillimeters = 10_000;

    /// <summary>
    /// How far outside <see cref="PrecisionStandOffMillimeters"/> the trigger
    /// may still open. The walker is still moving when it arrives, so a gate
    /// exactly on the stand-off costs shots without buying safety; half a metre
    /// is inside the measured transition by more than four metres.
    /// </summary>
    private const int PrecisionFireMarginMillimeters = 500;

    // 1.5 s of strafing per segment, stated against the rate so the driver
    // is not silently retuned by a Core tick-rate change.
    private const int StrafeSegmentTicks =
        3 * SimulationConstants.TicksPerSecond / 2;

    private readonly ILevel100ChainHost _host;
    private RetailCareerCampaign? _career;
    private readonly List<string> _log = [];
    private readonly SortedDictionary<int, int> _impactsByActor = [];
    private readonly Level100Terrain _terrain = Level100Terrain.Instance;

    private string? _lastNavigation;
    private int _lastDestroyed = -1;
    private Level100MissionOutcome _lastOutcome = Level100MissionOutcome.Running;
    private readonly HashSet<string> _seenEvents = [];

    private int _strafeTicksRemaining;
    private int _strafeDirection = 1;
    private int _blockedTicks;
    private int _repositionTicksRemaining;
    private Level100ActorId _engagedActorId;
    private int _engagedHealth;
    private int _engagedSinceTick;
    private int _lastProgressKey = int.MinValue;
    private int _lastProgressTick;
    private bool _flightLegCommittedToLanding;
    private bool _flightLegLaunched;
    private Level100MissionTrigger? _flightLegTrigger;

    private Level100ChainAutopilot(ILevel100ChainHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        if (host.Snapshot.Level100ActorMechanics.ActorRounds.Count != 0)
        {
            throw new ArgumentException(
                "The chain driver requires a fresh host with no live actor rounds.",
                nameof(host));
        }

        _nextActorRoundIdAtPreviousObservation =
            host.Snapshot.Level100ActorMechanics.NextActorRoundId;
    }

    internal IReadOnlyList<string> Report => _log;

    internal IReadOnlyDictionary<int, int> ImpactsByActor => _impactsByActor;

    internal WorldSnapshot Snapshot => _host.Snapshot;

    /// <summary>
    /// Continues the same host after <see cref="Run"/> has already left
    /// <c>Running</c>. The player-input Won e2e uses this for
    /// <see cref="SimInput.Idle"/> through the already-pinned 5.0 f overlay.
    /// </summary>
    internal WorldSnapshot Step(SimInput input) => FollowCareer(_host.Step(input));

    /// <summary>
    /// The career this run reaches, followed the way the frontend follows the
    /// host's mission events: each <c>SetSlotSave</c> stored at once, and the
    /// Won update applied once when the mission stands at its handoff, with
    /// FillOut's survivors from the end state. Bound only on the direct-Core
    /// <see cref="Create"/> path.
    /// </summary>
    internal RetailCareerCampaign Career =>
        _career ??
        throw new InvalidOperationException(
            "This driver has no live career; only Create() binds one.");

    private Level100ActorDefinitionSet? _careerDefinitions;
    private bool _careerUpdated;

    private WorldSnapshot FollowCareer(WorldSnapshot state)
    {
        if (_career is null)
        {
            return state;
        }

        foreach (Level100MissionEvent missionEvent in state.Level100MissionEvents)
        {
            if (missionEvent is Level100TutorialSlotSaved saved)
            {
                RetailSetSlotSave.PersistCareerSlot(_career.Slots, saved.Slot, saved.Value);
            }
        }

        if (!_careerUpdated)
        {
            _careerUpdated = Level100WonCareerHandoff.TryApply(
                _career,
                state.Level100Mission.Outcome,
                state.Level100Mission.TerminalState,
                RetailFillOutEndLevelData.BaseThingsLeft(state, _careerDefinitions!)) is not null;
        }

        return state;
    }

    /// <summary>
    /// One released actor round, recorded on the first tick it is visible in
    /// <c>Level100ActorMechanics.ActorRounds</c>, together with the
    /// <c>AiState</c> its owner held on that tick.
    /// </summary>
    internal readonly record struct ObservedRoundLaunch(
        int Tick,
        int RoundId,
        int OwnerActorId,
        int OwnerAiState);

    private readonly List<ObservedRoundLaunch> _roundLaunches = [];
    private readonly HashSet<int> _seenRoundIds = [];
    private HashSet<int> _liveRoundIdsAtPreviousObservation = [];
    private int? _abortTick;
    private IReadOnlyList<Level100ActorCommandIntentSnapshot> _mechanicsAtAbort =
        Array.Empty<Level100ActorCommandIntentSnapshot>();

    /// <summary>Every actor round this run ever saw, in first-seen order.</summary>
    internal IReadOnlyList<ObservedRoundLaunch> RoundLaunches => _roundLaunches;

    /// <summary>
    /// One simulated tick on which the player released a weapon, paired with
    /// the number of player rounds that tick actually created.
    ///
    /// <para><see cref="Events"/> is the whole ordered stream for the tick, not
    /// a count, so a producer that emitted one cue per ROUND instead of one per
    /// RELEASE is visible here rather than having to be inferred.</para>
    /// </summary>
    internal readonly record struct ObservedPlayerWeaponRelease(
        int Tick,
        IReadOnlyList<Level100WeaponFireEvent> Events,
        int RoundsCreated);

    private readonly List<ObservedPlayerWeaponRelease> _playerWeaponReleases = [];
    private int _lastObservedTick = -1;
    private int _lastObservedNextProjectileId;

    /// <summary>Every player weapon release this run saw, in tick order.</summary>
    internal IReadOnlyList<ObservedPlayerWeaponRelease> PlayerWeaponReleases =>
        _playerWeaponReleases;

    /// <summary>
    /// Pairs each tick's <c>Level100WeaponFireEvents</c> with the advance of
    /// <c>NextProjectileId</c> over the same tick. The id watermark is used
    /// rather than the live projectile list because a round can be created and
    /// expire without ever being sampled.
    /// </summary>
    private void RecordPlayerWeaponFire(WorldSnapshot state)
    {
        if (state.Tick == _lastObservedTick)
        {
            return;
        }

        int roundsCreated = state.NextProjectileId - _lastObservedNextProjectileId;
        bool contiguous = state.Tick == _lastObservedTick + 1;
        _lastObservedTick = state.Tick;
        _lastObservedNextProjectileId = state.NextProjectileId;
        if (state.Level100WeaponFireEvents.Count == 0)
        {
            return;
        }

        _playerWeaponReleases.Add(new ObservedPlayerWeaponRelease(
            state.Tick,
            state.Level100WeaponFireEvents,
            contiguous ? roundsCreated : -1));
    }

    /// <summary>
    /// The first tick on which the LevelScript's <c>aborted</c> local was set,
    /// i.e. the tick <c>event("Abort Airborne Drones")</c> landed.
    /// </summary>
    internal int? AbortTick => _abortTick;

    /// <summary>Actor mechanics state as it stood on <see cref="AbortTick"/>.</summary>
    internal IReadOnlyList<Level100ActorCommandIntentSnapshot> MechanicsAtAbort =>
        _mechanicsAtAbort;

    private readonly Dictionary<int, int> _waveTwoPeakHealth = [];
    private readonly Dictionary<int, int> _waveTwoLowestHealth = [];

    /// <summary>
    /// Total hull removed from the six <c>AirborneDrone2</c> spawns across the
    /// whole run, in the released health units the registry reports.
    ///
    /// <para>Pure observation, like <see cref="Blasters"/>: it reads only
    /// snapshot fields the driver already consults and is never fed back into a
    /// decision. A destroyed drone contributes its full life; a damaged one
    /// contributes the deepest cut it ever took, so a spawn that is destroyed
    /// after being wounded is not double-counted and one that is wounded and
    /// then vanishes from the snapshot is not lost.</para>
    ///
    /// <para><b>Why this exists.</b> It replaces the wave-2 <b>kill count</b> as
    /// the evidence that the driver is still fighting beat 9. The kill count is
    /// chaotic at a resolution finer than the simulation's own input
    /// quantisation - see the remarks on
    /// <c>Level100FullChainTests.ChainAutopilot_ReachesWonByInputAlone</c> - and
    /// damage dealt is not, because it accumulates every hit instead of only
    /// the last one of six.</para>
    /// </summary>
    internal int WaveTwoDamageDealt => _waveTwoPeakHealth
        .Sum(pair => pair.Value - _waveTwoLowestHealth[pair.Key]);

    /// <summary>How many wave-2 spawns took any damage at all.</summary>
    internal int WaveTwoSpawnsDamaged => _waveTwoPeakHealth
        .Count(pair => _waveTwoLowestHealth[pair.Key] < pair.Value);

    private void RecordWaveTwoDamage(WorldSnapshot state)
    {
        foreach (Level100ActorSnapshot actor in state.Level100Actors.Actors)
        {
            if (actor.TargetGroup != Level100MissionTargetGroup.AirborneTargets2)
            {
                continue;
            }

            int id = actor.ActorId.Value;
            int health = actor.Lifecycle == Level100ActorLifecycle.Destroyed
                ? 0
                : actor.Health;
            if (!_waveTwoPeakHealth.TryGetValue(id, out int peak) || health > peak)
            {
                _waveTwoPeakHealth[id] = Math.Max(peak, health);
            }

            if (!_waveTwoLowestHealth.TryGetValue(id, out int lowest) || health < lowest)
            {
                _waveTwoLowestHealth[id] = health;
            }
        }
    }

    /// <summary>
    /// Records the abort boundary and every round launch either side of it.
    ///
    /// <para>This is always on, and it is the only observable this run carries
    /// that is sensitive to anything happening <b>after</b> the abort. See
    /// <c>Level100FullChainTests.AbortAirborneDrones_SilencesTheDronesThatWereAttacking</c>
    /// for why that matters.</para>
    /// </summary>
    private void RecordArmamentEvidence(WorldSnapshot state)
    {
        if (_abortTick is null && state.Level100Mission.Aborted)
        {
            _abortTick = state.Tick;
            _mechanicsAtAbort = state.Level100ActorMechanics.Actors;
        }

        int previousNextActorRoundId = _nextActorRoundIdAtPreviousObservation;
        int nextActorRoundId = state.Level100ActorMechanics.NextActorRoundId;
        if (nextActorRoundId < previousNextActorRoundId)
        {
            throw new InvalidOperationException(
                "The actor-round identity allocator moved backwards.");
        }

        Dictionary<int, Level100ActorRoundSnapshot> liveRoundsById =
            state.Level100ActorMechanics.ActorRounds.ToDictionary(round => round.Id);
        foreach (Level100ActorRoundSnapshot round in liveRoundsById.Values)
        {
            if (round.Id >= nextActorRoundId)
            {
                throw new InvalidOperationException(
                    $"Live actor round {round.Id} is not below NextActorRoundId {nextActorRoundId}.");
            }
        }

        for (int roundId = previousNextActorRoundId;
             roundId < nextActorRoundId;
             roundId++)
        {
            if (!liveRoundsById.TryGetValue(roundId, out Level100ActorRoundSnapshot? round) ||
                round.ElapsedBaseTicks != 0)
            {
                throw new InvalidOperationException(
                    $"New actor round {roundId} was not published exactly once at elapsed tick zero.");
            }
        }

        _nextActorRoundIdAtPreviousObservation = nextActorRoundId;

        Dictionary<int, int> aiStateByActor = state.Level100ActorMechanics.Actors
            .ToDictionary(actor => actor.ActorId.Value, actor => actor.AiState);
        foreach (Level100ActorRoundSnapshot round in
                 state.Level100ActorMechanics.ActorRounds)
        {
            if (_liveRoundIdsAtPreviousObservation.Contains(round.Id))
            {
                continue;
            }

            if (round.Id < previousNextActorRoundId || round.Id >= nextActorRoundId)
            {
                throw new InvalidOperationException(
                    $"Newly appearing actor round {round.Id} falls outside the allocator's " +
                    $"new interval [{previousNextActorRoundId}, {nextActorRoundId}).");
            }

            if (!_seenRoundIds.Add(round.Id))
            {
                throw new InvalidOperationException(
                    $"Actor-round identity {round.Id} was reused after disappearance.");
            }

            _roundLaunches.Add(new ObservedRoundLaunch(
                state.Tick,
                round.Id,
                round.OwnerActorId.Value,
                aiStateByActor.TryGetValue(round.OwnerActorId.Value, out int ai)
                    ? ai
                    : SimulationConstants.ReleasedAiStateOn));
        }

        _liveRoundIdsAtPreviousObservation = liveRoundsById.Keys.ToHashSet();
    }

    /// <summary>
    /// A run with all four released tutorial slots already saved
    /// (<c>SLOT_TUTORIAL_1..4</c>). That is a returning player, not a cold
    /// first career: it skips the <c>GetSlot(...) == FALSE</c> arms, which are
    /// the HUD lectures and their <c>player.Deactivate()</c> /
    /// <c>player.Activate()</c> theatre. **It skips no kill gate and no
    /// trigger** - every `numTargets` countdown and every volume test is
    /// outside those arms. Stated because it changes what this run is evidence
    /// of.
    /// </summary>
    internal static Level100ChainAutopilot Create() =>
        Create(new Level100TutorialProgress(true, true, true, true));

    /// <summary>
    /// The same driver on a chosen tutorial-slot state. <c>default</c> is the
    /// cold first career - all four <c>SLOT_TUTORIAL_*</c> unsaved - which is
    /// what the client's own New Game path produces and what
    /// <see cref="CreateOnClientSession"/> therefore runs.
    /// </summary>
    internal static Level100ChainAutopilot Create(
        Level100TutorialProgress progress,
        bool quantizeLookToClientPointerPath = false,
        uint seed = 1u,
        bool quantizeLookToIntegerMousePixels = false,
        Level100LookPerturbation? lookPerturbation = null,
        bool horizontalOnlyZoneHandoff = false,
        Level100ActorDefinitionSet? actorDefinitions = null)
    {
        // The seed is snapshot and hash material only - the released gameplay
        // RNG is `ReleasedRandomSeed`, reseeded to 123456 at level start - but a
        // run compared against another run has to match on it anyway, or the
        // state hashes differ for a reason that is not the thing being measured.
        Level100ActorDefinitionSet definitions = actorDefinitions ?? Level100TestActorDefinitions.Create();
        var simulation = new Simulation(seed, definitions, progress);
        Level100ChainAutopilot driver = CreateOn(
            new Level100DirectChainHost(
                simulation,
                quantizeLookToClientPointerPath,
                quantizeLookToIntegerMousePixels));
        driver._career = RetailCareerReCalcLinks.CreateColdTrainingSlice();
        driver._careerDefinitions = definitions;
        driver._horizontalOnlyZoneHandoff = horizontalOnlyZoneHandoff;
        driver._beatNinePerturbation = lookPerturbation;
        return driver;
    }

    /// <summary>
    /// The same driver, delivering every decision through the shipping client's
    /// player-input surface instead of straight into Core.
    /// </summary>
    internal static Level100ChainAutopilot CreateOnClientSession(
        Level100InteractiveChainHost host) => CreateOn(host);

    /// <summary>
    /// Idles through the released opening pan and hands back a driver bound to
    /// <paramref name="host"/>. The pan is spent as input, not skipped: it is
    /// <see cref="SimInput.Idle"/> for
    /// <see cref="SimulationConstants.Level100OpeningPanTicks"/> ticks, which is
    /// what a player who does not touch the controls supplies, and
    /// <c>CPlayer__ReceiveButtonAction</c> rejects everything else that early
    /// anyway.
    /// </summary>
    private static Level100ChainAutopilot CreateOn(ILevel100ChainHost host)
    {
        for (int tick = 0; tick < SimulationConstants.Level100OpeningPanTicks; tick++)
        {
            host.Step(SimInput.Idle);
        }

        return new Level100ChainAutopilot(host);
    }

    /// <summary>
    /// The same run with the trigger held shut for the whole of beat 9.
    ///
    /// <para>This control deliberately exercises the authored low-hull abort
    /// and supplies Blaster outcomes independently of the main route's combat
    /// performance. It supports
    /// <see cref="Level100FullChainTests.AbortAirborneDrones_SilencesTheDronesThatWereAttacking"/>
    /// and
    /// <see cref="Level100FullChainTests.BlasterDamageMatchesCausalRoundReceipts"/>.</para>
    ///
    /// <para>The only difference is <see cref="SimActions.Fire"/> in beat 9.
    /// <c>Simulation.TryFire</c> precedes <c>UpdateMovement</c> and does not
    /// directly change motion state. This uses the same controller with its
    /// shots suppressed; later shared-RNG and world outcomes can differ.</para>
    /// </summary>
    internal static Level100ChainAutopilot CreateWithWaveTwoTriggerHeldShut()
    {
        Level100ChainAutopilot driver = Create();
        driver._waveTwoTriggerHeldShut = true;
        return driver;
    }

    private bool _waveTwoTriggerHeldShut;

    /// <summary>
    /// The same control with the beat-9 <b>evasive crab</b> also held at zero:
    /// a pilot who tracks the drones and shoots at nothing while flying
    /// straight through the wave.
    ///
    /// <para>Removing the crab supplies a distinct movement and impact path
    /// for <see cref="Level100FullChainTests.BlasterDamageMatchesCausalRoundReceipts"/>.
    /// Historical hit counts are not invariants; the test checks actual round
    /// identities and damage receipts in both controls.</para>
    /// </summary>
    internal static Level100ChainAutopilot CreateWithWaveTwoTriggerAndCrabHeldShut()
    {
        Level100ChainAutopilot driver = CreateWithWaveTwoTriggerHeldShut();
        driver._waveTwoCrabHeldStill = true;
        return driver;
    }

    private bool _waveTwoCrabHeldStill;

    /// <summary>Runs until the mission leaves <c>Running</c> or the budget ends.</summary>
    internal Level100MissionOutcome Run(int maximumTicks)
    {
        for (int tick = 0; tick < maximumTicks; tick++)
        {
            WorldSnapshot state = _host.Snapshot;
            Observe(state);
            if (state.Level100Mission.Outcome != Level100MissionOutcome.Running)
            {
                return state.Level100Mission.Outcome;
            }

            WorldSnapshot next = FollowCareer(_host.Step(NextInput(state)));
            foreach (Level100DestructionEvent destruction in next.Level100DestructionEvents)
            {
                if (destruction.Kind is not (
                        Level100DestructionEventKind.PulseImpact or
                        Level100DestructionEventKind.VulcanImpact))
                {
                    continue;
                }

                _impactsByActor.TryGetValue(destruction.ActorId, out int count);
                _impactsByActor[destruction.ActorId] = count + 1;
            }
        }

        WorldSnapshot final = _host.Snapshot;
        Observe(final);
        _log.Add(
            $"t{final.Tick} BUDGET EXHAUSTED outcome={final.Level100Mission.Outcome} " +
            $"nav={final.Level100Mission.NavigationObjective ?? "(none)"} " +
            $"mode={final.Mode} hull={final.Hull} pos=({final.PlayerPosition.X},{final.PlayerPosition.Z})");
        LogLiveObjectives(final);
        return final.Level100Mission.Outcome;
    }

    /// <summary>
    /// The last place the player stood on dry land. A sortie that chases a
    /// drone out over the sea and then puts down - or simply runs its store
    /// dry - is <c>Level100MissionFailureReason.WaterLoss</c>, which is a lost
    /// level at full hull. Measured: an earlier revision of this driver
    /// followed the third wave-1 drone west and drowned at (-107362, 52796)
    /// with 17,500 of 20,000 hull intact.
    /// </summary>
    private SimVector2 _lastDryGround;

    // ------------------------------------------------------------------
    // Blaster ballistics measurement
    //
    // Pure observation of snapshots the driver already reads. It changes no
    // input and is not consulted by any decision. Round identity, damage and
    // disappearance receipts establish hits; reconstructed geometry is diagnostic.
    // ------------------------------------------------------------------

    /// <summary>
    /// One <c>Blaster</c> aimed at the player, from launch to terminal
    /// disappearance by impact or expiry. A null hit outcome means the host
    /// cannot expose Core's internal causal receipt.
    /// </summary>
    internal readonly record struct ObservedBlaster(
        int RoundId,
        int LaunchTick,
        double LaunchSlantMeters,
        double PerpendicularSpeedMetersPerSecond,
        double ClosestApproachMillimeters,
        bool? Hit,
        bool ReconstructedCylinderContact,
        int ReconstructedCylinderContactTick,
        int LifeTicks,
        int FinalRemainingBaseTicks,
        int ClosestTick);

    private sealed class TrackedBlaster
    {
        public int LaunchTick;
        public double LaunchSlantMeters;
        public double PerpendicularSpeed;
        public double Closest = double.MaxValue;
        public int ClosestTick = -1;
        public bool ReconstructedCylinderContact;
        public int ReconstructedCylinderContactTick = -1;
        public SimVector3 Previous;
        public bool HasPrevious;
        public int LastSeenTick;
        public int FinalRemainingBaseTicks;
    }

    private readonly Dictionary<int, TrackedBlaster> _liveBlasters = [];
    private readonly List<ObservedBlaster> _blasters = [];
    private SimVector3 _previousPlayerPoint;
    private bool _hasPreviousPlayerPoint;

    internal IReadOnlyList<ObservedBlaster> Blasters => _blasters;

    private readonly List<Level100PlayerDamageEvent> _playerDamageEvents = [];
    private int _nextActorRoundIdAtPreviousObservation;

    /// <summary>Every explicit player-damage boundary this run observed.</summary>
    internal IReadOnlyList<Level100PlayerDamageEvent> PlayerDamageEvents =>
        _playerDamageEvents;

    private void RecordBlasterBallistics(WorldSnapshot state)
    {
        _playerDamageEvents.AddRange(state.Level100PlayerDamageEvents);

        Level100ActorSnapshot? player = state.Level100Actors.Actors
            .FirstOrDefault(actor => actor.Name == "Player 1");
        if (player is null)
        {
            return;
        }

        var playerPoint = new SimVector3(
            state.PlayerPosition.X,
            state.PlayerElevationMillimeters,
            state.PlayerPosition.Z);
        double velocityX = 0;
        double velocityY = 0;
        double velocityZ = 0;
        if (_hasPreviousPlayerPoint)
        {
            velocityX = (double)playerPoint.X - _previousPlayerPoint.X;
            velocityY = (double)playerPoint.Y - _previousPlayerPoint.Y;
            velocityZ = (double)playerPoint.Z - _previousPlayerPoint.Z;
        }

        _previousPlayerPoint = playerPoint;
        _hasPreviousPlayerPoint = true;

        var seen = new HashSet<int>();
        foreach (Level100ActorRoundSnapshot round in
                 state.Level100ActorMechanics.ActorRounds)
        {
            if (round.Kind != Level100ActorRoundKind.Blaster ||
                round.TargetActorId != player.ActorId)
            {
                continue;
            }

            seen.Add(round.Id);
            double yaw = round.YawMicroRadians / 1_000_000d;
            double pitch = round.PitchMicroRadians / 1_000_000d;
            double directionX = -Math.Sin(yaw) * Math.Cos(pitch);
            double directionY = Math.Sin(pitch);
            double directionZ = Math.Cos(yaw) * Math.Cos(pitch);

            if (!_liveBlasters.TryGetValue(round.Id, out TrackedBlaster? tracked))
            {
                // The muzzle is the owning drone. Move-then-weapon ordering
                // publishes a new round at elapsed tick zero before it has
                // taken its first base-tick step.
                Level100ActorSnapshot owner = state.Level100Actors.Actors
                    .First(actor => actor.ActorId == round.OwnerActorId);
                SimVector3 muzzle = owner.Pose.PositionMillimeters;
                double slantX = (double)playerPoint.X - muzzle.X;
                double slantY = (double)playerPoint.Y - muzzle.Y;
                double slantZ = (double)playerPoint.Z - muzzle.Z;

                double along = (velocityX * directionX) +
                    (velocityY * directionY) +
                    (velocityZ * directionZ);
                double perpX = velocityX - (along * directionX);
                double perpY = velocityY - (along * directionY);
                double perpZ = velocityZ - (along * directionZ);
                double perpendicular = Math.Sqrt(
                    (perpX * perpX) + (perpY * perpY) + (perpZ * perpZ));

                tracked = new TrackedBlaster
                {
                    LaunchTick = state.Tick,
                    LaunchSlantMeters = Math.Sqrt(
                        (slantX * slantX) + (slantY * slantY) + (slantZ * slantZ)) / 1_000d,
                    PerpendicularSpeed =
                        perpendicular * SimulationConstants.TicksPerSecond / 1_000d,
                };
                _liveBlasters[round.Id] = tracked;
            }

            tracked.LastSeenTick = state.Tick;
            tracked.FinalRemainingBaseTicks = round.RemainingBaseTicks;

            // Forward-looking diagnostic reconstruction.
            // `AdvanceLevel100ActorMechanics` runs BEFORE `UpdateMovement`, so
            // the segment production sweeps next starts at this snapshot's
            // round position and advances one base-tick step against this
            // snapshot's player pose. Reconstructing it backwards from
            // consecutive snapshots misses the fatal segment entirely, because
            // a round that impacts is removed inside the step that killed it.
            const double BlasterStepMillimeters =
                (double)SimulationConstants.Level100BlasterSpeedMillimetersPerSecond /
                Level100ActorMechanics.RetailBaseTicksPerSecond;
            var swept = new SimVector3(
                (int)(round.PositionMillimeters.X + (directionX * BlasterStepMillimeters)),
                (int)(round.PositionMillimeters.Y + (directionY * BlasterStepMillimeters)),
                (int)(round.PositionMillimeters.Z + (directionZ * BlasterStepMillimeters)));
            if (!tracked.ReconstructedCylinderContact &&
                Level100ActorMechanics.TryResolveBattleEngineCylinderContact(
                    round.PositionMillimeters,
                    swept,
                    player.Pose.PositionMillimeters,
                    out _))
            {
                tracked.ReconstructedCylinderContact = true;
                tracked.ReconstructedCylinderContactTick = state.Tick;
            }

            double candidate = PointToSegmentMillimeters(
                playerPoint, round.PositionMillimeters, swept);
            if (candidate < tracked.Closest)
            {
                tracked.Closest = candidate;
                tracked.ClosestTick = state.Tick;
            }

            tracked.Previous = round.PositionMillimeters;
            tracked.HasPrevious = true;
        }

        int[] disappearedRoundIds = _liveBlasters.Keys
            .Where(id => !seen.Contains(id))
            .OrderBy(id => id)
            .ToArray();
        int blasterDamageEventCount = state.Level100PlayerDamageEvents.Count(damage =>
            damage.Source == Level100PlayerDamageSource.ActorRound &&
            damage.IncomingDamageMilliLife == 200);
        IReadOnlyList<int>? impactRoundIds =
            _host.ActorRoundDamageIdsForMeasurement;
        if (impactRoundIds is not null &&
            impactRoundIds.Distinct().Count() != impactRoundIds.Count)
        {
            throw new InvalidOperationException(
                $"Tick {state.Tick} reused an actor-round identity across damage receipts.");
        }

        HashSet<int> causalHitRoundIds = impactRoundIds is null
            ? []
            : impactRoundIds
                .Where(id => _liveBlasters.ContainsKey(id))
                .ToHashSet();
        if (impactRoundIds is not null &&
            causalHitRoundIds.Count != blasterDamageEventCount)
        {
            throw new InvalidOperationException(
                $"Tick {state.Tick} has {blasterDamageEventCount} Blaster damage " +
                $"event(s) but exact impact receipts name {causalHitRoundIds.Count} " +
                $"tracked Blaster round(s): [{string.Join(", ", causalHitRoundIds.Order())}].");
        }

        int[] nonDisappearingHitIds = causalHitRoundIds
            .Where(id => !disappearedRoundIds.Contains(id))
            .OrderBy(id => id)
            .ToArray();
        if (nonDisappearingHitIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Tick {state.Tick} names hit Blaster round(s) that remained live: " +
                $"[{string.Join(", ", nonDisappearingHitIds)}].");
        }

        foreach (int id in disappearedRoundIds)
        {
            TrackedBlaster finished = _liveBlasters[id];
            _liveBlasters.Remove(id);
            _blasters.Add(new ObservedBlaster(
                id,
                finished.LaunchTick,
                finished.LaunchSlantMeters,
                finished.PerpendicularSpeed,
                finished.Closest,
                impactRoundIds is null ? null : causalHitRoundIds.Contains(id),
                finished.ReconstructedCylinderContact,
                finished.ReconstructedCylinderContactTick,
                finished.LastSeenTick - finished.LaunchTick,
                finished.FinalRemainingBaseTicks,
                finished.ClosestTick));
        }
    }

    private static double Distance(SimVector3 a, SimVector3 b)
    {
        double deltaX = (double)a.X - b.X;
        double deltaY = (double)a.Y - b.Y;
        double deltaZ = (double)a.Z - b.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    private static double PointToSegmentMillimeters(
        SimVector3 point,
        SimVector3 start,
        SimVector3 end)
    {
        double segmentX = (double)end.X - start.X;
        double segmentY = (double)end.Y - start.Y;
        double segmentZ = (double)end.Z - start.Z;
        double lengthSquared =
            (segmentX * segmentX) + (segmentY * segmentY) + (segmentZ * segmentZ);
        if (lengthSquared < 1.0)
        {
            return Distance(point, start);
        }

        double toX = (double)point.X - start.X;
        double toY = (double)point.Y - start.Y;
        double toZ = (double)point.Z - start.Z;
        double projection = Math.Clamp(
            ((toX * segmentX) + (toY * segmentY) + (toZ * segmentZ)) / lengthSquared,
            0,
            1);
        double offsetX = toX - (projection * segmentX);
        double offsetY = toY - (projection * segmentY);
        double offsetZ = toZ - (projection * segmentZ);
        return Math.Sqrt(
            (offsetX * offsetX) + (offsetY * offsetY) + (offsetZ * offsetZ));
    }

    private readonly List<long> _poseTrace = [];

    /// <summary>
    /// One packed player pose per observed tick, for comparing two runs that are
    /// supposed to be the same run.
    ///
    /// <para>Pure observation of fields the driver already reads, and consulted
    /// by no decision. It exists because "the client's input surface is
    /// lossless" is a claim that has to be measured against a control rather
    /// than argued, and a final-state hash only says <i>whether</i> two runs
    /// diverged, never <i>when</i>.</para>
    /// </summary>
    internal IReadOnlyList<long> PoseTrace => _poseTrace;

    private void Observe(WorldSnapshot state)
    {
        _poseTrace.Add(
            ((long)state.PlayerPosition.X * 31L) +
            ((long)state.PlayerPosition.Z * 131L) +
            ((long)state.FacingYawMicroRad * 1_009L) +
            ((long)state.FacingPitchMicroRad * 10_007L) +
            state.Hull);
        if (state.Mode == VehicleMode.Walker)
        {
            // The commanded morph has landed; re-arm the one-shot recorder for
            // the next leg.
            _morphCommanded = false;
        }

        if (WaterFailure is null && state.PlayerWaterFailure)
        {
            WaterFailure = (
                state.Tick,
                state.PlayerElevationMillimeters,
                _elevationLastTick);
        }

        if (state.PlayerElevationMillimeters < LowestElevationMillimeters)
        {
            LowestElevationMillimeters = state.PlayerElevationMillimeters;
            LowestElevationTick = state.Tick;
        }

        _elevationLastTick = state.PlayerElevationMillimeters;

        RecordPlayerWeaponFire(state);
        RecordArmamentEvidence(state);
        RecordBlasterBallistics(state);
        RecordWaveTwoDamage(state);
        if (state.PlayerOnGround && !state.PlayerInWater)
        {
            _lastDryGround = state.PlayerPosition;
        }
        string? navigation = state.Level100Mission.NavigationObjective;
        if (FerryLegStartTick is null &&
            string.Equals(navigation, "Target Zone 4", StringComparison.Ordinal))
        {
            FerryLegStartTick = state.Tick;
        }

        if (!string.Equals(navigation, _lastNavigation, StringComparison.Ordinal))
        {
            _log.Add(
                $"t{state.Tick} nav -> {navigation ?? "(none)"} " +
                $"mode={state.Mode} pos=({state.PlayerPosition.X},{state.PlayerPosition.Z}) hull={state.Hull}");
            _lastNavigation = navigation;
        }

        int destroyed = state.Level100Actors.Actors.Count(
            actor => actor.Lifecycle == Level100ActorLifecycle.Destroyed);
        if (destroyed != _lastDestroyed)
        {
            _log.Add(
                $"t{state.Tick} destroyed actors = {destroyed} hull={state.Hull} [" +
                string.Join(", ", state.Level100Actors.Actors
                    .Where(a => a.Lifecycle == Level100ActorLifecycle.Destroyed)
                    .Select(a => a.Name)) + "]");
            _lastDestroyed = destroyed;
        }

        foreach (Level100MissionEvent missionEvent in state.Level100MissionEvents)
        {
            if (missionEvent is Level100MissionEventPosted posted &&
                _seenEvents.Add(posted.EventName + "#" + state.Tick))
            {
                _log.Add($"t{state.Tick} EVENT {posted.EventName}");
            }
        }

        if (state.Level100Mission.Outcome != _lastOutcome)
        {
            _log.Add(
                $"t{state.Tick} OUTCOME {state.Level100Mission.Outcome} " +
                $"reason={state.Level100Mission.FailureReason} " +
                $"hull={state.Hull} mode={state.Mode} " +
                $"pos=({state.PlayerPosition.X},{state.PlayerPosition.Z}) " +
                $"y={state.PlayerElevationMillimeters} water={state.PlayerInWater}");
            _lastOutcome = state.Level100Mission.Outcome;
        }

        // Progress is "a target died or the navigation objective moved". If
        // neither happens for half a released minute the run has stalled, and
        // the stall has to be reported with the numbers that caused it rather
        // than discovered later from a final position.
        int progressKey = destroyed + (_lastNavigation?.Length ?? 0);
        if (progressKey != _lastProgressKey)
        {
            _lastProgressKey = progressKey;
            _lastProgressTick = state.Tick;
        }
        else if (state.Tick - _lastProgressTick > 0 &&
            (state.Tick - _lastProgressTick) % (30 * SimulationConstants.TicksPerSecond) == 0)
        {
            _log.Add(
                $"t{state.Tick} STALLED {(state.Tick - _lastProgressTick) / SimulationConstants.TicksPerSecond}s " +
                Diagnose(state));
        }
    }

    /// <summary>What the autopilot can see right now, in one line.</summary>
    private string Diagnose(WorldSnapshot state)
    {
        string common =
            $"mode={state.Mode} tr={state.Transition} pos=({state.PlayerPosition.X},{state.PlayerPosition.Z}) " +
            $"elev={state.PlayerElevationMillimeters} ground={state.PlayerGroundElevationMillimeters} " +
            $"onGround={state.PlayerOnGround} energy={state.Energy} hull={state.Hull} " +
            $"yaw={state.FacingYawMicroRad} pitch={state.FacingPitchMicroRad} " +
            $"pulse={state.Level100PulseCannonEnabled} twin={state.Level100VulcanCannonEnabled} " +
            $"jetGun={state.Level100MechVulcanCannonEnabled} flight={state.Level100FlightEnabled} " +
            $"control={state.Level100PlayerControlEnabled}";

        Level100ActorSnapshot? zone = ActiveZone(state);
        if (zone is not null)
        {
            return $"{common} zone={zone.Name} d={Horizontal(state, zone.Pose.PositionMillimeters):F0}";
        }

        Level100ActorSnapshot? target = NearestTarget(state);
        if (target is null)
        {
            return $"{common} nothing to do";
        }

        SimVector3 aim = AimPoint(state, target);
        double horizontal = Horizontal(state, aim);
        return $"{common} target={target.Name} hp={target.Health} " +
            $"aim=({aim.X},{aim.Y},{aim.Z}) d={horizontal:F0} " +
            $"yawErr={YawErrorTo(state, aim.X, aim.Z):F4} " +
            $"pitchErr={PitchErrorTo(state, aim, horizontal):F4} " +
            $"los={HasLineOfSight(state, aim)} " +
            $"friendlyOnRay={FriendlyStructureOnTheRay(state, target, aim)} " +
            $"precise={CollateralRiskBehind(state, target)}";
    }

    private void LogLiveObjectives(WorldSnapshot state)
    {
        foreach (Level100ActorSnapshot actor in state.Level100Actors.Actors
            .Where(actor => actor.IsObjective && actor.Active &&
                actor.Lifecycle == Level100ActorLifecycle.Alive)
            .OrderBy(actor => actor.ActorId.Value))
        {
            SimVector3 position = actor.Pose.PositionMillimeters;
            _log.Add(
                $"  live objective {actor.ActorId.Value} {actor.Name} grp={actor.TargetGroup} " +
                $"hp={actor.Health} pos=({position.X},{position.Y},{position.Z}) " +
                $"d={Horizontal(state, position):F0} los={HasLineOfSight(state, AimPoint(state, actor))}");
        }
    }

    // ------------------------------------------------------------------
    // Decision
    // ------------------------------------------------------------------

    private SimInput NextInput(WorldSnapshot state)
    {
        if (!state.Level100PlayerControlEnabled)
        {
            return SimInput.Idle;
        }

        Level100ActorSnapshot? zone = ActiveZone(state);
        if (zone is not null)
        {
            return NavigateToZone(state, zone);
        }

        _flightLegTrigger = null;
        _flightLegLaunched = false;
        _flightLegCommittedToLanding = false;

        Level100ActorSnapshot? target = NearestTarget(state);
        if (target is not null)
        {
            return Engage(state, target);
        }

        return Hold(state);
    }

    /// <summary>The trigger volume the released script currently wants entered.</summary>
    private static Level100ActorSnapshot? ActiveZone(WorldSnapshot state) =>
        state.Level100Actors.Actors.FirstOrDefault(actor =>
            actor.Trigger.HasValue &&
            actor.Active &&
            actor.IsObjective &&
            !actor.TriggerEventDispatched &&
            !actor.TriggerEntered);

    /// <summary>The nearest live scored target the script has activated.</summary>
    private static Level100ActorSnapshot? NearestTarget(WorldSnapshot state) =>
        state.Level100Actors.Actors
            .Where(actor =>
                actor.IsObjective &&
                actor.Active &&
                actor.Lifecycle == Level100ActorLifecycle.Alive &&
                actor.TargetGroup != Level100MissionTargetGroup.None &&
                actor.TargetGroup != Level100MissionTargetGroup.AirTrainer)
            .OrderBy(actor => Horizontal(state, actor.Pose.PositionMillimeters))
            .FirstOrDefault();

    /// <summary>
    /// Nothing to shoot. Either the script is talking, or - and this is beat
    /// 5's whole lesson - the Air Trainer is making its thirty-second attack
    /// run with both walker weapons disabled. <c>TUTORIAL_DODGE</c> tells the
    /// player to move, and standing still costs hull for no reason: measured,
    /// an earlier revision that idled here arrived at beat 9 with 750 of 1000
    /// hull instead of 1000, which is most of the margin the six attacking
    /// drones then eat.
    /// </summary>
    private SimInput Hold(WorldSnapshot state)
    {
        if (state.Transition != VehicleTransition.None)
        {
            return SimInput.Idle;
        }

        if (state.Mode != VehicleMode.Walker)
        {
            // Never come down over the sea (the rule the sorties follow). A
            // jet can be left with nothing to shoot mid-wave: measured with the
            // retail construction draws, the first wave's third drone was still
            // unflagged as an objective when the second died over the water,
            // and the unguarded morph lost the level at tick 4,162.
            if (OverWater(state.PlayerPosition))
            {
                double homeYawError = YawErrorTo(state, _lastDryGround.X, _lastDryGround.Z);
                double levelPitchError = -(state.FacingPitchMicroRad / 1_000_000d);
                return new SimInput(0, 1, SimActions.None, 0, 0,
                    LookAxis(homeYawError, 2_000), LookAxis(levelPitchError, 4_000));
            }

            return new SimInput(0, 0, SimActions.ToggleMode);
        }

        return UnderAttack(state)
            ? new SimInput(EvasiveStrafe(state), 0)
            : SimInput.Idle;
    }

    /// <summary>Is something in the world currently shooting at the player?</summary>
    private static bool UnderAttack(WorldSnapshot state) =>
        state.Level100Actors.Actors.Any(actor =>
            actor.Active &&
            actor.Lifecycle == Level100ActorLifecycle.Alive &&
            actor.TargetGroup is Level100MissionTargetGroup.AirTrainer
                or Level100MissionTargetGroup.AirborneTargets2);

    /// <summary>
    /// A lateral weave. Strafe rather than run: it keeps the body pointed where
    /// the reticle needs to be, and it does not walk the autopilot off a bank
    /// or into the water while it is not looking where it is going.
    /// </summary>
    private static sbyte EvasiveStrafe(WorldSnapshot state) =>
        (state.Tick / (2 * SimulationConstants.TicksPerSecond)) % 2 == 0
            ? (sbyte)1
            : (sbyte)-1;

    // ------------------------------------------------------------------
    // Navigation
    // ------------------------------------------------------------------

    /// <summary>
    /// The airframe's state on the tick this driver dropped out of jet mode on
    /// an approach to a trigger volume.
    ///
    /// <para><b>It is recorded because it was not.</b> The Target Zone 4 ferry
    /// drowning was diagnosed against a driver comment that said the morph
    /// happens "28 m up"; the morph tick and its altitude, speed and surface
    /// were in no trace anywhere, so the single number the whole diagnosis
    /// turned on was uncorroborated. This makes it an observable.</para>
    /// </summary>
    internal readonly record struct ObservedFlightLegMorph(
        int Tick,
        Level100MissionTrigger? Trigger,
        string Site,
        double HorizontalMillimeters,
        int GroundClearanceMillimeters,
        int SurfaceClearanceMillimeters,
        int SpeedMillimetersPerTick,
        int VerticalVelocityMillimetersPerTick,
        bool OverWater,
        bool CommittedToLanding)
    {
        /// <summary>
        /// The clearance this morph was allowed to happen at: the cruise
        /// hand-off radius, or the tighter band a committed dive must reach.
        /// </summary>
        internal int PermittedClearanceMillimeters => CommittedToLanding
            ? ZoneHandoffCommittedClearanceMillimeters
            : ZoneHandoffClearanceMillimeters;

        public override string ToString() =>
            $"t{Tick} morph@{Site} {Trigger} d={HorizontalMillimeters:F0} " +
            $"clearance={GroundClearanceMillimeters} " +
            $"surface={SurfaceClearanceMillimeters} speed={SpeedMillimetersPerTick} " +
            $"vy={VerticalVelocityMillimetersPerTick} overWater={OverWater} " +
            $"committed={CommittedToLanding}";
    }

    /// <summary>
    /// Horizontal range at which a zone approach stops flying and walks in.
    ///
    /// <para>Unchanged, and deliberately so. It was the whole of the hand-off
    /// test and it is now one of two terms; swapping it for a smaller number
    /// was proposed, measured and refuted, because moving the morph eight
    /// metres closer does not change what the morph <em>is</em>.</para>
    /// </summary>
    private const int ZoneWalkInHorizontalMillimeters = 20_000;

    /// <summary>
    /// Clearance above whatever is underneath - terrain, or the sea surface -
    /// at or below which an approach may leave jet mode.
    ///
    /// <para><b>Why the hand-off has to carry an altitude term at all, and why
    /// this is retail-faithful rather than a number that passes a test.</b> The
    /// morph exists for exactly one reason: <c>TargetZoneN.msl</c>'s
    /// <c>hit()</c> tests <c>InJetMode() == FALSE</c>, and that released builtin
    /// - <c>0x005380F0</c> negating <c>0x00408120</c> against the shipped 0.5 s
    /// threshold at <c>_DAT_005D85EC</c>, recorded at
    /// <see cref="Level100MissionTiming.JetModeState"/> - is FALSE <b>only for a
    /// walker whose last ground contact is inside fifteen ticks</b>. A walker
    /// that morphs tens of metres up is airborne for its whole fall and reads as
    /// in jet mode for all of it. Morphing there is not merely early; it is
    /// strictly incapable of doing the one thing it is for.</para>
    ///
    /// <para><b>And once it has morphed the driver has no say left.</b>
    /// <c>Simulation.UpdateAirborneWalkerMovement</c> takes no
    /// <see cref="SimInput"/> at all, so an airborne walker has no input-driven
    /// movement authority; the only surviving control is
    /// <c>ApplyWalkerLandingJets</c>, which is a pure brake this driver already
    /// holds on every airborne tick. The touchdown point is therefore fixed at
    /// the morph tick, and on the Target Zone 4 ferry it was fixed over open
    /// sea.</para>
    ///
    /// <para><b>The value introduces no new number: it is
    /// <see cref="ZoneWalkInHorizontalMillimeters"/>.</b> The hand-off radius
    /// was always meant to say "close enough to walk in from"; it just measured
    /// distance in two axes and ignored the third, while the volume it exists to
    /// satisfy is a THREE-dimensional sphere test
    /// (<c>Simulation.cs:2490-2494</c>) - which is itself a correction, made
    /// after all three legs were found completing 11-13 m up in level flight
    /// (see <c>Level100FlightLegTests</c>). This is the same correction applied
    /// one level earlier, to the decision instead of the test.</para>
    ///
    /// <para><b>It is also, measurably, a no-op on every approach that was
    /// already sound</b>, and that survived the 20 Hz migration. The value was
    /// derived by one rule: it must sit ABOVE every airborne hand-off the
    /// earlier beats make, so those beats do not move and beat 9 is not
    /// re-rolled, and BELOW the ferry's own arrival, so it bites there. Both
    /// bounds were re-measured at 20 Hz and both still hold:</para>
    ///
    /// <list type="table">
    ///   <item><description>beat 6 <c>TargetZone2</c>: 18,969 mm of surface
    ///   clearance at 30 Hz, <b>13,914</b> mm at 20 Hz (returning player);
    ///   15,244 mm on the cold career.</description></item>
    ///   <item><description>beat 8 <c>TargetZone3</c>: 19,759 mm at 30 Hz,
    ///   <b>19,972</b> mm at 20 Hz; 19,750 mm on the cold career. This is the
    ///   binding bound, and the margin is <b>28 mm</b> where it was 241.
    ///   </description></item>
    ///   <item><description>beat 10, the ferry, with the clearance term
    ///   reinstated as horizontal distance alone: 28,915 mm at 30 Hz,
    ///   <b>25,026</b> mm at 20 Hz; 24,438 mm on the cold career. The term
    ///   refuses all of them.</description></item>
    /// </list>
    ///
    /// <para><b>An intermediate 20 Hz measurement said the opposite and was
    /// wrong, for a reason worth recording.</b> Taken before the driver's own
    /// rate-denominated poles were converted (see <see cref="ErrorPole"/>),
    /// beat 9 collapsed onto the released sub-40 % abort branch, the ferry
    /// therefore started from a different state and arrived at 12,712 mm, and
    /// the cruise tier looked INERT - fixed and adverse arms bit-identical, and
    /// no tier at all could separate a 19,972 mm beat 8 from a 12,712 mm ferry.
    /// The tier was not the broken thing. <b>Re-derive the beat before
    /// re-deriving the threshold that reads its output.</b></para>
    ///
    /// <para><b>A single flat threshold this size is not enough on its own, and
    /// that was measured too.</b> A 20 m hand-off still commits roughly 25 m of
    /// ballistic drift - the fall is 258 ticks and the landing thrusters bottom
    /// out at a 35 mm/tick integer floor they never get below - so whether it
    /// lands on the dock is a property of the bearing, not of the policy. Under
    /// the twenty perturbations it drowned twice. That is what
    /// <see cref="ZoneHandoffCommittedClearanceMillimeters"/> is for.</para>
    ///
    /// <para><b>And a single flat threshold that small is not available.</b> 5 m,
    /// 8 m and 12 m are all defensible physics and all of them move beats 6 and
    /// 8, which re-rolls beat 9 - a beat this suite's own remarks describe as a
    /// cliff with no gradient. Measured on the returning player: 12 m keeps six
    /// kills but loses the cold career to <c>WaterLoss</c>; 8 m drops the
    /// returning player to two kills and the abort branch; 5 m drops it to
    /// three. Choosing between those by their beat-9 score would be fitting to a
    /// chaotic objective, which is exactly what <c>AGENTS.md</c> forbids.</para>
    /// </summary>
    internal const int ZoneHandoffClearanceMillimeters =
        ZoneWalkInHorizontalMillimeters;

    /// <summary>
    /// The clearance a leg that had to DIVE must reach before it may morph.
    ///
    /// <para><b>The rule in one sentence: hand off from the cruise, or from the
    /// deck, never from a dive.</b> The 20 m cruise hand-off above is safe for
    /// the two legs that use it, and measurably so - they touch down where they
    /// can walk in from. It is not safe for a leg that arrived high and had to
    /// come down to reach it, because such a leg still has 20 m of fall to spend
    /// and the drift that fall buys is decided by the bearing rather than by the
    /// driver. Measured: with 20 m as the only term, the twenty perturbations
    /// still drowned twice.</para>
    ///
    /// <para>The value is
    /// <see cref="SimulationConstants.JetGroundEffectHeightMillimeters"/> = 5 m,
    /// which is <c>BattleEngineJetPart.cpp</c>'s own definition of "near the
    /// surface" - the band in which the airframe is already being held up by
    /// the ground rather than by flight, and the same constant
    /// <c>Simulation.ApplyJetGroundEffect</c> tests against the same
    /// <c>max(terrain, water)</c> support plane this clearance is measured
    /// against.</para>
    ///
    /// <para><b>It is one of three terms and not sufficient on its own.</b> A
    /// committed leg must be inside this band underfoot, inside it half a
    /// second of travel ahead
    /// (<see cref="SurfaceClearanceAheadMillimeters"/>), and the ballistic
    /// landing it is about to commit to must come down on dry land
    /// (<see cref="BallisticTouchdownIsDryLand"/>). Each of the three was added
    /// because the sweep still drowned without it; the numbers are on those two
    /// members.</para>
    /// </summary>
    internal const int ZoneHandoffCommittedClearanceMillimeters =
        SimulationConstants.JetGroundEffectHeightMillimeters;

    /// <summary>
    /// Clearance below which the leg holds a shallow climb rather than a
    /// descent, so the airframe is never asked to fly nose-down inside the
    /// released ground-effect band. Unchanged from the measured
    /// <c>Level100FlightLegDriver</c>; only the surface it is measured against
    /// moved, from the terrain to <c>max(terrain, water)</c>.
    /// </summary>
    private const int FlightLegCruiseFloorMillimeters = 12_000;

    /// <summary>
    /// Range at which the leg stops cruising and starts putting the airframe on
    /// the ground. Unchanged from <c>Level100FlightLegDriver</c>; what changed
    /// is that reaching it is no longer sufficient to morph.
    /// </summary>
    private const int FlightLegFinalApproachMillimeters = 12_000;

    /// <summary>
    /// Reinstates the pre-fix hand-off - horizontal distance alone, no
    /// clearance term - for the adverse control in
    /// <c>Level100FerryLandingTests</c>. Nothing else in the driver changes.
    /// </summary>
    private bool _horizontalOnlyZoneHandoff;

    /// <summary>
    /// One member of <see cref="Level100LookPerturbation.Sweep"/>, applied to
    /// the look commands this driver issues while it is engaging wave 2, or
    /// null for the unperturbed run.
    /// </summary>
    private Level100LookPerturbation? _beatNinePerturbation;

    /// <summary>
    /// May this approach drop out of jet mode here?
    ///
    /// <para>Yes on the ground, and yes inside the released ground-effect band.
    /// <b>This is a no-op wherever flight is off or the approach is already on
    /// the ground</b>, which is every one of beats 1-8: they are walked, or
    /// flown and landed by <see cref="FlyLeg"/>, and in both cases the airframe
    /// is grounded when the walker branch is first asked for.</para>
    /// </summary>
    internal bool ClearedToLeaveJetMode(WorldSnapshot state)
    {
        if (_horizontalOnlyZoneHandoff || state.PlayerOnGround)
        {
            return true;
        }

        // A cruise hand-off must also come down on dry land. An approach can
        // reach the walk-in radius straight from a fight, at speed, without
        // ever committing to a landing; with the level's three-second pre-run
        // the cold career did that at Target Zone 4 and drifted 13 m into the
        // sea from 12 m up.
        if (!_flightLegCommittedToLanding)
        {
            return state.PlayerAltitudeAboveSurfaceMillimeters <=
                ZoneHandoffClearanceMillimeters &&
                BallisticTouchdownIsDryLand(state);
        }

        return state.PlayerAltitudeAboveSurfaceMillimeters <=
                ZoneHandoffCommittedClearanceMillimeters &&
            SurfaceClearanceAheadMillimeters(state) <=
                ZoneHandoffCommittedClearanceMillimeters &&
            BallisticTouchdownIsDryLand(state);
    }

    /// <summary>
    /// Would the landing this morph commits to come down on dry land?
    ///
    /// <para><b>This is the diagnosis's own sentence turned into a decision.</b>
    /// The whole failure is that "the touchdown point is fixed at the morph
    /// tick" - <c>Simulation.UpdateAirborneWalkerMovement</c> takes no
    /// <see cref="SimInput"/>, so once the morph is commanded the driver has no
    /// say in where it lands. A driver that may not steer after the commit has
    /// to know where the commit puts it, so it replays Core's own airborne-
    /// walker recurrence forward: the landing-thruster retentions - retail's
    /// <c>0.975</c> horizontal and <c>0.925</c> descending past the 10 mm
    /// minimum, per 20 Hz update, and since the 20 Hz migration those exact
    /// shipped floats rather than the <c>0.983263</c> / <c>0.949353</c> the
    /// 30 Hz Core had to carry
    /// (<c>BattleEngineWalkerPart.cpp:330-344</c>), then
    /// <see cref="SimulationConstants.WalkerGravityPerTick"/>, until the
    /// height field catches it.</para>
    ///
    /// <para><b>It is NOT a change to the water rule, and it is not the guard
    /// that measured inert.</b> The water rule
    /// (<c>Simulation.WaterFailureAtElevation</c>) is untouched and pinned by
    /// <c>Level100FerryLandingTests</c>. The guard that measured inert was
    /// placed in <see cref="FlyLeg"/>, on a decision <c>FlyLeg</c> never made;
    /// this one is on the decision itself. And it is not a substitute for the
    /// clearance terms above - it is the last of three, and on its own it would
    /// still be committing a thirty-metre ballistic drift.</para>
    ///
    /// <para><b>Why it is needed on top of the clearance, measured.</b> One
    /// perturbation handed off 4,970 mm above dry ground 2.6 m from the volume
    /// and drowned anyway: the dock shelves away faster than the walker
    /// descends, so five metres of clearance underfoot was six metres of drift
    /// and then open sea. Half a second of look-ahead does not see that either
    /// - the fall lasts forty-odd ticks and the look-ahead is fifteen.</para>
    /// </summary>
    private bool BallisticTouchdownIsDryLand(WorldSnapshot state)
    {
        long x = state.PlayerPosition.X;
        long z = state.PlayerPosition.Z;
        long elevation = state.PlayerElevationMillimeters;
        int velocityX = state.PlayerVelocity.X;
        int velocityZ = state.PlayerVelocity.Z;
        int verticalVelocity = state.PlayerVerticalVelocityMillimetersPerTick;

        for (int tick = 0; tick < BallisticTouchdownBudgetTicks; tick++)
        {
            // The morph is not instant, and the transition is the worst part of
            // the fall to leave out: `Simulation.UpdateTransitionMovement`
            // damps nothing while it runs and applies only
            // `MorphIntoWalkerGravityPerTick`, so the airframe coasts at full
            // speed for the whole of
            // `SimulationConstants.JetToWalkerTransitionTicks`.
            if (tick < SimulationConstants.JetToWalkerTransitionTicks)
            {
                verticalVelocity -= SimulationConstants.MorphIntoWalkerGravityPerTick;
                x += velocityX;
                z += velocityZ;
                elevation += verticalVelocity;
                continue;
            }

            velocityX = RetainLandingJet(
                velocityX,
                SimulationConstants.WalkerLandingJetHorizontalRetentionNumerator);
            velocityZ = RetainLandingJet(
                velocityZ,
                SimulationConstants.WalkerLandingJetHorizontalRetentionNumerator);
            if (verticalVelocity <
                -SimulationConstants.WalkerLandingJetMinimumDescentPerTick)
            {
                verticalVelocity = RetainLandingJet(
                    verticalVelocity,
                    SimulationConstants.WalkerLandingJetVerticalRetentionNumerator);
            }

            verticalVelocity -= SimulationConstants.WalkerGravityPerTick;
            x += velocityX;
            z += velocityZ;
            elevation += verticalVelocity;

            var here = new SimVector2((int)x, (int)z);
            int ground = _terrain.SampleGroundElevationMillimeters(here);
            if (elevation <=
                ground + Level100Terrain.WalkerCenterOfGravityMillimeters)
            {
                return ground > Level100Terrain.WaterElevationMillimeters;
            }
        }

        // It did not come down inside the budget, which for a fall bounded by
        // the hand-off clearance cannot happen. Refuse rather than guess.
        return false;
    }

    /// <summary>
    /// Long enough for any fall the hand-off clearance permits: the terminal
    /// descent under the released thrusters is 70 mm per tick, so five metres
    /// is about seventy ticks.
    /// </summary>
    private const int BallisticTouchdownBudgetTicks =
        20 * SimulationConstants.TicksPerSecond;

    private static int RetainLandingJet(int value, int numerator) =>
        (int)Math.Round(
            (double)value * numerator /
                SimulationConstants.WalkerLandingJetRetentionDenominator,
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// Clearance above the support plane half a second of travel AHEAD, which
    /// is where the airframe will be when the ballistic fall it is about to
    /// commit to actually lands.
    ///
    /// <para><b>Why the clearance underfoot is not enough, measured.</b> A
    /// Target Zone 4 approach handed off 4,992 mm above dry ground 2.5 m from
    /// the volume, drifted 6 m south-east while it fell, and drowned - because
    /// the dock shelves away faster than the walker descends, so five metres of
    /// clearance underfoot was never five metres of fall. It also never
    /// triggered on the way past: it was inside the 5.4 m sphere but had been
    /// airborne longer than the released 0.5 s, so <c>InJetMode()</c> read TRUE.
    /// </para>
    ///
    /// <para>The sample point and the support plane are the released
    /// ones: <c>Simulation.ApplyJetGroundEffect</c> looks
    /// <see cref="SimulationConstants.JetGroundEffectLookaheadTicks"/> of
    /// velocity ahead - <c>BattleEngineJetPart.cpp:548</c>'s half a second - and
    /// takes <c>max(terrain, water)</c> as the surface. Reading the terrain
    /// ahead is the same class of observation this driver already makes for
    /// line-of-sight before it fires; see the class remarks on what it reads.
    /// </para>
    /// </summary>
    private int SurfaceClearanceAheadMillimeters(WorldSnapshot state)
    {
        var ahead = new SimVector2(
            state.PlayerPosition.X +
                (state.PlayerVelocity.X *
                    SimulationConstants.JetGroundEffectLookaheadTicks),
            state.PlayerPosition.Z +
                (state.PlayerVelocity.Z *
                    SimulationConstants.JetGroundEffectLookaheadTicks));
        int support = Math.Max(
            _terrain.SampleGroundElevationMillimeters(ahead),
            Level100Terrain.WaterElevationMillimeters);
        return state.PlayerElevationMillimeters - support;
    }

    private readonly List<ObservedFlightLegMorph> _flightLegMorphs = [];

    /// <summary>Every jet-to-walker morph this run made on a zone approach.</summary>
    internal IReadOnlyList<ObservedFlightLegMorph> FlightLegMorphs => _flightLegMorphs;

    private bool _morphCommanded;

    /// <summary>
    /// The first tick on which <c>Target Zone 4</c> was the navigation
    /// objective - the tick the ferry home begins. Everything before it is
    /// beats 1 to 9.
    /// </summary>
    internal int? FerryLegStartTick { get; private set; }

    private int _elevationLastTick = int.MaxValue;

    /// <summary>
    /// The tick <c>Simulation.UpdateWaterFailureState</c> first declared a
    /// water loss on this run, with the committed elevation on that tick and on
    /// the tick before it.
    ///
    /// <para>It is the crossing, not the verdict, and that is the point: it
    /// lets a test assert that the released threshold fired on the FIRST tick
    /// the airframe was at or below it and not one tick earlier. See
    /// <c>Level100FerryLandingTests</c> Gate C.</para>
    /// </summary>
    internal (int Tick, int Elevation, int PreviousElevation)? WaterFailure
    {
        get;
        private set;
    }

    /// <summary>
    /// The deepest committed player elevation this run ever reached, and the
    /// tick it reached it on.
    ///
    /// <para>It is the other half of <see cref="WaterFailure"/> and it exists
    /// because the drowning stopped happening. A run that never drowns still
    /// carries a claim about the released water rule - that the rule did NOT
    /// fire, on every tick, at the elevation the airframe actually held - and
    /// without the low-water mark that claim is unfalsifiable. With it, the
    /// verdict and the elevation can be checked against each other on real
    /// runs whether or not any of them ends in the sea. See
    /// <c>Level100FerryLandingTests</c> Gate C.</para>
    /// </summary>
    internal int LowestElevationMillimeters { get; private set; } = int.MaxValue;

    /// <summary>The tick <see cref="LowestElevationMillimeters"/> was reached.</summary>
    internal int LowestElevationTick { get; private set; } = -1;

    private void RecordMorph(
        WorldSnapshot state,
        Level100ActorSnapshot zone,
        double horizontal,
        string site)
    {
        if (_morphCommanded)
        {
            return;
        }

        _morphCommanded = true;
        var morph = new ObservedFlightLegMorph(
            state.Tick,
            zone.Trigger,
            site,
            horizontal,
            state.PlayerAltitudeAboveGroundMillimeters,
            state.PlayerAltitudeAboveSurfaceMillimeters,
            (int)Math.Sqrt(
                ((long)state.PlayerVelocity.X * state.PlayerVelocity.X) +
                ((long)state.PlayerVelocity.Z * state.PlayerVelocity.Z)),
            state.PlayerVerticalVelocityMillimetersPerTick,
            state.PlayerGroundElevationMillimeters <
                Level100Terrain.WaterElevationMillimeters,
            _flightLegCommittedToLanding);
        _flightLegMorphs.Add(morph);
        _log.Add(morph.ToString());
    }

    private SimInput NavigateToZone(WorldSnapshot state, Level100ActorSnapshot zone)
    {
        SimVector3 position = zone.Pose.PositionMillimeters;
        double horizontal = Horizontal(state, position);
        if (state.Level100FlightEnabled &&
            (horizontal > ZoneWalkInHorizontalMillimeters ||
                !ClearedToLeaveJetMode(state)))
        {
            return FlyLeg(state, zone, horizontal);
        }

        // Walk the last metres in, or the whole way before flight is taught.
        if (state.Mode != VehicleMode.Walker)
        {
            if (state.Transition != VehicleTransition.None)
            {
                return SimInput.Idle;
            }

            RecordMorph(state, zone, horizontal, "NavigateToZone");
            return new SimInput(0, 0, SimActions.ToggleMode);
        }

        if (state.Transition != VehicleTransition.None)
        {
            return SimInput.Idle;
        }

        double yawError = YawErrorTo(state, position.X, position.Z);
        short lookX = WalkerLookAxis(yawError);
        double pitchError = -(state.FacingPitchMicroRad / 1_000_000d);
        short lookY = LookAxis(pitchError, 4_000);

        // Walking input is gated on ground contact, as both sibling drivers
        // already gate it (`FlyLeg` and `Level100FlightLegDriver`). It is
        // PROVABLY inert rather than merely measured so: `Simulation.cs:945-952`
        // reaches `UpdateWalkerMovement(input)` only when the same
        // `_playerOnGround` this snapshot reports is set, and discards the input
        // entirely otherwise.
        sbyte forward = state.PlayerOnGround && horizontal > 1_500
            ? (sbyte)1
            : (sbyte)0;
        SimActions actions = state.PlayerOnGround ? SimActions.None : SimActions.LandingJets;
        return new SimInput(0, forward, actions, 0, 0, lookX, lookY);
    }

    /// <summary>
    /// One released flight leg. Identical in policy to the measured
    /// <c>Level100FlightLegDriver</c>: transform once, hold a shallow climb
    /// clear of the ground effect, fly the bearing, then drop out of jet mode
    /// and ride the landing thrusters down into the volume - because
    /// <c>TargetZoneN.msl</c> tests <c>InJetMode() == FALSE</c>.
    ///
    /// <para><b>One addition, and it is the whole of the ferry fix.</b> Once
    /// the leg is inside <see cref="FlightLegFinalApproachMillimeters"/> it is
    /// committed to the landing, and a committed leg flies the airframe DOWN
    /// onto <see cref="ZoneHandoffClearanceMillimeters"/> at the released
    /// minimum throttle before it will morph. Until this existed the leg had no
    /// descent authority at all - its pitch law climbed to 12 m and then held
    /// whatever altitude it happened to arrive with, which after beat 9's
    /// dogfight was 29 m.</para>
    private SimInput FlyLeg(
        WorldSnapshot state,
        Level100ActorSnapshot zone,
        double horizontal)
    {
        if (_flightLegTrigger != zone.Trigger)
        {
            _flightLegTrigger = zone.Trigger;
            _flightLegLaunched = false;
            _flightLegCommittedToLanding = false;
        }

        SimVector3 position = zone.Pose.PositionMillimeters;
        double yawError = YawErrorTo(state, position.X, position.Z);
        short lookX = LookAxis(yawError, 2_000);

        if (!_flightLegLaunched)
        {
            if (state.Transition != VehicleTransition.None)
            {
                return SimInput.Idle;
            }

            if (state.Mode == VehicleMode.Jet)
            {
                _flightLegLaunched = true;
            }
            else
            {
                return new SimInput(0, 0, SimActions.ToggleMode);
            }
        }

        if (state.Mode == VehicleMode.Jet && state.Transition == VehicleTransition.None)
        {
            _flightLegCommittedToLanding |=
                horizontal < FlightLegFinalApproachMillimeters;

            // Cruise, unchanged: hold a shallow climb clear of the ground
            // effect and then fly the bearing. Once committed to the landing:
            // fly the airframe DOWN onto the hand-off band and let the throttle
            // back to the released minimum, because everything that happens
            // after the morph is ballistic and the only thing that bounds it is
            // the speed and height it is committed at.
            double targetPitch = _flightLegCommittedToLanding
                ? (state.PlayerAltitudeAboveSurfaceMillimeters >
                    ZoneHandoffCommittedClearanceMillimeters ? 0.20 : 0.0)
                : (state.PlayerAltitudeAboveGroundMillimeters <
                    FlightLegCruiseFloorMillimeters ? -0.20 : 0.0);
            double pitchError = targetPitch - (state.FacingPitchMicroRad / 1_000_000d);
            short lookY = LookAxis(pitchError, 4_000);
            if (_flightLegCommittedToLanding && ClearedToLeaveJetMode(state))
            {
                RecordMorph(state, zone, horizontal, "FlyLeg");
                return new SimInput(0, -1, SimActions.ToggleMode, 0, 0, lookX, lookY);
            }

            sbyte throttle = _flightLegCommittedToLanding
                ? (sbyte)-1
                : horizontal > 40_000 ? (sbyte)1 : (sbyte)0;
            return new SimInput(0, throttle, SimActions.None, 0, 0, lookX, lookY);
        }

        if (state.Mode == VehicleMode.Walker && state.Transition == VehicleTransition.None)
        {
            SimActions actions = _flightLegCommittedToLanding && !state.PlayerOnGround
                ? SimActions.LandingJets
                : SimActions.None;
            sbyte forward = state.PlayerOnGround && horizontal > 1_500 ? (sbyte)1 : (sbyte)0;
            double pitchError = -(state.FacingPitchMicroRad / 1_000_000d);
            short lookY = LookAxis(pitchError, 4_000);
            return new SimInput(0, forward, actions, 0, 0, lookX, lookY);
        }

        return SimInput.Idle;
    }

    // ------------------------------------------------------------------
    // Engagement
    // ------------------------------------------------------------------

    private SimInput Engage(WorldSnapshot state, Level100ActorSnapshot target)
    {
        bool airborneTarget =
            target.TargetGroup is Level100MissionTargetGroup.AirborneTargets1
                or Level100MissionTargetGroup.AirborneTargets2;

        // Both drone waves are fought in the air.
        //
        // Beat 7 has no choice: the script disables both walker weapons at the
        // end of beat 5 and only re-enables them at beat 8, so the jet's Mech
        // Vulcan Cannon is the only weapon the player owns for the first wave.
        //
        // Beat 9 is a choice, and it is the one the level teaches - the
        // LevelScript plays TUTORIAL_STRAFE at the head of
        // event("Reached Target Zone 3"), immediately before the second wave.
        // The numbers say the same thing. The wave-2 drones carry the Forseti
        // Drone Missile Launcher at CWeaponMaxRange 80.0 while the player's
        // Pulse Cannon round expires at 46.7 m, so a walker cannot answer them
        // at the range they open fire, and closing on foot at
        // WalkerMaximumSpeedPerTick costs eleven seconds inside their envelope.
        // Measured with a walker: 750 -> 0 hull, no wave-2 drone destroyed.
        if (target.TargetGroup == Level100MissionTargetGroup.AirborneTargets2)
        {
            // The one-permille sweep is applied HERE and nowhere else, because
            // the sweep this suite quotes is "twenty separate one-permille
            // perturbations of beat 9 ALONE". See
            // `Level100LookPerturbation` for why the scope matters.
            SimInput beatNine = EngageWaveTwo(state, target);
            return _beatNinePerturbation is { } perturbation
                ? beatNine with
                {
                    LookXAnalogPermille =
                        perturbation.Apply(beatNine.LookXAnalogPermille),
                    LookYAnalogPermille =
                        perturbation.Apply(beatNine.LookYAnalogPermille),
                }
                : beatNine;
        }

        if (airborneTarget)
        {
            return EngageFromJet(state, target);
        }

        if (state.Mode != VehicleMode.Walker)
        {
            return state.Transition == VehicleTransition.None
                ? new SimInput(0, 0, SimActions.ToggleMode)
                : SimInput.Idle;
        }

        if (state.Transition != VehicleTransition.None)
        {
            return SimInput.Idle;
        }

        return EngageFromWalker(state, target);
    }

    /// <summary>
    /// A ground target, on foot. Every target that reaches this method is a
    /// ground target: <see cref="Engage"/> routes both drone waves to the jet.
    /// </summary>
    private SimInput EngageFromWalker(
        WorldSnapshot state,
        Level100ActorSnapshot target)
    {
        // The released script re-enables Pulse for the moving-target exercise
        // without disabling Twin. EnableWeapon does not steal the selection,
        // so the player must use the released change-weapon edge once. The old
        // driver stayed green only because Core used active-flag priority and
        // silently auto-selected Pulse here.
        if (target.TargetGroup == Level100MissionTargetGroup.MovingTargets &&
            state.Level100PulseCannonEnabled &&
            state.Level100WalkerSelectedWeapon !=
                Level100MissionWeapon.PulseCannonPod)
        {
            return new SimInput(0, 0, SimActions.ChangeWeapon);
        }

        // Is there something the script has not armed yet in the volume a miss
        // would land in? If so this is a precision shot, not a brawl: the hull
        // sweep comes off, the feet stop, and the trigger tolerance tightens.
        // Measured cost of getting this wrong: two rounds.
        bool precise = CollateralRiskBehind(state, target);
        double standOff = precise
            ? PrecisionStandOffMillimeters
            : GroundStandOffMillimeters;
        double fireRange = precise
            ? PrecisionStandOffMillimeters + PrecisionFireMarginMillimeters
            : double.MaxValue;

        SimVector3 aim = AimPoint(state, target, sweep: !precise);
        double horizontal = Horizontal(state, aim);

        double yawError = YawErrorTo(state, aim.X, aim.Z);
        short lookX = WalkerLookAxis(yawError);
        double pitchError = PitchErrorTo(state, aim, horizontal);
        short lookY = LookAxis(pitchError, 4_000);

        // The whole point of this driver: a parked vehicle on the far side of a
        // crest cannot be shot from the near side, and firing into that crest
        // is what stalled the chain for 3,578 rounds.
        bool clear = HasLineOfSight(state, aim) &&
            !FriendlyStructureOnTheRay(state, target, aim);

        // A stance can be unusable even with a perfectly clear ray. The
        // released walker pitch limiter (CBattleEngine,
        // references/Onslaught/BattleEngine.cpp:1125-1180, transcribed in
        // Simulation.ApplyWalkerTerrainPitchLimit) is terrain-relative, so
        // standing on the lip of a steep bank caps how far the nose will come
        // down: measured at (-94335, 31781), where the ground falls 1.12 m per
        // metre, the reachable pitch was 0.07-0.13 rad against the 0.27 rad the
        // shot needed, and an earlier revision of this driver stood there for
        // 180 released seconds with a clear line of sight and a converged yaw.
        //
        // Rather than model that limit - a driver that predicts the engine's
        // constraints is a second implementation of the engine, and will be
        // wrong somewhere else - the test is the one a player actually applies:
        // THE TARGET'S HEALTH IS NOT GOING DOWN. That covers the pitch limiter,
        // an aim point that misses the hull, an occluder the sampled ray
        // stepped over, and anything else not yet met.
        if (target.ActorId != _engagedActorId)
        {
            _engagedActorId = target.ActorId;
            _engagedHealth = target.Health;
            _engagedSinceTick = state.Tick;
            _repositionTicksRemaining = 0;
        }
        else if (target.Health < _engagedHealth)
        {
            _engagedHealth = target.Health;
            _engagedSinceTick = state.Tick;
        }

        bool notHurtingIt = clear &&
            horizontal <= standOff &&
            state.Tick - _engagedSinceTick > 4 * SimulationConstants.TicksPerSecond;
        if (!clear || notHurtingIt)
        {
            _repositionTicksRemaining = Math.Max(
                _repositionTicksRemaining,
                2 * SimulationConstants.TicksPerSecond);
            if (notHurtingIt)
            {
                _engagedSinceTick = state.Tick;
            }
        }

        // A precision shot is taken standing still. Every metre walked while
        // aiming moves the terrain pitch the released limiter is holding the
        // nose against, and the misses that killed Target Truck #25 were 0.06
        // rad high while the walker was closing at speed.
        // ...but not when the watchdog above has just said the target's health
        // is not moving. Cancelling the reposition on `precise` alone made the
        // precision branch outrank the only check that can see a stance which
        // cannot work: measured, the driver stood at (23608, 69303) with a
        // clear ray, a converged yaw and a live Target Tank for 34,000 ticks
        // because every reposition the watchdog asked for was cancelled on the
        // same tick it was requested.
        if (precise && clear && horizontal <= standOff && !notHurtingIt)
        {
            _repositionTicksRemaining = 0;
            _strafeTicksRemaining = 0;
        }

        sbyte forward = 0;
        sbyte strafe = 0;
        if (_repositionTicksRemaining > 0)
        {
            _repositionTicksRemaining--;
            _blockedTicks++;
            if (horizontal > MinimumGroundStandOffMillimeters && _strafeTicksRemaining == 0)
            {
                // Close in. Most crests stop occluding once the muzzle is past
                // them, and closing shortens the ray.
                forward = 1;
            }
            else
            {
                // Standing on top of it is its own failure mode, and it is not
                // fixed by walking round. AimPoint's ladder is bounded in
                // ANGLE, so at the minimum stand-off it collapses to its 100 mm
                // floor and the whole ladder can sit below the terrain the ray
                // is sampled against: measured against Target Warehouse, whose
                // authored origin is 0.6 m below the ground the walker stands
                // on (buildings are not ground-seated - see the open list in
                // local-lab/INDEX.md), every rung of the ladder was under
                // ground + LineOfSightClearanceMillimeters, so `clear` was
                // false at every point on a 3.3 m orbit and the driver circled
                // for 34,000 ticks. Backing out restores the ladder's reach.
                if (horizontal <= MinimumGroundStandOffMillimeters)
                {
                    forward = -1;
                }

                if (_strafeTicksRemaining == 0)
                {
                    _strafeTicksRemaining = StrafeSegmentTicks;
                    _strafeDirection = -_strafeDirection;
                }

                _strafeTicksRemaining--;
                strafe = (sbyte)_strafeDirection;
            }
        }
        else
        {
            _blockedTicks = 0;
            _strafeTicksRemaining = 0;
            if (horizontal > standOff)
            {
                forward = 1;
            }

            // If something is shooting while this ground target is being
            // engaged, keep weaving: the strafe axis is independent of the aim
            // axis, so it costs nothing in accuracy.
            if (UnderAttack(state))
            {
                strafe = EvasiveStrafe(state);
            }
        }

        // Neither closing nor this strafe leg is working; try the other side.
        if (_blockedTicks > 15 * SimulationConstants.TicksPerSecond)
        {
            _blockedTicks = 0;
            _strafeDirection = -_strafeDirection;
            _strafeTicksRemaining = StrafeSegmentTicks * 2;
        }

        double tolerance = precise
            ? PrecisionTolerance(horizontal)
            : FireTolerance(horizontal);
        SimActions actions =
            clear && horizontal <= fireRange &&
            Math.Abs(yawError) < tolerance && Math.Abs(pitchError) < tolerance
                ? SimActions.Fire
                : SimActions.None;
        return new SimInput(strafe, forward, actions, 0, 0, lookX, lookY);
    }

    /// <summary>Energy at which a sortie is launched.</summary>
    private const int SortieLaunchEnergy = 7_000;

    /// <summary>Energy at which the jet comes home rather than stalling.</summary>
    private const int SortieRecoverEnergy = 900;

    /// <summary>
    /// Beat 7's drone wave, flown. The Aquila is not a helicopter: it cannot
    /// hold station on a target, and holding full throttle empties the store -
    /// the released Aquila Prototype costs are 0.005-0.012 energy per retail
    /// tick against a store of 8.0, which is 33-47 released seconds of flight,
    /// while `WalkerEnergyRegenerationPerTick` refills it only on the ground.
    /// So this flies discrete sorties: charge on the ground, launch, make firing
    /// passes, and come home before the store empties rather than stalling out
    /// of the sky, which is what an earlier revision did sixteen times.
    /// </summary>
    private SimInput EngageFromJet(WorldSnapshot state, Level100ActorSnapshot target)
    {
        if (state.Transition != VehicleTransition.None)
        {
            return SimInput.Idle;
        }

        SimVector3 aim = AimPoint(state, target);
        double horizontal = Horizontal(state, aim);
        double yawError = YawErrorTo(state, aim.X, aim.Z);
        short lookX = LookAxis(yawError, 2_000);
        int altitude = state.PlayerAltitudeAboveGroundMillimeters;

        if (state.Mode != VehicleMode.Jet)
        {
            // Launching on a part-full store was measured and is worse, not
            // better: with a third of the store the beat-9 sortie ran itself
            // dry 33 m further out than the abort's Target Zone 4 objective and
            // the run was lost on the way back. Charge fully, then commit.
            if (state.Energy < SortieLaunchEnergy)
            {
                // Charging. Face the wave and hold the landing thrusters if
                // still coming down; standing still is what refills the store.
                double groundPitchError = -(state.FacingPitchMicroRad / 1_000_000d);
                return new SimInput(
                    0,
                    0,
                    state.PlayerOnGround ? SimActions.None : SimActions.LandingJets,
                    0,
                    0,
                    lookX,
                    LookAxis(groundPitchError, 4_000));
            }

            return new SimInput(0, 0, SimActions.ToggleMode);
        }

        if (state.Energy < SortieRecoverEnergy)
        {
            // Out of store: put it down deliberately.
            return new SimInput(0, -1, SimActions.ToggleMode, 0, 0, lookX, 0);
        }

        // Climb clear of the ground-effect band first; it fights the nose and
        // the drones cruise five to fifty units above the terrain anyway.
        double pitchError = altitude < 9_000
            ? -0.25 - (state.FacingPitchMicroRad / 1_000_000d)
            : PitchErrorTo(state, aim, horizontal);
        short lookY = LookAxis(pitchError, 4_000);

        // Throttle: full only when there is ground to make up. Closing at
        // 18 m/s on a target 6 m away just produces another overshoot, and
        // every tick of full throttle is store that the next pass needs.
        sbyte throttle = horizontal > 60_000 ? (sbyte)1 : (sbyte)0;
        if (horizontal < 20_000 || Math.Abs(yawError) > 0.7)
        {
            throttle = -1;
        }

        double tolerance = FireTolerance(horizontal);
        SimActions actions =
            altitude >= 6_000 &&
            horizontal is > 3_000 and < 50_000 &&
            Math.Abs(yawError) < tolerance &&
            Math.Abs(pitchError) < tolerance
                ? SimActions.Fire
                : SimActions.None;
        return new SimInput(0, throttle, actions, 0, 0, lookX, lookY);
    }

    /// <summary>How long one crab is held before it is reversed.</summary>
    private const int JetStrafeSegmentTicks =
        5 * SimulationConstants.TicksPerSecond;

    /// <summary>
    /// The altitude band the crabbed jet is flown in. The floor keeps the nose
    /// out of the released ground effect; the ceiling exists because a crabbing
    /// jet sheds a vertical rate only through thrust.
    /// </summary>
    private const int WaveTwoFloorMillimeters = 9_000;

    private const int WaveTwoCeilingMillimeters = 26_000;

    /// <summary>
    /// Slant range beyond which reversing the crab is free. This is the shipped
    /// <c>Drone Vulcan Cannon</c> <c>CWeaponMaxRange</c> of 40.0 plus a margin
    /// for the closure that happens while the new crab builds.
    /// </summary>
    private const int WaveTwoSafeReversalMillimeters = 55_000;

    /// <summary>
    /// Clearance a landing site needs above the released water plane.
    /// <c>Simulation.UpdateWalkerGroundContact</c> declares the player in water
    /// when the sampled ground is at or below
    /// <c>Level100Terrain.WaterElevationMillimeters</c>, so this is a small
    /// margin on that test and not an invented shoreline. Measured: a 2 m
    /// margin classified the beat-9 charging position itself - sampled ground
    /// -598 mm against a -1160 mm water plane - as sea, and the driver spent
    /// the whole beat climbing "home" to where it already was.
    /// </summary>
    private const int DryGroundClearanceMillimeters = 300;

    /// <summary>Is the terrain under this point below the water plane?</summary>
    private bool OverWater(SimVector2 position) =>
        _terrain.SampleGroundElevationMillimeters(position) <
            Level100Terrain.WaterElevationMillimeters + DryGroundClearanceMillimeters;

    /// <summary>
    /// The released 3D centre distance, which is what every shipped range gate
    /// on this fight actually tests.
    /// </summary>
    private static double SlantRange(WorldSnapshot state, SimVector3 position)
    {
        double deltaX = (double)position.X - state.PlayerPosition.X;
        double deltaY = (double)position.Y - state.PlayerElevationMillimeters;
        double deltaZ = (double)position.Z - state.PlayerPosition.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }

    /// <summary>
    /// Beat 9's wave, which is a different fight from beat 7's and is driven
    /// separately for that reason.
    ///
    /// <para><b>Wave 1 does not shoot back.</b> <c>AirborneDrone1.msl</c> flies
    /// a route; <c>AirborneDrone2.msl</c>'s <c>init()</c> issues
    /// <c>Attack(player)</c> at line 26 and the six spawns carry a
    /// <c>Drone Vulcan Cannon</c> and a <c>Forseti Drone Missile Launcher</c>
    /// each. So beat 7 is a gunnery exercise and beat 9 is a dogfight, and the
    /// beat-7 driver above is deliberately left exactly as measured rather than
    /// generalised over both.</para>
    ///
    /// <para><b>What the shipped weapon records say the fight is.</b></para>
    /// <list type="bullet">
    ///   <item><description><c>Drone Vulcan Cannon</c>: <c>CWeaponMaxRange</c>
    ///   40.0, burst 8 at 0.15 s, reload 1.0 s, round <c>Blaster</c> at
    ///   <c>CRoundVelocity</c> 45.0 for <b>200 incoming damage</b> a hit. Six
    ///   drones is 48 rounds a second, or 9,600 incoming damage a second if
    ///   they all land. <c>CBattleEngine::Damage</c> then routes that through
    ///   Aquila shields; it is not 9,600 direct hull loss.</description></item>
    ///   <item><description><c>Forseti Drone Missile Launcher</c>:
    ///   <c>CWeaponMinRange</c> 20.0, <c>CWeaponMaxRange</c> 80.0, reload
    ///   <b>10.0 s</b>, round <c>Forseti Missile</c> at 15.0 for <b>2,500
    ///   aggregate incoming damage</b> (2.0 round plus 0.5 explosion). Whether
    ///   those become one or two retail Damage calls remains open. Slow and
    ///   rare, and it cannot fire inside 20 m at all.</description></item>
    /// </list>
    ///
    /// <para><b>The Vulcan is the whole threat, and it is beaten by moving.</b>
    /// Neither drone weapon mode carries <c>CWeaponTrack</c> and neither
    /// carries a lead law, so <c>LaunchActorRound</c> aims every Blaster at the
    /// player's position on the tick it is fired. The impact envelope is the
    /// battle engine's own 0.4 m radius (<c>CBattleEngine::GetRadius</c>). A
    /// Blaster fired from 30 m is in flight for 0.67 s, so it needs the player
    /// to cross less than 0.4 m in that time: <b>a perpendicular speed above
    /// about 0.6 m/s makes it a clean miss</b>. Flying straight at a drone is
    /// the one manoeuvre that supplies less than that, and it is what the
    /// previous revision did - measured, it lost 8,800 hull in 2.4 released
    /// seconds between t9958 and t10030 and reached the sub-40 % abort with
    /// two of six drones down.</para>
    ///
    /// <para>So this driver holds <see cref="SimInput.MoveX"/> throughout.
    /// That is <c>TUTORIAL_STRAFE</c>, which the LevelScript plays at the head
    /// of <c>event("Reached Target Zone 3")</c> immediately before this wave,
    /// and it is worth more than the lateral acceleration alone:
    /// <c>Simulation.UpdateJetMovement</c> refreshes
    /// <c>_jetStrafeTicksRemaining</c> on every strafing tick and
    /// <c>Simulation.JetAlignmentPermille</c> returns <b>0</b> for as long as
    /// that window is open, so the released velocity-to-forward alignment stops
    /// pulling the flight path back onto the nose and the airframe holds a
    /// standing crab. The reticle does not move; the aeroplane does.</para>
    ///
    /// <para>Historical stand-off experiments tried to reduce Blaster hits by
    /// increasing range. Their counts below describe the model at that time;
    /// they do not establish a universal range/velocity law.
    /// <see cref="Level100FullChainTests.BlasterDamageMatchesCausalRoundReceipts"/>
    /// now checks causal accounting for the current model. Both experiments
    /// held a band by putting the nearest drone off the nose while inside the
    /// floor and re-attacking past the ceiling:</para>
    ///
    /// <list type="table">
    ///   <item><description>11-19 m band, 90 degrees off: the historical
    ///   pre-funnel run's net hull loss attributed to Blaster hits fell from
    ///   7,600 to <b>200</b> - one hit in the whole beat - but
    ///   <c>Forseti</c> hits rose from two to <b>seven</b> and the run finished
    ///   with <b>zero</b> kills at 2,500 hull.</description></item>
    ///   <item><description>10-16 m band, 60 degrees off, to stop the airframe
    ///   overshooting into missile range: the historical pre-funnel model
    ///   counted 26 Blaster hits and three Forseti, leaving 7,300 hull - the
    ///   same total as that driver - and again <b>zero</b> kills.</description></item>
    /// </list>
    ///
    /// <para>Both fail for the same two shipped reasons. First, the
    /// <c>Forseti Drone Missile Launcher</c> carries <c>CWeaponMinRange</c>
    /// 20.0 and <c>CanActorWeaponFire</c> rejects the shot below it, so knife
    /// range is the only place in the level where the 2,500-incoming-damage
    /// weapon cannot fire at all. Its aggregate input is 12.5 times a
    /// 200-damage Blaster, but shield routing means that is not a 12.5x hull
    /// price. Second, and decisively, the Aquila cannot hover -
    /// the released speed correction floors the magnitude at
    /// <c>JetMinimumSpeedPerTick</c> even at <c>MoveZ</c> -1 - so a range band
    /// is held only by pointing the nose away from the target, and the time
    /// spent doing that is the same time the driver needs to hold a drone on
    /// the reticle. The stand-off does not resolve the track-or-dodge conflict;
    /// it renames it.</para>
    /// </summary>
    private SimInput EngageWaveTwo(WorldSnapshot state, Level100ActorSnapshot target)
    {
        if (state.Transition != VehicleTransition.None)
        {
            return SimInput.Idle;
        }

        SimVector3 aim = WaveTwoAimPoint(state, target);
        double horizontal = Horizontal(state, aim);
        double slant = SlantRange(state, aim);
        double yawError = YawErrorTo(state, aim.X, aim.Z);
        short lookX = LookAxis(yawError, 2_000);
        int altitude = state.PlayerAltitudeAboveGroundMillimeters;

        if (state.Mode != VehicleMode.Jet)
        {
            if (state.Energy < SortieLaunchEnergy)
            {
                double groundPitchError = -(state.FacingPitchMicroRad / 1_000_000d);
                return new SimInput(
                    0,
                    0,
                    state.PlayerOnGround ? SimActions.None : SimActions.LandingJets,
                    0,
                    0,
                    lookX,
                    LookAxis(groundPitchError, 4_000));
            }

            return new SimInput(0, 0, SimActions.ToggleMode);
        }

        // Never fight, and above all never come down, over the sea. The
        // released water path loses the level at full hull, and a drone that
        // has drifted out over the water is not worth following. The recovery
        // heading is the last place the player actually stood.
        bool overWater = OverWater(state.PlayerPosition);
        if (overWater)
        {
            yawError = YawErrorTo(state, _lastDryGround.X, _lastDryGround.Z);
            lookX = LookAxis(yawError, 2_000);
        }

        // Altitude, and this is the price of the crab. While MoveX is held
        // `JetAlignmentPermille` is 0, so the released alignment never pulls
        // the velocity back onto the nose - which is the point - but it also
        // means a vertical rate, once acquired, is not shed by anything except
        // thrust pointed the other way. Measured with the beat-7 driver's
        // "climb whenever below 9 m" rule and a held strafe: the jet wound
        // itself up to 57 m, ran the store dry there, dropped to walker and
        // fell out of the sky with all six drones untouched.
        //
        // So the crab is flown with an explicit altitude band and a rate term
        // instead.
        //
        // The correction is a BIAS ON THE AIM PITCH, not an alternative to it,
        // and the rate term is on whether or not the band is broken. The
        // earlier revision only corrected while outside the band and handed the
        // pitch axis back to the target inside it. That does not hold a band:
        // with the alignment at 0 the vertical velocity is a free integrator,
        // so the band was left at whatever rate the last tracking excursion had
        // built and the correction only ever fought the overshoot. Measured
        // with the constant applied and that controller: the jet climbed to
        // 31.1 m against a 26 m ceiling and spent the rest of the beat looking
        // down at drones from above, firing on 363 of 2,503 ticks.
        //
        // A rate term added to the aim pitch vanishes at steady state, so it
        // costs nothing once the vertical rate is dead - unlike an alternative
        // pitch command, which is off-target for as long as it is in charge.
        double aimPitch = -Math.Atan2(
            aim.Y - state.PlayerElevationMillimeters,
            Math.Max(1.0, horizontal));
        double bandBias = 0;
        if (altitude < WaveTwoFloorMillimeters)
        {
            bandBias = (altitude - WaveTwoFloorMillimeters) / 40_000d;
        }
        else if (altitude > WaveTwoCeilingMillimeters)
        {
            bandBias = (altitude - WaveTwoCeilingMillimeters) / 40_000d;
        }

        double verticalCorrection = Math.Clamp(
            bandBias +
                (state.PlayerVerticalVelocityMillimetersPerTick /
                    WaveTwoVerticalRateDivisor),
            -0.35,
            0.35);
        double commandedPitch = aimPitch + verticalCorrection;
        double altitudePitchError =
            commandedPitch - (state.FacingPitchMicroRad / 1_000_000d);

        // Both airborne axes are RATE commands from here down. See
        // `RateCommand`: on the ground the released input gain is speed
        // scheduled and the inversion below does not hold, which is why the
        // walker branch above still uses the proportional law.
        short lookYAltitude = RateCommand(
            WaveTwoPitchLambda * altitudePitchError * 1_000_000d,
            state.WalkerPitchVelocityMicroRadPerTick,
            SimulationConstants.JetPitchInputMicroRadPerTick);
        lookX = RateCommand(
            WaveTwoYawLambda * yawError * 1_000_000d,
            state.WalkerYawVelocityMicroRadPerTick,
            SimulationConstants.JetYawInputMicroRadPerTick);

        // Missile defence remains the larger half of this fight. The previous
        // direct-hull model showed 2,500/2,900 drops, which identified Forseti's
        // 2.0 round + 0.5 explosion with an occasional 200 Blaster, but those
        // hull deltas are historical evidence about that superseded model—not
        // the released shield funnel. Six launchers on a 10 s reload still put
        // roughly eighteen missiles in the air; the survivability budget must
        // be read from explicit Damage events and post-funnel resources.
        //
        // The released seeker is beatable and the shipped record says exactly
        // how. Round vtable slot 66 takes the direction to the target into the
        // round's own frame and, with `CRoundWeirdoSeek` 0, **clears the target
        // reader outright** when the forward component falls below
        // cos(`CRoundSeekAngle`) - 45 degrees. `CRoundSeek 3` never
        // re-acquires (`== 1` at
        // `CRound__SelectBestTargetReaderAndSyncAimState` is the only
        // self-acquire path in the image), so a dropped lock is dropped for
        // good. `CRoundTurnRate` is 0.04886922 rad per released tick, i.e.
        // 0.977 rad/s, while the jet's own yaw velocity accumulates to
        // `JetYawInputMicroRadPerTick / (1 - JetYawRetention)` = about
        // 1.47 rad/s. **The airframe out-turns the seeker**, so putting the
        // missile on the beam and holding full throttle opens the cone.
        Level100ActorRoundSnapshot? missile = InboundMissile(state);
        if (!overWater && missile is not null)
        {
            double bearing = BearingTo(state, missile.PositionMillimeters);
            double facingYaw = state.FacingYawMicroRad / 1_000_000d;
            // Either perpendicular puts the player outside the seeker's
            // 45-degree lock cone. Choose the one the nose can reach first;
            // tying every break to the current crab side made an ordinary
            // evasive turn needlessly take the long way round after the shipped
            // walker-yaw correction changed the phase at which the cold career
            // entered this fight.
            double clockwiseBeamError =
                NormalizeRadians(bearing + (Math.PI / 2) - facingYaw);
            double counterClockwiseBeamError =
                NormalizeRadians(bearing - (Math.PI / 2) - facingYaw);
            double beamError = Math.Abs(clockwiseBeamError) <=
                Math.Abs(counterClockwiseBeamError)
                    ? clockwiseBeamError
                    : counterClockwiseBeamError;
            short breakLookX = RateCommand(
                WaveTwoYawLambda * beamError * 1_000_000d,
                state.WalkerYawVelocityMicroRadPerTick,
                SimulationConstants.JetYawInputMicroRadPerTick);

            // The break does not stop the guns. The nose sweeps across the
            // wave while it turns, and a volley taken on the way past costs
            // nothing: SimActions.Fire is gated by MechVulcanReloadTicks and
            // no energy, so a shot that is on the target is free whatever the
            // aeroplane is doing.
            return new SimInput(
                (sbyte)WaveTwoCrab(state),
                1,
                _waveTwoTriggerHeldShut
                    ? SimActions.None
                    : WaveTwoFireGate(
                        state, aim, slant, altitude),
                0,
                0,
                breakLookX,
                lookYAltitude);
        }

        if (!overWater && state.Energy < SortieRecoverEnergy)
        {
            return new SimInput(0, -1, SimActions.ToggleMode, 0, 0, lookX, 0);
        }

        short lookY = lookYAltitude;

        // Throttle. Backing off is what makes this airframe turn: the released
        // speed correction drives it to JetMinimumSpeedPerTick (6 m/s) at
        // MoveZ -1 while the yaw rate stays a fixed
        // JetYawInputMicroRadPerTick, so the turn radius collapses from about
        // 61 m to about 20 m. Measured: a revision that never backed off could
        // not stay on any drone and flew the fight 180 m off station with no
        // kills at all.
        sbyte throttle = horizontal > 60_000 || overWater ? (sbyte)1 : (sbyte)0;
        if (!overWater && (horizontal < 20_000 || Math.Abs(yawError) > 0.7))
        {
            throttle = -1;
        }

        SimActions actions = overWater || _waveTwoTriggerHeldShut
            ? SimActions.None
            : WaveTwoFireGate(state, aim, slant, altitude);
        return new SimInput(
            (sbyte)WaveTwoCrab(state),
            throttle,
            actions,
            0,
            0,
            lookX,
            lookY);
    }

    /// <summary>
    /// The held strafe.
    ///
    /// <para><b>It is never reversed while a drone is inside its Vulcan's
    /// 40 m envelope.</b> The crab takes about five released seconds to build
    /// through the released friction, so a reversal is a window in which the
    /// perpendicular speed passes through zero - and that window is exactly
    /// what the Blaster needs. Measured on a run that reversed every 150 ticks
    /// regardless: hull held flat at 14,200 for a hundred ticks while the crab
    /// was developed, then fell 14,200 to 7,500 across the reversal.</para>
    /// </summary>
    private int WaveTwoCrab(WorldSnapshot state)
    {
        if (_waveTwoCrabHeldStill)
        {
            return 0;
        }

        if (state.Tick - _crabSinceTick >= JetStrafeSegmentTicks &&
            NearestWaveTwoSlant(state) > WaveTwoSafeReversalMillimeters)
        {
            _crabSinceTick = state.Tick;
            _crabDirection = -_crabDirection;
        }

        return _crabDirection;
    }

    private int _crabDirection = 1;
    private int _crabSinceTick;


    /// <summary>
    /// Pull the trigger whenever the reticle is genuinely on a drone, whatever
    /// the aeroplane is doing. The `Mech Air Bullet` costs no energy and the
    /// cadence is gated by its stored ready time, so a shot taken while
    /// the nose sweeps past during a missile break is free.
    /// </summary>
    internal static SimActions WaveTwoFireGate(
        WorldSnapshot state,
        SimVector3 aim,
        double slant,
        int altitude)
    {
        double tolerance = FireTolerance(slant);

        // Controller Fire precedes actor/mission events and player Move.
        // Test the retained pose; this update's look and movement have not yet
        // changed the pose. Steering and this gate use the same camera ray;
        // production launch handles the transformed emitter and convergence.
        double horizontal = Horizontal(state, aim);
        double yawError = YawErrorTo(state, aim.X, aim.Z);
        double pitchError = PitchErrorTo(state, aim, horizontal);

        return altitude >= 6_000 &&
            slant is > 800 and < 55_000 &&
            Math.Abs(yawError) < tolerance &&
            Math.Abs(pitchError) < tolerance
                ? SimActions.Fire
                : SimActions.None;
    }

    /// <summary>
    /// The released jet attitude plant, written from the two constants that
    /// produce it in <c>Simulation.UpdateJetOrientation</c>:
    /// <c>velocity(t+1) = RETAIN * velocity(t) + fullScale * response</c>,
    /// then <c>angle(t+1) = angle(t) + velocity(t+1)</c>.
    /// </summary>
    private const double AttitudeRetain =
        (double)SimulationConstants.WalkerYawRetentionNumerator /
        SimulationConstants.WalkerYawRetentionDenominator;

    /// <summary>
    /// Fraction of the remaining yaw error the controller asks the airframe to
    /// remove per tick.
    ///
    /// <para>With <see cref="RateCommand"/> inverting the lag this is a
    /// first-order closed-loop pole rather than a gain: against a stationary
    /// reference the error decays as <c>(1 - lambda)</c> per tick for as long
    /// as the airframe has the authority, and saturates into a bang-bang slew
    /// when it does not. That is why it can be near unity at all - the old
    /// proportional law had to stay slow because it had no braking term.</para>
    ///
    /// <para>Swept against the whole chain at pitch lambda 0.30, wave-2 kills:
    /// 0.20 -&gt; 0, 0.30 -&gt; 1, 0.45 -&gt; 2, 0.60 -&gt; 2, 0.80 -&gt; 6,
    /// 0.85 -&gt; 6, 0.90 -&gt; 6, 0.95 -&gt; 3. A three-point plateau, not a
    /// spike. <b>That sweep was run on the 30 Hz Core</b>, which is why the
    /// number it produced is handed to <see cref="ErrorPole"/> rather than
    /// used as a stick position directly.</para>
    /// </summary>
    private static readonly double WaveTwoYawLambda = ErrorPole(0.90);

    /// <summary>
    /// The same pole on the pitch axis, and it is deliberately a third of the
    /// yaw one.
    ///
    /// <para>Two reasons, both measurable. The released pitch authority is
    /// <c>WalkerPitchInputMicroRadPerTick</c> against
    /// <c>JetYawInputMicroRadPerTick</c> - a ratio of 2.49, unchanged by the
    /// 20 Hz migration, which moved the pair 3,938/9,805 to 8,547/21,277 - so
    /// the same pole asks the pitch axis for a rate it more often cannot
    /// deliver.
    /// And the pitch REFERENCE is not a clean bearing: it is the aim pitch
    /// plus <c>verticalCorrection</c>, which already carries a derivative term
    /// in the airframe's own vertical velocity. Closing a fast loop around a
    /// reference that contains a rate term of the same loop is the classic
    /// destabilising case.</para>
    ///
    /// <para>Measured at yaw lambda 0.90, wave-2 kills by pitch lambda:
    /// 0.15 -&gt; 1, 0.20 -&gt; 6, 0.25 -&gt; 3, 0.30 -&gt; 6, 0.35 -&gt; 5,
    /// 0.40 -&gt; 0. Over twenty one-permille perturbations of beat 9, 0.20
    /// and 0.30 are indistinguishable (full clear on 10 and 11 of 20
    /// respectively, never fewer than three kills on either); 0.30 is kept
    /// because it also holds the higher floor on wave-2 spawns damaged. That
    /// sweep, like the yaw one, was run on the 30 Hz Core.</para>
    /// </summary>
    private static readonly double WaveTwoPitchLambda = ErrorPole(0.30);

    /// <summary>
    /// The rate at which the two poles above were swept: <b>30 Hz</b>, the Core
    /// tick rate in force when <see cref="EngageWaveTwo"/> was tuned.
    ///
    /// <para>It is named rather than inlined because a closed-loop pole is a
    /// property of the controller AND the rate it runs at, and the 20 Hz
    /// migration proved that carrying one as a bare literal is silent: the
    /// three constants on this page were the only rate-denominated numbers in
    /// the whole driver that the migration did not move, and they are what took
    /// beat 9 from six kills to nought.</para>
    /// </summary>
    private const double PoleSweepRateHertz = 30d;

    /// <summary>
    /// Re-expresses a closed-loop error pole swept at
    /// <see cref="PoleSweepRateHertz"/> for whatever rate Core runs at now.
    ///
    /// <para><c>1 - lambda</c> is the fraction of the tracking error that
    /// survives one tick - a RETENTION - so it converts by the 20 Hz
    /// migration's <b>rule R2</b>, <c>r_new = r_old ^ (oldRate / newRate)</c>.
    /// At 30 -&gt; 20 Hz that is the same <c>r^1.5</c> that took the landing
    /// thruster from <c>0.983263</c> to retail's shipped <c>0.975</c>
    /// (<see cref="SimulationConstants.WalkerLandingJetHorizontalRetentionNumerator"/>).
    /// Yaw 0.90 becomes 0.968377 and pitch 0.30 becomes 0.414338.</para>
    ///
    /// <para><b>This is a conversion and not a re-tune, and the difference is
    /// measurable.</b> Carrying the 30 Hz poles unchanged onto the 20 Hz plant
    /// asks for a closed loop 1.5x slower in real time than the one that was
    /// swept, while the airframe's authority in radians per SECOND is
    /// unchanged - the migration preserved it exactly, 9,805/(1-0.861774) at
    /// 30 Hz and 21,277/(1-0.8) at 20 Hz are both 2.128 rad/s. Measured on the
    /// returning-player chain and the cold career together, with the vertical
    /// rate term at <see cref="WaveTwoVerticalRateDivisor"/>:</para>
    ///
    /// <list type="table">
    ///   <item><description>poles unconverted, divisor unconverted:
    ///   returning player 5 kills on the abort branch, cold career <b>Lost /
    ///   PlayerDeath</b> at t6877 with 1 kill.</description></item>
    ///   <item><description>divisor converted only: returning player 6 kills,
    ///   cold career 0 kills on the abort branch.</description></item>
    ///   <item><description>poles converted only: cold career 6 kills,
    ///   returning player 0 kills on the abort branch.</description></item>
    ///   <item><description><b>both converted: six kills, objective 4
    ///   Complete and no abort on BOTH careers</b>, at 19,800 and 9,200
    ///   hull.</description></item>
    /// </list>
    ///
    /// <para>Each single-term arm passes one career and fails the other, which
    /// is what fitting a chaotic objective looks like; only the complete
    /// conversion passes both, and it was not chosen by that score - it is the
    /// arithmetic.</para>
    /// </summary>
    private static double ErrorPole(double lambdaAtSweepRate) =>
        1d - Math.Pow(
            1d - lambdaAtSweepRate,
            PoleSweepRateHertz / SimulationConstants.TicksPerSecond);

    /// <summary>
    /// Divisor turning the airframe's own vertical rate into the pitch bias
    /// that damps it, in <see cref="EngageWaveTwo"/>'s altitude band.
    ///
    /// <para><b>It divides a PER-TICK velocity, so it carries the tick rate.</b>
    /// The numerator is
    /// <see cref="WorldSnapshot.PlayerVerticalVelocityMillimetersPerTick"/>;
    /// the same physical descent is 1.5x more millimetres per tick at 20 Hz
    /// than at 30, so a fixed divisor makes the derivative term 1.5x stronger
    /// for nothing. That is the migration's <b>rule R1</b> (velocity x1.5)
    /// applied to a denominator, and 400 x 30/20 = 600 exactly.</para>
    ///
    /// <para>Left at 400 the term saturated its own +-0.35 rad clamp on any
    /// ordinary sink rate - the released gravity is also 11 % stronger since
    /// the migration recovered it from the shipped floats - so the pitch axis
    /// spent beat 9 fighting its own altitude instead of tracking a drone.</para>
    /// </summary>
    private static readonly double WaveTwoVerticalRateDivisor =
        400d * PoleSweepRateHertz / SimulationConstants.TicksPerSecond;

    /// <summary>
    /// The stick position that makes the NEXT tick's angular velocity equal
    /// the requested one, given the velocity the airframe already carries.
    ///
    /// <para><b>This is the whole controller change, and it is a frame error
    /// in TIME rather than in space.</b> Core integrates a look axis into an
    /// angular VELOCITY behind a retention pole
    /// (<c>WalkerYawRetentionNumerator</c> 0.861774, so a steady stick reaches
    /// 7.23x its per-tick increment) and only then into an angle. A stick
    /// position is therefore a rate demand with a five-tick lag, not an angle
    /// demand, and a proportional controller on angle error carries no term
    /// that can take an accumulated rate back out again: it can only stop
    /// turning by waiting for the pole. Inverting the recurrence gives that
    /// term for nothing, and every quantity it needs is already in the
    /// snapshot.</para>
    ///
    /// <para><b>Measured, on the committed driver, over the 1,957 ticks of
    /// beat 9.</b> Median absolute yaw error 0.79 rad and upper-quartile
    /// 1.99 rad against a fire tolerance of 0.02-0.15, with roughly three
    /// times the required authority available; a firing solution existed on
    /// 7.3 % of ticks; bank angle reached 69 degrees because the yaw stick was
    /// saturated for most of the beat and <c>Simulation.cs:1255-1259</c> drives
    /// roll from the same axis. None of that is a gain problem - the airframe
    /// was being asked for the right thing in the wrong units.</para>
    ///
    /// <para><b>It is not a Core bypass and it is not privileged state.</b>
    /// <see cref="WorldSnapshot.WalkerYawVelocityMicroRadPerTick"/> and
    /// <see cref="WorldSnapshot.WalkerPitchVelocityMicroRadPerTick"/> are the
    /// same public snapshot fields this driver already reads for altitude, and
    /// the result still goes through <see cref="LookAxisCommand"/> and then
    /// through Core's own <c>LookAxisResponse</c>. What a player has that the
    /// old driver did not is the knowledge that the nose keeps swinging after
    /// the stick is centred.</para>
    ///
    /// <para><b>Airborne only.</b> <c>Simulation.UpdateJetOrientation</c>
    /// scales the axis by a <c>ratePermille</c> that is 1000 only in flight
    /// past the input ramp; on the ground it is speed-scheduled and this
    /// inversion does not hold. The walker branch of
    /// <see cref="EngageWaveTwo"/> is deliberately left on the proportional
    /// law.</para>
    /// </summary>
    private static short RateCommand(
        double desiredMicroRadPerTick,
        int measuredMicroRadPerTick,
        int fullScaleMicroRadPerTick)
    {
        double increment =
            desiredMicroRadPerTick - (AttitudeRetain * measuredMicroRadPerTick);
        int permille = (int)Math.Round(
            increment * 1_000d / fullScaleMicroRadPerTick);
        return LookAxisCommand.ForResponsePermille(
            Math.Clamp(permille, -1_000, 1_000));
    }

    /// <summary>
    /// Where the reticle goes on a wave-2 drone.
    ///
    /// <para>Start from the retained player and drone poses: controller Fire
    /// runs before either Move. Lead the drone through the bullet's flight
    /// time in three dimensions, using slant range. The shared ground-target
    /// <see cref="AimPoint"/> remains unchanged.</para>
    ///
    /// <para>The round is the jet's <c>Mech Air Bullet</c> at
    /// <c>CRoundVelocity</c> 60.0, not the walker Pulse round's 35.0; the
    /// intercept is solved by three fixed-point iterations because the flight
    /// time depends on the range to the lead point and the lead point depends
    /// on the flight time.</para>
    /// </summary>
    private static SimVector3 WaveTwoAimPoint(
        WorldSnapshot state,
        Level100ActorSnapshot target)
    {
        SimVector3 position = target.Pose.PositionMillimeters;
        SimVector3 velocity = target.Pose.LinearVelocityMillimetersPerTick;

        double muzzleX = state.PlayerPosition.X;
        double muzzleY = state.PlayerElevationMillimeters;
        double muzzleZ = state.PlayerPosition.Z;

        double droneX = position.X;
        double droneY = position.Y;
        double droneZ = position.Z;

        double flightTicks = 0;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            double deltaX = droneX + (velocity.X * flightTicks) - muzzleX;
            double deltaY = droneY + (velocity.Y * flightTicks) - muzzleY;
            double deltaZ = droneZ + (velocity.Z * flightTicks) - muzzleZ;
            flightTicks = Math.Sqrt(
                (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ)) /
                SimulationConstants.MechAirBulletSpeedPerTick;
        }

        return new SimVector3(
            (int)(droneX + (velocity.X * flightTicks)),
            (int)(droneY + (velocity.Y * flightTicks)),
            (int)(droneZ + (velocity.Z * flightTicks)));
    }

    /// <summary>
    /// Slant range at which a still-locked <c>Forseti Missile</c> is answered.
    /// It is inside <c>CWeaponMaxRange</c> 80.0 on purpose: breaking at the
    /// launch range would leave the driver evading permanently against six
    /// launchers, and the cone only opens when the bearing rate beats
    /// <c>CRoundTurnRate</c>, which needs the missile close.
    /// </summary>
    private const int MissileBreakRangeMillimeters = 22_000;

    // ==================================================================
    // Beat 9: what was measured on the way to clearing it, including the
    // things that did not work. Kept here rather than in a note because the
    // note is not in this repository and these are expensive to re-derive.
    //
    // WHAT THE OLD DRIVER WAS ACTUALLY DOING, from 1,957 ticks of telemetry:
    //   - median |yaw error| 0.79 rad, upper quartile 1.99, against a fire
    //     tolerance of 0.02-0.15. A firing solution existed on 7.3 % of ticks.
    //   - nineteen target switches, ten of them steps of more than two radians
    //     taken at a slant of 4-15 m. At t13605 the driver held 0.046 rad on
    //     drone 40; one tick later it was asked for 2.692 rad on drone 41.
    //     EVERY hull-loss cluster in the trace is one of those slews.
    //   - bank reached 69 degrees, because the yaw stick was saturated for most
    //     of the beat and Simulation.cs:1255-1259 drives roll from that same
    //     axis.
    //   - of 76 rounds fired at wave 2, measured on the swept segment against
    //     the drone origin: closest approach median 1,198 mm, and NOT ONE
    //     inside 500 mm. Two hits in the whole beat.
    //
    // TRIED AND MEASURED WORSE. All of these are single changes against the
    // shipped controller, all confined to this method, all at the same
    // 36,000-tick budget:
    //   - target selection by bearing-weighted cost with a commitment window
    //     (the obvious fix for the slews): 2 kills, 4 spawns damaged, and 0-2
    //     under perturbation. Worse than doing nothing about it.
    //   - line-of-sight-frame crab: choosing MoveX by projecting the ROLLED
    //     body-right axis (the direction Simulation.UpdateJetMovement actually
    //     applies the strafe along) onto the evasion direction. 0-4 kills, and
    //     1 spawn damaged at the unperturbed point. The frame transform is
    //     real and the arithmetic is right; reversing a crab still costs the
    //     five released seconds it takes to rebuild one.
    //   - a two-phase manoeuvre - stop strafing to get the flight path back,
    //     fly a tangent to a 14 m circle, then re-strafe to freeze the path
    //     and track with the nose. This is the manoeuvre the released
    //     mechanism suggests: JetAlignmentPermille is ~0 for 120 ticks after
    //     ANY strafing tick, so a driver that holds MoveX every tick of beat 9
    //     has switched off AlignVelocityToForward - and with the mix at 1000
    //     that call puts the whole velocity on the nose in one tick, i.e. a
    //     3 m turn radius at minimum speed. Measured: 0 kills, 0 spawns
    //     damaged, and the beat ended 700 ticks EARLIER because the hull fell
    //     faster.
    //   - survive-first throttle (hold MoveZ +1 whenever the crossing speed is
    //     under what 18/slant needs): on its own, 2 kills and 5 spawns damaged
    //     and 2.2x the time alive - the best single change other than the
    //     controller - but it loses the level outright on two of five
    //     perturbations, and combined with the rate controller it is worse
    //     than the rate controller alone.
    //   - line-of-sight rate feed-forward added to the rate command: 4-6
    //     kills, never better than without it.
    //   - tightening the wave-2 fire tolerance from 1,100 mm of target to 800:
    //     six kills on five of five NARROW perturbations and three to four on
    //     the wider set. A spike, and not shipped for exactly the reason the
    //     previous 3-kill spike was not.
    //   - deferring the flight-leg landing while the descent path is over
    //     water: inert. The landing that drowns a six-kill run is not
    //     committed in FlyLeg at all - NavigateToZone toggles to walker as
    //     soon as the zone is inside 20 m, and on the Target Zone 4 approach
    //     that happens 28 m up.
    //     RESOLVED. That defect is fixed - see
    //     `ZoneHandoffClearanceMillimeters`, which gives the hand-off the
    //     altitude term it never had - and the water guard remains the wrong
    //     shape: the fix is an ALTITUDE predicate, because the morph exists to
    //     satisfy `InJetMode() == FALSE` and nothing airborne can satisfy it.
    //     The measured worry in the sentence above - "the fix is in a method
    //     shared with beats 1-8" - was real and is measured rather than
    //     assumed: beats 6 and 8 hand off at 18,969 mm and 19,759 mm of surface
    //     clearance, which is NOT "on the ground", so the diagnosis's claim
    //     that an altitude term is automatically a no-op there is FALSE. It is
    //     a no-op at 20,000 mm and only at 20,000 mm; the whole measurement is
    //     on `ZoneHandoffClearanceMillimeters`.
    // ==================================================================

    /// <summary>
    /// The nearest still-locked released seeker heading for the player, or
    /// null.
    ///
    /// <para>Reading the round list is the same class of observation this
    /// driver already makes of <c>actor.Health</c>: a player sees the missile
    /// and its smoke trail, not this record. It is input-equivalent, not
    /// perception-equivalent, and the class remarks say so.</para>
    /// </summary>
    private static Level100ActorRoundSnapshot? InboundMissile(WorldSnapshot state)
    {
        Level100ActorSnapshot? player = state.Level100Actors.Actors
            .FirstOrDefault(actor => actor.Name == "Player 1");
        if (player is null)
        {
            return null;
        }

        Level100ActorRoundSnapshot? nearest = null;
        double nearestRange = MissileBreakRangeMillimeters;
        foreach (Level100ActorRoundSnapshot round in
                 state.Level100ActorMechanics.ActorRounds)
        {
            if (round.Kind != Level100ActorRoundKind.ForsetiMissile ||
                !round.Locked ||
                round.TargetActorId != player.ActorId)
            {
                continue;
            }

            double range = SlantRange(state, round.PositionMillimeters);
            if (range < nearestRange)
            {
                nearestRange = range;
                nearest = round;
            }
        }

        return nearest;
    }

    private static double BearingTo(WorldSnapshot state, SimVector3 position) =>
        Math.Atan2(
            -((double)position.X - state.PlayerPosition.X),
            (double)position.Z - state.PlayerPosition.Z);

    /// <summary>
    /// Slant range to the nearest live wave-2 drone, which is the quantity
    /// <c>CUnit__ClassifyTargetRangeBand</c> tests against
    /// <c>CWeaponMaxRange</c> 40.0.
    /// </summary>
    private static double NearestWaveTwoSlant(WorldSnapshot state)
    {
        double nearest = double.MaxValue;
        foreach (Level100ActorSnapshot actor in state.Level100Actors.Actors)
        {
            if (actor.TargetGroup != Level100MissionTargetGroup.AirborneTargets2 ||
                !actor.Active ||
                actor.Lifecycle != Level100ActorLifecycle.Alive)
            {
                continue;
            }

            nearest = Math.Min(nearest, SlantRange(state, actor.Pose.PositionMillimeters));
        }

        return nearest;
    }

    // ------------------------------------------------------------------
    // Geometry a player can see
    // ------------------------------------------------------------------

    /// <summary>
    /// Where on the target the reticle goes. A player sweeps the body rather
    /// than shooting at the pivot; the sweep also covers the unmodelled
    /// difference between an actor's origin and its hull centre.
    /// </summary>
    private SimVector3 AimPoint(
        WorldSnapshot state,
        Level100ActorSnapshot target,
        bool sweep = true,
        int roundSpeedPerTick = SimulationConstants.ProjectileSpeedPerTick)
    {
        SimVector3 position = target.Pose.PositionMillimeters;
        double rawX = (double)position.X - state.PlayerPosition.X;
        double rawZ = (double)position.Z - state.PlayerPosition.Z;
        double range = Math.Sqrt((rawX * rawX) + (rawZ * rawZ));
        double flightTicks = range / roundSpeedPerTick;

        // The sweep is bounded in *angle*, not in millimetres. A fixed 200 mm
        // ladder is a fifth of a radian at four metres, which fights the pitch
        // controller instead of searching the hull with it. A flying target
        // gets no sweep at all: it is already moving through the ladder faster
        // than the ladder moves, and the shot has to be taken now.
        //
        // The sweep also comes off entirely when a miss would land on an
        // unarmed tutorial target: searching the hull with a ladder means
        // deliberately putting rounds above and below it, and above it is where
        // Target Truck #25 is.
        int aimHeight = 0;
        if (!IsAirborneTarget(target))
        {
            aimHeight = 600;
            if (sweep)
            {
                int ladder = (int)Math.Clamp(range * 0.012, 100, 700);
                aimHeight += (((state.Tick / 11) % 5) - 2) * ladder / 2;
            }
        }

        int aimX = (int)(position.X +
            (target.Pose.LinearVelocityMillimetersPerTick.X * flightTicks));
        int aimZ = (int)(position.Z +
            (target.Pose.LinearVelocityMillimetersPerTick.Z * flightTicks));

        // The reticle never goes into the dirt. Several released targets carry
        // an authored origin at or below the ground the walker stands on -
        // Target Warehouse's is 0.6 m below it, because buildings are still not
        // ground-seated (open list, local-lab/INDEX.md). The ladder is bounded
        // in ANGLE, so at short range it collapses to its 100 mm floor and
        // every rung of it can sit under
        // ground + LineOfSightClearanceMillimeters, which makes
        // HasLineOfSight false from every stance. Measured: the driver orbited
        // Target Warehouse at 3.3-4.4 m for 34,000 ticks and never fired.
        int groundUnderAim = _terrain.SampleGroundElevationMillimeters(
            new SimVector2(aimX, aimZ));
        int aimY = Math.Max(
            position.Y + aimHeight,
            groundUnderAim + (2 * LineOfSightClearanceMillimeters));

        return new SimVector3(aimX, aimY, aimZ);
    }

    private static bool IsAirborneTarget(Level100ActorSnapshot target) =>
        target.TargetGroup is Level100MissionTargetGroup.AirborneTargets1
            or Level100MissionTargetGroup.AirborneTargets2;

    /// <summary>
    /// How tight the aim has to be before firing. A fixed radian tolerance is
    /// wrong at both ends: at 4 m it lets the round miss the hull entirely, and
    /// at 40 m it refuses shots a player would take. This is the angle
    /// subtended by roughly a metre of target at the current range.
    /// </summary>
    private static double FireTolerance(double range) =>
        Math.Clamp(1_100.0 / Math.Max(range, 1.0), 0.02, 0.15);

    /// <summary>
    /// Does the ground let this shot through? Sampled along the ray from the
    /// evidenced muzzle to the aim point. This is the check the previous driver
    /// did not make.
    /// </summary>
    private bool HasLineOfSight(WorldSnapshot state, SimVector3 aim)
    {
        double startX = state.PlayerPosition.X;
        double startZ = state.PlayerPosition.Z;
        // Gun 1 sits 258 mm above the Battle Engine (cockpit2.msh, z down).
        double startY = state.PlayerElevationMillimeters -
            (Level100CockpitEmitters.Gun(Level100CockpitEmitters.PulseGun, walkPose: true).ZMicrometres / 1000.0);
        double deltaX = aim.X - startX;
        double deltaY = aim.Y - startY;
        double deltaZ = aim.Z - startZ;

        for (int sample = 1; sample < LineOfSightSamples; sample++)
        {
            double fraction = (double)sample / LineOfSightSamples;
            int x = (int)(startX + (deltaX * fraction));
            int z = (int)(startZ + (deltaZ * fraction));
            double y = startY + (deltaY * fraction);
            int ground = _terrain.SampleGroundElevationMillimeters(new SimVector2(x, z));
            if (ground + LineOfSightClearanceMillimeters > y)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Is a script-bearing friendly structure standing in the next round's
    /// complete flight corridor? Every
    /// <c>Facilities</c>, <c>TankFactory</c>, <c>Hangar</c> and <c>Turret</c>
    /// actor answers <c>hit(THING_TYPE_AMMUNITION)</c> with
    /// <c>PostEvent("Hit Friendly Building")</c>, and the released LevelScript
    /// turns the twenty-first of those into <c>Friendly Building Destroyed</c>
    /// and then <c>LevelLostString</c>. A player does not shoot through the
    /// tank factory to reach a target behind it or keep firing when the same
    /// ray reaches one behind the target. An autopilot that does loses the
    /// level rather than stalling - which is a worse failure, because it looks
    /// like progress.
    /// </summary>
    private static bool FriendlyStructureOnTheRay(
        WorldSnapshot state,
        Level100ActorSnapshot target,
        SimVector3 aim)
    {
        const int StructureStandOffMillimeters = 9_000;
        double startX = state.PlayerPosition.X;
        double startZ = state.PlayerPosition.Z;
        double deltaX = aim.X - startX;
        double deltaZ = aim.Z - startZ;
        double lengthSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        if (lengthSquared < 1.0)
        {
            return false;
        }

        double roundReachMillimeters =
            state.Level100WalkerSelectedWeapon switch
            {
                Level100MissionWeapon.PulseCannonPod =>
                    SimulationConstants.ProjectileLifetimeTicks *
                    SimulationConstants.ProjectileSpeedPerTick,
                Level100MissionWeapon.MechTwinVulcanCannon =>
                    SimulationConstants.MechBulletLifetimeTicks *
                    SimulationConstants.MechBulletSpeedPerTick,
                _ => 0,
            };
        double maximumProjection =
            roundReachMillimeters / Math.Sqrt(lengthSquared);

        foreach (Level100ActorSnapshot actor in state.Level100Actors.Actors)
        {
            if (actor.ActorId == target.ActorId ||
                actor.TargetGroup != Level100MissionTargetGroup.None ||
                actor.Lifecycle != Level100ActorLifecycle.Alive ||
                actor.Trigger.HasValue ||
                actor.ScriptName is not ("Facilities" or "TankFactory" or "Hangar" or "Turret"))
            {
                continue;
            }

            double toX = actor.Pose.PositionMillimeters.X - startX;
            double toZ = actor.Pose.PositionMillimeters.Z - startZ;
            double projection = ((toX * deltaX) + (toZ * deltaZ)) / lengthSquared;
            if (projection <= 0 || projection > maximumProjection)
            {
                continue;
            }

            double offsetX = toX - (projection * deltaX);
            double offsetZ = toZ - (projection * deltaZ);
            if ((offsetX * offsetX) + (offsetZ * offsetZ) <
                (double)StructureStandOffMillimeters * StructureStandOffMillimeters)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Would a miss land on a tutorial target the script has <b>not armed
    /// yet</b>?
    ///
    /// <para>Killing one is the losing move that
    /// <see cref="Level100FullChainTests.NaiveWalkerAutopilot_BreaksTheTutorialAndLoses"/>
    /// exists to demonstrate, and it is a hazard for a competent driver too.
    /// Every <c>TargetTruckN.msl</c> answers <c>died()</c> with
    /// <c>switch (activated)</c>, and the <c>case FALSE</c> arm is
    /// <c>PostEvent("Broke Tutorial")</c> -&gt;
    /// <c>LevelLostString(LOSE_TUTORIAL_BROKE)</c>.</para>
    ///
    /// <para><c>IsObjective</c> is the observable that tracks those scripts'
    /// <c>activated</c> local: <c>event("Activate Static Targets 2")</c> is the
    /// one handler that both sets <c>activated = TRUE</c> and calls
    /// <c>SetObjective()</c>. A player reads the same thing off the HUD - the
    /// target is not marked yet, so it is not a target yet.</para>
    ///
    /// <para><b>Measured, and the numbers are why this switches the driver's
    /// behaviour instead of vetoing the shot.</b> <c>Target Truck #25</c> is
    /// parked at (31663, 68922) and beat 3's <c>Target Tank #23</c> at
    /// (31409, 70361) - <b>1.46 m apart</b>. The perpendicular distance from
    /// the shot line to the truck therefore never exceeds 1.46 m from any
    /// stance in the level, while the corridor a miss can wander into is 3.0 m
    /// of truck body plus the aim slop <see cref="FireTolerance"/> allows. No
    /// stance clears it, and a revision that treated this predicate as a veto
    /// was measured holding the trigger shut for 900 released seconds at 3.2 m
    /// with a clear ray and a converged reticle.</para>
    ///
    /// <para>What actually killed the truck was two rounds, not a spray: a
    /// pulse round takes 1,800 off a 3,000-hull truck, and the two that reached
    /// it were 0.06 rad high - inside the 0.15 rad
    /// <see cref="FireTolerance"/> ceiling - fired while closing at speed from
    /// 4.9 m above the tank, so they cleared the tank's hull and carried on
    /// down-range into the truck 1.46 m behind it. The answer is therefore the
    /// player's: stop, drop the hull-search ladder, and do not pull the trigger
    /// until the reticle is inside the hull rather than merely on the target.
    /// See <see cref="PrecisionTolerance"/>.</para>
    /// </summary>
    private static bool CollateralRiskBehind(
        WorldSnapshot state,
        Level100ActorSnapshot target)
    {
        // How far past the target a miss still matters. The round is not
        // stopped by the aim point - it flies for
        // ProjectileLifetimeTicks * ProjectileSpeedPerTick = 46.7 m - but a
        // miss that stays near the line of the shot is the one that reaches an
        // unarmed actor, and beyond this the terrain has taken it.
        const int OverShootMillimeters = 20_000;

        // Body half-width to allow around an unarmed actor's origin. The
        // released Target Truck is a vehicle, not a point.
        const int CollateralBodyMillimeters = 3_000;

        SimVector3 position = target.Pose.PositionMillimeters;
        double startX = state.PlayerPosition.X;
        double startZ = state.PlayerPosition.Z;
        double deltaX = position.X - startX;
        double deltaZ = position.Z - startZ;
        double lengthSquared = (deltaX * deltaX) + (deltaZ * deltaZ);
        if (lengthSquared < 1.0)
        {
            return false;
        }

        double length = Math.Sqrt(lengthSquared);
        double maximumProjection = 1.0 + (OverShootMillimeters / length);
        double corridor =
            CollateralBodyMillimeters + (length * FireTolerance(length));

        foreach (Level100ActorSnapshot actor in state.Level100Actors.Actors)
        {
            if (actor.ActorId == target.ActorId ||
                actor.TargetGroup == Level100MissionTargetGroup.None ||
                actor.Lifecycle != Level100ActorLifecycle.Alive ||
                actor.IsObjective)
            {
                continue;
            }

            double toX = actor.Pose.PositionMillimeters.X - startX;
            double toZ = actor.Pose.PositionMillimeters.Z - startZ;
            double projection = ((toX * deltaX) + (toZ * deltaZ)) / lengthSquared;
            if (projection <= 0 || projection > maximumProjection)
            {
                continue;
            }

            double offsetX = toX - (projection * deltaX);
            double offsetZ = toZ - (projection * deltaZ);
            if ((offsetX * offsetX) + (offsetZ * offsetZ) < corridor * corridor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The trigger tolerance when a miss would land on something the script has
    /// not armed. This is the angle subtended by half a metre at the target -
    /// well inside the hull - rather than the angle subtended by a metre, which
    /// is what <see cref="FireTolerance"/> allows and which is what put two
    /// rounds through <c>Target Truck #25</c>.
    /// </summary>
    private static double PrecisionTolerance(double range) =>
        Math.Clamp(500.0 / Math.Max(range, 1.0), 0.004, 0.030);

    private static double Horizontal(WorldSnapshot state, SimVector3 position)
    {
        double deltaX = (double)position.X - state.PlayerPosition.X;
        double deltaZ = (double)position.Z - state.PlayerPosition.Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
    }

    private static double YawErrorTo(WorldSnapshot state, int x, int z)
    {
        double deltaX = (double)x - state.PlayerPosition.X;
        double deltaZ = (double)z - state.PlayerPosition.Z;
        return NormalizeRadians(
            Math.Atan2(-deltaX, deltaZ) - (state.FacingYawMicroRad / 1_000_000d));
    }

    private static double PitchErrorTo(WorldSnapshot state, SimVector3 aim, double horizontal) =>
        -Math.Atan2(
            aim.Y - state.PlayerElevationMillimeters,
            Math.Max(1.0, horizontal)) -
        (state.FacingPitchMicroRad / 1_000_000d);

    /// <summary>
    /// The stick position that turns the airframe at <c>error * gain</c> of its
    /// full rate.
    ///
    /// <para><b>The gain is a response gain, not a raw stick gain, and the
    /// distinction became load-bearing when Core stopped being linear.</b> The
    /// jet and pitch call sites choose it by asking how strongly the nose should
    /// accelerate for a given aim error - 2,000 response permille per radian in
    /// yaw, 4,000 in pitch, both tuned against measured convergence. The ground
    /// yaw path uses <see cref="WalkerLookAxis"/> to state the same pre-existing
    /// controller in plant units, so the shipped full-scale correction does not
    /// silently retune the harness. Retail curves the axis
    /// (<c>references/Onslaught/Player.cpp:334-355</c>, ported to
    /// <see cref="LookAxisResponse"/>) and the curve is compressive, so handing
    /// that number straight to <see cref="SimInput"/> asks for a rate and gets
    /// 0.4665 of it near centre. <see cref="LookAxisCommand"/> converts the
    /// rate into the deflection that produces it, which is what a player's hand
    /// does; Core still applies the curve to the result.</para>
    ///
    /// <para>Measured without it, on the run this file exists to produce: the
    /// beat-3 precision shot at <c>Target Tank #23</c> stopped converging, a
    /// Pulse round destroyed the still-unactivated <c>Target Truck #25</c>
    /// 1.46 m behind it at t4240 - thirty ticks before the tank died rather
    /// than thirty-six after, which is the whole difference - and the run ended
    /// at t4281 when <c>event("Activate Static Targets 2")</c> ran
    /// <c>SetObjective()</c> on a destroyed actor.</para>
    /// </summary>
    private static short LookAxis(double error, double gain) =>
        LookAxisCommand.ForResponsePermille((int)(error * gain));

    /// <summary>
    /// Preserves the ground driver's pre-existing tuned yaw-velocity-increment gain after
    /// Core adopted the shipped Aquila yaw authority. The old 2,000 response
    /// permille/radian controller acted through a 22,667 µrad/tick full-scale
    /// increment, so its physical gain was 45,334 µrad/tick² per radian. State
    /// this in plant units and divide by the current evidenced full scale: the
    /// driver still uses ordinary look input, while a correction to the plant
    /// constant no longer silently retunes the test harness.
    /// </summary>
    private static short WalkerLookAxis(double error)
    {
        const double angularAccelerationGain = 45_334d;
        int responsePermille = (int)(
            error * angularAccelerationGain * 1_000d /
            SimulationConstants.WalkerYawInputMicroRadPerTick);
        return LookAxisCommand.ForResponsePermille(responsePermille);
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
