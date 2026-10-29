// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CApplyMenuItem::Render</c> packed colour — recovered from the pristine
/// specimen (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Gate.</b> <c>0x004A4310</c> is a load of the value at 0x00704A88 into al.
/// A zero test of al, branching to 0x004A4373 when equal skips the cosine. The skip arm keeps
/// the prologue (an or of 0xFFFFFFFF into esi) and forwards that colour to
/// <c>CMenuItem__Render</c> <c>0x004A32C0</c>. The byte is the same
/// <c>0x00704A88</c> <see cref="RetailOptionsMenu.HasPendingChanges"/>
/// already names: any deferred row whose current index is not its
/// committed index. This helper does not set that flag.</para>
///
/// <para><b>Clock.</b> The hit arm is a load of constant 0x0088A0A8 into ecx,
/// then direct call to 0x005159E0 (<c>PLATFORM__GetSysTimeFloat</c>), then
/// a double load of the 64-bit value at 0x005DB488 (image bits <c>0x4000000000000000</c> =
/// 2.0) and a direct call to 0x0055E3EA. That body is
/// a load of constant 0x00653330 into edx, then jump to 0x00563A10
/// (<c>CRT__FpuIntrinsicDispatch2Thunk</c> → <c>__cintrindisp2</c>). The
/// table at <c>0x00653330</c> begins with the name <c>"fmod"</c>
/// and slot 0 is <c>0x0055E3F4</c> (<c>fxch / fprem</c>). ST0 is therefore
/// <c>fmod(time, 2.0)</c>. Absolute phase is a launch-time value; this
/// type takes elapsed seconds and does not invent a start angle.</para>
///
/// <para><b>Colour.</b> a float multiply by the value at 0x005D85E0 (0x40C90FDB = 2π) /
/// <c>fcos</c> / a float add of the value at 0x005D8568 (1.0) /
/// a float multiply by the value at 0x005D85EC (0.5) / a float multiply by the value at 0x005D8C70 (255.0) /
/// a float-to-integer store to 64-bit stack local +8. EAX is seeded 0xFF; the channel is
/// <c>0xFF − rounded</c>. Then
/// <c>((ch | 0xFFFFFF00) &lt;&lt; 8 | ch) &lt;&lt; 8 | ch</c> —
/// <c>0xFFcccccc</c>. DrawOptions is the consumer. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, StartRetailStartupMedia, and the cursor stay
/// untouched. This is not the 0x00463E8D twin fade and not
/// <c>SetLanguage</c>.</para>
/// </summary>
public static class RetailOptionsApplyPulse
{
    /// <summary>A load of the value at 0x00704A88 into al at <c>0x004A4310</c>.</summary>
    public const uint PendingGateGlobal = 0x00704A88u;

    /// <summary>Prologue (an or of 0xFFFFFFFF into esi) at <c>0x004A4319</c>.</summary>
    public const uint IdlePackedColor = 0xFFFFFFFFu;

    /// <summary>A double load of the 64-bit value at 0x005DB488 at <c>0x004A432D</c>.</summary>
    public const uint PeriodGlobal = 0x005DB488u;

    /// <summary>Image bits at <c>0x005DB488</c>.</summary>
    public const ulong PeriodBits = 0x4000000000000000UL;

    /// <summary>The <see cref="PeriodBits"/> qword, not a second 2.0 literal.</summary>
    public static double PeriodSeconds => BitConverter.Int64BitsToDouble(unchecked((long)PeriodBits));

    /// <summary>A float multiply by the value at 0x005D85E0 at <c>0x004A4338</c>.</summary>
    public const uint TwoPiGlobal = 0x005D85E0u;

    /// <summary>Image bits at <c>0x005D85E0</c>.</summary>
    public const uint TwoPiBits = 0x40C90FDBu;

    /// <summary>The <see cref="TwoPiBits"/> dword.</summary>
    public static float TwoPi => BitConverter.UInt32BitsToSingle(TwoPiBits);

    /// <summary>A float add of the value at 0x005D8568 at <c>0x004A4345</c>.</summary>
    public const uint OneGlobal = 0x005D8568u;

    /// <summary>Image bits at <c>0x005D8568</c>.</summary>
    public const uint OneBits = 0x3F800000u;

    /// <summary>The <see cref="OneBits"/> dword.</summary>
    public static float One => BitConverter.UInt32BitsToSingle(OneBits);

    /// <summary>A float multiply by the value at 0x005D85EC at <c>0x004A434B</c>.</summary>
    public const uint HalfGlobal = 0x005D85ECu;

    /// <summary>Image bits at <c>0x005D85EC</c>.</summary>
    public const uint HalfBits = 0x3F000000u;

    /// <summary>The <see cref="HalfBits"/> dword.</summary>
    public static float Half => BitConverter.UInt32BitsToSingle(HalfBits);

    /// <summary>A float multiply by the value at 0x005D8C70 at <c>0x004A4351</c>.</summary>
    public const uint Scale255Global = 0x005D8C70u;

    /// <summary>Image bits at <c>0x005D8C70</c>.</summary>
    public const uint Scale255Bits = 0x437F0000u;

    /// <summary>The <see cref="Scale255Bits"/> dword.</summary>
    public static float Scale255 => BitConverter.UInt32BitsToSingle(Scale255Bits);

    /// <summary>
    /// A compare of field +0x1C of esi with field +0x20 of esi, branching to 0x004A3CBD when equal at
    /// <c>0x004A3C69</c> in <c>CMenuItemDropdown::Render</c>. Committed
    /// versus current after the optional GET resync. The cosine that
    /// follows is this same pack, not a second formula.
    /// </summary>
    public const uint DropdownCompareSite = 0x004A3C69u;

    /// <summary>
    /// A zero test of al after the <c>0x00704A88</c> load. Pending is
    /// the reconstructed flag, not a second reader of the BSS byte.
    /// </summary>
    public static bool ShouldPulse(bool pending) => pending;

    /// <summary>
    /// Per-row dropdown gate. Not <c>+0x25</c> — that byte is
    /// <c>HasPendingSelectionChange</c> only. Render compares the two
    /// selection dwords.
    /// </summary>
    public static bool DropdownRowIsPending(int committed, int current) =>
        committed != current;

    /// <summary>
    /// Grey channel after <c>fistp</c> and a subtraction of ecx from eax. Time is
    /// <c>GetSysTimeFloat</c> seconds; only the non-negative arm is used
    /// here because the frontend clock never goes backwards.
    /// </summary>
    public static int Channel(float seconds)
    {
        double period = PeriodSeconds;
        double wrapped = seconds - (period * Math.Floor(seconds / period));
        double pulse = ((Math.Cos(wrapped * TwoPi) + One) * Half) * Scale255;
        int rounded = (int)Math.Round(pulse, MidpointRounding.ToEven);
        return 0xFF - Math.Clamp(rounded, 0, 0xFF);
    }

    /// <summary>
    /// Packed ARGB forwarded to <c>CMenuItem__Render</c>. Idle is −1.
    /// Pending is <c>0xFFcccccc</c> for the channel at
    /// <paramref name="seconds"/>.
    /// </summary>
    public static uint PackedColor(bool pending, float seconds)
    {
        if (!pending)
        {
            return IdlePackedColor;
        }

        int channel = Channel(seconds);
        uint packed = (uint)channel;
        packed |= 0xFFFFFF00u;
        packed <<= 8;
        packed |= (uint)channel;
        packed <<= 8;
        packed |= (uint)channel;
        return packed;
    }
}
