// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Platform;

public sealed class PlatformInputTests
{
    private const int Enter = 0x1C;

    private static PlatformInput New(int width = 640, int height = 480, bool frontEnd = true) =>
        new(new PlatformWindow { Width = width, Height = height, FrontEndRunning = frontEnd });

    [Fact]
    public void CeTapeCharacterIsTrappedInMessageOrderWithItsScanCode()
    {
        PlatformInput input = New();
        List<(int Key, int Event, int CharScan)> events = [];
        input.KeyTrap = (key, e) => { events.Add((key, e, input.CharScanCode)); return false; };
        input.PostTapeMessage(new RetailKeyEvent(1, true, 0x41, 0x11e, "a"));
        input.PostTapeMessage(new RetailCharacterEvent(1, 0x10041, 0x11e));
        input.PostTapeMessage(new RetailKeyEvent(1, false, 0x41, 0x11e, "a"));
        input.Process(1);
        Assert.Equal([(0x9e, KeyTrapEvent.Down, 0), (0x41, KeyTrapEvent.Char, 0x9e),
            (0x9e, KeyTrapEvent.Up, 0)], events);
        Assert.Equal(0, input.CharScanCode);
        Assert.False(input.KeyOn(0x9e));

        PlatformInput noTrap = New();
        noTrap.PostTapeMessage(new RetailCharacterEvent(1, 0x41, 0x1e));
        noTrap.Process(1);
        Assert.False(noTrap.KeyPressed);
        Assert.False(noTrap.KeyOn(0x1e));
    }

    [Fact]
    public void CeTapeWindowMessagesKeepTheRecordedCursorAndScaleForTheFrontEnd()
    {
        PlatformInput input = New(1280, 720);
        input.Mouse.SystemCursor = (7, 9);
        RetailTape tape = RetailTape.Parse(System.Text.Encoding.ASCII.GetBytes("# onslaught tape 2\n"));
        input.PrepareTape(tape);
        Assert.Equal((0, 0), input.Mouse.SystemCursor);
        input.PostTapeMessage(new RetailWindowMouseEvent(1, MouseMessage.Move, 640, 360, 0, 1280, 720));
        input.PostTapeMessage(new RetailWindowMouseEvent(1, MouseMessage.LeftDown, 640, 360, 1, 1280, 720));
        input.PostTapeMessage(new RetailWindowMouseEvent(1, MouseMessage.LeftUp, 640, 360, 0, 1280, 720));
        input.PostTapeMessage(new RetailWindowMouseEvent(1, MouseMessage.Wheel, 12, 20, 0xff880000, 1280, 720));
        Assert.Equal((640, 360), input.Mouse.SystemCursor); // wheel does not change the replay cursor
        input.PrepareTape(tape); // retry shares the process cursor instead of resetting it
        Assert.Equal((640, 360), input.Mouse.SystemCursor);
        input.Process(1);
        Assert.Equal((320, 240), (input.Mouse.X, input.Mouse.Y));
        Assert.Equal(-120, input.Mouse.Wheel);
        Assert.True(input.Mouse.ClickedInRect(0, 0, 640, 480, null));
        Assert.False(input.Mouse.ClickedInRect(0, 0, 640, 480, null));
    }

    [Fact]
    public void CeTapeRejectsAWindowSizeMismatchBeforePostingOrMovingTheCursor()
    {
        PlatformInput input = New(640, 480);
        input.Mouse.SystemCursor = (3, 4);
        Assert.Throws<InvalidOperationException>(() => input.PostTapeMessage(
            new RetailWindowMouseEvent(10, MouseMessage.Move, 10, 20, 0, 1280, 720)));
        Assert.Equal(0, input.Pending);
        Assert.Equal((3, 4), input.Mouse.SystemCursor);
        input.PostTapeMessage(new RetailWindowMouseEvent(10, MouseMessage.Move, 10, 20, 0, 0, 0));
        Assert.Equal((10, 20), input.Mouse.SystemCursor); // no recorded size: the port has no size guard
    }

