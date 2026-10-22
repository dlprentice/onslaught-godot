// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay dest leftover and Z —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Format at <c>0x0046416E</c> and the settled
/// pack at <c>0x004641B1</c> / <c>0x004641B4</c> are already
/// <see cref="RetailMainMenuVersionOverlay"/>. This leftover starts
/// after that pack. Do not redo the sprintf or the colour OR.</para>
///
/// <para><b>Site.</b> Identity scale <c>push 0x3F800000</c> at
/// <c>0x004641BA</c> / <c>0x004641BF</c> is 1.0, not 0.01.
/// <c>0x004641C4</c> is <c>push 0x3C23D70A</c>. Dest is not an
/// immediate 464: <c>call 0x00515B00</c> at <c>0x004641C9</c>
/// is <c>PLATFORM__GetWindowHeight</c>
/// (<c>mov eax, [0x00888A0C]; ret</c>), then
/// <c>sub eax, 0x10</c> at <c>0x004641CE</c>,
/// <c>fild [esp+0x4C]</c>, <c>fstp [esp]</c>. That height global
/// sits past the image, so image-initial dest is not 464.
/// Dest X is <c>push 0</c> at <c>0x004641E2</c>.
/// <c>push 1</c> at <c>0x004641E4</c> is the
/// <c>CPlatform__Font</c> slot, then
/// <c>CDXFont__DrawTextDynamic</c>. Do not invent dest
/// immediates. Do not invent a 2px kerning hack. Do not invent
/// a 29% title-logo scale.</para>
///
/// <para><b>Call.</b> <c>CPlatform__Font</c> at <c>0x00515A70</c>
/// (<c>RET 4</c>) then <c>CDXFont__DrawTextDynamic</c> at
/// <c>0x00465710</c> (call at <c>0x004641ED</c>,
/// <c>RET 0x28</c>). The leftover 0.01 push is Z, not scale.
/// Dest Y is helper minus 16. Dest X is 0. The 2-D consumer
/// ignores z. Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// earlier and owns 0x0089D8A4 only. This type does not invent
/// that gate or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not the colour pack.</b> Overlay submit stays
/// VersionTint. DrawMainMenu keeps VersionTint, Format, DestX,
/// DestY(DesignHeight), and scale 1.0. Dest Y is not pushed as
/// a 464 immediate. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron
/// colour, the label colour, the selector-bar colour, the
/// selector-bar Z/X, the version format/colour, the title-logo
/// shadow colour, the title-logo shadow dest/Z, the title-logo
/// body dest/Z, the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayZ
{
    /// <summary><c>call 0x00515B00</c> at <c>0x004641C9</c>.</summary>
    public const uint HeightHelperSite = 0x004641C9u;

    /// <summary><c>PLATFORM__GetWindowHeight</c>. <c>mov eax, [0x00888A0C]; ret</c>.</summary>
    public const uint HeightHelper = 0x00515B00u;

    /// <summary>Window-height global. Past the image, so not an immediate 464.</summary>
    public const uint HeightGlobal = 0x00888A0Cu;

    /// <summary><c>sub eax, 0x10</c> at <c>0x004641CE</c>.</summary>
    public const uint DestYSubSite = 0x004641CEu;

    /// <summary>Integer subtract after GetWindowHeight. Not a dest immediate.</summary>
    public const int DestYSubtract = 0x10;

    /// <summary><c>push 0</c> at <c>0x004641E2</c>.</summary>
    public const uint DestXPushSite = 0x004641E2u;

    /// <summary>Dest X immediate. Integer 0 is also IEEE 0.0.</summary>
    public const float DestX = 0f;

    /// <summary><c>push 0x3C23D70A</c> at <c>0x004641C4</c>.</summary>
    public const uint ZPushSite = 0x004641C4u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3C23D70Au;

    /// <summary><c>push 0x3F800000</c> at <c>0x004641BA</c>.</summary>
    public const uint ScalePushSite = 0x004641BAu;

    /// <summary><c>push 0x3F800000</c> at <c>0x004641BF</c>. Second scale slot.</summary>
    public const uint ScaleYPushSite = 0x004641BFu;

    /// <summary>Identity scale. Already 1.0, not 0.01.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>mov ecx, 0x0088A0A8</c> at <c>0x004641D6</c>.</summary>
    public const uint FontThis = 0x0088A0A8u;

    /// <summary><c>CPlatform__Font</c>. <c>RET 4</c>.</summary>
    public const uint FontHelper = 0x00515A70u;

    /// <summary><c>push 1</c> at <c>0x004641E4</c>.</summary>
    public const uint FontSlotPushSite = 0x004641E4u;

    /// <summary>Font slot. DrawMainMenu keeps the existing title-font DrawText.</summary>
    public const int FontSlot = 1;

    /// <summary><c>call 0x00465710</c> at <c>0x004641ED</c>.</summary>
    public const uint CallSite = 0x004641EDu;

    /// <summary><c>CDXFont__DrawTextDynamic</c>. <c>RET 0x28</c>.</summary>
    public const uint DrawTextDynamic = 0x00465710u;

    /// <summary>Already-shipped format sibling. Not this leftover.</summary>
    public const uint FormatSiblingSite = 0x0046416Eu;

    /// <summary>Already-shipped colour sibling. Not this leftover.</summary>
    public const uint ColorSiblingSite = 0x004641B1u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render dest is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is dest/Z, not a title-logo scale. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.01 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Dest Y is helper minus 16. Do not invent a 464 immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

    /// <summary>0x00464251 is already <see cref="RetailMainMenuTitleLogoShadowZ"/>.</summary>
    public const bool RedoesTitleLogoShadowZ = false;

    /// <summary>0x004642CE is already <see cref="RetailMainMenuTitleLogoZ"/>.</summary>
    public const bool RedoesTitleLogoZ = false;

    /// <summary>0x00462FED is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00462FB9 is already <see cref="RetailMainMenuSelectorBarColor"/>.</summary>
    public const bool RedoesSelectorBarColor = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00462DDD is already <see cref="RetailMainMenuWritingColor"/>.</summary>
    public const bool RedoesWritingColor = false;

    /// <summary>0x00462D46 is already <see cref="RetailMainMenuWritingScroll"/>.</summary>
    public const bool RedoesWritingScroll = false;

    /// <summary>0x00463873 is already <see cref="RetailMainMenuLeftDecorShadow"/>.</summary>
    public const bool RedoesLeftDecorShadow = false;

    /// <summary>0x004638B7 is already <see cref="RetailMainMenuLeftDecorOverlay"/>.</summary>
    public const bool RedoesLeftDecorOverlay = false;

    /// <summary>0x00463A8F is already <see cref="RetailMainMenuLeftTwinShadow"/>.</summary>
    public const bool RedoesLeftTwinShadow = false;

    /// <summary>0x00463AD3 is already <see cref="RetailMainMenuLeftTwinOverlay"/>.</summary>
    public const bool RedoesLeftTwinOverlay = false;

    /// <summary>0x00463D1F is already <see cref="RetailMainMenuRightDecorShadow"/>.</summary>
    public const bool RedoesRightDecorShadow = false;

    /// <summary>0x00463D63 is already <see cref="RetailMainMenuRightDecorOverlay"/>.</summary>
    public const bool RedoesRightDecorOverlay = false;

    /// <summary>0x00463F3F is already <see cref="RetailMainMenuRightTwinShadow"/>.</summary>
    public const bool RedoesRightTwinShadow = false;

    /// <summary>0x00463F83 is already <see cref="RetailMainMenuRightTwinOverlay"/>.</summary>
    public const bool RedoesRightTwinOverlay = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This submit sits after it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float Scale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary>
    /// <c>GetWindowHeight - 16</c>. <paramref name="windowHeight"/> is
    /// the dword at <see cref="HeightGlobal"/>.
    /// </summary>
    public static float DestY(int windowHeight) => windowHeight - DestYSubtract;
}
