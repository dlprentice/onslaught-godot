// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client.Tests;

/// <summary>
/// Pins <c>CApplyMenuItem::Render</c> at <c>0x004A4310</c>–<c>0x004A4399</c>,
/// recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Gate.</b> <c>mov al, [0x00704A88]; test al, al; je 0x004A4373</c>.
/// The miss arm leaves ESI = −1 (<c>or esi, 0xFFFFFFFF</c> at
/// <c>0x004A4319</c>) and forwards that packed colour to
/// <c>CMenuItem__Render</c> <c>0x004A32C0</c>. The hit arm is the cosine
/// below. <c>0x00704A88</c> is the page-wide pending flag
/// <see cref="RetailOptionsMenu.HasPendingChanges"/> already pins.</para>
///
/// <para><b>Clock.</b> <c>mov ecx, 0x0088A0A8; call 0x005159E0</c> is
/// <c>PLATFORM__GetSysTimeFloat</c>. Then <c>fld qword [0x005DB488]</c>
/// (2.0) and <c>call 0x0055E3EA</c>. That thunk is
/// <c>CRT__FpuIntrinsicDispatch2Thunk</c>: <c>mov edx, 0x00653330 / jmp
/// 0x00563A10</c>. The table at <c>0x00653330</c> begins
/// <c>04 66 6D 6F 64</c> (<c>"fmod"</c>) and its first slot is
/// <c>0x0055E3F4</c> (<c>fFMOD</c>, <c>fxch / fprem</c>). So
/// ST0 = fmod(time, 2.0).</para>
///
/// <para><b>Colour.</b> <c>fmul [0x005D85E0]</c> (2π) /
/// <c>fcos</c> / <c>fadd [0x005D8568]</c> (1.0) /
/// <c>fmul [0x005D85EC]</c> (0.5) / <c>fmul [0x005D8C70]</c> (255.0) /
/// <c>fistp</c>. EAX starts at 0xFF; ESI = 0xFF − rounded pulse, then
/// <c>((esi | 0xFFFFFF00) &lt;&lt; 8 | ch) &lt;&lt; 8 | ch</c>. That is
/// <c>0xFFcccccc</c>. This helper does not invent the 0x00463E8D twin
/// fade and does not call <c>SetLanguage</c>.</para>
/// </summary>
public sealed class RetailOptionsApplyPulseTests
{
    [Fact]
    public void IdlePackedColourIsMinusOne()
    {
        Assert.Equal(0x00704A88u, RetailOptionsApplyPulse.PendingGateGlobal);
        Assert.Equal(0xFFFFFFFFu, RetailOptionsApplyPulse.IdlePackedColor);
        Assert.False(RetailOptionsApplyPulse.ShouldPulse(pending: false));
        Assert.True(RetailOptionsApplyPulse.ShouldPulse(pending: true));
        Assert.Equal(0xFFFFFFFFu, RetailOptionsApplyPulse.PackedColor(pending: false, 0f));
        Assert.Equal(0xFFFFFFFFu, RetailOptionsApplyPulse.PackedColor(pending: false, 0.5f));
    }

    [Fact]
    public void SpecimenFloatPoolIsTwoTwoPiOneHalfAndTwoHundredFiftyFive()
    {
        Assert.Equal(0x005DB488u, RetailOptionsApplyPulse.PeriodGlobal);
        Assert.Equal(0x4000000000000000UL, RetailOptionsApplyPulse.PeriodBits);
        Assert.Equal(2.0, RetailOptionsApplyPulse.PeriodSeconds);

        Assert.Equal(0x005D85E0u, RetailOptionsApplyPulse.TwoPiGlobal);
        Assert.Equal(0x40C90FDBu, RetailOptionsApplyPulse.TwoPiBits);
        Assert.Equal(BitConverter.UInt32BitsToSingle(0x40C90FDBu), RetailOptionsApplyPulse.TwoPi);

        Assert.Equal(0x005D8568u, RetailOptionsApplyPulse.OneGlobal);
        Assert.Equal(0x3F800000u, RetailOptionsApplyPulse.OneBits);

        Assert.Equal(0x005D85ECu, RetailOptionsApplyPulse.HalfGlobal);
        Assert.Equal(0x3F000000u, RetailOptionsApplyPulse.HalfBits);

        Assert.Equal(0x005D8C70u, RetailOptionsApplyPulse.Scale255Global);
        Assert.Equal(0x437F0000u, RetailOptionsApplyPulse.Scale255Bits);
        Assert.Equal(255f, RetailOptionsApplyPulse.Scale255);
    }

    [Fact]
    public void PendingCosineIsBlackAtMultiplesOfOneAndWhiteAtHalf()
    {
        // fmod(t, 2) * 2π; cos 1 → pulse 255 → ch 0; cos −1 → pulse 0 → ch 255.
        Assert.Equal(0, RetailOptionsApplyPulse.Channel(0f));
        Assert.Equal(255, RetailOptionsApplyPulse.Channel(0.5f));
        Assert.Equal(0, RetailOptionsApplyPulse.Channel(1f));
        Assert.Equal(255, RetailOptionsApplyPulse.Channel(1.5f));
        Assert.Equal(0, RetailOptionsApplyPulse.Channel(2f));

        Assert.Equal(0xFF000000u, RetailOptionsApplyPulse.PackedColor(pending: true, 0f));
        Assert.Equal(0xFFFFFFFFu, RetailOptionsApplyPulse.PackedColor(pending: true, 0.5f));
        Assert.Equal(
            RetailOptionsApplyPulse.PackedColor(pending: true, 0.25f),
            RetailOptionsApplyPulse.PackedColor(pending: true, 2.25f));
    }

    [Fact]
    public void DropdownPulseIsTheSameCosineGatedOnCommittedVersusCurrent()
    {
        // CMenuItemDropdown::Render 0x004A3C69: cmp [esi+0x1c],[esi+0x20] / je.
        // The cosine that follows is the Apply pack, not a second formula.
        Assert.Equal(0x004A3C69u, RetailOptionsApplyPulse.DropdownCompareSite);
        Assert.False(RetailOptionsApplyPulse.DropdownRowIsPending(3, 3));
        Assert.True(RetailOptionsApplyPulse.DropdownRowIsPending(3, 4));
        Assert.Equal(
            RetailOptionsApplyPulse.PackedColor(pending: true, 0.25f),
            RetailOptionsApplyPulse.PackedColor(
                RetailOptionsApplyPulse.DropdownRowIsPending(0, 1),
                0.25f));
        Assert.Equal(
            0xFFFFFFFFu,
            RetailOptionsApplyPulse.PackedColor(
                RetailOptionsApplyPulse.DropdownRowIsPending(2, 2),
                0.25f));
    }

    [Fact]
    public void DrawOptionRowUsesThePulseWhenApplyIsPending()
    {
        string options = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "godot-pause-source",
            "RetailFrontendFlow.Options.cs"));
        string draw = Slice(options, "private void DrawOptionRow");
        Assert.Contains("RetailOptionsApplyPulse.PackedColor", draw, StringComparison.Ordinal);
        Assert.Contains("RetailOptionsAction.Apply", draw, StringComparison.Ordinal);
        Assert.Contains("HasPendingChanges", draw, StringComparison.Ordinal);
        Assert.Contains("DropdownRowIsPending", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandleKey", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawLoading", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("DrawQuitConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("HandlePointerConfirm", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetLanguage", draw, StringComparison.Ordinal);
    }

    private static string Slice(string source, string header)
    {
        int start = source.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, header);
        int next = source.IndexOf("\n    private ", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }
}
