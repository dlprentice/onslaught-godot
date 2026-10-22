// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D89C selector-bar texture, dest X and Z —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00462FED</c> is
/// <c>mov eax, [0x0089D89C]</c> then <c>push eax</c>. That is the
/// selector-bar tile, not 0x0089D7F0 / D894 / D898 / D8A0 / D8A4.
/// Init stores the previous load of
/// <c>FrontEnd\v3\FE_BEA_title_text_box.tga</c> (<c>0x0062A300</c>)
/// at <c>mov [ebp+0x13C], eax</c> (<c>0x00468EC3</c>). Do not redo
/// <see cref="RetailMainMenuSelectorBarColor"/>,
/// <see cref="RetailMainMenuWritingZ"/>,
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
/// <para><b>Site.</b> Colour sibling <c>0x00462FB9</c> is already
/// <see cref="RetailMainMenuSelectorBarColor"/>. Identity scale
/// <c>push 0x3F800000</c> at <c>0x00462FC6</c> is 1.0, not 0.33.
/// <c>0x00462FF3</c> is <c>push 0x3EA8F5C3</c>. <c>0x00462FF9</c>
/// is <c>push 0x435B0000</c> (219.0). Dest Y is the selected-row
/// register, not this leftover. Do not invent dest Y.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c> (call at
/// <c>0x00462FFE</c>, <c>add esp, 0x2C</c> at <c>0x00463003</c>).
/// The leftover 0.33 push is Z, not scale. Dest X is 219. Mode 4
/// is the centre-anchor. The 2-D consumer ignores z. Not a 29%
/// title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits later
/// and owns 0x0089D8A4 only. This type does not invent that gate
/// or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not the colour pack.</b> Settled submit stays
/// <c>0x7E000000</c> from
/// <see cref="RetailMainMenuSelectorBarColor"/>. DrawMainMenuSelectorBar
/// keeps <c>SubmittedColor</c> and <c>_titleTextBox</c>. Scale stays
/// 1.0. Dest X is this leftover. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron colour,
/// the label colour, the selector-bar colour, the version overlay,
/// the title-logo shadow, the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuSelectorBarZ
{
    /// <summary><c>mov eax, [0x0089D89C]</c> at <c>0x00462FED</c>.</summary>
    public const uint TextureLoadSite = 0x00462FEDu;

    /// <summary>Selector-bar texture global.</summary>
    public const uint TextureGlobal = 0x0089D89Cu;

    /// <summary><c>FrontEnd\v3\FE_BEA_title_text_box.tga</c> at <c>0x0062A300</c>.</summary>
    public const uint TexturePathSite = 0x0062A300u;

    /// <summary>Load identity string. Not writing-chrome, not a bracket.</summary>
    public const string TexturePath = @"FrontEnd\v3\FE_BEA_title_text_box.tga";

    /// <summary><c>mov [ebp+0x13C], eax</c> at <c>0x00468EC3</c>.</summary>
    public const uint TextureStoreSite = 0x00468EC3u;

    /// <summary>Store displacement. Previous load of <see cref="TexturePath"/>.</summary>
    public const int TextureStoreOffset = 0x13C;

    /// <summary><c>push 0x3EA8F5C3</c> at <c>0x00462FF3</c>.</summary>
    public const uint ZPushSite = 0x00462FF3u;

    /// <summary><c>push 0x435B0000</c> at <c>0x00462FF9</c>.</summary>
    public const uint XPushSite = 0x00462FF9u;

    /// <summary><c>call 0x005563D0</c> at <c>0x00462FFE</c>.</summary>
    public const uint CallSite = 0x00462FFEu;

    /// <summary><c>push 0x3F800000</c> at <c>0x00462FC6</c>. Identity 1.0, not 0.33.</summary>
    public const uint ScalePushSite = 0x00462FC6u;

    /// <summary>Identity scale immediate. Not a title-logo 29%.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3EA8F5C3u;

    /// <summary>Dest X immediate bits.</summary>
    public const uint DestXBits = 0x435B0000u;

    /// <summary>Mode-4 dest X. Selector bar, not writing 458 or right 457.</summary>
    public static float DestX => BitConverter.UInt32BitsToSingle(DestXBits);

    /// <summary>Fourth-from-last push is mode 4.</summary>
    public const int Mode = 4;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Already-shipped colour sibling. 0x0089D89C, not this leftover.</summary>
    public const uint ColorSiblingSite = 0x00462FB9u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render Z is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.33 push is Z and the 1.0 push is identity. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.33 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

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

    /// <summary>0x00463E8D is the D8A4 twin gate. This submit sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float Scale => BitConverter.UInt32BitsToSingle(ScaleBits);
}
