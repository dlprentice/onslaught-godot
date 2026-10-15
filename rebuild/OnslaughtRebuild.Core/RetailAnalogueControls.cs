// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.Core;

/// <summary>
/// The released PC analogue-axis normalisation and the analogue arms of the
/// controller mapping loop. Pure arithmetic on a raw DirectInput axis value:
/// the device read is the caller's problem, the law is retail's.
/// </summary>
/// <remarks>
/// <para>
/// Owners in the pinned drop: <c>references/Onslaught/PCController.cpp:152-191</c>
/// (the four axis getters) and <c>Controller.cpp:218-417</c>
/// (<c>CController::DoMappings</c>). Retail identities in the pristine
/// <c>74154bfa…</c> image, file offset = VA - 0x400000:
/// </para>
/// <list type="bullet">
/// <item><c>0x00514640</c> <c>GetJoyAnalogueLeftX</c> — reads <c>DIJOYSTATE.lX</c> at <c>+0</c>.</item>
/// <item><c>0x00514670</c> <c>GetJoyAnalogueLeftY</c> — <c>lY</c> at <c>+4</c>.</item>
/// <item><c>0x005146A0</c> <c>GetJoyAnalogueRightX</c> — <c>lZ</c> at <c>+8</c>.</item>
/// <item><c>0x005146D0</c> <c>GetJoyAnalogueRightY</c> — <c>lRz</c> at <c>+0x14</c>.</item>
/// <item><c>0x0042DB40</c> <c>CController::DoMappings</c>.</item>
/// </list>
/// <para>
/// <b>Source and retail DIVERGE on the three 1/1000 axes, and retail wins.</b>
/// <c>PCController.cpp:155</c>, <c>:166</c> and <c>:177</c> all write
/// <c>lx = lx / 1000.0f</c>. The shipped code multiplies:
/// <c>fild dword ptr [eax]</c> then <c>fmul dword ptr [0x005DC6E4]</c>, and
/// <c>0x005DC6E4</c> holds <c>0x3A83126F</c>, which is <c>0.001f</c> exactly —
/// the correctly-rounded float nearest 1/1000, not 1/1000. Multiplying by that
/// reciprocal is not the same function as dividing by 1000: swept over raw
/// values in <c>[-70000, 70000]</c> the two disagree by one float ulp at 81 462
/// of the 140 001 inputs, i.e. most of them. This implementation multiplies,
/// because that is what shipped.
/// <br/>
/// The fourth axis has no such divergence: <c>0x005146D0</c> subtracts
/// <c>32768.0f</c> (<c>0x005E4928</c>) and multiplies by
/// <c>0x005D8DE4 = 2^-15</c>, and scaling by a power of two is exact, so
/// <c>vy / 32768.0f</c> and <c>vy * 2^-15</c> are the same function. Retail
/// also treats this axis differently in kind — <c>lRz</c> is read as an
/// unsigned-style 0..65535 range and re-centred, where the other three arrive
/// already signed. That asymmetry is in the source too
/// (<c>PCController.cpp:186-190</c>) and is not a divergence.
/// </para>
/// <para>
/// <b>A whole source stage is absent from retail.</b>
/// <c>Controller.cpp:226-233</c> reads all four axes at the top of
/// <c>DoMappings</c> and zeroes each one whose magnitude is under
/// <c>ANALOGUE_X_DEAD</c> / <c>ANALOGUE_Y_DEAD</c>, both <c>0.36f</c>
/// (<c>Controller.h:11-12</c>). No such stage exists at <c>0x0042DB40</c>: the
/// prologue goes from the inlined <c>CalcNumMappings</c> straight to the
/// <c>mPlaying</c> test and then to the repeat timer, with no axis reads and no
/// magnitude compare, and the constant <c>0.36f</c> (<c>0x3EB851EC</c>) does not
/// occur <b>anywhere</b> in the 2 506 752-byte image — nor does the double
/// <c>0.36</c>. So no 0.36 dead zone is applied here, and none is implemented.
/// Whether the shipped build applies a dead zone elsewhere, or applies none at
/// all, is open; the cheapest falsifier is to find any other write to
/// <c>mAnaloguex1..mAnaloguey2</c> in the image outside
/// <c>CPCController::ReadControllerState</c>.
/// </para>
/// <para>
/// The rest of the analogue mapping <b>does</b> agree. The four gates and their
/// exact constants are all present at <c>0x0042DB40</c>: <c>ANALOGUE_PLUS</c>
/// fires on <c>&gt; 0.0f</c> (<c>0x0042DDFA</c>), <c>ANALOGUE_MINUS</c> on
/// <c>&lt; 0.0f</c> (<c>0x0042DE31</c>), and the two act-as-digital repeat arms
/// on <c>&gt; 0.9f</c> (<c>0x005D8BB0</c>, at <c>0x0042DE64</c>) and
/// <c>&lt; -0.9f</c> (<c>0x005D8BB4</c>, at <c>0x0042DEEF</c>) — all four
/// strict, so a dead-centre axis fires nothing. The repeat delays are inlined
/// immediates: <c>0x3F000000</c> = <c>INITIAL_REPEAT_DELAY 0.5f</c> and
/// <c>0x3DF5C28F</c> = <c>REPEAT_DELAY 0.12f</c>, matching
/// <c>Controller.cpp:14-15</c>.
/// </para>
/// <para>
/// <b>Precision-control caveat.</b> Retail does one x87 multiply and the caller
/// stores the result to a float member, so the value is rounded twice: once at
/// the x87 precision control, once to float. This models the Win32 CRT default
/// of 53-bit control. Over raw values in <c>[-200000, 200000]</c> only 16
/// inputs — all exact ties, the smallest being ±768 — could differ under a
/// 64-bit control. The falsifier is to read the stored <c>mAnaloguex1</c> bits
/// out of a live pristine run with a raw <c>lX</c> of 768.
/// </para>
/// </remarks>
public static class RetailAnalogueControls
{
    /// <summary>
    /// The reciprocal retail multiplies the three signed axes by —
    /// <c>0x3A83126F</c> at <c>0x005DC6E4</c>. <b>Not</b> 1/1000.
    /// </summary>
    public const float AxisScale = 0.001f;

