// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render label GetTextExtent dest law — recovered
/// from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> The other CFEPMain::Render
/// DrawTextDynamic at <c>0x0046316F</c> is already
/// <see cref="RetailMainMenuLabelText"/>. Colour at
/// <c>0x0046300B</c> is already
/// <see cref="RetailMainMenuLabelColor"/>. This leftover is
/// the measure dest that produces ebx / <c>[esp+0x24]</c>.
/// Do not redo those.</para>
///
/// <para><b>Measure.</b> Regular rows (ebx != -1) save the
/// <c>[edx+0x30]</c> text pointer at <c>[esp+0x28]</c>.
/// <c>0x00462F25</c> is an address computation (esp+0x18) into ecx, then
/// a push of ecx, then push of eax, then push of constant 1,
/// a load of constant 0x0088A0A8 into ecx, a direct call to 0x00515A70 at
/// <c>0x00462F36</c>, a direct call to 0x00540680 at
/// <c>0x00462F3D</c>. The body exits (a return that pops 8 bytes of arguments). SIZE
/// layout is a store of edx to value pointed to by edi at <c>0x00540807</c>
/// (cx at +0) then a store of eax to field +4 of edi at
/// <c>0x00540815</c> (cy at +4). Do not invent dest
/// immediates from either store.</para>
///
/// <para><b>Dest X.</b> After the colour unpack,
/// <c>0x0046303B</c> is an integer-to-float load of stack local +0x18,
/// <c>0x0046303F</c> is a float multiply by the value at 0x005D85EC (0.5),
/// <c>0x00463045</c> is a reversed float subtract of the value at 0x005DB5E0 (219.0),
/// then a float store to stack local +0x30 and
/// a load of stack local +0x30 into ebx at <c>0x00463077</c>. Dest X
/// is therefore <c>219.0 − cx×0.5</c>, not a 219 push.</para>
///
/// <para><b>Dest Y.</b> <c>0x0046308C</c> loads
/// <c>[esp+0x1C]</c> (cy). Then <c>cdq</c> /
/// a subtraction of edx from eax, then arithmetic right shift of eax by 1 at
/// <c>0x00463093</c>, <c>fild</c>,
/// a reversed float subtract of stack local +0x10 at <c>0x0046309D</c>,
/// a float store to stack local +0x24. Dest Y is the row-Y slot minus
/// integer-half cy, not float-half cy and not a 304
/// immediate. The 2px MeasureText residual is width, not
/// this dest. DrawMainMenu keeps LabelColor, MeasureText
/// width, and scale 1.0. Dest Y keeps the existing
/// <c>rowY - 8</c> until cy is measured. Do not invent a
/// cy immediate. Do not invent a 2px kerning hack.</para>
///
/// <para><b>Not a fade.</b> Post-call
/// (a float load of stack local +0x10, then float add of the value at 0x005D857C) is the 20.0
/// row pitch already named by
/// <see cref="RetailMainMenuLabelText"/>. Nearby
/// float add of the value at 0x005DB5D8 (36.0) is a different path
/// skipped by the label (a jump to 0x0046364D). This type
/// does not invent dest from that 36.0. It does not invent
/// a sheen, dest immediates, a wrap width, or a 2px
/// kerning hack. It does not change MeasureText.</para>
///
/// <para><b>Not dest/Z/font/flags of the version overlay.</b>
/// Not <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion,
/// the cursor, Apply, dropdown, the colour AND, the
/// writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron
/// colour, the label colour, the label DrawTextDynamic, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font slot,
/// the version post-draw flags, the version pre-draw enable,
/// the version widen, the version tail, the title-logo
/// shadow dest/Z, the title-logo body dest/Z, the
/// selected-row icon colours, and the 0x00463873 /
/// 0x004638B7 / 0x00463A8F / 0x00463AD3 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuLabelDest
{
    /// <summary>An address computation (esp+0x18) into ecx. SIZE out-pointer.</summary>
    public const uint SizeLeaSite = 0x00462F25u;

    /// <summary>A store of eax to stack local +0x28 after <c>[edx+0x30]</c>.</summary>
    public const uint TextSaveSite = 0x00462F29u;

    /// <summary>A push of constant 1. FONT_SMALL for <see cref="FontHelper"/>.</summary>
    public const uint FontSlotPushSite = 0x00462F2Fu;

    /// <summary>GPL <c>FONT_SMALL</c>. Same slot as the label draw.</summary>
    public const int FontSlot = 1;

    /// <summary>A load of constant 0x0088A0A8 into ecx at <c>0x00462F31</c>.</summary>
    public const uint FontThis = 0x0088A0A8u;

    /// <summary>A direct call to 0x00515A70 at <c>0x00462F36</c>.</summary>
    public const uint FontCallSite = 0x00462F36u;

    /// <summary><c>CPlatform__Font</c>. A return that pops 4 bytes of arguments.</summary>
    public const uint FontHelper = 0x00515A70u;

    /// <summary>A direct call to 0x00540680 at <c>0x00462F3D</c>.</summary>
    public const uint GetTextExtentSite = 0x00462F3Du;

    /// <summary><c>CDXFont__GetTextExtent</c>. A return that pops 8 bytes of arguments.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary>A store of edx to value pointed to by edi. SIZE.cx fistp.</summary>
    public const uint SizeCxStoreSite = 0x00540807u;

    /// <summary>A store of eax to field +4 of edi. SIZE.cy fistp.</summary>
    public const uint SizeCyStoreSite = 0x00540815u;

    /// <summary>First dword of the SIZE out-pointer.</summary>
    public const int SizeCxOffset = 0;

    /// <summary>Second dword of the SIZE out-pointer.</summary>
    public const int SizeCyOffset = 4;

    /// <summary>A return that pops 8 bytes of arguments at <c>0x00540823</c>.</summary>
    public const int BodyRetImmediate = 8;

    /// <summary>An integer-to-float load of stack local +0x18. SIZE.cx, not dest.</summary>
    public const uint DestXFildSite = 0x0046303Bu;

    /// <summary>A float multiply by the value at 0x005D85EC.</summary>
    public const uint DestXHalfMulSite = 0x0046303Fu;

    /// <summary>Half-extent global. Same 0.5 as click-to-start.</summary>
    public const uint HalfGlobal = 0x005D85ECu;

    /// <summary>IEEE bits at <see cref="HalfGlobal"/>. 0.5.</summary>
    public const uint HalfBits = 0x3F000000u;

    /// <summary>A reversed float subtract of the value at 0x005DB5E0.</summary>
    public const uint DestXFsubrSite = 0x00463045u;

    /// <summary><c>fsubr</c> source. Dest X is not a push immediate.</summary>
    public const uint DestXGlobal = 0x005DB5E0u;

    /// <summary>IEEE bits at <see cref="DestXGlobal"/>. 219.0.</summary>
    public const uint DestXGlobalBits = 0x435B0000u;

    /// <summary>A float store to stack local +0x30.</summary>
    public const uint DestXStoreSite = 0x0046304Bu;

    /// <summary>A load of stack local +0x30 into ebx. Dest X into ebx.</summary>
    public const uint DestXLoadSite = 0x00463077u;

    /// <summary>A load of stack local +0x1C into eax. SIZE.cy, not dest.</summary>
    public const uint DestYCyLoadSite = 0x0046308Cu;

    /// <summary>An arithmetic right shift of eax by 1 after a sign extension
    /// of eax into edx, then subtraction of edx from eax.</summary>
    public const uint DestYSarSite = 0x00463093u;

    /// <summary>A reversed float subtract of stack local +0x10. Row-Y slot, not a 304 immediate.</summary>
    public const uint DestYFsubrSite = 0x0046309Du;

    /// <summary>A float store to stack local +0x24. Dest Y later loaded into ecx.</summary>
    public const uint DestYStoreSite = 0x004630A1u;

    /// <summary>Dest is ebx/ecx. Do not invent a 219/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover dest is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a label fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Dest Y uses integer-half cy, not <c>cy×0.5</c>.</summary>
    public const bool InventsFloatHalfDestY = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x0046316F is already <see cref="RetailMainMenuLabelText"/>.</summary>
    public const bool RedoesLabelText = false;

    /// <summary>0x004641ED is already <see cref="RetailMainMenuVersionOverlayTail"/>.</summary>
    public const bool RedoesVersionOverlayTail = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00462FF3 is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>SIZE layout is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="HalfBits"/> decoded as IEEE-754 single.</summary>
    public static float Half => BitConverter.UInt32BitsToSingle(HalfBits);

    /// <summary><see cref="DestXGlobalBits"/> decoded as IEEE-754 single.</summary>
    public static float DestXAnchor => BitConverter.UInt32BitsToSingle(DestXGlobalBits);

    /// <summary>
    /// A sign extension of eax into edx, then subtraction of edx from
    /// eax, then arithmetic right shift of eax by 1. Toward-zero half of
    /// SIZE.cy. Not float half.
    /// </summary>
    public static int IntegerHalf(int value) => value / 2;

    /// <summary>
    /// <c>219.0 − cx×0.5</c>. <paramref name="cx"/> is SIZE.cx
    /// after <c>fild</c>, or the current MeasureText width.
    /// </summary>
    public static float DestX(float cx) => DestXAnchor - (cx * Half);

    /// <summary>
    /// Row-Y slot minus integer-half SIZE.cy. Do not invent a
    /// 304 dest immediate.
    /// </summary>
    public static float DestY(float rowY, int cy) => rowY - IntegerHalf(cy);
}
