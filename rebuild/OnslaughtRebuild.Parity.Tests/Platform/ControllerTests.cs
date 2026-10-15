// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Platform;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Platform;

public sealed class ControllerTests
{
    private sealed class Receiver(ControlType type, bool inPause = false) : IButtonReceiver
    {
        public List<(int Button, float Value)> Received { get; } = [];
        public void ReceiveButtonAction(Controller from, int button, float value) => Received.Add((button, value));
        public bool CanBeControlledWhenInPause => inPause;
        public ControlType ControlType => type;
    }

    private sealed class Game : IControllerGame
    {
        public bool IsPaused { get; set; }
        public bool IsMultiplayer { get; set; }
        public List<int> Debug { get; } = [];
        public void ReceiveButtonAction(Controller from, int button, float value) => Debug.Add(button);
    }

    private sealed class Rig
    {
        public PlatformInput Input { get; } = new(new PlatformWindow { Width = 640, Height = 480 });
        public ControllerShared Shared { get; } = new();
        public Game Game { get; } = new();
        public float Time { get; set; } = 10f;
        public long Counter { get; private set; }
        public ControllerContext Context { get; }

        public Rig() => Context = new ControllerContext(ControlBindings.Defaults(), Input, Shared, Game, () => Time);

        /// <summary>A frame: time moves, the platform pumps, then the controller flushes.</summary>
        public void Frame(Controller controller)
        {
            Counter += 333_333;
            Time += 1f / 30f;
            Input.Process(Counter);
            controller.Flush(Context);
        }
    }

