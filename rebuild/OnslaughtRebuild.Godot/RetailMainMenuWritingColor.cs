// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render Forseti writing-chrome colour — recovered from
/// the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Site.</b> After the 0x00462DD2 <c>fistp</c> of the page
/// fade, <c>0x00462DDD</c> is <c>mov ecx, eax</c>.
/// <c>0x00462DE4</c> is <c>shl ecx, 6; sub ecx, eax; shl 16</c>.
/// <c>0x00462DDF</c> is the intervening <c>push 0x3F800000</c>;
/// <c>0x00462DE3</c> is that immediate's last byte, not the shl.
/// EAX is the
/// 0x00462D7A <c>(transition-0.75)*4</c> fade byte, clamped
/// 0..255. Settled fade is 255.</para>
///
/// <para><b>Pack.</b> <c>((eax &lt;&lt; 6) - eax) &lt;&lt; 16 |
/// 0x00FFFFFF</c> = <c>(eax * 63) &lt;&lt; 16</c> in the alpha
/// byte and white RGB. Settled 255 submits <c>0x3EFFFFFF</c>.</para>
///
/// <para><b>Not the capture tint.</b> Frame 3000 draws 3, 4 and 5
/// are still <c>0x3E7F7F7F</c>. The submitted dword fails that
/// compare, so this type does not replace <c>ChromeTint</c> and
/// DrawMainMenu is not a consumer of <see cref="SubmittedColor"/>.
/// The Y scroll stays <see cref="RetailMainMenuWritingScroll"/>.
/// Not <c>SetLanguage</c>. Not a Process increment. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the sine pin, the blink, the chevron colour, the label
/// colour, and the selector-bar colour stay untouched.</para>
/// </summary>
public static class RetailMainMenuWritingColor
{
    /// <summary><c>mov ecx, eax</c> at <c>0x00462DDD</c>.</summary>
    public const uint Site = 0x00462DDDu;

    /// <summary><c>shl ecx, 6</c> at <c>0x00462DE4</c>.</summary>
    public const uint ShiftSite = 0x00462DE4u;

    /// <summary><c>shl ecx, 6</c>.</summary>
    public const int ShiftLeft = 6;

    /// <summary><c>or esi, 0x00FFFFFF</c> at <c>0x00462DF6</c>.</summary>
    public const uint RgbOr = 0x00FFFFFFu;

    /// <summary>Settled <c>fistp</c> after the 0x00462D9x clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 draws 3, 4 and 5.</summary>
    public const uint CaptureDiffuse = 0x3E7F7F7Fu;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0x3EFFFFFFu;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Capture <see cref="CaptureDiffuse"/> still owns the quads.
    /// </summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>
    /// The 0x00462D9x signed clamp, then <c>cmp</c> against 0xFF.
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
    /// <c>(eax &lt;&lt; 6) - eax</c> then <c>&lt;&lt; 16</c> or white RGB.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int eax = ClampFadeByte(fadeByte);
        unchecked
        {
            uint ecx = (uint)((eax << ShiftLeft) - eax) << 16;
            return ecx | RgbOr;
        }
    }
}
