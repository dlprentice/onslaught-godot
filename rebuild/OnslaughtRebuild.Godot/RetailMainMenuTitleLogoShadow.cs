// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render title-logo shadow colour — recovered from
/// the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Fade source.</b> <c>0x0046423D</c> is
/// <c>cmp ebx, 0x0C</c>. <c>0x00464240</c> is
/// <c>fistp [esp+0x18]</c> of the page-fade byte.
/// <c>0x0046424A</c> is <c>mov esi, 255</c> on the dest == 0x0c
/// arm (click-to-start arrival). This lane only ever takes that
/// arm, so the shadow fade is 255. This is not a 29% scale.</para>
///
/// <para><b>Site.</b> <c>0x0046424F</c> is <c>mov ecx, esi</c>.
/// <c>0x00464256</c> is <c>shl ecx, 6</c>.
/// <c>0x0046425B</c> is <c>sub ecx, esi</c>.
/// <c>0x00464264</c> is <c>shl ecx, 16</c>.
/// <c>0x0046426E</c> is <c>and ecx, 0xFF000000</c>.
/// <c>0x00464251</c> is the <c>mov eax, [0x0089d88c]</c> texture
/// load, not the pack.</para>
///
/// <para><b>Pack.</b> <c>((esi &lt;&lt; 6) - esi) &lt;&lt; 16 &amp;
/// 0xFF000000</c> = <c>(esi * 63) &lt;&lt; 16</c> in the alpha
/// byte and black RGB. Settled 255 submits <c>0x3E000000</c>,
/// which is capture ShadowTint. DrawMainMenu
/// keeps ShadowTint and does not call
/// <see cref="SubmittedColor"/>. The body pack at 0x004642E3 /
/// 0x004642F0 stays TitleLogoTint. Not <c>SetLanguage</c>. Not
/// the 0x00463E8D twin fade. Not a Process increment. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the sine pin, the blink, the
/// chevron colour, the label colour, the selector-bar colour,
/// the writing-chrome colour, and the version overlay stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuTitleLogoShadow
{
    /// <summary><c>mov ecx, esi</c> at <c>0x0046424F</c>.</summary>
    public const uint Site = 0x0046424Fu;

    /// <summary><c>shl ecx, 6</c> at <c>0x00464256</c>.</summary>
    public const uint ShiftSite = 0x00464256u;

    /// <summary><c>shl ecx, 6</c>.</summary>
    public const int ShiftLeft = 6;

    /// <summary><c>and ecx, 0xFF000000</c> at <c>0x0046426E</c>.</summary>
    public const uint AndSite = 0x0046426Eu;

    /// <summary>The AND immediate. Black RGB, alpha only.</summary>
    public const uint AlphaMask = 0xFF000000u;

    /// <summary><c>cmp ebx, 0x0C</c> at <c>0x0046423D</c>.</summary>
    public const uint DestCompareSite = 0x0046423Du;

    /// <summary>Click-to-start page ordinal.</summary>
    public const int ClickPageDest = 0x0C;

    /// <summary><c>mov esi, 255</c> at <c>0x0046424A</c>.</summary>
    public const int DestForceImmediate = 255;

    /// <summary>Settled page-fade <c>fistp</c>, and the dest==0x0c force.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Capture ShadowTint / documented logo-shadow immediate.</summary>
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
    /// Dest == 0x0c writes 255; otherwise the signed 0..255 clamp.
    /// </summary>
    public static int FadeByte(int fadeByte, bool destIsClickPage) =>
        destIsClickPage ? DestForceImmediate : ClampFadeByte(fadeByte);

    /// <summary>
    /// The 0x0046424x signed clamp, then <c>cmp</c> against 0xFF.
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
