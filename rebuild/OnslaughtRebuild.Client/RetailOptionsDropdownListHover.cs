// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded list hover leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para><b>Sibling.</b> Expanded list colour at <c>0x004A3F6C</c> is
/// already <see cref="RetailOptionsDropdownListColor"/>. Expanded list
/// dest Y at <c>0x004A3F3C</c> is already
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
/// expanded-arm <c>call 0x004693D0</c> / <c>mov [esi+0x20], edi</c>.
/// Do not redo those. Do not invent dest Y as 5, 15.5, 268, 284, or
/// 304. Do not invent dest X as 322.5 or dest from 148.0. Do not
/// invent dest as the 2.0 constant.</para>
///
/// <para><b>Hover.</b> Official 74154bfa independently re-read:
/// <c>0x004A3F55</c> is <c>fld [esp+0x1C]</c> collapsed dest leftover.
/// <c>0x004A3F59</c> is <c>fadd [0x005D8BA0]</c> pad leftover.
/// <c>0x004A3F5F</c> is <c>fstp [esp+0x34]</c>. <c>0x004A3F65</c> is
/// <c>mov ebp, [esp+0x10]</c> dest Y leftover. <c>0x004A3F78</c> is
/// <c>fild [esp+0x20]</c> leftover label SIZE.cx. <c>0x004A3F7C</c>
/// is <c>mov ecx, [esp+0x34]</c>. <c>0x004A3F80</c> is
/// <c>fadd [esp+0x1C]</c>. <c>0x004A3F84</c> is
/// <c>fadd [0x005D8BA0]</c>. <c>0x004A3F8A</c> is
/// <c>fstp [esp+0x30]</c>. <c>0x004A3F8E</c> is
/// <c>fld [esp+0x38]</c> pitch leftover. <c>0x004A3F92</c> is
/// <c>fadd [esp+0x10]</c>. <c>0x004A3F9A</c> is
/// <c>fstp [esp+0x14]</c>. <c>0x004A3FA2</c> is <c>push edx</c>.
/// <c>0x004A3FA3</c> is <c>push eax</c>. <c>0x004A3FA4</c> is
/// <c>push ebp</c>. <c>0x004A3FA5</c> is <c>push ecx</c>.
/// <c>0x004A3FA6</c> is <c>call 0x004693D0</c>. <c>0x004A3FAB</c>
/// is <c>add esp, 0x10</c>. <c>0x004A3FAE</c> is
/// <c>test al, al</c>. <c>0x004A3FB0</c> is
/// <c>je 0x004A3FB5</c>. <c>0x004A3FB2</c> is
/// <c>mov [esi+0x20], edi</c>. Hover writes currentIndex. Dest Y
/// does not. Colour leftover already consults currentIndex. Nearby
/// 15.5, 322.5, and 148.0 are measurements, not dest. Click at
/// <c>0x004A4010</c> is a later leftover.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, and the 0x00463669
/// compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListHover
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>fld [esp+0x1C]</c>. Collapsed dest leftover, not dest itself.</summary>
    public const uint CollapsedLeftoverLoadSite = 0x004A3F55u;

    /// <summary><c>fadd [0x005D8BA0]</c>. Pad leftover, not dest.</summary>
    public const uint PadAddSite = 0x004A3F59u;

    /// <summary>2.0 source. Pad leftover, not dest X itself.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary><c>fstp [esp+0x34]</c>. Left leftover. Already dest X.</summary>
    public const uint LeftStoreSite = 0x004A3F5Fu;

    /// <summary><c>mov ebp, [esp+0x10]</c>. Top leftover. Already dest Y.</summary>
    public const uint TopLoadSite = 0x004A3F65u;

    /// <summary><c>fild [esp+0x20]</c>. Leftover label SIZE.cx, not dest.</summary>
    public const uint LabelCxFildSite = 0x004A3F78u;

    /// <summary><c>mov ecx, [esp+0x34]</c>. Left into the hit helper.</summary>
    public const uint LeftLoadSite = 0x004A3F7Cu;

    /// <summary><c>fstp [esp+0x30]</c>. Right leftover, not dest X.</summary>
    public const uint RightStoreSite = 0x004A3F8Au;

    /// <summary><c>fstp [esp+0x14]</c>. Bottom leftover, not dest Y.</summary>
    public const uint BottomStoreSite = 0x004A3F9Au;

    /// <summary><c>push edx</c>. Bottom into the hit helper.</summary>
    public const uint BottomPushSite = 0x004A3FA2u;

    /// <summary><c>push eax</c>. Right into the hit helper.</summary>
    public const uint RightPushSite = 0x004A3FA3u;

    /// <summary><c>push ebp</c>. Top into the hit helper.</summary>
    public const uint TopPushSite = 0x004A3FA4u;

    /// <summary><c>push ecx</c>. Left into the hit helper.</summary>
    public const uint LeftPushSite = 0x004A3FA5u;

    /// <summary><c>call 0x004693D0</c>. Hover hit leftover.</summary>
    public const uint HoverHitSite = 0x004A3FA6u;

    /// <summary>Hover helper. Already named on the colour leftover.</summary>
    public const uint HoverHit = 0x004693D0u;

    /// <summary><c>add esp, 0x10</c> after the cdecl hit helper.</summary>
    public const uint HoverHitPop = 0x10u;

    /// <summary><c>test al, al</c>. Hover writes only when the helper returns true.</summary>
    public const uint HitTestSite = 0x004A3FAEu;

    /// <summary><c>je 0x004A3FB5</c>. Miss keeps currentIndex.</summary>
    public const uint MissJumpSite = 0x004A3FB0u;

    /// <summary>Fall-through target after a miss.</summary>
    public const uint MissJumpTarget = 0x004A3FB5u;

    /// <summary><c>mov [esi+0x20], edi</c>. Hover writes currentIndex.</summary>
    public const uint CurrentIndexStoreSite = 0x004A3FB2u;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Hover leftover, not dest.</summary>
    public const uint CurrentIndexOffset = 0x20u;

    /// <summary>Point-in-rect leftover used by <see cref="HoverHit"/>.</summary>
    public const uint PointInRect = 0x00523B50u;

    /// <summary>Click hit at <c>0x004A4010</c> is a later leftover.</summary>
    public const uint ClickHitSite = 0x004A4010u;

    /// <summary>Click helper. Not this leftover.</summary>
    public const uint ClickHit = 0x00469400u;

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

    /// <summary>The 2.0 pad is not dest or hover itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Hover is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Hover writes currentIndex. Dest Y does not.</summary>
    public const bool UsesCurrentIndex = true;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3FA6 is this leftover.</summary>
    public const bool IsHoverHit = true;

    /// <summary>0x004A4010 is a later leftover.</summary>
    public const bool IsClickHit = false;

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

    /// <summary>The hover leftover is not a MeasureText change.</summary>
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

    /// <summary>Bottom is dest leftover Y plus pitch leftover.</summary>
    public static float Bottom(float incomingY, int count, int pitch, int index) =>
        Top(incomingY, count, pitch, index) + pitch;

    /// <summary>
    /// <c>0x00523B50</c> on the hover rect: left &lt;= x &lt; right and
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
    /// Hover writes currentIndex when the hit leftover returns true.
    /// A miss keeps the incoming currentIndex. That is not dest.
    /// </summary>
    public static int CurrentIndexAfterHover(int currentIndex, int index, bool hit) =>
        hit ? index : currentIndex;
}
