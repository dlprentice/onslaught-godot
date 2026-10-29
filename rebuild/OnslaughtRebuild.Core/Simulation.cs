// SPDX-License-Identifier: GPL-3.0-only

using System.Numerics;

namespace OnslaughtRebuild.Core;

public sealed partial class Simulation
{
    internal readonly record struct TerrainGroundImpactResponse(
        int SpeedMillimetersPerTick,
        bool DamageThresholdCrossed,
        int DamageMilliLife,
        SimVector3 Velocity);

    private sealed class MutableProjectile
    {
        // Rounds are deterministic ammunition state, not script actors. They
        // enter the actor runtime only as the released external Ammunition
        // type mask carried by a hit fact.
        public required int Id { get; init; }
        public required Level100ProjectileKind Kind { get; init; }
        public SimVector2 Position { get; set; }
        public required SimVector2 Velocity { get; set; }
        public int ElevationMillimeters { get; set; }
        public required int VerticalVelocityMillimetersPerTick { get; set; }
        public int RemainingTicks { get; set; }
        // A seeking round's heading, launch time (+0xf4) and bound target.
        public bool Seeks => Kind == Level100ProjectileKind.MicroMissile;
        public int YawMicroRad { get; set; }
        public int PitchMicroRad { get; set; }
        public uint LaunchTimeBits { get; init; }
        public Level100ActorId? SeekTarget { get; set; }
        // Set by the life event. Every player round's life ends in the ring,
        // before that frame's MOVE, so the flag never outlives the flush.
        public bool Dying { get; set; }
        // Immutable Kind is already in the snapshot/hash. It owns these
        // configured parameters for both launch and sweep, so no separate
        // mutable or unhashed radius/damage copy can drift from the round.
        public int ContactRadiusMillimeters => Kind switch
        {
            Level100ProjectileKind.MechPulseBoltLarge =>
                SimulationConstants.LargePulseContactRadiusMillimeters,
            Level100ProjectileKind.MechPulseBoltMedium or
                Level100ProjectileKind.MechBullet or Level100ProjectileKind.MechAirBullet =>
                Level100ContactMechanics.PulseRadiusMillimeters,
            // Micro Missile carries no CRoundRadius (record default 0).
            Level100ProjectileKind.MicroMissile => 0,
            _ => throw new InvalidDataException($"Unsupported player round {Kind}."),
        };
        public uint DamageBits => Kind switch
        {
            Level100ProjectileKind.MechPulseBoltMedium => Level100DestructionState.PulseDamageBits,
            Level100ProjectileKind.MechPulseBoltLarge => Level100DestructionState.LargePulseDirectDamageBits,
            Level100ProjectileKind.MechBullet => Level100DestructionState.MechBulletDamageBits,
            Level100ProjectileKind.MechAirBullet => SimulationConstants.MechAirBulletDamageBits,
            Level100ProjectileKind.MicroMissile => Level100MissilePod.DamageBits,
            _ => throw new InvalidDataException($"Unsupported player round {Kind}."),
        };
    }

    private sealed class MutableWalkerFoot
    {
        public required int Id { get; init; }
        public required SimVector2 StanceOffset { get; init; }
        public SimVector2 Position { get; set; }
        public int GroundElevationMillimeters { get; set; }
        public int PhaseThirds { get; set; }
        public int LiftMillimeters { get; set; }
    }

    private readonly uint _seed;
    private readonly List<MutableProjectile> _projectiles = [];
    private readonly List<MutableWalkerFoot> _walkerFeet = [];
    private readonly List<Level100MissionEvent> _level100MissionEvents = [];
    private readonly List<Level100ActorScriptCommand> _level100ActorScriptCommands = [];
    private readonly Level100TutorialProgress _level100TutorialProgress;
    private readonly Level100ActorDefinitionSet _level100ActorDefinitions;
    private readonly int _worldNumber;
    private Level100Mission _level100Mission = null!;
    private Level100ActorRegistry _level100Actors = null!;
    private Level100ActorScriptRuntime _level100ActorScripts = null!;
    private Level100ActorMechanics _level100ActorMechanics = null!;
    private Level100DestructionRuntime _level100Destruction = null!;
    private Level100ActorId _level100PlayerActorId;
    private int _tick;
    private uint _retailEventFrameCount;
    private int _nextProjectileId;
    private VehicleMode _mode;
    private VehicleTransition _transition;
    private int _playerGroundElevationMillimeters;
    private int _playerGroundDeltaMillimeters;
    private bool _playerOnGround;
    private bool _playerInWater;
    private bool _playerWaterFailure;
    private bool _playerOnSteepSlope;
    private bool _landingJetsActive;
    private int _groundImpactSpeedMillimetersPerTick;
    private readonly List<AquilaFlightEvent> _flightEvents = [];
    private readonly List<Level100WeaponFireEvent> _weaponFireEvents = [];
    private readonly List<Level100PlayerDamageEvent> _playerDamageEvents = [];
    private readonly List<Level100DamageFlashSnapshot> _damageFlashes = [];
    private sbyte _facingX;
    private sbyte _facingZ;
    // Continuous body yaw (0 = +Z) and its retail-observed inertial step.
    private int _facingYawMicroRad;
    private int _walkerYawVelocityMicroRadPerTick;
    private int _facingPitchMicroRad;
    private int _walkerPitchVelocityMicroRadPerTick;
    private int _bodyRollMicroRad;
    private int _rollVelocityMicroRadPerTick;
    private int _walkerLastMoveXPermille;
    private int _walkerLastMoveZPermille;
    private int _walkerLastHardLeftTimeBits;
    private int _walkerLastHardRightTimeBits;
    private int _walkerLastHardForwardTimeBits;
    private int _walkerLastHardBackwardTimeBits;
    private int _walkerDashTicksRemaining;
    private int _walkerSoundTravelMillimeters;
    private int _walkerSoundRolloverCount;
    private int _zoomPermille;
    private int _desiredZoomPermille;
    private int _energy;
    private int _shield;
    private int _augmentCharge;
    private bool _augmentActive;
    private int _transformTicksRemaining;
    private int _fireCooldownTicksRemaining;
    private int _twinVulcanReloadTicksRemaining;
    private int _level100OpeningTicksRemaining;
    private bool _level100FlightEnabled;
    private int _flightModeFlag = RetailEnableFlightMode.FlagDisabled;
    private readonly Level100PlayerWeaponRuntime _level100PlayerWeapons = new();
    private int _level100HudEmphasisMask;
    private bool _walkerToJetUsesTakeoffLift;
    private bool _walkerToJetLiftApplied;
    private int _ticksSinceGroundContact;
    private int _jetTicksSinceTransform;
    private int _jetStrafeTicksRemaining;
    private int _jetStrafeAccelerationRemainder;
    private int _jetEnergyDrainRemainderMicro;
    private int _jetThrusterPermille;
    private int _jetGroundedSlowTicks;
    private int _jetStallTicks;
    private int _jetEnergyDrainThisTick;
    private bool _jetMovedThisTick;

