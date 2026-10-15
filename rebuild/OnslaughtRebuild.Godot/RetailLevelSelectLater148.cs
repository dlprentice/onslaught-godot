// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> leftover after the 10.0 fsub —
/// <c>fld [0x005DB53C]</c> (148.0) / <c>fsub [esi+0x3460]</c> /
/// <c>fstp [esp+0x14]</c> — recovered from official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash. <c>FEPLevelSelect</c> is absent from the
/// pinned GPL drop. The identical triple has two image hits:
/// <c>0x00460B66</c> and this later site.
///
/// <para><b>Sibling.</b> 148.0 fsub at <c>0x00460B66</c> is already
/// <see cref="RetailLevelSelectFsub148"/>. 10.0 fsub at
/// <c>0x00460C94</c> is already <see cref="RetailLevelSelectFsub10"/>.
/// Sliding-borders call at <c>0x00460B61</c> is already
/// <see cref="RetailLevelSelectSlidingBorders"/>. Latch-to-button
/// SET at <c>0x0042D5CF</c> is already
/// <see cref="OnslaughtRebuild.Client.RetailFrontendLatchToButton"/>.
/// FMV skip OR at <c>0x0053F2EB</c> already owns the three latch
/// dwords. Expanded list click / hover / colour / dest Y / dest X /
/// panel dest / cancel / click-hit sound stay on their leftovers.
/// This leftover is the later Render <c>148.0 - [this+0x3460]</c>
/// store. Do not redo those. Do not invent dest Y as 5, 15.5, 268,
/// 284, or 304. Do not invent dest X as 322.5 or dest from 148.0.
/// Do not invent dest from 90.0. Do not invent dest from 570.0.
/// Do not invent dest as the 2.0 constant.</para>
///
/// <para><b>Pad.</b> Official 74154bfa independently re-read:
/// <c>0x00460E24</c> is <c>fld [0x005DB53C]</c>
/// (<c>d9 05 3c b5 5d 00</c>). <c>0x005DB53C</c> is
/// <c>00 00 14 43</c> (148.0). <c>0x00460E2A</c> is
/// <c>fsub [esi+0x3460]</c> (<c>d8 a6 60 34 00 00</c>).
/// <c>0x00460E30</c> is <c>fstp [esp+0x14]</c>
/// (<c>d9 5c 24 14</c>). Dest does not. Colour leftover already
/// consults currentIndex. Hover leftover already owns
/// <c>0x004A3FA6</c>. Click leftover already owns
/// <c>0x004A4010</c>. Cancel leftover already owns
/// <c>0x004A4059</c>. Click-hit sound leftover already owns
/// <c>0x004A403C</c>. Nearby 15.5, 322.5, 148.0, 90.0, and
/// 570.0 are measurements, not dest. Dest is not the 2.0
/// constant.</para>
///
/// <para><b>Window.</b> First consumers of <c>[esp+0x14]</c> are
/// <c>0x00460E99</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460E9D</c> <c>fcomp [0x00629390]</c> (90.0) and
/// <c>0x00460EAC</c> <c>fld [esp+0x14]</c> /
/// <c>0x00460EB0</c> <c>fcomp [0x00629394]</c> (570.0).
/// <c>test ah, 1</c> / <c>jnz</c> clears the flag when the pad
/// is below 90.0. <c>test ah, 0x41</c> / <c>jnz</c> keeps the
/// flag when the pad is not above 570.0. The flag is therefore
/// set when <c>90.0 &lt;= pad &lt;= 570.0</c>. That is not dest.
/// The later <c>[esp+0x94]</c> fsub at <c>0x00460E34</c> is
/// later. The later 610.0 / 0.0 pair at <c>0x00460F30</c> is
/// later. The other <c>fld [esp+0x14]</c> / <c>fsub 10.0</c>
/// pair at <c>0x004721D7</c> is later.</para>
///
/// <para><b>Settled field.</b> Same init as
/// <see cref="RetailLevelSelectFsub148"/>: settled field is 0.
/// Settled pad is 148.0. That pad sits inside this later window.
/// DrawLevelSelect already owns the measured node centres.
/// Do not invent dest from 148.0. Do not invent dest from 90.0.
/// Do not invent dest from 570.0.</para>
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
/// the 10.0 fsub, and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailLevelSelectLater148
{
    /// <summary><c>CFEPLevelSelect::Render</c> at <c>0x00460B40</c>.</summary>
    public const uint RenderSite = 0x00460B40u;

    /// <summary>Already <see cref="RetailLevelSelectSlidingBorders.CallSite"/>.</summary>
    public const uint SlidingCallSite = 0x00460B61u;

    /// <summary>Already <see cref="RetailLevelSelectFsub148.FldSite"/>.</summary>
    public const uint Fsub148Site = 0x00460B66u;

    /// <summary>Already <see cref="RetailLevelSelectFsub10.FsubSite"/>.</summary>
    public const uint Fsub10Site = 0x00460C94u;

    /// <summary><c>fld [0x005DB53C]</c> at <c>0x00460E24</c>. Later leftover after the 10.0 fsub.</summary>
    public const uint FldSite = 0x00460E24u;

    /// <summary>148.0 source. Same dword as <see cref="RetailLevelSelectFsub148.OffsetConst"/>.</summary>
    public const uint OffsetConst = 0x005DB53Cu;

    /// <summary>IEEE bits at <see cref="OffsetConst"/>. 148.0. Not dest.</summary>
    public const uint OffsetBits = 0x43140000u;

    /// <summary><c>fsub [esi+0x3460]</c> at <c>0x00460E2A</c>.</summary>
    public const uint FsubSite = 0x00460E2Au;

    /// <summary><c>this+0x3460</c>. Subtracted field, not dest.</summary>
    public const int FieldOffset = 0x3460;

    /// <summary><c>fstp [esp+0x14]</c> at <c>0x00460E30</c>.</summary>
    public const uint FstpSite = 0x00460E30u;

    /// <summary>Stack local that receives the pad. Not dest.</summary>
    public const int StackLocal = 0x14;

    /// <summary><c>fld [esp+0x14]</c> at <c>0x00460E99</c>. First consumer reload.</summary>
    public const uint WindowLowFldSite = 0x00460E99u;

    /// <summary><c>fcomp [0x00629390]</c> at <c>0x00460E9D</c>. First consumer.</summary>
    public const uint WindowLowFcompSite = 0x00460E9Du;

    /// <summary>90.0 source. Window low, not dest.</summary>
    public const uint WindowLowConst = 0x00629390u;

    /// <summary>IEEE bits at <see cref="WindowLowConst"/>. 90.0.</summary>
    public const uint WindowLowBits = 0x42B40000u;

    /// <summary><c>fld [esp+0x14]</c> at <c>0x00460EAC</c>.</summary>
    public const uint WindowHighFldSite = 0x00460EACu;

    /// <summary><c>fcomp [0x00629394]</c> at <c>0x00460EB0</c>. First consumer.</summary>
    public const uint WindowHighFcompSite = 0x00460EB0u;

    /// <summary>570.0 source. Window high, not dest.</summary>
    public const uint WindowHighConst = 0x00629394u;

    /// <summary>IEEE bits at <see cref="WindowHighConst"/>. 570.0.</summary>
    public const uint WindowHighBits = 0x440E8000u;

    /// <summary>Later <c>fld [esp+0x94]</c> at <c>0x00460E34</c>. Not this leftover.</summary>
    public const uint LaterEsp94Site = 0x00460E34u;

    /// <summary>Later <c>fld [esp+0x14]</c> / <c>fcomp 610.0</c> at <c>0x00460F30</c>.</summary>
    public const uint Later610Site = 0x00460F30u;

    /// <summary>Other <c>fld [esp+0x14]</c> / <c>fsub 10.0</c> pair at <c>0x004721D7</c>.</summary>
    public const uint OtherPairSite = 0x004721D7u;

    /// <summary>Latch SET at <c>0x0042D5CF</c> is already owned.</summary>
    public const uint LatchSetSite = 0x0042D5CFu;

    /// <summary>FMV skip OR at <c>0x0053F2EB</c> is already owned.</summary>
    public const uint FmvOrSite = 0x0053F2EBu;

    /// <summary>Init writes 0 into the subtracted field.</summary>
    public const float SettledField = 0f;

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

    /// <summary>148.0 is the offset, not dest.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>10.0 is the earlier subtract, not dest.</summary>
    public const bool InventsDestFrom10 = false;

    /// <summary>Settled 138.0 is pad-minus-ten, not dest.</summary>
    public const bool InventsDestFrom138 = false;

    /// <summary>322.0 is an earlier later-push, not dest.</summary>
    public const bool InventsDestFrom322 = false;

    /// <summary>610.0 is the earlier window high, not dest.</summary>
    public const bool InventsDestFrom610 = false;

    /// <summary>90.0 is this window low, not dest.</summary>
    public const bool InventsDestFrom90 = false;

    /// <summary>570.0 is this window high, not dest.</summary>
    public const bool InventsDestFrom570 = false;

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

    /// <summary>0x00460E24 is this leftover.</summary>
    public const bool IsLater148 = true;

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

    /// <summary>0x00463647 is already language pitch.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in LevelSelect.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="OffsetBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float Offset => BitConverter.UInt32BitsToSingle(OffsetBits);

    /// <summary><see cref="WindowLowBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float WindowLow => BitConverter.UInt32BitsToSingle(WindowLowBits);

    /// <summary><see cref="WindowHighBits"/> decoded as IEEE-754 single. Not dest.</summary>
    public static float WindowHigh => BitConverter.UInt32BitsToSingle(WindowHighBits);

    /// <summary>Settled <c>148.0 - 0</c>. Window input, not dest.</summary>
    public static float SettledPad => Pad(SettledField);

    /// <summary>
    /// <c>148.0 - [this+0x3460]</c> stored at <c>[esp+0x14]</c>.
    /// That is not dest.
    /// </summary>
    public static float Pad(float field) => Offset - field;

    /// <summary>
    /// First consumers of the later pad:
    /// <c>90.0 &lt;= pad &lt;= 570.0</c>. That is not dest from
    /// 148.0, 90.0, or 570.0.
    /// </summary>
    public static bool Applies(float pad) => pad >= WindowLow && pad <= WindowHigh;
}
