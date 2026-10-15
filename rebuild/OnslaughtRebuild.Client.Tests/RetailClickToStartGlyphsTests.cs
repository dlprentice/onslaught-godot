// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPIntro "Click to start" glyph submits recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle).
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051B92F</c>–<c>0x0051BAC2</c>.
/// File offset = VA − <c>0x400000</c>. <c>push 0x77</c> then
/// <c>Localization__GetStringById</c> (<c>0x00524830</c>),
/// <c>CPlatform__Font</c> slot 0 (<c>0x00515A70</c>),
/// <c>CDXFont__GetTextExtent</c> (<c>0x00540680</c>), then five
/// <c>CDXFont__DrawTextScaled</c> (<c>0x00540010</c>) calls. Not
/// <c>CText__GetStringById</c>. Not attract <c>vectorlosttoyssplash</c>.
/// Not TWIMTBP.</para>
///
/// <para>What is asserted is the LAW, not Godot pixels. The previous
/// <c>DrawClickToStart</c> prompt used four ±1 outlines + a white body at
/// <c>y=400</c> with <c>textScale = 2</c> from a capture. These cases pin
/// the specimen Y immediates 401 / 399 / 400, <c>sx=sy=1</c>, z bits
/// <c>0x3DCCCCCD</c>, and <c>X = 320 − width×0.5 + dx</c>.
/// <c>DrawClickToStart</c> must call the recovered glyphs rather than keep
/// that reconstruction copy.</para>
/// </summary>
public sealed class RetailClickToStartGlyphsTests
{
    [Fact]
    public void PromptLooksUpLocalizationSeventySevenOnFontSlotZero()
    {
        // 0x0051B92F push 0x77; 0x0051B931 call Localization__GetStringById.
        // 0x0051B941 push 0; mov ecx, 0x0088A0A8; call CPlatform__Font.
        Assert.Equal(0x77, RetailClickToStartGlyphs.LocalizationId);
        Assert.Equal(0, RetailClickToStartGlyphs.FontSlot);
    }

    [Fact]
    public void FivePassesAreThePlusOrMinusOneOutlineThenTheBody()
    {
        // Y immediates 0x43C88000 / 0x43C78000 / 0x43C80000.
        // Outline colour push 0xFF000000; body push -1.
        // dx from fsub / fadd [1.0f] at 0x005D8568 around 320 - width*0.5.
        Assert.Equal(5, RetailClickToStartGlyphs.Passes.Length);
        Assert.Equal(
            new RetailClickToStartGlyphs.Pass(-1f, 401f, 0xFF000000u),
            RetailClickToStartGlyphs.Passes[0]);
        Assert.Equal(
            new RetailClickToStartGlyphs.Pass(1f, 401f, 0xFF000000u),
            RetailClickToStartGlyphs.Passes[1]);
        Assert.Equal(
            new RetailClickToStartGlyphs.Pass(-1f, 399f, 0xFF000000u),
            RetailClickToStartGlyphs.Passes[2]);
        Assert.Equal(
            new RetailClickToStartGlyphs.Pass(1f, 399f, 0xFF000000u),
            RetailClickToStartGlyphs.Passes[3]);
        Assert.Equal(
            new RetailClickToStartGlyphs.Pass(0f, 400f, 0xFFFFFFFFu),
            RetailClickToStartGlyphs.Passes[4]);
        Assert.Equal(0xFF000000u, RetailClickToStartGlyphs.OutlineColor);
        Assert.Equal(0xFFFFFFFFu, RetailClickToStartGlyphs.BodyColor);
    }

    [Fact]
    public void XIsCentreMinusHalfExtentPlusDxNotAScaleTwoMeasure()
    {
        // fild [esp+8] (GetTextExtent width); fmul [0.5] at 0x005D85EC;
        // fsubr [320.0] at 0x005DB3E8; then fsub/fadd [1.0] or nothing.
        Assert.Equal(320f, RetailClickToStartGlyphs.CentreX);
        Assert.Equal(0.5f, RetailClickToStartGlyphs.HalfWidth);
        Assert.Equal(1f, RetailClickToStartGlyphs.ScaleX);
        Assert.Equal(1f, RetailClickToStartGlyphs.ScaleY);
        Assert.Equal(0x3DCCCCCDu, RetailClickToStartGlyphs.ZBits);
        Assert.Equal(
            0.1f,
            BitConverter.UInt32BitsToSingle(RetailClickToStartGlyphs.ZBits));

        Assert.Equal(269f, RetailClickToStartGlyphs.X(RetailClickToStartGlyphs.Passes[0], 100));
        Assert.Equal(271f, RetailClickToStartGlyphs.X(RetailClickToStartGlyphs.Passes[1], 100));
        Assert.Equal(270f, RetailClickToStartGlyphs.X(RetailClickToStartGlyphs.Passes[4], 100));

        // A scale-2 measure centred as 320 - (2*width)*0.5 is 220 at width 100.
        Assert.NotEqual(220f, RetailClickToStartGlyphs.X(RetailClickToStartGlyphs.Passes[4], 100));
        Assert.NotEqual(2f, RetailClickToStartGlyphs.ScaleX);
    }

    [Fact]
    public void GlyphsShareTheRecoveredPromptBlinkGate()
    {
        // The five submits sit inside the same timer>4 / fmod<2 arm
        // already pinned by RetailClickToStartPrompt.
        Assert.False(RetailClickToStartGlyphs.ShouldDraw(0d));
        Assert.False(RetailClickToStartGlyphs.ShouldDraw(4.0d));
        Assert.True(RetailClickToStartGlyphs.ShouldDraw(4.0d + 1e-6));
        Assert.True(RetailClickToStartGlyphs.ShouldDraw(5.7d));
        Assert.False(RetailClickToStartGlyphs.ShouldDraw(6.0d));
    }

    [Fact]
    public void DrawClickToStartCallsTheRecoveredGlyphsInsteadOfTheScaleTwoCopy()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        Assert.Contains("RetailClickToStartGlyphs.ShouldDraw", flow);
        Assert.Contains("RetailClickToStartGlyphs.X", flow);
        Assert.Contains("RetailClickToStartGlyphs.Passes", flow);
        Assert.DoesNotContain("const float textScale = 2f;", flow);
        Assert.DoesNotContain("new Vector2(320f - (width * 0.5f), 400f)", flow);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);
    }
}
