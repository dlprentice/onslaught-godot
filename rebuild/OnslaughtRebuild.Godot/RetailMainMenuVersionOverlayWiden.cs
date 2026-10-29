// SPDX-License-Identifier: GPL-3.0-only

namespace OnslaughtRebuild.GodotClient;

/// <summary>
/// CFEPMain::Render version overlay Text__AsciiToWideScratch leftover —
/// recovered from official 74154bfa
/// (a private pristine copy),
/// SHA-256 <c>74154bfae14ddc8ecb87a0766f5bc381c7b7f1ab334ed7a753040eda1e1e7750</c>.
/// File offset = VA − <c>0x400000</c>. Independently re-hashed this
/// cycle (2,506,752 bytes). Official 74154bfa image base is
/// <c>0x400000</c>.
///
/// <para><b>Sibling.</b> Format at <c>0x0046416E</c> and the settled
/// pack at <c>0x004641B1</c> / <c>0x004641B4</c> are already
/// <see cref="RetailMainMenuVersionOverlay"/>. Dest Y
/// (GetWindowHeight-16), dest X push 0, and Z 0.01 are already
/// <see cref="RetailMainMenuVersionOverlayZ"/>. Font slot 1 /
/// Font13PS is already
/// <see cref="RetailMainMenuVersionOverlayFont"/>. Post-draw
/// <c>[0x00679B40]=1</c> at <c>0x004641FC</c> is already
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. Pre-draw
/// <c>[0x00679B40]=0</c> at <c>0x00464180</c> is already
/// <see cref="RetailMainMenuVersionOverlayEnable"/>. This leftover
/// is the cdecl widen of the sprintf buffer. Do not redo the
/// sprintf, the colour OR, dest/Z, the font slot, the pre-draw
/// store, or the post-draw stores.</para>
///
/// <para><b>Call.</b> After the enable-byte store,
/// <c>0x00464187</c> is a push of constant 0, <c>0x00464189</c> is
/// a push of constant 0, <c>0x0046418B</c> is a push of constant 0x447A0000,
/// <c>0x00464190</c> is a push of edx (the
/// address computation (esp+0x3C) into edx sprintf buffer), and
/// <c>0x00464191</c> is a direct call to 0x004F7BF0. The body loads
/// <c>[esp+8]</c> after a push of esi, so one cdecl argument.
/// A stack cleanup of 4 bytes at <c>0x004641A0</c> pops only that
/// argument. EAX is then pushed at <c>0x004641A8</c> as the wide
/// pointer into the DrawTextDynamic setup. The three earlier
/// pushes remain on the stack; this type does not invent dest,
/// wrap, fade, scale, or sheen from them.</para>
///
/// <para><b>Body.</b> <c>0x004F7BF0</c> through near <c>ret</c>
/// at <c>0x004F7C62</c> (not <c>ret n</c>). Ring dword
/// <c>[0x00854D40]</c> increments and wraps at 4. Bank base
/// <c>0x0084CD40</c>. Each slot is a left shift by 0xC wchar units;
/// the returned pointer is a left shift by 0xD bytes from the bank.
/// Per byte: a sign-extending load of bl into di, a bit test of bl against 0x80, then
/// an addition of 0x100 to edi when the high bit is set, store
/// <c>word</c>. The stored unit for <c>0x80</c> is
/// <c>0x0080</c>, not <c>0x0180</c>: the addend undoes the
/// sign-extend so the wide unit is the unsigned byte. Cold
/// <c>V1.00</c> is therefore identity. This type does not model
/// the four-slot scratch lifetime.</para>
///
/// <para><b>Not a fade.</b> The post-call
/// (a float multiply by the value at 0x005D8C70) is already the colour-pack 255 scale
/// in <see cref="RetailMainMenuVersionOverlay"/>. The post-draw
/// (a float compare against the constant 0.0) is already the title-logo shadow clamp in
/// <see cref="RetailMainMenuVersionOverlayFlags"/>. This type
/// does not invent a version fade, a sheen, dest immediates, a
/// wrap width, or a 2px kerning hack. DrawMainMenu keeps
/// title-font DrawText, VersionTint, Format, DestX,
/// DestY(DesignHeight), and scale 1.0.</para>
///
/// <para><b>Not dest/Z/font/flags/enable.</b> Dest Y stays helper
/// minus 16. Dest X stays 0. Z stays 0.01. Font slot stays 1 /
/// Font13PS. Post-draw restore stays 1. Pre-draw store stays 0.
/// Not <c>SetLanguage</c>. HandleKey, DrawLoading,
/// DrawQuitConfirm, HandlePointerConfirm, HandlePointerMotion, the
/// cursor, Apply, dropdown, the colour AND, the writing-chrome Y,
/// the writing-chrome colour, the writing-chrome Z/X, the sine
/// pin, the blink, the chevron colour, the label colour, the
/// selector-bar colour, the selector-bar Z/X, the version
/// format/colour, the version dest/Z, the version font slot, the
/// version post-draw flags, the version pre-draw enable, the
/// title-logo shadow dest/Z, the title-logo body dest/Z, the
/// selected-row icon colours, and the 0x00463873 / 0x004638B7 /
/// 0x00463A8F / 0x00463AD3 / 0x00463D1F / 0x00463D63 /
/// 0x00463F3F / 0x00463F83 pair stay untouched.</para>
/// </summary>
public static class RetailMainMenuVersionOverlayWiden
{
    /// <summary>Already <see cref="RetailMainMenuVersionOverlayEnable"/>.</summary>
    public const uint EnableSiblingSite = 0x00464180u;

