// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

public enum Level100ActorScriptCommandKind
{
    FollowWaypoint = 1,
    FollowWaypointWait = 2,
    SetAIState = 3,
    SetAllegiance = 4,
    Attack = 5,
    Retreat = 6,
    Stop = 7,
    SetSnowDensity = 8,
    Print = 9,
    Damage = 10,
    Land = 11,
}

public enum Level100ActorScriptWaitKind
{
    None = 0,
    Pause = 1,
    FollowWaypoint = 2,
}

public sealed record Level100ActorScriptCommand(
    long Sequence,
    int Tick,
    Level100ActorId? ActorId,
    Level100ActorScriptCommandKind Kind,
    Level100ActorId? TargetActorId,
    string? Argument,
    int Scalar);

public sealed record Level100ActorScriptEventPosted(
    long Sequence,
    int Tick,
    Level100ActorId? ActorId,
    string EventName);

public sealed record Level100ActorScriptContinuationSnapshot(
    long Sequence,
    int? DueTick,
    Level100ActorScriptWaitKind WaitKind,
    string? WaitArgument,
    Level100ScriptExecutionSnapshot Execution);

public sealed record Level100ActorScriptInstanceSnapshot(
    Level100ActorId? ActorId,
    string ProgramName,
    string ProgramSha256,
    bool Initialized,
    IReadOnlyList<Level100ScriptLocalSnapshot> Locals,
    Level100ScriptExecutionSnapshot ActiveExecution,
    IReadOnlyList<Level100QueuedEventSnapshot> QueuedEvents,
    IReadOnlyList<Level100ActorScriptContinuationSnapshot> Continuations);

public sealed record Level100ActorScriptRuntimeSnapshot(
    int Tick,
    long NextSequence,
    bool PlayerInJetMode,
    IReadOnlyList<Level100ActorScriptInstanceSnapshot> Instances,
    IReadOnlyList<Level100ActorScriptEventPosted> PendingPostedEvents,
    IReadOnlyList<Level100ActorScriptCommand> PendingCommands);

/// <summary>
/// The four values the Mission-native <c>Damage</c> wrapper at
/// <c>0x005348C0</c> was measured forwarding to the runtime receiver's damage
/// virtual (<c>vtable +0xA0</c>) through its indirect call at
/// <c>0x005348E3</c>. The wrapper itself performs no arithmetic: the life law
/// belongs to whichever virtual the receiver resolves, so this type carries the
/// amount as exact float32 bits rather than a converted value.
/// </summary>
internal readonly record struct Level100MissionDamageForward(
    Level100ActorId Receiver,
    uint AmountBits,
    bool ApplyShields,
    int MeshPartIndex)
{
    internal float Amount =>
        BitConverter.Int32BitsToSingle(unchecked((int)AmountBits));
}

/// <summary>
/// Deterministic owner for the released Level 100 actor programs. It executes
/// the hash-pinned object code and exposes movement/AI waits as typed mechanics
/// boundaries; it does not implement those mechanics or complete them by time.
/// </summary>
public sealed class Level100ActorScriptRuntime
{
    private readonly Level100ActorRegistry _actors;
    private readonly Level100ActorId _playerActorId;
    private readonly Action<Level100ActorId>? _afterSpawn;
    private readonly Func<int>? _eventTimeFloatBits;

    /// <summary>
    /// Files a thing's INIT_SCRIPT on the level event manager. With it, every
    /// init() and ready() waits for its event (the Simulation's retail path);
    /// without it, the runtime initializes at once, as its standalone tests
    /// drive it.
    /// </summary>
    private Action<Level100ActorId>? _requestInit;
    private readonly SortedDictionary<int, Instance> _instances = [];
    private readonly List<Level100ActorScriptEventPosted> _postedEvents = [];
    private readonly List<Level100ActorScriptCommand> _commands = [];
    private long _nextSequence = 1;
    private int _tick;
    private bool _playerInJetMode;
    private VehicleMode _playerMode;
    private VehicleTransition _playerTransition;
    private int _ticksSinceGroundContact;
    private int _followLoopFlag = RetailIScriptFollowWaypoint.FlagIdle;
    private int _waitStopFlag = RetailIScriptWaitStop.FlagIdle;
    private bool _initializing;
    private Instance? _setup;

    /// <summary>
    /// The world's other script carriers besides <c>LevelScript</c> (the
    /// mission's) and <c>Setup</c>: World 110's <c>Weather</c> (row 39). Like
    /// Setup they belong to no actor.
    /// </summary>
    private readonly SortedDictionary<string, Instance> _carriers = new(StringComparer.Ordinal);

    private int WorldNumber => _actors.WorldNumber;

    /// <summary>The level's gameplay stream, for <c>Rand</c>; the Simulation supplies it.</summary>
    internal Func<int>? SharedRandom { get; set; }

    /// <summary>
    /// The mission's message box, for an actor script's <c>PlayCharMessage</c>
    /// and <c>PlayCharMessageWait</c>: speaker, message and whether the script
    /// waits; returns the ticks until the message clears.
    /// </summary>
    internal Func<int, int, bool, int>? RequestMessage { get; set; }

    private IEnumerable<string> OtherCarrierNames =>
        Level100ScriptCarriers.For(WorldNumber)
            .Select(carrier => carrier.ScriptName)
            .Where(name => name is not ("LevelScript" or "Setup"));

    public Level100ActorScriptRuntime(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId) : this(actors, playerActorId, null, null)
    {
    }

    public Level100ActorScriptRuntime(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId,
        Action<Level100ActorId>? afterSpawn,
        Func<int>? eventTimeFloatBits)
    {
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _playerActorId = ValidatePlayer(playerActorId);
        _afterSpawn = afterSpawn;
        _eventTimeFloatBits = eventTimeFloatBits;
    }

    public Level100ActorScriptRuntime(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId,
        Level100ActorScriptRuntimeSnapshot snapshot)
        : this(actors, playerActorId, snapshot, null, null)
    {
    }

