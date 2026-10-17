// SPDX-License-Identifier: GPL-3.0-or-later

using Godot;

namespace OnslaughtRebuild.Host;

/// <summary>
/// The live host's US physical-key positions mapped to PC virtual keys and DirectInput scan codes. Godot key enum
/// values are not PC scan codes. This is an input adapter, not a replacement for the game's configurable bindings.
/// International-only, media and unidentified physical keys are not admitted; logical characters are never guessed.
/// </summary>
public static class HostKeyMap
{
    public readonly record struct PcKey(int VirtualKey, int ScanCode);

    public static bool TryMap(Key physical, KeyLocation location, out PcKey mapped)
    {
        bool right = location == KeyLocation.Right;
        mapped = physical switch
        {
            Key.Escape => new(0x1b, 0x01),
            Key.Key1 => new(0x31, 0x02), Key.Key2 => new(0x32, 0x03), Key.Key3 => new(0x33, 0x04),
            Key.Key4 => new(0x34, 0x05), Key.Key5 => new(0x35, 0x06), Key.Key6 => new(0x36, 0x07),
            Key.Key7 => new(0x37, 0x08), Key.Key8 => new(0x38, 0x09), Key.Key9 => new(0x39, 0x0a),
            Key.Key0 => new(0x30, 0x0b), Key.Minus => new(0xbd, 0x0c), Key.Equal => new(0xbb, 0x0d),
            Key.Backspace => new(0x08, 0x0e), Key.Tab => new(0x09, 0x0f),
            Key.Q => new(0x51, 0x10), Key.W => new(0x57, 0x11), Key.E => new(0x45, 0x12),
            Key.R => new(0x52, 0x13), Key.T => new(0x54, 0x14), Key.Y => new(0x59, 0x15),
            Key.U => new(0x55, 0x16), Key.I => new(0x49, 0x17), Key.O => new(0x4f, 0x18),
            Key.P => new(0x50, 0x19), Key.Bracketleft => new(0xdb, 0x1a), Key.Bracketright => new(0xdd, 0x1b),
            Key.Enter => new(0x0d, 0x1c), Key.Ctrl => new(0x11, right ? 0x9d : 0x1d),
            Key.A => new(0x41, 0x1e), Key.S => new(0x53, 0x1f), Key.D => new(0x44, 0x20),
            Key.F => new(0x46, 0x21), Key.G => new(0x47, 0x22), Key.H => new(0x48, 0x23),
            Key.J => new(0x4a, 0x24), Key.K => new(0x4b, 0x25), Key.L => new(0x4c, 0x26),
            Key.Semicolon => new(0xba, 0x27), Key.Apostrophe => new(0xde, 0x28), Key.Quoteleft => new(0xc0, 0x29),
            Key.Shift => new(0x10, right ? 0x36 : 0x2a), Key.Backslash => new(0xdc, 0x2b),
            Key.Z => new(0x5a, 0x2c), Key.X => new(0x58, 0x2d), Key.C => new(0x43, 0x2e),
            Key.V => new(0x56, 0x2f), Key.B => new(0x42, 0x30), Key.N => new(0x4e, 0x31),
            Key.M => new(0x4d, 0x32), Key.Comma => new(0xbc, 0x33), Key.Period => new(0xbe, 0x34),
            Key.Slash => new(0xbf, 0x35), Key.KpMultiply => new(0x6a, 0x37),
            Key.Alt => new(0x12, right ? 0xb8 : 0x38), Key.Space => new(0x20, 0x39), Key.Capslock => new(0x14, 0x3a),
            Key.F1 => new(0x70, 0x3b), Key.F2 => new(0x71, 0x3c), Key.F3 => new(0x72, 0x3d),
            Key.F4 => new(0x73, 0x3e), Key.F5 => new(0x74, 0x3f), Key.F6 => new(0x75, 0x40),
            Key.F7 => new(0x76, 0x41), Key.F8 => new(0x77, 0x42), Key.F9 => new(0x78, 0x43),
            Key.F10 => new(0x79, 0x44), Key.Numlock => new(0x90, 0x45), Key.Scrolllock => new(0x91, 0x46),
            Key.Kp7 => new(0x67, 0x47), Key.Kp8 => new(0x68, 0x48), Key.Kp9 => new(0x69, 0x49),
            Key.KpSubtract => new(0x6d, 0x4a), Key.Kp4 => new(0x64, 0x4b), Key.Kp5 => new(0x65, 0x4c),
            Key.Kp6 => new(0x66, 0x4d), Key.KpAdd => new(0x6b, 0x4e), Key.Kp1 => new(0x61, 0x4f),
            Key.Kp2 => new(0x62, 0x50), Key.Kp3 => new(0x63, 0x51), Key.Kp0 => new(0x60, 0x52),
            Key.KpPeriod => new(0x6e, 0x53), Key.F11 => new(0x7a, 0x57), Key.F12 => new(0x7b, 0x58),
            Key.KpEnter => new(0x0d, 0x9c), Key.KpDivide => new(0x6f, 0xb5), Key.Print => new(0x2c, 0xb7),
            Key.Pause => new(0x13, 0xc5), Key.Home => new(0x24, 0xc7), Key.Up => new(0x26, 0xc8),
            Key.Pageup => new(0x21, 0xc9), Key.Left => new(0x25, 0xcb), Key.Right => new(0x27, 0xcd),
            Key.End => new(0x23, 0xcf), Key.Down => new(0x28, 0xd0), Key.Pagedown => new(0x22, 0xd1),
            Key.Insert => new(0x2d, 0xd2), Key.Delete => new(0x2e, 0xd3),
            Key.Meta => new(right ? 0x5c : 0x5b, right ? 0xdc : 0xdb), Key.Menu => new(0x5d, 0xdd),
            _ => default,
        };
        return mapped.ScanCode != 0;
    }
}
