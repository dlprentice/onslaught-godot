// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay post-draw flag leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Format at <c>0x0046416E</c> and the settled
/// pack at <c>0x004641B1</c> / <c>0x004641B4</c> are already
/// <see cref="RetailMainMenuVersionOverlay"/>. Dest Y
/// (GetWindowHeight-16), dest X push 0, and Z 0.01 are already
/// <see cref="RetailMainMenuVersionOverlayZ"/>. Font slot 1 /
/// Font13PS is already
/// <see cref="RetailMainMenuVersionOverlayFont"/>. This leftover
/// starts after <c>CDXFont__DrawTextDynamic</c> returns. Do not
/// redo the sprintf, the colour OR, dest/Z, or the font slot.</para>
///
/// <para><b>Stores.</b> After the draw at <c>0x004641ED</c>,
/// <c>0x004641FC</c> is <c>mov byte [0x00679B40], 1</c>,
/// <c>0x00464203</c> is <c>mov byte [0x009C68AC], 0</c>, and
/// <c>0x0046420A</c> is <c>mov byte [0x009C690D], 1</c>. Those
/// three writes are unconditional. The same <c>0x009C68AC=0</c> /
/// <c>0x009C690D=1</c> pair already sits at the CFEPMain::Render
/// prologue <c>0x00462D5E</c> / <c>0x00462D65</c>. The earlier
/// <c>mov byte [0x00679B40], 0</c> at <c>0x00464180</c> (after
/// sprintf, before dest/Z) is already
/// <see cref="RetailMainMenuVersionOverlayEnable"/>, not this one.
/// A two-instruction reader at <c>0x00465F00</c> is
/// <c>mov al, [0x00679B40]; ret</c>. Do not invent a meaning
/// beyond the store bytes.</para>
///
/// <para><b>Not a fade.</b> <c>0x004641F2</c> is
/// <c>fld [esp+0x38]</c> and <c>0x004641F6</c> is
/// <c>fcom [0x005D856C]</c>. That dword is image
/// <c>0x00000000</c> / <c>+0.0f</c>. The compare is consumed
/// later at <c>0x00464211</c> <c>fnstsw ax</c> /
/// <c>test ah, 1</c> by the already-owned title-logo shadow
/// clamp (<c>0.0</c> / <c>1.0</c> at <c>0x005D8568</c> then
/// <c>fmul [0x005D8C70]</c> = 255, then
/// <see cref="RetailMainMenuTitleLogoShadow"/> at
/// <c>0x0046423D</c>). The three stores sit between
/// <c>fcom</c> and <c>fnstsw</c>. This type does not invent a
/// version fade, a sheen, dest immediates, or a 2px kerning
/// hack.</para>
///
/// <para><b>Not dest/Z/font.</b> Dest Y stays helper minus 16.
/// Dest X stays 0. Z stays 0.01. Font slot stays 1 / Font13PS.
/// DrawMainMenu keeps title-font DrawText, VersionTint, Format,
/// DestX, DestY(DesignHeight), and scale 1.0. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, the writing-chrome Y, the
/// writing-chrome colour, the writing-chrome Z/X, the sine pin,
/// the blink, the chevron colour, the label colour, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font slot,
/// the title-logo shadow dest/Z, the title-logo body dest/Z,
/// the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayFlags
{
    /// <summary><c>call 0x00465710</c> at <c>0x004641ED</c>.</summary>
    public const uint DrawCallSite = 0x004641EDu;

    /// <summary><c>CDXFont__DrawTextDynamic</c>. <c>RET 0x28</c>.</summary>
    public const uint DrawTextDynamic = 0x00465710u;

    /// <summary><c>fld [esp+0x38]</c> at <c>0x004641F2</c>.</summary>
    public const uint FldSite = 0x004641F2u;

    /// <summary><c>fcom [0x005D856C]</c> at <c>0x004641F6</c>.</summary>
    public const uint FcomSite = 0x004641F6u;

    /// <summary>Image dword at the <c>fcom</c>. <c>+0.0f</c>.</summary>
    public const uint FcomZeroGlobal = 0x005D856Cu;

    /// <summary>Image bits at <see cref="FcomZeroGlobal"/>.</summary>
    public const uint FcomZeroBits = 0x00000000u;

    /// <summary><c>fnstsw ax</c> at <c>0x00464211</c>. Consumes the <c>fcom</c>.</summary>
    public const uint FnstswSite = 0x00464211u;

    /// <summary><c>mov byte [0x00679B40], 1</c> at <c>0x004641FC</c>.</summary>
    public const uint EnableByteStoreSite = 0x004641FCu;

    /// <summary>Post-draw enable-byte global.</summary>
    public const uint EnableByteGlobal = 0x00679B40u;

    /// <summary>Immediate stored after DrawTextDynamic.</summary>
    public const byte EnableByteAfterDraw = 1;

    /// <summary>
    /// Sibling leftover after sprintf. Not this leftover.
    /// <c>mov byte [0x00679B40], 0</c>.
    /// </summary>
    public const uint EnableByteBeforeDrawSite = 0x00464180u;

    /// <summary>Immediate at <see cref="EnableByteBeforeDrawSite"/>.</summary>
    public const byte EnableByteBeforeDraw = 0;

    /// <summary><c>mov al, [0x00679B40]; ret</c>.</summary>
    public const uint EnableByteReader = 0x00465F00u;

    /// <summary><c>mov byte [0x009C68AC], 0</c> at <c>0x00464203</c>.</summary>
    public const uint StateAStoreSite = 0x00464203u;

    /// <summary>First render-state global of the pair.</summary>
    public const uint StateAGlobal = 0x009C68ACu;

    /// <summary>Immediate stored after DrawTextDynamic.</summary>
    public const byte StateAAfterDraw = 0;

    /// <summary>Same pair at CFEPMain::Render prologue.</summary>
    public const uint StateAPrologueSite = 0x00462D5Eu;

    /// <summary><c>mov byte [0x009C690D], 1</c> at <c>0x0046420A</c>.</summary>
    public const uint StateBStoreSite = 0x0046420Au;

    /// <summary>Second render-state global of the pair.</summary>
    public const uint StateBGlobal = 0x009C690Du;

    /// <summary>Immediate stored after DrawTextDynamic.</summary>
    public const byte StateBAfterDraw = 1;

    /// <summary>Same pair at CFEPMain::Render prologue.</summary>
    public const uint StateBPrologueSite = 0x00462D65u;

    /// <summary>Already-owned title-logo shadow dest compare.</summary>
    public const uint TitleLogoShadowDestCompare = 0x0046423Du;

    /// <summary>The three stores sit between <c>fcom</c> and <c>fnstsw</c>.</summary>
    public const bool StoresAreUnconditional = true;

    /// <summary>0x00464180 is a sibling leftover, not this one.</summary>
    public const bool OwnsBeforeDrawStore = false;

    /// <summary>The <c>fcom</c> is the title-logo shadow clamp, not a version fade.</summary>
    public const bool InventsFade = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render stores are not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is post-draw stores, not a title-logo scale.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>Dest Y stays helper minus 16. Do not invent a 464 immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x004641C9 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x004641E4 is already <see cref="RetailMainMenuVersionOverlayFont"/>.</summary>
    public const bool RedoesVersionOverlayFont = false;

    /// <summary>0x004642CE is already <see cref="RetailMainMenuTitleLogoZ"/>.</summary>
    public const bool RedoesTitleLogoZ = false;

    /// <summary>0x00464251 is already <see cref="RetailMainMenuTitleLogoShadowZ"/>.</summary>
    public const bool RedoesTitleLogoShadowZ = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

    /// <summary>0x00462FED is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits after it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="FcomZeroBits"/> decoded as IEEE-754 single.</summary>
    public static float FcomZero => BitConverter.UInt32BitsToSingle(FcomZeroBits);

    /// <summary>
    /// The three post-draw immediates. Not a fade and not dest/Z.
    /// </summary>
    public static (byte Enable, byte StateA, byte StateB) AfterDrawStores() =>
        (EnableByteAfterDraw, StateAAfterDraw, StateBAfterDraw);
}
