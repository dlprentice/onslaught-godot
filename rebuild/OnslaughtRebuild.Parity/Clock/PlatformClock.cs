// SPDX-License-Identifier: GPL-3.0-only
// GetSysTimeFloat adapted from onslaught-decomp src/PCPlatform.cpp (CPCPlatform::GetSysTimeFloat, 005159e0), in Stuart's
// release. The virtual timer models the project's replay shim (local-data/link-runtime/20261015-f/shim/replay_shim.cpp,
// Present clock), which is our own tool.

using OnslaughtRebuild.Parity.Numerics;

namespace OnslaughtRebuild.Parity.Clock;

/// <summary>
/// The machine's clocks as the retail replay harness presents them to the game, and the one Present counter every part
/// of the parity host shares. The harness's shim drives every timer from the Present count: after the first Present,
/// QueryPerformanceCounter is <c>base + n * 333333</c> at a frequency of 10,000,000, and timeGetTime, GetTickCount and
/// GetSystemTimeAsFileTime advance with it. Before the first Present the timers are the real ones; this model holds
/// them at fixed values (a run's behaviour must not depend on them: the level-100 golden shows the game's first
/// GetSysTimeFloat call comes after the first Present). <see cref="Present"/> is the only place the count moves.
/// </summary>
public sealed class PlatformClock
{
    /// <summary>QueryPerformanceFrequency under Wine (the shim does not hook it).</summary>
    public const long QpcFrequency = 10_000_000;

    /// <summary>
    /// Virtual seconds per Present: the harness passes <c>BEA_REPLAY_DT=0.0333333</c> (tools/replayrun.py's default),
    /// which the shim reads with atof.
    /// </summary>
    public const double ReplayDt = 0.0333333;

    /// <summary>Calls to a hooked timer without a Present after which the shim lets virtual time move on by one step.</summary>
    public const int SpinEscapeCalls = 2_000_000;

    private readonly long _realQpc;
    private readonly uint _realMs;
    private readonly ulong _realFileTime;
    private int _spin;
    private bool _firstSeenDone;
    private long _firstSeen;

    /// <param name="realQpc">The real QueryPerformanceCounter value, before and at the first Present.</param>
    /// <param name="realMilliseconds">The real timeGetTime and GetTickCount value, before and at the first Present.</param>
    /// <param name="realFileTime">The real GetSystemTimeAsFileTime value, before and at the first Present.</param>
    public PlatformClock(long realQpc = 0, uint realMilliseconds = 0, ulong realFileTime = 0)
    {
        _realQpc = realQpc;
        _realMs = realMilliseconds;
        _realFileTime = realFileTime;
    }

    /// <summary>QPC ticks per Present: <c>(LONGLONG)((double)freq * dt)</c>, which is 333,333.</summary>
    public static long QpcTicksPerPresent { get; } = (long)(QpcFrequency * ReplayDt);

    /// <summary>Present calls returned since process start (the first is 1).</summary>
    public long PresentCount { get; private set; }

    /// <summary>Times the shim's spin escape has advanced virtual time without a Present.</summary>
    public long SpinEscapes { get; private set; }

    /// <summary>CPCPlatform::mClockDivisor (+0x10): 1.0f, set when the platform starts.</summary>
    public float ClockDivisor { get; set; } = 1.0f;

    /// <summary>The process's deferred display-reset request, independent of any one menu or level.</summary>
    public Platform.DisplayReset DisplayReset { get; } = new();

    /// <summary>CPlatform::Flip: DeviceFlip's Present and callbacks, then the deferred display reset.</summary>
    public void Present()
    {
        PresentDevice();
        DisplayReset.CompletePlatformFlip();
    }

    /// <summary>
    /// A raw device Present, including the first one during device creation: advance the replay clock and report its
    /// return. It does not execute CPlatform::Flip's reset tail. Normal game/loading/front-end flips use Present().
    /// </summary>
    public void PresentDevice()
    {
        FrameTimerFrame();
        PresentCount++;
        _spin = 0;
        Presented?.Invoke();
    }

