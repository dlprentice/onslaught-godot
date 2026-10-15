// SPDX-License-Identifier: GPL-3.0-or-later

using OnslaughtRebuild.GodotClient;

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins the remaining CFEPIntro::Process arm after the timer —
/// <c>0x0051B79E</c>–<c>0x0051B801</c> — recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> After <c>fstp [esi+0x18]</c> the timer add falls into
/// <c>mov eax,[0x00677614]; test / je 0x0051B801</c>. The already-shipped
/// mouse dispatch at <c>0x0051B801</c> is the skip target. Clock, idle
/// <c>-3</c>, and the full-window mouse rect are other helpers. This arm
/// is not those.</para>
///
/// <para>The three handshake dwords (<c>0x00677614</c>, <c>0x00677624</c>,
/// <c>0x0067762C</c>) and the stash (<c>0x00663058</c>) sit in uninitialised
/// <c>.data</c>, so the image-initial flag is 0. Cold Process therefore
/// jumps to the mouse dispatch. The mutations these cases kill are a
/// cold-path <c>-3</c> from this arm, a kind-0 reset of
/// <c>this+0x0C</c>/<c>this+0x10</c>, and inventing this handshake inside
/// <c>DrawClickToStart</c>.</para>
/// </summary>
public sealed class RetailClickToStartProcessTests
{
    [Fact]
    public void ImageInitialFlagSkipsTheEntireArm()
    {
        // 0x0051B79E mov eax,[0x00677614]; test eax,eax / je 0x0051B801.
        // pe_read_va: VA is in the uninitialised part of .data.
        Assert.Equal(0x00677614u, RetailClickToStartProcess.FlagGlobal);
        Assert.Equal(0u, RetailClickToStartProcess.ImageInitialFlag);
        Assert.False(RetailClickToStartProcess.ShouldEnter(0u));
        Assert.True(RetailClickToStartProcess.ShouldEnter(1u));
        Assert.True(RetailClickToStartProcess.ShouldEnter(0xFFFFFFFFu));

        RetailClickToStartProcess.Tick cold = RetailClickToStartProcess.Evaluate(0u, 0, 1);
        Assert.False(cold.WriteAttractResult);
        Assert.False(cold.StashPageField);
        Assert.False(cold.ResetPageFields);
        Assert.False(cold.ClearFlag);
        Assert.False(cold.WriteConsumedLatch);
    }

    [Fact]
    public void KindZeroWithLatchOneWritesMinusThreeAndStashesThisPlusFourteen()
    {
        // 0x0051B7A7 [0x00677624]==0 → 0x0051B7BA load [0x0067762C],
        // store ecx=-1, dec / jne 0x0051B7DB. Latch==1 falls through:
        // mov edx,[esi+0x14]; mov [0x008A956C], -3; mov [0x00663058], edx.
        Assert.Equal(0x00677624u, RetailClickToStartProcess.KindGlobal);
        Assert.Equal(0x0067762Cu, RetailClickToStartProcess.LatchGlobal);
        Assert.Equal(0x00663058u, RetailClickToStartProcess.StashGlobal);
        Assert.Equal(0x008A956Cu, RetailClickToStartProcess.ResultGlobal);
        Assert.Equal(0, RetailClickToStartProcess.KindZero);
        Assert.Equal(1, RetailClickToStartProcess.LatchReady);
        Assert.Equal(-3, RetailClickToStartProcess.AttractResult);
        Assert.Equal(0x14, RetailClickToStartProcess.StashFieldOffset);
        Assert.Equal(RetailClickToStartPrompt.IdleResult, RetailClickToStartProcess.AttractResult);

        RetailClickToStartProcess.Tick tick = RetailClickToStartProcess.Evaluate(1u, 0, 1);
        Assert.True(tick.WriteAttractResult);
        Assert.True(tick.StashPageField);
        Assert.True(tick.WriteConsumedLatch);
        Assert.Equal(-1, RetailClickToStartProcess.LatchConsumed);
    }

