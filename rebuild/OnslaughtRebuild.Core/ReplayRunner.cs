// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnslaughtRebuild.Core;

public sealed record ReplayResult(
    WorldSnapshot FinalState,
    string FinalStateHash,
    string TraceHash);

public sealed record ReplayDivergence(
    [property: JsonPropertyName("tick")] int Tick,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("beforeValue")] string BeforeValue,
    [property: JsonPropertyName("afterValue")] string AfterValue);

public sealed record ReplayDiff(
    [property: JsonPropertyName("traceHashMismatch")] bool TraceHashMismatch,
    [property: JsonPropertyName("behavioralEventMismatch")] bool BehavioralEventMismatch,
    [property: JsonPropertyName("finalStateMismatch")] bool FinalStateMismatch,
    [property: JsonPropertyName("firstDivergence")] ReplayDivergence? FirstDivergence)
{
    public const string CurrentSchemaVersion = "onslaught-rebuild-replay-diff.v1";

    [JsonPropertyName("schemaVersion"), JsonPropertyOrder(-1)]
    public string SchemaVersion => CurrentSchemaVersion;
}

public sealed record ReplayComparison(
    ReplayResult Before,
    ReplayResult After,
    ReplayDiff Diff);

public static class ReplayRunner
{
    private readonly record struct ReplayStep(
        int Tick,
        SimInput Input,
        WorldSnapshot State,
        byte[] StateBytes);

    private sealed class ReplayExecution : IDisposable
    {
        private readonly CommandTape _tape;
        private readonly CommandTapeReader _reader;
        private readonly Simulation _simulation;
        private readonly ReplayTraceHasher _trace = new();
        private WorldSnapshot _state;
        private int _nextTick;
        private bool _completed;

        public ReplayExecution(
            CommandTape tape,
            Level100ActorDefinitionSet level100ActorDefinitions)
        {
            _tape = tape;
            _reader = new CommandTapeReader(tape);
            _simulation = new Simulation(tape.Seed, level100ActorDefinitions);
            _state = _simulation.Snapshot;
        }

        public bool HasNext => _nextTick < _tape.DurationTicks;

        public ReplayStep Step()
        {
            if (!HasNext || _completed)
            {
                throw new InvalidOperationException("Replay execution has no next tick.");
            }

            int tick = _nextTick++;
            SimInput input = _reader.ReadNext(tick);
            _state = _simulation.Step(input);
            byte[] stateBytes = StateHasher.GetCanonicalBytes(_state);
            _trace.Append(tick, input, stateBytes);
            return new ReplayStep(tick, input, _state, stateBytes);
        }

        public ReplayResult Complete()
        {
            if (HasNext || _completed)
            {
                throw new InvalidOperationException(
                    "Replay execution must consume its tape exactly once before completion.");
            }

            _completed = true;
            return new ReplayResult(
                _state,
                StateHasher.ComputeHex(_state),
                _trace.GetCurrentHash());
        }

        public void Dispose() => _trace.Dispose();
    }

    public static ReplayResult Run(
        CommandTape tape,
        Level100ActorDefinitionSet level100ActorDefinitions)
    {
        ArgumentNullException.ThrowIfNull(tape);
        ArgumentNullException.ThrowIfNull(level100ActorDefinitions);
        tape.Validate();

        using var execution = new ReplayExecution(tape, level100ActorDefinitions);
        while (execution.HasNext)
        {
            execution.Step();
        }

        return execution.Complete();
    }

    public static ReplayComparison Compare(
        CommandTape beforeTape,
        CommandTape afterTape,
        Level100ActorDefinitionSet level100ActorDefinitions)
    {
        ArgumentNullException.ThrowIfNull(beforeTape);
        ArgumentNullException.ThrowIfNull(afterTape);
        ArgumentNullException.ThrowIfNull(level100ActorDefinitions);
        beforeTape.Validate();
        afterTape.Validate();

        using var beforeExecution = new ReplayExecution(
            beforeTape,
            level100ActorDefinitions);
        using var afterExecution = new ReplayExecution(
            afterTape,
            level100ActorDefinitions);

        ReplayDivergence? firstDivergence = null;
        if (beforeTape.Seed != afterTape.Seed)
        {
            firstDivergence = Difference(
                0,
                "replay.seed",
                beforeTape.Seed,
                afterTape.Seed);
        }

        while (beforeExecution.HasNext || afterExecution.HasNext)
        {
            if (beforeExecution.HasNext && afterExecution.HasNext)
            {
                ReplayStep beforeStep = beforeExecution.Step();
                ReplayStep afterStep = afterExecution.Step();
                firstDivergence ??= FindFirstDivergence(beforeStep, afterStep);
                continue;
            }

            int tick = Math.Min(
                beforeTape.DurationTicks,
                afterTape.DurationTicks);
            firstDivergence ??= Difference(
                tick,
                "replay.durationTicks",
                beforeTape.DurationTicks,
                afterTape.DurationTicks);
            if (beforeExecution.HasNext)
            {
                beforeExecution.Step();
            }

            if (afterExecution.HasNext)
            {
                afterExecution.Step();
            }
        }

        ReplayResult before = beforeExecution.Complete();
        ReplayResult after = afterExecution.Complete();
        bool traceHashMismatch = !string.Equals(
            before.TraceHash,
            after.TraceHash,
            StringComparison.Ordinal);
        bool finalStateMismatch = !string.Equals(
            before.FinalStateHash,
            after.FinalStateHash,
            StringComparison.Ordinal);

        return new ReplayComparison(
            before,
            after,
            new ReplayDiff(
                traceHashMismatch,
                firstDivergence is not null,
                finalStateMismatch,
                firstDivergence));
    }

