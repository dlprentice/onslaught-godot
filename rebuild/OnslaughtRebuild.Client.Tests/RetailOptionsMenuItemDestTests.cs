// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItem__Render</c> label dest leftover at
/// <c>0x004A3394</c> — SIZE.cx integer-half then incoming dest X
/// minus that half — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A337B</c> <c>lea ecx, [esp+0x0C]</c>,
/// <c>0x004A338F</c> <c>call 0x00540680</c> (RET 8 at
/// <c>0x00540823</c>), <c>0x004A3394</c> <c>mov eax, [esp+0x0C]</c>,
/// <c>0x004A3398</c> <c>mov [esp+0x20], 0x3F800000</c> (1.0),
/// <c>0x004A33A0</c> <c>cdq / sub eax, edx / sar eax, 1</c>,
/// <c>0x004A33A9</c> <c>fild [esp+0x24]</c>,
/// <c>0x004A33AD</c> <c>fld [esp+0x18]</c>,
/// <c>0x004A33B1</c> <c>fsub st(1)</c>,
/// <c>0x004A33B3</c> <c>fstp [esp+0x24]</c>,
/// <c>0x004A33BB</c> <c>fcomp [0x005D85D8]</c>,
/// <c>0x005D85D8</c> is <c>00 00 A0 40</c> (5.0).
/// Dest Y is incoming <c>[esp+0x1C]</c>, not a 5.0 push.
/// Nearby <c>and esi, ebp</c> at <c>0x004A33FC</c> is already
/// <c>RetailOptionsMenuItemColor</c>. DrawOptionTextCentered
/// consumes DestX. Dest Y keeps the row top. Do not invent dest
/// Y=5, dest X=5, dest Y=268, dest Y=284, dest Y=304, wrap, fade,
/// sheen, or a 2px kerning hack. Do not change MeasureText. Do
/// not redo the colour AND, Apply pulse, dropdown cosine,
/// language pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsMenuItemDestTests
{
    [Fact]
    public void SpecimenSitesAreLabelDestNotDestImmediates()
    {
        Assert.Equal(0x004A32C0u, RetailOptionsMenuItemDest.RenderSite);
        Assert.Equal(0x004A337Bu, RetailOptionsMenuItemDest.SizeLeaSite);
        Assert.Equal(0x004A338Fu, RetailOptionsMenuItemDest.ExtentCallSite);
        Assert.Equal(0x00540680u, RetailOptionsMenuItemDest.GetTextExtent);
        Assert.Equal(0x004A3394u, RetailOptionsMenuItemDest.CxLoadSite);
        Assert.Equal(0x004A3398u, RetailOptionsMenuItemDest.ScaleStoreSite);
        Assert.Equal(0x3F800000u, RetailOptionsMenuItemDest.ScaleBits);
        Assert.Equal(0x004A33A3u, RetailOptionsMenuItemDest.HalfSarSite);
        Assert.Equal(0x004A33A9u, RetailOptionsMenuItemDest.FildHalfSite);
        Assert.Equal(0x004A33ADu, RetailOptionsMenuItemDest.FldDestXSite);
        Assert.Equal(0x004A33B1u, RetailOptionsMenuItemDest.FsubSite);
        Assert.Equal(0x004A33B3u, RetailOptionsMenuItemDest.DestStoreSite);
        Assert.Equal(0x004A33BBu, RetailOptionsMenuItemDest.FcompSite);
        Assert.Equal(0x005D85D8u, RetailOptionsMenuItemDest.LeftoverMinGlobal);
        Assert.Equal(0x40A00000u, RetailOptionsMenuItemDest.LeftoverMinBits);
        Assert.Equal(0x004A33D2u, RetailOptionsMenuItemDest.LeftoverMinStoreSite);
        Assert.Equal(0x004A33EAu, RetailOptionsMenuItemDest.DestYLoadSite);
        Assert.Equal(0x004A3410u, RetailOptionsMenuItemDest.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsMenuItemDest.DrawText);
        Assert.Equal(
            RetailOptionsMenuItemColor.RenderSite,
            RetailOptionsMenuItemDest.RenderSite);
        Assert.Equal(
            RetailOptionsMenuItemColor.AndSite,
            0x004A33FCu);
        Assert.True(RetailOptionsMenuItemDest.SizeLeaSite < RetailOptionsMenuItemDest.ExtentCallSite);
        Assert.True(RetailOptionsMenuItemDest.ExtentCallSite < RetailOptionsMenuItemDest.CxLoadSite);
        Assert.True(RetailOptionsMenuItemDest.CxLoadSite < RetailOptionsMenuItemDest.ScaleStoreSite);
        Assert.True(RetailOptionsMenuItemDest.ScaleStoreSite < RetailOptionsMenuItemDest.HalfSarSite);
        Assert.True(RetailOptionsMenuItemDest.HalfSarSite < RetailOptionsMenuItemDest.FildHalfSite);
        Assert.True(RetailOptionsMenuItemDest.FildHalfSite < RetailOptionsMenuItemDest.FldDestXSite);
        Assert.True(RetailOptionsMenuItemDest.FldDestXSite < RetailOptionsMenuItemDest.FsubSite);
        Assert.True(RetailOptionsMenuItemDest.FsubSite < RetailOptionsMenuItemDest.DestStoreSite);
        Assert.True(RetailOptionsMenuItemDest.DestStoreSite < RetailOptionsMenuItemDest.FcompSite);
        Assert.True(RetailOptionsMenuItemDest.FcompSite < RetailOptionsMenuItemDest.LeftoverMinStoreSite);
        Assert.True(RetailOptionsMenuItemDest.LeftoverMinStoreSite < RetailOptionsMenuItemDest.DestYLoadSite);
        Assert.True(RetailOptionsMenuItemDest.DestYLoadSite < RetailOptionsMenuItemColor.AndSite);
        Assert.True(RetailOptionsMenuItemColor.AndSite < RetailOptionsMenuItemDest.DrawTextCallSite);
        Assert.False(RetailOptionsMenuItemDest.InventsDestY5);
        Assert.False(RetailOptionsMenuItemDest.InventsDestX5);
        Assert.False(RetailOptionsMenuItemDest.InventsDestY268);
        Assert.False(RetailOptionsMenuItemDest.InventsDestY284);
        Assert.False(RetailOptionsMenuItemDest.InventsDestY304);
        Assert.False(RetailOptionsMenuItemDest.InventsDestImmediates);
        Assert.False(RetailOptionsMenuItemDest.InventsKerningHack);
        Assert.False(RetailOptionsMenuItemDest.InventsSheen);
        Assert.False(RetailOptionsMenuItemDest.InventsWrapWidth);
        Assert.False(RetailOptionsMenuItemDest.InventsFade);
        Assert.False(RetailOptionsMenuItemDest.IsSetLanguage);
        Assert.False(RetailOptionsMenuItemDest.IsButtonPressed);
        Assert.False(RetailOptionsMenuItemDest.RedoesMenuItemColor);
        Assert.False(RetailOptionsMenuItemDest.RedoesApplyPulse);
        Assert.False(RetailOptionsMenuItemDest.RedoesLabelDest);
        Assert.False(RetailOptionsMenuItemDest.RedoesLanguagePitch);
        Assert.False(RetailOptionsMenuItemDest.UsesTwinFadeGate);
        Assert.False(RetailOptionsMenuItemDest.UsesLanguageCompare);
        Assert.False(RetailOptionsMenuItemDest.ChangesMeasureText);
    }

    [Fact]
    public void LabelDestIsCenterMinusIntegerHalfAndIsNotFive()
    {
        Assert.Equal(1f, RetailOptionsMenuItemDest.IdentityScale);
        Assert.Equal(5f, RetailOptionsMenuItemDest.LeftoverMinX);
        Assert.Equal(8, RetailOptionsMenuItemDest.IntegerHalf(16));
        Assert.Equal(8, RetailOptionsMenuItemDest.IntegerHalf(17));
        Assert.Equal(43, RetailOptionsMenuItemDest.IntegerHalf(86));
        Assert.Equal(43, RetailOptionsMenuItemDest.IntegerHalf(87));
        Assert.Equal(312f, RetailOptionsMenuItemDest.DestX(320f, 16));
        Assert.Equal(312f, RetailOptionsMenuItemDest.DestX(320f, 17));
        Assert.Equal(277f, RetailOptionsMenuItemDest.DestX(320f, 86));
        Assert.Equal(277f, RetailOptionsMenuItemDest.DestX(320f, 87));
        Assert.Equal(320f, RetailOptionsMenuItemDest.DestX(320f, 0));
        Assert.Equal(1f, RetailOptionsMenuItemDest.Scale(320f, 16));
        Assert.Equal(1f, RetailOptionsMenuItemDest.Scale(320f, 86));
        Assert.NotEqual(5f, RetailOptionsMenuItemDest.DestX(320f, 16));
        Assert.NotEqual(5f, RetailOptionsMenuItemDest.DestX(320f, 86));
        Assert.NotEqual(268f, RetailOptionsMenuItemDest.DestX(320f, 16));
        Assert.NotEqual(284f, RetailOptionsMenuItemDest.DestX(320f, 16));
        Assert.NotEqual(304f, RetailOptionsMenuItemDest.DestX(320f, 16));
        Assert.NotEqual(
            320f - (17f * 0.5f),
            RetailOptionsMenuItemDest.DestX(320f, 17));
        Assert.NotEqual(
            RetailOptionsMenuItemDest.LeftoverMinX,
            RetailOptionsMenuItemDest.IdentityScale);
        Assert.False(RetailOptionsMenuItemDest.InventsDestY5);
        Assert.False(RetailOptionsMenuItemDest.InventsDestX5);
        Assert.False(RetailOptionsMenuItemDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawOptionRowConsumesDestXAndDoesNotInventDestFive()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string centered = Slice(options, "private void DrawOptionTextCentered");
        string labelValue = Slice(options, "private void DrawLabelValueRow");
        string valueBar = Slice(options, "private void DrawValueBarRow");

        Assert.Contains("RetailOptionsMenuItemDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsMenuItemDest.DestX", centered, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsMenuItemColor.PackedColor", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest.LeftoverMinX", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest.Scale", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("0.5f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemDest", valueBar, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsMenuItemDest", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsMenuItemDest", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsMenuItemDest", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsMenuItemDest", click, StringComparison.Ordinal);
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
