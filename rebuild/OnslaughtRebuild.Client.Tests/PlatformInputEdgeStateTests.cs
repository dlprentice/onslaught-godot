// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.TestSupport;

namespace OnslaughtRebuild.Client.Tests;

public sealed class PlatformInputEdgeStateTests
{
    [Fact]
    public void HeldKeyReadTracksPressAndRelease()
    {
        var state = new PlatformInputEdgeState();

        state.ObserveKey(0x51, pressed: true, echo: false);
        Assert.Equal(1, state.GetHeldKey(0x51));

        state.ObserveKey(0x51, pressed: false, echo: false);
        Assert.Equal(0, state.GetHeldKey(0x51));
    }

    [Fact]
    public void ConsumeKeyOnceReturnsTheLatchedByteAndClearsOnlyThatKey()
    {
        var state = new PlatformInputEdgeState();
        state.ObserveKey(0x51, pressed: true, echo: false);
        state.ObserveKey(0x52, pressed: true, echo: false);

        Assert.Equal(1, state.ConsumeKeyOnce(0x51));
        Assert.Equal(0, state.ConsumeKeyOnce(0x51));
        Assert.Equal(1, state.ConsumeKeyOnce(0x52));
        Assert.Equal(1, state.GetHeldKey(0x51));
    }

    [Fact]
    public void JoyButtonRisingEdgeComparesPreviousAndCurrentFrameBytes()
    {
        var state = new PlatformInputEdgeState();

        state.ObserveJoyButton(joypad: 2, button: 7, value: 0x80);

        Assert.Equal(0, state.GetPreviousJoyButton(2, 7));
        Assert.Equal(0x80, state.GetCurrentJoyButton(2, 7));
        Assert.True(state.IsJoyButtonRising(2, 7));

        state.AdvanceFrame();

        Assert.Equal(0x80, state.GetPreviousJoyButton(2, 7));
        Assert.False(state.IsJoyButtonRising(2, 7));
        Assert.Equal(1, state.FrameIndex);
    }

    [Fact]
    public void JoyButtonHeldReadsTheCurrentByteAcrossFrames()
    {
        var state = new PlatformInputEdgeState();
        state.ObserveJoyButton(joypad: 0, button: 3, value: 0x80);

        state.AdvanceFrame();

        Assert.True(state.IsJoyButtonHeld(0, 3));
        Assert.Equal(0x80, state.GetCurrentJoyButton(0, 3));
    }

    [Fact]
    public void JoyButtonFallingEdgeRequiresAPreviouslyHeldByte()
    {
        var state = new PlatformInputEdgeState();
        state.ObserveJoyButton(joypad: 1, button: 4, value: 0x80);
        state.AdvanceFrame();

        state.ObserveJoyButton(joypad: 1, button: 4, value: 0);

        Assert.True(state.IsJoyButtonFalling(1, 4));
        Assert.False(state.IsJoyButtonHeld(1, 4));

        state.AdvanceFrame();
        Assert.False(state.IsJoyButtonFalling(1, 4));
    }

    [Fact]
    public void ResetClearsEveryByteAtAnExplicitGenerationBoundary()
    {
        var state = new PlatformInputEdgeState();
        state.ObserveKey(0x51, pressed: true, echo: false);
        state.ObserveJoyButton(joypad: 0, button: 1, value: 0x80);
        state.AdvanceFrame();
        state.ObserveJoyButton(joypad: 0, button: 1, value: 0);

        state.Reset();

        Assert.Equal(0, state.GetHeldKey(0x51));
        Assert.Equal(0, state.ConsumeKeyOnce(0x51));
        Assert.Equal(0, state.GetPreviousJoyButton(0, 1));
        Assert.Equal(0, state.GetCurrentJoyButton(0, 1));
        Assert.Equal(1, state.FrameIndex);
        Assert.Equal(1, state.ResetGeneration);
    }

    [Fact]
    public void KeyEchoDoesNotCreateAHeldOrConsumeOnceByte()
    {
        var state = new PlatformInputEdgeState();

        state.ObserveKey(0x51, pressed: true, echo: true);

        Assert.Equal(0, state.GetHeldKey(0x51));
        Assert.Equal(0, state.ConsumeKeyOnce(0x51));
    }

