// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D88C title-logo body dest X/Y and Z —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x004642CE</c> is
/// <c>mov eax, [0x0089D88C]</c> then later <c>push eax</c>. That is
/// the title-logo body, not 0x0089D7F0 / D894 / D898 / D89C / D8A0 /
/// D8A4. Init stores the previous load of
/// <c>FrontEnd\v3\FE_BEA_Title2.tga</c> (<c>0x0062A3A0</c>)
/// at <c>mov [ebp+0x12C], eax</c> (<c>0x00468E60</c>). The parent
/// card's 0x004642CB is the previous call's last 00; <c>a1</c>
/// starts at 0x004642CE. Do not redo
/// <see cref="RetailMainMenuTitleLogoShadow"/>,
/// <see cref="RetailMainMenuSelectorBarZ"/>,
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
/// <para><b>Site.</b> Colour siblings <c>0x004642E4</c>
/// (<c>and edx, 0xFFAFCFFF</c>) and <c>0x004642F1</c>
/// (<c>or edx, 0x00AFCFFF</c>) already own TitleLogoTint.
/// Shadow colour sibling <c>0x0046424F</c> is already
/// <see cref="RetailMainMenuTitleLogoShadow"/>. Identity scale
/// <c>push 0x3F800000</c> at <c>0x004642DD</c> /
/// <c>0x004642EC</c> / <c>0x004642F7</c> is 1.0, not 0.999.
/// Nearby <c>push 0x3F866666</c> at <c>0x00464269</c> is already
/// ShadowScaleBoost 1.05. <c>0x004642FE</c> is
/// <c>push 0x3F7FBE77</c>. <c>0x00464303</c> is
/// <c>push 0x43020000</c> (130.0). <c>0x00464308</c> is
/// <c>push 0x43A00000</c> (320.0). Do not invent a 29%
/// title-logo scale.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c> (call at
/// <c>0x0046430D</c>, <c>add esp, 0x2C</c> at <c>0x00464312</c>).
/// The leftover 0.999 push is Z, not scale. Dest Y is 130. Dest X
/// is 320. Mode 4 is the centre-anchor. The 2-D consumer ignores z.
/// Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// earlier and owns 0x0089D8A4 only. This type does not invent
/// that gate or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not the colour pack.</b> Body submit stays
/// TitleLogoTint. DrawMainMenu keeps TitleLogoTint and
/// ShadowTint. Scale stays 1.0. Dest X/Y are this leftover. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, the writing-chrome Y, the
/// writing-chrome colour, the writing-chrome Z/X, the sine pin,
/// the blink, the chevron colour, the label colour, the
/// selector-bar colour, the selector-bar Z/X, the version overlay,
/// the title-logo shadow, the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuTitleLogoZ
{
    /// <summary><c>mov eax, [0x0089D88C]</c> at <c>0x004642CE</c>.</summary>
    public const uint TextureLoadSite = 0x004642CEu;

    /// <summary>Title-logo texture global.</summary>
    public const uint TextureGlobal = 0x0089D88Cu;

    /// <summary><c>FrontEnd\v3\FE_BEA_Title2.tga</c> at <c>0x0062A3A0</c>.</summary>
    public const uint TexturePathSite = 0x0062A3A0u;

    /// <summary>Load identity string. Not writing-chrome, not the selector bar.</summary>
    public const string TexturePath = @"FrontEnd\v3\FE_BEA_Title2.tga";

    /// <summary><c>mov [ebp+0x12C], eax</c> at <c>0x00468E60</c>.</summary>
    public const uint TextureStoreSite = 0x00468E60u;

    /// <summary>Store displacement. Previous load of <see cref="TexturePath"/>.</summary>
    public const int TextureStoreOffset = 0x12C;

    /// <summary><c>push 0x3F7FBE77</c> at <c>0x004642FE</c>.</summary>
    public const uint ZPushSite = 0x004642FEu;

    /// <summary><c>push 0x43020000</c> at <c>0x00464303</c>.</summary>
    public const uint YPushSite = 0x00464303u;

    /// <summary><c>push 0x43A00000</c> at <c>0x00464308</c>.</summary>
    public const uint XPushSite = 0x00464308u;

    /// <summary><c>call 0x005563D0</c> at <c>0x0046430D</c>.</summary>
    public const uint CallSite = 0x0046430Du;

    /// <summary><c>push 0x3F800000</c> at <c>0x004642DD</c>. Identity 1.0, not 0.999.</summary>
    public const uint ScalePushSite = 0x004642DDu;

    /// <summary>Identity scale immediate. Not a title-logo 29%.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3F7FBE77u;

    /// <summary>Dest Y immediate bits.</summary>
    public const uint DestYBits = 0x43020000u;

    /// <summary>Dest X immediate bits.</summary>
    public const uint DestXBits = 0x43A00000u;

    /// <summary>Mode-4 dest Y. Title-logo body, not a selector-bar row.</summary>
    public static float DestY => BitConverter.UInt32BitsToSingle(DestYBits);

    /// <summary>Mode-4 dest X. Title-logo body, not writing 458 or selector 219.</summary>
    public static float DestX => BitConverter.UInt32BitsToSingle(DestXBits);

    /// <summary>Fourth-from-last push is mode 4.</summary>
    public const int Mode = 4;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Already-shipped body tint AND. Not this leftover.</summary>
    public const uint ColorSiblingSite = 0x004642E4u;

    /// <summary>Already-shipped body tint OR. Not this leftover.</summary>
    public const uint ColorOrSiblingSite = 0x004642F1u;

    /// <summary>Already-shipped shadow colour sibling. Not this leftover.</summary>
    public const uint ShadowColorSiblingSite = 0x0046424Fu;

    /// <summary>Already-shipped ShadowScaleBoost push. 1.05, not 0.999.</summary>
    public const uint ShadowScalePushSite = 0x00464269u;

    /// <summary>Shadow scale immediate. Already ShadowScaleBoost.</summary>
    public const uint ShadowScaleBits = 0x3F866666u;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render Z is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.999 push is Z and the 1.0 push is identity. Do not invent 29%.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.999 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

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
}
