// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay — recovered from the pristine
/// specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes).
///
/// <para><b>Format.</b> <c>0x0046416E</c> is
/// <c>push 0x00629454</c>. That string is <c>"V%1d.%02d"</c>.
/// <c>0x00464163</c> is <c>mov eax, [0x00629410]</c> (major).
/// <c>0x0046415D</c> is <c>mov edx, [0x00679980]</c> (minor).
/// The major dword is image-initial 1. The minor VA is past the
/// 2,506,752-byte image, so image-initial is 0. Cold sprintf is
/// therefore <c>V1.00</c>.</para>
///
/// <para><b>Not WinMain.</b> <c>WinMain</c> (<c>0x00512130</c>), at
/// <c>0x005121BF</c>, overwrites those globals from
/// <c>VS_FIXEDFILEINFO</c> (specimen resource is 1.0.0.0). This
/// type does not call <c>GetFileVersionInfo</c>. Image-initial and
/// the resource agree, so DrawMainMenu can format the image
/// initials.</para>
///
/// <para><b>Colour.</b> After <c>fistp [esp+0x28]</c>,
/// <c>0x004641B1</c> is <c>shl eax, 24</c> and <c>0x004641B4</c>
/// is <c>or eax, 0x00102025</c>. Settled fade 255 submits
/// <c>0xFF102025</c>, which is capture VersionTint / frame 3000
/// draw 33. DrawMainMenu keeps VersionTint and does not call
/// <see cref="SubmittedColor"/>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, the writing-chrome Y,
/// the sine pin, the blink, the chevron colour, the label colour,
/// and the selector-bar colour stay untouched. Not
/// <c>SetLanguage</c>. Not the 0x00463E8D twin fade. Not a
/// Process increment. Not a title-logo 29% scale.</para>
/// </summary>
public static class RetailMainMenuVersionOverlay
{
    /// <summary><c>push 0x00629454</c> at <c>0x0046416E</c>.</summary>
    public const uint FormatSite = 0x0046416Eu;

    /// <summary>The pushed format pointer.</summary>
    public const uint FormatGlobal = 0x00629454u;

    /// <summary>Image bytes at <see cref="FormatGlobal"/>.</summary>
    public const string FormatString = "V%1d.%02d";

    /// <summary><c>mov eax, [0x00629410]</c> at <c>0x00464163</c>.</summary>
    public const uint MajorGlobal = 0x00629410u;

    /// <summary>Image dword at <see cref="MajorGlobal"/>.</summary>
    public const int ImageInitialMajor = 1;

    /// <summary><c>mov edx, [0x00679980]</c> at <c>0x0046415D</c>.</summary>
    public const uint MinorGlobal = 0x00679980u;

    /// <summary>
    /// Uninitialised BSS. The image does not contain this VA, so the
    /// cold dword is 0.
    /// </summary>
    public const int ImageInitialMinor = 0;

    /// <summary><c>shl eax, 24</c> at <c>0x004641B1</c>.</summary>
    public const uint ShiftSite = 0x004641B1u;

    /// <summary><c>shl eax, 24</c>.</summary>
    public const int ShiftLeft = 24;

    /// <summary><c>or eax, 0x00102025</c> at <c>0x004641B4</c>.</summary>
    public const uint OrSite = 0x004641B4u;

    /// <summary>The OR immediate.</summary>
    public const uint RgbOr = 0x00102025u;

    /// <summary>Settled page-fade <c>fistp</c>.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 draw 33 body.</summary>
    public const uint CaptureDiffuse = 0xFF102025u;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0xFF102025u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// WinMain's resource write is not this helper.
    /// </summary>
    public const bool IsGetFileVersionInfo = false;

    /// <summary>
    /// This overlay is not a title-logo scale. Do not invent 29%.
    /// </summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>
    /// <c>"V%1d.%02d"</c>: minimum width 1 on the major, zero-padded
    /// width 2 on the minor.
    /// </summary>
    public static string Format(int major, int minor) =>
        string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "V{0}.{1:D2}",
            major,
            minor);

    /// <summary>
    /// Signed clamp to the settled fade byte, then
    /// <c>cmp</c> against 0xFF as on the sibling packs.
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
    /// <c>shl 24</c> then <c>or 0x00102025</c>.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int eax = ClampFadeByte(fadeByte);
        unchecked
        {
            return ((uint)eax << ShiftLeft) | RgbOr;
        }
    }
}
