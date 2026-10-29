// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded list colour leftover —
/// recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// a private twin copy is
/// the same size and hash.
///
/// <para><b>Sibling.</b> Expanded list dest X at <c>0x004A3FCD</c> is
/// already <see cref="RetailOptionsDropdownListDest"/>. Expanded list
/// dest Y at <c>0x004A3F3C</c> is already
/// <see cref="RetailOptionsDropdownListDestY"/>. Expanded panel dest
/// at <c>0x004A3F36</c> is already
/// <see cref="RetailOptionsDropdownPanelDest"/>. Collapsed value dest
/// at <c>0x004A40B4</c> is already
/// <see cref="RetailOptionsDropdownValueDest"/>. Label dest at
/// <c>0x004A3D19</c> is already <see cref="RetailOptionsDropdownDest"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. Row colour AND at
/// <c>0x004A33FC</c> is already <see cref="RetailOptionsMenuItemColor"/>.
/// This leftover is the expanded-arm (a load of constant 0xFF404040 into ebx,
/// then or of -1 into ebx). Do not redo those. Do not invent dest Y as 5,
/// 15.5, 268, 284, or 304. Do not invent dest X as 322.5 or dest from
/// 148.0. Do not invent dest as the 2.0 constant.</para>
///
/// <para><b>Colour.</b> Official 74154bfa independently re-read:
/// <c>0x004A3F43</c> is a zeroing of edi. <c>0x004A3F65</c> is
/// a load of stack local +0x10 into ebp dest Y leftover.
/// <c>0x004A3F69</c> is a load of field +0x20 of esi into eax currentIndex.
/// <c>0x004A3F6C</c> is a load of constant 0xFF404040 into ebx.
/// <c>0x004A3F71</c> is a compare of edi with eax. <c>0x004A3F73</c> is
/// a conditional branch to 0x004A3F78 (taken when not equal). <c>0x004A3F75</c>
/// is an or of -1 into ebx. <c>0x004A3FCC</c> is a push of ebx into the
/// DrawText path. <c>0x004A3FCD</c> then reuses ebx for dest X.
/// Idle is <c>0xFF404040</c>. When the loop index equals
/// currentIndex, ebx becomes <c>0xFFFFFFFF</c>. Colour consults
/// currentIndex. Dest Y does not. Nearby 15.5, 322.5, and 148.0 are
/// measurements, not dest. Hover at <c>0x004A3FA6</c> and click at
/// <c>0x004A4010</c> are later leftovers.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, the cursor, the row colour AND,
/// Apply, dropdown cosine, writing chrome, language pitch,
/// CMenuItem dest, dropdown label dest, icon dest, collapsed
/// value dest, expanded list dest X, expanded panel dest, expanded
/// list dest Y, and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListColor
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary>A zeroing of edi. Loop index leftover, not dest.</summary>
    public const uint LoopIndexZeroSite = 0x004A3F43u;

    /// <summary>A load of field +0x20 of esi into eax. currentIndex. Colour leftover, not dest.</summary>
    public const uint CurrentIndexLoadSite = 0x004A3F69u;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Colour leftover, not dest.</summary>
    public const uint CurrentIndexOffset = 0x20u;

    /// <summary>A load of constant 0xFF404040 into ebx.</summary>
    public const uint IdleColorSite = 0x004A3F6Cu;

    /// <summary>Idle packed colour leftover.</summary>
    public const uint IdlePackedColor = 0xFF404040u;

    /// <summary>A compare of edi with eax. Loop index against currentIndex.</summary>
    public const uint CompareSite = 0x004A3F71u;

    /// <summary>A conditional branch to 0x004A3F78 (taken when not equal). Skip the selected or.</summary>
    public const uint SelectedSkipSite = 0x004A3F73u;

    /// <summary>Fall-through target after the selected or.</summary>
    public const uint SelectedSkipTarget = 0x004A3F78u;

    /// <summary>An or of -1 into ebx.</summary>
    public const uint SelectedOrSite = 0x004A3F75u;

    /// <summary>Sign-extended (an or of -1 into ebx).</summary>
    public const uint SelectedOrImmediate = 0xFFFFFFFFu;

    /// <summary>A push of ebx into the DrawText path. Colour, not dest.</summary>
    public const uint ColorPushSite = 0x004A3FCCu;

    /// <summary>A direct call to 0x004659A0 after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A3FE1u;

    /// <summary>Expanded list draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary>Hover hit at <c>0x004A3FA6</c> is a later leftover.</summary>
    public const uint HoverHitSite = 0x004A3FA6u;

    /// <summary>Hover helper. Not this leftover.</summary>
    public const uint HoverHit = 0x004693D0u;

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

    /// <summary>The 2.0 pad is not dest or colour.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Colour is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Colour consults currentIndex. Dest Y does not.</summary>
    public const bool UsesCurrentIndex = true;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3FA6 is a later leftover.</summary>
    public const bool IsHoverHit = false;

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

    /// <summary>The colour leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary>
    /// Idle packed colour, or idle or -1 when the loop index equals
    /// currentIndex. Colour, not dest.
    /// </summary>
    public static uint PackedColor(int index, int currentIndex) =>
        index == currentIndex
            ? IdlePackedColor | SelectedOrImmediate
            : IdlePackedColor;
}