    public Level100ActorScriptRuntime(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId,
        Level100ActorScriptRuntimeSnapshot snapshot,
        Action<Level100ActorId>? afterSpawn,
        Func<int>? eventTimeFloatBits)
        : this(actors, playerActorId, afterSpawn, eventTimeFloatBits)
    {
        // Restore the suspended executions once; do not replay initialization.
        // Future SpawnThing calls still need the caller's admission hook and clock.
        Restore(snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
    }

    public Level100ActorScriptRuntimeSnapshot Snapshot => new(
        _tick,
        _nextSequence,
        _playerInJetMode,
        Array.AsReadOnly((_setup is null
                ? _instances.Values
                : new[] { _setup }.Concat(_carriers.Values).Concat(_instances.Values))
            .OrderBy(item => item.ActorId.Value)
            .Select(SnapshotInstance)
            .ToArray()),
        Array.AsReadOnly(_postedEvents.OrderBy(item => item.Sequence).ToArray()),
        Array.AsReadOnly(_commands.OrderBy(item => item.Sequence).ToArray()));

    /// <summary>
    /// The retail path: binds Setup and every authored script without running
    /// any <c>init()</c>. Each runs when the level event manager delivers its
    /// INIT_SCRIPT (<see cref="RunScriptInit"/>, <see cref="RunSetupInit"/>),
    /// and a script bound later by <c>SetScript</c> or <c>SpawnThing</c> has
    /// its INIT_SCRIPT filed through <paramref name="requestInit"/>.
    /// </summary>
    public void AttachReleasedScripts(Action<Level100ActorId> requestInit)
    {
        ArgumentNullException.ThrowIfNull(requestInit);
        if (_initializing || _instances.Count != 0 || _setup is not null)
        {
            throw new InvalidOperationException("Level 100 actor scripts were attached twice.");
        }

        _requestInit = requestInit;
        _setup = new Instance(default, Level100MissionProgram.LoadEmbedded(WorldNumber, "Setup"));
        foreach (string carrier in OtherCarrierNames)
        {
            _carriers.Add(carrier, new Instance(default, Level100MissionProgram.LoadEmbedded(WorldNumber, carrier)));
        }
        foreach (Level100ActorSnapshot actor in _actors.Snapshot.Actors
            .Where(actor => actor.ScriptName is not null)
            .OrderBy(actor => actor.ActorId.Value))
        {
            Attach(actor.ActorId, actor.ScriptName!);
        }
    }

    /// <summary>A thing's INIT_SCRIPT: its script's <c>init()</c>.</summary>
    internal void RunScriptInit(Level100ActorId actorId)
    {
        if (!_instances.TryGetValue(actorId.Value, out Instance? instance))
        {
            // The thing lost its script, or was deleted, before the event.
            return;
        }

        InitializeInstance(instance);
        PumpEvents(instance);
    }

    /// <summary>Another carrier's INIT_SCRIPT (World 110's Weather).</summary>
    internal void RunCarrierInit(string scriptName)
    {
        Instance carrier = _carriers.TryGetValue(scriptName, out Instance? instance)
            ? instance
            : throw new InvalidOperationException($"Carrier {scriptName} was never attached.");
        InitializeInstance(carrier);
        PumpEvents(carrier);
    }

    /// <summary>The Setup carrier's INIT_SCRIPT.</summary>
    internal void RunSetupInit()
    {
        Instance setup = _setup ?? throw new InvalidOperationException("Setup was never attached.");
        InitializeInstance(setup);
        PumpEvents(setup);
    }

    public void InitializeReleasedScripts()
    {
        if (_initializing || _instances.Count != 0)
        {
            throw new InvalidOperationException("Level 100 actor scripts were initialized twice.");
        }

        _initializing = true;
        try
        {
            // Setup is a released global program, not a world actor. Its
            // initializer assigns exact scripts to Player and named facilities.
            _setup = new Instance(default, Level100MissionProgram.LoadEmbedded(WorldNumber, "Setup"));
            InitializeInstance(_setup);

            foreach (Level100ActorSnapshot actor in _actors.Snapshot.Actors
                .Where(actor => actor.ScriptName is not null)
                .OrderBy(actor => actor.ActorId.Value))
            {
                Attach(actor.ActorId, actor.ScriptName!);
            }

            while (true)
            {
                Instance? next = _instances.Values
                    .Where(item => !item.Initialized)
                    .OrderBy(item => item.ActorId.Value)
                    .FirstOrDefault();
                if (next is null)
                {
                    break;
                }

                InitializeInstance(next);
            }
        }
        finally
        {
            _initializing = false;
        }
    }

    /// <summary>
    /// Isolated FollowWaypoint emit-command names the rebuild
    /// path. This is the <c>IScript+0x18</c> literal-1 store.
    /// FollowWaypointWait / CVM / <c>+0x1c</c> stay unclaimed.
    /// </summary>
    public int FollowLoopFlag => _followLoopFlag;

    /// <summary>
    /// Isolated <see cref="Level100MissionTiming.PauseTicks"/> names
    /// the rebuild sleep. This is the
    /// <c>mov [0x0089c800], 1</c> store on actor-script Pause.
    /// CVM snapshot / 0.05f / FollowWaypointWait stay unclaimed.
    /// </summary>
    public int WaitStopFlag => _waitStopFlag;

    public void SetPlayerInJetMode(bool inJetMode) => _playerInJetMode = inJetMode;

    public void SetPlayerFlightState(
        VehicleMode mode,
        VehicleTransition transition,
        int ticksSinceGroundContact)
    {
        _playerMode = mode;
        _playerTransition = transition;
        _ticksSinceGroundContact = ticksSinceGroundContact;
        _playerInJetMode = mode == VehicleMode.Jet;
    }

    public void AdvanceTick()
    {
        _tick = checked(_tick + 1);
        foreach (Instance instance in _instances.Values.OrderBy(item => item.ActorId.Value))
        {
            while (true)
            {
                Continuation? continuation = instance.Continuations
                    .Where(item => item.DueTick.HasValue && item.DueTick.Value <= _tick)
                    .OrderBy(item => item.DueTick)
                    .ThenBy(item => item.Sequence)
                    .FirstOrDefault();
                if (continuation is null)
                {
                    break;
                }

                instance.Continuations.Remove(continuation);
                RunExecution(instance, continuation.Execution);
            }

            PumpEvents(instance);
        }
    }

    public bool CompleteMechanicsWait(
        Level100ActorId actorId,
        Level100ActorScriptWaitKind waitKind,
        string? argument = null)
    {
        Instance instance = RequireInstance(actorId);
        Continuation? continuation = instance.Continuations
            .Where(item => !item.DueTick.HasValue &&
                           item.WaitKind == waitKind &&
                           string.Equals(item.WaitArgument, argument, StringComparison.Ordinal))
            .OrderBy(item => item.Sequence)
            .FirstOrDefault();
        if (continuation is null)
        {
            return false;
        }

        instance.Continuations.Remove(continuation);
        RunExecution(instance, continuation.Execution);
        PumpEvents(instance);
        return true;
    }

    public void AttachAndInitializeSpawnedActor(
        Level100ActorId actorId,
        string scriptName)
    {
        Level100ActorSnapshot actor = _actors.GetActor(actorId);
        if (!actor.SpawnOwnerId.HasValue ||
            !string.Equals(actor.ScriptName, scriptName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Actor {actorId} is not a matching released Level 100 spawn.");
        }

        Attach(actorId, scriptName);
        if (_requestInit is not null)
        {
            // Its construction filed its INIT_SCRIPT and ready().
            return;
        }

        Instance instance = RequireInstance(actorId);
        InitializeInstance(instance);
        RunImmediateReadyForLegacySpawn(instance);
    }

    /// <summary>Plane-owned event 2003 delivers builtin 6 to this script only.</summary>
    internal void DispatchReady(Level100ActorId actorId)
    {
        if (!_instances.TryGetValue(actorId.Value, out Instance? instance)) return;
        RunBuiltIn(instance, 6, null);
        PumpEvents(instance);
    }

    public void PublishEvent(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        foreach (Instance instance in _instances.Values.OrderBy(item => item.ActorId.Value))
        {
            QueueNamedEvent(instance, eventName);
        }
    }

    public void DispatchFact(Level100ActorFactSnapshot fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (!_instances.TryGetValue(fact.ActorId.Value, out Instance? instance))
        {
            return;
        }

        int builtInIndex = fact.Kind switch
        {
            Level100ActorFactKind.Hit => 4,
            Level100ActorFactKind.StartedDying => 5,
            Level100ActorFactKind.Died => 3,
            Level100ActorFactKind.TriggerDispatchReady => 4,
            // IScript::Shutdown (0x00533810) runs built-in 7, shutdown().
            Level100ActorFactKind.ShutDown => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };
        Level100ScriptValue? parameter = builtInIndex == 4
            ? fact.Kind == Level100ActorFactKind.TriggerDispatchReady
                ? Thing(_playerActorId)
                : fact.OtherActorId.HasValue
                    ? Thing(fact.OtherActorId.Value)
                    : Level100ScriptValue.ExternalThing(fact.OtherThingTypeMask)
            : null;
        RunBuiltIn(instance, builtInIndex, parameter);
        PumpEvents(instance);

        if (fact.Kind == Level100ActorFactKind.Died)
        {
            // `died()` is the LAST thing a released script ever runs. The
            // engine deletes the script object on the same call, before the
            // thing itself is shut down:
            //
            //   references/Onslaught/thing.cpp:711-724
            //     void CComplexThing::AddShutdownEvent()
            //     {
            //         if ((mFlags & TF_DECLARED_SHUTDOWN) == 0)
            //         {
            //             if (mMissionScript)
            //             {
            //                 mMissionScript->Died();
            //                 delete mMissionScript;
            //                 mMissionScript = NULL;
            //             }
            //         }
            //         SUPERTYPE::AddShutdownEvent();
            //     }
            //
            // (`CBattleEngine::StartDieProcess`, BattleEngine.cpp:2617-2640,
            // does the same for the player.) Every later `if (mMissionScript)`
            // in the released thing then fails silently, so a destroyed thing
            // receives no further named event, no `hit()`, and no pending
            // `Pause` continuation - the delete takes those with it.
            //
            // WHAT THIS FIXES. Without it a destroyed actor's script stayed in
            // `_instances` and `PublishEvent` kept broadcasting to it. Measured
            // on the Level 100 chain: `Target Truck #25` was destroyed before
            // `event("Activate Static Targets 2")` was posted, its `died()`
            // correctly posted `Broke Tutorial`, and then the SAME dead script
            // also ran the activation handler and called `SetObjective()` on
            // its own destroyed owner. Retail cannot reach that call at all -
            // the listener is gone - so the released outcome is the
            // `LevelLostString(LOSE_TUTORIAL_BROKE)` that `Broke Tutorial` was
            // already on its way to producing.
            _instances.Remove(fact.ActorId.Value);
        }
        else if (fact.Kind == Level100ActorFactKind.ShutDown)
        {
            // The thing leaves the world after its script's shutdown(); the
            // script object goes with it (thing.cpp:573-579).
            _instances.Remove(fact.ActorId.Value);
        }
    }

    public IReadOnlyList<Level100ActorScriptEventPosted> DrainPostedEvents()
    {
        Level100ActorScriptEventPosted[] result = _postedEvents
            .OrderBy(item => item.Sequence)
            .ToArray();
        _postedEvents.Clear();
        return Array.AsReadOnly(result);
    }

    public IReadOnlyList<Level100ActorScriptCommand> DrainCommands()
    {
        Level100ActorScriptCommand[] result = _commands
            .OrderBy(item => item.Sequence)
            .ToArray();
        _commands.Clear();
        return Array.AsReadOnly(result);
    }

    private void Restore(Level100ActorScriptRuntimeSnapshot snapshot)
    {
        if (snapshot.Tick < 0 || snapshot.NextSequence <= 0 ||
            snapshot.Instances is null || snapshot.PendingPostedEvents is null ||
            snapshot.PendingCommands is null || snapshot.Instances.Any(item => item is null) ||
            snapshot.PendingPostedEvents.Any(item => item is null) ||
            snapshot.PendingCommands.Any(item => item is null))
        {
            throw new ArgumentException("Actor-script runtime snapshot is malformed.", nameof(snapshot));
        }

        _tick = snapshot.Tick;
        _nextSequence = snapshot.NextSequence;
        _playerInJetMode = snapshot.PlayerInJetMode;
        _playerMode = snapshot.PlayerInJetMode ? VehicleMode.Jet : VehicleMode.Walker;
        _playerTransition = VehicleTransition.None;
        _ticksSinceGroundContact = 0;
        var sequences = new HashSet<long>();

        foreach (Level100ActorScriptInstanceSnapshot source in snapshot.Instances)
        {
            Level100MissionProgram program;
            try
            {
                program = Level100MissionProgram.LoadEmbedded(WorldNumber, source.ProgramName);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidDataException or FileNotFoundException)
            {
                throw new ArgumentException("Actor-script snapshot names an unknown program.", nameof(snapshot), exception);
            }
            if (!string.Equals(program.Sha256, source.ProgramSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Actor-script program identity changed.", nameof(snapshot));
            }

            Level100ActorId actorId = source.ActorId ?? default;
            if (actorId.Value == 0)
            {
                bool setup = string.Equals(program.Name, "Setup", StringComparison.Ordinal);
                if (setup ? _setup is not null
                        : !OtherCarrierNames.Contains(program.Name, StringComparer.Ordinal) ||
                          _carriers.ContainsKey(program.Name))
                {
                    throw new ArgumentException("Actor-script snapshot has an invalid global program.", nameof(snapshot));
                }
            }
            else
            {
                Level100ActorSnapshot actor = _actors.GetActor(actorId);
                if (!string.Equals(actor.ScriptName, program.Name, StringComparison.Ordinal) ||
                    _instances.ContainsKey(actorId.Value))
                {
                    throw new ArgumentException("Actor-script binding changed.", nameof(snapshot));
                }
            }

            if (RestoreExecution(source.ActiveExecution, program, snapshot) is not null)
            {
                throw new ArgumentException(
                    "Actor-script snapshot contains an execution outside a resumable wait.",
                    nameof(snapshot));
            }

            var instance = new Instance(actorId, program)
            {
                Initialized = source.Initialized,
            };
            RestoreLocals(instance, source.Locals, snapshot);

            if (source.QueuedEvents is null || source.Continuations is null ||
                source.QueuedEvents.Any(item => item is null) ||
                source.Continuations.Any(item => item is null))
            {
                throw new ArgumentException("Actor-script instance queues are malformed.", nameof(snapshot));
            }
            foreach (Level100QueuedEventSnapshot queued in source.QueuedEvents)
            {
                ValidateSequence(queued.Sequence, sequences, snapshot);
                if (string.IsNullOrEmpty(queued.EventName) ||
                    !program.NamedEventInstructionPointers.ContainsKey(queued.EventName))
                {
                    throw new ArgumentException("Actor-script queued event is not in its program.", nameof(snapshot));
                }
                instance.EventQueue.Enqueue(new QueuedEvent(queued.Sequence, queued.EventName));
            }
            foreach (Level100ActorScriptContinuationSnapshot continuation in source.Continuations)
            {
                ValidateSequence(continuation.Sequence, sequences, snapshot);
                bool validWait = continuation.WaitKind switch
                {
                    Level100ActorScriptWaitKind.Pause =>
                        continuation.DueTick.HasValue && continuation.DueTick.Value > _tick,
                    Level100ActorScriptWaitKind.FollowWaypoint =>
                        !continuation.DueTick.HasValue && !string.IsNullOrEmpty(continuation.WaitArgument),
                    _ => false,
                };
                if (!validWait)
                {
                    throw new ArgumentException("Actor-script continuation wait is invalid.", nameof(snapshot));
                }
                instance.Continuations.Add(new Continuation(
                    continuation.Sequence,
                    continuation.DueTick,
                    continuation.WaitKind,
                    continuation.WaitArgument,
                    RestoreExecution(continuation.Execution, program, snapshot) ??
                        throw new ArgumentException("Actor-script continuation has no execution.", nameof(snapshot))));
            }

            if (actorId.Value == 0)
            {
                if (string.Equals(program.Name, "Setup", StringComparison.Ordinal))
                {
                    _setup = instance;
                }
                else
                {
                    _carriers.Add(program.Name, instance);
                }
            }
            else
            {
                _instances.Add(actorId.Value, instance);
            }
        }

        // A script that has run `died()` has been deleted - see the citation in
        // `DispatchFact` - so a destroyed actor is expected to have NO
        // instance, and the binding set can no longer be an exact equality.
        // The two halves it still has to satisfy are: nothing in `_instances`
        // that is not a scripted actor, and an instance for every scripted
        // actor that has not died. A ground unit's DiedAwaitingShutdown phase
        // has already dispatched died() even though collision remains live.
        // `Destroyed` with an instance is permitted
        // only because the registry sets the lifecycle when it enqueues the
        // `Died` fact and the teardown happens when that fact is dispatched.
        var scriptedActorIds = _actors.Snapshot.Actors
            .Where(actor => actor.ScriptName is not null)
            .Select(actor => actor.ActorId.Value)
            .ToHashSet();
        int[] undestroyedScriptedActorIds = _actors.Snapshot.Actors
            .Where(actor => actor.ScriptName is not null &&
                actor.Lifecycle is Level100ActorLifecycle.Alive or
                    Level100ActorLifecycle.StartedDying)
            .Select(actor => actor.ActorId.Value)
            .ToArray();
        // A script still waiting for its INIT_SCRIPT is legitimately
        // uninitialized; the event is in the level event manager.
        if (_setup is null ||
            OtherCarrierNames.Any(name => !_carriers.ContainsKey(name)) ||
            _instances.Keys.Any(id => !scriptedActorIds.Contains(id)) ||
            undestroyedScriptedActorIds.Any(id => !_instances.ContainsKey(id)))
        {
            throw new ArgumentException(
                "Actor-script snapshot does not contain the complete released binding set.",
                nameof(snapshot));
        }

        foreach (Level100ActorScriptEventPosted posted in snapshot.PendingPostedEvents)
        {
            ValidateSequence(posted.Sequence, sequences, snapshot);
            if (posted.Tick < 0 || posted.Tick > _tick || string.IsNullOrEmpty(posted.EventName))
            {
                throw new ArgumentException("Actor-script posted event is invalid.", nameof(snapshot));
            }
            ValidateOptionalActor(posted.ActorId, snapshot);
            _postedEvents.Add(posted);
        }
        foreach (Level100ActorScriptCommand command in snapshot.PendingCommands)
        {
            ValidateSequence(command.Sequence, sequences, snapshot);
            if (command.Tick < 0 || command.Tick > _tick || !Enum.IsDefined(command.Kind))
            {
                throw new ArgumentException("Actor-script command is invalid.", nameof(snapshot));
            }
            ValidateOptionalActor(command.ActorId, snapshot);
            ValidateOptionalActor(command.TargetActorId, snapshot);
            _commands.Add(command);
        }
    }

    private void RestoreLocals(
        Instance instance,
        IReadOnlyList<Level100ScriptLocalSnapshot> locals,
        Level100ActorScriptRuntimeSnapshot snapshot)
    {
        if (locals is null || locals.Count != instance.Program.Symbols.Count ||
            locals.Any(item => item is null))
        {
            throw new ArgumentException("Actor-script locals changed shape.", nameof(snapshot));
        }
        for (int index = 0; index < locals.Count; index++)
        {
            Level100ScriptLocalSnapshot local = locals[index];
            if (local.Ordinal != index ||
                !string.Equals(local.Name, instance.Program.Symbols[index].Name, StringComparison.Ordinal))
            {
                throw new ArgumentException("Actor-script local identity changed.", nameof(snapshot));
            }
            instance.Locals[index] = RestoreValue(local.Value, snapshot);
        }
    }

    private Execution? RestoreExecution(
        Level100ScriptExecutionSnapshot source,
        Level100MissionProgram program,
        Level100ActorScriptRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Stack is null || source.CallFrames is null)
        {
            throw new ArgumentException("Actor-script execution collections are missing.", nameof(snapshot));
        }
        if (source.EventName is null)
        {
            if (source.InstructionPointer != -1 || source.Flags != 0 ||
                source.SavedStackSize != 0 || source.Abort || source.CallContext.HasValue ||
                source.Stack.Count != 0 || source.CallFrames.Count != 0)
            {
                throw new ArgumentException("Empty actor-script execution is not canonical.", nameof(snapshot));
            }
            return null;
        }
        if (source.InstructionPointer < 0 || source.InstructionPointer > program.Instructions.Count ||
            source.SavedStackSize < 0 ||
            source.SavedStackSize > source.Stack.Count ||
            source.CallFrames.Any(pointer => pointer < 0 || pointer > program.Instructions.Count))
        {
            throw new ArgumentException("Actor-script execution is invalid.", nameof(snapshot));
        }

        var execution = new Execution(source.EventName, source.InstructionPointer)
        {
            Flags = source.Flags,
            SavedStackSize = source.SavedStackSize,
            Abort = source.Abort,
            CallContext = source.CallContext.HasValue
                ? RestoreValue(source.CallContext.Value, snapshot)
                : null,
        };
        if (execution.CallContext.HasValue)
        {
            _ = execution.CallContext.Value.AsActorId();
            _ = _actors.GetActor(execution.CallContext.Value.AsActorId());
        }
        execution.Stack.AddRange(source.Stack.Select(value => RestoreValue(value, snapshot)));
        execution.CallFrames.AddRange(source.CallFrames);
        return execution;
    }

    private Level100ScriptValue RestoreValue(
        Level100ScriptValueSnapshot source,
        Level100ActorScriptRuntimeSnapshot snapshot)
    {
        if (!Enum.IsDefined(source.Type) ||
            (source.Type == Level100ScriptValueType.Float &&
                !float.IsFinite(BitConverter.Int32BitsToSingle(source.Scalar))) ||
            (source.Type == Level100ScriptValueType.Position &&
                new[] { source.Scalar, source.ComponentY, source.ComponentZ }
                    .Any(bits => !float.IsFinite(BitConverter.Int32BitsToSingle(bits)))) ||
            (source.Type == Level100ScriptValueType.Thing && source.Scalar < 0) ||
            (source.Type == Level100ScriptValueType.Thing &&
                (source.Text is not null || source.ComponentZ != 0 ||
                 (unchecked((uint)source.ComponentY) &
                    ~Level100ReleasedThingTypeMasks.ProvenBits) != 0)))
        {
            throw new ArgumentException("Actor-script value is invalid.", nameof(snapshot));
        }
        if (source.Type == Level100ScriptValueType.Thing && source.Scalar > 0)
        {
            var actorId = new Level100ActorId(source.Scalar);
            _ = _actors.GetActor(actorId);
            if (_actors.GetThingTypeMask(actorId) != unchecked((uint)source.ComponentY))
            {
                throw new ArgumentException("Actor-script thing type mask changed.", nameof(snapshot));
            }
        }
        return new Level100ScriptValue(
            source.Type,
            source.Scalar,
            source.ComponentY,
            source.ComponentZ,
            source.Text);
    }

    private void ValidateSequence(
        long sequence,
        HashSet<long> sequences,
        Level100ActorScriptRuntimeSnapshot snapshot)
    {
        if (sequence <= 0 || sequence >= _nextSequence || !sequences.Add(sequence))
        {
            throw new ArgumentException("Actor-script sequencing is invalid.", nameof(snapshot));
        }
    }

    private void ValidateOptionalActor(
        Level100ActorId? actorId,
        Level100ActorScriptRuntimeSnapshot snapshot)
    {
        if (actorId.HasValue)
        {
            try
            {
                _ = _actors.GetActor(actorId.Value);
            }
            catch (KeyNotFoundException exception)
            {
                throw new ArgumentException("Actor-script snapshot references a missing actor.", nameof(snapshot), exception);
            }
        }
    }

    private void InitializeInstance(Instance instance)
    {
        if (instance.Initialized)
        {
            return;
        }

        instance.Initialized = true;
        if (instance.Program.RunInitializer)
        {
            RunNewExecution(instance, "<global initializer>", 0);
        }

        RunBuiltIn(instance, 0, null);
    }

    /// <summary>Binds a script; false when the thing already runs it.</summary>
    private bool Attach(Level100ActorId actorId, string scriptName)
    {
        if (_instances.TryGetValue(actorId.Value, out Instance? existing))
        {
            if (!string.Equals(existing.Program.Name, scriptName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Actor {actorId} changed script after execution began.");
            }
            return false;
        }

        _instances.Add(actorId.Value, new Instance(actorId, Level100MissionProgram.LoadEmbedded(WorldNumber, scriptName)));
        return true;
    }

    private void RunBuiltIn(
        Instance instance,
        int builtInIndex,
        Level100ScriptValue? parameter)
    {
        int instructionPointer = instance.Program.BuiltInEventInstructionPointers[builtInIndex];
        if (instructionPointer < 0)
        {
            return;
        }

        var execution = new Execution($"<built-in:{builtInIndex}>", instructionPointer)
        {
            CallContext = instance.ActorId.Value > 0
                ? Thing(instance.ActorId)
                : null,
        };
        if (parameter.HasValue)
        {
            execution.Stack.Add(parameter.Value);
        }

        RunExecution(instance, execution);
    }

    private void QueueNamedEvent(Instance instance, string eventName)
    {
        if (!instance.Program.NamedEventInstructionPointers.ContainsKey(eventName))
        {
            return;
        }

        instance.EventQueue.Enqueue(new QueuedEvent(NextSequence(), eventName));
        PumpEvents(instance);
    }

    private void PumpEvents(Instance instance)
    {
        while (instance.ActiveExecution is null &&
               instance.EventQueue.TryDequeue(out QueuedEvent item))
        {
            RunNewExecution(
                instance,
                item.EventName,
                instance.Program.NamedEventInstructionPointers[item.EventName],
                seedNamedEventGuard: true);
        }
    }

    private void RunNewExecution(
        Instance instance,
        string eventName,
        int instructionPointer,
        bool seedNamedEventGuard = false)
    {
        var execution = new Execution(eventName, instructionPointer)
        {
            CallContext = instance.ActorId.Value > 0
                ? Thing(instance.ActorId)
                : null,
        };
        if (seedNamedEventGuard)
        {
            execution.Stack.Add(Level100ScriptValue.Boolean(true));
        }

        RunExecution(instance, execution);
    }

    private void RunExecution(Instance instance, Execution execution)
    {
        if (instance.ActiveExecution is not null)
        {
            throw new InvalidOperationException(
                $"Released actor script {instance.Program.Name} re-entered synchronously.");
        }

        instance.ActiveExecution = execution;
        int instructionBudget = 10_000;
        while (!execution.Stopped)
        {
            if (instructionBudget-- == 0)
            {
                throw new InvalidOperationException(
                    $"Released actor script {instance.Program.Name} exceeded its instruction budget.");
            }

            if ((uint)execution.InstructionPointer >= instance.Program.Instructions.Count)
            {
                throw new InvalidOperationException(
                    $"{instance.Program.Name} instruction pointer {execution.InstructionPointer} is invalid.");
            }

            Level100Instruction instruction = instance.Program.Instructions[execution.InstructionPointer++];
            WaitRequest wait = ExecuteInstruction(instance, execution, instruction);
            if (wait.Kind != Level100ActorScriptWaitKind.None)
            {
                execution.SavedStackSize = execution.Stack.Count;
                execution.Abort = false;
                instance.Continuations.Add(new Continuation(
                    NextSequence(),
                    wait.Kind == Level100ActorScriptWaitKind.Pause
                        ? checked(_tick + wait.Ticks)
                        : null,
                    wait.Kind,
                    wait.Argument,
                    execution.Clone()));
                instance.ActiveExecution = null;
                return;
            }
        }

        instance.ActiveExecution = null;
    }

    private WaitRequest ExecuteInstruction(
        Instance instance,
        Execution execution,
        Level100Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case 1:
                PushBinaryNumeric(execution, static (a, b) => a + b, static (a, b) => unchecked(a + b));
                break;
            case 2:
                PushBinaryNumeric(execution, static (a, b) => a - b, static (a, b) => unchecked(a - b));
                break;
            case 3:
                PushBinaryNumeric(execution, static (a, b) => a * b, static (a, b) => unchecked(a * b));
                break;
            case 5:
                execution.Stack.Add(instance.Local(instruction.Attribute));
                break;
            case 6:
                instance.SetLocal(instruction.Attribute, Pop(execution));
                break;
            case 8:
            {
                bool right = Pop(execution).AsBoolean();
                bool left = Pop(execution).AsBoolean();
                execution.Stack.Add(Level100ScriptValue.Boolean(left && right));
                break;
            }
            case 9:
                PushNumericComparison(execution, static (a, b) => a > b);
                break;
            case 10:
                PushNumericComparison(execution, static (a, b) => a < b);
                break;
            case 13:
                break;
            case 14:
                _ = Pop(execution);
                break;
            case 15:
                if (execution.Stack.Count < 2)
                {
                    throw new InvalidOperationException("Released actor-script CMP stack underflow.");
                }
                execution.Flags = ValuesEqual(execution.Stack[^2], execution.Stack[^1]) ? 1 : 0;
                break;
            case 16:
            {
                Level100ScriptValue right = Pop(execution);
                Level100ScriptValue left = Pop(execution);
                execution.Stack.Add(Level100ScriptValue.Boolean(ValuesEqual(left, right)));
                break;
            }
            case 18:
                if (execution.Flags == 0)
                {
                    execution.InstructionPointer = instruction.Attribute;
                }
                break;
            case 19:
                if (!Pop(execution).AsBoolean())
                {
                    execution.InstructionPointer = instruction.Attribute;
                }
                break;
            case 20:
                execution.InstructionPointer = instruction.Attribute;
                break;
            case 21:
                instance.SetLocalAssigned(instruction.Attribute, Peek(execution));
                break;
            case 22:
            {
                Level100ScriptValue context = Pop(execution);
                if (context.Type != Level100ScriptValueType.Thing)
                {
                    throw new InvalidOperationException("Actor-script call context is not a thing.");
                }
                execution.CallContext = context;
                break;
            }
            case 23:
                if (execution.CallFrames.Count == 0)
                {
                    execution.Stopped = true;
                }
                else
                {
                    int last = execution.CallFrames.Count - 1;
                    execution.InstructionPointer = execution.CallFrames[last];
                    execution.CallFrames.RemoveAt(last);
                }
                break;
            case 24:
                return ExecuteNativeCall(instance, execution, instruction.Attribute);
            default:
                throw new InvalidOperationException(
                    $"Opcode {instruction.Opcode} is outside the exact Level 100 actor-program set.");
        }

        return WaitRequest.None;
    }

    private WaitRequest ExecuteNativeCall(Instance instance, Execution execution, int attribute)
    {
        int command = attribute & 0xff;
        int argumentCount = (attribute >> 8) & 0xff;
        int returnCount = (attribute >> 16) & 0xff;
        if ((attribute & unchecked((int)0xff000000)) != 0 || returnCount > 1)
        {
            throw new InvalidOperationException($"Invalid actor-script call attribute 0x{attribute:x8}.");
        }

        var arguments = new Level100ScriptValue[argumentCount];
        for (int index = argumentCount - 1; index >= 0; index--)
        {
            arguments[index] = Pop(execution);
        }

        NativeResult result = InvokeNative(instance, execution, command, arguments);
        execution.CallContext = instance.ActorId.Value > 0
            ? Thing(instance.ActorId)
            : null;
        execution.Stack.Add(result.Value);
        return result.Wait;
    }

    private NativeResult InvokeNative(
        Instance instance,
        Execution execution,
        int command,
        IReadOnlyList<Level100ScriptValue> arguments)
    {
        switch (command)
        {
            case 0: // FollowWaypoint — IScript__FollowWaypoint 0x00537d70
                RequireArguments(command, arguments, 2);
                EmitCommand(instance.ActorId, Level100ActorScriptCommandKind.FollowWaypoint,
                    argument: arguments[0].AsString(), scalar: arguments[1].AsInteger());
                // Isolated emit-command names the rebuild path / flag.
                // Isolated Start names literal-1; one live store of 1
                // is not unique versus increment from 0. FollowWaypointWait
                // early-out / CVM / +0x1c / AddEvent 2000 stay unclaimed.
                _followLoopFlag = RetailIScriptFollowWaypoint.Start(_followLoopFlag);
                return NativeResult.Void;
            case 1: // FollowWaypointWait
                RequireArguments(command, arguments, 1);
                string waypoint = arguments[0].AsString();
                EmitCommand(instance.ActorId, Level100ActorScriptCommandKind.FollowWaypointWait,
                    argument: waypoint);
                return NativeResult.Mechanics(Level100ActorScriptWaitKind.FollowWaypoint, waypoint);
            case 3: // SpawnThing
                RequireArguments(command, arguments, 4);
                Level100ActorId owner = RequireContext(execution).AsActorId();
                IReadOnlyList<Level100ActorId> spawned = _actors.SpawnThing(
                    owner,
                    arguments[0].AsString(),
                    arguments[1].AsString(),
                    arguments[2].AsInteger(),
                    arguments[3].AsString(),
                    _eventTimeFloatBits?.Invoke() ?? 0);
                foreach (Level100ActorId actorId in spawned)
                {
                    // Physical/guide/destruction admission precedes the new
                    // script's initializer, just as for mission-owned spawns.
                    // On the retail path that construction also filed the
                    // script's INIT_SCRIPT and ready() for the next frame.
                    _afterSpawn?.Invoke(actorId);
                    Attach(actorId, arguments[3].AsString());
                    if (_requestInit is null)
                    {
                        Instance spawnedInstance = RequireInstance(actorId);
                        InitializeInstance(spawnedInstance);
                        RunImmediateReadyForLegacySpawn(spawnedInstance);
                    }
                }
                return NativeResult.Void;
            case 6: // Rand — IScript::Rand 0x00538230: one shared draw (0x00538237), r mod n
                RequireArguments(command, arguments, 1);
                int randomDraw = (SharedRandom ?? throw new InvalidOperationException(
                    "Rand needs the level's gameplay stream."))();
                return new NativeResult(
                    Level100ScriptValue.Integer(randomDraw % arguments[0].AsInteger()), WaitRequest.None);
            case 28: // PlayCharMessage
                RequireArguments(command, arguments, 3);
                _ = arguments[2].AsFloat();
                _ = (RequestMessage ?? throw new InvalidOperationException("Messages need the mission."))(
                    arguments[0].AsInteger(), arguments[1].AsInteger(), false);
                return NativeResult.Void;
            case 36: // PlayCharMessageWait
                RequireArguments(command, arguments, 3);
                _ = arguments[2].AsFloat();
                _waitStopFlag = RetailIScriptWaitStop.Stop(_waitStopFlag);
                int messageTicks = (RequestMessage ?? throw new InvalidOperationException("Messages need the mission."))(
                    arguments[0].AsInteger(), arguments[1].AsInteger(), true);
                return NativeResult.Pause(messageTicks, arguments[1].AsInteger());
            case 72: // Land — IScript::Land 0x005361d0: a unit's slot 93
                RequireArguments(command, arguments, 0);
                EmitCommand(RequireContext(execution).AsActorId(), Level100ActorScriptCommandKind.Land);
                return NativeResult.Void;
            case 73: // SpawnersEmpty — 0x00535a90 -> 0x004fd7e0
                RequireArguments(command, arguments, 0);
                return new NativeResult(
                    Level100ScriptValue.Boolean(SpawnersEmpty(RequireContext(execution).AsActorId())),
                    WaitRequest.None);
            case 92: // GetInitialHealth — 0x00535a30: a unit's slot 78, else 0.0
                RequireArguments(command, arguments, 0);
                Level100ActorId healthOf = RequireContext(execution).AsActorId();
                return new NativeResult(
                    Level100ScriptValue.Float(Level100ConstructionClasses.IsUnit(
                        Level100ConstructionClasses.Of(_actors.GetActor(healthOf).DefinitionName))
                        ? _actors.GetInitialHealth(healthOf)
                        : 0),
                    WaitRequest.None);
            case 4: // Pause — IScript__Pause 0x00537c70
                RequireArguments(command, arguments, 1);
                // Isolated PauseTicks names the rebuild sleep.
                // Isolated Stop names LevelScript PlayCharMessageWait.
                // TargetZone2/3/4 hit() Pause(0.5) is this store.
                // CVM snapshot / 0.05f / FollowWaypointWait stay
                // unclaimed. ChargeWeapon stays unclaimed.
                _waitStopFlag = RetailIScriptWaitStop.Stop(_waitStopFlag);
                return NativeResult.Pause(
                    Level100MissionTiming.PauseTicks(arguments[0].AsFloat()),
                    arguments[0].Scalar);
            case 5: // PostEvent
                RequireArguments(command, arguments, 1);
                string eventName = arguments[0].AsString();
                _postedEvents.Add(new Level100ActorScriptEventPosted(
                    NextSequence(), _tick, instance.ActorId.Value > 0 ? instance.ActorId : null, eventName));
                PublishEvent(eventName);
                return NativeResult.Void;
            case 11: // Print
                RequireArguments(command, arguments, 1);
                EmitCommand(instance.ActorId, Level100ActorScriptCommandKind.Print,
                    argument: arguments[0].AsString());
                return NativeResult.Void;
            case 12: // Exists
                RequireArguments(command, arguments, 1);
                return new NativeResult(
                    Level100ScriptValue.Boolean(
                        arguments[0].Type == Level100ScriptValueType.Thing && arguments[0].Scalar > 0),
                    WaitRequest.None);
            case 14: // GetThingRef
                RequireArguments(command, arguments, 1);
                Level100ActorId? reference = _actors.GetThingRef(arguments[0].AsString());
                return new NativeResult(
                    reference.HasValue
                        ? Thing(reference.Value)
                        : Level100ScriptValue.NullThing,
                    WaitRequest.None);
            case 15: // Activate
                RequireArguments(command, arguments, 0);
                _actors.Activate(RequireContext(execution).AsActorId());
                return NativeResult.Void;
            case 18: // GetHealth
                RequireArguments(command, arguments, 0);
                return new NativeResult(
                    Level100ScriptValue.Float(_actors.GetActor(RequireContext(execution).AsActorId()).Health),
                    WaitRequest.None);
            case 21: // GetPlayer
                RequireArguments(command, arguments, 1);
                return new NativeResult(
                    Thing(_playerActorId),
                    WaitRequest.None);
            case 23: // SetObjective
                InvokeObjectiveNative(
                    command,
                    RequireContext(execution).AsActorId(),
                    arguments);
                return NativeResult.Void;
            case 24: // SetAIState
                RequireArguments(command, arguments, 1);
                EmitCommand(RequireContext(execution).AsActorId(),
                    Level100ActorScriptCommandKind.SetAIState, scalar: arguments[0].AsInteger());
                return NativeResult.Void;
            case 25: // IsA
                RequireArguments(command, arguments, 1);
                Level100ScriptValue context = RequireContext(execution);
                uint requestedType = unchecked((uint)arguments[0].AsInteger());
                bool isType = (context.AsThingTypeMask() & requestedType) != 0;
                return new NativeResult(Level100ScriptValue.Boolean(isType), WaitRequest.None);
            case 29: // Deactivate
                RequireArguments(command, arguments, 0);
                _actors.Deactivate(RequireContext(execution).AsActorId());
                return NativeResult.Void;
            case 30: // UnsetObjective
                InvokeObjectiveNative(
                    command,
                    RequireContext(execution).AsActorId(),
                    arguments);
                return NativeResult.Void;
            case 31: // IsObjective
                RequireArguments(command, arguments, 0);
                return new NativeResult(
                    Level100ScriptValue.Boolean(
                        _actors.GetActor(RequireContext(execution).AsActorId()).IsObjective),
                    WaitRequest.None);
            case 41: // SetAllegiance
                RequireArguments(command, arguments, 1);
                EmitCommand(RequireContext(execution).AsActorId(),
                    Level100ActorScriptCommandKind.SetAllegiance, scalar: arguments[0].AsInteger());
                return NativeResult.Void;
            case 44: // SetSnowDensity
                RequireArguments(command, arguments, 1);
                EmitCommand(instance.ActorId, Level100ActorScriptCommandKind.SetSnowDensity,
                    scalar: arguments[0].Scalar);
                return NativeResult.Void;
            case 48: // SetScript
                RequireArguments(command, arguments, 1);
                Level100ActorId scriptActor = RequireContext(execution).AsActorId();
                string scriptName = arguments[0].AsString();
                _actors.SetScript(scriptActor, scriptName);
                // SetScript binds the VM, then files the INIT_SCRIPT at -1
                // (0x004f42b1-0x004f42da), so the bound init() runs next frame.
                if (Attach(scriptActor, scriptName))
                {
                    _requestInit?.Invoke(scriptActor);
                }
                return NativeResult.Void;
            case 49: // GetPos
                return new NativeResult(
                    InvokePositionNative(
                        command,
                        RequireContext(execution).AsActorId(),
                        arguments),
                    WaitRequest.None);
            case 69: // Damage
            {
                Level100MissionDamageForward forward = InvokeDamageNative(
                    command,
                    RequireContext(execution).AsActorId(),
                    arguments);
                EmitCommand(
                    forward.Receiver,
                    Level100ActorScriptCommandKind.Damage,
                    scalar: unchecked((int)forward.AmountBits));
                return NativeResult.Void;
            }
            case 82: // Retreat
                RequireArguments(command, arguments, 0);
                EmitCommand(RequireContext(execution).AsActorId(), Level100ActorScriptCommandKind.Retreat);
                return NativeResult.Void;
            case 86: // Attack
                RequireArguments(command, arguments, 1);
                EmitCommand(
                    RequireContext(execution).AsActorId(),
                    Level100ActorScriptCommandKind.Attack,
                    targetActorId: arguments[0].AsActorId());
                return NativeResult.Void;
            case 95: // Stop
                RequireArguments(command, arguments, 0);
                EmitCommand(RequireContext(execution).AsActorId(), Level100ActorScriptCommandKind.Stop);
                return NativeResult.Void;
            case 125: // InJetMode — IScript__InJetMode 0x005380f0
                RequireArguments(command, arguments, 0);
                return new NativeResult(
                    Level100ScriptValue.Boolean(
                        RetailIScriptInJetMode.Evaluate(
                            RequireContext(execution).AsThingTypeMask(),
                            _playerMode,
                            _playerTransition,
                            _ticksSinceGroundContact)),
                    WaitRequest.None);
            case 135: // SetPos
                _ = InvokePositionNative(
                    command,
                    RequireContext(execution).AsActorId(),
                    arguments);
                return NativeResult.Void;
            default:
                throw new InvalidOperationException(
                    $"Native call {command} is outside the exact Level 100 actor-program set.");
        }
    }

    /// <summary>
    /// Implements the bounded Mission-native position contract shared by the
    /// script dispatcher and focused parity tests. Retail safe-copy evidence
    /// proves that SetPos makes an immediate GetPos equal to its position
    /// argument; it does not prove an orientation, velocity, collision, or
    /// persistence change, so those pose fields are deliberately preserved.
    /// </summary>
    internal Level100ScriptValue InvokePositionNative(
        int command,
        Level100ActorId receiver,
        IReadOnlyList<Level100ScriptValue> arguments)
    {
        switch (command)
        {
            case 49: // GetPos
                RequireArguments(command, arguments, 0);
                return Level100ScriptValue.Position(
                    _actors.GetPose(receiver).PositionMillimeters);
            case 135: // SetPos
                RequireArguments(command, arguments, 1);
                Level100ActorPoseSnapshot pose = _actors.GetPose(receiver);
                _actors.SetPose(
                    receiver,
                    pose with
                    {
                        PositionMillimeters = arguments[0].AsPositionMillimeters(),
                    });
                return Level100ScriptValue.Integer(0);
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(command),
                    command,
                    "Expected Mission native GetPos or SetPos.");
        }
    }

    internal bool InvokeInJetModeNative(Level100ActorId receiver) =>
        RetailIScriptInJetMode.Evaluate(
            _actors.GetThingTypeMask(receiver),
            _playerMode,
            _playerTransition,
            _ticksSinceGroundContact);

    /// <summary>
    /// Isolated emit-command names the rebuild path. This is
    /// the <c>IScript+0x18</c> literal-1 store on native 0.
    /// FollowWaypointWait stays unclaimed.
    /// </summary>
    internal int InvokeFollowWaypointNative(
        Level100ActorId actor,
        string path,
        int flag)
    {
        EmitCommand(
            actor,
            Level100ActorScriptCommandKind.FollowWaypoint,
            argument: path,
            scalar: flag);
        _followLoopFlag = RetailIScriptFollowWaypoint.Start(_followLoopFlag);
        return _followLoopFlag;
    }

    /// <summary>
    /// Implements the bounded Mission-native objective-flag contract shared by
    /// the script dispatcher and focused parity tests. Retail static evidence
    /// proves that SetObjective and UnsetObjective pass literal true and false,
    /// respectively, to CThing::SetObjective; it does not establish a native
    /// return value or complete HUD/objective-system behavior.
    /// </summary>
    internal void InvokeObjectiveNative(
        int command,
        Level100ActorId receiver,
        IReadOnlyList<Level100ScriptValue> arguments)
    {
        RequireArguments(command, arguments, 0);
        bool objective = command switch
        {
            23 => true,  // SetObjective
            30 => false, // UnsetObjective
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command,
                "Expected Mission native SetObjective or UnsetObjective."),
        };
        _actors.SetObjective(receiver, objective);
    }

