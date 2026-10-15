// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render unselected language-chevron colour at
/// <c>0x0046336B</c> / <c>0x004634F4</c>, recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para>First ESI pack is the title-body idiom
/// (<c>shl 8 / sub / shl 16 / not / and 0x00FFFFFF / xor</c>).
/// Settled fade-byte 255 yields <c>0xFEFFFFFF</c>, then the
/// unselected <c>shr 8 / shl 6 / xor</c> arm and the draw unpack
/// submit <c>0x3EFFFFFF</c>. Neither dword is capture
/// <c>ChromeTint</c> <c>0x3E7F7F7F</c>. Session cannot hold
/// <c>this+0x08=-1</c>, so this is not a wire and not
/// <c>SetLanguage</c>.</para>
/// </summary>
public sealed class RetailMainMenuLanguageChevronColorTests
{
    [Fact]
    public void SpecimenSitesAreTheTwoFiftyOnePointTwoChevrons()
    {
        Assert.Equal(0x0046336Bu, RetailMainMenuLanguageChevronColor.LeftSite);
        Assert.Equal(0x004634F4u, RetailMainMenuLanguageChevronColor.RightSite);
        Assert.Equal(0x08, RetailMainMenuLanguageChevronColor.SelectedOffset);
        Assert.Equal(-1, RetailMainMenuLanguageChevronColor.LanguageSelectedIndex);
        Assert.Equal(255, RetailMainMenuLanguageChevronColor.ImageSettledFadeByte);
        Assert.Equal(0x00FFFFFFu, RetailMainMenuLanguageChevronColor.RgbMask);
        Assert.Equal(0x3E7F7F7Fu, RetailMainMenuLanguageChevronColor.CaptureDiffuse);
        Assert.Equal(0xFEFFFFFFu, RetailMainMenuLanguageChevronColor.SettledFirstPack);
        Assert.Equal(0x3EFFFFFFu, RetailMainMenuLanguageChevronColor.SettledUnselectedSubmitted);
        Assert.False(RetailMainMenuLanguageChevronColor.IsSetLanguage);
        Assert.False(RetailMainMenuLanguageChevronColor.ReplacesChromeTint);
        Assert.False(RetailMainMenuLanguageChevronColor.IsButtonPressed);
    }

    [Fact]
    public void OnlyMinusOneSkipsTheUnselectedArm()
    {
        Assert.False(RetailMainMenuLanguageChevronColor.IsSelected(0));
        Assert.False(RetailMainMenuLanguageChevronColor.IsSelected(1));
        Assert.False(RetailMainMenuLanguageChevronColor.IsSelected(4));
        Assert.True(RetailMainMenuLanguageChevronColor.IsSelected(-1));
        Assert.Equal(
            RetailMainMenuHitTest.LanguageSelectedIndex,
            RetailMainMenuLanguageChevronColor.LanguageSelectedIndex);
    }

    [Fact]
    public void FadeByteClampsSignedThenCapsAtTwoFiftyFive()
    {
        Assert.Equal(0, RetailMainMenuLanguageChevronColor.ClampFadeByte(-1));
        Assert.Equal(0, RetailMainMenuLanguageChevronColor.ClampFadeByte(0));
        Assert.Equal(254, RetailMainMenuLanguageChevronColor.ClampFadeByte(254));
        Assert.Equal(255, RetailMainMenuLanguageChevronColor.ClampFadeByte(255));
        Assert.Equal(255, RetailMainMenuLanguageChevronColor.ClampFadeByte(256));
        Assert.Equal(
            255,
            RetailMainMenuLanguageChevronColor.ClampFadeByte(
                RetailMainMenuLanguageChevronColor.ImageSettledFadeByte));
    }

    [Fact]
    public void FirstEsiPackDoesNotReproduceCaptureChromeTint()
    {
        uint first = RetailMainMenuLanguageChevronColor.FirstPack(
            RetailMainMenuLanguageChevronColor.ImageSettledFadeByte);
        Assert.Equal(0xFEFFFFFFu, first);
        Assert.NotEqual(RetailMainMenuLanguageChevronColor.CaptureDiffuse, first);
        Assert.Equal(
            RetailClickToStartTitle.BodyColor(3.0d),
            first);
    }

    [Fact]
    public void SettledUnselectedSubmitIsWhiteAlphaNotCaptureGrey()
    {
        uint submitted = RetailMainMenuLanguageChevronColor.SubmittedColor(
            RetailMainMenuLanguageChevronColor.ImageSettledFadeByte,
            selected: false);
        Assert.Equal(0x3EFFFFFFu, submitted);
        Assert.NotEqual(RetailMainMenuLanguageChevronColor.CaptureDiffuse, submitted);
        Assert.Equal(
            0x3EFFFFFFu,
            RetailMainMenuLanguageChevronColor.SubmittedColor(254, selected: false));
    }

    [Fact]
    public void SelectedArmKeepsTheFirstPackThroughTheDrawUnpack()
    {
        Assert.Equal(
            0xFDFFFFFFu,
            RetailMainMenuLanguageChevronColor.SubmittedColor(255, selected: true));
        Assert.NotEqual(
            RetailMainMenuLanguageChevronColor.CaptureDiffuse,
            RetailMainMenuLanguageChevronColor.SubmittedColor(255, selected: true));
    }

    [Fact]
    public void DrawLanguageSelectorKeepsCaptureChromeTintAndDoesNotWireThePack()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawLanguageSelector");

        Assert.Contains("ChromeTint", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLanguageChevronColor.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuLanguageChevronColor.FirstPack",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);
        Assert.Contains("0x3e7f7f7f", flow, StringComparison.OrdinalIgnoreCase);
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