    [Fact]
    public void LevelInputDoesNotDropWindowOrCharacterEvents()
    {
        var level = new OnslaughtRebuild.Parity.Game.LevelInput(new OnslaughtRebuild.Parity.Clock.PlatformClock(),
            ControlBindings.Defaults());
        List<(int, int)> trapped = [];
        level.Input.KeyTrap = (key, e) => { trapped.Add((key, e)); return false; };
        RetailTapeEvent[] events = [
            new RetailWindowMouseEvent(1, MouseMessage.Move, 123, 234, 0, 640, 480),
            new RetailCharacterEvent(1, 0x61, 0x1e),
        ];
        level.Deliver(events);
        Assert.Equal((123, 234), level.Input.Mouse.SystemCursor);
        level.Input.Process(1);
        Assert.Equal((123, 234), (level.Input.Mouse.X, level.Input.Mouse.Y));
        Assert.Equal([(0x61, KeyTrapEvent.Char)], trapped);
    }

    [Fact]
    public void TapeKeysAreHandledByScanCodeWhenThePlatformPumps()
    {
        PlatformInput input = New();
        input.PostTapeKey(0x0D, Enter, down: true);
        Assert.False(input.KeyOn(Enter));
        input.Process(333_333);
        Assert.True(input.KeyOn(Enter));
        Assert.True(input.KeyPressed);
        Assert.True(input.KeyOnce(Enter));
        Assert.False(input.KeyOnce(Enter));

        input.PostTapeKey(0x0D, Enter, down: false);
        input.Process(666_666);
        Assert.False(input.KeyOn(Enter));
        Assert.True(input.KeyUp(Enter));
        input.ClearKeyWasUp();
        Assert.False(input.KeyUp(Enter));
    }

    [Fact]
    public void AnExtendedKeyIsRecordedAbove0x80()
    {
        PlatformInput input = New();
        input.PostKeyMessage(0x26, 1 | (0x48 << 16) | 0x1000000, down: true);
        input.Process(1);
        Assert.True(input.KeyOn(0xC8));
        Assert.False(input.KeyOn(0x48));
    }

    [Fact]
    public void ADownAndUpInOneFrameLeaveThePressReadable()
    {
        PlatformInput input = New();
        input.PostTapeKey(0x0D, Enter, down: true);
        input.PostTapeKey(0x0D, Enter, down: false);
        input.Process(1);
        Assert.False(input.KeyOn(Enter));
        Assert.True(input.KeyOnce(Enter));
    }

    [Fact]
    public void AKeyReadOnceThisFrameStaysReadableUntilTimeMoves()
    {
        PlatformInput input = New();
        input.Process(0);
        input.PostTapeKey(0x0D, Enter, down: true);
        input.Process(333_333);
        Assert.True(input.KeyOnceThisFrame(Enter));
        Assert.True(input.KeyOnceThisFrame(Enter));
        input.Process(333_333);
        Assert.True(input.KeyOnceThisFrame(Enter));
        input.Process(666_666);
        Assert.False(input.KeyOnceThisFrame(Enter));
    }

    [Fact]
    public void AKeyTrapTakesPressesButKeyUpsStillRelease()
    {
        PlatformInput input = New();
        List<(int Key, int Event)> trapped = [];
        input.PostTapeKey(0x0D, Enter, down: true);
        input.Process(1);
        input.KeyTrap = (key, e) => { trapped.Add((key, e)); return true; };
        input.PostTapeKey(0x41, 0x1E, down: true);
        input.PostTapeKey(0x0D, Enter, down: false);
        input.Process(2);
        Assert.Equal([(0x1E, KeyTrapEvent.Down), (Enter, KeyTrapEvent.Up)], trapped);
        Assert.False(input.KeyOn(0x1E));
        Assert.False(input.KeyOn(Enter));
        Assert.True(input.KeyUp(Enter));
    }

