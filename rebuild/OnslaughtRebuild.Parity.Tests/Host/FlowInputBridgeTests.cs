// SPDX-License-Identifier: GPL-3.0-only
using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Host;

/// <summary>Authored host events and the real platform pump; no game files or Godot runtime.</summary>
public sealed class FlowInputBridgeTests
{
    private const int VirtualKey = 'A';
    private const int ScanCode = 30;

    private sealed class Rig
    {
        public PlatformInput Input { get; } = new(new PlatformWindow());
        public Queue<Action<PlatformInput>> Posted { get; } = new();
        public FlowInputBridge Bridge { get; }

        public Rig() => Bridge = new(Posted.Enqueue);

        public void Deliver()
        {
            while (Posted.TryDequeue(out Action<PlatformInput>? action)) action(Input);
        }

        public void Press()
        {
            Assert.True(Bridge.QueueKey(VirtualKey, ScanCode, true));
            Deliver();
            Input.Process(0);
            Assert.True(Input.KeyOn(ScanCode));
        }
    }

    [Fact]
    public void KeysStillWaitForTheWorkerAndPlatformPump()
    {
        var r = new Rig();
        Assert.True(r.Bridge.QueueKey(VirtualKey, ScanCode, true));
        Assert.Single(r.Posted);
        Assert.Equal(0, r.Input.Pending);
        Assert.False(r.Input.KeyOn(ScanCode));
        r.Deliver();
        Assert.Equal(1, r.Input.Pending);
        Assert.False(r.Input.KeyOn(ScanCode));
        r.Input.Process(0);
        Assert.True(r.Input.KeyOn(ScanCode));
        Assert.True(r.Bridge.QueueKey(VirtualKey, ScanCode, false));
        r.Deliver();
        r.Input.Process(1);
        Assert.False(r.Input.KeyOn(ScanCode));
    }

    [Fact]
    public void FocusLossClearsHeldKeysAndCachedEdgesOnTheWorker()
    {
        var r = new Rig();
        r.Press();
        Assert.True(r.Input.KeyOnceThisFrame(ScanCode));
        r.Bridge.SetFocused(false);
        Assert.True(r.Input.KeyOn(ScanCode)); // No cross-thread mutation.
        r.Deliver();
        Assert.False(r.Input.KeyOn(ScanCode));
        Assert.False(r.Input.KeyOnceThisFrame(ScanCode));
        Assert.False(r.Input.KeyPressed);
    }

    [Fact]
    public void AQueuedKeyCannotReplayThroughTheTrapAfterFocusReturns()
    {
        var r = new Rig();
        int trapped = 0;
        r.Input.KeyTrap = (_, _) => { trapped++; return true; };
        Assert.True(r.Bridge.QueueKey(VirtualKey, ScanCode, true));
        r.Bridge.SetFocused(false);
        r.Bridge.SetFocused(true);
        r.Deliver();
        Assert.Equal(0, r.Input.Pending);
        r.Input.Process(0);
        Assert.Equal(0, trapped);
        Assert.False(r.Input.KeyOn(ScanCode));
    }

    [Fact]
    public void UnfocusedKeysAreRejectedAndCannotLatchAfterFocusReturns()
    {
        var r = new Rig();
        r.Bridge.SetFocused(false);
        r.Deliver();
        Assert.False(r.Bridge.QueueKey(VirtualKey, ScanCode, true));
        Assert.False(r.Bridge.QueueKey(VirtualKey, ScanCode, false));
        Assert.Empty(r.Posted);
        r.Bridge.SetFocused(true);
        r.Deliver();
        r.Input.Process(0);
        Assert.False(r.Input.KeyOn(ScanCode));
        r.Press(); // New focused input still works.
    }

    [Fact]
    public void DuplicateFocusSignalsDoNotQueueDuplicateReleases()
    {
        var r = new Rig();
        r.Bridge.SetFocused(true);
        Assert.Empty(r.Posted);
        r.Bridge.SetFocused(false);
        r.Bridge.SetFocused(false);
        Assert.Single(r.Posted);
        r.Bridge.SetFocused(true);
        r.Bridge.SetFocused(true);
        Assert.Single(r.Posted);
    }

    [Fact]
    public void FocusLossAlsoDropsPendingMouseAndTransientButtonState()
    {
        var r = new Rig();
        Array.Fill(r.Input.Mouse.Held, true);
        foreach (MouseButton button in r.Input.Mouse.Buttons)
        {
            button.Down = true;
            button.Released = true;
        }
        r.Input.Mouse.Moved = true;
        r.Input.Mouse.Wheel = 1;
        r.Input.PostMouseMessage(MouseMessage.LeftDown, 1, 2, 0);
        r.Bridge.SetFocused(false);
        r.Deliver();
        Assert.Equal(0, r.Input.Pending);
        Assert.All(r.Input.Mouse.Held, Assert.False);
        Assert.All(r.Input.Mouse.Buttons, button =>
        {
            Assert.False(button.Down);
            Assert.False(button.Released);
        });
        Assert.False(r.Input.Mouse.Moved);
        Assert.Equal(0, r.Input.Mouse.Wheel);
    }
}