    private long _frameTimerLast = -1;
    private long _frameTicks;

    /// <summary>
    /// CFrameTimer's mFramesPerSec (PLATFORM.GetFPS, 00515950): started at 1 when the platform starts (CPCPlatform::Init),
    /// then at each flip a quarter of the instant rate plus three quarters of the last, at least 1.
    /// </summary>
    public float FramesPerSec { get; private set; } = 1.0f;

    /// <summary>
    /// CFrameTimer::Frame (00423720), called from CPCPlatform::DeviceFlip before the flip: the counter's ticks since the
    /// last frame make the instant rate. (Every Present here: the films' and loading screen's flips are taken to pass
    /// through DeviceFlip too.)
    /// </summary>
    private void FrameTimerFrame()
    {
        long t = PresentCount == 0 ? _realQpc : _realQpc + ((PresentCount + SpinEscapes) * QpcTicksPerPresent);
        if (_frameTimerLast < 0)
        {
            // CFrameTimer::Start(1.0f): mLastTime is the counter when the platform starts.
            _frameTimerLast = t;
            return;
        }

        if (t != _frameTimerLast)
        {
            _frameTicks = t - _frameTimerLast;
            _frameTimerLast = t;
            float rate = (float)X87.Add(X87.Mul(X87.Div((float)QpcFrequency, (float)_frameTicks), 0.25f), X87.Mul(FramesPerSec, 0.75f));
            FramesPerSec = rate < 1.0f ? 1.0f : rate;
        }
    }

    /// <summary>Called as each Present returns (for checks that sample state at the Present itself).</summary>
    public Action? Presented { get; set; }

    /// <summary>
    /// The virtual time's steps without counting a timer call (the port's PortClockPeekNs and C's virtual DirectSound
    /// read it this way): the Presents plus the spin escapes.
    /// </summary>
    public long PeekSteps => PresentCount + SpinEscapes;

    public long QueryPerformanceCounter() =>
        PresentCount == 0 ? _realQpc : _realQpc + VirtualSteps() * QpcTicksPerPresent;

    public uint TimeGetTime() =>
        PresentCount == 0 ? _realMs : unchecked(_realMs + (uint)((double)VirtualSteps() * ReplayDt * 1000.0));

    public uint GetTickCount() => TimeGetTime();

    public ulong GetSystemTimeAsFileTime() =>
        PresentCount == 0 ? _realFileTime : _realFileTime + (ulong)((double)VirtualSteps() * ReplayDt * 10000000.0);

    /// <summary>
    /// CPCPlatform::GetSysTimeFloat: seconds since its first call. The compiled code loads the 64-bit tick difference
    /// and the frequency exactly (fild qword), multiplies the frequency by mClockDivisor (fmul dword) and divides
    /// (fdivp), at 24-bit precision control: two roundings, the product's and the quotient's. The result is returned in
    /// ST0; every caller uses it as a float.
    /// </summary>
    public float GetSysTimeFloat()
    {
        long ts = QueryPerformanceCounter();
        if (!_firstSeenDone)
        {
            _firstSeen = ts;
            _firstSeenDone = true;
        }

        double divisor = X87.Mul(ExactLoad(QpcFrequency), ClockDivisor);
        return (float)X87.Div(ExactLoad(ts - _firstSeen), divisor);
    }

    /// <summary>The shim's VPolls: every hooked timer call counts a spin; too many without a Present add a step.</summary>
    private long VirtualSteps()
    {
        if (++_spin > SpinEscapeCalls)
        {
            _spin = 0;
            SpinEscapes++;
        }

        return PresentCount + SpinEscapes;
    }

    /// <summary>fild qword: exact for the values this clock produces (|x| below 2^53).</summary>
    private static double ExactLoad(long x)
    {
        if (Math.Abs(x) >= 1L << 53)
        {
            throw new OverflowException($"{x} has more than 53 significant bits; fild would keep all 64.");
        }

        return x;
    }
}
