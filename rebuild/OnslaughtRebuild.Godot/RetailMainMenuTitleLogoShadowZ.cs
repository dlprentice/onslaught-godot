// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render 0x0089D88C title-logo shadow dest leftover
/// and Z — recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Texture.</b> Load at <c>0x00464251</c> is
/// a load of the value at 0x0089D88C into eax then later push of eax. That is
/// the same Title2 surface as the body, not 0x0089D7F0 / D894 /
/// D898 / D89C / D8A0 / D8A4. Init stores the previous load of
/// <c>FrontEnd\v3\FE_BEA_Title2.tga</c> (<c>0x0062A3A0</c>)
/// at a store of eax to frame local +0x12C (<c>0x00468E60</c>). Colour
/// sibling <c>0x0046424F</c> is already
/// <see cref="RetailMainMenuTitleLogoShadow"/>. Body dest/Z
/// sibling <c>0x004642CE</c> is already
/// <see cref="RetailMainMenuTitleLogoZ"/>. Do not redo those, or
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
/// <para><b>Site.</b> Colour sibling <c>0x0046424F</c> already owns
/// the *63 pack. A push of constant 0x3F866666 at <c>0x00464269</c> /
/// <c>0x00464274</c> is already ShadowScaleBoost 1.05.
/// <c>0x0046427B</c> is a push of constant 0x3DCCCCCD. Dest is not an
/// immediate 325/140: a load of constant 0x0089D758 into ecx then
/// a direct call to 0x00468750 at <c>0x00464285</c>: a float add of the
/// value at 0x005D8C20 (value 140.0), then a direct call to 0x00468730
/// at <c>0x00464299</c>: a float add of the value at 0x005DB4A8 (value 325.0). Those
/// addends are the already-measured title-logo ellipse centre
/// (320+5, 130+10). Both helpers land on the already-owned
/// GetShadowOffset leaves. The ellipse itself is
/// <c>RetailFrontendDecorShadow</c> — do not redo it. Do not
/// invent dest immediates. Do not invent a 29% title-logo
/// scale.</para>
///
/// <para><b>Call.</b> Eleven cdecl dwords into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c> (call at
/// <c>0x004642A8</c>, a stack cleanup of 0x2C bytes at <c>0x004642AD</c>).
/// The leftover 0.1 push is Z, not scale. Dest is helper plus
/// addend. Mode 4 is the centre-anchor. The 2-D consumer ignores
/// z. Not a 29% title-logo scale. Not a sheen.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// earlier and owns 0x0089D8A4 only. This type does not invent
/// that gate or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not the colour pack.</b> Shadow submit stays
/// ShadowTint. DrawMainMenu keeps ShadowTint, ShadowScaleBoost,
/// and body dest + sharedShadow. Dest addends are not pushed as
/// RenderSurface immediates. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron
/// colour, the label colour, the selector-bar colour, the
/// selector-bar Z/X, the version overlay, the title-logo shadow
/// colour, the title-logo body dest/Z, the selected-row icon
/// colours, and the 0x00463873 / 0x004638B7 / 0x00463A8F /
/// 0x00463AD3 / 0x00463D1F / 0x00463D63 / 0x00463F3F /
/// 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuTitleLogoShadowZ
{
    /// <summary>A load of the value at 0x0089D88C into eax at <c>0x00464251</c>.</summary>
    public const uint TextureLoadSite = 0x00464251u;

    /// <summary>Title-logo texture global. Same Title2 as the body.</summary>
    public const uint TextureGlobal = 0x0089D88Cu;

    /// <summary><c>FrontEnd\v3\FE_BEA_Title2.tga</c> at <c>0x0062A3A0</c>.</summary>
    public const uint TexturePathSite = 0x0062A3A0u;

    /// <summary>Load identity string. Not writing-chrome, not the selector bar.</summary>
    public const string TexturePath = @"FrontEnd\v3\FE_BEA_Title2.tga";

    /// <summary>A store of eax to frame local +0x12C at <c>0x00468E60</c>.</summary>
    public const uint TextureStoreSite = 0x00468E60u;

    /// <summary>Store displacement. Previous load of <see cref="TexturePath"/>.</summary>
    public const int TextureStoreOffset = 0x12C;

    /// <summary>A push of constant 0x3DCCCCCD at <c>0x0046427B</c>.</summary>
    public const uint ZPushSite = 0x0046427Bu;

    /// <summary>A load of constant 0x0089D758 into ecx at <c>0x00464280</c>.</summary>
    public const uint SurfThisLoadSite = 0x00464280u;

    /// <summary>CDXSurf this-pointer global. The offset helpers ignore ecx.</summary>
    public const uint SurfThis = 0x0089D758u;

    /// <summary>A direct call to 0x00468750 at <c>0x00464285</c>.</summary>
    public const uint YHelperSite = 0x00464285u;

    /// <summary>Already-owned GetShadowOffsetY leaf. Not an immediate dest.</summary>
    public const uint YHelper = 0x00468750u;

    /// <summary>A float add of the value at 0x005D8C20 at <c>0x0046428A</c>.</summary>
    public const uint DestYAddSite = 0x0046428Au;

    /// <summary>Dest-Y addend pool. Not a RenderSurface immediate.</summary>
    public const uint DestYAddGlobal = 0x005D8C20u;

    /// <summary>Dest-Y addend bits. 140.0, the already-measured 130+10 centre.</summary>
    public const uint DestYAddBits = 0x430C0000u;

    /// <summary>Mode-4 dest Y addend. Title-logo ellipse centre, not a dest push.</summary>
    public static float DestYAdd => BitConverter.UInt32BitsToSingle(DestYAddBits);

    /// <summary>A direct call to 0x00468730 at <c>0x00464299</c>.</summary>
    public const uint XHelperSite = 0x00464299u;

    /// <summary>Already-owned GetShadowOffsetX leaf. Not an immediate dest.</summary>
    public const uint XHelper = 0x00468730u;

    /// <summary>A float add of the value at 0x005DB4A8 at <c>0x0046429E</c>.</summary>
    public const uint DestXAddSite = 0x0046429Eu;

    /// <summary>Dest-X addend pool. Not a RenderSurface immediate.</summary>
    public const uint DestXAddGlobal = 0x005DB4A8u;

    /// <summary>Dest-X addend bits. 325.0, the already-measured 320+5 centre.</summary>
    public const uint DestXAddBits = 0x43A28000u;

    /// <summary>Mode-4 dest X addend. Title-logo ellipse centre, not a dest push.</summary>
    public static float DestXAdd => BitConverter.UInt32BitsToSingle(DestXAddBits);

    /// <summary>A direct call to 0x005563D0 at <c>0x004642A8</c>.</summary>
    public const uint CallSite = 0x004642A8u;

    /// <summary>A push of constant 0x3F866666 at <c>0x00464269</c>. Already ShadowScaleBoost.</summary>
    public const uint ScalePushSite = 0x00464269u;

    /// <summary>A push of constant 0x3F866666 at <c>0x00464274</c>. Second scale slot.</summary>
    public const uint ScaleYPushSite = 0x00464274u;

    /// <summary>Shadow scale immediate. Already ShadowScaleBoost 1.05, not 0.1.</summary>
    public const uint ScaleBits = 0x3F866666u;

    /// <summary>Z immediate. Not scale.</summary>
    public const uint ZBits = 0x3DCCCCCDu;

    /// <summary>Fourth-from-last push is mode 4.</summary>
    public const int Mode = 4;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary>Already-shipped shadow colour sibling. Not this leftover.</summary>
    public const uint ColorSiblingSite = 0x0046424Fu;

    /// <summary>Already-shipped body dest/Z sibling. Not this leftover.</summary>
    public const uint BodySiblingSite = 0x004642CEu;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render Z is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The 0.1 push is Z and the 1.05 push is already ShadowScaleBoost.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>The leftover 0.1 is the Z slot, not sx/sy.</summary>
    public const bool TreatsZAsScale = false;

    /// <summary>Dest is helper plus addend. Do not invent 325/140 immediates.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

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

    /// <summary>The 325/140 addends are the already-shipped ellipse centre.</summary>
    public const bool RedoesDecorShadow = false;

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
