// SPDX-License-Identifier: GPL-3.0-only

using Godot;
using OnslaughtRebuild.Parity.Platform;
using Button = Godot.MouseButton;

namespace OnslaughtRebuild.Host.Checks;

/// <summary>
/// Focused adapter checks, callable without a Godot window/native InputEvent. These exercise the exact value-taking
/// production entry points used by Observe. OS event delivery and physical capture require a separate offscreen run.
/// </summary>
public static class HostInputChecks
{
    public static int Run()
    {
        int cases = 0;
        void Case(Action test) { test(); cases++; }
        Case(PhysicalKeysHavePcCodes);
        Case(ExtendedAndSidedKeysStayDistinct);
        Case(KeysWaitForThePlatformPump);
        Case(ShortPressAndEchoReachThePlatform);
        Case(FractionalMotionAccumulatesAcrossDrains);
        Case(MotionInOneDrainSumsBeforeTruncation);
        Case(DeviceButtonsUseTheTapeBits);
        Case(WindowButtonsUseWindowMessages);
        Case(WheelUsesSigned120UnitsAndPreservesFractions);
        Case(UnfocusedInputDoesNotAccumulate);
        Case(LimitedPauseAllowsOnlyBackAndReleases);
        Case(() => FocusLossDiscardsAllState(created: true));
        Case(() => FocusLossDiscardsAllState(created: false));
        Case(FocusLossCannotReplayKeyTrapMessages);
        Case(FocusLossDropsOncePerFrameCache);
        Case(DeviceReacquisitionDropsOldFractionalMotion);
        Case(DeviceReacquisitionDropsUnpolledPacketAndWheelFraction);
        Case(ExplicitPauseBoundaryDropsSameBatchMenuKeysAndRecreatedMouse);
        Case(UnsupportedAndNonfiniteInputAreNotAdmitted);
        return cases;
    }

    private sealed class Rig
    {
        public PlatformInput Platform { get; } = new(new PlatformWindow());
        public DirectInputMouse Device { get; }
        public HostInputAdapter Adapter { get; }
        public Rig(bool created = true)
        {
            Device = new(Platform.Mouse, Platform.Window);
            if (created) Device.Init();
            Adapter = new(Platform, Device);
            Adapter.SetFocused(true);
        }
        public void Pump(long time = 1) => Platform.Process(time);
    }

    private static void PhysicalKeysHavePcCodes()
    {
        (Key Key, int Vk, int Scan)[] expected =
        [
            (Key.W, 0x57, 0x11), (Key.A, 0x41, 0x1e), (Key.S, 0x53, 0x1f), (Key.D, 0x44, 0x20),
            (Key.Tab, 0x09, 0x0f), (Key.Space, 0x20, 0x39), (Key.Escape, 0x1b, 0x01),
            (Key.Enter, 0x0d, 0x1c), (Key.Backspace, 0x08, 0x0e), (Key.Key0, 0x30, 0x0b),
            (Key.F12, 0x7b, 0x58), (Key.Semicolon, 0xba, 0x27), (Key.Apostrophe, 0xde, 0x28),
        ];
        foreach ((Key key, int vk, int scan) in expected)
        {
            Check(HostKeyMap.TryMap(key, KeyLocation.Unspecified, out var mapped), $"map {key}");
            Equal(new HostKeyMap.PcKey(vk, scan), mapped, $"PC key {key}");
        }
        Check(!HostKeyMap.TryMap(Key.None, KeyLocation.Unspecified, out _), "missing physical key rejected");
    }

    private static void ExtendedAndSidedKeysStayDistinct()
    {
        (Key Key, KeyLocation Location, int Scan)[] expected =
        [
            (Key.Up, KeyLocation.Unspecified, 0xc8), (Key.Kp8, KeyLocation.Unspecified, 0x48),
            (Key.KpEnter, KeyLocation.Unspecified, 0x9c), (Key.Enter, KeyLocation.Unspecified, 0x1c),
            (Key.Ctrl, KeyLocation.Left, 0x1d), (Key.Ctrl, KeyLocation.Right, 0x9d),
            (Key.Shift, KeyLocation.Left, 0x2a), (Key.Shift, KeyLocation.Right, 0x36),
            (Key.Alt, KeyLocation.Left, 0x38), (Key.Alt, KeyLocation.Right, 0xb8),
        ];
        var r = new Rig();
        foreach (var e in expected)
        {
            r.Adapter.QueueKey(e.Key, e.Location, true);
            r.Adapter.DrainBeforePresent(); r.Pump();
            Check(r.Platform.KeyOn(e.Scan), $"held scan {e.Scan:x}");
            r.Adapter.ReleaseAll();
        }
    }

