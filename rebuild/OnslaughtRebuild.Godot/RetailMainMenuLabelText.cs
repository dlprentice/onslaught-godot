// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render label DrawTextDynamic leftover — recovered
/// from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Colour at <c>0x0046300B</c> is already
/// <see cref="RetailMainMenuLabelColor"/>. Language sine at
/// <c>0x0046319E</c> is already
/// <see cref="RetailMainMenuLanguageSine"/> and is the
/// <c>cmp ebx, -1 / je 0x00463191</c> arm. Version overlay
/// DrawTextDynamic tail at <c>0x004641ED</c> is already
/// <see cref="RetailMainMenuVersionOverlayTail"/>. This leftover
/// is the other <c>call 0x00465710</c>. Do not redo those.</para>
///
/// <para><b>Call.</b> Regular rows (ebx != -1) save the
/// <c>[edx+0x30]</c> text pointer at <c>[esp+0x28]</c> and
/// measure it with FONT_SMALL /
/// <c>CDXFont__GetTextExtent</c>. After the colour unpack,
/// <c>0x0046313D</c> / <c>0x00463141</c> reload
/// <c>[esp+0x28]</c> / <c>[esp+0x24]</c>. Then
/// <c>0x00463145</c> / <c>0x00463147</c> / <c>0x00463149</c>
/// push 0 / 0 / <c>0x447A0000</c>, <c>push eax</c> /
/// <c>push ebp</c> / 1.0 / 1.0 / <c>0x3EA3D70A</c> /
/// <c>push ecx</c> / <c>push ebx</c> / <c>push 1</c>,
/// <c>mov ecx, 0x0088A0A8</c>, <c>call 0x00515A70</c> at
/// <c>0x00463168</c>, <c>mov ecx, eax</c>,
/// <c>call 0x00465710</c> at <c>0x0046316F</c>. The body
/// exits <c>RET 0x28</c>. Dest is ebx (X) and ecx (Y), not
/// immediates. The leftover float is past fcom 0.0 / 0.25 /
/// 0.5. Arg 9 is 0 so that colour arm is skipped.</para>
///
/// <para><b>Z.</b> <c>0x3EA3D70A</c> is IEEE 0.32. It is not
/// writing-chrome Z 0.9, not selector-bar Z 0.33, and not
/// version overlay Z 0.01. Identity scales stay 1.0. The 2-D
/// consumer ignores z.</para>
///
/// <para><b>Cite-fix.</b> <c>cmp ebx, 0x3E8</c> is at
/// <c>0x00465771</c> (<c>81 fb e8 03 00 00</c>).
/// <c>0x00465777</c> is <c>mov word [eax], 0</c>. Do not
/// invent wrap from either site. This is not a new leftover.</para>
///
/// <para><b>Not dest immediates.</b> Dest X is ebx after
/// <c>fsubr [0x005DB5E0]</c> (219.0 minus half extent). Dest
/// Y is <c>[esp+0x24]</c> after <c>fsubr [esp+0x10]</c>
/// (row Y minus half height). Do not invent dest immediates.
/// The 2px MeasureText residual stays open. DrawMainMenu
/// keeps LabelColor, MeasureText dest, and scale 1.0.</para>
///
/// <para><b>Not a fade.</b> Post-call
/// <c>fld [esp+0x10] / fadd [0x005D857C]</c> is the 20.0 row
/// pitch into the loop increment at <c>0x0046364D</c>, not a
/// label fade. <c>0x009C68AC</c> / <c>0x009C690D</c> stores
/// are already <see cref="RetailMainMenuVersionOverlayFlags"/>.
/// This type does not invent a sheen, dest immediates, a wrap
/// width, or a 2px kerning hack.</para>
///
/// <para><b>Not dest/Z/font/flags of the version overlay.</b>
/// Not <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion,
/// the cursor, Apply, dropdown, the colour AND, the
/// writing-chrome Y, the writing-chrome colour, the
/// writing-chrome Z/X, the sine pin, the blink, the chevron
/// colour, the label colour, the selector-bar colour, the
/// selector-bar Z/X, the version format/colour, the version
/// dest/Z, the version font slot, the version post-draw
/// flags, the version pre-draw enable, the version widen, the
/// version tail, the title-logo shadow dest/Z, the title-logo
/// body dest/Z, the selected-row icon colours, and the
/// 0x00463873 / 0x004638B7 / 0x00463A8F / 0x00463AD3 /
/// 0x00463D1F / 0x00463D63 / 0x00463F3F / 0x00463F83 pair
/// stay untouched.</para>
/// </summary>
public static class RetailMainMenuLabelText
{
    /// <summary><c>mov [esp+0x28], eax</c> after <c>[edx+0x30]</c>.</summary>
    public const uint TextSaveSite = 0x00462F29u;

