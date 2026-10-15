// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render language-row fall-through increment —
/// recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Slot init at <c>0x00462E96</c> /
/// <c>0x00462EF5</c> is already <see cref="RetailMainMenuRowY"/>.
/// Dest Y at <c>0x0046309D</c> is already
/// <see cref="RetailMainMenuLabelDest"/>. Label
/// DrawTextDynamic at <c>0x0046316F</c> is already
/// <see cref="RetailMainMenuLabelText"/> and is the
/// <c>jmp 0x0046364D</c> arm after <c>fadd [0x005D857C]</c>
/// (20.0). Language hover centres at 268 / 304 are already
/// <see cref="RetailMainMenuHitTest"/>. This leftover is
/// the language fall-through that reaches
/// <c>0x00463643</c>. Do not redo those. Do not invent
/// dest Y as 268, 284, or 304.</para>
///
/// <para><b>Reach.</b> Official 74154bfa independently
/// re-read: <c>0x004634EE</c> is
/// <c>jne 0x00463643</c>. After
/// <c>CDXSurf__RenderSurface</c> at <c>0x0046363B</c>,
/// <c>0x00463640</c> is <c>add esp, 0x58</c> and
/// falls through into the same site. Labels never land
/// here: they <c>jmp 0x0046364D</c> from
/// <c>0x0046318C</c>.</para>
///
/// <para><b>Add.</b> <c>0x00463643</c> is
/// <c>fld [esp+0x10]</c>. <c>0x00463647</c> is
/// <c>fadd [0x005DB5D8]</c> (36.0). The shared tail at
/// <c>0x0046364D</c> is <c>mov ebx, [esp+0x20]</c>,
/// then <c>0x00463653</c> <c>fstp [esp+0x10]</c>,
/// then <c>0x00463657</c> <c>inc ebx</c>. Cold language
/// 268.0 plus 36.0 is 304.0, which is
/// <see cref="RetailMainMenuRowY.NonzeroSlotY"/>.</para>
///
/// <para><b>Not dest.</b> Nearby
/// <c>push 0x438E0000</c> at <c>0x00463636</c> is 284.0
/// and is not this leftover. Dest Y stays the dest
/// leftover: row-Y slot minus integer-half cy.
/// DrawMainMenu keeps dest Y as <c>rowY - 8</c> and
/// regular rows at NonzeroSlotY. The 2px MeasureText
/// residual is width, not this add. Do not invent dest
/// from 36.0. Label pitch 20.0 is already
/// <see cref="RetailMainMenuLabelText"/>.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates,
/// a wrap width, or a 2px kerning hack. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// Apply, dropdown, the colour AND, the writing-chrome Y,
/// the writing-chrome colour, the writing-chrome Z/X, the
/// sine pin, the blink, the chevron colour, the label
/// colour, the label dest, the label DrawTextDynamic, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font
/// slot, the version post-draw flags, the version pre-draw
/// enable, the version widen, the version tail, the
/// title-logo shadow dest/Z, the title-logo body dest/Z,
/// the selected-row icon colours, and the 0x00463873 /
/// 0x004638B7 / 0x00463A8F / 0x00463AD3 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay
/// untouched.</para>
/// </summary>
public static class RetailMainMenuLanguagePitch
{
    /// <summary><c>fld [esp+0x10]</c>.</summary>
    public const uint FldSite = 0x00463643u;

    /// <summary><c>fadd [0x005DB5D8]</c>.</summary>
    public const uint AddSite = 0x00463647u;

    /// <summary>36.0 source. Same dword as <see cref="RetailMainMenuRowY.SkippedPitchGlobal"/>.</summary>
    public const uint PitchGlobal = 0x005DB5D8u;

    /// <summary>IEEE bits at <see cref="PitchGlobal"/>. 36.0.</summary>
    public const uint PitchBits = 0x42100000u;

    /// <summary>Shared tail. Labels <c>jmp</c> here after adding 20.0.</summary>
    public const uint SharedTailSite = 0x0046364Du;

