// SPDX-License-Identifier: GPL-3.0-or-later

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
    public void TheEventClockStoresCountTimesTheTick()
    {
        var events = new EventClock();
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
        var sim = new ClockSimulation(level: 100, baseReadPresent: 100, levelStartPresent: 100, preRunTicks: 60);
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
        Assert.All(renderCounts.Skip(1), r => Assert.InRange(r, 1, 2));
        Assert.Equal(3.0, PresentsPerTwoTicks(renderCounts.Skip(200).Take(6000).ToList()));
        // The pre-run's 60 ticks, then 2 ticks per 3 Presents over the 9,900 Presents after the level start: with
        // mBaseTime read at the first MainLoop's Present there is nothing to catch up.
        Assert.Equal(60 + 9_900 * 2 / 3, sim.Events.FrameCount);
    }

    [Fact]
    public void FrameTimeIsTheClockAtTheLastRenderBeforeEachPresent()
    {
        var sim = new ClockSimulation(level: 100, baseReadPresent: 3100, levelStartPresent: 3100);
        for (long k = 1; k <= 10_000; k++)
        {
            sim.AdvanceToNextPresent();
            if (k > 3100)
            {
                // GetSysTimeFloat counts from Present 1, and FT is read at the render before Present k.
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
        var sim = new ClockSimulation(level: 100, baseReadPresent: 3100, levelStartPresent: 3100, preRunTicks: 60);
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
