// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render main-menu label colour at
/// <c>0x0046300B</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para>Idle <c>0xFF4F4F4F</c>, disabled <c>0x7F1F1F1F</c> when the
/// <c>[edx+0x24]</c> call returns 0, selected <c>0xFFFF6F3F</c> when
/// <c>ebx == [edi+8]</c> (selected wins). Settled ESI 255 then the
/// imul fade and draw unpack submit the capture dwords
/// <c>0xFD4F4F4F</c> / <c>0x7D1F1F1F</c> / <c>0xFDFF6F3F</c>.</para>
/// </summary>
public sealed class RetailMainMenuLabelColorTests
{
    [Fact]
    public void SpecimenImmediatesAreIdleDisabledThenSelected()
    {
        Assert.Equal(0x0046300Bu, RetailMainMenuLabelColor.IdleSite);
        Assert.Equal(0x00463017u, RetailMainMenuLabelColor.DisabledSite);
        Assert.Equal(0x00463021u, RetailMainMenuLabelColor.SelectedSite);
        Assert.Equal(0xFF4F4F4Fu, RetailMainMenuLabelColor.IdlePackedColor);
        Assert.Equal(0x7F1F1F1Fu, RetailMainMenuLabelColor.DisabledPackedColor);
        Assert.Equal(0xFFFF6F3Fu, RetailMainMenuLabelColor.SelectedPackedColor);
        Assert.Equal(0xFD4F4F4Fu, RetailMainMenuLabelColor.CaptureIdle);
        Assert.Equal(0x7D1F1F1Fu, RetailMainMenuLabelColor.CaptureDisabled);
        Assert.Equal(0xFDFF6F3Fu, RetailMainMenuLabelColor.CaptureSelected);
        Assert.Equal(255, RetailMainMenuLabelColor.ImageSettledFadeByte);
        Assert.False(RetailMainMenuLabelColor.IsSetLanguage);
        Assert.False(RetailMainMenuLabelColor.IsButtonPressed);
    }

    [Fact]
    public void SelectedWinsOverDisabled()
    {
        Assert.Equal(
            0xFFFF6F3Fu,
            RetailMainMenuLabelColor.BaseColor(selected: true, available: false));
        Assert.Equal(
            0x7F1F1F1Fu,
            RetailMainMenuLabelColor.BaseColor(selected: false, available: false));
        Assert.Equal(
            0xFF4F4F4Fu,
            RetailMainMenuLabelColor.BaseColor(selected: false, available: true));
        Assert.Equal(
            0xFFFF6F3Fu,
            RetailMainMenuLabelColor.BaseColor(selected: true, available: true));
    }

    [Fact]
    public void SettledSubmitMatchesCaptureDiffuse()
    {
        int esi = RetailMainMenuLabelColor.ImageSettledFadeByte;
        Assert.Equal(
            0xFD4F4F4Fu,
            RetailMainMenuLabelColor.SubmittedColor(selected: false, available: true, esi));
        Assert.Equal(
            0x7D1F1F1Fu,
            RetailMainMenuLabelColor.SubmittedColor(selected: false, available: false, esi));
        Assert.Equal(
            0xFDFF6F3Fu,
            RetailMainMenuLabelColor.SubmittedColor(selected: true, available: true, esi));
        Assert.Equal(
            RetailMainMenuLabelColor.CaptureSelected,
            RetailMainMenuLabelColor.SubmittedColor(selected: true, available: false, esi));
    }

    [Fact]
    public void DrawMainMenuWiresTheSettledPackAndLeavesTheHotspotsAlone()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLabelColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("ImageSettledFadeByte", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm");
        Assert.DoesNotContain("RetailMainMenuLabelColor", quit, StringComparison.Ordinal);
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