    /// <summary>A push of constant 0 immediately after the enable-byte store.</summary>
    public const uint FirstLeftoverPushSite = 0x00464187u;

    /// <summary>Second push of constant 0. Not an argument of this call.</summary>
    public const uint SecondLeftoverPushSite = 0x00464189u;

    /// <summary>A push of constant 0x447A0000. Not an argument of this call.</summary>
    public const uint FloatLeftoverPushSite = 0x0046418Bu;

    /// <summary>IEEE bits of the leftover float push. Not dest or wrap.</summary>
    public const uint FloatLeftoverBits = 0x447A0000u;

    /// <summary>A push of edx of the sprintf buffer.</summary>
    public const uint ArgPushSite = 0x00464190u;

    /// <summary>A direct call to 0x004F7BF0 at <c>0x00464191</c>.</summary>
    public const uint CallSite = 0x00464191u;

    /// <summary><c>Text__AsciiToWideScratch</c>.</summary>
    public const uint AsciiToWideScratch = 0x004F7BF0u;

    /// <summary>Near <c>ret</c> of the widen body. Not <c>ret n</c>.</summary>
    public const uint BodyRetSite = 0x004F7C62u;

    /// <summary>First byte of the near ret.</summary>
    public const byte BodyRetOpcode = 0xC3;

    /// <summary>A stack cleanup of 4 bytes after the call.</summary>
    public const uint AddEspSite = 0x004641A0u;

    /// <summary>One cdecl dword popped. The three leftover pushes stay.</summary>
    public const int AddEspImmediate = 4;

    /// <summary>The body consumes one stack pointer.</summary>
    public const int ArgCount = 1;

    /// <summary>A push of eax of the returned wide pointer.</summary>
    public const uint ReturnPushSite = 0x004641A8u;

    /// <summary>Already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const uint ColorSiblingSite = 0x004641B1u;

    /// <summary>Four-slot ring index. Lifetime is not this leftover.</summary>
    public const uint RingGlobal = 0x00854D40u;

    /// <summary>Wide scratch bank base.</summary>
    public const uint BankGlobal = 0x0084CD40u;

    /// <summary>Ring wraps when the incremented index reaches this.</summary>
    public const int SlotCount = 4;

    /// <summary>A left shift of ebx by 0xC wchar units per slot.</summary>
    public const int SlotIndexShift = 0xC;

    /// <summary>A left shift of eax by 0xD byte return offset.</summary>
    public const int ReturnShift = 0xD;

    /// <summary>A bit test of bl against 0x80.</summary>
    public const int HighBitMask = 0x80;

    /// <summary>An addition of 0x100 to edi undoes <c>movsx</c> for high bytes.</summary>
    public const int HighBitAddend = 0x100;

    /// <summary>Already the enable leftover's sprintf-buffer lea.</summary>
    public const int SprintfLeaDisp = 0x3C;

    /// <summary>This leftover owns the cdecl widen call.</summary>
    public const bool OwnsCall = true;

    /// <summary>The three pre-call pushes are siblings, not this leftover.</summary>
    public const bool OwnsLeftoverPushes = false;

