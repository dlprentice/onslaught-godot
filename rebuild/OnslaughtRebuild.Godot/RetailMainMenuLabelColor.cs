// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render main-menu label colour — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Bases.</b> <c>0x0046300B</c> is
/// <c>mov ebp, 0xFF4F4F4F</c>. <c>0x00463010</c> is
/// <c>call [edx+0x24] / test eax, eax / jnz</c>; the zero arm at
/// <c>0x00463017</c> is <c>mov ebp, 0x7F1F1F1F</c>.
/// <c>0x0046301C</c> is <c>cmp ebx, [edi+8] / jnz</c>; the hit arm
/// at <c>0x00463021</c> is <c>mov ebp, 0xFFFF6F3F</c> and wins.
/// That call is not named here. Not <c>SetLanguage</c>.</para>
///
/// <para><b>Fade.</b> ESI is the same 0x00462ECA 0..255 fade byte.
/// <c>0x00463032</c> / <c>0x00463049</c> are <c>imul ecx/eax, esi</c>
/// then <c>xor / and 0x00FFFFFF / xor</c>. The draw unpack at
/// <c>0x00463055</c> is the chevron-style
/// <c>shr 8 / *255 / xor / and / xor</c>. Settled ESI 255 submits
/// <c>0xFD4F4F4F</c> / <c>0x7D1F1F1F</c> / <c>0xFDFF6F3F</c> —
/// frame 3000 draws 12–25. DrawMainMenu is the consumer. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the sine pin, the blink, and the
/// chevron colour stay untouched.</para>
/// </summary>
public static class RetailMainMenuLabelColor
{
    /// <summary><c>mov ebp, 0xFF4F4F4F</c> at <c>0x0046300B</c>.</summary>
    public const uint IdleSite = 0x0046300Bu;

    /// <summary><c>mov ebp, 0x7F1F1F1F</c> at <c>0x00463017</c>.</summary>
    public const uint DisabledSite = 0x00463017u;

    /// <summary><c>mov ebp, 0xFFFF6F3F</c> at <c>0x00463021</c>.</summary>
    public const uint SelectedSite = 0x00463021u;

    /// <summary>The idle immediate.</summary>
    public const uint IdlePackedColor = 0xFF4F4F4Fu;

    /// <summary>The zero-arm immediate.</summary>
    public const uint DisabledPackedColor = 0x7F1F1F1Fu;

    /// <summary>The selected-row immediate. Wins over disabled.</summary>
    public const uint SelectedPackedColor = 0xFFFF6F3Fu;

    /// <summary>Frame 3000 live-row body, e.g. draw 17.</summary>
    public const uint CaptureIdle = 0xFD4F4F4Fu;

    /// <summary>Frame 3000 disabled body, draws 14/15.</summary>
    public const uint CaptureDisabled = 0x7D1F1F1Fu;

    /// <summary>Frame 3000 selected body, e.g. draw 13.</summary>
    public const uint CaptureSelected = 0xFDFF6F3Fu;

    /// <summary>Settled <c>fistp(fade*255)</c> after the 0x00462ECA clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary><c>and eax, 0x00FFFFFF</c> at <c>0x0046304E</c>.</summary>
    public const uint RgbMask = 0x00FFFFFFu;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Selected immediate wins. Else the zero arm is disabled.
    /// </summary>
    public static uint BaseColor(bool selected, bool available)
    {
        if (selected)
        {
            return SelectedPackedColor;
        }

        return available ? IdlePackedColor : DisabledPackedColor;
    }

    /// <summary>
    /// <c>0x00462ECA</c> signed clamp, then <c>cmp esi, 0xFF</c>.
    /// </summary>
    public static int ClampFadeByte(int fadeByte)
    {
        if (fadeByte < 0)
        {
            return 0;
        }

        return fadeByte > ImageSettledFadeByte ? ImageSettledFadeByte : fadeByte;
    }

    /// <summary>
    /// <c>imul</c> fade at <c>0x00463032</c> / <c>0x00463049</c>.
    /// </summary>
    public static uint FadeMul(uint colour, int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint shifted = colour >> 8;
            uint ecx = (shifted & 0x00FF0000u) * (uint)esi;
            uint eax = (shifted & 0xFFFF0000u) * (uint)esi;
            eax ^= colour;
            eax &= RgbMask;
            return eax ^ ecx;
        }
    }

    /// <summary>
    /// Draw unpack at <c>0x00463055</c>.
    /// </summary>
    public static uint DrawUnpack(uint packed)
    {
        unchecked
        {
            uint eax = packed >> 8;
            uint ecx = eax;
            eax &= 0x00FF0000u;
            ecx &= 0xFFFF0000u;
            uint ebx = (ecx << 8) - ecx;
            ebx ^= packed;
            uint edx = (eax << 8) - eax;
            ebx &= RgbMask;
            return ebx ^ edx;
        }
    }

    /// <summary>
    /// Pushed label colour. Settled ESI matches
    /// <see cref="CaptureIdle"/> / <see cref="CaptureDisabled"/> /
    /// <see cref="CaptureSelected"/>.
    /// </summary>
    public static uint SubmittedColor(bool selected, bool available, int fadeByte) =>
        DrawUnpack(FadeMul(BaseColor(selected, available), fadeByte));
}
