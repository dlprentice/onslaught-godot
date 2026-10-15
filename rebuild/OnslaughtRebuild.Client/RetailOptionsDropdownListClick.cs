// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded list click leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para><b>Sibling.</b> Expanded list hover at <c>0x004A3FA6</c> is
/// already <see cref="RetailOptionsDropdownListHover"/>. Expanded list
/// colour at <c>0x004A3F6C</c> is already
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
/// expanded-arm <c>call 0x00469400</c> / <c>mov [esi+0x20], edi</c> /
/// <c>mov byte [esi+0x24], 0</c>. Do not redo those. Do not invent dest
/// Y as 5, 15.5, 268, 284, or 304. Do not invent dest X as 322.5 or dest
/// from 148.0. Do not invent dest as the 2.0 constant.</para>
///
/// <para><b>Click.</b> Official 74154bfa independently re-read:
/// <c>0x004A3FE6</c> is <c>fild [esp+0x24]</c> leftover SIZE.cy.
/// <c>0x004A3FEA</c> is <c>fadd [esp+0x10]</c> dest Y leftover.
/// <c>0x004A3FEE</c> is <c>fstp [esp+0x30]</c>. <c>0x004A3FF2</c> is
/// <c>fild [esp+0x20]</c> leftover label SIZE.cx. <c>0x004A3FF6</c>
/// is <c>mov ecx, [esp+0x30]</c>. <c>0x004A3FFA</c> is
/// <c>push ecx</c>. <c>0x004A3FFB</c> is <c>fadd [esp+0x20]</c>
/// collapsed dest leftover. <c>0x004A3FFF</c> is
/// <c>fadd [0x005D8BA0]</c> pad leftover. <c>0x004A4005</c> is
/// <c>fstp [esp+0x40]</c>. <c>0x004A400D</c> is <c>push edx</c>.
/// <c>0x004A400E</c> is <c>push ebp</c>. <c>0x004A400F</c> is
/// <c>push ebx</c>. <c>0x004A4010</c> is <c>call 0x00469400</c>.
/// <c>0x004A4015</c> is <c>add esp, 0x10</c>. <c>0x004A4018</c>
/// is <c>test al, al</c>. <c>0x004A401A</c> is
/// <c>je 0x004A4044</c>. <c>0x004A401C</c> is
/// <c>mov al, [esi+0x25]</c>. <c>0x004A401F</c> is
/// <c>mov [esi+0x20], edi</c>. <c>0x004A4022</c> is
/// <c>test al, al</c>. <c>0x004A4024</c> is
/// <c>mov byte [esi+0x24], 0</c>. <c>0x004A4028</c> is
/// <c>jne 0x004A403A</c>. <c>0x004A402A</c> is
/// <c>cmp [esi+0x1c], edi</c>. <c>0x004A4034</c> is
/// <c>mov [esi+0x1c], edi</c>. <c>0x004A4037</c> is
/// <c>call [eax+0x38]</c>. Click writes currentIndex and the expand
/// byte. Dest Y does not. Colour leftover already consults
/// currentIndex. Hover leftover already owns <c>0x004A3FA6</c>.
/// Nearby 15.5, 322.5, and 148.0 are measurements, not dest. Dest is
/// not the 2.0 constant.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListClick
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>fild [esp+0x24]</c>. Leftover SIZE.cy, not dest.</summary>
    public const uint CyFildSite = 0x004A3FE6u;

    /// <summary><c>fadd [esp+0x10]</c>. Dest Y leftover, not dest itself.</summary>
    public const uint DestYAddSite = 0x004A3FEAu;

    /// <summary><c>fstp [esp+0x30]</c>. Bottom leftover, not dest Y.</summary>
    public const uint BottomStoreSite = 0x004A3FEEu;

    /// <summary><c>fild [esp+0x20]</c>. Leftover label SIZE.cx, not dest.</summary>
    public const uint LabelCxFildSite = 0x004A3FF2u;

    /// <summary><c>mov ecx, [esp+0x30]</c>. Bottom into the hit helper.</summary>
    public const uint BottomLoadSite = 0x004A3FF6u;

    /// <summary><c>push ecx</c>. Bottom into the hit helper.</summary>
    public const uint BottomPushSite = 0x004A3FFAu;

    /// <summary><c>fadd [esp+0x20]</c>. Collapsed dest leftover, not dest.</summary>
    public const uint CollapsedAddSite = 0x004A3FFBu;

    /// <summary><c>fadd [0x005D8BA0]</c>. Pad leftover, not dest.</summary>
    public const uint PadAddSite = 0x004A3FFFu;

    /// <summary>2.0 source. Pad leftover, not dest X itself.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary><c>fstp [esp+0x40]</c>. Right leftover, not dest X.</summary>
    public const uint RightStoreSite = 0x004A4005u;

    /// <summary><c>mov edx, [esp+0x40]</c>. Right into the hit helper.</summary>
    public const uint RightLoadSite = 0x004A4009u;

    /// <summary><c>push edx</c>. Right into the hit helper.</summary>
    public const uint RightPushSite = 0x004A400Du;

    /// <summary><c>push ebp</c>. Dest Y leftover into the hit helper.</summary>
    public const uint TopPushSite = 0x004A400Eu;

    /// <summary><c>push ebx</c>. Dest X leftover into the hit helper.</summary>
    public const uint LeftPushSite = 0x004A400Fu;

    /// <summary><c>call 0x00469400</c>. Click hit leftover.</summary>
    public const uint ClickHitSite = 0x004A4010u;

    /// <summary>Click helper. Forwards the leftover LTRB into <see cref="PointInRect"/>.</summary>
    public const uint ClickHit = 0x00469400u;

    /// <summary><c>add esp, 0x10</c> after the cdecl hit helper.</summary>
    public const uint ClickHitPop = 0x10u;

    /// <summary><c>test al, al</c>. Click writes only when the helper returns true.</summary>
    public const uint HitTestSite = 0x004A4018u;

    /// <summary><c>je 0x004A4044</c>. Miss keeps currentIndex and the expand byte.</summary>
    public const uint MissJumpSite = 0x004A401Au;

    /// <summary>Fall-through target after a miss.</summary>
    public const uint MissJumpTarget = 0x004A4044u;

    /// <summary><c>mov al, [esi+0x25]</c>. Pending byte leftover, not dest.</summary>
    public const uint PendingLoadSite = 0x004A401Cu;

    /// <summary>Pending byte at <c>[this+0x25]</c>. Click leftover, not dest.</summary>
    public const uint PendingByteOffset = 0x25u;

    /// <summary><c>mov [esi+0x20], edi</c>. Click writes currentIndex.</summary>
    public const uint CurrentIndexStoreSite = 0x004A401Fu;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Click leftover, not dest.</summary>
    public const uint CurrentIndexOffset = 0x20u;

    /// <summary><c>test al, al</c> after the pending-byte load.</summary>
    public const uint PendingTestSite = 0x004A4022u;

    /// <summary><c>mov byte [esi+0x24], 0</c>. Click writes the expand byte.</summary>
    public const uint ExpandStoreSite = 0x004A4024u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>. Already dest leftover.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary><c>jne 0x004A403A</c>. Pending skips the live apply.</summary>
    public const uint PendingSkipSite = 0x004A4028u;

    /// <summary>Fall-through target after a pending skip.</summary>
    public const uint PendingSkipTarget = 0x004A403Au;

    /// <summary><c>cmp [esi+0x1c], edi</c>. committedIndex leftover, not dest.</summary>
    public const uint CommittedCompareSite = 0x004A402Au;

    /// <summary>committedIndex at <c>[this+0x1c]</c>. Click leftover, not dest.</summary>
    public const uint CommittedOffset = 0x1Cu;

    /// <summary><c>mov [esi+0x1c], edi</c>. Live apply leftover, not dest.</summary>
    public const uint CommittedStoreSite = 0x004A4034u;

    /// <summary>vtable +0x38. SET leftover. Not dest.</summary>
    public const uint SetSlot = 0x38u;

    /// <summary><c>call [eax+0x38]</c>. Live apply leftover, not dest.</summary>
    public const uint SetCallSite = 0x004A4037u;

    /// <summary>Point-in-rect leftover used by <see cref="ClickHit"/>.</summary>
    public const uint PointInRect = 0x00523CC0u;

    /// <summary>Hover hit at <c>0x004A3FA6</c> is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const uint HoverHitSite = 0x004A3FA6u;

    /// <summary>Hover helper. Not this leftover.</summary>
    public const uint HoverHit = 0x004693D0u;

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

    /// <summary>The 2.0 pad is not dest or click itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Click is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Click writes currentIndex. Dest Y does not.</summary>
    public const bool UsesCurrentIndex = true;

    /// <summary>Click writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3FA6 is already <see cref="RetailOptionsDropdownListHover"/>.</summary>
    public const bool IsHoverHit = false;

    /// <summary>0x004A4010 is this leftover.</summary>
    public const bool IsClickHit = true;

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

    /// <summary>The click leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary>Left is the already-pinned list dest leftover.</summary>
    public static float Left(float incomingX) =>
        RetailOptionsDropdownListDest.DestX(incomingX);

    /// <summary>Top is the already-pinned list dest Y leftover.</summary>
    public static float Top(float incomingY, int count, int pitch, int index) =>
        RetailOptionsDropdownListDestY.DestY(incomingY, count, pitch, index);

    /// <summary>
    /// Right is dest leftover X plus leftover label SIZE.cx. Width is
    /// not dest and not the panel leftover.
    /// </summary>
    public static float Right(float incomingX, int labelCx) =>
        Left(incomingX) + labelCx;

    /// <summary>Bottom is dest leftover Y plus leftover SIZE.cy.</summary>
    public static float Bottom(float incomingY, int count, int pitch, int index) =>
        Top(incomingY, count, pitch, index) + pitch;

    /// <summary>
    /// <c>0x00523CC0</c> on the click rect: left &lt;= x &lt; right and
    /// top &lt;= y &lt; bottom. Dest leftovers are inputs, not this leftover.
    /// </summary>
    public static bool Contains(
        float x,
        float y,
        float destX,
        float destY,
        int labelCx,
        int pitch) =>
        x >= destX &&
        x < destX + labelCx &&
        y >= destY &&
        y < destY + pitch;

    /// <summary>
    /// Click writes currentIndex when the hit leftover returns true.
    /// A miss keeps the incoming currentIndex. That is not dest.
    /// </summary>
    public static int CurrentIndexAfterClick(int currentIndex, int index, bool hit) =>
        hit ? index : currentIndex;

    /// <summary>
    /// Click writes the expand byte to 0 when the hit leftover returns
    /// true. A miss keeps the incoming expand byte. That is not dest.
    /// </summary>
    public static bool ExpandAfterClick(bool expanded, bool hit) =>
        hit ? false : expanded;

    /// <summary>
    /// Live apply leftover: pending byte clear and committedIndex not
    /// equal to the clicked index. That is not dest.
    /// </summary>
    public static bool AppliesLive(byte pendingByte, int committedIndex, int index) =>
        pendingByte == 0 && committedIndex != index;
}
