// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> collapsed value dest leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Label dest at <c>0x004A3D19</c> is already
/// <see cref="RetailOptionsDropdownDest"/> (full width).
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. This leftover is
/// the collapsed-arm <c>mov edx, [esp+0x28]</c> after vtable
/// +0x44. Do not redo those. Do not invent dest Y as 5, 268, 284,
/// or 304. Do not invent dest as the 2.0 constant.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A3D38</c> is <c>fadd [0x005D8BA0]</c>.
/// <c>0x004A3D3E</c> is <c>fstp [esp+0x18]</c>.
/// <c>0x004A3DA8</c> tests <c>[esi+0x24]</c>.
/// <c>0x004A3DAD</c> is <c>je 0x004A409B</c>.
/// <c>0x004A40B1</c> is <c>call [eax+0x44]</c>.
/// Independently read +0x44 helpers end <c>RET 4</c>, so the four
/// DrawText tail pushes stay on the stack and
/// <c>[esp+0x28]</c> aliases that earlier <c>[esp+0x18]</c>
/// store. Dest X is incoming dest X plus the 2.0 pad leftover.
/// Dest is not 2.0.</para>
///
/// <para><b>Not dest Y.</b> Dest Y is incoming
/// <c>[esp+0x10C]</c> still in ebx from <c>0x004A3D78</c>.
/// Nearby 5.0 at <c>0x005D85D8</c> is leftover min dest X for
/// the label, not this dest. Nearby 2.0 is the pad leftover, not
/// dest Y and not dest X itself. DrawLabelValueRow consumes
/// DestX. Dest Y keeps the row top and scale 1.0. The 2px
/// MeasureText residual is width, not this dest.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the
/// cursor, the colour AND, Apply, dropdown cosine, writing
/// chrome, language pitch, CMenuItem dest, dropdown label dest,
/// icon dest, and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownValueDest
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>fadd [0x005D8BA0]</c>. Pad leftover, not dest Y.</summary>
    public const uint PadAddSite = 0x004A3D38u;

    /// <summary><c>fstp [esp+0x18]</c>. Incoming dest X plus pad.</summary>
    public const uint PadStoreSite = 0x004A3D3Eu;

    /// <summary>2.0 source. Pad leftover, not dest X itself.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary><c>mov ebx, [esp+0x10C]</c>. Incoming dest Y.</summary>
    public const uint DestYLoadSite = 0x004A3D78u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary><c>mov al, [esi+0x24]</c>.</summary>
    public const uint ExpandTestSite = 0x004A3DA8u;

    /// <summary><c>je 0x004A409B</c>.</summary>
    public const uint CollapseJumpSite = 0x004A3DADu;

    /// <summary>Collapsed arm. Not the expanded list.</summary>
    public const uint CollapseTarget = 0x004A409Bu;

    /// <summary>vtable +0x44. State string. <c>RET 4</c>.</summary>
    public const uint GetStateSlot = 0x44u;

    /// <summary><c>call [eax+0x44]</c>.</summary>
    public const uint GetStateCallSite = 0x004A40B1u;

    /// <summary>Independently read +0x44 helpers end <c>RET 4</c>.</summary>
    public const uint GetStateRet = 4u;

    /// <summary><c>mov edx, [esp+0x28]</c>. Dest X leftover.</summary>
    public const uint DestLoadSite = 0x004A40B4u;

    /// <summary>IEEE bits of the identity scale pushes. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>call 0x004659A0</c> after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A40CAu;

    /// <summary>Collapsed value draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

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

    /// <summary>Dest is incoming plus pad. Do not invent a 5/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover scale pushes are identity 1.0, not a wrap.</summary>
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

    /// <summary>Collapsed dest does not half SIZE.cx.</summary>
    public const bool UsesIntegerHalf = false;

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float IdentityScale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="PadBits"/> decoded as IEEE-754 single. Not dest X.</summary>
    public static float Pad => BitConverter.UInt32BitsToSingle(PadBits);

    /// <summary>
    /// Incoming dest X plus the 2.0 pad leftover. Dest is not the
    /// pad constant.
    /// </summary>
    public static float DestX(float incomingX) => incomingX + Pad;
}
