// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D898 not/and/xor overlay — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00463AF8</c> is
/// <c>mov eax, [0x0089D898]</c>. That is the leftover left twin
/// (title-bracket-02), not 0x0089D894, not 0x0089D8A0, and
/// not 0x0089D8A4. Do not redo
/// <see cref="RetailMainMenuLeftTwinShadow"/>,
/// <see cref="RetailMainMenuLeftDecorShadow"/>,
/// <see cref="RetailMainMenuLeftDecorOverlay"/>,
/// <see cref="RetailMainMenuRightDecorShadow"/>,
/// <see cref="RetailMainMenuRightDecorOverlay"/>,
/// <see cref="RetailMainMenuRightTwinShadow"/>, or
/// <see cref="RetailMainMenuRightTwinOverlay"/>.</para>
///
/// <para><b>Site.</b> <c>0x00463AD3</c> is <c>mov eax, esi</c>
/// (<c>8b c6</c>). <c>0x00463AD5</c> is
/// <c>mov ecx, [esp+0x40]</c> (sx/sy, not the pack).
/// <c>0x00463AD9</c> is <c>shl eax, 8</c>.
/// <c>0x00463ADC</c> is <c>sub eax, esi</c>.
/// <c>0x00463ADE</c> is <c>push ebp</c> (trailing rotation), not the
/// pack. <c>0x00463ADF</c> is <c>shl eax, 16</c>.
/// <c>0x00463AE2</c> is <c>mov edx, eax</c>.
/// <c>0x00463AE4</c> is <c>push 0x3F800000</c> (identity 1.0), not
/// a 29% title-logo scale. <c>0x00463AE9</c> is <c>not edx</c>.
/// <c>0x00463AED</c> is <c>and edx, 0x00FFFFFF</c>.
/// <c>0x00463AF6</c> is <c>xor edx, eax</c> (<c>33 d0</c>). ESI is
/// the earlier signed 0..255 fade clamp. Settled fade is 255.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c>:
/// <c>(219, 344, z bits 0x3E99999A, 0x0089D898, colour, sx=sy
/// from [esp+0x40], mode 4, 0, 1.0, ebp)</c>. The leftover 0.3
/// push at <c>0x00463B00</c> is Z, not scale. Dest immediates at
/// <c>0x00463B05</c> / <c>0x00463B0A</c> are 344.0 / 219.0 — the
/// leftover left-twin body, not right 355 / 457. The 2-D consumer
/// ignores z. Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// later and owns 0x0089D8A4 only. This type does not invent
/// that gate or a <c>ShouldDraw</c>. Shadow sibling
/// <c>0x00463A8F</c> is already
/// <see cref="RetailMainMenuLeftTwinShadow"/>.</para>
///
/// <para><b>Pack.</b> <c>shl 8 / sub / shl 16 / not / and
/// 0x00FFFFFF / xor</c> is the same first pack as
/// <see cref="RetailClickToStartTitle.BodyColor"/>,
/// <see cref="RetailMainMenuLanguageChevronColor.FirstPack"/>,
/// <see cref="RetailMainMenuLeftDecorOverlay"/>,
/// <see cref="RetailMainMenuRightTwinOverlay"/>, and
/// <see cref="RetailMainMenuRightDecorOverlay"/>. Settled 255
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
/// shadow, the selected-row icon colours, and the 0x00463873 /
/// 0x004638B7 / 0x00463A8F / 0x00463D1F / 0x00463D63 / 0x00463F3F /
/// 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuLeftTwinOverlay
{
    /// <summary><c>mov eax, esi</c> at <c>0x00463AD3</c>.</summary>
    public const uint Site = 0x00463AD3u;

    /// <summary><c>mov ecx, [esp+0x40]</c> at <c>0x00463AD5</c>. sx/sy, not the pack.</summary>
    public const uint Esp40Site = 0x00463AD5u;

    /// <summary><c>shl eax, 8</c> at <c>0x00463AD9</c>.</summary>
    public const uint ShiftSite = 0x00463AD9u;

    /// <summary><c>shl eax, 8</c>.</summary>
    public const int ShiftLeft = 8;

    /// <summary><c>sub eax, esi</c> at <c>0x00463ADC</c>.</summary>
    public const uint SubSite = 0x00463ADCu;

    /// <summary><c>push ebp</c> at <c>0x00463ADE</c>. Rotation, not the pack.</summary>
    public const uint PushEbpSite = 0x00463ADEu;

    /// <summary><c>shl eax, 16</c> at <c>0x00463ADF</c>.</summary>
    public const uint Shift16Site = 0x00463ADFu;

    /// <summary><c>mov edx, eax</c> at <c>0x00463AE2</c>.</summary>
    public const uint CopySite = 0x00463AE2u;

    /// <summary><c>push 0x3F800000</c> at <c>0x00463AE4</c>. Identity 1.0, not 29%.</summary>
    public const uint ScalePushSite = 0x00463AE4u;

    /// <summary>Identity scale immediate. Not a title-logo 29%.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>not edx</c> at <c>0x00463AE9</c>.</summary>
    public const uint NotSite = 0x00463AE9u;

    /// <summary><c>and edx, 0x00FFFFFF</c> at <c>0x00463AED</c>.</summary>
    public const uint AndSite = 0x00463AEDu;

    /// <summary><c>xor edx, eax</c> at <c>0x00463AF6</c>.</summary>
    public const uint XorSite = 0x00463AF6u;

    /// <summary>The AND immediate.</summary>
    public const uint RgbMask = 0x00FFFFFFu;

    /// <summary><c>mov eax, [0x0089D898]</c> at <c>0x00463AF8</c>.</summary>
    public const uint TextureLoadSite = 0x00463AF8u;

    /// <summary>Leftover left-twin texture global. Not D894 / D8A0 / D8A4.</summary>
    public const uint TextureGlobal = 0x0089D898u;

    /// <summary><c>push 0x3E99999A</c> at <c>0x00463B00</c>.</summary>
    public const uint ZPushSite = 0x00463B00u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3E99999Au;

    /// <summary><c>push 0x43AC0000</c> at <c>0x00463B05</c>.</summary>
    public const uint YPushSite = 0x00463B05u;

    /// <summary>Mode-4 dest Y. Leftover left-twin body, not right 355.</summary>
    public const float DestY = 344f;

    /// <summary><c>push 0x435B0000</c> at <c>0x00463B0A</c>.</summary>
    public const uint XPushSite = 0x00463B0Au;

    /// <summary>Mode-4 dest X. Leftover left-twin body, not right 457.</summary>
    public const float DestX = 219f;

    /// <summary>Fourth-from-last push at <c>0x00463AF3</c>.</summary>
    public const int Mode = 4;

    /// <summary><c>call 0x005563D0</c> at <c>0x00463B0F</c>.</summary>
    public const uint CallSite = 0x00463B0Fu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Already-shipped *63 sibling. 0x0089D898, not this pack.</summary>
    public const uint ShadowSiblingSite = 0x00463A8Fu;

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

    /// <summary>The 0.3 push is Z and the 1.0 push is identity. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.3 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Capture <see cref="CaptureDiffuse"/> still owns the quad.</summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>Capture ChromeTint still owns the writing chrome.</summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>Capture ShadowTint still owns the shadows.</summary>
    public const bool ReplacesShadowTint = false;

    /// <summary>0x00463A8F is already <see cref="RetailMainMenuLeftTwinShadow"/>.</summary>
    public const bool RedoesShadowSibling = false;

    /// <summary>0x00463873 is already <see cref="RetailMainMenuLeftDecorShadow"/>.</summary>
    public const bool RedoesLeftDecorShadow = false;

    /// <summary>0x004638B7 is already <see cref="RetailMainMenuLeftDecorOverlay"/>.</summary>
    public const bool RedoesLeftDecorOverlay = false;

    /// <summary>0x00463D1F is already <see cref="RetailMainMenuRightDecorShadow"/>.</summary>
    public const bool RedoesRightDecorShadow = false;

    /// <summary>0x00463D63 is already <see cref="RetailMainMenuRightDecorOverlay"/>.</summary>
    public const bool RedoesRightDecorOverlay = false;

    /// <summary>0x00463F3F is already <see cref="RetailMainMenuRightTwinShadow"/>.</summary>
    public const bool RedoesRightTwinShadow = false;

    /// <summary>0x00463F83 is already <see cref="RetailMainMenuRightTwinOverlay"/>.</summary>
    public const bool RedoesRightTwinOverlay = false;

    /// <summary>The 224/349 addends stay on the already-shipped ellipse.</summary>
    public const bool RedoesDecorShadow = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This pair sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float Scale => BitConverter.UInt32BitsToSingle(ScaleBits);

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
