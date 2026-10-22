// SPDX-License-Identifier: GPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using OnslaughtRebuild.Parity.Career;
using OnslaughtRebuild.Parity.Flow;
using OnslaughtRebuild.Parity.Flow.Menus;
using OnslaughtRebuild.Parity.Platform;
using OnslaughtRebuild.Parity.Tests.Career;
using Xunit;

namespace OnslaughtRebuild.Parity.Tests.Flow;

public sealed class KeyboardPresetTests
{
    // Isolated original-code results, 2026-10-17, pristine BEA.exe SHA-256 74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750.
    // Execute 00514210/00453460/00453630 (table initializers), their constructors, 0042db10 (first-ID lookup)
    // and 00453780 (preset application), with no imported calls/stubs. Native x86 and Unicorn agree on every byte
    // of the initial ten whole-table results. extended.py adds 27 signed-preset/enabled-pad cases: 26 agree in both
    // runners; the 32767/no-pad case runs natively only. Enabled pads bound the current keyboard-only host;
    // preset >= 2 with enabled pads has additional joystick effects not implemented here.
    // These hashes cover the first sixteen records, including their padding.
    // Private reproducer, in the maintainers' evidence store: control-preset/{oracle.py,native.c,extended.py}.
    private const string PresetHash = "53f7bfbb170190465f3cdda664a2858bb10bb2736264e8f75dadd3bdc6d1c0cf";

    private static FrontEnd Host()
    {
        FrontEnd frontEnd = FrontEndTests.NewFrontEnd(FrontEndTests.BootedClock());
        CareerOptions options = CareerOptions.Read(new byte[CareerOptions.Size]);
        frontEnd.CurrentOptions = () => options;
        frontEnd.SetOptions = value => options = value;
        return frontEnd;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] Table(ControlBindings bindings) =>
        Enumerable.Range(0, bindings.Count + 1).SelectMany(i => bindings.Record(i).ToArray()).ToArray();

    private static byte[] AlteredBindings()
    {
        byte[] bytes = ControlBindings.Defaults().SaveConfigurable();
        for (int i = 0; i < CareerSaveGame.BindingCount; i++)
        {
            bytes[i * 32 + 1] = (byte)(i + 17);
            bytes[i * 32 + 2] = (byte)(i + 31);
            bytes[i * 32 + 3] = (byte)(i + 49);
            for (int j = 8; j < 32; j++) bytes[i * 32 + j] = unchecked((byte)(i * 37 + j * 11 + 7));
        }
        return bytes;
    }

    [Theory]
    [InlineData("custom_zero", 0, "b86f82745dd4fabdf836d9b19b38051f755aaaecfa3171989b8334f84d40bef8")]
    [InlineData("custom_one", 1, PresetHash)]
    [InlineData("custom_negative", -1, PresetHash)]
    [InlineData("custom_two", 2, PresetHash)]
    [InlineData("custom_three", 3, PresetHash)]
    [InlineData("reordered", 1, "b64c1272bc0bd74ec44a44463111042b54d8270fac980c8d5412a255d9551cdb")]
    [InlineData("unknown_id", 1, "fa14d92838700f563f1048022411c37476b4907cf66ba9a6ba984eef90e16245")]
    [InlineData("duplicate_id", 1, "a0b8e237484aa8b2851ed37026fbb0c2b110847958977d3b5f91e6aaea3546f9")]
    [InlineData("nonconfigurable_id", 1, PresetHash)]
    [InlineData("all_unknown", 1, "11042354e222c25618db239120e663c52b954d74ab890f8bafb8266da5e70d82")]
    public void PresetApplicationMatchesTheOriginalRoutineByIdAndWholeRecord(string variant, int preset, string expectedHash)
    {
        IMenuHost host = Host();
        byte[] input = AlteredBindings();
        switch (variant)
        {
            case "reordered":
                input = Enumerable.Range(0, 16).Reverse().SelectMany(i => input.AsSpan(i * 32, 32).ToArray()).ToArray();
                break;
            case "unknown_id":
                BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(4), 777);
                break;
            case "duplicate_id":
                input.AsSpan(4, 4).CopyTo(input.AsSpan(36, 4));
                break;
            case "nonconfigurable_id":
                input[0] = 0;
                break;
            case "all_unknown":
                for (int i = 0; i < 16; i++) BinaryPrimitives.WriteInt32LittleEndian(input.AsSpan(i * 32 + 4), 777 + i);
                break;
        }

