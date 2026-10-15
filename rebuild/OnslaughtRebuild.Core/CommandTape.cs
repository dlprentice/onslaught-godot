// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnslaughtRebuild.Core;

public sealed record CommandSpan(
    int StartTick,
    int DurationTicks,
    sbyte MoveX,
    sbyte MoveZ,
    bool ToggleMode = false,
    bool Fire = false,
    bool Reset = false,
    sbyte LookX = 0,
    sbyte LookY = 0,
    short LookXAnalogPermille = 0,
    short LookYAnalogPermille = 0,
    bool LandingJets = false,
    // BUTTON_SKIP_PANNING 0x3a, KEY_ONCE in every shipped row, so an edge.
    bool SkipPanning = false,
    // BUTTON_MECH_CHANGE_WEAPON 0x14, BUTTON_ONCE in the released PC mapping.
    bool ChangeWeapon = false,
    // BUTTON_MECH_CHARGE_GUN_POD 0x13 is a held level: Core advances the
    // Pulse Cannon Pod charge once per tick while it stays set (see
    // Simulation.TryChargeWeapon). It must never be folded into the edge law
    // below.
    bool ChargeWeapon = false,
    // BUTTON_MECH_CHANGE_ZOOM_IN 0x10 / _OUT 0x11 are one-shot actions in the
    // released mapping (mouse wheel), so one-tick edges like ChangeWeapon.
    bool ZoomIn = false,
    bool ZoomOut = false)
{
    [JsonIgnore]
    public int EndTickExclusive => checked(StartTick + DurationTicks);

    public SimInput ToInput()
    {
        SimActions actions = SimActions.None;
        if (ToggleMode)
        {
            actions |= SimActions.ToggleMode;
        }

        if (Fire)
        {
            actions |= SimActions.Fire;
        }

        if (Reset)
        {
            actions |= SimActions.Reset;
        }

        if (LandingJets)
        {
            actions |= SimActions.LandingJets;
        }

        if (SkipPanning)
        {
            actions |= SimActions.SkipPanning;
        }

        if (ChangeWeapon)
        {
            actions |= SimActions.ChangeWeapon;
        }

        if (ChargeWeapon)
        {
            actions |= SimActions.ChargeWeapon;
        }

        if (ZoomIn)
        {
            actions |= SimActions.ZoomIn;
        }

        if (ZoomOut)
        {
            actions |= SimActions.ZoomOut;
        }

        return new SimInput(
            MoveX,
            MoveZ,
            actions,
            LookX,
            LookY,
            LookXAnalogPermille,
            LookYAnalogPermille);
    }
}

public sealed record CommandTape
{
    /// <summary>
    /// The tape wire format. <b>Bump this whenever <see cref="CommandSpan"/>
    /// gains, loses or re-means a field</b>, and rely on
    /// <see cref="Validate"/> rejecting a mismatch rather than on a reader
    /// noticing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>v4 -&gt; v5</c>, 2026-08-23: <c>CommandSpan</c> gained
    /// <c>ChargeWeapon</c> (a held level, like <c>LandingJets</c>) and
    /// <c>ZoomIn</c>/<c>ZoomOut</c> (one-tick edges) when Core grew the
    /// recorder seam, so a captured human tape can carry the complete consumed
    /// action set. Compatibility is the explicit migrate/read policy:
    /// <see cref="CommandTapeCodec.Deserialize"/> transparently upgrades a v4
    /// document — whose field set is a strict subset of v5's — and stamps it
    /// as v5, so the tracked scenario keeps working unchanged. The upgrade is
    /// never a silent reinterpretation: every v4 field means exactly what it
    /// meant in v4, and a document that already claims v5 identity is checked
    /// against v5 exactly. A downgrade (v5 to v4) is impossible by
    /// construction, because v5-only fields would be dropped.
    /// </para>
    /// <para>
    /// <c>v3 -&gt; v4</c>, 2026-08-13: <c>CommandSpan.ChangeWeapon</c> was
    /// added when Core implemented the released weapon-cycle action. It is a
    /// one-tick <c>BUTTON_ONCE</c> edge. That bump was fail-closed: the v3
    /// reader rejected v4 documents and vice versa, which is why this bump
    /// introduces the one-way upgrade path above instead of extending it.
    /// </para>
    /// <para>
    /// <c>v2 -&gt; v3</c>, 2026-08-09: <c>CommandSpan.Fire</c> changed from a
    /// held action to the shipped controller's <c>BUTTON_RELEASE</c> edge. A
    /// fire span must therefore be exactly one tick; otherwise an old held-fire
    /// tape would silently replay as several release edges.
    /// </para>
    /// <para>
    /// <c>v1 -&gt; v2</c>, 2026-07-27: <c>CommandSpan.SkipPanning</c>
    /// (<c>BUTTON_SKIP_PANNING</c> <c>0x3a</c>) was added. It is load-bearing —
    /// it ends the opening pan and, through
    /// <c>Level100Mission.NotifyPlayingStateStarted</c>, re-bases the whole
    /// tutorial message chain. The codec has always rejected unknown JSON
    /// members, so an older reader would reject—not silently drop—the new
    /// property. The version still moves with every field change so writers and
    /// readers identify one exact wire contract and missing newly introduced
    /// fields cannot masquerade as the same schema.
    /// </para>
    /// <para>
    /// The one tracked tape, <c>rebuild/scenarios/first-flight.v1.json</c>,
    /// keeps both its filename and its v4 schema declaration on this bounded
    /// card: that <c>v1</c> is the scenario's own name (unchanged since
    /// <c>3cc382e8</c>), while the v4 document is upgraded by the explicit
    /// migrate/read path above. No fixture or pinned hash is refreshed merely
    /// to prove the recorder infrastructure.
    /// </para>
    /// </remarks>
    public const string CurrentSchemaVersion = "onslaught-rebuild-command-tape.v5";

