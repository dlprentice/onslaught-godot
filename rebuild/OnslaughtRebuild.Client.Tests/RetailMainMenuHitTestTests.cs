// SPDX-License-Identifier: GPL-3.0-only

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPMain::Render pointer hit-test gate at
/// <c>0x004630AC</c> / <c>0x004631EF</c>, recovered from the pristine
/// specimen (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Compare.</b> Both sites
/// (a float load of stack local +0xA4, then float compare against the value at 0x005D8BB0, popping the
/// result, then FPU status-word read, then bit test of ah against 0x41, branching when not equal).
/// <c>pe_read_va</c> of <c>0x005D8BB0</c> is <c>0x3F666666</c> (0.9).
/// A bit test of ah against 0x41 is C3|C0, so equality skips. Hit-test runs only
/// when transition &gt; 0.9. That is the last tenth of the 50-frame
/// <c>SetPage(FEP_MAIN, 50)</c> reveal, not the first.</para>
///
/// <para><b>Not ButtonPressed.</b> <c>FrontEnd.cpp:551-552</c> still
/// swallows every button while <c>mActivePage == FEP_TRANSITION</c>.
/// These sites write <c>this+0x08</c> after <c>0x004693D0</c> returns
/// true — hover selection inside Render, not confirm. Language hover
/// writes <c>-1</c>. That is not <c>CFrontEnd::SetLanguage</c>.</para>
///
/// <para><b>Not the twin fade.</b> The third <c>0x005D8BB0</c> site at
/// <c>0x00463E8D</c> is a bit test of ah against 1, branching to 0x00463FC7 when equal — transition
/// &lt; 0.9 — and then a scale of <c>0x0089D8A4</c>. That is a
/// different predicate. This helper does not invent that fade.</para>
///
/// <para>The mutations these cases kill are "ignore the last tenth",
/// a confirm during FEP_TRANSITION, leaving HandlePointerMotion
/// ungated, and wiring this gate through HandleKey / DrawLoading /
/// DrawQuitConfirm / DrawClickToStart / HandlePointerConfirm.</para>
/// </summary>
public sealed class RetailMainMenuHitTestTests
{
    [Fact]
    public void HitTestRunsOnlyWhenTransitionIsStrictlyAboveZeroPointNine()
    {
        // 0x004630AC / 0x004631EF: a float compare against the value at
        // 0x005D8BB0, popping the result; a bit test of ah against 0x41 / jne.
        Assert.Equal(0x005D8BB0u, RetailMainMenuHitTest.ThresholdGlobal);
        Assert.Equal(0x3F666666u, RetailMainMenuHitTest.ThresholdBits);
        Assert.Equal(0.9f, RetailMainMenuHitTest.Threshold);
        Assert.False(RetailMainMenuHitTest.AcceptsHitTest(0f));
        Assert.False(RetailMainMenuHitTest.AcceptsHitTest(0.5f));
        Assert.False(RetailMainMenuHitTest.AcceptsHitTest(0.9f));
        Assert.True(RetailMainMenuHitTest.AcceptsHitTest(0.9000001f));
        Assert.True(RetailMainMenuHitTest.AcceptsHitTest(1f));
    }

    [Fact]
    public void TransitionSwallowsButtonsButNotPointerMotion()
    {
        // FrontEnd.cpp:551-552. Motion is CFEPMain::Render hover.
        Assert.True(RetailMainMenuHitTest.SwallowsFrontendInput(
            pageIsTransition: true,
            isPointerMotion: false));
        Assert.False(RetailMainMenuHitTest.SwallowsFrontendInput(
            pageIsTransition: true,
            isPointerMotion: true));
        Assert.False(RetailMainMenuHitTest.SwallowsFrontendInput(
            pageIsTransition: false,
            isPointerMotion: false));
        Assert.False(RetailMainMenuHitTest.SwallowsFrontendInput(
            pageIsTransition: false,
            isPointerMotion: true));

        // 50-frame SetPage(FEP_MAIN, 50): 45/50 = 0.9 skips, 46/50 runs.
        Assert.False(RetailMainMenuHitTest.AcceptsHitTest(45f / 50f));
        Assert.True(RetailMainMenuHitTest.AcceptsHitTest(46f / 50f));
        Assert.False(RetailMainMenuHitTest.IsButtonPressed);
    }

    [Fact]
    public void LanguageHoverRectIsOneHundredNineteenToThreeNineteenAroundYTwoSixtyEight()
    {
        // 0x00462E96: a store of constant 0x43860000 to stack local +0x10 (268). Half-extent is
        // 0x005D857C = 20. cdecl into 0x004693D0 is (119, Y-20, 319, Y+20).
        // 0x00523B50 is left <= x < right and top <= y < bottom.
        Assert.Equal(119f, RetailMainMenuHitTest.LanguageHoverLeft);
        Assert.Equal(319f, RetailMainMenuHitTest.LanguageHoverRight);
        Assert.Equal(268f, RetailMainMenuHitTest.LanguageHoverCenterY);
        Assert.Equal(20f, RetailMainMenuHitTest.LanguageHoverHalfExtent);
        Assert.Equal(
            RetailFrontendLanguageRow.SymmetryAxisX,
            (RetailMainMenuHitTest.LanguageHoverLeft + RetailMainMenuHitTest.LanguageHoverRight) * 0.5,
            9);

        Assert.True(RetailMainMenuHitTest.LanguageHoverContains(219f, 268f));
        Assert.True(RetailMainMenuHitTest.LanguageHoverContains(119f, 248f));
        Assert.False(RetailMainMenuHitTest.LanguageHoverContains(118.9f, 268f));
        Assert.False(RetailMainMenuHitTest.LanguageHoverContains(319f, 268f));
        Assert.False(RetailMainMenuHitTest.LanguageHoverContains(219f, 247.9f));
        Assert.False(RetailMainMenuHitTest.LanguageHoverContains(219f, 288f));
    }