    [Fact]
    public void KindZeroDoesNotResetPageFieldsBecauseTheLatchIsAlreadyMinusOne()
    {
        // After the kind-0 store at 0x0051B7BF, fall-through 0x0051B7DB
        // reloads [0x0067762C] (now -1), stores -1 again, dec / jne
        // 0x0051B801. The this+0x0C / this+0x10 / flag-clear writes are
        // skipped. Kind 0 is not kind 10.
        RetailClickToStartProcess.Tick ready = RetailClickToStartProcess.Evaluate(1u, 0, 1);
        Assert.False(ready.ResetPageFields);
        Assert.False(ready.ClearFlag);

        RetailClickToStartProcess.Tick idle = RetailClickToStartProcess.Evaluate(1u, 0, 0);
        Assert.False(idle.WriteAttractResult);
        Assert.False(idle.StashPageField);
        Assert.False(idle.ResetPageFields);
        Assert.False(idle.ClearFlag);
        Assert.True(idle.WriteConsumedLatch);
    }

    [Fact]
    public void KindTenWithLatchOneResetsPageFieldsAndClearsTheFlag()
    {
        // 0x0051B7B3 cmp eax, 0x0A / je 0x0051B7DB. That site loads the
        // live latch (not the kind-0 -1), stores -1, dec / jne skip.
        // Latch==1: mov [esi+0x0C], 0; mov [esi+0x10], 1;
        // mov [0x00677614], 0. No -3. No stash.
        Assert.Equal(10, RetailClickToStartProcess.KindTen);
        Assert.Equal(0x0C, RetailClickToStartProcess.SubstateOffset);
        Assert.Equal(0, RetailClickToStartProcess.SubstateReset);
        Assert.Equal(0x10, RetailClickToStartProcess.Field10Offset);
        Assert.Equal(1, RetailClickToStartProcess.Field10Set);

        RetailClickToStartProcess.Tick tick = RetailClickToStartProcess.Evaluate(1u, 10, 1);
        Assert.False(tick.WriteAttractResult);
        Assert.False(tick.StashPageField);
        Assert.True(tick.ResetPageFields);
        Assert.True(tick.ClearFlag);
        Assert.True(tick.WriteConsumedLatch);

        RetailClickToStartProcess.Tick unread = RetailClickToStartProcess.Evaluate(1u, 10, 0);
        Assert.False(unread.ResetPageFields);
        Assert.False(unread.ClearFlag);
        Assert.True(unread.WriteConsumedLatch);
    }

    [Fact]
    public void OtherKindsSkipWithoutConsumingTheLatch()
    {
        // 0x0051B7B3 cmp 0x0A / je 0x0051B7DB; else jmp 0x0051B801.
        // Kind 1/6/8/11 are other pages' compares, not this arm.
        foreach (int kind in new[] { 1, 6, 8, 11, -1 })
        {
            RetailClickToStartProcess.Tick tick = RetailClickToStartProcess.Evaluate(1u, kind, 1);
            Assert.False(tick.WriteAttractResult);
            Assert.False(tick.StashPageField);
            Assert.False(tick.ResetPageFields);
            Assert.False(tick.ClearFlag);
            Assert.False(tick.WriteConsumedLatch);
        }
    }

    [Fact]
    public void DrawClickToStartDoesNotInventTheColdHandshake()
    {
        // Image-initial flag is 0. This arm is Process, not Render.
        // DrawClickToStart must not emit the -3 / stash / page reset.
        Assert.False(
            RetailClickToStartProcess.ShouldEnter(RetailClickToStartProcess.ImageInitialFlag));

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
        Assert.DoesNotContain("RetailClickToStartProcess", body);
        Assert.DoesNotContain("WriteAttractResult", body);
        Assert.DoesNotContain("vectorlosttoyssplash", flow);
        Assert.DoesNotContain("TWIMTBP", flow);

        Assert.DoesNotContain(
            "RetailClickToStartProcess",
            Slice(flow, "private void DrawLoading()"));
        Assert.DoesNotContain(
            "RetailClickToStartProcess",
            Slice(flow, "private void DrawQuitConfirm()"));
        Assert.DoesNotContain(
            "RetailClickToStartProcess",
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
