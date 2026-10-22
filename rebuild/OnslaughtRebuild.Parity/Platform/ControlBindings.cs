// SPDX-License-Identifier: GPL-3.0-only
// Adapted from onslaught-decomp src/PCController.cpp (CONTROL_BINDINGS), src/Controller.cpp (SControlBinding's constructors)
// and src/Controller.h (SControlBinding, SControlInput, EJoyButtonPushType, the button ids), plus src/FEPControls.cpp
// (the keyboard/mouse preset and ApplyControlPreset 00453780).

using System.Buffers.Binary;

namespace OnslaughtRebuild.Parity.Platform;

/// <summary><c>EJoyButtonPushType</c>: how a binding's input is read.</summary>
public enum PushType
{
    ButtonOn,
    ButtonOnce,
    ButtonRelease,
    ButtonRepeat,
    AnaloguePlus,
    AnalogueMinus,
    AnaloguePlusActAsButtonRepeat,
    AnalogueMinusActAsButtonRepeat,
    KeyOnce,
    KeyOn,
    KeyRelease,
    MouseAxisPlusAny,
    MouseAxisMinusAny,
    MouseAxisPlus,
    MouseAxisMinus,
    MouseButtonOn,
    MouseButtonOnce,
    MouseButtonRelease,
}

/// <summary>The game's button ids (Controller.h <c>BUTTON_*</c>) used by the bindings and the code reading them.</summary>
public static class Buttons
{
    public const int ToggleFreeCamera = 1;
    public const int SkipCutscene = 7;
    public const int ConsoleMenuUp = 8;
    public const int ConsoleMenuDown = 9;
    public const int ConsoleMenuSelect = 10;
    public const int MechChangeZoomIn = 16;
    public const int MechChangeZoomOut = 17;
    public const int MechFireGunPod = 18;
    public const int MechChargeGunPod = 19;
    public const int MechChangeWeapon = 20;
    public const int MechLandingJets = 21;
    public const int MechYawLeft = 25;
    public const int MechPitchUp = 26;
    public const int MechYawRight = 27;
    public const int MechPitchDown = 28;
    public const int MechStrafeLeft = 29;
    public const int MechStrafeRight = 30;
    public const int MechForward = 31;
    public const int MechBackward = 32;
    public const int MechMorph = 33;
    public const int CameraPitchUp = 34;
    public const int CameraPitchDown = 35;
    public const int CameraYawLeft = 36;
    public const int CameraYawRight = 37;
    public const int CameraMoveForward = 38;
    public const int CameraMoveBackwards = 39;
    public const int CameraStrafeLeft = 40;
    public const int CameraStrafeRight = 41;
    public const int FrontEndMenuUp = 42;
    public const int FrontEndMenuDown = 43;
    public const int FrontEndMenuSelect = 44;
    public const int FrontEndCheat = 45;
    public const int FrontEndMenuBack = 46;
    public const int PauseMenuUp = 49;
    public const int PauseMenuDown = 50;
    public const int PauseMenuSelect = 51;
    public const int FrontEndMenuLeft = 54;
    public const int FrontEndMenuRight = 55;
    public const int Pause = 56;
    public const int Help = 57;
    public const int SkipPanning = 58;
    public const int MechCloak = 59;

    /// <summary><c>START_ACTION_BUTTONS</c>: below this are the hardwired debug buttons.</summary>
    public const int StartActionButtons = 16;
}

/// <summary>DirectInput key codes (<c>DIK_*</c>) the default bindings use: scan codes, extended keys above 0x80.</summary>
public static class Dik
{
    public const int Escape = 0x01, Back = 0x0E, Tab = 0x0F, W = 0x11, Return = 0x1C, A = 0x1E, S = 0x1F, D = 0x20, F = 0x21, X = 0x2D;
    public const int LShift = 0x2A, RShift = 0x36, Space = 0x39, Numpad8 = 0x48, Numpad2 = 0x50, NumpadEnter = 0x9C;
    public const int Up = 0xC8, Left = 0xCB, Right = 0xCD, Down = 0xD0;
}

/// <summary>
/// The control bindings (<c>CONTROL_BINDINGS</c>) as the game holds them: 32-byte <c>SControlBinding</c> records ending at
/// one with id -1. Each record is kept as its bytes, because a career load copies the 16 configurable ones in raw
/// (<c>CCareer::Load</c>) and a save copies them out raw, padding included.
/// </summary>
public sealed class ControlBindings
{
    public const int RecordSize = 32;

