// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// Deterministic owner for the exact released Level 100 LevelScript object.
/// It implements only the opcodes and native calls reached by that payload;
/// mechanics and presentation consume commands or report explicit facts.
/// </summary>
public sealed class Level100Mission
{
    private readonly Level100MissionProgram _program;
    private readonly Level100ActorRegistry _actors;
    private readonly Level100ActorId _playerActorId;
    private readonly int _worldNumber;
    private readonly Level100ScriptValue[] _locals;
    private readonly Queue<QueuedEvent> _eventQueue = new();
    private readonly Queue<PendingMessage> _pendingMessages = new();
    private readonly List<Continuation> _continuations = [];
    private readonly List<Level100MissionEvent> _events = [];
    private readonly ObjectiveState[] _primaryObjectives =
    [
        new(1, 0, Level100PrimaryObjectiveStatus.Uninitialized),
        new(2, 0, Level100PrimaryObjectiveStatus.Uninitialized),
        new(3, 0, Level100PrimaryObjectiveStatus.Uninitialized),
        new(4, 0, Level100PrimaryObjectiveStatus.Uninitialized),
    ];
    private readonly RetailSecondaryObjectiveState _secondaryObjectives = new();

    private Execution? _activeExecution;
    private long _nextSequence = 1;
    private int _tick;
    private bool _initializerRan;
    private Level100MissionOutcome _outcome = Level100MissionOutcome.Running;
    private Level100MissionTerminalState _terminalState;
    private Level100MissionFailureReason _failureReason;
    private int _failureTextId;
    private int _terminalTicksRemaining;
    private readonly int _initialPlayerHealth;
    private int _latestPlayerHealth;
    private int _observedPlayerHealth;
    private bool _playerActive = true;
    private bool _flightModeEnabled = true;
    private int _flightModeFlag = RetailEnableFlightMode.FlagDisabled;
    private int _waitStopFlag = RetailIScriptWaitStop.FlagIdle;
    private Level100MissionWeaponAvailability _pulseCannonAvailability;
    private Level100MissionWeaponAvailability _twinVulcanAvailability;
    private Level100MissionWeaponAvailability _mechVulcanAvailability;
    private Level100MissionWeaponAvailability _missilePodAvailability;
    private string? _navigationObjective;
    private bool _tutorialIntroduction;
    private bool _tutorialPulseCannon;
    private bool _tutorialVulcanCannon;
    private bool _tutorialStatusBars;
    private int _scoreDelta;
    private int _gameScore = RetailAddScore.LoadLevelZero;
    private readonly Dictionary<int, int> _hudPartWords = new();
    private readonly Dictionary<Level100MissionWeapon, int> _weaponActiveWords = new();
    private readonly Dictionary<int, int> _thingFlagWords = new();

    /// <summary>
    /// The Core tick the most recently scheduled character message clears, or
    /// <see cref="int.MinValue"/> before the first one. The released message
    /// box owns this, not the script: see
    /// <see cref="Level100MissionTiming.MessageBoxAllowedTick"/> and
    /// <see cref="Level100MissionTiming.MessageAdvanceDelayTicks"/>.
    /// </summary>
    private int _messageClearTick = int.MinValue;

    /// <summary>
    /// The first Core tick on which the released message box may make a
    /// character message active, i.e. the tick
    /// <c>ALLOWED_TO_PLAY_MESSAGES</c> lands.
    /// </summary>
    /// <remarks>
    /// <c>CGame::StartPlayingState</c>
    /// (<c>references/Onslaught/game.cpp:3025-3031</c>) posts that event
    /// <c>NEXT_FRAME</c>, so this is
    /// <c>playingStateStartTick + ReleasedEventFrameTicks</c>. It starts at
    /// <see cref="Level100MissionTiming.MessageBoxAllowedTickFor"/> — the
    /// value for a pan that is allowed to run its full length after the
    /// pre-run — and <see cref="NotifyPlayingStateStarted"/> moves it earlier
    /// when the player skips the pan. It is canonical simulation state, so it
    /// is hashed.
    /// </remarks>
    private int _messageBoxAllowedTick;

    public Level100Mission(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId,
        Level100TutorialProgress tutorialProgress = default,
        int initialPlayerHealth = SimulationConstants.MaximumHull,
        int worldNumber = Level100MissionProgram.WorldNumber100)
        : this(actors, playerActorId, tutorialProgress, initialPlayerHealth, worldNumber, runInit: true)
    {
    }

