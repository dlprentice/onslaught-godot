// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItemDropdown::Render</c> label dest leftover at
/// <c>0x004A3D19</c> — <c>fild</c> SIZE.cx then incoming dest X minus
/// that full width — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A3D0D</c> <c>call 0x00515A70</c>,
/// <c>0x004A3D14</c> <c>call 0x00540680</c> (RET 8 at
/// <c>0x00540823</c>), <c>0x004A3D19</c> <c>fild [esp+0x1C]</c>,
/// <c>0x004A3D1D</c> <c>fld [esp+0x108]</c>,
/// <c>0x004A3D24</c> <c>mov [esp+0x14], 0x3F800000</c> (1.0),
/// <c>0x004A3D2C</c> <c>fsub st(1)</c>,
/// <c>0x004A3D2E</c> <c>fstp [esp+0x0C]</c>,
/// <c>0x004A3D46</c> <c>fcomp [0x005D85D8]</c>,
/// <c>0x005D85D8</c> is <c>00 00 A0 40</c> (5.0).
/// Dest Y is incoming <c>[esp+0x10C]</c>, not a 5.0 push.
/// Nearby <c>fadd [0x005D8BA0]</c> is 2.0 and is not dest.
/// <c>0x004A3394</c> is already <c>RetailOptionsMenuItemDest</c>
/// (integer-half). <c>0x004A3C69</c> is already the dropdown
/// cosine gate. DrawLabelValueRow consumes DestX. Dest Y keeps
/// the row top. Do not invent dest Y=5, dest X=5, dest Y=268,
/// dest Y=284, dest Y=304, dest from 2.0, wrap, fade, sheen, or
/// a 2px kerning hack. Do not change MeasureText. Do not redo
/// the colour AND, Apply pulse, dropdown cosine, CMenuItem dest,
/// language pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsDropdownDestTests
{
    [Fact]
    public void SpecimenSitesAreDropdownDestNotDestImmediates()
    {
        Assert.Equal(0x004A3C30u, RetailOptionsDropdownDest.RenderSite);
        Assert.Equal(0x004A3D0Du, RetailOptionsDropdownDest.FontCallSite);
        Assert.Equal(0x00515A70u, RetailOptionsDropdownDest.Font);
        Assert.Equal(0x004A3D14u, RetailOptionsDropdownDest.ExtentCallSite);
        Assert.Equal(0x00540680u, RetailOptionsDropdownDest.GetTextExtent);
        Assert.Equal(0x004A3D19u, RetailOptionsDropdownDest.FildCxSite);
        Assert.Equal(0x004A3D1Du, RetailOptionsDropdownDest.FldDestXSite);
        Assert.Equal(0x004A3D24u, RetailOptionsDropdownDest.ScaleStoreSite);
        Assert.Equal(0x3F800000u, RetailOptionsDropdownDest.ScaleBits);
        Assert.Equal(0x004A3D2Cu, RetailOptionsDropdownDest.FsubSite);
        Assert.Equal(0x004A3D2Eu, RetailOptionsDropdownDest.DestStoreSite);
        Assert.Equal(0x004A3D46u, RetailOptionsDropdownDest.FcompSite);
        Assert.Equal(0x005D85D8u, RetailOptionsDropdownDest.LeftoverMinGlobal);
        Assert.Equal(0x40A00000u, RetailOptionsDropdownDest.LeftoverMinBits);
        Assert.Equal(0x004A3D60u, RetailOptionsDropdownDest.LeftoverMinStoreSite);
        Assert.Equal(0x005D8BA0u, RetailOptionsDropdownDest.PadGlobal);
        Assert.Equal(0x40000000u, RetailOptionsDropdownDest.PadBits);
        Assert.Equal(0x004A3D78u, RetailOptionsDropdownDest.DestYLoadSite);
        Assert.Equal(0x004A3DA3u, RetailOptionsDropdownDest.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsDropdownDest.DrawText);
        Assert.Equal(0x004A40B4u, RetailOptionsDropdownDest.CollapsedValueDestSite);
        Assert.Equal(
            RetailOptionsApplyPulse.DropdownCompareSite,
            0x004A3C69u);
        Assert.Equal(
            RetailOptionsMenuItemDest.CxLoadSite,
            0x004A3394u);
        Assert.True(RetailOptionsDropdownDest.FontCallSite < RetailOptionsDropdownDest.ExtentCallSite);
        Assert.True(RetailOptionsDropdownDest.ExtentCallSite < RetailOptionsDropdownDest.FildCxSite);
        Assert.True(RetailOptionsDropdownDest.FildCxSite < RetailOptionsDropdownDest.FldDestXSite);
        Assert.True(RetailOptionsDropdownDest.FldDestXSite < RetailOptionsDropdownDest.ScaleStoreSite);
        Assert.True(RetailOptionsDropdownDest.ScaleStoreSite < RetailOptionsDropdownDest.FsubSite);
        Assert.True(RetailOptionsDropdownDest.FsubSite < RetailOptionsDropdownDest.DestStoreSite);
        Assert.True(RetailOptionsDropdownDest.DestStoreSite < RetailOptionsDropdownDest.FcompSite);
        Assert.True(RetailOptionsDropdownDest.FcompSite < RetailOptionsDropdownDest.LeftoverMinStoreSite);
        Assert.True(RetailOptionsDropdownDest.LeftoverMinStoreSite < RetailOptionsDropdownDest.DestYLoadSite);
        Assert.True(RetailOptionsDropdownDest.DestYLoadSite < RetailOptionsDropdownDest.DrawTextCallSite);
        Assert.True(RetailOptionsApplyPulse.DropdownCompareSite < RetailOptionsDropdownDest.FildCxSite);
        Assert.True(RetailOptionsMenuItemDest.CxLoadSite < RetailOptionsDropdownDest.FildCxSite);
        Assert.True(RetailOptionsDropdownDest.DrawTextCallSite < RetailOptionsDropdownDest.CollapsedValueDestSite);
        Assert.False(RetailOptionsDropdownDest.InventsDestY5);
        Assert.False(RetailOptionsDropdownDest.InventsDestX5);
        Assert.False(RetailOptionsDropdownDest.InventsDestY268);
        Assert.False(RetailOptionsDropdownDest.InventsDestY284);
        Assert.False(RetailOptionsDropdownDest.InventsDestY304);
        Assert.False(RetailOptionsDropdownDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownDest.InventsDestImmediates);
        Assert.False(RetailOptionsDropdownDest.InventsKerningHack);
        Assert.False(RetailOptionsDropdownDest.InventsSheen);
        Assert.False(RetailOptionsDropdownDest.InventsWrapWidth);
        Assert.False(RetailOptionsDropdownDest.InventsFade);
        Assert.False(RetailOptionsDropdownDest.IsSetLanguage);
        Assert.False(RetailOptionsDropdownDest.IsButtonPressed);
        Assert.False(RetailOptionsDropdownDest.RedoesMenuItemDest);
        Assert.False(RetailOptionsDropdownDest.RedoesMenuItemColor);
        Assert.False(RetailOptionsDropdownDest.RedoesApplyPulse);
        Assert.False(RetailOptionsDropdownDest.RedoesLanguagePitch);
        Assert.False(RetailOptionsDropdownDest.UsesTwinFadeGate);
        Assert.False(RetailOptionsDropdownDest.UsesLanguageCompare);
        Assert.False(RetailOptionsDropdownDest.ChangesMeasureText);
        Assert.False(RetailOptionsDropdownDest.UsesIntegerHalf);
    }

    [Fact]
    public void LabelDestIsIncomingMinusFullWidthAndIsNotHalfOrFive()
    {
        Assert.Equal(1f, RetailOptionsDropdownDest.IdentityScale);
        Assert.Equal(5f, RetailOptionsDropdownDest.LeftoverMinX);
        Assert.Equal(2f, RetailOptionsDropdownDest.Pad);
        Assert.Equal(16, RetailOptionsDropdownDest.Width(16));
        Assert.Equal(17, RetailOptionsDropdownDest.Width(17));
        Assert.Equal(86, RetailOptionsDropdownDest.Width(86));
        Assert.Equal(303f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.Equal(302f, RetailOptionsDropdownDest.DestX(319f, 17));
        Assert.Equal(233f, RetailOptionsDropdownDest.DestX(319f, 86));
        Assert.Equal(232f, RetailOptionsDropdownDest.DestX(319f, 87));
        Assert.Equal(319f, RetailOptionsDropdownDest.DestX(319f, 0));
        Assert.Equal(1f, RetailOptionsDropdownDest.Scale(319f, 16));
        Assert.Equal(1f, RetailOptionsDropdownDest.Scale(319f, 86));
        Assert.NotEqual(5f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(5f, RetailOptionsDropdownDest.DestX(319f, 86));
        Assert.NotEqual(2f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(268f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(284f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(304f, RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(
            RetailOptionsMenuItemDest.DestX(319f, 16),
            RetailOptionsDropdownDest.DestX(319f, 16));
        Assert.NotEqual(
            319f - RetailOptionsMenuItemDest.IntegerHalf(17),
            RetailOptionsDropdownDest.DestX(319f, 17));
        Assert.NotEqual(
            319f - (17f * 0.5f),
            RetailOptionsDropdownDest.DestX(319f, 17));
        Assert.NotEqual(
            RetailOptionsDropdownDest.LeftoverMinX,
            RetailOptionsDropdownDest.IdentityScale);
        Assert.NotEqual(
            RetailOptionsDropdownDest.Pad,
            RetailOptionsDropdownDest.LeftoverMinX);
        Assert.False(RetailOptionsDropdownDest.InventsDestY5);
        Assert.False(RetailOptionsDropdownDest.InventsDestX5);
        Assert.False(RetailOptionsDropdownDest.InventsDestFromPad);
        Assert.False(RetailOptionsDropdownDest.UsesIntegerHalf);
        Assert.False(RetailOptionsDropdownDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawLabelValueRowConsumesDestXAndDoesNotInventDestFive()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string centered = Slice(options, "private void DrawOptionTextCentered");
        string labelValue = Slice(options, "private void DrawLabelValueRow");
        string valueBar = Slice(options, "private void DrawValueBarRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");

        Assert.Contains("RetailOptionsDropdownDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsDropdownDest.DestX", labelValue, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsApplyPulse.DropdownRowIsPending", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest.LeftoverMinX", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest.Scale", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest.Pad", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("2f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("0.5f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("IntegerHalf", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", valueBar, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsDropdownDest", dropdown, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsDropdownDest", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsDropdownDest", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsDropdownDest", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsDropdownDest", click, StringComparison.Ordinal);
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