    private readonly byte[][] _records;

    private ControlBindings(byte[][] records) => _records = records;

    /// <summary>The table as static initialisation builds it (PCController.cpp), in order, with its terminator.</summary>
    public static ControlBindings Defaults()
    {
        List<byte[]> r =
        [
            One(true, Buttons.MechYawLeft, PushType.MouseAxisMinus, 0, 0),
            One(true, Buttons.MechYawRight, PushType.MouseAxisPlus, 0, 0),
            One(true, Buttons.MechPitchUp, PushType.MouseAxisPlus, 1, 0),
            One(true, Buttons.MechPitchDown, PushType.MouseAxisMinus, 1, 0),
            One(true, Buttons.MechStrafeLeft, PushType.KeyOn, Dik.A, 'A'),
            One(true, Buttons.MechStrafeRight, PushType.KeyOn, Dik.D, 'D'),
            One(true, Buttons.MechForward, PushType.KeyOn, Dik.W, 'W'),
            One(true, Buttons.MechBackward, PushType.KeyOn, Dik.S, 'S'),
            One(true, Buttons.MechCloak, PushType.KeyOnce, Dik.Tab, 0),
            One(true, Buttons.MechMorph, PushType.KeyOnce, Dik.Space, 0),
            One(true, Buttons.MechChargeGunPod, PushType.MouseButtonOn, 0, 0),
            One(true, Buttons.MechFireGunPod, PushType.MouseButtonRelease, 0, 0),
            One(true, Buttons.MechChangeWeapon, PushType.MouseButtonOnce, 2, 0),
            One(true, Buttons.MechChangeZoomIn, PushType.MouseButtonOnce, 4, 0),
            One(true, Buttons.MechChangeZoomOut, PushType.MouseButtonOnce, 3, 0),
            One(true, Buttons.MechLandingJets, PushType.KeyOn, Dik.LShift, 0),
            One(false, Buttons.FrontEndMenuBack, PushType.KeyOnce, Dik.Back, 0),
            One(false, Buttons.FrontEndMenuBack, PushType.KeyOnce, Dik.Escape, 0),
            One(false, Buttons.SkipCutscene, PushType.KeyOnce, Dik.Space, 0),
            One(false, Buttons.SkipCutscene, PushType.KeyOnce, Dik.Return, 0),
            One(false, Buttons.SkipCutscene, PushType.KeyOnce, Dik.Escape, 0),
            One(false, Buttons.SkipCutscene, PushType.KeyOnce, Dik.NumpadEnter, 0),
            One(false, Buttons.SkipPanning, PushType.KeyOnce, Dik.Space, 0),
            One(false, Buttons.SkipPanning, PushType.KeyOnce, Dik.Return, 0),
            One(false, Buttons.SkipPanning, PushType.KeyOnce, Dik.Escape, 0),
            One(false, Buttons.SkipPanning, PushType.KeyOnce, Dik.NumpadEnter, 0),
            One(false, Buttons.ConsoleMenuUp, PushType.KeyOnce, Dik.Numpad8, 0),
            One(false, Buttons.ConsoleMenuDown, PushType.KeyOnce, Dik.Numpad2, 0),
            One(false, Buttons.FrontEndMenuSelect, PushType.KeyOnce, Dik.Return, 0),
            One(false, Buttons.FrontEndMenuSelect, PushType.KeyOnce, Dik.Space, 0),
            One(false, Buttons.FrontEndMenuDown, PushType.KeyOnce, Dik.Down, 0),
            One(false, Buttons.FrontEndMenuUp, PushType.KeyOnce, Dik.Up, 0),
            One(false, Buttons.FrontEndMenuLeft, PushType.KeyOnce, Dik.Left, 0),
            One(false, Buttons.FrontEndMenuRight, PushType.KeyOnce, Dik.Right, 0),
            One(false, Buttons.Pause, PushType.KeyOnce, Dik.Escape, 0),
            One(false, Buttons.PauseMenuUp, PushType.KeyOnce, Dik.Up, 0),
            One(false, Buttons.PauseMenuDown, PushType.KeyOnce, Dik.Down, 0),
            One(false, Buttons.PauseMenuSelect, PushType.KeyOnce, Dik.Return, 0),
            One(false, Buttons.ToggleFreeCamera, PushType.KeyOnce, Dik.F, 0),
            One(false, Buttons.CameraYawLeft, PushType.KeyOn, Dik.Left, 0),
            One(false, Buttons.CameraYawRight, PushType.KeyOn, Dik.Right, 0),
            One(false, Buttons.CameraPitchUp, PushType.KeyOn, Dik.Up, 0),
            One(false, Buttons.CameraPitchDown, PushType.KeyOn, Dik.Down, 0),
            One(false, Buttons.CameraStrafeLeft, PushType.KeyOn, Dik.A, 0),
            One(false, Buttons.CameraStrafeRight, PushType.KeyOn, Dik.D, 0),
            One(false, Buttons.CameraMoveForward, PushType.KeyOn, Dik.W, 0),
            One(false, Buttons.CameraMoveBackwards, PushType.KeyOn, Dik.S, 0),
            Terminator(),
        ];
        return new ControlBindings([.. r]);
    }

