// SPDX-License-Identifier: GPL-3.0-or-later

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro "Click to start" glyph submits — one
/// <c>Localization__GetStringById</c> lookup and five
/// <c>CDXFont__DrawTextScaled</c> calls — recovered from the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051B92F</c>–<c>0x0051BAC2</c>.
/// Not in the pinned GPL drop (<c>FEPIntro.cpp</c> is absent). This is not
/// <c>CText__GetStringById</c>, not attract <c>vectorlosttoyssplash</c>, and
/// not TWIMTBP.</para>
///
/// <para><b>Lookup.</b> <c>push 0x77</c> at <c>0x0051B92F</c> then
/// <c>Localization__GetStringById</c> at <c>0x00524830</c>. Font this is
/// <c>CPlatform__Font(0x0088A0A8, 0)</c> at <c>0x00515A70</c>. Width is the
/// first dword of <c>CDXFont__GetTextExtent</c> at <c>0x00540680</c>
/// (<c>fild [esp+8]</c>).</para>
///
/// <para><b>Passes.</b> Mode is five <c>DrawTextScaled</c> calls at
/// <c>0x00540010</c>. <c>sx=sy</c> immediates <c>0x3F800000</c> (1.0). z bits
/// <c>0x3DCCCCCD</c> (0.1). Y immediates 401 / 401 / 399 / 399 / 400
/// (<c>0x43C88000</c> / <c>0x43C78000</c> / <c>0x43C80000</c>). Outline colour
/// <c>0xFF000000</c>; body <c>push -1</c> = <c>0xFFFFFFFF</c>.</para>
///
/// <para><b>X.</b> <c>fmul [0.5f]</c> at <c>0x005D85EC</c>,
/// <c>fsubr [320.0f]</c> at <c>0x005DB3E8</c>, then <c>fsub</c> / <c>fadd</c>
/// <c>[1.0f]</c> at <c>0x005D8568</c> on the four outline corners. That is
/// <c>320 − width×0.5 + dx</c>. A capture-derived <c>textScale = 2</c> is
/// not the body.</para>
///
/// <para>No Godot types. The 2-D consumer ignores z; the bits stay on the
/// helper so a later depth owner does not have to re-read the body.
/// <c>DrawClickToStart</c> consumes <see cref="ShouldDraw"/>, <see cref="X"/>,
/// and <see cref="Passes"/>.</para>
/// </summary>
public static class RetailClickToStartGlyphs
{
    /// <summary><c>push 0x77</c> at <c>0x0051B92F</c>.</summary>
    public const int LocalizationId = 0x77;

    /// <summary>Second argument of <c>CPlatform__Font</c> at <c>0x0051B941</c>.</summary>
    public const int FontSlot = 0;

    /// <summary><c>0x005DB3E8</c> = <c>320.0f</c>.</summary>
    public const float CentreX = 320f;

    /// <summary><c>0x005D85EC</c> = <c>0.5f</c>.</summary>
    public const float HalfWidth = 0.5f;

    /// <summary>x_scale immediate <c>0x3F800000</c>.</summary>
    public const float ScaleX = 1f;

    /// <summary>y_scale immediate <c>0x3F800000</c>.</summary>
    public const float ScaleY = 1f;

    /// <summary>z immediate at each call site.</summary>
    public const uint ZBits = 0x3DCCCCCDu;

    /// <summary>Outline colour immediate at <c>0x0051B95D</c>.</summary>
    public const uint OutlineColor = 0xFF000000u;

    /// <summary>Body colour immediate at <c>0x0051BA89</c> (<c>push -1</c>).</summary>
    public const uint BodyColor = 0xFFFFFFFFu;

    /// <summary>One <c>CDXFont__DrawTextScaled</c> call.</summary>
    public readonly record struct Pass(float Dx, float Y, uint Color);

    /// <summary>
    /// Outline corners then body, in the order Render issues them
    /// (<c>0x0051B954</c>–<c>0x0051BAC2</c>).
    /// </summary>
    public static readonly Pass[] Passes =
    [
        new(-1f, 401f, OutlineColor),
        new(1f, 401f, OutlineColor),
        new(-1f, 399f, OutlineColor),
        new(1f, 399f, OutlineColor),
        new(0f, 400f, BodyColor),
    ];

    /// <summary>
    /// Whether Render would submit the five glyph calls. Same arm as
    /// <see cref="RetailClickToStartPrompt.IsPromptVisible"/>.
    /// </summary>
    public static bool ShouldDraw(double timer) =>
        RetailClickToStartPrompt.IsPromptVisible(timer);

    /// <summary>
    /// <c>320 − width×0.5 + dx</c>. <paramref name="width"/> is the first
    /// dword written by <c>GetTextExtent</c>.
    /// </summary>
    public static float X(Pass pass, int width) =>
        CentreX - (width * HalfWidth) + pass.Dx;
}
