// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Host;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests;

public sealed class HostFlowKeyboardTests
{
    // Authored key identities: these tests need no game data or recorded input.
    private const int FirstVirtualKey = 1;
    private const int FirstScanCode = 2;
    private const int SecondVirtualKey = 3;
    private const int SecondScanCode = 4;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusLossReleasesAProcessedPressWithHeldFocusAsCausalControl(bool loseFocus)
    {
        var input = new PlatformInput(new PlatformWindow());
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, replay: false, focused: true);
        Assert.True(keyboard.Observe(FirstVirtualKey, FirstScanCode, true));
        input.Process(0);
        Assert.True(input.KeyDown[FirstScanCode]);

        if (loseFocus) keyboard.SetFocused(false);
        input.Process(1);

        Assert.Equal(!loseFocus, input.KeyDown[FirstScanCode]);
    }

    [Fact]
    public void FocusLossReleasesAPressQueuedBeforeTheFirstInputPump()
    {
        var input = new PlatformInput(new PlatformWindow());
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, replay: false, focused: true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        keyboard.SetFocused(false);
        input.Process(0);

        Assert.False(input.KeyDown[FirstScanCode]);
        Assert.Equal(0, input.Pending);
    }

    [Fact]
    public void RegainingFocusRequiresAFreshPressAndIgnoresUnfocusedInput()
    {
        var input = new PlatformInput(new PlatformWindow());
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, replay: false, focused: true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        input.Process(0);
        keyboard.SetFocused(false);
        Assert.False(keyboard.Observe(SecondVirtualKey, SecondScanCode, true));
        input.Process(1);
        keyboard.SetFocused(true);
        input.Process(2);

        Assert.False(input.KeyDown[FirstScanCode]);
        Assert.False(input.KeyDown[SecondScanCode]);
        Assert.True(keyboard.Observe(FirstVirtualKey, FirstScanCode, true));
        input.Process(3);
        Assert.True(input.KeyDown[FirstScanCode]);
    }

    [Fact]
    public void AReleasedKeyIsNotReleasedAgainOnFocusLoss()
    {
        List<(int, int, bool)> posted = [];
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), false, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, false);
        keyboard.SetFocused(false);

        Assert.Equal(new[]
        {
            (FirstVirtualKey, FirstScanCode, true),
            (FirstVirtualKey, FirstScanCode, false),
        }, posted);
    }

    [Fact]
    public void DuplicatePressesAreForwardedButReleasedOnceInPressOrder()
    {
        List<(int, int, bool)> posted = [];
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), false, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        keyboard.Observe(SecondVirtualKey, SecondScanCode, true);
        keyboard.SetFocused(false);
        keyboard.SetFocused(false);

        Assert.Equal(new[]
        {
            (FirstVirtualKey, FirstScanCode, true),
            (FirstVirtualKey, FirstScanCode, true),
            (SecondVirtualKey, SecondScanCode, true),
            (FirstVirtualKey, FirstScanCode, false),
            (SecondVirtualKey, SecondScanCode, false),
        }, posted);
    }

    [Fact]
    public void ReplayExcludesNativeKeyAndFocusTransitions()
    {
        List<(int, int, bool)> posted = [];
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), true, true);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true));
        keyboard.SetFocused(false);
        keyboard.SetFocused(true);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, false));
        Assert.Empty(posted);
    }

    [Fact]
    public void InitiallyUnfocusedFlowWaitsForFocusBeforeForwardingKeys()
    {
        List<(int, int, bool)> posted = [];
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), false, false);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true));
        Assert.Empty(posted);
        keyboard.SetFocused(true);
        Assert.True(keyboard.Observe(FirstVirtualKey, FirstScanCode, true));
        Assert.Single(posted);
    }

    [Fact]
    public void StopDiscardsHeldKeysAndRefusesCallbacksAfterTheRunIsDisposed()
    {
        bool disposed = false;
        int posted = 0;
        var keyboard = new FlowKeyboardInput((_, _, _) =>
        {
            Assert.False(disposed);
            posted++;
        }, false, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true);
        keyboard.Stop();
        disposed = true;
        keyboard.SetFocused(false);
        keyboard.SetFocused(true);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, false));
        Assert.False(keyboard.Observe(SecondVirtualKey, SecondScanCode, true));
        keyboard.Stop();

        Assert.Equal(1, posted);
    }
}
