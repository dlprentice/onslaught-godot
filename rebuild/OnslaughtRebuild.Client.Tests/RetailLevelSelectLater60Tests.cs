// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.Client;
using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the <c>CFEPLevelSelect::Render</c> later leftover after the
/// later 610.0 / 0.0 pair — <c>0x00460F6F</c>
/// <c>fild [esp+0x3C]</c> / <c>0x00460F73</c>
/// <c>fmul [0x005DB538]</c> (60.0) / <c>0x00460F79</c>
/// <c>fmul [0x005D85EC]</c> (0.5) / <c>0x00460F7F</c>
/// <c>fadd [0x005DB3E8]</c> (320.0) / <c>0x00460F85</c>
/// <c>fstp [esp+0x18]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). Image base <c>0x400000</c>.
/// File offset = VA − <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para>Official bytes independently re-read this cycle:
/// <c>0x00460F5E</c> is already the later-610 first store
/// <c>mov [esp+0x28], 0</c> (<c>c7 44 24 28 00 00 00 00</c>).
/// <c>0x00460F6F</c> <c>fild [esp+0x3C]</c>
/// (<c>db 44 24 3c</c>), <c>0x00460F73</c>
/// <c>fmul [0x005DB538]</c> (<c>d8 0d 38 b5 5d 00</c>),
/// <c>0x005DB538</c> is <c>00 00 70 42</c> (60.0, bits
/// <c>0x42700000</c>), <c>0x00460F79</c>
/// <c>fmul [0x005D85EC]</c> (<c>d8 0d ec 85 5d 00</c>),
/// <c>0x005D85EC</c> is <c>00 00 00 3f</c> (0.5, bits
/// <c>0x3F000000</c>), <c>0x00460F7F</c>
/// <c>fadd [0x005DB3E8]</c> (<c>d8 05 e8 b3 5d 00</c>),
/// <c>0x005DB3E8</c> is <c>00 00 a0 43</c> (320.0, bits
/// <c>0x43A00000</c>). First store is <c>0x00460F85</c>
/// <c>fstp [esp+0x18]</c> (<c>d9 5c 24 18</c>). That is
/// not dest. The later 610.0 / 0.0 pair at <c>0x00460F30</c>
/// is already owned. The later 1.0 fcom at <c>0x00460E62</c>
/// is already owned. The later <c>[esp+0x94]</c> shift at
/// <c>0x00460E34</c> is already owned. The later 148.0
/// triple at <c>0x00460E24</c> is already owned. The 10.0
/// fsub at <c>0x00460C94</c> is already owned. The 148.0 fsub
/// at <c>0x00460B66</c> is already owned. Sliding-borders
/// already owns <c>0x00460B61</c>. Latch SET already owns
/// <c>0x0042D5CF</c>. FMV skip already owns the OR at
/// <c>0x0053F2EB</c>. The later <c>fld [esp+0x94]</c> /
/// <c>fcomp 1.0</c> at <c>0x00460FD8</c> is later. Dest Y
/// does not. Dest is not 15.5, 322.5, 148.0, 10.0, 138.0,
/// 322.0, 610.0, 90.0, 570.0, 0.75, 4.0, 1.0, 255.0, 0.0,
/// 60.0, 0.5, 320.0, or the 2.0 constant. DrawLevelSelect
/// consumes the leftover as the later scaled local, not dest.
/// Do not invent dest Y=5, dest X=5, dest Y=268, dest Y=284,
/// dest Y=304, dest from the 2.0 constant, wrap, fade, sheen,
/// or a 2px kerning hack. Do not change MeasureText. Do not
/// redo dest leftovers, list colour, list hover, list click,
/// list cancel, click-hit sound, latch-to-button SET, the
/// sliding-borders call, the 148.0 fsub, the 10.0 fsub, the
/// later 148.0 triple, the later <c>[esp+0x94]</c> shift,
/// the later 1.0 fcom, the later 610.0 / 0.0 pair, Apply
/// pulse, dropdown cosine, language pitch, or the 0x00463669
/// compare. Do not invent dest from 60.0. Do not invent dest
/// from 0.5. Do not invent dest from 320.0. Do not invent a
/// fade. Do not invent that the third FMV latch is dest.</para>
/// </summary>
public sealed class RetailLevelSelectLater60Tests
{
    [Fact]
    public void SpecimenSitesAreLater60ScaleNotDestColourHoverClickCancelOrLater610()
    {
        Assert.Equal(0x00460B40u, RetailLevelSelectLater60.RenderSite);
        Assert.Equal(0x00460B61u, RetailLevelSelectLater60.SlidingCallSite);
        Assert.Equal(0x00460B66u, RetailLevelSelectLater60.Fsub148Site);
        Assert.Equal(0x00460C94u, RetailLevelSelectLater60.Fsub10Site);
        Assert.Equal(0x00460E24u, RetailLevelSelectLater60.Later148Site);
        Assert.Equal(0x00460E34u, RetailLevelSelectLater60.LaterEsp94Site);
        Assert.Equal(0x00460E62u, RetailLevelSelectLater60.LaterOneSite);
        Assert.Equal(0x00460F30u, RetailLevelSelectLater60.Later610Site);
        Assert.Equal(0x00460F5Eu, RetailLevelSelectLater60.Later610StoreSite);
        Assert.Equal(0x00460F6Fu, RetailLevelSelectLater60.FildSite);
        Assert.Equal(0x3C, RetailLevelSelectLater60.FildLocal);
        Assert.Equal(0x00460F73u, RetailLevelSelectLater60.Fmul60Site);
        Assert.Equal(0x005DB538u, RetailLevelSelectLater60.FactorConst);
        Assert.Equal(0x42700000u, RetailLevelSelectLater60.FactorBits);
        Assert.Equal(0x00460F79u, RetailLevelSelectLater60.FmulHalfSite);
        Assert.Equal(0x005D85ECu, RetailLevelSelectLater60.HalfConst);
        Assert.Equal(0x3F000000u, RetailLevelSelectLater60.HalfBits);
        Assert.Equal(0x00460F7Fu, RetailLevelSelectLater60.FaddSite);
        Assert.Equal(0x005DB3E8u, RetailLevelSelectLater60.AddendConst);
        Assert.Equal(0x43A00000u, RetailLevelSelectLater60.AddendBits);
        Assert.Equal(0x00460F85u, RetailLevelSelectLater60.FstpSite);
        Assert.Equal(0x18, RetailLevelSelectLater60.StoreLocal);
        Assert.Equal(0x00460FD8u, RetailLevelSelectLater60.LaterEsp94OneSite);
        Assert.Equal(0x00460FECu, RetailLevelSelectLater60.LaterEsp18Site);
        Assert.Equal(0x0042D5CFu, RetailLevelSelectLater60.LatchSetSite);
        Assert.Equal(0x0053F2EBu, RetailLevelSelectLater60.FmvOrSite);
        Assert.Equal(
            RetailLevelSelectLater610.LaterFmul60Site,
            RetailLevelSelectLater60.Fmul60Site);
        Assert.Equal(
            RetailLevelSelectLater610.Later60Const,
            RetailLevelSelectLater60.FactorConst);
        Assert.Equal(
            RetailLevelSelectLater610.Later60Bits,
            RetailLevelSelectLater60.FactorBits);
        Assert.Equal(
            RetailLevelSelectLater610.WindowHighFldSite,
            RetailLevelSelectLater60.Later610Site);
        Assert.Equal(
            RetailLevelSelectLater610.FirstStoreSite,
            RetailLevelSelectLater60.Later610StoreSite);
        Assert.Equal(
            RetailLevelSelectLaterOne.FcomSite,
            RetailLevelSelectLater60.LaterOneSite);
        Assert.Equal(
            RetailLevelSelectLaterEsp94.FldSite,
            RetailLevelSelectLater60.LaterEsp94Site);
        Assert.Equal(
            RetailLevelSelectLater148.FldSite,
            RetailLevelSelectLater60.Later148Site);
        Assert.Equal(
            RetailLevelSelectSlidingBorders.CallSite,
            RetailLevelSelectLater60.SlidingCallSite);
        Assert.Equal(
            RetailFrontendLatchToButton.RightSetSite,
            RetailLevelSelectLater60.LatchSetSite);
        Assert.NotEqual(
            RetailLevelSelectLater60.Fmul60Site,
            RetailLevelSelectLater60.Later610Site);
        Assert.NotEqual(
            RetailLevelSelectLater60.FstpSite,
            RetailLevelSelectLater60.LaterEsp94OneSite);
        Assert.NotEqual(
            RetailOptionsDropdownListHover.HoverHitSite,
            RetailLevelSelectLater60.Fmul60Site);
        Assert.True(RetailLevelSelectLater60.Later610StoreSite < RetailLevelSelectLater60.FildSite);
        Assert.True(RetailLevelSelectLater60.FildSite < RetailLevelSelectLater60.Fmul60Site);
        Assert.True(RetailLevelSelectLater60.Fmul60Site < RetailLevelSelectLater60.FmulHalfSite);
        Assert.True(RetailLevelSelectLater60.FmulHalfSite < RetailLevelSelectLater60.FaddSite);
        Assert.True(RetailLevelSelectLater60.FaddSite < RetailLevelSelectLater60.FstpSite);
        Assert.True(RetailLevelSelectLater60.FstpSite < RetailLevelSelectLater60.LaterEsp94OneSite);
        Assert.True(RetailLevelSelectLater60.LaterEsp94OneSite < RetailLevelSelectLater60.LaterEsp18Site);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater60.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom320);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater60.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater60.InventsFade);
        Assert.True(RetailLevelSelectLater60.IsLater60);
        Assert.False(RetailLevelSelectLater60.IsLater610);
        Assert.False(RetailLevelSelectLater60.IsLaterOne);
        Assert.False(RetailLevelSelectLater60.RedoesLater610);
        Assert.False(RetailLevelSelectLater60.RedoesLaterOne);
        Assert.False(RetailLevelSelectLater60.ChangesMeasureText);
        Assert.False(RetailLevelSelectLater60.UsesCurrentIndex);
        Assert.True(RetailLevelSelectLater610.IsLater610);
        Assert.True(RetailLevelSelectLaterOne.IsLaterOne);
        Assert.True(RetailFrontendLatchToButton.IsLatchSet);
    }

    [Fact]
    public void ScaledIsEaxMinusOneTimes60TimesHalfPlus320AndDoesNotInventDest()
    {
        Assert.Equal(60f, RetailLevelSelectLater60.Factor);
        Assert.Equal(0.5f, RetailLevelSelectLater60.Half);
        Assert.Equal(320f, RetailLevelSelectLater60.Addend);
        Assert.Equal(320f, RetailLevelSelectLater60.Scaled(1));
        Assert.Equal(350f, RetailLevelSelectLater60.Scaled(2));
        Assert.Equal(290f, RetailLevelSelectLater60.Scaled(0));
        Assert.False(RetailLevelSelectLater60.InventsDestFrom60);
        Assert.False(RetailLevelSelectLater60.InventsDestFromHalf);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom320);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom322);
        Assert.False(RetailLevelSelectLater60.InventsDestX322_5);
        Assert.False(RetailLevelSelectLater60.InventsDestFrom610);
        Assert.False(RetailLevelSelectLater60.InventsDestImmediates);
        Assert.False(RetailLevelSelectLater60.InventsFade);
        Assert.False(RetailLevelSelectLater60.RedoesLater610);
        Assert.False(RetailLevelSelectLater60.ChangesMeasureText);
        Assert.True(RetailLevelSelectLater60.IsLater60);
        Assert.False(RetailLevelSelectLater60.IsLater610);
        Assert.False(RetailLevelSelectLater60.IsClickHit);
        Assert.False(RetailLevelSelectLater60.IsHoverHit);
    }

    [Fact]
    public void DrawLevelSelectConsumesLater60AndDoesNotPileIntoMainMenuOrOptions()
    {
        string flow = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.cs"));
        string level = Slice(flow, "private void DrawLevelSelect()");
        string main = Slice(flow, "private void DrawMainMenu()");
        string quit = Slice(flow, "private void DrawQuitConfirm()");
        string loading = Slice(flow, "private void DrawLoading(");
        string click = Slice(flow, "private void DrawClickToStart()");
        string pointerConfirm = Slice(flow, "private bool HandlePointerConfirm(");
        string handleKey = Slice(flow, "private bool HandleKey(");

        Assert.Contains("RetailLevelSelectLater60", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater60.Factor", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater60.Half", level, StringComparison.Ordinal);
        Assert.Contains("RetailLevelSelectLater60.Addend", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", level, StringComparison.Ordinal);
        Assert.DoesNotContain("60f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("320f", level, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", level, StringComparison.Ordinal);
        Assert.DoesNotContain("AcceptsTwinFade", level, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", level, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", level, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", main, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", quit, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", loading, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", click, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", pointerConfirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", handleKey, StringComparison.Ordinal);

        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        string dropdown = Slice(options, "private void DrawOptionDropdown");
        string motion = Slice(options, "private bool HandleOptionsPointerMotion");
        string confirm = Slice(options, "private bool HandleOptionsPointerConfirm");
        string cancel = Slice(options, "private bool HandleOptionsPointerCancel");
        Assert.DoesNotContain("RetailLevelSelectLater60", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", dropdown, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", motion, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", confirm, StringComparison.Ordinal);
        Assert.DoesNotContain("RetailLevelSelectLater60", cancel, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTextExtent", cancel, StringComparison.Ordinal);
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
