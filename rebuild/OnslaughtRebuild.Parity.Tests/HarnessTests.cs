// SPDX-License-Identifier: GPL-3.0-only

using System.Text;
using OnslaughtRebuild.Parity.Platform;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class HarnessTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static RetailTape Tape(string text) => RetailTape.Parse(Encoding.ASCII.GetBytes(text));

    [Fact]
    public void TapeKeepsFileOrderWithinAFrame()
    {
        RetailTape tape = RetailTape.Load(Fixture("synthetic.tape"));

        Assert.Equal(5, tape.Events.Count);
        Assert.Equal(3, tape.FirstFrame);
        Assert.Equal(6, tape.LastFrame);
        Assert.Equal(64, tape.Sha256.Length);
        RetailTapeEvent[] frame4 = tape.EventsAt(4).ToArray();
        Assert.Equal(new RetailMouseEvent(4, -2, 1, 0, 1), frame4[0]);
        Assert.Equal(new RetailKeyEvent(4, false, 0x57, 0x11, "w"), frame4[1]);
        Assert.True(tape.EventsAt(5).IsEmpty);
        Assert.Equal(new RetailKeyEvent(6, true, 0x1e, 0x1e, "a"), tape.EventsAt(6)[0]);
    }

    [Fact]
    public void CeTapeKeepsCharacterAndWindowEventsAndTheirClientSizes()
    {
        const string text = "# onslaught tape 2\nW_CLIENT 1280 720\n"
            + "W 3 move 100 200 0\nK 3 down 41 1e a\nC 3 61 1e\nM 3 -2 1 0 1\n"
            + "W_CLIENT 640 480\nW 4 wheel -10 20 ff880001\n";
        RetailTape tape = Tape(text);
        Assert.Equal(2, tape.Version);
        Assert.True(tape.UsesTapeCursor);
        Assert.Equal(5, tape.Events.Count);
        RetailTapeEvent[] at3 = tape.EventsAt(3).ToArray();
        Assert.Equal(new RetailWindowMouseEvent(3, MouseMessage.Move, 100, 200, 0, 1280, 720), at3[0]);
        Assert.Equal(new RetailKeyEvent(3, true, 0x41, 0x1e, "a"), at3[1]);
        Assert.Equal(new RetailCharacterEvent(3, 0x61, 0x1e), at3[2]);
        Assert.Equal(new RetailMouseEvent(3, -2, 1, 0, 1), at3[3]);
        Assert.Equal(new RetailWindowMouseEvent(4, MouseMessage.Wheel, -10, 20, 0xff880001, 640, 480), tape.EventsAt(4)[0]);
    }

    [Fact]
    public void CeTapeAcceptsWindowsTextModeButRejectsMixedLineEndings()
    {
        RetailTape tape = Tape("# onslaught tape 2\r\nW_CLIENT 640 480\r\nW 1 move 3 4 0\r\n");
        Assert.Equal(2, tape.Version);
        Assert.Single(tape.Events);
        Assert.Throws<FormatException>(() => Tape("# onslaught tape 2\r\nW_CLIENT 640 480\n"));
    }

    [Theory]
    [InlineData("# onslaught tape 3\n")]
    [InlineData("# onslaught tape invalid\n")]
    [InlineData("W_CLIENT -1 480\n")]
    [InlineData("W_CLIENT 640\n")]
    [InlineData("W 1 invalid 1 2 0\n")]
    [InlineData("W 1 move 1 2 100000000\n")]
    [InlineData("C 1 61 200\n")]
    [InlineData("C 1 61 1e\nW 0 move 1 2 0\n")]
    public void CeTapeRejectsUnsupportedOrMalformedEvents(string text) =>
        Assert.Throws<FormatException>(() => Tape(text));

    [Fact]
    public void LegacyRunnerRefusesWindowEventsInsteadOfDroppingThem()
    {
        RetailTape tape = Tape("# onslaught tape 2\nW_CLIENT 640 480\nW 1 move 3 4 0\n");
        Assert.Throws<NotSupportedException>(() => ParityRunner.Run(new RecordingSimulation(), tape, 1, 1));
        // Even without a W line, version 2 changes GetCursorPos's source for the whole process.
        RetailTape cursorOnly = Tape("# onslaught tape 2\nM 1 0 0 0 0\n");
        Assert.Throws<NotSupportedException>(() => ParityRunner.Run(new RecordingSimulation(), cursorOnly, 1, 1));
    }

    [Theory]
    [InlineData("K 5 down 57 11 w\nK 4 up 57 11 w\n")]
    [InlineData("K 5 pressed 57 11 w\n")]
    [InlineData("K 5 down 57 11 w\r\n")]
    [InlineData("M 5 0 0 0 8\n")]
    [InlineData("M 0 0 0 0 0\n")]
    [InlineData("K 4294967296 down 41 1e a\n")]
    [InlineData("J 5 0 0 0 0\n")]
    [InlineData("K 5 down 157 11 w\n")]
    public void TapeRejectsMalformedLines(string text)
    {
        Assert.Throws<FormatException>(() => Tape(text));
    }

    [Fact]
    public void ProbeTraceCollapsesIdenticalRepeatsAndRoundTrips()
    {
        string text = File.ReadAllText(Fixture("synthetic.probes"));
        ProbeTrace trace = ProbeTrace.Parse(text);

        Assert.Equal(new long[] { 2, 4 }, trace.Frames);
        var writer = new StringWriter();
        trace.Write(writer);
        Assert.Equal(text.Replace("P 4 LIFE 0000a04100000040\nP 4 LIFE", "P 4 LIFE", StringComparison.Ordinal), writer.ToString());
        Assert.Equal("100", ProbeCatalog.Describe(ProbeCatalog.Level, trace.At(2)![ProbeCatalog.Level]));
        Assert.Equal("value=11223344 seed=aabbccdd", ProbeCatalog.Describe(ProbeCatalog.Random, trace.At(2)![ProbeCatalog.Random]));
    }

    [Theory]
    [InlineData("P 2 LVL 64000000\nP 2 LVL 65000000\n")]
    [InlineData("P 2 LVL 640000\n")]
    [InlineData("P 2 XYZ 64000000\n")]
    [InlineData("P 2 LVL 6400000g\n")]
    [InlineData("Q 2 LVL 64000000\n")]
    public void ProbeTraceRejectsConflictsAndBadFields(string text)
    {
        Assert.Throws<FormatException>(() => ProbeTrace.Parse(text));
    }

    [Fact]
    public void IdenticalTracesAreOneToOne()
    {
        ProbeTrace golden = ProbeTrace.Load(Fixture("synthetic.probes"));
        ProbeTrace candidate = ProbeTrace.Load(Fixture("synthetic.probes"));

        TraceComparison comparison = TraceComparison.Compare(golden, candidate);

        Assert.True(comparison.IsOneToOne);
        Assert.Null(comparison.FirstDivergenceFrame);
        Assert.Contains("verdict: 1:1", comparison.Report(golden, candidate), StringComparison.Ordinal);
        Assert.Equal(1, comparison.Probes.Single(p => p.Name == ProbeCatalog.Random).ReferenceChanges);
        Assert.Equal(0, comparison.Probes.Single(p => p.Name == ProbeCatalog.Position).ReferenceChanges);
    }

    [Fact]
    public void EmptyTracesCannotBeAdmittedOrCompared()
    {
        Assert.Throws<FormatException>(() => ProbeTrace.Parse(""));
        ProbeTrace valid = ProbeTrace.Parse("P 1 EVT 00000000\n");
        Assert.Throws<InvalidDataException>(() => TraceComparison.Compare(new ProbeTrace(), valid));
        Assert.Throws<InvalidDataException>(() => TraceComparison.Compare(valid, new ProbeTrace()));
    }

    [Theory]
    [InlineData("P 50 EVT 00000000\nP 100 EVT 00000000\n", "FT", 50)]
    [InlineData("P 50 EVT 00000000\nP 50 FT 00000000\n", "EVT", 100)]
    public void ReferenceCoverageRequiresEveryScheduledField(string text, string missing, int present)
    {
        var error = Assert.Throws<InvalidDataException>(() => ProbeTrace.Parse(text)
            .RequireCoverage(50, 100, 50, "EVT", "FT"));
        Assert.Contains($"{missing} at Present {present}", error.Message);
    }

    [Fact]
    public void OneDifferentByteIsTheFirstDivergence()
    {
        ProbeTrace golden = ProbeTrace.Load(Fixture("synthetic.probes"));
        ProbeTrace candidate = ProbeTrace.Parse(File.ReadAllText(Fixture("synthetic.probes"))
            .Replace("P 4 RNG 45332211ddccbbaa", "P 4 RNG 46332211ddccbbaa", StringComparison.Ordinal));

        TraceComparison comparison = TraceComparison.Compare(golden, candidate);

        Assert.False(comparison.IsOneToOne);
        Assert.Equal(4, comparison.FirstDivergenceFrame);
        ProbeComparison rng = comparison.Probes.Single(p => p.Name == ProbeCatalog.Random);
        Assert.Equal((2, 1, 0), (rng.ComparedSamples, rng.MatchingSamples, rng.MissingSamples));
        Assert.True(comparison.Probes.Where(p => p.Name != ProbeCatalog.Random).All(p => p.Exact));
    }

    [Fact]
    public void AMatchOnlyUnderAFrameOffsetIsNotOneToOne()
    {
        ProbeTrace golden = ProbeTrace.Load(Fixture("synthetic.probes"));
        ProbeTrace shifted = ProbeTrace.Parse(File.ReadAllText(Fixture("synthetic.probes"))
            .Replace("P 2 ", "P 1 ", StringComparison.Ordinal).Replace("P 4 ", "P 3 ", StringComparison.Ordinal));

        TraceComparison unshifted = TraceComparison.Compare(golden, shifted);
        TraceComparison offset = TraceComparison.Compare(golden, shifted, frameOffset: 1);

        Assert.True(unshifted.Probes.All(p => p.MissingSamples == 2));
        Assert.True(offset.Probes.All(p => p.Exact));
        Assert.False(offset.IsOneToOne);
        Assert.Contains("probes equal only under a non-zero frame offset", offset.Report(golden, shifted), StringComparison.Ordinal);
    }

    [Fact]
    public void FirstTickSampleReadsEventTime()
    {
        ProbeTrace golden = ProbeTrace.Load(Fixture("synthetic.probes"));

        Assert.Equal((4L, 1), TraceComparison.FirstTickSample(golden));
    }

    [Fact]
    public void RunnerReadsProbesBeforeDeliveringThatFramesEvents()
    {
        var simulation = new RecordingSimulation();

        ProbeTrace trace = ParityRunner.Run(simulation, RetailTape.Load(Fixture("synthetic.tape")), maxFrames: 6, sampleEvery: 2);

        Assert.Equal(new long[] { 2, 4, 6 }, trace.Frames);
        Assert.Equal(
            [
                "present 1", "present 2", "probes 2", "present 3", "key 3 down 11", "mouse 3 3,0 b0",
                "present 4", "probes 4", "mouse 4 -2,1 b1", "key 4 up 11", "present 5", "present 6", "probes 6",
                "key 6 down 1e",
            ],
            simulation.Log);
    }

    [Fact]
    public void RunnerRefusesASimulationThatSkipsAPresent()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ParityRunner.Run(new RecordingSimulation { Step = 2 }, RetailTape.Load(Fixture("synthetic.tape")), 6, 2));
    }

    [Fact]
    public void UnimplementedSimulationReadsNoProbe()
    {
        ProbeTrace trace = ParityRunner.Run(new UnimplementedSimulation(), RetailTape.Load(Fixture("synthetic.tape")), 10, 5);

        Assert.Equal(0, trace.SampleCount);
    }

    [RetailDataFact]
    public void RetailLevel100TapeAndGoldenParse()
    {
        RetailTape tape = RetailTape.Load(RetailData.File("tapes/level100-keys-mouse-10000f.tape"));
        ProbeTrace golden = ProbeTrace.Load(RetailData.File("golden/m100-or.retail.probes"));

        Assert.Equal(4896, tape.Events.Count);
        Assert.Equal(3300, tape.FirstFrame);
        Assert.Equal(200, golden.SampleCount);
        Assert.Equal(50, golden.Frames.First());
        Assert.Equal(10000, golden.Frames.Last());

        // REPORT-6 section 1 lists 138 / 94 / 0 / 0 / 138 changes (EVT / POS / LIFE / SC / RNG) in retail's trace.
        TraceComparison self = TraceComparison.Compare(golden, golden);
        int Changes(string name) => self.Probes.Single(p => p.Name == name).ReferenceChanges;
        Assert.Equal((138, 94, 0, 0, 138),
            (Changes(ProbeCatalog.EventTime), Changes(ProbeCatalog.Position), Changes(ProbeCatalog.LifeEnergy),
             Changes(ProbeCatalog.Score), Changes(ProbeCatalog.Random)));
        Assert.True(self.IsOneToOne);
    }

    private sealed class RecordingSimulation : IParitySimulation
    {
        public List<string> Log { get; } = [];

        public long Step { get; init; } = 1;

        public long PresentCount { get; private set; }

        public void AdvanceToNextPresent()
        {
            PresentCount += Step;
            Log.Add($"present {PresentCount}");
        }

        public void DeliverKey(RetailKeyEvent key) =>
            Log.Add($"key {key.Frame} {(key.Down ? "down" : "up")} {key.ScanCode:x2}");

        public void DeliverMouse(RetailMouseEvent mouse) =>
            Log.Add($"mouse {mouse.Frame} {mouse.DeltaX},{mouse.DeltaY} b{mouse.Buttons}");

        public void ReadProbes(long frame, ProbeTrace trace)
        {
            Log.Add($"probes {frame}");
            trace.Add(frame, ProbeCatalog.Level, BitConverter.GetBytes(100));
        }
    }
}

public sealed class ProbeTraceNullTests
{
    [Fact]
    public void NullProbeValuesParseCompareAndWriteBack()
    {
        // Invented values: a sample where the player's battle engine does not exist reports POS and LIFE as null.
        const string text = "P 50 EVT 00000000\nP 50 POS null\nP 50 LIFE null\nP 100 POS 0000803f0000004000004040\n";
        ProbeTrace trace = ProbeTrace.Parse(text);

        Assert.True(trace.TryGet(50, ProbeCatalog.Position, out byte[] pos));
        Assert.Empty(pos);
        Assert.Equal("null", ProbeCatalog.Describe(ProbeCatalog.Position, pos));
        var writer = new StringWriter();
        trace.Write(writer);
        Assert.Contains("P 50 POS null\n", writer.ToString());
        Assert.True(TraceComparison.Compare(trace, ProbeTrace.Parse(writer.ToString()), 0).IsOneToOne);
    }
}
