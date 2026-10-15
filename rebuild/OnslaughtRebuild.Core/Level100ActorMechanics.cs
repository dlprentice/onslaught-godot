// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public enum Level100ActorCommandIntent
{
    Stopped = 0,
    FollowingWaypoint = 1,
    Attacking = 2,
    Retreating = 3,
}

public sealed record Level100ActorCommandIntentSnapshot(
    Level100ActorId ActorId,
    int AiState,
    int Allegiance,
    bool HasAllegianceOverride,
    Level100ActorCommandIntent Intent,
    Level100ActorId? TargetActorId,
    string? WaypointPath,
    // The level row of the node the follower is steering at; the next one is
    // that node's own target (Level100WaypointPathDefinition).
    int? WaypointNodeIndex,
    int WaypointCommandScalar,
    bool WaitForWaypointCompletion,
    int GroundFullGuideBaseTickPhase)
{
    public Level100PlaneGuideSnapshot? PlaneGuide { get; init; }
    public Level100PlaneSpawnerExitSnapshot? PlaneSpawnerExit { get; init; }

    /// <summary>A <c>CDropship</c>'s landing state (<c>+0x27c</c>); 0 for anything else.</summary>
    public int DropshipLandingState { get; init; }
}

public sealed record Level100ActorMechanicsSnapshot(
    long LastConsumedCommandSequence,
    IReadOnlyList<Level100ActorCommandIntentSnapshot> Actors,
    int ReleasedRandomSeed,
    int NextActorRoundId,
    IReadOnlyList<Level100ActorWeaponSnapshot> ActorWeapons,
    IReadOnlyList<Level100ActorRoundSnapshot> ActorRounds)
{
    public RetailEventSchedulerSnapshot? PlaneEvents { get; init; }

    /// <summary>Every constructed unit's callback state, by actor.</summary>
    public IReadOnlyList<Level100UnitCallbackSnapshot>? UnitCallbacks { get; init; }

    /// <summary>The landscape damage a lost base building left at load, in stamp order.</summary>
    public IReadOnlyList<Level100LandscapeDamageStamp> LandscapeDamageStamps { get; init; } = [];
}

/// <summary>
/// One landscape damage stamp (<c>0x005475d0</c>): retail X and Y as float32
/// bits, and the damage type.
/// </summary>
public sealed record Level100LandscapeDamageStamp(int XFloatBits, int YFloatBits, int DamageType);

public sealed record Level100ActorMechanicsWaitCompletion(
    Level100ActorId ActorId,
    Level100ActorScriptWaitKind WaitKind,
    string Argument);

/// <summary>
/// Canonical consumer for the released Level 100 actor-script mechanics
/// commands. It implements only the evidenced CGroundVehicle guide cadence,
/// normal speed, heading bound, and terrain grounding. Actor identity and the
/// full physical pose remain exclusively in
/// <see cref="Level100ActorRegistry"/>.
/// </summary>
/// <remarks>
/// This class used to carry a 20-of-every-30 accumulator, because the released
/// base frame is 20 Hz and Core ran at 30. Core now runs at 20 Hz, so every
/// Core tick IS a released base tick, the accumulator was the identity, and it
/// has been deleted along with its serialized-and-hashed snapshot field and the
/// <c>ClearSkippedCoreTickVelocities</c> pass that only existed to zero the
/// reported velocity on the Core ticks that were not base ticks. That pass made
/// a plane's reported <c>LinearVelocityMillimetersPerTick</c> alternate between
/// the base-tick step and zero, which any interpolating renderer saw as a
/// stutter; the stutter is gone with it.
/// </remarks>
public sealed partial class Level100ActorMechanics
{
    /// <summary>
    /// The released base-frame rate, <c>GAME_FR</c>
    /// (<c>references/Onslaught/thing.h:28</c>). Core runs at this rate, so it
    /// equals <see cref="SimulationConstants.TicksPerSecond"/> - but the two
    /// are not the same fact, and the divisions that turn authored
    /// per-second retail data into per-tick data are stated against this one.
    /// </summary>
    public const int RetailBaseTicksPerSecond =
        SimulationConstants.RetailTicksPerSecond;

    private const int FixedTrigScale = 1 << 30;
    private const int HalfPiMicroRad = 1_570_796;
    private const int PiMicroRad = 3_141_593;
    private const int TwoPiMicroRad = PiMicroRad * 2;
    private const int CordicGainQ30 = 652_032_874;

    private static ReadOnlySpan<int> CordicAnglesMicroRad =>
    [
        785_398, 463_648, 244_979, 124_355, 62_419, 31_240, 15_624,
        7_812, 3_906, 1_953, 977, 488, 244, 122, 61, 31, 15, 8, 4, 2, 1,
    ];

    private sealed class ActorState
    {
        internal required Level100ActorId ActorId { get; init; }
        internal int AiState { get; set; }
        internal int Allegiance { get; set; }
        internal bool HasAllegianceOverride { get; set; }
        internal Level100ActorCommandIntent Intent { get; set; }
        internal Level100ActorId? TargetActorId { get; set; }
        internal string? WaypointPath { get; set; }
        internal int? WaypointNodeIndex { get; set; }
        internal int WaypointCommandScalar { get; set; }
        internal bool WaitForWaypointCompletion { get; set; }
        internal int GroundFullGuideBaseTickPhase { get; set; }

