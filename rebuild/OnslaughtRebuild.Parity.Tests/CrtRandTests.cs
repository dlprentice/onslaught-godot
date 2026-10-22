// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Crt;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class CrtRandTests
{
    [Fact]
    public void AnUnseededStreamGivesTheMicrosoftRuntimeSequence()
    {
        // The well-known first values of the Microsoft C runtime's rand() with its default state of 1.
        var r = new CrtRandStream();
        Assert.Equal([41, 18467, 6334, 26500, 19169], Enumerable.Range(0, 5).Select(_ => r.Rand()));
        Assert.Equal(5, r.Calls);
    }

    [Fact]
    public void SrandSetsTheStateAndValuesStayWithinRandMax()
    {
        var r = new CrtRandStream();
        r.Srand(0xdeadbeef);
        Assert.Equal(0xdeadbeefu, r.HoldRand);
        uint expected = unchecked((0xdeadbeefu * 214013u) + 2531011u);
        Assert.Equal((int)((expected >> 16) & 0x7fff), r.Rand());
        for (int i = 0; i < 100_000; i++)
        {
            Assert.InRange(r.Rand(), 0, CrtRandStream.RandMax);
        }
    }

    [Fact]
    public void EachThreadHasItsOwnStreamStartingAtOne()
    {
        var crt = new CrtRand();
        crt.MainThread.Srand(1234);
        crt.MainThread.Rand();
        CrtRandStream worker = crt.ForThread(7);

        Assert.Equal(1u, worker.HoldRand);
        Assert.Equal(41, worker.Rand());
        Assert.Same(worker, crt.ForThread(7));
        Assert.Same(crt.MainThread, crt.ForThread(CrtRand.MainThreadId));
    }
}
