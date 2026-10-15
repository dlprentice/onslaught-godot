// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render selector-bar colour — recovered from the
/// pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Site.</b> <c>0x00462FB9</c> is <c>mov eax, esi</c> on
/// the <c>ebx == [edi+8]</c> arm only. <c>0x00462FB5</c> is the
/// preceding <c>fild [esp+0x18]</c>, not this move. ESI is the
/// icon-fade byte after the 0x00462F9C clamp, not the page-fade
/// byte. Settled icon-fade is 255.</para>
///
/// <para><b>Pack.</b> <c>shl eax, 7; sub esi; shl 16;
/// and 0xFF000000</c> = <c>(esi * 127) &lt;&lt; 16</c> in the
/// alpha byte. Settled 255 submits <c>0x7E000000</c>, which is
/// frame 3000 draw 11. DrawMainMenuSelectorBar is the consumer.
/// DrawQuitConfirm keeps <c>HighlightTint</c>. Not
/// <c>SetLanguage</c>. Not a Process increment. HandleKey,
/// DrawLoading, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, the writing-chrome Y,
/// the sine pin, the blink, the chevron colour, and the label
/// colour stay untouched.</para>
/// </summary>
public static class RetailMainMenuSelectorBarColor
{
    /// <summary><c>mov eax, esi</c> at <c>0x00462FB9</c>.</summary>
    public const uint Site = 0x00462FB9u;

    /// <summary><c>shl eax, 7</c> at <c>0x00462FBD</c>.</summary>
    public const uint ShiftSite = 0x00462FBDu;

    /// <summary><c>shl eax, 7</c>.</summary>
    public const int ShiftLeft = 7;

    /// <summary><c>and eax, 0xFF000000</c> at <c>0x00462FE6</c>.</summary>
    public const uint AlphaMask = 0xFF000000u;

    /// <summary>Settled icon-fade <c>fistp</c> after the 0x00462F9C clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 draw 11.</summary>
    public const uint CaptureDiffuse = 0x7E000000u;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0x7E000000u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// <c>0x00462F9C</c> signed clamp, then <c>cmp esi, 0xFF</c>.
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
    /// <c>(esi &lt;&lt; 7) - esi</c> then <c>&lt;&lt; 16</c> masked to alpha.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint eax = (uint)((esi << ShiftLeft) - esi) << 16;
            return eax & AlphaMask;
        }
    }
}
