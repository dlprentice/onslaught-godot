// SPDX-License-Identifier: GPL-3.0-only
// Source-flow tests grounded in pristine BEA.exe SHA-256
// 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750:
// CVSync::SetValue 004cf500 queues state 1; CPlatform::Flip 004d2580 clears before reset and saves only state 2;
// CDisplayModeKey::Set 00420d10 packs dimensions and format, not refresh rate. No graphics reset is exercised here.

using System.Buffers.Binary;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Clock;
using OnslaughtRebuild.Parity.Containers;
using OnslaughtRebuild.Parity.Events;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Flow.Pages;
using OnslaughtRebuild.Parity.Game;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Tests.Career;
using OnslaughtRebuild.Parity.Text;
using OnslaughtRebuild.Parity.Things;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class DisplayResetTests
{
    private static FrontEnd Host()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(new PlatformClock());
        byte[] original = File.ReadAllBytes(CareerTests.FixturePath);
        CareerLoadResult loaded = CareerSaveGame.Load(frontEnd.Career, original, suggestNextLevel: false);
        Assert.True(loaded.Loaded);
        frontEnd.Controls.Bindings.LoadConfigurable(loaded.Bindings!);
        CareerOptions options = loaded.Options!;
        frontEnd.CurrentOptions = () => options;
        frontEnd.SetOptions = value => options = value;
        return frontEnd;
    }

    private static OptionMenuItem Item(MenuOptionsList list, int text) =>
        Assert.IsType<OptionMenuItem>(list.Items.Single(i => i.PCText == text));

    private static void Apply(MenuOptionsList list) =>
        list.Items.OfType<ApplyMenuItem>().Single().ReceiveButtonAction(FrontEndButtons.MenuSelect, 1);

    [Fact]
    public void ApplyCommitsEveryChangedSettingBeforeOneDeferredReset()
    {
        FrontEnd frontEnd = Host();
        IMenuHost host = frontEnd;
        var writes = new List<byte[]>();
        frontEnd.WriteDefaultOptionsFile = writes.Add;
        // This menu currently supports quality level 1 only; retain that separate limitation.
        host.Options = OptionSettings.SetQualityLevel(host.Options, 1);
        MenuOptionsList list = OptionsLists.Display(host, 0);
        list.Reset();
        OptionMenuItem resolution = Item(list, 0x13), vsync = Item(list, 0x14), texture = Item(list, 0x16);
        resolution.Selection = 1;
        vsync.Selection = 1 - vsync.Value;
        texture.Selection = (texture.Value + 1) % 3;
        int resets = 0;
        frontEnd.Clock.DisplayReset.ResetEnvironment = (skipDialog, showProgress) =>
        {
            Assert.True(skipDialog);
            Assert.True(showProgress);
            Assert.Equal(DisplayResetRequest.None, frontEnd.Clock.DisplayReset.Pending);
            Assert.Equal(vsync.Selection, host.Options.VSync);
            Assert.Equal(texture.Selection, host.Options.TextureResolutionLossShift);
            Assert.Equal(1, host.Machine.CurrentMode);
            resets++;
        };

        Apply(list);
        Assert.Same(frontEnd.Clock.DisplayReset, host.Machine.Reset);
        Assert.Equal(DisplayResetRequest.Apply, host.Machine.Reset.Pending);
        Assert.Equal((resolution.Selection, vsync.Selection, texture.Selection), (resolution.Value, vsync.Value, texture.Value));
        Assert.Equal(0x82580320u, host.Options.DisplayModeKey); // 800x600, 32-bit format.
        Assert.Equal(0, resets);
        frontEnd.Clock.Presented = () => Assert.Equal(DisplayResetRequest.Apply, host.Machine.Reset.Pending);
        frontEnd.Begin(FrontEndPage.Main);
        Assert.True(frontEnd.Step()); // Exercise the real front-end Render -> platform Flip path.
        Assert.Equal(1, resets);
        Assert.Empty(writes); // State 1 does not save at reset; pause resume/options close save separately.
        frontEnd.Clock.Presented = null;
        Apply(list);
        frontEnd.Clock.Present();
        Assert.Equal(1, resets); // An unchanged Apply queues nothing.
        texture.Selection = (texture.Value + 1) % 3;
        Apply(list);
        frontEnd.Clock.Present();
        Assert.Equal(2, resets); // A new changed Apply gets its own serial.
    }

    [Fact]
    public void RawDevicePresentReportsTheFrameButOnlyPlatformFlipConsumesReset()
    {
        PlatformClock clock = new();
        var order = new List<string>();
        clock.DisplayReset.RequestApply();
        clock.Presented = () => order.Add($"present:{clock.PresentCount}:{clock.DisplayReset.Pending}");
        clock.DisplayReset.ResetEnvironment = (_, _) => order.Add($"reset:{clock.DisplayReset.Pending}");
        clock.PresentDevice();
        Assert.Equal(DisplayResetRequest.Apply, clock.DisplayReset.Pending);
        clock.Present();
        Assert.Equal(["present:1:Apply", "present:2:Apply", "reset:None"], order);
    }

    [Fact]
    public void ResetClearsBeforeNestedLoadingPresentsAndStateTwoSavesAfterSuccess()
    {
        PlatformClock clock = new();
        var order = new List<string>();
        clock.DisplayReset.RequestAfterOptionsRead(displayChanged: true, deviceCreated: true);
        clock.Presented = () => order.Add($"present:{clock.PresentCount}:{clock.DisplayReset.Pending}");
        clock.DisplayReset.ResetEnvironment = (_, _) =>
        {
            order.Add($"reset:{clock.DisplayReset.Pending}");
            clock.Present(); // Models a loading flip; not a real device/resource reset.
            order.Add("reset-return");
        };
        clock.DisplayReset.SaveDefaultOptions = () => order.Add("save");
        clock.Present();
        Assert.Equal(["present:1:LoadedOptions", "reset:None", "present:2:None", "reset-return", "save"], order);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrFailingResetIsExplicitAndCannotSave(bool handlerPresent)
    {
        FrontEnd frontEnd = Host();
        int saves = 0;
        frontEnd.WriteDefaultOptionsFile = _ => saves++;
        if (handlerPresent) frontEnd.Clock.DisplayReset.ResetEnvironment = (_, _) => throw new NotSupportedException("failed device reset");
        frontEnd.Clock.DisplayReset.RequestAfterOptionsRead(displayChanged: true, deviceCreated: true);
        Assert.Throws<NotSupportedException>(frontEnd.Clock.Present);
        Assert.Equal(1, frontEnd.Clock.PresentCount);
        Assert.Equal(DisplayResetRequest.None, frontEnd.Clock.DisplayReset.Pending); // Retail already cleared it.
        Assert.Equal(0, saves);
    }

    [Fact]
    public void AStateTwoResetCannotSilentlyDiscardItsRequiredPersistence()
    {
        FrontEnd frontEnd = Host();
        frontEnd.Clock.DisplayReset.ResetEnvironment = (_, _) => { }; // Transaction stub, not a graphics reset.
        frontEnd.Clock.DisplayReset.RequestAfterOptionsRead(true, true);
        Assert.Throws<NotSupportedException>(frontEnd.Clock.Present);
        Assert.Equal(DisplayResetRequest.None, frontEnd.Clock.DisplayReset.Pending);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void OptionsReadNeedsAChangeAndAnExistingDevice(bool changed, bool device)
    {
        PlatformClock clock = new();
        clock.DisplayReset.RequestAfterOptionsRead(changed, device);
        clock.Present(); // No handler is required without a request.
        Assert.Equal(DisplayResetRequest.None, clock.DisplayReset.Pending);
        clock.DisplayReset.RequestApply();
        clock.DisplayReset.RequestAfterOptionsRead(changed, device);
        Assert.Equal(DisplayResetRequest.Apply, clock.DisplayReset.Pending); // A non-request does not cancel an earlier one.
    }

    [Fact]
    public void LaterRequestUsesRetailAssignmentRatherThanAStatePriority()
    {
        PlatformClock clock = new();
        MachineDisplay display = new(clock.DisplayReset);
        clock.DisplayReset.RequestAfterOptionsRead(true, true);
        display.DisplaySettingChanged();
        Assert.Equal(DisplayResetRequest.Apply, clock.DisplayReset.Pending);
        clock.DisplayReset.RequestAfterOptionsRead(true, true);
        display.DisplaySettingChanged(); // The same Apply serial does not rewrite the reason again.
        Assert.Equal(DisplayResetRequest.LoadedOptions, clock.DisplayReset.Pending);
    }

    [Fact]
    public void StateTwoSerializesCurrentCareerBindingsAndModeAndPreservesOtherBytes()
    {
        FrontEnd frontEnd = Host();
        IMenuHost host = frontEnd;
        byte[] original = File.ReadAllBytes(CareerTests.FixturePath);
        frontEnd.Clock.DisplayReset.RequestAfterOptionsRead(true, true);
        // Mutations after the request prove that persistence reads current state, not a queued stale image.
        frontEnd.Career.Record.SoundVolume = 0.375f;
        byte[] bindings = host.Bindings.SaveConfigurable();
        bindings[1] ^= 1;
        host.Bindings.LoadConfigurable(bindings);
        host.Options = host.Options with { MouseSensitivity = 12f };
        host.Machine.CurrentMode = 2;
        var writes = new List<byte[]>();
        frontEnd.WriteDefaultOptions = writes.Add; // Same sink used by normal career load and post-reset save.
        frontEnd.Clock.DisplayReset.ResetEnvironment = (_, _) => Assert.Empty(writes);
        frontEnd.Clock.Present();
        byte[] actual = Assert.Single(writes);
        byte[] expected = (byte[])original.Clone();
        BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(2 + 9356), 0.375f);
        expected[2 + CareerRecord.Size + 1] ^= 1;
        int options = 2 + CareerRecord.Size + CareerSaveGame.BindingsSize;
        BinaryPrimitives.WriteSingleLittleEndian(expected.AsSpan(options + 4), 12f);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(options + 40), 0x83000400u);
        Assert.Equal(expected, actual); // Whole-image comparison includes all reserved/unknown bytes.
        frontEnd.Clock.Present();
        Assert.Single(writes);
        host.SaveFromPause();
        Assert.Equal(2, writes.Count);
        Assert.Equal(actual, writes[1]); // A later ordinary save must not resurrect the pre-reset mode key.
        Assert.Equal(original, File.ReadAllBytes(CareerTests.FixturePath));
    }

    [Theory]
    [InlineData(DisplayResetRequest.None)]
    [InlineData(DisplayResetRequest.Apply)]
    [InlineData(DisplayResetRequest.LoadedOptions)]
    public void CareerLoadUsesTheSamePendingOwnerWithoutReplacingOptions(DisplayResetRequest pending)
    {
        FrontEnd frontEnd = Host();
        IMenuHost host = frontEnd;
        CareerOptions before = host.Options;
        byte[] gold = File.ReadAllBytes(CareerTests.FixturePath);
        frontEnd.MemoryCard.Folder.Write("RealCareer", gold);
        var writes = new List<byte[]>();
        frontEnd.WriteDefaultOptionsFile = writes.Add;
        if (pending == DisplayResetRequest.Apply) host.Machine.DisplaySettingChanged();
        if (pending == DisplayResetRequest.LoadedOptions) frontEnd.Clock.DisplayReset.RequestAfterOptionsRead(true, true);
        var load = new LoadGamePage(frontEnd) { SaveGameNumber = 0, SaveGameName = "RealCareer" };
        load.StartLoad();
        Assert.Equal(LoadGamePage.Text.LoadDone, load.LastText);
        Assert.Equal(pending, frontEnd.Clock.DisplayReset.Pending);
        Assert.Equal(before, host.Options);
        if (pending == DisplayResetRequest.None) Assert.Equal(gold, Assert.Single(writes));
        else Assert.Empty(writes);
    }

    [Theory]
    [InlineData(800, 600, true, 0x82580320u)]
    [InlineData(800, 600, false, 0x02580320u)]
    [InlineData(0x10280, 0x101e0, true, 0x81e00280u)]
    public void ModeKeyUsesTheSelectedFormatAndRetailBitWidths(int width, int height, bool bits32, uint expected)
    {
        MachineDisplay display = new(new DisplayReset()) { Modes = [new(width, height, 0, bits32)] };
        Assert.Equal(expected, display.CurrentModeKey);
    }

    [Fact]
    public void StandaloneLevelUsesItsClockOwnerAndItsOwnPersistenceSink()
    {
        var clock = new PlatformClock();
        var context = new ThingContext(new SPtrSetPool(), new EventManager(), new SeedUniformRandomNumberStream(1), null!, null!);
        FrontEnd source = Host();
        IMenuHost sourceHost = source;
        var level = new LevelMenuHost(context, clock, source.Career, TextIds.Read([]), sourceHost.Options, sourceHost.Bindings);
        var input = new LevelInput(clock, level.Bindings);
        Assert.Same(clock.DisplayReset, level.Machine.Reset);
        level.Machine.CurrentMode = 1;
        clock.DisplayReset.RequestAfterOptionsRead(true, true);
        clock.DisplayReset.ResetEnvironment = (_, _) => Assert.Null(level.DefaultOptionsFile);
        input.Flip();
        Assert.NotNull(level.DefaultOptionsFile);
        CareerOptions saved = CareerOptions.Read(level.DefaultOptionsFile.AsSpan(CareerSaveGame.Size - CareerOptions.Size));
        Assert.Equal(0x82580320u, saved.DisplayModeKey);
        byte[] savedImage = level.DefaultOptionsFile;
        level.SaveFromPause();
        Assert.Equal(savedImage, level.DefaultOptionsFile);
    }
}
