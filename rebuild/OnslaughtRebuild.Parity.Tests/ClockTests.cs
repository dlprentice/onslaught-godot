// SPDX-License-Identifier: GPL-3.0-only

using System.Numerics;
using OnslaughtRebuild.Parity.Clock;

namespace OnslaughtRebuild.Parity.Tests;

/// <summary>
/// The M2 clock: the replay shim's virtual timers, CPCPlatform::GetSysTimeFloat, the event clock and CGame::MainLoop's
/// cadence, checked against exact references (rational arithmetic rounded once to 24 bits) and, where the retail data is
/// present, against the level-100 golden trace.
/// </summary>
public sealed class ClockTests
{
    [Fact]
    public void TheShimsTimerStepsOncePerPresent()
    {
        var clock = new PlatformClock(realQpc: 5_000_000, realMilliseconds: 7_000, realFileTime: 1_000);
        Assert.Equal(333333, PlatformClock.QpcTicksPerPresent);
        Assert.Equal(5_000_000, clock.QueryPerformanceCounter()); // real (here: held) before the first Present
        Assert.Equal(7_000u, clock.TimeGetTime());

        clock.Present();
        Assert.Equal(5_000_000 + 333333, clock.QueryPerformanceCounter());
        Assert.Equal(7_000u + 33u, clock.TimeGetTime());          // (DWORD)(1 * 0.0333333 * 1000.0)
        Assert.Equal(1_000ul + 333333ul, clock.GetSystemTimeAsFileTime());
        for (int i = 0; i < 29; i++)
        {
            clock.Present();
        }

        Assert.Equal(5_000_000 + 30 * 333333L, clock.QueryPerformanceCounter());
        Assert.Equal(7_000u + 999u, clock.TimeGetTime());          // 30 * 0.0333333 * 1000 = 999.999
    }

    [Fact]
    public void TwoMillionTimerCallsWithoutAPresentMoveTimeOneStep()
    {
        var clock = new PlatformClock();
        clock.Present();
        long first = clock.QueryPerformanceCounter();
        for (int i = 1; i < PlatformClock.SpinEscapeCalls; i++)
        {
            Assert.Equal(first, clock.QueryPerformanceCounter());
        }

        Assert.Equal(first + PlatformClock.QpcTicksPerPresent, clock.QueryPerformanceCounter()); // call 2,000,001
        Assert.Equal(1, clock.SpinEscapes);
        clock.Present(); // restarts the spin count; the escape stays in the time base
        Assert.Equal(first + 2 * PlatformClock.QpcTicksPerPresent, clock.QueryPerformanceCounter());
    }

    [Fact]
    public void GetSysTimeFloatRoundsTheExactQuotientOnce()
    {
        var clock = new PlatformClock(realQpc: 123_456_789);
        clock.Present();
        Assert.Equal(0.0f, clock.GetSysTimeFloat()); // first call: first_seen
        int naiveDiffers = 0;
        for (long n = 1; n <= 20_000; n++)
        {
            clock.Present();
            long ticks = n * PlatformClock.QpcTicksPerPresent;
            float expected = Round24(new BigInteger(ticks), new BigInteger(PlatformClock.QpcFrequency));
            Assert.Equal(expected, clock.GetSysTimeFloat());
            if ((float)ticks / 1e7f != expected)
            {
                naiveDiffers++; // a float conversion of the tick count first would round twice
            }
        }

        Assert.True(naiveDiffers > 0);
    }

    [Fact]
    public void TheEventManagerStoresCountTimesTheTick()
    {
        var events = new OnslaughtRebuild.Parity.Events.EventManager();
        events.Init();
        for (int count = 1; count <= 20_000; count++)
        {
            events.AdvanceTime();
            // 0.05f is exactly 13421773 / 2^28.
            Assert.Equal(Round24(new BigInteger(count) * 13421773, BigInteger.One << 28), events.Time);
        }
    }

