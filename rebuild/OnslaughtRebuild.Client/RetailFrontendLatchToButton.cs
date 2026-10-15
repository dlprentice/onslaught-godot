// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>PlatformInput__PollMouseState</c> latch-to-button SET leftover —
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
/// <see cref="RetailOptionsDropdownListCancel"/>. Click-hit sound at
/// <c>0x004A403C</c> is already
/// <see cref="RetailOptionsDropdownListClickSound"/>. FMV skip OR at
/// <c>0x0053F2EB</c> already owns the three latch dwords. This leftover
/// is the <c>test ah, 0x80</c> / <c>mov [0x0089BE28], ecx</c> SET.
/// Do not redo those. Do not invent dest Y as 5, 15.5, 268, 284, or
/// 304. Do not invent dest X as 322.5 or dest from 148.0. Do not
/// invent dest as the 2.0 constant.</para>
///
/// <para><b>SET.</b> Official 74154bfa independently re-read:
/// <c>0x0042D4D0</c> is <c>PlatformInput__PollMouseState</c>.
/// <c>0x0042D4D6</c> is <c>xor ebx, ebx</c> (<c>33 db</c>).
/// <c>0x0042D58F</c> is <c>mov ecx, 1</c> (<c>b9 01 00 00 00</c>).
/// <c>0x0042D5CA</c> is <c>test ah, 0x80</c> (<c>f6 c4 80</c>).
/// <c>0x0042D5CD</c> is <c>je +0x0e</c> (target <c>0x0042D5DD</c>).
/// <c>0x0042D5CF</c> is the unique <c>mov [0x0089BE28], ecx</c>
/// (<c>89 0d 28 be 89 00</c>, one image hit). Cycle 85 retargets the
/// five published sites that sat mid-instruction. That is the
/// right-mouse latch SET. A miss jumps to <c>0x0042D5DD</c> and skips
/// the write of 1. Dest Y does not.
/// Colour leftover already consults currentIndex. Hover leftover
/// already owns <c>0x004A3FA6</c>. Click leftover already owns
/// <c>0x004A4010</c>. Cancel leftover already owns the load at
/// <c>0x004A4068</c> and the clear at <c>0x004A407D</c>. Click-hit
/// sound leftover already owns <c>0x004A403C</c>. Nearby 15.5, 322.5,
/// and 148.0 are measurements, not dest. Dest is not the 2.0 constant.
/// CFEPLevelSelect 148.0 fsub is later.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// expanded list click, expanded list cancel, click-hit sound, and
/// the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailFrontendLatchToButton
{
    /// <summary><c>PlatformInput__PollMouseState</c> body at <c>0x0042D4D0</c>.</summary>
    public const uint PollSite = 0x0042D4D0u;

    /// <summary><c>xor ebx, ebx</c>. Clear path leftover, not dest.</summary>
    public const uint EbxZeroSite = 0x0042D4D6u;

    /// <summary><c>mov ecx, 1</c> at <c>0x0042D58F</c>. SET value leftover. Not dest.</summary>
    public const uint OneLoadSite = 0x0042D58Fu;

    /// <summary>SET writes 1. Miss skips that write.</summary>
    public const uint SetValue = 1u;

    /// <summary><c>test ah, 0x80</c> at <c>0x0042D5CA</c>. Right-button leftover. Not dest.</summary>
    public const uint RightMaskSite = 0x0042D5CAu;

    /// <summary>AH bit 0x80. Right mouse. Not dest.</summary>
    public const byte RightButtonMask = 0x80;

    /// <summary><c>je +0x0e</c> at <c>0x0042D5CD</c>. Miss skips the SET leftover.</summary>
    public const uint RightSkipSite = 0x0042D5CDu;

    /// <summary>Miss target at <c>0x0042D5DD</c> skips the SET leftover.</summary>
    public const uint RightMissTarget = 0x0042D5DDu;

    /// <summary>Unique <c>mov [0x0089BE28], ecx</c> at <c>0x0042D5CF</c>. Latch-to-button SET leftover.</summary>
    public const uint RightSetSite = 0x0042D5CFu;

    /// <summary>Latch at <c>0x0089BE28</c>. Already cancel leftover's load/clear.</summary>
    public const uint Latch = 0x0089BE28u;

    /// <summary>Left latch at <c>0x0089BDF8</c>. Already one of the three FMV mouse latches.</summary>
    public const uint LeftLatch = 0x0089BDF8u;

    /// <summary>Middle latch at <c>0x0089BE10</c>. Already one of the three FMV mouse latches.</summary>
    public const uint MiddleLatch = 0x0089BE10u;

    /// <summary>FMV skip OR at <c>0x0053F2EB</c> is already owned. This leftover is the SET.</summary>
    public const uint FmvOrSite = 0x0053F2EBu;

    /// <summary>Cancel load at <c>0x004A4068</c> is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const uint CancelLoadSite = 0x004A4068u;

    /// <summary>Cancel clear at <c>0x004A407D</c> is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const uint CancelClearSite = 0x004A407Du;

    /// <summary>Click-hit sound at <c>0x004A403C</c> is already <see cref="RetailOptionsDropdownListClickSound"/>.</summary>
    public const uint ClickSoundSite = 0x004A403Cu;

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

    /// <summary>The 2.0 pad is not dest or the SET itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>SET is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>SET does not write currentIndex. Dest Y does not either.</summary>
    public const bool UsesCurrentIndex = false;

    /// <summary>0x0042D5CF is this leftover.</summary>
    public const bool IsLatchSet = true;

    /// <summary>0x0053F2EB is already FMV skip OR.</summary>
    public const bool IsFmvSkip = false;

    /// <summary>0x004A403C is already <see cref="RetailOptionsDropdownListClickSound"/>.</summary>
    public const bool IsClickSound = false;

    /// <summary>0x004A4010 is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const bool IsClickHit = false;

    /// <summary>0x004A3FA6 is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const bool IsHoverHit = false;

    /// <summary>0x004A4059 is already <see cref="RetailOptionsDropdownListCancel"/>.</summary>
    public const bool IsCancel = false;

    /// <summary>The leftover does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Poll leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
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

    /// <summary>0x004A403C is already <see cref="RetailOptionsDropdownListClickSound"/>.</summary>
    public const bool RedoesDropdownListClickSound = false;

    /// <summary>0x004A33FC is already <see cref="RetailOptionsMenuItemColor"/>.</summary>
    public const bool RedoesMenuItemColor = false;

    /// <summary>0x004A4310 / 0x004A3C69 is already <see cref="RetailOptionsApplyPulse"/>.</summary>
    public const bool RedoesApplyPulse = false;

    /// <summary>0x00463647 is already <c>RetailMainMenuLanguagePitch</c>.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in PollMouseState.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The latch-to-button SET leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary>
    /// Right-mouse SET. <c>test ah, 0x80</c> then write 1. A miss
    /// jumps to <see cref="RightMissTarget"/> and skips the write.
    /// That is not dest.
    /// </summary>
    public static bool Set(bool rightDown) => rightDown;
}
