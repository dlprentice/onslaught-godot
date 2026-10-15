// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// <c>CFEPLevelSelect::Render</c> first leftover — the unique
/// <c>call 0x00467200</c> to
/// <c>CFrontEnd__DrawSlidingTextBordersAndMask</c> — recovered from
/// official 74154bfa
/// <c>local-lab/pristine-verification-2026-07-26/pristine-target/BEA.exe</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>. Twin
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c> is
/// the same size and hash.
///
/// <para><b>Sibling.</b> Latch-to-button SET at <c>0x0042D5CF</c> is
/// already <see cref="OnslaughtRebuild.Client.RetailFrontendLatchToButton"/>.
/// FMV skip OR at <c>0x0053F2EB</c> already owns the three latch
/// dwords. Expanded list click / hover / colour / dest Y / dest X /
/// panel dest / cancel / click-hit sound stay on their leftovers.
/// This leftover is the Render prologue call. Do not redo those.
/// Do not invent dest Y as 5, 15.5, 268, 284, or 304. Do not invent
/// dest X as 322.5 or dest from 148.0. Do not invent dest as the
/// 2.0 constant.</para>
///
/// <para><b>Call.</b> Official 74154bfa independently re-read:
/// <c>0x00460B40</c> is <c>sub esp, 0x80</c> (<c>81 ec 80 00 00 00</c>).
/// <c>0x00460B46</c> is <c>mov eax, [esp+0x88]</c> (dest page).
/// <c>0x00460B50</c> is <c>mov esi, ecx</c> (this).
/// <c>0x00460B53</c> is <c>mov ecx, [esp+0x94]</c> (transition).
/// <c>0x00460B5A</c> / <c>0x00460B5B</c> push dest then transition.
/// <c>0x00460B5C</c> is <c>mov ecx, 0x0089D758</c>.
/// <c>0x00460B61</c> is the unique <c>call 0x00467200</c>
/// (<c>e8 9a 66 00 00</c>, one image hit). That is
/// <c>CFrontEnd__DrawSlidingTextBordersAndMask</c>.
/// <c>0x00460B66</c> <c>fld [0x005DB53C]</c> (148.0) /
/// <c>0x00460B6C</c> <c>fsub [esi+0x3460]</c> /
/// <c>0x00460B72</c> <c>fstp [esp+0x14]</c> is later. Dest does
/// not. Colour leftover already consults currentIndex. Hover
/// leftover already owns <c>0x004A3FA6</c>. Click leftover already
/// owns <c>0x004A4010</c>. Cancel leftover already owns
/// <c>0x004A4059</c>. Click-hit sound leftover already owns
/// <c>0x004A403C</c>. Nearby 15.5, 322.5, and 148.0 are
/// measurements, not dest. Dest is not the 2.0 constant.</para>
///
/// <para><b>Settled.</b> Pinned GPL
/// <c>references/Onslaught/FrontEnd.cpp:778</c> /
/// <c>:807</c>: <c>FEP_LEVEL_SELECT</c> is in
/// <c>got_standard_SlidingTextBordersAndMask</c>, which forces
/// <c>transition = 1</c> when the from-page is not
/// <c>FEP_VIRTUAL_KEYBOARD</c>. Settled inside scale is
/// <c>SELECT_BRACKET_SCALE</c> 1.25. That is not dest from 148.0.
/// DrawLevelSelect already owns the measured 328/343 dest.</para>
///
/// <para><b>Not a fade.</b> Not a sheen, dest immediates, or a
/// 2px kerning hack. Not <c>SetLanguage</c>. HandleKey,
/// DrawLoading, DrawQuitConfirm, HandlePointerConfirm, the cursor,
/// the row colour AND, Apply, dropdown cosine, writing chrome,
/// language pitch, CMenuItem dest, dropdown label dest, icon dest,
/// collapsed value dest, expanded list dest X, expanded panel dest,
/// expanded list dest Y, expanded list colour, expanded list hover,
/// expanded list click, expanded list cancel, click-hit sound,
/// latch-to-button SET, and the 0x00463669 compare stay untouched.</para>
/// </summary>
public static class RetailLevelSelectSlidingBorders
{
    /// <summary><c>CFEPLevelSelect::Render</c> at <c>0x00460B40</c>.</summary>
    public const uint RenderSite = 0x00460B40u;

    /// <summary><c>sub esp, 0x80</c>. Frame leftover, not dest.</summary>
    public const uint PrologueSite = 0x00460B40u;

    /// <summary>Prologue immediate. Not dest and not the 148.0 later fld.</summary>
    public const int PrologueImmediate = 0x80;

    /// <summary><c>mov eax, [esp+0x88]</c> at <c>0x00460B46</c>. Dest page, not dest XY.</summary>
    public const uint DestPageLoadSite = 0x00460B46u;

