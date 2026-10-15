// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> expanded panel dest leftover —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Expanded list dest at <c>0x004A3FCD</c> is
/// already <see cref="RetailOptionsDropdownListDest"/>. Collapsed
/// value dest at <c>0x004A40B4</c> is already
/// <see cref="RetailOptionsDropdownValueDest"/>. Label dest at
/// <c>0x004A3D19</c> is already <see cref="RetailOptionsDropdownDest"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/>. Icon dest at
/// <c>0x004A3301</c> is already
/// <see cref="RetailOptionsMenuItemIconDest"/>. This leftover is
/// the expanded-arm <c>CVBufTexture__DrawSpriteEx</c> dest and the
/// <c>add ebp, 3</c> width leftover. Do not redo those. Do not
/// invent dest Y as 5, 15.5, 268, 284, or 304. Do not invent dest
/// X as 322.5 or the 2.0 constant.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A3D38</c> is <c>fadd [0x005D8BA0]</c>.
/// <c>0x004A3D3E</c> is <c>fstp [esp+0x18]</c>.
/// After <c>push ebp</c> that store aliases <c>[esp+0x1C]</c>.
/// <c>0x004A3F16</c> is <c>mov ecx, [esp+0x34]</c>, which aliases
/// that leftover after the DrawSpriteEx pack pushes.
/// <c>0x004A3F36</c> is <c>push ecx</c>. Dest X is the collapsed
/// dest leftover. Dest is not 2.0.</para>
///
/// <para><b>Dest Y.</b> <c>0x004A3E01</c> is <c>dec eax</c>.
/// <c>0x004A3E02</c> is <c>imul [esp+0x24]</c> (label SIZE.cy).
/// <c>0x004A3E07</c> / <c>0x004A3E0A</c> are <c>cdq</c> /
/// <c>sar eax, 1</c>. <c>0x004A3E14</c> is
/// <c>fsubr [esp+0x110]</c> incoming dest Y.
/// <c>0x004A3F00</c> is <c>mov ebp, [esp+0x24]</c>.
/// <c>0x004A3F35</c> is <c>push ebp</c>. Dest Y is incoming dest Y
/// minus integer-half of (count-1)*cy, then clamped against 0.0
/// and 480.0. Dest is not 15.5 and not currentIndex.</para>
///
/// <para><b>Width.</b> <c>0x004A3EF4</c> is <c>add ebp, 3</c>
/// after the max state SIZE.cx walk. Width is max cx plus 3.
/// Nearby 2.0 is the dest X pad leftover, not width.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, the cursor, the colour AND,
/// Apply, dropdown cosine, writing chrome, language pitch,
/// CMenuItem dest, dropdown label dest, icon dest, collapsed
/// value dest, expanded list dest, and the 0x00463669 compare
/// stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownPanelDest
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>dec eax</c>. Count minus one before the half.</summary>
    public const uint CountDecSite = 0x004A3E01u;

    /// <summary><c>imul [esp+0x24]</c>. Label SIZE.cy leftover, not dest.</summary>
    public const uint PitchImulSite = 0x004A3E02u;

    /// <summary><c>cdq</c> before the signed half.</summary>
    public const uint CdqSite = 0x004A3E07u;

    /// <summary><c>sar eax, 1</c>. Integer-half of (count-1)*cy.</summary>
    public const uint HalfSarSite = 0x004A3E0Au;

    /// <summary><c>fsubr [esp+0x110]</c>. Incoming dest Y minus that half.</summary>
    public const uint DestYSubSite = 0x004A3E14u;

    /// <summary>0.0 source. Leftover min dest Y, not dest Y itself.</summary>
    public const uint ClampMinGlobal = 0x005D856Cu;

    /// <summary>IEEE bits at <see cref="ClampMinGlobal"/>. 0.0.</summary>
    public const uint ClampMinBits = 0x00000000u;

    /// <summary>480.0 source. Leftover max dest Y+height, not dest Y.</summary>
    public const uint ClampMaxGlobal = 0x005DB34Cu;

    /// <summary>IEEE bits at <see cref="ClampMaxGlobal"/>. 480.0.</summary>
    public const uint ClampMaxBits = 0x43F00000u;

    /// <summary><c>add ebp, 3</c>. Width leftover, not dest X.</summary>
    public const uint WidthAddSite = 0x004A3EF4u;

    /// <summary>Immediate at <see cref="WidthAddSite"/>.</summary>
    public const int WidthPad = 3;

    /// <summary><c>mov [esp+0x28], ebp</c>. Max cx plus 3.</summary>
    public const uint WidthStoreSite = 0x004A3EFCu;

    /// <summary><c>mov ebp, [esp+0x24]</c>. Dest Y leftover.</summary>
    public const uint DestYLoadSite = 0x004A3F00u;

    /// <summary><c>mov ecx, [esp+0x34]</c>. Dest X leftover.</summary>
    public const uint DestXLoadSite = 0x004A3F16u;

    /// <summary><c>push ebp</c>. Dest Y into DrawSpriteEx.</summary>
    public const uint DestYPushSite = 0x004A3F35u;

    /// <summary><c>push ecx</c>. Dest X into DrawSpriteEx.</summary>
    public const uint DestXPushSite = 0x004A3F36u;

    /// <summary><c>call 0x00555BE0</c>.</summary>
    public const uint DrawSpriteCallSite = 0x004A3F37u;

    /// <summary>Expanded panel draw. Not DrawText.</summary>
    public const uint DrawSpriteEx = 0x00555BE0u;

    /// <summary><c>add esp, 0x3C</c> after DrawSpriteEx.</summary>
    public const uint DrawSpritePop = 0x3Cu;

    /// <summary>2.0 source. Pad leftover, not dest X itself.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary>Expanded flag at <c>[this+0x24]</c>.</summary>
    public const uint ExpandByteOffset = 0x24u;

    /// <summary><c>mov al, [esi+0x24]</c>.</summary>
    public const uint ExpandTestSite = 0x004A3DA8u;

    /// <summary><c>je 0x004A409B</c>.</summary>
    public const uint CollapseJumpSite = 0x004A3DADu;

    /// <summary>Collapsed arm. Not this leftover.</summary>
    public const uint CollapseTarget = 0x004A409Bu;

    /// <summary>Z immediate at <c>0x004A3F30</c>. Not dest.</summary>
    public const uint ZBits = 0x3B83126Fu;

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

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>Dest is collapsed leftover plus centered Y. Do not invent a 5/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a panel fade.</summary>
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

    /// <summary>Panel dest does not half SIZE.cx.</summary>
    public const bool UsesIntegerHalfOfWidth = false;

    /// <summary><see cref="PadBits"/> decoded as IEEE-754 single. Not dest X.</summary>
    public static float Pad => BitConverter.UInt32BitsToSingle(PadBits);

    /// <summary><see cref="ClampMinBits"/> decoded as IEEE-754 single. Not dest Y.</summary>
    public static float ClampMin => BitConverter.UInt32BitsToSingle(ClampMinBits);

    /// <summary><see cref="ClampMaxBits"/> decoded as IEEE-754 single. Not dest Y.</summary>
    public static float ClampMax => BitConverter.UInt32BitsToSingle(ClampMaxBits);

    /// <summary>
    /// <c>cdq / sub eax,edx / sar eax,1</c>. Toward-zero half of
    /// (count-1)*cy. Not float half.
    /// </summary>
    public static int IntegerHalf(int value) => value / 2;

    /// <summary>
    /// Collapsed dest leftover. Dest is not the pad constant.
    /// </summary>
    public static float DestX(float incomingX) => incomingX + Pad;

    /// <summary>
    /// Incoming dest Y minus integer-half of (count-1)*cy, then
    /// clamped so dest Y and dest Y+height stay in [0, 480]. Dest
    /// is not 15.5 and not currentIndex.
    /// </summary>
    public static float DestY(float incomingY, int count, int pitch)
    {
        float destY = incomingY - IntegerHalf((count - 1) * pitch);
        int height = count * pitch;
        if (destY < ClampMin)
        {
            destY = ClampMin;
        }

        if (destY + height > ClampMax)
        {
            if (height > ClampMax)
            {
                return ClampMin;
            }

            return ClampMax - height;
        }

        return destY;
    }

    /// <summary>
    /// Max state SIZE.cx plus the <c>add ebp, 3</c> leftover.
    /// Width is not dest X.
    /// </summary>
    public static float Width(int maxCx) => maxCx + WidthPad;
}