    /// <summary>
    /// Implements the bounded Mission-native <c>Damage</c> forwarding contract
    /// shared by the script dispatcher and focused parity tests. Retail runtime
    /// evidence proves the wrapper at <c>0x005348C0</c> hands its caller's
    /// amount to the runtime receiver's damage virtual unchanged, alongside a
    /// non-null source, <c>applyShields = 1</c>, and <c>meshPartIndex = -1</c>;
    /// the measured Turret 01 receiver resolved <c>vtable +0xA0</c> to
    /// <c>CUnit__ApplyDamage</c> at <c>0x004F9A90</c>, which refuted the rival
    /// that the wrapper hardcodes <c>CBattleEngine::Damage</c> at
    /// <c>0x0040A890</c>. Reading that same slot out of the pristine image
    /// confirms two arms of the selection: <c>CBattleEngine</c>'s vtable
    /// <c>0x005D89C4 + 0xA0</c> holds <c>0x0040A890</c> while the measured
    /// receiver's <c>0x005E24DC + 0xA0</c> holds <c>0x004F9A90</c>. A natural
    /// level-720 call adds a third: a prison building's slot 40 is
    /// <c>CBuilding__VFunc_40_004179a0</c>, which is not a damage law but a
    /// forwarder passing the same four arguments to <c>CUnit__ApplyDamage</c>.
    /// So <c>+0xA0</c> is a per-class virtual, implemented directly by some
    /// classes and forwarded by others. The wrapper
    /// therefore selects no law of its own, so this emits a typed boundary
    /// instead of subtracting life here; the receiving laws stay with
    /// <c>Level100Destruction</c> and <c>Level100PlayerDamage</c>, which own and
    /// test them separately. No Level 100 consumer applies the emitted command,
    /// because no shipped Level 100 program issues this native at all.
    /// </summary>
    /// <remarks>
    /// Two evidence layers stay separate. The virtual-dispatch and argument
    /// facts come from a forced Level 100 <c>Setup</c> program measured at
    /// amounts 1000.0 and 7.25. Shipped authored content calls this native at
    /// six sites in four levels. Those are authored call sites; the level-521
    /// opening trace proves the owning <c>hive.msl</c> is attached and live
    /// (its uniquely-authored <c>Teleport</c> executed) while this native and
    /// both damage laws stayed uncovered, so the contact handler is reachable
    /// but unfired at level start:
    /// <c>level521\hive.msl:339</c> and its 522 and
    /// 530 twins pass the float literal 0.25 to an explicit
    /// <c>THING_TYPE_BATTLE_ENGINE</c> receiver, while
    /// <c>level500\Submarine.msl:83,87</c> and <c>level720\Prison.msl:37</c>
    /// use the bare self-receiver form and pass, respectively, a
    /// <c>GetHealth()</c>-derived quotient, the integer literal 100, and
    /// <c>orig_health / 2</c>. The amount therefore shares units with
    /// <c>GetHealth</c> and is not always float-typed in source, which is why
    /// an integer argument is widened rather than rejected. Prison.msl then
    /// polls <c>GetHealth()</c> until it falls to <c>orig_health / 2</c>, so
    /// shipped content itself depends on the plain decrement that the measured
    /// life bits <c>0x3BA3D70B - 0x447A0000 = 0xC479FFAE</c> confirm without a
    /// clamp at zero. A decode of all 25 hash-pinned Level 100 objects finds no
    /// shipped Level 100 program that issues native 69 at all, so reaching this
    /// path is a property of the released VM rather than of released Level 100
    /// content. Whether the compiler emits Integer or Float for a literal such
    /// as <c>Damage(100)</c> is unmeasured, as are the source argument's
    /// identity, positive-shield absorption, death and cleanup ordering, and
    /// the natural weapon path. The August 15 level-720 query measured the
    /// natural <c>Prison.msl:37</c> call and the building receiver's forwarding
    /// arm. Its retained result is recorded in RE-INDEX.md and developer_state.json.
    /// The raw TTD recording was later retired; retained extracts do not permit
    /// new queries of that session or settle the remaining limits above.
    /// </remarks>
    internal Level100MissionDamageForward InvokeDamageNative(
        int command,
        Level100ActorId receiver,
        IReadOnlyList<Level100ScriptValue> arguments)
    {
        if (command != 69)
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command,
                "Expected Mission native Damage.");
        }

        RequireArguments(command, arguments, 1);
        Level100ScriptValue amount = arguments[0];

        // Preserve the authored float32 payload exactly. Retail forwards the
        // caller's value without conversion, so a Float argument must keep its
        // shipped bits rather than round-trip through an arithmetic widening.
        uint amountBits = amount.Type == Level100ScriptValueType.Float
            ? unchecked((uint)amount.Scalar)
            : unchecked((uint)BitConverter.SingleToInt32Bits(amount.AsFloat()));

        return new Level100MissionDamageForward(
            receiver,
            amountBits,
            ApplyShields: true,
            MeshPartIndex: -1);
    }

    private void EmitCommand(
        Level100ActorId actorId,
        Level100ActorScriptCommandKind kind,
        Level100ActorId? targetActorId = null,
        string? argument = null,
        int scalar = 0) => _commands.Add(new Level100ActorScriptCommand(
            NextSequence(), _tick, actorId.Value > 0 ? actorId : null, kind, targetActorId, argument, scalar));

    private Instance RequireInstance(Level100ActorId actorId) =>
        _instances.TryGetValue(actorId.Value, out Instance? instance)
            ? instance
            : throw new KeyNotFoundException($"Actor {actorId} has no Level 100 script instance.");

    private void RunImmediateReadyForLegacySpawn(Instance instance)
    {
        // Admitted raw Planes receive Ready only through their scheduled exit
        // handoff. Other retained actor classes still use the older bridge.
        if (_actors.GetBaseState(instance.ActorId).RetailPlane is null)
            RunBuiltIn(instance, 6, null);
    }

    private long NextSequence() => _nextSequence++;

    private Level100ScriptValue Thing(Level100ActorId actorId) =>
        Level100ScriptValue.Thing(actorId, _actors.GetThingTypeMask(actorId));

    private Level100ActorId ValidatePlayer(Level100ActorId actorId)
    {
        if (_actors.GetThingTypeMask(actorId) != Level100ReleasedThingTypeMasks.BattleEngine)
        {
            throw new ArgumentException(
                "The canonical Level 100 player must carry the released Battle Engine type bit.",
                nameof(actorId));
        }
        return actorId;
    }

    private static Level100ScriptValue RequireContext(Execution execution) =>
        execution.CallContext ?? throw new InvalidOperationException(
            "Released actor-script native method has no thing context.");

    private static void RequireArguments(
        int command,
        IReadOnlyCollection<Level100ScriptValue> arguments,
        int expected)
    {
        if (arguments.Count != expected)
        {
            throw new InvalidOperationException(
                $"Released native call {command} expected {expected} arguments, got {arguments.Count}.");
        }
    }

    private static Level100ScriptValue Pop(Execution execution)
    {
        if (execution.Stack.Count == 0)
        {
            throw new InvalidOperationException("Released actor-script stack underflow.");
        }
        int last = execution.Stack.Count - 1;
        Level100ScriptValue result = execution.Stack[last];
        execution.Stack.RemoveAt(last);
        return result;
    }

    private static Level100ScriptValue Peek(Execution execution) =>
        execution.Stack.Count > 0
            ? execution.Stack[^1]
            : throw new InvalidOperationException("Released actor-script stack underflow.");

    private static void PushBinaryNumeric(
        Execution execution,
        Func<float, float, float> floatOperation,
        Func<int, int, int> integerOperation)
    {
        Level100ScriptValue right = Pop(execution);
        Level100ScriptValue left = Pop(execution);
        execution.Stack.Add(
            left.Type == Level100ScriptValueType.Float || right.Type == Level100ScriptValueType.Float
                ? Level100ScriptValue.Float(floatOperation(left.AsFloat(), right.AsFloat()))
                : Level100ScriptValue.Integer(integerOperation(left.AsInteger(), right.AsInteger())));
    }

    private static void PushNumericComparison(
        Execution execution,
        Func<float, float, bool> comparison)
    {
        Level100ScriptValue right = Pop(execution);
        Level100ScriptValue left = Pop(execution);
        execution.Stack.Add(Level100ScriptValue.Boolean(comparison(left.AsFloat(), right.AsFloat())));
    }

    private static bool ValuesEqual(Level100ScriptValue left, Level100ScriptValue right)
    {
        if (left.Type == Level100ScriptValueType.Float || right.Type == Level100ScriptValueType.Float)
        {
            return left.AsFloat() == right.AsFloat();
        }
        if (left.Type is Level100ScriptValueType.Integer or Level100ScriptValueType.Boolean &&
            right.Type is Level100ScriptValueType.Integer or Level100ScriptValueType.Boolean)
        {
            return left.Scalar == right.Scalar;
        }
        return left.Type == right.Type && left.Scalar == right.Scalar &&
               left.ComponentY == right.ComponentY && left.ComponentZ == right.ComponentZ &&
               string.Equals(left.Text, right.Text, StringComparison.Ordinal);
    }

    private static Level100ActorScriptInstanceSnapshot SnapshotInstance(Instance instance) => new(
        instance.ActorId.Value > 0 ? instance.ActorId : null,
        instance.Program.Name,
        instance.Program.Sha256,
        instance.Initialized,
        Array.AsReadOnly(instance.Program.Symbols.Select((symbol, index) =>
            new Level100ScriptLocalSnapshot(index, symbol.Name, instance.Locals[index].Snapshot)).ToArray()),
        SnapshotExecution(instance.ActiveExecution),
        Array.AsReadOnly(instance.EventQueue.Select(item =>
            new Level100QueuedEventSnapshot(item.Sequence, item.EventName)).ToArray()),
        Array.AsReadOnly(instance.Continuations.OrderBy(item => item.Sequence).Select(item =>
            new Level100ActorScriptContinuationSnapshot(
                item.Sequence, item.DueTick, item.WaitKind, item.WaitArgument,
                SnapshotExecution(item.Execution))).ToArray()));

    private static Level100ScriptExecutionSnapshot SnapshotExecution(Execution? execution) =>
        execution is null
            ? new Level100ScriptExecutionSnapshot(
                null, -1, 0, 0, false, null,
                Array.Empty<Level100ScriptValueSnapshot>(), Array.Empty<int>())
            : new Level100ScriptExecutionSnapshot(
                execution.EventName, execution.InstructionPointer, execution.Flags,
                execution.SavedStackSize, execution.Abort,
                execution.CallContext?.Snapshot,
                execution.Stack.Select(item => item.Snapshot).ToArray(),
                execution.CallFrames.ToArray());

    private sealed class Instance
    {
        internal Instance(Level100ActorId actorId, Level100MissionProgram program)
        {
            ActorId = actorId;
            Program = program;
            Locals = program.Symbols.Select(item => item.InitialValue).ToArray();
        }

        internal Level100ActorId ActorId { get; }
        internal Level100MissionProgram Program { get; }
        internal Level100ScriptValue[] Locals { get; }
        internal bool Initialized { get; set; }
        internal Execution? ActiveExecution { get; set; }
        internal Queue<QueuedEvent> EventQueue { get; } = [];
        internal List<Continuation> Continuations { get; } = [];

        internal Level100ScriptValue Local(int ordinal) =>
            (uint)ordinal < Locals.Length
                ? Locals[ordinal]
                : throw new InvalidOperationException($"Released symbol ordinal {ordinal} is invalid.");

        internal void SetLocal(int ordinal, Level100ScriptValue value)
        {
            _ = Local(ordinal);
            Locals[ordinal] = value;
        }

        internal void SetLocalAssigned(int ordinal, Level100ScriptValue value)
        {
            Level100ScriptValue existing = Local(ordinal);
            if (existing.Type == Level100ScriptValueType.Unset || existing.Type == value.Type)
            {
                Locals[ordinal] = value;
                return;
            }
            if (existing.Type == Level100ScriptValueType.Float &&
                value.Type == Level100ScriptValueType.Integer)
            {
                Locals[ordinal] = Level100ScriptValue.Float(value.Scalar);
                return;
            }
            throw new InvalidOperationException(
                $"Released assignment cannot assign {value.Type} to {existing.Type} symbol {ordinal}.");
        }
    }

    private sealed class Execution
    {
        internal Execution(string? eventName, int instructionPointer)
        {
            EventName = eventName;
            InstructionPointer = instructionPointer;
        }
        internal string? EventName { get; }
        internal int InstructionPointer { get; set; }
        internal int Flags { get; set; }
        internal int SavedStackSize { get; set; }
        internal bool Abort { get; set; }
        internal Level100ScriptValue? CallContext { get; set; }
        internal List<Level100ScriptValue> Stack { get; } = [];
        internal List<int> CallFrames { get; } = [];
        internal bool Stopped { get; set; }

        internal Execution Clone()
        {
            var clone = new Execution(EventName, InstructionPointer)
            {
                Flags = Flags,
                SavedStackSize = SavedStackSize,
                Abort = Abort,
                CallContext = CallContext,
                Stopped = Stopped,
            };
            clone.Stack.AddRange(Stack);
            clone.CallFrames.AddRange(CallFrames);
            return clone;
        }
    }

    private readonly record struct QueuedEvent(long Sequence, string EventName);
    private sealed record Continuation(
        long Sequence,
        int? DueTick,
        Level100ActorScriptWaitKind WaitKind,
        string? WaitArgument,
        Execution Execution);

    private readonly record struct WaitRequest(
        Level100ActorScriptWaitKind Kind,
        int Ticks,
        string? Argument)
    {
        internal static WaitRequest None => new(Level100ActorScriptWaitKind.None, 0, null);
    }

    /// <summary>
    /// <c>0x004fd7e0</c>: a unit's spawners (<c>+0x18c</c>) are empty when each
    /// has run out and is idle; a unit with none is empty, and a thing that is
    /// not a unit returns false. A loaded landing craft ("Muspell Light Landing
    /// Craft", with its SpawnerA and SpawnerB) is not: Core does not deploy
    /// cargo yet, so it stays loaded.
    /// </summary>
    private bool SpawnersEmpty(Level100ActorId actorId)
    {
        string? definition = _actors.GetActor(actorId).DefinitionName;
        return Level100ConstructionClasses.IsUnit(Level100ConstructionClasses.Of(definition)) &&
            definition != "Muspell Light Landing Craft";
    }

    private readonly record struct NativeResult(Level100ScriptValue Value, WaitRequest Wait)
    {
        internal static NativeResult Void => new(Level100ScriptValue.Integer(0), WaitRequest.None);
        internal static NativeResult Pause(int ticks, int raw) => new(
            Level100ScriptValue.Integer(0),
            new WaitRequest(Level100ActorScriptWaitKind.Pause, ticks,
                raw.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        internal static NativeResult Mechanics(Level100ActorScriptWaitKind kind, string argument) => new(
            Level100ScriptValue.Integer(0), new WaitRequest(kind, 0, argument));
    }
}