    /// <summary>
    /// The one immediately previous schema a v5 reader upgrades in place. A
    /// v4 document's field set is a strict subset of v5's — every field kept
    /// its exact meaning — so the upgrade stamps the current identity and
    /// validates under the current (stricter) rules.
    /// </summary>
    public const string PreviousUpgradableSchemaVersion = "onslaught-rebuild-command-tape.v4";

    [JsonConstructor]
    public CommandTape(
        string schemaVersion,
        string name,
        uint seed,
        int durationTicks,
        string? expectedFinalStateHash,
        string? expectedTraceHash,
        IReadOnlyList<CommandSpan>? spans)
    {
        SchemaVersion = schemaVersion;
        Name = name;
        Seed = seed;
        DurationTicks = durationTicks;
        ExpectedFinalStateHash = expectedFinalStateHash;
        ExpectedTraceHash = expectedTraceHash;
        Spans = spans is null ? null! : Array.AsReadOnly(spans.ToArray());
    }

    public string SchemaVersion { get; init; }

    public string Name { get; init; }

    public uint Seed { get; init; }

    public int DurationTicks { get; init; }

    public string? ExpectedFinalStateHash { get; init; }

    public string? ExpectedTraceHash { get; init; }

    public IReadOnlyList<CommandSpan> Spans { get; }

