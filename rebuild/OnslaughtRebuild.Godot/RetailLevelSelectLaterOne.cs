// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> leftover after the later
/// <c>[esp+0x94]</c> shift — <c>fcom [0x005D8568]</c> (1.0) /
/// <c>fmul [0x005D8C70]</c> (255.0) — recovered from official
/// 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop.
///
/// <para><b>Sibling.</b> Later <c>[esp+0x94]</c> shift at
/// <c>0x00460E34</c> is already
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
/// This leftover is the later Render 1.0 high-compare and 255.0
/// scale of the already-shifted local. Do not redo those. Do not
/// invent dest Y as 5, 15.5, 268, 284, or 304. Do not invent dest
/// X as 322.5 or dest from 148.0. Do not invent dest from 1.0. Do
/// not invent dest from 255.0. Do not invent dest from 0.75. Do
/// not invent dest from 4.0. Do not invent dest as the 2.0
/// constant. Do not invent a fade.</para>
///
/// <para><b>High compare.</b> Official 74154bfa independently
/// re-read: <c>0x00460E4D</c> is already the later-esp94 first
/// store consumer <c>fst [esp+0x40]</c>
/// (<c>d9 54 24 40</c>). <c>0x00460E62</c> is
/// <c>fcom [0x005D8568]</c> (<c>d8 15 68 85 5d 00</c>).
/// <c>0x005D8568</c> is <c>00 00 80 3f</c> (1.0).
/// <c>0x00460E68</c> is <c>fnstsw ax</c> / <c>test ah, 0x41</c>
/// / <c>jnz 0x00460E77</c> — keep ST when it is not above 1.0.
/// <c>0x00460E71</c> is <c>fld [0x005D8568]</c> after
/// <c>fstp</c> when ST is above 1.0. Dest does not. Colour
/// leftover already consults currentIndex. Hover leftover already
/// owns <c>0x004A3FA6</c>. Click leftover already owns
/// <c>0x004A4010</c>. Cancel leftover already owns
/// <c>0x004A4059</c>. Click-hit sound leftover already owns
/// <c>0x004A403C</c>. Nearby 15.5, 322.5, 148.0, 0.75, 4.0, 1.0,
/// and 255.0 are measurements, not dest. Dest is not the 2.0
/// constant.</para>
///
/// <para><b>Scale.</b> Both the high-clamp join and the below-zero
/// skip-around at <c>0x00460E5A</c> <c>fld [0x005D856C]</c> (0.0)
/// fall into <c>0x00460E77</c> <c>fmul [0x005D8C70]</c>
/// (<c>d8 0d 70 8c 5d 00</c>). <c>0x005D8C70</c> is
/// <c>00 00 7f 43</c> (255.0). First store consumer is
/// <c>0x00460E7F</c> <c>fistp [esp+0x4C]</c>
/// (<c>df 7c 24 4c</c>), then <c>mov [esp+0x1C], eax</c>. That
/// is a store of the scaled local, not dest. The earlier
/// <c>fmul [0x005D8C70]</c> at <c>0x00460B8D</c> is earlier and
/// is not this leftover. The later 610.0 / 0.0 pair at
/// <c>0x00460F30</c> is later. DrawLevelSelect already owns the
/// measured node centres. Do not invent dest from 1.0. Do not
/// invent dest from 255.0. Do not invent a fade.</para>
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
/// <c>[esp+0x94]</c> shift, and the 0x00463669 compare stay
/// untouched.</para>
/// </summary>
public static class RetailLevelSelectLaterOne
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

    /// <summary>Already <see cref="RetailLevelSelectLaterEsp94.FstSite"/>.</summary>
    public const uint LaterEsp94FstSite = 0x00460E4Du;

    /// <summary>Below-zero skip-around <c>fld [0x005D856C]</c> at <c>0x00460E5A</c>. Joins the 255.0 fmul. Not dest.</summary>
    public const uint ClampLowSite = 0x00460E5Au;

    /// <summary><c>fcom [0x005D8568]</c> at <c>0x00460E62</c>. Later leftover after the later [esp+0x94] shift.</summary>
    public const uint FcomSite = 0x00460E62u;

    /// <summary>1.0 source. High compare, not dest.</summary>
    public const uint OneConst = 0x005D8568u;

    /// <summary>IEEE bits at <see cref="OneConst"/>. 1.0. Not dest.</summary>
    public const uint OneBits = 0x3F800000u;

    /// <summary>Above-one reload <c>fld [0x005D8568]</c> at <c>0x00460E71</c>. Not dest.</summary>
    public const uint ClampHighSite = 0x00460E71u;

    /// <summary><c>fmul [0x005D8C70]</c> at <c>0x00460E77</c>.</summary>
    public const uint FmulSite = 0x00460E77u;

    /// <summary>255.0 source. Scale, not dest.</summary>
    public const uint ScaleConst = 0x005D8C70u;

    /// <summary>IEEE bits at <see cref="ScaleConst"/>. 255.0. Not dest.</summary>
    public const uint ScaleBits = 0x437F0000u;

    /// <summary><c>fistp [esp+0x4C]</c> at <c>0x00460E7F</c>. First store consumer. Not dest.</summary>
    public const uint FistpSite = 0x00460E7Fu;

    /// <summary>Stack local that receives the scaled integer. Not dest.</summary>
    public const int StoreLocal = 0x4C;

    /// <summary>Integer copy at <c>[esp+0x1C]</c>. Not dest.</summary>
    public const int IntLocal = 0x1C;

    /// <summary>0.0 source used by the below-zero skip-around. Same dword as <see cref="RetailLevelSelectLaterEsp94.ZeroConst"/>.</summary>
    public const uint ZeroConst = 0x005D856Cu;

    /// <summary>IEEE bits at <see cref="ZeroConst"/>. 0.0.</summary>
    public const uint ZeroBits = 0x00000000u;

    /// <summary>Earlier <c>fmul [0x005D8C70]</c> at <c>0x00460B8D</c>. Not this leftover.</summary>
    public const uint Earlier255Site = 0x00460B8Du;

    /// <summary>Later <c>fld [esp+0x14]</c> / <c>fcomp 610.0</c> at <c>0x00460F30</c>.</summary>
    public const uint Later610Site = 0x00460F30u;

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

    /// <summary>1.0 is the high compare, not dest.</summary>
    public const bool InventsDestFrom1 = false;

    /// <summary>255.0 is the scale, not dest.</summary>
    public const bool InventsDestFrom255 = false;

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

    /// <summary>0x00460E62 is this leftover.</summary>
    public const bool IsLaterOne = true;

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

    /// <summary><see cref="ScaleBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Scale => BitConverter.UInt32BitsToSingle(ScaleBits);

    /// <summary><see cref="ZeroBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float CompareZero => BitConverter.UInt32BitsToSingle(ZeroBits);

    /// <summary>
    /// High-compare keep when the shifted local is not above 1.0.
    /// That is not dest from 1.0.
    /// </summary>
    public static bool AboveOne(float shifted) => shifted > CompareOne;

    /// <summary>
    /// Below-zero skip-around plus the 1.0 high clamp before the
    /// 255.0 multiply. That is not dest and not a fade.
    /// </summary>
    public static float Clamped(float shifted)
    {
        if (shifted < CompareZero)
        {
            return CompareZero;
        }

        if (AboveOne(shifted))
        {
            return CompareOne;
        }

        return shifted;
    }

    /// <summary>
    /// <c>clamp(shifted, 0.0, 1.0) * 255.0</c> before
    /// <c>fistp [esp+0x4C]</c>. That is not dest from 1.0 or 255.0.
    /// </summary>
    public static float Scaled(float shifted) => Clamped(shifted) * Scale;
}
