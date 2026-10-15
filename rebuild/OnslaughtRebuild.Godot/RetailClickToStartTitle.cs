// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro title-logo slam — five <c>CDXSurf__RenderSurface</c> calls on
/// <c>0x0089d88c</c>, then a sixth z=0.02 copy — recovered from the pristine
/// specimen <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>,
/// SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051BBA0</c>–<c>0x0051BD8C</c>.
/// Not in the pinned GPL drop.</para>
///
/// <para><b>Gate.</b> <c>GetTime()-[this+4]</c>, <c>fmul [1.2]</c> at
/// <c>0x005DBCE4</c>, <c>fcom [2.0]</c> at <c>0x005D8BA0</c>,
/// <c>test ah,0x41 / jnz skip</c>. The logo is drawn only when
/// page-elapsed × 1.2 is strictly greater than 2.</para>
///
/// <para><b>Scale.</b> <c>v = 25 - 12*page</c> (the
/// <c>fsubr [0.5]; fadd [2]; fmul [10]</c> form of <c>10*(2.5-1.2*page)</c>).
/// While <c>v &gt;= 1</c> the remaining ST after the brightness
/// <c>fistp</c> is <c>v</c>; once <c>v &lt; 1</c> (page &gt; 2) it is
/// replaced with 1. Then <c>fmul [0.5]</c> at <c>0x0051BC29</c>. That is a
/// slam from 2.5 down to 0.5, then a freeze. It is not a sine pulse and it
/// is not 0.35.</para>
///
/// <para><b>Passes.</b> Mode 4 (centre) at z 0.05 on the four ±2 corners of
/// (250, 290), then the body at (250, 290) z 0.04. Outline colour is
/// <c>(round(u*255)*159) &lt;&lt; 16 &amp; 0xFF000000</c> (black RGB). Body
/// colour is the <c>*255 / not / and 0x00FFFFFF / xor</c> pack
/// (<c>0xFEFFFFFF</c> once settled). <c>u</c> is 1 when <c>v &lt; 1</c>,
/// else <c>1/v</c>.</para>
///
/// <para><b>Sixth pass.</b> After the body call, Render compares the
/// <c>[esp+0x14]</c> slot (<c>-1</c> while <c>v &gt;= 1</c>, else
/// <c>1-v</c>) with <c>3.0</c> at <c>0x005D8CC0</c> (<c>test ah,1 / je
/// skip</c>) and <c>0.0</c> at <c>0x005D856C</c> (<c>test ah,0x41 / jne
/// skip</c>). That is a second gate: <c>2 &lt; page &lt; 2.25</c>, not
/// <c>page*1.2 &gt; 2</c>. The extra call is mode 4 at (250, 290) z bits
/// <c>0x3CA3D70A</c> (0.02), sx=sy=<c>1-v</c>, colour the body pack of
/// <c>round(32/(1-v))</c> (<c>1/(4*(1-v))*128</c> at <c>0x0051BD3C</c>).
/// It is not folded into <see cref="Passes"/>.</para>
///
/// <para>No Godot types. The 2-D consumer ignores z; the values stay on the
/// pass records so a later depth owner does not have to re-read the body.</para>
/// </summary>
public static class RetailClickToStartTitle
{
    /// <summary><c>0x005DBCE4</c> = <c>1.2f</c>.</summary>
    public const double TimeScale = 1.2;

    /// <summary><c>0x005D8BA0</c> = <c>2.0f</c>.</summary>
    public const double GateSeconds = 2.0;

    /// <summary>Settled sx=sy once page &gt; 2.</summary>
    public const float SettledScale = 0.5f;

    /// <summary><c>0x005D8CC0</c> = <c>3.0f</c>. Sixth pass needs 1-v strictly below this.</summary>
    public const float SixthFadeLimit = 3.0f;

    /// <summary>z immediate at <c>0x0051BD78</c>.</summary>
    public const uint SixthZBits = 0x3CA3D70Au;

    /// <summary>One mode-4 <c>CDXSurf__RenderSurface</c> call.</summary>
    public readonly record struct Pass(float X, float Y, float Z, bool Outline);

    /// <summary>
    /// Outline corners then body, in the order Render issues them
    /// (<c>0x0051BC45</c>–<c>0x0051BCFC</c>).
    /// </summary>
    public static readonly Pass[] Passes =
    [
        new(252f, 292f, 0.05f, Outline: true),
        new(248f, 292f, 0.05f, Outline: true),
        new(252f, 288f, 0.05f, Outline: true),
        new(248f, 288f, 0.05f, Outline: true),
        new(250f, 290f, 0.04f, Outline: false),
    ];

