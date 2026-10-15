// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render pointer hit-test — recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this cycle
/// (2,506,752 bytes).
///
/// <para><b>Gate.</b> Two sites, <c>0x004630AC</c> (menu rows) and
/// <c>0x004631EF</c> (language row):
/// <c>fld [esp+0xA4]; fcomp [0x005D8BB0]; fnstsw ax; test ah, 0x41 / jne skip</c>.
/// <c>0x005D8BB0</c> is <c>0x3F666666</c> (0.9). C3|C0 means
/// transition &lt;= 0.9 skips. Hit-test therefore runs only when
/// transition &gt; 0.9 — the last tenth of the 50-frame
/// <c>SetPage(FEP_MAIN, 50)</c> reveal. The comment that used to say
/// retail ignores that last tenth had the compare backwards.</para>
///
/// <para><b>Not ButtonPressed.</b> <c>FrontEnd.cpp:551-552</c> still
/// drops every button while <c>mActivePage == FEP_TRANSITION</c>. A
/// true <c>0x004693D0</c> writes <c>this+0x08</c> only. Language hover
/// writes <c>-1</c> at <c>0x00463274</c>. That is not
/// <c>CFrontEnd::SetLanguage</c> (<c>FrontEnd.cpp:557-560</c>).</para>
///
/// <para><b>Language rect.</b> cdecl into <c>0x004693D0</c> is
/// <c>(119, Y-20, 319, Y+20)</c>. <c>0x00523B50</c> is
/// <c>left &lt;= x &lt; right</c> and <c>top &lt;= y &lt; bottom</c>.
/// <c>Y</c> is seeded <c>0x43860000</c> (268) at <c>0x00462E96</c>.
/// <c>[0x0083D990]</c> nonzero overwrites <c>0x43980000</c> (304);
/// that dword is uninitialised <c>.data</c>, so cold Y is 268. Half
/// extent is <c>0x005D857C</c> = 20.</para>
///
/// <para><b>Not the twin fade.</b> <c>0x00463E8D</c> uses
/// <c>test ah, 1 / je 0x00463FC7</c> (transition &lt; 0.9) and then
/// scales <c>0x0089D8A4</c>. That predicate is exposed so it cannot
/// be mistaken for this gate. No fade is implemented.</para>
///
/// <para>No Godot types. HandlePointerMotion is the consumer.
/// HandleKey, DrawLoading, DrawQuitConfirm, DrawClickToStart, and
/// HandlePointerConfirm stay untouched. Confirm is ButtonPressed,
/// still blocked for the whole FEP_TRANSITION.</para>
/// </summary>
public static class RetailMainMenuHitTest
{
    /// <summary><c>fcomp [0x005D8BB0]</c> at <c>0x004630AC</c>.</summary>
    public const uint ThresholdGlobal = 0x005D8BB0u;

    /// <summary>Image bits at <c>0x005D8BB0</c>.</summary>
    public const uint ThresholdBits = 0x3F666666u;

    /// <summary>The <see cref="ThresholdBits"/> dword, not a second 0.9 literal.</summary>
    public static float Threshold => BitConverter.UInt32BitsToSingle(ThresholdBits);

    /// <summary><c>push 0x42EE0000</c> at <c>0x00463263</c>.</summary>
    public const float LanguageHoverLeft = 119f;

    /// <summary><c>push 0x439F8000</c> at <c>0x0046325A</c>.</summary>
    public const float LanguageHoverRight = 319f;

    /// <summary><c>mov [esp+0x10], 0x43860000</c> at <c>0x00462E96</c>.</summary>
    public const float LanguageHoverCenterY = 268f;

    /// <summary><c>0x005D857C</c>, added and subtracted from Y.</summary>
    public const float LanguageHoverHalfExtent = 20f;

    /// <summary><c>mov [edi+8], -1</c> at <c>0x00463274</c>.</summary>
    public const int LanguageSelectedIndex = -1;

    /// <summary><c>[0x0083D990]</c>. Uninitialised <c>.data</c>.</summary>
    public const uint LanguageCenterFlagGlobal = 0x0083D990u;

    /// <summary>Image-initial dword. Cold Render keeps 268.</summary>
    public const uint ImageInitialLanguageCenterFlag = 0u;

    /// <summary><c>mov [esp+0x10], 0x43980000</c> at <c>0x00462EF5</c>.</summary>
    public const float AlternateLanguageHoverCenterY = 304f;

    /// <summary>
    /// Render hover is not <c>CFrontEnd::ReceiveButtonAction</c>.
    /// </summary>
    public const bool IsButtonPressed = false;

    /// <summary>
    /// Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.
    /// </summary>
    public const bool IsSetLanguage = false;

    /// <summary>
    /// <c>test ah, 0x41 / jne skip</c> after <c>fcomp [0.9]</c>.
    /// </summary>
    public static bool AcceptsHitTest(float transition) => transition > Threshold;

    /// <summary>
    /// <c>FrontEnd.cpp:551-552</c> drops <c>ButtonPressed</c> for the
    /// whole <c>FEP_TRANSITION</c>. Pointer motion is Render hover, not
    /// a button, so it is not swallowed here.
    /// </summary>
    public static bool SwallowsFrontendInput(bool pageIsTransition, bool isPointerMotion) =>
        pageIsTransition && !isPointerMotion;

    /// <summary>
    /// The third <c>0x005D8BB0</c> site: <c>test ah, 1 / je</c>.
    /// Fall-through when transition &lt; 0.9. Not implemented as a fade.
    /// </summary>
    public static bool AcceptsTwinFade(float transition) => transition < Threshold;

    /// <summary>Cold Y is 268 because the flag image-initial is 0.</summary>
    public static float LanguageHoverCenterYFor(uint flag) =>
        flag == ImageInitialLanguageCenterFlag
            ? LanguageHoverCenterY
            : AlternateLanguageHoverCenterY;

    /// <summary>
    /// <c>0x00523B50</c> on the language rect. Default centre is the
    /// cold 268 immediate.
    /// </summary>
    public static bool LanguageHoverContains(float x, float y) =>
        LanguageHoverContains(x, y, LanguageHoverCenterY);

    /// <summary>
    /// <c>left &lt;= x &lt; right</c> and <c>top &lt;= y &lt; bottom</c>.
    /// </summary>
    public static bool LanguageHoverContains(float x, float y, float centerY) =>
        x >= LanguageHoverLeft &&
        x < LanguageHoverRight &&
        y >= centerY - LanguageHoverHalfExtent &&
        y < centerY + LanguageHoverHalfExtent;
}