    /// <summary>The records before the terminator.</summary>
    public int Count
    {
        get
        {
            int count = 0;
            while (Id(count) != -1) count++;
            return count;
        }
    }

    public bool Configurable(int binding) => _records[binding][0] != 0;
    public int Id(int binding) => BinaryPrimitives.ReadInt32LittleEndian(_records[binding].AsSpan(4));
    public int InputPad(int binding, int input) => BinaryPrimitives.ReadInt32LittleEndian(_records[binding].AsSpan(8 + 12 * input));
    public PushType InputType(int binding, int input) => (PushType)BinaryPrimitives.ReadInt32LittleEndian(_records[binding].AsSpan(12 + 12 * input));
    public void SetInputPad(int binding, int input, int pad) => BinaryPrimitives.WriteInt32LittleEndian(_records[binding].AsSpan(8 + 12 * input), pad);
    public short InputKey(int binding, int input) => BinaryPrimitives.ReadInt16LittleEndian(_records[binding].AsSpan(16 + 12 * input));
    public short InputVirtualKey(int binding, int input) => BinaryPrimitives.ReadInt16LittleEndian(_records[binding].AsSpan(18 + 12 * input));

    /// <summary>The configurable records' bytes, in table order, as <c>CCareer::Save</c> writes them (16 × 32 for the defaults).</summary>
    public byte[] SaveConfigurable()
    {
        List<byte> bytes = [];
        for (int binding = 0; Id(binding) != -1; binding++)
        {
            if (Configurable(binding)) bytes.AddRange(_records[binding]);
        }
        return [.. bytes];
    }

    /// <summary>
    /// <c>CCareer::Load</c>'s binding copy: each configurable record, in table order, is replaced by the next 32 bytes
    /// of <paramref name="source"/>. The game tests "configurable" on the record before overwriting it.
    /// </summary>
    public void LoadConfigurable(ReadOnlySpan<byte> source)
    {
        int offset = 0;
        for (int binding = 0; Id(binding) != -1; binding++)
        {
            if (Configurable(binding))
            {
                source.Slice(offset, RecordSize).CopyTo(_records[binding]);
                offset += RecordSize;
            }
        }
    }

    public ReadOnlySpan<byte> Record(int binding) => _records[binding];

    /// <summary>
    /// ApplyControlPreset (00453780), for the current host with no enabled joystick inputs. Zero leaves every byte
    /// untouched. Any signed nonzero preset copies each whole static keyboard record into the first binding with
    /// its id, regardless of its configurable flag; missing ids and later duplicate ids keep their bytes.
    /// Retail's signed loop takes one keyboard pass for negative values. Values greater than one cycle through the
    /// same table when no joystick is enabled; the repeated copies are idempotent and collapsed here.
    /// Presets >= 2 with enabled joysticks also change input slots (including fixed records with only one pad).
    /// Those device-dependent effects are outside this keyboard-only host, not implied by this method.
    /// </summary>
    public void ApplyPreset(int preset)
    {
        if (preset == 0) return;

        foreach (byte[] entry in KeyboardAndMousePreset)
        {
            int id = BinaryPrimitives.ReadInt32LittleEndian(entry.AsSpan(4));
            for (int binding = 0; Id(binding) != -1; binding++)
            {
                if (Id(binding) != id) continue;
                entry.CopyTo(_records[binding], 0);
                break;
            }
        }
    }