    /// <summary><c>fstp [esp+0x10]</c> after the add.</summary>
    public const uint FstpSite = 0x00463653u;

    /// <summary><c>inc ebx</c> after the store.</summary>
    public const uint IncSite = 0x00463657u;

    /// <summary><c>jne 0x00463643</c>.</summary>
    public const uint ReachJneSite = 0x004634EEu;

    /// <summary>Direct language skip onto the fld.</summary>
    public const uint ReachJneTarget = 0x00463643u;

    /// <summary><c>call 0x005563D0</c> immediately before the fall-through.</summary>
    public const uint FallthroughCallSite = 0x0046363Bu;

    /// <summary><c>CDXSurf__RenderSurface</c>.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary><c>add esp, 0x58</c> then fall through into <see cref="FldSite"/>.</summary>
    public const uint FallthroughAddEspSite = 0x00463640u;

    /// <summary>Stack pop after the surface. Not dest.</summary>
    public const int FallthroughAddEspImmediate = 0x58;

    /// <summary><c>push 0x438E0000</c>. Nearby dest Y. Not this leftover.</summary>
    public const uint NearbyDestYPushSite = 0x00463636u;

    /// <summary>IEEE bits of the nearby dest Y push. 284.0.</summary>
    public const uint NearbyDestYBits = 0x438E0000u;

    /// <summary><c>jmp 0x0046364D</c> after the label 20.0 add.</summary>
    public const uint LabelJmpSite = 0x0046318Cu;

    /// <summary><c>fadd [0x005D857C]</c> on the label arm.</summary>
    public const uint LabelPitchAddSite = 0x00463178u;

    /// <summary>20.0 source. Already <see cref="RetailMainMenuLabelText"/>.</summary>
    public const uint LabelPitchGlobal = 0x005D857Cu;

    /// <summary>IEEE bits at <see cref="LabelPitchGlobal"/>. 20.0.</summary>
    public const uint LabelPitchBits = 0x41A00000u;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not the nearby 284 push.</summary>
    public const bool InventsDestY284 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>Dest is ebx/ecx. Do not invent a 219/268/284/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover add is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a label fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00462E96 is already <see cref="RetailMainMenuRowY"/>.</summary>
    public const bool RedoesRowY = false;

    /// <summary>0x0046309D is already <see cref="RetailMainMenuLabelDest"/>.</summary>
    public const bool RedoesLabelDest = false;

    /// <summary>0x0046316F is already <see cref="RetailMainMenuLabelText"/>.</summary>
    public const bool RedoesLabelText = false;

    /// <summary>Language hover is already <see cref="RetailMainMenuHitTest"/>.</summary>
    public const bool RedoesHitTest = false;

    /// <summary>0x0046319E is already <see cref="RetailMainMenuLanguageSine"/>.</summary>
    public const bool RedoesLanguageSine = false;

    /// <summary>Language blink is already <see cref="RetailMainMenuLanguageBlink"/>.</summary>
    public const bool RedoesLanguageBlink = false;

    /// <summary>0x004641ED is already <see cref="RetailMainMenuVersionOverlayTail"/>.</summary>
    public const bool RedoesVersionOverlayTail = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00462FF3 is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>The add is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="PitchBits"/> decoded as IEEE-754 single.</summary>
    public static float Pitch => BitConverter.UInt32BitsToSingle(PitchBits);

    /// <summary><see cref="LabelPitchBits"/> decoded as IEEE-754 single.</summary>
    public static float LabelPitch => BitConverter.UInt32BitsToSingle(LabelPitchBits);

    /// <summary><see cref="NearbyDestYBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float NearbyDestY => BitConverter.UInt32BitsToSingle(NearbyDestYBits);

    /// <summary>
    /// <c>[esp+0x10]</c> after the language add. Cold 268.0
    /// plus 36.0 is 304.0.
    /// </summary>
    public static float NextRegularSlotY =>
        RetailMainMenuRowY.LanguageSlotY + Pitch;

    /// <summary><c>[esp+0x10]</c> plus the 36.0 add.</summary>
    public static float NextSlotY(float slotY) => slotY + Pitch;
}