        /// <summary>A <c>CDropship</c>'s landing state, <c>+0x27c</c>: 0 airborne, 2 after <c>Land</c>.</summary>
        internal int DropshipLandingState { get; set; }
        internal Level100PlaneGuideSnapshot? PlaneGuide { get; set; }
        internal Level100PlaneSpawnerExitSnapshot? PlaneSpawnerExit { get; set; }
    }

    private readonly Level100ActorRegistry _actors;
    private readonly Level100ActorDefinitionSet _definitions;
    private readonly Dictionary<string, Level100WaypointPathDefinition> _waypointPaths;
    private readonly SortedDictionary<int, ActorState> _states = [];
    private long _lastConsumedCommandSequence;

    public Level100ActorMechanics(
        Level100ActorRegistry actors,
        Level100ActorDefinitionSet definitions) : this(actors, definitions, construct: true)
    {
    }

    /// <summary>
    /// Constructs the level with the Battle Engine's own 6002/6003 draws
    /// supplied by its owner, at the Battle Engine's place in the load order.
    /// </summary>
    internal Level100ActorMechanics(
        Level100ActorRegistry actors,
        Level100ActorDefinitionSet definitions,
        Action<RetailEventScheduler, Func<int>> battleEngineRefresh) : this(actors, definitions, construct: false)
    {
        ArgumentNullException.ThrowIfNull(battleEngineRefresh);
        ConstructLevel(battleEngineRefresh);
    }

    private Level100ActorMechanics(Level100ActorRegistry actors,
        Level100ActorDefinitionSet definitions, bool construct)
    {
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        ValidateDefinitionIdentity();
        _waypointPaths = _definitions.WaypointPaths.ToDictionary(
            path => path.Name, SeatWaypoints, StringComparer.Ordinal);
        if (construct) ConstructLevel(null);
    }

    /// <summary>
    /// A path with its waypoints at the height they take at load: raised to
    /// the ground, then to the water, when below them. Only z changes; a
    /// third sample in <c>CWaypoint::InitAndLink</c> (<c>0x005057d4</c>)
    /// cannot change it again (<c>waypoint-paths.md</c>, "Loading").
    /// </summary>
    private Level100WaypointPathDefinition SeatWaypoints(Level100WaypointPathDefinition path) =>
        path with
        {
            Points = Array.AsReadOnly(path.Points.Select(point =>
            {
                Level100FloatVector4Bits authored = point.RetailComponentsFloatBits;
                Level100FloatVector3Bits seated =
                    _actors.SeatRetailPosition(new(authored.X, authored.Y, authored.Z));
                return point with { RetailComponentsFloatBits = authored with { Z = seated.Z } };
            }).ToArray()),
        };

    private Level100WaypointPathDefinition GetWaypointPath(string name) =>
        _waypointPaths.TryGetValue(name, out Level100WaypointPathDefinition? path)
            ? path
            : throw new KeyNotFoundException($"Waypoint path '{name}' does not exist.");

    public Level100ActorMechanics(
        Level100ActorRegistry actors,
        Level100ActorDefinitionSet definitions,
        Level100ActorMechanicsSnapshot snapshot)
        : this(actors, definitions, construct: false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Actors);
        ArgumentNullException.ThrowIfNull(snapshot.ActorWeapons);
        ArgumentNullException.ThrowIfNull(snapshot.ActorRounds);
        if (snapshot.LastConsumedCommandSequence < 0 ||
            snapshot.Actors.Any(item => item is null))
        {
            throw new ArgumentException(
                "Level 100 actor mechanics snapshot is invalid.",
                nameof(snapshot));
        }

        _lastConsumedCommandSequence = snapshot.LastConsumedCommandSequence;
        _states.Clear(); // Restore retained state; do not replay creation RNG/events.
        foreach (Level100ActorCommandIntentSnapshot source in snapshot.Actors)
        {
            ValidateSnapshotState(source, snapshot);
            if (!_states.TryAdd(source.ActorId.Value, Restore(source)))
            {
                throw new ArgumentException(
                    "Level 100 actor mechanics snapshot has duplicate actors.",
                    nameof(snapshot));
            }
        }

