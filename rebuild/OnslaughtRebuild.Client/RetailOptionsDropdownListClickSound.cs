// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> click-hit sound leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para><b>Sibling.</b> Expanded list click at <c>0x004A4010</c> is
/// already <see cref="RetailOptionsDropdownListClick"/>. Expanded list
/// hover at <c>0x004A3FA6</c> is already
/// <see cref="RetailOptionsDropdownListHover"/>. Expanded list colour
/// at <c>0x004A3F6C</c> is already
/// <see cref="RetailOptionsDropdownListColor"/>. Expanded list dest Y
/// at <c>0x004A3F3C</c> is already
/// <see cref="RetailOptionsDropdownListDestY"/>. Expanded list dest X
/// at <c>0x004A3FCD</c> is already
/// <see cref="RetailOptionsDropdownListDest"/>. Expanded panel dest
/// at <c>0x004A3F36</c> is already
/// <see cref="RetailOptionsDropdownPanelDest"/>. Collapsed value dest
/// at <c>0x004A40B4</c> is already
/// <see cref="RetailOptionsDropdownValueDest"/>. Label dest at
/// <c>0x004A3D19</c> is already <see cref="RetailOptionsDropdownDest"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. Post-loop cancel at
/// <c>0x004A4059</c> is already
/// <see cref="RetailOptionsDropdownListCancel"/>. This leftover is the
/// post-write <c>push 1</c> / <c>call 0x00468770</c>. Do not redo
/// those. Do not invent dest Y as 5, 15.5, 268, 284, or 304. Do not
/// invent dest X as 322.5 or dest from 148.0. Do not invent dest as
/// the 2.0 constant.</para>
///
/// <para><b>Sound.</b> Official 74154bfa independently re-read:
/// <c>0x004A403A</c> is <c>push 1</c>.
/// <c>0x004A403C</c> is <c>call 0x00468770</c>.
/// <c>0x004A4041</c> is <c>add esp, 4</c>.
/// Pending skip and live apply both join at the push. A miss jumps to
/// <c>0x004A4044</c> and skips the call. That is
/// <c>CFrontEnd__PlaySound(1)</c> Front End Select after the click
/// writes. Dest Y does not. Colour leftover already consults
/// currentIndex. Hover leftover already owns <c>0x004A3FA6</c>. Click
/// leftover already owns <c>0x004A4010</c>. Cancel leftover already
/// owns <c>0x004A4059</c>. Nearby 15.5, 322.5, and 148.0 are
/// measurements, not dest. Dest is not the 2.0 constant. Latch-to-button
/// wiring of <c>0x0089BE28</c> is later.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// expanded list click, expanded list cancel, and the 0x00463669
/// compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListClickSound
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>push 1</c>. Select sound leftover. Not dest.</summary>
    public const uint SoundPushSite = 0x004A403Au;

    /// <summary><c>CFrontEnd__PlaySound(1)</c>. Front End Select. Not dest.</summary>
    public const uint SoundId = 1u;

    /// <summary><c>call 0x00468770</c>. Select sound leftover. Not dest.</summary>
    public const uint SoundCallSite = 0x004A403Cu;

    /// <summary>PlaySound leftover. Cancel back sound at <c>0x004A4087</c> is already pinned.</summary>
    public const uint PlaySound = 0x00468770u;

    /// <summary><c>add esp, 4</c> after the cdecl sound helper.</summary>
    public const uint SoundPopSite = 0x004A4041u;

    /// <summary><c>add esp, 4</c> after the cdecl sound helper.</summary>
    public const uint SoundPop = 4u;

    /// <summary>Miss target at <c>0x004A4044</c> skips the sound leftover.</summary>
    public const uint MissJumpTarget = 0x004A4044u;

    /// <summary>Click hit at <c>0x004A4010</c> is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const uint ClickHitSite = 0x004A4010u;

    /// <summary><c>call [eax+0x38]</c> is already click leftover. Sound is after it.</summary>
    public const uint SetCallSite = 0x004A4037u;

    /// <summary>Cancel at <c>0x004A4059</c> is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const uint CancelSite = 0x004A4059u;

    /// <summary>Hover hit at <c>0x004A3FA6</c> is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const uint HoverHitSite = 0x004A3FA6u;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Already click leftover. Sound does not write it.</summary>
    public const uint CurrentIndexOffset = 0x20u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>. Already click leftover. Sound does not write it.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary>Dest Y is not a 5 push.</summary>
    public const bool InventsDestY5 = false;

    /// <summary>Left-aligned dest X is not a 5 push.</summary>
    public const bool InventsDestX5 = false;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not a 284 push.</summary>
    public const bool InventsDestY284 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>The 2.0 pad is not dest or the sound itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Sound is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Sound does not write currentIndex. Dest Y does not either.</summary>
    public const bool UsesCurrentIndex = false;

    /// <summary>0x004A403C is this leftover.</summary>
    public const bool IsClickSound = true;

    /// <summary>0x004A4010 is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const bool IsClickHit = false;

    /// <summary>0x004A3FA6 is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const bool IsHoverHit = false;

    /// <summary>0x004A4059 is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const bool IsCancel = false;

    /// <summary>The leftover does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3394 is already <see cref="RetailOptionsMenuItemDest"/>.</summary>
    public const bool RedoesMenuItemDest = false;

    /// <summary>0x004A3301 is already <see cref="RetailOptionsMenuItemIconDest"/>.</summary>
    public const bool RedoesMenuItemIconDest = false;

    /// <summary>0x004A3D19 is already <see cref="RetailOptionsDropdownDest"/>.</summary>
    public const bool RedoesDropdownDest = false;

    /// <summary>0x004A40B4 is already <see cref="RetailOptionsDropdownValueDest"/>.</summary>
    public const bool RedoesDropdownValueDest = false;

    /// <summary>0x004A3FCD is already <see cref="RetailOptionsDropdownListDest"/>.</summary>
    public const bool RedoesDropdownListDest = false;

    /// <summary>0x004A3F36 is already <see cref="RetailOptionsDropdownPanelDest"/>.</summary>
    public const bool RedoesDropdownPanelDest = false;

    /// <summary>0x004A3F3C is already <see cref="RetailOptionsDropdownListDestY"/>.</summary>
    public const bool RedoesDropdownListDestY = false;

    /// <summary>0x004A3F6C is already <see cref="RetailOptionsDropdownListColor"/>.</summary>
    public const bool RedoesDropdownListColor = false;

    /// <summary>0x004A3FA6 is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const bool RedoesDropdownListHover = false;

    /// <summary>0x004A4010 is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const bool RedoesDropdownListClick = false;

    /// <summary>0x004A4059 is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const bool RedoesDropdownListCancel = false;

    /// <summary>0x004A33FC is already <see cref="RetailOptionsMenuItemColor"/>.</summary>
    public const bool RedoesMenuItemColor = false;

    /// <summary>0x004A4310 / 0x004A3C69 is already <see cref="RetailOptionsApplyPulse"/>.</summary>
    public const bool RedoesApplyPulse = false;

    /// <summary>0x00463647 is already <c>RetailMainMenuLanguagePitch</c>.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in Options.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The click-hit sound leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary>
    /// Front End Select. Sound id 1. Not dest and not cancel's Back.
    /// </summary>
    public static RetailFrontendAudioCue Cue => RetailFrontendAudioCue.Select;

    /// <summary>
    /// Sound leftover plays only on a click hit. A miss jumps to
    /// <see cref="MissJumpTarget"/> and skips the call. That is not dest.
    /// </summary>
    public static bool Applies(bool hit) => hit;
}
