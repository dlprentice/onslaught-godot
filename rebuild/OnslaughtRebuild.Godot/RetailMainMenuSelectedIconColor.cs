// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render selected-row icon body colour — recovered from
/// the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Fade source.</b> <c>0x0046405C</c> is
/// <c>fistp [esp+0x18]</c> of the icon-fade byte.
/// <c>0x00464064</c> is the signed 0..255 clamp
/// (<c>test esi / jge / xor esi / cmp 255 / mov esi, 255</c>).
/// Settled icon-fade is 255. This is not a Process increment.</para>
///
/// <para><b>Site.</b> <c>0x004640DC</c> is <c>mov edx, esi</c>.
/// <c>0x004640E5</c> is <c>shl edx, 8</c>.
/// <c>0x004640EA</c> is <c>sub edx, esi</c>.
/// <c>0x004640F3</c> is <c>shl edx, 16</c>.
/// <c>0x004640FD</c> is <c>or edx, 0x00FFFFFF</c>.
/// <c>0x004640DE</c> is the
/// <c>mov eax, [edi*4+0x0089D7A8]</c> texture load, not the pack.
/// Body scale pushes at <c>0x004640F8</c> stay 1.0.</para>
///
/// <para><b>Pack.</b> <c>((esi &lt;&lt; 8) - esi) &lt;&lt; 16 |
/// 0x00FFFFFF</c> = <c>(esi * 255) &lt;&lt; 16</c> in the high
/// word and white RGB. Settled 255 submits <c>0xFEFFFFFF</c>.</para>
///
/// <para><b>Not the capture tint.</b> Frame 3000 draw 31 is still
/// <c>0xFE7F7F7F</c>. The submitted dword fails that compare, so
/// this type does not replace <c>BracketTint</c> and DrawMainMenu
/// is not a consumer of <see cref="SubmittedColor"/>. ChromeTint
/// stays <c>0x3E7F7F7F</c>. ShadowTint stays <c>0x3E000000</c>.
/// Body scale stays 1.0 — not a 29% scale. Not
/// <c>SetLanguage</c>. Not the 0x00463E8D twin fade. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the sine pin, the blink, the
/// chevron colour, the label colour, the selector-bar colour,
/// the writing-chrome colour, the version overlay, and the
/// title-logo shadow stay untouched.</para>
/// </summary>
public static class RetailMainMenuSelectedIconColor
{
    /// <summary><c>mov edx, esi</c> at <c>0x004640DC</c>.</summary>
    public const uint Site = 0x004640DCu;

    /// <summary><c>shl edx, 8</c> at <c>0x004640E5</c>.</summary>
    public const uint ShiftSite = 0x004640E5u;

    /// <summary><c>shl edx, 8</c>.</summary>
    public const int ShiftLeft = 8;

    /// <summary><c>sub edx, esi</c> at <c>0x004640EA</c>.</summary>
    public const uint SubSite = 0x004640EAu;

    /// <summary><c>shl edx, 16</c> at <c>0x004640F3</c>.</summary>
    public const uint Shift16Site = 0x004640F3u;

    /// <summary><c>or edx, 0x00FFFFFF</c> at <c>0x004640FD</c>.</summary>
    public const uint OrSite = 0x004640FDu;

    /// <summary>The OR immediate. White RGB, alpha from the pack.</summary>
    public const uint RgbOr = 0x00FFFFFFu;

    /// <summary>Settled icon-fade <c>fistp</c> after the 0x00464064 clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 draw 31 / documented BracketTint.</summary>
    public const uint CaptureDiffuse = 0xFE7F7F7Fu;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0xFEFFFFFFu;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Capture <see cref="CaptureDiffuse"/> still owns the quad.
    /// </summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>
    /// Body scale stays the already-drawn 1.0. Do not invent 29%.
    /// </summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>
    /// Capture ChromeTint still owns the writing chrome.
    /// </summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>
    /// Capture ShadowTint still owns the title-logo and icon shadows.
    /// </summary>
    public const bool ReplacesShadowTint = false;

    /// <summary>
    /// The 0x00464064 signed clamp, then <c>cmp</c> against 0xFF.
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
    /// <c>(esi &lt;&lt; 8) - esi</c> then <c>&lt;&lt; 16</c> or white RGB.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint edx = (uint)((esi << ShiftLeft) - esi) << 16;
            return edx | RgbOr;
        }
    }
}
