// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CMenuItem__Render</c> icon dest leftover at
/// <c>0x004A3301</c> — SIZE.cx integer-half then incoming dest X
/// minus that half via <c>fsubr</c>, with no leftover min dest X —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x004A32CC</c> <c>mov eax, [edi+0x0C]</c>,
/// <c>0x004A32E1</c> <c>call 0x004F2580</c>
/// (<c>CText__GetStringById</c>),
/// <c>0x004A32F5</c> <c>call 0x00515A70</c>,
/// <c>0x004A32FC</c> <c>call 0x00540680</c> (RET 8 at
/// <c>0x00540823</c>), <c>0x004A3301</c> <c>mov eax, [esp+0x10]</c>,
/// <c>0x004A3305</c> <c>push 0x3F800000</c> (1.0),
/// <c>0x004A330A</c> <c>cdq / sub eax, edx</c>,
/// <c>0x004A330D</c> <c>mov edx, [esp+0x24]</c> (incoming dest Y),
/// <c>0x004A3311</c> <c>sar eax, 1</c>,
/// <c>0x004A331C</c> <c>fild [esp+0x30]</c>,
/// <c>0x004A332C</c> <c>fsubr [esp+0x2C]</c>,
/// <c>0x004A3338</c> <c>fstp [esp+0x44]</c>,
/// <c>0x004A334A</c> <c>call 0x004659A0</c>,
/// <c>0x004A3353</c> <c>fadd [0x005D857C]</c>,
/// <c>0x005D857C</c> is <c>00 00 A0 41</c> (20.0).
/// Dest Y is incoming dest Y, not a 20.0 push.
/// Nearby <c>0x005D85D8</c> 5.0 is the later label leftover
/// (<c>RetailOptionsMenuItemDest</c>), not this dest.
/// Nearby <c>0x005D8BA0</c> 2.0 is not dest.
/// <c>Init</c> / <c>InitWithIcon</c> both store 0 at
/// <c>[this+0x0C]</c> — do not invent a prefix draw.
/// DrawOptionRow cites DestX. Dest Y keeps the row top.
/// Scale stays 1.0. Do not invent dest Y=5, dest X=5,
/// dest Y=20, dest Y=268, dest Y=284, dest Y=304, dest from
/// 2.0, wrap, fade, sheen, or a 2px kerning hack. Do not
/// change MeasureText. Do not redo the label dest, dropdown
/// dest, colour AND, Apply pulse, dropdown cosine, language
/// pitch, or the 0x00463669 compare.</para>
/// </summary>
public sealed class RetailOptionsMenuItemIconDestTests
{
    [Fact]
    public void SpecimenSitesAreIconDestNotDestImmediates()
    {
        Assert.Equal(0x004A32C0u, RetailOptionsMenuItemIconDest.RenderSite);
        Assert.Equal(0x0Cu, RetailOptionsMenuItemIconDest.IconIdOffset);
        Assert.Equal(0x004A32CCu, RetailOptionsMenuItemIconDest.IconIdLoadSite);
        Assert.Equal(0x004A32E1u, RetailOptionsMenuItemIconDest.GetStringCallSite);
        Assert.Equal(0x004F2580u, RetailOptionsMenuItemIconDest.GetStringById);
        Assert.Equal(0x004A32F5u, RetailOptionsMenuItemIconDest.FontCallSite);
        Assert.Equal(0x00515A70u, RetailOptionsMenuItemIconDest.Font);
        Assert.Equal(0x004A32FCu, RetailOptionsMenuItemIconDest.ExtentCallSite);
        Assert.Equal(0x00540680u, RetailOptionsMenuItemIconDest.GetTextExtent);
        Assert.Equal(0x00540823u, RetailOptionsMenuItemIconDest.GetTextExtentRetSite);
        Assert.Equal(0x004A3301u, RetailOptionsMenuItemIconDest.CxLoadSite);
        Assert.Equal(0x004A3305u, RetailOptionsMenuItemIconDest.ScalePushSite);
        Assert.Equal(0x3F800000u, RetailOptionsMenuItemIconDest.ScaleBits);
        Assert.Equal(0x004A330Au, RetailOptionsMenuItemIconDest.CdqSite);
        Assert.Equal(0x004A330Du, RetailOptionsMenuItemIconDest.DestYLoadSite);
        Assert.Equal(0x004A3311u, RetailOptionsMenuItemIconDest.HalfSarSite);
        Assert.Equal(0x004A331Cu, RetailOptionsMenuItemIconDest.FildHalfSite);
        Assert.Equal(0x004A332Cu, RetailOptionsMenuItemIconDest.FsubrSite);
        Assert.Equal(0x004A3338u, RetailOptionsMenuItemIconDest.DestStoreSite);
        Assert.Equal(0x004A334Au, RetailOptionsMenuItemIconDest.DrawTextCallSite);
        Assert.Equal(0x004659A0u, RetailOptionsMenuItemIconDest.DrawText);
        Assert.Equal(0x004A3353u, RetailOptionsMenuItemIconDest.PitchAddSite);
        Assert.Equal(0x005D857Cu, RetailOptionsMenuItemIconDest.PitchGlobal);
        Assert.Equal(0x41A00000u, RetailOptionsMenuItemIconDest.PitchBits);
        Assert.Equal(
            RetailOptionsMenuItemDest.RenderSite,
            RetailOptionsMenuItemIconDest.RenderSite);
        Assert.Equal(
            RetailOptionsMenuItemDest.CxLoadSite,
            0x004A3394u);
        Assert.True(RetailOptionsMenuItemIconDest.IconIdLoadSite < RetailOptionsMenuItemIconDest.GetStringCallSite);
        Assert.True(RetailOptionsMenuItemIconDest.GetStringCallSite < RetailOptionsMenuItemIconDest.FontCallSite);
        Assert.True(RetailOptionsMenuItemIconDest.FontCallSite < RetailOptionsMenuItemIconDest.ExtentCallSite);
        Assert.True(RetailOptionsMenuItemIconDest.ExtentCallSite < RetailOptionsMenuItemIconDest.CxLoadSite);
        Assert.True(RetailOptionsMenuItemIconDest.CxLoadSite < RetailOptionsMenuItemIconDest.ScalePushSite);
        Assert.True(RetailOptionsMenuItemIconDest.ScalePushSite < RetailOptionsMenuItemIconDest.CdqSite);
        Assert.True(RetailOptionsMenuItemIconDest.CdqSite < RetailOptionsMenuItemIconDest.DestYLoadSite);
        Assert.True(RetailOptionsMenuItemIconDest.DestYLoadSite < RetailOptionsMenuItemIconDest.HalfSarSite);
        Assert.True(RetailOptionsMenuItemIconDest.HalfSarSite < RetailOptionsMenuItemIconDest.FildHalfSite);
        Assert.True(RetailOptionsMenuItemIconDest.FildHalfSite < RetailOptionsMenuItemIconDest.FsubrSite);
        Assert.True(RetailOptionsMenuItemIconDest.FsubrSite < RetailOptionsMenuItemIconDest.DestStoreSite);
        Assert.True(RetailOptionsMenuItemIconDest.DestStoreSite < RetailOptionsMenuItemIconDest.DrawTextCallSite);
        Assert.True(RetailOptionsMenuItemIconDest.DrawTextCallSite < RetailOptionsMenuItemIconDest.PitchAddSite);
        Assert.True(RetailOptionsMenuItemIconDest.PitchAddSite < RetailOptionsMenuItemDest.CxLoadSite);
        Assert.True(RetailOptionsMenuItemIconDest.UsesFsubr);
        Assert.False(RetailOptionsMenuItemIconDest.HasLeftoverMinDestX);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY5);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestX5);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY20);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY268);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY284);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY304);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestFromPad);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestFromPitch);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestImmediates);
        Assert.False(RetailOptionsMenuItemIconDest.InventsKerningHack);
        Assert.False(RetailOptionsMenuItemIconDest.InventsSheen);
        Assert.False(RetailOptionsMenuItemIconDest.InventsWrapWidth);
        Assert.False(RetailOptionsMenuItemIconDest.InventsFade);
        Assert.False(RetailOptionsMenuItemIconDest.InventsPrefixDraw);
        Assert.False(RetailOptionsMenuItemIconDest.IsSetLanguage);
        Assert.False(RetailOptionsMenuItemIconDest.IsButtonPressed);
        Assert.False(RetailOptionsMenuItemIconDest.RedoesMenuItemDest);
        Assert.False(RetailOptionsMenuItemIconDest.RedoesDropdownDest);
        Assert.False(RetailOptionsMenuItemIconDest.RedoesMenuItemColor);
        Assert.False(RetailOptionsMenuItemIconDest.RedoesApplyPulse);
        Assert.False(RetailOptionsMenuItemIconDest.RedoesLanguagePitch);
        Assert.False(RetailOptionsMenuItemIconDest.UsesTwinFadeGate);
        Assert.False(RetailOptionsMenuItemIconDest.UsesLanguageCompare);
        Assert.False(RetailOptionsMenuItemIconDest.ChangesMeasureText);
    }

    [Fact]
    public void IconDestIsCenterMinusIntegerHalfWithoutLeftoverMin()
    {
        Assert.Equal(1f, RetailOptionsMenuItemIconDest.IdentityScale);
        Assert.Equal(20f, RetailOptionsMenuItemIconDest.LeftoverLabelPitch);
        Assert.Equal(8, RetailOptionsMenuItemIconDest.IntegerHalf(16));
        Assert.Equal(8, RetailOptionsMenuItemIconDest.IntegerHalf(17));
        Assert.Equal(43, RetailOptionsMenuItemIconDest.IntegerHalf(86));
        Assert.Equal(43, RetailOptionsMenuItemIconDest.IntegerHalf(87));
        Assert.Equal(312f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.Equal(312f, RetailOptionsMenuItemIconDest.DestX(320f, 17));
        Assert.Equal(277f, RetailOptionsMenuItemIconDest.DestX(320f, 86));
        Assert.Equal(277f, RetailOptionsMenuItemIconDest.DestX(320f, 87));
        Assert.Equal(320f, RetailOptionsMenuItemIconDest.DestX(320f, 0));
        Assert.Equal(1f, RetailOptionsMenuItemIconDest.Scale(320f, 16));
        Assert.Equal(1f, RetailOptionsMenuItemIconDest.Scale(320f, 86));
        Assert.Equal(1f, RetailOptionsMenuItemIconDest.Scale(4f, 16));
        Assert.NotEqual(1f, RetailOptionsMenuItemDest.Scale(4f, 16));
        Assert.Equal(
            RetailOptionsMenuItemDest.DestX(320f, 17),
            RetailOptionsMenuItemIconDest.DestX(320f, 17));
        Assert.NotEqual(5f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(20f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(2f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(268f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(284f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(304f, RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.NotEqual(
            320f - (17f * 0.5f),
            RetailOptionsMenuItemIconDest.DestX(320f, 17));
        Assert.NotEqual(
            RetailOptionsMenuItemIconDest.LeftoverLabelPitch,
            RetailOptionsMenuItemIconDest.IdentityScale);
        Assert.NotEqual(
            RetailOptionsDropdownDest.DestX(320f, 16),
            RetailOptionsMenuItemIconDest.DestX(320f, 16));
        Assert.False(RetailOptionsMenuItemIconDest.HasLeftoverMinDestX);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY5);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestX5);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestY20);
        Assert.False(RetailOptionsMenuItemIconDest.InventsDestFromPitch);
        Assert.False(RetailOptionsMenuItemIconDest.InventsPrefixDraw);
        Assert.False(RetailOptionsMenuItemIconDest.ChangesMeasureText);
    }

    [Fact]
    public void DrawOptionRowCitesDestXAndDoesNotInventDestTwenty()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string centered = Slice(options, "private void DrawOptionTextCentered");
        string labelValue = Slice(options, "private void DrawLabelValueRow");
        string valueBar = Slice(options, "private void DrawValueBarRow");

        Assert.Contains("RetailOptionsMenuItemIconDest", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsMenuItemIconDest.DestX", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsMenuItemDest.DestX", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest.LeftoverLabelPitch", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest.Scale", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("20f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("5f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("5.0", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("0.5f", centered, StringComparison.Ordinal);
        Assert.DoesNotContain("268f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("284f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("304f", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("0x00463669", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", labelValue, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", valueBar, StringComparison.Ordinal);

        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string main = Slice(flow, "private void DrawMainMenu()");
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", main, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", quit, StringComparison.Ordinal);
        string loading = Slice(flow, "private void DrawLoading(");
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", loading, StringComparison.Ordinal);
        string click = Slice(flow, "private void DrawClickToStart()");
        Assert.DoesNotContain("RetailOptionsMenuItemIconDest", click, StringComparison.Ordinal);
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
