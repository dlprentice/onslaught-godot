// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> leftover after the 148.0 fsub
/// window — <c>fld [esp+0x14]</c> / <c>fsub [0x005D85CC]</c>
/// (10.0) / <c>fstp [esp]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para><b>Sibling.</b> 148.0 fsub at <c>0x00460B66</c> is already
/// <see cref="RetailLevelSelectFsub148"/>. Sliding-borders call at
/// <c>0x00460B61</c> is already
/// <see cref="RetailLevelSelectSlidingBorders"/>. Latch-to-button
/// SET at <c>0x0042D5CF</c> is already
/// <see cref="OnslaughtRebuild.Client.RetailFrontendLatchToButton"/>.
/// FMV skip OR at <c>0x0053F2EB</c> already owns the three latch
/// dwords. Expanded list click / hover / colour / dest Y / dest X /
/// panel dest / cancel / click-hit sound stay on their leftovers.
/// This leftover is the later Render <c>[esp+0x14] - 10.0</c>
/// store. Do not redo those. Do not invent dest Y as 5, 15.5, 268,
/// 284, or 304. Do not invent dest X as 322.5 or dest from 148.0.
/// Do not invent dest from 10.0. Do not invent dest as the 2.0
/// constant.</para>
///
/// <para><b>Delta.</b> Official 74154bfa independently re-read:
/// <c>0x00460C90</c> is <c>fld [esp+0x14]</c>
/// (<c>d9 44 24 14</c>). <c>0x00460C94</c> is
/// <c>fsub [0x005D85CC]</c> (<c>d8 25 cc 85 5d 00</c>).
/// <c>0x005D85CC</c> is <c>00 00 20 41</c> (10.0). The prior leftover
/// labelled this second opcode <c>fld</c>; the bytes are
/// <c>d8 /4</c> with ModR/M <c>25</c> — <c>fsub m32</c> — not
/// <c>d9 05</c> <c>fld m32</c>. The same pair also sits at
/// <c>0x004721D7</c>; that is not this leftover. Dest does not.
/// Colour leftover already consults currentIndex. Hover leftover
/// already owns <c>0x004A3FA6</c>. Click leftover already owns
/// <c>0x004A4010</c>. Cancel leftover already owns
/// <c>0x004A4059</c>. Click-hit sound leftover already owns
/// <c>0x004A403C</c>. Nearby 15.5, 322.5, 148.0, and 10.0 are
/// measurements, not dest. Dest is not the 2.0 constant.</para>
///
/// <para><b>First consumer.</b> <c>0x00460CE7</c> is
/// <c>fstp [esp]</c> (<c>d9 1c 24</c>) immediately before
/// <c>0x00460CEA</c> <c>call 0x005563D0</c>
/// (<c>CDXSurf__RenderSurface</c>, <c>add esp, 0x2C</c>). That
/// overwrites the last push with <c>pad - 10.0</c>. Settled pad is
/// 148.0 so settled delta is 138.0. That is not dest. The nearby
/// <c>push 0x43A10000</c> (322.0) at <c>0x00460CE1</c> and
/// <c>push 0x3F747AE1</c> at <c>0x00460CDC</c> are later. The later
/// triple at <c>0x00460E24</c> is later. Texture load
/// <c>mov ecx, [0x0089D888]</c> at <c>0x00460CD5</c> is later.
/// DrawLevelSelect already owns the measured node centres. Do not
/// invent dest from 10.0.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// expanded list click, expanded list cancel, click-hit sound,
/// latch-to-button SET, the sliding-borders call, the 148.0 fsub,
/// and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailLevelSelectFsub10
{
    /// <summary><c>CFEPLevelSelect::Render</c> at <c>0x00460B40</c>.</summary>
    public const uint RenderSite = 0x00460B40u;

    /// <summary>Already <see cref="RetailLevelSelectSlidingBorders.CallSite"/>.</summary>
    public const uint SlidingCallSite = 0x00460B61u;

    /// <summary>Already <see cref="RetailLevelSelectFsub148.FldSite"/>.</summary>
    public const uint Fsub148Site = 0x00460B66u;

    /// <summary><c>fld [esp+0x14]</c> at <c>0x00460C90</c>. Reloads the pad.</summary>
    public const uint FldSite = 0x00460C90u;

    /// <summary>Stack local that holds the pad. Same slot as the 148.0 fsub.</summary>
    public const int StackLocal = 0x14;

    /// <summary><c>fsub [0x005D85CC]</c> at <c>0x00460C94</c>. First leftover after the window.</summary>
    public const uint FsubSite = 0x00460C94u;

    /// <summary>10.0 source. Same dword as <see cref="RetailLevelSelectFsub148.LaterTenConst"/>.</summary>
    public const uint TenConst = 0x005D85CCu;

    /// <summary>IEEE bits at <see cref="TenConst"/>. 10.0. Not dest.</summary>
    public const uint TenBits = 0x41200000u;

    /// <summary><c>fstp [esp]</c> at <c>0x00460CE7</c>. First consumer. Not dest.</summary>
    public const uint FstpSite = 0x00460CE7u;

    /// <summary><c>call 0x005563D0</c> at <c>0x00460CEA</c>.</summary>
    public const uint CallSite = 0x00460CEAu;

    /// <summary><c>CDXSurf__RenderSurface</c>. Same callee as the writing-chrome tiles.</summary>
    public const uint RenderSurface = 0x005563D0u;

    /// <summary><c>mov ecx, [0x0089D888]</c> at <c>0x00460CD5</c>. Later. Not dest.</summary>
    public const uint TextureLoadSite = 0x00460CD5u;

    /// <summary>Texture global loaded for the submit. Later. Not dest.</summary>
    public const uint TextureGlobal = 0x0089D888u;

    /// <summary><c>push 0x43A10000</c> at <c>0x00460CE1</c>. Later. Not dest.</summary>
    public const uint Later322PushSite = 0x00460CE1u;

    /// <summary>IEEE bits of the later 322.0 push. Not this leftover and not dest.</summary>
    public const uint Later322Bits = 0x43A10000u;

    /// <summary><c>push 0x3F747AE1</c> at <c>0x00460CDC</c>. Later. Not dest.</summary>
    public const uint LaterZPushSite = 0x00460CDCu;

    /// <summary>IEEE bits of the later 0.955 push. Not this leftover and not dest.</summary>
    public const uint LaterZBits = 0x3F747AE1u;

    /// <summary>Later identical 148.0 triple at <c>0x00460E24</c>. Not this leftover.</summary>
    public const uint LaterTripleSite = 0x00460E24u;

    /// <summary>Other <c>fld [esp+0x14]</c> / <c>fsub 10.0</c> pair at <c>0x004721D7</c>.</summary>
    public const uint OtherPairSite = 0x004721D7u;

    /// <summary>Latch SET at <c>0x0042D5CF</c> is already owned.</summary>
    public const uint LatchSetSite = 0x0042D5CFu;

    /// <summary>FMV skip OR at <c>0x0053F2EB</c> is already owned.</summary>
    public const uint FmvOrSite = 0x0053F2EBu;

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

    /// <summary>The 2.0 pad is not dest or this leftover.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>148.0 is the earlier offset, not dest.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>10.0 is the subtract, not dest.</summary>
    public const bool InventsDestFrom10 = false;

    /// <summary>Settled 138.0 is pad-minus-ten, not dest.</summary>
    public const bool InventsDestFrom138 = false;

    /// <summary>322.0 is a later push, not dest.</summary>
    public const bool InventsDestFrom322 = false;

    /// <summary>610.0 is the earlier window high, not dest.</summary>
    public const bool InventsDestFrom610 = false;

    /// <summary>The leftover does not invent dest immediates.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>The leftover does not write currentIndex.</summary>
    public const bool UsesCurrentIndex = false;

    /// <summary>0x00460C94 is this leftover.</summary>
    public const bool IsFsub10 = true;

    /// <summary>0x00460C94 is <c>fsub</c>, not <c>fld</c>.</summary>
    public const bool IsFld10 = false;

    /// <summary>0x00460B66 is already the 148.0 fsub.</summary>
    public const bool IsFsub148 = false;

    /// <summary>0x00460B61 is already the sliding-borders call.</summary>
    public const bool IsSlidingBordersCall = false;

    /// <summary>0x0042D5CF is already latch SET.</summary>
    public const bool IsLatchSet = false;

    /// <summary>0x0053F2EB is already FMV skip OR.</summary>
    public const bool IsFmvSkip = false;

    /// <summary>0x004A403C is already click-hit sound.</summary>
    public const bool IsClickSound = false;

    /// <summary>0x004A4010 is already click hit.</summary>
    public const bool IsClickHit = false;

    /// <summary>0x004A3FA6 is already hover hit.</summary>
    public const bool IsHoverHit = false;

    /// <summary>0x004A4059 is already cancel.</summary>
    public const bool IsCancel = false;

    /// <summary>The leftover does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render leftover is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x004A3394 is already menu-item dest.</summary>
    public const bool RedoesMenuItemDest = false;

    /// <summary>0x004A3301 is already icon dest.</summary>
    public const bool RedoesMenuItemIconDest = false;

    /// <summary>0x004A3D19 is already dropdown dest.</summary>
    public const bool RedoesDropdownDest = false;

    /// <summary>0x004A40B4 is already collapsed value dest.</summary>
    public const bool RedoesDropdownValueDest = false;

    /// <summary>0x004A3FCD is already expanded list dest X.</summary>
    public const bool RedoesDropdownListDest = false;

    /// <summary>0x004A3F36 is already expanded panel dest.</summary>
    public const bool RedoesDropdownPanelDest = false;

    /// <summary>0x004A3F3C is already expanded list dest Y.</summary>
    public const bool RedoesDropdownListDestY = false;

    /// <summary>0x004A3F6C is already expanded list colour.</summary>
    public const bool RedoesDropdownListColor = false;

    /// <summary>0x004A3FA6 is already expanded list hover.</summary>
    public const bool RedoesDropdownListHover = false;

    /// <summary>0x004A4010 is already expanded list click.</summary>
    public const bool RedoesDropdownListClick = false;

    /// <summary>0x004A4059 is already expanded list cancel.</summary>
    public const bool RedoesDropdownListCancel = false;

    /// <summary>0x004A403C is already click-hit sound.</summary>
    public const bool RedoesDropdownListClickSound = false;

    /// <summary>0x0042D5CF is already latch SET.</summary>
    public const bool RedoesLatchToButton = false;

    /// <summary>0x00460B61 is already the sliding-borders call.</summary>
    public const bool RedoesSlidingBorders = false;

    /// <summary>0x00460B66 is already the 148.0 fsub.</summary>
    public const bool RedoesFsub148 = false;

    /// <summary>0x00463647 is already language pitch.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in LevelSelect.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="TenBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Ten => BitConverter.UInt32BitsToSingle(TenBits);

    /// <summary>
    /// <c>[esp+0x14] - 10.0</c> stored at <c>[esp]</c> before the
    /// <c>CDXSurf__RenderSurface</c> call. That is not dest.
    /// </summary>
    public static float Delta(float pad) => pad - Ten;
}