    public void Validate()
    {
        if (!string.Equals(SchemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported command tape schema: {SchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidDataException("Command tape name is required.");
        }

        if (Seed == 0)
        {
            throw new InvalidDataException("Command tape seed must be nonzero.");
        }

        if (DurationTicks is < 1 or > 1_000_000)
        {
            throw new InvalidDataException("Command tape duration must be between 1 and 1,000,000 ticks.");
        }

        ValidateOptionalHash(ExpectedFinalStateHash, "Expected final state hash");
        ValidateOptionalHash(ExpectedTraceHash, "Expected trace hash");

        if (Spans is null)
        {
            throw new InvalidDataException("Command tape spans are required.");
        }

        int previousEnd = 0;
        foreach (CommandSpan span in Spans)
        {
            if (span is null)
            {
                throw new InvalidDataException("Command tape spans cannot contain null entries.");
            }

            if (span.StartTick < previousEnd || span.StartTick < 0)
            {
                throw new InvalidDataException("Command spans must be sorted and non-overlapping.");
            }

            int endTickExclusive;
            try
            {
                endTickExclusive = span.EndTickExclusive;
            }
            catch (OverflowException exception)
            {
                throw new InvalidDataException("Command span end tick exceeds the supported range.", exception);
            }

            if (span.DurationTicks <= 0 || endTickExclusive > DurationTicks)
            {
                throw new InvalidDataException("Command span is outside the tape duration.");
            }

            SimInput input = span.ToInput();
            try
            {
                input.Validate();
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException("Command span contains invalid input values.", exception);
            }

            if ((span.ToggleMode || span.Fire || span.Reset || span.SkipPanning ||
                    span.ChangeWeapon || span.ZoomIn || span.ZoomOut) &&
                span.DurationTicks != 1)
            {
                throw new InvalidDataException(
                    "ToggleMode, Fire, Reset, SkipPanning, ChangeWeapon, ZoomIn and ZoomOut are edge actions and require a one-tick span.");
            }

            previousEnd = endTickExclusive;
        }
    }

    private static void ValidateOptionalHash(string? hash, string label)
    {
        if (hash is not null &&
            (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidDataException($"{label} must be a 64-character SHA-256 hex value.");
        }
    }

    /// <summary>
    /// The tape's content identity: the lowercase SHA-256 of its LF-canonical
    /// serialization. Two tapes with the same identity carry the same canonical
    /// input sequence and, when replayed against the same actor definitions,
    /// produce the same hashes; a single tick of difference moves it.
    /// </summary>
    public static string IdentityOf(CommandTape tape)
    {
        ArgumentNullException.ThrowIfNull(tape);
        string canonical = CommandTapeCodec.Serialize(tape).ReplaceLineEndings("\n");
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}

public static class CommandTapeCodec
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static CommandTape Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using JsonDocument document = JsonDocument.Parse(json);
        ValidateUniqueMembers(document.RootElement);
        CommandTape tape = document.RootElement.Deserialize<CommandTape>(s_options)
            ?? throw new InvalidDataException("Command tape JSON did not contain a document.");
        if (string.Equals(
                tape.SchemaVersion,
                CommandTape.PreviousUpgradableSchemaVersion,
                StringComparison.Ordinal))
        {
            // Explicit migrate/read: a v4 document's field set is a strict
            // subset of v5's, so stamping the current identity and validating
            // under the current rules upgrades it exactly, with no field
            // re-meaning. Reject a document that only CLAIMS v4 while carrying
            // any v5-only member; its schema identity and field set disagree.
            ValidatePreviousFieldSet(document.RootElement);
            tape = tape with { SchemaVersion = CommandTape.CurrentSchemaVersion };
        }

        tape.Validate();
        return tape;
    }

    public static string Serialize(CommandTape tape)
    {
        ArgumentNullException.ThrowIfNull(tape);
        tape.Validate();
        return JsonSerializer.Serialize(tape, s_options).ReplaceLineEndings("\n") + "\n";
    }

    private static void ValidateUniqueMembers(JsonElement element)
    {
        // Deserialization and TryGetProperty can select the last occurrence.
        // Reject ambiguity before either can discard a span, action, schema
        // declaration or expected hash. Property.Name is already unescaped;
        // comparison stays ordinal and case-sensitive like the codec itself.
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Duplicate command tape JSON member '{property.Name}'.");
                ValidateUniqueMembers(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                ValidateUniqueMembers(item);
        }
    }

    private static void ValidatePreviousFieldSet(JsonElement root)
    {
        if (!root.TryGetProperty("spans", out JsonElement spans) ||
            spans.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement span in spans.EnumerateArray())
        {
            if (span.ValueKind == JsonValueKind.Object &&
                (span.TryGetProperty("chargeWeapon", out _) ||
                    span.TryGetProperty("zoomIn", out _) ||
                    span.TryGetProperty("zoomOut", out _)))
            {
                throw new InvalidDataException(
                    "A v4 command tape cannot contain the v5-only chargeWeapon, zoomIn or zoomOut members.");
            }
        }
    }
}

/// <summary>
/// Expands one validated tape in strict tick order without rescanning its span
/// list. Replay and behavioral comparison share this cursor so both consume the
/// exact same input law.
/// </summary>
internal sealed class CommandTapeReader
{
    private readonly CommandTape _tape;
    private int _spanIndex;
    private int _nextTick;

    public CommandTapeReader(CommandTape tape)
    {
        _tape = tape ?? throw new ArgumentNullException(nameof(tape));
    }

    public SimInput ReadNext(int tick)
    {
        if (tick != _nextTick || tick < 0 || tick >= _tape.DurationTicks)
        {
            throw new InvalidOperationException(
                $"Command tape reader expected tick {_nextTick}, not {tick}.");
        }

        while (_spanIndex < _tape.Spans.Count &&
               _tape.Spans[_spanIndex].EndTickExclusive <= tick)
        {
            _spanIndex++;
        }

        SimInput input = SimInput.Idle;
        if (_spanIndex < _tape.Spans.Count)
        {
            CommandSpan span = _tape.Spans[_spanIndex];
            if (tick >= span.StartTick && tick < span.EndTickExclusive)
            {
                input = span.ToInput();
            }
        }

        _nextTick++;
        return input;
    }
}

/// <summary>
/// Records the exact per-tick inputs a client consumed into a
/// <see cref="CommandTape"/>. This is the capture half of the replay-tape
/// seam: a client feeds one <see cref="SimInput"/> per simulation step, in
/// step order with no gaps and no repeats, and the recorder merges the run
/// into the canonical sorted, non-overlapping, coalesced span list the tape
/// schema requires.
///
/// <para>The recorder is pure: no clock, no filesystem, no process. The
/// caller decides which ticks exist (recording starts at gameplay, tick 0,
/// with a fixed seed) and how the finished tape is persisted.</para>
/// </summary>
public sealed class CommandTapeRecorder : IDisposable
{
    private readonly List<(int Tick, SimInput Input)> _observations = [];
    private ReplayTraceHasher? _trace;
    private byte[]? _lastStateBytes;
    private int _nextTick;
    private bool _disposed;

    /// <summary>
    /// The number of ticks observed so far; the next legal observation is
    /// exactly this tick.
    /// </summary>
    public int NextTick => _nextTick;

    /// <summary>
    /// Observes the input Core consumed on <paramref name="tick"/>.
    /// Ticks must be contiguous from zero — a gap would silently record a
    /// different session than the one that ran, and a repeated tick has no
    /// single input for the simulation to have consumed.
    /// </summary>
    public void Observe(int tick, SimInput input)
    {
        ValidateObservation(tick, input);
        if (_trace is not null)
        {
            throw new InvalidOperationException("An observed-state recording requires a resulting snapshot for every input.");
        }

        _observations.Add((tick, input));
        _nextTick = tick + 1;
    }

    /// <summary>
    /// Records the input and hashes the actual resulting snapshot immediately.
    /// Every step from tick zero must include its snapshot; mixing input-only
    /// observations would leave an unverified gap in the live trace.
    /// </summary>
    public void Observe(int tick, SimInput input, WorldSnapshot resultingState)
    {
        ArgumentNullException.ThrowIfNull(resultingState);
        ValidateObservation(tick, input);
        if (_trace is null && _nextTick != 0)
        {
            throw new InvalidOperationException("Observed-state recording must include snapshots from the first input.");
        }
        if (resultingState.Tick != checked(tick + 1))
        {
            throw new ArgumentException(
                "The resulting snapshot must belong to the recorded input's next simulation tick.",
                nameof(resultingState));
        }

        byte[] stateBytes = StateHasher.GetCanonicalBytes(resultingState);
        _trace ??= new ReplayTraceHasher();
        _trace.Append(tick, input, stateBytes);
        _lastStateBytes = stateBytes;
        _observations.Add((tick, input));
        _nextTick = tick + 1;
    }

    private void ValidateObservation(int tick, SimInput input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        input.Validate();
        if (tick != _nextTick)
        {
            if (tick < _nextTick)
            {
                throw new ArgumentException(
                    $"Tick {tick} was already observed; the next tick is {_nextTick}.",
                    nameof(tick));
            }

            throw new ArgumentException(
                $"Observation jumped to tick {tick}; tick {_nextTick} was never observed. A gap would record an input sequence the session never ran.",
                nameof(tick));
        }
    }

    /// <summary>
    /// Builds a tape whose expectations came entirely from the ordered states
    /// observed during recording. No simulation is replayed to derive a hash.
    /// The recorder remains usable until its owner disposes it.
    /// </summary>
    public CommandTape BuildObserved(string name, uint seed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_trace is null || _lastStateBytes is null)
        {
            throw new InvalidOperationException("A verified recording requires at least one observed input and resulting snapshot.");
        }

        return Build(
            name,
            seed,
            _nextTick,
            Convert.ToHexString(SHA256.HashData(_lastStateBytes)).ToLowerInvariant(),
            _trace.GetCurrentHash());
    }

    public void Dispose()
    {
        _trace?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Builds a validated tape over the observed inputs. The duration covers
    /// every recorded tick exactly.
    /// </summary>
    public CommandTape Build(string name, uint seed) =>
        Build(name, seed, _nextTick);

    /// <summary>
    /// Builds a validated tape with explicit expectations. The duration must
    /// cover every recorded tick; pass the hashes captured at the end of the
    /// recorded session to make the tape self-verifying under
    /// <c>--expect</c>.
    /// </summary>
    public CommandTape Build(
        string name,
        uint seed,
        int durationTicks,
        string? expectedFinalStateHash = null,
        string? expectedTraceHash = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (durationTicks < _nextTick)
        {
            throw new ArgumentException(
                $"Duration {durationTicks} does not cover the {_nextTick} recorded ticks.");
        }

        List<CommandSpan> spans = CommandTapeBuilder.Merge(_observations);

        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            name,
            seed,
            durationTicks,
            expectedFinalStateHash,
            expectedTraceHash,
            spans);
        tape.Validate();
        return tape;
    }
}

/// <summary>
/// Merges per-tick observations into canonical <see cref="CommandSpan"/> runs:
/// contiguous identical inputs coalesce into one span, a changed input starts
/// a new span at its own tick, and all-idle runs are dropped so a tape carries
/// only what actually reached the simulation.
/// </summary>
public static class CommandTapeBuilder
{
    public static List<CommandSpan> Merge(
        IReadOnlyList<(int Tick, SimInput Input)> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var spans = new List<CommandSpan>();
        int? runStart = null;
        int runLength = 0;

        for (int index = 0; index < observations.Count; index++)
        {
            (int tick, SimInput input) = observations[index];
            bool idle = IsIdle(input);
            bool edge = HasEdgeAction(input);
            bool splits = runStart.HasValue &&
                (idle ||
                    edge ||
                    HasEdgeAction(observations[index - runLength].Item2) ||
                    tick != runStart.Value + runLength ||
                    !InputEquals(observations[index - runLength].Item2, input));
            if (splits)
            {
                spans.Add(SpanOf(
                    runStart!.Value,
                    runLength,
                    observations[index - runLength].Item2));
                runStart = null;
                runLength = 0;
            }

            if (idle)
            {
                continue;
            }

            if (!runStart.HasValue)
            {
                runStart = tick;
            }

            runLength++;
        }

        if (runStart.HasValue)
        {
            spans.Add(SpanOf(
                runStart.Value,
                runLength,
                observations[observations.Count - runLength].Item2));
        }

        return spans;
    }

