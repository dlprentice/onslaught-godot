// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render selected-row icon shadow colour — recovered from
/// the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Fade source.</b> <c>0x0046405F</c> is
/// <c>fistp qword [esp+0x18]</c> of the icon-fade byte.
/// <c>0x00464067</c> is the signed 0..255 clamp
/// (<c>test esi / jge / xor esi / cmp 255 / jle / mov esi, 255</c>).
/// The keep-going leftover labeled <c>0x00464075</c> as the pack
/// start; that byte is <c>7E 05</c>, the clamp <c>jle</c>.
/// Settled icon-fade is 255. This is not a Process increment.</para>
///
/// <para><b>Site.</b> <c>0x0046407C</c> is <c>mov ecx, esi</c>.
/// <c>0x00464085</c> is <c>shl ecx, 6</c>.
/// <c>0x0046408A</c> is <c>sub ecx, esi</c>.
/// <c>0x00464093</c> is <c>shl ecx, 16</c>.
/// <c>0x0046409D</c> is <c>and ecx, 0xFF000000</c>.
/// <c>0x0046407E</c> is the
/// <c>mov eax, [edi*4+0x0089D7A8]</c> texture load, not the pack.
/// Shadow scale pushes at <c>0x00464098</c> stay the already-drawn
/// 1.05 <c>ShadowScaleBoost</c>.</para>
///
/// <para><b>Pack.</b> <c>((esi &lt;&lt; 6) - esi) &lt;&lt; 16 &amp;
/// 0xFF000000</c> = <c>(esi * 63) &lt;&lt; 16</c> in the alpha
/// byte and black RGB. Settled 255 submits <c>0x3E000000</c>,
/// which is capture ShadowTint. DrawMainMenu
/// keeps ShadowTint and does not call
/// <see cref="SubmittedColor"/>. Body scale stays 1.0. ChromeTint
/// stays <c>0x3E7F7F7F</c>. BracketTint stays <c>0xFE7F7F7F</c>.
/// Not a 29% scale. Not <c>SetLanguage</c>. Not the 0x00463E8D
/// twin fade. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, the writing-chrome Y, the sine pin,
/// the blink, the chevron colour, the label colour, the
/// selector-bar colour, the writing-chrome colour, the version
/// overlay, the title-logo shadow, and the selected-row icon
/// body stay untouched.</para>
/// </summary>
public static class RetailMainMenuSelectedIconShadow
{
    /// <summary><c>mov ecx, esi</c> at <c>0x0046407C</c>.</summary>
    public const uint Site = 0x0046407Cu;

    /// <summary><c>shl ecx, 6</c> at <c>0x00464085</c>.</summary>
    public const uint ShiftSite = 0x00464085u;

    /// <summary><c>shl ecx, 6</c>.</summary>
    public const int ShiftLeft = 6;

    /// <summary><c>sub ecx, esi</c> at <c>0x0046408A</c>.</summary>
    public const uint SubSite = 0x0046408Au;

    /// <summary><c>shl ecx, 16</c> at <c>0x00464093</c>.</summary>
    public const uint Shift16Site = 0x00464093u;

    /// <summary><c>and ecx, 0xFF000000</c> at <c>0x0046409D</c>.</summary>
    public const uint AndSite = 0x0046409Du;

    /// <summary>The AND immediate. Black RGB, alpha only.</summary>
    public const uint AlphaMask = 0xFF000000u;

    /// <summary><c>fistp qword [esp+0x18]</c> at <c>0x0046405F</c>.</summary>
    public const uint FistpSite = 0x0046405Fu;

    /// <summary>
    /// The leftover-suggested VA. Specimen byte is <c>7E 05</c>
    /// (<c>jle</c> after <c>cmp esi, 255</c>), not the pack.
    /// </summary>
    public const uint ClampJleSite = 0x00464075u;

    /// <summary>Settled icon-fade <c>fistp</c> after the 0x00464067 clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Capture ShadowTint / frame 3000 documented icon-shadow immediate.</summary>
    public const uint CaptureDiffuse = 0x3E000000u;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0x3E000000u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Body scale stays the already-drawn 1.0. Do not invent 29%.
    /// </summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>
    /// Capture <see cref="CaptureDiffuse"/> still owns the quad.
    /// </summary>
    public const bool ReplacesShadowTint = false;

    /// <summary>
    /// Capture ChromeTint still owns the writing chrome.
    /// </summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>
    /// Capture BracketTint still owns the selected-row icon body.
    /// </summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>
    /// The 0x00464067 signed clamp, then <c>cmp</c> against 0xFF.
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
    /// <c>(esi &lt;&lt; 6) - esi</c> then <c>&lt;&lt; 16</c> and alpha-only.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint ecx = (uint)((esi << ShiftLeft) - esi) << 16;
            return ecx & AlphaMask;
        }
    }
}
