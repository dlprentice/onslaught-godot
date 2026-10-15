// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the CFEPIntro click-to-start input law recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Action.</b> <c>CFEPIntro</c> handler <c>0x0051B660</c>–<c>0x0051B6AA</c>
/// (<c>RET 8</c>): <c>cmp [esp+4], 0x2C / jne ret</c>, then
/// <c>mov eax,[ecx+0x0C]; test/jnz ret</c>. Only action <c>0x2C</c> with
/// page substate 0 continues. Cold-start globals take
/// <c>push 0x32; push 0; call CFrontEnd::SetPage 0x00466AE0</c>
/// (<c>0x0051B686</c>/<c>0x0051B690</c>).</para>
///
/// <para><b>Default keys.</b> <c>OptionsEntries__InitDefaultSingleBindingsTable</c>
/// <c>0x00514210</c> issues two <c>KEY_ONCE=8</c> rows for action <c>0x2C</c>:
/// DIK <c>0x1C</c> Enter at <c>0x00514448</c> and DIK <c>0x39</c> Space at
/// <c>0x0051445C</c>. Escape and Numpad Enter are not those rows.</para>
///
/// <para><b>Mouse.</b> <c>CFEPIntro::Process</c> <c>0x0051B801</c> pushes
/// <c>0x2C</c>, calls <c>PLATFORM__GetWindowWidth 0x00515940</c> twice (not
/// <c>GetWindowHeight 0x00515B00</c>), then
/// <c>CFrontEnd__ProcessMouseReadyOrDispatch 0x00469390</c> with
/// <c>(0, 0, width, width, 0x2C)</c>. The consume path at <c>0x00523BC0</c>
/// hit-tests <c>[0x0089BDA8]</c>/<c>[0x0089BDA4]</c> against that rect
/// (<c>left &lt;= x &lt; width</c> and <c>top &lt;= y &lt; width</c>) and
/// dispatches action <c>0x2C</c> through <c>0x004669A0</c>. There is no
/// glyph, splash, or title hit-rect.</para>
///
/// <para>The mutations these cases kill are a prompt/logo hit-rect and a
/// keyboard-only rewrite of <c>HandlePointerConfirm</c>.</para>
/// </summary>
public sealed class RetailClickToStartInputTests
{
    [Fact]
    public void OnlyActionZeroX2CWithZeroSubstateIsAccepted()
    {
        // 0x0051B660 cmp [esp+4], 0x2C / jne ret; 0x0051B667 [ecx+0x0C] test/jnz.
        Assert.Equal(0x2C, RetailClickToStartInput.ConfirmAction);
        Assert.True(RetailClickToStartInput.AcceptsAction(0x2C, pageSubstate: 0));
        Assert.False(RetailClickToStartInput.AcceptsAction(0x2C, pageSubstate: 1));
        Assert.False(RetailClickToStartInput.AcceptsAction(0, pageSubstate: 0));
        Assert.False(RetailClickToStartInput.AcceptsAction(7, pageSubstate: 0));
        Assert.False(RetailClickToStartInput.AcceptsAction(0x33, pageSubstate: 0));
        Assert.False(RetailClickToStartInput.AcceptsAction(0x3A, pageSubstate: 0));
    }

    [Fact]
    public void ColdStartSetPageIsFepMainOverFiftyFrames()
    {
        // 0x0051B686 push 0x32; 0x0051B690 push 0; call 0x00466AE0.
        // The other two SetPage pairs (page 0x16 / page 0x14) are gated on
        // [0x0083D448] and [0x008A9AB4] and are not this cold-start path.
        Assert.Equal(0, RetailClickToStartInput.SetPageOrdinal);
        Assert.Equal(50, RetailClickToStartInput.SetPageFrames);
    }

    [Fact]
    public void DefaultConfirmScanCodesAreEnterAndSpaceOnly()
    {
        // 0x00514448 push 0x1C; 0x0051445C push 0x39. KEY_ONCE=8, action 0x2C.
        Assert.Equal(new[] { 0x1C, 0x39 }, RetailClickToStartInput.DefaultConfirmScanCodes);
        Assert.True(RetailClickToStartInput.AcceptsDefaultConfirmScanCode(0x1C));
        Assert.True(RetailClickToStartInput.AcceptsDefaultConfirmScanCode(0x39));
        Assert.False(RetailClickToStartInput.AcceptsDefaultConfirmScanCode(0x01));
        Assert.False(RetailClickToStartInput.AcceptsDefaultConfirmScanCode(0x9C));
        Assert.False(RetailClickToStartInput.AcceptsDefaultConfirmScanCode(0x1E));
    }

    [Fact]
    public void MouseRectIsTheWindowOriginByWindowWidthNotAGlyphBox()
    {
        // 0x0051B82F push 0; push 0; both extents are GetWindowWidth 0x00515940.
        Assert.Equal(0f, RetailClickToStartInput.MouseRectLeft);
        Assert.Equal(0f, RetailClickToStartInput.MouseRectTop);
        Assert.True(RetailClickToStartInput.MouseRectUsesWindowWidthForBothExtents);
        Assert.False(RetailClickToStartInput.HasGlyphHitRect);

        // 640-class stage is inside (0,0)-(width,width) on any landscape client.
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(0f, 0f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(320f, 240f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(639f, 479f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(10f, 10f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(124f, -6f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(250f, 290f));
        Assert.True(RetailClickToStartInput.AcceptsMouseAt(320f, 400f));
    }

    [Fact]
    public void HandlePointerConfirmCallsTheFullWindowMousePredicate()
    {
        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));

        int handle = flow.IndexOf("private bool HandlePointerConfirm", StringComparison.Ordinal);
        Assert.True(handle >= 0);
        string handleBody = flow[handle..];
        int click = handleBody.IndexOf(
            "case RetailFrontendScreen.ClickToStart:",
            StringComparison.Ordinal);
        Assert.True(click >= 0);
        string arm = handleBody[click..];
        const string casePrefix = "case RetailFrontendScreen.ClickToStart:";
        int next = arm.IndexOf("case RetailFrontendScreen.", casePrefix.Length, StringComparison.Ordinal);
        if (next >= 0)
        {
            arm = arm[..next];
        }

        // DrawClickToStart also has a ClickToStart case; this arm is the
        // pointer owner. A prompt/logo HasPoint here must fail.
        Assert.Contains("RetailFrontendScenePath.AcceptsClickToStartMouse", arm);
        Assert.DoesNotContain("HasPoint(design)", arm);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);
    }
}