    [Fact]
    public void FrontEndClicksAreScaledTo640By480AndConsumedOnce()
    {
        PlatformInput input = New(800, 600);
        input.PostMouseMessage(MouseMessage.Move, 400, 300, 0);
        input.PostMouseMessage(MouseMessage.LeftDown, 400, 300, 0x1);
        input.PostMouseMessage(MouseMessage.LeftUp, 400, 300, 0);
        input.Process(1);
        Mouse mouse = input.Mouse;
        Assert.Equal((320, 240), (mouse.X, mouse.Y));
        Assert.Equal((0.5f, 0.5f), (mouse.FracX, mouse.FracY));
        Assert.Equal((320, 240), (mouse.Buttons[0].X, mouse.Buttons[0].Y));
        Assert.True(mouse.Buttons[0].Down);
        Assert.True(mouse.Buttons[0].Released);
        Assert.False(mouse.Held[0]);

        int clicks = 0;
        Assert.True(mouse.DispatchClickInRect(0, 0, 640, 480, null, () => clicks++));
        Assert.False(mouse.ClickedInRect(0, 0, 640, 480, null));
        Assert.Equal(1, clicks);
        Assert.False(mouse.DispatchClickInRect(0, 0, 100, 100, null, () => clicks++));
        mouse.ResetTransientState();
        Assert.False(mouse.Buttons[0].Down);
        Assert.False(mouse.Moved);
    }

    [Fact]
    public void ARightClickOrAClickOutsideRaisesNothing()
    {
        PlatformInput input = New();
        input.PostMouseMessage(MouseMessage.RightDown, 10, 10, 0x2);
        input.PostMouseMessage(MouseMessage.RightUp, 10, 10, 0);
        input.PostMouseMessage(MouseMessage.Move, 600, 400, 0);
        input.PostMouseMessage(MouseMessage.LeftDown, 600, 400, 0x1);
        input.PostMouseMessage(MouseMessage.LeftUp, 600, 400, 0);
        input.Process(1);
        int clicks = 0;
        input.Mouse.DispatchClickInRect(0, 0, 100, 100, null, () => clicks++);
        Assert.Equal(0, clicks);
        Assert.True(input.Mouse.Buttons[2].Released);
    }

    [Fact]
    public void WithDirectInputDrivingTheCursorMovesOnlyRescaleIt()
    {
        PlatformInput input = New(1280, 960);
        DirectInputMouse device = new(input.Mouse, input.Window);
        device.Init();
        Assert.Equal((640, 480), (input.Mouse.X, input.Mouse.Y));
        input.PostMouseMessage(MouseMessage.Move, 10, 10, 0);
        input.Process(1);
        Assert.Equal((320, 240), (input.Mouse.X, input.Mouse.Y));
    }

    [Fact]
    public void TheWheelAddsTheMessagesSignedDelta()
    {
        PlatformInput input = New();
        input.PostMouseMessage(MouseMessage.Wheel, 0, 0, unchecked((int)0xFF880000));
        input.Process(1);
        Assert.Equal(-120, input.Mouse.Wheel);
    }

    [Fact]
    public void DirectInputDeltasAreConsumedOnceAndButtonsReleaseWhenLetGo()
    {
        PlatformInput input = New(frontEnd: false);
        DirectInputMouse device = new(input.Mouse, input.Window);
        device.Init();
        device.AddMouseDelta(5, -3, 0, 0);
        device.AddMouseDelta(2, 1, 120, 0x01);
        device.PollState();
        Assert.Equal((320 + 7, 240 - 2, 120), (input.Mouse.X, input.Mouse.Y, input.Mouse.Wheel));
        Assert.True(input.Mouse.Buttons[0].Down);
        Assert.True(input.Mouse.Held[0]);
        device.PollMotion();
        Assert.Equal((327, 238), (input.Mouse.X, input.Mouse.Y));

        device.AddMouseDelta(0, 0, 0, 0x06);
        device.PollState();
        Assert.False(input.Mouse.Buttons[0].Down);
        Assert.True(input.Mouse.Buttons[0].Released);
        Assert.True(input.Mouse.Held[2]);
        Assert.True(input.Mouse.Held[1]);
    }

    [Fact]
    public void WithoutTheDeviceThePollsDoNothing()
    {
        PlatformInput input = New();
        DirectInputMouse device = new(input.Mouse, input.Window);
        device.AddMouseDelta(5, 5, 0, 1);
        device.PollState();
        Assert.Equal((0, 0), (input.Mouse.X, input.Mouse.Y));
    }
}