    // sKeyPresets (initializer 00453460), not CONTROL_BINDINGS' single-input defaults. The initializers and
    // ApplyControlPreset were executed independently from the pristine retail image, natively and in Unicorn;
    // KeyboardPresetTests pins the whole-record output, including static zero padding and alternate key codes.
    private static readonly byte[][] KeyboardAndMousePreset =
    [
        Two(true, Buttons.MechYawLeft, PushType.MouseAxisMinusAny, 0, PushType.KeyOn, 0x24, 0, 'J'),
        Two(true, Buttons.MechYawRight, PushType.MouseAxisPlusAny, 0, PushType.KeyOn, 0x26, 0, 'L'),
        Two(true, Buttons.MechPitchUp, PushType.MouseAxisPlusAny, 1, PushType.KeyOn, 0x17, 0, 'I'),
        Two(true, Buttons.MechPitchDown, PushType.MouseAxisMinusAny, 1, PushType.KeyOn, 0x25, 0, 'K'),
        Two(true, Buttons.MechStrafeLeft, PushType.KeyOn, Dik.Left, PushType.KeyOn, Dik.A, 0, 'A'),
        Two(true, Buttons.MechStrafeRight, PushType.KeyOn, Dik.Right, PushType.KeyOn, Dik.D, 0, 'D'),
        Two(true, Buttons.MechForward, PushType.KeyOn, Dik.Up, PushType.KeyOn, Dik.W, 0, 'W'),
        Two(true, Buttons.MechBackward, PushType.KeyOn, Dik.Down, PushType.KeyOn, Dik.S, 0, 'S'),
        Two(true, Buttons.MechCloak, PushType.KeyOnce, Dik.RShift, PushType.KeyOnce, Dik.Tab, 0, 0),
        Two(true, Buttons.MechMorph, PushType.KeyOnce, 0x52, PushType.KeyOnce, Dik.Space, 0, 0),
        Two(true, Buttons.MechChargeGunPod, PushType.MouseButtonOn, 0, PushType.KeyOn, 0x3a, 0, 0),
        Two(true, Buttons.MechFireGunPod, PushType.MouseButtonRelease, 0, PushType.KeyRelease, 0x3a, 0, 0),
        Two(true, Buttons.MechChangeWeapon, PushType.MouseButtonOnce, 2, PushType.KeyOnce, 0x27, 0, 0),
        Two(true, Buttons.MechChangeZoomIn, PushType.MouseButtonOnce, 4, PushType.KeyOnce, 0x0d, 0, '+'),
        Two(true, Buttons.MechChangeZoomOut, PushType.MouseButtonOnce, 3, PushType.KeyOnce, 0x0c, 0, '-'),
        Two(true, Buttons.MechLandingJets, PushType.KeyOn, 0x9d, PushType.KeyOn, Dik.LShift, 0, 0),
    ];

    /// <summary>SControlBinding's dual-input constructor (0042d2b0): both inputs use pad 0.</summary>
    private static byte[] Two(bool configurable, int button, PushType type, int key, PushType type2, int key2,
        int virtualKey, int virtualKey2)
    {
        byte[] record = One(configurable, button, type, key, virtualKey);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(20), 0);
        BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(24), (int)type2);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(28), (short)key2);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(30), (short)virtualKey2);
        return record;
    }

    /// <summary><c>SControlBinding(configurable, button, type, key, vkey)</c> (0x0042d260): input 1 is unused (pad -1).</summary>
    private static byte[] One(bool configurable, int button, PushType type, int key, int virtualKey)
    {
        byte[] r = new byte[RecordSize];
        r[0] = configurable ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(4), button);
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(8), 0);
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(12), (int)type);
        BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(16), (short)key);
        BinaryPrimitives.WriteInt16LittleEndian(r.AsSpan(18), (short)virtualKey);
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(20), -1);
        return r;
    }

    /// <summary><c>SControlBinding()</c> (0x0042d300): not configurable, id -1; the inputs keep static storage's zeros.</summary>
    private static byte[] Terminator()
    {
        byte[] r = new byte[RecordSize];
        BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(4), -1);
        return r;
    }
}