    /// <summary><c>mov esi, ecx</c> at <c>0x00460B50</c>. This leftover, not dest.</summary>
    public const uint ThisSaveSite = 0x00460B50u;

    /// <summary><c>mov ecx, [esp+0x94]</c> at <c>0x00460B53</c>. Transition leftover.</summary>
    public const uint TransitionLoadSite = 0x00460B53u;

    /// <summary><c>push eax</c> at <c>0x00460B5A</c>. Dest page, not dest XY.</summary>
    public const uint DestPagePushSite = 0x00460B5Au;

    /// <summary><c>push ecx</c> at <c>0x00460B5B</c>. Transition leftover.</summary>
    public const uint TransitionPushSite = 0x00460B5Bu;

    /// <summary><c>mov ecx, 0x0089D758</c> at <c>0x00460B5C</c>.</summary>
    public const uint FrontendThisLoadSite = 0x00460B5Cu;

    /// <summary>FRONTEND this. Same object as the main-menu surf this.</summary>
    public const uint FrontendThis = 0x0089D758u;

    /// <summary>Unique <c>call 0x00467200</c> at <c>0x00460B61</c>. First leftover.</summary>
    public const uint CallSite = 0x00460B61u;

    /// <summary><c>CFrontEnd__DrawSlidingTextBordersAndMask</c>.</summary>
    public const uint DrawSlidingTextBordersAndMask = 0x00467200u;

    /// <summary><c>fld [0x005DB53C]</c> at <c>0x00460B66</c>. Later. Not dest.</summary>
    public const uint LaterFldSite = 0x00460B66u;

    /// <summary>148.0 constant. Later leftover, not dest.</summary>
    public const uint LaterConst = 0x005DB53Cu;

    /// <summary>IEEE bits at <see cref="LaterConst"/>. 148.0. Not dest.</summary>
    public const uint LaterConstBits = 0x43140000u;

    /// <summary><c>fsub [esi+0x3460]</c> at <c>0x00460B6C</c>. Later. Not dest.</summary>
    public const uint LaterFsubSite = 0x00460B6Cu;

    /// <summary><c>fstp [esp+0x14]</c> at <c>0x00460B72</c>. Later. Not dest.</summary>
    public const uint LaterFstpSite = 0x00460B72u;

    /// <summary>Latch SET at <c>0x0042D5CF</c> is already owned.</summary>
    public const uint LatchSetSite = 0x0042D5CFu;

    /// <summary>FMV skip OR at <c>0x0053F2EB</c> is already owned.</summary>
    public const uint FmvOrSite = 0x0053F2EBu;

    /// <summary>GPL <c>SELECT_BRACKET_SCALE</c>. Settled inside scale. Not dest.</summary>
    public const float SettledInsideScale = 1.25f;

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

    /// <summary>The 2.0 pad is not dest or this call.</summary>
    public const bool InventsDestFromPad = false;

    /// <summary>Dest Y is not a 15.5 inset.</summary>
    public const bool InventsDestY15_5 = false;

    /// <summary>Dest X is not a 322.5 measurement.</summary>
    public const bool InventsDestX322_5 = false;

    /// <summary>148.0 is the later fld, not dest.</summary>
    public const bool InventsDestFrom148 = false;

    /// <summary>The call is not a dest immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover does not invent a wrap from 0.0625.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover does not invent a list fade.</summary>
    public const bool InventsFade = false;

    /// <summary>The call does not write currentIndex.</summary>
    public const bool UsesCurrentIndex = false;

    /// <summary>0x00460B61 is this leftover.</summary>
    public const bool IsSlidingBordersCall = true;

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

    /// <summary>0x00463647 is already language pitch.</summary>
    public const bool RedoesLanguagePitch = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits in LevelSelect.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>0x00463669 is not this leftover.</summary>
    public const bool UsesLanguageCompare = false;

    /// <summary>The sliding-borders call leftover is not a MeasureText change.</summary>
    public const bool ChangesMeasureText = false;

    /// <summary><see cref="LaterConstBits"/> decoded as IEEE-754 single.</summary>
    public static float LaterConstValue => BitConverter.UInt32BitsToSingle(LaterConstBits);

    /// <summary>
    /// Pinned GPL <c>got_standard_SlidingTextBordersAndMask</c> plus
    /// the helper's <c>from != FEP_VIRTUAL_KEYBOARD</c> gate. Settled
    /// <c>FEP_LEVEL_SELECT</c> forces transition 1. That is not dest.
    /// </summary>
    public static bool Applies(bool standardPage, bool fromVirtualKeyboard) =>
        standardPage && !fromVirtualKeyboard;
}