    [Fact]
    public void MainLoopRendersOnceOrTwicePerTickAtThirtyPresentsASecond()
    {
        var sim = new ClockSimulation(level: 100, baseReadPresent: 100, levelStartPresent: 100);
        var renderCounts = new List<int>();
        int lastTicks = sim.Events.FrameCount;
        for (int i = 0; i < 10_000; i++)
        {
            sim.AdvanceToNextPresent();
            if (sim.Events.FrameCount != lastTicks)
            {
                renderCounts.Add(sim.Loop.LastRenderCount);
                lastTicks = sim.Events.FrameCount;
            }
        }

        // Each MainLoop is one tick (0.05 s) and Presents until the clock has passed the next frame boundary; with a
        // Present every 1/30 s that is 1 or 2 Presents, 3 Presents per 2 ticks.
        // The first two samples are the pre-run's ticks (seen at the loading screen's Present) and the first MainLoop
        // pass, before any pass has finished rendering.
        Assert.All(renderCounts.Skip(2), r => Assert.InRange(r, 1, 2));
        Assert.Equal(3.0, PresentsPerTwoTicks(renderCounts.Skip(201).Take(6000).ToList()));
        // The pre-run's 60 ticks (FINISHED_PRE_RUN), then 2 ticks per 3 Presents after the level start (the loading
        // screen's Present and 9,899 MainLoop Presents): mBaseTime was read just before, so there is nothing to catch up.
        Assert.Equal(60 + 9_900 * 2 / 3, sim.Events.FrameCount);
    }

    [Fact]
    public void FrameTimeIsTheClockAtTheLastRenderBeforeEachPresent()
    {
        var sim = new ClockSimulation(level: 100, baseReadPresent: 3100, levelStartPresent: 3100);
        for (long k = 1; k <= 10_000; k++)
        {
            sim.AdvanceToNextPresent();
            if (k > 3101)
            {
                // GetSysTimeFloat counts from Present 1, and FT is read at the render before Present k (Present 3101 is
                // the loading screen's, after the pre-run).
                float expected = Round24(new BigInteger((k - 2) * PlatformClock.QpcTicksPerPresent),
                    new BigInteger(PlatformClock.QpcFrequency));
                Assert.Equal(expected, sim.Loop.FrameTime);
            }
        }
    }

    [RetailDataFact]
    public void Level100GoldenEventTimeFrameTimeAndLevelMatchEverySample()
    {
        ProbeTrace golden = ProbeTrace.Load(RetailData.File("golden/m100-or.retail.probes"));
        golden.RequireCoverage(50, 10000, 50, "EVT", "FT", "LVL");
        // `-level 100` without -skipfmv: the intro FMV released the loading screen, so MainLoop follows PreRun directly
        // (C's per-Present run with these settings matches at all 3,400 Presents this way, base read at 3100).
        var sim = new ClockSimulation(level: 100, baseReadPresent: 3100, levelStartPresent: 3100,
            loadingScreenAfterPreRun: false);
        // The clock reads no input, so the run needs no tape: Present by Present, sampling as the harness does.
        var ours = new ProbeTrace();
        while (sim.PresentCount < 10_000)
        {
            sim.AdvanceToNextPresent();
            if (sim.PresentCount % 50 == 0)
            {
                sim.ReadProbes(sim.PresentCount, ours);
            }
        }

        TraceComparison comparison = TraceComparison.Compare(golden, ours);
        foreach (string name in new[] { ProbeCatalog.EventTime, ProbeCatalog.FrameTime, ProbeCatalog.Level })
        {
            ProbeComparison probe = comparison.Probes.Single(p => p.Name == name);
            Assert.True(probe.Exact, $"{name}: {probe}");
            Assert.Equal(200, probe.ReferenceSamples);
        }
    }

