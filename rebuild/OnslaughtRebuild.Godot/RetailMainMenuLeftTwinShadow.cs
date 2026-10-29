// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D898 *63 alpha shadow — recovered from
/// official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00463A76</c> is
/// a load of the value at 0x0089D898 into eax. That is the leftover left twin
/// (title-bracket-02), not 0x0089D894, not 0x0089D8A0, and
/// not 0x0089D8A4. Do not redo
/// <see cref="RetailMainMenuLeftDecorShadow"/>,
/// <see cref="RetailMainMenuLeftDecorOverlay"/>,
/// <see cref="RetailMainMenuRightDecorShadow"/>,
/// <see cref="RetailMainMenuRightDecorOverlay"/>,
/// <see cref="RetailMainMenuRightTwinShadow"/>, or
/// <see cref="RetailMainMenuRightTwinOverlay"/>.</para>
///
/// <para><b>Site.</b> <c>0x00463A8F</c> is a copy of esi into ecx.
/// <c>0x00463A91</c> is a left shift of ecx by 6.
/// <c>0x00463A94</c> is a subtraction of esi from ecx.
/// <c>0x00463A96</c> is a left shift of ecx by 16.
/// <c>0x00463A99</c> is a mask of ecx with 0xFF000000.
/// ESI is the earlier signed 0..255 fade clamp. Settled fade is
/// 255.</para>
///
/// <para><b>Call.</b> Colour then 0x0089D898 then
/// a push of constant 0x3EB33333 at <c>0x00463AA1</c>. That leftover 0.35
/// is Z, not scale. A load of constant 0x0089D758 into ecx then
/// a direct call to 0x00468730 at <c>0x00463AAB</c>: a float add of the
/// value at 0x005DB5D0 (value 349.0), then a direct call to 0x00468730
/// again at <c>0x00463ABF</c>: a float add of the value at 0x005DB5CC (value 224.0), then
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c>. Dest
/// addends are the already-measured left-arc ellipse centre
/// (219+5, 344+5), not the right dest. Both helpers land on the
/// already-owned <c>0x00468730</c> leaf. The ellipse itself is
/// <c>RetailFrontendDecorShadow</c> — do not redo it. Not a 29%
/// title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> owns
/// 0x0089D8A4 only. This type does not invent that gate or a
/// <c>ShouldDraw</c>. Body leftover <c>0x00463AD3</c> is the
/// not/and/xor pack on the same 0x0089D898 — dest immediates
/// 344.0 / 219.0 and Z <c>0x3E99999A</c>, not this type.</para>
///
/// <para><b>Pack.</b> <c>((esi &lt;&lt; 6) - esi) &lt;&lt; 16 &amp;
/// 0xFF000000</c> = <c>(esi * 63) &lt;&lt; 16</c> in the alpha
/// byte and black RGB. Same *63 pack as
/// <see cref="RetailMainMenuTitleLogoShadow"/>,
/// <see cref="RetailMainMenuSelectedIconShadow"/>,
/// <see cref="RetailMainMenuLeftDecorShadow"/>,
/// <see cref="RetailMainMenuRightTwinShadow"/>, and
/// <see cref="RetailMainMenuRightDecorShadow"/>. Settled 255
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
/// colours, and the 0x00463873 / 0x004638B7 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuLeftTwinShadow
{
    /// <summary>A copy of esi into ecx at <c>0x00463A8F</c>.</summary>
    public const uint Site = 0x00463A8Fu;

    /// <summary>A left shift of ecx by 6 at <c>0x00463A91</c>.</summary>
    public const uint ShiftSite = 0x00463A91u;

    /// <summary>A left shift of ecx by 6.</summary>
    public const int ShiftLeft = 6;

    /// <summary>A subtraction of esi from ecx at <c>0x00463A94</c>.</summary>
    public const uint SubSite = 0x00463A94u;

    /// <summary>A left shift of ecx by 16 at <c>0x00463A96</c>.</summary>
    public const uint Shift16Site = 0x00463A96u;

    /// <summary>A mask of ecx with 0xFF000000 at <c>0x00463A99</c>.</summary>
    public const uint AndSite = 0x00463A99u;

    /// <summary>The AND immediate. Black RGB, alpha only.</summary>
    public const uint AlphaMask = 0xFF000000u;

    /// <summary>A load of the value at 0x0089D898 into eax at <c>0x00463A76</c>.</summary>
    public const uint TextureLoadSite = 0x00463A76u;

    /// <summary>Leftover left-twin texture global. Not D894 / D8A0 / D8A4.</summary>
    public const uint TextureGlobal = 0x0089D898u;

    /// <summary>A push of constant 0x3EB33333 at <c>0x00463AA1</c>.</summary>
    public const uint ZPushSite = 0x00463AA1u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3EB33333u;

    /// <summary>A load of constant 0x0089D758 into ecx at <c>0x00463AA6</c>.</summary>
    public const uint SurfThisLoadSite = 0x00463AA6u;

    /// <summary>CDXSurf this-pointer global. The offset helpers ignore ecx.</summary>
    public const uint SurfThis = 0x0089D758u;

    /// <summary>A direct call to 0x00468730 at <c>0x00463AAB</c>.</summary>
    public const uint YHelperSite = 0x00463AABu;

    /// <summary>Already-owned dest leaf. Both axes land here.</summary>
    public const uint YHelper = 0x00468730u;

    /// <summary>A float add of the value at 0x005DB5D0 at <c>0x00463AB0</c>.</summary>
    public const uint DestYAddSite = 0x00463AB0u;

    /// <summary>Dest-Y addend pool.</summary>
    public const uint DestYAddGlobal = 0x005DB5D0u;

    /// <summary>Mode-4 dest Y addend. Left-arc ellipse centre 344+5.</summary>
    public const float DestYAdd = 349f;

    /// <summary>A direct call to 0x00468730 at <c>0x00463ABF</c>.</summary>
    public const uint XHelperSite = 0x00463ABFu;

    /// <summary>Same already-owned dest leaf as <see cref="YHelper"/>.</summary>
    public const uint XHelper = 0x00468730u;

    /// <summary>A float add of the value at 0x005DB5CC at <c>0x00463AC4</c>.</summary>
    public const uint DestXAddSite = 0x00463AC4u;

    /// <summary>Dest-X addend pool.</summary>
    public const uint DestXAddGlobal = 0x005DB5CCu;

    /// <summary>Mode-4 dest X addend. Left-arc ellipse centre 219+5.</summary>
    public const float DestXAdd = 224f;

    /// <summary>Fourth-from-last push at <c>0x00463A83</c>.</summary>
    public const int Mode = 4;

    /// <summary>A direct call to 0x005563D0 at <c>0x00463ACE</c>.</summary>
    public const uint CallSite = 0x00463ACEu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Leftover body sibling copy of esi into eax. Not this pack.</summary>
    public const uint BodySiblingSite = 0x00463AD3u;

    /// <summary>Body dest-Y immediate push. 344.0, not this shadow.</summary>
    public const uint BodyDestYPushSite = 0x00463B05u;

    /// <summary>Body dest Y. Leftover left-twin body, not right 355.</summary>
    public const float BodyDestY = 344f;

    /// <summary>Body dest-X immediate push. 219.0, not this shadow.</summary>
    public const uint BodyDestXPushSite = 0x00463B0Au;

    /// <summary>Body dest X. Leftover left-twin body, not right 457.</summary>
    public const float BodyDestX = 219f;

    /// <summary>Body Z leftover <c>0x3E99999A</c>. Not this shadow.</summary>
    public const uint BodyZBits = 0x3E99999Au;

    /// <summary>A load of the value at 0x0089D898 into eax at <c>0x00463AF8</c>.</summary>
    public const uint BodyTextureLoadSite = 0x00463AF8u;

    /// <summary>Same leftover left-twin texture as the shadow.</summary>
    public const uint BodyTextureGlobal = 0x0089D898u;

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

    /// <summary>0x00463AD3 leftover not/and/xor is not this pack.</summary>
    public const bool RedoesBodyOverlay = false;

    /// <summary>The 224/349 addends are the already-shipped ellipse centre.</summary>
    public const bool RedoesDecorShadow = false;

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
