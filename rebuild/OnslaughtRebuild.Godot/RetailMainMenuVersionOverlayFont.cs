// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay font-slot leftover —
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
/// <see cref="RetailMainMenuVersionOverlayZ"/>. This leftover is
/// the <c>push 1</c> / <c>CPlatform__Font</c> select. Do not redo
/// the sprintf, the colour OR, or dest/Z.</para>
///
/// <para><b>Select.</b> <c>0x00515A70</c> is
/// <c>mov eax, [esp+4]; cmp eax, 3; ja zero; jmp [eax*4+0x00515AA0]</c>
/// (<c>RET 4</c>). Jump-table slot 1 is <c>0x00515A8C</c>:
/// <c>mov eax, [ecx+0x20]; ret 4</c>. It is not this+0x1C.
/// Slot 2 is this+0x1C. Slot 0 is this+0x18. Slot 3 is this+0x24.
/// InitFonts at <c>0x0051571B</c> / <c>0x0051571D</c> pushes 16
/// then <c>0x0063E10C</c> <c>"Font13PS.tga"</c>, stores
/// <c>[esi+0x20]</c> at <c>0x0051572C</c>, then
/// <c>CDXBitmapFont__InitTextureFontSlot</c> at <c>0x0053F830</c>.
/// GPL <c>Platform.h</c> <c>FONT_SMALL</c> is 1;
/// <c>PCPlatform.cpp</c> InitFonts loads Font13PS at 16 and
/// <c>Font(FONT_SMALL)</c> returns <c>SmallFont()</c>. The parent
/// card's this+0x1C is slot 2 / Terminal, not this leftover.</para>
///
/// <para><b>Call.</b> Version site <c>push 1</c> at
/// <c>0x004641E4</c>, <c>call 0x00515A70</c> at
/// <c>0x004641E6</c>, <c>mov ecx, eax</c>, then
/// <c>CDXFont__DrawTextDynamic</c> at <c>0x004641ED</c>. There is
/// no <c>CDXFont__GetTextExtent</c> on the sprintf buffer. The
/// 2px MeasureText residual stays open; do not invent a kerning
/// hack. DrawMainMenu keeps title-font DrawText (already
/// Font13PS), VersionTint, Format, DestX, DestY(DesignHeight),
/// and scale 1.0.</para>
///
/// <para><b>Not the twin-fade gate.</b> <c>0x00463E8D</c> sits
/// earlier and owns 0x0089D8A4 only. This type does not invent
/// that gate or a <c>ShouldDraw</c>.</para>
///
/// <para><b>Not dest/Z.</b> Dest Y stays helper minus 16. Dest X
/// stays 0. Z stays 0.01. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm,
/// HandlePointerMotion, the cursor, Apply, dropdown, the colour
/// AND, the writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron
/// colour, the label colour, the selector-bar colour, the
/// selector-bar Z/X, the version format/colour, the version
/// dest/Z, the title-logo shadow dest/Z, the title-logo body
/// dest/Z, the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayFont
{
    /// <summary><c>CPlatform__Font</c>. <c>RET 4</c>.</summary>
    public const uint FontHelper = 0x00515A70u;

    /// <summary><c>jmp [eax*4+0x00515AA0]</c>.</summary>
    public const uint JumpTableSite = 0x00515AA0u;

    /// <summary>Slot 0 case. <c>mov eax, [ecx+0x18]; ret 4</c>.</summary>
    public const uint Slot0Case = 0x00515A80u;

    /// <summary>Slot 1 case. <c>mov eax, [ecx+0x20]; ret 4</c>.</summary>
    public const uint Slot1Case = 0x00515A8Cu;

    /// <summary>Slot 2 case. <c>mov eax, [ecx+0x1C]; ret 4</c>.</summary>
    public const uint Slot2Case = 0x00515A86u;

    /// <summary>Slot 3 case. <c>mov eax, [ecx+0x24]; ret 4</c>.</summary>
    public const uint Slot3Case = 0x00515A92u;

    /// <summary>FONT_NORMAL / font22.512.tga.</summary>
    public const int Slot0Offset = 0x18;

    /// <summary>FONT_SMALL / Font13PS.tga. Not this+0x1C.</summary>
    public const int Slot1Offset = 0x20;

    /// <summary>FONT_DEBUG / Terminal.</summary>
    public const int Slot2Offset = 0x1C;

    /// <summary>FONT_TITLE / TitleFont.tga.</summary>
    public const int Slot3Offset = 0x24;

    /// <summary><c>push 1</c> at <c>0x004641E4</c>.</summary>
    public const uint FontSlotPushSite = 0x004641E4u;

    /// <summary>GPL <c>FONT_SMALL</c>. Same push as the dest/Z sibling.</summary>
    public const int FontSlot = 1;

    /// <summary><c>call 0x00515A70</c> at <c>0x004641E6</c>.</summary>
    public const uint FontCallSite = 0x004641E6u;

    /// <summary><c>call 0x00465710</c> at <c>0x004641ED</c>.</summary>
    public const uint DrawCallSite = 0x004641EDu;

    /// <summary><c>CDXFont__DrawTextDynamic</c>. <c>RET 0x28</c>.</summary>
    public const uint DrawTextDynamic = 0x00465710u;

    /// <summary>Not called on the version sprintf buffer.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary><c>push 0x0063E10C</c> at <c>0x0051571D</c>.</summary>
    public const uint Slot1NamePushSite = 0x0051571Du;

    /// <summary>Image bytes at the slot-1 name push.</summary>
    public const uint Slot1NameSite = 0x0063E10Cu;

    /// <summary>InitFonts name for <see cref="Slot1Offset"/>.</summary>
    public const string Slot1Name = "Font13PS.tga";

    /// <summary><c>push 16</c> at <c>0x0051571B</c>.</summary>
    public const uint Slot1CellPushSite = 0x0051571Bu;

    /// <summary>Font13PS cell size. Matches GPL InitialiseAsBitmapFont 16.</summary>
    public const int Slot1CellSize = 16;

    /// <summary><c>mov [esi+0x20], eax</c> at <c>0x0051572C</c>.</summary>
    public const uint Slot1StoreSite = 0x0051572Cu;

    /// <summary><c>CDXBitmapFont__InitTextureFontSlot</c> after the store.</summary>
    public const uint InitTextureFontSlot = 0x0053F830u;

    /// <summary>GPL <c>Platform.h</c> <c>FONT_SMALL</c>.</summary>
    public const int GplFontSmall = 1;

    /// <summary>GPL <c>Platform.h</c> <c>FONT_DEBUG</c>. Slot 2, not this leftover.</summary>
    public const int GplFontDebug = 2;

    /// <summary>The parent card's this+0x1C is slot 2.</summary>
    public const bool Slot1IsThisPlus1C = false;

    /// <summary>Slot 1 is FONT_SMALL, not FONT_DEBUG.</summary>
    public const bool Slot1IsDebugFont = false;

    /// <summary>Version leftover has no GetTextExtent on the sprintf buffer.</summary>
    public const bool HasGetTextExtentOnSprintf = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render font select is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is font select, not a title-logo scale.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x004641C9 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x004642CE is already <see cref="RetailMainMenuTitleLogoZ"/>.</summary>
    public const bool RedoesTitleLogoZ = false;

    /// <summary>0x00464251 is already <see cref="RetailMainMenuTitleLogoShadowZ"/>.</summary>
    public const bool RedoesTitleLogoShadowZ = false;

    /// <summary>0x00462FED is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This select sits after it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>
    /// Jump-table this-offset for <paramref name="slot"/>. Out of
    /// 0..3 returns 0, matching the <c>ja</c> zero path.
    /// </summary>
    public static int SelectOffset(int slot) => slot switch
    {
        0 => Slot0Offset,
        1 => Slot1Offset,
        2 => Slot2Offset,
        3 => Slot3Offset,
        _ => 0,
    };

    /// <summary>
    /// InitFonts name stored at <see cref="SelectOffset"/>. Empty
    /// outside 0..3.
    /// </summary>
    public static string SelectName(int slot) => slot switch
    {
        0 => "font22.512.tga",
        1 => Slot1Name,
        2 => "Terminal",
        3 => "TitleFont.tga",
        _ => string.Empty,
    };
}