    private static ReplayDivergence? FindFirstDivergence(
        in ReplayStep before,
        in ReplayStep after)
    {
        if (before.Tick != after.Tick)
        {
            return Difference(
                Math.Min(before.Tick, after.Tick),
                "replay.tick",
                before.Tick,
                after.Tick);
        }

        int tick = before.Tick;
        if (before.Input.MoveX != after.Input.MoveX)
        {
            return Difference(tick, "input.moveX", before.Input.MoveX, after.Input.MoveX);
        }

        if (before.Input.MoveZ != after.Input.MoveZ)
        {
            return Difference(tick, "input.moveZ", before.Input.MoveZ, after.Input.MoveZ);
        }

        if (before.Input.Actions != after.Input.Actions)
        {
            return Difference(
                tick,
                "input.actions",
                (ushort)before.Input.Actions,
                (ushort)after.Input.Actions);
        }

        if (before.Input.LookX != after.Input.LookX)
        {
            return Difference(tick, "input.lookX", before.Input.LookX, after.Input.LookX);
        }

        if (before.Input.LookY != after.Input.LookY)
        {
            return Difference(tick, "input.lookY", before.Input.LookY, after.Input.LookY);
        }

        if (before.Input.LookXAnalogPermille != after.Input.LookXAnalogPermille)
        {
            return Difference(
                tick,
                "input.lookXAnalogPermille",
                before.Input.LookXAnalogPermille,
                after.Input.LookXAnalogPermille);
        }

        if (before.Input.LookYAnalogPermille != after.Input.LookYAnalogPermille)
        {
            return Difference(
                tick,
                "input.lookYAnalogPermille",
                before.Input.LookYAnalogPermille,
                after.Input.LookYAnalogPermille);
        }

        // StateHasher's canonical bytes already contain every event stream and
        // observable inspected below. The equal fast path keeps a same-tape
        // comparison linear even when an event log is append-only; detailed
        // category scans are needed only on the first unequal state.
        if (before.StateBytes.AsSpan().SequenceEqual(after.StateBytes))
        {
            return null;
        }

        ReplayDivergence? eventDivergence = CompareEvents(
            tick,
            "event.aquilaFlight",
            before.State.AquilaFlightEventLog,
            after.State.AquilaFlightEventLog);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100Mission",
            before.State.Level100MissionEvents,
            after.State.Level100MissionEvents);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100PlayerDamage",
            before.State.Level100PlayerDamageEvents,
            after.State.Level100PlayerDamageEvents);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100Destruction",
            before.State.Level100DestructionEvents,
            after.State.Level100DestructionEvents);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100WeaponFire",
            before.State.Level100WeaponFireEvents,
            after.State.Level100WeaponFireEvents);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100ActorScriptPosted",
            before.State.Level100ActorScripts.PendingPostedEvents,
            after.State.Level100ActorScripts.PendingPostedEvents);
        eventDivergence ??= CompareEvents(
            tick,
            "event.level100ActorScriptCommand",
            before.State.Level100ActorScriptCommands,
            after.State.Level100ActorScriptCommands);
        if (eventDivergence is not null)
        {
            return eventDivergence;
        }

        if (before.State.Mode != after.State.Mode)
        {
            return Difference(tick, "observable.vehicleMode", before.State.Mode, after.State.Mode);
        }

        if (before.State.Transition != after.State.Transition)
        {
            return Difference(
                tick,
                "observable.vehicleTransition",
                before.State.Transition,
                after.State.Transition);
        }

        if (before.State.PlayerPosition != after.State.PlayerPosition)
        {
            return Difference(
                tick,
                "observable.playerPosition",
                before.State.PlayerPosition,
                after.State.PlayerPosition);
        }

        if (before.State.FacingYawMicroRad != after.State.FacingYawMicroRad ||
            before.State.FacingPitchMicroRad != after.State.FacingPitchMicroRad ||
            before.State.BodyRollMicroRad != after.State.BodyRollMicroRad)
        {
            return new ReplayDivergence(
                tick,
                "observable.playerFacing",
                FormatFacing(before.State),
                FormatFacing(after.State));
        }

        if (before.State.Energy != after.State.Energy ||
            before.State.Shield != after.State.Shield ||
            before.State.Hull != after.State.Hull)
        {
            return new ReplayDivergence(
                tick,
                "observable.playerVitals",
                FormatVitals(before.State),
                FormatVitals(after.State));
        }

        if (before.State.Level100Mission.Outcome != after.State.Level100Mission.Outcome)
        {
            return Difference(
                tick,
                "observable.missionOutcome",
                before.State.Level100Mission.Outcome,
                after.State.Level100Mission.Outcome);
        }

        if (before.State.TargetsDestroyed != after.State.TargetsDestroyed)
        {
            return Difference(
                tick,
                "observable.targetsDestroyed",
                before.State.TargetsDestroyed,
                after.State.TargetsDestroyed);
        }

        if (before.State.Projectiles.Count != after.State.Projectiles.Count)
        {
            return Difference(
                tick,
                "observable.activeProjectiles",
                before.State.Projectiles.Count,
                after.State.Projectiles.Count);
        }

        return new ReplayDivergence(
            tick,
            "observable.stateHash",
            HashHex(before.StateBytes),
            HashHex(after.StateBytes));
    }

    private static ReplayDivergence? CompareEvents<T>(
        int tick,
        string category,
        IReadOnlyList<T> before,
        IReadOnlyList<T> after)
    {
        int shared = Math.Min(before.Count, after.Count);
        for (int index = 0; index < shared; index++)
        {
            if (!EqualityComparer<T>.Default.Equals(before[index], after[index]))
            {
                return new ReplayDivergence(
                    tick,
                    category,
                    FormatEvent(index, before[index]),
                    FormatEvent(index, after[index]));
            }
        }

        if (before.Count == after.Count)
        {
            return null;
        }

        return new ReplayDivergence(
            tick,
            category,
            before.Count > shared
                ? FormatEvent(shared, before[shared])
                : "<absent>",
            after.Count > shared
                ? FormatEvent(shared, after[shared])
                : "<absent>");
    }

    private static ReplayDivergence Difference<T>(
        int tick,
        string category,
        T before,
        T after) =>
        new(tick, category, FormatValue(before), FormatValue(after));

    private static string FormatValue<T>(T value)
    {
        object? boxed = value;
        if (boxed is null)
        {
            return "null";
        }

        if (boxed.GetType().IsEnum)
        {
            return boxed.ToString() ?? string.Empty;
        }

        if (boxed is IFormattable formattable)
        {
            return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return JsonSerializer.Serialize(boxed, boxed.GetType());
    }

    private static string FormatEvent<T>(int index, T value)
    {
        object? boxed = value;
        string serialized = boxed is null
            ? "null"
            : JsonSerializer.Serialize(boxed, boxed.GetType());
        return string.Concat(
            "index=",
            index.ToString(CultureInfo.InvariantCulture),
            ";value=",
            serialized);
    }

    private static string FormatFacing(WorldSnapshot state) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"yaw={state.FacingYawMicroRad};pitch={state.FacingPitchMicroRad};roll={state.BodyRollMicroRad}");

    private static string FormatVitals(WorldSnapshot state) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"energy={state.Energy};shield={state.Shield};hull={state.Hull}");

    private static string HashHex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

