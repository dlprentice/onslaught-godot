// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItemDropdown::Render</c> label dest leftover — recovered
/// from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> <c>cmp [esi+0x1C], [esi+0x20]</c> at
/// <c>0x004A3C69</c> is already
/// <see cref="RetailOptionsApplyPulse.DropdownCompareSite"/>.
/// <c>CMenuItem__Render</c> dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/> (integer-half). This
/// leftover is incoming dest X minus full SIZE.cx after
/// <c>0x00540680</c>. Do not redo those. Do not invent dest Y as
/// 5, 268, 284, or 304. Do not invent dest from the nearby 2.0
/// pad.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A3D0D</c> is <c>call 0x00515A70</c>.
/// <c>0x004A3D14</c> is <c>call 0x00540680</c>.
/// <c>0x004A3D19</c> is <c>fild [esp+0x1C]</c> (SIZE.cx).
/// <c>0x004A3D1D</c> is <c>fld [esp+0x108]</c> (incoming dest X).
/// <c>0x004A3D24</c> stores identity scale 1.0.
/// <c>0x004A3D2C</c> is <c>fsub st(1)</c> into
/// <c>0x004A3D2E</c> <c>fstp [esp+0x0C]</c>.</para>
///
/// <para><b>Not dest Y.</b> Nearby <c>fcomp [0x005D85D8]</c>
/// (5.0) at <c>0x004A3D46</c> and the <c>mov [esp+0x0C],
/// 0x40A00000</c> store at <c>0x004A3D60</c> are leftover min
/// dest X, not dest Y. Nearby <c>fadd [0x005D8BA0]</c> is 2.0
/// and is not dest. Dest Y is incoming <c>[esp+0x10C]</c> at
/// <c>0x004A3D78</c>. DrawLabelValueRow keeps dest Y as the row
/// top and scale 1.0. The collapsed value dest at
/// <c>0x004A40B4</c> is a later leftover. The 2px MeasureText
/// residual is width, not this dest. Do not invent dest from
/// 5.0 or 2.0. Do not invent a wrap from the leftover scale
/// arm.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the
/// cursor, the colour AND, Apply, dropdown cosine, writing
/// chrome, language pitch, CMenuItem dest, and the 0x00463669
/// compare stay untouched.</para>
/// </summary>
public static class RetailOptionsDropdownDest
{
    /// <summary><c>CMenuItemDropdown::Render</c> body at <c>0x004A3C30</c>.</summary>
    public const uint RenderSite = 0x004A3C30u;

    /// <summary><c>call 0x00515A70</c> before the extent.</summary>
    public const uint FontCallSite = 0x004A3D0Du;

    /// <summary>Font helper. Not the dest leftover.</summary>
    public const uint Font = 0x00515A70u;

    /// <summary><c>call 0x00540680</c>.</summary>
    public const uint ExtentCallSite = 0x004A3D14u;

    /// <summary>SIZE store. Same helper as CMenuItem dest.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary><c>fild [esp+0x1C]</c>. SIZE.cx, not integer-half.</summary>
    public const uint FildCxSite = 0x004A3D19u;

    /// <summary><c>fld [esp+0x108]</c>. Incoming dest X.</summary>
    public const uint FldDestXSite = 0x004A3D1Du;

    /// <summary><c>mov [esp+0x14], 0x3F800000</c>.</summary>
    public const uint ScaleStoreSite = 0x004A3D24u;

    /// <summary>IEEE bits of the identity scale store. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>fsub st(1)</c>.</summary>
    public const uint FsubSite = 0x004A3D2Cu;

    /// <summary><c>fstp [esp+0x0C]</c>. Dest X leftover.</summary>
    public const uint DestStoreSite = 0x004A3D2Eu;

    /// <summary><c>fcomp [0x005D85D8]</c>.</summary>
    public const uint FcompSite = 0x004A3D46u;

    /// <summary>5.0 source. Leftover min dest X, not dest Y.</summary>
    public const uint LeftoverMinGlobal = 0x005D85D8u;

    /// <summary>IEEE bits at <see cref="LeftoverMinGlobal"/>. 5.0.</summary>
    public const uint LeftoverMinBits = 0x40A00000u;

    /// <summary><c>mov [esp+0x0C], 0x40A00000</c>. Leftover min dest X.</summary>
    public const uint LeftoverMinStoreSite = 0x004A3D60u;

    /// <summary>Nearby 2.0 source. Not dest.</summary>
    public const uint PadGlobal = 0x005D8BA0u;

    /// <summary>IEEE bits at <see cref="PadGlobal"/>. 2.0.</summary>
    public const uint PadBits = 0x40000000u;

    /// <summary><c>mov ebx, [esp+0x10C]</c>. Incoming dest Y.</summary>
    public const uint DestYLoadSite = 0x004A3D78u;

    /// <summary><c>call 0x004659A0</c> after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A3DA3u;

    /// <summary>Label draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary>Collapsed value dest. Later leftover, not this dest.</summary>
    public const uint CollapsedValueDestSite = 0x004A40B4u;

    /// <summary>Dest Y is not a 5 push.</summary>
    public const bool InventsDestY5 = false;

    /// <summary>Right-aligned dest X is not a 5 push.</summary>
    public const bool InventsDestX5 = false;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not a 284 push.</summary>
    public const bool InventsDestY284 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>Nearby 2.0 is not dest.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest is incoming minus full width. Do not invent a 5/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover scale arm is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a label fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3394 is already <see cref="RetailOptionsMenuItemDest"/>.</summary>
    public const bool RedoesMenuItemDest = false;

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

    /// <summary>Dropdown dest subtracts full SIZE.cx. CMenuItem dest halves.</summary>
    public const bool UsesIntegerHalf = false;

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float IdentityScale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="LeftoverMinBits"/> decoded as IEEE-754 single. Not dest Y.</summary>
    public static float LeftoverMinX => BitConverter.UInt32BitsToSingle(LeftoverMinBits);

    /// <summary><see cref="PadBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Pad => BitConverter.UInt32BitsToSingle(PadBits);

    /// <summary>
    /// SIZE.cx as <c>fild</c>ed. Full width, not integer-half.
    /// </summary>
    public static int Width(int cx) => cx;

    /// <summary>
    /// Incoming dest X minus full SIZE.cx. Do not invent dest
    /// from 5.0 or 2.0.
    /// </summary>
    public static float DestX(float incomingX, int cx) => incomingX - Width(cx);

    /// <summary>
    /// Identity 1.0 unless leftover dest X is below 5.0.
    /// DrawLabelValueRow keeps 1.0.
    /// </summary>
    public static float Scale(float incomingX, int cx)
    {
        int width = Width(cx);
        if (width == 0)
        {
            return IdentityScale;
        }

        if (DestX(incomingX, cx) < LeftoverMinX)
        {
            return (incomingX - LeftoverMinX) / width;
        }

        return IdentityScale;
    }
}
