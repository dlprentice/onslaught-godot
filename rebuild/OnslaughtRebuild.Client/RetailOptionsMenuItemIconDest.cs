// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Client;

/// <summary>
/// <c>CMenuItem__Render</c> icon dest leftover — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Label dest at <c>0x004A3394</c> is already
/// <see cref="RetailOptionsMenuItemDest"/> (integer-half, leftover
/// min dest X). Dropdown dest at <c>0x004A3D19</c> is already
/// <see cref="RetailOptionsDropdownDest"/> (full width). This leftover
/// is the earlier <c>[this+0x0C]</c> arm after <c>0x00540680</c>:
/// integer-half then <c>fsubr</c>, with no leftover min dest X. Do
/// not redo those. Do not invent dest Y as 5, 20, 268, 284, or
/// 304.</para>
///
/// <para><b>Dest X.</b> Official 74154bfa independently re-read:
/// <c>0x004A32CC</c> is <c>mov eax, [edi+0x0C]</c>.
/// <c>0x004A32E1</c> is <c>call 0x004F2580</c>.
/// <c>0x004A32F5</c> is <c>call 0x00515A70</c>.
/// <c>0x004A32FC</c> is <c>call 0x00540680</c>.
/// <c>0x004A3301</c> is <c>mov eax, [esp+0x10]</c>.
/// <c>0x004A3305</c> pushes identity scale 1.0.
/// <c>0x004A330A</c> is <c>cdq / sub eax, edx</c>.
/// <c>0x004A3311</c> is <c>sar eax, 1</c>.
/// <c>0x004A331C</c> <c>fild</c>s that half.
/// <c>0x004A332C</c> is <c>fsubr [esp+0x2C]</c> into
/// <c>0x004A3338</c> <c>fstp [esp+0x44]</c>.</para>
///
/// <para><b>Not dest Y.</b> Dest Y is incoming
/// <c>[esp+0x24]</c> at <c>0x004A330D</c>. Nearby
/// <c>fadd [0x005D857C]</c> (20.0) at <c>0x004A3353</c> is leftover
/// label pitch, not dest. Nearby 5.0 at <c>0x005D85D8</c> is the
/// later label leftover, not this dest. Nearby 2.0 is not dest.
/// <c>Init</c> and <c>InitWithIcon</c> both store 0 at
/// <c>[this+0x0C]</c>. Do not invent a prefix draw. DrawOptionRow
/// cites DestX. Dest Y keeps the row top and scale 1.0. The 2px
/// MeasureText residual is width, not this dest.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the
/// cursor, the colour AND, Apply, dropdown cosine, writing
/// chrome, language pitch, CMenuItem dest, dropdown dest, and
/// the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailOptionsMenuItemIconDest
{
    /// <summary><c>CMenuItem__Render</c> body at <c>0x004A32C0</c>.</summary>
    public const uint RenderSite = 0x004A32C0u;

    /// <summary><c>[this+0x0C]</c>. Icon string id.</summary>
    public const uint IconIdOffset = 0x0Cu;

    /// <summary><c>mov eax, [edi+0x0C]</c>.</summary>
    public const uint IconIdLoadSite = 0x004A32CCu;

    /// <summary><c>call 0x004F2580</c>.</summary>
    public const uint GetStringCallSite = 0x004A32E1u;

    /// <summary><c>CText__GetStringById</c>.</summary>
    public const uint GetStringById = 0x004F2580u;

    /// <summary><c>call 0x00515A70</c>.</summary>
    public const uint FontCallSite = 0x004A32F5u;

    /// <summary>Font helper. Not the dest leftover.</summary>
    public const uint Font = 0x00515A70u;

    /// <summary><c>call 0x00540680</c>.</summary>
    public const uint ExtentCallSite = 0x004A32FCu;

    /// <summary>SIZE store. Same helper as CMenuItem dest.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary><c>ret 8</c> at the extent helper.</summary>
    public const uint GetTextExtentRetSite = 0x00540823u;

    /// <summary><c>mov eax, [esp+0x10]</c>. SIZE.cx.</summary>
    public const uint CxLoadSite = 0x004A3301u;

    /// <summary><c>push 0x3F800000</c>.</summary>
    public const uint ScalePushSite = 0x004A3305u;

    /// <summary>IEEE bits of the identity scale push. 1.0.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>cdq</c> before the half.</summary>
    public const uint CdqSite = 0x004A330Au;

    /// <summary><c>mov edx, [esp+0x24]</c>. Incoming dest Y.</summary>
    public const uint DestYLoadSite = 0x004A330Du;

    /// <summary><c>sar eax, 1</c> after <c>cdq / sub eax, edx</c>.</summary>
    public const uint HalfSarSite = 0x004A3311u;

    /// <summary><c>fild [esp+0x30]</c>.</summary>
    public const uint FildHalfSite = 0x004A331Cu;

    /// <summary><c>fsubr [esp+0x2C]</c>.</summary>
    public const uint FsubrSite = 0x004A332Cu;

    /// <summary><c>fstp [esp+0x44]</c>. Dest X leftover.</summary>
    public const uint DestStoreSite = 0x004A3338u;

    /// <summary><c>call 0x004659A0</c> after the dest leftover.</summary>
    public const uint DrawTextCallSite = 0x004A334Au;

    /// <summary>Icon draw. Not DrawTextDynamic.</summary>
    public const uint DrawText = 0x004659A0u;

    /// <summary><c>fadd [0x005D857C]</c>. Leftover label pitch, not dest.</summary>
    public const uint PitchAddSite = 0x004A3353u;

    /// <summary>20.0 source. Leftover label pitch, not dest Y.</summary>
    public const uint PitchGlobal = 0x005D857Cu;

    /// <summary>IEEE bits at <see cref="PitchGlobal"/>. 20.0.</summary>
    public const uint PitchBits = 0x41A00000u;

    /// <summary>This arm uses <c>fsubr</c>, not <c>fsub st(1)</c>.</summary>
    public const bool UsesFsubr = true;

    /// <summary>No leftover min dest X compare in this arm.</summary>
    public const bool HasLeftoverMinDestX = false;

    /// <summary>Dest Y is not a 5 push.</summary>
    public const bool InventsDestY5 = false;

    /// <summary>Centered dest X is not a 5 push.</summary>
    public const bool InventsDestX5 = false;

    /// <summary>Dest Y is not a 20 push.</summary>
    public const bool InventsDestY20 = false;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not a 284 push.</summary>
    public const bool InventsDestY284 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>Nearby 2.0 is not dest.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Nearby 20.0 is leftover label pitch, not dest.</summary>
    public const bool InventsDestFromPitch = false;

    /// <summary>Dest is incoming minus integer-half. Do not invent a 5/20/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>There is no leftover scale arm to invent a wrap from.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent an icon fade.</summary>
    public const bool InventsFade = false;

    /// <summary><c>Init</c> / <c>InitWithIcon</c> store 0 at +0x0C.</summary>
    public const bool InventsPrefixDraw = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3394 is already <see cref="RetailOptionsMenuItemDest"/>.</summary>
    public const bool RedoesMenuItemDest = false;

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

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float IdentityScale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="PitchBits"/> decoded as IEEE-754 single. Not dest Y.</summary>
    public static float LeftoverLabelPitch => BitConverter.UInt32BitsToSingle(PitchBits);

    /// <summary>
    /// <c>cdq / sub eax,edx / sar eax,1</c>. Toward-zero half of
    /// SIZE.cx. Not float half.
    /// </summary>
    public static int IntegerHalf(int value) => value / 2;

    /// <summary>
    /// Incoming dest X minus integer-half SIZE.cx. Do not invent
    /// dest from 5.0 or 20.0.
    /// </summary>
    public static float DestX(float incomingX, int cx) => incomingX - IntegerHalf(cx);

    /// <summary>
    /// Identity 1.0. This arm has no leftover min dest X.
    /// </summary>
    public static float Scale(float incomingX, int cx) => IdentityScale;
}
