// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render label GetTextExtent dest law at
/// <c>0x00462F3D</c> — SIZE cx/cy, dest X <c>219.0 − cx×0.5</c>,
/// dest Y row-Y minus integer-half cy — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00462F25</c> <c>lea ecx,[esp+0x18]</c>,
/// <c>0x00462F29</c> <c>mov [esp+0x28],eax</c>,
/// <c>push ecx / push eax / push 1</c>,
/// <c>call 0x00515A70</c> at <c>0x00462F36</c>,
/// <c>call 0x00540680</c> at <c>0x00462F3D</c>
/// (<c>RET 8</c>). SIZE store is
/// <c>mov [edi],edx</c> at <c>0x00540807</c> then
/// <c>mov [edi+4],eax</c> at <c>0x00540815</c>. Dest X is
/// <c>fild [esp+0x18]</c> / <c>fmul [0x005D85EC]</c> (0.5) /
/// <c>fsubr [0x005DB5E0]</c> (219.0) into ebx. Dest Y is
/// <c>mov eax,[esp+0x1C]</c> / <c>cdq</c> / <c>sub eax,edx</c> /
/// <c>sar eax,1</c> then <c>fsubr [esp+0x10]</c> into
/// <c>[esp+0x24]</c>. Dest is ebx/ecx, not immediates.
/// The 2px MeasureText residual stays open. DrawMainMenu
/// keeps LabelColor, MeasureText width, and scale 1.0. Do
/// not invent dest, wrap, fade, sheen, or a 2px kerning
/// hack. Do not redo label DrawTextDynamic, version
/// DrawTextDynamic tail, writing Z/X, selector-bar Z/X,
/// 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuLabelDestTests
{
    [Fact]
    public void SpecimenSitesAreGetTextExtentSizeThenHalfExtentDestNotImmediates()
    {
        Assert.Equal(0x00462F25u, RetailMainMenuLabelDest.SizeLeaSite);
        Assert.Equal(0x00462F29u, RetailMainMenuLabelDest.TextSaveSite);
        Assert.Equal(0x00462F2Fu, RetailMainMenuLabelDest.FontSlotPushSite);
        Assert.Equal(1, RetailMainMenuLabelDest.FontSlot);
        Assert.Equal(0x0088A0A8u, RetailMainMenuLabelDest.FontThis);
        Assert.Equal(0x00462F36u, RetailMainMenuLabelDest.FontCallSite);
        Assert.Equal(0x00515A70u, RetailMainMenuLabelDest.FontHelper);
        Assert.Equal(0x00462F3Du, RetailMainMenuLabelDest.GetTextExtentSite);
        Assert.Equal(0x00540680u, RetailMainMenuLabelDest.GetTextExtent);
        Assert.Equal(0x00540807u, RetailMainMenuLabelDest.SizeCxStoreSite);
        Assert.Equal(0x00540815u, RetailMainMenuLabelDest.SizeCyStoreSite);
        Assert.Equal(0, RetailMainMenuLabelDest.SizeCxOffset);
        Assert.Equal(4, RetailMainMenuLabelDest.SizeCyOffset);
        Assert.Equal(8, RetailMainMenuLabelDest.BodyRetImmediate);
        Assert.Equal(0x0046303Bu, RetailMainMenuLabelDest.DestXFildSite);
        Assert.Equal(0x0046303Fu, RetailMainMenuLabelDest.DestXHalfMulSite);
        Assert.Equal(0x005D85ECu, RetailMainMenuLabelDest.HalfGlobal);
        Assert.Equal(0x3F000000u, RetailMainMenuLabelDest.HalfBits);
        Assert.Equal(0x00463045u, RetailMainMenuLabelDest.DestXFsubrSite);
        Assert.Equal(0x005DB5E0u, RetailMainMenuLabelDest.DestXGlobal);
        Assert.Equal(0x435B0000u, RetailMainMenuLabelDest.DestXGlobalBits);
        Assert.Equal(0x0046304Bu, RetailMainMenuLabelDest.DestXStoreSite);
        Assert.Equal(0x00463077u, RetailMainMenuLabelDest.DestXLoadSite);
        Assert.Equal(0x0046308Cu, RetailMainMenuLabelDest.DestYCyLoadSite);
        Assert.Equal(0x00463093u, RetailMainMenuLabelDest.DestYSarSite);
        Assert.Equal(0x0046309Du, RetailMainMenuLabelDest.DestYFsubrSite);
        Assert.Equal(0x004630A1u, RetailMainMenuLabelDest.DestYStoreSite);
        Assert.Equal(
            RetailMainMenuLabelText.GetTextExtentSite,
            RetailMainMenuLabelDest.GetTextExtentSite);
        Assert.Equal(
            RetailMainMenuLabelText.GetTextExtent,
            RetailMainMenuLabelDest.GetTextExtent);
        Assert.Equal(
            RetailMainMenuLabelText.DestXGlobal,
            RetailMainMenuLabelDest.DestXGlobal);
        Assert.Equal(
            RetailMainMenuLabelText.DestXGlobalBits,
            RetailMainMenuLabelDest.DestXGlobalBits);
        Assert.Equal(
            RetailMainMenuSelectorBarZ.DestX,
            RetailMainMenuLabelDest.DestXAnchor);
        Assert.Equal(
            RetailClickToStartGlyphs.HalfWidth,
            RetailMainMenuLabelDest.Half);
        Assert.NotEqual(
            RetailMainMenuLabelText.CallSite,
            RetailMainMenuLabelDest.GetTextExtentSite);
        Assert.False(RetailMainMenuLabelDest.InventsDestImmediates);
        Assert.False(RetailMainMenuLabelDest.InventsKerningHack);
        Assert.False(RetailMainMenuLabelDest.InventsSheen);
        Assert.False(RetailMainMenuLabelDest.InventsWrapWidth);
        Assert.False(RetailMainMenuLabelDest.InventsFade);
        Assert.False(RetailMainMenuLabelDest.InventsFloatHalfDestY);
        Assert.False(RetailMainMenuLabelDest.IsSetLanguage);
        Assert.False(RetailMainMenuLabelDest.IsButtonPressed);
        Assert.False(RetailMainMenuLabelDest.RedoesLabelText);
        Assert.False(RetailMainMenuLabelDest.RedoesVersionOverlayTail);
        Assert.False(RetailMainMenuLabelDest.RedoesWritingZ);
        Assert.False(RetailMainMenuLabelDest.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuLabelDest.UsesTwinFadeGate);
        Assert.False(RetailMainMenuLabelDest.ChangesMeasureText);
    }

    [Fact]
    public void DestIsAnchorMinusFloatHalfCxAndRowYMinusIntegerHalfCy()
    {
        Assert.Equal(219f, RetailMainMenuLabelDest.DestXAnchor);
        Assert.Equal(0.5f, RetailMainMenuLabelDest.Half);
        Assert.Equal(0, RetailMainMenuLabelDest.IntegerHalf(0));
        Assert.Equal(8, RetailMainMenuLabelDest.IntegerHalf(16));
        Assert.Equal(8, RetailMainMenuLabelDest.IntegerHalf(17));
        Assert.Equal(7, RetailMainMenuLabelDest.IntegerHalf(15));
        Assert.Equal(219f, RetailMainMenuLabelDest.DestX(0f));
        Assert.Equal(179f, RetailMainMenuLabelDest.DestX(80f));
        Assert.Equal(296f, RetailMainMenuLabelDest.DestY(304f, 16));
        Assert.Equal(260f, RetailMainMenuLabelDest.DestY(268f, 16));
        Assert.Equal(296f, RetailMainMenuLabelDest.DestY(304f, 17));
        Assert.Equal(297f, RetailMainMenuLabelDest.DestY(304f, 15));
        Assert.NotEqual(304f - (17f * 0.5f), RetailMainMenuLabelDest.DestY(304f, 17));
        Assert.NotEqual(219f, RetailMainMenuLabelDest.DestX(80f));
        Assert.NotEqual(304f, RetailMainMenuLabelDest.DestY(304f, 16));
        Assert.Equal(
            RetailMainMenuLabelText.DestXAnchor,
            RetailMainMenuLabelDest.DestXAnchor);
        Assert.False(RetailMainMenuLabelDest.InventsDestImmediates);
        Assert.False(RetailMainMenuLabelDest.InventsFloatHalfDestY);
        Assert.False(RetailMainMenuLabelDest.InventsKerningHack);
        Assert.False(RetailMainMenuLabelDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawMainMenuConsumesDestXFromMeasureTextAndDoesNotInventDestOrKerning()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuLabelDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelDest.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailMainMenuLabelColor.SubmittedColor", draw, StringComparison.Ordinal);
        Assert.Contains("MeasureText", draw, StringComparison.Ordinal);
        Assert.Contains("DrawText(", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailMainMenuLabelDest.DestY", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0.32", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("1000.0", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x447A0000", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("0x3EA3D70A", draw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("42f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain(" - 2", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerMotion", draw, StringComparison.Ordinal);

        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailMainMenuLabelDest", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuLabelDest", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuLabelDest", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuLabelDest", bar, StringComparison.Ordinal);
        string language = Slice(flow, "private void DrawLanguageSelector");
        Assert.DoesNotContain("RetailMainMenuLabelDest", language, StringComparison.Ordinal);
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
