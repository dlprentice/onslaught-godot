// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// The CFEPIntro splash dest — one <c>CDXSurf__RenderSurface</c> call on
/// <c>0x0089d880</c> / <c>FrontEnd\v2\fe_splash1.tga</c> — recovered from
/// the pristine specimen
/// <c>local-lab/safe-copy-bea-pristine/BEA.exe.original.backup</c>, SHA-256
/// <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>
/// (2,506,752 bytes, re-hashed this cycle). File offset = VA − <c>0x400000</c>.
///
/// <para><b>Body.</b> <c>CFEPIntro::Render</c> <c>0x0051B866</c>–<c>0x0051B8F4</c>
/// plus the <c>ADD ESP, 0x2C</c> at <c>0x0051B902</c>. Not in the pinned GPL
/// drop (<c>FEPIntro.cpp</c> is absent). This is not attract
/// <c>vectorlosttoyssplash.tga</c> and not TWIMTBP.</para>
///
/// <para><b>Scale.</b> Already pinned by <see cref="RetailClickToStartPrompt"/>:
/// argument <c>min(this+0x18, 1.0)</c>, then
/// <c>((cos(t*π)+1)*0.375)+0.46875</c>. The dest consumes that stored scale.</para>
///
/// <para><b>Dest.</b> After <c>fstp [esp+0x20]</c> at <c>0x0051B8A9</c> the
/// body reloads the scale twice. Y is
/// <c>fmul [0x005E49EC]</c> / <c>fsubr [0x005DBE00]</c> / <c>fsub [0x005E49E8]</c>
/// = <c>(18 − scale×−222) − (−117.9375)</c>. X is
/// <c>fmul [0x005E49E4]</c> / <c>fsubr [0x005E49E0]</c> / <c>fsub [0x005E49DC]</c>
/// = <c>(558 − scale×238) − 126.4375</c>. Settled scale 0.46875 lands at
/// (320, 240). A scale-free centre is not the body.</para>
///
/// <para><b>Pack.</b> Eleven cdecl dwords, right-to-left, into
/// <c>CDXSurf__RenderSurface</c> at <c>0x005563D0</c> (plain <c>RET</c> wrapper
/// that prepends UV defaults 0/1/0/1 and calls
/// <c>CVBufTexture__DrawSpriteEx</c> at <c>0x00555BE0</c>):
/// <c>(X, Y, z bits 0x3F75C28F, 0x0089d880, colour −1, sx=sy=scale, mode 4,
/// 0, 1.0, 0)</c>. Mode 4 is the centre-anchor used by the title slam.</para>
///
/// <para>No Godot types. The 2-D consumer ignores z; the bits stay on the
/// helper so a later depth owner does not have to re-read the body.
/// <c>DrawClickToStart</c> consumes <see cref="X"/>, <see cref="Y"/>, and
/// <see cref="Scale"/>. The LostToys slide and glyphs are wired separately.</para>
/// </summary>
public static class RetailClickToStartSplash
{
    /// <summary>Texture global loaded at <c>0x0051B8C3</c>.</summary>
    public const uint TextureGlobal = 0x0089D880u;

    /// <summary>Fourth-from-last push at <c>0x0051B88D</c>.</summary>
    public const int Mode = 4;

    /// <summary>Colour immediate at <c>0x0051B8C8</c> (<c>push -1</c>).</summary>
    public const uint Color = 0xFFFFFFFFu;

    /// <summary>Z immediate at <c>0x0051B8CB</c>.</summary>
    public const uint ZBits = 0x3F75C28Fu;

    /// <summary>First pushed dword at <c>0x0051B884</c> (last argument).</summary>
    public const float TrailingC = 0f;

    /// <summary>Second pushed dword at <c>0x0051B886</c> (<c>0x3F800000</c>).</summary>
    public const float TrailingB = 1f;

    /// <summary>Third pushed dword at <c>0x0051B88B</c>.</summary>
    public const float TrailingA = 0f;

    /// <summary><c>0x005E49E0</c> = <c>558.0f</c>.</summary>
    public const float BaseX = 558f;

    /// <summary><c>0x005E49E4</c> = <c>238.0f</c>.</summary>
    public const float CoeffX = 238f;

    /// <summary><c>0x005E49DC</c> = <c>126.4375f</c>.</summary>
    public const float OffsetX = 126.4375f;

    /// <summary><c>0x005DBE00</c> = <c>18.0f</c>.</summary>
    public const float BaseY = 18f;

    /// <summary><c>0x005E49EC</c> = <c>−222.0f</c>.</summary>
    public const float CoeffY = -222f;

    /// <summary><c>0x005E49E8</c> = <c>−117.9375f</c>.</summary>
    public const float OffsetY = -117.9375f;

    /// <summary><see cref="ZBits"/> decoded as IEEE-754 single.</summary>
    public static float Z => BitConverter.UInt32BitsToSingle(ZBits);

    /// <summary>
    /// Whether Render would submit the splash. There is no timer skip after
    /// the page-transition==1.0 compare: the call issues at timer 0.
    /// </summary>
    public static bool ShouldDraw(double timer)
    {
        _ = timer;
        return true;
    }

    /// <summary>sx=sy. Same law as <see cref="RetailClickToStartPrompt.SplashScale"/>.</summary>
    public static float Scale(double timer) => RetailClickToStartPrompt.SplashScale(timer);

    /// <summary><c>(558 − scale×238) − 126.4375</c>.</summary>
    public static float X(double timer)
    {
        float scale = Scale(timer);
        return (BaseX - (scale * CoeffX)) - OffsetX;
    }

    /// <summary><c>(18 − scale×−222) − (−117.9375)</c>.</summary>
    public static float Y(double timer)
    {
        float scale = Scale(timer);
        return (BaseY - (scale * CoeffY)) - OffsetY;
    }
}
