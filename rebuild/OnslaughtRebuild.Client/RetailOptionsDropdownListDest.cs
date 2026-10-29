// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded list dest leftover —
/// recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Collapsed value dest at <c>0x004A40B4</c> is
/// already <see cref="RetailOptionsDropdownValueDest"/>. Label dest at
/// <c>0x004A3D19</c> is already <see cref="RetailOptionsDropdownDest"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. This leftover is
/// the expanded-arm (a load of stack local +0x4C into ebx) after vtable
/// +0x44. Do not redo those. Do not invent dest Y as 5, 268, 284,
/// or 304. Do not invent dest as the 2.0 constant.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A3D38</c> is a float add of the value at 0x005D8BA0.
/// <c>0x004A3D3E</c> is a float store to stack local +0x18.
/// <c>0x004A3DA8</c> tests <c>[esi+0x24]</c>.
/// <c>0x004A3DAD</c> is a conditional branch to 0x004A409B (taken when equal).
/// After a push of ebp, that <c>[esp+0x18]</c> aliases
/// <c>[esp+0x1C]</c>. <c>0x004A3F55</c> is a float load of stack local +0x1C.
/// <c>0x004A3F59</c> is a float add of the value at 0x005D8BA0.
/// <c>0x004A3F5F</c> is a float store to stack local +0x34.
/// <c>0x004A3FC8</c> is an indirect call through vtable slot 17.
/// Independently read +0x44 helpers end (a return that pops 4 bytes of arguments), so the four
/// leftover pushes stay and <c>[esp+0x4C]</c> aliases that
/// <c>[esp+0x34]</c> store. Dest X is the collapsed dest leftover
/// plus the 2.0 pad leftover. Dest is not 2.0.</para>
///
/// <para><b>Not dest Y.</b> Dest Y is the entry top leftover in
/// ebp. Nearby 5.0 at <c>0x005D85D8</c> is leftover min dest X for
/// the label, not this dest. Nearby 2.0 is the pad leftover, not
/// dest Y and not dest X itself. DrawOptionDropdown consumes
/// DestX. Dest Y keeps the entry top and scale 1.0. The 2px
/// MeasureText residual is width, not this dest.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the
/// cursor, the colour AND, Apply, dropdown cosine, writing
/// chrome, language pitch, CMenuItem dest, dropdown label dest,
/// icon dest, collapsed value dest, and the 0x00463669 compare
/// stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownListDest
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary>A float load of stack local +0x1C. Collapsed dest leftover after a push of ebp.</summary>
    public const uint CollapsedLeftoverLoadSite = 0x004A3F55u;

    /// <summary>A float add of the value at 0x005D8BA0. Pad leftover, not dest Y.</summary>
    public const uint PadAddSite = 0x004A3F59u;

    /// <summary>A float store to stack local +0x34. Collapsed dest leftover plus pad.</summary>
    public const uint PadStoreSite = 0x004A3F5Fu;

    /// <summary>2.0 source. Pad leftover, not dest X itself.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary>A load of field +0x24 of esi into al.</summary>
    public const uint ExpandTestSite = 0x004A3DA8u;

    /// <summary>A conditional branch to 0x004A409B (taken when equal).</summary>
    public const uint CollapseJumpSite = 0x004A3DADu;

    /// <summary>Collapsed arm. Not this leftover.</summary>
    public const uint CollapseTarget = 0x004A409Bu;

    /// <summary>vtable +0x44. State string. A return that pops 4 bytes of arguments.</summary>
    public const uint GetStateSlot = 0x44u;

    /// <summary>An indirect call through vtable slot 17.</summary>
    public const uint GetStateCallSite = 0x004A3FC8u;

    /// <summary>Independently read +0x44 helpers end (a return that pops 4 bytes of arguments).</summary>
    public const uint GetStateRet = 4u;

    /// <summary>A load of stack local +0x4C into ebx. Dest X leftover.</summary>
    public const uint DestLoadSite = 0x004A3FCDu;

    /// <summary>IEEE bits of the identity scale leftover. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary>A direct call to 0x00515A70 before DrawText.</summary>
    public const uint FontCallSite = 0x004A3FDAu;

    /// <summary>Font helper. Independently read (a return that pops 4 bytes of arguments) at <c>0x00515A83</c>.</summary>
    public const uint Font = 0x00515A70u;

    /// <summary>Independently read font helper ends a return that pops 4 bytes of arguments.</summary>
    public const uint FontRet = 4u;

    /// <summary>A direct call to 0x004659A0 after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A3FE1u;

    /// <summary>Expanded list draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary>Independently read DrawText ends a return that pops 32 bytes of arguments at <c>0x00465A19</c>.</summary>
    public const uint DrawTextRet = 32u;

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

    /// <summary>The 2.0 pad is not dest X itself.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest is collapsed leftover plus pad. Do not invent a 5/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover scale is identity 1.0, not a wrap.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a value fade.</summary>
    public const bool InventsFade = false;

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

    /// <summary>Expanded dest does not half SIZE.cx.</summary>
    public const bool UsesIntegerHalf = false;

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float IdentityScale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="PadBits"/> decoded as IEEE-754 single. Not dest X.</summary>
    public static float Pad => BitConverter.UInt32BitsToSingle(PadBits);

    /// <summary>
    /// Collapsed dest leftover plus the 2.0 pad leftover. Dest is
    /// not the pad constant.
    /// </summary>
    public static float DestX(float incomingX) =>
        RetailOptionsDropdownValueDest.DestX(incomingX) + Pad;
}
