// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro LostToys slide — two <c>CDXSurf__RenderSurface</c> calls on
/// <c>0x0089d7bc</c> — recovered from the pristine specimen
/// (a private pristine copy), SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051BAC2</c>–<c>0x0051BB9D</c>.
/// Not in the pinned GPL drop (<c>FEPIntro.cpp</c> is absent). This is
/// <c>FrontEnd\LostToys.tga</c>, not attract <c>vectorlosttoyssplash.tga</c>
/// and not TWIMTBP.</para>
///
/// <para><b>Fade.</b> After the two byte writes at <c>0x0051BAC2</c>, Render
/// loads <c>this+0x18</c>, a float subtract of the constant 4.0f at <c>0x005D85BC</c>, then
/// clamps that difference: a float compare against the constant 0.0f at <c>0x005D856C</c> /
/// a bit test of ah against 1, branching when equal replaces a negative with 0;
/// a float compare against the constant 1.0f at <c>0x005D8568</c> / a bit test of ah against 0x41, branching when not equal
/// replaces a value strictly greater than 1 with 1. There is no skip: both
/// surface calls still issue when the timer is at or below 4 (the pair sits
/// 400 px off the left edge).</para>
///
/// <para><b>Offset.</b> a reversed float subtract of the constant 1.0f, then copy
/// of an FPU stack entry to the top, then float multiply using the FPU stack then
/// a float multiply by the constant 400.0f at <c>0x005DB358</c>. That is
/// <c>(1-fade)² * 400</c>. The leftover <c>(1-fade)</c> is
/// an FPU stack pop-discarded. A linear <c>(1-fade)*400</c> is not the
/// body.</para>
///
/// <para><b>Passes.</b> Mode-0 pair, sx=sy=1. Shadow at settled (124, −6)
/// (<c>0x005E49D8</c> / push <c>0xC0C00000</c>) with colour
/// <c>0x3F000000</c> and z bits <c>0x3DCED917</c>. Body at settled
/// (120, −10) (<c>0x005DB4D0</c> / push <c>0xC1200000</c>) with colour
/// <c>0xFFFFFFFF</c> and z bits <c>0x3DCCCCCD</c>. Each X is
/// <c>settled − offset</c> (<c>fsub</c> of the stored 400-scaled square).
/// The first call's extra (a push of ebp) is not claimed as a scale.</para>
///
/// <para>No Godot types. The 2-D consumer ignores z; the bits stay on the
/// pass records so a later depth owner does not have to re-read the body.
/// <c>DrawClickToStart</c> consumes <see cref="ShouldDraw"/>, <see cref="X"/>,
/// and <see cref="Passes"/>. Not attract splash. Not TWIMTBP.</para>
/// </summary>
public static class RetailClickToStartSlide
{
    /// <summary><c>0x005D85BC</c> = <c>4.0f</c>. Fade argument is timer minus this.</summary>
    public const double GateSeconds = 4.0;

    /// <summary><c>0x005DB358</c> = <c>400.0f</c>.</summary>
    public const float TravelPixels = 400f;

    /// <summary>Shadow colour immediate at <c>0x0051BB31</c>.</summary>
    public const uint ShadowColor = 0x3F000000u;

    /// <summary>Body colour immediate at <c>0x0051BB81</c> (a push of constant -1).</summary>
    public const uint BodyColor = 0xFFFFFFFFu;

    /// <summary>Shadow z immediate at <c>0x0051BB3B</c>.</summary>
    public const uint ShadowZBits = 0x3DCED917u;

    /// <summary>Body z immediate at <c>0x0051BB84</c>.</summary>
    public const uint BodyZBits = 0x3DCCCCCDu;

    /// <summary>One mode-0 <c>CDXSurf__RenderSurface</c> call.</summary>
    public readonly record struct Pass(float SettledX, float Y, uint ZBits, uint Color)
    {
        public float Z => BitConverter.UInt32BitsToSingle(ZBits);
    }

    /// <summary>
    /// Shadow then body, in the order Render issues them
    /// (<c>0x0051BB0F</c>–<c>0x0051BB92</c>).
    /// </summary>
    public static readonly Pass[] Passes =
    [
        new(124f, -6f, ShadowZBits, ShadowColor),
        new(120f, -10f, BodyZBits, BodyColor),
    ];

    /// <summary>
    /// Whether Render would submit the pair. There is no skip after the two
    /// byte writes: both calls issue even when <paramref name="timer"/> is
    /// at or below <see cref="GateSeconds"/>.
    /// </summary>
    public static bool ShouldDraw(double timer)
    {
        _ = timer;
        return true;
    }

    /// <summary>
    /// <c>clamp(timer - 4, 0, 1)</c> from the two <c>fcom</c>s at
    /// <c>0x0051BAD9</c> / <c>0x0051BAF0</c>.
    /// </summary>
    public static float Fade(double timer)
    {
        float delta = (float)timer - (float)GateSeconds;
        if (delta < 0f)
        {
            return 0f;
        }

        return delta > 1f ? 1f : delta;
    }

    /// <summary>
    /// Horizontal travel still remaining: <c>(1-fade)² * 400</c>.
    /// </summary>
    public static float Offset(double timer)
    {
        float remain = 1f - Fade(timer);
        return remain * remain * TravelPixels;
    }

    /// <summary>Settled X minus <see cref="Offset"/>.</summary>
    public static float X(Pass pass, double timer) => pass.SettledX - Offset(timer);
}