    [Fact]
    public void LanguageHoverWritesMinusOneAndIsNotAConfirm()
    {
        // 0x00463274: a store of constant -1 to field +8 of edi. ButtonPressed stays FrontEnd.cpp:551.
        Assert.Equal(-1, RetailMainMenuHitTest.LanguageSelectedIndex);
        Assert.False(RetailMainMenuHitTest.IsButtonPressed);
        Assert.False(RetailMainMenuHitTest.IsSetLanguage);
    }

    [Fact]
    public void AlternateLanguageCenterYIsBehindAnImageInitialZeroFlag()
    {
        // 0x00462EE7 [0x0083D990] test / je keeps 268. Nonzero writes
        // 0x43980000 (304) at 0x00462EF5. pe_read_va: uninitialised .data.
        Assert.Equal(0x0083D990u, RetailMainMenuHitTest.LanguageCenterFlagGlobal);
        Assert.Equal(0u, RetailMainMenuHitTest.ImageInitialLanguageCenterFlag);
        Assert.Equal(304f, RetailMainMenuHitTest.AlternateLanguageHoverCenterY);
        Assert.Equal(268f, RetailMainMenuHitTest.LanguageHoverCenterYFor(0u));
        Assert.Equal(304f, RetailMainMenuHitTest.LanguageHoverCenterYFor(1u));
        Assert.True(RetailMainMenuHitTest.LanguageHoverContains(219f, 304f, 304f));
        Assert.False(RetailMainMenuHitTest.LanguageHoverContains(219f, 268f, 304f));
    }

    [Fact]
    public void TwinFadeCompareIsTheOppositePredicateAndIsNotThisHelper()
    {
        // 0x00463E8D: a float compare against the value at 0x005D8BB0, popping the result; a
        // bit test of ah against 1, then conditional branch to 0x00463FC7 (taken when equal).
        // C0 only: fall through when transition < 0.9. Not a bit test of ah against 0x41.
        Assert.False(RetailMainMenuHitTest.AcceptsTwinFade(0.9f));
        Assert.False(RetailMainMenuHitTest.AcceptsTwinFade(1f));
        Assert.True(RetailMainMenuHitTest.AcceptsTwinFade(0f));
        Assert.True(RetailMainMenuHitTest.AcceptsTwinFade(0.8999f));
        Assert.NotEqual(
            RetailMainMenuHitTest.AcceptsHitTest(0.5f),
            RetailMainMenuHitTest.AcceptsTwinFade(0.5f));
    }

    [Fact]
    public void HotspotMethodsDoNotCallTheHitTestHelper()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        Assert.DoesNotContain("RetailMainMenuHitTest", Slice(flow, "private void DrawClickToStart"));
        Assert.DoesNotContain("RetailMainMenuHitTest", Slice(flow, "private bool HandlePointerConfirm"));
        Assert.DoesNotContain("RetailMainMenuHitTest", Slice(flow, "private bool HandleKey"));
        Assert.DoesNotContain("RetailMainMenuHitTest", Slice(flow, "private void DrawLoading"));
        Assert.DoesNotContain("RetailMainMenuHitTest", Slice(flow, "private void DrawQuitConfirm"));
        Assert.DoesNotContain("TWIMTBP", flow);
    }

    [Fact]
    public void HandlePointerMotionWiresTheHitTestGate()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        string input = SliceUntil(flow, "public override void _Input", "public override void _Draw");
        string motion = Slice(flow, "private bool HandlePointerMotion");

        Assert.Contains("RetailMainMenuHitTest.SwallowsFrontendInput", input);
        Assert.DoesNotContain("if (_mainTransitionTime > 0)", input);
        Assert.Contains("RetailMainMenuHitTest.AcceptsHitTest", motion);
        Assert.Contains("RetailMainMenuHitTest.LanguageHoverContains", motion);
        Assert.DoesNotContain("Confirm()", motion);
        Assert.DoesNotContain("SetLanguage", motion);
        Assert.DoesNotContain("AcceptsTwinFade", motion);
        Assert.DoesNotContain("TWIMTBP", flow);
    }

    private static string Slice(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf("\n    private ", signature.Length, StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }

    private static string SliceUntil(string source, string signature, string endSignature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, signature);
        string rest = source[start..];
        int next = rest.IndexOf(endSignature, signature.Length, StringComparison.Ordinal);
        return next >= 0 ? rest[..next] : rest;
    }
}