    private static string GoldPath()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "tests_shared", "fixtures", "gold_career_save.bin");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("gold_career_save.bin");
    }

    [Fact]
    public void TheDefaultTableHas47BindingsOf32BytesAnd16Configurable()
    {
        ControlBindings bindings = ControlBindings.Defaults();
        Assert.Equal(47, bindings.Count);
        Assert.Equal(CareerSaveGame.BindingsSize, bindings.SaveConfigurable().Length);
        Assert.Equal(Buttons.MechYawLeft, bindings.Id(0));
        Assert.Equal(PushType.KeyOn, bindings.InputType(4, 0));
        Assert.Equal((short)'A', bindings.InputVirtualKey(4, 0));
        Assert.Equal(-1, bindings.InputPad(4, 1));
    }

    [Fact]
    public void TheGoldCareersBindingsLoadAndSaveBackByteForByte()
    {
        byte[] gold = File.ReadAllBytes(GoldPath());
        ReadOnlySpan<byte> block = gold.AsSpan(2 + CareerRecord.Size, CareerSaveGame.BindingsSize);
        ControlBindings bindings = ControlBindings.Defaults();
        bindings.LoadConfigurable(block);
        Assert.Equal(block.ToArray(), bindings.SaveConfigurable());
        Assert.Equal(47, bindings.Count);
    }

    [Fact]
    public void EnterRaisesEveryReturnBindingOnceInTableOrderThenNothing()
    {
        Rig rig = new();
        Receiver frontEnd = new(ControlType.FrontEnd);
        Controller controller = new(0, 1, frontEnd);
        rig.Input.PostTapeKey(0x0D, Dik.Return, down: true);
        rig.Frame(controller);
        Assert.Equal([Buttons.SkipCutscene, Buttons.SkipPanning, Buttons.FrontEndMenuSelect, Buttons.PauseMenuSelect],
            frontEnd.Received.Select(r => r.Button));
        frontEnd.Received.Clear();
        rig.Frame(controller);
        Assert.Empty(frontEnd.Received);
    }

    [Fact]
    public void HeldMovementKeysSendTheirDeflectionEveryFrame()
    {
        Rig rig = new();
        Receiver mech = new(ControlType.Mech);
        Controller controller = new(0, 1, mech);
        rig.Input.PostTapeKey(0x57, Dik.W, down: true);
        rig.Frame(controller);
        rig.Frame(controller);
        Assert.Equal([(Buttons.MechForward, -1f), (Buttons.CameraMoveForward, -1f), (Buttons.MechForward, -1f), (Buttons.CameraMoveForward, -1f)],
            mech.Received);
    }

    [Fact]
    public void MouseAxesClampAndACentredAxisIsNotActivity()
    {
        Rig rig = new();
        Receiver mech = new(ControlType.Mech);
        Controller controller = new(0, 1, mech);
        rig.Context.DirectInputCreated = true;
        rig.Input.Mouse.X = 320 + 100;
        rig.Input.Mouse.Y = 240;
        rig.Shared.LastTimeAnythingPressed = 1f;
        rig.Frame(controller);
        Assert.Contains((Buttons.MechYawRight, 1f), mech.Received);
        Assert.Contains((Buttons.MechYawLeft, 0f), mech.Received);
        Assert.Contains((Buttons.MechPitchUp, 0f), mech.Received);
        Assert.True(rig.Shared.MouseAxisRead);
        // Yaw right was real activity.
        Assert.Equal(rig.Time, rig.Shared.LastTimeAnythingPressed);

        Rig centred = new();
        Controller other = new(0, 1, new Receiver(ControlType.Mech));
        centred.Context.DirectInputCreated = true;
        centred.Input.Mouse.X = 320;
        centred.Input.Mouse.Y = 240;
        centred.Shared.LastTimeAnythingPressed = 1f;
        centred.Frame(other);
        Assert.Equal(1f, centred.Shared.LastTimeAnythingPressed);
    }

    [Fact]
    public void WithoutTheDirectInputMouseAxesSendNothing()
    {
        Rig rig = new();
        Receiver mech = new(ControlType.Mech);
        Controller controller = new(0, 1, mech);
        rig.Input.Mouse.X = 500;
        rig.Frame(controller);
        Assert.DoesNotContain(mech.Received, r => r.Button == Buttons.MechYawRight);
    }

    [Fact]
    public void BothShiftsSendTheCheat()
    {
        Rig rig = new();
        Receiver frontEnd = new(ControlType.FrontEnd);
        Controller controller = new(0, 1, frontEnd);
        rig.Input.PostTapeKey(0x10, Dik.LShift, down: true);
        rig.Input.PostTapeKey(0x10, Dik.RShift, down: true);
        rig.Frame(controller);
        Assert.Contains((Buttons.FrontEndCheat, 0f), frontEnd.Received);
    }

    [Fact]
    public void PauseBlocksAReceiverThatCannotBeControlledInPauseAndDebugButtonsGoToTheGame()
    {
        Rig rig = new();
        Receiver mech = new(ControlType.Mech, inPause: false);
        Controller controller = new(0, 1, mech);
        rig.Game.IsPaused = true;
        rig.Input.PostTapeKey(0x0D, Dik.Return, down: true);
        rig.Frame(controller);
        Assert.Empty(mech.Received);
        Assert.Equal([Buttons.SkipCutscene], rig.Game.Debug);
    }

    [Fact]
    public void TheNewestControlledObjectReceivesTheButtons()
    {
        Rig rig = new();
        Receiver first = new(ControlType.FrontEnd), second = new(ControlType.FrontEnd);
        Controller controller = new(0, 1, first);
        controller.SetToControl(second);
        rig.Input.PostTapeKey(0x26, Dik.Up, down: true);
        rig.Frame(controller);
        Assert.Empty(first.Received);
        Assert.Contains((Buttons.FrontEndMenuUp, 1f), second.Received);
        controller.RelinquishControl();
        Assert.Same(first, controller.ToControl);
    }

    [Fact]
    public void TheCursorIsDrawnBackTowardsTheCentreByAtLeastAPixel()
    {
        PlatformInput input = new(new PlatformWindow { Width = 640, Height = 480 });
        ControllerShared shared = new() { MouseAxisRead = true };
        input.Mouse.X = 400;
        input.Mouse.Y = 241;
        Controller.UpdateMouseCentre(input, shared, reset: false);
        Assert.Equal((367, 240), (input.Mouse.X, input.Mouse.Y));
        Assert.False(shared.MouseAxisRead);
        Controller.UpdateMouseCentre(input, shared, reset: false);
        Assert.Equal((367, 240), (input.Mouse.X, input.Mouse.Y));
    }

    [Fact]
    public void OldButtonsAbove63ReadTheSecondWordAsRetailDoes()
    {
        Rig rig = new();
        Controller controller = new(0, 1, new Receiver(ControlType.FrontEnd));
        controller.SetButton(38);
        controller.Flush(rig.Context);
        Assert.True(controller.IsButtonOldSet(38));
        // 70 - 32 = 38: the missing else reads the second word's bit 6.
        Assert.True(controller.IsButtonOldSet(70));
    }
}