    /// <param name="lostBaseRows">
    /// The base-world rows the career marks lost (<c>CCareer::DoesBaseThingExist</c>
    /// false for this world), which the load skips: World 110's carry-over from
    /// Level 100. Empty for a full base world.
    /// </param>
    public Simulation(
        uint seed,
        Level100ActorDefinitionSet level100ActorDefinitions,
        Level100TutorialProgress tutorialProgress = default,
        int worldNumber = Level100MissionProgram.WorldNumber100,
        IReadOnlyCollection<int>? lostBaseRows = null)
    {
        if (seed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), "Seed must be nonzero.");
        }

        _seed = seed;
        _level100ActorDefinitions = level100ActorDefinitions ??
            throw new ArgumentNullException(nameof(level100ActorDefinitions));
        if (worldNumber != Level100MissionProgram.WorldNumber100 &&
            worldNumber != Level100MissionProgram.WorldNumber110)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldNumber),
                $"World {worldNumber} has no admitted mission session.");
        }

        if (_level100ActorDefinitions.WorldNumber != worldNumber)
        {
            throw new ArgumentException(
                $"The actor definitions carry world {_level100ActorDefinitions.WorldNumber}, not the requested world {worldNumber}.",
                nameof(level100ActorDefinitions));
        }

        _worldNumber = worldNumber;
        _level100TutorialProgress = tutorialProgress;
        _lostBaseRows = Array.AsReadOnly((lostBaseRows ?? []).ToArray());
        ResetDynamicState();
    }

    private IReadOnlyList<int> _lostBaseRows = [];

    /// <summary>The career world whose mission program this session executes.</summary>
    public int WorldNumber => _worldNumber;

    public WorldSnapshot Snapshot => CreateSnapshot();


    /// <summary>
    /// Measurement seam. Applies exactly the two <em>capability grants</em> the
    /// released LevelScript performs at the head of beat 6 —
    /// <c>player.EnableFlightMode()</c> (including
    /// (a store of constant 1 to field +0x58c of ecx)) and, for one Target Zone,
    /// <c>target.SetObjective(); target.Activate();</c> — so that the three
    /// "fly there and land" legs can be measured while beats 3–5 are still
    /// mechanically blocked by causes outside this lane (Target Truck and
    /// Target Drone have no contact geometry yet).
    /// </summary>
    /// <remarks>
    /// This posts <b>no</b> named mission event and advances no beat. The
    /// "Reached Target Zone N" event still has to be produced by the world:
    /// the player must actually fly into the volume and be out of jet mode
    /// there, exactly as <c>TargetZoneN.msl</c> <c>hit()</c> requires. Nothing
    /// in the shipped game path calls this; it exists only so a test can pay
    /// for the parts of the tutorial that are blocked elsewhere.
    /// </remarks>
    internal void GrantFlightLegForMeasurement(Level100MissionTrigger trigger)
    {
        _level100FlightEnabled = true;
        // Isolated Level100FlightEnabled names the rebuild bool.
        // Isolated Enable names literal-1; one live store of 1 is
        // not unique versus increment from 0. This is the same
        // Enable LevelScript posts at beat 6. Disable's clear /
        // morph stay unclaimed. ChargeWeapon stays unclaimed.
        _flightModeFlag = RetailEnableFlightMode.Enable(_flightModeFlag);
        Level100ActorSnapshot zone = _level100Actors.Snapshot.Actors.Single(
            actor => actor.Trigger == trigger);
        _level100Actors.SetObjective(zone.ActorId, true);
        _level100Actors.Activate(zone.ActorId);
    }

    internal uint Level100PulseCannonChargeBits =>
        _level100PlayerWeapons.PulseCannonChargeBits;

    /// <summary>
    /// Isolated <see cref="WorldSnapshot.Level100FlightEnabled"/>
    /// names the rebuild bool. This is official
    /// (a store of constant 1 to field +0x58c of ecx). Disable's clear stays
    /// unclaimed.
    /// </summary>
    internal int Level100FlightModeFlag => _flightModeFlag;

    /// <summary>
    /// The level's stored event-manager clock at <c>0x00672FD0</c>, using
    /// <c>CEventManager::AdvanceTime</c> (0x0044B600) through its shared Core
    /// calculation. InitRestartLoop resets this clock; the replay tick does
    /// not reset. This does not claim full controller/event dispatch parity.
    /// </summary>
    internal float EngineTimeSeconds =>
        RetailEventScheduler.TimeAtFrameCount(_retailEventFrameCount);


    /// <summary>
    /// Causal-probe seam for one terrain touchdown. It places the released
    /// Level 100 player in an airborne walker pose; the next normal
    /// <see cref="Step"/> still owns gravity, terrain sampling, contact,
    /// damage, velocity response, events, and actor-state synchronization.
    /// </summary>
    /// <remarks>
    /// No shipped path calls this. Tests use it only to make the production
    /// <c>CommitFlightMovement</c> contact edge repeatable without first
    /// navigating the tutorial's flight gate.
    /// </remarks>
    internal void SetAirborneWalkerContactStateForMeasurement(
        SimVector3 position,
        SimVector3 velocity,
        int dashTicksRemaining = 0)
    {
        if (dashTicksRemaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dashTicksRemaining));
        }

        _mode = VehicleMode.Walker;
        _transition = VehicleTransition.None;
        _transformTicksRemaining = 0;
        _playerGroundElevationMillimeters =
            Level100Terrain.Instance.SampleGroundElevationMillimeters(
                new SimVector2(position.X, position.Z));
        _playerGroundDeltaMillimeters = 0;
        _playerOnGround = false;
        _playerInWater = false;
        _ticksSinceGroundContact = 1;
        _walkerDashTicksRemaining = dashTicksRemaining;
        CommitLevel100PlayerPose(
            new SimVector2(position.X, position.Z),
            position.Y,
            new SimVector2(velocity.X, velocity.Z),
            velocity.Y,
            transitionPose: false);
    }

    /// <summary>
    /// Causal-probe seam for supported released player round kinds. It queues one
    /// round across a caller-supplied contact segment, with a two-frame life;
    /// the next normal <see cref="Step"/> still owns movement, contact
    /// selection, impact-kind routing, event production and removal.
    /// </summary>
    /// <remarks>
    /// No shipped path calls this. The seam exists so the production
    /// <c>MovePlayerRound</c> kind switch can be falsified without replacing
    /// it with a manually injected destruction effect.
    /// </remarks>
    internal void QueueRoundForContactMeasurement(
        Level100ProjectileKind kind,
        SimVector3 start,
        SimVector3 end)
    {
        if (kind is not (Level100ProjectileKind.MechPulseBoltMedium or
            Level100ProjectileKind.MechPulseBoltLarge or
            Level100ProjectileKind.MechBullet or Level100ProjectileKind.MechAirBullet))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "The contact probe accepts only supported released player rounds.");
        }
        if (start == end)
        {
            throw new ArgumentException(
                "The contact probe requires a non-empty sweep.",
                nameof(end));
        }

        int id = _nextProjectileId++;
        _projectiles.Add(new MutableProjectile
        {
            Id = id,
            Kind = kind,
            Position = new SimVector2(start.X, start.Z),
            Velocity = new SimVector2(
                checked(end.X - start.X),
                checked(end.Z - start.Z)),
            ElevationMillimeters = start.Y,
            VerticalVelocityMillimetersPerTick = checked(end.Y - start.Y),
            RemainingTicks = 2,
        });
        FilePlayerRoundEvents(id, 2);
    }

    /// <summary>
    /// Causal-probe seam for one released actor-round impact on the player.
    /// It bypasses only the round's flight to contact; the next normal
    /// <see cref="Step"/> owns the production damage and flash path.
    /// </summary>
    internal void QueueActorRoundImpactForMeasurement(
        Level100ActorId ownerActorId,
        Level100ActorRoundKind kind,
        SimVector3 sourcePositionMillimeters)
    {
        _ = _level100Actors.GetActor(ownerActorId);
        Level100ActorRoundData round = Level100ActorArmament.Round(kind);
        _level100ActorMechanics.QueueActorRoundImpactForMeasurement(
            new Level100ActorRoundImpact(
                _level100PlayerActorId,
                ownerActorId,
                kind,
                sourcePositionMillimeters,
                round.IncomingDamageMilliLife));
    }

    public WorldSnapshot Step(
        SimInput input,
        IReadOnlyList<Level100SimulationFact>? level100Facts = null)
    {
        input.Validate();
        _level100Destruction.ValidateExternalFacts(level100Facts);
        ValidateLevel100PlayerDamageFacts(level100Facts);

        _tick++;
        _flightEvents.Clear();
        _weaponFireEvents.Clear();
        _playerDamageEvents.Clear();
        _actorRoundDamageIdsForMeasurement.Clear();

        if (input.HasAction(SimActions.Reset))
        {
            ResetDynamicState();
            return CreateSnapshot();
        }

        AdvanceFrame(input, level100Facts);
        return CreateSnapshot();
    }

    /// <summary>
    /// One <c>CGame::Update</c>: the event clock, the controller, the flush and
    /// the Moves. <see cref="Step"/> runs one per tick, and level construction
    /// runs the pre-run's frames through it before the first tick.
    /// </summary>
    private void AdvanceFrame(
        SimInput input,
        IReadOnlyList<Level100SimulationFact>? level100Facts)
    {
        // CGame::Update (0x0046EB37-0x0046EBCE) advances the manager before
        // controller Flush and event Flush, unless already paused. Therefore
        // the update that delivers PAUSE_GAME advances the clock; subsequent
        // paused UI updates do not. The existing nominal terminal-event timing
        // remains a separate limitation, as does full event priority below.
        bool eventManagerAdvanced = !_level100Mission.GameplayPaused;
        if (eventManagerAdvanced)
        {
            _retailEventFrameCount = unchecked(_retailEventFrameCount + 1);
            _level100ActorMechanics.AdvanceEventClock(_retailEventFrameCount);
        }

        _level100MissionEvents.Clear();
        _level100ActorScriptCommands.Clear();
        _level100Destruction.BeginTick();

        // game.cpp:1922-1933 and pristine 0x0046EB5D/0x0046EB98/0x0046EBCE:
        // AdvanceTime -> controller Flush -> event Flush. These button calls
        // are synchronous, so they consume power, panning, weapon flags and
        // the retained emitter before any actor/mission callback or Move.
        // The shipped initializer at 0x005142C4-0x00514350 orders rows 9-14 as
        // Morph, Charge, Fire, ChangeWeapon, ZoomIn, ZoomOut. A callback cannot
        // retroactively authorize a button that was already refused.
        bool transitionWasActive = _transition != VehicleTransition.None;
        SimInput controllerInput = !_level100Mission.GameplayPaused &&
            _level100Mission.Outcome == Level100MissionOutcome.Running &&
            Level100PlayerActive && _level100OpeningTicksRemaining == 0
            ? input
            : SimInput.Idle;
        TryToggleMode(controllerInput);
        TryChargeWeapon(controllerInput);
        TryFire(controllerInput);
        TryChangeWeapon(controllerInput);
        TryChangeZoom(controllerInput);

        // START_OF_FRAME shutdown follows synchronous controller Flush and
        // precedes actor/mission callbacks and Move. Paused UI updates do
        // not advance or drain this ring lane.
        if (eventManagerAdvanced)
            _level100Destruction.FlushStartOfFrame(_retailEventFrameCount);

        // Read the pan state BEFORE AdvanceOpeningCamera consumes it. On the
        // tick the player skips, that call sets _level100OpeningTicksRemaining
        // to 0, so the gate below cannot see the pan it is supposed to be
        // gating on. See AdvanceOpeningCamera's remarks for the released
        // dispatch order this reproduces.
        bool skippedThePanThisTick = _level100OpeningTicksRemaining > 0 &&
            input.HasAction(SimActions.SkipPanning);

        // Scripted loss pauses immediately. Player-death/water loss keeps the
        // world live through the nominal 15-second terminal clock, but the
        // update that consumes its final tick is already the PAUSE_GAME update
        // and must not advance actor scripts or world mechanics.
        bool gameplayPausesThisTick = _level100Mission.GameplayPausesOnNextTick;
        if (!gameplayPausesThisTick)
        {
            AdvanceOpeningCamera(input);
            SyncLevel100PlayerState();
            _level100ActorScripts.AdvanceTick();
        }
        PumpLevel100EventBus();
        _level100Mission.AdvanceTick(PlayerHull);
        PumpLevel100EventBus();
        PostGamePlaying();
        ApplyLevel100Facts(level100Facts);
        if (_level100Mission.GameplayPaused)
        {
            return;
        }

        ProcessLevel100DamageFlashes();
        AdvanceLevel100ActorMechanics();
        bool playerPartMoveStarted =
            _level100Actors.GetLifecycle(_level100PlayerActorId) ==
                Level100ActorLifecycle.Alive;

        // Movement retains its later projection. Power and panning are distinct
        // gates: LevelScript's returning-career branch never deactivates the
        // player, so only panning suppresses its early input. The cold career
        // remains inactive until the introduction callback. Skip suppression is
        // covered separately by Level100SkipPanningTests; full axis/controller
        // and Move dispatch parity is outside this direct-button correction.
        SimInput playerInput = _level100Mission.Outcome == Level100MissionOutcome.Running &&
            Level100PlayerActive &&
            _level100OpeningTicksRemaining == 0 &&
            !skippedThePanThisTick
            ? input
            : SimInput.Idle;

        // BECOME_JET/BECOME_WALKER complete in the later event phase. An existing
        // transition still blocked the controller above on its final tick; a
        // transition started by that controller retains its full first tick.
        if (transitionWasActive)
        {
            AdvanceTransition();
        }
        UpdateAugmentState();

        // These counters only project nominal reload duration; stored float
        // ready times own eligibility. Do not shorten a value stamped by a
        // successful volley in this update's earlier controller phase.
        if (_fireCooldownTicksRemaining > 0 && !_weaponFireEvents.Any(
            item => item.Weapon != Level100PlayerWeapon.MechTwinVulcanCannon))
        {
            _fireCooldownTicksRemaining--;
        }

        if (_twinVulcanReloadTicksRemaining > 0 && !_weaponFireEvents.Any(
            item => item.Weapon == Level100PlayerWeapon.MechTwinVulcanCannon))
        {
            _twinVulcanReloadTicksRemaining--;
        }

        UpdateZoom();
        HandleLocks();
        UpdateMovement(playerInput);
        AdvanceBattleEngineRotationTail();
        UpdateWalkerHydraulicCue();
        UpdateWalkerFeet();
        // hit() InJetMode reads the actor-script flight state.
        // Isolated JetModeState names the pre-filter; this sync
        // is the same Evaluate the compiled TargetZone hit()
        // calls, on this tick's movement, not last tick's.
        SyncLevel100PlayerState();
        UpdateLevel100TriggerActors();
        UpdateResources(playerPartMoveStarted);
        SyncLevel100PlayerState();
    }

    /// <summary>
    /// The opening pan, and the released law that lets the player end it early.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CPlayer::ReceiveButtonAction</c>
    /// (<c>references/Onslaught/Player.cpp:311-315</c>, retail
    /// <c>0x004D3110</c> lines 39-47) handles the skip action ABOVE the
    /// movement gate:
    /// </para>
    /// <code>
    ///   if (button == BUTTON_SKIP_PANNING &amp;&amp; mCurrentViewMode == PLAYER_PAN_VIEW
    ///       &amp;&amp; GAME.GetGameState() == GAME_STATE_PANNING)
    ///   {
    ///       GotoControlView() ;
    ///       GAME.StartPlayingState() ;
    ///   }
    ///   // if game is pre running or panning don't allow control of movement
    ///   if (GAME.GetGameState() &lt; GAME_STATE_PLAYING) return ;   // Player.cpp:319
    /// </code>
    /// <para>
    /// That placement is the whole mechanism, and it encodes three separate
    /// facts:
    /// </para>
    /// <list type="number">
    ///   <item>the skip is accepted <em>only</em> while panning — the retail
    ///   condition is <c>0x008a9ac0 == 2</c>, <c>GAME_STATE_PANNING</c>
    ///   (<c>references/Onslaught/game.h:46</c>);</item>
    ///   <item>no movement or weapon input does anything during the pan,
    ///   because every other button falls through to the <c>:319</c>
    ///   gate;</item>
    ///   <item>after <c>StartPlayingState</c> the skip does nothing —
    ///   <c>CGame::StartPlayingState</c> itself early-returns when the state is
    ///   already <c>GAME_STATE_PLAYING</c> (<c>game.cpp:3027</c>).</item>
    /// </list>
    /// <para>
    /// <b>The skip tick is still a panning tick for every other input.</b>
    /// Retail's <c>CController::DoMappings</c> walks the shipped table in
    /// order, and every player-action row (indices 0-15, the
    /// <c>active=1</c> block) comes BEFORE the four
    /// <c>BUTTON_SKIP_PANNING</c> rows (22-25). So on the frame the pan is
    /// skipped, movement, morph, fire and the rest have already been dispatched
    /// and rejected by the <c>:319</c> gate; they take effect from the next
    /// frame.
    /// </para>
    /// <para>
    /// <b>CORRECTED 2026-07-27.</b> This paragraph used to end "Core reproduces
    /// that by leaving this tick's <c>playerInput</c> gated in
    /// <see cref="Step"/>", and it also claimed the law test asserted all three
    /// facts above. Both were false, and an adversarial review caught it by
    /// deleting the <c>_level100OpeningTicksRemaining == 0</c> clause outright
    /// and watching the whole suite stay green. This method zeroes
    /// <c>_level100OpeningTicksRemaining</c>, and the gate then read that same
    /// field and found it already zero — so on the skip tick the pan clause
    /// gated nothing. What was actually suppressing input was
    /// <c>Level100PlayerActive</c>, which is a different released gate that
    /// merely happens to be closed at the same time <em>on a cold career</em>.
    /// On a returning career it is open from tick 0 and the divergence was
    /// live. <see cref="Step"/> now reads the pan state before this call runs.
    /// </para>
    /// <para>
    /// Direct controller calls now precede the event phase
    /// (<c>game.cpp:1922-1933</c>, pristine <c>0x0046EB5D-0x0046EBCE</c>), so
    /// they also see the pan still active on its natural final update. The
    /// existing movement projection remains later and still accepts input on
    /// that natural boundary. Full pan-camera/event and movement dispatch
    /// parity remains open; only the direct button phase is corrected here.
    /// </para>
    /// <para>
    /// <c>GotoControlView()</c> (<c>Player.cpp:72-83</c>) is the camera half
    /// and belongs to the client; Core owns only
    /// <c>GAME.StartPlayingState()</c>.
    /// </para>
    /// </remarks>
    private void AdvanceOpeningCamera(SimInput input)
    {
        bool wasPanning = _level100OpeningTicksRemaining > 0;
        if (!wasPanning)
        {
            return;
        }

        _level100OpeningTicksRemaining--;

        if (input.HasAction(SimActions.SkipPanning))
        {
            _level100OpeningTicksRemaining = 0;
        }

        if (_level100OpeningTicksRemaining == 0)
        {
            // CGame::StartPlayingState (game.cpp:3025-3031). Reached exactly
            // once per run, either from FINISHED_PANNING at the end of the
            // six-second pan (game.cpp:3051-3055) or from the skip above.
            // The mission keeps its own clock - SimActions.Reset gives it a
            // fresh one while _tick keeps running - so it derives the gate
            // itself rather than being handed a simulation tick.
            _level100Mission.NotifyPlayingStateStarted();
            // StartPlayingState also posts "game playing" to every script
            // (game.cpp:3029); World 110's Scout answers it. Retail posts it
            // inside the frame's event flush, so what the scripts start from it
            // (LevelScript's Pause(2.0) after "Enemy Engaged") counts from this
            // frame: it is posted once the mission's clock has reached it.
            _level100GamePlayingPending = true;
        }
    }

    private bool _level100GamePlayingPending;

    private void PostGamePlaying()
    {
        if (!_level100GamePlayingPending)
        {
            return;
        }

        _level100GamePlayingPending = false;
        _level100Mission.QueueExternalEvent("game playing");
        _level100ActorScripts.PublishEvent("game playing");
        PumpLevel100EventBus();
    }

    private Level100ActorPoseSnapshot PlayerPose =>
        _level100Actors.GetPose(_level100PlayerActorId);

    private SimVector2 PlayerPosition
    {
        get
        {
            Level100ActorPoseSnapshot pose = PlayerPose;
            return new SimVector2(
                pose.PositionMillimeters.X,
                pose.PositionMillimeters.Z);
        }
    }

    private SimVector2 PlayerVelocity
    {
        get
        {
            Level100ActorPoseSnapshot pose = PlayerPose;
            return new SimVector2(
                pose.LinearVelocityMillimetersPerTick.X,
                pose.LinearVelocityMillimetersPerTick.Z);
        }
    }

    private int PlayerGroundElevationMillimeters => _playerGroundElevationMillimeters;

    private int PlayerGroundDeltaMillimeters => _playerGroundDeltaMillimeters;

    private int PlayerElevationMillimeters => PlayerPose.PositionMillimeters.Y;

    private int PlayerVerticalVelocityMillimetersPerTick =>
        PlayerPose.LinearVelocityMillimetersPerTick.Y;

    private int PlayerHull => _level100Actors.GetHealth(_level100PlayerActorId);

    private bool Level100PlayerActive => _level100Actors.IsActive(_level100PlayerActorId);

    private void CommitLevel100PlayerPose(
        SimVector2 position,
        int elevationMillimeters,
        SimVector2 velocity,
        int verticalVelocityMillimetersPerTick,
        bool transitionPose = true)
    {
        FixedBodyBasis body = GetBodyBasis();
        long upX = ((long)body.ForwardY * body.RightZ) -
            ((long)body.ForwardZ * body.RightY);
        long upY = ((long)body.ForwardZ * body.RightX) -
            ((long)body.ForwardX * body.RightZ);
        long upZ = ((long)body.ForwardX * body.RightY) -
            ((long)body.ForwardY * body.RightX);
        float rightX = (float)body.RightX / FixedTrigScale;
        float rightY = (float)body.RightY / FixedTrigScale;
        float rightZ = (float)body.RightZ / FixedTrigScale;
        float upComponentX =
            (float)DivideRoundNearest(upX, FixedTrigScale) / FixedTrigScale;
        float upComponentY =
            (float)DivideRoundNearest(upY, FixedTrigScale) / FixedTrigScale;
        float upComponentZ =
            (float)DivideRoundNearest(upZ, FixedTrigScale) / FixedTrigScale;
        float forwardX = (float)body.ForwardX / FixedTrigScale;
        float forwardY = (float)body.ForwardY / FixedTrigScale;
        float forwardZ = (float)body.ForwardZ / FixedTrigScale;
        static int Bits(float value) => BitConverter.SingleToInt32Bits(value);

        var basis = new Level100FloatBasis3Bits(
            Bits(rightX), Bits(upComponentX), Bits(forwardX),
            Bits(rightY), Bits(upComponentY), Bits(forwardY),
            Bits(rightZ), Bits(upComponentZ), Bits(forwardZ));
        var pose = new Level100ActorPoseSnapshot(
            new SimVector3(
                position.X,
                elevationMillimeters,
                position.Z),
            basis,
            new SimVector3(
                velocity.X,
                verticalVelocityMillimetersPerTick,
                velocity.Z),
            new SimVector3(
                _walkerPitchVelocityMicroRadPerTick,
                _walkerYawVelocityMicroRadPerTick,
                _rollVelocityMicroRadPerTick));
        if (transitionPose)
        {
            _level100Actors.AdvancePose(_level100PlayerActorId, pose);
        }
        else
        {
            _level100Actors.UpdateCurrentPose(_level100PlayerActorId, pose);
        }
    }

    private void SyncLevel100PlayerState()
    {
        Level100ActorPoseSnapshot pose = PlayerPose;
        CommitLevel100PlayerPose(
            new SimVector2(
                pose.PositionMillimeters.X,
                pose.PositionMillimeters.Z),
            pose.PositionMillimeters.Y,
            new SimVector2(
                pose.LinearVelocityMillimetersPerTick.X,
                pose.LinearVelocityMillimetersPerTick.Z),
            pose.LinearVelocityMillimetersPerTick.Y,
            transitionPose: false);
        _level100ActorScripts.SetPlayerFlightState(
            _mode,
            _transition,
            _ticksSinceGroundContact);
    }

    private void DestroyLevel100PlayerActor()
    {
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) ==
            Level100ActorLifecycle.Destroyed)
        {
            return;
        }

        _level100Destruction.ReportExternalStartedDying(_level100PlayerActorId);
        _level100Destruction.ReportExternalDied(_level100PlayerActorId);
        DrainAndDispatchLevel100ActorFacts();
    }

    private void ApplyLevel100Facts(IReadOnlyList<Level100SimulationFact>? facts)
    {
        if (facts is null)
        {
            return;
        }

        foreach (Level100SimulationFact fact in facts)
        {
            ArgumentNullException.ThrowIfNull(fact);
            switch (fact)
            {
                case Level100ActorHitFact hit:
                    _level100Actors.ReportHit(
                        hit.ActorId,
                        hit.OtherActorId,
                        hit.OtherThingTypeMask);
                    DrainAndDispatchLevel100ActorFacts();
                    break;
                case Level100ActorStartedDyingFact startedDying:
                    _level100Destruction.ReportExternalStartedDying(
                        startedDying.ActorId);
                    DrainAndDispatchLevel100ActorFacts();
                    break;
                case Level100ActorDiedFact died:
                    _level100Destruction.ReportExternalDied(died.ActorId);
                    DrainAndDispatchLevel100ActorFacts();
                    break;
                case Level100ActorPoseFact pose:
                    if (pose.ActorId == _level100PlayerActorId)
                    {
                        throw new InvalidOperationException(
                            "Player pose is owned by the deterministic vehicle simulation.");
                    }
                    _level100Actors.SetPose(pose.ActorId, pose.Pose);
                    break;
                case Level100ActorActivationFact activation:
                    if (activation.Active)
                    {
                        _level100Actors.Activate(activation.ActorId);
                    }
                    else
                    {
                        _level100Actors.Deactivate(activation.ActorId);
                    }
                    break;
                case Level100ActorObjectiveFact objective:
                    _level100Actors.SetObjective(objective.ActorId, objective.IsObjective);
                    break;
                case Level100ActorHealthFact health:
                    _level100Destruction.SetExternalHealth(
                        health.ActorId,
                        health.Health);
                    break;
                case Level100SpawnThingFact spawn:
                    IReadOnlyList<Level100ActorId> spawned = _level100Actors.SpawnThing(
                        spawn.OwnerActorId,
                        spawn.DefinitionName,
                        spawn.SpawnerName,
                        spawn.Count,
                        spawn.ScriptName,
                        BitConverter.SingleToInt32Bits(EngineTimeSeconds));
                    foreach (Level100ActorId actorId in spawned)
                    {
                        RegisterSpawnedLevel100Actor(actorId);
                        _level100ActorScripts.AttachAndInitializeSpawnedActor(
                            actorId,
                            spawn.ScriptName);
                    }
                    break;
                case Level100MissionInputFact missionInput:
                    _level100Mission.SubmitInput(missionInput.Input);
                    break;
                case Level100PlayerDamageFact damage:
                    if (damage.IncomingDamageMilliLife <= 0)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(facts),
                            "Player damage must be positive.");
                    }

                    if (ApplyLevel100PlayerDamage(
                        damage.IncomingDamageMilliLife,
                        damageShields: true,
                        Level100PlayerDamageSource.ExternalFact,
                        out bool factRequestsDeath))
                    {
                        _level100Mission.ReportPlayerHitDuringEvasion();
                        CompleteLevel100PlayerDamageDeath(factRequestsDeath);
                    }
                    break;
                case Level100PlayerDeathFact:
                    DestroyLevel100PlayerActor();
                    _level100Mission.ReportPlayerDeath();
                    break;
                case Level100WaterLossFact:
                    _level100Mission.ReportWaterLoss();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(facts),
                        $"Unknown Level 100 fact type {fact.GetType().Name}.");
            }

            PumpLevel100EventBus();
        }
    }

    private static void ValidateLevel100PlayerDamageFacts(
        IReadOnlyList<Level100SimulationFact>? facts)
    {
        if (facts is null)
        {
            return;
        }

        foreach (Level100SimulationFact fact in facts)
        {
            if (fact is not Level100PlayerDamageFact damage)
            {
                continue;
            }
            if (damage.IncomingDamageMilliLife <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(facts),
                    "Player damage must be positive.");
            }
            // The public fact is a shielded Damage call. Constrain it to the
            // exact whole-milli subset before the tick mutates; internal actor
            // rounds already use 200/2500, and water bypasses shields.
            if (damage.IncomingDamageMilliLife <=
                    SimulationConstants.MaximumShield &&
                damage.IncomingDamageMilliLife % 50 != 0)
            {
                throw new InvalidOperationException(
                    "A player-damage fact that can reach the sufficient-shield " +
                    "branch must be divisible by 50 until fractional retail " +
                    "state is represented canonically.");
            }
        }
    }

    private void ApplyWeaponAvailability(Level100WeaponAvailabilityChanged weapon)
    {
        _level100PlayerWeapons.SetActive(weapon.Weapon, weapon.Enabled);
    }

    private void ApplyHudEmphasis(Level100HudEmphasisChanged emphasis)
    {
        if (emphasis.PartId is < 0 or > 30)
        {
            throw new InvalidOperationException(
                $"Released Level 100 requested unsupported HUD part {emphasis.PartId}.");
        }

        int bit = 1 << emphasis.PartId;
        _level100HudEmphasisMask = emphasis.Emphasized
            ? _level100HudEmphasisMask | bit
            : _level100HudEmphasisMask & ~bit;
    }

    private void ApplyLevel100ActorCommand(Level100ActorCommandRequested command)
    {
        switch (command.Command)
        {
            case Level100ActorCommand.Activate:
                _level100Actors.Activate(command.ActorId);
                break;
            case Level100ActorCommand.Deactivate:
                _level100Actors.Deactivate(command.ActorId);
                break;
            case Level100ActorCommand.SetObjective:
                _level100Actors.SetObjective(command.ActorId, true);
                break;
            case Level100ActorCommand.UnsetObjective:
                _level100Actors.SetObjective(command.ActorId, false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private void ApplyLevel100SpawnRequest(Level100SpawnThingRequested spawn)
    {
        IReadOnlyList<Level100ActorId> spawned = _level100Actors.SpawnThing(
            spawn.OwnerActorId,
            spawn.DefinitionName,
            spawn.SpawnerName,
            spawn.Count,
            spawn.ScriptName,
            BitConverter.SingleToInt32Bits(EngineTimeSeconds));
        foreach (Level100ActorId actorId in spawned)
        {
            RegisterSpawnedLevel100Actor(actorId);
            _level100ActorScripts.AttachAndInitializeSpawnedActor(
                actorId,
                spawn.ScriptName);
        }
    }

    private void DrainAndDispatchLevel100ActorFacts()
    {
        foreach (Level100ActorFactSnapshot fact in _level100Actors.DrainFacts())
        {
            _level100ActorScripts.DispatchFact(fact);
            PumpLevel100EventBus();
        }
    }

    private void RegisterSpawnedLevel100Actor(Level100ActorId actorId)
    {
        _level100ActorMechanics.RegisterSpawnedActor(actorId);
        _level100Destruction.RegisterActor(actorId);
    }

    private void PumpLevel100EventBus()
    {
        for (int pass = 0; pass < 1_000; pass++)
        {
            IReadOnlyList<Level100MissionEvent> missionEvents = _level100Mission.DrainEvents();
            foreach (Level100MissionEvent missionEvent in missionEvents)
            {
                _level100MissionEvents.Add(missionEvent);
                ApplyLevel100MissionEvent(missionEvent);
            }

            IReadOnlyList<Level100ActorScriptEventPosted> actorEvents =
                _level100ActorScripts.DrainPostedEvents();
            IReadOnlyList<Level100ActorScriptCommand> actorCommands =
                _level100ActorScripts.DrainCommands();
            _level100ActorScriptCommands.AddRange(actorCommands);
            _level100ActorMechanics.ConsumeCommands(actorCommands);
            foreach (Level100ActorScriptEventPosted actorEvent in actorEvents)
            {
                if (actorEvent.ActorId is { } actorId)
                {
                    Level100ActorSnapshot actor = _level100Actors.GetActor(actorId);
                    if (actor.Trigger.HasValue &&
                        !actor.TriggerEventDispatched)
                    {
                        _level100Actors.MarkTriggerEventDispatched(actorId);
                    }
                }

                _level100Mission.QueueExternalEvent(actorEvent.EventName);
            }

            if (missionEvents.Count == 0 && actorEvents.Count == 0 && actorCommands.Count == 0)
            {
                return;
            }
        }

        throw new InvalidOperationException("Level 100 script event bus did not settle.");
    }

    private void AdvanceLevel100ActorMechanics()
    {
        IReadOnlyList<Level100ActorMechanicsWaitCompletion> completions =
            _level100ActorMechanics.AdvanceTick(_retailEventFrameCount, actorId =>
            {
                _level100ActorScripts.DispatchReady(actorId);
                // Ready's commands must settle before the next queued callback.
                PumpLevel100EventBus();
            }, actorId =>
            {
                _level100Destruction.StartPlaneDeathAfterSpawnerLoss(actorId);
                DrainAndDispatchLevel100ActorFacts();
            }, HandleBattleEngineEvent,
            new SimVector3(PlayerPosition.X, PlayerElevationMillimeters, PlayerPosition.Z),
            DrainAndDispatchLevel100ActorFacts);
        foreach (Level100ActorMechanicsWaitCompletion completion in completions)
        {
            if (!_level100ActorScripts.CompleteMechanicsWait(
                    completion.ActorId,
                    completion.WaitKind,
                    completion.Argument))
            {
                throw new InvalidOperationException(
                    $"Actor {completion.ActorId} completed an unowned mechanics wait.");
            }
        }

        if (completions.Count > 0)
        {
            PumpLevel100EventBus();
        }

        ApplyLevel100ActorRoundImpacts();
    }

    /// <summary>
    /// Routes released actor-owned rounds that reached their designated target.
    /// The Level 100 scripts only ever issue <c>Attack(player)</c>, so the
    /// player is the only supported target today; a round aimed at anything
    /// else is spent without effect rather than routed through an unevidenced
    /// path.
    /// </summary>
    private void ApplyLevel100ActorRoundImpacts()
    {
        IReadOnlyList<Level100ActorRoundImpactReceipt> receipts =
            _level100ActorMechanics.DrainActorRoundImpactReceipts();
        foreach (Level100ActorRoundImpactReceipt receipt in receipts)
        {
            Level100ActorRoundImpact impact = receipt.Impact;
            if (impact.TargetActorId != _level100PlayerActorId ||
                impact.IncomingDamageMilliLife <= 0)
            {
                continue;
            }

            if (_level100Actors.GetLifecycle(_level100PlayerActorId) ==
                Level100ActorLifecycle.Destroyed)
            {
                continue;
            }

            // Released THING_TYPE_AMMUNITION contact on the player. This is the
            // hit BattleEngine.msl's `hit()` observes during the beat-5 dodge.
            // The script-level hit callback runs before Damage; the explicit
            // evasion report below runs after the Damage boundary succeeds.
            _level100Actors.ReportHit(
                _level100PlayerActorId,
                otherThingTypeMask: Level100ReleasedThingTypeMasks.Ammunition);
            DrainAndDispatchLevel100ActorFacts();
            if (receipt.ContactOnly)
            {
                // A dying round's last step: the script hit only.
                PumpLevel100EventBus();
                continue;
            }

            if (ApplyLevel100PlayerDamage(
                impact.IncomingDamageMilliLife,
                damageShields: true,
                Level100PlayerDamageSource.ActorRound,
                out bool impactRequestsDeath))
            {
                if (receipt.RoundId is { } roundId)
                {
                    _actorRoundDamageIdsForMeasurement.Add(roundId);
                }
                AddLevel100DamageFlash(impact.SourcePositionMillimeters);
                _level100Mission.ReportPlayerHitDuringEvasion();
                CompleteLevel100PlayerDamageDeath(impactRequestsDeath);
            }

            PumpLevel100EventBus();
        }
    }

    private readonly List<int> _actorRoundDamageIdsForMeasurement = [];

    /// <summary>
    /// Exact actor-round identities whose impacts produced player-damage
    /// events on the current tick. This is an internal causal receipt for
    /// tests; it is not snapshot, replay, or state-hash material.
    /// </summary>
    internal IReadOnlyList<int> ActorRoundDamageIdsForMeasurement =>
        Array.AsReadOnly(_actorRoundDamageIdsForMeasurement.ToArray());

    private void AddLevel100DamageFlash(SimVector3 sourcePositionMillimeters)
    {
        if (_damageFlashes.Count >= SimulationConstants.Level100DamageFlashCapacity)
        {
            return;
        }

        Level100ActorSnapshot player = _level100Actors.GetActor(_level100PlayerActorId);
        long deltaX = (long)sourcePositionMillimeters.X -
            player.Pose.PositionMillimeters.X;
        long deltaZ = (long)sourcePositionMillimeters.Z -
            player.Pose.PositionMillimeters.Z;
        int sourceYaw = FixedAtan2(-deltaX, deltaZ);
        _damageFlashes.Add(new Level100DamageFlashSnapshot(
            NormalizeMicroRad(_facingYawMicroRad - sourceYaw),
            _tick));
    }

    private void ProcessLevel100DamageFlashes()
    {
        for (int index = 0; index < _damageFlashes.Count; index++)
        {
            Level100DamageFlashSnapshot flash = _damageFlashes[index];
            if (flash.StartTick + SimulationConstants.Level100DamageFlashLifetimeTicks <
                _tick)
            {
                _damageFlashes.RemoveAt(index);
                break;
            }
        }
    }

    private void ApplyLevel100MissionEvent(Level100MissionEvent missionEvent)
    {
        switch (missionEvent)
        {
            case Level100PlayerActivationChanged activation:
                if (activation.Active)
                {
                    _level100Actors.Activate(_level100PlayerActorId);
                }
                else
                {
                    _level100Actors.Deactivate(_level100PlayerActorId);
                }
                break;
            case Level100FlightModeAvailabilityChanged flight:
                _level100FlightEnabled = flight.Enabled;
                if (flight.Enabled)
                {
                    // Isolated FlightModeEnabled names the rebuild
                    // bool. Isolated Enable names literal-1. Disable's
                    // clear / morph stay unclaimed.
                    _flightModeFlag = RetailEnableFlightMode.Enable(_flightModeFlag);
                }

                if (!flight.Enabled &&
                    _mode == VehicleMode.Jet &&
                    _transition == VehicleTransition.None)
                {
                    BeginJetToWalkerTransition();
                }
                break;
            case Level100WeaponAvailabilityChanged weapon:
                ApplyWeaponAvailability(weapon);
                break;
            case Level100HudEmphasisChanged emphasis:
                ApplyHudEmphasis(emphasis);
                break;
            case Level100ActorCommandRequested actorCommand:
                ApplyLevel100ActorCommand(actorCommand);
                break;
            case Level100SpawnThingRequested spawn:
                ApplyLevel100SpawnRequest(spawn);
                break;
            case Level100MissionEventPosted posted:
                _level100ActorScripts.PublishEvent(posted.EventName);
                break;
        }
    }

    private void EmitFlightEvent(
        AquilaFlightEvents kind,
        AquilaJetWeapon weapon = AquilaJetWeapon.None)
    {
        ushort value = (ushort)kind;
        if (value == 0 || (value & (value - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "A flight event must contain exactly one event kind.");
        }

        _flightEvents.Add(new AquilaFlightEvent(
            _tick,
            kind,
            _mode,
            _transition,
            weapon));
    }

    /// <summary>
    /// Records one player weapon RELEASE. Called once per launch instant,
    /// outside the volley loop, because that is where the released
    /// <c>ProjectileBurst__SpawnFromCurrentPreset</c> (0x005069f0) issues its
    /// single <c>CSoundManager::PlayEffect</c> - see
    /// <see cref="Level100WeaponFireEvent"/> for the disassembly.
    /// </summary>
    private void EmitWeaponFireEvent(Level100PlayerWeapon weapon, int roundCount)
    {
        if (weapon == Level100PlayerWeapon.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weapon),
                "A weapon fire event must name the released weapon that fired.");
        }

        if (roundCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(roundCount),
                "A weapon release launches at least one round.");
        }

        _weaponFireEvents.Add(new Level100WeaponFireEvent(
            _tick,
            weapon,
            roundCount));
    }

    private void TryToggleMode(SimInput input)
    {
        if (!input.HasAction(SimActions.ToggleMode) ||
            _transition != VehicleTransition.None)
        {
            if (input.HasAction(SimActions.ToggleMode))
            {
                EmitFlightEvent(AquilaFlightEvents.TransformRejected);
            }
            return;
        }

        if (_mode == VehicleMode.Walker)
        {
            if (!_level100FlightEnabled ||
                _flightModeFlag != RetailEnableFlightMode.FlagEnabled ||
                _walkerDashTicksRemaining > 0)
            {
                EmitFlightEvent(AquilaFlightEvents.TransformRejected);
                return;
            }

            _level100PlayerWeapons.LoseCurrentWeaponChargesForMorph();
            _desiredZoomPermille = SimulationConstants.ZoomOutPermille;
            if (_energy < SimulationConstants.TransformEnergyThreshold)
            {
                EmitFlightEvent(AquilaFlightEvents.TransformRejected);
                return;
            }

            _walkerToJetUsesTakeoffLift =
                _playerOnGround ||
                _ticksSinceGroundContact < SimulationConstants.RecentGroundContactTicks;
            _walkerToJetLiftApplied = false;
            _transition = VehicleTransition.WalkerToJet;
            _transformTicksRemaining = _walkerToJetUsesTakeoffLift
                ? SimulationConstants.WalkerToJetTransitionTicks
                : SimulationConstants.WalkerToJetAirborneTransitionTicks;
            EmitFlightEvent(AquilaFlightEvents.WalkerToJetStarted);
            return;
        }

        BeginJetToWalkerTransition();
    }

    private void BeginJetToWalkerTransition()
    {
        if (_transition != VehicleTransition.None || _mode != VehicleMode.Jet)
        {
            return;
        }

        _level100PlayerWeapons.LoseCurrentWeaponChargesForMorph();
        _transition = VehicleTransition.JetToWalker;
        _desiredZoomPermille = SimulationConstants.ZoomOutPermille;
        _transformTicksRemaining = SimulationConstants.JetToWalkerTransitionTicks;
        _jetGroundedSlowTicks = 0;
        _jetStallTicks = 0;
        EmitFlightEvent(AquilaFlightEvents.JetToWalkerStarted);
    }

    private void TryChangeZoom(SimInput input)
    {
        // The pristine default physics.dat (175,603 bytes, sha256
        // e1fb3dedbeb29b4b4151da2c8cbbdc940b716b1a2321e1d6a9ba1542c74ada14)
        // gives both selectable Level-100 walker weapons zoom mode 1 at +0x08.
        // Jet weapons omit it, so wheel actions are ignored outside a settled
        // walker state. Charge zoom mode 2 remains deliberately open.
        if (_mode == VehicleMode.Walker && _transition == VehicleTransition.None)
        {
            if (input.HasAction(SimActions.ZoomIn))
            {
                _desiredZoomPermille = SimulationConstants.ZoomInPermille;
            }
            if (input.HasAction(SimActions.ZoomOut))
            {
                _desiredZoomPermille = SimulationConstants.ZoomOutPermille;
            }
        }
    }

    private void UpdateZoom()
    {
        // Desired zoom is set by controller rows 13/14; interpolation remains
        // in the later BattleEngine Move owner (BattleEngine.cpp:1435-1479).
        if (_zoomPermille < _desiredZoomPermille)
        {
            _zoomPermille = Math.Min(
                _zoomPermille + SimulationConstants.ZoomStepPermillePerTick,
                _desiredZoomPermille);
        }
        else if (_zoomPermille > _desiredZoomPermille)
        {
            _zoomPermille = Math.Max(
                _zoomPermille - SimulationConstants.ZoomStepPermillePerTick,
                _desiredZoomPermille);
        }
    }

    private void AdvanceTransition()
    {
        if (_transformTicksRemaining == 0)
        {
            return;
        }

        _transformTicksRemaining--;
        if (_transformTicksRemaining != 0)
        {
            return;
        }

        if (_transition == VehicleTransition.WalkerToJet)
        {
            _mode = VehicleMode.Jet;
            _shield = 0;
            _jetTicksSinceTransform = 0;
            _jetStrafeTicksRemaining = 0;
            _jetGroundedSlowTicks = 0;
            _jetStallTicks = 0;
        }
        else if (_transition == VehicleTransition.JetToWalker)
        {
            _mode = VehicleMode.Walker;
        }

        EmitFlightEvent(AquilaFlightEvents.TransformCompleted);
        _transition = VehicleTransition.None;
    }

    private void UpdateMovement(SimInput input)
    {
        _playerGroundDeltaMillimeters = 0;
        _playerOnSteepSlope = false;
        _landingJetsActive = false;
        _groundImpactSpeedMillimetersPerTick = 0;
        _jetMovedThisTick = false;
        _jetEnergyDrainThisTick = 0;
        if (_transition != VehicleTransition.None)
        {
            UpdateTransitionMovement();
            return;
        }

        if (_mode == VehicleMode.Walker)
        {
            UpdateWalkerYaw(input.LookX, input.LookXAnalogPermille);
            UpdateWalkerPitch(input.LookY, input.LookYAnalogPermille);
            UpdateNonJetRoll();
            ApplyWalkerLandingJets(input);
            if (_playerOnGround)
            {
                UpdateWalkerMovement(input);
            }
            else
            {
                UpdateAirborneWalkerMovement();
            }
            return;
        }

        UpdateJetMovement(input);
    }

    private void UpdateWalkerHydraulicCue()
    {
        if (!_playerOnGround ||
            _mode != VehicleMode.Walker ||
            _transition != VehicleTransition.None)
        {
            _walkerSoundRolloverCount = 0;
            return;
        }

        int speedMillimetersPerTick = Magnitude3D(
            PlayerVelocity.X,
            PlayerVerticalVelocityMillimetersPerTick,
            PlayerVelocity.Z);
        _walkerSoundTravelMillimeters += speedMillimetersPerTick;
        if (_walkerSoundTravelMillimeters >
            SimulationConstants.WalkerSoundTravelRolloverMillimeters)
        {
            _walkerSoundTravelMillimeters -=
                SimulationConstants.WalkerSoundTravelRolloverMillimeters;
            _walkerSoundRolloverCount++;
        }

        if (speedMillimetersPerTick <
            SimulationConstants.WalkerSoundStoppedSpeedMillimetersPerTick)
        {
            if (_walkerSoundRolloverCount >
                SimulationConstants.WalkerSoundMinimumRolloverCount)
            {
                EmitFlightEvent(AquilaFlightEvents.WalkerHydraulicsRequested);
            }
            _walkerSoundRolloverCount = 0;
        }
    }

    private void ApplyWalkerLandingJets(SimInput input)
    {
        if (!input.HasAction(SimActions.LandingJets))
        {
            return;
        }

        _landingJetsActive = true;
        SimVector2 playerVelocity = PlayerVelocity;
        var retainedVelocity = new SimVector2(
            Retain(
                playerVelocity.X,
                SimulationConstants.WalkerLandingJetHorizontalRetentionNumerator,
                SimulationConstants.WalkerLandingJetRetentionDenominator),
            Retain(
                playerVelocity.Z,
                SimulationConstants.WalkerLandingJetHorizontalRetentionNumerator,
                SimulationConstants.WalkerLandingJetRetentionDenominator));
        int verticalVelocity = PlayerVerticalVelocityMillimetersPerTick;
        if (verticalVelocity <
            -SimulationConstants.WalkerLandingJetMinimumDescentPerTick)
        {
            verticalVelocity = Retain(
                verticalVelocity,
                SimulationConstants.WalkerLandingJetVerticalRetentionNumerator,
                SimulationConstants.WalkerLandingJetRetentionDenominator);
        }

        CommitLevel100PlayerPose(
            PlayerPosition,
            PlayerElevationMillimeters,
            retainedVelocity,
            verticalVelocity,
            transitionPose: false);
    }

    private void UpdateTransitionMovement()
    {
        _walkerYawVelocityMicroRadPerTick = RetainAngularVelocity(
            _walkerYawVelocityMicroRadPerTick);
        _facingYawMicroRad = NormalizeMicroRad(
            _facingYawMicroRad + _walkerYawVelocityMicroRadPerTick);
        QuantizeFacingFromYaw();

        _walkerPitchVelocityMicroRadPerTick = RetainAngularVelocity(
            _walkerPitchVelocityMicroRadPerTick);
        if (_transition == VehicleTransition.WalkerToJet &&
            _facingPitchMicroRad > -30_000)
        {
            _walkerPitchVelocityMicroRadPerTick -=
                SimulationConstants.WalkerToJetPitchInputMicroRadPerTick;
        }
        ApplyJetPitchSoftLimit();
        _facingPitchMicroRad = NormalizeMicroRad(
            _facingPitchMicroRad + _walkerPitchVelocityMicroRadPerTick);

        _rollVelocityMicroRadPerTick = RetainAngularVelocity(
            _rollVelocityMicroRadPerTick);
        _bodyRollMicroRad = NormalizeMicroRad(
            _bodyRollMicroRad + _rollVelocityMicroRadPerTick);
        RetainNonJetRollAngle();

        SimVector2 playerVelocity = PlayerVelocity;
        int velocityX = playerVelocity.X;
        int velocityZ = playerVelocity.Z;
        int verticalVelocity = PlayerVerticalVelocityMillimetersPerTick;
        if (_transition == VehicleTransition.WalkerToJet)
        {
            if (_walkerToJetUsesTakeoffLift)
            {
                if (!_walkerToJetLiftApplied)
                {
                    verticalVelocity +=
                        SimulationConstants.WalkerToJetLiftImpulsePerTick;
                    if (_playerOnGround)
                    {
                        velocityX = RetainWalkerVelocity(velocityX);
                        velocityZ = RetainWalkerVelocity(velocityZ);
                        verticalVelocity = RetainWalkerVerticalVelocity(verticalVelocity);
                    }
                    _walkerToJetLiftApplied = true;
                }
            }

            verticalVelocity -= SimulationConstants.WalkerGravityPerTick;
        }
        else
        {
            if (_playerOnGround)
            {
                velocityX = RetainWalkerVelocity(velocityX);
                velocityZ = RetainWalkerVelocity(velocityZ);
            }
            verticalVelocity -= SimulationConstants.MorphIntoWalkerGravityPerTick;
        }

        ApplyLevel100FlightLimits(ref velocityX, ref velocityZ, ref verticalVelocity);
        CommitFlightMovement(velocityX, velocityZ, verticalVelocity);
    }

    private void UpdateAirborneWalkerMovement()
    {
        SimVector2 playerVelocity = PlayerVelocity;
        int velocityX = playerVelocity.X;
        int velocityZ = playerVelocity.Z;
        int verticalVelocity = PlayerVerticalVelocityMillimetersPerTick;
        if (_walkerDashTicksRemaining > 0)
        {
            // CBattleEngineWalkerPart::Move retains the complete velocity while
            // a dash is live even after ground contact is lost. Its speed cap
            // returns only for the final four updates.
            velocityX = RetainWalkerVelocity(velocityX);
            velocityZ = RetainWalkerVelocity(velocityZ);
            verticalVelocity = RetainWalkerVelocity(verticalVelocity);
            if (_walkerDashTicksRemaining <
                SimulationConstants.WalkerDashFrictionThresholdTicks)
            {
                SimVector2 horizontal = ClampMagnitude(
                    new SimVector2(velocityX, velocityZ),
                    SimulationConstants.WalkerMaximumSpeedPerTick);
                velocityX = horizontal.X;
                velocityZ = horizontal.Z;
            }
            _walkerDashTicksRemaining--;
        }
        verticalVelocity -= SimulationConstants.WalkerGravityPerTick;
        ApplyLevel100FlightLimits(ref velocityX, ref velocityZ, ref verticalVelocity);
        CommitFlightMovement(velocityX, velocityZ, verticalVelocity);
    }

    private void UpdateJetMovement(SimInput input)
    {
        _jetMovedThisTick = true;
        if (_energy > 0)
        {
            // CBattleEngineJetPart::Thrust stores 0.5-vy/2 while energy is
            // available. Core's forward input is the opposite sign of vy.
            _jetThrusterPermille = 500 + (input.MoveZ * 500);
        }
        int throttlePermille = _jetThrusterPermille;
        _jetEnergyDrainThisTick = _energy > 0
            ? TakeJetEnergyDrain(throttlePermille)
            : 0;

        SimVector2 playerVelocity = PlayerVelocity;
        int velocityX = playerVelocity.X;
        int velocityY = PlayerVerticalVelocityMillimetersPerTick;
        int velocityZ = playerVelocity.Z;
        FixedBodyBasis basis = GetBodyBasis();

        if (input.MoveX != 0 && _energy > 0)
        {
            int strafeAcceleration = TakeJetStrafeAcceleration(input.MoveX);
            AddDirection(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                basis.RightX,
                basis.RightY,
                basis.RightZ,
                strafeAcceleration);
            _jetStrafeTicksRemaining = SimulationConstants.JetStrafeAlignmentTicks;
        }

        bool poweredFlight = _energy > 0 && !_playerOnGround;
        if (poweredFlight)
        {
            ApplyJetGroundEffect(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                basis);

            int magnitude = Magnitude3D(velocityX, velocityY, velocityZ);
            int targetSpeed =
                SimulationConstants.JetMinimumSpeedPerTick +
                ((SimulationConstants.JetMaximumSpeedPerTick -
                  SimulationConstants.JetMinimumSpeedPerTick) *
                 throttlePermille / 1_000);
            int speedCorrection = DivideRoundNearest(
                (long)(targetSpeed - magnitude) *
                    SimulationConstants.JetTargetCorrectionNumerator,
                SimulationConstants.JetTargetCorrectionDenominator);
            AddDirection(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                basis.ForwardX,
                basis.ForwardY,
                basis.ForwardZ,
                speedCorrection);

            magnitude = Magnitude3D(velocityX, velocityY, velocityZ);
            int alignmentPermille = JetAlignmentPermille();
            AlignVelocityToForward(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                basis,
                magnitude,
                alignmentPermille);

            int surfaceAltitude = PlayerAltitudeAboveSupportMillimeters();
            int friction = JetFrictionNumerator(
                surfaceAltitude,
                Magnitude3D(velocityX, velocityY, velocityZ));
            RetainMagnitude(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                friction,
                SimulationConstants.JetFrictionDenominator);
            _jetGroundedSlowTicks = 0;
        }
        else
        {
            velocityX = Retain(
                velocityX,
                SimulationConstants.JetGroundedRetentionNumerator,
                SimulationConstants.JetGroundedResponseDenominator);
            velocityY = Retain(
                velocityY,
                SimulationConstants.JetGroundedRetentionNumerator,
                SimulationConstants.JetGroundedResponseDenominator);
            velocityZ = Retain(
                velocityZ,
                SimulationConstants.JetGroundedRetentionNumerator,
                SimulationConstants.JetGroundedResponseDenominator);
            int retainedMagnitude = Magnitude3D(velocityX, velocityY, velocityZ);
            int coupling = DivideRoundNearest(
                (long)retainedMagnitude *
                    SimulationConstants.JetGroundedForwardCouplingNumerator,
                SimulationConstants.JetGroundedResponseDenominator);
            AddDirection(
                ref velocityX,
                ref velocityY,
                ref velocityZ,
                basis.ForwardX,
                basis.ForwardY,
                basis.ForwardZ,
                coupling);
        }

        if (_energy == 0)
        {
            velocityY -= SimulationConstants.JetGravityPerTick;
        }

        ApplyJetWaterSkim(ref velocityX, ref velocityY, ref velocityZ);
        ApplyLevel100FlightLimits(ref velocityX, ref velocityZ, ref velocityY);
        CommitFlightMovement(velocityX, velocityZ, velocityY);
        UpdateJetOrientation(input);
        UpdateAutomaticJetLanding();

        _jetTicksSinceTransform++;
        if (_jetStrafeTicksRemaining > 0)
        {
            _jetStrafeTicksRemaining--;
        }
    }

    // CBattleEngineJetPart::Move's
    //   cost = (maxCost - minCost) * mThrusterValue + minCost
    // per retail tick, converted to Core ticks through the one shared ratio and
    // carried in micro-retail energy so no fraction is discarded.
    private const int JetEnergyDrainDenominator =
        SimulationConstants.MicroRetailEnergyPerCoreEnergyUnit *
        SimulationConstants.RetailTicksPerCoreTickDenominator;

    private int TakeJetEnergyDrain(int throttlePermille)
    {
        int costMicroPerRetailTick =
            SimulationConstants.JetMinimumEnergyDrainMicroPerRetailTick +
            ((SimulationConstants.JetMaximumEnergyDrainMicroPerRetailTick -
              SimulationConstants.JetMinimumEnergyDrainMicroPerRetailTick) *
             throttlePermille / 1_000);
        int accumulated = _jetEnergyDrainRemainderMicro +
            (costMicroPerRetailTick *
             SimulationConstants.RetailTicksPerCoreTickNumerator);
        int drain = accumulated / JetEnergyDrainDenominator;
        _jetEnergyDrainRemainderMicro = accumulated % JetEnergyDrainDenominator;
        return drain;
    }

    private int TakeJetStrafeAcceleration(sbyte input)
    {
        int accumulated = _jetStrafeAccelerationRemainder +
            (input * SimulationConstants.JetStrafeAccelerationNumerator);
        int acceleration = accumulated /
            SimulationConstants.JetStrafeAccelerationDenominator;
        _jetStrafeAccelerationRemainder = accumulated %
            SimulationConstants.JetStrafeAccelerationDenominator;
        return acceleration;
    }

    private void ApplyJetGroundEffect(
        ref int velocityX,
        ref int velocityY,
        ref int velocityZ,
        FixedBodyBasis basis)
    {
        // Half a SECOND of travel ahead, per BattleEngineJetPart.cpp:548. This
        // read `velocity / 2` - half a Core tick - and was 30x too near; see
        // SimulationConstants.JetGroundEffectLookaheadTicks for the derivation.
        var samplePosition = new SimVector2(
            PlayerPosition.X + (velocityX * SimulationConstants.JetGroundEffectLookaheadTicks),
            PlayerPosition.Z + (velocityZ * SimulationConstants.JetGroundEffectLookaheadTicks));
        int ground = Level100Terrain.Instance.SampleGroundElevationMillimeters(samplePosition);
        bool overWater = Level100Terrain.WaterElevationMillimeters > ground;
        int support = overWater
            ? Level100Terrain.WaterElevationMillimeters
            : ground;
        int altitude = Math.Max(0, PlayerElevationMillimeters - support);
        if (altitude >= SimulationConstants.JetGroundEffectHeightMillimeters)
        {
            return;
        }

        // BattleEngineJetPart.cpp:565
        //   FVector acceleration = mOrientation*FVector(0,(5-altitude)/400,0);
        //   mMainPart->AddVelocity(acceleration);
        // per retail update, with altitude in retail units. Core's altitude is
        // in millimetres and 5 units is JetGroundEffectHeightMillimeters, so
        // the divisor is the shipped 400 verbatim. It read 900 at 30 Hz -
        // 400 x 9/4, the free-acceleration rate factor - and that factor is
        // now 1.
        int forwardAcceleration = DivideRoundNearest(
            SimulationConstants.JetGroundEffectHeightMillimeters - altitude,
            SimulationConstants.JetGroundEffectAccelerationDivisor);
        AddDirection(
            ref velocityX,
            ref velocityY,
            ref velocityZ,
            basis.ForwardX,
            basis.ForwardY,
            basis.ForwardZ,
            forwardAcceleration);

        if (velocityY < 0)
        {
            velocityY = Retain(
                velocityY,
                SimulationConstants.JetDescendingGroundEffectRetentionNumerator,
                SimulationConstants.JetDescendingGroundEffectRetentionDenominator);
        }

        int targetPitch = overWater
            ? 0
            : SampleTerrainPitchMicroRad(samplePosition, _facingYawMicroRad);
        int targetRoll = overWater
            ? 0
            : SampleTerrainRollMicroRad(samplePosition, _facingYawMicroRad);
        int heightFactor =
            SimulationConstants.JetGroundEffectHeightMillimeters - altitude;
        long responseNumerator =
            (long)SimulationConstants.JetGroundFollowNumerator * heightFactor;
        long responseDenominator =
            (long)SimulationConstants.JetGroundFollowDenominator *
            SimulationConstants.JetGroundEffectHeightMillimeters;
        _walkerPitchVelocityMicroRadPerTick += DivideRoundNearest(
            (long)(targetPitch - _facingPitchMicroRad) * responseNumerator,
            responseDenominator);
        _rollVelocityMicroRadPerTick += DivideRoundNearest(
            (long)(targetRoll - _bodyRollMicroRad) * responseNumerator,
            responseDenominator);
    }

    private void ApplyJetWaterSkim(
        ref int velocityX,
        ref int velocityY,
        ref int velocityZ)
    {
        if (Level100Terrain.WaterElevationMillimeters <=
            _playerGroundElevationMillimeters)
        {
            return;
        }

        int altitude =
            PlayerElevationMillimeters - Level100Terrain.WaterElevationMillimeters;
        int horizontalMagnitude = Magnitude2D(velocityX, velocityZ);
        if (altitude >= SimulationConstants.JetSkimHeightMillimeters ||
            horizontalMagnitude <= SimulationConstants.JetSkimMinimumHorizontalSpeedPerTick)
        {
            return;
        }

        // BattleEngineJetPart.cpp:532-534, per retail update:
        //   mVelocity.Z -= magnitudeXY*0.3f;      (retail Z down, Core Y up)
        //   mPitchvel   -= 0.01f*magnitudeXY;
        // The 0.3 is a ratio between two velocities and carries no rate factor
        // at all. The pitch kick is 0.01 rad per RETAIL UNIT of horizontal
        // speed, so at 20 Hz it is 10_000 micro-rad per 1_000 mm. It read
        // 4_608/667 at 30 Hz - 10_000 divided by the R3 damped-input factor
        // 2.170337, over the old 667 mm retail-unit speed scale.
        velocityY += DivideRoundNearest((long)horizontalMagnitude * 3, 10);
        _walkerPitchVelocityMicroRadPerTick -= DivideRoundNearest(
            (long)horizontalMagnitude *
                SimulationConstants.JetWaterSkimPitchKickMicroRadPerRetailUnit,
            SimulationConstants.MillimetersPerRetailUnit);
        velocityX = Retain(
            velocityX,
            SimulationConstants.JetSkimRetentionNumerator,
            SimulationConstants.JetSkimRetentionDenominator);
        velocityY = Retain(
            velocityY,
            SimulationConstants.JetSkimRetentionNumerator,
            SimulationConstants.JetSkimRetentionDenominator);
        velocityZ = Retain(
            velocityZ,
            SimulationConstants.JetSkimRetentionNumerator,
            SimulationConstants.JetSkimRetentionDenominator);

        // BattleEngineJetPart.cpp:536-538
        //     float damage=(0.5f-altitude)*20.0f;
        //     mMainPart->Damage(damage,NULL,FALSE);
        //     mMainPart->HostileEnvironment();
        //
        // Skimming water HURTS, and this reconstruction simply did not apply it -
        // measured 2026-07-27 by flying four skim passes at ~500 mm over water at
        // 400+ mm/tick with the hull sitting at full throughout.
        //
        // Units, and THE TWO THOUSANDS CANCEL - which is worth spelling out
        // because getting it wrong once produced damage 1,000x too small and the
        // law test below caught it:
        //     depth in millimetres -> released units   is  / 1,000
        //     released life        -> registry milli-life is  x 1,000
        // so the conversion is just depth_mm * 20. Core now runs at retail's
        // own 20 Hz rate, so the retained ratio is 1/1. At zero altitude that is
        // 10,000 milli-life per update against a 20,000 hull.
        //
        // That is brutal, and it is meant to be: retail's own numbers are 10.0
        // damage per update against mLife 20.0. Damage starts death only below
        // zero, so two updates leave exact-zero life alive and the third kills:
        // 0.15 seconds. This was NOT softened to taste, and it could not have
        // been written at all before
        // MaximumHull was corrected from 1.0 to 20.0 released life - against the
        // old unit the same law would have killed the player in a sixth of a
        // tick and read as a bug in this code rather than in the constant.
        //
        // HostileEnvironment() (BattleEngine.cpp:3269) is NOT here: it plays
        // "hud_hostile_environment" on a 5 s cooldown, which is presentation.
        // The WaterSkim event below is what a client gates that sample on.
        int skimDamage = checked((int)DivideRoundNearest(
            (long)(SimulationConstants.JetSkimHeightMillimeters - altitude) *
                SimulationConstants.JetWaterSkimDamagePerReleasedUnit *
                SimulationConstants.RetailTicksPerCoreTickNumerator,
            SimulationConstants.RetailTicksPerCoreTickDenominator));
        if (skimDamage > 0)
        {
            if (ApplyLevel100PlayerDamage(
                skimDamage,
                damageShields: false,
                Level100PlayerDamageSource.WaterSkim,
                out bool skimRequestsDeath))
            {
                CompleteLevel100PlayerDamageDeath(skimRequestsDeath);
            }
        }

        EmitFlightEvent(AquilaFlightEvents.WaterSkim);
    }

    private void UpdateJetOrientation(SimInput input)
    {
        SimVector2 playerVelocity = PlayerVelocity;
        int speed = Magnitude3D(
            playerVelocity.X,
            PlayerVerticalVelocityMillimetersPerTick,
            playerVelocity.Z);
        int ratePermille = speed > 7 ? 1_000 : 0;
        if (_playerOnGround && speed < SimulationConstants.JetMaximumSpeedPerTick)
        {
            ratePermille = speed < SimulationConstants.JetGroundedSlowSpeedPerTick
                ? 0
                : Math.Min(1_000, speed * 1_000 / SimulationConstants.JetMaximumSpeedPerTick);
        }
        ratePermille = ratePermille *
            Math.Min(_jetTicksSinceTransform, SimulationConstants.JetInputRampTicks) /
            SimulationConstants.JetInputRampTicks;

        int lookXPermille = ResolveLookInputPermille(input.LookX, input.LookXAnalogPermille);
        int lookYPermille = ResolveLookInputPermille(input.LookY, input.LookYAnalogPermille);
        _walkerYawVelocityMicroRadPerTick =
            RetainAngularVelocity(_walkerYawVelocityMicroRadPerTick) +
            ScaleZoomedLookInput(
                SimulationConstants.JetYawInputMicroRadPerTick,
                lookXPermille * ratePermille / 1_000);
        _walkerPitchVelocityMicroRadPerTick =
            RetainAngularVelocity(_walkerPitchVelocityMicroRadPerTick) +
            ScaleZoomedLookInput(
                SimulationConstants.JetPitchInputMicroRadPerTick,
                lookYPermille * ratePermille / 1_000);
        _rollVelocityMicroRadPerTick =
            RetainAngularVelocity(_rollVelocityMicroRadPerTick) +
            ScaleZoomedLookInput(
                SimulationConstants.JetRollInputMicroRadPerTick,
                lookXPermille * ratePermille / 1_000);

        ApplyJetPitchSoftLimit();
        _facingYawMicroRad = NormalizeMicroRad(
            _facingYawMicroRad + _walkerYawVelocityMicroRadPerTick);
        _facingPitchMicroRad = NormalizeMicroRad(
            _facingPitchMicroRad + _walkerPitchVelocityMicroRadPerTick);
        _bodyRollMicroRad = NormalizeMicroRad(
            _bodyRollMicroRad + _rollVelocityMicroRadPerTick);
        if (!(_playerOnGround && speed < SimulationConstants.JetGroundedSlowSpeedPerTick))
        {
            _bodyRollMicroRad = Retain(
                _bodyRollMicroRad,
                SimulationConstants.JetRollAutoLevelNumerator,
                SimulationConstants.JetRollAutoLevelDenominator);
        }
        QuantizeFacingFromYaw();
    }

    private void ApplyJetPitchSoftLimit()
    {
        int excess = Math.Abs(_facingPitchMicroRad) -
            SimulationConstants.JetPitchSoftLimitMicroRad;
        if (excess <= 0 ||
            Math.Sign(_walkerPitchVelocityMicroRadPerTick) !=
            Math.Sign(_facingPitchMicroRad))
        {
            return;
        }

        int multiplier = Math.Max(0, 1_000_000 - ((excess * 3) / 2));
        _walkerPitchVelocityMicroRadPerTick = Retain(
            _walkerPitchVelocityMicroRadPerTick,
            multiplier,
            1_000_000);
    }

    private void UpdateAutomaticJetLanding()
    {
        SimVector2 playerVelocity = PlayerVelocity;
        int speed = Magnitude3D(
            playerVelocity.X,
            PlayerVerticalVelocityMillimetersPerTick,
            playerVelocity.Z);
        if (_playerOnGround && speed < SimulationConstants.JetGroundedSlowSpeedPerTick)
        {
            _jetGroundedSlowTicks++;
        }
        else if (!_playerOnGround && _energy > 0)
        {
            _jetGroundedSlowTicks = 0;
        }

        if (!_playerOnGround &&
            _jetTicksSinceTransform >= SimulationConstants.JetTransformAlignmentTicks &&
            speed < SimulationConstants.JetStallSpeedPerTick)
        {
            if (_jetStallTicks == 0)
            {
                EmitFlightEvent(AquilaFlightEvents.StallStarted);
            }
            _jetStallTicks++;
        }
        else
        {
            _jetStallTicks = 0;
        }

        if (_jetTicksSinceTransform < SimulationConstants.JetAutoLandEligibilityTicks)
        {
            return;
        }

        if (_jetGroundedSlowTicks >= SimulationConstants.JetAutoLandDelayTicks ||
            speed < SimulationConstants.JetAutoLandSpeedPerTick ||
            _jetStallTicks >= SimulationConstants.JetStallDelayTicks)
        {
            BeginJetToWalkerTransition();
        }
    }

    private int JetAlignmentPermille()
    {
        if (_jetTicksSinceTransform < SimulationConstants.JetTransformAlignmentTicks)
        {
            return _jetTicksSinceTransform * 1_000 /
                SimulationConstants.JetTransformAlignmentTicks;
        }
        if (_jetStrafeTicksRemaining > 0)
        {
            return (SimulationConstants.JetStrafeAlignmentTicks -
                _jetStrafeTicksRemaining) * 1_000 /
                SimulationConstants.JetStrafeAlignmentTicks;
        }

        return 1_000;
    }

    /// <summary>
    /// Core's integer form of <c>CBattleEngineJetPart::GetFriction</c>'s ladder.
    /// <c>internal</c> rather than <c>private</c> because no replay in this
    /// repository reaches the slow-flight gate — see
    /// <c>RetailJetFrictionTests.CoreLadder_GatesTheInterpolatedArmAtOnePointFive</c>,
    /// which is the only falsifier the gate has.
    /// </summary>
    internal static int JetFrictionNumerator(int altitude, int speed)
    {
        if (altitude < 1_000)
        {
            return SimulationConstants.JetNearSurfaceFrictionNumerator;
        }
        if (altitude < 3_000)
        {
            // THE SLOW-FLIGHT GATE IS 1.5 RELEASED UNITS PER UPDATE, NOT 1.0.
            // `BattleEngineJetPart.cpp:628` reads `if (velocity<1.5f)`, and the
            // pristine 74154bfa image agrees byte for byte: the float compare against
            // the value at 0x005D8BD8, popping the result at 0x00411B39 inside
            // CBattleEngineJetPart::GetFriction (0x00411AA0) loads 0x3FC00000
            // = 1.5f. One released unit per update is
            // SimulationConstants.MillimetersPerRetailUnit = 1_000 Core
            // millimetres per tick - the same scale that makes the altitude
            // bounds above 1_000 and 3_000 for retail's 1 and 3 - so the gate
            // is 1_500.
            //
            // It was 1_000, i.e. retail's 1.0, which flattened the interpolated
            // arm to 0.99 for every speed in [1.0, 1.5) below the ceiling;
            // retail interpolates there, reaching 0.98 at altitude 2.
            // RetailJetFriction is the float-exact model of the same law and
            // RetailJetFrictionTests pins the gate to those bytes.
            if (speed >= 1_500)
            {
                return SimulationConstants.JetNearSurfaceFrictionNumerator;
            }

            if (altitude < 2_000)
            {
                return Interpolate(
                    SimulationConstants.JetNearSurfaceFrictionNumerator,
                    SimulationConstants.JetCruiseFrictionNumerator,
                    altitude - 1_000,
                    1_000);
            }

            return Interpolate(
                SimulationConstants.JetCruiseFrictionNumerator,
                SimulationConstants.JetLowAltitudeFrictionNumerator,
                altitude - 2_000,
                1_000);
        }

        return SimulationConstants.JetCruiseFrictionNumerator;
    }

    private static int Interpolate(int from, int to, int elapsed, int duration) =>
        from + DivideRoundNearest((long)(to - from) * elapsed, duration);

    private void UpdateWalkerYaw(sbyte lookX, short analogPermille)
    {
        int inputPermille = ResolveLookInputPermille(lookX, analogPermille);
        _walkerYawVelocityMicroRadPerTick =
            (int)((long)_walkerYawVelocityMicroRadPerTick *
                SimulationConstants.WalkerYawRetentionNumerator /
                SimulationConstants.WalkerYawRetentionDenominator) +
            ScaleZoomedLookInput(
                SimulationConstants.WalkerYawInputMicroRadPerTick,
                inputPermille);
        _facingYawMicroRad = NormalizeMicroRad(
            _facingYawMicroRad + _walkerYawVelocityMicroRadPerTick);
        QuantizeFacingFromYaw();
    }

    private void UpdateWalkerPitch(sbyte lookY, short analogPermille)
    {
        int inputPermille = ResolveLookInputPermille(lookY, analogPermille);
        _walkerPitchVelocityMicroRadPerTick =
            RetainAngularVelocity(_walkerPitchVelocityMicroRadPerTick) +
            ScaleZoomedLookInput(
                SimulationConstants.WalkerPitchInputMicroRadPerTick,
                inputPermille);
        ApplyWalkerTerrainPitchLimit();
        _facingPitchMicroRad = NormalizeMicroRad(
            _facingPitchMicroRad + _walkerPitchVelocityMicroRadPerTick);
    }

    private void ApplyWalkerTerrainPitchLimit()
    {
        if (!_playerOnGround)
        {
            ApplyJetPitchSoftLimit();
            return;
        }

        int groundPitch = SampleTerrainPitchMicroRad(
            PlayerPosition,
            _facingYawMicroRad);
        int delta = _facingPitchMicroRad - groundPitch;
        if (delta > 700_000)
        {
            _walkerPitchVelocityMicroRadPerTick -= DivideRoundNearest(
                (long)(delta - 700_000) *
                    SimulationConstants.WalkerTerrainPitchCorrectionNumerator,
                SimulationConstants.WalkerTerrainPitchCorrectionDenominator);
        }
        else if (delta > 500_000 && _walkerPitchVelocityMicroRadPerTick > 0)
        {
            int multiplier = Math.Max(0, 1_000_000 - ((delta - 500_000) * 6));
            _walkerPitchVelocityMicroRadPerTick = Retain(
                _walkerPitchVelocityMicroRadPerTick,
                multiplier,
                1_000_000);
        }
        else if (delta < -1_000_000)
        {
            _walkerPitchVelocityMicroRadPerTick -= DivideRoundNearest(
                (long)(delta + 1_000_000) *
                    SimulationConstants.WalkerTerrainPitchCorrectionNumerator,
                SimulationConstants.WalkerTerrainPitchCorrectionDenominator);
        }
        else if (delta < -800_000 && _walkerPitchVelocityMicroRadPerTick < 0)
        {
            int multiplier = Math.Max(0, 1_000_000 - (Math.Abs(delta + 800_000) * 6));
            _walkerPitchVelocityMicroRadPerTick = Retain(
                _walkerPitchVelocityMicroRadPerTick,
                multiplier,
                1_000_000);
        }
    }

    private void UpdateNonJetRoll()
    {
        _rollVelocityMicroRadPerTick = RetainAngularVelocity(
            _rollVelocityMicroRadPerTick);
        _bodyRollMicroRad = NormalizeMicroRad(
            _bodyRollMicroRad + _rollVelocityMicroRadPerTick);
        RetainNonJetRollAngle();
    }

    private void RetainNonJetRollAngle()
    {
        _bodyRollMicroRad = Retain(
            _bodyRollMicroRad,
            SimulationConstants.WalkerYawRetentionNumerator,
            SimulationConstants.WalkerYawRetentionDenominator);
    }

    private static int RetainAngularVelocity(int velocity) =>
        (int)((long)velocity * SimulationConstants.WalkerYawRetentionNumerator /
            SimulationConstants.WalkerYawRetentionDenominator);

    private static int RetainWalkerVerticalVelocity(int velocity) => Retain(
        velocity,
        SimulationConstants.WalkerVerticalRetentionNumerator,
        SimulationConstants.WalkerVerticalRetentionDenominator);

    // Player.cpp:334-355 curves the look axis before it reaches the battle
    // engine, so the curve belongs at the single point where Core resolves a
    // look input rather than at each of the four consumers.
    private static int ResolveLookInputPermille(sbyte digital, short analog) =>
        LookAxisResponse.Apply(
            Math.Clamp((digital * 1_000) + analog, -1_000, 1_000));

    private int ScaleZoomedLookInput(int fullScale, int inputPermille) =>
        DivideRoundNearest(
            (long)fullScale * inputPermille * _zoomPermille,
            (long)SimulationConstants.ZoomScale * 1_000);

    private void UpdateWalkerMovement(SimInput input)
    {
        SimVector2 acceleration = ResolveWalkerAcceleration(input);
        var velocity = new SimVector2(
            RetainWalkerVelocity(PlayerVelocity.X) + acceleration.X,
            RetainWalkerVelocity(PlayerVelocity.Z) + acceleration.Z);
        if (_walkerDashTicksRemaining <
            SimulationConstants.WalkerDashFrictionThresholdTicks)
        {
            velocity = ClampMagnitude(
                velocity,
                SimulationConstants.WalkerMaximumSpeedPerTick);
        }
        MoveWalker(velocity);
        if (_walkerDashTicksRemaining > 0)
        {
            _walkerDashTicksRemaining--;
        }
    }

    private SimVector2 ResolveWalkerAcceleration(SimInput input)
    {
        if (_walkerDashTicksRemaining > 0)
        {
            return SimVector2.Zero;
        }

        int moveXPermille = input.MoveX * 1_000;
        int moveZPermille = input.MoveZ * 1_000;
        bool dashForwardOrBackward = ObserveWalkerForwardGesture(moveZPermille);
        int localX = input.MoveX * SimulationConstants.WalkerAccelerationPerTick;
        int localZ = input.MoveZ * SimulationConstants.WalkerAccelerationPerTick;
        bool dashLeftOrRight = false;

        // Retail receives the longitudinal button callbacks before the lateral
        // callbacks. Once a longitudinal dash starts, the lateral callback
        // returns at its mDoingDashCount gate and does not alter its history.
        if (dashForwardOrBackward)
        {
            EmitFlightEvent(AquilaFlightEvents.WalkerDashRequested);
            localX = 0;
            localZ *= SimulationConstants.WalkerDashAccelerationMultiplier;
        }
        else
        {
            dashLeftOrRight = ObserveWalkerStrafeGesture(moveXPermille);
            if (dashLeftOrRight)
            {
                EmitFlightEvent(AquilaFlightEvents.WalkerDashRequested);
                localX *= SimulationConstants.WalkerDashAccelerationMultiplier;
            }
        }

        if (!dashForwardOrBackward && !dashLeftOrRight &&
            input.MoveX != 0 && input.MoveZ != 0)
        {
            localX = localX * 181 / 256;
            localZ = localZ * 181 / 256;
        }

        (int sin, int cos) = FixedSinCos(_facingYawMicroRad);
        return new SimVector2(
            DivideRoundNearest(((long)localX * cos) - ((long)localZ * sin), FixedTrigScale),
            DivideRoundNearest(((long)localX * sin) + ((long)localZ * cos), FixedTrigScale));
    }

    private bool ObserveWalkerForwardGesture(int movePermille)
    {
        bool hardForward =
            _walkerLastMoveZPermille < SimulationConstants.WalkerDashStartPermille &&
            movePermille > SimulationConstants.WalkerDashStartPermille;
        bool forwardEdge =
            _walkerLastMoveZPermille < SimulationConstants.WalkerDashEndPermille &&
            movePermille > SimulationConstants.WalkerDashEndPermille;
        bool hardBackward =
            _walkerLastMoveZPermille > -SimulationConstants.WalkerDashStartPermille &&
            movePermille < -SimulationConstants.WalkerDashStartPermille;
        bool backwardEdge =
            _walkerLastMoveZPermille > -SimulationConstants.WalkerDashEndPermille &&
            movePermille < -SimulationConstants.WalkerDashEndPermille;

        if (hardForward)
        {
            _walkerLastHardForwardTimeBits = BitConverter.SingleToInt32Bits(EngineTimeSeconds);
        }
        if (hardBackward)
        {
            _walkerLastHardBackwardTimeBits = BitConverter.SingleToInt32Bits(EngineTimeSeconds);
        }

        bool dash =
            (forwardEdge && WalkerDashWindowContains(_walkerLastHardBackwardTimeBits)) ||
            (backwardEdge && WalkerDashWindowContains(_walkerLastHardForwardTimeBits));
        _walkerLastMoveZPermille = movePermille;
        if (dash)
        {
            _walkerDashTicksRemaining = SimulationConstants.WalkerDashLengthTicks;
        }
        return dash;
    }

    private bool ObserveWalkerStrafeGesture(int movePermille)
    {
        bool hardRight =
            _walkerLastMoveXPermille < SimulationConstants.WalkerDashStartPermille &&
            movePermille > SimulationConstants.WalkerDashStartPermille;
        bool rightEdge =
            _walkerLastMoveXPermille < SimulationConstants.WalkerDashEndPermille &&
            movePermille > SimulationConstants.WalkerDashEndPermille;
        bool hardLeft =
            _walkerLastMoveXPermille > -SimulationConstants.WalkerDashStartPermille &&
            movePermille < -SimulationConstants.WalkerDashStartPermille;
        bool leftEdge =
            _walkerLastMoveXPermille > -SimulationConstants.WalkerDashEndPermille &&
            movePermille < -SimulationConstants.WalkerDashEndPermille;

        if (hardRight)
        {
            _walkerLastHardRightTimeBits = BitConverter.SingleToInt32Bits(EngineTimeSeconds);
        }
        if (hardLeft)
        {
            _walkerLastHardLeftTimeBits = BitConverter.SingleToInt32Bits(EngineTimeSeconds);
        }

        bool dashRight = rightEdge && WalkerDashWindowContains(_walkerLastHardLeftTimeBits);
        bool dashLeft = leftEdge && WalkerDashWindowContains(_walkerLastHardRightTimeBits);
        _walkerLastMoveXPermille = movePermille;
        if (dashRight || dashLeft)
        {
            _walkerDashTicksRemaining = SimulationConstants.WalkerDashLengthTicks;
            if (dashLeft)
            {
                _rollVelocityMicroRadPerTick =
                    SimulationConstants.WalkerDashRollVelocityMicroRadPerTick;
            }
            else
            {
                // Retail's lateral pair is intentionally asymmetric: left
                // assigns +0.08, while right subtracts 0.08 from residual roll.
                _rollVelocityMicroRadPerTick -=
                    SimulationConstants.WalkerDashRollVelocityMicroRadPerTick;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Retail's dash window (walker-dash.md): the opposite hard press's event
    /// time lies strictly between now - mDashTime and now - 0.5 * mDashTime.
    /// Each difference is rounded to float32, as the x87 does at the single
    /// precision Direct3D leaves it in (<c>0x00412e1f-0x00412e58</c>; the
    /// same pair in Backward, StrafeLeft and StrafeRight).
    /// </summary>
    private bool WalkerDashWindowContains(int hardPressTimeBits)
    {
        float now = EngineTimeSeconds;
        float last = BitConverter.Int32BitsToSingle(hardPressTimeBits);
        float dashTime = BitConverter.Int32BitsToSingle(SimulationConstants.WalkerDashTimeFloatBits);
        return last > (float)(now - dashTime) && last < (float)(now - (dashTime * 0.5f));
    }

    private static int RetainWalkerVelocity(int velocity) =>
        (int)((long)velocity * SimulationConstants.WalkerVelocityRetentionNumerator /
            SimulationConstants.WalkerVelocityRetentionDenominator);

    private const int FixedTrigScale = 1 << 30;
    private const int HalfPiMicroRad = 1_570_796;
    private const int PiMicroRad = 3_141_593;
    private const int CordicGainQ30 = 652_032_874;

    // Integer CORDIC keeps the local-to-world rotation deterministic without
    // introducing platform floating-point math into Core state updates.
    private static ReadOnlySpan<int> CordicAnglesMicroRad =>
    [
        785_398, 463_648, 244_979, 124_355, 62_419, 31_240, 15_624,
        7_812, 3_906, 1_953, 977, 488, 244, 122, 61, 31, 15, 8, 4, 2, 1,
    ];

    private static (int Sin, int Cos) FixedSinCos(int angleMicroRad)
    {
        int angle = NormalizeMicroRad(angleMicroRad);
        int resultSign = 1;
        if (angle > HalfPiMicroRad)
        {
            angle -= PiMicroRad;
            resultSign = -1;
        }
        else if (angle < -HalfPiMicroRad)
        {
            angle += PiMicroRad;
            resultSign = -1;
        }

        long x = CordicGainQ30;
        long y = 0;
        int remainder = angle;
        ReadOnlySpan<int> angles = CordicAnglesMicroRad;
        for (int index = 0; index < angles.Length; index++)
        {
            long previousX = x;
            if (remainder >= 0)
            {
                x -= y >> index;
                y += previousX >> index;
                remainder -= angles[index];
            }
            else
            {
                x += y >> index;
                y -= previousX >> index;
                remainder += angles[index];
            }
        }

        return ((int)y * resultSign, (int)x * resultSign);
    }

    private static int FixedAtan2(long y, long x)
    {
        if (x == 0)
        {
            return y switch
            {
                > 0 => HalfPiMicroRad,
                < 0 => -HalfPiMicroRad,
                _ => 0,
            };
        }

        int angle = 0;
        if (x < 0)
        {
            bool upperHalf = y >= 0;
            x = -x;
            y = -y;
            angle = upperHalf ? PiMicroRad : -PiMicroRad;
        }

        ReadOnlySpan<int> angles = CordicAnglesMicroRad;
        for (int index = 0; index < angles.Length; index++)
        {
            long previousX = x;
            if (y > 0)
            {
                x += y >> index;
                y -= previousX >> index;
                angle += angles[index];
            }
            else if (y < 0)
            {
                x -= y >> index;
                y += previousX >> index;
                angle -= angles[index];
            }
        }

        return NormalizeMicroRad(angle);
    }

    private readonly record struct FixedBodyBasis(
        int ForwardX,
        int ForwardY,
        int ForwardZ,
        int RightX,
        int RightY,
        int RightZ,
        int UpX = 0,
        int UpY = 0,
        int UpZ = 0);

    private FixedBodyBasis GetBodyBasis()
    {
        (int yawSin, int yawCos) = FixedSinCos(_facingYawMicroRad);
        (int pitchSin, int pitchCos) = FixedSinCos(_facingPitchMicroRad);
        (int rollSin, int rollCos) = FixedSinCos(_bodyRollMicroRad);

        int forwardX = -MultiplyFixed(yawSin, pitchCos);
        int forwardY = -pitchSin;
        int forwardZ = MultiplyFixed(yawCos, pitchCos);
        int baseRightX = yawCos;
        int baseRightZ = yawSin;
        int baseUpX = -MultiplyFixed(pitchSin, yawSin);
        int baseUpY = pitchCos;
        int baseUpZ = MultiplyFixed(pitchSin, yawCos);
        int rightX = MultiplyFixed(baseRightX, rollCos) +
            MultiplyFixed(baseUpX, rollSin);
        int rightY = MultiplyFixed(baseUpY, rollSin);
        int rightZ = MultiplyFixed(baseRightZ, rollCos) +
            MultiplyFixed(baseUpZ, rollSin);
        // The same roll turns up away from right.
        int upX = MultiplyFixed(baseUpX, rollCos) - MultiplyFixed(baseRightX, rollSin);
        int upY = MultiplyFixed(baseUpY, rollCos);
        int upZ = MultiplyFixed(baseUpZ, rollCos) - MultiplyFixed(baseRightZ, rollSin);
        return new FixedBodyBasis(
            forwardX,
            forwardY,
            forwardZ,
            rightX,
            rightY,
            rightZ,
            upX,
            upY,
            upZ);
    }

    private static int MultiplyFixed(int left, int right) =>
        DivideRoundNearest((long)left * right, FixedTrigScale);

    private static void AddDirection(
        ref int velocityX,
        ref int velocityY,
        ref int velocityZ,
        int directionX,
        int directionY,
        int directionZ,
        int magnitude)
    {
        velocityX += DivideRoundNearest((long)directionX * magnitude, FixedTrigScale);
        velocityY += DivideRoundNearest((long)directionY * magnitude, FixedTrigScale);
        velocityZ += DivideRoundNearest((long)directionZ * magnitude, FixedTrigScale);
    }

    private static void AlignVelocityToForward(
        ref int velocityX,
        ref int velocityY,
        ref int velocityZ,
        FixedBodyBasis basis,
        int magnitude,
        int mixPermille)
    {
        int targetX = DivideRoundNearest(
            (long)basis.ForwardX * magnitude,
            FixedTrigScale);
        int targetY = DivideRoundNearest(
            (long)basis.ForwardY * magnitude,
            FixedTrigScale);
        int targetZ = DivideRoundNearest(
            (long)basis.ForwardZ * magnitude,
            FixedTrigScale);
        velocityX = Interpolate(velocityX, targetX, mixPermille, 1_000);
        velocityY = Interpolate(velocityY, targetY, mixPermille, 1_000);
        velocityZ = Interpolate(velocityZ, targetZ, mixPermille, 1_000);
        if (mixPermille == 1_000)
        {
            SetMagnitude(ref velocityX, ref velocityY, ref velocityZ, magnitude);
        }
    }

    private int PlayerAltitudeAboveSupportMillimeters() =>
        PlayerElevationMillimeters - Math.Max(
            _playerGroundElevationMillimeters,
            Level100Terrain.WaterElevationMillimeters);

    private static int Magnitude2D(int x, int z) =>
        IntegerSquareRoot(((long)x * x) + ((long)z * z));

    private static int Magnitude3D(int x, int y, int z) =>
        IntegerSquareRoot(
            ((long)x * x) +
            ((long)y * y) +
            ((long)z * z));

    /// <summary>
    /// The source and pristine-PC-static <c>CBattleEngine::DeclareOnGround</c>
    /// response in Core's integer units. This is deliberately separate from
    /// the actor's later generic vertical bounce.
    /// </summary>
    internal static TerrainGroundImpactResponse ResolveTerrainGroundImpact(
        bool isWalkerState,
        bool isJetState,
        bool walkerDashActive,
        SimVector3 velocity,
        SimVector2 groundGradientPermille)
    {
        if (isWalkerState && isJetState)
        {
            throw new ArgumentException(
                "A ground impact cannot be in pure walker and pure jet state simultaneously.");
        }

        long velocitySquared =
            ((long)velocity.X * velocity.X) +
            ((long)velocity.Y * velocity.Y) +
            ((long)velocity.Z * velocity.Z);
        int speed = IntegerSquareRoot(velocitySquared);

        // BattleEngine.cpp:2966-2973 returns before either damage or velocity
        // response while the walker dash counter is nonzero.
        if (isWalkerState && walkerDashActive)
        {
            return new TerrainGroundImpactResponse(
                speed,
                DamageThresholdCrossed: false,
                DamageMilliLife: 0,
                velocity);
        }

        int threshold = isWalkerState
            ? SimulationConstants.WalkerGroundImpactThresholdPerTick
            : SimulationConstants.JetGroundImpactThresholdPerTick;
        if (velocitySquared <= (long)threshold * threshold)
        {
            SimVector3 retainedVelocity = isJetState
                ? new SimVector3(
                    Retain(
                        velocity.X,
                        SimulationConstants.JetLowSpeedGroundContactRetentionNumerator,
                        SimulationConstants.GroundContactRetentionDenominator),
                    Retain(
                        velocity.Y,
                        SimulationConstants.JetLowSpeedGroundContactRetentionNumerator,
                        SimulationConstants.GroundContactRetentionDenominator),
                    Retain(
                        velocity.Z,
                        SimulationConstants.JetLowSpeedGroundContactRetentionNumerator,
                        SimulationConstants.GroundContactRetentionDenominator))
                : velocity;
            return new TerrainGroundImpactResponse(
                speed,
                DamageThresholdCrossed: false,
                DamageMilliLife: 0,
                retainedVelocity);
        }

        // Core Y is up. For height y=h(x,z), (-dh/dx,1,-dh/dz) is the matching
        // normal. The source squares the normalized dot, so normal orientation
        // itself is immaterial. BigInteger keeps this one rare contact ratio
        // exact until the deterministic one-part-per-million quantization; a
        // steep HFLD normal can overflow dot^2 * scale in Int64.
        long normalX = -groundGradientPermille.X;
        const long normalY = 1_000;
        long normalZ = -groundGradientPermille.Z;
        long normalSquared =
            (normalX * normalX) +
            (normalY * normalY) +
            (normalZ * normalZ);
        long normalDotVelocity =
            (normalX * velocity.X) +
            (normalY * velocity.Y) +
            (normalZ * velocity.Z);
        BigInteger incidenceNumerator =
            (BigInteger)normalDotVelocity * normalDotVelocity *
            SimulationConstants.GroundImpactIncidenceScale;
        BigInteger incidenceDenominator =
            (BigInteger)normalSquared * velocitySquared;
        int incidenceSquaredPermillion = Math.Clamp(
            (int)((incidenceNumerator + (incidenceDenominator / 2)) /
                incidenceDenominator),
            0,
            SimulationConstants.GroundImpactIncidenceScale);
        int retainedPermillion =
            SimulationConstants.GroundImpactIncidenceScale -
            incidenceSquaredPermillion;
        int damageMilliLife = checked((int)DivideRoundNearest(
            (long)speed * SimulationConstants.GroundImpactDamageMultiplier *
                incidenceSquaredPermillion,
            SimulationConstants.GroundImpactIncidenceScale));
        var retained = new SimVector3(
            Retain(
                velocity.X,
                retainedPermillion,
                SimulationConstants.GroundImpactIncidenceScale),
            Retain(
                velocity.Y,
                retainedPermillion,
                SimulationConstants.GroundImpactIncidenceScale),
            Retain(
                velocity.Z,
                retainedPermillion,
                SimulationConstants.GroundImpactIncidenceScale));
        return new TerrainGroundImpactResponse(
            speed,
            DamageThresholdCrossed: true,
            damageMilliLife,
            retained);
    }

    internal static int SampleTerrainPitchMicroRad(
        SimVector2 position,
        int yawMicroRad)
    {
        SimVector2 gradient =
            Level100Terrain.Instance.SampleGroundGradientPermille(position);
        (int sin, int cos) = FixedSinCos(yawMicroRad);
        int forwardSlopePermille = DivideRoundNearest(
            (-(long)gradient.X * sin) + ((long)gradient.Z * cos),
            FixedTrigScale);
        int tangentLengthPermille = IntegerSquareRoot(
            1_000_000L + ((long)forwardSlopePermille * forwardSlopePermille));
        return -DivideRoundNearest(
            (long)forwardSlopePermille * 1_000_000,
            tangentLengthPermille);
    }

    internal static int SampleTerrainRollMicroRad(
        SimVector2 position,
        int yawMicroRad)
    {
        SimVector2 gradient =
            Level100Terrain.Instance.SampleGroundGradientPermille(position);
        (int sin, int cos) = FixedSinCos(yawMicroRad);
        int rightSlopePermille = DivideRoundNearest(
            ((long)gradient.X * cos) + ((long)gradient.Z * sin),
            FixedTrigScale);
        int tangentLengthPermille = IntegerSquareRoot(
            1_000_000L + ((long)rightSlopePermille * rightSlopePermille));
        return DivideRoundNearest(
            (long)rightSlopePermille * 500_000,
            tangentLengthPermille);
    }

    private static int Retain(int value, int numerator, int denominator) =>
        DivideRoundNearest((long)value * numerator, denominator);

    private static void RetainMagnitude(
        ref int x,
        ref int y,
        ref int z,
        int numerator,
        int denominator)
    {
        int magnitude = Magnitude3D(x, y, z);
        if (magnitude == 0)
        {
            return;
        }

        int retainedMagnitude = Retain(magnitude, numerator, denominator);
        SetMagnitude(ref x, ref y, ref z, retainedMagnitude);
    }

    private static void SetMagnitude(ref int x, ref int y, ref int z, int magnitude)
    {
        if (magnitude < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }
        if (magnitude == 0)
        {
            x = 0;
            y = 0;
            z = 0;
            return;
        }

        int currentMagnitude = Magnitude3D(x, y, z);
        if (currentMagnitude == 0 || currentMagnitude == magnitude)
        {
            return;
        }

        int dominantAxis;
        int dominantSign;
        if (Math.Abs(x) >= Math.Abs(y) && Math.Abs(x) >= Math.Abs(z))
        {
            dominantAxis = 0;
            dominantSign = Math.Sign(x);
        }
        else if (Math.Abs(y) >= Math.Abs(z))
        {
            dominantAxis = 1;
            dominantSign = Math.Sign(y);
        }
        else
        {
            dominantAxis = 2;
            dominantSign = Math.Sign(z);
        }

        x = DivideRoundNearest((long)x * magnitude, currentMagnitude);
        y = DivideRoundNearest((long)y * magnitude, currentMagnitude);
        z = DivideRoundNearest((long)z * magnitude, currentMagnitude);

        // Fixed-point component rounding can leave the vector one scalar unit
        // either side of the source float operation. Correct the dominant axis
        // within a fixed bound so malformed future inputs cannot hang Core.
        const int maximumCorrectionSteps = 4;
        for (int step = 0; step < maximumCorrectionSteps; step++)
        {
            int correctedMagnitude = Magnitude3D(x, y, z);
            if (correctedMagnitude == magnitude)
            {
                return;
            }

            int correction = dominantSign *
                (correctedMagnitude < magnitude ? 1 : -1);
            switch (dominantAxis)
            {
                case 0:
                    x += correction;
                    break;
                case 1:
                    y += correction;
                    break;
                default:
                    z += correction;
                    break;
            }
        }

        throw new InvalidOperationException(
            $"Fixed magnitude correction did not converge to {magnitude}.");
    }

    private static int DivideRoundNearest(long numerator, long denominator)
    {
        long half = denominator / 2L;
        return (int)(numerator >= 0
            ? (numerator + half) / denominator
            : (numerator - half) / denominator);
    }

    private void MoveWalker(SimVector2 velocity)
    {
        SimVector2 currentPosition = PlayerPosition;
        if (GoingIntoLevel100Water(velocity))
        {
            velocity = SimVector2.Zero;
        }
        else
        {
            velocity = ResolveLevel100SteepSlope(velocity);
        }
        SimVector2 nextPosition = new(
            currentPosition.X + velocity.X,
            currentPosition.Z + velocity.Z);
        nextPosition = ResolveLevel100WalkerContact(
            currentPosition,
            nextPosition,
            SimulationConstants.Level100ControlTowerPosition,
            SimulationConstants.Level100ControlTowerContactRadius);
        nextPosition = ResolveLevel100WalkerContact(
            currentPosition,
            nextPosition,
            SimulationConstants.Level100TankFactoryPosition,
            SimulationConstants.Level100TankFactoryContactRadius);
        CommitPlayerPosition(nextPosition);
    }

    private bool GoingIntoLevel100Water(SimVector2 velocity)
    {
        if (!_playerOnGround)
        {
            return false;
        }

        int currentGround = _playerGroundElevationMillimeters;
        int futureGround = Level100Terrain.Instance.SampleGroundElevationMillimeters(
            new SimVector2(
                PlayerPosition.X + velocity.X,
                PlayerPosition.Z + velocity.Z));
        int water = Level100Terrain.WaterElevationMillimeters;
        if (water - currentGround > 300)
        {
            return futureGround < currentGround;
        }

        return water - futureGround > 300;
    }

    private SimVector2 ResolveLevel100SteepSlope(SimVector2 velocity)
    {
        for (int iteration = 0; iteration < 6; iteration++)
        {
            var future = new SimVector2(
                PlayerPosition.X + velocity.X,
                PlayerPosition.Z + velocity.Z);
            SimVector2 gradient =
                Level100Terrain.Instance.SampleGroundGradientPermille(future);
            long gradientSquared =
                ((long)gradient.X * gradient.X) +
                ((long)gradient.Z * gradient.Z);
            if (gradientSquared <=
                SimulationConstants.Level100SteepSlopeGradientSquaredThreshold)
            {
                return velocity;
            }

            _playerOnSteepSlope = true;
            long uphill =
                ((long)velocity.X * gradient.X) +
                ((long)velocity.Z * gradient.Z);
            if (uphill <= 0)
            {
                return velocity;
            }

            velocity = new SimVector2(
                velocity.X - DivideRoundNearest(uphill * gradient.X, gradientSquared),
                velocity.Z - DivideRoundNearest(uphill * gradient.Z, gradientSquared));
        }

        return velocity;
    }

    private static SimVector2 ResolveLevel100WalkerContact(
        SimVector2 currentPosition,
        SimVector2 nextPosition,
        SimVector2 facilityPosition,
        int contactRadius)
    {
        long offsetX = (long)nextPosition.X - facilityPosition.X;
        long offsetZ = (long)nextPosition.Z - facilityPosition.Z;
        long radiusSquared = (long)contactRadius * contactRadius;
        long distanceSquared = (offsetX * offsetX) + (offsetZ * offsetZ);
        if (distanceSquared >= radiusSquared)
        {
            return nextPosition;
        }

        if (distanceSquared == 0)
        {
            offsetX = (long)currentPosition.X - facilityPosition.X;
            offsetZ = (long)currentPosition.Z - facilityPosition.Z;
            distanceSquared = (offsetX * offsetX) + (offsetZ * offsetZ);
            if (distanceSquared == 0)
            {
                return new SimVector2(facilityPosition.X + contactRadius, facilityPosition.Z);
            }
        }

        int distance = IntegerSquareRoot(distanceSquared);
        int resolvedX =
            facilityPosition.X + DivideRoundNearest(offsetX * contactRadius, distance);
        int resolvedZ =
            facilityPosition.Z + DivideRoundNearest(offsetZ * contactRadius, distance);
        while (true)
        {
            long resolvedOffsetX = (long)resolvedX - facilityPosition.X;
            long resolvedOffsetZ = (long)resolvedZ - facilityPosition.Z;
            if ((resolvedOffsetX * resolvedOffsetX) + (resolvedOffsetZ * resolvedOffsetZ) >=
                radiusSquared)
            {
                return new SimVector2(resolvedX, resolvedZ);
            }

            if (Math.Abs(offsetX) >= Math.Abs(offsetZ))
            {
                resolvedX += Math.Sign(offsetX);
            }
            else
            {
                resolvedZ += Math.Sign(offsetZ);
            }
        }
    }

    private void CommitPlayerPosition(SimVector2 nextPosition)
    {
        SimVector2 previousPosition = PlayerPosition;
        int previousGroundElevation = _playerGroundElevationMillimeters;
        SimVector2 velocity = new(
            nextPosition.X - previousPosition.X,
            nextPosition.Z - previousPosition.Z);
        _playerGroundElevationMillimeters =
            Level100Terrain.Instance.SampleGroundElevationMillimeters(nextPosition);
        _playerGroundDeltaMillimeters =
            _playerGroundElevationMillimeters - previousGroundElevation;
        int elevation =
            _playerGroundElevationMillimeters +
            Level100Terrain.WalkerCenterOfGravityMillimeters;
        bool wasInWater = _playerInWater;
        _playerOnGround = true;
        _playerInWater =
            elevation <=
            Level100Terrain.WaterElevationMillimeters +
                Level100Terrain.WalkerCenterOfGravityMillimeters;
        if (!wasInWater && _playerInWater)
        {
            EmitFlightEvent(AquilaFlightEvents.EnteredWater);
        }
        UpdateWaterFailureState(elevation);
        _ticksSinceGroundContact = 0;
        CommitLevel100PlayerPose(
            nextPosition,
            elevation,
            velocity,
            0);
    }

    private void CommitFlightMovement(
        int velocityX,
        int velocityZ,
        int verticalVelocity)
    {
        bool wasOnGround = _playerOnGround;
        bool wasInWater = _playerInWater;
        int previousGroundElevation = _playerGroundElevationMillimeters;
        SimVector2 playerPosition = PlayerPosition;
        var nextPosition = new SimVector2(
            playerPosition.X + velocityX,
            playerPosition.Z + velocityZ);
        int nextGround =
            Level100Terrain.Instance.SampleGroundElevationMillimeters(nextPosition);
        int nextElevation = PlayerElevationMillimeters + verticalVelocity;
        int minimumElevation =
            nextGround + Level100Terrain.WalkerCenterOfGravityMillimeters;
        bool onGround = nextElevation <= minimumElevation;
        if (onGround)
        {
            if (!wasOnGround)
            {
                TerrainGroundImpactResponse response = ResolveTerrainGroundImpact(
                    isWalkerState: _mode == VehicleMode.Walker &&
                        _transition == VehicleTransition.None,
                    isJetState: _mode == VehicleMode.Jet &&
                        _transition == VehicleTransition.None,
                    walkerDashActive: _walkerDashTicksRemaining != 0,
                    new SimVector3(velocityX, verticalVelocity, velocityZ),
                    Level100Terrain.Instance.SampleGroundGradientPermille(nextPosition));
                _groundImpactSpeedMillimetersPerTick =
                    response.SpeedMillimetersPerTick;
                velocityX = response.Velocity.X;
                verticalVelocity = response.Velocity.Y;
                velocityZ = response.Velocity.Z;
                if (response.DamageThresholdCrossed)
                {
                    EmitFlightEvent(
                        AquilaFlightEvents.GroundImpactDamageThresholdCrossed);
                }
                if (response.DamageMilliLife > 0 &&
                    ApplyLevel100PlayerDamage(
                        response.DamageMilliLife,
                        damageShields: false,
                        Level100PlayerDamageSource.GroundImpact,
                        out bool impactRequestsDeath))
                {
                    CompleteLevel100PlayerDamageDeath(impactRequestsDeath);
                }
            }

            nextElevation = minimumElevation;
            if (verticalVelocity < -37)
            {
                verticalVelocity = Math.Max(1, -verticalVelocity / 100);
            }
            else
            {
                verticalVelocity = 0;
            }
        }

        _playerGroundElevationMillimeters = nextGround;
        _playerGroundDeltaMillimeters = nextGround - previousGroundElevation;
        _playerOnGround = onGround;
        _playerInWater =
            nextElevation <=
            Level100Terrain.WaterElevationMillimeters +
                Level100Terrain.WalkerCenterOfGravityMillimeters;
        if (!wasOnGround && onGround)
        {
            EmitFlightEvent(AquilaFlightEvents.Touchdown);
        }
        if (!wasInWater && _playerInWater)
        {
            EmitFlightEvent(AquilaFlightEvents.EnteredWater);
        }
        UpdateWaterFailureState(nextElevation);
        _ticksSinceGroundContact = onGround
            ? 0
            : Math.Min(int.MaxValue, _ticksSinceGroundContact + 1);
        CommitLevel100PlayerPose(
            nextPosition,
            nextElevation,
            new SimVector2(velocityX, velocityZ),
            verticalVelocity);
    }

    /// <summary>
    /// Retail's water kill test, as a predicate on the committed player
    /// elevation.
    ///
    /// <para><c>references/Onslaught/BattleEngine.cpp:1249-1266</c>:</para>
    /// <code>
    /// float altitude = mPos.Z - MAP.GetWaterLevel();
    /// if ((altitude > -0.2f) &amp;&amp; ((mVulnerable) || (!CLIPARAMS.mDeveloperMode)))
    ///     StartDieProcess();
    /// </code>
    /// <para>Retail's Z axis points DOWN - proven inside the same family, at
    /// <c>actor.cpp:120-125</c>, where the ground clamp fires on
    /// <c>alt &lt;= mPos.Z</c> and then assigns <c>mPos.Z = alt</c> - so
    /// <c>altitude &gt; -0.2f</c> in retail's frame is
    /// <c>A &lt; waterAltitude + 0.2 m</c> in this one. The 0.2 m is
    /// <see cref="SimulationConstants.WaterFailureClearanceMillimeters"/> = 200.
    /// <c>mVulnerable || !developerMode</c> is unconditionally true in a retail
    /// build and is therefore not a divergence.</para>
    ///
    /// <para>Extracted only so it can be named and pinned; the call site below
    /// is unchanged. See <c>Level100FerryLandingTests</c> Gate C - loosening
    /// this is how every previous attempt at the Target Zone 4 ferry went
    /// wrong, and it is the one change that would fake every other gate in that
    /// file.</para>
    /// </summary>
    internal static bool WaterFailureAtElevation(int playerElevationMillimeters) =>
        playerElevationMillimeters <=
            Level100Terrain.WaterElevationMillimeters +
                SimulationConstants.WaterFailureClearanceMillimeters;

    private void UpdateWaterFailureState(int playerElevationMillimeters)
    {
        if (_playerWaterFailure ||
            !WaterFailureAtElevation(playerElevationMillimeters))
        {
            return;
        }

        _playerWaterFailure = true;
        EmitFlightEvent(AquilaFlightEvents.WaterFailureStarted);
        if (_level100Mission.ReportWaterLoss())
        {
            PumpLevel100EventBus();
        }
    }

    private void ApplyLevel100FlightLimits(
        ref int velocityX,
        ref int velocityZ,
        ref int verticalVelocity)
    {
        SimVector2 playerPosition = PlayerPosition;
        velocityX = ApplyMapEdgeVelocityLimit(
            playerPosition.X,
            velocityX,
            Level100Terrain.MinimumRelativeXMillimeters,
            Level100Terrain.MaximumRelativeXMillimeters);
        velocityZ = ApplyMapEdgeVelocityLimit(
            playerPosition.Z,
            velocityZ,
            Level100Terrain.MinimumRelativeZMillimeters,
            Level100Terrain.MaximumRelativeZMillimeters);
        if (PlayerElevationMillimeters >
            SimulationConstants.Level100MaximumElevationMillimeters &&
            verticalVelocity > 0)
        {
            verticalVelocity = 0;
        }
    }

    private static int ApplyMapEdgeVelocityLimit(
        int position,
        int velocity,
        int minimum,
        int maximum)
    {
        int outside = position < minimum
            ? minimum - position
            : position > maximum
                ? position - maximum
                : 0;
        bool movingOut =
            (position < minimum && velocity < 0) ||
            (position > maximum && velocity > 0);
        if (!movingOut)
        {
            return velocity;
        }

        int remaining = Math.Max(
            0,
            SimulationConstants.Level100MapEdgeSlowdownMillimeters - outside);
        return Retain(
            velocity,
            remaining,
            SimulationConstants.Level100MapEdgeSlowdownMillimeters);
    }

    private void UpdateWalkerFeet()
    {
        if (_mode != VehicleMode.Walker || _transition != VehicleTransition.None)
        {
            AlignWalkerFeetToNaturalTargets();
            return;
        }

        long playerDisplacementSquared =
            ((long)PlayerVelocity.X * PlayerVelocity.X) +
            ((long)PlayerVelocity.Z * PlayerVelocity.Z) +
            ((long)PlayerGroundDeltaMillimeters * PlayerGroundDeltaMillimeters);
        bool ownerMoved = playerDisplacementSquared > 10L * 10L;
        int threshold = ownerMoved
            ? SimulationConstants.WalkerFootMovingThresholdMillimeters
            : SimulationConstants.WalkerFootStationaryThresholdMillimeters;

        for (int footIndex = 0; footIndex < _walkerFeet.Count; footIndex++)
        {
            MutableWalkerFoot foot = _walkerFeet[footIndex];
            SimVector2 targetPosition = NaturalWalkerFootPosition(foot.StanceOffset);
            int targetGround =
                Level100Terrain.Instance.SampleGroundElevationMillimeters(targetPosition);
            int deltaX = targetPosition.X - foot.Position.X;
            int deltaZ = targetPosition.Z - foot.Position.Z;
            int deltaGround = targetGround - foot.GroundElevationMillimeters;

            if (foot.PhaseThirds == 0)
            {
                long distanceSquared =
                    ((long)deltaX * deltaX) +
                    ((long)deltaZ * deltaZ) +
                    ((long)deltaGround * deltaGround);
                if (distanceSquared > (long)threshold * threshold &&
                    CanBeginWalkerFootSwing(footIndex, distanceSquared, threshold))
                {
                    // Released CMCMech enters phase one without moving or
                    // lifting the planted point until its next update.
                    foot.PhaseThirds = 3;
                }
                continue;
            }

            foot.PhaseThirds +=
                SimulationConstants.WalkerFootPhaseUnitsPerSecond * 3 /
                SimulationConstants.TicksPerSecond;
            if (foot.PhaseThirds >=
                (SimulationConstants.WalkerFootPhaseEnd + 1) * 3)
            {
                foot.PhaseThirds = 0;
                foot.LiftMillimeters = 0;
                continue;
            }

            int phaseDenominator = SimulationConstants.WalkerFootPhaseEnd * 3;
            foot.Position = new SimVector2(
                foot.Position.X + DivideRoundNearest(
                    (long)deltaX * foot.PhaseThirds,
                    phaseDenominator),
                foot.Position.Z + DivideRoundNearest(
                    (long)deltaZ * foot.PhaseThirds,
                    phaseDenominator));
            foot.GroundElevationMillimeters =
                Level100Terrain.Instance.SampleGroundElevationMillimeters(foot.Position);
            int phaseAngleMicroRad = DivideRoundNearest(
                (long)foot.PhaseThirds * PiMicroRad,
                phaseDenominator);
            (int phaseSin, _) = FixedSinCos(phaseAngleMicroRad);
            foot.LiftMillimeters = Math.Max(
                0,
                DivideRoundNearest(
                    (long)phaseSin * SimulationConstants.WalkerFootLiftMillimeters,
                    FixedTrigScale));
        }
    }

    private bool CanBeginWalkerFootSwing(
        int footIndex,
        long distanceSquared,
        int threshold)
    {
        long forcedThreshold = threshold * 2L;
        if (distanceSquared > forcedThreshold * forcedThreshold)
        {
            return true;
        }

        int earlySwings = _walkerFeet.Count(foot =>
            foot.PhaseThirds > 0 &&
            foot.PhaseThirds <= (SimulationConstants.WalkerFootPhaseEnd / 2) * 3);
        if (earlySwings >= SimulationConstants.WalkerFootMaximumEarlySwings)
        {
            return false;
        }

        bool sameParityReady = false;
        bool predecessorClear = true;
        for (int otherIndex = 0; otherIndex < _walkerFeet.Count; otherIndex++)
        {
            int otherPhase = _walkerFeet[otherIndex].PhaseThirds;
            bool otherReady = otherPhase == 0 ||
                otherPhase > (SimulationConstants.WalkerFootPhaseEnd / 2) * 3;
            if (otherReady && otherIndex != footIndex)
            {
                if ((otherIndex & 1) == (footIndex & 1))
                {
                    sameParityReady = true;
                }
            }
            else if (otherIndex == footIndex - 1)
            {
                predecessorClear = false;
            }
        }

        return sameParityReady && predecessorClear;
    }

    private void BuildWalkerFeet()
    {
        _walkerFeet.Clear();
        for (int index = 0;
             index < SimulationConstants.WalkerFootStanceOffsetsMillimeters.Count;
             index++)
        {
            SimVector2 stance = SimulationConstants.WalkerFootStanceOffsetsMillimeters[index];
            SimVector2 position = NaturalWalkerFootPosition(stance);
            _walkerFeet.Add(new MutableWalkerFoot
            {
                Id = index,
                StanceOffset = stance,
                Position = position,
                GroundElevationMillimeters =
                    Level100Terrain.Instance.SampleGroundElevationMillimeters(position),
            });
        }
    }

    private void AlignWalkerFeetToNaturalTargets()
    {
        foreach (MutableWalkerFoot foot in _walkerFeet)
        {
            foot.Position = NaturalWalkerFootPosition(foot.StanceOffset);
            foot.GroundElevationMillimeters =
                Level100Terrain.Instance.SampleGroundElevationMillimeters(foot.Position);
            foot.PhaseThirds = 0;
            foot.LiftMillimeters = 0;
        }
    }

    private SimVector2 NaturalWalkerFootPosition(SimVector2 stanceOffset)
    {
        SimVector2 playerPosition = PlayerPosition;
        (int sin, int cos) = FixedSinCos(_facingYawMicroRad);
        return new SimVector2(
            playerPosition.X + DivideRoundNearest(
                ((long)stanceOffset.X * cos) - ((long)stanceOffset.Z * sin),
                FixedTrigScale),
            playerPosition.Z + DivideRoundNearest(
                ((long)stanceOffset.X * sin) + ((long)stanceOffset.Z * cos),
                FixedTrigScale));
    }

    private void UpdateLevel100TriggerActors()
    {
        DrainAndDispatchLevel100ActorFacts();

        foreach (Level100ActorSnapshot trigger in _level100Actors.Snapshot.Actors
            .Where(actor => actor.Trigger.HasValue && actor.Pose is not null)
            .OrderBy(actor => actor.ActorId.Value))
        {
            if (trigger.TriggerEventDispatched)
            {
                continue;
            }

            bool inJetMode = RetailIScriptInJetMode.Evaluate(
                _level100Actors.GetThingTypeMask(_level100PlayerActorId),
                _mode,
                _transition,
                _ticksSinceGroundContact);
            Level100MissionJetModeState jetModeState = inJetMode
                ? Level100MissionJetModeState.InJetMode
                : Level100MissionJetModeState.NotInJetMode;
            if (trigger.Active &&
                (!Level100MissionTiming.RequiresNotInJetMode(trigger.Trigger!.Value) ||
                 !inJetMode) &&
                IsWithinLevel100Trigger(
                    PlayerPosition,
                    PlayerElevationMillimeters,
                    trigger.Pose!.PositionMillimeters))
            {
                // The side actor owns its factual overlap and released pause;
                // LevelScript sees only the resulting named event. TriggerEntered
                // is not success — hit() Pause then PostEvent is.
                _level100Actors.BeginTriggerDispatch(
                    trigger.ActorId,
                    jetModeState);
                DrainAndDispatchLevel100ActorFacts();
            }
        }
    }

    // Level100ObjectiveTriggerRadius is a centre-to-centre *sphere* overlap
    // threshold (authored 5.0 plus the single-player CBattleEngine 0.4 that
    // CBattleEngine::GetRadius returns), and the released engine carries one
    // isotropic radius per thing -- CThing builds its own extent as
    // FVector(r, r, r) (references/Onslaught/thing.cpp:231-236, 519-520).
    // Testing it in the horizontal plane alone let a jet satisfy
    // "Reached Target Zone N" by passing 12 m overhead, which would make the
    // released landing lesson (HELP_RETRO) decorative: measured, all three of
    // beats 6, 8 and 10 completed at 11-13 m up in level flight, never landing.
    //
    // All five Level 100 trigger volumes are authored at retail Z = 0, and
    // released CThing::Init (0x004F34A0) raises an authored thing to the height
    // field sample -- see the private lab note STATIC-OBJECT-SEATING-2026-07-26.md.
    // Without that the test would be stricter than retail by the terrain height
    // under each volume: 898 mm at Target Zone 2, which costs 434 mm of
    // horizontal tolerance.
    //
    // Level100ActorRegistry.SeatOnGround now owns that seating for EVERY actor
    // class rather than for ground vehicles alone, so a volume arrives here
    // already seated and this line is idempotent on it -- the registry's seat
    // is max(authored, ground, water), and max(that, ground) is that. It is
    // kept because a pose can also arrive from a restored snapshot or from
    // SetPose, neither of which re-runs Init, and the containment test must not
    // depend on which of those produced the pose.
    private bool IsWithinLevel100Trigger(
        SimVector2 position,
        int elevationMillimeters,
        SimVector3 trigger)
    {
        var triggerHorizontal = new SimVector2(trigger.X, trigger.Z);
        int seatedTriggerY = Math.Max(
            trigger.Y,
            Level100Terrain.Instance.SampleGroundElevationMillimeters(
                triggerHorizontal));
        long deltaX = (long)position.X - trigger.X;
        long deltaY = (long)elevationMillimeters - seatedTriggerY;
        long deltaZ = (long)position.Z - trigger.Z;
        long radius = SimulationConstants.Level100ObjectiveTriggerRadius;
        return (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ) <=
            radius * radius;
    }

    private static SimVector2 ClampMagnitude(SimVector2 value, int maximum)
    {
        long magnitudeSquared = ((long)value.X * value.X) + ((long)value.Z * value.Z);
        long maximumSquared = (long)maximum * maximum;
        if (magnitudeSquared <= maximumSquared)
        {
            return value;
        }

        int magnitude = IntegerSquareRoot(magnitudeSquared);
        if ((long)magnitude * magnitude < magnitudeSquared)
        {
            magnitude++;
        }

        return new SimVector2(
            value.X * maximum / magnitude,
            value.Z * maximum / magnitude);
    }

    private static int IntegerSquareRoot(long value)
    {
        long low = 0;
        long high = Math.Min(value, int.MaxValue);
        long result = 0;
        while (low <= high)
        {
            long middle = low + ((high - low) / 2);
            long square = (long)middle * middle;
            if (square <= value)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return checked((int)result);
    }

    /// <summary>
    /// Full turn rounded to integer micro-radians. Keeps yaw in (−half, half].
    /// </summary>
    private const int TwoPiMicroRad = 6_283_185;

    private static int NormalizeMicroRad(int microRad)
    {
        int wrapped = microRad % TwoPiMicroRad;
        if (wrapped > TwoPiMicroRad / 2)
        {
            wrapped -= TwoPiMicroRad;
        }
        else if (wrapped <= -(TwoPiMicroRad / 2))
        {
            wrapped += TwoPiMicroRad;
        }

        return wrapped;
    }

    private void QuantizeFacingFromYaw()
    {
        // Eight-way snap from continuous yaw for FacingX/Z and fire aim.
        int yaw = NormalizeMicroRad(_facingYawMicroRad);
        int sector = (int)((long)((yaw + TwoPiMicroRad) % TwoPiMicroRad) * 8 / TwoPiMicroRad);
        (_facingX, _facingZ) = sector switch
        {
            0 => ((sbyte)0, (sbyte)1),
            1 => ((sbyte)1, (sbyte)1),
            2 => ((sbyte)1, (sbyte)0),
            3 => ((sbyte)1, (sbyte)-1),
            4 => ((sbyte)0, (sbyte)-1),
            5 => ((sbyte)-1, (sbyte)-1),
            6 => ((sbyte)-1, (sbyte)0),
            _ => ((sbyte)-1, (sbyte)1),
        };
    }

    private void UpdateResources(bool playerPartMoveStarted)
    {
        // The released outer CBattleEngine::Move does not a direct call to WalkerPart::Move
        // or JetPart::Move once IsDying() is true. Resource regeneration/drain
        // belongs to those part moves, so a pre-Move lethal hit skips it. A
        // lethal water skim occurs near the END of JetPart::Move, after its
        // energy work; preserve that already-started part update even though
        // the lifecycle has changed by the time this method commits it.
        if (!playerPartMoveStarted)
        {
            return;
        }

        if (!_jetMovedThisTick)
        {
            if (CanRechargeWalkerEnergy(_ticksSinceGroundContact))
            {
                // BattleEngineWalkerPart.cpp:376-388: a heat shot or charge this
                // update cleared mShieldsRecharging, which halves the recharge
                // (0x00413804); 0.05 / 2 is exactly 25 milli-units.
                int recharge = _level100PlayerWeapons.ShieldsRecharging
                    ? SimulationConstants.WalkerEnergyRegenerationPerTick
                    : SimulationConstants.WalkerEnergyRegenerationPerTick / 2;
                _energy = Math.Min(
                    SimulationConstants.MaximumEnergy,
                    _energy + recharge);
            }
            _level100PlayerWeapons.ResumeShieldsRecharging();
            if (_energy == SimulationConstants.MaximumEnergy)
            {
                _jetEnergyDrainRemainderMicro = 0;
            }
            _shield = _energy;
            return;
        }

        int previousEnergy = _energy;
        _energy = Math.Max(0, _energy - _jetEnergyDrainThisTick);
        if (previousEnergy > 0 && _energy == 0)
        {
            _jetEnergyDrainRemainderMicro = 0;
        }
        _shield = 0;
    }

    /// <summary>
    /// Released WalkerPart recharge gate. The pristine body compares elapsed
    /// time since ground contact strictly below 0.3 seconds.
    /// </summary>
    internal static bool CanRechargeWalkerEnergy(int ticksSinceGroundContact)
    {
        if (ticksSinceGroundContact < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksSinceGroundContact));
        }

        return ticksSinceGroundContact <
            SimulationConstants.WalkerRechargeGroundContactTicks;
    }

    private void UpdateAugmentState()
    {
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) !=
            Level100ActorLifecycle.Alive)
        {
            return;
        }

        // CBattleEngine::Move handles this after Damage has accumulated charge.
        // Aquila Prototype's primary Pulse Cannon Pod uses an available store,
        // so its AugmentWeapon gate succeeds when the released 10.0 threshold
        // is reached. Damage itself never flips the active flag.
        Level100PlayerDamageState advanced = Level100PlayerDamage.AdvanceAugment(
            new Level100PlayerDamageState(
                PlayerHull,
                _energy,
                _shield,
                _augmentCharge,
                _augmentActive));
        _augmentCharge = advanced.AugmentChargeMilli;
        _augmentActive = advanced.AugmentActive;
    }

    private bool ApplyLevel100PlayerDamage(
        int incomingDamageMilliLife,
        bool damageShields,
        Level100PlayerDamageSource source,
        out bool requestsDeath)
    {
        requestsDeath = false;
        if (_level100Actors.GetLifecycle(_level100PlayerActorId) !=
            Level100ActorLifecycle.Alive)
        {
            return false;
        }

        Level100PlayerDamageResult result = Level100PlayerDamage.Apply(
            new Level100PlayerDamageState(
                PlayerHull,
                _energy,
                _shield,
                _augmentCharge,
                _augmentActive),
            incomingDamageMilliLife,
            damageShields,
            isWalker: _mode == VehicleMode.Walker &&
                _transition == VehicleTransition.None);

        _energy = result.State.EnergyMilli;
        _shield = result.State.ShieldMilli;
        _augmentCharge = result.State.AugmentChargeMilli;
        _augmentActive = result.State.AugmentActive;
        _level100Destruction.SetExternalHealth(
            _level100PlayerActorId,
            Math.Max(0, result.State.LifeMilli));
        _playerDamageEvents.Add(new Level100PlayerDamageEvent(
            _tick,
            source,
            incomingDamageMilliLife,
            result.ShieldAbsorbedMilliLife,
            result.LifeDamageMilliLife,
            result.RequestsDeath));
        // CBattleEngine::Damage ends with AddShockShake of the life lost
        // times 0.125, halved while shields remain (0x0040ab75-0x0040abc2).
        _shake.Add(
            RetailBattleEngineShake.DamageAmount(
                result.LifeDamageMilliLife,
                result.State.ShieldMilli != 0),
            _level100ActorMechanics.NextReleasedRandom);
        requestsDeath = result.RequestsDeath;
        return true;
    }

    private void CompleteLevel100PlayerDamageDeath(bool requestsDeath)
    {
        if (!requestsDeath)
        {
            return;
        }

        DestroyLevel100PlayerActor();
        _level100Mission.ReportPlayerDeath();
    }

    private void TryFire(SimInput input)
    {
        if (!input.HasAction(SimActions.Fire) ||
            _transformTicksRemaining != 0)
        {
            return;
        }

        // CBattleEngine::FireWeapon (BattleEngine.cpp:1958-1970): record the
        // lock count the burst's targets round-robin over, then the part fires.
        _playerLocks.RecordFireWeapon();
        FireCurrentWeapon();
    }

    /// <summary>
    /// The part's <c>FireWeapon</c> then <c>CWeapon::Fire</c> (<c>0x00506010</c>):
    /// sample and clear charge, check readiness, stamp the reload from the
    /// burst's start, zero the Battle Engine's target cursor and spawn the
    /// first burst event.
    /// </summary>
    private void FireCurrentWeapon()
    {
        if (!_level100PlayerWeapons.TryPrepareFire(
            _mode, _transition, EngineTimeSeconds, out Level100ProjectileKind pulseRound))
        {
            return;
        }

        float now = EngineTimeSeconds;
        if (_mode == VehicleMode.Jet)
        {
            Level100MissionWeapon selected =
                _level100PlayerWeapons.GetCurrentWeapon(VehicleMode.Jet);
            if (selected == Level100MissionWeapon.MissilePod)
            {
                FireMissilePod();
                return;
            }
            if (selected != Level100MissionWeapon.MechVulcanCannon)
            {
                throw new InvalidOperationException(
                    $"Unsupported Level 100 jet weapon {selected}.");
            }

            _fireCooldownTicksRemaining = SimulationConstants.MechVulcanReloadTicks;
            _level100PlayerWeapons.StampReadyAt(selected, now);
            _playerLocks.ResetCurrentTarget();
            // Mech Vulcan Cannon fires two Mech Air Bullet rounds per event.
            // The two authored muzzle offsets remain unmodelled.
            SpawnPlayerBurst(
                selected,
                jetPart: true,
                SimulationConstants.MechVulcanVolleySize,
                Level100ProjectileKind.MechAirBullet,
                SimulationConstants.MechAirBulletSpeedPerTick,
                SimulationConstants.MechAirBulletLifetimeTicks,
                SimulationConstants.PlayerVulcanInaccuracyMicroRadians,
                MechVulcanPower,
                () => EmitFlightEvent(
                    AquilaFlightEvents.JetWeaponFireRequested,
                    AquilaJetWeapon.MechVulcanCannon));
            return;
        }

        Level100MissionWeapon walkerSelected =
            _level100PlayerWeapons.GetCurrentWeapon(VehicleMode.Walker);
        if (walkerSelected == Level100MissionWeapon.PulseCannonPod)
        {
            // Legacy countdown is a nominal-duration projection. Raw stored
            // per-weapon float time above owns the strict readiness decision.
            _fireCooldownTicksRemaining = pulseRound == Level100ProjectileKind.MechPulseBoltLarge
                ? (int)(Level100PulseCannonCharge.ChargedReloadTime * SimulationConstants.TicksPerSecond)
                : SimulationConstants.PulseCannonReloadTicks;
            _level100PlayerWeapons.StampReadyAt(walkerSelected, now, pulseRound);
            _playerLocks.ResetCurrentTarget();
            bool largePulse = pulseRound == Level100ProjectileKind.MechPulseBoltLarge;
            // `Mech Pulse Cannon Charged` carries no CWeaponVolleySize node, so
            // it takes the shipped default of 1 and one release is one round.
            // Both charge levels' first node is +0 CWeaponInaccuracy (Charged
            // @0x134E3, Charged 2 @0x135B3 in the pinned physics.dat); the
            // 0x3C0EFA35 once used here is `Mech Pulse Cannon` @0x13473, the
            // Small bolt's mode, which the Pulse Cannon Pod never selects. The
            // scatter block still draws twice. Large carries its own physical
            // scalars; its spatial blast, launch sound and impact presentation
            // remain open, so this is not complete parity.
            SpawnPlayerBurst(
                walkerSelected,
                jetPart: false,
                1,
                pulseRound,
                largePulse ? SimulationConstants.LargePulseSpeedPerTick :
                    SimulationConstants.ProjectileSpeedPerTick,
                largePulse ? SimulationConstants.LargePulseLifetimeTicks :
                    SimulationConstants.ProjectileLifetimeTicks,
                0,
                largePulse ? PulseChargedTwoPower : PulseChargedPower,
                null);
            return;
        }

        // The released script disables the Pulse Cannon Pod and enables the
        // Mech Twin Vulcan Cannon at the end of the first firing-range
        // exercise. Its weapon mode fires CWeaponVolleySize 4 Mech Bullet
        // rounds per CWeaponReloadTime 0.05 s. The four CWeaponLaunchSequence
        // muzzle entries remain unmodelled, so the volley leaves the one
        // evidenced BattleEngine emitter.
        if (walkerSelected != Level100MissionWeapon.MechTwinVulcanCannon)
        {
            throw new InvalidOperationException(
                $"Unsupported Level 100 walker weapon {walkerSelected}.");
        }

        _twinVulcanReloadTicksRemaining =
            SimulationConstants.TwinVulcanReloadTicks;
        _level100PlayerWeapons.StampReadyAt(walkerSelected, now);
        _playerLocks.ResetCurrentTarget();
        SpawnPlayerBurst(
            walkerSelected,
            jetPart: false,
            SimulationConstants.TwinVulcanVolleySize,
            Level100ProjectileKind.MechBullet,
            SimulationConstants.MechBulletSpeedPerTick,
            SimulationConstants.MechBulletLifetimeTicks,
            SimulationConstants.PlayerVulcanInaccuracyMicroRadians,
            TwinVulcanPower,
            null);
    }

    /// <summary>
    /// The emitter of a burst event's <paramref name="round"/>-th round, from
    /// the mode's launch sequence (<see cref="Level100CockpitEmitters"/>).
    /// </summary>
    internal static int LaunchGun(Level100MissionWeapon weapon, int round) => weapon switch
    {
        Level100MissionWeapon.PulseCannonPod => Level100CockpitEmitters.PulseGun,
        Level100MissionWeapon.MechTwinVulcanCannon =>
            Level100CockpitEmitters.TwinVulcanSequence[round % Level100CockpitEmitters.TwinVulcanSequence.Length],
        Level100MissionWeapon.MechVulcanCannon =>
            Level100CockpitEmitters.MechVulcanSequence[round % Level100CockpitEmitters.MechVulcanSequence.Length],
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    // CWeaponPower (mode +0x40, default 0 at 0x0042fb81) from the pinned
    // physics.dat: Mech Pulse Cannon Charged 0x3cf5c28f, Charged 2 0x3d4ccccd;
    // neither Vulcan mode carries the node.
    private static readonly float PulseChargedPower = BitConverter.UInt32BitsToSingle(0x3cf5c28fu);
    private static readonly float PulseChargedTwoPower = BitConverter.UInt32BitsToSingle(0x3d4ccccdu);
    private const float TwinVulcanPower = 0.0f;
    private const float MechVulcanPower = 0.0f;

    /// <summary>
    /// One burst event of <c>ProjectileBurst__SpawnFromCurrentPreset</c>
    /// (<c>0x005069f0</c>) for a Battle Engine weapon, in the RE lane's order
    /// (<c>reverse-engineering/contracts/render-platform/ProjectileBurst__SpawnFromCurrentPreset__005069f0.md</c>):
    /// the part's <c>WeaponFired</c> once, before the volley (a refusal ends the
    /// event with no round, draw, lock, recoil or sound); then per round the
    /// pitch and yaw scatter draws, the target from <c>GetCurrentTarget</c>,
    /// <c>FireLock</c> when this is the current weapon, the round's Actor Init
    /// draw, and <c>RecoilWeapon</c>'s shake draws.
    /// </summary>
    private void SpawnPlayerBurst(
        Level100MissionWeapon weapon,
        bool jetPart,
        int volleySize,
        Level100ProjectileKind kind,
        int speedPerTick,
        int lifetimeTicks,
        int inaccuracyMicroRadians,
        float power,
        Action? launchCue)
    {
        float now = EngineTimeSeconds;
        if (!_level100PlayerWeapons.WeaponFired(weapon, jetPart, now))
        {
            return;
        }

        launchCue?.Invoke();
        EmitWeaponFireEvent(PlayerWeaponIdentity(weapon), volleySize);
        for (int round = 0; round < volleySize; round++)
        {
            (int yawInaccuracy, int pitchInaccuracy) =
                _level100ActorMechanics.NextWeaponInaccuracy(inaccuracyMicroRadians);
            Level100ActorId? target = _playerLocks.GetCurrentTarget(now);
            if (weapon == RetailCurrentWeapon)
            {
                _playerLocks.FireLock(target, now);
            }
            // CRound::Init -> CActor::Init takes the round's Move-phase draw.
            _ = _level100ActorMechanics.NextReleasedRandom();
            LaunchWalkerRound(kind, speedPerTick, lifetimeTicks, yawInaccuracy, pitchInaccuracy,
                LaunchGun(weapon, round));
            _shake.Add(power, _level100ActorMechanics.NextReleasedRandom);
        }
    }

    private static Level100PlayerWeapon PlayerWeaponIdentity(Level100MissionWeapon weapon) => weapon switch
    {
        Level100MissionWeapon.PulseCannonPod => Level100PlayerWeapon.PulseCannonPod,
        Level100MissionWeapon.MechTwinVulcanCannon => Level100PlayerWeapon.MechTwinVulcanCannon,
        Level100MissionWeapon.MechVulcanCannon => Level100PlayerWeapon.MechVulcanCannon,
        _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
    };

    private void TryChargeWeapon(SimInput input)
    {
        if (!input.HasAction(SimActions.ChargeWeapon) ||
            _transformTicksRemaining != 0)
        {
            return;
        }

        // Row 10 precedes release row 11. Both part wrappers call FireWeapon
        // while a ready non-chargeable weapon is held. Pulse instead charges.
        if (_level100PlayerWeapons.AdvanceCharge(_mode, _transition, EngineTimeSeconds))
        {
            FireCurrentWeapon();
        }
    }

    private void TryChangeWeapon(SimInput input)
    {
        if (!input.HasAction(SimActions.ChangeWeapon) ||
            _transformTicksRemaining != 0)
        {
            return;
        }

        // CController's released default table dispatches Fire before Change
        // Weapon (rows 11 and 12), which is why this runs after TryFire when a
        // synthetic input carries both edges. CBattleEngine::ChangeWeapon then
        // refuses configurations with one or zero active weapons and delegates
        // the cycle to the current vehicle part.
        _ = _level100PlayerWeapons.SelectNextActive(_mode);
    }

    /// <summary>
    /// Emits one round from the evidenced cockpit emitter along the current
    /// aim. Speed, lifetime, contact radius and damage are the round's, not
    /// the pulse's.
    /// </summary>
    private void LaunchWalkerRound(
        Level100ProjectileKind kind,
        int speedPerTick,
        int lifetimeTicks,
        int yawInaccuracyMicroRadians,
        int pitchInaccuracyMicroRadians,
        int gun,
        Level100ActorId? seekTarget = null,
        uint launchTimeBits = 0,
        (int YawMicroRad, int PitchMicroRad)? launchAngle = null)
    {
        // Retail samples the cockpit's Gun emitter before correcting its
        // launch orientation. CBattleEngine::GetLaunchPosition (0x0040c990,
        // BattleEngine.cpp:3000-3069) takes the emitter's world pose, p = M·p
        // + P, with M the body orientation (+0x3c, roll included) and P the
        // Battle Engine's position, then reuses the distance the last
        // crosshair refresh (event 6002) retained and rotates this emitter
        // toward that point on the current view line. Inaccuracy rotates the
        // corrected direction, never the emitter.
        SimVector3 emitter = CockpitEmitterWorldPosition(
            Level100CockpitEmitters.Gun(gun, walkPose: _mode == VehicleMode.Walker));
        (int baseYaw, int basePitch) = ReticleAdjustedLaunchAngles(emitter);
        (int launchYaw, int launchPitch) = Level100ActorMechanics.ComposeLaunchDirection(
            baseYaw,
            basePitch,
            launchAngle ?? Level100ActorMechanics.DefaultLaunchAngle,
            (yawInaccuracyMicroRadians, pitchInaccuracyMicroRadians));
        (int launchSin, int launchCos) = FixedSinCos(launchYaw);
        (int launchPitchSin, int launchPitchCos) = FixedSinCos(launchPitch);
        int horizontalSpeed = DivideRoundNearest(
            (long)launchPitchCos * speedPerTick,
            FixedTrigScale);
        int velocityX = DivideRoundNearest(
            -(long)launchSin * horizontalSpeed,
            FixedTrigScale);
        int velocityZ = DivideRoundNearest(
            (long)launchCos * horizontalSpeed,
            FixedTrigScale);
        int verticalVelocity = DivideRoundNearest(
            -(long)launchPitchSin * speedPerTick,
            FixedTrigScale);

        int id = _nextProjectileId++;
        _projectiles.Add(new MutableProjectile
        {
            Id = id,
            Kind = kind,
            Position = new SimVector2(emitter.X, emitter.Z),
            Velocity = new SimVector2(velocityX, velocityZ),
            ElevationMillimeters = emitter.Y,
            VerticalVelocityMillimetersPerTick = verticalVelocity,
            RemainingTicks = lifetimeTicks,
            YawMicroRad = launchYaw,
            PitchMicroRad = launchPitch,
            LaunchTimeBits = launchTimeBits,
            SeekTarget = seekTarget,
        });
        FilePlayerRoundEvents(id, lifetimeTicks);
    }

    /// <summary>
    /// <c>CRound::Init</c>'s MOVE and life events for a Battle Engine round,
    /// on the level's one event manager. Every player round's life span is a
    /// whole number of seconds, so ticks / 20 is its exact float.
    /// </summary>
    private void FilePlayerRoundEvents(int roundId, int lifetimeTicks) =>
        _level100ActorMechanics.FileRoundEvents(
            Level100ActorMechanics.PlayerRoundListener(roundId),
            lifetimeTicks / (float)SimulationConstants.TicksPerSecond);

    /// <summary>
    /// A cockpit emitter's world position: the Battle Engine's position plus
    /// the body orientation times the emitter's model position. Retail's model
    /// z points down, so it enters Core's up axis negated. The cockpit tilt S
    /// and the render-fraction lerp of the pose are open (the RE lane's aiming
    /// contract), so this is the current pose with S the identity.
    /// </summary>
    private SimVector3 CockpitEmitterWorldPosition(Level100CockpitEmitters.Emitter model)
    {
        FixedBodyBasis body = GetBodyBasis();
        long Axis(int right, int forward, int up) =>
            ((long)model.XMicrometres * right) +
            ((long)model.YMicrometres * forward) -
            ((long)model.ZMicrometres * up);
        const long Scale = (long)FixedTrigScale * 1000;
        SimVector2 position = PlayerPosition;
        return new SimVector3(
            checked(position.X + DivideRoundNearest(Axis(body.RightX, body.ForwardX, body.UpX), Scale)),
            checked(PlayerElevationMillimeters + DivideRoundNearest(Axis(body.RightY, body.ForwardY, body.UpY), Scale)),
            checked(position.Z + DivideRoundNearest(Axis(body.RightZ, body.ForwardZ, body.UpZ), Scale)));
    }

    /// <summary>
    /// <c>CBattleEngine::GetLaunchPosition</c> (<c>0x0040c990</c>,
    /// <c>BattleEngine.cpp:3016-3067</c>) for a gravity-free weapon that
    /// adjusts its aim: with a retained crosshair report the round is aimed
    /// at the point the current view line reaches at that report's distance
    /// (<c>mWlcr.mDistToImpact</c>, written by the last 6002 refresh, not a
    /// fresh trace); without one it keeps <c>mOrientation</c> times the
    /// auto-aim matrix.
    /// </summary>
    private (int YawMicroRad, int PitchMicroRad) ReticleAdjustedLaunchAngles(
        SimVector3 emitter)
    {
        if (_crosshairHitKind == Level100CrosshairHitKind.Nothing)
        {
            return (_facingYawMicroRad, _facingPitchMicroRad);
        }

        const long LineLengthMillimeters = 1_000_000;
        (SimVector3 start, SimVector3 end) = CrosshairLine();
        long distance = _crosshairHitDistanceMillimeters;
        var contact = new SimVector3(
            checked(start.X + DivideRoundNearest((end.X - (long)start.X) * distance, LineLengthMillimeters)),
            checked(start.Y + DivideRoundNearest((end.Y - (long)start.Y) * distance, LineLengthMillimeters)),
            checked(start.Z + DivideRoundNearest((end.Z - (long)start.Z) * distance, LineLengthMillimeters)));
        int deltaX = checked(contact.X - emitter.X);
        int deltaY = checked(contact.Y - emitter.Y);
        int deltaZ = checked(contact.Z - emitter.Z);
        int horizontal = Magnitude2D(deltaX, deltaZ);
        if (horizontal == 0 && deltaY == 0)
        {
            return (_facingYawMicroRad, _facingPitchMicroRad);
        }

        return (
            FixedAtan2(-deltaX, deltaZ),
            FixedAtan2(-deltaY, Math.Max(1, horizontal)));
    }

    /// <summary>
    /// A Battle Engine round's MOVE or life event, delivered in the level
    /// event manager's flush (the RE lane's round Frames contract). The life
    /// event comes first in its frame: a <c>CRoundExplode</c> round (the Micro
    /// Missile) bursts in the air where it is (<c>0x004d9a54</c> →
    /// <c>0x004d9f30</c>), and every round starts dying. The MOVE delivered
    /// after it still takes one last step and is not re-filed, so a round
    /// makes k + 1 Moves; that step can meet things but hits nothing.
    /// </summary>
    private void HandlePlayerRoundEvent(RetailEventScheduler events, RetailEventDispatch dispatch)
    {
        int roundId = Level100ActorMechanics.PlayerRoundId(dispatch.Listener);
        MutableProjectile projectile = _projectiles.Find(round => round.Id == roundId) ??
            throw new InvalidOperationException($"Round {roundId} has an event but no round.");
        switch (dispatch.EventNum)
        {
            case Level100ActorMechanics.RoundLifeEvent:
                if (projectile.Kind == Level100ProjectileKind.MicroMissile)
                {
                    _level100Destruction.ReportAirBurst(
                        new SimVector3(
                            projectile.Position.X,
                            projectile.ElevationMillimeters,
                            projectile.Position.Z),
                        Level100DestructionEffectKind.MicroMissileImpact);
                }
                projectile.Dying = true;
                return;
            case Level100ActorMechanics.RoundMoveEvent:
                if (MovePlayerRound(projectile) || projectile.Dying)
                {
                    // CRound::Shutdown calls the owning Battle Engine's LockHit
                    // for the bound target (0x004d8e00).
                    if (projectile.Seeks)
                    {
                        _playerLocks.LockHit(projectile.SeekTarget);
                    }
                    _projectiles.Remove(projectile);
                    events.ClearListenerEvents(dispatch.Listener);
                    return;
                }

                Level100ActorMechanics.RefileRoundMove(events, dispatch);
                return;
            default:
                throw new InvalidOperationException($"Unadmitted round event {dispatch.EventNum}.");
        }
    }

    /// <summary>
    /// One <c>CRound::Move</c> of a Battle Engine round: a seeking round's two
    /// wiggle draws and guidance, the step, and its contact sweep. Returns
    /// whether the round hit.
    /// </summary>
    private bool MovePlayerRound(MutableProjectile projectile)
    {
        if (projectile.Seeks)
        {
            // CRound::Move takes its two wiggle draws first, then guides;
            // the wiggle bends only this step's travel (the actor rounds'
            // shared law).
            (int wiggleYaw, int wigglePitch) =
                _level100ActorMechanics.NextWiggle(Level100MissilePod.WiggleMicroRadians);
            SteerSeekingPlayerRound(projectile);
            (SimVector2 velocity, int vertical) = VelocityFromAngles(
                NormalizeMicroRad(projectile.YawMicroRad + wiggleYaw),
                NormalizeMicroRad(projectile.PitchMicroRad + wigglePitch),
                Level100MissilePod.SpeedMillimetersPerTick);
            projectile.Velocity = velocity;
            projectile.VerticalVelocityMillimetersPerTick = vertical;
        }

        var start = new SimVector3(
            projectile.Position.X,
            projectile.ElevationMillimeters,
            projectile.Position.Z);
        var end = new SimVector3(
            checked(projectile.Position.X + projectile.Velocity.X),
            checked(projectile.ElevationMillimeters +
                projectile.VerticalVelocityMillimetersPerTick),
            checked(projectile.Position.Z + projectile.Velocity.Z));
        projectile.Position = new SimVector2(end.X, end.Z);
        projectile.ElevationMillimeters = end.Y;
        projectile.RemainingTicks--;

        Level100DestructionEffectKind impactEffectKind = projectile.Kind switch
        {
            Level100ProjectileKind.MechPulseBoltMedium =>
                Level100DestructionEffectKind.PulseImpact,
            Level100ProjectileKind.MechPulseBoltLarge =>
                // Retained Medium presentation placeholder; Large's
                // actual effect/audio assets are not admitted here.
                Level100DestructionEffectKind.PulseImpact,
            Level100ProjectileKind.MechBullet or
                Level100ProjectileKind.MechAirBullet =>
                Level100DestructionEffectKind.VulcanImpact,
            Level100ProjectileKind.MicroMissile =>
                Level100DestructionEffectKind.MicroMissileImpact,
            _ => throw new InvalidDataException(
                $"Projectile {projectile.Id} has unsupported impact kind " +
                $"{projectile.Kind}."),
        };
        if (projectile.Dying)
        {
            // A dying round still meets things, but its own Hit (slot 39,
            // 0x004d8ae0) returns at 0x004d8af7 while the dying bit is set: no
            // damage, no impact explosion, no death. Only the struck thing's
            // script hit notification remains (the RE lane's "No hit while
            // dying").
            if (_level100Destruction.TryReportDyingRoundContact(start, end, projectile.ContactRadiusMillimeters))
            {
                DrainAndDispatchLevel100ActorFacts();
            }
            return false;
        }

        // Only Medium retains the older fixed second damage stage.
        // Large applies its configured direct amount through the common
        // sweep; a spatial blast cannot be replaced with another fixed 4.
        bool hit = projectile.Kind == Level100ProjectileKind.MechPulseBoltMedium
            ? _level100Destruction.TryApplyPulseSweep(start, end, out _, _retailEventFrameCount)
            : _level100Destruction.TryApplyRoundSweep(
                start,
                end,
                projectile.ContactRadiusMillimeters,
                projectile.DamageBits,
                impactEffectKind,
                out _,
                _retailEventFrameCount);
        if (hit)
        {
            DrainAndDispatchLevel100ActorFacts();
        }

        return hit;
    }

    /// <summary>
    /// A round's per-tick displacement along (yaw, pitch), in the same fixed
    /// arithmetic <see cref="LaunchWalkerRound"/> uses.
    /// </summary>
    private static (SimVector2 Velocity, int Vertical) VelocityFromAngles(
        int yawMicroRad,
        int pitchMicroRad,
        int speedPerTick)
    {
        (int sin, int cos) = FixedSinCos(yawMicroRad);
        (int pitchSin, int pitchCos) = FixedSinCos(pitchMicroRad);
        int horizontal = DivideRoundNearest((long)pitchCos * speedPerTick, FixedTrigScale);
        return (
            new SimVector2(
                DivideRoundNearest(-(long)sin * horizontal, FixedTrigScale),
                DivideRoundNearest((long)cos * horizontal, FixedTrigScale)),
            DivideRoundNearest(-(long)pitchSin * speedPerTick, FixedTrigScale));
    }

    private void ResetDynamicState()
    {
        _retailEventFrameCount = 0;
        _nextProjectileId = 1;
        _mode = VehicleMode.Walker;
        _transition = VehicleTransition.None;
        (SimVector2 initialPosition, int startYawMicroRad) = PlayerStart();
        _playerGroundElevationMillimeters =
            Level100Terrain.Instance.SampleGroundElevationMillimeters(initialPosition);
        _playerGroundDeltaMillimeters = 0;
        _playerOnGround = true;
        _playerInWater =
            _playerGroundElevationMillimeters <= Level100Terrain.WaterElevationMillimeters;
        _playerWaterFailure = false;
        _playerOnSteepSlope = false;
        _landingJetsActive = false;
        _groundImpactSpeedMillimetersPerTick = 0;
        _flightEvents.Clear();
        _weaponFireEvents.Clear();
        _playerDamageEvents.Clear();
        _damageFlashes.Clear();
        _facingYawMicroRad = startYawMicroRad;
        QuantizeFacingFromYaw();
        _walkerYawVelocityMicroRadPerTick = 0;
        _facingPitchMicroRad = 0;
        _walkerPitchVelocityMicroRadPerTick = 0;
        _bodyRollMicroRad = 0;
        _rollVelocityMicroRadPerTick = 0;
        _walkerLastMoveXPermille = 0;
        _walkerLastMoveZPermille = 0;
        _walkerLastHardLeftTimeBits = SimulationConstants.WalkerDashInitialHistoryFloatBits;
        _walkerLastHardRightTimeBits = SimulationConstants.WalkerDashInitialHistoryFloatBits;
        _walkerLastHardForwardTimeBits = SimulationConstants.WalkerDashInitialHistoryFloatBits;
        _walkerLastHardBackwardTimeBits = SimulationConstants.WalkerDashInitialHistoryFloatBits;
        _walkerDashTicksRemaining = 0;
        _walkerSoundTravelMillimeters = 0;
        _walkerSoundRolloverCount = 0;
        _zoomPermille = SimulationConstants.ZoomOutPermille;
        _desiredZoomPermille = SimulationConstants.ZoomOutPermille;
        _energy = SimulationConstants.MaximumEnergy;
        _shield = SimulationConstants.MaximumShield;
        _augmentCharge = 0;
        _augmentActive = false;
        _transformTicksRemaining = 0;
        _fireCooldownTicksRemaining = 0;
        _twinVulcanReloadTicksRemaining = 0;
        // The pan starts when the pre-run ends (RunPreRun).
        _level100OpeningTicksRemaining = 0;
        // A configured weapon starts ACTIVE.
        //
        // CORRECTION, recorded here because a commit message cannot be edited:
        // 424483da argued this from the BINARY and said +0x9c "is exactly what
        // the Enable (0x004fe2b0) and Disable (0x004fe310) list-walkers write".
        // That call path is WRONG and was refuted on 2026-07-26. Those two are
        // shared Unit-family virtuals over this+0x17C and are not the player's
        // path at all; Battle Engine script calls route through 0x0040DC30 /
        // 0x0040DC60 to the part-specific writers at 0x004127A0 / 0x00412830
        // (jet) and 0x00414970 / 0x00414A40 (walker). So there are at least SIX
        // direct writers, not two, plus configuration reset as a further
        // semantic path. Do not cite that commit's addresses.
        //
        // The CONCLUSION survives untouched, because it rests on the source and
        // the shipped scripts below rather than on that call path. But it is
        // supported STATICALLY, not behaviourally: no copied-retail watchpoint
        // on +0x9c from construction through beat 7 exists, and no capture shows
        // a Mech Vulcan round fired with no prior script enable. Until one does,
        // do not restate this as observed retail behaviour.
        //
        // `CBattleEngineJetPart::
        // ResetConfiguration` and `CBattleEngineWalkerPart::ResetConfiguration`
        // spawn one CWeapon per name in the configuration's weapon list, call
        // `weapon->Init(init)` and append it; neither ever calls
        // `SetActive(FALSE)`, and `EnableWeapon`/`DisableWeapon` only flip that
        // flag on a weapon that is already owned
        // (references/Onslaught/BattleEngineJetPart.cpp:976-1044,
        // BattleEngineWalkerPart.cpp:1030-1065).
        //
        // The shipped Level 100 object proves the same thing on its own, and
        // more sharply. Across all 25 hash-pinned compiled scripts,
        // `Mech Vulcan Cannon` and `Missile Pod` are named exactly once each -
        // in `DisableWeapon` inside `event("Abort Airborne Drones")` - and are
        // never enabled anywhere; and `init()` opens by *disabling*
        // `Mech Twin Vulcan Cannon` and `Pulse Cannon Pod`. Under a
        // start-disabled model every one of those four instructions is dead
        // code and beat 7 is unwinnable, because the script disables both
        // walker weapons at the end of beat 5 and the jet's Mech Vulcan Cannon
        // is the only weapon the player has for the first drone wave. Under
        // start-enabled every one of them is necessary. Aquila Prototype ships
        // walker `["Mech Twin Vulcan Cannon"]`, mPrimaryWeapon
        // `"Pulse Cannon Pod"` and jet `["Mech Vulcan Cannon", "Missile Pod"]`
        // (battle engine configurations.dat @0x2d2), so all four are owned.
        //
        // Measured before this change: a `SimInput`-only autopilot that reached
        // beat 7 held Fire in jet mode with `Level100MechVulcanCannonEnabled`
        // false for 570 released seconds and launched no round, because
        // `TryFire` returns on that flag. Flight mode gets the same treatment
        // for the same reason; the released `init()` disables it explicitly on
        // the first tick, so the observable start state is unchanged there.
        _level100FlightEnabled = true;
        _flightModeFlag = RetailEnableFlightMode.FlagDisabled;
        _level100PlayerWeapons.ResetConfiguration();
        _level100HudEmphasisMask = 0;
        _walkerToJetUsesTakeoffLift = false;
        _walkerToJetLiftApplied = false;
        _ticksSinceGroundContact = 0;
        _jetTicksSinceTransform = 0;
        _jetStrafeTicksRemaining = 0;
        _jetStrafeAccelerationRemainder = 0;
        _jetEnergyDrainRemainderMicro = 0;
        _jetThrusterPermille = 500;
        _jetGroundedSlowTicks = 0;
        _jetStallTicks = 0;
        _jetEnergyDrainThisTick = 0;
        _jetMovedThisTick = false;
        _projectiles.Clear();
        _level100Actors = new Level100ActorRegistry(_level100ActorDefinitions, _lostBaseRows);
        // The Battle Engine is built inline by level-world row 0, so its
        // 6002/6003 draws come at that point of the load, after the base world.
        ResetBattleEngineTargeting();
        _level100ActorMechanics = new Level100ActorMechanics(
            _level100Actors,
            _level100ActorDefinitions,
            InitializeBattleEngineRefreshEvents);
        _level100Destruction = new Level100DestructionRuntime(_level100Actors);
        _level100PlayerActorId = _level100Actors.GetThingRef("Player 1") ??
            throw new InvalidOperationException("Level 100 Player is missing.");
        _level100Actors.SetHealth(_level100PlayerActorId, SimulationConstants.MaximumHull);
        _level100Actors.Activate(_level100PlayerActorId);
        CommitLevel100PlayerPose(
            initialPosition,
            _playerGroundElevationMillimeters +
                Level100Terrain.WalkerCenterOfGravityMillimeters,
            SimVector2.Zero,
            0,
            transitionPose: false);
        BuildWalkerFeet();
        _level100ActorScripts = new Level100ActorScriptRuntime(
            _level100Actors,
            _level100PlayerActorId,
            RegisterSpawnedLevel100Actor,
            () => BitConverter.SingleToInt32Bits(EngineTimeSeconds));
        // Scripts are bound now and run their init() when the pre-run's first
        // flush delivers each INIT_SCRIPT the load filed.
        _level100ActorScripts.SharedRandom = _level100ActorMechanics.NextSharedRandom;
        _level100ActorScripts.RequestMessage = (speaker, message, waits) =>
            _level100Mission.RequestScriptMessage(speaker, message, waits);
        _level100ActorScripts.AttachReleasedScripts(_level100ActorMechanics.FileScriptInit);
        _level100MissionEvents.Clear();
        _level100ActorScriptCommands.Clear();
        _level100Mission = new Level100Mission(
            _level100Actors,
            _level100PlayerActorId,
            _level100TutorialProgress,
            PlayerHull,
            _worldNumber,
            runInit: false);
        SyncLevel100PlayerState();
        PumpLevel100EventBus();
        LoadSnapshotForMeasurement = CreateSnapshot();
        RunPreRun();
    }

    /// <summary>
    /// The state at the end of the load, before the pre-run's frames: an
    /// internal measurement receipt for construction tests, not snapshot,
    /// replay or state-hash material.
    /// </summary>
    internal WorldSnapshot? LoadSnapshotForMeasurement { get; private set; }

    /// <summary>
    /// Where the Battle Engine starts: Level 100's Start is Core's origin with
    /// its authored yaw; another world's is its Start row (Player 1's
    /// authored position, on the ground as <c>CStart::Init</c>'s clamp puts it)
    /// and that row's yaw, in the same datum.
    /// </summary>
    private (SimVector2 Position, int YawMicroRad) PlayerStart()
    {
        if (_worldNumber == Level100MissionProgram.WorldNumber100)
        {
            return (SimVector2.Zero, SimulationConstants.Level100PlayerStartYawMicroRad);
        }

        Level100ActorDefinition start = _level100ActorDefinitions.Actors.SingleOrDefault(actor => actor.Name == "Player 1") ??
            throw new NotSupportedException($"World {_worldNumber}'s definitions have no Start (Player 1).");
        float yaw = BitConverter.Int32BitsToSingle(start.AuthoredTransform.RetailEulerFloatBits.X);
        return (
            new SimVector2(start.InitialPose.PositionMillimeters.X, start.InitialPose.PositionMillimeters.Z),
            checked((int)Math.Round(yaw * 1_000_000.0, MidpointRounding.AwayFromZero)));
    }

    /// <summary>
    /// <c>CGame::PreRun</c> (<c>game.cpp:2063-2071</c>): whole updates, with
    /// nothing rendered and no player input, from frame 1 until frame 60's
    /// flush delivers FINISHED_PRE_RUN (filed at now + 3.0 before the load,
    /// <c>game.cpp:371-373</c>) and <c>CGame::StartPanState</c> starts the pan.
    /// Level construction includes them, so tick 0 is retail frame 60 and
    /// the first tick's update is frame 61. What the frames report (mission
    /// events, script commands, flight and weapon logs) is kept for the first
    /// tick, as retail shows it once the visuals start.
    /// </summary>
    private void RunPreRun()
    {
        var missionEvents = new List<Level100MissionEvent>(_level100MissionEvents);
        var scriptCommands = new List<Level100ActorScriptCommand>(_level100ActorScriptCommands);
        for (int frame = 0; frame < SimulationConstants.Level100PreRunTicks; frame++)
        {
            AdvanceFrame(SimInput.Idle, level100Facts: null);
            missionEvents.AddRange(_level100MissionEvents);
            scriptCommands.AddRange(_level100ActorScriptCommands);
        }

        _level100MissionEvents.Clear();
        _level100MissionEvents.AddRange(missionEvents);
        _level100ActorScriptCommands.Clear();
        _level100ActorScriptCommands.AddRange(scriptCommands);
        // FINISHED_PRE_RUN -> CGame::StartPanState: FINISHED_PANNING files at
        // now + the world's pan length, so the pan ends on frame 60 + its ticks.
        _level100OpeningTicksRemaining = SimulationConstants.OpeningPanTicks(_worldNumber);
    }

    private WorldSnapshot CreateSnapshot()
    {
        Level100ActorSnapshot player = _level100Actors.GetActor(_level100PlayerActorId);
        SimVector2 playerPosition = new(
            player.Pose.PositionMillimeters.X,
            player.Pose.PositionMillimeters.Z);
        SimVector2 playerVelocity = new(
            player.Pose.LinearVelocityMillimetersPerTick.X,
            player.Pose.LinearVelocityMillimetersPerTick.Z);
        ProjectileSnapshot[] projectiles = _projectiles
            .OrderBy(projectile => projectile.Id)
            .Select(projectile => new ProjectileSnapshot(
                projectile.Id,
                projectile.Kind,
                projectile.Position,
                projectile.Velocity,
                projectile.ElevationMillimeters,
                projectile.VerticalVelocityMillimetersPerTick,
                projectile.RemainingTicks)
            {
                Seeking = projectile.Seeks
                    ? new Level100SeekingRoundSnapshot(
                        projectile.YawMicroRad,
                        projectile.PitchMicroRad,
                        projectile.LaunchTimeBits,
                        projectile.SeekTarget)
                    : null,
            })
            .ToArray();
        WalkerFootContactSnapshot[] walkerFeet = _walkerFeet
            .OrderBy(foot => foot.Id)
            .Select(foot => new WalkerFootContactSnapshot(
                foot.Id,
                foot.Position,
                foot.GroundElevationMillimeters,
                foot.PhaseThirds,
                foot.LiftMillimeters))
            .ToArray();
        return new WorldSnapshot(
            _tick,
            _seed,
            _level100TutorialProgress,
            _mode,
            _transition,
            playerPosition,
            playerVelocity,
            _playerGroundElevationMillimeters,
            _playerGroundDeltaMillimeters,
            player.Pose.PositionMillimeters.Y,
            player.Pose.LinearVelocityMillimetersPerTick.Y,
            _playerOnGround,
            _playerInWater,
            _playerWaterFailure,
            _playerOnSteepSlope,
            _landingJetsActive,
            _groundImpactSpeedMillimetersPerTick,
            Array.AsReadOnly(_flightEvents.ToArray()),
            _facingX,
            _facingZ,
            _facingYawMicroRad,
            _walkerYawVelocityMicroRadPerTick,
            _facingPitchMicroRad,
            _walkerPitchVelocityMicroRadPerTick,
            _bodyRollMicroRad,
            _rollVelocityMicroRadPerTick,
            _walkerLastMoveXPermille,
            _walkerLastMoveZPermille,
            _walkerLastHardLeftTimeBits,
            _walkerLastHardRightTimeBits,
            _walkerLastHardForwardTimeBits,
            _walkerLastHardBackwardTimeBits,
            _walkerDashTicksRemaining,
            _walkerSoundTravelMillimeters,
            _walkerSoundRolloverCount,
            _energy,
            _shield,
            player.Health,
            _augmentCharge,
            _augmentActive,
            Array.AsReadOnly(_playerDamageEvents.ToArray()),
            Array.AsReadOnly(_damageFlashes.ToArray()),
            _transformTicksRemaining,
            _walkerToJetUsesTakeoffLift,
            _walkerToJetLiftApplied,
            _ticksSinceGroundContact,
            _jetTicksSinceTransform,
            _jetStrafeTicksRemaining,
            _jetStrafeAccelerationRemainder,
            _jetEnergyDrainRemainderMicro,
            _jetThrusterPermille,
            _jetGroundedSlowTicks,
            _jetStallTicks,
            _fireCooldownTicksRemaining,
            _twinVulcanReloadTicksRemaining,
            _level100OpeningTicksRemaining,
            player.Active,
            _level100FlightEnabled,
            _level100PlayerWeapons.IsActive(Level100MissionWeapon.PulseCannonPod),
            _level100PlayerWeapons.IsActive(Level100MissionWeapon.MechTwinVulcanCannon),
            _level100PlayerWeapons.IsActive(Level100MissionWeapon.MechVulcanCannon),
            _level100PlayerWeapons.IsActive(Level100MissionWeapon.MissilePod),
            _level100PlayerWeapons.WalkerSelectedWeapon,
            _level100PlayerWeapons.JetSelectedWeapon,
            _zoomPermille,
            _desiredZoomPermille,
            _level100HudEmphasisMask,
            _level100Mission.Snapshot,
            Array.AsReadOnly(_level100MissionEvents.ToArray()),
            _level100Actors.Snapshot,
            _level100Destruction.Snapshot,
            _level100Destruction.Events,
            Array.AsReadOnly(_weaponFireEvents.ToArray()),
            _level100ActorScripts.Snapshot,
            Array.AsReadOnly(_level100ActorScriptCommands.ToArray()),
            _level100ActorMechanics.Snapshot,
            _nextProjectileId,
            Array.AsReadOnly(projectiles),
            Array.AsReadOnly(walkerFeet))
        {
            RetailEventFrameCount = _retailEventFrameCount,
            Level100PlayerWeaponState = _level100PlayerWeapons.Snapshot,
            Level100BattleEngineTargeting = TargetingSnapshot,
            Level100PlayerStores = _level100PlayerWeapons.StoresSnapshot,
            Level100BattleEngineShake = _shake.Snapshot,
            Level100MissilePod = _level100PlayerWeapons.PodSnapshot,
        };
    }

}
