// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPIntro splash dest and <c>CDXSurf__RenderSurface</c> argument
/// pack recovered from the pristine specimen
/// (a private pristine copy), SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle).
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051B866</c>–<c>0x0051B8F4</c>
/// then a stack cleanup of 0x2C bytes at <c>0x0051B902</c>. File offset = VA −
/// <c>0x400000</c>. One <c>CDXSurf__RenderSurface</c> (<c>0x005563D0</c>) call
/// on <c>0x0089d880</c> (<c>FrontEnd\v2\fe_splash1.tga</c>). Not attract
/// <c>vectorlosttoyssplash</c>. Not TWIMTBP.</para>
///
/// <para>What is asserted is the LAW, not Godot pixels. These cases pin
/// the specimen float-pool VAs, the right-to-left 11-dword pack, and
/// refuse a scale-free (320, 240) dest. <c>DrawClickToStart</c> must
/// call the recovered dest rather than keep an unlabeled copy.</para>
/// </summary>
public sealed class RetailClickToStartSplashTests
{
    [Fact]
    public void DestUsesTheSpecimenAffineNotAFixedCentre()
    {
        // Y: a float multiply by the value at 0x005E49EC=-222; a reversed float subtract of
        // the value at 0x005DBE00=18; a float subtract of the value at 0x005E49E8=-117.9375.
        // X: a float multiply by the value at 0x005E49E4=238; a reversed float subtract of
        // the value at 0x005E49E0=558; a float subtract of the value at 0x005E49DC=126.4375.
        Assert.Equal(558f, RetailClickToStartSplash.BaseX);
        Assert.Equal(238f, RetailClickToStartSplash.CoeffX);
        Assert.Equal(126.4375f, RetailClickToStartSplash.OffsetX);
        Assert.Equal(18f, RetailClickToStartSplash.BaseY);
        Assert.Equal(-222f, RetailClickToStartSplash.CoeffY);
        Assert.Equal(-117.9375f, RetailClickToStartSplash.OffsetY);

        // Settled SplashScale (timer >= 1) is 0.46875 → (320, 240).
        Assert.Equal(320f, RetailClickToStartSplash.X(1d), 5);
        Assert.Equal(240f, RetailClickToStartSplash.Y(1d), 5);
        Assert.Equal(RetailClickToStartSplash.X(1d), RetailClickToStartSplash.X(2d), 5);
        Assert.Equal(RetailClickToStartSplash.Y(1d), RetailClickToStartSplash.Y(2d), 5);

        // t=0 scale is 1.21875. A scale-free centre would still be (320, 240).
        Assert.Equal(141.5f, RetailClickToStartSplash.X(0d), 5);
        Assert.Equal(406.5f, RetailClickToStartSplash.Y(0d), 5);
        Assert.NotEqual(320f, RetailClickToStartSplash.X(0d));
        Assert.NotEqual(240f, RetailClickToStartSplash.Y(0d));
    }

    [Fact]
    public void ArgumentPackIsModeFourWithMatchingScalesAndTheSpecimenZ()
    {
        // Pushes at 0x0051B884..0x0051B8CB then fstp Y / fstp X / a direct call to 0x005563D0.
        // A stack cleanup of 0x2C bytes at 0x0051B902 is eleven cdecl dwords.
        Assert.Equal(0x0089D880u, RetailClickToStartSplash.TextureGlobal);
        Assert.Equal(4, RetailClickToStartSplash.Mode);
        Assert.Equal(0xFFFFFFFFu, RetailClickToStartSplash.Color);
        Assert.Equal(0x3F75C28Fu, RetailClickToStartSplash.ZBits);
        Assert.Equal(0f, RetailClickToStartSplash.TrailingA);
        Assert.Equal(1f, RetailClickToStartSplash.TrailingB);
        Assert.Equal(0f, RetailClickToStartSplash.TrailingC);
        Assert.Equal(
            0x3F75C28Fu,
            (uint)BitConverter.SingleToUInt32Bits(RetailClickToStartSplash.Z));

        Assert.Equal(
            RetailClickToStartPrompt.SplashScale(0d),
            RetailClickToStartSplash.Scale(0d));
        Assert.Equal(
            RetailClickToStartPrompt.SplashScale(1d),
            RetailClickToStartSplash.Scale(1d));
        Assert.Equal(
            RetailClickToStartSplash.Scale(1d),
            RetailClickToStartSplash.Scale(2d));
        Assert.NotEqual(
            RetailClickToStartSplash.Scale(0d),
            RetailClickToStartSplash.Scale(1d));
    }

    [Fact]
    public void SplashSubmitsOnTheActivePageEvenBeforeThePromptGate()
    {
        // No jne around the 0x0089d880 call. The only skip is the
        // page-transition==1.0 compare at 0x0051B851, which is not this helper.
        Assert.True(RetailClickToStartSplash.ShouldDraw(0d));
        Assert.True(RetailClickToStartSplash.ShouldDraw(1d));
        Assert.True(RetailClickToStartSplash.ShouldDraw(4d));
        Assert.True(RetailClickToStartSplash.ShouldDraw(30d));
    }

    [Fact]
    public void DrawClickToStartCallsTheRecoveredSplashDestInsteadOfTheUnlabeledCopy()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        Assert.Contains("RetailClickToStartSplash.X", flow);
        Assert.Contains("RetailClickToStartSplash.Y", flow);
        Assert.Contains("RetailClickToStartSplash.Scale", flow);
        Assert.DoesNotContain("(558f - (splashScale * 238f)) - 126.4375f", flow);
        Assert.DoesNotContain("135.9375f + (222f * splashScale)", flow);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);
    }
}
