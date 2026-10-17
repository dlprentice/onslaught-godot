// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Platform;

public sealed class HostSuspensionTests
{
    [Fact]
    public void PendingDiscardDoesNotDispatchTrapsOrReplaceRetailFlush()
    {
        var input = new PlatformInput(new PlatformWindow());
        input.PostTapeKey(0x57, 0x11, true); input.Process(1);
        Assert.True(input.KeyOnceThisFrame(0x11));
        int trapped = 0;
        input.KeyTrap = (_, _) => { trapped++; return true; };
        input.PostTapeKey(0x20, 0x39, true);
        input.FlushInputBuffers();
        Assert.Equal(1, input.Pending); // Existing retail Flush semantics are deliberately unchanged.
        input.DiscardPendingMessages();
        input.Process(1);
        Assert.Equal(0, input.Pending);
        Assert.Equal(0, trapped);
        Assert.False(input.KeyOnceThisFrame(0x11));
        Assert.False(input.KeyOn(0x39));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DevicePendingDiscardDoesNotPollOrChangeCreation(bool created)
    {
        var input = new PlatformInput(new PlatformWindow());
        var device = new DirectInputMouse(input.Mouse, input.Window);
        if (created) device.Init();
        input.Mouse.X = 12; input.Mouse.Y = 34;
        device.AddMouseDelta(800, -900, 120, 7);
        device.DiscardPendingState();
        Assert.Equal(created, device.Created);
        Assert.Equal(12, input.Mouse.X);
        Assert.Equal(34, input.Mouse.Y);
        if (!created) device.Init();
        int x = input.Mouse.X, y = input.Mouse.Y;
        device.PollState();
        Assert.Equal(x, input.Mouse.X);
        Assert.Equal(y, input.Mouse.Y);
        Assert.Equal(0, input.Mouse.Wheel);
        Assert.All(input.Mouse.Held, value => Assert.False(value));
    }
}
