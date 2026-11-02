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

    private static void RejectCharacter(char character, int scanCode) =>
        Assert.Fail("A key without native text must not post a character.");

    private static FlowKeyboardInput WithText(PlatformInput input, bool replay = false, bool focused = true) =>
        new(input.PostTapeKey,
            (character, scanCode) => input.PostTapeMessage(new RetailCharacterEvent(0, character, scanCode)),
            replay, focused);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FocusLossReleasesAProcessedPressWithHeldFocusAsCausalControl(bool loseFocus)
    {
        var input = new PlatformInput(new PlatformWindow());
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, RejectCharacter, replay: false, focused: true);
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
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, RejectCharacter, replay: false, focused: true);
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
        var keyboard = new FlowKeyboardInput(input.PostTapeKey, RejectCharacter, replay: false, focused: true);
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
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), RejectCharacter, false, true);
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
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), RejectCharacter, false, true);
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
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), RejectCharacter, true, true);
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
        var keyboard = new FlowKeyboardInput((key, scan, down) => posted.Add((key, scan, down)), RejectCharacter, false, false);
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
        }, RejectCharacter, false, true);
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

    [Fact]
    public void NativeTextAndEchoFollowTheKeyPressWithoutRepeatingItsPhysicalTransition()
    {
        var input = new PlatformInput(new PlatformWindow());
        List<(int Value, int Kind, int CharScan)> events = [];
        input.KeyTrap = (value, kind) => { events.Add((value, kind, input.CharScanCode)); return false; };
        FlowKeyboardInput keyboard = WithText(input);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q');
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'Q', echo: true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, false, 'q');
        input.Process(0);

        Assert.Equal(new[]
        {
            (FirstScanCode, KeyTrapEvent.Down, 0),
            ((int)'q', KeyTrapEvent.Char, FirstScanCode),
            ((int)'Q', KeyTrapEvent.Char, FirstScanCode),
            (FirstScanCode, KeyTrapEvent.Up, 0),
        }, events);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(-1L)]
    [InlineData(0)]
    [InlineData((int)'\b')]
    [InlineData((int)'\r')]
    [InlineData(0x7f)]
    [InlineData(0x85)]
    [InlineData(0xd800)]
    [InlineData(0xdfff)]
    [InlineData(0x10000)]
    [InlineData(0x10ffff)]
    [InlineData(long.MaxValue)]
    public void ControlsSurrogatesAndOutOfRangeValuesDoNotBecomeText(long unicode)
    {
        var input = new PlatformInput(new PlatformWindow());
        List<int> kinds = [];
        input.KeyTrap = (_, kind) => { kinds.Add(kind); return false; };
        FlowKeyboardInput keyboard = WithText(input);
        Assert.True(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, unicode));
        input.Process(0);

        Assert.Equal(new[] { KeyTrapEvent.Down }, kinds);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ReplayAndUnfocusedInputCannotPostNativeCharacters(bool replay, bool focused)
    {
        var input = new PlatformInput(new PlatformWindow());
        FlowKeyboardInput keyboard = WithText(input, replay, focused);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q'));
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q', echo: true));

        Assert.Equal(0, input.Pending);
    }

    [Fact]
    public void EchoCannotResumeTextAfterFocusLossWithoutAFreshPhysicalPress()
    {
        var input = new PlatformInput(new PlatformWindow());
        int characters = 0;
        input.KeyTrap = (_, kind) => { if (kind == KeyTrapEvent.Char) characters++; return false; };
        FlowKeyboardInput keyboard = WithText(input);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q');
        keyboard.SetFocused(false);
        keyboard.SetFocused(true);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q', echo: true));
        input.Process(0);
        Assert.Equal(1, characters);

        Assert.True(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q'));
        input.Process(1);
        Assert.Equal(2, characters);
    }

    [Fact]
    public void StopAlsoRefusesNativeCharacterCallbacks()
    {
        bool disposed = false;
        int characters = 0;
        var keyboard = new FlowKeyboardInput((_, _, _) => Assert.False(disposed), (_, _) =>
        {
            Assert.False(disposed);
            characters++;
        }, false, true);
        keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q');
        keyboard.Stop();
        disposed = true;
        keyboard.SetFocused(true);
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q'));
        Assert.False(keyboard.Observe(FirstVirtualKey, FirstScanCode, true, 'q', echo: true));

        Assert.Equal(1, characters);
    }
}