        RestoreArmament(snapshot);
        RestoreUnitCallbacks(snapshot);
        RestorePlaneEvents(snapshot);
        _landscapeDamageStamps.AddRange(snapshot.LandscapeDamageStamps ?? []);
    }

    private readonly List<Level100LandscapeDamageStamp> _landscapeDamageStamps = [];

    public Level100ActorMechanicsSnapshot Snapshot => new(
        _lastConsumedCommandSequence,
        Array.AsReadOnly(_states.Values.Select(SnapshotState).ToArray()),
        _releasedRandom.Seed,
        _nextActorRoundId,
        SnapshotActorWeapons(),
        SnapshotActorRounds())
    {
        PlaneEvents = _planeEvents?.Snapshot,
        UnitCallbacks = _unitCallbacks.Count == 0 ? null : UnitCallbackSnapshots,
        LandscapeDamageStamps = Array.AsReadOnly(_landscapeDamageStamps.ToArray()),
    };

    private static bool OwnsCommand(Level100ActorScriptCommandKind kind) =>
        kind is
            Level100ActorScriptCommandKind.Land or
            Level100ActorScriptCommandKind.FollowWaypoint or
            Level100ActorScriptCommandKind.FollowWaypointWait or
            Level100ActorScriptCommandKind.SetAIState or
            Level100ActorScriptCommandKind.SetAllegiance or
            Level100ActorScriptCommandKind.Attack or
            Level100ActorScriptCommandKind.Retreat or
            Level100ActorScriptCommandKind.Stop;

    public void ConsumeCommands(
        IReadOnlyList<Level100ActorScriptCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        foreach (Level100ActorScriptCommand command in commands)
        {
            ConsumeCommand(command, requireOwned: false);
        }
    }

    public void ApplyCommand(Level100ActorScriptCommand command) =>
        ConsumeCommand(command, requireOwned: true);

    /// <summary>
    /// One Core tick, which is one released base tick - see the class remarks
    /// for the accumulator this replaced.
    /// </summary>
    public IReadOnlyList<Level100ActorMechanicsWaitCompletion> AdvanceTick(
        Action<Level100ActorId>? dispatchReady = null,
        Action<Level100ActorId>? startPlaneDeath = null)
    {
        _planeEvents?.AdvanceTime();
        return AdvanceRetailBaseTick(dispatchReady, startPlaneDeath, null);
    }

    /// <param name="cameraPosition">
    /// Player 0's current camera position, which each unit's 4003 compares
    /// with its own. Core has no pan camera, so the caller supplies the Battle
    /// Engine's position, which is exactly the first-person camera's.
    /// </param>
    /// <param name="settleFacts">
    /// Settles the facts a callback reported (a unit's SHUTDOWN), inside the
    /// flush, before the next callback.
    /// </param>
    internal IReadOnlyList<Level100ActorMechanicsWaitCompletion> AdvanceTick(uint eventFrameCount,
        Action<Level100ActorId>? dispatchReady = null,
        Action<Level100ActorId>? startPlaneDeath = null,
        Action<RetailEventScheduler, RetailEventDispatch>? battleEngineEvent = null,
        SimVector3? cameraPosition = null,
        Action? settleFacts = null)
    {
        if (_planeEvents is not null && _planeEvents.FrameCount != eventFrameCount)
            throw new InvalidOperationException("Aircraft callbacks must share the Simulation event clock.");
        _cameraPosition = cameraPosition;
        _settleFacts = settleFacts;
        try
        {
            return AdvanceRetailBaseTick(dispatchReady, startPlaneDeath, battleEngineEvent);
        }
        finally
        {
            _cameraPosition = null;
            _settleFacts = null;
        }
    }

    private void ConsumeCommand(
        Level100ActorScriptCommand command,
        bool requireOwned)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Sequence <= _lastConsumedCommandSequence)
        {
            throw new InvalidOperationException(
                $"Level 100 actor command sequence {command.Sequence} follows " +
                $"{_lastConsumedCommandSequence}.");
        }

        bool owned = OwnsCommand(command.Kind);
        if (requireOwned && !owned)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                $"Level 100 actor mechanics does not own command {command.Kind}.");
        }

        if (owned)
        {
            switch (command.Kind)
            {
                case Level100ActorScriptCommandKind.FollowWaypoint:
                case Level100ActorScriptCommandKind.FollowWaypointWait:
                    BeginWaypoint(command);
                    break;
                case Level100ActorScriptCommandKind.Land:
                {
                    // IScript::Land (0x005361d0) calls a unit's slot 93; a
                    // CDropship's (0x00447f50) lands where the craft is and
                    // sets landing state 2 through slot 118 (0x00447a40).
                    ActorState state = RequireState(command);
                    if (Level100ConstructionClasses.Of(_actors.GetActor(state.ActorId).DefinitionName) ==
                        Level100ConstructionClass.Dropship)
                    {
                        state.DropshipLandingState = 2;
                    }
                    break;
                }
                case Level100ActorScriptCommandKind.SetAIState:
                {
                    ActorState state = RequireState(command);
                    state.AiState = command.Scalar;
                    if (command.Scalar == 1 && state.TargetActorId.HasValue)
                        SetStoppedIntent(state); // Clears controller target; guide destination survives.
                    break;
                }
                case Level100ActorScriptCommandKind.SetAllegiance:
                {
                    ActorState state = RequireState(command);
                    state.Allegiance = command.Scalar;
                    state.HasAllegianceOverride = true;
                    break;
                }
                case Level100ActorScriptCommandKind.Attack:
                    BeginAttack(command);
                    break;
                case Level100ActorScriptCommandKind.Retreat:
                {
                    // An air unit's Retreat is slot 100 alone: its guide flies
                    // to the retreat point and nothing else stops.
                    ActorState state = RequireState(command);
                    if (state.PlaneGuide is not null)
                    {
                        RetreatAirUnit(state);
                    }
                    else
                    {
                        SetSimpleIntent(command, Level100ActorCommandIntent.Retreating);
                    }
                    break;
                }
                case Level100ActorScriptCommandKind.Stop:
                    Stop(command);
                    break;
            }
        }

        _lastConsumedCommandSequence = command.Sequence;
    }

    private IReadOnlyList<Level100ActorMechanicsWaitCompletion>
        AdvanceRetailBaseTick(Action<Level100ActorId>? dispatchReady, Action<Level100ActorId>? startPlaneDeath,
            Action<RetailEventScheduler, RetailEventDispatch>? battleEngineEvent)
    {
        var completions = new List<Level100ActorMechanicsWaitCompletion>();
        InvalidatePlaneReferences();
        _planeEvents?.Flush((events, item) =>
            DispatchPlaneEvent(events, item, dispatchReady, startPlaneDeath, battleEngineEvent));
        foreach (ActorState state in _states.Values)
        {
            Level100ActorSnapshot actor = _actors.GetActor(state.ActorId);
            Level100ActorMotionDefinition? motion =
                _definitions.FindMotionDefinition(actor.DefinitionName);
            if (motion?.MotionClass ==
                Level100ActorMotionClass.GroundVehicle)
            {
                // The Actor's MOVE/LF_MOVE cadence runs every frame from the
                // frame after construction, active or not, until the unit is
                // deleted; what a Move does depends on the unit's state.
                if (actor.Lifecycle == Level100ActorLifecycle.Destroyed ||
                    ConstructedThisFrame(state.ActorId))
                {
                    continue;
                }

                bool fullGuideUpdate = state.GroundFullGuideBaseTickPhase == 0;
                state.GroundFullGuideBaseTickPhase =
                    (state.GroundFullGuideBaseTickPhase + 1) % motion.FullGuideBaseTicks!.Value;
                if (actor.Lifecycle == Level100ActorLifecycle.DiedAwaitingShutdown ||
                    (actor.Active && actor.Lifecycle == Level100ActorLifecycle.Alive))
                {
                    if (actor.Lifecycle == Level100ActorLifecycle.DiedAwaitingShutdown)
                    {
                        AdvanceDyingGroundVehicle(state, motion, fullGuideUpdate);
                    }
                    else if (state.Intent ==
                        Level100ActorCommandIntent.FollowingWaypoint)
                    {
                        AdvanceGroundVehicle(
                            state,
                            motion,
                            fullGuideUpdate);
                    }
                    else
                    {
                        ZeroActorVelocity(state.ActorId);
                    }
                }
                else
                {
                    ZeroActorVelocity(state.ActorId);
                }
                actor = _actors.GetActor(state.ActorId);
            }

            if (state.Intent !=
                    Level100ActorCommandIntent.FollowingWaypoint ||
                !PlaneScriptControlAvailable(state) ||
                motion is null ||
                !actor.Active ||
                actor.Lifecycle != Level100ActorLifecycle.Alive)
            {
                continue;
            }

            ObserveWaypointArrival(state, actor, motion, completions);
        }

        // Rounds move on their own MOVE events inside the flush above. A round
        // these weapons launch files its first MOVE into the current bucket,
        // so it first moves in the next frame.
        AdvanceActorWeapons();

        return Array.AsReadOnly(completions.ToArray());
    }

    private static int Q30Hypotenuse(int left, int right)
    {
        ulong square =
            (ulong)((long)left * left) + (ulong)((long)right * right);
        if (square == 0)
        {
            return 0;
        }
        ulong root = 0;
        ulong bit = 1UL << 62;
        while (bit > square)
        {
            bit >>= 2;
        }
        while (bit != 0)
        {
            if (square >= root + bit)
            {
                square -= root + bit;
                root = (root >> 1) + bit;
            }
            else
            {
                root >>= 1;
            }
            bit >>= 2;
        }
        return checked((int)root);
    }

    private void AdvanceDyingGroundVehicle(
        ActorState state, Level100ActorMotionDefinition motion, bool fullUpdate)
    {
        // Retail Ground guide 0x0047d750 -> 0x004fcf00 stops velocity on
        // full MOVE only. Actor LF_MOVE retains velocity and old orientation
        // between those calls (actor.cpp:53-70, 211-257). No waypoint update.
        Level100ActorPoseSnapshot pose = _actors.GetPose(state.ActorId);
        SimVector3 velocity = fullUpdate ? SimVector3.Zero : pose.LinearVelocityMillimetersPerTick;
        int x = checked(pose.PositionMillimeters.X + velocity.X);
        int z = checked(pose.PositionMillimeters.Z + velocity.Z);
        int y = Math.Max(checked(pose.PositionMillimeters.Y + velocity.Y),
            checked(Level100Terrain.Instance.SampleGroundElevationMillimeters(new SimVector2(x, z)) +
                motion.CoreGroundOriginOffsetMillimeters!.Value));
        var position = new SimVector3(x, y, z);
        if (fullUpdate)
        {
            _actors.AdvancePose(state.ActorId, pose with
            {
                PositionMillimeters = position,
                LinearVelocityMillimetersPerTick = SimVector3.Zero,
                AngularVelocityMicroRadiansPerTick = SimVector3.Zero,
            });
        }
        else
        {
            _actors.AdvanceLowFidelityPosition(state.ActorId, position);
        }
        // This is the existing millimetre motion projection. Native float
        // velocity, contact timestamps, full terrain tilt/bob and water
        // callbacks remain separate contracts, not inferred from this clamp.
    }

    private void AdvanceGroundVehicle(
        ActorState state,
        Level100ActorMotionDefinition motion,
        bool fullGuideUpdate)
    {
        Level100ActorPoseSnapshot pose = _actors.GetPose(state.ActorId);
        Level100ActorPoseSnapshot guidedPose = fullGuideUpdate
            ? UpdateGroundVehicleGuide(state, motion, pose)
            : pose with
            {
                AngularVelocityMicroRadiansPerTick = SimVector3.Zero,
            };
        SimVector3 velocity = GroundVehicleStep(
            state,
            motion,
            guidedPose);
        int nextX = checked(
            guidedPose.PositionMillimeters.X + velocity.X);
        int nextZ = checked(
            guidedPose.PositionMillimeters.Z + velocity.Z);
        int nextY = checked(
            Level100Terrain.Instance.SampleGroundElevationMillimeters(
                new SimVector2(nextX, nextZ)) +
            motion.CoreGroundOriginOffsetMillimeters!.Value);
        var nextPosition = new SimVector3(nextX, nextY, nextZ);
        _actors.AdvancePose(
            state.ActorId,
            guidedPose with
            {
                PositionMillimeters = nextPosition,
                LinearVelocityMillimetersPerTick = new SimVector3(
                    nextPosition.X - guidedPose.PositionMillimeters.X,
                    nextPosition.Y - guidedPose.PositionMillimeters.Y,
                    nextPosition.Z - guidedPose.PositionMillimeters.Z),
            });
    }

    private Level100ActorPoseSnapshot UpdateGroundVehicleGuide(
        ActorState state,
        Level100ActorMotionDefinition motion,
        Level100ActorPoseSnapshot pose)
    {
        Level100WaypointPathDefinition path =
            GetWaypointPath(state.WaypointPath!);
        Level100WaypointPointDefinition point =
            path.Point(state.WaypointNodeIndex!.Value);
        long deltaX =
            (long)point.PositionMillimeters.X -
            pose.PositionMillimeters.X;
        long deltaZ =
            (long)point.PositionMillimeters.Z -
            pose.PositionMillimeters.Z;
        if (deltaX == 0 && deltaZ == 0)
        {
            return pose with
            {
                AngularVelocityMicroRadiansPerTick = SimVector3.Zero,
            };
        }

        int forwardX = FloatBitsToQ30(
            pose.BasisFloatBits.Row0Z);
        int forwardZ = FloatBitsToQ30(
            pose.BasisFloatBits.Row2Z);
        int currentYaw = FixedAtan2(-forwardX, forwardZ);
        int desiredYaw = FixedAtan2(-deltaX, deltaZ);
        int error = NormalizeMicroRad(desiredYaw - currentYaw);
        int maximumTurnPerBaseTick = ScalePositiveFloatBits(
            motion.MaximumTurnRadiansPerBaseTickFloatBits!.Value,
            1_000_000);
        int fullUpdateMultiplier =
            motion.FullGuideBaseTicks!.Value;
        int easedTurn = DivideRoundNearest(
            (long)Math.Abs(error) * fullUpdateMultiplier,
            10);
        int maximumTurn =
            checked(maximumTurnPerBaseTick * fullUpdateMultiplier);
        int turn = Math.Sign(error) *
            Math.Min(easedTurn, maximumTurn);
        return pose with
        {
            BasisFloatBits = RotateBasisAroundCoreY(
                pose.BasisFloatBits,
                turn),
            AngularVelocityMicroRadiansPerTick =
                new SimVector3(0, turn, 0),
        };
    }

    private SimVector3 GroundVehicleStep(
        ActorState state,
        Level100ActorMotionDefinition motion,
        Level100ActorPoseSnapshot pose)
    {
        Level100WaypointPathDefinition path =
            GetWaypointPath(state.WaypointPath!);
        Level100WaypointPointDefinition point =
            path.Point(state.WaypointNodeIndex!.Value);
        long deltaX =
            (long)point.PositionMillimeters.X -
            pose.PositionMillimeters.X;
        long deltaZ =
            (long)point.PositionMillimeters.Z -
            pose.PositionMillimeters.Z;
        long radius = motion.ArrivalRadiusMillimeters;
        if ((deltaX * deltaX) + (deltaZ * deltaZ) <
            radius * radius)
        {
            return SimVector3.Zero;
        }

        int forwardX = FloatBitsToQ30(
            pose.BasisFloatBits.Row0Z);
        int forwardZ = FloatBitsToQ30(
            pose.BasisFloatBits.Row2Z);
        int yaw = FixedAtan2(-forwardX, forwardZ);
        int speedMillimetersPerSecond = ScalePositiveFloatBits(
            motion.MaximumSpeedFloatBits!.Value,
            1_000);
        int speedMillimetersPerBaseTick = DivideRoundNearest(
            speedMillimetersPerSecond,
            RetailBaseTicksPerSecond);
        (int sin, int cos) = FixedSinCos(yaw);
        return new SimVector3(
            DivideRoundNearest(
                -(long)sin * speedMillimetersPerBaseTick,
                FixedTrigScale),
            0,
            DivideRoundNearest(
                (long)cos * speedMillimetersPerBaseTick,
                FixedTrigScale));
    }

    private void ObserveWaypointArrival(
        ActorState state,
        Level100ActorSnapshot actor,
        Level100ActorMotionDefinition motion,
        List<Level100ActorMechanicsWaitCompletion> completions)
    {
        Level100WaypointPathDefinition path =
            GetWaypointPath(state.WaypointPath!);
        Level100WaypointPointDefinition point =
            path.Point(state.WaypointNodeIndex!.Value);
        if (state.PlaneGuide is not null)
        {
            // An air unit arrives on the follower's own float test, with its
            // class radius (slot 94: 5.0 for CPlane at 0x0050e8e0, 8.0 for
            // CDropship at 0x0050ead0).
            if (!RetailIScriptFollowWaypoint.Arrived(
                    _actors.GetBaseState(state.ActorId).RetailPoses!.Current.PositionFloatBits,
                    point.RetailComponentsFloatBits,
                    motion.ArrivalRadiusMillimeters / 1000f))
            {
                return;
            }
        }
        else
        {
            long deltaX =
                (long)point.PositionMillimeters.X -
                actor.Pose.PositionMillimeters.X;
            long deltaZ =
                (long)point.PositionMillimeters.Z -
                actor.Pose.PositionMillimeters.Z;
            long radius = motion.ArrivalRadiusMillimeters;
            if ((deltaX * deltaX) + (deltaZ * deltaZ) >=
                radius * radius)
            {
                return;
            }
        }

        // On arrival the next node is the waypoint's own target
        // (UpdateWaypointFollowing 0x00538470, `mov ecx,[eax+0x3c]` at
        // 0x005384dc). A chain that loops never ends; one that targets itself
        // logs an error and ends the walk.
        if (point.TargetNodeIndex is { } next && next != point.NodeIndex)
        {
            state.WaypointNodeIndex = next;
            SetPlaneWaypointDestination(state);
            return;
        }

        // With no next node the unit's slot 64 runs and the script resumes
        // (waypoint-paths.md, "Following"). A CDropship's slot 64 is a bare
        // `ret` (0x00459990): it keeps its last goal and its velocity. A
        // CPlane resets its guide (0x00422750 -> 0x0047e3d0) and a ground
        // vehicle also stops (0x004fcf00); ZeroActorVelocity is both.
        bool waited = state.WaitForWaypointCompletion;
        string completedPath = state.WaypointPath!;
        SetStoppedIntent(state);
        if (motion.MotionClass != Level100ActorMotionClass.Dropship)
        {
            ZeroActorVelocity(state.ActorId);
        }
        if (waited)
        {
            completions.Add(new Level100ActorMechanicsWaitCompletion(
                actor.ActorId,
                Level100ActorScriptWaitKind.FollowWaypoint,
                completedPath));
        }
    }

    private void BeginWaypoint(Level100ActorScriptCommand command)
    {
        ActorState state = RequireState(command);
        string pathName = command.Argument ??
            throw new InvalidOperationException(
                "Released waypoint command has no path.");
        Level100WaypointPathDefinition path = GetWaypointPath(pathName);
        Level100ActorSnapshot actor = _actors.GetActor(state.ActorId);
        _ = _definitions.FindMotionDefinition(actor.DefinitionName) ??
            throw new InvalidOperationException(
                $"Released waypoint actor {actor.ActorId} has no class motion definition.");
        // FollowWaypointWait (0x00537e40) starts at the node nearest the
        // unit's own position (0x00505c30), not at the list's first node.
        Level100FloatVector3Bits position =
            _actors.GetBaseState(state.ActorId).RetailPoses?.Current.PositionFloatBits ??
            RetailPositionFromProjection(actor.Pose.PositionMillimeters);
        Level100WaypointPointDefinition start = path.NearestPoint(position) ??
            throw new NotSupportedException(
                $"No node of waypoint path '{pathName}' is within reach of actor {actor.ActorId}.");
        state.Intent = Level100ActorCommandIntent.FollowingWaypoint;
        state.TargetActorId = null;
        state.WaypointPath = pathName;
        state.WaypointNodeIndex = start.NodeIndex;
        // The released command scalar is canonical replay state, but its
        // actor-specific movement meaning is not established by this slice.
        state.WaypointCommandScalar = command.Scalar;
        state.WaitForWaypointCompletion =
            command.Kind ==
            Level100ActorScriptCommandKind.FollowWaypointWait;
        SetPlaneWaypointDestination(state);
    }

    private void BeginAttack(Level100ActorScriptCommand command)
    {
        ActorState state = RequireState(command);
        Level100ActorId target = command.TargetActorId ??
            throw new InvalidOperationException(
                "Released Attack command has no target.");
        _ = _actors.GetActor(target);
        ClearWaypoint(state);
        state.Intent = Level100ActorCommandIntent.Attacking;
        state.TargetActorId = target;
        if (state.PlaneGuide is null) ZeroActorVelocity(state.ActorId);
        ArmActorWeapons(state);
    }

    private void SetSimpleIntent(
        Level100ActorScriptCommand command,
        Level100ActorCommandIntent intent)
    {
        ActorState state = RequireState(command);
        ClearWaypoint(state);
        state.Intent = intent;
        state.TargetActorId = null;
        ZeroActorVelocity(state.ActorId);
    }

    private void Stop(Level100ActorScriptCommand command)
    {
        ActorState state = RequireState(command);
        SetStoppedIntent(state);
        ZeroActorVelocity(state.ActorId);
    }

    private ActorState RequireState(
        Level100ActorScriptCommand command)
    {
        Level100ActorId actorId = command.ActorId ??
            throw new InvalidOperationException(
                $"Released actor command {command.Kind} has no actor.");
        _ = _actors.GetActor(actorId);
        if (_states.TryGetValue(actorId.Value, out ActorState? state))
        {
            return state;
        }

        if (_actors.GetBaseState(actorId).RetailPlane is not null)
            throw new InvalidOperationException("Raw aircraft mechanics must be registered before its script initializer.");

        state = new ActorState
        {
            ActorId = actorId,
            Intent = Level100ActorCommandIntent.Stopped,
            Allegiance = _actors.GetAuthoredAllegiance(actorId),
        };
        _states.Add(actorId.Value, state);
        return state;
    }

    /// <summary>A draw from the level's one gameplay stream, for a script's <c>Rand</c>.</summary>
    internal int NextSharedRandom() => _releasedRandom.Next();

    private void ZeroActorVelocity(Level100ActorId actorId)
    {
        if (_states.TryGetValue(actorId.Value, out ActorState? state) && state.PlaneGuide is { } guide)
        {
            ThingActorBaseState actor = _actors.GetPlaneState(actorId);
            state.PlaneGuide = guide with { Mode = 0, Destination = actor.Snapshot.RetailPoses!.Current.PositionFloatBits };
            actor.ClearRetailPlaneDrive();
        }
        else _actors.StopMotion(actorId);
    }

    private static void SetStoppedIntent(ActorState state)
    {
        ClearWaypoint(state);
        state.Intent = Level100ActorCommandIntent.Stopped;
        state.TargetActorId = null;
    }

    private static void ClearWaypoint(ActorState state)
    {
        state.WaypointPath = null;
        state.WaypointNodeIndex = null;
        state.WaypointCommandScalar = 0;
        state.WaitForWaypointCompletion = false;
    }

    private void ValidateDefinitionIdentity()
    {
        if (!StringComparer.Ordinal.Equals(
                _actors.Snapshot.DefinitionSetIdentitySha256,
                _definitions.IdentitySha256))
        {
            throw new ArgumentException(
                "Level 100 actor mechanics definitions do not own the registry.");
        }
    }

    private void ValidateSnapshotState(
        Level100ActorCommandIntentSnapshot source,
        Level100ActorMechanicsSnapshot snapshot)
    {
        Level100ActorSnapshot actor =
            _actors.GetActor(source.ActorId);
        Level100ActorMotionDefinition? motion =
            _definitions.FindMotionDefinition(actor.DefinitionName);
        bool validGroundPhase =
            motion?.MotionClass ==
                Level100ActorMotionClass.GroundVehicle
                ? source.GroundFullGuideBaseTickPhase >= 0 &&
                    source.GroundFullGuideBaseTickPhase <
                    motion.FullGuideBaseTicks!.Value
                : source.GroundFullGuideBaseTickPhase == 0;
        ValidatePlaneGuide(source);
        if (source.ActorId.Value <= 0 ||
            !Enum.IsDefined(source.Intent) ||
            !validGroundPhase)
        {
            throw new ArgumentException(
                "Level 100 actor mechanics snapshot has invalid state.",
                nameof(snapshot));
        }

        if (source.Intent ==
            Level100ActorCommandIntent.FollowingWaypoint)
        {
            if (source.TargetActorId.HasValue ||
                source.WaypointPath is null ||
                motion is null)
            {
                throw new ArgumentException(
                    "Level 100 actor mechanics snapshot has invalid waypoint intent.",
                    nameof(snapshot));
            }
            Level100WaypointPathDefinition path =
                GetWaypointPath(source.WaypointPath);
            if (source.WaypointNodeIndex is not { } node || !path.HasNode(node))
            {
                throw new ArgumentException(
                    "Level 100 actor mechanics snapshot has invalid waypoint progress.",
                    nameof(snapshot));
            }
            return;
        }

        if (source.WaypointPath is not null ||
            source.WaypointNodeIndex is not null ||
            source.WaypointCommandScalar != 0 ||
            source.WaitForWaypointCompletion)
        {
            throw new ArgumentException(
                "Level 100 actor mechanics snapshot retained an inactive waypoint.",
                nameof(snapshot));
        }

        if (source.Intent ==
            Level100ActorCommandIntent.Attacking)
        {
            if (!source.TargetActorId.HasValue)
            {
                throw new ArgumentException(
                    "Level 100 actor mechanics snapshot has no attack target.",
                    nameof(snapshot));
            }
            _ = _actors.GetActor(source.TargetActorId.Value);
        }
        else if (source.TargetActorId.HasValue)
        {
            throw new ArgumentException(
                "Level 100 actor mechanics snapshot has an unexpected target.",
                nameof(snapshot));
        }
    }

    private static Level100ActorCommandIntentSnapshot SnapshotState(
        ActorState state) => new(
            state.ActorId,
            state.AiState,
            state.Allegiance,
            state.HasAllegianceOverride,
            state.Intent,
            state.TargetActorId,
            state.WaypointPath,
            state.WaypointNodeIndex,
            state.WaypointCommandScalar,
            state.WaitForWaypointCompletion,
            state.GroundFullGuideBaseTickPhase)
        {
            PlaneGuide = state.PlaneGuide,
            PlaneSpawnerExit = state.PlaneSpawnerExit,
            DropshipLandingState = state.DropshipLandingState,
        };

    private static ActorState Restore(
        Level100ActorCommandIntentSnapshot source) => new()
        {
            ActorId = source.ActorId,
            AiState = source.AiState,
            Allegiance = source.Allegiance,
            HasAllegianceOverride = source.HasAllegianceOverride,
            Intent = source.Intent,
            TargetActorId = source.TargetActorId,
            WaypointPath = source.WaypointPath,
            WaypointNodeIndex = source.WaypointNodeIndex,
            WaypointCommandScalar = source.WaypointCommandScalar,
            WaitForWaypointCompletion =
                source.WaitForWaypointCompletion,
            GroundFullGuideBaseTickPhase =
                source.GroundFullGuideBaseTickPhase,
            DropshipLandingState = source.DropshipLandingState,
            PlaneGuide = source.PlaneGuide,
            PlaneSpawnerExit = source.PlaneSpawnerExit,
        };

    private static Level100FloatBasis3Bits RotateBasisAroundCoreY(
        Level100FloatBasis3Bits basis,
        int angleMicroRad)
    {
        if (angleMicroRad == 0)
        {
            return basis;
        }

        (int sin, int cos) = FixedSinCos(angleMicroRad);
        int r0x = FloatBitsToQ30(basis.Row0X);
        int r0y = FloatBitsToQ30(basis.Row0Y);
        int r0z = FloatBitsToQ30(basis.Row0Z);
        int r2x = FloatBitsToQ30(basis.Row2X);
        int r2y = FloatBitsToQ30(basis.Row2Y);
        int r2z = FloatBitsToQ30(basis.Row2Z);
        return new Level100FloatBasis3Bits(
            Q30ToFloatBits(MultiplyFixed(cos, r0x) -
                MultiplyFixed(sin, r2x)),
            Q30ToFloatBits(MultiplyFixed(cos, r0y) -
                MultiplyFixed(sin, r2y)),
            Q30ToFloatBits(MultiplyFixed(cos, r0z) -
                MultiplyFixed(sin, r2z)),
            basis.Row1X,
            basis.Row1Y,
            basis.Row1Z,
            Q30ToFloatBits(MultiplyFixed(sin, r0x) +
                MultiplyFixed(cos, r2x)),
            Q30ToFloatBits(MultiplyFixed(sin, r0y) +
                MultiplyFixed(cos, r2y)),
            Q30ToFloatBits(MultiplyFixed(sin, r0z) +
                MultiplyFixed(cos, r2z)));
    }

    private static int MultiplyFixed(int left, int right) =>
        DivideRoundNearest(
            (long)left * right,
            FixedTrigScale);

    private static (int Sin, int Cos) FixedSinCos(
        int angleMicroRad)
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

    private static int NormalizeMicroRad(int angle)
    {
        while (angle > PiMicroRad)
        {
            angle -= TwoPiMicroRad;
        }
        while (angle < -PiMicroRad)
        {
            angle += TwoPiMicroRad;
        }
        return angle;
    }

    private static int ScalePositiveFloatBits(
        int bits,
        int scale)
    {
        uint raw = unchecked((uint)bits);
        int exponent = (int)((raw >> 23) & 0xFF);
        uint fraction = raw & 0x7FFFFF;
        if ((raw & 0x80000000) != 0 ||
            exponent == 0xFF ||
            (exponent == 0 && fraction == 0))
        {
            throw new InvalidDataException(
                "A Level 100 ground-motion scalar is not finite and positive.");
        }

        ulong significand = exponent == 0
            ? fraction
            : (1U << 23) | fraction;
        int binaryExponent = exponent == 0
            ? -149
            : exponent - 150;
        ulong scaled = checked(significand * (ulong)scale);
        ulong value = binaryExponent >= 0
            ? checked(scaled << binaryExponent)
            : RoundShiftRightToEven(scaled, -binaryExponent);
        return checked((int)value);
    }

    private static int FloatBitsToQ30(int bits)
    {
        uint raw = unchecked((uint)bits);
        bool negative = (raw & 0x80000000) != 0;
        int exponent = (int)((raw >> 23) & 0xFF);
        uint fraction = raw & 0x7FFFFF;
        if (exponent == 0xFF)
        {
            throw new InvalidDataException(
                "A Level 100 actor basis contains a non-finite component.");
        }

        ulong significand = exponent == 0
            ? fraction
            : (1U << 23) | fraction;
        int binaryExponent = exponent == 0
            ? -119
            : exponent - 120;
        ulong magnitude = binaryExponent >= 0
            ? checked(significand << binaryExponent)
            : RoundShiftRightToEven(significand, -binaryExponent);
        int value = checked((int)magnitude);
        return negative ? -value : value;
    }

    private static int Q30ToFloatBits(int value)
    {
        if (value == 0)
        {
            return 0;
        }

        bool negative = value < 0;
        uint magnitude = checked((uint)Math.Abs((long)value));
        int highestBit = 31 -
            System.Numerics.BitOperations.LeadingZeroCount(magnitude);
        int unbiasedExponent = highestBit - 30;
        ulong significand;
        if (highestBit <= 23)
        {
            significand = (ulong)magnitude << (23 - highestBit);
        }
        else
        {
            int shift = highestBit - 23;
            significand = RoundShiftRightToEven(magnitude, shift);
            if (significand == (1UL << 24))
            {
                significand >>= 1;
                unbiasedExponent++;
            }
        }

        int biasedExponent = unbiasedExponent + 127;
        if (biasedExponent is <= 0 or >= 0xFF)
        {
            throw new InvalidDataException(
                "A Level 100 actor basis left the supported finite range.");
        }

        uint result =
            (negative ? 0x80000000U : 0U) |
            ((uint)biasedExponent << 23) |
            ((uint)significand & 0x7FFFFF);
        return unchecked((int)result);
    }

    private static ulong RoundShiftRightToEven(
        ulong value,
        int shift)
    {
        if (shift <= 0)
        {
            return checked(value << -shift);
        }
        if (shift >= 64)
        {
            return 0;
        }

        ulong quotient = value >> shift;
        ulong mask = (1UL << shift) - 1;
        ulong remainder = value & mask;
        ulong halfway = 1UL << (shift - 1);
        return remainder > halfway ||
            (remainder == halfway && (quotient & 1) != 0)
                ? quotient + 1
                : quotient;
    }

    private static int DivideRoundNearest(
        long value,
        long denominator)
    {
        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }
        return checked((int)(value >= 0
            ? (value + (denominator / 2)) / denominator
            : -((-value + (denominator / 2)) / denominator)));
    }
}