    /// <summary><c>call 0x00540680</c> at <c>0x00462F3D</c>.</summary>
    public const uint GetTextExtentSite = 0x00462F3Du;

    /// <summary><c>CDXFont__GetTextExtent</c>. Not dest and not a 2px hack.</summary>
    public const uint GetTextExtent = 0x00540680u;

    /// <summary><c>cmp ebx, -1 / je 0x00463191</c> at <c>0x00462F14</c>.</summary>
    public const uint LanguageRowGateSite = 0x00462F17u;

    /// <summary>Language-sine arm. Already <see cref="RetailMainMenuLanguageSine"/>.</summary>
    public const uint LanguageRowTarget = 0x00463191u;

    /// <summary><c>mov eax, [esp+0x28]</c>. Text pointer.</summary>
    public const uint TextLoadSite = 0x0046313Du;

    /// <summary><c>mov ecx, [esp+0x24]</c>. Dest Y, not an immediate.</summary>
    public const uint DestYLoadSite = 0x00463141u;

    /// <summary><c>push 0</c>. DrawTextDynamic stack arg 10.</summary>
    public const uint FirstLeftoverPushSite = 0x00463145u;

    /// <summary>Second <c>push 0</c>. DrawTextDynamic stack arg 9.</summary>
    public const uint SecondLeftoverPushSite = 0x00463147u;

    /// <summary><c>push 0x447A0000</c>. DrawTextDynamic stack arg 8.</summary>
    public const uint FloatLeftoverPushSite = 0x00463149u;

    /// <summary>IEEE bits of stack arg 8. Not dest and not wrap.</summary>
    public const uint FloatSlotBits = 0x447A0000u;

    /// <summary><c>push eax</c>. Wide text from <see cref="TextLoadSite"/>.</summary>
    public const uint TextPushSite = 0x0046314Eu;

    /// <summary><c>push ebp</c>. Already <see cref="RetailMainMenuLabelColor"/>.</summary>
    public const uint ColorPushSite = 0x0046314Fu;

    /// <summary><c>push 0x3F800000</c>. Identity scale Y.</summary>
    public const uint ScaleYPushSite = 0x00463150u;

    /// <summary><c>push 0x3F800000</c>. Identity scale X.</summary>
    public const uint ScaleXPushSite = 0x00463155u;

    /// <summary>Identity scale immediate. Not Z.</summary>
    public const uint ScaleBits = 0x3F800000u;

    /// <summary><c>push 0x3EA3D70A</c>. Z, not scale.</summary>
    public const uint ZPushSite = 0x0046315Au;

    /// <summary>Z immediate. Not writing 0.9 and not selector-bar 0.33.</summary>
    public const uint ZBits = 0x3EA3D70Au;

    /// <summary><c>push ecx</c>. Dest Y from <see cref="DestYLoadSite"/>.</summary>
    public const uint DestYPushSite = 0x0046315Fu;

    /// <summary><c>push ebx</c>. Dest X. Not an immediate.</summary>
    public const uint DestXPushSite = 0x00463160u;