    /// <summary>Re-centring offset for <c>lRz</c> — <c>0x005E4928</c>.</summary>
    public const float RightYCentre = 32768.0f;

    /// <summary>Exact 2^-15 scale for <c>lRz</c> — <c>0x005D8DE4</c>.</summary>
    public const float RightYScale = 1.0f / 32768.0f;

    /// <summary><c>ANALOGUE_ACT_AS_DIGITAL_THRESHOLD</c> — <c>Controller.h:46</c>; <c>0x005D8BB0</c>.</summary>
    public const float ActAsDigitalThreshold = 0.9f;

    /// <summary><c>INITIAL_REPEAT_DELAY</c> — <c>Controller.cpp:14</c>; immediate <c>0x3F000000</c>.</summary>
    public const float InitialRepeatDelay = 0.5f;

    /// <summary><c>REPEAT_DELAY</c> — <c>Controller.cpp:15</c>; immediate <c>0x3DF5C28F</c>.</summary>
    public const float RepeatDelay = 0.12f;

    /// <summary>
    /// <c>CPCController::GetJoyAnalogueLeftX</c> — <c>0x00514640</c>. Raw
    /// <c>DIJOYSTATE.lX</c> in, platform-independent axis out.
    /// </summary>
    public static float NormalizeLeftX(int rawAxis) => ScaleSignedAxis(rawAxis);

    /// <summary><c>CPCController::GetJoyAnalogueLeftY</c> — <c>0x00514670</c>, over <c>lY</c>.</summary>
    public static float NormalizeLeftY(int rawAxis) => ScaleSignedAxis(rawAxis);

    /// <summary><c>CPCController::GetJoyAnalogueRightX</c> — <c>0x005146A0</c>, over <c>lZ</c>.</summary>
    public static float NormalizeRightX(int rawAxis) => ScaleSignedAxis(rawAxis);

    /// <summary>
    /// <c>CPCController::GetJoyAnalogueRightY</c> — <c>0x005146D0</c>, over
    /// <c>lRz</c>. Re-centres a 0..65535 axis and scales by an exact power of
    /// two, so this arm is bit-identical to the source's division.
    /// </summary>
    public static float NormalizeRightY(int rawAxis) =>
        (float)((((double)rawAxis) - (double)RightYCentre) * (double)RightYScale);

    /// <summary>
    /// The value retail's axis getters return when the requested pad is not
    /// present — <c>fld dword ptr [0x005D856C]</c>, which is <c>0.0f</c>. All
    /// four getters take this arm; <c>0x005146D0</c> takes it on either of two
    /// tests.
    /// </summary>
    public static float AbsentPadAxis => 0.0f;

    /// <summary><c>ANALOGUE_PLUS</c> — <c>Controller.cpp:313</c>; <c>0x0042DDFA</c>. Strict.</summary>
    public static bool AnaloguePlusFires(float analogueValue) => analogueValue > 0.0f;

    /// <summary><c>ANALOGUE_MINUS</c> — <c>Controller.cpp:323</c>; <c>0x0042DE31</c>. Strict.</summary>
    public static bool AnalogueMinusFires(float analogueValue) => analogueValue < 0.0f;

    /// <summary>
    /// <c>ANALOGUE_PLUS_ACT_AS_BUTTON_REPEAT</c> — <c>Controller.cpp:333</c>;
    /// <c>0x0042DE64</c>. Strictly above <c>0.9f</c>.
    /// </summary>
    public static bool AnaloguePlusRepeatArms(float analogueValue) =>
        analogueValue > ActAsDigitalThreshold;

    /// <summary>
    /// <c>ANALOGUE_MINUS_ACT_AS_BUTTON_REPEAT</c> — <c>Controller.cpp:347</c>;
    /// <c>0x0042DEEF</c>. Strictly below <c>-0.9f</c>.
    /// </summary>
    public static bool AnalogueMinusRepeatArms(float analogueValue) =>
        analogueValue < -ActAsDigitalThreshold;

    private static float ScaleSignedAxis(int rawAxis) =>
        (float)((double)rawAxis * (double)AxisScale);
}
