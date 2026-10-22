// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPIntro LostToys slide recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle).
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051BAC2</c>–<c>0x0051BB9D</c>.
/// File offset = VA − <c>0x400000</c>. Two <c>CDXSurf__RenderSurface</c>
/// (<c>0x005563D0</c>) calls on <c>0x0089d7bc</c> (<c>FrontEnd\LostToys.tga</c>).
/// Not attract <c>vectorlosttoyssplash</c>. Not TWIMTBP.</para>
///
/// <para>What is asserted is the LAW, not Godot pixels. These cases pin
/// the specimen clamp / square / 400 travel and refuse a linear travel.
/// <c>DrawClickToStart</c> must call the recovered slide rather than keep
/// the reconstruction fade, offset, and rects.</para>
/// </summary>
public sealed class RetailClickToStartSlideTests
{
    [Fact]
    public void FadeClampsTimerMinusFourToTheClosedUnitInterval()
    {
        // 0x0051BAD0 fld [this+0x18]; 0x0051BAD3 fsub [4.0f] at 0x005D85BC;
        // 0x0051BAD9 fcom [0.0f] / test ah,1 / je keep; 0x0051BAF0 fcom [1.0f]
        // / test ah,0x41 / jne keep.
        Assert.Equal(4.0, RetailClickToStartSlide.GateSeconds, 5);
        Assert.Equal(0f, RetailClickToStartSlide.Fade(0d));
        Assert.Equal(0f, RetailClickToStartSlide.Fade(4.0d));
        Assert.Equal(0.25f, RetailClickToStartSlide.Fade(4.25d), 5);
        Assert.Equal(0.5f, RetailClickToStartSlide.Fade(4.5d), 5);
        Assert.Equal(1f, RetailClickToStartSlide.Fade(5.0d));
        Assert.Equal(1f, RetailClickToStartSlide.Fade(10.0d));
    }

    [Fact]
    public void OffsetIsTheSquaredRemainderTimesFourHundredNotALinearTravel()
    {
        // 0x0051BB05 fsubr [1.0f]; 0x0051BB0D fmul st(1);
        // 0x0051BB1D fmul [400.0f] at 0x005DB358.
        Assert.Equal(400f, RetailClickToStartSlide.TravelPixels);
        Assert.Equal(400f, RetailClickToStartSlide.Offset(0d));
        Assert.Equal(400f, RetailClickToStartSlide.Offset(4.0d));
        Assert.Equal(225f, RetailClickToStartSlide.Offset(4.25d), 5);
        Assert.Equal(100f, RetailClickToStartSlide.Offset(4.5d), 5);
        Assert.Equal(0f, RetailClickToStartSlide.Offset(5.0d));
        Assert.Equal(0f, RetailClickToStartSlide.Offset(12.0d));

        // Linear (1-fade)*400 would be 200 at timer 4.5.
        Assert.NotEqual(200f, RetailClickToStartSlide.Offset(4.5d));
    }

    [Fact]
    public void BothPassesIssueEvenWhileTheTimerIsAtOrBelowTheGate()
    {
        // No jne around the pair. timer<=4 still submits, 400 px off-screen.
        Assert.True(RetailClickToStartSlide.ShouldDraw(0d));
        Assert.True(RetailClickToStartSlide.ShouldDraw(4.0d));
        Assert.True(RetailClickToStartSlide.ShouldDraw(4.0d + 1e-6));
        Assert.True(RetailClickToStartSlide.ShouldDraw(30d));
        Assert.Equal(-276f, RetailClickToStartSlide.X(RetailClickToStartSlide.Passes[0], 0d), 5);
        Assert.Equal(-280f, RetailClickToStartSlide.X(RetailClickToStartSlide.Passes[1], 0d), 5);
    }

    [Fact]
    public void TwoPassesAreTheShadowThenTheBodyAtTheSpecimenAnchors()
    {
        Assert.Equal(2, RetailClickToStartSlide.Passes.Length);
        Assert.Equal(
            new RetailClickToStartSlide.Pass(124f, -6f, 0x3DCED917u, 0x3F000000u),
            RetailClickToStartSlide.Passes[0]);
        Assert.Equal(
            new RetailClickToStartSlide.Pass(120f, -10f, 0x3DCCCCCDu, 0xFFFFFFFFu),
            RetailClickToStartSlide.Passes[1]);
        Assert.Equal(0x3F000000u, RetailClickToStartSlide.ShadowColor);
        Assert.Equal(0xFFFFFFFFu, RetailClickToStartSlide.BodyColor);
        Assert.Equal(
            0x3DCED917u,
            (uint)BitConverter.SingleToUInt32Bits(RetailClickToStartSlide.Passes[0].Z));
        Assert.Equal(
            0x3DCCCCCDu,
            (uint)BitConverter.SingleToUInt32Bits(RetailClickToStartSlide.Passes[1].Z));
        Assert.Equal(124f, RetailClickToStartSlide.X(RetailClickToStartSlide.Passes[0], 5.0d));
        Assert.Equal(120f, RetailClickToStartSlide.X(RetailClickToStartSlide.Passes[1], 5.0d));
    }

    [Fact]
    public void DrawClickToStartCallsTheRecoveredSlideInsteadOfTheReconstructionCopy()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        Assert.Contains("RetailClickToStartSlide.ShouldDraw", flow);
        Assert.Contains("RetailClickToStartSlide.X", flow);
        Assert.Contains("RetailClickToStartSlide.Passes", flow);
        Assert.Contains("0x0089d7bc", flow);
        Assert.DoesNotContain("(1f - fade) * (1f - fade) * 400f", flow);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);
    }
}
