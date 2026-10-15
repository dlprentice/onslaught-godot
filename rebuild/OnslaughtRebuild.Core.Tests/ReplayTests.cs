// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Core;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Core.Tests;

public sealed class ReplayTests
{
    private static Level100ActorDefinitionSet ActorDefinitions =>
        Level100TestActorDefinitions.Create();

    // The first tick a first run has player control. The released
    // LevelScript reaches player.Activate() when TUTORIAL_TECHNICIAN_01
    // clears, and the released message box holds the opening five messages
    // to Level100MissionTiming.MessageBoxAllowedTick + the advance gaps:
    // 182 +169 +6+210 +6+183 +6+163 +6+65 = 996. Two fresh app-owned Steam
    // runs measured the Battle Engine power flag at +0x580 changing 0 -> 1
    // at tick 1000 (rebuild/PROVENANCE.md); the four-tick residual is the
    // 50 ms sampler. The old value here was 790, which is the same sum with
    // the gate and the gaps both absent.
    private const int FirstRunControlTick = 996;
    [Fact]
    public void FirstFlightReplay_IsDeterministic()
    {
        CommandTape tape = LoadFirstFlightTape();

        ReplayResult first = ReplayRunner.Run(tape, ActorDefinitions);
        ReplayResult second = ReplayRunner.Run(tape, ActorDefinitions);

        Assert.Equal(first.FinalStateHash, second.FinalStateHash);
        Assert.Equal(first.TraceHash, second.TraceHash);
        Assert.Equal(tape.DurationTicks, first.FinalState.Tick);
    }