/// <summary>
/// One canonical trace format for replay and live recording. Callers supply
/// each consumed input and the canonical bytes of its actual resulting state.
/// </summary>
internal sealed class ReplayTraceHasher : IDisposable
{
    private static readonly byte[] s_traceHeader = CreateTraceHeader();
    private readonly IncrementalHash _trace = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public ReplayTraceHasher() => _trace.AppendData(s_traceHeader);

    public void Append(int inputSlot, SimInput input, byte[] stateBytes) =>
        _trace.AppendData(CreateTraceEntry(inputSlot, input, stateBytes));

    public string GetCurrentHash() =>
        Convert.ToHexString(_trace.GetCurrentHash()).ToLowerInvariant();

    public void Dispose() => _trace.Dispose();

    private static byte[] CreateTraceHeader()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("ONSLAUGHT-REBUILD-TRACE"));
            // 4: SimActions widened from byte to ushort so the complete
            // released action set fits (see SimActions). Each trace entry's
            // action field is two bytes from here on.
            writer.Write(4);
        }

        return stream.ToArray();
    }

    private static byte[] CreateTraceEntry(int inputSlot, SimInput input, byte[] stateBytes)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(inputSlot);
            writer.Write(input.MoveX);
            writer.Write(input.MoveZ);
            writer.Write(input.LookX);
            writer.Write(input.LookY);
            writer.Write(input.LookXAnalogPermille);
            writer.Write(input.LookYAnalogPermille);
            writer.Write((ushort)input.Actions);
            writer.Write(stateBytes.Length);
            writer.Write(stateBytes);
        }

        return stream.ToArray();
    }
}