    /// <param name="runInit">
    /// False on the retail path: LevelScript's <c>init()</c> then waits for
    /// its carrier's INIT_SCRIPT (<see cref="RunInit"/>), which the first
    /// flush delivers at the carrier's row position.
    /// </param>
    internal Level100Mission(
        Level100ActorRegistry actors,
        Level100ActorId playerActorId,
        Level100TutorialProgress tutorialProgress,
        int initialPlayerHealth,
        int worldNumber,
        bool runInit)
    {
        if (initialPlayerHealth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPlayerHealth));
        }

        if (worldNumber != Level100MissionProgram.WorldNumber100 &&
            worldNumber != Level100MissionProgram.WorldNumber110)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldNumber),
                $"World {worldNumber} has no admitted mission session.");
        }

        _worldNumber = worldNumber;
        _messageBoxAllowedTick = Level100MissionTiming.MessageBoxAllowedTickFor(worldNumber);
        _program = Level100MissionProgram.LoadEmbedded(worldNumber, "LevelScript");
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _playerActorId = playerActorId;
        if (_actors.GetThingTypeMask(_playerActorId) !=
            Level100ReleasedThingTypeMasks.BattleEngine)
        {
            throw new ArgumentException(
                "The canonical Level 100 player must carry the released Battle Engine type bit.",
                nameof(playerActorId));
        }

        if (_actors.Definitions.WorldNumber != worldNumber)
        {
            throw new ArgumentException(
                $"The actor definitions carry world {_actors.Definitions.WorldNumber}, not the requested world {worldNumber}.",
                nameof(actors));
        }

        _locals = _program.Symbols.Select(symbol => symbol.InitialValue).ToArray();
        _initialPlayerHealth = initialPlayerHealth;
        _latestPlayerHealth = initialPlayerHealth;
        _observedPlayerHealth = initialPlayerHealth;
        _tutorialIntroduction = tutorialProgress.Introduction;
        _tutorialPulseCannon = tutorialProgress.PulseCannon;
        _tutorialVulcanCannon = tutorialProgress.VulcanCannon;
        _tutorialStatusBars = tutorialProgress.StatusBars;

        RunNewExecution("<global initializer>", 0);
        _initializerRan = true;
        if (runInit)
        {
            RunInit();
        }
    }

    /// <summary>LevelScript's INIT_SCRIPT: its <c>init()</c>, once.</summary>
    internal void RunInit()
    {
        if (_initRan)
        {
            throw new InvalidOperationException("LevelScript's init() already ran.");
        }

        _initRan = true;
        RunNewExecution("init", _program.BuiltInEventInstructionPointers[0]);
    }

    private bool _initRan;


    /// <summary>The career world whose hash-pinned mission program is running.</summary>
    public int WorldNumber => _worldNumber;

    public Level100MissionSnapshot Snapshot => new(
        _tick,
        _program.Sha256,
        _worldNumber,
        _initializerRan,
        _outcome == Level100MissionOutcome.Running,
        _nextSequence,
        SnapshotExecution(_activeExecution),
        _program.Symbols
            .Select((symbol, ordinal) => new Level100ScriptLocalSnapshot(
                ordinal,
                symbol.Name,
                _locals[ordinal].Snapshot))
            .ToArray(),
        _eventQueue.Select(item => new Level100QueuedEventSnapshot(
            item.Sequence,
            item.EventName)).ToArray(),
        _continuations
            .OrderBy(item => item.DueTick)
            .ThenBy(item => item.Sequence)
            .Select(item => new Level100ScriptContinuationSnapshot(
                item.Sequence,
                item.DueTick,
                item.WaitKind,
                item.WaitArgument,
                SnapshotExecution(item.Execution)))
            .ToArray(),
        _outcome,
        _terminalState,
        _failureReason,
        _failureTextId,
        _terminalTicksRemaining,
        _initialPlayerHealth,
        _latestPlayerHealth,
        _observedPlayerHealth,
        _playerActive,
        _flightModeEnabled,
        _pulseCannonAvailability,
        _twinVulcanAvailability,
        _mechVulcanAvailability,
        _missilePodAvailability,
        _navigationObjective,
        LocalBoolean(10),
        LocalBoolean(8),
        LocalInteger(4),
        _scoreDelta,
        new Level100TutorialProgress(
            _tutorialIntroduction,
            _tutorialPulseCannon,
            _tutorialVulcanCannon,
            _tutorialStatusBars),
        _primaryObjectives.Select(item => new Level100PrimaryObjectiveSnapshot(
            item.Objective,
            item.TextId,
            item.Status)).ToArray(),
        _secondaryObjectives.Snapshot,
        _events.ToArray(),
        _messageClearTick,
        _pendingMessages.Select(item => item.Message).ToArray(),
        _messageBoxAllowedTick);

    public Level100MissionOutcome Outcome => _outcome;

    /// <summary>
    /// <c>CGame+0xf4</c> — the <c>add [0x008a9b8c], eax</c>
    /// destination. Isolated <see cref="Level100MissionSnapshot.ScoreDelta"/>
    /// names the rebuild accumulator, not this store.
    /// </summary>
    public int GameScore => _gameScore;

    /// <summary>
    /// <c>CBattleEngine+0x58c</c> — the
    /// <c>mov dword ptr [ecx+0x58c], 1</c> destination.
    /// Isolated <see cref="Level100MissionSnapshot.FlightModeEnabled"/>
    /// names the rebuild bool, not this store.
    /// </summary>
    public int FlightModeFlag => _flightModeFlag;

    /// <summary>
    /// <c>0x0089c800</c> — the
    /// <c>mov dword ptr [0x0089c800], 1</c> destination
    /// on Pause / PlayCharMessageWait. Isolated
    /// <see cref="Level100MissionTiming.PauseTicks"/> /
    /// <see cref="Level100MissionTiming.MessagePlaybackTicks"/>
    /// name the rebuild sleep, not this store.
    /// </summary>
    public int WaitStopFlag => _waitStopFlag;

    /// <summary>
    /// <c>dword [index*4+0x008aa51c]</c> — Highlight stores 2,
    /// UnHighlight stores 1. Isolated
    /// <see cref="Level100HudEmphasisChanged.Emphasized"/> names
    /// the rebuild bool, not this store. A part this mission
    /// never wrote reads 0 here; that is not a BSS-init claim.
    /// </summary>
    public int HudPartWord(int partIndex) =>
        _hudPartWords.TryGetValue(partIndex, out int word) ? word : 0;

    /// <summary>
    /// <c>CWeapon+0x9c</c> — the
    /// <c>mov dword ptr [edi+0x9c], 1</c> destination.
    /// Isolated <see cref="Level100WeaponAvailabilityChanged.Enabled"/>
    /// names the rebuild bool, not this store. A weapon this
    /// mission never Enabled reads 0 here; that is not a
    /// start-active or Disable store-0 claim.
    /// </summary>
    public int WeaponActiveWord(Level100MissionWeapon weapon) =>
        _weaponActiveWords.TryGetValue(weapon, out int word) ? word : 0;

    /// <summary>
    /// <c>CThing+0x2c</c> — SetObjective ORs 0x20,
    /// UnsetObjective ANDs ~0x20. Isolated
    /// <see cref="Level100ActorSnapshot.IsObjective"/> and
    /// <see cref="Level100MissionSnapshot.NavigationObjective"/>
    /// name the rebuild bool / string, not this store. A
    /// thing this mission never marked reads 0 here; that
    /// is not a noticeboard or TF_DYING claim.
    /// </summary>
    public int ThingFlagWord(string thingName)
    {
        ArgumentException.ThrowIfNullOrEmpty(thingName);
        Level100ActorId? actorId = _actors.GetThingRef(thingName);
        if (!actorId.HasValue)
        {
            throw new InvalidOperationException(
                $"Released Level 100 requested unknown thing '{thingName}'.");
        }

        return ThingFlagWord(actorId.Value);
    }

    /// <summary>
    /// <c>CThing+0x2c</c> keyed by actor. See
    /// <see cref="ThingFlagWord(string)"/>.
    /// </summary>
    public int ThingFlagWord(Level100ActorId actorId) =>
        _thingFlagWords.TryGetValue(actorId.Value, out int word) ? word : 0;

    internal bool GameplayPaused => Level100MissionTiming.GameplayPaused(
        _outcome,
        _failureReason,
        _terminalTicksRemaining);

    internal bool GameplayPausesOnNextTick =>
        Level100MissionTiming.GameplayPausesOnNextTick(
            _outcome,
            _failureReason,
            _terminalTicksRemaining);

    /// <summary>
    /// <c>CGame::StartPlayingState</c>
    /// (<c>references/Onslaught/game.cpp:3025-3031</c>): the game leaves
    /// <c>GAME_STATE_PANNING</c> and the message box is allowed to play from
    /// the next released event frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Idempotent by taking the earlier value, which is the deterministic
    /// equivalent of retail's <c>if (mGameState == GAME_STATE_PLAYING)
    /// return;</c> guard at <c>game.cpp:3027</c>: the pan can only ever end
    /// early, never late, so a second notification can never move the gate
    /// back.
    /// </para>
    /// <para>
    /// <b>Moving the gate has to re-base what is already queued against it.</b>
    /// Retail's message box holds a queue and starts the head the moment
    /// <c>ALLOWED_TO_PLAY_MESSAGES</c> lands; nothing precomputes a wall-clock
    /// start. Core reformulates that as precomputed ticks, which is equivalent
    /// only while the gate is known at scheduling time — and the released
    /// script schedules its first messages during the pan, before the player
    /// has decided whether to sit through it.
    /// </para>
    /// <para>
    /// The re-base is exact rather than approximate. At any tick of the pan no
    /// message can have become active, because every scheduled start is at or
    /// after the old gate and the old gate is after the end of the pan. So each
    /// of the three quantities below is <c>oldGate + k</c> for some <c>k</c>
    /// that does not depend on the gate, and subtracting the delta yields
    /// <c>newGate + k</c>:
    /// </para>
    /// <list type="bullet">
    ///   <item>queued message start ticks;</item>
    ///   <item><see cref="_messageClearTick"/>, the box's own cursor;</item>
    ///   <item>continuations with
    ///   <see cref="Level100ScriptWaitKind.CharacterMessage"/>, whose due tick
    ///   IS a message-clear tick — <c>PlayCharMessageWait</c> sleeps exactly
    ///   until the box is done.</item>
    /// </list>
    /// <para>
    /// <see cref="Level100ScriptWaitKind.Pause"/> continuations are
    /// deliberately left alone: a script <c>Pause(seconds)</c> is an
    /// event-manager delay with no relationship to the message box.
    /// </para>
    /// <para>
    /// <b>Mission time is not simulation time.</b> <c>SimActions.Reset</c>
    /// rebuilds this object with a fresh clock while
    /// <see cref="Simulation"/>'s tick keeps running, so the gate has to be
    /// computed from this object's own tick. Passing the simulation tick would
    /// make every post-reset skip land after the initial gate and be silently
    /// discarded. <see cref="Simulation.Step"/> calls this before
    /// <see cref="AdvanceTick"/>, so the mission tick that is beginning is
    /// <c>_tick + 1</c>.
    /// </para>
    /// </remarks>
    public void NotifyPlayingStateStarted()
    {
        int allowed = checked(
            _tick + 1 + Level100MissionTiming.ReleasedEventFrameTicks);
        if (allowed >= _messageBoxAllowedTick)
        {
            return;
        }

        int shift = _messageBoxAllowedTick - allowed;
        _messageBoxAllowedTick = allowed;

        if (_messageClearTick != int.MinValue)
        {
            _messageClearTick = checked(_messageClearTick - shift);
        }

        PendingMessage[] queued = _pendingMessages.ToArray();
        _pendingMessages.Clear();
        foreach (PendingMessage item in queued)
        {
            int startTick = checked(item.StartTick - shift);
            if (startTick <= _tick)
            {
                throw new InvalidOperationException(
                    "The released message box cannot start a message before the game leaves GAME_STATE_PANNING.");
            }

            _pendingMessages.Enqueue(new PendingMessage(
                startTick,
                item.Message with { Tick = startTick }));
        }

        for (int index = 0; index < _continuations.Count; index++)
        {
            Continuation continuation = _continuations[index];
            if (continuation.WaitKind == Level100ScriptWaitKind.CharacterMessage)
            {
                _continuations[index] = continuation with
                {
                    DueTick = checked(continuation.DueTick - shift),
                };
            }
        }
    }

    public IReadOnlyList<Level100MissionEvent> DrainEvents()
    {
        Level100MissionEvent[] result = _events.ToArray();
        _events.Clear();
        return result;
    }

    /// <summary>
    /// Bounded deterministic instrument for the exact world-110 authored
    /// native-84 sequence at instructions 64..67. The ordinary session remains
    /// suspended at its first Pause; this executes only the two constant loads,
    /// the native call, and its void-result pop against that hash-pinned program.
    /// </summary>
    internal void RunWorld110SecondaryObjectiveCompleteInstrument()
    {
        const int FirstInstruction = 64;
        const int EndInstruction = 68;
        const string World110LevelScriptSha256 =
            "f5c157ba2c6a9acbee78d895a25be82252951b93bdfdd8886a79ecd7bfe222aa";

        Continuation? openingWait = _continuations.SingleOrDefault();
        if (_worldNumber != Level100MissionProgram.WorldNumber110 ||
            !StringComparer.Ordinal.Equals(_program.Sha256, World110LevelScriptSha256) ||
            _tick != 0 ||
            _activeExecution is not null ||
            openingWait is null ||
            openingWait.WaitKind != Level100ScriptWaitKind.Pause ||
            openingWait.Execution.InstructionPointer != 35)
        {
            throw new InvalidOperationException(
                "The world-110 native-84 instrument requires the untouched first-Pause session.");
        }

        var execution = new Execution(
            "<world-110 native-84 instrument>",
            FirstInstruction);
        while (execution.InstructionPointer < EndInstruction)
        {
            Level100Instruction instruction =
                _program.Instructions[execution.InstructionPointer++];
            WaitRequest wait = ExecuteInstruction(execution, instruction);
            if (wait.Kind != Level100ScriptWaitKind.None)
            {
                throw new InvalidOperationException(
                    "The authored world-110 native-84 instrument unexpectedly waited.");
            }
        }

        if (execution.InstructionPointer != EndInstruction ||
            execution.Stack.Count != 0 ||
            execution.CallContext is not null ||
            execution.Stopped)
        {
            throw new InvalidOperationException(
                "The authored world-110 native-84 sequence did not consume two arguments and one void result.");
        }
    }

    public void AdvanceTick(int latestPlayerHealth)
    {
        if (latestPlayerHealth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(latestPlayerHealth));
        }

        _tick = checked(_tick + 1);
        _latestPlayerHealth = latestPlayerHealth;

        if (_outcome != Level100MissionOutcome.Running)
        {
            AdvanceTerminalCountdown();
            return;
        }

        ReleaseDueMessages();

        while (_outcome == Level100MissionOutcome.Running)
        {
            Continuation? next = _continuations
                .Where(item => item.DueTick <= _tick)
                .OrderBy(item => item.DueTick)
                .ThenBy(item => item.Sequence)
                .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            _continuations.Remove(next);
            RunExecution(next.Execution);
        }

        PumpEventQueue();
    }

    public bool SubmitInput(Level100MissionInput input) => input switch
    {
        Level100MissionInput.BrokeTutorial => QueueNamedEvent("Broke Tutorial"),
        Level100MissionInput.HitFriendlyBuilding =>
            QueueNamedEvent("Hit Friendly Building"),
        Level100MissionInput.FriendlyBuildingDestroyed =>
            QueueNamedEvent("Friendly Building Destroyed"),
        // This is the released mismatch: Facilities/Turret emit this spelling,
        // while LevelScript has no matching listener.
        Level100MissionInput.DestroyedFriendlyBuilding => false,
        Level100MissionInput.FlashThings => QueueNamedEvent("Flash things"),
        Level100MissionInput.HelpPlayer => QueueNamedEvent("Help Player"),
        Level100MissionInput.AbortAirborneDrones =>
            QueueNamedEvent("Abort Airborne Drones"),
        Level100MissionInput.EvadeFailed => QueueNamedEvent("Evade Failed"),
        _ => false,
    };

    public bool ReportPlayerHitDuringEvasion() => SubmitInput(Level100MissionInput.EvadeFailed);

    public bool ReportFriendlyBuildingHit() =>
        SubmitInput(Level100MissionInput.HitFriendlyBuilding);

    public bool ReportFriendlyBuildingDestroyed() =>
        SubmitInput(Level100MissionInput.FriendlyBuildingDestroyed);

    public bool ReportDestroyedFriendlyBuilding() =>
        SubmitInput(Level100MissionInput.DestroyedFriendlyBuilding);

    public bool ReportHelpPlayer() => SubmitInput(Level100MissionInput.HelpPlayer);

    public bool ReportAbortAirborneDrones() =>
        SubmitInput(Level100MissionInput.AbortAirborneDrones);

    public bool QueueExternalEvent(string eventName)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        return QueueNamedEvent(eventName);
    }

    public bool ReportPlayerDeath() => DeclareExternalLoss(Level100MissionFailureReason.PlayerDeath);

    public bool ReportWaterLoss() => DeclareExternalLoss(Level100MissionFailureReason.WaterLoss);

    private bool QueueNamedEvent(string eventName)
    {
        if (_outcome != Level100MissionOutcome.Running ||
            !_program.NamedEventInstructionPointers.ContainsKey(eventName))
        {
            return false;
        }

        _eventQueue.Enqueue(new QueuedEvent(NextSequence(), eventName));
        PumpEventQueue();
        return true;
    }

    private void PumpEventQueue()
    {
        while (_outcome == Level100MissionOutcome.Running &&
               _activeExecution is null &&
               _eventQueue.TryDequeue(out QueuedEvent item))
        {
            RunNewExecution(
                item.EventName,
                _program.NamedEventInstructionPointers[item.EventName],
                seedNamedEventGuard: true);
        }
    }

    private void RunNewExecution(
        string eventName,
        int instructionPointer,
        bool seedNamedEventGuard = false)
    {
        if (_outcome != Level100MissionOutcome.Running)
        {
            return;
        }

        var execution = new Execution(eventName, instructionPointer);
        if (seedNamedEventGuard)
        {
            // Every released named-event record declares one dispatch
            // parameter. Level 100's event body consumes that true dispatch
            // predicate in its leading JMPFALSE instruction.
            execution.Stack.Add(Level100ScriptValue.Boolean(true));
        }

        RunExecution(execution);
    }

    private void RunExecution(Execution execution)
    {
        if (_activeExecution is not null)
        {
            throw new InvalidOperationException("LevelScript execution re-entered synchronously.");
        }

        _activeExecution = execution;
        int instructionBudget = 10_000;
        while (!execution.Stopped && _outcome == Level100MissionOutcome.Running)
        {
            if (instructionBudget-- == 0)
            {
                throw new InvalidOperationException("The released LevelScript exceeded its instruction budget.");
            }

            if ((uint)execution.InstructionPointer >= _program.Instructions.Count)
            {
                throw new InvalidOperationException(
                    $"LevelScript instruction pointer {execution.InstructionPointer} is invalid.");
            }

            Level100Instruction instruction = _program.Instructions[execution.InstructionPointer++];
            WaitRequest wait = ExecuteInstruction(execution, instruction);
            if (wait.Kind != Level100ScriptWaitKind.None)
            {
                execution.SavedStackSize = execution.Stack.Count;
                execution.Abort = false;
                _continuations.Add(new Continuation(
                    NextSequence(),
                    checked(_tick + wait.Ticks),
                    wait.Kind,
                    wait.Argument,
                    execution.Clone()));
                _activeExecution = null;
                return;
            }
        }

        _activeExecution = null;
        if (_outcome != Level100MissionOutcome.Running)
        {
            _eventQueue.Clear();
            _continuations.Clear();
            _pendingMessages.Clear();
        }
    }

    private WaitRequest ExecuteInstruction(Execution execution, Level100Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case 1:
                PushBinaryNumeric(execution, static (left, right) => left + right,
                    static (left, right) => unchecked(left + right));
                break;
            case 2:
                PushBinaryNumeric(execution, static (left, right) => left - right,
                    static (left, right) => unchecked(left - right));
                break;
            case 3:
                PushBinaryNumeric(execution, static (left, right) => left * right,
                    static (left, right) => unchecked(left * right));
                break;
            case 5:
                execution.Stack.Add(Local(instruction.Attribute));
                break;
            case 6:
                SetLocal(instruction.Attribute, Pop(execution));
                break;
            case 9:
                PushNumericComparison(execution, static (left, right) => left > right);
                break;
            case 10:
                PushNumericComparison(execution, static (left, right) => left < right);
                break;
            case 13:
                break;
            case 14:
                _ = Pop(execution);
                break;
            case 16:
            {
                Level100ScriptValue right = Pop(execution);
                Level100ScriptValue left = Pop(execution);
                execution.Stack.Add(Level100ScriptValue.Boolean(ValuesEqual(left, right)));
                break;
            }
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
                SetLocalAssigned(instruction.Attribute, Peek(execution));
                break;
            case 22:
            {
                Level100ScriptValue context = Pop(execution);
                if (context.Type != Level100ScriptValueType.Thing)
                {
                    throw new InvalidOperationException("LevelScript call context is not a thing.");
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
                return ExecuteNativeCall(execution, instruction.Attribute);
            default:
                throw new InvalidOperationException(
                    $"Opcode {instruction.Opcode} is outside the released Level 100 runtime slice.");
        }

        return WaitRequest.None;
    }

    private WaitRequest ExecuteNativeCall(Execution execution, int attribute)
    {
        int command = attribute & 0xff;
        int argumentCount = (attribute >> 8) & 0xff;
        int returnCount = (attribute >> 16) & 0xff;
        if ((attribute & unchecked((int)0xff000000)) != 0 || returnCount > 1)
        {
            throw new InvalidOperationException($"Invalid released native-call attribute 0x{attribute:x8}.");
        }

        var arguments = new Level100ScriptValue[argumentCount];
        for (int index = argumentCount - 1; index >= 0; index--)
        {
            arguments[index] = Pop(execution);
        }

        NativeResult result = InvokeNative(execution, command, arguments);
        execution.CallContext = null;
        execution.Stack.Add(result.Value);
        return result.Wait;
    }

    private NativeResult InvokeNative(
        Execution execution,
        int command,
        IReadOnlyList<Level100ScriptValue> arguments)
    {
        switch (command)
        {
            case 3: // SpawnThing
                RequireArguments(command, arguments, 4);
                _events.Add(new Level100SpawnThingRequested(
                    _tick,
                    RequireContext(execution),
                    arguments[0].AsString(),
                    arguments[1].AsString(),
                    arguments[2].AsInteger(),
                    arguments[3].AsString()));
                return NativeResult.Void;
            case 4: // Pause — IScript__Pause 0x00537c70
                RequireArguments(command, arguments, 1);
                StoreWaitStop();
                return NativeResult.Waiting(
                    Level100ScriptWaitKind.Pause,
                    Level100MissionTiming.PauseTicks(arguments[0].AsFloat()),
                    arguments[0].Scalar);
            case 5: // PostEvent
                RequireArguments(command, arguments, 1);
                PostEvent(arguments[0].AsString());
                return NativeResult.Void;
            case 9: // LevelWon
                RequireArguments(command, arguments, 0);
                DeclareWon();
                return NativeResult.Void;
            case 14: // GetThingRef
                RequireArguments(command, arguments, 1);
                string thingName = arguments[0].AsString();
                Level100ActorId? thing = _actors.GetThingRef(thingName);
                return new NativeResult(
                    thing.HasValue
                        ? Level100ScriptValue.Thing(
                            thing.Value,
                            _actors.GetThingTypeMask(thing.Value))
                        : Level100ScriptValue.NullThing,
                    WaitRequest.None);
            case 15: // Activate
                RequireArguments(command, arguments, 0);
                SetActorActivation(RequireContext(execution), true);
                return NativeResult.Void;
            case 18: // GetHealth
                RequireArguments(command, arguments, 0);
                _ = RequireContext(execution);
                _observedPlayerHealth = _latestPlayerHealth;
                return new NativeResult(
                    Level100ScriptValue.Float(_observedPlayerHealth),
                    WaitRequest.None);
            case 21: // GetPlayer
                RequireArguments(command, arguments, 1);
                return new NativeResult(
                    Level100ScriptValue.Thing(
                        _playerActorId,
                        _actors.GetThingTypeMask(_playerActorId)),
                    WaitRequest.None);
            case 23: // SetObjective — IScript__SetObjective 0x00535ed0
                RequireArguments(command, arguments, 0);
                SetNavigationObjective(RequireContext(execution));
                return NativeResult.Void;
            case 28: // PlayCharMessage
                RequireArguments(command, arguments, 3);
                RequestMessage(arguments, waits: false);
                return NativeResult.Void;
            case 29: // Deactivate
                RequireArguments(command, arguments, 0);
                SetActorActivation(RequireContext(execution), false);
                return NativeResult.Void;
            case 30: // UnsetObjective — IScript__UnsetObjective 0x00535ee0
                RequireArguments(command, arguments, 0);
                UnsetNavigationObjective(RequireContext(execution));
                return NativeResult.Void;
            case 34: // HighlightHudPart — IScript__HighlightHudPart 0x00535e60
                RequireArguments(command, arguments, 1);
                StoreHudPartWord(arguments[0].AsInteger(), highlight: true);
                return NativeResult.Void;
            case 35: // UnHighlightHudPart — IScript__UnHighlightHudPart 0x00535e80
                RequireArguments(command, arguments, 1);
                StoreHudPartWord(arguments[0].AsInteger(), highlight: false);
                return NativeResult.Void;
            case 11: // Print — IScript::Print 0x00537ad0 writes the debug log only
                RequireArguments(command, arguments, 1);
                return NativeResult.Void;
            case 40: // GetNumUnits — IScript::GetNumUnits 0x00535590
                RequireArguments(command, arguments, 2);
                return new NativeResult(
                    Level100ScriptValue.Integer(_actors.CountUnits(arguments[0].AsInteger(), arguments[1].AsInteger())),
                    WaitRequest.None);
            case 36: // PlayCharMessageWait
                RequireArguments(command, arguments, 3);
                int messageTicks = RequestMessage(arguments, waits: true);
                return NativeResult.Waiting(
                    Level100ScriptWaitKind.CharacterMessage,
                    messageTicks,
                    arguments[1].AsInteger());
            case 83: // PrimaryObjectiveComplete
                RequireArguments(command, arguments, 2);
                SetPrimaryObjective(arguments, Level100PrimaryObjectiveStatus.Complete);
                return NativeResult.Void;
            case 84: // SecondaryObjectiveComplete — IScript__SecondaryObjectiveComplete 0x00534410
                RequireArguments(command, arguments, 2);
                _secondaryObjectives.SetComplete(
                    arguments[0].AsInteger(),
                    arguments[1].AsInteger());
                return NativeResult.Void;
            case 85: // AddScore — IScript__AddScore 0x005343c0
                RequireArguments(command, arguments, 1);
                int delta = arguments[0].AsInteger();
                _scoreDelta = unchecked(_scoreDelta + delta);
                // Isolated ScoreDelta names the rebuild accumulator.
                // Isolated FillOut ScoreWord copies a parameterized
                // dword. First-play elapsed / FillOut score stay
                // unclaimed. Live GAME.mSlots stay unclaimed.
                _gameScore = RetailAddScore.Add(_gameScore, delta);
                _events.Add(new Level100ScoreChanged(_tick, delta, _scoreDelta));
                return NativeResult.Void;
            case 87: // PrimaryObjectiveFailed
                RequireArguments(command, arguments, 2);
                SetPrimaryObjective(arguments, Level100PrimaryObjectiveStatus.Failed);
                return NativeResult.Void;
            case 88: // SecondaryObjectiveFailed — IScript__SecondaryObjectiveFailed 0x00534470
                RequireArguments(command, arguments, 2);
                _secondaryObjectives.SetFailed(
                    arguments[0].AsInteger(),
                    arguments[1].AsInteger());
                return NativeResult.Void;
            case 98: // EnableWeapon — IScript__EnableWeapon 0x00534fb0
                RequireArguments(command, arguments, 1);
                SetWeapon(arguments[0].AsString(), true);
                return NativeResult.Void;
            case 99: // DisableWeapon
                RequireArguments(command, arguments, 1);
                SetWeapon(arguments[0].AsString(), false);
                return NativeResult.Void;
            case 100: // EnableFlightMode — IScript__EnableFlightMode 0x00535070
                RequireArguments(command, arguments, 0);
                // Isolated FlightModeEnabled names the rebuild bool.
                // Isolated Enable names literal-1; one live store of 1
                // is not unique versus increment from 0. Disable's
                // clear / morph stay unclaimed. ChargeWeapon stays
                // unclaimed. Live GAME.mSlots stay unclaimed.
                _flightModeFlag = RetailEnableFlightMode.Enable(_flightModeFlag);
                SetFlightMode(true);
                return NativeResult.Void;
            case 101: // DisableFlightMode
                RequireArguments(command, arguments, 0);
                SetFlightMode(false);
                return NativeResult.Void;
            case 106: // LevelLostString
                RequireArguments(command, arguments, 1);
                DeclareLost(
                    Level100MissionFailureReason.TutorialBroken,
                    arguments[0].AsInteger());
                return NativeResult.Void;
            case 118: // AddHelpMessage
                RequireArguments(command, arguments, 1);
                _events.Add(new Level100HelpRequested(_tick, arguments[0].AsInteger()));
                return NativeResult.Void;
            case 124: // GetSlot
                RequireArguments(command, arguments, 1);
                return new NativeResult(
                    Level100ScriptValue.Boolean(GetTutorialSlot(arguments[0].AsInteger())),
                    WaitRequest.None);
            case 133: // SetSlotSave — IScript__SetSlotSave 0x00533900
                RequireArguments(command, arguments, 2);
                SetTutorialSlot(arguments[0].AsInteger(), arguments[1].AsBoolean());
                return NativeResult.Void;
            default:
                throw new InvalidOperationException(
                    $"Native call {command} is outside the released Level 100 runtime slice.");
        }
    }

    /// <summary>
    /// Schedules one <c>PlayCharMessage</c>/<c>PlayCharMessageWait</c> against
    /// the released message box and returns how many ticks a waiting script
    /// must sleep, measured from the current tick.
    /// </summary>
    /// <remarks>
    /// The message box, not the script, decides when a message becomes active:
    /// nothing may play before
    /// <see cref="Level100MissionTiming.MessageBoxAllowedTick"/>, and a message
    /// requested while another is up waits out the previous one plus
    /// <see cref="Level100MissionTiming.MessageAdvanceDelayTicks"/>. The
    /// emitted <see cref="Level100MessageRequested"/> is therefore held back to
    /// the tick the message actually becomes active, so the HUD and the audio
    /// adapter both see the released delivery instant rather than the instant
    /// the script line executed.
    /// </remarks>
    /// <summary>An actor script's message through this mission's message box.</summary>
    internal int RequestScriptMessage(int speakerId, int messageId, bool waits) =>
        RequestMessage(speakerId, messageId, waits);

    private int RequestMessage(IReadOnlyList<Level100ScriptValue> arguments, bool waits)
    {
        _ = arguments[2].AsFloat();
        return RequestMessage(arguments[0].AsInteger(), arguments[1].AsInteger(), waits);
    }

    private int RequestMessage(int speakerId, int messageId, bool waits)
    {
        if (waits)
        {
            StoreWaitStop();
        }

        int ticks = Level100MissionTiming.MessagePlaybackTicks(messageId);

        int earliest = _messageBoxAllowedTick;
        if (_messageClearTick != int.MinValue)
        {
            earliest = Math.Max(
                earliest,
                checked(_messageClearTick + Level100MissionTiming.MessageAdvanceDelayTicks));
        }

        int startTick = Math.Max(_tick, earliest);
        _messageClearTick = checked(startTick + ticks);

        var message = new Level100MessageRequested(
            startTick,
            speakerId,
            messageId,
            waits,
            ticks);
        if (startTick <= _tick)
        {
            _events.Add(message);
        }
        else
        {
            _pendingMessages.Enqueue(new PendingMessage(startTick, message));
        }

        // The script sleeps until the message CLEARS, not for its own duration:
        // retail's PlayCharMessageWait resumes when the message box reports the
        // message finished, which is startTick + ticks.
        return checked(_messageClearTick - _tick);
    }

    private void ReleaseDueMessages()
    {
        while (_pendingMessages.Count > 0 && _pendingMessages.Peek().StartTick <= _tick)
        {
            _events.Add(_pendingMessages.Dequeue().Message);
        }
    }

    private void PostEvent(string eventName)
    {
        _events.Add(new Level100MissionEventPosted(_tick, eventName));
        // Retail PostEvent publishes to the event bus. If this same compiled
        // object has a matching listener, that listener is queued as well as
        // the external event fact; execution remains non-reentrant.
        if (_program.NamedEventInstructionPointers.ContainsKey(eventName))
        {
            _eventQueue.Enqueue(new QueuedEvent(NextSequence(), eventName));
        }
    }

    private void SetActorActivation(Level100ActorId actorId, bool active)
    {
        if (actorId == _playerActorId)
        {
            _playerActive = active;
            _events.Add(new Level100PlayerActivationChanged(_tick, active));
        }

        _events.Add(new Level100ActorCommandRequested(
            _tick,
            actorId,
            active ? Level100ActorCommand.Activate : Level100ActorCommand.Deactivate));
    }

    private void SetNavigationObjective(Level100ActorId actorId)
    {
        string thingName = _actors.GetActor(actorId).Name;
        _navigationObjective = thingName;
        StoreObjectiveFlag(actorId, mark: true);
        _events.Add(new Level100NavigationObjectiveChanged(_tick, thingName));
        _events.Add(new Level100ActorCommandRequested(
            _tick,
            actorId,
            Level100ActorCommand.SetObjective));
    }

    private void UnsetNavigationObjective(Level100ActorId actorId)
    {
        string thingName = _actors.GetActor(actorId).Name;
        if (string.Equals(_navigationObjective, thingName, StringComparison.Ordinal))
        {
            _navigationObjective = null;
            _events.Add(new Level100NavigationObjectiveChanged(_tick, null));
        }

        StoreObjectiveFlag(actorId, mark: false);
        _events.Add(new Level100ActorCommandRequested(
            _tick,
            actorId,
            Level100ActorCommand.UnsetObjective));
    }

    /// <summary>
    /// Isolated <see cref="Level100ActorSnapshot.IsObjective"/>
    /// and the navigation name stay the rebuild facts. These
    /// stores are the official <c>or 0x20</c> / <c>and ~0x20</c>
    /// at <c>CThing+0x2c</c>. Noticeboard stays unclaimed.
    /// </summary>
    private void StoreObjectiveFlag(Level100ActorId actorId, bool mark)
    {
        int current = ThingFlagWord(actorId);
        _thingFlagWords[actorId.Value] = mark
            ? RetailSetObjective.Mark(current)
            : RetailSetObjective.Unmark(current);
    }

    /// <summary>
    /// Isolated <see cref="Level100MissionTiming.PauseTicks"/>
    /// and message duration stay the rebuild sleeps. This
    /// store is official <c>mov [0x0089c800], 1</c>. CVM
    /// snapshot / 0.05f / FollowWaypointWait stay unclaimed.
    /// </summary>
    private void StoreWaitStop()
    {
        _waitStopFlag = RetailIScriptWaitStop.Stop(_waitStopFlag);
    }

    /// <summary>
    /// Isolated <see cref="Level100HudEmphasisChanged.Emphasized"/>
    /// names the rebuild bool. These stores are the official
    /// immediates 2 then 1. Array extent stays unclaimed.
    /// </summary>
    private void StoreHudPartWord(int partIndex, bool highlight)
    {
        int current = HudPartWord(partIndex);
        _hudPartWords[partIndex] = highlight
            ? RetailHighlightHudPart.Highlight(current)
            : RetailHighlightHudPart.Unhighlight(current);
        _events.Add(new Level100HudEmphasisChanged(
            _tick,
            partIndex,
            highlight));
    }

    private void SetPrimaryObjective(
        IReadOnlyList<Level100ScriptValue> arguments,
        Level100PrimaryObjectiveStatus status)
    {
        int objective = arguments[0].AsInteger();
        int textId = arguments[1].AsInteger();
        if (objective < 1 || objective > _primaryObjectives.Length)
        {
            throw new InvalidOperationException($"Released objective {objective} is invalid.");
        }

        _primaryObjectives[objective - 1] = new ObjectiveState(objective, textId, status);
        _events.Add(new Level100PrimaryObjectiveChanged(
            _tick,
            objective,
            textId,
            status));
    }

    private void SetWeapon(string weaponName, bool enabled)
    {
        Level100MissionWeapon weapon = weaponName switch
        {
            "Pulse Cannon Pod" => Level100MissionWeapon.PulseCannonPod,
            "Mech Twin Vulcan Cannon" => Level100MissionWeapon.MechTwinVulcanCannon,
            "Mech Vulcan Cannon" => Level100MissionWeapon.MechVulcanCannon,
            "Missile Pod" => Level100MissionWeapon.MissilePod,
            _ => throw new InvalidOperationException(
                $"Released Level 100 requested unknown weapon '{weaponName}'."),
        };
        Level100MissionWeaponAvailability availability = enabled
            ? Level100MissionWeaponAvailability.Enabled
            : Level100MissionWeaponAvailability.Disabled;
        switch (weapon)
        {
            case Level100MissionWeapon.PulseCannonPod:
                _pulseCannonAvailability = availability;
                break;
            case Level100MissionWeapon.MechTwinVulcanCannon:
                _twinVulcanAvailability = availability;
                break;
            case Level100MissionWeapon.MechVulcanCannon:
                _mechVulcanAvailability = availability;
                break;
            case Level100MissionWeapon.MissilePod:
                _missilePodAvailability = availability;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(weapon));
        }

        if (enabled)
        {
            // Isolated Enabled names the rebuild bool / enum.
            // Isolated Enable names literal-1; one live store of 1
            // is not unique versus increment from 0. Disable's
            // store-0 / ChangeWeapon stay unclaimed. ChargeWeapon
            // stays unclaimed. Live GAME.mSlots stay unclaimed.
            int current = WeaponActiveWord(weapon);
            _weaponActiveWords[weapon] = RetailEnableWeapon.Enable(current);
        }

        _events.Add(new Level100WeaponAvailabilityChanged(_tick, weapon, enabled));
    }

    private void SetFlightMode(bool enabled)
    {
        _flightModeEnabled = enabled;
        _events.Add(new Level100FlightModeAvailabilityChanged(_tick, enabled));
    }

    private bool GetTutorialSlot(int slot) => slot switch
    {
        63 => _tutorialIntroduction,
        64 => _tutorialPulseCannon,
        65 => _tutorialVulcanCannon,
        66 => _tutorialStatusBars,
        _ => throw new InvalidOperationException($"Released Level 100 requested unknown slot {slot}."),
    };

    private void SetTutorialSlot(int slot, bool value)
    {
        switch (slot)
        {
            case 63:
                _tutorialIntroduction = value;
                break;
            case 64:
                _tutorialPulseCannon = value;
                break;
            case 65:
                _tutorialVulcanCannon = value;
                break;
            case 66:
                _tutorialStatusBars = value;
                break;
            default:
                throw new InvalidOperationException(
                    $"Released Level 100 requested unknown saved slot {slot}.");
        }

        // IScript::SetSlotSave at 0x00533900 also calls CCareer::SetSlot
        // immediately: the career's owner applies it from this event. Live
        // GAME.mSlots stay unclaimed.
        _events.Add(new Level100TutorialSlotSaved(_tick, slot, value));
    }

    private void DeclareWon()
    {
        if (_outcome != Level100MissionOutcome.Running)
        {
            return;
        }

        _outcome = Level100MissionOutcome.Won;
        _terminalState = Level100MissionTerminalState.SuccessCountdown;
        _terminalTicksRemaining = Level100MissionTiming.SuccessCountdownTicks;
        _events.Add(new Level100MissionOutcomeDeclared(
            _tick,
            _outcome,
            Level100MissionFailureReason.None,
            0));
        _events.Add(new Level100TerminalStateChanged(_tick, _terminalState));
    }

    private void DeclareLost(Level100MissionFailureReason reason, int failureTextId = 0)
    {
        if (_outcome != Level100MissionOutcome.Running ||
            reason == Level100MissionFailureReason.None)
        {
            return;
        }

        _outcome = Level100MissionOutcome.Lost;
        _failureReason = reason;
        _failureTextId = failureTextId;
        _terminalState = Level100MissionTerminalState.FailureCountdown;
        _terminalTicksRemaining = Level100MissionTiming.FailureTerminalTicks(reason);
        _events.Add(new Level100MissionOutcomeDeclared(
            _tick,
            _outcome,
            reason,
            failureTextId));
        _events.Add(new Level100TerminalStateChanged(_tick, _terminalState));
    }

    private bool DeclareExternalLoss(Level100MissionFailureReason reason)
    {
        if (_outcome != Level100MissionOutcome.Running)
        {
            return false;
        }

        DeclareLost(reason);
        _activeExecution = null;
        _eventQueue.Clear();
        _continuations.Clear();
        return true;
    }

    private void AdvanceTerminalCountdown()
    {
        if (_terminalTicksRemaining == 0)
        {
            return;
        }

        _terminalTicksRemaining--;
        if (_outcome == Level100MissionOutcome.Won && _terminalTicksRemaining == 0)
        {
            _terminalState = Level100MissionTerminalState.FrontEndHandoffReady;
            _events.Add(new Level100TerminalStateChanged(_tick, _terminalState));
        }
        else if (_outcome == Level100MissionOutcome.Lost &&
                 _terminalTicksRemaining ==
                 Level100MissionTiming.FailureTerminalTicks(_failureReason) -
                 Level100MissionTiming.FailureMenuDelayTicks)
        {
            _terminalState = Level100MissionTerminalState.FailureMenuReady;
            _events.Add(new Level100TerminalStateChanged(_tick, _terminalState));
        }
        else if (_outcome == Level100MissionOutcome.Lost && _terminalTicksRemaining == 0)
        {
            _terminalState = Level100MissionTerminalState.FailureCountdownElapsed;
            _events.Add(new Level100TerminalStateChanged(_tick, _terminalState));
        }
    }

    private long NextSequence()
    {
        long result = _nextSequence;
        _nextSequence = checked(_nextSequence + 1);
        return result;
    }

    private Level100ScriptValue Local(int ordinal)
    {
        if ((uint)ordinal >= _locals.Length)
        {
            throw new InvalidOperationException($"Released symbol ordinal {ordinal} is invalid.");
        }

        return _locals[ordinal];
    }

    private int LocalInteger(int ordinal)
    {
        Level100ScriptValue value = Local(ordinal);
        return value.Type == Level100ScriptValueType.Unset ? 0 : value.AsInteger();
    }

    private bool LocalBoolean(int ordinal)
    {
        Level100ScriptValue value = Local(ordinal);
        return value.Type != Level100ScriptValueType.Unset && value.AsBoolean();
    }

    private void SetLocal(int ordinal, Level100ScriptValue value)
    {
        _ = Local(ordinal);
        _locals[ordinal] = value;
    }

    private void SetLocalAssigned(int ordinal, Level100ScriptValue value)
    {
        Level100ScriptValue existing = Local(ordinal);
        if (existing.Type == Level100ScriptValueType.Unset || existing.Type == value.Type)
        {
            _locals[ordinal] = value;
            return;
        }

        if (existing.Type == Level100ScriptValueType.Float &&
            value.Type == Level100ScriptValueType.Integer)
        {
            _locals[ordinal] = Level100ScriptValue.Float(value.Scalar);
            return;
        }

        throw new InvalidOperationException(
            $"Released assignment cannot assign {value.Type} to {existing.Type} symbol {ordinal}.");
    }

    private static Level100ScriptValue Pop(Execution execution)
    {
        if (execution.Stack.Count == 0)
        {
            throw new InvalidOperationException("Released LevelScript stack underflow.");
        }

        int last = execution.Stack.Count - 1;
        Level100ScriptValue value = execution.Stack[last];
        execution.Stack.RemoveAt(last);
        return value;
    }

    private static Level100ScriptValue Peek(Execution execution)
    {
        if (execution.Stack.Count == 0)
        {
            throw new InvalidOperationException("Released LevelScript stack underflow.");
        }

        return execution.Stack[^1];
    }

    private static void PushBinaryNumeric(
        Execution execution,
        Func<float, float, float> floatOperation,
        Func<int, int, int> integerOperation)
    {
        Level100ScriptValue right = Pop(execution);
        Level100ScriptValue left = Pop(execution);
        if (left.Type == Level100ScriptValueType.Float ||
            right.Type == Level100ScriptValueType.Float)
        {
            execution.Stack.Add(Level100ScriptValue.Float(
                floatOperation(left.AsFloat(), right.AsFloat())));
        }
        else
        {
            execution.Stack.Add(Level100ScriptValue.Integer(
                integerOperation(left.AsInteger(), right.AsInteger())));
        }
    }

    private static void PushNumericComparison(
        Execution execution,
        Func<float, float, bool> comparison)
    {
        Level100ScriptValue right = Pop(execution);
        Level100ScriptValue left = Pop(execution);
        execution.Stack.Add(Level100ScriptValue.Boolean(
            comparison(left.AsFloat(), right.AsFloat())));
    }

    private static bool ValuesEqual(Level100ScriptValue left, Level100ScriptValue right)
    {
        if (left.Type == Level100ScriptValueType.Float ||
            right.Type == Level100ScriptValueType.Float)
        {
            return left.AsFloat() == right.AsFloat();
        }

        if (left.Type is Level100ScriptValueType.Integer or Level100ScriptValueType.Boolean &&
            right.Type is Level100ScriptValueType.Integer or Level100ScriptValueType.Boolean)
        {
            return left.Scalar == right.Scalar;
        }

        return left.Type == right.Type &&
               left.Scalar == right.Scalar &&
               left.ComponentY == right.ComponentY &&
               left.ComponentZ == right.ComponentZ &&
               string.Equals(left.Text, right.Text, StringComparison.Ordinal);
    }

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

    private static Level100ActorId RequireContext(Execution execution) =>
        execution.CallContext?.AsActorId() ?? throw new InvalidOperationException(
            "Released LevelScript native method has no resolved actor context.");

    private static Level100ScriptExecutionSnapshot SnapshotExecution(Execution? execution) =>
        execution is null
            ? new Level100ScriptExecutionSnapshot(
                null,
                -1,
                0,
                0,
                false,
                null,
                Array.Empty<Level100ScriptValueSnapshot>(),
                Array.Empty<int>())
            : new Level100ScriptExecutionSnapshot(
                execution.EventName,
                execution.InstructionPointer,
                execution.Flags,
                execution.SavedStackSize,
                execution.Abort,
                execution.CallContext?.Snapshot,
                execution.Stack.Select(item => item.Snapshot).ToArray(),
                execution.CallFrames.ToArray());

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
            var result = new Execution(EventName, InstructionPointer)
            {
                Flags = Flags,
                SavedStackSize = SavedStackSize,
                Abort = Abort,
                CallContext = CallContext,
                Stopped = Stopped,
            };
            result.Stack.AddRange(Stack);
            result.CallFrames.AddRange(CallFrames);
            return result;
        }
    }

    private sealed record Continuation(
        long Sequence,
        int DueTick,
        Level100ScriptWaitKind WaitKind,
        int WaitArgument,
        Execution Execution);

    private readonly record struct QueuedEvent(long Sequence, string EventName);

    private readonly record struct PendingMessage(
        int StartTick,
        Level100MessageRequested Message);

    private readonly record struct ObjectiveState(
        int Objective,
        int TextId,
        Level100PrimaryObjectiveStatus Status);

    private readonly record struct WaitRequest(
        Level100ScriptWaitKind Kind,
        int Ticks,
        int Argument)
    {
        internal static WaitRequest None => new(Level100ScriptWaitKind.None, 0, 0);
    }

    private readonly record struct NativeResult(Level100ScriptValue Value, WaitRequest Wait)
    {
        internal static NativeResult Void => new(Level100ScriptValue.Integer(0), WaitRequest.None);

        internal static NativeResult Waiting(
            Level100ScriptWaitKind kind,
            int ticks,
            int argument) => new(
                Level100ScriptValue.Integer(0),
                new WaitRequest(kind, ticks, argument));
    }
}