    [Fact]
    public void ReplayComparison_SameTapeReportsNoDivergence()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "same-tape",
            1,
            3,
            null,
            null,
            [new CommandSpan(1, 1, 1, 0)]);

        ReplayComparison comparison = ReplayRunner.Compare(
            tape,
            tape,
            ActorDefinitions);

        Assert.Equal(
            "onslaught-rebuild-replay-diff.v1",
            comparison.Diff.SchemaVersion);
        Assert.Equal(comparison.Before.TraceHash, comparison.After.TraceHash);
        Assert.Equal(
            comparison.Before.FinalStateHash,
            comparison.After.FinalStateHash);
        Assert.False(comparison.Diff.TraceHashMismatch);
        Assert.False(comparison.Diff.BehavioralEventMismatch);
        Assert.False(comparison.Diff.FinalStateMismatch);
        Assert.Null(comparison.Diff.FirstDivergence);
    }

    [Fact]
    public void ReplayComparison_OneTickInputMutationReportsTheExactFirstDivergence()
    {
        var before = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "before",
            1,
            2,
            null,
            null,
            []);
        var after = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "after",
            1,
            2,
            null,
            null,
            [new CommandSpan(1, 1, 1, 0)]);

        ReplayComparison comparison = ReplayRunner.Compare(
            before,
            after,
            ActorDefinitions);

        Assert.True(comparison.Diff.TraceHashMismatch);
        Assert.True(comparison.Diff.BehavioralEventMismatch);
        Assert.False(comparison.Diff.FinalStateMismatch);
        Assert.NotNull(comparison.Diff.FirstDivergence);
        ReplayDivergence divergence = comparison.Diff.FirstDivergence!;
        Assert.Equal(1, divergence.Tick);
        Assert.Equal("input.moveX", divergence.Category);
        Assert.Equal("0", divergence.BeforeValue);
        Assert.Equal("1", divergence.AfterValue);
    }

    [Fact]
    public void ReplayComparison_DistinguishesFinalStateMismatch()
    {
        var before = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "seed-one",
            1,
            1,
            null,
            null,
            []);
        var after = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "seed-two",
            2,
            1,
            null,
            null,
            []);

        ReplayComparison comparison = ReplayRunner.Compare(
            before,
            after,
            ActorDefinitions);

        Assert.True(comparison.Diff.TraceHashMismatch);
        Assert.True(comparison.Diff.BehavioralEventMismatch);
        Assert.True(comparison.Diff.FinalStateMismatch);
        Assert.NotNull(comparison.Diff.FirstDivergence);
        Assert.Equal("replay.seed", comparison.Diff.FirstDivergence!.Category);
        Assert.Equal("1", comparison.Diff.FirstDivergence.BeforeValue);
        Assert.Equal("2", comparison.Diff.FirstDivergence.AfterValue);
    }

    [Fact]
    public void CommandTape_RoundTripsWithoutChangingReplayState()
    {
        CommandTape tape = LoadFirstFlightTape();
        string serialized = CommandTapeCodec.Serialize(tape);
        CommandTape roundTripped = CommandTapeCodec.Deserialize(serialized);

        Assert.EndsWith("\n", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", serialized, StringComparison.Ordinal);
        ReplayResult original = ReplayRunner.Run(tape, ActorDefinitions);
        ReplayResult replayed = ReplayRunner.Run(roundTripped, ActorDefinitions);
        Assert.Equal(original.FinalStateHash, replayed.FinalStateHash);
        Assert.Equal(original.TraceHash, replayed.TraceHash);
        Assert.Equal(tape.Spans.Count, roundTripped.Spans.Count);
    }

    [Fact]
    public void CommandTape_RejectsOverlappingSpans()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "overlap",
            1,
            10,
            null,
            null,
            [
                new CommandSpan(0, 5, 0, 1),
                new CommandSpan(4, 2, 1, 0),
            ]);

        Assert.Throws<InvalidDataException>(tape.Validate);
    }

    [Fact]
    public void CommandTape_RejectsMissingSpanCollectionAsInvalidData()
    {
        const string json = """
            {
              "schemaVersion": "onslaught-rebuild-command-tape.v4",
              "name": "missing-spans",
              "seed": 1,
              "durationTicks": 10,
              "expectedFinalStateHash": null
            }
            """;

        Assert.Throws<InvalidDataException>(() => CommandTapeCodec.Deserialize(json));
    }

    [Fact]
    public void CommandTape_RejectsOverflowingSpanAsInvalidData()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "overflow",
            1,
            10,
            null,
            null,
            [new CommandSpan(int.MaxValue, 2, 0, 0)]);

        Assert.Throws<InvalidDataException>(tape.Validate);
    }

    [Fact]
    public void CommandTape_RejectsUnknownJsonProperties()
    {
        const string json = """
            {
              "schemaVersion": "onslaught-rebuild-command-tape.v4",
              "name": "unknown-property",
              "seed": 1,
              "durationTicks": 1,
              "expectedFinalStateHash": null,
              "spans": [],
              "firing": true
            }
            """;

        Assert.Throws<System.Text.Json.JsonException>(() => CommandTapeCodec.Deserialize(json));
    }

    [Fact]
    public void CommandTape_RejectsInvalidAxisAsInvalidData()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "invalid-axis",
            1,
            1,
            null,
            null,
            [new CommandSpan(0, 1, 2, 0)]);

        Assert.Throws<InvalidDataException>(tape.Validate);
    }

    [Fact]
    public void CommandTape_RejectsZeroSeed()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "zero-seed",
            0,
            1,
            null,
            null,
            []);

        Assert.Throws<InvalidDataException>(tape.Validate);
    }

    [Fact]
    public void CommandTape_RejectsMultiTickEdgeActions()
    {
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "held-edge",
            1,
            2,
            null,
            null,
            [new CommandSpan(0, 2, 0, 0, ToggleMode: true)]);

        Assert.Throws<InvalidDataException>(tape.Validate);
    }

    [Fact]
    public void CommandTape_FireReleaseEdgeRequiresExactlyOneTick()
    {
        var released = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "one-fire-release",
            1,
            1,
            null,
            null,
            [new CommandSpan(0, 1, 0, 0, Fire: true)]);
        released.Validate();

        var held = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "held-fire",
            1,
            2,
            null,
            null,
            [new CommandSpan(0, 2, 0, 0, Fire: true)]);

        Assert.Throws<InvalidDataException>(held.Validate);
    }

    [Fact]
    public void CommandTape_ChangeWeaponRoundTripsAsOneReleasedEdge()
    {
        var source = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "change-weapon",
            1,
            1,
            null,
            null,
            [new CommandSpan(0, 1, 0, 0, ChangeWeapon: true)]);

        CommandTape tape = CommandTapeCodec.Deserialize(
            CommandTapeCodec.Serialize(source));

        Assert.True(tape.Spans[0].ChangeWeapon);
        Assert.True(tape.Spans[0].ToInput().HasAction(SimActions.ChangeWeapon));

        var held = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "held-change-weapon",
            1,
            2,
            null,
            null,
            [new CommandSpan(0, 2, 0, 0, ChangeWeapon: true)]);
        Assert.Throws<InvalidDataException>(held.Validate);
    }

    [Fact]
    public void CommandTape_DefensivelyCopiesSpanCollections()
    {
        var source = new List<CommandSpan> { new(0, 1, 0, 1) };
        var tape = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "immutable-spans",
            1,
            1,
            null,
            null,
            source);

        source.Clear();

        Assert.Single(tape.Spans);
        var spans = Assert.IsAssignableFrom<IList<CommandSpan>>(tape.Spans);
        Assert.True(spans.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => spans.Clear());
    }

    [Fact]
    public void CommandSpan_LookX_RoundsTripsInJsonAndAffectsFacing()
    {
        var source = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "look-hold",
            1,
            FirstRunControlTick + 20,
            null,
            null,
            [new CommandSpan(FirstRunControlTick, 20, 0, 0, LookX: 1)]);
        string json = CommandTapeCodec.Serialize(source);
        CommandTape tape = CommandTapeCodec.Deserialize(json);
        Assert.Equal(1, tape.Spans[0].LookX);
        ReplayResult result = ReplayRunner.Run(tape, ActorDefinitions);
        Assert.Equal(1, result.FinalState.FacingX);
        Assert.Equal(0, result.FinalState.FacingZ);
    }

    [Fact]
    public void CommandSpan_AnalogLook_RoundTripsAndRepeatsIdentically()
    {
        var source = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "analog-look",
            1,
            FirstRunControlTick + 2,
            null,
            null,
            [new CommandSpan(
                FirstRunControlTick,
                1,
                0,
                0,
                LookXAnalogPermille: 365,
                LookYAnalogPermille: -183)]);

        string json = CommandTapeCodec.Serialize(source);
        CommandTape tape = CommandTapeCodec.Deserialize(json);
        ReplayResult first = ReplayRunner.Run(tape, ActorDefinitions);
        ReplayResult second = ReplayRunner.Run(tape, ActorDefinitions);

        Assert.Equal(365, tape.Spans[0].LookXAnalogPermille);
        Assert.Equal(-183, tape.Spans[0].LookYAnalogPermille);
        Assert.Equal(first.FinalStateHash, second.FinalStateHash);
        Assert.Equal(first.TraceHash, second.TraceHash);
        Assert.NotEqual(SimulationConstants.Level100PlayerStartYawMicroRad, first.FinalState.FacingYawMicroRad);
        Assert.NotEqual(0, first.FinalState.FacingPitchMicroRad);
    }

    [Fact]
    public void CommandSpan_LandingJets_RoundTripsAsAHeldDeterministicAction()
    {
        var source = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "landing-jets",
            1,
            1_001,
            null,
            null,
            [new CommandSpan(
                1_000,
                1,
                0,
                0,
                LandingJets: true)]);

        CommandTape tape = CommandTapeCodec.Deserialize(CommandTapeCodec.Serialize(source));
        ReplayResult first = ReplayRunner.Run(tape, ActorDefinitions);
        ReplayResult second = ReplayRunner.Run(tape, ActorDefinitions);

        Assert.True(tape.Spans[0].LandingJets);
        Assert.True(first.FinalState.LandingJetsActive);
        Assert.Equal(first.FinalStateHash, second.FinalStateHash);
        Assert.Equal(first.TraceHash, second.TraceHash);
    }

    [Fact]
    public void CommandSpan_MissingLookAxes_DefaultsToZero()
    {
        const string json = """
              {
                "schemaVersion": "onslaught-rebuild-command-tape.v4",
                "name": "no-look",
                "seed": 1,
                "durationTicks": 1,
                "spans": [
                  { "startTick": 0, "durationTicks": 1, "moveX": 0, "moveZ": 1 }
                ]
              }
              """;
        CommandTape tape = CommandTapeCodec.Deserialize(json);
        Assert.Equal(0, tape.Spans[0].LookX);
        Assert.Equal(0, tape.Spans[0].LookY);
        Assert.Equal(0, tape.Spans[0].LookXAnalogPermille);
        Assert.Equal(0, tape.Spans[0].LookYAnalogPermille);
    }

    [Fact]
    public void ReplayHashesIncludeContinuousYawBeforeFacingSectorChanges()
    {
        var looked = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "looked",
            1,
            FirstRunControlTick + 1,
            null,
            null,
            [new CommandSpan(FirstRunControlTick, 1, 0, 0, LookX: 1)]);
        var idled = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "idled",
            1,
            FirstRunControlTick + 1,
            null,
            null,
            []);

        ReplayResult lookedResult = ReplayRunner.Run(looked, ActorDefinitions);
        ReplayResult idledResult = ReplayRunner.Run(idled, ActorDefinitions);

        Assert.Equal(idledResult.FinalState.FacingX, lookedResult.FinalState.FacingX);
        Assert.Equal(idledResult.FinalState.FacingZ, lookedResult.FinalState.FacingZ);
        Assert.Equal(523_163, lookedResult.FinalState.FacingYawMicroRad);
        Assert.Equal(13_333, lookedResult.FinalState.WalkerYawVelocityMicroRadPerTick);
        Assert.NotEqual(idledResult.FinalStateHash, lookedResult.FinalStateHash);
        Assert.NotEqual(idledResult.TraceHash, lookedResult.TraceHash);
    }

    [Fact]
    public void ReplayTraceHash_DistinguishesHistoriesErasedByReset()
    {
        var movedThenReset = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "moved-reset",
            1,
            2,
            null,
            null,
            [
                new CommandSpan(0, 1, 1, 0),
                new CommandSpan(1, 1, 0, 0, Reset: true),
            ]);
        var idledThenReset = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "idle-reset",
            1,
            2,
            null,
            null,
            [new CommandSpan(1, 1, 0, 0, Reset: true)]);

        ReplayResult moved = ReplayRunner.Run(movedThenReset, ActorDefinitions);
        ReplayResult idled = ReplayRunner.Run(idledThenReset, ActorDefinitions);
        Assert.Equal(moved.FinalStateHash, idled.FinalStateHash);

        Assert.NotEqual(moved.TraceHash, idled.TraceHash);
    }

    [Fact]
    public void CoreAssembly_HasNoPresentationOrReferenceSourceDependency()
    {
        string[] references = typeof(Simulation).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Godot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("WinUI", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, name => name.Contains("OnslaughtCareerEditor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CoreCodec_DoesNotExposeFilesystemLoading()
    {
        Assert.Null(typeof(CommandTapeCodec).GetMethod("Load"));
    }

    [Fact]
    public void CoreSource_HasNoFilesystemProcessClockOrNetworkApiUsage()
    {
        string sourceRoot = Path.Combine(AppContext.BaseDirectory, "core-source");
        string[] forbiddenTokens =
        [
            "File.",
            "Directory.",
            "Path.",
            "FileStream",
            "FileInfo",
            "DirectoryInfo",
            "DriveInfo",
            "FileSystemWatcher",
            "StreamReader",
            "StreamWriter",
            "MemoryMappedFile",
            "Environment.",
            "Process.",
            "Console.",
            "AppContext.",
            "OperatingSystem.",
            "RuntimeInformation.",
            "DateTime.",
            "DateTimeOffset.",
            "Stopwatch.",
            "TimeProvider",
            "Task.Delay",
            "Task.Run",
            "Thread.",
            "Parallel.",
            "Guid.NewGuid",
            "new Random(",
            "Random.Shared",
            "RandomNumberGenerator",
            "HttpClient",
            "HttpMessageInvoker",
            "WebRequest",
            "WebClient",
            "TcpClient",
            "TcpListener",
            "UdpClient",
            "Socket",
            "NetworkStream",
            "Dns.",
        ];

        foreach (string path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                Assert.DoesNotContain(token, source, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void CoreProject_HasNoExternalOrCustomCodeInputs()
    {
        string projectPath = Path.Combine(
            AppContext.BaseDirectory,
            "core-project",
            "OnslaughtRebuild.Core.csproj");
        string projectText = File.ReadAllText(projectPath);
        var project = System.Xml.Linq.XDocument.Parse(projectText);

        string[] forbiddenElements =
        [
            "ProjectReference",
            "Reference",
            "PackageReference",
            "FrameworkReference",
            "PackageDownload",
            "NativeReference",
            "COMReference",
            "AdditionalFiles",
            "Analyzer",
            "Import",
            "UsingTask",
            "Target",
        ];
        string[] linkedInputElements = ["Compile", "Content", "EmbeddedResource", "None"];

        Assert.DoesNotContain(
            project.Descendants(),
            element => forbiddenElements.Contains(element.Name.LocalName, StringComparer.Ordinal));
        Assert.DoesNotContain(
            project.Descendants(),
            element =>
            {
                if (!linkedInputElements.Contains(element.Name.LocalName, StringComparer.Ordinal) ||
                    element.Attribute("Include") is not { Value: string include })
                {
                    return false;
                }

                string normalized = include.Replace('\\', '/');
                return Path.IsPathRooted(include) ||
                    normalized.Equals("..", StringComparison.Ordinal) ||
                    normalized.StartsWith("../", StringComparison.Ordinal) ||
                    normalized.Contains("/../", StringComparison.Ordinal);
            });
        Assert.DoesNotContain("references/Onslaught", projectText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("references\\Onslaught", projectText, StringComparison.OrdinalIgnoreCase);
    }

    private static CommandTape LoadFirstFlightTape()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "scenarios", "first-flight.v1.json");
        return CommandTapeCodec.Deserialize(File.ReadAllText(path));
    }

    // ------------------------------------------------------------------
    // P8 stage 1: CommandTape v5 (ChargeWeapon held, ZoomIn/ZoomOut edges)
    // and the recorder/builder seam. The pre-existing tests above stay on
    // CommandTape.CurrentSchemaVersion; these pin the new wire contract.
    // ------------------------------------------------------------------

    /// <summary>
    /// v4 -&gt; v5, 2026-08-23: <c>CommandSpan.ChargeWeapon</c> (held) and
    /// <c>CommandSpan.ZoomIn</c>/<c>CommandSpan.ZoomOut</c> (one-tick edges)
    /// were added so a recorded human tape can carry the complete consumed
    /// action set. Compatibility is the explicit migrate/read policy: a v4
    /// document is upgraded in place by <see cref="CommandTapeCodec.Deserialize"/>
    /// because its field set is a strict subset of v5's, while any other
    /// unknown schema still fails closed (see Level100SkipPanningTests.
    /// ATapeWrittenUnderTheOldSchemaIsRejectedRatherThanMisparsed, which pins
    /// the same law for v3).
    /// </summary>
    [Fact]
    public void V5_UpgradesAV4TapeExplicitlyWithoutReinterpretingFields()
    {
        const string v4 = """
            {
              "schemaVersion": "onslaught-rebuild-command-tape.v4",
              "name": "v4-tape",
              "seed": 1,
              "durationTicks": 10,
              "expectedFinalStateHash": null,
              "expectedTraceHash": null,
              "spans": [
                {
                  "startTick": 0, "durationTicks": 1, "moveX": 0, "moveZ": 1,
                  "lookXAnalogPermille": 365, "landingJets": true
                }
              ]
            }
            """;

        CommandTape tape = CommandTapeCodec.Deserialize(v4);

        // The upgrade stamps current identity and preserves every v4 field's
        // meaning exactly; no v5-only field appears from nowhere.
        Assert.Equal(CommandTape.CurrentSchemaVersion, tape.SchemaVersion);
        CommandSpan span = tape.Spans[0];
        Assert.Equal(0, span.MoveX);
        Assert.Equal(1, span.MoveZ);
        Assert.Equal(365, span.LookXAnalogPermille);
        Assert.True(span.LandingJets);
        Assert.False(span.ChargeWeapon);
        Assert.False(span.ZoomIn);
        Assert.False(span.ZoomOut);

        // Re-serializing writes the current schema with identical content.
        CommandTape readBack = CommandTapeCodec.Deserialize(
            CommandTapeCodec.Serialize(tape));
        Assert.Equal(CommandTape.IdentityOf(tape), CommandTape.IdentityOf(readBack));

        const string falseIdentity = """
            {
              "schemaVersion": "onslaught-rebuild-command-tape.v4",
              "name": "false-v4-identity",
              "seed": 1,
              "durationTicks": 1,
              "spans": [
                {
                  "startTick": 0, "durationTicks": 1, "moveX": 0, "moveZ": 0,
                  "zoomIn": true
                }
              ]
            }
            """;
        InvalidDataException rejected = Assert.Throws<InvalidDataException>(
            () => CommandTapeCodec.Deserialize(falseIdentity));
        Assert.Contains("v5-only", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void V5_ChargeWeaponAndZoomEdgesRoundTripWithTheirActionKinds()
    {
        var source = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "v5-actions",
            1,
            4,
            null,
            null,
            [
                new CommandSpan(0, 2, 0, 0, ChargeWeapon: true),
                new CommandSpan(2, 1, 0, 0, ZoomIn: true),
                new CommandSpan(3, 1, 0, 0, ZoomOut: true),
            ]);

        string json = CommandTapeCodec.Serialize(source);
        Assert.Contains("\"chargeWeapon\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"zoomIn\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"zoomOut\": true", json, StringComparison.Ordinal);

        CommandTape tape = CommandTapeCodec.Deserialize(json);
        Assert.True(tape.Spans[0].ChargeWeapon);
        Assert.True(tape.Spans[0].ToInput().HasAction(SimActions.ChargeWeapon));
        Assert.True(tape.Spans[1].ZoomIn);
        Assert.True(tape.Spans[1].ToInput().HasAction(SimActions.ZoomIn));
        Assert.True(tape.Spans[2].ZoomOut);
        Assert.True(tape.Spans[2].ToInput().HasAction(SimActions.ZoomOut));
    }

    [Fact]
    public void V5_MultiTickZoomEdgeIsRejectedLikeEveryOtherEdgeAction()
    {
        var held = new CommandTape(
            CommandTape.CurrentSchemaVersion,
            "held-zoom-out",
            1,
            2,
            null,
            null,
            [new CommandSpan(0, 2, 0, 0, ZoomOut: true)]);

        Assert.Throws<InvalidDataException>(held.Validate);
    }

    [Fact]
    public void Recorder_CoalescesContiguousIdenticalInputsIntoSortedNonOverlappingSpans()
    {
        var recorder = new CommandTapeRecorder();
        for (int tick = 0; tick < 3; tick++)
        {
            recorder.Observe(tick, new SimInput(1, 1, SimActions.ChargeWeapon, 1, 0, 250, -125));
        }

        // The look axes decay to zero on the fourth tick while the charge
        // level stays held: a different input, so the run splits there and the
        // tail keeps only what is still nonzero.
        recorder.Observe(3, new SimInput(0, 0, SimActions.ChargeWeapon));

        CommandTape tape = recorder.Build("coalesce", 9u);

        Assert.Equal(9u, tape.Seed);
        Assert.Equal(4, tape.DurationTicks);
        Assert.Equal(2, tape.Spans.Count);
        CommandSpan held = tape.Spans[0];
        Assert.Equal((0, 3), (held.StartTick, held.DurationTicks));
        Assert.Equal(1, held.MoveX);
        Assert.True(held.ChargeWeapon);
        Assert.Equal(250, held.LookXAnalogPermille);
        Assert.Equal(-125, held.LookYAnalogPermille);

        // Sorted and non-overlapping by construction, but validated anyway.
        tape.Validate();
    }

    [Fact]
    public void Recorder_PreservesExactTickBoundariesWhenInputsChange()
    {
        var recorder = new CommandTapeRecorder();
        recorder.Observe(0, new SimInput(0, 1));
        recorder.Observe(1, new SimInput(0, 1));
        recorder.Observe(2, new SimInput(1, 0));

        CommandTape tape = recorder.Build("boundaries", 3);

        Assert.Equal(2, tape.Spans.Count);
        Assert.Equal((0, 2), (tape.Spans[0].StartTick, tape.Spans[0].DurationTicks));
        Assert.Equal((2, 1), (tape.Spans[1].StartTick, tape.Spans[1].DurationTicks));
    }

    [Fact]
    public void Recorder_RejectsSkippedOrRepeatedObservations()
    {
        var skipped = new CommandTapeRecorder();
        skipped.Observe(0, SimInput.Idle);

        ArgumentException gap = Assert.Throws<ArgumentException>(
            () => skipped.Observe(2, SimInput.Idle));
        Assert.Contains("tick 1", gap.Message, StringComparison.Ordinal);

        var repeated = new CommandTapeRecorder();
        repeated.Observe(0, SimInput.Idle);
        repeated.Observe(1, SimInput.Idle);
        ArgumentException duplicate = Assert.Throws<ArgumentException>(
            () => repeated.Observe(1, SimInput.Idle));
        Assert.Contains("already observed", duplicate.Message, StringComparison.Ordinal);

        var first = new CommandTapeRecorder();
        ArgumentException start = Assert.Throws<ArgumentException>(
            () => first.Observe(3, SimInput.Idle));
        Assert.Contains("tick 0 was never observed", start.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recorder_DerivesTapeIdentityFromLfCanonicalJson()
    {
        var recorder = new CommandTapeRecorder();
        recorder.Observe(0, new SimInput(0, 1));

        CommandTape tape = recorder.Build("identity", 1);
        string canonical =
            CommandTapeCodec.Serialize(tape).ReplaceLineEndings("\n");
        string identity = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();

        Assert.Equal(identity, CommandTape.IdentityOf(tape));
    }

    [Fact]
    public void RecordedSpans_ReplayToTheirRecordedTraceUnderRunner()
    {
        // Warm to first control, then record eight consumed ticks carrying a
        // held charge level plus pointer-look axes. The recorder observes the
        // whole session from tick 0 — warmup included, exactly as a real
        // capture does — so the recorded tape replays to exactly the hashes
        // observed at capture time.
        const uint seed = 11;
        var definitions = ActorDefinitions;
        var simulation = new Simulation(seed, definitions);
        using var recorder = new CommandTapeRecorder();
        for (int tick = 0; tick < WarmupTicksForRecording; tick++)
        {
            WorldSnapshot state = simulation.Step(SimInput.Idle);
            recorder.Observe(tick, SimInput.Idle, state);
        }

        WorldSnapshot final = simulation.Snapshot;
        for (int offset = 0; offset < 8; offset++)
        {
            var input = new SimInput(
                0,
                1,
                SimActions.ChargeWeapon | SimActions.LandingJets,
                0,
                0,
                365,
                -183);
            final = simulation.Step(input);
            recorder.Observe(WarmupTicksForRecording + offset, input, final);
        }

        CommandTape tape = recorder.BuildObserved("recorded-replay", seed);

        ReplayResult replayed = ReplayRunner.Run(
            CommandTapeCodec.Deserialize(CommandTapeCodec.Serialize(tape)),
            definitions);

        Assert.Equal(tape.ExpectedTraceHash, replayed.TraceHash);
        Assert.Equal(StateHasher.ComputeHex(final), replayed.FinalStateHash);
        Assert.Equal(tape.ExpectedFinalStateHash, replayed.FinalStateHash);
    }

    [Fact]
    public void ObservedRecording_PreservesTransientStateDivergenceEvenWhenTheFinalStateMatches()
    {
        const uint seed = 11;
        var definitions = ActorDefinitions;
        var simulation = new Simulation(seed, definitions);
        using var recorder = new CommandTapeRecorder();
        for (int tick = 0; tick < 3; tick++)
        {
            WorldSnapshot actual = simulation.Step(SimInput.Idle);
            // Inject one wrong intermediate observation, then return to the
            // genuine simulation snapshots. A trace derived by replay would
            // erase this divergence even though it happened during recording.
            WorldSnapshot observed = tick == 0 ? actual with { Hull = actual.Hull - 1 } : actual;
            recorder.Observe(tick, SimInput.Idle, observed);
        }

        CommandTape tape = recorder.BuildObserved("transient-divergence", seed);
        ReplayResult replayed = ReplayRunner.Run(tape, definitions);

        Assert.Equal(tape.ExpectedFinalStateHash, replayed.FinalStateHash);
        Assert.NotEqual(tape.ExpectedTraceHash, replayed.TraceHash);
    }

    [Fact]
    public void ObservedRecording_RejectsMissingOrOutOfOrderSnapshots()
    {
        var simulation = new Simulation(11, ActorDefinitions);
        WorldSnapshot first = simulation.Step(SimInput.Idle);
        WorldSnapshot second = simulation.Step(SimInput.Idle);
        using var recorder = new CommandTapeRecorder();

        Assert.Throws<ArgumentException>(() => recorder.Observe(0, SimInput.Idle, second));
        Assert.Equal(0, recorder.NextTick);
        recorder.Observe(0, SimInput.Idle, first);
        Assert.Throws<InvalidOperationException>(() => recorder.Observe(1, SimInput.Idle));
        recorder.Observe(1, SimInput.Idle, second);
        Assert.Equal(2, recorder.BuildObserved("complete-observations", 11).DurationTicks);

        using var inputsOnly = new CommandTapeRecorder();
        inputsOnly.Observe(0, SimInput.Idle);
        Assert.Throws<InvalidOperationException>(() => inputsOnly.Observe(1, SimInput.Idle, second));
        Assert.Throws<InvalidOperationException>(() => inputsOnly.BuildObserved("missing-first-state", 11));
    }

    /// <summary>
    /// The recorder's merge law, pinned directly on
    /// <see cref="CommandTapeBuilder.Merge"/>: contiguous identical inputs form
    /// one span, a changed input splits the run at its own tick, a held level
    /// and an edge can share one tick's input, and all-idle runs are dropped.
    /// </summary>
    [Fact]
    public void Builder_MergesIdenticalRunsSplitsOnChangeAndDropsIdle()
    {
        var observations = new List<(int Tick, SimInput Input)>
        {
            (0, new SimInput(0, 1)),
            (1, new SimInput(0, 1)),
            (2, SimInput.Idle),
            (3, new SimInput(1, 0, SimActions.Fire | SimActions.ZoomIn)),
        };

        List<CommandSpan> spans = CommandTapeBuilder.Merge(observations);

        Assert.Equal(2, spans.Count);
        Assert.Equal((0, 2), (spans[0].StartTick, spans[0].DurationTicks));
        Assert.Equal(1, spans[0].MoveZ);
        Assert.Equal((3, 1), (spans[1].StartTick, spans[1].DurationTicks));
        Assert.True(spans[1].Fire);
        Assert.True(spans[1].ZoomIn);
        Assert.True(spans[1].ToInput().HasAction(SimActions.Fire));

        // Identical edge inputs on adjacent ticks MUST remain two one-tick
        // spans; coalescing them would violate the v5 edge law.
        List<CommandSpan> adjacentEdges = CommandTapeBuilder.Merge(
        [
            (0, new SimInput(0, 0, SimActions.ZoomIn)),
            (1, new SimInput(0, 0, SimActions.ZoomIn)),
        ]);
        Assert.Equal(2, adjacentEdges.Count);
        Assert.All(adjacentEdges, span => Assert.Equal(1, span.DurationTicks));

        // An idle-only observation list produces a valid empty tape.
        List<CommandSpan> idleOnly = CommandTapeBuilder.Merge(
        [
            (0, SimInput.Idle),
            (1, SimInput.Idle),
        ]);
        Assert.Empty(idleOnly);
    }

    private const int WarmupTicksForRecording = 1004;
}