    /// <summary>
    /// Extra mode-4 call at <c>0x0051BD87</c>. Not in <see cref="Passes"/> —
    /// the gate is <c>2 &lt; page &lt; 2.25</c>, not <c>page*1.2 &gt; 2</c>.
    /// </summary>
    public static readonly Pass SixthPass = new(250f, 290f, 0.02f, Outline: false);

    /// <summary>Whether Render would submit the five title-logo calls.</summary>
    public static bool ShouldDraw(double pageSeconds) =>
        pageSeconds * TimeScale > GateSeconds;

    /// <summary>
    /// Whether Render would submit the sixth z=0.02 call
    /// (<c>0x0051BD01</c>–<c>0x0051BD25</c>).
    /// </summary>
    public static bool ShouldDrawSixth(double pageSeconds)
    {
        float fade = SixthFade(pageSeconds);
        return fade > 0f && fade < SixthFadeLimit;
    }

    /// <summary>
    /// sx=sy. <c>0.5 * v</c> while <c>v = 25-12*page &gt;= 1</c>, else 0.5.
    /// </summary>
    public static float Scale(double pageSeconds)
    {
        float v = Ramp(pageSeconds);
        return v < 1f ? SettledScale : SettledScale * v;
    }

    /// <summary>
    /// Sixth-pass sx=sy: remaining ST after <c>fmul [0.5]</c> at
    /// <c>0x0051BD5A</c> is <c>1-v</c>.
    /// </summary>
    public static float SixthScale(double pageSeconds) => SixthFade(pageSeconds);

    /// <summary>
    /// Outline pack at <c>0x0051BC21</c>: <c>edi*159 &lt;&lt; 16</c> masked to
    /// the alpha byte. RGB stays 0.
    /// </summary>
    public static uint OutlineColor(double pageSeconds)
    {
        int edi = BrightnessByte(pageSeconds);
        return ((uint)(edi * 159) << 16) & 0xFF000000u;
    }

    /// <summary>
    /// Body pack at <c>0x0051BCCA</c>: <c>edi*255 &lt;&lt; 16</c>, then
    /// <c>not / and 0x00FFFFFF / xor</c>. Settled value is <c>0xFEFFFFFF</c>.
    /// </summary>
    public static uint BodyColor(double pageSeconds)
    {
        int edi = BrightnessByte(pageSeconds);
        return PackBodyColor(edi);
    }

    /// <summary>
    /// Sixth-pass colour at <c>0x0051BD50</c>: brightness
    /// <c>round(32/(1-v))</c>, then the same body pack.
    /// </summary>
    public static uint SixthColor(double pageSeconds)
    {
        int edi = (int)MathF.Round(32f / SixthFade(pageSeconds));
        return PackBodyColor(edi);
    }

    /// <summary>
    /// <c>fsubr [0.5]; fadd [2]; fmul [10]</c> = <c>25 - 12*page</c>.
    /// </summary>
    private static float Ramp(double pageSeconds) =>
        25f - (12f * (float)pageSeconds);

    /// <summary>
    /// <c>u = v &lt; 1 ? 1 : 1/v</c>, then <c>fistp (u*255)</c>.
    /// </summary>
    private static int BrightnessByte(double pageSeconds)
    {
        float v = Ramp(pageSeconds);
        float u = v < 1f ? 1f : 1f / v;
        return (int)MathF.Round(u * 255f);
    }

    /// <summary>
    /// The <c>[esp+0x14]</c> slot written at <c>0x0051BBCA</c>: <c>-1</c>
    /// while <c>v &gt;= 1</c>, else <c>1-v</c>.
    /// </summary>
    private static float SixthFade(double pageSeconds)
    {
        float v = Ramp(pageSeconds);
        return v < 1f ? 1f - v : -1f;
    }

    /// <summary>
    /// <c>shl 8 / sub / shl 16 / not / and 0x00FFFFFF / xor</c>.
    /// </summary>
    private static uint PackBodyColor(int edi)
    {
        uint eax = (uint)((edi << 8) - edi) << 16;
        uint ecx = (~eax) & 0x00FFFFFFu;
        return ecx ^ eax;
    }
}
