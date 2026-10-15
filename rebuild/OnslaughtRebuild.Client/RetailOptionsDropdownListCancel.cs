// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> post-loop cancel leftover —
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
/// <see cref="RetailOptionsMenuItemIconDest"/>. This leftover is the
/// post-loop <c>call 0x0044DEA0</c> / <c>[0x0089BE28]</c> /
/// <c>mov [esi+0x20], edx</c> / <c>mov byte [esi+0x24], 0</c>. Do not
/// redo those. Do not invent dest Y as 5, 15.5, 268, 284, or 304. Do
/// not invent dest X as 322.5 or dest from 148.0. Do not invent dest
/// as the 2.0 constant.</para>
///
/// <para><b>Cancel.</b> Official 74154bfa independently re-read:
/// <c>0x004A4059</c> is <c>mov ecx, 0x675688</c>.
/// <c>0x004A405E</c> is <c>call 0x0044DEA0</c>.
/// <c>0x004A4063</c> is <c>test eax, eax</c>.
/// <c>0x004A4065</c> is <c>pop ebp</c>.
/// <c>0x004A4066</c> is <c>jne 0x004A40CF</c>.
/// <c>0x004A4068</c> is <c>mov eax, [0x0089BE28]</c>.
/// <c>0x004A406D</c> is <c>test eax, eax</c>.
/// <c>0x004A406F</c> is <c>je 0x004A40CF</c>.
/// <c>0x004A4071</c> is <c>mov edx, [esi+0x1c]</c>.
/// <c>0x004A4074</c> is <c>mov byte [esi+0x24], 0</c>.
/// <c>0x004A4078</c> is <c>mov [esi+0x20], edx</c>.
/// <c>0x004A407B</c> is <c>push 2</c>.
/// <c>0x004A407D</c> is <c>mov [0x0089BE28], 0</c>.
/// <c>0x004A4087</c> is <c>call 0x00468770</c>.
/// <c>0x0044DEA0</c> returns 1 only when <c>[ecx+0x1F8C]</c> and
/// <c>[ecx+0x1F98]</c> are both nonzero. Cancel writes currentIndex
/// from committedIndex and the expand byte when the helper returns 0
/// and the latch is set. Dest Y does not. Colour leftover already
/// consults currentIndex. Hover leftover already owns
/// <c>0x004A3FA6</c>. Click leftover already owns <c>0x004A4010</c>.
/// Nearby 15.5, 322.5, and 148.0 are measurements, not dest. Dest is
/// not the 2.0 constant. Click-hit sound at <c>0x004A403C</c>
/// <c>call 0x00468770(1)</c> is a later leftover.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// expanded list click, and the 0x00463669 compare stay untouched.
/// Back (<c>0x2E</c>) does not own this leftover.</para>
/// </summary>
public static class RetailOptionsDropdownListCancel
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>mov ecx, 0x675688</c>. Frontend this leftover, not dest.</summary>
    public const uint FrontEndLoadSite = 0x004A4059u;

    /// <summary>Frontend this used by <see cref="Helper"/>.</summary>
    public const uint FrontEndThis = 0x00675688u;

    /// <summary><c>call 0x0044DEA0</c>. Cancel helper leftover.</summary>
    public const uint HelperCallSite = 0x004A405Eu;

    /// <summary>
    /// Helper. Returns 1 when <see cref="HelperFieldA"/> and
    /// <see cref="HelperFieldB"/> are both nonzero.
    /// </summary>
    public const uint Helper = 0x0044DEA0u;

    /// <summary><c>[this+0x1F8C]</c>. Helper leftover, not dest.</summary>
    public const uint HelperFieldA = 0x1F8Cu;

    /// <summary><c>[this+0x1F98]</c>. Helper leftover, not dest.</summary>
    public const uint HelperFieldB = 0x1F98u;

    /// <summary><c>test eax, eax</c>. Cancel skips when the helper returns nonzero.</summary>
    public const uint HelperTestSite = 0x004A4063u;

    /// <summary><c>pop ebp</c> after the helper test. Not dest.</summary>
    public const uint EbpPopSite = 0x004A4065u;

    /// <summary><c>jne 0x004A40CF</c>. Helper nonzero keeps currentIndex and the expand byte.</summary>
    public const uint HelperSkipSite = 0x004A4066u;

    /// <summary>Fall-through target after a helper or latch skip.</summary>
    public const uint SkipTarget = 0x004A40CFu;

    /// <summary><c>mov eax, [0x0089BE28]</c>. Latch leftover, not dest.</summary>
    public const uint LatchLoadSite = 0x004A4068u;

    /// <summary>Latch at <c>0x0089BE28</c>. Already one of the three FMV mouse latches.</summary>
    public const uint Latch = 0x0089BE28u;

    /// <summary><c>test eax, eax</c>. Cancel writes only when the latch is set.</summary>
    public const uint LatchTestSite = 0x004A406Du;

    /// <summary><c>je 0x004A40CF</c>. Clear latch keeps currentIndex and the expand byte.</summary>
    public const uint LatchSkipSite = 0x004A406Fu;

    /// <summary><c>mov edx, [esi+0x1c]</c>. committedIndex leftover, not dest.</summary>
    public const uint CommittedLoadSite = 0x004A4071u;

    /// <summary>committedIndex at <c>[this+0x1c]</c>. Already click leftover.</summary>
    public const uint CommittedOffset = 0x1Cu;

    /// <summary><c>mov byte [esi+0x24], 0</c>. Cancel writes the expand byte.</summary>
    public const uint ExpandStoreSite = 0x004A4074u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>. Already dest leftover.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary><c>mov [esi+0x20], edx</c>. Cancel writes currentIndex from committedIndex.</summary>
    public const uint CurrentIndexStoreSite = 0x004A4078u;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Cancel leftover, not dest.</summary>
    public const uint CurrentIndexOffset = 0x20u;

    /// <summary><c>push 2</c>. Back sound leftover. Not dest.</summary>
    public const uint SoundPushSite = 0x004A407Bu;

    /// <summary><c>CFrontEnd__PlaySound(2)</c>. Front End Back. Not dest.</summary>
    public const uint SoundId = 2u;

    /// <summary><c>mov [0x0089BE28], 0</c>. Cancel clears the latch.</summary>
    public const uint LatchClearSite = 0x004A407Du;

    /// <summary><c>call 0x00468770</c>. Back sound leftover. Not dest.</summary>
    public const uint SoundCallSite = 0x004A4087u;

    /// <summary>PlaySound leftover. Click-hit sound at <c>0x004A403C</c> is later.</summary>
    public const uint PlaySound = 0x00468770u;

    /// <summary>Click hit at <c>0x004A4010</c> is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const uint ClickHitSite = 0x004A4010u;

    /// <summary>Hover hit at <c>0x004A3FA6</c> is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const uint HoverHitSite = 0x004A3FA6u;

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

    /// <summary>The 2.0 pad is not dest or cancel itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Cancel is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Cancel writes currentIndex from committedIndex. Dest Y does not.</summary>
    public const bool UsesCurrentIndex = true;

    /// <summary>0x004A4059 is this leftover.</summary>
    public const bool IsCancel = true;

    /// <summary>0x004A4010 is already <see cref="RetailOptionsDropdownListClick"/>.</summary>
    public const bool IsClickHit = false;

    /// <summary>0x004A3FA6 is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const bool IsHoverHit = false;

    /// <summary>Cancel writes committedIndex back. It does not call <c>SetLanguage</c>.</summary>
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

    /// <summary>The cancel leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary>
    /// <c>0x0044DEA0</c>: both <c>[ecx+0x1F8C]</c> and
    /// <c>[ecx+0x1F98]</c> nonzero. Cancel skips when this is true.
    /// </summary>
    public static bool HelperNonzero(int field1F8C, int field1F98) =>
        field1F8C != 0 && field1F98 != 0;

    /// <summary>
    /// Cancel writes only when the helper returns 0 and the latch is
    /// set. That is not dest.
    /// </summary>
    public static bool Applies(bool helperNonzero, bool latch) =>
        !helperNonzero && latch;

    /// <summary>
    /// Cancel writes currentIndex from committedIndex when it applies.
    /// A skip keeps the incoming currentIndex. That is not dest.
    /// </summary>
    public static int CurrentIndexAfterCancel(int currentIndex, int committedIndex, bool apply) =>
        apply ? committedIndex : currentIndex;

    /// <summary>
    /// Cancel writes the expand byte to 0 when it applies. A skip keeps
    /// the incoming expand byte. That is not dest.
    /// </summary>
    public static bool ExpandAfterCancel(bool expanded, bool apply) =>
        apply ? false : expanded;
}