    private static void KeysWaitForThePlatformPump()
    {
        var r = new Rig();
        r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, true);
        Equal(0, r.Platform.Pending, "host owns undrained key");
        r.Adapter.DrainBeforePresent();
        Equal(1, r.Platform.Pending, "posted once");
        Check(!r.Platform.KeyOn(0x11), "not pumped by adapter");
        r.Pump(); Check(r.Platform.KeyOn(0x11), "held after pump");
        r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, false);
        r.Adapter.DrainBeforePresent(); r.Pump(2);
        Check(!r.Platform.KeyOn(0x11) && r.Platform.KeyUp(0x11), "release");
    }

    private static void ShortPressAndEchoReachThePlatform()
    {
        var r = new Rig();
        r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, true);
        r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, false);
        r.Adapter.DrainBeforePresent(); r.Pump();
        Check(!r.Platform.KeyOn(0x39) && r.Platform.KeyOnce(0x39), "short press remains readable");
        r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, true);
        r.Adapter.DrainBeforePresent(); r.Pump(2); Check(r.Platform.KeyOnce(0x39), "first down");
        r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, true);
        r.Adapter.DrainBeforePresent(); r.Pump(3); Check(r.Platform.KeyOnce(0x39), "repeat follows PC key-down semantics");
    }

    private static void FractionalMotionAccumulatesAcrossDrains()
    {
        var r = new Rig();
        for (int i = 0; i < 4; i++)
        {
            r.Adapter.QueueMotion(0.25, -0.25, 10, 20);
            r.Adapter.DrainBeforePresent(); r.Device.PollState();
        }
        Equal(321, r.Platform.Mouse.X, "positive fractional sum");
        Equal(239, r.Platform.Mouse.Y, "negative fractional sum");
        r.Device.PollState(); Equal(321, r.Platform.Mouse.X, "not delivered twice");
    }

    private static void MotionInOneDrainSumsBeforeTruncation()
    {
        var r = new Rig();
        r.Adapter.QueueMotion(1.75, -1.75, 1, 2);
        r.Adapter.QueueMotion(-0.25, 0.25, 5, 6);
        r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Equal(321, r.Platform.Mouse.X, "sum before truncation x");
        Equal(239, r.Platform.Mouse.Y, "sum before truncation y");
        Equal(0, r.Platform.Pending, "device motion is not also a window message");
        r.Adapter.QueueMotion(0.5, -0.5, 0, 0);
        r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Equal(322, r.Platform.Mouse.X, "remainder carried x");
        Equal(238, r.Platform.Mouse.Y, "remainder carried y");
    }

    private static void DeviceButtonsUseTheTapeBits()
    {
        var r = new Rig();
        r.Adapter.QueueButton(Button.Right, true, 0, 0);
        r.Adapter.QueueButton(Button.Middle, true, 0, 0);
        r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Check(!r.Platform.Mouse.Held[0] && r.Platform.Mouse.Held[1] && r.Platform.Mouse.Held[2], "right/middle order");
        r.Adapter.QueueButton(Button.Right, false, 0, 0);
        r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Check(!r.Platform.Mouse.Held[2] && r.Platform.Mouse.Buttons[2].Released, "right release");
        Check(r.Platform.Mouse.Held[1], "middle persists");
    }

    private static void WindowButtonsUseWindowMessages()
    {
        var r = new Rig(false);
        r.Adapter.QueueButton(Button.Middle, true, 24, 35);
        r.Adapter.QueueMotion(99, 88, 40, 50);
        r.Adapter.DrainBeforePresent(); r.Pump();
        Equal(40, r.Platform.Mouse.X, "window absolute x"); Equal(50, r.Platform.Mouse.Y, "window absolute y");
        Check(r.Platform.Mouse.Held[1], "WM middle bit survives move");
        Equal(24, r.Platform.Mouse.Buttons[1].X, "press location");
        r.Adapter.QueueButton(Button.Middle, false, 40, 50);
        r.Adapter.DrainBeforePresent(); r.Pump(2);
        Check(!r.Platform.Mouse.Held[1] && r.Platform.Mouse.Buttons[1].Released, "window release");
    }

    private static void WheelUsesSigned120UnitsAndPreservesFractions()
    {
        foreach (bool created in new[] { false, true })
        {
            var r = new Rig(created);
            r.Adapter.QueueButton(Button.WheelUp, true, 0, 0, 0);
            r.Adapter.QueueButton(Button.WheelDown, true, 0, 0, 0.5);
            r.Adapter.DrainBeforePresent(); r.Pump(); r.Device.PollState();
            Equal(60, r.Platform.Mouse.Wheel, "notches and direction");
            r.Platform.Mouse.ResetTransientState();
            for (int i = 0; i < 4; i++)
            {
                r.Adapter.QueueButton(Button.WheelUp, true, 0, 0, 1.0 / 480);
                r.Adapter.DrainBeforePresent(); r.Pump(i + 2); r.Device.PollState();
            }
            Equal(1, r.Platform.Mouse.Wheel, "fractional wheel counts");
        }
    }

    private static void UnfocusedInputDoesNotAccumulate()
    {
        var r = new Rig(); r.Adapter.SetFocused(false);
        Check(!r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, true), "unfocused key");
        Check(!r.Adapter.QueueMotion(100, 20, 0, 0), "unfocused motion");
        Check(!r.Adapter.QueueButton(Button.Left, true, 0, 0), "unfocused button");
        r.Adapter.SetFocused(true); r.Adapter.DrainBeforePresent(); r.Device.PollState(); r.Pump();
        Equal(320, r.Platform.Mouse.X, "no motion after refocus");
        Check(!r.Platform.KeyOn(0x11) && !r.Platform.Mouse.Held[0], "no replayed press");
    }

    private static void LimitedPauseAllowsOnlyBackAndReleases()
    {
        var r = new Rig();
        r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, true); r.Adapter.DrainBeforePresent(); r.Pump();
        Check(!r.Adapter.QueueKey(Key.Enter, KeyLocation.Unspecified, true, false), "hidden select rejected");
        Check(!r.Adapter.QueueKey(Key.Down, KeyLocation.Unspecified, true, false), "hidden navigation rejected");
        Check(!r.Adapter.QueueButton(Button.Left, true, 0, 0, menuNavigationEnabled: false), "hidden click rejected");
        Check(!r.Adapter.QueueMotion(1, 2, 0, 0, false), "hidden motion rejected");
        Check(r.Adapter.QueueKey(Key.Escape, KeyLocation.Unspecified, true, false), "Escape resume");
        Check(r.Adapter.QueueKey(Key.Backspace, KeyLocation.Unspecified, true, false), "Backspace resume");
        Check(r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, false, false), "held release accepted");
        Check(r.Adapter.QueueButton(Button.Left, false, 0, 0, menuNavigationEnabled: false), "mouse release accepted");
        r.Adapter.DrainBeforePresent(); r.Pump(2);
        Check(!r.Platform.KeyOn(0x11) && r.Platform.KeyOnce(0x01), "released key and resume");
    }

    private static void FocusLossDiscardsAllState(bool created)
    {
        var r = new Rig(created);
        r.Adapter.QueueKey(Key.W, KeyLocation.Unspecified, true); r.Adapter.DrainBeforePresent(); r.Pump();
        r.Platform.Mouse.Held[0] = true;
        r.Adapter.QueueMotion(0.75, 0.25, 1, 2); r.Adapter.DrainBeforePresent();
        r.Device.AddMouseDelta(900, -700, 120, 7); // Pending even when the core's device is not created.
        r.Platform.PostTapeKey(0x20, 0x39, true);
        r.Adapter.QueueButton(Button.Left, true, 0, 0);
        r.Adapter.SetFocused(false);
        Equal(created, r.Device.Created, "focus does not change device lifetime");
        Equal(0, r.Adapter.PendingEvents, "host pending cleared"); Equal(0, r.Platform.Pending, "platform pending cleared");
        Check(!r.Platform.KeyOn(0x11) && !r.Platform.Mouse.Held.Any(v => v), "held state cleared");
        r.Adapter.SetFocused(true);
        if (!created) r.Device.Init();
        r.Adapter.QueueMotion(0.25, 0.25, 0, 0); r.Adapter.DrainBeforePresent(); r.Device.PollState(); r.Pump(2);
        Equal(320, r.Platform.Mouse.X, "queued and fractional motion discarded");
        Equal(240, r.Platform.Mouse.Y, "pending y discarded");
        Equal(0, r.Platform.Mouse.Wheel, "pending wheel discarded");
        Check(!r.Platform.KeyOn(0x39) && !r.Platform.Mouse.Held.Any(v => v), "pending buttons/keys discarded");
    }

    private static void FocusLossCannotReplayKeyTrapMessages()
    {
        var r = new Rig(); int trapped = 0;
        r.Platform.KeyTrap = (_, _) => { trapped++; return true; };
        r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, true); r.Adapter.DrainBeforePresent();
        r.Adapter.SetFocused(false); r.Adapter.SetFocused(true); r.Pump();
        Equal(0, trapped, "focus cleanup must not invoke key trap");
    }

    private static void FocusLossDropsOncePerFrameCache()
    {
        var r = new Rig(); r.Adapter.QueueKey(Key.Space, KeyLocation.Unspecified, true);
        r.Adapter.DrainBeforePresent(); r.Pump(); Check(r.Platform.KeyOnceThisFrame(0x39), "once read");
        r.Adapter.SetFocused(false); r.Adapter.SetFocused(true);
        Check(!r.Platform.KeyOnceThisFrame(0x39), "same-clock focus cannot resurrect once-read key");
    }

    private static void DeviceReacquisitionDropsOldFractionalMotion()
    {
        var r = new Rig(); r.Adapter.QueueMotion(0.75, 0, 0, 0); r.Adapter.DrainBeforePresent(); r.Device.PollState();
        r.Device.Shutdown(24, 35);
        r.Adapter.DrainBeforePresent(); r.Device.Init();
        r.Adapter.QueueMotion(0.25, 0, 0, 0); r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Equal(320, r.Platform.Mouse.X, "no pre-menu fraction");
    }

    private static void UnsupportedAndNonfiniteInputAreNotAdmitted()
    {
        var r = new Rig();
        Check(!r.Adapter.QueueKey(Key.None, KeyLocation.Unspecified, true), "no logical fallback");
        Check(!r.Adapter.QueueButton(Button.WheelLeft, true, 0, 0), "horizontal wheel unsupported");
        try { r.Adapter.QueueMotion(double.NaN, 0, 0, 0); throw new Exception("NaN accepted"); }
        catch (ArgumentOutOfRangeException) { }
        Equal(0, r.Adapter.PendingEvents, "invalid events do not mutate queue");
    }

    private static void DeviceReacquisitionDropsUnpolledPacketAndWheelFraction()
    {
        var r = new Rig();
        r.Adapter.QueueMotion(80.75, -10.75, 0, 0);
        r.Adapter.QueueButton(Button.WheelUp, true, 0, 0, 1.0 / 480);
        r.Adapter.QueueButton(Button.Left, true, 0, 0);
        r.Adapter.DrainBeforePresent(); // Deliberately NOT polled before Shutdown.
        r.Device.Shutdown(24, 35); r.Adapter.DrainBeforePresent(); r.Device.Init();
        r.Adapter.QueueMotion(0.25, -0.25, 0, 0);
        r.Adapter.QueueButton(Button.Left, false, 0, 0);
        r.Adapter.QueueButton(Button.WheelUp, true, 0, 0, 3.0 / 480);
        r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Equal(320, r.Platform.Mouse.X, "unpolled pre-shutdown x discarded");
        Equal(240, r.Platform.Mouse.Y, "unpolled pre-shutdown y discarded");
        Equal(0, r.Platform.Mouse.Wheel, "pre-shutdown wheel fraction discarded");
        Check(!r.Platform.Mouse.Held[0], "new release defeats stale device button");
    }

    private static void ExplicitPauseBoundaryDropsSameBatchMenuKeysAndRecreatedMouse()
    {
        var r = new Rig();
        r.Adapter.QueueKey(Key.Escape, KeyLocation.Unspecified, true);
        r.Adapter.QueueKey(Key.Down, KeyLocation.Unspecified, true);
        r.Adapter.QueueKey(Key.Enter, KeyLocation.Unspecified, true);
        r.Adapter.QueueMotion(17.75, 0, 0, 0);
        r.Adapter.DrainBeforePresent(); r.Pump();
        r.Device.Shutdown(24, 35); r.Device.Init(); // Both transitions occur between adapter drains.
        r.Adapter.ReleaseAll(); // Host calls at the observed core pause/restart boundary.
        Check(r.Adapter.Focused && r.Device.Created, "boundary changes neither focus nor core device lifetime");
        Check(!r.Platform.KeyOnce(0xc8) && !r.Platform.KeyOnce(0xd0) && !r.Platform.KeyOnce(0x1c), "no latent hidden menu input");
        r.Adapter.QueueMotion(0.25, 0, 0, 0); r.Adapter.DrainBeforePresent(); r.Device.PollState();
        Equal(320, r.Platform.Mouse.X, "between-drain recreation loses old packet and fraction");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual, string message) where T : IEquatable<T> =>
        Check(expected.Equals(actual), $"{message}: expected {expected}, actual {actual}");
}
