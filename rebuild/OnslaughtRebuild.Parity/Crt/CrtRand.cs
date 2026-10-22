// SPDX-License-Identifier: GPL-3.0-only
// The behaviour of the C runtime's rand and srand as retail links them (VC6 SP5 LIBCMT), measured from the pinned
// toolchain's rand.obj and tidtable.obj; no runtime code is copied.

namespace OnslaughtRebuild.Parity.Crt;

/// <summary>
/// One thread's C runtime random stream. LIBCMT keeps the state per thread (_tiddata +0x14, _holdrand): _initptd sets it
/// to 1 for every new thread, srand stores its argument, and rand computes
/// <c>holdrand = holdrand * 214013 + 2531011</c> and returns <c>(holdrand &gt;&gt; 16) &amp; 0x7fff</c>.
/// </summary>
public sealed class CrtRandStream
{
    /// <summary>RAND_MAX.</summary>
    public const int RandMax = 0x7fff;

    /// <summary>_holdrand: 1 until srand.</summary>
    public uint HoldRand { get; private set; } = 1;

    /// <summary>rand calls made on this stream (a count for localising divergences; retail keeps none).</summary>
    public long Calls { get; private set; }

    /// <summary>srand(seed).</summary>
    public void Srand(uint seed) => HoldRand = seed;

    /// <summary>A diagnostic observer called on each rand() (null in normal runs).</summary>
    public Action? Observer { get; set; }

    /// <summary>rand().</summary>
    public int Rand()
    {
        Calls++;
        Observer?.Invoke();
        HoldRand = unchecked((HoldRand * 214013u) + 2531011u);
        return (int)((HoldRand >> 16) & RandMax);
    }
}

/// <summary>
/// The C runtime's random state for the whole process: one <see cref="CrtRandStream"/> per thread, each starting at 1.
/// The main thread's stream is the one the game shares (front end, particles, sound triggers and simulation all call
/// rand on it); a thread the game starts gets a fresh stream.
/// </summary>
public sealed class CrtRand
{
    /// <summary>The id <see cref="MainThread"/> is registered under.</summary>
    public const int MainThreadId = 0;

    private readonly Dictionary<int, CrtRandStream> _threads = new() { [MainThreadId] = new CrtRandStream() };

    /// <summary>The main thread's stream.</summary>
    public CrtRandStream MainThread => _threads[MainThreadId];

    /// <summary>The stream of thread <paramref name="threadId"/>, created at 1 the first time it is asked for.</summary>
    public CrtRandStream ForThread(int threadId)
    {
        if (!_threads.TryGetValue(threadId, out CrtRandStream? stream))
        {
            stream = new CrtRandStream();
            _threads[threadId] = stream;
        }

        return stream;
    }
}
