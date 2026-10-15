// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> leftover after the later 60.0 /
/// 0.5 / 320.0 scale — <c>fld [esp+0x94]</c> /
/// <c>fcomp [0x005D8568]</c> (1.0) / <c>fnstsw ax</c> /
/// <c>test ah, 0x40</c> / <c>jz</c> / first equal-1 consumer
/// <c>fld [esp+0x18]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para><b>Sibling.</b> Later 60.0 / 0.5 / 320.0 scale at
/// <c>0x00460F73</c> is already
/// <see cref="RetailLevelSelectLater60"/>. Later 610.0 / 0.0 pair at
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
/// This leftover is the later Render equal-1 compare of
/// <c>[esp+0x94]</c>. Do not redo those. Do not invent dest Y as
/// 5, 15.5, 268, 284, or 304. Do not invent dest X as 322.5 or
/// dest from 148.0. Do not invent dest from 1.0. Do not invent
/// dest from 20.0. Do not invent dest from 60.0. Do not invent
/// dest from 0.5. Do not invent dest from 320.0. Do not invent
/// dest from 610.0. Do not invent dest from 0.0. Do not invent
/// dest as the 2.0 constant.</para>
///
/// <para><b>Equal-1 compare.</b> Official 74154bfa independently
/// re-read: <c>0x00460F85</c> is already the later-60 first store
/// <c>fstp [esp+0x18]</c> (<c>d9 5c 24 18</c>).
/// <c>0x00460FD2</c> is <c>jz 0x00461069</c>
/// (<c>0f 84 91 00 00 00</c>). <c>0x00460FD8</c> is
/// <c>fld [esp+0x94]</c> (<c>d9 84 24 94 00 00 00</c>),
/// not <c>0x00460FD6</c>. <c>0x00460FDF</c> is
/// <c>fcomp [0x005D8568]</c> (<c>d8 1d 68 85 5d 00</c>).
/// <c>0x005D8568</c> is <c>00 00 80 3f</c> (1.0).
/// <c>0x00460FE5</c> is <c>fnstsw ax</c> / <c>test ah, 0x40</c>
/// / <c>jz 0x00461069</c> — skip when the local is not equal to
/// 1.0. Dest does not. Colour leftover already consults
/// currentIndex. Hover leftover already owns <c>0x004A3FA6</c>.
/// Click leftover already owns <c>0x004A4010</c>. Cancel leftover
/// already owns <c>0x004A4059</c>. Click-hit sound leftover
/// already owns <c>0x004A403C</c>. Nearby 15.5, 322.5, 148.0,
/// 0.75, 4.0, 1.0, 255.0, 60.0, 0.5, 320.0, and 20.0 are
/// measurements, not dest. Dest is not the 2.0 constant.</para>
///
/// <para><b>First consumer.</b> The equal-1 fall-through first
/// consumer is <c>0x00460FEC</c> <c>fld [esp+0x18]</c>
/// (<c>d9 44 24 18</c>). That is a load of the later-60 scaled
/// local, not dest. The later <c>fadd [0x005D857C]</c> (20.0)
/// at <c>0x00460FF0</c> is later. DrawLevelSelect already owns
/// the measured node centres. Do not invent dest from 1.0. Do
/// not invent dest from 20.0. Do not invent a fade.</para>
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
/// / 0.0 pair, the later 60.0 scale, and the 0x00463669 compare
/// stay untouched.</para>
/// </summary>
public static class RetailLevelSelectLaterEsp94One
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

    /// <summary>Already <see cref="RetailLevelSelectLater60.FstpSite"/>.</summary>
    public const uint Later60FstpSite = 0x00460F85u;

    /// <summary>Prior <c>jz 0x00461069</c> at <c>0x00460FD2</c>. Skips this leftover. Not dest.</summary>
    public const uint PriorJzSite = 0x00460FD2u;

    /// <summary><c>fld [esp+0x94]</c> at <c>0x00460FD8</c>. Later leftover after the later 60.0 scale. Not <c>0x00460FD6</c>.</summary>
    public const uint FldSite = 0x00460FD8u;

    /// <summary>Stack local compared against 1.0. Same slot as <see cref="RetailLevelSelectLaterEsp94.StackLocal"/>. Not dest.</summary>
    public const int StackLocal = 0x94;

    /// <summary><c>fcomp [0x005D8568]</c> at <c>0x00460FDF</c>.</summary>
    public const uint FcompSite = 0x00460FDFu;

    /// <summary>1.0 source. Equal compare, not dest. Same dword as <see cref="RetailLevelSelectLaterOne.OneConst"/>.</summary>
    public const uint OneConst = 0x005D8568u;

    /// <summary>IEEE bits at <see cref="OneConst"/>. 1.0. Not dest.</summary>
    public const uint OneBits = 0x3F800000u;

    /// <summary><c>fld [esp+0x18]</c> at <c>0x00460FEC</c>. First equal-1 consumer. Not dest.</summary>
    public const uint FirstConsumerSite = 0x00460FECu;

    /// <summary>Stack local loaded on the equal-1 fall-through. Same slot as <see cref="RetailLevelSelectLater60.StoreLocal"/>. Not dest.</summary>
    public const int ConsumerLocal = 0x18;

    /// <summary>Later <c>fadd [0x005D857C]</c> (20.0) at <c>0x00460FF0</c>. Not this leftover.</summary>
    public const uint LaterFadd20Site = 0x00460FF0u;

    /// <summary>20.0 source used later. Not this leftover and not dest.</summary>
    public const uint Later20Const = 0x005D857Cu;

    /// <summary>IEEE bits at <see cref="Later20Const"/>. 20.0. Not dest.</summary>
    public const uint Later20Bits = 0x41A00000u;

    /// <summary>Not-equal / prior-jz join at <c>0x00461069</c>. Not this leftover's first consumer.</summary>
    public const uint NotEqualSite = 0x00461069u;

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

    /// <summary>1.0 is the equal compare, not dest.</summary>
    public const bool InventsDestFrom1 = false;

    /// <summary>255.0 is the earlier scale, not dest.</summary>
    public const bool InventsDestFrom255 = false;

    /// <summary>0.0 is the earlier window low, not dest.</summary>
    public const bool InventsDestFrom0 = false;

    /// <summary>60.0 is the earlier factor, not dest.</summary>
    public const bool InventsDestFrom60 = false;

    /// <summary>0.5 is the earlier second factor, not dest.</summary>
    public const bool InventsDestFromHalf = false;

    /// <summary>320.0 is the earlier addend, not dest.</summary>
    public const bool InventsDestFrom320 = false;

    /// <summary>20.0 is the later addend, not dest.</summary>
    public const bool InventsDestFrom20 = false;

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

    /// <summary>0x00460FD8 is this leftover.</summary>
    public const bool IsLaterEsp94One = true;

    /// <summary>0x00460F73 is already the later 60.0 scale.</summary>
    public const bool IsLater60 = false;

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

    /// <summary>0x00460F73 is already the later 60.0 scale.</summary>
    public const bool RedoesLater60 = false;

    /// <summary>0x00463647 is already language pitch.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in LevelSelect.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="OneBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float CompareOne => BitConverter.UInt32BitsToSingle(OneBits);

    /// <summary><see cref="Later20Bits"/> decoded as IEEE-754 single. Later, not dest.</summary>
    public static float Later20 => BitConverter.UInt32BitsToSingle(Later20Bits);

    /// <summary>
    /// Later consumers of <c>[esp+0x94]</c> after
    /// <c>fcomp 1.0</c> / <c>test ah, 0x40</c>:
    /// <c>local == 1.0</c>. That is not dest from 1.0 or 20.0.
    /// </summary>
    public static bool Applies(float local) => local == CompareOne;
}
