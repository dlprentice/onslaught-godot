// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render version overlay leftover stack
/// 0 / 0 / <c>0x447A0000</c> as CDXFont__DrawTextDynamic's last
/// three stack args, recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>RetailMainMenuVersionOverlayWiden already owns the cdecl
/// <c>add esp, 4</c> and does not own these three pushes.
/// RetailMainMenuVersionOverlay already owns the sprintf and the
/// settled pack. RetailMainMenuVersionOverlayZ already owns
/// GetWindowHeight-16 dest Y, dest X push 0, and Z 0.01.
/// RetailMainMenuVersionOverlayFont already owns push 1 /
/// FONT_SMALL / Font13PS. RetailMainMenuVersionOverlayFlags already
/// owns the post-draw restore. RetailMainMenuVersionOverlayEnable
/// already owns the pre-draw store. Official bytes independently
/// re-read this cycle: after the enable-byte store,
/// <c>0x00464187</c> / <c>0x00464189</c> / <c>0x0046418B</c> push
/// 0 / 0 / <c>0x447A0000</c>. Those three dwords remain after
/// <c>add esp, 4</c> and become stack args 10 / 9 / 8 of
/// <c>CDXFont__DrawTextDynamic</c> (<c>RET 0x28</c>). The body
/// loads them at <c>[esp+0x2758]</c> / <c>[esp+0x2754]</c> /
/// <c>fld [esp+0x2750]</c>. The leftover float is past the
/// below-zero / below-quarter / below-half compares, and leftover
/// arg 9 is 0 so that colour arm is skipped. The wchar clamp
/// <c>cmp ebx, 0x3E8</c> starts at <c>0x00465771</c>
/// (<c>81 fb e8 03 00 00</c>); <c>0x00465777</c> is the separate
/// following <c>mov word [eax], 0</c> store
/// (<c>66 c7 00 00 00</c>). Neither site is this leftover. The
/// 2px MeasureText residual stays open. DrawMainMenu keeps
/// title-font DrawText. Do not invent dest, wrap, fade, or sheen.
/// Do not redo version AsciiToWide, version dest/Z, version font
/// slot, version post-draw flags, version pre-draw enable,
/// title-logo dest/Z, title-logo shadow dest/Z, selector-bar Z/X,
/// writing Z/X, 0x00463873, 0x004638B7, 0x00463A8F, 0x00463AD3,
/// 0x00463D1F, 0x00463D63, 0x00463F3F, or 0x00463F83.</para>
/// </summary>
public sealed class RetailMainMenuVersionOverlayTailTests
{
    [Fact]
    public void SpecimenSitesAreDrawTextDynamicTailSlotsNotAFade()
    {
        Assert.Equal(0x00464180u, RetailMainMenuVersionOverlayTail.EnableSiblingSite);
        Assert.Equal(0x00464187u, RetailMainMenuVersionOverlayTail.FirstLeftoverPushSite);
        Assert.Equal(0x00464189u, RetailMainMenuVersionOverlayTail.SecondLeftoverPushSite);
        Assert.Equal(0x0046418Bu, RetailMainMenuVersionOverlayTail.FloatLeftoverPushSite);
        Assert.Equal(0x447A0000u, RetailMainMenuVersionOverlayTail.FloatSlotBits);
        Assert.Equal(0x004641A0u, RetailMainMenuVersionOverlayTail.WidenAddEspSite);
        Assert.Equal(4, RetailMainMenuVersionOverlayTail.WidenAddEspImmediate);
        Assert.Equal(0x004641EDu, RetailMainMenuVersionOverlayTail.CallSite);
        Assert.Equal(0x00465710u, RetailMainMenuVersionOverlayTail.DrawTextDynamic);
        Assert.Equal(0x00465997u, RetailMainMenuVersionOverlayTail.BodyRetSite);
        Assert.Equal(0x28, RetailMainMenuVersionOverlayTail.BodyRetImmediate);
        Assert.Equal(10, RetailMainMenuVersionOverlayTail.StackArgCount);
        Assert.Equal(0x2720, RetailMainMenuVersionOverlayTail.AllocaSize);
        Assert.Equal(0x10, RetailMainMenuVersionOverlayTail.RegisterPushBytes);
        Assert.Equal(8, RetailMainMenuVersionOverlayTail.FloatStackArg);
        Assert.Equal(9, RetailMainMenuVersionOverlayTail.SecondStackArg);
        Assert.Equal(10, RetailMainMenuVersionOverlayTail.FirstStackArg);
        Assert.Equal(0x0046578Fu, RetailMainMenuVersionOverlayTail.Arg8LoadSite);
        Assert.Equal(0x2750, RetailMainMenuVersionOverlayTail.Arg8Disp);
        Assert.Equal(0x0046587Au, RetailMainMenuVersionOverlayTail.Arg9LoadSite);
        Assert.Equal(0x2754, RetailMainMenuVersionOverlayTail.Arg9Disp);
        Assert.Equal(0x00465902u, RetailMainMenuVersionOverlayTail.Arg10LoadSite);
        Assert.Equal(0x2758, RetailMainMenuVersionOverlayTail.Arg10Disp);
        Assert.Equal(0x00465796u, RetailMainMenuVersionOverlayTail.BelowZeroCompareSite);
        Assert.Equal(0x005D856Cu, RetailMainMenuVersionOverlayTail.BelowZeroGlobal);
        Assert.Equal(0x00000000u, RetailMainMenuVersionOverlayTail.BelowZeroBits);
        Assert.Equal(0x004657A7u, RetailMainMenuVersionOverlayTail.BelowQuarterCompareSite);
        Assert.Equal(0x005D858Cu, RetailMainMenuVersionOverlayTail.BelowQuarterGlobal);
        Assert.Equal(0x3E800000u, RetailMainMenuVersionOverlayTail.BelowQuarterBits);
        Assert.Equal(0x004657DDu, RetailMainMenuVersionOverlayTail.BelowHalfCompareSite);
        Assert.Equal(0x005D85ECu, RetailMainMenuVersionOverlayTail.BelowHalfGlobal);
        Assert.Equal(0x3F000000u, RetailMainMenuVersionOverlayTail.BelowHalfBits);
        Assert.Equal(0x00465881u, RetailMainMenuVersionOverlayTail.Arg9TestSite);
        Assert.Equal(0, RetailMainMenuVersionOverlayTail.SecondSlot);
        Assert.Equal(0, RetailMainMenuVersionOverlayTail.FirstSlot);
        Assert.Equal(0x00465771u, RetailMainMenuVersionOverlayTail.LengthClampSite);
        Assert.Equal(0x00465777u, RetailMainMenuVersionOverlayTail.LengthClampStoreSite);
        Assert.Equal(0x3E8, RetailMainMenuVersionOverlayTail.LengthClampImmediate);
        Assert.Equal(0x00464191u, RetailMainMenuVersionOverlayTail.WidenSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayEnable.StoreSite,
            RetailMainMenuVersionOverlayTail.EnableSiblingSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayWiden.AddEspSite,
            RetailMainMenuVersionOverlayTail.WidenAddEspSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayWiden.FirstLeftoverPushSite,
            RetailMainMenuVersionOverlayTail.FirstLeftoverPushSite);
        Assert.Equal(
            RetailMainMenuVersionOverlayWiden.FloatLeftoverBits,
            RetailMainMenuVersionOverlayTail.FloatSlotBits);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.DrawTextDynamic,
            RetailMainMenuVersionOverlayTail.DrawTextDynamic);
        Assert.Equal(
            RetailMainMenuVersionOverlayZ.CallSite,
            RetailMainMenuVersionOverlayTail.CallSite);
        Assert.False(RetailMainMenuVersionOverlayWiden.OwnsLeftoverPushes);
        Assert.True(RetailMainMenuVersionOverlayTail.OwnsLeftoverPushes);
        Assert.False(RetailMainMenuVersionOverlayTail.OwnsWidenCall);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsKerningHack);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsWrapWidth);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsSheen);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsLengthClampAsWrap);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsTitleBarZFlag);
        Assert.False(RetailMainMenuVersionOverlayTail.IsSetLanguage);
        Assert.False(RetailMainMenuVersionOverlayTail.IsButtonPressed);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlay);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlayZ);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlayFont);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlayFlags);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlayEnable);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesVersionOverlayWiden);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesTitleLogoZ);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesTitleLogoShadowZ);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesSelectorBarZ);
        Assert.False(RetailMainMenuVersionOverlayTail.RedoesWritingZ);
        Assert.False(RetailMainMenuVersionOverlayTail.UsesTwinFadeGate);
    }

    [Fact]
    public void LeftoverFloatSkipsBelowZeroQuarterAndHalfArms()
    {
        Assert.Equal(1000f, RetailMainMenuVersionOverlayTail.FloatSlot);
        Assert.Equal(0f, RetailMainMenuVersionOverlayTail.BelowZero);
        Assert.Equal(0.25f, RetailMainMenuVersionOverlayTail.BelowQuarter);
        Assert.Equal(0.5f, RetailMainMenuVersionOverlayTail.BelowHalf);
        Assert.Equal(
            RetailMainMenuVersionOverlayTail.Arg8Disp,
            RetailMainMenuVersionOverlayTail.StackDispAfterFrame(
                RetailMainMenuVersionOverlayTail.FloatStackArg));
        Assert.Equal(
            RetailMainMenuVersionOverlayTail.Arg9Disp,
            RetailMainMenuVersionOverlayTail.StackDispAfterFrame(
                RetailMainMenuVersionOverlayTail.SecondStackArg));
        Assert.Equal(
            RetailMainMenuVersionOverlayTail.Arg10Disp,
            RetailMainMenuVersionOverlayTail.StackDispAfterFrame(
                RetailMainMenuVersionOverlayTail.FirstStackArg));
        Assert.Equal(
            RetailMainMenuVersionOverlayTail.BodyRetImmediate,
            RetailMainMenuVersionOverlayTail.StackArgCount * 4);
        Assert.False(
            RetailMainMenuVersionOverlayTail.TakesBelowZeroArm(
                RetailMainMenuVersionOverlayTail.FloatSlot));
        Assert.False(
            RetailMainMenuVersionOverlayTail.TakesBelowQuarterArm(
                RetailMainMenuVersionOverlayTail.FloatSlot));
        Assert.False(
            RetailMainMenuVersionOverlayTail.TakesBelowHalfArm(
                RetailMainMenuVersionOverlayTail.FloatSlot));
        Assert.True(
            RetailMainMenuVersionOverlayTail.SkipsArg9Arm(
                RetailMainMenuVersionOverlayTail.SecondSlot));
        Assert.NotEqual(
            RetailMainMenuVersionOverlayTail.LengthClampImmediate,
            unchecked((int)RetailMainMenuVersionOverlayTail.FloatSlotBits));
        Assert.NotEqual(
            RetailMainMenuVersionOverlayTail.LengthClampSite,
            RetailMainMenuVersionOverlayTail.LengthClampStoreSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.FirstLeftoverPushSite <
            RetailMainMenuVersionOverlayWiden.CallSite);
        Assert.True(
            RetailMainMenuVersionOverlayWiden.AddEspSite <
            RetailMainMenuVersionOverlayTail.CallSite);
        Assert.True(
            RetailMainMenuVersionOverlayTail.CallSite <
            RetailMainMenuVersionOverlayFlags.EnableByteStoreSite);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsWrapWidth);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsDestImmediates);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsFade);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsLengthClampAsWrap);
        Assert.False(RetailMainMenuVersionOverlayTail.InventsKerningHack);
    }

    [Fact]
    public void DrawMainMenuKeepsTitleFontDrawTextAndDoesNotInventDestOrWrap()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        string draw = Slice(flow, "private void DrawMainMenu()");

        Assert.Contains("RetailMainMenuVersionOverlayTail", draw, StringComparison.Ordinal);
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
        Assert.DoesNotContain("RetailMainMenuVersionOverlayTail", quit, StringComparison.Ordinal);
        string choice = Slice(flow, "private void DrawQuitConfirmChoice");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayTail", choice, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayTail", loading, StringComparison.Ordinal);
        string bar = Slice(flow, "private void DrawMainMenuSelectorBar");
        Assert.DoesNotContain("RetailMainMenuVersionOverlayTail", bar, StringComparison.Ordinal);
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
