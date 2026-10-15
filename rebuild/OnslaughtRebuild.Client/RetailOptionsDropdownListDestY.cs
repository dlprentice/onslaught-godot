// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded list dest Y leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Expanded list dest X at <c>0x004A3FCD</c> is
/// already <see cref="RetailOptionsDropdownListDest"/>. Expanded panel
/// dest at <c>0x004A3F36</c> / <c>0x004A3F35</c> is already
/// <see cref="RetailOptionsDropdownPanelDest"/>. Collapsed value dest
/// at <c>0x004A40B4</c> is already
/// <see cref="RetailOptionsDropdownValueDest"/>. Label dest at
/// <c>0x004A3D19</c> is already <see cref="RetailOptionsDropdownDest"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. This leftover is the
/// expanded-arm dest Y after <c>fild [esp+0x60]</c>. Do not redo
/// those. Do not invent dest Y as 5, 15.5, 268, 284, or 304. Do not
/// invent dest X as 322.5 or dest from 148.0. Do not invent dest as
/// the 2.0 constant.</para>
///
/// <para><b>Dest Y.</b> Official 74154bfa independently re-read:
/// <c>0x004A3DB5</c> is <c>push ebp</c>. After that push, label
/// SIZE.cy leftovers sit at <c>[esp+0x24]</c>.
/// <c>0x004A3E45</c> is <c>fstp [esp+0x10]</c> panel dest Y.
/// <c>0x004A3F00</c> is <c>mov ebp, [esp+0x24]</c>, which aliases
/// that dest Y after the DrawSpriteEx pack start.
/// <c>0x004A3F3C</c> is <c>fild [esp+0x60]</c>, which aliases
/// SIZE.cy after the 0x3C pack. <c>0x004A3F47</c> is
/// <c>fmul [esp+0x18]</c> scale leftover (identity 1.0 unless the
/// panel height leftover shrank it). <c>0x004A3F8E</c> is
/// <c>fld [esp+0x38]</c>. <c>0x004A3F92</c> is
/// <c>fadd [esp+0x10]</c>. <c>0x004A3FD1</c> is <c>push ebp</c>.
/// Dest Y is the panel dest leftover plus index times (SIZE.cy times
/// scale). Dest Y does not consult currentIndex at
/// <c>[esi+0x20]</c>. That slot is colour leftover, not dest.
/// Nearby 15.5, 322.5, and 148.0 are measurements, not dest.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, the cursor, the colour AND,
/// Apply, dropdown cosine, writing chrome, language pitch,
/// CMenuItem dest, dropdown label dest, icon dest, collapsed
/// value dest, expanded list dest X, expanded panel dest, and the
/// 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListDestY
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>fild [esp+0x60]</c>. SIZE.cy leftover, not dest.</summary>
    public const uint CyFildSite = 0x004A3F3Cu;

    /// <summary><c>add esp, 0x3C</c> after DrawSpriteEx.</summary>
    public const uint DrawSpritePopSite = 0x004A3F40u;

    /// <summary>Bytes popped after DrawSpriteEx. Aliases the fild slot to <c>[esp+0x24]</c>.</summary>
    public const uint DrawSpritePop = 0x3Cu;

    /// <summary><c>fmul [esp+0x18]</c>. Scale leftover, not dest Y.</summary>
    public const uint ScaleMulSite = 0x004A3F47u;

    /// <summary><c>fstp [esp+0x38]</c>. SIZE.cy times scale.</summary>
    public const uint PitchStoreSite = 0x004A3F4Bu;

    /// <summary><c>mov ebp, [esp+0x10]</c>. Later-entry dest Y leftover.</summary>
    public const uint LoopDestYLoadSite = 0x004A3F65u;

    /// <summary><c>fld [esp+0x38]</c>. Pitch leftover, not dest X.</summary>
    public const uint PitchLoadSite = 0x004A3F8Eu;

    /// <summary><c>fadd [esp+0x10]</c>. Current dest Y plus pitch.</summary>
    public const uint DestYAddSite = 0x004A3F92u;

    /// <summary><c>fstp [esp+0x14]</c>. Next dest Y leftover.</summary>
    public const uint DestYStoreSite = 0x004A3F9Au;

    /// <summary><c>push ebp</c>. Dest Y into DrawText.</summary>
    public const uint DestYPushSite = 0x004A3FD1u;

    /// <summary><c>call 0x004659A0</c> after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A3FE1u;

    /// <summary>Expanded list draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary><c>mov [esp+0x10], ecx</c>. Advance dest Y leftover.</summary>
    public const uint AdvanceStoreSite = 0x004A404Du;

    /// <summary>IEEE bits of the identity scale leftover. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary>currentIndex at <c>[this+0x20]</c>. Colour leftover, not dest.</summary>
    public const uint CurrentIndexOffset = 0x20u;

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

    /// <summary>The 2.0 pad is not dest Y.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is not the 148.0 level-select leftover.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>Dest is panel dest Y plus index times cy. Do not invent a 5/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Dest Y does not consult currentIndex.</summary>
    public const bool UsesCurrentIndex = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
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

    /// <summary>The dest leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float IdentityScale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary>
    /// Scale leftover at <c>[esp+0x18]</c>. Identity 1.0 unless
    /// count*cy exceeds 480.0. Not dest Y.
    /// </summary>
    public static float Scale(int count, int pitch)
    {
        int height = count * pitch;
        if (height > RetailOptionsDropdownPanelDest.ClampMax)
        {
            return RetailOptionsDropdownPanelDest.ClampMax / height;
        }

        return IdentityScale;
    }

    /// <summary>
    /// Panel dest leftover plus index times (SIZE.cy times scale).
    /// Dest Y does not consult currentIndex. Dest is not 15.5.
    /// </summary>
    public static float DestY(float incomingY, int count, int pitch, int index) =>
        RetailOptionsDropdownPanelDest.DestY(incomingY, count, pitch) +
        (index * pitch * Scale(count, pitch));
}
