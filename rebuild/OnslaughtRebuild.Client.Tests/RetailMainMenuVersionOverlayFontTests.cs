// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay font-slot leftover after
/// dest/Z, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuVersionOverlay already owns the sprintf at
/// <c>0x0046416E</c> and the settled pack at <c>0x004641B1</c> /
/// <c>0x004641B4</c>. RetailMainMenuVersionOverlayZ already owns
/// GetWindowHeight-16 dest Y, dest X push 0, and Z 0.01. Official
/// bytes independently re-read this cycle: <c>push 1</c> at
/// <c>0x004641E4</c> then <c>call 0x00515A70</c> at
/// <c>0x004641E6</c> then <c>mov ecx, eax</c> then
/// <c>CDXFont__DrawTextDynamic</c> at <c>0x004641ED</c>.
/// <c>CPlatform__Font</c> is <c>cmp eax, 3 / ja zero / jmp [eax*4+0x00515AA0]</c>.
/// Slot 1 jumps to <c>0x00515A8C</c> (<c>mov eax, [ecx+0x20]; ret 4</c>),
/// not this+0x1C. Slot 2 is this+0x1C. InitFonts stores
/// <c>Font13PS.tga</c> (<c>0x0063E10C</c>) at <c>[esi+0x20]</c>
/// (<c>0x0051572C</c>) after <c>push 16</c> and
/// <c>CDXBitmapFont__InitTextureFontSlot</c>. GPL
/// <c>Platform.h</c> <c>FONT_SMALL</c> is 1 and
/// <c>PCPlatform.cpp</c> InitFonts / Font() agree. No
/// <c>CDXFont__GetTextExtent</c> call on the sprintf buffer.
/// DrawMainMenu keeps title-font DrawText (already Font13PS).
/// Do not invent a 2px kerning hack. Do not redo version dest/Z,
/// title-logo dest/Z, title-logo shadow dest/Z, selector-bar Z/X,
/// writing Z/X, 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayFontTests
{
    [Fact]
    public void SpecimenSitesAreSlotOneThisPlusTwentyFont13PSNotThisPlus1C()
    {
        Assert.Equal(0x00515A70u, RetailMainMenuVersionOverlayFont.FontHelper);
        Assert.Equal(0x00515AA0u, RetailMainMenuVersionOverlayFont.JumpTableSite);
        Assert.Equal(0x00515A80u, RetailMainMenuVersionOverlayFont.Slot0Case);
        Assert.Equal(0x00515A8Cu, RetailMainMenuVersionOverlayFont.Slot1Case);
        Assert.Equal(0x00515A86u, RetailMainMenuVersionOverlayFont.Slot2Case);
        Assert.Equal(0x00515A92u, RetailMainMenuVersionOverlayFont.Slot3Case);
        Assert.Equal(0x18, RetailMainMenuVersionOverlayFont.Slot0Offset);
        Assert.Equal(0x20, RetailMainMenuVersionOverlayFont.Slot1Offset);
        Assert.Equal(0x1C, RetailMainMenuVersionOverlayFont.Slot2Offset);
        Assert.Equal(0x24, RetailMainMenuVersionOverlayFont.Slot3Offset);
        Assert.Equal(0x004641E4u, RetailMainMenuVersionOverlayFont.FontSlotPushSite);
        Assert.Equal(1, RetailMainMenuVersionOverlayFont.FontSlot);
        Assert.Equal(0x004641E6u, RetailMainMenuVersionOverlayFont.FontCallSite);
        Assert.Equal(0x004641EDu, RetailMainMenuVersionOverlayFont.DrawCallSite);
        Assert.Equal(0x00465710u, RetailMainMenuVersionOverlayFont.DrawTextDynamic);
        Assert.Equal(0x00540680u, RetailMainMenuVersionOverlayFont.GetTextExtent);
        Assert.Equal(0x0051571Du, RetailMainMenuVersionOverlayFont.Slot1NamePushSite);
        Assert.Equal(0x0063E10Cu, RetailMainMenuVersionOverlayFont.Slot1NameSite);
        Assert.Equal("Font13PS.tga", RetailMainMenuVersionOverlayFont.Slot1Name);
        Assert.Equal(0x0051571Bu, RetailMainMenuVersionOverlayFont.Slot1CellPushSite);
        Assert.Equal(16, RetailMainMenuVersionOverlayFont.Slot1CellSize);
        Assert.Equal(0x0051572Cu, RetailMainMenuVersionOverlayFont.Slot1StoreSite);
        Assert.Equal(0x0053F830u, RetailMainMenuVersionOverlayFont.InitTextureFontSlot);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.FontHelper,
            RetailMainMenuVersionOverlayFont.FontHelper);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.FontSlot,
            RetailMainMenuVersionOverlayFont.FontSlot);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.FontSlotPushSite,
            RetailMainMenuVersionOverlayFont.FontSlotPushSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.CallSite,
            RetailMainMenuVersionOverlayFont.DrawCallSite);
        Assert.NotEqual(0x1C, RetailMainMenuVersionOverlayFont.Slot1Offset);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFont.Slot1Offset,
            RetailMainMenuVersionOverlayFont.Slot2Offset);
        Assert.False(RetailMainMenuVersionOverlayFont.Slot1IsThisPlus1C);
        Assert.False(RetailMainMenuVersionOverlayFont.Slot1IsDebugFont);
        Assert.False(RetailMainMenuVersionOverlayFont.HasGetTextExtentOnSprintf);
        Assert.False(RetailMainMenuVersionOverlayFont.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayFont.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayFont.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayFont.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayFont.InventsTitleLogoScale);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesVersionOverlayZ);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayFont.UsesTwinFadeGate);
    }

    [Fact]
    public void SelectOffsetOneIsSmallFontAndVersionSiteHasNoGetTextExtent()
    {
        Assert.Equal(0x18, RetailMainMenuVersionOverlayFont.SelectOffset(0));
        Assert.Equal(0x20, RetailMainMenuVersionOverlayFont.SelectOffset(1));
        Assert.Equal(0x1C, RetailMainMenuVersionOverlayFont.SelectOffset(2));
        Assert.Equal(0x24, RetailMainMenuVersionOverlayFont.SelectOffset(3));
        Assert.Equal(0, RetailMainMenuVersionOverlayFont.SelectOffset(4));
        Assert.Equal(0, RetailMainMenuVersionOverlayFont.SelectOffset(-1));
        Assert.Equal("font22.512.tga", RetailMainMenuVersionOverlayFont.SelectName(0));
        Assert.Equal("Font13PS.tga", RetailMainMenuVersionOverlayFont.SelectName(1));
        Assert.Equal("Terminal", RetailMainMenuVersionOverlayFont.SelectName(2));
        Assert.Equal("TitleFont.tga", RetailMainMenuVersionOverlayFont.SelectName(3));
        Assert.Equal(string.Empty, RetailMainMenuVersionOverlayFont.SelectName(4));
        Assert.Equal(1, RetailMainMenuVersionOverlayFont.GplFontSmall);
        Assert.Equal(2, RetailMainMenuVersionOverlayFont.GplFontDebug);
        Assert.Equal(
            RetailMainMenuVersionOverlayFont.Slot1Name,
            RetailMainMenuVersionOverlayFont.SelectName(
                RetailMainMenuVersionOverlayFont.FontSlot));
        Assert.Equal(
            RetailMainMenuVersionOverlayFont.Slot1Offset,
            RetailMainMenuVersionOverlayFont.SelectOffset(
                RetailMainMenuVersionOverlayFont.FontSlot));
        Assert.NotEqual(
            RetailClickToStartGlyphs.FontSlot,
            RetailMainMenuVersionOverlayFont.FontSlot);
        Assert.Equal(
            RetailClickToStartOverlay.FontSlot,
            RetailMainMenuVersionOverlayFont.FontSlot);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFont.GetTextExtent,
            RetailMainMenuVersionOverlayFont.DrawTextDynamic);
        Assert.NotEqual(
            RetailMainMenuVersionOverlayFont.FontCallSite,
            RetailMainMenuVersionOverlayZ.CallSite);
        Assert.False(RetailMainMenuVersionOverlayFont.HasGetTextExtentOnSprintf);
        Assert.False(RetailMainMenuVersionOverlayFont.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayFont.Slot1IsThisPlus1C);
        Assert.False(RetailMainMenuVersionOverlayFont.RedoesVersionOverlayZ);
    }

    [Fact]
    public void DrawMainMenuKeepsTitleFontDrawTextAndDoesNotInventKerning()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

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
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFont", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFont", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFont", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayFont", bar, StringComparison.Ordinal);
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