    [Fact]
    public void DistinctNonEchoPressEventsCanEachLatchOnce()
    {
        var state = new PlatformInputEdgeState();
        state.ObserveKey(0x51, pressed: true, echo: false);
        Assert.Equal(1, state.ConsumeKeyOnce(0x51));

        state.ObserveKey(0x51, pressed: true, echo: false);

        Assert.Equal(1, state.ConsumeKeyOnce(0x51));
    }

    [Fact]
    public void InteractiveSessionConsumesAKeyOnceThroughItsExistingQueue()
    {
        var session = new InteractiveSession(
            0x4F4E534Cu,
            Level100TestActorDefinitions.Create());
        const int toggleKey = 0x51;
        session.PlatformInput.ObserveKey(toggleKey, pressed: true, echo: false);

        if (session.PlatformInput.ConsumeKeyOnce(toggleKey) != 0)
        {
            session.QueueToggleMode();
        }
        session.AdvanceFrameTicks(500_000);

        Assert.Equal(1, session.Metrics.ToggleEdgesConsumed);
        Assert.Equal(1, session.PlatformInput.FrameIndex);

        session.ReleaseAllInput();
        Assert.Equal(1, session.PlatformInput.ResetGeneration);
        Assert.Equal(0, session.PlatformInput.GetHeldKey(toggleKey));
    }

    [Fact]
    public void CaptureOrdersEquivalentStateIndependentlyOfEventInsertionOrder()
    {
        var first = new PlatformInputEdgeState();
        first.ObserveKey(9, pressed: true, echo: false);
        first.ObserveKey(2, pressed: true, echo: false);
        first.ObserveJoyButton(joypad: 3, button: 8, value: 0x80);
        first.ObserveJoyButton(joypad: 0, button: 4, value: 0x80);

        var second = new PlatformInputEdgeState();
        second.ObserveJoyButton(joypad: 0, button: 4, value: 0x80);
        second.ObserveJoyButton(joypad: 3, button: 8, value: 0x80);
        second.ObserveKey(2, pressed: true, echo: false);
        second.ObserveKey(9, pressed: true, echo: false);

        PlatformInputEdgeSnapshot firstSnapshot = first.Capture();
        PlatformInputEdgeSnapshot secondSnapshot = second.Capture();

        Assert.Equal(firstSnapshot.HeldKeys, secondSnapshot.HeldKeys);
        Assert.Equal(firstSnapshot.ConsumeOnceKeys, secondSnapshot.ConsumeOnceKeys);
        Assert.Equal(firstSnapshot.CurrentJoyButtons, secondSnapshot.CurrentJoyButtons);
    }

    [Fact]
    public void GodotEventsFeedTheStateBeforeExistingSessionQueuesConsumeThem()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "FirstFlightGame.cs"));
        int inputStart = source.IndexOf(
            "public override void _Input(InputEvent inputEvent)",
            StringComparison.Ordinal);
        int inputEnd = source.IndexOf(
            "private bool HandleAuthenticPauseInput",
            inputStart,
            StringComparison.Ordinal);
        string input = source[inputStart..inputEnd];

        AssertOccursInOrder(
            input,
            "ObservePlatformInputEvent(inputEvent);",
            "_session.PlatformInput.ConsumeKeyOnce",
            "_session.QueueMovementPulse",
            "_session.QueueToggleMode");
        Assert.Contains("_session.PlatformInput.IsJoyButtonRising", input, StringComparison.Ordinal);
        Assert.Contains("_session.PlatformInput.ObserveKey", source, StringComparison.Ordinal);
        Assert.Contains("_session.PlatformInput.ObserveJoyButton", source, StringComparison.Ordinal);
    }

    private static void AssertOccursInOrder(string text, params string[] values)
    {
        int cursor = 0;
        foreach (string value in values)
        {
            int index = text.IndexOf(value, cursor, StringComparison.Ordinal);
            Assert.True(index >= cursor, $"Expected '{value}' at or after offset {cursor}.");
            cursor = index + value.Length;
        }
    }
}
