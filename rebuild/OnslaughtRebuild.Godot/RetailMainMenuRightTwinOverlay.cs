// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D8A4 not/and/xor overlay — recovered from
/// the pristine specimen
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Gate.</b> <c>0x00463E8D</c> is
/// a float compare against the value at 0x005D8BB0, popping the result
/// then a bit test of ah against 1, branching to 0x00463FC7 when equal.
/// Fall-through when transition &lt; 0.9. Settled frames skip the
/// call. That is the already-exposed twin predicate, not a new fade
/// and not the 0x00464343 title-logo sheen.</para>
///
/// <para><b>Site.</b> <c>0x00463F83</c> is a copy of esi into eax.
/// <c>0x00463F89</c> is a left shift of eax by 8.
/// <c>0x00463F8C</c> is a subtraction of esi from eax.
/// <c>0x00463F8E</c> is a push of ebp (trailing rotation), not the
/// pack. <c>0x00463F8F</c> is a left shift of eax by 16.
/// <c>0x00463F92</c> is a copy of eax into edx.
/// <c>0x00463F99</c> is <c>not edx</c>.
/// <c>0x00463F9D</c> is a mask of edx with 0x00FFFFFF.
/// <c>0x00463FA6</c> is an xor of edx with eax. ESI is the earlier
/// signed 0..255 fade clamp. Settled fade is 255.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c>:
/// <c>(457, 355, z bits 0x3E99999A, 0x0089D8A4, colour, sx=sy
/// from [esp+0x40], mode 4, 0, 1.0, ebp)</c>. The leftover 0.3
/// push at <c>0x00463FB0</c> is Z, not scale. The 2-D consumer
/// ignores z. Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Pack.</b> a left shift by 8, then subtraction, then left shift by 16,
/// then bitwise not, then mask with 0x00FFFFFF, then xor is the same first pack as
/// <see cref="RetailClickToStartTitle.BodyColor"/> and
/// <see cref="RetailMainMenuLanguageChevronColor.FirstPack"/>.
/// Settled 255 submits <c>0xFEFFFFFF</c>.</para>
///
/// <para><b>Not the capture tint.</b> Frame 3000 draw 31 is still
/// <c>0xFE7F7F7F</c>. The submitted dword fails that compare, so
/// this type does not replace <c>BracketTint</c> and DrawMainMenu
/// is not a consumer of <see cref="SubmittedColor"/>. ChromeTint
/// stays <c>0x3E7F7F7F</c>. ShadowTint stays <c>0x3E000000</c>.
/// Not <c>SetLanguage</c>. Not the implemented twin fade.
/// HandleKey, DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the sine pin, the blink, the
/// chevron colour, the label colour, the selector-bar colour,
/// the writing-chrome colour, the version overlay, the title-logo
/// shadow, and the selected-row icon colours stay untouched.</para>
/// </summary>
public static class RetailMainMenuRightTwinOverlay
{
    /// <summary>A copy of esi into eax at <c>0x00463F83</c>.</summary>
    public const uint Site = 0x00463F83u;

    /// <summary>A left shift of eax by 8 at <c>0x00463F89</c>.</summary>
    public const uint ShiftSite = 0x00463F89u;

    /// <summary>A left shift of eax by 8.</summary>
    public const int ShiftLeft = 8;

    /// <summary>A subtraction of esi from eax at <c>0x00463F8C</c>.</summary>
    public const uint SubSite = 0x00463F8Cu;

    /// <summary>A push of ebp at <c>0x00463F8E</c>. Rotation, not the pack.</summary>
    public const uint PushEbpSite = 0x00463F8Eu;

    /// <summary>A left shift of eax by 16 at <c>0x00463F8F</c>.</summary>
    public const uint Shift16Site = 0x00463F8Fu;

    /// <summary>A copy of eax into edx at <c>0x00463F92</c>.</summary>
    public const uint CopySite = 0x00463F92u;

    /// <summary><c>not edx</c> at <c>0x00463F99</c>.</summary>
    public const uint NotSite = 0x00463F99u;

    /// <summary>A mask of edx with 0x00FFFFFF at <c>0x00463F9D</c>.</summary>
    public const uint AndSite = 0x00463F9Du;

    /// <summary>An xor of edx with eax at <c>0x00463FA6</c>.</summary>
    public const uint XorSite = 0x00463FA6u;

    /// <summary>The AND immediate.</summary>
    public const uint RgbMask = 0x00FFFFFFu;

    /// <summary>A load of the value at 0x0089D8A4 into eax at <c>0x00463FA8</c>.</summary>
    public const uint TextureLoadSite = 0x00463FA8u;

    /// <summary>Right transition twin texture global.</summary>
    public const uint TextureGlobal = 0x0089D8A4u;

    /// <summary>A push of constant 0x3E99999A at <c>0x00463FB0</c>.</summary>
    public const uint ZPushSite = 0x00463FB0u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3E99999Au;

    /// <summary>A push of constant 0x43B18000 at <c>0x00463FB5</c>.</summary>
    public const uint YPushSite = 0x00463FB5u;

    /// <summary>Mode-4 dest Y.</summary>
    public const float DestY = 355f;

    /// <summary>A push of constant 0x43E48000 at <c>0x00463FBA</c>.</summary>
    public const uint XPushSite = 0x00463FBAu;

    /// <summary>Mode-4 dest X. Same body anchor as the selected-row icon.</summary>
    public const float DestX = 457f;

    /// <summary>Fourth-from-last push at <c>0x00463FA3</c>.</summary>
    public const int Mode = 4;

    /// <summary>A direct call to 0x005563D0 at <c>0x00463FBF</c>.</summary>
    public const uint CallSite = 0x00463FBFu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>A float compare against the value at 0x005D8BB0, popping the result at <c>0x00463E8D</c>.</summary>
    public const uint GateSite = 0x00463E8Du;

    /// <summary><c>je</c> target when transition &gt;= 0.9.</summary>
    public const uint SkipSite = 0x00463FC7u;

    /// <summary>Settled fade byte after the signed 0..255 clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 draw 31 / documented BracketTint.</summary>
    public const uint CaptureDiffuse = 0xFE7F7F7Fu;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0xFEFFFFFFu;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.3 push is Z. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.3 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Capture <see cref="CaptureDiffuse"/> still owns the quad.</summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>Capture ChromeTint still owns the writing chrome.</summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>Capture ShadowTint still owns the shadows.</summary>
    public const bool ReplacesShadowTint = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary>
    /// A bit test of ah against 1, branching to 0x00463FC7 when equal
    /// after a float compare against the constant 0.9, popping the result.
    /// </summary>
    public static bool ShouldDraw(float transition) =>
        RetailMainMenuHitTest.AcceptsTwinFade(transition);

    /// <summary>The earlier signed clamp, then <c>cmp</c> against 0xFF.</summary>
    public static int ClampFadeByte(int fadeByte)
    {
        if (fadeByte < 0)
        {
            return 0;
        }

        return fadeByte > ImageSettledFadeByte ? ImageSettledFadeByte : fadeByte;
    }

    /// <summary>
    /// A left shift by 8, then subtraction, then left shift by 16, then bitwise not, then mask with 0x00FFFFFF, then xor.
    /// </summary>
    public static uint SubmittedColor(int fadeByte)
    {
        int esi = ClampFadeByte(fadeByte);
        unchecked
        {
            uint eax = (uint)((esi << ShiftLeft) - esi) << 16;
            uint edx = (~eax) & RgbMask;
            return edx ^ eax;
        }
    }
}
