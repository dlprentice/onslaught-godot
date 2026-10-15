// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render language-chevron blink at
/// <c>0x00463334</c> / <c>0x004634C0</c>, recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><c>fistp(mCounter)</c> then MSVC signed remainder 64
/// (<c>and edx, 0x8000003F</c> / <c>or 0xFFFFFFC0</c>).
/// <c>cmp edx, 0x32</c> draws. Else <c>[edi+0x1C]/[edi+0x20]</c>
/// must be &gt; <c>[0x005D856C]</c> = 0. Cold BSS counter is 0,
/// so settled frames draw. This is not a Process increment, not
/// SetLanguage, and not the 2x <c>0x007F7F7F</c> no-op copies.</para>
/// </summary>
public sealed class RetailMainMenuLanguageBlinkTests
{
    [Fact]
    public void SpecimenPoolIsSignedRemainderSixtyFourBelowFifty()
    {
        Assert.Equal(0x008A9570u, RetailMainMenuLanguageBlink.CounterGlobal);
        Assert.Equal(0u, RetailMainMenuLanguageBlink.ImageInitialCounterBits);
        Assert.Equal(0f, RetailMainMenuLanguageBlink.ImageInitialCounter);
        Assert.Equal(0x00463334u, RetailMainMenuLanguageBlink.LeftSite);
        Assert.Equal(0x004634C0u, RetailMainMenuLanguageBlink.RightSite);
        Assert.Equal(0x8000003Fu, RetailMainMenuLanguageBlink.SignedMask);
        Assert.Equal(0xFFFFFFC0u, RetailMainMenuLanguageBlink.NegativeFixup);
        Assert.Equal(64, RetailMainMenuLanguageBlink.Period);
        Assert.Equal(0x32, RetailMainMenuLanguageBlink.VisibleBelow);
        Assert.Equal(0x005D856Cu, RetailMainMenuLanguageBlink.TimerThresholdGlobal);
        Assert.Equal(0u, RetailMainMenuLanguageBlink.TimerThresholdBits);
        Assert.Equal(0f, RetailMainMenuLanguageBlink.TimerThreshold);
        Assert.Equal(0f, RetailMainMenuLanguageBlink.ImageInitialTimer);
        Assert.False(RetailMainMenuLanguageBlink.IsSetLanguage);
        Assert.False(RetailMainMenuLanguageBlink.DrawsDoubleCopy);
    }

    [Fact]
    public void ColdCounterDrawsBothChevrons()
    {
        Assert.True(RetailMainMenuLanguageBlink.ShouldDraw(
            RetailMainMenuLanguageBlink.ImageInitialCounter,
            RetailMainMenuLanguageBlink.ImageInitialTimer));
        Assert.Equal(0, RetailMainMenuLanguageBlink.Remainder(0f));
    }

    [Fact]
    public void RemainderWrapsAtSixtyFourAndHidesFromFifty()
    {
        Assert.Equal(49, RetailMainMenuLanguageBlink.Remainder(49f));
        Assert.True(RetailMainMenuLanguageBlink.ShouldDraw(49f, 0f));
        Assert.Equal(50, RetailMainMenuLanguageBlink.Remainder(50f));
        Assert.False(RetailMainMenuLanguageBlink.ShouldDraw(50f, 0f));
        Assert.Equal(63, RetailMainMenuLanguageBlink.Remainder(63f));
        Assert.False(RetailMainMenuLanguageBlink.ShouldDraw(63f, 0f));
        Assert.Equal(0, RetailMainMenuLanguageBlink.Remainder(64f));
        Assert.True(RetailMainMenuLanguageBlink.ShouldDraw(64f, 0f));
        Assert.Equal(-1, RetailMainMenuLanguageBlink.Remainder(-1f));
        Assert.True(RetailMainMenuLanguageBlink.ShouldDraw(-1f, 0f));
    }

    [Fact]
    public void PositiveTimerOverridesTheHiddenBand()
    {
        Assert.False(RetailMainMenuLanguageBlink.ShouldDraw(50f, 0f));
        Assert.True(RetailMainMenuLanguageBlink.ShouldDraw(50f, 0.0001f));
        Assert.False(RetailMainMenuLanguageBlink.ShouldDraw(50f, -1f));
    }

    [Fact]
    public void DrawLanguageSelectorWiresTheBlinkAndLeavesTheHotspotsAlone()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawLanguageSelector");

        Assert.Contains("RetailMainMenuLanguageBlink.ShouldDraw", draw, StringComparison.Ordinal);
        Assert.Contains("ImageInitialCounter", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x007F7F7F", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsApplyPulse", draw, StringComparison.Ordinal);
    }

    private static string Slice(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf("\n    private ", signature.Length, StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }
}
