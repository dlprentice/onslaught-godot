// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the unread CFEPIntro::Process head —
/// <c>0x0051B6B0</c>–<c>0x0051B705</c> — recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> Clock, 30 s idle <c>-3</c>, the 0x00677614
/// handshake, and the full-window mouse rect are other helpers. This is
/// the unread prologue in front of them. Not a fade. Not pixels.</para>
///
/// <para><b>Early-out.</b> <c>mov eax,[esp+4]; test / jne 0x0051B83B</c>
/// pops esi and <c>RET 4</c>. A nonzero stack arg skips HUD, ResetFlags,
/// the 3 s dispatch, idle, clock, handshake, and mouse.</para>
///
/// <para><b>HUD.</b> Zero-arg fall-through does
/// <c>mov ecx, 0x00704858; call 0x0041A200</c>. That body is
/// <c>xor al, al; ret</c>, so <c>test al,al / je 0x0051B6D4</c> never
/// reaches ResetFlags <c>0x0051B610</c> from this site.</para>
///
/// <para><b>Dev-mode confirm.</b> <c>0x00662DF4</c> is uninitialised
/// <c>.data</c> (image-initial 0). Nonzero plus
/// <c>GetTime()-[this+4]</c> strictly greater than <c>3.0f</c> at
/// <c>0x005D8CC0</c> (<c>test ah,0x41 / jne skip</c>) then
/// <c>CALL [vtable+0x0C]</c> with action <c>0x2C</c> and
/// <c>0x3F800000</c> (1.0f). Cold Process therefore never auto-confirms.
/// The mutations these cases kill are a 3 s click-to-start skip on the
/// cold path, inventing ResetFlags from a live HUD, and wiring this head
/// into <c>DrawClickToStart</c>.</para>
/// </summary>
public sealed class RetailClickToStartProcessHeadTests
{
    [Fact]
    public void NonzeroStackArgReturnsImmediately()
    {
        // 0x0051B6B0 mov eax,[esp+4]; test eax,eax / jne 0x0051B83B.
        Assert.Equal(0x0051B6B0u, RetailClickToStartProcessHead.ProcessVa);
        Assert.Equal(0x0051B83Bu, RetailClickToStartProcessHead.EarlyOutVa);
        Assert.Equal(4, RetailClickToStartProcessHead.StackArgBytes);

        RetailClickToStartProcessHead.Tick skip = RetailClickToStartProcessHead.Evaluate(
            processState: 1,
            showHud: true,
            devMode: 1u,
            pageSeconds: 99d);
        Assert.True(skip.ReturnedImmediately);
        Assert.False(skip.CalledResetFlags);
        Assert.False(skip.DispatchConfirm);
    }

