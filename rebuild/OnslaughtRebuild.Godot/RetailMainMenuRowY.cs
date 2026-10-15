// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render row-Y slot init — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Dest Y at <c>0x0046309D</c> is already
/// <see cref="RetailMainMenuLabelDest"/>: <c>fsubr [esp+0x10]</c>
/// minus integer-half cy. Language hover centres at 268 / 304 are
/// already <see cref="RetailMainMenuHitTest"/>. Writing-chrome Y
/// says <c>0x00462E96</c> overwrites the same slot after the
/// three tiles. This leftover is the slot init that produces
/// <c>[esp+0x10]</c> and the starting index in ebp. Do not redo
/// those. Do not invent dest Y as 268 or 304.</para>
///
/// <para><b>Seed.</b> <c>0x00462E96</c> is
/// <c>mov [esp+0x10], 0x43860000</c> (268.0). Then
/// <c>0x00462EE7</c> is <c>mov eax, [0x0083D990]</c>,
/// <c>0x00462EEC</c> is <c>or ebp, -1</c>,
/// <c>0x00462EEF</c> is <c>test eax, eax</c>,
/// <c>0x00462EF1</c> is <c>je 0x00462EFD</c>. Zero keeps
/// 268.0 and index -1.</para>
///
/// <para><b>Nonzero.</b> <c>0x00462EF3</c> is
/// <c>xor ebp, ebp</c> and <c>0x00462EF5</c> is
/// <c>mov [esp+0x10], 0x43980000</c> (304.0). Regular
/// rows therefore start at index 0 / 304.0.
/// <c>0x0083D990</c> is uninitialised <c>.data</c>, so
/// the image-initial dword is 0 and cold Render keeps
/// 268.0 / index -1. This type does not invent a career
/// name for that dword.</para>
///
/// <para><b>Not dest.</b> Dest Y stays the dest leftover:
/// row-Y slot minus integer-half cy. DrawMainMenu keeps
/// dest Y as <c>rowY - 8</c> until cy is measured. The
/// 2px MeasureText residual is width, not this slot.
/// Nearby <c>fadd [0x005DB5D8]</c> (36.0) at
/// <c>0x00463647</c> is the fall-through increment
/// skipped by the label <c>jmp 0x0046364D</c>. Do not
/// invent dest from that 36.0. Label pitch 20.0 is
/// already <see cref="RetailMainMenuLabelText"/>.</para>
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
public static class RetailMainMenuRowY
{
    /// <summary><c>mov [esp+0x10], 0x43860000</c>.</summary>
    public const uint SeedSite = 0x00462E96u;

    /// <summary>IEEE bits stored at <see cref="SeedSite"/>. 268.0.</summary>
    public const uint LanguageSlotBits = 0x43860000u;

    /// <summary><c>mov eax, [0x0083D990]</c>.</summary>
    public const uint FlagLoadSite = 0x00462EE7u;

    /// <summary>Uninitialised <c>.data</c>. Same dword as hover.</summary>
    public const uint FlagGlobal = 0x0083D990u;

    /// <summary><c>or ebp, -1</c>.</summary>
    public const uint LanguageIndexSite = 0x00462EECu;

    /// <summary>Cold starting index. Language row.</summary>
    public const int LanguageIndex = -1;

    /// <summary><c>test eax, eax</c> after the flag load.</summary>
    public const uint FlagTestSite = 0x00462EEFu;

    /// <summary><c>je 0x00462EFD</c>. Zero keeps 268 / -1.</summary>
    public const uint FlagJeSite = 0x00462EF1u;

    /// <summary>Fall-through after the <c>je</c>.</summary>
    public const uint FlagJeTarget = 0x00462EFDu;

    /// <summary><c>xor ebp, ebp</c>.</summary>
    public const uint RegularIndexSite = 0x00462EF3u;

    /// <summary>Nonzero starting index. First regular row.</summary>
    public const int RegularIndex = 0;

    /// <summary><c>mov [esp+0x10], 0x43980000</c>.</summary>
    public const uint NonzeroSlotSite = 0x00462EF5u;

    /// <summary>IEEE bits stored at <see cref="NonzeroSlotSite"/>. 304.0.</summary>
    public const uint NonzeroSlotBits = 0x43980000u;

    /// <summary>Image-initial dword. Cold Render keeps 268 / -1.</summary>
    public const uint ImageInitialFlag = 0u;

    /// <summary><c>fadd [0x005DB5D8]</c> at <c>0x00463647</c>.</summary>
    public const uint SkippedPitchSite = 0x00463647u;

    /// <summary>36.0 source. Label <c>jmp 0x0046364D</c> skips it.</summary>
    public const uint SkippedPitchGlobal = 0x005DB5D8u;

    /// <summary>IEEE bits at <see cref="SkippedPitchGlobal"/>. 36.0.</summary>
    public const uint SkippedPitchBits = 0x42100000u;

    /// <summary>Dest Y is not a 268 push.</summary>
    public const bool InventsDestY268 = false;

    /// <summary>Dest Y is not a 304 push.</summary>
    public const bool InventsDestY304 = false;

    /// <summary>Dest is ebx/ecx. Do not invent a 219/268/304 push.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 36.0 fall-through is not dest.</summary>
    public const bool InventsSkippedPitchAsDest = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover slot is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a label fade.</summary>
    public const bool InventsFade = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x0046309D is already <see cref="RetailMainMenuLabelDest"/>.</summary>
    public const bool RedoesLabelDest = false;

    /// <summary>0x0046316F is already <see cref="RetailMainMenuLabelText"/>.</summary>
    public const bool RedoesLabelText = false;

    /// <summary>Language hover is already <see cref="RetailMainMenuHitTest"/>.</summary>
    public const bool RedoesHitTest = false;

    /// <summary>0x004641ED is already <see cref="RetailMainMenuVersionOverlayTail"/>.</summary>
    public const bool RedoesVersionOverlayTail = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00462FF3 is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits before it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>The slot is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="LanguageSlotBits"/> decoded as IEEE-754 single.</summary>
    public static float LanguageSlotY => BitConverter.UInt32BitsToSingle(LanguageSlotBits);

    /// <summary><see cref="NonzeroSlotBits"/> decoded as IEEE-754 single.</summary>
    public static float NonzeroSlotY => BitConverter.UInt32BitsToSingle(NonzeroSlotBits);

    /// <summary><see cref="SkippedPitchBits"/> decoded as IEEE-754 single.</summary>
    public static float SkippedPitch => BitConverter.UInt32BitsToSingle(SkippedPitchBits);

    /// <summary>
    /// <c>[esp+0x10]</c> after the flag test. Image-initial 0
    /// keeps 268.0. Nonzero overwrites 304.0.
    /// </summary>
    public static float SlotY(uint flag) =>
        flag == ImageInitialFlag ? LanguageSlotY : NonzeroSlotY;

    /// <summary>
    /// ebp after the flag test. Image-initial 0 keeps -1.
    /// Nonzero writes 0.
    /// </summary>
    public static int StartingIndex(uint flag) =>
        flag == ImageInitialFlag ? LanguageIndex : RegularIndex;
}
