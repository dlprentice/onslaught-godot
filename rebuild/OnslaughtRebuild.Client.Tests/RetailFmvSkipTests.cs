// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the retail FMV abort law recovered from the pristine specimen
/// (a private pristine copy), SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle).
///
/// <para><b>Receiver.</b> <c>CFMV::ReceiveButtonAction</c> <c>0x004656E0</c>–
/// <c>0x00465708</c>. File offset = VA − <c>0x400000</c>.
/// Independently disassembled this cycle:
/// a compare of eax with 7, branching when equal, a compare of eax with 0x42, branching when not equal,
/// a bit test of field +0x0C of ecx, branching when zero, then
/// a store of constant 1 to field +8 of ecx and a store of constant 0 to the value at 0x006630CC.</para>
///
/// <para><b>Default keys.</b> <c>OptionsEntries__InitDefaultSingleBindingsTable</c>
/// <c>0x00514210</c> issues four <c>KEY_ONCE=8</c> rows for button 7 with
/// DIK <c>0x39</c>/<c>0x1C</c>/<c>0x01</c>/<c>0x9C</c>.</para>
///
/// <para><b>Mouse.</b> Playback loop <c>0x0053F190</c> at
/// <c>0x0053F2EB</c> ORs three dwords <c>0x0089BDF8</c> /
/// <c>0x0089BE10</c> / <c>0x0089BE28</c> and takes the same quit write.
/// Identified as L/M/R transient latches by
/// <c>CGame__RunIntroFMV.md</c>; this cycle only re-reads the OR, not the
/// slot-to-button names.</para>
///
/// <para>The mutation these cases kill is
/// <c>RetailStartupSequence._Input</c> aborting on any key, any mouse
/// button, or any joypad button.</para>
/// </summary>
public sealed class RetailFmvSkipTests
{
    [Fact]
    public void OnlyButtonSevenAlwaysWritesTheQuitFlag()
    {
        // 0x004656E4: a compare of eax with 7 / je write. Controller.h BUTTON_SKIP_CUTSCENE=7.
        Assert.Equal(7, RetailFmvSkip.SkipCutsceneButton);
        Assert.True(RetailFmvSkip.AcceptsButton(7, attractGate: false));
        Assert.True(RetailFmvSkip.AcceptsButton(7, attractGate: true));
        Assert.False(RetailFmvSkip.AcceptsButton(0, attractGate: true));
        Assert.False(RetailFmvSkip.AcceptsButton(1, attractGate: true));
        Assert.False(RetailFmvSkip.AcceptsButton(0x2C, attractGate: true));
        Assert.False(RetailFmvSkip.AcceptsButton(0x3A, attractGate: true));
    }

    [Fact]
    public void ButtonSixtySixWritesOnlyWhenTheAttractGateIsNonzero()
    {
        // 0x004656E9: a compare of eax with 0x42; 0x004656EE: a load of field +0x0C of ecx into eax; test/jz ret.
        // Controller.h BUTTON_BREAK_ATTRACT_MODE=66. No default keyboard row
        // for 0x42 was found in 0x00514210 this cycle; the predicate is still
        // the receiver's.
        Assert.Equal(66, RetailFmvSkip.BreakAttractButton);
        Assert.False(RetailFmvSkip.AcceptsButton(66, attractGate: false));
        Assert.True(RetailFmvSkip.AcceptsButton(66, attractGate: true));
    }

    [Fact]
    public void DefaultSkipScanCodesAreTheFourKeyOnceRowsForButtonSeven()
    {
        // 0x00514210 +0x16e / +0x182 / +0x196 / +0x1ad:
        // push 0; push DIK; push 8; push 7; push 0.
        Assert.Equal(new[] { 0x39, 0x1C, 0x01, 0x9C }, RetailFmvSkip.DefaultSkipScanCodes);
        Assert.True(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x39));
        Assert.True(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x1C));
        Assert.True(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x01));
        Assert.True(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x9C));
        Assert.False(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x1E)); // A
        Assert.False(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x0F)); // Tab
        Assert.False(RetailFmvSkip.AcceptsDefaultSkipScanCode(0x48)); // Up
    }

    [Fact]
    public void AnyOfTheThreeBackendLatchSlotsQuitsAndExtraMouseButtonsDoNot()
    {
        // 0x0053F2EB: a load of the value at 0x0089BDF8 into eax / test / jnz quit, then +0x18, +0x18.
        Assert.True(RetailFmvSkip.AcceptsMouseLatch(left: true, middle: false, right: false));
        Assert.True(RetailFmvSkip.AcceptsMouseLatch(left: false, middle: true, right: false));
        Assert.True(RetailFmvSkip.AcceptsMouseLatch(left: false, middle: false, right: true));
        Assert.False(RetailFmvSkip.AcceptsMouseLatch(left: false, middle: false, right: false));
        Assert.False(RetailFmvSkip.AcceptsExtraMouseButton);
    }

    [Fact]
    public void StartupSequenceAbortsOnlyOnTheCitedSkipSet()
    {
        string sequence = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailStartupSequence.cs"));

        Assert.Contains("RetailFrontendScenePath.AcceptsStartupSkip", sequence);
        Assert.DoesNotContain("InputEventJoypadButton", sequence);
        // The withdrawn any-key arm.
        Assert.DoesNotContain("InputEventKey key => key.Pressed && !key.Echo,", sequence);
    }
}
