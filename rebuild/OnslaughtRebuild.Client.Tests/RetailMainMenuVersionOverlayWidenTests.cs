// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay Text__AsciiToWideScratch leftover
/// after the pre-draw enable-byte and before dest/Z/font/draw, recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuVersionOverlayEnable already owns
/// <c>mov byte [0x00679B40], 0</c> at <c>0x00464180</c>.
/// RetailMainMenuVersionOverlay already owns the sprintf at
/// <c>0x0046416E</c> and the settled pack at <c>0x004641B1</c> /
/// <c>0x004641B4</c>. RetailMainMenuVersionOverlayZ already owns
/// GetWindowHeight-16 dest Y, dest X push 0, and Z 0.01.
/// RetailMainMenuVersionOverlayFont already owns push 1 /
/// FONT_SMALL / Font13PS. RetailMainMenuVersionOverlayFlags already
/// owns the post-draw restore. Official bytes independently re-read
/// this cycle: after the enable-byte store,
/// <c>0x00464187</c> / <c>0x00464189</c> / <c>0x0046418B</c> push
/// 0 / 0 / <c>0x447A0000</c>, then <c>push edx</c> at
/// <c>0x00464190</c> and <c>call 0x004F7BF0</c> at
/// <c>0x00464191</c>. The body is cdecl one-arg: <c>add esp, 4</c>
/// at <c>0x004641A0</c>. EAX is pushed at <c>0x004641A8</c> as the
/// wide scratch pointer. The three earlier pushes stay on the stack;
/// this leftover does not invent dest, wrap, fade, or sheen from
/// them. The 2px MeasureText residual stays open. DrawMainMenu keeps
/// title-font DrawText. Do not redo version dest/Z, version font
/// slot, version post-draw flags, version pre-draw enable,
/// title-logo dest/Z, title-logo shadow dest/Z, selector-bar Z/X,
/// writing Z/X, 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayWidenTests
{
    [Fact]
    public void SpecimenSitesAreCdeclOneArgAfterEnableNotAFade()
    {
        Assert.Equal(0x00464180u, RetailMainMenuVersionOverlayWiden.EnableSiblingSite);
        Assert.Equal(0x00464187u, RetailMainMenuVersionOverlayWiden.FirstLeftoverPushSite);
        Assert.Equal(0x00464189u, RetailMainMenuVersionOverlayWiden.SecondLeftoverPushSite);
        Assert.Equal(0x0046418Bu, RetailMainMenuVersionOverlayWiden.FloatLeftoverPushSite);
        Assert.Equal(0x447A0000u, RetailMainMenuVersionOverlayWiden.FloatLeftoverBits);
        Assert.Equal(0x00464190u, RetailMainMenuVersionOverlayWiden.ArgPushSite);
        Assert.Equal(0x00464191u, RetailMainMenuVersionOverlayWiden.CallSite);
        Assert.Equal(0x004F7BF0u, RetailMainMenuVersionOverlayWiden.AsciiToWideScratch);
        Assert.Equal(0x004F7C62u, RetailMainMenuVersionOverlayWiden.BodyRetSite);
        Assert.Equal(0xC3u, RetailMainMenuVersionOverlayWiden.BodyRetOpcode);
        Assert.Equal(0x004641A0u, RetailMainMenuVersionOverlayWiden.AddEspSite);
        Assert.Equal(4, RetailMainMenuVersionOverlayWiden.AddEspImmediate);
        Assert.Equal(1, RetailMainMenuVersionOverlayWiden.ArgCount);
        Assert.Equal(0x004641A8u, RetailMainMenuVersionOverlayWiden.ReturnPushSite);
        Assert.Equal(0x004641B1u, RetailMainMenuVersionOverlayWiden.ColorSiblingSite);
        Assert.Equal(0x00854D40u, RetailMainMenuVersionOverlayWiden.RingGlobal);
        Assert.Equal(0x0084CD40u, RetailMainMenuVersionOverlayWiden.BankGlobal);
        Assert.Equal(4, RetailMainMenuVersionOverlayWiden.SlotCount);
        Assert.Equal(0xC, RetailMainMenuVersionOverlayWiden.SlotIndexShift);
        Assert.Equal(0xD, RetailMainMenuVersionOverlayWiden.ReturnShift);
        Assert.Equal(0x80, RetailMainMenuVersionOverlayWiden.HighBitMask);
        Assert.Equal(0x100, RetailMainMenuVersionOverlayWiden.HighBitAddend);
        Assert.Equal(0x3C, RetailMainMenuVersionOverlayWiden.SprintfLeaDisp);
        Assert.Equal(
            RetailMainMenuVersionOverlayEnable.StoreSite,
            RetailMainMenuVersionOverlayWiden.EnableSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayEnable.AsciiToWideSiblingSite,
            RetailMainMenuVersionOverlayWiden.CallSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayEnable.AsciiToWideScratch,
            RetailMainMenuVersionOverlayWiden.AsciiToWideScratch);
        Assert.Equal(
            RetailMainMenuVersionOverlay.ShiftSite,
            RetailMainMenuVersionOverlayWiden.ColorSiblingSite);
        Assert.False(RetailMainMenuVersionOverlayEnable.OwnsAsciiToWide);
        Assert.True(RetailMainMenuVersionOverlayWiden.OwnsCall);
        Assert.False(RetailMainMenuVersionOverlayWiden.OwnsLeftoverPushes);
        Assert.False(RetailMainMenuVersionOverlayWiden.OwnsRingLifetime);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsWrapWidth);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayWiden.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayWiden.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesVersionOverlayZ);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesVersionOverlayFont);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesVersionOverlayFlags);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesVersionOverlayEnable);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayWiden.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayWiden.UsesTwinFadeGate);
    }

    [Fact]
    public void WidenOfVersionFormatIsIdentityAndHighBitStoresUnsignedByte()
    {
        Assert.Equal(
            "V1.00",
            RetailMainMenuVersionOverlay.Format(
                RetailMainMenuVersionOverlay.ImageInitialMajor,
                RetailMainMenuVersionOverlay.ImageInitialMinor));
        Assert.Equal(
            "V1.00",
            RetailMainMenuVersionOverlayWiden.Widen(
                RetailMainMenuVersionOverlay.Format(
                    RetailMainMenuVersionOverlay.ImageInitialMajor,
                    RetailMainMenuVersionOverlay.ImageInitialMinor)));
        Assert.Equal("V1.00", RetailMainMenuVersionOverlayWiden.Widen("V1.00"));
        Assert.Equal(string.Empty, RetailMainMenuVersionOverlayWiden.Widen(string.Empty));
        Assert.Equal(0x0041, RetailMainMenuVersionOverlayWiden.WidenUnit(0x41));
        Assert.Equal(0x0000, RetailMainMenuVersionOverlayWiden.WidenUnit(0x00));
        Assert.Equal(0x007F, RetailMainMenuVersionOverlayWiden.WidenUnit(0x7F));
        Assert.Equal(0x0080, RetailMainMenuVersionOverlayWiden.WidenUnit(0x80));
        Assert.Equal(0x00FF, RetailMainMenuVersionOverlayWiden.WidenUnit(0xFF));
        Assert.NotEqual(0x0180, RetailMainMenuVersionOverlayWiden.WidenUnit(0x80));
        Assert.Equal(1000f, RetailMainMenuVersionOverlayWiden.FloatLeftover);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.EnableSiblingSite <
            RetailMainMenuVersionOverlayWiden.FirstLeftoverPushSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.FirstLeftoverPushSite <
            RetailMainMenuVersionOverlayWiden.SecondLeftoverPushSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.SecondLeftoverPushSite <
            RetailMainMenuVersionOverlayWiden.FloatLeftoverPushSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.FloatLeftoverPushSite <
            RetailMainMenuVersionOverlayWiden.ArgPushSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.ArgPushSite <
            RetailMainMenuVersionOverlayWiden.CallSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.CallSite <
            RetailMainMenuVersionOverlayWiden.AddEspSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.AddEspSite <
            RetailMainMenuVersionOverlayWiden.ReturnPushSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.ReturnPushSite <
            RetailMainMenuVersionOverlayWiden.ColorSiblingSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.ColorSiblingSite <
            RetailMainMenuVersionOverlayZ.ScalePushSite);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayWiden.CallSite,
            RetailMainMenuVersionOverlayEnable.StoreSite);
        Assert.False(RetailMainMenuVersionOverlayWiden.OwnsLeftoverPushes);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsWrapWidth);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayWiden.InventsKerningHack);
    }

    [Fact]
    public void DrawMainMenuKeepsTitleFontDrawTextAndDoesNotInventDest()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlayWiden", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayEnable", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayFlags", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayFont", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlayZ.DestY", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuVersionOverlay.Format", draw, StringComparison.Ordinal);
        Assert.Contains("VersionTint", draw, StringComparison.Ordinal);
        Assert.Contains("DrawText(", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RetailMainMenuVersionOverlay.SubmittedColor",
            draw,
            StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("42f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(" - 2", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.01", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.29", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000.0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x447A0000", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayWiden", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayWiden", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayWiden", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayWiden", bar, StringComparison.Ordinal);
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
