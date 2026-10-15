// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItem__Render</c> label dest leftover — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> <c>and esi, ebp</c> at <c>0x004A33FC</c>
/// is already <see cref="RetailOptionsMenuItemColor"/>. Apply's
/// cosine is already <see cref="RetailOptionsApplyPulse"/>. Main
/// menu dest at <c>0x0046309D</c> is already
/// Godot <c>RetailMainMenuLabelDest</c>. This leftover is the
/// label dest after <c>0x00540680</c>. Do not redo those. Do not
/// invent dest Y as 5, 268, 284, or 304.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A337B</c> is <c>lea ecx, [esp+0x0C]</c>.
/// <c>0x004A338F</c> is <c>call 0x00540680</c>.
/// <c>0x004A3394</c> is <c>mov eax, [esp+0x0C]</c>.
/// <c>0x004A3398</c> stores identity scale 1.0.
/// <c>0x004A33A0</c> is <c>cdq / sub eax, edx / sar eax, 1</c>.
/// <c>0x004A33A9</c> <c>fild</c>s that half.
/// <c>0x004A33AD</c> is <c>fld [esp+0x18]</c> (incoming dest X).
/// <c>0x004A33B1</c> is <c>fsub st(1)</c> into
/// <c>0x004A33B3</c> <c>fstp [esp+0x24]</c>.</para>
///
/// <para><b>Not dest Y.</b> Nearby <c>fcomp [0x005D85D8]</c>
/// (5.0) at <c>0x004A33BB</c> and the <c>mov [esp+0x24],
/// 0x40A00000</c> store at <c>0x004A33D2</c> are leftover min
/// dest X, not dest Y. Dest Y is incoming <c>[esp+0x1C]</c> at
/// <c>0x004A33EA</c>. DrawOptionTextCentered keeps dest Y as the
/// row top and scale 1.0. The 2px MeasureText residual is width,
/// not this dest. Do not invent dest from 5.0. Do not invent a
/// wrap from the leftover scale arm.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the
/// cursor, the colour AND, Apply, dropdown cosine, writing
/// chrome, language pitch, and the 0x00463669 compare stay
/// untouched.</para>
/// </summary>
public static class RetailOptionsMenuItemDest
{
    /// <summary><c>CMenuItem__Render</c> body at <c>0x004A32C0</c>.</summary>
    public const uint RenderSite = 0x004A32C0u;

    /// <summary><c>lea ecx, [esp+0x0C]</c>. SIZE dest.</summary>
    public const uint SizeLeaSite = 0x004A337Bu;

    /// <summary><c>call 0x00540680</c>.</summary>
    public const uint ExtentCallSite = 0x004A338Fu;

    /// <summary>SIZE store. Same helper as main-menu dest.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary><c>mov eax, [esp+0x0C]</c>. SIZE.cx.</summary>
    public const uint CxLoadSite = 0x004A3394u;

    /// <summary><c>mov [esp+0x20], 0x3F800000</c>.</summary>
    public const uint ScaleStoreSite = 0x004A3398u;

    /// <summary>IEEE bits of the identity scale store. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>sar eax, 1</c> after <c>cdq / sub eax, edx</c>.</summary>
    public const uint HalfSarSite = 0x004A33A3u;

    /// <summary><c>fild [esp+0x24]</c>.</summary>
    public const uint FildHalfSite = 0x004A33A9u;

    /// <summary><c>fld [esp+0x18]</c>. Incoming dest X.</summary>
    public const uint FldDestXSite = 0x004A33ADu;

    /// <summary><c>fsub st(1)</c>.</summary>
    public const uint FsubSite = 0x004A33B1u;

    /// <summary><c>fstp [esp+0x24]</c>. Dest X leftover.</summary>
    public const uint DestStoreSite = 0x004A33B3u;

    /// <summary><c>fcomp [0x005D85D8]</c>.</summary>
    public const uint FcompSite = 0x004A33BBu;

    /// <summary>5.0 source. Leftover min dest X, not dest Y.</summary>
    public const uint LeftoverMinGlobal = 0x005D85D8u;

    /// <summary>IEEE bits at <see cref="LeftoverMinGlobal"/>. 5.0.</summary>
    public const uint LeftoverMinBits = 0x40A00000u;

    /// <summary><c>mov [esp+0x24], 0x40A00000</c>. Leftover min dest X.</summary>
    public const uint LeftoverMinStoreSite = 0x004A33D2u;

    /// <summary><c>mov eax, [esp+0x1C]</c>. Incoming dest Y.</summary>
    public const uint DestYLoadSite = 0x004A33EAu;

    /// <summary><c>call 0x004659A0</c> after the colour AND.</summary>
    public const uint DrawTextCallSite = 0x004A3410u;

    /// <summary>Label draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary>Dest Y is not a 5 push.</summary>
    public const bool InventsDestY5 = false;

    /// <summary>Centered dest X is not a 5 push.</summary>
    public const bool InventsDestX5 = false;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not a 284 push.</summary>
    public const bool InventsDestY284 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>Dest is incoming minus integer-half. Do not invent a 5/268/284/304 push.</summary>
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

    /// <summary>0x004A33FC is already <see cref="RetailOptionsMenuItemColor"/>.</summary>
    public const bool RedoesMenuItemColor = false;

    /// <summary>0x004A4310 is already <see cref="RetailOptionsApplyPulse"/>.</summary>
    public const bool RedoesApplyPulse = false;

    /// <summary>0x0046309D is already main-menu dest.</summary>
    public const bool RedoesLabelDest = false;

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

    /// <summary><see cref="LeftoverMinBits"/> decoded as IEEE-754 single. Not dest Y.</summary>
    public static float LeftoverMinX => BitConverter.UInt32BitsToSingle(LeftoverMinBits);

    /// <summary>
    /// <c>cdq / sub eax,edx / sar eax,1</c>. Toward-zero half of
    /// SIZE.cx. Not float half.
    /// </summary>
    public static int IntegerHalf(int value) => value / 2;

    /// <summary>
    /// Incoming dest X minus integer-half SIZE.cx. Do not invent
    /// dest from 5.0.
    /// </summary>
    public static float DestX(float centerX, int cx) => centerX - IntegerHalf(cx);

    /// <summary>
    /// Identity 1.0 unless leftover dest X is below 5.0.
    /// DrawOptionTextCentered keeps 1.0.
    /// </summary>
    public static float Scale(float centerX, int cx)
    {
        int half = IntegerHalf(cx);
        if (half == 0)
        {
            return IdentityScale;
        }

        if (DestX(centerX, cx) < LeftoverMinX)
        {
            return (centerX - LeftoverMinX) / half;
        }

        return IdentityScale;
    }
}