    private static bool IsIdle(in SimInput input) =>
        InputEquals(input, SimInput.Idle);

    private static bool HasEdgeAction(in SimInput input) =>
        input.HasAction(
            SimActions.ToggleMode |
            SimActions.Fire |
            SimActions.Reset |
            SimActions.SkipPanning |
            SimActions.ChangeWeapon |
            SimActions.ZoomIn |
            SimActions.ZoomOut);

    private static bool InputEquals(in SimInput left, in SimInput right) =>
        left.MoveX == right.MoveX &&
        left.MoveZ == right.MoveZ &&
        left.Actions == right.Actions &&
        left.LookX == right.LookX &&
        left.LookY == right.LookY &&
        left.LookXAnalogPermille == right.LookXAnalogPermille &&
        left.LookYAnalogPermille == right.LookYAnalogPermille;

    private static CommandSpan SpanOf(int startTick, int durationTicks, in SimInput input)
    {
        bool toggleMode = input.HasAction(SimActions.ToggleMode);
        bool fire = input.HasAction(SimActions.Fire);
        bool reset = input.HasAction(SimActions.Reset);
        bool landingJets = input.HasAction(SimActions.LandingJets);
        bool skipPanning = input.HasAction(SimActions.SkipPanning);
        bool chargeWeapon = input.HasAction(SimActions.ChargeWeapon);
        bool changeWeapon = input.HasAction(SimActions.ChangeWeapon);
        bool zoomIn = input.HasAction(SimActions.ZoomIn);
        bool zoomOut = input.HasAction(SimActions.ZoomOut);

        return new CommandSpan(
            startTick,
            durationTicks,
            input.MoveX,
            input.MoveZ,
            toggleMode,
            fire,
            reset,
            input.LookX,
            input.LookY,
            input.LookXAnalogPermille,
            input.LookYAnalogPermille,
            landingJets,
            skipPanning,
            changeWeapon,
            chargeWeapon,
            zoomIn,
            zoomOut);
    }
}
