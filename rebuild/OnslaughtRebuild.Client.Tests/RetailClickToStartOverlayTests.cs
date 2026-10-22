// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPIntro::Render tail after the sixth title pass, recovered
/// from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>0x0051BD8C</c>–<c>0x0051BE66</c> (<c>RET 8</c>).
/// After <c>ADD ESP, 0x2C</c> / <c>JMP</c> past the sixth-pass <c>FSTP</c>,
/// Render loads <c>0x0089BB68</c> at <c>0x0051BD93</c> and
/// <c>JE 0x0051BE5E</c> to the epilogue when the dword is 0. The 10th
/// <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) and the font-1
/// <c>CDXFont__DrawText</c> walk are inside that gate. They are not a
/// seventh title pass and they are not Infogrames <c>+0x124</c>.</para>
///
/// <para>The three globals live in uninitialised <c>.data</c> (image-initial
/// 0). Operand scan of the specimen finds no store of those immediates.
/// The mutations these cases kill are an always-on black splash, a sixth
/// entry in <c>RetailClickToStartTitle.Passes</c>, and a fade invented for
/// the leftover 0xDA bytes.</para>
/// </summary>
public sealed class RetailClickToStartOverlayTests
{
    [Fact]
    public void ImageInitialFlagSkipsTheEntireTail()
    {
        // 0x0051BD93 mov eax,[0x0089BB68]; test eax,eax / je 0x0051BE5E.
        // pe_read_va: VA is in the uninitialised part of .data.
        Assert.Equal(0x0089BB68u, RetailClickToStartOverlay.FlagGlobal);
        Assert.Equal(0u, RetailClickToStartOverlay.ImageInitialFlag);
        Assert.False(RetailClickToStartOverlay.ShouldDraw(0u));
        Assert.True(RetailClickToStartOverlay.ShouldDraw(1u));
        Assert.True(RetailClickToStartOverlay.ShouldDraw(0xFFFFFFFFu));
    }

    [Fact]
    public void BackdropIsModeZeroBlackSplashAtOriginScaleTen()
    {
        // Pushes at 0x0051BDA6–0x0051BDC5 then call 0x005563D0.
        // Texture is 0x0089D880 (same as the pulse splash), not title-logo.
        // 0x41200000 = 10.0; colour 0xFF000000; mode 0; x=y=z=0.
        Assert.Equal(RetailClickToStartSplash.TextureGlobal, RetailClickToStartOverlay.BackdropTextureGlobal);
        Assert.Equal(0, RetailClickToStartOverlay.BackdropMode);
        Assert.Equal(0xFF000000u, RetailClickToStartOverlay.BackdropColor);
        Assert.Equal(10f, RetailClickToStartOverlay.BackdropScale);
        Assert.Equal(0f, RetailClickToStartOverlay.BackdropX);
        Assert.Equal(0f, RetailClickToStartOverlay.BackdropY);
        Assert.Equal(0u, RetailClickToStartOverlay.BackdropZBits);
        Assert.NotEqual(RetailClickToStartOverlay.BackdropMode, RetailClickToStartSplash.Mode);
        Assert.NotEqual(RetailClickToStartOverlay.BackdropX, RetailClickToStartTitle.SixthPass.X);
    }

    [Fact]
    public void TextWalkUsesFontSlotOneAtSixtyFourAndSixteenPixelLines()
    {
        // 0x0051BDD8 first-byte test; 0x0051BE33 push 0x42800000 (64);
        // 0x0051BE38 push 1 then CPlatform__Font 0x00515A70;
        // 0x0051BE46 CDXFont__DrawText 0x00540640; 0x0051BE4F fadd [0x005D8BC0]=16.
        // Y0 is fld [0x005DBB64]=64 then fsub [0x0089BB6C].
        Assert.Equal(0x00897B28u, RetailClickToStartOverlay.TextGlobal);
        Assert.Equal(0x0089BB6Cu, RetailClickToStartOverlay.ScrollGlobal);
        Assert.Equal(1, RetailClickToStartOverlay.FontSlot);
        Assert.NotEqual(RetailClickToStartOverlay.FontSlot, RetailClickToStartGlyphs.FontSlot);
        Assert.Equal(64f, RetailClickToStartOverlay.TextX);
        Assert.Equal(64f, RetailClickToStartOverlay.TextOriginY);
        Assert.Equal(16f, RetailClickToStartOverlay.LineStep);
        Assert.Equal(0xFFFFFFFFu, RetailClickToStartOverlay.TextColor);
        Assert.Equal(64f, RetailClickToStartOverlay.TextY(0f));
        Assert.Equal(48f, RetailClickToStartOverlay.TextY(16f));
        Assert.False(RetailClickToStartOverlay.ShouldDrawText(0u, 0x41));
        Assert.False(RetailClickToStartOverlay.ShouldDrawText(1u, 0));
        Assert.True(RetailClickToStartOverlay.ShouldDrawText(1u, 0x41));
    }

    [Fact]
    public void AsciiLinesSplitOnNewlineAndSkipALeadingLineFeed()
    {
        // 0x0051BDED–0x0051BE5C: per line, skip one leading 0x0A, copy until
        // 0x0A or NUL widening each byte to wchar, DrawText, then loop while
        // [esi] != 0. Image-initial first byte is 0 so the walk never starts.
        Assert.Empty(RetailClickToStartOverlay.Lines(""));
        Assert.Equal(new[] { "A", "B" }, RetailClickToStartOverlay.Lines("A\nB"));
        Assert.Equal(new[] { "A", "B" }, RetailClickToStartOverlay.Lines("\nA\nB"));
        Assert.Equal(new[] { "A", "", "B" }, RetailClickToStartOverlay.Lines("A\n\nB"));
        Assert.Equal(new[] { "OK" }, RetailClickToStartOverlay.Lines("OK"));
        Assert.Equal(new[] { "" }, RetailClickToStartOverlay.Lines("\n"));
        Assert.Equal(new[] { "A", "" }, RetailClickToStartOverlay.Lines("A\n"));
    }

    [Fact]
    public void TitlePassesStayFiveAndDrawClickToStartDoesNotInventTheColdOverlay()
    {
        // The tail is a second gate on 0x0089BB68, not page*1.2>2 and not
        // 2<page<2.25. Passes.Length stays 5. Image-initial flag is 0, so
        // DrawClickToStart must not emit the mode-0 scale-10 black splash.
        Assert.Equal(5, RetailClickToStartTitle.Passes.Length);
        Assert.False(RetailClickToStartOverlay.ShouldDraw(RetailClickToStartOverlay.ImageInitialFlag));

        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        int start = flow.IndexOf("private void DrawClickToStart()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        string body = flow[start..];
        int next = body.IndexOf("\n    private ", 1, StringComparison.Ordinal);
        if (next >= 0)
        {
            body = body[..next];
        }

        Assert.Contains("RetailClickToStartTitle.ShouldDrawSixth", body);
        Assert.DoesNotContain("BackdropScale", body);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);
        Assert.DoesNotContain("fe_infogrames", body);
    }
}
