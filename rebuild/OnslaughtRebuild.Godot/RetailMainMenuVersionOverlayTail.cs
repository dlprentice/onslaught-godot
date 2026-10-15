// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay leftover stack into
/// CDXFont__DrawTextDynamic — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Format at <c>0x0046416E</c> and the settled
/// pack at <c>0x004641B1</c> / <c>0x004641B4</c> are already
/// <see cref="RetailMainMenuVersionOverlay"/>. Dest Y
/// (GetWindowHeight-16), dest X push 0, and Z 0.01 are already
/// <see cref="RetailMainMenuVersionOverlayZ"/>. Font slot 1 /
/// Font13PS is already
/// <see cref="RetailMainMenuVersionOverlayFont"/>. Post-draw
/// <c>[0x00679B40]=1</c> at <c>0x004641FC</c> is already
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. Pre-draw
/// <c>[0x00679B40]=0</c> at <c>0x00464180</c> is already
/// <see cref="RetailMainMenuVersionOverlayEnable"/>. The cdecl
/// widen at <c>0x00464191</c> is already
/// <see cref="RetailMainMenuVersionOverlayWiden"/> and does not
/// own these three pushes. This leftover is the leftover stack
/// that remains after <c>add esp, 4</c>. Do not redo the
/// sprintf, the colour OR, dest/Z, the font slot, the pre-draw
/// store, the post-draw stores, or the widen.</para>
///
/// <para><b>Slots.</b> After the enable-byte store,
/// <c>0x00464187</c> is <c>push 0</c>, <c>0x00464189</c> is
/// <c>push 0</c>, and <c>0x0046418B</c> is
/// <c>push 0x447A0000</c>. <c>add esp, 4</c> at
/// <c>0x004641A0</c> pops only the cdecl widen argument, so those
/// three dwords remain. Later pushes are dest X, dest Y, Z,
/// identity scales, colour, and the wide pointer. Right-to-left
/// that is ten stack args into <c>call 0x00465710</c> at
/// <c>0x004641ED</c>. The body exits <c>RET 0x28</c> at
/// <c>0x00465997</c>. The first leftover push is stack arg 10,
/// the second is arg 9, and the float is arg 8. Dest, Z, scale,
/// colour, and the wide pointer stay with their siblings.</para>
///
/// <para><b>Body.</b> Alloca is <c>0x2720</c>, then four
/// register pushes. Arg 8 is <c>fld [esp+0x2750]</c> at
/// <c>0x0046578F</c>, then <c>fcom</c> against image
/// <c>+0.0</c> / <c>+0.25</c> / <c>+0.5</c>. The leftover
/// float is past all three. Arg 9 is
/// <c>mov eax, [esp+0x2754]</c> at <c>0x0046587A</c>;
/// <c>test eax, eax</c> / <c>jz</c> skips that arm because
/// the leftover is 0. Arg 10 is
/// <c>mov edi, [esp+0x2758]</c> at <c>0x00465902</c>. The
/// <c>cmp ebx, 0x3E8</c> at <c>0x00465771</c>
/// (<c>81 fb e8 03 00 00</c>) is a wchar-count clamp, not this
/// leftover float. <c>0x00465777</c> is the separate following
/// <c>mov word [eax], 0</c> store (<c>66 c7 00 00 00</c>). GPL
/// <c>references/Onslaught/FrontEnd.cpp</c> line 1225 shows the
/// same <c>1000.f</c> as the eighth argument on the title-bar
/// path; that call's tenth argument is
/// <c>FONT_USE_Z_BUFFER</c>, and this leftover's tenth is 0.
/// This type does not invent dest, wrap, fade, scale, or sheen
/// from the leftover slots.</para>
///
/// <para><b>Not a fade.</b> The post-call
/// <c>fmul [0x005D8C70]</c> is already the colour-pack 255 scale
/// in <see cref="RetailMainMenuVersionOverlay"/>. The post-draw
/// <c>fcom [0.0]</c> is already the title-logo shadow clamp in
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. This type
/// does not invent a version fade, a sheen, dest immediates, a
/// wrap width, or a 2px kerning hack. DrawMainMenu keeps
/// title-font DrawText, VersionTint, Format, DestX,
/// DestY(DesignHeight), and scale 1.0.</para>
///
/// <para><b>Not dest/Z/font/flags/enable/widen.</b> Dest Y stays
/// helper minus 16. Dest X stays 0. Z stays 0.01. Font slot
/// stays 1 / Font13PS. Post-draw restore stays 1. Pre-draw store
/// stays 0. Widen stays cdecl one-arg. Not
/// <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, the writing-chrome Y,
/// the writing-chrome colour, the writing-chrome Z/X, the sine
/// pin, the blink, the chevron colour, the label colour, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font slot, the
/// version post-draw flags, the version pre-draw enable, the
/// version widen, the title-logo shadow dest/Z, the title-logo
/// body dest/Z, the selected-row icon colours, and the 0x00463873
/// / 0x004638B7 / 0x00463A8F / 0x00463AD3 / 0x00463D1F /
/// 0x00463D63 / 0x00463F3F / 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayTail
{
    /// <summary>Already <see cref="RetailMainMenuVersionOverlayEnable"/>.</summary>
    public const uint EnableSiblingSite = 0x00464180u;

    /// <summary><c>push 0</c>. DrawTextDynamic stack arg 10.</summary>
    public const uint FirstLeftoverPushSite = 0x00464187u;

    /// <summary>Second <c>push 0</c>. DrawTextDynamic stack arg 9.</summary>
    public const uint SecondLeftoverPushSite = 0x00464189u;

    /// <summary><c>push 0x447A0000</c>. DrawTextDynamic stack arg 8.</summary>
    public const uint FloatLeftoverPushSite = 0x0046418Bu;

    /// <summary>IEEE bits of stack arg 8. Not dest and not wrap.</summary>
    public const uint FloatSlotBits = 0x447A0000u;

    /// <summary>Already <see cref="RetailMainMenuVersionOverlayWiden"/>.</summary>
    public const uint WidenAddEspSite = 0x004641A0u;

    /// <summary>One cdecl dword popped. These three leftover pushes stay.</summary>
    public const int WidenAddEspImmediate = 4;

    /// <summary><c>call 0x00465710</c> at <c>0x004641ED</c>.</summary>
    public const uint CallSite = 0x004641EDu;

    /// <summary><c>CDXFont__DrawTextDynamic</c>.</summary>
    public const uint DrawTextDynamic = 0x00465710u;

    /// <summary><c>RET 0x28</c> at <c>0x00465997</c>.</summary>
    public const uint BodyRetSite = 0x00465997u;

    /// <summary>Ten stack dwords. Not <c>RET 0x2C</c>.</summary>
    public const int BodyRetImmediate = 0x28;

    /// <summary>Thiscall stack arity implied by <see cref="BodyRetImmediate"/>.</summary>
    public const int StackArgCount = 10;

    /// <summary>Alloca probe immediate at the DrawTextDynamic prologue.</summary>
    public const int AllocaSize = 0x2720;

    /// <summary><c>push ebx/ebp/esi/edi</c> after the this-save.</summary>
    public const int RegisterPushBytes = 0x10;

    /// <summary>One-based stack-arg index of the leftover float.</summary>
    public const int FloatStackArg = 8;

    /// <summary>One-based stack-arg index of the second leftover 0.</summary>
    public const int SecondStackArg = 9;

    /// <summary>One-based stack-arg index of the first leftover 0.</summary>
    public const int FirstStackArg = 10;

    /// <summary><c>fld [esp+0x2750]</c> at <c>0x0046578F</c>.</summary>
    public const uint Arg8LoadSite = 0x0046578Fu;

    /// <summary>Displacement after alloca and four register pushes.</summary>
    public const int Arg8Disp = 0x2750;

    /// <summary><c>mov eax, [esp+0x2754]</c> at <c>0x0046587A</c>.</summary>
    public const uint Arg9LoadSite = 0x0046587Au;

    /// <summary>Displacement of stack arg 9 after the frame.</summary>
    public const int Arg9Disp = 0x2754;

    /// <summary><c>mov edi, [esp+0x2758]</c> at <c>0x00465902</c>.</summary>
    public const uint Arg10LoadSite = 0x00465902u;

    /// <summary>Displacement of stack arg 10 after the frame.</summary>
    public const int Arg10Disp = 0x2758;

    /// <summary><c>fcom [0x005D856C]</c> at <c>0x00465796</c>.</summary>
    public const uint BelowZeroCompareSite = 0x00465796u;

    /// <summary>Image dword compared first. <c>+0.0f</c>.</summary>
    public const uint BelowZeroGlobal = 0x005D856Cu;

    /// <summary>IEEE bits at <see cref="BelowZeroGlobal"/>.</summary>
    public const uint BelowZeroBits = 0x00000000u;

    /// <summary><c>fcom [0x005D858C]</c> at <c>0x004657A7</c>.</summary>
    public const uint BelowQuarterCompareSite = 0x004657A7u;

    /// <summary>Image dword compared second. <c>+0.25f</c>.</summary>
    public const uint BelowQuarterGlobal = 0x005D858Cu;

    /// <summary>IEEE bits at <see cref="BelowQuarterGlobal"/>.</summary>
    public const uint BelowQuarterBits = 0x3E800000u;

    /// <summary><c>fcom [0x005D85EC]</c> at <c>0x004657DD</c>.</summary>
    public const uint BelowHalfCompareSite = 0x004657DDu;

    /// <summary>Image dword compared third. <c>+0.5f</c>.</summary>
    public const uint BelowHalfGlobal = 0x005D85ECu;

    /// <summary>IEEE bits at <see cref="BelowHalfGlobal"/>.</summary>
    public const uint BelowHalfBits = 0x3F000000u;

    /// <summary><c>test eax, eax</c> of stack arg 9 at <c>0x00465881</c>.</summary>
    public const uint Arg9TestSite = 0x00465881u;

    /// <summary>Immediate stored as stack arg 9. Skips that arm.</summary>
    public const int SecondSlot = 0;

    /// <summary>Immediate stored as stack arg 10. Not the title-bar flag.</summary>
    public const int FirstSlot = 0;

    /// <summary><c>cmp ebx, 0x3E8</c> at <c>0x00465771</c>. Wchar clamp.</summary>
    public const uint LengthClampSite = 0x00465771u;

    /// <summary><c>mov word [eax], 0</c>. Following store, not the clamp compare.</summary>
    public const uint LengthClampStoreSite = 0x00465777u;

    /// <summary>Wchar-count clamp. Not the leftover float.</summary>
    public const int LengthClampImmediate = 0x3E8;

    /// <summary>Already <see cref="RetailMainMenuVersionOverlayWiden"/>.</summary>
    public const uint WidenSiblingSite = 0x00464191u;

    /// <summary>This leftover owns the three leftover pushes as tail slots.</summary>
    public const bool OwnsLeftoverPushes = true;

    /// <summary>0x00464191 is already <see cref="RetailMainMenuVersionOverlayWiden"/>.</summary>
    public const bool OwnsWidenCall = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is tail slots, not a title-logo scale.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>Dest Y stays helper minus 16. Do not invent a 464 immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The leftover float is not a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a version fade.</summary>
    public const bool InventsFade = false;

    /// <summary>0x3E8 is a wchar clamp, not this leftover.</summary>
    public const bool InventsLengthClampAsWrap = false;

    /// <summary>The tenth leftover is 0, not the title-bar Z-buffer flag.</summary>
    public const bool InventsTitleBarZFlag = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x004641C9 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x004641E4 is already <see cref="RetailMainMenuVersionOverlayFont"/>.</summary>
    public const bool RedoesVersionOverlayFont = false;

    /// <summary>0x004641FC is already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const bool RedoesVersionOverlayFlags = false;

    /// <summary>0x00464180 is already <see cref="RetailMainMenuVersionOverlayEnable"/>.</summary>
    public const bool RedoesVersionOverlayEnable = false;

    /// <summary>0x00464191 is already <see cref="RetailMainMenuVersionOverlayWiden"/>.</summary>
    public const bool RedoesVersionOverlayWiden = false;

    /// <summary>0x004642CE is already <see cref="RetailMainMenuTitleLogoZ"/>.</summary>
    public const bool RedoesTitleLogoZ = false;

    /// <summary>0x00464251 is already <see cref="RetailMainMenuTitleLogoShadowZ"/>.</summary>
    public const bool RedoesTitleLogoShadowZ = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

    /// <summary>0x00462FED is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits after it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary><see cref="FloatSlotBits"/> decoded as IEEE-754 single.</summary>
    public static float FloatSlot => BitConverter.UInt32BitsToSingle(FloatSlotBits);

    /// <summary><see cref="BelowZeroBits"/> decoded as IEEE-754 single.</summary>
    public static float BelowZero => BitConverter.UInt32BitsToSingle(BelowZeroBits);

    /// <summary><see cref="BelowQuarterBits"/> decoded as IEEE-754 single.</summary>
    public static float BelowQuarter => BitConverter.UInt32BitsToSingle(BelowQuarterBits);

    /// <summary><see cref="BelowHalfBits"/> decoded as IEEE-754 single.</summary>
    public static float BelowHalf => BitConverter.UInt32BitsToSingle(BelowHalfBits);

    /// <summary>
    /// Post-alloca, post-register-push displacement of a one-based
    /// stack argument. Arg 8 is <see cref="Arg8Disp"/>.
    /// </summary>
    public static int StackDispAfterFrame(int oneBasedArg) =>
        AllocaSize + RegisterPushBytes + (4 * oneBasedArg);

    /// <summary>
    /// Whether leftover arg 8 takes the below-zero arm. The
    /// version overlay leftover does not.
    /// </summary>
    public static bool TakesBelowZeroArm(float leftover) => leftover < BelowZero;

    /// <summary>
    /// Whether leftover arg 8 takes the below-quarter arm. The
    /// version overlay leftover does not.
    /// </summary>
    public static bool TakesBelowQuarterArm(float leftover) => leftover < BelowQuarter;

    /// <summary>
    /// Whether leftover arg 8 takes the below-half arm. The
    /// version overlay leftover does not.
    /// </summary>
    public static bool TakesBelowHalfArm(float leftover) => leftover < BelowHalf;

    /// <summary>
    /// Whether leftover arg 9 is zero, matching the <c>jz</c>
    /// that skips that colour arm.
    /// </summary>
    public static bool SkipsArg9Arm(int leftover) => leftover == SecondSlot;
}
