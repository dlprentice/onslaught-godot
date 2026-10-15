// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay pre-draw enable-byte leftover —
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
/// <see cref="RetailMainMenuVersionOverlayFont"/>. Post-draw
/// <c>[0x00679B40]=1</c> at <c>0x004641FC</c> is already
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. This leftover
/// is the store of 0 after sprintf and before dest/Z. Do not redo
/// the sprintf, the colour OR, dest/Z, the font slot, or the
/// post-draw stores.</para>
///
/// <para><b>Store.</b> After sprintf at <c>0x00464174</c>
/// (<c>0x0055DE9B</c>), <c>add esp, 0x10</c> at
/// <c>0x00464179</c>, and <c>lea edx, [esp+0x3C]</c> at
/// <c>0x0046417C</c>, <c>0x00464180</c> is
/// <c>mov byte [0x00679B40], 0</c>. The next instruction is
/// <c>push 0</c> at <c>0x00464187</c>. The
/// <c>Text__AsciiToWideScratch</c> call at <c>0x00464191</c>
/// (<c>0x004F7BF0</c>) is a sibling leftover, not this one. The
/// CFrontEnd::Run stores at <c>0x00468500</c> / <c>0x0046858B</c>
/// are also siblings. Do not invent a meaning beyond the store
/// bytes.</para>
///
/// <para><b>Reader.</b> A two-instruction reader at
/// <c>0x00465F00</c> is <c>mov al, [0x00679B40]; ret</c>. Six
/// CALL sites land on that reader. This type does not invent
/// what those callers do with AL.</para>
///
/// <para><b>Not a fade.</b> The post-draw
/// <c>fcom [0.0]</c> is already the title-logo shadow clamp in
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. This type
/// does not invent a version fade, a sheen, dest immediates, or
/// a 2px kerning hack. DrawMainMenu keeps title-font DrawText,
/// VersionTint, Format, DestX, DestY(DesignHeight), and scale
/// 1.0.</para>
///
/// <para><b>Not dest/Z/font/flags.</b> Dest Y stays helper minus
/// 16. Dest X stays 0. Z stays 0.01. Font slot stays 1 /
/// Font13PS. Post-draw restore stays 1. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading, DrawQuitConfirm,
/// HandlePointerConfirm, HandlePointerMotion, the cursor, Apply,
/// dropdown, the colour AND, the writing-chrome Y, the
/// writing-chrome colour, the writing-chrome Z/X, the sine pin,
/// the blink, the chevron colour, the label colour, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font slot,
/// the version post-draw flags, the title-logo shadow dest/Z,
/// the title-logo body dest/Z, the selected-row icon colours,
/// and the 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayEnable
{
    /// <summary>Already-shipped format sibling. Not this leftover.</summary>
    public const uint FormatSiblingSite = 0x0046416Eu;

    /// <summary><c>call 0x0055DE9B</c> at <c>0x00464174</c>.</summary>
    public const uint SprintfSite = 0x00464174u;

    /// <summary>CRT sprintf. Not this leftover.</summary>
    public const uint Sprintf = 0x0055DE9Bu;

    /// <summary><c>add esp, 0x10</c> at <c>0x00464179</c>.</summary>
    public const uint AddEspSite = 0x00464179u;

    /// <summary>Stack pop after sprintf. Four pushed dwords.</summary>
    public const int AddEspImmediate = 0x10;

    /// <summary><c>lea edx, [esp+0x3C]</c> at <c>0x0046417C</c>.</summary>
    public const uint LeaSite = 0x0046417Cu;

    /// <summary>Displacement of the sprintf-buffer lea.</summary>
    public const int LeaDisp = 0x3C;

    /// <summary><c>mov byte [0x00679B40], 0</c> at <c>0x00464180</c>.</summary>
    public const uint StoreSite = 0x00464180u;

    /// <summary>Pre-draw enable-byte global.</summary>
    public const uint EnableByteGlobal = 0x00679B40u;

    /// <summary>Immediate stored after sprintf and before dest/Z.</summary>
    public const byte EnableByteBeforeDraw = 0;

    /// <summary>Already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const byte EnableByteAfterDraw = 1;

    /// <summary><c>push 0</c> immediately after the store.</summary>
    public const uint NextInstructionSite = 0x00464187u;

    /// <summary>
    /// Sibling leftover. <c>call 0x004F7BF0</c> at <c>0x00464191</c>.
    /// </summary>
    public const uint AsciiToWideSiblingSite = 0x00464191u;

    /// <summary><c>Text__AsciiToWideScratch</c>. Not this leftover.</summary>
    public const uint AsciiToWideScratch = 0x004F7BF0u;

    /// <summary><c>mov al, [0x00679B40]; ret</c>.</summary>
    public const uint EnableByteReader = 0x00465F00u;

    /// <summary>First byte of the reader. <c>mov al, moffs8</c>.</summary>
    public const byte ReaderMovAlOpcode = 0xA0;

    /// <summary>Second instruction of the reader. Near <c>ret</c>.</summary>
    public const byte ReaderRetOpcode = 0xC3;

    /// <summary>
    /// The six image CALL sites that land on
    /// <see cref="EnableByteReader"/>. This type does not invent
    /// what those callers do with AL.
    /// </summary>
    public static readonly uint[] ReaderCallSites =
    {
        0x005235A7u,
        0x00523705u,
        0x0052384Bu,
        0x0052398Fu,
        0x00540061u,
        0x005562BDu,
    };

    /// <summary>CFrontEnd::Run sibling store of 1. Not this leftover.</summary>
    public const uint RunStoreOneSiblingSite = 0x00468500u;

    /// <summary>CFrontEnd::Run sibling store of 0. Not this leftover.</summary>
    public const uint RunStoreZeroSiblingSite = 0x0046858Bu;

    /// <summary>Already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const uint AfterDrawSiblingSite = 0x004641FCu;

    /// <summary>This leftover owns the pre-draw store of 0.</summary>
    public const bool OwnsBeforeDrawStore = true;

    /// <summary>0x004641FC is already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const bool OwnsAfterDrawStore = false;

    /// <summary>CFrontEnd::Run stores are siblings, not this leftover.</summary>
    public const bool OwnsCFrontEndRunStores = false;

    /// <summary>0x00464191 is a sibling leftover, not this one.</summary>
    public const bool OwnsAsciiToWide = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render stores are not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is a pre-draw store, not a title-logo scale.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>Dest Y stays helper minus 16. Do not invent a 464 immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The leftover is a store of 0, not a version fade.</summary>
    public const bool InventsFade = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x004641C9 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x004641E4 is already <see cref="RetailMainMenuVersionOverlayFont"/>.</summary>
    public const bool RedoesVersionOverlayFont = false;

    /// <summary>0x004641FC is already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const bool RedoesVersionOverlayFlags = false;

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

    /// <summary>
    /// The pre-draw immediate. Not a fade and not dest/Z.
    /// </summary>
    public static byte BeforeDrawStore() => EnableByteBeforeDraw;
}