    [Fact]
    public void ImageGetShowHudIsXorAlAlSoResetFlagsIsSkipped()
    {
        // 0x0051B6BF mov ecx, 0x00704858; call 0x0041A200.
        // 0x0041A200 xor al, al; ret. test al,al / je 0x0051B6D4.
        Assert.Equal(0x0041A200u, RetailClickToStartProcessHead.GetShowHudVa);
        Assert.Equal(0x00704858u, RetailClickToStartProcessHead.ShowHudObject);
        Assert.Equal(0x0051B610u, RetailClickToStartProcessHead.ResetFlagsVa);
        Assert.False(RetailClickToStartProcessHead.ImageGetShowHud);

        RetailClickToStartProcessHead.Tick cold = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: RetailClickToStartProcessHead.ImageGetShowHud,
            devMode: 0u,
            pageSeconds: 0d);
        Assert.False(cold.ReturnedImmediately);
        Assert.False(cold.CalledResetFlags);
        Assert.False(cold.DispatchConfirm);
    }

    [Fact]
    public void ShowHudTrueWouldCallResetFlags()
    {
        // Counterfactual of test al,al / je. This image never takes it.
        RetailClickToStartProcessHead.Tick tick = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: true,
            devMode: 0u,
            pageSeconds: 0d);
        Assert.False(tick.ReturnedImmediately);
        Assert.True(tick.CalledResetFlags);
        Assert.False(tick.DispatchConfirm);
    }

    [Fact]
    public void ImageInitialDevModeSkipsTheThreeSecondConfirm()
    {
        // 0x0051B6D4 mov eax,[0x00662DF4]; test / je 0x0051B705.
        // pe_read_va: VA is in the uninitialised part of .data.
        Assert.Equal(0x00662DF4u, RetailClickToStartProcessHead.DevModeGlobal);
        Assert.Equal(0u, RetailClickToStartProcessHead.ImageInitialDevMode);
        Assert.Equal(0x005159E0u, RetailClickToStartProcessHead.GetTimeVa);
        Assert.Equal(0x0088A0A8u, RetailClickToStartProcessHead.GetTimeObject);
        Assert.Equal(0x005D8CC0u, RetailClickToStartProcessHead.ThreeSecondsVa);
        Assert.Equal(3.0, RetailClickToStartProcessHead.AutoConfirmSeconds);
        Assert.Equal(0x0051B705u, RetailClickToStartProcessHead.HeadEndVa);

        RetailClickToStartProcessHead.Tick cold = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: false,
            devMode: RetailClickToStartProcessHead.ImageInitialDevMode,
            pageSeconds: 99d);
        Assert.False(cold.DispatchConfirm);
    }

    [Fact]
    public void DevModeAfterThreeSecondsDispatchesAction2CFloatOne()
    {
        // 0x0051B6EA fcomp [0x005D8CC0]=3.0f; test ah,0x41 / jne 0x0051B705.
        // Then push 0x3F800000, push 0x2C, call [esi]+0x0C.
        Assert.Equal(0x2C, RetailClickToStartProcessHead.ConfirmAction);
        Assert.Equal(RetailClickToStartInput.ConfirmAction, RetailClickToStartProcessHead.ConfirmAction);
        Assert.Equal(0x3F800000u, RetailClickToStartProcessHead.ConfirmValueBits);
        Assert.Equal(1f, BitConverter.UInt32BitsToSingle(RetailClickToStartProcessHead.ConfirmValueBits));
        Assert.Equal(0x0C, RetailClickToStartProcessHead.VtableSlotOffset);

        RetailClickToStartProcessHead.Tick tick = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: false,
            devMode: 1u,
            pageSeconds: 3.0d + 1e-6);
        Assert.False(tick.ReturnedImmediately);
        Assert.False(tick.CalledResetFlags);
        Assert.True(tick.DispatchConfirm);

        RetailClickToStartProcessHead.Tick exact = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: false,
            devMode: 1u,
            pageSeconds: 3.0d);
        Assert.False(exact.DispatchConfirm);

        RetailClickToStartProcessHead.Tick unread = RetailClickToStartProcessHead.Evaluate(
            processState: 0,
            showHud: false,
            devMode: 1u,
            pageSeconds: 2.9d);
        Assert.False(unread.DispatchConfirm);
    }

    [Fact]
    public void DrawClickToStartDoesNotInventTheColdHead()
    {
        // Image HUD is false. Image-initial 0x00662DF4 is 0. This head
        // is Process, not Render. DrawClickToStart must not emit ResetFlags
        // or a 3 s action-0x2C dispatch.
        Assert.False(RetailClickToStartProcessHead.ImageGetShowHud);
        Assert.False(
            RetailClickToStartProcessHead.Evaluate(
                processState: 0,
                showHud: RetailClickToStartProcessHead.ImageGetShowHud,
                devMode: RetailClickToStartProcessHead.ImageInitialDevMode,
                pageSeconds: 99d).DispatchConfirm);

        string flow = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "godot-pause-source", "RetailFrontendFlow.cs"));
        int start = flow.IndexOf("private void DrawClickToStart()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        string body = flow[start..];
        int next = body.IndexOf("\n    private ", 1, StringComparison.Ordinal);
        if (next >= 0)
        {
            body = body[..next];
        }

        Assert.Contains("RetailClickToStartTitle.ShouldDrawSixth", body);
        Assert.DoesNotContain("RetailClickToStartProcessHead", body);
        Assert.DoesNotContain("DispatchConfirm", body);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);

        Assert.DoesNotContain(
            "RetailClickToStartProcessHead",
            Slice(flow, "private void DrawLoading()"));
        Assert.DoesNotContain(
            "RetailClickToStartProcessHead",
            Slice(flow, "private void DrawQuitConfirm()"));
        Assert.DoesNotContain(
            "RetailClickToStartProcessHead",
            Slice(flow, "private bool HandleKey("));
    }

    private static string Slice(string flow, string marker)
    {
        int start = flow.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        string body = flow[start..];
        int next = body.IndexOf("\n    private ", 1, StringComparison.Ordinal);
        return next >= 0 ? body[..next] : body;
    }
}
