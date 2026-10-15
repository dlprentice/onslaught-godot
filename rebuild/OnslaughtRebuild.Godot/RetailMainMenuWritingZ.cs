// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D7F0 writing-chrome Z and dest X —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00462DC4</c> is
/// <c>mov edx, [0x0089D7F0]</c>. That is Forseti writing large, not
/// 0x0089D894 / D898 / D89C / D8A0 / D8A4. Do not redo
/// <see cref="RetailMainMenuWritingColor"/>,
/// <see cref="RetailMainMenuWritingScroll"/>,
/// <see cref="RetailMainMenuLeftDecorShadow"/>,
/// <see cref="RetailMainMenuLeftDecorOverlay"/>,
/// <see cref="RetailMainMenuLeftTwinShadow"/>,
/// <see cref="RetailMainMenuLeftTwinOverlay"/>,
/// <see cref="RetailMainMenuRightDecorShadow"/>,
/// <see cref="RetailMainMenuRightDecorOverlay"/>,
/// <see cref="RetailMainMenuRightTwinShadow"/>, or
/// <see cref="RetailMainMenuRightTwinOverlay"/>.</para>
///
/// <para><b>Site.</b> <see cref="RetailMainMenuWritingScroll"/> owns
/// the Y prologue at <c>0x00462D46</c> and says it does not own the
/// 458 X or the 0.9 Z. Tile 0 <c>0x00462DFF</c> is
/// <c>push 0x3F666666</c>. <c>0x00462E05</c> is
/// <c>push 0x43E50000</c> (458.0). Tile 1 <c>0x00462E39</c> /
/// <c>0x00462E42</c> and tile 2 <c>0x00462E76</c> /
/// <c>0x00462E7F</c> push the same pair. Identity scale
/// <c>push 0x3F800000</c> at <c>0x00462DCC</c> is 1.0, not 0.9.
/// Colour sibling <c>0x00462DDD</c> is already
/// <see cref="RetailMainMenuWritingColor"/>.</para>
///
/// <para><b>Call.</b> Three cdecl submits into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c> (tile 0
/// <c>0x00462E0A</c>, tile 1 <c>0x00462E47</c>, tile 2
/// <c>0x00462E84</c>). The leftover 0.9 push is Z, not scale. Dest X
/// is 458 on every tile. The 2-D consumer ignores z. Not a 29%
/// title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits later
/// and owns 0x0089D8A4 only. This type does not invent that gate
/// or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not the capture tint.</b> Frame 3000 draws 3, 4 and 5
/// stay <c>0x3E7F7F7F</c>. This type does not replace
/// <c>ChromeTint</c> and DrawMainMenu is not a consumer of a
/// submitted colour. Scale stays 1.0. Y stays
/// <see cref="RetailMainMenuWritingScroll.TileY"/>. X stays
/// <see cref="RetailMainMenuWritingScroll.TileX"/>, which is this
/// dest. Not <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, the writing-chrome Y,
/// the writing-chrome colour, the sine pin, the blink, the chevron
/// colour, the label colour, the selector-bar colour, the version
/// overlay, the title-logo shadow, the selected-row icon colours,
/// and the 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuWritingZ
{
    /// <summary><c>push 0x3F666666</c> at <c>0x00462DFF</c>.</summary>
    public const uint Tile0ZPushSite = 0x00462DFFu;

    /// <summary><c>push 0x43E50000</c> at <c>0x00462E05</c>.</summary>
    public const uint Tile0XPushSite = 0x00462E05u;

    /// <summary><c>call 0x005563D0</c> at <c>0x00462E0A</c>.</summary>
    public const uint Tile0CallSite = 0x00462E0Au;

    /// <summary><c>push 0x3F666666</c> at <c>0x00462E39</c>.</summary>
    public const uint Tile1ZPushSite = 0x00462E39u;

    /// <summary><c>push 0x43E50000</c> at <c>0x00462E42</c>.</summary>
    public const uint Tile1XPushSite = 0x00462E42u;

    /// <summary><c>call 0x005563D0</c> at <c>0x00462E47</c>.</summary>
    public const uint Tile1CallSite = 0x00462E47u;

    /// <summary><c>push 0x3F666666</c> at <c>0x00462E76</c>.</summary>
    public const uint Tile2ZPushSite = 0x00462E76u;

    /// <summary><c>push 0x43E50000</c> at <c>0x00462E7F</c>.</summary>
    public const uint Tile2XPushSite = 0x00462E7Fu;

    /// <summary><c>call 0x005563D0</c> at <c>0x00462E84</c>.</summary>
    public const uint Tile2CallSite = 0x00462E84u;

    /// <summary><c>mov edx, [0x0089D7F0]</c> at <c>0x00462DC4</c>.</summary>
    public const uint TextureLoadSite = 0x00462DC4u;

    /// <summary>Forseti writing-chrome texture global.</summary>
    public const uint TextureGlobal = 0x0089D7F0u;

    /// <summary><c>push 0x3F800000</c> at <c>0x00462DCC</c>. Identity 1.0, not 0.9.</summary>
    public const uint ScalePushSite = 0x00462DCCu;

    /// <summary>Identity scale immediate. Not a title-logo 29%.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3F666666u;

    /// <summary>Tile 0 Z bits. Same dword as tiles 1 and 2.</summary>
    public const uint Tile0ZBits = ZBits;

    /// <summary>Tile 1 Z bits.</summary>
    public const uint Tile1ZBits = ZBits;

    /// <summary>Tile 2 Z bits.</summary>
    public const uint Tile2ZBits = ZBits;

    /// <summary>Dest X immediate bits.</summary>
    public const uint DestXBits = 0x43E50000u;

    /// <summary>Tile 0 dest X bits. Same dword as tiles 1 and 2.</summary>
    public const uint Tile0DestXBits = DestXBits;

    /// <summary>Tile 1 dest X bits.</summary>
    public const uint Tile1DestXBits = DestXBits;

    /// <summary>Tile 2 dest X bits.</summary>
    public const uint Tile2DestXBits = DestXBits;

    /// <summary>Mode-4 dest X. Writing chrome, not left 219 or right 457.</summary>
    public static float DestX => BitConverter.UInt32BitsToSingle(DestXBits);

    /// <summary>Fourth-from-last push is mode 4 on each tile.</summary>
    public const int Mode = 4;

    /// <summary>Three unrolled 0x0089D7F0 submits.</summary>
    public const int TileCount = 3;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Already-shipped colour sibling. 0x0089D7F0, not this leftover.</summary>
    public const uint ColorSiblingSite = 0x00462DDDu;

    /// <summary>Already-shipped Y sibling. Not this leftover.</summary>
    public const uint ScrollSiblingSite = 0x00462D46u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render Z is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.9 push is Z and the 1.0 push is identity. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.9 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Capture ChromeTint still owns the writing chrome.</summary>
    public const bool ReplacesChromeTint = false;

    /// <summary>Capture BracketTint still owns the brackets.</summary>
    public const bool ReplacesBracketTint = false;

    /// <summary>Capture ShadowTint still owns the shadows.</summary>
    public const bool ReplacesShadowTint = false;

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

    /// <summary>0x00463E8D is the D8A4 twin gate. This trio sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float Scale => BitConverter.UInt32BitsToSingle(ScaleBits);
}
