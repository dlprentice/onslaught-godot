// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> leftover after the later 610.0 /
/// 0.0 pair — <c>fild [esp+0x3C]</c> /
/// <c>fmul [0x005DB538]</c> (60.0) / <c>fmul [0x005D85EC]</c>
/// (0.5) / <c>fadd [0x005DB3E8]</c> (320.0) /
/// <c>fstp [esp+0x18]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para><b>Sibling.</b> Later 610.0 / 0.0 pair at
/// <c>0x00460F30</c> is already
/// <see cref="RetailLevelSelectLater610"/>. Later 1.0 fcom at
/// <c>0x00460E62</c> is already
/// <see cref="RetailLevelSelectLaterOne"/>. Later
/// <c>[esp+0x94]</c> shift at <c>0x00460E34</c> is already
/// <see cref="RetailLevelSelectLaterEsp94"/>. Later 148.0 triple at
/// <c>0x00460E24</c> is already <see cref="RetailLevelSelectLater148"/>.
/// 10.0 fsub at <c>0x00460C94</c> is already
/// <see cref="RetailLevelSelectFsub10"/>. 148.0 fsub at
/// <c>0x00460B66</c> is already <see cref="RetailLevelSelectFsub148"/>.
/// Sliding-borders call at <c>0x00460B61</c> is already
/// <see cref="RetailLevelSelectSlidingBorders"/>. Latch-to-button
/// SET at <c>0x0042D5CF</c> is already
/// <see cref="OnslaughtRebuild.Client.RetailFrontendLatchToButton"/>.
/// FMV skip OR at <c>0x0053F2EB</c> already owns the three latch
/// dwords. Expanded list click / hover / colour / dest Y / dest X /
/// panel dest / cancel / click-hit sound stay on their leftovers.
/// This leftover is the later Render
/// <c>((eax - 1) * 60.0 * 0.5) + 320.0</c> store to
/// <c>[esp+0x18]</c>. Do not redo those. Do not invent dest Y as
/// 5, 15.5, 268, 284, or 304. Do not invent dest X as 322.5 or
/// dest from 148.0. Do not invent dest from 320.0. Do not invent
/// dest from 60.0. Do not invent dest from 0.5. Do not invent
/// dest from 610.0. Do not invent dest from 0.0. Do not invent
/// dest as the 2.0 constant.</para>
///
/// <para><b>Scale.</b> Official 74154bfa independently re-read:
/// <c>0x00460F5E</c> is already the later-610 first store
/// <c>mov [esp+0x28], 0</c> (<c>c7 44 24 28 00 00 00 00</c>).
/// <c>0x00460F6F</c> is <c>fild [esp+0x3C]</c>
/// (<c>db 44 24 3c</c>) of the <c>lea ecx, [eax-1]</c> local.
/// <c>0x00460F73</c> is <c>fmul [0x005DB538]</c>
/// (<c>d8 0d 38 b5 5d 00</c>). <c>0x005DB538</c> is
/// <c>00 00 70 42</c> (60.0). <c>0x00460F79</c> is
/// <c>fmul [0x005D85EC]</c> (<c>d8 0d ec 85 5d 00</c>).
/// <c>0x005D85EC</c> is <c>00 00 00 3f</c> (0.5).
/// <c>0x00460F7F</c> is <c>fadd [0x005DB3E8]</c>
/// (<c>d8 05 e8 b3 5d 00</c>). <c>0x005DB3E8</c> is
/// <c>00 00 a0 43</c> (320.0). First store is
/// <c>0x00460F85</c> <c>fstp [esp+0x18]</c>
/// (<c>d9 5c 24 18</c>). That is a store of the scaled local,
/// not dest. The later <c>fld [esp+0x94]</c> /
/// <c>fcomp 1.0</c> at <c>0x00460FD8</c> is later. The later
/// <c>fld [esp+0x18]</c> at <c>0x00460FEC</c> is later.
/// DrawLevelSelect already owns the measured node centres. Do
/// not invent dest from 60.0. Do not invent dest from 0.5. Do
/// not invent dest from 320.0. Do not invent a fade.</para>
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
/// the 10.0 fsub, the later 148.0 triple, the later
/// <c>[esp+0x94]</c> shift, the later 1.0 fcom, the later 610.0
/// / 0.0 pair, and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailLevelSelectLater60
{
    /// <summary><c>CFEPLevelSelect::Render</c> at <c>0x00460B40</c>.</summary>
    public const uint RenderSite = 0x00460B40u;

    /// <summary>Already <see cref="RetailLevelSelectSlidingBorders.CallSite"/>.</summary>
    public const uint SlidingCallSite = 0x00460B61u;

    /// <summary>Already <see cref="RetailLevelSelectFsub148.FldSite"/>.</summary>
    public const uint Fsub148Site = 0x00460B66u;

    /// <summary>Already <see cref="RetailLevelSelectFsub10.FsubSite"/>.</summary>
    public const uint Fsub10Site = 0x00460C94u;

    /// <summary>Already <see cref="RetailLevelSelectLater148.FldSite"/>.</summary>
    public const uint Later148Site = 0x00460E24u;

    /// <summary>Already <see cref="RetailLevelSelectLaterEsp94.FldSite"/>.</summary>
    public const uint LaterEsp94Site = 0x00460E34u;

    /// <summary>Already <see cref="RetailLevelSelectLaterOne.FcomSite"/>.</summary>
    public const uint LaterOneSite = 0x00460E62u;

    /// <summary>Already <see cref="RetailLevelSelectLater610.WindowHighFldSite"/>.</summary>
    public const uint Later610Site = 0x00460F30u;

    /// <summary>Already <see cref="RetailLevelSelectLater610.FirstStoreSite"/>.</summary>
    public const uint Later610StoreSite = 0x00460F5Eu;

    /// <summary><c>fild [esp+0x3C]</c> at <c>0x00460F6F</c>. Input is <c>eax - 1</c>. Not dest.</summary>
    public const uint FildSite = 0x00460F6Fu;

    /// <summary>Stack local that feeds the fild. Not dest.</summary>
    public const int FildLocal = 0x3C;

    /// <summary><c>fmul [0x005DB538]</c> at <c>0x00460F73</c>.</summary>
    public const uint Fmul60Site = 0x00460F73u;

    /// <summary>60.0 source. Factor, not dest.</summary>
    public const uint FactorConst = 0x005DB538u;

    /// <summary>IEEE bits at <see cref="FactorConst"/>. 60.0. Not dest.</summary>
    public const uint FactorBits = 0x42700000u;

    /// <summary><c>fmul [0x005D85EC]</c> at <c>0x00460F79</c>.</summary>
    public const uint FmulHalfSite = 0x00460F79u;

    /// <summary>0.5 source. Factor, not dest.</summary>
    public const uint HalfConst = 0x005D85ECu;

    /// <summary>IEEE bits at <see cref="HalfConst"/>. 0.5. Not dest.</summary>
    public const uint HalfBits = 0x3F000000u;

    /// <summary><c>fadd [0x005DB3E8]</c> at <c>0x00460F7F</c>.</summary>
    public const uint FaddSite = 0x00460F7Fu;

    /// <summary>320.0 source. Addend, not dest.</summary>
    public const uint AddendConst = 0x005DB3E8u;

    /// <summary>IEEE bits at <see cref="AddendConst"/>. 320.0. Not dest.</summary>
    public const uint AddendBits = 0x43A00000u;

    /// <summary><c>fstp [esp+0x18]</c> at <c>0x00460F85</c>. First store. Not dest.</summary>
    public const uint FstpSite = 0x00460F85u;

    /// <summary>Stack local that receives the scaled value. Not dest.</summary>
    public const int StoreLocal = 0x18;

    /// <summary>Later <c>fld [esp+0x94]</c> / <c>fcomp 1.0</c> at <c>0x00460FD8</c>. Not this leftover.</summary>
    public const uint LaterEsp94OneSite = 0x00460FD8u;

    /// <summary>Later <c>fld [esp+0x18]</c> at <c>0x00460FEC</c>. Not this leftover.</summary>
    public const uint LaterEsp18Site = 0x00460FECu;

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

    /// <summary>10.0 is the earlier subtract, not dest.</summary>
    public const bool InventsDestFrom10 = false;

    /// <summary>Settled 138.0 is pad-minus-ten, not dest.</summary>
    public const bool InventsDestFrom138 = false;

    /// <summary>322.0 is an earlier later-push, not dest.</summary>
    public const bool InventsDestFrom322 = false;

    /// <summary>610.0 is the earlier window high, not dest.</summary>
    public const bool InventsDestFrom610 = false;

    /// <summary>90.0 is the later-148 window low, not dest.</summary>
    public const bool InventsDestFrom90 = false;

    /// <summary>570.0 is the later-148 window high, not dest.</summary>
    public const bool InventsDestFrom570 = false;

    /// <summary>0.75 is the earlier subtrahend, not dest.</summary>
    public const bool InventsDestFrom075 = false;

    /// <summary>4.0 is the earlier factor, not dest.</summary>
    public const bool InventsDestFrom4 = false;

    /// <summary>1.0 is the earlier high compare, not dest.</summary>
    public const bool InventsDestFrom1 = false;

    /// <summary>255.0 is the earlier scale, not dest.</summary>
    public const bool InventsDestFrom255 = false;

    /// <summary>0.0 is the earlier window low, not dest.</summary>
    public const bool InventsDestFrom0 = false;

    /// <summary>60.0 is the factor, not dest.</summary>
    public const bool InventsDestFrom60 = false;

    /// <summary>0.5 is the second factor, not dest.</summary>
    public const bool InventsDestFromHalf = false;

    /// <summary>320.0 is the addend, not dest.</summary>
    public const bool InventsDestFrom320 = false;

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

    /// <summary>0x00460F73 is this leftover.</summary>
    public const bool IsLater60 = true;

    /// <summary>0x00460F30 is already the later 610.0 / 0.0 pair.</summary>
    public const bool IsLater610 = false;

    /// <summary>0x00460E62 is already the later 1.0 fcom.</summary>
    public const bool IsLaterOne = false;

    /// <summary>0x00460E34 is already the later [esp+0x94] shift.</summary>
    public const bool IsLaterEsp94 = false;

    /// <summary>0x00460E24 is already the later 148.0 triple.</summary>
    public const bool IsLater148 = false;

    /// <summary>0x00460B66 is already the 148.0 fsub.</summary>
    public const bool IsFsub148 = false;

    /// <summary>0x00460C94 is already the 10.0 fsub.</summary>
    public const bool IsFsub10 = false;

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

    /// <summary>0x00460C94 is already the 10.0 fsub.</summary>
    public const bool RedoesFsub10 = false;

    /// <summary>0x00460E24 is already the later 148.0 triple.</summary>
    public const bool RedoesLater148 = false;

    /// <summary>0x00460E34 is already the later [esp+0x94] shift.</summary>
    public const bool RedoesLaterEsp94 = false;

    /// <summary>0x00460E62 is already the later 1.0 fcom.</summary>
    public const bool RedoesLaterOne = false;

    /// <summary>0x00460F30 is already the later 610.0 / 0.0 pair.</summary>
    public const bool RedoesLater610 = false;

    /// <summary>0x00463647 is already language pitch.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in LevelSelect.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="FactorBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Factor => BitConverter.UInt32BitsToSingle(FactorBits);

    /// <summary><see cref="HalfBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Half => BitConverter.UInt32BitsToSingle(HalfBits);

    /// <summary><see cref="AddendBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Addend => BitConverter.UInt32BitsToSingle(AddendBits);

    /// <summary>
    /// <c>((eax - 1) * 60.0 * 0.5) + 320.0</c> before
    /// <c>fstp [esp+0x18]</c>. That is not dest from 60.0,
    /// 0.5, or 320.0.
    /// </summary>
    public static float Scaled(int eax) => ((eax - 1) * Factor * Half) + Addend;
}
