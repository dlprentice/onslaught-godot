// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render selector-bar colour at
/// <c>0x00462FB9</c>, recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><c>mov eax, esi; shl 7; sub esi; shl 16; and 0xFF000000</c>.
/// ESI is the icon-fade byte after the 0x00462F9C clamp, only on the
/// <c>ebx == [edi+8]</c> arm. Settled 255 submits <c>0x7E000000</c>,
/// which is frame 3000 draw 11 / HighlightTint.</para>
/// </summary>
public sealed class RetailMainMenuSelectorBarColorTests
{
    [Fact]
    public void SpecimenPackIsEsiTimesOneTwentySevenShiftedToAlpha()
    {
        Assert.Equal(0x00462FB9u, RetailMainMenuSelectorBarColor.Site);
        Assert.Equal(0x00462FBDu, RetailMainMenuSelectorBarColor.ShiftSite);
        Assert.Equal(0x7, RetailMainMenuSelectorBarColor.ShiftLeft);
        Assert.Equal(0xFF000000u, RetailMainMenuSelectorBarColor.AlphaMask);
        Assert.Equal(255, RetailMainMenuSelectorBarColor.ImageSettledFadeByte);
        Assert.Equal(0x7E000000u, RetailMainMenuSelectorBarColor.CaptureDiffuse);
        Assert.Equal(0x7E000000u, RetailMainMenuSelectorBarColor.SettledSubmitted);
        Assert.False(RetailMainMenuSelectorBarColor.IsSetLanguage);
        Assert.False(RetailMainMenuSelectorBarColor.IsButtonPressed);
    }

    [Fact]
    public void SettledSubmitMatchesCaptureHighlightTint()
    {
        Assert.Equal(
            0x7E000000u,
            RetailMainMenuSelectorBarColor.SubmittedColor(
                RetailMainMenuSelectorBarColor.ImageSettledFadeByte));
        Assert.Equal(0u, RetailMainMenuSelectorBarColor.SubmittedColor(0));
        Assert.Equal(0x7E000000u, RetailMainMenuSelectorBarColor.SubmittedColor(256));
        Assert.Equal(0u, RetailMainMenuSelectorBarColor.SubmittedColor(-1));
    }

    [Fact]
    public void DrawMainMenuSelectorBarWiresThePackAndLeavesQuitAlone()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenuSelectorBar");

        Assert.Contains("RetailMainMenuSelectorBarColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("ImageSettledFadeByte", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuSelectorBarColor", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuSelectorBarColor", choice, StringComparison.Ordinal);
        // Cited FEMessBox chrome marker; HighlightTint was the reconstruction
        // era marker superseded by the wt/t_7d9a828d merge.
        Assert.Contains("RetailFeMessBox.HighlightColor", choice, StringComparison.Ordinal);
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