    /// <summary><c>push 1</c>. FONT_SMALL for <see cref="FontHelper"/>.</summary>
    public const uint FontSlotPushSite = 0x00463161u;

    /// <summary>GPL <c>FONT_SMALL</c>. Same slot as the version overlay.</summary>
    public const int FontSlot = 1;

    /// <summary><c>mov ecx, 0x0088A0A8</c> at <c>0x00463163</c>.</summary>
    public const uint FontThis = 0x0088A0A8u;

    /// <summary><c>call 0x00515A70</c> at <c>0x00463168</c>.</summary>
    public const uint FontCallSite = 0x00463168u;

    /// <summary><c>CPlatform__Font</c>. <c>RET 4</c>.</summary>
    public const uint FontHelper = 0x00515A70u;

    /// <summary><c>call 0x00465710</c> at <c>0x0046316F</c>.</summary>
    public const uint CallSite = 0x0046316Fu;

    /// <summary><c>CDXFont__DrawTextDynamic</c>.</summary>
    public const uint DrawTextDynamic = 0x00465710u;

    /// <summary><c>RET 0x28</c> at <c>0x00465997</c>.</summary>
    public const uint BodyRetSite = 0x00465997u;

    /// <summary>Ten stack dwords. Not <c>RET 0x2C</c>.</summary>
    public const int BodyRetImmediate = 0x28;

    /// <summary>Thiscall stack arity implied by <see cref="BodyRetImmediate"/>.</summary>
    public const int StackArgCount = 10;

    /// <summary><c>jmp 0x0046364D</c> after the two flag stores.</summary>
    public const uint PostCallJmpSite = 0x0046318Cu;

    /// <summary>Loop increment. Adds 20.0 to row Y. Not a fade.</summary>
    public const uint PostCallJmpTarget = 0x0046364Du;

    /// <summary><c>cmp ebx, 0x3E8</c>. Cite-fix: not 0x00465777.</summary>
    public const uint LengthClampSite = 0x00465771u;

    /// <summary><c>mov word [eax], 0</c>. Not the cmp and not wrap.</summary>
    public const uint LengthClampStoreSite = 0x00465777u;

    /// <summary>Wchar-count clamp. Not the leftover float.</summary>
    public const int LengthClampImmediate = 0x3E8;

    /// <summary><c>fsubr [0x005DB5E0]</c> source. Dest X is not a push immediate.</summary>
    public const uint DestXGlobal = 0x005DB5E0u;

    /// <summary>IEEE bits at <see cref="DestXGlobal"/>. 219.0.</summary>
    public const uint DestXGlobalBits = 0x435B0000u;

    /// <summary>Immediate stored as stack arg 9. Skips that arm.</summary>
    public const int SecondSlot = 0;

    /// <summary>Immediate stored as stack arg 10. Not the title-bar flag.</summary>
    public const int FirstSlot = 0;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>Dest is ebx/ecx. Do not invent a 219/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The leftover float is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a label fade.</summary>
    public const bool InventsFade = false;

    /// <summary>0x3E8 is a wchar clamp, not this leftover.</summary>
    public const bool InventsLengthClampAsWrap = false;

    /// <summary>0x004641ED is already <see cref="RetailMainMenuVersionOverlayTail"/>.</summary>
    public const bool RedoesVersionOverlayTail = false;

    /// <summary>0x004641C4 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00462FF3 is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x0046319E is already <see cref="RetailMainMenuLanguageSine"/>.</summary>
    public const bool OwnsLanguageSine = false;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary><see cref="FloatSlotBits"/> decoded as IEEE-754 single.</summary>
    public static float FloatSlot => BitConverter.UInt32BitsToSingle(FloatSlotBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float ScaleX => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single.</summary>
    public static float ScaleY => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="DestXGlobalBits"/> decoded as IEEE-754 single.</summary>
    public static float DestXAnchor => BitConverter.UInt32BitsToSingle(DestXGlobalBits);
}
