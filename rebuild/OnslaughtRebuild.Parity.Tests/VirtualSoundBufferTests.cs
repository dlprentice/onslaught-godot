// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Script;
using OnslaughtRebuild.Parity.Sound;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class VirtualSoundBufferTests
{
    private static SampleBufferFormat Format(uint bytes, int rate = 0)
    { Assert.True(SampleBufferFormat.TryCreate(bytes, rate, out var f)); return f; }

    [Fact]
    public void ExactEndAtLastFrameRemainsPlayingUntilNextBlockAndThenResetsCursor()
    {
        long block = 0;
        var b = new VirtualSoundBuffer(Format(882), () => block);
        b.Play(); block = 1;
        Assert.Equal(1u, b.GetStatus()); Assert.Equal(882u, b.GetCurrentPosition().Play);
        block = 2;
        Assert.Equal(0u, b.GetStatus()); Assert.Equal(0u, b.GetCurrentPosition().Play);
        b.Play(); Assert.Equal(1u, b.GetStatus());
    }

    [Fact]
    public void RealIncomingSampleEndsAtEightWholeBlocksWithoutChangingGameEventState()
    {
        long ns = 0;
        var b = new VirtualSoundBuffer(Format(6944, 1), () => ns * 44100 / 1_000_000_000 / 441);
        b.Play(); ns = 79_999_999;
        Assert.Equal(1u, b.GetStatus()); Assert.Equal(3086u, b.GetCurrentPosition().Play);
        ns = 80_000_000;
        Assert.Equal(0u, b.GetStatus()); Assert.Equal(0u, b.GetCurrentPosition().Play);
    }

    [Fact]
    public void PauseRetainsFractionalCursorAndResumeDoesNotConsumePausedBlocks()
    {
        long block = 0;
        var b = new VirtualSoundBuffer(Format(8000, 1), () => block);
        b.Play(); block = 1; b.Stop();
        Assert.Equal(440u, b.GetCurrentPosition().Play); // 220.5 source frames retained internally
        block = 100; Assert.Equal(0u, b.GetStatus()); b.Play();
        block = 101; Assert.Equal(882u, b.GetCurrentPosition().Play);
    }

    [Fact]
    public void FrequencyChangeAndInvalidRequestCatchUpAtPreviousFrequency()
    {
        long block = 0;
        var b = new VirtualSoundBuffer(Format(16000, 1), () => block);
        b.Play(); block = 1;
        Assert.True(b.TrySetFrequency(44100)); Assert.Equal(440u, b.GetCurrentPosition().Play);
        block = 2;
        Assert.False(b.TrySetFrequency(99)); Assert.Equal(44100u, b.Frequency);
        Assert.Equal(1322u, b.GetCurrentPosition().Play);
        Assert.True(b.TrySetFrequency(0)); Assert.Equal(22050u, b.Frequency);
    }

    [Fact]
    public void LoopingWrapsAndChangingPlayFlagsDoesNotRestartCursor()
    {
        long block = 0;
        var b = new VirtualSoundBuffer(Format(1000), () => block);
        b.Play(true); block = 2;
        Assert.Equal(5u, b.GetStatus()); Assert.Equal(764u, b.GetCurrentPosition().Play);
        b.Play(false); Assert.Equal(764u, b.GetCurrentPosition().Play);
        block = 3; Assert.Equal(0u, b.GetStatus());
    }

    [Fact]
    public void PositionWrapsAndTruncatesToFrameAndWriteCursorUsesNativeIntegerOrder()
    {
        long block = 0;
        var b = new VirtualSoundBuffer(Format(1000, 1), () => block);
        b.SetCurrentPosition(503);
        Assert.Equal((2u, 442u), b.GetCurrentPosition());
        b.SetCurrentPosition(500); Assert.Equal(0u, b.GetCurrentPosition().Play);
    }

    [Fact]
    public void ExistingVirtualVoiceClockSuppliesBlocksWithoutAddingPlatformReads()
    {
        var clock = new PlatformClock();
        var voice = new VirtualVoice(clock, _ => throw new InvalidOperationException("No sample read expected."));
        var b = new VirtualSoundBuffer(Format(16000, 1), voice.BlocksMixed);
        clock.Present(); b.Play();
        clock.Present(); Assert.Equal(1322u, b.GetCurrentPosition().Play); // 3 blocks, 661.5 source frames
        clock.Present(); Assert.Equal(2646u, b.GetCurrentPosition().Play); // 6 blocks total
        for (int i = 0; i < 1000; i++) b.GetStatus();
        Assert.Equal(0, clock.SpinEscapes);
    }

    [Fact]
    public void BackwardsClockFailsRatherThanInventingElapsedPlayback()
    {
        long block = 1;
        var b = new VirtualSoundBuffer(Format(1000), () => block);
        b.Play(); block = 0;
        Assert.Throws<InvalidOperationException>(() => b.GetStatus());
    }

    // Reuse the existing private oracle reader. This group is produced by the ACTUAL port dsound.cpp, not retail
    // instructions: zero-valued authored buffers, virtual time, no device, no 3D/Doppler or admission competition.
    [RetailOracleFact("virtual-sound-backend")]
    public void PublicStatusCursorsAndFrequencyMatchActualVirtualMixer()
    {
        var actual = new List<string>();
        foreach ((string function, uint[] w, string expected) in RetailOracle.Cases("virtual-sound-backend"))
        {
            Assert.Equal("Buffer", function);
            Assert.Equal(3 + checked((int)w[2]) * 3, w.Length);
            long ns = 0;
            var b = new VirtualSoundBuffer(Format(w[0], unchecked((int)w[1])), () => ns * 44100 / 1_000_000_000 / 441);
            var fields = new List<uint> { b.Format.BufferBytes };
            for (int i = 3; i < w.Length; i += 3)
            {
                ns = w[i]; uint success = 1;
                switch (w[i + 1])
                {
                    case 0: b.Play(w[i + 2] != 0); break;
                    case 1: b.Stop(); break;
                    case 2: b.SetCurrentPosition(w[i + 2]); break;
                    case 3: success = b.TrySetFrequency(w[i + 2]) ? 1u : 0u; break;
                    case 4: break;
                    default: throw new InvalidDataException("Unknown backend probe operation.");
                }
                uint status = b.GetStatus(); var position = b.GetCurrentPosition();
                fields.AddRange([success, status, position.Play, position.Write, b.Frequency]);
            }
            string line = "Buffer " + string.Join(" ", fields.Select(v => v.ToString("x8")));
            Assert.True(line == expected, $"case {actual.Count}:\nours: {line}\nbackend: {expected}"); actual.Add(line);
        }
        Assert.Equal(512, actual.Count);
        RetailOracle.WriteOurs("virtual-sound-backend", actual.ToArray());
    }
}
