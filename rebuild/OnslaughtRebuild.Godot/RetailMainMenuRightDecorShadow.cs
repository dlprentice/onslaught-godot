// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D8A0 *63 alpha shadow — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00463D06</c> is
/// <c>mov eax, [0x0089D8A0]</c>. That is the already-owned right
/// primary decor (symbol-bracket-01), not 0x0089D8A4 and not the
/// left pair. Do not redo
/// <see cref="RetailMainMenuRightTwinShadow"/> or
/// <see cref="RetailMainMenuRightTwinOverlay"/>.</para>
///
/// <para><b>Site.</b> <c>0x00463D1F</c> is <c>mov ecx, esi</c>.
/// <c>0x00463D21</c> is <c>shl ecx, 6</c>.
/// <c>0x00463D24</c> is <c>sub ecx, esi</c>.
/// <c>0x00463D26</c> is <c>shl ecx, 16</c>.
/// <c>0x00463D29</c> is <c>and ecx, 0xFF000000</c>.
/// ESI is the earlier signed 0..255 fade clamp at
/// <c>0x00463CC7</c>. Settled fade is 255.</para>
///
/// <para><b>Call.</b> Colour then 0x0089D8A0 then
/// <c>push 0x3EB33333</c> at <c>0x00463D31</c>. That leftover 0.35
/// is Z, not scale. <c>mov ecx, 0x0089D758</c> then
/// <c>call 0x00468750</c> (fcos of <c>mCounter</c>)
/// <c>fadd [0x005DB5C8]=365.0</c>, then <c>call 0x00468730</c>
/// (fsin) <c>fadd [0x005DB5C4]=462.0</c>, then
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c>. Dest
/// addends are the already-measured right-arc ellipse centre
/// (457+5, 355+10), not the left dest. The ellipse itself is
/// <c>RetailFrontendDecorShadow</c> — do not redo it. Not a 29%
/// title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// after this pair and owns 0x0089D8A4 only. This type does not
/// invent that gate or a <c>ShouldDraw</c>. Body sibling
/// <c>0x00463D63</c> is the leftover not/and/xor pack at
/// dest immediates 355.0 / 457.0 and Z <c>0x3E99999A</c> — not
/// this type.</para>
///
/// <para><b>Pack.</b> <c>((esi &lt;&lt; 6) - esi) &lt;&lt; 16 &amp;
/// 0xFF000000</c> = <c>(esi * 63) &lt;&lt; 16</c> in the alpha
/// byte and black RGB. Same *63 pack as
/// <see cref="RetailMainMenuTitleLogoShadow"/>,
/// <see cref="RetailMainMenuSelectedIconShadow"/>, and
/// <see cref="RetailMainMenuRightTwinShadow"/>. Settled 255
/// submits <c>0x3E000000</c>, which is capture ShadowTint, so
/// this type does not replace ShadowTint and DrawMainMenu is not
/// a consumer of <see cref="SubmittedColor"/>. ChromeTint stays
/// <c>0x3E7F7F7F</c>. BracketTint stays <c>0xFE7F7F7F</c>. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, the writing-chrome Y, the sine pin,
/// the blink, the chevron colour, the label colour, the
/// selector-bar colour, the writing-chrome colour, the version
/// overlay, the title-logo shadow, the selected-row icon
/// colours, and the 0x00463F3F / 0x00463F83 D8A4 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuRightDecorShadow
{
    /// <summary><c>mov ecx, esi</c> at <c>0x00463D1F</c>.</summary>
    public const uint Site = 0x00463D1Fu;

    /// <summary><c>shl ecx, 6</c> at <c>0x00463D21</c>.</summary>
    public const uint ShiftSite = 0x00463D21u;

    /// <summary><c>shl ecx, 6</c>.</summary>
    public const int ShiftLeft = 6;

    /// <summary><c>sub ecx, esi</c> at <c>0x00463D24</c>.</summary>
    public const uint SubSite = 0x00463D24u;

    /// <summary><c>shl ecx, 16</c> at <c>0x00463D26</c>.</summary>
    public const uint Shift16Site = 0x00463D26u;

    /// <summary><c>and ecx, 0xFF000000</c> at <c>0x00463D29</c>.</summary>
    public const uint AndSite = 0x00463D29u;

    /// <summary>The AND immediate. Black RGB, alpha only.</summary>
    public const uint AlphaMask = 0xFF000000u;

    /// <summary><c>mov eax, [0x0089D8A0]</c> at <c>0x00463D06</c>.</summary>
    public const uint TextureLoadSite = 0x00463D06u;

    /// <summary>Right primary decor texture global. Not 0x0089D8A4.</summary>
    public const uint TextureGlobal = 0x0089D8A0u;

    /// <summary><c>push 0x3EB33333</c> at <c>0x00463D31</c>.</summary>
    public const uint ZPushSite = 0x00463D31u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3EB33333u;

    /// <summary><c>mov ecx, 0x0089D758</c> at <c>0x00463D36</c>.</summary>
    public const uint SurfThisLoadSite = 0x00463D36u;

    /// <summary>CDXSurf this-pointer global. The offset helpers ignore ecx.</summary>
    public const uint SurfThis = 0x0089D758u;

    /// <summary><c>call 0x00468750</c> at <c>0x00463D3B</c>.</summary>
    public const uint YHelperSite = 0x00463D3Bu;

    /// <summary>Leaf fcos of <c>mCounter</c>. Already owned by the ellipse.</summary>
    public const uint YHelper = 0x00468750u;

    /// <summary><c>fadd [0x005DB5C8]</c> at <c>0x00463D40</c>.</summary>
    public const uint DestYAddSite = 0x00463D40u;

    /// <summary>Dest-Y addend pool.</summary>
    public const uint DestYAddGlobal = 0x005DB5C8u;

    /// <summary>Mode-4 dest Y addend. Right-arc ellipse centre 355+10.</summary>
    public const float DestYAdd = 365f;

    /// <summary><c>call 0x00468730</c> at <c>0x00463D4F</c>.</summary>
    public const uint XHelperSite = 0x00463D4Fu;

    /// <summary>Leaf fsin of <c>mCounter</c>. Already owned by the ellipse.</summary>
    public const uint XHelper = 0x00468730u;

    /// <summary><c>fadd [0x005DB5C4]</c> at <c>0x00463D54</c>.</summary>
    public const uint DestXAddSite = 0x00463D54u;

    /// <summary>Dest-X addend pool.</summary>
    public const uint DestXAddGlobal = 0x005DB5C4u;

    /// <summary>Mode-4 dest X addend. Right-arc ellipse centre 457+5.</summary>
    public const float DestXAdd = 462f;

    /// <summary>Fourth-from-last push at <c>0x00463D13</c>.</summary>
    public const int Mode = 4;

    /// <summary><c>call 0x005563D0</c> at <c>0x00463D5E</c>.</summary>
    public const uint CallSite = 0x00463D5Eu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Leftover body sibling <c>mov eax, esi</c>. Not this pack.</summary>
    public const uint BodySiblingSite = 0x00463D63u;

    /// <summary>Body dest-Y immediate push. 355.0, not this shadow.</summary>
    public const uint BodyDestYPushSite = 0x00463D95u;

    /// <summary>Body dest Y. Right-arc body, not left 344.</summary>
    public const float BodyDestY = 355f;

    /// <summary>Body dest-X immediate push. 457.0, not this shadow.</summary>
    public const uint BodyDestXPushSite = 0x00463D9Au;

    /// <summary>Body dest X. Right-arc body, not left 219.</summary>
    public const float BodyDestX = 457f;

    /// <summary>Body Z leftover <c>0x3E99999A</c>. Not this shadow.</summary>
    public const uint BodyZBits = 0x3E99999Au;

    /// <summary>Settled fade byte after the signed 0..255 clamp.</summary>
    public const int ImageSettledFadeByte = 255;

    /// <summary>Frame 3000 / documented ShadowTint.</summary>
    public const uint CaptureDiffuse = 0x3E000000u;

    /// <summary>The <see cref="ImageSettledFadeByte"/> submit.</summary>
    public const uint SettledSubmitted = 0x3E000000u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render colour is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.35 push is Z. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.35 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Capture <see cref="CaptureDiffuse"/> still owns the quad.</summary>
    public const bool ReplacesShadowTint = false;

    /// <summary>Capture ChromeTint still owns the writing chrome.</summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>Capture BracketTint still owns the body overlay.</summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>0x00463D63 is leftover, not this type.</summary>
    public const bool RedoesBodyOverlay = false;

    /// <summary>The 462/365 addends are the already-shipped ellipse centre.</summary>
    public const bool RedoesDecorShadow = false;

    /// <summary>0x00463F3F is already <see cref="RetailMainMenuRightTwinShadow"/>.</summary>
    public const bool RedoesRightTwinShadow = false;

    /// <summary>0x00463F83 is already <see cref="RetailMainMenuRightTwinOverlay"/>.</summary>
    public const bool RedoesRightTwinOverlay = false;

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
