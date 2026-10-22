// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

/// <summary>
/// CJoystickEnableItem's toggle (PauseMenu.cpp:1374, 004d0410), from the source: the harness machine has no joysticks,
/// so no port tape can select the item.
/// </summary>
public sealed class JoystickEnableTests
{
    // A configurable record: input i's pad at 8 + 12i, its type at 12 + 12i.
    private static void SetInput(byte[] records, int record, int input, int pad, PushType type)
    {
        Span<byte> r = records.AsSpan(record * ControlBindings.RecordSize);
        BinaryPrimitives.WriteInt32LittleEndian(r[(8 + 12 * input)..], pad);
        BinaryPrimitives.WriteInt32LittleEndian(r[(12 + 12 * input)..], (int)type);
    }

    [Fact]
    public void TakingAJoystickOutClearsItsJoystickInputsOnlyAndPuttingItBackRestoresNothing()
    {
        ControlBindings bindings = ControlBindings.Defaults();
        byte[] records = bindings.SaveConfigurable();
        SetInput(records, 0, 0, 1, PushType.ButtonOn);
        SetInput(records, 0, 1, 1, PushType.KeyOn);
        SetInput(records, 1, 0, 0, PushType.ButtonRepeat);
        SetInput(records, 2, 0, 1, PushType.AnalogueMinusActAsButtonRepeat);
        SetInput(records, 3, 0, 1, PushType.AnaloguePlus);
        SetInput(records, 4, 0, 1, PushType.MouseButtonOn);
        bindings.LoadConfigurable(records);
        int[] configurable = Enumerable.Range(0, bindings.Count).Where(bindings.Configurable).ToArray();
        var machine = new MachineDisplay(new DisplayReset()) { Joypads = 2 };
        Assert.True(machine.JoystickInUse[1]);

        OptionSettings.ToggleJoystick(machine, bindings, 1);
        Assert.False(machine.JoystickInUse[1]);
        Assert.Equal(-1, bindings.InputPad(configurable[0], 0)); // a button on pad 1
        Assert.Equal(1, bindings.InputPad(configurable[0], 1)); // a key keeps its pad
        Assert.Equal(0, bindings.InputPad(configurable[1], 0)); // another pad
        Assert.Equal(-1, bindings.InputPad(configurable[2], 0)); // the last button-read type
        Assert.Equal(-1, bindings.InputPad(configurable[3], 0)); // an axis is a joystick input too
        Assert.Equal(1, bindings.InputPad(configurable[4], 0)); // a mouse button keeps its pad

        OptionSettings.ToggleJoystick(machine, bindings, 1);
        Assert.True(machine.JoystickInUse[1]);
        Assert.Equal(-1, bindings.InputPad(configurable[0], 0));
        Assert.Equal(1, bindings.InputPad(configurable[4], 0));
    }
}
