// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D8A0 not/and/xor overlay — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00463D88</c> is
/// <c>mov eax, [0x0089D8A0]</c>. That is the already-owned right
/// primary decor (symbol-bracket-01), not 0x0089D8A4 and not the
/// left pair. Do not redo
/// <see cref="RetailMainMenuRightTwinOverlay"/>,
/// <see cref="RetailMainMenuRightTwinShadow"/>, or
/// <see cref="RetailMainMenuRightDecorShadow"/>.</para>
///
/// <para><b>Site.</b> <c>0x00463D63</c> is <c>mov eax, esi</c>.
/// <c>0x00463D65</c> is <c>mov ecx, [esp+0x40]</c> (sx/sy, not the
/// pack). <c>0x00463D69</c> is <c>shl eax, 8</c>.
/// <c>0x00463D6C</c> is <c>sub eax, esi</c>.
/// <c>0x00463D6E</c> is <c>push ebp</c> (trailing rotation), not the
/// pack. <c>0x00463D6F</c> is <c>shl eax, 16</c>.
/// <c>0x00463D72</c> is <c>mov edx, eax</c>.
/// <c>0x00463D79</c> is <c>not edx</c>.
/// <c>0x00463D7D</c> is <c>and edx, 0x00FFFFFF</c>.
/// <c>0x00463D86</c> is <c>xor edx, eax</c>. ESI is the earlier
/// signed 0..255 fade clamp at <c>0x00463CC7</c>. Settled fade is
/// 255.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c>:
/// <c>(457, 355, z bits 0x3E99999A, 0x0089D8A0, colour, sx=sy
/// from [esp+0x40], mode 4, 0, 1.0, ebp)</c>. The leftover 0.3
/// push at <c>0x00463D90</c> is Z, not scale. Dest immediates at
/// <c>0x00463D95</c> / <c>0x00463D9A</c> are 355.0 / 457.0 — the
/// right-arc body, not left 344 / 219. The 2-D consumer ignores z.
/// Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// after this pair and owns 0x0089D8A4 only. This type does not
/// invent that gate or a <c>ShouldDraw</c>. Shadow sibling
/// <c>0x00463D1F</c> is already
/// <see cref="RetailMainMenuRightDecorShadow"/>.</para>
///
/// <para><b>Pack.</b> <c>shl 8 / sub / shl 16 / not / and
/// 0x00FFFFFF / xor</c> is the same first pack as
/// <see cref="RetailClickToStartTitle.BodyColor"/>,
/// <see cref="RetailMainMenuLanguageChevronColor.FirstPack"/>, and
/// <see cref="RetailMainMenuRightTwinOverlay"/>. Settled 255
/// submits <c>0xFEFFFFFF</c>.</para>
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
/// shadow, the selected-row icon colours, and the 0x00463F3F /
/// 0x00463F83 D8A4 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuRightDecorOverlay
{
    /// <summary><c>mov eax, esi</c> at <c>0x00463D63</c>.</summary>
    public const uint Site = 0x00463D63u;

    /// <summary><c>mov ecx, [esp+0x40]</c> at <c>0x00463D65</c>. sx/sy, not the pack.</summary>
    public const uint Esp40Site = 0x00463D65u;

    /// <summary><c>shl eax, 8</c> at <c>0x00463D69</c>.</summary>
    public const uint ShiftSite = 0x00463D69u;

    /// <summary><c>shl eax, 8</c>.</summary>
    public const int ShiftLeft = 8;

    /// <summary><c>sub eax, esi</c> at <c>0x00463D6C</c>.</summary>
    public const uint SubSite = 0x00463D6Cu;

    /// <summary><c>push ebp</c> at <c>0x00463D6E</c>. Rotation, not the pack.</summary>
    public const uint PushEbpSite = 0x00463D6Eu;

    /// <summary><c>shl eax, 16</c> at <c>0x00463D6F</c>.</summary>
    public const uint Shift16Site = 0x00463D6Fu;

    /// <summary><c>mov edx, eax</c> at <c>0x00463D72</c>.</summary>
    public const uint CopySite = 0x00463D72u;

    /// <summary><c>not edx</c> at <c>0x00463D79</c>.</summary>
    public const uint NotSite = 0x00463D79u;

    /// <summary><c>and edx, 0x00FFFFFF</c> at <c>0x00463D7D</c>.</summary>
    public const uint AndSite = 0x00463D7Du;

    /// <summary><c>xor edx, eax</c> at <c>0x00463D86</c>.</summary>
    public const uint XorSite = 0x00463D86u;

    /// <summary>The AND immediate.</summary>
    public const uint RgbMask = 0x00FFFFFFu;

    /// <summary><c>mov eax, [0x0089D8A0]</c> at <c>0x00463D88</c>.</summary>
    public const uint TextureLoadSite = 0x00463D88u;

    /// <summary>Right primary decor texture global. Not 0x0089D8A4.</summary>
    public const uint TextureGlobal = 0x0089D8A0u;

    /// <summary><c>push 0x3E99999A</c> at <c>0x00463D90</c>.</summary>
    public const uint ZPushSite = 0x00463D90u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3E99999Au;

    /// <summary><c>push 0x43B18000</c> at <c>0x00463D95</c>.</summary>
    public const uint YPushSite = 0x00463D95u;

    /// <summary>Mode-4 dest Y. Right-arc body, not left 344.</summary>
    public const float DestY = 355f;

    /// <summary><c>push 0x43E48000</c> at <c>0x00463D9A</c>.</summary>
    public const uint XPushSite = 0x00463D9Au;

    /// <summary>Mode-4 dest X. Right-arc body, not left 219.</summary>
    public const float DestX = 457f;

    /// <summary>Fourth-from-last push at <c>0x00463D83</c>.</summary>
    public const int Mode = 4;

    /// <summary><c>call 0x005563D0</c> at <c>0x00463D9F</c>.</summary>
    public const uint CallSite = 0x00463D9Fu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>0x00463E8D is the D8A4 twin gate. This pair sits before it.</summary>
    public const uint TwinGateSite = 0x00463E8Du;

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

    /// <summary>0x00463D1F is already <see cref="RetailMainMenuRightDecorShadow"/>.</summary>
    public const bool RedoesRightDecorShadow = false;

    /// <summary>0x00463F3F is already <see cref="RetailMainMenuRightTwinShadow"/>.</summary>
    public const bool RedoesRightTwinShadow = false;

    /// <summary>0x00463F83 is already <see cref="RetailMainMenuRightTwinOverlay"/>.</summary>
    public const bool RedoesRightTwinOverlay = false;

    /// <summary>The 462/365 addends stay on the already-shipped ellipse.</summary>
    public const bool RedoesDecorShadow = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This pair sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

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
    /// <c>shl 8 / sub / shl 16 / not / and 0x00FFFFFF / xor</c>.
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