    /// <summary>
    /// C's per-Present retail run of level 100 (golden-640: -level 100 -skipfmv -forcewindowed -res 640 480, probes
    /// after every Present 1..3400): the level is taken and its base time read after Present 2, PreRun's 60 ticks run in
    /// the same gap, Present 3 is the loading screen at 100% (EVT 3.0, FT 0) and MainLoop starts with Present 4.
    /// </summary>
    [RetailFileFact("golden-640/level100.perpresent-3400.probes")]
    public void Level100PerPresentEventTimeFrameTimeAndLevelMatchEveryPresent()
    {
        // The clock run does not model the RNG probe; only the clock probes are compared.
        AssertEveryPresentMatches("golden-640/level100.perpresent-3400.probes",
            new ClockSimulation(level: 100, baseReadPresent: 2, levelStartPresent: 2));
    }

    /// <summary>
    /// C's per-Present retail run of level 100 with the old settings (golden/level100.perpresent-oldsettings-3400.probes:
    /// plain -level 100, so the intro FMV plays): the base read and PreRun after Present 3100, no loading Present, and
    /// MainLoop's first Present (3101) already at EVT 3.05.
    /// </summary>
    [RetailFileFact("golden/level100.perpresent-oldsettings-3400.probes")]
    public void Level100OldSettingsEventTimeFrameTimeAndLevelMatchEveryPresent() =>
        AssertEveryPresentMatches("golden/level100.perpresent-oldsettings-3400.probes",
            new ClockSimulation(level: 100, baseReadPresent: 3100, levelStartPresent: 3100, loadingScreenAfterPreRun: false));

    private static void AssertEveryPresentMatches(string golden, ClockSimulation sim)
    {
        ProbeTrace reference = ProbeTrace.Load(RetailData.File(golden));
        reference.RequireCoverage(1, 3400, 1, "EVT", "FT", "LVL");
        var ours = new ProbeTrace();
        while (sim.PresentCount < 3_400)
        {
            sim.AdvanceToNextPresent();
            sim.ReadProbes(sim.PresentCount, ours);
        }

        TraceComparison comparison = TraceComparison.Compare(reference, ours);
        foreach (string name in new[] { ProbeCatalog.EventTime, ProbeCatalog.FrameTime, ProbeCatalog.Level })
        {
            ProbeComparison probe = comparison.Probes.Single(p => p.Name == name);
            Assert.True(probe.Exact, $"{name}: {probe}");
            Assert.Equal(3_400, probe.ReferenceSamples);
        }
    }

    private static double PresentsPerTwoTicks(List<int> renderCounts) => 2.0 * renderCounts.Sum() / renderCounts.Count;

    /// <summary>num / den rounded once to a 24-bit significand, ties to even (positive values).</summary>
    private static float Round24(BigInteger num, BigInteger den)
    {
        if (num.IsZero)
        {
            return 0;
        }

        int e = (int)num.GetBitLength() - (int)den.GetBitLength() - 26;
        BigInteger q = e >= 0 ? BigInteger.DivRem(num, den << e, out BigInteger rem)
                              : BigInteger.DivRem(num << -e, den, out rem);
        int extra = (int)q.GetBitLength() - 24;
        BigInteger kept = q >> extra;
        BigInteger dropped = q - (kept << extra);
        BigInteger half = BigInteger.One << (extra - 1);
        if (dropped > half || (dropped == half && (!rem.IsZero || !kept.IsEven)))
        {
            kept += 1;
        }

        return (float)Math.ScaleB((double)kept, e + extra);
    }
}

/// <summary>A <see cref="RetailDataFactAttribute"/> that also needs one file of the retail data.</summary>
public sealed class RetailFileFactAttribute : FactAttribute
{
    public RetailFileFactAttribute(string relative)
    {
        if (RetailData.Root is null || !File.Exists(RetailData.File(relative)))
        {
            Skip = $"retail parity data file {relative} not found (set ONSLAUGHT_PARITY_DATA)";
        }
    }
}
