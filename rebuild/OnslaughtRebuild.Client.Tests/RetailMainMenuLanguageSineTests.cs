// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render language-selected sine at
/// <c>0x0046319E</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para>The gate is <c>[edi+8] == -1</c>. Session cannot hold that
/// value, so this is not a wire and not <c>SetLanguage</c>. Cold
/// <c>mCounter</c> is BSS 0. This is not the 0x00463E8D twin fade
/// and not a Process increment.</para>
/// </summary>
public sealed class RetailMainMenuLanguageSineTests
{
    [Fact]
    public void SpecimenPoolIsPiApproxTimesOneThirtiethTimesNegativeThirtyTwo()
    {
        Assert.Equal(0x008A9570u, RetailMainMenuLanguageSine.CounterGlobal);
        Assert.Equal(0u, RetailMainMenuLanguageSine.ImageInitialCounterBits);
        Assert.Equal(0f, RetailMainMenuLanguageSine.ImageInitialCounter);
        Assert.Equal(0x005DB4F4u, RetailMainMenuLanguageSine.PiApproxGlobal);
        Assert.Equal(0x4048F5C3u, RetailMainMenuLanguageSine.PiApproxBits);
        Assert.Equal(3.14f, RetailMainMenuLanguageSine.PiApprox);
        Assert.Equal(0x005DB4F0u, RetailMainMenuLanguageSine.ReciprocalThirtyGlobal);
        Assert.Equal(0x3D088889u, RetailMainMenuLanguageSine.ReciprocalThirtyBits);
        Assert.Equal(1f / 30f, RetailMainMenuLanguageSine.ReciprocalThirty, 7);
        Assert.Equal(0x005DB5DCu, RetailMainMenuLanguageSine.AmplitudeGlobal);
        Assert.Equal(0xC2000000u, RetailMainMenuLanguageSine.AmplitudeBits);
        Assert.Equal(-32f, RetailMainMenuLanguageSine.Amplitude);
        Assert.Equal(0xFF7F7F7Fu, RetailMainMenuLanguageSine.SkipPackedColor);
        Assert.Equal(0xFFDFDFDFu, RetailMainMenuLanguageSine.SelectedBasePackedColor);
        Assert.Equal(-1, RetailMainMenuLanguageSine.LanguageSelectedIndex);
        Assert.False(RetailMainMenuLanguageSine.IsSetLanguage);
        Assert.False(RetailMainMenuLanguageSine.IsButtonPressed);
    }

    [Fact]
    public void OnlyMinusOneTakesTheSineArm()
    {
        Assert.False(RetailMainMenuLanguageSine.IsSelected(0));
        Assert.False(RetailMainMenuLanguageSine.IsSelected(1));
        Assert.False(RetailMainMenuLanguageSine.IsSelected(4));
        Assert.True(RetailMainMenuLanguageSine.IsSelected(-1));
        Assert.Equal(
            RetailMainMenuHitTest.LanguageSelectedIndex,
            RetailMainMenuLanguageSine.LanguageSelectedIndex);
    }

    [Fact]
    public void SkipArmIsTheSeededGreyAndDoesNotUseTheCounter()
    {
        Assert.Equal(
            0xFF7F7F7Fu,
            RetailMainMenuLanguageSine.PackedColor(selected: false, 0f));
        Assert.Equal(
            0xFF7F7F7Fu,
            RetailMainMenuLanguageSine.PackedColor(selected: false, 15f));
        Assert.Equal(0, RetailMainMenuLanguageSine.Channel(0f));
    }

    [Fact]
    public void ColdSelectedCounterRestoresTheBasePackedGrey()
    {
        float cold = RetailMainMenuLanguageSine.ImageInitialCounter;
        Assert.Equal(0, RetailMainMenuLanguageSine.Channel(cold));
        Assert.Equal(
            0xFFDFDFDFu,
            RetailMainMenuLanguageSine.PackedColor(selected: true, cold));
    }

    [Fact]
    public void QuarterTurnFillsTheNegativeAmplitudeAndWrapsThePack()
    {
        // 15 * 3.14 / 30 = 1.57; sin(1.57) ≈ 1; * -32 ≈ -32.
        Assert.Equal(-32, RetailMainMenuLanguageSine.Channel(15f));
        Assert.Equal(
            0xFFFFFFFFu,
            RetailMainMenuLanguageSine.PackedColor(selected: true, 15f));
    }

    [Fact]
    public void DrawLanguageSelectorDoesNotInventSetLanguageOrLightThePulse()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawLanguageSelector");

        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLanguageSine.PackedColor", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0xfd3f3f3f", flow, StringComparison.OrdinalIgnoreCase);
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