        host.Bindings.LoadConfigurable(input);
        byte[] before = Table(host.Bindings);
        OptionSettings.ApplyControlPreset(host, preset);
        byte[] after = Table(host.Bindings);
        Assert.Equal(preset, host.Options.ControlPreset);
        Assert.Equal(expectedHash, Hash(after.AsSpan(0, 512)));
        Assert.Equal(before[512..], after[512..]); // Every fixed binding and the terminator survive.
        if (preset == 0 || variant == "all_unknown") Assert.Equal(before, after);
        if (variant == "unknown_id") Assert.Equal(before[..32], after[..32]);
        if (variant == "duplicate_id") Assert.Equal(before[32..64], after[32..64]);
        if (variant == "nonconfigurable_id") Assert.True(host.Bindings.Configurable(0));
    }

    [Fact]
    public void ChoosingTheKeyboardPresetCommitsImmediatelyAndCustomKeepsItsBindings()
    {
        IMenuHost host = Host();
        MenuOptionsList controls = OptionsLists.Controls(host, 0, 0, 0);
        var preset = Assert.IsType<OptionMenuItem>(controls.Items[1]);
        preset.Refresh();
        byte[] before = Table(host.Bindings);
        preset.ReceiveButtonAction(FrontEndButtons.MenuRight, 1f);
        Assert.True(preset.Expanded);
        Assert.Equal(before, Table(host.Bindings));
        preset.ReceiveButtonAction(FrontEndButtons.MenuSelect, 1f);
        Assert.False(preset.Expanded);
        Assert.Equal((short)1, host.Options.ControlPreset);
        Assert.Equal(PresetHash, Hash(host.Bindings.SaveConfigurable()));
        Assert.Equal("a6091c2c86a39bc4d31670556caba7b2e036d8add21ae871efd087acf67f70c6", Hash(Table(host.Bindings)));

        byte[] selected = Table(host.Bindings);
        preset.ReceiveButtonAction(FrontEndButtons.MenuLeft, 1f);
        preset.ReceiveButtonAction(FrontEndButtons.MenuSelect, 1f);
        Assert.Equal((short)0, host.Options.ControlPreset);
        Assert.Equal(selected, Table(host.Bindings));
    }

    [Theory]
    [InlineData(short.MinValue)]
    [InlineData((short)-1)]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)2)]
    [InlineData((short)3)]
    [InlineData(short.MaxValue)]
    public void BootAppliesTheSavedPresetAfterLoadingRawBindingsButCareerSelectionDoesNot(short preset)
    {
        byte[] original = File.ReadAllBytes(CareerTests.FixturePath);
        byte[] file = (byte[])original.Clone();
        int bindingOffset = 2 + CareerRecord.Size;
        int optionOffset = bindingOffset + CareerSaveGame.BindingsSize;
        AlteredBindings().CopyTo(file, bindingOffset);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(optionOffset + 8), preset);
        string expectedBindings = preset == 0 ? Hash(AlteredBindings()) : PresetHash;

        var career = OnslaughtRebuild.Parity.Career.Career.Constructed();
        ControlBindings bindings = ControlBindings.Defaults();
        CareerOptions? options = DefaultOptions.Load(career, bindings, file);
        Assert.NotNull(options);
        Assert.Equal(preset, options.ControlPreset);
        Assert.Equal(expectedBindings, Hash(bindings.SaveConfigurable()));
        var expectedCareer = OnslaughtRebuild.Parity.Career.Career.Constructed();
        CareerSaveGame.Load(expectedCareer, file, suggestNextLevel: false);
        expectedCareer.Blank();
        Assert.Equal(expectedCareer.Record.Bytes.ToArray(), career.Record.Bytes.ToArray());
        byte[] saved = CareerSaveGame.Save(career, bindings.SaveConfigurable(), options);
        Assert.Equal(file.AsSpan(optionOffset).ToArray(), saved.AsSpan(optionOffset).ToArray());
        Assert.Equal(expectedBindings, Hash(saved.AsSpan(bindingOffset, CareerSaveGame.BindingsSize)));

        CareerLoadResult selected = CareerSaveGame.Load(career, file, suggestNextLevel: true);
        Assert.True(selected.Loaded);
        Assert.Null(selected.Bindings);
        Assert.Null(selected.Options);
        Assert.Equal(original, File.ReadAllBytes(CareerTests.FixturePath));
    }
}