    /// <summary>The rotating 4-slot bank lifetime is not modelled here.</summary>
    public const bool OwnsRingLifetime = false;

    /// <summary>The 2px MeasureText residual is not this leftover.</summary>
    public const bool InventsKerningHack = false;

    /// <summary>Hover writes <c>-1</c>. It does not call <c>SetLanguage</c>.</summary>
    public const bool IsSetLanguage = false;

    /// <summary>Render widen is not <c>CFrontEnd::ReceiveButtonAction</c>.</summary>
    public const bool IsButtonPressed = false;

    /// <summary>0x00464343 already owns the title-logo sheen.</summary>
    public const bool InventsSheen = false;

    /// <summary>The leftover is a widen, not a title-logo scale.</summary>
    public const bool InventsTitleLogoScale = false;

    /// <summary>Dest Y stays helper minus 16. Do not invent a 464 immediate.</summary>
    public const bool InventsDestImmediates = false;

    /// <summary>The leftover float push is not claimed as a wrap width.</summary>
    public const bool InventsWrapWidth = false;

    /// <summary>The leftover is a widen, not a version fade.</summary>
    public const bool InventsFade = false;

    /// <summary>0x0046416E is already <see cref="RetailMainMenuVersionOverlay"/>.</summary>
    public const bool RedoesVersionOverlay = false;

    /// <summary>0x004641C9 is already <see cref="RetailMainMenuVersionOverlayZ"/>.</summary>
    public const bool RedoesVersionOverlayZ = false;

    /// <summary>0x004641E4 is already <see cref="RetailMainMenuVersionOverlayFont"/>.</summary>
    public const bool RedoesVersionOverlayFont = false;

    /// <summary>0x004641FC is already <see cref="RetailMainMenuVersionOverlayFlags"/>.</summary>
    public const bool RedoesVersionOverlayFlags = false;

    /// <summary>0x00464180 is already <see cref="RetailMainMenuVersionOverlayEnable"/>.</summary>
    public const bool RedoesVersionOverlayEnable = false;

    /// <summary>0x004642CE is already <see cref="RetailMainMenuTitleLogoZ"/>.</summary>
    public const bool RedoesTitleLogoZ = false;

    /// <summary>0x00464251 is already <see cref="RetailMainMenuTitleLogoShadowZ"/>.</summary>
    public const bool RedoesTitleLogoShadowZ = false;

    /// <summary>0x0046424F is already <see cref="RetailMainMenuTitleLogoShadow"/>.</summary>
    public const bool RedoesTitleLogoShadow = false;

    /// <summary>0x00462FED is already <see cref="RetailMainMenuSelectorBarZ"/>.</summary>
    public const bool RedoesSelectorBarZ = false;

    /// <summary>0x00462DFF is already <see cref="RetailMainMenuWritingZ"/>.</summary>
    public const bool RedoesWritingZ = false;

    /// <summary>0x00463E8D is the D8A4 twin gate. This leftover sits after it.</summary>
    public const bool UsesTwinFadeGate = false;

    /// <summary>
    /// Leftover float bits as IEEE-754 single. Not dest and not wrap.
    /// </summary>
    public static float FloatLeftover => BitConverter.UInt32BitsToSingle(FloatLeftoverBits);

    /// <summary>
    /// Stored wide unit for one source byte: <c>movsx</c>, then
    /// <c>+0x100</c> when the high bit is set, then the low 16 bits.
    /// High byte <c>0x80</c> stores <c>0x0080</c>, not <c>0x0180</c>.
    /// </summary>
    public static int WidenUnit(byte source)
    {
        int unit = unchecked((sbyte)source);
        if ((source & HighBitMask) != 0)
        {
            unit += HighBitAddend;
        }

        return unit & 0xFFFF;
    }

    /// <summary>
    /// Per-byte widen of the sprintf buffer. Cold <c>V1.00</c> is
    /// identity. The rotating scratch bank is not modelled.
    /// </summary>
    public static string Widen(string ascii)
    {
        if (ascii.Length == 0)
        {
            return string.Empty;
        }

        char[] units = new char[ascii.Length];
        for (int i = 0; i < ascii.Length; i++)
        {
            units[i] = (char)WidenUnit(unchecked((byte)ascii[i]));
        }

        return new string(units);
    }
}
