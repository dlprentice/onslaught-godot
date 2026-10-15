// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render language-chevron colour — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Sites.</b> Left <c>0x0046336B</c>, right <c>0x004634F4</c>.
/// Both are <c>mov eax, esi; shl 8; sub esi; shl 16; not edx;
/// and 0x00FFFFFF; xor eax</c> — the same first pack as
/// <see cref="RetailClickToStartTitle.BodyColor"/>. ESI is the
/// 0x00462ECA fade byte, still live because the language iteration
/// is <c>ebx=-1</c> and jumps from <c>0x00462F17</c> over the
/// selected-row ESI rewrite. Settled fade is 255.</para>
///
/// <para><b>Unselected.</b> <c>cmp [edi+8], -1 / je 0x004633AD</c>
/// (right: <c>0x00463533</c>) skips the <c>shr 8 / and / shl 6 /
/// xor</c> arm. Session cannot hold <c>-1</c>, so settled frames
/// take that arm. The draw unpack then submits
/// <c>0x3EFFFFFF</c>.</para>
///
/// <para><b>Not the capture tint.</b> Frame 3000 draws 7 and 9 are
/// still <c>0x3E7F7F7F</c>. First pack <c>0xFEFFFFFF</c> and the
/// submitted dword both fail that compare, so this type does not
/// replace <c>ChromeTint</c> and DrawLanguageSelector is not a
/// consumer. Not <c>SetLanguage</c>. Not a Process increment.
/// HandleKey, DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the sine pin, and the blink stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuLanguageChevronColor
{
    /// <summary>Left chevron <c>mov eax, esi</c> at <c>0x0046336B</c>.</summary>
    public const uint LeftSite = 0x0046336Bu;

    /// <summary>Right chevron <c>mov eax, esi</c> at <c>0x004634F4</c>.</summary>
    public const uint RightSite = 0x004634F4u;

    /// <summary><c>cmp [edi+8], -1</c> at <c>0x00463384</c>.</summary>
    public const int SelectedOffset = 0x08;

    /// <summary>Hover writes <c>-1</c>. Session cannot store this.</summary>
    public const int LanguageSelectedIndex = -1;

    /// <summary>
    /// Settled <c>fistp(fade*255)</c> after the 0x00462ECA clamp.
    /// </summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary><c>and edx, 0x00FFFFFF</c> at <c>0x00463377</c>.</summary>
    public const uint RgbMask = 0x00FFFFFFu;

    /// <summary>
    /// Frame 3000 draws 7 and 9. Not the specimen submit.
    /// </summary>
    public const uint CaptureDiffuse = 0x3E7F7F7Fu;

    /// <summary>First pack of <see cref="ImageSettledFadeByte"/>.</summary>
    public const uint SettledFirstPack = 0xFEFFFFFFu;

    /// <summary>Draw-unpack of the unselected settled arm.</summary>
    public const uint SettledUnselectedSubmitted = 0x3EFFFFFFu;

    /// <summary>
    /// Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.
    /// </summary>
    public const bool IsSetLanguage = false;

    /// <summary>
    /// Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.
    /// </summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Capture <see cref="CaptureDiffuse"/> still owns the quad.
    /// </summary>
    public const bool ReplacesChromeTint = false;

    /// <summary><c>cmp [edi+8], -1</c>. Session cannot store this.</summary>
    public static bool IsSelected(int index) => index == LanguageSelectedIndex;

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
    /// <c>shl 8 / sub / shl 16 / not / and 0x00FFFFFF / xor</c>.
    /// </summary>
    public static uint FirstPack(int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint eax = (uint)((esi << 8) - esi) << 16;
            uint edx = (~eax) & RgbMask;
            return edx ^ eax;
        }
    }

    /// <summary>
    /// Unselected arm at <c>0x00463389</c>:
    /// <c>shr 8 / and / shl 6 / xor / and 0x00FFFFFF / shl 6 / xor</c>.
    /// </summary>
    public static uint UnselectedTransform(uint first)
    {
        unchecked
        {
            uint eax = first >> 8;
            uint ecx = eax;
            eax &= 0x00FF0000u;
            ecx &= 0xFFFF0000u;
            ecx <<= 6;
            ecx ^= first;
            ecx &= RgbMask;
            eax <<= 6;
            return ecx ^ eax;
        }
    }

    /// <summary>
    /// Draw unpack at <c>0x004633AD</c> that becomes the pushed colour.
    /// </summary>
    public static uint DrawUnpack(uint packed)
    {
        unchecked
        {
            uint eax = packed >> 8;
            uint ecx = eax;
            eax &= 0x00FF0000u;
            ecx &= 0xFFFF0000u;
            uint ebx = ecx;
            ebx = (ebx << 8) - ecx;
            ebx ^= packed;
            uint edx = eax;
            edx = (edx << 8) - eax;
            ebx &= RgbMask;
            return ebx ^ edx;
        }
    }

    /// <summary>
    /// Pushed chevron colour. Unselected settled is
    /// <see cref="SettledUnselectedSubmitted"/>, which is not
    /// <see cref="CaptureDiffuse"/>.
    /// </summary>
    public static uint SubmittedColor(int fadeByte, bool selected)
    {
        uint first = FirstPack(fadeByte);
        uint packed = selected ? first : UnselectedTransform(first);
        return DrawUnpack(packed);
    }
}
